"""Build a local, read-only-source IIDX art catalogue and sprite browser."""

import argparse
from collections import Counter
from datetime import datetime, timedelta, timezone
import hashlib
import importlib.metadata
import json
import os
from pathlib import Path
import re
import shutil
import sys
from urllib.parse import quote

sys.dont_write_bytecode = True

CATEGORIES = (
    (r"gmframe.*", "框体"),
    (r"notes.*|lnote.*", "音符与长条"),
    (r"judge.*", "判定"),
    (r"beam.*", "光柱"),
    (r"exp\d+", "击打演出"),
    (r"fcombo.*", "全连演出"),
    (r"turn.*", "转盘"),
    (r"lane\d+(?:_anim)?", "挡板候选"),
    (r"gameparts.*|basic_frame.*|common.*|graph.*", "共用界面组件"),
    (r"mselect.*|navigate_mselect.*|opt_select.*|notes_radar.*", "选曲与选项"),
    (r"result.*|d_result.*", "结算"),
    (r"\d+|i_\d+.*", "歌曲相关候选"),
    (r"qp.*|qpro.*|story_chara.*", "角色"),
    (r"sub_.*", "副屏与定制预览"),
)


def category(path):
    for pattern, label in CATEGORIES:
        if re.fullmatch(pattern, path.stem):
            return label
    return "其它美术"


def candidate_style(path):
    match = re.fullmatch(r"gmframe(\d{1,2})(?:_om)?", path.stem)
    return int(match[1]) if match else None


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def inventory(game_root, graphic_root="contents/data/graphic"):
    graphic = game_root / graphic_root
    roots = [graphic]
    mods = game_root / "contents/data_mods"
    if mods.is_dir():
        roots.extend(sorted(p / "graphic" for p in mods.iterdir()
                            if p.name != "_cache" and (p / "graphic").is_dir()))
    rows = []
    for root in roots:
        for path in sorted(root.rglob("*")):
            if not path.is_file():
                continue
            if not path.resolve().is_relative_to(game_root):
                raise ValueError(f"Graphic source escapes the game root: {path}")
            stat = path.stat()
            rows.append({"path": path.relative_to(game_root).as_posix(),
                         "bytes": stat.st_size, "mtimeNs": stat.st_mtime_ns,
                         "extension": path.suffix.lower(), "category": category(path),
                         "candidateStyle": candidate_style(path)})
    return rows


