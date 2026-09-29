// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Video;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Visual;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using osuTK;

namespace osu.Game.Rulesets.Bms.Tests
{
    public partial class TestSceneBmsBgaSharedContent : OsuTestScene
    {
        [Resolved]
        private GameHost host { get; set; } = null!;

        public override bool AutomaticallyRunFirstStep => false;

        [Test]
        public void TestFourViewportsShareImageAndVideoPlayback()
        {
            DefaultBmsBgaPanelDisplay panel = null!;
            BgaWorkingBeatmap working = null!;
            var clock = new ManualClock { CurrentTime = 0, IsRunning = true };
            var framedClock = new FramedClock(clock);
            Task? capture = null;

            AddStep("load four-viewport image/video chart", () =>
            {
                var beatmap = new BmsBeatmap { BmsInfo = new BmsBeatmapInfo { Keymode = BmsKeymode.Key14K } };
                working = new BgaWorkingBeatmap(beatmap);
                var layout = new BmsGameplayLayoutProvider(beatmap);
                layout.PublishForTesting(BmsPlayfieldStyle.Center, new BmsGameplayLayoutConfiguration());
                panel = new DefaultBmsBgaPanelDisplay(layout) { Clock = framedClock };
                panel.SetBgaSource(new[]
                {
                    new BmsBgaTimelineEntry(0, BmsBgaLayer.Base, "base.png", false),
                    new BmsBgaTimelineEntry(0, BmsBgaLayer.Poor, "poor.png", false),
                    new BmsBgaTimelineEntry(1000, BmsBgaLayer.Base, "video.mp4", true),
                }, BmsPoorBgaMode.Default);
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[]
                    {
                        (typeof(IBindable<WorkingBeatmap>), new Bindable<WorkingBeatmap>(working)),
                        (typeof(BmsRulesetConfigManager), RulesetConfigs.GetConfigFor(new BmsRuleset())!),
                    },
                    Child = panel,
                };
                framedClock.ProcessFrame();
            });
            AddUntilStep("base image loaded", () => panel?.ChildrenOfType<Sprite>().Any(sprite => sprite.Texture != null) == true);
            AddStep("record image resources", () =>
            {
                TestContext.Progress.WriteLine($"bga mirrors=4 players={panel.ChildrenOfType<BmsBgaPlayer>().Count()} imageReads={working.ImageReads}");
                Assert.That(panel.ChildrenOfType<BmsBgaPlayer>().Count(), Is.EqualTo(1));
                Assert.That(panel.ChildrenOfType<BufferedContainerView<Drawable>>().Count(), Is.EqualTo(3));
                Assert.That(panel.ChildrenOfType<BufferedContainerView<Drawable>>().All(view =>
                    view.DrawSize == panel.ChildrenOfType<BmsBgaPlayer>().Single().DrawSize), Is.True,
                    "Mirrors must retain the source viewport aspect ratio.");
                Assert.That(working.ImageReads, Is.EqualTo(1));
                Assert.That(panel.LayoutSnapshot!.BgaViewports.Count, Is.EqualTo(4));
                assertViewportRevision(panel, 2);
            });
            AddWaitStep("allow mirror draw frames", 3);
            AddStep("verify rendered mirrors on a desktop host", () => capture = verifyMirrorPixels(panel, false));
            AddUntilStep("wait for rendered mirror verification", () => capture?.IsCompleted == true);
            AddStep("propagate mirror drawing errors", () => capture!.GetAwaiter().GetResult());
            AddStep("trigger poor on the shared clock", panelNotifyMiss);
            AddWaitStep("allow poor image and mirror draw frames", 3);
            AddAssert("poor image loaded once", () => working.ImageReads, () => Is.EqualTo(2));
            AddStep("verify poor appears on every mirror", () => capture = verifyMirrorPixels(panel, true));
            AddUntilStep("wait for poor mirror verification", () => capture?.IsCompleted == true);
            AddStep("propagate poor drawing errors", () => capture!.GetAwaiter().GetResult());
            AddStep("advance to video", () =>
            {
                clock.CurrentTime = 1200;
                framedClock.ProcessFrame();
            });
            AddUntilStep("video decoder produces frames", () => panel.ChildrenOfType<Video>().Any()
                                                                              && panel.ChildrenOfType<Video>().All(video => video.FramesProcessed > 0));
            AddStep("record video resources and identical viewport state", () =>
            {
                TestContext.Progress.WriteLine($"bga mirrors=4 videos={panel.ChildrenOfType<Video>().Count()} videoReads={working.VideoReads}");
                Assert.That(panel.ChildrenOfType<Video>().Count(), Is.EqualTo(1));
                Assert.That(working.VideoReads, Is.EqualTo(1));
                assertViewportRevision(panel, 3);
                Assert.That(panel.ChildrenOfType<Video>().All(video => Math.Abs(video.PlaybackPosition - 200) < 1), Is.True);
            });
            AddStep("pause all mirrors", () => { clock.IsRunning = false; framedClock.ProcessFrame(); });
            AddWaitStep("allow pause update", 2);
            AddAssert("video position held", () => panel.ChildrenOfType<Video>().All(video => Math.Abs(video.PlaybackPosition - 200) < 1));
            AddStep("rewind to image", () => { clock.CurrentTime = 100; framedClock.ProcessFrame(); });
            AddWaitStep("allow rewind update", 2);
            AddStep("all mirror revisions rewind together", () => assertViewportRevision(panel, 2));
            BmsBgaPlayer oldPlayer = null!;
            Video oldVideo = null!;
            AddStep("replace the content session", () =>
            {
                oldPlayer = panel.ChildrenOfType<BmsBgaPlayer>().Single();
                oldVideo = panel.ChildrenOfType<Video>().Single();
                panel.SetBgaSource(new[] { new BmsBgaTimelineEntry(0, BmsBgaLayer.Base, "base.png", false) }, BmsPoorBgaMode.Default);
            });
            AddUntilStep("old player and decoder disposed", () => isDisposed(oldPlayer) && isDisposed(oldVideo));
            AddAssert("replacement retains one player", () => panel.ChildrenOfType<BmsBgaPlayer>().Count(), () => Is.EqualTo(1));
            AddStep("detach player resources", () => Clear());

