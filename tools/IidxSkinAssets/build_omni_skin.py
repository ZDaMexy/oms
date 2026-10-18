#!/usr/bin/env python3
"""Build one local Lightning-era IIDX study, with selected frame/material contributions.

Inputs are independent, verified author libraries. This tool never opens a game installation.
Original media and its generated author package remain local, outside the OMS distribution.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import uuid
import zipfile

import brotli
from PIL import Image

from choice_previews import (MODULES as PREVIEW_MODULES, audit_previews, build_choice_previews,
                             preview_files, preview_path)

NAME = "Lighting Model Skin Omni Until 33"
MANIFEST_CONTRACT = "oms-gameplay-skin-manifest.v1"
SCENE_CONTRACT = "oms-gameplay-skin-scene.v1"
EVENT_CONTRACT = "oms-gameplay-skin-event.v1"
SCENE_HEADER_FILE = "gameplay-skin.scene.json"
FRAME_NAMES = {
    "iidx27": "27 HEROIC VERSE", "iidx28": "28 BISTROVER", "iidx29": "29 CastHour",
    "iidx30": "30 RESIDENT", "iidx31": "31 EPOLIS", "iidx32": "32 Pinky Crush", "iidx33": "33 Sparkle Shower",
    "bpl-apina": "BPL APINA VRAMeS", "bpl-gamepanic": "BPL GAME PANIC", "bpl-gigo": "BPL GiGO",
    "bpl-leisureland": "BPL LEISURE LAND", "bpl-round1": "BPL ROUND1", "bpl-silkhat": "BPL SILK HAT",
    "bpl-supernova-hd": "BPL SUPERNOVA Tohoku", "bpl-tradz": "BPL TAITO STATION Tradz",
}
SUPPORTED = ["5k", "6k", "7k", "8k", "14k", "7k-7k", "8k-8k"]
ROOT_GLOBAL_TARGET = "Target: Global ruleset=any keymode=any stage-mode=any"


def read_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")


def exact_file(root: Path, relative: str) -> Path:
    result = (root / relative).resolve(strict=True)
    if not result.is_relative_to(root):
        raise ValueError(f"Asset path escapes its independent library: {relative}")
    return result


def copy_exact(source: Path, destination: Path):
    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.exists():
        if source.read_bytes() != destination.read_bytes():
            raise ValueError(f"Conflicting author asset: {destination}")
    else:
        shutil.copyfile(source, destination)


def neutral(identifier: str):
    return {"id": identifier, "type": "container", "target": {"kind": "global"}, "blend": "inherit", "properties": {}, "effects": [], "children": []}


def scene_document(identifier: str):
    return {"contract": SCENE_CONTRACT, "root": neutral(identifier), "tracks": [], "stateMachines": [],
            "bindings": [], "variants": [], "templates": [], "instances": []}


def manifest_document(resources=None):
    return {"contract": MANIFEST_CONTRACT, "scene": SCENE_HEADER_FILE, "sceneContract": SCENE_CONTRACT,
            "eventContract": EVENT_CONTRACT, "resources": resources or []}


def walk(node):
    yield node
    for child in node.get("children", []):
        yield from walk(child)


def resource_references(document, fonts=()):
    roots = [document["root"], *(template["root"] for template in document.get("templates", []))]
    referenced = {node["resource"] for root in roots for node in walk(root) if node.get("resource")}
    referenced.update(glyph["resource"] for font in fonts for glyph in font["glyphs"])
    for program in [*document.get("tracks", []), *document.get("variants", [])]:
        if program.get("property") != "resource":
            continue
        referenced.update(case["resource"] for case in program.get("cases", []))
        if program.get("default"):
            referenced.add(program["default"])
        referenced.update(key["value"] for key in program.get("keyframes", []))
    return referenced


def prepare_graph(library: Path, output: Path):
    """Keep the actual graph font independent of a selected main-frame font."""
    rows = read_json(library / "library.json")["textures"]
    resources, glyphs, sources = [], [], []
    for digit in range(10):
        matches = [row for row in rows if row["package"] == "contents/data/graphic/1/graph.ifs"
                   and row["name"] == f"gr_sco_{digit:02}.png"]
        if len(matches) != 1:
            raise ValueError("The original graph digit must have one exact source")
        row = matches[0]
        source = exact_file(library, row["sprite"])
        if hashlib.sha256(source.read_bytes()).hexdigest() != row["sha256"]:
            raise ValueError("The verified graph digit changed")
        if (row["width"], row["height"]) != (36, 24) or any(row["sampler"][key] != "nearest" for key in ("mag", "min")):
            raise ValueError("The graph source no longer matches its measured font contract")
        identifier, relative = f"omni.graph.d{digit}", f"art/graph/gr_sco_{digit:02}.png"
        copy_exact(source, output / relative)
        resources.append({"id": identifier, "type": "texture", "path": relative, "filter": "nearest"})
        glyphs.append({"character": str(digit), "resource": identifier, "advance": 40})
        sources.append({"package": row["package"], "sprite": row["sprite"], "sha256": row["sha256"]})
    pixel = "art/graph/author-comparison-white.png"
    (output / pixel).parent.mkdir(parents=True, exist_ok=True)
    Image.new("RGBA", (1, 1), (255, 255, 255, 255)).save(output / pixel)
    resources.append({"id": "omni.graph.author-comparison", "type": "texture", "path": pixel, "filter": "nearest"})
    return {"font": {"id": "omni.graph.score", "lineHeight": 24, "glyphs": glyphs}, "resources": resources,
            "receipt": {"sourceDigits": sources, "lineHeight": 24, "advance": 40,
                        "currentSourceHook": {"p1": [1747, 50, 1903, 74], "p2": [245, 50, 401, 74]},
                        "maximumRowAndComparison": "author adaptation; no original rival, target, or score-to-bar rule asserted",
                        "visibility": "graph-visible (default false); maximum and progress additionally require score.maximum-available"}}


def graph_readout(identifier, layout, *, readable=True):
    group = neutral(identifier)
    group["properties"] = {"visible": False, "z": -30 if readable else -1}
    if readable:
        group["slot"] = "decoration"
    shift = -1502 if layout == "p2" else 0
    offset = 128 if readable else 0
    bindings = []

    def box(name, x, y, width, height, **properties):
        item = neutral(identifier + "." + name)
        item["properties"] = {"x": (x + shift) / 1920, "y": y / 1080,
                              "width": width / 1920, "height": height / 1080, **properties}
        return item

    def text(name, caption, x, y, width, height, font_size):
        item = box(name, x, y, width, height, text=caption, **{"font-size": font_size * 768 / 1080,
                   "alignment": "right", "colour": "#ffffffff"})
        item["type"] = "text"
        return item

    def bind(target, prop, source):
        bindings.append({"id": target + ".bind." + prop, "target": target, "property": prop, "source": source})

    # The 4 source slots begin at 1747 and advance by 40, with the final 36px glyph ending at 1903.
    # A wider box right-aligns longer real OMS values without synthesising leading digits.
    current = text("current", "", 1523, 50 + offset, 380, 24, 24)
    current["properties"].update({"bitmap-font": "omni.graph.score", "format": "integer"})
    current_label = text("current-label", "CURRENT", 1510, 30 + offset, 393, 16, 12)
    maximum_group = neutral(identifier + ".available")
    maximum_group["properties"] = {"visible": False}
    maximum = text("maximum", "", 1523, 98 + offset, 380, 24, 24)
    maximum["properties"].update({"bitmap-font": "omni.graph.score", "format": "integer"})
    maximum_label = text("maximum-label", "MAXIMUM", 1510, 78 + offset, 393, 16, 12)
    track = box("comparison-track", 1747, 138 + offset, 156, 8, colour="#303030ff")
    fill = box("comparison", 1747, 138 + offset, 156, 8, colour="#50bfffff", **{"scale-x": 0})
    for item in (track, fill):
        item.update({"type": "sprite", "resource": "omni.graph.author-comparison"})
    maximum_group["children"] = [maximum_label, maximum, track, fill]
    group["children"] = [current_label, current, maximum_group]
    if readable:
        # Framework Depth is larger behind. The independent root is in front of Graph Art (-20),
        # while this local depth keeps the plate behind its labels and original bitmap digits (0).
        backplate = box("backplate", 1504, 150, 414, 134, colour="#000000c0", z=1)
        backplate.update({"type": "sprite", "resource": "omni.graph.author-comparison"})
        group["children"].insert(0, backplate)
    bind(identifier, "visible", "option.graph-visible")
    bind(current["id"], "text", "score.value")
    bind(maximum["id"], "text", "score.maximum")
    bind(maximum_group["id"], "visible", "score.maximum-available")
    bind(fill["id"], "scale-x", "score.progress")
    return group, bindings


def add_graph(document, information, layout):
    group, bindings = graph_readout(information["id"] + ".graph", layout)
    document["bindings"].extend(bindings)
    return group


def graph_readout_receipt():
    return {"presentation": "author adaptation below OMS play buttons; not original host placement",
            "canvas": [1920, 1080], "sourceHookVerticalOffset": 128,
            "currentY": 178, "currentLabelY": 158, "maximumY": 226, "maximumLabelY": 206,
            "comparisonBarY": 266, "backplateBoundsP1Dp": [1504, 150, 1918, 284], "p2HorizontalShift": -1502,
            "backplateColour": "#000000c0", "reusedResource": "omni.graph.author-comparison",
            "ordering": "independent selected-information-marker template Depth=-30, in front of Graph Art Depth=-20; local plate Depth=1 behind labels Depth=0",
            "visibility": "same graph-visible whole-group binding; default false unchanged"}


def atlas_fonts(manifest, source: Path, output: Path, prefix: str, resource_prefix: str):
    """Lossless packing of glyph pixels; retain original advances, line heights, alpha and sampling."""
    fonts = manifest.get("bitmapFonts", [])
    if not fonts:
        return {"glyphs": 0}
    resources = {item["id"]: item for item in manifest["resources"]}
    glyph_resources = sorted({glyph["resource"] for font in fonts for glyph in font["glyphs"]})
    filters = sorted({resources[identifier].get("filter", "linear") for identifier in glyph_resources})
    placements, atlases = {}, []
    total_pixels = 0
    for filtering in filters:
        group = [identifier for identifier in glyph_resources if resources[identifier].get("filter", "linear") == filtering]
        images, rectangles = {}, {}
        cursor_x = cursor_y = row_height = 0
        atlas_width = 1024
        for identifier in group:
            with Image.open(exact_file(source, resources[identifier]["path"])) as original:
                image = original.convert("RGBA")
            if image.width + 4 > atlas_width or image.height > 512:
                raise ValueError("The source glyph does not fit the bounded atlas")
            if cursor_x + image.width + 4 > atlas_width:
                cursor_x, cursor_y, row_height = 0, cursor_y + row_height, 0
            rectangles[identifier] = [cursor_x + 2, cursor_y + 2, image.width, image.height]
            images[identifier] = image
            cursor_x += image.width + 4
            row_height = max(row_height, image.height + 4)
        atlas_height = cursor_y + row_height
        if atlas_height > 4096:
            raise ValueError("The source glyph atlas exceeds the image budget")
        atlas = Image.new("RGBA", (atlas_width, atlas_height))
        for identifier, image in images.items():
            x, y, width, height = rectangles[identifier]
            atlas.paste(image, (x, y))
            if atlas.crop((x, y, x + width, y + height)).tobytes() != image.tobytes():
                raise AssertionError("Glyph pixel preservation failed")
        relative = prefix + "font/glyphs-" + filtering + ".png"
        target = output / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        atlas.save(target)
        atlas_id = resource_prefix + ".glyph-atlas-" + filtering
        placements.update({identifier: (atlas_id, rectangle) for identifier, rectangle in rectangles.items()})
        atlases.append({"id": atlas_id, "type": "texture", "path": relative, "filter": filtering})
        total_pixels += atlas_width * atlas_height
    for font in fonts:
        for glyph in font["glyphs"]:
            glyph["resource"], glyph["rect"] = placements[glyph["resource"]]
    manifest["resources"] = [resource for resource in manifest["resources"] if resource["id"] not in placements]
    manifest["resources"].extend(atlases)
    return {"glyphs": len(glyph_resources), "atlases": len(atlases), "atlasPixels": total_pixels, "pixelEquality": True}


def build_frame(frame: str, source: Path, output: Path, graph):
    original_manifest = read_json(source / "gameplay-skin.json")
    original_scene = read_json(source / "gameplay-skin.scene.json")
    prefix = f"gameplay/frames/{frame}/"
    ini_path = prefix + "materials.ini"
    document = scene_document(f"omni.frame.{frame}.root")
    retained, graph_instances = [], []
    for template in original_scene["templates"]:
        identifier = template["id"]
        if ".template.key." not in identifier and not re.search(r"\.template\.(p1|p2|dp)\.(background|information|gauge)$", identifier):
            continue
        template = copy.deepcopy(template)

        def prune(node):
            node["children"] = [prune(child) for child in node.get("children", [])
                                if not child["id"].startswith("f.") and ".fixed-turn." not in child["id"]]
            return node

        prune(template["root"])
        match = re.search(r"\.template\.(p1|p2|dp)\.information$", identifier)
        if match:
            layout = match.group(1)
            group = add_graph(document, template["root"], layout)
            graph_template = identifier + ".graph"
            retained.append({"id": graph_template, "root": group})
            graph_instances.append({"id": group["id"] + ".instance", "template": graph_template,
                                    "material": {"slot": "decoration", "resource": f"markers/{layout}-information"}})
        retained.append(template)
    document["templates"] = retained
    template_ids = {template["id"] for template in retained}
    node_ids = {node["id"] for template in retained for node in walk(template["root"])}
    document["instances"] = [copy.deepcopy(item) for item in original_scene["instances"] if item["template"] in template_ids] + graph_instances
    for instance in document["instances"]:
        material = instance["material"]["resource"]
        match = re.fullmatch(r"markers/(p1|p2|dp)-information", material)
        instance["material"]["resource"] = "markers/frame-" + match.group(1) + "-information" if match else prefix + material
    for field in ("tracks", "bindings", "variants"):
        document[field].extend(copy.deepcopy(item) for item in original_scene[field] if item["target"] in node_ids)
    font_ids = {node["properties"]["bitmap-font"] for template in retained for node in walk(template["root"])
                if "bitmap-font" in node.get("properties", {})}
    manifest = manifest_document()
    manifest["layouts"] = original_manifest["layouts"]
    manifest["bitmapFonts"] = [copy.deepcopy(font) for font in original_manifest.get("bitmapFonts", []) if font["id"] in font_ids]
    referenced = resource_references(document, manifest["bitmapFonts"])
    manifest["resources"] = [copy.deepcopy(resource) for resource in original_manifest["resources"] if resource["id"] in referenced]
    keep_slots = {"stage.background", "decoration", "hud.gauge", "hud.combo", "hud.text", "bga.frame", "playfield.judgement-line",
                  "stage.foreground", "playfield.backdrop", "playfield.baseplate", "playfield.key", "playfield.lane-surface",
                  "playfield.lane-divider", "playfield.hit-target", "effect.key-flash"}
    material_lines, section = [], ""
    for line in (source / "skin.ini").read_text(encoding="utf-8").splitlines():
        if line.startswith("["):
            section = line
        if section == "[General]":
            continue
        if section.startswith("[GameplaySkin."):
            if ":" in line and not line.startswith("Target:") and not line.startswith("["):
                key = line.split(":", 1)[0]
                if key not in keep_slots or key == "decoration" and "-information\"" not in line and line != "decoration: resource Suppress":
                    continue
            def material_path(match):
                original = match.group(1)
                information = re.fullmatch(r"markers/(p1|p2|dp)-information", original)
                mapped = "markers/frame-" + information.group(1) + "-information" if information else prefix + original
                copy_exact(exact_file(source, original + ".png"), output / (mapped + ".png"))
                return 'Provide "' + mapped + '"'
            line = re.sub(r'Provide "([^"]+)"', material_path, line)
        material_lines.append(line)
    materials, _ = correct_frame_decoration_scope(("\n".join(material_lines) + "\n").encode("utf-8"))
    (output / ini_path).parent.mkdir(parents=True, exist_ok=True)
    (output / ini_path).write_bytes(materials)
    variants, layout_receipts = {}, {}
    for layout in ("p1", "p2", "dp"):
        selected_scene = copy.deepcopy(document)
        selected_scene["templates"] = [template for template in selected_scene["templates"]
            if ".template.key." in template["id"] or ".template." + layout + "." in template["id"]]
        selected_templates = {template["id"] for template in selected_scene["templates"]}
        selected_nodes = {node["id"] for template in selected_scene["templates"] for node in walk(template["root"])}
        selected_scene["instances"] = [instance for instance in selected_scene["instances"] if instance["template"] in selected_templates]
        for field in ("tracks", "bindings", "variants"):
            selected_scene[field] = [item for item in selected_scene[field] if item["target"] in selected_nodes]
        selected_fonts = {node["properties"]["bitmap-font"] for template in selected_scene["templates"] for node in walk(template["root"])
                          if "bitmap-font" in node.get("properties", {})}
        selected_manifest = copy.deepcopy(manifest)
        selected_manifest["bitmapFonts"] = [font for font in selected_manifest["bitmapFonts"] if font["id"] in selected_fonts]
        selected_resources = resource_references(selected_scene, selected_manifest["bitmapFonts"])
        selected_manifest["resources"] = [resource for resource in selected_manifest["resources"] if resource["id"] in selected_resources]
        # A selected layout packs only its own font metrics and glyphs. Original advances and pixels remain intact.
        packed = atlas_fonts(selected_manifest, source, output, prefix + layout + "/", "omni.frame." + frame + "." + layout)
        for resource in selected_manifest["resources"]:
            if resource["path"].startswith(prefix):
                continue
            original_path = resource["path"]
            resource["path"] = prefix + original_path
            copy_exact(exact_file(source, original_path), output / resource["path"])
        selected_manifest["bitmapFonts"].append(copy.deepcopy(graph["font"]))
        selected_manifest["resources"].extend(copy.deepcopy(graph["resources"]))
        selected_scene_path = prefix + layout + ".scene.json"
        write_json(output / selected_scene_path, selected_scene)
        write_json(output / (prefix + layout + ".manifest.json"), selected_manifest)
        variants[layout] = {"manifest": prefix + layout + ".manifest.json", "scene": selected_scene_path, "ini": ini_path}
        layout_receipts[layout] = {"resources": len(selected_manifest["resources"]), "fonts": len(selected_manifest["bitmapFonts"]),
                                   "glyphPacking": packed}
    return {"value": frame, "label": FRAME_NAMES[frame], **variants["dp"],
        "variants": [{"when": {"layout": layout}, **variants[layout]} for layout in ("p1", "p2")]}, {
        "frame": frame, "selectedLayouts": layout_receipts, "sharedInformationMarkers": True,
        "removed": ["fixed embedded turn icon", "old FC00", "old judge00", "old notes/CN/HCN/beam/EXP/cover"],
    }


def build_fullcombo(catalog_path: Path, output: Path):
    catalog = read_json(catalog_path)
    source = catalog_path.parent
    # Source exporter deliberately stores original textures once across all 25 designs.
    texture_map = {texture["resource"]: texture for texture in catalog["textures"]}
    resources = {}
    for identifier, texture in texture_map.items():
        path = texture.get("path", texture.get("png"))
        relative = "art/fullcombo/" + Path(path).name
        copy_exact(exact_file(source, path), output / relative)
        resources[identifier] = {"id": identifier, "type": "texture", "path": relative, "filter": "nearest"}
    choices, receipts = [], []
    for package in catalog["packages"]:
        number = int(package.get("id", package.get("package", "fcombo00")).removeprefix("fcombo"))
        value = f"fc{number:02}"
        definitions = []
        required = set()
        root_paths = {}
        durations = {}
        roots = package.get("roots", package.get("movies"))
        for layout in ("p1", "p2", "dp"):
            item = roots[layout] if isinstance(roots, dict) else next(root for root in roots if root.get("layout") == layout)
            path = item.get("clip", item.get("path"))
            clip = read_json(exact_file(source, path))
            if not clip["frames"] or clip["frames"][-1]["nodes"] or clip["loop"] or clip["trigger"] != "gameplay.complete":
                raise ValueError("A full-combo clip lost its original one-shot completion lifecycle")
            durations[layout] = (len(clip["frames"]) - 1) * 1000 / clip["fps"]
            refs = sorted({geometry["resource"] for geometry in clip["geometries"] if geometry["resource"] is not None})
            required.update(refs)
            relative = f"gameplay/fullcombo/{value}/{layout}.json.br"
            (output / relative).parent.mkdir(parents=True, exist_ok=True)
            (output / relative).write_bytes(brotli.compress(json.dumps(clip, separators=(",", ":")).encode("utf-8"), quality=7))
            animation_id = f"omni.fc.{layout}"
            definitions.append({"id": animation_id, "path": relative, "resources": refs})
            root_paths[layout] = animation_id
        if len(set(durations.values())) != 1:
            raise ValueError("The source full-combo layouts no longer share their complete duration")
        prefix = f"gameplay/fullcombo/{value}/"
        scene_path, manifest_path = prefix + "scene.json", prefix + "manifest.json"
        document = scene_document("omni.fc.root")
        document["completionOutro"] = max(durations.values())
        for layout, animation_id in root_paths.items():
            identifier = "omni.fc." + layout
            root = {"id": identifier, "type": "recorded-animation", "animation": animation_id,
                    "target": {"kind": "global"}, "slot": "decoration", "blend": "inherit",
                    "properties": {"visible": False, "z": -100}, "effects": [], "children": []}
            document["templates"].append({"id": identifier + ".template", "root": root})
            document["instances"].append({"id": identifier + ".instance", "template": identifier + ".template",
                "material": {"slot": "decoration", "resource": f"markers/frame-{layout}-information"}})
            document["bindings"].append({"id": identifier + ".visible", "target": identifier, "property": "visible", "source": "gameplay.full-combo"})
            # This exact terminal-empty boundary also fulfils the scene's bounded completion-outro contract.
            document["tracks"].append({"id": identifier + ".lifecycle", "type": "tween", "target": identifier,
                "property": "opacity", "easing": "step", "loop": False, "trigger": "gameplay.complete",
                "keyframes": [{"id": identifier + ".start", "time": 0, "value": 1},
                              {"id": identifier + ".end", "time": durations[layout], "value": 0}]})
        manifest = manifest_document([resources[identifier] for identifier in sorted(required)])
        manifest["animations"] = definitions
        write_json(output / manifest_path, manifest)
        write_json(output / scene_path, document)
        choice = {"value": value, "label": f"Full Combo {number:02}", "manifest": manifest_path, "scene": scene_path}
        choices.append(choice)
        receipts.append({"choice": value, "resources": len(required), "duration": document["completionOutro"], "allThreeLayouts": True,
                         "frameVariants": 0, "defaultIdentity": "needs-original-menu-confirmation"})
    return {"id": "full-combo", "label": "Full Combo Effect", "default": "fc00", "keymodes": SUPPORTED, "choices": choices}, receipts


def catalog_contributions(catalog):
    """The C2 catalog owns actual branch filenames; manifest.scene is a fixed C1 header literal."""
    result = []
    for module in catalog["modules"]:
        for choice in module["choices"]:
            for contribution in (choice, *choice.get("variants", [])):
                if bool(contribution.get("manifest")) != bool(contribution.get("scene")):
                    raise ValueError("A C2 contribution must declare both actual manifest and scene paths")
                result.append(contribution)
    return result


def package_closure(output: Path):
    """Ship referenced data only; offline research/checkpoints are never skin resources."""
    files = {"skin.ini", "gameplay-skin.json", "gameplay-skin.scene.json"}
    pending = ["gameplay-skin.json", "skin.ini"]
    visited = set()
    while pending:
        relative = pending.pop()
        if relative in visited:
            continue
        visited.add(relative)
        path = exact_file(output, relative)
        additions = []
        if relative.endswith(".ini"):
            text = path.read_text(encoding="utf-8")
            additions.extend(match + ".png" for match in re.findall(r'Provide "([^"]+)"', text))
        elif relative.endswith(".json"):
            data = read_json(path)
            if data.get("contract") == MANIFEST_CONTRACT:
                additions.extend(resource["path"] for resource in data.get("resources", []))
                additions.extend(animation["path"] for animation in data.get("animations", []))
                additions.extend(choice["preview"] for option in data.get("options", [])
                                 for choice in option.get("choices", []) if choice.get("preview"))
                if relative == "gameplay-skin.json":
                    additions.extend(data[key] for key in ("scene", "contentCatalog") if data.get(key))
            elif data.get("contract") == "oms-gameplay-skin-content.v1":
                additions.extend(preview_files(data))
                for contribution in catalog_contributions(data):
                    additions.extend(contribution[key] for key in ("manifest", "scene", "ini") if contribution.get(key))
        for addition in additions:
            exact_file(output, addition)
            files.add(addition)
            if addition.endswith((".json", ".ini")):
                pending.append(addition)
    return sorted(files)


def load_modules(directory: Path, output: Path):
    """Consume a frozen module export without repeating any source animation work."""
    metadata = directory.parent / (directory.name + "-module-author-data")
    catalog = read_json(exact_file(metadata, "modules.json"))
    if catalog["contract"] != "oms-gameplay-skin-content.v1":
        raise ValueError("Unsupported frozen module catalog")
    rows = read_json(exact_file(metadata, "files.json"))
    seen = set()
    for item in rows:
        relative = item["path"]
        if relative in seen:
            raise ValueError("Duplicate frozen module file")
        seen.add(relative)
        source = exact_file(directory, relative)
        if source.stat().st_size != item["bytes"] or hashlib.sha256(source.read_bytes()).hexdigest() != item["sha256"]:
            raise ValueError(f"Frozen module file changed: {relative}")
        copy_exact(source, output / relative)
    return {"modules": catalog["modules"], "options": read_json(exact_file(metadata, "options.json")),
            "receipts": read_json(exact_file(metadata, "receipts.json")),
            "frozenDirectory": str(directory), "copiedFiles": len(rows)}


def audit_package(output: Path, files):
    """Check author closure and conservative per-selection limits before creating an archive."""
    catalog = read_json(output / "gameplay/content.json")
    root = read_json(output / "gameplay-skin.json")
    if root["scene"] != SCENE_HEADER_FILE:
        raise ValueError("The root manifest lost the fixed C1 scene contract header")
    ordinary_options = {item["id"] for item in root["options"]}
    module_ids = [module["id"] for module in catalog["modules"]]
    if len(set(module_ids)) != len(module_ids) or ordinary_options.intersection(module_ids):
        raise ValueError("The unified option namespace is not closed")
    if len(ordinary_options) != len(root["options"]) or len(ordinary_options) + len(module_ids) > 32:
        raise ValueError("The unified option budget was exceeded")
    image_sizes, programs, manifests, manifest_resources = {}, {}, {}, {}
    resource_definitions, resource_pixels, resource_bytes = {}, {}, {}

    def pixels(relative):
        if relative not in image_sizes:
            with Image.open(exact_file(output, relative)) as image:
                image_sizes[relative] = image.width * image.height
        value = image_sizes[relative]
        if value <= 0 or value > 16 * 1024 * 1024:
            raise ValueError(f"Texture exceeds its individual decoded budget: {relative}")
        return value

    def contribution(manifest_path, scene_path):
        contribution_key = (manifest_path, scene_path)
        if contribution_key in manifests:
            return manifests[contribution_key]
        manifest_file = exact_file(output, manifest_path)
        manifest = read_json(manifest_file)
        if manifest["scene"] != SCENE_HEADER_FILE:
            raise ValueError(f"A branch manifest.scene must be the fixed C1 header, not its C2 path: {manifest_path}")
        scene_file = exact_file(output, scene_path)
        scene = read_json(scene_file)
        resources = {item["id"]: item for item in manifest.get("resources", [])}
        manifest_resources[contribution_key] = frozenset(resources)
        for identifier, resource in resources.items():
            signature = json.dumps(resource, sort_keys=True, separators=(",", ":"))
            if identifier in resource_definitions and resource_definitions[identifier] != signature:
                raise ValueError(f"A shared resource ID changes texture or sampler: {identifier}")
            resource_definitions[identifier] = signature
            resource_pixels[identifier] = pixels(resource["path"])
            resource_bytes[identifier] = exact_file(output, resource["path"]).stat().st_size
            if resource_bytes[identifier] > 16 * 1024 * 1024:
                raise ValueError(f"One encoded texture exceeds 16MiB: {resource['path']}")
        fonts = {item["id"]: item for item in manifest.get("bitmapFonts", [])}
        animations = {item["id"]: item for item in manifest.get("animations", [])}
        roots = [scene["root"], *(item["root"] for item in scene.get("templates", []))]
        nodes = [node for root_node in roots for node in walk(root_node)]
        node_ids = {node["id"] for node in nodes}
        if len(node_ids) != len(nodes) or len(resources) != len(manifest.get("resources", [])) or len(fonts) != len(manifest.get("bitmapFonts", [])):
            raise ValueError(f"Duplicate author identity: {manifest_path}")
        referenced = resource_references(scene, fonts.values())
        for node in nodes:
            if node.get("blend") not in ("inherit", "alpha", "additive", "multiply", "screen"):
                raise ValueError(f"Invalid C5 scene blend token: {node['id']}")
            if node.get("target", {}).get("kind") is None:
                raise ValueError(f"Node target does not use the public kind field: {node['id']}")
            if node.get("properties", {}).get("bitmap-font") not in (None, *fonts):
                raise ValueError(f"Missing exact bitmap font: {node['id']}")
            if node.get("animation") not in (None, *animations):
                raise ValueError(f"Missing recorded animation: {node['id']}")
        template_roots = {template["id"]: template["root"] for template in scene.get("templates", [])}
        for instance in scene.get("instances", []):
            if instance.get("material") and template_roots[instance["template"]].get("slot") != instance["material"]["slot"]:
                raise ValueError(f"A material-selected template root does not declare its exact slot: {instance['id']}")
        for field in ("tracks", "bindings", "variants"):
            if any(item["target"] not in node_ids for item in scene.get(field, [])):
                raise ValueError(f"Program targets a pruned node: {manifest_path}")
        for animation in animations.values():
            referenced.update(animation["resources"])
            if animation["path"] not in programs:
                data = exact_file(output, animation["path"]).read_bytes()
                decoded = brotli.decompress(data) if animation["path"].endswith(".br") else data
                clip = json.loads(decoded)
                if clip["contract"] != "oms-gameplay-recorded-animation.v1" or len(clip["frames"]) < 2 or clip["frames"][-1]["nodes"]:
                    raise ValueError(f"Invalid recorded terminal frame: {animation['path']}")
                actual = {geometry["resource"] for geometry in clip["geometries"] if geometry.get("resource") is not None}
                if actual != set(animation["resources"]):
                    raise ValueError(f"Recorded resource list is not exact: {animation['path']}")
                triangles = [len(geometry["triangles"]) // 3 for geometry in clip["geometries"]]
                node_triangles = [triangles[node["geometry"]] if node["geometry"] >= 0 else 0 for node in clip["nodes"]]
                programs[animation["path"]] = {"encodedBytes": len(data), "decodedBytes": len(decoded),
                    "frames": len(clip["frames"]), "nodes": len(clip["nodes"]), "states": len(clip["states"]),
                    "duration": (len(clip["frames"]) - 1) * 1000 / clip["fps"],
                    "vertices": sum(len(geometry["vertices"]) for geometry in clip["geometries"]),
                    "applications": sum(len(frame["nodes"]) for frame in clip["frames"]),
                    "maximumLiveNodes": max(len(frame["nodes"]) for frame in clip["frames"]),
                    "maximumLiveTriangles": max(sum(node_triangles[node] for node, state in frame["nodes"]) for frame in clip["frames"])}
                for key, maximum in {"encodedBytes": 16 * 1024 * 1024, "decodedBytes": 128 * 1024 * 1024,
                                     "frames": 3600, "nodes": 4096, "states": 262144, "vertices": 262144,
                                     "applications": 2_000_000, "maximumLiveTriangles": 32768}.items():
                    if programs[animation["path"]][key] > maximum:
                        raise ValueError(f"Recorded {key} exceeds budget: {animation['path']}")
        if not referenced.issubset(resources):
            raise ValueError(f"Undeclared texture dependency: {manifest_path}: {sorted(referenced.difference(resources))}")
        total_pixels = sum(pixels(item["path"]) for item in resources.values())
        result = {"resources": len(resources), "pixels": total_pixels, "decodedTextureBytes": total_pixels * 4,
            "encodedTextureBytes": sum(resource_bytes[identifier] for identifier in resources),
            "fonts": len(fonts), "nodes": len(nodes),
            "tracks": len(scene.get("tracks", [])), "keyframes": sum(len(track["keyframes"]) for track in scene.get("tracks", [])),
            "bindings": len(scene.get("bindings", [])), "variants": len(scene.get("variants", [])),
            "templates": len(scene.get("templates", [])), "instances": len(scene.get("instances", [])),
            "animations": len(animations), "sceneBytes": scene_file.stat().st_size,
            "manifestBytes": manifest_file.stat().st_size,
            "recordedDecodedBytes": sum(programs[item["path"]]["decodedBytes"] for item in animations.values()),
            "recordedNodes": sum(programs[item["path"]]["nodes"] for item in animations.values()),
            "recordedStates": sum(programs[item["path"]]["states"] for item in animations.values()),
            "recordedTriangles": sum(programs[item["path"]]["maximumLiveTriangles"] for item in animations.values())}
        for key, maximum in {"resources": 256, "pixels": 96 * 1024 * 1024, "decodedTextureBytes": 384 * 1024 * 1024,
                             "encodedTextureBytes": 64 * 1024 * 1024,
                             "fonts": 9, "nodes": 2048,
                             "tracks": 512, "keyframes": 8192, "bindings": 512, "variants": 512,
                             "templates": 128, "instances": 1024, "animations": 32,
                             "sceneBytes": 512 * 1024, "manifestBytes": 64 * 1024}.items():
            if result[key] > maximum:
                raise ValueError(f"Contribution {key} exceeds its public budget: {manifest_path}: {result[key]} > {maximum}")
        manifests[contribution_key] = result
        return result

    module_bounds, module_resources = [], {}
    for module in catalog["modules"]:
        choices = module["choices"]
        if not choices or module["default"] not in {choice["value"] for choice in choices}:
            raise ValueError("A module has no actual default contribution")
        measurements = []
        selected_resource_sets = set()
        for choice in choices:
            for part in (choice, *choice.get("variants", [])):
                path = part.get("manifest")
                if path:
                    scene_path = part.get("scene")
                    if not scene_path:
                        raise ValueError("A contribution has no actual C2 catalog scene path")
                    measurements.append(contribution(path, scene_path))
                    selected_resource_sets.add(manifest_resources[(path, scene_path)])
        module_resources[module["id"]] = selected_resource_sets
        module_bounds.append({"module": module["id"], "choices": len(choices),
                              **{key: max(item[key] for item in measurements) for key in measurements[0]}})
    totals = {key: sum(item[key] for item in module_bounds) for key in module_bounds[0] if key not in ("module", "choices")}
    independent_resource_sum = {key: totals[key] for key in ("resources", "pixels", "decodedTextureBytes", "encodedTextureBytes")}
    # Identical source art is intentionally shared by several modules. A resource is counted once in an
    # active manifest, rather than once per module that uses it. Components handle arbitrary shared sets:
    # exclusive dependencies use each module's maximum; at most k distinct shared sets can be selected by
    # k modules, so the k largest unique shared footprints safely bound their union without a Cartesian scan.
    participants = {}
    for module_id, candidates in module_resources.items():
        for identifier in set().union(*candidates):
            participants.setdefault(identifier, set()).add(module_id)
    shared_ids = {identifier for identifier, owners in participants.items() if len(owners) > 1}
    unseen = set(module_resources)
    texture_components = []
    while unseen:
        component = {min(unseen)}
        pending = list(component)
        while pending:
            module_id = pending.pop()
            neighbours = set().union(*(participants[identifier] for identifier in set().union(*module_resources[module_id]) & shared_ids))
            for neighbour in neighbours - component:
                component.add(neighbour)
                pending.append(neighbour)
        unseen.difference_update(component)
        shared_sets = {candidate & shared_ids for module_id in component for candidate in module_resources[module_id]}
        shared_pixel_values = sorted((sum(resource_pixels[item] for item in candidate) for candidate in shared_sets), reverse=True)
        shared_count_values = sorted((len(candidate) for candidate in shared_sets), reverse=True)
        shared_byte_values = sorted((sum(resource_bytes[item] for item in candidate) for candidate in shared_sets), reverse=True)
        exclusive_pixels = sum(max(sum(resource_pixels[item] for item in candidate - shared_ids)
                                   for candidate in module_resources[module_id]) for module_id in component)
        exclusive_resources = sum(max(len(candidate - shared_ids) for candidate in module_resources[module_id]) for module_id in component)
        exclusive_bytes = sum(max(sum(resource_bytes[item] for item in candidate - shared_ids)
                                 for candidate in module_resources[module_id]) for module_id in component)
        texture_components.append({"modules": sorted(component),
            "pixels": exclusive_pixels + sum(shared_pixel_values[:len(component)]),
            "resources": exclusive_resources + sum(shared_count_values[:len(component)]),
            "encodedTextureBytes": exclusive_bytes + sum(shared_byte_values[:len(component)]),
            "distinctSharedFootprints": len(shared_sets)})
    totals["pixels"] = sum(item["pixels"] for item in texture_components)
    totals["resources"] = sum(item["resources"] for item in texture_components)
    totals["decodedTextureBytes"] = totals["pixels"] * 4
    totals["encodedTextureBytes"] = sum(item["encodedTextureBytes"] for item in texture_components)
    aggregate_violations = []
    # These are deliberately conservative: they sum independent module maxima without relying on a lucky selection.
    for key, maximum in {"resources": 256, "pixels": 96 * 1024 * 1024, "decodedTextureBytes": 384 * 1024 * 1024,
                         "encodedTextureBytes": 64 * 1024 * 1024,
                         "fonts": 9, "nodes": 2048,
                         "tracks": 512, "keyframes": 8192, "bindings": 512, "variants": 512,
                         "templates": 128, "instances": 1024, "animations": 32,
                         "sceneBytes": 512 * 1024, "recordedDecodedBytes": 128 * 1024 * 1024,
                         "recordedNodes": 32768, "recordedStates": 131072}.items():
        if totals[key] > maximum:
            aggregate_violations.append({"budget": key, "upperBound": totals[key], "limit": maximum})
    previews = audit_previews(output, catalog)
    total_bytes = sum(exact_file(output, path).stat().st_size for path in files)
    config_bytes = sum(exact_file(output, path).stat().st_size for path in files if path.endswith((".json", ".ini")))
    if len(files) > 8192 or total_bytes > 512 * 1024 * 1024 or config_bytes > 16 * 1024 * 1024:
        raise ValueError("The referenced archive exceeds an unchanged import budget")
    return {"method": "independent maximum per selected content module, then sum; no runtime acceptance asserted",
            "files": len(files), "uncompressedBytes": total_bytes, "configurationBytes": config_bytes,
            "contentChoices": sum(item["choices"] for item in module_bounds), "options": len(ordinary_options) + len(module_ids),
            "choicePreviews": previews,
            "selectedUpperBound": totals, "perModule": module_bounds, "uniqueRecordedPrograms": len(programs),
            "textureSharingComponents": texture_components, "unsharedIndependentSum": independent_resource_sum,
            "textureBudget": {"pixels": 96 * 1024 * 1024, "decodedBytes": 384 * 1024 * 1024,
                "previousPixelLimit": 64 * 1024 * 1024, "previousLimitPassed": totals["pixels"] <= 64 * 1024 * 1024,
                "reason": "Complete independent original Lane/Graph/LIFT artworks plus other selected media exceed the previous limit; source pixels are preserved"},
            "recordedMaximum": {key: max(item[key] for item in programs.values()) for key in next(iter(programs.values()))},
            "aggregateViolations": aggregate_violations,
            "capturedResearchMetadataInArchive": False}


def flat_configuration_path(path: str, kind: str):
    if re.fullmatch("config/" + kind + r"[0-9a-f]{64}\.json", path):
        return path
    return "config/" + kind + hashlib.sha256(path.encode("utf-8")).hexdigest() + ".json"


def correct_root_ini_target(content: bytes):
    """Repair the one known Omni root declaration; preserve every other byte, including newlines."""
    fixed, count = re.subn(rb"(?m)^Target: Global(?=\r?$)", ROOT_GLOBAL_TARGET.encode("ascii"), content)
    if count > 1 or len(re.findall(rb"(?m)^" + re.escape(ROOT_GLOBAL_TARGET.encode("ascii")) + rb"(?=\r?$)", fixed)) != 1:
        raise ValueError("The Omni root INI must contain one complete public Global target")
    return fixed


def correct_frame_decoration_scope(content: bytes):
    """Stop a Frame's Global information selector at each actual Stage, preserving narrower CUSTOM declarations."""
    lines = content.splitlines(keepends=True)
    corrected, stages, added = [], [], []
    section, index = b"", 0
    suppression = b"decoration: resource Suppress"
    while index < len(lines):
        line = lines[index]
        text = line.rstrip(b"\r\n")
        if text.startswith(b"["):
            section = text
        if section.startswith(b"[GameplaySkin.") and text.startswith(b"Target: Stage "):
            end = index + 1
            while end < len(lines) and not lines[end].startswith((b"Target:", b"[")):
                end += 1
            declarations = [item.rstrip(b"\r\n") for item in lines[index + 1:end]
                            if re.match(rb"^[ \t]*decoration[ \t]*:", item)]
            if declarations and declarations != [suppression]:
                raise ValueError("An Omni Frame Stage has an unexpected decoration declaration")
            target = text.decode("ascii")
            stages.append(target)
            corrected.append(line)
            if not declarations:
                newline = b"\r\n" if line.endswith(b"\r\n") else b"\n" if line.endswith(b"\n") else None
                if newline is None:
                    raise ValueError("A Frame Stage target has no declaration line terminator")
                corrected.append(suppression + newline)
                added.append(target)
            corrected.extend(lines[index + 1:end])
            index = end
        else:
            corrected.append(line)
            index += 1
    if not stages:
        raise ValueError("An Omni Frame material document has no actual Stage targets")
    return b"".join(corrected), {"stageTargets": stages, "addedSuppressionTargets": added}


