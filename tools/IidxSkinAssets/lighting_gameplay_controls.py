"""Original gameplay controls on read-only OMS speed and cover projections.

The source AFP/GEO data establish artwork and geometry. The actual ruleset owns
the speed calculation; the Normal ladder follows its selected 1..20 preset.
Score SUB TARGET is a fallback for an unavailable primary, never a third bar.
"""
from __future__ import annotations

import copy
import hashlib
import io
import json
from pathlib import Path
import struct

from lighting_panels import GRAPH_FONT, walk
from lighting_start_options import _declarations

PACKAGE = "contents/data/graphic/1/gameparts.ifs"
FONT = "gc.font"
COVER_MARKER = "gameplay/controls/stage-readout"
LIFT_MARKER = "custom/markers/v/readout"
SCALE = 768 / 1080
TARGETS = ("maximum", "personal-best", "aaa", "aa", "a", "percentage")
LAYOUTS = ("p1", "p2", "dp")
SOURCE_MOVIES = ("spd_p1", "spd_p2", "spd_dp", "spd2_p1", "spd2_p2", "spd2_dp",
                 "spd3_p1", "spd3_p2", "spd3_dp", "lane_sud", "lane_lift_sud",
                 "x_sub_length_lane", "x_notes_time_lane", "x_lift_number")
CLASSIC_ASSET_NAMES = ("tx_num1_4", "playm_classic_spd_sel")
CLASSIC_ASSET_SIZES = {"tx_num1_4": (90, 394), "playm_classic_spd_sel": (150, 70)}
CLASSIC_VALUES = (4, 3.75, 3.5, 3.25, 3, 2.75, 2.5, 2.25, 2, 1.5, 1)
CLASSIC_GEOMETRY = {
    "p1": {"strip": (555, 320, 90, 394), "center": (600, 331), "notes": (574, 264, 84, 12)},
    "p2": {"strip": (1281, 320, 90, 394), "center": (1326, 331), "notes": (1269, 264, 84, 12)},
    "dp": {"strip": (915, 98, 90, 394), "center": (960, 109), "notes": (918, 37, 84, 12)},
}


def require(value, message):
    if not value:
        raise ValueError(message)


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def inside(root, relative):
    require(isinstance(relative, str) and relative and ":" not in relative and "\\" not in relative,
            "Use a contained source path")
    part = Path(relative)
    require(not part.is_absolute() and ".." not in part.parts, "A source path escapes its independent owner")
    path = (root / part).resolve()
    require(path.is_relative_to(root), "A source path escapes its independent owner")
    return path


