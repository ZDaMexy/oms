"""Offline source-geometry separation for private Omni column backgrounds.

This module does not infer what the native Column Brightness Adjust control does.
The source library and author stay read-only. A prepared split is usable only
after its full source redraw and separate column composition pass exact RGBA.
"""
from __future__ import annotations

import contextlib
import copy
import io
import json
from pathlib import Path

import brotli
from PIL import Image

from build_fullcombo_catalog import to_recorded_animation
from export_customization_animations import evaluator_tools, library_index
from lighting_lane_intro import SOURCE_PACKAGES, _texture_rows
from lighting_persistent import (_SourceRaster, _file, _independent, _json, _sha, _walk, _write,
                                background_owner, verify_background)

CONTRACT = "oms-iidx-column-source-reference.v1"
LAYOUTS = ("p1", "p2", "dp")
CANVAS = (1920, 1080)
COLUMN_TEXTURES = {"lane_1p.png", "lane_2p.png"}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def select_source(root, geometry, textures, *, names=COLUMN_TEXTURES):
    """Select exact textured meshes and every original ancestor; never an AABB."""
    selected, paths = set(), []
    for node in root["nodes"]:
        if node["geometry"] is None:
            continue
        draws = geometry["geometries"][node["geometry"]]["draws"]
        used = {textures[draw["texture"]]["name"] for draw in draws if draw["texture"] is not None}
        if used & names:
            require(used <= names, "A column mesh also owns unrelated source artwork")
            selected.add(node["id"])
    require(selected, "No exact original column meshes were found")
    retained = set(selected)
    for identifier in sorted(selected):
        current, path = identifier, []
        while current >= 0:
            node = root["nodes"][current]
            retained.add(current)
            path.append([node["sourceTag"], node["depth"]])
            current = node["parent"]
        paths.append(list(reversed(path)))
    result = copy.deepcopy(root)
    for node in result["nodes"]:
        if node["id"] not in selected:
            node["geometry"] = None
    for frame in result["frames"]:
        frame["nodes"] = [pair for pair in frame["nodes"] if pair[0] in retained]
    return result, {"meshes": sorted(selected), "ancestorNodes": len(retained) - len(selected), "layerPaths": paths}


def static_column_clip(root, geometry, resource_ids, *, frame_index=180):
    """One genuine settled source sample and one non-playing terminal sentinel."""
    require(0 <= frame_index < len(root["frames"]) - 1, "The static column source sample is outside the recorded prefix")
    result = copy.deepcopy(root)
    result["frames"] = [copy.deepcopy(root["frames"][frame_index]), {"nodes": [], "camera": None}]
    clip = to_recorded_animation(geometry, result, resource_ids)
    clip.update(loop=True, trigger="gameplay.attach")
    return clip


def verify_settled_columns(root, *, frame_index=180):
    """A one-frame program may replace only the proven constant source tail."""
    require(root["frames"][-1] == {"nodes": [], "camera": None}, "The source column terminal sentinel changed")
    settled = root["frames"][frame_index]
    require(settled["nodes"] and all(frame == settled for frame in root["frames"][frame_index:-1]),
            "The original column tail still moves; a static source clip would lose animation")
    return {"firstSourceFrame": frame_index, "lastSourceFrame": len(root["frames"]) - 2,
            "checkedFrames": len(root["frames"]) - frame_index - 1, "exactSourceStateIdentity": True}


def _draw_signatures(clip, frame):
    states = dict(frame["nodes"])
    result = []
    for identifier, _ in frame["nodes"]:
        node = clip["nodes"][identifier]
        if node["geometry"] < 0:
            continue
        ancestry, current = [], identifier
        while current >= 0:
            ancestry.append(clip["states"][states[current]])
            current = clip["nodes"][current]["parent"]
        result.append({"geometry": clip["geometries"][node["geometry"]], "ancestry": list(reversed(ancestry)),
                       "blend": node["blend"], "camera": frame["camera"]})
    return result


def _partition_nodes(clip, selected):
    keep = set(selected)
    for index in tuple(keep):
        current = clip["nodes"][index]["parent"]
        while current >= 0:
            keep.add(current)
            current = clip["nodes"][current]["parent"]
    node_ids = {old: new for new, old in enumerate(sorted(keep))}
    geometry_ids = {old: new for new, old in enumerate(sorted({clip["nodes"][index]["geometry"] for index in selected}))}
    state_ids = {old: new for new, old in enumerate(sorted({state for frame in clip["frames"]
                                                          for node, state in frame["nodes"] if node in keep}))}
    result = copy.deepcopy(clip)
    result["geometries"] = [copy.deepcopy(clip["geometries"][index]) for index in geometry_ids]
    result["states"] = [copy.deepcopy(clip["states"][index]) for index in state_ids]
    result["nodes"] = [{**node, "parent": node_ids[node["parent"]] if node["parent"] >= 0 else -1,
                        "geometry": geometry_ids[node["geometry"]] if index in selected else -1}
                       for index, node in enumerate(clip["nodes"]) if index in keep]
    result["frames"] = [{"nodes": [[node_ids[node], state_ids[state]] for node, state in frame["nodes"] if node in keep],
                         "camera": copy.deepcopy(frame["camera"])} for frame in clip["frames"]]
    return result


