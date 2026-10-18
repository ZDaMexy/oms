// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.Tests
{
    public sealed partial class ManiaGameplaySkinLayoutSolverTest
    {
        [TestCase(5, false)]
        [TestCase(5, true)]
        [TestCase(6, false)]
        [TestCase(6, true)]
        [TestCase(7, false)]
        [TestCase(7, true)]
        [TestCase(8, false)]
        [TestCase(8, true)]
        [TestCase(14, false)]
        [TestCase(16, false)]
        public void TestAuthorEmptySlotsKeepTheExactNativeColumnsAndKeyboard(int total, bool right)
        {
            int[] counts = total > 10 ? new[] { total / 2, total / 2 } : new[] { total };
            var topologyOwner = new ManiaGameplaySkinLaneTopologyRevisionOwner();
            ManiaGameplaySkinLaneTopologyPublication topology = topologyOwner.Publish(createBeatmap(counts));
            GameplaySkinAuthorLayout author = authorLayout(counts, right);
            GameplaySkinLayoutSnapshot result = ManiaGameplaySkinLayoutSolver.Solve(topology, new TestSkinSource(),
                GameplaySkinLayoutRevisionOwner.CreateCompatibility().PackageRevision, 0, authoredEnvironment(),
                GameplaySkinScrollDirection.Down, author);
            Assert.Multiple(() =>
            {
                Assert.That(result.Context.Topology, Is.SameAs(topology.Publication.Topology));
                Assert.That(result.LanesInLogicalOrder, Has.Count.EqualTo(total));
                Assert.That(result.Context.KeymodeId, Is.EqualTo(string.Join("-", counts.Select(n => $"{n}k"))));
                Assert.That(result.Context.NativeContextId, Is.EqualTo($"stages-{string.Join("-", counts)}"));
                Assert.That(result.LanesInLogicalOrder.Any(l => l.TopologyEntry.Identity.Role == GameplaySkinLaneRole.Scratch), Is.False);
                Assert.That(result.Context.Topology.LanesInLogicalOrder.Count(l => l.Identity.Role == GameplaySkinLaneRole.SpecialKey), Is.EqualTo(counts.Sum(n => n % 2)));
                Assert.That(result.Diagnostics, Is.Empty);
                Assert.That(result.Surfaces.Count(s => s.Id.StartsWith(ManiaGameplaySkinLayout.KEY_VISUAL_SURFACE_PREFIX, StringComparison.Ordinal)), Is.EqualTo(total));
                Assert.That(result.Surfaces.All(s => result.Context.SafeBounds.Contains(s.Rect)), Is.True);
            });
            foreach (GameplaySkinLayoutGroup group in result.GroupsInLogicalOrder)
                foreach (GameplaySkinLaneTopologyEntry lane in group.TopologyGroup.LanesInLogicalOrder)
                {
                    GameplaySkinAuthorLaneSpan span = author.Stages[group.TopologyGroup.LogicalIndex].Lanes[lane.GroupLocalLogicalIndex];
                    GameplaySkinLayoutLane actual = result.GetLane(lane.Identity.Id);
                    Assert.That(actual.TopologyEntry, Is.SameAs(lane));
                    Assert.That(actual.Rect.Left, Is.EqualTo(group.Rect.Left + span.X * group.Rect.Width).Within(0.000001));
                    Assert.That(actual.Rect.Width, Is.EqualTo(span.Width * group.Rect.Width).Within(0.000001));
                }
            if (total is 5 or 7)
            {
                GameplaySkinLayoutGroup group = result.GroupsInLogicalOrder[0];
                float unusedScratch = right ? group.Rect.Right - result.LanesInLogicalOrder.Max(l => l.Rect.Right)
                    : result.LanesInLogicalOrder.Min(l => l.Rect.Left) - group.Rect.Left;
                Assert.That(unusedScratch * 1920, Is.EqualTo(96).Within(0.001));
            }
            if (total > 10)
                Assert.That((result.GroupsInLogicalOrder[1].Rect.Left - result.GroupsInLogicalOrder[0].Rect.Right) * 1920, Is.EqualTo(130).Within(0.001));
        }

        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        public void TestAbsentOrUnmatchedAuthorProfileRetainsLegacyGeometryAndSpecialColumn(int count)
        {
            var skin = new TestSkinSource();
            skin.Set(LegacyManiaSkinConfigurationLookups.ColumnWidth, 47, 0);
            var topology = new ManiaGameplaySkinLaneTopologyRevisionOwner().Publish(createBeatmap(count));
            GameplaySkinLayoutSnapshot solveProfile(GameplaySkinAuthorLayout? profile) => ManiaGameplaySkinLayoutSolver.Solve(
                topology, skin, GameplaySkinLayoutRevisionOwner.CreateCompatibility().PackageRevision, 0,
                authoredEnvironment(), GameplaySkinScrollDirection.Down, profile);
            GameplaySkinLayoutSnapshot original = solveProfile(null), unmatched = solveProfile(authorLayout(new[] { count == 5 ? 7 : 5 }, false));
            Assert.Multiple(() =>
            {
                Assert.That(unmatched.LanesInLogicalOrder.Select(l => l.Rect), Is.EqualTo(original.LanesInLogicalOrder.Select(l => l.Rect)));
                Assert.That(unmatched.Surfaces.Select(s => s.Rect), Is.EqualTo(original.Surfaces.Select(s => s.Rect)));
                Assert.That(original.Context.PresentationStyleId, Is.EqualTo("mania-single"));
                Assert.That(original.Context.Topology.LanesInLogicalOrder.Count(l => l.Identity.Role == GameplaySkinLaneRole.SpecialKey), Is.EqualTo(count % 2));
                Assert.That(unmatched.Diagnostics.Any(d => d.Code == "mania.layout.author-topology-mismatch"), Is.True);
                Assert.That(original.Surfaces.Any(s => s.Id.StartsWith(ManiaGameplaySkinLayout.KEY_VISUAL_SURFACE_PREFIX, StringComparison.Ordinal)), Is.False);
            });
        }

        [TestCase(GameplaySkinScrollDirection.Down)]
        [TestCase(GameplaySkinScrollDirection.Up)]
        public void TestAuthorFieldShorterThanOnePixelKeepsTheHitLineInsideItsNativeStage(GameplaySkinScrollDirection direction)
        {
            GameplaySkinLayoutRect field = GameplaySkinLayoutRect.Create(0.1f, 0.1f, 0.3f, 0.0001f);
            var author = new GameplaySkinAuthorLayout("authored.short", "mania", "1k", "any",
                new[] { new GameplaySkinAuthorLayoutStage(field, new[] { new GameplaySkinAuthorLaneSpan(0, 1) }) });
            var topology = new ManiaGameplaySkinLaneTopologyRevisionOwner().Publish(createBeatmap(1));
            GameplaySkinLayoutSnapshot result = ManiaGameplaySkinLayoutSolver.Solve(topology, new TestSkinSource(),
                GameplaySkinLayoutRevisionOwner.CreateCompatibility().PackageRevision, 0, authoredEnvironment(), direction, author);
            GameplaySkinLayoutRect projectedField = result.GetSurface(ManiaGameplaySkinLayout.PLAYFIELD_SURFACE).Rect;
            GameplaySkinLayoutRect hit = result.GetSurface(ManiaGameplaySkinLayout.HIT_TARGET_SURFACE).Rect;
            Assert.Multiple(() =>
            {
                Assert.That(field.Height, Is.LessThan(1f / 1080));
                Assert.That(result.Context.Topology, Is.SameAs(topology.Publication.Topology));
                Assert.That(result.GroupsInLogicalOrder.Single().Rect, Is.EqualTo(field));
                Assert.That(projectedField.Height, Is.EqualTo(field.Height).Within(0.00000001f));
                Assert.That(hit.Height, Is.EqualTo(projectedField.Height));
                Assert.That(field.Contains(hit), Is.True);
                Assert.That(projectedField.Contains(hit), Is.True);
                Assert.That(result.Diagnostics, Is.Empty);
            });
        }

        private static ManiaGameplaySkinLayoutEnvironment authoredEnvironment()
            => new ManiaGameplaySkinLayoutEnvironment(fullBounds(), fullBounds(), 16f / 9f, 1, renderPixelWidth: 1920, renderPixelHeight: 1080);

        private static GameplaySkinAuthorLayout authorLayout(int[] counts, bool right)
        {
            var stages = new GameplaySkinAuthorLayoutStage[counts.Length];
            for (int group = 0; group < counts.Length; group++)
            {
                bool scratchRight = counts.Length == 2 ? group == 1 : right;
                double left = counts.Length == 2 ? group == 0 ? 455 : 1025 : right ? 1409 : 71;
                var all = new GameplaySkinAuthorLaneSpan[8];
                double x = 4;
                for (int i = 0; i < 8; i++)
                {
                    bool scratch = i == (scratchRight ? 7 : 0);
                    int normal = scratchRight ? i : i - 1;
                    double width = scratch ? 90 : normal % 2 == 0 ? 52 : 40;
                    all[i] = new GameplaySkinAuthorLaneSpan(x / 440, width / 440);
                    x += width + 2;
                }
                int count = counts[group];
                int start = scratchRight ? count == 5 || count == 6 ? 2 : 0 : count == 5 || count == 7 ? 1 : 0;
                GameplaySkinAuthorLaneSpan[] lanes = all.Skip(start).Take(count).ToArray();
                GameplaySkinAuthorLaneSpan[] keys = (GameplaySkinAuthorLaneSpan[])lanes.Clone();
                if (count is 6 or 8)
                {
                    int index = scratchRight ? count - 1 : 0;
                    double keyLeft = counts.Length == 2 ? group == 0 ? 399 : 1371 : scratchRight ? 1755 : 15;
                    keys[index] = new GameplaySkinAuthorLaneSpan((keyLeft - left) / 440, 150d / 440);
                }
                stages[group] = new GameplaySkinAuthorLayoutStage(GameplaySkinLayoutRect.Create((float)(left / 1920), 0, 440f / 1920, 728f / 1080), lanes, keys);
            }
            return new GameplaySkinAuthorLayout("authored.columns", "mania", string.Join("-", counts.Select(n => $"{n}k")), "any", stages, 120d / 1080, 110d / 1080);
        }
    }
}
