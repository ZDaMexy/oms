// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.Tests
{
    [TestFixture]
    public sealed partial class ManiaGameplaySkinLayoutSolverTest
    {
        [TestCase(new[] { 4 })]
        [TestCase(new[] { 5 })]
        [TestCase(new[] { 4, 5 })]
        [TestCase(new[] { 5, 5 })]
        public void TestRealStageVectorProducesOneCompleteSnapshot(int[] stageColumns)
        {
            GameplaySkinLayoutSnapshot snapshot = solve(stageColumns);

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Context.NativeContextId, Is.EqualTo($"stages-{string.Join("-", stageColumns)}"));
                Assert.That(snapshot.Context.PresentationStyleId, Is.EqualTo(stageColumns.Length == 1 ? "mania-single" : "mania-dual"));
                Assert.That(snapshot.GroupsInLogicalOrder.Select(group => group.TopologyGroup.LanesInLogicalOrder.Count), Is.EqualTo(stageColumns));
                Assert.That(snapshot.LanesInLogicalOrder, Has.Count.EqualTo(stageColumns.Sum()));
                Assert.That(snapshot.Context.PackageRevision.SourceKind, Is.EqualTo(GameplaySkinPackageSourceKind.Compatibility));
                Assert.That(snapshot.Context.TopologyRevision, Is.Zero);
                Assert.That(snapshot.Context.LayoutRevision, Is.Zero);
                Assert.That(snapshot.Surfaces.Select(surface => surface.Id), Does.Contain(ManiaGameplaySkinLayout.PLAYFIELD_SURFACE));
                Assert.That(snapshot.Surfaces.Select(surface => surface.Id), Does.Contain(ManiaGameplaySkinLayout.HIT_TARGET_SURFACE));
                Assert.That(snapshot.Surfaces.Select(surface => surface.Id), Does.Contain(ManiaGameplaySkinLayout.JUDGEMENT_SURFACE));
            });

            foreach (GameplaySkinLayoutGroup group in snapshot.GroupsInLogicalOrder)
                foreach (GameplaySkinLaneTopologyEntry topologyLane in group.TopologyGroup.LanesInLogicalOrder)
                {
                    GameplaySkinLayoutLane lane = snapshot.GetLane(topologyLane.Identity.Id);
                    Assert.That(group.Rect.Contains(lane.Rect), Is.True);
                    Assert.That(lane.TopologyEntry, Is.SameAs(topologyLane));
                }

            if (stageColumns.Length == 2)
                Assert.That(snapshot.GroupsInLogicalOrder[0].Rect.Intersects(snapshot.GroupsInLogicalOrder[1].Rect), Is.False);
        }

        [Test]
        public void TestDualStageSpecialKeysRemainStageLocal()
        {
            GameplaySkinLayoutSnapshot snapshot = solve(5, 5);
            GameplaySkinLaneTopologyEntry firstSpecial = snapshot.GroupsInLogicalOrder[0].TopologyGroup.LanesInLogicalOrder
                                                                .Single(lane => lane.Identity.Role == GameplaySkinLaneRole.SpecialKey);
            GameplaySkinLaneTopologyEntry secondSpecial = snapshot.GroupsInLogicalOrder[1].TopologyGroup.LanesInLogicalOrder
                                                                 .Single(lane => lane.Identity.Role == GameplaySkinLaneRole.SpecialKey);

            Assert.Multiple(() =>
            {
                Assert.That(firstSpecial.GroupLocalLogicalIndex, Is.EqualTo(2));
                Assert.That(secondSpecial.GroupLocalLogicalIndex, Is.EqualTo(2));
                Assert.That(firstSpecial.GlobalLogicalIndex, Is.EqualTo(2));
                Assert.That(secondSpecial.GlobalLogicalIndex, Is.EqualTo(7));
                Assert.That(snapshot.GetLane(firstSpecial.Identity.Id).LaneId.Value, Is.EqualTo("mania.lane.column-3"));
                Assert.That(snapshot.GetLane(secondSpecial.Identity.Id).LaneId.Value, Is.EqualTo("mania.lane.column-8"));
            });
        }

        [Test]
        public void TestDualStageColumnConfigurationUsesExplicitGlobalLogicalIndex()
        {
            var skin = new TestSkinSource();
            skin.Set(LegacyManiaSkinConfigurationLookups.ColumnWidth, 16, 3);
            skin.Set(LegacyManiaSkinConfigurationLookups.ColumnWidth, 120, 4);
            skin.Set(LegacyManiaSkinConfigurationLookups.ColumnWidth, 24, 8);

            GameplaySkinLayoutSnapshot snapshot = solve(
                new[] { 4, 5 },
                skin,
                ManiaGameplaySkinLayoutEnvironment.CreateCompatibility());
            GameplaySkinLaneTopologyGroup secondGroup = snapshot.GroupsInLogicalOrder[1].TopologyGroup;
            GameplaySkinLaneTopologyEntry secondStageFirst = secondGroup.LanesInLogicalOrder[0];
            GameplaySkinLaneTopologyEntry secondStageLast = secondGroup.LanesInLogicalOrder[4];

            Assert.Multiple(() =>
            {
                Assert.That(secondStageFirst.GroupLocalLogicalIndex, Is.Zero);
                Assert.That(secondStageFirst.GlobalLogicalIndex, Is.EqualTo(4));
                Assert.That(secondStageLast.GroupLocalLogicalIndex, Is.EqualTo(4));
                Assert.That(secondStageLast.GlobalLogicalIndex, Is.EqualTo(8));
                Assert.That(snapshot.GetLane(secondStageFirst.Identity.Id).Rect.Width,
                    Is.GreaterThan(snapshot.GetLane(secondStageLast.Identity.Id).Rect.Width * 4));
                Assert.That(snapshot.GetLane(secondStageFirst.Identity.Id).Rect.Width,
                    Is.GreaterThan(snapshot.LanesInLogicalOrder[3].Rect.Width * 4));
            });
        }

        [TestCase((int)SkinCurrentRevisionSourceKind.RealmPackage)]
        [TestCase((int)SkinCurrentRevisionSourceKind.ManagedFolder)]
        [TestCase((int)SkinCurrentRevisionSourceKind.ExternalFolder)]
        public void TestExactCommonMaterialResolutionAndLegacyBeatmapPriority(int sourceKindValue)
        {
            var sourceKind = (SkinCurrentRevisionSourceKind)sourceKindValue;
            Guid selectedId = Guid.NewGuid();
            Texture texture = (Texture)RuntimeHelpers.GetUninitializedObject(typeof(Texture));
            const string configuration = """
                                         [GameplaySkin.Common:1]
                                         Target: Lane ruleset=mania keymode=5k stage-mode=single group=mania.group.stage-1 lane=mania.lane.column-1 group-logical=0 group-visual=0 global-logical=0 global-visual=0 group-local-logical=0 group-local-visual=0
                                         object.note: resource Provide "selected-note"
                                         object.long-note.body: colour Provide "invalid-body"
                                         object.long-note.tail: resource Suppress
                                         playfield.key: resource Suppress
                                         effect.key-flash: resource Provide "unsupported-flash"
                                         author.private-slot: resource Provide "private-value"
                                         """;
            const string stale_configuration = """
                                              [GameplaySkin.Common:1]
                                              Target: Lane ruleset=mania keymode=5k stage-mode=single group=mania.group.stage-1 lane=mania.lane.column-1 group-logical=0 group-visual=0 global-logical=0 global-visual=0 group-local-logical=0 group-local-visual=0
                                              object.note: resource Provide "stale-note"
                                              """;

            using var selected = new DocumentTestSkin(selectedId, configuration, texture);
            using var staleSameId = new DocumentTestSkin(selectedId, stale_configuration, texture);
            using var legacyBeatmap = new ResourceTestLegacyBeatmapSkin(texture, "[General]\nName: legacy-a\n");
            using var alternateLegacyBeatmap = new ResourceTestLegacyBeatmapSkin(texture, "[General]\nName: legacy-b\n");
            // The stale same-ID source deliberately precedes the current owner. C4 must select by the retained
            // C2 package owner/content revision, never by record ID or source-vector order.
            var source = new OrderedTestSkinSource(legacyBeatmap, staleSameId, selected);
            var revision = new SkinCurrentRevision(
                17,
                selectedId,
                "package-content-a",
                sourceKind,
                selected,
                false,
                _ => { });
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.Create(revision);
            var topologyOwner = new ManiaGameplaySkinLaneTopologyRevisionOwner();
            GameplaySkinLayoutSnapshot snapshot = ManiaGameplaySkinLayoutSolver.Solve(
                topologyOwner.Publish(createBeatmap(5)),
                source,
                package,
                3,
                ManiaGameplaySkinLayoutEnvironment.CreateCompatibility(),
                GameplaySkinScrollDirection.Down);
            GameplaySkinResolvedMaterialSet materials = ManiaGameplaySkinMaterialResolver.Resolve(snapshot, source);
            GameplaySkinLaneTopologyGroup group = snapshot.Context.Topology.GroupsInLogicalOrder[0];
            GameplaySkinLaneTopologyEntry lane = group.LanesInLogicalOrder[0];
            GameplaySkinResolvedMaterialTarget target = GameplaySkinResolvedMaterialTarget.ForLane(group, lane);

            Assert.Multiple(() =>
            {
                Assert.That(materials.Snapshot, Is.SameAs(snapshot));
                Assert.That(materials.PackageRevision, Is.SameAs(package));
                Assert.That(materials.ContractIdentity, Is.EqualTo(GameplaySkinMaterialContractIdentity.Current));
                Assert.That(materials.Entries, Has.Count.EqualTo(25));

                Assert.That(materials.TryGet(new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.Note, target), out GameplaySkinResolvedMaterialEntry? note), Is.True);
                Assert.That(note!.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Provide));
                Assert.That(note.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.SelectedPackage));
                Assert.That(note.Source.StableId, Is.EqualTo("selected-common"));
                Assert.That(note.TryGetMaterial<IManiaGameplaySkinMaterial>(out _), Is.True);

                Assert.That(materials.TryGet(new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.LongNoteBody, target), out GameplaySkinResolvedMaterialEntry? body), Is.True);
                Assert.That(body!.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.ProgrammaticFallback));
                Assert.That(materials.Diagnostics.Any(diagnostic => diagnostic.Code == "mania.document.invalid"
                                                                    && ReferenceEquals(diagnostic.Key?.Slot, GameplaySkinSlotCatalog.LongNoteBody)), Is.True);

                Assert.That(materials.TryGet(new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.LongNoteTail, target), out GameplaySkinResolvedMaterialEntry? tail), Is.True);
                Assert.That(tail!.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Suppress));
                Assert.That(tail.Source.StableId, Is.EqualTo("selected-common"));

                Assert.That(materials.TryGet(new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.KeyVisual, target), out GameplaySkinResolvedMaterialEntry? key), Is.True);
                Assert.That(key!.State, Is.EqualTo(GameplaySkinResolvedMaterialState.Provide));
                Assert.That(key.Source.Kind, Is.EqualTo(GameplaySkinResolvedMaterialSourceKind.LegacyBeatmapCompatibility));
                Assert.That(key.Source.StableId, Is.EqualTo("legacy-beatmap-compatibility"));
                Assert.That(key.Source.ContentRevision, Is.EqualTo(legacyBeatmap.GameplaySkinDocument.Identity.ContentRevision));
                Assert.That(key.Source.ContentRevision, Is.Not.EqualTo(alternateLegacyBeatmap.GameplaySkinDocument.Identity.ContentRevision));

                Assert.That(materials.Diagnostics.Any(diagnostic => diagnostic.Code == "OMS-SKIN-CODEC-009"), Is.True);
                Assert.That(materials.Diagnostics.Any(diagnostic => diagnostic.Code == "mania.capability.unsupported-slot"
                                                                    && ReferenceEquals(diagnostic.Key?.Slot, GameplaySkinSlotCatalog.KeyFlash)), Is.True);
                GameplaySkinResolvedMaterialDiagnostic beatmapDiagnostic = materials.Diagnostics.First(diagnostic =>
                    diagnostic.Source?.Kind == GameplaySkinResolvedMaterialSourceKind.LegacyBeatmapCompatibility);
                Assert.That(beatmapDiagnostic.Source!.ContentRevision, Is.EqualTo(legacyBeatmap.GameplaySkinDocument.Identity.ContentRevision));
                Assert.That(beatmapDiagnostic.ToString(), Does.Not.Contain(beatmapDiagnostic.Source.ContentRevision));
                Assert.That(beatmapDiagnostic.Source.ToString(), Does.Not.Contain(beatmapDiagnostic.Source.ContentRevision));
            });
        }

        [Test]
        public void TestExactSelectedPackageRejectsNonAuthoringDocumentSource()
        {
            Guid selectedId = Guid.NewGuid();
            Texture texture = (Texture)RuntimeHelpers.GetUninitializedObject(typeof(Texture));
            using var selected = new DocumentTestSkin(
                selectedId,
                "[GameplaySkin.Common:1]\nobject.note: resource Provide \"selected-note\"\n",
                texture,
                allowsAuthoring: false);
            var source = new OrderedTestSkinSource(selected);
            var revision = new SkinCurrentRevision(
                18,
                selectedId,
                "package-content-b",
                SkinCurrentRevisionSourceKind.RealmPackage,
                selected,
                false,
                _ => { });
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.Create(revision);
            var topologyOwner = new ManiaGameplaySkinLaneTopologyRevisionOwner();
            GameplaySkinLayoutSnapshot snapshot = ManiaGameplaySkinLayoutSolver.Solve(
                topologyOwner.Publish(createBeatmap(4)),
                source,
                package,
                4,
                ManiaGameplaySkinLayoutEnvironment.CreateCompatibility(),
                GameplaySkinScrollDirection.Down);

            Assert.That(
                () => ManiaGameplaySkinMaterialResolver.Resolve(snapshot, source),
                Throws.InvalidOperationException.With.Message.Contains("not eligible"));
        }

        [Test]
        public void TestExactMaterialResourcePreparationHonoursCancellationWithoutProducingSnapshot()
        {
            Guid selectedId = Guid.NewGuid();
            Texture texture = (Texture)RuntimeHelpers.GetUninitializedObject(typeof(Texture));
            using var cancellation = new CancellationTokenSource();
            const string configuration = """
                                         [GameplaySkin.Common:1]
                                         Target: Lane ruleset=mania keymode=4k stage-mode=single group=mania.group.stage-1 lane=mania.lane.column-1 group-logical=0 group-visual=0 global-logical=0 global-visual=0 group-local-logical=0 group-local-visual=0
                                         object.note: resource Provide "selected-note"
                                         """;
            using var selected = new DocumentTestSkin(
                selectedId,
                configuration,
                texture,
                onTextureLookup: cancellation.Cancel);
            var source = new OrderedTestSkinSource(selected);
            var revision = new SkinCurrentRevision(
                19,
                selectedId,
                "package-content-c",
                SkinCurrentRevisionSourceKind.ManagedFolder,
                selected,
                false,
                _ => { });
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.Create(revision);
            var topologyOwner = new ManiaGameplaySkinLaneTopologyRevisionOwner();
            GameplaySkinLayoutSnapshot snapshot = ManiaGameplaySkinLayoutSolver.Solve(
                topologyOwner.Publish(createBeatmap(4)),
                source,
                package,
                5,
                ManiaGameplaySkinLayoutEnvironment.CreateCompatibility(),
                GameplaySkinScrollDirection.Down);

            Assert.Multiple(() =>
            {
                Assert.That(
                    () => ManiaGameplaySkinMaterialResolver.Resolve(snapshot, source, cancellation.Token),
                    Throws.InstanceOf<OperationCanceledException>());
                Assert.That(cancellation.IsCancellationRequested, Is.True);
            });
        }

        [Test]
        public void TestExactOwnerWithoutPublicationCannotReachStandalonePlayfieldFallback()
        {
            using var skin = new TestSkin(Guid.NewGuid(), "exact-owner-probe");
            var revision = new SkinCurrentRevision(
                1,
                skin.SkinInfo.ID,
                "content-a",
                SkinCurrentRevisionSourceKind.RealmPackage,
                skin,
                false,
                _ => { });
            var dependencies = new DependencyContainer();
            dependencies.Cache(new GameplaySkinLayoutRevisionOwner(GameplaySkinPackageRevision.Create(revision)));
            var playfield = new DependencyProbeManiaPlayfield(new List<StageDefinition> { new StageDefinition(4) });

            Assert.That(
                () => playfield.InvokeCreateChildDependencies(dependencies),
                Throws.InvalidOperationException.With.Message.Contains("explicitly cached compatibility layout owner"));
        }

        [Test]
        public void TestExactOwnerCannotConsumeInjectedSnapshotOutsideCurrentPublication()
        {
            using var skin = new TestSkin(Guid.NewGuid(), "exact-owner-injected-snapshot-probe");
            var revision = new SkinCurrentRevision(
                1,
                skin.SkinInfo.ID,
                "content-a",
                SkinCurrentRevisionSourceKind.RealmPackage,
                skin,
                false,
                _ => { });
            var dependencies = new DependencyContainer();
            dependencies.Cache(new GameplaySkinLayoutRevisionOwner(GameplaySkinPackageRevision.Create(revision)));
            dependencies.Cache(ManiaGameplaySkinLayout.CreateCompatibility(
                new[] { new StageDefinition(4) }, new TestSkinSource()).Snapshot);
            var playfield = new DependencyProbeManiaPlayfield(new List<StageDefinition> { new StageDefinition(4) });

            Assert.That(
                () => playfield.InvokeCreateChildDependencies(dependencies),
                Throws.InvalidOperationException.With.Message.Contains("exact committed layout publication"));
        }

        [TestCase(0.55f, 0.8f)]
        [TestCase(4f / 3f, 1f)]
        [TestCase(32f / 9f, 2.25f)]
        public void TestAspectDpiAndSafeBoundsRemainFiniteAndContained(float aspectRatio, float dpiScale)
        {
            var screen = GameplaySkinLayoutRect.Create(0, 0, 1, 1);
            var safe = GameplaySkinLayoutRect.Create(0.08f, 0.04f, 0.84f, 0.9f);
            GameplaySkinLayoutSnapshot snapshot = solve(
                new[] { 5, 5 },
                new TestSkinSource(),
                new ManiaGameplaySkinLayoutEnvironment(screen, safe, aspectRatio, dpiScale));

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Context.AspectRatio, Is.EqualTo(aspectRatio));
                Assert.That(snapshot.Context.DpiScale, Is.EqualTo(dpiScale));
                Assert.That(snapshot.GroupsInLogicalOrder.All(group => safe.Contains(group.Rect)), Is.True);
                Assert.That(snapshot.LanesInLogicalOrder.All(lane => safe.Contains(lane.Rect)), Is.True);
                Assert.That(snapshot.Surfaces.All(surface => safe.Contains(surface.Rect)), Is.True);
                Assert.That(snapshot.LanesInLogicalOrder.All(lane => allFiniteAndPositive(lane.Rect)), Is.True);
            });
        }

        [Test]
        public void TestDpiParticipatesWithoutChangingStableIdentity()
        {
            GameplaySkinLayoutSnapshot lowDpi = solve(
                new[] { 4 },
                new TestSkinSource(),
                new ManiaGameplaySkinLayoutEnvironment(fullBounds(), fullBounds(), 16f / 9f, 1));
            GameplaySkinLayoutSnapshot highDpi = solve(
                new[] { 4 },
                new TestSkinSource(),
                new ManiaGameplaySkinLayoutEnvironment(fullBounds(), fullBounds(), 16f / 9f, 2.25f));

            Assert.Multiple(() =>
            {
                Assert.That(highDpi.GroupsInLogicalOrder[0].Rect.Width, Is.GreaterThan(lowDpi.GroupsInLogicalOrder[0].Rect.Width));
                Assert.That(highDpi.LanesInLogicalOrder.Select(lane => lane.LaneId),
                    Is.EqualTo(lowDpi.LanesInLogicalOrder.Select(lane => lane.LaneId)));
                Assert.That(highDpi.LanesInLogicalOrder.Select(lane => lane.TopologyEntry.Identity.Role),
                    Is.EqualTo(lowDpi.LanesInLogicalOrder.Select(lane => lane.TopologyEntry.Identity.Role)));
            });
        }

        [Test]
        public void TestInvalidFieldsFallbackIndependentlyWithStableDiagnostics()
        {
            var skin = new TestSkinSource();
            skin.Set(LegacyManiaSkinConfigurationLookups.ColumnWidth, float.NaN, 0);
            skin.Set(LegacyManiaSkinConfigurationLookups.ColumnWidth, 120, 1);
            skin.Set(LegacyManiaSkinConfigurationLookups.LeftColumnSpacing, float.PositiveInfinity, 2);
            skin.Set(LegacyManiaSkinConfigurationLookups.RightColumnSpacing, -1, 3);
            skin.Set(LegacyManiaSkinConfigurationLookups.HitPosition, float.NegativeInfinity);
            skin.Set(LegacyManiaSkinConfigurationLookups.StagePaddingTop, -20);
            skin.Set(LegacyManiaSkinConfigurationLookups.StagePaddingBottom, 400);
            skin.Set(LegacyManiaSkinConfigurationLookups.BarLineHeight, float.NaN);
            skin.Set(LegacyManiaSkinConfigurationLookups.ComboPosition, float.PositiveInfinity);

            GameplaySkinLayoutSnapshot snapshot = solve(new[] { 5 }, skin, ManiaGameplaySkinLayoutEnvironment.CreateCompatibility());
            string[] diagnostics = snapshot.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(diagnostics, Does.Contain("mania.layout.column-width-fallback"));
                Assert.That(diagnostics, Does.Contain("mania.layout.column-left-spacing-fallback"));
                Assert.That(diagnostics, Does.Contain("mania.layout.column-right-spacing-fallback"));
                Assert.That(diagnostics, Does.Contain("mania.layout.hit-position-fallback"));
                Assert.That(diagnostics, Does.Contain("mania.layout.stage-padding-top-fallback"));
                Assert.That(diagnostics, Does.Contain("mania.layout.stage-padding-bottom-fallback"));
                Assert.That(diagnostics, Does.Contain("mania.layout.barline-height-fallback"));
                Assert.That(diagnostics, Does.Contain("mania.layout.combo-position-fallback"));
                Assert.That(diagnostics, Does.Contain("mania.layout.environment-fallback"));
                Assert.That(snapshot.LanesInLogicalOrder.All(lane => allFiniteAndPositive(lane.Rect)), Is.True);
                Assert.That(snapshot.LanesInLogicalOrder[1].Rect.Width, Is.GreaterThan(snapshot.LanesInLogicalOrder[0].Rect.Width));
                Assert.That(snapshot.Context.SafeBounds.Contains(snapshot.GetSurface(ManiaGameplaySkinLayout.JUDGEMENT_SURFACE).Rect), Is.True);
                Assert.That(snapshot.Context.SafeBounds.Contains(snapshot.GetSurface(ManiaGameplaySkinLayout.COMBO_SURFACE).Rect), Is.True);
            });
        }

        [Test]
        public void TestComboPositionIsSolvedOnceIntoHudSurface()
        {
            var skin = new TestSkinSource();
            skin.Set(LegacyManiaSkinConfigurationLookups.ComboPosition, 384);

            GameplaySkinLayoutSnapshot snapshot = solve(new[] { 5 }, skin, new ManiaGameplaySkinLayoutEnvironment(fullBounds(), fullBounds(), 4f / 3f, 1));
            GameplaySkinLayoutRect combo = snapshot.GetSurface(ManiaGameplaySkinLayout.COMBO_SURFACE).Rect;

            Assert.Multiple(() =>
            {
                Assert.That(combo.Top + combo.Height / 2, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(snapshot.Context.SafeBounds.Contains(combo), Is.True);
                Assert.That(snapshot.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Not.Contain("mania.layout.combo-position-fallback"));
            });
        }

        [Test]
        public void TestPublishedNativeStageVectorIsDefensivelyImmutable()
        {
            var beatmap = createBeatmap(4, 5);
            var topologyOwner = new ManiaGameplaySkinLaneTopologyRevisionOwner();
            ManiaGameplaySkinLaneTopologyPublication publication = topologyOwner.Publish(beatmap);
            beatmap.Stages.Clear();
            beatmap.Stages.Add(new StageDefinition(9));

            GameplaySkinLayoutSnapshot snapshot = solve(publication, new TestSkinSource(), ManiaGameplaySkinLayoutEnvironment.CreateCompatibility());

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Context.NativeContextId, Is.EqualTo("stages-4-5"));
                Assert.That(snapshot.GroupsInLogicalOrder.Select(group => group.TopologyGroup.LanesInLogicalOrder.Count), Is.EqualTo(new[] { 4, 5 }));
                Assert.That(snapshot.LanesInLogicalOrder, Has.Count.EqualTo(9));
            });
        }

        private static GameplaySkinLayoutSnapshot solve(params int[] stageColumns)
            => solve(stageColumns, new TestSkinSource(), ManiaGameplaySkinLayoutEnvironment.CreateCompatibility());

        private static GameplaySkinLayoutSnapshot solve(
            int[] stageColumns,
            TestSkinSource skin,
            ManiaGameplaySkinLayoutEnvironment environment)
        {
            var topologyOwner = new ManiaGameplaySkinLaneTopologyRevisionOwner();
            return solve(topologyOwner.Publish(createBeatmap(stageColumns)), skin, environment);
        }

        private static GameplaySkinLayoutSnapshot solve(
            ManiaGameplaySkinLaneTopologyPublication topologyPublication,
            TestSkinSource skin,
            ManiaGameplaySkinLayoutEnvironment environment)
        {
            GameplaySkinLayoutRevisionOwner owner = GameplaySkinLayoutRevisionOwner.CreateCompatibility();
            GameplaySkinPreparedLayout prepared = owner.Prepare(revision => ManiaGameplaySkinLayoutSolver.Solve(
                topologyPublication,
                skin,
                owner.PackageRevision,
                revision,
                environment,
                GameplaySkinScrollDirection.Down));
            Assert.That(owner.TryCommit(prepared), Is.True);
            return owner.Current!;
        }

        private static ManiaBeatmap createBeatmap(params int[] stageColumns)
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(stageColumns[0]));

            foreach (int columns in stageColumns.Skip(1))
                beatmap.Stages.Add(new StageDefinition(columns));

            return beatmap;
        }

        private static GameplaySkinLayoutRect fullBounds() => GameplaySkinLayoutRect.Create(0, 0, 1, 1);

        private static bool allFiniteAndPositive(GameplaySkinLayoutRect rect)
            => float.IsFinite(rect.X) && float.IsFinite(rect.Y) && float.IsFinite(rect.Width) && float.IsFinite(rect.Height)
               && rect.Width > 0 && rect.Height > 0;

        private sealed class TestSkinSource : ISkinSource
        {
            private readonly Dictionary<(LegacyManiaSkinConfigurationLookups Lookup, int? Column), float> values = new();

            public event Action? SourceChanged;

            public IEnumerable<ISkin> AllSources => new[] { this };

            public void Set(LegacyManiaSkinConfigurationLookups lookup, float value, int? column = null)
                => values[(lookup, column)] = value;

            public ISkin? FindProvider(Func<ISkin, bool> lookupFunction) => lookupFunction(this) ? this : null;

            public Drawable? GetDrawableComponent(ISkinComponentLookup lookup) => null;

            public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public ISample? GetSample(ISampleInfo sampleInfo) => null;

            public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
                where TLookup : notnull
                where TValue : notnull
            {
                if (lookup is ManiaSkinConfigurationLookup maniaLookup
                    && typeof(TValue) == typeof(float)
                    && values.TryGetValue((maniaLookup.Lookup, maniaLookup.ColumnIndex), out float value))
                {
                    return (IBindable<TValue>)(object)new Bindable<float>(value);
                }

                return null;
            }

            public void NotifyChanged() => SourceChanged?.Invoke();
        }

        private sealed partial class DependencyProbeManiaPlayfield : ManiaPlayfield
        {
            public DependencyProbeManiaPlayfield(List<StageDefinition> stages)
                : base(stages)
            {
            }

            public IReadOnlyDependencyContainer InvokeCreateChildDependencies(IReadOnlyDependencyContainer parent)
                => CreateChildDependencies(parent);
        }

        private sealed class TestSkin : Skin
        {
            public TestSkin(Guid id, string name)
                : base(new SkinInfo(name) { ID = id }, null)
            {
            }

            public override ISample? GetSample(ISampleInfo sampleInfo) => null;

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup) => null;
        }

        private sealed class DocumentTestSkin : Skin
        {
            private readonly Texture texture;
            private readonly bool allowsAuthoring;
            private readonly Action? onTextureLookup;

            public override bool AllowsGameplaySkinDocumentAuthoring => allowsAuthoring;

            public DocumentTestSkin(
                Guid id,
                string configuration,
                Texture texture,
                bool allowsAuthoring = true,
                Action? onTextureLookup = null)
                : base(
                    new SkinInfo("C4 document test") { ID = id },
                    null,
                    new ConfigurationResourceStore(Encoding.UTF8.GetBytes(configuration)))
            {
                this.texture = texture;
                this.allowsAuthoring = allowsAuthoring;
                this.onTextureLookup = onTextureLookup;
            }

            public override ISample? GetSample(ISampleInfo sampleInfo) => null;

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT)
            {
                onTextureLookup?.Invoke();
                return componentName == "selected-note" ? texture : null;
            }

            public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup) => null;
        }

        private sealed class ResourceTestLegacyBeatmapSkin : LegacyBeatmapSkin
        {
            private readonly Texture texture;

            public ResourceTestLegacyBeatmapSkin(Texture texture, string configuration)
                : base(new BeatmapInfo(), null)
            {
                this.texture = texture;

                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(configuration), writable: false);
                ParseConfigurationStream(stream);
            }

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT)
                => componentName.StartsWith("mania-key", StringComparison.Ordinal) ? texture : null;
        }

        private sealed class OrderedTestSkinSource : ISkinSource
        {
            private readonly ISkin[] sources;

            public event Action? SourceChanged;

            public IEnumerable<ISkin> AllSources => sources;

            public OrderedTestSkinSource(params ISkin[] sources)
            {
                this.sources = sources;
            }

            public ISkin? FindProvider(Func<ISkin, bool> lookupFunction)
                => sources.FirstOrDefault(lookupFunction);

            public Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
                => sources.Select(source => source.GetDrawableComponent(lookup)).FirstOrDefault(drawable => drawable != null);

            public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT)
                => sources.Select(source => source.GetTexture(componentName, wrapModeS, wrapModeT)).FirstOrDefault(texture => texture != null);

            public ISample? GetSample(ISampleInfo sampleInfo)
                => sources.Select(source => source.GetSample(sampleInfo)).FirstOrDefault(sample => sample != null);

            public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
                where TLookup : notnull
                where TValue : notnull
                => sources.Select(source => source.GetConfig<TLookup, TValue>(lookup)).FirstOrDefault(value => value != null);

            public void NotifyChanged() => SourceChanged?.Invoke();
        }

        private sealed class ConfigurationResourceStore : IResourceStore<byte[]>
        {
            private readonly byte[] configuration;

            public ConfigurationResourceStore(byte[] configuration)
            {
                this.configuration = configuration;
            }

            public byte[] Get(string name) => name == "skin.ini" ? configuration.ToArray() : null!;

            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default)
                => Task.FromResult(Get(name));

            public Stream? GetStream(string name)
                => name == "skin.ini" ? new MemoryStream(configuration, writable: false) : null;

            public IEnumerable<string> GetAvailableResources() => new[] { "skin.ini" };

            public void Dispose()
            {
            }
        }
    }
}
