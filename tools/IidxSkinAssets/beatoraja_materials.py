"""Source-faithful material recipes for the independent beatoraja exports.

No files are written. ``sink`` is main's ``add_source(relative_author_path)``
callback; it returns a native source ID and owns copying/content deduplication.
Image ``source`` and recipe geometry are provenance metadata, not JsonSkin fields.
The compositor owns destinations, raster padding, timer bindings and baking.
Unrepresentable author behavior is returned in ``gaps`` instead of substituted.
"""

from __future__ import annotations

import copy
import hashlib
from pathlib import Path
import struct
from typing import Any, Callable

from beatoraja_author import (AuthorPackage, Contribution, material_bindings,
                             material_templates, source_path, walk_nodes)

SourceSink = Callable[[str], str]
NOTE_FIELDS = {
    "note": ("object.note", None),
    "lnstart": ("object.long-note.head", "cn"),
    "lnend": ("object.long-note.tail", "cn"),
    "lnbody": ("object.long-note.body", "cn"),
    "lnbodyActive": ("object.long-note.body", "cn-holding"),
    "hcnstart": ("object.long-note.head", "hcn"),
    "hcnend": ("object.long-note.tail", "hcn"),
    "hcnbody": ("object.long-note.body", "hcn"),
    "hcnbodyActive": ("object.long-note.body", "hcn-holding"),
    "hcnbodyReactive": ("object.long-note.body", "hcn-holding"),
    "hcnbodyMiss": ("object.long-note.body", "hcn-released"),
}
CAP_FIELDS = ("note", "lnstart", "lnend", "hcnstart", "hcnend")
JUDGE_GRADES = ("perfect", "great", "good", "meh", "miss", "ok")


def _mode(keymode: str, presentation: str) -> int:
    if (keymode, presentation) not in (("7k", "p1"), ("7k", "p2"), ("14k", "dp")):
        raise ValueError("Material export requires SP1, SP2 or DP")
    return 2 if keymode == "14k" else 1


def _context(options: dict[str, Any], presentation: str, **extra) -> dict[str, Any]:
    return {**{"option." + k: v for k, v in options.items()},
            "layout.ruleset": "bms", "layout.presentation": presentation,
            "object.auto-assist": "none", "combo.active": True, **extra}


def _key(value: Any) -> str:
    return str(value).lower() if isinstance(value, bool) else str(value)


def _property(contribution: Contribution, node: dict, prop: str, context: dict,
              default: Any = None) -> Any:
    value = node.get("resource", default) if prop == "resource" else node.get("properties", {}).get(prop, default)
    for variant in contribution.scene.get("variants", []):
        if variant["target"] == node["id"] and variant["property"] == prop:
            source = variant["source"]
            if source not in context:
                raise ValueError(f"Cannot resolve {prop} source {source}")
            chosen = next((c for c in variant["cases"] if c["key"] == _key(context[source])), None)
            value = chosen["resource" if prop == "resource" else "value"] if chosen else variant["default"]
    for binding in contribution.scene.get("bindings", []):
        if binding["target"] == node["id"] and binding["property"] == prop:
            if binding["source"] not in context:
                raise ValueError(f"Cannot resolve bound {prop} source {binding['source']}")
            value = context[binding["source"]]
    return value


def _png_size(path: Path) -> tuple[int, int]:
    with path.open("rb") as stream:
        header = stream.read(24)
    if len(header) != 24 or header[:8] != b"\x89PNG\r\n\x1a\n" or header[12:16] != b"IHDR":
        raise ValueError("Expected an original PNG texture")
    size = struct.unpack(">II", header[16:24])
    if min(size) <= 0:
        raise ValueError("Empty PNG texture")
    return size


