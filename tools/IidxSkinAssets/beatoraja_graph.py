"""Export each author's Graph art against beatoraja 0.8.9 score properties.

The frame's C5 material marker activates templates in the frame, base and
graph-target contributions. The native host owns the target selection: its
total EX/PB/target rates are 110/113/115, not the pace rates 112/114. Original
target artwork is retained, while its name and values describe that host target.

Official-source boundaries: BMSPlayer replaces a missing PB with ScoreData()
before setTargetScore. Its public skin API consequently cannot distinguish a
missing PB from a recorded zero. This exporter draws the native zero and does
not duplicate the current score as a supposed missing-PB fallback.
"""

from __future__ import annotations

import math
from typing import Any

from beatoraja_author import AuthorPackage, Contribution, material_bindings, walk_nodes
from beatoraja_hud import _bitmap_font, _scaled_integer
from beatoraja_native import (
    AssetSink, Lua, all_of, anchor, colour, lua, native_colour, native_rect,
    scene_properties,
)


_MAXIMUM = "(main_state.number(106) * 2)"
_HAS_MAXIMUM = "(main_state.number(106) > 0 and main_state.number(106) < 2147483647)"
_RANKS = {"score.rank-a-progress": 6, "score.rank-aa-progress": 7,
          "score.rank-aaa-progress": 8}
_RATE_REFS = {"score.progress": 110, "score.personal-best-progress": 113}
_IDENTITY = (1.0, 0.0, 0.0, 1.0, 0.0, 0.0)
_SCALE_CLOCK = 1_000_000  # Native destination time is integral milliseconds.


def _multiply(a, b):
    aa, ab, ac, ad, ax, ay = a
    ba, bb, bc, bd, bx, by = b
    return (aa * ba + ac * bb, ab * ba + ad * bb,
            aa * bc + ac * bd, ab * bc + ad * bd,
            aa * bx + ac * by + ax, ab * bx + ad * by + ay)


def _geometry(properties, parent_matrix, parent_size):
    """Keep the rotation pivot; node_rect alone discards the Standard bar pivot."""
    pw, ph = parent_size
    ax, ay = anchor(properties.get("anchor", "top-left"))
    ox, oy = anchor(properties.get("origin", "top-left"))
    w = pw * properties.get("width", 1) * properties.get("scale-x", 1)
    h = ph * properties.get("height", 1) * properties.get("scale-y", 1)
    angle = math.radians(properties.get("rotation", 0))
    c, s = math.cos(angle), math.sin(angle)
    # y increases downwards, as in the original OMS scene.
    x = pw * (properties.get("x", 0) + ax) - c * ox * w + s * oy * h
    y = ph * (properties.get("y", 0) + ay) - s * ox * w - c * oy * h
    return _multiply(parent_matrix, (c, s, -s, c, x, y)), (w, h)


def _bounds(matrix, size):
    a, b, c, d, x, y = matrix
    w, h = size
    corners = [(x + a * px + c * py, y + b * px + d * py)
               for px, py in ((0, 0), (w, 0), (0, h), (w, h))]
    left, top = min(p[0] for p in corners), min(p[1] for p in corners)
    return left, top, max(p[0] for p in corners) - left, max(p[1] for p in corners) - top


def _upright(matrix):
    return all(math.isclose(a, b, abs_tol=1e-8) for a, b in zip(matrix[:4], _IDENTITY[:4]))


def _sample_track(track, clock):
    """A numeric option is a clock value, not an ignored animation trigger."""
    if track.get("type", "tween") != "tween" or track.get("loop", False):
        raise ValueError("Graph static track must be a non-looping tween")
    points = track["keyframes"]
    if not points or any(b["time"] <= a["time"] for a, b in zip(points, points[1:])):
        raise ValueError("Graph track keys must increase strictly")
    if clock <= points[0]["time"]:
        return points[0]["value"]
    for left, right in zip(points, points[1:]):
        if clock < right["time"]:
            if track.get("easing", "linear") == "step":
                return left["value"]
            if track.get("easing", "linear") != "linear":
                raise ValueError("Unmapped Graph static-track easing")
            fraction = (clock - left["time"]) / (right["time"] - left["time"])
            return left["value"] + (right["value"] - left["value"]) * fraction
    return points[-1]["value"]


