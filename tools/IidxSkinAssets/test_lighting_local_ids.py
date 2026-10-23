"""Typed local-ID rewriting preserves all external author identities."""

import copy
import unittest

import lighting_local_ids as local_ids


def node(identifier, kind="container", properties=None, children=None, **extra):
    return {"id": identifier, "type": kind, "target": {"kind": "global"}, "blend": "alpha",
            "properties": properties or {}, "effects": [], "children": children or [], **extra}


def fixture():
    prefix = "long.judgement.branch"
    fonts = [prefix + ".font." + palette for palette in ("b", "y")]
    document = {"contract": local_ids.SCENE, "root": node(prefix + ".root", blend="inherit"), "templates": [], "instances": [],
                "tracks": [], "bindings": [], "variants": [], "stateMachines": []}
    manifest = {"contract": local_ids.MANIFEST, "scene": "gameplay/custom/j/hd/scene.json", "sceneContract": local_ids.SCENE,
                "eventContract": "oms-gameplay-skin-event.v1", "resources": [
                    {"id": "label.digits", "type": "texture", "path": "art/digits.png", "filter": "nearest"},
                    {"id": "label.perfect", "type": "texture", "path": "art/perfect.png", "filter": "nearest"}],
                "bitmapFonts": [{"id": identifier, "lineHeight": 100, "glyphs": [
                    {"character": "1", "resource": "label.digits", "rect": [0, 0, 74, 100], "advance": 55}]} for identifier in fonts],
                "animations": []}
    for layout in ("p1", "p2", "dp"):
        root_id = prefix + "." + layout
        rows = []
        for group, font_id, grades in (("perfect", fonts[0], ("perfect",)), ("other", fonts[1], ("great", "good", "meh", "miss", "ok"))):
            row_id = root_id + "." + group
            label = node(row_id + ".label", "sprite", {"height-px": 70}, resource="label.perfect")
            combo = node(row_id + ".combo", "text", {"text": "", "font-size": 70, "bitmap-font": font_id, "format": "integer"})
            rows.append(node(row_id, properties={"x": .5, "y": .5, "origin": "centre", "layout": "horizontal", "gap-px": -11},
                             children=[label, combo]))
            document["bindings"].append({"id": combo["id"] + ".bind", "target": combo["id"], "property": "text", "source": "combo.value"})
            document["variants"].append({"id": row_id + ".v", "target": row_id, "property": "visible", "source": "judgement.result", "default": False,
                "cases": [{"id": row_id + ".c." + key, "key": key, "value": True} for key in grades]})
        root = node(root_id, properties={"opacity": 0}, slot="hud.judgement", children=rows)
        template_id = prefix + ".template." + layout
        document["templates"].append({"id": template_id, "root": root})
        document["instances"].append({"id": template_id + ".instance", "template": template_id,
                                      "material": {"slot": "hud.judgement", "resource": "custom/markers/j/" + layout}})
        document["stateMachines"].append({"id": root_id + ".machine", "initial": root_id + ".empty", "states": [
            {"id": root_id + ".empty", "set": [{"id": root_id + ".hide", "target": root_id, "property": "opacity", "value": 0}]},
            {"id": root_id + ".active", "set": [{"id": root_id + ".show", "target": root_id, "property": "opacity", "value": 1}]}],
            "transitions": [{"id": root_id + ".arrive", "from": root_id + ".empty", "to": root_id + ".active", "event": "judgement.hit"}]})
    return document, manifest


def nodes(document):
    def walk(root):
        yield root
        for child in root["children"]:
            yield from walk(child)
    return [item for root in [document["root"], *(template["root"] for template in document["templates"])] for item in walk(root)]


