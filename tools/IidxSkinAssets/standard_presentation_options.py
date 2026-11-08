"""Split qualified Standard cabinet clips at their original lane draw boundaries.

No image is decoded or modified. Each output layer retains the original local
ancestor states, masks, geometry, camera, frame count and loop. Concatenating
its visible mesh order must reproduce the original mesh order in every frame.
The caller applies six-step RGB brightness only to lane layers. Its native
26 caller evidence is not attributed to the separately supplied 22/23/24 art.
The two 22 sources with a baked lane use disjoint subdivisions of that original
mesh, with interpolated original UVs; they never tint the complete cabinet.
"""
from __future__ import annotations

import argparse
from copy import deepcopy
import hashlib
import json
import math
from pathlib import Path
from standard_source_catalog import frame_token, frame_id, package_key, source_style, SOURCE22_STYLES, SOURCE21_STYLES
import re
from standard_source_catalog import styles as source_styles, layout_family


BRIGHTNESS_OPTION = {"id": "lane-brightness", "type": "number", "label": "轨道底纹亮度",
                     "default": 1, "min": .5, "max": 1, "step": .1}
FRAME_IDS = frozenset((3, 8, 12, 14, 16, 17, 19, 21, 25, 26))


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def digest(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def stats(clip):
    return {"animations": 1, "nodes": len(clip["nodes"]), "states": len(clip["states"]),
            "geometries": len(clip["geometries"]), "frames": len(clip["frames"]),
            "triangles": sum(len(g["triangles"]) // 3 for g in clip["geometries"]),
            "frameNodeReferences": sum(len(f["nodes"]) for f in clip["frames"]),
            "maximumLiveNodes": max((len(f["nodes"]) for f in clip["frames"]), default=0)}


def earlier_lane_roots(source, layout, style):
    """Resolve the two qualified older styles' unnamed lane GEO placements.

    These movies have no lane game binding. Their package-local sp_lane GEO
    is a leaf mesh, identified by its complete original placement path and
    shape name. Do not brighten a neighbouring lane_bg shell or its ancestor.
    """
    movie = source["sourceMovie"]
    if source["package"] != package_key(style) or layout["package"] != source["package"]:
        raise ValueError("Earlier lane geometry belongs to a different source package")
    expected = {
        "1p_frame": {"lane_1p": "sp_lane_1p"},
        "2p_frame": {"lane_2p": "sp_lane_1p" if style == 23 else "sp_lane_2p"},
        "center_frame_dp_1p": {"lane_1p": "sp_lane_1p", "lane_2p": "sp_lane_2p"},
    }
    if movie not in expected:
        raise ValueError("Earlier lane geometry is not qualified for this movie")
    result = {}
    nodes = {n["id"]: n for n in source["nodes"]}
    vertices = [{"x": x, "y": y, "z": 0} for x, y in ((0, 0), (292, 0), (0, 484), (292, 484))]
    for name, texture in expected[movie].items():
        draws = [d for d in layout["draws"] if d["texture"] == texture]
        if len(draws) != 1:
            raise ValueError("Earlier lane requires one exact package-local GEO draw")
        draw, = draws
        asset = draw["asset"]
        if (asset["package"] != source["package"] or asset["name"] != texture
                or (asset["width"], asset["height"]) != (292, 484)
                or draw["source_vertices"] != vertices or draw["game_bindings"]
                or len(draw["draw_params"]) != 1
                or draw["draw_params"][0]["region"] != texture
                or draw["draw_params"][0]["vertexes"] != [0, 1, 2, 2, 1, 3]):
            raise ValueError("Earlier lane GEO is not the qualified original lane surface")
        shape = draw["geometry"].split(", ")[0]
        candidates = []
        for n in source["nodes"]:
            if n["source"] != shape or n["geometry"] is None or n["kind"] != "mesh":
                continue
            ancestry, current = [], n
            while True:
                ancestry.append({"depth": current["depth"], "tag_id": current["sourceTag"]})
                if current["parent"] < 0:
                    break
                if current["parent"] >= current["id"]:
                    raise ValueError("Earlier lane source ancestry is not in traversal order")
                current = nodes[current["parent"]]
            path = [{"depth": item["depth"], "tag_id": item["tag_id"]} for item in draw["layer_path"]]
            if ancestry[::-1] == path:
                candidates.append(n["id"])
        if len(candidates) != 1 or any(n["parent"] == candidates[0] for n in source["nodes"]):
            raise ValueError("Earlier lane GEO must resolve to one original leaf mesh")
        result[name] = candidates[0]
    if len(set(result.values())) != len(result):
        raise ValueError("Earlier lane placements must be distinct")
    return result


def lane_roots(source, layout, *, style=None):
    """Identify original named placements, never texture resemblance or bounds."""
    result = {}
    hooks = [h for h in layout["dynamic_instances"] if h["name"] in ("lane_1p", "lane_2p")]
    if style in (23, 24):
        if hooks:
            raise ValueError("Earlier lane source unexpectedly contains named lane placements")
        return earlier_lane_roots(source, layout, style)
    if style in SOURCE22_STYLES or style in SOURCE21_STYLES:
        if hooks:
            raise ValueError("22 source unexpectedly contains named lane placements")
        if source["sourceMovie"] not in ("1p_frame", "2p_frame", "center_frame_dp_1p"):
            return {}
        if pendual_baked_surfaces(source, layout, style):
            raise ValueError("Baked 22 lane requires geometry partition, not whole-panel brightness")
        _pendual_lane_domains(source, layout, style)
        return earlier_lane_roots(source, layout, style)
    for hook in hooks:
        candidates = [n["id"] for n in source["nodes"] if
                      (n["sourceTag"], n["sourceObject"], n["depth"]) ==
                      (hook["source_tag"], hook["object_id"], hook["depth"])]
        if len(candidates) != 1 or hook["name"] in result:
            raise ValueError("Lane source placement must resolve to one original node")
        result[hook["name"]] = candidates[0]
    return result


def _pendual_lane_domains(source, layout, style):
    from standard_lane_geometry import source_lane_regions
    if source["package"] != package_key(style) or layout["package"] != source["package"]:
        raise ValueError("22 lane belongs to a different source package")
    mode = {"1p_frame": "p1", "2p_frame": "p2", "center_frame_dp_1p": "dp"}[source["sourceMovie"]]
    return source_lane_regions({"package": source["package"], "common_family": layout_family(style),
                                "layouts": {source["sourceMovie"]: layout}}, style, mode)


def pendual_baked_surfaces(source, layout, style):
    """Identify only the qualified 22 panel placements with embedded lanes.

    The local rectangle is the measured 292x484 source-plane convention also
    used by standard_lane_geometry. The native note scissor is a separate 482px
    contract. A name or a similar-looking raster alone cannot select a mesh.
    """
    movie = source["sourceMovie"]
    if style not in SOURCE22_STYLES or movie not in ("1p_frame", "2p_frame", "center_frame_dp_1p"):
        return {}
    if style != "source22-gmframe9" and not (style == "source22-gmframe8" and movie != "center_frame_dp_1p"):
        return {}
    if any(h["name"] in ("lane_1p", "lane_2p") for h in layout["dynamic_instances"]):
        raise ValueError("22 baked panel unexpectedly contains named lane placements")
    domains = _pendual_lane_domains(source, layout, style)
    result = {}
    for group, _ in enumerate(domains):
        right = group == 1 if movie == "center_frame_dp_1p" else movie == "2p_frame"
        player = "2p" if right else "1p"
        if style == "source22-gmframe8":
            texture, left = "sp_frame_" + player, 16 if right else 48
        else:
            texture, left = "frame_lane_" + player, 5 if right else 57
        draw, = (d for d in layout["draws"] if d["texture"] == texture)
        if (draw["asset"]["name"] != texture or draw["game_bindings"]
                or len(draw["draw_params"]) != 1
                or draw["draw_params"][0]["region"] != texture
                or draw["draw_params"][0]["vertexes"] != [0, 1, 2, 2, 1, 3]):
            raise ValueError("22 baked lane is not the qualified original textured quad")
        shape = draw["geometry"].split(", ")[0]
        matches = []
        for node in source["nodes"]:
            if node["source"] != shape or node["kind"] != "mesh" or node["geometry"] is None:
                continue
            chain, current = [], node
            while True:
                chain.append({"depth": current["depth"], "tag_id": current["sourceTag"]})
                if current["parent"] < 0:
                    break
                if current["parent"] >= current["id"]:
                    raise ValueError("22 panel ancestry is not in original traversal order")
                current = source["nodes"][current["parent"]]
            path = [{"depth": item["depth"], "tag_id": item["tag_id"]} for item in draw["layer_path"]]
            if chain[::-1] == path:
                matches.append(node)
        if len(matches) != 1 or any(n["parent"] == matches[0]["id"] for n in source["nodes"]):
            raise ValueError("22 baked lane must resolve to one original leaf mesh")
        node = matches[0]
        result["lane_" + player] = {"node": node["id"], "geometry": node["geometry"], "texture": texture,
                                  "rectangle": [left, 0, left + 292, 484],
                                  "sourceVertices": [[v["x"], v["y"], v["z"]] for v in draw["source_vertices"]]}
    return result


def split_geometry_rectangle(geometry, rectangle):
    """Partition every original triangle, interpolating all XYZ/UV components.

    Each half-plane intersection is calculated once for both sides. The outside
    pieces are disjoint; together with the inside they exactly cover the source
    triangle. Parent transforms, perspective, filters and masks remain upstream
    of these meshes, as in the existing recorded-animation evaluator.
    """
    points, indices = geometry["vertices"], geometry["triangles"]
    left, top, right, bottom = rectangle
    if (not left < right or not top < bottom or not indices or len(indices) % 3
            or geometry["resource"] is None or "colour" in geometry
            or any(len(v) != 5 or not all(math.isfinite(c) for c in v) for v in points)):
        raise ValueError("Baked panel partition requires finite textured triangles and a positive rectangle")
    pieces = {"frame": {"resource": geometry["resource"], "vertices": [], "triangles": []},
              "lane": {"resource": geometry["resource"], "vertices": [], "triangles": []}}
    tables = {kind: {} for kind in pieces}

    def area(a, b, c):
        return ((b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])) / 2

    def append(kind, polygon):
        target, table = pieces[kind], tables[kind]
        for index in range(1, len(polygon) - 1):
            triangle = (polygon[0], polygon[index], polygon[index + 1])
            if abs(area(*triangle)) <= 1e-12:
                continue
            for point in triangle:
                key = tuple(point)
                if key not in table:
                    table[key] = len(target["vertices"])
                    target["vertices"].append(list(point))
                target["triangles"].append(table[key])

    def cut(polygon, axis, boundary, sign):
        inside, outside = [], []
        if not polygon:
            return inside, outside
        previous = polygon[-1]
        previous_inside = sign * (previous[axis] - boundary) >= 0
        for point in polygon:
            point_inside = sign * (point[axis] - boundary) >= 0
            if point_inside != previous_inside:
                factor = (boundary - previous[axis]) / (point[axis] - previous[axis])
                intersection = [a + factor * (b - a) for a, b in zip(previous, point)]
                intersection[axis] = boundary
                inside.append(intersection)
                outside.append(intersection)
            (inside if point_inside else outside).append(point)
            previous, previous_inside = point, point_inside
        return inside, outside

    original_area = 0
    for offset in range(0, len(indices), 3):
        polygon = [points[index] for index in indices[offset:offset + 3]]
        original_area += abs(area(*polygon))
        for axis, boundary, sign in ((0, left, 1), (0, right, -1), (1, top, 1), (1, bottom, -1)):
            polygon, outside = cut(polygon, axis, boundary, sign)
            append("frame", outside)
        append("lane", polygon)
    areas = {kind: sum(abs(area(*(value["vertices"][index] for index in value["triangles"][offset:offset + 3])))
                       for offset in range(0, len(value["triangles"]), 3)) for kind, value in pieces.items()}
    if min(areas.values()) <= 0 or not math.isclose(sum(areas.values()), original_area, rel_tol=1e-12, abs_tol=1e-8):
        raise ValueError("Baked lane subdivision does not preserve the original surface area")
    return pieces, {"originalArea": original_area, "insideArea": areas["lane"], "outsideArea": areas["frame"],
                    "triangles": {kind: len(value["triangles"]) // 3 for kind, value in pieces.items()}}


def partition_baked_lane_clip(source, clip, surfaces, source_geometry, source_frame_indices):
    """Replace each qualified original leaf by adjacent outside/inside leaves.

    All source and public identities are paired before expansion. The derived
    nodes share their original parent and every original per-frame state; no
    new transform, colour, mask, timing or resource is introduced. The ordinary
    layer partitioner then verifies the full expanded draw order in every frame.
    """
    if len(source_frame_indices) != len(clip["frames"]):
        raise ValueError("Source/public frame cardinality differs")
    source_nodes = source["nodes"]
    if [n["id"] for n in source_nodes] != list(range(len(source_nodes))):
        raise ValueError("Source node IDs are not a contiguous identity table")
    if any(n["parent"] >= n["id"] or n["parent"] < -1 for n in source_nodes):
        raise ValueError("Source parent tree is not in its original traversal order")
    public_source = {}
    for original_index, frame in zip(source_frame_indices, clip["frames"]):
        hidden, visible = set(), []
        for n, s in source["frames"][original_index]["nodes"]:
            if not source["states"][s]["visible"] or source_nodes[n]["parent"] in hidden:
                hidden.add(n)
            else:
                visible.append(n)
        if len(visible) != len(frame["nodes"]):
            raise ValueError("Source/public visible-node cardinality differs")
        for n, (public, _) in zip(visible, frame["nodes"]):
            if public in public_source and public_source[public] != n:
                raise ValueError("Public recorded node has ambiguous source identity")
            public_source[public] = n
    if set(public_source) != set(range(len(clip["nodes"]))):
        raise ValueError("Recorded clip has nodes without a source-frame identity")
    targets = {entry["node"]: entry for entry in surfaces.values()}
    if len(targets) != len(surfaces):
        raise ValueError("Baked lane placements must be distinct")
    expanded_source_nodes, source_map, source_origins = [], {}, []
    for n in source_nodes:
        source_map[n["id"]] = []
        for _ in range(2 if n["id"] in targets else 1):
            identifier = len(expanded_source_nodes)
            source_map[n["id"]].append(identifier)
            expanded_source_nodes.append({**n, "id": identifier,
                                          "parent": source_map[n["parent"]][0] if n["parent"] >= 0 else -1})
            source_origins.append(n["id"])
    expanded_source = {**source, "nodes": expanded_source_nodes,
                       "frames": [{**f, "nodes": [[derived, s] for n, s in f["nodes"] for derived in source_map[n]]}
                                  for f in source["frames"]]}
    geometries, public_nodes, public_map, public_origins, geometry_origins = [], [], {}, [], []
    geometry_map, proofs = {}, []
    for public, node in enumerate(clip["nodes"]):
        target = targets.get(public_source[public])
        original_geometry = node["geometry"]
        kinds = ("frame", "lane") if target else (None,)
        for kind in kinds:
            geometry_index = -1
            if original_geometry >= 0:
                key = (original_geometry, tuple(target["rectangle"]) if target else None, kind)
                if key not in geometry_map:
                    geometry = clip["geometries"][original_geometry]
                    if target:
                        original = source_geometry["geometries"][target["geometry"]]
                        if (original["source"] != source_nodes[public_source[public]]["source"]
                                or original["vertices"] != target["sourceVertices"] or original["colours"]
                                or len(original["draws"]) != 1):
                            raise ValueError("Baked panel source geometry no longer matches its qualified leaf")
                        draw, = original["draws"]
                        if (draw["texture"] is None or draw["sourceColour"] is not None
                                or not draw["flags"] & 1 or draw["indices"] != [0, 1, 2, 2, 1, 3]
                                or geometry["triangles"] != draw["indices"]
                                or geometry["vertices"] != [[*v, *uv] for v, uv in zip(original["vertices"], draw["uv"])]):
                            raise ValueError("Baked panel recorded XYZ/UV/triangles differ from the source geometry")
                        pieces, proof = split_geometry_rectangle(geometry, target["rectangle"])
                        geometry = pieces[kind]
                        if kind == "lane":
                            proofs.append({"sourceNode": public_source[public], "publicNode": public,
                                           "sourceGeometry": target["geometry"], "publicGeometry": original_geometry,
                                           "rectangle": target["rectangle"], **proof})
                    geometry_map[key] = len(geometries)
                    geometries.append(deepcopy(geometry))
                    geometry_origins.append(original_geometry)
                geometry_index = geometry_map[key]
            elif target:
                raise ValueError("Baked lane unexpectedly became a public container")
            public_map.setdefault(public, []).append(len(public_nodes))
            public_nodes.append({**node, "parent": public_map[node["parent"]][0] if node["parent"] >= 0 else -1,
                                 "geometry": geometry_index})
            public_origins.append(public)
    expanded = {**clip, "nodes": public_nodes, "geometries": geometries,
                "frames": [{**frame, "nodes": [[derived, s] for n, s in frame["nodes"] for derived in public_map[n]]}
                           for frame in clip["frames"]]}
    roots = {name: source_map[item["node"]][1] for name, item in surfaces.items()}
    layers = partition_clip(expanded_source, expanded, roots, source_frame_indices)
    for layer in layers:
        layer["subdivisionNodeIds"] = layer["originalNodeIds"]
        layer["originalNodeIds"] = [public_origins[n] for n in layer["subdivisionNodeIds"]]
        layer["sourceNodeIds"] = [source_origins[n] for n in layer["sourceNodeIds"]]
        layer["originalGeometryIds"] = [geometry_origins[g] for g in layer["originalGeometryIds"]]
    return layers, {"method": "Disjoint original-triangle half-plane subdivision; original XYZ/UV interpolation",
                    "rectangles": surfaces, "partitions": proofs,
                    "originalParentsStatesMasksCamerasAndTimingPreserved": True,
                    "originalLeafOrderPreservedWithAdjacentPieces": True,
                    "noPngDecodeOrPixelModification": True}


def partition_clip(source, clip, roots, source_frame_indices):
    """Return ordered lossless clip layers plus private source identity receipts.

    Source and public nodes are paired in their evaluator order. Every state,
    parent relationship and source identity is checked before a layer is made.
    This retains recorded mesh data directly rather than reprojecting it.
    """
    if len(source_frame_indices) != len(clip["frames"]):
        raise ValueError("Source/public frame cardinality differs")
    if not roots or len(set(roots.values())) != len(roots):
        raise ValueError("Split requires distinct original lane roots")
    source_nodes, public_nodes = source["nodes"], clip["nodes"]
    if [n["id"] for n in source_nodes] != list(range(len(source_nodes))):
        raise ValueError("Source node IDs are not a contiguous identity table")
    if any(n["parent"] >= n["id"] or n["parent"] < -1 for n in source_nodes):
        raise ValueError("Source parent tree is not in its original traversal order")
    root_ids = set(roots.values())
    order = None
    for frame in source["frames"]:
        found = [n for n, _ in frame["nodes"] if n in root_ids]
        if found:
            if set(found) != root_ids or (order is not None and order != found):
                raise ValueError("Lane roots disappear or exchange draw order; explicit source handling required")
            order = found
    if order is None:
        raise ValueError("Lane source roots never occur")
    root_order = {identifier: i for i, identifier in enumerate(order)}
    owner = {}
    for n in source_nodes:
        owner[n["id"]] = n["id"] if n["id"] in root_ids else owner.get(n["parent"])
    public_source, frame_mesh_layers, all_layers = {}, [], set()
    for original_index, runtime_frame in zip(source_frame_indices, clip["frames"]):
        original = source["frames"][original_index]
        hidden, visible, frame_map, passed = set(), [], {}, 0
        for source_id, state_id in original["nodes"]:
            if source_id in root_order:
                passed = root_order[source_id] + 1
            n, s = source_nodes[source_id], source["states"][state_id]
            if not s["visible"] or n["parent"] in hidden:
                hidden.add(source_id)
                continue
            layer = 2 * root_order[owner[source_id]] + 1 if owner[source_id] is not None else 2 * passed
            visible.append((source_id, state_id, layer))
        if len(visible) != len(runtime_frame["nodes"]):
            raise ValueError("Source/public visible-node cardinality differs")
        assignments = {}
        for (source_id, state_id, layer), (public_id, public_state_id) in zip(visible, runtime_frame["nodes"]):
            sn, ss = source_nodes[source_id], source["states"][state_id]
            pn, ps = public_nodes[public_id], clip["states"][public_state_id]
            expected = {"matrix": ss["matrix"], "origin": ss["origin"],
                        "projection": {0: "affine", 1: "affine", 2: "perspective"}[ss["projection"]],
                        "multiply": ss["mult"], "add": ss["add"], "hsl": ss["hsl"], "mask": ss["mask"]}
            if any(ps[k] != value for k, value in expected.items()):
                raise ValueError("Source/public transform, colour or mask differs")
            if pn["parent"] != (frame_map[sn["parent"]] if sn["parent"] >= 0 else -1):
                raise ValueError("Source/public hierarchy differs")
            if public_id in public_source and public_source[public_id] != source_id:
                raise ValueError("Public recorded node has ambiguous source identity")
            public_source[public_id], frame_map[source_id] = source_id, public_id
            if pn["geometry"] >= 0:
                if sn["geometry"] is None:
                    raise ValueError("Source container unexpectedly acquired render geometry")
                assignments[public_id] = layer
                all_layers.add(layer)
        frame_mesh_layers.append(assignments)

    # Empty frame layers can be omitted. Adjacent lane layers can share one
    # brightness node only when no original frame mesh intervenes in any frame.
    layer_groups = []
    for layer in sorted(all_layers):
        if layer % 2 and layer_groups and all(i % 2 for i in layer_groups[-1]):
            layer_groups[-1].append(layer)
        else:
            layer_groups.append([layer])
    if not layer_groups:
        raise ValueError("Source lane split contains no visible geometry")

    results = []
    for group in layer_groups:
        selected_frames, used_nodes, used_states, used_geometry = [], set(), set(), set()
        for frame, assignments in zip(clip["frames"], frame_mesh_layers):
            selected = {n for n, layer in assignments.items() if layer in group}
            for n in tuple(selected):
                parent = public_nodes[n]["parent"]
                while parent >= 0:
                    if public_nodes[parent]["geometry"] >= 0:
                        raise ValueError("Mesh ancestor cannot be duplicated into another draw layer")
                    selected.add(parent)
                    parent = public_nodes[parent]["parent"]
            entries = [[n, s] for n, s in frame["nodes"] if n in selected]
            selected_frames.append({"nodes": entries, "camera": deepcopy(frame["camera"])})
            used_nodes.update(n for n, _ in entries)
            used_states.update(s for _, s in entries)
            used_geometry.update(public_nodes[n]["geometry"] for n in selected if public_nodes[n]["geometry"] >= 0)
        nodes = {n: i for i, n in enumerate(sorted(used_nodes))}
        states = {s: i for i, s in enumerate(sorted(used_states))}
        geometry = {g: i for i, g in enumerate(sorted(used_geometry))}
        projected = {k: deepcopy(v) for k, v in clip.items() if k not in ("nodes", "states", "geometries", "frames")}
        projected["nodes"] = [{**deepcopy(public_nodes[n]),
                               "parent": nodes[public_nodes[n]["parent"]] if public_nodes[n]["parent"] >= 0 else -1,
                               "geometry": geometry[public_nodes[n]["geometry"]] if public_nodes[n]["geometry"] >= 0 else -1}
                              for n in nodes]
        projected["states"] = [deepcopy(clip["states"][s]) for s in states]
        projected["geometries"] = [deepcopy(clip["geometries"][g]) for g in geometry]
        projected["frames"] = [{"nodes": [[nodes[n], states[s]] for n, s in f["nodes"]], "camera": f["camera"]}
                               for f in selected_frames]
        results.append({"kind": "lane" if group[0] % 2 else "frame", "clip": projected,
                        "sourceLayers": group, "originalNodeIds": list(nodes),
                        "sourceNodeIds": [public_source[n] for n in nodes],
                        "originalGeometryIds": list(geometry)})

    for frame_index, frame in enumerate(clip["frames"]):
        expected = [n for n, _ in frame["nodes"] if public_nodes[n]["geometry"] >= 0]
        actual = [layer["originalNodeIds"][n] for layer in results
                  for n, _ in layer["clip"]["frames"][frame_index]["nodes"]
                  if layer["clip"]["nodes"][n]["geometry"] >= 0]
        if actual != expected:
            raise ValueError("Split would reorder original visible mesh drawing")
    return results


def export_lane_layers(library, research, style, movie, output):
    """Export an ordered layer list compatible with existing frame branches."""
    import brotli
    style = source_style(style)
    library = Path(library).resolve(strict=True)
    research = Path(research).resolve(strict=True)
    output = Path(output).resolve()
    inventory = read(library / "library.json")
    if style not in source_styles(inventory) or not re.fullmatch(r"[a-z0-9_]+", movie):
        raise ValueError("Use a qualified Standard movie/style")
    if output.is_relative_to(library) or output.is_relative_to(research):
        raise ValueError("Layer output must not modify existing source research")
    if inventory["status"] != "complete" or inventory["sourceMetadataUnchanged"] is not True:
        raise ValueError("Use the independently verified Standard library")
    cabinet = research / package_key(style)
    reference = read(cabinet / "skin-reference-map.json")
    folder = cabinet / "recorded" / movie
    original = read(folder / "export.json")
    if (reference["package"] != package_key(style) or reference["common_family"] != layout_family(style)
            or original["source"]["package"] != reference["package"] or original["source"]["movie"] != movie):
        raise ValueError("Frame export does not match the selected Standard source")
    source = read(folder / "source-root.json")
    layout = reference["layouts"][movie]
    baked = pendual_baked_surfaces(source, layout, style)
    roots = ({name: entry["node"] for name, entry in baked.items()} if baked
             else lane_roots(source, layout, style=style))
    source_geometry = read(folder / "source-geometry.json") if baked else None
    destination = output / package_key(style) / movie
    destination.mkdir(parents=True, exist_ok=False)
    result = {"contract": "oms-standard-lane-layers.v1", "source": original["source"],
              "options": [BRIGHTNESS_OPTION] if roots else [],
              "receipt": {"sourceExport": str(folder / "export.json"), "sourceExportSHA256": digest(folder / "export.json"),
                          "sourceRootSHA256": digest(folder / "source-root.json"), "laneRoots": roots,
                          "pngBytesPreserved": True, "perFrameDrawOrderPreserved": True,
                          "limits": "Source layers only; final public CLI and actual Player budgets/visuals are separate checks."}}
    if style in SOURCE21_STYLES:
        result["receipt"]["laneIdentification"] = "Exact original21 package-local unnamed sp_lane GEO and complete placement path"
        result["receipt"]["brightnessEvidence"] = (
            "Original21 meshes and source pixels; existing OMS six-step brightness author option. "
            "The 26 colour caller and multiplier RVAs are not evidence for21. "
            "The original source-plane domain remains292x484; only the separate21 cover-number clip is qualified at482px. "
            "Native21 note-coordinate parity is not asserted.")
    elif style in SOURCE22_STYLES:
        result["receipt"]["laneIdentification"] = ("Exact package-local baked panel GEO and complete placement path; disjoint lane subdivision"
                                                   if baked else "Exact package-local unnamed sp_lane GEO and complete placement path")
        result["receipt"]["brightnessEvidence"] = (
            "22 original meshes; existing OMS six-step brightness author option. "
            "The 26 native colour caller and multiplier RVAs are not evidence for this source. "
            "The source-plane domain remains 292x484; the independently audited 22 native note scissor is 482px.")
        if baked:
            result["receipt"]["sourceGeometrySHA256"] = digest(folder / "source-geometry.json")
    elif style in (23, 24):
        result["receipt"]["laneIdentification"] = "Exact package-local sp_lane GEO and full source placement path"
        result["receipt"]["brightnessEvidence"] = (
            "23/24 source mesh separation; existing OMS six-step brightness author option. "
            "The 26 native colour caller and multiplier RVAs are not evidence for these earlier sources.")
    else:
        result["receipt"].update(nativeColourCallerRVA="0x349fb0", nativeMultiplierRVA="0x16ada0")
    for mode in ("motion", "steady"):
        branch = original[mode]
        clip_file = folder / f"{mode}.clip.json.br"
        clip_receipt = next(f for f in branch["files"] if f["path"] == branch["animations"][0]["path"])
        if digest(clip_file) != clip_receipt["sha256"]:
            raise ValueError("Original recorded clip changed after source export")
        clip = json.loads(brotli.decompress(clip_file.read_bytes()))
        frame_indices = list(range(len(source["frames"]))) if mode == "motion" else [original["source"]["steadyFrameZeroBased"], len(source["frames"]) - 1]
        if not roots:
            result[mode] = {"layers": [{"kind": "frame", **deepcopy(branch), "bindings": []}],
                            "receipt": {"before": stats(clip), "after": stats(clip), "delta": {k: 0 for k in stats(clip)}}}
            continue
        subdivision = None
        if baked:
            layers, subdivision = partition_baked_lane_clip(source, clip, baked, source_geometry, frame_indices)
        else:
            layers = partition_clip(source, clip, roots, frame_indices)
        result[mode] = {"layers": []}
        totals = {key: 0 for key in stats(clip)}
        for i, layer in enumerate(layers):
            value = layer["clip"]
            encoded = json.dumps(value, ensure_ascii=False, separators=(",", ":"), allow_nan=False).encode("utf-8")
            compressed = brotli.compress(encoded, quality=5)
            if len(compressed) > 16 * 1024 * 1024 or len(encoded) > 128 * 1024 * 1024:
                raise ValueError("Split source layer exceeds encoded/decoded clip byte budget")
            name = f"{mode}.layer{i:02}.clip.json.br"
            path = destination / name
            path.write_bytes(compressed)
            archive = f"standard/lane-layers/{frame_id(style)}/{movie}/{name}"
            prefix = branch["node"]["id"].removesuffix(".node") + f".layer{i:02}"
            node = deepcopy(branch["node"])
            node["id"], node["animation"] = prefix + ".node", prefix + ".clip"
            used = {g["resource"] for g in value["geometries"] if g["resource"] is not None}
            resources = [deepcopy(r) for r in branch["resources"] if r["id"] in used]
            if {r["id"] for r in resources} != used:
                raise ValueError("Source layer resource closure is incomplete")
            paths = {r["path"] for r in resources}
            files = []
            for item in branch["files"]:
                if item["path"] not in paths:
                    continue
                file = Path(item["source"]).resolve(strict=True)
                if not file.is_relative_to(library) or digest(file) != item["sha256"]:
                    raise ValueError("Original layer texture leaves verified Standard source or has changed")
                files.append(deepcopy(item))
            if {f["path"] for f in files} != paths:
                raise ValueError("Source layer file closure is incomplete")
            files.append({"source": str(path), "path": archive, "sha256": digest(path)})
            bindings = []
            if layer["kind"] == "lane":
                bindings.append({"id": prefix + ".brightness", "target": node["id"],
                                 "property": "brightness", "source": "option.lane-brightness"})
            counts = stats(value)
            for key, count in counts.items():
                totals[key] += count
            result[mode]["layers"].append({"kind": layer["kind"], "node": node, "resources": resources,
                "animations": [{"id": node["animation"], "path": archive, "resources": sorted(used)}],
                "files": files, "bindings": bindings, "tracks": [],
                "receipt": {**{k: v for k, v in layer.items() if k != "clip"}, "counts": counts,
                            "encodedBytes": len(compressed), "decodedBytes": len(encoded)}})
        before = stats(clip)
        result[mode]["receipt"] = {"before": before, "after": totals,
                                  "delta": {k: totals[k] - before[k] for k in before},
                                  "uniqueResources": len({r["id"] for layer in result[mode]["layers"] for r in layer["resources"]}),
                                  "sourceFramesUnchanged": True}
        if subdivision:
            result[mode]["receipt"]["geometrySubdivision"] = subdivision
    (destination / "export.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("library", "research", "output"):
        parser.add_argument("--" + name, type=Path, required=True)
    parser.add_argument("--styles", type=source_style, nargs="+", required=True)
    parser.add_argument("--movies", nargs="+", default=["1p_frame", "2p_frame", "center_frame_dp_1p"])
    args = parser.parse_args()
    for style in args.styles:
        for movie in args.movies:
            exported = export_lane_layers(args.library, args.research, style, movie, args.output)
            print(json.dumps({"style": style, "movie": movie,
                              **{mode: exported[mode]["receipt"] for mode in ("motion", "steady")}}, ensure_ascii=False), flush=True)
