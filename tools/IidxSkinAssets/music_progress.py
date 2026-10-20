"""Author-only original music cursors on a read-only timing.progress binding.

The reference is an offline source audit, not a declaration of the original host's
progress formula. This module does not open a game installation or import the
Omni packager. It appends to the existing full-canvas information ownership.
"""

from __future__ import annotations

from collections.abc import Iterable, Mapping
import copy
from dataclasses import dataclass
import hashlib
from io import BytesIO
import json
import math
from pathlib import Path
import re
import struct
from types import MappingProxyType
import zlib

from PIL import Image

REFERENCE_CONTRACT = "oms-iidx-music-progress-reference.v1"
RESOURCE_ID = "omni.music-progress.cursor"
RESOURCE_PATH = "art/common/music-progress-cursor.png"
CANVAS = (1920, 1080)
LAYOUTS = ("p1", "p2", "dp")


def _stable_id(value):
    # Public GameplaySkinStableIdentityId.Validate plus the scene codec's 128-character limit.
    segment = r"[a-z](?:[a-z0-9-]*[a-z0-9])?"
    if not isinstance(value, str) or not 1 <= len(value) <= 128 or re.fullmatch(segment + r"(?:\." + segment + r")*", value) is None:
        raise ValueError(f"Generated author ID is not a public stable ID: {value}")
    return value


@dataclass(frozen=True)
class CursorReference:
    id: str
    start: tuple[float, float, float, float]
    end: tuple[float, float, float, float]
    flip_x: bool
    evidence_json: str


@dataclass(frozen=True)
class TextureReference:
    source: Path
    data: bytes
    sha256: str
    filtering: str
    horizontally_symmetric: bool

    def declaration(self):
        return {"id": _stable_id(RESOURCE_ID), "type": "texture", "path": RESOURCE_PATH, "filter": self.filtering}


@dataclass(frozen=True)
class FrameReference:
    id: str
    layouts: Mapping[str, tuple[CursorReference, ...]]
    texture: TextureReference


@dataclass(frozen=True)
class MusicProgressReference:
    source: Path
    sha256: str
    frames: Mapping[str, FrameReference]
    texture: TextureReference


def _json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def _contained(root: Path, relative: str):
    if not isinstance(relative, str) or not relative or "\\" in relative or ":" in relative:
        raise ValueError("An author reference path must be a relative forward-slash path")
    candidate = Path(relative)
    if candidate.is_absolute() or ".." in candidate.parts:
        raise ValueError("An author reference path must stay inside its directory")
    resolved = (root / candidate).resolve()
    if not resolved.is_relative_to(root):
        raise ValueError("An author reference path escapes its directory")
    return resolved


def _independent(path: Path):
    path = Path(path)
    if path.drive.lower() == "d:":
        raise ValueError("The music progress tool only consumes independent author/reference copies")
    resolved = path.resolve(strict=True)
    if resolved.drive.lower() == "d:":
        raise ValueError("The music progress tool only consumes independent author/reference copies")
    return resolved


def _rectangle(value, description):
    if not isinstance(value, list) or len(value) != 4 or any(
        isinstance(number, bool) or not isinstance(number, (int, float)) or abs(number) > CANVAS[0] or not math.isfinite(number) for number in value
    ):
        raise ValueError(f"{description} must contain four finite rectangle numbers")
    x, y, width, height = value
    if x < 0 or y < 0 or width <= 0 or height <= 0 or x + width > CANVAS[0] or y + height > CANVAS[1]:
        raise ValueError(f"{description} is outside its declared canvas")
    return tuple(value)


