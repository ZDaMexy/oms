"""Recover lane345's complete nested cat without changing its source library.

Explicit OMS author adaptation: the original border loops at 128/60 seconds;
the original embedded cat loops at 256/60 seconds. A private in-memory scheduler
extends only the otherwise transparent root with empty ticks so the same child
can advance. No original child frame, FPS, PNG or UV is replaced. This is not an
assertion about the guest original game's unavailable CUSTOM selector.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import io
import json
from pathlib import Path
import contextlib

import brotli
from PIL import Image, ImageDraw

from build_fullcombo_catalog import (InternTable, LocalStateRecorder,
    to_recorded_animation, world_matrix)
from customization_modules import ModuleBuilder
from export_customization_animations import evaluator_tools, library_index, source_geometry
from lighting_gameplay_controls import inside, require
from lighting_persistent import _independent as _independent_media
from music_progress import _stable_id

CONTRACT = "oms-iidx-dynamic-cover-lane345.v1"
PACKAGE = "contents/data/graphic/lane345_anim.ifs"
OLD_ANIMATION = "l.clip.n345.clip"
PREFIXES = {"cat": "dc.l345.cat", "border": "dc.l345.border"}
PIN = "1e66fcd780afb3088ce8e78c88615a143db492ee"
PINNED_BLOBS = {"bemani/format/afp/render.py": "f566367eddcfd9c01bb923088f9e5ac19b14fcc4",
    "bemani/format/afp/swf.py": "32fd91b3934646a13e0678a310c39af5a2e1abcf",
    "bemani/format/afp/geo.py": "ebb2ddcb2eb30fea60c907bce1870d6452ef53d0"}


def _independent(path, **kwargs):
    require("private-data" not in {part.casefold() for part in Path(path).parts}, "Protected player data is outside dynamic artwork authoring")
    result = _independent_media(path, **kwargs)
    require("private-data" not in {part.casefold() for part in result.parts}, "Protected player data is outside dynamic artwork authoring")
    return result


def digest(data):
    return hashlib.sha256(data).hexdigest()


def save(path, value):
    path = Path(path)
    require(not path.exists(), "Evidence output already exists: " + str(path))
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, separators=(",", ":"), allow_nan=False) + "\n", encoding="utf-8")


def walk(value):
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from walk(child)
    elif isinstance(value, list):
        for child in value:
            yield from walk(child)


def verify_evaluator(root):
    """Verify unpacked source bytes against the pinned tree, never git rev-parse."""
    root = _independent(root)
    tree_path = root.parent / "bemaniutils-tree.json"
    data = tree_path.read_bytes()
    tree = json.loads(data.decode("utf-8-sig"))
    require(tree["sha"] == PIN and tree["truncated"] is False, "Use the complete pinned bemani tree receipt")
    entries = {item["path"]: item for item in tree["tree"]}
    inputs = [{"path": str(tree_path), "sha256": digest(data)}]
    for relative, expected in PINNED_BLOBS.items():
        require(entries[relative]["sha"] == expected, "The public pinned parser/renderer blob changed")
    for relative, entry in sorted(entries.items()):
        if not relative.startswith("bemani/format/afp/") or not relative.endswith(".py"):
            continue
        path = inside(root, relative)
        source = path.read_bytes()
        blob = hashlib.sha1(b"blob " + str(len(source)).encode("ascii") + b"\0" + source).hexdigest()
        require(blob == entry["sha"], "An unpacked pinned AFP source file changed: " + relative)
        inputs.append({"path": str(path), "sha256": digest(source), "gitBlob": blob})
    return {"commit": PIN, "tree": str(tree_path), "verifiedFiles": len(inputs) - 1, "readInputs": inputs,
            "method": "git-blob-content-against-pinned-tree; directory is not treated as a Git checkout"}


def clock_movie(movie, branch, *, ticks=None):
    """Copy only the scheduling root; its real 256-frame child is never edited."""
    require(branch in PREFIXES and movie.fps == 60 and len(movie.frames) == 128,
            "Use the verified lane345 128@60 root")
    selected = copy.copy(movie)
    count = ticks if ticks is not None else (512 if branch == "cat" else 128)
    require(type(count) is int and count >= 128 and count <= 512 and count % 128 == 0,
            "The independent scheduler must remain bounded")
    empty = next((frame for frame in movie.frames if frame.num_tags == 0), None)
    require(empty is not None, "A genuine empty source tick is required for transparent scheduling")
    selected.frames = list(movie.frames) + [copy.copy(empty) for _ in range(count - 128)]
    return selected


def branch_renderer(tools, renderer_class, branch):
    require(branch in PREFIXES, "Unknown independent source branch")
    class ClockRenderer(renderer_class):
        def _AFPRenderer__place(self, tag, clip, prefix=""):
            if clip is self._AFPRenderer__root:
                if isinstance(tag, tools.AP2PlaceObjectTag):
                    is_cat = tag.source_tag_id == 279
                    if (branch == "cat") != is_cat:
                        return None, False
                elif branch == "cat" and type(tag).__name__ == "AP2RemoveObjectTag":
                    return None, False
            return super()._AFPRenderer__place(tag, clip, prefix)
    return ClockRenderer


def visible_draws(root, frame, geometries, rows):
    """Canonical source-space draw identity for graph/FPS comparisons and proof PNGs."""
    worlds, visible, result = {}, {}, []
    identity = [[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]]
    for node_id, state_id in root["frames"][frame]["nodes"]:
        node, state = root["nodes"][node_id], root["states"][state_id]
        parent = node["parent"]
        shown = state["visible"] and visible.get(parent, True)
        visible[node_id] = shown
        matrix = world_matrix(state["matrix"], state["origin"], worlds.get(parent, identity))
        worlds[node_id] = matrix
        if not shown or node["geometry"] is None:
            continue
        require(state["projection"] in (0, 1) and state["mult"] == [1, 1, 1, 1]
                and state["add"] == [0, 0, 0, 0] and state["hsl"] == [0, 0, 0]
                and not state["filters"] and state["mask"] is None and state["blend"] in (0, 1, 2),
                "The lane345 proof renderer only accepts its measured plain affine source draws")
        geometry = geometries[node["geometry"]]
        require(len(geometry["draws"]) == 1 and not geometry["colours"], "Unexpected lane345 source primitive")
        draw = geometry["draws"][0]
        require(draw["texture"] is not None, "A lane345 proof draw needs its original PNG")
        row = rows[draw["texture"]]
        vertices = [[sum([*point, 1][k] * matrix[k][axis] for k in range(4)) for axis in range(3)]
                    for point in geometry["vertices"]]
        result.append({"source": geometry["source"], "name": row["name"], "sprite": row["sprite"], "sha256": row["sha256"],
            "rgbaSha256": row["rgbaSha256"], "size": [row["width"], row["height"]],
            "vertices": [[round(value, 7) for value in point] for point in vertices],
            "uv": [[round(value, 6) for value in uv] for uv in draw["uv"]], "indices": draw["indices"]})
    return result


def draw_frame(draws, library):
    """Reference-only nearest raster for measured axis-aligned, full-source quads."""
    canvas = Image.new("RGBA", (432, 724))
    for draw in draws:
        width, height = draw["size"]
        require(all(abs(value - expected) <= 1e-5 for uv, wanted in zip(draw["uv"], [[0, 0], [1, 0], [0, 1], [1, 1]])
                    for value, expected in zip(uv, wanted))
                and draw["indices"] == [0, 1, 2, 2, 1, 3], "Proof PNGs require full original nearest/clamp quads")
        a, b, c, d = draw["vertices"]
        require(a[1] == b[1] and c[1] == d[1] and a[0] == c[0] and b[0] == d[0]
                and abs(b[0] - a[0]) == width and abs(c[1] - a[1]) == height,
                "The proof raster must not silently rescale or rotate source pixels")
        with Image.open(inside(library, draw["sprite"])) as source:
            image = source.convert("RGBA")
        if b[0] < a[0]:
            image = image.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        if c[1] < a[1]:
            image = image.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
        x, y = min(a[0], b[0]), min(a[1], c[1])
        require(x == round(x) and y == round(y), "Proof source placement is not pixel-aligned")
        canvas.alpha_composite(image, (int(x), int(y)))
    return canvas


def encode_lossless_atlas(source, target):
    """Change only a derived atlas encoding; prove every decoded RGBA byte equal."""
    source, target = Path(source), Path(target)
    require(not target.exists(), "A lossless derived atlas output already exists")
    with Image.open(source) as image:
        original = image.convert("RGBA")
        pixels = original.tobytes()
        size = original.size
        original.save(target, format="WEBP", lossless=True, quality=100, method=6, exact=True)
    with Image.open(target) as image:
        require(image.size == size and image.convert("RGBA").tobytes() == pixels,
                "Lossless atlas encoding changed original pixels, including transparent RGB")
    return {"method": "WebP-lossless-exact-RGBA; derived atlas only", "sourceEncoding": "PNG", "targetEncoding": "WebP-lossless",
        "decodedRgbaSha256": digest(pixels), "wholeCanvasPixelsUnchanged": True,
        "pngBytes": source.stat().st_size, "encodedBytes": target.stat().st_size,
        "sourceOriginalPngsChanged": False, "uvOrSamplerChanged": False}


def export_source(library, output, bemani_root, image_tools):
    library, output = _independent(library), _independent(output, output=True, exists=False)
    require(not output.exists(), "Create a new independent dynamic-cover evidence directory")
    evaluator = verify_evaluator(bemani_root)
    packages, texture_index = library_index(library)
    source = packages[PACKAGE]
    metadata_path = inside(library, source["authorData"])
    metadata_data = metadata_path.read_bytes()
    metadata = json.loads(metadata_data.decode("utf-8-sig"))
    rows = list(texture_index[PACKAGE])
    tools, source_swf, state_renderer = evaluator_tools(Path(bemani_root).resolve(strict=True), Path(image_tools).resolve(strict=True))
    inputs = evaluator["readInputs"] + [{"path": str(library / "library.json"), "sha256": digest((library / "library.json").read_bytes())},
                                     {"path": str(metadata_path), "sha256": digest(metadata_data)}]
    for path in (inside(library, metadata["rawRoot"]) / "tex/texturelist.xml",
                 Path(__file__), Path(__file__).with_name("build_fullcombo_catalog.py"),
                 Path(__file__).with_name("export_customization_animations.py"),
                 Path(__file__).with_name("customization_modules.py")):
        inputs.append({"path": str(path.resolve()), "sha256": digest(path.read_bytes())})
    captured = io.StringIO()
    with contextlib.redirect_stdout(captured), contextlib.redirect_stderr(captured):
        shapes, geometries, mapping = source_geometry(tools, library, metadata, rows)
        movies, filters = {}, InternTable()
        for item in metadata["movies"]:
            raw, bsi = inside(library, item["raw"]), inside(library, item["bsi"])
            require(digest(raw.read_bytes()) == item["sha256"], "An original lane345 AFP changed")
            movie = source_swf(item["name"], raw.read_bytes(), bsi.read_bytes())
            movie.parse()
            movies[item["name"]] = movie
            inputs.extend([{"path": str(path), "sha256": digest(path.read_bytes())} for path in (raw, bsi)])
            for block in movie.export_filter_blocks:
                filters.add(block)
        for item in metadata["geometry"]:
            inputs.append({"path": str(inside(library, item["raw"])), "sha256": item["sha256"]})
        main = movies["lane345_anim"]
        require(len(main.frames) == 128 and main.fps == 60 and main.exported_tags["cat_movie_01"] == 279,
                "The original lane345 root changed")
        roots, recorders = {}, {}
        for branch in ("cat", "border"):
            selected = clock_movie(main, branch)
            local_movies = {**movies, "lane345_anim": selected}
            recorder = LocalStateRecorder(tools, branch_renderer(tools, state_renderer, branch))
            roots[branch] = recorder.record(PACKAGE, branch, selected, local_movies, shapes, mapping, filters)
            roots[branch]["sourceDeclaredFrames"] = 128
            roots[branch]["clockPolicy"] = "Explicit OMS branch clock; only the copied transparent parent scheduler has extra empty ticks"
            recorders[branch] = {"checks": recorder.math_checks, "maximumError": recorder.math_max_error}
        independent = movies["cat_movie_01"]
        require(independent.fps == 30 and len(independent.frames) == 128, "Independent original cat FPS changed")
        comparison = LocalStateRecorder(tools, state_renderer).record(PACKAGE, "original-cat-30fps", independent, movies, shapes, mapping, filters)
        full_original = LocalStateRecorder(tools, state_renderer).record(PACKAGE, "original-main-60fps", main, movies, shapes, mapping, filters)
    warnings = sorted(set(captured.getvalue().splitlines()))
    require(not [line for line in warnings if line.startswith("WARNING:")
                 and "includes unparsed options and might not display properly!" not in line], "Unsupported source evaluation warnings")
    output.mkdir(parents=True)
    save(output / "cat-512-source-root.json", roots["cat"])
    save(output / "border-128-source-root.json", roots["border"])
    save(output / "source-geometry.json", {"geometries": geometries, "filters": filters.values})
    cat_identity = [node["id"] for node in roots["cat"]["nodes"] if node["sourceTag"] == 279]
    require(len(cat_identity) == 1 and all(cat_identity[0] in dict(frame["nodes"]) for frame in roots["cat"]["frames"][:-1]),
            "The same real child placement must survive the full bounded scheduler")
    source_frames, contact = [], Image.new("RGB", (8 * 116, 4 * 214), (28, 30, 34))
    painter = ImageDraw.Draw(contact)
    for tick in range(128):
        expected_border = [draw for draw in visible_draws(full_original, tick, geometries, rows)
                           if not draw["name"].startswith("cat_movie_")]
        require(expected_border == visible_draws(roots["border"], tick, geometries, rows),
                "Extracting the cat changed a real source border draw")
    for tick in range(512):
        draws = visible_draws(roots["cat"], tick, geometries, rows)
        original = visible_draws(comparison, (tick % 256) // 2, geometries, rows)
        require(len(draws) == len(original) == 1, "The actual cat branch must have one drawn source image")
        expected = f"cat_movie_{(tick % 256) // 4 + 1:02}.png"
        require(draws[0]["name"] == expected and original[0]["name"] == expected, "Source cat frame cadence is not four 60Hz ticks")
        require({key: value for key, value in draws[0].items() if key != "source"}
                == {key: value for key, value in original[0].items() if key != "source"},
                "Embedded 60fps cat differs from its genuine independent 30fps source draw")
        source_frames.append({"tick": tick, "sourceImage": expected, "sourceSha256": draws[0]["sha256"],
                              "independent30fpsFrame": (tick % 256) // 2, "childPlacementNode": cat_identity[0]})
        if 128 <= tick < 256 and tick % 4 == 0:
            image = draw_frame(draws, library)
            require(digest(image.tobytes()) == draws[0]["rgbaSha256"], "A full cat proof viewport changed source pixels")
            original_path = inside(library, draws[0]["sprite"])
            tail = output / "tail-originals" / expected
            tail.parent.mkdir(exist_ok=True)
            tail.write_bytes(original_path.read_bytes())
            border = draw_frame(visible_draws(roots["border"], tick % 128, geometries, rows), library)
            image.alpha_composite(border)
            image_path = output / "composite" / f"tick{tick:03}.png"
            image_path.parent.mkdir(exist_ok=True)
            image.save(image_path)
            index = (tick - 128) // 4
            preview = image.convert("RGB")
            preview.thumbnail((108, 188))
            contact.paste(preview, ((index % 8) * 116 + 4, (index // 8) * 214))
            painter.text(((index % 8) * 116 + 4, (index // 8) * 214 + 190), f"{expected[10:12]} / {tick}", fill="white")
    contact.save(output / "tail-composite-contact.jpg", quality=93)
    source_package = {"geometries": geometries, "filters": filters.values}
    resources = {index: "dc.source.t" + row["sha256"][:24] for index, row in enumerate(rows)}
    exported, all_files, all_resources, animations = {}, {}, {}, []
    for branch, period in (("cat", 256), ("border", 128)):
        prefix = PREFIXES[branch]
        root = copy.deepcopy(roots[branch])
        root["frames"] = root["frames"][:period] + [{"nodes": [], "camera": None}]
        root["frameCount"], root["endFrameIndex"], root["durationMs"] = period, period, period * 1000 / 60
        clip = to_recorded_animation(source_package, root, resources)
        clip.update({"loop": True, "trigger": "gameplay.attach"})
        used = {geometry["resource"] for geometry in clip["geometries"] if geometry["resource"] is not None}
        declarations, files = {}, {}
        for index, row in enumerate(rows):
            if resources[index] not in used:
                continue
            png = inside(library, row["sprite"])
            data = png.read_bytes()
            require(digest(data) == row["sha256"] and row["sampler"]["min"] == row["sampler"]["mag"] == "nearest"
                    and row["sampler"]["wrapS"] == row["sampler"]["wrapT"] == "clamp", "Original source PNG/sampler changed")
            inputs.append({"path": str(png), "sha256": row["sha256"]})
            relative = "custom/dc-source/" + row["sha256"] + ".png"
            declarations[resources[index]] = {"id": resources[index], "type": "texture", "path": relative, "filter": "nearest"}
            files[relative] = {"source": str(png), "path": relative, "sha256": row["sha256"]}
        branch_dir = output / branch
        branch_dir.mkdir()
        declarations, files, atlas = ModuleBuilder.pack_large_lane_clip(None, clip, declarations, files, prefix, branch_dir)
        compact_resources, compact_files = {}, {}
        remap = {}
        for page_index, page in enumerate(atlas["pages"]):
            number = len(all_resources) + page_index
            identifier, relative = "dc.r" + str(number), "dc/r" + str(number) + ".png"
            old_identifier, old_path = page["resource"], page["path"]
            selected_file = files[old_path]
            if branch == "cat" and page_index == 0:
                encoded_atlas = branch_dir / "module-atlas-p0.webp"
                page["encoding"] = encode_lossless_atlas(selected_file["source"], encoded_atlas)
                relative = "dc/r" + str(number) + ".webp"
                selected_file = {"source": str(encoded_atlas), "sha256": digest(encoded_atlas.read_bytes())}
            remap[old_identifier] = identifier
            compact_resources[identifier] = {**declarations[old_identifier], "id": identifier, "path": relative}
            compact_files[relative] = {**selected_file, "path": relative}
            page.update({"resource": identifier, "path": relative})
        for geometry in clip["geometries"]:
            if geometry["resource"] is not None:
                geometry["resource"] = remap[geometry["resource"]]
        declarations, files = compact_resources, compact_files
        encoded = json.dumps(clip, separators=(",", ":"), allow_nan=False).encode("utf-8")
        compressed = brotli.compress(encoded, quality=5)
        path = branch_dir / "clip.json.br"
        path.write_bytes(compressed)
        short_name = "a" if branch == "cat" else "b"
        relative = "dc/" + short_name + ".json.br"
        files[relative] = {"source": str(path), "path": relative, "sha256": digest(compressed)}
        animation = {"id": "dc." + short_name, "path": relative, "resources": sorted(declarations)}
        animations.append(animation)
        all_files.update(files)
        all_resources.update(declarations)
        exported[branch] = {"periodFrames": period, "fps": 60, "framesIncludingEmptyEnd": len(clip["frames"]),
            "durationMs": period * 1000 / 60, "animation": animation["id"], "atlas": atlas,
            "runtimeBytes": len(compressed), "decodedJsonBytes": len(encoded),
            "nodes": len(clip["nodes"]), "states": len(clip["states"]), "geometries": len(clip["geometries"])}
    for identifier in [*all_resources, *(row["id"] for row in animations)]:
        _stable_id(identifier)
    for item in inputs:
        require(digest(Path(item["path"]).read_bytes()) == item["sha256"], "A read-only source changed during export")
    result = {"contract": CONTRACT, "library": str(library), "sourceArchiveSha256": source["sha256"],
        "evaluator": evaluator, "readInputs": inputs, "resources": list(all_resources.values()),
        "animations": animations, "files": list(all_files.values()), "branches": exported,
        "proof": {"catSchedule": source_frames, "sameChildPlacementAcross512Ticks": True,
            "all256TicksMatchIndependentOriginal30fps": True, "fullSecondCycleMatches": True,
            "all128BorderTicksMatchUnmodifiedSourceMain": True,
            "tailOriginalPngCount": 32, "sourcePixelsChanged": False, "sourceUVChanged": False,
            "atlasChange": "Lossless repeated-clamp-edge pages; only corresponding geometry UV addresses are rebased",
            "borderPeriodFrames": 128, "catPeriodFrames": 256, "fps": 60,
            "sourceRootDeclaredFrames": 128, "transparentCatSchedulerTicks": 512,
            "sourceOriginalChildFramesAndFpsChanged": False, "recorders": recorders, "warnings": warnings},
        "budget": {"resources": len(all_resources), "animations": len(animations),
            "pixels": sum(page["size"][0] * page["size"][1] for branch in exported.values() for page in branch["atlas"]["pages"]),
            "fontsAdded": 0, "optionsAdded": 0},
        "qualification": "Explicit OMS independent border/cat loop adaptation. Source graph/pixels verified offline; original-game CUSTOM and native OMS playback have not been claimed by this export."}
    save(output / "reference.json", result)
    return result


def load_reference(path):
    path = Path(path).resolve(strict=True)
    owner = _independent(path.parent)
    reference = json.loads(path.read_text(encoding="utf-8-sig"))
    require(reference.get("contract") == CONTRACT and set(reference["branches"]) == {"cat", "border"},
            "Use the complete independent dynamic-cover reference")
    for branch, period in (("cat", 256), ("border", 128)):
        detail = reference["branches"][branch]
        require(detail["periodFrames"] == period and detail["fps"] == 60
                and detail["framesIncludingEmptyEnd"] == period + 1, "The verified independent branch clock changed")
    require(reference["proof"]["sameChildPlacementAcross512Ticks"]
            and reference["proof"]["all256TicksMatchIndependentOriginal30fps"]
            and reference["proof"]["all128BorderTicksMatchUnmodifiedSourceMain"]
            and len(reference["proof"]["catSchedule"]) == 512,
            "The original nested placement continuity proof is missing")
    for tick, item in enumerate(reference["proof"]["catSchedule"]):
        require(item["tick"] == tick and item["sourceImage"] == f"cat_movie_{(tick % 256) // 4 + 1:02}.png"
                and item["independent30fpsFrame"] == (tick % 256) // 2, "The measured original cat cadence changed")
    for item in reference["files"]:
        source = _independent(item["source"])
        require(source.is_relative_to(owner) and digest(source.read_bytes()) == item["sha256"],
                "A generated independent atlas/clip changed")
    for item in reference["readInputs"]:
        source = _independent(item["path"])
        require(digest(source.read_bytes()) == item["sha256"], "An immutable evaluator/library input changed")
    return reference


def transform_contribution(manifest, scene, reference):
    """Replace only the existing l345 animation in the same material/layout host."""
    require(reference.get("contract") == CONTRACT, "Use the verified complete lane345 export")
    result, changed = copy.deepcopy(manifest), copy.deepcopy(scene)
    previous = [item for item in result["animations"] if item["id"] == OLD_ANIMATION]
    require(len(previous) == 1, "The contribution must reference its existing lane345 animation")
    old_resources = set(previous[0]["resources"])
    require(not any(old_resources.intersection(item["resources"]) for item in result["animations"] if item["id"] != OLD_ANIMATION)
            and not any(item.get("resource") in old_resources for item in walk(scene)),
            "A replaced lane345 atlas unexpectedly serves another consumer")
    result["animations"] = [item for item in result["animations"] if item["id"] != OLD_ANIMATION] + copy.deepcopy(reference["animations"])
    result["resources"] = [item for item in result["resources"] if item["id"] not in old_resources] + copy.deepcopy(reference["resources"])
    replacements = []
    for node in list(walk(changed)):
        if node.get("type") != "recorded-animation" or node.get("animation") != OLD_ANIMATION:
            continue
        require(not node["children"], "An existing recorded animation unexpectedly has children")
        children = []
        short_id = "dc.n" + digest(node["id"].encode("utf-8"))[:12]
        for branch in ("cat", "border"):
            children.append({"id": short_id + (".c" if branch == "cat" else ".b"), "type": "recorded-animation", "target": {"kind": "global"},
                "animation": reference["branches"][branch]["animation"], "blend": "alpha", "properties": {}, "effects": [], "children": []})
        replacements.append(node["id"])
        node.update({"type": "container", "children": children})
        del node["animation"]
    require(replacements, "There is no actual lane345 source consumer to replace")
    ids = [item["id"] for item in walk(changed) if "id" in item]
    require(len(ids) == len(set(ids)), "New independent loop node ID collision")
    for identifier in ids:
        _stable_id(identifier)
    return result, changed, {"replacedConsumers": replacements, "ordinaryMaterialAndLayoutHostsPreserved": True,
        "source": CONTRACT, "fontsAdded": 0, "optionsAdded": 0, "nativeOriginalCustomVerified": False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--library", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--bemani-root", required=True, type=Path)
    parser.add_argument("--image-tools", required=True, type=Path)
    args = parser.parse_args()
    result = export_source(args.library, args.output, args.bemani_root, args.image_tools)
    print(json.dumps({"output": str(args.output), "budget": result["budget"], "proof": result["qualification"]}, ensure_ascii=True))


if __name__ == "__main__":
    main()
