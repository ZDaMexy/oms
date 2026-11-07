"""Read the frozen, independent OMS author inputs for beatoraja export.

This selects the same content contribution as the OMS catalog. It does not run
OMS scenes or pretend that their bindings are native beatoraja properties.
"""

from __future__ import annotations

from dataclasses import dataclass
import json
from pathlib import Path, PurePosixPath
import re
from typing import Any

import brotli


NAMES = {
    "standard": "Standard Model Skin Omni Until 26",
    "lightning": "Lightning Model Skin Omni Until 33",
}


def source_path(root: Path, relative: str) -> Path:
    """Resolve one declared source file, rejecting paths outside its author."""
    if not isinstance(relative, str) or not relative or "\\" in relative:
        raise ValueError("Expected a non-empty, forward-slash author path")
    part = PurePosixPath(relative)
    if part.is_absolute() or any(p in ("..", ".") or ":" in p for p in part.parts):
        raise ValueError("Unsafe author path")
    path = root.joinpath(*part.parts).resolve(strict=True)
    if not path.is_relative_to(root.resolve()) or not path.is_file():
        raise ValueError("Author file escaped its source root")
    return path


def read_json(path: Path) -> dict[str, Any]:
    data = path.read_bytes()
    if path.suffix == ".br":
        data = brotli.decompress(data)
    result = json.loads(data.decode("utf-8-sig"))
    if not isinstance(result, dict):
        raise ValueError("Expected an author JSON object")
    return result


def applies(definition: dict[str, Any], keymode: str) -> bool:
    return (not definition.get("rulesets") or "bms" in definition["rulesets"]) and (
        not definition.get("keymodes") or keymode in definition["keymodes"])


@dataclass(frozen=True)
class Contribution:
    module: str
    choice: str
    manifest_path: str | None
    scene_path: str | None
    ini_path: str | None
    manifest: dict[str, Any]
    scene: dict[str, Any]
    ini: str

    @property
    def resources(self) -> dict[str, dict[str, Any]]:
        return {item["id"]: item for item in self.manifest.get("resources", [])}

    @property
    def templates(self) -> dict[str, dict[str, Any]]:
        return {item["id"]: item["root"] for item in self.scene.get("templates", [])}


class AuthorPackage:
    def __init__(self, root: Path, family: str):
        self.root = root.resolve(strict=True)
        if family not in NAMES:
            raise ValueError("Unknown independent skin family")
        self.family = family
        self.name = NAMES[family]
        ini = source_path(self.root, "skin.ini").read_text(encoding="utf-8-sig")
        original_name = re.search(r"^Name\s*:\s*(.+)$", ini, re.MULTILINE)
        accepted_names = {self.name}
        if family == "lightning":
            accepted_names.add("Lighting Model Skin Omni Until 33")
        if not original_name or original_name[1].strip() not in accepted_names:
            raise ValueError("Author identity does not match the requested family")
        self.manifest = read_json(source_path(self.root, "gameplay-skin.json"))
        if self.manifest.get("contract") != "oms-gameplay-skin-manifest.v1":
            raise ValueError("Unsupported source manifest contract")
        self.catalog = read_json(source_path(self.root, self.manifest["contentCatalog"]))
        if self.catalog.get("contract") != "oms-gameplay-skin-content.v1":
            raise ValueError("Unsupported source catalog contract")
        self.modules = {item["id"]: item for item in self.catalog["modules"]}
        self._json: dict[str, dict[str, Any]] = {}

    def json(self, relative: str) -> dict[str, Any]:
        if relative not in self._json:
            self._json[relative] = read_json(source_path(self.root, relative))
        return self._json[relative]

    def options(self, keymode: str = "7k", presentation: str = "p1",
                overrides: dict[str, Any] | None = None) -> dict[str, Any]:
        if keymode not in ("5k", "7k", "14k") or presentation not in ("p1", "p2", "dp"):
            raise ValueError("The beatoraja export supports 5K/7K SP and 14K DP")
        if (keymode == "14k") != (presentation == "dp"):
            raise ValueError("Keymode and presentation do not agree")
        definitions = [*self.manifest.get("options", []), *self.modules.values()]
        options = {item["id"]: item["default"] for item in definitions if applies(item, keymode)}
        if keymode in ("5k", "7k"):
            options["layout"] = presentation
        else:
            # OMS omits this SP-only selector for DP. Retaining p1 would select
            # the wrong frame contribution before the generic DP variants.
            options.pop("layout", None)
        for key, value in (overrides or {}).items():
            if key not in options:
                raise ValueError(f"Unavailable author option: {key}")
            options[key] = value
        return options

    def contribution(self, module_id: str, options: dict[str, Any]) -> Contribution:
        module = self.modules[module_id]
        value = options[module_id]
        choice = next((c for c in module["choices"] if c["value"] == value), None)
        if choice is None:
            raise ValueError(f"Unknown choice for {module_id}")
        # Runtime selects FirstOrDefault, including orderedVariants catalogs.
        # A matching variant is a complete contribution, not an overlay of the
        # remaining variants or the base branch.
        branch = next((v for v in choice.get("variants", []) if all(
            k in options and options[k] == expected for k, expected in v["when"].items())), choice)
        return self._contribution(module_id, str(value), branch)

    def _contribution(self, module: str, choice: str, branch: dict[str, Any]) -> Contribution:
        manifest_path = branch.get("manifest")
        scene_path = branch.get("scene")
        ini_path = branch.get("ini")
        return Contribution(module, choice, manifest_path, scene_path, ini_path,
                            self.json(manifest_path) if manifest_path else {},
                            self.json(scene_path) if scene_path else {},
                            source_path(self.root, ini_path).read_text(encoding="utf-8-sig") if ini_path else "")

    def base(self) -> Contribution:
        return self._contribution("base", "base", {
            "manifest": "gameplay-skin.json", "scene": self.manifest["scene"], "ini": "skin.ini"})


