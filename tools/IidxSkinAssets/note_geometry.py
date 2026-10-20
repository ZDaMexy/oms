"""Correct the known Omni note/cap author routes for Mania's native carrier.

BMS retains its original 22.5-unit template. The selected Mania public material
uses the unchanged normal-00 marker with a 48-unit height reference width. Only
the Mania C4 resource name selects the new template; no runtime schema changes,
image resampling, body resizing, or ruleset-specific engine logic are involved.
"""

from __future__ import annotations

import copy
import hashlib
import json
import math
from pathlib import Path
import re
import struct
import zlib

SCENE_CONTRACT = "oms-gameplay-skin-scene.v1"
SCALE = 768 / 1080
BMS_CARRIER_HEIGHT = 22.5
MANIA_REFERENCE_WIDTH = 48
COLOURS = ("white", "black", "scratch")
WIDTHS = {"white": 52, "black": 40, "scratch": 90}
SIZE_HEIGHTS = {"normal": 24, "long": 27, "short": 21, "veryshort": 18}
_ROOT = re.compile(r"(?:(n)\.n(\d{2})\.(normal|long|short|veryshort)|(c)\.c(\d{2})\.(normal|long|short|veryshort)\.(source|cn-palette))\.root\Z")
_ID = re.compile(r"[a-z](?:[a-z0-9-]*[a-z0-9])?(?:\.[a-z](?:[a-z0-9-]*[a-z0-9])?)*\Z")
_MODE_KEYS = ("ln", "cn", "hcn")
_APPEARANCE_KEYS = ("ln", "ln-holding", "ln-released", "cn", "cn-holding", "cn-released", "hcn", "hcn-holding", "hcn-released")


def _markers():
    result = {}
    for colour in COLOURS:
        result[f"custom/markers/n/{colour}"] = ("object.note", (WIDTHS[colour], 15))
        for part in ("head", "tail"):
            result[f"custom/markers/c/{colour}-{part}"] = (f"object.long-note.{part}", (WIDTHS[colour], 24))
    return result


MARKERS = _markers()
ALIASES = {source: source.rsplit("/", 1)[0] + "/mania-" + source.rsplit("/", 1)[1] for source in MARKERS}
_ORIGINALS = {alias: source for source, alias in ALIASES.items()}


def _require(condition, message):
    if not condition:
        raise ValueError(message)


def _stable(value):
    _require(isinstance(value, str) and len(value) <= 128 and _ID.fullmatch(value) is not None,
             "An original or generated note identity is not a public stable ID")
    return value


def _equal_number(value, expected):
    return isinstance(value, (float, int)) and not isinstance(value, bool) and math.isfinite(value) and math.isclose(value, expected, rel_tol=0, abs_tol=1e-12)


def _family(document):
    root = document.get("root") if isinstance(document, dict) else None
    identifier = root.get("id") if isinstance(root, dict) else None
    match = _ROOT.fullmatch(identifier) if isinstance(identifier, str) else None
    if match is None:
        _require(not isinstance(identifier, str) or not identifier.startswith(("n.n", "c.c")),
                 "A known note scene has an unexpected root identity")
        return None
    category = "n" if match.group(1) else "c"
    style, size = (int(match.group(2)), match.group(3)) if category == "n" else (int(match.group(5)), match.group(6))
    _require(style <= (11 if category == "n" else 10), "A note scene has an unsupported source style")
    return category, style, size, identifier.removesuffix(".root")


def _source_height(category, style, size, colour):
    if category == "c":
        return SIZE_HEIGHTS[size]
    if style == 11:
        return {"normal": 12, "long": 15, "short": 9, "veryshort": 6}[size]
    # The only taller regular-note canvas in the captured 00..10 library.
    return 16 if style == 5 and size == "long" and colour == "white" else 15


