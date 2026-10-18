// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public class SkinGameplayScopeResolutionTest
    {
        [TestCase(BmsKeymode.Key5K, 5)]
        [TestCase(BmsKeymode.Key7K, 7)]
        [TestCase(BmsKeymode.Key9K_Bms, 9)]
        [TestCase(BmsKeymode.Key9K_Pms, 9)]
        [TestCase(BmsKeymode.Key14K, 14)]
        public void TestBmsUsesAuthoritativePlayableKeysWithoutAddingScratch(BmsKeymode keymode, int expected)
        {
            var beatmap = new BmsBeatmap { BmsInfo = new BmsBeatmapInfo { Keymode = keymode } };
            // The source metadata cannot override the actual BMS runtime keymode.
            beatmap.BeatmapInfo.Difficulty.CircleSize = 4;
            Assert.That(new BmsRuleset().GetGameplaySkinKeyCount(beatmap), Is.EqualTo(expected));
        }

        [TestCase(4, false, 4)]
        [TestCase(7, false, 7)]
        [TestCase(7, true, 14)]
        [TestCase(8, true, 16)]
        [TestCase(10, true, 20)]
        public void TestManiaUsesTotalActualStageColumns(int stageKeys, bool dual, int expected)
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(stageKeys));
            if (dual)
                beatmap.Stages.Add(new StageDefinition(stageKeys));
            beatmap.BeatmapInfo.Difficulty.CircleSize = 5;
            Assert.That(new ManiaRuleset().GetGameplaySkinKeyCount(beatmap), Is.EqualTo(expected));
        }

        [Test]
        public void TestEditorCompatibilityPredictionUsesBmsAndNativeManiaCounts()
        {
            var source = new BeatmapInfo { Ruleset = new BmsRuleset().RulesetInfo };
            source.Difficulty.CircleSize = 7;
            var mania = new ManiaRuleset();
            Assert.Multiple(() =>
            {
                Assert.That(mania.GetGameplaySkinKeyCount(source, Array.Empty<Mod>()), Is.EqualTo(7));
                source.Ruleset = mania.RulesetInfo;
                source.Difficulty.CircleSize = 14;
                Assert.That(mania.GetGameplaySkinKeyCount(source, Array.Empty<Mod>()), Is.EqualTo(14));
            });
        }

        [Test]
        public void TestSettingsEnumerationMatchesActualManiaInputVariants()
        {
            int[] actualCounts = new ManiaRuleset().AvailableVariants
                .Select(variant => variant >= (int)PlayfieldType.Dual ? variant - (int)PlayfieldType.Dual : variant)
                .Distinct().OrderBy(count => count).ToArray();
            Assert.That(SkinSelectionScope.GetSupportedKeyCounts("mania"), Is.EqualTo(actualCounts));
        }
    }
}
