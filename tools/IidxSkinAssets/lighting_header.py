"""Read real chart declarations in the verified Sparkle Shower header openings.

The white OMS text is an explicit author adaptation. It is not an extracted
IIDX difficulty label, an IIDX level conversion, or a new metadata authority.
"""
from __future__ import annotations

import copy

WIDTH, HEIGHT = 1920, 1080
FONT_SCALE = 768 / HEIGHT
LAYOUTS = {"p1": ("center_frame_sp", "1p", 270),
           "p2": ("center_frame_sp", "2p", -270),
           "dp": ("dp_frame_1p", "1p", 0)}


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


def append_header(scene, reference, layout, prefix, occupied, *, mods_rect=None):
    """Add to the one existing Global information consumer, never a Stage/Group/Lane pool."""
    program = header_program(reference, layout, prefix, mods_rect=mods_rect)
    new_ids = identifiers({key: value for key, value in program.items() if key != "receipt"})
    require(not new_ids.intersection(occupied), "A new header identity collides with the selected content domain")
    require(not any(binding["source"] in ("song.difficulty", "song.level") for binding in scene.get("bindings", [])),
            "Do not overwrite an existing chart declaration consumer")
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
    host["children"].append(program["root"])
    result["bindings"].extend(program["bindings"])
    result["variants"].extend(program["variants"])
    # Reversibility is the preservation proof for all original typed identities and programs.
    restored = copy.deepcopy(result)
    next(item["root"] for item in restored["templates"] if item["id"] == template["id"])["children"].pop()
    del restored["bindings"][-2:]
    restored["variants"].pop()
    require(restored == scene, "An existing scene field changed")
    return result, {**program["receipt"], "template": template["id"], "instance": instances[0]["id"],
                    "marker": instances[0]["material"]["resource"], "addedIds": sorted(new_ids),
                    "allOriginalSceneFieldsAndIdsPreserved": True}