def _validate_variant(variant, root, source, keys):
    identifier = root["id"]
    _require(isinstance(variant, dict) and set(variant) == {"id", "target", "property", "source", "default", "cases"}
             and variant["id"] == identifier + ".v." + source and variant["target"] == identifier
             and variant["property"] == "resource" and variant["source"] == source
             and variant["default"] == root["resource"] and isinstance(variant["cases"], list)
             and len(variant["cases"]) == len(keys), "An original long-note resource variant was altered")
    for case, key in zip(variant["cases"], keys):
        _require(isinstance(case, dict) and set(case) == {"id", "key", "resource"}
                 and case["id"] == identifier + ".c." + source + "." + key and case["key"] == key,
                 "An original long-note resource case was altered")
        _stable(case["resource"])
        if key in ("ln", "cn"):
            _require(case["resource"] == root["resource"], "The original LN/CN base resource changed")


def correct_scene(document, marker_dimensions):
    """Validate then append Mania templates in place; return a correction receipt.

    marker_dimensions maps the original extensionless C4 marker name to (w, h).
    An unrelated scene is left untouched. A partially or differently corrected
    known scene is rejected before any mutation.
    """
    family = _family(document)
    if family is None:
        return {"recognized": False, "changed": False, "clones": []}
    category, style, size, prefix = family
    _require(set(document) == {"contract", "root", "tracks", "stateMachines", "bindings", "variants", "templates", "instances"}
             and document["contract"] == SCENE_CONTRACT, "A known note scene has an unexpected document structure")
    expected_root = {"id": prefix + ".root", "type": "container", "target": {"kind": "global"}, "blend": "inherit",
                     "properties": {}, "effects": [], "children": []}
    _require(document["root"] == expected_root and document["tracks"] == [] and document["stateMachines"] == []
             and document["bindings"] == [], "A known note scene no longer has its original neutral dispatcher")
    _require(all(isinstance(document[key], list) for key in ("templates", "instances", "variants")), "Invalid note scene arrays")
    specifications = [(colour, part) for colour in COLOURS for part in ((None,) if category == "n" else ("head", "body", "tail"))]
    count = len(specifications)
    _require(len(document["templates"]) >= count and len(document["instances"]) >= count,
             "A known note scene is missing original templates or instances")
    original_variants = document["variants"][:count] if category == "c" else []
    _require(category == "n" or len(original_variants) == count, "A long-note scene is missing its resource variants")
    templates, instances, variants, rows = [], [], [], []
    for index, (colour, part) in enumerate(specifications):
        name = colour + ("." + part if part else "")
        slot = "object.note" if part is None else "object.long-note." + part
        marker = f"custom/markers/{category}/{colour}" + ("-" + part if part else "")
        node_id, template_id = prefix + "." + name, prefix + ".t." + name
        template, instance = document["templates"][index], document["instances"][index]
        _require(isinstance(template, dict) and set(template) == {"id", "root"} and template["id"] == template_id,
                 "Original note templates changed identity or order")
        root = template["root"]
        _require(isinstance(root, dict) and set(root) == {"id", "type", "target", "blend", "properties", "effects", "children", "resource", "slot"}
                 and root["id"] == node_id and root["type"] == "sprite" and root["target"] == {"kind": "global"}
                 and root["blend"] == "alpha" and root["slot"] == slot and root["children"] == [] and root["effects"] == [],
                 "An original note template is not its known native Sprite")
        _stable(root["resource"])
        _require(instance == {"id": template_id + ".i", "template": template_id, "material": {"slot": slot, "resource": marker}},
                 "An original note material selector changed")
        if category == "c":
            _validate_variant(original_variants[index], root, "object.long-note-appearance" if part == "body" else "object.long-note-mode",
                              _APPEARANCE_KEYS if part == "body" else _MODE_KEYS)
        if part == "body":
            _require(root["properties"] == {}, "The original native long-note body geometry changed")
            continue
        props = root["properties"]
        expected_height = _source_height(category, style, size, colour) * SCALE / BMS_CARRIER_HEIGHT
        _require(isinstance(props, dict) and set(props) == {"height", "y", "anchor", "origin"}
                 and props["anchor"] == "bottom-left" and props["origin"] == "bottom-left"
                 and _equal_number(props["height"], expected_height) and _equal_number(props["y"], -5 * SCALE / BMS_CARRIER_HEIGHT),
                 "A known BMS note normalization was altered")
        dimensions = marker_dimensions.get(marker)
        _require(isinstance(dimensions, (tuple, list)) and tuple(dimensions) == MARKERS[marker][1],
                 "The original normal-00 marker dimensions changed or are missing")
        width, height = dimensions
        carrier = height * MANIA_REFERENCE_WIDTH / width
        factor = BMS_CARRIER_HEIGHT / carrier
        clone = copy.deepcopy(template)
        clone["id"] = _stable(template_id + ".mania")
        clone["root"]["id"] = _stable(node_id + ".mania")
        clone["root"]["properties"]["height"] *= factor
        clone["root"]["properties"]["y"] *= factor
        clone_instance = copy.deepcopy(instance)
        clone_instance["id"] = _stable(instance["id"] + ".mania")
        clone_instance["template"] = clone["id"]
        clone_instance["material"]["resource"] = ALIASES[marker]
        templates.append(clone)
        instances.append(clone_instance)
        if category == "c":
            variant = copy.deepcopy(original_variants[index])
            variant["id"] = _stable(variant["id"] + ".mania")
            variant["target"] = clone["root"]["id"]
            for case in variant["cases"]:
                case["id"] = _stable(case["id"] + ".mania")
            variants.append(variant)
        rows.append({"template": template_id, "maniaTemplate": clone["id"], "slot": slot, "marker": marker,
                     "maniaMarker": ALIASES[marker], "markerPixels": list(dimensions), "carrierHeight": carrier,
                     "heightReferenceWidth": MANIA_REFERENCE_WIDTH, "factor": factor,
                     "sourceProperties": copy.deepcopy(props), "maniaProperties": copy.deepcopy(clone["root"]["properties"])})
    tails = (document["templates"][count:], document["instances"][count:], document["variants"][len(original_variants):])
    expected_tails = (templates, instances, variants)
    fresh = all(tail == [] for tail in tails)
    _require(fresh or all(actual == expected for actual, expected in zip(tails, expected_tails)),
             "The known note scene has conflicting or incomplete Mania clones")
    if fresh:
        document["templates"].extend(templates)
        document["instances"].extend(instances)
        document["variants"].extend(variants)
    return {"recognized": True, "changed": fresh, "root": prefix + ".root", "category": category,
            "addedTemplates": len(templates) if fresh else 0, "clones": rows,
            "bmsTemplatesAndBodyPreserved": True}


