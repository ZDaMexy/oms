// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsGameplayLayoutSolverTest
    {
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.P1)]
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.P2)]
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.Center)]
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.CenterRightScratch)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P1)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P2)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.Center)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.CenterRightScratch)]
        [TestCase(BmsKeymode.Key9K_Bms, BmsPlayfieldStyle.Center)]
        [TestCase(BmsKeymode.Key9K_Pms, BmsPlayfieldStyle.Center)]
        [TestCase(BmsKeymode.Key14K, BmsPlayfieldStyle.Center)]
        public void TestCanonicalMatrixProducesOneCompleteSnapshot(BmsKeymode keymode, BmsPlayfieldStyle style)
        {
            BmsBeatmap beatmap = createBeatmap(keymode);
            var provider = new BmsGameplayLayoutProvider(beatmap);
            BmsGameplayLayoutSnapshot snapshot = provider.PublishForTesting(style, new BmsGameplayLayoutConfiguration());

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Keymode, Is.EqualTo(keymode));
                Assert.That(snapshot.Style, Is.EqualTo(style.GetAppliedStyle(keymode)));
                Assert.That(snapshot.LanesInLogicalOrder, Has.Count.EqualTo(BmsRuleset.GetLaneCount(keymode)));
                Assert.That(snapshot.Neutral.LanesInLogicalOrder.Select(lane => lane.TopologyEntry.GlobalLogicalIndex), Is.EqualTo(Enumerable.Range(0, BmsRuleset.GetLaneCount(keymode))));
                Assert.That(snapshot.Neutral.LanesInLogicalOrder.Select(lane => lane.TopologyEntry.GlobalVisualIndex), Is.EquivalentTo(Enumerable.Range(0, BmsRuleset.GetLaneCount(keymode))));
                Assert.That(snapshot.Neutral.Surfaces.Select(surface => surface.Id), Does.Contain(BmsGameplayLayoutSurfaceIds.Playfield));
                Assert.That(snapshot.Neutral.Surfaces.Select(surface => surface.Id), Does.Contain(BmsGameplayLayoutSurfaceIds.Gauge));
                Assert.That(snapshot.Neutral.Surfaces.Select(surface => surface.Id), Does.Contain(BmsGameplayLayoutSurfaceIds.Combo));
                Assert.That(snapshot.Neutral.Surfaces.Select(surface => surface.Id), Does.Contain(BmsGameplayLayoutSurfaceIds.Judgement));
                Assert.That(snapshot.BgaViewports, Is.Not.Empty);
                Assert.That(snapshot.LanesInLogicalOrder.Select(lane => lane.LaneId.Value), Is.Unique);
                Assert.That(snapshot.KeymodeResolution, Is.SameAs(beatmap.BmsInfo.KeymodeResolution));
                Assert.That(snapshot.KeymodeDiagnostic, Does.StartWith("bms.keymode."));
            });

            assertCompleteAndBounded(snapshot);
        }

        [Test]
        public void TestFourteenKeyDeckScratchGapAndBgaMatrix()
        {
            var provider = new BmsGameplayLayoutProvider(createBeatmap(BmsKeymode.Key14K));
            BmsGameplayLayoutSnapshot snapshot = provider.PublishForTesting(BmsPlayfieldStyle.Center, new BmsGameplayLayoutConfiguration());
            GameplaySkinLayoutGroup first = snapshot.Neutral.GroupsInLogicalOrder[0];
            GameplaySkinLayoutGroup second = snapshot.Neutral.GroupsInLogicalOrder[1];

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Neutral.GroupsInLogicalOrder, Has.Count.EqualTo(2));
                Assert.That(first.TopologyGroup.LanesInLogicalOrder, Has.Count.EqualTo(8));
                Assert.That(second.TopologyGroup.LanesInLogicalOrder, Has.Count.EqualTo(8));
                Assert.That(snapshot.GetLaneByLogicalIndex(0).LaneId.Value, Is.EqualTo("bms.lane.scratch-1"));
                Assert.That(snapshot.GetLaneByLogicalIndex(15).LaneId.Value, Is.EqualTo("bms.lane.scratch-2"));
                Assert.That(snapshot.GetLaneByLogicalIndex(14).LaneId.Value, Is.EqualTo("bms.lane.key-14"));
                Assert.That(second.Rect.Left - first.Rect.Right, Is.GreaterThan(0));
                Assert.That(snapshot.BgaViewports, Has.Count.EqualTo(4));
                Assert.That(snapshot.BgaViewports.All(viewport => !viewport.Intersects(snapshot.PlayfieldRect)), Is.True);
            });
        }

        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.P1, 92)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P1, 116)]
        [TestCase(BmsKeymode.Key9K_Bms, BmsPlayfieldStyle.Center, 126)]
        [TestCase(BmsKeymode.Key9K_Pms, BmsPlayfieldStyle.Center, 126)]
        [TestCase(BmsKeymode.Key14K, BmsPlayfieldStyle.Center, 228)]
        public void TestEveryPublicSlotExpandsToExactApplicableBmsTargets(
            BmsKeymode keymode,
            BmsPlayfieldStyle style,
            int expectedEntryCount)
        {
            BmsGameplayLayoutSnapshot layout = new BmsGameplayLayoutProvider(createBeatmap(keymode))
                                                       .PublishForTesting(style, new BmsGameplayLayoutConfiguration());
            GameplaySkinResolvedMaterialKey[] keys = GameplaySkinSlotCatalog.All
                                                                          .SelectMany(descriptor =>
                                                                              GameplaySkinPublicSlotMaterialTargets.Enumerate(descriptor, layout.Neutral)
                                                                                                                   .Select(target => new GameplaySkinResolvedMaterialKey(descriptor, target)))
                                                                          .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(BmsGameplayResolvedNoteMaterialPreparer.RuntimeCapabilities.Support, Has.Count.EqualTo(28));
                Assert.That(keys, Has.Length.EqualTo(expectedEntryCount));
                Assert.That(keys, Is.Unique);
                Assert.That(keys.Select(key => key.Slot).Distinct(), Is.EquivalentTo(
                    keymode is BmsKeymode.Key9K_Bms or BmsKeymode.Key9K_Pms
                        ? GameplaySkinSlotCatalog.Common
                        : GameplaySkinSlotCatalog.All));
                Assert.That(keys.Where(key => key.Target.Kind == GameplaySkinResolvedMaterialTargetKind.Lane)
                                .All(key => key.Target.GroupId != null
                                            && key.Target.LaneId != null
                                            && key.Target.GroupLogicalIndex.HasValue
                                            && key.Target.GroupVisualIndex.HasValue
                                            && key.Target.GlobalLogicalIndex.HasValue
                                            && key.Target.GlobalVisualIndex.HasValue
                                            && key.Target.GroupLocalLogicalIndex.HasValue
                                            && key.Target.GroupLocalVisualIndex.HasValue), Is.True);
            });

            GameplaySkinResolvedMaterialKey[] scratchExtension = keys.Where(key =>
                ReferenceEquals(key.Slot, GameplaySkinSlotCatalog.Turntable)
                || ReferenceEquals(key.Slot, GameplaySkinSlotCatalog.Laser)).ToArray();
            int expectedScratchExtensionCount = keymode is BmsKeymode.Key9K_Bms or BmsKeymode.Key9K_Pms
                ? 0
                : keymode == BmsKeymode.Key14K ? 4 : 2;
            Assert.That(scratchExtension, Has.Length.EqualTo(expectedScratchExtensionCount));

            if (keymode == BmsKeymode.Key14K)
            {
                GameplaySkinLaneTopologyGroup[] groups = layout.Neutral.Context.Topology.GroupsInLogicalOrder.ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(groups, Has.Length.EqualTo(2));
                    Assert.That(keys.Count(key => ReferenceEquals(key.Slot, GameplaySkinSlotCatalog.StageBackground)), Is.EqualTo(2));
                    Assert.That(keys.Count(key => ReferenceEquals(key.Slot, GameplaySkinSlotCatalog.BarLine)), Is.EqualTo(2));
                    Assert.That(scratchExtension.Select(key => key.Target.LaneId!.Value),
                        Is.EquivalentTo(new[]
                        {
                            "bms.lane.scratch-1", "bms.lane.scratch-1",
                            "bms.lane.scratch-2", "bms.lane.scratch-2",
                        }));
                    Assert.That(groups.Select(group => group.Identity.Id.Value),
                        Is.EqualTo(new[] { "bms.group.deck-1", "bms.group.deck-2" }));
                });
            }
        }

        [Test]
        public void TestStableIdsSurviveStyleGeometryAndLayoutRevision()
        {
            var provider = new BmsGameplayLayoutProvider(createBeatmap(BmsKeymode.Key7K));
            BmsGameplayLayoutSnapshot first = provider.PublishForTesting(BmsPlayfieldStyle.P1, new BmsGameplayLayoutConfiguration());
            BmsGameplayLayoutSnapshot second = provider.PublishForTesting(BmsPlayfieldStyle.P2, new BmsGameplayLayoutConfiguration { PlayfieldWidth = 0.61f });
            BmsGameplayLayoutSnapshot third = provider.PublishForTesting(BmsPlayfieldStyle.CenterRightScratch, new BmsGameplayLayoutConfiguration { NormalLaneRelativeSpacing = 0.2f });

            Assert.Multiple(() =>
            {
                Assert.That(second.Context.LayoutRevision, Is.EqualTo(first.Context.LayoutRevision + 1));
                Assert.That(third.Context.LayoutRevision, Is.EqualTo(second.Context.LayoutRevision + 1));
                Assert.That(second.LanesInLogicalOrder.Select(lane => lane.LaneId.Value), Is.EqualTo(first.LanesInLogicalOrder.Select(lane => lane.LaneId.Value)));
                Assert.That(third.LanesInLogicalOrder.Select(lane => lane.LaneId.Value), Is.EqualTo(first.LanesInLogicalOrder.Select(lane => lane.LaneId.Value)));
                Assert.That(second.Neutral.GroupsInLogicalOrder.Select(group => group.GroupId.Value), Is.EqualTo(first.Neutral.GroupsInLogicalOrder.Select(group => group.GroupId.Value)));
                Assert.That(first.GetLaneByLogicalIndex(0).VisualIndex, Is.EqualTo(0));
                Assert.That(second.GetLaneByLogicalIndex(0).VisualIndex, Is.EqualTo(7));
                Assert.That(third.GetLaneByLogicalIndex(0).VisualIndex, Is.EqualTo(7));
            });
        }

        [TestCase(0.62f, 1f, 0f, 0f, 1f, 1f)]
        [TestCase(1f, 1.25f, 0.03f, 0.04f, 0.94f, 0.91f)]
        [TestCase(16f / 9f, 1.5f, 0.08f, 0.02f, 0.84f, 0.95f)]
        [TestCase(3.55f, 2f, 0.02f, 0.07f, 0.96f, 0.86f)]
        public void TestAspectDpiAndSafeAreaMatrix(float aspect, float dpi, float safeX, float safeY, float safeWidth, float safeHeight)
        {
            var environment = new BmsGameplayLayoutEnvironment(
                GameplaySkinLayoutRect.Create(0, 0, 1, 1),
                GameplaySkinLayoutRect.Create(safeX, safeY, safeWidth, safeHeight),
                aspect,
                dpi);
            var provider = new BmsGameplayLayoutProvider(createBeatmap(BmsKeymode.Key14K));
            BmsGameplayLayoutSnapshot snapshot = provider.PublishForTesting(BmsPlayfieldStyle.Center, new BmsGameplayLayoutConfiguration(), environment);

            assertCompleteAndBounded(snapshot);
            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Context.AspectRatio, Is.EqualTo(aspect));
                Assert.That(snapshot.Context.DpiScale, Is.EqualTo(dpi));
                Assert.That(snapshot.Context.SafeBounds, Is.EqualTo(environment.SafeBounds));
                Assert.That(snapshot.BgaViewports.All(viewport => !viewport.Intersects(snapshot.PlayfieldRect)), Is.True);
                Assert.That(snapshot.BgaViewports.All(viewport => !viewport.Intersects(snapshot.GaugeRect)), Is.True);
            });
        }

        [Test]
        public void TestEveryInvalidGeometryFieldFallsBackIndependently()
        {
            var provider = new BmsGameplayLayoutProvider(createBeatmap(BmsKeymode.Key7K));
            BmsGameplayLayoutSnapshot snapshot = provider.PublishForTesting(BmsPlayfieldStyle.Center, new BmsGameplayLayoutConfiguration
            {
                NormalLaneRelativeWidth = 0,
                ScratchLaneRelativeWidth = float.NegativeInfinity,
                NormalLaneRelativeSpacing = -1,
                ScratchLaneRelativeSpacing = float.PositiveInfinity,
                PlayfieldWidth = float.NaN,
                PlayfieldHeight = -2,
                HitTargetHeight = -1,
                HitTargetBarHeight = float.PositiveInfinity,
                HitTargetLineHeight = -5,
                HitTargetGlowRadius = -1,
                BarLineHeight = 0,
            });

            string[] diagnostics = snapshot.Neutral.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(diagnostics, Has.Length.EqualTo(11));
                Assert.That(diagnostics, Is.Unique);
                Assert.That(diagnostics.All(code => code.StartsWith("bms.layout.invalid-", StringComparison.Ordinal)), Is.True);
                Assert.That(snapshot.Profile.NormalLaneRelativeWidth, Is.EqualTo(1));
                Assert.That(snapshot.Profile.ScratchLaneRelativeWidth, Is.EqualTo(1.5f));
                Assert.That(snapshot.Profile.NormalLaneRelativeSpacing, Is.Zero);
                Assert.That(snapshot.Profile.ScratchLaneRelativeSpacing, Is.EqualTo(0.12f));
                Assert.That(snapshot.Profile.PlayfieldWidth, Is.EqualTo(0.396f).Within(0.0001f));
                Assert.That(snapshot.Profile.PlayfieldHeight, Is.EqualTo(BmsPlayfieldLayoutProfile.DEFAULT_PLAYFIELD_HEIGHT));
                Assert.That(snapshot.Profile.HitTargetHeight, Is.EqualTo(16));
                Assert.That(snapshot.Profile.HitTargetBarHeight, Is.EqualTo(12));
                Assert.That(snapshot.Profile.HitTargetLineHeight, Is.EqualTo(3));
                Assert.That(snapshot.Profile.HitTargetGlowRadius, Is.EqualTo(6));
                Assert.That(snapshot.Profile.BarLineHeight, Is.EqualTo(2));
            });

            assertCompleteAndBounded(snapshot);
        }

        [Test]
        public void TestPlayfieldStageLaneAndTargetRetainSameSnapshot()
        {
            var playfield = BmsPlayfield.CreateCompatibility(createBeatmap(BmsKeymode.Key14K));
            BmsGameplayLayoutSnapshot snapshot = playfield.LayoutSnapshot;

            Assert.Multiple(() =>
            {
                Assert.That(playfield.GroupContainers.All(group => ReferenceEquals(group.LayoutSnapshot, snapshot)), Is.True);
                Assert.That(playfield.Lanes.All(lane => ReferenceEquals(lane.LayoutSnapshot, snapshot)), Is.True);
                Assert.That(playfield.Lanes.All(lane => ReferenceEquals(lane.HitTarget.LayoutSnapshot, snapshot)), Is.True);
                Assert.That(playfield.Lanes.Select(lane => lane.LayoutSnapshotLane!.LaneId.Value), Is.EqualTo(snapshot.LanesInLogicalOrder.Select(lane => lane.LaneId.Value)));
            });
        }

        [Test]
        public void TestTypedLaneLayoutIsDefensivelyImmutable()
        {
            var provider = new BmsGameplayLayoutProvider(createBeatmap(BmsKeymode.Key14K));
            BmsGameplayLayoutSnapshot snapshot = provider.PublishForTesting(BmsPlayfieldStyle.Center, new BmsGameplayLayoutConfiguration());
            BmsLaneLayout.Lane original = snapshot.LaneLayout.Lanes[0];
            BmsGameplayLayoutLane typedOriginal = snapshot.GetLaneByLogicalIndex(0);
            GameplaySkinLayoutRect geometryOriginal = typedOriginal.NeutralLane.Rect;

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.LaneLayout.Lanes, Is.Not.InstanceOf<BmsLaneLayout.Lane[]>());
                Assert.That(() => ((IList<BmsLaneLayout.Lane>)snapshot.LaneLayout.Lanes)[0] = default, Throws.TypeOf<NotSupportedException>());
                Assert.That(snapshot.LaneLayout.Lanes[0].LaneIndex, Is.EqualTo(original.LaneIndex));
                Assert.That(snapshot.LaneLayout.Lanes[0].Action, Is.EqualTo(original.Action));
                Assert.That(snapshot.LaneLayout.Lanes[0].RelativeStart, Is.EqualTo(original.RelativeStart));
                Assert.That(snapshot.GetLaneByLogicalIndex(0), Is.SameAs(typedOriginal));
                Assert.That(snapshot.GetLaneByLogicalIndex(0).Action, Is.EqualTo(original.Action));
                Assert.That(snapshot.GetLaneByLogicalIndex(0).NeutralLane.Rect, Is.EqualTo(geometryOriginal));
            });
        }

        private static BmsBeatmap createBeatmap(BmsKeymode keymode)
            => new BmsBeatmap
            {
                BmsInfo = new BmsBeatmapInfo { Keymode = keymode },
            };

        private static void assertCompleteAndBounded(BmsGameplayLayoutSnapshot snapshot)
        {
            GameplaySkinLayoutRect safe = snapshot.Context.SafeBounds;

            Assert.Multiple(() =>
            {
                Assert.That(safe.Contains(snapshot.PlayfieldRect), Is.True);
                Assert.That(safe.Contains(snapshot.GaugeRect), Is.True);
                Assert.That(safe.Contains(snapshot.ComboRect), Is.True);
                Assert.That(safe.Contains(snapshot.HudRect), Is.True);
                Assert.That(snapshot.Neutral.LanesInLogicalOrder.All(lane => safe.Contains(lane.Rect)), Is.True);
                Assert.That(snapshot.Neutral.GroupsInLogicalOrder.All(group => safe.Contains(group.Rect)), Is.True);
                Assert.That(snapshot.Neutral.Surfaces.All(surface => safe.Contains(surface.Rect)), Is.True);
                Assert.That(snapshot.Neutral.Surfaces.All(surface =>
                    float.IsFinite(surface.Rect.X)
                    && float.IsFinite(surface.Rect.Y)
                    && float.IsFinite(surface.Rect.Width)
                    && float.IsFinite(surface.Rect.Height)
                    && surface.Rect.Width > 0
                    && surface.Rect.Height > 0), Is.True);
                Assert.That(snapshot.PlayfieldRect.Intersects(snapshot.GaugeRect), Is.False);
            });
        }
    }
}
