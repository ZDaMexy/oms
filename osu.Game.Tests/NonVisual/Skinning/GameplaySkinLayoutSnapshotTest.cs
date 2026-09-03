// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Textures;
using osu.Game.Audio;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public class GameplaySkinLayoutSnapshotTest
    {
        [Test]
        public void TestDocumentBindsExactPublicationAndMatchesResolvedTargetCoordinates()
        {
            const string configuration = """
                                         [GameplaySkin.Common:1]
                                         Target: Lane ruleset=mania keymode=2k stage-mode=single group=test.group lane=test.lane-1 group-logical=0 group-visual=0 global-logical=0 global-visual=0 group-local-logical=0 group-local-visual=0
                                         object.note: resource Provide "note.png"
                                         """;

            byte[] configurationBytes = Encoding.UTF8.GetBytes(configuration);
            string configurationHash = Convert.ToHexString(SHA256.HashData(configurationBytes)).ToLowerInvariant();
            GameplaySkinDocument document = GameplaySkinDocumentCodec.Decode(
                configurationBytes,
                GameplaySkinDocumentIdentity.CreateUnboundPackageParse(configurationHash));
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            var recordId = new Guid("ee183c91-042f-484a-bb76-2cc7f568f831");
            using var ownerA = new TestSkin(recordId, "package-a");
            using var ownerB = new TestSkin(recordId, "package-b");
            var revisionA = new SkinCurrentRevision(
                17,
                recordId,
                "package-content-a",
                SkinCurrentRevisionSourceKind.ManagedFolder,
                ownerA,
                keepsReusableOwner: false,
                _ => { });
            var revisionB = new SkinCurrentRevision(
                17,
                recordId,
                "package-content-a",
                SkinCurrentRevisionSourceKind.ManagedFolder,
                ownerB,
                keepsReusableOwner: false,
                _ => { });
            GameplaySkinPackageRevision packageA = GameplaySkinPackageRevision.Create(revisionA);
            GameplaySkinPackageRevision packageB = GameplaySkinPackageRevision.Create(revisionB);
            GameplaySkinLayoutSnapshot publicationA = createSnapshot(topology, packageA, 9, 0.2f);
            GameplaySkinLayoutSnapshot sameValuesDifferentLayout = createSnapshot(topology, packageA, 9, 0.2f);
            GameplaySkinLayoutSnapshot nextLayout = createSnapshot(topology, packageA, 10, 0.2f);
            GameplaySkinLayoutSnapshot sameValuesDifferentPackage = createSnapshot(topology, packageB, 9, 0.2f);
            GameplaySkinDocument bound = document.BindToPublication(publicationA);
            GameplaySkinResolvedMaterialTarget resolvedTarget = GameplaySkinResolvedMaterialTarget.ForLane(
                topology.GroupsInLogicalOrder[0],
                topology.LanesInLogicalOrder[0]);
            GameplaySkinDocumentEntry entry = bound.GetEntry(GameplaySkinSlotCatalog.Note, resolvedTarget);
            GameplaySkinDocument identityOnlyForgery = document.WithIdentity(bound.Identity);
            GameplaySkinRuntimeCapabilitySet capabilities = GameplaySkinRuntimeCapabilitySet.Create(new[]
            {
                GameplaySkinRuntimeSlotSupport.Create(GameplaySkinSlotCatalog.Note, GameplaySkinRuntimeSlotCapability.Provide),
            });

            Assert.Multiple(() =>
            {
                Assert.That(bound.Identity.SourceKind, Is.EqualTo(GameplaySkinDocumentSourceKind.ManagedFolder));
                Assert.That(bound.Identity.SourceId, Is.EqualTo(recordId));
                Assert.That(bound.Identity.ContentRevision, Is.EqualTo(configurationHash));
                Assert.That(bound.Identity.PackageRevision, Is.EqualTo(17));
                Assert.That(bound.Identity.CurrentRevision, Is.EqualTo(17));
                Assert.That(bound.Identity.LayoutRevision, Is.EqualTo(9));
                Assert.That(document.Identity.IsBound, Is.False);
                Assert.That(identityOnlyForgery.Identity.IsBound, Is.True);
                Assert.That(() => new GameplaySkinDocumentSlotProvider<string, TestMaterial>(
                    document,
                    capabilities,
                    "selected",
                    _ => resolvedTarget,
                    (_, _) => new TestMaterial("unreachable")), Throws.ArgumentException);
                Assert.That(() => new GameplaySkinDocumentSlotProvider<string, TestMaterial>(
                    identityOnlyForgery,
                    capabilities,
                    "selected",
                    _ => resolvedTarget,
                    (_, _) => new TestMaterial("unreachable")), Throws.ArgumentException);
                Assert.That(() => new GameplaySkinDocumentSlotProvider<string, TestMaterial>(
                    bound,
                    capabilities,
                    "selected",
                    _ => resolvedTarget,
                    (_, _) => new TestMaterial("note")), Throws.Nothing);
                Assert.That(bound.BindToPublication(publicationA), Is.SameAs(bound));
                Assert.That(() => bound.BindToPublication(sameValuesDifferentLayout), Throws.InvalidOperationException);
                Assert.That(() => bound.BindToPublication(nextLayout), Throws.InvalidOperationException);
                Assert.That(() => bound.BindToPublication(sameValuesDifferentPackage), Throws.InvalidOperationException);

                Assert.That(entry.Presence, Is.EqualTo(GameplaySkinDocumentDeclarationPresence.Declared));
                Assert.That(entry.Validity, Is.EqualTo(GameplaySkinDocumentValueValidity.Valid));
                Assert.That(entry.Operation, Is.EqualTo(GameplaySkinDocumentOperation.Provide));
                Assert.That(entry.Descriptor, Is.SameAs(GameplaySkinSlotCatalog.Note));
                Assert.That(entry.DeclaredSlotId, Is.EqualTo(GameplaySkinSlotCatalog.Note.Id));
                Assert.That(entry.Target.GroupId, Is.EqualTo(resolvedTarget.GroupId));
                Assert.That(entry.Target.LaneId, Is.EqualTo(resolvedTarget.LaneId));
                Assert.That(entry.Target.GroupLogicalIndex, Is.EqualTo(resolvedTarget.GroupLogicalIndex));
                Assert.That(entry.Target.GroupVisualIndex, Is.EqualTo(resolvedTarget.GroupVisualIndex));
                Assert.That(entry.Target.GlobalLogicalIndex, Is.EqualTo(resolvedTarget.GlobalLogicalIndex));
                Assert.That(entry.Target.GlobalVisualIndex, Is.EqualTo(resolvedTarget.GlobalVisualIndex));
                Assert.That(entry.Target.GroupLocalLogicalIndex, Is.EqualTo(resolvedTarget.GroupLocalLogicalIndex));
                Assert.That(entry.Target.GroupLocalVisualIndex, Is.EqualTo(resolvedTarget.GroupLocalVisualIndex));
            });

            GameplaySkinDocumentTarget[] driftedTargets =
            {
                GameplaySkinDocumentTarget.ForLane(GameplaySkinLaneGroupId.Create("test.other-group"), resolvedTarget.LaneId!, 0, 0, 0, 0, 0, 0),
                GameplaySkinDocumentTarget.ForLane(resolvedTarget.GroupId!, GameplaySkinLaneId.Create("test.other-lane"), 0, 0, 0, 0, 0, 0),
                GameplaySkinDocumentTarget.ForLane(resolvedTarget.GroupId!, resolvedTarget.LaneId!, 1, 0, 0, 0, 0, 0),
                GameplaySkinDocumentTarget.ForLane(resolvedTarget.GroupId!, resolvedTarget.LaneId!, 0, 1, 0, 0, 0, 0),
                GameplaySkinDocumentTarget.ForLane(resolvedTarget.GroupId!, resolvedTarget.LaneId!, 0, 0, 1, 0, 0, 0),
                GameplaySkinDocumentTarget.ForLane(resolvedTarget.GroupId!, resolvedTarget.LaneId!, 0, 0, 0, 1, 0, 0),
                GameplaySkinDocumentTarget.ForLane(resolvedTarget.GroupId!, resolvedTarget.LaneId!, 0, 0, 0, 0, 1, 0),
                GameplaySkinDocumentTarget.ForLane(resolvedTarget.GroupId!, resolvedTarget.LaneId!, 0, 0, 0, 0, 0, 1),
            };

            foreach (GameplaySkinDocumentTarget drifted in driftedTargets)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(drifted.Matches(resolvedTarget), Is.False);
                    Assert.That(bound.GetEntry(GameplaySkinSlotCatalog.Note, drifted).Presence,
                        Is.EqualTo(GameplaySkinDocumentDeclarationPresence.Absent));
                });
            }
        }

        [TestCase("other.group", "test.lane-1", 0, 0, 0, 0, 0, 0)]
        [TestCase("test.group", "other.lane", 0, 0, 0, 0, 0, 0)]
        [TestCase("test.group", "test.lane-1", 1, 0, 0, 0, 0, 0)]
        [TestCase("test.group", "test.lane-1", 0, 1, 0, 0, 0, 0)]
        [TestCase("test.group", "test.lane-1", 0, 0, 1, 0, 0, 0)]
        [TestCase("test.group", "test.lane-1", 0, 0, 0, 1, 0, 0)]
        [TestCase("test.group", "test.lane-1", 0, 0, 0, 0, 1, 0)]
        [TestCase("test.group", "test.lane-1", 0, 0, 0, 0, 0, 1)]
        public void TestBoundDocumentDiagnosesEveryExactTargetCoordinateDrift(
            string groupId,
            string laneId,
            int groupLogical,
            int groupVisual,
            int globalLogical,
            int globalVisual,
            int groupLocalLogical,
            int groupLocalVisual)
        {
            string configuration = $"""
                                    [GameplaySkin.Common:1]
                                    Target: Lane ruleset=mania keymode=2k stage-mode=single group={groupId} lane={laneId} group-logical={groupLogical} group-visual={groupVisual} global-logical={globalLogical} global-visual={globalVisual} group-local-logical={groupLocalLogical} group-local-visual={groupLocalVisual}
                                    object.note: resource Provide "note.png"
                                    """;
            GameplaySkinDocument parsed = GameplaySkinDocumentCodec.Decode(
                configuration,
                GameplaySkinDocumentIdentity.CreateUnboundPackageParse("target-drift"));
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            GameplaySkinLayoutSnapshot snapshot = createSnapshot(topology, package, 0, 0.2f);
            GameplaySkinDocument bound = parsed.BindToPublication(snapshot);
            GameplaySkinResolvedMaterialTarget target = GameplaySkinResolvedMaterialTarget.ForLane(
                topology.GroupsInLogicalOrder[0],
                topology.LanesInLogicalOrder[0]);
            GameplaySkinCodecDiagnostic diagnostic = bound.Diagnostics.Single();

            Assert.Multiple(() =>
            {
                Assert.That(parsed.Diagnostics, Is.Empty);
                Assert.That(diagnostic.Code, Is.EqualTo(GameplaySkinCodecDiagnosticCode.InvalidPublicationTarget));
                Assert.That(diagnostic.Id, Is.EqualTo("OMS-SKIN-CODEC-021"));
                Assert.That(diagnostic.SlotId, Is.EqualTo(GameplaySkinSlotCatalog.Note.Id));
                Assert.That(diagnostic.ToString(), Does.Not.Contain(groupId));
                Assert.That(diagnostic.ToString(), Does.Not.Contain(laneId));
                Assert.That(bound.GetEntry(GameplaySkinSlotCatalog.Note, target).Presence,
                    Is.EqualTo(GameplaySkinDocumentDeclarationPresence.Absent));
            });
        }

        [Test]
        public void TestBoundDocumentUsesFrozenRulesetKeymodeStageAndScopeSpecificity()
        {
            const string configuration = """
                                         [GameplaySkin.Common:1]
                                         Target: Lane ruleset=any keymode=any stage-mode=any group=bms.group.deck-1 lane=bms.lane.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=1 group-local-visual=1
                                         object.note: resource Provide "broad"
                                         Target: Lane ruleset=any keymode=5k stage-mode=any group=bms.group.deck-1 lane=bms.lane.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=1 group-local-visual=1
                                         object.note: resource Provide "keymode-only"
                                         Target: Lane ruleset=bms keymode=any stage-mode=any group=bms.group.deck-1 lane=bms.lane.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=1 group-local-visual=1
                                         object.note: resource Provide "ruleset-only"
                                         Target: Lane ruleset=bms keymode=5k stage-mode=single group=bms.group.deck-1 lane=bms.lane.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=1 group-local-visual=1
                                         object.note: resource Inherit
                                         Target: Global ruleset=bms keymode=5k stage-mode=single
                                         decoration: resource Provide "global"
                                         Target: Stage ruleset=bms keymode=5k stage-mode=single group=bms.group.deck-1 group-logical=0 group-visual=0
                                         decoration: resource Provide "stage"
                                         Target: Group ruleset=bms keymode=5k stage-mode=single group=bms.group.deck-1 group-logical=0 group-visual=0
                                         decoration: resource Provide "group"
                                         Target: Lane ruleset=bms keymode=5k stage-mode=single group=bms.group.deck-1 lane=bms.lane.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=1 group-local-visual=1
                                         decoration: resource Provide "lane"
                                         """;

            GameplaySkinDocument parsed = GameplaySkinDocumentCodec.Decode(
                configuration,
                GameplaySkinDocumentIdentity.CreateUnboundPackageParse("selector.source"));
            GameplaySkinDocument roundTrip = GameplaySkinDocumentCodec.Decode(
                GameplaySkinDocumentCodec.Encode(parsed),
                GameplaySkinDocumentIdentity.CreateUnboundPackageParse("selector.source"));
            GameplaySkinLayoutSnapshot bms5 = createBmsSnapshot("5k", 5, includeScratch: true);
            GameplaySkinLayoutSnapshot bms7 = createBmsSnapshot("7k", 7, includeScratch: true);
            GameplaySkinDocument bound5 = parsed.BindToPublication(bms5);
            GameplaySkinDocument bound7 = roundTrip.BindToPublication(bms7);
            GameplaySkinLaneTopologyGroup group5 = bms5.Context.Topology.GroupsInLogicalOrder[0];
            GameplaySkinLaneTopologyEntry lane5 = bms5.Context.Topology.LanesInLogicalOrder
                                                               .Single(lane => lane.Identity.Id.Value == "bms.lane.key-1");
            GameplaySkinLaneTopologyGroup group7 = bms7.Context.Topology.GroupsInLogicalOrder[0];
            GameplaySkinLaneTopologyEntry lane7 = bms7.Context.Topology.LanesInLogicalOrder
                                                               .Single(lane => lane.Identity.Id.Value == "bms.lane.key-1");
            GameplaySkinResolvedMaterialTarget laneTarget5 = GameplaySkinResolvedMaterialTarget.ForLane(group5, lane5);
            GameplaySkinDocumentEntry note5 = bound5.GetEntry(GameplaySkinSlotCatalog.Note, laneTarget5);
            GameplaySkinDocumentEntry note7 = bound7.GetEntry(
                GameplaySkinSlotCatalog.Note,
                GameplaySkinResolvedMaterialTarget.ForLane(group7, lane7));
            GameplaySkinRuntimeCapabilitySet capabilities = GameplaySkinRuntimeCapabilitySet.Create(new[]
            {
                GameplaySkinRuntimeSlotSupport.Create(GameplaySkinSlotCatalog.Note, GameplaySkinRuntimeSlotCapability.Provide),
            });
            var provider = new GameplaySkinDocumentSlotProvider<string, TestMaterial>(
                bound5,
                capabilities,
                "selected-document",
                _ => laneTarget5,
                (entry, _) => new TestMaterial(entry.Value!));
            SkinSlotResult<TestMaterial> shadowed = provider.GetSlot(
                new GameplaySkinSlotLookup<string>(GameplaySkinSlotCatalog.Note, "lane"));

            Assert.Multiple(() =>
            {
                Assert.That(parsed.Diagnostics, Is.Empty);
                Assert.That(roundTrip.Diagnostics, Is.Empty);
                Assert.That(note5.Operation, Is.EqualTo(GameplaySkinDocumentOperation.Inherit));
                Assert.That(shadowed.Kind, Is.EqualTo(SkinSlotResultKind.Inherit));
                Assert.That(note7.Value, Is.EqualTo("ruleset-only"),
                    "Ruleset specificity is evaluated before keymode specificity.");
                Assert.That(bound5.GetEntry(GameplaySkinSlotCatalog.Decoration, GameplaySkinResolvedMaterialTarget.Global).Value,
                    Is.EqualTo("global"));
                Assert.That(bound5.GetEntry(GameplaySkinSlotCatalog.Decoration, GameplaySkinResolvedMaterialTarget.ForStage(group5)).Value,
                    Is.EqualTo("stage"));
                Assert.That(bound5.GetEntry(GameplaySkinSlotCatalog.Decoration, GameplaySkinResolvedMaterialTarget.ForGroup(group5)).Value,
                    Is.EqualTo("group"));
                Assert.That(bound5.GetEntry(GameplaySkinSlotCatalog.Decoration, laneTarget5).Value,
                    Is.EqualTo("lane"));
                Assert.That(roundTrip.Sections.SelectMany(section => section.Entries).Select(entry => entry.Target),
                    Is.EqualTo(parsed.Sections.SelectMany(section => section.Entries).Select(entry => entry.Target)));
            });
        }

        [Test]
        public void TestMoreSpecificInvalidAndEmptyDeclarationsShadowBroadProvideBeforeNextAuthority()
        {
            const string configuration = """
                                         [GameplaySkin.Common:1]
                                         Target: Lane ruleset=any keymode=any stage-mode=any group=bms.group.deck-1 lane=bms.lane.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=1 group-local-visual=1
                                         object.note: resource Provide "broad-note"
                                         object.long-note.head: resource Provide "broad-head"
                                         Target: Lane ruleset=bms keymode=5k stage-mode=single group=bms.group.deck-1 lane=bms.lane.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=1 group-local-visual=1
                                         object.note: colour Provide "invalid-specific"
                                         object.long-note.head: resource Provide ""
                                         """;

            GameplaySkinLayoutSnapshot snapshot = createBmsSnapshot("5k", 5, includeScratch: true);
            GameplaySkinDocument document = GameplaySkinDocumentCodec.Decode(
                configuration,
                GameplaySkinDocumentIdentity.CreateUnboundPackageParse("selector.shadow"))
                                                                    .BindToPublication(snapshot);
            GameplaySkinLaneTopologyGroup group = snapshot.Context.Topology.GroupsInLogicalOrder[0];
            GameplaySkinLaneTopologyEntry lane = snapshot.Context.Topology.LanesInLogicalOrder
                                                               .Single(entry => entry.Identity.Id.Value == "bms.lane.key-1");
            GameplaySkinResolvedMaterialTarget target = GameplaySkinResolvedMaterialTarget.ForLane(group, lane);
            GameplaySkinDocumentEntry invalid = document.GetEntry(GameplaySkinSlotCatalog.Note, target);
            GameplaySkinDocumentEntry empty = document.GetEntry(GameplaySkinSlotCatalog.LongNoteHead, target);
            GameplaySkinRuntimeCapabilitySet capabilities = GameplaySkinRuntimeCapabilitySet.Create(new[]
            {
                GameplaySkinRuntimeSlotSupport.Create(GameplaySkinSlotCatalog.Note, GameplaySkinRuntimeSlotCapability.Provide),
                GameplaySkinRuntimeSlotSupport.Create(GameplaySkinSlotCatalog.LongNoteHead, GameplaySkinRuntimeSlotCapability.Provide),
            });
            var selected = new GameplaySkinDocumentSlotProvider<string, TestMaterial>(
                document,
                capabilities,
                "selected-document",
                _ => target,
                (entry, _) => new TestMaterial(entry.Value!));
            var fallback = new StaticMaterialProvider("programmatic-fallback", new TestMaterial("lower-authority"));
            IGameplaySkinSlotProvider<GameplaySkinSlotLookup<string>, TestMaterial>[] providers = { selected, fallback };
            GameplaySkinSlotResolution<TestMaterial> note = GameplaySkinSlotResolver.Resolve(
                GameplaySkinSlotCatalog.Note,
                "lane",
                providers);
            GameplaySkinSlotResolution<TestMaterial> head = GameplaySkinSlotResolver.Resolve(
                GameplaySkinSlotCatalog.LongNoteHead,
                "lane",
                providers);

            Assert.Multiple(() =>
            {
                Assert.That(invalid.Validity, Is.EqualTo(GameplaySkinDocumentValueValidity.Invalid));
                Assert.That(invalid.Value, Is.EqualTo("invalid-specific"));
                Assert.That(empty.Validity, Is.EqualTo(GameplaySkinDocumentValueValidity.Empty));
                Assert.That(empty.Value, Is.Empty);
                Assert.That(note.Result.Kind, Is.EqualTo(SkinSlotResultKind.Provide));
                Assert.That(note.Result.Value.Name, Is.EqualTo("lower-authority"));
                Assert.That(note.ProviderName, Is.EqualTo("programmatic-fallback"));
                Assert.That(note.Diagnostics.Single().Code, Is.EqualTo(GameplaySkinSlotDiagnosticCode.ProviderFailed));
                Assert.That(note.Diagnostics.Single().SlotId, Is.EqualTo(GameplaySkinSlotCatalog.Note.Id));
                Assert.That(((GameplaySkinDocumentSlotRejectedException)note.Diagnostics.Single().Exception!).Code,
                    Is.EqualTo("gameplay-skin.entry-invalid"));
                Assert.That(head.Result.Kind, Is.EqualTo(SkinSlotResultKind.Provide));
                Assert.That(head.Result.Value.Name, Is.EqualTo("lower-authority"));
                Assert.That(head.ProviderName, Is.EqualTo("programmatic-fallback"));
                Assert.That(head.Diagnostics.Single().Code, Is.EqualTo(GameplaySkinSlotDiagnosticCode.ProviderFailed));
                Assert.That(head.Diagnostics.Single().SlotId, Is.EqualTo(GameplaySkinSlotCatalog.LongNoteHead.Id));
                Assert.That(((GameplaySkinDocumentSlotRejectedException)head.Diagnostics.Single().Exception!).Code,
                    Is.EqualTo("gameplay-skin.entry-empty"));
                Assert.That(document.Diagnostics.Count(diagnostic => diagnostic.Code == GameplaySkinCodecDiagnosticCode.InvalidValueType),
                    Is.EqualTo(1));
            });
        }

        [Test]
        public void TestSnapshotDefensivelyCopiesEveryCollectionAndUsesExactTopologyEntries()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            GameplaySkinLayoutContext context = createContext(topology, package, 0);
            var group = new GameplaySkinLayoutGroup(topology.GroupsInLogicalOrder[0], rect(0.2f, 0, 0.6f, 0.9f));
            var firstLane = new GameplaySkinLayoutLane(topology.LanesInLogicalOrder[0], rect(0.2f, 0, 0.3f, 0.9f));
            var secondLane = new GameplaySkinLayoutLane(topology.LanesInLogicalOrder[1], rect(0.5f, 0, 0.3f, 0.9f));
            var surface = new GameplaySkinLayoutSurface("playfield", group.Rect, 0, true, true);
            GameplaySkinLayoutGroup[] groups = { group };
            GameplaySkinLayoutLane[] lanes = { firstLane, secondLane };
            GameplaySkinLayoutSurface[] surfaces = { surface };
            GameplaySkinLayoutRect[] viewports = { rect(0.82f, 0.02f, 0.16f, 0.18f) };
            GameplaySkinLayoutDiagnostic[] diagnostics = { new GameplaySkinLayoutDiagnostic("layout.width.fallback") };

            GameplaySkinLayoutSnapshot snapshot = GameplaySkinLayoutSnapshot.Create(
                context, groups, lanes, surfaces, viewports, diagnostics);

            groups[0] = null!;
            lanes[0] = null!;
            surfaces[0] = null!;
            viewports[0] = rect(0.01f, 0.01f, 0.05f, 0.05f);
            diagnostics[0] = null!;

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Context, Is.SameAs(context));
                Assert.That(snapshot.GroupsInLogicalOrder.Single(), Is.SameAs(group));
                Assert.That(snapshot.LanesInLogicalOrder, Is.EqualTo(new[] { firstLane, secondLane }));
                Assert.That(snapshot.GetLane(firstLane.LaneId), Is.SameAs(firstLane));
                Assert.That(snapshot.GetGroup(group.GroupId), Is.SameAs(group));
                Assert.That(snapshot.GetSurface("playfield"), Is.SameAs(surface));
                Assert.That(snapshot.BgaViewports.Single(), Is.EqualTo(rect(0.82f, 0.02f, 0.16f, 0.18f)));
                Assert.That(snapshot.Diagnostics.Single().Code, Is.EqualTo("layout.width.fallback"));
            });
        }

        [Test]
        public void TestGeometryRejectsNonFiniteNonPositiveAndOutOfSafeBounds()
        {
            Assert.Multiple(() =>
            {
                Assert.That(() => GameplaySkinLayoutRect.Create(float.NaN, 0, 1, 1), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => GameplaySkinLayoutRect.Create(0, float.PositiveInfinity, 1, 1), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => GameplaySkinLayoutRect.Create(0, 0, 0, 1), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => GameplaySkinLayoutRect.Create(0, 0, 1, -1), Throws.TypeOf<ArgumentOutOfRangeException>());
            });

            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            GameplaySkinLayoutContext context = createContext(topology, package, 0);
            var group = new GameplaySkinLayoutGroup(topology.GroupsInLogicalOrder[0], rect(0.2f, 0, 0.6f, 0.9f));

            Assert.That(() => GameplaySkinLayoutSnapshot.Create(
                context,
                new[] { group },
                new[]
                {
                    new GameplaySkinLayoutLane(topology.LanesInLogicalOrder[0], rect(0.2f, 0, 0.3f, 0.9f)),
                    new GameplaySkinLayoutLane(topology.LanesInLogicalOrder[1], rect(0.7f, 0, 0.2f, 0.9f)),
                },
                new[] { new GameplaySkinLayoutSurface("playfield", group.Rect, 0, true, true) }),
                Throws.ArgumentException);

            Assert.That(() => GameplaySkinLayoutContext.Create(
                "test", "test.native", "test.two-key", "test.center", topology,
                rect(0, 0, 1, 1), rect(0, 0, 1, 1), 16f / 9f, 1,
                (GameplaySkinScrollDirection)999, package, 0, 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void TestRevisionOwnerIsLatestWinsAndFailureKeepsExactOldReference()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            var owner = new GameplaySkinLayoutRevisionOwner(package);
            GameplaySkinPreparedLayout first = owner.Prepare(revision => createSnapshot(topology, package, revision, 0.2f));
            GameplaySkinPreparedLayout latest = owner.Prepare(revision => createSnapshot(topology, package, revision, 0.25f));

            Assert.Multiple(() =>
            {
                Assert.That(owner.TryCommit(first), Is.False);
                Assert.That(owner.Current, Is.Null);
                Assert.That(owner.TryCommit(latest), Is.True);
                Assert.That(owner.Current, Is.SameAs(latest.Snapshot));
                Assert.That(owner.Current!.Context.LayoutRevision, Is.Zero);
            });

            GameplaySkinLayoutSnapshot previous = owner.Current!;
            Assert.That(() => owner.Prepare(_ => throw new InvalidOperationException("solve failed")), Throws.InvalidOperationException);
            Assert.That(owner.Current, Is.SameAs(previous));
        }

        [Test]
        public void TestNeutralSnapshotAndRulesetAdapterCommitAsOnePublicationReference()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            var owner = new GameplaySkinLayoutRevisionOwner(package);
            TestLayoutAdapter? adapter = null;
            GameplaySkinPreparedLayout prepared = owner.PreparePublication(revision =>
            {
                adapter = new TestLayoutAdapter(createSnapshot(topology, package, revision, 0.2f));
                return GameplaySkinLayoutPublication.Create(adapter);
            });

            Assert.That(owner.TryCommit(prepared), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(owner.CurrentPublication, Is.SameAs(prepared.Publication));
                Assert.That(owner.Current, Is.SameAs(prepared.Snapshot));
                Assert.That(owner.CurrentPublication!.Snapshot, Is.SameAs(prepared.Snapshot));
                Assert.That(owner.CurrentPublication.GetAdapter<TestLayoutAdapter>(), Is.SameAs(adapter));
                Assert.That(adapter!.Snapshot, Is.SameAs(owner.Current));
            });
        }

        [Test]
        public void TestLayoutAdapterAndResolvedMaterialSetCommitAsOneExactPublicationReference()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            GameplaySkinLayoutSnapshot snapshot = createSnapshot(topology, package, 0, 0.2f);
            var adapter = new TestLayoutAdapter(snapshot);
            GameplaySkinLaneTopologyGroup group = topology.GroupsInLogicalOrder[0];
            GameplaySkinLaneTopologyEntry lane = topology.LanesInLogicalOrder[0];
            GameplaySkinResolvedMaterialTarget target = GameplaySkinResolvedMaterialTarget.ForLane(group, lane);
            GameplaySkinResolvedMaterialSourceIdentity source = GameplaySkinResolvedMaterialSourceIdentity.Create(
                GameplaySkinResolvedMaterialSourceKind.SelectedPackage,
                "selected.current",
                "content-a");
            GameplaySkinMaterialContractIdentity contract = GameplaySkinMaterialContractIdentity.CurrentFor(snapshot);
            GameplaySkinResolvedMaterialEntry[] entries =
            {
                GameplaySkinResolvedMaterialEntry.Provide(GameplaySkinSlotCatalog.Note, target, source, new TestMaterial("note-a")),
                GameplaySkinResolvedMaterialEntry.Suppress(GameplaySkinSlotCatalog.LongNoteTail, target, source),
            };
            GameplaySkinResolvedMaterialDiagnostic[] diagnostics =
            {
                new GameplaySkinResolvedMaterialDiagnostic(
                    "skin.material.optional-suppressed",
                    entries[1].Key,
                    source),
            };
            GameplaySkinResolvedMaterialSet materialSet = GameplaySkinResolvedMaterialSet.Create(
                snapshot,
                contract,
                entries,
                diagnostics);

            GameplaySkinLayoutPublication publication = GameplaySkinLayoutPublication.Create(adapter, materialSet);
            entries[0] = null!;
            diagnostics[0] = null!;

            Assert.Multiple(() =>
            {
                Assert.That(publication.Snapshot, Is.SameAs(snapshot));
                Assert.That(publication.MaterialSet, Is.SameAs(materialSet));
                Assert.That(materialSet.Snapshot, Is.SameAs(snapshot));
                Assert.That(materialSet.PackageRevision, Is.SameAs(package));
                Assert.That(materialSet.LayoutRevision, Is.Zero);
                Assert.That(materialSet.ContractIdentity, Is.SameAs(contract));
                Assert.That(materialSet.Entries, Has.Count.EqualTo(2));
                Assert.That(materialSet.Diagnostics.Single().Code, Is.EqualTo("skin.material.optional-suppressed"));
                Assert.That(materialSet.Entries[0].TryGetMaterial<TestMaterial>(out _), Is.True);
                Assert.That(materialSet.Entries[0].GetMaterial<TestMaterial>().Name, Is.EqualTo("note-a"));
                Assert.That(materialSet.Entries[0].DeclaredValueType, Is.EqualTo(typeof(TestMaterial)));
                Assert.That(materialSet.Entries[1].State, Is.EqualTo(GameplaySkinResolvedMaterialState.Suppress));
                Assert.That(materialSet.Entries[1].TryGetMaterial<TestMaterial>(out _), Is.False);
                Assert.That(materialSet.Entries[0].Target.LaneId, Is.EqualTo(lane.Identity.Id));
                Assert.That(materialSet.Entries[0].Target.GroupId, Is.EqualTo(group.Identity.Id));
                Assert.That(materialSet.Entries[0].Target.GlobalLogicalIndex, Is.EqualTo(lane.GlobalLogicalIndex));
                Assert.That(materialSet.Entries[0].Target.GlobalVisualIndex, Is.EqualTo(lane.GlobalVisualIndex));
                Assert.That(materialSet.Entries[0].Target.GroupLocalLogicalIndex, Is.EqualTo(lane.GroupLocalLogicalIndex));
                Assert.That(materialSet.Entries[0].Target.GroupLocalVisualIndex, Is.EqualTo(lane.GroupLocalVisualIndex));
            });
        }

        [Test]
        public void TestPublicationRejectsMaterialSetFromAnotherLayoutSnapshot()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            GameplaySkinLayoutSnapshot first = createSnapshot(topology, package, 0, 0.2f);
            GameplaySkinLayoutSnapshot other = createSnapshot(topology, package, 0, 0.25f);
            GameplaySkinResolvedMaterialSet materialSet = GameplaySkinResolvedMaterialSet.Create(
                first,
                GameplaySkinMaterialContractIdentity.CurrentFor(first),
                Array.Empty<GameplaySkinResolvedMaterialEntry>());

            Assert.That(
                () => GameplaySkinLayoutPublication.Create(new TestLayoutAdapter(other), materialSet),
                Throws.ArgumentException);
        }

        [Test]
        public void TestCompatibilityPublicationCreatesExactEmptyMaterialSet()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            GameplaySkinLayoutSnapshot snapshot = createSnapshot(topology, package, 0, 0.2f);
            GameplaySkinLayoutPublication publication = GameplaySkinLayoutPublication.Create(new TestLayoutAdapter(snapshot));

            Assert.Multiple(() =>
            {
                Assert.That(publication.MaterialSet.IsEmpty, Is.True);
                Assert.That(publication.MaterialSet.Snapshot, Is.SameAs(snapshot));
                Assert.That(publication.MaterialSet.PackageRevision, Is.SameAs(package));
                Assert.That(publication.MaterialSet.LayoutRevision, Is.Zero);
                Assert.That(publication.MaterialSet.ContractIdentity, Is.EqualTo(GameplaySkinMaterialContractIdentity.CompatibilityEmpty));
            });
        }

        [Test]
        public void TestResolvedMaterialEntriesEnforceCatalogScopeAndSuppressEligibility()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneTopologyGroup group = topology.GroupsInLogicalOrder[0];
            GameplaySkinLaneTopologyEntry lane = topology.LanesInLogicalOrder[0];
            GameplaySkinResolvedMaterialSourceIdentity source = GameplaySkinResolvedMaterialSourceIdentity.Create(
                GameplaySkinResolvedMaterialSourceKind.SelectedPackage,
                "selected.current",
                "content-a");

            Assert.Multiple(() =>
            {
                Assert.That(
                    () => GameplaySkinResolvedMaterialEntry.Suppress(
                        GameplaySkinSlotCatalog.Note,
                        GameplaySkinResolvedMaterialTarget.ForLane(group, lane),
                        source),
                    Throws.ArgumentException);
                Assert.That(
                    () => GameplaySkinResolvedMaterialEntry.Provide(
                        GameplaySkinSlotCatalog.Note,
                        GameplaySkinResolvedMaterialTarget.ForStage(group),
                        source,
                        new TestMaterial("wrong-scope")),
                    Throws.ArgumentException);
                Assert.That(
                    () => GameplaySkinResolvedMaterialEntry.Provide(
                        GameplaySkinSlotCatalog.Note,
                        GameplaySkinResolvedMaterialTarget.ForLane(group, lane),
                        source,
                        typeof(string),
                        new TestMaterial("wrong-type")),
                    Throws.ArgumentException);
            });
        }

        [Test]
        public void TestGenericPublicMaterialResolutionCoversEveryBmsCatalogTargetAndPreservesThreeStates()
        {
            GameplaySkinLayoutSnapshot snapshot = createBmsSnapshot("5k", 5, includeScratch: true);
            const string configuration = """
                                         [GameplaySkin.Common:1]
                                         Target: Lane ruleset=bms keymode=5k stage-mode=single group=bms.group.deck-1 lane=bms.lane.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=1 group-local-visual=1
                                         playfield.lane-surface: resource Provide "surfaces/key-1"
                                         effect.key-flash: resource Suppress
                                         effect.hit-explosion: resource Provide "effects/missing"
                                         Target: Stage ruleset=bms keymode=5k stage-mode=single group=bms.group.deck-1 group-logical=0 group-visual=0
                                         playfield.judgement-line: resource Suppress
                                         """;
            GameplaySkinDocument document = GameplaySkinDocumentCodec.Decode(
                configuration,
                GameplaySkinDocumentIdentity.CreateUnboundPackageParse("public-slot-test"))
                                                                  .BindToPublication(snapshot);
            GameplaySkinRuntimeCapabilitySet capabilities = GameplaySkinPublicSlotMaterialCapabilities.Create(GameplaySkinSlotCatalog.All);
            GameplaySkinResolvedMaterialSourceIdentity selected = GameplaySkinResolvedMaterialSourceIdentity.Create(
                GameplaySkinResolvedMaterialSourceKind.SelectedPackage,
                "selected.public-test",
                "content-a");
            GameplaySkinResolvedMaterialSourceIdentity programmatic = GameplaySkinResolvedMaterialSourceIdentity.Create(
                GameplaySkinResolvedMaterialSourceKind.ProgrammaticFallback,
                "programmatic.public-test",
                "v1");
            Texture texture = (Texture)RuntimeHelpers.GetUninitializedObject(typeof(Texture));
            GameplaySkinPublicSlotMaterialResolution resolution = GameplaySkinPublicSlotMaterialResolver.Resolve(
                snapshot,
                document,
                capabilities,
                GameplaySkinSlotCatalog.All,
                selected,
                programmatic,
                resource => resource == "surfaces/key-1" ? texture : null);
            GameplaySkinLaneTopologyGroup group = snapshot.Context.Topology.GroupsInLogicalOrder[0];
            GameplaySkinLaneTopologyEntry lane = snapshot.Context.Topology.LanesInLogicalOrder
                                                           .Single(candidate => candidate.Identity.Id.Value == "bms.lane.key-1");
            GameplaySkinResolvedMaterialTarget laneTarget = GameplaySkinResolvedMaterialTarget.ForLane(group, lane);
            GameplaySkinResolvedMaterialTarget stageTarget = GameplaySkinResolvedMaterialTarget.ForStage(group);
            GameplaySkinResolvedMaterialSet materials = GameplaySkinResolvedMaterialSet.Create(
                snapshot,
                GameplaySkinMaterialContractIdentity.CurrentFor(snapshot),
                resolution.Entries,
                resolution.Diagnostics);

            Assert.Multiple(() =>
            {
                Assert.That(capabilities.Support, Has.Count.EqualTo(28));
                Assert.That(resolution.Entries, Has.Count.EqualTo(92));
                Assert.That(resolution.Entries.Select(entry => entry.Key), Is.Unique);
                Assert.That(resolution.Entries.Select(entry => entry.Slot).Distinct(), Is.EquivalentTo(GameplaySkinSlotCatalog.All));

                GameplaySkinResolvedMaterialEntry surface = materials.Entries.Single(entry =>
                    ReferenceEquals(entry.Slot, GameplaySkinSlotCatalog.LaneSurface) && entry.Target.Equals(laneTarget));
                GameplaySkinPublicSlotMaterial surfaceMaterial = surface.GetMaterial<GameplaySkinPublicSlotMaterial>();
                Assert.That(surface.Source, Is.EqualTo(selected));
                Assert.That(surfaceMaterial.ResourceName, Is.EqualTo("surfaces/key-1"));
                Assert.That(surfaceMaterial.Texture, Is.SameAs(texture));

                GameplaySkinResolvedMaterialEntry flash = materials.Entries.Single(entry =>
                    ReferenceEquals(entry.Slot, GameplaySkinSlotCatalog.KeyFlash) && entry.Target.Equals(laneTarget));
                Assert.That(flash.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Suppress));
                Assert.That(flash.Source, Is.EqualTo(selected));

                GameplaySkinResolvedMaterialEntry missing = materials.Entries.Single(entry =>
                    ReferenceEquals(entry.Slot, GameplaySkinSlotCatalog.HitExplosion) && entry.Target.Equals(laneTarget));
                Assert.That(missing.Source, Is.EqualTo(programmatic));
                Assert.That(missing.GetMaterial<GameplaySkinPublicSlotMaterial>().IsProgrammaticFallback, Is.True);
                Assert.That(materials.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == "gameplay-skin.public-resource.missing" && diagnostic.Key!.Equals(missing.Key)), Is.True);

                GameplaySkinResolvedMaterialEntry required = materials.Entries.Single(entry =>
                    ReferenceEquals(entry.Slot, GameplaySkinSlotCatalog.JudgementLine) && entry.Target.Equals(stageTarget));
                Assert.That(required.Source, Is.EqualTo(programmatic));
                Assert.That(materials.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == "gameplay-skin.entry-invalid" && diagnostic.Key!.Equals(required.Key)), Is.True);
                Assert.That(document.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == GameplaySkinCodecDiagnosticCode.SuppressionForbidden
                    && diagnostic.SlotId == GameplaySkinSlotCatalog.JudgementLine.Id), Is.True);

                Assert.That(materials.Entries.Where(entry => entry.Target.Kind == GameplaySkinResolvedMaterialTargetKind.Lane)
                                     .All(entry => entry.Target.GroupId != null
                                                   && entry.Target.LaneId != null
                                                   && entry.Target.GroupLogicalIndex.HasValue
                                                   && entry.Target.GroupVisualIndex.HasValue
                                                   && entry.Target.GlobalLogicalIndex.HasValue
                                                   && entry.Target.GlobalVisualIndex.HasValue
                                                   && entry.Target.GroupLocalLogicalIndex.HasValue
                                                   && entry.Target.GroupLocalVisualIndex.HasValue), Is.True);
            });
        }

        [Test]
        public void TestGenericPublicMaterialPreparationCancellationReturnsNoPartialResult()
        {
            GameplaySkinLayoutSnapshot snapshot = createBmsSnapshot("5k", 5, includeScratch: true);
            const string configuration = """
                                         [GameplaySkin.Common:1]
                                         Target: Lane ruleset=bms keymode=5k stage-mode=single group=bms.group.deck-1 lane=bms.lane.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=1 group-local-visual=1
                                         playfield.lane-surface: resource Provide "surfaces/key-1"
                                         """;
            GameplaySkinDocument document = GameplaySkinDocumentCodec.Decode(
                configuration,
                GameplaySkinDocumentIdentity.CreateUnboundPackageParse("public-slot-cancel"))
                                                                  .BindToPublication(snapshot);
            GameplaySkinResolvedMaterialSourceIdentity selected = GameplaySkinResolvedMaterialSourceIdentity.Create(
                GameplaySkinResolvedMaterialSourceKind.SelectedPackage,
                "selected.public-test",
                "content-a");
            GameplaySkinResolvedMaterialSourceIdentity programmatic = GameplaySkinResolvedMaterialSourceIdentity.Create(
                GameplaySkinResolvedMaterialSourceKind.ProgrammaticFallback,
                "programmatic.public-test",
                "v1");
            Texture texture = (Texture)RuntimeHelpers.GetUninitializedObject(typeof(Texture));
            using var cancellation = new CancellationTokenSource();

            Assert.That(
                () => GameplaySkinPublicSlotMaterialResolver.Resolve(
                    snapshot,
                    document,
                    GameplaySkinPublicSlotMaterialCapabilities.Create(new[] { GameplaySkinSlotCatalog.LaneSurface }),
                    new[] { GameplaySkinSlotCatalog.LaneSurface },
                    selected,
                    programmatic,
                    _ =>
                    {
                        cancellation.Cancel();
                        return texture;
                    },
                    cancellation.Token),
                Throws.InstanceOf<OperationCanceledException>());
        }

        [Test]
        public void TestGenericPublicMaterialRejectsExternalOrAmbiguousResourceNames()
        {
            Texture texture = (Texture)RuntimeHelpers.GetUninitializedObject(typeof(Texture));

            Assert.Multiple(() =>
            {
                Assert.That(
                    () => GameplaySkinPublicSlotMaterial.FromPreparedResource(GameplaySkinSlotCatalog.LaneSurface, "../outside", texture),
                    Throws.ArgumentException);
                Assert.That(
                    () => GameplaySkinPublicSlotMaterial.FromPreparedResource(GameplaySkinSlotCatalog.LaneSurface, @"C:\outside", texture),
                    Throws.ArgumentException);
                Assert.That(
                    () => GameplaySkinPublicSlotMaterial.FromPreparedResource(GameplaySkinSlotCatalog.LaneSurface, @"folder\resource", texture),
                    Throws.ArgumentException);
                Assert.That(
                    () => GameplaySkinPublicSlotMaterial.FromPreparedResource(GameplaySkinSlotCatalog.LaneSurface, "folder/resource?alias", texture),
                    Throws.ArgumentException);
                Assert.That(
                    () => GameplaySkinPublicSlotMaterial.FromPreparedResource(GameplaySkinSlotCatalog.LaneSurface, "folder/CON.png", texture),
                    Throws.ArgumentException);
                Assert.That(
                    () => GameplaySkinPublicSlotMaterial.FromPreparedResource(GameplaySkinSlotCatalog.LaneSurface, "folder/e\u0301.png", texture),
                    Throws.ArgumentException);
            });
        }

        [Test]
        public void TestStaleMaterialContractCannotReplaceCommittedPublication()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            var owner = new GameplaySkinLayoutRevisionOwner(package);
            using GameplaySkinPreparedLayout first = prepareMaterialPublication(owner, topology, package, "material-a", 0.2f);

            Assert.That(owner.TryCommit(first), Is.True);
            GameplaySkinLayoutPublication committed = owner.CurrentPublication!;

            Assert.That(
                () => owner.PreparePublication(revision =>
                {
                    GameplaySkinLayoutSnapshot snapshot = createSnapshot(topology, package, revision, 0.3f);
                    GameplaySkinLaneTopologyGroup group = topology.GroupsInLogicalOrder[0];
                    GameplaySkinLaneTopologyEntry lane = topology.LanesInLogicalOrder[0];
                    GameplaySkinResolvedMaterialEntry entry = GameplaySkinResolvedMaterialEntry.Provide(
                        GameplaySkinSlotCatalog.Note,
                        GameplaySkinResolvedMaterialTarget.ForLane(group, lane),
                        GameplaySkinResolvedMaterialSourceIdentity.Create(
                            GameplaySkinResolvedMaterialSourceKind.SelectedPackage,
                            "selected.current",
                            "material-stale"),
                        new TestMaterial("material-stale"));
                    GameplaySkinResolvedMaterialSet stale = GameplaySkinResolvedMaterialSet.Create(
                        snapshot,
                        new GameplaySkinMaterialContractIdentity("catalog.stale", "codec.stale", "resolver.stale"),
                        new[] { entry });
                    return GameplaySkinLayoutPublication.Create(new TestLayoutAdapter(snapshot), stale);
                }),
                Throws.ArgumentException.With.Message.Contains("current catalog/codec/resolver"));

            Assert.That(owner.CurrentPublication, Is.SameAs(committed));
        }

        [Test]
        public void TestVersionedRuntimeSupportProfilesDecideEveryFrozenCatalogSlot()
        {
            GameplaySkinRuntimeSupportProfile bms = GameplaySkinRuntimeSupportProfile.Bms;
            GameplaySkinRuntimeSupportProfile mania = GameplaySkinRuntimeSupportProfile.Mania;

            Assert.Multiple(() =>
            {
                Assert.That(bms.ContractVersion, Is.EqualTo(GameplaySkinRuntimeSupportProfile.CONTRACT_ID));
                Assert.That(mania.ContractVersion, Is.EqualTo(GameplaySkinRuntimeSupportProfile.CONTRACT_ID));
                Assert.That(bms.Decisions.Select(decision => decision.Descriptor), Is.EqualTo(GameplaySkinSlotCatalog.All));
                Assert.That(mania.Decisions.Select(decision => decision.Descriptor), Is.EqualTo(GameplaySkinSlotCatalog.All));
                Assert.That(bms.Decisions.Select(decision => decision.Descriptor.Id), Is.Unique);
                Assert.That(mania.Decisions.Select(decision => decision.Descriptor.Id), Is.Unique);
                Assert.That(bms.Capabilities.Support, Has.Count.EqualTo(28));
                Assert.That(mania.Capabilities.Support, Has.Count.EqualTo(23));
                Assert.That(mania.Decisions
                                 .Where(decision => decision.Kind == GameplaySkinRuntimeSupportDecisionKind.NotApplicable)
                                 .Select(decision => decision.Descriptor),
                    Is.EquivalentTo(new[]
                    {
                        GameplaySkinSlotCatalog.Mine,
                        GameplaySkinSlotCatalog.Turntable,
                        GameplaySkinSlotCatalog.Laser,
                        GameplaySkinSlotCatalog.BgaViewport,
                        GameplaySkinSlotCatalog.BgaFrame,
                    }));
            });
        }

        [Test]
        public void TestExactPublicationRejectsWrongRulesetRuntimeSupportProfile()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            GameplaySkinLayoutSnapshot snapshot = createSnapshot(topology, package, 0, 0.2f);
            GameplaySkinResolvedMaterialSet wrongProfile = GameplaySkinResolvedMaterialSet.Create(
                snapshot,
                GameplaySkinMaterialContractIdentity.CurrentFor(GameplaySkinRuntimeSupportProfile.Bms),
                Array.Empty<GameplaySkinResolvedMaterialEntry>());

            Assert.That(
                () => GameplaySkinLayoutPublication.Create(new TestLayoutAdapter(snapshot), wrongProfile),
                Throws.ArgumentException.With.Message.Contains("exact ruleset profile"));
        }

        [Test]
        public void TestNotApplicableSlotCannotEnterResolvedMaterialSet()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            GameplaySkinLayoutSnapshot snapshot = createSnapshot(topology, package, 0, 0.2f);
            GameplaySkinLaneTopologyGroup group = topology.GroupsInLogicalOrder[0];
            GameplaySkinLaneTopologyEntry lane = topology.LanesInLogicalOrder[0];
            GameplaySkinResolvedMaterialEntry mine = GameplaySkinResolvedMaterialEntry.Provide(
                GameplaySkinSlotCatalog.Mine,
                GameplaySkinResolvedMaterialTarget.ForLane(group, lane),
                GameplaySkinResolvedMaterialSourceIdentity.Create(
                    GameplaySkinResolvedMaterialSourceKind.ProgrammaticFallback,
                    "programmatic",
                    "v1"),
                new TestMaterial("mine"));

            Assert.That(
                () => GameplaySkinResolvedMaterialSet.Create(
                    snapshot,
                    GameplaySkinMaterialContractIdentity.CurrentFor(snapshot),
                    new[] { mine }),
                Throws.ArgumentException.With.Message.Contains("versioned runtime profile"));
        }

        [Test]
        public void TestCatalogApplicabilityIsValidatedByProviderAndFinalMaterialSet()
        {
            GameplaySkinLayoutSnapshot bms5 = createBmsSnapshot("5k", 5, includeScratch: true);
            GameplaySkinLaneTopologyGroup group = bms5.Context.Topology.GroupsInLogicalOrder[0];
            GameplaySkinLaneTopologyEntry scratch = bms5.Context.Topology.LanesInLogicalOrder
                                                               .Single(lane => lane.Identity.Role == GameplaySkinLaneRole.Scratch);
            GameplaySkinLaneTopologyEntry key = bms5.Context.Topology.LanesInLogicalOrder
                                                           .First(lane => lane.Identity.Role == GameplaySkinLaneRole.Key);
            GameplaySkinResolvedMaterialTarget scratchTarget = GameplaySkinResolvedMaterialTarget.ForLane(group, scratch);
            GameplaySkinResolvedMaterialTarget keyTarget = GameplaySkinResolvedMaterialTarget.ForLane(group, key);
            GameplaySkinResolvedMaterialSourceIdentity source = GameplaySkinResolvedMaterialSourceIdentity.Create(
                GameplaySkinResolvedMaterialSourceKind.SelectedPackage,
                "selected.current",
                "content-a");
            GameplaySkinResolvedMaterialEntry invalidEntry = GameplaySkinResolvedMaterialEntry.Provide(
                GameplaySkinSlotCatalog.Turntable,
                keyTarget,
                source,
                new TestMaterial("turntable-on-key"));
            GameplaySkinDocument document = GameplaySkinDocumentCodec.Decode(
                string.Empty,
                GameplaySkinDocumentIdentity.CreateUnboundPackageParse("unversioned"))
                                                               .BindToPublication(bms5);
            GameplaySkinRuntimeCapabilitySet capabilities = GameplaySkinRuntimeCapabilitySet.Create(new[]
            {
                GameplaySkinRuntimeSlotSupport.Create(GameplaySkinSlotCatalog.Turntable, GameplaySkinRuntimeSlotCapability.Provide),
            });
            var provider = new GameplaySkinDocumentSlotProvider<string, TestMaterial>(
                document,
                capabilities,
                "selected-document",
                _ => keyTarget,
                (_, _) => new TestMaterial("unreachable"));
            var lookup = new GameplaySkinSlotLookup<string>(GameplaySkinSlotCatalog.Turntable, "exact-target");
            GameplaySkinLayoutSnapshot mania = createSnapshot(
                createTopology(), GameplaySkinPackageRevision.CreateCompatibility(), 0, 0.2f);
            GameplaySkinResolvedMaterialTarget maniaTarget = GameplaySkinResolvedMaterialTarget.ForLane(
                mania.Context.Topology.GroupsInLogicalOrder[0],
                mania.Context.Topology.LanesInLogicalOrder[0]);

            Assert.Multiple(() =>
            {
                Assert.That(
                    GameplaySkinSlotApplicabilityValidator.Validate(GameplaySkinSlotCatalog.Turntable, bms5, scratchTarget),
                    Is.EqualTo(GameplaySkinSlotApplicabilityResult.Applicable));
                Assert.That(
                    GameplaySkinSlotApplicabilityValidator.Validate(GameplaySkinSlotCatalog.Turntable, bms5, keyTarget),
                    Is.EqualTo(GameplaySkinSlotApplicabilityResult.UnsupportedLaneRole));
                Assert.That(
                    GameplaySkinSlotApplicabilityValidator.Validate(
                        GameplaySkinSlotCatalog.Turntable,
                        mania,
                        maniaTarget),
                    Is.EqualTo(GameplaySkinSlotApplicabilityResult.UnsupportedRuleset));
                Assert.That(
                    () => GameplaySkinResolvedMaterialSet.Create(
                        bms5,
                        GameplaySkinMaterialContractIdentity.CurrentFor(bms5),
                        new[] { invalidEntry }),
                    Throws.ArgumentException);
                Assert.That(
                    () => provider.GetSlot(lookup),
                    Throws.TypeOf<GameplaySkinDocumentSlotRejectedException>()
                          .With.Property(nameof(GameplaySkinDocumentSlotRejectedException.Code))
                          .EqualTo("gameplay-skin.applicability-unsupported"));
            });
        }

        [Test]
        public void TestResolvedMaterialIdentityAndDiagnosticsAreStrictlyPathFree()
        {
            GameplaySkinResolvedMaterialKey key = new GameplaySkinResolvedMaterialKey(
                GameplaySkinSlotCatalog.BgaViewport,
                GameplaySkinResolvedMaterialTarget.Global);
            const string private_hash = "d21f723b68c5e08a9fdbb421d7d6f13da569f4e7d0a95ee36911612738447216";
            GameplaySkinResolvedMaterialSourceIdentity source = GameplaySkinResolvedMaterialSourceIdentity.Create(
                GameplaySkinResolvedMaterialSourceKind.SelectedPackage,
                "selected.current",
                private_hash);
            var diagnostic = new GameplaySkinResolvedMaterialDiagnostic("skin.material.invalid", key, source);
            string serialised = JsonConvert.SerializeObject(diagnostic);
            GameplaySkinResolvedMaterialDiagnostic invalidTargetDiagnostic = GameplaySkinResolvedMaterialDiagnostic.ForDocument(
                "skin.material.target-invalid",
                source,
                GameplaySkinSlotCatalog.LongNoteBody);
            string serialisedInvalidTarget = JsonConvert.SerializeObject(invalidTargetDiagnostic);

            Assert.Multiple(() =>
            {
                Assert.That(
                    () => GameplaySkinResolvedMaterialSourceIdentity.Create(
                        GameplaySkinResolvedMaterialSourceKind.SelectedPackage,
                        @"C:\authors\secret",
                        "content-a"),
                    Throws.ArgumentException);
                Assert.That(
                    () => GameplaySkinResolvedMaterialSourceIdentity.Create(
                        GameplaySkinResolvedMaterialSourceKind.SelectedPackage,
                        "..",
                        "content-a"),
                    Throws.ArgumentException);
                Assert.That(
                    () => new GameplaySkinResolvedMaterialDiagnostic("Skin.Material.Invalid", key),
                    Throws.ArgumentException);
                Assert.That(source.ToString(), Is.EqualTo("SelectedPackage:Bound"));
                Assert.That(diagnostic.ToString(), Does.Not.Contain(source.StableId));
                Assert.That(diagnostic.ToString(), Does.Not.Contain(source.ContentRevision));
                Assert.That(serialised, Does.Contain("\"SourceKind\":3"));
                Assert.That(serialised, Does.Not.Contain(source.StableId));
                Assert.That(serialised, Does.Not.Contain(source.ContentRevision));
                Assert.That(invalidTargetDiagnostic.Key, Is.Null);
                Assert.That(invalidTargetDiagnostic.CatalogSlotId, Is.EqualTo(GameplaySkinSlotCatalog.LongNoteBody.Id));
                Assert.That(invalidTargetDiagnostic.ToString(), Does.Contain(GameplaySkinSlotCatalog.LongNoteBody.Id));
                Assert.That(invalidTargetDiagnostic.ToString(), Does.Not.Contain(source.ContentRevision));
                Assert.That(serialisedInvalidTarget, Does.Contain(GameplaySkinSlotCatalog.LongNoteBody.Id));
                Assert.That(serialisedInvalidTarget, Does.Not.Contain(source.ContentRevision));
            });
        }

        [Test]
        public void TestCommittedMaterialDiagnosticsUseOneStablePersistenceSafeProductLogBatch()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            var owner = new GameplaySkinLayoutRevisionOwner(package);
            GameplaySkinLaneTopologyGroup group = topology.GroupsInLogicalOrder[0];
            GameplaySkinLaneTopologyEntry lane = topology.LanesInLogicalOrder[0];
            const string private_hash = "d21f723b68c5e08a9fdbb421d7d6f13da569f4e7d0a95ee36911612738447216";
            GameplaySkinResolvedMaterialSourceIdentity source = GameplaySkinResolvedMaterialSourceIdentity.Create(
                GameplaySkinResolvedMaterialSourceKind.SelectedPackage,
                "selected.current",
                private_hash);

            GameplaySkinPreparedLayout prepare() => owner.PreparePublication(revision =>
            {
                GameplaySkinLayoutSnapshot snapshot = createSnapshot(topology, package, revision, 0.2f);
                var diagnostic = new GameplaySkinResolvedMaterialDiagnostic(
                    "OMS-SKIN-CODEC-009",
                    new GameplaySkinResolvedMaterialKey(
                        GameplaySkinSlotCatalog.Note,
                        GameplaySkinResolvedMaterialTarget.ForLane(group, lane)),
                    source);
                GameplaySkinResolvedMaterialSet set = GameplaySkinResolvedMaterialSet.Create(
                    snapshot,
                    GameplaySkinMaterialContractIdentity.CurrentFor(snapshot),
                    Array.Empty<GameplaySkinResolvedMaterialEntry>(),
                    new[] { diagnostic, diagnostic });
                return GameplaySkinLayoutPublication.Create(new TestLayoutAdapter(snapshot), set);
            });

            int logOperationsBeforeCommit = owner.MaterialDiagnosticsLogOperations;
            using GameplaySkinPreparedLayout first = prepare();
            Assert.That(owner.TryCommit(first), Is.True);
            Assert.DoesNotThrow(() => owner.MaterialDiagnosticsObserver.GetAwaiter().GetResult());

            string? productBatch = owner.LastMaterialDiagnosticsBatch;

            Assert.Multiple(() =>
            {
                Assert.That(owner.MaterialDiagnosticsLogOperations, Is.EqualTo(logOperationsBeforeCommit + 1));
                Assert.That(productBatch, Is.Not.Null);
                Assert.That(productBatch, Does.Contain("count=1"));
                Assert.That(productBatch, Does.Contain("code=OMS-SKIN-CODEC-009"));
                Assert.That(productBatch, Does.Contain("slot=object.note"));
                Assert.That(productBatch, Does.Contain("lane:test.group:test.lane-1:gl=0:gv=0:l=0:v=0:gll=0:glv=0"));
                Assert.That(productBatch, Does.Contain("source=SelectedPackage"));
                Assert.That(productBatch, Does.Not.Contain(source.StableId));
                Assert.That(productBatch, Does.Not.Contain(private_hash));
                Assert.That(productBatch, Does.Not.Contain(@"C:\"));
            });
        }

        [Test]
        public void TestCompatibilityContractCannotCarryResolvedEntries()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            GameplaySkinLayoutSnapshot snapshot = createSnapshot(topology, package, 0, 0.2f);
            GameplaySkinResolvedMaterialEntry entry = GameplaySkinResolvedMaterialEntry.Provide(
                GameplaySkinSlotCatalog.Note,
                GameplaySkinResolvedMaterialTarget.ForLane(topology.GroupsInLogicalOrder[0], topology.LanesInLogicalOrder[0]),
                GameplaySkinResolvedMaterialSourceIdentity.Create(
                    GameplaySkinResolvedMaterialSourceKind.SelectedPackage,
                    "selected.current",
                    "content-a"),
                new TestMaterial("note-a"));

            Assert.That(
                () => GameplaySkinResolvedMaterialSet.Create(
                    snapshot,
                    GameplaySkinMaterialContractIdentity.CompatibilityEmpty,
                    new[] { entry }),
                Throws.ArgumentException);
        }

        [Test]
        public void TestRejectedPreparedMaterialPublicationKeepsExactPreviousTriple()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            var owner = new GameplaySkinLayoutRevisionOwner(package);
            GameplaySkinPreparedLayout first = prepareMaterialPublication(owner, topology, package, "material-a", 0.2f);
            Assert.That(owner.TryCommit(first), Is.True);
            GameplaySkinLayoutPublication publishedA = owner.CurrentPublication!;

            GameplaySkinPreparedLayout stale = prepareMaterialPublication(owner, topology, package, "material-b", 0.25f);
            GameplaySkinPreparedLayout latest = prepareMaterialPublication(owner, topology, package, "material-c", 0.3f);

            Assert.Multiple(() =>
            {
                Assert.That(owner.TryCommit(stale), Is.False);
                Assert.That(owner.CurrentPublication, Is.SameAs(publishedA));
                Assert.That(owner.Current, Is.SameAs(publishedA.Snapshot));
                Assert.That(owner.CurrentPublication!.MaterialSet, Is.SameAs(publishedA.MaterialSet));
                Assert.That(owner.TryCommit(latest), Is.True);
                Assert.That(owner.CurrentPublication, Is.SameAs(latest.Publication));
                Assert.That(owner.CurrentPublication!.MaterialSet, Is.SameAs(latest.Publication.MaterialSet));
            });
        }

        [Test]
        public void TestPreparedPublicationRetirementFollowsCarrierAndCommittedOwnerLifetime()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            var owner = new GameplaySkinLayoutRevisionOwner(package);
            var retirement = new CountingDisposable();
            var sceneRetirement = new CountingDisposable();
            GameplaySkinPreparedLayout prepared = prepareMaterialPublication(
                owner,
                topology,
                package,
                "material",
                0.2f,
                retirement,
                sceneRetirement);

            Assert.Multiple(() =>
            {
                Assert.That(retirement.DisposeCount, Is.Zero);
                Assert.That(sceneRetirement.DisposeCount, Is.Zero);
            });
            Assert.That(owner.TryCommit(prepared), Is.True);

            prepared.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(retirement.DisposeCount, Is.Zero, "A committed carrier must transfer retirement to the root owner.");
                Assert.That(sceneRetirement.DisposeCount, Is.Zero, "Prepared scene resources must share the exact committed root lifetime.");
                Assert.That(owner.CurrentPublication, Is.SameAs(prepared.Publication));
            });

            owner.Dispose();
            owner.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(retirement.DisposeCount, Is.EqualTo(1));
                Assert.That(sceneRetirement.DisposeCount, Is.EqualTo(1));
                Assert.That(owner.CurrentPublication, Is.Null);
            });
        }

        [Test]
        public void TestUnadmittedPublicationRetirementIsExplicitAndIdempotent()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            var retirement = new CountingDisposable();
            var sceneRetirement = new CountingDisposable();
            GameplaySkinLayoutPublication publication = createMaterialPublication(
                topology,
                package,
                0,
                "material",
                0.2f,
                retirement,
                sceneRetirement);

            publication.Dispose();
            publication.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(retirement.DisposeCount, Is.EqualTo(1));
                Assert.That(sceneRetirement.DisposeCount, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestPublicationValidationFailureRetiresSceneAndPackageResourcesExactlyOnce()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            GameplaySkinLayoutSnapshot exactSnapshot = createSnapshot(topology, package, 0, 0.2f);
            GameplaySkinLayoutSnapshot foreignSnapshot = createSnapshot(topology, package, 0, 0.3f);
            GameplaySkinLaneTopologyGroup group = topology.GroupsInLogicalOrder[0];
            GameplaySkinLaneTopologyEntry lane = topology.LanesInLogicalOrder[0];
            GameplaySkinResolvedMaterialEntry entry = GameplaySkinResolvedMaterialEntry.Provide(
                GameplaySkinSlotCatalog.Note,
                GameplaySkinResolvedMaterialTarget.ForLane(group, lane),
                GameplaySkinResolvedMaterialSourceIdentity.Create(
                    GameplaySkinResolvedMaterialSourceKind.SelectedPackage,
                    "selected.current",
                    "material"),
                new TestMaterial("material"));
            GameplaySkinResolvedMaterialSet materialSet = GameplaySkinResolvedMaterialSet.Create(
                exactSnapshot,
                GameplaySkinMaterialContractIdentity.CurrentFor(exactSnapshot),
                new[] { entry });
            var sceneRetirement = new CountingDisposable();
            var packageRetirement = new CountingDisposable();
            var scene = new GameplaySkinPreparedScene(
                exactSnapshot,
                materialSet,
                "scene-test",
                null,
                null,
                Array.Empty<GameplaySkinPreparedSceneResource>(),
                Array.Empty<GameplaySkinPreparedSceneNode>(),
                sceneRetirement);

            Assert.That(
                () => GameplaySkinLayoutPublication.Create(
                    new TestLayoutAdapter(foreignSnapshot),
                    materialSet,
                    scene,
                    packageRetirement),
                Throws.ArgumentException.With.Message.StartsWith(
                    "A resolved gameplay skin material set must retain the exact package and layout snapshot being published."));

            scene.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(sceneRetirement.DisposeCount, Is.EqualTo(1),
                    "A scene retirement taken before publication validation must never be stranded.");
                Assert.That(packageRetirement.DisposeCount, Is.EqualTo(1),
                    "The exact-package borrow must retire together with a rejected scene publication.");
            });
        }

        [Test]
        public void TestStalePreparedPublicationRetiresOnlyItsOwnProvisionalResources()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            var owner = new GameplaySkinLayoutRevisionOwner(package);
            var staleRetirement = new CountingDisposable();
            var latestRetirement = new CountingDisposable();
            GameplaySkinPreparedLayout stale = prepareMaterialPublication(owner, topology, package, "stale", 0.2f, staleRetirement);
            GameplaySkinPreparedLayout latest = prepareMaterialPublication(owner, topology, package, "latest", 0.25f, latestRetirement);

            Assert.That(owner.TryCommit(stale), Is.False);
            Assert.That(owner.TryCommit(stale), Is.False);

            Assert.Multiple(() =>
            {
                Assert.That(staleRetirement.DisposeCount, Is.EqualTo(1));
                Assert.That(latestRetirement.DisposeCount, Is.Zero);
                Assert.That(owner.CurrentPublication, Is.Null);
            });

            Assert.That(owner.TryCommit(latest), Is.True);
            owner.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(staleRetirement.DisposeCount, Is.EqualTo(1));
                Assert.That(latestRetirement.DisposeCount, Is.EqualTo(1));
            });
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void TestDispatcherFailureRetiresPreparedPublicationExactlyOnce(int mode)
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            Action? lateCommit = null;
            GameplaySkinLayoutRevisionOwner owner = createOwner(package, commit =>
            {
                switch (mode)
                {
                    case 0:
                        return false;

                    case 1:
                        throw new InvalidOperationException("scheduler fault");

                    default:
                        lateCommit = commit;
                        return true;
                }
            });
            var retirement = new CountingDisposable();
            var sceneRetirement = new CountingDisposable();
            GameplaySkinPreparedLayout prepared = prepareMaterialPublication(
                owner,
                topology,
                package,
                "material",
                0.2f,
                retirement,
                sceneRetirement);

            Assert.That(owner.TryCommit(prepared), Is.False);

            lateCommit?.Invoke();
            prepared.Dispose();
            owner.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(retirement.DisposeCount, Is.EqualTo(1));
                Assert.That(sceneRetirement.DisposeCount, Is.EqualTo(1));
                Assert.That(owner.CurrentPublication, Is.Null);
            });
        }

        [Test]
        public void TestPostSolveParticipantBarrierFailureRetiresPreparedPublication()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            bool participantCurrent = true;
            var owner = new GameplaySkinLayoutRevisionOwner(
                package,
                validateRoot: () => true,
                acquireWorkLease: () => null,
                captureParticipantGeneration: () => 0,
                validateParticipantGeneration: generation => generation == 0 && participantCurrent,
                commitAtParticipantGeneration: commitCompatibility,
                dispatchCommit: commit =>
                {
                    commit();
                    return true;
                });
            var retirement = new CountingDisposable();

            Assert.That(() => owner.PreparePublication(revision =>
            {
                GameplaySkinLayoutPublication publication = createMaterialPublication(
                    topology,
                    package,
                    revision,
                    "material",
                    0.2f,
                    retirement);
                participantCurrent = false;
                return publication;
            }), Throws.TypeOf<GameplaySkinLayoutParticipantBarrierChangedException>());

            Assert.Multiple(() =>
            {
                Assert.That(retirement.DisposeCount, Is.EqualTo(1));
                Assert.That(owner.CurrentPublication, Is.Null);
            });
        }

        [Test]
        public void TestOwnerDisposedDuringSolveRetiresPublicationBeforeReturningCarrier()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            var owner = new GameplaySkinLayoutRevisionOwner(package);
            var retirement = new CountingDisposable();

            Assert.That(() => owner.PreparePublication(revision =>
            {
                GameplaySkinLayoutPublication publication = createMaterialPublication(
                    topology,
                    package,
                    revision,
                    "material",
                    0.2f,
                    retirement);
                owner.Dispose();
                return publication;
            }), Throws.InvalidOperationException.With.Message.EqualTo(
                "The gameplay layout root changed during background preparation."));

            Assert.Multiple(() =>
            {
                Assert.That(retirement.DisposeCount, Is.EqualTo(1));
                Assert.That(owner.IsDisposed, Is.True);
                Assert.That(owner.CurrentPublication, Is.Null);
            });
        }

        [Test]
        public void TestRevisionOwnerRejectsDifferentPackageAndForgedRevision()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            GameplaySkinPackageRevision otherPackage = GameplaySkinPackageRevision.CreateCompatibility();
            var owner = new GameplaySkinLayoutRevisionOwner(package);

            Assert.Multiple(() =>
            {
                Assert.That(() => owner.Prepare(revision => createSnapshot(topology, otherPackage, revision, 0.2f)), Throws.ArgumentException);
                Assert.That(() => owner.Prepare(revision => createSnapshot(topology, package, revision + 1, 0.2f)), Throws.ArgumentException);
                Assert.That(owner.Current, Is.Null);
            });
        }

        [Test]
        public void TestPreparedCarrierIsBoundToIssuingOwner()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            var issuingOwner = new GameplaySkinLayoutRevisionOwner(package);
            var foreignOwner = new GameplaySkinLayoutRevisionOwner(package);
            GameplaySkinPreparedLayout prepared = issuingOwner.Prepare(
                revision => createSnapshot(topology, package, revision, 0.2f));

            Assert.Multiple(() =>
            {
                Assert.That(foreignOwner.TryCommit(prepared), Is.False);
                Assert.That(foreignOwner.Current, Is.Null);
                Assert.That(issuingOwner.TryCommit(prepared), Is.True);
                Assert.That(issuingOwner.Current, Is.SameAs(prepared.Snapshot));
            });
        }

        [Test]
        public void TestAbortedPreparedLayoutCannotCommit()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            var owner = new GameplaySkinLayoutRevisionOwner(package);
            GameplaySkinPreparedLayout prepared = owner.Prepare(revision => createSnapshot(topology, package, revision, 0.2f));

            prepared.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(owner.TryCommit(prepared), Is.False);
                Assert.That(owner.Current, Is.Null);
            });
        }

        [Test]
        public void TestSuccessfulPublicationsUseConsecutiveLayoutRevisionsAndOneExactPackage()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            var owner = new GameplaySkinLayoutRevisionOwner(package);

            GameplaySkinPreparedLayout first = owner.Prepare(
                revision => createSnapshot(topology, package, revision, 0.2f));
            Assert.That(owner.TryCommit(first), Is.True);
            GameplaySkinLayoutSnapshot publishedA = owner.Current!;

            GameplaySkinPreparedLayout second = owner.Prepare(
                revision => createSnapshot(topology, package, revision, 0.25f));
            Assert.That(owner.TryCommit(second), Is.True);
            GameplaySkinLayoutSnapshot publishedB = owner.Current!;

            Assert.Multiple(() =>
            {
                Assert.That(publishedA.Context.LayoutRevision, Is.Zero);
                Assert.That(publishedB.Context.LayoutRevision, Is.EqualTo(1));
                Assert.That(publishedB, Is.Not.SameAs(publishedA));
                Assert.That(publishedA.Context.PackageRevision, Is.SameAs(package));
                Assert.That(publishedB.Context.PackageRevision, Is.SameAs(package));
            });
        }

        [Test]
        public void TestCommitDispatcherRejectionAndFaultPreserveExactPublishedReference()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            bool reject = false;
            bool fault = false;
            GameplaySkinLayoutRevisionOwner owner = createOwner(package, commit =>
            {
                if (fault)
                    throw new InvalidOperationException("scheduler fault");

                if (reject)
                    return false;

                commit();
                return true;
            });

            GameplaySkinPreparedLayout first = owner.Prepare(revision => createSnapshot(topology, package, revision, 0.2f));
            Assert.That(owner.TryCommit(first), Is.True);
            GameplaySkinLayoutSnapshot published = owner.Current!;

            reject = true;
            GameplaySkinPreparedLayout rejected = owner.Prepare(revision => createSnapshot(topology, package, revision, 0.25f));
            Assert.Multiple(() =>
            {
                Assert.That(owner.TryCommit(rejected), Is.False);
                Assert.That(owner.TryCommit(rejected), Is.False);
                Assert.That(owner.Current, Is.SameAs(published));
            });

            reject = false;
            fault = true;
            GameplaySkinPreparedLayout faulted = owner.Prepare(revision => createSnapshot(topology, package, revision, 0.3f));
            Assert.Multiple(() =>
            {
                Assert.That(owner.TryCommit(faulted), Is.False);
                Assert.That(owner.TryCommit(faulted), Is.False);
                Assert.That(owner.Current, Is.SameAs(published));
            });
        }

        [Test]
        public void TestAsynchronousDispatcherCannotPublishAfterCallerAbortsCarrier()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            Action? lateCommit = null;
            GameplaySkinLayoutRevisionOwner owner = createOwner(package, commit =>
            {
                lateCommit = commit;
                return true;
            });
            GameplaySkinPreparedLayout prepared = owner.Prepare(revision => createSnapshot(topology, package, revision, 0.2f));

            Assert.Multiple(() =>
            {
                Assert.That(owner.TryCommit(prepared), Is.False);
                Assert.That(owner.Current, Is.Null);
                Assert.That(lateCommit, Is.Not.Null);
            });

            lateCommit!();

            Assert.That(owner.Current, Is.Null, "A dispatcher which returns before its callback must never publish later.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task TestDispatcherRejectingOrThrowingAfterCallbackClaimJoinsTerminalCommit(bool throwAfterClaim)
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            using var callbackEnteredCommit = new ManualResetEventSlim();
            using var releaseCommit = new ManualResetEventSlim();
            bool blockCommit = false;
            var owner = new GameplaySkinLayoutRevisionOwner(
                package,
                validateRoot: () =>
                {
                    if (blockCommit)
                    {
                        callbackEnteredCommit.Set();

                        while (!releaseCommit.Wait(TimeSpan.FromMilliseconds(100)))
                        {
                        }
                    }

                    return true;
                },
                acquireWorkLease: () => null,
                captureParticipantGeneration: () => 0,
                validateParticipantGeneration: generation => generation == 0,
                commitAtParticipantGeneration: commitCompatibility,
                dispatchCommit: commit =>
                {
                    var callbackThread = new Thread(() => commit()) { IsBackground = true };
                    callbackThread.Start();
                    Assert.That(callbackEnteredCommit.Wait(TimeSpan.FromSeconds(10)), Is.True);

                    if (throwAfterClaim)
                        throw new InvalidOperationException("dispatcher fault after callback claim");

                    return false;
                });
            GameplaySkinPreparedLayout prepared = owner.Prepare(
                revision => createSnapshot(topology, package, revision, 0.2f));
            blockCommit = true;

            Task<bool> commitTask = Task.Run(() => owner.TryCommit(prepared));
            Assert.That(callbackEnteredCommit.Wait(TimeSpan.FromSeconds(10)), Is.True);
            Assert.That(commitTask.IsCompleted, Is.False, "The caller must join a callback which already owns admission.");

            releaseCommit.Set();
            Assert.That(await commitTask.WaitAsync(TimeSpan.FromSeconds(10)), Is.True);
            Assert.That(owner.Current, Is.SameAs(prepared.Snapshot));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void TestSynchronousCallbackResultWinsDispatcherContradictionAndReentrancy(int mode)
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            GameplaySkinLayoutRevisionOwner owner = createOwner(package, commit =>
            {
                commit();

                switch (mode)
                {
                    case 0:
                        return false;

                    case 1:
                        throw new InvalidOperationException("fault after synchronous commit");

                    default:
                        commit();
                        return true;
                }
            });
            GameplaySkinPreparedLayout prepared = owner.Prepare(
                revision => createSnapshot(topology, package, revision, 0.2f));

            Assert.Multiple(() =>
            {
                Assert.That(owner.TryCommit(prepared), Is.True);
                Assert.That(owner.Current, Is.SameAs(prepared.Snapshot));
                Assert.That(owner.CurrentPublication, Is.SameAs(prepared.Publication));
            });
        }

        [Test]
        public void TestExactRootIsRevalidatedInsideAtomicParticipantAdmission()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            bool exactRoot = true;
            var owner = new GameplaySkinLayoutRevisionOwner(
                package,
                validateRoot: () => exactRoot,
                acquireWorkLease: () => null,
                captureParticipantGeneration: () => 0,
                validateParticipantGeneration: generation => generation == 0,
                commitAtParticipantGeneration: (_, commit) =>
                {
                    exactRoot = false;
                    commit();
                    return true;
                },
                dispatchCommit: commit =>
                {
                    commit();
                    return true;
                });
            GameplaySkinPreparedLayout prepared = owner.Prepare(
                revision => createSnapshot(topology, package, revision, 0.2f));

            Assert.Multiple(() =>
            {
                Assert.That(owner.TryCommit(prepared), Is.False);
                Assert.That(owner.Current, Is.Null);
                Assert.That(owner.CurrentPublication, Is.Null);
            });
        }

        private static GameplaySkinLayoutSnapshot createSnapshot(
            GameplaySkinLaneTopologySnapshot topology,
            GameplaySkinPackageRevision package,
            long revision,
            float left)
        {
            GameplaySkinLayoutContext context = createContext(topology, package, revision);
            var groupRect = rect(left, 0, 0.6f, 0.9f);
            var groups = new[] { new GameplaySkinLayoutGroup(topology.GroupsInLogicalOrder[0], groupRect) };
            var lanes = new[]
            {
                new GameplaySkinLayoutLane(topology.LanesInLogicalOrder[0], rect(left, 0, 0.3f, 0.9f)),
                new GameplaySkinLayoutLane(topology.LanesInLogicalOrder[1], rect(left + 0.3f, 0, 0.3f, 0.9f)),
            };
            return GameplaySkinLayoutSnapshot.Create(
                context,
                groups,
                lanes,
                new[] { new GameplaySkinLayoutSurface("playfield", groupRect, 0, true, true) });
        }

        private static GameplaySkinPreparedLayout prepareMaterialPublication(
            GameplaySkinLayoutRevisionOwner owner,
            GameplaySkinLaneTopologySnapshot topology,
            GameplaySkinPackageRevision package,
            string materialName,
            float left,
            IDisposable? retirement = null,
            IDisposable? sceneRetirement = null)
            => owner.PreparePublication(revision => createMaterialPublication(
                topology,
                package,
                revision,
                materialName,
                left,
                retirement,
                sceneRetirement));

        private static GameplaySkinLayoutPublication createMaterialPublication(
            GameplaySkinLaneTopologySnapshot topology,
            GameplaySkinPackageRevision package,
            long revision,
            string materialName,
            float left,
            IDisposable? retirement = null,
            IDisposable? sceneRetirement = null)
        {
            GameplaySkinLayoutSnapshot snapshot = createSnapshot(topology, package, revision, left);
            GameplaySkinLaneTopologyGroup group = topology.GroupsInLogicalOrder[0];
            GameplaySkinLaneTopologyEntry lane = topology.LanesInLogicalOrder[0];
            GameplaySkinResolvedMaterialSourceIdentity source = GameplaySkinResolvedMaterialSourceIdentity.Create(
                GameplaySkinResolvedMaterialSourceKind.SelectedPackage,
                "selected.current",
                materialName);
            GameplaySkinResolvedMaterialEntry entry = GameplaySkinResolvedMaterialEntry.Provide(
                GameplaySkinSlotCatalog.Note,
                GameplaySkinResolvedMaterialTarget.ForLane(group, lane),
                source,
                new TestMaterial(materialName));
            GameplaySkinResolvedMaterialSet set = GameplaySkinResolvedMaterialSet.Create(
                snapshot,
                GameplaySkinMaterialContractIdentity.CurrentFor(snapshot),
                new[] { entry });
            var adapter = new TestLayoutAdapter(snapshot);

            if (sceneRetirement != null)
            {
                var scene = new GameplaySkinPreparedScene(
                    snapshot,
                    set,
                    "scene-test",
                    null,
                    null,
                    Array.Empty<GameplaySkinPreparedSceneResource>(),
                    Array.Empty<GameplaySkinPreparedSceneNode>(),
                    sceneRetirement);

                return retirement == null
                    ? GameplaySkinLayoutPublication.Create(adapter, set, scene)
                    : GameplaySkinLayoutPublication.Create(adapter, set, scene, retirement);
            }

            return retirement == null
                ? GameplaySkinLayoutPublication.Create(adapter, set)
                : GameplaySkinLayoutPublication.Create(adapter, set, retirement);
        }

        private static GameplaySkinLayoutRevisionOwner createOwner(
            GameplaySkinPackageRevision package,
            Func<Action, bool> dispatchCommit)
            => new GameplaySkinLayoutRevisionOwner(
                package,
                validateRoot: () => true,
                acquireWorkLease: () => null,
                captureParticipantGeneration: () => 0,
                validateParticipantGeneration: generation => generation == 0,
                commitAtParticipantGeneration: commitCompatibility,
                dispatchCommit);

        private static bool commitCompatibility(long generation, Action commit)
        {
            if (generation != 0)
                return false;

            commit();
            return true;
        }

        private static GameplaySkinLayoutContext createContext(
            GameplaySkinLaneTopologySnapshot topology,
            GameplaySkinPackageRevision package,
            long revision)
            => GameplaySkinLayoutContext.Create(
                "mania",
                "stages-2",
                "2k",
                "mania-single",
                topology,
                rect(0, 0, 1, 1),
                rect(0, 0, 1, 1),
                16f / 9f,
                1,
                GameplaySkinScrollDirection.Down,
                package,
                topologyRevision: 0,
                layoutRevision: revision);

        private static GameplaySkinLayoutSnapshot createBmsSnapshot(string keymodeId, int keyCount, bool includeScratch)
        {
            GameplaySkinLaneGroupIdentity groupIdentity = GameplaySkinLaneGroupIdentity.Create(
                GameplaySkinLaneGroupId.Create("bms.group.deck-1"), GameplaySkinLaneSide.Primary);
            var lanes = new List<GameplaySkinLaneTopologyEntry>();
            int globalIndex = 0;

            if (includeScratch)
            {
                lanes.Add(GameplaySkinLaneTopologyEntry.Create(
                    GameplaySkinLaneIdentity.Create(
                        GameplaySkinLaneId.Create("bms.lane.scratch-1"), groupIdentity, GameplaySkinLaneRole.Scratch),
                    globalIndex, globalIndex, globalIndex, globalIndex));
                globalIndex++;
            }

            for (int key = 1; key <= keyCount; key++, globalIndex++)
            {
                lanes.Add(GameplaySkinLaneTopologyEntry.Create(
                    GameplaySkinLaneIdentity.Create(
                        GameplaySkinLaneId.Create($"bms.lane.key-{key}"), groupIdentity, GameplaySkinLaneRole.Key),
                    globalIndex, globalIndex, globalIndex, globalIndex));
            }

            GameplaySkinLaneTopologySnapshot topology = GameplaySkinLaneTopologySnapshot.Create(new[]
            {
                GameplaySkinLaneTopologyGroup.Create(groupIdentity, 0, 0, lanes),
            });
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            GameplaySkinLayoutContext context = GameplaySkinLayoutContext.Create(
                "bms",
                $"bms.chart.{keymodeId}",
                keymodeId,
                "p1",
                topology,
                rect(0, 0, 1, 1),
                rect(0, 0, 1, 1),
                16f / 9f,
                1,
                GameplaySkinScrollDirection.Down,
                package,
                topologyRevision: 0,
                layoutRevision: 0);
            GameplaySkinLayoutRect groupRect = rect(0.2f, 0, 0.6f, 0.9f);
            float laneWidth = groupRect.Width / lanes.Count;
            GameplaySkinLayoutLane[] layoutLanes = topology.LanesInLogicalOrder
                                                               .Select(lane => new GameplaySkinLayoutLane(
                                                                   lane,
                                                                   rect(groupRect.Left + lane.GlobalLogicalIndex * laneWidth, 0, laneWidth, 0.9f)))
                                                               .ToArray();

            return GameplaySkinLayoutSnapshot.Create(
                context,
                new[] { new GameplaySkinLayoutGroup(topology.GroupsInLogicalOrder[0], groupRect) },
                layoutLanes,
                new[] { new GameplaySkinLayoutSurface("playfield", groupRect, 0, true, true) });
        }

        private static GameplaySkinLaneTopologySnapshot createTopology()
        {
            GameplaySkinLaneGroupIdentity group = GameplaySkinLaneGroupIdentity.Create(
                GameplaySkinLaneGroupId.Create("test.group"), GameplaySkinLaneSide.Neutral);
            return GameplaySkinLaneTopologySnapshot.Create(new[]
            {
                GameplaySkinLaneTopologyGroup.Create(group, 0, 0, new[]
                {
                    GameplaySkinLaneTopologyEntry.Create(
                        GameplaySkinLaneIdentity.Create(GameplaySkinLaneId.Create("test.lane-1"), group, GameplaySkinLaneRole.Key),
                        0, 0, 0, 0),
                    GameplaySkinLaneTopologyEntry.Create(
                        GameplaySkinLaneIdentity.Create(GameplaySkinLaneId.Create("test.lane-2"), group, GameplaySkinLaneRole.SpecialKey),
                        1, 1, 1, 1),
                }),
            });
        }

        private static GameplaySkinLayoutRect rect(float x, float y, float width, float height)
            => GameplaySkinLayoutRect.Create(x, y, width, height);

        private sealed class TestLayoutAdapter : IGameplaySkinLayoutAdapter
        {
            public GameplaySkinLayoutSnapshot Snapshot { get; }

            public TestLayoutAdapter(GameplaySkinLayoutSnapshot snapshot)
            {
                Snapshot = snapshot;
            }
        }

        private sealed record TestMaterial(string Name);

        private sealed class CountingDisposable : IDisposable
        {
            private int disposeCount;

            public int DisposeCount => Volatile.Read(ref disposeCount);

            public void Dispose() => Interlocked.Increment(ref disposeCount);
        }

        private sealed class StaticMaterialProvider : IGameplaySkinSlotProvider<GameplaySkinSlotLookup<string>, TestMaterial>
        {
            private readonly TestMaterial material;

            public string Name { get; }

            public StaticMaterialProvider(string name, TestMaterial material)
            {
                Name = name;
                this.material = material;
            }

            public SkinSlotResult<TestMaterial> GetSlot(GameplaySkinSlotLookup<string> slot)
                => SkinSlotResult<TestMaterial>.Provide(material);
        }

        private sealed class TestSkin : Skin
        {
            public TestSkin(Guid recordId, string name)
                : base(new SkinInfo(name) { ID = recordId }, null)
            {
            }

            public override ISample? GetSample(ISampleInfo sampleInfo) => null;

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup) => null;
        }
    }
}