class _Images:
    def __init__(self, author: AuthorPackage, sink: SourceSink):
        self.author, self.sink = author, sink
        self.images: dict[tuple, dict] = {}
        self.sources: dict[str, str] = {}

    def add(self, contribution: Contribution, resource: str, crop=None) -> str:
        item = contribution.resources[resource]
        if item["type"] != "texture":
            raise ValueError("A native image requires an actual texture")
        relative = item["path"]
        width, height = _png_size(source_path(self.author.root, relative))
        rect = tuple(crop or (0, 0, width, height))
        if (len(rect) != 4 or any(not isinstance(v, int) for v in rect)
                or min(rect[:2]) < 0 or min(rect[2:]) <= 0
                or rect[0] + rect[2] > width or rect[1] + rect[3] > height):
            raise ValueError("Texture crop is outside its own source PNG")
        key = (relative, rect)
        if key not in self.images:
            if relative not in self.sources:
                self.sources[relative] = self.sink(relative)
            identifier = self.author.family + ".material." + hashlib.sha256(repr(key).encode()).hexdigest()[:20]
            self.images[key] = {"id": identifier, "src": self.sources[relative], "source": relative,
                                "x": rect[0], "y": rect[1], "w": rect[2], "h": rect[3], "divx": 1, "divy": 1}
        return self.images[key]["id"]

    def by_id(self, identifier: str) -> dict:
        return next(i for i in self.images.values() if i["id"] == identifier)


def _lane_bindings(contribution: Contribution, keymode: str, presentation: str) -> dict[int, dict]:
    lanes: dict[int, dict] = {}
    for item in material_bindings(contribution, keymode, presentation):
        target = item["target"]
        if target["kind"] != "lane":
            continue
        logical = int(target["global-logical"])
        lane = lanes.setdefault(logical, {"target": target, "slots": {}})
        if lane["target"] != target:
            raise ValueError("Ambiguous logical lane target")
        previous = lane["slots"].setdefault(item["slot"], item)
        if previous != item:
            raise ValueError("Conflicting material declarations")
    return lanes


def _native_lanes(lanes: dict[int, dict], groups: int) -> list[dict]:
    result = []
    if len(lanes) != groups * 8:
        raise ValueError("Expected all explicit source lanes")
    for group in range(groups):
        members = [l for l in lanes.values() if int(l["target"]["group-logical"]) == group]
        scratch = [l for l in members if l["target"]["lane"].startswith("bms.lane.scratch-")]
        keys = sorted((l for l in members if l not in scratch), key=lambda l: int(l["target"]["group-local-logical"]))
        if len(scratch) != 1 or len(keys) != 7:
            raise ValueError("Expected seven keys and one scratch per deck")
        result.extend([*keys, scratch[0]])
    return result


def _material(contribution: Contribution, item: dict) -> dict:
    if item["mode"] != "Provide":
        raise ValueError("Suppressed material has no drawable source")
    matches = material_templates(contribution, item["slot"], item["resource"])
    if len(matches) != 1:
        raise ValueError("Expected exactly one template for this material")
    return matches[0]


def _sprite(contribution: Contribution, item: dict, context: dict) -> tuple[dict, str]:
    root = _material(contribution, item)
    while root["type"] == "container":
        # Lightning's scratch-assist replacement is an identity container with
        # mutually exclusive real sprites. Resolve its actual visible branch.
        identity = {"width": 1, "height": 1, "x": 0, "y": 0, "opacity": 1}
        if root.get("effects") or any(identity.get(k) != v for k, v in root.get("properties", {}).items()):
            raise ValueError("Transformed note container needs an explicit bake")
        visible = [n for n in root.get("children", []) if _property(contribution, n, "visible", context, True)]
        if len(visible) != 1:
            raise ValueError("Note material does not resolve to one actual sprite")
        root = visible[0]
    if root["type"] != "sprite" or root.get("children") or root.get("effects"):
        raise ValueError("This note material requires an explicit bake, not a guessed texture")
    return root, _property(contribution, root, "resource", context)


def _bake(contribution: Contribution, root: dict, slot: str, lane_indices: list[int]) -> dict:
    return {"module": contribution.module, "manifest": contribution.manifest_path,
            "scene": contribution.scene_path, "root": copy.deepcopy(root), "slot": slot,
            "lane_indices": lane_indices,
            "animations": [copy.deepcopy(a) for a in contribution.manifest.get("animations", [])
                           if a["id"] in {n.get("animation") for n in walk_nodes(root)}]}