def frame_ini_paths(catalog):
    module = next(item for item in catalog["modules"] if item["id"] == "frame")
    if {choice["value"] for choice in module["choices"]} != set(FRAME_NAMES):
        raise ValueError("The Omni Frame module does not contain the exact fifteen verified choices")
    paths = []
    for choice in module["choices"]:
        expected = f"gameplay/frames/{choice['value']}/materials.ini"
        if any(part.get("ini") != expected for part in (choice, *choice.get("variants", []))):
            raise ValueError("A Frame choice no longer shares its own material document")
        paths.append(expected)
    return sorted(paths)


def correct_frame_ini_scopes(output: Path):
    rows = []
    for relative in frame_ini_paths(read_json(output / "gameplay/content.json")):
        path = exact_file(output, relative)
        original = path.read_bytes()
        corrected, scopes = correct_frame_decoration_scope(original)
        if original != corrected:
            path.write_bytes(corrected)
        rows.append({"path": relative, "beforeSha256": hashlib.sha256(original).hexdigest(),
                     "afterSha256": hashlib.sha256(corrected).hexdigest(), **scopes})
    return {"rule": "Every Frame Stage explicitly suppresses decoration; Global information and all Group/Lane declarations remain byte-exact",
            "scopeEvidence": "GameplaySkinDocumentTarget.Matches: Stage matches Stage/Group/Lane; GetEntry selects more-specific matching targets",
            "filesChecked": len(rows), "filesChanged": sum(row["beforeSha256"] != row["afterSha256"] for row in rows),
            "stageTargets": sum(len(row["stageTargets"]) for row in rows),
            "addedSuppressions": sum(len(row["addedSuppressionTargets"]) for row in rows), "changes": rows}