class LightingLocalIdentityTest(unittest.TestCase):
    def test_only_typed_local_declarations_and_references_change(self):
        document, manifest = fixture()
        # Coinciding strings in other namespaces make a blanket string replacement observable.
        document["root"]["id"] = manifest["bitmapFonts"][0]["id"]
        external = document["root"]["id"]
        decorative = node("perfect", "recorded-animation", {"opacity": 1}, animation=external, clock="timing.elapsed")
        decorative["target"] = {"kind": "lane", "id": external}
        decorative["effects"] = [{"id": "oj0", "type": "shadow", "properties": {"radius": 2}}]
        document["root"]["children"].append(decorative)
        document["tracks"].append({"id": "long.decorative.track", "type": "tween", "target": decorative["id"], "property": "opacity",
            "easing": "step", "loop": False, "clock": "timing.elapsed", "keyframes": [{"id": "long.decorative.key", "time": 0, "value": 1}]})
        manifest["animations"] = [{"id": external, "path": "animations/" + external + ".json.br"}]
        # Resource/path, font, recorded animation, topology and variant-case namespaces are all external here.
        label_id = document["templates"][0]["root"]["children"][0]["children"][0]["id"]
        manifest["resources"][1]["id"] = label_id
        for template in document["templates"]:
            template["root"]["children"][0]["children"][0]["resource"] = label_id
        before, original_manifest = copy.deepcopy(document), copy.deepcopy(manifest)
        receipt = local_ids.compact_local_ids(document, manifest, module="judgement")
        mapping = receipt["mapping"]
        self.assertEqual(original_manifest, manifest)
        self.assertGreater(receipt["changed"], 50)
        self.assertEqual(len(mapping), len(set(mapping.values())))
        for old_node, new_node in zip(nodes(before), nodes(document)):
            self.assertEqual(mapping[old_node["id"]], new_node["id"])
            for field in ("target", "properties", "resource", "animation", "clock", "type", "slot", "blend"):
                self.assertEqual(old_node.get(field), new_node.get(field))
            for old_effect, new_effect in zip(old_node["effects"], new_node["effects"]):
                self.assertEqual({**old_effect, "id": mapping[old_effect["id"]]}, new_effect)
        for old_instance, new_instance in zip(before["instances"], document["instances"]):
            self.assertEqual({**old_instance, "id": mapping[old_instance["id"]], "template": mapping[old_instance["template"]]}, new_instance)
        for field in ("tracks", "bindings", "variants"):
            for old_item, new_item in zip(before[field], document[field]):
                self.assertEqual(mapping[old_item["id"]], new_item["id"])
                self.assertEqual(mapping[old_item["target"]], new_item["target"])
                for key in set(old_item) - {"id", "target", "keyframes", "cases"}:
                    self.assertEqual(old_item[key], new_item[key])
                for key in ("keyframes", "cases"):
                    for old_child, new_child in zip(old_item.get(key, []), new_item.get(key, [])):
                        self.assertEqual({**old_child, "id": mapping[old_child["id"]]}, new_child)
        for old_machine, machine in zip(before["stateMachines"], document["stateMachines"]):
            self.assertEqual(mapping[old_machine["initial"]], machine["initial"])
            for old_state, state in zip(old_machine["states"], machine["states"]):
                self.assertEqual(mapping[old_state["id"]], state["id"])
                for assignment, new_assignment in zip(old_state["set"], state["set"]):
                    self.assertEqual({**assignment, "id": mapping[assignment["id"]], "target": mapping[assignment["target"]]}, new_assignment)
            for transition, new_transition in zip(old_machine["transitions"], machine["transitions"]):
                self.assertEqual({**transition, "id": mapping[transition["id"]], "from": mapping[transition["from"]],
                                  "to": mapping[transition["to"]]}, new_transition)
        repeated = copy.deepcopy(document)
        self.assertEqual(0, local_ids.compact_local_ids(document, manifest, module="judgement")["changed"])
        self.assertEqual(repeated, document)

    def test_cn_and_judgement_local_namespaces_cannot_collide_when_combined(self):
        document, manifest = fixture()
        cn = copy.deepcopy(document)
        judge_receipt = local_ids.compact_local_ids(document, manifest, module="judgement")
        cn_receipt = local_ids.compact_local_ids(cn, manifest, module="charge-notes")
        self.assertFalse(set(judge_receipt["mapping"].values()) & set(cn_receipt["mapping"].values()))

    def test_duplicate_declarations_and_script_or_unknown_contracts_are_rejected_atomically(self):
        for conflict in ("duplicate", "script", "contract", "manifest-contract", "unknown-section", "module"):
            with self.subTest(conflict=conflict):
                document, manifest = fixture()
                if conflict == "duplicate":
                    document["bindings"][0]["id"] = document["root"]["id"]
                elif conflict == "script":
                    manifest["script"] = "gameplay/local-ids.lua"
                elif conflict == "contract":
                    document["contract"] = "unknown-scene-contract"
                elif conflict == "manifest-contract":
                    manifest["contract"] = "unknown-manifest-contract"
                elif conflict == "unknown-section":
                    document["futureIdReferences"] = []
                before = copy.deepcopy((document, manifest))
                with self.assertRaises(ValueError):
                    local_ids.compact_local_ids(document, manifest, module="frame" if conflict == "module" else "judgement")
                self.assertEqual(before, (document, manifest))


if __name__ == "__main__":
    unittest.main()
