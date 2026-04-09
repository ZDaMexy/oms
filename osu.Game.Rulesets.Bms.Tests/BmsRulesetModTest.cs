// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Input.Bindings;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsRulesetModTest
    {
        private readonly BmsBeatmapDecoder decoder = new BmsBeatmapDecoder();

        [Test]
        public void TestExposesLaneCoverMods()
        {
            var mods = new BmsRuleset().GetModsFor(ModType.DifficultyIncrease).ToArray();
            var topMods = mods.OfType<BmsModLaneCoverTop>().ToArray();
            var bottomMods = mods.OfType<BmsModLaneCoverBottom>().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(topMods, Has.Length.EqualTo(1));
                Assert.That(bottomMods, Has.Length.EqualTo(1));
            });
        }

        [Test]
        public void TestExposesGaugeMods()
        {
            var reductionMods = new BmsRuleset().GetModsFor(ModType.DifficultyReduction).ToArray();
            var increaseMods = new BmsRuleset().GetModsFor(ModType.DifficultyIncrease).ToArray();
            var assistEasyMods = reductionMods.OfType<BmsModGaugeAssistEasy>().ToArray();
            var easyMods = reductionMods.OfType<BmsModGaugeEasy>().ToArray();
            var gasMods = increaseMods.OfType<BmsModGaugeAutoShift>().ToArray();
            var hardMods = increaseMods.OfType<BmsModGaugeHard>().ToArray();
            var exHardMods = increaseMods.OfType<BmsModGaugeExHard>().ToArray();
            var hazardMods = increaseMods.OfType<BmsModGaugeHazard>().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(assistEasyMods, Has.Length.EqualTo(1));
                Assert.That(easyMods, Has.Length.EqualTo(1));
                Assert.That(gasMods, Has.Length.EqualTo(1));
                Assert.That(hardMods, Has.Length.EqualTo(1));
                Assert.That(exHardMods, Has.Length.EqualTo(1));
                Assert.That(hazardMods, Has.Length.EqualTo(1));
            });
        }

        [Test]
        public void TestExposesLongNoteModeMods()
        {
            var mods = new BmsRuleset().GetModsFor(ModType.Conversion).ToArray();
            var beatorajaJudgeMods = mods.OfType<BmsModJudgeBeatoraja>().ToArray();
            var lr2JudgeMods = mods.OfType<BmsModJudgeLr2>().ToArray();
            var chargeNoteMods = mods.OfType<BmsModChargeNote>().ToArray();
            var hellChargeNoteMods = mods.OfType<BmsModHellChargeNote>().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(beatorajaJudgeMods, Has.Length.EqualTo(1));
                Assert.That(lr2JudgeMods, Has.Length.EqualTo(1));
                Assert.That(chargeNoteMods, Has.Length.EqualTo(1));
                Assert.That(hellChargeNoteMods, Has.Length.EqualTo(1));
            });
        }

        [Test]
        public void TestJudgeModeModsAreMutuallyExclusive()
        {
            var mods = new Mod[]
            {
                new BmsModJudgeBeatoraja(),
                new BmsModJudgeLr2(),
            };

            Assert.That(ModUtils.CheckCompatibleSet(mods, out var invalidMods), Is.False);
            Assert.That(invalidMods!.Select(mod => mod.GetType()), Does.Contain(typeof(BmsModJudgeBeatoraja)).And.Contain(typeof(BmsModJudgeLr2)));
        }

        [Test]
        public void TestLongNoteModeModsAreMutuallyExclusive()
        {
            var mods = new Mod[]
            {
                new BmsModChargeNote(),
                new BmsModHellChargeNote(),
            };

            Assert.That(ModUtils.CheckCompatibleSet(mods, out var invalidMods), Is.False);
            Assert.That(invalidMods!.Select(mod => mod.GetType()), Does.Contain(typeof(BmsModChargeNote)).And.Contain(typeof(BmsModHellChargeNote)));
        }

        [Test]
        public void TestGaugeModsAreMutuallyExclusive()
        {
            var mods = new Mod[]
            {
                new BmsModGaugeAutoShift(),
                new BmsModGaugeHard(),
            };

            Assert.That(ModUtils.CheckCompatibleSet(mods, out var invalidMods), Is.False);
            Assert.That(invalidMods!.Select(mod => mod.GetType()), Does.Contain(typeof(BmsModGaugeAutoShift)).And.Contain(typeof(BmsModGaugeHard)));
        }

        [Test]
        public void TestLaneCoverModsCreateTopAndBottomCovers()
        {
            var beatmap = createPlayableBeatmap();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap);

            var topMod = new BmsModLaneCoverTop();
            var bottomMod = new BmsModLaneCoverBottom();

            topMod.CoverPercent.Value = 35;
            bottomMod.CoverPercent.Value = 20;

            topMod.ApplyToDrawableRuleset(drawableRuleset);
            bottomMod.ApplyToDrawableRuleset(drawableRuleset);

            var laneCovers = drawableRuleset.Playfield.CoverContainer.Children.OfType<BmsLaneCover>().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(laneCovers, Has.Length.EqualTo(2));
                Assert.That(laneCovers.Single(cover => cover.CoverPosition == BmsLaneCoverPosition.Top).CoverPercent.Value, Is.EqualTo(35));
                Assert.That(laneCovers.Single(cover => cover.CoverPosition == BmsLaneCoverPosition.Bottom).CoverPercent.Value, Is.EqualTo(20));
                Assert.That(laneCovers.Single(cover => cover.CoverPosition == BmsLaneCoverPosition.Top).IsFocused.Value, Is.True);
                Assert.That(laneCovers.Single(cover => cover.CoverPosition == BmsLaneCoverPosition.Bottom).IsFocused.Value, Is.False);
                Assert.That(drawableRuleset.PlayfieldAdjustmentContainer.Children.OfType<BmsLaneCover>(), Is.Empty);
            });
        }

        [Test]
        public void TestScrollAdjustmentTargetsTopCoverByDefault()
        {
            var beatmap = createPlayableBeatmap();
            var topMod = new BmsModLaneCoverTop();
            var bottomMod = new BmsModLaneCoverBottom();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap, new Mod[] { topMod, bottomMod });

            topMod.ApplyToDrawableRuleset(drawableRuleset);
            bottomMod.ApplyToDrawableRuleset(drawableRuleset);

            Assert.That(drawableRuleset.AdjustLaneCover(1), Is.True);

            var laneCovers = drawableRuleset.Playfield.CoverContainer.Children.OfType<BmsLaneCover>().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(topMod.CoverPercent.Value, Is.EqualTo(51));
                Assert.That(bottomMod.CoverPercent.Value, Is.EqualTo(50));
                Assert.That(laneCovers.Single(cover => cover.CoverPosition == BmsLaneCoverPosition.Top).CoverPercent.Value, Is.EqualTo(51));
            });
        }

        [Test]
        public void TestExposesLaneCoverFocusDefaultBinding()
        {
            var bindings = new BmsRuleset().GetDefaultKeyBindings().ToArray();

            Assert.That(bindings.Select(binding => binding.Action), Does.Contain(BmsAction.LaneCoverFocus));
        }

        [TestCase(6, 13, 6, "5K")]
        [TestCase(8, 17, 8, "7K")]
        [TestCase(9, 9, 9, "9K")]
        [TestCase(16, 18, 16, "14K")]
        public void TestGameplayBindingsFollowVariant(int variant, int expectedBindingCount, int expectedDistinctActions, string expectedName)
        {
            var ruleset = new BmsRuleset();
            var bindings = ruleset.GetDefaultKeyBindings(variant).ToArray();
            var gameplayBindings = bindings.Where(binding => binding.Action is BmsAction action && action.IsLaneAction()).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(gameplayBindings, Has.Length.EqualTo(expectedBindingCount));
                Assert.That(gameplayBindings.Select(binding => binding.Action).Distinct().Count(), Is.EqualTo(expectedDistinctActions));
                Assert.That(ruleset.GetVariantName(variant).ToString(), Is.EqualTo(expectedName));
            });
        }

        [Test]
        public void TestScratchBindingsExposeDefaultSignals()
        {
            var bindings = new BmsRuleset().GetDefaultKeyBindings(8).Where(binding => binding.Action is BmsAction.Scratch1).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(bindings, Has.Length.EqualTo(3));
                Assert.That(bindings.SelectMany(binding => binding.KeyCombination.Keys).OrderBy(key => key).ToArray(), Is.EqualTo(new[] { InputKey.A, InputKey.Q, InputKey.Joystick5 }));
            });
        }

        [Test]
        public void TestFocusAdjustmentTargetsBottomCover()
        {
            var beatmap = createPlayableBeatmap();
            var topMod = new BmsModLaneCoverTop();
            var bottomMod = new BmsModLaneCoverBottom();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap, new Mod[] { topMod, bottomMod });

            topMod.ApplyToDrawableRuleset(drawableRuleset);
            bottomMod.ApplyToDrawableRuleset(drawableRuleset);

            Assert.That(drawableRuleset.AdjustLaneCover(1, preferBottom: true), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(topMod.CoverPercent.Value, Is.EqualTo(50));
                Assert.That(bottomMod.CoverPercent.Value, Is.EqualTo(51));
            });
        }

        [Test]
        public void TestFocusRefreshMarksBottomCoverAsFocused()
        {
            var beatmap = createPlayableBeatmap();
            var topMod = new BmsModLaneCoverTop();
            var bottomMod = new BmsModLaneCoverBottom();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap, new Mod[] { topMod, bottomMod });

            topMod.ApplyToDrawableRuleset(drawableRuleset);
            bottomMod.ApplyToDrawableRuleset(drawableRuleset);

            drawableRuleset.UpdateLaneCoverFocus(preferBottom: true);

            var laneCovers = drawableRuleset.Playfield.LaneCovers.ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(laneCovers.Single(cover => cover.CoverPosition == BmsLaneCoverPosition.Top).IsFocused.Value, Is.False);
                Assert.That(laneCovers.Single(cover => cover.CoverPosition == BmsLaneCoverPosition.Bottom).IsFocused.Value, Is.True);
            });
        }

        [Test]
        public void TestScrollAdjustmentFallsBackToBottomCover()
        {
            var beatmap = createPlayableBeatmap();
            var bottomMod = new BmsModLaneCoverBottom();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap, new Mod[] { bottomMod });

            bottomMod.ApplyToDrawableRuleset(drawableRuleset);

            Assert.That(drawableRuleset.AdjustLaneCover(-1), Is.True);

            var laneCovers = drawableRuleset.Playfield.CoverContainer.Children.OfType<BmsLaneCover>().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(bottomMod.CoverPercent.Value, Is.EqualTo(49));
                Assert.That(laneCovers.Single().CoverPercent.Value, Is.EqualTo(49));
            });
        }

        private BmsBeatmap createPlayableBeatmap()
        {
            const string text = @"
#TITLE Lane Cover Stub
#BPM 120
#00111:AA00
";

            var decodedChart = decoder.DecodeText(text, "lane-cover-stub.bme");
            return (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();
        }
    }
}