def flatten_contribution_configs(output: Path):
    """Relocate only contribution JSON; leave original staging data available as evidence."""
    catalog_path = output / "gameplay/content.json"
    catalog = read_json(catalog_path)
    manifest_paths, scene_paths = set(), set()
    contributions = catalog_contributions(catalog)
    for part in contributions:
        if part.get("manifest"):
            manifest_paths.add(part["manifest"])
        if part.get("scene"):
            scene_paths.add(part["scene"])
    source_manifests = {path: read_json(exact_file(output, path)) for path in manifest_paths}
    if manifest_paths.intersection({"gameplay-skin.json"}) or scene_paths.intersection({"gameplay-skin.scene.json"}):
        raise ValueError("A contribution aliases the root manifest or scene")

    manifest_map = {path: flat_configuration_path(path, "m") for path in sorted(manifest_paths)}
    scene_map = {path: flat_configuration_path(path, "s") for path in sorted(scene_paths)}
    if len(set(manifest_map.values())) != len(manifest_map) or len(set(scene_map.values())) != len(scene_map):
        raise ValueError("Contribution path hashing produced a collision")
    for original, destination in scene_map.items():
        copy_exact(exact_file(output, original), output / destination)
    for original, destination in manifest_map.items():
        manifest = source_manifests[original]
        manifest["scene"] = SCENE_HEADER_FILE
        write_json(output / destination, manifest)
    for part in contributions:
        if part.get("manifest"):
            part["manifest"] = manifest_map[part["manifest"]]
        if part.get("scene"):
            part["scene"] = scene_map[part["scene"]]
    write_json(catalog_path, catalog)
    mapping = {"manifests": manifest_map, "scenes": scene_map}
    return {"manifests": len(manifest_map), "scenes": len(scene_map),
            "pathMapSha256": hashlib.sha256(json.dumps(mapping, sort_keys=True, separators=(",", ":")).encode("utf-8")).hexdigest(),
            "rule": "config/m<sha256(original-relative-path)>.json and config/s<sha256(original-relative-path)>.json; already-flat names remain stable",
            "changedFields": ["content catalog contribution manifest/scene paths", "contribution manifest fixed C1 scene header"],
            "manifestSceneHeader": SCENE_HEADER_FILE, "actualSceneAuthority": "C2 catalog contribution.scene",
            "mediaClipIniPaths": "unchanged", "originalStagingFiles": "retained"}


