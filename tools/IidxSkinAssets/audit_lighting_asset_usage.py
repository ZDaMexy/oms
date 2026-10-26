"""Read-only, private Omni artwork/reference audit. No HDD execution or player data.

Run with --help. The output must be a new directory outside every input tree.
References are the union of all catalogue choices, not proof of visible gameplay.
Only this script's new output directory is written; source PNGs are never copied.
"""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
from typing import Any

from PIL import Image, ImageDraw


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def checked_root(value: str | Path) -> Path:
    path = Path(value).resolve(strict=True)
    if not path.is_dir() or "private-data" in {part.lower() for part in path.parts}:
        raise ValueError(f"Not an allowed input directory: {path}")
    return path


def member(root: Path, value: str) -> Path:
    """Resolve portable author/library paths without accepting traversal/absolute paths."""
    normal = value.replace("\\", "/")
    pure = PurePosixPath(normal)
    if pure.is_absolute() or ":" in normal or ".." in pure.parts:
        raise ValueError(f"Unsafe member path: {value}")
    path = (root / normal).resolve(strict=True)
    if not path.is_relative_to(root) or "private-data" in {part.lower() for part in path.parts}:
        raise ValueError(f"Member escaped its input directory: {value}")
    return path


def output_directory(value: str | Path, inputs: list[Path]) -> Path:
    path = Path(value).resolve()
    if "private-data" in {part.lower() for part in path.parts}:
        raise ValueError("Output must not be in private-data")
    if any(path.is_relative_to(root) or root.is_relative_to(path) for root in inputs):
        raise ValueError("Output overlaps an input tree")
    if path.exists():
        raise ValueError("Output already exists; choose a new directory (no overwrite)")
    path.mkdir(parents=True, exist_ok=False)
    return path


class ReadLedger:
    """Hash actual bytes, decode equal encoded images once, and check all read metadata."""

    def __init__(self):
        self.files: dict[Path, dict[str, Any]] = {}
        self.pixels: dict[str, dict[str, Any]] = {}

    def read(self, path: Path) -> bytes:
        before = path.stat()
        data = path.read_bytes()
        after = path.stat()
        stamp = (before.st_size, before.st_mtime_ns)
        if stamp != (after.st_size, after.st_mtime_ns):
            raise ValueError(f"Input changed while reading: {path}")
        digest = sha256(data)
        previous = self.files.get(path)
        if previous and (previous["sha256"] != digest or previous["stamp"] != stamp):
            raise ValueError(f"Input changed during audit: {path}")
        self.files[path] = {"sha256": digest, "stamp": stamp}
        return data

    def json(self, path: Path) -> Any:
        return json.loads(self.read(path).decode("utf-8-sig"))

    def image(self, path: Path) -> dict[str, Any]:
        data = self.read(path)
        digest = sha256(data)
        if digest not in self.pixels:
            import io
            with Image.open(io.BytesIO(data)) as image:
                rgba = image.convert("RGBA")
                self.pixels[digest] = {"width": rgba.width, "height": rgba.height,
                                       "rgbaSha256": sha256(rgba.tobytes()),
                                       "alphaBounds": rgba.getchannel("A").getbbox()}
        return {"sha256": digest, **self.pixels[digest]}

    def verify_unchanged(self):
        for path, record in self.files.items():
            current = path.stat()
            if record["stamp"] != (current.st_size, current.st_mtime_ns):
                raise ValueError(f"Input metadata changed during audit: {path}")


def pixel_key(row):
    return row["width"], row["height"], row["rgbaSha256"]


def objects(value):
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from objects(child)
    elif isinstance(value, list):
        for child in value:
            yield from objects(child)


def scene_dependencies(scene):
    resources, fonts, animations = set(), set(), set()
    tints: dict[str, set[str]] = defaultdict(set)
    for item in objects(scene):
        for key, target in (("resource", resources), ("bitmap-font", fonts), ("animation", animations)):
            if isinstance(item.get(key), str):
                target.add(item[key])
        if item.get("property") in ("resource", "bitmap-font", "animation"):
            target = {"resource": resources, "bitmap-font": fonts, "animation": animations}[item["property"]]
            for value in [item.get("default"), *(case.get("value") for case in item.get("cases", [])),
                          *(frame.get("value") for frame in item.get("keyframes", []))]:
                if isinstance(value, str):
                    target.add(value)
        if isinstance(item.get("bitmap-font"), str) and isinstance(item.get("colour"), str):
            tints[item["bitmap-font"]].add(item["colour"].lower())
    return resources, fonts, animations, tints


