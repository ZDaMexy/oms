// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Game.Online.API;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsPersonalBestModComparisonTest
    {
        [TestCase("SUD")]
        [TestCase("HID")]
        [TestCase("LIFT")]
        public void TestCoverPresencePositionOpacityAndRememberDoNotPartitionTheQuery(string acronym)
        {
            APIMod selected = api(acronym, ("remember_gameplay_changes", false));
            if (acronym == "LIFT")
                selected.Settings.Add("lift_units", 620);
            else
            {
                selected.Settings.Add("cover_percent", 620);
                selected.Settings.Add("cover_opacity", 300);
            }
            Assert.That(signature(new[] { selected }), Is.EqualTo(signature(Array.Empty<APIMod>())));
        }

        [TestCase("ASCR", "tint_scratch_notes", "scratch_tint_colour", "scratch_visibility")]
        [TestCase("ANOT", "tint_notes", "note_tint_colour", "note_visibility")]
        public void TestAssistTintIsIgnoredWhileAssistPresenceAndVisibilityRemainDistinct(string acronym, string tint, string colour, string visibility)
        {
            APIMod original = api(acronym);
            APIMod changed = api(acronym, (tint, false), (colour, "#123456"));
            Assert.That(signature(new[] { changed }), Is.EqualTo(signature(new[] { original })));
            Assert.That(signature(new[] { original }), Is.Not.EqualTo(signature(Array.Empty<APIMod>())));
            changed.Settings.Add(visibility, 1);
            Assert.That(signature(new[] { changed }), Is.Not.EqualTo(signature(new[] { original })));
        }

        [TestCase("ASCR", "scratch_visibility", 0)]
        [TestCase("ANOT", "note_visibility", 0)]
        [TestCase("RD", "random_mode", 0)]
        public void TestOnlyDeclaredDefaultSettingsAreEquivalentToOmission(string acronym, string setting, int value)
        {
            Assert.That(signature(new[] { api(acronym, (setting, value)) }), Is.EqualTo(signature(new[] { api(acronym) })));
            Assert.That(signature(new[] { api(acronym, (setting, value.ToString())) }), Is.Not.EqualTo(signature(new[] { api(acronym) })),
                "Do not parse or guess that a different stored JSON type represents the declared default.");
        }

        private static IEnumerable<TestCaseData> gameplayDifferences()
        {
            yield return new TestCaseData(new Mod[] { new BmsModAutoScratch() }, new Mod[] { new BmsModAutoNote() }).SetName("AssistKindsRemainDistinct");
            yield return new TestCaseData(new Mod[] { new BmsModJudgeIidx() }, new Mod[] { new BmsModJudgeBeatoraja() }).SetName("JudgeModesRemainDistinct");
            yield return new TestCaseData(new Mod[] { new BmsModGaugeHard() }, new Mod[] { new BmsModGaugeEasy() }).SetName("GaugeModesRemainDistinct");
            yield return new TestCaseData(new Mod[] { new BmsModChargeNote() }, new Mod[] { new BmsModHellChargeNote() }).SetName("ChargeModesRemainDistinct");
            yield return new TestCaseData(new Mod[] { new BmsModChargeNote() }, Array.Empty<Mod>()).SetName("LnAndCnRemainDistinct");
            yield return new TestCaseData(new Mod[] { new BmsModMirror() }, Array.Empty<Mod>()).SetName("MirrorPresenceRemainsDistinct");
            yield return new TestCaseData(new Mod[] { new BmsModRandom { Seed = { Value = 123 } } }, new Mod[] { new BmsModRandom { Seed = { Value = 456 } } }).SetName("RandomSeedsRemainDistinct");
            yield return new TestCaseData(new Mod[] { new BmsModRandom { RandomMode = { Value = BmsRandomMode.RRandom }, Seed = { Value = 123 } } },
                new Mod[] { new BmsModRandom { Seed = { Value = 123 } } }).SetName("RandomModesRemainDistinct");
            yield return new TestCaseData(new Mod[] { new BmsModRandom { CustomPattern = { Value = "7654321" }, Seed = { Value = 123 } } },
                new Mod[] { new BmsModRandom { CustomPattern = { Value = "1234567" }, Seed = { Value = 123 } } }).SetName("RandomPatternsRemainDistinct");
        }

        [TestCaseSource(nameof(gameplayDifferences))]
        public void TestGameplayDifferencesAreNotRemoved(Mod[] current, Mod[] saved)
            => Assert.That(signature(current.Select(mod => new APIMod(mod)).ToArray()), Is.Not.EqualTo(signature(saved.Select(mod => new APIMod(mod)).ToArray())));

        [TestCase("ASCR")]
        [TestCase("SUD")]
        [TestCase("HID")]
        [TestCase("LIFT")]
        [TestCase("FUTURE")]
        public void TestUnknownSettingPresenceValueAndJsonTypeRemainStrict(string acronym)
        {
            APIMod unknown = api(acronym, ("future_rule", 1));
            Assert.That(signature(new[] { unknown }), Is.Not.EqualTo(signature(new[] { api(acronym) })));
            Assert.That(signature(new[] { unknown }), Is.Not.EqualTo(signature(new[] { api(acronym, ("future_rule", 2)) })));
            Assert.That(signature(new[] { unknown }), Is.Not.EqualTo(signature(new[] { api(acronym, ("future_rule", "1")) })));
            Assert.That(BmsPersonalBestModComparison.Normalize(new[] { unknown }).Single().Acronym, Is.EqualTo(acronym));
        }

        [Test]
        public void TestUnknownModDoesNotInheritKnownTintOrCoverExceptions()
        {
            APIMod future = api("FUTURE", ("tint_notes", false), ("cover_percent", 100));
            Assert.That(signature(new[] { future }), Is.Not.EqualTo(signature(new[] { api("FUTURE") })));
            Assert.That(signature(new[] { api("FUTURE") }), Is.Not.EqualTo(signature(Array.Empty<APIMod>())));
        }

        [Test]
        public void TestUnknownCoverSettingsRemainWhileKnownDisplayFieldsCanDiffer()
        {
            APIMod before = api("SUD", ("future_rule", true), ("cover_percent", 50));
            APIMod after = api("SUD", ("future_rule", true), ("cover_percent", 900), ("cover_opacity", 0));
            Assert.That(signature(new[] { before }), Is.EqualTo(signature(new[] { after })));
            Assert.That(BmsPersonalBestModComparison.Normalize(new[] { before }), Has.Count.EqualTo(1));
        }

        [Test]
        public void TestProjectionDoesNotMutateOrShareOriginalApiModSettings()
        {
            APIMod[] original = { api("ASCR", ("tint_scratch_notes", false), ("scratch_visibility", 1)),
                api("FUTURE", ("nested", new JObject { ["value"] = 1 })) };
            string saved = new JArray(original.Select(JObject.FromObject)).ToString();
            IReadOnlyList<APIMod> projected = BmsPersonalBestModComparison.Normalize(original);
            Assert.That(new JArray(original.Select(JObject.FromObject)).ToString(), Is.EqualTo(saved));
            Assert.That(projected[0], Is.Not.SameAs(original[0]));
            Assert.That(projected[0].Settings, Is.Not.SameAs(original[0].Settings));
            ((JObject)projected[1].Settings["nested"])["value"] = 2;
            Assert.That(new JArray(original.Select(JObject.FromObject)).ToString(), Is.EqualTo(saved));
        }

        private static APIMod api(string acronym, params (string Key, object Value)[] settings)
            => new APIMod { Acronym = acronym, Settings = settings.ToDictionary(pair => pair.Key, pair => pair.Value) };

        private static string signature(IReadOnlyList<APIMod> mods)
            => new JArray(BmsPersonalBestModComparison.Normalize(mods).OrderBy(mod => mod.Acronym).Select(JObject.FromObject)).ToString();
    }
}