def partition_recorded_runs(clip, column_resources, base_resources):
    """Retain DP's literal base1/column1/base2/column2 order without AABB guesses."""
    require(column_resources and base_resources and not column_resources & base_resources, "Use disjoint exact source roles")
    require(all(item["resource"] in column_resources | base_resources for item in clip["geometries"]),
            "The28 prefix contains an unrelated source mesh")
    orders = [[node for node, _ in frame["nodes"] if clip["nodes"][node]["geometry"] >= 0] for frame in clip["frames"]]
    order = max(orders, key=len)
    require(order and len(order) == len(set(order)) and set(order) == set().union(*map(set, orders)),
            "The source prefix has no stable complete draw order")
    require(all(frame_order == [node for node in order if node in frame_order] for frame_order in orders),
            "The source prefix changes draw order")
    runs = []
    for node in order:
        resource = clip["geometries"][clip["nodes"][node]["geometry"]]["resource"]
        role = "columns" if resource in column_resources else "base"
        if not runs or runs[-1]["role"] != role:
            runs.append({"role": role, "nodes": []})
        runs[-1]["nodes"].append(node)
    require([run["role"] for run in runs] in (["base", "columns"], ["base", "columns", "base", "columns"]),
            "The28 prefix does not retain its exact source base/column order")
    parts = [{"role": run["role"], "clip": _partition_nodes(clip, set(run["nodes"]))} for run in runs]
    for index, frame in enumerate(clip["frames"]):
        require(_draw_signatures(clip, frame) == [draw for part in parts for draw in _draw_signatures(part["clip"], part["clip"]["frames"][index])],
                "The source-run partition changed the original draw stream")
    return parts, {"checkedFramesIncludingSentinel": len(clip["frames"]), "exactDrawStreamPreserved": True,
                   "sourceRunOrder": [part["role"] for part in parts], "aabbIndependenceAssumed": False,
                   "sourceAlphaMasksMatricesUvsAndCamerasPreserved": True}


class ColumnRaster(_SourceRaster):
    """Reuse the original renderer, GEO, texture cache and source movie parser."""

    def only(self, name, paths, *, frames=(180,)):
        tools, movie = self.tools, self.movie(name)
        selected = {tuple(tuple(pair) for pair in path) for path in paths}

        class SelectedRenderer(tools.AFPRenderer):
            def __init__(self, **kwargs):
                super().__init__(**kwargs)
                self.stack = []

            def _AFPRenderer__render_object(self, image, obj, *args, **kwargs):
                path = (*self.stack, (obj.source.tag_id, obj.depth))
                if not any(path == candidate[:len(path)] for candidate in selected):
                    return image
                self.stack.append(path[-1])
                try:
                    return super()._AFPRenderer__render_object(image, obj, *args, **kwargs)
                finally:
                    self.stack.pop()

        renderer = SelectedRenderer(shapes={}, textures={}, swfs={}, single_threaded=True, enable_aa=False)
        for name_value, shape in self.shapes.items():
            renderer.add_shape(name_value, shape)
        for row in self.textures:
            stem = row["name"].removesuffix(".png")
            if stem in renderer.textures:
                continue
            if stem not in self.images:
                data = _file(self.library, row["sprite"]).read_bytes()
                require(_sha(data) == row["sha256"], "An original column PNG changed")
                with Image.open(io.BytesIO(data)) as image:
                    self.images[stem] = image.convert("RGBA")
            renderer.add_texture(stem, self.images[stem])
        renderer.add_swf(name, movie)
        return renderer.render_path(name, background_color=tools.Color(0, 0, 0, 0), only_frames=[frame + 1 for frame in frames])


def compose_source(raster, research, *, frame, layout, source_canvas, omitted=()):
    """Use the existing source centre/side order and native black BGA apertures."""
    import math

    shift = 0 if layout == "dp" else 270 if layout == "p1" else -270
    centre_name = "center_frame_dp_1p" if layout == "dp" else "center_frame_sp"
    movie = "1p_frame" if layout == "p1" else "2p_frame" if layout == "p2" else centre_name
    if frame in ("iidx30", "iidx31", "iidx32", "iidx33"):
        # build_skin.background uses the literal native placeholder geometry
        # for the four numbered HD frames. BPL and SD use the separate safe
        # aperture composition below; their reference directories differ.
        side = raster.render(movie, omitted)
        gameplay = _json(research / "gameplay-assets.json")
        if layout == "dp":
            result = side
        else:
            centre = raster.render(centre_name)
            for viewport in gameplay["native_bga_placeholders"]:
                if viewport["movie"] == centre_name:
                    bounds = viewport["bounds"]
                    centre.paste((0, 0, 0, 255), (math.floor(bounds["left"]), math.floor(bounds["top"]),
                                                  math.ceil(bounds["right"]), math.ceil(bounds["bottom"])))
            result = Image.new("RGBA", CANVAS)
            result.alpha_composite(centre, (shift, 0))
            result.alpha_composite(side)
        if layout == "dp":
            for viewport in gameplay["native_bga_placeholders"]:
                if viewport["movie"] == centre_name:
                    bounds = viewport["bounds"]
                    result.paste((0, 0, 0, 255), (math.floor(bounds["left"]), math.floor(bounds["top"]),
                                                  math.ceil(bounds["right"]), math.ceil(bounds["bottom"])))
        return result
    scale = CANVAS[0] / source_canvas[0]
    require(CANVAS[1] / source_canvas[1] == scale, "The original column canvas requires a different source projection")
    centre = raster.render(centre_name, omitted if layout == "dp" else ()).resize(CANVAS, Image.Resampling.NEAREST)
    result = Image.new("RGBA", CANVAS)
    for viewport in _json(research / "effective-bga-viewports.json")["viewports"]:
        if viewport["movie"] == centre_name:
            left, top, right, bottom = viewport["fullyTransparentRectangularViewportCandidate"]["bounds"]
            result.paste((0, 0, 0, 255), (math.floor(left * scale + shift), math.floor(top * scale),
                                          math.ceil(right * scale + shift), math.ceil(bottom * scale)))
    result.alpha_composite(centre, (shift, 0))
    side = raster.render("dp_frame_1p" if layout == "dp" else movie, () if layout == "dp" else omitted)
    result.alpha_composite(side.resize(CANVAS, Image.Resampling.NEAREST))
    return result


def _frame_part(author, frame, layout):
    catalog = _json(author / "gameplay/content.json")
    choice = next(choice for module in catalog["modules"] if module["id"] == "frame"
                  for choice in module["choices"] if choice["value"] == frame)
    return choice if layout == "dp" else next(part for part in choice["variants"] if part["when"].get("layout") == layout)


