// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Video;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Visual;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using osuTK;

namespace osu.Game.Rulesets.Bms.Tests
{
    public partial class TestSceneBmsBgaSharedContent
    {
        [Test]
        public void TestGameplaySessionSurvivesDisplaySkinInvalidationAndWindowReplacement()
        {
            var clock = new ManualClock { CurrentTime = 0, IsRunning = true };
            var framedClock = new FramedClock(clock);
            BmsBeatmap beatmap = null!;
            BgaWorkingBeatmap working = null!;
            BmsBgaPlaybackSession session = null!;
            BmsBgaPlayer player = null!;
            BmsBgaPanel panel = null!;
            RefreshableBgaSkinProvider displayHost = null!;
            Drawable oldDisplay = null!;
            Video video = null!;
            long redraws = 0;
            Task? capture = null;

            AddStep("mount gameplay-owned content and skinnable views", () =>
            {
                beatmap = new BmsBeatmap { BmsInfo = new BmsBeatmapInfo { Keymode = BmsKeymode.Key14K } };
                working = new BgaWorkingBeatmap(beatmap);
                session = new BmsBgaPlaybackSession(new[]
                {
                    new BmsBgaTimelineEntry(0, BmsBgaLayer.Base, "base.png", false),
                    new BmsBgaTimelineEntry(0, BmsBgaLayer.Poor, "poor.png", false),
                    new BmsBgaTimelineEntry(1000, BmsBgaLayer.Base, "video.mp4", true),
                }, BmsPoorBgaMode.Default);
                player = session.Player!;
                panel = new BmsBgaPanel(session, createSessionLayout(beatmap));
                displayHost = new RefreshableBgaSkinProvider { RelativeSizeAxes = Axes.Both, Child = panel };
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Clock = framedClock,
                    CachedDependencies = new (Type, object)[]
                    {
                        (typeof(IBindable<WorkingBeatmap>), new Bindable<WorkingBeatmap>(working)),
                        (typeof(BmsRulesetConfigManager), RulesetConfigs.GetConfigFor(new BmsRuleset())!),
                    },
                    Children = new Drawable[] { session, displayHost },
                };
                framedClock.ProcessFrame();
            });
            AddUntilStep("four views and one source loaded", () => panel.Drawable is DefaultBmsBgaPanelDisplay { IsLoaded: true }
                                                                               && working.ImageReads == 1
                                                                               && panel.ChildrenOfType<BufferedContainerView<Drawable>>().Count() == 4);
            AddAssert("display cannot own the player", () => !panel.ChildrenOfType<BmsBgaPlayer>().Any());
            AddStep("trigger POOR and invalidate only the skin display", () =>
            {
                panel.NotifyMiss();
                oldDisplay = panel.Drawable!;
                displayHost.Refresh();
            });
            AddUntilStep("replacement display ready and old display released", () => panel.Drawable is { IsLoaded: true }
                                                                                       && !ReferenceEquals(panel.Drawable, oldDisplay)
                                                                                       && isDisposed(oldDisplay));
            AddWaitStep("draw replacement POOR views", 3);
            AddStep("POOR survived presentation replacement", () => capture = verifyMirrorPixels((DefaultBmsBgaPanelDisplay)panel.Drawable!, true));
            AddUntilStep("POOR pixels checked", () => capture?.IsCompleted == true);
            AddStep("propagate POOR pixel result", () => capture!.GetAwaiter().GetResult());
            AddAssert("same player and cached images retained", () => ReferenceEquals(session.Player, player) && !isDisposed(player) && working.ImageReads == 2);
            AddStep("advance to decoded video", () => { clock.CurrentTime = 1200; framedClock.ProcessFrame(); });
            AddUntilStep("video produces frames", () => session.ChildrenOfType<Video>().Any(v => v.FramesProcessed > 0));
            AddStep("retain exact video and pause", () =>
            {
                video = session.ChildrenOfType<Video>().Single();
                clock.IsRunning = false;
                framedClock.ProcessFrame();
                oldDisplay = panel.Drawable!;
                displayHost.Refresh();
            });
            AddUntilStep("paused skin display rebuilt", () => panel.Drawable is { IsLoaded: true } && !ReferenceEquals(panel.Drawable, oldDisplay));
            AddAssert("paused decoder and its position retained", () =>
                ReferenceEquals(session.ChildrenOfType<Video>().Single(), video) && Math.Abs(video.PlaybackPosition - 200) < 1 && working.VideoReads == 1);
            AddStep("replace four windows with one authored window", () => replaceWindows("0.01,0.10,0.10,0.20,fit"));
            AddUntilStep("one new window mounted", () => panel.Drawable is { IsLoaded: true } && panel.ChildrenOfType<BufferedContainerView<Drawable>>().Count() == 1);
            AddAssert("window count did not replace content", () => ReferenceEquals(session.Player, player) && ReferenceEquals(session.ChildrenOfType<Video>().Single(), video));
            AddStep("hide presentation", () => { panel.Alpha = 0; session.PresentationEnabled = false; });
            AddWaitStep("allow hidden presentation boundary", 2);
            AddStep("record hidden redraw count and advance clock", () =>
            {
                redraws = session.SurfaceRedrawCount;
                clock.IsRunning = true;
                clock.CurrentTime = 1300;
                framedClock.ProcessFrame();
            });
            AddWaitStep("advance hidden media", 3);
            AddAssert("hidden surface is not redrawn", () => session.SurfaceRedrawCount == redraws);
            AddAssert("hidden content still follows gameplay time", () => Math.Abs(video.PlaybackPosition - 300) < 1);
            AddStep("show the same source again", () => { panel.Alpha = 1; session.PresentationEnabled = true; });
            AddUntilStep("surface redraw resumes", () => session.SurfaceRedrawCount > redraws);
            AddStep("author no BGA windows", () => replaceWindows("none"));
            AddUntilStep("empty display loaded", () => panel.Drawable is { IsLoaded: true } && !panel.ChildrenOfType<BufferedContainerView<Drawable>>().Any());
            AddWaitStep("retire old views", 3);
            AddStep("record no-window redraw count", () => redraws = session.SurfaceRedrawCount);
            AddWaitStep("no-window frames", 3);
            AddAssert("no windows need no surface redraw", () => session.SurfaceRedrawCount == redraws);
            AddAssert("no window does not dispose player", () => !isDisposed(player) && !isDisposed(video) && working.VideoReads == 1);
            AddStep("restore default windows", () => replaceWindows(null));
            AddUntilStep("four restored views ready", () => panel.Drawable is { IsLoaded: true } && panel.ChildrenOfType<BufferedContainerView<Drawable>>().Count() == 4);
            AddAssert("restored windows reuse the decoder", () => ReferenceEquals(session.ChildrenOfType<Video>().Single(), video) && working.VideoReads == 1);
            AddStep("exit gameplay owner", () => Clear());
            AddUntilStep("gameplay source and media released", () => isDisposed(session) && isDisposed(player) && isDisposed(video));

