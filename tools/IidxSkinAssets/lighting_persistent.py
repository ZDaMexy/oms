"""Split audited persistent Frame components without painting over baked PNGs.

This is an offline author tool.  The source library and old author are read-only;
the reference owns verified source clips and newly rendered background layers.
No original game, HDD, player data or runtime source interpreter is involved.
"""

from __future__ import annotations

import argparse
import contextlib
import copy
import hashlib
import io
import json
import math
from pathlib import Path
import sys

sys.dont_write_bytecode = True

from build_fullcombo_catalog import all_tags, to_recorded_animation
from export_customization_animations import evaluator_tools, library_index
from lighting_lane_intro import SOURCE_PACKAGES, _root_return_tags

CONTRACT = "oms-iidx-persistent-frame-reference.v1"
BPL_FRAMES = ("bpl-apina", "bpl-gamepanic", "bpl-gigo", "bpl-leisureland", "bpl-round1", "bpl-silkhat", "bpl-tradz")
ASTERISK_FRAMES = ("iidx33", *BPL_FRAMES)
FRAMES = ("iidx33", "iidx28", *BPL_FRAMES)
MOVIES = {"p1": "1p_frame", "p2": "2p_frame", "dp": "center_frame_dp_1p"}
CANVAS = (1920, 1080)
HANDOVER_MS = 1000


def _require(condition, message):
    if not condition:
        raise ValueError(message)


def _independent(path, *, output=False, exists=True):
    path = Path(path)
    forbidden = ("c:", "d:") if output else ("d:",)
    _require(path.drive.casefold() not in forbidden, "Persistent authoring requires an independent non-HDD path")
    path = path.resolve(strict=exists)
    _require(path.drive.casefold() not in forbidden, "Persistent authoring requires an independent non-HDD path")
    return path


def _file(root, relative, *, exists=True):
    _require(isinstance(relative, str) and relative and ":" not in relative and "\\" not in relative,
             "Persistent assets require contained relative paths")
    part = Path(relative)
    _require(not part.is_absolute() and ".." not in part.parts, "A persistent asset escapes its owner")
    result = (root / part).resolve(strict=exists)
    _require(result.is_relative_to(root), "A persistent asset escapes its owner")
    return result


def _json(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def _write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, separators=(",", ":"), allow_nan=False) + "\n", encoding="utf-8")


def _sha(data):
    return hashlib.sha256(data).hexdigest()


def _walk(node):
    yield node
    for child in node.get("children", []):
        yield from _walk(child)


def background_owner(scene, manifest, *, frame, layout):
    """Find the actual C2 owner even after all scene IDs have been compacted."""
    material = f"gameplay/frames/{frame}/backgrounds/{layout}"
    instances = [item for item in scene["instances"]
                 if item["material"] == {"slot": "stage.background", "resource": material}]
    _require(len(instances) == 1, "The selected contribution must have one exact Frame background instance")
    templates = [item for item in scene["templates"] if item["id"] == instances[0]["template"]]
    baked = [item for item in manifest["resources"] if item["path"] == material + ".png"]
    _require(len(templates) == len(baked) == 1, "The selected Frame background owner or baked resource changed")
    _require(any(node.get("resource") == baked[0]["id"] for node in _walk(templates[0]["root"])),
             "The selected background does not consume its catalog baked resource")
    return templates[0]["root"], baked[0]


def _source_owner(source, catalog, *, frame, layout):
    choices = [choice for module in catalog["modules"] if module["id"] == "frame"
               for choice in module["choices"] if choice["value"] == frame]
    _require(len(choices) == 1, "The author lacks the selected Frame choice")
    parts = [choices[0]] if layout == "dp" else [
        part for part in choices[0].get("variants", []) if part["when"].get("layout") == layout]
    _require(parts, "The author lacks the selected Frame layout")
    owners = []
    for part in parts:
        manifest, scene = (_json(_file(source, part[key])) for key in ("manifest", "scene"))
        _, baked = background_owner(scene, manifest, frame=frame, layout=layout)
        data = _file(source, baked["path"]).read_bytes()
        owners.append({"manifest": part["manifest"], "scene": part["scene"], "ini": part["ini"],
                       "baked": baked, "bakedSha256": _sha(data)})
    _require(len({row["bakedSha256"] for row in owners}) == 1, "Frame variants disagree on the baked background")
    return owners[0]


