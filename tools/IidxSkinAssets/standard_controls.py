"""Bind IIDX26 source key lights to actual OMS lane input.

``build_controls(library, frame_map, style=26, layout="p1", contexts=...)``
returns manifest resources/animations, scene templates/instances/bindings/
variants/tracks, byte-preserving file copies, and material ``targets``. For each
target write ``[GameplaySkin.Common:1]``, ``Target: Lane {target}``, then
``playfield.key: resource Provide "{material}"``. The caller must replace its
empty key declaration, rather than append contradictory duplicate declarations.

Released caps already belong to the original cabinet movie. This contribution
contains only input lights: it never gates or redraws those caps. The source has
no per-key input hook or press/release clock. Raster anchors are identified in
the supplied 26 cabinet artwork, and retain that artwork's AFP transform. Only
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


def cabinet_keys(reference, style, layout, group):
    """Seven left-to-right caps with their own source draw, not a guessed key hook.

    03..19 store the caps in their full original frame raster. 21 and 26 store
    them in a panel raster. 25 supplies individual AFP key_w/key_b geometry.
    The following pixel anchors were inspected in these 26-source rasters;
    they are intentionally not a scale conversion from a Lightning canvas.
    """
    movie = "center_frame_dp_1p" if layout == "dp" else "2p_frame" if layout == "p2" else "1p_frame"
    right = group == 1 if layout == "dp" else layout == "p2"
    draws = reference["layouts"][movie]["draws"]
    lane = only([d for d in draws if f"lane_{'2p' if right else '1p'}" in d["game_bindings"]], "source lane")
    field = lane["bounds"]
    # SPADA's lane scales from its top while its key-area edge stays at 484;
    # the actual projected bottom, not the unstretched texture height, owns y.
    if field["right"] - field["left"] != 292 or lane["asset"]["height"] != 484 or field["bottom"] <= field["top"]:
        raise ValueError("Source lane needs a separately qualified control topology")
    if style == 25:
        candidates = [d for d in draws if d["texture"] in ("key_w", "key_b") and
                      field["left"] <= (d["bounds"]["left"] + d["bounds"]["right"]) / 2 < field["right"]]
        if len(candidates) != 7:
            raise ValueError("Source does not contain seven independent key-cap draws")
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
        elif style == 26:
            panels = [d for d in draws if d["texture"] == "conpane_key"]
            panels.sort(key=lambda d: d["bounds"]["left"])
            if len(panels) != (2 if layout == "dp" else 1):
                raise ValueError("Rootage source panel count differs")
            draw = panels[group] if layout == "dp" else panels[0]
            white, black = (6, 19, 28, 47), (42, 14, 20, 40)
        else:
            raise ValueError("Unqualified source style")
        keys = []
        for colour, (x, y, width, height), count in (("white", white, 4), ("black", black, 3)):
            for index in range(count):
                cap = [x + 64 * index, y, width, height]
                if cap[0] < 0 or cap[1] < 0 or cap[0] + width > draw["asset"]["width"] or cap[1] + height > draw["asset"]["height"]:
                    raise ValueError("Measured source cap lies outside its original raster")
                keys.append({"colour": colour, "draw": draw, "cap": cap, "anchorKind": "inspected-source-raster"})
        keys.sort(key=lambda key: project(key["draw"]["matrix"], key["cap"][0] + key["cap"][2] / 2, key["cap"][1])[0])
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
    library, frame_map = Path(library).resolve(strict=True), Path(frame_map).resolve(strict=True)
    reference, inventory = read(frame_map), read(library / "library.json")
    if style not in STYLES or layout not in ("p1", "p2", "dp"):
        raise ValueError("Unqualified Standard Model cabinet/layout")
    if inventory["status"] != "complete" or inventory["sourceMetadataUnchanged"] is not True:
        raise ValueError("Controls require the verified complete 26 library")
    if reference["package"] != f"gmframe{style:02}" or reference["common_family"] != "0" or reference["stage"] != {"width": 1280, "height": 720, "fps": 60.0}:
        raise ValueError("Control reference is not the qualified cabinet and common source family")
    index = {(r["package"].removeprefix("contents/data/graphic/").removesuffix(".ifs"), Path(r["name"]).stem): r
             for r in inventory["textures"]}
    output = frame_map.parent / "controls-v1" / layout
    result = {key: [] for key in ("resources", "animations", "templates", "instances", "bindings", "variants", "tracks", "targets", "files")}
    sources, checked, lights, resources = [], set(), {}, {}
    texturelist = source_path(library, "packages/contents/data/graphic/0/gameparts/raw/tex/texturelist.xml")
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
        row = verify_asset("0/gameparts", f"light_{colour}_key")
        if (row["width"], row["height"]) != size:
            raise ValueError("Native 26 key light dimensions differ; do not substitute HD art")
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
                prefix = f"standard.controls.f{style:02}.{layout}.g{group}.k{slot}"
                material = f"standard/markers/controls-f{style:02}-{layout}-g{group}-k{slot}"
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
    return result
