"""Original COMBO palettes and BMS zero-combo presentation for Omni.

The three PGREAT colour pairs are observed original artwork. Their blue/green/
pink 100 ms phases are an explicit OMS author rhythm, not a claim about the
original game's period or initial phase. All clocks use the existing per-owner
``judgement.hit`` track origin.
"""
from __future__ import annotations

import copy
import hashlib
import io
import json
from pathlib import Path

from PIL import Image

from lighting_gameplay_controls import inside, require

CONTRACT = "oms-iidx-combo-states.v1"
PHASES = (("b", "blue"), ("g", "white"), ("r", "pink"))


def walk(value):
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from walk(child)
    elif isinstance(value, list):
        for child in value:
            yield from walk(child)


def encoded_size(value):
    return len(json.dumps(value, ensure_ascii=False, separators=(",", ":"), allow_nan=False).encode("utf-8"))


def _digest(data):
    return hashlib.sha256(data).hexdigest()


def palette_art(library, rows, style):
    """Pack only new green/red glyphs, retaining every source RGBA byte."""
    package = f"contents/data/graphic/judge{style:02}.ifs"
    selected = {row["name"].removesuffix(".png"): row for row in rows if row["package"] == package}
    prefix = "c" + "0123456789abcdefghijklmnopqrstuvwxyz"[style]
    proof, glyphs, images = [], {}, {}

    def original(name):
        row = selected[name]
        path = inside(library, row["sprite"])
        data = path.read_bytes()
        with Image.open(io.BytesIO(data)) as source:
            image = source.convert("RGBA")
        require(_digest(data) == row["sha256"] and image.size == (row["width"], row["height"])
                and _digest(image.tobytes()) == row["rgbaSha256"], "A frozen original COMBO image changed: " + name)
        require(row["sampler"]["mag"] == row["sampler"]["min"] == "nearest"
                and row["sampler"]["wrapS"] == row["sampler"]["wrapT"] == "clamp", "Preserve original COMBO sampling")
        proof.append({"source": str(path), "pngSha256": row["sha256"], "rgbaSha256": row["rgbaSha256"],
                      "width": image.width, "height": image.height})
        return image, data

    original_glyphs = {(palette, digit): original(f"judge_combo_{digit}{palette}")[0]
                       for palette in ("g", "r") for digit in range(10)}
    width = max(image.width for image in original_glyphs.values())
    height = max(image.height for image in original_glyphs.values())
    atlas = Image.new("RGBA", (10 * width, 2 * height))
    for (palette, digit), image in original_glyphs.items():
        left, top = digit * width, (0 if palette == "g" else height)
        atlas.paste(image, (left, top))
        rect = (left, top, left + image.width, top + image.height)
        require(atlas.crop(rect).tobytes() == image.tobytes(), "A new atlas changed original complete RGBA glyph pixels")
        glyphs[(palette, str(digit))] = [left, top, image.width, image.height]
    output = io.BytesIO()
    atlas.save(output, format="PNG")
    images[f"c/{style:x}a.png"] = output.getvalue()
    resources = [{"id": prefix, "type": "texture", "path": f"c/{style:x}a.png", "filter": "nearest"}]
    labels = {}
    blue, _ = original("judge_p_great_blue")
    for palette, name in PHASES[1:]:
        image, data = original("judge_p_great_" + name)
        require(image.size == blue.size, "PGREAT phase labels must preserve the existing label canvas size")
        relative = f"c/{style:x}{palette}.png"
        images[relative] = data
        labels[palette] = prefix + palette
        resources.append({"id": labels[palette], "type": "texture", "path": relative, "filter": "nearest"})
    return {"style": style, "resource": prefix, "resources": resources, "glyphs": glyphs, "labels": labels,
            "images": images, "proof": {"package": package, "readInputs": proof, "atlasRgbaSha256": _digest(atlas.tobytes()),
                "atlasSize": list(atlas.size), "completeRgbaPreserved": True, "resampled": False, "tinted": False}}