def select_components(root, package, texture_rows, *, frame, layout):
    """Keep only the proven mesh identities and every original ancestor."""
    _require(frame in FRAMES and layout in MOVIES, "Unknown persistent Frame/layout")
    names = {"unit_asterisk.png"} if frame in ASTERISK_FRAMES else {"lane_base.png", "lane_1p.png", "lane_2p.png"}
    selected = set()
    for node in root["nodes"]:
        if node["geometry"] is None:
            continue
        geometry = package["geometries"][node["geometry"]]
        if any(draw["texture"] is not None and draw["texture"] < len(texture_rows)
               and texture_rows[draw["texture"]]["name"] in names for draw in geometry["draws"]):
            selected.add(node["id"])
    expected = (2 if layout == "dp" else 1) * (1 if frame in ASTERISK_FRAMES else 2)
    _require(len(selected) == expected, "The audited persistent component closure changed")
    retained = set(selected)
    paths = []
    for identifier in sorted(selected):
        chain, current = [], identifier
        while current >= 0:
            retained.add(current)
            node = root["nodes"][current]
            chain.append([node["sourceTag"], node["depth"]])
            current = node["parent"]
        paths.append(list(reversed(chain)))
    result = copy.deepcopy(root)
    for node in result["nodes"]:
        if node["id"] not in selected:
            node["geometry"] = None
    for frame_value in result["frames"]:
        frame_value["nodes"] = [pair for pair in frame_value["nodes"] if pair[0] in retained]
    return result, {"meshes": sorted(selected), "ancestorNodes": len(retained) - len(selected), "omittedLayerPaths": paths}


def component_clip(root, package, resource_ids, *, frame, source_loop):
    """Preserve the source prefix/return or the exact settled one-shot handover."""
    selected = copy.deepcopy(root)
    _require(root["fps"] == 60 and root["frameCount"] == len(root["frames"]) - 1,
             "The source recording must retain its full 60Hz prefix and empty terminal")
    if frame in ASTERISK_FRAMES:
        _require(source_loop == 180 and root["frameCount"] == 300, "The asterisk requires its literal original180..299 return")
        # The recorder appends a non-playing clear sentinel. C5 uses Count-1
        # playable frames, so retain it while the loop remains exactly180..299.
        _require(root["frames"][-1] == {"nodes": [], "camera": None}, "The source recorder terminal changed")
    else:
        _require(frame == "iidx28" and source_loop == 180 and root["frameCount"] in (300, 420),
                 "28 requires its audited original root and loop label")
        settled = root["frames"][30]
        _require(all(value == settled for value in root["frames"][31:-1]),
                 "The selected28 persistent components do not settle at source frame30")
        selected["frames"] = copy.deepcopy(root["frames"][:31])
        selected["frames"].extend(copy.deepcopy(settled) for _ in range(60 - 31))
        selected["frames"].append({"nodes": [], "camera": None})
    clip = to_recorded_animation(package, selected, resource_ids)
    clip["trigger"] = "gameplay.attach"
    clip["loop"] = frame in ASTERISK_FRAMES
    if clip["loop"]:
        clip["loopStartFrame"] = source_loop
    return clip


def verify_background(rendered, expected_path, evidence):
    """A failed full RGBA baseline never becomes a split-background success."""
    from PIL import Image, ImageChops

    with Image.open(expected_path) as png:
        expected = png.convert("RGBA")
    actual = rendered.convert("RGBA")
    result = {"expectedPngSha256": _sha(Path(expected_path).read_bytes()),
              "expectedSize": list(expected.size), "renderedSize": list(actual.size),
              "expectedRgbaSha256": _sha(expected.tobytes()), "renderedRgbaSha256": _sha(actual.tobytes())}
    result["matches"] = expected.size == actual.size and expected.tobytes() == actual.tobytes()
    if not result["matches"]:
        evidence.mkdir(parents=True, exist_ok=True)
        actual.save(evidence / "baseline-render.png")
        result["renderedEvidence"] = "baseline-render.png"
        if expected.size == actual.size:
            difference = ImageChops.difference(expected, actual)
            mask = difference.getchannel("R")
            for channel in ("G", "B", "A"):
                mask = ImageChops.lighter(mask, difference.getchannel(channel))
            result["differentPixels"] = expected.width * expected.height - mask.histogram()[0]
            result["differenceBounds"] = mask.getbbox()
            difference.save(evidence / "baseline-difference.png")
    return result


