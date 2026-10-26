"""FAST/SLOW C2 artwork on closed, exact-group read-only timing directions.

No coordinates for Position A/B/C are guessed here. A separate reviewed capture
receipt must provide their offsets from the current judgement-row centre in
1920x1080 native pixels. P1/P2/DP evidence and authored adaptations stay explicit.
The existing judgement material target, surface, position tracks and brightness
remain the host; offset values and cumulative FAST/SLOW counts are never inputs.
"""
from __future__ import annotations

import copy
import hashlib
import io
import json
import math
from pathlib import Path

from PIL import Image

from lighting_gameplay_controls import SCALE, empty_scene, inside, require
from music_progress import _stable_id

MODULE = "timing-display"
POSITION_CONTRACT = "oms-iidx-timing-display-positions.v1"
POSITION_SPACE = "judgement-row-centre-offset-native-1920x1080"
SOURCE_CONTRACT = "oms-iidx-timing-display-art.v1"
LAYOUTS = ("p1", "p2", "dp")
POSITIONS = ("a", "b", "c")
CHOICES = ("off", "a", "a-separated", "b", "b-separated", "c", "c-separated")
DIRECTION_SOURCE = "judgement.timing-direction"
KEYS_SOURCE = "judgement.keys-timing-direction"
SCRATCH_SOURCE = "judgement.scratch-timing-direction"
DIRECTIONS = ("none", "fast", "slow")
PACKAGE = "contents/data/graphic/1/gameparts.ifs"
ALIAS_PACKAGE = "contents/data/graphic/0/gameparts.ifs"
ASSETS = {"fast": "td.f", "slow": "td.s", "s_fast": "td.sf", "s_slow": "td.ss"}
MATERIALS = {layout: "custom/markers/j/" + layout for layout in LAYOUTS}


def digest(data):
    return hashlib.sha256(data).hexdigest()


def module_definition():
    labels = {"off": "OFF（关闭游玩时 FAST/SLOW）"}
    for position in POSITIONS:
        title = "Position " + position.upper()
        labels[position] = title + "（合并显示判定快慢）"
        labels[position + "-separated"] = title + " · Separate Display（按键与转盘分开显示）"
    return {"id": MODULE, "label": "FAST/SLOW（游玩时判定快慢）", "rulesets": ["bms"],
            "keymodes": ["5k", "7k", "14k"], "default": "a",
            "choices": [{"value": choice, "label": labels[choice]} for choice in CHOICES]}


def reviewed_positions(document):
    """Use SP source anchors and explicit OMS C3 mirror rules; no extra approval gate."""
    require(isinstance(document, dict) and document.get("contract") == POSITION_CONTRACT
            and document.get("coordinateSpace") == POSITION_SPACE,
            "Supply Position A/B/C source anchors in the documented judgement-relative space")
    captures = document.get("captures")
    require(isinstance(captures, list) and captures and all(isinstance(item, dict) and isinstance(item.get("path"), str)
            and isinstance(item.get("sha256"), str) and len(item["sha256"]) == 64 for item in captures),
            "Position evidence needs hashed independent captures")
    positions = copy.deepcopy(document.get("positions"))
    require(isinstance(positions, dict) and "p1" in positions and set(positions).issubset(LAYOUTS),
            "Supply source P1 anchors; P2/DP use recorded OMS adaptations unless independently supplied")
    basis = copy.deepcopy(document.get("layoutBasis", {}))
    require(isinstance(basis, dict) and set(basis).issubset(LAYOUTS), "Invalid layout evidence basis")
    for layout in positions:
        basis.setdefault(layout, "source-reference")
        require(isinstance(positions[layout], dict) and set(positions[layout]) == set(POSITIONS), "Supply all three Position anchors for " + layout)
        for position in POSITIONS:
            channels = positions[layout][position]
            require(isinstance(channels, dict) and set(channels) == {"combined", "keys", "scratch"},
                    "Supply combined, keys and scratch offsets for every Position")
            for offset in channels.values():
                require(isinstance(offset, list) and len(offset) == 2 and all(type(value) in (int, float)
                    and math.isfinite(value) for value in offset), "Position offsets must be two finite native-pixel numbers")
                require(abs(offset[0]) <= 1920 and abs(offset[1]) <= 1080, "Position offsets exceed the native canvas")
    if "p2" not in positions:
        positions["p2"] = {position: {channel: [-offset[0], offset[1]] for channel, offset in channels.items()}
                           for position, channels in positions["p1"].items()}
        basis["p2"] = "oms-mirror-of-p1-on-exact-c3-judgement-host"
    if "dp" not in positions:
        positions["dp"] = copy.deepcopy(positions["p1"])
        basis["dp"] = "oms-p1-offset-on-exact-left-group-and-mirrored-right-group"
    result = copy.deepcopy(document)
    result.update({"positions": positions, "layoutBasis": basis,
        "adaptation": {"dpRightGroup": "Mirror X offsets for bms.group.deck-2; retain exact group target and existing host centre",
            "framesAndModes": "Use each current C3 judgement host width/height and existing option.judgement-position tracks",
            "nativeParity": "Menu previews establish placement relationships; native gameplay precision is claimed only where capture/source evidence states it"}})
    result.setdefault("scope", "SP source anchors; missing P2/DP anchors use explicit OMS C3 mirror adaptations, not native DP/5K observations")
    return result


