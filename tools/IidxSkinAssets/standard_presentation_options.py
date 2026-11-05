"""Split qualified 26 cabinet clips at their original lane draw boundaries.

No image is decoded or modified. Each output layer retains the original local
ancestor states, masks, geometry, camera, frame count and loop. Concatenating
its visible mesh order must reproduce the original mesh order in every frame.
The caller applies the native six-step RGB brightness only to lane layers.
"""
from __future__ import annotations

import argparse
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import re


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


def lane_roots(source, layout):
    """Identify original named placements, never texture resemblance or bounds."""
    result = {}
    hooks = [h for h in layout["dynamic_instances"] if h["name"] in ("lane_1p", "lane_2p")]
    for hook in hooks:
        candidates = [n["id"] for n in source["nodes"] if
                      (n["sourceTag"], n["sourceObject"], n["depth"]) ==
                      (hook["source_tag"], hook["object_id"], hook["depth"])]
        if len(candidates) != 1 or hook["name"] in result:
            raise ValueError("Lane source placement must resolve to one original node")
        result[hook["name"]] = candidates[0]
    return result


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
    library = Path(library).resolve(strict=True)
    research = Path(research).resolve(strict=True)
    output = Path(output).resolve()
    if style not in FRAME_IDS or not re.fullmatch(r"[a-z0-9_]+", movie):
        raise ValueError("Use a qualified Standard movie/style")
    if output.is_relative_to(library) or output.is_relative_to(research):
        raise ValueError("Layer output must not modify existing source research")
    inventory = read(library / "library.json")
    if inventory["status"] != "complete" or inventory["sourceMetadataUnchanged"] is not True:
        raise ValueError("Use the independently verified 26 library")
    cabinet = research / f"gmframe{style:02}"
    reference = read(cabinet / "skin-reference-map.json")
    folder = cabinet / "recorded" / movie
    original = read(folder / "export.json")
    if (reference["package"] != f"gmframe{style:02}" or reference["common_family"] != "0"
            or original["source"]["package"] != reference["package"] or original["source"]["movie"] != movie):
        raise ValueError("Frame export does not match the selected 26 source")
    source = read(folder / "source-root.json")
    roots = lane_roots(source, reference["layouts"][movie])
    destination = output / f"gmframe{style:02}" / movie
    destination.mkdir(parents=True, exist_ok=False)
    result = {"contract": "oms-standard-lane-layers.v1", "source": original["source"],
              "options": [BRIGHTNESS_OPTION] if roots else [],
              "receipt": {"sourceExport": str(folder / "export.json"), "sourceExportSHA256": digest(folder / "export.json"),
                          "sourceRootSHA256": digest(folder / "source-root.json"), "laneRoots": roots,
                          "nativeColourCallerRVA": "0x349fb0", "nativeMultiplierRVA": "0x16ada0",
                          "pngBytesPreserved": True, "perFrameDrawOrderPreserved": True,
                          "limits": "Source layers only; final public CLI and actual Player budgets/visuals are separate checks."}}
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
            archive = f"standard/lane-layers/iidx{style:02}/{movie}/{name}"
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
                    raise ValueError("Original layer texture leaves verified 26 source or has changed")
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
    (destination / "export.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("library", "research", "output"):
        parser.add_argument("--" + name, type=Path, required=True)
    parser.add_argument("--styles", type=int, nargs="+", required=True)
    parser.add_argument("--movies", nargs="+", default=["1p_frame", "2p_frame", "center_frame_dp_1p"])
    args = parser.parse_args()
    for style in args.styles:
        for movie in args.movies:
            exported = export_lane_layers(args.library, args.research, style, movie, args.output)
            print(json.dumps({"style": style, "movie": movie,
                              **{mode: exported[mode]["receipt"] for mode in ("motion", "steady")}}, ensure_ascii=False), flush=True)
