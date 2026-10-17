// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Timing;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Tests
{
    public partial class BmsScoreProcessorTest
    {
        [TestCase(HitResult.Perfect, true)]
        [TestCase(HitResult.Great, true)]
        [TestCase(HitResult.Good, true)]
        [TestCase(HitResult.Miss, false)]
        public void TestFullComboCompletionUsesActualMaximumAndCompletionRatherThanBreakCount(HitResult result, bool expected)
        {
            var beatmap = createBeatmap(3);
            using var processor = new TestableBmsScoreProcessor();
            var manual = new ManualClock { CurrentTime = 10000 };
            var clock = new FramedClock(manual);
            clock.ProcessFrame();
            processor.Clock = clock;
            processor.ApplyBeatmap(beatmap);
            foreach (var obj in beatmap.HitObjects)
                processor.ApplyResult(createResult(obj, result));

            Assert.That(processor.IsFullCombo, Is.False, "Reaching the maximum is not an engine completion edge yet.");
            processor.UpdateCompletionState();
            Assert.Multiple(() =>
            {
                Assert.That(processor.HasCompleted.Value, Is.True);
                Assert.That(processor.MaximumCombo, Is.EqualTo(3));
                Assert.That(processor.IsFullCombo, Is.EqualTo(expected));
                Assert.That(BmsScoreProcessor.GetComboBreakCount(processor.Statistics), Is.Zero,
                    "All MISS has no previous combo to break but must never qualify for the full-combo animation.");
            });
        }

        [Test]
        public void TestEmptyBeatmapNeverQualifiesForFullCombo()
        {
            using var processor = new TestableBmsScoreProcessor();
            var clock = new FramedClock(new ManualClock { CurrentTime = 10000 });
            clock.ProcessFrame();
            processor.Clock = clock;
            processor.ApplyBeatmap(createBeatmap(0));
            processor.UpdateCompletionState();
            Assert.That(processor.HasCompleted.Value, Is.True);
            Assert.That(processor.MaximumCombo, Is.Zero);
            Assert.That(processor.IsFullCombo, Is.False);
        }

        [Test]
        public void TestFullComboEligibilityRevertsAndRebuildsWithTheActualScore()
        {
            var beatmap = createBeatmap(2);
            using var processor = new TestableBmsScoreProcessor { TrackHitEvents = true };
            var clock = new FramedClock(new ManualClock { CurrentTime = 10000 });
            clock.ProcessFrame();
            processor.Clock = clock;
            processor.ApplyBeatmap(beatmap);
            JudgementResult[] results = beatmap.HitObjects.Select(obj => createResult(obj, HitResult.Perfect)).ToArray();
            foreach (JudgementResult result in results)
                processor.ApplyResult(result);
            processor.UpdateCompletionState();
            Assert.That(processor.IsFullCombo, Is.True);

            processor.RevertResult(results[^1]);
            processor.UpdateCompletionState();
            Assert.That(processor.HasCompleted.Value, Is.False);
            Assert.That(processor.IsFullCombo, Is.False);
            processor.ApplyResult(results[^1]);
            processor.UpdateCompletionState();
            Assert.That(processor.IsFullCombo, Is.True);
            processor.ApplyBeatmap(beatmap);
            Assert.That(processor.IsFullCombo, Is.False);
        }
    }
}
