"""Original AUTO SCRATCH art for frozen Omni note/CN contributions.

These helpers do not write an author or operate an installation. Read helpers
measure the independent library/current author; transform_contribution is pure.
Only the existing BMS scratch material consumers change. Auto Note has no
established IIDX art here. The extra auto_joint image has no current C4 consumer
and is recorded, not stretched into a long-note body or used to add BSS/MSS.
"""
from __future__ import annotations

import copy
import hashlib
import io
import json
from pathlib import Path
import re

from PIL import Image

from lighting_gameplay_controls import inside, require
from music_progress import _stable_id

SOURCE = "object.auto-assist"
CONTRACT = "oms-iidx-auto-scratch-art.v1"
SIZES = ("normal", "long", "short", "veryshort")
PALETTES = ("source", "cn-palette")
NOTE_MARKER = "custom/markers/n/scratch"
CHARGE_MARKERS = {part: "custom/markers/c/scratch-" + part for part in ("head", "body", "tail")}
PART_TEXTURES = {"head": "start", "body": "beam0", "tail": "end"}
CHARGE_TEXTURES = ("notes_bss_start", "notes_bss_beam0", "notes_bss_beam1", "notes_bss2_beam4", "notes_bss_end",
                   "notes_bss_auto_start", "notes_bss_auto_beam0", "notes_bss_auto_end", "notes_bss_auto_joint")
# These are measured discrete source canvases, not a fallback to another size.
# Their exact PNG/pixel hashes and original UV rectangles are measured below and
# remain in the private source receipt rather than a source-art hash list here.
SOURCE_CANVAS_EXCEPTIONS = {"contents/data/graphic/notes_hcn01_long.ifs": {
    "notes_bss_auto_start": (90, 24), "notes_bss_auto_end": (90, 24), "notes_bss_auto_joint": (90, 36)}}


def digest(data):
    return hashlib.sha256(data).hexdigest()


def _independent(path):
    path = Path(path).resolve(strict=True)
    require(path.drive.casefold() != "d:" and "private-data" not in {part.casefold() for part in path.parts},
            "Use an independent source library or frozen author, not an installation or player data")
    return path


def _walk(value):
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from _walk(child)
    elif isinstance(value, list):
        for child in value:
            yield from _walk(child)


def _check_ids(scene):
    declared = set()
    for value in _walk(scene):
        if "id" not in value:
            continue
        identifier = _stable_id(value["id"])
        require(identifier not in declared, "Duplicate scene declaration: " + identifier)
        declared.add(identifier)
    return declared


def _inherit_owner(node, slot):
    """Only the new template host owns Provide; all branches inherit that key."""
    require(node.get("slot") in (None, slot), "An AUTO SCRATCH branch declares a different material owner")
    node.pop("slot", None)
    for child in node["children"]:
        _inherit_owner(child, slot)


def material_ownership(scene):
    """Trace all material-template edges, including currently invisible branches.

    C# prepareNode passes the material instance's exact rootTargetOverride through
    every descendant. A second SlotId would create another owner and is rejected
    before visibility or an assist/mode variant is considered.
    """
    templates = {template["id"]: template["root"] for template in scene["templates"]}
    edges, roots = [], []
    for instance in scene["instances"]:
        if "material" not in instance:
            continue
        material = instance["material"]
        root = templates[instance["template"]]
        require(root.get("slot") == material["slot"], "A material template needs its one exact owner at the root")
        target = (instance["id"], material["resource"])
        owner = (material["slot"], target)
        roots.append({"instance": instance["id"], "root": root["id"], "slot": material["slot"]})

        def visit(node, inherited, parent=None):
            declared = node.get("slot")
            require(not (declared is not None and inherited is not None),
                    "prepareNode rejects a descendant SlotId under an inherited owner: " + node["id"])
            effective_owner = (declared, target) if declared is not None else inherited
            require(effective_owner == owner, "A branch lost its exact material owner/target")
            if parent is not None:
                edges.append({"parent": parent, "child": node["id"], "slot": effective_owner[0],
                              "materialInstance": effective_owner[1][0], "exactTargetInherited": True})
            for child in node["children"]:
                visit(child, effective_owner, node["id"])

        visit(root, None)
    return {"roots": roots, "edges": edges, "includesInvisibleBranches": True, "nestedSlotOwners": 0}


