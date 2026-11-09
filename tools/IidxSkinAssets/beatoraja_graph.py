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


def _source20_zero_field(node, contribution, presentation, fonts):
    """Qualify just original20's two four-cell bright-zero fields."""
    if contribution.module == "graph-target":
        choice = contribution.choice
        if choice not in ("aaa", "aa", "a", "custom", "next", "next-plus"):
            return None
        text_id = f"standard.graph.fs20.target.{choice}.{presentation}.total"
        source = ({"aaa": "score.rank-aaa", "aa": "score.rank-aa", "a": "score.rank-a"}.get(choice)
                  or f"value.standard.graph.fs20.target.{choice}.{presentation}.value."
                  + ("selected.l2.n0" if choice == "next" else "target"))
        native = "main_state.exscore_rival()"
    elif contribution.module in ("base", "frame"):
        text_id = f"standard.graph.fs20.{presentation}.current.value"
        source, native = "score.value", "main_state.exscore()"
    else:
        return None
    if node["id"] != text_id + ".fixed-cells" or node["type"] != "container" or node.get("properties"):
        return None
    children = node.get("children", [])
    if len(children) != 2:
        return None
    text, grid = children
    font = fonts.get("standard.graph.fs20.font.score", {})
    zero = next((g for g in font.get("glyphs", []) if g["character"] == "0"), None)
    if (text["id"] != text_id or text["type"] != "text" or grid["id"] != text_id + ".leading-zeros"
            or grid["type"] != "container" or zero is None or zero["advance"] != 22 or font.get("lineHeight") != 22):
        return None
    props = text.get("properties", {})
    if (props.get("bitmap-font") != font["id"] or props.get("format") != "integer"
            or props.get("alignment") != "right" or props.get("colour") != "#ffffffff"
            or not math.isclose(props.get("width", 0), 89 / 1280, abs_tol=1e-10)
            or not math.isclose(props.get("height", 0), 22 / 720, abs_tol=1e-10)):
        return None
    bindings = [b for b in contribution.scene.get("bindings", []) if b["target"] == text_id and b["property"] == "text"]
    if len(bindings) != 1 or bindings[0]["source"] != source:
        return None
    grid_props = grid.get("properties", {})
    expected = {"x": props.get("x", 0) + props["width"], "y": props.get("y", 0) + props["height"] / 2,
                "width": props["width"], "height": props["height"]}
    if (set(grid_props) != {*expected, "origin"} or grid_props["origin"] != "centre-right"
            or any(not math.isclose(grid_props[key], value, abs_tol=1e-10) for key, value in expected.items())):
        return None
    tracks = contribution.scene.get("tracks", [])
    fits = [t for t in tracks if t["target"] == text_id and t["property"] == "font-size"]
    if len(fits) != 1:
        return None
    fit = fits[0]
    times = [0, 10_000, 100_000, 1_000_000, 10_000_000]
    factors = [1, *(min(1, 89 / (22 * (digits - 1) + 23)) for digits in (5, 6, 7, 10))]
    base_size = 22 * 768 / 720
    if (fit.get("type", "tween") != "tween" or fit.get("clock") != "score.maximum"
            or fit.get("easing") != "step" or fit.get("loop", False)
            or [p["time"] for p in fit["keyframes"]] != times
            or any(not math.isclose(p["value"], factor * base_size, abs_tol=1e-9)
                   for p, factor in zip(fit["keyframes"], factors))):
        return None
    scales = [t for t in tracks if t["target"] == grid["id"]]
    if len(scales) != 2 or {t["property"] for t in scales} != {"scale-x", "scale-y"}:
        return None
    for track in scales:
        if (track.get("type", "tween") != "tween" or track.get("clock") != "score.maximum"
                or track.get("easing") != "step" or track.get("loop", False)
                or [p["time"] for p in track["keyframes"]] != times
                or any(not math.isclose(p["value"], factor, abs_tol=1e-9)
                       for p, factor in zip(track["keyframes"], factors))):
            return None
    zeros = grid.get("children", [])
    if [z["id"] for z in zeros] != [text_id + f".leading-zero.n{i}" for i in (2, 1, 0)]:
        return None
    gates = {}
    for column, sprite in zip((2, 1, 0), zeros):
        prop = sprite.get("properties", {})
        gate = [t for t in tracks if t["target"] == sprite["id"]]
        threshold = 10 ** (3 - column)
        if (sprite["type"] != "sprite" or sprite.get("resource") != zero["resource"]
                or sprite.get("children") or sprite.get("effects") or sprite.get("blend", "inherit") != "inherit"
                or set(prop) != {"x", "width", "height"}
                or any(not math.isclose(prop.get(key, -1), value, abs_tol=1e-10) for key, value in
                       (("x", column * 22 / 89), ("width", 23 / 89), ("height", 1)))
                or len(gate) != 1):
            return None
        gate = gate[0]
        if (gate.get("type", "tween") != "tween" or gate.get("property") != "opacity" or gate.get("clock") != source
                or gate.get("loop", False) or gate.get("easing") != "step"
                or [(p["time"], p["value"]) for p in gate["keyframes"]] != [(0, 1), (threshold, 0)]):
            return None
        gates[sprite["id"]] = {"track": gate["id"], "condition": f"{native} < {threshold}", "column": column}
    return {"grid": grid["id"], "number": text_id, "scale": scales[0], "scaleTracks": {t["id"] for t in scales}, "gates": gates}