class AuthorReferences:
    def __init__(self, root: Path, ledger: ReadLedger):
        self.root, self.ledger = root, ledger
        self.refs: dict[str, dict[str, set[str]]] = defaultdict(lambda: defaultdict(set))
        self.pairs = set()
        self.inis = set()
        self.manifests = set()
        self.scenes = set()
        self.clips = set()
        self.missing_ids = set()
        self.tinted_glyphs = []
        self.atlas_regions = defaultdict(list)
        self.image_sizes = {}
        self.catalogue_choices = 0

    def add(self, path, kind, origin):
        member(self.root, path)
        self.refs[path][kind].add(origin)

    def previews(self, document, origin):
        for item in objects(document):
            if isinstance(item.get("preview"), str):
                self.add(item["preview"], "preview", origin)

    def ini(self, path):
        if path in self.inis:
            return
        self.inis.add(path)
        text = self.ledger.read(member(self.root, path)).decode("utf-8-sig")
        for name in re.findall(r'\bProvide\s+"([^"\r\n]+)"', text):
            self.add(name + ".png", "runtime-ini-selector", path)

    def pair(self, manifest_path, scene_path):
        pair = manifest_path, scene_path
        if pair in self.pairs:
            return
        self.pairs.add(pair)
        self.manifests.add(manifest_path)
        self.scenes.add(scene_path)
        manifest = self.ledger.json(member(self.root, manifest_path))
        scene = self.ledger.json(member(self.root, scene_path))
        resources = {item["id"]: item for item in manifest.get("resources", [])}
        for resource in resources.values():
            self.add(resource["path"], "declared", manifest_path)
        self.previews(manifest, manifest_path)
        required, font_ids, animation_ids, tints = scene_dependencies(scene)
        for font in manifest.get("bitmapFonts", []):
            if font["id"] not in font_ids:
                continue
            for glyph in font.get("glyphs", []):
                required.add(glyph["resource"])
                if glyph.get("rect") and glyph["resource"] in resources:
                    self.atlas_regions[resources[glyph["resource"]]["path"]].append({"kind": "font-glyph",
                        "rect": glyph["rect"], "font": font["id"], "character": glyph["character"], "scene": scene_path})
                for tint in sorted(tints[font["id"]]):
                    if glyph["resource"] in resources:
                        self.tinted_glyphs.append({"manifest": manifest_path, "scene": scene_path,
                            "font": font["id"], "character": glyph["character"], "colour": tint,
                            "path": resources[glyph["resource"]]["path"]})
        for animation in manifest.get("animations", []):
            if animation["id"] not in animation_ids:
                continue
            path = animation["path"]
            self.add(path, "runtime-recorded-animation", scene_path)
            self.clips.add(path)
            data = self.ledger.read(member(self.root, path))
            if path.endswith(".br"):
                import brotli
                data = brotli.decompress(data)
            clip = json.loads(data.decode("utf-8-sig"))
            clip_resources = scene_dependencies(clip)[0]
            declared_animation_resources = set(animation.get("resources", []))
            if not clip_resources.issubset(declared_animation_resources):
                raise ValueError(f"Recorded clip has undeclared resources: {path}")
            required.update(clip_resources)
            for index, geometry in enumerate(clip.get("geometries", [])):
                resource_id = geometry.get("resource")
                if resource_id not in resources or not geometry.get("vertices"):
                    continue
                image_path = resources[resource_id]["path"]
                if image_path not in self.image_sizes:
                    measured = self.ledger.image(member(self.root, image_path))
                    self.image_sizes[image_path] = measured["width"], measured["height"]
                width, height = self.image_sizes[image_path]
                points = [(vertex[3] * width, vertex[4] * height) for vertex in geometry["vertices"]]
                left, top = min(point[0] for point in points), min(point[1] for point in points)
                right, bottom = max(point[0] for point in points), max(point[1] for point in points)
                self.atlas_regions[image_path].append({"kind": "recorded-clip-geometry-uv", "clip": path,
                    "geometry": index, "rect": [left, top, right - left, bottom - top]})
        for resource_id in required:
            if resource_id in resources:
                self.add(resources[resource_id]["path"], "runtime-scene-font-or-clip", scene_path)
            elif (self.root / (resource_id + ".png")).is_file():
                self.add(resource_id + ".png", "runtime-scene-selector", scene_path)
            else:
                # INI marker/selectors need not be manifest texture IDs.
                self.missing_ids.add((scene_path, resource_id))

    def collect(self):
        manifest = self.ledger.json(member(self.root, "gameplay-skin.json"))
        self.pair("gameplay-skin.json", manifest["scene"])
        self.ini("skin.ini")
        if manifest.get("contentCatalog"):
            path = manifest["contentCatalog"]
            catalog = self.ledger.json(member(self.root, path))
            self.previews(catalog, path)
            for module in catalog.get("modules", []):
                for choice in module.get("choices", []):
                    self.catalogue_choices += 1
                    for contribution in (choice, *choice.get("variants", [])):
                        if contribution.get("manifest") and contribution.get("scene"):
                            self.pair(contribution["manifest"], contribution["scene"])
                        if contribution.get("ini"):
                            self.ini(contribution["ini"])
        rows = []
        for path, kinds in sorted(self.refs.items()):
            if not path.lower().endswith((".png", ".jpg", ".jpeg", ".webp")):
                continue
            rows.append({"path": path, **self.ledger.image(member(self.root, path)),
                "kinds": sorted(kinds), "origins": {kind: sorted(values) for kind, values in kinds.items()}})
        return rows