def save_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def byte_preserving_ifs(ifs_class):
    """Opt-in reader for binary texture names that cannot be represented as text.

    Only image names may use opaque storage identifiers. The original bytes,
    rather than a replacement character or guessed encoding, select the MD5
    payload. Other metadata and all pixel/AFP/GEO decoding remain ifstools'.
    No installed module or source archive is patched.
    """
    from kbinxml import KBinXML
    from kbinxml.bytebuffer import ByteBuffer
    from ifstools.ifs import FileBlob, IFSFlags, SIGNATURE
    from ifstools.handlers.generic_file import GenericFile
    from ifstools.handlers.generic_folder import GenericFolder
    from ifstools.handlers.tex_folder import TexFolder

    class TextureNames(KBinXML):
        def __init__(self, data):
            self.raw_strings = {}
            self.opaque_names = set()
            super().__init__(data)

        def data_grab_string(self):
            encoded = bytes(self.data_grab_auto())
            if not encoded or encoded[-1] != 0 or b"\0" in encoded[:-1]:
                raise ValueError("Texture metadata string is not NUL terminated or contains embedded NUL")
            raw = encoded[:-1]
            try:
                value = raw.decode(self.encoding)
            except UnicodeDecodeError:
                value = "raw_name_" + hashlib.sha256(raw).hexdigest()
                self.opaque_names.add(value)
            if value in self.raw_strings and self.raw_strings[value] != raw:
                raise ValueError("Texture names have colliding text or opaque identifiers")
            self.raw_strings[value] = raw
            return value

    class PreservedTextureList(GenericFile):
        def _load_from_ifs(self, convert_kbin=True, **kwargs):
            if convert_kbin:
                return self.byte_names.to_text().encode("utf-8")
            return super()._load_from_ifs(convert_kbin=False, **kwargs)

    class TextureFolder(TexFolder):
        def tree_complete(self):
            info = next((f for n, f in self.files.items() if n.endswith(".xml")), None)
            if info is None:
                return super().tree_complete()
            original = info.load(convert_kbin=False)
            if not KBinXML.is_binary_xml(original):
                return super().tree_complete()
            names = TextureNames(original)
            if not names.opaque_names:
                return super().tree_complete()

            # Reject malformed values elsewhere; an arbitrary damaged field is
            # not made valid just because it can be assigned a safe filename.
            image_names = set()
            for element in names.xml_doc.iter():
                for key, value in element.attrib.items():
                    if value in names.opaque_names:
                        if element.tag != "image" or key != "name":
                            raise ValueError("Invalid encoded string outside a texture image name")
                        image_names.add(value)
                if element.text in names.opaque_names or element.tail in names.opaque_names:
                    raise ValueError("Invalid encoded string outside a texture image name")
            if image_names != names.opaque_names:
                raise ValueError("Unaccounted invalid texture metadata string")

            GenericFolder.tree_complete(self)
            self.folders.pop("_cache", None)
            self.info_file, self.info_kbin = info, names
            self.encoding_override = None
            self.original_texturelist = original
            self.texture_name_map = []
            hashed_files = {n for n in self.files if re.fullmatch(r"[0-9a-f]{32}", n)}
            matched, storage_names = set(), set()
            for element in names.xml_doc.findall(".//image"):
                name = element.attrib["name"]
                if (not name or re.search(r'[<>:"/\\|?*\x00-\x1f]', name)
                        or name.endswith((".", " "))
                        or re.match(r"(?i)^(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)", name)):
                    raise ValueError("Unsafe texture storage name")
                if name.casefold() in storage_names:
                    raise ValueError("Texture storage names collide on Windows")
                storage_names.add(name.casefold())
                raw = names.raw_strings[name]
                key = hashlib.md5(raw).hexdigest()
                if key not in hashed_files or key in matched:
                    raise ValueError("Texture name bytes do not identify one original MD5 payload")
                if name + ".png" in self.files:
                    raise ValueError("Texture storage name collides with an archive entry")
                matched.add(key)
                record = {"encoding": names.encoding, "rawNameBytesHex": raw.hex(),
                          "md5Entry": key, "decodedName": None if name in names.opaque_names else name,
                          "storageIdentifier": name}
                node = self.files.pop(key)
                node.name = name + ".png"
                node.texture_name_identity = record
                self.files[node.name] = node
                self.texture_name_map.append(record)
            if matched != hashed_files:
                raise ValueError("Unmapped original texture MD5 payloads")
            info.__class__ = PreservedTextureList
            info.byte_names = names
            self.compress = names.xml_doc.attrib.get("compress")
            self._create_images()

    class RootFolder(GenericFolder):
        def from_xml(self, element):
            # GenericFolder initialises handlers before dispatching from_xml.
            self.folder_handlers["tex"] = TextureFolder
            return super().from_xml(element)

    class PreservedIFS(ifs_class):
        def load_ifs(self, path, super_disable=False, super_skip_bad=False, super_abort_if_bad=False):
            self.is_file = True
            self.ifs_out = Path(path).name
            self.folder_out = Path(path).stem + "_ifs"
            self.default_out = self.folder_out
            self.file = open(path, "rb")
            try:
                header = ByteBuffer(self.file.read(36))
                if header.get_u32() != SIGNATURE:
                    raise ValueError("Given file was not an IFS file")
                self.flags = IFSFlags(header.get_u16())
                if header.get_u16() ^ self.flags != 0xFFFF:
                    raise ValueError("Invalid IFS flags complement")
                self.time = header.get_u32()
                header.get_u32()  # tree allocation size, not reliable in repacked archives
                manifest_end = header.get_u32()
                self.manifest_md5 = header.get_bytes(16) if self.flags & IFSFlags.HasMD5 else None
                if not header.offset <= manifest_end <= os.fstat(self.file.fileno()).st_size:
                    raise ValueError("IFS manifest is outside the archive")
                self.file.seek(header.offset)
                manifest = self.file.read(manifest_end - header.offset)
                if not KBinXML.is_binary_xml(manifest):
                    manifest = manifest.rstrip(b"\0")
                self.manifest = KBinXML(manifest)
                self.data_blob = FileBlob(self.file, manifest_end)
                self.tree = RootFolder(self.data_blob, self.manifest.xml_doc,
                                       super_disable=super_disable, super_skip_bad=super_skip_bad,
                                       super_abort_if_bad=super_abort_if_bad)
            except Exception:
                self.file.close()
                raise

    return PreservedIFS