def _texture(reference_path: Path, declaration):
    if not isinstance(declaration, dict) or declaration.get("width") != 22 or declaration.get("height") != 30:
        raise ValueError("The original music cursor texture must declare 22 by 30 pixels")
    filtering = declaration.get("filter")
    if filtering not in ("nearest", "linear"):
        raise ValueError("The music cursor must declare its actual source filter")
    expected_hash = declaration.get("sha256")
    if not isinstance(expected_hash, str) or not re.fullmatch(r"[0-9a-f]{64}", expected_hash):
        raise ValueError("The original music cursor must have its source SHA256")
    source = _contained(reference_path.parent, declaration.get("path"))
    data = source.read_bytes()
    if hashlib.sha256(data).hexdigest() != expected_hash:
        raise ValueError("The original music cursor PNG changed from its reference SHA256")
    if (len(data) < 33 or data[:16] != b"\x89PNG\r\n\x1a\n\x00\x00\x00\x0dIHDR"
            or struct.unpack(">II", data[16:24]) != (22, 30)
            or zlib.crc32(data[12:29]) != struct.unpack(">I", data[29:33])[0]):
        raise ValueError("The original music cursor PNG header does not match its reference")
    try:
        with Image.open(BytesIO(data)) as original:
            original.verify()
        with Image.open(BytesIO(data)) as original:
            pixels = original.convert("RGBA").tobytes()
    except (OSError, SyntaxError) as error:
        raise ValueError("The original music cursor PNG is not decodable") from error
    rows = [pixels[index:index + 22 * 4] for index in range(0, len(pixels), 22 * 4)]
    mirrored = b"".join(row[index:index + 4] for row in rows for index in range(21 * 4, -1, -4))
    return TextureReference(source, data, expected_hash, filtering, pixels == mirrored)


def load_reference(path: Path, *, expected_frames: Iterable[str]):
    """Validate the complete offline frame/layout/texture reference before author mutation."""
    path = _independent(path)
    data = path.read_bytes()
    document = json.loads(data)
    expected = tuple(expected_frames)
    if not expected or any(not isinstance(frame, str) or not frame for frame in expected) or len(set(expected)) != len(expected):
        raise ValueError("Expected Frame IDs must be distinct non-empty strings")
    if not isinstance(document, dict) or document.get("contract") != REFERENCE_CONTRACT:
        raise ValueError("Unknown music progress reference contract")
    frames = document.get("frames")
    if not isinstance(frames, dict) or set(frames) != set(expected):
        raise ValueError("The music progress reference does not contain the exact expected Frame IDs")
    texture = _texture(path, document.get("texture"))
    prepared = {}
    for frame in expected:
        layouts = frames[frame]
        if not isinstance(layouts, dict) or set(layouts) != set(LAYOUTS):
            raise ValueError(f"Frame {frame} does not contain all three exact information layouts")
        parsed_layouts = {}
        for layout in LAYOUTS:
            item = layouts[layout]
            if not isinstance(item, dict) or item.get("canvas") != list(CANVAS):
                raise ValueError(f"Frame {frame}/{layout} must use the declared 1920 by 1080 canvas")
            cursors = item.get("cursors")
            count = 2 if layout == "dp" else 1
            if not isinstance(cursors, list) or len(cursors) != count:
                raise ValueError(f"Frame {frame}/{layout} must retain all {count} actual cursor occurrences")
            parsed = []
            for index, cursor in enumerate(cursors):
                if not isinstance(cursor, dict) or not isinstance(cursor.get("id"), str) or not cursor["id"].strip():
                    raise ValueError(f"Frame {frame}/{layout}/{index} has no source cursor ID")
                start = _rectangle(cursor.get("start"), f"{frame}/{layout}/{index} start")
                end = _rectangle(cursor.get("end"), f"{frame}/{layout}/{index} end")
                if start[0] != end[0] or start[2:] != end[2:] or end[1] <= start[1]:
                    raise ValueError(f"Frame {frame}/{layout}/{index} is not a fixed-size downward vertical travel")
                if start[3] / (end[1] - start[1]) > 4:
                    raise ValueError(f"Frame {frame}/{layout}/{index} exceeds the public relative-size budget")
                flip_x = cursor.get("flipX")
                if not isinstance(flip_x, bool):
                    raise ValueError(f"Frame {frame}/{layout}/{index} must retain its source flipX state")
                if flip_x and not texture.horizontally_symmetric:
                    raise ValueError("A flipped cursor requires the proven horizontal RGBA symmetry of the original PNG")
                evidence = cursor.get("evidence")
                if not isinstance(evidence, dict) or not evidence:
                    raise ValueError(f"Frame {frame}/{layout}/{index} has no endpoint evidence")
                if (evidence.get("sourceBlend") != 8
                        or evidence.get("sourceMultiply") != {"r": 1, "g": 1, "b": 1, "a": 1}
                        or evidence.get("sourceAdd") != {"r": 0, "g": 0, "b": 0, "a": 0}):
                    raise ValueError(f"Frame {frame}/{layout}/{index} is not the audited untinted additive cursor")
                parsed.append(CursorReference(cursor["id"], start, end, flip_x,
                                              json.dumps(evidence, ensure_ascii=False, separators=(",", ":"))))
            parsed_layouts[layout] = tuple(parsed)
        prepared[frame] = FrameReference(frame, MappingProxyType(parsed_layouts), texture)
    return MusicProgressReference(path, hashlib.sha256(data).hexdigest(), MappingProxyType(prepared), texture)