def _program_targets(scene, ids):
    """Keep complete program items owned by the retained template trees."""
    result = {}
    for key in ("bindings", "variants", "tracks", "stateMachines"):
        result[key] = [copy.deepcopy(item) for item in scene[key]
                       if (item.get("target") in ids if key != "stateMachines"
                           else all(entry["target"] in ids for state in item["states"] for entry in state["set"]))]
    return result


def _leaf_rows(template, scene):
    nodes = {node["id"]: node for node in walk(template["root"]) if "type" in node}
    variants = [v for v in scene["variants"] if v["source"] == "judgement.result" and v["property"] == "visible"
                and v["target"] in nodes]
    require(len(variants) == 2, "Each existing Judgement template must own one PGREAT and one other-grade row")
    rows = {}
    for variant in variants:
        row = nodes[variant["target"]]
        require(row["properties"].get("layout") == "horizontal" and len(row["children"]) == 2
                and [child["type"] for child in row["children"]] == ["sprite", "text"], "Use the measured label/digit row")
        key = "perfect" if [c["key"] for c in variant["cases"]] == ["perfect"] else "other"
        require(key not in rows, "Duplicate Judgement grade row")
        rows[key] = row
    require(set(rows) == {"perfect", "other"}, "Both authoritative grade rows are required")
    return rows


def _template_signature(template, scene):
    root = copy.deepcopy(template["root"])
    nodes = [node for node in walk(root) if "type" in node]
    program = _program_targets(scene, {node["id"] for node in nodes})
    for node in nodes:
        if node["properties"].get("layout") == "horizontal":
            node["properties"].pop("x")
            node["properties"].pop("y")
    value = {"root": root, "program": program}
    ids = {item["id"]: str(index) for index, item in enumerate(item for item in walk(value) if "id" in item)}
    def normalise(item):
        if isinstance(item, str):
            return ids.get(item, item)
        if isinstance(item, dict):
            return {key: normalise(child) for key, child in item.items()}
        if isinstance(item, list):
            return [normalise(child) for child in item]
        return item
    return normalise(value)