def _selection(spec):
    module, choice, size, palette = (spec[key] for key in ("module", "choice", "size", "palette"))
    require(size in SIZES and palette in PALETTES, "Unsupported original size/palette selection")
    if module == "notes":
        require(re.fullmatch(r"n(?:0[0-9]|1[01])", choice) is not None and palette == "source", "Unknown Notes choice")
        style = int(choice[1:])
    else:
        require(module == "charge-notes" and re.fullmatch(r"c(?:0[0-9]|10)", choice) is not None,
                "Unknown Charge Notes choice")
        style = int(choice[1:])
    return module, style, size, palette


def contribution_specs(catalogue):
    """Enumerate all 48 Notes and 88 CN size/palette contributions, preserving paths."""
    result = []
    for module_id, prefix, count in (("notes", "n", 12), ("charge-notes", "c", 11)):
        modules = [module for module in catalogue["modules"] if module["id"] == module_id]
        require(len(modules) == 1, "The frozen catalogue must contain one " + module_id + " module")
        choices = modules[0]["choices"]
        require({choice["value"] for choice in choices} == {prefix + f"{index:02}" for index in range(count)}
                and len(choices) == count, "The original design catalogue changed: " + module_id)
        for choice in choices:
            seen = set()
            for index, part in enumerate((choice, *choice.get("variants", []))):
                when = {} if index == 0 else part["when"]
                if module_id == "notes":
                    require(index == 0 or set(when) == {"note-size"}, "Unexpected Notes contribution condition")
                    size, palette = when.get("note-size", "normal"), "source"
                else:
                    require(index == 0 or set(when) == {"charge-size", "hcn-color"},
                            "Unexpected Charge Notes contribution condition")
                    size, palette = when.get("charge-size", "normal"), when.get("hcn-color", "source")
                spec = {"module": module_id, "choice": choice["value"], "size": size, "palette": palette,
                        "manifest": part["manifest"], "scene": part["scene"], "when": copy.deepcopy(when)}
                if "ini" in part:
                    spec["ini"] = part["ini"]
                _selection(spec)
                require((size, palette) not in seen, "Repeated size/palette contribution")
                seen.add((size, palette))
                result.append(spec)
            expected = {(size, "source") for size in SIZES} if module_id == "notes" else {
                (size, palette) for size in SIZES for palette in PALETTES}
            require(seen == expected, "A frozen size/palette contribution is missing")
    return result


def source_packages(spec):
    module, style, size, palette = _selection(spec)
    suffix = "" if size == "normal" else "_" + size
    if module == "notes":
        # This is the existing explicit notes11 author-geometry adaptation, not
        # a missing-file fallback. There are no notes11_long/short source IFSes.
        package = f"notes{style:02}" + (suffix if style != 11 else "")
        return {"notes": "contents/data/graphic/" + package + ".ifs"}
    cn = "contents/data/graphic/" + f"notes_cn{style:02}{suffix}.ifs"
    return {"cn": cn, "hcn": cn if palette == "cn-palette" else "contents/data/graphic/" + f"notes_hcn{style:02}{suffix}.ifs"}


