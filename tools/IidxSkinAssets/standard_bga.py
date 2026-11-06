"""Source-qualified Classic BGA Frame author module for the independent IIDX26 skin.

The prepare command reads the selected private library and evaluates every
original cover frame. Export consumes that evidence after rechecking its exact
source files. No game art or private source hashes belong in this public tool.
"""
from __future__ import annotations

import argparse
import contextlib
import copy
import hashlib
import io
import json
import math
from pathlib import Path
from urllib.parse import unquote
import xml.etree.ElementTree as ET

from standard_graph import FRAME_IDS, digest, node
from standard_source_catalog import (styles as source_styles, families as source_families,
                                     metadata, presentation_family, centre_translation)

CONTRACT = "oms-standard-bga-source.v1"
CANVAS = (1280, 720)
CENTRES = ("center_frame_sp", "center_frame_wide_1p", "center_frame_wide_2p", "center_frame_2pp",
           "center_frame_dp_1p", "center_frame_dp_2p")
COVERS = {"anime_cover": (800, 720), "anime_cover2": (932, 720)}
COPULA_COVERS = {"anime_cover": (800, 586), "anime_cover2": (938, 720)}


def cover_sizes(family):
    return COPULA_COVERS if family == "s23" else COVERS


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def inside(root, relative):
    path = (root / unquote(relative)).resolve(strict=True)
    if not path.is_relative_to(root) or not path.is_file():
        raise ValueError("BGA source escapes its selected library")
    return path


def source_metadata(library):
    data = read(library / "library.json")
    if data["status"] != "complete" or data["sourceMetadataUnchanged"] is not True:
        raise ValueError("BGA requires a complete verified source library")
    packages = {row["source"]: row for row in data["packages"]}
    selected_frames = {int(Path(source).stem[7:]) for source in packages
                       if Path(source).stem.startswith("gmframe") and Path(source).stem[7:].isdigit()}
    if selected_frames != set(source_styles(data)):
        raise ValueError("BGA requires the independent ten-frame IIDX26 selection")
    required = [f"contents/data/graphic/gmframe{style:02}.ifs" for style in source_styles(data)]
    required += [f"contents/data/graphic/{family}/gameparts.ifs" for family in source_families(data)]
    for source in required:
        if source not in packages:
            raise ValueError("BGA source family is absent: " + source)
        if digest(inside(library, "source-copies/" + source)) != packages[source]["sha256"]:
            raise ValueError("BGA source archive differs from its library inventory")
    return data, required


def cover_frame_state(root, source, *, sizes=COVERS):
    """Keep original vertices/UVs and all ancestor state in the comparison."""
    from build_fullcombo_catalog import world_matrix

    samples = []
    for frame in root["frames"][:-1]:
        matrices, chains, draws = {}, {}, []
        for ni, si in frame["nodes"]:
            original = root["nodes"][ni]
            state = root["states"][si]
            parent = matrices.get(original["parent"], [[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]])
            matrix = world_matrix(state["matrix"], state["origin"], parent)
            matrices[ni] = matrix
            chain = chains.get(original["parent"], []) + [state]
            chains[ni] = chain
            if original["geometry"] is None:
                continue
            geometry = source["geometry"][original["geometry"]]
            if len(geometry["draws"]) != 1:
                raise ValueError("BGA cover requires its single original quad")
            draw = geometry["draws"][0]
            texture = draw.get("unresolvedTexture")
            if texture not in COVERS:
                raise ValueError("BGA cover references an unqualified source texture")
            width, height = sizes[texture]
            if (geometry["vertices"] != [[0, 0, 0], [width, 0, 0], [0, height, 0], [width, height, 0]]
                    or geometry["atlasUV"] != [[0, 0, 0], [1, 0, 0], [0, 1, 0], [1, 1, 0]]
                    or draw["indices"] != [0, 1, 2, 2, 1, 3] or not draw["flags"] & 0x40
                    or any(colour != [1, 1, 1, 1] for colour in geometry["colours"])
                    or draw["sourceColour"] not in (None, [1, 1, 1, 1])):
                raise ValueError("BGA cover source geometry/UV requires a different consumer")
            points = [[p[0] * matrix[0][0] + p[1] * matrix[1][0] + matrix[3][0],
                       p[0] * matrix[0][1] + p[1] * matrix[1][1] + matrix[3][1]] for p in geometry["vertices"]]
            draws.append({"texture": texture, "geometry": geometry["source"], "vertices": geometry["vertices"],
                          "uv": geometry["atlasUV"], "quad": [points[i] for i in (0, 1, 3, 2)], "stateChain": chain})
        samples.append(draws)
    if not samples or len(samples[0]) != 1 or any(sample != samples[0] for sample in samples):
        raise ValueError("BGA cover changes during its original entry/loop; export an original movie instead")
    value = samples[0][0]
    for state in value["stateChain"]:
        if (state["mult"] != [1, 1, 1, 1] or state["add"] != [0, 0, 0, 0] or state["hsl"] != [0, 0, 0]
                or not state["visible"] or state["mask"] is not None or state["filters"] or state["blend"] != 0
                or state["projection"] != 1):
            raise ValueError("BGA cover has source colour/mask state requiring a different consumer")
    return value


