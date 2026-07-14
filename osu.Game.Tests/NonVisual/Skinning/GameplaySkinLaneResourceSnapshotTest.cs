// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using osu.Game.IO;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinLaneResourceSnapshotTest
    {
        [Test]
        public void TestClosedFieldCatalogHasStableSlotAssociations()
        {
            Assert.Multiple(() =>
            {
                Assert.That(GameplaySkinLaneResourceFieldCatalog.All.Select(field => field.Id), Is.EqualTo(new[]
                {
                    "object.note.resource",
                    "object.long-note.head.resource",
                    "object.long-note.body.resource",
                    "object.long-note.tail.resource",
                    "playfield.key.resource",
                    "playfield.key.pressed-resource",
                }));
                Assert.That(GameplaySkinLaneResourceFieldCatalog.All.Select(field => field.Slot), Is.EqualTo(new[]
                {
                    GameplaySkinSlotCatalog.Note,
                    GameplaySkinSlotCatalog.LongNoteHead,
                    GameplaySkinSlotCatalog.LongNoteBody,
                    GameplaySkinSlotCatalog.LongNoteTail,
                    GameplaySkinSlotCatalog.KeyVisual,
                    GameplaySkinSlotCatalog.KeyVisual,
                }));
                Assert.That(GameplaySkinLaneResourceFieldCatalog.TryGet("object.note.resource", out GameplaySkinLaneResourceField? note), Is.True);
                Assert.That(note, Is.SameAs(GameplaySkinLaneResourceFieldCatalog.Note));
                Assert.That(GameplaySkinLaneResourceFieldCatalog.TryGet("Object.Note.Resource", out _), Is.False);
                Assert.That(GameplaySkinLaneResourceFieldCatalog.TryGet("future.resource", out _), Is.False);
                Assert.That(GameplaySkinLaneResourceFieldCatalog.TryGet(null, out _), Is.False);
            });
        }

        [Test]
        public void TestMissingAndExplicitEmptyResourceRemainDistinct()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneId firstLane = topology.LanesInLogicalOrder[0].Identity.Id;
            GameplaySkinLaneResourceSnapshot snapshot = GameplaySkinLaneResourceSnapshot.Create(topology, new[]
            {
                GameplaySkinLaneResourceDeclaration.Create(firstLane, GameplaySkinLaneResourceFieldCatalog.Note, string.Empty),
            });

            GameplaySkinConfigurationDeclaration<string> empty = snapshot.GetDeclaration(firstLane, GameplaySkinLaneResourceFieldCatalog.Note);
            GameplaySkinConfigurationDeclaration<string> absent = snapshot.GetDeclaration(firstLane, GameplaySkinLaneResourceFieldCatalog.LongNoteHead);

            Assert.Multiple(() =>
            {
                Assert.That(empty.IsDeclared, Is.True);
                Assert.That(empty.Value, Is.Empty);
                Assert.That(absent.IsDeclared, Is.False);
                Assert.That(() => absent.Value, Throws.InvalidOperationException);
            });
        }

        [Test]
        public void TestSnapshotCopiesAndOrdersDeclarationsDeterministically()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneId firstLane = topology.LanesInLogicalOrder[0].Identity.Id;
            GameplaySkinLaneId secondLane = topology.LanesInLogicalOrder[1].Identity.Id;
            var input = new[]
            {
                GameplaySkinLaneResourceDeclaration.Create(secondLane, GameplaySkinLaneResourceFieldCatalog.Key, "second-key"),
                GameplaySkinLaneResourceDeclaration.Create(firstLane, GameplaySkinLaneResourceFieldCatalog.LongNoteBody, "first-body"),
                GameplaySkinLaneResourceDeclaration.Create(firstLane, GameplaySkinLaneResourceFieldCatalog.Note, "first-note"),
            };

            GameplaySkinLaneResourceSnapshot snapshot = GameplaySkinLaneResourceSnapshot.Create(topology, input);
            input[0] = GameplaySkinLaneResourceDeclaration.Create(firstLane, GameplaySkinLaneResourceFieldCatalog.KeyPressed, "mutated");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Declarations.Select(declaration => (declaration.LaneId.Value, declaration.Field.Id)), Is.EqualTo(new[]
                {
                    ("test.lane.first", "object.note.resource"),
                    ("test.lane.first", "object.long-note.body.resource"),
                    ("test.lane.second", "playfield.key.resource"),
                }));
                Assert.That(snapshot.GetDeclaration(secondLane, GameplaySkinLaneResourceFieldCatalog.Key).Value, Is.EqualTo("second-key"));
                Assert.That(snapshot.GetDeclaration(firstLane, GameplaySkinLaneResourceFieldCatalog.KeyPressed).IsDeclared, Is.False);
                Assert.That(snapshot.Topology, Is.SameAs(topology));
            });
        }

        [Test]
        public void TestEquivalentLaneIdUsesOrdinalLookup()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneResourceSnapshot snapshot = GameplaySkinLaneResourceSnapshot.Create(topology, new[]
            {
                GameplaySkinLaneResourceDeclaration.Create(
                    topology.LanesInLogicalOrder[0].Identity.Id,
                    GameplaySkinLaneResourceFieldCatalog.Note,
                    "note"),
            });

            Assert.That(snapshot.GetDeclaration(
                GameplaySkinLaneId.Create("test.lane.first"),
                GameplaySkinLaneResourceFieldCatalog.Note).Value, Is.EqualTo("note"));
        }

        [Test]
        public void TestInvalidSnapshotInputsFailClosed()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneId lane = topology.LanesInLogicalOrder[0].Identity.Id;
            GameplaySkinLaneResourceDeclaration declaration = GameplaySkinLaneResourceDeclaration.Create(
                lane, GameplaySkinLaneResourceFieldCatalog.Note, "note");
            GameplaySkinLaneResourceDeclaration[] containsNull = { null! };
            var unknownField = new GameplaySkinLaneResourceField("test.unknown-field", GameplaySkinSlotCatalog.Note);

            Assert.Multiple(() =>
            {
                Assert.That(() => GameplaySkinLaneResourceDeclaration.Create(null!, GameplaySkinLaneResourceFieldCatalog.Note, "note"), Throws.ArgumentNullException);
                Assert.That(() => GameplaySkinLaneResourceDeclaration.Create(lane, null!, "note"), Throws.ArgumentNullException);
                Assert.That(() => GameplaySkinLaneResourceDeclaration.Create(lane, GameplaySkinLaneResourceFieldCatalog.Note, null!), Throws.ArgumentNullException);
                Assert.That(() => GameplaySkinLaneResourceDeclaration.Create(lane, unknownField, "note"), Throws.ArgumentException);
                Assert.That(() => GameplaySkinLaneResourceSnapshot.Create(null!, Array.Empty<GameplaySkinLaneResourceDeclaration>()), Throws.ArgumentNullException);
                Assert.That(() => GameplaySkinLaneResourceSnapshot.Create(topology, null!), Throws.ArgumentNullException);
                Assert.That(() => GameplaySkinLaneResourceSnapshot.Create(topology, containsNull), Throws.ArgumentException);
                Assert.That(() => GameplaySkinLaneResourceSnapshot.Create(topology, new[] { declaration, declaration }), Throws.ArgumentException);
                Assert.That(() => GameplaySkinLaneResourceSnapshot.Create(topology, new[]
                {
                    GameplaySkinLaneResourceDeclaration.Create(GameplaySkinLaneId.Create("test.lane.outside"), GameplaySkinLaneResourceFieldCatalog.Note, "note"),
                }), Throws.ArgumentException);
            });
        }

        [Test]
        public void TestInvalidQueriesFailClosed()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneResourceSnapshot snapshot = GameplaySkinLaneResourceSnapshot.Create(
                topology, Array.Empty<GameplaySkinLaneResourceDeclaration>());
            var unknownField = new GameplaySkinLaneResourceField("test.unknown-field", GameplaySkinSlotCatalog.Note);

            Assert.Multiple(() =>
            {
                Assert.That(() => snapshot.GetDeclaration(null!, GameplaySkinLaneResourceFieldCatalog.Note), Throws.ArgumentNullException);
                Assert.That(() => snapshot.GetDeclaration(topology.LanesInLogicalOrder[0].Identity.Id, null!), Throws.ArgumentNullException);
                Assert.That(() => snapshot.GetDeclaration(GameplaySkinLaneId.Create("test.lane.outside"), GameplaySkinLaneResourceFieldCatalog.Note), Throws.ArgumentException);
                Assert.That(() => snapshot.GetDeclaration(topology.LanesInLogicalOrder[0].Identity.Id, unknownField), Throws.ArgumentException);
            });
        }

        [Test]
        public void TestSafeStringsDoNotExposeResourceName()
        {
            const string private_resource = "private/package/path/note";
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneResourceDeclaration declaration = GameplaySkinLaneResourceDeclaration.Create(
                topology.LanesInLogicalOrder[0].Identity.Id,
                GameplaySkinLaneResourceFieldCatalog.Note,
                private_resource);

            Assert.Multiple(() =>
            {
                Assert.That(declaration.ToString(), Does.Not.Contain(private_resource));
                Assert.That(declaration.ToString(), Does.Contain("test.lane.first").And.Contain("object.note.resource"));
                Assert.That(GameplaySkinConfigurationDeclaration<GameplaySkinLaneResourceSnapshot>.Declared(
                    GameplaySkinLaneResourceSnapshot.Create(topology, new[] { declaration })).ToString(), Is.EqualTo("Declared"));
            });
        }

        [Test]
        public void TestSnapshotSurfaceDoesNotExposeRuntimeAuthority()
        {
            Type[] publicPropertyTypes = typeof(GameplaySkinLaneResourceSnapshot)
                                         .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                         .Select(property => property.PropertyType)
                                         .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(typeof(GameplaySkinLaneResourceSnapshot).IsSealed, Is.True);
                Assert.That(typeof(GameplaySkinLaneResourceDeclaration).IsSealed, Is.True);
                Assert.That(typeof(GameplaySkinLaneResourceSnapshot).GetConstructors(), Is.Empty);
                Assert.That(typeof(GameplaySkinLaneResourceDeclaration).GetConstructors(), Is.Empty);
                Assert.That(publicPropertyTypes.Select(type => type.FullName), Has.None.Contains("Drawable"));
                Assert.That(publicPropertyTypes.Select(type => type.FullName), Has.None.Contains("Bindable"));
                Assert.That(publicPropertyTypes.Select(type => type.FullName), Has.None.Contains("ISkin"));
                Assert.That(publicPropertyTypes, Has.None.AssignableTo<Delegate>());
            });
        }

        [Test]
        public void TestLegacyManiaMissingAndEmptyBucketsRemainDistinct()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            Dictionary<GameplaySkinLaneId, int> mapping = createTwoColumnMapping(topology);

            GameplaySkinConfigurationDeclaration<GameplaySkinLaneResourceSnapshot> missing =
                LegacyManiaGameplaySkinLaneResourceSnapshotFactory.Create(
                    decodeMania("[Mania]\nKeys: 4\n"), 2, topology, mapping);
            GameplaySkinConfigurationDeclaration<GameplaySkinLaneResourceSnapshot> empty =
                LegacyManiaGameplaySkinLaneResourceSnapshotFactory.Create(
                    decodeMania("[Mania]\nKeys: 2\n"), 2, topology, mapping);

            Assert.Multiple(() =>
            {
                Assert.That(missing.IsDeclared, Is.False);
                Assert.That(empty.IsDeclared, Is.True);
                Assert.That(empty.Value.Declarations, Is.Empty);
                Assert.That(empty.Value.GetDeclaration(topology.LanesInLogicalOrder[0].Identity.Id, GameplaySkinLaneResourceFieldCatalog.Note).IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestLegacyManiaProjectsAllSixFieldsAndExplicitEmpty()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneId firstLane = topology.LanesInLogicalOrder[0].Identity.Id;
            GameplaySkinLaneId secondLane = topology.LanesInLogicalOrder[1].Identity.Id;
            IReadOnlyList<LegacyManiaSkinConfiguration> decoded = decodeMania(
                "[Mania]\n" +
                "Keys: 2\n" +
                "NoteImage0: note\n" +
                "NoteImage0H: head\n" +
                "NoteImage0L: body\n" +
                "NoteImage0T: tail\n" +
                "KeyImage0: key\n" +
                "KeyImage0D: key-down\n" +
                "NoteImage1:\n");

            GameplaySkinLaneResourceSnapshot snapshot = LegacyManiaGameplaySkinLaneResourceSnapshotFactory.Create(
                decoded, 2, topology, createTwoColumnMapping(topology)).Value;

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.GetDeclaration(firstLane, GameplaySkinLaneResourceFieldCatalog.Note).Value, Is.EqualTo("note"));
                Assert.That(snapshot.GetDeclaration(firstLane, GameplaySkinLaneResourceFieldCatalog.LongNoteHead).Value, Is.EqualTo("head"));
                Assert.That(snapshot.GetDeclaration(firstLane, GameplaySkinLaneResourceFieldCatalog.LongNoteBody).Value, Is.EqualTo("body"));
                Assert.That(snapshot.GetDeclaration(firstLane, GameplaySkinLaneResourceFieldCatalog.LongNoteTail).Value, Is.EqualTo("tail"));
                Assert.That(snapshot.GetDeclaration(firstLane, GameplaySkinLaneResourceFieldCatalog.Key).Value, Is.EqualTo("key"));
                Assert.That(snapshot.GetDeclaration(firstLane, GameplaySkinLaneResourceFieldCatalog.KeyPressed).Value, Is.EqualTo("key-down"));
                Assert.That(snapshot.GetDeclaration(secondLane, GameplaySkinLaneResourceFieldCatalog.Note).IsDeclared, Is.True);
                Assert.That(snapshot.GetDeclaration(secondLane, GameplaySkinLaneResourceFieldCatalog.Note).Value, Is.Empty);
                Assert.That(snapshot.GetDeclaration(secondLane, GameplaySkinLaneResourceFieldCatalog.Key).IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestLegacyManiaSnapshotIsDetachedFromMutableDecoderOutput()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            IReadOnlyList<LegacyManiaSkinConfiguration> decoded = decodeMania("[Mania]\nKeys: 2\nNoteImage0: original\n");
            GameplaySkinLaneResourceSnapshot snapshot = LegacyManiaGameplaySkinLaneResourceSnapshotFactory.Create(
                decoded, 2, topology, createTwoColumnMapping(topology)).Value;

            decoded[0].ImageLookups["NoteImage0"] = "mutated";
            decoded[0].ImageLookups["KeyImage0"] = "late";

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.GetDeclaration(topology.LanesInLogicalOrder[0].Identity.Id, GameplaySkinLaneResourceFieldCatalog.Note).Value,
                    Is.EqualTo("original"));
                Assert.That(snapshot.GetDeclaration(topology.LanesInLogicalOrder[0].Identity.Id, GameplaySkinLaneResourceFieldCatalog.Key).IsDeclared,
                    Is.False);
            });
        }

        [Test]
        public void TestLegacyManiaFactoryRejectsAmbiguousOrInvalidInputs()
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
                Assert.That(() => LegacyManiaGameplaySkinLaneResourceSnapshotFactory.Create(null!, 2, topology, mapping), Throws.ArgumentNullException);
                Assert.That(() => LegacyManiaGameplaySkinLaneResourceSnapshotFactory.Create(Array.Empty<LegacyManiaSkinConfiguration>(), 2, null!, mapping), Throws.ArgumentNullException);
                Assert.That(() => LegacyManiaGameplaySkinLaneResourceSnapshotFactory.Create(Array.Empty<LegacyManiaSkinConfiguration>(), 2, topology, null!), Throws.ArgumentNullException);
                Assert.That(() => LegacyManiaGameplaySkinLaneResourceSnapshotFactory.Create(Array.Empty<LegacyManiaSkinConfiguration>(), 0, topology, mapping), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => LegacyManiaGameplaySkinLaneResourceSnapshotFactory.Create(containsNull, 2, topology, mapping), Throws.ArgumentException);
                Assert.That(() => LegacyManiaGameplaySkinLaneResourceSnapshotFactory.Create(duplicate, 2, topology, mapping), Throws.ArgumentException);
                Assert.That(() => LegacyManiaGameplaySkinLaneResourceSnapshotFactory.Create(Array.Empty<LegacyManiaSkinConfiguration>(), 2, topology, outsideMapping), Throws.ArgumentException);
                Assert.That(() => LegacyManiaGameplaySkinLaneResourceSnapshotFactory.Create(Array.Empty<LegacyManiaSkinConfiguration>(), 2, topology, outOfRangeMapping), Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        private static GameplaySkinLaneTopologySnapshot createTopology()
        {
            GameplaySkinLaneGroupIdentity group = GameplaySkinLaneGroupIdentity.Create(
                GameplaySkinLaneGroupId.Create("test.group.main"), GameplaySkinLaneSide.Neutral);
            var lanes = new[]
            {
                GameplaySkinLaneTopologyEntry.Create(
                    GameplaySkinLaneIdentity.Create(GameplaySkinLaneId.Create("test.lane.first"), group, GameplaySkinLaneRole.Key),
                    0, 0, 0, 0),
                GameplaySkinLaneTopologyEntry.Create(
                    GameplaySkinLaneIdentity.Create(GameplaySkinLaneId.Create("test.lane.second"), group, GameplaySkinLaneRole.Key),
                    1, 1, 1, 1),
            };

            return GameplaySkinLaneTopologySnapshot.Create(new[]
            {
                GameplaySkinLaneTopologyGroup.Create(group, 0, 0, lanes),
            });
        }

        private static Dictionary<GameplaySkinLaneId, int> createTwoColumnMapping(GameplaySkinLaneTopologySnapshot topology)
            => topology.LanesInLogicalOrder.ToDictionary(lane => lane.Identity.Id, lane => lane.GlobalLogicalIndex);

        private static IReadOnlyList<LegacyManiaSkinConfiguration> decodeMania(string skinIni)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(skinIni));
            using var reader = new LineBufferedReader(stream);
            return new LegacyManiaSkinDecoder().Decode(reader);
        }
    }
}