def source_reference(library, specs):
    """Verify exact PNG bytes, decoded pixels, discrete sizes and atlas UV receipts."""
    library = _independent(library)
    metadata_path = inside(library, "library.json")
    metadata_bytes = metadata_path.read_bytes()
    metadata = json.loads(metadata_bytes.decode("utf-8-sig"))
    require(metadata.get("status") == "complete" and metadata.get("sourceMetadataUnchanged") is True,
            "Use the complete verified independent source library")
    specs = list(specs)
    require(specs, "Supply actual frozen C2 contribution selections")
    requests = {}
    for spec in specs:
        for package in source_packages(spec).values():
            requests[package] = ("notes_turn", "notes_auto_turn") if spec["module"] == "notes" else CHARGE_TEXTURES
    package_rows = {row["source"]: row for row in metadata["packages"]}
    texture_rows = {}
    for row in metadata["textures"]:
        key = row["package"], row["name"]
        if row["package"] in requests:
            require(key not in texture_rows, "Duplicate source texture identity")
            texture_rows[key] = row
    packages, inputs = {}, [{"path": str(metadata_path), "sha256": digest(metadata_bytes)}]
    for package, names in sorted(requests.items()):
        require(package in package_rows, "Missing original source package: " + package)
        source = package_rows[package]
        package_path = inside(library, source["authorData"])
        package_bytes = package_path.read_bytes()
        detail = json.loads(package_bytes.decode("utf-8-sig"))
        require(not detail["movies"] and not detail["geometry"] and detail["unknownPlacementCount"] == 0,
                "This frozen note package no longer has the verified texture-only consumer boundary")
        inputs.append({"path": str(package_path), "sha256": digest(package_bytes)})
        assets = {}
        for name in names:
            key = package, name + ".png"
            require(key in texture_rows, "Missing original texture: " + package + "/" + name)
            row = texture_rows[key]
            sampler = row["sampler"]
            require(sampler["min"] == sampler["mag"] == "nearest" and sampler["wrapS"] == sampler["wrapT"] == "clamp",
                    "Preserve the original nearest/clamp sampler")
            path = inside(library, row["sprite"])
            data = path.read_bytes()
            require(digest(data) == row["sha256"], "An original AUTO SCRATCH source PNG changed")
            with Image.open(io.BytesIO(data)) as image:
                require(image.size == (row["width"], row["height"]), "Source PNG dimensions changed")
                rgba = digest(image.convert("RGBA").tobytes())
            require(rgba == row["rgbaSha256"], "Source PNG pixel receipt changed")
            left, right, top, bottom = row["uvRect"]
            require((right - left, bottom - top) == (row["width"], row["height"]), "Source UV dimensions disagree")
            if name.startswith("notes_bss"):
                head_height = {"normal": 24, "long": 27, "short": 21, "veryshort": 18}[
                    next((size for size in SIZES[1:] if package.endswith("_" + size + ".ifs")), "normal")]
                expected_height = head_height + 12 if name.endswith("joint") else 24 if "beam" in name else head_height
            else:
                expected_height = 12 if package.endswith("notes11.ifs") else 15
            standard_canvas = (90, expected_height)
            exception = SOURCE_CANVAS_EXCEPTIONS.get(package, {}).get(name)
            require((row["width"], row["height"]) == (exception or standard_canvas), "Original Scratch artwork size changed")
            assets[name] = {"name": name, "package": package, "source": row["sprite"], "sha256": row["sha256"],
                "rgbaSha256": rgba, "width": row["width"], "height": row["height"],
                "imageRect": copy.deepcopy(row["imageRect"]), "uvRect": copy.deepcopy(row["uvRect"]),
                "sampler": copy.deepcopy(sampler), "sourceArchiveSha256": source["sha256"]}
            if exception:
                assets[name]["canvasException"] = {"standardSizeCanvas": list(standard_canvas),
                    "actualSourceCanvas": list(exception), "basis": "explicit-measured-notes-hcn01-long-discrete-AUTO-canvas",
                    "pixelsResampled": False}
            inputs.append({"path": str(path), "sha256": row["sha256"]})
        packages[package] = {"assets": assets, "sourceArchiveSha256": source["sha256"],
            "consumerBoundary": "Texture/UV source only; existing OMS C4 scratch markers establish the current head/body/tail consumers"}
    return {"contract": CONTRACT, "library": str(library), "librarySha256": digest(metadata_bytes), "packages": packages,
        "readInputs": inputs, "originalHddAccessed": False, "originalPixelsChanged": False, "fontsAdded": 0,
        "unconsumedJoint": "notes_bss_auto_joint is retained as source evidence. Current C4 has no joint consumer; BSS/MSS behavior is not added.",
        "scope": "AUTO SCRATCH art only; Auto Note and full Autoplay retain their normal source branch"}