class _SourceRaster:
    def __init__(self, library, metadata, textures, tools, source_swf):
        self.library, self.metadata, self.textures, self.tools, self.source_swf = library, metadata, textures, tools, source_swf
        self.shapes = {}
        for row in metadata["geometry"]:
            data = _file(library, row["raw"]).read_bytes()
            _require(row["parsed"] and _sha(data) == row["sha256"], "A preserved source GEO changed")
            shape = tools.Shape(row["name"], data)
            shape.parse(text_obfuscated=False)
            self.shapes[row["name"]] = shape
        self.movies, self.images, self.cache = {}, {}, {}

    def movie(self, name):
        if name not in self.movies:
            row = next(item for item in self.metadata["movies"] if item["name"] == name)
            data = _file(self.library, row["raw"]).read_bytes()
            _require(row["parsed"] and _sha(data) == row["sha256"], "A preserved source AFP changed")
            bsi = _file(self.library, row["bsi"])
            movie = self.source_swf(name, data, bsi.read_bytes())
            movie.parse()
            self.movies[name] = movie
        return self.movies[name]

    def render(self, name, omitted=()):
        from PIL import Image

        key = name, tuple(tuple(tuple(pair) for pair in path) for path in omitted)
        if key in self.cache:
            return self.cache[key].copy()
        tools, movie = self.tools, self.movie(name)
        named = {(tag.export_source_sprite, tag.object_id, tag.depth): tag.movie_name
                 for tag in all_tags(movie.tags, tools.AP2DefineSpriteTag)
                 if isinstance(tag, tools.AP2PlaceObjectTag) and tag.movie_name}
        omit = set(key[1])

        class StaticRenderer(tools.AFPRenderer):
            def __init__(self, **kwargs):
                super().__init__(**kwargs)
                self.stack, self.removed = [], set()

            def _AFPRenderer__render_object(self, image, obj, *args, **kwargs):
                parent = self.stack[-1][0] if self.stack else None
                instance = named.get((parent, obj.object_id, obj.depth))
                if instance and instance not in ("lane_1p", "lane_2p"):
                    return image
                if isinstance(obj, tools.PlacedShape) and any(draw.region == "playm_hs_dot" for draw in obj.source.draw_params):
                    return image
                path = (*self.stack, (obj.source.tag_id, obj.depth))
                if path in omit:
                    self.removed.add(path)
                    return image
                self.stack.append(path[-1])
                try:
                    return super()._AFPRenderer__render_object(image, obj, *args, **kwargs)
                finally:
                    self.stack.pop()

        renderer = StaticRenderer(shapes={}, textures={}, swfs={}, single_threaded=True, enable_aa=False)
        for shape_name, shape in self.shapes.items():
            renderer.add_shape(shape_name, shape)
        for row in self.textures:
            stem = row["name"].removesuffix(".png")
            if stem in renderer.textures:
                continue
            if stem not in self.images:
                data = _file(self.library, row["sprite"]).read_bytes()
                _require(_sha(data) == row["sha256"], "A preserved source PNG changed")
                with Image.open(io.BytesIO(data)) as image:
                    self.images[stem] = image.convert("RGBA")
            renderer.add_texture(stem, self.images[stem])
        renderer.add_swf(name, movie)
        images = list(renderer.render_path(name, background_color=tools.Color(0, 0, 0, 0), only_frames=[181]))
        _require(len(images) == 1 and renderer.removed == omit, "Source redraw did not remove the exact audited layer paths")
        self.cache[key] = images[0].convert("RGBA")
        return self.cache[key].copy()


