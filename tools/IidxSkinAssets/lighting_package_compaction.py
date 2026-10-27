"""Reversible, typed JSON identity and texture-path compaction for frozen authors.

This module neither decodes media nor edits inputs. Public material selectors,
resource/font/animation identities, options and all sampling data remain literal.
"""
from __future__ import annotations

import copy
import hashlib
import itertools
import json
from pathlib import PurePosixPath
import re

MANIFEST = "oms-gameplay-skin-manifest.v1"
SCENE = "oms-gameplay-skin-scene.v1"
CATALOG = "oms-gameplay-skin-content.v1"
SCENE_FIELDS = {"contract", "root", "templates", "instances", "tracks", "bindings", "variants", "stateMachines"}
STABLE_SEGMENT = r"[a-z](?:[a-z0-9-]*[a-z0-9])?"
STABLE_ID = re.compile(STABLE_SEGMENT + r"(?:\." + STABLE_SEGMENT + r")*")


def require(condition, message):
    if not condition:
        raise ValueError(message)


def json_bytes(value):
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"), allow_nan=False).encode("utf-8")


def canonical_sha(value):
    return hashlib.sha256(json.dumps(value, ensure_ascii=False, separators=(",", ":"),
                                     sort_keys=True, allow_nan=False).encode("utf-8")).hexdigest()


def stable_id(value):
    require(isinstance(value, str) and 1 <= len(value) <= 128 and STABLE_ID.fullmatch(value) is not None,
            "A private identity violates the public stable-ID grammar: " + repr(value))
    return value


def walk(node):
    yield node
    for child in node.get("children", []):
        yield from walk(child)


def identity_slots(document):
    """Return declaration dictionaries and only the public schema's typed references."""
    require(document.get("contract") == SCENE and SCENE_FIELDS <= set(document)
            and not set(document) - SCENE_FIELDS - {"preparationDuration", "completionOutro"},
            "Use a closed public scene document; unknown top-level fields need explicit review")
    nodes = [node for root in [document["root"], *(item["root"] for item in document["templates"])] for node in walk(root)]
    declarations = [*nodes, *(effect for node in nodes for effect in node.get("effects", [])),
                    *document["templates"], *document["instances"]]
    for field in ("tracks", "bindings", "variants", "stateMachines"):
        declarations.extend(document[field])
    declarations.extend(frame for track in document["tracks"] for frame in track["keyframes"])
    declarations.extend(case for variant in document["variants"] for case in variant["cases"])
    for machine in document["stateMachines"]:
        declarations.extend(machine["states"])
        declarations.extend(machine["transitions"])
        declarations.extend(assignment for state in machine["states"] for assignment in state["set"])
    identities = [stable_id(item["id"]) for item in declarations]
    require(len(identities) == len(set(identities)), "A scene has duplicate declaration identities")
    node_ids, template_ids = {item["id"] for item in nodes}, {item["id"] for item in document["templates"]}
    references = []

    def reference(item, field, allowed):
        require(stable_id(item[field]) in allowed, "A typed scene reference is dangling: " + item[field])
        references.append((item, field))

    for instance in document["instances"]:
        reference(instance, "template", template_ids)
    for field in ("tracks", "bindings", "variants"):
        for item in document[field]:
            reference(item, "target", node_ids)
    for machine in document["stateMachines"]:
        states = {state["id"] for state in machine["states"]}
        reference(machine, "initial", states)
        for transition in machine["transitions"]:
            reference(transition, "from", states)
            reference(transition, "to", states)
        for state in machine["states"]:
            for assignment in state["set"]:
                reference(assignment, "target", node_ids)
    return declarations, references


def strings(value):
    if isinstance(value, str):
        yield value
    elif isinstance(value, list):
        for child in value:
            yield from strings(child)
    elif isinstance(value, dict):
        for key, child in value.items():
            yield key
            yield from strings(child)


def external_scene_strings(document):
    """Even an unknown non-typed field can pin a declaration instead of being rewritten."""
    declarations, references = identity_slots(document)
    omitted = {(id(item), "id") for item in declarations} | {(id(item), key) for item, key in references}

    def visit(value):
        if isinstance(value, str):
            yield value
        elif isinstance(value, list):
            for child in value:
                yield from visit(child)
        elif isinstance(value, dict):
            for key, child in value.items():
                yield key
                if (id(value), key) not in omitted:
                    yield from visit(child)
    return set(visit(document))


def short_ids(reserved):
    letters, tails = "abcdefghijklmnopqrstuvwxyz", "abcdefghijklmnopqrstuvwxyz0123456789"
    for length in itertools.count(1):
        for first in letters:
            for tail in itertools.product(tails, repeat=length - 1):
                candidate = first + "".join(tail)
                if candidate not in reserved:
                    yield candidate