def clear_hole(image):
    """Largest all-zero-alpha rectangle containing the source centre pixel.

    Family1 also has small transparent decoration cut-outs below the opening.
    Taking the bounds of all transparent pixels would merge those into the
    BGA viewport. For every possible vertical interval through the centre,
    intersect the horizontal zero-alpha runs through that same centre. This
    exhausts the possible containing rectangles without admitting any painted
    or partially transparent pixel. Equal-area ties prefer width, then top/left.
    """
    alpha = image.getchannel("A")
    width, height = alpha.size
    cx, cy = width // 2, height // 2
    pixels = alpha.tobytes()
    if pixels[cy * width + cx] != 0:
        raise ValueError("BGA cover has no fully transparent centre opening")
    first, last = cy, cy + 1
    while first > 0 and pixels[(first - 1) * width + cx] == 0:
        first -= 1
    while last < height and pixels[last * width + cx] == 0:
        last += 1
    runs = {}
    for y in range(first, last):
        left, right = cx, cx + 1
        offset = y * width
        while left > 0 and pixels[offset + left - 1] == 0:
            left -= 1
        while right < width and pixels[offset + right] == 0:
            right += 1
        runs[y] = left, right
    best, best_key = None, None
    upper_left, upper_right = 0, width
    for top in range(cy, first - 1, -1):
        upper_left, upper_right = max(upper_left, runs[top][0]), min(upper_right, runs[top][1])
        left, right = upper_left, upper_right
        for bottom in range(cy + 1, last + 1):
            left, right = max(left, runs[bottom - 1][0]), min(right, runs[bottom - 1][1])
            key = (right - left) * (bottom - top), right - left, -top, -left
            if best_key is None or key > best_key:
                best, best_key = [left, top, right, bottom], key
    return best


