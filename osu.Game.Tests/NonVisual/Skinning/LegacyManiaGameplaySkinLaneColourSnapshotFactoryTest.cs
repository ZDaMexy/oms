// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.IO;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK.Graphics;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class LegacyManiaGameplaySkinLaneColourSnapshotFactoryTest
    {
        [Test]
        public void TestMissingAndExplicitEmptyBucketsRemainDistinct()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            Dictionary<GameplaySkinLaneId, int> mapping = createTwoColumnMapping(topology);

            GameplaySkinConfigurationDeclaration<GameplaySkinLaneColourSnapshot> missing =
                LegacyManiaGameplaySkinLaneColourSnapshotFactory.Create(
                    decode("[Mania]\nKeys: 4\n"), 2, topology, mapping);
            GameplaySkinConfigurationDeclaration<GameplaySkinLaneColourSnapshot> empty =
                LegacyManiaGameplaySkinLaneColourSnapshotFactory.Create(
                    decode("[Mania]\nKeys: 2\n"), 2, topology, mapping);

            Assert.Multiple(() =>
            {
                Assert.That(missing.IsDeclared, Is.False);
                Assert.That(empty.IsDeclared, Is.True);
                Assert.That(empty.Value.Declarations, Is.Empty);
            });
        }

        [Test]
        public void TestCanonicalOneBasedTokensMapThroughExplicitZeroBasedColumns()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneId firstLane = topology.LanesInLogicalOrder[0].Identity.Id;
            GameplaySkinLaneId secondLane = topology.LanesInLogicalOrder[1].Identity.Id;
            var reversedSourceMapping = new Dictionary<GameplaySkinLaneId, int>
            {
                [firstLane] = 1,
                [secondLane] = 0,
            };
            GameplaySkinLaneColourSnapshot snapshot = LegacyManiaGameplaySkinLaneColourSnapshotFactory.Create(
                decode(
                    "[Mania]\n" +
                    "Keys: 2\n" +
                    "Colour1: 1,2,3\n" +
                    "ColourLight2: 4,5,6,0\n"),
                2,
                topology,
                reversedSourceMapping).Value;

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.GetDeclaration(secondLane, GameplaySkinLaneColourFieldCatalog.LaneBackground).Value,
                    Is.EqualTo(new Color4(1, 2, 3, 255)));
                Assert.That(snapshot.GetDeclaration(firstLane, GameplaySkinLaneColourFieldCatalog.LaneLight).Value,
                    Is.EqualTo(new Color4(4, 5, 6, 0)));
                Assert.That(snapshot.GetDeclaration(firstLane, GameplaySkinLaneColourFieldCatalog.LaneBackground).IsDeclared, Is.False);
                Assert.That(snapshot.GetDeclaration(secondLane, GameplaySkinLaneColourFieldCatalog.LaneLight).IsDeclared, Is.False);
                Assert.That(snapshot.Topology, Is.SameAs(topology));
            });
        }

        [Test]
        public void TestOnlyExactCanonicalTokensEnterClosedSidecar()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            IReadOnlyList<LegacyManiaSkinConfiguration> decoded = decode(
                "[Mania]\n" +
                "Keys: 2\n" +
                "Colour0: 1,2,3\n" +
                "Colour3: 1,2,3\n" +
                "Colour01: 1,2,3\n" +
                "Colour+1: 1,2,3\n" +
                "Colour-1: 1,2,3\n" +
                "Colour1x: 1,2,3\n" +
                "ColourLight0: 1,2,3\n" +
                "ColourLight3: 1,2,3\n" +
                "ColourLight01: 1,2,3\n" +
                "ColourLight+1: 1,2,3\n" +
                "ColourLight1x: 1,2,3\n" +
                "ColourColumnLine: 1,2,3\n" +
                "ColourPrivateToken: 1,2,3\n" +
                "colour1: 1,2,3\n");
            GameplaySkinLaneColourSnapshot snapshot = LegacyManiaGameplaySkinLaneColourSnapshotFactory.Create(
                decoded, 2, topology, createTwoColumnMapping(topology)).Value;

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Declarations, Is.Empty);
                Assert.That(decoded[0].CustomColours.Keys, Does.Contain("Colour0")
                    .And.Contain("Colour3")
                    .And.Contain("Colour01")
                    .And.Contain("Colour+1")
                    .And.Contain("Colour1x")
                    .And.Contain("ColourPrivateToken"));
                Assert.That(decoded[0].CustomColours.Keys, Does.Not.Contain("colour1"));
            });
        }

        [Test]
        public void TestColourBeforeKeysIsAttributedToAcceptedBucket()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneColourSnapshot snapshot = LegacyManiaGameplaySkinLaneColourSnapshotFactory.Create(
                decode("[Mania]\nColour2: 9,8,7,6\nKeys: 2\n"),
                2,
                topology,
                createTwoColumnMapping(topology)).Value;

            Assert.That(snapshot.GetDeclaration(
                topology.LanesInLogicalOrder[1].Identity.Id,
                GameplaySkinLaneColourFieldCatalog.LaneBackground).Value, Is.EqualTo(new Color4(9, 8, 7, 6)));
        }

        [Test]
        public void TestValidDuplicateUsesLastAcceptedAndMalformedDoesNotOverwrite()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneColourSnapshot validDuplicate = createSnapshot(
                topology,
                "[Mania]\nKeys: 2\nColour1: 1,2,3\nColour1: 4,5,6,7\n");
            GameplaySkinLaneColourSnapshot malformedDuplicate = createSnapshot(
                topology,
                "[Mania]\nKeys: 2\nColourLight1: 8,9,10,11\nColourLight1: 256,9,10,11\n");
            GameplaySkinLaneColourSnapshot onlyMalformed = createSnapshot(
                topology,
                "[Mania]\nKeys: 2\nColour2: 1,2\n");

            Assert.Multiple(() =>
            {
                Assert.That(validDuplicate.GetDeclaration(
                    topology.LanesInLogicalOrder[0].Identity.Id,
                    GameplaySkinLaneColourFieldCatalog.LaneBackground).Value, Is.EqualTo(new Color4(4, 5, 6, 7)));
                Assert.That(malformedDuplicate.GetDeclaration(
                    topology.LanesInLogicalOrder[0].Identity.Id,
                    GameplaySkinLaneColourFieldCatalog.LaneLight).Value, Is.EqualTo(new Color4(8, 9, 10, 11)));
                Assert.That(onlyMalformed.GetDeclaration(
                    topology.LanesInLogicalOrder[1].Identity.Id,
                    GameplaySkinLaneColourFieldCatalog.LaneBackground).IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestDiscardedDuplicateBucketDoesNotPolluteAcceptedBucket()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            IReadOnlyList<LegacyManiaSkinConfiguration> decoded = decode(
                "[Mania]\n" +
                "Keys: 2\n" +
                "Colour1: 1,2,3\n" +
                "Keys: 2\n" +
                "Colour2: 4,5,6\n");
            GameplaySkinLaneColourSnapshot snapshot = LegacyManiaGameplaySkinLaneColourSnapshotFactory.Create(
                decoded, 2, topology, createTwoColumnMapping(topology)).Value;

            Assert.Multiple(() =>
            {
                Assert.That(decoded, Has.Count.EqualTo(1));
                Assert.That(snapshot.GetDeclaration(
                    topology.LanesInLogicalOrder[0].Identity.Id,
                    GameplaySkinLaneColourFieldCatalog.LaneBackground).Value, Is.EqualTo(new Color4(1, 2, 3, 255)));
                Assert.That(snapshot.GetDeclaration(
                    topology.LanesInLogicalOrder[1].Identity.Id,
                    GameplaySkinLaneColourFieldCatalog.LaneBackground).IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestCompatibilityDictionaryMutationCannotForgeEraseOrAlterProvenance()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            IReadOnlyList<LegacyManiaSkinConfiguration> decoded = decode(
                "[Mania]\nKeys: 2\nColour1: 1,2,3,4\nColourLight2: 5,6,7,8\n");

            decoded[0].CustomColours["Colour1"] = Color4.Red;
            decoded[0].CustomColours["Colour2"] = Color4.Green;
            decoded[0].CustomColours.Remove("ColourLight2");
            GameplaySkinLaneColourSnapshot snapshot = LegacyManiaGameplaySkinLaneColourSnapshotFactory.Create(
                decoded, 2, topology, createTwoColumnMapping(topology)).Value;
            decoded[0].CustomColours.Clear();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.GetDeclaration(
                    topology.LanesInLogicalOrder[0].Identity.Id,
                    GameplaySkinLaneColourFieldCatalog.LaneBackground).Value, Is.EqualTo(new Color4(1, 2, 3, 4)));
                Assert.That(snapshot.GetDeclaration(
                    topology.LanesInLogicalOrder[1].Identity.Id,
                    GameplaySkinLaneColourFieldCatalog.LaneLight).Value, Is.EqualTo(new Color4(5, 6, 7, 8)));
                Assert.That(snapshot.GetDeclaration(
                    topology.LanesInLogicalOrder[1].Identity.Id,
                    GameplaySkinLaneColourFieldCatalog.LaneBackground).IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestPartialAndManyToOneMappingsAreSupportedAndCopied()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneId firstLane = topology.LanesInLogicalOrder[0].Identity.Id;
            GameplaySkinLaneId secondLane = topology.LanesInLogicalOrder[1].Identity.Id;
            IReadOnlyList<LegacyManiaSkinConfiguration> decoded = decode("[Mania]\nKeys: 2\nColour1: 10,20,30\n");
            var partialMapping = new Dictionary<GameplaySkinLaneId, int> { [firstLane] = 0 };
            GameplaySkinLaneColourSnapshot partial = LegacyManiaGameplaySkinLaneColourSnapshotFactory.Create(
                decoded, 2, topology, partialMapping).Value;
            var sharedSourceMapping = new Dictionary<GameplaySkinLaneId, int>
            {
                [firstLane] = 0,
                [secondLane] = 0,
            };
            GameplaySkinLaneColourSnapshot sharedSource = LegacyManiaGameplaySkinLaneColourSnapshotFactory.Create(
                decoded, 2, topology, sharedSourceMapping).Value;

            partialMapping[firstLane] = 1;
            sharedSourceMapping[firstLane] = 1;

            Assert.Multiple(() =>
            {
                Assert.That(partial.GetDeclaration(firstLane, GameplaySkinLaneColourFieldCatalog.LaneBackground).IsDeclared, Is.True);
                Assert.That(partial.GetDeclaration(secondLane, GameplaySkinLaneColourFieldCatalog.LaneBackground).IsDeclared, Is.False);
                Assert.That(sharedSource.GetDeclaration(firstLane, GameplaySkinLaneColourFieldCatalog.LaneBackground).Value,
                    Is.EqualTo(new Color4(10, 20, 30, 255)));
                Assert.That(sharedSource.GetDeclaration(secondLane, GameplaySkinLaneColourFieldCatalog.LaneBackground).Value,
                    Is.EqualTo(new Color4(10, 20, 30, 255)));
            });
        }

        [Test]
        public void TestFactoryRejectsAmbiguousOrInvalidInputs()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            Dictionary<GameplaySkinLaneId, int> mapping = createTwoColumnMapping(topology);
            var duplicate = new[] { new LegacyManiaSkinConfiguration(2), new LegacyManiaSkinConfiguration(2) };
            LegacyManiaSkinConfiguration[] containsNull = { null! };
            var outsideMapping = new Dictionary<GameplaySkinLaneId, int>
            {
                [GameplaySkinLaneId.Create("test.lane.outside")] = 0,
            };
            var outOfRangeMapping = new Dictionary<GameplaySkinLaneId, int>
            {
                [topology.LanesInLogicalOrder[0].Identity.Id] = 2,
            };

            Assert.Multiple(() =>
            {
                Assert.That(() => LegacyManiaGameplaySkinLaneColourSnapshotFactory.Create(null!, 2, topology, mapping),
                    Throws.ArgumentNullException);
                Assert.That(() => LegacyManiaGameplaySkinLaneColourSnapshotFactory.Create(
                    Array.Empty<LegacyManiaSkinConfiguration>(), 2, null!, mapping), Throws.ArgumentNullException);
                Assert.That(() => LegacyManiaGameplaySkinLaneColourSnapshotFactory.Create(
                    Array.Empty<LegacyManiaSkinConfiguration>(), 2, topology, null!), Throws.ArgumentNullException);
                Assert.That(() => LegacyManiaGameplaySkinLaneColourSnapshotFactory.Create(
                    Array.Empty<LegacyManiaSkinConfiguration>(), 0, topology, mapping), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => LegacyManiaGameplaySkinLaneColourSnapshotFactory.Create(containsNull, 2, topology, mapping),
                    Throws.ArgumentException);
                Assert.That(() => LegacyManiaGameplaySkinLaneColourSnapshotFactory.Create(duplicate, 2, topology, mapping),
                    Throws.ArgumentException);
                Assert.That(() => LegacyManiaGameplaySkinLaneColourSnapshotFactory.Create(
                    Array.Empty<LegacyManiaSkinConfiguration>(), 2, topology, outsideMapping), Throws.ArgumentException);
                Assert.That(() => LegacyManiaGameplaySkinLaneColourSnapshotFactory.Create(
                    Array.Empty<LegacyManiaSkinConfiguration>(), 2, topology, outOfRangeMapping), Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void TestSidecarRejectsInvalidFieldOrIndexAndReturnsDefensiveCopy()
        {
            var configuration = new LegacyManiaSkinConfiguration(2);
            configuration.AcceptPerColumnColour(LegacyManiaSkinPerColumnColourField.ColumnBackground, 0, Color4.Red);
            GameplaySkinConfigurationDeclaration<Color4>[] copied =
                configuration.CopyAcceptedPerColumnColourDeclarations(LegacyManiaSkinPerColumnColourField.ColumnBackground);
            copied[0] = GameplaySkinConfigurationDeclaration<Color4>.Absent;

            Assert.Multiple(() =>
            {
                Assert.That(configuration.CopyAcceptedPerColumnColourDeclarations(
                    LegacyManiaSkinPerColumnColourField.ColumnBackground)[0].Value, Is.EqualTo(Color4.Red));
                Assert.That(() => configuration.AcceptPerColumnColour(0, 0, Color4.Red), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => configuration.AcceptPerColumnColour(
                    LegacyManiaSkinPerColumnColourField.ColumnBackground | LegacyManiaSkinPerColumnColourField.ColumnLight,
                    0,
                    Color4.Red), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => configuration.AcceptPerColumnColour(
                    LegacyManiaSkinPerColumnColourField.ColumnBackground, -1, Color4.Red), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => configuration.AcceptPerColumnColour(
                    LegacyManiaSkinPerColumnColourField.ColumnLight, 2, Color4.Red), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => configuration.CopyAcceptedPerColumnColourDeclarations(0), Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void TestFactoryAndSnapshotSurfaceDoNotExposeRawColourKeysOrMutableCompatibilityDictionary()
        {
            string[] snapshotPropertyNames = typeof(GameplaySkinLaneColourSnapshot).GetProperties().Select(property => property.Name).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(typeof(LegacyManiaGameplaySkinLaneColourSnapshotFactory).IsAbstract
                            && typeof(LegacyManiaGameplaySkinLaneColourSnapshotFactory).IsSealed, Is.True);
                Assert.That(snapshotPropertyNames, Is.EquivalentTo(new[]
                {
                    nameof(GameplaySkinLaneColourSnapshot.Topology),
                    nameof(GameplaySkinLaneColourSnapshot.Declarations),
                }));
                Assert.That(typeof(GameplaySkinLaneColourSnapshot).GetProperties().Select(property => property.PropertyType),
                    Has.None.EqualTo(typeof(Dictionary<string, Color4>)));
                Assert.That(typeof(GameplaySkinLaneColourDeclaration).GetProperties().Select(property => property.PropertyType),
                    Has.None.EqualTo(typeof(string)));
            });
        }

        private static GameplaySkinLaneColourSnapshot createSnapshot(GameplaySkinLaneTopologySnapshot topology, string skinIni)
            => LegacyManiaGameplaySkinLaneColourSnapshotFactory.Create(
                decode(skinIni), 2, topology, createTwoColumnMapping(topology)).Value;

        private static IReadOnlyList<LegacyManiaSkinConfiguration> decode(string skinIni)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(skinIni));
            using var reader = new LineBufferedReader(stream);
            return new LegacyManiaSkinDecoder().Decode(reader);
        }

        private static Dictionary<GameplaySkinLaneId, int> createTwoColumnMapping(GameplaySkinLaneTopologySnapshot topology)
            => topology.LanesInLogicalOrder.ToDictionary(lane => lane.Identity.Id, lane => lane.GlobalLogicalIndex);

        private static GameplaySkinLaneTopologySnapshot createTopology()
        {
            GameplaySkinLaneGroupIdentity group = GameplaySkinLaneGroupIdentity.Create(
                GameplaySkinLaneGroupId.Create("test.group.main"), GameplaySkinLaneSide.Neutral);
            var lanes = new[]
            {
                GameplaySkinLaneTopologyEntry.Create(
                    GameplaySkinLaneIdentity.Create(GameplaySkinLaneId.Create("test.lane.first"), group, GameplaySkinLaneRole.Key),
                    0, 0, 1, 1),
                GameplaySkinLaneTopologyEntry.Create(
                    GameplaySkinLaneIdentity.Create(GameplaySkinLaneId.Create("test.lane.second"), group, GameplaySkinLaneRole.Key),
                    1, 1, 0, 0),
            };

            return GameplaySkinLaneTopologySnapshot.Create(new[]
            {
                GameplaySkinLaneTopologyGroup.Create(group, 0, 0, lanes),
            });
        }
    }
}
