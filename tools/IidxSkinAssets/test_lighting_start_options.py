import copy
import contextlib
import hashlib
import io
import json
import math
from pathlib import Path
import tempfile
import unittest
import zipfile
from unittest.mock import patch

from PIL import Image

import build_lighting_start_options as builder
from lighting_start_options import (BAR_MATERIAL, BAR_PATH, BAR_RESOURCE, BAR_TARGETS, BRIGHTNESS, OPTION_ID,
                                    add_brightness_options, attach_barline_brightness, attach_barline_materials,
                                    attach_brightness, brightness_definition)


def scene(module="notes"):
    slot = sorted(BRIGHTNESS[module][3])[0]
    node = {"id": "art.source", "type": "sprite", "slot": slot, "resource": "source.texture",
            "properties": {"opacity": 0.3, "colour": "#12345678", "x": 0.25}, "children": [], "effects": []}
    return {"contract": "oms-gameplay-skin-scene.v1", "root": {"id": "empty", "type": "container", "children": []},
            "templates": [{"id": "source.template", "root": node}],
            "instances": [{"id": "source.instance", "template": "source.template", "material": {"slot": slot}}],
            "tracks": [{"id": "old.fade", "target": "art.source", "property": "opacity"}],
            "bindings": [{"id": "old.visible", "target": "art.source", "property": "visible", "source": "input.pressed"}],
            "variants": [{"id": "old.resource", "target": "art.source", "cases": []}],
            "stateMachines": [{"id": "old.machine", "states": [{"id": "off", "set": [
                {"id": "old.hide", "target": "art.source", "property": "opacity", "value": 0}]}], "transitions": []}]}