def prepare_bga_geometry(library, bemani_root, image_tools):
    """Re-evaluate the actual library. The returned evidence stays private."""
    from build_fullcombo_catalog import LocalStateRecorder, all_tags, exporter_classes, load_tools
    from standard_components import StandardComponents
    from standard_reference import cabinet_loop_control

    library = Path(library).resolve(strict=True)
    data, required = source_metadata(library)
    # Reuse the exact source loader without making any component output folder.
    source_reader = StandardComponents.__new__(StandardComponents)
    source_reader.library = library
    source_reader.packages = {row["source"].removeprefix("contents/data/graphic/").removesuffix(".ifs"): row
                              for row in data["packages"]}
    source_reader.index = {(row["package"].removeprefix("contents/data/graphic/").removesuffix(".ifs"),
                            Path(row["name"]).stem): row for row in data["textures"]}
    source_reader.package_cache = {}
    source_reader.tools = tools = load_tools(Path(bemani_root).resolve(strict=True), Path(image_tools).resolve(strict=True))
    source_reader.SourceSWF, source_reader.StateRenderer = exporter_classes(tools)
    source_files = {"library.json": digest(library / "library.json")}
    for name in required:
        source_files["source-copies/" + name] = digest(inside(library, "source-copies/" + name))
    rows = []
    for style in source_styles(data):
        package = f"gmframe{style:02}"
        source = source_reader._package(package)
        source_files.update(source["hashes"])
        texture_list = f"packages/contents/data/graphic/{package}/raw/tex/texturelist.xml"
        source_files[texture_list] = digest(inside(library, texture_list))
        for movie_name in CENTRES:
            movie = source["movies"][movie_name]
            named = {(tag.export_source_sprite, tag.object_id, tag.depth): tag.movie_name
                     for tag in all_tags(movie.tags, tools.AP2DefineSpriteTag)
                     if isinstance(tag, tools.AP2PlaceObjectTag) and tag.movie_name}
            cover_names = sorted({name for name in named.values() if name in COVERS})
            row = {"style": style, "movie": movie_name, "frames": len(movie.frames), "fps": movie.fps, "covers": cover_names}
            if not cover_names:
                rows.append(row)
                continue
            expected_frames = 300 if style in (23, 24) else 420
            if len(cover_names) != 1 or len(movie.frames) != expected_frames or movie.fps != 60:
                raise ValueError("Original BGA cover frame count or hook set changed")
            _, flow_tags = cabinet_loop_control(movie)

            class CoverRenderer(source_reader.StateRenderer):
                def _AFPRenderer__place(self, tag, clip, prefix=""):
                    if id(tag) in flow_tags and clip is self._AFPRenderer__root:
                        return None, False
                    return super()._AFPRenderer__place(tag, clip, prefix)

                def render_path(self, *args, **kwargs):
                    def keep_branch(obj, parent_tag, retained=False):
                        retained = retained or named.get((parent_tag, obj.object_id, obj.depth)) in COVERS
                        if isinstance(obj, tools.PlacedClip):
                            children = [kept for child in obj.placed_objects
                                        if (kept := keep_branch(child, obj.source.tag_id, retained)) is not None]
                            if not retained and not children:
                                return None
                            clone = copy.copy(obj)
                            clone.placed_objects = children
                            return clone
                        return copy.copy(obj) if retained else None

                    for image in super().render_path(*args, **kwargs):
                        original = self._AFPRenderer__root
                        self._AFPRenderer__root = keep_branch(original, None)
                        if self._AFPRenderer__root is None:
                            self._AFPRenderer__root = copy.copy(original)
                            self._AFPRenderer__root.placed_objects = []
                        yield image
                        self._AFPRenderer__root = original

            log = io.StringIO()
            with contextlib.redirect_stdout(log), contextlib.redirect_stderr(log):
                root = LocalStateRecorder(tools, CoverRenderer).record(package, movie_name, movie, source["movies"],
                    source["shapes"], source["mapping"], source["filters"])
            if log.getvalue().strip():
                raise ValueError("BGA cover source evaluation produced unsupported diagnostics: " + log.getvalue())
            row.update(cover_frame_state(root, source, sizes=cover_sizes(presentation_family(style))))
            row["constantSourceFrames"] = len(movie.frames)
            rows.append(row)
    textures = []
    for family in source_families(data):
        xml_path = f"packages/contents/data/graphic/{family}/gameparts/raw/tex/texturelist.xml"
        source_files[xml_path] = digest(inside(library, xml_path))
        samplers = {image.attrib["name"]: (atlas.attrib["mag_filter"], atlas.attrib["min_filter"],
                                          atlas.attrib["wrap_s"], atlas.attrib["wrap_t"])
                    for atlas in ET.parse(inside(library, xml_path)).getroot().findall("texture") for image in atlas.findall("image")}
        for name, size in cover_sizes(family).items():
            original = source_reader.index[family + "/gameparts", name]
            source = inside(library, original["sprite"])
            if digest(source) != original["sha256"] or (original["width"], original["height"]) != size:
                raise ValueError("Original BGA cover image differs from its library metadata")
            source_files[original["sprite"]] = original["sha256"]
            with tools.Image.open(source) as image:
                if image.size != size:
                    raise ValueError("Original BGA cover PNG dimensions changed")
                hole = clear_hole(image.convert("RGBA"))
            if (samplers[name][0] not in ("nearest", "linear") or samplers[name][0] != samplers[name][1]
                    or samplers[name][2:] != ("clamp", "clamp")):
                raise ValueError("BGA source sampler requires a different consumer")
            textures.append({"family": family, "name": name, "path": original["sprite"], "sha256": original["sha256"],
                             "size": list(size), "imageRect": original["imageRect"], "uvRect": original["uvRect"],
                             "filter": samplers[name][0], "clearHole": hole,
                             "holeSelection": "Maximum-area all-zero-alpha rectangle containing the original image centre; external transparent decoration cannot enlarge the viewport."})
    return {"contract": CONTRACT, "librarySHA256": digest(library / "library.json"), "canvas": list(CANVAS),
            "sourceFiles": [{"path": path, "sha256": value} for path, value in sorted(source_files.items())],
            "rows": rows, "textures": textures,
            "scope": "Original complete cover entry/loop geometry only. External executable BGA-format and Camera selection are not inferred."}


