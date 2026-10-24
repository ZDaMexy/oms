"""Make small, honest option illustrations from each contribution's own art.

These PNGs illustrate a design; they are not extra gameplay textures.
No original texture, recorded animation or material declaration is modified.
"""

from __future__ import annotations

from collections import Counter
import hashlib
from io import BytesIO
import json
from pathlib import Path
import re

from PIL import Image, ImageChops, ImageDraw, ImageStat

MODULES = frozenset(("frame", "notes", "charge-notes", "judgement", "explosion", "key-beam", "turntable",
                     "lane-cover", "graph-area", "lift-art", "full-combo"))
SIZE = (256, 160)
MAXIMUM_IMAGE_BYTES = 256 * 1024
MAXIMUM_TOTAL_BYTES = 32 * 1024 * 1024


def preview_path(module: str, choice: str) -> str:
    if not all(re.fullmatch(r"[a-z0-9][a-z0-9-]*", value) for value in (module, choice)):
        raise ValueError("A preview identity must be a simple author identifier")
    # These three catalogs select the same original cover art. Sharing the PNG
    # also shares the captured byte/header object, rather than tripling its cost.
    if module in ("graph-area", "lift-art") and choice != "blank":
        module = "lane-cover"
    return f"previews/{module}/{choice}.png"


def exact_file(root: Path, relative: str) -> Path:
    path = (root / relative).resolve(strict=True)
    if not path.is_relative_to(root) or not path.is_file():
        raise ValueError(f"Preview input escapes its author package: {relative}")
    return path


def read_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def walk(node):
    yield node
    for child in node.get("children", []):
        yield from walk(child)


def visible_intensity(image):
    red, green, blue, alpha = image.convert("RGBA").split()
    return ImageChops.multiply(ImageChops.lighter(ImageChops.lighter(red, green), blue), alpha)