def plan_identity_aliases(scenes, manifests, catalog, ini_contents):
    all_ids, external = set(), set(strings(catalog))
    for manifest in manifests.values():
        require(manifest.get("contract") == MANIFEST and "script" not in manifest,
                "Only frozen, script-free public manifests can have private scene IDs compacted")
        external.update(strings(manifest))
    for document in scenes.values():
        declarations, _ = identity_slots(document)
        all_ids.update(item["id"] for item in declarations)
        external.update(external_scene_strings(document))
    # INI material expressions are not scene syntax. Their literal tokens pin IDs.
    external.update(token for text in ini_contents for token in re.findall(r"[a-z][a-z0-9.-]*", text.casefold()))
    frozen = all_ids & external
    candidates = short_ids(all_ids | external)
    candidate = next(candidates)
    mapping = {}
    for identifier in sorted(all_ids - frozen):
        if len(candidate) < len(identifier):
            mapping[identifier] = candidate
            candidate = next(candidates)
    return mapping, {"uniqueDeclarations": len(all_ids), "changedIdentities": len(mapping),
                     "externallyPinnedIdentities": sorted(frozen),
                     "method": "Global injective aliases of typed scene declarations/references only; every old ID and external literal reserved"}


def rewrite_scene(document, mapping):
    require(len(set(mapping.values())) == len(mapping), "The identity mapping is not injective")
    for old, new in mapping.items():
        stable_id(old)
        stable_id(new)
    result = copy.deepcopy(document)
    declarations, references = identity_slots(result)
    require(not {identifier for identifier, alias in mapping.items() if identifier != alias} & external_scene_strings(result),
            "An identity also occurs outside the typed declaration/reference grammar")
    for item in declarations:
        item["id"] = mapping.get(item["id"], item["id"])
    for item, key in references:
        item[key] = mapping.get(item[key], item[key])
    identity_slots(result)
    return result


def normal_path(value):
    require(isinstance(value, str) and bool(value) and "\\" not in value and ":" not in value
            and not value.startswith("/") and all(part not in ("", ".", "..") for part in value.split("/")),
            "Use a canonical, relative package path: " + repr(value))
    return value.casefold()


def parts(catalog):
    require(catalog.get("contract") == CATALOG, "Use the public content catalog")
    for module in catalog["modules"]:
        for choice in module["choices"]:
            for index, part in enumerate((choice, *choice.get("variants", []))):
                require(bool(part.get("scene")) == bool(part.get("manifest")), "A contribution lost its scene/manifest pair")
                yield module["id"], choice["value"], index, part


def protected_media_paths(manifests, scenes, catalog, ini_contents):
    previews = {value for container in [catalog, *manifests.values()] for key, value in _fields(container)
                if key == "preview" and isinstance(value, str)}
    selectors = {instance["material"]["resource"] for scene in scenes.values() for instance in scene["instances"]
                 if isinstance(instance.get("material", {}).get("resource"), str)}
    provides = {value for text in ini_contents for value in re.findall(r'Provide "([^"]+)"', text)}
    return {"previews": sorted(previews), "materialSelectors": sorted(selectors), "iniProvides": sorted(provides)}


def _fields(value):
    if isinstance(value, list):
        for child in value:
            yield from _fields(child)
    elif isinstance(value, dict):
        for key, child in value.items():
            yield key, child
            yield from _fields(child)


def plan_texture_aliases(manifests, scenes, catalog, ini_contents, closure):
    protected = protected_media_paths(manifests, scenes, catalog, ini_contents)
    previews = {normal_path(value) for value in protected["previews"]}
    selectors = {value.replace("\\", "/").casefold() for value in protected["materialSelectors"] + protected["iniProvides"]}
    ini_text = "\n".join(ini_contents).replace("\\", "/").casefold()
    occupied = {normal_path(value) for value in closure}
    require(len(occupied) == len(closure), "The source closure has case-insensitive path collisions")
    textures = sorted({resource["path"] for manifest in manifests.values() for resource in manifest.get("resources", [])})
    mapping, excluded, serial = {}, {}, 0
    for old in textures:
        normalized = normal_path(old)
        stem = str(PurePosixPath(normalized).with_suffix(""))
        require(normalized in occupied, "A texture is outside the source dependency closure")
        reason = ("option-preview" if normalized in previews else
                  "c4-marker-prefix" if normalized.startswith(("markers/", "custom/markers/")) else
                  "c4-material-selector" if normalized in selectors or stem in selectors else
                  "ini-literal" if normalized in ini_text or stem in ini_text else None)
        if reason:
            excluded[old] = reason
            continue
        extension = PurePosixPath(old).suffix
        require(extension.casefold() in {".png", ".webp", ".jpg", ".jpeg"}, "An unreviewed texture extension needs an explicit contract")
        while True:
            new = f"w/{serial}{extension}"
            serial += 1
            if normal_path(new) not in occupied:
                break
        if len(new) >= len(old):
            excluded[old] = "already-short"
            continue
        occupied.add(normal_path(new))
        mapping[old] = new
    require(len(set(mapping.values())) == len(mapping), "Texture aliases must retain a one-to-one physical source relation")
    return mapping, {**protected, "excludedPaths": excluded, "texturePaths": len(textures),
                     "changedPaths": len(mapping), "iniFilesChecked": len(ini_contents)}