def validated_geometry(library, source_geometry):
    value = read(source_geometry) if isinstance(source_geometry, (str, Path)) else copy.deepcopy(source_geometry)
    data, required = source_metadata(library)
    if value["contract"] != CONTRACT or value["canvas"] != list(CANVAS) or value["librarySHA256"] != digest(library / "library.json"):
        raise ValueError("BGA geometry evidence belongs to another library")
    declared = {row["path"]: row["sha256"] for row in value["sourceFiles"]}
    required_files = {"library.json", *("source-copies/" + source for source in required),
                      *(f"packages/{source.removesuffix('.ifs')}/raw/tex/texturelist.xml" for source in required)}
    if len(declared) != len(value["sourceFiles"]) or not required_files <= declared.keys():
        raise ValueError("BGA geometry source closure is incomplete")
    for path, expected in declared.items():
        if digest(inside(library, path)) != expected:
            raise ValueError("BGA geometry source bytes changed: " + path)
    rows = {(row["style"], row["movie"]): row for row in value["rows"]}
    if len(rows) != len(value["rows"]) or set(rows) != {(style, movie) for style in source_styles(data) for movie in CENTRES}:
        raise ValueError("BGA geometry requires all ten original cabinet hook sets")
    for (style, movie), row in rows.items():
        raw = f"packages/contents/data/graphic/gmframe{style:02}/raw"
        required_movie_files = {raw + "/afp/" + movie}
        if (library / raw / "afp/bsi" / movie).is_file():
            required_movie_files.add(raw + "/afp/bsi/" + movie)
        if not required_movie_files <= declared.keys():
            raise ValueError("BGA movie is not in the geometry source closure")
        if "_dp_" in movie:
            if row["covers"]:
                raise ValueError("Original DP has no qualified BGA cover hook")
        elif (row.get("constantSourceFrames") != (300 if style in (23, 24) else 420)
              or row["frames"] != (300 if style in (23, 24) else 420) or row["fps"] != 60 or len(row["covers"]) != 1):
            raise ValueError("BGA cover entry/loop is not completely constant")
        else:
            texture = "anime_cover2" if "_wide_" in movie else "anime_cover"
            if (row["covers"] != [texture] or row["texture"] != texture
                    or raw + "/geo/" + row["geometry"] not in declared):
                raise ValueError("BGA cover geometry is not in its original movie source closure")
    textures = {(row["family"], row["name"]): row for row in value["textures"]}
    if (len(textures) != len(value["textures"])
            or set(textures) != {(family, name) for family in source_families(data) for name in COVERS}):
        raise ValueError("BGA geometry requires both exact original cover families")
    for key, row in textures.items():
        originals = [original for original in data["textures"]
                     if original["package"] == f"contents/data/graphic/{key[0]}/gameparts.ifs"
                     and Path(original["name"]).stem == key[1]]
        if len(originals) != 1:
            raise ValueError("BGA cover texture family is absent or ambiguous")
        original = originals[0]
        if (declared.get(row["path"]) != row["sha256"] or row["size"] != list(cover_sizes(key[0])[key[1]])
                or row["path"] != original["sprite"] or row["sha256"] != original["sha256"]
                or row["size"] != [original["width"], original["height"]]
                or row["imageRect"] != original["imageRect"] or row["uvRect"] != original["uvRect"]
                or row["filter"] not in ("nearest", "linear")):
            raise ValueError("BGA texture is not in the geometry source closure")
    return value, rows, textures