            void panelNotifyMiss() => panel.NotifyMiss();
        }

        private async Task verifyMirrorPixels(DefaultBmsBgaPanelDisplay panel, bool poor)
        {
            if (host is HeadlessGameHost)
                return;

            Vector2[] centres = panel.LayoutSnapshot!.BgaViewports.Select(viewport => panel.ToScreenSpace(new Vector2(
                (viewport.X + viewport.Width / 2) * panel.DrawWidth,
                (viewport.Y + viewport.Height / 2) * panel.DrawHeight))).ToArray();
            using Image<Rgba32>? pixels = await host.TakeScreenshotAsync().ConfigureAwait(false);
            Assert.That(pixels, Is.Not.Null);
            foreach (Vector2 centre in centres)
            {
                Rgba32 pixel = pixels![(int)centre.X, (int)centre.Y];
                if (poor)
                {
                    Assert.That(pixel.R, Is.GreaterThan(150), "Every viewport must switch to the shared red POOR image.");
                    Assert.That(pixel.G, Is.LessThan(100));
                    Assert.That(pixel.B, Is.LessThan(100));
                }
                else
                {
                    Assert.That(pixel.G, Is.GreaterThan(100), "Every viewport must draw the shared cyan image at its own location.");
                    Assert.That(pixel.B, Is.GreaterThan(150));
                    Assert.That(pixel.R, Is.LessThan(70));
                }
            }

            string? path = Environment.GetEnvironmentVariable("OMS_BGA_CAPTURE_PATH");
            if (!string.IsNullOrEmpty(path))
            {
                if (poor)
                    path = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "-poor.png");
                await pixels!.SaveAsPngAsync(path).ConfigureAwait(false);
            }
        }

        private static void assertViewportRevision(DefaultBmsBgaPanelDisplay panel, long expected)
        {
            var source = (IBmsBgaPanelStateSource)panel;
            for (int index = 0; index < 4; index++)
            {
                Assert.That(source.TryGetContentState(index, out _, out long revision), Is.True);
                Assert.That(revision, Is.EqualTo(expected));
            }
        }

        private static bool isDisposed(Drawable drawable)
            => (bool)typeof(Drawable).GetProperty("IsDisposed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(drawable)!;

        private sealed class BgaWorkingBeatmap : TestWorkingBeatmap
        {
            private readonly byte[] image;
            private readonly byte[] poorImage;
            public int ImageReads { get; private set; }
            public int VideoReads { get; private set; }

            public BgaWorkingBeatmap(IBeatmap beatmap)
                : base(beatmap)
            {
                using var pixels = new Image<Rgba32>(64, 64, new Rgba32(30, 180, 220));
                using var stream = new MemoryStream();
                pixels.SaveAsPng(stream);
                image = stream.ToArray();
                using var poorPixels = new Image<Rgba32>(64, 64, new Rgba32(220, 30, 40));
                using var poorStream = new MemoryStream();
                poorPixels.SaveAsPng(poorStream);
                poorImage = poorStream.ToArray();
            }

            public override Stream? GetStream(string storagePath)
            {
                if (storagePath is "base.png" or "poor.png")
                {
                    ImageReads++;
                    return new MemoryStream(storagePath == "poor.png" ? poorImage : image, writable: false);
                }

                if (storagePath == "video.mp4")
                {
                    VideoReads++;
                    return typeof(TestSceneBmsBgaSharedContent).Assembly.GetManifestResourceStream("osu.Game.Rulesets.Bms.Tests.Resources.bga-test-video.mp4");
                }

                return null;
            }
        }
    }
}