def bitmap_consumers(scene, manifest, *, frame, layout, resources):
    """Pin every bitmap consumer and its unchanged entrance/availability ancestry."""
    owner, _ = background_owner(scene, manifest, frame=frame, layout=layout)
    controls = {}
    for field in ("tracks", "bindings", "variants"):
        for item in scene[field]:
            controls.setdefault(item["target"], []).append((field, item))
    rows = []

    def visit(node, parents):
        chain = [*parents, node]
        if node.get("resource") in resources:
            require(node["type"] == "sprite" and not node.get("children") and not node.get("effects")
                    and node.get("blend") in ("alpha", "inherit") and not set(node["properties"]) & {"colour", "brightness", "fill-mode"},
                    "Only neutral existing bitmap leaves may be separated")
            for ancestor in chain:
                require(ancestor.get("properties", {}).get("opacity", 1) in (0, 1),
                        "A translucent bitmap ancestry would change split-layer composition")
                for field, item in controls.get(ancestor["id"], []):
                    if item["property"] == "opacity":
                        require(field == "tracks" and item["easing"] == "step"
                                and all(key["value"] in (0, 1) for key in item["keyframes"]),
                                "A fading bitmap ancestry would change split-layer composition")
            rows.append({"node": node["id"], "resource": node["resource"],
                         "ancestors": [ancestor["id"] for ancestor in chain],
                         "sourceLeaf": copy.deepcopy(node),
                         "controlIds": [item["id"] for ancestor in chain for _, item in controls.get(ancestor["id"], [])]})
        for child in node.get("children", []):
            visit(child, chain)

    visit(owner, [])
    require(rows and {row["resource"] for row in rows} == set(resources), "A verified bitmap has no actual background consumer")
    return rows


def persistent_column_clip(author, manifest, column_hashes):
    resources = {resource["id"] for resource in manifest["resources"]
                 if _sha(_file(author, resource["path"]).read_bytes()) in column_hashes}
    matches = []
    for definition in manifest.get("animations", []):
        if not set(definition["resources"]) & resources:
            continue
        data = _file(author, definition["path"]).read_bytes()
        clip = json.loads(brotli.decompress(data) if definition["path"].endswith(".br") else data)
        if any(geometry["resource"] in resources for geometry in clip["geometries"]):
            matches.append((definition, clip, resources))
    require(len(matches) == 1, "Expected one original persistent 28 base/column program")
    definition, clip, resources = matches[0]
    require(clip["loop"] is False and clip["fps"] == 60 and len(clip["frames"]) == 61
            and clip["frames"][-1] == {"nodes": [], "camera": None}, "The existing 28 source prefix/hold/sentinel changed")
    return definition, clip, resources


def verify_28_handover(root, geometry, textures):
    for names in (COLUMN_TEXTURES, {"lane_base.png"}):
        selected, _ = select_source(root, geometry, textures, names=names)
        require(all(frame == selected["frames"][30] for frame in selected["frames"][31:-1]),
                "The28 source component no longer matches its frame30 hold through the static handover")
    return {"heldSourceFrame": 30, "staticSourceFrame": 180, "allRemainingSourceStatesIdentical": True}


def verify_28_prefix(raster, root, geometry, textures, author, manifest, evidence):
    """Verify every moving source frame and every current C5 prefix/hold frame."""
    handover = verify_28_handover(root, geometry, textures)
    _, columns = select_source(root, geometry, textures)
    _, base = select_source(root, geometry, textures, names={"lane_base.png"})
    source_paths = {node: path for selection in (base, columns) for node, path in zip(selection["meshes"], selection["layerPaths"])}
    order = max(([node for node, _ in frame["nodes"] if node in source_paths] for frame in root["frames"][:31]), key=len)
    runs = []
    for node in order:
        role = "columns" if node in columns["meshes"] else "base"
        if not runs or runs[-1]["role"] != role:
            runs.append({"role": role, "paths": []})
        runs[-1]["paths"].append(source_paths[node])
    with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
        original_frames = list(raster.only(root["sourceMovie"], base["layerPaths"] + columns["layerPaths"], frames=range(31)))
        rendered_runs = [list(raster.only(root["sourceMovie"], run["paths"], frames=range(31))) for run in runs]
    require(len(original_frames) == 31 and all(len(images) == 31 for images in rendered_runs),
            "The source prefix renderer omitted a requested frame")
    require(len({_sha(image.convert("RGBA").tobytes()) for image in original_frames}) > 1,
            "The 28 source prefix must contain its real moving geometry")
    mismatches = []
    for index, original in enumerate(original_frames):
        actual = Image.new("RGBA", original.size)
        for images in rendered_runs:
            actual.alpha_composite(images[index].convert("RGBA"))
        if actual.tobytes() != original.convert("RGBA").tobytes():
            mismatches.append(index)
            evidence.mkdir(parents=True, exist_ok=True)
            actual.save(evidence / f"frame-{index:03}-split.png")
            original.save(evidence / f"frame-{index:03}-original.png")
    column_hashes = {texture["sha256"] for texture in textures if texture["name"] in COLUMN_TEXTURES}
    definition, clip, resources = persistent_column_clip(author, manifest, column_hashes)
    base_hashes = {texture["sha256"] for texture in textures if texture["name"] == "lane_base.png"}
    base_resources = {resource["id"] for resource in manifest["resources"] if _sha(_file(author, resource["path"]).read_bytes()) in base_hashes}
    parts, stream = partition_recorded_runs(clip, resources, base_resources)
    require([run["role"] for run in runs] == stream["sourceRunOrder"], "The captured28 program changed its original source draw order")
    return {"status": "verified" if not mismatches else "rgba-mismatch", "sourcePixelFrames": 31,
            "sourcePixelMismatches": mismatches, "originalProgram": definition["path"],
            "originalProgramSha256": _sha(_file(author, definition["path"]).read_bytes()),
            "prefixAndHold": stream, "programFrames": len(clip["frames"]), "sourceHandover": handover,
            "sourceRuns": [{"role": part["role"], "geometryCount": len(part["clip"]["geometries"])} for part in parts]}


