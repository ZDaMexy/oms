"""Original20 identity isolation, complete alias contracts and unqualified geometry.

These tests use synthetic metadata only; no archives, art or private hashes.
"""
import copy
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import standard_source_catalog as catalog


def composition21():
    return {"sourceComposition": {"contract": catalog.COMPOSITION21,
                                  "aliases24": copy.deepcopy(catalog.EARLIER_ALIASES),
                                  "aliases22": copy.deepcopy(catalog.SOURCE22_ALIASES),
                                  "aliases21": copy.deepcopy(catalog.SOURCE21_ALIASES)}}


def composition20():
    data = composition21()
    data["sourceComposition"].update(contract=catalog.COMPOSITION20,
                                      aliases20=copy.deepcopy(catalog.SOURCE20_ALIASES))
    return data


class Original20IdentityTest(unittest.TestCase):
    def test_original20_has_a_separate_frame_and_six_own_core_aliases(self):
        self.assertEqual(catalog.SOURCE20_STYLES, ("source20-gmframe0",))
        self.assertEqual(catalog.SOURCE20_CORE_ALIASES,
                         {"gmframe0": "source20/gmframe0", "basic_frame": "s20/basic_frame",
                          "common": "s20/common", "gameparts": "s20/gameparts",
                          "graph": "s20/graph", "title": "s20/title"})
        self.assertEqual(catalog.SOURCE20_DISTINCT_CUSTOM_ALIASES,
                         {name: "source20/" + name for name in ("beam00", "notes00", "turn00")})
        self.assertEqual(catalog.SOURCE20_ALIASES,
                         {**catalog.SOURCE20_CORE_ALIASES, **catalog.SOURCE20_DISTINCT_CUSTOM_ALIASES})
        style = "source20-gmframe0"
        self.assertEqual(catalog.source_style(style), style)
        self.assertEqual(catalog.frame_id(style), style)
        self.assertEqual(catalog.frame_token(style), "20-gmframe0")
        self.assertEqual(catalog.package_key(style), "source20/gmframe0")
        self.assertEqual(catalog.layout_family(style), "s20")
        self.assertEqual(catalog.presentation_family(style), "s20")
        self.assertEqual(catalog.family_suffix("s20"), "fs20")

    def test_original20_does_not_relabel_the_existing18_themes(self):
        old, extended = composition21(), composition20()
        before = copy.deepcopy(old)
        old_styles = catalog.styles(old)
        self.assertEqual(len(old_styles), 18)
        self.assertEqual(catalog.styles(extended), (*old_styles, "source20-gmframe0"))
        self.assertEqual(catalog.retained_styles(extended),
                         (*catalog.retained_styles(old), "source20-gmframe0"))
        self.assertEqual(len(catalog.retained_styles(extended)), 20)
        self.assertEqual(set(catalog.retained_styles(extended)) - set(catalog.styles(extended)),
                         {"source22-gmframe0"})
        self.assertEqual(catalog.families(extended), (*catalog.families(old), "s20"))
        for helper in (catalog.frame_id, catalog.frame_token, catalog.package_key):
            new_values = [helper(style) for style in catalog.retained_styles(extended)]
            self.assertEqual(len(new_values), len(set(new_values)))
        self.assertEqual(catalog.package_key("source22-gmframe9"), "source22/gmframe9")
        self.assertEqual(catalog.package_key("source21-gmframe0"), "source21/gmframe0")
        self.assertEqual(catalog.package_key(21), "gmframe21")
        self.assertEqual(old, before)

    def test_original20_contract_retains_each_explicit_predecessor_family(self):
        extended = composition20()
        self.assertTrue(catalog.expanded(extended))
        self.assertTrue(catalog.includes22(extended))
        self.assertTrue(catalog.includes21(extended))
        self.assertTrue(catalog.includes20(extended))
        self.assertEqual(catalog.art_identity(extended), "IIDX20+IIDX21+IIDX22+IIDX24+IIDX26")
        self.assertEqual(catalog.art_identity(composition21()), "IIDX21+IIDX22+IIDX24+IIDX26")

    def test_each_alias_contract_is_required_without_missing_or_extra_entries(self):
        for family in ("aliases24", "aliases22", "aliases21", "aliases20"):
            for change in ("missing-contract", "missing-entry", "extra-entry", "wrong-owner"):
                with self.subTest(family=family, change=change):
                    data = composition20()
                    aliases = data["sourceComposition"][family]
                    if change == "missing-contract":
                        del data["sourceComposition"][family]
                    elif change == "missing-entry":
                        aliases.pop(next(iter(aliases)))
                    elif change == "extra-entry":
                        aliases["unqualified"] = "s20/unqualified"
                    else:
                        aliases[next(iter(aliases))] = "source22/gmframe9"
                    with self.assertRaisesRegex(ValueError, "Unrecognized"):
                        catalog.styles(data)

    def test_aliases_alone_do_not_promote_an_older_composition(self):
        for contract in (catalog.COMPOSITION, catalog.COMPOSITION22, catalog.COMPOSITION21):
            with self.subTest(contract=contract):
                data = composition21()
                data["sourceComposition"]["contract"] = contract
                expected = catalog.styles(data)
                expected_families = catalog.families(data)
                data["sourceComposition"]["aliases20"] = copy.deepcopy(catalog.SOURCE20_ALIASES)
                self.assertFalse(catalog.includes20(data))
                self.assertEqual(catalog.styles(data), expected)
                self.assertEqual(catalog.families(data), expected_families)
        self.assertFalse(catalog.includes20({}))

    def test_numeric20_and_guessed_frame_or_family_names_are_rejected(self):
        for value in (20, "20", "020", "iidx20", "２０", "source20-gmframe9",
                      "source20-gmframe0-00", "source20/gmframe0", True):
            with self.subTest(value=value), self.assertRaises(ValueError):
                catalog.source_style(value)
        for family in ("20", "source20", "s020", "s19"):
            with self.subTest(family=family), self.assertRaises(ValueError):
                catalog.family_suffix(family)

    def test_unknown_composition_cannot_infer_original20_from_valid_aliases(self):
        data = composition20()
        data["sourceComposition"]["contract"] = catalog.COMPOSITION20 + ".unknown"
        with self.assertRaisesRegex(ValueError, "Unrecognized"):
            catalog.includes20(data)

    def test_original20_uses_its_own_signed_movie_setter_and_separate_wide_dp(self):
        for layout, wide, expected in (("p1", False, 35), ("p2", False, -35),
                                       ("dp", False, 0), ("p1", True, 0), ("p2", True, 0)):
            with self.subTest(layout=layout, wide=wide):
                self.assertEqual(catalog.centre_translation("source20-gmframe0", layout, wide=wide), expected)
        with self.assertRaisesRegex(ValueError, "Unqualified Original20 layout"):
            catalog.centre_translation("source20-gmframe0", "guessed")
        self.assertEqual(catalog.centre_translation("source21-gmframe0", "p1"), 35)
        self.assertEqual(catalog.centre_translation("source22-gmframe9", "p2"), -35)
        self.assertEqual(catalog.centre_translation(26, "p1"), 36)
        self.assertEqual(catalog.centre_translation(26, "dp"), 0)


if __name__ == "__main__":
    unittest.main()
