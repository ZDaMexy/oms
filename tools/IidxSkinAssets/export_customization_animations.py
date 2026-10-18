"""Export verified source-library movies as bounded recorded-animation assets.

No source HDD, IFS extraction, runtime AFP interpreter, or generated pixels are
used here. Source PNGs are referenced by copy instructions for the pack builder.
"""

import argparse
import contextlib
from functools import lru_cache
import hashlib
import io
import json
import math
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

sys.dont_write_bytecode = True

from build_fullcombo_catalog import (InternTable, LocalStateRecorder, content_key, exporter_classes,
                                     load_tools, point_values, read_json, rgba_values, to_recorded_animation,
                                     write_json)

STABLE_ID = re.compile(r"[a-z][a-z0-9_-]*(?:\.[a-z][a-z0-9_-]*)*\Z")
TRIGGERS = {"gameplay.attach", "gameplay.complete", "judgement.hit", "input.key.down", "object.spawn"}


def file_sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def inside(root, relative):
    path = (root / relative).resolve(strict=True)
    if not path.is_relative_to(root):
        raise ValueError(f"Source path escapes verified library: {relative}")
    return path


@lru_cache(maxsize=2)
def library_index(library):
    data = read_json(library / "library.json")
    if data["status"] != "complete" or data["sourceMetadataUnchanged"] is not True:
        raise ValueError("Source library is not complete and verified")
    packages = {row["source"]: row for row in data["packages"]}
    textures = {}
    for texture in data["textures"]:
        textures.setdefault(texture["package"], []).append(texture)
    return packages, textures


@lru_cache(maxsize=1)
def evaluator_tools(bemani_root, image_tools):
    tools = load_tools(bemani_root, image_tools)
    source_swf, renderer = exporter_classes(tools)
    return tools, source_swf, renderer


def root_loop_control(movie):
    """Recognise only the actual literal aeplib.gotoAndPlay(this, label) pattern.

    Its source action is translated to closed heldLoop/releaseFrame control.
    All other bytecode remains in the offline evaluator and is checked for
    unsupported operations. The original actions are retained in the receipt.
    """
    controls, tags = [], set()
    for frame_index, frame in enumerate(movie.frames):
        for index in range(frame.start_tag_offset, frame.start_tag_offset + frame.num_tags):
            tag = movie.tags[index]
            if type(tag).__name__ != "AP2DoActionTag":
                continue
            actions = tag.bytecode.as_dict()["actions"]
            useful = [item for item in actions if item["action"] != "END"]
            if [item["action"] for item in useful] != ["PUSH", "GET_VARIABLE", "PUSH", "CALL_METHOD"]:
                continue
            first, _, method, _ = useful
            if len(first["objects"]) != 4 or first["objects"][1:3] != ["THIS", "2"]:
                continue
            if not first["objects"][3].endswith(": aeplib)") or len(method["objects"]) != 1:
                continue
            if not method["objects"][0].endswith(": gotoAndPlay)"):
                continue
            label_repr = first["objects"][0]
            matches = [label for label in movie.labels if repr(label) == label_repr]
            if len(matches) != 1:
                raise ValueError("A root self-loop names an unrecognised source label")
            target = matches[0]
            if not target.endswith("_loop"):
                raise ValueError(f"Actual root gotoAndPlay target is not a supported hold loop: {target}")
            stem = target[:-5]
            required = [stem + "_in", target, stem + "_out"]
            if any(label not in movie.labels for label in required):
                raise ValueError("Source hold loop lacks explicit intro/loop/release labels")
            start, loop, release = [movie.labels[label] for label in required]
            if start != 0 or not 0 <= loop < release < len(movie.frames) or frame_index != release - 1:
                raise ValueError("Actual hold-loop branch and labels do not form the bounded supported timeline")
            controls.append({"tagIndex": index, "sourceFrame": frame_index, "labels": {label: movie.labels[label] for label in required},
                             "heldLoop": [loop, release], "releaseFrame": release,
                             "sourceBytecode": tag.bytecode.as_dict()})
            tags.add(id(tag))
    if len(controls) > 1:
        raise ValueError("Actual root movie contains multiple incompatible hold-loop branches")
    return controls, tags