def rewrite_manifest(document, mapping):
    require(document.get("contract") == MANIFEST and "script" not in document, "Use a script-free public manifest")
    result = copy.deepcopy(document)
    for resource in result.get("resources", []):
        resource["path"] = mapping.get(resource["path"], resource["path"])
    return result


def rewrite_catalog(catalog, scene_paths, manifest_paths):
    result = copy.deepcopy(catalog)
    for _, _, _, part in parts(result):
        if part.get("scene"):
            part["scene"] = scene_paths[part["scene"]]
            part["manifest"] = manifest_paths[part["manifest"]]
    return result


def restore_catalog(catalog, original, scene_paths, manifest_paths):
    """Restore each ordered contribution, including exact paths if JSON was deduplicated."""
    result = copy.deepcopy(catalog)
    new_parts, old_parts = list(parts(result)), list(parts(original))
    require(len(new_parts) == len(old_parts), "A content contribution was removed")
    for (module, choice, index, part), (old_module, old_choice, old_index, old) in zip(new_parts, old_parts):
        require((module, choice, index) == (old_module, old_choice, old_index), "Contribution order or choice identity changed")
        if old.get("scene"):
            require(part["scene"] == scene_paths[old["scene"]] and part["manifest"] == manifest_paths[old["manifest"]],
                    "A contribution does not follow its declared configuration path mapping")
            part["scene"], part["manifest"] = old["scene"], old["manifest"]
    require(json_bytes(result) == json_bytes(original), "Catalog fields outside configuration paths changed")
    return result


def numeric_reserve(value):
    if type(value) is int:
        return 2
    if isinstance(value, list):
        return sum(numeric_reserve(child) for child in value)
    if isinstance(value, dict):
        return sum(numeric_reserve(child) for child in value.values())
    return 0


def selected_projection(catalog, manifests, scenes, physical_sizes):
    """Metadata-only counterpart of the existing conservative scene/manifest bound.

    Texture/count/codec/runtime validation is deliberately left to the caller's
    current shared authoring gate; no media is decoded by this calculation.
    """
    root, root_scene = manifests["gameplay-skin.json"], scenes["gameplay-skin.scene.json"]
    resources = {item["id"]: item for item in root.get("resources", [])}
    fonts = {item["id"]: item for item in root.get("bitmapFonts", [])}
    modules = []
    for module in catalog["modules"]:
        candidates = []
        for choice in module["choices"]:
            for part in (choice, *choice.get("variants", [])):
                if not part.get("manifest"):
                    continue
                manifest, scene = manifests[part["manifest"]], scenes[part["scene"]]
                exclusive = copy.deepcopy(manifest)
                for field, shared in (("resources", resources), ("bitmapFonts", fonts)):
                    for item in manifest.get(field, []):
                        require(item["id"] not in shared or item == shared[item["id"]], "A shared root definition differs from its contribution")
                    exclusive[field] = [item for item in manifest.get(field, []) if item["id"] not in shared]
                candidates.append({"sceneBytes": physical_sizes[part["scene"]] + numeric_reserve(scene),
                                   "manifestBytes": len(json_bytes(exclusive)) + numeric_reserve(exclusive)})
        modules.append({"module": module["id"], "choices": len(module["choices"]),
                        **{key: max((value[key] for value in candidates), default=0)
                           for key in ("sceneBytes", "manifestBytes")}})
    totals = {key: sum(module[key] for module in modules) for key in ("sceneBytes", "manifestBytes")}
    totals["sceneBytes"] += physical_sizes["gameplay-skin.scene.json"] + numeric_reserve(root_scene)
    totals["manifestBytes"] += physical_sizes["gameplay-skin.json"] + numeric_reserve(root)
    return {"selectedUpperBound": totals, "modules": modules,
            "limits": {"sceneBytes": 524288, "manifestBytes": 65536},
            "status": "metadata-projection-only-current-codec-and-package-audit-pending"}
