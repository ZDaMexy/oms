"""Catalogue original IIDX customization art from verified independent copies.

The source installation is read-only. IFS parsers receive only physical copies
inside a new media library. Source PNG pixels, UV margins, XML, AFP and GEO are
preserved. This produces author data, never advertised or fabricated UI choices.
Full-combo packages are deliberately excluded and supplied by their own audit.
"""
import argparse
from collections import Counter, defaultdict
from concurrent.futures import ThreadPoolExecutor
import contextlib
from datetime import datetime, timezone
from functools import partial
import hashlib
import importlib.metadata
import io
import json
import os
from pathlib import Path
import re
import shutil
import sys
import time
import types
import xml.etree.ElementTree as ET

sys.dont_write_bytecode = True
PREFIX = "contents/data/graphic/"
FAMILIES = [
    ("explosion", r"exp\d{2}"), ("turntable", r"turn\d{2}"),
    ("judgement", r"judge\d{2}"), ("notes", r"notes\d{2}(?:_(?:long|short|veryshort))?"),
    ("charge-notes", r"notes_(?:cn|hcn)\d{2}(?:_(?:long|short|veryshort))?"),
    ("key-beam", r"beam\d{2}(?:_(?:long|short|veryshort))?"),
    ("lane-cover", r"lane\d{3}(?:_anim)?"),
    ("premium-icon", r"sub_customize_panel\d{3}(?:_[a-z0-9_]+)?"),
    ("premium-background", r"sub_customize_bg\d{3}"),
    ("graph-area", r"[01]/graph(?:_[ajk])?"),
    ("game-frame", r"(?:[01]/)?gmframe[^/]+"),
    ("shared-controls", r"[01]/(?:gameparts|common|basic_frame|sub_premium_area(?:_[ajk])?)|lnote00"),
]


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".writing")
    temporary.write_text(json.dumps(value, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
    for attempt in range(31):
        try:
            os.replace(temporary, path)
            return
        except PermissionError as error:
            # Windows readers without FILE_SHARE_DELETE can briefly hold the
            # current checkpoint. Retry that concrete read/replace race only.
            if getattr(error, "winerror", None) != 5 or attempt == 30:
                raise
            time.sleep(0.1)


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def inside(root, relative, exists=True):
    path = (root / relative).resolve(strict=exists)
    if not path.is_relative_to(root.resolve(strict=True)):
        raise ValueError(f"Path escaped its declared root: {relative}")
    return path


def inventory(source):
    graphic = source / "contents/data/graphic"
    result = []
    for path in sorted(graphic.rglob("*")):
        if path.is_file():
            inside(graphic, path.relative_to(graphic))
            stat = path.stat()
            result.append({"path": path.relative_to(source).as_posix(), "bytes": stat.st_size, "mtimeNs": stat.st_mtime_ns})
    return result


def family(source):
    key = source.removeprefix(PREFIX).removesuffix(".ifs")
    for name, pattern in FAMILIES:
        if re.fullmatch(pattern, key):
            return name
    raise ValueError(f"Source is not in the agreed non-FC customization catalogue: {source}")


def descriptor(source):
    key = source.removeprefix(PREFIX).removesuffix(".ifs")
    stem = Path(key).name
    match = re.fullmatch(r"(exp|turn|judge|notes|notes_cn|notes_hcn|beam|lane)(\d+)(?:_(long|short|veryshort|anim))?", stem)
    result = {"key": key, "source": source, "family": family(source), "variant": "normal"}
    if match:
        result.update(styleId=int(match[2]), variant=match[3] or "normal", subtype=match[1])
        preview = stem.replace("beam", "keybeam", 1) if match[1] == "beam" else stem
        result["originalPreview"] = PREFIX + "customize/" + preview + ".jpg"
    elif stem.startswith("sub_customize_bg"):
        result.update(styleId=int(stem.removeprefix("sub_customize_bg")), originalPreview=PREFIX + "customize/subbg" + stem[-3:] + ".jpg")
    return result


def copy_tree(source, target, root):
    target.mkdir(parents=True, exist_ok=False)
    for file in sorted(source.rglob("*")):
        inside(root, file.relative_to(root))
        destination = target / file.relative_to(source)
        if file.is_dir():
            destination.mkdir(exist_ok=True)
        else:
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(file, destination)


def load_afp_parser(path):
    for name, relative in [("bemani", "bemani"), ("bemani.format", "bemani/format"), ("bemani.format.afp", "bemani/format/afp")]:
        module = types.ModuleType(name)
        module.__path__ = [str(path / relative)]
        sys.modules[name] = module
    from bemani.format.afp.swf import SWF, AP2DefineSpriteTag, AP2PlaceObjectTag
    from bemani.format.afp.geo import Shape
    return SWF, Shape, AP2DefineSpriteTag, AP2PlaceObjectTag


def parse_controls(raw, target, media, parser_types):
    SWF, Shape, DefineSprite, PlaceObject = parser_types

    def walk(tags):
        for tag in tags:
            yield tag
            if isinstance(tag, DefineSprite):
                yield from walk(tag.tags)

    movies, geometry, default_cues = [], [], []

    def collect_cues(value, pointer=""):
        if isinstance(value, dict):
            for key, child in value.items():
                yield from collect_cues(child, pointer + "/" + str(key))
        elif isinstance(value, list):
            for i, child in enumerate(value):
                yield from collect_cues(child, pointer + "/" + str(i))
        elif isinstance(value, str) and re.search(r"(?i)default|customiz|(?:^|[^a-z])(?:exp|turn|judge|notes|beam|fcombo|lane)\d{2,3}", value):
            yield {"pointer": pointer, "snippet": value[:512], "truncated": len(value) > 512}
    for file in sorted((raw / "afp").glob("*")):
        if not file.is_file() or file.suffix == ".xml":
            continue
        bsi = raw / "afp/bsi" / file.name
        row = {"name": file.name, "raw": file.relative_to(media).as_posix(),
            "sha256": sha(file), "bsi": bsi.relative_to(media).as_posix() if bsi.is_file() else None}
        captured = io.StringIO()
        try:
            movie = SWF(file.name, file.read_bytes(), bsi.read_bytes() if bsi.is_file() else b"")
            with contextlib.redirect_stdout(captured), contextlib.redirect_stderr(captured):
                movie.parse()
            payload = movie.as_dict()
            out = target / "afp" / (file.name + ".json")
            write_json(out, payload)
            default_cues.extend({"movie": file.name, "parsedSource": out.relative_to(media).as_posix(), **cue}
                for cue in collect_cues(payload))
            unknown = [t.as_dict() for t in walk(movie.tags) if isinstance(t, PlaceObject) and t.unrecognized_options]
            row.update(parsed=movie.parsed, data=out.relative_to(media).as_posix(), fps=movie.fps,
                frames=len(movie.frames), stage=payload["location"], imports=payload["imported_tags"],
                exports=payload["exported_tags"], labels=movie.labels, unknownPlacements=unknown)
        except (OSError, MemoryError):
            raise
        except Exception as error:
            # A compatibility failure is recoverable for an art catalogue: raw
            # payloads remain intact and the affected clip is explicitly unknown.
            row.update(parsed=False, error=f"{type(error).__name__}: {error}")
        if captured.getvalue():
            log = target / "logs" / (file.name + ".txt")
            log.parent.mkdir(parents=True, exist_ok=True)
            log.write_text(captured.getvalue(), encoding="utf-8")
            row["parserLog"] = log.relative_to(media).as_posix()
        movies.append(row)
    for file in sorted((raw / "geo").glob("*")):
        if not file.is_file():
            continue
        row = {"name": file.name, "raw": file.relative_to(media).as_posix(), "sha256": sha(file)}
        try:
            shape = Shape(file.name, file.read_bytes())
            shape.parse(text_obfuscated=False)
            payload = shape.as_dict()
            out = target / "geo" / (file.name + ".json")
            write_json(out, payload)
            row.update(parsed=shape.parsed, data=out.relative_to(media).as_posix(), bounds=payload["bounds"],
                regions=[p.region for p in shape.draw_params if p.region], vertexCount=len(shape.vertex_points))
        except (OSError, MemoryError):
            raise
        except Exception as error:
            row.update(parsed=False, error=f"{type(error).__name__}: {error}")
        geometry.append(row)
    afplist = raw / "afp/afplist.xml"
    afp_entries = [{"name": node.attrib.get("name"), "geoIds": node.findtext("geo", "").split()}
        for node in ET.parse(afplist).getroot().findall("afp")] if afplist.is_file() else []
    result = {"schemaVersion": 1, "rawRoot": raw.relative_to(media).as_posix(), "afplist": afp_entries,
        "movies": movies, "geometry": geometry, "parsedMovieCount": sum(bool(m.get("parsed")) for m in movies),
        "parsedGeometryCount": sum(bool(g.get("parsed")) for g in geometry),
        "unknownPlacementCount": sum(len(m.get("unknownPlacements", [])) for m in movies),
        "scope": "Exact parsed source structures. No external host input, defaults, material selector, filter support or runtime reproduction is inferred."}
    write_json(target / "default-binding-cues.json", {"status": "source-cues-only", "resolvedOriginalHostDefaults": None,
        "interpretation": "Raw names/default-case text are search leads, never proof of a selected gameplay default. Full original parsed JSON remains linked.",
        "cues": default_cues})
    result["defaultBindingCues"] = (target / "default-binding-cues.json").relative_to(media).as_posix()
    write_json(target / "package.json", result)
    return result


def textures_for(raw, sprites, source, media, Image):
    tree = ET.parse(raw / "tex/texturelist.xml").getroot()
    xml_images = {}
    for atlas in tree.findall("texture"):
        sampler = {"atlas": atlas.attrib["name"], "mag": atlas.attrib.get("mag_filter"), "min": atlas.attrib.get("min_filter"),
            "wrapS": atlas.attrib.get("wrap_s"), "wrapT": atlas.attrib.get("wrap_t")}
        for node in atlas.findall("image"):
            xml_images[node.attrib["name"]] = (node, sampler)
    rows = []
    for path in sorted(sprites.glob("*.png")):
        name = path.stem
        if name not in xml_images:
            raise ValueError(f"Decoded PNG is not named in the preserved texturelist: {source}/{name}")
        node, sampler = xml_images[name]
        image_rect = [int(x) // 2 for x in node.findtext("imgrect").split()]
        uv_rect = [int(x) // 2 for x in node.findtext("uvrect").split()]
        with Image.open(path) as decoded:
            decoded.load()
            rgba = decoded.convert("RGBA")
        if rgba.size != (uv_rect[1] - uv_rect[0], uv_rect[3] - uv_rect[2]):
            raise ValueError("Decoded PNG dimensions changed the original UV margins")
        raw_png = raw / "tex" / path.name
        if not raw_png.is_file():
            raise ValueError("The uncropped original PNG companion is missing")
        with Image.open(raw_png) as image:
            image.verify()
        rows.append({"package": source, "name": path.name, "width": rgba.width, "height": rgba.height,
            "imageRect": image_rect, "uvRect": uv_rect, "sprite": path.relative_to(media).as_posix(),
            "raw": raw_png.relative_to(media).as_posix(), "sha256": sha(path),
            "rgbaSha256": hashlib.sha256(rgba.tobytes()).hexdigest(), "alphaBounds": rgba.getchannel("A").getbbox(),
            "sampler": sampler})
    if len(rows) != len(xml_images):
        raise ValueError(f"Texture XML and decoded PNG counts differ: {source}")
    return rows


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--game-root", required=True, type=Path)
    ap.add_argument("--output", required=True, type=Path)
    ap.add_argument("--metadata-output", required=True, type=Path)
    ap.add_argument("--selection", required=True, type=Path)
    ap.add_argument("--ifstools-path", required=True, type=Path)
    ap.add_argument("--afp-parser-path", required=True, type=Path)
    ap.add_argument("--reuse-library", action="append", default=[], type=Path)
    ap.add_argument("--workers", type=int, default=4)
    ap.add_argument("--resume", action="store_true", help="Continue this exact owned catalogue after verifying its checkpoint and source inventory")
    args = ap.parse_args()
    source, media, metadata = args.game_root.resolve(strict=True), args.output.resolve(), args.metadata_output.resolve(strict=True)
    if (media.exists() != args.resume) or not media.parent.is_dir() or media.drive.lower() != "e:" or metadata.drive.lower() != "f:":
        ap.error("Use a new E media folder and the existing F metadata folder")
    if media.is_relative_to(source) or source.is_relative_to(media) or os.environ.get("TEMP", "").lower().startswith("c:"):
        ap.error("Source/media must be separate and UseDevelopmentStorage must be active")
    selected = read_json(args.selection)
    if not isinstance(selected, list) or not selected or len(selected) != len(set(selected)):
        ap.error("Selection must be a nonempty distinct list of original relative IFS paths")
    selected = sorted(selected, key=lambda s: ([f[0] for f in FAMILIES].index(family(s)), s))
    before = inventory(source)
    if args.resume:
        state = read_json(media / "customization-catalog.json")
        old_inventory = read_json(media / "source-inventory-before.json")
        if (state["status"] != "extracting" or state["mediaRoot"] != str(media)
                or state["selectionSha256"] != sha(args.selection) or old_inventory != before):
            ap.error("Resume requires the exact owned selection/media root and unchanged original inventory")
    before_by_name = {r["path"]: r for r in before}
    for relative in selected:
        if relative not in before_by_name or not relative.startswith(PREFIX) or not relative.endswith(".ifs"):
            ap.error(f"Selected source is not in the original graphic inventory: {relative}")
    source_bytes = sum(before_by_name[p]["bytes"] for p in selected)
    if shutil.disk_usage(media.parent).free < max(source_bytes * 16, 1024 ** 3):
        ap.error("Insufficient independent media space for the conservative archive/PNG/author-data allowance")
    sys.path.insert(0, str(args.ifstools_path.resolve(strict=True)))
    from ifstools import IFS
    import ifstools.ifs as ifs_module
    from ifstools.handlers.image_file import ImageFile
    from PIL import Image
    # Limit this catalogue process only, leaving the bundled dependency untouched.
    ifs_module.ThreadPoolExecutor = partial(ThreadPoolExecutor, max_workers=args.workers)
    parser_types = load_afp_parser(args.afp_parser_path.resolve(strict=True))
    from build_library import extract_copy
    reuse = defaultdict(list)
    for root in args.reuse_library:
        root = root.resolve(strict=True)
        data = read_json(root / "library.json")
        if data["status"] != "complete":
            ap.error("Only completed independent libraries may be reused")
        for package in data["packages"]:
            original = package.get("originalSource", package["source"])
            reuse[original].append((root, package))
    if args.resume:
        completed = read_json(media / "library.json")
        package_rows, texture_rows = completed["packages"], completed["textures"]
        if [p["source"] for p in package_rows] != [p["source"] for p in state["packages"]]:
            ap.error("Resume checkpoint library/catalogue identities differ")
        for package in package_rows:
            if sha(inside(media, Path("source-copies") / package["source"])) != package["sha256"]:
                ap.error("A completed independent source copy changed; preserve and investigate")
        print(f"Verified resume checkpoint: {len(package_rows)} complete packages retained", flush=True)
    else:
        media.mkdir()
        write_json(media / "source-inventory-before.json", before)
        write_json(metadata / "source-inventory-before.json", before)
        package_rows, texture_rows = [], []
        state = {"schemaVersion": 1, "status": "extracting", "mediaRoot": str(media), "library": "library.json",
            "selectionCount": len(selected), "selectedSourceBytes": source_bytes,
            "selectionSha256": sha(args.selection), "packages": [], "sourceGameLaunched": False, "sourceGameWritten": False,
            "fullCombo": {"ownedBySeparateAudit": True, "includedHere": False}}

    def checkpoint():
        write_json(media / "packages.json", package_rows)
        write_json(media / "library.json", {"schemaVersion": 1, "status": state["status"], "sourceMetadataUnchanged": state["status"] == "complete",
            "packages": package_rows, "textures": texture_rows, "sourceFileCount": len(before),
            "uniqueSpriteContents": len({t["sha256"] for t in texture_rows}), "note": "Original source names0/1 retained; catalogue availability does not declare a runtime consumer."})
        write_json(media / "customization-catalog.json", state)
        write_json(metadata / "catalog.json", state)

    checkpoint()
    completed_sources = {p["source"] for p in package_rows}
    for number, relative in enumerate(selected, 1):
        if relative in completed_sources:
            continue
        original = inside(source, relative)
        stat = original.stat()
        expected = sha(original)
        copied = media / "source-copies" / relative
        copied.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(original, copied)
        if sha(copied) != expected:
            raise RuntimeError("Physical independent copy did not match the source SHA")
        package_root = media / "packages" / Path(relative).with_suffix("")
        raw, sprites = package_root / "raw", package_root / "sprites"
        reused = None
        for reuse_root, package in reuse.get(relative, []):
            reuse_sha = package.get("sha256") or package.get("copySha256")
            if reuse_sha != expected:
                continue
            old = inside(reuse_root, Path("packages") / Path(package["source"]).with_suffix(""))
            copy_tree(old / "raw", raw, reuse_root)
            copy_tree(old / "sprites", sprites, reuse_root)
            reused = str(reuse_root)
            break
        if reused is None:
            raw.parent.mkdir(parents=True, exist_ok=False)
            extract_copy(copied, raw, ImageFile, IFS, cropped=False)
            extract_copy(copied, sprites, ImageFile, IFS, cropped=True)
        rows = textures_for(raw, sprites, relative, media, Image)
        author_root = media / "author-data" / Path(relative).with_suffix("")
        controls = parse_controls(raw, author_root, media, parser_types)
        after = original.stat()
        if (stat.st_size, stat.st_mtime_ns) != (after.st_size, after.st_mtime_ns) or sha(original) != expected:
            raise RuntimeError("Original source hash or metadata changed during read-only extraction")
        raw_files = [p for p in raw.rglob("*") if p.is_file()]
        package_rows.append({"source": relative, "sha256": expected, "sourceUnchanged": True,
            "sourceBytes": stat.st_size, "sourceMtimeNs": stat.st_mtime_ns, "spriteCount": len(rows),
            "rawFileCount": len(raw_files), "rawExtensions": dict(Counter(p.suffix.lower() for p in raw_files)),
            "authorData": (author_root / "package.json").relative_to(media).as_posix(),
            "afpCount": len(controls["movies"]), "geoCount": len(controls["geometry"]),
            "parsedAfpCount": controls["parsedMovieCount"], "parsedGeoCount": controls["parsedGeometryCount"],
            "unknownPlacementCount": controls["unknownPlacementCount"], "reusedIndependentLibrary": reused})
        texture_rows.extend(rows)
        item = descriptor(relative)
        item.update(status="complete", spriteCount=len(rows), authorData=package_rows[-1]["authorData"],
            afpCount=package_rows[-1]["afpCount"], geoCount=package_rows[-1]["geoCount"],
            parsedAfpCount=package_rows[-1]["parsedAfpCount"], parsedGeoCount=package_rows[-1]["parsedGeoCount"],
            originalPreviewExists=item.get("originalPreview") in before_by_name)
        state["packages"].append(item)
        checkpoint()
        print(f"{number}/{len(selected)} {relative}: {len(rows)} PNG, AFP {controls['parsedMovieCount']}/{len(controls['movies'])}, GEO {controls['parsedGeometryCount']}/{len(controls['geometry'])}", flush=True)
    previews = []
    for row in before:
        relative = row["path"]
        if not relative.startswith(PREFIX + "customize/") or not relative.lower().endswith(".jpg"):
            continue
        original = inside(source, relative)
        expected, stat = sha(original), original.stat()
        target = media / "previews" / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(original, target)
        with Image.open(target) as image:
            size = list(image.size)
            image.verify()
        after = original.stat()
        if sha(target) != expected or sha(original) != expected or (stat.st_size, stat.st_mtime_ns) != (after.st_size, after.st_mtime_ns):
            raise RuntimeError("Original menu JPEG changed during read-only copy")
        previews.append({"source": relative, "copy": target.relative_to(media).as_posix(), "sha256": expected, "size": size})
    after = inventory(source)
    write_json(media / "source-inventory-after.json", after)
    write_json(metadata / "source-inventory-after.json", after)
    if before != after:
        raise RuntimeError("Original graphic inventory changed; catalogue cannot claim unchanged source")
    write_json(media / "original-previews.json", previews)
    state.update(status="complete", completedAtUtc=datetime.now(timezone.utc).isoformat(),
        packageCount=len(package_rows), textureCount=len(texture_rows), decodedPixels=sum(t["width"] * t["height"] for t in texture_rows),
        uniquePixelContents=len({(t["rgbaSha256"], t["width"], t["height"]) for t in texture_rows}),
        originalPreviewCount=len(previews), sourceInventoryUnchanged=True,
        parsedAfpCount=sum(p["parsedAfpCount"] for p in package_rows), afpCount=sum(p["afpCount"] for p in package_rows),
        parsedGeoCount=sum(p["parsedGeoCount"] for p in package_rows), geoCount=sum(p["geoCount"] for p in package_rows),
        sourceCopyHashChecksAllPassed=True, ifstoolsVersion=importlib.metadata.version("ifstools"))
    checkpoint()
    write_json(metadata / "completion-receipt.json", {k: v for k, v in state.items() if k != "packages"})
    print(f"Complete: {len(package_rows)} non-FC packages, {len(texture_rows)} exact UV PNGs; original graphic unchanged.", flush=True)


if __name__ == "__main__":
    main()