def _select_templates(author, options, keymode, presentation):
    frame = author.contribution("frame", options)
    markers = {b["resource"] for b in material_bindings(frame, keymode, presentation)
               if b["target"]["kind"] == "global" and b["slot"] == "decoration"
               and b["mode"] == "Provide"}
    contributions = [author.base(), frame, author.contribution("graph-target", options)]
    if "graph-area" in options:
        contributions.append(author.contribution("graph-area", options))
    selected = []
    for contribution in contributions:
        seen = set()
        for instance in contribution.scene.get("instances", []):
            material = instance.get("material", {})
            template_id = instance["template"]
            if (material.get("slot") != "decoration" or material.get("resource") not in markers
                    or template_id in seen):
                continue
            seen.add(template_id)
            root = contribution.templates[template_id]
            ids = {node["id"] for node in walk_nodes(root)}
            operations = [o for k in ("bindings", "variants", "tracks")
                          for o in contribution.scene.get(k, []) if o["target"] in ids]
            if any(o.get("source", o.get("clock")) == "option.graph-visible" for o in operations):
                selected.append((contribution, template_id, root))
    # OMS depth decreases towards the foreground. Native destinations draw in
    # insertion order, so panels precede bars and their readouts.
    selected.sort(key=lambda item: -item[2].get("properties", {}).get("z", 0))
    return contributions, selected


def _visibility(operation):
    source = operation["source"]
    if source in ("score.maximum-available", "score.rank-available"):
        value = _HAS_MAXIMUM
    elif source == "score.personal-best-available":
        value = "true"  # Native's zero is a valid public PB value, not an existence test.
    else:
        return None
    if operation["kind"] == "bindings":
        return value
    cases = operation["cases"]
    if not isinstance(operation.get("default"), bool) or any(not isinstance(c.get("value"), bool) for c in cases):
        return None
    branches = " ".join(f"if v == {lua(c['key'])} then return {lua(c['value'])} end" for c in cases)
    return f"(function() local v = tostring({value}); {branches} return {lua(operation['default'])} end)()"


def _font_sizes(track):
    if track is None:
        return [(None, None)]
    if track.get("easing") != "step" or track.get("loop"):
        raise ValueError("Graph score-fit track must use step interpolation")
    points = track["keyframes"]
    if len({p["value"] for p in points}) == 1:
        return [(points[0]["value"], None)]
    result = []
    for index, point in enumerate(points):
        low = point["time"] if index else None
        high = points[index + 1]["time"] if index + 1 < len(points) else None
        # Adjacent ranges with the same size need only one native object.
        if result and result[-1][0] == point["value"]:
            result[-1] = (point["value"], (result[-1][1][0], high))
        else:
            result.append((point["value"], (low, high)))
    return result


