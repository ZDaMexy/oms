"""Original event artwork driven by observed public beatoraja state.

Custom timers own only 10000--10015. They never write gameplay timers or scores.
An observed render-frame edge is not claimed to be the original engine event
timestamp; snapshots and retries establish a baseline without inventing edges.
"""

from beatoraja_author import material_bindings, material_templates, walk_nodes
from beatoraja_native import Lua, colour, lua, node_rect, scene_properties


GAUGE_TIMER = 10000
SCORE_TIMERS = {name: 10010 + i for i, name in enumerate(("update", "mybest", "target", "aaa", "aa", "a"))}
MAXIMUM = "(main_state.number(106) * 2)"
HAS_MAXIMUM = "(main_state.number(106) > 0 and main_state.number(106) < 2147483647)"


def gauge_timer():
    return {"id": GAUGE_TIMER, "timer": Lua("""(function()
  local off=main_state.timer_off_value
  local previous, play, kind, previous_time=nil,nil,nil,nil
  local origin=off
  return function()
    local now=main_state.time()
    local active=main_state.timer(41)
    local value=main_state.gauge()
    local current_kind=main_state.gauge_type()
    if active==off or play~=active or kind~=current_kind or previous==nil or now<previous_time then
      origin=off
    elseif value>previous then origin=now end
    previous,play,kind,previous_time=value,active,current_kind,now
    return origin
  end
end)()""")}


def score_timer(name):
    values = {"mybest": ("main_state.exscore_best()", ">"),
              "target": ("main_state.exscore_rival()", ">"),
              **{rank: (f"math.ceil({MAXIMUM} * {weight} / 9)", ">=")
                 for rank, weight in (("aaa", 8), ("aa", 7), ("a", 6))}}
    selected = list(values) if name == "update" else [name]
    thresholds = "{" + ",".join("{" + values[k][0] + "," + lua(values[k][1]) + "}" for k in selected) + "}"
    return {"id": SCORE_TIMERS[name], "timer": Lua("""(function()
  local off=main_state.timer_off_value
  local previous,play,maximum,previous_time=nil,nil,nil,nil
  local old,spent={},{}
  local origin=off
  local function reached(score, threshold)
    return threshold[1]>0 and (threshold[2]=='>' and score>threshold[1] or threshold[2]=='>=' and score>=threshold[1])
  end
  return function()
    local now=main_state.time()
    local active=main_state.timer(41)
    local score=main_state.exscore()
    local total=""" + MAXIMUM + """
    local thresholds=""" + thresholds + """
    local reset=active==off or previous==nil or play~=active or maximum~=total or now<previous_time or score<previous
    for i,t in ipairs(thresholds) do if old[i]~=t[1] then reset=true end end
    if reset then
      origin=off
      for i,t in ipairs(thresholds) do spent[i]=reached(score,t) end
    elseif """ + HAS_MAXIMUM + """ then
      for i,t in ipairs(thresholds) do
        if not spent[i] and reached(score,t) then
          spent[i]=true
          if not reached(previous,t) then origin=now end
        end
      end
    end
    for i,t in ipairs(thresholds) do old[i]=t[1] end
    previous,play,maximum,previous_time=score,active,total,now
    return origin
  end
end)()""")}


def _expression(source, scene, seen=()):
    known = {"score.value": "main_state.exscore()", "score.maximum": MAXIMUM,
             "score.maximum-available": HAS_MAXIMUM, "score.rank-available": HAS_MAXIMUM,
             "gauge.clear-state": "(main_state.option(1240) and 'clear' or 'uncleared')",
             "gauge.increase-available": f"(main_state.timer({GAUGE_TIMER}) ~= main_state.timer_off_value)",
             **{f"score.rank-{rank}": f"math.ceil({MAXIMUM} * {weight} / 9)"
                for rank, weight in (("aaa", 8), ("aa", 7), ("a", 6))}}
    if isinstance(source, (int, float)):
        return lua(source)
    if source in known:
        return known[source]
    if not source.startswith("value.") or source in seen:
        raise ValueError("Unsupported feedback state: " + source)
    definition = next(v for v in scene.get("numericSources", []) if v["id"] == source[6:])
    a = _expression(definition["left"], scene, (*seen, source))
    b = _expression(definition["right"], scene, (*seen, source))
    op = definition["operation"]
    if op in ("min", "max"):
        return f"math.{op}({a},{b})"
    if op in ("add", "subtract", "multiply"):
        return f"({a} " + {"add": "+", "subtract": "-", "multiply": "*"}[op] + f" {b})"
    raise ValueError("Unsupported feedback numeric operation: " + op)


def _condition(operation, scene):
    expression = _expression(operation["source"], scene)
    if operation["kind"] == "variants":
        cases = {c["key"]: c["value"] for c in operation["cases"]}
        expression = ("(function() local cases=" + lua(cases) + "; local value=cases[tostring(" + expression
                      + ")]; if value==nil then return " + lua(operation["default"]) + " end; return value end)()")
    if operation["property"] == "opacity":
        # These original integer rank differences are clamped to 0/1.
        expression = f"(({expression}) > 0)"
    return expression


