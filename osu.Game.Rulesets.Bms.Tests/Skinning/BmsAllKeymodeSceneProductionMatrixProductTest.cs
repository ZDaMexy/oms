// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using oms.Input;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.P1, "c5-matrix-5k.bms", 28)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P2, "c5-matrix-7k.bme", 28)]
        [TestCase(BmsKeymode.Key9K_Bms, BmsPlayfieldStyle.Center, "c5-matrix-9k.bms", 26)]
        [TestCase(BmsKeymode.Key9K_Pms, BmsPlayfieldStyle.Center, "c5-matrix-9k.pms", 26)]
        [TestCase(BmsKeymode.Key14K, BmsPlayfieldStyle.Center, "c5-matrix-14k.bms", 28)]
        public void TestCurrentPackageAuthorSceneReachesEveryApplicableBmsProductionSlot(
            BmsKeymode keymode,
            BmsPlayfieldStyle style,
            string chartFilename,
            int expectedApplicableSlots)
        {
            var selection = new C5CurrentPackageSelection();
            ExactLayoutJourneyHost renderer = null!;
            GameplaySkinSceneRuntimeHost sceneHost = null!;
            string? lastProductionOwnerIssue = null;

            addSelectC5Package(
                selection,
                MaterialDiagnosticPackageSource.ManagedFolder,
                root => writeC5MatrixPackage(root, keymode, style, everyLaneNoteOnly: false));

            AddStep("mount exact keymode production tree", () =>
            {
                var fixture = new ExactBmsProductionFixture(
                    createC5MatrixChart(keymode),
                    chartFilename,
                    style,
                    initialGameplayTime: 2_000,
                    keymodeOverride: keymode);
                Add(renderer = new ExactLayoutJourneyHost(manager, exactBmsFixture: fixture));
                renderer.ShowBms();
            });
            AddUntilStep("wait for exact package layout material and scene publication", () => renderer.BmsReady);
            AddStep("mount production core HUD consumer", () => renderer.AddProductionCoreHud());
            AddUntilStep("wait for bounded scene and HUD owners", () =>
            {
                sceneHost ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().SingleOrDefault()!;
                return sceneHost?.IsSceneReady == true
                       && sceneHost.PendingCreationCount == 0
                       && renderer.CoreHud?.IsLoaded == true
                       && renderer.BmsDrawable.Playfield.KeysoundStore.ChannelPool.All(channel => channel.LoadState >= LoadState.Ready);
            });
            AddUntilStep("wait for every applicable slot production host", () =>
            {
                string? issue = selectedProductionOwnerIssue(renderer, sceneHost);

                if (!string.Equals(issue, lastProductionOwnerIssue, StringComparison.Ordinal))
                {
                    lastProductionOwnerIssue = issue;
                    TestContext.Progress.WriteLine($"C5 production owner pending: {issue ?? "none"}");
                }

                return issue == null;
            });
            AddStep("assert full applicable catalog reaches exact production owners", () =>
                assertC5MatrixProduction(renderer, sceneHost, keymode, style, expectedApplicableSlots));
            AddStep("detach exact keymode production tree", () => renderer.Expire());
            AddUntilStep("wait for exact keymode tree detach", () => renderer.Parent == null);
        }

        [TestCase(MaterialDiagnosticPackageSource.ManagedFolder, C5LaneModKind.Mirror)]
        [TestCase(MaterialDiagnosticPackageSource.OrdinaryRealm, C5LaneModKind.Mirror)]
        [TestCase(MaterialDiagnosticPackageSource.OrdinaryRealm, C5LaneModKind.SeededRandom)]
        public void TestCurrentPackageModUsesOneFinalLaneForMaterialSceneEventsAndKeysound(
            MaterialDiagnosticPackageSource source,
            C5LaneModKind modKind)
        {
            const int random_seed = 20260417;

            var selection = new C5CurrentPackageSelection();
            ExactLayoutJourneyHost renderer = null!;
            GameplaySkinEventSubscription subscription = null!;
            GameplaySkinSceneRuntimeHost sceneHost = null!;
            BmsHitObject movedNote = null!;
            BmsLane targetLane = null!;
            DrawableBmsHitObject movedDrawable = null!;
            BmsAsyncNoteDrawable noteHost = null!;
            GameplaySkinResolvedMaterialKey finalNoteKey = null!;
            long objectId = -1;
            OmsAction finalOmsAction = default;
            var observed = new List<GameplaySkinEventEnvelope>();
            string? lastObjectBindingIssue = null;
            string? lastFinalLaneEventIssue = null;

            addSelectC5Package(
                selection,
                source,
                root => writeC5MatrixPackage(root, BmsKeymode.Key7K, BmsPlayfieldStyle.P1, everyLaneNoteOnly: true));

            AddStep("mount modded exact current-package production tree", () =>
            {
                Mod laneMod = createLaneMod(modKind, random_seed);
                var fixture = new ExactBmsProductionFixture(
                    createC5ModChart(),
                    "c5-current-package-mod.bme",
                    BmsPlayfieldStyle.P1,
                    new[] { laneMod },
                    initialGameplayTime: 1_500,
                    prepareBeatmap: beatmap => ((IApplicableToBeatmap)laneMod).ApplyToBeatmap(beatmap));
                Add(renderer = new ExactLayoutJourneyHost(manager, exactBmsFixture: fixture));
                renderer.ShowBms();
            });
            AddUntilStep("wait for modded exact publication", () => renderer.BmsReady);
            AddUntilStep("wait for modded scene and keysound owners", () =>
            {
                sceneHost ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().SingleOrDefault()!;
                return sceneHost?.IsSceneReady == true
                       && renderer.BmsDrawable.Playfield.KeysoundStore.ChannelPool.All(channel => channel.LoadState >= LoadState.Ready);
            });
            AddStep("capture one parser-owned post-mod object identity", () =>
            {
                movedNote = renderer.BmsBeatmap.HitObjects.OfType<BmsHitObject>()
                                    .Single(note => note is not BmsHoldNote && note.KeysoundSample?.Filename == "moved.wav");
                BmsGameplayLayoutSnapshot layout = renderer.BmsDrawable.LayoutSnapshot;
                BmsGameplayLayoutLane finalLane = layout.GetLaneByLogicalIndex(movedNote.LaneIndex);
                targetLane = renderer.BmsDrawable.Playfield.Lanes.Single(lane => lane.LaneIndex == movedNote.LaneIndex);
                finalNoteKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.Note,
                    BmsGameplayNoteMaterialTarget.Create(layout, finalLane));
                objectId = renderer.BmsDrawable.GetGameplaySkinObjectId(movedNote);

                Assert.That(OmsBmsActionMap.TryMapToOmsAction(renderer.BmsDrawable.Variant, finalLane.Action, out finalOmsAction), Is.True);
                renderer.BmsDrawable.Playfield.KeysoundStore.EnablePlaybackLogForTesting();
                subscription = renderer.BmsDrawable.GameplaySkinEventStream.Subscribe();
                subscription.DrainFrame(observed.Add);
            });
            AddStep("assert deterministic final permutation and armed lane timeline", () =>
            {
                if (modKind == C5LaneModKind.Mirror)
                    Assert.That(movedNote.LaneIndex, Is.EqualTo(6));
                else
                    assertSeededRandomIsDeterministic(movedNote.LaneIndex, random_seed);

                Assert.Multiple(() =>
                {
                    Assert.That(movedNote.LaneIndex, Is.Not.EqualTo(1), "The fixture seed must exercise a real lane move.");
                    Assert.That(renderer.BmsBeatmap.LaneKeysoundTimelineDiagnostic, Is.Null,
                        "Permutation Random retains one bijective armed-lane timeline; only S-RANDOM disables it.");
                    Assert.That(renderer.BmsBeatmap.GetLaneKeysoundTimeline(movedNote.LaneIndex)
                                        .Any(entry => entry.Sample.Filename == "moved.wav"), Is.True);
                    Assert.That(targetLane.LayoutSnapshotLane!.LaneId, Is.EqualTo(finalNoteKey.Target.LaneId));
                    Assert.That(finalNoteKey.Target.Matches(renderer.BmsDrawable.LayoutSnapshot.Neutral.Context.Topology), Is.True);
                });
            });
            AddStep("advance into the real pooled object usage", () => renderer.AdvanceBmsTo(movedNote.StartTime - 500));
            AddUntilStep("wait for exact object spawn and specialised binding", () =>
            {
                subscription.DrainFrame(observed.Add);
                movedDrawable ??= targetLane.AllHitObjects.OfType<DrawableBmsHitObject>()
                                            .SingleOrDefault(drawable => ReferenceEquals(drawable.HitObject, movedNote))!;
                noteHost ??= movedDrawable?.ChildrenOfType<BmsAsyncNoteDrawable>()
                                          .SingleOrDefault(host => host.Lookup.Element == BmsNoteSkinElements.Note)!;
                string? issue = movedDrawable == null
                    ? "pooled drawable is not present in the final lane"
                    : !movedDrawable.IsLoaded
                        ? "pooled drawable is not loaded"
                        : noteHost == null
                            ? "note component host is not mounted"
                            : noteHost.SpecialisedSceneVisual == null
                                ? "specialised author scene visual is not mounted"
                                : noteHost.SpecialisedSceneVisual.BoundObjectId != objectId
                                    ? $"specialised visual object id is {noteHost.SpecialisedSceneVisual.BoundObjectId}, expected {objectId}"
                                    : !getObservedObjectStates(observed).Any(state => state.ObjectId == objectId)
                                        ? "production object state is absent from Snapshot/ObjectSpawned"
                                        : null;

                if (!string.Equals(issue, lastObjectBindingIssue, StringComparison.Ordinal))
                {
                    lastObjectBindingIssue = issue;
                    TestContext.Progress.WriteLine($"C5 final-lane object binding pending: {issue ?? "none"}");
                }

                return issue == null;
            });
            AddStep("advance final lane to the exact hit time", () => renderer.AdvanceBmsTo(movedNote.StartTime));
            AddStep("hit final lane through production input and shared sample store", () =>
            {
                Assert.That(renderer.BmsDrawable.GameplayInputManager!.TriggerOmsActionPressed(finalOmsAction), Is.True);
            });
            AddStep("settle the real judgement frame", () =>
            {
                renderer.AdvanceBmsTo(movedNote.StartTime + 1);
                subscription.DrainFrame(observed.Add);
            });
            AddUntilStep("wait for final-lane input and judgement envelopes", () =>
            {
                subscription.DrainFrame(observed.Add);
                bool hasInput = observed.Any(envelope => envelope.EventKind == GameplaySkinEventKind.InputPressed
                                                         && envelope.LaneId?.Equals(finalNoteKey.Target.LaneId) == true);
                bool hasJudgement = observed.Any(envelope => envelope.EventKind == GameplaySkinEventKind.JudgementApplied
                                                             && envelope.Payload is GameplaySkinJudgementEventPayload judgement
                                                             && judgement.State.ObjectId == objectId);
                string? issue = !hasInput
                    ? "final LaneId input edge is absent"
                    : !hasJudgement
                        ? "final object judgement edge is absent"
                        : null;

                if (!string.Equals(issue, lastFinalLaneEventIssue, StringComparison.Ordinal))
                {
                    lastFinalLaneEventIssue = issue;
                    TestContext.Progress.WriteLine($"C5 final-lane event pending: {issue ?? "none"}");
                }

                return issue == null;
            });
            AddStep("release final lane and replay its armed timeline", () =>
            {
                Assert.That(renderer.BmsDrawable.GameplayInputManager!.TriggerOmsActionReleased(finalOmsAction), Is.True);

                foreach (var channel in renderer.BmsDrawable.Playfield.KeysoundStore.ChannelPool)
                    channel.Stop();

                renderer.BmsDrawable.Playfield.KeysoundStore.ClearPlaybackLogForTesting();
                renderer.AdvanceBmsTo(movedNote.StartTime + 100);
                _ = targetLane.OnPressed(new KeyBindingPressEvent<BmsAction>(new osu.Framework.Input.States.InputState(), targetLane.LayoutLane.Action));
            });
            AddStep("assert one final LaneId owns material scene events and actual WAV", () =>
            {
                GameplaySkinResolvedMaterialSet materials = renderer.BmsLayoutProbe.Publication!.MaterialSet;
                Assert.That(materials.TryGet(finalNoteKey, out GameplaySkinResolvedMaterialEntry? selectedEntry), Is.True);
                GameplaySkinSpecialisedSceneVisual visual = noteHost.SpecialisedSceneVisual!;
                GameplaySkinLaneGroupId finalGroup = renderer.BmsDrawable.LayoutSnapshot
                                                           .GetLaneByLogicalIndex(movedNote.LaneIndex)
                                                           .NeutralLane.TopologyEntry.Identity.Group.Id;
                GameplaySkinObjectStateSnapshot objectState = getObservedObjectStates(observed)
                                                              .First(state => state.ObjectId == objectId);
                GameplaySkinJudgementEventPayload judgement = observed.Where(envelope => envelope.EventKind == GameplaySkinEventKind.JudgementApplied)
                                                                      .Select(envelope => envelope.Payload)
                                                                      .OfType<GameplaySkinJudgementEventPayload>()
                                                                      .Single(payload => payload.State.ObjectId == objectId);
                GameplaySkinInputEventPayload input = observed.Where(envelope => envelope.EventKind == GameplaySkinEventKind.InputPressed
                                                                                  && envelope.LaneId?.Equals(finalNoteKey.Target.LaneId) == true)
                                                               .Select(envelope => envelope.Payload)
                                                               .OfType<GameplaySkinInputEventPayload>()
                                                               .Single();

                Assert.Multiple(() =>
                {
                    Assert.That(selectedEntry!.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.SelectedPackage));
                    Assert.That(selectedEntry.Source.IsSelectedDocumentDeclaration, Is.True);
                    Assert.That(selectedEntry.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Provide));
                    Assert.That(noteHost.ResolvedMaterialKey, Is.EqualTo(finalNoteKey));
                    Assert.That(visual.Key, Is.EqualTo(finalNoteKey));
                    Assert.That(visual.BoundObjectId, Is.EqualTo(objectId));
                    Assert.That(objectState.LaneId, Is.EqualTo(finalNoteKey.Target.LaneId));
                    Assert.That(objectState.GroupId, Is.EqualTo(finalGroup));
                    Assert.That(judgement.State.LaneId, Is.EqualTo(finalNoteKey.Target.LaneId));
                    Assert.That(judgement.State.GroupId, Is.EqualTo(finalGroup));
                    Assert.That(input.State.LaneId, Is.EqualTo(finalNoteKey.Target.LaneId));
                    Assert.That(input.State.GroupId, Is.EqualTo(finalGroup));
                    Assert.That(renderer.BmsDrawable.Playfield.KeysoundStore.PlaybackLogForTesting,
                        Has.Some.Property(nameof(KeysoundPlaybackRecord.Filename)).EqualTo("moved.wav"));
                });
            });
            AddStep("detach mod event consumer and production tree", () =>
            {
                subscription.Dispose();
                renderer.Expire();
            });
            AddUntilStep("wait for modded production tree detach", () => renderer.Parent == null);
        }

        private void addSelectC5Package(
            C5CurrentPackageSelection selection,
            MaterialDiagnosticPackageSource source,
            Action<string> writer)
        {
            switch (source)
            {
                case MaterialDiagnosticPackageSource.OrdinaryRealm:
                    AddStep("create ordinary current C5 package", () =>
                    {
                        selection.PackageRoot = LocalStorage.GetFullPath($"realm-c5-production-{Guid.NewGuid():N}");
                        writer(selection.PackageRoot);
                        selection.Candidate = createRealmRevisionCandidate(selection.PackageRoot);
                        manager.CurrentSkinInfo.Value = selection.Candidate;
                    });
                    break;

                case MaterialDiagnosticPackageSource.ManagedFolder:
                    AddStep("create managed current C5 package", () =>
                    {
                        (selection.PackageRoot, selection.Candidate) = createCandidate(
                            writer,
                            typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                        manager.CurrentSkinInfo.Value = selection.Candidate;
                    });
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(source), source, "This production slice accepts ordinary or managed exact current packages.");
            }

            AddUntilStep("wait for exact current C5 package", () =>
                selection.Candidate != null
                && manager.CurrentSkinInfo.Value.ID == selection.Candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == selection.Candidate.ID
                && manager.CurrentSkin.Value is BmsLegacySkin
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
        }

        private static bool selectedProductionOwnersReady(ExactLayoutJourneyHost renderer, GameplaySkinSceneRuntimeHost sceneHost)
            => selectedProductionOwnerIssue(renderer, sceneHost) == null;

        private static string? selectedProductionOwnerIssue(ExactLayoutJourneyHost renderer, GameplaySkinSceneRuntimeHost sceneHost)
        {
            GameplaySkinResolvedMaterialEntry[] selected = renderer.BmsLayoutProbe.Publication!.MaterialSet.Entries
                                                                    .Where(entry => entry.Source.IsSelectedDocumentDeclaration)
                                                                    .ToArray();

            if (selected.Length == 0)
                return "no selected author declarations";

            GameplaySkinSpecialisedSceneVisual[] specialised = renderer.BmsDrawable
                                                                       .ChildrenOfType<GameplaySkinSpecialisedSceneVisual>()
                                                                       .ToArray();

            foreach (GameplaySkinResolvedMaterialEntry entry in selected)
            {
                if (!sceneHost.TryGetVisualGate(entry.Key, out GameplaySkinSceneHostedSlot? gate)
                    || gate == null)
                    return $"{entry.Key}: missing visual gate";

                if (gate.Route == GameplaySkinSceneHostRoute.Scene)
                {
                    if (gate.RoutedNodes.Count != 1
                        || !gate.IsReplacementReady
                        || !sceneHost.TryGetHostedDrawable(entry.Key, out Drawable? drawable)
                        || drawable?.Parent == null)
                        return $"{entry.Key}: scene route is not mounted and replacement-ready";
                }
                else if (gate.Route == GameplaySkinSceneHostRoute.Semantic)
                {
                    if (gate.RoutedNodes.Count != 0
                        || !gate.IsReplacementReady
                        || !sceneHost.TryGetHostedDrawable(entry.Key, out Drawable? drawable)
                        || drawable?.Parent == null)
                        return $"{entry.Key}: shared semantic route is not mounted and replacement-ready";
                }
                else if (gate.Route != GameplaySkinSceneHostRoute.Specialised)
                    return $"{entry.Key}: unexpected production route {gate.Route}";
            }

            foreach (IGrouping<GameplaySkinSlotDescriptor, GameplaySkinResolvedMaterialEntry> group in
                     selected.Where(entry => sceneHost.TryGetVisualGate(entry.Key, out GameplaySkinSceneHostedSlot? gate)
                                                   && gate?.Route == GameplaySkinSceneHostRoute.Specialised)
                             .GroupBy(entry => entry.Slot))
            {
                if (ReferenceEquals(group.Key, GameplaySkinSlotCatalog.HitExplosion))
                {
                    foreach (GameplaySkinResolvedMaterialEntry entry in group)
                    {
                        BmsLane? lane = renderer.BmsDrawable.Playfield.Lanes.SingleOrDefault(candidate =>
                            candidate.LayoutSnapshotLane?.LaneId.Equals(entry.Target.LaneId) == true);

                        if (lane == null || lane.HitExplosionPoolSize != lane.HitExplosionPoolCapacity)
                            return $"{entry.Key}: hit-explosion pool is absent or not fully prewarmed";
                    }

                    // The prepared bounded pool is the production host. A visual becomes replacement-ready only
                    // while a real judgement owns a lease; overlap/reuse/ceiling are exercised by the dedicated test.
                    continue;
                }

                GameplaySkinResolvedMaterialKey[] selectedKeys = group.Select(entry => entry.Key).ToArray();

                if (!specialised.Any(visual => ReferenceEquals(visual.Key.Slot, group.Key)
                                               && selectedKeys.Contains(visual.Key)
                                               && visual.Parent != null))
                {
                    return $"{group.Key.Id}: no selected specialised production owner is mounted";
                }
            }

            return null;
        }

        private static void assertC5MatrixProduction(
            ExactLayoutJourneyHost renderer,
            GameplaySkinSceneRuntimeHost sceneHost,
            BmsKeymode keymode,
            BmsPlayfieldStyle requestedStyle,
            int expectedApplicableSlots)
        {
            GameplaySkinLayoutPublication publication = renderer.BmsLayoutProbe.Publication!;
            BmsGameplayLayoutSnapshot layout = publication.GetAdapter<BmsGameplayLayoutSnapshot>();
            GameplaySkinLaneTopologySnapshot topology = layout.Neutral.Context.Topology;
            GameplaySkinSlotDescriptor[] applicable = GameplaySkinSlotCatalog.All
                                                                      .Where(descriptor => GameplaySkinPublicSlotMaterialTargets
                                                                          .Enumerate(descriptor, layout.Neutral).Count > 0)
                                                                      .ToArray();
            GameplaySkinResolvedMaterialEntry[] selected = publication.MaterialSet.Entries
                                                                      .Where(entry => entry.Source.IsSelectedDocumentDeclaration)
                                                                      .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(layout.Keymode, Is.EqualTo(keymode));
                Assert.That(layout.Style, Is.EqualTo(requestedStyle.GetAppliedStyle(keymode)));
                Assert.That(publication.PreparedScene.HasAuthorScene, Is.True);
                Assert.That(publication.PreparedScene.Snapshot, Is.SameAs(layout.Neutral));
                Assert.That(publication.PreparedScene.MaterialSet, Is.SameAs(publication.MaterialSet));
                Assert.That(publication.MaterialSet.ContractIdentity.IsCurrentFor(GameplaySkinRuntimeSupportProfile.Bms), Is.True);
                Assert.That(applicable, Has.Length.EqualTo(expectedApplicableSlots));
                Assert.That(selected.Select(entry => entry.Slot).Distinct(), Is.EquivalentTo(applicable));
                Assert.That(sceneHost.RuntimeCapabilities.Support.Keys, Is.EquivalentTo(applicable.Select(slot => slot.Id)));
                Assert.That(selected.All(entry => entry.State == GameplaySkinResolvedMaterialState.Provide
                                                  && entry.Source.Kind == GameplaySkinResolvedMaterialSourceKind.SelectedPackage
                                                  && entry.Target.Matches(topology)), Is.True);
                Assert.That(selectedProductionOwnersReady(renderer, sceneHost), Is.True);
                Assert.That(sceneHost.RuntimeFaults, Is.Empty);
            });

            foreach (GameplaySkinResolvedMaterialEntry entry in selected)
            {
                Assert.That(sceneHost.RuntimeCapabilities.TryGet(entry.Slot, out GameplaySkinRuntimeSlotSupport? support), Is.True, entry.Key.ToString());
                Assert.That(support!.Capabilities.HasFlag(GameplaySkinRuntimeSlotCapability.Provide), Is.True, entry.Key.ToString());
            }

            GameplaySkinLaneTopologyEntry[] scratch = topology.LanesInLogicalOrder
                                                              .Where(lane => lane.Identity.Role == GameplaySkinLaneRole.Scratch)
                                                              .ToArray();
            GameplaySkinLaneTopologyEntry[] special = topology.LanesInLogicalOrder
                                                              .Where(lane => lane.Identity.Role == GameplaySkinLaneRole.SpecialKey)
                                                              .ToArray();

            if (keymode is BmsKeymode.Key9K_Bms or BmsKeymode.Key9K_Pms)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(topology.GroupsInLogicalOrder, Has.Count.EqualTo(1));
                    Assert.That(scratch, Is.Empty, "The parser-owned 9K topology must not invent a scratch lane.");
                    Assert.That(special, Is.Empty, "The frozen BMS 9K projection uses nine stable Key roles, not a second SpecialKey authority.");
                    Assert.That(selected.Any(entry => ReferenceEquals(entry.Slot, GameplaySkinSlotCatalog.Turntable)
                                                      || ReferenceEquals(entry.Slot, GameplaySkinSlotCatalog.Laser)), Is.False);
                });
            }
            else
            {
                int expectedScratch = keymode == BmsKeymode.Key14K ? 2 : 1;
                Assert.That(scratch, Has.Length.EqualTo(expectedScratch));
                Assert.That(selected.Where(entry => ReferenceEquals(entry.Slot, GameplaySkinSlotCatalog.Turntable))
                                    .Select(entry => entry.Target.LaneId), Is.EquivalentTo(scratch.Select(lane => lane.Identity.Id)));
                Assert.That(selected.Where(entry => ReferenceEquals(entry.Slot, GameplaySkinSlotCatalog.Laser))
                                    .Select(entry => entry.Target.LaneId), Is.EquivalentTo(scratch.Select(lane => lane.Identity.Id)));
            }

            int expectedGroups = keymode == BmsKeymode.Key14K ? 2 : 1;
            Assert.That(topology.GroupsInLogicalOrder, Has.Count.EqualTo(expectedGroups));
            Assert.That(renderer.BmsDrawable.Playfield.GroupContainers, Has.Count.EqualTo(expectedGroups));
            Assert.That(renderer.BmsDrawable.Playfield.BarLinePlayfields, Has.Count.EqualTo(expectedGroups));
            Assert.That(selected.Where(entry => ReferenceEquals(entry.Slot, GameplaySkinSlotCatalog.BarLine))
                                .Select(entry => entry.Target.GroupId),
                Is.EquivalentTo(topology.GroupsInLogicalOrder.Select(group => group.Identity.Id)));

            if (keymode == BmsKeymode.Key14K)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(topology.GroupsInLogicalOrder.Select(group => group.Identity.Id.Value),
                        Is.EqualTo(new[] { "bms.group.deck-1", "bms.group.deck-2" }));
                    Assert.That(layout.GetLaneByLogicalIndex(0).LaneId.Value, Is.EqualTo("bms.lane.scratch-1"));
                    Assert.That(layout.GetLaneByLogicalIndex(15).LaneId.Value, Is.EqualTo("bms.lane.scratch-2"));
                    Assert.That(selected.Where(entry => ReferenceEquals(entry.Slot, GameplaySkinSlotCatalog.Note))
                                        .Select(entry => entry.Target.GroupId),
                        Is.EquivalentTo(topology.GroupsInLogicalOrder.Select(group => group.Identity.Id)));
                });
            }
        }

        private static void writeC5MatrixPackage(
            string root,
            BmsKeymode keymode,
            BmsPlayfieldStyle style,
            bool everyLaneNoteOnly)
        {
            Directory.CreateDirectory(root);
            string notes = Path.Combine(root, "notes");
            Directory.CreateDirectory(notes);

            BmsGameplaySkinLaneTopologyProjection projection = BmsGameplaySkinLaneTopologyFactory.Create(keymode, style);
            MatrixDeclaration[] declarations = createMatrixDeclarations(projection.Topology, everyLaneNoteOnly).ToArray();
            string keymodeId = getKeymodeId(keymode);
            string stageMode = projection.Topology.GroupsInLogicalOrder.Count == 2 ? "dual" : "single";
            var common = new StringBuilder();
            var bms = new StringBuilder();

            foreach (MatrixDeclaration declaration in declarations)
            {
                StringBuilder target = declaration.Slot.CatalogFamily == GameplaySkinSlotCatalogFamily.Bms ? bms : common;
                appendDocumentTarget(target, declaration.Target, keymodeId, stageMode);
                target.Append(declaration.Slot.Id).AppendLine(": resource Provide \"notes/matrix\"");
            }

            var skin = new StringBuilder()
                       .AppendLine("[General]")
                       .AppendLine("Name: C5 all-keymode production matrix")
                       .AppendLine("Author: OMS tests")
                       .AppendLine("Version: 2.7")
                       .AppendLine()
                       .AppendLine("[Bms]")
                       .Append("Keymode: ").AppendLine(getSkinKeymode(keymode))
                       .AppendLine()
                       .AppendLine("[GameplaySkin.Common:1]")
                       .Append(common);

            if (bms.Length > 0)
                skin.AppendLine().AppendLine("[GameplaySkin.Bms:1]").Append(bms);

            File.WriteAllText(Path.Combine(root, "skin.ini"), skin.ToString());
            using (var image = new Image<Rgba32>(13, 17, new Rgba32(30, 210, 160, 255)))
            using (Stream output = File.Create(Path.Combine(notes, "matrix.png")))
                image.SaveAsPng(output);

            writeC5MatrixScene(root, declarations);
        }

        private static IEnumerable<MatrixDeclaration> createMatrixDeclarations(
            GameplaySkinLaneTopologySnapshot topology,
            bool everyLaneNoteOnly)
        {
            int nodeIndex = 0;

            if (everyLaneNoteOnly)
            {
                foreach (GameplaySkinLaneTopologyEntry lane in topology.LanesInLogicalOrder)
                {
                    GameplaySkinLaneTopologyGroup group = topology.GroupsInLogicalOrder.Single(candidate => candidate.Identity.Id.Equals(lane.Identity.Group.Id));
                    yield return new MatrixDeclaration(
                        GameplaySkinSlotCatalog.Note,
                        GameplaySkinResolvedMaterialTarget.ForLane(group, lane),
                        $"node.slot-{++nodeIndex:000}");
                }

                yield break;
            }

            foreach (GameplaySkinSlotDescriptor descriptor in GameplaySkinSlotCatalog.All)
            {
                if ((descriptor.AllowedScopes & GameplaySkinSlotScope.Global) != 0)
                {
                    yield return new MatrixDeclaration(descriptor, GameplaySkinResolvedMaterialTarget.Global, $"node.slot-{++nodeIndex:000}");
                    continue;
                }

                if ((descriptor.AllowedScopes & GameplaySkinSlotScope.Stage) != 0)
                {
                    foreach (GameplaySkinLaneTopologyGroup group in topology.GroupsInLogicalOrder)
                    {
                        yield return new MatrixDeclaration(
                            descriptor,
                            GameplaySkinResolvedMaterialTarget.ForStage(group),
                            $"node.slot-{++nodeIndex:000}");
                    }

                    continue;
                }

                if ((descriptor.AllowedScopes & GameplaySkinSlotScope.Group) != 0)
                {
                    foreach (GameplaySkinLaneTopologyGroup group in topology.GroupsInLogicalOrder)
                    {
                        yield return new MatrixDeclaration(
                            descriptor,
                            GameplaySkinResolvedMaterialTarget.ForGroup(group),
                            $"node.slot-{++nodeIndex:000}");
                    }

                    continue;
                }

                if ((descriptor.AllowedScopes & GameplaySkinSlotScope.Lane) == 0)
                    throw new InvalidOperationException($"Catalog slot {descriptor.Id} has no public target scope.");

                foreach (GameplaySkinLaneTopologyGroup group in topology.GroupsInLogicalOrder)
                {
                    GameplaySkinLaneTopologyEntry? lane = descriptor.CatalogFamily == GameplaySkinSlotCatalogFamily.Bms
                        ? group.LanesInLogicalOrder.SingleOrDefault(candidate => candidate.Identity.Role == GameplaySkinLaneRole.Scratch)
                        : group.LanesInLogicalOrder.FirstOrDefault(candidate => candidate.Identity.Role != GameplaySkinLaneRole.Scratch);

                    if (lane == null)
                        continue;

                    yield return new MatrixDeclaration(
                        descriptor,
                        GameplaySkinResolvedMaterialTarget.ForLane(group, lane),
                        $"node.slot-{++nodeIndex:000}");
                }
            }
        }

        private static void appendDocumentTarget(
            StringBuilder output,
            GameplaySkinResolvedMaterialTarget target,
            string keymodeId,
            string stageMode)
        {
            output.Append("Target: ").Append(target.Kind switch
            {
                GameplaySkinResolvedMaterialTargetKind.Global => "Global",
                GameplaySkinResolvedMaterialTargetKind.Stage => "Stage",
                GameplaySkinResolvedMaterialTargetKind.Group => "Group",
                GameplaySkinResolvedMaterialTargetKind.Lane => "Lane",
                _ => throw new ArgumentOutOfRangeException(nameof(target)),
            });
            output.Append(" ruleset=bms keymode=").Append(keymodeId).Append(" stage-mode=").Append(stageMode);

            if (target.GroupId != null)
            {
                output.Append(" group=").Append(target.GroupId.Value)
                      .Append(" group-logical=").Append(target.GroupLogicalIndex)
                      .Append(" group-visual=").Append(target.GroupVisualIndex);
            }

            if (target.LaneId != null)
            {
                output.Append(" lane=").Append(target.LaneId.Value)
                      .Append(" global-logical=").Append(target.GlobalLogicalIndex)
                      .Append(" global-visual=").Append(target.GlobalVisualIndex)
                      .Append(" group-local-logical=").Append(target.GroupLocalLogicalIndex)
                      .Append(" group-local-visual=").Append(target.GroupLocalVisualIndex);
            }

            output.AppendLine();
        }

        private static void writeC5MatrixScene(string root, IReadOnlyList<MatrixDeclaration> declarations)
        {
            File.WriteAllText(
                Path.Combine(root, GameplaySkinSceneContracts.MANIFEST_FILE_NAME),
                new JObject
                {
                    ["contract"] = GameplaySkinSceneContracts.MANIFEST_CONTRACT_ID,
                    ["scene"] = GameplaySkinSceneContracts.SCENE_FILE_NAME,
                    ["sceneContract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
                    ["eventContract"] = GameplaySkinSceneContracts.EVENT_CONTRACT_ID,
                    ["resources"] = new JArray(new JObject
                    {
                        ["id"] = "texture.matrix",
                        ["type"] = "texture",
                        ["path"] = "notes/matrix.png",
                    }),
                }.ToString(Formatting.Indented));

            var children = new JArray();

            foreach (MatrixDeclaration declaration in declarations)
            {
                children.Add(new JObject
                {
                    ["id"] = declaration.NodeId,
                    ["type"] = "sprite",
                    ["target"] = createSceneTarget(declaration.Target),
                    ["slot"] = declaration.Slot.Id,
                    ["resource"] = "texture.matrix",
                    ["blend"] = ReferenceEquals(declaration.Slot, GameplaySkinSlotCatalog.HitExplosion) ? "additive" : "alpha",
                    ["properties"] = new JObject
                    {
                        ["opacity"] = 0.75,
                        ["visible"] = true,
                    },
                    ["effects"] = new JArray(),
                    ["children"] = new JArray(),
                });
            }

            File.WriteAllText(
                Path.Combine(root, GameplaySkinSceneContracts.SCENE_FILE_NAME),
                new JObject
                {
                    ["contract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
                    ["root"] = new JObject
                    {
                        ["id"] = "node.root",
                        ["type"] = "container",
                        ["target"] = new JObject { ["kind"] = "global" },
                        ["blend"] = "inherit",
                        ["properties"] = new JObject(),
                        ["effects"] = new JArray(),
                        ["children"] = children,
                    },
                    ["tracks"] = new JArray(),
                    ["stateMachines"] = new JArray(),
                    ["bindings"] = new JArray(),
                    ["templates"] = new JArray(),
                    ["instances"] = new JArray(),
                }.ToString(Formatting.Indented));
        }

        private static JObject createSceneTarget(GameplaySkinResolvedMaterialTarget target)
        {
            var result = new JObject
            {
                ["kind"] = target.Kind switch
                {
                    GameplaySkinResolvedMaterialTargetKind.Global => "global",
                    GameplaySkinResolvedMaterialTargetKind.Stage => "stage",
                    GameplaySkinResolvedMaterialTargetKind.Group => "group",
                    GameplaySkinResolvedMaterialTargetKind.Lane => "lane",
                    _ => throw new ArgumentOutOfRangeException(nameof(target)),
                },
            };

            if (target.LaneId != null)
            {
                result["id"] = target.LaneId.Value;
                result["index"] = target.GlobalLogicalIndex;
            }
            else if (target.GroupId != null)
            {
                result["id"] = target.GroupId.Value;
                result["index"] = target.GroupLogicalIndex;
            }

            return result;
        }

        private static string createC5MatrixChart(BmsKeymode keymode)
        {
            string evidence = keymode switch
            {
                BmsKeymode.Key5K => "#00111:0100\n#00115:0100\n#00116:0100\n#00151:02000200\n#001D1:00AA0000",
                BmsKeymode.Key7K => "#00111:0100\n#00116:0100\n#00118:0100\n#00119:0100\n#00151:02000200\n#001D1:00AA0000",
                BmsKeymode.Key9K_Bms or BmsKeymode.Key9K_Pms =>
                    string.Join("\n", Enumerable.Range(0x11, 9).Select(channel => $"#001{channel:X2}:0100"))
                    + "\n#00151:02000200\n#001D1:00AA0000",
                BmsKeymode.Key14K =>
                    "#00111:0100\n#00116:0100\n#00118:0100\n#00119:0100\n"
                    + "#00121:0100\n#00126:0100\n#00128:0100\n#00129:0100\n"
                    + "#00151:02000200\n#00161:02000200\n#001D1:00AA0000\n#001E1:00AA0000",
                _ => throw new ArgumentOutOfRangeException(nameof(keymode)),
            };

            return "#TITLE C5 all-keymode production matrix\n"
                   + "#BPM 120\n"
                   + "#RANK 2\n"
                   + "#LNTYPE 1\n"
                   + "#WAV01 matrix-note.wav\n"
                   + "#WAV02 matrix-hold.wav\n"
                   + evidence + "\n";
        }

        private static string createC5ModChart()
            => "#TITLE C5 current package mod identity\n"
               + "#BPM 120\n"
               + "#RANK 2\n"
               + "#WAVAA moved.wav\n"
               + "#WAVB1 key-2.wav\n"
               + "#WAVB2 key-6.wav\n"
               + "#WAVBB key-7.wav\n"
               + "#00111:B100\n"
               + "#00112:AA00\n"
               + "#00118:B200\n"
               + "#00119:BB00\n";

        private static Mod createLaneMod(C5LaneModKind kind, int seed)
            => kind switch
            {
                C5LaneModKind.Mirror => new BmsModMirror(),
                C5LaneModKind.SeededRandom => new BmsModRandom
                {
                    Seed = { Value = seed },
                    RandomMode = { Value = BmsRandomMode.Random },
                },
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown BMS lane production mod."),
            };

        private static void assertSeededRandomIsDeterministic(int actualLane, int seed)
        {
            var ruleset = new BmsRuleset();
            BmsDecodedChart decoded = new BmsBeatmapDecoder().DecodeText(createC5ModChart(), "c5-current-package-mod.bme");
            var duplicate = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decoded), ruleset).Convert();
            var duplicateMod = (BmsModRandom)createLaneMod(C5LaneModKind.SeededRandom, seed);
            duplicateMod.ApplyToBeatmap(duplicate);
            BmsHitObject duplicateMoved = duplicate.HitObjects.OfType<BmsHitObject>()
                                                   .Single(note => note is not BmsHoldNote && note.KeysoundSample?.Filename == "moved.wav");

            Assert.Multiple(() =>
            {
                Assert.That(duplicateMod.Seed.Value, Is.EqualTo(seed));
                Assert.That(duplicateMoved.LaneIndex, Is.EqualTo(actualLane));
            });
        }

        private static IEnumerable<GameplaySkinObjectStateSnapshot> getObservedObjectStates(
            IEnumerable<GameplaySkinEventEnvelope> envelopes)
        {
            foreach (GameplaySkinEventEnvelope envelope in envelopes)
            {
                if (envelope.Payload is GameplaySkinObjectEventPayload objectPayload)
                    yield return objectPayload.State;
                else if (envelope.Payload is GameplaySkinStateEventPayload statePayload)
                {
                    foreach (GameplaySkinObjectStateSnapshot state in statePayload.State.ActiveObjects)
                        yield return state;
                }
                else if (envelope.Payload is GameplaySkinPublicationEventPayload publicationPayload)
                {
                    foreach (GameplaySkinObjectStateSnapshot state in publicationPayload.State.ActiveObjects)
                        yield return state;
                }
            }
        }

        private static string getSkinKeymode(BmsKeymode keymode)
            => keymode switch
            {
                BmsKeymode.Key5K => "5K",
                BmsKeymode.Key7K => "7K",
                BmsKeymode.Key9K_Bms => "9K",
                BmsKeymode.Key9K_Pms => "9K_PMS",
                BmsKeymode.Key14K => "14K",
                _ => throw new ArgumentOutOfRangeException(nameof(keymode)),
            };

        private static string getKeymodeId(BmsKeymode keymode)
            => keymode switch
            {
                BmsKeymode.Key5K => "5k",
                BmsKeymode.Key7K => "7k",
                BmsKeymode.Key9K_Bms => "9k-bms",
                BmsKeymode.Key9K_Pms => "9k-pms",
                BmsKeymode.Key14K => "14k",
                _ => throw new ArgumentOutOfRangeException(nameof(keymode)),
            };

        private sealed class ExactBmsProductionFixture
        {
            public string ChartText { get; }

            public string ChartFilename { get; }

            public BmsPlayfieldStyle PlayfieldStyle { get; }

            public IReadOnlyList<Mod>? Mods { get; }

            public double InitialGameplayTime { get; }

            public Action<BmsBeatmap>? PrepareBeatmap { get; }

            public BmsKeymode? KeymodeOverride { get; }

            public ExactBmsProductionFixture(
                string chartText,
                string chartFilename,
                BmsPlayfieldStyle playfieldStyle,
                IReadOnlyList<Mod>? mods = null,
                double initialGameplayTime = 1_500,
                Action<BmsBeatmap>? prepareBeatmap = null,
                BmsKeymode? keymodeOverride = null)
            {
                ChartText = chartText;
                ChartFilename = chartFilename;
                PlayfieldStyle = playfieldStyle;
                Mods = mods;
                InitialGameplayTime = initialGameplayTime;
                PrepareBeatmap = prepareBeatmap;
                KeymodeOverride = keymodeOverride;
            }
        }

        private sealed class C5CurrentPackageSelection
        {
            public string PackageRoot { get; set; } = string.Empty;

            public Live<SkinInfo> Candidate { get; set; } = null!;
        }

        private readonly record struct MatrixDeclaration(
            GameplaySkinSlotDescriptor Slot,
            GameplaySkinResolvedMaterialTarget Target,
            string NodeId);

        public enum C5LaneModKind
        {
            Mirror,
            SeededRandom,
        }
    }
}
