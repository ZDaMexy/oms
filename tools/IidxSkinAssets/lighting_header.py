"""Sparkle Shower header geometry, original art and exact raw-declaration fallback.

The ordinary Frame builder still uses header_program's white-text foundation.
The existing-header upgrade preserves those two text consumers and all metadata.
"""
from __future__ import annotations

import copy
import hashlib
from pathlib import Path

from PIL import Image

WIDTH, HEIGHT = 1920, 1080
FONT_SCALE = 768 / HEIGHT
LAYOUTS = {"p1": ("center_frame_sp", "1p", 270),
           "p2": ("center_frame_sp", "2p", -270),
           "dp": ("dp_frame_1p", "1p", 0)}
DIFFICULTY_TEXTURES = dict(zip(("BEGINNER", "NORMAL", "HYPER", "ANOTHER", "LEGGENDARIA"),
                             ("playm_beginner", "playm_normal", "playm_hyper", "playm_another", "playm_leggendaria")))
LEVEL_TEXTURES = {str(level): f"lvlnum_{level:02}" for level in range(1, 13)}
# Exact source digests live only in the caller's explicit private inventory.
ART_NAMES = (*DIFFICULTY_TEXTURES.values(), "txt_lv_dot", *LEVEL_TEXTURES.values())


def require(condition, message):
    if not condition:
        raise ValueError(message)


def identifiers(value):
    if isinstance(value, dict):
        result = {value["id"]} if isinstance(value.get("id"), str) else set()
        for item in value.values():
            result.update(identifiers(item))
        return result
    if isinstance(value, list):
        return set().union(*(identifiers(item) for item in value))
    return set()


def walk(root):
    yield root
    for child in root.get("children", []):
        yield from walk(child)


def hook(reference, movie, binding):
    draws = reference["layouts"][movie]["draws"]
    matches = [draw for draw in draws if binding in draw["game_bindings"]]
    require(matches and all(draw["asset"]["package"] == "gmframe33" for draw in matches),
            "The header requires a real Sparkle Shower game-driven source hook")
    bounds = matches[0]["bounds"]
    require(all(draw["bounds"] == bounds for draw in matches), "The source hook has ambiguous geometric bounds")
    return copy.deepcopy(bounds), [{"binding": binding, "texture": draw["texture"],
                                  "asset": draw["asset"], "bounds": draw["bounds"],
                                  "matrix": draw.get("matrix")} for draw in matches]


def overlaps(first, second):
    return (first["left"] < second["right"] and first["right"] > second["left"]
            and first["top"] < second["bottom"] and first["bottom"] > second["top"])