def _json(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def _node(identifier, kind="container", properties=None, *, resource=None, children=(), slot=None):
    result = {"id": identifier, "type": kind, "target": {"kind": "global"}, "blend": "inherit",
              "properties": properties or {}, "effects": [], "children": list(children)}
    if resource is not None:
        result["resource"] = resource
    if slot is not None:
        result["slot"] = slot
    return result


def empty_scene(identifier):
    return {"contract": "oms-gameplay-skin-scene.v1", "root": _node(identifier),
            "templates": [], "instances": [], "bindings": [], "variants": [], "tracks": [], "stateMachines": []}


def _bind(scene, target, prop, source):
    scene["bindings"].append({"id": target + ".b." + prop, "target": target, "property": prop, "source": source})


def _gate(scene, identifier, source, values, *, children=(), properties=None):
    result = _node(identifier, properties={"visible": False, **(properties or {})}, children=children)
    scene["variants"].append({"id": identifier + ".v", "target": identifier, "property": "visible", "source": source,
                              "default": False, "cases": [{"id": identifier + ".c" + str(index), "key": value,
                                                              "value": True} for index, value in enumerate(values)]})
    return result


def _template(scene, root, material):
    scene["templates"].append({"id": root["id"] + ".t", "root": root})
    scene["instances"].append({"id": root["id"] + ".i", "template": root["id"] + ".t",
                               "material": {"slot": root["slot"], "resource": material}})


def _pixel(identifier, bounds, resource):
    x, y, width, height = bounds
    return _node(identifier, "sprite", {"x": x / 1920, "y": y / 1080,
                                       "width": width / 1920, "height": height / 1080}, resource=resource)


def _text(identifier, bounds, text="", *, size=20, font=FONT, **properties):
    x, y, width, height = bounds
    props = {"x": x / 1920, "y": y / 1080, "width": width / 1920, "height": height / 1080,
             "text": text, "font-size": size * SCALE, "colour": "#ffffffff", **properties}
    if font is not None:
        props.update({"bitmap-font": font, "format": props.get("format", "integer")})
    return _node(identifier, "text", props)


def _centered_text(identifier, center_x, center_y, text="", **properties):
    # SpriteText's centre alignment owns the natural glyph origin. A complete
    # canvas carrier can therefore position the centre without a fake text box.
    return _node(identifier, "text", {"x": center_x / 1920 - 0.5, "y": center_y / 1080 - 0.5,
        "text": text, "font-size": 20 * SCALE, "colour": "#ffffffff", "bitmap-font": FONT,
        "format": "integer", "alignment": "centre", **properties})


def _matrix(first, second):
    a, b, c, d, x, y = first
    e, f, g, h, u, v = second
    return (a * e + c * f, b * e + d * f, a * g + c * h, b * g + d * h,
            a * u + c * v + x, b * u + d * v + y)


def source_reference(library):
    """Extract static source geometry without evaluating host ActionScript."""
    from PIL import Image

    library = Path(library).resolve(strict=True)
    index = _json(library / "library.json")
    require(index.get("status") == "complete" and index.get("sourceMetadataUnchanged") is True,
            "Use the complete independently verified texture library")
    folder = "author-data/contents/data/graphic/1/gameparts/"
    metadata = _json(library / folder / "package.json")
    geometry = {item["name"]: item for item in metadata["geometry"]}
    movies, originals = {}, []

    def movie(name):
        entry = next(item for item in metadata["movies"] if item["name"] == name)
        require(entry["parsed"] and sha(inside(library, entry["raw"])) == entry["sha256"], "A source AFP changed")
        path = inside(library, entry["data"])
        originals.append({"kind": "afp", "name": name, "rawSha256": entry["sha256"],
                          "parsedSha256": sha(path), "parsed": entry["data"]})
        return _json(path)

    for name in SOURCE_MOVIES:
        data = movie(name)
        definitions = {}

        def definitions_in(tags):
            for tag in tags:
                if tag.get("id") is not None:
                    definitions[tag["id"]] = tag
                if tag["type"] == "AP2DefineSpriteTag":
                    definitions_in(tag["tags"])
        definitions_in(data["tags"])
        shapes, active = [], set()

        def expand(identifier, transform=(1, 0, 0, 1, 0, 0), path=(), settled=False):
            require(identifier not in active, "A source control symbol recurses")
            definition = definitions.get(identifier)
            if definition is None:
                return  # aeplib is an imported host helper, not artwork.
            if definition["type"] == "AP2ShapeTag":
                entry = geometry[definition["reference"]]
                require(entry["parsed"] and sha(inside(library, entry["raw"])) == entry["sha256"], "A source GEO changed")
                shape = _json(inside(library, entry["data"]))
                regions = [draw["region"] for draw in shape["draw_params"] if draw.get("region")]
                require(len(set(regions)) <= 1, "A source control shape needs projective sampling")
                if regions:
                    a, b, c, d, x, y = transform
                    require(b == c == 0 and a != 0 and d > 0,
                            f"Control source artwork needs explicit geometry: {name}/{regions[0]} {transform}")
                    bounds = shape["bounds"]
                    shapes.append({"region": regions[0], "bounds": [min(x + a * bounds["left"], x + a * bounds["right"]), y + d * bounds["top"],
                        abs(a) * (bounds["right"] - bounds["left"]), d * (bounds["bottom"] - bounds["top"])], "mirroredX": a < 0,
                        "instances": list(path), "geometry": definition["reference"], "geoSha256": entry["sha256"]})
                return
            if definition["type"] != "AP2DefineSpriteTag":
                return
            active.add(identifier)
            frames = definition["frames"] if settled else definition["frames"][:1]
            placed = {}
            for frame in frames:
                for tag in definition["tags"][frame["start_tag_offset"]:frame["start_tag_offset"] + frame["num_tags"]]:
                    if tag["type"] != "AP2PlaceObjectTag":
                        continue
                    if tag["update"]:
                        require(tag["depth"] in placed, "A source control update has no placement")
                        old = placed[tag["depth"]]
                        old.update({key: value for key, value in tag.items() if value is not None and key not in ("update", "triggers")})
                    else:
                        placed[tag["depth"]] = copy.deepcopy(tag)
            for tag in placed.values():
                t = tag.get("transform") or {"a": 1, "b": 0, "c": 0, "d": 1, "tx": 0, "ty": 0}
                o = tag.get("rotation_origin") or {"x": 0, "y": 0}
                local = (t["a"], t["b"], t["c"], t["d"], t["tx"] - t["a"] * o["x"] - t["c"] * o["y"],
                         t["ty"] - t["b"] * o["x"] - t["d"] * o["y"])
                expand(tag["source_tag_id"], _matrix(transform, local), (*path, tag.get("movie_name") or str(tag["depth"])))
            active.remove(identifier)
        expand(data["exported_tags"][name], settled=True)
        movies[name] = {"canvas": data["location"], "fps": data["fps"], "shapes": shapes}

    textures = {(item["package"], Path(item["name"]).stem): item for item in index["textures"]}
    tint = []
    for digit in range(10):
        white = textures[PACKAGE, f"notes_time_num{digit}"]
        green = textures[PACKAGE, f"notes_time_num{digit}g"]
        require(sha(inside(library, white["sprite"])) == white["sha256"]
                and sha(inside(library, green["sprite"])) == green["sha256"], "A numeric source PNG changed")
        with Image.open(inside(library, white["sprite"])) as w, Image.open(inside(library, green["sprite"])) as g:
            wp, gp = list(w.convert("RGBA").getdata()), list(g.convert("RGBA").getdata())
            require(w.size == g.size == (22, 20) and len(wp) == len(gp), "White/green digits need different source geometry")
            require(all(a[3] == b[3] and (not a[3] or (round(a[0] * 100 / 255), a[1], 0) == b[:3])
                        for a, b in zip(wp, gp, strict=True)), "The original green digits are not a rendered tint of the white font")
        tint.append({"digit": digit, "whiteSha256": white["sha256"], "greenSha256": green["sha256"],
                     "sameAlpha": True, "visiblePixelTint": "#64ff00"})
    panels = {}
    for mode, prefix in (("normal", "spd"), ("floating", "spd2"), ("classic", "spd3")):
        panels[mode] = {}
        for layout in LAYOUTS:
            movie_name = prefix + "_" + layout
            rows = movies[movie_name]["shapes"]
            region = {"normal": "spd_window", "floating": "floating_spd_window", "classic": "spd_classic_window"}[mode]
            region += "_dp" if layout == "dp" else ""
            window, = [item for item in rows if item["region"] == region]
            panels[mode][layout] = {"movie": movie_name, "window": window, "shapes": rows}
    return {"contract": "oms-iidx-gameplay-control-source.v1", "library": str(library), "originalHddAccessed": False,
            "sourceMovies": originals, "movies": movies, "panels": panels, "sharedWhiteGreenFont": tint,
            "review": "AFP/GEO placement and original PNGs only. Default digit placeholders do not prove original host values or speed mapping."}


def derive_control_png(name, source_data):
    """The two bounded pixel adaptations required by the current text owner."""
    from PIL import Image

    patches = {"floating_spd_window": ((152, 442), (73, 425, 9, 9)),
               "floating_spd_window_dp": ((128, 498), (60, 437, 9, 9))}
    if name != "playm_spd_dot" and name not in patches:
        return source_data, None
    image = Image.open(io.BytesIO(source_data)).convert("RGBA")
    source_pixels = image.tobytes()
    if name == "playm_spd_dot":
        require(image.size == (8, 8), "The original speed point no longer has its measured 8x8 bounds")
        result = Image.new("RGBA", (8, 20), (0, 0, 0, 0))
        result.paste(image, (0, 12))
        require(result.crop((0, 12, 8, 20)).tobytes() == source_pixels
                and result.crop((0, 0, 8, 12)).tobytes() == bytes(8 * 12 * 4), "The point's original RGBA was changed")
        proof = {"operation": "transparent-line-canvas", "sourceDimensions": [8, 8], "dimensions": [8, 20],
                 "sourceRgbaPlacement": [0, 12, 8, 8], "sourceRgbaUnchanged": True, "paddingRgba": [0, 0, 0, 0],
                 "lineHeightAndAdvanceUnchanged": [20, 8]}
    else:
        dimensions, (x, y, width, height) = patches[name]
        require(image.size == dimensions, "A floating panel no longer has its measured source dimensions")
        colour = (255, 132, 0, 255)
        require(all(image.getpixel((px, py)) == colour for px in range(x - 1, x + width + 1)
                    for py in (y - 1, y + height))
                and all(image.getpixel((px, py)) == colour for px in (x - 1, x + width)
                        for py in range(y, y + height)), "The baked point is no longer surrounded by the measured flat orange")
        result = image.copy()
        result.paste(colour, (x, y, x + width, y + height))
        actual = result.tobytes()
        changed = []
        for index in range(image.width * image.height):
            if source_pixels[index * 4:index * 4 + 4] != actual[index * 4:index * 4 + 4]:
                px, py = index % image.width, index // image.width
                require(x <= px < x + width and y <= py < y + height, "A panel pixel changed outside its baked point")
                changed.append(index)
        require(changed, "The floating panel's baked point was not found")
        proof = {"operation": "remove-baked-point-under-owned-text", "dimensions": list(image.size),
                 "patch": [x, y, width, height], "fillRgba": list(colour), "changedPixels": len(changed),
                 "outsidePatchRgbaUnchanged": True, "surroundingRgbaMeasuredEqual": True}
    proof.update(sourceRgbaSha256=hashlib.sha256(source_pixels).hexdigest(),
                 rgbaSha256=hashlib.sha256(result.tobytes()).hexdigest())
    encoded = io.BytesIO()
    result.save(encoded, format="PNG", compress_level=9)
    return encoded.getvalue(), proof


def prepare_assets(library, output, reference):
    library, output = Path(library).resolve(strict=True), Path(output).resolve(strict=True)
    require(not output.is_relative_to(library) and not library.is_relative_to(output), "Keep source and author independent")
    index = _json(library / "library.json")
    rows = {(item["package"], Path(item["name"]).stem): item for item in index["textures"]}
    names = {f"notes_time_num{digit}" for digit in range(10)} | {"playm_spd_dot", "tx_sub_length", "tx_notes_time", "slider_bar",
             "tx_hispeed", "tx_floating_hispeed", "tx_classic_hispeed", "tx_current_hispeed", "playm_spd_sel"}
    for mode in reference["panels"].values():
        names.update(panel["window"]["region"] for panel in mode.values())
    resources, mapping, receipt = [], {}, []
    # Append the newly used original labels, preserving every existing resource
    # identity and the source font's unchanged glyph mapping/metrics.
    ordered_names = [*sorted(names), *(name for name in ("tx_fspd_num10", "tx_fspd_num05", *CLASSIC_ASSET_NAMES) if name not in names)]
    for index, name in enumerate(ordered_names):
        row = rows[PACKAGE, name]
        require(row["sampler"]["min"] == row["sampler"]["mag"] and row["sampler"]["mag"] in ("linear", "nearest"),
                "A control texture uses an unsupported sampler")
        source = inside(library, row["sprite"])
        require(sha(source) == row["sha256"], "A source control PNG changed")
        source_data = source.read_bytes()
        output_data, derivation = derive_control_png(name, source_data)
        digest = hashlib.sha256(output_data).hexdigest()
        relative = "art/gc/" + digest[:16] + ".png"
        target = inside(output, relative)
        if target.exists():
            require(target.read_bytes() == output_data, "A control output conflicts")
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(output_data)
        identifier = "gc.a" + format(index, "x")
        mapping[name] = identifier
        resources.append({"id": identifier, "type": "texture", "path": relative, "filter": row["sampler"]["mag"]})
        receipt.append({"name": name, "source": row["sprite"], "sourceSha256": row["sha256"], "path": relative, "sha256": digest,
                        "width": row["width"], "height": row["height"], "originalPngBytesPreserved": derivation is None,
                        **({"derivation": derivation} if derivation else {})})
    glyphs = [{"character": str(digit), "resource": mapping[f"notes_time_num{digit}"], "advance": 21} for digit in range(10)]
    glyphs.append({"character": ".", "resource": mapping["playm_spd_dot"], "advance": 8})
    return {"resources": resources, "bitmapFonts": [{"id": FONT, "lineHeight": 20, "glyphs": glyphs}],
            "mapping": mapping, "receipt": receipt}


def _readout_row(scene, identifier, value_source, assets, *, stage_width, stage_height, above):
    """Fixed-pixel leaf rows in the complete, uncompressed native stage rect.

    Logical pixels are legal only on a horizontal container's direct Sprite
    leaves. Font leaves use their natural glyph size. The two zero-height row
    carriers position leaf centres without stretching a label with the cover.
    """
    row = _node(identifier)
    label_y, number_y = (-30, -14) if above else (10, 26)
    pad = (stage_width - 432) / 2
    for suffix, source, center, label, colour in (
            ("w", value_source, 151, "tx_sub_length", "#ffffffff"),
            ("g", "scroll.green-number", 281, "tx_notes_time", "#64ff00ff")):
        x = (pad + center) / stage_width
        label_node = _node(identifier + "." + suffix + "l", "sprite", {"height-px": 12 * SCALE}, resource=assets[label])
        label_row = _node(identifier + "." + suffix + "lr", properties={"x": x, "y": label_y / stage_height,
            "width": 0, "height": 0, "layout": "horizontal", "gap-px": 0}, children=[label_node])
        text = _node(identifier + "." + suffix, "text", {"text": "", "bitmap-font": FONT,
            "font-size": 20 * SCALE, "format": "integer", "colour": colour})
        text_row = _node(identifier + "." + suffix + "nr", properties={"x": x, "y": number_y / stage_height,
            "width": 0, "height": 0, "layout": "horizontal", "gap-px": 0}, children=[text])
        _bind(scene, text["id"], "text", source)
        row["children"].extend((label_row, text_row))
    return row


def upgrade_hi_speed_readouts(scene):
    """Add the native BMS multiplier while retaining the existing parameter where unavailable."""
    result = copy.deepcopy(scene)
    roots = [result["root"], *(item["root"] for item in result.get("templates", []))]
    nodes = {item["id"]: item for root in roots for item in walk(root)}
    parents = {child["id"]: item for item in nodes.values() for child in item.get("children", [])}
    upgraded = []
    for binding in list(result["bindings"]):
        if binding["property"] != "text" or binding["source"] != "scroll.speed":
            continue
        target = binding["target"]
        if any(item["target"] == target and item["source"] == "scroll.hi-speed-available" for item in result.get("variants", [])):
            continue
        require(target in parents and nodes[target]["type"] == "text", "A Hi-Speed readout needs its existing container and text owner")
        require(not any(item["target"] == target for item in result["tracks"]), "A directly animated Hi-Speed text needs its animation ownership qualified before cloning")
        require(sum(item["target"] == target for item in result["bindings"]) == 1
                and not any(item.get("target") == target for key in ("variants", "stateMachines") for item in result.get(key, [])),
                "A Hi-Speed text with other direct controllers needs its ownership qualified before cloning")
        native_id = target + ".actual"
        require(native_id not in nodes, "The native Hi-Speed readout identity is already occupied")
        native = copy.deepcopy(nodes[target])
        native["id"] = native_id
        parents[target]["children"].append(native)
        nodes[native_id] = native
        parents[native_id] = parents[target]
        result["bindings"].append({**binding, "id": binding["id"] + ".actual", "target": native_id, "source": "scroll.hi-speed"})
        for identifier, default, selected in ((target, True, False), (native_id, False, True)):
            result["variants"].append({"id": identifier + ".hs-availability", "target": identifier,
                "property": "visible", "source": "scroll.hi-speed-available", "default": default,
                "cases": [{"id": identifier + ".hs-availability.true", "key": "true", "value": selected}]})
        upgraded.append({"legacy": target, "native": native_id})
    current_formats = []
    floating_gates = {item["target"] for item in result["variants"] if item["source"] == "scroll.mode"
        and item["property"] == "visible" and item["default"] is False
        and any(case["key"] == "floating" and case["value"] is True for case in item["cases"])
        and all(case["key"] == "floating" or case["value"] is False for case in item["cases"])}
    for binding in result["bindings"]:
        if binding["property"] != "text" or binding["source"] not in ("scroll.speed", "scroll.hi-speed"):
            continue
        identifier = binding["target"]
        owner = nodes[identifier]
        while owner["id"] not in floating_gates and owner["id"] in parents:
            owner = parents[owner["id"]]
        if owner["id"] not in floating_gates:
            continue
        properties = nodes[identifier]["properties"]
        require(properties.get("format") in ("fixed-2", "fixed-2-pad2"), "Keep the qualified original Floating CURRENT numeric owner")
        if properties["format"] == "fixed-2":
            properties["format"] = "fixed-2-pad2"
            current_formats.append(identifier)
    return result, {"readouts": upgraded, "currentFormats": current_formats,
        "scope": "Native BMS multiplier when provided; original parameter/art and selector clocks retained. Floating CURRENT keeps two integer digits."}


def build_controls(reference, assets, *, stage_geometry):
    scene = empty_scene("gc.root")
    mapping = assets["mapping"]
    def original_sprite(identifier, original, resource):
        item = _pixel(identifier, original["bounds"], resource)
        if original.get("mirroredX"):
            item["type"] = "projective-sprite"
            scene["tracks"].append({"id": identifier + ".mirror", "type": "frame", "target": identifier,
                "property": "projective-quad", "easing": "step", "loop": False,
                "keyframes": [{"id": identifier + ".mirror.k", "time": 0,
                    "value": {"vertices": [[1, 0, 1], [0, 0, 1], [0, 1, 1], [1, 1, 1]], "opacity": 1}}]})
        return item
    for layout in LAYOUTS:
        identifier = "gc." + layout
        panel = _node(identifier + ".hold", properties={"visible": False})
        _bind(scene, panel["id"], "visible", "scroll.adjusting")
        root = _node(identifier, slot="decoration", properties={"visible": False, "z": -31}, children=[panel])
        _bind(scene, identifier, "visible", "scroll.available")
        for mode in ("normal", "floating", "classic"):
            source = reference["panels"][mode][layout]
            x, y, width, height = source["window"]["bounds"]
            selected = _gate(scene, identifier + "." + mode, "scroll.mode", [mode])
            selected["children"].append(original_sprite(selected["id"] + ".plate", source["window"], mapping[source["window"]["region"]]))
            label = {"normal": "tx_hispeed", "floating": "tx_floating_hispeed", "classic": "tx_classic_hispeed"}[mode]
            original = [item for item in source["shapes"] if item["region"] == label]
            if original:
                selected["children"].append(original_sprite(selected["id"] + ".label", original[0], mapping[label]))
            minimum, maximum = (1, 20) if mode == "normal" else (0.5, 10)
            if mode == "floating":
                top, bottom = y + 42, y + height - 120
                for suffix, name in (("max", "tx_fspd_num10"), ("min", "tx_fspd_num05")):
                    original, = [item for item in source["shapes"] if item["region"] == name]
                    selected["children"].append(original_sprite(selected["id"] + "." + suffix, original, mapping[name]))
                slider = _pixel(selected["id"] + ".slider", (x + 28, bottom, 66, 10), mapping["slider_bar"])
                keyframes = [(minimum, bottom / 1080), (maximum, top / 1080)]
            else:
                ticks = (20, 18, 16, 14, 12, 10, 8, 6, 4, 2, 1) if mode == "normal" else (10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0.5)
                center = x + (64 if layout == "dp" else 93 if layout == "p2" else 59)
                first = y + (41 if layout == "dp" else 36)
                positions = [(value, first + 37 * index) for index, value in enumerate(ticks)]
                for index, (tick, center_y) in enumerate(positions):
                    selected["children"].append(_centered_text(selected["id"] + ".tick" + str(index), center, center_y, str(tick)))
                slider = _pixel(selected["id"] + ".slider", (center - 65, positions[-1][1] - 35, 130, 70), mapping["playm_spd_sel"])
                keyframes = [(value, (center_y - 35) / 1080) for value, center_y in reversed(positions)]
            selected["children"].append(slider)
            scene["tracks"].append({"id": slider["id"] + ".t", "type": "tween", "target": slider["id"], "property": "y",
                "clock": "scroll.speed", "easing": "linear", "loop": False,
                "keyframes": [{"id": slider["id"] + ".k" + str(index), "time": time, "value": value}
                              for index, (time, value) in enumerate(keyframes)]})
            if mode == "floating":
                digits = [item["bounds"] for item in source["shapes"] if item["region"] == "playm_spd_00"]
                require(len(digits) == 4, "A floating panel no longer has its original four CURRENT digit positions")
                current_left, current_top = min(item[0] for item in digits), min(item[1] for item in digits)
                current_right, current_bottom = max(item[0] + item[2] for item in digits), max(item[1] + item[3] for item in digits)
                value = _centered_text(selected["id"] + ".value", (current_left + current_right) / 2,
                                       (current_top + current_bottom) / 2, format="fixed-2-pad2")
                _bind(scene, value["id"], "text", "scroll.hi-speed")
                selected["children"].append(value)
                actual_label = [item for item in source["shapes"] if item["region"] == "tx_current_hispeed"]
                if actual_label:
                    selected["children"].append(_pixel(selected["id"] + ".current-label", actual_label[0]["bounds"], mapping["tx_current_hispeed"]))
            panel["children"].append(selected)
        normal = reference["panels"]["normal"][layout]["window"]["bounds"]
        x, y = normal[:2]
        green = _centered_text(identifier + ".gn", x + 64, y - 18, colour="#64ff00ff")
        _bind(scene, green["id"], "text", "scroll.green-number")
        label = _pixel(identifier + ".gnl", (x + 22, y - 41, 84, 12), mapping["tx_notes_time"])
        no_hidden = _gate(scene, identifier + ".nohid", "cover.hidden-active", ["false"], children=[label, green])
        no_lift = _node(identifier + ".nolift", children=[no_hidden])
        no_sudden = _gate(scene, identifier + ".nosud", "cover.sudden-active", ["false"], children=[no_lift])
        scene["tracks"].append({"id": no_lift["id"] + ".lift", "type": "tween", "target": no_lift["id"], "property": "opacity",
             "clock": "lift.value", "easing": "step", "loop": False,
             "keyframes": [{"id": no_lift["id"] + ".l0", "time": 0, "value": 1},
                           {"id": no_lift["id"] + ".l1", "time": 1, "value": 0}]})
        panel["children"].append(no_sudden)
        _template(scene, root, "markers/frame-" + layout + "-information")

    stage = _node("gc.stage", slot="decoration", properties={"visible": False, "z": -40})
    _bind(scene, stage["id"], "visible", "scroll.adjusting")
    require(stage_geometry, "Readouts require the actual selected Frame's authored stage dimensions")
    for geometry_index, geometry in enumerate(stage_geometry):
        prefix = "gc.stage.g" + str(geometry_index)
        frame = _gate(scene, prefix, "option.frame", geometry["frames"])
        stage["children"].append(frame)
        for position in ("sudden", "hidden"):
            identifier = prefix + "." + position
            source = "cover." + position + "-units"
            carrier = _readout_row(scene, identifier, source, mapping, stage_width=geometry["width"],
                                   stage_height=geometry["height"], above=position == "sudden")
            carrier["properties"]["visible"] = False
            _bind(scene, identifier, "visible", "cover." + position + "-active")
            frame["children"].append(carrier)
            scene["tracks"].append({"id": identifier + ".edge", "type": "tween", "target": identifier,
                "property": "y", "clock": source, "easing": "linear", "loop": False,
                "keyframes": [{"id": identifier + ".e0", "time": 0, "value": 0 if position == "sudden" else 1},
                              {"id": identifier + ".e1", "time": 1000, "value": 1 if position == "sudden" else 0}]})
        lift = _node(prefix + ".lift", properties={"visible": False})
        _bind(scene, lift["id"], "visible", "option.lift-readout")
        frame["children"].append(lift)
        nonzero = _node(prefix + ".lift.nonzero")
        lift["children"].append(nonzero)
        scene["tracks"].append({"id": nonzero["id"] + ".t", "type": "tween", "target": nonzero["id"],
            "property": "opacity", "clock": "lift.value", "easing": "step", "loop": False,
            "keyframes": [{"id": nonzero["id"] + ".z0", "time": 0, "value": 0}, {"id": nonzero["id"] + ".z1", "time": 1, "value": 1}]})
        for reverse in (False, True):
            identifier = prefix + ".lift." + str(reverse).lower()
            carrier = _readout_row(scene, identifier, "lift.value", mapping, stage_width=geometry["width"],
                                   stage_height=geometry["height"], above=reverse)
            gate = _gate(scene, identifier + ".direction", "lift.reversed", [str(reverse).lower()], children=[carrier])
            nonzero["children"].append(gate)
            scene["tracks"].append({"id": identifier + ".edge", "type": "tween", "target": identifier,
                "property": "y", "clock": "lift.offset", "easing": "linear", "loop": False,
                "keyframes": [{"id": identifier + ".e0", "time": 0, "value": 0 if reverse else 1},
                              {"id": identifier + ".e1", "time": 1, "value": 1 if reverse else 0}]})
    _template(scene, stage, COVER_MARKER)
    return upgrade_classic_presentation(scene, reference, mapping)[0]


def classic_geometry(reference, layout):
    """Use the whole original strip and common numeric baseline, not digit-count formatting."""
    source = reference["panels"]["classic"][layout]
    expected = CLASSIC_GEOMETRY[layout]
    def one(region):
        matches = [item for item in source["shapes"] if item["region"] == region]
        require(len(matches) == 1, "Classic needs its one original " + region + " shape")
        return matches[0]
    strip, cursor, notes = (one(name) for name in (*CLASSIC_ASSET_NAMES, "tx_notes_time"))
    cx, cy = expected["center"]
    require(tuple(strip["bounds"]) == expected["strip"] and not strip.get("mirroredX", False), "Use the complete upright Classic strip GEO")
    require(tuple(cursor["bounds"]) == (cx - 75, cy + 37 * 10 - 35, 150, 70)
            and not cursor.get("mirroredX", False), "Use the original upright 150x70 Classic selector GEO")
    require(tuple(notes["bounds"]) == expected["notes"] and not notes.get("mirroredX", False), "Use the original Classic NOTES TIME anchor")
    digits = [item for item in source["shapes"] if item["region"].startswith("notes_time_num") and item["region"].endswith("g")]
    require(digits and all(item["bounds"][1] == expected["notes"][1] + 12 and item["bounds"][3] == 20
                           and not item.get("mirroredX", False) for item in digits), "Classic numeric source rows need their common 20px baseline")
    label_x, label_y, label_width, _ = expected["notes"]
    return {"strip": strip, "cursor": cursor, "notes": notes, "center": (cx, cy),
            "greenCenter": (label_x + label_width / 2, label_y + 12 + 10),
            "qualification": "Label-centred natural integer text at the common source numeric row; no 3/4-digit padding or original host formatter inferred"}


def _classic_properties(properties):
    return {key: float(format(value, ".9g")) if key in ("x", "y", "width", "height") else value for key, value in properties.items()}


def _classic_pixel(identifier, bounds, resource):
    result = _pixel(identifier, bounds, resource)
    result["properties"] = _classic_properties(result["properties"])
    return result


def _scene_nodes(scene):
    return [item for root in (scene["root"], *(template["root"] for template in scene["templates"])) for item in walk(root)]


def _same_bounds(node, bounds, *, centered=False):
    expected = (bounds[0] / 1920 - (0.5 if centered else 0), bounds[1] / 1080 - (0.5 if centered else 0))
    p = node["properties"]
    position = all(abs(p.get(key, float("inf")) - value) * dimension < 1e-4 for key, value, dimension in zip(("x", "y"), expected, (1920, 1080)))
    return position and (centered or all(abs(p.get(key, float("inf")) * dimension - value) < 1e-4
                                        for key, value, dimension in zip(("width", "height"), bounds[2:], (1920, 1080))))


def reverse_classic_presentation(scene, receipt):
    result = copy.deepcopy(scene)
    by_id = {item["id"]: item for item in _scene_nodes(result)}
    for change in receipt["layouts"]:
        for field in ("oldClassic", "oldGreenGate"):
            original = change[field]
            target = by_id[original["id"]]
            target.clear(); target.update(copy.deepcopy(original))
    old_tracks = {change["oldTrack"]["id"]: change["oldTrack"] for change in receipt["layouts"]}
    result["tracks"] = [copy.deepcopy(old_tracks.get(item["id"], item)) for item in result["tracks"]]
    for field in ("bindings", "variants"):
        added = set(receipt["addedPrograms"][field])
        require({item["id"] for item in result[field]} >= added, "Classic additions are no longer reversible")
        result[field] = [item for item in result[field] if item["id"] not in added]
    return result


def upgrade_classic_presentation(scene, reference, mapping):
    """Replace only three real Classic branches, recognising compact or original IDs structurally."""
    result = copy.deepcopy(scene)
    old_ids = {item["id"] for item in _declarations(scene, _scene_nodes(scene))}
    additions = {"bindings": [], "variants": []}
    changes = []
    for layout in LAYOUTS:
        geometry = classic_geometry(reference, layout)
        templates = [template for template in result["templates"] if any(
            instance["template"] == template["id"] and instance.get("material") == {"slot": "decoration", "resource": "markers/frame-" + layout + "-information"}
            for instance in result["instances"])]
        candidates = []
        for template in templates:
            nodes = {node["id"]: node for node in walk(template["root"])}
            for variant in result["variants"]:
                if variant["target"] in nodes and variant["source"] == "scroll.mode" and variant["property"] == "visible" and variant["default"] is False and len(variant["cases"]) == 1 and all(
                        case["key"] == "classic" and case.get("value") is True for case in variant["cases"]):
                    candidates.append((template, nodes, nodes[variant["target"]]))
        require(len(candidates) == 1, "Use exactly one actual Classic information branch per layout")
        template, nodes, classic = candidates[0]
        require(classic["type"] == "container" and classic["target"] == {"kind": "global"}
                and classic["properties"] == {"visible": False}, "Use the unchanged Classic mode carrier")
        require(template["root"]["target"] == {"kind": "global"} and template["root"].get("slot") == "decoration"
                and template["root"]["properties"] == {"visible": False, "z": -31}, "Classic requires its existing full-canvas control owner")
        require(sum(instance["template"] == template["id"] for instance in result["instances"]) == 1,
                "Do not attach Classic to a pooled information owner")
        children = classic["children"]
        sliders = [child for child in children if any(track["target"] == child["id"] and track.get("clock") == "scroll.speed"
                                                    and track["property"] == "y" for track in result["tracks"])]
        require(len(sliders) == 1 and sliders[0]["type"] == "sprite" and sliders[0]["resource"] == mapping["playm_spd_sel"],
                "Use the actual old130x70 Classic cursor, not an inferred suffix")
        slider = sliders[0]
        cx, first = geometry["center"]
        require(_same_bounds(slider, (cx - 65, first + 37 * 10 - 35, 130, 70)), "The old Classic cursor geometry differs")
        ticks = [child for child in children if child["type"] == "text" and child["properties"].get("bitmap-font") == FONT]
        old_values = (10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0.5)
        require(len(ticks) == 11 and [child["properties"].get("text") for child in ticks] == [str(value) for value in old_values]
                and all(_same_bounds(child, (cx, first + 37 * index), centered=True) for index, child in enumerate(ticks)),
                "Use the actual old eleven text ticks; reject already-upgraded or different Classic branches")
        removed = {child["id"] for child in ticks}
        require(not any(item["target"] in removed for field in ("bindings", "variants", "tracks") for item in result[field]),
                "A Classic static tick has another semantic consumer")
        source = reference["panels"]["classic"][layout]
        static = [child for child in children if child["id"] not in removed and child is not slider]
        expected_static = [(source["window"], mapping[source["window"]["region"]])]
        expected_static.extend((item, mapping["tx_classic_hispeed"]) for item in source["shapes"] if item["region"] == "tx_classic_hispeed")
        require(len(static) == len(expected_static) and all(child.get("resource") == resource and _same_bounds(child, shape["bounds"])
                for child, (shape, resource) in zip(static, expected_static)), "Keep the original Classic plate/title and their exact source placement")
        track, = [track for track in result["tracks"] if track["target"] == slider["id"] and track["property"] == "y"]
        require(track["type"] == "tween" and track["clock"] == "scroll.speed" and track["easing"] == "linear" and track["loop"] is False
                and len(track["keyframes"]) == 11 and [item["time"] for item in track["keyframes"]] == list(reversed(old_values)),
                "Keep the existing linear, clamped Classic clock and track identity")
        green_bindings = [binding for binding in result["bindings"] if binding["source"] == "scroll.green-number" and binding["target"] in nodes]
        require(len(green_bindings) == 1, "The actual control owner needs one existing shared GN numeric carrier")
        green = nodes[green_bindings[0]["target"]]
        green_gates = [node for node in nodes.values() if green in node["children"]]
        require(len(green_gates) == 1 and len(green_gates[0]["children"]) == 2, "Use the existing shared GN pair")
        green_gate = green_gates[0]
        label, = [child for child in green_gate["children"] if child is not green]
        require(label["type"] == "sprite" and label["resource"] == mapping["tx_notes_time"]
                and green["type"] == "text" and green["properties"].get("bitmap-font") == FONT
                and green["properties"].get("format") == "integer" and green["properties"].get("colour") == "#64ff00ff"
                and green["properties"].get("alignment") == "centre", "Retain the actual shared integer/font/tint; no placeholder formatter inference")
        prefix = "cl." + layout
        change = {"layout": layout, "template": template["id"], "oldClassic": copy.deepcopy(classic),
                  "oldTrack": copy.deepcopy(track), "oldGreenGate": copy.deepcopy(green_gate), "removedTickIds": sorted(removed),
                  "geometry": geometry}
        strip = _classic_pixel(prefix + ".s", geometry["strip"]["bounds"], mapping["tx_num1_4"])
        replacement = []
        for child in children:
            if child["id"] in removed:
                if child is ticks[0]: replacement.append(strip)
            else: replacement.append(child)
        classic["children"] = replacement
        # Cursor remains after the strip, as in the real speed(depth5) source layer.
        slider["resource"] = mapping["playm_classic_spd_sel"]
        slider["properties"].update(_classic_pixel(slider["id"], geometry["cursor"]["bounds"], slider["resource"])["properties"])
        for keyframe, (value, index) in zip(track["keyframes"], reversed(list(zip(CLASSIC_VALUES, range(11))))):
            keyframe.update(time=value, value=(first + 37 * index - 35) / 1080)
        # Keep the old pair's IDs/properties/binding in Normal/Floating; only Classic uses the new anchor.
        normal = _node(prefix + ".n", children=green_gate["children"])
        classic_label = _classic_pixel(prefix + ".l", geometry["notes"]["bounds"], mapping["tx_notes_time"])
        classic_green = copy.deepcopy(green); classic_green["id"] = prefix + ".g"
        gx, gy = geometry["greenCenter"]
        classic_green["properties"].update(_classic_properties({"x": gx / 1920 - 0.5, "y": gy / 1080 - 0.5}))
        separate = _node(prefix + ".c", properties={"visible": False}, children=[classic_label, classic_green])
        green_gate["children"] = [normal, separate]
        for suffix, target, default, value in (("nv", normal["id"], True, False), ("cv", separate["id"], False, True)):
            variant = {"id": prefix + "." + suffix, "target": target, "property": "visible", "source": "scroll.mode", "default": default,
                       "cases": [{"id": prefix + "." + suffix + ".c", "key": "classic", "value": value}]}
            result["variants"].append(variant); additions["variants"].append(variant["id"])
        binding = {"id": prefix + ".gb", "target": classic_green["id"], "property": "text", "source": "scroll.green-number"}
        result["bindings"].append(binding); additions["bindings"].append(binding["id"])
        change.update(stripNode=strip["id"], cursorNode=slider["id"], classicGreen=classic_green["id"],
                      oldPairModeGate=normal["id"], classicPairModeGate=separate["id"])
        changes.append(change)
    declarations = _declarations(result, _scene_nodes(result))
    ids = [item["id"] for item in declarations]
    require(len(ids) == len(set(ids)), "Classic additions collide with a current typed identity")
    new_ids = set(ids) - old_ids
    receipt = {"layouts": changes, "addedPrograms": additions, "addedIds": sorted(new_ids),
               "normalFloatingNodesBindingsTracksPreserved": True, "originalFontDefinitionUnchanged": True,
               "clock": "scroll.speed", "offGrid": "Existing linear interpolation and endpoint clamp only; actual legacy values and speed math unchanged"}
    require(reverse_classic_presentation(result, receipt) == scene, "A non-Classic or unplanned typed field changed")
    return result, receipt


def _selected_document(document, template_ids):
    templates = [copy.deepcopy(item) for item in document["templates"] if item["id"] in template_ids]
    node_ids = {node["id"] for template in templates for node in walk(template["root"])}
    require(not any(assignment["target"] in node_ids for machine in document["stateMachines"]
                    for state in machine["states"] for assignment in state["set"]), "A shared component has state-machine ownership")
    selected = empty_scene("detached.root")
    selected["templates"] = templates
    selected["instances"] = [copy.deepcopy(item) for item in document["instances"] if item["template"] in template_ids]
    remaining = copy.deepcopy(document)
    remaining["templates"] = [item for item in remaining["templates"] if item["id"] not in template_ids]
    remaining["instances"] = [item for item in remaining["instances"] if item["template"] not in template_ids]
    for field in ("bindings", "variants", "tracks"):
        selected[field] = [copy.deepcopy(item) for item in document[field] if item["target"] in node_ids]
        remaining[field] = [item for item in remaining[field] if item["target"] not in node_ids]
    return selected, remaining


def select_stage_frame(document, frame):
    """Keep only the already-selected Frame geometry in its C2 contribution."""
    result = copy.deepcopy(document)
    template, = result["templates"]
    root = template["root"]
    selectors = {variant["target"]: variant for variant in result["variants"] if variant["source"] == "option.frame"}
    selected = [child for child in root["children"] if any(case["key"] == frame for case in selectors[child["id"]]["cases"])]
    require(len(selected) == 1, "A Frame has no unique source stage geometry")
    root["children"] = selected[0]["children"]
    retained = {node["id"] for node in walk(root)}
    for field in ("bindings", "variants", "tracks"):
        result[field] = [item for item in result[field] if item["target"] in retained]
    return result


def compact(document, prefix):
    declarations = _declarations(document, [node for root in (document["root"], *(t["root"] for t in document["templates"])) for node in walk(root)])
    ids = [item["id"] for item in declarations]
    require(len(ids) == len(set(ids)), "A component has duplicate declaration identities")
    mapping = {identifier: prefix + format(index, "x") for index, identifier in enumerate(ids)}
    def rewrite(value):
        if isinstance(value, str):
            return mapping.get(value, value)
        if isinstance(value, list):
            return [rewrite(item) for item in value]
        if isinstance(value, dict):
            return {key: rewrite(item) for key, item in value.items()}
        return value
    return rewrite(document)


def compact_static_geometry(scene):
    """Use shorter decimals only where the renderer receives identical floats.

    Track times, tween endpoints, glyph metrics and all other doubles retain
    their original precision. These static properties and variant assignments
    are explicitly converted to framework floats by the production renderer.
    """
    result = copy.deepcopy(scene)
    properties = {"x", "y", "width", "height", "scale-x", "scale-y", "rotation", "z", "font-size", "opacity", "corner-radius"}
    changed = 0
    def shorten(value):
        nonlocal changed
        if type(value) is not float:
            return value
        for precision in range(1, 10):
            candidate = float(format(value, "." + str(precision) + "g"))
            if struct.pack("<f", candidate) == struct.pack("<f", value):
                if len(str(candidate)) < len(str(value)):
                    changed += 1
                    return candidate
                break
        return value
    for root in (result["root"], *(t["root"] for t in result["templates"])):
        for node in walk(root):
            for prop in properties.intersection(node["properties"]):
                node["properties"][prop] = shorten(node["properties"][prop])
    for variant in result["variants"]:
        if variant["property"] in properties:
            variant["default"] = shorten(variant["default"])
            for case in variant["cases"]:
                if "value" in case:
                    case["value"] = shorten(case["value"])
    return result, {"changedLiterals": changed, "renderedFloatBitsUnchanged": True,
                    "trackTimesTweenEndpointsAndFontMetricsUnchanged": True}


def compact_projective_frames(scene):
    """Preserve the exact float inputs of the existing step-frame renderer.

    ProjectiveSprite converts X/Y before multiplying by DrawWidth/Height, Q
    before submitting the vertex, and opacity before assigning Alpha. Numeric
    tween endpoints, frame times, topology and source frame order are untouched.
    """
    result = copy.deepcopy(scene)
    changed = 0
    def shorten(value):
        nonlocal changed
        if type(value) is not float:
            return value
        for precision in range(1, 10):
            candidate = float(format(value, "." + str(precision) + "g"))
            if struct.pack("<f", candidate) == struct.pack("<f", value) and not (value != 0 and candidate == 0):
                if len(str(candidate)) < len(str(value)):
                    changed += 1
                    return candidate
                break
        return value
    frames = 0
    for track in result["tracks"]:
        if track["type"] != "frame" or track["property"] != "projective-quad":
            continue
        for frame in track["keyframes"]:
            frames += 1
            value = frame["value"]
            value["vertices"] = [[shorten(number) for number in vertex] for vertex in value["vertices"]]
            value["opacity"] = shorten(value["opacity"])
    return result, {"changedLiterals": changed, "frames": frames, "renderedVertexAndAlphaFloatBitsUnchanged": True,
                    "sourceFrameOrderTimesAndNumericTweensUnchanged": True}


def extract_graph(document, *, layout):
    selectors = [item for item in document["variants"] if item["source"] == "option.graph-target"]
    require(len(selectors) == 6 and {item["cases"][0]["key"] for item in selectors} == set(TARGETS),
            "Use the six established primary graph targets")
    selected = {item["target"] for item in selectors}
    visible_roots = {item["target"] for item in document["bindings"]
                     if item["source"] == "option.graph-visible" and item["property"] == "visible"}
    template_ids = {template["id"] for template in document["templates"] if template["root"]["id"] in visible_roots}
    require(len(template_ids) == 2, "The graph must retain its independent plate and statistics templates")
    graph, remaining = _selected_document(document, template_ids)
    require(selected <= {node["id"] for template in graph["templates"] for node in walk(template["root"])},
            "All primary targets must belong to the selected statistics owner")
    require(len(graph["instances"]) == 2 and all(item["material"] == {"slot": "decoration", "resource": "markers/frame-" + layout + "-information"}
        for item in graph["instances"]), "The graph no longer matches its actual selected layout marker")
    return compact(graph, "gg." + layout + "."), remaining


def extract_lift_readout(document):
    templates = {item["id"] for item in document["templates"] if item["root"].get("slot") == "playfield.lift.decoration"}
    require(len(templates) == 1, "The original LIFT contribution needs exactly one readout owner")
    selected, remaining = _selected_document(document, templates)
    require(len(selected["instances"]) == 1 and selected["instances"][0]["material"] == {
        "slot": "playfield.lift.decoration", "resource": LIFT_MARKER}, "The LIFT readout marker changed")
    return selected, remaining


def add_sub_target_option(manifest):
    result = copy.deepcopy(manifest)
    require(not any(item["id"] == "graph-sub-target" for item in result["options"]), "A SUB TARGET option already exists")
    primary, = [item for item in result["options"] if item["id"] == "graph-target"]
    require(tuple(item["value"] for item in primary["choices"]) == TARGETS, "Preserve all six primary targets")
    option = {"id": "graph-sub-target", "type": "choice", "label": "SUB TARGET · 主目标无数据时的备用目标", "default": "none",
              "choices": [{"value": "none", "label": "关闭"}, *copy.deepcopy(primary["choices"])]}
    result["options"].insert(result["options"].index(primary) + 1, option)
    return result


def share_graph_layouts(graphs):
    """Prove and reuse the exact P1/DP geometry and the source P2 translation.

    The runtime discriminator is the actual solved layout presentation. Saved
    option values are never used to infer whether the current layout is DP.
    """
    require(set(graphs) == set(LAYOUTS), "Every actual graph layout is required")
    normalized = {}
    for layout, source in graphs.items():
        value = compact(source, "gg.common.")
        for instance in value["instances"]:
            instance["material"]["resource"] = "markers/frame-p1-information"
        normalized[layout] = value
    require(normalized["p1"] == normalized["dp"], "P1 and DP graph declarations are not exactly shared")
    result = copy.deepcopy(normalized["p1"])
    p1_nodes = {node["id"]: node for root in (result["root"], *(t["root"] for t in result["templates"])) for node in walk(root)}
    p2_nodes = {node["id"]: node for root in (normalized["p2"]["root"], *(t["root"] for t in normalized["p2"]["templates"])) for node in walk(root)}
    require(p1_nodes.keys() == p2_nodes.keys(), "P2 graph node ownership differs")
    shift, extra = 1502 / 1920, 104 / 1920
    adapted = []
    for identifier, node in p1_nodes.items():
        other = p2_nodes[identifier]
        require({key: value for key, value in node.items() if key not in ("properties", "children")}
                == {key: value for key, value in other.items() if key not in ("properties", "children")}
                and [child["id"] for child in node.get("children", [])] == [child["id"] for child in other.get("children", [])],
                "P2 graph content differs")
        first, second = node["properties"], other["properties"]
        require(first.keys() == second.keys(), "P2 graph properties differ")
        for prop, value in first.items():
            expected = second[prop]
            if type(value) in (int, float) and type(expected) in (int, float):
                delta = expected - value + (shift if prop == "x" else 0)
                if abs(delta) < 1e-12:
                    continue
                if prop == "x" and abs(delta - extra) < 1e-12:
                    target_value = value + extra
                elif prop == "width" and abs(delta - extra / 0.7) < 1e-12:
                    target_value = expected
                else:
                    raise ValueError("P2 graph differs beyond its source translation and number-button clearance")
                variant = {"id": identifier + ".p2." + prop, "target": identifier, "property": prop,
                    "source": "layout.presentation", "default": value,
                    "cases": [{"id": identifier + ".p2." + prop + ".c", "key": "p2", "value": target_value}]}
                result["variants"].append(variant)
                adapted.append({"node": identifier, "property": prop, "p1": value, "p2": expected})
            else:
                require(value == expected, "P2 graph changes an original non-geometric property")
    # Geometry validation above accounts for every source property. Remaining
    # declarations must be identical after normalising their layout marker.
    for field in ("bindings", "variants", "tracks", "stateMachines", "templates", "instances", "contract"):
        if field != "templates":
            require(normalized["p1"][field] == normalized["p2"][field], "P2 graph programs differ")
    originals = result["instances"]
    result["instances"] = []
    for layout in LAYOUTS:
        for instance in originals:
            copied = copy.deepcopy(instance)
            copied["id"] += "." + layout
            copied["material"]["resource"] = "markers/frame-" + layout + "-information"
            result["instances"].append(copied)
    for template in result["templates"]:
        identifier = template["root"]["id"]
        result["variants"].append({"id": identifier + ".p2.shift", "target": identifier, "property": "x",
            "source": "layout.presentation", "default": 0,
            "cases": [{"id": identifier + ".p2.shift.c", "key": "p2", "value": -shift}]})
    return result, {"sourceP1AndDpExact": True, "p2SourceTranslationPixels": -1502,
                    "p2NumberClearancePixels": 104, "verifiedPropertyDifferences": adapted,
                    "discriminator": "actual solved layout.presentation, never option.layout", "numericTolerance": 1e-12}


def add_sub_target(graph):
    result = copy.deepcopy(graph)
    nodes = {node["id"]: node for root in (result["root"], *(t["root"] for t in result["templates"])) for node in walk(root)}
    parents = {child["id"]: node for node in nodes.values() for child in node.get("children", [])}
    selectors = [item for item in result["variants"] if item["source"] == "option.graph-target"]
    require(len(selectors) == 6, "SUB TARGET requires six original primary branches")
    branches = {selector["cases"][0]["key"]: nodes[selector["target"]] for selector in selectors}
    owner = parents[branches["maximum"]["id"]]
    require(all(parents[branch["id"]] is owner for branch in branches.values()), "Primary graph targets must share one actual owner")
    outer = parents[owner["id"]]
    require(any(item["target"] == owner["id"] and item["source"] == "score.maximum-available" for item in result["bindings"]),
            "SUB TARGET must retain the real maximum availability gate")
    value_source = next(item for item in result["bindings"] if item["source"] == "score.maximum" and item["property"] == "text")
    no_data_properties = copy.deepcopy(nodes[value_source["target"]]["properties"])
    for field in ("bitmap-font", "format", "scale-x"):
        no_data_properties.pop(field, None)
    no_data_properties.update(text="NO DATA", **{"font-size": 18 * SCALE})
    serial = 0
    def no_data(parent, prefix):
        parent["children"].append(_node(prefix + ".text", "text", copy.deepcopy(no_data_properties)))
        for variant in graph["variants"]:
            if variant["target"] == value_source["target"] and variant["source"] == "layout.presentation" and variant["property"] in ("x", "width"):
                copied = copy.deepcopy(variant)
                copied["target"] = prefix + ".text"
                copied["id"] = prefix + ".p2." + variant["property"]
                for index, case in enumerate(copied["cases"]):
                    case["id"] = copied["id"] + ".c" + str(index)
                result["variants"].append(copied)
    missing_max = _gate(result, "gs.nomax", "score.maximum-available", ["false"])
    no_data(missing_max, "gs.nomax")
    outer["children"].append(missing_max)
    original_programs = {field: copy.deepcopy(result[field]) for field in ("bindings", "variants", "tracks")}
    for main_values, availability, key in ((["personal-best"], "score.personal-best-available", "pb"),
                                            (["aaa", "aa", "a"], "score.rank-available", "rank")):
        missing = _gate(result, "gs." + key + ".missing", availability, ["false"])
        main = _gate(result, "gs." + key, "option.graph-target", main_values, children=[missing])
        owner["children"].append(main)
        for target in TARGETS:
            if (key == "pb" and target == "personal-best") or (key == "rank" and target in ("aaa", "aa", "a")):
                continue  # The primary missing gate already proves these fallbacks unavailable.
            serial += 1
            original = branches[target]
            branch_nodes = list(walk(original))
            remap = {node["id"]: "gs.b" + str(serial) + ".n" + str(index) for index, node in enumerate(branch_nodes)}
            def rewrite(value):
                if isinstance(value, str):
                    return remap.get(value, value)
                if isinstance(value, list):
                    return [rewrite(item) for item in value]
                if isinstance(value, dict):
                    return {k: rewrite(v) for k, v in value.items()}
                return value
            cloned = rewrite(copy.deepcopy(original))
            removed = set()
            if target in ("maximum", "percentage"):
                available, = cloned["children"]
                removed.add(available["id"])
                cloned["children"] = available["children"]
            for node in walk(cloned):
                text = node["properties"].get("text")
                if node["type"] == "text" and text and text != "%":
                    node["properties"]["text"] = "SUB " + text
            missing["children"].append(cloned)
            for field in ("bindings", "variants", "tracks"):
                for program in original_programs[field]:
                    if program["target"] not in remap:
                        continue
                    copied = rewrite(copy.deepcopy(program))
                    if copied["target"] in removed:
                        continue
                    copied["id"] = "gs.b" + str(serial) + "." + field + str(len(result[field]))
                    if field == "variants":
                        if copied["source"] == "option.graph-target":
                            copied["source"] = "option.graph-sub-target"
                        for index, case in enumerate(copied["cases"]):
                            case["id"] = copied["id"] + ".c" + str(index)
                    elif field == "tracks":
                        for index, frame in enumerate(copied["keyframes"]):
                            frame["id"] = copied["id"] + ".k" + str(index)
                    result[field].append(copied)
        for sub_values, sub_availability, suffix in ((["none"], None, "none"),
                (["personal-best"], "score.personal-best-available", "pb"), (["aaa", "aa", "a"], "score.rank-available", "rank")):
            gate = _gate(result, "gs." + key + ".nodata." + suffix, "option.graph-sub-target", sub_values)
            missing["children"].append(gate)
            if sub_availability:
                child = _gate(result, gate["id"] + ".missing", sub_availability, ["false"])
                gate["children"].append(child)
                no_data(child, child["id"])
            else:
                no_data(gate, gate["id"])
    # The target column's empty frame is static. One shared outline is enough
    # for all mutually exclusive primary/fallback branches, and remains empty
    # beside NO DATA rather than implying an available target fill.
    primary_sprites = [node for node in walk(branches["maximum"]) if node["type"] == "sprite"]
    outline, = [node for node in primary_sprites if node["properties"].get("origin") != "bottom-left"]
    outline_ids = {node["id"] for node in walk(owner) if node["type"] == "sprite"
                   and node.get("resource") == outline.get("resource") and node["properties"] == outline["properties"]}
    require(not any(item["target"] in outline_ids for field in ("bindings", "variants", "tracks") for item in result[field]),
            "A target outline has live ownership and cannot be shared")
    for parent in list(walk(owner)):
        parent["children"] = [child for child in parent.get("children", []) if child["id"] not in outline_ids]
    common_outline = copy.deepcopy(outline)
    common_outline["id"] = "gs.target-outline"
    owner["children"].insert(0, common_outline)
    return compact(result, "gs.shared.")