def extract_copy(copied, output, image_class, ifs_class, cropped, preserve_raw_texture_names=False):
    reader = byte_preserving_ifs(ifs_class) if preserve_raw_texture_names else ifs_class
    archive = reader(str(copied), super_abort_if_bad=True)
    try:
        for node in [*archive.tree.all_folders, *archive.tree.all_files]:
            if not (output / node.full_path).resolve().is_relative_to(output.resolve()):
                raise ValueError("IFS entry escapes its extraction directory")
        archive.extract(path=str(output), progress=False, recurse=False,
                        extract_manifest=not cropped, rename_dupes=True,
                        tex_only=cropped, crop_to_uvrect=cropped)
        if not cropped:
            for folder in archive.tree.all_folders:
                if not hasattr(folder, "texture_name_map"):
                    continue
                original = output / folder.full_path / "texturelist.original.bin"
                mapping = output / folder.full_path / "texturelist.name-map.json"
                if original.exists() or mapping.exists():
                    raise ValueError("Texture name evidence would overwrite an archive entry")
                original.write_bytes(folder.original_texturelist)
                save_json(mapping, {"originalMetadataSha256": hashlib.sha256(folder.original_texturelist).hexdigest(),
                                    "images": folder.texture_name_map})
        metadata = {}
        for node in archive.tree.all_files:
            if not isinstance(node, image_class):
                continue
            info = {"rawPath": node.full_path.replace("\\", "/"),
                    "imageRect": node.imgrect, "uvRect": node.uvrect}
            if hasattr(node, "texture_name_identity"):
                info["nameIdentity"] = node.texture_name_identity
            metadata[node.name] = info
        return metadata
    finally:
        archive.close()