class StartOptionsTest(unittest.TestCase):
    def test_existing_material_geometry_colour_alpha_media_and_visibility_are_untouched(self):
        for module in BRIGHTNESS:
            with self.subTest(module=module):
                source = scene(module)
                before = copy.deepcopy(source)
                result, receipt = attach_brightness(source, module=module)
                self.assertEqual(before, source)
                self.assertEqual({key: value for key, value in result.items() if key != "bindings"},
                                 {key: value for key, value in before.items() if key != "bindings"})
                self.assertEqual(result["bindings"][:-1], before["bindings"])
                self.assertEqual(result["bindings"][-1]["source"], "option." + BRIGHTNESS[module][0])
                self.assertEqual(receipt["addedNodes"], 0)

    def test_one_binding_per_existing_material_root_without_nested_multiplication(self):
        source = scene()
        source["templates"][0]["root"]["children"] = [{"id": "nested", "type": "sprite", "resource": "original"}]
        another = copy.deepcopy(source["templates"][0])
        another["id"], another["root"]["id"] = "another", "another.root"
        another["root"]["children"][0]["id"] = "another.nested"
        source["templates"].append(another)
        source["instances"].append({"id": "another.instance", "template": "another", "material": {"slot": "object.note"}})
        result, receipt = attach_brightness(source, module="notes")
        self.assertEqual(receipt["materialRoots"], 2)
        self.assertEqual([item["target"] for item in result["bindings"] if item["property"] == "brightness"],
                         ["art.source", "another.root"])

    def test_repeated_upgrade_is_idempotent(self):
        first, _ = attach_brightness(scene(), module="notes")
        second, receipt = attach_brightness(first, module="notes")
        self.assertEqual(first, second)
        self.assertFalse(receipt["changed"])

    def test_global_declaration_identity_conflicts_are_rejected(self):
        mutations = [
            lambda s: s["root"].update(id="ob.n.v0"),
            lambda s: s["templates"][0]["root"]["children"].append({"id": "ob.n.v0", "children": []}),
            lambda s: s["templates"][0]["root"]["effects"].append({"id": "ob.n.v0"}),
            lambda s: s["templates"][0].update(id="ob.n.v0"),
            lambda s: s["instances"][0].update(id="ob.n.v0"),
            lambda s: s["tracks"][0].update(id="ob.n.v0"),
            lambda s: s["tracks"][0].update(keyframes=[{"id": "ob.n.v0"}]),
            lambda s: s["variants"][0].update(id="ob.n.v0"),
            lambda s: s["variants"][0].update(cases=[{"id": "ob.n.v0"}]),
            lambda s: s["stateMachines"][0].update(id="ob.n.v0"),
            lambda s: s["stateMachines"][0]["states"][0].update(id="ob.n.v0"),
            lambda s: s["stateMachines"][0]["states"][0]["set"][0].update(id="ob.n.v0"),
            lambda s: s["stateMachines"][0]["transitions"].append({"id": "ob.n.v0"}),
            lambda s: s["tracks"][0].update(id=s["root"]["id"]),
        ]
        for mutate in mutations:
            with self.subTest(mutation=mutate):
                source = scene(); mutate(source); before = copy.deepcopy(source)
                with self.assertRaises(ValueError): attach_brightness(source, module="notes")
                self.assertEqual(source, before)

    def test_idempotent_binding_does_not_hide_other_brightness_owners(self):
        original, _ = attach_brightness(scene(), module="notes")
        mutations = [
            lambda s: s["templates"][0]["root"]["properties"].update(brightness=.5),
            lambda s: s["templates"][0]["root"]["children"].append({"id": "nested", "properties": {"brightness": .5}}),
            lambda s: s["tracks"].append({"id": "competing", "target": "art.source", "property": "brightness"}),
            lambda s: s["stateMachines"][0]["states"][0]["set"].append(
                {"id": "competing", "target": "art.source", "property": "brightness"}),
            lambda s: s["bindings"].append({"id": "nested", "target": "unknown", "property": "brightness", "source": "option." + OPTION_ID}),
        ]
        for mutate in mutations:
            source = copy.deepcopy(original); mutate(source); before = copy.deepcopy(source)
            with self.subTest(mutation=mutate):
                with self.assertRaises(ValueError): attach_brightness(source, module="notes")
                self.assertEqual(source, before)

    def test_conflicts_fail_without_mutating_input(self):
        mutations = [
            lambda s: s["templates"][0]["root"]["properties"].update(brightness=0.5),
            lambda s: s["tracks"].append({"target": "art.source", "property": "brightness"}),
            lambda s: s["stateMachines"][0]["states"][0]["set"].append({"target": "art.source", "property": "brightness"}),
            lambda s: s["bindings"].append({"id": "ob.n.v0", "target": "elsewhere", "property": "opacity"}),
            lambda s: s["templates"][0]["root"].update(slot="object.long-note.body"),
            lambda s: s["root"].update(children=[{"id": "unreviewed"}]),
        ]
        for mutate in mutations:
            with self.subTest(mutation=mutate):
                source = scene(); mutate(source); before = copy.deepcopy(source)
                with self.assertRaises(ValueError): attach_brightness(source, module="notes")
                self.assertEqual(source, before)

    def test_all_existing_art_modules_share_exactly_one_setting_but_not_declaration_ids(self):
        bindings = [attach_brightness(scene(module), module=module)[0]["bindings"][-1] for module in BRIGHTNESS]
        self.assertEqual({binding["source"] for binding in bindings}, {"option." + OPTION_ID})
        self.assertEqual(len({binding["id"] for binding in bindings}), len(BRIGHTNESS))

    def test_definition_preserves_observed_range_and_default(self):
        value = brightness_definition(OPTION_ID, minimum=0.1, maximum=1, step=0.1, default=1)
        self.assertEqual((value["min"], value["max"], value["step"], value["default"]), (0.1, 1, 0.1, 1))
        for overrides in [dict(minimum=-.1), dict(maximum=1.1), dict(step=0), dict(default=0.55),
                          dict(default=True), dict(step=math.nan), dict(maximum=math.inf), dict(step=1e-320), dict(step=1e-5)]:
            fields = dict(minimum=0.1, maximum=1, step=0.1, default=1); fields.update(overrides)
            with self.subTest(overrides=overrides):
                with self.assertRaises(ValueError): brightness_definition(OPTION_ID, **fields)
        for old_id in ("note-brightness", "beam-brightness", "explosion-brightness", "judgement-brightness"):
            with self.assertRaises(ValueError): brightness_definition(old_id, minimum=0, maximum=1, step=.1, default=1)

    def test_options_are_added_once_without_changing_existing_choices(self):
        manifest = {"contract": "oms-gameplay-skin-manifest.v1", "options": [
            {"id": "old", "type": "toggle", "default": True}], "resources": [{"id": "same"}]}
        before = copy.deepcopy(manifest)
        definition = brightness_definition(OPTION_ID, minimum=0.1, maximum=1, step=0.1, default=1)
        result = add_brightness_options(manifest, [definition])
        self.assertEqual(manifest, before)
        self.assertEqual(result["resources"], before["resources"])
        self.assertEqual(result["options"][:-1], before["options"])
        self.assertEqual(add_brightness_options(result, [definition]), result)
        conflict = copy.deepcopy(result); conflict["options"][-1]["default"] = .5
        with self.assertRaises(ValueError): add_brightness_options(conflict, [definition])
        conflict = copy.deepcopy(manifest); conflict["options"].append(copy.deepcopy(conflict["options"][0]))
        with self.assertRaises(ValueError): add_brightness_options(conflict, [definition])

    def test_barline_uses_one_sprite_with_same_shared_multiplier_and_no_native_fade_override(self):
        original = neutral_scene()
        result, receipt = attach_barline_brightness(original)
        self.assertEqual(original, neutral_scene())
        self.assertEqual(result["root"], original["root"])
        self.assertEqual(len(result["templates"]), 1)
        sprite = result["templates"][0]["root"]
        self.assertEqual((sprite["type"], sprite["slot"], sprite["resource"]), ("sprite", "playfield.bar-line", BAR_RESOURCE))
        self.assertEqual(sprite["properties"], {})
        self.assertEqual(sprite["children"], [])
        self.assertEqual(sprite["effects"], [])
        self.assertEqual(result["tracks"], [])
        self.assertEqual(result["stateMachines"], [])
        self.assertEqual(result["bindings"], [{"id": "ob.bar.b", "target": sprite["id"], "property": "brightness", "source": "option." + OPTION_ID}])
        self.assertEqual(result["instances"][0]["material"], {"slot": "playfield.bar-line", "resource": BAR_MATERIAL})
        self.assertTrue(receipt["replacesCanonicalSprite"])
        self.assertEqual(receipt["addedNodes"], 1)

    def test_barline_conflicts_are_atomic_and_ini_preserves_all_original_bytes(self):
        original = neutral_scene()
        mutations = [lambda s: s["root"].update(id="ob.bar.b"),
                     lambda s: s["root"]["children"].append({"id": "existing"}),
                     lambda s: s["templates"].append({"id": "existing"}),
                     lambda s: s["bindings"].append({"id": "existing"})]
        for mutate in mutations:
            value = copy.deepcopy(original); mutate(value); before = copy.deepcopy(value)
            with self.assertRaises(ValueError): attach_barline_brightness(value)
            self.assertEqual(value, before)
        ini = b'[General]\r\nName: exact prefix\r\n[GameplaySkin.Common:1]\r\nTarget: Global ruleset=any keymode=any stage-mode=any\r\n'
        result = attach_barline_materials(ini)
        self.assertTrue(result.startswith(ini))
        self.assertEqual(result.count(b'playfield.bar-line: resource Provide'), 4)
        for target in BAR_TARGETS:
            self.assertIn((target + '\nplayfield.bar-line: resource Provide "' + BAR_MATERIAL + '"').encode(), result)
        with self.assertRaises(ValueError): attach_barline_materials(result)


