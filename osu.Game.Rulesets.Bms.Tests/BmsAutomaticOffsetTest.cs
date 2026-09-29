// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.IO;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsAutomaticOffsetTest
    {
        [TestCase(14.999, 0)]
        [TestCase(15, 1)]
        [TestCase(-15, -1)]
        [TestCase(44.999, 1)]
        [TestCase(45, 2)]
        [TestCase(-90, -3)]
        [TestCase(150, 5)]
        [TestCase(-150, -5)]
        [TestCase(150.001, 0)]
        public void TestBeatorajaAdjustment(double error, double expected)
            => Assert.That(DrawableBmsRuleset.AdjustVisualOffset(0, HitResult.Good, error), Is.EqualTo(expected));

        [TestCase(HitResult.Meh)]
        [TestCase(HitResult.Miss)]
        [TestCase(HitResult.Ok)]
        [TestCase(HitResult.IgnoreHit)]
        public void TestPoorAndNonTimingResultsDoNotAdjust(HitResult result)
            => Assert.That(DrawableBmsRuleset.AdjustVisualOffset(20, result, 90), Is.EqualTo(20));

        [Test]
        public void TestClampAndDirectionReversal()
        {
            double offset = DrawableBmsRuleset.AdjustVisualOffset(499, HitResult.Great, 150);
            Assert.That(offset, Is.EqualTo(500));
            Assert.That(DrawableBmsRuleset.AdjustVisualOffset(offset, HitResult.Perfect, -30), Is.EqualTo(499));
            Assert.That(DrawableBmsRuleset.AdjustVisualOffset(-499, HitResult.Good, -150), Is.EqualTo(-500));
        }

        [Test]
        public void TestRecordingFlushAndSeekPreserveVisualHistoryAndTotalVersion()
        {
            var timeline = new BmsVisualOffsetTimeline { InitialOffset = 10 };
            var score = new Score();
            var recorder = new BmsReplayRecorder(score, timeline);
            timeline.Record(1000, 12);
            timeline.Record(1000, 14);
            timeline.Record(1100, 13);
            recorder.EndRecording();

            var data = score.ScoreInfo.GetRulesetData<BmsScoreInfoData>()!;
            Assert.That(data.Version, Is.EqualTo(BmsScoreInfoData.TOTAL_RULES_VERSION));
            var saved = data.VisualOffset!;
            saved.Validate();
            Assert.That(saved.Changes.Count, Is.EqualTo(2));
            Assert.That(saved.OffsetAt(1200), Is.EqualTo(13));
            Assert.That(saved.OffsetAt(1000), Is.EqualTo(14));
            Assert.That(saved.OffsetAt(999), Is.EqualTo(10));
            Assert.That(saved.OffsetAt(1200), Is.EqualTo(13));
        }

        [Test]
        public void TestReplacingRecordedFuture()
        {
            var timeline = new BmsVisualOffsetTimeline();
            timeline.Record(1000, 1);
            timeline.Record(2000, 2);
            timeline.Record(1500, -1);
            Assert.That(timeline.OffsetAt(3000), Is.EqualTo(-1));
            Assert.That(timeline.OffsetAt(1200), Is.EqualTo(1));
        }

        [Test]
        public void TestMalformedReplayVisualDataIsRejectedAtBoundary()
        {
            var timeline = new BmsVisualOffsetTimeline { InitialOffset = double.NaN };
            Assert.Throws<InvalidDataException>(timeline.Validate);
            timeline.InitialOffset = 0;
            timeline.Changes.Add(new BmsVisualOffsetTimeline.Change { Time = 1, Offset = 501 });
            Assert.Throws<InvalidDataException>(timeline.Validate);
        }
    }
}