def read_contribution(author, spec):
    """Read a frozen contribution and measure its declared PNGs without changing it."""
    author = _independent(author)
    paths = {key: inside(author, spec[key]) for key in ("manifest", "scene")}
    raw = {key: path.read_bytes() for key, path in paths.items()}
    manifest, scene = (json.loads(raw[key].decode("utf-8-sig")) for key in ("manifest", "scene"))
    measurements, inputs, cache = {}, [{"path": str(paths[key]), "sha256": digest(raw[key])} for key in paths], {}
    for resource in manifest["resources"]:
        path = inside(author, resource["path"])
        if resource["path"] not in cache:
            data = path.read_bytes()
            with Image.open(io.BytesIO(data)) as image:
                measured = {"sha256": digest(data), "rgbaSha256": digest(image.convert("RGBA").tobytes()),
                            "width": image.width, "height": image.height}
            cache[resource["path"]] = measured
            inputs.append({"path": str(path), "sha256": measured["sha256"]})
        measurements[resource["id"]] = copy.deepcopy(cache[resource["path"]])
    return manifest, scene, {"resources": measurements, "readInputs": inputs}


def _variant(identifier, target, source, default, cases, *, prop="resource"):
    field = "resource" if prop == "resource" else "value"
    return {"id": identifier, "target": target, "property": prop, "source": source, "default": default,
            "cases": [{"id": identifier + ".c" + str(index), "key": key, field: value}
                      for index, (key, value) in enumerate(cases.items())]}