def load_positions(path):
    path = Path(path).resolve(strict=True)
    require(path.drive.casefold() != "d:" and "private-data" not in {part.casefold() for part in path.parts},
            "Position receipts must come from independent captures, not an installation or player data")
    data = path.read_bytes()
    document = reviewed_positions(json.loads(data.decode("utf-8-sig")))
    for capture in document["captures"]:
        source = Path(capture["path"]).resolve(strict=True)
        require(source.drive.casefold() != "d:" and "private-data" not in {part.casefold() for part in source.parts},
                "Only independent capture files may supply position evidence")
        require(source.is_file() and digest(source.read_bytes()) == capture["sha256"], "A reviewed capture changed")
    return document, {"path": str(path), "sha256": digest(data)}


def source_reference(library):
    """Measure only the four original PNGs and their equivalent 0/1 aliases."""
    library = Path(library).resolve(strict=True)
    require(library.drive.casefold() != "d:" and "private-data" not in {part.casefold() for part in library.parts},
            "Use an independent source library")
    metadata_path = inside(library, "library.json")
    metadata_bytes = metadata_path.read_bytes()
    metadata = json.loads(metadata_bytes.decode("utf-8-sig"))
    require(metadata.get("status") == "complete" and metadata.get("sourceMetadataUnchanged") is True,
            "Use the complete verified source library")
    packages = {item["source"]: item for item in metadata["packages"]}
    selected, aliases, inputs = {}, {}, []
    for name in ASSETS:
        rows = [row for row in metadata["textures"] if row["package"] in (PACKAGE, ALIAS_PACKAGE) and row["name"] == name + ".png"]
        require(len(rows) == 2 and {row["package"] for row in rows} == {PACKAGE, ALIAS_PACKAGE},
                "Both known 0/1 source aliases must exist for " + name)
        measured = []
        for row in rows:
            require((row["width"], row["height"]) == (64, 22), "FAST/SLOW artwork must retain its original 64x22 pixels")
            sampler = row["sampler"]
            require(sampler["min"] == sampler["mag"] == "nearest" and sampler["wrapS"] == sampler["wrapT"] == "clamp",
                    "The original timing artwork needs its recorded nearest/clamp sampler")
            path = inside(library, row["sprite"])
            data = path.read_bytes()
            require(digest(data) == row["sha256"], "A source timing PNG changed")
            with Image.open(io.BytesIO(data)) as image:
                require(image.size == (64, 22), "Source timing PNG dimensions changed")
                rgba = digest(image.convert("RGBA").tobytes())
            require(rgba == row["rgbaSha256"], "A source timing PNG pixel receipt changed")
            item = {"name": name, "package": row["package"], "source": row["sprite"], "sha256": row["sha256"],
                    "rgbaSha256": rgba, "width": 64, "height": 22, "sampler": copy.deepcopy(sampler),
                    "imageRect": row.get("imageRect"), "uvRect": row.get("uvRect"),
                    "sourceArchiveSha256": packages[row["package"]]["sha256"]}
            inputs.append({"path": str(path), "sha256": item["sha256"]})
            measured.append(item)
            if row["package"] == PACKAGE:
                selected[name] = item
        require(len({item["rgbaSha256"] for item in measured}) == 1, "The 0/1 timing images are not pixel-equivalent aliases")
        aliases[name] = measured
    return {"contract": SOURCE_CONTRACT, "library": str(library), "librarySha256": digest(metadata_bytes),
            "selectedPackage": PACKAGE, "assets": selected, "equivalentAliases": aliases, "readInputs": inputs,
            "originalHddAccessed": False, "originalPixelsChanged": False, "fontsAdded": 0,
            "scope": "Original sprite identity/sampler only; source images do not establish A/B/C coordinates or timing semantics"}