def neutral_scene():
    return {"contract": "oms-gameplay-skin-scene.v1", "root": {"id": "dispatch", "type": "container", "target": {"kind": "global"},
            "blend": "inherit", "properties": {}, "children": [], "effects": []},
            "templates": [], "instances": [], "tracks": [], "bindings": [], "variants": [], "stateMachines": []}


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def private_fixture(root):
    """Tiny synthetic authors and reference pixels; never real original-menu evidence."""
    source = root / "authors" / "frozen"
    source.mkdir(parents=True)
    (source / "skin.ini").write_text("[General]\nName: synthetic fixture\n[GameplaySkin.Common:1]\n"
                                   "Target: Global ruleset=any keymode=any stage-mode=any\n", encoding="utf-8")
    (source / "art").mkdir()
    Image.new("RGBA", (2, 2), (10, 20, 30, 100)).save(source / "art" / "source.png")
    (source / "previews").mkdir()
    Image.new("RGB", (2, 2), "blue").save(source / "previews" / "source.png")
    manifest = {"contract": "oms-gameplay-skin-manifest.v1", "scene": "gameplay-skin.scene.json",
        "sceneContract": "oms-gameplay-skin-scene.v1", "eventContract": "oms-gameplay-skin-event.v1",
        "resources": [], "contentCatalog": "gameplay/content.json",
        "options": [{"id": "old-toggle", "type": "toggle", "label": "old", "default": True}]}
    write(source / "gameplay-skin.json", manifest)
    write(source / "gameplay-skin.scene.json", neutral_scene())
    modules = []
    for module in BRIGHTNESS:
        document = scene(module)
        write(source / "config" / (module + ".scene.json"), document)
        write(source / "config" / (module + ".manifest.json"), {
            "contract": manifest["contract"], "scene": manifest["scene"], "sceneContract": manifest["sceneContract"],
            "eventContract": manifest["eventContract"], "resources": [{"id": "source.texture", "type": "texture", "path": "art/source.png"}]})
        choice = {"value": "one", "label": "preserved", "manifest": "config/" + module + ".manifest.json",
                  "scene": "config/" + module + ".scene.json", "preview": "previews/source.png"}
        choice["variants"] = [{"when": {"old-toggle": True}, "manifest": choice["manifest"], "scene": choice["scene"]}]
        modules.append({"id": module, "default": "one", "label": module, "choices": [choice]})
    write(source / "gameplay/content.json", {"contract": "oms-gameplay-skin-content.v1", "modules": modules})
    write(source / "generation.json", {"synthetic": True})
    write(source / "package-audit.json", {"synthetic": True})
    source.with_suffix(".osk").write_bytes(b"immutable old synthetic archive")
    screenshot = root / "reference.png"
    Image.new("RGB", (2, 2), "green").save(screenshot)
    bar = Image.new("RGBA", (256, 32), (0, 0, 0, 0)); bar.paste((167, 184, 205, 130), (0, 0, 256, 2))
    buffer = io.BytesIO(); bar.save(buffer, format="PNG"); bar_data = buffer.getvalue()
    canonical = root / "canonical.osk"
    with zipfile.ZipFile(canonical, "x") as package:
        for entry in sorted(builder.BAR_SOURCES): package.writestr(entry, bar_data)
        package.writestr("skin.ini", "[GameplaySkin.Common:1]\n" + "\n".join(
            target + '\nplayfield.bar-line: resource Provide "' + ("bms/bar" if "ruleset=bms " in target else "mania/bar") + '"'
            for target in BAR_TARGETS) + "\n")
    reference = {"contract": builder.REFERENCE, "status": "reviewed",
        "source": {"manifestSha256": digest(source / "gameplay-skin.json"),
                   "generationSha256": digest(source / "generation.json"), "archiveSha256": digest(source.with_suffix(".osk"))},
        "canonical": {"archiveSha256": digest(canonical), "provenance": "Synthetic unit-test canonical package; not an actual installation.",
            "barSources": [{"path": name, "sha256": hashlib.sha256(bar_data).hexdigest()} for name in sorted(builder.BAR_SOURCES)]},
        "setting": {"id": OPTION_ID, "min": .1, "max": 1, "step": .1, "default": 1,
            "reference": {"menuLabel": "Synthetic Lane Objects Brightness Adjust", "description": "Synthetic one shared control for all five object categories.",
                "review": "Unit-test fixture, not native evidence.",
                "screenshots": [{"path": "reference.png", "sha256": digest(screenshot), "supports": ["min", "max", "step", "default"]}]}}
        }
    path = root / "reviewed.json"
    write(path, reference)
    return source, path, source.parent / "upgraded"