def feedback_plan(author, options, keymode, presentation, canvas, kind):
    if kind not in ("gauge", "graph"):
        raise ValueError("Unknown original feedback kind")
    result = {"customTimers": [], "draws": [], "gaps": [], "handled_node_ids": [],
              "handled_root_ids": [], "handled_template_ids": []}
    frame = author.contribution("frame", options)
    context = {"layout.ruleset": "bms", "layout.presentation": presentation,
               **{"option." + k: v for k, v in options.items()}}
    selected = []
    if kind == "gauge":
        for binding in material_bindings(frame, keymode, presentation):
            if binding["target"]["kind"] == "global" and binding["slot"] == "decoration" and binding["mode"] == "Provide":
                selected.extend((frame, None, root) for root in material_templates(frame, "decoration", binding["resource"]))
    else:
        from beatoraja_graph import _select_templates
        _, selected = _select_templates(author, options, keymode, presentation)
    used_timers = set()
    for contribution, template_id, root in selected:
        animations = {a["id"]: a for a in contribution.manifest.get("animations", [])}
        programs = {name: author.json(item["path"]) for name, item in animations.items()
                    if kind == "gauge" and author.family == "standard" and name.startswith("sg.f")}
        targets = {n["id"] for n in walk_nodes(root) if n["type"] == "recorded-animation" and
                   (bool(n.get("scoreCrossing")) if kind == "graph" else
                    n["animation"] in programs and (programs[n["animation"]]["trigger"] == "gauge.increase" or
                    n["animation"].endswith(".loop.clip.initial") and programs[n["animation"]]["trigger"] == "gameplay.attach"))}
        if not targets:
            continue
        if kind == "graph":
            result["handled_root_ids"].append(root["id"])
            result["handled_template_ids"].append(template_id)
        else:
            # Skip exactly the dynamic Gauge feedback subtree in generic art.
            for child in walk_nodes(root):
                operations = [o for field in ("variants", "bindings") for o in contribution.scene.get(field, [])
                              if o["target"] == child["id"] and o["source"] == "gauge.clear-state"]
                if operations and any(n["id"] in targets for n in walk_nodes(child)):
                    result["handled_node_ids"].append(child["id"])
        def visit(node, parent, rgba, conditions):
            if not any(n["id"] in targets for n in walk_nodes(node)):
                return
            prop, dynamic = scene_properties(node, contribution.scene, context)
            conditions = list(conditions)
            if prop.get("visible", True) is False and not any(o["property"] == "visible" for o in dynamic):
                return
            for operation in dynamic:
                if operation["property"] not in ("visible", "opacity"):
                    raise ValueError("Unmapped feedback geometry/resource binding")
                conditions.append(_condition(operation, contribution.scene))
                prop[operation["property"]] = True if operation["property"] == "visible" else 1
            if prop.get("rotation", 0) or node.get("effects"):
                raise ValueError("Feedback transform requires a qualified adapter")
            rect = node_rect(prop, parent)
            rgba = colour(prop, rgba)
            if node["id"] in targets:
                if kind == "gauge":
                    timer = GAUGE_TIMER if programs[node["animation"]]["trigger"] == "gauge.increase" else 0
                    used_timers.add(GAUGE_TIMER)
                else:
                    name = node["id"].rsplit(".", 1)[-1]
                    if name not in SCORE_TIMERS:
                        raise ValueError("Unqualified source graph notification")
                    if author.json(animations[node["animation"]]["path"])["trigger"] != "score.crossing":
                        raise ValueError("Source crossing clip uses an unrelated trigger")
                    thresholds = node["scoreCrossing"]["thresholds"]
                    comparisons = [t["comparison"] for t in thresholds]
                    expected = [">", ">", ">=", ">=", ">="] if name == "update" else [">" if name in ("mybest", "target") else ">="]
                    if [{"greater": ">", "at-least": ">="}[c] for c in comparisons] != expected:
                        raise ValueError("Source crossing comparison changed")
                    timer = SCORE_TIMERS[name]
                    used_timers.add(timer)
                result["draws"].append({"manifest": contribution.manifest_path, "animation": node["animation"],
                                        "rect": rect, "rgba": rgba, "timer": timer, "conditions": conditions})
            for child in sorted(node.get("children", []), key=lambda n: -n.get("properties", {}).get("z", 0)):
                visit(child, rect, rgba, conditions)
        visit(root, canvas, (1, 1, 1, 1), [])
    if GAUGE_TIMER in used_timers:
        result["customTimers"].append(gauge_timer())
    result["customTimers"].extend(score_timer(name) for name, timer in SCORE_TIMERS.items() if timer in used_timers)
    if used_timers:
        result["gaps"].append({"code": "native-feedback-render-edge-adaptation", "informational": True, "kind": kind,
            "detail": "Original frame sequence uses positive public Gauge/EX changes observed once per render, not unavailable engine event timestamps. Snapshots, retry, threshold changes and zero/missing PB establish a baseline; they never invent crossings. Several changes between renders collapse to the latest observed edge."})
        if kind == "graph":
            result["gaps"].append({"code": "native-feedback-target-ownership", "informational": True,
                "detail": "PB and A/AA/AAA are real public totals; the target cut-in follows the actual native host target, consistent with the target graph. The source artwork selector does not replace host target policy."})
    return result