def transform_contribution(manifest, scene, spec, reference, measurements):
    """Return new manifest/scene/receipt; all supplied objects and normal branches stay intact."""
    module, style, size, palette = _selection(spec)
    require(reference.get("contract") == CONTRACT, "Use verified original AUTO SCRATCH artwork")
    require(not any(value.get("source") == SOURCE for value in _walk(scene)), "AUTO SCRATCH is already attached")
    require(manifest["contract"] == "oms-gameplay-skin-manifest.v1" and scene["contract"] == "oms-gameplay-skin-scene.v1",
            "Use public frozen gameplay contributions")
    _check_ids(scene)
    result_manifest, result_scene = copy.deepcopy(manifest), copy.deepcopy(scene)
    resources = {resource["id"]: resource for resource in manifest["resources"]}
    require(len(resources) == len(manifest["resources"]), "Duplicate original resource identity")
    templates = {template["id"]: template for template in result_scene["templates"]}
    packages = source_packages(spec)
    logical_sources, new_files, consumers = [], {}, []

    def art(package, name):
        require(package in reference["packages"] and name in reference["packages"][package]["assets"],
                "A required original AUTO SCRATCH image is absent: " + package + "/" + name)
        return reference["packages"][package]["assets"][name]

    def check_original(identifier, item):
        require(identifier in resources and identifier in measurements["resources"], "Missing measured original C4 resource")
        measured = measurements["resources"][identifier]
        require(resources[identifier].get("filter") == "nearest" and all(measured[key] == item[key]
            for key in ("sha256", "width", "height")), "Original C4 resource does not match its exact source package")

    def auto_resource(item):
        identifier = "as.t" + item["sha256"][:12]
        path = "a/" + item["sha256"][:16] + ".png"
        declaration = {"id": identifier, "type": "texture", "path": path, "filter": "nearest"}
        _stable_id(identifier)
        existing = next((row for row in result_manifest["resources"] if row["id"] == identifier), None)
        require(existing is None or existing == declaration, "AUTO SCRATCH resource ID collision")
        if existing is None:
            result_manifest["resources"].append(declaration)
        require(path not in new_files or new_files[path]["sha256"] == item["sha256"], "AUTO SCRATCH content-prefix collision")
        new_files[path] = {"source": str(Path(reference["library"]) / item["source"]), "sha256": item["sha256"],
                           "rgbaSha256": item["rgbaSha256"], "width": item["width"], "height": item["height"]}
        logical_sources.append(copy.deepcopy(item))
        return identifier

    expected = {"note": NOTE_MARKER} if module == "notes" else CHARGE_MARKERS
    for index, (part, marker) in enumerate(expected.items()):
        instances = [instance for instance in result_scene["instances"] if instance.get("material", {}).get("resource") == marker]
        require(len(instances) == 1, "Expected one exact BMS scratch material consumer: " + marker)
        instance = instances[0]
        template = templates[instance["template"]]
        require(sum(item["template"] == instance["template"] for item in result_scene["instances"]) == 1,
                "A BMS scratch template unexpectedly serves another consumer")
        original = template["root"]
        slot = "object.note" if part == "note" else "object.long-note." + part
        require(instance["material"]["slot"] == slot and original.get("slot") == slot
                and original["type"] == "sprite" and not original["children"], "Unexpected original C4 scratch sprite structure")
        require(original["properties"].get("visible", True) is True and not any(
            value.get("target") == original["id"] and value.get("property") == "visible"
            for value in (*scene.get("variants", []), *scene["bindings"], *scene["tracks"],
                          *(entry for machine in scene["stateMachines"] for entry in _walk(machine)))),
                "Existing scratch visibility writer cannot be replaced")
        prefix = "as.h" + str(index)
        if part == "note":
            normal, automatic = (art(packages["notes"], name) for name in ("notes_turn", "notes_auto_turn"))
            check_original(original["resource"], normal)
            require((normal["width"], normal["height"]) == (automatic["width"], automatic["height"]),
                    "Notes AUTO geometry is not the same source canvas")
            require(not any(value.get("target") == original["id"] and value.get("property") == "resource"
                            for value in scene.get("variants", [])), "Unexpected Notes resource writer")
            automatic_id = auto_resource(automatic)
            result_scene.setdefault("variants", []).append(_variant(prefix + ".r", original["id"], SOURCE,
                original["resource"], {"scratch": automatic_id}))
            consumers.append({"marker": marker, "slot": slot, "originalNode": original["id"],
                "autoResources": {"scratch": automatic_id}, "geometry": copy.deepcopy(original["properties"]),
                "sizeMethod": "existing-notes11-author-geometry-scale" if style == 11 and size != "normal" else "original-discrete-PNG"})
            continue

        ending = PART_TEXTURES[part]
        normal = art(packages["cn"], "notes_bss_" + ending)
        check_original(original["resource"], normal)
        variants = [variant for variant in scene.get("variants", [])
                    if variant["target"] == original["id"] and variant["property"] == "resource"]
        require(len(variants) == 1 and variants[0]["source"] == (
            "object.long-note-appearance" if part == "body" else "object.long-note-mode"),
            "Preserve the actual original long-note mode/appearance consumer")
        variant = variants[0]
        check_original(variant["default"], normal)
        wanted = {}
        for mode in ("ln", "cn", "hcn"):
            package = packages["hcn"] if mode == "hcn" else packages["cn"]
            wanted[mode] = art(package, "notes_bss_" + ending)
            if part == "body":
                wanted[mode + "-holding"] = art(package, "notes_bss_beam1")
                wanted[mode + "-released"] = art(package, "notes_bss2_beam4")
        require({case["key"] for case in variant["cases"]} == set(wanted), "Original long-note resource cases changed")
        for case in variant["cases"]:
            check_original(case["resource"], wanted[case["key"]])
        automatic = {mode: art(packages["hcn"] if mode == "hcn" else packages["cn"], "notes_bss_auto_" + ending)
                     for mode in ("ln", "cn", "hcn")}
        height_scales = {}
        auto_geometry = {}
        for mode, item in automatic.items():
            ordinary = wanted[mode]
            require(item["width"] == ordinary["width"], "AUTO SCRATCH must retain the actual source width")
            if item["height"] != ordinary["height"]:
                exception = SOURCE_CANVAS_EXCEPTIONS.get(item["package"], {}).get(item["name"])
                require(part in ("head", "tail") and exception == (item["width"], item["height"])
                        and ordinary["height"] == 27,
                        "AUTO SCRATCH canvas mismatch has no explicit original source exception")
            height_scales[mode] = item["height"] / ordinary["height"]
            properties = copy.deepcopy(original["properties"])
            if height_scales[mode] != 1:
                height_fields = [field for field in ("height", "height-px") if field in properties]
                require(len(height_fields) == 1, "The source-sized AUTO cap needs one explicit original height")
                properties[height_fields[0]] *= height_scales[mode]
            auto_geometry[mode] = properties
        auto_ids = {mode: auto_resource(item) for mode, item in automatic.items()}
        if len(set(height_scales.values())) > 1:
            # A resource variant cannot write height. Keep the ordinary CN cap
            # and the smaller source HCN AUTO cap in independent mode gates.
            require(height_scales["ln"] == height_scales["cn"] == 1
                    and height_scales["hcn"] == 24 / 27 and auto_ids["ln"] == auto_ids["cn"],
                    "Unexpected source canvas combination needs its own explicit adaptation")
            caps = []
            for mode in ("cn", "hcn"):
                cap = copy.deepcopy(original)
                cap.update({"id": prefix + ".auto." + mode, "resource": auto_ids[mode], "properties": auto_geometry[mode]})
                caps.append(cap)
                result_scene.setdefault("variants", []).append(_variant(prefix + ".size." + mode, cap["id"],
                    "object.long-note-mode", mode == "cn", {"hcn": mode == "hcn"}, prop="visible"))
            automatic_node = {"id": prefix + ".auto", "type": "container", "target": copy.deepcopy(original["target"]),
                "slot": slot, "blend": "inherit", "properties": {"width": 1, "height": 1}, "effects": [], "children": caps}
        else:
            automatic_node = copy.deepcopy(original)
            automatic_node.update({"id": prefix + ".auto", "resource": auto_ids["cn"], "properties": auto_geometry["cn"]})
        _inherit_owner(original, slot)
        _inherit_owner(automatic_node, slot)
        wrapper = {"id": prefix + ".host", "type": "container", "target": copy.deepcopy(original["target"]),
            "slot": slot, "blend": "inherit", "properties": {"width": 1, "height": 1}, "effects": [],
            "children": [original, automatic_node]}
        template["root"] = wrapper
        result_scene.setdefault("variants", []).extend([
            _variant(prefix + ".normal", original["id"], SOURCE, True, {"scratch": False}, prop="visible"),
            _variant(prefix + ".enabled", automatic_node["id"], SOURCE, False, {"scratch": True}, prop="visible")])
        if automatic_node["type"] == "sprite" and len(set(auto_ids.values())) > 1:
            result_scene["variants"].append(_variant(prefix + ".mode", automatic_node["id"],
                "object.long-note-mode", auto_ids["cn"], auto_ids))
        bindings = [binding for binding in scene["bindings"] if binding["target"] == original["id"]]
        for binding_index, binding in enumerate(bindings):
            require(binding["property"] == "brightness", "Unexpected scratch sprite binding requires an explicit adaptation")
            result_scene["bindings"].append({**copy.deepcopy(binding), "id": prefix + ".b" + str(binding_index),
                                             "target": automatic_node["id"]})
        consumers.append({"marker": marker, "slot": slot, "originalNode": original["id"],
            "autoNode": automatic_node["id"], "wrapper": wrapper["id"], "autoResources": auto_ids,
            "geometry": copy.deepcopy(original["properties"]), "autoGeometry": auto_geometry, "heightScales": height_scales,
            "bodySource": "auto_beam0; not auto_joint" if part == "body" else None})
    _check_ids(result_scene)
    ownership = material_ownership(result_scene)
    for row in result_manifest["resources"]:
        _stable_id(row["id"])
    receipt = {"contract": CONTRACT, "module": module, "choice": spec["choice"], "size": size, "palette": palette,
        "sourcePackages": packages, "consumers": consumers, "originalArt": logical_sources,
        "newFiles": new_files, "addedResources": len(result_manifest["resources"]) - len(manifest["resources"]),
        "source": SOURCE, "values": {"none": "original branch", "note": "original branch", "scratch": "original AUTO SCRATCH art"},
        "joint": {package: copy.deepcopy(art(package, "notes_bss_auto_joint")) for package in sorted(set(packages.values()))}
                 if module == "charge-notes" else {},
        "ordinaryNodesAndProgramsPreserved": True, "nativeHiddenAndTintContainerChanged": False,
        "materialOwnership": ownership, "redundantDescendantSlotDeclarationsRemoved": module == "charge-notes",
        "autoNoteArtInvented": False, "fontsAdded": 0, "optionsAdded": 0,
        "scope": "Existing C4 ordinary Scratch and long-note head/body/tail presentation only; no BSS/MSS/joint semantics"}
    return result_manifest, result_scene, receipt
