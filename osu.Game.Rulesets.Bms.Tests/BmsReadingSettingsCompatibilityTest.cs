// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsReadingSettingsCompatibilityTest
    {
        [TestCase("SUD")]
        [TestCase("HID")]
        [TestCase("LIFT")]
        public void TestHistoricalReadingModsRemainRecognisedPlayableAndQueryComparable(string acronym)
        {
            var api = new APIMod { Acronym = acronym };
            api.Settings.Add("remember_gameplay_changes", false);
            if (acronym == "LIFT")
                api.Settings.Add("lift_units", 623f);
            else
            {
                api.Settings.Add("cover_percent", 623f);
                api.Settings.Add("cover_opacity", 418f);
            }
            var ruleset = new BmsRuleset();
            var stored = new ScoreInfo { Ruleset = ruleset.RulesetInfo, APIMods = new[] { api } };
            string original = stored.ModsJson;
            Mod restored = stored.Mods.Single();
            Assert.Multiple(() =>
            {
                Assert.That(restored, Is.Not.InstanceOf<UnknownMod>());
                Assert.That(restored.Type, Is.EqualTo(ModType.System));
                Assert.That(restored.HasImplementation, Is.True);
                Assert.That(restored.UserPlayable, Is.True);
                Assert.That(ruleset.CreateAllMods().Count(mod => mod.Acronym == acronym), Is.EqualTo(1));
                Assert.That(ruleset.GetModsFor(ModType.Conversion).Any(mod => mod.Acronym == acronym), Is.False);
                Assert.That(ModUtils.CheckModsBelongToRuleset(ruleset, new[] { restored }), Is.True);
                Assert.That(ModUtils.CheckValidForGameplay(new[] { restored }, out _), Is.True);
            });
            if (restored is BmsModLaneCover cover)
            {
                Assert.That(cover.CoverPercent.Value, Is.EqualTo(623));
                Assert.That(cover.CoverOpacity.Value, Is.EqualTo(418));
                Assert.That(cover.RememberGameplayChanges.Value, Is.False);
            }
            else
            {
                var lift = (BmsModLift)restored;
                Assert.That(lift.LiftUnits.Value, Is.EqualTo(623));
                Assert.That(lift.RememberGameplayChanges.Value, Is.False);
            }
            Assert.That(BmsPersonalBestModComparison.Normalize(stored.APIMods), Is.Empty);
            Assert.That(stored.ModsJson, Is.EqualTo(original), "Recognising old reading Mods must not rewrite the historical settings payload.");
        }

        [TestCase(6)]
        [TestCase(7)]
        public void TestHistoricalScoreWithoutSnapshotKeepsItsVersionResultsAndLegacyModParameters(int version)
        {
            var legacy = new BmsModSudden { CoverPercent = { Value = 681 }, CoverOpacity = { Value = 427 } };
            var stored = new ScoreInfo { Ruleset = new BmsRuleset().RulesetInfo, Mods = new Mod[] { legacy }, TotalScore = 8 };
            stored.Statistics[HitResult.Perfect] = 4;
            stored.MaximumStatistics[HitResult.Perfect] = 4;
            stored.SetRulesetData(new BmsScoreInfoData
            {
                Version = version,
                FinalGauge = 0.82,
                ClearLamp = BmsClearLamp.NormalClear,
                GaugeRulesFamily = BmsGaugeRulesFamily.LR2,
            });
            string originalMods = stored.ModsJson;
            Assert.That(JObject.Parse(stored.RulesetDataJson).Property("reading_settings"), Is.Null);
            BmsScoreInfoData before = stored.GetRulesetData<BmsScoreInfoData>()!;
            Assert.That(before.ReadingSettings, Is.Null);
            new BmsRuleset().PrepareScoreInfoForResults(stored, new BmsBeatmap());
            BmsScoreInfoData after = stored.GetRulesetData<BmsScoreInfoData>()!;
            Assert.Multiple(() =>
            {
                Assert.That(after.ReadingSettings, Is.Null);
                Assert.That(after.Version, Is.EqualTo(version));
                Assert.That(after.FinalGauge, Is.EqualTo(0.82));
                Assert.That(after.ClearLamp, Is.EqualTo(BmsClearLamp.NormalClear));
                Assert.That(stored.TotalScore, Is.EqualTo(8));
                Assert.That(stored.Statistics[HitResult.Perfect], Is.EqualTo(4));
                Assert.That(stored.MaximumStatistics[HitResult.Perfect], Is.EqualTo(4));
                Assert.That(stored.ModsJson, Is.EqualTo(originalMods));
            });
            var restored = (BmsModSudden)stored.Mods.Single();
            Assert.That(restored.CoverPercent.Value, Is.EqualTo(681));
            Assert.That(restored.CoverOpacity.Value, Is.EqualTo(427));
            Assert.That(JObject.Parse(stored.RulesetDataJson).Property("reading_settings"), Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestNewRecordingKeepsItsInitialThirteenReadingValuesThroughFlushAndReplayTail(bool enabled)
        {
            BmsReadingSettingsSnapshot initial = snapshot(enabled);
            initial.Validate();
            var score = new Score
            {
                ScoreInfo = new ScoreInfo
                {
                    Ruleset = new BmsRuleset().RulesetInfo,
                    User = new APIUser { Id = 12345, Username = "Reading compatibility fixture" },
                    Mods = new Mod[] { new BmsModMirror() },
                    TotalScore = 8,
                },
            };
            score.ScoreInfo.Statistics[HitResult.Perfect] = 4;
            score.ScoreInfo.MaximumStatistics[HitResult.Perfect] = 4;
            string originalMods = score.ScoreInfo.ModsJson;
            var offsets = new BmsVisualOffsetTimeline { InitialOffset = 12 };
            using var recorder = new BmsReplayRecorder(score, offsets, initial);
            Assert.That(score.ScoreInfo.GetRulesetData<BmsScoreInfoData>()!.ReadingSettings, Is.EqualTo(initial));
            // Later runtime/result data cannot replace the one entry snapshot captured by the recorder.
            BmsScoreInfoData later = score.ScoreInfo.GetRulesetData<BmsScoreInfoData>()!;
            later.ReadingSettings = snapshot(!enabled);
            later.FinalGauge = 0.91;
            later.ClearLamp = BmsClearLamp.FullCombo;
            score.ScoreInfo.SetRulesetData(later);
            offsets.Record(1200, 15);
            recorder.EndRecording();
            BmsScoreInfoData saved = score.ScoreInfo.GetRulesetData<BmsScoreInfoData>()!;
            Assert.Multiple(() =>
            {
                Assert.That(saved.Version, Is.EqualTo(BmsScoreInfoData.TOTAL_RULES_VERSION));
                Assert.That(saved.ReadingSettings, Is.EqualTo(initial));
                Assert.That(saved.FinalGauge, Is.EqualTo(0.91));
                Assert.That(saved.ClearLamp, Is.EqualTo(BmsClearLamp.FullCombo));
                Assert.That(saved.VisualOffset!.OffsetAt(1300), Is.EqualTo(15));
                Assert.That(score.ScoreInfo.ModsJson, Is.EqualTo(originalMods));
                Assert.That(score.ScoreInfo.Mods.Any(mod => mod is BmsModLaneCover or BmsModLift), Is.False);
                Assert.That(score.ScoreInfo.TotalScore, Is.EqualTo(8));
                Assert.That(score.ScoreInfo.Statistics[HitResult.Perfect], Is.EqualTo(4));
                Assert.That(score.ScoreInfo.MaximumStatistics[HitResult.Perfect], Is.EqualTo(4));
            });
            var readingJson = (JObject)JObject.Parse(score.ScoreInfo.RulesetDataJson)["reading_settings"]!;
            Assert.That(readingJson.Properties().Count(), Is.EqualTo(13));
            LegacyReplaySoloScoreInfo exported = LegacyReplaySoloScoreInfo.FromScore(score.ScoreInfo);
            LegacyReplaySoloScoreInfo imported = JsonConvert.DeserializeObject<LegacyReplaySoloScoreInfo>(JsonConvert.SerializeObject(exported))!;
            var replay = new ScoreInfo { Ruleset = new BmsRuleset().RulesetInfo, APIMods = imported.Mods, RulesetDataJson = imported.RulesetDataJson! };
            BmsScoreInfoData decoded = replay.GetRulesetData<BmsScoreInfoData>()!;
            Assert.That(decoded.ReadingSettings, Is.EqualTo(initial));
            decoded.ReadingSettings!.Value.Validate();
            Assert.That(replay.ModsJson, Is.EqualTo(originalMods));
            Assert.That(imported.Statistics[HitResult.Perfect], Is.EqualTo(4));
            Assert.That(imported.MaximumStatistics[HitResult.Perfect], Is.EqualTo(4));
        }

        [Test]
        public void TestRecorderWithoutReadingSourceDoesNotInventAConfigurationSnapshot()
        {
            var score = new Score { ScoreInfo = new ScoreInfo { Ruleset = new BmsRuleset().RulesetInfo, Mods = new Mod[] { new BmsModLift { LiftUnits = { Value = 713 } } } } };
            string original = score.ScoreInfo.ModsJson;
            using var recorder = new BmsReplayRecorder(score);
            recorder.EndRecording();
            Assert.That(score.ScoreInfo.GetRulesetData<BmsScoreInfoData>()!.ReadingSettings, Is.Null);
            Assert.That(JObject.Parse(score.ScoreInfo.RulesetDataJson).Property("reading_settings"), Is.Null);
            Assert.That(score.ScoreInfo.ModsJson, Is.EqualTo(original));
        }

        private static BmsReadingSettingsSnapshot snapshot(bool enabled)
            => new BmsReadingSettingsSnapshot(BmsHiSpeedMode.Floating, 9, 2.75, 1.75,
                enabled, enabled, enabled, !enabled, 681, 143, 369, 427, 812);
    }
}