def resource_assets(reference):
    require(reference.get("contract") == SOURCE_CONTRACT and set(reference["assets"]) == set(ASSETS),
            "Use the measured original timing artwork receipt")
    return {name: {"id": ASSETS[name], "type": "texture", "path": "art/td/" + item["sha256"][:16] + ".png", "filter": "nearest"}
            for name, item in reference["assets"].items()}


def verify_reference_unchanged(reference):
    library = Path(reference["library"])
    require(digest(inside(library, "library.json").read_bytes()) == reference["librarySha256"], "The source library metadata changed")
    require(all(digest(Path(item["path"]).read_bytes()) == item["sha256"] for item in reference["readInputs"]),
            "A source timing image changed during authoring")


def _node(identifier, kind="container", properties=None, *, resource=None, children=(), slot=None):
    result = {"id": identifier, "type": kind, "target": {"kind": "global"}, "blend": "inherit",
              "properties": properties or {}, "effects": [], "children": list(children)}
    if resource is not None:
        result["resource"] = resource
    if slot is not None:
        result["slot"] = slot
    return result


def judgement_geometry(scene, host_sizes):
    """Retain exact existing material owners and their two-layer position projection."""
    result = {}
    for layout, material in MATERIALS.items():
        instances = [item for item in scene["instances"] if item["material"] == {"slot": "hud.judgement", "resource": material}]
        require(len(instances) == 1, "Use the one existing judgement instance for " + layout)
        templates = [item for item in scene["templates"] if item["id"] == instances[0]["template"]]
        require(len(templates) == 1, "The existing judgement template is missing")
        root = templates[0]["root"]
        require(root.get("slot") == "hud.judgement" and len(root["children"]) == 1, "Use the reviewed judgement-position carrier")
        gate = root["children"][0]
        require(gate["properties"].get("width") == gate["properties"].get("height") == 1 and len(gate["children"]) == 2,
                "Use the unmasked same-sized judgement gate and both existing grade rows")
        rows = gate["children"]
        anchors = {(row["properties"]["x"], row["properties"]["y"]) for row in rows}
        require(len(anchors) == 1 and all(row["properties"].get("layout") == "horizontal"
                and row["properties"].get("origin") == "centre" for row in rows), "Existing grade rows must share their measured centre")
        tracks = []
        for target in (root["id"], gate["id"]):
            matches = [item for item in scene["tracks"] if item["target"] == target and item["property"] == "y"]
            require(len(matches) == 1 and matches[0].get("clock") == "option.judgement-position"
                    and matches[0].get("loop") is False and matches[0]["easing"] == "linear",
                    "Preserve the two existing judgement-position clocks")
            tracks.append(copy.deepcopy(matches[0]))
        require([(item["time"], item["value"]) for item in tracks[0]["keyframes"]]
                == [(item["time"], item["value"]) for item in tracks[1]["keyframes"]], "Position carriers no longer split the same displacement")
        brightness = [item for item in scene["bindings"] if item["target"] == root["id"] and item["property"] == "brightness"]
        require(len(brightness) == 1 and brightness[0]["source"] == "option.lane-objects-brightness",
                "Preserve the existing judgement brightness projection")
        width, height = host_sizes[layout]
        require(all(type(value) in (int, float) and math.isfinite(value) and value > 0 for value in (width, height)),
                "Use measured current Frame judgement-host dimensions")
        x, y = next(iter(anchors))
        result[layout] = {"material": material, "x": x, "y": y, "hostWidth": width, "hostHeight": height,
                          "positionTracks": tracks, "brightnessSource": brightness[0]["source"]}
    return result


