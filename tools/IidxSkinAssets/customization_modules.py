"""Author independently selectable IIDX art modules from a verified local library.

This tool never opens a game installation. Original PNGs are copied byte for
byte; timing and geometry for animated material come from the offline exporter.
The caller owns Frame/Full Combo modules and the final package/catalogue.
"""

import argparse
import copy
import hashlib
import json
from pathlib import Path
import shutil
import sys

sys.dont_write_bytecode = True

from build_skin import node, read_json, stable, within, write_json

SCALE = 768 / 1080
MANIFEST = "oms-gameplay-skin-manifest.v1"
SCENE = "oms-gameplay-skin-scene.v1"
FRAMES = [*[f"iidx{i}" for i in range(27, 34)], "bpl-apina", "bpl-gamepanic", "bpl-gigo",
          "bpl-leisureland", "bpl-round1", "bpl-silkhat", "bpl-supernova-hd", "bpl-tradz"]
SIZES = ("normal", "long", "short", "veryshort")
COLOURS = ("white", "black", "scratch")
COMMON = "[GameplaySkin.Common:1]"


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def frame_sd(frame):
    return frame in {"iidx27", "iidx28", "iidx29"}


def material_contexts():
    """Exactly the logical lane topology used by the existing frame author."""
    for ruleset, modes in (("bms", (5, 7, 14)), ("mania", (5, 6, 7, 8, 14, 16))):
        for mode in modes:
            dual = mode > 8
            count = (8 if ruleset == "bms" else mode // 2) if dual else (mode + 1 if ruleset == "bms" else mode)
            keymode = f"{mode}k" if ruleset == "bms" or not dual else f"{count}k-{count}k"
            for layout in (("dp",) if dual else ("p1", "p2")):
                suffix = f"ruleset={ruleset} keymode={keymode} stage-mode={'dual' if dual else 'single'}"
                if not dual:
                    suffix += f" presentation={layout}"
                for group in range(2 if dual else 1):
                    group_id = f"bms.group.deck-{group + 1}" if ruleset == "bms" else f"mania.group.stage-{group + 1}"
                    stage = f"{suffix} group={group_id} group-logical={group} group-visual={group}"
                    lanes = []
                    right = group == 1 if dual else layout == "p2"
                    source_colours = ["white", "black", "white", "black", "white", "black", "white", "scratch"] if right else [
                        "scratch", "white", "black", "white", "black", "white", "black", "white"]
                    for local in range(count):
                        logical = group * count + local
                        if ruleset == "bms":
                            scratch = local == (count - 1 if right and dual else 0)
                            visual = (count - 1 if scratch else local - 1) if layout == "p2" else local
                            lane = f"bms.lane.scratch-{group + 1}" if scratch else f"bms.lane.key-{logical}"
                            slot = local if dual else ((7 if scratch else local - 1 + (2 if mode == 5 else 0)) if right else local)
                        else:
                            visual = local
                            lane = f"mania.lane.column-{logical + 1}"
                            slot = ((7 - count + local) if count in (5, 7) else (8 - count + local)) if right else (
                                1 + local if count in (5, 7) else local)
                        target = (f"{stage} lane={lane} global-logical={logical} global-visual={group * count + visual} "
                                  f"group-local-logical={local} group-local-visual={visual}")
                        lanes.append({"target": target, "colour": source_colours[slot]})
                    yield {"ruleset": ruleset, "layout": layout, "group": group, "suffix": suffix, "stage": stage, "lanes": lanes}


class Branch:
    def __init__(self, owner, category, choice, suffix="base"):
        self.owner = owner
        self.prefix = stable(f"{category}.{choice}.{suffix}")
        self.base = f"gameplay/custom/{category}/{choice}/{suffix}"
        self.resources, self.fonts, self.animations = {}, [], {}
        self.roots, self.templates, self.instances, self.bindings, self.variants = [], [], [], [], []
        self.tracks, self.machines, self.ini = [], [], []

    def asset(self, package, name, category=None):
        resource, row = self.owner.asset(category or self.prefix.split(".")[0], package, name)
        self.resources[resource["id"]] = resource
        return resource["id"], resource["path"].removesuffix(".png"), row

    def template(self, name, root, slot, material):
        identifier = self.prefix + ".t." + stable(name)
        self.templates.append({"id": identifier, "root": root})
        self.instances.append({"id": identifier + ".i", "template": identifier,
                               "material": {"slot": slot, "resource": material}})

    def bind(self, target, prop, source):
        self.bindings.append({"id": target + ".b." + stable(prop), "target": target, "property": prop, "source": source})

    def variant(self, target, source, default, cases, prop="resource"):
        self.variants.append({"id": target + ".v." + stable(source), "target": target, "property": prop,
                              "source": source, "default": default,
                              "cases": [{"id": target + ".c." + stable(source) + "." + stable(key), "key": key,
                                         "resource" if prop == "resource" else "value": value} for key, value in cases.items()]})

    def show(self, target, source, keys):
        self.variant(target, source, False, {key: True for key in keys}, "visible")

    def lane_ini(self, materials, ruleset=None):
        for context in self.owner.contexts:
            if ruleset is not None and context["ruleset"] != ruleset:
                continue
            for lane in context["lanes"]:
                chosen = materials[lane["colour"]]
                self.ini.extend((COMMON, "Target: Lane " + lane["target"]))
                self.ini.extend(f'{slot}: resource Provide "{path}"' for slot, path in chosen.items())

    def stage_ini(self, slot, material, layouts=None):
        for context in self.owner.contexts:
            if layouts is None or context["layout"] in layouts:
                self.ini.extend((COMMON, "Target: Stage " + context["stage"], f'{slot}: resource Provide "{material}"'))

    def include_clip(self, exported):
        for item in exported["files"]:
            self.owner.copy_file(Path(item["source"]), item["path"], item["sha256"])
        for resource in exported["resources"]:
            self.resources[resource["id"]] = resource
        for animation in exported["animations"]:
            self.animations[animation["id"]] = animation
        return copy.deepcopy(exported["node"])

    def finish(self):
        root = node(self.prefix + ".root", blend="inherit", children=self.roots)
        manifest = {"contract": MANIFEST, "scene": self.base + "/scene.json", "sceneContract": SCENE,
                    "eventContract": "oms-gameplay-skin-event.v1", "resources": list(self.resources.values()),
                    "bitmapFonts": self.fonts, "animations": list(self.animations.values())}
        scene = {"contract": SCENE, "root": root, "tracks": self.tracks, "stateMachines": self.machines,
                 "bindings": self.bindings, "variants": self.variants, "templates": self.templates, "instances": self.instances}
        if len(manifest["resources"]) > 256 or len(self.fonts) > 2:
            raise ValueError(f"Customization contribution exceeds its resource/font budget: {self.prefix}")
        self.owner.json(self.base + "/manifest.json", manifest)
        self.owner.json(self.base + "/scene.json", scene)
        ini = "\n".join(self.ini) + "\n"
        ini_path = "gameplay/custom/materials/m" + hashlib.sha256(ini.encode("utf-8")).hexdigest()[:24] + ".ini"
        self.owner.text(ini_path, ini)
        self.owner.branch_counts.append({"branch": self.base, "resources": len(self.resources),
                                         "fonts": len(self.fonts), "animations": len(self.animations)})
        return {"manifest": self.base + "/manifest.json", "scene": self.base + "/scene.json", "ini": ini_path}


class ModuleBuilder:
    def __init__(self, library, output, bemani_root, image_tools, frame_maps):
        self.library, self.output = library.resolve(strict=True), output.resolve()
        self.bemani_root = bemani_root.resolve(strict=True)
        self.image_tools = image_tools.resolve(strict=True)
        self.frame_maps = frame_maps.resolve(strict=True)
        if self.output.drive.lower() in ("c:", "d:"):
            raise ValueError("Generated modules must remain on the authorised development/media drive")
        data = read_json(self.library / "library.json")
        if data["status"] != "complete" or data["sourceMetadataUnchanged"] is not True:
            raise ValueError("An independently copied and fully verified source library is required")
        self.catalog = read_json(self.library / "customization-catalog.json")
        self.packages = {row["key"]: row for row in self.catalog["packages"]}
        self.index = {(row["package"].removeprefix("contents/data/graphic/").removesuffix(".ifs"),
                       Path(row["name"]).stem): row for row in data["textures"]}
        self.output.mkdir(parents=True, exist_ok=True)
        self.files, self.used_assets, self.receipts, self.branch_counts = {}, {}, [], []
        self.modules, self.options, self.clip_cache = [], [], {}
        self.atlas_cache = {}
        self.contexts = list(material_contexts())

    def copy_file(self, source, relative, expected=None):
        if Path(relative).is_absolute() or ".." in Path(relative).parts:
            raise ValueError("Output path must remain package-relative")
        target = self.output / relative
        expected = expected or digest(source)
        if relative in self.files:
            if self.files[relative]["sha256"] != expected:
                raise ValueError(f"Different content uses the same package path: {relative}")
            return
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.exists():
            if digest(target) != expected:
                raise FileExistsError(f"Refusing to overwrite different package content: {target}")
        else:
            shutil.copyfile(source, target)
        if digest(target) != expected:
            raise ValueError(f"Copied file hash mismatch: {target}")
        self.files[relative] = {"path": relative, "sha256": expected, "bytes": target.stat().st_size}

    def text(self, relative, text):
        target = self.output / relative
        encoded = text.encode("utf-8")
        if target.exists() and target.read_bytes() != encoded:
            raise FileExistsError(f"Refusing to overwrite an existing module: {target}")
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(encoded)
        self.files[relative] = {"path": relative, "sha256": digest(target), "bytes": len(encoded)}

    def json(self, relative, value):
        encoded = json.dumps(value, ensure_ascii=False, separators=(",", ":"), allow_nan=False)
        limit = 64 * 1024 if relative.endswith("manifest.json") else 512 * 1024 if relative.endswith("scene.json") else None
        if limit and len(encoded.encode("utf-8")) > limit:
            raise ValueError(f"Module exceeds structured document budget: {relative}")
        self.text(relative, encoded + "\n")

    def asset(self, category, package, name):
        row = self.index[(package, name)]
        sampler = row["sampler"]
        if sampler["mag"] != sampler["min"] or sampler["mag"] not in ("nearest", "linear") or sampler["wrapS"] != "clamp" or sampler["wrapT"] != "clamp":
            raise ValueError(f"Sampler is outside the current author contract: {package}/{name}")
        source = within(self.library, row["sprite"])
        relative = f"custom/art/{category}/{row['sha256']}.png"
        self.copy_file(source, relative, row["sha256"])
        identifier = f"{category}.a.t{row['sha256'][:24]}"
        resource = {"id": identifier, "type": "texture", "path": relative, "filter": sampler["mag"]}
        self.used_assets[(package, name)] = {"package": package, "texture": name, "source": row["sprite"],
                                           "path": relative, "sha256": row["sha256"], "sampler": sampler,
                                           "width": row["width"], "height": row["height"]}
        return resource, row

    def fallback_marker(self, category, name, package, texture):
        """Keep one real default native fallback while C5 owns each chosen style."""
        row = self.index[(package, texture)]
        path = f"custom/markers/{category}/{name}"
        self.copy_file(within(self.library, row["sprite"]), path + ".png", row["sha256"])
        self.used_assets[("fallback", category, name)] = {"package": package, "texture": texture,
            "source": row["sprite"], "path": path + ".png", "sha256": row["sha256"], "sampler": row["sampler"],
            "width": row["width"], "height": row["height"], "role": "shared-original-default-native-fallback"}
        return path

    def add_module(self, identifier, label, choices, default):
        self.modules.append({"id": identifier, "label": label, "default": default, "choices": choices})

    def judgement_atlas(self, package):
        if package in self.atlas_cache:
            return self.atlas_cache[package]
        sys.path.insert(0, str(self.image_tools))
        from PIL import Image
        rows = [(palette, digit, self.index[(package, f"judge_combo_{digit}{palette}")])
                for palette in ("b", "y") for digit in range(10)]
        width = max(row["width"] for _, _, row in rows)
        height = max(row["height"] for _, _, row in rows)
        image = Image.new("RGBA", (width * 10, height * 2))
        glyphs, source_rows = {}, []
        for palette, digit, row in rows:
            source = within(self.library, row["sprite"])
            if digest(source) != row["sha256"]:
                raise ValueError("Source glyph changed before lossless atlas assembly")
            left, top = digit * width, (0 if palette == "b" else height)
            with Image.open(source) as png:
                rgba = png.convert("RGBA")
                image.paste(rgba, (left, top))
                copied = image.crop((left, top, left + row["width"], top + row["height"]))
                if copied.tobytes() != rgba.tobytes():
                    raise ValueError("Lossless atlas differs from original glyph pixels")
            rect = [left, top, row["width"], row["height"]]
            glyphs[(palette, digit)] = {"rect": rect, "height": row["height"]}
            source_rows.append({"source": row["sprite"], "sourcePNG_SHA256": row["sha256"], "rect": rect,
                                "rgbaSHA256": row["rgbaSha256"], "unchangedPixels": True})
        relative = f"custom/font/{package}-digits.png"
        target = self.output / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.exists():
            with Image.open(target) as existing:
                if existing.convert("RGBA").tobytes() != image.tobytes() or existing.size != image.size:
                    raise FileExistsError("Existing atlas has different original glyph content")
        else:
            image.save(target)
        resource = {"id": "j.atlas." + package, "type": "texture", "path": relative, "filter": "nearest"}
        self.files[relative] = {"path": relative, "sha256": digest(target), "bytes": target.stat().st_size}
        self.receipts.append({"category": "judgement", "kind": "lossless-glyph-atlas", "path": relative,
                              "sourcePackage": package, "sourceGlyphs": source_rows, "resampled": False,
                              "recoloured": False, "trimmed": False, "extruded": False})
        self.atlas_cache[package] = resource, glyphs
        return resource, glyphs

    def sizes(self, identifier, label):
        self.options.append({"id": identifier, "type": "choice", "label": label, "default": "normal",
                             "choices": [{"value": key, "label": value} for key, value in zip(SIZES, ("标准", "长", "短", "极短"))]})

    def clip(self, package, movie, prefix, *, loop, trigger, texture_dependencies=(), sample_frame=None):
        from export_customization_animations import export_clip
        key = (package, movie, prefix, loop, trigger, texture_dependencies, sample_frame)
        if key not in self.clip_cache:
            author_root = self.output.parent / (self.output.name + "-animation-author-data")
            existing = author_root / prefix / "export.json"
            if existing.exists():
                result = read_json(existing)
                if (result["source"]["library"] != str(self.library) or result["source"]["movie"] != movie
                        or result["source"]["package"] != self.packages[package]["source"]
                        or result["source"].get("sampleFrame") != sample_frame
                        or [item["package"] for item in result["source"].get("textureDependencies", [])] != [self.packages[key]["source"] for key in texture_dependencies]):
                    raise ValueError("Cached movie export does not match the verified source selection")
            else:
                result = export_clip(self.library, package, movie, author_root, prefix=prefix, loop=loop, trigger=trigger,
                                     bemani_root=self.bemani_root, image_tools=self.image_tools,
                                     texture_dependencies=texture_dependencies, sample_frame=sample_frame)
            result = self.shared_clip_resources(result, prefix)
            self.clip_cache[key] = result
            self.receipts.append({"category": prefix.split(".")[0], "kind": "original-movie", "source": result["source"],
                                  "receipt": result["receipt"]})
        return self.clip_cache[key]

    def shared_clip_resources(self, exported, prefix):
        """Intern PNG bytes across movies; rewrite only neutral resource identities."""
        import brotli
        result = copy.deepcopy(exported)
        category = prefix.split(".")[0]
        by_path = {item["path"]: item for item in result["files"]}
        remap, resources, files = {}, {}, {}
        for resource in result["resources"]:
            original = by_path[resource["path"]]
            identifier = f"{category}.a.t{original['sha256'][:24]}"
            relative = f"custom/art/{category}/{original['sha256']}.png"
            remap[resource["id"]] = identifier
            declaration = {**resource, "id": identifier, "path": relative}
            if identifier in resources and resources[identifier] != declaration:
                raise ValueError("A shared original PNG requires incompatible sampler declarations")
            resources[identifier] = declaration
            files[relative] = {**original, "path": relative}
        for definition in result["animations"]:
            original = by_path[definition["path"]]
            source = Path(original["source"])
            clip = json.loads(brotli.decompress(source.read_bytes()))
            for geometry in clip["geometries"]:
                if geometry["resource"] is not None:
                    geometry["resource"] = remap[geometry["resource"]]
            atlas_receipt = None
            if category == "l":
                resources, files, atlas_receipt = self.pack_large_lane_clip(clip, resources, files, prefix, source.parent)
            encoded = json.dumps(clip, ensure_ascii=False, separators=(",", ":"), allow_nan=False).encode("utf-8")
            encoded = brotli.compress(encoded, quality=5)
            target = source.with_name("module-clip.json.br")
            if target.exists():
                if target.read_bytes() != encoded:
                    raise FileExistsError("An existing shared-resource clip differs from the exact source graph")
            else:
                target.write_bytes(encoded)
            definition["resources"] = sorted({geometry["resource"] for geometry in clip["geometries"] if geometry["resource"] is not None})
            files[definition["path"]] = {"source": str(target), "path": definition["path"], "sha256": digest(target)}
        result["resources"], result["files"] = list(resources.values()), list(files.values())
        result["receipt"]["moduleResourceInterning"] = {"method": "exact-original-PNG-sha256-and-sampler",
            "clipFieldsChanged": ["geometries[].resource"] + (["geometries[].vertices[].uv"] if atlas_receipt else []),
            "originalXYZFramesColoursUntouched": True, "losslessSourcePixelAtlas": atlas_receipt,
            "runtimeClipSHA256": [item["sha256"] for item in result["files"] if item["path"].endswith(".json.br")]}
        return result

    def pack_large_lane_clip(self, clip, resources, files, prefix, author_dir):
        """Losslessly atlas original nearest/clamp lane movies.

        Source clamp UV must stay inside the explicitly repeated two-texel edge
        border. UV is never clamped/reinterpreted. Source XYZ, state and frame
        tables are untouched; border pixels repeat the original clamp edges.
        """
        from PIL import Image
        if any(resource["filter"] != "nearest" for resource in resources.values()):
            raise ValueError("Large lane atlas requires independently verified nearest sampling")
        items = []
        for identifier, resource in resources.items():
            file = files[resource["path"]]
            with Image.open(file["source"]) as source:
                size = source.size
            items.append({"id": identifier, "file": file, "size": size})
        items.sort(key=lambda item: (-item["size"][1], -item["size"][0], item["id"]))
        padding, maximum = 2, 4096
        source_sizes = {item["id"]: item["size"] for item in items}
        maximum_outside = 0
        for geometry in clip["geometries"]:
            if geometry["resource"] is None:
                continue
            iw, ih = source_sizes[geometry["resource"]]
            for vertex in geometry["vertices"]:
                u, v = vertex[3] * iw, vertex[4] * ih
                outside = max(0, -u, u - iw, -v, v - ih)
                maximum_outside = max(maximum_outside, outside)
                if outside > padding:
                    raise ValueError("Actual source clamp UV exceeds the explicitly repeated edge texels")
        pages = [[]]
        x = y = row_height = 0
        for item in items:
            width, height = (dimension + padding * 2 for dimension in item["size"])
            if width > maximum or height > maximum:
                raise ValueError("An original lane texture does not fit a bounded lossless atlas page")
            if x + width > maximum:
                y += row_height
                x = row_height = 0
            if y + height > maximum:
                pages.append([])
                x = y = row_height = 0
            item["left"], item["top"] = x + padding, y + padding
            pages[-1].append(item)
            x += width
            row_height = max(row_height, height)
        new_resources, new_files, mapping, receipts = {}, {}, {}, []
        for page_index, page in enumerate(pages):
            width = max(item["left"] + item["size"][0] + padding for item in page)
            height = max(item["top"] + item["size"][1] + padding for item in page)
            if width * height > 16 * 1024 * 1024:
                raise ValueError("Lossless atlas exceeds the unchanged single-texture pixel budget")
            canvas = Image.new("RGBA", (width, height))
            atlas_id = prefix + ".atlas.p" + str(page_index)
            relative = f"custom/atlas/{prefix}-p{page_index}.png"
            page_receipts = []
            for item in page:
                left, top = item["left"], item["top"]
                iw, ih = item["size"]
                with Image.open(item["file"]["source"]) as source:
                    image = source.convert("RGBA")
                    canvas.paste(image, (left, top))
                    for offset in range(1, padding + 1):
                        canvas.paste(image.crop((0, 0, 1, ih)), (left - offset, top))
                        canvas.paste(image.crop((iw - 1, 0, iw, ih)), (left + iw - 1 + offset, top))
                        canvas.paste(image.crop((0, 0, iw, 1)), (left, top - offset))
                        canvas.paste(image.crop((0, ih - 1, iw, ih)), (left, top + ih - 1 + offset))
                    for dx in range(1, padding + 1):
                        for dy in range(1, padding + 1):
                            for sx, sy, px, py in ((0, 0, left - dx, top - dy),
                                                  (iw - 1, 0, left + iw - 1 + dx, top - dy),
                                                  (0, ih - 1, left - dx, top + ih - 1 + dy),
                                                  (iw - 1, ih - 1, left + iw - 1 + dx, top + ih - 1 + dy)):
                                canvas.putpixel((px, py), image.getpixel((sx, sy)))
                    cropped = canvas.crop((left, top, left + iw, top + ih))
                    if cropped.tobytes() != image.tobytes():
                        raise ValueError("Lane atlas changed an original source pixel")
                    rgba_hash = hashlib.sha256(image.tobytes()).hexdigest()
                mapping[item["id"]] = (atlas_id, left, top, iw, ih, width, height)
                page_receipts.append({"originalResource": item["id"], "sourcePNG": item["file"]["source"],
                                      "sourceSHA256": item["file"]["sha256"], "rect": [left, top, iw, ih],
                                      "rgbaSHA256": rgba_hash, "unchangedPixels": True})
            target = author_dir / f"module-atlas-p{page_index}.png"
            if target.exists():
                with Image.open(target) as previous:
                    if previous.size != canvas.size or previous.convert("RGBA").tobytes() != canvas.tobytes():
                        raise FileExistsError("Existing original-pixel atlas differs")
            else:
                canvas.save(target)
            new_resources[atlas_id] = {"id": atlas_id, "type": "texture", "path": relative, "filter": "nearest"}
            new_files[relative] = {"source": str(target), "path": relative, "sha256": digest(target)}
            receipts.append({"resource": atlas_id, "path": relative, "size": [width, height], "sources": page_receipts})
        vertices = 0
        for geometry in clip["geometries"]:
            if geometry["resource"] is None:
                continue
            identifier, left, top, iw, ih, width, height = mapping[geometry["resource"]]
            geometry["resource"] = identifier
            for vertex in geometry["vertices"]:
                vertex[3] = (left + vertex[3] * iw) / width
                vertex[4] = (top + vertex[4] * ih) / height
                vertices += 1
        return new_resources, new_files, {"pages": receipts, "padding": padding,
            "paddingMethod": "Exact source clamp-edge repetition, no resampling/trimming/rotation/recolouring",
            "uvVerticesRebased": vertices, "sourceUVWithinRepeatedClampPixels": True,
            "maximumOriginalOutsideUVTexels": maximum_outside, "sourceSampler": "nearest/clamp"}

    def notes(self):
        self.sizes("note-size", "音符尺寸")
        choices = []
        for style in range(12):
            contributions = {}
            for size in SIZES:
                package = f"notes{style:02}" + ("_" + size if size != "normal" and style != 11 else "")
                b = Branch(self, "n", f"n{style:02}", size)
                materials = {}
                for colour in COLOURS:
                    texture = "notes_turn" if colour == "scratch" else "notes_" + colour
                    resource, _, row = b.asset(package, texture)
                    path = self.fallback_marker("n", colour, "notes00", texture)
                    authored_scale = {"normal": 1, "long": 1.25, "short": .75, "veryshort": .5}[size] if style == 11 else 1
                    root = node(b.prefix + "." + colour, "sprite", {
                        "height": row["height"] * authored_scale * SCALE / 22.5,
                        "y": -5 * SCALE / 22.5, "anchor": "bottom-left", "origin": "bottom-left"}, resource, "object.note")
                    b.template(colour, root, "object.note", path)
                    materials[colour] = {"object.note": path}
                b.lane_ini(materials)
                contributions[size] = b.finish()
                self.receipts.append({"category": "notes", "style": style, "size": size, "sourcePackage": package,
                                      "sizeMethod": "author-geometry-scale" if style == 11 and size != "normal" else "original-discrete-PNG",
                                      "authorScale": authored_scale, "pixelsResampled": False})
            choices.append({"value": f"n{style:02}", "label": f"音符 {style:02}", **contributions["normal"],
                            "variants": [{"when": {"note-size": size}, **contributions[size]} for size in SIZES[1:]]})
        self.add_module("notes", "Notes · 音符", choices, "n00")

    def charge_notes(self):
        self.sizes("charge-size", "长音符尺寸")
        self.options.append({"id": "hcn-color", "type": "choice", "label": "HCN 配色", "default": "source",
            "choices": [{"value": "source", "label": "原 HCN 素材配色"},
                        {"value": "cn-palette", "label": "作者适配：沿用同款 CN 配色"}]})
        choices = []
        for style in range(11):
            contributions = {}
            for size, palette in ((size, palette) for size in SIZES for palette in ("source", "cn-palette")):
                suffix = "_" + size if size != "normal" else ""
                cn, hcn = f"notes_cn{style:02}{suffix}", f"notes_hcn{style:02}{suffix}"
                if palette == "cn-palette":
                    hcn = cn
                b = Branch(self, "c", f"c{style:02}", size + "." + palette)
                materials = {}
                for colour in COLOURS:
                    values = {}
                    stem = "notes_bss" if colour == "scratch" else "notes_cn_" + colour
                    released = "notes_bss2_beam4" if colour == "scratch" else "notes_cn2_" + colour + "_beam4"
                    for part, ending in (("head", "start"), ("body", "beam0"), ("tail", "end")):
                        texture = stem + "_" + ending
                        resource, _, row = b.asset(cn, texture)
                        path = self.fallback_marker("c", colour + "-" + part, "notes_cn00", texture)
                        hcn_resource = b.asset(hcn, texture)[0]
                        identifier = b.prefix + "." + colour + "." + part
                        properties = {} if part == "body" else {"height": row["height"] * SCALE / 22.5,
                            "y": -5 * SCALE / 22.5, "anchor": "bottom-left", "origin": "bottom-left"}
                        b.template(colour + "." + part, node(identifier, "sprite", properties, resource,
                                   "object.long-note." + part), "object.long-note." + part, path)
                        if part == "body":
                            cn_active = b.asset(cn, stem + "_beam1")[0]
                            cn_released = b.asset(cn, released)[0]
                            b.variant(identifier, "object.long-note-appearance", resource, {
                                "ln": resource, "ln-holding": cn_active, "ln-released": cn_released,
                                "cn": resource, "cn-holding": cn_active, "cn-released": cn_released,
                                "hcn": hcn_resource, "hcn-holding": b.asset(hcn, stem + "_beam1")[0],
                                "hcn-released": b.asset(hcn, released)[0]})
                        else:
                            b.variant(identifier, "object.long-note-mode", resource, {"ln": resource, "cn": resource, "hcn": hcn_resource})
                        values["object.long-note." + part] = path
                    materials[colour] = values
                b.lane_ini(materials)
                contributions[(size, palette)] = b.finish()
                self.receipts.append({"category": "charge-notes", "style": style, "size": size, "sourcePackages": sorted({cn, hcn}),
                                      "sizeMethod": "original-discrete-PNG", "bodyState": "actual object.long-note-appearance",
                                      "hcnColor": palette,
                                      "colourControlScope": "source preserves the original HCN package. cn-palette is an explicitly authored same-style CN-material substitution for actual HCN states; no official UI color1/2 mapping is asserted.",
                                      "pixelsRecolouredOrResampled": False})
            choices.append({"value": f"c{style:02}", "label": f"长音符 {style:02}", **contributions[("normal", "source")],
                "variants": [{"when": {"charge-size": size, "hcn-color": palette}, **contributions[(size, palette)]}
                             for size in SIZES for palette in ("source", "cn-palette") if (size, palette) != ("normal", "source")]})
        self.add_module("charge-notes", "Charge Notes · 长音符", choices, "c00")

    def judgements(self):
        self.options.append({"id": "judgement-display", "type": "choice", "label": "判定显示", "default": "normal",
                             "choices": [{"value": "normal", "label": "判定与连击"}, {"value": "label-only", "label": "仅判定"}]})
        choices = []
        for style in range(21):
            contributions = {}
            for sd in (False, True):
                b = Branch(self, "j", f"j{style:02}", "sd" if sd else "hd")
                package = f"judge{style:02}"
                atlas, atlas_glyphs = self.judgement_atlas(package)
                b.resources[atlas["id"]] = atlas
                fonts = {}
                for suffix in ("b", "y"):
                    identifier = b.prefix + ".font." + suffix
                    glyphs = []
                    for digit in range(10):
                        glyph = atlas_glyphs[(suffix, digit)]
                        glyphs.append({"character": str(digit), "resource": atlas["id"], "rect": glyph["rect"],
                                       "advance": 55 * glyph["height"] / 100})
                    b.fonts.append({"id": identifier, "lineHeight": glyph["height"], "glyphs": glyphs})
                    fonts[suffix] = identifier
                for layout in ("p1", "p2", "dp"):
                    height, width = (726, 438) if sd else (728, 440)
                    native_y, slot_height = height * .81, height * .12
                    dx = {"p1": -4, "p2": -8, "dp": 0}[layout]
                    dy = 0 if layout == "dp" else 565 - native_y
                    material = self.fallback_marker("j", layout, "judge00", "judge_p_great_blue")
                    root_id = b.prefix + "." + layout
                    children = []
                    for group, palette, grades in (("perfect", "b", ("perfect",)), ("other", "y", ("great", "good", "meh", "miss", "ok"))):
                        row_id = root_id + "." + group
                        label_id, combo_id = row_id + ".label", row_id + ".combo"
                        initial = b.asset(package, "judge_p_great_blue" if group == "perfect" else "judge_great_yellow")[0]
                        label = node(label_id, "sprite", {"height-px": 100 * SCALE}, initial)
                        combo = node(combo_id, "text", {"text": "", "font-size": 100 * SCALE,
                                     "bitmap-font": fonts[palette], "format": "integer", "colour": "#ffffffff"})
                        b.bind(combo_id, "text", "combo.value")
                        b.show(combo_id, "option.judgement-display", ["normal"])
                        row_node = node(row_id, properties={"x": .5 + dx / width, "y": .5 + dy / slot_height,
                            "origin": "centre", "layout": "horizontal", "gap-px": -16 * SCALE, "visible": False}, children=[label, combo])
                        b.show(row_id, "judgement.result", grades)
                        if group == "other":
                            b.variant(label_id, "judgement.result", initial, {
                                key: b.asset(package, name)[0] for key, name in (("great", "judge_great_yellow"),
                                ("good", "judge_good"), ("meh", "judge_bad"), ("miss", "judge_poor"), ("ok", "judge_poor"))})
                        children.append(row_node)
                    root = node(root_id, properties={"opacity": 0}, slot="hud.judgement", children=children)
                    b.template(layout, root, "hud.judgement", material)
                    b.stage_ini("hud.judgement", material, [layout])
                    b.machines.append({"id": root_id + ".m", "initial": root_id + ".empty",
                        "states": [{"id": root_id + ".empty", "set": [{"id": root_id + ".hide", "target": root_id, "property": "opacity", "value": 0}]},
                                   {"id": root_id + ".active", "set": [{"id": root_id + ".show", "target": root_id, "property": "opacity", "value": 1}]}],
                        "transitions": [{"id": root_id + ".arrive", "from": root_id + ".empty", "to": root_id + ".active", "event": "judgement.hit"}]})
                contributions[sd] = b.finish()
            choices.append({"value": f"j{style:02}", "label": f"判定文字 {style:02}", **contributions[False],
                            "variants": [{"when": {"frame": frame}, **contributions[frame_sd(frame)]} for frame in FRAMES]})
        self.add_module("judgement", "Judgment Characters · 判定文字", choices, "j00")
        self.receipts.append({"category": "judgement", "sourceStyleCount": 21, "fontCountPerBranch": 2,
            "originalDefaultEvidence": "33 observed judge00 blue GREAT/combo, not proof of original host factory setting",
            "calibration": "judge00 uses measured100px row/55px digit advance/-16px gap and P1/P2 anchors; other styles are explicitly authored100px normalized rows, not executable-derived metrics.",
            "unknown": ["Original PGREAT colour cadence", "Historical/default-menu choice", "Other-style host spacing"]})

    def static_lanes(self):
        choices = []
        for style in range(366):
            animated = f"lane{style:03}" not in self.packages
            b = Branch(self, "l", f"l{style:03}")
            root = self.lane_art(b, style)
            root["id"], root["slot"] = b.prefix + ".art", "playfield.lane-cover.fill"
            marker = self.fallback_marker("l", "cover", "lane000", "lane000")
            b.template("art", root, "playfield.lane-cover.fill", marker)
            b.stage_ini("playfield.lane-cover.fill", marker)
            choices.append({"value": f"l{style:03}", "label": f"挡板 {style:03}" + (" · 原动画" if animated else ""), **b.finish()})
        self.add_module("lane-cover", "Lane Cover · 挡板", choices, "l000")

    def lane_art(self, branch, style):
        package = f"lane{style:03}"
        if package in self.packages:
            resource = branch.asset(package, package, "l")[0]
            return node(branch.prefix + ".art", "sprite", resource=resource)
        package += "_anim"
        exported = self.clip(package, package, f"l.clip.n{style:03}", loop=True, trigger="gameplay.attach")
        result = branch.include_clip(exported)
        result["id"], result["properties"] = branch.prefix + ".art", {}
        return result

    def explosions(self):
        self.options.append({"id": "explosion-scale", "type": "number", "label": "爆炸缩放", "default": 1,
                             "min": .5, "max": 1.5, "step": .05})
        choices = []
        for style in range(27):
            package = f"exp{style:02}"
            exported = self.clip(package, package, f"e.clip.n{style:02}", loop=False, trigger="judgement.hit")
            stage = exported["source"]["stage"]
            source_width, source_height = stage[2] - stage[0], stage[3] - stage[1]
            contributions = {}
            for sd in (False, True):
                b = Branch(self, "e", f"e{style:02}", "sd" if sd else "hd")
                prototype = b.include_clip(exported)
                materials = {ruleset: {} for ruleset in ("bms", "mania")}
                # Marker content is a genuine source image, used only on native fallback.
                for ruleset in ("bms", "mania"):
                    for colour in COLOURS:
                        width = 90 if colour == "scratch" else (39 if sd else 40) if colour == "black" else (51 if sd else 52)
                        identifier = b.prefix + "." + ruleset + "." + colour
                        root_properties = {"width": 4, "height": 4}
                        if ruleset == "mania":
                            host_height = (726 if sd else 728) - 1
                            root_properties = {"width": 4, "height": 90 / host_height, "y": (host_height - 22.5) / host_height}
                        animation = copy.deepcopy(prototype)
                        animation["id"] = identifier + ".art"
                        properties = {"x": .5, "y": .5, "origin": "centre",
                                      "width": source_width / (width * 4), "height": source_height / 90}
                        for dim, scale in (("width", "scale-x"), ("height", "scale-y")):
                            if properties[dim] > 4:
                                properties[scale], properties[dim] = properties[dim] / 4, 4
                        animation["properties"] = properties
                        scale_id = identifier + ".scale"
                        scale = node(scale_id, properties={"x": .125, "y": 17.5 / 90, "origin": "centre"}, children=[animation])
                        b.bind(scale_id, "scale-x", "option.explosion-scale")
                        b.bind(scale_id, "scale-y", "option.explosion-scale")
                        marker = self.fallback_marker("e", ruleset + "-" + colour, "exp00", "exp00_00")
                        root = node(identifier, properties=root_properties, slot="effect.hit-explosion", children=[scale])
                        b.template(ruleset + "." + colour, root, "effect.hit-explosion", marker)
                        materials[ruleset][colour] = {"effect.hit-explosion": marker}
                    b.lane_ini(materials[ruleset], ruleset)
                contributions[sd] = b.finish()
            choices.append({"value": f"e{style:02}", "label": f"爆炸 {style:02}", **contributions[False],
                            "variants": [{"when": {"frame": frame}, **contributions[frame_sd(frame)]} for frame in FRAMES]})
        self.add_module("explosion", "Explosion · 击打爆炸", choices, "e00")
        self.receipts.append({"category": "explosion", "sourceStyleCount": 27,
            "geometry": "Existing native BMS22.5 hit host and mania(H-1) full-column normalization; source canvas centred at native lane midpoint/17.5 hit pixels.",
            "scaling": "Author scale multiplies a separate centre-origin wrapper; original source positions/PNG bytes are unchanged.",
            "sourceFps": "Each source movie supplies its own original AFP fps, including exp20 at29.9697265625.",
            "requiredHitHostLifetimeMs": max(value["source"]["durationMs"] for key, value in self.clip_cache.items() if key[0].startswith("exp"))})

    def beams(self):
        self.sizes("beam-length", "光柱长度")
        choices = []
        for style in range(15):
            contributions = {}
            for size in SIZES:
                package = f"beam{style:02}" + ("_" + size if size != "normal" else "")
                metadata = read_json(within(self.library, self.packages[package]["authorData"]))
                movies = {movie["name"] for movie in metadata["movies"]}
                for sd in (False, True):
                    b = Branch(self, "b", f"b{style:02}", ("sd" if sd else "hd") + "." + size)
                    materials = {}
                    height = 726 if sd else 728
                    for colour in COLOURS:
                        movie = "beam_" + ("turn" if colour == "scratch" else colour)
                        texture = "playm_bar_" + ("turn" if colour == "scratch" else colour)
                        if (package, texture) not in self.index:
                            texture = Path(next(row["name"] for (key, _), row in self.index.items() if key == package)).stem
                        resource, _, row = b.asset(package, texture)
                        marker = self.fallback_marker("b", colour, "beam00", "playm_bar_" + ("turn" if colour == "scratch" else colour))
                        identifier = b.prefix + "." + colour
                        if movie in movies:
                            exported = self.clip(package, movie, f"b.clip.n{style:02}.{size}.{colour}", loop=style == 4, trigger="input.key.down")
                            root = b.include_clip(exported)
                            root["id"], root["slot"] = identifier, "decoration"
                            source_stage = exported["source"]["stage"]
                            root["properties"] = {"height": (source_stage[3] - source_stage[1]) / height}
                            if style == 4:
                                # No source in/loop/out labels exist here. Preserve its real
                                # sequential movie, and explicitly gate a held-only author loop.
                                b.bind(identifier, "visible", "input.pressed")
                                self.receipts.append({"category": "beam", "style": style, "size": size,
                                    "movie": movie, "control": "author held-only whole-source loop; no invented release labels",
                                    "unknown": "Original unlabeled beam04 host release/retrigger semantics"})
                        else:
                            root = node(identifier, "sprite", {"height": row["height"] / height}, resource,
                                        "decoration", "additive")
                            b.bind(identifier, "visible", "input.pressed")
                        b.template(colour, root, "decoration", marker)
                        materials[colour] = {"decoration": marker}
                    b.lane_ini(materials)
                    contributions[(sd, size)] = b.finish()
            choices.append({"value": f"b{style:02}", "label": f"光柱 {style:02}", **contributions[(False, "normal")],
                            "variants": [{"when": {"frame": frame, "beam-length": size}, **contributions[(frame_sd(frame), size)]}
                                         for frame in FRAMES for size in SIZES]})
        self.add_module("key-beam", "Key beam · 按键光柱", choices, "b00")

    def turntables(self):
        maps = {identifier: read_json(within(self.frame_maps.parent, relative))
                for identifier, relative in read_json(self.frame_maps).items()}
        if set(maps) != set(FRAMES):
            raise ValueError("Turntable source maps must cover exactly the15 declared Frame choices")
        choices = []
        for style in range(46):
            b = Branch(self, "t", f"t{style:02}")
            resource = b.asset(f"turn{style:02}", f"turn{style:02}")[0]
            for layout, movie in (("p1", "1p_frame"), ("p2", "2p_frame"), ("dp", "dp_frame_1p")):
                frames = []
                for frame in FRAMES:
                    source = maps[frame]
                    scale = 1920 / source["stage"]["width"]
                    children = []
                    for group in range(2 if layout == "dp" else 1):
                        binding = "turn_2p" if group == 1 or layout == "p2" else "turn_1p"
                        draw = next(draw for draw in source["layouts"][movie]["draws"] if binding in draw["game_bindings"])
                        matrix = draw["matrix"]
                        if matrix["b"] or matrix["c"] or matrix["d"] <= 0:
                            raise ValueError("Source turntable needs a non-axis-aligned replacement surface")
                        bounds = {key: value * scale for key, value in draw["bounds"].items()}
                        props = {"x": bounds["left"] / 1920, "y": bounds["top"] / 1080,
                                 "width": (bounds["right"] - bounds["left"]) / 1920,
                                 "height": (bounds["bottom"] - bounds["top"]) / 1080}
                        identifier = b.prefix + "." + layout + "." + frame + ".n" + str(group)
                        mirrored = matrix["a"] < 0
                        children.append(node(identifier, "projective-sprite" if mirrored else "sprite", props, resource))
                        if mirrored:
                            b.tracks.append({"id": identifier + ".orientation", "type": "frame", "target": identifier,
                                "property": "projective-quad", "easing": "step", "loop": False,
                                "keyframes": [{"id": identifier + ".fixed", "time": 0,
                                               "value": {"vertices": [[1, 0, 1], [0, 0, 1], [0, 1, 1], [1, 1, 1]], "opacity": 1}}]})
                    identifier = b.prefix + "." + layout + "." + frame
                    frames.append(node(identifier, properties={"visible": False}, children=children))
                    b.show(identifier, "option.frame", [frame])
                marker = f"markers/frame-{layout}-information"
                b.template(layout, node(b.prefix + "." + layout, slot="decoration", children=frames), "decoration", marker)
            choices.append({"value": f"t{style:02}", "label": f"转盘 {style:02}", **b.finish()})
        self.add_module("turntable", "Turntable · 转盘", choices, "t00")
        self.receipts.append({"category": "turntable", "sourceStyleCount": 46,
            "sourceMaps": {key: maps[key]["layouts"]["1p_frame"]["package"] for key in FRAMES},
            "geometry": "Each Frame's original four turn bindings, bounds and negative-X orientation. SD source coordinates explicitly project1.5 to the author canvas.",
            "materialSelection": "Additional template on the Frame's existing Global information marker; never overrides its decoration material.",
            "defaultEvidence": "turn00 full47x47 RGBA is byte-equal to33 usr_turn00; external original host selection remains unknown.",
            "unknown": "Original per-input rotation/animation; these46 source packages are texture-only."})

    def auxiliary_backgrounds(self):
        self.options.extend([
            {"id": "auxiliary-display", "type": "toggle", "label": "辅助屏初始展开", "default": False},
            {"id": "concentration", "type": "toggle", "label": "游玩时辅助屏专注模式", "default": False},
            {"id": "concentration-brightness", "type": "number", "label": "专注模式亮度", "default": .5,
             "min": 0, "max": 1, "step": .05}])
        for category, identifier, label, slot, marker in (
                ("p", "premium-background", "Premium Area Background · 辅助屏背景", "hud.auxiliary", "markers/auxiliary"),
                ("k", "concentration-background", "Concentration Mode · 专注背景", "hud.auxiliary-concentration", "markers/auxiliary-concentration")):
            choices = []
            for style in range(103):
                package = f"sub_customize_bg{style:03}"
                b = Branch(self, category, f"g{style:03}")
                resource, _, row = b.asset(package, package, "p")
                if (row["width"], row["height"]) != (1280, 720):
                    raise ValueError("Premium source background requires a different auxiliary aspect")
                root = node(b.prefix + ".background", "sprite", resource=resource, slot=slot)
                b.template("background", root, slot, marker)
                choices.append({"value": f"g{style:03}", "label": f"背景 {style:03}", **b.finish()})
            self.add_module(identifier, label, choices, "g000")
        self.receipts.append({"category": "auxiliary-backgrounds", "sourceStyleCount": 103,
            "sourcePixels": [1280, 720], "placement": "Independent16:9 canvas in a collapsible Player overlay panel, default collapsed; opening the panel may cover main Frame or BGA pixels.",
            "sharedOriginalAssets": "Premium and Concentration reference the same original PNG declarations; dimming is the dedicated viewport's brightness transform.",
            "defaultEvidence": "Initial source selection g000 is an author fallback for a collapsed optional viewport; not claimed as the original host's default.",
            "interaction": "The Player owns the auxiliary open/collapse and concentration per-play touch reset."})

    def auxiliary_icons(self):
        sources = [(f"i{style:02}", f"图标 {style:02}", f"sub_customize_panel{style:03}",
                    (f"sub_customize_panel{style:03}_j",)) for style in range(3)]
        for key, label in (("apina", "APINA VRAMeS"), ("gigo", "GiGO"), ("game_p", "GAME PANIC"),
                           ("silk_hat", "SILK HAT"), ("nova", "SUPERNOVA Tohoku"), ("tradz", "TAITO STATION Tradz"),
                           ("round1", "ROUND1"), ("leisure_l", "LEISURE LAND")):
            sources.append(("i-" + key.replace("_", "-"), label, "sub_customize_panel003",
                            ("sub_customize_panel003_j", "sub_customize_panel003_bpl_" + key)))
        choices = []
        for value, label, package, dependencies in sources:
            b = Branch(self, "i", value)
            exported = self.clip(package, "mainicon_playcus", "i.clip." + value, loop=True, trigger="gameplay.attach",
                                 texture_dependencies=dependencies, sample_frame=20)
            art = b.include_clip(exported)
            art["id"] = b.prefix + ".art"
            # This is a genuine250px original menu icon, placed by the author in
            # the new auxiliary viewport, not an assertion of original host XY.
            art["properties"] = {"x": .5, "y": .5, "width": 250 / 1280, "height": 250 / 720, "origin": "centre"}
            root = node(b.prefix + ".icon", slot="hud.auxiliary", children=[art])
            b.template("icon", root, "hud.auxiliary", "markers/auxiliary")
            choices.append({"value": value, "label": label, **b.finish()})
        self.add_module("premium-icon", "Premium Area Icon · 辅助屏图标", choices, "i00")
        self.receipts.append({"category": "premium-icon", "menuThemes": 11, "sourceIfsCount": 24,
            "sourceSelection": "Three original000/001/002 plus eight BPL overrides of the003 shell; a/j/k are language packages, not extra themes.",
            "language": "Explicit original_j text dependencies; labels are source Japanese, not fabricated Chinese sprites.",
            "sourceTiming": "Original260-frame interactive menu movie evaluated through its idle-label frame20; the selected genuine frame is held statically.",
            "scope": "Visual icon theme only. Original touch/menu/DJDATA interactions and account functions are not simulated.",
            "placement": "Authored centre of the dedicated auxiliary viewport; original icon canvas250x250 retained.",
            "pixelPreservation": "Original PNGs, fullUV and source hierarchy/matrices; resource-ID-only interning."})

    def graph_art(self):
        choices = [{"value": "blank", "label": "原空白", **Branch(self, "g", "blank").finish()}]
        for style in range(366):
            b = Branch(self, "g", f"l{style:03}")
            art = self.lane_art(b, style)
            for layout in ("p1", "p2", "dp"):
                identifier = b.prefix + "." + layout
                image = copy.deepcopy(art)
                image["id"] = identifier + ".art"
                image["properties"] = {"x": -9 / 414, "y": -17 / 692, "width": 432 / 414, "height": 724 / 692}
                left = 2 if layout == "p2" else 1504
                clipped = node(identifier + ".clip", "clip", {"x": left / 1920, "y": 178 / 1080,
                    "width": 414 / 1920, "height": 692 / 1080, "clip-mode": "bounds"}, children=[image])
                root = node(identifier, properties={"z": -20}, slot="decoration", children=[clipped])
                b.bind(identifier, "visible", "option.graph-visible")
                b.template(layout, root, "decoration", f"markers/frame-{layout}-information")
            choices.append({"value": f"l{style:03}", "label": f"原挡板艺术 {style:03}", **b.finish()})
        self.add_module("graph-area", "Graph Area · 图表区域艺术", choices, "blank")
        self.receipts.append({"category": "graph-area", "choices": 367, "sourceScope": "Explicit author composition into the actual original customize hook; original host selected-lane binding remains unknown.",
            "sourcePackage": "1/graph", "sourceMovie": "graph_1p/graph_2p", "sourceHook": "x_graph.x_lane_clip.customize",
            "originalArtCanvas": [432, 724], "sourceArtP1": [1495, 161, 1927, 885],
            "sourceClipP1": [1504, 178, 1918, 870], "p2Offset": [-1502, 0],
            "dp": "Author application of the sameP1 right-hand graph aperture; no original DP host capture asserted.",
            "default": "blank", "visibility": "Independent root option.graph-visible gate; default off preserves the main33 presentation. Premium auxiliary visibility is unrelated.",
            "drawOrder": "z=-20 behind the Frame's actual-score/maximum/progress contribution.",
            "statistics": "Caller owns true score bindings; no original target/rival/history placeholder is included here."})

    def control_black(self, branch):
        sys.path.insert(0, str(self.image_tools))
        from PIL import Image
        relative = "custom/control/lift-black.png"
        target = self.output / relative
        image = Image.new("RGBA", (1, 1), (0, 0, 0, 255))
        if target.exists():
            with Image.open(target) as previous:
                if previous.size != (1, 1) or previous.convert("RGBA").tobytes() != image.tobytes():
                    raise FileExistsError("Existing lift control marker differs")
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            image.save(target)
        self.files[relative] = {"path": relative, "sha256": digest(target), "bytes": target.stat().st_size}
        identifier = "v.control.black"
        branch.resources[identifier] = {"id": identifier, "type": "texture", "path": relative, "filter": "nearest"}
        return identifier, relative.removesuffix(".png")

    def lift_art(self):
        self.options.append({"id": "lift-readout", "type": "toggle", "label": "显示原素材 LIFT 数值", "default": True,
                             "rulesets": ["bms"], "keymodes": ["5k", "7k", "14k"]})
        choices = []
        for style in [None, *range(366)]:
            value = "blank" if style is None else f"l{style:03}"
            b = Branch(self, "v", value)
            black, marker = self.control_black(b)
            for sd in (False, True):
                identifier = b.prefix + (".sd" if sd else ".hd")
                children = [node(identifier + ".black", "sprite", resource=black)]
                if style is not None:
                    art = self.lane_art(b, style)
                    art["id"] = identifier + ".art"
                    width, pad = (438, 3) if sd else (440, 4)
                    art["properties"] = {"x": pad / width, "width": 432 / width}
                    children.append(art)
                root = node(identifier, slot="playfield.lift.fill", properties={"visible": False}, children=children)
                b.show(identifier, "option.frame", [frame for frame in FRAMES if frame_sd(frame) == sd])
                b.template("fill.sd" if sd else "fill.hd", root, "playfield.lift.fill", marker)
            glyphs = []
            for digit in range(10):
                resource, _, row = b.asset("1/gameparts", f"notes_time_num{digit}gs")
                if (row["width"], row["height"]) != (16, 20):
                    raise ValueError("Actual LIFT source glyph requires another font metric")
                glyphs.append({"character": str(digit), "resource": resource, "advance": 16})
            font_id = "v.font.units"
            b.fonts.append({"id": font_id, "lineHeight": 20, "glyphs": glyphs})
            line, _, _ = b.asset("1/gameparts", "notes_time_line")
            readout_marker = self.fallback_marker("v", "readout", "1/gameparts", "notes_time_line")
            text_id, row_id, root_id = b.prefix + ".units", b.prefix + ".row", b.prefix + ".readout"
            text = node(text_id, "text", {"text": "", "font-size": 20 * SCALE, "bitmap-font": font_id, "format": "integer"})
            b.bind(text_id, "text", "lift.value")
            row = node(row_id, properties={"x": .5, "y": 0, "origin": "bottom-centre", "layout": "horizontal", "gap-px": 0},
                       children=[text, node(b.prefix + ".separator", "sprite", {"height-px": 20 * SCALE}, line)])
            b.bind(row_id, "visible", "option.lift-readout")
            readout = node(root_id, slot="playfield.lift.decoration", children=[row])
            b.bind(root_id, "visible", "lift.available")
            b.template("readout", readout, "playfield.lift.decoration", readout_marker)
            for context in self.contexts:
                if context["ruleset"] == "bms":
                    b.ini.extend(("[GameplaySkin.Bms:1]", "Target: Stage " + context["stage"],
                                  f'playfield.lift.fill: resource Provide "{marker}"',
                                  f'playfield.lift.decoration: resource Provide "{readout_marker}"'))
            choices.append({"value": value, "label": "原黑色" if style is None else f"原挡板艺术 {style:03}", **b.finish()})
        self.add_module("lift-art", "LIFT · 底部挡板艺术", choices, "blank")
        self.modules[-1].update(rulesets=["bms"], keymodes=["5k", "7k", "14k"])
        self.receipts.append({"category": "lift", "choices": 367,
            "sourcePackages": ["1/gameparts", "lane000..365 including20 animated sources"],
            "nativeOwnership": "BMS-only explicit playfield.lift.fill/decoration. The actual controller consumes Units/offset/reversed; the package never changes the mod or native hit geometry.",
            "fillGeometry": "Full-stage uncompressed carrier, x4/440 or3/438 with inner432 art; the native owner clips the actual bottom/top offset region.",
            "readout": "True lift.value0..1000, one original16x20 gs digit font and original16x20 separator. The unknown right-hand original field is omitted.",
            "readoutPosition": "Author candidate at actual edge, above and centred, unflipped in reverse; original host edge offset remains uncalibrated.",
            "blank": "Generated one-pixel black control fill, explicitly not presented as an extracted artwork.",
            "sourceBinding": "Lane artwork applied as an explicit author composition; no original external host selected-lane binding is inferred."})


def build_modules(library: Path, output: Path, *, bemani_root: Path, image_tools: Path, frame_maps: Path) -> dict:
    """Build local non-Frame/non-Full-Combo module contributions and receipts."""
    builder = ModuleBuilder(Path(library), Path(output), bemani_root, image_tools, frame_maps)
    builder.notes()
    builder.charge_notes()
    builder.judgements()
    builder.explosions()
    builder.beams()
    builder.static_lanes()
    builder.turntables()
    builder.auxiliary_backgrounds()
    builder.auxiliary_icons()
    builder.graph_art()
    builder.lift_art()
    result = {"modules": builder.modules, "options": builder.options, "files": list(builder.files.values()),
              "receipts": {"sourceLibrary": str(builder.library), "sourceAssets": list(builder.used_assets.values()),
                           "branches": builder.branch_counts, "details": builder.receipts}}
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--library", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--bemani-root", type=Path, required=True)
    parser.add_argument("--image-tools", type=Path, required=True)
    parser.add_argument("--frame-maps", type=Path, required=True)
    args = parser.parse_args()
    result = build_modules(args.library, args.output, bemani_root=args.bemani_root, image_tools=args.image_tools,
                           frame_maps=args.frame_maps)
    metadata = args.output.parent / (args.output.name + "-module-author-data")
    metadata.mkdir(parents=True, exist_ok=False)
    write_json(metadata / "receipts.json", result["receipts"])
    write_json(metadata / "modules.json", {"contract": "oms-gameplay-skin-content.v1", "modules": result["modules"]})
    write_json(metadata / "options.json", result["options"])
    write_json(metadata / "files.json", result["files"])
    print(json.dumps({"modules": [{"id": item["id"], "choices": len(item["choices"])} for item in result["modules"]],
                      "files": len(result["files"]), "output": str(args.output)}, ensure_ascii=True))


if __name__ == "__main__":
    main()