def fixture_audit(output, files):
    manifest = builder.read_json(output / "gameplay-skin.json")
    catalog = builder.read_json(output / "gameplay/content.json")
    return {"files": len(files), "uncompressedBytes": sum((output / p).stat().st_size for p in files),
        "configurationBytes": sum((output / p).stat().st_size for p in files if p.endswith((".json", ".ini"))),
        "options": len(manifest["options"]) + len(catalog["modules"]), "selectedUpperBound": {}, "aggregateViolations": []}


class StartOptionsBuilderTest(unittest.TestCase):
    def test_reviewed_input_requires_one_setting_values_label_description_and_actual_image_receipts(self):
        with tempfile.TemporaryDirectory() as directory:
            source, path, output = private_fixture(Path(directory))
            reference = builder.load_reference(path)
            self.assertEqual({d["id"] for d in reference["definitions"]}, set(builder.OPTION_LABELS))
            original = builder.read_json(path)
            mutations = [
                lambda r: r.update(status="pending"),
                lambda r: r.pop("setting"),
                lambda r: r["setting"].pop("step"),
                lambda r: r["setting"].update(id="note-brightness"),
                lambda r: r["setting"]["reference"].update(screenshots=[]),
                lambda r: r["setting"]["reference"]["screenshots"][0].update(sha256="0" * 64),
                lambda r: r["setting"]["reference"]["screenshots"][0].update(supports=["default"]),
                lambda r: r["setting"]["reference"].update(review=""),
                lambda r: r["setting"]["reference"].update(description=""),
                lambda r: r["canonical"]["barSources"].pop(),
            ]
            for mutate in mutations:
                value = copy.deepcopy(original); mutate(value); write(path, value)
                with self.subTest(mutation=mutate):
                    with self.assertRaises(ValueError): builder.load_reference(path)
                    self.assertFalse(output.exists())
            self.assertTrue(source.with_suffix(".osk").exists())

    def test_full_migration_reuses_scenes_and_preserves_media_choices_options_and_old_archive(self):
        with tempfile.TemporaryDirectory() as directory:
            source, path, output = private_fixture(Path(directory))
            before = {p.relative_to(source).as_posix(): p.read_bytes() for p in source.rglob("*") if p.is_file()}
            old_archive = source.with_suffix(".osk").read_bytes()
            with patch.object(builder, "audit_package", side_effect=fixture_audit), contextlib.redirect_stdout(io.StringIO()):
                receipt = builder.upgrade(source, path, output, canonical_source=path.parent / "canonical.osk")
            self.assertEqual(len(receipt["brightnessUpgrade"]), len(BRIGHTNESS), "Repeated choice/variant aliases share one transformed scene.")
            self.assertEqual(receipt["unifiedOptionCount"], 7)
            self.assertEqual(receipt["reviewedStartOptions"]["setting"]["id"], OPTION_ID)
            self.assertEqual(receipt["sourcePreservation"]["addedCanonicalAssets"], [BAR_PATH])
            self.assertEqual(receipt["sourcePreservation"]["catalogChoices"], len(BRIGHTNESS))
            self.assertEqual(receipt["sourcePreservation"]["previewFiles"], 1)
            self.assertTrue(output.with_suffix(".osk").is_file())
            self.assertEqual(source.with_suffix(".osk").read_bytes(), old_archive)
            self.assertEqual({p.relative_to(source).as_posix(): p.read_bytes() for p in source.rglob("*") if p.is_file()}, before)
            self.assertEqual((output / "art/source.png").read_bytes(), before["art/source.png"])
            self.assertEqual((output / "previews/source.png").read_bytes(), before["previews/source.png"])
            with zipfile.ZipFile(path.parent / "canonical.osk") as canonical:
                self.assertEqual((output / BAR_PATH).read_bytes(), canonical.read("bms/bar.png"))
                self.assertEqual((output / BAR_PATH).read_bytes(), canonical.read("mania/bar.png"))
            with Image.open(output / BAR_PATH) as bar:
                self.assertEqual(bar.size, (256, 32), "The transparent vertical padding controls the old sprite's visible thickness.")
                self.assertEqual(bar.getpixel((0, 0)), (167, 184, 205, 130))
                self.assertEqual(bar.getpixel((255, 1)), (167, 184, 205, 130))
                self.assertEqual(bar.getpixel((0, 2)), (0, 0, 0, 0))
                self.assertEqual(bar.getpixel((255, 31)), (0, 0, 0, 0))
            self.assertTrue(receipt["barlineUpgrade"]["originalBytesCopied"])
            self.assertEqual(receipt["barlineUpgrade"]["sha256"], digest(output / BAR_PATH))
            self.assertEqual((output / "skin.ini").read_bytes(), attach_barline_materials(before["skin.ini"]))
            self.assertEqual(builder.read_json(output / "gameplay-skin.scene.json"), attach_barline_brightness(json.loads(before["gameplay-skin.scene.json"]))[0])
            root_manifest = builder.read_json(output / "gameplay-skin.json")
            self.assertEqual(root_manifest["resources"], [{"id": BAR_RESOURCE, "type": "texture", "path": BAR_PATH}])
            self.assertEqual(len([option for option in root_manifest["options"] if option["id"] == OPTION_ID]), 1)
            self.assertEqual(builder.read_json(output / "gameplay-skin.json")["options"][0], builder.read_json(source / "gameplay-skin.json")["options"][0])
            old_catalog, new_catalog = (builder.read_json(p / "gameplay/content.json") for p in (source, output))
            for old_module, new_module in zip(old_catalog["modules"], new_catalog["modules"]):
                self.assertEqual(old_module["choices"][0]["preview"], new_module["choices"][0]["preview"])
                part = new_module["choices"][0]
                self.assertEqual(part["scene"], part["variants"][0]["scene"])
                document = builder.read_json(output / part["scene"])
                self.assertEqual(len([b for b in document["bindings"] if b["property"] == "brightness"]), len(document["templates"]))
                self.assertEqual({b["source"] for b in document["bindings"] if b["property"] == "brightness"}, {"option." + OPTION_ID})

    def test_canonical_hash_pixel_and_scope_mismatches_never_create_staging_or_publish(self):
        for mutation in ("archive-hash", "image-hash", "different-png", "missing-group", "wrong-texture"):
            with self.subTest(mutation=mutation), tempfile.TemporaryDirectory() as directory:
                source, path, output = private_fixture(Path(directory))
                canonical_path = path.parent / "canonical.osk"
                reference = builder.read_json(path)
                source_before = {p.relative_to(source).as_posix(): p.read_bytes() for p in source.rglob("*") if p.is_file()}
                if mutation == "archive-hash":
                    reference["canonical"]["archiveSha256"] = "0" * 64
                elif mutation == "image-hash":
                    reference["canonical"]["barSources"][0]["sha256"] = "0" * 64
                else:
                    with zipfile.ZipFile(canonical_path) as package:
                        entries = {name: package.read(name) for name in package.namelist()}
                    if mutation == "different-png":
                        with Image.open(io.BytesIO(entries["mania/bar.png"])) as bar:
                            bar.putpixel((0, 0), (167, 184, 205, 129))
                            stream = io.BytesIO(); bar.save(stream, format="PNG")
                        entries["mania/bar.png"] = stream.getvalue()
                        for entry in reference["canonical"]["barSources"]:
                            entry["sha256"] = hashlib.sha256(entries[entry["path"]]).hexdigest()
                    elif mutation == "missing-group":
                        entries["skin.ini"] = entries["skin.ini"].replace((BAR_TARGETS[-1] + '\nplayfield.bar-line: resource Provide "mania/bar"\n').encode(), b"")
                    else:
                        entries["skin.ini"] = entries["skin.ini"].replace(b'Provide "bms/bar"', b'Provide "bms/other"')
                    with zipfile.ZipFile(canonical_path, "w") as package:
                        for name, data in entries.items(): package.writestr(name, data)
                    reference["canonical"]["archiveSha256"] = digest(canonical_path)
                write(path, reference)
                with patch.object(builder, "publish_prepared_package") as publish:
                    with self.assertRaises(ValueError): builder.upgrade(source, path, output, canonical_source=canonical_path)
                    publish.assert_not_called()
                self.assertEqual({p.relative_to(source).as_posix(): p.read_bytes() for p in source.rglob("*") if p.is_file()}, source_before)
                self.assertEqual(sorted(p.name for p in source.parent.iterdir()), sorted([source.name, source.with_suffix(".osk").name]))

    def test_existing_barline_material_cannot_shadow_the_shared_sprite(self):
        with tempfile.TemporaryDirectory() as directory:
            source, path, output = private_fixture(Path(directory))
            with (source / "skin.ini").open("a", encoding="utf-8") as ini:
                ini.write('\n' + BAR_TARGETS[0] + '\nplayfield.bar-line: resource Provide "art/source"\n')
            with self.assertRaises(ValueError): builder.upgrade(source, path, output, canonical_source=path.parent / "canonical.osk")
            self.assertFalse(output.exists())
            self.assertEqual(sorted(p.name for p in source.parent.iterdir()), sorted([source.name, source.with_suffix(".osk").name]))

    def test_full_audit_must_report_exactly_one_added_global_option(self):
        with tempfile.TemporaryDirectory() as directory:
            source, path, output = private_fixture(Path(directory))
            def wrong_options(directory, files):
                result = fixture_audit(directory, files)
                result["options"] += 3
                return result
            with patch.object(builder, "audit_package", side_effect=wrong_options), patch.object(builder, "publish_prepared_package") as publish:
                with self.assertRaises(ValueError): builder.upgrade(source, path, output, canonical_source=path.parent / "canonical.osk")
                publish.assert_not_called()
            self.assertFalse(output.exists())

    def test_bad_source_identity_or_claimed_output_never_publishes(self):
        with tempfile.TemporaryDirectory() as directory:
            source, path, output = private_fixture(Path(directory))
            original = builder.read_json(path)
            bad = copy.deepcopy(original); bad["source"]["manifestSha256"] = "0" * 64; write(path, bad)
            with self.assertRaises(ValueError): builder.upgrade(source, path, output, canonical_source=path.parent / "canonical.osk")
            self.assertEqual(sorted(p.name for p in source.parent.iterdir()), sorted([source.name, source.with_suffix(".osk").name]))
            write(path, original)
            output.with_suffix(".osk").write_bytes(b"claimed")
            with self.assertRaises(ValueError): builder.upgrade(source, path, output, canonical_source=path.parent / "canonical.osk")
            self.assertEqual(output.with_suffix(".osk").read_bytes(), b"claimed")
            self.assertFalse(output.exists())

    def test_budget_failure_retains_source_and_cannot_publish(self):
        with tempfile.TemporaryDirectory() as directory:
            source, path, output = private_fixture(Path(directory))
            original = digest(source / "gameplay-skin.json")
            def over_budget(directory, files):
                audit = fixture_audit(directory, files)
                audit["aggregateViolations"] = [{"budget": "sceneBytes", "upperBound": 524289, "limit": 524288}]
                return audit
            with patch.object(builder, "audit_package", side_effect=over_budget), patch.object(builder, "publish_prepared_package") as publish:
                with self.assertRaises(ValueError): builder.upgrade(source, path, output, canonical_source=path.parent / "canonical.osk")
                publish.assert_not_called()
            self.assertFalse(output.exists())
            self.assertFalse(output.with_suffix(".osk").exists())
            self.assertEqual(digest(source / "gameplay-skin.json"), original)


if __name__ == "__main__":
    unittest.main()