def _compose(raster, research, *, frame, layout, omitted=()):
    from PIL import Image

    shift = 0 if layout == "dp" else 270 if layout == "p1" else -270
    centre_name = "center_frame_dp_1p" if layout == "dp" else "center_frame_sp"
    side_name = MOVIES[layout]
    if frame == "iidx33":
        side = raster.render(side_name, omitted)
        gameplay = _json(research / "gameplay-assets.json")
        if layout == "dp":
            background = side
        else:
            centre = raster.render(centre_name)
            for entry in gameplay["native_bga_placeholders"]:
                if entry["movie"] == centre_name:
                    b = entry["bounds"]
                    centre.paste((0, 0, 0, 255), (math.floor(b["left"]), math.floor(b["top"]), math.ceil(b["right"]), math.ceil(b["bottom"])))
            background = Image.new("RGBA", CANVAS)
            background.alpha_composite(centre, (shift, 0))
            background.alpha_composite(side)
        if layout == "dp":
            for entry in gameplay["native_bga_placeholders"]:
                if entry["movie"] == centre_name:
                    b = entry["bounds"]
                    background.paste((0, 0, 0, 255), (math.floor(b["left"]), math.floor(b["top"]), math.ceil(b["right"]), math.ceil(b["bottom"])))
        return background
    # The BPL authors use the same safe-aperture composition as28, including
    # its fixed DP symbol layer, but their source canvas is already native HD.
    scale = 1.5 if frame == "iidx28" else 1
    effective = _json(research / "effective-bga-viewports.json")
    centre = raster.render(centre_name, omitted if layout == "dp" else ()).resize(CANVAS, Image.Resampling.NEAREST)
    background = Image.new("RGBA", CANVAS)
    for entry in effective["viewports"]:
        if entry["movie"] == centre_name:
            left, top, right, bottom = entry["fullyTransparentRectangularViewportCandidate"]["bounds"]
            background.paste((0, 0, 0, 255), (math.floor(left * scale + shift), math.floor(top * scale),
                                                        math.ceil(right * scale + shift), math.ceil(bottom * scale)))
    background.alpha_composite(centre, (shift, 0))
    side = raster.render("dp_frame_1p" if layout == "dp" else side_name, () if layout == "dp" else omitted)
    background.alpha_composite(side.resize(CANVAS, Image.Resampling.NEAREST))
    return background


def _reuse_layout(reference, output, *, frame, layout, owner, source_row, root_path):
    """Reuse a completed pixel check only while all recorded inputs stay exact."""
    import brotli
    from PIL import Image

    row = copy.deepcopy(reference["frames"][frame][layout])
    _require(row["status"] == "verified" and row["baseline"]["matches"]
             and row["baseline"]["expectedPngSha256"] == owner["bakedSha256"]
             and row["sourceOwner"] == owner and row["sourceRootSha256"] == _sha(root_path.read_bytes())
             and row["sourceRawAfpSha256"] == source_row["sha256"]
             and _sha(_file(reference["library"], source_row["raw"]).read_bytes()) == source_row["sha256"],
             "A previous persistent baseline no longer has the exact source inputs")
    for texture in row["textures"]:
        _require(_sha(_file(reference["library"], texture["source"]).read_bytes()) == texture["sha256"],
                 "A reused persistent source PNG changed")
    files = []
    for item in (row["background"], row["clip"]):
        source = _file(reference["directory"], item["path"])
        data = source.read_bytes()
        _require(_sha(data) == item["sha256"], "A reused persistent asset changed")
        files.append((item["path"], data))
    with Image.open(io.BytesIO(files[0][1])) as image:
        _require(_sha(image.convert("RGBA").tobytes()) == row["background"]["rgbaSha256"],
                 "A reused persistent background lost its verified pixels")
    clip = json.loads(brotli.decompress(files[1][1]))
    _require(len(clip["frames"]) == (301 if frame in ASTERISK_FRAMES else 61)
             and clip["frames"][-1] == {"nodes": [], "camera": None}
             and clip["loop"] == (frame in ASTERISK_FRAMES)
             and clip.get("loopStartFrame") == (180 if frame in ASTERISK_FRAMES else None),
             "A reused persistent clip lost its playable-prefix/sentinel contract")
    for relative, data in files:
        target = _file(output, relative, exists=False)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    row["reusedPixelVerification"] = {"reference": str(reference["referencePath"]),
        "referenceSha256": reference["referenceSha256"], "allInputsAndOutputBytesUnchanged": True}
    return row


