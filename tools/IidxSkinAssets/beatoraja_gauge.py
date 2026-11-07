"""Original Gauge border markers and boundary shade on public native state."""

import math

from beatoraja_author import walk_nodes
from beatoraja_native import Lua, anchor, colour, native_colour, native_rect, node_rect, scene_properties
from beatoraja_widgets import GAUGE_PALETTES, IMAGE_FIELDS, _gauge_host, _rect, _route, _pixels


def _pulse_palette(image, track, sink, identifier):
    """Two independent native clocks: image alpha pulse and destination clip."""
    from PIL import Image
    keys = track["keyframes"]
    if (track.get("clock") != "timing.time" or not track.get("loop") or track["property"] != "opacity"
            or track.get("easing", "linear") != "linear" or len(keys) < 2 or keys[0]["time"] != 0
            or keys[0]["value"] != keys[-1]["value"] or any(a["time"] >= b["time"] for a, b in zip(keys, keys[1:]))
            or any(not 0 <= k["value"] <= 1 for k in keys)):
        raise ValueError("Unqualified Gauge boundary opacity pulse")
    low = round(min(k["value"] for k in keys) * 255)
    high = round(max(k["value"] for k in keys) * 255)
    key = (image["src"], image["x"], image["y"], image["w"], image["h"], low, high)
    if key not in sink._gauge_pulse_palettes:
        pixels = _pixels(image, sink)
        atlas = Image.new("RGBA", (pixels.width, pixels.height * (high-low+1)))
        alpha = pixels.getchannel("A")
        for level in range(low, high+1):
            cell = pixels.copy()
            cell.putalpha(alpha.point([round(v * level / 255) for v in range(256)]))
            atlas.paste(cell, (0, (level-low)*pixels.height))
        sink._gauge_pulse_palettes[key] = (sink.add_raster(atlas), pixels.width, pixels.height)
    source, width, height = sink._gauge_pulse_palettes[key]
    images = [{"id": f"{identifier}.alpha.{level}", "src": source, "x": 0, "y": (level-low)*height,
               "w": width, "h": height} for level in range(low, high+1)]
    branches = " ".join(f"if phase < {b['time']} then value={a['value']} + (phase-{a['time']}) * "
                        f"{(b['value']-a['value'])/(b['time']-a['time'])}; break end"
                        for a,b in zip(keys,keys[1:]))
    callback = Lua(f"function() local phase=(main_state.time()/1000)%{keys[-1]['time']}; local value={keys[-1]['value']}; "
                   f"repeat {branches} until true; return math.max(0,math.min({high-low},math.floor(value*255+0.5)-{low})) end")
    return images, {"id": identifier, "images": [i["id"] for i in images], "value": callback}


def compose_gauge_markers(recipe, contribution, gauge_stages, canvas_height):
    # Official 0.8.9 seven-/fourteen-key GrooveGauge properties: ASSIST EASY
    # border 60; EASY/NORMAL border 80; survival types do not show this marker.
    borders = (0.6, 0.8, 0.8, 0, 0, 0, 0, 0, 0)
    result = {"image": [], "destination": [], "gaps": [], "handled_nodes": []}
    targets = {b["target"] for b in recipe["bindings"]
               if b["source"] == "gauge.clear-threshold" and b["property"] == "x"}
    images = {image["id"]: image for image in recipe["images"]}
    stages = [_rect(stage) for stage in gauge_stages]
    canvas = (0, 0, canvas_height * 16 / 9, canvas_height)
    order = {node["id"]: i for i, node in enumerate(node for root in
             [contribution.scene["root"], *contribution.templates.values()] for node in walk_nodes(root))}
    def leaves(node, route):
        if node["type"] == "sprite":
            yield node, route
        elif node["type"] == "container":
            for child in sorted(node.get("children", []), key=lambda n: -n.get("properties", {}).get("z", 0)):
                yield from leaves(child, [*route, child])
        else:
            raise ValueError("Gauge border requires its original sprite/container subtree")
    for destination in sorted(recipe["destinations"], key=lambda d: order[d["node"]["id"]]):
        node, route = destination["node"], destination["ancestors"]
        if node["id"] not in targets:
            continue
        owners = [bar for bar in recipe["bars"] if bar["ancestors"][0]["id"] == route[0]["id"]]
        if not owners or any(n["type"] == "clip" for n in route):
            result["gaps"].append({"code": "gauge-border-owner-unqualified", "node": node["id"]})
            continue
        hosts = {_gauge_host(bar, stages, canvas) for bar in owners}
        if len(hosts) != 1:
            raise ValueError("Gauge border has multiple material owners")
        host = hosts.pop()
        for gauge_type, palette in enumerate(GAUGE_PALETTES):
            for cleared in (False, True):
                context = {**recipe.get("context", {}), "gauge.type": palette,
                           "gauge.clear-threshold": borders[gauge_type],
                           "gauge.clear-state": "clear" if cleared else "uncleared"}
                for leaf, leaf_route in leaves(node, route):
                    unresolved = [o for ancestor in leaf_route for o in scene_properties(ancestor, contribution.scene, context)[1]]
                    if unresolved:
                        raise ValueError("Gauge border has an unmapped ancestor binding")
                    rect, rgba, visible = _route(leaf_route, host, contribution.scene, context)
                    if not visible or rgba[3] == 0:
                        continue
                    properties, _ = scene_properties(leaf, contribution.scene, context)
                    image = images[recipe["resource_images"][properties["resource"]]]
                    identifier = f"{recipe['family']}.gauge.border.{leaf['id']}.{gauge_type}.{int(cleared)}"
                    result["image"].append({**{k: v for k, v in image.items() if k in IMAGE_FIELDS}, "id": identifier})
                    result["destination"].append({"id": identifier, "filter": 0, "blend": 1,
                        "draw": Lua(f"function() return main_state.gauge_type() == {gauge_type} and "
                                    + ("" if cleared else "not ") + "main_state.option(1240) end"),
                        "dst": [{**native_rect(rect, canvas_height), **native_colour(rgba)}]})
        result["handled_nodes"].append(node["id"])
    return result


