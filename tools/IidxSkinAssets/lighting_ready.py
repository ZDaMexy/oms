"""Original shared Lightning GET READY on the real preparation-stage clock.

This offline author helper consumes a verified independent library and a bounded
recorded export of the original 1p_ready wrapper. It preserves the initial letter
scramble, original additive glow, and the wrapper's actual fade/slide out. A
single immutable clip is reused at the proven 2P and DP wrapper translations.
No pixels are generated, no source game is opened, and no timing is written.
"""

from __future__ import annotations

import copy
import hashlib
import json
from pathlib import Path

PACKAGE = "contents/data/graphic/1/gameparts.ifs"
ANIMATION = "lr.ready"
CLOCK = "preparation.ready-elapsed"
VISIBLE = "preparation.ready"
DURATION_MS = 1500
CANVAS = (1920, 1080)
LAYOUT_X = {"p1": (75,), "p2": (1413,), "dp": (458, 1028)}
WRAPPERS = {"p1": "1p_ready", "p2": "2p_ready", "dp": "dp_ready"}


def _json(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def _independent(path, *, exists=True):
    path = Path(path)
    if path.drive.casefold() == "d:":
        raise ValueError("GET READY authoring consumes independent source libraries only")
    result = path.resolve(strict=exists)
    if result.drive.casefold() == "d:":
        raise ValueError("GET READY authoring consumes independent source libraries only")
    return result


def _contained(root, relative):
    if not isinstance(relative, str) or not relative or ":" in relative or "\\" in relative:
        raise ValueError("GET READY asset paths must be contained relative paths")
    part = Path(relative)
    if part.is_absolute() or ".." in part.parts:
        raise ValueError("A GET READY asset path escapes its independent root")
    result = (root / part).resolve()
    if not result.is_relative_to(root):
        raise ValueError("A GET READY asset path escapes its independent root")
    return result


def _copy_exact(source, destination):
    data = source.read_bytes()
    if destination.exists():
        if destination.read_bytes() != data:
            raise ValueError("GET READY output conflicts with an original asset")
    else:
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(data)
    return data


def _wrapper_channels(movie):
    """Extract only the original root placement channels, keeping full values.

    The closed source contract has 90 frames, and each root only places or
    updates one or two instances of the same original x_get_ready symbol.
    Internal letters/glow are retained by the recorded source export itself.
    """
    if (movie["fps"] != 60 or len(movie["frames"]) != 90
            or movie["location"] != {"left": 0, "top": 0, "bottom": 1080, "right": 1920}):
        raise ValueError("The GET READY wrapper no longer uses the verified 1080p 90-frame source timeline")
    symbol = movie["exported_tags"]["x_get_ready"]
    channels = {}
    for frame_index, frame in enumerate(movie["frames"]):
        tags = movie["tags"][frame["start_tag_offset"]:frame["start_tag_offset"] + frame["num_tags"]]
        for tag in tags:
            if tag["type"] in ("AP2DefineSpriteTag", "AP2ShapeTag"):
                continue
            if tag["type"] != "AP2PlaceObjectTag":
                raise ValueError("The original GET READY wrapper needs an unrecorded root operation")
            depth = tag["depth"]
            if not tag["update"]:
                if frame_index != 0 or depth in channels or tag["source_tag_id"] != symbol:
                    raise ValueError("A GET READY wrapper no longer contains only the original shared symbol")
                channels[depth] = {"x": tag["transform"]["tx"], "events": []}
            if depth not in channels:
                raise ValueError("A GET READY source update has no original placement")
            values = {key: copy.deepcopy(tag[key]) for key in (
                "transform", "rotation_origin", "projection", "mult_color", "add_color", "hsl_shift", "blend")}
            if values["transform"] is not None:
                values["transform"]["tx"] -= channels[depth]["x"]
            channels[depth]["events"].append([frame_index, values])
    return sorted(channels.values(), key=lambda channel: channel["x"])


def verify_wrappers(library):
    """Prove shared timing and pixels before translating one complete clip."""
    library = _independent(library)
    folder = library / "author-data/contents/data/graphic/1/gameparts/afp"
    movies = {layout: _json(folder / (name + ".json")) for layout, name in WRAPPERS.items()}
    baseline = _wrapper_channels(movies["p1"])[0]["events"]
    result = {}
    for layout, movie in movies.items():
        channels = _wrapper_channels(movie)
        if tuple(channel["x"] for channel in channels) != LAYOUT_X[layout]:
            raise ValueError("Original GET READY horizontal anchors changed")
        if any(channel["events"] != baseline for channel in channels):
            raise ValueError("GET READY wrappers no longer share exact fade/slide timing")
        result[layout] = {"movie": WRAPPERS[layout], "x": list(LAYOUT_X[layout]),
                          "sharedWrapperEvents": len(baseline), "frames": 90, "fps": 60,
                          "sameMotionAndAlpha": True}
    return result


def export_reference(library, output, *, bemani_root, image_tools):
    """Evaluate the complete original wrapper offline, never at gameplay time."""
    library, output = _independent(library), _independent(output, exists=False)
    if output.drive.casefold() == "c:" or output.is_relative_to(library) or library.is_relative_to(output):
        raise ValueError("GET READY exports require independent non-system-disk author output")
    verify_wrappers(library)
    from export_customization_animations import export_clip
    return export_clip(library, PACKAGE, "1p_ready", output, bemani_root=bemani_root, image_tools=image_tools,
                       prefix="lr.reference", loop=False, trigger="gameplay.attach")


def prepare_assets(library, output, reference_export):
    """Copy original PNG bytes and intern the bounded clip under shared short IDs.

    reference_export is the original export.json, not a game installation or a
    package from player storage. The full wrapper, rather than x_get_ready
    alone, is required because the real fade/slide out lives in that wrapper.
    """
    library, output = _independent(library), _independent(output)
    reference_export = _independent(reference_export)
    if output.drive.casefold() == "c:" or output.is_relative_to(library) or library.is_relative_to(output):
        raise ValueError("GET READY author output must be independent of the read-only library")
    reference = _json(reference_export)
    index = _json(library / "library.json")
    if index.get("status") != "complete" or index.get("sourceMetadataUnchanged") is not True:
        raise ValueError("GET READY requires a completed verified independent library")
    source = reference["source"]
    if (Path(source["library"]).resolve() != library or source["package"] != PACKAGE
            or source["movie"] != "1p_ready" or source["fps"] != 60
            or source["sourceFrames"] != 90 or source["durationMs"] != DURATION_MS
            or source["sampleFrame"] is not None or source["stage"] != [0, 0, 1920, 1080]):
        raise ValueError("GET READY requires the complete original 1p_ready wrapper, including its fade out")
    package = next(row for row in index["packages"] if row["source"] == PACKAGE)
    if reference["receipt"]["sourceArchiveSHA256"] != package["sha256"]:
        raise ValueError("GET READY source archive identity differs from its verified library")
    wrappers = verify_wrappers(library)
    files = {row["path"]: row for row in reference["files"]}
    clip_file = files[reference["animations"][0]["path"]]
    clip_source = _independent(clip_file["source"])
    if not clip_source.is_relative_to(reference_export.parent):
        raise ValueError("The GET READY recorded source escapes its export")
    encoded = clip_source.read_bytes()
    if hashlib.sha256(encoded).hexdigest() != clip_file["sha256"] or clip_file["sha256"] != reference["receipt"]["clipSHA256"]:
        raise ValueError("The complete GET READY source clip changed")
    import brotli
    clip = json.loads(brotli.decompress(encoded))
    if (clip["contract"] != "oms-gameplay-recorded-animation.v1" or clip["width"] != 1920 or clip["height"] != 1080
            or clip["fps"] != 60 or len(clip["frames"]) != 91 or clip["frames"][-1]["nodes"] or clip["loop"]):
        raise ValueError("The GET READY recorded graph is not the complete bounded source timeline")
    by_path = {row["sprite"]: row for row in index["textures"] if row["package"] == PACKAGE}
    resources, mapping, originals, seen = [], {}, [], {}
    for resource in reference["resources"]:
        item = files[resource["path"]]
        png = _independent(item["source"])
        if not png.is_relative_to(library):
            raise ValueError("A GET READY texture escapes its verified source library")
        relative_source = png.relative_to(library).as_posix()
        row = by_path[relative_source]
        data = png.read_bytes()
        digest = hashlib.sha256(data).hexdigest()
        sampler = row["sampler"]
        if (digest != item["sha256"] or digest != row["sha256"]
                or sampler["min"] != sampler["mag"] or sampler["mag"] != resource["filter"]):
            raise ValueError("A GET READY PNG or its original sampler changed")
        identifier = "lr.t" + digest[:16]
        relative = "art/lr/" + digest[:16] + ".png"
        declaration = {"id": identifier, "type": "texture", "path": relative, "filter": resource["filter"]}
        if identifier in seen:
            if seen[identifier] != (digest, declaration):
                raise ValueError("A GET READY short content identity conflicts")
        else:
            seen[identifier] = (digest, declaration)
            resources.append(declaration)
            _copy_exact(png, _contained(output, relative))
            originals.append({"source": relative_source, "path": relative, "sha256": digest,
                              "width": row["width"], "height": row["height"], "originalPngBytesPreserved": True})
        mapping[resource["id"]] = identifier
    for geometry in clip["geometries"]:
        if geometry["resource"] is not None:
            geometry["resource"] = mapping[geometry["resource"]]
    compressed = brotli.compress(json.dumps(clip, ensure_ascii=False, separators=(",", ":"), allow_nan=False).encode("utf-8"), quality=5)
    clip_path = "art/lr/ready.json.br"
    destination = _contained(output, clip_path)
    if destination.exists() and destination.read_bytes() != compressed:
        raise ValueError("The GET READY shared clip conflicts with an existing author output")
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(compressed)
    animation = {"id": ANIMATION, "path": clip_path, "resources": sorted(set(mapping.values()))}
    return {"resources": resources, "animations": [animation],
            "receipt": {"contract": "oms-iidx-lighting-ready.v1", "package": PACKAGE, "movie": "1p_ready",
                        "wrappers": wrappers, "sourceFrames": 90, "fps": 60, "durationMs": DURATION_MS,
                        "sourceClipSHA256": clip_file["sha256"], "clipSHA256": hashlib.sha256(compressed).hexdigest(),
                        "sourceTextures": originals, "recordedNodes": len(clip["nodes"]), "recordedStates": len(clip["states"]),
                        "runtimeClipBytes": len(compressed), "decodedTextureBytes": sum(row["width"] * row["height"] * 4 for row in originals),
                        "originalPngBytesPreserved": True, "fullFrameRasters": 0, "runtimeSourceInterpreter": False,
                        "retained": "Original letter scramble, shared letter/glow meshes and additive blend, exact wrapper alpha/slide out"}}


def build(identifier, layout, assets):
    if layout not in LAYOUT_X or len(assets["animations"]) != 1 or assets["animations"][0]["id"] != ANIMATION:
        raise ValueError("GET READY requires one prepared shared clip and an exact p1/p2/dp layout")
    children = []
    for index, left in enumerate(LAYOUT_X[layout]):
        children.append({"id": identifier + ".n" + str(index), "type": "recorded-animation",
                         "target": {"kind": "global"}, "animation": ANIMATION, "clock": CLOCK,
                         "blend": "alpha", "properties": {"x": (left - 75) / 1920, "y": 0, "width": 1, "height": 1},
                         "effects": [], "children": []})
    root = {"id": identifier, "type": "container", "target": {"kind": "global"}, "slot": "decoration",
            "blend": "inherit", "properties": {"visible": False, "z": -35}, "effects": [], "children": children}
    return {"roots": [root], "bindings": [{"id": identifier + ".visible", "target": identifier,
             "property": "visible", "source": VISIBLE}],
            "receipt": {"layout": layout, "clock": CLOCK, "visible": VISIBLE, "originalAnchors": list(LAYOUT_X[layout]),
                        "sharedClipInstances": len(children), "sourceWrapperTimingPreserved": True,
                        "frames": 90, "durationMs": DURATION_MS, "nativeNotesOrInputsDelayedByModule": False}}


def inject(document, manifest, assets, *, layout):
    information = [template for template in document["templates"]
                   if template["id"].endswith(f".template.{layout}.information")]
    if len(information) != 1:
        raise ValueError("GET READY requires one exact selected Frame information owner")
    identifier = information[0]["root"]["id"] + ".ready"
    if any(item["root"]["id"] == identifier for item in document["templates"]):
        raise ValueError("The selected Frame already contains GET READY")
    result = build(identifier, layout, assets)
    root = result["roots"][0]
    template = {"id": identifier + ".template", "root": root}
    document["templates"].append(template)
    document["instances"].append({"id": identifier + ".instance", "template": template["id"],
                                   "material": {"slot": "decoration", "resource": f"markers/frame-{layout}-information"}})
    document["bindings"].extend(result["bindings"])
    for key in ("resources", "animations"):
        existing = {item["id"]: item for item in manifest.get(key, [])}
        for item in assets[key]:
            if item["id"] in existing:
                if existing[item["id"]] != item:
                    raise ValueError("GET READY conflicts with an existing selected resource or clip")
            else:
                manifest.setdefault(key, []).append(copy.deepcopy(item))
    return result["receipt"]
