"""Append only externally qualified non33 BMS header consumers.

All art digests, original geometry and actual owner identities come from the
explicit private qualification input. This module has no image encoder or host API.
"""
from __future__ import annotations

import copy

from lighting_header import DIFFICULTY_TEXTURES, LEVEL_TEXTURES, FONT_SCALE, identifiers, overlaps, require, walk, _sprite, _variant

BPL = frozenset(("bpl-apina", "bpl-gamepanic", "bpl-gigo", "bpl-leisureland", "bpl-round1", "bpl-silkhat", "bpl-tradz"))
TARGET_LAYOUTS = {"iidx27": ("p1", "p2", "dp"), "iidx28": ("dp",), "iidx31": ("p1", "p2"), "iidx32": ("p1", "p2"),
                  **{frame: ("p1", "p2", "dp") for frame in sorted(BPL)}}
TARGETS = frozenset((frame, layout) for frame, layouts in TARGET_LAYOUTS.items() for layout in layouts)


def known_difficulties(frame):
    # The only available27 diff GEO is130x16; its BEGINNER PNG is94x16.
    # No original placement/scaling for that value has been established.
    return {key: name for key, name in DIFFICULTY_TEXTURES.items() if frame != "iidx27" or key != "BEGINNER"}


def union(first, second):
    return {"left": min(first["left"], second["left"]), "top": min(first["top"], second["top"]),
            "right": max(first["right"], second["right"]), "bottom": max(first["bottom"], second["bottom"])}


def close_bounds(first, second):
    return set(first) == set(second) == {"left", "top", "right", "bottom"} and all(abs(first[key] - second[key]) <= 1e-4 for key in first)


def native_mods(manifest):
    rectangles = sorted({tuple(profile["modsRect"]) for profile in manifest["layouts"] if profile["ruleset"] == "bms"})
    require(rectangles, "Use the actual BMS native Mods rectangles")
    return [dict(zip(("left", "top", "right", "bottom"), (x * 1920, y * 1080, (x + width) * 1920, (y + height) * 1080)))
            for x, y, width, height in rectangles]


def validate_geometry(frame, branch, manifest, art):
    layout = branch["layout"]
    require((frame["id"], layout) in TARGETS, "Do not apply an unqualified layout or33")
    require(frame["difficultyKeys"] == list(known_difficulties(frame["id"])), "Do not silently qualify27 BEGINNER or normalise raw keys")
    required = set(known_difficulties(frame["id"]).values()) | {"txt_lv_dot", *LEVEL_TEXTURES.values()}
    require(set(art["mapping"]) == required, "Declare only the complete actually-consumed original roles")
    protected = native_mods(manifest)
    require(len(protected) == len(branch["nativeModsBounds"])
            and all(close_bounds(first, second) for first, second in zip(protected, branch["nativeModsBounds"], strict=True)),
            "The actual native Mods geometry differs from its private qualification")
    stage = frame["stage"]
    require((stage["width"], stage["height"]) in ((1280, 720), (1920, 1080)), "Use the actual source-stage projection")
    scale = 1920 / stage["width"]
    require(1080 / stage["height"] == scale, "Do not distort a source canvas")
    bounds = {}
    for role, source_names in (("difficulty", known_difficulties(frame["id"]).values()), ("label", ("txt_lv_dot",)), ("number", LEVEL_TEXTURES.values())):
        component = branch["geometry"][role]
        require(component["originalAffineFullCanvasMatch"] and not component["negativeTextScale"], "Use upright, whole original GEO")
        matrix, original = component["matrix"], component["sourceBounds"]
        require(matrix["b"] == matrix["c"] == 0 and matrix["a"] > 0 and matrix["d"] > 0, "No unqualified shear or mirrored text")
        require(all(art["sizes"][name] == component["sourceCanvas"] for name in source_names), "Do not stretch another original glyph canvas into the sampled placeholder")
        width, height = component["sourceCanvas"]
        require(abs((original["right"] - original["left"]) - matrix["a"] * width) < 1e-4
                and abs((original["bottom"] - original["top"]) - matrix["d"] * height) < 1e-4, "The complete source canvas no longer matches its original affine GEO")
        projected, applied = component["projectedBounds"], component["appliedBounds"]
        require(abs(projected["right"] - projected["left"] - (original["right"] - original["left"]) * scale) < 1e-4
                and abs(projected["bottom"] - projected["top"] - (original["bottom"] - original["top"]) * scale) < 1e-4,
                "Keep the actual source scale, including SD1.5 projection")
        require(abs(applied["right"] - applied["left"] - (projected["right"] - projected["left"])) < 1e-4
                and abs(applied["bottom"] - applied["top"] - (projected["bottom"] - projected["top"])) < 1e-4,
                "Do not resize original canvases to fit Mods")
        if branch["originalDirect"]:
            require(close_bounds(applied, projected), "A direct source GEO was moved")
        else:
            aliases = branch["same33"]
            require(frame["id"] in BPL and layout == "dp"
                    and all(aliases[key] for key in ("sourceHeaderGEOExactlyLike33", "nativeModsExactlyLike33", "all18PngsExact33Aliases", "canReuseAlreadyApproved33HeaderGeometryAdaptation")),
                    "Only the seven proven DP aliases may reuse the existing33 author adaptation")
        require(0 <= applied["left"] < applied["right"] <= 1920 and 0 <= applied["top"] < applied["bottom"] <= 1080
                and not any(overlaps(applied, rectangle) for rectangle in protected), "Do not overlap actual native Mods or leave the screen")
        bounds[role] = applied
    bounds["level"] = union(bounds["label"], bounds["number"])
    require(not any(overlaps(bounds["level"], rectangle) for rectangle in protected), "The complete level/fallback opening overlaps native Mods")
    return bounds