def correct_ini(data):
    """Return (bytes, receipt), changing only exact Mania Lane note/cap Provides."""
    _require(isinstance(data, bytes), "Note INI input must be bytes")
    section, mania_lane, changed, already = "", False, [], 0
    output = []
    slot_start = re.compile(r"\s*(object\.note|object\.long-note\.(?:head|tail))\s*:")
    provide = re.compile(r'(?P<before>\s*(?P<slot>object\.note|object\.long-note\.(?:head|tail))\s*:\s*resource\s+Provide\s+")(?P<path>[^"]+)(?P<after>"[ \t]*(?://[^\r\n]*)?)\Z')
    for line_number, line in enumerate(data.decode("utf-8").splitlines(keepends=True), 1):
        body = line.rstrip("\r\n")
        ending = line[len(body):]
        stripped = body.strip().lstrip("\ufeff")
        if stripped.startswith("["):
            section, mania_lane = stripped, False
        elif stripped.startswith("Target:"):
            tokens = stripped.split()
            mania_lane = section == "[GameplaySkin.Common:1]" and len(tokens) > 1 and tokens[1] == "Lane" and "ruleset=mania" in tokens[2:]
            if mania_lane:
                _require(sum(token.startswith("ruleset=") for token in tokens[2:]) == 1, "A Mania Lane has conflicting ruleset selectors")
        elif mania_lane and slot_start.match(body):
            match = provide.fullmatch(body)
            _require(match is not None, "A known Mania note/cap must retain a resource Provide")
            slot, path = match["slot"], match["path"]
            source = _ORIGINALS.get(path, path)
            _require(source in MARKERS and MARKERS[source][0] == slot, "A Mania note/cap has an unexpected original marker")
            if path == source:
                alias = ALIASES[source]
                body = match["before"] + alias + match["after"]
                changed.append({"line": line_number, "slot": slot, "from": source, "to": alias})
            else:
                already += 1
        output.append(body + ending)
    corrected = "".join(output).encode("utf-8")
    return corrected, {"changed": corrected != data, "rewrittenEntries": changed, "alreadyCorrectEntries": already}


