// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        [Test]
        public void TestAutomaticHistoryIsIndependentOfManualOffsetsAndCoalescesActualModeChanges()
        {
            var timeline = new BmsVisualOffsetTimeline
            {
                InitialOffset = 0,
                Automatic = new BmsVisualOffsetTimeline.AutomaticState { Initial = false },
            };
            timeline.Record(1000, 20);
            timeline.Automatic.Record(900, true);
            timeline.Automatic.Record(1000, false);
            timeline.Automatic.Record(1000, true);
            timeline.Automatic.Record(1500, false);
            timeline.Validate();
            var saved = JsonConvert.DeserializeObject<BmsVisualOffsetTimeline>(JsonConvert.SerializeObject(timeline))!;
            Assert.Multiple(() =>
            {
                Assert.That(saved.Changes, Has.Count.EqualTo(1));
                Assert.That(saved.OffsetAt(999), Is.Zero);
                Assert.That(saved.OffsetAt(1000), Is.EqualTo(20));
                Assert.That(saved.Automatic!.Changes, Has.Count.EqualTo(2));
                Assert.That(saved.Automatic.At(899), Is.False);
                Assert.That(saved.Automatic.At(900), Is.True);
                Assert.That(saved.Automatic.At(1499), Is.True);
                Assert.That(saved.Automatic.At(1500), Is.False);
                Assert.That(saved.Automatic.At(800), Is.False);
            });
            saved.Automatic!.Record(1000, false);
            Assert.That(saved.Automatic.At(1400), Is.False);
            Assert.That(saved.Automatic.Changes, Has.Count.EqualTo(2));
            Assert.That(saved.Changes, Has.Count.EqualTo(1));
        }

        [Test]
        public void TestLegacyVisualHistoryHasNoInventedAutomaticMode()
        {
            const string json = "{\"initial\":0,\"changes\":[{\"time\":1000,\"offset\":20}]}";
            var legacy = JsonConvert.DeserializeObject<BmsVisualOffsetTimeline>(json)!;
            legacy.Validate();
            Assert.That(legacy.Automatic, Is.Null);
            Assert.That(legacy.OffsetAt(1000), Is.EqualTo(20));
            Assert.That(JObject.Parse(JsonConvert.SerializeObject(legacy)).Property("automatic"), Is.Null);
        }

        [Test]
        public void TestAutomaticModeCanRemainOnWithoutAnyNumericOffsetChanges()
        {
            var timeline = new BmsVisualOffsetTimeline
            {
                InitialOffset = 0,
                Automatic = new BmsVisualOffsetTimeline.AutomaticState { Initial = true },
            };
            timeline.Validate();
            Assert.That(timeline.Changes, Is.Empty);
            Assert.That(timeline.Automatic.At(2000), Is.True);
            Assert.That(timeline.Automatic.Changes, Is.Empty);
        }

        [TestCase("{\"enabled\":true}")]
        [TestCase("{\"time\":null,\"enabled\":true}")]
        public void TestAutomaticModeChangeRequiresItsActualRecordedTime(string change)
        {
            string json = "{\"initial\":0,\"changes\":[],\"automatic\":{\"initial\":false,\"changes\":[" + change + "]}}";
            Assert.That(() => JsonConvert.DeserializeObject<BmsVisualOffsetTimeline>(json), Throws.TypeOf<JsonSerializationException>());
        }

        [TestCase("missing initial")]
        [TestCase("missing changes")]
        [TestCase("missing enabled")]
        [TestCase("duplicate time")]
        [TestCase("nonfinite time")]
        public void TestMalformedAutomaticStateCannotBeInterpretedAsOff(string invalidity)
        {
            var automatic = new BmsVisualOffsetTimeline.AutomaticState { Initial = false };
            automatic.Changes.Add(new BmsVisualOffsetTimeline.AutomaticChange { Time = 1000, Enabled = true });
            switch (invalidity)
            {
                case "missing initial": automatic.Initial = null; break;
                case "missing changes": automatic.Changes = null!; break;
                case "missing enabled": automatic.Changes[0].Enabled = null; break;
                case "duplicate time": automatic.Changes.Add(new BmsVisualOffsetTimeline.AutomaticChange { Time = 1000, Enabled = false }); break;
                case "nonfinite time": automatic.Changes[0].Time = double.NaN; break;
            }
            var timeline = new BmsVisualOffsetTimeline { Automatic = automatic };
            Assert.That(timeline.Validate, Throws.TypeOf<InvalidDataException>());
        }
    }
}
