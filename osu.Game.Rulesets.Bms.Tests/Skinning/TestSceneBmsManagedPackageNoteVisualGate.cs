// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.Tests.Skinning.ManualGate;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using osu.Game.Tests;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    /// <summary>
    /// Exact-runner-only visual gate for the real managed-package BMS ordinary-note path.
    /// Ordinary desktop TestBrowser discovery is retained, but loading fails closed before any gate step can mutate skin state.
    /// </summary>
    [TestFixture]
    public partial class TestSceneBmsManagedPackageNoteVisualGate : OsuTestScene
    {
        private const int cycle_count = 3;
        private const double phase_dwell_duration = 3000;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        private Task<Live<SkinInfo>> goodImport = null!;
        private Task<Live<SkinInfo>> brokenImport = null!;
        private Task<bool>? goodDelete;
        private Task<bool>? brokenDelete;
        private NonDeletingImportTask goodImportTask = null!;
        private NonDeletingImportTask brokenImportTask = null!;
        private Live<SkinInfo> goodSkin = null!;
        private Live<SkinInfo> brokenSkin = null!;
        private Live<SkinInfo>? originalSkin;
        private MemoryStream? goodArchive;
        private MemoryStream? brokenArchive;
        private Guid goodSkinId;
        private Guid brokenSkinId;

        private Container? noteMount;
        private BmsAsyncNoteDrawable noteHost = null!;
        private Box statusBackground = null!;
        private OsuSpriteText statusText = null!;
        private double phaseDwellEnd;
        private bool cleanupCompleted = true;

        protected override bool UseFreshStoragePerRun => true;

        public override bool AutomaticallyRunFirstStep => false;

        [BackgroundDependencyLoader(permitNulls: true)]
        private void validateExactIsolation(GameHost host, ExactVisualTestIsolation? exactIsolation)
        {
            if (!IsExecutionIsolated(host is HeadlessGameHost, exactIsolation != null))
                throw new InvalidOperationException("This state-mutating visual gate may only run in the isolated exact-test game.");
        }

        internal static bool IsExecutionIsolated(bool isHeadlessHost, bool hasExactIsolationMarker)
            => isHeadlessHost || hasExactIsolationMarker;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("reset visual gate state", prepareGateRun);
        }

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("detach live production note host", detachProductionNoteHost);
            AddStep("restore the originally selected skin", () =>
            {
                if (originalSkin != null)
                    skinManager.CurrentSkinInfo.Value = originalSkin;
            });
            AddUntilStep("wait for the original current pair", () =>
                originalSkin == null || selectedSkinIs(originalSkin));
            AddStep("delete the good gate import through SkinManager", () =>
                goodDelete = goodSkinId == Guid.Empty
                    ? Task.FromResult(true)
                    : skinManager.DeleteSkinAsync(goodSkinId));
            AddUntilStep("wait for the good gate import deletion", () => goodDelete?.IsCompleted == true);
            AddStep("assert the good gate import deletion", () =>
            {
                Assert.That(goodDelete!.GetAwaiter().GetResult(), Is.True);
                goodSkinId = Guid.Empty;
            });
            AddStep("delete the broken gate import through SkinManager", () =>
                brokenDelete = brokenSkinId == Guid.Empty
                    ? Task.FromResult(true)
                    : skinManager.DeleteSkinAsync(brokenSkinId));
            AddUntilStep("wait for the broken gate import deletion", () => brokenDelete?.IsCompleted == true);
            AddStep("assert the broken gate import deletion", () =>
            {
                Assert.That(brokenDelete!.GetAwaiter().GetResult(), Is.True);
                brokenSkinId = Guid.Empty;
            });
            AddStep("dispose visual gate resources", cleanupGateState);
        }

        [Test]
        public void TestAutomaticGoodAndBrokenPackageCycle()
        {
            AddStep("remember original selected skin", () => originalSkin = skinManager.CurrentSkinInfo.Value);
            AddStep("build production note display", buildDisplay);

            AddStep("import generated good package", () =>
            {
                goodArchive = new MemoryStream(BmsNoteAnimationManualGateGenerator.CreateGoodPackage(), writable: false);
                goodImportTask = new NonDeletingImportTask(
                    goodArchive,
                    "bms-note-animation-visual-gate-good.osk");
                goodImport = skinManager.Import(goodImportTask);
            });
            AddUntilStep("good package imported", () => goodImport?.IsCompleted == true);
            AddUntilStep("good source cleanup safely intercepted", () => goodImportTask?.DeleteRequested == true);
            AddStep("capture good package", () =>
            {
                goodSkin = goodImport.GetAwaiter().GetResult();
                goodSkinId = goodSkin.ID;
            });

            AddStep("import generated broken package", () =>
            {
                brokenArchive = new MemoryStream(BmsNoteAnimationManualGateGenerator.CreateBrokenPackage(), writable: false);
                brokenImportTask = new NonDeletingImportTask(
                    brokenArchive,
                    "bms-note-animation-visual-gate-broken.osk");
                brokenImport = skinManager.Import(brokenImportTask);
            });
            AddUntilStep("broken package imported", () => brokenImport?.IsCompleted == true);
            AddUntilStep("broken source cleanup safely intercepted", () => brokenImportTask?.DeleteRequested == true);
            AddStep("capture broken package", () =>
            {
                brokenSkin = brokenImport.GetAwaiter().GetResult();
                brokenSkinId = brokenSkin.ID;
            });

            for (int cycle = 1; cycle <= cycle_count; cycle++)
            {
                queueGoodPhase(cycle);
                queueBrokenPhase(cycle);
            }

            AddStep("mark three-cycle visual gate passed", () =>
            {
                statusBackground.Colour = new Color4(12, 132, 72, 255);
                statusText.Text = "PASS · 3 轮 GOOD 动画 / BROKEN 回落均已完成 · 可关闭窗口";
            });
        }

        private void cleanupGateState()
        {
            if (cleanupCompleted)
                return;

            var failures = new List<Exception>();

            try
            {
                attemptCleanup("detach the live production note host", detachProductionNoteHost);
            }
            finally
            {
                attemptCleanup("dispose the good-package archive", () =>
                {
                    goodArchive?.Dispose();
                    goodArchive = null;
                });
                attemptCleanup("dispose the broken-package archive", () =>
                {
                    brokenArchive?.Dispose();
                    brokenArchive = null;
                });
            }

            if (failures.Count > 0)
                throw new AggregateException("Failed to completely clean up the BMS note-animation visual gate state.", failures);

            cleanupCompleted = true;
            originalSkin = null;
            goodImport = null!;
            brokenImport = null!;
            goodDelete = null;
            brokenDelete = null;
            goodImportTask = null!;
            brokenImportTask = null!;
            goodSkin = null!;
            brokenSkin = null!;
            noteMount = null;
            goodSkinId = Guid.Empty;
            brokenSkinId = Guid.Empty;

            void attemptCleanup(string operation, Action action)
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failures.Add(new InvalidOperationException($"Failed to {operation}.", exception));
                }
            }
        }

        private void prepareGateRun()
        {
            cleanupGateState();

            originalSkin = null;
            goodImport = null!;
            brokenImport = null!;
            goodImportTask = null!;
            brokenImportTask = null!;
            goodSkin = null!;
            brokenSkin = null!;
            goodDelete = null;
            brokenDelete = null;
            goodArchive = null;
            brokenArchive = null;
            goodSkinId = Guid.Empty;
            brokenSkinId = Guid.Empty;
            phaseDwellEnd = 0;
            cleanupCompleted = false;
        }

        private void buildDisplay()
        {
            Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(7, 12, 22, 255),
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Width = 0.9f,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 18),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Text = "BMS managed .osk · ordinary-note visual gate",
                                Font = OsuFont.GetFont(size: 28),
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 76,
                                Masking = true,
                                CornerRadius = 12,
                                BorderThickness = 3,
                                BorderColour = Color4.White,
                                Children = new Drawable[]
                                {
                                    statusBackground = new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = new Color4(85, 68, 20, 255),
                                    },
                                    statusText = new OsuSpriteText
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Text = "准备导入 gate 包…",
                                        Font = OsuFont.GetFont(size: 24),
                                    },
                                },
                            },
                            new Container
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Size = new Vector2(720, 240),
                                Masking = true,
                                CornerRadius = 16,
                                BorderThickness = 4,
                                BorderColour = new Color4(35, 217, 255, 255),
                                Children = new Drawable[]
                                {
                                    new Box
                                     {
                                         RelativeSizeAxes = Axes.Both,
                                         Colour = new Color4(13, 24, 40, 255),
                                     },
                                     noteMount = new Container
                                     {
                                         RelativeSizeAxes = Axes.Both,
                                         Child = createProductionNoteHost(),
                                     },
                                },
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Text = "真实路径：SkinManager → RulesetSkinProvidingContainer → BmsAsyncNoteDrawable",
                                Font = OsuFont.GetFont(size: 18),
                                Colour = new Color4(160, 210, 225, 255),
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Text = "每个阶段仅在目标 visual 完全加载后开始驻留；自动循环 3 轮。",
                                Font = OsuFont.GetFont(size: 17),
                                Colour = new Color4(175, 180, 195, 255),
                            },
                        },
                    },
                },
            };
        }

        private RulesetSkinProvidingContainer createProductionNoteHost()
        {
            var ruleset = new BmsRuleset();
            var beatmap = new BmsBeatmap
            {
                BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
            };

            return new RulesetSkinProvidingContainer(ruleset, beatmap, null)
            {
                RelativeSizeAxes = Axes.Both,
                Child = noteHost = new BmsAsyncNoteDrawable(
                    new BmsNoteSkinLookup(BmsNoteSkinElements.Note, laneIndex: 1, isScratch: false, keymode: BmsKeymode.Key7K)),
            };
        }

        private void detachProductionNoteHost()
        {
            noteMount?.Clear(disposeChildren: true);
            noteHost = null!;
        }

        private void mountProductionNoteHost()
        {
            if (noteMount == null)
                throw new InvalidOperationException("The visual gate note mount is not available.");

            noteMount.Child = createProductionNoteHost();
        }

        private void queueGoodPhase(int cycle)
        {
            AddStep($"cycle {cycle}: reject good package while live preview is attached", () =>
            {
                showLoading(cycle, "GOOD · 正在异步加载 60 帧包");
                Live<SkinInfo> previousSelection = skinManager.CurrentSkinInfo.Value;
                Skin previousOwner = skinManager.CurrentSkin.Value;
                SkinCurrentRevision previousRevision = skinManager.CurrentRevision;
                BmsAsyncNoteDrawable previousHost = noteHost;

                skinManager.CurrentSkinInfo.Value = goodSkin;

                Assert.Multiple(() =>
                {
                    Assert.That(skinManager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.LiveGameplayActive));
                    Assert.That(skinManager.CurrentSkinInfo.Value, Is.SameAs(previousSelection));
                    Assert.That(skinManager.CurrentSkin.Value, Is.SameAs(previousOwner));
                    Assert.That(skinManager.CurrentRevision, Is.SameAs(previousRevision));
                    Assert.That(noteHost, Is.SameAs(previousHost));
                });
            });
            AddStep($"cycle {cycle}: detach live preview before good selection", detachProductionNoteHost);
            AddStep($"cycle {cycle}: select good package", () => skinManager.CurrentSkinInfo.Value = goodSkin);
            AddUntilStep($"cycle {cycle}: good selection active", () => selectedSkinIs(goodSkin));
            AddStep($"cycle {cycle}: remount live preview on good revision", mountProductionNoteHost);
            AddUntilStep($"cycle {cycle}: 60-frame visual loaded", () =>
                noteHost.Drawable is BmsSourceBoundNoteDrawable { IsLoaded: true }
                && noteHost.Drawable.ChildrenOfType<TextureAnimation>().SingleOrDefault()?.FrameCount
                == BmsNoteAnimationManualGateGenerator.ANIMATION_FRAME_COUNT);
            AddStep($"cycle {cycle}: begin good dwell", () =>
                beginDwell(cycle, "GOOD 已加载 · BmsSourceBoundNoteDrawable · 60/60 帧", new Color4(16, 112, 76, 255)));
            AddUntilStep($"cycle {cycle}: good dwell complete", () => Clock.CurrentTime >= phaseDwellEnd);
        }

        private void queueBrokenPhase(int cycle)
        {
            AddStep($"cycle {cycle}: reject broken package while live preview is attached", () =>
            {
                showLoading(cycle, "BROKEN · 正在异步验证缺失 frame 0 的回落");
                Live<SkinInfo> previousSelection = skinManager.CurrentSkinInfo.Value;
                Skin previousOwner = skinManager.CurrentSkin.Value;
                SkinCurrentRevision previousRevision = skinManager.CurrentRevision;
                BmsAsyncNoteDrawable previousHost = noteHost;

                skinManager.CurrentSkinInfo.Value = brokenSkin;

                Assert.Multiple(() =>
                {
                    Assert.That(skinManager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.LiveGameplayActive));
                    Assert.That(skinManager.CurrentSkinInfo.Value, Is.SameAs(previousSelection));
                    Assert.That(skinManager.CurrentSkin.Value, Is.SameAs(previousOwner));
                    Assert.That(skinManager.CurrentRevision, Is.SameAs(previousRevision));
                    Assert.That(noteHost, Is.SameAs(previousHost));
                });
            });
            AddStep($"cycle {cycle}: detach live preview before broken selection", detachProductionNoteHost);
            AddStep($"cycle {cycle}: select broken package", () => skinManager.CurrentSkinInfo.Value = brokenSkin);
            AddUntilStep($"cycle {cycle}: broken selection active", () => selectedSkinIs(brokenSkin));
            AddStep($"cycle {cycle}: remount live preview on broken revision", mountProductionNoteHost);
            AddUntilStep($"cycle {cycle}: default fallback loaded", () => noteHost.Drawable is DefaultBmsNoteDisplay { IsLoaded: true });
            AddStep($"cycle {cycle}: begin broken dwell", () =>
                beginDwell(cycle, "BROKEN 已安全回落 · DefaultBmsNoteDisplay", new Color4(156, 55, 24, 255)));
            AddUntilStep($"cycle {cycle}: broken dwell complete", () => Clock.CurrentTime >= phaseDwellEnd);
        }

        private bool selectedSkinIs(Live<SkinInfo> expected)
            => skinManager.CurrentSkinInfo.Value.ID == expected.ID
               && skinManager.CurrentSkin.Value.SkinInfo.ID == expected.ID;

        private void showLoading(int cycle, string message)
        {
            statusBackground.Colour = new Color4(120, 91, 18, 255);
            statusText.Text = $"循环 {cycle}/{cycle_count} · {message}";
        }

        private void beginDwell(int cycle, string message, Color4 colour)
        {
            statusBackground.Colour = colour;
            statusText.Text = $"循环 {cycle}/{cycle_count} · {message} · 驻留 3 秒";
            phaseDwellEnd = Clock.CurrentTime + phase_dwell_duration;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                try
                {
                    cleanupGateState();
                }
                catch (Exception exception)
                {
                    Logger.Error(exception, "Failed to clean up the isolated BMS note-animation visual gate state.");
                }
            }

            base.Dispose(isDisposing);
        }

        private sealed class NonDeletingImportTask : ImportTask
        {
            public bool DeleteRequested { get; private set; }

            public NonDeletingImportTask(Stream stream, string filename)
                : base(stream, filename)
            {
            }

            public override void DeleteFile() => DeleteRequested = true;
        }
    }
}
