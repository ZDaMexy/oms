"""Native-chrome-only author regressions; shared C# codec remains authoritative."""
import json
import unittest

from lighting_song_select import ASSETS, CONTRACT, append_song_select, song_select_page


class SongSelectAuthorTests(unittest.TestCase):
    def parent(self):
        return {"contract": CONTRACT, "results": {"width": 1920, "height": 1080,
            "resources": [{"id": "old", "path": "pages/results/original.png"}], "elements": [{"id": "real-score", "source": "score.value"}]}}

    def test_real_results_bytes_and_all_its_fields_are_preserved(self):
        parent = self.parent()
        original = json.dumps(parent, ensure_ascii=False, indent=2).encode("utf-8") + b"\n"
        result = append_song_select(original)
        self.assertTrue(result.startswith(original.rstrip()[:-1]))
        self.assertEqual(json.loads(result)["results"], parent["results"])
        self.assertEqual(json.loads(result)["songSelect"], song_select_page())

    def test_no_existing_page_or_unknown_parent_is_overwritten(self):
        for parent in ({"contract": CONTRACT}, {**self.parent(), "songSelect": song_select_page()}, {**self.parent(), "unexpected": 1}):
            with self.subTest(parent=parent), self.assertRaises(ValueError):
                append_song_select(json.dumps(parent).encode())

    def test_resource_budget_is_aggregate_not_reset_for_another_page(self):
        parent = self.parent()
        parent["results"]["resources"] = [{"id": "r" + str(i), "path": f"pages/result-{i}.png"} for i in range(28)]
        with self.assertRaises(ValueError):
            append_song_select(json.dumps(parent).encode())

    def test_conflicting_resource_identity_is_rejected(self):
        for key, value in (("id", "ss-row"), ("path", "PAGES/SONGSELECT33/listb_lightning.png")):
            parent = self.parent()
            parent["results"]["resources"][0][key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                append_song_select(json.dumps(parent).encode())

    def test_only_proven_originals_and_native_host_roles_are_published(self):
        page = song_select_page()
        self.assertEqual(set(page), {"resources", "background", "listEdge", "row", "selectedRow", "brand"})
        self.assertEqual({page[name] for name in page if name != "resources"}, {asset.identifier for asset in ASSETS})
        self.assertEqual(len(ASSETS), 5)
        self.assertEqual(sum(asset.width * asset.height for asset in ASSETS), 182500)
        self.assertTrue(all(len(asset.sha256) == 64 and asset.destination.endswith(asset.name) for asset in ASSETS))
        self.assertTrue(all("_usr" not in asset.name and "decide" not in asset.name for asset in ASSETS))


if __name__ == "__main__":
    unittest.main()
