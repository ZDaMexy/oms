// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Input.Bindings;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
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
            var mods = new BmsRuleset().GetModsFor(ModType.Conversion).ToArray();
            var suddenMods = mods.OfType<BmsModSudden>().ToArray();
            var hiddenMods = mods.OfType<BmsModHidden>().ToArray();
            var liftMods = mods.OfType<BmsModLift>().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(suddenMods, Has.Length.EqualTo(1));
                Assert.That(hiddenMods, Has.Length.EqualTo(1));
                Assert.That(liftMods, Has.Length.EqualTo(1));
                Assert.That(suddenMods.Single().Type, Is.EqualTo(ModType.Conversion));
                Assert.That(hiddenMods.Single().Type, Is.EqualTo(ModType.Conversion));
                Assert.That(liftMods.Single().Type, Is.EqualTo(ModType.Conversion));
            });
        }

        [Test]
        public void TestExposesMirrorAndRandomMods()
        {
            var mods = new BmsRuleset().GetModsFor(ModType.Conversion).ToArray();
            var mirrorMods = mods.OfType<BmsModMirror>().ToArray();
            var randomMods = mods.OfType<BmsModRandom>().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(mirrorMods, Has.Length.EqualTo(1));
                Assert.That(randomMods, Has.Length.EqualTo(1));
                Assert.That(mirrorMods.Single().Type, Is.EqualTo(ModType.Conversion));
                Assert.That(randomMods.Single().Type, Is.EqualTo(ModType.Conversion));
            });
        }

        [Test]
        public void TestExposesGaugeMods()
        {
            var reductionMods = new BmsRuleset().GetModsFor(ModType.DifficultyReduction).ToArray();
            var increaseMods = new BmsRuleset().GetModsFor(ModType.DifficultyIncrease).ToArray();
            var assistEasyMods = reductionMods.OfType<BmsModGaugeAssistEasy>().ToArray();
            var easyMods = reductionMods.OfType<BmsModGaugeEasy>().ToArray();
            var gasMods = reductionMods.OfType<BmsModGaugeAutoShift>().ToArray();
            var hardMods = increaseMods.OfType<BmsModGaugeHard>().ToArray();
            var exHardMods = increaseMods.OfType<BmsModGaugeExHard>().ToArray();
            var hazardMods = increaseMods.OfType<BmsModGaugeHazard>().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(assistEasyMods, Has.Length.EqualTo(1));
                Assert.That(easyMods, Has.Length.EqualTo(1));
                Assert.That(gasMods, Has.Length.EqualTo(1));
                Assert.That(gasMods.Single().Type, Is.EqualTo(ModType.DifficultyReduction));
                Assert.That(hardMods, Has.Length.EqualTo(1));
                Assert.That(exHardMods, Has.Length.EqualTo(1));
                Assert.That(hazardMods, Has.Length.EqualTo(1));
            });
        }

        [Test]
        public void TestGaugeModsAreMarkedImplementedForSelection()
        {
            var reductionMods = new BmsRuleset().GetModsFor(ModType.DifficultyReduction).ToArray();
            var increaseMods = new BmsRuleset().GetModsFor(ModType.DifficultyIncrease).ToArray();

            var gaugeMods = reductionMods.OfType<BmsModGauge>().Cast<Mod>()
                                         .Concat(increaseMods.OfType<BmsModGauge>().Cast<Mod>())
                                         .Append(reductionMods.OfType<BmsModGaugeAutoShift>().Single())
                                         .ToArray();

            Assert.That(gaugeMods, Is.Not.Empty);
            Assert.That(gaugeMods.All(mod => mod.HasImplementation), Is.True);
        }

        [Test]
        public void TestExposesAutoScratchAutoNoteAndAutoplayMods()
        {
            var reductionMods = new BmsRuleset().GetModsFor(ModType.DifficultyReduction).ToArray();
            var autoScratchMods = reductionMods.OfType<BmsModAutoScratch>().ToArray();
            var autoNoteMods = reductionMods.OfType<BmsModAutoNote>().ToArray();
            var autoplayMods = reductionMods.OfType<BmsModAutoplay>().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(autoScratchMods, Has.Length.EqualTo(1));
                Assert.That(autoNoteMods, Has.Length.EqualTo(1));
                Assert.That(autoplayMods, Has.Length.EqualTo(1));
                Assert.That(autoScratchMods.Single().Type, Is.EqualTo(ModType.DifficultyReduction));
                Assert.That(autoNoteMods.Single().Type, Is.EqualTo(ModType.DifficultyReduction));
                Assert.That(autoplayMods.Single().Type, Is.EqualTo(ModType.DifficultyReduction));
            });
        }

        [Test]
        public void TestAutoAssistModsAreMutuallyIncompatibleAndRemainIncompatibleWithAutoplay()
        {
            var autoScratch = new BmsModAutoScratch();
            var autoNote = new BmsModAutoNote();
            var autoplay = new BmsModAutoplay();

            Assert.Multiple(() =>
            {
                Assert.That(ModUtils.CheckCompatibleSet(new Mod[] { autoScratch, autoNote }), Is.False);
                Assert.That(ModUtils.CheckCompatibleSet(new Mod[] { autoScratch, autoplay }), Is.False);
                Assert.That(ModUtils.CheckCompatibleSet(new Mod[] { autoNote, autoplay }), Is.False);
            });
        }

        [Test]
        public void TestRulesetReturnsBmsAutoplayMod()
        {
            var autoplay = new BmsRuleset().GetAutoplayMod();

            Assert.Multiple(() =>
            {
                Assert.That(autoplay, Is.TypeOf<BmsModAutoplay>());
                Assert.That(autoplay!.Type, Is.EqualTo(ModType.DifficultyReduction));
            });
        }

        [Test]
        public void TestRulesetCreatesBmsConvertibleReplayFrame()
            => Assert.That(new BmsRuleset().CreateConvertibleReplayFrame(), Is.TypeOf<BmsReplayFrame>());

        [Test]
        public void TestExposesLongNoteModeMods()
        {
            var mods = new BmsRuleset().GetModsFor(ModType.Conversion).ToArray();
            var judgeRankMods = mods.OfType<BmsModJudgeRank>().ToArray();
            var beatorajaJudgeMods = mods.OfType<BmsModJudgeBeatoraja>().ToArray();
            var lr2JudgeMods = mods.OfType<BmsModJudgeLr2>().ToArray();
            var iidxJudgeMods = mods.OfType<BmsModJudgeIidx>().ToArray();
            var chargeNoteMods = mods.OfType<BmsModChargeNote>().ToArray();
            var hellChargeNoteMods = mods.OfType<BmsModHellChargeNote>().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(judgeRankMods, Has.Length.EqualTo(1));
                Assert.That(beatorajaJudgeMods, Has.Length.EqualTo(1));
                Assert.That(lr2JudgeMods, Has.Length.EqualTo(1));
                Assert.That(iidxJudgeMods, Has.Length.EqualTo(1));
                Assert.That(chargeNoteMods, Has.Length.EqualTo(1));
                Assert.That(hellChargeNoteMods, Has.Length.EqualTo(1));
            });
        }

        [Test]
        public void TestLongNoteModeModsAreMarkedImplementedForSelection()
        {
            var longNoteModeMods = new BmsRuleset().GetModsFor(ModType.Conversion)
                                                   .OfType<BmsModLongNoteMode>()
                                                   .Cast<Mod>()
                                                   .ToArray();

            Assert.That(longNoteModeMods, Is.Not.Empty);
            Assert.That(longNoteModeMods.All(mod => mod.HasImplementation), Is.True);
        }

        [Test]
        public void TestJudgeRankModIsMarkedImplementedForSelection()
        {
            var judgeRankMod = new BmsRuleset().GetModsFor(ModType.Conversion).OfType<BmsModJudgeRank>().Single();

            Assert.That(judgeRankMod.HasImplementation, Is.True);
        }

        [Test]
        public void TestJudgeModeModsAreMarkedImplementedForSelection()
        {
            var judgeModeMods = new BmsRuleset().GetModsFor(ModType.Conversion)
                                                .OfType<BmsModJudgeMode>()
                                                .Cast<Mod>()
                                                .ToArray();

            Assert.That(judgeModeMods, Is.Not.Empty);
            Assert.That(judgeModeMods.All(mod => mod.HasImplementation), Is.True);
        }

        [Test]
        public void TestJudgeModeModsRemainCompatibleWithJudgeRankMod()
        {
            var judgeRankMod = new BmsModJudgeRank();

            Assert.Multiple(() =>
            {
                Assert.That(ModUtils.CheckCompatibleSet(new Mod[] { new BmsModJudgeBeatoraja(), judgeRankMod }, out _), Is.True);
                Assert.That(ModUtils.CheckCompatibleSet(new Mod[] { new BmsModJudgeLr2(), judgeRankMod }, out _), Is.True);
            });
        }

        [Test]
        public void TestIidxJudgeModeIsIncompatibleWithJudgeDifficultyMod()
        {
            var mods = new Mod[]
            {
                new BmsModJudgeIidx(),
                new BmsModJudgeRank(),
            };

            Assert.That(ModUtils.CheckCompatibleSet(mods, out var invalidMods), Is.False);
            Assert.That(invalidMods!.Select(mod => mod.GetType()), Does.Contain(typeof(BmsModJudgeIidx)).And.Contain(typeof(BmsModJudgeRank)));
        }

        [Test]
        public void TestJudgeModeModsAreMutuallyExclusive()
        {
            var mods = new Mod[]
            {
                new BmsModJudgeBeatoraja(),
                new BmsModJudgeLr2(),
                new BmsModJudgeIidx(),
            };

            Assert.That(ModUtils.CheckCompatibleSet(mods, out var invalidMods), Is.False);
            Assert.That(invalidMods!.Select(mod => mod.GetType()), Does.Contain(typeof(BmsModJudgeBeatoraja)).And.Contain(typeof(BmsModJudgeLr2)).And.Contain(typeof(BmsModJudgeIidx)));
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
        public void TestMirrorAndRandomModsAreMutuallyExclusive()
        {
            var mods = new Mod[]
            {
                new BmsModMirror(),
                new BmsModRandom(),
            };

            Assert.That(ModUtils.CheckCompatibleSet(mods, out var invalidMods), Is.False);
            Assert.That(invalidMods!.Select(mod => mod.GetType()), Does.Contain(typeof(BmsModMirror)).And.Contain(typeof(BmsModRandom)));
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
            var suddenMod = new BmsModSudden();
            var hiddenMod = new BmsModHidden();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap, new Mod[] { suddenMod, hiddenMod });

            suddenMod.CoverPercent.Value = 350;
            hiddenMod.CoverPercent.Value = 200;

            suddenMod.ApplyToDrawableRuleset(drawableRuleset);
            hiddenMod.ApplyToDrawableRuleset(drawableRuleset);

            var laneCovers = drawableRuleset.Playfield.CoverContainer.Children.OfType<BmsLaneCover>().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(laneCovers, Has.Length.EqualTo(2));
                Assert.That(laneCovers.Single(cover => cover.CoverPosition == BmsLaneCoverPosition.Sudden).CoverPercent.Value, Is.EqualTo(350));
                Assert.That(laneCovers.Single(cover => cover.CoverPosition == BmsLaneCoverPosition.Hidden).CoverPercent.Value, Is.EqualTo(200));
                Assert.That(laneCovers.Single(cover => cover.CoverPosition == BmsLaneCoverPosition.Sudden).IsFocused.Value, Is.True);
                Assert.That(laneCovers.Single(cover => cover.CoverPosition == BmsLaneCoverPosition.Hidden).IsFocused.Value, Is.False);
                Assert.That(drawableRuleset.PlayfieldAdjustmentContainer.Children.OfType<BmsLaneCover>(), Is.Empty);
            });
        }

        [Test]
        public void TestScrollAdjustmentTargetsTopCoverByDefault()
        {
            var beatmap = createPlayableBeatmap();
            var suddenMod = new BmsModSudden();
            var hiddenMod = new BmsModHidden();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap, new Mod[] { suddenMod, hiddenMod });

            suddenMod.ApplyToDrawableRuleset(drawableRuleset);
            hiddenMod.ApplyToDrawableRuleset(drawableRuleset);

            Assert.That(drawableRuleset.AdjustLaneCover(1), Is.True);

            var laneCovers = drawableRuleset.Playfield.CoverContainer.Children.OfType<BmsLaneCover>().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(suddenMod.CoverPercent.Value, Is.EqualTo(251));
                Assert.That(hiddenMod.CoverPercent.Value, Is.EqualTo(250));
                Assert.That(laneCovers.Single(cover => cover.CoverPosition == BmsLaneCoverPosition.Sudden).CoverPercent.Value, Is.EqualTo(251));
            });
        }

        [Test]
        public void TestExposesLaneCoverFocusDefaultBinding()
        {
            var bindings = new BmsRuleset().GetDefaultKeyBindings().ToArray();
            var laneCoverBindings = bindings.Where(binding => binding.Action is BmsAction.LaneCoverFocus).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(bindings.Select(binding => binding.Action), Does.Contain(BmsAction.LaneCoverFocus));
                Assert.That(laneCoverBindings, Has.Length.EqualTo(1));
                Assert.That(laneCoverBindings[0].KeyCombination.Keys.ToArray(), Is.EqualTo(new[] { InputKey.W }));
            });
        }

        [TestCase(6, 7, 6, "5K")]
        [TestCase(8, 9, 8, "7K")]
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
                Assert.That(bindings, Has.Length.EqualTo(2));
                Assert.That(bindings.SelectMany(binding => binding.KeyCombination.Keys).OrderBy(key => key).ToArray(), Is.EqualTo(new[] { InputKey.LShift, InputKey.RShift }));
            });
        }

        [Test]
        public void TestFocusAdjustmentTargetsBottomCover()
        {
            var beatmap = createPlayableBeatmap();
            var suddenMod = new BmsModSudden();
            var hiddenMod = new BmsModHidden();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap, new Mod[] { suddenMod, hiddenMod });

            suddenMod.ApplyToDrawableRuleset(drawableRuleset);
            hiddenMod.ApplyToDrawableRuleset(drawableRuleset);

            Assert.That(drawableRuleset.AdjustLaneCover(1, preferBottom: true), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(suddenMod.CoverPercent.Value, Is.EqualTo(250));
                Assert.That(hiddenMod.CoverPercent.Value, Is.EqualTo(251));
            });
        }

        [Test]
        public void TestFocusRefreshMarksBottomCoverAsFocused()
        {
            var beatmap = createPlayableBeatmap();
            var suddenMod = new BmsModSudden();
            var hiddenMod = new BmsModHidden();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap, new Mod[] { suddenMod, hiddenMod });

            suddenMod.ApplyToDrawableRuleset(drawableRuleset);
            hiddenMod.ApplyToDrawableRuleset(drawableRuleset);

            drawableRuleset.UpdateLaneCoverFocus(preferBottom: true);

            var laneCovers = drawableRuleset.Playfield.LaneCovers.ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(laneCovers.Single(cover => cover.CoverPosition == BmsLaneCoverPosition.Sudden).IsFocused.Value, Is.False);
                Assert.That(laneCovers.Single(cover => cover.CoverPosition == BmsLaneCoverPosition.Hidden).IsFocused.Value, Is.True);
            });
        }

        [Test]
        public void TestTemporaryBottomFocusDoesNotOverwritePersistentTarget()
        {
            var beatmap = createPlayableBeatmap();
            var suddenMod = new BmsModSudden();
            var hiddenMod = new BmsModHidden();
            var liftMod = new BmsModLift();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap, new Mod[] { suddenMod, hiddenMod, liftMod });

            suddenMod.ApplyToDrawableRuleset(drawableRuleset);
            hiddenMod.ApplyToDrawableRuleset(drawableRuleset);
            liftMod.ApplyToDrawableRuleset(drawableRuleset);

            Assert.That(drawableRuleset.CycleGameplayAdjustmentTarget(), Is.True);
            Assert.That(drawableRuleset.CycleGameplayAdjustmentTarget(), Is.True);

            drawableRuleset.UpdateLaneCoverFocus(preferBottom: true);

            Assert.Multiple(() =>
            {
                Assert.That(drawableRuleset.ActiveAdjustmentTarget.Value, Is.EqualTo(BmsGameplayAdjustmentTarget.Hidden));
                Assert.That(drawableRuleset.ActiveAdjustmentTargetIndex.Value, Is.EqualTo(1));
                Assert.That(drawableRuleset.IsAdjustmentTargetTemporarilyOverridden.Value, Is.True);
                Assert.That(drawableRuleset.Playfield.LaneCovers.Single(cover => cover.CoverPosition == BmsLaneCoverPosition.Hidden).IsFocused.Value, Is.True);
            });

            assertGameplayFeedbackStateTargetState(drawableRuleset, BmsGameplayAdjustmentTarget.Hidden, 3, 1, true);

            drawableRuleset.RefreshLaneCoverFocus();

            Assert.Multiple(() =>
            {
                Assert.That(drawableRuleset.ActiveAdjustmentTarget.Value, Is.EqualTo(BmsGameplayAdjustmentTarget.Lift));
                Assert.That(drawableRuleset.ActiveAdjustmentTargetIndex.Value, Is.EqualTo(2));
                Assert.That(drawableRuleset.IsAdjustmentTargetTemporarilyOverridden.Value, Is.False);
                Assert.That(drawableRuleset.Playfield.LaneCovers.All(cover => !cover.IsFocused.Value), Is.True);
            });

            assertGameplayFeedbackStateTargetState(drawableRuleset, BmsGameplayAdjustmentTarget.Lift, 3, 2, false);
        }

        [Test]
        public void TestScrollSpeedMetricsReflectAppliedLaneCovers()
        {
            var beatmap = createPlayableBeatmap();
            var suddenMod = new BmsModSudden();
            var hiddenMod = new BmsModHidden();
            var liftMod = new BmsModLift();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap, new Mod[] { suddenMod, hiddenMod, liftMod });

            suddenMod.ApplyToDrawableRuleset(drawableRuleset);
            hiddenMod.ApplyToDrawableRuleset(drawableRuleset);
            liftMod.ApplyToDrawableRuleset(drawableRuleset);

            suddenMod.CoverPercent.Value = 350;
            hiddenMod.CoverPercent.Value = 200;
            liftMod.LiftUnits.Value = 150;

            var metrics = drawableRuleset.GetScrollSpeedMetrics();

            Assert.Multiple(() =>
            {
                Assert.That(metrics.SuddenUnits, Is.EqualTo(350));
                Assert.That(metrics.HiddenUnits, Is.EqualTo(200));
                Assert.That(metrics.LiftUnits, Is.EqualTo(150));
                Assert.That(metrics.WhiteNumber, Is.EqualTo(350));
                Assert.That(metrics.VisibleLaneUnits, Is.EqualTo(450));
            });
        }

        [Test]
        public void TestGameplayFeedbackStateMirrorsInitialPacemaker()
        {
            var beatmap = createPlayableBeatmap();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap, null);

            Assert.That(drawableRuleset.ExScorePacemakerInfo.Value, Is.EqualTo(drawableRuleset.GameplayFeedbackState.Value.ExScorePacemakerInfo));

            if (!drawableRuleset.GameplayFeedbackState.Value.ExScorePacemakerInfo.HasValue)
                return;

            Assert.Multiple(() =>
            {
                Assert.That(drawableRuleset.GameplayFeedbackState.Value.ExScorePacemakerInfo!.Value.CurrentExScore, Is.EqualTo(0));
                Assert.That(drawableRuleset.GameplayFeedbackState.Value.ExScorePacemakerInfo!.Value.JudgedHits, Is.EqualTo(0));
            });
        }

        [Test]
        public void TestGameplayFeedbackStateStartsWithZeroJudgementCounts()
        {
            var beatmap = createPlayableBeatmap();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap, null);

            Assert.Multiple(() =>
            {
                Assert.That(drawableRuleset.GameplayFeedbackState.Value.JudgementCounts.PerfectCount, Is.EqualTo(0));
                Assert.That(drawableRuleset.GameplayFeedbackState.Value.JudgementCounts.GreatCount, Is.EqualTo(0));
                Assert.That(drawableRuleset.GameplayFeedbackState.Value.JudgementCounts.GoodCount, Is.EqualTo(0));
                Assert.That(drawableRuleset.GameplayFeedbackState.Value.JudgementCounts.BadCount, Is.EqualTo(0));
                Assert.That(drawableRuleset.GameplayFeedbackState.Value.JudgementCounts.PoorCount, Is.EqualTo(0));
                Assert.That(drawableRuleset.GameplayFeedbackState.Value.JudgementCounts.EmptyPoorCount, Is.EqualTo(0));
            });
        }

        [Test]
        public void TestGameplayFeedbackStateMirrorsInitialExScoreProgress()
        {
            var beatmap = createPlayableBeatmap();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap, null);

            BmsExScoreProgressInfo? expectedProgressInfo = drawableRuleset.ExScorePacemakerInfo.Value.HasValue
                ? BmsExScoreProgressInfo.Create(
                    drawableRuleset.ExScorePacemakerInfo.Value.Value.CurrentExScore,
                    drawableRuleset.ExScorePacemakerInfo.Value.Value.MaximumExScore)
                : null;

            Assert.That(drawableRuleset.GameplayFeedbackState.Value.ExScoreProgressInfo, Is.EqualTo(expectedProgressInfo));

            if (!drawableRuleset.GameplayFeedbackState.Value.ExScoreProgressInfo.HasValue)
                return;

            Assert.Multiple(() =>
            {
                Assert.That(drawableRuleset.GameplayFeedbackState.Value.ExScoreProgressInfo!.Value.CurrentExScore, Is.EqualTo(0));
                Assert.That(drawableRuleset.GameplayFeedbackState.Value.ExScoreProgressInfo!.Value.MaximumExScore, Is.EqualTo(2));
                Assert.That(drawableRuleset.GameplayFeedbackState.Value.ExScoreProgressInfo!.Value.DjLevel, Is.EqualTo(BmsDjLevel.F));
            });
        }

        [Test]
        public void TestLiftModAppliesToPlayfield()
        {
            var beatmap = createPlayableBeatmap();
            var liftMod = new BmsModLift();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap, new Mod[] { liftMod });

            liftMod.LiftUnits.Value = 240;
            liftMod.ApplyToDrawableRuleset(drawableRuleset);

            Assert.That(drawableRuleset.Playfield.LiftUnits.Value, Is.EqualTo(240));
        }

        [Test]
        public void TestGameplayAdjustmentFallsBackToLiftWhenOnlyLiftEnabled()
        {
            var beatmap = createPlayableBeatmap();
            var liftMod = new BmsModLift();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap, new Mod[] { liftMod });

            liftMod.ApplyToDrawableRuleset(drawableRuleset);

            Assert.That(drawableRuleset.AdjustGameplayAdjustment(1), Is.True);
            Assert.That(liftMod.LiftUnits.Value, Is.EqualTo(251));
        }

        [Test]
        public void TestAdjustmentTargetStateIsNullWhenNoTargetsEnabled()
        {
            var beatmap = createPlayableBeatmap();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap, null);

            drawableRuleset.RefreshLaneCoverFocus();

            Assert.Multiple(() =>
            {
                Assert.That(drawableRuleset.EnabledAdjustmentTargetCount.Value, Is.EqualTo(0));
                Assert.That(drawableRuleset.ActiveAdjustmentTargetIndex.Value, Is.EqualTo(-1));
                Assert.That(drawableRuleset.ActiveAdjustmentTarget.Value, Is.Null);
            });

            assertGameplayFeedbackStateTargetState(drawableRuleset, null, 0, -1, false);
        }

        [Test]
        public void TestSingleEnabledAdjustmentTargetIsExposedAsActiveTarget()
        {
            var beatmap = createPlayableBeatmap();
            var liftMod = new BmsModLift();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap, new Mod[] { liftMod });

            liftMod.ApplyToDrawableRuleset(drawableRuleset);
            drawableRuleset.RefreshLaneCoverFocus();

            Assert.Multiple(() =>
            {
                Assert.That(drawableRuleset.EnabledAdjustmentTargetCount.Value, Is.EqualTo(1));
                Assert.That(drawableRuleset.ActiveAdjustmentTargetIndex.Value, Is.EqualTo(0));
                Assert.That(drawableRuleset.ActiveAdjustmentTarget.Value, Is.EqualTo(BmsGameplayAdjustmentTarget.Lift));
                Assert.That(drawableRuleset.Playfield.LaneCovers.All(cover => !cover.IsFocused.Value), Is.True);
            });

            assertGameplayFeedbackStateTargetState(drawableRuleset, BmsGameplayAdjustmentTarget.Lift, 1, 0, false);
        }

        [Test]
        public void TestGameplayAdjustmentCyclesAcrossEnabledTargets()
        {
            var beatmap = createPlayableBeatmap();
            var suddenMod = new BmsModSudden();
            var hiddenMod = new BmsModHidden();
            var liftMod = new BmsModLift();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap, new Mod[] { suddenMod, hiddenMod, liftMod });

            suddenMod.ApplyToDrawableRuleset(drawableRuleset);
            hiddenMod.ApplyToDrawableRuleset(drawableRuleset);
            liftMod.ApplyToDrawableRuleset(drawableRuleset);

            drawableRuleset.RefreshLaneCoverFocus();

            Assert.Multiple(() =>
            {
                Assert.That(drawableRuleset.EnabledAdjustmentTargetCount.Value, Is.EqualTo(3));
                Assert.That(drawableRuleset.ActiveAdjustmentTargetIndex.Value, Is.EqualTo(0));
                Assert.That(drawableRuleset.ActiveAdjustmentTarget.Value, Is.EqualTo(BmsGameplayAdjustmentTarget.Sudden));
            });

            assertGameplayFeedbackStateTargetState(drawableRuleset, BmsGameplayAdjustmentTarget.Sudden, 3, 0, false);

            Assert.That(drawableRuleset.AdjustGameplayAdjustment(1), Is.True);
            Assert.That(suddenMod.CoverPercent.Value, Is.EqualTo(251));

            Assert.That(drawableRuleset.CycleGameplayAdjustmentTarget(), Is.True);
            Assert.That(drawableRuleset.ActiveAdjustmentTargetIndex.Value, Is.EqualTo(1));
            Assert.That(drawableRuleset.ActiveAdjustmentTarget.Value, Is.EqualTo(BmsGameplayAdjustmentTarget.Hidden));
            assertGameplayFeedbackStateTargetState(drawableRuleset, BmsGameplayAdjustmentTarget.Hidden, 3, 1, false);
            Assert.That(drawableRuleset.AdjustGameplayAdjustment(1), Is.True);
            Assert.That(hiddenMod.CoverPercent.Value, Is.EqualTo(251));

            Assert.That(drawableRuleset.CycleGameplayAdjustmentTarget(), Is.True);
            Assert.That(drawableRuleset.ActiveAdjustmentTargetIndex.Value, Is.EqualTo(2));
            Assert.That(drawableRuleset.ActiveAdjustmentTarget.Value, Is.EqualTo(BmsGameplayAdjustmentTarget.Lift));
            assertGameplayFeedbackStateTargetState(drawableRuleset, BmsGameplayAdjustmentTarget.Lift, 3, 2, false);
            Assert.That(drawableRuleset.AdjustGameplayAdjustment(1), Is.True);
            Assert.That(liftMod.LiftUnits.Value, Is.EqualTo(251));

            Assert.That(drawableRuleset.CycleGameplayAdjustmentTarget(), Is.True);
            Assert.That(drawableRuleset.ActiveAdjustmentTargetIndex.Value, Is.EqualTo(0));
            Assert.That(drawableRuleset.ActiveAdjustmentTarget.Value, Is.EqualTo(BmsGameplayAdjustmentTarget.Sudden));
            assertGameplayFeedbackStateTargetState(drawableRuleset, BmsGameplayAdjustmentTarget.Sudden, 3, 0, false);
            Assert.That(drawableRuleset.AdjustGameplayAdjustment(1), Is.True);
            Assert.That(suddenMod.CoverPercent.Value, Is.EqualTo(252));
        }

        [Test]
        public void TestLatestJudgementFeedbackTracksTimingAndIgnoresNonBasicResults()
        {
            var beatmap = createPlayableBeatmap();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap, null);
            var playableHitObject = beatmap.HitObjects.OfType<BmsHitObject>().First();
            var expectedEmptyPoorFeedback = new BmsJudgementTimingFeedback(HitResult.Ok, 0, false);

            drawableRuleset.HandleGameplayJudgementResult(createResult(playableHitObject, HitResult.Perfect, -3.2));

            Assert.That(drawableRuleset.LatestJudgementFeedback.Value.HasValue, Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(drawableRuleset.LatestJudgementFeedback.Value!.Value.Result, Is.EqualTo(HitResult.Perfect));
                Assert.That(drawableRuleset.LatestJudgementFeedback.Value!.Value.TimeOffset, Is.EqualTo(-3.2).Within(0.001));
                Assert.That(drawableRuleset.LatestJudgementFeedback.Value!.Value.ShowsTimingDirection, Is.True);
                Assert.That(drawableRuleset.GameplayFeedbackState.Value.LatestJudgementFeedback, Is.EqualTo(drawableRuleset.LatestJudgementFeedback.Value));
                Assert.That(drawableRuleset.GameplayFeedbackState.Value.TimingFeedbackVisualRange, Is.EqualTo(drawableRuleset.TimingFeedbackVisualRange.Value));
            });

            drawableRuleset.HandleGameplayJudgementResult(createResult(new BmsEmptyPoorHitObject(), HitResult.Ok));

            Assert.That(drawableRuleset.LatestJudgementFeedback.Value.HasValue, Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(drawableRuleset.LatestJudgementFeedback.Value!.Value.Result, Is.EqualTo(expectedEmptyPoorFeedback.Result));
                Assert.That(drawableRuleset.LatestJudgementFeedback.Value!.Value.TimeOffset, Is.EqualTo(expectedEmptyPoorFeedback.TimeOffset).Within(0.001));
                Assert.That(drawableRuleset.LatestJudgementFeedback.Value!.Value.ShowsTimingDirection, Is.EqualTo(expectedEmptyPoorFeedback.ShowsTimingDirection));
                Assert.That(drawableRuleset.GameplayFeedbackState.Value.LatestJudgementFeedback, Is.EqualTo(drawableRuleset.LatestJudgementFeedback.Value));
            });

            drawableRuleset.HandleGameplayJudgementResult(createResult(playableHitObject, HitResult.ComboBreak, 12));

            Assert.That(drawableRuleset.LatestJudgementFeedback.Value.HasValue, Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(drawableRuleset.LatestJudgementFeedback.Value!.Value.Result, Is.EqualTo(expectedEmptyPoorFeedback.Result));
                Assert.That(drawableRuleset.LatestJudgementFeedback.Value!.Value.TimeOffset, Is.EqualTo(expectedEmptyPoorFeedback.TimeOffset).Within(0.001));
                Assert.That(drawableRuleset.LatestJudgementFeedback.Value!.Value.ShowsTimingDirection, Is.EqualTo(expectedEmptyPoorFeedback.ShowsTimingDirection));
                Assert.That(drawableRuleset.GameplayFeedbackState.Value.LatestJudgementFeedback, Is.EqualTo(drawableRuleset.LatestJudgementFeedback.Value));
            });

            Assert.That(drawableRuleset.RecentJudgementFeedbacks, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(drawableRuleset.RecentJudgementFeedbacks[0].Result, Is.EqualTo(HitResult.Perfect));
                Assert.That(drawableRuleset.RecentJudgementFeedbacks[0].TimeOffset, Is.EqualTo(-3.2).Within(0.001));
                Assert.That(drawableRuleset.RecentJudgementFeedbacks[0].ShowsTimingDirection, Is.True);
            });
        }

        [Test]
        public void TestLaneCoverFocusClearsWhenLiftBecomesCurrentTarget()
        {
            var beatmap = createPlayableBeatmap();
            var suddenMod = new BmsModSudden();
            var hiddenMod = new BmsModHidden();
            var liftMod = new BmsModLift();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap, new Mod[] { suddenMod, hiddenMod, liftMod });

            suddenMod.ApplyToDrawableRuleset(drawableRuleset);
            hiddenMod.ApplyToDrawableRuleset(drawableRuleset);
            liftMod.ApplyToDrawableRuleset(drawableRuleset);

            Assert.That(drawableRuleset.CycleGameplayAdjustmentTarget(), Is.True);
            Assert.That(drawableRuleset.CycleGameplayAdjustmentTarget(), Is.True);

            Assert.That(drawableRuleset.Playfield.LaneCovers.All(cover => !cover.IsFocused.Value), Is.True);
        }

        [Test]
        public void TestScrollAdjustmentFallsBackToBottomCover()
        {
            var beatmap = createPlayableBeatmap();
            var hiddenMod = new BmsModHidden();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap, new Mod[] { hiddenMod });

            hiddenMod.ApplyToDrawableRuleset(drawableRuleset);

            Assert.That(drawableRuleset.AdjustLaneCover(-1), Is.True);

            var laneCovers = drawableRuleset.Playfield.CoverContainer.Children.OfType<BmsLaneCover>().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(hiddenMod.CoverPercent.Value, Is.EqualTo(249));
                Assert.That(laneCovers.Single().CoverPercent.Value, Is.EqualTo(249));
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

        private static JudgementResult createResult(HitObject hitObject, HitResult hitResult, double? timeOffset = null)
        {
            var result = new JudgementResult(hitObject, hitObject.CreateJudgement())
            {
                Type = hitResult,
            };

            if (timeOffset.HasValue)
                result.TimeOffset = timeOffset.Value;

            return result;
        }

        private static void assertGameplayFeedbackStateTargetState(DrawableBmsRuleset drawableRuleset, BmsGameplayAdjustmentTarget? expectedTarget, int expectedCount,
                                                                   int expectedIndex, bool expectedTemporaryOverride)
        {
            Assert.Multiple(() =>
            {
                Assert.That(drawableRuleset.GameplayFeedbackState.Value.ActiveAdjustmentTarget, Is.EqualTo(expectedTarget));
                Assert.That(drawableRuleset.GameplayFeedbackState.Value.EnabledAdjustmentTargetCount, Is.EqualTo(expectedCount));
                Assert.That(drawableRuleset.GameplayFeedbackState.Value.ActiveAdjustmentTargetIndex, Is.EqualTo(expectedIndex));
                Assert.That(drawableRuleset.GameplayFeedbackState.Value.IsAdjustmentTargetTemporarilyOverridden, Is.EqualTo(expectedTemporaryOverride));
            });
        }
    }
}