def header_program(reference, layout, prefix, *, mods_rect=None):
    require(reference["stage"] == {"width": WIDTH, "height": HEIGHT, "fps": 60},
            "Do not project another canvas using Sparkle Shower coordinates")
    require(layout in LAYOUTS, "The source has no verified header layout")
    movie, player, dx = LAYOUTS[layout]
    require(reference["layouts"][movie]["package"] == "gmframe33", "Do not relabel a different cabinet")
    difficulty, difficulty_draws = hook(reference, movie, "diff_" + player)
    label, label_draws = hook(reference, movie, "lv_" + player)
    digits, digit_draws = hook(reference, movie, "lvlnum_" + player)
    level = {"left": min(label["left"], digits["left"]), "top": min(label["top"], digits["top"]),
             "right": max(label["right"], digits["right"]), "bottom": max(label["bottom"], digits["bottom"])}
    protected = (dict(zip(("left", "top", "right", "bottom"),
                         (mods_rect[0] * WIDTH, mods_rect[1] * HEIGHT,
                          (mods_rect[0] + mods_rect[2]) * WIDTH, (mods_rect[1] + mods_rect[3]) * HEIGHT)))
                 if mods_rect is not None else None)
    fields, children, bindings = [], [], []
    for name, suffix, source, source_bounds, draws, size in (
            ("difficulty", "d", "song.difficulty", difficulty, difficulty_draws, 22),
            ("level", "l", "song.level", level, label_draws + digit_draws, 20)):
        bounds = {**source_bounds, "left": source_bounds["left"] + dx, "right": source_bounds["right"] + dx}
        adaptation = None
        if protected is not None and overlaps(bounds, protected):
            require(name == "level" and layout == "dp", "Do not hide a header conflict with another layout")
            # The real DP modsRect covers the complete original LV./digit opening.
            # Use the adjacent header space above that rectangle, keeping difficulty intact.
            bounds = {"left": difficulty["right"] + 4, "top": difficulty["top"],
                      "right": protected["right"], "bottom": difficulty["bottom"]}
            require(bounds["right"] - bounds["left"] >= 36 and not overlaps(bounds, protected),
                    "The real native Mod display leaves no readable adjacent header area")
            adaptation = "OMS DP level moved beside the original difficulty opening, above the actual native modsRect; original level opening is fully occupied by that rectangle."
        # Nine significant digits retain renderer precision without increasing the scene with long decimals.
        geometry = {"x": bounds["left"] / WIDTH, "y": bounds["top"] / HEIGHT,
                    "width": (bounds["right"] - bounds["left"]) / WIDTH,
                    "height": (bounds["bottom"] - bounds["top"]) / HEIGHT}
        properties = {key: float(format(value, ".9g")) for key, value in geometry.items()}
        properties.update({"font-size": float(format(size * FONT_SCALE, ".9g")), "text": "",
                           "alignment": "centre", "text-overflow": "ellipsis", "colour": "#ffffffff"})
        identifier = prefix + suffix
        children.append({"id": identifier, "type": "text", "target": {"kind": "global"}, "blend": "alpha",
                         "properties": properties, "effects": [], "children": []})
        bindings.append({"id": prefix + "b" + suffix, "target": identifier, "property": "text", "source": source})
        fields.append({"name": name, "node": identifier, "source": source, "sourceMovie": movie,
                       "sourceDraws": draws, "sourceBounds": source_bounds, "translation": [dx, 0],
                       "appliedBounds": bounds, "fontSizeLogical": properties["font-size"],
                       "safeAreaAdaptation": adaptation})
    root = {"id": prefix, "type": "container", "target": {"kind": "global"}, "blend": "alpha",
            "properties": {}, "effects": [], "children": children}
    variant = {"id": prefix + "v", "target": prefix, "property": "visible", "source": "layout.ruleset",
               "default": False, "cases": [{"id": prefix + "c", "key": "bms", "value": True}]}
    return {"root": root, "bindings": bindings, "variants": [variant], "receipt": {
        "layout": layout, "fields": fields, "ruleset": "bms", "nativeFont": "OMS default; no bitmap font or new image",
        "adaptation": "Raw immutable declarations in white native text. Level uses the union of the original LV. and digit openings; no permanent LV. prefix or invented value.",
        "emptyOrUnknown": "Empty sources remain empty. No fallback, level conversion, uppercase transform or difficulty-to-colour mapping.",
        "longText": "Both fields use bounded ellipsis in their recorded appliedBounds; title/artist/BGA/Mod geometry is unchanged.",
        "protectedNativeModsBounds": protected,
        "addedNodes": 3, "addedBindings": 2, "addedVariants": 1, "reservedTextGlyphIncrement": 512}}


def original_art(sprites, expected_sha256):
    """Identify full PNG bytes against the already-validated private inventory."""
    sprites = Path(sprites).resolve(strict=True)
    resources, sources, mapping = [], [], {}
    for index, name in enumerate(ART_NAMES):
        path = sprites / (name + ".png")
        require(path.is_file() and not path.is_symlink() and path.resolve().is_relative_to(sprites), "Use a contained original PNG")
        data = path.read_bytes()
        digest = hashlib.sha256(data).hexdigest()
        require(digest == expected_sha256[name], "Original gmframe33 PNG bytes changed: " + name)
        size = (186, 26) if name in DIFFICULTY_TEXTURES.values() else (46, 20) if name == "txt_lv_dot" else (36, 20)
        with Image.open(path) as image:
            require(image.format == "PNG" and image.size == size and image.mode == "RGBA", "Unexpected original header canvas: " + name)
        identifier, relative = f"h33.r{index}", f"header33/{name}.png"
        resources.append({"id": identifier, "type": "texture", "path": relative, "filter": "nearest"})
        mapping[name] = identifier
        sources.append({"texture": name, "source": str(path), "path": relative, "sha256": digest,
                        "bytes": len(data), "width": size[0], "height": size[1], "resource": identifier})
    return {"resources": resources, "sources": sources, "mapping": mapping}


def _sprite(identifier, bounds, resource):
    properties = {"x": bounds["left"] / WIDTH, "y": bounds["top"] / HEIGHT,
                  "width": (bounds["right"] - bounds["left"]) / WIDTH,
                  "height": (bounds["bottom"] - bounds["top"]) / HEIGHT}
    return {"id": identifier, "type": "sprite", "target": {"kind": "global"}, "blend": "alpha",
            "resource": resource, "properties": {key: float(format(value, ".9g")) for key, value in properties.items()},
            "effects": [], "children": []}


def _variant(identifier, target, prop, source, default, cases):
    field = "resource" if prop == "resource" else "value"
    return {"id": identifier, "target": target, "property": prop, "source": source, "default": default,
            "cases": [{"id": identifier + f".k{index}", "key": key, field: value} for index, (key, value) in enumerate(cases.items())]}