def correct_graph_readout_scene(document):
    frame = document["root"]["id"].removeprefix("omni.frame.").removesuffix(".root")
    if document["root"]["id"] != f"omni.frame.{frame}.root" or frame not in FRAME_NAMES:
        return []
    changes = []
    information_templates = [template for template in document["templates"]
                             if re.search(r"\.template\.(p1|p2|dp)\.information$", template["id"])]
    if not information_templates:
        raise ValueError("A Frame contribution has no exact information template for Graph stats")
    for template in information_templates:
        layout = re.search(r"\.template\.(p1|p2|dp)\.information$", template["id"]).group(1)
        identifier = template["root"]["id"] + ".graph"
        original, bindings = graph_readout(identifier, layout, readable=False)
        corrected, _ = graph_readout(identifier, layout)
        if any(sum(item == binding for item in document["bindings"]) != 1 for binding in bindings):
            raise ValueError("The Graph readout no longer has its exact original read-only bindings")
        graph_template = {"id": template["id"] + ".graph", "root": corrected}
        graph_instance = {"id": identifier + ".instance", "template": graph_template["id"],
                          "material": {"slot": "decoration", "resource": f"markers/frame-{layout}-information"}}
        children = template["root"]["children"]
        old_groups = [child for child in children if child["id"] == identifier]
        existing_templates = [item for item in document["templates"] if item["id"] == graph_template["id"]]
        existing_instances = [item for item in document["instances"] if item["id"] == graph_instance["id"]]
        if old_groups:
            if old_groups != [original] or existing_templates or existing_instances:
                raise ValueError("The legacy Graph readout does not match the proven v6 geometry and ownership")
            template["root"]["children"] = [child for child in children if child["id"] != identifier]
            document["templates"].append(graph_template)
            document["instances"].append(graph_instance)
            changes.append(identifier)
        elif existing_templates != [graph_template] or existing_instances != [graph_instance]:
            raise ValueError("The independent Graph readout does not match its exact readable author layout")
    return changes