def verify_background_variants(raster, author, scene, manifest, persistent, output, research, *, frame, layout, root,
                               columns, selection, baked, initial):
    """Account for the current original, starless and laneless source closures."""
    owner, _ = background_owner(scene, manifest, frame=frame, layout=layout)
    used = {node.get("resource") for node in _walk(owner)}
    entries = [{"oldResource": baked["id"], "oldPath": baked["path"], "oldSha256": _sha(_file(author, baked["path"]).read_bytes()),
                "status": "split", "sourceOmissions": [], "floorless": initial["floorless"],
                "baseline": initial["baseline"], "recomposition": initial["recomposition"]}]
    for resource in manifest["resources"]:
        if resource["id"] not in used or not resource["path"].startswith("art/lp/b"):
            continue
        require(persistent is not None and persistent["sourceRecording"] == str(root["recordingPath"]),
                "Current persistent backgrounds require their exact explicit source reference")
        source = persistent["frames"][frame][layout]
        require(source["status"] == "verified" and source["sourceRootSha256"] == initial["sourceRootSha256"],
                "The persistent source hierarchy differs from the current column recording")
        require(source["background"]["sha256"] == _sha(_file(author, resource["path"]).read_bytes()),
                "The current persistent background differs from its preserved source redraw")
        omissions = source["selection"]["omittedLayerPaths"]
        evidence = output / "evidence" / frame / layout / "persistent"
        # The earlier persistent exporter already redrew this exact closure.
        # Reuse only its hash-pinned PNG, never re-render an identical full-screen
        # baseline just to repeat that receipt. The NEW floorless draw below is
        # still rendered from AFP/GEO, then recomposed against the current PNG.
        preserved = _file(persistent["directory"], source["background"]["path"])
        require(_sha(preserved.read_bytes()) == source["background"]["sha256"], "The preserved persistent redraw changed")
        with Image.open(preserved) as image:
            baseline = verify_background(image.convert("RGBA"), _file(author, resource["path"]), evidence)
        row = {"oldResource": resource["id"], "oldPath": resource["path"], "oldSha256": source["background"]["sha256"],
               "sourceOmissions": omissions, "baseline": baseline,
               "baselineProofReuse": {"reference": str(persistent["referencePath"]), "sha256": persistent["referenceSha256"],
                                      "sourceGraphAndRedrawPngUnchanged": True}}
        entries.append(row)
        if not baseline["matches"]:
            row["status"] = "source-baseline-mismatch"
            continue
        # The 28 prefix's laneless background has already removed these meshes;
        # retain it and adjust the existing source component program separately.
        absent = all(any(path == column[:len(path)] for path in omissions) for column in selection["layerPaths"])
        if absent:
            row["status"] = "unchanged-no-column"
            continue
        exclusions = omissions + [path for path in selection["layerPaths"] if path not in omissions]
        floorless = compose_source(raster, research, frame=frame, layout=layout, source_canvas=root["canvas"], omitted=exclusions)
        recomposed = Image.alpha_composite(floorless, columns)
        row["recomposition"] = verify_background(recomposed, _file(author, resource["path"]), evidence / "recomposition")
        if not row["recomposition"]["matches"]:
            row["status"] = "column-recomposition-mismatch"
            floorless.save(evidence / "floorless-source.png")
            continue
        relative = f"floorless/{frame}/{layout}-persistent.png"
        path = output / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        floorless.save(path)
        row.update(status="split", floorless={"path": relative, "sha256": _sha(path.read_bytes()), "rgbaSha256": _sha(floorless.tobytes())})
    return entries