def rectangle(value):
    keys = ("left", "top", "right", "bottom")
    if set(value) != set(keys) or any(type(value[key]) not in (int, float) or not math.isfinite(value[key]) for key in keys):
        raise ValueError("BGA aperture requires four finite source-pixel bounds")
    left, top, right, bottom = (value[key] for key in keys)
    if not 0 <= left < right <= CANVAS[0] or not 0 <= top < bottom <= CANVAS[1]:
        raise ValueError("BGA aperture leaves the original canvas")
    return dict(value)


def project_hole(quad, size, hole):
    """The verified source is an axis-aligned quad; retain a negative X axis."""
    if (len(quad) != 4 or any(len(point) != 2 or any(not math.isfinite(v) for v in point) for point in quad)
            or quad[0][1] != quad[1][1] or quad[2][1] != quad[3][1]
            or quad[0][0] != quad[3][0] or quad[1][0] != quad[2][0]):
        raise ValueError("BGA cover needs its original axis-aligned source quad")
    sx, sy = (quad[1][0] - quad[0][0]) / size[0], (quad[3][1] - quad[0][1]) / size[1]
    if abs(sx) != 1 or sy != 1:
        raise ValueError("BGA cover original source scale changed")
    if (len(hole) != 4 or any(type(value) not in (int, float) or not math.isfinite(value) for value in hole)
            or not 0 <= hole[0] < hole[2] <= size[0] or not 0 <= hole[1] < hole[3] <= size[1]):
        raise ValueError("BGA cover hole leaves its original texture")
    x = [quad[0][0] + sx * hole[i] for i in (0, 2)]
    y = [quad[0][1] + sy * hole[i] for i in (1, 3)]
    return {"left": min(x), "top": min(y), "right": max(x), "bottom": max(y)}


def intersect(left, right):
    result = {"left": max(left["left"], right["left"]), "top": max(left["top"], right["top"]),
              "right": min(left["right"], right["right"]), "bottom": min(left["bottom"], right["bottom"])}
    if result["left"] >= result["right"] or result["top"] >= result["bottom"]:
        raise ValueError("BGA original cover hole and cabinet aperture do not intersect")
    return result


def viewport_ini(layout, apertures):
    viewports = "; ".join(",".join(f"{value:.10g}" for value in (box["left"] / 1280, box["top"] / 720,
        (box["right"] - box["left"]) / 1280, (box["bottom"] - box["top"]) / 720)) + ",fit" for box in apertures)
    field = "BgaViewports" if layout == "dp" else "BgaViewportsP2" if layout == "p2" else "BgaViewportsP1"
    return "".join(f"[Bms]\nKeymode: {mode}K\n{field}: {viewports}\n\n" for mode in ((14,) if layout == "dp" else (5, 7)))


