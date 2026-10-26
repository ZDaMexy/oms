// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Input.Events;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public partial class BmsJudgementTimingProvenanceTest
    {
        [TestCase(false, 0, HitResult.Perfect)]
        [TestCase(false, -30, HitResult.Great)]
        [TestCase(false, 30, HitResult.Great)]
        [TestCase(false, 150, HitResult.Miss)]
        [TestCase(true, -30, HitResult.Great)]
        public void TestActualNotePressRetainsItsTimingAndOriginalResult(bool scratch, double offset, HitResult expected)
        {
            BmsHitObject note = createNote(scratch);
            var manual = new ManualClock { CurrentTime = note.StartTime + offset };
            var clock = new FramedClock(manual);
            using var drawable = new TestNote(note) { Clock = clock };
            drawable.Apply(note);
            clock.ProcessFrame();
            bool? timingAtDelivery = null;
            drawable.OnNewResult += (_, result) => timingAtDelivery = metadata(result).HasPlayerTiming;

            Assert.That(drawable.OnPressed(press(scratch)), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(timingAtDelivery, Is.True, "Provenance must be set before the real judgement event is delivered.");
                Assert.That(metadata(drawable.Result).HasPlayerTiming, Is.True,
                    "This records input provenance even for a pressed POOR; display qualification remains separate.");
                Assert.That(drawable.Result.Type, Is.EqualTo(expected));
                Assert.That(drawable.Result.RawTime, Is.EqualTo(note.StartTime + offset));
                Assert.That(drawable.Result.TimeOffset, Is.EqualTo(offset));
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestAutomaticNoteCompletionHasNoPlayerTiming(bool scratch)
        {
            BmsHitObject note = createNote(scratch);
            var beatmap = new BmsBeatmap { HitObjects = { note } };
            if (scratch)
                new BmsModAutoScratch().ApplyToBeatmap(beatmap);
            else
                new BmsModAutoNote().ApplyToBeatmap(beatmap);

            var manual = new ManualClock { CurrentTime = note.StartTime + 7 };
            var clock = new FramedClock(manual);
            using var drawable = new TestNote(note) { Clock = clock };
            drawable.Apply(note);
            clock.ProcessFrame();

            Assert.That(drawable.OnPressed(press(scratch)), Is.False);
            drawable.JudgeFrame();

            Assert.Multiple(() =>
            {
                Assert.That(drawable.Result.Type, Is.EqualTo(HitResult.IgnoreHit));
                Assert.That(drawable.Result.RawTime, Is.EqualTo(note.StartTime + 7));
                Assert.That(drawable.Result.TimeOffset, Is.EqualTo(7));
                Assert.That(metadata(drawable.Result).HasPlayerTiming, Is.False,
                    "A frame-sized automatic offset is not measured player timing.");
            });
        }

        [Test]
        public void TestRewindAndPooledReuseDoNotRetainPreviousInputProvenance()
        {
            BmsHitObject note = createNote(false);
            var manual = new ManualClock { CurrentTime = note.StartTime + 30 };
            var clock = new FramedClock(manual);
            using var drawable = new TestNote(note) { Clock = clock };
            drawable.Apply(note);
            clock.ProcessFrame();
            Assert.That(drawable.OnPressed(press(false)), Is.True);
            Assert.That(metadata(drawable.Result).HasPlayerTiming, Is.True);

            drawable.Entry!.OnRevertResult();
            drawable.Result.Reset();
            Assert.That(metadata(drawable.Result).HasPlayerTiming, Is.False, "A reverted result has no surviving timing observation.");

            manual.CurrentTime = note.StartTime + 1000;
            clock.ProcessFrame();
            drawable.JudgeFrame();
            Assert.Multiple(() =>
            {
                Assert.That(drawable.Result.Type, Is.EqualTo(HitResult.Miss));
                Assert.That(drawable.Result.RawTime, Is.EqualTo(note.StartTime + 1000));
                Assert.That(metadata(drawable.Result).HasPlayerTiming, Is.False, "Timeout after a rewind must replace the old input provenance.");
            });

            BmsHitObject next = createNote(false);
            next.StartTime = 5000;
            next.AutoPlay = true;
            manual.CurrentTime = next.StartTime + 7;
            clock.ProcessFrame();
            drawable.Apply(next);
            drawable.JudgeFrame();
            Assert.Multiple(() =>
            {
                Assert.That(drawable.Result.Type, Is.EqualTo(HitResult.Perfect));
                Assert.That(drawable.Result.RawTime, Is.EqualTo(next.StartTime + 7));
                Assert.That(drawable.Result.TimeOffset, Is.EqualTo(7));
                Assert.That(metadata(drawable.Result).HasPlayerTiming, Is.False);
            });
        }

        [TestCase(BmsLongNoteMode.LN, false, false)]
        [TestCase(BmsLongNoteMode.LN, true, false)]
        [TestCase(BmsLongNoteMode.CN, false, false)]
        [TestCase(BmsLongNoteMode.CN, true, false)]
        [TestCase(BmsLongNoteMode.HCN, false, false)]
        [TestCase(BmsLongNoteMode.HCN, true, false)]
        [TestCase(BmsLongNoteMode.CN, true, true)]
        [TestCase(BmsLongNoteMode.HCN, false, true)]
        public void TestHoldHeadAndReleaseAreDistinctFromAutomaticTailCompletion(BmsLongNoteMode mode, bool release, bool scratch)
        {
            BmsHoldNote hold = createHold(scratch);
            var manual = new ManualClock { CurrentTime = hold.StartTime - 30 };
            var clock = new FramedClock(manual);
            using var drawable = new TestHold(hold) { Clock = clock, LongNoteModeOverrideForTesting = mode };
            drawable.Apply(hold);
            foreach (var child in drawable.NestedHitObjects)
                child.Clock = clock;
            clock.ProcessFrame();
            var head = drawable.NestedHitObjects.OfType<DrawableBmsHoldNoteHead>().Single();
            var tail = drawable.NestedHitObjects.OfType<DrawableBmsHoldNoteTail>().Single();

            Assert.That(drawable.OnPressed(press(scratch)), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(head.Result.Type, Is.EqualTo(HitResult.Great));
                Assert.That(head.Result.RawTime, Is.EqualTo(hold.StartTime - 30));
                Assert.That(head.Result.TimeOffset, Is.EqualTo(-30));
                Assert.That(metadata(head.Result).HasPlayerTiming, Is.True);
            });

            manual.CurrentTime = hold.EndTime + (release ? -30 : 7);
            clock.ProcessFrame();
            if (release)
                drawable.OnReleased(new KeyBindingReleaseEvent<BmsAction>(new Framework.Input.States.InputState(), action(scratch)));
            else
                drawable.JudgeFrame();

            Assert.Multiple(() =>
            {
                Assert.That(tail.Result.Type, Is.EqualTo(mode.RequiresTailJudgement()
                    ? release ? HitResult.Great : HitResult.Perfect
                    : HitResult.IgnoreHit));
                Assert.That(tail.Result.RawTime, Is.EqualTo(manual.CurrentTime));
                Assert.That(tail.Result.TimeOffset, Is.EqualTo(release ? -30 : 7));
                Assert.That(metadata(tail.Result).HasPlayerTiming, Is.EqualTo(release),
                    "Only the real release carries timing; an ignored LN release still retains its real provenance.");
                Assert.That(metadata(drawable.Result).HasPlayerTiming, Is.False, "The parent only completes its existing bookkeeping.");
                Assert.That(drawable.NestedHitObjects.OfType<DrawableBmsHoldNoteBodyTick>()
                                    .All(tick => tick.Result is not IGameplaySkinJudgementTimingMetadata), Is.True);
                Assert.That(drawable.AllJudged, Is.True);
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestAssistedHoldNeverAcquiresPlayerTiming(bool scratch)
        {
            BmsHoldNote hold = createHold(scratch);
            var beatmap = new BmsBeatmap { HitObjects = { hold } };
            BmsLongNoteMode.HCN.ApplyToBeatmap(beatmap);
            if (scratch)
                new BmsModAutoScratch().ApplyToBeatmap(beatmap);
            else
                new BmsModAutoNote().ApplyToBeatmap(beatmap);

            var manual = new ManualClock { CurrentTime = hold.StartTime + 7 };
            var clock = new FramedClock(manual);
            using var drawable = new TestHold(hold) { Clock = clock };
            drawable.Apply(hold);
            foreach (var child in drawable.NestedHitObjects)
                child.Clock = clock;
            clock.ProcessFrame();

            Assert.That(drawable.OnPressed(press(scratch)), Is.False);
            drawable.JudgeFrame();
            manual.CurrentTime = hold.EndTime + 7;
            clock.ProcessFrame();
            drawable.JudgeFrame();

            Assert.Multiple(() =>
            {
                Assert.That(drawable.AllJudged, Is.True);
                foreach (var child in drawable.NestedHitObjects.OfType<DrawableBmsHitObject>())
                {
                    Assert.That(child.Result.Type, Is.EqualTo(HitResult.IgnoreHit));
                    Assert.That(metadata(child.Result).HasPlayerTiming, Is.False);
                }
                Assert.That(metadata(drawable.Result).HasPlayerTiming, Is.False);
            });
        }

        private static IGameplaySkinJudgementTimingMetadata metadata(JudgementResult result)
            => (IGameplaySkinJudgementTimingMetadata)result;

        private static KeyBindingPressEvent<BmsAction> press(bool scratch)
            => new KeyBindingPressEvent<BmsAction>(new Framework.Input.States.InputState(), action(scratch));

        private static BmsAction action(bool scratch) => scratch ? BmsAction.Scratch1 : BmsAction.Key1;

        private static BmsHitObject createNote(bool scratch)
        {
            var note = new BmsHitObject { StartTime = 1000, LaneIndex = scratch ? 0 : 1, IsScratch = scratch, Keymode = BmsKeymode.Key7K };
            note.ApplyDefaults(new ControlPointInfo(), difficulty());
            return note;
        }

        private static BmsHoldNote createHold(bool scratch)
        {
            var hold = new BmsHoldNote { StartTime = 1000, Duration = 1000, LaneIndex = scratch ? 0 : 1, IsScratch = scratch, Keymode = BmsKeymode.Key7K };
            hold.ApplyDefaults(new ControlPointInfo(), difficulty());
            return hold;
        }

        private static BeatmapDifficulty difficulty()
            => new BeatmapDifficulty { OverallDifficulty = OsuOdJudgementSystem.MapRankToOverallDifficulty(2) };

        private sealed partial class TestNote : DrawableBmsHitObject
        {
            public TestNote(BmsHitObject note)
                : base(note)
            {
            }

            public void JudgeFrame() => CheckForResult(false, Time.Current - HitObject.StartTime);
        }

        private sealed partial class TestHold : DrawableBmsHoldNote
        {
            public TestHold(BmsHoldNote hold)
                : base(hold)
            {
            }

            public void JudgeFrame() => CheckForResult(false, Time.Current - HitObject.StartTime);
        }
    }
}
