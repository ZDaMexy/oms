// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using oms.Input;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Testing;
using osu.Framework.Threading;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
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
        public void TestInvalidSameIdBSceneKeepsExactAWhileAbsentScenePublishes(bool external)
        {
            var context = new CurrentRevisionProductContext();
            FullSkinSettingsCallerHost caller = null!;
            ExactLayoutJourneyHost rendererA = null!;
            ExactLayoutJourneyHost lateRenderer = null!;
            ExactLayoutJourneyHost absentRenderer = null!;
            SkinRevisionParticipantRegistration sceneParticipant = null!;
            SkinCurrentRevision revisionA = null!;
            SkinCurrentRevision absentRevision = null!;
            GameplaySkinLayoutPublication publicationA = null!;
            int prepareCount = 0;
            int scenePrepareCount = 0;
            int retiredA = 0;
            var scenePrepareFailures = new ConcurrentQueue<Exception>();

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
                Assert.That(publicationA.PreparedScene.HasAuthorScene, Is.True);
            });
            AddStep("detach A renderer before reload attempt", () => rendererA.Expire());
            AddUntilStep("wait for A renderer detach before failed B", () => rendererA.Parent == null);
            AddStep("register exact production scene prepare participant", () =>
            {
                BmsBeatmap beatmap = rendererA.BmsBeatmap;
                sceneParticipant = manager.RegisterRevisionParticipant(
                    SkinRevisionParticipantKind.CoherentVisualConsumer,
                    "exact BMS scene validation participant",
                    prepareCommit: (nextRevision, cancellationToken) => Task.Run(() =>
                    {
                        Interlocked.Increment(ref scenePrepareCount);
                        try
                        {
                            using GameplaySkinLayoutPublication ignored = prepareExactBmsPublication(nextRevision, beatmap, cancellationToken);
                        }
                        catch (Exception exception)
                        {
                            scenePrepareFailures.Enqueue(exception);
                            throw;
                        }

                        return new SkinRevisionParticipantCommit(() => { }, () => { });
                    }, cancellationToken),
                    affectsGameplayLayoutPublication: true);
            });
            AddUntilStep("wait for failed-B reload affordance", () => caller.ReloadCurrentButton.Enabled.Value);
            AddStep("write invalid B scene manifest and invoke same-ID reload", () =>
            {
                writeInvalidAuthorSceneManifest(context.PackageRoot);
                caller.ReloadCurrentButton.TriggerClick();
            });
            AddUntilStep("wait for invalid B scene prepare failure", () =>
                prepareCount == 1 && scenePrepareCount == 1 && caller.ReloadCurrentButton.Enabled.Value);
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
                    Assert.That(late.PreparedScene.HasAuthorScene, Is.True);
                    Assert.That(late.PreparedScene.ContentRevision, Is.EqualTo(publicationA.PreparedScene.ContentRevision));
                    Assert.That(late.PreparedScene.Snapshot, Is.SameAs(late.Snapshot));
                    Assert.That(late.PreparedScene.MaterialSet, Is.SameAs(late.MaterialSet));
                    Assert.That(
                        late.MaterialSet.Entries.Select(entry => (entry.Key, entry.State, entry.Source)),
                        Is.EqualTo(publicationA.MaterialSet.Entries.Select(entry => (entry.Key, entry.State, entry.Source))));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(retiredA, Is.Zero);
                });
            });
            AddStep("detach late A renderer", () => lateRenderer.Expire());
            AddUntilStep("wait for late A renderer detach", () => lateRenderer.Parent == null);
            AddStep("remove invalid manifest and retry same-ID B as explicit absent scene", () =>
            {
                File.Delete(Path.Combine(context.PackageRoot, GameplaySkinSceneContracts.MANIFEST_FILE_NAME));
                File.Delete(Path.Combine(context.PackageRoot, GameplaySkinSceneContracts.SCENE_FILE_NAME));
                caller.ReloadCurrentButton.TriggerClick();
            });
            AddUntilStep("wait for absent-scene B prepare completion", () =>
                prepareCount >= 2
                && scenePrepareCount >= 2
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("assert absent-scene B committed", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(prepareCount, Is.EqualTo(2));
                    Assert.That(scenePrepareCount, Is.EqualTo(2));
                    Assert.That(scenePrepareFailures, Has.Count.EqualTo(1),
                        string.Join(Environment.NewLine, scenePrepareFailures.Select(failure => failure.ToString())));
                    Assert.That(manager.CurrentRevision, Is.Not.SameAs(revisionA));
                });
            });
            AddStep("mount renderer for accepted absent scene", () =>
            {
                absentRevision = manager.CurrentRevision;
                Add(absentRenderer = new ExactLayoutJourneyHost(manager));
                absentRenderer.ShowBms();
            });
            AddUntilStep("wait for absent-scene renderer", () => absentRenderer.BmsReady);
            AddStep("assert absent scene is an empty exact publication, not invalid fallback", () =>
            {
                GameplaySkinLayoutPublication absent = absentRenderer.BmsLayoutProbe.Publication!;
                assertExactBmsLayoutTree(absentRenderer, absentRevision);

                Assert.Multiple(() =>
                {
                    Assert.That(absentRevision.RecordId, Is.EqualTo(revisionA.RecordId));
                    Assert.That(absentRevision.ContentRevision, Is.Not.EqualTo(revisionA.ContentRevision));
                    Assert.That(absent.PreparedScene.HasAuthorScene, Is.False);
                    Assert.That(absent.PreparedScene.Program.HasAuthorScene, Is.False);
                    Assert.That(absent.PreparedScene.Roots, Is.Empty);
                    Assert.That(absent.PreparedScene.Snapshot, Is.SameAs(absent.Snapshot));
                    Assert.That(absent.PreparedScene.MaterialSet, Is.SameAs(absent.MaterialSet));
                });
            });
            AddStep("detach absent-scene renderer and participant", () =>
            {
                absentRenderer.Expire();
                sceneParticipant.Dispose();
            });
            AddUntilStep("wait for absent-scene renderer detach", () => absentRenderer.Parent == null);
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
            BmsAsyncNoteDrawable holdHeadHost = null!;
            BmsAsyncNoteDrawable holdBodyHost = null!;
            BmsAsyncNoteDrawable holdTailHost = null!;
            DrawableBmsHitObject noteDrawable = null!;
            DrawableBmsHoldNote holdDrawable = null!;
            DrawableBmsMine mineDrawable = null!;
            DrawableBmsBarLine barLineDrawable = null!;
            GameplaySkinSceneRuntimeHost sceneHost = null!;
            string packageRoot = string.Empty;
            Task<bool>? registrationTask = null;
            Task<IList<Live<SkinInfo>>>? dropdownTask = null;
            SkinCurrentRevision revision = null!;
            GameplaySkinEventSubscription eventSubscription = null!;
            var observedEvents = new List<GameplaySkinEventEnvelope>();
            int productionDrawableWaitAttempts = 0;
            int coreHudWaitAttempts = 0;

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
            AddStep("capture production BMS scene host", () =>
                sceneHost = renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single());
            AddUntilStep("wait for bounded BMS scene creation", () => sceneHost.PendingCreationCount == 0);
            AddStep("mount real core HUD against BMS scene runtime", () => renderer.AddProductionCoreHud());
            AddUntilStep("wait for exact BMS core text HUD partitions", () =>
            {
                HUDOverlay? hud = renderer.CoreHud;

                if (++coreHudWaitAttempts % 500 == 0)
                {
                    string containers = hud == null
                        ? "<none>"
                        : string.Join(",", hud.ChildrenOfType<SkinnableContainer>()
                                              .Select(container => $"{container.ComponentsLoaded}/{container.LoadState}"));
                    string componentTypes = hud == null
                        ? "<none>"
                        : string.Join(",", hud.ChildrenOfType<Drawable>()
                                               .OfType<IGameplaySkinHudProgrammaticVisualSource>()
                                               .Select(component =>
                                               {
                                                   var drawable = (Drawable)component;
                                                   return $"{drawable.GetType().Name}:{component.GameplaySkinHudRole}:{drawable.Parent?.GetType().Name ?? "<none>"}";
                                               }));
                    TestContext.Progress.WriteLine(
                        $"BMS core HUD wait: attempts={coreHudWaitAttempts}; loaded={hud?.IsLoaded == true}; " +
                        $"gauge={hud?.GameplaySkinGaugePartitions.Count ?? 0}; combo={hud?.GameplaySkinComboPartitions.Count ?? 0}; " +
                        $"judgement={hud?.GameplaySkinJudgementPartitions.Count ?? 0}; text={hud?.GameplaySkinTextPartitions.Count ?? 0}; " +
                        $"decoration={hud?.GameplaySkinDecorationPartitions.Count ?? 0}; containers={containers}; components={componentTypes}");
                }

                return hud?.IsLoaded == true
                       && hud.GameplaySkinTextPartitions.Count > 0;
            });
            AddUntilStep("wait for pooled public note, hold, mine, and bar-line production drawables", () =>
            {
                BmsHitObject note = renderer.BmsBeatmap.HitObjects.OfType<BmsHitObject>()
                                                  .First(candidateNote => candidateNote is not BmsHoldNote && candidateNote.LaneIndex == 1);
                BmsHoldNote hold = renderer.BmsBeatmap.HitObjects.OfType<BmsHoldNote>()
                                                 .Single(candidateHold => candidateHold.LaneIndex == 1);
                BmsMine mine = renderer.BmsBeatmap.Mines.Single(candidateMine => candidateMine.LaneIndex == 1);
                noteDrawable ??= renderer.BmsDrawable.ChildrenOfType<DrawableBmsHitObject>()
                                                .FirstOrDefault(drawable => ReferenceEquals(drawable.HitObject, note))!;
                holdDrawable ??= renderer.BmsDrawable.ChildrenOfType<DrawableBmsHoldNote>()
                                                .FirstOrDefault(drawable => ReferenceEquals(drawable.HitObject, hold))!;
                mineDrawable ??= renderer.BmsDrawable.ChildrenOfType<DrawableBmsMine>()
                                                .FirstOrDefault(drawable => ReferenceEquals(drawable.HitObject, mine))!;
                barLineDrawable ??= renderer.BmsDrawable.ChildrenOfType<DrawableBmsBarLine>()
                                                   .FirstOrDefault(drawable => drawable.HitObject.GroupLogicalIndex == 0
                                                                               && drawable.HitObject.StartTime > 0)!;

                if (++productionDrawableWaitAttempts % 500 == 0)
                {
                    TestContext.Progress.WriteLine(
                        $"BMS public drawable wait: attempts={productionDrawableWaitAttempts}; " +
                        $"note={noteDrawable?.IsLoaded == true}; hold={holdDrawable?.IsLoaded == true}; " +
                        $"mine={mineDrawable?.IsLoaded == true}; bar-line={barLineDrawable?.IsLoaded == true}; " +
                        $"all-note={renderer.BmsDrawable.ChildrenOfType<DrawableBmsHitObject>().Count()}; " +
                        $"all-hold={renderer.BmsDrawable.ChildrenOfType<DrawableBmsHoldNote>().Count()}; " +
                        $"all-mine={renderer.BmsDrawable.ChildrenOfType<DrawableBmsMine>().Count()}; " +
                        $"all-bar-line={renderer.BmsDrawable.ChildrenOfType<DrawableBmsBarLine>().Count()}");
                }

                return noteDrawable?.IsLoaded == true
                       && holdDrawable?.IsLoaded == true
                       && mineDrawable?.IsLoaded == true
                       && barLineDrawable?.IsLoaded == true;
            });
            AddUntilStep("wait for prepared public note production visual", () =>
            {
                noteHost ??= noteDrawable.ChildrenOfType<BmsAsyncNoteDrawable>()
                                        .SingleOrDefault(host => host.Lookup.Element == BmsNoteSkinElements.Note)!;
                Sprite? sprite = noteHost?.Drawable?.ChildrenOfType<Sprite>().SingleOrDefault();
                return noteHost?.Drawable is BmsSourceBoundNoteDrawable
                       && sprite?.Texture?.Width == 11
                       && sprite.Texture.Height == 13;
            });
            AddUntilStep("wait for prepared public hold-head production visual", () =>
            {
                holdHeadHost ??= holdDrawable.ChildrenOfType<BmsAsyncNoteDrawable>()
                                            .SingleOrDefault(host => host.Lookup.Element == BmsNoteSkinElements.LongNoteHead)!;
                return holdHeadHost?.Drawable is BmsSourceBoundNoteDrawable;
            });
            AddUntilStep("wait for prepared public hold-body production visual", () =>
            {
                holdBodyHost ??= holdDrawable.ChildrenOfType<BmsAsyncNoteDrawable>()
                                            .SingleOrDefault(host => host.Lookup.Element == BmsNoteSkinElements.LongNoteBody)!;
                return holdBodyHost?.Drawable is BmsSourceBoundLongNoteBodyDrawable;
            });
            AddUntilStep("wait for prepared public hold-tail production visual", () =>
            {
                holdTailHost ??= holdDrawable.ChildrenOfType<BmsAsyncNoteDrawable>()
                                            .SingleOrDefault(host => host.Lookup.Element == BmsNoteSkinElements.LongNoteTail)!;
                return holdTailHost?.Drawable is BmsSourceBoundNoteDrawable;
            });
            AddStep("attach read-only BMS event consumer and press a real lane action", () =>
            {
                GameplaySkinLayoutPublication publication = renderer.BmsLayoutProbe.Publication!;
                GameplaySkinEventStream stream = renderer.BmsDrawable.GameplaySkinEventStream;
                eventSubscription = stream.Subscribe();
                eventSubscription.DrainFrame(observedEvents.Add);

                Assert.Multiple(() =>
                {
                    Assert.That(renderer.BmsDrawable.GameplaySkinEventRuntime!.Publication, Is.SameAs(publication));
                    Assert.That(stream.CurrentRevision, Is.EqualTo(publication.EventRevision));
                    Assert.That(observedEvents, Has.Count.EqualTo(1));
                    Assert.That(observedEvents[0].DeliveryKind, Is.EqualTo(GameplaySkinEventDeliveryKind.Snapshot));
                    Assert.That(observedEvents[0].Revision, Is.EqualTo(publication.EventRevision));
                    Assert.That(renderer.BmsDrawable.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.Key1P_1), Is.True);
                });
            });
            AddUntilStep("wait for real BMS input producer edge", () =>
            {
                eventSubscription.DrainFrame(observedEvents.Add);
                return observedEvents.Any(envelope => envelope.EventKind == GameplaySkinEventKind.InputPressed
                                                      && envelope.LaneId?.Value == "bms.lane.key-1");
            });
            AddStep("release real BMS lane action", () =>
                Assert.That(renderer.BmsDrawable.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.Key1P_1), Is.True));
            AddStep("assert public document material reached exact production note", () =>
            {
                GameplaySkinLayoutPublication publication = renderer.BmsLayoutProbe.Publication!;
                BmsGameplayLayoutSnapshot layout = publication.GetAdapter<BmsGameplayLayoutSnapshot>();
                GameplaySkinResolvedMaterialSet materialSet = publication.MaterialSet;
                BmsGameplayLayoutLane lane = layout.GetLaneByLogicalIndex(1);
                var key = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.Note,
                    BmsGameplayNoteMaterialTarget.Create(layout, lane));
                var laneSurfaceKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.LaneSurface,
                    BmsGameplayNoteMaterialTarget.Create(layout, lane));
                GameplaySkinLaneTopologyGroup group = layout.Neutral.Context.Topology.GroupsInLogicalOrder.Single();
                var headKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.LongNoteHead,
                    BmsGameplayNoteMaterialTarget.Create(layout, lane));
                var bodyKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.LongNoteBody,
                    BmsGameplayNoteMaterialTarget.Create(layout, lane));
                var tailKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.LongNoteTail,
                    BmsGameplayNoteMaterialTarget.Create(layout, lane));
                var mineKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.Mine,
                    BmsGameplayNoteMaterialTarget.Create(layout, lane));
                var barLineKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.BarLine,
                    GameplaySkinResolvedMaterialTarget.ForGroup(group));
                GameplaySkinResolvedMaterialTarget stageTarget = GameplaySkinResolvedMaterialTarget.ForStage(group);
                var hitTargetKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.HitTarget, key.Target);
                var judgementLineKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.JudgementLine, stageTarget);
                var laneDividerKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.LaneDivider, key.Target);
                var laneCoverFillKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.LaneCoverFill, stageTarget);
                var laneCoverDecorationKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.LaneCoverDecoration, stageTarget);
                var playfieldBackdropKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.PlayfieldBackdrop, stageTarget);
                var playfieldBaseplateKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.PlayfieldBaseplate, stageTarget);
                var judgementDisplayKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.JudgementDisplay, stageTarget);
                var comboDisplayKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.ComboDisplay, stageTarget);
                var gaugeVisualKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.GaugeVisual, stageTarget);
                Assert.That(materialSet.TryGet(key, out GameplaySkinResolvedMaterialEntry? entry), Is.True);
                Assert.That(materialSet.TryGet(laneSurfaceKey, out GameplaySkinResolvedMaterialEntry? laneSurfaceEntry), Is.True);
                Assert.That(materialSet.TryGet(headKey, out GameplaySkinResolvedMaterialEntry? headEntry), Is.True);
                Assert.That(materialSet.TryGet(bodyKey, out GameplaySkinResolvedMaterialEntry? bodyEntry), Is.True);
                Assert.That(materialSet.TryGet(tailKey, out GameplaySkinResolvedMaterialEntry? tailEntry), Is.True);
                Assert.That(materialSet.TryGet(mineKey, out GameplaySkinResolvedMaterialEntry? mineEntry), Is.True);
                Assert.That(materialSet.TryGet(barLineKey, out GameplaySkinResolvedMaterialEntry? barLineEntry), Is.True);
                IBmsResolvedNoteMaterial material = entry!.GetMaterial<IBmsResolvedNoteMaterial>();
                Sprite visual = noteHost.Drawable!.ChildrenOfType<Sprite>().Single();
                BmsManagedPackageSourceRevision selectedSource = ((BmsLegacySkin)revision.Owner).CaptureManagedPackageSourceRevision();
                GameplaySkinPreparedScene preparedScene = publication.PreparedScene;
                GameplaySkinPreparedSceneResource publicNoteSceneResource = preparedScene.Resources.Single(resource =>
                    resource.Id == "texture.public-note");
                GameplaySkinEventEnvelope inputEdge = observedEvents.Single(envelope =>
                    envelope.EventKind == GameplaySkinEventKind.InputPressed
                    && envelope.LaneId?.Value == "bms.lane.key-1");
                GameplaySkinResolvedMaterialEntry selectedStageBackground = materialSet.Entries.Single(candidateEntry =>
                    ReferenceEquals(candidateEntry.Slot, GameplaySkinSlotCatalog.StageBackground));
                GameplaySkinResolvedMaterialEntry[] selectedEntries = materialSet.Entries.Where(candidateEntry =>
                    candidateEntry.Source.Kind == GameplaySkinResolvedMaterialSourceKind.SelectedPackage).ToArray();
                GameplaySkinResolvedMaterialEntry bgaViewportEntry = selectedEntries.Single(candidateEntry =>
                    ReferenceEquals(candidateEntry.Slot, GameplaySkinSlotCatalog.BgaViewport));
                GameplaySkinResolvedMaterialEntry bgaFrameEntry = selectedEntries.Single(candidateEntry =>
                    ReferenceEquals(candidateEntry.Slot, GameplaySkinSlotCatalog.BgaFrame));
                GameplaySkinResolvedMaterialEntry turntableEntry = selectedEntries.Single(candidateEntry =>
                    ReferenceEquals(candidateEntry.Slot, GameplaySkinSlotCatalog.Turntable));
                GameplaySkinResolvedMaterialEntry laserEntry = selectedEntries.Single(candidateEntry =>
                    ReferenceEquals(candidateEntry.Slot, GameplaySkinSlotCatalog.Laser));
                GameplaySkinResolvedMaterialEntry keyVisualEntry = selectedEntries.Single(candidateEntry =>
                    candidateEntry.Key.Equals(new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.KeyVisual, key.Target)));
                GameplaySkinResolvedMaterialEntry[] decorationEntries = selectedEntries.Where(candidateEntry =>
                    ReferenceEquals(candidateEntry.Slot, GameplaySkinSlotCatalog.Decoration)).ToArray();

                GameplaySkinSceneHostedSlot requireGate(GameplaySkinResolvedMaterialKey requiredKey)
                {
                    Assert.That(sceneHost.TryGetVisualGate(requiredKey, out GameplaySkinSceneHostedSlot? requiredGate), Is.True,
                        $"Missing production scene gate for {requiredKey}.");
                    return requiredGate!;
                }

                GameplaySkinSceneHostedSlot[] selectedGates = selectedEntries.Select(candidateEntry => requireGate(candidateEntry.Key)).ToArray();
                GameplaySkinSceneHostedSlot[] decorationGates = decorationEntries.Select(candidateEntry => requireGate(candidateEntry.Key)).ToArray();
                GameplaySkinSceneHostedSlot hitTargetGate = requireGate(hitTargetKey);
                GameplaySkinSceneHostedSlot judgementLineGate = requireGate(judgementLineKey);
                GameplaySkinSceneHostedSlot laneDividerGate = requireGate(laneDividerKey);
                GameplaySkinSceneHostedSlot laneCoverFillGate = requireGate(laneCoverFillKey);
                GameplaySkinSceneHostedSlot laneCoverDecorationGate = requireGate(laneCoverDecorationKey);
                GameplaySkinSceneHostedSlot playfieldBackdropGate = requireGate(playfieldBackdropKey);
                GameplaySkinSceneHostedSlot playfieldBaseplateGate = requireGate(playfieldBaseplateKey);
                GameplaySkinSceneHostedSlot judgementDisplayGate = requireGate(judgementDisplayKey);
                GameplaySkinSceneHostedSlot comboDisplayGate = requireGate(comboDisplayKey);
                GameplaySkinSceneHostedSlot gaugeVisualGate = requireGate(gaugeVisualKey);
                GameplaySkinSceneHostedSlot bgaViewportGate = requireGate(bgaViewportEntry.Key);
                GameplaySkinSceneHostedSlot bgaFrameGate = requireGate(bgaFrameEntry.Key);
                GameplaySkinSceneHostedSlot turntableGate = requireGate(turntableEntry.Key);
                GameplaySkinSceneHostedSlot laserGate = requireGate(laserEntry.Key);
                Assert.That(sceneHost.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? noteGate), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(headKey, out GameplaySkinSceneHostedSlot? headGate), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(bodyKey, out GameplaySkinSceneHostedSlot? bodyGate), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(tailKey, out GameplaySkinSceneHostedSlot? tailGate), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(mineKey, out GameplaySkinSceneHostedSlot? mineGate), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(barLineKey, out GameplaySkinSceneHostedSlot? barLineGate), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(laneSurfaceKey, out GameplaySkinSceneHostedSlot? laneSurfaceGate), Is.True);
                Assert.That(sceneHost.TryGetVisualGate(selectedStageBackground.Key, out GameplaySkinSceneHostedSlot? stageBackgroundGate), Is.True);
                BmsLane targetLane = renderer.BmsDrawable.Playfield.Lanes.Single(candidateLane =>
                    candidateLane.LayoutSnapshotLane?.LaneId.Value == "bms.lane.key-1");
                BmsBgaPanel bgaPanel = renderer.BmsDrawable.ChildrenOfType<BmsBgaPanel>().Single();
                BmsGaugeBar gaugeBar = renderer.BmsDrawable.ChildrenOfType<BmsGaugeBar>().Single();
                BmsComboCounter comboCounter = renderer.BmsDrawable.ChildrenOfType<BmsComboCounter>().Single();
                BmsHudLayoutSnapshotCarrier hudCarrier = renderer.BmsDrawable.ChildrenOfType<BmsHudLayoutSnapshotCarrier>().Single();
                BmsLaneCover[] laneCovers = renderer.BmsDrawable.Playfield.LaneCovers.ToArray();
                Assert.That(bgaPanel.TryGetContentState(0, out GameplaySkinBgaContentState bgaContentState, out long bgaContentRevision), Is.True);
                var attachState = (GameplaySkinStateEventPayload)observedEvents[0].Payload;
                GameplaySkinBgaStateSnapshot attachedBga = attachState.State.BgaViewports.Single(viewport => viewport.ViewportIndex == 0);
                Assert.That(sceneHost.TryGetHostedDrawable(bgaViewportEntry.Key, out _), Is.False);
                Assert.That(sceneHost.TryGetHostedDrawable(bgaFrameEntry.Key, out _), Is.False);
                DefaultBmsBgaPanelDisplay bgaDisplay = (DefaultBmsBgaPanelDisplay)bgaPanel.Drawable!;
                GameplaySkinSpecialisedSceneVisual bgaViewportVisual = bgaDisplay.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>()
                    .Single(candidate => candidate.Key.Equals(bgaViewportEntry.Key));
                GameplaySkinSpecialisedSceneVisual bgaFrameVisual = bgaDisplay.ChildrenOfType<GameplaySkinSpecialisedSceneVisual>()
                    .Single(candidate => candidate.Key.Equals(bgaFrameEntry.Key));

                GameplaySkinSlotDescriptor[] specialisedSlots =
                {
                    GameplaySkinSlotCatalog.Note,
                    GameplaySkinSlotCatalog.LongNoteHead,
                    GameplaySkinSlotCatalog.LongNoteBody,
                    GameplaySkinSlotCatalog.LongNoteTail,
                    GameplaySkinSlotCatalog.KeyVisual,
                    GameplaySkinSlotCatalog.HitExplosion,
                    GameplaySkinSlotCatalog.Mine,
                    GameplaySkinSlotCatalog.BarLine,
                    GameplaySkinSlotCatalog.LaneCoverFill,
                    GameplaySkinSlotCatalog.LaneCoverDecoration,
                    GameplaySkinSlotCatalog.BgaViewport,
                    GameplaySkinSlotCatalog.BgaFrame,
                };

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
                    Assert.That(
                        new[] { entry, headEntry, bodyEntry, tailEntry, mineEntry, laneSurfaceEntry, barLineEntry }
                            .All(candidateEntry => candidateEntry?.Source.Kind == GameplaySkinResolvedMaterialSourceKind.SelectedPackage),
                        Is.True,
                        "Every authored exact target in this product slice must retain selected-package authority.");
                    Assert.That(materialSet.Entries, Has.Count.EqualTo(
                        GameplaySkinSlotCatalog.All.Sum(descriptor =>
                            GameplaySkinPublicSlotMaterialTargets.Enumerate(descriptor, publication.Snapshot).Count)));
                    Assert.That(materialSet.Entries.Select(candidateEntry => candidateEntry.Slot).Distinct(),
                        Is.EquivalentTo(GameplaySkinSlotCatalog.All));
                    Assert.That(selectedEntries.Select(candidateEntry => candidateEntry.Slot).Distinct(),
                        Is.EquivalentTo(GameplaySkinSlotCatalog.All),
                        "The production package must author at least one real exact target for every advertised public slot.");
                    Assert.That(selectedGates.All(gate => gate.SuppressesProgrammaticVisual), Is.True,
                        "Every authored exact key must have a ready production replacement before the old BMS visual is hidden.");
                    Assert.That(selectedEntries.Where(candidateEntry => !specialisedSlots.Contains(candidateEntry.Slot))
                                               .All(candidateEntry => sceneHost.TryGetHostedDrawable(candidateEntry.Key, out _)), Is.True,
                        "Every authored shared static/effect/HUD/root key must own a mounted production drawable.");
                    Assert.That(BmsGameplayResolvedNoteMaterialPreparer.RuntimeCapabilities.Support, Has.Count.EqualTo(28));
                    Assert.That(materialSet.PackageRevision.RecordId, Is.EqualTo(revision.RecordId));
                    Assert.That(materialSet.PackageRevision.ContentRevision, Is.EqualTo(revision.ContentRevision));
                    Assert.That(preparedScene.HasAuthorScene, Is.True);
                    Assert.That(preparedScene.Snapshot, Is.SameAs(publication.Snapshot));
                    Assert.That(preparedScene.MaterialSet, Is.SameAs(materialSet));
                    Assert.That(preparedScene.PackageRevision, Is.SameAs(publication.Snapshot.Context.PackageRevision));
                    Assert.That(preparedScene.EventContractId, Is.EqualTo(GameplaySkinSceneContracts.EVENT_CONTRACT_ID));
                    Assert.That(preparedScene.Roots.Single().Source.Id, Is.EqualTo("node.root"));
                    Assert.That(preparedScene.Roots.Single().Slot, Is.Null,
                        "The document dispatcher is neutral; every public slot owns an independent sibling subtree.");
                    Assert.That(sceneHost.Publication, Is.SameAs(publication));
                    Assert.That(sceneHost.EventStream, Is.SameAs(renderer.BmsDrawable.GameplaySkinEventStream));
                    Assert.That(sceneHost.HostedSlots, Has.Count.EqualTo(materialSet.Entries.Count));
                    Assert.That(noteGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                    Assert.That(noteGate.RoutedNodes.Select(node => node.InstanceId), Is.EqualTo(new[] { "node.note" }));
                    Assert.That(headGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                    Assert.That(bodyGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                    Assert.That(tailGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                    Assert.That(mineGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                    Assert.That(barLineGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                    Assert.That(sceneHost.TryGetHostedDrawable(key, out _), Is.False,
                        "The typed Note consumer and shared scene host must not draw the same exact slot twice.");
                    Assert.That(sceneHost.TryGetRuntimeNode("node.note", out _), Is.False);
                    Assert.That(sceneHost.TryGetRuntimeNode("node.long-note-head", out _), Is.False);
                    Assert.That(sceneHost.TryGetRuntimeNode("node.long-note-body", out _), Is.False);
                    Assert.That(sceneHost.TryGetRuntimeNode("node.long-note-tail", out _), Is.False);
                    Assert.That(sceneHost.TryGetRuntimeNode("node.mine", out _), Is.False);
                    Assert.That(sceneHost.TryGetRuntimeNode("node.bar-line", out _), Is.False,
                        "Mine and BarLine scene claims belong to their real scrolling engine drawables, never a shared root overlay.");
                    Assert.That(laneSurfaceEntry!.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Provide));
                    Assert.That(laneSurfaceEntry.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.SelectedPackage));
                    Assert.That(laneSurfaceGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Scene));
                    Assert.That(laneSurfaceGate.SuppressesProgrammaticVisual, Is.True);
                    Assert.That(targetLane.GameplaySkinLaneSurfaceFallbackVisual.Alpha, Is.Zero);
                    Assert.That(laneDividerGate.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Semantic));
                    Assert.That(laneDividerGate.SuppressesProgrammaticVisual, Is.True);
                    Assert.That(targetLane.GameplaySkinLaneDividerFallbackVisual.Alpha, Is.Zero);
                    Assert.That(sceneHost.TryGetRuntimeNode("node.lane-surface", out GameplaySkinSceneRuntimeNode? laneSurfaceNode), Is.True);
                    Assert.That(((Sprite)laneSurfaceNode!.ContentDrawable).Texture,
                        Is.SameAs(laneSurfaceEntry.GetMaterial<GameplaySkinPublicSlotMaterial>().Texture));
                    Assert.That(targetLane.ChildrenOfType<SkinnableDrawable>()
                                          .Any(candidate => candidate.Drawable is DefaultBmsLaneBackgroundDisplay
                                                            && candidate.Alpha > 0
                                                            && candidate.Drawable.Alpha > 0),
                        Is.False,
                        "A selected exact LaneSurface scene must replace, not overdraw, the existing BMS lane background host.");
                    Assert.That(stageBackgroundGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Semantic));
                    Assert.That(stageBackgroundGate.SuppressesProgrammaticVisual, Is.True);
                    Assert.That(sceneHost.TryGetHostedDrawable(selectedStageBackground.Key, out _), Is.True,
                        "A selected StageBackground must be reachable through the mounted shared production layer.");
                    Assert.That(targetLane.HitTarget.ResolvedMaterialSet, Is.SameAs(materialSet));
                    Assert.That(targetLane.HitTarget.ResolvedMaterialKey.Slot, Is.SameAs(GameplaySkinSlotCatalog.KeyVisual));
                    Assert.That(targetLane.HitTarget.SceneVisualGate.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                    Assert.That(targetLane.HitTarget.SceneVisualGate.SuppressesProgrammaticVisual, Is.True);
                    Assert.That(targetLane.HitTarget.GameplaySkinKeyFlashFallbackVisual, Is.Not.Null,
                        "KeyVisual has no BMS native fallback and must not own or hide the independent KeyFlash glow.");
                    Assert.That(containsTexture(targetLane.HitTarget,
                        keyVisualEntry.GetMaterial<GameplaySkinPublicSlotMaterial>().Texture!), Is.True,
                        "KeyVisual must be instantiated by its real lane-local hit-target owner.");
                    Assert.That(hitTargetGate.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Semantic));
                    Assert.That(judgementLineGate.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Semantic));
                    Assert.That(targetLane.HitTarget.GameplaySkinHitTargetFallbackVisual!.Alpha, Is.Zero);
                    Assert.That(targetLane.HitTarget.GameplaySkinJudgementLineFallbackVisual!.Alpha, Is.Zero);
                    Assert.That(laneCoverFillGate.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                    Assert.That(laneCoverDecorationGate.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                    Assert.That(laneCovers, Has.Length.EqualTo(2));
                    Assert.That(laneCovers.SelectMany(cover => cover.GameplaySkinStageFallbackVisuals)
                                                .All(stageVisual => stageVisual.Target!.Equals(stageTarget)
                                                                    && stageVisual.FillVisual.Alpha == 0
                                                                    && stageVisual.DecorationVisual.Alpha == 0), Is.True,
                        "Both native Sudden/Hidden cover owners must hide their exact stage fill and decoration when authored replacements are ready.");
                    Assert.That(playfieldBackdropGate.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Semantic));
                    Assert.That(playfieldBaseplateGate.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Semantic));
                    Assert.That(renderer.BmsDrawable.Playfield.GameplaySkinStageFallbackVisuals
                                                .All(stageVisual => stageVisual.StageBackgroundVisual.Alpha == 0), Is.True);
                    Assert.That(renderer.BmsDrawable.Playfield.PlayfieldBackdropVisual.Alpha, Is.Zero);
                    Assert.That(renderer.BmsDrawable.Playfield.PlayfieldBaseplateVisual.Alpha, Is.Zero);
                    Assert.That(judgementDisplayGate.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Semantic));
                    Assert.That(comboDisplayGate.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Semantic));
                    Assert.That(gaugeVisualGate.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Semantic));
                    Assert.That(renderer.BmsDrawable.Playfield.JudgementVisual.Alpha, Is.Zero);
                    Assert.That(comboCounter.GameplaySkinFallbackVisual.Alpha, Is.Zero);
                    Assert.That(gaugeBar.GameplaySkinFallbackVisual.Alpha, Is.Zero);
                    Assert.That(renderer.CoreHud!.GameplaySkinGaugePartitions, Is.Empty,
                        "The core HUD must not create a second gauge authority beside the ruleset-owned BMS gauge carrier.");
                    Assert.That(renderer.CoreHud.GameplaySkinComboPartitions, Is.Empty,
                        "The core HUD must not create a second combo authority beside the ruleset-owned BMS combo carrier.");
                    Assert.That(renderer.CoreHud.GameplaySkinJudgementPartitions, Is.Empty,
                        "The core HUD must not create a second judgement authority beside the BMS playfield owner.");
                    Assert.That(renderer.CoreHud.GameplaySkinTextOwners, Is.Not.Empty);
                    Assert.That(renderer.CoreHud.GameplaySkinTextOwners.All(owner => owner.Alpha == 0), Is.True,
                        "A ready BMS TextHud must hide the real shared score/stat/text owners through the production HUDOverlay.");
                    Assert.That(renderer.CoreHud.GameplaySkinTextPartitions.All(partition =>
                        partition.ControllingKeys.Any(key => ReferenceEquals(key.Slot, GameplaySkinSlotCatalog.TextHud)
                                                             && key.Target.Kind == GameplaySkinResolvedMaterialTargetKind.Global)
                        && partition.ControllingKeys.Any(key => ReferenceEquals(key.Slot, GameplaySkinSlotCatalog.TextHud)
                                                                && key.Target.Kind == GameplaySkinResolvedMaterialTargetKind.Stage)), Is.True,
                        "Each real BMS core text owner must be controlled by its exact global and stage TextHud keys.");
                    Assert.That(renderer.CoreHud.GameplaySkinDecorationPartitions, Is.Empty,
                        "Authored Decoration is mounted by the shared semantic scene host and must not invent a duplicate core-HUD owner.");
                    Assert.That(decorationGates, Is.Not.Empty);
                    Assert.That(decorationGates.All(gate => gate.Route == GameplaySkinSceneHostRoute.Semantic
                                                           && gate.IsReplacementReady), Is.True,
                        "Every authored Decoration target must be mounted and replacement-ready through the shared semantic scene host.");
                    Assert.That(decorationEntries.All(decorationEntry =>
                        sceneHost.TryGetHostedDrawable(decorationEntry.Key, out Drawable? drawable)
                        && drawable?.Parent != null), Is.True,
                        "Every authored Decoration target must own a live production drawable.");
                    Assert.That(renderer.CoreHud.GameplaySkinGaugePartitions.Any(partition => partition.Visual is BmsGaugeBar), Is.False,
                        "The shared HUD adapter must not register the ruleset-owned BMS gauge a second time.");
                    Assert.That(renderer.CoreHud.GameplaySkinComboPartitions.Any(partition => partition.Visual is BmsComboCounter), Is.False,
                        "The shared HUD adapter must not register the ruleset-owned BMS combo a second time.");
                    Assert.That(hudCarrier.GaugeProgrammaticVisualOwner, Is.SameAs(gaugeBar));
                    Assert.That(hudCarrier.ComboProgrammaticVisualOwner, Is.SameAs(comboCounter),
                        "Ruleset HUD owners must retain their single existing stage-key registration path.");
                    Assert.That(bgaViewportGate.Layer, Is.EqualTo(GameplaySkinSceneLayer.Background));
                    Assert.That(bgaFrameGate.Layer, Is.EqualTo(GameplaySkinSceneLayer.Overlay));
                    Assert.That(bgaViewportGate.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                    Assert.That(bgaFrameGate.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                    Assert.That(bgaViewportVisual.IsApplied, Is.True);
                    Assert.That(bgaFrameVisual.IsApplied, Is.True);
                    Assert.That(isDescendantOf(bgaViewportVisual, bgaDisplay), Is.True);
                    Assert.That(isDescendantOf(bgaFrameVisual, bgaDisplay), Is.True,
                        "Both BGA author surfaces must live inside the engine-owned viewport rather than in a detached shared overlay.");
                    Assert.That(bgaViewportVisual.Parent!.Alpha, Is.EqualTo(1),
                        "The package's real engine-owned static background is a ready BGA content surface.");
                    Assert.That(containsTexture(bgaViewportVisual,
                        bgaViewportEntry.GetMaterial<GameplaySkinPublicSlotMaterial>().Texture!), Is.True);
                    Assert.That(containsTexture(bgaFrameVisual,
                        bgaFrameEntry.GetMaterial<GameplaySkinPublicSlotMaterial>().Texture!), Is.True);
                    Assert.That(bgaContentState, Is.EqualTo(GameplaySkinBgaContentState.Ready));
                    Assert.That(bgaContentRevision, Is.EqualTo(0));
                    Assert.That(attachedBga.ContentState, Is.EqualTo(bgaContentState));
                    Assert.That(attachedBga.ContentRevision, Is.EqualTo(bgaContentRevision));
                    Assert.That(turntableEntry.Target.LaneId?.Value, Is.EqualTo("bms.lane.scratch-1"));
                    Assert.That(turntableEntry.Target.GroupId?.Value, Is.EqualTo("bms.group.deck-1"));
                    Assert.That(turntableEntry.Target.GlobalLogicalIndex, Is.EqualTo(0));
                    Assert.That(turntableEntry.Target.GroupLocalLogicalIndex, Is.EqualTo(0));
                    Assert.That(laserEntry.Target, Is.EqualTo(turntableEntry.Target),
                        "Turntable and Laser must bind the same exact stable scratch LaneId, including deck-local indices.");
                    Assert.That(turntableGate.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Semantic));
                    Assert.That(laserGate.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Semantic));
                    Assert.That(inputEdge.Revision, Is.EqualTo(publication.EventRevision));
                    Assert.That(inputEdge.GroupId?.Value, Is.EqualTo("bms.group.deck-1"));
                    Assert.That(double.IsFinite(inputEdge.GameplayTime), Is.True);
                    Assert.That(((PoolableDrawable)noteDrawable).IsInPool, Is.True);
                    Assert.That(((PoolableDrawable)holdDrawable).IsInPool, Is.True);
                    Assert.That(((PoolableDrawable)mineDrawable).IsInPool, Is.True);
                    Assert.That(((PoolableDrawable)barLineDrawable).IsInPool, Is.True);
                    Assert.That(isDescendantOf(noteDrawable, targetLane.HitObjectContainer), Is.True);
                    Assert.That(isDescendantOf(holdDrawable, targetLane.HitObjectContainer), Is.True);
                    Assert.That(isDescendantOf(mineDrawable, targetLane.HitObjectContainer), Is.True);
                    Assert.That(isDescendantOf(barLineDrawable, renderer.BmsDrawable.Playfield.BarLinePlayfields.Single().HitObjectContainer), Is.True,
                        "BMS BarLine must retain its exact group-scoped scrolling owner after specialised scene routing.");
                    Assert.That(containsTexture(mineDrawable, publicNoteSceneResource!.Texture!), Is.True,
                        "The real pooled Mine owner must mount the author-scene resource, not only claim its exact-key gate.");
                    Assert.That(containsTexture(barLineDrawable, publicNoteSceneResource.Texture!), Is.True,
                        "The real pooled BarLine owner must mount the author-scene resource, not only claim its exact-key gate.");
                });

                assertSpecialisedSceneConsumer(noteHost, materialSet, key, noteGate!, "node.note");
                assertSpecialisedSceneConsumer(holdHeadHost, materialSet, headKey, headGate!, "node.long-note-head");
                assertSpecialisedSceneConsumer(holdBodyHost, materialSet, bodyKey, bodyGate!, "node.long-note-body");
                assertSpecialisedSceneConsumer(holdTailHost, materialSet, tailKey, tailGate!, "node.long-note-tail");
                assertSpecialisedSceneConsumer(mineDrawable, materialSet, mineKey, mineGate!, "node.mine");
                assertSpecialisedSceneConsumer(barLineDrawable, materialSet, barLineKey, barLineGate!, "node.bar-line");
                assertSpecialisedSceneConsumer(targetLane.HitTarget, materialSet, targetLane.HitTarget.ResolvedMaterialKey,
                    targetLane.HitTarget.SceneVisualGate, "node.key-visual");
            });
            AddStep("detach public-material renderer", () =>
            {
                eventSubscription.Dispose();
                renderer.Expire();
            });
            AddUntilStep("wait for public-material renderer detach", () => renderer.Parent == null);
        }

        [Test]
        public void TestSelectedCommonTailSuppressPublishesExplicitMarkerWithoutPostCommitFallback()
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;
            DrawableBmsHoldNote holdDrawable = null!;
            BmsAsyncNoteDrawable tailHost = null!;
            GameplaySkinSceneRuntimeHost sceneHost = null!;
            int tailWaitAttempts = 0;

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
                Add(renderer = new ExactLayoutJourneyHost(manager, useFixedBmsClock: true));
                renderer.ShowBms();
            });
            AddUntilStep("wait for exact suppressed-tail publication", () => renderer.BmsReady);
            AddStep("capture production suppress scene host", () =>
                sceneHost = renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single());
            AddStep("assert exact suppressed-tail publication before drawable load", () =>
            {
                GameplaySkinLayoutPublication publication = renderer.BmsLayoutProbe.Publication!;
                BmsGameplayLayoutSnapshot layout = publication.GetAdapter<BmsGameplayLayoutSnapshot>();
                BmsGameplayLayoutLane lane = layout.GetLaneByLogicalIndex(1);
                var key = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.LongNoteTail,
                    BmsGameplayNoteMaterialTarget.Create(layout, lane));

                Assert.Multiple(() =>
                {
                    Assert.That(publication.MaterialSet.TryGet(key, out GameplaySkinResolvedMaterialEntry? entry), Is.True);
                    Assert.That(entry!.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Suppress));
                    Assert.That(sceneHost.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate), Is.True);
                    Assert.That(gate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Suppressed));
                });
            });
            AddUntilStep("wait for explicit suppressed-tail marker", () =>
            {
                BmsHoldNote hold = renderer.BmsBeatmap.HitObjects.OfType<BmsHoldNote>().Single();
                holdDrawable ??= renderer.BmsDrawable.ChildrenOfType<DrawableBmsHoldNote>()
                                                 .FirstOrDefault(drawable => ReferenceEquals(drawable.HitObject, hold))!;
                tailHost ??= holdDrawable?.ChildrenOfType<BmsAsyncNoteDrawable>()
                                         .SingleOrDefault(host => host.Lookup.Element == BmsNoteSkinElements.LongNoteTail)!;

                if (++tailWaitAttempts % 500 == 0)
                {
                    TestContext.Progress.WriteLine(
                        $"BMS suppressed-tail wait: attempts={tailWaitAttempts}; hold={holdDrawable?.IsLoaded == true}; " +
                        $"tail-host={tailHost?.IsLoaded == true}; visual={tailHost?.Drawable?.GetType().Name ?? "<none>"}; " +
                        $"all-holds={renderer.BmsDrawable.ChildrenOfType<DrawableBmsHoldNote>().Count()}; " +
                        $"all-tail-hosts={renderer.BmsDrawable.ChildrenOfType<BmsAsyncNoteDrawable>().Count(host => host.Lookup.Element == BmsNoteSkinElements.LongNoteTail)}");
                }

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
                    Assert.That(sceneHost.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate), Is.True);
                    Assert.That(gate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Suppressed));
                    Assert.That(gate.SuppressesProgrammaticVisual, Is.True);
                    Assert.That(sceneHost.TryGetHostedDrawable(key, out _), Is.False,
                        "Suppress must hide the existing typed base host without adding a second scene drawable.");
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
                Add(renderer = new ExactLayoutJourneyHost(manager, beatmapSkin, useFixedBmsClock: true));
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
                    bmsRulesetOverride: new NoteResourceBmsRuleset(createPng(new Rgba32(35, 205, 145, 255))),
                    useFixedBmsClock: true));
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
                            using var exactSource = new ExactSourceChain(manager.CurrentSkin.Value);
                            GameplaySkinLayoutPublication publication = BmsGameplayResolvedNoteMaterialPreparer.Prepare(
                                exactSource,
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
        public void TestPublicKeyVisualDeclarationResolvesThroughFinalBmsRuntimeCapability()
        {
            Live<SkinInfo> candidate = null!;
            ExactLayoutJourneyHost renderer = null!;

            AddStep("create package with public BMS key visual", () =>
            {
                (_, candidate) = createCandidate(writePublicBmsKeyVisualPackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for unsupported-capability package", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && manager.CurrentSkin.Value.PackageContentRevision != null);
            AddStep("mount exact BMS renderer for public material", () =>
            {
                Add(renderer = new ExactLayoutJourneyHost(manager));
                renderer.ShowBms();
            });
            AddUntilStep("wait for exact capability material publication", () => renderer.BmsReady);
            AddStep("assert final BMS capability resolves the exact public material", () =>
            {
                GameplaySkinLayoutPublication publication = renderer.BmsLayoutProbe.Publication!;
                BmsGameplayLayoutSnapshot layout = publication.GetAdapter<BmsGameplayLayoutSnapshot>();
                BmsGameplayLayoutLane lane = layout.GetLane(GameplaySkinLaneId.Create("bms.lane.key-1"));
                GameplaySkinResolvedMaterialTarget target = BmsGameplayNoteMaterialTarget.Create(layout, lane);
                GameplaySkinResolvedMaterialDiagnostic[] capabilityDiagnostics = publication.MaterialSet.Diagnostics
                    .Where(diagnostic => diagnostic.Code == "bms.capability.unsupported-slot")
                    .ToArray();
                bool found = publication.MaterialSet.TryGet(
                    new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.KeyVisual, target),
                    out GameplaySkinResolvedMaterialEntry? keyVisual);

                Assert.Multiple(() =>
                {
                    Assert.That(GameplaySkinSlotCatalog.All, Does.Contain(GameplaySkinSlotCatalog.KeyVisual));
                    Assert.That(BmsGameplayResolvedNoteMaterialPreparer.RuntimeCapabilities.TryGet(GameplaySkinSlotCatalog.KeyVisual, out _), Is.True);
                    Assert.That(BmsManagedPackageNoteMaterializer.RuntimeCapabilities.TryGet(GameplaySkinSlotCatalog.KeyVisual, out _), Is.False);
                    Assert.That(capabilityDiagnostics, Is.Empty);
                    Assert.That(found, Is.True);
                    Assert.That(keyVisual!.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Provide));
                    Assert.That(keyVisual.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.SelectedPackage));
                    Assert.That(keyVisual.Source.StableId, Is.EqualTo("selected-public-slots"));
                    Assert.That(keyVisual.GetMaterial<GameplaySkinPublicSlotMaterial>().Texture, Is.Not.Null);
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

        private static void assertSpecialisedSceneConsumer(
            Drawable consumer,
            GameplaySkinResolvedMaterialSet materialSet,
            GameplaySkinResolvedMaterialKey key,
            GameplaySkinSceneHostedSlot gate,
            params string[] expectedNodeIds)
        {
            Type? contract = consumer.GetType().GetInterfaces()
                                     .SingleOrDefault(type => type.Name == "IGameplaySkinSpecialisedSceneConsumer");
            Assert.That(contract, Is.Not.Null,
                $"{consumer.GetType().Name} must consume specialised scene nodes through the shared immutable contract.");

            object? read(string propertyName)
            {
                var property = contract!.GetProperty(propertyName);
                Assert.That(property, Is.Not.Null,
                    $"The specialised scene consumer contract must expose {propertyName}.");
                return property!.GetValue(consumer);
            }

            string[]? appliedNodeIds = ((IEnumerable<string>?)read("AppliedSceneNodeIds"))?.ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(read("ResolvedMaterialSet"), Is.SameAs(materialSet));
                Assert.That(read("ResolvedMaterialKey"), Is.EqualTo(key));
                Assert.That(read("SceneVisualGate"), Is.SameAs(gate));
                Assert.That(appliedNodeIds, Is.EqualTo(expectedNodeIds));
                Assert.That(appliedNodeIds, Is.Unique);
                Assert.That(gate.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                Assert.That(gate.RoutedNodes.Select(node => node.InstanceId), Is.EqualTo(expectedNodeIds));
            });
        }

        private static bool containsTexture(Drawable root, Texture texture)
            => root.ChildrenOfType<Sprite>().Any(sprite => ReferenceEquals(sprite.Texture, texture));

        private static bool isDescendantOf(Drawable drawable, CompositeDrawable expectedAncestor)
        {
            Drawable? current = drawable.Parent;

            while (current != null)
            {
                if (ReferenceEquals(current, expectedAncestor))
                    return true;

                current = current.Parent;
            }

            return false;
        }

        private static void writeTailSuppressPackage(string root)
        {
            writeLayoutRevisionPackage(root, "A", new Rgba32(240, 40, 80, 255));
            File.AppendAllText(
                Path.Combine(root, "skin.ini"),
                "\n" +
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
                "object.note: resource Provide \"notes/public-note\"\n" +
                "object.long-note.head: resource Provide \"notes/public-note\"\n" +
                "object.long-note.body: resource Provide \"notes/public-note\"\n" +
                "object.long-note.tail: resource Provide \"notes/public-note\"\n" +
                "object.mine: resource Provide \"notes/public-note\"\n" +
                "playfield.lane-surface: resource Provide \"notes/public-note\"\n" +
                "playfield.key: resource Provide \"notes/public-note\"\n" +
                "effect.key-flash: resource Provide \"notes/public-note\"\n" +
                "effect.hit-explosion: resource Provide \"notes/public-note\"\n" +
                "playfield.hit-target: resource Provide \"notes/public-note\"\n" +
                "playfield.lane-divider: resource Provide \"notes/public-note\"\n" +
                "decoration: resource Provide \"notes/public-note\"\n" +
                "Target: Group ruleset=bms keymode=5k stage-mode=single group=bms.group.deck-1 group-logical=0 group-visual=0\n" +
                "playfield.bar-line: resource Provide \"notes/public-note\"\n" +
                "Target: Stage ruleset=bms keymode=5k stage-mode=single group=bms.group.deck-1 group-logical=0 group-visual=0\n" +
                "playfield.judgement-line: resource Provide \"notes/public-note\"\n" +
                "playfield.lane-cover.fill: resource Provide \"notes/public-note\"\n" +
                "hud.judgement: resource Provide \"notes/public-note\"\n" +
                "hud.combo: resource Provide \"notes/public-note\"\n" +
                "hud.gauge: resource Provide \"notes/public-note\"\n" +
                "hud.text: resource Provide \"notes/public-note\"\n" +
                "stage.background: resource Provide \"notes/public-note\"\n" +
                "stage.foreground: resource Provide \"notes/public-note\"\n" +
                "playfield.backdrop: resource Provide \"notes/public-note\"\n" +
                "playfield.baseplate: resource Provide \"notes/public-note\"\n" +
                "playfield.lane-cover.decoration: resource Provide \"notes/public-note\"\n" +
                "decoration: resource Provide \"notes/public-note\"\n" +
                "Target: Global ruleset=bms keymode=5k stage-mode=single\n" +
                "hud.text: resource Provide \"notes/public-note\"\n" +
                "bga.viewport: resource Provide \"notes/public-note\"\n" +
                "bga.frame: resource Provide \"notes/public-note\"\n" +
                "decoration: resource Provide \"notes/public-note\"\n" +
                "\n[GameplaySkin.Bms:1]\n" +
                "Target: Lane ruleset=bms keymode=5k stage-mode=single group=bms.group.deck-1 lane=bms.lane.scratch-1 group-logical=0 group-visual=0 global-logical=0 global-visual=0 group-local-logical=0 group-local-visual=0\n" +
                "playfield.turntable: resource Provide \"notes/public-note\"\n" +
                "playfield.laser: resource Provide \"notes/public-note\"\n");

            using var image = new Image<Rgba32>(11, 13, new Rgba32(25, 215, 165, 255));
            using Stream output = File.Create(Path.Combine(notes, "public-note.png"));
            image.SaveAsPng(output);
            writePublicFiveKeyAuthorScene(root);
        }

        private static void writePublicFiveKeyAuthorScene(string root)
        {
            File.WriteAllText(
                Path.Combine(root, GameplaySkinSceneContracts.MANIFEST_FILE_NAME),
                """
                {
                  "contract": "oms-gameplay-skin-manifest.v1",
                  "scene": "gameplay-skin.scene.json",
                  "sceneContract": "oms-gameplay-skin-scene.v1",
                  "eventContract": "oms-gameplay-skin-event.v1",
                  "resources": [
                    { "id": "texture.public-note", "type": "texture", "path": "notes/public-note.png" }
                  ]
                }
                """);
            File.WriteAllText(
                Path.Combine(root, GameplaySkinSceneContracts.SCENE_FILE_NAME),
                """
                {
                  "contract": "oms-gameplay-skin-scene.v1",
                  "root": {
                    "id": "node.root",
                    "type": "container",
                    "target": { "kind": "global" },
                    "blend": "inherit",
                    "properties": {},
                    "effects": [],
                    "children": [
                      {
                        "id": "node.lane-surface",
                        "type": "sprite",
                        "target": { "kind": "lane", "id": "bms.lane.key-1", "index": 1 },
                        "slot": "playfield.lane-surface",
                        "blend": "alpha",
                        "properties": { "opacity": 0.5, "visible": true },
                        "effects": [],
                        "children": []
                      },
                      {
                        "id": "node.key-visual",
                        "type": "sprite",
                        "target": { "kind": "lane", "id": "bms.lane.key-1", "index": 1 },
                        "slot": "playfield.key",
                        "blend": "alpha",
                        "properties": { "opacity": 0.8, "visible": true },
                        "effects": [],
                        "children": []
                      },
                      {
                        "id": "node.note",
                        "type": "sprite",
                        "target": { "kind": "lane", "id": "bms.lane.key-1", "index": 1 },
                        "slot": "object.note",
                        "resource": "texture.public-note",
                        "blend": "alpha",
                        "properties": { "opacity": 0.75, "visible": true },
                        "effects": [],
                        "children": []
                      },
                      {
                        "id": "node.long-note-head",
                        "type": "sprite",
                        "target": { "kind": "lane", "id": "bms.lane.key-1", "index": 1 },
                        "slot": "object.long-note.head",
                        "resource": "texture.public-note",
                        "blend": "alpha",
                        "properties": { "opacity": 0.7, "visible": true },
                        "effects": [],
                        "children": []
                      },
                      {
                        "id": "node.long-note-body",
                        "type": "sprite",
                        "target": { "kind": "lane", "id": "bms.lane.key-1", "index": 1 },
                        "slot": "object.long-note.body",
                        "resource": "texture.public-note",
                        "blend": "alpha",
                        "properties": { "opacity": 0.65, "visible": true },
                        "effects": [],
                        "children": []
                      },
                      {
                        "id": "node.long-note-tail",
                        "type": "sprite",
                        "target": { "kind": "lane", "id": "bms.lane.key-1", "index": 1 },
                        "slot": "object.long-note.tail",
                        "resource": "texture.public-note",
                        "blend": "alpha",
                        "properties": { "opacity": 0.6, "visible": true },
                        "effects": [],
                        "children": []
                      },
                      {
                        "id": "node.mine",
                        "type": "sprite",
                        "target": { "kind": "lane", "id": "bms.lane.key-1", "index": 1 },
                        "slot": "object.mine",
                        "resource": "texture.public-note",
                        "blend": "alpha",
                        "properties": { "opacity": 0.55, "visible": true },
                        "effects": [],
                        "children": []
                      },
                      {
                        "id": "node.hit-explosion",
                        "type": "sprite",
                        "target": { "kind": "lane", "id": "bms.lane.key-1", "index": 1 },
                        "slot": "effect.hit-explosion",
                        "resource": "texture.public-note",
                        "blend": "additive",
                        "properties": { "opacity": 0.9, "visible": true },
                        "effects": [],
                        "children": []
                      },
                      {
                        "id": "node.bar-line",
                        "type": "sprite",
                        "target": { "kind": "group", "id": "bms.group.deck-1", "index": 0 },
                        "slot": "playfield.bar-line",
                        "resource": "texture.public-note",
                        "blend": "alpha",
                        "properties": { "opacity": 0.5, "visible": true },
                        "effects": [],
                        "children": []
                      }
                    ]
                  },
                  "tracks": [],
                  "stateMachines": [],
                  "bindings": [],
                  "templates": [],
                  "instances": []
                }
                """);
        }

        private static void writeAuthorScenePackage(string root, string revision)
        {
            File.WriteAllText(
                Path.Combine(root, GameplaySkinSceneContracts.MANIFEST_FILE_NAME),
                """
                {
                  "contract": "oms-gameplay-skin-manifest.v1",
                  "scene": "gameplay-skin.scene.json",
                  "sceneContract": "oms-gameplay-skin-scene.v1",
                  "eventContract": "oms-gameplay-skin-event.v1",
                  "resources": []
                }
                """);
            File.WriteAllText(
                Path.Combine(root, GameplaySkinSceneContracts.SCENE_FILE_NAME),
                $$"""
                {
                  "contract": "oms-gameplay-skin-scene.v1",
                  "root": {
                    "id": "node.root",
                    "type": "container",
                    "target": { "kind": "global" },
                    "slot": "decoration",
                    "blend": "alpha",
                    "properties": { "opacity": {{(string.Equals(revision, "B", StringComparison.Ordinal) ? "0.75" : "0.5")}}, "visible": true },
                    "effects": [],
                    "children": []
                  },
                  "tracks": [],
                  "stateMachines": [],
                  "bindings": [],
                  "templates": [],
                  "instances": []
                }
                """);
        }

        private static void writeInvalidAuthorSceneManifest(string root)
        {
            File.WriteAllText(
                Path.Combine(root, GameplaySkinSceneContracts.MANIFEST_FILE_NAME),
                """
                {
                  "contract": "oms-gameplay-skin-manifest.v2",
                  "scene": "gameplay-skin.scene.json",
                  "sceneContract": "oms-gameplay-skin-scene.v1",
                  "eventContract": "oms-gameplay-skin-event.v1",
                  "resources": []
                }
                """);
        }

        private static GameplaySkinLayoutPublication prepareExactBmsPublication(
            SkinCurrentRevision revision,
            BmsBeatmap beatmap,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.Create(revision);
            var topologyOwner = new BmsGameplaySkinLaneTopologyRevisionOwner();
            BmsGameplaySkinLaneTopologyPublication topology = topologyOwner.Publish(
                beatmap.BmsInfo.Keymode,
                BmsPlayfieldStyle.P1);
            BmsGameplayLayoutSnapshot layout = BmsGameplayLayoutSolver.Solve(
                beatmap.BmsInfo.KeymodeResolution,
                BmsPlayfieldStyle.P1,
                BmsGameplayLayoutConfiguration.FromSkin(revision.Owner, beatmap.BmsInfo.Keymode),
                BmsGameplayLayoutEnvironment.Default,
                package,
                topology,
                0);
            using var exactSource = new ExactSourceChain(revision.Owner);
            return BmsGameplayResolvedNoteMaterialPreparer.Prepare(exactSource, layout, cancellationToken);
        }

        private static void writePublicationTargetDiagnosticPackage(string root)
        {
            writeLayoutRevisionPackage(root, "A", new Rgba32(240, 40, 80, 255));
            File.AppendAllText(
                Path.Combine(root, "skin.ini"),
                "\n" +
                "Target: Lane ruleset=mania keymode=4k stage-mode=single group=mania.group.stage-1 lane=mania.lane.key-1 group-logical=0 group-visual=0 global-logical=0 global-visual=0 group-local-logical=0 group-local-visual=0\n" +
                "object.note: resource Bogus\n" +
                "Target: Lane ruleset=bms keymode=7k stage-mode=single group=bms.group.deck-1 lane=bms.lane.key-1 group-logical=0 group-visual=0 global-logical=999 global-visual=1 group-local-logical=1 group-local-visual=1\n" +
                "object.note: resource Provide \"notes/note\"\n");
        }

        private static void writePublicBmsKeyVisualPackage(string root)
        {
            writeLayoutRevisionPackage(root, "A", new Rgba32(240, 40, 80, 255));
            File.AppendAllText(
                Path.Combine(root, "skin.ini"),
                "\n" +
                "Target: Lane ruleset=bms keymode=7k stage-mode=single group=bms.group.deck-1 lane=bms.lane.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=1 group-local-visual=1\n" +
                "playfield.key: resource Provide \"notes/note\"\n");
        }

        private const string diagnostic_sensitive_resource = "C:/private-author/secret-note-resource.png";

        private static void writeLoggedInvalidDiagnosticPackage(string root)
        {
            writeLayoutRevisionPackage(root, "A", new Rgba32(240, 40, 80, 255));
            File.AppendAllText(
                Path.Combine(root, "skin.ini"),
                "\n" +
                "Target: Lane ruleset=bms keymode=7k stage-mode=single group=bms.group.deck-1 lane=bms.lane.key-1 group-logical=0 group-visual=0 global-logical=999 global-visual=1 group-local-logical=1 group-local-visual=1\n" +
                $"object.note: resource Provide \"{diagnostic_sensitive_resource}\"\n");
        }

        private static void writePortableManiaCapabilityPackage(string root)
        {
            writeLayoutRevisionPackage(root, "A", new Rgba32(240, 40, 80, 255));
            File.AppendAllText(
                Path.Combine(root, "skin.ini"),
                "\n" +
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
                Assert.That(materialSet.Entries, Has.Count.EqualTo(
                    GameplaySkinSlotCatalog.All.Sum(descriptor =>
                        GameplaySkinPublicSlotMaterialTargets.Enumerate(descriptor, publication.Snapshot).Count)));
                Assert.That(materialSet.Entries.Select(entry => entry.Slot).Distinct(), Is.EquivalentTo(GameplaySkinSlotCatalog.All));
                Assert.That(BmsGameplayResolvedNoteMaterialPreparer.RuntimeCapabilities.Support, Has.Count.EqualTo(28));
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
                Assert.That(notes.All(note => ReferenceEquals(note.ExactMaterialSet, materialSet)), Is.True);
                Assert.That(renderer.BmsDrawable.PreStartSpeedPreviewMaterialSet, Is.SameAs(materialSet));
                Assert.That(renderer.BmsDrawable.BgaMaterialSet, Is.SameAs(materialSet));
                Assert.That(renderer.BmsDrawable.HudMaterialSet, Is.SameAs(materialSet));
                Assert.That(renderer.BmsDrawable.GaugeMaterialSet, Is.SameAs(materialSet));
                Assert.That(renderer.BmsDrawable.ComboMaterialSet, Is.SameAs(materialSet));
                Assert.That(renderer.BmsDrawable.GameplaySkinEventRuntime!.Publication, Is.SameAs(publication));
                Assert.That(renderer.BmsDrawable.GameplaySkinEventStream.CurrentRevision, Is.EqualTo(publication.EventRevision));
            });
        }

        private static void assertExactManiaLayoutTree(ExactLayoutJourneyHost renderer, SkinCurrentRevision revision)
        {
            assertExactLayoutPair(renderer.ManiaLayoutProbe, revision, "mania");
            GameplaySkinLayoutPublication publication = renderer.ManiaLayoutProbe.Publication!;
            GameplaySkinLayoutSnapshot snapshot = renderer.ManiaDrawable.LayoutSnapshot;

            Assert.Multiple(() =>
            {
                Assert.That(publication.MaterialSet.Entries, Has.Count.EqualTo(
                    GameplaySkinSlotCatalog.Common.Where(GameplaySkinRuntimeSupportProfile.Mania.IsSupported).Sum(descriptor =>
                        GameplaySkinPublicSlotMaterialTargets.Enumerate(descriptor, snapshot).Count)));
                Assert.That(publication.MaterialSet.Entries.Select(entry => entry.Slot).Distinct(),
                    Is.EquivalentTo(GameplaySkinSlotCatalog.Common.Where(GameplaySkinRuntimeSupportProfile.Mania.IsSupported)));
                Assert.That(publication.PreparedScene.Snapshot, Is.SameAs(snapshot));
                Assert.That(publication.PreparedScene.MaterialSet, Is.SameAs(publication.MaterialSet));
                Assert.That(renderer.ManiaDrawable.GameplaySkinEventStream.CurrentRevision, Is.EqualTo(publication.EventRevision));
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
                Assert.That(publication.PreparedScene.Snapshot, Is.SameAs(publication.Snapshot));
                Assert.That(publication.PreparedScene.MaterialSet, Is.SameAs(publication.MaterialSet));
                Assert.That(publication.PreparedScene.PackageRevision, Is.SameAs(publication.Snapshot.Context.PackageRevision));
                Assert.That(publication.EventRevision.GameplayRevision, Is.EqualTo(publication.Snapshot.Context.PackageRevision.Generation));
                Assert.That(publication.EventRevision.LayoutRevision, Is.EqualTo(publication.Snapshot.Context.LayoutRevision));
                Assert.That(publication.EventRevision.MaterialRevision, Is.EqualTo(publication.MaterialSet.LayoutRevision));
                Assert.That(publication.EventRevision.SceneRevision, Is.EqualTo(publication.PreparedScene.SceneRevision));
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
                Assert.That(layoutB.PreparedScene, Is.Not.SameAs(layoutA.PreparedScene));
                Assert.That(layoutA.MaterialSet.Snapshot, Is.SameAs(layoutA.Snapshot));
                Assert.That(layoutB.MaterialSet.Snapshot, Is.SameAs(layoutB.Snapshot));
                Assert.That(layoutA.PreparedScene.Snapshot, Is.SameAs(layoutA.Snapshot));
                Assert.That(layoutB.PreparedScene.Snapshot, Is.SameAs(layoutB.Snapshot));
                Assert.That(layoutA.PreparedScene.MaterialSet, Is.SameAs(layoutA.MaterialSet));
                Assert.That(layoutB.PreparedScene.MaterialSet, Is.SameAs(layoutB.MaterialSet));
                Assert.That(layoutB.EventRevision.GameplayRevision, Is.GreaterThan(layoutA.EventRevision.GameplayRevision),
                    "A same-ID package replacement must carry a distinct exact gameplay event identity even when both root-local layout revisions start at zero.");
                Assert.That(layoutA.EventRevision, Is.Not.EqualTo(layoutB.EventRevision));
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
                $"ComboPosition: {(first ? "120" : "180")}\n" +
                "\n" +
                "[GameplaySkin.Common:1]\n" +
                "Target: Global ruleset=bms keymode=7k stage-mode=single\n" +
                "decoration: resource Provide \"notes/note\"\n" +
                "Target: Global ruleset=mania keymode=4k stage-mode=single\n" +
                "decoration: resource Provide \"notes/note\"\n",
                StringComparison.Ordinal);
            File.WriteAllText(skinIniPath, skinIni);
            writeAuthorScenePackage(packageRoot, revision);
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

            private readonly BmsRuleset bmsRuleset;

            private readonly Container providerHost;
            private readonly Container bmsAuxiliaryHost;
            private readonly ManualClock? bmsSourceClock;
            private readonly FramedClock? bmsFrameClock;

            public BmsBeatmap BmsBeatmap { get; }

            public DrawableBmsRuleset BmsDrawable { get; }

            public DrawableManiaRuleset ManiaDrawable { get; }

            public HUDOverlay? CoreHud { get; private set; }

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
                bool useFiveKeyBeatmap = false,
                bool useFourteenKeyBeatmap = false,
                bool includeBgaTimeline = false,
                bool useChargeLongNotes = false,
                bool useFixedBmsClock = false,
                ExactBmsProductionFixture? exactBmsFixture = null)
                : base(skinManager.CurrentSkin.Value)
            {
                if (useFiveKeyBeatmap && useFourteenKeyBeatmap)
                    throw new ArgumentException("The exact layout journey host accepts one BMS chart mode.");

                if (exactBmsFixture != null
                    && (useFiveKeyBeatmap || useFourteenKeyBeatmap || includeBgaTimeline || useChargeLongNotes))
                {
                    throw new ArgumentException("An exact BMS production fixture cannot be combined with a legacy journey fixture switch.");
                }

                if (includeBgaTimeline && !useFourteenKeyBeatmap)
                    throw new ArgumentException("The exact layout journey host's BGA fixture is defined for its 14K chart.");

                this.skinManager = skinManager;
                RelativeSizeAxes = Axes.Both;

                bmsRuleset = bmsRulesetOverride ?? new BmsRuleset();
                bmsRulesetConfig = new BmsRulesetConfigManager(null, bmsRuleset.RulesetInfo);

                if (exactBmsFixture != null)
                    bmsRulesetConfig.GetBindable<BmsPlayfieldStyle>(BmsRulesetSetting.PlayfieldStyle).Value = exactBmsFixture.PlayfieldStyle;

                scoreProcessor = bmsRuleset.CreateScoreProcessor();
                healthProcessor = bmsRuleset.CreateHealthProcessor(0);
                string bmsText = exactBmsFixture?.ChartText ?? (useFourteenKeyBeatmap
                    ? @"
#TITLE Current revision partial-stage material
#BPM 120
#WAV01 note.wav
#00111:0100
#00116:0100
#00121:0100
#00126:0100
"
                    : useFiveKeyBeatmap
                        ? @"
#TITLE Current revision public material
#BPM 120
#WAV01 note.wav
#WAV02 hold.wav
#00111:0100
#00112:0100
#00113:0100
#00114:0100
#00115:0100
#00151:02000200
#001D1:00AA0000
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
");
                string bmsFilename = exactBmsFixture?.ChartFilename ?? (useFourteenKeyBeatmap
                    ? "current-revision-partial-stage.bms"
                    : useFiveKeyBeatmap
                        ? "current-revision-public-material.bms"
                        : "current-revision-layout.bme");

                if (includeBgaTimeline)
                    bmsText += "\n#BMP01 custom-gate-bga.png\n#00104:0100\n";

                BmsBeatmapDecoderOptions? decoderOptions = exactBmsFixture?.KeymodeOverride is BmsKeymode keymodeOverride
                    ? new BmsBeatmapDecoderOptions(keymodeOverride)
                    : null;
                var decoded = new BmsBeatmapDecoder().DecodeText(bmsText, bmsFilename, decoderOptions);
                BmsBeatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decoded), bmsRuleset).Convert();
                BmsBeatmap bmsBeatmap = BmsBeatmap;
                exactBmsFixture?.PrepareBeatmap?.Invoke(bmsBeatmap);
                IReadOnlyList<Mod>? bmsMods = exactBmsFixture?.Mods ?? (useChargeLongNotes
                    ? new Mod[] { new BmsModChargeNote() }
                    : null);

                if (bmsMods != null)
                    scoreProcessor.Mods.Value = bmsMods;

                healthProcessor.ApplyBeatmap(bmsBeatmap);
                scoreProcessor.ApplyBeatmap(bmsBeatmap);
                BmsDrawable = (DrawableBmsRuleset)bmsRuleset.CreateDrawableRulesetWith(bmsBeatmap, bmsMods);

                if (useFiveKeyBeatmap || useFourteenKeyBeatmap || useFixedBmsClock || exactBmsFixture != null)
                {
                    new BmsModSudden().ApplyToDrawableRuleset(BmsDrawable);
                    new BmsModHidden().ApplyToDrawableRuleset(BmsDrawable);

                    bmsSourceClock = new ManualClock
                    {
                        CurrentTime = exactBmsFixture?.InitialGameplayTime ?? 1_500,
                        IsRunning = false,
                    };
                    bmsFrameClock = new FramedClock(bmsSourceClock);
                    bmsFrameClock.ProcessFrame();
                    BmsDrawable.Clock = bmsFrameClock;
                }

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

            public void AdvanceBmsTo(double gameplayTime)
            {
                if (bmsSourceClock == null || bmsFrameClock == null)
                    throw new InvalidOperationException("This exact BMS host was not constructed with a fixed production clock.");

                bmsSourceClock.CurrentTime = gameplayTime;
                bmsFrameClock.ProcessFrame();
                BmsDrawable.UpdateSubTree();
            }

            public void AddProductionCoreHud()
            {
                if (CoreHud != null)
                    throw new InvalidOperationException("The production core HUD has already been mounted.");

                CoreHud = new HUDOverlay(BmsDrawable, Array.Empty<Mod>(), new PlayerConfiguration())
                {
                    RelativeSizeAxes = Axes.Both,
                    AlwaysPresent = true,
                };
                var gameplayClock = new GameplayClockContainer(
                    new TrackVirtual(60_000),
                    applyOffsets: false,
                    requireDecoupling: false);
                var gameplayState = new GameplayState(
                    BmsBeatmap,
                    bmsRuleset,
                    scoreProcessor: scoreProcessor,
                    healthProcessor: healthProcessor);
                BmsProvider.Add(new CoreHudDependenciesContainer(gameplayClock, gameplayState)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = gameplayClock.WithChild(CoreHud),
                });
            }

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

        private partial class CoreHudDependenciesContainer : Container
        {
            private readonly IGameplayClock gameplayClock;
            private readonly GameplayState gameplayState;

            public CoreHudDependenciesContainer(IGameplayClock gameplayClock, GameplayState gameplayState)
            {
                this.gameplayClock = gameplayClock;
                this.gameplayState = gameplayState;
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
                dependencies.CacheAs(gameplayClock);
                dependencies.Cache(gameplayState);
                return dependencies;
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