def _walk(node):
    yield node
    for child in node.get("children", []):
        yield from _walk(child)


def _track(information_id: str, index: int, cursor: CursorReference):
    prefix = f"{information_id}.music-progress.o{index}"
    x, y, width, height = cursor.start
    travel = cursor.end[1] - y
    sprite = {"id": _stable_id(prefix + ".cursor"), "type": "sprite", "target": {"kind": "global"}, "blend": "additive",
              "resource": RESOURCE_ID, "properties": {"x": 0, "y": 0, "width": 1, "height": height / travel},
              "effects": [], "children": []}
    track = {"id": _stable_id(prefix + ".track"), "type": "container", "target": {"kind": "global"}, "blend": "inherit",
             "properties": {"x": x / CANVAS[0], "y": y / CANVAS[1], "width": width / CANVAS[0], "height": travel / CANVAS[1]},
             "effects": [], "children": [sprite]}
    binding = {"id": _stable_id(prefix + ".progress"), "target": sprite["id"], "property": "y", "source": "timing.progress"}
    return track, binding


def inject(document: dict, manifest: dict, frame_reference: FrameReference):
    """Append exact cursor occurrences to one selected information layout, or verify a previous injection."""
    if (document.get("contract") != "oms-gameplay-skin-scene.v1"
            or document.get("root", {}).get("id") != f"omni.frame.{frame_reference.id}.root"
            or manifest.get("contract") != "oms-gameplay-skin-manifest.v1"):
        raise ValueError("The selected scene/manifest does not belong to the expected Frame ID")
    templates = [(template, re.search(r"\.template\.(p1|p2|dp)\.information$", template.get("id", "")))
                 for template in document.get("templates", [])]
    information = [(template["root"], match.group(1)) for template, match in templates if match]
    if len(information) != 1:
        raise ValueError("A selected Frame scene must have one exact information layout")
    owner, layout = information[0]
    if (owner.get("type") != "container" or owner.get("slot") != "decoration" or owner.get("target") != {"kind": "global"}
            or owner.get("properties") != {} or owner.get("effects") != []):
        raise ValueError("The information owner must remain an untransformed full-global decoration Container")
    resource = frame_reference.texture.declaration()
    resources = manifest.get("resources")
    bindings = document.get("bindings")
    if not isinstance(resources, list) or not isinstance(bindings, list) or not isinstance(owner.get("children"), list):
        raise ValueError("The selected Frame is missing its declared resource/binding/child arrays")
    owned_resources = [item for item in resources if item.get("id") == RESOURCE_ID]
    if owned_resources and owned_resources != [resource]:
        raise ValueError("The music progress resource ID conflicts with an existing declaration")
    roots = [document["root"], *(template["root"] for template in document.get("templates", []))]
    existing_nodes = [node for root in roots for node in _walk(root)]
    expected, rows, additions = [], [], []
    for index, cursor in enumerate(frame_reference.layouts[layout]):
        track, binding = _track(owner["id"], index, cursor)
        expected.extend([track["id"], track["children"][0]["id"]])
        matching_tracks = [node for node in existing_nodes if node.get("id") == track["id"]]
        matching_sprites = [node for node in existing_nodes if node.get("id") == track["children"][0]["id"]]
        matching_bindings = [item for item in bindings if item.get("id") == binding["id"]]
        if matching_tracks or matching_sprites or matching_bindings:
            if (matching_tracks != [track] or matching_sprites != [track["children"][0]] or matching_bindings != [binding]
                    or sum(child == track for child in owner["children"]) != 1):
                raise ValueError("The music progress node/binding ID collides with an incomplete or changed cursor")
        else:
            additions.append((track, binding))
        rows.append({"index": index, "sourceId": cursor.id, "track": track["id"], "cursor": track["children"][0]["id"],
                     "start": list(cursor.start), "end": list(cursor.end), "flipX": cursor.flip_x,
                     "flipPreservedByPixelSymmetry": cursor.flip_x and frame_reference.texture.horizontally_symmetric,
                     "evidence": json.loads(cursor.evidence_json)})
    prefix = owner["id"] + ".music-progress."
    if any(node.get("id", "").startswith(prefix) and node["id"] not in expected for node in existing_nodes):
        raise ValueError("The information owner has unexpected music progress occurrences")
    if not owned_resources:
        resources.append(resource)
    for track, binding in additions:
        owner["children"].append(track)
        bindings.append(binding)
    return {"frame": frame_reference.id, "layout": layout, "cursors": rows, "resource": resource,
            "addedCursors": len(additions), "addedResource": not owned_resources,
            "geometry": "static travel Container; child Sprite y=timing.progress; child height is cursorHeight/travelHeight"}