def walk_nodes(root: dict[str, Any]):
    yield root
    for child in root.get("children", []):
        yield from walk_nodes(child)


def bms_sections(ini: str) -> dict[str, dict[str, str]]:
    sections: dict[str, dict[str, str]] = {}
    for block in re.split(r"(?=^\[)", ini, flags=re.MULTILINE):
        if not block.startswith("[Bms]\n") and not block.startswith("[Bms]\r\n"):
            continue
        values = {}
        for line in block.splitlines()[1:]:
            if ":" in line and not line.lstrip().startswith(("//", ";")):
                key, value = line.split(":", 1)
                values[key.strip()] = value.strip()
        keymode = values.get("Keymode")
        if keymode:
            sections.setdefault(keymode.lower(), {}).update(values)
    return sections


def layout_for(contribution: Contribution, keymode: str, presentation: str) -> dict[str, Any]:
    matches = [item for item in contribution.manifest.get("layouts", [])
               if item["ruleset"] == "bms" and item["keymode"] == keymode
               and item.get("presentation", "any") in (presentation, "any")]
    exact = [item for item in matches if item.get("presentation") == presentation]
    selected = exact or matches
    if len(selected) != 1:
        raise ValueError("Expected one explicit source layout for this play mode")
    return selected[0]


def material_bindings(contribution: Contribution, keymode: str,
                      presentation: str) -> list[dict[str, Any]]:
    """Read explicit BMS material targets; retain scope and logical identity."""
    result: list[dict[str, Any]] = []
    target: dict[str, str] | None = None
    for line in contribution.ini.splitlines():
        line = line.strip()
        if line.startswith("["):
            target = None
        elif line.startswith("Target:"):
            parts = line[7:].split()
            target = {"kind": parts[0].lower(), **dict(p.split("=", 1) for p in parts[1:])}
            if target.get("ruleset") != "bms" or target.get("keymode") != keymode:
                target = None
            elif target.get("presentation", "any") not in (presentation, "any"):
                target = None
        elif target is not None:
            match = re.fullmatch(r'([\w.-]+): resource (Provide|Suppress)(?: "([^"]+)")?', line)
            if match:
                result.append({"target": target.copy(), "slot": match[1],
                               "mode": match[2], "resource": match[3]})
    return result


def material_templates(contribution: Contribution, slot: str, marker: str) -> list[dict[str, Any]]:
    return [contribution.templates[item["template"]]
            for item in contribution.scene.get("instances", [])
            if item.get("material") == {"slot": slot, "resource": marker}]
