// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Testing;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ManiaHoldNote = osu.Game.Rulesets.Mania.Objects.HoldNote;
using ManiaNote = osu.Game.Rulesets.Mania.Objects.Note;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [Test]
        public void TestDetachedExactGameplayRootsRetireManagedPackageMaterialPreparations()
        {
            var context = new CurrentRevisionProductContext();
            ExactLayoutJourneyHost first = null!;
            ExactLayoutJourneyHost second = null!;
            BmsLegacySkin selected = null!;
            SkinCurrentRevision selectedRevision = null!;

            addSelectLayoutRevisionA(context, external: false);
            AddStep("capture selected exact package owner", () =>
            {
                selectedRevision = manager.CurrentRevision;
                selected = (BmsLegacySkin)selectedRevision.Owner;
            });
            AddStep("mount first exact BMS root", () =>
            {
                Add(first = new ExactLayoutJourneyHost(manager));
                first.ShowBms();
            });
            AddUntilStep("wait for first exact BMS publication", () => first.BmsReady);
            AddStep("first root owns one exact material borrow", () => Assert.Multiple(() =>
            {
                Assert.That(manager.CurrentRevision, Is.SameAs(selectedRevision));
                Assert.That(manager.CurrentRevision.Owner, Is.SameAs(selected));
                Assert.That(selected.ActiveExactManagedPackageNotePreparationCount, Is.EqualTo(1));
                Assert.That(selected.ActiveExactManagedPackageNoteBorrowCount, Is.EqualTo(1));
            }));
            AddStep("detach first exact BMS root", () => first.Expire());
            AddUntilStep("wait for first exact BMS root detach", () => first.Parent == null);
            AddUntilStep("first exact material preparation retired", () =>
                selected.ActiveExactManagedPackageNotePreparationCount == 0
                && selected.ActiveExactManagedPackageNoteBorrowCount == 0);
            AddStep("mount second exact BMS root", () =>
            {
                Add(second = new ExactLayoutJourneyHost(manager));
                second.ShowBms();
            });
            AddUntilStep("wait for second exact BMS publication", () => second.BmsReady);
            AddStep("second distinct layout owns only one exact material borrow", () => Assert.Multiple(() =>
            {
                Assert.That(manager.CurrentRevision, Is.SameAs(selectedRevision));
                Assert.That(manager.CurrentRevision.Owner, Is.SameAs(selected));
                Assert.That(selectedRevision.Retired.IsCompleted, Is.False);
                Assert.That(selected.ActiveExactManagedPackageNotePreparationCount, Is.EqualTo(1));
                Assert.That(selected.ActiveExactManagedPackageNoteBorrowCount, Is.EqualTo(1));
            }));
            AddStep("detach second exact BMS root", () => second.Expire());
            AddUntilStep("wait for second exact BMS root detach", () => second.Parent == null);
            AddUntilStep("second exact material preparation retired", () =>
                selected.ActiveExactManagedPackageNotePreparationCount == 0
                && selected.ActiveExactManagedPackageNoteBorrowCount == 0);
        }

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

        [TestCase(false)]
        [TestCase(true)]
        public void TestFailedSameIdBReloadKeepsExactAMaterialForLateRenderer(bool external)
        {
            var context = new CurrentRevisionProductContext();
            FullSkinSettingsCallerHost caller = null!;
            ExactLayoutJourneyHost rendererA = null!;
            ExactLayoutJourneyHost lateRenderer = null!;
            SkinCurrentRevision revisionA = null!;
            GameplaySkinLayoutPublication publicationA = null!;
            int prepareCount = 0;
            int retiredA = 0;

            addSelectLayoutRevisionA(context, external);
            AddStep("mount A renderer before failed B", () =>
            {
                revisionA = manager.CurrentRevision;
                manager.CurrentRevisionPrepareStarted = () => prepareCount++;
                manager.CurrentRevisionRetired += revision =>
                {
                    if (ReferenceEquals(revision, revisionA))
                        retiredA++;
                };
                Add(caller = new FullSkinSettingsCallerHost(manager));
                Add(rendererA = new ExactLayoutJourneyHost(manager));
                rendererA.ShowBms();
            });
            AddUntilStep("wait for exact A material before failed B", () => rendererA.BmsReady);
            AddStep("capture exact A material publication", () =>
            {
                assertExactBmsLayoutTree(rendererA, revisionA);
                publicationA = rendererA.BmsLayoutProbe.Publication!;
            });
            AddStep("detach A renderer before reload attempt", () => rendererA.Expire());
            AddUntilStep("wait for A renderer detach before failed B", () => rendererA.Parent == null);
            AddUntilStep("wait for failed-B reload affordance", () => caller.ReloadCurrentButton.Enabled.Value);
            AddStep("remove B configuration and invoke same-ID reload", () =>
            {
                File.Delete(Path.Combine(context.PackageRoot, "skin.ini"));
                caller.ReloadCurrentButton.TriggerClick();
            });
            AddUntilStep("wait for B prepare failure", () => prepareCount == 1 && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("assert manager retained exact A revision", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(manager.CurrentRevision.Owner, Is.SameAs(revisionA.Owner));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(retiredA, Is.Zero);
                });
            });
            AddStep("mount late renderer after failed B", () =>
            {
                Add(lateRenderer = new ExactLayoutJourneyHost(manager));
                lateRenderer.ShowBms();
            });
            AddUntilStep("wait for late exact A material", () => lateRenderer.BmsReady);
            AddStep("assert failed B preserved exact A material authority", () =>
            {
                assertExactBmsLayoutTree(lateRenderer, revisionA);
                GameplaySkinLayoutPublication late = lateRenderer.BmsLayoutProbe.Publication!;

                Assert.Multiple(() =>
                {
                    Assert.That(late.MaterialSet.PackageRevision.RecordId, Is.EqualTo(publicationA.MaterialSet.PackageRevision.RecordId));
                    Assert.That(late.MaterialSet.PackageRevision.ContentRevision, Is.EqualTo(publicationA.MaterialSet.PackageRevision.ContentRevision));
                    Assert.That(late.MaterialSet.ContractIdentity, Is.EqualTo(publicationA.MaterialSet.ContractIdentity));
                    Assert.That(
                        late.MaterialSet.Entries.Select(entry => (entry.Key, entry.State, entry.Source)),
                        Is.EqualTo(publicationA.MaterialSet.Entries.Select(entry => (entry.Key, entry.State, entry.Source))));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(retiredA, Is.Zero);
                });
            });
            AddStep("detach late A renderer", () => lateRenderer.Expire());
            AddUntilStep("wait for late A renderer detach", () => lateRenderer.Parent == null);
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

        [TestCase(MaterialDiagnosticPackageSource.OrdinaryRealm)]
        [TestCase(MaterialDiagnosticPackageSource.ManagedFolder)]
        [TestCase(MaterialDiagnosticPackageSource.ExternalFolder)]
        public void TestPublicCommonNoteMaterialDrivesExactFiveKeyRendererFromCurrentRevision(MaterialDiagnosticPackageSource source)
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;
            BmsAsyncNoteDrawable noteHost = null!;
            string packageRoot = string.Empty;
            Task<bool>? registrationTask = null;
            Task<IList<Live<SkinInfo>>>? dropdownTask = null;
            SkinCurrentRevision revision = null!;

            switch (source)
            {
                case MaterialDiagnosticPackageSource.OrdinaryRealm:
                    AddStep("create ordinary Realm public-material package", () =>
                    {
                        packageRoot = LocalStorage.GetFullPath($"realm-public-note-{Guid.NewGuid():N}");
                        writePublicCommonFiveKeyNotePackage(packageRoot);
                        candidate = createRealmRevisionCandidate(packageRoot);
                        manager.CurrentSkinInfo.Value = candidate;
                    });
                    break;

                case MaterialDiagnosticPackageSource.ManagedFolder:
                    AddStep("create managed public-material package", () =>
                    {
                        (packageRoot, candidate) = createCandidate(
                            writePublicCommonFiveKeyNotePackage,
                            typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                        manager.CurrentSkinInfo.Value = candidate;
                    });
                    break;

                case MaterialDiagnosticPackageSource.ExternalFolder:
                    AddStep("create and register external public-material package", () =>
                    {
                        packageRoot = createExternalPackage(writePublicCommonFiveKeyNotePackage);
                        registrationTask = manager.RegisterExternalFolderAsync(packageRoot);
                    });
                    AddUntilStep("wait for external public-material registration", () => registrationTask?.IsCompleted == true);
                    AddStep("query external public-material candidate", () =>
                    {
                        Assert.That(registrationTask!.GetAwaiter().GetResult(), Is.True);
                        dropdownTask = manager.GetAllUsableSkinsAsync();
                    });
                    AddUntilStep("wait for external public-material candidate", () => dropdownTask?.IsCompleted == true);
                    AddStep("select external public-material package", () =>
                    {
                        candidate = dropdownTask!.GetAwaiter().GetResult()
                                                .Single(record => record.PerformRead(info =>
                                                    info.IsExternalFilesystemStorage
                                                    && string.Equals(info.FilesystemStoragePath, packageRoot, StringComparison.OrdinalIgnoreCase)));
                        manager.CurrentSkinInfo.Value = candidate;
                    });
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown public-material package source.");
            }

            AddUntilStep("wait for exact public-material current revision", () =>
                candidate != null
                && manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && manager.CurrentSkin.Value is BmsLegacySkin
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount exact five-key BMS renderer from current revision", () =>
            {
                revision = manager.CurrentRevision;
                Add(renderer = new ExactLayoutJourneyHost(manager, useFiveKeyBeatmap: true));
                renderer.ShowBms();
            });
            AddUntilStep("wait for exact five-key material publication", () => renderer.BmsReady);
            AddUntilStep("wait for public note texture in production drawable", () =>
            {
                noteHost = renderer.BmsDrawable.ChildrenOfType<BmsAsyncNoteDrawable>()
                                   .FirstOrDefault(host => host.Lookup.Element == BmsNoteSkinElements.Note
                                                           && host.Lookup.Keymode == BmsKeymode.Key5K
                                                           && host.Lookup.LaneIndex == 1
                                                           && host.Lookup.LaneId?.Value == "bms.lane.key-1")!;
                Sprite? sprite = noteHost?.Drawable?.ChildrenOfType<Sprite>().SingleOrDefault();
                return noteHost?.Drawable is BmsSourceBoundNoteDrawable
                       && sprite?.Texture?.Width == 11
                       && sprite.Texture.Height == 13;
            });
            AddStep("assert public document material reached exact production note", () =>
            {
                GameplaySkinLayoutPublication publication = renderer.BmsLayoutProbe.Publication!;
                BmsGameplayLayoutSnapshot layout = publication.GetAdapter<BmsGameplayLayoutSnapshot>();
                GameplaySkinResolvedMaterialSet materialSet = publication.MaterialSet;
                BmsGameplayLayoutLane lane = layout.GetLaneByLogicalIndex(1);
                var key = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.Note,
                    BmsGameplayNoteMaterialTarget.Create(layout, lane));
                Assert.That(materialSet.TryGet(key, out GameplaySkinResolvedMaterialEntry? entry), Is.True);
                IBmsResolvedNoteMaterial material = entry!.GetMaterial<IBmsResolvedNoteMaterial>();
                Sprite visual = noteHost.Drawable!.ChildrenOfType<Sprite>().Single();
                BmsManagedPackageSourceRevision selectedSource = ((BmsLegacySkin)revision.Owner).CaptureManagedPackageSourceRevision();

                Assert.Multiple(() =>
                {
                    Assert.That(layout.Keymode, Is.EqualTo(BmsKeymode.Key5K));
                    Assert.That(lane.LaneId.Value, Is.EqualTo("bms.lane.key-1"));
                    Assert.That(materialSet.Snapshot, Is.SameAs(layout.Neutral));
                    Assert.That(noteHost.Lookup.LayoutSnapshot, Is.SameAs(layout));
                    Assert.That(noteHost.Lookup.MaterialSet, Is.SameAs(materialSet));
                    Assert.That(entry.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Provide));
                    Assert.That(entry.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.SelectedPackage));
                    Assert.That(entry.Source.StableId, Is.EqualTo("selected-document"));
                    Assert.That(entry.Source.ContentRevision, Is.EqualTo(selectedSource.ParsedConfigurationContentHash));
                    Assert.That(entry.RuntimeValueType, Is.EqualTo(typeof(BmsSourceBoundNoteMaterial)));
                    Assert.That(material.Element, Is.EqualTo(BmsNoteSkinElements.Note));
                    Assert.That(material.FrameCount, Is.EqualTo(1));
                    Assert.That(noteHost.Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                    Assert.That(visual.Texture.Width, Is.EqualTo(11));
                    Assert.That(visual.Texture.Height, Is.EqualTo(13));
                    Assert.That(noteHost.ChildrenOfType<DefaultBmsNoteDisplay>(), Is.Empty,
                        "The exact selected-document material must not re-enter the legacy bucket or programmatic fallback.");
                    Assert.That(materialSet.Entries.Count(candidateEntry => candidateEntry.Source.StableId == "selected-document"), Is.EqualTo(1));
                    Assert.That(materialSet.PackageRevision.RecordId, Is.EqualTo(revision.RecordId));
                    Assert.That(materialSet.PackageRevision.ContentRevision, Is.EqualTo(revision.ContentRevision));
                });
            });
            AddStep("detach public-material renderer", () => renderer.Expire());
            AddUntilStep("wait for public-material renderer detach", () => renderer.Parent == null);
        }

        [Test]
        public void TestSelectedCommonTailSuppressPublishesExplicitMarkerWithoutPostCommitFallback()
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;
            BmsAsyncNoteDrawable tailHost = null!;

            AddStep("create selected package with common tail suppress", () =>
            {
                (_, candidate) = createCandidate(writeTailSuppressPackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for selected suppress package", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && manager.CurrentSkin.Value.PackageContentRevision != null);
            AddStep("mount exact BMS renderer for suppressed tail", () =>
            {
                Add(renderer = new ExactLayoutJourneyHost(manager));
                renderer.ShowBms();
            });
            AddUntilStep("wait for exact suppressed-tail publication", () => renderer.BmsReady);
            AddUntilStep("wait for explicit suppressed-tail marker", () =>
            {
                tailHost = renderer.BmsDrawable.ChildrenOfType<BmsAsyncNoteDrawable>()
                                   .FirstOrDefault(host => host.Lookup.Element == BmsNoteSkinElements.LongNoteTail
                                                           && host.Lookup.LaneIndex == 1)!;
                return tailHost?.Drawable is BmsSuppressedNoteDrawable;
            });
            AddStep("assert suppress remains explicit after commit", () =>
            {
                GameplaySkinLayoutPublication publication = renderer.BmsLayoutProbe.Publication!;
                BmsGameplayLayoutSnapshot layout = publication.GetAdapter<BmsGameplayLayoutSnapshot>();
                BmsGameplayLayoutLane lane = layout.GetLaneByLogicalIndex(1);
                GameplaySkinResolvedMaterialTarget target = BmsGameplayNoteMaterialTarget.Create(layout, lane);
                var key = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.LongNoteTail, target);

                Assert.Multiple(() =>
                {
                    Assert.That(publication.MaterialSet.TryGet(key, out GameplaySkinResolvedMaterialEntry? entry), Is.True);
                    Assert.That(entry!.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Suppress));
                    Assert.That(tailHost.Drawable, Is.TypeOf<BmsSuppressedNoteDrawable>());
                    Assert.That(tailHost.ChildrenOfType<DefaultBmsLongNoteTailDisplay>(), Is.Empty,
                        "A committed Suppress entry must not re-enter the protected legacy fallback.");
                });
            });
            AddStep("detach suppress renderer", () => renderer.Expire());
            AddUntilStep("wait for suppress renderer detach", () => renderer.Parent == null);
        }

        [Test]
        public void TestLegacyBeatmapDirectMaterialWinsAboveSelectedSuppressInExactPublication()
        {
            Live<SkinInfo> candidate = null!;
            ResourceLegacyBeatmapSkin beatmapSkin = null!;
            ExactLayoutJourneyHost renderer = null!;
            BmsAsyncNoteDrawable tailHost = null!;

            AddStep("create selected suppress package and legacy beatmap skin", () =>
            {
                (_, candidate) = createCandidate(writeTailSuppressPackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                beatmapSkin = new ResourceLegacyBeatmapSkin(host.Renderer.WhitePixel);
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for selected package below beatmap", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && manager.CurrentSkin.Value.PackageContentRevision != null);
            AddStep("mount exact renderer with real legacy beatmap skin", () =>
            {
                Add(renderer = new ExactLayoutJourneyHost(manager, beatmapSkin));
                renderer.ShowBms();
            });
            AddUntilStep("wait for exact beatmap compatibility publication", () => renderer.BmsReady);
            AddUntilStep("wait for beatmap tail material visual", () =>
            {
                tailHost = renderer.BmsDrawable.ChildrenOfType<BmsAsyncNoteDrawable>()
                                   .FirstOrDefault(host => host.Lookup.Element == BmsNoteSkinElements.LongNoteTail
                                                           && host.Lookup.LaneIndex == 1)!;
                return tailHost?.Drawable is BmsSourceBoundNoteDrawable;
            });
            AddStep("assert legacy beatmap material outranks selected suppress", () =>
            {
                GameplaySkinLayoutPublication publication = renderer.BmsLayoutProbe.Publication!;
                BmsGameplayLayoutSnapshot layout = publication.GetAdapter<BmsGameplayLayoutSnapshot>();
                BmsGameplayLayoutLane lane = layout.GetLaneByLogicalIndex(1);
                var key = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.LongNoteTail,
                    BmsGameplayNoteMaterialTarget.Create(layout, lane));

                Assert.Multiple(() =>
                {
                    Assert.That(beatmapSkin.AllowsGameplaySkinDocumentAuthoring, Is.False);
                    Assert.That(publication.MaterialSet.TryGet(key, out GameplaySkinResolvedMaterialEntry? entry), Is.True);
                    Assert.That(entry!.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Provide));
                    Assert.That(entry.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.LegacyBeatmapCompatibility));
                    Assert.That(entry.Source.StableId, Is.EqualTo("legacy-beatmap-direct"));
                    Assert.That(tailHost.Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                    Assert.That(tailHost.ChildrenOfType<BmsSuppressedNoteDrawable>(), Is.Empty);
                });
            });
            AddStep("detach beatmap compatibility renderer", () => renderer.Expire());
            AddUntilStep("wait for beatmap compatibility renderer detach", () => renderer.Parent == null);
            AddStep("dispose legacy beatmap skin", () => beatmapSkin.Dispose());
        }

        [Test]
        public void TestRulesetResourceMaterialIsPreparedIntoExactPublicationBeforeProgrammaticFallback()
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;
            BmsAsyncNoteDrawable laneTwoNote = null!;

            AddStep("create selected package with a lane-two ruleset resource", () =>
            {
                (_, candidate) = createCandidate(
                    root => writeLayoutRevisionPackage(root, "A", new Rgba32(240, 40, 80, 255)),
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for selected package above ruleset resource", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && manager.CurrentSkin.Value.PackageContentRevision != null);
            AddStep("mount exact BMS renderer with production ruleset resource store", () =>
            {
                Add(renderer = new ExactLayoutJourneyHost(
                    manager,
                    bmsRulesetOverride: new NoteResourceBmsRuleset(createPng(new Rgba32(35, 205, 145, 255)))));
                renderer.ShowBms();
            });
            AddUntilStep("wait for exact ruleset-resource publication", () => renderer.BmsReady);
            AddUntilStep("wait for lane-two production note visual", () =>
            {
                laneTwoNote = renderer.BmsDrawable.ChildrenOfType<BmsAsyncNoteDrawable>()
                                      .FirstOrDefault(host => host.Lookup.Element == BmsNoteSkinElements.Note
                                                              && host.Lookup.LaneIndex == 2)!;
                return laneTwoNote?.Drawable is BmsSourceBoundNoteDrawable;
            });
            AddStep("assert ruleset resource was prepared into final material set", () =>
            {
                GameplaySkinLayoutPublication publication = renderer.BmsLayoutProbe.Publication!;
                BmsGameplayLayoutSnapshot layout = publication.GetAdapter<BmsGameplayLayoutSnapshot>();
                BmsGameplayLayoutLane lane = layout.GetLaneByLogicalIndex(2);
                var key = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.Note,
                    BmsGameplayNoteMaterialTarget.Create(layout, lane));

                Assert.Multiple(() =>
                {
                    Assert.That(publication.MaterialSet.TryGet(key, out GameplaySkinResolvedMaterialEntry? entry), Is.True);
                    Assert.That(entry!.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Provide));
                    Assert.That(entry.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.RulesetResources));
                    Assert.That(entry.Source.StableId, Is.EqualTo("bms-ruleset-resources"));
                    Assert.That(laneTwoNote.Lookup.MaterialSet, Is.SameAs(publication.MaterialSet));
                    Assert.That(laneTwoNote.Drawable, Is.TypeOf<BmsSourceBoundNoteDrawable>());
                    Assert.That(laneTwoNote.ChildrenOfType<DefaultBmsNoteDisplay>(), Is.Empty,
                        "A committed ruleset-resource material must not re-enter the programmatic fallback.");
                });
            });
            AddStep("detach ruleset-resource renderer", () => renderer.Expire());
            AddUntilStep("wait for ruleset-resource renderer detach", () => renderer.Parent == null);
        }

        [Test]
        public void TestExactMaterialPreparationCancellationOnHostShutdownNeverCommits()
        {
            var context = new CurrentRevisionProductContext();
            ExactLayoutJourneyHost renderer = null!;
            CancellationProbeBmsRuleset ruleset = null!;
            SkinCurrentRevision revision = null!;
            BmsLegacySkin selected = null!;
            CancellationTokenSource loadCancellation = null!;
            Task loadTask = null!;
            bool loadCallbackInvoked = false;

            addSelectLayoutRevisionA(context, external: false);
            AddStep("mount exact BMS renderer with blocked material resource", () =>
            {
                revision = manager.CurrentRevision;
                selected = (BmsLegacySkin)revision.Owner;
                ruleset = new CancellationProbeBmsRuleset(createPng(new Rgba32(35, 205, 145, 255)));
                renderer = new ExactLayoutJourneyHost(manager, bmsRulesetOverride: ruleset);
                // Pre-attach the production provider while the outer host is still unloaded. Adding the complete
                // subtree below then runs its BDL on the framework background loader, whose real token is cancelled
                // through LoadComponentAsync below; attaching the provider to an already-loaded host would synchronously
                // join PreparePublication on the update thread and make a cancellation step impossible to schedule.
                renderer.ShowBms();
                loadCancellation = new CancellationTokenSource();
                loadTask = LoadComponentAsync(
                    renderer,
                    loaded =>
                    {
                        loadCallbackInvoked = true;
                        Add(loaded);
                    },
                    loadCancellation.Token);
            });
            AddUntilStep("wait for production material prepare gate", () => ruleset.Resources.Entered.IsSet);
            AddStep("assert no partial publication before cancellation", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(ruleset.PrepareEntered, Is.True);
                    Assert.That(ruleset.PreparationToken.CanBeCanceled, Is.True);
                    Assert.That(ruleset.RevisionOwner, Is.Not.Null);
                    Assert.That(ruleset.RevisionOwner!.CurrentPublication, Is.Null);
                    Assert.That(renderer.BmsLayoutProbe.Publication, Is.Null);
                });

                loadCancellation.Cancel();
            });
            AddUntilStep("wait for host shutdown cancellation", () => ruleset.PreparationToken.IsCancellationRequested);
            AddStep("release cancelled resource prepare", () => ruleset.Resources.Release.Set());
            AddUntilStep("wait for cancelled renderer load completion", () => loadTask.IsCompleted);
            AddUntilStep("cancelled provisional material borrow retired", () =>
                selected.ActiveExactManagedPackageNotePreparationCount == 0
                && selected.ActiveExactManagedPackageNoteBorrowCount == 0);
            AddStep("assert cancelled prepare never committed package layout material", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(ruleset.PreparationToken.IsCancellationRequested, Is.True);
                    Assert.That(ruleset.RevisionOwner, Is.Not.Null);
                    Assert.That(ruleset.RevisionOwner!.CurrentPublication, Is.Null);
                    Assert.That(renderer.BmsLayoutProbe.Publication, Is.Null);
                    Assert.That(renderer.BmsReady, Is.False);
                    Assert.That(loadCallbackInvoked, Is.False);
                    Assert.That(renderer.Parent, Is.Null);
                    Assert.That(manager.CurrentRevision, Is.SameAs(revision));
                    Assert.That(revision.Retired.IsCompleted, Is.False);
                });

                renderer.Dispose();
                loadCancellation.Dispose();
            });
        }

        [Test]
        public void TestCancellationAfterExactMaterialCarrierCreationReleasesBorrowAndWorkLease()
        {
            var context = new CurrentRevisionProductContext();

            addSelectLayoutRevisionA(context, external: false);
            AddStep("cancel after exact material carrier creation", () =>
            {
                SkinCurrentRevision revision = manager.CurrentRevision;
                var selected = (BmsLegacySkin)revision.Owner;
                using var beatmapHost = new ExactLayoutJourneyHost(manager);
                using SkinRevisionParticipantRegistration participant = manager.RegisterRevisionParticipant(
                    SkinRevisionParticipantKind.LiveGameplayHost,
                    "cancelled exact BMS layout root",
                    affectsGameplayLayoutPublication: true);
                GameplaySkinPackageRevision package = GameplaySkinPackageRevision.Create(revision);
                using var owner = new GameplaySkinLayoutRevisionOwner(
                    package,
                    validateRoot: () => participant.TryGetCurrentRevision(out SkinCurrentRevision? current)
                                        && package.RetainsExact(current!),
                    acquireWorkLease: participant.AcquireWorkLease,
                    captureParticipantGeneration: () => participant.TryCapturePublicationGeneration(out long generation) ? generation : null,
                    validateParticipantGeneration: participant.IsPublicationGenerationCurrent,
                    commitAtParticipantGeneration: participant.TryCommitAtPublicationGeneration,
                    dispatchCommit: commit =>
                    {
                        commit();
                        return true;
                    });
                using var cancellation = new CancellationTokenSource();
                bool borrowWasActive = false;

                Assert.That(
                    () => owner.PreparePublication(
                        layoutRevision =>
                        {
                            BmsBeatmap beatmap = beatmapHost.BmsBeatmap;
                            var topologyOwner = new BmsGameplaySkinLaneTopologyRevisionOwner();
                            BmsGameplaySkinLaneTopologyPublication topology = topologyOwner.Publish(
                                beatmap.BmsInfo.Keymode,
                                BmsPlayfieldStyle.P1);
                            BmsGameplayLayoutSnapshot layout = BmsGameplayLayoutSolver.Solve(
                                beatmap.BmsInfo.KeymodeResolution,
                                BmsPlayfieldStyle.P1,
                                BmsGameplayLayoutConfiguration.FromSkin(manager.CurrentSkin.Value, beatmap.BmsInfo.Keymode),
                                BmsGameplayLayoutEnvironment.Default,
                                package,
                                topology,
                                layoutRevision);
                            GameplaySkinLayoutPublication publication = BmsGameplayResolvedNoteMaterialPreparer.Prepare(
                                manager.CurrentSkin.Value,
                                layout,
                                CancellationToken.None);
                            borrowWasActive = selected.ActiveExactManagedPackageNotePreparationCount == 1
                                              && selected.ActiveExactManagedPackageNoteBorrowCount == 1;
                            cancellation.Cancel();
                            return publication;
                        },
                        cancellation.Token),
                    Throws.TypeOf<OperationCanceledException>());

                Assert.Multiple(() =>
                {
                    Assert.That(borrowWasActive, Is.True);
                    Assert.That(owner.CurrentPublication, Is.Null);
                    Assert.That(selected.ActiveExactManagedPackageNotePreparationCount, Is.Zero);
                    Assert.That(selected.ActiveExactManagedPackageNoteBorrowCount, Is.Zero);
                    Assert.That(revision.WorkDetached.IsCompleted, Is.True);
                    Assert.That(manager.CurrentRevision, Is.SameAs(revision));
                });
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestExactPublishedLookupFaultNeverFallsBackAfterCommit(bool returnNull)
        {
            var context = new CurrentRevisionProductContext();
            ExactLayoutJourneyHost renderer = null!;
            BmsAsyncNoteDrawable faultingHost = null!;
            var callbackScheduler = new Scheduler();

            addSelectLayoutRevisionA(context, external: false);
            AddStep("mount exact BMS renderer before resolver fault", () =>
            {
                Add(renderer = new ExactLayoutJourneyHost(manager));
                renderer.ShowBms();
            });
            AddUntilStep("wait for exact material before resolver fault", () => renderer.BmsReady);
            AddStep("mount exact lookup with faulting post-commit visual resolver", () =>
            {
                GameplaySkinLayoutPublication publication = renderer.BmsLayoutProbe.Publication!;
                BmsGameplayLayoutSnapshot layout = publication.GetAdapter<BmsGameplayLayoutSnapshot>();
                BmsGameplayLayoutLane lane = layout.GetLaneByLogicalIndex(1);
                var lookup = new BmsNoteSkinLookup(
                    BmsNoteSkinElements.Note,
                    lane.LogicalIndex,
                    lane.IsScratch,
                    layout.Keymode,
                    lane.LaneId,
                    layout,
                    publication.MaterialSet);
                var compatibilityProvider = new BmsManagedPackageNoteCompatibilityProvider((BmsLegacySkin)manager.CurrentSkin.Value);

                Assert.Multiple(() =>
                {
                    Assert.That(compatibilityProvider.ClaimsCompatibilityDeclaration(lookup), Is.False);
                    Assert.That(() => compatibilityProvider.ResolveCompatibility(lookup), Throws.ArgumentException);
                    Assert.That(
                        () => compatibilityProvider.GetSlot(
                            new GameplaySkinSlotLookup<BmsNoteSkinLookup>(GameplaySkinSlotCatalog.Note, lookup)),
                        Throws.ArgumentException);
                });
                faultingHost = new BmsAsyncNoteDrawable(lookup)
                {
                    LoadCallbackScheduler = callbackScheduler,
                    DrawableResolver = returnNull
                        ? (_, _) => null
                        : (_, _) => throw new InvalidOperationException("Intentional exact material visual fault."),
                };
                renderer.AddBmsAuxiliary(faultingHost);
                Assert.That(faultingHost.Drawable, Is.TypeOf<BmsPublishedNotePendingDrawable>());
            });
            AddStep("wait for exact visual fault callback", () =>
            {
                Assert.That(
                    SpinWait.SpinUntil(
                        () => faultingHost.PendingLoadTask?.IsCompleted == true && callbackScheduler.HasPendingTasks,
                        TimeSpan.FromSeconds(10)),
                    Is.True);
            });
            AddStep("surface exact visual fault without fallback", () =>
            {
                Assert.Throws<InvalidOperationException>(() => callbackScheduler.Update());
                callbackScheduler.Update();

                Assert.Multiple(() =>
                {
                    Assert.That(faultingHost.Drawable, Is.TypeOf<BmsPublishedNotePendingDrawable>());
                    Assert.That(faultingHost.ChildrenOfType<DefaultBmsNoteDisplay>(), Is.Empty);
                });
            });
            AddStep("remove exact fault host", () =>
                Assert.That(renderer.RemoveBmsAuxiliary(faultingHost), Is.True));
            AddStep("detach exact fault renderer", () => renderer.Expire());
            AddUntilStep("wait for exact fault renderer detach", () => renderer.Parent == null);
        }

        [Test]
        public void TestSameIdStaleLayoutContentRevisionCannotBindNewSelectedMaterial()
        {
            var context = new CurrentRevisionProductContext();
            ExactLayoutJourneyHost renderer = null!;
            SkinCurrentRevision? staleRevision = null;

            addSelectLayoutRevisionA(context, external: false);
            AddStep("mount exact current BMS renderer", () =>
            {
                Add(renderer = new ExactLayoutJourneyHost(manager));
                renderer.ShowBms();
            });
            AddUntilStep("wait for exact current publication", () => renderer.BmsReady);
            AddStep("reject stale same-ID layout token against new selected package", () =>
            {
                SkinCurrentRevision current = manager.CurrentRevision;
                staleRevision = new SkinCurrentRevision(
                    current.Generation,
                    current.RecordId,
                    $"stale-{current.ContentRevision}",
                    current.SourceKind,
                    current.Owner,
                    current.KeepsReusableOwner,
                    _ => { });
                GameplaySkinPackageRevision stalePackage = GameplaySkinPackageRevision.Create(staleRevision);
                var staleOwner = new GameplaySkinLayoutRevisionOwner(
                    stalePackage,
                    validateRoot: () => true,
                    acquireWorkLease: staleRevision.AcquireWorkLease,
                    captureParticipantGeneration: () => 0,
                    validateParticipantGeneration: _ => true,
                    commitAtParticipantGeneration: (_, commit) =>
                    {
                        commit();
                        return true;
                    },
                    dispatchCommit: commit =>
                    {
                        commit();
                        return true;
                    });

                Assert.That(() => BmsGameplayLayoutProvider.TryPrepareExact(
                    staleOwner,
                    renderer.BmsBeatmap,
                    BmsPlayfieldStyle.P1,
                    manager.CurrentSkin.Value,
                    null,
                    null,
                    CancellationToken.None,
                    out _),
                    Throws.TypeOf<InvalidOperationException>()
                          .With.Message.EqualTo("The selected BMS package content revision does not match its exact layout publication."));
                Assert.Multiple(() =>
                {
                    Assert.That(staleOwner.CurrentPublication, Is.Null);
                    Assert.That(renderer.BmsLayoutProbe.Publication, Is.SameAs(renderer.BmsDrawable.LayoutProvider.RevisionOwner!.CurrentPublication));
                    Assert.That(renderer.BmsLayoutProbe.Publication!.MaterialSet.PackageRevision.ContentRevision, Is.EqualTo(current.ContentRevision));
                });
            });
            AddStep("release stale same-ID revision token", () => staleRevision!.ReleaseManagerLease());
            AddStep("detach current renderer after stale rejection", () => renderer.Expire());
            AddUntilStep("wait for current renderer detach", () => renderer.Parent == null);
        }

        [Test]
        public void TestExactMaterialPreparationRejectsForeignSameIdentityOwnerAndSelectsRetainedOwner()
        {
            var context = new CurrentRevisionProductContext();
            ExactLayoutJourneyHost renderer = null!;
            ForeignIdentityBmsLegacySkin foreign = null!;

            addSelectLayoutRevisionA(context, external: false);
            AddStep("mount exact BMS renderer for retained-owner proof", () =>
            {
                Add(renderer = new ExactLayoutJourneyHost(manager));
                renderer.ShowBms();
            });
            AddUntilStep("wait for exact retained-owner publication", () => renderer.BmsReady);
            AddStep("prefer retained owner after foreign same-identity source", () =>
            {
                SkinCurrentRevision current = manager.CurrentRevision;
                GameplaySkinLayoutPublication publication = renderer.BmsLayoutProbe.Publication!;
                BmsGameplayLayoutSnapshot layout = publication.GetAdapter<BmsGameplayLayoutSnapshot>();
                foreign = new ForeignIdentityBmsLegacySkin(current.RecordId, current.ContentRevision);
                var aggregate = new ExactSourceChain(foreign, current.Owner);

                using GameplaySkinLayoutPublication preparedPublication = BmsGameplayResolvedNoteMaterialPreparer.Prepare(
                    aggregate,
                    layout,
                    CancellationToken.None);
                GameplaySkinResolvedMaterialSet prepared = preparedPublication.MaterialSet;

                Assert.Multiple(() =>
                {
                    Assert.That(layout.Neutral.Context.PackageRevision.RetainsExactSource(current.Owner), Is.True);
                    Assert.That(layout.Neutral.Context.PackageRevision.RetainsExactSource(foreign), Is.False);
                    Assert.That(prepared.PackageRevision, Is.SameAs(publication.MaterialSet.PackageRevision));
                    Assert.That(prepared.Entries, Is.Not.Empty);
                    Assert.That(prepared.Entries.Any(entry =>
                        entry.Source.Kind == GameplaySkinResolvedMaterialSourceKind.SelectedPackage), Is.True);
                });

                aggregate.Dispose();
            });
            AddStep("reject foreign-only same-identity source", () =>
            {
                BmsGameplayLayoutSnapshot layout = renderer.BmsLayoutProbe.Publication!.GetAdapter<BmsGameplayLayoutSnapshot>();
                var foreignOnly = new ExactSourceChain(foreign);

                Assert.That(
                    () => BmsGameplayResolvedNoteMaterialPreparer.Prepare(foreignOnly, layout, CancellationToken.None),
                    Throws.TypeOf<InvalidOperationException>()
                          .With.Message.EqualTo("The BMS material source chain contains only a foreign same-identity package owner."));

                foreignOnly.Dispose();
            });
            AddStep("detach retained-owner renderer", () => renderer.Expire());
            AddUntilStep("wait for retained-owner renderer detach", () => renderer.Parent == null);
            AddStep("dispose foreign identity owner", () => foreign.Dispose());
        }

        [Test]
        public void TestInvalidCurrentBmsTargetKeepsCatalogDiagnosticWithoutBorrowingLaneKey()
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;

            AddStep("create package with irrelevant mania error and invalid BMS target", () =>
            {
                (_, candidate) = createCandidate(writePublicationTargetDiagnosticPackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for publication-target diagnostic package", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && manager.CurrentSkin.Value.PackageContentRevision != null);
            AddStep("mount exact BMS renderer for document diagnostics", () =>
            {
                Add(renderer = new ExactLayoutJourneyHost(manager));
                renderer.ShowBms();
            });
            AddUntilStep("wait for exact diagnostic material publication", () => renderer.BmsReady);
            AddStep("assert BMS diagnostic has catalog identity but no fabricated target", () =>
            {
                GameplaySkinResolvedMaterialDiagnostic[] codecDiagnostics = renderer.BmsLayoutProbe.Publication!.MaterialSet.Diagnostics
                    .Where(diagnostic => diagnostic.Code.StartsWith("OMS-SKIN-CODEC-", StringComparison.Ordinal))
                    .ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(codecDiagnostics, Has.Length.EqualTo(1),
                        "A declaration selected only for mania must not become a BMS publication diagnostic.");
                    Assert.That(codecDiagnostics[0].Code, Is.EqualTo("OMS-SKIN-CODEC-021"));
                    Assert.That(codecDiagnostics[0].Key, Is.Null,
                        "An invalid stable ID/index must not borrow the first BMS Note lane as its diagnostic target.");
                    Assert.That(codecDiagnostics[0].CatalogSlotId, Is.EqualTo(GameplaySkinSlotCatalog.Note.Id));
                    Assert.That(codecDiagnostics[0].SourceKind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.SelectedPackage));
                });
            });
            AddStep("detach diagnostic renderer", () => renderer.Expire());
            AddUntilStep("wait for diagnostic renderer detach", () => renderer.Parent == null);
        }

        [TestCase(MaterialDiagnosticPackageSource.OrdinaryRealm)]
        [TestCase(MaterialDiagnosticPackageSource.ManagedFolder)]
        [TestCase(MaterialDiagnosticPackageSource.ExternalFolder)]
        public void TestInvalidPublicDeclarationLogsOneRedactedDiagnosticFromExactCurrentRevision(MaterialDiagnosticPackageSource source)
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;
            string packageRoot = string.Empty;
            Task<bool>? registrationTask = null;
            Task<IList<Live<SkinInfo>>>? dropdownTask = null;
            SkinCurrentRevision revision = null!;
            string configurationRevision = string.Empty;
            var logEntries = new ConcurrentQueue<LogEntry>();

            void capture(LogEntry entry)
            {
                if (entry.Message.StartsWith("Gameplay skin material diagnostic:", StringComparison.Ordinal))
                    logEntries.Enqueue(entry);
            }

            switch (source)
            {
                case MaterialDiagnosticPackageSource.OrdinaryRealm:
                    AddStep("create ordinary Realm diagnostic package", () =>
                    {
                        packageRoot = LocalStorage.GetFullPath($"realm-material-diagnostic-{Guid.NewGuid():N}");
                        writeLoggedInvalidDiagnosticPackage(packageRoot);
                        candidate = createRealmRevisionCandidate(packageRoot);
                        manager.CurrentSkinInfo.Value = candidate;
                    });
                    break;

                case MaterialDiagnosticPackageSource.ManagedFolder:
                    AddStep("create managed diagnostic package", () =>
                    {
                        (packageRoot, candidate) = createCandidate(
                            writeLoggedInvalidDiagnosticPackage,
                            typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                        manager.CurrentSkinInfo.Value = candidate;
                    });
                    break;

                case MaterialDiagnosticPackageSource.ExternalFolder:
                    AddStep("create and register external diagnostic package", () =>
                    {
                        packageRoot = createExternalPackage(writeLoggedInvalidDiagnosticPackage);
                        registrationTask = manager.RegisterExternalFolderAsync(packageRoot);
                    });
                    AddUntilStep("wait for external diagnostic registration", () => registrationTask?.IsCompleted == true);
                    AddStep("query external diagnostic candidate", () =>
                    {
                        Assert.That(registrationTask!.GetAwaiter().GetResult(), Is.True);
                        dropdownTask = manager.GetAllUsableSkinsAsync();
                    });
                    AddUntilStep("wait for external diagnostic candidate", () => dropdownTask?.IsCompleted == true);
                    AddStep("select external diagnostic package", () =>
                    {
                        candidate = dropdownTask!.GetAwaiter().GetResult()
                                                .Single(record => record.PerformRead(info =>
                                                    info.IsExternalFilesystemStorage
                                                    && string.Equals(info.FilesystemStoragePath, packageRoot, StringComparison.OrdinalIgnoreCase)));
                        manager.CurrentSkinInfo.Value = candidate;
                    });
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown diagnostic package source.");
            }

            AddUntilStep("wait for exact diagnostic current revision", () =>
                candidate != null
                && manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && manager.CurrentSkin.Value is BmsLegacySkin
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("subscribe product diagnostic sink and mount BMS renderer", () =>
            {
                revision = manager.CurrentRevision;
                configurationRevision = ((BmsLegacySkin)revision.Owner).CaptureManagedPackageSourceRevision().ParsedConfigurationContentHash!;
                Logger.NewEntry += capture;
                Add(renderer = new ExactLayoutJourneyHost(manager));
                renderer.ShowBms();
            });
            AddUntilStep("wait for exact diagnostic publication and log", () => renderer.BmsReady && !logEntries.IsEmpty);
            AddStep("unsubscribe and assert one persistence-safe diagnostic", () =>
            {
                Logger.NewEntry -= capture;
                LogEntry[] entries = logEntries.ToArray();

                Assert.That(entries, Has.Length.EqualTo(1));
                string message = entries[0].Message;

                Assert.Multiple(() =>
                {
                    Assert.That(entries[0].Level, Is.EqualTo(LogLevel.Important));
                    Assert.That(message, Does.StartWith("Gameplay skin material diagnostic: count=1"));
                    Assert.That(message, Does.Contain("code=OMS-SKIN-CODEC-021"));
                    Assert.That(message.Split("OMS-SKIN-CODEC-021", StringSplitOptions.None), Has.Length.EqualTo(2),
                        "The exact owner must deduplicate one public diagnostic within its committed batch.");
                    Assert.That(message, Does.Contain("slot=object.note"));
                    Assert.That(message, Does.Contain("target=document"));
                    Assert.That(message, Does.Contain("source=SelectedPackage"));
                    Assert.That(message, Does.Not.Contain(diagnostic_sensitive_resource));
                    Assert.That(message, Does.Not.Contain("secret-note-resource"));
                    Assert.That(message, Does.Not.Contain(packageRoot));
                    Assert.That(message, Does.Not.Contain(@"C:\"));
                    Assert.That(message, Does.Not.Contain("C:/"));
                    Assert.That(message, Does.Not.Contain(candidate.ID.ToString("D")));
                    Assert.That(message, Does.Not.Contain(candidate.ID.ToString("N")));
                    Assert.That(message, Does.Not.Contain(revision.ContentRevision));
                    Assert.That(message, Does.Not.Contain(configurationRevision));
                });
            });
            AddStep("detach diagnostic-log renderer", () => renderer.Expire());
            AddUntilStep("wait for diagnostic-log renderer detach", () => renderer.Parent == null);
        }

        [Test]
        public void TestPublicKeyVisualDeclarationReportsUnsupportedBmsRuntimeCapability()
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;

            AddStep("create package with public but unhosted BMS key visual", () =>
            {
                (_, candidate) = createCandidate(writeUnsupportedBmsCapabilityPackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for unsupported-capability package", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && manager.CurrentSkin.Value.PackageContentRevision != null);
            AddStep("mount exact BMS renderer for capability diagnostic", () =>
            {
                Add(renderer = new ExactLayoutJourneyHost(manager));
                renderer.ShowBms();
            });
            AddUntilStep("wait for exact capability material publication", () => renderer.BmsReady);
            AddStep("assert catalog eligibility remains separate from BMS runtime support", () =>
            {
                GameplaySkinResolvedMaterialDiagnostic[] capabilityDiagnostics = renderer.BmsLayoutProbe.Publication!.MaterialSet.Diagnostics
                    .Where(diagnostic => diagnostic.Code == "bms.capability.unsupported-slot")
                    .ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(GameplaySkinSlotCatalog.All, Does.Contain(GameplaySkinSlotCatalog.KeyVisual));
                    Assert.That(BmsManagedPackageNoteMaterializer.RuntimeCapabilities.TryGet(GameplaySkinSlotCatalog.KeyVisual, out _), Is.False);
                    Assert.That(capabilityDiagnostics, Has.Length.EqualTo(1));
                    Assert.That(capabilityDiagnostics[0].Key, Is.Not.Null);
                    Assert.That(capabilityDiagnostics[0].Key!.Slot, Is.SameAs(GameplaySkinSlotCatalog.KeyVisual));
                    Assert.That(capabilityDiagnostics[0].CatalogSlotId, Is.EqualTo(GameplaySkinSlotCatalog.KeyVisual.Id));
                    Assert.That(capabilityDiagnostics[0].SourceKind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.SelectedPackage));
                });
            });
            AddStep("detach capability renderer", () => renderer.Expire());
            AddUntilStep("wait for capability renderer detach", () => renderer.Parent == null);
        }

        [Test]
        public void TestPortableManiaKeyVisualDeclarationDoesNotReportBmsCapabilityDiagnostic()
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;

            AddStep("create package with mania-selected public key visual", () =>
            {
                (_, candidate) = createCandidate(writePortableManiaCapabilityPackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for portable mania declaration package", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && manager.CurrentSkin.Value.PackageContentRevision != null);
            AddStep("mount exact BMS renderer below mania-selected declaration", () =>
            {
                Add(renderer = new ExactLayoutJourneyHost(manager));
                renderer.ShowBms();
            });
            AddUntilStep("wait for exact BMS publication with portable declaration", () => renderer.BmsReady);
            AddStep("assert non-applicable selector emitted no BMS capability diagnostic", () =>
            {
                GameplaySkinDocument document = manager.CurrentSkin.Value.GameplaySkinDocument;
                GameplaySkinResolvedMaterialDiagnostic[] capabilityDiagnostics = renderer.BmsLayoutProbe.Publication!.MaterialSet.Diagnostics
                    .Where(diagnostic => diagnostic.Code == "bms.capability.unsupported-slot")
                    .ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(document.Sections.SelectMany(section => section.Entries)
                                        .Count(entry => ReferenceEquals(entry.Descriptor, GameplaySkinSlotCatalog.KeyVisual)), Is.EqualTo(1));
                    Assert.That(capabilityDiagnostics, Is.Empty);
                });
            });
            AddStep("detach portable-selector renderer", () => renderer.Expire());
            AddUntilStep("wait for portable-selector renderer detach", () => renderer.Parent == null);
        }

        private static void writeTailSuppressPackage(string root)
        {
            writeLayoutRevisionPackage(root, "A", new Rgba32(240, 40, 80, 255));
            File.AppendAllText(
                Path.Combine(root, "skin.ini"),
                "\n[GameplaySkin.Common:1]\n" +
                "Target: Lane ruleset=bms keymode=7k stage-mode=single group=bms.group.deck-1 lane=bms.lane.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=1 group-local-visual=1\n" +
                "object.long-note.tail: resource Suppress\n");
        }

        private static void writePublicCommonFiveKeyNotePackage(string root)
        {
            string notes = Path.Combine(root, "notes");
            Directory.CreateDirectory(notes);
            File.WriteAllText(
                Path.Combine(root, "skin.ini"),
                "[General]\n" +
                "Name: public common five-key product\n" +
                "Author: OMS tests\n" +
                "Version: 2.7\n" +
                "\n" +
                "[Bms]\n" +
                "Keymode: 5K\n" +
                "\n" +
                "[GameplaySkin.Common:1]\n" +
                "Target: Lane ruleset=bms keymode=5k stage-mode=single group=bms.group.deck-1 lane=bms.lane.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=1 group-local-visual=1\n" +
                "object.note: resource Provide \"notes/public-note\"\n");

            using var image = new Image<Rgba32>(11, 13, new Rgba32(25, 215, 165, 255));
            using Stream output = File.Create(Path.Combine(notes, "public-note.png"));
            image.SaveAsPng(output);
        }

        private static void writePublicationTargetDiagnosticPackage(string root)
        {
            writeLayoutRevisionPackage(root, "A", new Rgba32(240, 40, 80, 255));
            File.AppendAllText(
                Path.Combine(root, "skin.ini"),
                "\n[GameplaySkin.Common:1]\n" +
                "Target: Lane ruleset=mania keymode=4k stage-mode=single group=mania.group.stage-1 lane=mania.lane.key-1 group-logical=0 group-visual=0 global-logical=0 global-visual=0 group-local-logical=0 group-local-visual=0\n" +
                "object.note: resource Bogus\n" +
                "Target: Lane ruleset=bms keymode=7k stage-mode=single group=bms.group.deck-1 lane=bms.lane.key-1 group-logical=0 group-visual=0 global-logical=999 global-visual=1 group-local-logical=1 group-local-visual=1\n" +
                "object.note: resource Provide \"notes/note\"\n");
        }

        private static void writeUnsupportedBmsCapabilityPackage(string root)
        {
            writeLayoutRevisionPackage(root, "A", new Rgba32(240, 40, 80, 255));
            File.AppendAllText(
                Path.Combine(root, "skin.ini"),
                "\n[GameplaySkin.Common:1]\n" +
                "Target: Lane ruleset=bms keymode=7k stage-mode=single group=bms.group.deck-1 lane=bms.lane.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=1 group-local-visual=1\n" +
                "playfield.key: resource Provide \"notes/note\"\n");
        }

        private const string diagnostic_sensitive_resource = "C:/private-author/secret-note-resource.png";

        private static void writeLoggedInvalidDiagnosticPackage(string root)
        {
            writeLayoutRevisionPackage(root, "A", new Rgba32(240, 40, 80, 255));
            File.AppendAllText(
                Path.Combine(root, "skin.ini"),
                "\n[GameplaySkin.Common:1]\n" +
                "Target: Lane ruleset=bms keymode=7k stage-mode=single group=bms.group.deck-1 lane=bms.lane.key-1 group-logical=0 group-visual=0 global-logical=999 global-visual=1 group-local-logical=1 group-local-visual=1\n" +
                $"object.note: resource Provide \"{diagnostic_sensitive_resource}\"\n");
        }

        private static void writePortableManiaCapabilityPackage(string root)
        {
            writeLayoutRevisionPackage(root, "A", new Rgba32(240, 40, 80, 255));
            File.AppendAllText(
                Path.Combine(root, "skin.ini"),
                "\n[GameplaySkin.Common:1]\n" +
                "Target: Lane ruleset=mania keymode=4k stage-mode=single group=mania.group.stage-1 lane=mania.lane.key-1 group-logical=0 group-visual=0 global-logical=0 global-visual=0 group-local-logical=0 group-local-visual=0\n" +
                "playfield.key: resource Provide \"notes/note\"\n");
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
            GameplaySkinLayoutPublication publication = renderer.BmsLayoutProbe.Publication!;
            BmsGameplayLayoutSnapshot snapshot = renderer.BmsDrawable.LayoutSnapshot;
            GameplaySkinResolvedMaterialSet materialSet = publication.MaterialSet;
            DrawableBmsHitObject[] notes = renderer.BmsDrawable.ChildrenOfType<DrawableBmsHitObject>().ToArray();
            BmsManagedPackageSourceRevision? selectedSourceRevision =
                (revision.Owner as BmsLegacySkin)?.CaptureManagedPackageSourceRevision();

            Assert.Multiple(() =>
            {
                Assert.That(publication.GetAdapter<BmsGameplayLayoutSnapshot>(), Is.SameAs(snapshot));
                Assert.That(materialSet.Entries, Is.Not.Empty, "A production BMS publication must resolve every hosted note material during prepare.");
                Assert.That(materialSet.Snapshot, Is.SameAs(snapshot.Neutral));
                Assert.That(selectedSourceRevision, Is.Not.Null);
                Assert.That(selectedSourceRevision!.SkinId, Is.EqualTo(revision.RecordId));
                Assert.That(selectedSourceRevision.PackageContentRevision, Is.EqualTo(revision.ContentRevision));
                Assert.That(renderer.BmsDrawable.ResolvedMaterialSet, Is.SameAs(materialSet));
                Assert.That(renderer.BmsDrawable.LayoutProvider.CurrentMaterialSet, Is.SameAs(materialSet));
                Assert.That(renderer.BmsDrawable.Playfield.LayoutSnapshot, Is.SameAs(snapshot));
                Assert.That(renderer.BmsDrawable.Playfield.ResolvedMaterialSet, Is.SameAs(materialSet));
                Assert.That(renderer.BmsDrawable.Playfield.GroupContainers.All(group => ReferenceEquals(group.LayoutSnapshot, snapshot)), Is.True);
                Assert.That(renderer.BmsDrawable.Playfield.Lanes.All(lane => ReferenceEquals(lane.LayoutSnapshot, snapshot)), Is.True);
                Assert.That(notes, Is.Not.Empty);
                Assert.That(notes.All(note => ReferenceEquals(note.ExactMaterialSet, materialSet)), Is.True);
                Assert.That(renderer.BmsDrawable.PreStartSpeedPreviewMaterialSet, Is.SameAs(materialSet));
                Assert.That(renderer.BmsDrawable.BgaMaterialSet, Is.SameAs(materialSet));
                Assert.That(renderer.BmsDrawable.HudMaterialSet, Is.SameAs(materialSet));
                Assert.That(renderer.BmsDrawable.GaugeMaterialSet, Is.SameAs(materialSet));
                Assert.That(renderer.BmsDrawable.ComboMaterialSet, Is.SameAs(materialSet));
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
                Assert.That(layoutB.MaterialSet, Is.Not.SameAs(layoutA.MaterialSet));
                Assert.That(layoutA.MaterialSet.Snapshot, Is.SameAs(layoutA.Snapshot));
                Assert.That(layoutB.MaterialSet.Snapshot, Is.SameAs(layoutB.Snapshot));
                Assert.That(layoutA.MaterialSet.PackageRevision, Is.SameAs(layoutA.Snapshot.Context.PackageRevision));
                Assert.That(layoutB.MaterialSet.PackageRevision, Is.SameAs(layoutB.Snapshot.Context.PackageRevision));
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
            private readonly Container bmsAuxiliaryHost;

            public BmsBeatmap BmsBeatmap { get; }

            public DrawableBmsRuleset BmsDrawable { get; }

            public DrawableManiaRuleset ManiaDrawable { get; }

            public RulesetSkinProvidingContainer BmsProvider { get; }

            public RulesetSkinProvidingContainer ManiaProvider { get; }

            public GameplayLayoutPublicationProbe BmsLayoutProbe { get; }

            public GameplayLayoutPublicationProbe ManiaLayoutProbe { get; }

            public bool BmsReady => BmsLayoutProbe.Publication != null
                                    && BmsDrawable.IsLoaded
                                    && BmsDrawable.PreStartSpeedPreviewMaterialSet != null
                                    && BmsDrawable.BgaMaterialSet != null
                                    && BmsDrawable.HudMaterialSet != null
                                    && BmsDrawable.GaugeMaterialSet != null
                                    && BmsDrawable.ComboMaterialSet != null
                                    && BmsDrawable.ChildrenOfType<DrawableBmsHitObject>().Any()
                                    && BmsDrawable.Playfield.GroupContainers.All(group => group.IsLoaded)
                                    && BmsDrawable.Playfield.Lanes.All(lane => lane.IsLoaded);

            public bool ManiaReady => ManiaLayoutProbe.Publication != null
                                      && ManiaDrawable.IsLoaded
                                      && ManiaDrawable.Playfield.Stages.All(stage =>
                                          stage.IsLoaded && stage.Columns.All(column => column.IsLoaded));

            public ExactLayoutJourneyHost(
                SkinManager skinManager,
                ISkin? bmsBeatmapSkin = null,
                BmsRuleset? bmsRulesetOverride = null,
                bool useFiveKeyBeatmap = false)
                : base(skinManager.CurrentSkin.Value)
            {
                this.skinManager = skinManager;
                RelativeSizeAxes = Axes.Both;

                BmsRuleset bmsRuleset = bmsRulesetOverride ?? new BmsRuleset();
                bmsRulesetConfig = new BmsRulesetConfigManager(null, bmsRuleset.RulesetInfo);
                scoreProcessor = bmsRuleset.CreateScoreProcessor();
                healthProcessor = bmsRuleset.CreateHealthProcessor(0);
                string bmsText = useFiveKeyBeatmap
                    ? @"
#TITLE Current revision public material
#BPM 120
#WAV01 note.wav
#00111:0100
#00112:0100
#00113:0100
#00114:0100
#00115:0100
"
                    : @"
#TITLE Current revision layout product
#BPM 120
#LNTYPE 1
#WAV01 note.wav
#WAV02 hold.wav
#00111:0100
#00112:0100
#00119:0001
                #00151:02000200
";
                string bmsFilename = useFiveKeyBeatmap
                    ? "current-revision-public-material.bms"
                    : "current-revision-layout.bme";
                var decoded = new BmsBeatmapDecoder().DecodeText(bmsText, bmsFilename);
                BmsBeatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decoded), bmsRuleset).Convert();
                BmsBeatmap bmsBeatmap = BmsBeatmap;
                healthProcessor.ApplyBeatmap(bmsBeatmap);
                scoreProcessor.ApplyBeatmap(bmsBeatmap);
                BmsDrawable = (DrawableBmsRuleset)bmsRuleset.CreateDrawableRulesetWith(bmsBeatmap);
                BmsLayoutProbe = new GameplayLayoutPublicationProbe();
                BmsProvider = new RulesetSkinProvidingContainer(
                    bmsRuleset,
                    bmsBeatmap,
                    bmsBeatmapSkin,
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
                            bmsAuxiliaryHost = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                            },
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

            public void AddBmsAuxiliary(Drawable drawable) => bmsAuxiliaryHost.Add(drawable);

            public bool RemoveBmsAuxiliary(Drawable drawable) => bmsAuxiliaryHost.Remove(drawable, disposeImmediately: true);

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

        private sealed class ResourceLegacyBeatmapSkin : LegacyBeatmapSkin
        {
            private readonly Texture texture;

            public ResourceLegacyBeatmapSkin(Texture texture)
                : base(new BeatmapInfo { LocalFilePath = "legacy-direct.bme" }, null)
            {
                this.texture = texture ?? throw new ArgumentNullException(nameof(texture));
            }

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT)
                // LegacySkinExtensions probes animated resources as "<name>-0", "<name>-1", ... until the
                // first miss. This fixture represents static beatmap-local resources, so claiming every prefixed
                // animation frame would make the production material preparer enumerate an unbounded sequence.
                => componentName.StartsWith("mania-note", StringComparison.Ordinal)
                   && componentName.IndexOf('-', "mania-note".Length) < 0
                    ? texture
                    : base.GetTexture(componentName, wrapModeS, wrapModeT);
        }

        private sealed class ForeignIdentityBmsLegacySkin : BmsLegacySkin
        {
            public ForeignIdentityBmsLegacySkin(Guid recordId, string contentRevision)
                : base(
                    new SkinInfo("foreign same-identity owner", "OMS tests", typeof(BmsLegacySkin).GetInvariantInstantiationInfo())
                    {
                        ID = recordId,
                        Hash = contentRevision,
                    },
                    resources: null,
                    fallbackStore: null,
                    configurationFilename: "skin.ini")
            {
            }
        }

        private sealed partial class ExactSourceChain : SkinProvidingContainer
        {
            public ExactSourceChain(params ISkin[] sources)
            {
                SetSources(sources);
            }
        }

        public enum MaterialDiagnosticPackageSource
        {
            OrdinaryRealm = 0,
            ManagedFolder = 1,
            ExternalFolder = 2,
        }

        private sealed class NoteResourceBmsRuleset : BmsRuleset
        {
            private readonly byte[] noteTexture;

            public NoteResourceBmsRuleset()
                : this(Array.Empty<byte>())
            {
                // RulesetInfo creates a parameterless instance only to query bindings. The production material fixture
                // uses the explicit texture constructor below.
            }

            public NoteResourceBmsRuleset(byte[] noteTexture)
            {
                this.noteTexture = noteTexture ?? throw new ArgumentNullException(nameof(noteTexture));
            }

            public override IResourceStore<byte[]> CreateResourceStore()
                => new NoteResourceStore(noteTexture);
        }

        private sealed class CancellationProbeBmsRuleset : BmsRuleset, IGameplaySkinLayoutPreparer
        {
            public BlockingNoteResourceStore Resources { get; }

            public GameplaySkinLayoutRevisionOwner? RevisionOwner { get; private set; }

            public CancellationToken PreparationToken { get; private set; }

            public bool PrepareEntered { get; private set; }

            public CancellationProbeBmsRuleset()
                : this(Array.Empty<byte>())
            {
                // RulesetInfo may create a parameterless instance only to query bindings. It must never inherit the
                // product fixture's blocking resource gate.
                Resources.Release.Set();
            }

            public CancellationProbeBmsRuleset(byte[] noteTexture)
            {
                Resources = new BlockingNoteResourceStore(noteTexture);
            }

            public override IResourceStore<byte[]> CreateResourceStore() => Resources;

            GameplaySkinLayoutPreparationResult IGameplaySkinLayoutPreparer.PrepareGameplaySkinLayout(
                IBeatmap beatmap,
                IReadOnlyDependencyContainer dependencies,
                CancellationToken cancellationToken)
            {
                PrepareEntered = true;
                PreparationToken = cancellationToken;
                RevisionOwner = dependencies.Get<GameplaySkinLayoutRevisionOwner>();
                return PrepareGameplaySkinLayout(beatmap, dependencies, cancellationToken);
            }
        }

        private sealed class BlockingNoteResourceStore : IResourceStore<byte[]>
        {
            private static readonly string[] resource_names =
            {
                "Textures/mania-note1.png",
                "Textures/mania-note2.png",
            };

            private readonly byte[] noteTexture;
            private int entered;

            public ManualResetEventSlim Entered { get; } = new ManualResetEventSlim();

            public ManualResetEventSlim Release { get; } = new ManualResetEventSlim();

            public BlockingNoteResourceStore(byte[] noteTexture)
            {
                this.noteTexture = noteTexture ?? throw new ArgumentNullException(nameof(noteTexture));
            }

            public byte[] Get(string name)
            {
                if (!resource_names.Contains(name, StringComparer.Ordinal))
                    return null!;

                waitForRelease();
                return noteTexture;
            }

            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(Get(name));
            }

            public Stream? GetStream(string name)
            {
                if (!resource_names.Contains(name, StringComparer.Ordinal))
                    return null;

                waitForRelease();
                return new MemoryStream(noteTexture, writable: false);
            }

            public IEnumerable<string> GetAvailableResources() => resource_names;

            public void Dispose() => Release.Set();

            private void waitForRelease()
            {
                if (Interlocked.Exchange(ref entered, 1) == 0)
                    Entered.Set();

                if (!Release.Wait(TimeSpan.FromSeconds(30)))
                    throw new TimeoutException("The BMS material cancellation fixture was not released.");
            }
        }

        private sealed class NoteResourceStore : IResourceStore<byte[]>
        {
            private static readonly string[] resource_names =
            {
                "Textures/mania-note1.png",
                "Textures/mania-note2.png",
            };

            private readonly byte[] noteTexture;

            public NoteResourceStore(byte[] noteTexture)
            {
                this.noteTexture = noteTexture;
            }

            public byte[] Get(string name) => resource_names.Contains(name, StringComparer.Ordinal) ? noteTexture : null!;

            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default)
                => Task.FromResult(Get(name));

            public Stream? GetStream(string name)
                => resource_names.Contains(name, StringComparer.Ordinal) ? new MemoryStream(noteTexture, writable: false) : null;

            public IEnumerable<string> GetAvailableResources() => resource_names;

            public void Dispose()
            {
            }
        }
    }
}