def collect_note_materials(author: AuthorPackage, options: dict[str, Any], keymode: str,
                           presentation: str, sink: SourceSink) -> dict[str, Any]:
    """Return modern NoteSet arrays in key1..key7,scratch order on each deck.

    HCN reactive means re-held after losing the head: the author's actual holding
    material applies. The released material maps to native hcnbodyMiss. These are
    the modern fields used by JsonPlaySkinObjectLoader, not its inverted legacy
    lnactive/hcndamage aliases. LN and CN share native slots, so their author
    materials must actually agree. No mine/hidden art is invented.
    """
    groups = _mode(keymode, presentation)
    contributions = {m: author.contribution(m, options) for m in ("notes", "charge-notes")}
    bindings = {m: _lane_bindings(c, keymode, presentation) for m, c in contributions.items()}
    lanes = _native_lanes(bindings["notes"], groups)
    images = _Images(author, sink)
    note = {"id": author.family + ".notes", **{field: [] for field in NOTE_FIELDS}}
    geometry, released, bakes = [], [], {}
    context = _context(options, presentation)
    for index, lane in enumerate(lanes):
        logical = int(lane["target"]["global-logical"])
        slots = {}
        for field, (slot, state) in NOTE_FIELDS.items():
            module = "notes" if field == "note" else "charge-notes"
            c = contributions[module]
            item = bindings[module][logical]["slots"][slot]
            mode = "hcn" if state and state.startswith("hcn") else "cn"
            states = {**context, "object.long-note-mode": mode, "object.long-note-appearance": state}
            root, resource = _sprite(c, item, states)
            if mode == "cn" and field != "note":
                ln = {**states, "object.long-note-mode": "ln", "object.long-note-appearance": state.replace("cn", "ln", 1)}
                if _sprite(c, item, ln)[1] != resource:
                    raise ValueError("The selected LN and CN art cannot share a native NoteSet slot")
            image = images.add(c, resource)
            note[field].append(image)
            slots[field] = {"image": image, "resource": resource, "node": root["id"],
                            "properties": copy.deepcopy(root.get("properties", {})),
                            "source_size": [images.by_id(image)["w"], images.by_id(image)["h"]]}
        c = contributions["charge-notes"]
        item = bindings["charge-notes"][logical]["slots"]["object.long-note.body"]
        _, resource = _sprite(c, item, {**context, "object.long-note-mode": "cn", "object.long-note-appearance": "cn-released"})
        released.append(images.add(c, resource))
        geometry.append({"native_index": index, "source_target": copy.deepcopy(lane["target"]), "slots": slots})
        effect = bindings["charge-notes"][logical]["slots"].get("effect.long-note")
        if effect:
            root = _material(c, effect)
            request = bakes.setdefault(root["id"], _bake(c, root, "effect.long-note", []))
            request["lane_indices"].append(index)
    gaps = [{"code": "mine-art-unavailable", "detail": "Selected author modules declare no native mine material; no default skin art was copied."}]
    if released != note["lnbody"]:
        gaps.append({"code": "ln-cn-released-body-not-separate", "detail": "Native LN/CN has only idle and active body slots; author released images are retained in extra_states."})
    if any(len({g["slots"][f]["source_size"][1] for f in CAP_FIELDS}) > 1 for g in geometry):
        gaps.append({"code": "shared-note-height-needs-padding", "detail": "Native normal/head/tail share lane.scale. Preserve source sizes by transparent padding, not by shrinking the caps."})
    conditional = [v for c in contributions.values() for v in c.scene.get("variants", []) if v.get("source") == "object.auto-assist"]
    if conditional:
        gaps.append({"code": "auto-assist-material-condition", "detail": "Recipes resolve unassisted notes; source auto-assist variants require the host's actual assist state."})
    return {"images": list(images.images.values()), "note": note, "geometry": geometry,
            "extra_states": {"ln_cn_released": released}, "bakes": list(bakes.values()), "gaps": gaps,
            "state_mapping": {"hcnbodyReactive": "author hcn-holding after re-hold", "hcnbodyMiss": "author hcn-released"},
            "carrier": {"alignment": "bottom", "padding": "transparent above source pixels",
                        "evidence": "LaneRenderer draws normal at time y; drawLongNote draws head at y-height and tail at y, both as rectangle lower edges.",
                        "body_padding": False}}