def correct_scene_author_contracts(document):
    """Repair proven author declarations; preserve recorded source clips and unrelated scene geometry."""
    changed = {"recordedBlendNodes": [], "fullComboSlotRoots": [], "graphReadoutRoots": []}
    roots = [document["root"], *(template["root"] for template in document.get("templates", []))]
    for root in roots:
        for item in walk(root):
            if item["type"] == "recorded-animation" and item.get("blend") == "normal":
                item["blend"] = "alpha"
                changed["recordedBlendNodes"].append(item["id"])

    if document["root"]["id"] == "omni.fc.root":
        templates = {template["id"]: template for template in document["templates"]}
        instances = {instance["template"]: instance for instance in document["instances"]}
        expected_templates = {f"omni.fc.{layout}.template" for layout in ("p1", "p2", "dp")}
        if set(templates) != expected_templates or set(instances) != expected_templates or len(document["instances"]) != 3:
            raise ValueError("The known full-combo consumer no longer has its exact three layout templates")
        for layout in ("p1", "p2", "dp"):
            identifier = "omni.fc." + layout
            root = templates[identifier + ".template"]["root"]
            material = instances[identifier + ".template"]["material"]
            if (root["id"] != identifier or root["type"] != "recorded-animation" or root["animation"] != identifier
                    or root["target"] != {"kind": "global"}
                    or material != {"slot": "decoration", "resource": f"markers/frame-{layout}-information"}):
                raise ValueError("The full-combo consumer no longer matches its proven ID and marker-selector shape")
            if "slot" not in root:
                root["slot"] = "decoration"
                changed["fullComboSlotRoots"].append(identifier)
            elif root["slot"] != "decoration":
                raise ValueError("The full-combo template declared a conflicting native slot")
    changed["graphReadoutRoots"] = correct_graph_readout_scene(document)
    return changed