def export_reference(library, author, recording, output, *, frame_research, bemani_root, image_tools,
                     frames=("iidx33", "iidx28", "bpl-apina"), layouts=LAYOUTS, persistent_reference=None):
    """Produce research evidence only; no author directory or OSK is generated."""
    library, author, recording = (_independent(path) for path in (library, author, recording))
    output = _independent(output, output=True, exists=False)
    require(not output.exists() and frames and len(frames) == len(set(frames))
            and layouts and len(layouts) == len(set(layouts)) and set(layouts) <= set(LAYOUTS)
            and set(frames) <= SOURCE_PACKAGES.keys() and set(frames) == frame_research.keys(),
            "Choose a fresh reference directory and exact per-Frame research inputs")
    require(not any(output.is_relative_to(path) or path.is_relative_to(output) for path in (library, author, recording.parent)),
            "Column reference output overlaps an immutable source")
    source_reference = _json(recording)
    require(source_reference["status"] == "complete" and source_reference["originalHddAccessed"] is False
            and Path(source_reference["library"]).resolve() == library, "Use the matching complete original source recording")
    tools, source_swf, _ = evaluator_tools(_independent(bemani_root), _independent(image_tools))
    packages, texture_index = library_index(library)
    persistent = None
    if persistent_reference is not None:
        persistent_path = _independent(persistent_reference)
        persistent = _json(persistent_path)
        require(persistent.get("contract") == "oms-iidx-persistent-frame-reference.v1"
                and persistent.get("status") == "complete" and persistent.get("originalHddAccessed") is False,
                "Use the complete independent original persistent-source proof")
        persistent.update(directory=persistent_path.parent, referencePath=persistent_path, referenceSha256=_sha(persistent_path.read_bytes()))
    output.mkdir(parents=True)
    result = {"contract": CONTRACT, "status": "probing", "library": str(library), "sourceAuthor": str(author),
              "recording": str(recording), "nativeFormula": "not-inferred", "originalHddAccessed": False, "frames": {}}
    _write(output / "reference.json", result)
    for frame in frames:
        package = SOURCE_PACKAGES[frame]
        research = _independent(frame_research[frame])
        research_stage = _json(_file(research, "skin-reference-map.json"))["stage"]
        metadata = _json(_file(library, packages[package]["authorData"]))
        textures = _texture_rows(library, package, texture_index)
        captured = io.StringIO()
        with contextlib.redirect_stdout(captured), contextlib.redirect_stderr(captured):
            raster = ColumnRaster(library, metadata, textures, tools, source_swf)
        result["frames"][frame] = {}
        for layout in layouts:
            key = f"{frame}/{layout}"
            row = {"status": "probing"}
            result["frames"][frame][layout] = row
            graph_dir = recording.parent / "source-graphs" / frame
            root_path, geometry_path = graph_dir / f"{layout}-root.json.br", graph_dir / f"{layout}-geometry.json.br"
            root = json.loads(brotli.decompress(root_path.read_bytes()))
            root["recordingPath"] = recording
            require([research_stage["width"], research_stage["height"]] == root["canvas"],
                    "The preserved research canvas belongs to a different source edition (including SD/HD Supernova)")
            geometry = json.loads(brotli.decompress(geometry_path.read_bytes()))
            selected, selection = select_source(root, geometry, textures)
            require(len(selection["meshes"]) == (2 if layout == "dp" else 1), "The original column mesh count changed")
            row.update(sourceRootSha256=_sha(root_path.read_bytes()), sourceGeometrySha256=_sha(geometry_path.read_bytes()),
                       sourcePackage=package, sourceMovie=root["sourceMovie"], selection=selection,
                       settledTail=verify_settled_columns(selected))
            part = _frame_part(author, frame, layout)
            scene, manifest = (_json(_file(author, part[field])) for field in ("scene", "manifest"))
            row["sourceOwner"] = {field: part[field] for field in ("scene", "manifest")}
            row["sourceOwner"].update(sceneSha256=_sha(_file(author, part["scene"]).read_bytes()),
                                      manifestSha256=_sha(_file(author, part["manifest"]).read_bytes()))
            _, baked = background_owner(scene, manifest, frame=frame, layout=layout)
            evidence = output / "evidence" / frame / layout
            prior = None if persistent is None else persistent.get("frames", {}).get(frame, {}).get(layout)
            if prior is not None:
                source_movie = next(movie for movie in metadata["movies"] if movie["name"] == root["sourceMovie"])
                require(prior["status"] == "verified" and prior["sourceRootSha256"] == row["sourceRootSha256"]
                        and prior["sourceRawAfpSha256"] == source_movie["sha256"]
                        and prior["sourceOwner"]["bakedSha256"] == _sha(_file(author, baked["path"]).read_bytes())
                        and prior["baseline"]["matches"], "The preserved full-source baseline differs from this exact closure")
                row["baseline"] = copy.deepcopy(prior["baseline"])
                row["baselineProofReuse"] = {"reference": str(persistent["referencePath"]), "sha256": persistent["referenceSha256"],
                                              "sourceAfpGraphAndBakedPngUnchanged": True}
            else:
                with contextlib.redirect_stdout(captured), contextlib.redirect_stderr(captured):
                    baseline = compose_source(raster, research, frame=frame, layout=layout, source_canvas=root["canvas"])
                row["baseline"] = verify_background(baseline, _file(author, baked["path"]), evidence)
            if not row["baseline"]["matches"]:
                row["status"] = "source-baseline-mismatch"
                _write(output / "reference.json", result)
                print(key + ": source baseline mismatch", flush=True)
                continue
            with contextlib.redirect_stdout(captured), contextlib.redirect_stderr(captured):
                floorless = compose_source(raster, research, frame=frame, layout=layout,
                                           source_canvas=root["canvas"], omitted=selection["layerPaths"])
                columns = next(iter(raster.only(root["sourceMovie"], selection["layerPaths"])))
            columns = columns.convert("RGBA").resize(CANVAS, Image.Resampling.NEAREST)
            recomposed = Image.alpha_composite(floorless, columns)
            row["recomposition"] = verify_background(recomposed, _file(author, baked["path"]), evidence / "recomposition")
            if not row["recomposition"]["matches"]:
                row["status"] = "column-recomposition-mismatch"
                evidence.mkdir(parents=True, exist_ok=True)
                floorless.save(evidence / "floorless-source.png")
                columns.save(evidence / "columns-source.png")
            else:
                row["status"] = "static-source-verified"
                for name, image in (("floorless", floorless), ("columns-evidence", columns)):
                    relative = f"{name}/{frame}/{layout}.png"
                    path = output / relative
                    path.parent.mkdir(parents=True, exist_ok=True)
                    image.save(path)
                    row[name] = {"path": relative, "sha256": _sha(path.read_bytes()), "rgbaSha256": _sha(image.tobytes())}
                aliases = {index: "lc.t" + texture["sha256"][:24] for index, texture in enumerate(textures)}
                clip = static_column_clip(selected, geometry, aliases)
                relative = f"clips/{frame}/{layout}.json.br"
                path = output / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(brotli.compress(json.dumps(clip, separators=(",", ":"), allow_nan=False).encode(), quality=7))
                used = {item["resource"] for item in clip["geometries"]}
                row["clip"] = {"path": relative, "sha256": _sha(path.read_bytes()), "frames": 2, "playableFrames": 1,
                               "sourceSampleFrame": 180, "sourceCanvas": root["canvas"], "addedTerminalFrame": True}
                row["textures"] = [{"id": aliases[index], "source": texture["sprite"], "sha256": texture["sha256"],
                                    "width": texture["width"], "height": texture["height"], "filter": texture["sampler"]["min"]}
                                   for index, texture in enumerate(textures) if aliases[index] in used]
                with contextlib.redirect_stdout(captured), contextlib.redirect_stderr(captured):
                    row["backgrounds"] = verify_background_variants(raster, author, scene, manifest, persistent, output,
                        research, frame=frame, layout=layout, root=root, columns=columns,
                        selection=selection, baked=baked, initial=row)
                    if frame == "iidx28":
                        row["prefix"] = verify_28_prefix(raster, root, geometry, textures, author, manifest, evidence / "prefix")
                if (any(item["status"] not in ("split", "unchanged-no-column") for item in row["backgrounds"])
                        or frame == "iidx28" and row["prefix"]["status"] != "verified"):
                    row["status"] = "lifecycle-source-mismatch"
                else:
                    row["bitmapConsumers"] = bitmap_consumers(scene, manifest, frame=frame, layout=layout,
                        resources={item["oldResource"] for item in row["backgrounds"] if item["status"] == "split"})
            _write(output / "reference.json", result)
            print(key + ": " + row["status"], flush=True)
        log = output / "logs" / (frame + ".log")
        log.parent.mkdir(exist_ok=True)
        log.write_text(captured.getvalue(), encoding="utf-8")
    statuses = [row["status"] for groups in result["frames"].values() for row in groups.values()]
    result["status"] = "static-probe-complete" if all(status == "static-source-verified" for status in statuses) else "partial-failure"
    result["lifecycleValidation"] = {"bitmapVariants": "full source RGBA including original and persistent branches",
        "bitmapControls": "original entrance/availability/regional ancestry retained; fractional fades rejected",
        "prefix28": "31 moving source pixel frames and61 original program draw streams per selected layout",
        "runtime": "pending current-host CLI and Player; no author was generated"}
    _write(output / "reference.json", result)
    return result


