"""Retain the selected author's song-position cursor on native playback progress.

Only actual global decoration routes with a timing.progress -> y binding are
owned here. The original image, parent-space travel and inherited tint remain;
beatoraja's public music-progress property owns the value and duration policy.
"""

from beatoraja_author import material_bindings, material_templates
from beatoraja_native import colour, native_colour, native_rect, node_rect, scene_properties


def _routes(author, options, keymode, presentation):
    frame = author.contribution("frame", options)
    targets = {operation["target"] for operation in frame.scene.get("bindings", [])
               if operation["source"] == "timing.progress" and operation["property"] == "y"}
    selected, seen = [], set()

    def visit(node, ancestors):
        if node["id"] in targets and node["id"] not in seen:
            seen.add(node["id"])
            selected.append((*ancestors, node))
        for child in node.get("children", []):
            visit(child, (*ancestors, node))

    for binding in material_bindings(frame, keymode, presentation):
        if (binding["target"]["kind"] == "global" and binding["slot"] == "decoration"
                and binding["mode"] == "Provide"):
            for root in material_templates(frame, "decoration", binding["resource"]):
                visit(root, ())
    return frame, selected


def progress_ownership(author, options, keymode, presentation):
    """Exact leaf ownership for independently composed decoration fragments."""
    _, routes = _routes(author, options, keymode, presentation)
    return {"handled_node_ids": [route[-1]["id"] for route in routes]}


def export_progress(author, options, keymode, presentation, sink, canvas):
    """Return native slider/destination arrays without changing source pixels."""
    frame, routes = _routes(author, options, keymode, presentation)
    result = {"slider": [], "destination": [], "gaps": [],
              "handled_node_ids": [route[-1]["id"] for route in routes]}
    context = {"layout.ruleset": "bms", "layout.presentation": presentation,
               **{"option." + key: value for key, value in options.items()}}
    for route in routes:
        rect, rgba, blend, visible = canvas, (1, 1, 1, 1), "alpha", True
        cursor = route[-1]
        for node in route:
            properties, dynamic = scene_properties(node, frame.scene, context)
            is_cursor = node is cursor
            expected = [operation for operation in dynamic
                        if (is_cursor and operation["kind"] == "bindings"
                            and operation["source"] == "timing.progress" and operation["property"] == "y")]
            if dynamic != expected or (is_cursor and len(expected) != 1):
                raise ValueError("Progress cursor has an unqualified dynamic route")
            if (node["type"] != ("sprite" if is_cursor else "container")
                    or node.get("effects") or properties.get("rotation", 0)
                    or properties.get("clip", False)
                    or any(track["target"] == node["id"] for track in frame.scene.get("tracks", []))):
                raise ValueError("Progress cursor requires its static axis-aligned source route")
            if is_cursor:
                span = rect[3]
                properties["y"] = 0  # The binding replaces, rather than adds to, source y.
            rect = node_rect(properties, rect)
            visible = visible and properties.get("visible", True)
            rgba = colour(properties, rgba)
            blend = node.get("blend", "inherit") if node.get("blend", "inherit") != "inherit" else blend
        if not visible or rgba[3] == 0:
            continue
        if span <= 0 or min(rect[2:]) <= 0 or blend not in ("alpha", "additive"):
            raise ValueError("Progress cursor requires positive source travel and a supported blend")
        resource = frame.resources[properties["resource"]]
        if resource["type"] != "texture":
            raise ValueError("Progress cursor must use its original texture")
        source = sink.add_source(resource["path"])
        width, height = sink.dimensions[source]
        identifier = author.family + ".progress." + cursor["id"]
        result["slider"].append({"id": identifier, "src": source, "x": 0, "y": 0,
                                 "w": width, "h": height, "angle": 2, "range": round(span),
                                 "type": 6, "changeable": False})
        result["destination"].append({"id": identifier, "filter": 0,
                                      "blend": 2 if blend == "additive" else 1,
                                      "dst": [{**native_rect(rect, canvas[3]), **native_colour(rgba)}]})
    if result["slider"]:
        result["gaps"].extend([
            {"code": "native-progress-duration-adaptation", "informational": True,
             "detail": "Original song-position art follows native music progress (rate 6): PLAY elapsed / host playtime, "
                       "zero before PLAY and capped at one. Native playtime includes its completion margin and practice/autoplay policy; "
                       "it is not OMS's first-to-last hit-object interval. No source beat timing or artificial cursor animation is inferred."},
            {"code": "native-progress-integer-travel", "informational": True,
             "detail": "Native destination and travel are integral source pixels; the official loader then truncates scaled Slider travel "
                       "to whole destination pixels. Cursor-origin error is below 1 source pixel plus 1/destinationScale, apart from float arithmetic. "
                       "Original PNG bytes, crop and source geometry remain the reference."},
        ])
    return result
