// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using NUnit.Framework;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public partial class BmsDrawableRulesetTest
    {
        private readonly BmsBeatmapDecoder decoder = new BmsBeatmapDecoder();

        [Test]
        public void TestCreatesDrawableRulesetForPlayableBeatmap()
        {
            var beatmap = createPlayableBeatmap();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap);
            var expectedVariant = BmsLaneLayout.CreateFor(beatmap).Lanes.Count;

            Assert.Multiple(() =>
            {
                Assert.That(drawableRuleset, Is.TypeOf<DrawableBmsRuleset>());
                Assert.That(drawableRuleset.Playfield, Is.TypeOf<BmsPlayfield>());
                Assert.That(drawableRuleset.Variant, Is.EqualTo(expectedVariant));
            });
        }

        [Test]
        public void TestDrawableRulesetUsesDedicatedPlayfieldAdjustmentContainer()
        {
            var beatmap = createPlayableBeatmap();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap);

            Assert.That(drawableRuleset.PlayfieldAdjustmentContainer, Is.TypeOf<BmsPlayfieldAdjustmentContainer>());
        }

        [Test]
        public void TestDrawableRulesetDefaultsLongNoteModeToLn()
        {
            var beatmap = createPlayableBeatmap();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap);

            Assert.That(drawableRuleset.LongNoteMode, Is.EqualTo(BmsLongNoteMode.LN));
        }

        [Test]
        public void TestDrawableRulesetDefaultsJudgeModeToOd()
        {
            var beatmap = createPlayableBeatmap();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap);

            Assert.That(drawableRuleset.JudgeMode, Is.EqualTo(BmsJudgeMode.OD));
        }

        [Test]
        public void TestDrawableRulesetUsesRelativeBeatLengthScaling()
        {
            var beatmap = createPlayableBeatmap();
            var drawableRuleset = new TestableDrawableBmsRuleset(new BmsRuleset(), beatmap);

            Assert.That(drawableRuleset.UsesRelativeBeatLengthScaling, Is.True);
        }

        [Test]
        public void TestRulesetDisplaysPersistedKeyCountAttribute()
        {
            var beatmap = createPlayableBeatmap();
            int expectedKeyCount = BmsRuleset.GetKeyCount(beatmap.BmsInfo.Keymode);
            var displayAttributes = new BmsRuleset().GetBeatmapAttributesForDisplay(beatmap.BeatmapInfo, Array.Empty<Mod>()).ToArray();

            Assert.That(displayAttributes.Any(a => a.Acronym == "KC" && a.OriginalValue == expectedKeyCount && a.AdjustedValue == expectedKeyCount), Is.True);
            Assert.That(displayAttributes.Any(a => a.Acronym == "RANK"), Is.True);
        }

        [TestCase(BmsLongNoteMode.CN)]
        [TestCase(BmsLongNoteMode.HCN)]
        public void TestDrawableRulesetUsesSelectedLongNoteModeMods(BmsLongNoteMode expectedMode)
        {
            var beatmap = createPlayableBeatmap();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap, new Mod[] { createLongNoteModeMod(expectedMode) });

            Assert.That(drawableRuleset.LongNoteMode, Is.EqualTo(expectedMode));
        }

        [TestCase(BmsJudgeMode.Beatoraja)]
        [TestCase(BmsJudgeMode.LR2)]
        [TestCase(BmsJudgeMode.IIDX)]
        public void TestDrawableRulesetUsesSelectedJudgeModeMods(BmsJudgeMode expectedMode)
        {
            var beatmap = createPlayableBeatmap();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap, new Mod[] { createJudgeModeMod(expectedMode) });

            Assert.That(drawableRuleset.JudgeMode, Is.EqualTo(expectedMode));
        }

        [TestCase(BmsJudgeMode.OD, 17.5, 43.5, 76.5, 130.5, 167.5, 17.5, 43.5, 76.5, 130.5, 209.375)]
        [TestCase(BmsJudgeMode.Beatoraja, 15, 45, 112, 210, 210, 90, 120, 150, 210, 210)]
        [TestCase(BmsJudgeMode.LR2, 18, 40, 100, 200, 200, 18, 40, 100, 200, 240)]
        [TestCase(BmsJudgeMode.IIDX, 16.67, 33.33, 116.67, 250, 250, 16.67, 33.33, 116.67, 250, 250)]
        public void TestDrawableRulesetAppliesSelectedJudgeWindows(BmsJudgeMode judgeMode, double expectedPerfect, double expectedGreat, double expectedGood, double expectedBad, double expectedPoor, double expectedReleasePerfect, double expectedReleaseGreat, double expectedReleaseGood, double expectedReleaseBad, double expectedReleaseMiss)
        {
            var beatmap = createPlayableBeatmap(rank: 2);
            IReadOnlyList<Mod> mods = judgeMode == BmsJudgeMode.OD ? Array.Empty<Mod>() : new[] { createJudgeModeMod(judgeMode) };

            _ = new BmsRuleset().CreateDrawableRulesetWith(beatmap, mods);

            var note = beatmap.HitObjects.OfType<BmsHitObject>().Single(hitObject => hitObject is not BmsHoldNote && hitObject.LaneIndex == 1);
            var hold = beatmap.HitObjects.OfType<BmsHoldNote>().Single();
            var tailTimingWindows = (BmsTimingWindows)hold.Tail!.HitWindows;

            Assert.Multiple(() =>
            {
                Assert.That(note.HitWindows.WindowFor(HitResult.Perfect), Is.EqualTo(expectedPerfect).Within(0.001));
                Assert.That(note.HitWindows.WindowFor(HitResult.Great), Is.EqualTo(expectedGreat).Within(0.001));
                Assert.That(note.HitWindows.WindowFor(HitResult.Good), Is.EqualTo(expectedGood).Within(0.001));
                Assert.That(note.HitWindows.WindowFor(HitResult.Meh), Is.EqualTo(expectedBad).Within(0.001));
                Assert.That(note.HitWindows.WindowFor(HitResult.Miss), Is.EqualTo(expectedPoor).Within(0.001));
                Assert.That(hold.Head!.HitWindows.WindowFor(HitResult.Great), Is.EqualTo(expectedGreat).Within(0.001));
                Assert.That(hold.Tail!.HitWindows.WindowFor(HitResult.Miss), Is.EqualTo(expectedPoor).Within(0.001));
                Assert.That(tailTimingWindows.WindowFor(HitResult.Perfect, isLongNoteRelease: true), Is.EqualTo(expectedReleasePerfect).Within(0.001));
                Assert.That(tailTimingWindows.WindowFor(HitResult.Great, isLongNoteRelease: true), Is.EqualTo(expectedReleaseGreat).Within(0.001));
                Assert.That(tailTimingWindows.WindowFor(HitResult.Good, isLongNoteRelease: true), Is.EqualTo(expectedReleaseGood).Within(0.001));
                Assert.That(tailTimingWindows.WindowFor(HitResult.Meh, isLongNoteRelease: true), Is.EqualTo(expectedReleaseBad).Within(0.001));
                Assert.That(tailTimingWindows.WindowFor(HitResult.Miss, isLongNoteRelease: true), Is.EqualTo(expectedReleaseMiss).Within(0.001));
            });
        }

        [Test]
        public void TestBeatorajaScratchUsesDedicatedJudgeWindows()
        {
            var beatmap = createPlayableBeatmap(rank: 2);

            _ = new BmsRuleset().CreateDrawableRulesetWith(beatmap, new Mod[] { new BmsModJudgeBeatoraja() });

            var note = beatmap.HitObjects.OfType<BmsHitObject>().Single(hitObject => hitObject is not BmsHoldNote && hitObject.LaneIndex == 1);
            var scratch = beatmap.HitObjects.OfType<BmsHitObject>().Single(hitObject => hitObject.IsScratch && hitObject is not BmsHoldNote);

            Assert.Multiple(() =>
            {
                Assert.That(scratch.HitWindows.WindowFor(HitResult.Perfect), Is.EqualTo(22).Within(0.001));
                Assert.That(scratch.HitWindows.WindowFor(HitResult.Great), Is.EqualTo(52).Within(0.001));
                Assert.That(scratch.HitWindows.WindowFor(HitResult.Good), Is.EqualTo(120).Within(0.001));
                Assert.That(scratch.HitWindows.WindowFor(HitResult.Meh), Is.EqualTo(217).Within(0.001));
                Assert.That(scratch.HitWindows.WindowFor(HitResult.Meh), Is.GreaterThan(note.HitWindows.WindowFor(HitResult.Meh)));
            });
        }

        [TestCase(BmsJudgeMode.Beatoraja, BmsJudgeRank.VeryHard, 5, 15, 37, 70)]
        [TestCase(BmsJudgeMode.LR2, BmsJudgeRank.Easy, 21, 60, 120, 200)]
        public void TestDrawableRulesetAppliesJudgeRankOverrideMod(BmsJudgeMode judgeMode, BmsJudgeRank judgeRank, double expectedPerfect, double expectedGreat, double expectedGood, double expectedBad)
        {
            var beatmap = createPlayableBeatmap(rank: 1);
            var judgeRankMod = new BmsModJudgeRank();
            judgeRankMod.JudgeRank.Value = judgeRank;
            judgeRankMod.ApplyToDifficulty(beatmap.Difficulty);
            beatmap.BeatmapInfo.Difficulty.OverallDifficulty = beatmap.Difficulty.OverallDifficulty;

            IReadOnlyList<Mod> mods =
            [
                createJudgeModeMod(judgeMode),
                judgeRankMod,
            ];

            _ = new BmsRuleset().CreateDrawableRulesetWith(beatmap, mods);

            var note = beatmap.HitObjects.OfType<BmsHitObject>().Single(hitObject => hitObject is not BmsHoldNote && hitObject.LaneIndex == 1);

            Assert.Multiple(() =>
            {
                Assert.That(note.HitWindows.WindowFor(HitResult.Perfect), Is.EqualTo(expectedPerfect).Within(0.001));
                Assert.That(note.HitWindows.WindowFor(HitResult.Great), Is.EqualTo(expectedGreat).Within(0.001));
                Assert.That(note.HitWindows.WindowFor(HitResult.Good), Is.EqualTo(expectedGood).Within(0.001));
                Assert.That(note.HitWindows.WindowFor(HitResult.Meh), Is.EqualTo(expectedBad).Within(0.001));
            });
        }

        [Test]
        public void TestBeatorajaTimingWindowsExposeExcessivePoorRange()
        {
            var beatmap = createPlayableBeatmap(rank: 2);

            _ = new BmsRuleset().CreateDrawableRulesetWith(beatmap, new Mod[] { new BmsModJudgeBeatoraja() });

            var note = beatmap.HitObjects.OfType<BmsHitObject>().Single(hitObject => hitObject is not BmsHoldNote && hitObject.LaneIndex == 1);
            var timingWindows = (BmsTimingWindows)note.HitWindows;

            Assert.Multiple(() =>
            {
                Assert.That(timingWindows.CanTriggerExcessivePoor(-500), Is.True);
                Assert.That(timingWindows.CanTriggerExcessivePoor(-500.001), Is.False);
                Assert.That(timingWindows.CanTriggerExcessivePoor(150), Is.True);
                Assert.That(timingWindows.CanTriggerExcessivePoor(150.001), Is.False);
            });
        }

        [Test]
        public void TestLr2TimingWindowsUsePreNoteOnlyExcessivePoorRange()
        {
            var beatmap = createPlayableBeatmap(rank: 2);

            _ = new BmsRuleset().CreateDrawableRulesetWith(beatmap, new Mod[] { new BmsModJudgeLr2() });

            var note = beatmap.HitObjects.OfType<BmsHitObject>().Single(hitObject => hitObject is not BmsHoldNote && hitObject.LaneIndex == 1);
            var timingWindows = (BmsTimingWindows)note.HitWindows;

            Assert.Multiple(() =>
            {
                Assert.That(timingWindows.CanTriggerExcessivePoor(-1000), Is.True);
                Assert.That(timingWindows.CanTriggerExcessivePoor(-1000.001), Is.False);
                Assert.That(timingWindows.CanTriggerExcessivePoor(0), Is.True);
                Assert.That(timingWindows.CanTriggerExcessivePoor(0.001), Is.False);
            });
        }

        [Test]
        public void TestBeatorajaLaneTriggersLateEmptyPoorAfterJudgedNote()
        {
            var beatmap = createPlayableBeatmap(rank: 2);

            _ = new BmsRuleset().CreateDrawableRulesetWith(beatmap, new Mod[] { new BmsModJudgeBeatoraja() });

            var note = beatmap.HitObjects.OfType<BmsHitObject>().Single(hitObject => hitObject is not BmsHoldNote && hitObject.LaneIndex == 1);
            var (lane, drawable, manualClock, testClock) = createLaneWithDrawable(beatmap, note);

            manualClock.CurrentTime = note.StartTime;
            testClock.ProcessFrame();

            Assert.That(drawable.OnPressed(createPressEvent()), Is.True);

            manualClock.CurrentTime = note.StartTime + 100;
            testClock.ProcessFrame();

            Assert.That(lane.OnPressed(createPressEvent()), Is.True);
        }

        [Test]
        public void TestLr2LaneDoesNotTriggerLateEmptyPoorAfterJudgedNote()
        {
            var beatmap = createPlayableBeatmap(rank: 2);

            _ = new BmsRuleset().CreateDrawableRulesetWith(beatmap, new Mod[] { new BmsModJudgeLr2() });

            var note = beatmap.HitObjects.OfType<BmsHitObject>().Single(hitObject => hitObject is not BmsHoldNote && hitObject.LaneIndex == 1);
            var (lane, drawable, manualClock, testClock) = createLaneWithDrawable(beatmap, note);

            manualClock.CurrentTime = note.StartTime;
            testClock.ProcessFrame();

            Assert.That(drawable.OnPressed(createPressEvent()), Is.True);

            manualClock.CurrentTime = note.StartTime + 100;
            testClock.ProcessFrame();

            Assert.That(lane.OnPressed(createPressEvent()), Is.False);
        }

        [TestCase(BmsJudgeMode.OD)]
        [TestCase(BmsJudgeMode.Beatoraja)]
        [TestCase(BmsJudgeMode.LR2)]
        [TestCase(BmsJudgeMode.IIDX)]
        public void TestTailReleaseUsesExactJudgeWindowBoundaries(BmsJudgeMode judgeMode)
        {
            var beatmap = createPlayableBeatmap(rank: 2);
            IReadOnlyList<Mod> mods = judgeMode == BmsJudgeMode.OD ? Array.Empty<Mod>() : new[] { createJudgeModeMod(judgeMode) };

            _ = new BmsRuleset().CreateDrawableRulesetWith(beatmap, mods);

            var hold = beatmap.HitObjects.OfType<BmsHoldNote>().Single();
            var tailTimingWindows = (BmsTimingWindows)hold.Tail!.HitWindows;
            double perfectWindow = tailTimingWindows.WindowFor(HitResult.Perfect, isLongNoteRelease: true);
            double greatWindow = tailTimingWindows.WindowFor(HitResult.Great, isLongNoteRelease: true);
            double goodWindow = tailTimingWindows.WindowFor(HitResult.Good, isLongNoteRelease: true);
            double mehWindow = tailTimingWindows.WindowFor(HitResult.Meh, isLongNoteRelease: true);
            double missWindow = tailTimingWindows.WindowFor(HitResult.Miss, isLongNoteRelease: true);

            Assert.Multiple(() =>
            {
                Assert.That(DrawableBmsHoldNote.ResultForTailRelease(hold, hold.EndTime + perfectWindow), Is.EqualTo(HitResult.Perfect));
                Assert.That(DrawableBmsHoldNote.ResultForTailRelease(hold, hold.EndTime + perfectWindow + 0.001), Is.EqualTo(HitResult.Great));
                Assert.That(DrawableBmsHoldNote.ResultForTailRelease(hold, hold.EndTime + greatWindow), Is.EqualTo(HitResult.Great));
                Assert.That(DrawableBmsHoldNote.ResultForTailRelease(hold, hold.EndTime + greatWindow + 0.001), Is.EqualTo(HitResult.Good));
                Assert.That(DrawableBmsHoldNote.ResultForTailRelease(hold, hold.EndTime + goodWindow), Is.EqualTo(HitResult.Good));
                Assert.That(DrawableBmsHoldNote.ResultForTailRelease(hold, hold.EndTime + goodWindow + 0.001), Is.EqualTo(HitResult.Meh));
                Assert.That(DrawableBmsHoldNote.ResultForTailRelease(hold, hold.EndTime + mehWindow), Is.EqualTo(HitResult.Meh));
                Assert.That(DrawableBmsHoldNote.ResultForTailRelease(hold, hold.EndTime + mehWindow + 0.001), Is.EqualTo(HitResult.None));
                Assert.That(DrawableBmsHoldNote.ResultForTailRelease(hold, hold.EndTime + missWindow), Is.EqualTo(missWindow > mehWindow ? HitResult.None : HitResult.Meh));
                Assert.That(DrawableBmsHoldNote.HasMissedTailReleaseWindow(hold, hold.EndTime + missWindow), Is.False);
                Assert.That(DrawableBmsHoldNote.HasMissedTailReleaseWindow(hold, hold.EndTime + missWindow + 0.001), Is.True);
            });
        }

        [Test]
        public void TestCreatesPlaceholderDrawablesForCurrentHitObjectTypes()
        {
            var beatmap = createPlayableBeatmap();
            var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap);

            foreach (var hitObject in beatmap.HitObjects)
                Assert.That(drawableRuleset.CreateDrawableRepresentation(hitObject), Is.Not.Null);
        }

            [Test]
            public void TestCreatesDedicatedDrawableForHoldNotes()
            {
                var beatmap = createPlayableBeatmap();
                var drawableRuleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(beatmap);
                var hold = beatmap.HitObjects.OfType<BmsHoldNote>().Single();

                Assert.That(drawableRuleset.CreateDrawableRepresentation(hold), Is.TypeOf<DrawableBmsHoldNote>());
            }

        [Test]
        public void TestPlayfieldRoutesDrawablesToMatchingLanes()
        {
            var beatmap = createPlayableBeatmap();
            var playfield = new BmsPlayfield(beatmap);

            var scratch = beatmap.HitObjects.OfType<BmsHitObject>().Single(hitObject => hitObject.IsScratch && hitObject is not BmsHoldNote);
            var regular = beatmap.HitObjects.OfType<BmsHitObject>().Single(hitObject => !hitObject.IsScratch && hitObject is not BmsHoldNote && hitObject.LaneIndex == 1);

            var scratchDrawable = new DrawableBmsHitObject(scratch);
            var regularDrawable = new DrawableBmsHitObject(regular);

            playfield.Add(scratchDrawable);
            playfield.Add(regularDrawable);

            Assert.Multiple(() =>
            {
                Assert.That(playfield.Lanes[0], Is.TypeOf<BmsScratchLane>());
                Assert.That(playfield.Lanes[1], Is.TypeOf<BmsLane>());
                Assert.That(playfield.Lanes[0].AllHitObjects.Contains(scratchDrawable), Is.True);
                Assert.That(playfield.Lanes[0].AllHitObjects.Contains(regularDrawable), Is.False);
                Assert.That(playfield.Lanes[1].AllHitObjects.Contains(regularDrawable), Is.True);
            });
        }

        [Test]
        public void TestPlayfieldAddsMeasureBarLinesToEachLane()
        {
            var beatmap = createPlayableBeatmap();
            var playfield = new BmsPlayfield(beatmap);

            foreach (var lane in playfield.Lanes)
                Assert.That(lane.AllHitObjects.Count(hitObject => hitObject is DrawableBmsBarLine), Is.EqualTo(beatmap.MeasureStartTimes.Count));
        }

        [Test]
        public void TestPlayfieldCreatesLaneHitTargets()
        {
            var beatmap = createPlayableBeatmap();
            var playfield = new BmsPlayfield(beatmap);

            Assert.Multiple(() =>
            {
                Assert.That(playfield.Lanes[0].HitTarget, Is.TypeOf<BmsScratchHitTarget>());
                Assert.That(playfield.Lanes[1].HitTarget, Is.TypeOf<BmsHitTarget>());
                Assert.That(playfield.Lanes.All(lane => lane.HitTarget != null), Is.True);
            });
        }

        [Test]
        public void TestPlayfieldUsesLayoutProfileForLaneDecorations()
        {
            var beatmap = createPlayableBeatmap();
            var playfield = new BmsPlayfield(beatmap);
            var barLines = playfield.Lanes.SelectMany(lane => lane.AllHitObjects.OfType<DrawableBmsBarLine>()).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(playfield.LayoutProfile, Is.SameAs(playfield.LaneLayout.Profile));
                Assert.That(playfield.Lanes.All(lane => Math.Abs(lane.HitTarget.Height - playfield.LayoutProfile.HitTargetHeight) <= 0.0001f), Is.True);
                Assert.That(barLines, Is.Not.Empty);
                Assert.That(barLines.All(barLine => Math.Abs(barLine.Height - playfield.LayoutProfile.BarLineHeight) <= 0.0001f), Is.True);
            });
        }

        [Test]
        public void TestPlayfieldAppliesLaneLayoutPositions()
        {
            var beatmap = createPlayableBeatmap();
            var playfield = new BmsPlayfield(beatmap);
            var layout = BmsLaneLayout.CreateFor(beatmap);

            Assert.That(playfield.Lanes, Has.Count.EqualTo(layout.Lanes.Count));

            for (int i = 0; i < layout.Lanes.Count; i++)
            {
                var expectedLane = layout.Lanes[i];
                var drawableLane = playfield.Lanes[i];

                Assert.Multiple(() =>
                {
                    Assert.That(drawableLane.X, Is.EqualTo(expectedLane.RelativeStart / layout.TotalRelativeWidth).Within(0.0001f));
                    Assert.That(drawableLane.Width, Is.EqualTo(expectedLane.RelativeWidth / layout.TotalRelativeWidth).Within(0.0001f));
                    Assert.That(drawableLane.Height, Is.EqualTo(1).Within(0.0001f));
                });
            }

            Assert.Multiple(() =>
            {
                Assert.That(playfield.Lanes[1].X, Is.GreaterThan(playfield.Lanes[0].Width));
                Assert.That(playfield.Lanes[2].X, Is.EqualTo(playfield.Lanes[1].X + playfield.Lanes[1].Width).Within(0.0001f));
            });
        }

        [Test]
        public void TestPlayfieldCreatesBackgroundLayerUsingStageFileMetadata()
        {
            var beatmap = createPlayableBeatmap(stageFile: "stage.png", backgroundFile: "fallback.png");
            var playfield = new BmsPlayfield(beatmap);

            Assert.Multiple(() =>
            {
                Assert.That(playfield.BackgroundLayer, Is.Not.Null);
                Assert.That(playfield.BackgroundLayer.HasDisplayedAsset, Is.True);
                Assert.That(playfield.BackgroundLayer.DisplayedAssetName, Is.EqualTo("stage.png"));
            });
        }

        [Test]
        public void TestPlayfieldCreatesSharedKeysoundStore()
        {
            var beatmap = createPlayableBeatmap();
            var playfield = new BmsPlayfield(beatmap);

            Assert.Multiple(() =>
            {
                Assert.That(playfield.KeysoundStore, Is.Not.Null);
                Assert.That(playfield.KeysoundStore.ConcurrentChannels, Is.EqualTo(BmsKeysoundStore.DEFAULT_CONCURRENT_CHANNELS));
            });
        }

        [Test]
        public void TestSharedKeysoundStoreChannelCountCanBeReconfigured()
        {
            var beatmap = createPlayableBeatmap();
            var playfield = new BmsPlayfield(beatmap);

            playfield.KeysoundStore.ConcurrentChannels = 24;

            Assert.That(playfield.KeysoundStore.ConcurrentChannels, Is.EqualTo(24));
        }

        [Test]
        public void TestDrawableHitObjectsExposeKeysoundSamples()
        {
            var beatmap = createPlayableBeatmap();

            var bgm = beatmap.HitObjects.OfType<BmsBgmEvent>().Single();
            var regular = beatmap.HitObjects.OfType<BmsHitObject>().Single(hitObject => hitObject is not BmsHoldNote && hitObject.LaneIndex == 2);
            var hold = beatmap.HitObjects.OfType<BmsHoldNote>().Single();

            var bgmSamples = new DrawableBmsHitObject(bgm).GetSamples().OfType<BmsKeysoundSampleInfo>().Select(sample => sample.Filename).ToArray();
            var regularSample = new DrawableBmsHitObject(regular).GetSamples().OfType<BmsKeysoundSampleInfo>().Single();
            var holdSample = new DrawableBmsHitObject(hold).GetSamples().OfType<BmsKeysoundSampleInfo>().Single();

            Assert.Multiple(() =>
            {
                Assert.That(bgmSamples, Is.EqualTo(new[] { "bgm.wav" }));
                Assert.That(regularSample.Filename, Is.EqualTo("keys/note.wav"));
                Assert.That(regularSample.LookupNames.ToArray(), Is.EqualTo(new[] { "keys/note.wav", "keys/note" }));
                Assert.That(holdSample.Filename, Is.EqualTo("hold/head.ogg"));
            });
        }

        [Test]
        public void TestHoldNotesCreateNestedTailKeysoundEvents()
        {
            var beatmap = createPlayableBeatmap();
            var hold = beatmap.HitObjects.OfType<BmsHoldNote>().Single();
            var head = hold.Head;
            var bodyTicks = hold.BodyTicks;
            var tail = hold.Tail;

            Assert.Multiple(() =>
            {
                Assert.That(head, Is.Not.Null);
                Assert.That(bodyTicks, Is.Not.Empty);
                Assert.That(tail, Is.Not.Null);
                Assert.That(hold.NestedHitObjects.OfType<BmsHoldNoteHead>().Single(), Is.SameAs(head));
                Assert.That(hold.NestedHitObjects.OfType<BmsHoldNoteBodyTick>().Count(), Is.EqualTo(bodyTicks.Count));
                Assert.That(hold.NestedHitObjects.OfType<BmsHoldNoteTailEvent>().Single(), Is.SameAs(tail));
                Assert.That(head!.StartTime, Is.EqualTo(hold.StartTime).Within(0.001));
                Assert.That(new DrawableBmsHitObject(head).GetSamples().OfType<BmsKeysoundSampleInfo>().Single().Filename, Is.EqualTo("hold/head.ogg"));
                Assert.That(tail!.StartTime, Is.EqualTo(hold.EndTime).Within(0.001));
                Assert.That(new DrawableBmsHitObject(tail).GetSamples().OfType<BmsKeysoundSampleInfo>().Single().Filename, Is.EqualTo("hold/tail.wav"));
            });
        }

        [Test]
        public void TestPlayableNotesDoNotAutoApplyMaxResult()
        {
            var beatmap = createPlayableBeatmap();
            var regular = beatmap.HitObjects.OfType<BmsHitObject>().Single(hitObject => hitObject is not BmsHoldNote && hitObject.LaneIndex == 1);
            var hold = beatmap.HitObjects.OfType<BmsHoldNote>().Single();

            Assert.Multiple(() =>
            {
                Assert.That(DrawableBmsHitObject.ShouldAutoApplyMaxResult(regular), Is.False);
                Assert.That(DrawableBmsHitObject.ShouldAutoApplyMaxResult(hold), Is.False);
            });
        }

        [Test]
        public void TestPlayableNotesSupportPlayerInput()
        {
            var beatmap = createPlayableBeatmap();
            var regular = beatmap.HitObjects.OfType<BmsHitObject>().Single(hitObject => hitObject is not BmsHoldNote && hitObject.LaneIndex == 1);
            var scratch = beatmap.HitObjects.OfType<BmsHitObject>().Single(hitObject => hitObject.IsScratch && hitObject is not BmsHoldNote);
            var hold = beatmap.HitObjects.OfType<BmsHoldNote>().Single();
            var bgm = beatmap.HitObjects.OfType<BmsBgmEvent>().Single();

            Assert.Multiple(() =>
            {
                Assert.That(DrawableBmsHitObject.SupportsPlayerInput(regular), Is.True);
                Assert.That(DrawableBmsHitObject.SupportsPlayerInput(scratch), Is.True);
                Assert.That(DrawableBmsHitObject.SupportsPlayerInput(hold), Is.True);
                Assert.That(DrawableBmsHitObject.SupportsPlayerInput(bgm), Is.False);
            });
        }

        [Test]
        public void TestConvertedBeatmapMapsRankToOverallDifficulty()
        {
            var beatmap = createPlayableBeatmap(rank: 2);

            Assert.Multiple(() =>
            {
                Assert.That(beatmap.Difficulty.OverallDifficulty, Is.EqualTo(7).Within(0.001));
                Assert.That(beatmap.BeatmapInfo.Difficulty.OverallDifficulty, Is.EqualTo(7).Within(0.001));
            });
        }

        [Test]
        public void TestPlayerInputUsesBmsTimingWindows()
        {
            var note = new BmsHitObject
            {
                StartTime = 1000,
                LaneIndex = 1,
            };

            note.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty
            {
                OverallDifficulty = OsuOdJudgementSystem.MapRankToOverallDifficulty(2),
            });

            Assert.Multiple(() =>
            {
                Assert.That(note.HitWindows, Is.TypeOf<BmsTimingWindows>());
                Assert.That(note.HitWindows.WindowFor(HitResult.Perfect), Is.EqualTo(17.5).Within(0.001));
                Assert.That(note.HitWindows.WindowFor(HitResult.Great), Is.EqualTo(43.5).Within(0.001));
                Assert.That(note.HitWindows.WindowFor(HitResult.Good), Is.EqualTo(76.5).Within(0.001));
                Assert.That(note.HitWindows.WindowFor(HitResult.Meh), Is.EqualTo(130.5).Within(0.001));
                Assert.That(note.HitWindows.WindowFor(HitResult.Miss), Is.EqualTo(167.5).Within(0.001));
                Assert.That(DrawableBmsHitObject.ResultForPlayerInput(note, 0), Is.EqualTo(HitResult.Perfect));
                Assert.That(DrawableBmsHitObject.ResultForPlayerInput(note, 100), Is.EqualTo(HitResult.Meh));
                Assert.That(DrawableBmsHitObject.ResultForPlayerInput(note, 150), Is.EqualTo(HitResult.Miss));
                Assert.That(DrawableBmsHitObject.ResultForPlayerInput(note, 180), Is.EqualTo(HitResult.None));
                Assert.That(DrawableBmsHitObject.CanStillBeHitByPlayer(note, -10), Is.True);
                Assert.That(DrawableBmsHitObject.CanStillBeHitByPlayer(note, 131), Is.True);
                Assert.That(DrawableBmsHitObject.CanStillBeHitByPlayer(note, 168), Is.False);
                Assert.That(new DrawableBmsHitObject(note).DisplayResult, Is.True);
            });
        }

        [Test]
        public void TestPlayerTriggeredPoorConsumesNoteInput()
        {
            var note = new BmsHitObject
            {
                StartTime = 1000,
                LaneIndex = 1,
            };

            note.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty
            {
                OverallDifficulty = OsuOdJudgementSystem.MapRankToOverallDifficulty(2),
            });

            var manualClock = new ManualClock
            {
                CurrentTime = note.StartTime + note.HitWindows.WindowFor(HitResult.Meh) + 1,
                IsRunning = true,
            };

            var testClock = new FramedClock(manualClock);
            var drawable = new DrawableBmsHitObject(note)
            {
                Clock = testClock,
            };

            drawable.Apply(note);
            testClock.ProcessFrame();

            Assert.Multiple(() =>
            {
                Assert.That(drawable.OnPressed(createPressEvent()), Is.True);
                Assert.That(drawable.Result.Type, Is.EqualTo(HitResult.Miss));
            });
        }

        [Test]
        public void TestScratchStreamNotesResolveOnRepeatedScratchPresses()
        {
            var beatmap = createScratchStreamBeatmap();
            var scratchNotes = beatmap.HitObjects.OfType<BmsHitObject>()
                                    .Where(hitObject => hitObject.IsScratch && hitObject is not BmsHoldNote)
                                    .OrderBy(hitObject => hitObject.StartTime)
                                    .ToArray();

            Assert.That(scratchNotes, Has.Length.EqualTo(2));

            var manualClock = new ManualClock
            {
                CurrentTime = scratchNotes[0].StartTime,
                IsRunning = true,
            };

            var testClock = new FramedClock(manualClock);
            var playfield = new BmsPlayfield(beatmap)
            {
                Clock = testClock,
            };

            var firstDrawable = new DrawableBmsHitObject(scratchNotes[0])
            {
                Clock = testClock,
            };

            var secondDrawable = new DrawableBmsHitObject(scratchNotes[1])
            {
                Clock = testClock,
            };

            firstDrawable.Apply(scratchNotes[0]);
            secondDrawable.Apply(scratchNotes[1]);

            playfield.Add(firstDrawable);
            playfield.Add(secondDrawable);

            testClock.ProcessFrame();
            playfield.UpdateSubTree();

            Assert.Multiple(() =>
            {
                Assert.That(firstDrawable.OnPressed(createPressEvent(BmsAction.Scratch1)), Is.True);
                Assert.That(firstDrawable.Result.Type, Is.EqualTo(HitResult.Perfect));
                Assert.That(secondDrawable.Judged, Is.False);
            });

            manualClock.CurrentTime = scratchNotes[1].StartTime;
            testClock.ProcessFrame();
            playfield.UpdateSubTree();

            Assert.Multiple(() =>
            {
                Assert.That(secondDrawable.OnPressed(createPressEvent(BmsAction.Scratch1)), Is.True);
                Assert.That(secondDrawable.Result.Type, Is.EqualTo(HitResult.Perfect));
            });
        }

        [Test]
        public void TestScratchStreamLateHitForcesEarlierScratchMiss()
        {
            var beatmap = createScratchStreamBeatmap(scratchLaneData: "DDDD00000000000000000000");
            var scratchNotes = beatmap.HitObjects.OfType<BmsHitObject>()
                                    .Where(hitObject => hitObject.IsScratch && hitObject is not BmsHoldNote)
                                    .OrderBy(hitObject => hitObject.StartTime)
                                    .ToArray();

            Assert.That(scratchNotes, Has.Length.EqualTo(2));
            Assert.That(scratchNotes[1].StartTime - scratchNotes[0].StartTime, Is.LessThan(scratchNotes[0].HitWindows.WindowFor(HitResult.Miss)));

            var manualClock = new ManualClock
            {
                CurrentTime = scratchNotes[1].StartTime,
                IsRunning = true,
            };

            var testClock = new FramedClock(manualClock);
            var playfield = new BmsPlayfield(beatmap)
            {
                Clock = testClock,
            };

            var firstDrawable = new DrawableBmsHitObject(scratchNotes[0])
            {
                Clock = testClock,
            };

            var secondDrawable = new DrawableBmsHitObject(scratchNotes[1])
            {
                Clock = testClock,
            };

            firstDrawable.Apply(scratchNotes[0]);
            secondDrawable.Apply(scratchNotes[1]);

            playfield.Add(firstDrawable);
            playfield.Add(secondDrawable);

            testClock.ProcessFrame();
            playfield.UpdateSubTree();

            Assert.Multiple(() =>
            {
                Assert.That(secondDrawable.OnPressed(createPressEvent(BmsAction.Scratch1)), Is.True);
                Assert.That(firstDrawable.Result.Type, Is.EqualTo(HitResult.Miss));
                Assert.That(secondDrawable.Result.Type, Is.EqualTo(HitResult.Perfect));
            });
        }

        [Test]
        public void TestHoldNotesUseHeadWindowForInputStart()
        {
            var hold = new BmsHoldNote
            {
                StartTime = 1000,
                EndTime = 1500,
                LaneIndex = 1,
            };

            hold.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty
            {
                OverallDifficulty = OsuOdJudgementSystem.MapRankToOverallDifficulty(2),
            });

            var tailTimingWindows = (BmsTimingWindows)hold.Tail!.HitWindows;
            double successfulHitTailReleaseOffset = tailTimingWindows.WindowFor(HitResult.Meh, isLongNoteRelease: true) - 1;
            double successfulPoorTailReleaseOffset = tailTimingWindows.WindowFor(HitResult.Miss, isLongNoteRelease: true) - 1;
            double failedTailReleaseOffset = tailTimingWindows.WindowFor(HitResult.Miss, isLongNoteRelease: true) + 1;

            Assert.Multiple(() =>
            {
                Assert.That(hold.Head, Is.Not.Null);
                Assert.That(hold.Tail, Is.Not.Null);
                Assert.That(hold.Head!.HitWindows, Is.TypeOf<BmsTimingWindows>());
                Assert.That(hold.Tail!.HitWindows, Is.TypeOf<BmsTimingWindows>());
                Assert.That(DrawableBmsHitObject.ResultForPlayerInput(hold.Head, 0), Is.EqualTo(HitResult.Perfect));
                Assert.That(DrawableBmsHitObject.ResultForPlayerInput(hold.Head, 150), Is.EqualTo(HitResult.Miss));
                Assert.That(DrawableBmsHitObject.ResultForPlayerInput(hold.Head, hold.Head.HitWindows.WindowFor(HitResult.Miss) + 1), Is.EqualTo(HitResult.None));
                Assert.That(hold.MaximumJudgementOffset, Is.EqualTo(tailTimingWindows.WindowFor(HitResult.Miss, isLongNoteRelease: true)).Within(0.001));
                Assert.That(DrawableBmsHoldNote.HasMissedHoldStartWindow(hold, 1000 + hold.Head.HitWindows.WindowFor(HitResult.Miss) + 1), Is.True);
                Assert.That(DrawableBmsHoldNote.HasReachedHoldTail(hold, hold.EndTime - 1), Is.False);
                Assert.That(DrawableBmsHoldNote.HasReachedHoldTail(hold, hold.EndTime), Is.True);
                Assert.That(DrawableBmsHoldNote.HasMissedTailReleaseWindow(hold, hold.EndTime + successfulPoorTailReleaseOffset), Is.False);
                Assert.That(DrawableBmsHoldNote.HasMissedTailReleaseWindow(hold, hold.EndTime + failedTailReleaseOffset), Is.True);
                Assert.That(DrawableBmsHoldNote.ResultForTailRelease(hold, hold.EndTime), Is.EqualTo(HitResult.Perfect));
                Assert.That(DrawableBmsHoldNote.ResultForTailRelease(hold, hold.EndTime - successfulHitTailReleaseOffset).IsHit(), Is.True);
                Assert.That(DrawableBmsHoldNote.ResultForTailRelease(hold, hold.EndTime - successfulPoorTailReleaseOffset), Is.EqualTo(HitResult.None));
                Assert.That(DrawableBmsHoldNote.ResultForTailRelease(hold, hold.EndTime + successfulHitTailReleaseOffset).IsHit(), Is.True);
                Assert.That(DrawableBmsHoldNote.ResultForTailRelease(hold, hold.EndTime + successfulPoorTailReleaseOffset), Is.EqualTo(HitResult.None));
                Assert.That(DrawableBmsHoldNote.ResultForTailRelease(hold, hold.EndTime + failedTailReleaseOffset), Is.EqualTo(HitResult.None));
            });
        }

        [Test]
        public void TestLnPoorHeadPressConsumesInputWithoutStartingHold()
        {
            var hold = new BmsHoldNote
            {
                StartTime = 1000,
                EndTime = 1500,
                LaneIndex = 1,
            };

            hold.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty
            {
                OverallDifficulty = OsuOdJudgementSystem.MapRankToOverallDifficulty(2),
            });

            var manualClock = new ManualClock
            {
                CurrentTime = hold.StartTime + hold.Head!.HitWindows.WindowFor(HitResult.Meh) + 1,
                IsRunning = true,
            };

            var testClock = new FramedClock(manualClock);
            var drawable = new DrawableBmsHoldNote(hold)
            {
                LongNoteModeOverrideForTesting = BmsLongNoteMode.LN,
                Clock = testClock,
            };

            drawable.Apply(hold);
            testClock.ProcessFrame();

            foreach (var nested in drawable.NestedHitObjects)
                nested.Clock = testClock;

            var headDrawable = drawable.NestedHitObjects.OfType<DrawableBmsHoldNoteHead>().Single();
            var tailDrawable = drawable.NestedHitObjects.OfType<DrawableBmsHoldNoteTail>().Single();

            Assert.Multiple(() =>
            {
                Assert.That(drawable.OnPressed(createPressEvent()), Is.True);
                Assert.That(headDrawable.Result.Type, Is.EqualTo(HitResult.Miss));
                Assert.That(drawable.IsHoldingForTesting, Is.False);
                Assert.That(tailDrawable.Judged, Is.False);
            });
        }

        [Test]
        public void TestHeadPressDoesNotImmediatelyResolveHoldTail()
        {
            var hold = new BmsHoldNote
            {
                StartTime = 1000,
                EndTime = 1005,
                LaneIndex = 1,
            };

            hold.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty
            {
                OverallDifficulty = OsuOdJudgementSystem.MapRankToOverallDifficulty(2),
            });

            var testClock = new FramedClock(new ManualClock
            {
                CurrentTime = hold.StartTime,
                IsRunning = true,
            });

            var drawable = new DrawableBmsHoldNote(hold);
            drawable.Clock = testClock;
            drawable.Apply(hold);

            foreach (var nested in drawable.NestedHitObjects)
                nested.Clock = testClock;

            Assert.Multiple(() =>
            {
                Assert.That(drawable.TryApplyHeadPress(HitResult.Perfect), Is.True);
                Assert.That(drawable.Judged, Is.False);
                Assert.That(drawable.AllJudged, Is.False);
            });
        }

        [Test]
        public void TestHcnLateBodyPressStartsHoldAfterHeadMiss()
        {
            var hold = new BmsHoldNote
            {
                StartTime = 1000,
                EndTime = 1500,
                LaneIndex = 1,
            };

            hold.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty
            {
                OverallDifficulty = OsuOdJudgementSystem.MapRankToOverallDifficulty(2),
            });

            double latePressTime = hold.StartTime + hold.Head!.HitWindows.WindowFor(HitResult.Miss) + 1;
            var testClock = new FramedClock(new ManualClock
            {
                CurrentTime = latePressTime,
                IsRunning = true,
            });

            var drawable = new DrawableBmsHoldNote(hold)
            {
                LongNoteModeOverrideForTesting = BmsLongNoteMode.HCN,
                Clock = testClock,
            };

            drawable.Apply(hold);

            foreach (var nested in drawable.NestedHitObjects)
                nested.Clock = testClock;

            var headDrawable = drawable.NestedHitObjects.OfType<DrawableBmsHoldNoteHead>().Single();

            Assert.Multiple(() =>
            {
                Assert.That(drawable.TryApplyLateBodyPress(), Is.True);
                Assert.That(drawable.IsHoldingForTesting, Is.True);
                Assert.That(headDrawable.Result.Type, Is.EqualTo(HitResult.Miss));
                Assert.That(drawable.AllJudged, Is.False);
            });
        }

        [Test]
        public void TestCnEarlyReleaseCanRepressAndResolveTail()
        {
            var hold = new BmsHoldNote
            {
                StartTime = 1000,
                EndTime = 1500,
                LaneIndex = 1,
            };

            hold.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty
            {
                OverallDifficulty = OsuOdJudgementSystem.MapRankToOverallDifficulty(2),
            });

            var manualClock = new ManualClock
            {
                CurrentTime = hold.StartTime,
                IsRunning = true,
            };

            var testClock = new FramedClock(manualClock);

            var drawable = new DrawableBmsHoldNote(hold)
            {
                LongNoteModeOverrideForTesting = BmsLongNoteMode.CN,
                Clock = testClock,
            };

            drawable.Apply(hold);
            testClock.ProcessFrame();

            foreach (var nested in drawable.NestedHitObjects)
                nested.Clock = testClock;

            var headDrawable = drawable.NestedHitObjects.OfType<DrawableBmsHoldNoteHead>().Single();
            var tailDrawable = drawable.NestedHitObjects.OfType<DrawableBmsHoldNoteTail>().Single();

            Assert.That(drawable.OnPressed(createPressEvent()), Is.True);
            Assert.That(headDrawable.Result.Type, Is.EqualTo(HitResult.Perfect));

            manualClock.CurrentTime = hold.StartTime + 10;
            testClock.ProcessFrame();
            drawable.OnReleased(createReleaseEvent());

            Assert.Multiple(() =>
            {
                Assert.That(drawable.IsHoldingForTesting, Is.False);
                Assert.That(tailDrawable.Judged, Is.False);
                Assert.That(drawable.AllJudged, Is.False);
            });

            manualClock.CurrentTime = hold.EndTime;
            testClock.ProcessFrame();

            Assert.That(drawable.OnPressed(createPressEvent()), Is.True);
            Assert.That(drawable.IsHoldingForTesting, Is.True);

            drawable.OnReleased(createReleaseEvent());

            Assert.Multiple(() =>
            {
                Assert.That(tailDrawable.Result.Type, Is.EqualTo(HitResult.Perfect));
                Assert.That(drawable.AllJudged, Is.True);
            });
        }

        [Test]
        public void TestCnLatePressStartsHoldAfterHeadMiss()
        {
            var hold = new BmsHoldNote
            {
                StartTime = 1000,
                EndTime = 1500,
                LaneIndex = 1,
            };

            hold.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty
            {
                OverallDifficulty = OsuOdJudgementSystem.MapRankToOverallDifficulty(2),
            });

            double latePressTime = hold.StartTime + hold.Head!.HitWindows.WindowFor(HitResult.Miss) + 1;
            var manualClock = new ManualClock
            {
                CurrentTime = latePressTime,
                IsRunning = true,
            };

            var testClock = new FramedClock(manualClock);

            var drawable = new DrawableBmsHoldNote(hold)
            {
                LongNoteModeOverrideForTesting = BmsLongNoteMode.CN,
                Clock = testClock,
            };

            drawable.Apply(hold);
            testClock.ProcessFrame();

            foreach (var nested in drawable.NestedHitObjects)
                nested.Clock = testClock;

            var headDrawable = drawable.NestedHitObjects.OfType<DrawableBmsHoldNoteHead>().Single();
            var tailDrawable = drawable.NestedHitObjects.OfType<DrawableBmsHoldNoteTail>().Single();

            Assert.That(drawable.OnPressed(createPressEvent()), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(drawable.IsHoldingForTesting, Is.True);
                Assert.That(headDrawable.Result.Type, Is.EqualTo(HitResult.Miss));
                Assert.That(tailDrawable.Judged, Is.False);
            });

            manualClock.CurrentTime = hold.EndTime;
            testClock.ProcessFrame();
            drawable.OnReleased(createReleaseEvent());

            Assert.Multiple(() =>
            {
                Assert.That(tailDrawable.Result.Type, Is.EqualTo(HitResult.Perfect));
                Assert.That(drawable.AllJudged, Is.True);
            });
        }

        [TestCase(BmsLongNoteMode.LN, HitResult.IgnoreHit)]
        [TestCase(BmsLongNoteMode.CN, HitResult.Perfect)]
        [TestCase(BmsLongNoteMode.HCN, HitResult.Perfect)]
        public void TestHoldingThroughTailAutoResolvesHold(BmsLongNoteMode longNoteMode, HitResult expectedTailResult)
        {
            var hold = new BmsHoldNote
            {
                StartTime = 1000,
                EndTime = 1500,
                LaneIndex = 1,
            };

            hold.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty
            {
                OverallDifficulty = OsuOdJudgementSystem.MapRankToOverallDifficulty(2),
            });

            var manualClock = new ManualClock
            {
                CurrentTime = hold.StartTime,
                IsRunning = true,
            };

            var testClock = new FramedClock(manualClock);

            var drawable = new TestDrawableBmsHoldNote(hold)
            {
                LongNoteModeOverrideForTesting = longNoteMode,
                Clock = testClock,
            };

            drawable.Apply(hold);
            testClock.ProcessFrame();

            foreach (var nested in drawable.NestedHitObjects)
                nested.Clock = testClock;

            var headDrawable = drawable.NestedHitObjects.OfType<DrawableBmsHoldNoteHead>().Single();
            var tailDrawable = drawable.NestedHitObjects.OfType<DrawableBmsHoldNoteTail>().Single();

            Assert.That(drawable.OnPressed(createPressEvent()), Is.True);

            manualClock.CurrentTime = hold.EndTime;
            testClock.ProcessFrame();
            drawable.TriggerAutoJudgementForTesting();

            Assert.Multiple(() =>
            {
                Assert.That(headDrawable.Result.Type, Is.EqualTo(HitResult.Perfect));
                Assert.That(tailDrawable.Result.Type, Is.EqualTo(expectedTailResult));
                Assert.That(drawable.IsHoldingForTesting, Is.False);
                Assert.That(drawable.AllJudged, Is.True);
            });
        }

        [Test]
        public void TestTailJudgedModesLatePressStopAfterTailMissWindow()
        {
            var hold = new BmsHoldNote
            {
                StartTime = 1000,
                EndTime = 1500,
                LaneIndex = 1,
            };

            hold.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty
            {
                OverallDifficulty = OsuOdJudgementSystem.MapRankToOverallDifficulty(2),
            });

            double failedTailReleaseOffset = ((BmsTimingWindows)hold.Tail!.HitWindows).WindowFor(HitResult.Miss, isLongNoteRelease: true) + 1;

            Assert.Multiple(() =>
            {
                Assert.That(DrawableBmsHoldNote.CanApplyLateBodyPress(BmsLongNoteMode.HCN, hold, tailJudged: false, hold.EndTime + failedTailReleaseOffset), Is.False);
                Assert.That(DrawableBmsHoldNote.CanApplyLateBodyPress(BmsLongNoteMode.CN, hold, tailJudged: false, hold.EndTime + failedTailReleaseOffset), Is.False);
            });
        }

        [TestCase(BmsLongNoteMode.CN)]
        [TestCase(BmsLongNoteMode.HCN)]
        public void TestTailJudgedModesAllowLatePressThroughTailMissBoundary(BmsLongNoteMode longNoteMode)
        {
            var hold = new BmsHoldNote
            {
                StartTime = 1000,
                EndTime = 1500,
                LaneIndex = 1,
            };

            hold.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty
            {
                OverallDifficulty = OsuOdJudgementSystem.MapRankToOverallDifficulty(2),
            });

            double missWindow = ((BmsTimingWindows)hold.Tail!.HitWindows).WindowFor(HitResult.Miss, isLongNoteRelease: true);

            Assert.Multiple(() =>
            {
                Assert.That(DrawableBmsHoldNote.CanApplyLateBodyPress(longNoteMode, hold, tailJudged: false, hold.EndTime + missWindow), Is.True);
                Assert.That(DrawableBmsHoldNote.CanApplyLateBodyPress(longNoteMode, hold, tailJudged: false, hold.EndTime + missWindow + 0.001), Is.False);
                Assert.That(DrawableBmsHoldNote.CanApplyLateBodyPress(longNoteMode, hold, tailJudged: true, hold.EndTime + missWindow), Is.False);
            });
        }

        [Test]
        public void TestAutoPlayObjectsStillApplyMaxResult()
        {
            var beatmap = createPlayableBeatmap();
            var bgm = beatmap.HitObjects.OfType<BmsBgmEvent>().Single();
            var tail = beatmap.HitObjects.OfType<BmsHoldNote>().Single().Tail!;
            var autoPlayNote = new BmsHitObject
            {
                StartTime = 1000,
                LaneIndex = 0,
                AutoPlay = true,
            };

            Assert.Multiple(() =>
            {
                Assert.That(DrawableBmsHitObject.ShouldAutoApplyMaxResult(bgm), Is.True);
                Assert.That(DrawableBmsHitObject.ShouldAutoApplyMaxResult(tail), Is.False);
                Assert.That(DrawableBmsHitObject.ShouldAutoApplyMaxResult(autoPlayNote), Is.True);
            });
        }

        private BmsBeatmap createScratchStreamBeatmap(string scratchLaneData = "DDDD", int rank = 2)
            => createBeatmapFromText($@"
#TITLE Scratch Stream Stub
#BPM 120
#RANK {rank}
#00101:AA00
#WAVAA bgm.wav
#WAVDD scratch.wav
    #00116:{scratchLaneData}
", "scratch-stream-stub.bme");

        private BmsBeatmap createPlayableBeatmap(string? stageFile = null, string? backgroundFile = null, int rank = 2)
        {
            string metadata = string.Empty;

            if (!string.IsNullOrWhiteSpace(stageFile))
                metadata += $"#STAGEFILE {stageFile}\n";

            if (!string.IsNullOrWhiteSpace(backgroundFile))
                metadata += $"#BACKBMP {backgroundFile}\n";

            string text = $@"
#TITLE Drawable Stub
#BPM 120
#RANK {rank}
{metadata}#00101:AA00
#WAVAA bgm.wav
#WAVBB key1.wav
#WAVCC keys/note.wav
#WAVDD scratch.wav
#WAVEE hold/head.ogg
#WAVFF hold/tail.wav
#00111:BB00
#00112:CC00
#00116:DD00
#LNTYPE 1
#00152:EE00FF00
";

            return createBeatmapFromText(text, "drawable-stub.bme");
        }

        private BmsBeatmap createBeatmapFromText(string text, string path)
        {
            var decodedChart = decoder.DecodeText(text, path);
            return (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();
        }

        private static Mod createLongNoteModeMod(BmsLongNoteMode longNoteMode)
            => longNoteMode switch
            {
                BmsLongNoteMode.CN => new BmsModChargeNote(),
                BmsLongNoteMode.HCN => new BmsModHellChargeNote(),
                _ => throw new AssertionException($"Unsupported long note mode test input: {longNoteMode}"),
            };

        private static Mod createJudgeModeMod(BmsJudgeMode judgeMode)
            => judgeMode switch
            {
                BmsJudgeMode.Beatoraja => new BmsModJudgeBeatoraja(),
                BmsJudgeMode.LR2 => new BmsModJudgeLr2(),
                BmsJudgeMode.IIDX => new BmsModJudgeIidx(),
                _ => throw new AssertionException($"Unsupported judge mode test input: {judgeMode}"),
            };

        private static KeyBindingPressEvent<BmsAction> createPressEvent(BmsAction action = BmsAction.Key1)
            => new KeyBindingPressEvent<BmsAction>(new osu.Framework.Input.States.InputState(), action);

        private static KeyBindingReleaseEvent<BmsAction> createReleaseEvent(BmsAction action = BmsAction.Key1)
            => new KeyBindingReleaseEvent<BmsAction>(new osu.Framework.Input.States.InputState(), action);

        private static (BmsLane Lane, DrawableBmsHitObject Drawable, ManualClock ManualClock, FramedClock TestClock) createLaneWithDrawable(BmsBeatmap beatmap, BmsHitObject hitObject)
        {
            var layout = BmsLaneLayout.CreateFor(beatmap);
            var laneDefinition = layout.Lanes.Single(lane => lane.LaneIndex == hitObject.LaneIndex && lane.IsScratch == hitObject.IsScratch);
            var manualClock = new ManualClock
            {
                CurrentTime = hitObject.StartTime,
                IsRunning = true,
            };
            var testClock = new FramedClock(manualClock);
            var lane = new BmsLane(laneDefinition, layout.Lanes.Count, layout.Keymode, layout.Profile)
            {
                Clock = testClock,
            };
            var drawable = new DrawableBmsHitObject(hitObject)
            {
                Clock = testClock,
            };

            drawable.Apply(hitObject);
            lane.Add(drawable);
            testClock.ProcessFrame();

            return (lane, drawable, manualClock, testClock);
        }

        private sealed partial class TestableDrawableBmsRuleset : DrawableBmsRuleset
        {
            public TestableDrawableBmsRuleset(BmsRuleset ruleset, IBeatmap beatmap)
                : base(ruleset, beatmap)
            {
            }

            public bool UsesRelativeBeatLengthScaling => RelativeScaleBeatLengths;
        }

        private sealed partial class TestDrawableBmsHoldNote : DrawableBmsHoldNote
        {
            public TestDrawableBmsHoldNote(BmsHoldNote hitObject)
                : base(hitObject)
            {
            }

            public void TriggerAutoJudgementForTesting() => CheckForResult(false, 0);
        }
    }
}
