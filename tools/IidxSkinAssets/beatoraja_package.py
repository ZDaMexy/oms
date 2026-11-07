"""Package one frozen Omni family as native, independently selected Lua shards.

The header contains only real author choices. A choice is advertised only when
its adapter is available in every included frame/mode. Missing bakes and other
unmapped choices remain in package-receipt.json; they never silently substitute
another author's choice. Frame decoration/HUD limitations are also retained.

The build is additive in module choices, not their Cartesian product. Notes and
charge notes share one scanned, transparent-padded cap height. Lua combines
their disjoint NoteSet fields with the selected frame's lane geometry. Shards
are content-addressed within this output only, and contain fresh table factories
so Lua's require cache cannot retain a previously merged selection.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import itertools
import json
from decimal import Decimal
from pathlib import Path
from typing import Any

from beatoraja_author import AuthorPackage, applies, layout_for, source_path
from beatoraja_export import ANIMATION_RUNTIME, MODE, Composer, read_bakes
from beatoraja_materials import CAP_FIELDS, NOTE_FIELDS, collect_note_materials
from beatoraja_native import AssetSink, Lua, lua


ARRAYS = ("source", "font", "image", "imageset", "value", "floatvalue", "text",
          "slider", "graph", "gaugegraph", "judgegraph", "bpmgraph", "judge",
          "hiddenCover", "liftCover", "customTimers")
SINGLETONS = ("note", "gauge", "bga")
TIMINGS = ("loadend", "playstart", "close", "fadeout", "finishmargin")
NOTE_IMAGE_FIELDS = (*NOTE_FIELDS, "mine", "hidden", "processed", "lnactive",
                     "hcnactive", "hcndamage", "hcnreactive")
MODULES = ("notes", "charge-notes", "judgement", "lane-cover", "lift-art",
           "key-beam", "explosion", "full-combo", "bga-frame", "graph-target",
           "turntable", "graph-area", "timing-display")
LABELS = {"notes": "Notes", "charge-notes": "Charge Notes", "judgement": "Judgement",
          "lane-cover": "Lane Cover", "lift-art": "Lift Art", "key-beam": "Key Beam",
          "explosion": "Explosion", "full-combo": "Full Combo", "bga-frame": "BGA Frame",
          "graph-target": "Graph Target Artwork", "turntable": "Turntable", "graph-area": "Graph Area",
          "timing-display": "Timing Display"}
STAGES = ("frame", "bga-frame", "graph-background", "graph-area", "graph-target", "decorations", "turntable", "key-beam",
          "hit-line", "note-geometry", "notes", "charge-notes", "explosion", "long-note-effects", "key-lights",
          "lane-cover", "lift-art", "reading", "judgement", "timing-display", "gauge", "hud", "full-combo", "events")
CONTROL_GROUPS = {"notes": ("note-size",),
                  "charge-notes": ("charge-size", "hcn-color", "hcn-display"),
                  "long-note-effects": ("charge-size", "hcn-color", "hcn-display"),
                  "judgement": ("judgement-display", "judgement-position"),
                  "key-beam": ("beam-length",), "timing-display": ("timing-position", "judgement-position"),
                  "events": ("ready-animation", "get-ready"), "reading": ("lift-readout",)}
CONTROL_LABELS = {"note-size": "Note Size", "charge-size": "Charge Size", "hcn-color": "HCN Colour",
                  "hcn-display": "HCN Display", "judgement-display": "Judgement Display",
                  "judgement-position": "Judgement Position", "beam-length": "Beam Length",
                  "ready-animation": "Ready Animation", "get-ready": "Get Ready", "timing-position": "Timing Position",
                  "lift-readout": "Lift Readout"}
NONBLOCKING = {"mine-art-unavailable", "ln-cn-released-body-not-separate",
               "shared-note-height-needs-padding", "auto-assist-material-condition",
               "native BMS mine addition", "judge-combo-fit-track", "native-font-spacing-rounded",
               "native-judge-state-ownership", "native-timing-feedback-adaptation",
               "native-scratch-motion", "native-decoration-integer-geometry", "fixed-decoration-quad-rasterized",
               "fixed-affine-quad-rasterized", "native-cover-animation-objects", "native-graph-area-animation-objects",
               "native-reading-cabinet-edge-adaptation", "native-reading-value-adaptation",
               "native-reading-fixed-clip-quantisation", "native-feedback-render-edge-adaptation",
               "native-feedback-target-ownership", "native-numeric-advance-quantization",
               "native-hold-lane-state-adaptation", "native-hold-render-edge-adaptation"}


def stage_module(stage):
    # Long-note lighting is selected by the same real charge-note contribution.
    return "charge-notes" if stage == "long-note-effects" else stage


def tool_file_hashes(root: Path | None = None) -> dict[str, str]:
    """Pin the local adapter set, including newly added wrapper dependencies."""
    root = root or Path(__file__).resolve().parent
    return {path.name: hashlib.sha256(path.read_bytes()).hexdigest()
            for path in sorted(root.glob("beatoraja_*.py"))}


_IMPORTED_TOOL_FILES = tool_file_hashes()


def verify_tool_files(expected: dict[str, str], root: Path | None = None):
    actual = tool_file_hashes(root)
    changed = sorted(name for name in expected.keys() | actual.keys() if expected.get(name) != actual.get(name))
    if changed:
        raise ValueError("Adapter source changed during this process; restart the build: " + ", ".join(changed))


def _canonical(value):
    if isinstance(value, dict):
        return {key: _canonical(value[key]) for key in sorted(value)}
    if isinstance(value, list):
        return [_canonical(item) for item in value]
    return value


def namespace_fragment(fragment: dict, prefix: str) -> dict:
    """Rename actual object IDs/references; never src, numeric refs or Lua code."""
    result = copy.deepcopy(fragment)
    identifiers = set()
    for field in ARRAYS:
        if field == "source":
            continue
        identifiers.update(item["id"] for item in result.get(field, [])
                           if isinstance(item.get("id"), str) and item["id"] != "native.fallback")
    for field in SINGLETONS:
        item = result.get(field, {})
        if isinstance(item.get("id"), str):
            identifiers.add(item["id"])
    mapping = {identifier: prefix + "." + identifier for identifier in identifiers}

    def rewrite(value, key=None):
        if isinstance(value, Lua):
            return value
        if isinstance(value, dict):
            return {k: rewrite(v, k) for k, v in value.items()}
        if isinstance(value, list):
            return [rewrite(item, key) for item in value]
        if isinstance(value, str) and key in ("id", "font", "images", "nodes", *NOTE_IMAGE_FIELDS):
            return mapping.get(value, value)
        return value

    # A font/source ID is a declaration in its own namespace; source records
    # and native.fallback are shared exactly, not renamed along with objects.
    sources = result.pop("source", [])
    result = rewrite(result)
    if sources:
        result["source"] = sources
    return result


def note_fragment(skin: dict, part: str) -> dict:
    """Keep one disjoint NoteSet patch and only images that it references."""
    result = copy.deepcopy(skin)
    fields = {"note"} if part == "notes" else set(NOTE_FIELDS) - {"note"}
    if part == "note-geometry":
        fields = set(skin["note"]) - set(NOTE_FIELDS)
    result["note"] = {key: value for key, value in result["note"].items() if key in fields}
    if part != "note-geometry":
        result["destination"] = []
    used = set()

    def visit(value, key=None):
        if isinstance(value, dict):
            for k, v in value.items():
                visit(v, k)
        elif isinstance(value, list):
            for v in value:
                visit(v, key)
        elif isinstance(value, str) and key in ("id", *NOTE_IMAGE_FIELDS):
            used.add(value)

    visit(result["note"])
    result["image"] = [item for item in result["image"] if item["id"] in used]
    sources = {item["src"] for item in result["image"]}
    result["source"] = [item for item in result["source"] if item["id"] in sources]
    return result


def extract_fragment(skin: dict, baseline: dict) -> dict:
    output = {field: skin[field] for field in (*ARRAYS, *SINGLETONS, "destination")
              if skin.get(field)}
    output.update({field: skin[field] for field in TIMINGS if skin[field] != baseline[field]})
    return output


class Shards:
    def __init__(self, output: Path):
        self.root = output / "shards"
        self.root.mkdir()
        self.records = {}

    def add(self, fragment: dict) -> str:
        canonical = _canonical(fragment)
        digest = hashlib.sha256(lua(canonical).encode("utf-8")).hexdigest()
        module = "shards/" + digest
        if digest not in self.records:
            payload = namespace_fragment(canonical, "p" + digest[:24])
            if payload.get("customTimers"):
                signatures, timers = {}, {}
                for timer in payload["customTimers"]:
                    identifier = timer["id"]
                    if not isinstance(identifier, int) or isinstance(identifier, bool) or not 10000 <= identifier <= 19999:
                        raise ValueError("Custom timers require an official numeric custom-timer ID")
                    signature = hashlib.sha256(lua(_canonical(timer)).encode("utf-8")).hexdigest()
                    if identifier in signatures and signatures[identifier] != signature:
                        raise ValueError("Conflicting custom timer definition in a fragment: " + str(identifier))
                    signatures[identifier] = signature
                    timers.setdefault(identifier, timer)
                payload["customTimers"] = list(timers.values())
                payload["_customTimerSignatures"] = signatures
            text = ANIMATION_RUNTIME + "\nreturn function() return " + lua(payload) + " end\n"
            data = text.encode("utf-8")
            (self.root / (digest + ".lua")).write_bytes(data)
            self.records[digest] = {"path": module + ".lua", "bytes": len(data),
                                    "sha256": hashlib.sha256(data).hexdigest(),
                                    "destinations": len(fragment.get("destination", []))}
        return module


def stable_option_ids(author: AuthorPackage) -> dict[str, dict[str, int]]:
    """Unavailable entries reserve their slot, so adding a bake cannot remap saved choices."""
    next_op, result = 1000, {}
    for module in author.modules:
        result[module] = {}
        for choice in author.modules[module]["choices"]:
            result[module][choice["value"]] = next_op
            next_op += 1
    # Reserve all finite author controls, even those not currently exposed.
    for definition in getattr(author, "manifest", {}).get("options", []):
        if definition["id"] == "layout":
            result["layout"] = {"p1": 920, "p2": 921}
            continue
        result[definition["id"]] = {}
        for choice in control_choices(definition):
            result[definition["id"]][choice["value"]] = next_op
            next_op += 1
    return result


def control_choices(definition: dict) -> list[dict]:
    kind = definition["type"]
    if kind == "choice":
        return definition["choices"]
    if kind == "toggle":
        return [{"value": True, "label": "ON"}, {"value": False, "label": "OFF"}]
    if kind != "number":
        raise ValueError("Unknown source control type: " + kind)
    start, stop, step = (Decimal(str(definition[key])) for key in ("min", "max", "step"))
    if step <= 0 or stop < start or (stop - start) % step:
        raise ValueError("Source numeric choices must span their declared step exactly")
    values = [start + step * i for i in range(int((stop - start) / step) + 1)]
    return [{"value": int(v) if v == int(v) else float(v), "label": format(v.normalize(), "f")} for v in values]


def local_controls(author: AuthorPackage, stage: str, keymode: str) -> dict[str, list[dict]]:
    module = stage_module(stage)
    if module in MODULES and module not in author.modules and stage != "timing-display":
        return {}
    definitions = {item["id"]: item for item in author.manifest.get("options", [])}
    return {name: control_choices(definitions[name]) for name in CONTROL_GROUPS.get(stage, ())
            if name in definitions and applies(definitions[name], keymode)}


def control_combinations(definitions: dict[str, list[dict]]) -> list[dict]:
    return [dict(zip(definitions, values)) for values in itertools.product(
        *([item["value"] for item in choices] for choices in definitions.values()))]


def insert_selection(paths: dict, values: list, path: str):
    current = paths
    for value in values[:-1]:
        current = current.setdefault(value, {})
    current[values[-1]] = path


def available_selections(author, stage, coverage, modes, frames):
    """Keep working defaults when an optional size/color still lacks its bake.

    Add a control value only when the whole small, within-component combination
    remains supported across the advertised module choices, frames and modes.
    No component loses its working default just because a new control is partial.
    """
    stages = ("charge-notes", "long-note-effects") if stage_module(stage) == "charge-notes" else (stage,)
    records = [r for r in coverage if r["stage"] in stages]
    definitions = {item["id"]: item for item in author.manifest.get("options", [])}
    controls = {name: definitions[name] for name in CONTROL_GROUPS.get(stage, ()) if name in definitions}
    defaults = tuple(definition["default"] for definition in controls.values())
    parts = {}
    for record in records:
        identity = (record["mode"], record["frame"], record["choice"],
                    tuple(record["options"].get(name, definitions[name]["default"]) for name in controls))
        parts.setdefault(identity, {})[record["stage"]] = record["available"] and not blocking_gaps(record["gaps"], record["stage"])
    grid = {identity: all(values.get(part, False) for part in stages) for identity, values in parts.items()}
    module = stage_module(stage)
    options = ([c["value"] for c in author.modules[module]["choices"]] if module in author.modules else [None])
    complete = [choice for choice in options if all(grid.get((mode, frame, choice, defaults), False)
                                                    for mode, frame in itertools.product(modes, frames))]
    allowed = {name: [definition["default"]] for name, definition in controls.items()}
    if complete:
        for name, definition in controls.items():
            for choice in control_choices(definition):
                value = choice["value"]
                if value in allowed[name]:
                    continue
                trial = {**allowed, name: [*allowed[name], value]}
                combinations = list(itertools.product(*trial.values()))
                if all(grid.get((mode, frame, module_choice, controls_value), False)
                       for mode, frame, module_choice, controls_value in itertools.product(modes, frames, complete, combinations)):
                    allowed = trial
    return complete, allowed


def _choices(author: AuthorPackage, module: str, keymode: str):
    definition = author.modules.get(module)
    if not definition or not applies(definition, keymode):
        return []
    return [c for c in definition["choices"] if applies(c, keymode)]


def _options(author, mode, frame, module=None, choice=None):
    options = author.options(MODE[mode][0], mode, {"frame": frame})
    if module:
        options[module] = choice
    for name in ("frame-opening", "entrance-animation"):
        if name in options:
            options[name] = False
    return options


def scan_cap_height(author: AuthorPackage, modes: list[str], frames: list[str]) -> int:
    """Linear scan, including real mode/frame variants; no paired selection grid."""
    height, seen = 0, set()
    for mode in modes:
        for frame in frames:
            base = _options(author, mode, frame)
            for module in ("notes", "charge-notes"):
                for choice in _choices(author, module, MODE[mode][0]):
                    for controls in control_combinations(local_controls(author, module, MODE[mode][0])):
                        options = {**base, module: choice["value"], **controls}
                        parts = [author.contribution(name, options) for name in ("notes", "charge-notes")]
                        signature = (mode, *(p.manifest_path for p in parts), *(p.scene_path for p in parts), *controls.items())
                        if signature in seen:
                            continue
                        seen.add(signature)
                        recipe = collect_note_materials(author, options, MODE[mode][0], mode, lambda relative: relative)
                        images = {item["id"]: item for item in recipe["images"]}
                        height = max(height, *(images[i]["h"] for field in CAP_FIELDS for i in recipe["note"][field]))
    if height <= 0:
        raise ValueError("No source note cap height was found")
    return height


def blocking_gaps(gaps: list[dict], module: str) -> list[dict]:
    """Known host semantic limits stay visible; absent artwork is not advertised."""
    return [gap for gap in gaps if gap.get("code", gap.get("feature")) not in NONBLOCKING
            and not (module == "graph-target" and gap.get("feature") == "graph"
                     and gap.get("reason") in ("native-target-selection", "native-pb-existence-unavailable",
                                                "recorded-graph-cutins-require-bake"))]


def has_component_output(fragment: dict, module: str, choice: str | None = None, options=None) -> bool:
    if module == "graph-area" and choice == "blank":
        return True  # This is the author's explicit absence of Graph Area art.
    if module == "timing-display":
        if choice == "off" or options and (options.get("timing-position") == "off" or options.get("judgement-position") == "off"):
            return True
    if module == "notes":
        return bool(fragment.get("note", {}).get("note"))
    if module == "charge-notes":
        return bool(fragment.get("note", {}).get("lnstart"))
    if module == "long-note-effects":
        # Absence is valid only because the adapter rejects an empty Provide.
        # Lightning's source has no provided effect.long-note materials.
        return True
    if module == "judgement":
        if options and (options.get("judgement-display") in (False, "off") or options.get("judgement-position") == "off"):
            return True
        return bool(fragment.get("judge"))
    if module == "lane-cover":
        return bool(fragment.get("slider")) and bool(fragment.get("hiddenCover"))
    if module == "lift-art":
        return bool(fragment.get("liftCover"))
    return bool(fragment.get("destination"))


class CompositionCache:
    """Reuse only qualified frame-independent material or equal-host Judge output.

    The cache belongs to this one author/sink/build. Actual contribution paths,
    all non-frame option values and referenced option.frame values are retained.
    Judge additionally keys the actual stage rectangles. Frame, Gauge, Graph,
    covers and event effects are deliberately composed normally.
    """
    def __init__(self, author: AuthorPackage):
        self.author = author
        self.values = {}
        self.dependencies = {}
        self.hits = 0
        self.misses = 0

    def key(self, stage, mode, frame, options, cap_height):
        if stage not in ("notes", "charge-notes", "judgement"):
            return None
        modules = ("judgement",) if stage == "judgement" else ("notes", "charge-notes")
        parts, depends_on_frame = [], False
        for module in modules:
            contribution = self.author.contribution(module, options)
            identity = (contribution.module, contribution.choice, contribution.manifest_path,
                        contribution.scene_path, contribution.ini_path)
            if identity not in self.dependencies:
                self.dependencies[identity] = '"option.frame"' in json.dumps(contribution.scene)
            depends_on_frame |= self.dependencies[identity]
            parts.append(identity)
        context = tuple(sorted((key, value) for key, value in options.items() if key != "frame" or depends_on_frame))
        geometry = tuple(tuple(stage["rect"]) for stage in layout_for(frame, MODE[mode][0], mode)["stages"]) if stage == "judgement" else ()
        return (stage, mode, cap_height, tuple(parts), context, geometry)


def compose_stage(c: Composer, stage: str, frame, cap_height, font):
    """One native pass, kept identical for direct composition and cache misses."""
    initial = copy.copy(c.skin)
    if stage == "frame": c.frame(frame)
    elif stage == "bga-frame":
        c.bga(frame)
        if stage in c.author.modules: c.bga_frame(frame)
    elif stage == "graph-background": c.graph(layer="background")
    elif stage == "graph-area": c.graph(layer="art")
    elif stage == "graph-target": c.graph(layer="foreground")
    elif stage == "decorations": c.decorations(frame)
    elif stage == "turntable": c.turntable(frame)
    elif stage == "key-beam": c.key_beams(frame)
    elif stage == "hit-line": c.hit_line(frame)
    elif stage in ("notes", "charge-notes", "note-geometry"): c.notes(frame, cap_height=cap_height)
    elif stage == "explosion": c.explosions(frame)
    elif stage == "long-note-effects": c.long_note_effects(frame)
    elif stage == "key-lights": c.key_lights(frame)
    elif stage == "lane-cover": c.covers(frame, kinds=("sudden", "hidden"))
    elif stage == "lift-art": c.covers(frame, kinds=("lift",))
    elif stage == "reading": c.reading(frame)
    elif stage == "judgement": c.widgets(frame, include_gauge=False)
    elif stage == "timing-display": c.timing_display(frame)
    elif stage == "gauge": c.widgets(frame, include_judge=False)
    elif stage == "hud": c.hud(frame, font)
    elif stage == "full-combo": c.full_combo(frame)
    elif stage == "events": c.event_clips(frame)
    else: raise ValueError("Unknown native stage: " + stage)
    skin = c.finish()
    if stage in ("notes", "charge-notes", "note-geometry"):
        skin = note_fragment(skin, stage)
    return extract_fragment(skin, initial)


MERGE_RUNTIME = r'''
local function append(skin, fragment)
    for _, field in ipairs(ARRAY_FIELDS) do
        local values = fragment[field]
        if values then
            skin[field] = skin[field] or {}
            local known = skin._seen[field] or {}
            skin._seen[field] = known
            for _, value in ipairs(values) do
                if field == 'customTimers' then
                    local signature=assert(fragment._customTimerSignatures[value.id], 'Missing custom timer identity')
                    local previous=skin._customTimerSignatures[value.id]
                    if previous and previous ~= signature then error('Conflicting custom timer: '..value.id) end
                    skin._customTimerSignatures[value.id]=signature
                end
                if not known[value.id] then
                    table.insert(skin[field], value)
                    known[value.id] = value
                elseif (field == 'source' or field == 'font') and known[value.id].path ~= value.path then
                    error('Conflicting shared asset: '..value.id)
                end
            end
        end
    end
    for _, field in ipairs({'note','gauge','bga'}) do
        if fragment[field] then
            skin[field] = skin[field] or {}
            for key, value in pairs(fragment[field]) do
                if skin[field][key] ~= nil then error('Overlapping native '..field..' field '..key) end
                skin[field][key] = value
            end
        end
    end
    for _, field in ipairs({'loadend','playstart','close','fadeout','finishmargin'}) do
        if fragment[field] then skin[field] = math.max(skin[field] or 0, fragment[field]) end
    end
    for _, destination in ipairs(fragment.destination or {}) do
        table.insert(skin.destination, destination)
    end
end
'''


def entry_script(header: dict, modes: list[str], frames: list[str], plan: dict,
                 choices: dict, defaults: dict, baseline: dict, option_ids=None) -> str:
    """Header-only reads do not require shards or allocate any image/font table."""
    maps, properties, next_op = {}, [], 1000
    if modes != ["dp"]:
        properties.append({"name": "Play Side", "item": [
            {"name": mode.upper(), "op": 920 if mode == "p1" else 921} for mode in modes],
            "def": modes[0].upper()})
    for module in ("frame", *MODULES, *CONTROL_LABELS):
        entries = choices.get(module, [])
        if not entries:
            continue
        label = "Frame" if module == "frame" else LABELS.get(module, CONTROL_LABELS.get(module))
        item, mapping = [], {}
        for choice in entries:
            op = option_ids[module][choice["value"]] if option_ids else next_op
            item.append({"name": choice["label"], "op": op})
            mapping[op] = choice["value"]
            next_op += 1
        properties.append({"name": label, "item": item,
                           "def": next(c["label"] for c in entries if c["value"] == defaults[module])})
        maps[module] = {"label": label, "values": mapping, "default": defaults[module]}
    header = {**header, "property": properties}
    text = "local header=" + lua(header) + "\nif not skin_config or skin_config.onlyload then return header end\n"
    text += "local selections=" + lua(maps) + "\nlocal choices={}\n"
    text += "for module, spec in pairs(selections) do\n local op=(skin_config.option or {})[spec.label]\n if op == nil then choices[module]=spec.default else choices[module]=spec.values[op] end\n if choices[module] == nil then error('Unknown native option for '..spec.label) end\nend\n"
    if modes == ["dp"]:
        text += "local mode='dp'\n"
    else:
        text += "local sides=" + lua({920 if m == "p1" else 921: m for m in modes}) + "\n"
        text += "local side=(skin_config.option or {})['Play Side']\nlocal mode=side == nil and " + lua(modes[0]) + " or sides[side]\nif not mode then error('Unknown Play Side') end\n"
    text += "local plan=" + lua(plan) + "\nlocal stages=" + lua(STAGES) + "\nlocal ARRAY_FIELDS=" + lua(ARRAYS) + "\n"
    text += MERGE_RUNTIME
    text += "local skin=" + lua(baseline) + "\nskin.property=header.property\nskin.destination={}\nskin._seen={}\nskin._customTimerSignatures={}\n"
    text += "local selected=assert(assert(plan[mode], 'Missing mode')[choices.frame], 'Missing frame')\n"
    text += "for _, stage in ipairs(stages) do\n local spec=selected[stage]\n local path=spec\n if type(spec)=='table' then\n  path=spec.paths\n  for _,selector in ipairs(spec.selectors) do\n   local key=choices[selector]\n   if key == nil then key=spec.defaults[selector] end\n   if key == nil then path=nil; break end\n   path=assert(path[key], 'Missing native shard for '..stage..'/'..selector)\n  end\n end\n if path then append(skin,require(path)()) end\nend\n"
    text += "skin._seen=nil\nskin._customTimerSignatures=nil\nreturn skin\n"
    return text


def export_package(author: AuthorPackage, output: Path, *, modes=None, frames=None,
                   bake_roots=(), bake_overrides=(), native_font: Path | None = None,
                   module_choices: dict[str, list[str]] | None = None) -> dict:
    verify_tool_files(_IMPORTED_TOOL_FILES)
    tool_files = tool_file_hashes()
    modes = list(modes or MODE)
    frames = list(frames or [c["value"] for c in author.modules["frame"]["choices"]])
    if len(set(modes)) != len(modes) or any(mode not in MODE for mode in modes):
        raise ValueError("Modes must be distinct p1/p2/dp values")
    actual_frames = {c["value"] for c in author.modules["frame"]["choices"]}
    if not frames or len(set(frames)) != len(frames) or not set(frames) <= actual_frames:
        raise ValueError("Frames must be distinct choices from this frozen author")
    for module, values in (module_choices or {}).items():
        if module not in MODULES or module not in author.modules or not values or len(set(values)) != len(values):
            raise ValueError("Invalid requested module subset: " + module)
        if not set(values) <= {c["value"] for c in author.modules[module]["choices"]}:
            raise ValueError("Unknown frozen author choice: " + module)
    if output.exists():
        raise ValueError("Package output must be a fresh directory")
    output.mkdir(parents=True)
    sink = AssetSink(author.root, output)
    font = sink.add_native_font(native_font) if native_font else None
    baked = read_bakes(list(bake_roots), author, list(bake_overrides))
    shards = Shards(output)
    cache = CompositionCache(author)
    cap_height = scan_cap_height(author, modes, frames)
    plan, coverage, baseline = {}, [], None
    shared_font = {"id": "native.fallback", "path": font} if font else None
    for mode in modes:
        plan[mode] = {}
        for theme in frames:
            options = _options(author, mode, theme)
            frame = author.contribution("frame", options)
            plan[mode][theme] = selected = {}
            # Graph roots must be excluded from both generic frame art and HUD.
            graph = Composer(author, sink, options, mode, baked)
            graph.graph()
            roots = graph.handled_roots
            if baseline is None or mode == "dp":
                baseline = {key: value for key, value in graph.skin.items()
                            if key not in (*ARRAYS, *SINGLETONS, "destination")}
            for stage in STAGES:
                module = stage_module(stage)
                is_module = module in MODULES and module in author.modules
                entries = _choices(author, module, MODE[mode][0]) if is_module else [None]
                if is_module and module_choices and module in module_choices:
                    entries = [c for c in entries if c["value"] in module_choices[module]]
                controls_definition = local_controls(author, stage, MODE[mode][0])
                combinations = control_combinations(controls_definition)
                selectors = ([module] if is_module else []) + list(controls_definition)
                stage_choices = {}
                for choice, controls in itertools.product(entries, combinations):
                    opts = {**options, **({module: choice["value"]} if choice else {}), **controls}
                    c = Composer(author, sink, opts, mode, baked)
                    c.handled_roots.update(roots)
                    cache_key = cache.key(stage, mode, frame, opts, cap_height)
                    cached = cache.values.get(cache_key) if cache_key is not None else None
                    if cached:
                        cache.hits += 1
                        fragment, c.gaps, path = cached
                    else:
                        if cache_key is not None: cache.misses += 1
                        fragment = compose_stage(c, stage, frame, cap_height, font)
                        if any(t.get("font") == "native.fallback" for t in fragment.get("text", [])):
                            if not shared_font:
                                raise ValueError("An explicit --native-font is required for this source's native host labels")
                            fragment.setdefault("font", []).append(shared_font)
                        if (is_module or stage == "timing-display" and controls_definition) and not has_component_output(
                                fragment, stage, choice["value"] if choice else None, opts):
                            c.gap("selected component emitted no native output", module=stage, choice=choice["value"] if choice else None)
                        path = None
                    blockers = blocking_gaps(c.gaps, stage) if is_module or stage == "timing-display" and controls_definition else []
                    identity = {"mode": mode, "frame": theme, "stage": stage,
                                "choice": choice["value"] if choice else None, "options": controls}
                    record = {**identity, "available": not blockers,
                              "status": "unavailable" if blockers else "available-with-gaps" if c.gaps else "mapped",
                              "gaps": c.gaps,
                              "destinations": len(fragment.get("destination", []))}
                    if choice:
                        part = author.contribution(module, opts)
                        record["source"] = {"manifest": part.manifest_path, "scene": part.scene_path, "ini": part.ini_path}
                    elif stage == "timing-display" and controls_definition:
                        record["source"] = {"manifest": frame.manifest_path, "scene": frame.scene_path, "ini": frame.ini_path}
                    if not blockers:
                        path = path or shards.add(fragment)
                        record["shard"] = path
                        if selectors: insert_selection(stage_choices, [opts[name] for name in selectors], path)
                        else: selected[stage] = path
                    coverage.append(record)
                    if cache_key is not None and not cached:
                        cache.values[cache_key] = (fragment, c.gaps, path)
                if selectors:
                    selected[stage] = {"selectors": selectors, "paths": stage_choices,
                                       "defaults": {name: options[name] for name in controls_definition}}
            print(json.dumps({"family": author.family, "mode": mode, "frame": theme,
                              "shards": len(shards.records)}, ensure_ascii=True), flush=True)
    catalog = {"frame": [c for c in author.modules["frame"]["choices"] if c["value"] in frames]}
    defaults = {"frame": author.modules["frame"]["default"] if author.modules["frame"]["default"] in frames else frames[0]}
    selection_report, allowed_controls = {}, {}
    for module in MODULES:
        if module not in author.modules:
            continue
        candidates = {c["choice"] for c in coverage if c["stage"] == module}
        complete, allowed_controls[module] = available_selections(author, module, coverage, modes, frames)
        catalog[module] = [c for c in author.modules[module]["choices"] if c["value"] in complete]
        original = author.modules[module]["default"]
        defaults[module] = original if original in complete else next((c["value"] for c in catalog[module]), None)
        selection_report[module] = {"sourceDefault": original, "packageDefault": defaults[module],
                                    "available": [c["value"] for c in catalog[module]],
                                    "unavailable": [c["value"] for c in author.modules[module]["choices"]
                                                    if c["value"] in candidates and c["value"] not in complete]}
    control_report = {}
    source_controls = {item["id"]: item for item in author.manifest.get("options", [])}
    for stage, names in CONTROL_GROUPS.items():
        module = stage_module(stage)
        if module in MODULES and module not in author.modules and stage != "timing-display":
            continue
        for name in names:
            if name not in source_controls:
                continue
            if module in author.modules:
                exposed = bool(catalog.get(module))
                supported_values = allowed_controls.get(module, {}).get(name, []) if exposed else []
            else:
                complete, allowed = available_selections(author, stage, coverage, modes, frames)
                supported_values = allowed.get(name, []) if complete else []
            definition = source_controls[name]
            if name in control_report:
                supported_values = [v for v in control_report[name]["available"] if v in supported_values]
                control_report[name]["stages"].append(stage)
                control_report[name]["available"] = supported_values
            else:
                control_report[name] = {"stages": [stage], "available": supported_values,
                                        "default": definition["default"], "definition": definition}
    for name, report in control_report.items():
        definition, supported_values = report["definition"], report["available"]
        exposed = len(supported_values) > 1
        if exposed:
            catalog[name] = [c for c in control_choices(definition) if c["value"] in supported_values]
            defaults[name] = definition["default"]
        report["status"] = (("exposed" if len(supported_values) == len(control_choices(definition))
                              else "partially-exposed") if exposed else "fixed-not-exposed")
        report["unavailable"] = [c["value"] for c in control_choices(definition) if c["value"] not in supported_values]
    entry_files = {}
    for keymode, filename, relevant in (("7k", "play7.luaskin", [m for m in modes if m != "dp"]),
                                        ("14k", "play14.luaskin", [m for m in modes if m == "dp"])):
        if not relevant:
            continue
        current = {mode: plan[mode] for mode in relevant}
        header = {key: baseline[key] for key in ("name", "author", "w", "h")}
        header["type"] = 0 if keymode == "7k" else 2
        base = {**baseline, "type": header["type"]}
        selection_file = "selection" + keymode[:-1] + ".lua"
        (output / selection_file).write_text("return " + lua(current) + "\n", encoding="utf-8")
        lazy_plan = Lua("require(" + lua(selection_file[:-4]) + ")")
        (output / filename).write_text(entry_script(header, relevant, frames, lazy_plan, catalog, defaults, base,
                                                    stable_option_ids(author)), encoding="utf-8")
        for name in (filename, selection_file):
            entry_files[name] = hashlib.sha256((output / name).read_bytes()).hexdigest()
    receipt = {"contract": "oms-beatoraja-modular-package.v1", "family": author.family, "name": author.name,
               "toolFilesSha256": tool_files,
               "constructionCache": {"stages": ["notes", "charge-notes", "judgement"], "hits": cache.hits, "misses": cache.misses},
               "sourceManifestSha256": hashlib.sha256((author.root / "gameplay-skin.json").read_bytes()).hexdigest(),
               "sourceCatalogSha256": hashlib.sha256(source_path(author.root, author.manifest["contentCatalog"]).read_bytes()).hexdigest(),
               "modes": modes, "frames": frames, "capHeight": cap_height, "modules": selection_report,
               "nativeOptionIds": stable_option_ids(author),
               "entryFilesSha256": entry_files,
               "fixedAuthorControls": {m: {k: v for k, v in _options(author, m, defaults["frame"]).items()
                                            if k not in (*MODULES, "frame") and k not in catalog} for m in modes},
               "localControls": control_report,
               "sourceOptionCoverage": [{"id": item["id"], "type": item["type"], "sourceDefault": item["default"],
                                          "status": "mapped-to-Play-Side" if item["id"] == "layout" else
                                                    "mapped-to-" + CONTROL_LABELS[item["id"]] if item["id"] in catalog else "fixed-not-exposed",
                                          "definition": item} for item in author.manifest.get("options", [])],
               "coverage": coverage, "shards": shards.records, "assets": sink.provenance,
               "boundary": "Native loader structure only; no visual, GPU, input, timing or product acceptance implied."}
    verify_tool_files(tool_files)
    (output / "package-receipt.json").write_text(json.dumps(receipt, indent=2, ensure_ascii=False), encoding="utf-8")
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--author", type=Path, required=True)
    parser.add_argument("--family", choices=("standard", "lightning"), required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--modes", default="p1,p2,dp")
    parser.add_argument("--frames", help="Comma-separated real Frame choices; default: all frozen choices")
    parser.add_argument("--bake-root", type=Path, action="append", default=[])
    parser.add_argument("--bake-override", type=Path, action="append", default=[])
    parser.add_argument("--native-font", type=Path)
    parser.add_argument("--choices", action="append", default=[], metavar="MODULE=VALUE,VALUE",
                        help="Explicit development subset; other modules retain all real choices")
    args = parser.parse_args()
    subsets = dict(value.split("=", 1) for value in args.choices)
    receipt = export_package(AuthorPackage(args.author, args.family), args.output,
                             modes=args.modes.split(","), frames=args.frames.split(",") if args.frames else None,
                             bake_roots=args.bake_root, bake_overrides=args.bake_override,
                             native_font=args.native_font, module_choices={k: v.split(",") for k, v in subsets.items()})
    print(json.dumps({"family": receipt["family"], "shards": len(receipt["shards"]),
                      "coverage": len(receipt["coverage"]), "modules": {k: {"available": len(v["available"]),
                      "unavailable": len(v["unavailable"])} for k, v in receipt["modules"].items()}}, ensure_ascii=True))


if __name__ == "__main__":
    main()