def correct_scene_files(output: Path):
    paths = sorted({part["scene"] for part in catalog_contributions(read_json(output / "gameplay/content.json")) if part.get("scene")})
    rows = []
    for relative in paths:
        path = exact_file(output, relative)
        document = read_json(path)
        changes = correct_scene_author_contracts(document)
        if any(changes.values()):
            write_json(path, document)
            rows.append({"path": relative, **changes})
    return {"rules": ["outer C5 recorded-animation blend normal -> alpha; internal clip blend stays normal",
                      "only verified omni.fc.* roots on the three fixed decoration markers receive slot decoration",
                      "verified Frame Graph readouts move below OMS buttons into independent selected-marker templates with a shared-texture dark plate"],
            "changedScenes": len(rows), "recordedBlendNodes": sum(len(row["recordedBlendNodes"]) for row in rows),
            "fullComboSlotRoots": sum(len(row["fullComboSlotRoots"]) for row in rows),
            "graphReadoutRoots": sum(len(row["graphReadoutRoots"]) for row in rows), "changes": rows}


def verify_repackage(source: Path, output: Path, files):
    original_files = package_closure(source)
    original_catalog = read_json(source / "gameplay/content.json")
    expected_catalog = copy.deepcopy(original_catalog)
    manifests, scenes = set(), set()
    for part in catalog_contributions(expected_catalog):
        for key, kind, paths in (("manifest", "m", manifests), ("scene", "s", scenes)):
            if part.get(key):
                original = part[key]
                paths.add(original)
                part[key] = flat_configuration_path(original, kind)
    for module in expected_catalog["modules"]:
        if module["id"] in PREVIEW_MODULES:
            for choice in module["choices"]:
                choice["preview"] = preview_path(module["id"], choice["value"])
    for path in manifests:
        expected = read_json(exact_file(source, path))
        expected["scene"] = SCENE_HEADER_FILE
        if read_json(exact_file(output, flat_configuration_path(path, "m"))) != expected:
            raise ValueError(f"Repackaging changed a manifest beyond its fixed C1 scene header: {path}")
    if read_json(output / "gameplay/content.json") != expected_catalog:
        raise ValueError("Repackaging changed a catalog beyond its configuration paths and exact option illustrations")
    mappings = {**{path: flat_configuration_path(path, "m") for path in manifests},
                **{path: flat_configuration_path(path, "s") for path in scenes}}
    if sorted({mappings.get(path, path) for path in original_files} | preview_files(expected_catalog)) != files:
        raise ValueError("Repackaging changed the original reference closure beyond its exact option illustrations")
    unchanged, byte_equal_scenes = 0, 0
    corrected_scenes, corrected_frame_inis = [], []
    frame_inis = set(frame_ini_paths(original_catalog))
    original_root_ini = exact_file(source, "skin.ini").read_bytes()
    expected_root_ini = correct_root_ini_target(original_root_ini)
    if exact_file(output, "skin.ini").read_bytes() != expected_root_ini:
        raise ValueError("Repackaging changed the root INI beyond its explicit public target repair")
    for relative in original_files:
        if relative in manifests or relative in ("gameplay/content.json", "skin.ini"):
            continue
        destination = mappings.get(relative, relative)
        if relative in frame_inis:
            original_ini = exact_file(source, relative).read_bytes()
            expected_ini, scopes = correct_frame_decoration_scope(original_ini)
            if exact_file(output, destination).read_bytes() != expected_ini:
                raise ValueError(f"Repackaging changed a Frame INI beyond its Stage decoration suppression: {relative}")
            if original_ini != expected_ini:
                corrected_frame_inis.append({"path": relative, "beforeSha256": hashlib.sha256(original_ini).hexdigest(),
                    "afterSha256": hashlib.sha256(expected_ini).hexdigest(), **scopes})
                continue
        if relative in scenes:
            expected_scene = read_json(exact_file(source, relative))
            changes = correct_scene_author_contracts(expected_scene)
            if read_json(exact_file(output, destination)) != expected_scene:
                raise ValueError(f"Repackaging changed a scene beyond the proven author declaration repairs: {relative}")
            if any(changes.values()):
                corrected_scenes.append({"path": relative, **changes})
                continue
        if exact_file(source, relative).read_bytes() != exact_file(output, destination).read_bytes():
            raise ValueError(f"Repackaging changed original media, animation, INI or scene bytes: {relative}")
        if relative in scenes:
            byte_equal_scenes += 1
        else:
            unchanged += 1
    return {"source": str(source), "unchangedPathAndBytes": unchanged, "relocatedByteEqualScenes": byte_equal_scenes,
            "manifestsCheckedForFixedSceneHeader": len(manifests), "manifestSceneHeader": SCENE_HEADER_FILE,
            "actualSceneAuthority": "C2 catalog contribution.scene",
            "catalogChanges": ["contribution configuration paths", "per-choice optional preview paths"],
            "newPreviewFiles": len(preview_files(expected_catalog) - set(original_files)),
            "allPngClipAndOtherIniBytesPreserved": True,
            "frameIniScopeCorrections": corrected_frame_inis,
            "sceneContractCorrections": corrected_scenes, "otherSceneFieldsPreserved": True,
            "rootIniCorrection": {"changed": original_root_ini != expected_root_ini, "onlyPermittedChange": ROOT_GLOBAL_TARGET,
                "beforeSha256": hashlib.sha256(original_root_ini).hexdigest(), "afterSha256": hashlib.sha256(expected_root_ini).hexdigest()}}


