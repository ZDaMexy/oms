"""Original Omni first-note cue art on read-only, real-lane arrival metadata.

The source images contain the arrow, note head and vertical caption together.
They are never cropped, recoloured, mirrored, resampled or redrawn here. Source
geometry and timing have not been captured from the original running host;
the three-second lead and top-of-lane placement are explicit author choices.
"""

from __future__ import annotations

import copy
from dataclasses import dataclass
import hashlib
from io import BytesIO
import json
from pathlib import Path
import re
import struct

from lighting_motion import FRAMES, frame_namespace

REFERENCE_CONTRACT = "oms-iidx-lighting-localized-reference.v1"
SCENE_CONTRACT = "oms-gameplay-skin-scene.v1"
MANIFEST_CONTRACT = "oms-gameplay-skin-manifest.v1"
OPTION = {"id": "arrival-cue", "type": "toggle", "label": "显示首次音符到来预告", "default": True}
LEAD_MS = 3000
FADE_OUT_MS = 80
FADE_IN_MS = 500
_ROLES = {"scratch": ("start_notes_s", 90), "white": ("start_notes_w", 52), "black": ("start_notes_b", 40)}
_SHA256 = re.compile(r"[0-9a-f]{64}\Z")


def _require(condition, message):
    if not condition:
        raise ValueError(message)


def _independent(path):
    path = Path(path)
    _require(path.drive.lower() != "d:", "Arrival art must come from an independent source copy")
    resolved = path.resolve(strict=True)
    _require(resolved.drive.lower() != "d:", "Arrival art must come from an independent source copy")
    return resolved


def _contained(root, relative):
    _require(isinstance(relative, str) and relative and "\\" not in relative and ":" not in relative,
             "Arrival art needs a contained relative forward-slash path")
    path = Path(relative)
    _require(not path.is_absolute() and ".." not in path.parts, "Arrival art path escapes its source reference")
    resolved = (root / path).resolve(strict=True)
    _require(resolved.is_relative_to(root), "Arrival art path escapes its source reference")
    return resolved


@dataclass(frozen=True)
class ArrivalTexture:
    role: str
    source_id: str
    source_package: str
    source_entry: str
    data: bytes
    sha256: str
    rgba_sha256: str
    width: int
    height: int

    def declaration(self):
        return {"id": f"omni.arrival.{self.role}", "type": "texture",
                "path": f"art/arrival/{self.role}.png", "filter": "nearest"}


@dataclass(frozen=True)
class ArrivalReference:
    sha256: str
    textures: tuple[ArrivalTexture, ...]
    source_directory: Path | None = None


def load_reference(path):
    """Capture and verify the exact independent PNG bytes before modifying an author package."""
    from PIL import Image

    path = _independent(path)
    raw = path.read_bytes()
    document = json.loads(raw)
    _require(isinstance(document, dict) and document.get("contract") == REFERENCE_CONTRACT
             and document.get("status") == "complete" and document.get("originalHddAccessed") is False,
             "Expected a complete independent localized-art reference")
    rows = document.get("textures")
    _require(isinstance(rows, list) and len(rows) == 3
             and {row.get("id") for row in rows if isinstance(row, dict)} == {value[0] for value in _ROLES.values()},
             "Localized arrival art must contain exactly the three original cue images")
    by_id = {row["id"]: row for row in rows}
    textures = []
    for role, (source_id, width) in _ROLES.items():
        row = by_id[source_id]
        _require(row.get("width") == width and row.get("height") == 270 and row.get("sourceUvPixelsPreserved") is True
                 and row.get("sourcePackage") == "1/gameparts_j.ifs"
                 and row.get("sourceEntry") == f"tex/{source_id}.png"
                 and row.get("sampler") == {"mag": "nearest", "min": "nearest", "wrapS": "clamp", "wrapT": "clamp"},
                 "Arrival texture geometry, UV preservation or original sampler changed")
        png_hash, rgba_hash = row.get("sha256"), row.get("rgbaSha256")
        _require(isinstance(png_hash, str) and _SHA256.fullmatch(png_hash)
                 and isinstance(rgba_hash, str) and _SHA256.fullmatch(rgba_hash), "Arrival image hashes are missing")
        data = _contained(path.parent, row.get("sprite")).read_bytes()
        _require(hashlib.sha256(data).hexdigest() == png_hash, "Arrival PNG differs from the source receipt")
        _require(len(data) >= 33 and data[:16] == b"\x89PNG\r\n\x1a\n\x00\x00\x00\x0dIHDR"
                 and struct.unpack(">II", data[16:24]) == (width, 270), "Arrival PNG header dimensions changed")
        try:
            with Image.open(BytesIO(data)) as image:
                image.verify()
            with Image.open(BytesIO(data)) as image:
                pixels = image.convert("RGBA").tobytes()
        except (OSError, SyntaxError) as error:
            raise ValueError("Arrival source PNG is not decodable") from error
        _require(hashlib.sha256(pixels).hexdigest() == rgba_hash, "Arrival source UV pixels differ from the receipt")
        textures.append(ArrivalTexture(role, source_id, row["sourcePackage"], row["sourceEntry"], data,
                                       png_hash, rgba_hash, width, 270))
    return ArrivalReference(hashlib.sha256(raw).hexdigest(), tuple(textures), path.parent)


