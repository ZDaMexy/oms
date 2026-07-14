// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public sealed class BmsGameplaySkinLaneTopologyFactoryTest
    {
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.P1, GameplaySkinLaneSide.Primary, 0)]
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.Center, GameplaySkinLaneSide.Primary, 0)]
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.P2, GameplaySkinLaneSide.Secondary, 5)]
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.CenterRightScratch, GameplaySkinLaneSide.Secondary, 5)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P1, GameplaySkinLaneSide.Primary, 0)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.Center, GameplaySkinLaneSide.Primary, 0)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P2, GameplaySkinLaneSide.Secondary, 7)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.CenterRightScratch, GameplaySkinLaneSide.Secondary, 7)]
        public void TestProjectsSinglePlayExactOrder(
            BmsKeymode keymode,
            BmsPlayfieldStyle style,
            GameplaySkinLaneSide expectedSide,
            int expectedScratchVisualIndex)
        {
            BmsLaneLayout layout = BmsLaneLayout.CreateForKeymode(keymode, style: style);
            BmsGameplaySkinLaneTopologyProjection projection = BmsGameplaySkinLaneTopologyFactory.Create(layout);
            GameplaySkinLaneTopologySnapshot topology = projection.Topology;
            int laneCount = BmsRuleset.GetLaneCount(keymode);
            string[] expectedLogicalOrder = new[] { "bms.lane.scratch-1" }
                                            .Concat(Enumerable.Range(1, laneCount - 1).Select(index => $"bms.lane.key-{index}"))
                                            .ToArray();
            string[] expectedVisualOrder = expectedScratchVisualIndex == 0
                ? expectedLogicalOrder
                : expectedLogicalOrder.Skip(1).Append(expectedLogicalOrder[0]).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(projection.Keymode, Is.EqualTo(keymode));
                Assert.That(projection.AppliedStyle, Is.EqualTo(style));
                Assert.That(topology.GroupsInLogicalOrder, Has.Count.EqualTo(1));
                Assert.That(topology.GroupsInLogicalOrder[0].Identity.Id.Value, Is.EqualTo("bms.group.deck-1"));
                Assert.That(topology.GroupsInLogicalOrder[0].Identity.Side, Is.EqualTo(expectedSide));
                Assert.That(topology.LanesInLogicalOrder.Select(lane => lane.Identity.Id.Value), Is.EqualTo(expectedLogicalOrder));
                Assert.That(topology.LanesInVisualOrder.Select(lane => lane.Identity.Id.Value), Is.EqualTo(expectedVisualOrder));
                Assert.That(topology.LanesInLogicalOrder[0].Identity.Role, Is.EqualTo(GameplaySkinLaneRole.Scratch));
                Assert.That(topology.LanesInLogicalOrder.Skip(1).Select(lane => lane.Identity.Role), Is.All.EqualTo(GameplaySkinLaneRole.Key));
                Assert.That(topology.LanesInLogicalOrder[0].GlobalVisualIndex, Is.EqualTo(expectedScratchVisualIndex));
                Assert.That(topology.LanesInLogicalOrder.Select(lane => lane.GlobalLogicalIndex), Is.EqualTo(Enumerable.Range(0, laneCount)));
                Assert.That(topology.LanesInLogicalOrder.Select(lane => lane.GroupLocalLogicalIndex), Is.EqualTo(Enumerable.Range(0, laneCount)));
                Assert.That(topology.LanesInLogicalOrder.Select(lane => lane.GlobalVisualIndex),
                    Is.EqualTo(layout.Lanes.Select(lane => lane.VisualIndex)));
                Assert.That(topology.LanesInLogicalOrder.Select(lane => lane.GroupLocalVisualIndex),
                    Is.EqualTo(layout.Lanes.Select(lane => lane.VisualIndex)));
            });
        }

        [TestCase(BmsKeymode.Key5K)]
        [TestCase(BmsKeymode.Key7K)]
        public void TestPresentationStyleDoesNotChangeStableLaneOrLogicalIdentity(BmsKeymode keymode)
        {
            GameplaySkinLaneTopologySnapshot primary = BmsGameplaySkinLaneTopologyFactory.Create(
                BmsLaneLayout.CreateForKeymode(keymode, style: BmsPlayfieldStyle.P1)).Topology;
            GameplaySkinLaneTopologySnapshot secondary = BmsGameplaySkinLaneTopologyFactory.Create(
                BmsLaneLayout.CreateForKeymode(keymode, style: BmsPlayfieldStyle.P2)).Topology;

            Assert.Multiple(() =>
            {
                Assert.That(primary.GroupsInLogicalOrder[0].Identity.Id, Is.EqualTo(secondary.GroupsInLogicalOrder[0].Identity.Id));
                Assert.That(primary.GroupsInLogicalOrder[0].Identity.Side, Is.EqualTo(GameplaySkinLaneSide.Primary));
                Assert.That(secondary.GroupsInLogicalOrder[0].Identity.Side, Is.EqualTo(GameplaySkinLaneSide.Secondary));
                Assert.That(primary.LanesInLogicalOrder.Select(lane => lane.Identity.Id),
                    Is.EqualTo(secondary.LanesInLogicalOrder.Select(lane => lane.Identity.Id)));
                Assert.That(primary.LanesInLogicalOrder.Select(lane => lane.Identity.Role),
                    Is.EqualTo(secondary.LanesInLogicalOrder.Select(lane => lane.Identity.Role)));
                Assert.That(primary.LanesInLogicalOrder.Select(lane => lane.GlobalLogicalIndex),
                    Is.EqualTo(secondary.LanesInLogicalOrder.Select(lane => lane.GlobalLogicalIndex)));
                Assert.That(primary.LanesInLogicalOrder.Select(lane => lane.GroupLocalLogicalIndex),
                    Is.EqualTo(secondary.LanesInLogicalOrder.Select(lane => lane.GroupLocalLogicalIndex)));
                Assert.That(primary.LanesInLogicalOrder.Select(lane => lane.GlobalVisualIndex),
                    Is.Not.EqualTo(secondary.LanesInLogicalOrder.Select(lane => lane.GlobalVisualIndex)));
            });
        }

        [TestCase(BmsKeymode.Key9K_Bms, BmsPlayfieldStyle.P2)]
        [TestCase(BmsKeymode.Key9K_Pms, BmsPlayfieldStyle.CenterRightScratch)]
        public void TestProjectsNineKeyContextWithoutInventingScratch(BmsKeymode keymode, BmsPlayfieldStyle requestedStyle)
        {
            BmsGameplaySkinLaneTopologyProjection projection = BmsGameplaySkinLaneTopologyFactory.Create(
                BmsLaneLayout.CreateForKeymode(keymode, style: requestedStyle));
            GameplaySkinLaneTopologySnapshot topology = projection.Topology;

            Assert.Multiple(() =>
            {
                Assert.That(projection.Keymode, Is.EqualTo(keymode));
                Assert.That(projection.AppliedStyle, Is.EqualTo(BmsPlayfieldStyle.Center));
                Assert.That(topology.GroupsInLogicalOrder, Has.Count.EqualTo(1));
                Assert.That(topology.GroupsInLogicalOrder[0].Identity.Side, Is.EqualTo(GameplaySkinLaneSide.Neutral));
                Assert.That(topology.LanesInLogicalOrder.Select(lane => lane.Identity.Id.Value),
                    Is.EqualTo(Enumerable.Range(1, 9).Select(index => $"bms.lane.key-{index}")));
                Assert.That(topology.LanesInLogicalOrder.Select(lane => lane.Identity.Role), Is.All.EqualTo(GameplaySkinLaneRole.Key));
                Assert.That(topology.LanesInLogicalOrder.Select(lane => lane.GlobalVisualIndex), Is.EqualTo(Enumerable.Range(0, 9)));
            });
        }

        [Test]
        public void TestNineKeyBmsAndPmsKeepDistinctNativeContextWithSameNeutralOrder()
        {
            BmsGameplaySkinLaneTopologyProjection bms = BmsGameplaySkinLaneTopologyFactory.Create(
                BmsLaneLayout.CreateForKeymode(BmsKeymode.Key9K_Bms));
            BmsGameplaySkinLaneTopologyProjection pms = BmsGameplaySkinLaneTopologyFactory.Create(
                BmsLaneLayout.CreateForKeymode(BmsKeymode.Key9K_Pms));

            Assert.Multiple(() =>
            {
                Assert.That(bms.Keymode, Is.EqualTo(BmsKeymode.Key9K_Bms));
                Assert.That(pms.Keymode, Is.EqualTo(BmsKeymode.Key9K_Pms));
                Assert.That(bms.Topology.LanesInLogicalOrder.Select(lane => lane.Identity.Id),
                    Is.EqualTo(pms.Topology.LanesInLogicalOrder.Select(lane => lane.Identity.Id)));
                Assert.That(bms.Topology.LanesInLogicalOrder.Select(lane => lane.Identity.Role),
                    Is.EqualTo(pms.Topology.LanesInLogicalOrder.Select(lane => lane.Identity.Role)));
                Assert.That(bms.Topology.LanesInLogicalOrder.Select(lane => lane.GlobalVisualIndex),
                    Is.EqualTo(pms.Topology.LanesInLogicalOrder.Select(lane => lane.GlobalVisualIndex)));
            });
        }

        [TestCase(BmsPlayfieldStyle.P1)]
        [TestCase(BmsPlayfieldStyle.P2)]
        [TestCase(BmsPlayfieldStyle.Center)]
        [TestCase(BmsPlayfieldStyle.CenterRightScratch)]
        public void TestProjectsFourteenKeyDualDeckAndScratchTwo(BmsPlayfieldStyle requestedStyle)
        {
            BmsGameplaySkinLaneTopologyProjection projection = BmsGameplaySkinLaneTopologyFactory.Create(
                BmsLaneLayout.CreateForKeymode(BmsKeymode.Key14K, style: requestedStyle));
            GameplaySkinLaneTopologySnapshot topology = projection.Topology;
            GameplaySkinLaneTopologyGroup primary = topology.GroupsInLogicalOrder[0];
            GameplaySkinLaneTopologyGroup secondary = topology.GroupsInLogicalOrder[1];
            string[] expectedLaneIds = new[] { "bms.lane.scratch-1" }
                                       .Concat(Enumerable.Range(1, 14).Select(index => $"bms.lane.key-{index}"))
                                       .Append("bms.lane.scratch-2")
                                       .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(projection.Keymode, Is.EqualTo(BmsKeymode.Key14K));
                Assert.That(projection.AppliedStyle, Is.EqualTo(BmsPlayfieldStyle.Center));
                Assert.That(topology.GroupsInLogicalOrder.Select(group => group.Identity.Id.Value),
                    Is.EqualTo(new[] { "bms.group.deck-1", "bms.group.deck-2" }));
                Assert.That(topology.GroupsInLogicalOrder.Select(group => group.Identity.Side),
                    Is.EqualTo(new[] { GameplaySkinLaneSide.Primary, GameplaySkinLaneSide.Secondary }));
                Assert.That(primary.LanesInLogicalOrder, Has.Count.EqualTo(8));
                Assert.That(secondary.LanesInLogicalOrder, Has.Count.EqualTo(8));
                Assert.That(topology.LanesInLogicalOrder.Select(lane => lane.Identity.Id.Value), Is.EqualTo(expectedLaneIds));
                Assert.That(topology.LanesInVisualOrder.Select(lane => lane.Identity.Id.Value), Is.EqualTo(expectedLaneIds));
                Assert.That(primary.LanesInLogicalOrder.Select(lane => lane.Identity.Id.Value), Is.EqualTo(expectedLaneIds.Take(8)));
                Assert.That(secondary.LanesInLogicalOrder.Select(lane => lane.Identity.Id.Value), Is.EqualTo(expectedLaneIds.Skip(8)));
                Assert.That(primary.LanesInLogicalOrder[0].Identity.Id.Value, Is.EqualTo("bms.lane.scratch-1"));
                Assert.That(primary.LanesInLogicalOrder[0].Identity.Role, Is.EqualTo(GameplaySkinLaneRole.Scratch));
                Assert.That(primary.LanesInLogicalOrder[0].GlobalLogicalIndex, Is.Zero);
                Assert.That(primary.LanesInLogicalOrder[0].GroupLocalLogicalIndex, Is.Zero);
                Assert.That(secondary.LanesInLogicalOrder[0].Identity.Id.Value, Is.EqualTo("bms.lane.key-8"));
                Assert.That(secondary.LanesInLogicalOrder[0].GlobalLogicalIndex, Is.EqualTo(8));
                Assert.That(secondary.LanesInLogicalOrder[0].GroupLocalLogicalIndex, Is.Zero);
                Assert.That(secondary.LanesInLogicalOrder[7].Identity.Id.Value, Is.EqualTo("bms.lane.scratch-2"));
                Assert.That(secondary.LanesInLogicalOrder[7].Identity.Role, Is.EqualTo(GameplaySkinLaneRole.Scratch));
                Assert.That(secondary.LanesInLogicalOrder[7].GlobalLogicalIndex, Is.EqualTo(15));
                Assert.That(secondary.LanesInLogicalOrder[7].GroupLocalLogicalIndex, Is.EqualTo(7));
                Assert.That(topology.LanesInLogicalOrder.Where(lane => lane.Identity.Role == GameplaySkinLaneRole.Scratch)
                                    .Select(lane => lane.GlobalLogicalIndex), Is.EqualTo(new[] { 0, 15 }));
                Assert.That(topology.LanesInLogicalOrder.Select(lane => lane.Identity.Role),
                    Is.EqualTo(new[] { GameplaySkinLaneRole.Scratch }
                               .Concat(Enumerable.Repeat(GameplaySkinLaneRole.Key, 14))
                               .Append(GameplaySkinLaneRole.Scratch)));
                Assert.That(topology.LanesInLogicalOrder.Select(lane => lane.GlobalLogicalIndex), Is.EqualTo(Enumerable.Range(0, 16)));
                Assert.That(topology.LanesInLogicalOrder.Select(lane => lane.GlobalVisualIndex), Is.EqualTo(Enumerable.Range(0, 16)));
                Assert.That(primary.LanesInLogicalOrder.Select(lane => lane.GroupLocalLogicalIndex), Is.EqualTo(Enumerable.Range(0, 8)));
                Assert.That(secondary.LanesInLogicalOrder.Select(lane => lane.GroupLocalLogicalIndex), Is.EqualTo(Enumerable.Range(0, 8)));
                Assert.That(primary.LanesInLogicalOrder.Select(lane => lane.GroupLocalVisualIndex), Is.EqualTo(Enumerable.Range(0, 8)));
                Assert.That(secondary.LanesInLogicalOrder.Select(lane => lane.GroupLocalVisualIndex), Is.EqualTo(Enumerable.Range(0, 8)));
            });
        }

        [Test]
        public void TestRejectsNullUnknownContextAndNonCanonicalLaneCount()
        {
            Assert.Multiple(() =>
            {
                Assert.That(() => BmsGameplaySkinLaneTopologyFactory.Create(null!), Throws.ArgumentNullException);
                Assert.That(() => BmsGameplaySkinLaneTopologyFactory.Create(
                    BmsLaneLayout.CreateForKeymode(BmsKeymode.Key7K, style: (BmsPlayfieldStyle)99)), Throws.ArgumentException);
                Assert.That(() => BmsGameplaySkinLaneTopologyFactory.Create(
                    BmsLaneLayout.CreateForKeymode((BmsKeymode)99)), Throws.ArgumentException);
                Assert.That(() => BmsGameplaySkinLaneTopologyFactory.Create(
                    BmsLaneLayout.CreateForKeymode(BmsKeymode.Key7K, minimumLaneCount: 9)), Throws.ArgumentException);
                Assert.That(() => BmsGameplaySkinLaneTopologyFactory.Create(
                    BmsLaneLayout.CreateForKeymode(BmsKeymode.Key7K, scratchLaneIndices: new[] { 7 }.ToHashSet())), Throws.ArgumentException);
                Assert.That(() => BmsGameplaySkinLaneTopologyFactory.Create(
                    BmsLaneLayout.CreateForKeymode(BmsKeymode.Key9K_Bms, scratchLaneIndices: new[] { 0 }.ToHashSet())), Throws.ArgumentException);
            });
        }

        [Test]
        public void TestFactoryAndNativeProjectionRemainInternal()
        {
            Assert.Multiple(() =>
            {
                Assert.That(typeof(BmsGameplaySkinLaneTopologyFactory).IsNotPublic, Is.True);
                Assert.That(typeof(BmsGameplaySkinLaneTopologyProjection).IsNotPublic, Is.True);
                Assert.That(typeof(BmsGameplaySkinLaneTopologyProjection).GetProperties().Select(property => property.Name),
                    Is.EquivalentTo(new[] { "Keymode", "AppliedStyle", "Topology" }));
                Assert.That(() => new BmsGameplaySkinLaneTopologyProjection(
                    BmsKeymode.Key7K, BmsPlayfieldStyle.Center, null!), Throws.ArgumentNullException);
            });
        }
    }
}
