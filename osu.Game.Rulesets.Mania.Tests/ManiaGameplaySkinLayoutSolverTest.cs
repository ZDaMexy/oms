// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Game.Audio;
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
    }
}