            void replaceWindows(string? declaration)
            {
                panel = new BmsBgaPanel(session, createSessionLayout(beatmap, declaration));
                displayHost.Child = panel;
            }
        }

        [Test]
        public void TestStandaloneLegacyDisplayReceivesSourceAfterEagerLoad()
        {
            BgaWorkingBeatmap working = null!;
            BmsBgaPanel panel = null!;

            AddStep("mount a legacy default display without a gameplay session", () =>
            {
                var beatmap = new BmsBeatmap { BmsInfo = new BmsBeatmapInfo { Keymode = BmsKeymode.Key7K } };
                working = new BgaWorkingBeatmap(beatmap);
                panel = new BmsBgaPanel(new[] { new BmsBgaTimelineEntry(0, BmsBgaLayer.Base, "base.png", false) },
                    BmsPoorBgaMode.Default, createSessionLayout(beatmap));
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[]
                    {
                        (typeof(IBindable<WorkingBeatmap>), new Bindable<WorkingBeatmap>(working)),
                        (typeof(BmsRulesetConfigManager), RulesetConfigs.GetConfigFor(new BmsRuleset())!),
                    },
                    Child = new RefreshableBgaSkinProvider { RelativeSizeAxes = Axes.Both, Child = panel },
                };
            });
            AddUntilStep("legacy preview receives and plays its supplied timeline", () => panel.Drawable.IsLoaded && working.ImageReads == 1);
            AddAssert("standalone preview owns one local player", () => panel.ChildrenOfType<BmsBgaPlayer>().Count() == 1);
            AddStep("release legacy preview", () => Clear());
        }

        [Test]
        public void TestAuthoredViewModesProjectTheWholeComposition()
        {
            BmsBgaPlaybackSession session = null!;
            DefaultBmsBgaPanelDisplay display = null!;
            GridBgaWorkingBeatmap working = null!;
            Task? capture = null;

            AddStep("mount three wide windows with separate scale modes", () =>
            {
                var beatmap = new BmsBeatmap { BmsInfo = new BmsBeatmapInfo { Keymode = BmsKeymode.Key7K } };
                var layout = createSessionLayout(beatmap, "0.02,0.05,0.20,0.08,fit;0.02,0.25,0.20,0.08,fill;0.02,0.45,0.20,0.08,stretch", BmsPlayfieldStyle.P2);
                working = new GridBgaWorkingBeatmap(beatmap);
                session = new BmsBgaPlaybackSession(new[] { new BmsBgaTimelineEntry(0, BmsBgaLayer.Base, "grid.png", false) }, BmsPoorBgaMode.Default);
                display = new DefaultBmsBgaPanelDisplay(layout, null, session);
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[]
                    {
                        (typeof(IBindable<WorkingBeatmap>), new Bindable<WorkingBeatmap>(working)),
                        (typeof(BmsRulesetConfigManager), RulesetConfigs.GetConfigFor(new BmsRuleset())!),
                    },
                    Children = new Drawable[] { session, display },
                };
            });
            AddUntilStep("grid and all projections loaded", () => display.IsLoaded && working.Reads == 1
                                                                                  && display.ChildrenOfType<BufferedContainerView<Drawable>>().Count() == 3);
            AddWaitStep("draw author projections", 4);
            AddStep("check projection geometry and resource ownership", () =>
            {
                var views = display.ChildrenOfType<BufferedContainerView<Drawable>>().ToArray();
                Assert.That(session.ChildrenOfType<BmsBgaPlayer>().Count(), Is.EqualTo(1));
                Assert.That(display.ChildrenOfType<BmsBgaPlayer>(), Is.Empty);
                Assert.That(working.Reads, Is.EqualTo(1));
                Assert.That(views[0].DrawWidth, Is.LessThan(views[0].Parent!.DrawWidth));
                Assert.That(views[1].DrawHeight, Is.GreaterThan(views[1].Parent!.DrawHeight));
                Assert.That(views[2].DrawSize, Is.EqualTo(views[2].Parent!.DrawSize));
                Assert.That(session.Player!.DrawWidth / session.Player.DrawHeight, Is.EqualTo(4f / 3f).Within(0.001));
            });
            AddStep("check edges on the desktop", () => capture = verifyProjectionPixels(display));
            AddUntilStep("edge pixels checked", () => capture?.IsCompleted == true);
            AddStep("propagate projection pixel result", () => capture!.GetAwaiter().GetResult());
            AddStep("release projection scene", () => Clear());
        }

        private async Task verifyProjectionPixels(DefaultBmsBgaPanelDisplay display)
        {
            if (host is HeadlessGameHost)
                return;

            var points = display.LayoutSnapshot!.BgaViewports.Select(viewport => new[]
            {
                display.ToScreenSpace(new Vector2((viewport.X + viewport.Width * 0.08f) * display.DrawWidth, (viewport.Y + viewport.Height * 0.5f) * display.DrawHeight)),
                display.ToScreenSpace(new Vector2((viewport.X + viewport.Width * 0.5f) * display.DrawWidth, (viewport.Y + viewport.Height * 0.08f) * display.DrawHeight)),
            }).ToArray();
            using Image<Rgba32>? pixels = await host.TakeScreenshotAsync().ConfigureAwait(false);
            Assert.That(pixels, Is.Not.Null);
            Rgba32 sample(int mode, int point) => pixels![(int)points[mode][point].X, (int)points[mode][point].Y];

            Assert.Multiple(() =>
            {
                Assert.That(sample(0, 0).R, Is.LessThan(40), "Fit keeps black bars beside the 4:3 composition.");
                Assert.That(sample(0, 1).G, Is.GreaterThan(180), "Fit keeps the complete top edge.");
                Assert.That(sample(1, 0).R, Is.GreaterThan(180), "Fill reaches the left edge.");
                Assert.That(sample(1, 1).G, Is.LessThan(100), "Fill crops the composition's top edge.");
                Assert.That(sample(2, 0).R, Is.GreaterThan(180), "Stretch reaches the left edge.");
                Assert.That(sample(2, 1).G, Is.GreaterThan(180), "Stretch preserves the top edge while changing its proportions.");
            });

            string? path = Environment.GetEnvironmentVariable("OMS_BGA_CAPTURE_PATH");
            if (!string.IsNullOrEmpty(path))
                await pixels!.SaveAsPngAsync(Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "-projections.png")).ConfigureAwait(false);
        }

        private static BmsGameplayLayoutProvider createSessionLayout(BmsBeatmap beatmap, string? declaration = null, BmsPlayfieldStyle style = BmsPlayfieldStyle.Center)
        {
            GameplaySkinBgaLayout? authored = null;
            if (declaration != null)
                Assert.That(GameplaySkinBgaLayout.TryParse(declaration, out authored, out _), Is.True);
            var layout = new BmsGameplayLayoutProvider(beatmap);
            layout.PublishForTesting(style, new BmsGameplayLayoutConfiguration { BgaLayout = authored });
            return layout;
        }

        private partial class RefreshableBgaSkinProvider : SkinProvidingContainer
        {
            public RefreshableBgaSkinProvider()
                : base(new BgaSessionTestSkin())
            {
            }

            public void Refresh() => TriggerSourceChanged();
        }

        private sealed class BgaSessionTestSkin : Skin
        {
            public BgaSessionTestSkin()
                : base(new SkinInfo(name: nameof(BgaSessionTestSkin)), null)
            {
            }

            // This is the same legacy fallback shape returned by BmsSkinTransformer. Gameplay must create its
            // session-backed presentation directly instead of mounting this display's standalone preview source.
            public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
                => lookup is BmsSkinComponentLookup { Component: BmsSkinComponents.BgaPanel } ? new DefaultBmsBgaPanelDisplay() : null;

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public override ISample? GetSample(ISampleInfo sampleInfo) => null;

            public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup) => null;
        }

        private sealed class GridBgaWorkingBeatmap : TestWorkingBeatmap
        {
            private readonly byte[] grid;
            public int Reads { get; private set; }

            public GridBgaWorkingBeatmap(IBeatmap beatmap)
                : base(beatmap)
            {
                using var pixels = new Image<Rgba32>(120, 90, new Rgba32(50, 50, 50));
                for (int y = 0; y < pixels.Height; y++)
                {
                    for (int x = 0; x < pixels.Width; x++)
                    {
                        if (x < 12 || x >= 108)
                            pixels[x, y] = new Rgba32(230, 20, 20);
                        if (y < 9 || y >= 81)
                            pixels[x, y] = new Rgba32(20, 230, 20);
                    }
                }

                using var stream = new MemoryStream();
                pixels.SaveAsPng(stream);
                grid = stream.ToArray();
            }

            public override Stream? GetStream(string storagePath)
            {
                if (storagePath != "grid.png")
                    return null;
                Reads++;
                return new MemoryStream(grid, writable: false);
            }
        }
    }
}