def copy_art(reference, author_directory):
    """Copy captured original bytes into a new independent author tree; never overwrite differing files."""
    output = _independent(author_directory)
    _require(output.is_dir() and output.drive.lower() != "c:", "Arrival output must be an independent non-system author directory")
    _require(reference.source_directory is not None and not output.is_relative_to(reference.source_directory)
             and not reference.source_directory.is_relative_to(output), "Arrival author output overlaps its source library")
    targets = []
    for texture in reference.textures:
        target = output / texture.declaration()["path"]
        _require(target.resolve().is_relative_to(output), "Arrival output path escapes its author tree")
        if target.exists():
            _require(target.is_file() and target.read_bytes() == texture.data, "Existing author arrival art differs from the verified source")
        targets.append((texture, target))
    receipts = []
    for texture, target in targets:
        if not target.exists():
            target.parent.mkdir(parents=True, exist_ok=True)
            with target.open("xb") as destination:
                destination.write(texture.data)
        _require(hashlib.sha256(target.read_bytes()).hexdigest() == texture.sha256, "Copied arrival art failed its source hash")
        receipts.append({"role": texture.role, "path": texture.declaration()["path"], "sourceId": texture.source_id,
                         "sourcePackage": texture.source_package, "sourceEntry": texture.source_entry,
                         "sha256": texture.sha256, "rgbaSha256": texture.rgba_sha256, "bytes": len(texture.data),
                         "width": texture.width, "height": texture.height, "byteExact": True})
    return {"sourceReferenceSha256": reference.sha256, "textures": receipts,
            "originalImageAndUvPixelsPreserved": True, "originalHddAccessed": False}


def _append_exact(items, value, description):
    previous = [item for item in items if item.get("id") == value["id"]]
    _require(not previous or previous == [value], f"Existing {description} differs from the arrival cue")
    if not previous:
        items.append(copy.deepcopy(value))


def install_option(root_manifest):
    """Declare the option once at the root authority, rather than in every Frame contribution."""
    _require(root_manifest.get("contract") == MANIFEST_CONTRACT, "Expected the root author manifest")
    _append_exact(root_manifest.setdefault("options", []), OPTION, "arrival option")


def _node(identifier, *, children=None, sprite=False, properties=None, resource=None, slot=None):
    result = {"id": identifier, "type": "sprite" if sprite else "container", "target": {"kind": "global"},
              "blend": "alpha", "properties": properties or {}, "effects": [], "children": children or []}
    if resource is not None:
        result["resource"] = resource
    if slot is not None:
        result["slot"] = slot
    return result


