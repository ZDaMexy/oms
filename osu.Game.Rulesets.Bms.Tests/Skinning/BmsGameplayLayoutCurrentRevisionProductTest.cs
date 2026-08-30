// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using SixLabors.ImageSharp.PixelFormats;
using ManiaHoldNote = osu.Game.Rulesets.Mania.Objects.HoldNote;
using ManiaNote = osu.Game.Rulesets.Mania.Objects.Note;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestManagedOrExternalSameIdReloadPublishesExactLayoutPairToLateRenderers(bool external)
        {
            var context = new CurrentRevisionProductContext();
            FullSkinSettingsCallerHost caller = null!;
            ExactLayoutJourneyHost rendererA = null!;
            ExactLayoutJourneyHost rendererB = null!;
            SkinCurrentRevision revisionA = null!;
            SkinCurrentRevision revisionB = null!;
            GameplaySkinLayoutPublication bmsLayoutA = null!;
            GameplaySkinLayoutPublication maniaLayoutA = null!;
            LayoutProductMetrics metricsA = default;

            addSelectLayoutRevisionA(context, external);
            AddStep("mount settings caller and A renderer host", () =>
            {
                revisionA = manager.CurrentRevision;
                Add(caller = new FullSkinSettingsCallerHost(manager));
                Add(rendererA = new ExactLayoutJourneyHost(manager));
            });
            addCaptureExactJourneyLayouts(() => rendererA, () => revisionA, layouts =>
            {
                bmsLayoutA = layouts.Bms;
                maniaLayoutA = layouts.Mania;
                metricsA = layouts.Metrics;
            }, "A");
            AddStep("detach every A gameplay layout holder", () => rendererA.Expire());
            AddUntilStep("wait for A renderer detach", () => rendererA.Parent == null);
            AddUntilStep("wait for reload affordance after A detach", () => caller.ReloadCurrentButton.Enabled.Value);
            AddStep("write same-ID B and invoke unique settings reload", () =>
            {
                writeLayoutRevisionPackage(context.PackageRoot, "B", new Rgba32(20, 210, 120, 255));
                caller.ReloadCurrentButton.TriggerClick();
            });
            AddUntilStep("wait for exact B package publication", () =>
                !ReferenceEquals(manager.CurrentRevision, revisionA)
                && manager.CurrentRevision.RecordId == revisionA.RecordId
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("mount late B renderer host", () =>
            {
                revisionB = manager.CurrentRevision;
                Add(rendererB = new ExactLayoutJourneyHost(manager));
            });
            addCaptureExactJourneyLayouts(() => rendererB, () => revisionB, layouts =>
            {
                assertSameIdLayoutTransition(revisionA, revisionB, bmsLayoutA, layouts.Bms, "bms");
                assertSameIdLayoutTransition(revisionA, revisionB, maniaLayoutA, layouts.Mania, "mania");
                assertProductionGeometryTransition(metricsA, layouts.Metrics);
            }, "B");
            AddStep("detach B renderer host", () => rendererB.Expire());
            AddUntilStep("wait for B renderer detach", () => rendererB.Parent == null);
        }

        [Test]
        public void TestRealmSameIdReloadPublishesExactLayoutPairToLateRenderers()
        {
            Live<SkinInfo> candidate = null!;
            FullSkinSettingsCallerHost caller = null!;
            ExactLayoutJourneyHost rendererA = null!;
            ExactLayoutJourneyHost rendererB = null!;
            SkinCurrentRevision revisionA = null!;
            SkinCurrentRevision revisionB = null!;
            GameplaySkinLayoutPublication bmsLayoutA = null!;
            GameplaySkinLayoutPublication maniaLayoutA = null!;
            LayoutProductMetrics metricsA = default;
            string sourceRoot = string.Empty;

            AddStep("create Realm package A", () =>
            {
                sourceRoot = LocalStorage.GetFullPath($"realm-layout-revision-{Guid.NewGuid():N}");
                writeLayoutRevisionPackage(sourceRoot, "A", new Rgba32(240, 40, 80, 255));
                candidate = createRealmRevisionCandidate(sourceRoot);
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for exact Realm A pair", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && manager.CurrentSkin.Value.PackageContentRevision != null);
            AddStep("mount Realm A settings caller and renderer", () =>
            {
                revisionA = manager.CurrentRevision;
                Add(caller = new FullSkinSettingsCallerHost(manager));
                Add(rendererA = new ExactLayoutJourneyHost(manager));
            });
            addCaptureExactJourneyLayouts(() => rendererA, () => revisionA, layouts =>
            {
                bmsLayoutA = layouts.Bms;
                maniaLayoutA = layouts.Mania;
                metricsA = layouts.Metrics;
            }, "Realm A");
            AddStep("detach Realm A gameplay layout holders", () => rendererA.Expire());
            AddUntilStep("wait for Realm A renderer detach", () => rendererA.Parent == null);
            AddUntilStep("wait for Realm reload affordance", () => caller.ReloadCurrentButton.Enabled.Value);
            AddStep("replace Realm declaration set with B and reload", () =>
            {
                writeLayoutRevisionPackage(sourceRoot, "B", new Rgba32(20, 210, 120, 255));
                replaceRealmRevisionFiles(candidate.ID, sourceRoot);
                caller.ReloadCurrentButton.TriggerClick();
            });
            AddUntilStep("wait for exact Realm B package publication", () =>
                !ReferenceEquals(manager.CurrentRevision, revisionA)
                && manager.CurrentRevision.RecordId == revisionA.RecordId
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("mount late Realm B renderer", () =>
            {
                revisionB = manager.CurrentRevision;
                Add(rendererB = new ExactLayoutJourneyHost(manager));
            });
            addCaptureExactJourneyLayouts(() => rendererB, () => revisionB, layouts =>
            {
                assertSameIdLayoutTransition(revisionA, revisionB, bmsLayoutA, layouts.Bms, "bms");
                assertSameIdLayoutTransition(revisionA, revisionB, maniaLayoutA, layouts.Mania, "mania");
                assertProductionGeometryTransition(metricsA, layouts.Metrics);
            }, "Realm B");
            AddStep("detach Realm B renderer", () => rendererB.Expire());
            AddUntilStep("wait for Realm B renderer detach", () => rendererB.Parent == null);
        }

        private void addCaptureExactJourneyLayouts(
            Func<ExactLayoutJourneyHost> renderer,
            Func<SkinCurrentRevision> revision,
            Action<ExactJourneyLayouts> capture,
            string label)
        {
            AddUntilStep($"wait for {label} renderer host", () => renderer().IsLoaded);
            AddStep($"mount {label} BMS production renderer", () => renderer().ShowBms());
            AddUntilStep($"wait for {label} BMS exact production tree", () => renderer().BmsReady);
            AddStep($"assert {label} BMS exact layout pair", () =>
                assertExactBmsLayoutTree(renderer(), revision()));
            AddStep($"mount {label} mania production renderer", () => renderer().ShowMania());
            AddUntilStep($"wait for {label} mania exact production tree", () => renderer().ManiaReady);
            AddStep($"capture {label} exact layout publications", () =>
            {
                assertExactManiaLayoutTree(renderer(), revision());
                capture(new ExactJourneyLayouts(
                    renderer().BmsLayoutProbe.Publication!,
                    renderer().ManiaLayoutProbe.Publication!,
                    renderer().CaptureProductMetrics()));
            });
        }

        private static void assertExactBmsLayoutTree(ExactLayoutJourneyHost renderer, SkinCurrentRevision revision)
        {
            assertExactLayoutPair(renderer.BmsLayoutProbe, revision, "bms");
            BmsGameplayLayoutSnapshot snapshot = renderer.BmsDrawable.LayoutSnapshot;

            Assert.Multiple(() =>
            {
                Assert.That(renderer.BmsLayoutProbe.Publication!.GetAdapter<BmsGameplayLayoutSnapshot>(), Is.SameAs(snapshot));
                Assert.That(renderer.BmsDrawable.Playfield.LayoutSnapshot, Is.SameAs(snapshot));
                Assert.That(renderer.BmsDrawable.Playfield.GroupContainers.All(group => ReferenceEquals(group.LayoutSnapshot, snapshot)), Is.True);
                Assert.That(renderer.BmsDrawable.Playfield.Lanes.All(lane => ReferenceEquals(lane.LayoutSnapshot, snapshot)), Is.True);
            });
        }

        private static void assertExactManiaLayoutTree(ExactLayoutJourneyHost renderer, SkinCurrentRevision revision)
        {
            assertExactLayoutPair(renderer.ManiaLayoutProbe, revision, "mania");
            GameplaySkinLayoutSnapshot snapshot = renderer.ManiaDrawable.LayoutSnapshot;

            Assert.Multiple(() =>
            {
                Assert.That(renderer.ManiaDrawable.Playfield.LayoutSnapshot, Is.SameAs(snapshot));
                Assert.That(renderer.ManiaDrawable.Playfield.Stages.All(stage => ReferenceEquals(stage.LayoutSnapshot, snapshot)), Is.True);
                Assert.That(renderer.ManiaDrawable.Playfield.Stages.SelectMany(stage => stage.Columns)
                                    .All(column => ReferenceEquals(column.LayoutSnapshot, snapshot)), Is.True);
            });
        }

        private static void assertExactLayoutPair(
            GameplayLayoutPublicationProbe probe,
            SkinCurrentRevision revision,
            string rulesetId)
        {
            GameplaySkinLayoutPublication publication = probe.Publication!;
            GameplaySkinPackageRevision package = probe.PackageRevision!;

            Assert.Multiple(() =>
            {
                Assert.That(publication.Snapshot.Context.RulesetId, Is.EqualTo(rulesetId));
                Assert.That(publication.Adapter.Snapshot, Is.SameAs(publication.Snapshot));
                Assert.That(publication.Snapshot.Context.PackageRevision, Is.SameAs(package));
                Assert.That(package.SourceKind, Is.Not.EqualTo(GameplaySkinPackageSourceKind.Compatibility));
                Assert.That(package.RecordId, Is.EqualTo(revision.RecordId));
                Assert.That(package.ContentRevision, Is.EqualTo(revision.ContentRevision));
                Assert.That(package.Generation, Is.EqualTo(revision.Generation));
                Assert.That(probe.Owner!.CurrentPublication, Is.SameAs(publication));
                Assert.That(probe.Owner.Current, Is.SameAs(publication.Snapshot));
                Assert.That(probe.Owner.LastPrepareWasUpdateThread, Is.False);
                Assert.That(probe.Owner.LastCommitWasUpdateThread, Is.True);
            });
        }

        private static void assertSameIdLayoutTransition(
            SkinCurrentRevision revisionA,
            SkinCurrentRevision revisionB,
            GameplaySkinLayoutPublication layoutA,
            GameplaySkinLayoutPublication layoutB,
            string rulesetId)
        {
            Assert.Multiple(() =>
            {
                Assert.That(revisionB.RecordId, Is.EqualTo(revisionA.RecordId));
                Assert.That(revisionB.ContentRevision, Is.Not.EqualTo(revisionA.ContentRevision));
                Assert.That(layoutB, Is.Not.SameAs(layoutA));
                Assert.That(layoutB.Snapshot, Is.Not.SameAs(layoutA.Snapshot));
                Assert.That(layoutA.Snapshot.Context.LayoutRevision, Is.Zero);
                Assert.That(layoutB.Snapshot.Context.LayoutRevision, Is.Zero);
                Assert.That(
                    layoutB.Snapshot.Diagnostics.Select(diagnostic => diagnostic.Code),
                    Is.EqualTo(layoutA.Snapshot.Diagnostics.Select(diagnostic => diagnostic.Code)));

                if (rulesetId == "bms")
                {
                    Assert.That(
                        layoutA.Snapshot.Diagnostics.Select(diagnostic => diagnostic.Code),
                        Is.EqualTo(new[] { "bms.layout.environment-window-fallback" }),
                        "The headless product host has no native window; only the stable environment fallback is expected.");
                }
                else
                {
                    Assert.That(
                        layoutA.Snapshot.Diagnostics.Select(diagnostic => diagnostic.Code),
                        Is.EqualTo(new[] { "mania.layout.environment-fallback" }),
                        "The headless product host has no native window; only the stable environment fallback is expected.");
                }
                Assert.That(
                    layoutB.Snapshot.LanesInLogicalOrder.Select(lane => lane.LaneId),
                    Is.EqualTo(layoutA.Snapshot.LanesInLogicalOrder.Select(lane => lane.LaneId)),
                    "Topology-preserving geometry reload must not create a second lane identity set.");
                Assert.That(
                    layoutB.Snapshot.GroupsInLogicalOrder.Select(group => group.GroupId),
                    Is.EqualTo(layoutA.Snapshot.GroupsInLogicalOrder.Select(group => group.GroupId)),
                    "Topology-preserving geometry reload must retain stable group identities.");
                Assert.That(layoutA.Snapshot.Context.PackageRevision.RecordId, Is.EqualTo(revisionA.RecordId));
                Assert.That(layoutA.Snapshot.Context.PackageRevision.ContentRevision, Is.EqualTo(revisionA.ContentRevision));
                Assert.That(layoutB.Snapshot.Context.PackageRevision.RecordId, Is.EqualTo(revisionB.RecordId));
                Assert.That(layoutB.Snapshot.Context.PackageRevision.ContentRevision, Is.EqualTo(revisionB.ContentRevision));
                Assert.That(layoutA.Snapshot.Context.RulesetId, Is.EqualTo(rulesetId));
                Assert.That(layoutB.Snapshot.Context.RulesetId, Is.EqualTo(rulesetId));

                string playfieldSurface = rulesetId == "bms" ? BmsGameplayLayoutSurfaceIds.Playfield : "mania.playfield";
                Assert.That(
                    layoutB.Snapshot.GetSurface(playfieldSurface).Rect.Width,
                    Is.Not.EqualTo(layoutA.Snapshot.GetSurface(playfieldSurface).Rect.Width).Within(0.0001f),
                    "The same-ID B package must publish different solved geometry, not merely a fresh object.");
                Assert.That(
                    layoutB.Snapshot.LanesInLogicalOrder[0].Rect.Width,
                    Is.Not.EqualTo(layoutA.Snapshot.LanesInLogicalOrder[0].Rect.Width).Within(0.0001f));
            });
        }

        private static void assertProductionGeometryTransition(LayoutProductMetrics metricsA, LayoutProductMetrics metricsB)
        {
            Assert.Multiple(() =>
            {
                Assert.That(metricsA.AllFinitePositive, Is.True);
                Assert.That(metricsB.AllFinitePositive, Is.True);
                Assert.That(metricsB.BmsGroupWidth, Is.Not.EqualTo(metricsA.BmsGroupWidth).Within(0.01f));
                Assert.That(metricsB.BmsLaneWidth, Is.Not.EqualTo(metricsA.BmsLaneWidth).Within(0.01f));
                Assert.That(metricsB.ManiaStageWidth, Is.Not.EqualTo(metricsA.ManiaStageWidth).Within(0.01f));
                Assert.That(metricsB.ManiaColumnWidth, Is.Not.EqualTo(metricsA.ManiaColumnWidth).Within(0.01f));
            });
        }

        private void addSelectLayoutRevisionA(CurrentRevisionProductContext context, bool external)
        {
            if (external)
            {
                AddStep("create and register external layout revision A", () =>
                {
                    context.PackageRoot = createExternalPackage(root =>
                        writeLayoutRevisionPackage(root, "A", new Rgba32(240, 40, 80, 255)));
                    context.RegistrationTask = manager.RegisterExternalFolderAsync(context.PackageRoot);
                });
                AddUntilStep("wait for external layout A registration", () => context.RegistrationTask?.IsCompleted == true);
                AddStep("query registered external layout A", () =>
                {
                    Assert.That(context.RegistrationTask!.GetAwaiter().GetResult(), Is.True);
                    context.DropdownTask = manager.GetAllUsableSkinsAsync();
                });
                AddUntilStep("wait for external layout A dropdown", () => context.DropdownTask?.IsCompleted == true);
                AddStep("select external layout A", () =>
                {
                    context.Candidate = context.DropdownTask!.GetAwaiter().GetResult()
                                                       .Single(record => record.PerformRead(info =>
                                                           info.IsExternalFilesystemStorage
                                                           && string.Equals(info.FilesystemStoragePath, context.PackageRoot, StringComparison.OrdinalIgnoreCase)));
                    manager.CurrentSkinInfo.Value = context.Candidate;
                });
            }
            else
            {
                AddStep("create and select managed layout revision A", () =>
                {
                    (context.PackageRoot, context.Candidate) = createCandidate(
                        root => writeLayoutRevisionPackage(root, "A", new Rgba32(240, 40, 80, 255)),
                        typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                    manager.CurrentSkinInfo.Value = context.Candidate;
                });
            }

            AddUntilStep("wait for exact layout A pair", () =>
                context.Candidate != null
                && manager.CurrentSkinInfo.Value.ID == context.Candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == context.Candidate.ID
                && manager.CurrentSkin.Value is BmsLegacySkin
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
        }

        private static void writeLayoutRevisionPackage(string packageRoot, string revision, Rgba32 noteColour)
        {
            writeRevisionPackage(packageRoot, revision, noteColour);

            bool first = string.Equals(revision, "A", StringComparison.Ordinal);
            string skinIniPath = Path.Combine(packageRoot, "skin.ini");
            string skinIni = File.ReadAllText(skinIniPath).Replace(
                "LongNoteBodyWidth: 0.4\n",
                "LongNoteBodyWidth: 0.4\n" +
                $"PlayfieldWidth: {(first ? "0.42" : "0.62")}\n" +
                $"NormalLaneWidth: {(first ? "0.90" : "1.15")}\n" +
                $"NormalLaneSpacing: {(first ? "0.02" : "0.12")}\n" +
                "\n" +
                "[Mania]\n" +
                "Keys: 4\n" +
                $"ColumnWidth: {(first ? "28,28,28,28" : "44,44,44,44")}\n" +
                $"ColumnSpacing: {(first ? "1,1,1" : "4,4,4")}\n" +
                $"HitPosition: {(first ? "420" : "320")}\n" +
                $"ComboPosition: {(first ? "120" : "180")}\n",
                StringComparison.Ordinal);
            File.WriteAllText(skinIniPath, skinIni);
        }

        private sealed partial class ExactLayoutJourneyHost : SkinProvidingContainer
        {
            [Cached]
            private readonly SkinManager skinManager;

            [Cached]
            private readonly BmsRulesetConfigManager bmsRulesetConfig;

            [Cached(typeof(ScoreProcessor))]
            private readonly ScoreProcessor scoreProcessor;

            [Cached(typeof(HealthProcessor))]
            private readonly HealthProcessor healthProcessor;

            private readonly Container providerHost;

            public DrawableBmsRuleset BmsDrawable { get; }

            public DrawableManiaRuleset ManiaDrawable { get; }

            public RulesetSkinProvidingContainer BmsProvider { get; }

            public RulesetSkinProvidingContainer ManiaProvider { get; }

            public GameplayLayoutPublicationProbe BmsLayoutProbe { get; }

            public GameplayLayoutPublicationProbe ManiaLayoutProbe { get; }

            public bool BmsReady => BmsLayoutProbe.Publication != null
                                    && BmsDrawable.IsLoaded
                                    && BmsDrawable.Playfield.GroupContainers.All(group => group.IsLoaded)
                                    && BmsDrawable.Playfield.Lanes.All(lane => lane.IsLoaded);

            public bool ManiaReady => ManiaLayoutProbe.Publication != null
                                      && ManiaDrawable.IsLoaded
                                      && ManiaDrawable.Playfield.Stages.All(stage =>
                                          stage.IsLoaded && stage.Columns.All(column => column.IsLoaded));

            public ExactLayoutJourneyHost(SkinManager skinManager)
                : base(skinManager.CurrentSkin.Value)
            {
                this.skinManager = skinManager;
                RelativeSizeAxes = Axes.Both;

                var bmsRuleset = new BmsRuleset();
                bmsRulesetConfig = new BmsRulesetConfigManager(null, bmsRuleset.RulesetInfo);
                scoreProcessor = bmsRuleset.CreateScoreProcessor();
                healthProcessor = bmsRuleset.CreateHealthProcessor(0);
                var decoded = new BmsBeatmapDecoder().DecodeText(@"
#TITLE Current revision layout product
#BPM 120
#LNTYPE 1
#WAV01 note.wav
#WAV02 hold.wav
#00111:0100
#00119:0001
                #00151:02000200
", "current-revision-layout.bme");
                var bmsBeatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decoded), bmsRuleset).Convert();
                healthProcessor.ApplyBeatmap(bmsBeatmap);
                scoreProcessor.ApplyBeatmap(bmsBeatmap);
                BmsDrawable = (DrawableBmsRuleset)bmsRuleset.CreateDrawableRulesetWith(bmsBeatmap);
                BmsLayoutProbe = new GameplayLayoutPublicationProbe();
                BmsProvider = new RulesetSkinProvidingContainer(
                    bmsRuleset,
                    bmsBeatmap,
                    null,
                    prepareGameplaySkinLayout: true)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            BmsDrawable,
                            BmsLayoutProbe,
                        },
                    },
                };

                var maniaRuleset = new ManiaRuleset();
                var maniaBeatmap = new ManiaBeatmap(new StageDefinition(4))
                {
                    BeatmapInfo = { Ruleset = maniaRuleset.RulesetInfo },
                    ControlPointInfo = new ControlPointInfo(),
                };
                maniaBeatmap.HitObjects.Add(new ManiaNote
                {
                    Column = 0,
                    StartTime = 1_000_000,
                });
                maniaBeatmap.HitObjects.Add(new ManiaHoldNote
                {
                    Column = 3,
                    StartTime = 1_001_000,
                    Duration = 2_000,
                });

                foreach (var hitObject in maniaBeatmap.HitObjects)
                    hitObject.ApplyDefaults(maniaBeatmap.ControlPointInfo, new BeatmapDifficulty());

                ManiaDrawable = (DrawableManiaRuleset)maniaRuleset.CreateDrawableRulesetWith(maniaBeatmap);
                ManiaLayoutProbe = new GameplayLayoutPublicationProbe();
                ManiaProvider = new RulesetSkinProvidingContainer(
                    maniaRuleset,
                    maniaBeatmap,
                    null,
                    prepareGameplaySkinLayout: true)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            ManiaDrawable,
                            ManiaLayoutProbe,
                        },
                    },
                };

                InternalChild = providerHost = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                };
            }

            public void ShowBms()
            {
                if (BmsProvider.Parent == null)
                    providerHost.Add(BmsProvider);
            }

            public void ShowMania()
            {
                if (ManiaProvider.Parent == null)
                    providerHost.Add(ManiaProvider);
            }

            public LayoutProductMetrics CaptureProductMetrics()
            {
                var bmsGroup = BmsDrawable.Playfield.GroupContainers.First();
                var bmsLane = BmsDrawable.Playfield.Lanes.First();
                var maniaStage = ManiaDrawable.Playfield.Stages.First();
                var maniaColumn = maniaStage.Columns.First();

                return new LayoutProductMetrics(
                    bmsGroup.ScreenSpaceDrawQuad.AABBFloat.Width,
                    bmsLane.ScreenSpaceDrawQuad.AABBFloat.Width,
                    maniaStage.ScreenSpaceDrawQuad.AABBFloat.Width,
                    maniaColumn.ScreenSpaceDrawQuad.AABBFloat.Width);
            }
        }

        private readonly record struct ExactJourneyLayouts(
            GameplaySkinLayoutPublication Bms,
            GameplaySkinLayoutPublication Mania,
            LayoutProductMetrics Metrics);

        private readonly record struct LayoutProductMetrics(
            float BmsGroupWidth,
            float BmsLaneWidth,
            float ManiaStageWidth,
            float ManiaColumnWidth)
        {
            public bool AllFinitePositive => finitePositive(BmsGroupWidth)
                                             && finitePositive(BmsLaneWidth)
                                             && finitePositive(ManiaStageWidth)
                                             && finitePositive(ManiaColumnWidth);

            private static bool finitePositive(float value) => float.IsFinite(value) && value > 0;
        }

        private sealed partial class GameplayLayoutPublicationProbe : Drawable
        {
            public GameplaySkinPackageRevision? PackageRevision { get; private set; }

            public GameplaySkinLayoutRevisionOwner? Owner { get; private set; }

            public GameplaySkinLayoutPublication? Publication { get; private set; }

            [BackgroundDependencyLoader]
            private void load(
                GameplaySkinPackageRevision packageRevision,
                GameplaySkinLayoutRevisionOwner owner)
            {
                PackageRevision = packageRevision;
                Owner = owner;
                Publication = owner.CurrentPublication
                              ?? throw new InvalidOperationException("A production layout probe loaded before exact publication.");
            }
        }
    }
}
