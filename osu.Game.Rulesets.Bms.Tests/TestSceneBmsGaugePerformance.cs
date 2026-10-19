// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    public partial class TestSceneBmsGaugePerformance : OsuTestScene
    {
        [TestCase(BmsKeymode.Key7K)]
        [TestCase(BmsKeymode.Key14K)]
        public void TestStableGaugeUpdatesDoNotFormatNewStrings(BmsKeymode keymode)
        {
            ProbedGauge gauge = null!;
            BmsGaugeProcessor health = null!;
            AddStep("load a real stage-local gauge", () =>
            {
                var beatmap = new BmsBeatmap { BmsInfo = new BmsBeatmapInfo { Keymode = keymode } };
                beatmap.HitObjects.Add(new BmsHitObject { StartTime = 10000, LaneIndex = 1, Keymode = keymode });
                var layout = new BmsGameplayLayoutProvider(beatmap);
                layout.PublishForTesting(BmsPlayfieldStyle.Center, new BmsGameplayLayoutConfiguration());
                health = new BmsGaugeProcessor(0);
                health.ApplyBeatmap(beatmap);
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[]
                    {
                        (typeof(GameplayState), new GameplayState(beatmap, new BmsRuleset())),
                        (typeof(HealthProcessor), health),
                        (typeof(BmsGameplayLayoutProvider), layout),
                    },
                    Child = gauge = new ProbedGauge(),
                };
            });
            AddUntilStep("the gauge is loaded", () => gauge?.IsLoaded == true);
            AddStep("measure unchanged real gauge updates", () =>
            {
                for (int i = 0; i < 256; i++)
                    gauge.Tick();

                const int updates = 65536;
                long start = Stopwatch.GetTimestamp();
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < updates; i++)
                    gauge.Tick();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                double elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                TestContext.Progress.WriteLine($"gauge: keymode={keymode} updates={updates} allocated={allocated} elapsedMs={elapsedMs:F3}");
                Assert.That(allocated, Is.Zero);
            });
            AddStep("retain exact percent formatting after value and culture changes", () =>
            {
                CultureInfo original = CultureInfo.CurrentCulture;
                try
                {
                    foreach (string culture in new[] { "en-US", "fr-FR" })
                    {
                        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                        foreach (double value in new[] { 0.2, 0.205, 0.285, 0.995, 1.0 })
                        {
                            health.Health.Value = value;
                            gauge.Tick();
                            string expected = value.ToString("P0", CultureInfo.CurrentCulture);
                            Assert.That(gauge.ChildrenOfType<OsuSpriteText>().Count(text => text.Text.ToString() == expected),
                                Is.EqualTo(keymode == BmsKeymode.Key14K ? 2 : 1));
                        }
                    }
                }
                finally
                {
                    CultureInfo.CurrentCulture = original;
                }
            });
        }

        private partial class ProbedGauge : BmsGaugeBar
        {
            public void Tick() => Update();
        }
    }
}