def _show(scene, identifier, source, cases):
    scene["variants"].append({"id": identifier + ".v", "target": identifier, "property": "visible", "source": source,
        "default": False, "cases": [{"id": identifier + ".c" + str(index), "key": key, "value": True}
                                      for index, key in enumerate(cases)]})


def audit_identifiers(contribution):
    """Apply the existing public ASCII-segment/128-char validator before writing.

    Material resource selectors are package paths, not scene resource IDs. They
    remain untouched; texture IDs, node IDs and every declaration/reference are
    validated in their actual schema positions.
    """
    scene, manifest = contribution["scene"], contribution["manifest"]
    declarations, references = [], []
    def dictionaries(value):
        if isinstance(value, dict):
            yield value
            for child in value.values():
                yield from dictionaries(child)
        elif isinstance(value, list):
            for child in value:
                yield from dictionaries(child)
    for document in (scene, manifest):
        for item in dictionaries(document):
            if "id" in item:
                declarations.append(_stable_id(item["id"]))
    for item in dictionaries(scene):
        if isinstance(item.get("target"), str):
            references.append(_stable_id(item["target"]))
        for field in ("slot", "template", "initial", "from", "to", "animation", "bitmap-font"):
            if field in item:
                references.append(_stable_id(item[field]))
        if "type" in item and "resource" in item:
            references.append(_stable_id(item["resource"]))
    for variant in scene["variants"]:
        if variant["property"] == "resource":
            references.append(_stable_id(variant["default"]))
            references.extend(_stable_id(case["resource"]) for case in variant["cases"])
        if variant["source"] == "layout.group":
            references.extend(_stable_id(case["key"]) for case in variant["cases"])
    for font in manifest.get("bitmapFonts", []):
        references.extend(_stable_id(glyph["resource"]) for glyph in font["glyphs"])
    for animation in manifest.get("animations", []):
        references.extend(_stable_id(resource) for resource in animation["resources"])
    return {"declaredIds": len(declarations), "referenceIds": len(references),
            "rule": "GameplaySkinStableIdentityId.Validate and MAX_STABLE_ID_LENGTH=128; shared music_progress._stable_id mirror"}