def _contained(root, relative):
    _require(isinstance(relative, str) and relative and "\\" not in relative and ":" not in relative,
             "An author contribution must use a contained relative path")
    path = Path(relative)
    _require(not path.is_absolute() and all(part not in ("", ".", "..") for part in relative.split("/")), "An author contribution escapes its directory")
    result = (root / path).resolve()
    _require(result.is_relative_to(root), "An author contribution escapes its directory")
    return result


def _png_dimensions(data):
    _require(len(data) >= 33 and data[:16] == b"\x89PNG\r\n\x1a\n\x00\x00\x00\x0dIHDR"
             and zlib.crc32(data[12:29]) == struct.unpack(">I", data[29:33])[0], "An original marker has an invalid PNG IHDR")
    width, height = struct.unpack(">II", data[16:24])
    _require(width > 0 and height > 0, "An original marker has invalid PNG dimensions")
    return width, height


def marker_dimensions(root, sources=None):
    """Read and validate the original nine marker PNG IHDRs without writing.

    The returned dict uses extensionless source names, exactly like ALIASES.
    Pass a subset of source names for a note-only or cap-only author fixture;
    the default requires all nine original markers.
    Alias PNG paths are ``ALIASES[source] + '.png'``; original PNG paths are
    ``source + '.png'``. This is also the pure repackage verifier's input to
    correct_scene. Pixel payloads are never decoded or transformed here.
    """
    root = Path(root)
    _require(root.drive.lower() not in ("c:", "d:"), "Marker inspection requires an independent non-system author directory")
    root = root.resolve(strict=True)
    _require(root.is_dir() and root.drive.lower() not in ("c:", "d:"), "Marker inspection requires an independent non-system author directory")
    dimensions = {}
    selected = tuple(MARKERS) if sources is None else tuple(sources)
    _require(len(set(selected)) == len(selected) and all(source in MARKERS for source in selected),
             "Marker inspection requires distinct known original marker names")
    for source in selected:
        expected = MARKERS[source][1]
        dimensions[source] = _png_dimensions(_contained(root, source + ".png").read_bytes())
        _require(dimensions[source] == expected, "The original normal-00 marker dimensions changed")
    return dimensions