def level_art_geometry(reference, layout, safe_bounds):
    movie, player, dx = LAYOUTS[layout]
    label, _ = hook(reference, movie, "lv_" + player)
    digits, _ = hook(reference, movie, "lvlnum_" + player)
    require((label["right"] - label["left"], label["bottom"] - label["top"]) == (46, 20)
            and (digits["right"] - digits["left"], digits["bottom"] - digits["top"]) == (36, 20), "Keep the full original Lv and number canvases")
    union = {"left": min(label["left"], digits["left"]), "top": min(label["top"], digits["top"]),
             "right": max(label["right"], digits["right"]), "bottom": max(label["bottom"], digits["bottom"])}
    if layout == "dp":
        # Keep both original canvases at native size. Use the original SP's 2px
        # inter-image gap when relocating this group above the real native Mods.
        sp_label, _ = hook(reference, "center_frame_sp", "lv_1p")
        sp_number, _ = hook(reference, "center_frame_sp", "lvlnum_1p")
        gap = sp_number["left"] - sp_label["right"]
        require(gap == 2, "Use the original 33 two-pixel Lv/number spacing")
        width, height = 46 + gap + 36, 20
        require(safe_bounds["right"] - safe_bounds["left"] >= width
                and safe_bounds["bottom"] - safe_bounds["top"] >= height,
                "The DP safe area cannot fit the original-size 84x20 Lv+whole-number group")
        left = (safe_bounds["left"] + safe_bounds["right"] - width) / 2
        top = (safe_bounds["top"] + safe_bounds["bottom"] - height) / 2
        applied_label = {"left": left, "top": top, "right": left + 46, "bottom": top + 20}
        applied_number = {"left": left + 46 + gap, "top": top, "right": left + width, "bottom": top + 20}
        applied_group = {"left": left, "top": top, "right": left + width, "bottom": top + height}
    else:
        def apply(rect):
            return {**rect, "left": rect["left"] + dx, "right": rect["right"] + dx}
        applied_label, applied_number, applied_group = apply(label), apply(digits), apply(union)
        gap = digits["left"] - label["right"]
    return {"label": applied_label, "number": applied_number, "group": applied_group, "scale": 1, "gap": gap,
            "sourceLabel": label, "sourceNumber": digits, "sourceGroup": union,
            "adaptation": "Original-size 46x20 and 36x20 canvases with the source SP's 2px gap, centered in the existing DP safe area; native Mods rectangle unchanged. Original DP source bounds remain recorded separately." if layout == "dp" else "Original upright GEO bounds and existing SP translation"}


def reverse_header_art(scene, receipt):
    restored = copy.deepcopy(scene)
    roots = [restored["root"], *(template["root"] for template in restored["templates"])]
    header = next(node for root in roots for node in walk(root) if node["id"] == receipt["header"])
    added = set(receipt["addedRootNodes"])
    require({node["id"] for node in header["children"] if node["id"] in added} == added, "The added header art is no longer reversible")
    header["children"] = [node for node in header["children"] if node["id"] not in added]
    variant_ids = set(receipt["addedVariantIds"])
    require({variant["id"] for variant in restored["variants"] if variant["id"] in variant_ids} == variant_ids, "The added variants are no longer reversible")
    restored["variants"] = [variant for variant in restored["variants"] if variant["id"] not in variant_ids]
    return restored