def source_scope(package, family):
    stem = PurePosixPath(package).stem
    if family.startswith("premium-") or stem.startswith("sub_premium_area"):
        return "excluded-premium-or-secondary", "Premium/secondary screen is explicitly outside gameplay completion scope"
    return "gameplay-or-unresolved", None


def source_rows(library: Path, localized: Path | None, fullcombo: Path | None, ledger: ReadLedger):
    result = []
    library_doc = ledger.json(member(library, "library.json"))
    catalogue = ledger.json(member(library, "customization-catalog.json"))
    inventory = ledger.json(member(library, "source-inventory-before.json"))
    original_previews = ledger.json(member(library, "original-previews.json"))
    packages = {item["source"]: item for item in catalogue["packages"]}
    relations = {}
    for package in library_doc["packages"]:
        info = ledger.json(member(library, package["authorData"]))
        by_region = defaultdict(list)
        movie_geometries = defaultdict(set)
        for movie in info.get("afplist", []):
            for identifier in movie.get("geoIds", []):
                movie_geometries[movie["name"] + "_shape" + str(identifier)].add(movie["name"])
        movie_data = {movie["name"]: movie for movie in info.get("movies", [])}
        for geo in info.get("geometry", []):
            movies = sorted(movie_geometries.get(geo["name"], []))
            for region in geo.get("regions", []):
                by_region[region].append({"geometry": geo["name"], "parsedGeometry": geo.get("data"),
                    "bounds": geo.get("bounds"), "movies": [{"name": name,
                        "parsed": movie_data.get(name, {}).get("data"),
                        "frames": movie_data.get(name, {}).get("frames"),
                        "fps": movie_data.get(name, {}).get("fps")} for name in movies]})
        relations[package["source"]] = by_region
    for texture in library_doc["textures"]:
        family = packages[texture["package"]]["family"]
        scope, reason = source_scope(texture["package"], family)
        measured = ledger.image(member(library, texture["sprite"]))
        for key in ("sha256", "width", "height", "rgbaSha256"):
            if measured[key] != texture[key]:
                raise ValueError(f"Source texture metadata mismatch ({key}): {texture['sprite']}")
        result.append({"id": "all-custom-v1:" + texture["sprite"], "library": str(library),
            "libraryKind": "all-custom-v1", "path": texture["sprite"], "package": texture["package"],
            "name": texture["name"], "family": family, "scope": scope, "scopeReason": reason,
            **measured, "imageRect": texture.get("imageRect"), "uvRect": texture.get("uvRect"),
            "relationships": relations[texture["package"]].get(PurePosixPath(texture["name"]).stem, [])})
    coverage = {"all-custom-v1": {"selectedPackages": len(packages),
        "sourceInventoryFiles": library_doc["sourceFileCount"], "textures": len(library_doc["textures"]),
        "uniquePixelContentsRecorded": library_doc.get("uniqueSpriteContents"),
        "sourceInventorySnapshotEntries": len(inventory),
        "sourceInventoryIfsFiles": sum(PurePosixPath(item["path"]).suffix.lower() == ".ifs" for item in inventory),
        "unselectedSourceInventoryIfsFiles": sum(PurePosixPath(item["path"]).suffix.lower() == ".ifs"
            and item["path"] not in packages for item in inventory),
        "sourceInventoryBoundary": "Recorded graphic-file metadata snapshot; the original HDD is not rescanned",
        "originalPreviewMetadataEntries": len(original_previews),
        "rawExtractedFilesRecorded": sum(item["rawFileCount"] for item in library_doc["packages"]),
        "parsedAfpRecorded": catalogue["parsedAfpCount"], "parsedGeoRecorded": catalogue["parsedGeoCount"],
        "familyPackageCounts": dict(Counter(item["family"] for item in packages.values())),
        "boundary": "Selected graphic IFS packages, not complete HDD extraction or semantic classification"}}
    if localized:
        doc = ledger.json(member(localized, "source-reference.json"))
        for texture in doc["textures"]:
            measured = ledger.image(member(localized, texture["sprite"]))
            for key in ("sha256", "width", "height", "rgbaSha256"):
                if measured[key] != texture[key]:
                    raise ValueError(f"Localized texture metadata mismatch: {texture['sprite']}")
            result.append({"id": "lighting-localized-v1:" + texture["sprite"], "library": str(localized),
                "libraryKind": "lighting-localized-v1", "path": texture["sprite"],
                "package": texture["sourcePackage"], "name": texture["id"], "family": "first-note-cue",
                "scope": "gameplay-or-unresolved", **measured,
                "imageRect": texture.get("imageRect"), "uvRect": texture.get("uvRect"), "relationships": []})
        coverage["lighting-localized-v1"] = {"textures": len(doc["textures"]), "boundary": doc["scope"]}
    if fullcombo:
        doc = ledger.json(member(fullcombo, "catalog.json"))
        by_hash = defaultdict(list)
        for package in doc["packages"]:
            data = ledger.json(member(fullcombo, package["path"]))
            for texture in data["textures"]:
                geometries = [geometry["source"] for geometry in data.get("geometries", [])
                              if any(draw.get("texture") == texture["resource"] for draw in geometry.get("draws", []))]
                by_hash[texture["sourcePNG"]].append({"package": package["id"], "sourceRegion": texture["source"],
                    "geometries": geometries, "roots": [{"layout": root["layout"], "path": root["path"]}
                                                         for root in package["roots"]]})
        for texture in doc["textures"]:
            measured = ledger.image(member(fullcombo, texture["path"]))
            if measured["sha256"] != texture["sha256"] or [measured["width"], measured["height"]] != texture["size"]:
                raise ValueError(f"Full Combo texture metadata mismatch: {texture['path']}")
            result.append({"id": "fullcombo-v3:" + texture["path"], "library": str(fullcombo),
                "libraryKind": "fullcombo-v3", "path": texture["path"], "package": "fullcombo-v3-shared-textures",
                "name": "/".join(sorted({item["sourceRegion"] for item in by_hash[texture["sha256"]]})),
                "family": "full-combo", "scope": "gameplay-or-unresolved", **measured,
                "relationships": by_hash[texture["sha256"]]})
        coverage["fullcombo-v3"] = {"packages": len(doc["packages"]), "textures": len(doc["textures"]),
            "boundary": "Independent neutral Full Combo export; not all raw original package entries", "scope": doc["scope"]}
    return result, coverage