def text(identifier, bounds, size):
    properties = {"x": bounds["left"] / 1920, "y": bounds["top"] / 1080,
                  "width": (bounds["right"] - bounds["left"]) / 1920, "height": (bounds["bottom"] - bounds["top"]) / 1080,
                  "font-size": size * FONT_SCALE}
    properties = {key: float(format(value, ".9g")) for key, value in properties.items()}
    properties.update(text="", alignment="centre", **{"text-overflow": "ellipsis", "colour": "#ffffffff"})
    return {"id": identifier, "type": "text", "target": {"kind": "global"}, "blend": "alpha", "properties": properties, "effects": [], "children": []}


def reverse_branch(scene, receipt):
    result = copy.deepcopy(scene)
    host = next(template["root"] for template in result["templates"] if template["id"] == receipt["template"])
    require(sum(child["id"] == receipt["root"] for child in host["children"]) == 1, "The appended header no longer has its exact owner")
    host["children"] = [child for child in host["children"] if child["id"] != receipt["root"]]
    for field in ("bindings", "variants"):
        additions = set(receipt["addedPrograms"][field])
        require({item["id"] for item in result[field]} >= additions, "The appended programs are no longer reversible")
        result[field] = [item for item in result[field] if item["id"] not in additions]
    return result


def append_branch(scene, manifest, frame, branch, art, occupied):
    bounds = validate_geometry(frame, branch, manifest, art)
    require(not any(item["source"] in ("song.difficulty", "song.level") for field in ("bindings", "variants") for item in scene[field]),
            "The other Frame already has difficulty/level consumers; this is not the33 hx upgrade")
    owner = branch["owner"]
    templates = [item for item in scene["templates"] if item["id"] == owner["template"]]
    require(len(templates) == 1, "Use the exact existing information template")
    host = templates[0]["root"]
    instances = [item for item in scene["instances"] if item["template"] == owner["template"]]
    require(host["id"] == owner["root"] and host["target"] == {"kind": "global"} and host.get("slot") == "decoration"
            and host["properties"] == owner["rootProperties"] == {} and instances == owner["instances"] and len(instances) == 1
            and instances[0]["material"] == {"slot": "decoration", "resource": "markers/frame-" + branch["layout"] + "-information"},
            "Do not attach to a pooled, transformed or different material owner")
    node_ids = {node["id"] for node in walk(host)}
    require({"score.value", "scroll.speed", "timing.bpm"}.issubset(item["source"] for item in scene["bindings"] if item["target"] in node_ids),
            "Use the real information owner;27 does not need title/artist consumers")
    prefix = "oh." + frame["id"] + "." + branch["layout"]
    known = known_difficulties(frame["id"])
    mapping = art["mapping"]
    first = mapping[next(iter(known.values()))]
    difficulty = _sprite(prefix + ".d", bounds["difficulty"], first)
    difficulty["properties"]["visible"] = False
    level = {"id": prefix + ".l", "type": "container", "target": {"kind": "global"}, "blend": "alpha",
             "properties": {"visible": False}, "effects": [], "children": [
                 _sprite(prefix + ".lv", bounds["label"], mapping["txt_lv_dot"]),
                 _sprite(prefix + ".n", bounds["number"], mapping["lvlnum_01"])]}
    header = {"id": prefix, "type": "container", "target": {"kind": "global"}, "blend": "alpha",
              "properties": {"visible": False}, "effects": [], "children": [
                  text(prefix + ".df", bounds["difficulty"], 22), text(prefix + ".lf", bounds["level"], 20), difficulty, level]}
    bindings = [{"id": prefix + ".bd", "target": prefix + ".df", "property": "text", "source": "song.difficulty"},
                {"id": prefix + ".bl", "target": prefix + ".lf", "property": "text", "source": "song.level"}]
    variants = [_variant(prefix + ".bms", prefix, "visible", "layout.ruleset", False, {"bms": True}),
                _variant(prefix + ".dr", difficulty["id"], "resource", "song.difficulty", first, {key: mapping[name] for key, name in known.items()}),
                _variant(prefix + ".dv", difficulty["id"], "visible", "song.difficulty", False, dict.fromkeys(known, True)),
                _variant(prefix + ".dfv", prefix + ".df", "visible", "song.difficulty", True, dict.fromkeys(known, False)),
                _variant(prefix + ".nr", prefix + ".n", "resource", "song.level", mapping["lvlnum_01"], {key: mapping[name] for key, name in LEVEL_TEXTURES.items()}),
                _variant(prefix + ".lvv", level["id"], "visible", "song.level", False, dict.fromkeys(LEVEL_TEXTURES, True)),
                _variant(prefix + ".lfv", prefix + ".lf", "visible", "song.level", True, dict.fromkeys(LEVEL_TEXTURES, False))]
    additions = {"root": header, "bindings": bindings, "variants": variants}
    added_ids = identifiers(additions)
    require(not added_ids.intersection(occupied), "New header identities collide with the current package")
    result = copy.deepcopy(scene)
    next(item["root"] for item in result["templates"] if item["id"] == owner["template"])["children"].append(header)
    result["bindings"].extend(bindings); result["variants"].extend(variants)
    receipt = {"frame": frame["id"], "layout": branch["layout"], "root": prefix, "template": owner["template"],
               "instance": instances[0]["id"], "marker": instances[0]["material"]["resource"], "bounds": bounds,
               "addedIds": sorted(added_ids), "addedPrograms": {"bindings": [item["id"] for item in bindings], "variants": [item["id"] for item in variants]},
               "knownDifficultyKeys": list(known), "knownLevelKeys": list(LEVEL_TEXTURES), "addedNodes": len(list(walk(header))),
               "addedBindings": 2, "addedVariants": 7, "addedCases": sum(len(item["cases"]) for item in variants),
               "additionalReservedTextGlyphs": 512, "additionalBitmapFonts": 0,
               "qualification": branch["qualification"], "unknown": "Exact raw text only; empty remains empty, INSANE is not LEGGENDARIA, no level clamping or parsing."}
    require(reverse_branch(result, receipt) == scene, "An unplanned typed field, owner, animation, layout or ID changed")
    return result, receipt