def transform_contribution(manifest, scene, art):
    """Share one immutable program; each material still creates its exact owner."""
    require(len(scene["templates"]) == len(scene["instances"]) == 3, "Use the frozen three-layout Judgement contribution")
    original_bytes = encoded_size(scene)
    original = copy.deepcopy(scene)
    rows = [_leaf_rows(template, original) for template in original["templates"]]
    first, second, dual = original["templates"]
    require(all(rows[0][key]["properties"]["y"] == rows[1][key]["properties"]["y"] for key in rows[0]),
            "SP sharing cannot alter either side's vertical anchor")
    deltas = [rows[1][key]["properties"]["x"] - rows[0][key]["properties"]["x"] for key in rows[0]]
    require(deltas[0] == deltas[1], "SP rows must have the same measured side translation")
    dp_x = [rows[2][key]["properties"]["x"] - rows[0][key]["properties"]["x"] for key in rows[0]]
    dp_y = [rows[2][key]["properties"]["y"] - rows[0][key]["properties"]["y"] for key in rows[0]]
    require(dp_x[0] == dp_x[1] and dp_y[0] == dp_y[1], "DP rows must share their original exact translation")
    signature = _template_signature(first, original)
    require(all(_template_signature(template, original) == signature for template in (second, dual)),
            "Sharing must not change a layout's artwork, typography, JP/display/brightness program or local geometry")
    kept = [first]
    retained_ids = {node["id"] for template in kept for node in walk(template["root"]) if "type" in node}
    result = {**copy.deepcopy(original), **_program_targets(original, retained_ids), "templates": kept}
    for instance in result["instances"]:
        instance["template"] = first["id"]
    updated_manifest = copy.deepcopy(manifest)
    known = {entry["id"] for entry in updated_manifest["resources"]}
    require(not known.intersection(entry["id"] for entry in art["resources"]), "New COMBO resources collide with the frozen contribution")
    updated_manifest["resources"].extend(copy.deepcopy(art["resources"]))
    bfont = rows[0]["perfect"]["children"][1]["properties"]["bitmap-font"]
    font, = [font for font in manifest["bitmapFonts"] if font["id"] == bfont]
    fonts = {}
    for palette in ("g", "r"):
        extra = copy.deepcopy(font)
        extra["id"] = art["resource"] + palette + "f"
        for glyph in extra["glyphs"]:
            glyph["resource"] = art["resource"]
            glyph["rect"] = art["glyphs"][(palette, glyph["character"])]
        updated_manifest["bitmapFonts"].append(extra)
        fonts[palette] = extra["id"]

    allocated = {item["id"] for item in walk(result) if "id" in item}
    next_id = 0

    def identifier():
        nonlocal next_id
        while True:
            number, suffix = next_id, ""
            while True:
                suffix = "0123456789abcdefghijklmnopqrstuvwxyz"[number % 36] + suffix
                number //= 36
                if number == 0:
                    break
            value = "q" + suffix
            next_id += 1
            if value not in allocated:
                allocated.add(value)
                return value

    def container(children):
        return {"id": identifier(), "type": "container", "target": {"kind": "global"}, "blend": "inherit",
                "properties": {}, "effects": [], "children": children}

    def visibility(target, source, keys, default=False):
        result["variants"].append({"id": identifier(), "target": target, "property": "visible", "source": source,
            "default": default, "cases": [{"id": identifier(), "key": key, "value": not default} for key in keys]})

    def copy_row(row):
        clone = copy.deepcopy(row)
        mapping = {}
        for node in walk(clone):
            if "type" in node:
                old = node["id"]
                node["id"] = identifier()
                mapping[old] = node["id"]
        # The frozen row has only exact-target text bindings and finite variants.
        for key in ("bindings", "variants"):
            for program in list(result[key]):
                if program["target"] not in mapping:
                    continue
                extra = copy.deepcopy(program)
                for item in walk(extra):
                    if "id" in item:
                        item["id"] = identifier()
                extra["target"] = mapping[program["target"]]
                result[key].append(extra)
        return clone

    def phase_track(target, phase, *, resources=None):
        values = resources if resources is not None else [int(index == phase) for index in range(3)]
        values = [*values, values[0]]
        result["tracks"].append({"id": identifier(), "type": "frame" if resources is not None else "tween", "target": target,
            "property": "resource" if resources is not None else "opacity", "easing": "step", "loop": True, "trigger": "judgement.hit",
            "keyframes": [{"id": identifier(), "time": index * 100, "value": value} for index, value in enumerate(values)]})

    def label_row(row):
        result_row = copy_row(row)
        removed_digit = result_row["children"].pop()
        result["bindings"] = [binding for binding in result["bindings"] if binding["target"] != removed_digit["id"]]
        result["variants"] = [variant for variant in result["variants"] if variant["target"] != removed_digit["id"]]
        return result_row

    def label_cycle(row, blue_resource):
        phase_track(row["children"][0]["id"], 0,
                    resources=[blue_resource, art["labels"]["g"], art["labels"]["r"]])

    for template in kept:
        grade_rows = _leaf_rows(template, original)
        gate = template["root"]["children"][0]
        require(gate["children"] == [grade_rows["perfect"], grade_rows["other"]], "Preserve the existing JP display gate")
        positive = container(gate["children"])
        result["bindings"].append({"id": identifier(), "target": positive["id"], "property": "visible", "source": "combo.active"})
        zero_bms_rows, zero_mania_rows = [], []
        for key, row in grade_rows.items():
            mania = copy_row(row)
            zero_mania_rows.append(mania)
            bms = label_row(row)
            zero_bms_rows.append(bms)
            if key == "perfect":
                label_cycle(bms, row["children"][0]["resource"])
        bms_gate, mania_gate = container(zero_bms_rows), container(zero_mania_rows)
        visibility(bms_gate["id"], "layout.ruleset", ["bms"])
        visibility(mania_gate["id"], "layout.ruleset", ["mania"])
        zero = container([bms_gate, mania_gate])
        visibility(zero["id"], "combo.active", ["false"])
        layout_gate = container([positive, zero])
        gate["children"] = [layout_gate]
        blue = grade_rows["perfect"]
        for phase, palette in enumerate(("b", "g", "r")):
            row = blue if palette == "b" else copy_row(blue)
            if palette != "b":
                row["children"][0]["resource"] = art["labels"][palette]
                row["children"][1]["properties"]["bitmap-font"] = fonts[palette]
            carrier = container([row])
            if palette == "b":
                positive["children"][positive["children"].index(blue)] = carrier
            else:
                positive["children"].append(carrier)
            # visible and opacity both write framework Alpha. Keep the grade
            # variant on the original row and the phase on a distinct carrier.
            phase_track(carrier["id"], phase)
        # The choice owns complete rows. Normal alone contains numeric cells;
        # label-only has one real sprite cell for either ruleset and every combo.
        # This also avoids preserving mania's previous invisible-cell offset.
        normal = container(layout_gate["children"])
        visibility(normal["id"], "option.judgement-display", ["normal"])
        labels = [label_row(grade_rows[key]) for key in ("perfect", "other")]
        label_cycle(labels[0], blue["children"][0]["resource"])
        label_display = container(labels)
        visibility(label_display["id"], "option.judgement-display", ["label-only"])
        layout_gate["children"] = [normal, label_display]
        digit_ids = {node["id"] for node in walk(normal) if node.get("type") == "text"}
        result["variants"] = [variant for variant in result["variants"]
                              if not (variant["target"] in digit_ids and variant["property"] == "visible"
                                      and variant["source"] == "option.judgement-display")]
        result["variants"].append({"id": identifier(), "target": layout_gate["id"], "property": "x", "source": "layout.presentation",
            "default": 0, "cases": [{"id": identifier(), "key": "p2", "value": deltas[0]},
                                     {"id": identifier(), "key": "dp", "value": dp_x[0]}]})
        result["variants"].append({"id": identifier(), "target": layout_gate["id"], "property": "y", "source": "layout.presentation",
            "default": 0, "cases": [{"id": identifier(), "key": "dp", "value": dp_y[0]}]})
    require(len(updated_manifest["bitmapFonts"]) == 4, "Each selected Judgement contribution owns precisely four ten-digit fonts")
    return updated_manifest, result, {"sharedTemplate": first["id"], "originalTemplates": [first["id"], second["id"], dual["id"]],
        "materialInstances": copy.deepcopy(result["instances"]), "spSideOffset": deltas[0],
        "dpOffset": [dp_x[0], dp_y[0]],
        "phasePolicy": "OMS author adaptation: blue+b, white+g, pink+r; 100ms each, 300ms loop, judgement.hit relative origin",
        "zeroComboPolicy": "Normal: BMS standalone centred label, mania keeps its original zero digit; label-only: one centred label in either ruleset at every combo; shared JP/brightness gates retained",
        "fontCount": 4, "sceneBytesBefore": original_bytes, "sceneBytesAfter": encoded_size(result)}


