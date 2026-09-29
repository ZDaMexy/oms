// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Diagnostics;
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
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public partial class BmsNativeGameplayPerformanceTest
    {
        [Test]
        public void TestRepeatedEarlyPressesInDenseLane()
        {
            var beatmap = createBeatmap();
            var clock = new FramedClock(new ManualClock { CurrentTime = 1000 });
            using var playfield = BmsPlayfield.CreateCompatibility(beatmap);
            playfield.Clock = clock;
            clock.ProcessFrame();

            var notes = Enumerable.Range(0, 64).Select(index =>
            {
                var note = new BmsHitObject { StartTime = 5000 + index * 10, LaneIndex = 1, Keymode = BmsKeymode.Key7K };
                note.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                var drawable = new DrawableBmsHitObject(note) { Clock = clock };
                drawable.Apply(note);
                playfield.Add(drawable);
                return drawable;
            }).ToArray();
            var press = new KeyBindingPressEvent<BmsAction>(new Framework.Input.States.InputState(), BmsAction.Key1);

            for (int warmup = 0; warmup < 8; warmup++)
                pressEveryCandidate();

            var stopwatch = Stopwatch.StartNew();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int repeat = 0; repeat < 256; repeat++)
                pressEveryCandidate();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            stopwatch.Stop();

            Assert.That(notes.All(note => !note.Judged), Is.True, "Early presses must leave every candidate unjudged.");
            Assert.That(allocated, Is.Zero, "Stable input must reuse the owner's ordered view without allocating per candidate.");
            TestContext.Progress.WriteLine($"native-early-press: candidates=64 repeats=256 allocated={allocated} elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F3}");

            void pressEveryCandidate()
            {
                foreach (var drawable in notes)
                {
                    if (drawable.OnPressed(press))
                        throw new InvalidOperationException("A note consumed an out-of-window early press.");
                }
            }
        }

        [TestCase(BmsLongNoteMode.LN)]
        [TestCase(BmsLongNoteMode.CN)]
        [TestCase(BmsLongNoteMode.HCN)]
        public void TestLongHoldSteadyJudgementCost(BmsLongNoteMode mode)
        {
            var hold = new BmsHoldNote { StartTime = 1000, Duration = 60_000, LaneIndex = 1, Keymode = BmsKeymode.Key7K };
            hold.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            var manualClock = new ManualClock { CurrentTime = hold.StartTime };
            var clock = new FramedClock(manualClock);
            using var drawable = new TestHold(hold) { Clock = clock, LongNoteModeOverrideForTesting = mode };
            drawable.Apply(hold);
            foreach (var child in drawable.NestedHitObjects)
                child.Clock = clock;
            clock.ProcessFrame();
            Assert.That(drawable.TryApplyHeadPress(HitResult.Perfect), Is.True);

            measureAt(hold.StartTime + 1000, "early");
            measureAt(hold.StartTime + 55_000, "late");

            manualClock.CurrentTime = hold.EndTime;
            clock.ProcessFrame();
            drawable.JudgeFrame();
            Assert.That(drawable.AllJudged, Is.True);
            Assert.That(drawable.NestedHitObjects.OfType<DrawableBmsHoldNoteBodyTick>().All(tick => tick.IsHit), Is.True);

            void measureAt(double time, string position)
            {
                manualClock.CurrentTime = time;
                clock.ProcessFrame();
                for (int warmup = 0; warmup < 128; warmup++)
                    drawable.JudgeFrame();
                var stopwatch = Stopwatch.StartNew();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int frame = 0; frame < 16_384; frame++)
                    drawable.JudgeFrame();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                stopwatch.Stop();
                TestContext.Progress.WriteLine($"native-hold: mode={mode} position={position} frames=16384 allocated={allocated} elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F3}");
            }
        }

        [Test]
        public void TestLongNoteModeReadCost()
        {
            using var ruleset = (DrawableBmsRuleset)new BmsRuleset().CreateDrawableRulesetWith(createBeatmap(), new Mod[] { new BmsModHellChargeNote() });
            for (int warmup = 0; warmup < 128; warmup++)
                _ = ruleset.LongNoteMode;

            var stopwatch = Stopwatch.StartNew();
            long before = GC.GetAllocatedBytesForCurrentThread();
            BmsLongNoteMode mode = default;
            for (int frame = 0; frame < 65_536; frame++)
                mode = ruleset.LongNoteMode;
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            stopwatch.Stop();

            Assert.That(mode, Is.EqualTo(BmsLongNoteMode.HCN));
            Assert.That(allocated, Is.Zero, "The fixed gameplay mode must not rescan Mods on every hold frame.");
            TestContext.Progress.WriteLine($"native-ln-mode: reads=65536 allocated={allocated} elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F3}");
        }

        [Test]
        public void TestContainerOrderTracksInactiveEditsAndRemoval()
        {
            using var container = new HitObjectContainer();
            var first = new HitObjectLifetimeEntry(new BmsHitObject { StartTime = 3000 });
            var second = new HitObjectLifetimeEntry(new BmsHitObject { StartTime = 1000 });
            var equal = new HitObjectLifetimeEntry(new BmsHitObject { StartTime = 1000 });
            container.Add(first);
            container.Add(second);
            container.Add(equal);

            var original = container.OrderedEntries;
            Assert.That(original, Is.EqualTo(new[] { second, equal, first }));
            Assert.That(container.OrderedEntries, Is.SameAs(original));

            // These entries have no drawable at all; edits must still invalidate the full-chart view.
            first.HitObject.StartTime = 500;
            var changed = container.OrderedEntries;
            Assert.That(changed, Is.Not.SameAs(original));
            Assert.That(changed, Is.EqualTo(new[] { first, second, equal }));

            Assert.That(container.Remove(second), Is.True);
            var removed = container.OrderedEntries;
            Assert.That(removed, Is.EqualTo(new[] { first, equal }));
            second.HitObject.StartTime = 0;
            Assert.That(container.OrderedEntries, Is.SameAs(removed), "Removed objects must release their ordering subscription.");

            container.Add(second);
            Assert.That(container.OrderedEntries, Is.EqualTo(new[] { second, first, equal }));
        }

        [TestCase(BmsLongNoteMode.LN)]
        [TestCase(BmsLongNoteMode.CN)]
        [TestCase(BmsLongNoteMode.HCN)]
        public void TestLongHoldRejudgesRevertedTicksAndResetsOnReuse(BmsLongNoteMode mode)
        {
            var hold = createHold(1000);
            var manualClock = new ManualClock { CurrentTime = hold.StartTime };
            var clock = new FramedClock(manualClock);
            using var drawable = new TestHold(hold) { Clock = clock, LongNoteModeOverrideForTesting = mode };
            apply(hold);

            for (double time = hold.StartTime + 100; time <= hold.StartTime + 1000; time += 100)
                seek(time);
            Assert.That(ticks().Count(tick => tick.Judged), Is.EqualTo(10));

            // This is the entry's actual result-reversion notification used by the playfield during a seek.
            // The parent stays applied, matching a rewind within an already visible long note.
            int revertedTicks = 0;
            drawable.OnRevertResult += (nested, _) =>
            {
                if (nested is DrawableBmsHoldNoteBodyTick)
                    revertedTicks++;
            };
            manualClock.CurrentTime = hold.StartTime + 200;
            clock.ProcessFrame();
            foreach (var tick in ticks().Where(tick => tick.Judged && tick.Result.RawTime > manualClock.CurrentTime).Reverse())
            {
                tick.Entry!.OnRevertResult();
                tick.Result.Reset();
            }
            Assert.That(revertedTicks, Is.EqualTo(8), "Nested entry reverts must reach the live parent before judgement resumes.");

            seek(hold.StartTime + 600);
            Assert.That(ticks().Count(tick => tick.Judged), Is.EqualTo(6));
            Assert.That(ticks().Where(tick => tick.Judged).All(tick => tick.IsHit), Is.True);

            seek(hold.EndTime);
            Assert.That(drawable.AllJudged, Is.True);

            // Reapplying the same drawable exercises the same OnFree/OnApply reset as a lane pool reuse.
            var next = createHold(5000);
            apply(next);
            seek(next.StartTime + 300);
            Assert.That(ticks().Count(tick => tick.Judged), Is.EqualTo(3));
            Assert.That(ticks().Where(tick => tick.Judged).All(tick => tick.IsHit), Is.True);
            seek(next.EndTime);
            Assert.That(drawable.AllJudged, Is.True);

            DrawableBmsHoldNoteBodyTick[] ticks() => drawable.NestedHitObjects.OfType<DrawableBmsHoldNoteBodyTick>().ToArray();

            void apply(BmsHoldNote note)
            {
                manualClock.CurrentTime = note.StartTime;
                clock.ProcessFrame();
                drawable.Apply(note);
                drawable.LongNoteModeOverrideForTesting = mode;
                foreach (var child in drawable.NestedHitObjects)
                {
                    child.Clock = clock;
                    // The detached fixture has no LoadAsyncComplete. Apply the existing synthetic entry as the
                    // real loader does, including its result-reversion subscription and parent entry identity.
                    child.Apply(child.Entry!);
                }
                Assert.That(drawable.TryApplyHeadPress(HitResult.Perfect), Is.True);
            }

            void seek(double time)
            {
                manualClock.CurrentTime = time;
                clock.ProcessFrame();
                drawable.JudgeFrame();
            }

            static BmsHoldNote createHold(double startTime)
            {
                var note = new BmsHoldNote { StartTime = startTime, Duration = 2000, LaneIndex = 1, Keymode = BmsKeymode.Key7K };
                note.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                return note;
            }
        }

        private static BmsBeatmap createBeatmap()
        {
            var decoded = new BmsBeatmapDecoder().DecodeText("#TITLE Performance fixture\n#BPM 120\n#00111:0100\n", "native-performance.bme");
            return (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decoded), new BmsRuleset()).Convert();
        }

        private partial class TestHold : DrawableBmsHoldNote
        {
            public TestHold(BmsHoldNote hold)
                : base(hold)
            {
            }

            public void JudgeFrame() => CheckForResult(false, Time.Current - HitObject.StartTime);
        }
    }
}
