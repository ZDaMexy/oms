"""Synthetic IFS fixtures for opt-in, byte-preserving texture-name recovery."""
import contextlib
import hashlib
import io
import json
from pathlib import Path
import struct
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from build_library import byte_preserving_ifs, extract_copy

from ifstools import IFS
from ifstools.handlers.image_file import ImageFile
from ifstools.handlers.node import Node
from kbinxml import KBinXML
from lxml import etree
from PIL import Image


BAD_NAME = b"label_\x81\x30\xed\xa0\x80"


def make_archive(path, names, *, wrong_entry=False, extra_entry=False, bad_format=False,
                 image_format="argb8888rev"):
    """Create a tiny archive without any original-game data or real source hash."""
    texturelist = etree.Element("texturelist")
    atlas = etree.SubElement(texturelist, "texture", name="atlas", format="f0" if bad_format else image_format)
    etree.SubElement(atlas, "size", __type="2u16").text = "4 4"
    replacements = []
    for index, raw in enumerate(names):
        placeholder = "n" + str(index).zfill(len(raw) - 1)
        if len(placeholder) != len(raw):
            raise ValueError("Fixture requires enough bytes for a unique placeholder")
        element = etree.SubElement(atlas, "image", name=placeholder)
        etree.SubElement(element, "imgrect", __type="4u16").text = "0 8 0 8"
        etree.SubElement(element, "uvrect", __type="4u16").text = "2 6 2 6"
        replacements.append((placeholder.encode(), raw))
    if bad_format:
        replacements.append((b"f0", b"\x81\x30"))
    original_metadata = KBinXML(texturelist).to_binary()
    for placeholder, raw in replacements:
        assert original_metadata.count(placeholder + b"\0") == 1
        original_metadata = original_metadata.replace(placeholder + b"\0", raw + b"\0")

    pixels = b"".join(bytes((x * 11, y * 17, x + y * 4, 255)) for y in range(4) for x in range(4))
    files = [("payload.bin", b"unchanged non-texture data"), ("tex/texturelist.xml", original_metadata)]
    for index, raw in enumerate(names):
        key = hashlib.md5(b"different-payload" if wrong_entry and index == 0 else raw).hexdigest()
        files.append(("tex/" + key, pixels))
    if extra_entry:
        files.append(("tex/" + hashlib.md5(b"unmapped-payload").hexdigest(), pixels))
    manifest = etree.Element("imgfs")
    texture_folder = etree.SubElement(manifest, "tex", __type="s32")
    texture_folder.text = "1"
    payload = bytearray()
    for name, data in files:
        parts = name.split("/")
        parent = texture_folder if len(parts) == 2 else manifest
        element = etree.SubElement(parent, Node.sanitize_name(parts[-1]), __type="3s32")
        element.text = f"{len(payload)} {len(data)} 1"
        payload.extend(data)
    metadata = KBinXML(manifest)
    binary_manifest = metadata.to_binary()
    header = struct.pack(">IHHIII", 0x6CAD8F89, 3, 3 ^ 0xFFFF, 1, metadata.mem_size, 36 + len(binary_manifest))
    path.write_bytes(header + hashlib.md5(binary_manifest).digest() + binary_manifest + payload)
    return original_metadata