def centred(image: Image.Image, size=SIZE, maximum_scale=3):
    scale = min(size[0] / image.width, size[1] / image.height, maximum_scale)
    dimensions = tuple(max(1, round(value * scale)) for value in image.size)
    if dimensions != image.size:
        image = image.resize(dimensions, Image.Resampling.NEAREST)
    canvas = Image.new("RGBA", size)
    canvas.alpha_composite(image, ((size[0] - image.width) // 2, (size[1] - image.height) // 2))
    return canvas


class Illustration:
    def __init__(self, root: Path, choice, original_library: Path | None = None):
        self.root = root
        self.choice = choice
        self.original_library = original_library
        self.manifest = read_json(exact_file(root, choice["manifest"]))
        self.scene = read_json(exact_file(root, choice["scene"]))
        self.resources = {item["id"]: item for item in self.manifest["resources"]}
        self.animations = {item["id"]: item for item in self.manifest.get("animations", [])}
        self.sources = []

    def image(self, identifier: str, rect=None):
        relative = self.resources[identifier]["path"]
        path = exact_file(self.root, relative)
        with Image.open(path) as source:
            image = source.convert("RGBA")
        if rect is not None:
            left, top, width, height = rect
            if min(left, top) < 0 or min(width, height) <= 0 or left + width > image.width or top + height > image.height:
                raise ValueError("A preview glyph rectangle escapes its original texture")
            image = image.crop((left, top, left + width, top + height))
        self.sources.append({"path": relative, "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                             "rect": rect})
        return image

    def template(self, ending):
        roots = [item["root"] for item in self.scene["templates"] if item["root"]["id"].endswith(ending)]
        if len(roots) != 1:
            raise ValueError(f"An illustration requires one exact source template: {ending}")
        return roots[0]

    def identifiers(self, node):
        values = []
        for item in walk(node):
            if item.get("resource"):
                values.append(item["resource"])
            if item.get("animation"):
                values.extend(self.animations[item["animation"]]["resources"])
        return list(dict.fromkeys(values))

    def strongest(self, identifiers):
        """Select an actual non-empty source bitmap, never a class placeholder."""
        candidates = []
        for identifier in identifiers:
            path = exact_file(self.root, self.resources[identifier]["path"])
            with Image.open(path) as source:
                # Additive source sprites often have an opaque black canvas.
                # Count visible colour, rather than selecting the largest dark
                # rectangle solely because its alpha channel is opaque.
                visible = visible_intensity(source)
                candidates.append((ImageStat.Stat(visible).sum[0], identifier))
        if not candidates or max(candidates)[0] == 0:
            raise ValueError("The design has no visible source bitmap for a preview")
        return self.image(max(candidates, key=lambda value: value[0])[1])

    def notes(self):
        canvas = Image.new("RGBA", SIZE)
        for index, colour in enumerate(("white", "black", "scratch")):
            image = self.image(self.template("." + colour)["resource"])
            row = centred(image, (232, 42), maximum_scale=2)
            canvas.alpha_composite(row, (12, 15 + index * 44))
        return canvas

    def charge_notes(self):
        samples = []
        for colour in ("white", "black", "scratch"):
            parts = {part: self.image(self.template("." + colour + "." + part)["resource"])
                     for part in ("head", "body", "tail")}
            width = parts["body"].width
            body_height = 96
            caps = {part: centred(parts[part], (width, 24), maximum_scale=1) for part in ("head", "tail")}
            sample = Image.new("RGBA", (width, body_height + 48))
            sample.alpha_composite(caps["tail"], (0, 0))
            sample.alpha_composite(parts["body"].resize((width, body_height), Image.Resampling.NEAREST), (0, 24))
            sample.alpha_composite(caps["head"], (0, body_height + 24))
            samples.append(sample)
        return centred(horizontal(samples, gap=12), (256, 160), maximum_scale=1)

    def judgement(self):
        root = self.template(".p1")
        label = next(item for item in walk(root) if item["id"].endswith(".perfect.label"))
        combo = next(item for item in walk(root) if item["id"].endswith(".perfect.combo"))
        font = next(item for item in self.manifest["bitmapFonts"] if item["id"] == combo["properties"]["bitmap-font"])
        glyphs = {item["character"]: item for item in font["glyphs"]}
        images = [self.image(label["resource"])]
        images.extend(self.image(glyphs[digit]["resource"], glyphs[digit]["rect"]) for digit in "593")
        return centred(horizontal(images, gap=0), (256, 160), maximum_scale=1)

    def explosion(self):
        root = self.template(".bms.white")
        image = self.strongest(self.identifiers(root))
        bounds = visible_intensity(image).point(lambda value: 255 if value > 3 else 0).getbbox()
        if bounds is None:
            raise ValueError("An explosion preview must remain visible")
        return centred(image.crop(bounds), maximum_scale=1)

    def key_beam(self):
        samples = []
        for colour in ("white", "black", "scratch"):
            image = self.strongest(self.identifiers(self.template("." + colour)))
            # The lower 200 source pixels show the material at the judgement
            # line without reducing a 723px beam to an unreadable thin stripe.
            height = min(image.height, 200)
            rect = [0, image.height - height, image.width, height]
            self.sources[-1]["rect"] = rect
            samples.append(image.crop((0, image.height - height, image.width, image.height)))
        return centred(horizontal(samples, gap=8), maximum_scale=1)

    def turntable(self):
        identifiers = self.identifiers(self.template(".p1"))
        if len(identifiers) != 1:
            raise ValueError("The verified turntable module must use one actual design texture")
        return centred(self.image(identifiers[0]), maximum_scale=2)

    def frame(self):
        identifiers = [item["id"] for item in self.resources.values() if item["path"].endswith("/backgrounds/dp.png")]
        if len(identifiers) != 1:
            raise ValueError("A Frame thumbnail requires its own complete static background")
        image = self.image(identifiers[0])
        image.thumbnail(SIZE, Image.Resampling.LANCZOS)
        return centred(image, maximum_scale=1)

    def lane_cover(self):
        nodes = [item for template in self.scene.get("templates", []) for item in walk(template["root"])
                 if item["id"].endswith(".art")]
        animations = {item["animation"] for item in nodes if item.get("animation")}
        identifiers = {item["resource"] for item in nodes if item.get("resource")}
        if len(animations) == 1 and not identifiers:
            if not re.fullmatch(r"l\d{3}", self.choice["value"]):
                raise ValueError("An animated cover must match its exact original CUSTOM identity")
            if self.original_library is None:
                image, sources = retained_preview(self.root, self.choice, "lane-cover")
            else:
                expected = f"contents/data/graphic/customize/lane{self.choice['value'][1:]}_anim.jpg"
                image, sources = original_custom_preview(self.original_library, expected)
            self.sources.extend(sources)
            return image
        if len(identifiers) != 1:
            raise ValueError("A cover thumbnail requires one exact original art texture")
        image = self.image(next(iter(identifiers)))
        image.thumbnail((240, 144), Image.Resampling.LANCZOS)
        return centred(image, maximum_scale=1)

    def graph_area(self):
        if self.resources:
            return self.lane_cover()
        # The existing choice caption says '原空白'. A transparent-area symbol
        # distinguishes this intentional empty design from a missing thumbnail.
        image = Image.new("RGBA", SIZE)
        draw = ImageDraw.Draw(image)
        for y in range(24, 136, 16):
            for x in range(24, 232, 16):
                draw.rectangle((x, y, x + 15, y + 15), fill=(88, 91, 106, 140) if (x + y) % 32 else (52, 55, 69, 140))
        draw.rectangle((24, 24, 231, 135), outline=(160, 164, 182, 255), width=2)
        self.sources.append({"kind": "intentional-empty-area", "labelProvidedByChoice": True})
        return image

    def lift_art(self):
        return self.lane_cover()


def horizontal(images, gap):
    canvas = Image.new("RGBA", (sum(image.width for image in images) + gap * (len(images) - 1),
                                max(image.height for image in images)))
    left = 0
    for image in images:
        canvas.alpha_composite(image, (left, canvas.height - image.height))
        left += image.width + gap
    return canvas


def retained_preview(root: Path, choice, module: str):
    relative = preview_path(module, choice["value"])
    if choice.get("preview") != relative:
        raise ValueError("New animation thumbnails require --choice-preview-library with original-previews.json")
    path = exact_file(root, relative)
    with Image.open(path) as source:
        image = source.convert("RGBA")
    if image.size != SIZE or path.stat().st_size > MAXIMUM_IMAGE_BYTES:
        raise ValueError("A retained thumbnail is outside the illustration budget")
    return image, [{"kind": "retained-declared-preview", "path": relative, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}]


def original_custom_preview(original_library: Path, expected: str):
    rows = [row for row in read_json(exact_file(original_library, "original-previews.json")) if row["source"] == expected]
    if len(rows) != 1:
        raise ValueError(f"An exact original CUSTOM preview is missing: {expected}")
    row = rows[0]
    path = exact_file(original_library, row["copy"])
    if hashlib.sha256(path.read_bytes()).hexdigest() != row["sha256"]:
        raise ValueError("The original CUSTOM preview differs from its source receipt")
    with Image.open(path) as source:
        image = source.convert("RGBA")
    if list(image.size) != row["size"]:
        raise ValueError("The original CUSTOM preview dimensions changed")
    image.thumbnail((240, 144), Image.Resampling.LANCZOS)
    return centred(image, maximum_scale=1), [{"kind": "original-custom-preview", **row}]


def full_combo_preview(root: Path, choice, original_library: Path | None):
    if not re.fullmatch(r"fc\d{2}", choice["value"]):
        raise ValueError("Full Combo preview identity must match its exact original CUSTOM choice")
    if original_library is None:
        return retained_preview(root, choice, "full-combo")
    return original_custom_preview(original_library, f"contents/data/graphic/customize/fcombo{choice['value'][2:]}.jpg")


def build_choice_previews(root: Path, original_library: Path | None = None):
    """Augment a fresh package tree; supports freshly exported and frozen modules."""
    root = root.resolve(strict=True)
    catalog_file = root / "gameplay/content.json"
    catalog = read_json(catalog_file)
    receipts = []
    for module in catalog["modules"]:
        if module["id"] not in MODULES:
            continue
        for choice in module["choices"]:
            if module["id"] == "full-combo":
                image, sources = full_combo_preview(root, choice, original_library)
            else:
                illustration = Illustration(root, choice, original_library)
                image = getattr(illustration, module["id"].replace("-", "_"))()
                sources = illustration.sources
            encoded = BytesIO()
            image.save(encoded, format="PNG", optimize=True)
            content = encoded.getvalue()
            if len(content) > MAXIMUM_IMAGE_BYTES or image.getchannel("A").getextrema()[1] == 0:
                raise ValueError("A generated preview exceeds its encoded budget or is empty")
            relative = preview_path(module["id"], choice["value"])
            destination = root / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            if destination.exists() and destination.read_bytes() != content:
                raise ValueError(f"An existing preview differs from the deterministic source illustration: {relative}")
            destination.write_bytes(content)
            choice["preview"] = relative
            receipts.append({"module": module["id"], "choice": choice["value"], "path": relative,
                             "bytes": len(content), "sha256": hashlib.sha256(content).hexdigest(),
                             "size": list(image.size), "sources": sources})
    total = sum(item["bytes"] for item in {row["path"]: row for row in receipts}.values())
    if total > MAXIMUM_TOTAL_BYTES:
        raise ValueError("The independent preview library exceeds its encoded budget")
    catalog_file.write_text(json.dumps(catalog, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    return {"method": "Per-choice actual contribution art; static illustrations",
            "perModule": dict(Counter(item["module"] for item in receipts)), "choices": len(receipts),
            "uniqueFiles": len({item["path"] for item in receipts}),
            "encodedBytes": total, "excludedModules": [module["id"] for module in catalog["modules"] if module["id"] not in MODULES],
            "beamCrop": "Lower200 original source pixels; illustrative detail rather than full beam length",
            "sharedCoverArtwork": "Lane Cover, Graph and LIFT reference the same bounded PNG for each identical original design",
            "gameplayMediaChanged": False, "items": receipts}


def preview_files(catalog):
    return {choice["preview"] for module in catalog["modules"] for choice in module["choices"] if choice.get("preview")}


def audit_previews(root: Path, catalog):
    files = preview_files(catalog)
    total = 0
    for relative in files:
        path = exact_file(root, relative)
        if path.suffix != ".png" or path.stat().st_size > MAXIMUM_IMAGE_BYTES:
            raise ValueError(f"Invalid independent option preview: {relative}")
        with Image.open(path) as image:
            if image.format != "PNG" or min(image.size) <= 0 or max(image.size) > 512:
                raise ValueError(f"Invalid option preview dimensions: {relative}")
            image.verify()
        total += path.stat().st_size
    if total > MAXIMUM_TOTAL_BYTES:
        raise ValueError("The independent preview library exceeds its encoded budget")
    return {"files": len(files), "encodedBytes": total, "separateFromGameplayTextureBudget": True}
