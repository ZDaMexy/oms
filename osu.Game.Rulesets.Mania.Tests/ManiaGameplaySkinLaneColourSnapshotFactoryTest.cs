// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.IO;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Tests
{
    [TestFixture]
    public sealed class ManiaGameplaySkinLaneColourSnapshotFactoryTest
    {
        [Test]
        public void TestMissingAndExplicitEmptyBucketsRemainDistinct()
        {
            ManiaBeatmap beatmap = createBeatmap(4);
            GameplaySkinConfigurationDeclaration<GameplaySkinLaneColourSnapshot> missing =
                ManiaGameplaySkinLaneColourSnapshotFactory.Create(decode("[Mania]\nKeys: 7\n"), beatmap);
            GameplaySkinConfigurationDeclaration<GameplaySkinLaneColourSnapshot> empty =
                ManiaGameplaySkinLaneColourSnapshotFactory.Create(decode("[Mania]\nKeys: 4\n"), beatmap);

            Assert.Multiple(() =>
            {
                Assert.That(missing.IsDeclared, Is.False);
                Assert.That(empty.IsDeclared, Is.True);
                Assert.That(empty.Value.Declarations, Is.Empty);
                Assert.That(empty.Value.Topology.LanesInLogicalOrder, Has.Count.EqualTo(4));
            });
        }

        [Test]
        public void TestNativeManiaUsesGlobalLogicalColumnsAndStableLaneIds()
        {
            GameplaySkinLaneColourSnapshot snapshot = ManiaGameplaySkinLaneColourSnapshotFactory.Create(
                decode(
                    "[Mania]\n" +
                    "Keys: 4\n" +
                    "Colour1: 1,2,3\n" +
                    "ColourLight4: 4,5,6,7\n"),
                createBeatmap(4)).Value;

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.GetDeclaration(
                    GameplaySkinLaneId.Create("mania.lane.column-1"), GameplaySkinLaneColourFieldCatalog.LaneBackground).Value,
                    Is.EqualTo(new Color4(1, 2, 3, 255)));
                Assert.That(snapshot.GetDeclaration(
                    GameplaySkinLaneId.Create("mania.lane.column-4"), GameplaySkinLaneColourFieldCatalog.LaneLight).Value,
                    Is.EqualTo(new Color4(4, 5, 6, 7)));
                Assert.That(snapshot.GetDeclaration(
                    GameplaySkinLaneId.Create("mania.lane.column-2"), GameplaySkinLaneColourFieldCatalog.LaneBackground).IsDeclared,
                    Is.False);
            });
        }

        [Test]
        public void TestDualStageDoesNotRestartLegacySourceColumns()
        {
            GameplaySkinLaneColourSnapshot snapshot = ManiaGameplaySkinLaneColourSnapshotFactory.Create(
                decode("[Mania]\nKeys: 10\nColour8: 8,7,6,5\nColourLight3: 3,4,5,6\n"),
                createBeatmap(5, 5)).Value;
            GameplaySkinLaneTopologyEntry secondStageTarget = snapshot.Topology.LanesInLogicalOrder[7];
            GameplaySkinLaneTopologyEntry firstStageTarget = snapshot.Topology.LanesInLogicalOrder[2];

            Assert.Multiple(() =>
            {
                Assert.That(secondStageTarget.Identity.Id.Value, Is.EqualTo("mania.lane.column-8"));
                Assert.That(secondStageTarget.Identity.Group.Id.Value, Is.EqualTo("mania.group.stage-2"));
                Assert.That(secondStageTarget.GroupLocalLogicalIndex, Is.EqualTo(2));
                Assert.That(snapshot.GetDeclaration(
                    secondStageTarget.Identity.Id, GameplaySkinLaneColourFieldCatalog.LaneBackground).Value,
                    Is.EqualTo(new Color4(8, 7, 6, 5)));
                Assert.That(snapshot.GetDeclaration(
                    firstStageTarget.Identity.Id, GameplaySkinLaneColourFieldCatalog.LaneLight).Value,
                    Is.EqualTo(new Color4(3, 4, 5, 6)));
            });
        }

        [Test]
        public void TestFactoryRejectsInvalidInputsThroughTopologyAuthority()
        {
            ManiaBeatmap empty = createBeatmap(4);
            empty.Stages.Clear();

            Assert.Multiple(() =>
            {
                Assert.That(() => ManiaGameplaySkinLaneColourSnapshotFactory.Create(null!, createBeatmap(4)), Throws.ArgumentNullException);
                Assert.That(() => ManiaGameplaySkinLaneColourSnapshotFactory.Create(
                    Array.Empty<LegacyManiaSkinConfiguration>(), null!), Throws.ArgumentNullException);
                Assert.That(() => ManiaGameplaySkinLaneColourSnapshotFactory.Create(
                    Array.Empty<LegacyManiaSkinConfiguration>(), empty), Throws.ArgumentException);
                Assert.That(() => ManiaGameplaySkinLaneColourSnapshotFactory.Create(
                    Array.Empty<LegacyManiaSkinConfiguration>(), createBeatmap(ManiaRuleset.MAX_STAGE_KEYS + 1)), Throws.ArgumentException);
            });
        }

        [Test]
        public void TestFactoryRemainsInternalAndDoesNotQueryProductionSkinAuthority()
        {
            Assert.Multiple(() =>
            {
                Assert.That(typeof(ManiaGameplaySkinLaneColourSnapshotFactory).IsNotPublic, Is.True);
                Assert.That(typeof(ManiaGameplaySkinLaneColourSnapshotFactory).GetFields(), Is.Empty);
                Assert.That(typeof(ManiaGameplaySkinLaneColourSnapshotFactory).GetProperties(), Is.Empty);
                Assert.That(typeof(ManiaGameplaySkinLaneColourSnapshotFactory).GetMethods()
                    .SelectMany(method => method.GetParameters())
                    .Select(parameter => parameter.ParameterType.Name), Has.None.EqualTo("ISkin"));
            });
        }

        private static ManiaBeatmap createBeatmap(params int[] stageColumns)
        {
            if (stageColumns.Length == 0)
                throw new ArgumentException("At least one stage is required.", nameof(stageColumns));

            var beatmap = new ManiaBeatmap(new StageDefinition(stageColumns[0]));

            foreach (int columns in stageColumns.Skip(1))
                beatmap.Stages.Add(new StageDefinition(columns));

            return beatmap;
        }

        private static IReadOnlyList<LegacyManiaSkinConfiguration> decode(string skinIni)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(skinIni));
            using var reader = new LineBufferedReader(stream);
            return new LegacyManiaSkinDecoder().Decode(reader);
        }
    }
}