def export_reference(library, research, source, output, *, frame_research, bemani_root, image_tools, frames=FRAMES,
                     reuse_reference=None):
    """Export a new independently verified reference; never mutate the v18 author."""
    import brotli

    library, research, source = (_independent(path) for path in (library, research, source))
    output = _independent(output, output=True, exists=False)
    _require(not output.exists(), "Persistent reference output must be a new directory")
    inputs = (library, research.parent, source, *(_independent(frame_research[frame]) for frame in frames))
    _require(not any(output.is_relative_to(root) or root.is_relative_to(output) for root in inputs),
             "Persistent output overlaps a read-only input")
    _require(frames and len(set(frames)) == len(frames) and set(frames).issubset(FRAMES), "Unknown persistent Frames")
    old_reference, catalog = _json(research), _json(source / "gameplay/content.json")
    _require(old_reference.get("status") == "complete" and old_reference.get("originalHddAccessed") is False,
             "Persistent components require a completed independent source recording")
    _require(_independent(old_reference["library"]) == library, "Source recording belongs to another library")
    reuse = None
    if reuse_reference is not None:
        reuse_reference = _independent(reuse_reference)
        reuse = load_reference(reuse_reference)
        _require(reuse["library"] == library and reuse["sourceAuthor"] == source
                 and _independent(reuse["sourceRecording"]) == research
                 and not output.is_relative_to(reuse["directory"]) and not reuse["directory"].is_relative_to(output),
                 "A reused persistent reference must keep separate output and identical source owners")
        reuse.update(referencePath=reuse_reference, referenceSha256=_sha(reuse_reference.read_bytes()))
    tools, source_swf, _ = evaluator_tools(_independent(bemani_root), _independent(image_tools))
    packages, texture_index = library_index(library)
    output.mkdir(parents=True)
    result = {"contract": CONTRACT, "status": "exporting", "library": str(library), "sourceAuthor": str(source),
              "sourceRecording": str(research), "originalHddAccessed": False, "runtimeFrameRasters": 0, "frames": {}}
    _write(output / "reference.json", result)
    for frame in frames:
        package_name = SOURCE_PACKAGES[frame]
        metadata = _json(_file(library, packages[package_name]["authorData"]))
        package_rows = list(texture_index[package_name])
        if reuse is not None and frame in reuse["frames"]:
            result["frames"][frame] = {}
            for layout, movie in MOVIES.items():
                owner = _source_owner(source, catalog, frame=frame, layout=layout)
                source_row = next(row for row in metadata["movies"] if row["name"] == movie)
                root_path = research.parent / "source-graphs" / frame / (layout + "-root.json.br")
                result["frames"][frame][layout] = _reuse_layout(reuse, output, frame=frame, layout=layout,
                    owner=owner, source_row=source_row, root_path=root_path)
                print(f"Persistent {frame}/{layout}: verified (reused exact source)", flush=True)
            _write(output / "reference.json", result)
            continue
        dependency_names = (("0/gameparts", "0/common", "0/basic_frame", "0/graph") if frame == "iidx28" else ("1/gameparts", "1/common"))
        rows, names = list(package_rows), {row["name"] for row in package_rows}
        for dependency in dependency_names:
            for row in texture_index["contents/data/graphic/" + dependency + ".ifs"]:
                if row["name"] not in names:
                    rows.append(row)
                    names.add(row["name"])
        log = io.StringIO()
        with contextlib.redirect_stdout(log), contextlib.redirect_stderr(log):
            raster = _SourceRaster(library, metadata, rows, tools, source_swf)
        result["frames"][frame] = {}
        for layout in MOVIES:
            key = f"{frame}/{layout}"
            owner = _source_owner(source, catalog, frame=frame, layout=layout)
            root_path = research.parent / "source-graphs" / frame / (layout + "-root.json.br")
            geometry_path = root_path.with_name(layout + "-geometry.json.br")
            root = json.loads(brotli.decompress(root_path.read_bytes()))
            package = json.loads(brotli.decompress(geometry_path.read_bytes()))
            with contextlib.redirect_stdout(log), contextlib.redirect_stderr(log):
                movie = raster.movie(MOVIES[layout])
                _, source_returns = _root_return_tags(movie)
            source_row = next(row for row in metadata["movies"] if row["name"] == movie.name)
            old_receipt = next(row for row in old_reference["frames"][frame]["sourceReceipts"] if row["layout"] == layout)
            _require(source_row["sha256"] == old_receipt["rawAfpSha256"] and root["sourceMovie"] == MOVIES[layout]
                     and root["package"] == package_name, "The recorded hierarchy no longer matches its source AFP")
            selected, selection = select_components(root, package, package_rows, frame=frame, layout=layout)
            with contextlib.redirect_stdout(log), contextlib.redirect_stderr(log):
                baseline = _compose(raster, _independent(frame_research[frame]), frame=frame, layout=layout)
            check = verify_background(baseline, _file(source, owner["baked"]["path"]), output / "evidence" / key)
            row = {"status": "baseline-mismatch", "sourceOwner": owner, "baseline": check, "selection": selection,
                   "sourceReturns": source_returns,
                   "sourceRawAfpSha256": source_row["sha256"], "sourceRootSha256": _sha(root_path.read_bytes()),
                   "clock": "presentation.elapsed" if frame in ASTERISK_FRAMES else "preparation.elapsed"}
            result["frames"][frame][layout] = row
            if check["matches"]:
                with contextlib.redirect_stdout(log), contextlib.redirect_stderr(log):
                    split = _compose(raster, _independent(frame_research[frame]), frame=frame, layout=layout,
                                     omitted=selection["omittedLayerPaths"])
                split_path = f"backgrounds/{key}.png"
                (output / split_path).parent.mkdir(parents=True, exist_ok=True)
                split.save(output / split_path)
                resource_ids = {index: "lp.t" + texture["sha256"][:24] for index, texture in enumerate(package_rows)}
                clip = component_clip(selected, package, resource_ids, frame=frame, source_loop=movie.labels["loop"])
                encoded = brotli.compress(json.dumps(clip, separators=(",", ":"), allow_nan=False).encode(), quality=7)
                clip_path = f"clips/{key}.json.br"
                (output / clip_path).parent.mkdir(parents=True, exist_ok=True)
                (output / clip_path).write_bytes(encoded)
                used = {geometry["resource"] for geometry in clip["geometries"] if geometry["resource"] is not None}
                textures = [{"id": resource_ids[index], "source": texture["sprite"], "sha256": texture["sha256"],
                             "bytes": _file(library, texture["sprite"]).stat().st_size,
                             "filter": texture["sampler"]["mag"], "width": texture["width"], "height": texture["height"]}
                            for index, texture in enumerate(package_rows) if resource_ids[index] in used]
                _require(len({texture["id"] for texture in textures}) == len(used), "Persistent textures are unresolved or duplicated")
                row.update({"status": "verified", "background": {"path": split_path, "sha256": _sha((output / split_path).read_bytes()),
                    "rgbaSha256": _sha(split.tobytes()), "width": split.width, "height": split.height},
                    "clip": {"path": clip_path, "sha256": _sha(encoded), "resources": sorted(used), "frames": len(clip["frames"]),
                             "sourceFrames": root["frameCount"], "playableFrames": len(clip["frames"]) - 1,
                             "addedTerminalFrame": True, "terminalFrameIndex": len(clip["frames"]) - 1,
                             "heldSettledSourceFrame": None if frame in ASTERISK_FRAMES else 30,
                             "loopStartFrame": clip.get("loopStartFrame"), "bytes": len(encoded), "decodedBytes": len(brotli.decompress(encoded))},
                    "textures": textures, "sourceFps": root["fps"], "sourceDurationMs": root["durationMs"],
                    "handoverMs": None if frame in ASTERISK_FRAMES else HANDOVER_MS,
                    "sourceMasksPreserved": True, "sourceBlendAndUvPreserved": True,
                    "timingScope": "Original star timeline; existing outer Frame assembly duration is retained" if frame in ASTERISK_FRAMES else
                        "Original persistent component0..30 at60Hz, held settled through the existing1000ms handover"})
            _write(output / "reference.json", result)
            print(f"Persistent {key}: {row['status']}", flush=True)
        (output / f"{frame}-source-render.log").write_text(log.getvalue(), encoding="utf-8")
    result["status"] = "complete" if all(row["status"] == "verified" for layouts in result["frames"].values() for row in layouts.values()) else "partial-failure"
    _write(output / "reference.json", result)
    return result