def upgrade_header(scene, reference, layout, fallback_prefix, prefix, occupied, art, *, mods_rect):
    """Upgrade the current existing white hx consumers; do not synthesize another header."""
    require(layout != "dp" or mods_rect is not None, "DP original art requires its actual native Mods rectangle")
    program = header_program(reference, layout, fallback_prefix, mods_rect=mods_rect)
    targets = {binding["source"]: binding["target"] for binding in scene["bindings"]
               if binding["source"] in ("song.title", "song.artist")}
    require(set(targets) == {"song.title", "song.artist"}, "Use the existing real song information tree")
    templates = [template for template in scene["templates"]
                 if template["root"].get("slot") == "decoration"
                 and set(targets.values()).issubset(node["id"] for node in walk(template["root"]))]
    require(len(templates) == 1, "The two metadata fields must share one existing information template")
    template = templates[0]
    instances = [instance for instance in scene["instances"] if instance["template"] == template["id"]]
    require(len(instances) == 1 and instances[0].get("material", {}).get("slot") == "decoration"
            and template["root"]["target"] == {"kind": "global"} and not template["root"]["properties"],
            "Header bounds require the one existing full-screen Global information marker")
    result = copy.deepcopy(scene)
    host = next(item["root"] for item in result["templates"] if item["id"] == template["id"])
    headers = [node for node in walk(host) if node["id"] == fallback_prefix]
    require(len(headers) == 1 and headers[0] == program["root"], "Use the current unmodified hx text fallback geometry")
    require(all(binding in scene["bindings"] for binding in program["bindings"])
            and program["variants"][0] in scene["variants"], "Keep the existing raw text bindings and BMS scope gate")
    fallback_ids = {fallback_prefix + "d", fallback_prefix + "l"}
    require(not any(item["target"] in fallback_ids and item["property"] in ("visible", "opacity")
                    for field in ("bindings", "variants", "tracks") for item in scene.get(field, [])),
            "Do not introduce a second fallback visibility writer")
    mapping = art["mapping"]
    require(set(mapping) == set(ART_NAMES), "Declare the complete original eighteen-image set")
    bounds = program["receipt"]["fields"][0]["appliedBounds"]
    require((bounds["right"] - bounds["left"], bounds["bottom"] - bounds["top"]) == (186, 26), "Keep the complete difficulty canvas and its aspect ratio")
    difficulty = _sprite(prefix + ".d", program["receipt"]["fields"][0]["appliedBounds"], mapping["playm_beginner"])
    difficulty["properties"]["visible"] = False
    geometry = level_art_geometry(reference, layout, program["receipt"]["fields"][1]["appliedBounds"])
    protected = program["receipt"]["protectedNativeModsBounds"]
    require(protected is None or all(not overlaps(bounds, protected) for bounds in (geometry["label"], geometry["number"], geometry["group"])),
            "Original Lv/number art must remain outside the real native Mods rectangle")
    level = {"id": prefix + ".l", "type": "container", "target": {"kind": "global"}, "blend": "alpha",
             "properties": {"visible": False}, "effects": [], "children": [
                 _sprite(prefix + ".lv", geometry["label"], mapping["txt_lv_dot"]),
                 _sprite(prefix + ".n", geometry["number"], mapping["lvlnum_01"])]}
    known_difficulty = {key: mapping[value] for key, value in DIFFICULTY_TEXTURES.items()}
    known_levels = {key: mapping[value] for key, value in LEVEL_TEXTURES.items()}
    variants = [_variant(prefix + ".dr", difficulty["id"], "resource", "song.difficulty", mapping["playm_beginner"], known_difficulty),
                _variant(prefix + ".dv", difficulty["id"], "visible", "song.difficulty", False, dict.fromkeys(known_difficulty, True)),
                _variant(prefix + ".df", fallback_prefix + "d", "visible", "song.difficulty", True, dict.fromkeys(known_difficulty, False)),
                _variant(prefix + ".nr", prefix + ".n", "resource", "song.level", mapping["lvlnum_01"], known_levels),
                _variant(prefix + ".lk", level["id"], "visible", "song.level", False, dict.fromkeys(known_levels, True)),
                _variant(prefix + ".lf", fallback_prefix + "l", "visible", "song.level", True, dict.fromkeys(known_levels, False))]
    additions = {"nodes": [difficulty, level], "variants": variants}
    new_ids = identifiers(additions)
    require(not new_ids.intersection(occupied), "New header art identities collide with existing author content")
    headers[0]["children"].extend(additions["nodes"])
    result["variants"].extend(variants)
    receipt = {**program["receipt"], "header": fallback_prefix, "template": template["id"], "instance": instances[0]["id"],
               "marker": instances[0]["material"]["resource"], "addedIds": sorted(new_ids), "addedRootNodes": [difficulty["id"], level["id"]],
               "addedVariantIds": [variant["id"] for variant in variants], "levelArtGeometry": geometry,
               "knownDifficultyKeys": list(known_difficulty), "knownLevelKeys": list(known_levels),
               "nativeFont": "Existing raw-text fallback only; original PNG branches add no font or text glyph reservation",
               "adaptation": "Known exact Ordinal declarations select complete original PNGs; all existing white consumers, bindings, geometry and BMS visibility remain as fallback",
               "emptyOrUnknown": "No empty case. Unavailable or empty metadata never displays the default sprite; unknown actual strings remain the original raw white text",
               "fallback": "Exact Ordinal keys only. INSANE, unknown, noncanonical or out-of-range level strings retain original raw white text; empty or missing metadata stays empty. No source normalisation.",
               "allOriginalSceneFieldsAndIdsPreserved": True, "addedNodes": 4, "addedBindings": 0, "addedVariants": 6,
               "reservedTextGlyphIncrement": 0, "newFonts": 0}
    require(reverse_header_art(result, receipt) == scene, "An existing scene field changed outside the added art/variants")
    return result, receipt