def build_choice(choice, geometry, positions, assets):
    require(choice in CHOICES, "Unknown timing-display choice")
    scene = empty_scene("td.root")
    manifest = {"contract": "oms-gameplay-skin-manifest.v1", "scene": "gameplay-skin.scene.json",
                "sceneContract": "oms-gameplay-skin-scene.v1", "eventContract": "oms-gameplay-skin-event.v1", "resources": []}
    if choice == "off":
        contribution = {"scene": scene, "manifest": manifest}
        audit_identifiers(contribution)
        return contribution
    positions = reviewed_positions(positions)
    require(set(geometry) == set(LAYOUTS) and set(assets) == set(ASSETS), "Use complete existing judgement hosts and original artwork")
    separate = choice.endswith("-separated")
    position = choice[0]
    channels = (("keys", KEYS_SOURCE, "fast", "slow"), ("scratch", SCRATCH_SOURCE, "s_fast", "s_slow")) if separate else (
        ("combined", DIRECTION_SOURCE, "fast", "slow"),)
    used = {name for _, _, fast, slow in channels for name in (fast, slow)}
    manifest["resources"] = [copy.deepcopy(assets[name]) for name in ASSETS if name in used]
    for index, layout in enumerate(LAYOUTS):
        host = geometry[layout]
        require(host["material"] == MATERIALS[layout], "Timing feedback must retain the exact native judgement group material")
        root_id, gate_id = f"td.h{index}", f"td.g{index}"
        rows = []
        for channel, source, fast, slow in channels:
            prefix = f"td.h{index}.{channel}"
            dx, dy = positions["positions"][layout][position][channel]
            x, y = host["x"] + dx / host["hostWidth"], host["y"] + dy / host["hostHeight"]
            require(abs(x) <= 4 and abs(y) <= 4, "A reviewed timing row exceeds the unchanged scene position budget")
            sprite = _node(prefix + ".art", "sprite", {"height-px": 22 * SCALE, "visible": False}, resource=assets[fast]["id"])
            _show(scene, sprite["id"], source, ("fast", "slow"))
            scene["variants"].append({"id": prefix + ".r", "target": sprite["id"], "property": "resource", "source": source,
                "default": assets[fast]["id"], "cases": [{"id": prefix + ".slow", "key": "slow", "resource": assets[slow]["id"]}]})
            rows.append(_node(prefix, properties={"x": x, "y": y, "origin": "centre", "layout": "horizontal", "gap-px": 0}, children=[sprite]))
            if layout == "dp":
                mirrored_x = host["x"] - dx / host["hostWidth"]
                require(abs(mirrored_x) <= 4, "The mirrored exact DP group exceeds the unchanged scene position budget")
                scene["variants"].append({"id": prefix + ".group", "target": prefix, "property": "x", "source": "layout.group",
                    "default": x, "cases": [{"id": prefix + ".right", "key": "bms.group.deck-2", "value": mirrored_x}]})
        gate = _node(gate_id, properties={"width": 1, "height": 1}, children=rows)
        root = _node(root_id, properties={"width": 1, "height": 1, "visible": False}, slot="hud.judgement", children=[gate])
        _show(scene, root_id, "layout.ruleset", ("bms",))
        scene["templates"].append({"id": root_id + ".t", "root": root})
        scene["instances"].append({"id": root_id + ".i", "template": root_id + ".t",
            "material": {"slot": "hud.judgement", "resource": host["material"]}})
        scene["bindings"].append({"id": root_id + ".b", "target": root_id, "property": "brightness", "source": host["brightnessSource"]})
        for layer, (target, original) in enumerate(zip((root_id, gate_id), host["positionTracks"])):
            track = copy.deepcopy(original)
            track.update({"id": f"td.p{index}.l{layer}", "target": target})
            for key, frame in enumerate(track["keyframes"]):
                frame["id"] = track["id"] + ".k" + str(key)
            scene["tracks"].append(track)
    contribution = {"scene": scene, "manifest": manifest}
    audit_identifiers(contribution)
    return contribution


def host_sizes(profiles):
    """Use current C3 judgement bands; do not invent Frame/Lift layout geometry."""
    candidates = {layout: [] for layout in LAYOUTS}
    for profile in profiles:
        if profile["ruleset"] != "bms":
            continue
        layout = "dp" if profile["keymode"] == "14k" else profile["presentation"]
        require(layout in LAYOUTS, "A BMS profile has no reviewed P1/P2/DP judgement host")
        stages = profile["stages"]
        require(stages, "A current Frame profile has no stage")
        top = min(stage["rect"][1] for stage in stages)
        bottom = max(stage["rect"][1] + stage["rect"][3] for stage in stages)
        height = (bottom - top) * 1080 * .12
        require(0 < height < 1080 * .11, "The existing inactive native judgement safe-height contract changed")
        candidates[layout].extend((stage["rect"][2] * 1920, height) for stage in stages)
    result = {}
    for layout, sizes in candidates.items():
        require(sizes and all(abs(size[0] - sizes[0][0]) < .001 and abs(size[1] - sizes[0][1]) < .001 for size in sizes),
                "A current Frame needs separate judgement geometry by mode/group, not an inferred common size")
        result[layout] = sizes[0]
    return result