def source_geometry(tools, library, metadata, textures):
    atlas_sizes = {}
    packages, _ = library_index(library)
    for package in {row["package"] for row in textures}:
        author = read_json(inside(library, packages[package]["authorData"]))
        raw_root = inside(library, author["rawRoot"])
        xml = ET.parse(raw_root / "tex/texturelist.xml").getroot()
        for atlas in xml.findall("texture"):
            size = [int(value) for value in atlas.findtext("size").split()]
            for image in atlas.findall("image"):
                atlas_sizes[package, image.attrib["name"]] = size
    texture_map = {row["name"].removesuffix(".png"): index for index, row in enumerate(textures)}
    shapes, geometries, mapping = {}, [], {}
    for source in metadata["geometry"]:
        if not source["parsed"]:
            raise ValueError(f"Source geometry was not parsed: {source['name']}")
        path = inside(library, source["raw"])
        if file_sha(path) != source["sha256"]:
            raise ValueError(f"Source geometry changed: {path}")
        shape = tools.Shape(source["name"], path.read_bytes())
        shape.parse(text_obfuscated=False)
        shapes[source["name"]] = shape
        draws = []
        for draw in shape.draw_params:
            uv, texture_id = [], None
            unresolved = None
            if draw.region is not None:
                if draw.region not in texture_map:
                    # A package can contain many unrelated movies. Only an
                    # actually drawn geometry requires this explicit dependency.
                    unresolved = draw.region
                else:
                    texture_id = texture_map[draw.region]
                    texture = textures[texture_id]
                    if draw.flags & 0x40:
                        # GEO's documented normalised-texture flag: source UVs
                        # already address the complete replacement sprite.
                        uv = [[point.x, point.y] for point in shape.tex_points]
                    else:
                        width, height = atlas_sizes[texture["package"], draw.region]
                        rect = texture["uvRect"]
                        uv = [[(point.x * width - rect[0]) / texture["width"],
                               (point.y * height - rect[2]) / texture["height"]] for point in shape.tex_points]
            value = {"texture": texture_id, "uv": uv, "indices": list(draw.vertexes), "flags": draw.flags,
                     "sourceColour": rgba_values(draw.blend, (1, 1, 1, 1)) if draw.blend is not None else None}
            if unresolved is not None:
                value["unresolvedTexture"] = unresolved
            draws.append(value)
        mapping[source["name"]] = len(geometries)
        geometries.append({"source": source["name"], "vertices": [point_values(point) for point in shape.vertex_points],
                           "atlasUV": [point_values(point) for point in shape.tex_points],
                           "colours": [rgba_values(colour, (1, 1, 1, 1)) for colour in shape.tex_colors], "draws": draws})
    return shapes, geometries, mapping


def node_for_reference(prefix, animation_id, stage, reference):
    left, top, right, bottom = reference
    if not all(type(value) in (int, float) and math.isfinite(value) for value in reference) or right <= left or bottom <= top:
        raise ValueError("Reference bounds must be a finite positive source-space rectangle")
    properties = {"x": (stage[0] - left) / (right - left), "y": (stage[1] - top) / (bottom - top),
                  "width": (stage[2] - stage[0]) / (right - left), "height": (stage[3] - stage[1]) / (bottom - top)}
    for dimension, scale in (("width", "scale-x"), ("height", "scale-y")):
        if properties[dimension] > 4:
            properties[scale] = properties[dimension] / 4
            properties[dimension] = 4
            if properties[scale] > 8:
                raise ValueError("Reference bounds exceed the existing scene scale budget")
    return {"id": prefix + ".node", "type": "recorded-animation", "target": {"kind": "global"},
            "animation": animation_id, "blend": "alpha", "properties": properties, "effects": [], "children": []}


