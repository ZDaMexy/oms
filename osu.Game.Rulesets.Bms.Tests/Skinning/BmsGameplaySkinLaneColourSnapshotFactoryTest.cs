// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.IO;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    [TestFixture]
    public sealed class BmsGameplaySkinLaneColourSnapshotFactoryTest
    {
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.P1, 0)]
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.P2, 5)]
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.Center, 0)]
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.CenterRightScratch, 5)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P1, 0)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P2, 7)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.Center, 0)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.CenterRightScratch, 7)]
        public void TestSinglePlayFullAndKeyOnlyMappingsFollowResolvedVisualStyle(
            BmsKeymode keymode,
            BmsPlayfieldStyle style,
            int scratchVisualColumn)
        {
            int fullColumns = BmsRuleset.GetLaneCount(keymode);
            int keyOnlyColumns = fullColumns - 1;
            IReadOnlyList<LegacyManiaSkinConfiguration> decoded = decode(
                createColourBucket(fullColumns, 10) + createColourBucket(keyOnlyColumns, 20));
            BmsLaneLayout layout = BmsLaneLayout.CreateForKeymode(keymode, style: style);
            GameplaySkinLaneColourSnapshot full =
                BmsGameplaySkinLaneColourSnapshotFactory.CreateFullVisual(layout, decoded).Value;
            GameplaySkinLaneColourSnapshot keyOnly =
                BmsGameplaySkinLaneColourSnapshotFactory.CreateKeyOnly(layout, decoded).Value;
            GameplaySkinLaneId scratch = GameplaySkinLaneId.Create("bms.lane.scratch-1");
            GameplaySkinLaneId key1 = GameplaySkinLaneId.Create("bms.lane.key-1");
            int key1FullColumn = scratchVisualColumn == 0 ? 1 : 0;

            Assert.Multiple(() =>
            {
                Assert.That(colour(full, scratch).Value, Is.EqualTo(sourceColour(10, scratchVisualColumn)));
                Assert.That(colour(full, key1).Value, Is.EqualTo(sourceColour(10, key1FullColumn)));
                Assert.That(lightColour(full, key1).Value, Is.EqualTo(sourceLightColour(10, key1FullColumn)));
                Assert.That(colour(keyOnly, key1).Value, Is.EqualTo(sourceColour(20, 0)));
                Assert.That(colour(keyOnly, scratch).IsDeclared, Is.False);
                Assert.That(full.Topology.TryGetLane(scratch, out GameplaySkinLaneTopologyEntry? scratchLane), Is.True);
                Assert.That(scratchLane!.GlobalVisualIndex, Is.EqualTo(scratchVisualColumn));
            });
        }

        [TestCase(BmsKeymode.Key9K_Bms)]
        [TestCase(BmsKeymode.Key9K_Pms)]
        public void TestNineKeyUsesFullVisualMappingOnly(BmsKeymode keymode)
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> decoded = decode(createColourBucket(9, 30));
            BmsLaneLayout layout = BmsLaneLayout.CreateForKeymode(keymode, style: BmsPlayfieldStyle.Center);
            GameplaySkinLaneColourSnapshot full =
                BmsGameplaySkinLaneColourSnapshotFactory.CreateFullVisual(layout, decoded).Value;

            Assert.Multiple(() =>
            {
                Assert.That(colour(full, GameplaySkinLaneId.Create("bms.lane.key-1")).Value, Is.EqualTo(sourceColour(30, 0)));
                Assert.That(colour(full, GameplaySkinLaneId.Create("bms.lane.key-9")).Value, Is.EqualTo(sourceColour(30, 8)));
                Assert.That(() => BmsGameplaySkinLaneColourSnapshotFactory.CreateKeyOnly(layout, decoded), Throws.ArgumentException);
                Assert.That(() => BmsGameplaySkinLaneColourSnapshotFactory.CreateEightColumnDeck(layout, decoded), Throws.ArgumentException);
            });
        }

        [Test]
        public void TestFourteenKeyFullDeckAndKeyOnlyMappings()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> decoded = decode(
                createColourBucket(16, 40) + createColourBucket(8, 50) + createColourBucket(14, 60));
            BmsLaneLayout layout = BmsLaneLayout.CreateForKeymode(BmsKeymode.Key14K, style: BmsPlayfieldStyle.Center);
            GameplaySkinLaneColourSnapshot full =
                BmsGameplaySkinLaneColourSnapshotFactory.CreateFullVisual(layout, decoded).Value;
            GameplaySkinLaneColourSnapshot deck =
                BmsGameplaySkinLaneColourSnapshotFactory.CreateEightColumnDeck(layout, decoded).Value;
            GameplaySkinLaneColourSnapshot keyOnly =
                BmsGameplaySkinLaneColourSnapshotFactory.CreateKeyOnly(layout, decoded).Value;
            GameplaySkinLaneId scratch1 = GameplaySkinLaneId.Create("bms.lane.scratch-1");
            GameplaySkinLaneId key1 = GameplaySkinLaneId.Create("bms.lane.key-1");
            GameplaySkinLaneId key8 = GameplaySkinLaneId.Create("bms.lane.key-8");
            GameplaySkinLaneId key14 = GameplaySkinLaneId.Create("bms.lane.key-14");
            GameplaySkinLaneId scratch2 = GameplaySkinLaneId.Create("bms.lane.scratch-2");

            Assert.Multiple(() =>
            {
                Assert.That(colour(full, scratch1).Value, Is.EqualTo(sourceColour(40, 0)));
                Assert.That(colour(full, key1).Value, Is.EqualTo(sourceColour(40, 1)));
                Assert.That(colour(full, key8).Value, Is.EqualTo(sourceColour(40, 8)));
                Assert.That(colour(full, scratch2).Value, Is.EqualTo(sourceColour(40, 15)));

                Assert.That(colour(deck, scratch1).Value, Is.EqualTo(sourceColour(50, 0)));
                Assert.That(colour(deck, key1).Value, Is.EqualTo(sourceColour(50, 1)));
                Assert.That(colour(deck, key8).Value, Is.EqualTo(sourceColour(50, 0)));
                Assert.That(colour(deck, scratch2).Value, Is.EqualTo(sourceColour(50, 7)));
                Assert.That(lightColour(deck, key8).Value, Is.EqualTo(sourceLightColour(50, 0)));

                Assert.That(colour(keyOnly, scratch1).IsDeclared, Is.False);
                Assert.That(colour(keyOnly, key1).Value, Is.EqualTo(sourceColour(60, 0)));
                Assert.That(colour(keyOnly, key8).Value, Is.EqualTo(sourceColour(60, 7)));
                Assert.That(colour(keyOnly, key14).Value, Is.EqualTo(sourceColour(60, 13)));
                Assert.That(colour(keyOnly, scratch2).IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestMissingAndExplicitEmptyBucketsRemainDistinct()
        {
            BmsLaneLayout layout = BmsLaneLayout.CreateForKeymode(BmsKeymode.Key7K);
            GameplaySkinConfigurationDeclaration<GameplaySkinLaneColourSnapshot> missing =
                BmsGameplaySkinLaneColourSnapshotFactory.CreateFullVisual(layout, decode("[Mania]\nKeys: 7\n"));
            GameplaySkinConfigurationDeclaration<GameplaySkinLaneColourSnapshot> empty =
                BmsGameplaySkinLaneColourSnapshotFactory.CreateFullVisual(layout, decode("[Mania]\nKeys: 8\n"));

            Assert.Multiple(() =>
            {
                Assert.That(missing.IsDeclared, Is.False);
                Assert.That(empty.IsDeclared, Is.True);
                Assert.That(empty.Value.Declarations, Is.Empty);
            });
        }

        [Test]
        public void TestInvalidInputsAndProjectionKindsFailClosed()
        {
            BmsLaneLayout sevenKey = BmsLaneLayout.CreateForKeymode(BmsKeymode.Key7K);

            Assert.Multiple(() =>
            {
                Assert.That(() => BmsGameplaySkinLaneColourSnapshotFactory.CreateFullVisual(
                    null!, Array.Empty<LegacyManiaSkinConfiguration>()), Throws.ArgumentNullException);
                Assert.That(() => BmsGameplaySkinLaneColourSnapshotFactory.CreateFullVisual(sevenKey, null!), Throws.ArgumentNullException);
                Assert.That(() => BmsGameplaySkinLaneColourSnapshotFactory.CreateEightColumnDeck(
                    sevenKey, Array.Empty<LegacyManiaSkinConfiguration>()), Throws.ArgumentException);
                Assert.That(() => BmsGameplaySkinLaneColourSnapshotFactory.CreateKeyOnly(
                    BmsLaneLayout.CreateForKeymode(BmsKeymode.Key9K_Bms), Array.Empty<LegacyManiaSkinConfiguration>()),
                    Throws.ArgumentException);
                Assert.That(() => BmsGameplaySkinLaneColourSnapshotFactory.CreateFullVisual(
                    BmsLaneLayout.CreateForKeymode((BmsKeymode)99), Array.Empty<LegacyManiaSkinConfiguration>()),
                    Throws.ArgumentException);
            });
        }

        [Test]
        public void TestMappingAndProjectionFactoriesRemainInternalWithoutProductionSkinAuthority()
        {
            Type[] factoryTypes =
            {
                typeof(BmsGameplaySkinLegacyManiaLaneMappingFactory),
                typeof(BmsGameplaySkinLaneColourSnapshotFactory),
            };

            Assert.Multiple(() =>
            {
                Assert.That(factoryTypes.Select(type => type.IsNotPublic), Is.All.True);
                Assert.That(factoryTypes.SelectMany(type => type.GetFields()), Is.Empty);
                Assert.That(factoryTypes.SelectMany(type => type.GetProperties()), Is.Empty);
                Assert.That(factoryTypes.SelectMany(type => type.GetMethods())
                    .SelectMany(method => method.GetParameters())
                    .Select(parameter => parameter.ParameterType.Name), Has.None.EqualTo("ISkin"));
            });
        }

        private static GameplaySkinConfigurationDeclaration<Color4> colour(
            GameplaySkinLaneColourSnapshot snapshot,
            GameplaySkinLaneId laneId)
            => snapshot.GetDeclaration(laneId, GameplaySkinLaneColourFieldCatalog.LaneBackground);

        private static GameplaySkinConfigurationDeclaration<Color4> lightColour(
            GameplaySkinLaneColourSnapshot snapshot,
            GameplaySkinLaneId laneId)
            => snapshot.GetDeclaration(laneId, GameplaySkinLaneColourFieldCatalog.LaneLight);

        private static Color4 sourceColour(int bucketMarker, int zeroBasedSourceColumn)
            => new((byte)bucketMarker, (byte)(zeroBasedSourceColumn + 1), (byte)0, (byte)255);

        private static Color4 sourceLightColour(int bucketMarker, int zeroBasedSourceColumn)
            => new((byte)(bucketMarker + 100), (byte)(zeroBasedSourceColumn + 1), (byte)0, (byte)255);

        private static string createColourBucket(int keys, int bucketMarker)
        {
            var builder = new StringBuilder();
            builder.AppendLine("[Mania]");
            builder.AppendLine($"Keys: {keys}");

            for (int sourceColumn = 0; sourceColumn < keys; sourceColumn++)
            {
                builder.AppendLine($"Colour{sourceColumn + 1}: {bucketMarker}, {sourceColumn + 1}, 0, 255");
                builder.AppendLine($"ColourLight{sourceColumn + 1}: {bucketMarker + 100}, {sourceColumn + 1}, 0, 255");
            }

            return builder.ToString();
        }

        private static IReadOnlyList<LegacyManiaSkinConfiguration> decode(string skinIni)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(skinIni));
            using var reader = new LineBufferedReader(stream);
            return new LegacyManiaSkinDecoder().Decode(reader);
        }
    }
}
