"""Bind qualified Standard source key lights to actual OMS lane input.

``build_controls(library, frame_map, style=26, layout="p1", contexts=...)``
returns manifest resources/animations, scene templates/instances/bindings/
variants/tracks, byte-preserving file copies, and material ``targets``. For each
target write ``[GameplaySkin.Common:1]``, ``Target: Lane {target}``, then
``playfield.key: resource Provide "{material}"``. The caller must replace its
empty key declaration, rather than append contradictory duplicate declarations.

Released caps already belong to the original cabinet movie. This contribution
contains only input lights: it never gates or redraws those caps. The source has
no per-key input hook or press/release clock. Raster anchors are identified in
the supplied cabinet artwork, and retain that artwork's AFP transform. Only
the source frame's settled projection is used. All qualified panels have
constant transforms throughout their source loop, including SPADA's retained
small rotation. Preparation gates the separate input-light layer.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import struct
import xml.etree.ElementTree as ET
import zlib

STYLES = (3, 8, 12, 14, 16, 17, 19, 21, 25, 26)
from standard_source_catalog import (frame_token, layout_family, package_key, source_style,
                                     styles as source_styles, SOURCE21_STYLES, SOURCE22_STYLES)
from standard_lane_geometry import source_lane_regions
KEY_HEIGHT = 80  # The explicit OMS key-area host in build_standard_skin.py.
WIDTHS = (60, 34, 26, 34, 26, 34, 26, 34)
COLOURS = ("scratch", "white", "black", "white", "black", "white", "black", "white")


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def digest(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def source_path(root, relative):
    path = (root / relative).resolve(strict=True)
    if not path.is_relative_to(root) or not path.is_file():
        raise ValueError("Control source leaves its library")
    return path


def fields(target):
    result = {}
    for part in target.split():
        key, value = part.split("=", 1)
        if key in result:
            raise ValueError("Duplicate material target selector")
        result[key] = value
    return result


def source_slot(context, lane):
    """Map the public logical lane to one physical source slot, without mirroring input."""
    target = fields(lane["target"])
    dual = context["layout"] == "dp"
    right = context["group"] == 1 if dual else context["layout"] == "p2"
    mode = target["keymode"]
    count = len(context["lanes"])
    local = int(target["group-local-logical"])
    if not 0 <= local < count or target["ruleset"] != context["ruleset"]:
        raise ValueError("Lane target does not match its context")
    if context["ruleset"] == "bms":
        if mode not in ("5k", "7k", "14k") or dual != (mode == "14k") or count != (8 if dual else int(mode[:-1]) + 1):
            raise ValueError("Unsupported BMS key topology")
        slot = local if dual or not right else (7 if local == 0 else local - 1 + (2 if mode == "5k" else 0))
    elif context["ruleset"] == "mania":
        if mode not in ("5k", "6k", "7k", "8k", "7k-7k", "8k-8k"):
            raise ValueError("Unsupported mania key topology")
        expected = int(mode.split("-")[0][:-1])
        if count != expected or dual != ("-" in mode):
            raise ValueError("Mania stage vector does not match its context")
        slot = ((7 - count + local) if count in (5, 7) else (8 - count + local)) if right else (
            1 + local if count in (5, 7) else local)
    else:
        raise ValueError("Unsupported ruleset")
    colours = tuple(reversed(COLOURS)) if right else COLOURS
    if not 0 <= slot < 8 or colours[slot] != lane["colour"]:
        raise ValueError("Physical source key colour disagrees with the public material context")
    return slot, right


def project(matrix, x, y):
    return [matrix["a"] * x + matrix["c"] * y + matrix["tx"],
            matrix["b"] * x + matrix["d"] * y + matrix["ty"]]


def rectangle(x, y, width, height):
    return [[x, y], [x + width, y], [x + width, y + height], [x, y + height]]


def light_quad(key, light):
    """Centre the unchanged native glow dimensions on a measured source key cap."""
    x, y, width, height = key["cap"]
    return rectangle(x + width / 2 - light["width"] / 2,
                     y + height / 2 - light["height"] / 2,
                     light["width"], light["height"])


def quad_for_host(key, light, host):
    x, y, width, height = host
    points = [project(key["draw"]["matrix"], *point) for point in light_quad(key, light)]
    quad = [[(px - x) / width, (py - y) / height, 1] for px, py in points]
    if any(abs(value) > 4 for point in quad for value in point[:2]):
        raise ValueError("Source light does not fit the public projective-quad range")
    return {"vertices": quad, "opacity": 1}


def quad_track(identifier, target, quad):
    """The public scene-track contract uses named keyframes, unlike recorded clips."""
    return {"id": identifier, "type": "frame", "target": target, "property": "projective-quad",
            "easing": "step", "loop": False,
            "keyframes": [{"id": identifier + ".fixed", "time": 0, "value": quad}]}


def only(rows, reason):
    if len(rows) != 1:
        raise ValueError(f"Expected one {reason}, found {len(rows)}")
    return rows[0]


def _raster_keys(draw, white, black, *, black_positions=None):
    keys = []
    for colour, (x, y, width, height), count in (("white", white, 4), ("black", black, 3)):
        for index in range(count):
            cap_x = black_positions[index] if colour == "black" and black_positions else x + 64 * index
            cap = [cap_x, y, width, height]
            if cap_x < 0 or y < 0 or cap_x + width > draw["asset"]["width"] or y + height > draw["asset"]["height"]:
                raise ValueError("Measured source cap lies outside its original raster")
            keys.append({"colour": colour, "draw": draw, "cap": cap, "anchorKind": "inspected-source-raster"})
    keys.sort(key=lambda key: project(key["draw"]["matrix"], key["cap"][0] + key["cap"][2] / 2, key["cap"][1])[0])
    return keys


def _source22_keys(draws, style, layout, group, right):
    """Measured caps in each retained 22 source, with its actual AFP projection.

    PRESENT/FUTURE use a mirrored 1P panel on the right. Its black painted cap
    differs from the retained SPADA panel by one pixel and must not inherit the
    ARC SCORE/other-source cap rectangle merely because its dimensions match.
    """
    package = package_key(style)
    if style in ("source22-gmframe0-00", "source22-gmframe0-01"):
        panels = sorted((d for d in draws if d["texture"] == "sp_key_1p"), key=lambda d: d["bounds"]["left"])
        if len(panels) != (2 if layout == "dp" else 1):
            raise ValueError("22 PRESENT/FUTURE must retain its original mirrored key panels")
        draw, size = panels[group] if layout == "dp" else panels[0], (322, 86)
        white, black = (91, 12, 28, 47), (127, 8, 20, 40)
    elif style in ("source22-gmframe0", "source22-gmframe10"):
        name = "sp_key_" + ("2p" if right else "1p")
        draw, size = only([d for d in draws if d["texture"] == name], "22 retained SPADA key panel"), (322, 86)
        white = (10 if right else 91, 12, 28, 47)
        black = (46, 7, 20, 40) if right else (128, 7, 18, 40)
    elif style == "source22-gmframe8":
        name = "dp_frame" if layout == "dp" else "sp_frame_" + ("2p" if right else "1p")
        draw = only([d for d in draws if d["texture"] == name], "22 LINCLE frame raster")
        size = (1280, 720) if layout == "dp" else (356, 720)
        x = (689 if right else 371) if layout == "dp" else (21 if right else 115)
        white, black = (x, 494, 28, 47), (x + 36, 489, 20, 40)
    elif style == "source22-gmframe9":
        name = "frame_lane_" + ("2p" if right else "1p")
        draw, size = only([d for d in draws if d["texture"] == name], "22 TRICORO lane panel"), (354, 609)
        x = 10 if right else 124
        white, black = (x, 494, 28, 47), (x + 36, 489, 20, 40)
    else:
        raise ValueError("Unqualified 22 key source")
    vertices = [{"x": x, "y": y, "z": 0} for x, y in
                ((0, 0), (size[0], 0), (0, size[1]), size)]
    if (draw["asset"]["package"] != package or (draw["asset"]["width"], draw["asset"]["height"]) != size
            or draw["source_vertices"] != vertices):
        raise ValueError("22 key panel identity, dimensions or original GEO changed")
    return _raster_keys(draw, white, black)


def _source21_keys(draws, style, right):
    """Own SPADA21 panel and GEO, including its retained small rotation."""
    name = "sp_key_" + ("2p" if right else "1p")
    draw = only([d for d in draws if d["texture"] == name], "21 original SPADA key panel")
    vertices = [{"x": x, "y": y, "z": 0} for x, y in ((0, 0), (322, 0), (0, 86), (322, 86))]
    if (draw["asset"]["package"] != package_key(style)
            or (draw["asset"]["width"], draw["asset"]["height"]) != (322, 86)
            or draw["source_vertices"] != vertices):
        raise ValueError("21 key panel identity, dimensions or original GEO changed")
    return _raster_keys(draw, (10 if right else 91, 12, 28, 47),
                        (46, 7, 20, 40) if right else (128, 7, 18, 40))


def cabinet_keys(reference, style, layout, group):
    """Seven left-to-right caps with their own source draw, not a guessed key hook.

    03..19 store the caps in their full original frame raster. 21, 23 and 26
    store them in a panel raster. 24 and 25 supply independent AFP key_w/key_b
    geometry. Raster anchors are inspected in each qualified source image;
    they are not a scale conversion from another cabinet or source family.
    """
    style = source_style(style)
    movie = "center_frame_dp_1p" if layout == "dp" else "2p_frame" if layout == "p2" else "1p_frame"
    right = group == 1 if layout == "dp" else layout == "p2"
    draws = reference["layouts"][movie]["draws"]
    regions = source_lane_regions(reference, style, layout)
    if not 0 <= group < len(regions):
        raise ValueError("Control group does not belong to this source layout")
    field = regions[group]
    if style in SOURCE21_STYLES:
        keys = _source21_keys(draws, style, right)
    elif style in SOURCE22_STYLES:
        keys = _source22_keys(draws, style, layout, group, right)
    elif style in (24, 25):
        candidates = [d for d in draws if d["texture"] in ("key_w", "key_b") and
                      field["left"] <= (d["bounds"]["left"] + d["bounds"]["right"]) / 2 < field["right"]]
        if len(candidates) != 7:
            raise ValueError("Source does not contain seven independent key-cap draws")
        if style == 24 and any(d["asset"]["package"] != "gmframe24" or
                               (d["asset"]["width"], d["asset"]["height"]) !=
                               ((34, 54) if d["texture"] == "key_w" else (26, 46)) for d in candidates):
            raise ValueError("SINOBUZ keys must retain their original local source geometry")
        keys = [{"colour": "white" if d["texture"] == "key_w" else "black", "draw": d,
                 "cap": [0, 0, d["asset"]["width"], d["asset"]["height"]], "anchorKind": "original-key-mesh"}
                for d in sorted(candidates, key=lambda d: d["bounds"]["left"])]
    else:
        if style < 21:
            name = f"main{style:02}_dp" if layout == "dp" else f"main{style:02}_sp_{'2p' if right else '1p'}"
            draw = only([d for d in draws if d["texture"] == name], "original frame raster")
            x = (689 if right else 371) if layout == "dp" else (21 if right and style == 19 else 115)
            white, black = (x, 494, 28, 47), (x + 36, 489, 20, 40)
        elif style == 21:
            name = f"sp_key_{'2p' if right else '1p'}"
            draw = only([d for d in draws if d["texture"] == name], "SPADA key panel")
            # The black cap is 18 px in 1P art and 20 px in 2P art. Native
            # 42 px light width is retained; it is centred, never stretched.
            white = (10 if right else 91, 12, 28, 47)
            black = (46, 7, 20, 40) if right else (128, 7, 18, 40)
        elif style == 23:
            panels = [d for d in draws if d["texture"] == "keyboard"]
            panels.sort(key=lambda d: d["bounds"]["left"])
            if len(panels) != (2 if layout == "dp" else 1):
                raise ValueError("copula source keyboard panel count differs")
            draw = panels[group] if layout == "dp" else panels[0]
            if (draw["asset"]["package"] != "gmframe23" or
                    (draw["asset"]["width"], draw["asset"]["height"]) != (234, 68)):
                raise ValueError("copula keys require their original keyboard raster")
            # Original painted caps inside the baked keyboard raster, measured
            # against its dark surround. The black cap is genuinely 19px wide.
            white, black = (7, 10, 28, 47), (44, 6, 19, 40)
        elif style == 26:
            panels = [d for d in draws if d["texture"] == "conpane_key"]
            panels.sort(key=lambda d: d["bounds"]["left"])
            if len(panels) != (2 if layout == "dp" else 1):
                raise ValueError("Rootage source panel count differs")
            draw = panels[group] if layout == "dp" else panels[0]
            white, black = (6, 19, 28, 47), (42, 14, 20, 40)
        else:
            raise ValueError("Unqualified source style")
        # copula's first black cap is one pixel farther right than a uniform
        # 64px repetition; retain each measured anchor.
        keys = _raster_keys(draw, white, black, black_positions=(44, 107, 171) if style == 23 else None)
    if [key["colour"] for key in keys] != ["white", "black", "white", "black", "white", "black", "white"]:
        raise ValueError("Source left-to-right key order differs")
    return movie, field, keys


def store_bytes(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists():
        if path.read_bytes() != data:
            raise ValueError(f"Existing controls evidence differs: {path}")
    else:
        path.write_bytes(data)
    return hashlib.sha256(data).hexdigest()


def transparent_png():
    def chunk(name, payload):
        return struct.pack(">I", len(payload)) + name + payload + struct.pack(">I", zlib.crc32(name + payload))
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", 1, 1, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(b"\0\0\0\0\0")) + chunk(b"IEND", b"")


def node(identifier, kind="container", **extra):
    return {"id": identifier, "type": kind, "target": {"kind": "global"}, "blend": "inherit",
            "properties": {"x": 0, "y": 0, "width": 1, "height": 1}, "effects": [], "children": [], **extra}


def build_controls(library, frame_map, *, style, layout, contexts):
    style = source_style(style)
    library, frame_map = Path(library).resolve(strict=True), Path(frame_map).resolve(strict=True)
    reference, inventory = read(frame_map), read(library / "library.json")
    if style not in source_styles(inventory) or layout not in ("p1", "p2", "dp"):
        raise ValueError("Unqualified Standard Model cabinet/layout")
    if inventory["status"] != "complete" or inventory["sourceMetadataUnchanged"] is not True:
        raise ValueError("Controls require the verified complete Standard library")
    family = layout_family(style)
    if reference["package"] != package_key(style) or reference["common_family"] != family or reference["stage"] != {"width": 1280, "height": 720, "fps": 60.0}:
        raise ValueError("Control reference is not the qualified cabinet and common source family")
    index = {(r["package"].removeprefix("contents/data/graphic/").removesuffix(".ifs"), Path(r["name"]).stem): r
             for r in inventory["textures"]}
    output = frame_map.parent / "controls-v1" / layout
    result = {key: [] for key in ("resources", "animations", "templates", "instances", "bindings", "variants", "tracks", "targets", "files")}
    sources, checked, lights, resources = [], set(), {}, {}
    texturelist = source_path(library, f"packages/contents/data/graphic/{family}/gameparts/raw/tex/texturelist.xml")
    samplers = {image.attrib["name"]: texture.attrib for texture in ET.parse(texturelist).getroot().findall("texture")
                for image in texture.findall("image")}
    sources.append({"path": texturelist.relative_to(library).as_posix(), "sha256": digest(texturelist), "role": "original light sampler"})

    def verify_asset(package, name):
        row = index[package, name]
        key = package, name
        if key not in checked:
            path = source_path(library, row["sprite"])
            if digest(path) != row["sha256"]:
                raise ValueError("Original control source PNG changed")
            checked.add(key)
            sources.append({"package": package, "name": name, "path": row["sprite"], "sha256": row["sha256"]})
        return row

    for colour, size in (("white", (50, 69)), ("black", (42, 62))):
        row = verify_asset(f"{family}/gameparts", f"light_{colour}_key")
        if (row["width"], row["height"]) != size:
            raise ValueError("Qualified source key light dimensions differ; do not substitute another source family")
        sampler = samplers[f"light_{colour}_key"]
        if (sampler["min_filter"] != sampler["mag_filter"] or sampler["min_filter"] not in ("nearest", "linear") or
                sampler["wrap_s"] != "clamp" or sampler["wrap_t"] != "clamp"):
            raise ValueError("Original control light requires an unsupported sampler")
        lights[colour] = row
        resources[colour] = "standard.controls.t.t" + row["sha256"][:24]
        path = f"standard/art/{row['sha256']}.png"
        result["resources"].append({"id": resources[colour], "type": "texture", "path": path, "filter": sampler["min_filter"]})
        result["files"].append({"source": str(source_path(library, row["sprite"])), "path": path, "sha256": row["sha256"]})
    empty = output / "empty.png"
    empty_sha = store_bytes(empty, transparent_png())
    authored, records = {}, []
    for context in contexts:
        if context["layout"] != layout:
            raise ValueError("Filter material_contexts() by this layout before building controls")
        group = context["group"]
        movie, field, keys = cabinet_keys(reference, style, layout, group)
        for lane in context["lanes"]:
            slot, right = source_slot(context, lane)
            identity = group, slot
            if identity not in authored:
                prefix = f"standard.controls.f{frame_token(style)}.{layout}.g{group}.k{slot}"
                material = f"standard/markers/controls-f{frame_token(style)}-{layout}-g{group}-k{slot}"
                authored[identity] = material
                root = node(prefix + ".root", slot="playfield.key")
                result["templates"].append({"id": prefix + ".template", "root": root})
                result["instances"].append({"id": prefix + ".instance", "template": prefix + ".template",
                                            "material": {"slot": "playfield.key", "resource": material}})
                result["files"].append({"source": str(empty), "path": material + ".png", "sha256": empty_sha})
                widths = tuple(reversed(WIDTHS)) if right else WIDTHS
                host = [field["left"] + 2 + sum(width + 2 for width in widths[:slot]), field["bottom"], widths[slot], KEY_HEIGHT]
                if lane["colour"] == "scratch":
                    records.append({"group": group, "sourceSlot": slot, "kind": "scratch", "host": host,
                                    "visualOwner": "original cabinet + independent Turntable module; no rectangular key glow"})
                else:
                    key = keys[slot if right else slot - 1]
                    light, resource = lights[key["colour"]], resources[key["colour"]]
                    asset = key["draw"]["asset"]
                    row = verify_asset(asset["package"], asset["name"])
                    if row["sha256"] != asset["sha256"]:
                        raise ValueError("Frame reference and original key raster differ")
                    pressed = node(prefix + ".input")
                    pressed["properties"]["visible"] = False
                    sprite = node(prefix + ".steady", "projective-sprite", resource=resource, blend="additive")
                    pressed["children"].append(sprite)
                    root["children"].append(pressed)
                    quad = quad_for_host(key, light, host)
                    result["tracks"].append(quad_track(prefix + ".quad", sprite["id"], quad))
                    result["bindings"].append({"id": prefix + ".pressed", "target": pressed["id"], "property": "visible", "source": "input.pressed"})
                    result["variants"].append({"id": prefix + ".preparing", "target": root["id"], "property": "visible",
                        "source": "preparation.active", "default": True,
                        "cases": [{"id": prefix + ".preparing.hide", "key": "true", "value": False}]})
                    record = {"group": group, "sourceSlot": slot, "kind": key["colour"], "host": host,
                              "anchorKind": key["anchorKind"], "sourceRaster": asset["sprite"], "sourceCap": key["cap"],
                              "sourceGeometry": key["draw"]["geometry"], "sourceMatrix": key["draw"]["matrix"],
                              "sourceLightSize": [light["width"], light["height"]], "projectedLightQuad": quad["vertices"]}
                    record["sourceMovie"] = movie
                    records.append(record)
            result["targets"].append({"target": lane["target"], "slot": "playfield.key", "material": authored[identity],
                                      "sourceSlot": slot, "colour": lane["colour"]})
    if not authored:
        raise ValueError("No actual material contexts were supplied")
    result["receipt"] = {"contract": "oms-standard-controls.v1", "style": style, "layout": layout,
        "commonSourceFamily": family,
        "inputSource": "input.pressed", "separatePreparationGate": "preparation.active=false", "pngBytesPreserved": True,
        "releasedCapsOwner": "unmodified source cabinet movie",
        "pressReleaseSourceClock": None, "sourceFiles": sources, "controls": records,
        "animations": len(result["animations"]), "templates": len(result["templates"]), "targets": len(result["targets"]),
        "limits": ["Source AFP provides no per-key input hook/press-release timeline; OMS input directly gates the native light PNG.",
                   "Additive light blending and raster-cap centring are the explicit OMS mapping, pending original-game input comparison.",
                   "All qualified key panels have constant matrices throughout the original loop interval. Exact projected sprites retain the source rotation/mirroring and full glow outside the key rectangle.",
                   "The cabinet owns its full entry animation; input lights remain hidden during actual preparation and use the source reference-frame projection during gameplay.",
                   "Scratch has no rectangular source key light: its cabinet and Turntable module remain the visual owners; this module adds no invented scratch art.",
                   "Material target and source export checks do not sign public CLI budgets or real Player input/visual acceptance."]}
    if style in (23, 24):
        result["receipt"]["limits"].append(
            "Earlier controls use their own s23/s24 gameparts lights and original cabinet cap transforms. "
            "The shared OMS 484px lane-plane convention is an explicit mapping, not a claim that the 24 native note scissor is 484px.")
    if style in SOURCE22_STYLES:
        result["receipt"]["limits"].append(
            "22 controls consume only s22/gameparts and their selected source22 cabinet. PRESENT/FUTURE's right panel uses its original mirrored 1P raster. "
            "The fixed 484px OMS component plane is an explicit adaptation; the evidenced 22 native note scissor is 482px. "
            "Baked LINCLE/TRICORO lane art remains in its original cabinet raster, independently of the logical note plane.")
    if style in SOURCE21_STYLES:
        result["receipt"]["limits"].append(
            "21 controls consume only s21/gameparts and the source21/gmframe0 panel, preserving its measured caps and original affine rotation. "
            "The 484px OMS component plane follows its own original lane GEO; the separately evidenced482px cover clip does not establish native note-coordinate parity. "
            "Input-gated additive glows remain an explicit OMS adaptation, not a native21 press/release timeline.")
    if style == 23:
        result["receipt"]["limits"].append(
            "copula kenban_light_big/mini are retained in the cabinet entry movie (before its loop); "
            "they are not repurposed as input-triggered lights.")
    return result