def export_graph(author: AuthorPackage, options: dict[str, Any], keymode: str,
                 presentation: str, sink: AssetSink,
                 canvas: tuple[float, float, float, float], *, layer="all") -> dict[str, Any]:
    """Return native arrays, ordered destinations and explicitly handled roots.

    ``canvas`` is a full, top-left pixel rectangle (0, 0, width, height).
    The caller provides the existing ``native.fallback`` font for source labels
    without an authored bitmap font and for the actual host target name.
    ``handled_template_ids`` are author template IDs, not their root-node IDs;
    both lists are returned to let other passes avoid duplicate Graph text/art.
    Recorded cut-in templates remain unhandled for the caller's bake pass.
    """
    x, y, width, height = canvas
    if x != 0 or y != 0 or width <= 0 or height <= 0:
        raise ValueError("Graph requires the full positive, zero-origin canvas")
    contributions, selected = _select_templates(author, options, keymode, presentation)
    if layer not in ("all", "background", "art", "foreground"):
        raise ValueError("Unknown Graph layer")
    if layer != "all":
        selected = [(c, t, r) for c, t, r in selected
                    if (c.module == "graph-area" if layer == "art" else
                        c.module != "graph-area" and
                        (r.get("properties", {}).get("z", 0) > -20 if layer == "background"
                         else r.get("properties", {}).get("z", 0) <= -20))]
    arrays = {key: [] for key in ("image", "graph", "font", "value", "text")}
    output = {"arrays": arrays, "destination": [], "gaps": [],
              "handled_template_ids": [], "handled_root_ids": []}
    context = {"layout.ruleset": "bms", "layout.presentation": presentation,
               **{"option." + key: value for key, value in options.items()}}
    resources, fonts, numeric = {}, {}, {}
    for contribution in contributions:
        resources.update(contribution.resources)
        fonts.update({f["id"]: f for f in contribution.manifest.get("bitmapFonts", [])})
        numeric.update({s["id"]: s for s in contribution.scene.get("numericSources", [])})
    native_fonts = {}

    def gap(contribution, node, reason, **details):
        item = {"feature": "graph", "module": contribution.module, "node": node,
                "reason": reason, **details}
        if item not in output["gaps"]:
            output["gaps"].append(item)

    def number_expression(source, contribution, seen=()):
        if contribution.module == "graph-target":
            if isinstance(source, str) and source.endswith(".difference"):
                return "(main_state.exscore() - main_state.exscore_rival())"
            if isinstance(source, str) and (source.startswith("score.") or source.endswith(".total")):
                return "main_state.exscore_rival()"
        known = {"score.value": "main_state.exscore()", "score.personal-best": "main_state.exscore_best()",
                 "score.maximum": _MAXIMUM}
        if source in known:
            return known[source]
        if isinstance(source, (float, int)):
            return lua(source)
        if not source.startswith("value."):
            return None
        name = source[6:]
        if name in seen:
            raise ValueError("Cyclic Graph numeric source")
        definition = numeric.get(name)
        if definition is None or definition["operation"] not in ("add", "subtract", "multiply"):
            return None
        left = number_expression(definition["left"], contribution, (*seen, name))
        right = number_expression(definition["right"], contribution, (*seen, name))
        if left is None or right is None:
            return None
        op = {"add": "+", "subtract": "-", "multiply": "*"}[definition["operation"]]
        return f"({left} {op} {right})"

    def destination(identifier, rect, rgba, blend, conditions, y_expression=None):
        values = {"time": 0, **native_rect(rect, height), **native_colour(rgba)}
        if y_expression is not None:
            values["y"] = Lua(y_expression)
        entry = {"id": identifier, "filter": 0, "blend": 2 if blend == "additive" else 1, "dst": [values]}
        draw = all_of(list(dict.fromkeys(c for c in conditions if c != "true")))
        if draw is not None:
            entry["draw"] = draw
        output["destination"].append(entry)
        return entry

    def export_text(contribution, node, properties, rect, rgba, conditions, scale, font_track):
        binding = next((o for o in contribution.scene.get("bindings", [])
                        if o["target"] == node["id"] and o["property"] == "text"), None)
        value = number_expression(binding["source"], contribution) if binding else None
        if binding and value is None:
            gap(contribution, node["id"], "unmapped-text-source", source=binding["source"])
            return
        label = properties.get("text", "")
        is_target_name = contribution.module == "graph-target" and not binding and label in (
            contribution.choice.upper(), "MAXIMUM", "CUSTOM %", "PERSONAL BEST", "MYBEST")
        if not value and not label:
            return
        for index, (font_size, interval) in enumerate(_font_sizes(font_track)):
            prop = {**properties}
            if font_size is not None:
                prop["font-size"] = font_size
            own_conditions = list(conditions)
            if interval:
                low, high = interval
                if low is not None:
                    own_conditions.append(f"{_MAXIMUM} >= {lua(low)}")
                if high is not None:
                    own_conditions.append(f"{_MAXIMUM} < {lua(high)}")
            identifier = f"graph.{contribution.module}.{node['id']}.{index}"
            font_id = prop.get("bitmap-font")
            # Native SkinNumber supports the source's anisotropic score digits;
            # its destination is a per-glyph cell, never the text's whole box.
            if value is not None and font_id in fonts and "-" not in {
                    glyph["character"] for glyph in fonts[font_id]["glyphs"]}:
                result = _scaled_integer(fonts[font_id], resources, sink, prop, rect, scale,
                                         int(height), identifier, value)
                if result is not None:
                    item, dest = result
                    arrays["value"].append(item)
                    entry = {"id": identifier, "blend": 1, "filter": 0,
                             "dst": [{"time": 0, **dest, **native_colour(rgba)}]}
                    draw = all_of(list(dict.fromkeys(c for c in own_conditions if c != "true")))
                    if draw is not None:
                        entry["draw"] = draw
                    output["destination"].append(entry)
                    continue
            if not math.isclose(scale[0], scale[1], abs_tol=1e-7):
                gap(contribution, node["id"], "unsupported-anisotropic-text", scale=list(scale))
                return
            text = {"id": identifier, "overflow": 1 if is_target_name else 0}
            if font_id and not is_target_name:
                if font_id not in native_fonts:
                    native_fonts[font_id] = _bitmap_font(fonts[font_id], resources, sink)
                    arrays["font"].append(native_fonts[font_id])
                text["font"] = native_fonts[font_id]["id"]
            else:
                text["font"] = "native.fallback"
            if is_target_name:
                text["ref"] = 3  # StringPropertyFactory.target, not a selector-only target list label.
            elif value is not None:
                text["value"] = Lua('function() return string.format("%.0f", ' + value + ') end')
            else:
                text["constantText"] = label
            alignment = prop.get("alignment", "left")
            text["align"] = {"left": 0, "centre": 1, "right": 2}[alignment]
            text["size"] = round(prop.get("font-size", 16) * height / 768 * scale[1])
            arrays["text"].append(text)
            tx, ty, tw, th = rect
            offset = {"left": 0, "centre": .5, "right": 1}[alignment]
            destination(identifier, (tx + tw * offset, ty + (th - text["size"]) / 2, tw, text["size"]),
                        rgba, "alpha", own_conditions)

    for contribution, template_id, root in selected:
        if any(n["type"] == "recorded-animation" for n in walk_nodes(root)):
            if options.get("graph-visible", True) and options.get("graph-cutin", True):
                gap(contribution, root["id"], "recorded-graph-cutins-require-bake", template=template_id)
            continue
        output["handled_template_ids"].append(template_id)
        output["handled_root_ids"].append(root["id"])
        last = {(o["target"], o["property"]): o["id"] for kind in ("bindings", "variants")
                for o in contribution.scene.get(kind, [])}

        def visit(node, matrix, size, rgba, conditions, scale, reveal=None, blend="alpha", clip=None):
            prop, operations = scene_properties(node, contribution.scene, context)
            operations = [o for o in operations if last[o["target"], o["property"]] == o["id"]]
            visible = next((o for o in operations if o["property"] == "visible"), None)
            own_conditions = list(conditions)
            if visible is not None:
                condition = _visibility(visible)
                if condition is None:
                    gap(contribution, node["id"], "unmapped-visibility", source=visible["source"])
                    return
                # Do not resurrect Standard's missing-PB current-score mirror.
                if (visible["source"] == "score.personal-best-available" and visible["kind"] == "variants"
                        and any(c["key"] == "false" and c.get("value") is True for c in visible["cases"])):
                    return
                own_conditions.append(condition)
            elif prop.get("visible", True) is False:
                return
            font_track = rank_track = None
            for track in contribution.scene.get("tracks", []):
                if track["target"] != node["id"]:
                    continue
                clock = track.get("clock")
                if clock in context:
                    prop[track["property"]] = _sample_track(track, float(context[clock]))
                elif clock in _RANKS and track["property"] == "y":
                    rank_track = track
                    prop["y"] = _sample_track(track, 0)
                elif clock == "score.maximum" and track["property"] == "font-size":
                    font_track = track
                else:
                    gap(contribution, node["id"], "unmapped-track", clock=clock, property=track["property"])
                    return
            own_reveal = reveal
            graph_ref = None
            for operation in operations:
                field, source = operation["property"], operation["source"]
                if field in ("visible", "text"):
                    continue
                if operation["kind"] == "bindings" and field in ("reveal-x", "scale-y"):
                    ref = 115 if contribution.module == "graph-target" else _RATE_REFS.get(source)
                    if ref is not None:
                        if field == "reveal-x":
                            own_reveal = ref
                        else:
                            prop["scale-y"], graph_ref = 1, ref
                        continue
                gap(contribution, node["id"], "unmapped-style", source=source, property=field)
                return
            # Lightning MAXIMUM's original target is a constant-height sprite;
            # replace that one visual role with the real native target rate.
            if (contribution.module == "graph-target" and node["type"] == "sprite"
                    and own_reveal is None and "scale-y" in prop
                    and prop.get("origin") == "bottom-left"):
                prop["scale-y"], graph_ref = 1, 115
            node_matrix, node_size = _geometry(prop, matrix, size)
            rect = _bounds(node_matrix, node_size)
            own_rgba = colour(prop, rgba)
            own_scale = (scale[0] * prop.get("scale-x", 1), scale[1] * prop.get("scale-y", 1))
            own_blend = node.get("blend", "inherit")
            own_blend = blend if own_blend == "inherit" else own_blend
            if node.get("effects"):
                gap(contribution, node["id"], "graph-effects-require-bake")
                return
            if node["type"] == "text":
                if not _upright(node_matrix):
                    gap(contribution, node["id"], "rotated-graph-text")
                    return
                export_text(contribution, node, prop, rect, own_rgba, own_conditions, own_scale, font_track)
            elif node["type"] == "sprite":
                if not _upright(node_matrix):
                    gap(contribution, node["id"], "unmapped-sprite-orientation")
                    return
                resource = resources[prop["resource"]]
                if resource["type"] != "texture":
                    gap(contribution, node["id"], "non-texture-graph-sprite")
                    return
                src = sink.add_source(resource["path"])
                sw, sh = sink.dimensions[src]
                identifier = f"graph.{contribution.module}.{node['id']}"
                item = {"id": identifier, "src": src, "x": 0, "y": 0, "w": sw, "h": sh}
                ref = graph_ref if graph_ref is not None else own_reveal
                stretch = graph_ref is not None and own_reveal is None
                if ref is not None:
                    # 110/113/115 are registered RateType properties. Graph.type
                    # reads that factory directly, as the official skin does.
                    item.update(type=ref, angle=1)
                    if stretch:
                        # Lightning scales the whole gradient, unlike Standard's
                        # reveal clip. Keep all source rows; only its native
                        # destination height follows the rate clock below.
                        item["value"] = Lua("function() return 1 end")
                    arrays["graph"].append(item)
                    own_conditions.append(_HAS_MAXIMUM)
                else:
                    arrays["image"].append(item)
                y_expression = None
                if rank_track is not None:
                    # This chart-constant value is evaluated when Lua builds the
                    # skin. The chart is present before BMSPlayer.loadSkin().
                    start = _sample_track(rank_track, 0)
                    end = _sample_track(rank_track, 1)
                    rate = f"({_HAS_MAXIMUM} and math.ceil({_MAXIMUM} * {_RANKS[rank_track['clock']]} / 9) / {_MAXIMUM} or 0)"
                    base_y = height - rect[1] - rect[3]
                    y_expression = f"math.floor({lua(base_y)} - {lua(size[1] * (end - start))} * {rate} + 0.5)"
                entry = destination(identifier, rect, own_rgba, own_blend, own_conditions, y_expression)
                if clip is not None:
                    entry["dst"][0].update({"clip_" + k: v for k, v in native_rect(clip, height).items()})
                if stretch:
                    full = entry["dst"][0]
                    entry["dst"] = [{**full, "h": 0}, {**full, "time": _SCALE_CLOCK}]
                    entry["loop"] = _SCALE_CLOCK
                    # TimerProperty.getMicro() is converted to integral ms by
                    # SkinObject. Quantise both terms before subtraction so a
                    # negative timer origin does not introduce truncation drift.
                    entry["timer"] = Lua(
                        "function() local rate = math.max(0, math.min(1, main_state.float_number("
                        + str(ref) + "))); return (math.floor(main_state.time() / 1000) - "
                        + f"math.floor(rate * {_SCALE_CLOCK} + 0.5)) * 1000 end")
            elif node["type"] == "clip":
                if prop.get("clip-mode") != "bounds":
                    gap(contribution, node["id"], "unmapped-graph-clip")
                    return
                if own_reveal is None:
                    if not _upright(node_matrix):
                        gap(contribution, node["id"], "unsupported-static-clip-direction")
                        return
                    if clip is None:
                        clip = rect
                    else:
                        left, top = max(clip[0], rect[0]), max(clip[1], rect[1])
                        right, bottom = min(clip[0] + clip[2], rect[0] + rect[2]), min(clip[1] + clip[3], rect[1] + rect[3])
                        if right <= left or bottom <= top:
                            return
                        clip = (left, top, right - left, bottom - top)
                else:
                    # The Standard source rotates its horizontal clipping carrier
                    # -90 degrees, then rotates the full source image +90 degrees.
                    # Its transformed reveal axis must grow from bottom to top.
                    if not (math.isclose(node_matrix[0], 0, abs_tol=1e-8)
                            and math.isclose(node_matrix[1], -1, abs_tol=1e-8)):
                        gap(contribution, node["id"], "unsupported-graph-reveal-direction")
                        return
            elif node["type"] != "container":
                gap(contribution, node["id"], "unsupported-graph-node", type=node["type"])
                return
            for child in sorted(node.get("children", []), key=lambda n: -n.get("properties", {}).get("z", 0)):
                visit(child, node_matrix, node_size, own_rgba, own_conditions, own_scale, own_reveal, own_blend, clip)

        visit(root, _IDENTITY, (width, height), (1, 1, 1, 1), [], (1, 1))

    if layer in ("all", "foreground") and options.get("graph-visible", True):
        output["gaps"].extend([
            {"feature": "graph", "reason": "native-pb-existence-unavailable",
             "detail": "Official playback APIs merge missing and zero PB. Native PB=0 is retained; no current-score mirror is fabricated."},
            {"feature": "graph", "reason": "native-target-selection",
             "detail": "Source target art follows the host target name/ref 3 and total score/rate 151/115. Artwork choices do not replace host target policy or reproduce the source pace algorithm. Available original crossing movies are handled by the separate public-state feedback adapter."},
        ])
    return output