def load_reference(path):
    path = _independent(path)
    reference = _json(path)
    _require(reference.get("contract") == CONTRACT and reference.get("status") in ("complete", "partial-failure")
             and reference.get("originalHddAccessed") is False, "Expected an independently exported persistent reference")
    reference["directory"] = path.parent
    reference["library"] = _independent(reference["library"])
    reference["sourceAuthor"] = _independent(reference["sourceAuthor"])
    return reference


def prepare_assets(reference, output, *, frame, layout, manifest):
    """Copy one contribution, aliasing already selected identical source PNGs."""
    import brotli

    output = _independent(output, output=True)
    row = reference["frames"][frame][layout]
    _require(row["status"] == "verified", "A mismatched source background cannot be prepared")
    for root in (reference["directory"], reference["library"], reference["sourceAuthor"]):
        _require(not output.is_relative_to(root) and not root.is_relative_to(output), "Persistent author output overlaps its reference")
    baked = _file(output, row["sourceOwner"]["baked"]["path"])
    _require(_sha(baked.read_bytes()) == row["sourceOwner"]["bakedSha256"],
             "The target author no longer has the verified original baked background")
    additions, aliases, copied = [], {}, []

    def copy_file(source, relative, expected):
        data = source.read_bytes()
        _require(_sha(data) == expected, "A verified persistent source changed")
        target = _file(output, relative, exists=False)
        if target.exists():
            _require(target.read_bytes() == data, "A persistent author asset conflicts with existing data")
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
        copied.append(relative)

    for texture in row["textures"]:
        source = _file(reference["library"], texture["source"])
        _require(_sha(source.read_bytes()) == texture["sha256"], "A verified original persistent PNG changed")
        existing = []
        for item in manifest["resources"]:
            if item["type"] != "texture" or item.get("filter", "linear") != texture["filter"]:
                continue
            path = _file(output, item["path"])
            if path.stat().st_size == texture["bytes"] and _sha(path.read_bytes()) == texture["sha256"]:
                existing.append(item)
        if existing:
            aliases[texture["id"]] = existing[0]["id"]
        else:
            relative = "art/lp/t" + texture["sha256"][:24] + ".png"
            copy_file(source, relative, texture["sha256"])
            aliases[texture["id"]] = texture["id"]
            additions.append({"id": texture["id"], "type": "texture", "path": relative, "filter": texture["filter"]})
    background = row["background"]
    background_path = "art/lp/b" + background["sha256"][:24] + ".png"
    copy_file(_file(reference["directory"], background["path"]), background_path, background["sha256"])
    background_resource = {"id": "lp.b" + background["sha256"][:24], "type": "texture", "path": background_path, "filter": "nearest"}
    additions.append(background_resource)
    data = _file(reference["directory"], row["clip"]["path"]).read_bytes()
    _require(_sha(data) == row["clip"]["sha256"], "A verified persistent clip changed")
    clip = json.loads(brotli.decompress(data))
    for geometry in clip["geometries"]:
        if geometry["resource"] is not None:
            geometry["resource"] = aliases[geometry["resource"]]
    encoded = brotli.compress(json.dumps(clip, separators=(",", ":"), allow_nan=False).encode(), quality=7)
    clip_path = "art/lp/c" + _sha(encoded)[:24] + ".json.br"
    target = _file(output, clip_path, exists=False)
    if target.exists():
        _require(target.read_bytes() == encoded, "A persistent clip content identity conflicts")
    else:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(encoded)
    animation = {"id": f"lp.a.{frame}.{layout}", "path": clip_path, "resources": sorted(set(aliases.values()))}
    return {"frame": frame, "layout": layout, "resources": additions, "animation": animation,
            "background": background_resource["id"], "sourceTextureIds": sorted(set(aliases.values())),
            "sourceBakedSha256": row["sourceOwner"]["bakedSha256"], "copiedFiles": copied + [clip_path],
            "clock": row["clock"], "handoverMs": row["handoverMs"]}