def export_clip(library, package, movie, output, *, bemani_root, image_tools, prefix, loop, trigger, reference_bounds=None,
                texture_dependencies=(), sample_frame=None):
    """Return node/resources/animations/files/receipt for one genuine source movie.

    reference_bounds is [left, top, right, bottom] in unmodified source coordinates.
    It controls only the returned scene node's placement inside its caller-owned
    native surface. Clip positions remain in source pixels, rebased solely for a
    nonzero source viewport origin. PNG files are never resampled or recoloured.
    """
    library = Path(library).resolve(strict=True)
    output = Path(output).resolve()
    if output.drive.casefold() in ("c:", "d:"):
        raise ValueError("Generated animation data must use the authorised media drive")
    if not STABLE_ID.fullmatch(prefix):
        raise ValueError("Animation prefix must use stable identifier components starting with letters")
    trigger = trigger or "gameplay.attach"
    if trigger not in TRIGGERS or type(loop) is not bool:
        raise ValueError("Animation loop/trigger is outside the closed playback contract")
    packages, texture_index = library_index(library)
    candidates = [value for key, value in packages.items() if key == package or Path(key).stem == package]
    if len(candidates) != 1:
        raise ValueError(f"Source package is not unique: {package}")
    source = candidates[0]
    metadata = read_json(inside(library, source["authorData"]))
    rows = list(texture_index.get(source["source"], []))
    dependencies = []
    names = {row["name"]: row for row in rows}
    for name in texture_dependencies:
        matches = [value for key, value in packages.items() if key == name or Path(key).stem == name]
        if len(matches) != 1 or matches[0]["source"] == source["source"]:
            raise ValueError(f"Texture dependency is not a distinct unique package: {name}")
        dependency = matches[0]
        dependencies.append({"package": dependency["source"], "sourceArchiveSHA256": dependency["sha256"]})
        for row in texture_index.get(dependency["source"], []):
            if row["name"] in names:
                existing = names[row["name"]]
                physical = lambda value: tuple(value["sampler"][key] for key in ("min", "mag", "wrapS", "wrapT"))
                if row["sha256"] != existing["sha256"] or physical(row) != physical(existing):
                    raise ValueError(f"Explicit texture dependencies conflict by source name: {row['name']}")
            else:
                names[row["name"]] = row
                rows.append(row)
    destination = output / prefix
    if destination.exists():
        raise FileExistsError(f"Animation export must be new: {destination}")
    output.mkdir(parents=True, exist_ok=True)
    tools, source_swf, renderer_class = evaluator_tools(Path(bemani_root).resolve(strict=True), Path(image_tools).resolve(strict=True))
    captured = io.StringIO()
    with contextlib.redirect_stdout(captured), contextlib.redirect_stderr(captured):
        shapes, geometries, geometry_map = source_geometry(tools, library, metadata, rows)
        movies, filter_table, raw_hashes = {}, InternTable(), {}
        for item in metadata["movies"]:
            if not item["parsed"]:
                raise ValueError(f"Source movie was not parsed: {item['name']}")
            raw = inside(library, item["raw"])
            if file_sha(raw) != item["sha256"]:
                raise ValueError(f"Source movie changed: {raw}")
            bsi_path = library / item["bsi"]
            parsed = source_swf(item["name"], raw.read_bytes(), bsi_path.read_bytes() if bsi_path.is_file() else b"")
            parsed.parse()
            movies[item["name"]] = parsed
            raw_hashes[item["raw"]] = item["sha256"]
            for block in parsed.export_filter_blocks:
                filter_table.add(block)
        if movie not in movies:
            raise ValueError(f"Source movie is absent: {package}/{movie}")
        selected = movies[movie]
        if sample_frame is not None and (type(sample_frame) is not int or not 0 <= sample_frame < len(selected.frames)):
            raise ValueError("Explicit preview frame is outside the original movie")
        controls, flow_tags = root_loop_control(selected)
        if controls and (trigger != "input.key.down" or loop):
            raise ValueError("A genuine held-loop source needs non-whole-loop input.key.down playback")

        class TimelineRenderer(renderer_class):
            def _AFPRenderer__place(self, tag, clip, prefix=""):
                if id(tag) in flow_tags and clip is self._AFPRenderer__root:
                    # This exact source branch is represented by heldLoop and
                    # releaseFrame; all original sequential frames are retained.
                    return None, False
                return super()._AFPRenderer__place(tag, clip, prefix)

        recorder = LocalStateRecorder(tools, TimelineRenderer)
        root = recorder.record(package, movie, selected, movies, shapes, geometry_map, filter_table,
                               frame_count=sample_frame + 1 if sample_frame is not None else None)
    warnings = sorted(set(captured.getvalue().splitlines()))
    unsupported = [line for line in warnings if line.startswith("WARNING:")
                   and "includes unparsed options and might not display properly!" not in line
                   and line != "WARNING: Root clip requested to play not in top-left corner!"]
    if unsupported:
        raise ValueError("Offline source evaluation has unsupported operations: " + "; ".join(unsupported))
    source_package = {"geometries": geometries, "filters": filter_table.values}
    resources = {index: prefix + ".t.t" + row["sha256"][:24] for index, row in enumerate(rows)}
    if len(set(resources.values())) != len(resources):
        # Same bytes may have source aliases; use the same manifest resource.
        if len({(row["sha256"], content_key({key: row["sampler"][key] for key in ("min", "mag", "wrapS", "wrapT")}))
                for row in rows}) != len(set(resources.values())):
            raise ValueError("A source PNG alias uses different sampler state")
    selected_root = root if sample_frame is None else {**root, "frames": [root["frames"][sample_frame], {"nodes": [], "camera": None}]}
    clip = to_recorded_animation(source_package, selected_root, resources)
    clip["loop"], clip["trigger"] = loop, trigger
    if controls:
        clip["heldLoop"] = controls[0]["heldLoop"]
        clip["releaseFrame"] = controls[0]["releaseFrame"]
    location = selected.location
    stage = [location.left, location.top, location.right, location.bottom]
    if location.left or location.top:
        for frame in clip["frames"]:
            if frame["camera"]:
                frame["camera"][0] -= location.left
                frame["camera"][1] -= location.top
        # A new container preserves shared/deduplicated original states: editing
        # a root state in place could also change children using that same id.
        rebase_state = len(clip["states"])
        clip["states"].append({"matrix": [1, 0, 0, 0, 1, 0, 0, 0, 1, -location.left, -location.top, 0],
                               "origin": [0, 0, 0], "projection": "affine", "multiply": [1, 1, 1, 1],
                               "add": [0, 0, 0, 0], "hsl": [0, 0, 0], "mask": None, "colourMatrix": None})
        clip["nodes"] = [{"parent": -1, "geometry": -1, "blend": "normal"}] + [
            {**node, "parent": node["parent"] + 1} for node in clip["nodes"]]
        for frame in clip["frames"]:
            if frame["nodes"]:
                frame["nodes"] = [[0, rebase_state]] + [[node_id + 1, state_id] for node_id, state_id in frame["nodes"]]
    used = {geometry["resource"] for geometry in clip["geometries"] if geometry["resource"] is not None}
    manifest_resources, files, seen = [], [], set()
    for index, row in enumerate(rows):
        identifier = resources[index]
        if identifier not in used or identifier in seen:
            continue
        seen.add(identifier)
        sampler = row["sampler"]
        if sampler["min"] != sampler["mag"] or sampler["min"] not in ("nearest", "linear") or sampler["wrapS"] != "clamp" or sampler["wrapT"] != "clamp":
            raise ValueError(f"Actual source sampler requires another explicit capability: {sampler}")
        png = inside(library, row["sprite"])
        if file_sha(png) != row["sha256"]:
            raise ValueError(f"Original source PNG changed: {png}")
        relative = f"custom/{prefix}/textures/{row['sha256']}.png"
        manifest_resources.append({"id": identifier, "type": "texture", "path": relative, "filter": sampler["min"]})
        files.append({"source": str(png), "path": relative, "sha256": row["sha256"]})
    import brotli
    encoded = json.dumps(clip, ensure_ascii=False, separators=(",", ":"), allow_nan=False).encode("utf-8")
    compressed = brotli.compress(encoded, quality=5)
    if len(compressed) > 16 * 1024 * 1024 or len(encoded) > 128 * 1024 * 1024:
        raise ValueError("Recorded source exceeds the bounded clip byte budget")
    destination.mkdir()
    clip_path = destination / "clip.json.br"
    clip_path.write_bytes(compressed)
    write_json(destination / "source-root.json", root)
    write_json(destination / "source-geometry.json", source_package)
    animation_id = prefix + ".clip"
    animation_path = f"custom/{prefix}/clip.json.br"
    files.append({"source": str(clip_path), "path": animation_path, "sha256": file_sha(clip_path)})
    reference = list(reference_bounds) if reference_bounds is not None else stage
    result = {"node": node_for_reference(prefix, animation_id, stage, reference), "resources": manifest_resources,
              "animations": [{"id": animation_id, "path": animation_path, "resources": sorted(used)}], "tracks": [], "files": files,
              "source": {"library": str(library), "package": source["source"], "movie": movie, "stage": stage, "referenceBounds": reference,
                         "fps": selected.fps, "sourceFrames": len(selected.frames), "durationMs": (len(clip["frames"]) - 1) * 1000 / selected.fps,
                         "sourceLoopLabels": selected.labels, "translatedRootControl": controls, "textureDependencies": dependencies,
                         "sampleFrame": sample_frame, "evaluatedPrefixFrames": root["frameCount"]},
              "receipt": {"sourceArchiveSHA256": source["sha256"], "rawMovieSHA256": raw_hashes,
                          "pngBytesPreserved": True, "pngsCopiedByExporter": 0, "runtimeSourceInterpreter": False,
                          "runtimeBytes": len(compressed), "decodedJSONBytes": len(encoded), "clipSHA256": file_sha(clip_path),
                          "nodes": len(clip["nodes"]), "states": len(clip["states"]), "framesIncludingEmptyEnd": len(clip["frames"]),
                          "hierarchyMathChecks": recorder.math_checks, "maximumHierarchyMathError": recorder.math_max_error,
                          "warnings": warnings, "resolvedBlend": True, "sourceStageOriginRebased": stage[:2] != [0, 0],
                          "pixelVerification": "Source-byte/graph export only; actual player and original-game colour parity remain separate gates."}}
    write_json(destination / "export.json", result)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--library", type=Path, required=True)
    parser.add_argument("--package", required=True)
    parser.add_argument("--movie", required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--bemani-root", type=Path, required=True)
    parser.add_argument("--image-tools", type=Path, required=True)
    parser.add_argument("--prefix", required=True)
    parser.add_argument("--loop", action="store_true")
    parser.add_argument("--trigger", default="gameplay.attach")
    parser.add_argument("--reference-bounds", type=float, nargs=4)
    parser.add_argument("--texture-dependency", action="append", default=[])
    parser.add_argument("--sample-frame", type=int)
    args = parser.parse_args()
    result = export_clip(args.library, args.package, args.movie, args.output, bemani_root=args.bemani_root,
                         image_tools=args.image_tools, prefix=args.prefix, loop=args.loop,
                         trigger=args.trigger, reference_bounds=args.reference_bounds,
                         texture_dependencies=args.texture_dependency, sample_frame=args.sample_frame)
    print(json.dumps({"source": result["source"], "receipt": result["receipt"]}, ensure_ascii=True))


if __name__ == "__main__":
    main()