def inject_frame(document, manifest, *, frame_id, reference, layout=None):
    """Add one cue per actual lane through the shared beam material markers.

    The existing lane-surface template remains present. Both it and the cue are
    selected by the same marker, so all beam choices retain their animation.
    C3 only expands templates for real lanes, and each expanded cue reads that
    exact lane's first-note metadata after the active mods have been applied.
    """
    _require(frame_id in FRAMES and document.get("contract") == SCENE_CONTRACT
             and document.get("root", {}).get("id") == f"omni.frame.{frame_id}.root"
             and manifest.get("contract") == MANIFEST_CONTRACT, "Expected one exact Omni Frame contribution")
    namespace = frame_namespace(frame_id)
    detected = [match[1] for template in document.get("templates", [])
                if (match := re.fullmatch(re.escape(namespace) + r"\.template\.(p1|p2|dp)\.information", template.get("id", "")))]
    _require(len(detected) == 1 and (layout is None or layout == detected[0]), "The arrival cue needs one exact selected information layout")
    layout = detected[0]
    _require(len(reference.textures) == 3 and {texture.role for texture in reference.textures} == set(_ROLES),
             "Arrival textures do not cover all original lane colours")
    stage_height = 726 if frame_id in ("iidx27", "iidx28", "iidx29") else 728
    additions = []
    for texture in reference.textures:
        prefix = f"{namespace}.{layout}.arrival.{texture.role}"
        sprite = _node(prefix + ".art", sprite=True, resource=texture.declaration()["id"],
                       properties={"x": 0, "y": 0, "width": 1, "height": texture.height / stage_height, "opacity": 0})
        visible = _node(prefix + ".before-note", properties={"visible": False}, children=[sprite])
        available = _node(prefix + ".available", properties={"visible": False}, children=[visible])
        root = _node(prefix, properties={"visible": False}, children=[available], slot="playfield.lane-surface")
        template = {"id": prefix + ".template", "root": root}
        instance = {"id": prefix + ".instance", "template": template["id"],
                    "material": {"slot": "playfield.lane-surface", "resource": f"custom/markers/b/{texture.role}"}}
        bindings = [{"id": prefix + ".option", "target": prefix, "property": "visible", "source": "option.arrival-cue"},
                    {"id": prefix + ".has-first-note", "target": available["id"], "property": "visible", "source": "arrival.available"},
                    {"id": prefix + ".not-yet-arrived", "target": visible["id"], "property": "visible", "source": "arrival.visible"}]
        track_id = prefix + ".lead"
        track = {"id": track_id, "type": "tween", "target": sprite["id"], "property": "opacity", "clock": "arrival.remaining",
                 "easing": "linear", "loop": False, "keyframes": [
                     {"id": track_id + f".k{index}", "time": time, "value": value}
                     for index, (time, value) in enumerate(((0, 0), (FADE_OUT_MS, 1), (LEAD_MS - FADE_IN_MS, 1), (LEAD_MS, 0)))]}
        additions.extend(((manifest["resources"], texture.declaration(), "arrival resource"),
                          (document["templates"], template, "arrival template"), (document["instances"], instance, "arrival instance"),
                          (document["tracks"], track, "arrival track")))
        additions.extend((document["bindings"], binding, "arrival binding") for binding in bindings)
    # Validate all identity conflicts before mutating either manifest or scene.
    for items, value, description in additions:
        previous = [item for item in items if item.get("id") == value["id"]]
        _require(not previous or previous == [value], f"Existing {description} differs from the arrival cue")
    for items, value, description in additions:
        _append_exact(items, value, description)
    return {"frame": frame_id, "layout": layout, "leadMs": LEAD_MS, "fadeInMs": FADE_IN_MS, "fadeOutMs": FADE_OUT_MS,
            "sourceReferenceSha256": reference.sha256, "sourceImages": [texture.source_id for texture in reference.textures],
            "materialMarkers": [f"custom/markers/b/{texture.role}" for texture in reference.textures],
            "placement": {"x": 0, "y": 0, "width": "one actual lane", "projectedHeightPx": 270,
                          "stageHeightPx": stage_height, "sourceHeadAndCaptionKeptTogether": True},
            "layer": "playfield.lane-surface / Underlay; all existing beam templates retained; notes stay above",
            "truth": "The real lane's arrival.available and arrival.visible, with non-looping arrival.remaining opacity",
            "authorAdaptations": ["Three-second lead and fade envelope", "Top-of-lane placement and per-lane preview"],
            "unknown": "Original host lead time, dynamic travel and exact preview placement"}