def author_directory_budget(files):
    """Directory archive readers count both real files and all intermediate directories."""
    author_files = {*files, "generation.json", "package-audit.json"}
    directories = {str(parent) for relative in author_files for parent in Path(relative).parents if str(parent) != "."}
    result = {"files": len(author_files), "directories": len(directories),
              "entries": len(author_files) + len(directories), "maximumEntries": 8192, "closureOnly": True}
    if result["entries"] > result["maximumEntries"]:
        raise ValueError(f"The real author directory exceeds its entry budget: {result}")
    return result


def prepare_package(output: Path, receipt, repackage_source: Path | None = None):
    root_ini_path = output / "skin.ini"
    original_root_ini = root_ini_path.read_bytes()
    root_ini = correct_root_ini_target(original_root_ini)
    if root_ini != original_root_ini:
        root_ini_path.write_bytes(root_ini)
    receipt["rootPublicTarget"] = ROOT_GLOBAL_TARGET
    receipt["configurationFlattening"] = flatten_contribution_configs(output)
    receipt["sceneContractCorrections"] = correct_scene_files(output)
    if receipt["sceneContractCorrections"]["changedScenes"] and "codecValidation" in receipt:
        receipt["sourceCodecValidation"] = receipt.pop("codecValidation")
        receipt["codecValidation"] = {"status": "pending", "reason": "Author scene declarations changed; the source package codec receipt is historical evidence only"}
    receipt["frameDecorationScopeCorrections"] = correct_frame_ini_scopes(output)
    receipt["graphStatistics"]["readoutLayout"] = graph_readout_receipt()
    receipt["choicePreviews"] = build_choice_previews(output)
    files = package_closure(output)
    if repackage_source:
        receipt["sourcePreservation"] = verify_repackage(repackage_source, output, files)
    audit = audit_package(output, files)
    audit["authorDirectory"] = author_directory_budget(files)
    write_json(output / "package-audit.json", audit)
    receipt["budgets"] = audit["selectedUpperBound"]
    receipt["closure"] = {"files": len(files), "uncompressedBytes": audit["uncompressedBytes"], "configurationBytes": audit["configurationBytes"]}
    receipt["authorDirectory"] = audit["authorDirectory"]
    write_json(output / "generation.json", receipt)
    if audit["aggregateViolations"]:
        receipt["status"] = "budget-rejected-pending-lossless-representation"
        write_json(output / "generation.json", receipt)
        raise ValueError(f"Aggregate budget audit rejected publication; evidence retained at {output / 'package-audit.json'}: {audit['aggregateViolations']}")
    return files, audit