def _assembly(scene, root):
    nodes = {node["id"]: node for node in _walk(root)}
    enabled_ids = [binding["target"] for binding in scene["bindings"]
                   if binding["source"] == "option.entrance-animation" and binding["target"] in nodes
                   and binding["property"] == "visible"]
    _require(len(enabled_ids) == 1, "The Frame must have one existing entrance option owner")
    enabled = nodes[enabled_ids[0]]
    descendants = {node["id"] for node in _walk(enabled)}
    available_ids = [binding["target"] for binding in scene["bindings"]
                     if binding["source"] == "preparation.available" and binding["target"] in descendants
                     and binding["property"] == "visible"]
    _require(len(available_ids) == 1, "The entrance must retain one preparation availability owner")
    active = nodes[available_ids[0]]
    active_ids = {node["id"] for node in _walk(active)}
    overlay_ids = [track["target"] for track in scene["tracks"] if track["target"] in active_ids
                   and track["property"] == "opacity" and track.get("clock") == "preparation.elapsed"
                   and [(key["time"], key["value"]) for key in track["keyframes"]] == [(0, 1), (HANDOVER_MS, 0)]]
    _require(len(overlay_ids) == 1, "The existing entrance lacks its exact1000ms overlay handover")
    return active, nodes[overlay_ids[0]]