def load_reference(path):
    path = _independent(path)
    result = _json(path)
    require(result.get("contract") == CONTRACT and result.get("status") == "static-probe-complete"
            and result.get("nativeFormula") == "not-inferred" and result.get("originalHddAccessed") is False,
            "Use a complete independently redrawn column-source reference")
    result["directory"] = path.parent
    result["library"] = _independent(result["library"])
    result["sourceAuthor"] = _independent(result["sourceAuthor"])
    return result


def merge_references(paths, author, output):
    """Reuse exact completed source proofs in a new portable reference directory."""
    author = _independent(author)
    output = _independent(output, output=True, exists=False)
    require(paths and not output.exists(), "Choose completed reference inputs and a fresh output")
    sources = [(Path(path).resolve(strict=True), load_reference(path)) for path in paths]
    library = sources[0][1]["library"]
    recording = _independent(sources[0][1]["recording"])
    require(all(item["library"] == library and _independent(item["recording"]) == recording for _, item in sources),
            "Only identical preserved library/recording identities may share source proofs")
    for root in (library, author, recording.parent, *(item["directory"] for _, item in sources)):
        require(not output.is_relative_to(root) and not root.is_relative_to(output), "The merged reference overlaps a preserved input")
    packages, texture_index = library_index(library)
    output.mkdir(parents=True)
    result = {"contract": CONTRACT, "status": "merging", "library": str(library), "sourceAuthor": str(author),
              "recording": str(recording), "nativeFormula": "not-inferred", "originalHddAccessed": False, "frames": {},
              "proofInputs": [{"path": str(path), "sha256": _sha(path.read_bytes())} for path, _ in sources]}
    _write(output / "reference.json", result)
    for _, source in sources:
        for frame, layouts in source["frames"].items():
            textures = _texture_rows(library, SOURCE_PACKAGES[frame], texture_index)
            for layout, input_row in layouts.items():
                require(layout not in result["frames"].setdefault(frame, {}), "A source Frame/layout proof was supplied twice")
                row = copy.deepcopy(input_row)
                require(row["status"] == "static-source-verified" and row["baseline"]["matches"] and row["recomposition"]["matches"],
                        "Only exact source RGBA rows may be reused")
                old_part, part = (_frame_part(root, frame, layout) for root in (source["sourceAuthor"], author))
                scene, manifest = (_json(_file(author, part[field])) for field in ("scene", "manifest"))
                require(_sha(_file(source["sourceAuthor"], old_part["scene"]).read_bytes()) == _sha(_file(author, part["scene"]).read_bytes()),
                        "The new parent changed the source-checked Frame scene")
                graph = recording.parent / "source-graphs" / frame
                root_path, geometry_path = graph / f"{layout}-root.json.br", graph / f"{layout}-geometry.json.br"
                require(_sha(root_path.read_bytes()) == row["sourceRootSha256"]
                        and _sha(geometry_path.read_bytes()) == row["sourceGeometrySha256"], "The preserved source graph changed")
                root = json.loads(brotli.decompress(root_path.read_bytes()))
                geometry = json.loads(brotli.decompress(geometry_path.read_bytes()))
                selected, selection = select_source(root, geometry, textures)
                require(selection == row["selection"], "The exact source column identity changed")
                row["settledTail"] = verify_settled_columns(selected)
                row["sourceOwner"] = {"scene": part["scene"], "manifest": part["manifest"],
                                      "sceneSha256": _sha(_file(author, part["scene"]).read_bytes()),
                                      "manifestSha256": _sha(_file(author, part["manifest"]).read_bytes())}
                resources = {item["id"]: item for item in manifest["resources"]}
                asset_rows = [row["floorless"], row["columns-evidence"], row["clip"]]
                for item in row["backgrounds"]:
                    require(item["oldResource"] in resources and resources[item["oldResource"]]["path"] == item["oldPath"]
                            and _sha(_file(author, item["oldPath"]).read_bytes()) == item["oldSha256"], "The new parent changed a proven background")
                    require(item["status"] in ("split", "unchanged-no-column") and item["baseline"]["matches"], "A reused background proof failed")
                    if item["status"] == "split":
                        require(item["recomposition"]["matches"], "A reused split composition failed")
                        asset_rows.append(item["floorless"])
                if frame == "iidx28":
                    require(row["prefix"]["status"] == "verified"
                            and _sha(_file(author, row["prefix"]["originalProgram"]).read_bytes()) == row["prefix"]["originalProgramSha256"],
                            "The new parent changed the pixel-checked28 source prefix")
                    row["prefix"]["sourceHandover"] = verify_28_handover(root, geometry, textures)
                row["bitmapConsumers"] = bitmap_consumers(scene, manifest, frame=frame, layout=layout,
                    resources={item["oldResource"] for item in row["backgrounds"] if item["status"] == "split"})
                for item in asset_rows:
                    data = _file(source["directory"], item["path"]).read_bytes()
                    require(_sha(data) == item["sha256"], "A completed source-proof asset changed")
                    target = _file(output, item["path"], exists=False)
                    require(not target.exists() or target.read_bytes() == data, "Two proof assets conflict")
                    target.parent.mkdir(parents=True, exist_ok=True)
                    target.write_bytes(data)
                result["frames"][frame][layout] = row
    result["status"] = "static-probe-complete"
    result["lifecycleValidation"] = {"sourceProofs": "all supplied successful RGBA and exact draw-stream receipts retained",
        "sourceGraphsScenesAndBackgrounds": "hash checked against this immutable parent",
        "runtime": "pending current-host CLI and Player; no author was generated"}
    _write(output / "reference.json", result)
    return result