def _visible_rows(contribution: Contribution, root: dict, context: dict, ancestors=()):
    if not _property(contribution, root, "visible", context, True):
        return
    route = (*ancestors, {"id": root["id"], "type": root["type"], "properties": copy.deepcopy(root.get("properties", {}))})
    if root.get("properties", {}).get("layout") == "horizontal":
        yield root, route
    for child in root.get("children", []):
        yield from _visible_rows(contribution, child, context, route)


def _font_value(contribution: Contribution, font: dict, images: _Images) -> tuple[dict | None, dict]:
    glyphs = {g["character"]: g for g in font["glyphs"]}
    if not set("0123456789").issubset(glyphs):
        raise ValueError("Judge bitmap font does not have ten actual digits")
    digits = [glyphs[str(d)] for d in range(10)]
    first = digits[0]
    x, y, width, height = first["rect"]
    advance = first["advance"]
    if any(g["resource"] != first["resource"] or g["rect"] != [x + i * width, y, width, height]
           or g["advance"] != advance for i, g in enumerate(digits)):
        raise ValueError("Judge glyphs are not a uniform source grid; explicit lossless atlas work is needed")
    image_id = images.add(contribution, first["resource"], [x, y, width * 10, height])
    image = images.by_id(image_id)
    identifier = images.author.family + ".combo." + hashlib.sha256(font["id"].encode()).hexdigest()[:16]
    value = {"id": identifier, "src": image["src"], "source": image["source"], "x": x, "y": y,
             "w": width * 10, "h": height, "divx": 10, "divy": 1, "digit": 10,
             "ref": 0}
    spacing = advance - width
    ready = float(spacing) == int(spacing)
    metadata = {"font": font["id"], "value": identifier if ready else None, "line_height": font["lineHeight"],
                "glyph_size": [width, height], "advance": advance, "source_space": spacing,
                "digit_count": 10, "grid_image": image_id, "native_spacing_ready": ready,
                "value_grid": copy.deepcopy(value)}
    if ready:
        value["space"] = int(spacing)
    return value if ready else None, metadata