def validated_derivations(sources, references, author, receipts, ledger):
    source_index = defaultdict(list)
    for source in sources:
        source_index[(source["path"], source["sha256"])].append(source)
    referenced = {row["path"]: row for row in references if any(kind.startswith("runtime-") for kind in row["kinds"])}
    edges = defaultdict(list)
    rejected = []
    for path in receipts:
        document = ledger.json(path)
        if document.get("contract") != "oms-omni-gameplay-control-assets.v1":
            raise ValueError(f"Unsupported derivation receipt: {path}")
        for asset in document["assets"]:
            if not asset.get("derivation"):
                continue
            matches = source_index.get((asset["source"], asset["sourceSha256"]), [])
            output = referenced.get(asset["path"])
            if not matches or not output or output["sha256"] != asset["sha256"]:
                rejected.append({"receipt": str(path), "name": asset["name"], "reason": "source or current runtime output not verified"})
                continue
            # Output/input pixels and hashes are measured; this validates the documented edge,
            # rather than claiming to reimplement every transformation in every historical tool.
            derivation = asset["derivation"]
            if derivation.get("rgbaSha256") and derivation["rgbaSha256"] != output["rgbaSha256"]:
                raise ValueError(f"Derived output RGBA does not match receipt: {asset['path']}")
            for source in matches:
                if derivation.get("sourceRgbaSha256") and derivation["sourceRgbaSha256"] != source["rgbaSha256"]:
                    raise ValueError(f"Derived input RGBA does not match receipt: {asset['source']}")
                edges[source["id"]].append({"kind": "explicit-transformation-receipt", "receipt": str(path),
                    "receiptSha256": ledger.files[path]["sha256"], "output": asset["path"],
                    "operation": derivation["operation"], "outputSha256": output["sha256"]})
    return edges, rejected