def build(args, parser):
    game_root = args.game_root.resolve(strict=True)
    output = args.output.resolve()
    if output.is_relative_to(game_root) or game_root.is_relative_to(output):
        parser.error("Output and game root must be separate, non-overlapping directories.")
    if output.drive.lower() == os.environ.get("SystemDrive", "C:").lower():
        parser.error("Use a non-system drive for the local art library.")
    if output.exists():
        parser.error("Output already exists; existing libraries are never overwritten.")
    if not output.parent.is_dir():
        parser.error("The output parent directory must already exist.")
    graphic_root = getattr(args, "graphic_root", "contents/data/graphic")
    graphic = game_root / graphic_root
    if not graphic.is_dir():
        parser.error(f"The selected graphic root does not exist: {graphic_root}")
    if args.ifstools_path:
        sys.path.insert(0, str(args.ifstools_path.resolve(strict=True)))
    from ifstools import IFS
    from ifstools.handlers.image_file import ImageFile
    from PIL import Image

    selection = json.loads(args.selection.read_text(encoding="utf-8"))
    if not isinstance(selection, list) or not selection or not all(isinstance(p, str) for p in selection):
        parser.error("Selection must be a non-empty JSON array of relative IFS paths.")
    if len(selection) != len(set(selection)):
        parser.error("Selection contains duplicate archive paths.")
    sources = []
    for relative in selection:
        source = (game_root / relative).resolve(strict=True)
        if not source.is_relative_to(graphic.resolve()) or source.suffix.lower() != ".ifs":
            parser.error(f"Selected input must be an IFS inside {graphic_root}: {relative}")
        sources.append(source)

    before = inventory(game_root, graphic_root)
    selected_bytes = sum(p.stat().st_size for p in sources)
    # Copies, two image representations and decoder work need working headroom.
    if shutil.disk_usage(output.parent).free < selected_bytes * 4 + 256 * 1024 * 1024:
        parser.error("Insufficient working space for this archive selection.")
    output.mkdir()
    save_json(output / "source-inventory.json", before)
    save_json(output / "run.json", {"status": "running", "selection": selection})
    packages, textures = [], []
    for source in sources:
        relative = source.relative_to(game_root)
        before_hash = digest(source)
        copied = output / "source-copies" / relative
        copied.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, copied)
        if digest(copied) != before_hash:
            raise RuntimeError("Source copy did not match the original archive")
        package_root = output / "packages" / relative.with_suffix("")
        raw, sprites = package_root / "raw", package_root / "sprites"
        raw.parent.mkdir(parents=True, exist_ok=False)
        preserve_names = getattr(args, "preserve_raw_texture_names", False)
        metadata = extract_copy(copied, raw, ImageFile, IFS, cropped=False,
                                preserve_raw_texture_names=preserve_names)
        extract_copy(copied, sprites, ImageFile, IFS, cropped=True,
                     preserve_raw_texture_names=preserve_names)
        raw_files = sorted(p for p in raw.rglob("*") if p.is_file())
        sprite_files = sorted(sprites.glob("*.png"))
        for path in raw.rglob("*.png"):
            with Image.open(path) as image:
                image.verify()
        for path in sprite_files:
            with Image.open(path) as image:
                width, height = image.size
                image.verify()
            info = metadata[path.name]
            image_rect, uv_rect = info["imageRect"], info["uvRect"]
            if (width, height) != (uv_rect[1] - uv_rect[0], uv_rect[3] - uv_rect[2]):
                raise RuntimeError("Cropped sprite dimensions do not match texture UV metadata")
            texture = {"package": relative.as_posix(), "name": path.name,
                             "category": category(source), "candidateStyle": candidate_style(source),
                             "width": width, "height": height,
                             "imageRect": image_rect, "uvRect": uv_rect,
                             "sprite": quote(path.relative_to(output).as_posix(), safe="/"),
                             "raw": quote((raw / info["rawPath"]).relative_to(output).as_posix(), safe="/"),
                             "sha256": digest(path)}
            if "nameIdentity" in info:
                texture["nameIdentity"] = info["nameIdentity"]
            textures.append(texture)
        after_hash = digest(source)
        if after_hash != before_hash:
            raise RuntimeError("Original archive changed while the library was being built")
        package = {"source": relative.as_posix(), "category": category(source),
                   "candidateStyle": candidate_style(source), "sha256": before_hash,
                   "sourceUnchanged": True, "spriteCount": len(sprite_files),
                   "rawFileCount": len(raw_files),
                   "rawExtensions": dict(Counter(p.suffix.lower() for p in raw_files))}
        recovered_names = [info["nameIdentity"] for info in metadata.values()
                           if "nameIdentity" in info and info["nameIdentity"]["decodedName"] is None]
        if recovered_names:
            package["bytePreservedTextureNames"] = recovered_names
            print(f"{relative.as_posix()}: preserved {len(recovered_names)} undecodable texture names by original bytes", flush=True)
        packages.append(package)
        save_json(output / "packages.json", packages)
        print(f"{relative.as_posix()}: {len(sprite_files)} verified UV sprites", flush=True)

    if inventory(game_root, graphic_root) != before:
        raise RuntimeError("Graphic source file names, sizes or last-write times changed")
    hashes = Counter(t["sha256"] for t in textures)
    result = {"createdAt": datetime.now(timezone(timedelta(hours=8))).isoformat(),
              "tool": "ifstools", "toolVersion": importlib.metadata.version("ifstools"),
              "status": "complete", "sourceMetadataUnchanged": True, "graphicRoot": graphic_root,
              "sourceFileCount": len(before), "packages": packages, "textures": textures,
              "uniqueSpriteContents": len(hashes),
              "note": "Filename-based version candidates; extraction does not establish complete skin coverage."}
    save_json(output / "library.json", result)
    template = Path(__file__).with_name("browser.html").read_text(encoding="utf-8")
    embedded = json.dumps(result, ensure_ascii=False).replace("<", "\\u003c").replace(">", "\\u003e")
    (output / "index.html").write_text(template.replace("__LIBRARY_DATA__", embedded), encoding="utf-8")
    save_json(output / "run.json", {key: value for key, value in result.items()
                                    if key not in ("packages", "textures")})
    print(f"Complete: {len(packages)} archives, {len(textures)} verified sprites; source unchanged.")
    print(output / "index.html")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-root", type=Path, required=True)
    parser.add_argument("--graphic-root", choices=("contents/data/graphic", "data/graphic"),
                        default="contents/data/graphic", help="Actual source layout; does not rename archives or infer game versions")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--selection", type=Path, default=Path(__file__).with_name("selection-33-hdd.json"))
    parser.add_argument("--ifstools-path", type=Path)
    parser.add_argument("--preserve-raw-texture-names", action="store_true",
                        help="Explicitly preserve undecodable binary texture names by raw bytes and exact MD5 payload; retain original metadata and a name map")
    build(parser.parse_args(), parser)