def collect_judge_materials(author: AuthorPackage, options: dict[str, Any], keymode: str,
                            presentation: str, sink: SourceSink) -> dict[str, Any]:
    """Return actual labels/number rows and native Judge destination prototypes.

    Ancestor geometry and all source tracks are retained for composition. Multiple
    PGREAT palette rows are alternatives, not silently conflated into one image.
    """
    _mode(keymode, presentation)
    c = author.contribution("judgement", options)
    markers = {i["resource"] for i in material_bindings(c, keymode, presentation)
               if i["slot"] == "hud.judgement" and i["mode"] == "Provide"}
    roots = [_material(c, {"slot": "hud.judgement", "resource": marker, "mode": "Provide"}) for marker in sorted(markers)]
    if not roots:
        raise ValueError("No actual judgement material for this presentation")
    images = _Images(author, sink)
    values, fonts = {}, {}
    for font in c.manifest.get("bitmapFonts", []):
        value, metadata = _font_value(c, font, images)
        values[font["id"]], fonts[font["id"]] = value, metadata
    results, gaps, resource_images = [], [], {}
    for native_index, grade in enumerate(JUDGE_GRADES):
        rows = []
        seen = set()
        context = _context(options, presentation, **{"judgement.result": grade})
        for root in roots:
            for row, route in _visible_rows(c, root, context):
                labels = [n for n in row.get("children", []) if n["type"] == "sprite"]
                numbers = [n for n in row.get("children", []) if n["type"] == "text"]
                if len(labels) != 1 or len(numbers) > 1:
                    raise ValueError("Unqualified judgement row needs a bake")
                label = labels[0]
                resource = _property(c, label, "resource", context)
                image = resource_images.setdefault(resource, images.add(c, resource))
                # Label-only PGREAT uses a resource track instead of separate
                # opacity-switched rows. Retain its actual texture dependencies
                # even though only the first palette is currently visible.
                for track in c.scene.get("tracks", []):
                    if track["target"] == label["id"] and track["property"] == "resource":
                        for key in track["keyframes"]:
                            resource_images.setdefault(key["value"], images.add(c, key["value"]))
                font = _property(c, numbers[0], "bitmap-font", context) if numbers else None
                identity = (image, font)
                if identity in seen:
                    continue
                seen.add(identity)
                rows.append({"image": image, "value": values[font]["id"] if font and values[font] else None,
                             "label_node": copy.deepcopy(label), "number_node": copy.deepcopy(numbers[0]) if numbers else None,
                             "ancestors": list(route), "font": fonts[font] if font else None})
        if not rows and options.get("judgement-display") not in (False, "off") and options.get("judgement-position") != "off":
            raise ValueError(f"No qualified judgement row for {grade}")
        results.append({"native_index": native_index, "grade": grade, "alternatives": rows})
        if len(rows) > 1:
            gaps.append({"code": "judgement-palette-timing", "grade": grade,
                         "detail": "Multiple actual palette rows remain; compositor must preserve source tracks before using the first alternative as final art."})
    used_fonts = {row["font"]["font"] for result in results for row in result["alternatives"] if row["font"]}
    for font_id in sorted(used_fonts):
        font = fonts[font_id]
        if not font["native_spacing_ready"]:
            gaps.append({"code": "fractional-font-spacing", "font": font_id, "blocking": True,
                         "detail": "Native Value.space is integer. Resolve the retained exact advance in destination coordinates; no rounded native Value was emitted."})
    judge = {"id": author.family + ".judge", "index": 0, "shift": True,
             "images": [{"id": r["alternatives"][0]["image"] if r["alternatives"] else ""} for r in results],
             "numbers": [{"id": r["alternatives"][0]["value"] or "" if r["alternatives"] else ""} for r in results]}
    return {"family": author.family, "presentation": presentation, "context": _context(options, presentation),
            "images": list(images.images.values()), "values": [v for v in values.values() if v], "judge": judge,
            "fonts": list(fonts.values()), "results": results, "resource_images": resource_images,
            "tracks": copy.deepcopy(c.scene.get("tracks", [])),
            "bindings": copy.deepcopy(c.scene.get("bindings", [])), "variants": copy.deepcopy(c.scene.get("variants", [])),
            "state_machines": copy.deepcopy(c.scene.get("stateMachines", [])), "gaps": gaps, "bakes": []}


def _node_routes(contribution: Contribution):
    def visit(node, route):
        route = [*route, {"id": node["id"], "type": node["type"], "properties": copy.deepcopy(node.get("properties", {}))}]
        yield node["id"], (node, route)
        for child in node.get("children", []):
            yield from visit(child, route)
    roots = [contribution.scene["root"], *contribution.templates.values()]
    return dict(item for root in roots for item in visit(root, []))


