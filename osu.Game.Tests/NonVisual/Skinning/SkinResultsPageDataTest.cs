// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning.Pages;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class SkinResultsPageDataTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestPresentationUsesTheSelectedScoresRulesetAndDisplayCountsWithoutMutatingIt(bool bms)
        {
            Ruleset ruleset = bms ? new BmsRuleset() : new ManiaRuleset();
            var score = TestResources.CreateTestScoreInfo(ruleset.RulesetInfo);
            score.Mods = Array.Empty<Mod>();
            score.TotalScore = 12345;
            score.Accuracy = 0.9876;
            score.MaxCombo = 42;
            var originalStatistics = new Dictionary<HitResult, int>(score.Statistics);
            string originalMods = score.ModsJson;
            SkinResultsPageData data = SkinResultsPageData.FromScore(score, 98765);
            Assert.Multiple(() =>
            {
                Assert.That(data.GetText("score.value"), Is.EqualTo("98,765"));
                Assert.That(data.GetText("score.accuracy"), Is.EqualTo("98.76%"));
                Assert.That(SkinResultsPageData.IsAllowedSource("score.accuracy.label"), Is.True);
                Assert.That(data.GetText("score.accuracy.label"), Is.EqualTo("ACCURACY"));
                Assert.That(data.GetText("score.max-combo"), Is.EqualTo("42"));
                Assert.That(SkinResultsPageData.IsAllowedSource("score.max-combo.label"), Is.True);
                Assert.That(data.GetText("score.max-combo.label"), Is.EqualTo("MAX COMBO"));
                Assert.That(data.GetText("score.label"), Is.EqualTo(ruleset.GetResultsScoreLabel(score)?.ToString() ?? string.Empty));
                Assert.That(data.GetText("hit.perfect"), Is.EqualTo(ruleset.GetDisplayCountForHitResult(score, HitResult.Perfect).ToString()));
                Assert.That(score.TotalScore, Is.EqualTo(12345));
                Assert.That(score.Statistics, Is.EquivalentTo(originalStatistics));
                Assert.That(score.ModsJson, Is.EqualTo(originalMods));
                Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)data.Values).Clear());
            });
            score.Statistics[HitResult.Perfect] = 7;
            Assert.That(data.GetText("hit.perfect"), Is.EqualTo(originalStatistics[HitResult.Perfect].ToString()));
        }

        [Test]
        public void TestInapplicableCountsAreUnavailableAndInvalidSourcesFail()
        {
            var score = TestResources.CreateTestScoreInfo(new ManiaRuleset().RulesetInfo);
            SkinResultsPageData data = SkinResultsPageData.FromScore(score, 0);
            Assert.That(data.GetText("score.value"), Is.EqualTo("0"));
            Assert.That(data.GetText("hit.small_bonus"), Is.Empty);
            Assert.Throws<ArgumentException>(() => data.GetText("fake.score"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestDefaultRulesetClearOutcomeUsesItsRealPassedState(bool passed)
        {
            var score = TestResources.CreateTestScoreInfo(new ManiaRuleset().RulesetInfo);
            score.Passed = passed;
            SkinResultsPageData data = SkinResultsPageData.FromScore(score, 0);
            Assert.That(data.GetBoolean("score.passed"), Is.EqualTo(passed));
            Assert.That(data.GetBoolean("score.cleared"), Is.EqualTo(passed));
        }

        [Test]
        public void TestBmsWithoutReliableStoredClearDoesNotBorrowPassed()
        {
            var score = TestResources.CreateTestScoreInfo(new BmsRuleset().RulesetInfo);
            score.Mods = Array.Empty<Mod>();
            score.Passed = true;
            SkinResultsPageData data = SkinResultsPageData.FromScore(score, 0);
            Assert.That(data.GetBoolean("score.passed"), Is.True);
            Assert.That(data.GetBoolean("score.cleared"), Is.Null);
            Assert.That(data.GetText("score.cleared"), Is.Empty);
        }

        [Test]
        public void TestMetadataIsBoundedByUnicodeCharactersAndRetainedWithoutScoreReferences()
        {
            var score = TestResources.CreateTestScoreInfo(new ManiaRuleset().RulesetInfo);
            score.BeatmapInfo!.Metadata.Title = string.Concat(System.Linq.Enumerable.Repeat("🎹", 300));
            score.BeatmapInfo.Metadata.Artist = "Artist\nName";
            SkinResultsPageData data = SkinResultsPageData.FromScore(score, 1);
            Assert.That(data.GetText("beatmap.title"), Has.Length.EqualTo(256));
            Assert.That(data.GetText("beatmap.artist"), Is.EqualTo("ArtistName"));
            score.BeatmapInfo.Metadata.Title = "changed";
            Assert.That(data.GetText("beatmap.title"), Has.Length.EqualTo(256));
        }
    }
}
