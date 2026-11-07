"""Original cover/Lift surfaces on native position and clipping primitives.

Complete verified animations use lossless regular grids with exact source-frame
membership. Each stable group remains a real native Slider/HiddenCover/LiftCover,
so the host still owns cover movement and clipping.
"""

from __future__ import annotations

import math
from pathlib import Path

from beatoraja_author import layout_for, material_bindings, material_templates, walk_nodes
from beatoraja_cover_atlas import cover_atlas
from beatoraja_native import Lua, colour, native_colour, native_rect, node_rect, scene_properties


def group_baked_runs(index):
    """Group a complete bake without losing run order, blend, crop or offset.

    Retained for the bake pipeline's static-crop organization metrics. Runtime
    cover/Graph exporters use cover_atlas instead. The returned draw callbacks
    depend on the caller's existing ANIMATION_RUNTIME ``animation_frame`` helper.
    Source identity/hashes are verified by read_bakes; atlas hashes are checked
    only by AssetSink.add_baked_page when the consumer copies a used page.
    """
    if (index.get("contract") != "oms-iidx-baked-animation.v1"
            or not index.get("verification", {}).get("passed") or not index.get("selectionComplete")):
        raise ValueError("Animation groups require a complete verified bake")
    source_count = index["sourceFrameCount"]
    frames = index["frames"]
    if [frame["sourceFrame"] for frame in frames] != list(range(source_count)):
        raise ValueError("Complete bake does not cover the entire source frame sequence")
    if index.get("heldLoop") or index.get("trigger") != "gameplay.attach":
        raise ValueError("Cover animation requires its gameplay.attach clock, not a partial event/input sequence")
    fps = index["framesPerSecond"]
    if not math.isfinite(fps) or fps <= 0:
        raise ValueError("Invalid source animation frame rate")
    count = source_count - int(bool(frames[-1].get("terminal")))
    loop_start = index.get("loopStartFrame", 0) if index["loop"] else -1
    if count <= 0 or index["loop"] and not 0 <= loop_start < count:
        raise ValueError("Invalid source animation loop bounds")
    # GameplaySkinRecordedAnimationProgram.FrameAt plays the whole prefix from
    # zero once. loopStartFrame affects only the wrap after the first duration.
    clock = f"animation_frame(0,{fps},{count},{loop_start},0)"
    groups = {}
    for frame in frames:
        if frame.get("terminal") and frame["runs"]:
            raise ValueError("A terminal cover frame must retain its empty source output")
        previous = -1
        for run in frame["runs"]:
            if run["order"] <= previous or run["blend"] not in ("normal", "additive"):
                raise ValueError("Baked run ordering/blend is not qualified")
            previous = run["order"]
            key = (run["order"], run["page"], *run["rect"], *run["offset"], run["blend"])
            group = groups.setdefault(key, {"order": run["order"], "page": run["page"], "rect": list(run["rect"]),
                                            "offset": list(run["offset"]), "blend": run["blend"], "frames": []})
            group["frames"].append(frame["sourceFrame"])
    result = []
    for key in sorted(groups):
        group = groups[key]
        # Contiguous membership is expressed as ranges, keeping repeated static
        # background crops small without changing which frames contain them.
        ranges = []
        for frame in group["frames"]:
            if ranges and frame == ranges[-1][1] + 1:
                ranges[-1][1] = frame
            else:
                ranges.append([frame, frame])
        condition = " or ".join(f"frame == {a}" if a == b else f"(frame >= {a} and frame <= {b})" for a, b in ranges)
        group["draw"] = Lua("function() local frame=" + clock + ";return " + condition + " end")
        result.append(group)
    return result