class BytePreservingTextureNameTest(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="oms-texture-byte-names-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.source = self.root / "synthetic.ifs"

    def extract(self, name, *, preserve=True, cropped=False):
        output = self.root / name
        metadata = extract_copy(self.source, output, ImageFile, IFS, cropped,
                                preserve_raw_texture_names=preserve)
        return output, metadata

    def assert_reader_error(self, pattern):
        with self.assertRaisesRegex(ValueError, pattern):
            byte_preserving_ifs(IFS)(str(self.source), super_abort_if_bad=True)
        # Failed construction closes its handle, so an author can fix/retry input.
        with self.source.open("r+b") as stream:
            self.assertEqual(stream.read(4), b"\x6c\xad\x8f\x89")

    def test_normal_ascii_and_shift_jis_names_are_identical_in_both_modes(self):
        make_archive(self.source, [b"white_note", "青ノート".encode("cp932")])
        before = self.source.read_bytes()
        ordinary, ordinary_metadata = self.extract("ordinary", preserve=False)
        preserving, preserving_metadata = self.extract("preserving")
        ordinary_uv, uv_metadata = self.extract("ordinary-uv", preserve=False, cropped=True)
        preserving_uv, preserved_uv_metadata = self.extract("preserving-uv", cropped=True)
        self.assertEqual(ordinary_metadata, preserving_metadata)
        self.assertEqual(uv_metadata, preserved_uv_metadata)
        self.assertEqual(self.source.read_bytes(), before)
        for relative in sorted(p.relative_to(ordinary) for p in ordinary.rglob("*") if p.is_file()):
            self.assertEqual((ordinary / relative).read_bytes(), (preserving / relative).read_bytes())
        self.assertEqual({p.name for p in ordinary_uv.glob("*.png")}, {p.name for p in preserving_uv.glob("*.png")})
        for name in ordinary_metadata:
            with Image.open(ordinary_uv / name) as left, Image.open(preserving_uv / name) as right:
                self.assertEqual(left.size, (2, 2))
                self.assertEqual(left.getpixel((0, 0)), (5, 17, 11, 255))
                self.assertEqual(left.tobytes(), right.tobytes())
            self.assertEqual(ordinary_metadata[name]["imageRect"], [0, 4, 0, 4])
            self.assertEqual(ordinary_metadata[name]["uvRect"], [1, 3, 1, 3])

    def test_invalid_name_preserves_exact_bytes_payload_and_uv_without_guessing(self):
        original = make_archive(self.source, [BAD_NAME, b"normal_note"])
        before = self.source.read_bytes()
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            with self.assertRaises(UnicodeDecodeError):
                self.extract("strict", preserve=False)
        output, metadata = self.extract("raw")
        uv, _ = self.extract("uv", cropped=True)
        identifier = "raw_name_" + hashlib.sha256(BAD_NAME).hexdigest()
        image = metadata[identifier + ".png"]
        self.assertEqual(image["nameIdentity"], {
            "encoding": "cp932", "rawNameBytesHex": BAD_NAME.hex(),
            "md5Entry": hashlib.md5(BAD_NAME).hexdigest(), "decodedName": None,
            "storageIdentifier": identifier,
        })
        self.assertEqual((output / "tex/texturelist.original.bin").read_bytes(), original)
        mapping = json.loads((output / "tex/texturelist.name-map.json").read_text(encoding="utf-8"))
        self.assertEqual(mapping["originalMetadataSha256"], hashlib.sha256(original).hexdigest())
        self.assertEqual(len(mapping["images"]), 2)
        self.assertEqual((output / "payload.bin").read_bytes(), b"unchanged non-texture data")
        with Image.open(uv / (identifier + ".png")) as pixels:
            self.assertEqual(pixels.size, (2, 2))
            self.assertEqual(pixels.getpixel((0, 0)), (5, 17, 11, 255))
        self.assertEqual(self.source.read_bytes(), before)

    def test_missing_original_md5_entry_is_not_guessed(self):
        make_archive(self.source, [BAD_NAME], wrong_entry=True)
        self.assert_reader_error("one original MD5 payload")

    def test_unmapped_texture_payload_is_not_silently_omitted(self):
        make_archive(self.source, [BAD_NAME], extra_entry=True)
        self.assert_reader_error("Unmapped original texture MD5 payloads")

    def test_invalid_non_name_metadata_is_not_recovered(self):
        make_archive(self.source, [b"normal_note"], bad_format=True)
        self.assert_reader_error("outside a texture image name")

    def test_opaque_identifier_collision_is_rejected(self):
        identifier = ("raw_name_" + hashlib.sha256(BAD_NAME).hexdigest()).encode()
        make_archive(self.source, [BAD_NAME, identifier])
        self.assert_reader_error("colliding text or opaque identifiers")

    def test_case_insensitive_filename_collision_is_rejected(self):
        make_archive(self.source, [BAD_NAME, b"same", b"SAME"])
        self.assert_reader_error("collide on Windows")

    def test_recovery_does_not_allow_path_or_device_names(self):
        for name in [b"../escape", b"nul", b"a:b", b"tail."]:
            with self.subTest(name=name):
                make_archive(self.source, [BAD_NAME, name])
                self.assert_reader_error("Unsafe texture storage name")
                self.assertFalse((self.root.parent / "escape.png").exists())

    def test_manifest_bounds_failure_is_not_recovered(self):
        make_archive(self.source, [BAD_NAME])
        source = bytearray(self.source.read_bytes())
        struct.pack_into(">I", source, 16, len(source) + 1)
        self.source.write_bytes(source)
        self.assert_reader_error("manifest is outside")

    def test_unknown_pixel_format_still_fails(self):
        make_archive(self.source, [BAD_NAME], image_format="unsupported_synthetic")
        with self.assertRaisesRegex(NotImplementedError, "Unknown format"):
            self.extract("unknown-format")


if __name__ == "__main__":
    unittest.main()