def publish_prepared_package(output: Path, final_output: Path, receipt, files, audit):
    # Build a clean publication from the reference closure. The source staging tree, including old
    # hierarchical configs and unused files, stays intact and can never leak into a directory import.
    publication_id = uuid.uuid4().hex[:12]
    publication = final_output.parent / (final_output.name + ".publication-" + publication_id)
    publication.mkdir()
    for relative in (*files, "generation.json", "package-audit.json"):
        copy_exact(exact_file(output, relative), publication / relative)
    actual_files = sum(1 for item in publication.rglob("*") if item.is_file())
    actual_directories = sum(1 for item in publication.rglob("*") if item.is_dir())
    if (actual_files, actual_directories) != (audit["authorDirectory"]["files"], audit["authorDirectory"]["directories"]):
        raise ValueError("The physical publication tree differs from its predicted reference closure")
    if package_closure(publication) != files:
        raise ValueError("The clean publication lost a dependency")

    archive = final_output.with_suffix(".osk")
    staging_archive = archive.parent / (archive.name + ".staging-" + publication_id)
    with zipfile.ZipFile(staging_archive, "x", zipfile.ZIP_DEFLATED, compresslevel=6) as package:
        for relative in files:
            package.write(publication / relative, relative)
    with zipfile.ZipFile(staging_archive) as package:
        if sorted(package.namelist()) != files or package.testzip() is not None:
            raise ValueError("The new archive failed its exact closure or CRC check")
    with staging_archive.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()
    receipt["archive"] = {"path": str(archive), "bytes": staging_archive.stat().st_size, "sha256": digest, "crc": "passed"}
    write_json(publication / "generation.json", receipt)
    if final_output.exists() or archive.exists():
        raise ValueError("The fresh delivery target was claimed during generation")
    publication.rename(final_output)
    staging_archive.rename(archive)
    print(json.dumps({"name": NAME, "author": str(final_output), "package": str(archive), "bytes": archive.stat().st_size,
                      "sha256": digest, "closure": receipt["closure"], "authorDirectory": audit["authorDirectory"],
                      "selectedUpperBound": audit["selectedUpperBound"]}, ensure_ascii=True))


def publish_package(output: Path, final_output: Path, receipt, repackage_source: Path | None = None):
    files, audit = prepare_package(output, receipt, repackage_source)
    publish_prepared_package(output, final_output, receipt, files, audit)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--frames-root", type=Path)
    parser.add_argument("--custom-library", type=Path)
    parser.add_argument("--fc-catalog", type=Path)
    parser.add_argument("--modules-directory", type=Path, help="Frozen module export with sibling -module-author-data metadata")
    parser.add_argument("--bemani-root", type=Path, help="Pinned independent bemaniutils checkout, required when exporting modules")
    parser.add_argument("--image-tools", type=Path, help="Local Python image dependencies, required when exporting modules")
    parser.add_argument("--frame-maps", type=Path, help="JSON mapping Frame IDs to package-relative original geometry maps")
    parser.add_argument("--repackage-directory", type=Path, help="Derive a fresh flattened package from an existing author closure without re-exporting media")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--frame-fc-only", action="store_true", help="Incomplete integration probe; never a full CUSTOM delivery")
    args = parser.parse_args()
    repackage_source = args.repackage_directory.resolve(strict=True) if args.repackage_directory else None
    module_directory = None
    if repackage_source:
        if any((args.frames_root, args.custom_library, args.fc_catalog, args.modules_directory, args.frame_fc_only, args.bemani_root, args.image_tools, args.frame_maps)):
            parser.error("Repackaging takes only --repackage-directory and --output")
        source_roots = [repackage_source]
    else:
        if not all((args.frames_root, args.custom_library, args.fc_catalog)):
            parser.error("A source build requires --frames-root, --custom-library and --fc-catalog")
        source_roots = [args.frames_root.resolve(strict=True), args.custom_library.resolve(strict=True), args.fc_catalog.resolve(strict=True).parent]
        module_directory = args.modules_directory.resolve(strict=True) if args.modules_directory else None
        if not module_directory and not args.frame_fc_only and not all((args.bemani_root, args.image_tools, args.frame_maps)):
            parser.error("Exporting modules requires --bemani-root, --image-tools and --frame-maps; use --modules-directory for frozen exports")
        if module_directory:
            source_roots.append(module_directory)
    output = args.output.resolve()
    if output.exists() or output.with_suffix(".osk").exists() or not output.parent.is_dir():
        parser.error("Use a fresh author directory with an existing parent")
    if output.drive.lower() == os.environ.get("SystemDrive", "C:").lower() or output.drive.lower() == "d:":
        parser.error("Author output must use an independent non-system, non-HDD drive")
    if any(output.is_relative_to(root) or root.is_relative_to(output) for root in source_roots):
        parser.error("Output must not overlap any source library")
    final_output = output
    staging_id = uuid.uuid4().hex[:12]
    output = final_output.parent / (final_output.name + ".staging-" + staging_id)
    output.mkdir()
    write_json(output / "generation.json", {"status": "running", "name": NAME})
    if repackage_source:
        for relative in package_closure(repackage_source):
            copy_exact(exact_file(repackage_source, relative), output / relative)
        receipt = read_json(exact_file(repackage_source, "generation.json"))
        receipt["status"] = "repackaged-pending-runtime-validation"
        receipt.pop("archive", None)
        receipt["repackagedFrom"] = str(repackage_source)
        publish_package(output, final_output, receipt, repackage_source)
        return
    graph = prepare_graph(source_roots[1], output)
    frames, frame_receipts = [], []
    for frame in FRAME_NAMES:
        choice, receipt = build_frame(frame, exact_file(source_roots[0], frame + "-candidate09"), output, graph)
        frames.append(choice)
        frame_receipts.append(receipt)
    frame_module = {"id": "frame", "label": "Game Frame", "default": "iidx33", "keymodes": SUPPORTED, "choices": frames}
    fc_module, fc_receipts = build_fullcombo(args.fc_catalog.resolve(strict=True), output)
    modules = {"modules": [], "options": [], "receipts": []}
    if not args.frame_fc_only:
        if module_directory:
            modules = load_modules(module_directory, output)
        else:
            from customization_modules import build_modules
            modules = build_modules(source_roots[1], output, bemani_root=args.bemani_root, image_tools=args.image_tools,
                                    frame_maps=args.frame_maps)
    layout = {"id": "layout", "type": "choice", "label": "游玩区域样式", "default": "p1", "effect": "presentation",
              "keymodes": ["5k", "6k", "7k", "8k"], "choices": [{"value": "p1", "label": "1P（左皿）"}, {"value": "p2", "label": "2P（右皿）"}]}
    scene = scene_document("omni.root")
    manifest = manifest_document()
    graph_visible = {"id": "graph-visible", "type": "toggle", "label": "Graph Area 显示统计", "default": False}
    manifest.update({"contentCatalog": "gameplay/content.json", "options": [layout, graph_visible, *modules["options"]]})
    if args.frame_fc_only:
        manifest["options"].extend([
            {"id": "auxiliary-display", "type": "toggle", "label": "Auxiliary display", "default": False},
            {"id": "concentration", "type": "toggle", "label": "Concentration", "default": False},
            {"id": "concentration-brightness", "type": "number", "label": "Concentration brightness", "default": 0.5,
             "min": 0, "max": 1, "step": 0.05}])
    manifest["auxiliaryDisplay"] = {"enabledOption": "auxiliary-display", "concentrationOption": "concentration",
                                     "brightnessOption": "concentration-brightness"}
    write_json(output / "gameplay-skin.json", manifest)
    write_json(output / "gameplay-skin.scene.json", scene)
    write_json(output / "gameplay/content.json", {"contract": "oms-gameplay-skin-content.v1", "modules": [frame_module, *modules["modules"], fc_module]})
    for marker in ("auxiliary", "auxiliary-concentration"):
        if not (output / ("markers/" + marker + ".png")).exists():
            source_marker = output / "markers/frame-p1-information.png"
            with Image.open(source_marker) as image:
                if image.convert("RGBA").getchannel("A").getextrema() != (0, 0):
                    raise ValueError("The auxiliary selector placeholder must be completely transparent")
            copy_exact(source_marker, output / ("markers/" + marker + ".png"))
        exact_file(output, "markers/" + marker + ".png")
    (output / "skin.ini").write_text(f"[General]\nName: {NAME}\nAuthor: Local IIDX study\nVersion: 2.7\n\n"
        '[GameplaySkin.Common:1]\n' + ROOT_GLOBAL_TARGET + '\nhud.auxiliary: resource Provide "markers/auxiliary"\n'
        'hud.auxiliary-concentration: resource Provide "markers/auxiliary-concentration"\n', encoding="utf-8")
    receipt = {"name": NAME, "status": "frame-fc-integration-probe" if args.frame_fc_only else "generated-pending-runtime-validation",
               "defaultFrame": "iidx33", "future34": "out-of-scope", "frameReceipts": frame_receipts, "fullComboReceipts": fc_receipts,
               "customization": modules["receipts"], "graphStatistics": graph["receipt"],
               "auxiliaryMarkers": "Byte-exact copies of the verified transparent Frame information marker; selector-only placeholders",
               "frozenModulesDirectory": modules.get("frozenDirectory"),
               "originalHdd": "not-opened-by-this-generator", "sourceDefaultMapping": "see dated source evidence"}
    publish_package(output, final_output, receipt)


if __name__ == "__main__":
    main()
