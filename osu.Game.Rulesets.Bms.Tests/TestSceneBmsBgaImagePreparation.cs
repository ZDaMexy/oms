// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Visual;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests
{
    public partial class TestSceneBmsBgaImagePreparation : OsuTestScene
    {
        [TestCase(BmsPoorBgaMode.Default)]
        [TestCase(BmsPoorBgaMode.Undisplayed)]
        public void TestFutureImagesArePreparedBeforeGameplayWithoutUpdateThreadReads(BmsPoorBgaMode poorMode)
        {
            var clock = new ManualClock { IsRunning = true };
            var framed = new FramedClock(clock);
            BmsBgaPlayer player = null!;
            ImageWorkingBeatmap working = null!;
            int futureReadsAfterLoad = 0;
            int poorReadsAfterLoad = 0;

            AddStep("load future BGA images through the real asynchronous drawable loader", () =>
            {
                working = new ImageWorkingBeatmap(new BmsBeatmap());
                var holder = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Clock = framed,
                    CachedDependencies = new (Type, object)[]
                    {
                        (typeof(IBindable<WorkingBeatmap>), new Bindable<WorkingBeatmap>(working)),
                        (typeof(BmsRulesetConfigManager), RulesetConfigs.GetConfigFor(new BmsRuleset())!),
                    },
                    Child = player = new BmsBgaPlayer(new[]
                    {
                        new BmsBgaTimelineEntry(10000, BmsBgaLayer.Base, "superseded.png", false),
                        new BmsBgaTimelineEntry(10000, BmsBgaLayer.Base, "future.png", false),
                        new BmsBgaTimelineEntry(10000, BmsBgaLayer.Poor, "poor.png", false),
                        new BmsBgaTimelineEntry(20000, BmsBgaLayer.Layer, "future.png", false),
                        new BmsBgaTimelineEntry(25000, BmsBgaLayer.Layer2, "missing.png", false),
                    }, poorMode),
                };
                LoadComponentAsync(holder, loaded => Child = loaded);
            });
            AddUntilStep("the complete media player is loaded", () => player?.IsLoaded == true && player.Parent != null);
            AddStep("retain the pre-playback resource counts", () =>
            {
                futureReadsAfterLoad = working.Reads("future.png");
                poorReadsAfterLoad = working.Reads("poor.png");
                clock.CurrentTime = 16000;
                framed.ProcessFrame();
            });
            AddUntilStep("the real future bitmap is displayed", () => player.ChildrenOfType<Sprite>().Any(sprite => sprite.Texture != null));
            AddStep("show the optional poor image", () => player.NotifyMiss());
            AddWaitStep("allow the optional POOR layer to become visible", 2);
            AddStep("advance to the repeated bitmap", () =>
            {
                clock.CurrentTime = 21000;
                framed.ProcessFrame();
            });
            AddWaitStep("allow the real layer updates", 2);
            AddStep("advance through a missing optional resource", () =>
            {
                clock.CurrentTime = 26000;
                framed.ProcessFrame();
            });
            AddWaitStep("allow the unavailable layer to settle", 2);
            AddStep("verify preparation, reuse and the update-thread boundary", () =>
            {
                TestContext.Progress.WriteLine($"bga-image: futureAtLoad={futureReadsAfterLoad} poorAtLoad={poorReadsAfterLoad} updateReads={working.UpdateThreadReads}");
                Assert.Multiple(() =>
                {
                    Assert.That(futureReadsAfterLoad, Is.EqualTo(1), "A late bitmap must already be decoded before gameplay starts.");
                    Assert.That(poorReadsAfterLoad, Is.EqualTo(poorMode == BmsPoorBgaMode.Undisplayed ? 0 : 1));
                    Assert.That(working.Reads("future.png"), Is.EqualTo(1), "Layers share one preparation of the same bitmap.");
                    Assert.That(working.Reads("superseded.png"), Is.Zero, "An entry replaced at the exact same layer/time is never visible and needs no decode.");
                    Assert.That(working.UpdateThreadReads, Is.Zero, "Changing the active BGA must never synchronously read or decode an image on Update.");
                });
            });
        }

        private sealed class ImageWorkingBeatmap : TestWorkingBeatmap
        {
            private readonly byte[] png;
            private readonly ConcurrentDictionary<string, int> reads = new ConcurrentDictionary<string, int>();
            private int updateThreadReads;

            public int UpdateThreadReads => Volatile.Read(ref updateThreadReads);

            public ImageWorkingBeatmap(IBeatmap beatmap)
                : base(beatmap)
            {
                using var image = new Image<Rgba32>(16, 16, new Rgba32(30, 180, 220));
                using var stream = new MemoryStream();
                image.SaveAsPng(stream);
                png = stream.ToArray();
            }

            public int Reads(string name) => reads.TryGetValue(name, out int count) ? count : 0;

            public override Stream? GetStream(string storagePath)
            {
                if (ThreadSafety.IsUpdateThread)
                    Interlocked.Increment(ref updateThreadReads);
                reads.AddOrUpdate(storagePath, 1, (_, count) => count + 1);
                return storagePath is "future.png" or "poor.png" ? new MemoryStream(png, writable: false) : null;
            }
        }
    }
}