def prepare_assets(reference, output, *, frame, layout, manifest):
    """Prepare proven geometry and replacements in a separate staging directory."""
    output = _independent(output, output=True)
    row = reference["frames"][frame][layout]
    require(row["status"] == "static-source-verified" and row.get("settledTail", {}).get("exactSourceStateIdentity")
            and row.get("bitmapConsumers"), "The reference lacks complete source-tail and bitmap-branch receipts")
    for root in (reference["directory"], reference["library"], reference["sourceAuthor"]):
        require(not output.is_relative_to(root) and not root.is_relative_to(output), "Column staging overlaps an immutable input")
    resources, animations, copied, aliases, prepared_textures = [], [], [], {}, {}

    def copy_data(data, relative, expected):
        require(_sha(data) == expected, "A verified column source changed")
        target = _file(output, relative, exists=False)
        if target.exists():
            require(target.read_bytes() == data, "A column content identity conflicts with an existing asset")
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
        copied.append(relative)

    for texture in row["textures"]:
        data = _file(reference["library"], texture["source"]).read_bytes()
        require(_sha(data) == texture["sha256"], "An original column source PNG changed")
        if texture["id"] in prepared_textures:
            previous = prepared_textures[texture["id"]]
            require(previous == (texture["sha256"], texture["filter"]), "Identical column texture IDs disagree on their sampler/source")
            continue
        prepared_textures[texture["id"]] = (texture["sha256"], texture["filter"])
        existing = [item for item in manifest["resources"] if item["type"] == "texture"
                    and item.get("filter", "linear") == texture["filter"]
                    and _file(output, item["path"]).stat().st_size == len(data)
                    and _sha(_file(output, item["path"]).read_bytes()) == texture["sha256"]]
        if existing:
            aliases[texture["id"]] = existing[0]["id"]
        else:
            relative = "art/lc/t" + texture["sha256"][:24] + ".png"
            copy_data(data, relative, texture["sha256"])
            aliases[texture["id"]] = texture["id"]
            resources.append({"id": texture["id"], "type": "texture", "path": relative, "filter": texture["filter"]})

    def program(clip):
        data = brotli.compress(json.dumps(clip, separators=(",", ":"), allow_nan=False).encode(), quality=7)
        identity = _sha(data)[:24]
        relative = "art/lc/c" + identity + ".json.br"
        copy_data(data, relative, _sha(data))
        definition = {"id": "lc.a" + identity, "path": relative,
                      "resources": sorted({geometry["resource"] for geometry in clip["geometries"] if geometry["resource"] is not None})}
        animations.append(definition)
        return definition

    data = _file(reference["directory"], row["clip"]["path"]).read_bytes()
    require(_sha(data) == row["clip"]["sha256"], "The verified source-column clip changed")
    clip = json.loads(brotli.decompress(data))
    for geometry in clip["geometries"]:
        if geometry["resource"] is not None:
            geometry["resource"] = aliases[geometry["resource"]]
    static = program(clip)
    backgrounds = []
    for item in row["backgrounds"]:
        require(_sha(_file(output, item["oldPath"]).read_bytes()) == item["oldSha256"],
                "The target author differs from the verified original background closure")
        if item["status"] == "unchanged-no-column":
            continue
        require(item["status"] == "split" and item["baseline"]["matches"] and item["recomposition"]["matches"],
                "A failed source redraw cannot become a column migration")
        image = item["floorless"]
        relative = "art/lc/b" + image["sha256"][:24] + ".png"
        copy_data(_file(reference["directory"], image["path"]).read_bytes(), relative, image["sha256"])
        old = next(resource for resource in manifest["resources"] if resource["id"] == item["oldResource"])
        resource = {"id": "lc.b" + image["sha256"][:24], "type": "texture", "path": relative,
                    "filter": old.get("filter", "linear")}
        resources.append(resource)
        backgrounds.append({"oldResource": item["oldResource"], "oldPath": item["oldPath"], "oldSha256": item["oldSha256"],
                            "resource": resource["id"], "path": resource["path"]})
    prefix = None
    if frame == "iidx28":
        require(row.get("prefix", {}).get("status") == "verified"
                and row["prefix"].get("sourceHandover", {}).get("allRemainingSourceStatesIdentical"),
                "The28 moving prefix and exact static handover have not passed their full source checks")
        hashes = {item["sha256"] for item in row["textures"]}
        original, source_clip, column_ids = persistent_column_clip(output, manifest, hashes)
        require(original["path"] == row["prefix"]["originalProgram"]
                and _sha(_file(output, original["path"]).read_bytes()) == row["prefix"]["originalProgramSha256"],
                "The current 28 original prefix differs from the pixel-checked reference")
        base_ids = {item["resource"] for item in source_clip["geometries"]} - column_ids
        parts, receipt = partition_recorded_runs(source_clip, column_ids, base_ids)
        prefix = {"original": copy.deepcopy(original),
                  "runs": [{"role": part["role"], "animation": program(part["clip"])} for part in parts], "receipt": receipt}
    return {"frame": frame, "layout": layout, "resources": resources, "animations": animations, "static": static,
            "backgrounds": backgrounds, "columnTextureIds": sorted(set(aliases.values())), "prefix": prefix,
            "bitmapConsumers": copy.deepcopy(row["bitmapConsumers"]), "copiedFiles": sorted(set(copied))}