def frame_path_plan(manifests, frame_manifests, existing_hashes, *, preserved_paths=(), prefix_length=6):
    """Relocate only Frame-exclusive texture paths, with complete-SHA collisions checked."""
    require(prefix_length == 6, "This bounded upgrade uses the reviewed six-hex path policy")
    frame_ids = {r["id"] for path in frame_manifests for r in manifests[path]["resources"]}
    outside_ids = {r["id"] for path, manifest in manifests.items() if path not in frame_manifests for r in manifest["resources"]}
    outside_paths = {r["path"] for path, manifest in manifests.items() if path not in frame_manifests for r in manifest["resources"]}
    candidates = {r["path"] for path in frame_manifests for r in manifests[path]["resources"]
                  if r["id"] in frame_ids - outside_ids and r["path"] not in outside_paths
                  and r["path"] not in preserved_paths and r["path"].endswith(".png")}
    occupied = dict(existing_hashes)
    plan = {}
    for old in sorted(candidates):
        digest = existing_hashes[old]
        new = f"f/{digest[:prefix_length]}.png"
        require(new not in occupied or occupied[new] == digest, "A short Frame path collides with different complete SHA256 content: " + new)
        occupied[new] = digest
        if len(new) < len(old):
            plan[old] = {"path": new, "sha256": digest}
    return plan


def apply_frame_paths(manifest, plan):
    updated = copy.deepcopy(manifest)
    for resource in updated["resources"]:
        if resource["path"] in plan:
            resource["path"] = plan[resource["path"]]["path"]
    return updated
