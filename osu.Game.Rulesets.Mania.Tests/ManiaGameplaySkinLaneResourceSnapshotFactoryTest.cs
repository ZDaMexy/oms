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

namespace osu.Game.Rulesets.Mania.Tests
{
    [TestFixture]
    public sealed class ManiaGameplaySkinLaneResourceSnapshotFactoryTest
    {
        [Test]
        public void TestMissingAndExplicitEmptyBucketsRemainDistinct()
        {
            ManiaBeatmap beatmap = createBeatmap(4);
            GameplaySkinConfigurationDeclaration<GameplaySkinLaneResourceSnapshot> missing =
                ManiaGameplaySkinLaneResourceSnapshotFactory.Create(decode("[Mania]\nKeys: 7\n"), beatmap);
            GameplaySkinConfigurationDeclaration<GameplaySkinLaneResourceSnapshot> empty =
                ManiaGameplaySkinLaneResourceSnapshotFactory.Create(decode("[Mania]\nKeys: 4\n"), beatmap);

            Assert.Multiple(() =>
            {
                Assert.That(missing.IsDeclared, Is.False);
                Assert.That(empty.IsDeclared, Is.True);
                Assert.That(empty.Value.Declarations, Is.Empty);
                Assert.That(empty.Value.Topology.LanesInLogicalOrder, Has.Count.EqualTo(4));
            });
        }

        [Test]
        public void TestNativeManiaUsesZeroBasedColumnsAndStableLaneIds()
        {
            GameplaySkinLaneResourceSnapshot snapshot = ManiaGameplaySkinLaneResourceSnapshotFactory.Create(
                decode(
                    "[Mania]\n" +
                    "Keys: 4\n" +
                    "NoteImage0: first\n" +
                    "NoteImage3: fourth\n" +
                    "KeyImage2D: third-down\n"),
                createBeatmap(4)).Value;

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.GetDeclaration(
                    GameplaySkinLaneId.Create("mania.lane.column-1"), GameplaySkinLaneResourceFieldCatalog.Note).Value, Is.EqualTo("first"));
                Assert.That(snapshot.GetDeclaration(
                    GameplaySkinLaneId.Create("mania.lane.column-4"), GameplaySkinLaneResourceFieldCatalog.Note).Value, Is.EqualTo("fourth"));
                Assert.That(snapshot.GetDeclaration(
                    GameplaySkinLaneId.Create("mania.lane.column-3"), GameplaySkinLaneResourceFieldCatalog.KeyPressed).Value, Is.EqualTo("third-down"));
                Assert.That(snapshot.GetDeclaration(
                    GameplaySkinLaneId.Create("mania.lane.column-2"), GameplaySkinLaneResourceFieldCatalog.Note).IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestDualStageUsesGlobalLegacyColumnAndKeepsLocalSpecialRole()
        {
            GameplaySkinLaneResourceSnapshot snapshot = ManiaGameplaySkinLaneResourceSnapshotFactory.Create(
                decode("[Mania]\nKeys: 10\nNoteImage7: second-stage-special\n"),
                createBeatmap(5, 5)).Value;
            GameplaySkinLaneTopologyEntry target = snapshot.Topology.LanesInLogicalOrder[7];

            Assert.Multiple(() =>
            {
                Assert.That(target.Identity.Id.Value, Is.EqualTo("mania.lane.column-8"));
                Assert.That(target.Identity.Group.Id.Value, Is.EqualTo("mania.group.stage-2"));
                Assert.That(target.Identity.Role, Is.EqualTo(GameplaySkinLaneRole.SpecialKey));
                Assert.That(target.GroupLocalLogicalIndex, Is.EqualTo(2));
                Assert.That(snapshot.GetDeclaration(target.Identity.Id, GameplaySkinLaneResourceFieldCatalog.Note).Value,
                    Is.EqualTo("second-stage-special"));
            });
        }

        [Test]
        public void TestPartialAndExplicitEmptyValuesRemainDeclarationsOnly()
        {
            GameplaySkinLaneResourceSnapshot snapshot = ManiaGameplaySkinLaneResourceSnapshotFactory.Create(
                decode("[Mania]\nKeys: 3\nNoteImage1:\nKeyImage1: key\n"),
                createBeatmap(3)).Value;
            GameplaySkinLaneId centre = GameplaySkinLaneId.Create("mania.lane.column-2");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.GetDeclaration(centre, GameplaySkinLaneResourceFieldCatalog.Note).IsDeclared, Is.True);
                Assert.That(snapshot.GetDeclaration(centre, GameplaySkinLaneResourceFieldCatalog.Note).Value, Is.Empty);
                Assert.That(snapshot.GetDeclaration(centre, GameplaySkinLaneResourceFieldCatalog.Key).Value, Is.EqualTo("key"));
                Assert.That(snapshot.GetDeclaration(centre, GameplaySkinLaneResourceFieldCatalog.LongNoteHead).IsDeclared, Is.False);
                Assert.That(snapshot.Topology.LanesInLogicalOrder[1].Identity.Role, Is.EqualTo(GameplaySkinLaneRole.SpecialKey));
            });
        }

        [Test]
        public void TestFactoryRejectsInvalidInputsThroughTopologyAuthority()
        {
            ManiaBeatmap empty = createBeatmap(4);
            empty.Stages.Clear();

            Assert.Multiple(() =>
            {
                Assert.That(() => ManiaGameplaySkinLaneResourceSnapshotFactory.Create(null!, createBeatmap(4)), Throws.ArgumentNullException);
                Assert.That(() => ManiaGameplaySkinLaneResourceSnapshotFactory.Create(Array.Empty<LegacyManiaSkinConfiguration>(), null!), Throws.ArgumentNullException);
                Assert.That(() => ManiaGameplaySkinLaneResourceSnapshotFactory.Create(Array.Empty<LegacyManiaSkinConfiguration>(), empty), Throws.ArgumentException);
                Assert.That(() => ManiaGameplaySkinLaneResourceSnapshotFactory.Create(
                    Array.Empty<LegacyManiaSkinConfiguration>(), createBeatmap(ManiaRuleset.MAX_STAGE_KEYS + 1)), Throws.ArgumentException);
            });
        }

        [Test]
        public void TestFactoryRemainsInternalAndDoesNotQuerySkinManager()
        {
            Assert.Multiple(() =>
            {
                Assert.That(typeof(ManiaGameplaySkinLaneResourceSnapshotFactory).IsNotPublic, Is.True);
                Assert.That(typeof(ManiaGameplaySkinLaneResourceSnapshotFactory).GetFields(), Is.Empty);
                Assert.That(typeof(ManiaGameplaySkinLaneResourceSnapshotFactory).GetProperties(), Is.Empty);
                Assert.That(typeof(ManiaGameplaySkinLaneResourceSnapshotFactory).GetMethods()
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