def inject_split(document, manifest, *, frame, layout, assets):
    """Separate only source-owned columns; no native setting/formula is inferred."""
    from lighting_start_options import _declarations

    require(assets["frame"] == frame and assets["layout"] == layout, "Column assets belong to another Frame/layout")
    scene, result_manifest = copy.deepcopy(document), copy.deepcopy(manifest)
    owner, baked = background_owner(scene, result_manifest, frame=frame, layout=layout)
    nodes = [node for root in (scene["root"], *(item["root"] for item in scene["templates"])) for node in _walk(root)]
    identifiers = [item.get("id") for item in _declarations(scene, nodes)]
    require(all(isinstance(value, str) and value for value in identifiers) and len(identifiers) == len(set(identifiers)),
            "The scene's typed declaration identities are missing or duplicated")
    require(not any(value.startswith("lc.") for value in identifiers), "A column split is already present or conflicts with reserved identities")
    by_id = {node["id"]: node for node in _walk(owner)}
    replacements = {item["oldResource"]: item for item in assets["backgrounds"]}
    consumers = bitmap_consumers(scene, result_manifest, frame=frame, layout=layout, resources=set(replacements))
    require(consumers == assets["bitmapConsumers"], "The actual bitmap controls/branches differ from the source-checked reference")
    targets, created, index = [], [], 0

    def identifier():
        nonlocal index
        # GameplaySkinStableIdentityId requires every dot-separated segment to
        # start with a lowercase ASCII letter; a bare numeric lc.0 is invalid.
        value = "lc.n" + format(index, "x")
        index += 1
        return value

    def node(kind, *, properties=None, **fields):
        result = {"id": identifier(), "type": kind, "target": {"kind": "global"}, "blend": "inherit",
                  "properties": properties or {}, "effects": [], "children": [], **fields}
        created.append(result["id"])
        return result

    for receipt in consumers:
        leaf = by_id[receipt["node"]]
        floorless = node("sprite", resource=replacements[leaf["resource"]]["resource"])
        columns = node("recorded-animation", animation=assets["static"]["id"], properties={"z": -1})
        targets.append(columns["id"])
        # Keep the original identity, rect, colour/opacity controls, parent clip,
        # order and all preparation gates. Both new children use that same rect.
        leaf.pop("resource")
        leaf["type"] = "container"
        leaf["children"] = [floorless, columns]

    raw = [node for node in _walk(owner) if node.get("resource") in assets["columnTextureIds"]]
    require(len(raw) == ((2 if layout == "dp" else 1) if frame == "iidx33" else 0)
            and all(node["type"] == "projective-sprite" for node in raw),
            "The existing raw source-column intro owners changed")
    targets.extend(node["id"] for node in raw)
    prefix = assets["prefix"]
    if frame == "iidx28":
        require(prefix is not None and prefix["receipt"]["exactDrawStreamPreserved"], "28 requires its proven source-program partition")
        clips = [node for node in _walk(owner) if node.get("animation") == prefix["original"]["id"]]
        require(len(clips) == 1, "28 must have exactly one original base/column program consumer")
        old = clips[0]
        parent = next(node for node in _walk(owner) if old in node.get("children", []))
        require(old["properties"] == {"z": -1} and not old.get("children") and not old.get("effects")
                and all(node is old or node["properties"].get("z", 0) >= 0 for node in parent["children"]),
                "The existing28 source component depth or direct siblings changed")
        require(not any(item["target"] == old["id"] for field in ("tracks", "bindings", "variants") for item in scene[field]),
                "The existing28 source component has independent controls")
        require(old.get("clock") == "preparation.elapsed", "The existing28 prefix clock changed")
        old["type"] = "container"
        old.pop("animation")
        old.pop("clock")
        for index_value, run in enumerate(prefix["runs"]):
            child = node("recorded-animation", animation=run["animation"]["id"], clock="preparation.elapsed",
                         properties={"z": -index_value})
            old["children"].append(child)
            if run["role"] == "columns":
                targets.append(child["id"])
        result_manifest["animations"] = [item for item in result_manifest["animations"] if item["id"] != prefix["original"]["id"]]
    else:
        require(prefix is None, "Only28 owns the split moving base/column prefix")

    for field, additions in (("resources", assets["resources"]), ("animations", assets["animations"])):
        for item in additions:
            previous = [entry for entry in result_manifest.setdefault(field, []) if entry["id"] == item["id"]]
            require(not previous or previous == [item], "A prepared column manifest identity conflicts")
            if not previous:
                result_manifest[field].append(copy.deepcopy(item))
    return scene, result_manifest, {"columnTargets": targets, "createdNodes": created, "sourceBitmapConsumers": consumers,
                                   "retiredBitmapResources": [copy.deepcopy(item) for item in assets["backgrounds"]],
                                   "originalBakedResource": baked["id"], "nativeFormula": "not-inferred",
                                   "sourceGeometryAlphaMasksUvsAndAncestorMatricesPreserved": True,
                                   "originalOptionPreparationAndRegionalControlsUnchanged": True}


def attach_column_binding(document, targets, *, option_id, property_name):
    """Apply the reviewed OMS RGB adaptation without changing original alpha."""
    require(option_id == "column-brightness" and property_name == "brightness",
            "Supply the reviewed Column setting and its OMS RGB brightness channel")
    result = copy.deepcopy(document)
    nodes = {node["id"]: node for root in (result["root"], *(item["root"] for item in result["templates"])) for node in _walk(root)}
    require(targets and len(targets) == len(set(targets)) and set(targets) <= nodes.keys(), "Column targets must be actual distinct source owners")
    require(not any(property_name in nodes[target]["properties"] for target in targets), "A column target already owns this scalar property")
    require(not any(item["target"] in targets and item["property"] == property_name
                    for field in ("tracks", "bindings", "variants") for item in result[field]),
            "A column target already has a competing scalar program")
    require(not any(item["target"] in targets and item["property"] == property_name
                    for machine in result["stateMachines"] for state in machine["states"] for item in state["set"]),
            "A column target already has a competing scalar state")
    from lighting_start_options import _declarations
    ids = {item["id"] for item in _declarations(result, list(nodes.values()))}
    bindings = [{"id": "lc.b" + format(index, "x"), "target": target,
                 "property": property_name, "source": "option." + option_id} for index, target in enumerate(targets)]
    require(not any(item["id"] in ids for item in bindings), "Column scalar binding identity conflict")
    result["bindings"].extend(bindings)
    return result
