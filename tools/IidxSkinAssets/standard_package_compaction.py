"""Compact authored V1 references without changing artwork or gameplay geometry.

The input is a generated package, never a game HDD or another skin's assets.
Material aliases, animation IDs, observed semantic node IDs and INI bytes remain
unchanged. Other scene identities and their typed references may shrink. Exact
duplicate catalog configurations share one original path, never texture owners.
The caller supplies a fresh output directory and a private receipt outside it.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import stat


MANIFEST = "gameplay-skin.json"
SCENE = "gameplay-skin.scene.json"
STABLE_ID = re.compile(r"^[a-z][a-z0-9.-]{0,127}$")
RESOURCE_LITERAL = re.compile(r'(?<!\\)("resource"\s*:\s*)("(?:[^"\\]|\\.)*")')
MAX_CONFIGURATION_BYTES = 32 * 1024 * 1024


def encoded(value):
    return json.dumps(value, ensure_ascii=False, separators=(",", ":")).encode("utf-8")


def digest(data):
    return hashlib.sha256(data).hexdigest()


def safe_path(root: Path, relative: str) -> Path:
    path = PurePosixPath(relative)
    if not relative or "\\" in relative or path.is_absolute() or ":" in relative or ".." in path.parts:
        raise ValueError(f"Unsafe package path: {relative}")
    result = root.joinpath(*path.parts)
    if not result.resolve().is_relative_to(root):
        raise ValueError(f"Package path escapes its owner: {relative}")
    return result


def mapped(value, mapping, kind):
    if value not in mapping:
        raise ValueError(f"Dangling {kind} reference: {value}")
    return mapping[value]


def scene_declarations(scene):
    """Only these five declaration identities have no target-reference role."""
    for track in scene.get("tracks", []):
        yield track
        yield from track.get("keyframes", [])
    yield from scene.get("bindings", [])
    for variant in scene.get("variants", []):
        yield variant
        yield from variant.get("cases", [])


def all_declared_ids(value):
    if isinstance(value, dict):
        if isinstance(value.get("id"), str):
            yield value["id"]
        for child in value.values():
            yield from all_declared_ids(child)
    elif isinstance(value, list):
        for child in value:
            yield from all_declared_ids(child)


def short_declaration_ids(scenes, reserved):
    result = {}
    index = 0
    for original in sorted({item["id"] for scene in scenes for item in scene_declarations(scene)}):
        while (candidate := f"cd{index:x}") in reserved:
            index += 1
        result[original] = candidate
        index += 1
    if len(set(result.values())) != len(result) or set(result.values()) & reserved:
        raise ValueError("Compacted declaration IDs collide with preserved identities")
    return result


def scene_nodes(scene):
    def visit(node):
        yield node
        for child in node.get("children", []):
            yield from visit(child)

    yield from visit(scene["root"])
    for template in scene.get("templates", []):
        yield from visit(template["root"])


def preserve_observed_node_id(identity):
    # Native capture observes these source IDs, including READY, Normal pages,
    # PB differences and the final cut-in name. Preserve the entire identity.
    return any(marker in identity for marker in (".reading.", ".arrival.", ".best.", ".cutin.")) or identity.endswith(".current.value")


def short_scene_ids(scenes, reserved):
    nodes = {node["id"] for scene in scenes for node in scene_nodes(scene)}
    identities = nodes | {item["id"] for scene in scenes for key in ("templates", "instances") for item in scene.get(key, [])}
    preserved = {identity for identity in nodes if preserve_observed_node_id(identity)}
    result = {}
    index = 0

    def base36(number):
        value = ""
        while number:
            number, remainder = divmod(number, 36)
            value = "0123456789abcdefghijklmnopqrstuvwxyz"[remainder] + value
        return value or "0"

    for identity in sorted(identities):
        if identity in preserved:
            result[identity] = identity
            continue
        while (candidate := "cz" + base36(index)) in reserved:
            index += 1
        result[identity] = candidate
        index += 1
    renamed = {value for key, value in result.items() if key != value}
    if len(set(result.values())) != len(result) or renamed & reserved or not all(STABLE_ID.fullmatch(value) for value in result.values()):
        raise ValueError("Compacted scene identities collide or are invalid")
    return result, sorted(preserved)


def validate_scene_references(scene, base):
    nodes = {node["id"] for document in (base, scene) for node in scene_nodes(document)}
    templates = {item["id"] for document in (base, scene) for item in document.get("templates", [])}
    for key in ("tracks", "bindings", "variants"):
        for item in scene.get(key, []):
            if item["target"] not in nodes:
                raise ValueError("Dangling scene node reference")
    for machine in scene.get("stateMachines", []):
        for state in machine["states"]:
            for assignment in state.get("set", []):
                if assignment["target"] not in nodes:
                    raise ValueError("Dangling scene node reference")
    for instance in scene.get("instances", []):
        if instance["template"] not in templates:
            raise ValueError("Dangling scene template reference")


def rewrite_scene(source, resources, fonts, animations, declarations, identities=None):
    result = copy.deepcopy(source)
    for item in scene_declarations(result):
        item["id"] = mapped(item["id"], declarations, "scene declaration")

    def value(reference, property_name):
        if property_name == "resource":
            return mapped(reference, resources, "texture")
        if property_name == "bitmap-font":
            return mapped(reference, fonts, "bitmap font")
        return reference

    def node(item):
        if identities is not None:
            item["id"] = mapped(item["id"], identities, "scene identity")
        if "resource" in item:
            item["resource"] = mapped(item["resource"], resources, "texture")
        if "animation" in item:
            item["animation"] = mapped(item["animation"], animations, "animation")
        properties = item.get("properties", {})
        if "bitmap-font" in properties:
            properties["bitmap-font"] = mapped(properties["bitmap-font"], fonts, "bitmap font")
        for child in item.get("children", []):
            node(child)

    node(result["root"])
    for template in result.get("templates", []):
        if identities is not None:
            template["id"] = mapped(template["id"], identities, "scene identity")
        node(template["root"])
    if identities is not None:
        for instance in result.get("instances", []):
            instance["id"] = mapped(instance["id"], identities, "scene identity")
            instance["template"] = mapped(instance["template"], identities, "template")
        for key in ("tracks", "bindings", "variants"):
            for item in result.get(key, []):
                item["target"] = mapped(item["target"], identities, "scene node")
    for variant in result.get("variants", []):
        property_name = variant["property"]
        variant["default"] = value(variant["default"], property_name)
        for case in variant["cases"]:
            field = "resource" if "resource" in case else "value"
            case[field] = value(case[field], property_name)
    for machine in result.get("stateMachines", []):
        for state in machine["states"]:
            for assignment in state.get("set", []):
                if identities is not None:
                    assignment["target"] = mapped(assignment["target"], identities, "scene node")
                assignment["value"] = value(assignment["value"], assignment["property"])
    for track in result.get("tracks", []):
        for keyframe in track.get("keyframes", []):
            keyframe["value"] = value(keyframe["value"], track["property"])
    return result


def catalog_contributions(catalog):
    for module_index, module in enumerate(catalog["modules"]):
        for choice_index, choice in enumerate(module["choices"]):
            path = ["modules", module_index, "choices", choice_index]
            yield choice, path
            for variant_index, variant in enumerate(choice.get("variants", [])):
                yield variant, path + ["variants", variant_index]


def configuration_aliases(source, catalogues, documents, protected):
    references = {}
    for catalog in catalogues.values():
        for contribution, _ in catalog_contributions(catalog):
            for field in ("manifest", "scene", "ini"):
                if contribution.get(field):
                    relative = contribution[field]
                    if relative in references and references[relative] != field:
                        raise ValueError("A catalog configuration has conflicting roles")
                    references[relative] = field
    aliases = {}
    groups = {}
    payloads = {}
    for relative, kind in sorted(references.items()):
        if relative in protected:
            continue
        if relative in documents:
            payload = documents[relative]
        else:
            payload = safe_path(source, relative).read_bytes()
        payloads[relative] = payload
        key = kind, digest(payload)
        if key not in groups:
            groups[key] = relative
        else:
            canonical = groups[key]
            if payloads[canonical] != payload:
                raise ValueError("Configuration digest matched different bytes")
            aliases[relative] = canonical
    return aliases, payloads


def rewrite_catalog(source, paths, aliases):
    result = copy.deepcopy(source)
    edits = []
    for contribution, pointer in catalog_contributions(result):
        for field, mapping in (("manifest", aliases), ("scene", aliases), ("ini", aliases), ("preview", paths)):
            original = contribution.get(field)
            if original in mapping and mapping[original] != original:
                contribution[field] = mapping[original]
                edits.append({"path": pointer + [field], "source": original, "output": mapping[original]})
    restored = copy.deepcopy(result)
    for edit in reversed(edits):
        parent = restored
        for segment in edit["path"][:-1]:
            parent = parent[segment]
        if parent[edit["path"][-1]] != edit["output"]:
            raise ValueError("Catalog reference inverse did not match")
        parent[edit["path"][-1]] = edit["source"]
    if restored != source:
        raise ValueError("Catalog rewrite changed fields beyond recorded references")
    return result, edits


def rewrite_recording(data, compressed, resources, allowed_sets):
    import brotli

    raw = brotli.decompress(data) if compressed else data
    original = json.loads(raw)
    if original.get("contract") != "oms-gameplay-recorded-animation.v1":
        raise ValueError("Unknown recorded-animation contract")
    references = {geometry["resource"] for geometry in original["geometries"] if geometry["resource"] is not None}
    for allowed in allowed_sets:
        if not references <= allowed:
            raise ValueError("A recording contains a texture outside its declared resource table")
    expected = copy.deepcopy(original)
    for geometry in expected["geometries"]:
        if geometry["resource"] is not None:
            geometry["resource"] = mapped(geometry["resource"], resources, "recorded texture")

    # Replace only reference string tokens. All numeric lexemes (vertices, UV, transforms,
    # frames, colours and timing) retain their original decoded bytes.
    changed = RESOURCE_LITERAL.sub(lambda match: match[1] + json.dumps(mapped(json.loads(match[2]), resources, "recorded texture")),
                                   raw.decode("utf-8")).encode("utf-8")
    if json.loads(changed) != expected:
        raise ValueError("Recorded-animation rewrite changed data beyond texture references")
    payload = brotli.compress(changed, quality=5) if compressed else changed
    if (brotli.decompress(payload) if compressed else payload) != changed:
        raise ValueError("Recorded-animation compression did not round-trip")
    return payload, {"decodedSourceSha256": digest(raw), "decodedOutputSha256": digest(changed),
                     "geometries": len(original["geometries"]), "frames": len(original["frames"]),
                     "numericLexemesPreserved": True, "geometryAndTimingUnchanged": True}


def compact_package(source: Path, output: Path, receipt: Path) -> dict:
    source, output, receipt = (Path(path).resolve() for path in (source, output, receipt))
    if not source.is_dir() or output.exists() or receipt.exists():
        raise ValueError("Require an existing source and fresh output/receipt paths")
    if output.is_relative_to(source) or source.is_relative_to(output) or receipt.is_relative_to(source) or receipt.is_relative_to(output):
        raise ValueError("Source, output and private receipt must have separate ownership")
    paths = []
    for path in sorted(source.rglob("*")):
        if path.is_symlink() or getattr(path.lstat(), "st_file_attributes", 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT:
            raise ValueError(f"Package links are not supported: {path}")
        if path.is_file():
            paths.append(path.relative_to(source).as_posix())
    if any(path.casefold() == "skin-pages.json" for path in paths):
        raise ValueError("Page-specific references are outside this gameplay-only compactor")
    if len({path.casefold() for path in paths}) != len(paths):
        raise ValueError("Source file paths collide after package normalization")

    def read(relative):
        return json.loads(safe_path(source, relative).read_text("utf-8-sig"))

    root = read(MANIFEST)
    manifests = {MANIFEST: root}
    scenes = {root.get("scene", SCENE): read(root.get("scene", SCENE))}
    catalogues = {}
    if root.get("contentCatalog"):
        catalog = read(root["contentCatalog"])
        catalogues[root["contentCatalog"]] = catalog
        for module in catalog["modules"]:
            for choice in module["choices"]:
                for contribution in (choice, *choice.get("variants", [])):
                    if contribution.get("manifest"):
                        manifests[contribution["manifest"]] = read(contribution["manifest"])
                    if contribution.get("scene"):
                        scenes[contribution["scene"]] = read(contribution["scene"])
    for manifest in manifests.values():
        if manifest.get("contract") != "oms-gameplay-skin-manifest.v1" or manifest.get("script"):
            raise ValueError("Only declarative V1 gameplay manifests can be compacted")
    if any(scene.get("contract") != "oms-gameplay-skin-scene.v1" for scene in scenes.values()):
        raise ValueError("Unknown gameplay scene contract")
    base_scene = scenes[root.get("scene", SCENE)]
    for scene in scenes.values():
        validate_scene_references(scene, base_scene)

    resource_ids = sorted({item["id"] for manifest in manifests.values() for item in manifest.get("resources", [])})
    font_ids = sorted({item["id"] for manifest in manifests.values() for item in manifest.get("bitmapFonts", [])})
    animation_ids = sorted({item["id"] for manifest in manifests.values() for item in manifest.get("animations", [])})
    resources = {old: f"cr{index:x}" for index, old in enumerate(resource_ids)}
    fonts = {old: f"cf{index:x}" for index, old in enumerate(font_ids)}
    animations = {old: old for old in animation_ids}
    shortened = set(resources.values()) | set(fonts.values())
    if len(shortened) != len(resources) + len(fonts) or shortened & set(animation_ids) or not all(STABLE_ID.fullmatch(item) for item in shortened):
        raise ValueError("Compacted stable IDs collide")
    # Reserve every original identity, including IDs in mutually exclusive branches,
    # and every shortened resource/font identity. Physical layout targets and
    # machine/state/numeric/animation identities remain outside the rename set.
    reserved = shortened | set(all_declared_ids([*manifests.values(), *scenes.values(), *catalogues.values()]))
    declarations = short_declaration_ids(scenes.values(), reserved)
    identities, observed_node_ids = short_scene_ids(scenes.values(), reserved | set(declarations.values()))

    texture_paths = sorted({item["path"] for manifest in manifests.values() for item in manifest.get("resources", [])})
    recordings = {}
    for manifest in manifests.values():
        for animation in manifest.get("animations", []):
            recordings.setdefault(animation["path"], []).append(set(animation["resources"]))
    if set(texture_paths) & set(recordings):
        raise ValueError("A texture and recording cannot share the same file")
    ini_values = set()
    for relative in paths:
        if relative.lower().endswith(".ini"):
            for line in safe_path(source, relative).read_text("utf-8-sig").splitlines():
                if not line.lstrip().startswith((";", "#", "[")) and (match := re.match(r"^[^:=]+[:=]\s*(.*?)\s*$", line)):
                    value = match[1]
                    provide = re.fullmatch(r'resource\s+Provide\s+("(?:[^"\\]|\\.)*")', value)
                    if provide:
                        value = json.loads(provide[1])
                    ini_values.add(value.replace("\\", "/").casefold())
    ini_values.update(instance["material"]["resource"].casefold() for scene in scenes.values()
                      for instance in scene.get("instances", []) if instance.get("material", {}).get("resource"))
    path_map = {}
    normalized_paths = {}
    for index, old in enumerate(texture_paths):
        path = PurePosixPath(old)
        stem = path.with_suffix("").as_posix().casefold()
        animation_stem = re.sub(r"-\d+$", "", stem)
        protected = "markers" in path.parts or old.casefold() in ini_values or stem in ini_values or animation_stem in ini_values
        path_map[old] = normalized_paths.setdefault(old.casefold(), old if protected else f"c/t{index:x}{path.suffix.lower()}")
    for index, old in enumerate(sorted(recordings)):
        path_map[old] = normalized_paths.setdefault(old.casefold(), f"c/a{index:x}" + (".json.br" if old.endswith(".br") else ".json"))
    for old in path_map:
        if not safe_path(source, old).is_file():
            raise ValueError(f"Missing declared asset: {old}")
    destinations = [normalized_paths.get(path.casefold(), path) for path in paths]
    if len({path.casefold() for path in destinations}) != len(paths):
        raise ValueError("Compacted file paths collide")

    documents = {}
    for path, manifest in manifests.items():
        changed = copy.deepcopy(manifest)
        for resource in changed.get("resources", []):
            resource["id"] = resources[resource["id"]]
            resource["path"] = path_map[resource["path"]]
        for font in changed.get("bitmapFonts", []):
            font["id"] = fonts[font["id"]]
            for glyph in font["glyphs"]:
                if glyph.get("resource") is not None:
                    glyph["resource"] = mapped(glyph["resource"], resources, "glyph texture")
        for animation in changed.get("animations", []):
            animation["path"] = path_map[animation["path"]]
            animation["resources"] = [mapped(item, resources, "animation texture") for item in animation["resources"]]
        for option in changed.get("options", []):
            for choice in option.get("choices", []):
                if choice.get("preview") in path_map:
                    choice["preview"] = path_map[choice["preview"]]
        documents[path] = encoded(changed)
    for path, scene in scenes.items():
        changed = rewrite_scene(scene, resources, fonts, animations, declarations, identities)
        # Every typed rename is bijective. No text, geometry, physical target,
        # source clock, state transition or material field is string-replaced.
        if rewrite_scene(changed, {v: k for k, v in resources.items()}, {v: k for k, v in fonts.items()}, animations,
                         {v: k for k, v in declarations.items()}, {v: k for k, v in identities.items()}) != scene:
            raise ValueError("Scene rewrite changed fields beyond the selected reference and declaration identities")
        documents[path] = encoded(changed)
    protected_configurations = {MANIFEST, root.get("scene", SCENE), "skin.ini", *catalogues}
    aliases, configuration_payloads = configuration_aliases(source, catalogues, documents, protected_configurations)
    if set(configuration_payloads) & set(path_map):
        raise ValueError("Catalog configurations cannot also own texture or animation files")
    catalog_edits = {}
    for path, catalog in catalogues.items():
        changed, edits = rewrite_catalog(catalog, path_map, aliases)
        catalog_edits[path] = edits
        documents[path] = encoded(changed)

    documents = {path.casefold(): data for path, data in documents.items()}
    recording_definitions = {}
    for path, allowed in recordings.items():
        item = recording_definitions.setdefault(path.casefold(), {"compressed": path.endswith(".br"), "allowed": []})
        if item["compressed"] != path.endswith(".br"):
            raise ValueError("Case aliases cannot disagree about recording compression")
        item["allowed"].extend(allowed)
    normalized_textures = {path.casefold() for path in texture_paths}
    output.mkdir(parents=True)
    file_receipts = []
    for relative in paths:
        data = safe_path(source, relative).read_bytes()
        output_relative = aliases.get(relative, normalized_paths.get(relative.casefold(), relative))
        destination = safe_path(output, output_relative)
        record = {"source": relative, "output": destination.relative_to(output).as_posix(), "sourceSha256": digest(data)}
        if relative.casefold() in recording_definitions:
            definition = recording_definitions[relative.casefold()]
            payload, verification = rewrite_recording(data, definition["compressed"], resources, definition["allowed"])
            record.update(verification)
        else:
            payload = documents.get(relative.casefold(), data)
        if relative in aliases:
            if payload != configuration_payloads[aliases[relative]]:
                raise ValueError("Shared configuration changed after exact-byte verification")
            record["sharedConfiguration"] = True
        else:
            destination.parent.mkdir(parents=True, exist_ok=True)
            with destination.open("xb") as stream:
                stream.write(payload)
            if destination.read_bytes() != payload:
                raise ValueError(f"Output bytes did not verify: {relative}")
        record.update({"outputSha256": digest(payload), "byteIdentical": payload == data,
                       "sourceBytes": len(data), "outputBytes": len(payload)})
        if relative.casefold() in normalized_textures and payload != data:
            raise ValueError("Compaction changed a source texture")
        file_receipts.append(record)
    for item in file_receipts:
        if item.get("sharedConfiguration") and digest(safe_path(output, item["output"]).read_bytes()) != item["outputSha256"]:
            raise ValueError("Shared configuration destination did not verify")
    configuration = [item for item in file_receipts if PurePosixPath(item["output"]).suffix.lower() in (".ini", ".json")]
    unique_configuration = {}
    for item in configuration:
        previous = unique_configuration.setdefault(item["output"].casefold(), item)
        if previous["outputSha256"] != item["outputSha256"]:
            raise ValueError("Configuration aliases disagree about output bytes")
    configuration_bytes = sum(item["outputBytes"] for item in unique_configuration.values())
    if configuration_bytes > MAX_CONFIGURATION_BYTES:
        raise ValueError(f"Compacted configuration exceeds the32MiB import budget: {configuration_bytes}")
    result = {"contract": "oms-private-package-compaction-receipt.v1", "source": str(source), "output": str(output),
              "resources": resources, "bitmapFonts": fonts, "animationIdsPreserved": animations, "paths": path_map,
              "sceneDeclarationIds": declarations,
              "sceneDeclarationFields": ["tracks.id", "tracks.keyframes.id", "bindings.id", "variants.id", "variants.cases.id"],
              "sceneIdentityIds": identities, "preservedObservedNodeIds": observed_node_ids,
              "sceneIdentityFields": ["nodes.id", "templates.id", "instances.id", "instances.template", "tracks.target",
                                      "bindings.target", "variants.target", "stateMachines.states.set.target"],
              "allOtherSceneFieldsVerifiedByInverseRewrite": True,
              "configurationAliases": aliases, "catalogReferenceEdits": catalog_edits,
              "configurationDeduplication": {"removedFiles": len(aliases), "uniqueConfigurationFiles": len(unique_configuration),
                                             "savedBytes": sum(len(configuration_payloads[path]) for path in aliases),
                                             "exactOutputBytesVerified": True, "catalogInverseVerified": True},
              "configurationBytes": {"source": sum(item["sourceBytes"] for item in configuration), "output": configuration_bytes,
                                     "limit": MAX_CONFIGURATION_BYTES, "remaining": MAX_CONFIGURATION_BYTES - configuration_bytes},
              "files": file_receipts, "texturesByteIdentical": True, "recordedGeometryTimingAndNumericLexemesUnchanged": True,
              "allSceneNodeTemplateAndTargetIdsPreserved": False, "allLayoutTargetIdsPreserved": True,
              "allIniAndMaterialAliasesPreserved": True,
              "inputQualification": "Generated author package; typed identity/reference rewrites and exact catalog configuration sharing only. Observed semantic node IDs, animation/state IDs, INI bytes, material aliases and artwork remain unchanged. Formal CLI and native Player remain required."}
    receipt.parent.mkdir(parents=True, exist_ok=True)
    with receipt.open("xb") as stream:
        stream.write(encoded(result))
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--receipt", type=Path, required=True)
    arguments = parser.parse_args()
    result = compact_package(arguments.source, arguments.output, arguments.receipt)
    print(json.dumps({"files": len(result["files"]), "textures": len(result["resources"]),
                      "fonts": len(result["bitmapFonts"]), "recordings": len(result["animationIdsPreserved"]),
                      "configurationBytes": result["configurationBytes"],
                      "receipt": str(arguments.receipt)}, ensure_ascii=False))