def inject(scene, manifest, *, frame, layout, assets):
    """Replace only the selected catalog background's proven component owners."""
    _require(frame in FRAMES and layout in MOVIES and assets["frame"] == frame and assets["layout"] == layout,
             "The persistent assets belong to another Frame/layout")
    root, baked = background_owner(scene, manifest, frame=frame, layout=layout)
    active, overlay = _assembly(scene, root)
    identifier = f"lp.{frame}.{layout}"
    _require(not any(node["id"].startswith("lp.") for node in _walk(root)), "Persistent components have already been injected")
    removed, saved_bytes = set(), 0
    if frame in ASTERISK_FRAMES:
        stars = [node for node in _walk(overlay) if node.get("resource") in assets["sourceTextureIds"]]
        # The audited v18 numbered33 owner already had inline source stars.
        # Its seven BPL peers used only baked reveal slices, with no raw star
        # resource or inline star to remove. Require each actual owner shape.
        expected_stars = (2 if layout == "dp" else 1) if frame == "iidx33" else 0
        _require(len(stars) == expected_stars and all(node["type"] == "projective-sprite" for node in stars),
                 "The existing entrance does not contain its exact source star owners")
        removed = {node["id"] for star in stars for node in _walk(star)}
        removed_tracks = [track for track in scene["tracks"] if track["target"] in removed]
        _require(len(removed_tracks) == len(stars) and all(track["property"] == "projective-quad" for track in removed_tracks),
                 "A source star has controls beyond its audited quad channel")
        saved_bytes = sum(len(json.dumps(track, separators=(",", ":")).encode()) for track in removed_tracks)
        for node in _walk(overlay):
            node["children"] = [child for child in node.get("children", []) if child["id"] not in removed]
        for field in ("tracks", "bindings", "variants"):
            scene[field] = [program for program in scene[field] if program["target"] not in removed]
        replaced = active
    else:
        replaced = overlay
    replacements = 0
    for node in _walk(replaced):
        if node.get("resource") == baked["id"]:
            node["resource"] = assets["background"]
            replacements += 1
    _require(replacements > 0, "The entrance has no expected baked background references")
    animation = {"id": identifier, "type": "recorded-animation", "animation": assets["animation"]["id"],
                 "clock": assets["clock"], "target": {"kind": "global"}, "blend": "alpha",
                 # RuntimeHost maps z directly to Drawable.Depth; lower depth draws
                 # in front. Keep original components above the starless/laneless
                 # background siblings at depth0, including after the handover.
                 "properties": {"z": -1}, "effects": [], "children": []}
    (active if frame in ASTERISK_FRAMES else overlay)["children"].append(animation)
    for resource in assets["resources"]:
        previous = [item for item in manifest["resources"] if item["id"] == resource["id"]]
        _require(not previous or previous == [resource], "A persistent resource identity conflicts")
        if not previous:
            manifest["resources"].append(copy.deepcopy(resource))
    _require(not any(item["id"] == assets["animation"]["id"] for item in manifest.get("animations", [])),
             "A persistent animation identity conflicts")
    manifest.setdefault("animations", []).append(copy.deepcopy(assets["animation"]))
    return {"frame": frame, "layout": layout, "clock": assets["clock"], "bakedReferencesReplaced": replacements,
            "removedIntroNodes": len(removed), "removedInlineTrackBytes": saved_bytes,
            "staticDisabledAndUnavailableBakedPreserved": True, "nativeBaseplateDeclarationsChanged": False,
            "frameRasters": 0, "sourceMasksBlendUvPreserved": True}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("library", "research", "source", "output", "bemani-root", "image-tools"):
        parser.add_argument("--" + name, type=Path, required=True)
    parser.add_argument("--frame-research", action="append", required=True, metavar="FRAME=DIRECTORY")
    parser.add_argument("--frame", action="append", choices=FRAMES)
    parser.add_argument("--reuse-reference", type=Path,
                        help="Completed pixel checks may be copied only after all source and output digests still match")
    args = parser.parse_args()
    research = {}
    for value in args.frame_research:
        frame, separator, path = value.partition("=")
        if not separator or frame not in FRAMES or frame in research:
            parser.error("Use one explicit --frame-research FRAME=DIRECTORY for each selected Frame")
        research[frame] = Path(path)
    frames = tuple(args.frame or FRAMES)
    if set(frames) != set(research):
        parser.error("Each selected Frame requires its exact static research directory")
    result = export_reference(args.library, args.research, args.source, args.output, frame_research=research,
                              bemani_root=args.bemani_root, image_tools=args.image_tools, frames=frames,
                              reuse_reference=args.reuse_reference)
    raise SystemExit(0 if result["status"] == "complete" else 1)


if __name__ == "__main__":
    main()