def _check_asset(output: Path, reference: MusicProgressReference):
    original = reference.texture.source.read_bytes()
    if original != reference.texture.data or hashlib.sha256(original).hexdigest() != reference.texture.sha256:
        raise ValueError("The original music cursor PNG changed after reference validation")
    target = _contained(output, RESOURCE_PATH)
    if target.exists() and target.read_bytes() != original:
        raise ValueError("The music cursor destination conflicts with the exact original PNG")
    return target


def prepare_resource(output: Path, reference: MusicProgressReference):
    """Copy the one original PNG byte-for-byte, refusing a source or destination change."""
    output = _independent(output)
    target = _check_asset(output, reference)
    created = not target.exists()
    if created:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(reference.texture.data)
    return {"resource": reference.texture.declaration(), "sha256": reference.texture.sha256, "created": created,
            "originalPngBytesPreserved": True, "horizontalRgbaSymmetry": reference.texture.horizontally_symmetric}


def apply_to_author(output: Path, reference: MusicProgressReference):
    """Plan all C2 Frame pairs first; then update only their JSON plus the single verified PNG."""
    output = _independent(output)
    root_manifest = _json(output / "gameplay-skin.json")
    catalog = _json(_contained(output, root_manifest.get("contentCatalog")))
    if catalog.get("contract") != "oms-gameplay-skin-content.v1":
        raise ValueError("The author has no supported C2 content catalog")
    modules = [module for module in catalog.get("modules", []) if module.get("id") == "frame"]
    if len(modules) != 1:
        raise ValueError("The author must have exactly one Frame content module")
    choices = modules[0].get("choices", [])
    if len(choices) != len(reference.frames) or {choice.get("value") for choice in choices} != set(reference.frames):
        raise ValueError("The author does not contain the exact reference Frame IDs")
    planned, original, pairs, receipts, scene_paths, manifest_paths = {}, {}, {}, [], set(), set()
    covered = {frame: set() for frame in reference.frames}

    def document(relative):
        path = _contained(output, relative)
        if path.suffix != ".json":
            raise ValueError("A Frame contribution must use a contained JSON file")
        if path not in planned:
            original[path] = _json(path)
            planned[path] = copy.deepcopy(original[path])
        return planned[path]

    for choice in choices:
        frame = choice["value"]
        for part in (choice, *choice.get("variants", [])):
            manifest_path, scene_path = part.get("manifest"), part.get("scene")
            key = (_contained(output, manifest_path), _contained(output, scene_path))
            if key in pairs:
                if pairs[key] != frame:
                    raise ValueError("A C2 Frame contribution is shared by conflicting Frame IDs")
                continue
            manifest_paths.add(key[0])
            scene_paths.add(key[1])
            receipt = inject(document(scene_path), document(manifest_path), reference.frames[frame])
            covered[frame].add(receipt["layout"])
            receipts.append(receipt)
            pairs[key] = frame
    if any(layouts != set(LAYOUTS) for layouts in covered.values()):
        raise ValueError("The C2 Frame contributions do not cover every p1/p2/dp information layout")
    _check_asset(output, reference)
    asset = prepare_resource(output, reference)
    changed = []
    for path, value in planned.items():
        if value != original[path]:
            path.write_text(json.dumps(value, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
            changed.append(path.relative_to(output).as_posix())
    return {"reference": str(reference.source), "referenceSha256": reference.sha256,
            "frameCount": len(reference.frames), "contributions": receipts, "changedJson": sorted(changed), "texture": asset,
            "changedScenes": sum(path.relative_to(output).as_posix() in changed for path in scene_paths),
            "changedManifests": sum(path.relative_to(output).as_posix() in changed for path in manifest_paths),
            "endpointPolicy": "Use audited source endpoints and explicit author adaptations exactly; no original host progress formula asserted"}
