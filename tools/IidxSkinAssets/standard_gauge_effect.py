"""Export the original26 per-cabinet Gauge-rise movies as public programs.

The two programs share their complete source states, geometry and PNG resources;
only bounded playback differs. Native3493f0 restarts on a displayed Gauge rise,
then repeats the original movie while the current clear threshold is met.
"""
from __future__ import annotations

import argparse
import copy
import json
from pathlib import Path
from standard_source_catalog import frame_token, frame_id, package_key, source_style, SOURCE22_STYLES

from build_fullcombo_catalog import world_matrix
from build_standard_skin import sha
from standard_components import StandardComponents
from standard_source_catalog import metadata, styles as source_styles


STYLES = (3, 8, 12, 14, 16, 17, 19, 21, 25, 26)
MOVIES = {"p1": "gauge_up_1p", "p2": "gauge_up_2p", "dp": "gauge_up_dp"}


def repeating_program(once):
    """Change playback only; retain the mandatory empty terminal frame."""
    if once["loop"] or once["trigger"] != "gauge.increase" or "heldLoop" in once or "releaseFrame" in once:
        raise ValueError("Gauge source must be an ordinary increase-triggered one-shot")
    if len(once["frames"]) < 2 or once["frames"][-1]["nodes"]:
        raise ValueError("Recorded Gauge source must retain its exact terminal boundary")
    result = copy.deepcopy(once)
    result["loop"] = True
    return result


def geometric_bounds(clip):
    """Source affine vertex extent, explicitly before masks and alpha coverage."""
    points = []
    identity = [[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]]
    for frame in clip["frames"]:
        worlds = {}
        for node_index, state_index in frame["nodes"]:
            placed, state = clip["nodes"][node_index], clip["states"][state_index]
            if state["projection"] != "affine":
                raise ValueError("Gauge extent needs a qualified perspective projection")
            world = world_matrix(state["matrix"], state["origin"],
                                 identity if placed["parent"] == -1 else worlds[placed["parent"]])
            worlds[node_index] = world
            if placed["geometry"] < 0:
                continue
            for vertex in clip["geometries"][placed["geometry"]]["vertices"]:
                xyz = [*vertex[:3], 1]
                points.append([sum(xyz[k] * world[k][axis] for k in range(4)) for axis in (0, 1)])
    if not points:
        raise ValueError("Gauge source has no drawable mesh")
    return [min(p[0] for p in points), min(p[1] for p in points),
            max(p[0] for p in points), max(p[1] for p in points)]


def export_gauge_feedback(library, output, bemani_root, image_tools, styles=None):
    import brotli
    qualified = source_styles(metadata(library))
    styles = qualified if styles is None else styles
    if any(style not in qualified for style in styles) or len(set(styles)) != len(styles):
        raise ValueError("Use each qualified26 cabinet at most once")
    exporter = StandardComponents(library, output, bemani_root, image_tools)
    frames, summary = {}, []
    for style in styles:
        layouts = frames[f"f{frame_token(style)}"] = {}
        for layout, movie in MOVIES.items():
            prefix = f"sg.f{frame_token(style)}.{layout}"
            original = exporter.clip(package_key(style), movie, prefix, loop=False, trigger="gauge.increase")
            if original["source"]["stage"] != [0, 0, 1280, 720] or original["source"]["fps"] != 60 or original["source"]["sourceFrames"] != 60:
                raise ValueError("Gauge source differs from its independently qualified canvas/timing")
            path = output / original["animations"][0]["path"]
            once = json.loads(brotli.decompress(path.read_bytes()))
            repeated = repeating_program(once)
            relative = f"custom/{prefix}/loop.json.br"
            target = output / relative
            target.write_bytes(brotli.compress(json.dumps(repeated, separators=(",", ":"), allow_nan=False).encode(), quality=5))
            exporter.files[relative] = {"path": relative, "sha256": sha(target), "bytes": target.stat().st_size}
            loop_id = prefix + ".loop.clip"
            loop_node = copy.deepcopy(original["node"])
            loop_node.update(id=prefix + ".loop", animation=loop_id)
            declaration = {**original["animations"][0], "id": loop_id, "path": relative}
            layouts[layout] = {"oneShot": original["node"], "loop": loop_node,
                "resources": original["resources"], "animations": [*original["animations"], declaration],
                "source": original["source"], "receipt": original["receipt"]}
            summary.append({"style": style, "layout": layout, "frames": 60, "fps": 60,
                "canvas": [1280, 720], "geometricBoundsBeforeMaskAndAlpha": geometric_bounds(once),
                "resources": len(original["resources"]), "animations": 2,
                "geometry": len(once["geometries"]), "nodes": len(once["nodes"]),
                "states": len(once["states"]), "triangleDefinitions": sum(len(g["triangles"]) // 3 for g in once["geometries"]),
                "sameOriginalFrames": once["frames"] == repeated["frames"],
                "sameOriginalGeometry": once["geometries"] == repeated["geometries"]})
    result = {"contract": "oms-standard-gauge-feedback.v1", "librarySHA256": exporter.source_library_sha,
        "frames": frames, "files": list(exporter.files.values()), "summary": summary,
        "receipt": {"source": "Qualified selected Standard gmframe gauge_up_1p/2p/dp; exact styles and source archive identities are retained per clip",
            "nativeCaller": "26 reference only:348423 loads selected cabinet clips;3493f0 restarts on original two-percent display increase or at/above native clear threshold after clip end. Earlier source movies use this OMS event adaptation; their executable call sites are not inferred.",
            "displayModes": "26 reference only:34a800 unconditionally calls3493f0 independently of34a130/3494e0 display mode. The OMS author retains that A/B/C behaviour for its selected source art.",
            "originalGeometry": "Complete original1280x720 hierarchy, meshes, masks, colours and60 source frames retained. No guessed per-cabinet offsets.",
            "projectionLimit": "Geometric summary bounds ignore mask/alpha coverage; they are not a replacement crop.",
            "lifecycleAdaptation": "One-shot trigger uses the real OMS Gauge-increase snapshot. Clear-loop initial origin and clear-state gating belong to the host author; no synthetic increase is emitted by this export.",
            "sourcePNGBytesPreserved": True}}
    (output / "gauge-feedback.json").write_text(json.dumps(result, ensure_ascii=False, separators=(",", ":")) + "\n", "utf-8")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    for field in ("library", "output", "bemani-root", "image-tools"):
        parser.add_argument("--" + field, type=Path, required=True)
    parser.add_argument("--styles", type=source_style, nargs="+")
    args = parser.parse_args()
    result = export_gauge_feedback(args.library, args.output, args.bemani_root, args.image_tools, args.styles)
    print(json.dumps({"output": str(args.output), "movies": len(result["summary"]), "files": len(result["files"])}))