def export_bga_module(library, output, contexts, *, source_geometry):
    """Own only BgaViewports INI and a clipped Global decoration contribution.

    Native bga.frame remains suppressed by the caller. Its engine owner clips
    to the new viewport and cannot hold an external border. The Global owner
    renders above BGA; limiting art to the old clear aperture protects lanes
    and the original cabinet. This is an explicit OMS composition adaptation.
    """
    library, output = Path(library).resolve(strict=True), Path(output).resolve()
    if output.drive.casefold() == "c:" or output.is_relative_to(library) or library.is_relative_to(output):
        raise ValueError("BGA output must be a separate non-system author directory")
    evidence, geometry, textures = validated_geometry(library, source_geometry)
    frame_ids = source_styles(metadata(library))
    evidence_digest = hashlib.sha256(json.dumps(evidence, ensure_ascii=False, sort_keys=True,
        separators=(",", ":"), allow_nan=False).encode("utf-8")).hexdigest()
    indexed = {}
    for context in contexts:
        style, layout, wide = context["style"], context["layout"], context["wide"]
        key = style, layout, wide
        if (style not in frame_ids or layout not in ("p1", "p2", "dp") or type(wide) is not bool
                or layout == "dp" and wide or key in indexed):
            raise ValueError("Duplicate or unsupported BGA cabinet context")
        apertures = [rectangle(value) for value in context["apertures"]]
        if len(apertures) != (2 if layout == "dp" else 1):
            raise ValueError("BGA requires the real single/dual aperture count")
        indexed[key] = {**context, "apertures": apertures}
    expected = {(style, layout, wide) for style in frame_ids for layout, wide in
                (("p1", False), ("p2", False), ("p1", True), ("p2", True), ("dp", False))}
    if set(indexed) != expected:
        raise ValueError("BGA requires all ten cabinet/layout/Graph contexts")
    files, receipts, choices = {}, [], []

    def write(relative, data):
        path = (output / relative).resolve()
        if not path.is_relative_to(output):
            raise ValueError("BGA output path escapes its author directory")
        path.parent.mkdir(parents=True, exist_ok=True)
        if path.exists():
            if path.read_bytes() != data:
                raise ValueError("BGA author file already has different bytes: " + relative)
        else:
            path.write_bytes(data)
        files[relative] = {"path": relative, "source": str(path), "sha256": digest(path)}

    def write_json(relative, value):
        write(relative, (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8"))

    for enabled in (False, True):
        choice = "on" if enabled else "off"
        branches = []
        for (style, layout, wide), context in sorted(indexed.items()):
            suffix = "wide" if wide else "base"
            prefix = f"standard.bga.{choice}.f{style:02}.{layout}.{suffix}"
            base = f"standard/bga/{choice}/f{style:02}/{layout}/{suffix}"
            resources, templates, instances, tracks = [], [], [], []
            original = context["apertures"]
            apertures = copy.deepcopy(original)
            receipt = {"frame": f"iidx{style:02}", "layout": layout, "wide": wide, "choice": choice,
                       "originalApertures": original, "sourceLibrarySHA256": evidence["librarySHA256"],
                       "sourceGeometryContentSHA256": evidence_digest}
            if enabled and layout != "dp":
                movie = "center_frame_wide_" + ("2p" if layout == "p2" else "1p") if wide else "center_frame_sp"
                hook = geometry[style, movie]
                family = presentation_family(style)
                texture = textures[family, hook["texture"]]
                dx = centre_translation(style, layout, wide)
                quad = [[x + dx, y] for x, y in hook["quad"]]
                hole = project_hole(quad, texture["size"], texture["clearHole"])
                apertures = [intersect(original[0], hole)]
                source = inside(library, texture["path"])
                relative = f"standard/bga/source/f{family}/{texture['name']}.png"
                write(relative, source.read_bytes())
                resource = f"standard.bga.texture.f{family}." + texture["name"].replace("_", "-")
                resources.append({"id": resource, "type": "texture", "path": relative, "filter": texture["filter"]})
                aperture = original[0]
                aw, ah = aperture["right"] - aperture["left"], aperture["bottom"] - aperture["top"]
                left, top = min(p[0] for p in quad), min(p[1] for p in quad)
                width, height = max(p[0] for p in quad) - left, max(p[1] for p in quad) - top
                art = node(prefix + ".art", "projective-sprite", {"x": (left - aperture["left"]) / aw,
                    "y": (top - aperture["top"]) / ah, "width": width / aw, "height": height / ah}, resource)
                clip = node(prefix + ".aperture", "clip", {"clip-mode": "bounds", "x": aperture["left"] / 1280,
                    "y": aperture["top"] / 720, "width": aw / 1280, "height": ah / 720}, children=[art])
                root = node(prefix + ".cover", properties={"z": 10}, slot="decoration", children=[clip])
                template = prefix + ".template"
                templates.append({"id": template, "root": root})
                instances.append({"id": template + ".instance", "template": template,
                    "material": {"slot": "decoration", "resource": f"markers/frame-{layout}-information"}})
                tracks.append({"id": prefix + ".quad", "type": "frame", "target": art["id"],
                    "property": "projective-quad", "easing": "step", "loop": False,
                    "keyframes": [{"id": prefix + ".quad.fixed", "time": 0,
                        "value": {"vertices": [[(x - left) / width, (y - top) / height, 1] for x, y in quad], "opacity": 1}}]})
                receipt.update(commonFamily=family, sourceMovie=movie, sourceTexture=texture, originalQuad=hook["quad"],
                    translatedQuad=quad, sourceHoleProjection=hole, sourceConstantFrames=hook["constantSourceFrames"],
                    sourceEntryAndLoopPreserved=True, pngBytesPreserved=True,
                    composition="Full original PNG/UV and quad, clipped only to the cabinet's prior fully-clear BGA aperture. This explicit OMS adaptation preserves existing lanes/cabinet and does not claim original caller pixel parity.",
                    layer="Global decoration is in the Overlay stratum above the real native BGA. Local z10 remains behind the existing information/Graph layers. Native bga.frame owner stays suppressed.")
            receipt["viewports"] = apertures
            if layout == "dp":
                receipt["dp"] = "No source cover hook; both choices preserve both existing authored viewports exactly."
            manifest = {"contract": "oms-gameplay-skin-manifest.v1", "scene": "gameplay-skin.scene.json",
                "sceneContract": "oms-gameplay-skin-scene.v1", "eventContract": "oms-gameplay-skin-event.v1",
                "resources": resources, "bitmapFonts": [], "animations": []}
            scene = {"contract": "oms-gameplay-skin-scene.v1", "root": node(prefix + ".root"), "tracks": tracks,
                "stateMachines": [], "bindings": [], "variants": [], "templates": templates, "instances": instances}
            write_json(base + "/manifest.json", manifest)
            write_json(base + "/scene.json", scene)
            write(base + "/viewports.ini", viewport_ini(layout, apertures).encode("utf-8"))
            contribution = {"manifest": base + "/manifest.json", "scene": base + "/scene.json", "ini": base + "/viewports.ini"}
            conditions = {"frame": f"iidx{style:02}"}
            if layout != "dp":
                conditions.update(layout=layout, **{"graph-visible": not wide})
            branches.append((layout == "dp", conditions, contribution))
            receipts.append(receipt)
        # More-specific SP conditions precede each frame's DP fallback.
        branches.sort(key=lambda branch: branch[0])
        default = next(contribution for dp, when, contribution in branches if dp and when["frame"] == "iidx26")
        choices.append({"value": choice, "label": "Classic BGA Frame（旧式外框）" if enabled else "完整画面",
                        **default, "orderedVariants": True,
                        "variants": [{"when": when, **contribution} for _, when, contribution in branches]})
    return {"module": {"id": "bga-frame", "label": "BGA 外框", "rulesets": ["bms"], "default": "off", "choices": choices},
            "files": list(files.values()), "receipts": receipts,
            "sourceBoundary": "Manual author choice. OMS has no original IIDX song-version or Camera state; no format detection or Camera3..6 is claimed."}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--library", type=Path, required=True)
    parser.add_argument("--bemani-root", type=Path, required=True)
    parser.add_argument("--image-tools", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True, help="New private source-geometry JSON, outside the author package")
    args = parser.parse_args()
    output = args.output.resolve()
    library = args.library.resolve(strict=True)
    if output.exists() or output.drive.casefold() == "c:" or output.is_relative_to(library):
        parser.error("Use a new non-system evidence JSON outside the selected library")
    result = prepare_bga_geometry(library, args.bemani_root, args.image_tools)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(output), "movies": len(result["rows"]), "textures": len(result["textures"])}))


if __name__ == "__main__":
    main()