def tint_derivations(sources, references, author, glyphs, receipts, ledger):
    """Prove the white-font + green-tint path, including current text/font/glyph binding."""
    by_hash = defaultdict(list)
    for source in sources:
        by_hash[source["sha256"]].append(source)
    author_rows = {row["path"]: row for row in references}
    edges = defaultdict(list)
    for receipt in receipts:
        doc = ledger.json(receipt)
        if doc.get("contract") != "oms-iidx-gameplay-control-source.v1":
            raise ValueError(f"Unsupported tint receipt: {receipt}")
        for equivalence in doc["sharedWhiteGreenFont"]:
            for glyph in glyphs:
                if (glyph["character"] != str(equivalence["digit"]) or glyph["colour"] != "#64ff00ff"
                        or author_rows[glyph["path"]]["sha256"] != equivalence["whiteSha256"]):
                    continue
                white_sources = by_hash[equivalence["whiteSha256"]]
                green_sources = by_hash[equivalence["greenSha256"]]
                if not white_sources or not green_sources:
                    raise ValueError("Tint receipt source hashes not found in measured libraries")
                white = white_sources[0]
                green = green_sources[0]
                with Image.open(member(Path(white["library"]), white["path"])) as image:
                    data = image.convert("RGBA").tobytes()
                    white_pixels = [tuple(data[index:index + 4]) for index in range(0, len(data), 4)]
                with Image.open(member(Path(green["library"]), green["path"])) as image:
                    data = image.convert("RGBA").tobytes()
                    green_pixels = [tuple(data[index:index + 4]) for index in range(0, len(data), 4)]
                if ((white["width"], white["height"]) != (green["width"], green["height"])
                    or any(a[3] != b[3] or (a[3] and tuple((c * tint + 127) // 255 for c, tint in zip(a[:3], (100, 255, 0))) != b[:3])
                           for a, b in zip(white_pixels, green_pixels))):
                    raise ValueError("White-font tint does not reproduce visible green pixels")
                for source in green_sources:
                    evidence = {"kind": "verified-visible-pixel-tint", "receipt": str(receipt),
                        "receiptSha256": ledger.files[receipt]["sha256"], "output": glyph["path"],
                        "font": glyph["font"], "character": glyph["character"], "colour": glyph["colour"],
                        "scene": glyph["scene"], "alphaEqual": True,
                        "boundary": "Visible pixels and alpha equal; transparent RGB is not a rendering difference"}
                    if evidence not in edges[source["id"]]:
                        edges[source["id"]].append(evidence)
    return edges


def rectangles_overlap(first, second):
    x, y, width, height = first
    other_x, other_y, other_width, other_height = second
    return min(x + width, other_x + other_width) > max(x, other_x) and min(y + height, other_y + other_height) > max(y, other_y)


def atlas_derivations(sources, references, author, atlas_regions, receipts, ledger):
    """Verify recorded module atlas crops against original RGBA and current glyph/UV uses."""
    by_relative, by_absolute = defaultdict(list), defaultdict(list)
    for source in sources:
        by_relative[(source["path"], source["sha256"])].append(source)
        by_absolute[(str(member(Path(source["library"]), source["path"])), source["sha256"])].append(source)
    runtime = {row["path"]: row for row in references if any(kind.startswith("runtime-") for kind in row["kinds"])}
    edges, uncertain = defaultdict(list), defaultdict(list)
    for receipt_path in receipts:
        document = ledger.json(receipt_path)
        if not isinstance(document.get("sourceLibrary"), str) or not isinstance(document.get("details"), list):
            raise ValueError(f"Unsupported module atlas receipt: {receipt_path}")
        pages = []
        for detail in document["details"]:
            if detail.get("kind") == "lossless-glyph-atlas":
                pages.append({"path": detail["path"], "sources": detail["sourceGlyphs"], "kind": "module-font-atlas"})
            interning = detail.get("receipt", {}).get("moduleResourceInterning", {})
            atlas = interning.get("losslessSourcePixelAtlas")
            if atlas:
                pages.extend({**page, "kind": "module-animation-atlas"} for page in atlas["pages"])
        for page in pages:
            output = runtime.get(page["path"])
            if not output:
                continue
            with Image.open(member(author, page["path"])) as image:
                canvas = image.convert("RGBA")
                if page.get("size") and page["size"] != [canvas.width, canvas.height]:
                    raise ValueError(f"Current atlas dimensions differ from receipt: {page['path']}")
                for entry in page["sources"]:
                    source_hash = entry.get("sourcePNG_SHA256", entry.get("sourceSHA256"))
                    if "source" in entry:
                        matches = by_relative.get((entry["source"], source_hash), [])
                    else:
                        # Absolute receipt paths are compared only against already measured inputs;
                        # arbitrary absolute paths in metadata are never opened.
                        matches = by_absolute.get((str(Path(entry["sourcePNG"]).resolve()), source_hash), [])
                    if not matches:
                        raise ValueError(f"Atlas input is not a measured source: {page['path']}")
                    left, top, width, height = entry["rect"]
                    if min(left, top) < 0 or width <= 0 or height <= 0 or left + width > canvas.width or top + height > canvas.height:
                        raise ValueError(f"Atlas crop is outside current image: {page['path']}")
                    crop = canvas.crop((left, top, left + width, top + height))
                    crop_hash = sha256(crop.tobytes())
                    if crop_hash != entry["rgbaSHA256"]:
                        raise ValueError(f"Current atlas crop differs from receipt: {page['path']}")
                    consumers = [region for region in atlas_regions.get(page["path"], [])
                                 if rectangles_overlap(entry["rect"], region["rect"])]
                    for source in matches:
                        if (width, height, crop_hash) != pixel_key(source):
                            raise ValueError(f"Current atlas crop differs from source pixels: {source['id']}")
                        edge = {"kind": page["kind"], "receipt": str(receipt_path),
                            "receiptSha256": ledger.files[receipt_path]["sha256"], "output": page["path"],
                            "outputSha256": output["sha256"], "rect": entry["rect"],
                            "rgbaSha256": crop_hash, "unchangedSourcePixels": True, "currentRegionConsumers": consumers}
                        (edges if consumers else uncertain)[source["id"]].append(edge)
    return edges, uncertain


def classify_sources(sources, references, derivations, uncertainties=None):
    runtime, preview, declared = defaultdict(list), defaultdict(list), defaultdict(list)
    source_groups = defaultdict(list)
    derived_groups = defaultdict(list)
    uncertain_groups = defaultdict(list)
    for row in sources:
        source_groups[pixel_key(row)].append(row["id"])
        for edge in derivations.get(row["id"], []):
            derived_groups[pixel_key(row)].append({"inputSourceId": row["id"], **edge})
        for edge in (uncertainties or {}).get(row["id"], []):
            uncertain_groups[pixel_key(row)].append({"inputSourceId": row["id"], **edge})
    for reference in references:
        key = pixel_key(reference)
        if any(kind.startswith("runtime-") for kind in reference["kinds"]):
            runtime[key].append(reference["path"])
        if "preview" in reference["kinds"]:
            preview[key].append(reference["path"])
        if "declared" in reference["kinds"]:
            declared[key].append(reference["path"])
    for row in sources:
        key = pixel_key(row)
        row["equivalentSourceCount"] = len(source_groups[key])
        row["runtimeReferences"] = sorted(runtime[key])
        row["previewReferences"] = sorted(preview[key])
        row["declaredReferences"] = sorted(declared[key])
        row["derivations"] = derived_groups[key]
        row["uncertainDerivations"] = uncertain_groups[key]
        if row["scope"].startswith("excluded-"):
            status, reason = "explicit_out_of_scope", row["scopeReason"]
        elif runtime[key]:
            status = "runtime_reference_alias" if len(source_groups[key]) > 1 else "runtime_reference_unique"
            reason = "Measured equal RGBA content has a current scene/font/recorded-clip/INI reference"
        elif row["derivations"]:
            status, reason = "derived_reference", "Measured input/output hashes and a current runtime path verify a derivation edge"
        elif preview[key]:
            status, reason = "preview_only", "Equal pixels only occur in referenced previews, not a detected runtime path"
        elif declared[key]:
            status, reason = "evidence_insufficient", "Equal pixels are declared but no scene/font/clip/INI consumer was found"
        elif uncertain_groups[key]:
            status, reason = "evidence_insufficient", "Verified original pixels are in a runtime atlas but no current glyph/UV region consumer was found"
        elif row["family"] == "game-frame":
            status, reason = "evidence_insufficient", "Frame authors also bake/composite/crop art; no complete per-input derivation receipt is available"
        elif "alphaBounds" in row and row["alphaBounds"] is None:
            status, reason = "evidence_insufficient", "Fully transparent source pixels may be a helper; no missing visible feature is inferred"
        else:
            status, reason = "candidate_unreferenced", "No content-equal runtime/preview or verified derivation reference found in the audited inputs"
        row["classification"], row["classificationReason"] = status, reason
    return source_groups


def candidate_score(row):
    text = row["name"].lower()
    score, topics = 0, []
    for words, points, topic in ((r"fast|slow|timing|scratch", 40, "timing-or-scratch-feedback"),
        (r"graph|ghost|pacemaker|next|rotate|best|rival", 30, "score-graph"),
        (r"gauge|hard|hazard|danger", 25, "gauge-display"),
        (r"lnote|hcn|charge|release", 20, "long-note-feedback"),
        (r"sud|hid|lift|cover|speed|spd", 15, "speed-and-cover-controls")):
        if re.search(words, text):
            score += points
            topics.append(topic)
    if row["family"] in ("shared-controls", "graph-area"):
        score += 25
    if row["relationships"]:
        score += 5
    return score, topics


def candidates(sources):
    groups = defaultdict(list)
    for row in sources:
        if row["classification"] == "candidate_unreferenced":
            groups[pixel_key(row)].append(row)
    rows = []
    for aliases in groups.values():
        chosen = max(aliases, key=lambda row: candidate_score(row)[0])
        score, topics = candidate_score(chosen)
        rows.append({"score": score, "topics": topics, "confidence": "candidate-only; reference audit is not runtime proof",
            "sourceId": chosen["id"], "name": chosen["name"], "family": chosen["family"],
            "library": chosen["library"], "path": chosen["path"], "package": chosen["package"],
            "width": chosen["width"], "height": chosen["height"], "rgbaSha256": chosen["rgbaSha256"],
            "sourceAliases": [row["id"] for row in aliases], "relationships": chosen["relationships"]})
    return sorted(rows, key=lambda row: (-row["score"], row["package"], row["name"]))


def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def write_jsonl(path, values):
    with path.open("w", encoding="utf-8", newline="\n") as stream:
        for value in values:
            stream.write(json.dumps(value, ensure_ascii=False, separators=(",", ":")) + "\n")


def contact_sheet(output, rows, count):
    selected = rows[:count]
    if not selected:
        return []
    pages = []
    for offset in range(0, len(selected), 24):
        chunk = selected[offset:offset + 24]
        canvas = Image.new("RGB", (1200, ((len(chunk) + 3) // 4) * 180), (30, 33, 38))
        draw = ImageDraw.Draw(canvas)
        for index, row in enumerate(chunk):
            x, y = (index % 4) * 300, (index // 4) * 180
            with Image.open(member(Path(row["library"]), row["path"])) as source:
                image = source.convert("RGBA")
                image.thumbnail((284, 110))
                canvas.paste(image, (x + (300 - image.width) // 2, y + 5 + (110 - image.height) // 2), image)
            title = row["name"].encode("ascii", errors="replace").decode("ascii")
            package = row["package"].encode("ascii", errors="replace").decode("ascii")
            draw.text((x + 6, y + 119), f"{offset + index + 1}. {title[:40]}", fill="white")
            draw.text((x + 6, y + 135), package[-43:], fill=(168, 186, 204))
            draw.text((x + 6, y + 151), f"{row['width']}x{row['height']} | aliases {len(row['sourceAliases'])}", fill=(168, 186, 204))
        path = output / f"candidate-contact-{offset // 24 + 1:02}.jpg"
        canvas.save(path, quality=90)
        pages.append(path.name)
    return pages


def audit(args):
    author = checked_root(args.author)
    library = checked_root(args.library)
    localized = checked_root(args.localized) if args.localized else None
    fullcombo = checked_root(args.fullcombo) if args.fullcombo else None
    receipts = [member(checked_root(Path(path).parent), Path(path).name) for path in args.derivation_receipt]
    tint_receipts = [member(checked_root(Path(path).parent), Path(path).name) for path in args.tint_receipt]
    module_receipts = [member(checked_root(Path(path).parent), Path(path).name) for path in args.module_receipt]
    inputs = [author, library, *(root for root in (localized, fullcombo) if root),
              *(path.parent for path in receipts + tint_receipts + module_receipts)]
    output = output_directory(args.output, inputs)
    ledger = ReadLedger()
    tool_path = Path(__file__).resolve()
    tool_digest = sha256(ledger.read(tool_path))
    print("Measuring source libraries (encoded bytes and unique RGBA contents)...", flush=True)
    sources, coverage = source_rows(library, localized, fullcombo, ledger)
    print(f"Measured {len(sources)} source records; collecting all author catalogue branches...", flush=True)
    collector = AuthorReferences(author, ledger)
    references = collector.collect()
    edges, rejected = validated_derivations(sources, references, author, receipts, ledger)
    for key, values in tint_derivations(sources, references, author, collector.tinted_glyphs, tint_receipts, ledger).items():
        edges[key].extend(values)
    atlas_edges, uncertain_atlas = atlas_derivations(sources, references, author, collector.atlas_regions, module_receipts, ledger)
    for key, values in atlas_edges.items():
        edges[key].extend(values)
    groups = classify_sources(sources, references, edges, uncertain_atlas)
    review = candidates(sources)
    sheets = contact_sheet(output, review, args.contact_count)
    ledger.verify_unchanged()
    family_counts = defaultdict(Counter)
    for source in sources:
        family_counts[source["family"]][source["classification"]] += 1
    summary = {"contract": "oms-omni-asset-usage-audit.v1", "completedAtUtc": datetime.now(timezone.utc).isoformat(),
        "tool": {"path": str(tool_path), "sha256": tool_digest, "arguments": vars(args)},
        "author": str(author), "output": str(output), "coverage": coverage,
        "sourceRecords": len(sources), "uniqueSourcePixelContents": len(groups),
        "classifications": dict(Counter(row["classification"] for row in sources)),
        "familyClassifications": dict(family_counts), "authorImageReferences": len(references),
        "authorImageKinds": dict(Counter(kind for row in references for kind in row["kinds"])),
        "catalogueChoices": collector.catalogue_choices, "manifestScenePairs": len(collector.pairs),
        "manifests": len(collector.manifests), "scenes": len(collector.scenes), "inis": len(collector.inis),
        "recordedAnimationFiles": len(collector.clips), "unresolvedSceneResourceIds": len(collector.missing_ids),
        "verifiedDerivationSourceRecords": len(edges), "rejectedDerivationEdges": rejected,
        "verifiedAtlasSourceRecords": len(atlas_edges), "atlasInputsWithoutRegionConsumer": len(uncertain_atlas),
        "candidateUniquePixelContents": len(review), "contactSheets": sheets,
        "readOnlyVerification": {"readFiles": len(ledger.files), "decodedUniqueImages": len(ledger.pixels),
                                 "allReadFileSizeAndMtimeUnchanged": True},
        "limitations": [
            "References cover the union of all catalogue choices/variants, not the selected default or observed visibility.",
            "Equal pixel groups prove content reuse, not which source package/locale was the provenance.",
            "Candidate unreferenced means no reference was found in these inputs; it does not mean an asset is dead or a feature missing.",
            "Source availability, parsed AFP/GEO, labels, and names do not prove host-driven timing, default identity, or gameplay semantics.",
            "Frame crops/composites without complete input receipts remain evidence insufficient; other unknown derivations can still exist.",
            "AFP/GEO links come from hashed package metadata/afplist indices; linked raw programs are not executed or interpreted by this audit.",
            "Preview matches only cover equal RGBA images; scaled/cropped/composited previews are not reverse-inferred.",
            "Premium/secondary scope exclusions are retained, and no new Concentration functionality is inferred.",
            "No original HDD, original game process, private player data, source library writes, or prior artifact writes are used.",
            "The full private JSONL inventory and contact sheets must stay local and ignored."]}
    write_json(output / "summary.json", summary)
    write_jsonl(output / "source-usage.jsonl", sources)
    write_jsonl(output / "author-references.jsonl", references)
    write_jsonl(output / "equivalent-content.jsonl", [{"width": key[0], "height": key[1], "rgbaSha256": key[2],
                                                       "sources": values} for key, values in groups.items()])
    write_json(output / "candidate-review.json", review)
    write_json(output / "unresolved-scene-resource-ids.json", sorted(collector.missing_ids))
    write_jsonl(output / "read-inputs.jsonl", [{"path": str(path), "sha256": record["sha256"],
        "bytes": record["stamp"][0], "mtimeNs": record["stamp"][1]} for path, record in sorted(ledger.files.items())])
    print(json.dumps({"output": str(output), "sourceRecords": len(sources), "classifications": summary["classifications"],
                      "candidateUniquePixelContents": len(review), "readOnlyVerification": summary["readOnlyVerification"]},
                     ensure_ascii=False, indent=2))
    return summary


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--author", required=True, help="Frozen author directory; read-only")
    parser.add_argument("--library", required=True, help="all-custom-v1 directory; read-only")
    parser.add_argument("--localized", help="Optional lighting-localized-v1 directory; read-only")
    parser.add_argument("--fullcombo", help="Optional fullcombo/v3 neutral catalogue directory; read-only")
    parser.add_argument("--derivation-receipt", action="append", default=[], help="Explicit control-assets.json receipt")
    parser.add_argument("--tint-receipt", action="append", default=[], help="Explicit gameplay control source-reference.json")
    parser.add_argument("--module-receipt", action="append", default=[], help="Explicit modules-v6-module-author-data/receipts.json")
    parser.add_argument("--output", required=True, help="New private output directory outside all input trees")
    parser.add_argument("--contact-count", type=int, default=48, help="Candidate contact sheet item count; 0 disables")
    args = parser.parse_args()
    if not 0 <= args.contact_count <= 240:
        parser.error("--contact-count must be between 0 and 240")
    audit(args)


if __name__ == "__main__":
    main()