def _source20_rank_states(node, contribution, presentation, numeric, nodes):
    prefix = f"standard.graph.fs20.{presentation}"
    rank = node["id"].removeprefix(prefix + ".line.")
    if (contribution.module not in ("base", "frame") or rank not in ("a", "aa", "aaa")
            or node["id"] != prefix + ".line." + rank or node["type"] != "sprite"
            or node.get("resource") != "standard.graph.fs20.texture.gr-line-" + rank
            or node.get("properties", {}).get("colour") != "#8787ffff"
            or node.get("properties", {}).get("opacity") != 1):
        return None
    reached = nodes.get(node["id"] + ".reached")
    if (reached is None or reached["type"] != "sprite" or reached.get("resource") != node.get("resource")
            or reached.get("properties", {}).get("colour") != "#ffff00ff"
            or reached.get("properties", {}).get("opacity") != 0
            or {k: v for k, v in reached["properties"].items() if k not in ("colour", "opacity")}
               != {k: v for k, v in node["properties"].items() if k not in ("colour", "opacity")}):
        return None
    clock = prefix + ".value.rank-colour-" + rank
    source = numeric.get(clock, {})
    if (source.get("operation") != "subtract" or source.get("left") != "score.rank-" + rank
            or source.get("right") != "score.value"):
        return None
    states = {}
    threshold = f"math.ceil({_MAXIMUM} * {_RANKS['score.rank-' + rank + '-progress']} / 9)"
    for sprite, values, comparison in ((node, [(0, 0), (1, 1)], "<"), (reached, [(0, 1), (1, 0)], ">=")):
        tracks = [t for t in contribution.scene.get("tracks", []) if t["target"] == sprite["id"]]
        if len(tracks) != 2 or {t["property"] for t in tracks} != {"y", "opacity"}:
            return None
        track = next(t for t in tracks if t["property"] == "opacity")
        if (track.get("type", "tween") != "tween" or track.get("clock") != "value." + clock
                or track.get("loop", False) or track.get("easing") != "step"
                or [(p["time"], p["value"]) for p in track["keyframes"]] != values):
            return None
        y_track = next(t for t in tracks if t["property"] == "y")
        if (y_track.get("type", "tween") != "tween" or y_track.get("clock") != "score.rank-" + rank + "-progress"
                or y_track.get("loop", False) or y_track.get("easing", "linear") != "linear"
                or [(p["time"], p["value"]) for p in y_track["keyframes"]] != [(0, 529 / 720), (1, 79 / 720)]):
            return None
        states[sprite["id"]] = {"track": track["id"], "condition": f"main_state.exscore() {comparison} {threshold}"}
    return states