def apply_to_author(output):
    """Plan all known n/c corrections, then copy exact aliases and write changed files.

    Existing aliases must be byte-identical to their originals. No manifest,
    catalog, option, original PNG, BMS entry, or long-note body is changed.
    """
    output = Path(output)
    _require(output.drive.lower() not in ("c:", "d:"), "Note normalization requires an independent non-system author directory")
    output = output.resolve(strict=True)
    _require(output.is_dir() and output.drive.lower() not in ("c:", "d:"), "Note normalization requires an independent non-system author directory")
    root_manifest = json.loads((output / "gameplay-skin.json").read_bytes())
    catalog = json.loads(_contained(output, root_manifest.get("contentCatalog")).read_bytes())
    _require(catalog.get("contract") == "oms-gameplay-skin-content.v1", "An author has no supported content catalog")
    modules = [module for module in catalog.get("modules", []) if module.get("id") in ("notes", "charge-notes")]
    _require(len({module["id"] for module in modules}) == len(modules), "The author has duplicate note modules")
    scene_paths, ini_paths, categories = {}, set(), set()
    for module in modules:
        category = "n" if module["id"] == "notes" else "c"
        categories.add(category)
        _require(isinstance(module.get("choices"), list) and module["choices"], "A note module has no choices")
        for choice in module["choices"]:
            for part in (choice, *choice.get("variants", [])):
                relative = part.get("scene")
                _contained(output, relative)
                _contained(output, part.get("ini"))
                _require(relative not in scene_paths or scene_paths[relative] == category, "A scene is shared by conflicting note categories")
                scene_paths[relative] = category
                ini_paths.add(part.get("ini"))
    originals, aliases, dimensions = {}, [], {}
    for source, (_, expected) in MARKERS.items():
        if source.split("/")[2] not in categories:
            continue
        path = _contained(output, source + ".png")
        data = path.read_bytes()
        originals[path] = data
        dimensions[source] = _png_dimensions(data)
        _require(dimensions[source] == expected, "The original normal-00 marker dimensions changed")
        alias = _contained(output, ALIASES[source] + ".png")
        _require(not alias.exists() or alias.read_bytes() == data, "A Mania marker alias conflicts with the exact original PNG")
        aliases.append((alias, data, {"source": source + ".png", "path": ALIASES[source] + ".png",
                                      "sha256": hashlib.sha256(data).hexdigest(), "width": expected[0], "height": expected[1],
                                      "created": not alias.exists(), "byteIdenticalToOriginal": True}))
    planned, scenes, inis = {}, [], []
    for relative, category in sorted(scene_paths.items()):
        path = _contained(output, relative)
        _require(path.suffix == ".json", "A note scene must be a JSON contribution")
        originals[path] = path.read_bytes()
        document = json.loads(originals[path])
        family = _family(document)
        _require(family is not None and family[0] == category, "A note module references an unrelated scene")
        receipt = correct_scene(document, dimensions)
        scenes.append({"path": relative, **receipt})
        if receipt["changed"]:
            planned[path] = json.dumps(document, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    for relative in sorted(ini_paths):
        path = _contained(output, relative)
        _require(path.suffix == ".ini", "A note material contribution must be an INI")
        originals[path] = path.read_bytes()
        data, receipt = correct_ini(originals[path])
        _require(receipt["rewrittenEntries"] or receipt["alreadyCorrectEntries"], "A note INI has no known Mania Lane contributions")
        inis.append({"path": relative, **receipt})
        if receipt["changed"]:
            planned[path] = data
    # All structural and alias conflicts are checked before the first author write.
    _require(all(path.read_bytes() == data for path, data in originals.items()), "Original author input changed during note normalization")
    _require(all(not path.exists() or path.read_bytes() == data for path, data, _ in aliases), "A Mania alias changed during note normalization")
    for path, data, row in aliases:
        if row["created"]:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    for path, data in planned.items():
        path.write_bytes(data)
    return {"contract": "oms-omni-note-geometry-correction.v1", "heightReferenceWidth": MANIA_REFERENCE_WIDTH,
            "bmsCarrierHeight": BMS_CARRIER_HEIGHT, "pixelToReferenceUnit": SCALE,
            "changedFiles": sorted(path.relative_to(output).as_posix() for path in planned),
            "changedScenes": sum(scene["changed"] for scene in scenes),
            "aliases": [row for _, _, row in aliases], "scenes": scenes, "inis": inis,
            "bodyAndBmsTemplatesPreserved": True,
            "carrierPolicy": "Selected Mania public normal-00 marker: markerHeight * 48 / markerWidth; BMS retains 22.5"}
