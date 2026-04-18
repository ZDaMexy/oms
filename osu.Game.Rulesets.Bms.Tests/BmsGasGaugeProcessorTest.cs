// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsGasGaugeProcessorTest
    {
        [Test]
        public void TestDowngradesThroughConfiguredGaugeChainWithoutFailing()
        {
            var beatmap = createBeatmap(total: 200, noteCount: 1000);
            var processor = new BmsGasGaugeProcessor(0, BmsGaugeType.ExHard, BmsGaugeType.Normal);

            processor.ApplyBeatmap(beatmap);

            for (int i = 0; i < 10; i++)
                processor.ApplyResult(createResult(beatmap.HitObjects[0], HitResult.Meh));

            for (int i = 0; i < 20; i++)
                processor.ApplyResult(createResult(beatmap.HitObjects[0], HitResult.Meh));

            Assert.Multiple(() =>
            {
                Assert.That(processor.GaugeType, Is.EqualTo(BmsGaugeType.Normal));
                Assert.That(processor.Health.Value, Is.EqualTo(BmsGaugeProcessor.STARTING_GAUGE).Within(0.000001));
                Assert.That(processor.HasFailed, Is.False);
                Assert.That(processor.ActivatedGaugeTypes, Is.EqualTo(new[] { BmsGaugeType.ExHard, BmsGaugeType.Hard, BmsGaugeType.Normal }));
            });
        }

        [Test]
        public void TestDowngradeDoesNotInvokeFailCallback()
        {
            var beatmap = createBeatmap(total: 200, noteCount: 1000);
            var processor = new BmsGasGaugeProcessor(0, BmsGaugeType.ExHard, BmsGaugeType.Normal);
            int failCount = 0;

            processor.Failed += () =>
            {
                failCount++;
                return true;
            };

            processor.ApplyBeatmap(beatmap);

            for (int i = 0; i < 10; i++)
                processor.ApplyResult(createResult(beatmap.HitObjects[0], HitResult.Meh));

            for (int i = 0; i < 20; i++)
                processor.ApplyResult(createResult(beatmap.HitObjects[0], HitResult.Meh));

            Assert.Multiple(() =>
            {
                Assert.That(processor.GaugeType, Is.EqualTo(BmsGaugeType.Normal));
                Assert.That(processor.HasFailed, Is.False);
                Assert.That(failCount, Is.EqualTo(0));
            });
        }

        [Test]
        public void TestFailsWhenConfiguredFloorGaugeDepletes()
        {
            var beatmap = createBeatmap(total: 200, noteCount: 1000);
            var processor = new BmsGasGaugeProcessor(0, BmsGaugeType.ExHard, BmsGaugeType.Hard);

            processor.ApplyBeatmap(beatmap);

            for (int i = 0; i < 10; i++)
                processor.ApplyResult(createResult(beatmap.HitObjects[0], HitResult.Meh));

            for (int i = 0; i < 20; i++)
                processor.ApplyResult(createResult(beatmap.HitObjects[0], HitResult.Meh));

            Assert.Multiple(() =>
            {
                Assert.That(processor.GaugeType, Is.EqualTo(BmsGaugeType.Hard));
                Assert.That(processor.Health.Value, Is.EqualTo(0).Within(0.000001));
                Assert.That(processor.HasFailed, Is.True);
            });
        }

        [Test]
        public void TestFloorFailureInvokesFailCallbackOnce()
        {
            var beatmap = createBeatmap(total: 200, noteCount: 1000);
            var processor = new BmsGasGaugeProcessor(0, BmsGaugeType.ExHard, BmsGaugeType.Hard);
            int failCount = 0;

            processor.Failed += () =>
            {
                failCount++;
                return true;
            };

            processor.ApplyBeatmap(beatmap);

            for (int i = 0; i < 10; i++)
                processor.ApplyResult(createResult(beatmap.HitObjects[0], HitResult.Meh));

            for (int i = 0; i < 20; i++)
                processor.ApplyResult(createResult(beatmap.HitObjects[0], HitResult.Meh));

            Assert.Multiple(() =>
            {
                Assert.That(processor.GaugeType, Is.EqualTo(BmsGaugeType.Hard));
                Assert.That(processor.HasFailed, Is.True);
                Assert.That(failCount, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestHazardGoodDoesNotTriggerDowngrade()
        {
            var beatmap = createBeatmap(total: 200, noteCount: 1000);
            var processor = new BmsGasGaugeProcessor(0, BmsGaugeType.Hazard, BmsGaugeType.Easy);

            processor.ApplyBeatmap(beatmap);
            processor.Health.Value = 0.5;
            processor.ApplyResult(createResult(beatmap.HitObjects[0], HitResult.Good));

            Assert.Multiple(() =>
            {
                Assert.That(processor.GaugeType, Is.EqualTo(BmsGaugeType.Hazard));
                Assert.That(processor.Health.Value, Is.EqualTo(0.5).Within(0.000001));
                Assert.That(processor.HasFailed, Is.False);
            });
        }

        [Test]
        public void TestHazardEmptyPoorDowngradesToNextGauge()
        {
            var beatmap = createBeatmap(total: 200, noteCount: 1000);
            var processor = new BmsGasGaugeProcessor(0, BmsGaugeType.Hazard, BmsGaugeType.ExHard);
            var emptyPoor = new BmsEmptyPoorHitObject
            {
                StartTime = 1000,
            };

            processor.ApplyBeatmap(beatmap);
            processor.ApplyResult(createResult(emptyPoor, HitResult.Ok));

            Assert.Multiple(() =>
            {
                Assert.That(processor.GaugeType, Is.EqualTo(BmsGaugeType.ExHard));
                Assert.That(processor.Health.Value, Is.EqualTo(1).Within(0.000001));
                Assert.That(processor.HasFailed, Is.False);
            });
        }

        private static BmsBeatmap createBeatmap(double total, int noteCount)
        {
            var beatmap = new BmsBeatmap
            {
                BmsInfo = new BmsBeatmapInfo
                {
                    Total = total,
                }
            };

            for (int i = 0; i < noteCount; i++)
            {
                beatmap.HitObjects.Add(new BmsHitObject
                {
                    StartTime = i,
                    LaneIndex = 1,
                });
            }

            return beatmap;
        }

        private static JudgementResult createResult(osu.Game.Rulesets.Objects.HitObject hitObject, HitResult hitResult)
            => new JudgementResult(hitObject, hitObject.CreateJudgement())
            {
                Type = hitResult,
            };
    }
}