def rewrite_catalog(catalog, replacements):
    result = copy.deepcopy(catalog)
    touched = set()
    for module in result["modules"]:
        for choice in module["choices"]:
            for part in (choice, *choice.get("variants", [])):
                key = (part.get("scene"), part.get("manifest"))
                if key not in replacements:
                    continue
                require(module["id"] == "frame" and choice["value"] in TARGET_LAYOUTS, "A replaced contribution also serves an unqualified consumer")
                layout = part.get("when", {}).get("layout", "dp")
                require((choice["value"], layout) in TARGETS, "A replaced contribution also serves an excluded layout")
                part["scene"], part["manifest"] = replacements[key]
                touched.add(key)
    require(touched == set(replacements) and len(touched) == 29, "Replace exactly29 actual independent scene/manifest pairs")
    inverse = {new: old for old, new in replacements.items()}
    require(len(inverse) == 29, "Distinct layouts unexpectedly collapsed")
    restored = copy.deepcopy(result)
    for module in restored["modules"]:
        for choice in module["choices"]:
            for part in (choice, *choice.get("variants", [])):
                key = (part.get("scene"), part.get("manifest"))
                if key in inverse: part["scene"], part["manifest"] = inverse[key]
    require(restored == catalog, "An option, condition, INI, preview, unqualified branch or other module changed")
    return result
