"""Original per-Frame lane openings for the independent Omni author package.

The offline recorder keeps the original local hierarchy, colours, masks, UVs
and blends. Only meshes which have gone away by the settled source frame are
retained; ordinary frame/score/turntable artwork stays with its existing owner.
Each lane-group viewport then clips that source tree without rasterising it.
Runtime consumes a bounded recorded clip on the read-only preparation clock.
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
import struct
import sys

sys.dont_write_bytecode = True

from build_fullcombo_catalog import (InternTable, LocalStateRecorder, read_json,
                                     to_recorded_animation)
from export_customization_animations import (evaluator_tools, file_sha, inside,
                                              library_index, source_geometry)
from lighting_motion import FRAMES, frame_namespace

CONTRACT = "oms-iidx-lighting-lane-opening-reference.v1"
CLOCK = "preparation.elapsed"
OPTION = {"id": "lane-opening", "type": "toggle", "label": "Gameplay · 轨道开场演出", "default": True}
SOURCE_PACKAGES = {**{f"iidx{i}": f"contents/data/graphic/gmframe{i}.ifs" for i in range(27, 34)},
                   **{f"bpl-{team}": f"contents/data/graphic/1/gmframe_bpls5_{suffix}.ifs"
                      for team, suffix in (("apina", "ap"), ("gamepanic", "gp"), ("gigo", "gi"),
                                           ("leisureland", "ll"), ("round1", "r1"), ("silkhat", "sh"), ("tradz", "tr"))},
                   "bpl-supernova-hd": "contents/data/graphic/0/gmframe_bpl_super_nova.ifs"}
SOURCE_MOVIES = {"p1": "1p_frame", "p2": "2p_frame", "dp": "center_frame_dp_1p"}
TEXTURE_DEPENDENCIES = ("contents/data/graphic/1/gameparts.ifs", "contents/data/graphic/1/common.ifs")


def _require(condition, message):
    if not condition:
        raise ValueError(message)


def _independent(path, *, output=False, exists=True):
    path = Path(path)
    _require(path.drive.casefold() not in (("c:", "d:") if output else ("d:",)),
             "Lane opening authoring requires an independent non-HDD path")
    path = path.resolve(strict=exists)
    _require(path.drive.casefold() not in (("c:", "d:") if output else ("d:",)),
             "Lane opening authoring requires an independent non-HDD path")
    return path


def _output_file(root, relative):
    _require(isinstance(relative, str) and relative and ":" not in relative and "\\" not in relative,
             "Opening assets require a contained relative author path")
    path = Path(relative)
    _require(not path.is_absolute() and ".." not in path.parts, "An opening asset escapes its author output")
    target = (root / path).resolve()
    _require(target.is_relative_to(root), "An opening asset escapes its author output")
    return target


def source_viewports(frame, layout):
    """Use the already-published Omni stage projection, including its empty slots.

    Source motion is not stretched to the current number of active keys. SD
    frames use a 1.5x source projection; their 292x484 source lane is the exact
    existing 438x726 native surface. All HD surfaces remain 440x728.
    """
    _require(frame in FRAMES and layout in SOURCE_MOVIES, "Unknown Omni Frame or layout")
    sd = frame in ("iidx27", "iidx28", "iidx29")
    width, height = (292, 484) if sd else (440, 728)
    left = ((304, 684) if sd else (455, 1025)) if layout == "dp" else (
        ((940,) if sd else (1409,)) if layout == "p2" else ((48,) if sd else (71,)))
    return [[x, 0, x + width, height] for x in left]


def _root_return_tags(movie):
    """Translate only the original final deepGotoAndPlay(this, 'loop') return.

    It lives after the one-shot introduction. Suppressing this exact final root
    branch allows inspection of the settled frame without evaluating a repeat.
    Other bytecode is left to the audited recorder and unsupported actions fail.
    """
    tags, receipts = set(), []
    _require(set(movie.labels) == {"loop"} and type(movie.labels["loop"]) is int
             and 0 < movie.labels["loop"] < len(movie.frames), "The original Frame lacks its bounded loop label")
    for frame_index, frame in enumerate(movie.frames):
        for index in range(frame.start_tag_offset, frame.start_tag_offset + frame.num_tags):
            tag = movie.tags[index]
            if type(tag).__name__ != "AP2DoActionTag":
                continue
            actions = tag.bytecode.as_dict()["actions"]
            useful = [item for item in actions if item["action"] != "END"]
            recognised = ([item["action"] for item in useful] == ["PUSH", "GET_VARIABLE", "PUSH", "CALL_METHOD"]
                          and useful[0].get("objects", [])[:3] == ["'loop'", "THIS", "2"]
                          and len(useful[0].get("objects", [])) == 4
                          and useful[0]["objects"][3].endswith(": aeplib)")
                          and len(useful[2].get("objects", [])) == 1
                          and useful[2]["objects"][0].endswith(": deepGotoAndPlay)"))
            _require(recognised and frame_index == len(movie.frames) - 1,
                     "The Frame root contains an unrecognised timeline action")
            tags.add(id(tag))
            receipts.append({"tagIndex": index, "sourceFrame": frame_index,
                             "targetLabel": "loop", "targetFrame": movie.labels["loop"],
                             "sourceBytecode": tag.bytecode.as_dict()})
    _require(len(tags) == 1, "The original Frame must have one explicit terminal return")
    return tags, receipts


def _effective_visibility(root, frame):
    result = {}
    states = root["states"]
    for identifier, state_id in frame["nodes"]:
        node, state = root["nodes"][identifier], states[state_id]
        parent = result.get(node["parent"], (True, 1.0))
        result[identifier] = (parent[0] and state["visible"], parent[1] * state["mult"][3])
    return result


def transient_source(root, package):
    """Return original intro-only geometry with all original ancestors and states.

    Fully transparent or absent settled meshes are introductions, even when
    the source keeps a dormant placement object alive. A solid additive colour
    can draw with zero alpha, so nonzero source add is included explicitly.
    """
    final = root["frames"][-2]
    settled = _effective_visibility(root, final)
    final_states = dict(final["nodes"])
    meshes, excluded = set(), []
    for node in root["nodes"]:
        if node["geometry"] is None:
            continue
        identifier = node["id"]
        visible, alpha = settled.get(identifier, (False, 0))
        additive = (any(value != 0 for value in root["states"][final_states[identifier]]["add"])
                    if identifier in final_states else False)
        if visible and (alpha > 0 or additive):
            excluded.append(identifier)
        else:
            meshes.add(identifier)
    _require(meshes, "The original Frame contains no independently transient opening meshes")
    retained = set(meshes)
    for identifier in meshes:
        parent = root["nodes"][identifier]["parent"]
        while parent >= 0:
            retained.add(parent)
            parent = root["nodes"][parent]["parent"]
    result = copy.deepcopy(root)
    # Geometry on an ancestor is uncommon, but it must not accidentally become
    # an extra persistent draw when retained solely for a transient descendant.
    for node in result["nodes"]:
        if node["id"] not in meshes:
            node["geometry"] = None
    for frame in result["frames"]:
        frame["nodes"] = [pair for pair in frame["nodes"] if pair[0] in retained]
    source_geometries = sorted({root["nodes"][identifier]["geometry"] for identifier in meshes})
    return result, {"transientMeshes": len(meshes), "retainedAncestorNodes": len(retained) - len(meshes),
                    "excludedSettledMeshes": len(excluded),
                    "sourceGeometries": [package["geometries"][index]["source"] for index in source_geometries]}


def lane_region_source(root, package, rectangles):
    """Omit source draws proven outside every active lane-region viewport.

    This removes centre-only frame flourishes before recorded blend validation;
    the 27 DP title's blend 75, for example, never intersects either lane. Only
    affine 2D bounds are rejected here. Perspective/camera content is retained
    for the real recorded evaluator to handle rather than approximated away.
    """
    candidates = {node["id"] for node in root["nodes"] if node["geometry"] is not None}
    retained_meshes, visible_frames = set(), set()
    for frame_index, frame in enumerate(root["frames"]):
        transforms, uncertain, visibility = {}, set(), {}
        for identifier, state_id in frame["nodes"]:
            node, state = root["nodes"][identifier], root["states"][state_id]
            matrix, origin = state["matrix"], state["origin"]
            parent_visible, parent_alpha, parent_add = visibility.get(node["parent"], (True, 1.0, False))
            visible = parent_visible and state["visible"]
            alpha = parent_alpha * state["mult"][3]
            additive_colour = parent_add or any(value != 0 for value in state["add"])
            visibility[identifier] = (visible, alpha, additive_colour)
            planar = (frame["camera"] is None and state["projection"] in (0, 1)
                      and all(matrix[index] == value for index, value in ((2, 0), (5, 0), (6, 0), (7, 0), (8, 1), (11, 0))))
            if not planar or node["parent"] in uncertain:
                uncertain.add(identifier)
                if identifier in candidates and visible and (alpha > 0 or additive_colour):
                    retained_meshes.add(identifier)
                    visible_frames.add(frame_index)
                continue
            a, b, c, d = matrix[0], matrix[1], matrix[3], matrix[4]
            tx, ty = matrix[9] - origin[0] * a - origin[1] * c, matrix[10] - origin[0] * b - origin[1] * d
            pa, pb, pc, pd, px, py = transforms.get(node["parent"], (1, 0, 0, 1, 0, 0))
            world = (pa * a + pc * b, pb * a + pd * b, pa * c + pc * d, pb * c + pd * d,
                     pa * tx + pc * ty + px, pb * tx + pd * ty + py)
            transforms[identifier] = world
            if identifier not in candidates or not visible or (alpha <= 0 and not additive_colour):
                continue
            vertices = package["geometries"][node["geometry"]]["vertices"]
            if any(point[2] != 0 for point in vertices):
                retained_meshes.add(identifier)
                visible_frames.add(frame_index)
                continue
            a, b, c, d, tx, ty = world
            xs = [point[0] * a + point[1] * c + tx for point in vertices]
            ys = [point[0] * b + point[1] * d + ty for point in vertices]
            if xs and any(min(xs) < right and max(xs) > left and min(ys) < bottom and max(ys) > top
                          for left, top, right, bottom in rectangles):
                retained_meshes.add(identifier)
                visible_frames.add(frame_index)
    _require(retained_meshes, "No genuine transient opening geometry intersects the original lanes")
    retained = set(retained_meshes)
    for identifier in retained_meshes:
        parent = root["nodes"][identifier]["parent"]
        while parent >= 0:
            retained.add(parent)
            parent = root["nodes"][parent]["parent"]
    result = copy.deepcopy(root)
    for node in result["nodes"]:
        if node["id"] not in retained_meshes:
            node["geometry"] = None
    for frame in result["frames"]:
        frame["nodes"] = [pair for pair in frame["nodes"] if pair[0] in retained]
    return result, {"meshesIntersectingLaneRegion": len(retained_meshes),
                    "omittedOutsideLaneMeshes": len(candidates - retained_meshes),
                    "visibleSourceFrameCount": len(visible_frames),
                    "firstVisibleSourceFrame": min(visible_frames), "lastVisibleSourceFrame": max(visible_frames)}


def crop_clip(clip, rectangle):
    """Rebase original local geometry to a bounded group viewport, with no pixels.

    Existing internal masks and the source camera survive. The recorded runtime
    additionally clips to its own canvas, so the other group/outer frame cannot
    spill into an active lane surface.
    """
    left, top, right, bottom = rectangle
    _require(all(type(value) in (int, float) and math.isfinite(value) for value in rectangle)
             and 0 <= left < right <= clip["width"] and 0 <= top < bottom <= clip["height"],
             "The source lane viewport escapes the original canvas")
    result = copy.deepcopy(clip)
    state_id = len(result["states"])
    result["states"].append({"matrix": [1, 0, 0, 0, 1, 0, 0, 0, 1, -left, -top, 0],
                             "origin": [0, 0, 0], "projection": "affine", "multiply": [1, 1, 1, 1],
                             "add": [0, 0, 0, 0], "hsl": [0, 0, 0], "mask": None, "colourMatrix": None})
    result["nodes"] = [{"parent": -1, "geometry": -1, "blend": "normal"}] + [
        {**node, "parent": node["parent"] + 1} for node in result["nodes"]]
    for frame in result["frames"]:
        if frame["nodes"]:
            frame["nodes"] = [[0, state_id]] + [[node + 1, state] for node, state in frame["nodes"]]
        if frame["camera"] is not None:
            frame["camera"][0] -= left
            frame["camera"][1] -= top
    result["width"], result["height"] = right - left, bottom - top
    return result


def _texture_rows(library, source, texture_index):
    rows = list(texture_index[source])
    names = {row["name"]: row for row in rows}
    for dependency in TEXTURE_DEPENDENCIES:
        for row in texture_index[dependency]:
            previous = names.get(row["name"])
            if previous is not None:
                # The owning Frame is the texture authority. The shared
                # packages only supply symbols which it does not contain;
                # e.g. 27 has its own qpro_usr, distinct from today's common.
                # This is the same explicit package-first lookup used by the
                # existing static Frame author, not a cross-package alias.
                continue
            else:
                rows.append(row)
                names[row["name"]] = row
    for row in rows:
        _require(inside(library, row["sprite"]).is_file(), "An original source texture is absent")
    return rows


def export_reference(library, output, *, bemani_root, image_tools, frames=FRAMES):
    """Record selected original packages to a new external reference directory.

    No image is copied here. The returned index owns byte-checked copy receipts,
    and prepare_assets copies only a selected Frame/layout into an author tree.
    """
    import brotli

    library = _independent(library)
    output = _independent(output, output=True, exists=False)
    _require(not output.exists() and not output.is_relative_to(library) and not library.is_relative_to(output),
             "Lane-opening reference output must be new and separate from its read-only library")
    _require(frames and len(set(frames)) == len(frames) and set(frames).issubset(FRAMES), "Unknown or duplicate source Frames")
    bemani_root, image_tools = _independent(bemani_root), _independent(image_tools)
    tools, source_swf, renderer = evaluator_tools(bemani_root, image_tools)
    packages, texture_index = library_index(library)
    output.mkdir(parents=True)
    result = {"contract": CONTRACT, "status": "exporting", "library": str(library), "originalHddAccessed": False,
              "originalImageBytesPreserved": True, "runtimeSourceInterpreter": False,
              "runtimeFrameRasters": 0, "frames": {}}
    for frame_id in frames:
        package = SOURCE_PACKAGES[frame_id]
        metadata = read_json(inside(library, packages[package]["authorData"]))
        rows = _texture_rows(library, package, texture_index)
        captured = io.StringIO()
        with contextlib.redirect_stdout(captured), contextlib.redirect_stderr(captured):
            shapes, geometries, geometry_map = source_geometry(tools, library, metadata, rows)
            movies, filters, hashes = {}, InternTable(), {}
            for item in metadata["movies"]:
                _require(item["parsed"], "An original Frame movie was not parsed")
                raw = inside(library, item["raw"])
                _require(file_sha(raw) == item["sha256"], "An original Frame movie changed")
                bsi = library / item["bsi"]
                movie = source_swf(item["name"], raw.read_bytes(), bsi.read_bytes() if bsi.is_file() else b"")
                movie.parse()
                movies[item["name"]] = movie
                hashes[item["name"]] = item["sha256"]
                for block in movie.export_filter_blocks:
                    filters.add(block)
            clips, receipts, transient_textures = {}, [], set()
            for layout, movie_name in SOURCE_MOVIES.items():
                movie = movies[movie_name]
                _require(movie.fps == 60 and movie.location.left == 0 and movie.location.top == 0,
                         "The Frame source has a different source clock or canvas origin")
                flow_tags, controls = _root_return_tags(movie)

                class IntroRenderer(renderer):
                    def _AFPRenderer__place(self, tag, owner, prefix=""):
                        if id(tag) in flow_tags and owner is self._AFPRenderer__root:
                            return None, False
                        return super()._AFPRenderer__place(tag, owner, prefix)

                recorder = LocalStateRecorder(tools, IntroRenderer)
                root = recorder.record(package, layout, movie, movies, shapes, geometry_map, filters)
                source_package = {"geometries": geometries, "filters": filters.values}
                evidence = output / "source-graphs" / frame_id
                evidence.mkdir(parents=True, exist_ok=True)
                for name, value in ((layout + "-root", root), (layout + "-geometry", source_package)):
                    (evidence / (name + ".json.br")).write_bytes(brotli.compress(
                        json.dumps(value, separators=(",", ":"), allow_nan=False).encode("utf-8"), quality=5))
                transient, receipt = transient_source(root, source_package)
                end_frame = movie.labels["loop"]
                # Only the genuine pre-loop sequence plays. The fully evaluated
                # settled tail is used above solely to distinguish static art.
                transient["frames"] = transient["frames"][:end_frame] + [{"nodes": [], "camera": None}]
                transient, spatial_receipt = lane_region_source(transient, source_package, source_viewports(frame_id, layout))
                receipt.update(spatial_receipt)
                source_ids = {index: "omni.intro.t" + row["sha256"][:24] for index, row in enumerate(rows)}
                items = []
                for group, rectangle in enumerate(source_viewports(frame_id, layout)):
                    regional, regional_receipt = lane_region_source(transient, source_package, [rectangle])
                    clip = to_recorded_animation(source_package, regional, source_ids)
                    clip["trigger"], clip["loop"] = "gameplay.attach", False
                    used = {geometry["resource"] for geometry in clip["geometries"] if geometry["resource"] is not None}
                    transient_textures.update(used)
                    selected = crop_clip(clip, rectangle)
                    raw = json.dumps(selected, ensure_ascii=False, separators=(",", ":"), allow_nan=False).encode("utf-8")
                    compressed = brotli.compress(raw, quality=7)
                    _require(len(raw) <= 128 * 1024 * 1024 and len(compressed) <= 16 * 1024 * 1024,
                             "The original opening exceeds the recorded-animation byte budget")
                    path = f"clips/{frame_id}/{layout}-g{group + 1}.json.br"
                    target = output / path
                    target.parent.mkdir(parents=True, exist_ok=True)
                    target.write_bytes(compressed)
                    items.append({"group": group, "clip": path, "sha256": hashlib.sha256(compressed).hexdigest(),
                                  "resources": sorted(used), "durationMs": end_frame * 1000 / movie.fps,
                                  "sourceViewport": rectangle, "sourceCanvas": root["canvas"],
                                  "sourceFrames": len(movie.frames), "sourcePrefixFrames": end_frame,
                                  "regionalSource": regional_receipt,
                                  "bytes": len(compressed), "decodedJsonBytes": len(raw),
                                  "nodes": len(selected["nodes"]), "states": len(selected["states"]),
                                  "geometries": len(selected["geometries"]),
                                  "sourceMasks": sum(state["mask"] is not None for state in selected["states"]),
                                  "additiveNodes": sum(node["blend"] == "additive" for node in selected["nodes"])})
                clips[layout] = items
                receipts.append({"layout": layout, "sourceMovie": movie_name, "rawAfpSha256": hashes[movie_name],
                                 "terminalReturn": controls, "sourceFPS": movie.fps, **receipt,
                                 "hierarchyMathChecks": recorder.math_checks,
                                 "maximumHierarchyMathError": recorder.math_max_error})
        warnings = sorted(set(captured.getvalue().splitlines()))
        unsupported = [line for line in warnings if line.startswith("WARNING:")
                       and "includes unparsed options and might not display properly!" not in line
                       and line != "WARNING: Root clip requested to play not in top-left corner!"]
        _require(not unsupported, "The offline Frame evaluation has unsupported operations: " + "; ".join(unsupported))
        textures, seen = [], set()
        for row in rows:
            identifier = "omni.intro.t" + row["sha256"][:24]
            if identifier not in transient_textures or identifier in seen:
                continue
            seen.add(identifier)
            sampler = row["sampler"]
            _require(sampler["min"] == sampler["mag"] and sampler["min"] in ("linear", "nearest")
                     and sampler["wrapS"] == sampler["wrapT"] == "clamp", "The original opening has unsupported sampling")
            source = inside(library, row["sprite"])
            data = source.read_bytes()
            _require(hashlib.sha256(data).hexdigest() == row["sha256"] and len(data) >= 33
                     and data[:8] == b"\x89PNG\r\n\x1a\n" and struct.unpack(">II", data[16:24]) == (row["width"], row["height"]),
                     "An original opening PNG no longer matches the verified library")
            textures.append({"id": identifier, "type": "texture", "path": "art/lighting-intro/" + row["sha256"] + ".png",
                             "filter": sampler["min"], "source": row["sprite"], "sha256": row["sha256"],
                             "width": row["width"], "height": row["height"], "sourceName": row["name"],
                             "sourcePackage": row["package"]})
        result["frames"][frame_id] = {"sourcePackage": package, "sourceArchiveSha256": packages[package]["sha256"],
                                     "layouts": clips, "textures": textures, "sourceReceipts": receipts,
                                     "warnings": warnings, "sourceArtwork": "Original package-specific transient lane-region geometry"}
        (output / "reference.json").write_text(json.dumps(result, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
        print(f"Lane opening {frame_id}: {len(textures)} original PNGs; "
              f"{sum(item['bytes'] for group in clips.values() for item in group)} recorded bytes", flush=True)
    result["status"] = "complete"
    (output / "reference.json").write_text(json.dumps(result, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    return result


def load_reference(path):
    path = _independent(path)
    result = read_json(path)
    _require(result.get("contract") == CONTRACT and result.get("status") == "complete"
             and result.get("originalHddAccessed") is False and result.get("runtimeFrameRasters") == 0,
             "Expected a complete independent lane-opening reference")
    _require(set(result["frames"]).issubset(FRAMES) and result["frames"], "The reference has unknown Frames")
    result["directory"] = path.parent
    result["library"] = _independent(result["library"])
    return result


def prepare_assets(reference, output, *, frame, layout):
    """Copy only this selected contribution, preserving source bytes and samplers."""
    output = _independent(output, output=True)
    library, source_directory = reference["library"], reference["directory"]
    _require(frame in reference["frames"] and layout in SOURCE_MOVIES, "The selected opening is absent")
    _require(not output.is_relative_to(library) and not library.is_relative_to(output)
             and not output.is_relative_to(source_directory) and not source_directory.is_relative_to(output),
             "The author output overlaps its read-only source reference")
    source = reference["frames"][frame]
    clips = source["layouts"][layout]
    used = {identifier for clip in clips for identifier in clip["resources"]}
    resources, animations, copies, pixels = [], [], [], 0
    for texture in source["textures"]:
        if texture["id"] not in used:
            continue
        resources.append({key: texture[key] for key in ("id", "type", "path", "filter")})
        copies.append((inside(library, texture["source"]), texture["path"], texture["sha256"]))
        pixels += texture["width"] * texture["height"]
    _require({item["id"] for item in resources} == used, "A selected opening has unresolved original PNGs")
    for clip in clips:
        identifier = f"lo.{layout}.g{clip['group'] + 1}"
        path = f"gameplay/lane-intro/{frame}/{layout}-g{clip['group'] + 1}.json.br"
        animations.append({"id": identifier, "path": path, "resources": clip["resources"]})
        copies.append((inside(source_directory, clip["clip"]), path, clip["sha256"]))
    for source_path, relative, sha256 in copies:
        target = _output_file(output, relative)
        data = source_path.read_bytes()
        _require(hashlib.sha256(data).hexdigest() == sha256, "A captured opening asset changed")
        if target.exists():
            _require(target.is_file() and target.read_bytes() == data, "An opening output conflicts with existing author data")
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
    return {"resources": resources, "animations": animations, "clips": clips, "frame": frame, "layout": layout,
            "durationMs": max(clip["durationMs"] for clip in clips), "texturePixels": pixels,
            "encodedBytes": sum((output / relative).stat().st_size for _, relative, _ in copies),
            "files": [relative for _, relative, _ in copies], "originalPngBytesPreserved": True}


def _node(identifier, kind="container", properties=None, *, children=(), slot=None):
    result = {"id": identifier, "type": kind, "target": {"kind": "global"}, "blend": "alpha",
              "properties": properties or {}, "effects": [], "children": list(children)}
    if slot is not None:
        result["slot"] = slot
    return result


def inject(document, manifest, *, frame, layout, assets):
    """Add this Frame's opening to real stage surfaces, below all native notes."""
    _require(document.get("contract") == "oms-gameplay-skin-scene.v1"
             and manifest.get("contract") == "oms-gameplay-skin-manifest.v1"
             and document.get("root", {}).get("id") == f"omni.frame.{frame}.root"
             and frame in FRAMES and layout in SOURCE_MOVIES
             and assets["frame"] == frame and assets["layout"] == layout, "Expected the exact selected Omni contribution")
    prefix = "lo." + frame_namespace(frame) + "." + layout
    children, bindings, variants = [], [], []
    for clip in assets["clips"]:
        group = clip["group"]
        identifier = prefix + f".g{group + 1}"
        animation = _node(identifier + ".clip", "recorded-animation")
        animation["animation"] = f"lo.{layout}.g{group + 1}"
        animation["clock"] = CLOCK
        available = _node(identifier + ".available", properties={"visible": False}, children=[animation])
        bindings.append({"id": identifier + ".available.bind", "target": available["id"],
                         "property": "visible", "source": "preparation.available"})
        if layout == "dp":
            stage = _node(identifier + ".stage", properties={"visible": False}, children=[available])
            variants.append({"id": identifier + ".stage.select", "target": stage["id"], "property": "visible",
                             "source": "layout.group", "default": False, "cases": [
                                 {"id": identifier + ".stage.bms", "key": f"bms.group.deck-{group + 1}", "value": True},
                                 {"id": identifier + ".stage.mania", "key": f"mania.group.stage-{group + 1}", "value": True}]})
            children.append(stage)
        else:
            children.append(available)
    root = _node(prefix, properties={"visible": False, "z": -20}, children=children, slot="playfield.baseplate")
    template = {"id": prefix + ".template", "root": root}
    instance = {"id": prefix + ".instance", "template": template["id"],
                "material": {"slot": "playfield.baseplate", "resource": f"gameplay/frames/{frame}/common/empty"}}
    bindings.append({"id": prefix + ".enabled", "target": prefix, "property": "visible", "source": "option.lane-opening"})
    additions = [(manifest.setdefault("animations", []), item) for item in assets["animations"]]
    additions += [(manifest["resources"], item) for item in assets["resources"]]
    additions += [(document["templates"], template), (document["instances"], instance)]
    additions += [(document["bindings"], item) for item in bindings]
    additions += [(document["variants"], item) for item in variants]
    for items, item in additions:
        matches = [previous for previous in items if previous.get("id") == item["id"]]
        _require(not matches or matches == [item], "An existing author declaration conflicts with the original opening")
    for items, item in additions:
        if not any(previous.get("id") == item["id"] for previous in items):
            items.append(copy.deepcopy(item))
    return {"frame": frame, "layout": layout, "clock": CLOCK, "durationMs": assets["durationMs"],
            "originalSourcePrefix": True, "runtimeFrameRasters": 0, "runtimeSourceInterpreter": False,
            "layer": "playfield.baseplate / Underlay; native notes and LN remain above the opening",
            "groups": len(assets["clips"]), "resources": len(assets["resources"]),
            "recordedClips": len(assets["animations"]), "texturePixels": assets["texturePixels"]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--library", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--bemani-root", type=Path, required=True)
    parser.add_argument("--image-tools", type=Path, required=True)
    parser.add_argument("--frame", choices=FRAMES, action="append")
    args = parser.parse_args()
    export_reference(args.library, args.output, bemani_root=args.bemani_root,
                     image_tools=args.image_tools, frames=tuple(args.frame) if args.frame else FRAMES)


if __name__ == "__main__":
    main()