def compose_gauge_shade(recipe, contribution, gauge_stages, canvas_height, sink=None):
    """Retain the original translucent moving strip with an interpolated clip.

    A public Gauge fraction supplies a synthetic destination clock, never a
    write to a built-in timer. Original art stays fixed; only its nested source
    clip changes. Integer destination endpoints have a reported pixel bound.
    """
    result = {"image": [], "imageset": [], "destination": [], "gaps": [], "handled_nodes": []}
    targets = {b["target"] for b in contribution.scene.get("bindings", [])
               if b["source"] == "gauge.value" and b["property"] == "x"}
    if not targets:
        return result
    images = {image["id"]: image for image in recipe["images"]}
    stages = [_rect(stage) for stage in gauge_stages]
    canvas = (0, 0, canvas_height * 16 / 9, canvas_height)
    routes = {}
    def collect(node, ancestors):
        routes[node["id"]] = (node, [*ancestors, node])
        for child in node.get("children", []):
            collect(child, [*ancestors, node])
    root_ids = {bar["ancestors"][0]["id"] for bar in recipe["bars"]}
    for root in [contribution.scene["root"], *getattr(contribution, "templates", {}).values()]:
        if root["id"] in root_ids:
            collect(root, [])
    for leaf, route in routes.values():
        if leaf["type"] != "sprite" or not any(n["id"] in targets for n in route):
            continue
        clips = [n for n in route if n["type"] == "clip"]
        if len(clips) != 2:
            raise ValueError("Gauge boundary requires the two original nested clips")
        fraction = clips[1]["properties"]["reveal-x"]
        if not 0 < fraction < 1:
            raise ValueError("Invalid original Gauge boundary width")
        owners = [bar for bar in recipe["bars"] if bar["ancestors"][0]["id"] == route[0]["id"]]
        hosts = {_gauge_host(bar, stages, canvas) for bar in owners}
        if len(hosts) != 1:
            raise ValueError("Gauge boundary has no unique material owner")
        host = hosts.pop()
        pulses = [t for t in contribution.scene.get("tracks", [])
                  if t["target"] in {n["id"] for n in route} and t.get("clock") == "timing.time" and t["property"] == "opacity"]
        if len(pulses) > 1:
            raise ValueError("Gauge boundary has competing opacity clocks")
        for gauge_type, palette in enumerate(GAUGE_PALETTES):
            snapshots = []
            for value in (0, fraction, 1):
                context = {**recipe.get("context", {}), "gauge.type": palette, "gauge.value": value}
                rect, rgba, visible, intersection = host, (1, 1, 1, 1), True, None
                for node in route:
                    prop, dynamic = scene_properties(node, contribution.scene, context)
                    if dynamic:
                        raise ValueError("Gauge boundary has unresolved source state")
                    for track in contribution.scene.get("tracks", []):
                        if track["target"] != node["id"]:
                            continue
                        if track in pulses:
                            prop["opacity"] = 1
                            continue
                        if track.get("clock") != "gauge.value" or track.get("easing", "linear") != "linear" or track.get("loop"):
                            raise ValueError("Gauge boundary has an unqualified track: " + str(track))
                        keys = track["keyframes"]
                        if len(keys) != 2 or [k["time"] for k in keys] != [0, 1]:
                            raise ValueError("Gauge boundary track is not its original linear range")
                        prop[track["property"]] = keys[0]["value"] + value * (keys[1]["value"] - keys[0]["value"])
                    rect = node_rect(prop, rect)
                    rotation = prop.get("rotation", 0) % 360
                    if rotation == 180:
                        ox, oy = anchor(prop.get("origin", "top-left"))
                        x, y, w, h = rect
                        rect = (x + 2*ox*w, y + 2*oy*h, -w, -h)
                    elif rotation:
                        raise ValueError("Gauge boundary needs an axis-aligned source route")
                    rgba = colour(prop, rgba)
                    visible &= prop.get("visible", True)
                    if node["type"] == "clip":
                        x, y, w, h = rect
                        revealed = w * prop.get("reveal-x", 1)
                        bounds = (min(x, x + revealed), min(y, y + h), max(x, x + revealed), max(y, y + h))
                        intersection = bounds if intersection is None else (max(bounds[0], intersection[0]), max(bounds[1], intersection[1]),
                                                                           min(bounds[2], intersection[2]), min(bounds[3], intersection[3]))
                snapshots.append((value, rect, rgba, visible, intersection, prop["resource"]))
            if not any(s[3] for s in snapshots):
                continue
            if not all(s[3] for s in snapshots):
                raise ValueError("Gauge shade visibility changes within its native fraction")
            first = snapshots[0]
            if any(any(not math.isclose(a, b, abs_tol=1e-5) for a, b in zip(s[1], first[1])) or s[2] != first[2]
                   or s[5] != first[5] for s in snapshots[1:]):
                raise ValueError("Source Gauge shade cannot be represented as a fixed image and moving clip")
            identifier = f"{recipe['family']}.gauge.shade.{leaf['id']}.{gauge_type}"
            image = images[recipe["resource_images"][first[5]]]
            if pulses:
                palette, imageset = _pulse_palette(image, pulses[0], sink, identifier)
                result["image"].extend(palette)
                result["imageset"].append(imageset)
            else:
                result["image"].append({**{k: v for k, v in image.items() if k in IMAGE_FIELDS}, "id": identifier})
            # Native TimerProperty truncates signed timestamps to milliseconds;
            # duplicate that rule in the gate. A zero-width native clip means
            # *no clipping*, so an elapsed-zero sample must not draw the image.
            duration = 1_000_000
            timer = Lua(f"function() return main_state.time() - math.floor(math.max(0,math.min(1,main_state.gauge()/100))*{duration}*1000) end")
            draw = Lua(f"function() if main_state.gauge_type()~={gauge_type} then return false end; "
                       f"local now=main_state.time(); local origin=now-math.floor(math.max(0,math.min(1,main_state.gauge()/100))*{duration}*1000); "
                       "local start=origin<0 and math.ceil(origin/1000) or math.floor(origin/1000); return math.floor(now/1000)-start>0 end")
            points = []
            for value, rect, rgba, _, clip, _ in snapshots:
                left, top, right, bottom = clip
                clip_rect = native_rect((left, top, max(0, right-left), max(0, bottom-top)), canvas_height)
                points.append({"time": round(value * duration), **native_rect(first[1], canvas_height), **native_colour(rgba),
                               **{"clip_" + key: v for key, v in clip_rect.items()}})
            result["destination"].append({"id": identifier, "filter": 0, "blend": 1, "loop": -1, "timer": timer,
                "draw": draw, "dst": points})
            result["handled_nodes"].append(leaf["id"])
        if pulses:
            result["gaps"].append({"code": "native-gauge-boundary-pulse-adaptation", "informational": True,
                "detail": "The original continuous opacity pulse follows the native screen clock independently of Gauge-driven clipping. It is sampled to an 8-bit alpha palette, with at most one alpha-byte rounding difference per source pixel."})
    result["gaps"].append({"code": "native-gauge-boundary-clip-adaptation", "informational": True,
        "detail": "Original fixed art, colour, signed 180-degree routes and nested moving boundary clip are retained. Public Gauge fractions drive native destination interpolation; integer clip endpoints differ by at most one source pixel. Signed millisecond timer rounding adds less than two millionths of the bar span. Elapsed-zero samples are suppressed because the host treats a zero-width clip as no clipping."})
    return result