def export_animated_graph_area(author, options, keymode, presentation, sink, canvas, baked):
    """Emit only animated Graph Area roots, in the caller's Graph art layer.

    Frame decoration markers select the exact original global panel and clip.
    Static Graph Area remains with export_graph; returned handled IDs let the
    caller replace that pass's generic recorded-template gap without double art.
    """
    result = {"image": [], "destination": [], "gaps": [], "handled_template_ids": [], "handled_root_ids": []}
    if "graph-area" not in author.modules:
        return result
    x0, y0, width, height = canvas
    if x0 != 0 or y0 != 0 or min(width, height) <= 0:
        raise ValueError("Graph Area requires a full positive zero-origin canvas")
    contribution = author.contribution("graph-area", options)
    frame = author.contribution("frame", options)
    markers = {item["resource"] for item in material_bindings(frame, keymode, presentation)
               if item["target"]["kind"] == "global" and item["slot"] == "decoration" and item["mode"] == "Provide"}
    selected = {item["template"]: contribution.templates[item["template"]]
                for item in contribution.scene.get("instances", [])
                if item.get("material", {}).get("slot") == "decoration" and item["material"]["resource"] in markers}
    context = {"layout.ruleset": "bms", "layout.presentation": presentation,
               **{"option." + key: value for key, value in options.items()}}
    grouped = {}

    def visit(node, parent, inherited, clip):
        properties, dynamic = scene_properties(node, contribution.scene, context)
        if properties.get("visible", True) is False:
            return
        if dynamic or node.get("effects") or any(t["target"] == node["id"] for t in contribution.scene.get("tracks", [])):
            raise ValueError("Animated Graph Area needs a qualified dynamic/effect/track adapter")
        if (properties.get("rotation", 0) % 360 or properties.get("scale-x", 1) != 1
                or properties.get("scale-y", 1) != 1 or node.get("blend", "inherit") not in ("inherit", "alpha")):
            raise ValueError("Animated Graph Area transform or whole-animation blend requires a bake")
        rect, rgba = node_rect(properties, parent), colour(properties, inherited)
        if node["type"] == "clip":
            if properties.get("clip-mode", "bounds") != "bounds":
                raise ValueError("Animated Graph Area requires its original bounds clip")
            if clip is None:
                clip = rect
            else:
                left, top = max(clip[0], rect[0]), max(clip[1], rect[1])
                right, bottom = min(clip[0] + clip[2], rect[0] + rect[2]), min(clip[1] + clip[3], rect[1] + rect[3])
                clip = (left, top, right - left, bottom - top)
            if min(clip[2:]) <= 0:
                return
        elif node["type"] == "recorded-animation":
            key = (contribution.manifest_path, node["animation"])
            details = {"manifest": key[0], "animation": key[1], "node": node["id"]}
            if node.get("clock"):
                result["gaps"].append({"code": "graph-area-animation-clock-unmapped", "clock": node["clock"], **details})
                return
            if key not in baked or not baked[key][1].get("selectionComplete"):
                result["gaps"].append({"code": "graph-area-animation-needs-complete-bake", **details})
                return
            path, index = baked[key]
            if (Path(index["source"]["authorRoot"]).resolve() != author.root
                    or (index["source"]["manifest"], index["source"]["animation"]) != key):
                raise ValueError("Graph Area bake identity does not match the selected author/material")
            if key not in grouped:
                grouped[key] = cover_atlas(sink, path, index)
            groups = grouped[key]
            used_pages = {group.source for group in groups}
            cw, ch = index["canvas"]
            x, y, w, h = rect
            for number, group in enumerate(groups):
                identifier = f"{author.family}.graph-area.{node['id']}.run{number}"
                result["image"].append({"id": identifier, **group.image()})
                ox, oy = group.offset
                rw, rh = group.size
                geometry = native_rect((x + ox * w / cw, y + oy * h / ch, rw * w / cw, rh * h / ch), height)
                if clip is not None:
                    geometry.update({"clip_" + key: value for key, value in native_rect(clip, height).items()})
                result["destination"].append({"id": identifier, "filter": 0, "blend": 2 if group.blend == "additive" else 1,
                                               "draw": group.draw, "dst": [{**geometry, **native_colour(rgba)}]})
            result["gaps"].append({"code": "native-graph-area-animation-objects", "informational": True,
                                   "objects": len(groups), "pages": len(used_pages), "sourceFrames": index["sourceFrameCount"],
                                   "maximumActiveRuns": max(len(frame["runs"]) for frame in index["frames"]), **details})
        elif node["type"] != "container":
            raise ValueError("Animated Graph Area contains an unhandled source primitive")
        for child in sorted(node.get("children", []), key=lambda item: -item.get("properties", {}).get("z", 0)):
            visit(child, rect, rgba, clip)

    for template_id, root in sorted(selected.items(), key=lambda item: -item[1].get("properties", {}).get("z", 0)):
        if not any(node["type"] == "recorded-animation" for node in walk_nodes(root)):
            continue
        result["handled_template_ids"].append(template_id)
        result["handled_root_ids"].append(root["id"])
        visit(root, canvas, (1, 1, 1, 1), None)
    return result