def collect_gauge_materials(author: AuthorPackage, options: dict[str, Any], keymode: str,
                            presentation: str, sink: SourceSink) -> dict[str, Any]:
    """Export the frame's actual full-bar textures and destination dependencies.

    These authors use a complete bar under reveal-x. A JsonSkin Gauge requires
    independently sampled nodes; it is not valid to invent a 50-cell source grid.
    The compositor receives the bar and all gauge-linked node routes explicitly.
    """
    _mode(keymode, presentation)
    c = author.contribution("frame", options)
    nodes = _node_routes(c)
    images = _Images(author, sink)
    gauge_bindings = [b for b in c.scene.get("bindings", []) if b.get("source", "").startswith("gauge.")]
    gauge_variants = [v for v in c.scene.get("variants", []) if v.get("source", "").startswith("gauge.")]
    bars = []
    for binding in gauge_bindings:
        if binding["source"] != "gauge.value" or binding["property"] != "reveal-x":
            continue
        root, route = nodes[binding["target"]]
        sprites = [n for n in walk_nodes(root) if n["type"] == "sprite"]
        if not sprites:
            raise ValueError("Gauge reveal has no static source bar")
        bar = sprites[0]
        states = {}
        for kind in ("normal", "easy", "assist-easy", "hard", "ex-hard", "hazard"):
            states[kind] = images.add(c, _property(c, bar, "resource", _context(options, presentation, **{"gauge.type": kind})))
        hosts = []
        for item in material_bindings(c, keymode, presentation):
            if item["mode"] != "Provide":
                continue
            if any(t["id"] == route[0]["id"] for t in material_templates(c, item["slot"], item["resource"])):
                hosts.append({"slot": item["slot"], "target": copy.deepcopy(item["target"])})
        if not hosts:
            raise ValueError("Gauge source root has no qualified material host")
        bars.append({"reveal_node": root["id"], "sprite_node": copy.deepcopy(bar), "states": states, "ancestors": route, "hosts": hosts})
    if not bars:
        raise ValueError("No qualified gauge-value reveal in the selected frame")
    targets = {item["target"] for item in [*gauge_bindings, *gauge_variants]}
    resource_ids = {v["default"] for v in gauge_variants if v["property"] == "resource"}
    resource_ids.update(case["resource"] for v in gauge_variants if v["property"] == "resource" for case in v["cases"])
    font_ids, bakes = set(), []
    for target in targets:
        subtree = nodes[target][0]
        for node in walk_nodes(subtree):
            if node.get("resource"):
                resource_ids.add(node["resource"])
            if node.get("properties", {}).get("bitmap-font"):
                font_ids.add(node["properties"]["bitmap-font"])
        if any(n["type"] == "recorded-animation" for n in walk_nodes(subtree)):
            bakes.append(_bake(c, subtree, "hud.gauge-feedback", []))
    fonts = [copy.deepcopy(f) for f in c.manifest.get("bitmapFonts", []) if f["id"] in font_ids]
    resource_ids.update(g["resource"] for f in fonts for g in f["glyphs"])
    resource_images = {r: images.add(c, r) for r in sorted(resource_ids)}
    return {"family": author.family, "presentation": presentation, "context": _context(options, presentation),
            "images": list(images.images.values()), "gauge": None, "bars": bars,
            "resource_images": resource_images, "bitmap_fonts": fonts,
            "bindings": copy.deepcopy(gauge_bindings), "variants": copy.deepcopy(gauge_variants),
            "destinations": [{"node": copy.deepcopy(nodes[target][0]), "ancestors": nodes[target][1]} for target in sorted(targets)],
            "bakes": bakes, "gaps": [{"code": "gauge-is-whole-bar", "detail": "Source declares full-bar reveal, not a native Gauge node grid; compose from these same-family bars or qualify a lossless subdivision."}]}


def collect_explosion_materials(author: AuthorPackage, options: dict[str, Any], keymode: str,
                                presentation: str) -> dict[str, Any]:
    """Hand actual explosion template closures to IidxSkinBake, without raster guesses."""
    groups = _mode(keymode, presentation)
    c = author.contribution("explosion", options)
    lanes = _native_lanes(_lane_bindings(c, keymode, presentation), groups)
    requests = {}
    for index, lane in enumerate(lanes):
        item = lane["slots"]["effect.hit-explosion"]
        root = _material(c, item)
        request = requests.setdefault(root["id"], _bake(c, root, item["slot"], []))
        request["lane_indices"].append(index)
    return {"images": [], "bakes": list(requests.values()), "gaps": []}