def _target_art_roles(family, contribution):
    """Identify the selected source's readouts, not its original target policy.

    Standard's frame-specific prefixes differ, and NEXT's selected total is an
    arithmetic source rather than a name ending in ``.total``. These roles are
    intentionally mapped together to the same native target as its bar.
    """
    values, names = {}, set()
    nodes = {n["id"]: n for root in contribution.templates.values() for n in walk_nodes(root)}
    bindings = {b["target"]: b for b in contribution.scene.get("bindings", []) if b["property"] == "text"}
    if family == "standard" and contribution.choice in ("custom", "next", "next-plus"):
        totals = [identifier for identifier in bindings if identifier.endswith(".total")]
        if len(totals) != 1:
            raise ValueError("Standard target artwork requires one authored total role")
        prefix = totals[0][:-len(".total")]
        suffix = "selected.l2.n0" if contribution.choice == "next" else "target"
        expected = {prefix + ".total": "value." + prefix + ".value." + suffix,
                    prefix + ".difference": "value." + prefix + ".value.difference"}
        label = prefix + ".label"
        label_text = {"custom": "CUSTOM %", "next": "NEXT", "next-plus": "NEXT+"}[contribution.choice]
        if (any(identifier not in nodes or nodes[identifier]["type"] != "text"
                or bindings.get(identifier, {}).get("source") != source
                for identifier, source in expected.items())
                or nodes.get(label, {}).get("properties", {}).get("text") != label_text):
            raise ValueError("Unrecognised Standard target artwork roles")
        values[prefix + ".total"] = "main_state.exscore_rival()"
        values[prefix + ".difference"] = "(main_state.exscore() - main_state.exscore_rival())"
        names.add(label)
        if contribution.choice == "custom":
            percent = prefix + ".custom-percent"
            if bindings.get(percent, {}).get("source") != "option.graph-custom-percent":
                raise ValueError("Standard custom artwork has no authored percentage readout")
            values[percent] = "main_state.number(135)"
    elif family == "lightning" and contribution.choice == "percentage":
        percentages = [identifier for identifier, binding in bindings.items()
                       if binding["source"] == "option.pacemaker-percent"]
        if len(percentages) != 1:
            raise ValueError("Lightning percentage artwork requires one authored percentage role")
        percent = percentages[0]
        parents = [n for n in nodes.values() if any(c["id"] == percent for c in n.get("children", []))]
        if len(parents) != 1:
            raise ValueError("Lightning percentage artwork has an ambiguous source container")
        labels = [n for n in parents[0]["children"] if n["type"] == "text"
                  and n.get("properties", {}).get("text") == "PACEMAKER" and n["id"] not in bindings]
        if len(labels) != 1:
            raise ValueError("Lightning percentage artwork has no unique target name role")
        values[percent] = "main_state.number(135)"
        names.add(labels[0]["id"])
    return values, names


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
    target = next(c for c in contributions if c.module == "graph-target")
    target_values, target_names = _target_art_roles(author.family, target)
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
        value = target_values.get(node["id"]) if contribution.module == "graph-target" else None
        if value is None and binding:
            value = number_expression(binding["source"], contribution)
        if binding and value is None:
            gap(contribution, node["id"], "unmapped-text-source", source=binding["source"])
            return
        label = properties.get("text", "")
        is_target_name = contribution.module == "graph-target" and not binding and (
            node["id"] in target_names or label in (
                contribution.choice.upper(), "MAXIMUM", "CUSTOM %", "PERSONAL BEST", "MYBEST"))
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

        grids, zero_gates, rank_states, invalid_fields = {}, {}, {}, {}
        if author.family == "standard" and options.get("frame") == "source20-gmframe0":
            expected = {f"standard.graph.fs20.{presentation}.current.value.fixed-cells",
                        f"standard.graph.fs20.target.{contribution.choice}.{presentation}.total.fixed-cells"}
            for item in walk_nodes(root):
                if item["id"] not in expected:
                    continue
                field = _source20_zero_field(item, contribution, presentation, fonts)
                if field is None:
                    invalid_fields[item["id"]] = "unqualified-original20-leading-zero-field"
                else:
                    grids[field["grid"]] = field
                    zero_gates.update(field["gates"])
            nodes = {item["id"]: item for item in walk_nodes(root)}
            for item in nodes.values():
                if item["id"] not in {f"standard.graph.fs20.{presentation}.line.{rank}" for rank in ("a", "aa", "aaa")}:
                    continue
                states = _source20_rank_states(item, contribution, presentation, numeric, nodes)
                if states is None:
                    for identifier in (item["id"], item["id"] + ".reached"):
                        invalid_fields[identifier] = "unqualified-original20-rank-state"
                else:
                    rank_states.update(states)

        def visit(node, matrix, size, rgba, conditions, scale, reveal=None, blend="alpha", clip=None, fit_branch=None):
            if node["id"] in invalid_fields:
                gap(contribution, node["id"], invalid_fields[node["id"]])
                return
            if node["id"] in grids and fit_branch is None:
                field = grids[node["id"]]
                for index, (factor, interval) in enumerate(_font_sizes(field["scale"])):
                    own_conditions = list(conditions)
                    if interval:
                        low, high = interval
                        if low is not None:
                            own_conditions.append(f"{_MAXIMUM} >= {lua(low)}")
                        if high is not None:
                            own_conditions.append(f"{_MAXIMUM} < {lua(high)}")
                    visit(node, matrix, size, rgba, own_conditions, scale, reveal, blend, clip,
                          (field["grid"], factor, index))
                return
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
                if node["id"] in rank_states and track["id"] == rank_states[node["id"]]["track"]:
                    prop["opacity"] = 1
                    own_conditions.append(rank_states[node["id"]]["condition"])
                elif (fit_branch is not None and node["id"] == fit_branch[0]
                        and track["id"] in grids[node["id"]]["scaleTracks"]):
                    prop[track["property"]] = fit_branch[1]
                elif node["id"] in zero_gates and track["id"] == zero_gates[node["id"]]["track"]:
                    own_conditions.append(zero_gates[node["id"]]["condition"])
                elif clock in context:
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
                identifier = f"graph.{contribution.module}.{node['id']}" + (f".fit{fit_branch[2]}" if fit_branch else "")
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
                if fit_branch is not None and node["id"] in zero_gates:
                    # Match the actual native integer's rounded cells, not a
                    # second independent rounding of the source-size grid.
                    number_id = f"graph.{contribution.module}.{grids[fit_branch[0]]['number']}.{fit_branch[2]}"
                    number = next((v for v in arrays["value"] if v["id"] == number_id), None)
                    main = next((d for d in output["destination"] if d["id"] == number_id), None)
                    if number is None or main is None or (number["align"], number["digit"], number["padding"]) != (0, 10, 0):
                        output["destination"].remove(entry)
                        gap(contribution, node["id"], "unqualified-original20-native-number-cells")
                        return
                    cell = main["dst"][0]
                    step = cell["w"] + number["space"]
                    column = zero_gates[node["id"]]["column"]
                    entry["dst"][0].update(x=cell["x"] + (number["digit"] - 4 + column) * step,
                                          y=cell["y"], w=cell["w"], h=cell["h"])
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
                visit(child, node_matrix, node_size, own_rgba, own_conditions, own_scale, own_reveal, own_blend, clip, fit_branch)

        visit(root, _IDENTITY, (width, height), (1, 1, 1, 1), [], (1, 1))

    if layer in ("all", "foreground") and options.get("graph-visible", True):
        output["gaps"].extend([
            {"feature": "graph", "reason": "native-pb-existence-unavailable",
             "detail": "Official playback APIs merge missing and zero PB. Native PB=0 is retained; no current-score mirror is fabricated."},
            {"feature": "graph", "reason": "native-target-selection",
             "detail": "Source target art follows the host target name/ref 3 and total score/rate 151/115. Percentage readouts use the host's integer percentage/ref 135, not an independent author percentage control. Artwork choices do not replace host target policy or reproduce the source pace algorithm. Standard NEXT/NEXT+ retain their catalog identities as AAA artwork aliases under this host-target contract. Available original crossing movies are handled by the separate public-state feedback adapter."},
        ])
    return output