def export_covers(author, options, keymode, presentation, sink, canvas, baked,
                  kinds=("sudden", "hidden", "lift")):
    """Export selected actual material owners, retaining black fill and margins.

    ``baked`` is the current author's source-verified read_bakes mapping. Missing
    or partial sequences remain gaps; their unrelated first frame is never used.
    Object counts are recorded per animation/kind/stage for native load review.
    """
    result = {"slider": [], "hiddenCover": [], "liftCover": [], "destination": [], "gaps": []}
    if (keymode, presentation) not in (("5k", "p1"), ("5k", "p2"), ("7k", "p1"), ("7k", "p2"), ("14k", "dp")):
        raise ValueError("Cover export requires a matching SP or DP presentation")
    if len(set(kinds)) != len(kinds) or any(kind not in ("sudden", "hidden", "lift") for kind in kinds):
        raise ValueError("Cover export needs distinct native cover kinds")
    x0, y0, canvas_width, canvas_height = canvas
    if x0 != 0 or y0 != 0 or min(canvas_width, canvas_height) <= 0:
        raise ValueError("Cover export requires a full positive zero-origin canvas")
    layout = layout_for(author.contribution("frame", options), keymode, presentation)
    base_context = {"layout.ruleset": "bms", "layout.presentation": presentation, "lift.reversed": False,
                    **{"option." + key: value for key, value in options.items()}}
    grouped = {}

    for kind in kinds:
        contribution = author.contribution("lift-art" if kind == "lift" else "lane-cover", options)
        slot = "playfield.lift.fill" if kind == "lift" else "playfield.lane-cover.fill"
        for binding in material_bindings(contribution, keymode, presentation):
            if binding["slot"] != slot or binding["mode"] != "Provide":
                continue
            target = binding["target"]
            if target["kind"] != "stage":
                raise ValueError("Cover material requires its exact stage owner")
            stage = int(target["group-logical"])
            sx, sy, sw, sh = layout["stages"][stage]["rect"]
            sx, sy, sw, sh = sx * canvas_width, sy * canvas_height, sw * canvas_width, sh * canvas_height
            baseline = canvas_height - sy - sh
            context = {**base_context, "layout.group": target["group"]}

            def emit(identifier, image, rect, rgba, blend, draw=None):
                image = {**image, "id": identifier}
                if kind == "sudden":
                    result["slider"].append({**image, "angle": 2, "range": round(sh), "type": 4, "changeable": False})
                else:
                    result[kind + "Cover"].append({**image, "disapearLine": round(baseline), "isDisapearLineLinkLift": kind == "hidden"})
                destination = {"id": identifier, "filter": 0, "blend": 2 if blend == "additive" else 1,
                               "op": [{"sudden": 271, "hidden": 273, "lift": 272}[kind]],
                               "dst": [{**native_rect(rect, canvas_height), **native_colour(rgba)}]}
                if draw is not None:
                    destination["draw"] = draw
                result["destination"].append(destination)

            def visit(node, parent, inherited):
                properties, dynamic = scene_properties(node, contribution.scene, context)
                if properties.get("visible", True) is False:
                    return
                if dynamic or node.get("effects") or any(t["target"] == node["id"] for t in contribution.scene.get("tracks", [])):
                    raise ValueError("Cover requires an explicit dynamic/effect/track adapter")
                if (properties.get("rotation", 0) % 360 or properties.get("scale-x", 1) != 1
                        or properties.get("scale-y", 1) != 1 or node.get("blend", "inherit") not in ("inherit", "alpha")):
                    raise ValueError("Cover source transform or whole-animation blend needs a qualified bake")
                rect, rgba = node_rect(properties, parent), colour(properties, inherited)
                prefix = f"{author.family}.cover.{kind}.{stage}.{node['id']}"
                if node["type"] == "sprite":
                    resource = contribution.resources[properties["resource"]]
                    if resource["type"] != "texture":
                        raise ValueError("Cover sprite requires its declared texture")
                    src = sink.add_source(resource["path"])
                    emit(prefix, {"src": src, "x": 0, "y": 0, "w": sink.dimensions[src][0], "h": sink.dimensions[src][1]},
                         rect, rgba, "normal")
                elif node["type"] == "recorded-animation":
                    key = (contribution.manifest_path, node["animation"])
                    details = {"kind": kind, "stage": stage, "manifest": key[0], "animation": key[1]}
                    if node.get("clock"):
                        result["gaps"].append({"code": "cover-animation-clock-unmapped", "clock": node["clock"], **details})
                        return
                    if key not in baked or not baked[key][1].get("selectionComplete"):
                        result["gaps"].append({"code": "cover-animation-needs-complete-bake", **details})
                        return
                    path, index = baked[key]
                    if (Path(index["source"]["authorRoot"]).resolve() != author.root
                            or (index["source"]["manifest"], index["source"]["animation"]) != key):
                        raise ValueError("Cover bake identity does not match the selected author/material")
                    if key not in grouped:
                        grouped[key] = cover_atlas(sink, path, index)
                    groups = grouped[key]
                    used_pages = {group.source for group in groups}
                    cw, ch = index["canvas"]
                    x, y, width, height = rect
                    for number, group in enumerate(groups):
                        ox, oy = group.offset
                        rw, rh = group.size
                        emit(prefix + ".run" + str(number), group.image(),
                             (x + ox * width / cw, y + oy * height / ch, rw * width / cw, rh * height / ch),
                             rgba, group.blend, group.draw)
                    result["gaps"].append({"code": "native-cover-animation-objects", "informational": True,
                                           "objects": len(groups), "pages": len(used_pages), "sourceFrames": index["sourceFrameCount"],
                                           "maximumActiveRuns": max(len(frame["runs"]) for frame in index["frames"]), **details})
                elif node["type"] != "container":
                    raise ValueError("Cover requires its declared sprite/container/recorded-animation source")
                for child in node.get("children", []):
                    visit(child, rect, rgba)

            for root in material_templates(contribution, slot, binding["resource"]):
                visit(root, (sx, sy - sh if kind == "sudden" else sy + sh, sw, sh), (1, 1, 1, 1))
    return result
