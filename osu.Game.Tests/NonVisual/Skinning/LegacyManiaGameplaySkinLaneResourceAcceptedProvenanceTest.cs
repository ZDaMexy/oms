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
    public sealed class LegacyManiaGameplaySkinLaneResourceAcceptedProvenanceTest
    {
        private static readonly (LegacyManiaSkinLaneResourceField LegacyField, GameplaySkinLaneResourceField Field, string SourceKey)[] resource_fields =
        {
            (LegacyManiaSkinLaneResourceField.Note, GameplaySkinLaneResourceFieldCatalog.Note, "NoteImage0"),
            (LegacyManiaSkinLaneResourceField.LongNoteHead, GameplaySkinLaneResourceFieldCatalog.LongNoteHead, "NoteImage0H"),
            (LegacyManiaSkinLaneResourceField.LongNoteBody, GameplaySkinLaneResourceFieldCatalog.LongNoteBody, "NoteImage0L"),
            (LegacyManiaSkinLaneResourceField.LongNoteTail, GameplaySkinLaneResourceFieldCatalog.LongNoteTail, "NoteImage0T"),
            (LegacyManiaSkinLaneResourceField.Key, GameplaySkinLaneResourceFieldCatalog.Key, "KeyImage0"),
            (LegacyManiaSkinLaneResourceField.KeyPressed, GameplaySkinLaneResourceFieldCatalog.KeyPressed, "KeyImage0D"),
        };

        [Test]
        public void TestAllSixExactCanonicalDeclarationsPopulateCompatibilityAndSidecar()
        {
            Dictionary<string, string> expected = createAllResourceValues("accepted-");
            IReadOnlyList<LegacyManiaSkinConfiguration> decoded = decode(
                "[Mania]\n" +
                "Keys: 2\n" +
                createResourceLines(expected));
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneId firstLane = topology.LanesInLogicalOrder[0].Identity.Id;
            GameplaySkinLaneResourceSnapshot snapshot = createSnapshot(decoded, topology);

            Assert.Multiple(() =>
            {
                Assert.That(decoded[0].ImageLookups, Is.EquivalentTo(expected));

                foreach ((LegacyManiaSkinLaneResourceField legacyField, GameplaySkinLaneResourceField field, string sourceKey) in resource_fields)
                {
                    Assert.That(decoded[0].GetAcceptedLaneResource(legacyField, 0).Value, Is.EqualTo(expected[sourceKey]));
                    Assert.That(snapshot.GetDeclaration(firstLane, field).Value, Is.EqualTo(expected[sourceKey]));
                }
            });
        }

        [Test]
        public void TestExplicitEmptyAndDuplicateUseLastAcceptedValue()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneId firstLane = topology.LanesInLogicalOrder[0].Identity.Id;
            GameplaySkinLaneResourceSnapshot snapshot = createSnapshot(
                decode(
                    "[Mania]\n" +
                    "Keys: 2\n" +
                    "NoteImage0: first\n" +
                    "NoteImage0:   \n" +
                    "KeyImage0D: first\n" +
                    "KeyImage0D: last:variant\n"),
                topology);

            Assert.Multiple(() =>
            {
                GameplaySkinConfigurationDeclaration<string> empty = snapshot.GetDeclaration(firstLane, GameplaySkinLaneResourceFieldCatalog.Note);
                Assert.That(empty.IsDeclared, Is.True);
                Assert.That(empty.Value, Is.Empty);
                Assert.That(snapshot.GetDeclaration(firstLane, GameplaySkinLaneResourceFieldCatalog.KeyPressed).Value, Is.EqualTo("last:variant"));
            });
        }

        [Test]
        public void TestOnlyStrictZeroBasedAsciiCanonicalTokensEnterSidecar()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> decoded = decode(
                "[Mania]\n" +
                "Keys: 2\n" +
                "NoteImage1: canonical-note\n" +
                "KeyImage0D: canonical-key-down\n" +
                "NoteImage00: compatibility-only\n" +
                "NoteImage01: compatibility-only\n" +
                "NoteImage+0: compatibility-only\n" +
                "NoteImage-0: compatibility-only\n" +
                "NoteImage2: compatibility-only\n" +
                "NoteImage0h: compatibility-only\n" +
                "NoteImage0D: compatibility-only\n" +
                "NoteImage0HH: compatibility-only\n" +
                "NoteImage\uff10: compatibility-only\n" +
                "KeyImage00: compatibility-only\n" +
                "KeyImage01D: compatibility-only\n" +
                "KeyImage+0: compatibility-only\n" +
                "KeyImage-0: compatibility-only\n" +
                "KeyImage2: compatibility-only\n" +
                "KeyImage0d: compatibility-only\n" +
                "KeyImage0H: compatibility-only\n" +
                "KeyImage\uff10: compatibility-only\n" +
                "noteImage0: ignored\n" +
                "keyImage0: ignored\n");
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneResourceSnapshot snapshot = createSnapshot(decoded, topology);
            GameplaySkinLaneId firstLane = topology.LanesInLogicalOrder[0].Identity.Id;
            GameplaySkinLaneId secondLane = topology.LanesInLogicalOrder[1].Identity.Id;
            string[] compatibilityOnlyKeys =
            {
                "NoteImage00", "NoteImage01", "NoteImage+0", "NoteImage-0", "NoteImage2", "NoteImage0h", "NoteImage0D", "NoteImage0HH", "NoteImage\uff10",
                "KeyImage00", "KeyImage01D", "KeyImage+0", "KeyImage-0", "KeyImage2", "KeyImage0d", "KeyImage0H", "KeyImage\uff10",
            };

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Declarations.Select(declaration => (declaration.LaneId, declaration.Field)), Is.EquivalentTo(new[]
                {
                    (firstLane, GameplaySkinLaneResourceFieldCatalog.KeyPressed),
                    (secondLane, GameplaySkinLaneResourceFieldCatalog.Note),
                }));
                Assert.That(snapshot.GetDeclaration(secondLane, GameplaySkinLaneResourceFieldCatalog.Note).Value, Is.EqualTo("canonical-note"));
                Assert.That(snapshot.GetDeclaration(firstLane, GameplaySkinLaneResourceFieldCatalog.KeyPressed).Value, Is.EqualTo("canonical-key-down"));
                Assert.That(decoded[0].ImageLookups.Keys, Does.Contain("NoteImage1").And.Contain("KeyImage0D"));
                Assert.That(decoded[0].ImageLookups.Keys, Is.SupersetOf(compatibilityOnlyKeys));
                Assert.That(decoded[0].ImageLookups.Keys, Does.Not.Contain("noteImage0").And.Not.Contain("keyImage0"));
            });
        }

        [Test]
        public void TestDeclarationsBeforeKeysAreAttributedToAcceptedBucket()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneResourceSnapshot snapshot = createSnapshot(
                decode(
                    "[Mania]\n" +
                    "NoteImage0H: head-before-keys\n" +
                    "KeyImage1: key-before-keys\n" +
                    "Keys: 2\n"),
                topology);

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.GetDeclaration(
                    topology.LanesInLogicalOrder[0].Identity.Id,
                    GameplaySkinLaneResourceFieldCatalog.LongNoteHead).Value, Is.EqualTo("head-before-keys"));
                Assert.That(snapshot.GetDeclaration(
                    topology.LanesInLogicalOrder[1].Identity.Id,
                    GameplaySkinLaneResourceFieldCatalog.Key).Value, Is.EqualTo("key-before-keys"));
            });
        }

        [Test]
        public void TestDiscardedDuplicateBucketDoesNotPolluteAcceptedBucket()
        {
            IReadOnlyList<LegacyManiaSkinConfiguration> decoded = decode(
                "[Mania]\n" +
                "Keys: 2\n" +
                "NoteImage0: accepted\n" +
                "Keys: 2\n" +
                "KeyImage1: discarded\n");
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneResourceSnapshot snapshot = createSnapshot(decoded, topology);

            Assert.Multiple(() =>
            {
                Assert.That(decoded, Has.Count.EqualTo(1));
                Assert.That(snapshot.GetDeclaration(
                    topology.LanesInLogicalOrder[0].Identity.Id,
                    GameplaySkinLaneResourceFieldCatalog.Note).Value, Is.EqualTo("accepted"));
                Assert.That(snapshot.GetDeclaration(
                    topology.LanesInLogicalOrder[1].Identity.Id,
                    GameplaySkinLaneResourceFieldCatalog.Key).IsDeclared, Is.False);
                Assert.That(decoded[0].ImageLookups.Keys, Does.Not.Contain("KeyImage1"));
            });
        }

        [Test]
        public void TestManualCompatibilityDictionaryCannotForgeAcceptedProvenance()
        {
            var configuration = new LegacyManiaSkinConfiguration(2)
            {
                ImageLookups = createAllResourceValues("forged-"),
            };
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneResourceSnapshot snapshot = createSnapshot(new[] { configuration }, topology);

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Declarations, Is.Empty);

                foreach ((LegacyManiaSkinLaneResourceField legacyField, _, _) in resource_fields)
                    Assert.That(configuration.GetAcceptedLaneResource(legacyField, 0).IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestCompatibilityMutationCannotReplaceEraseOrForgeAcceptedProvenance()
        {
            Dictionary<string, string> expected = createAllResourceValues("accepted-");
            IReadOnlyList<LegacyManiaSkinConfiguration> decoded = decode(
                "[Mania]\n" +
                "Keys: 2\n" +
                createResourceLines(expected));
            LegacyManiaSkinConfiguration configuration = decoded[0];

            configuration.ImageLookups["NoteImage0"] = "replaced";
            configuration.ImageLookups.Remove("NoteImage0H");
            configuration.ImageLookups.Clear();
            configuration.ImageLookups = createAllResourceValues("reassigned-");

            foreach ((_, GameplaySkinLaneResourceField field, _) in resource_fields)
                configuration.ImageLookups[sourceKeyFor(field, 1)] = "late-added";

            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneResourceSnapshot snapshot = createSnapshot(decoded, topology);
            configuration.ImageLookups.Clear();

            Assert.Multiple(() =>
            {
                foreach ((LegacyManiaSkinLaneResourceField legacyField, GameplaySkinLaneResourceField field, string sourceKey) in resource_fields)
                {
                    Assert.That(configuration.GetAcceptedLaneResource(legacyField, 0).Value, Is.EqualTo(expected[sourceKey]));
                    Assert.That(snapshot.GetDeclaration(topology.LanesInLogicalOrder[0].Identity.Id, field).Value, Is.EqualTo(expected[sourceKey]));
                    Assert.That(snapshot.GetDeclaration(topology.LanesInLogicalOrder[1].Identity.Id, field).IsDeclared, Is.False);
                }
            });
        }

        [Test]
        public void TestInvalidFieldIndexOrNullResourceIsRejectedAtomically()
        {
            var configuration = new LegacyManiaSkinConfiguration(2);
            Dictionary<string, string> expected = createAllResourceValues("accepted-");

            foreach ((LegacyManiaSkinLaneResourceField legacyField, _, string sourceKey) in resource_fields)
                configuration.AcceptLaneResource(legacyField, 0, expected[sourceKey]);

            Assert.Multiple(() =>
            {
                Assert.That(() => configuration.AcceptLaneResource(0, 0, "invalid"), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => configuration.AcceptLaneResource(
                    LegacyManiaSkinLaneResourceField.Note | LegacyManiaSkinLaneResourceField.Key,
                    0,
                    "invalid"), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => configuration.AcceptLaneResource(LegacyManiaSkinLaneResourceField.Note, -1, "invalid"),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => configuration.AcceptLaneResource(LegacyManiaSkinLaneResourceField.KeyPressed, 2, "invalid"),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => configuration.AcceptLaneResource(LegacyManiaSkinLaneResourceField.Key, 1, null!), Throws.ArgumentNullException);
                Assert.That(() => configuration.GetAcceptedLaneResource(0, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => configuration.GetAcceptedLaneResource(
                    LegacyManiaSkinLaneResourceField.Note | LegacyManiaSkinLaneResourceField.Key, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => configuration.GetAcceptedLaneResource(LegacyManiaSkinLaneResourceField.Note, -1),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => configuration.GetAcceptedLaneResource(LegacyManiaSkinLaneResourceField.Note, 2),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(configuration.ImageLookups, Is.EquivalentTo(expected));

                foreach ((LegacyManiaSkinLaneResourceField legacyField, _, string sourceKey) in resource_fields)
                {
                    Assert.That(configuration.GetAcceptedLaneResource(legacyField, 0).Value, Is.EqualTo(expected[sourceKey]));
                    Assert.That(configuration.GetAcceptedLaneResource(legacyField, 1).IsDeclared, Is.False);
                }
            });
        }

        [Test]
        public void TestAcceptedSidecarIsInternalAndSafeStringsDoNotExposeResourceName()
        {
            const string private_resource_name = "private-resource-name-987654";
            IReadOnlyList<LegacyManiaSkinConfiguration> decoded = decode(
                "[Mania]\n" +
                "Keys: 2\n" +
                $"NoteImage0: {private_resource_name}\n");
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneResourceSnapshot snapshot = createSnapshot(decoded, topology);
            GameplaySkinLaneResourceDeclaration declaration = snapshot.Declarations.Single();
            MethodInfo? acceptMethod = typeof(LegacyManiaSkinConfiguration).GetMethod(
                "AcceptLaneResource", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo? getMethod = typeof(LegacyManiaSkinConfiguration).GetMethod(
                "GetAcceptedLaneResource", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.Multiple(() =>
            {
                Assert.That(typeof(LegacyManiaSkinLaneResourceField).IsNotPublic, Is.True);
                Assert.That(acceptMethod, Is.Not.Null);
                Assert.That(acceptMethod!.IsAssembly, Is.True);
                Assert.That(getMethod, Is.Not.Null);
                Assert.That(getMethod!.IsAssembly, Is.True);
                Assert.That(typeof(LegacyManiaSkinConfiguration).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                                                                    .Select(method => method.Name),
                    Has.None.EqualTo("AcceptLaneResource").And.None.EqualTo("GetAcceptedLaneResource"));
                Assert.That(snapshot.ToString(), Does.Not.Contain(private_resource_name));
                Assert.That(declaration.ToString(), Does.Not.Contain(private_resource_name));
                Assert.That(snapshot.GetDeclaration(declaration.LaneId, declaration.Field).ToString(), Is.EqualTo("Declared"));
                Assert.That(decoded[0].GetAcceptedLaneResource(LegacyManiaSkinLaneResourceField.Note, 0).ToString(), Is.EqualTo("Declared"));
            });
        }

        private static GameplaySkinLaneResourceSnapshot createSnapshot(
            IReadOnlyList<LegacyManiaSkinConfiguration> decoded,
            GameplaySkinLaneTopologySnapshot topology)
            => LegacyManiaGameplaySkinLaneResourceSnapshotFactory.Create(
                decoded,
                2,
                topology,
                topology.LanesInLogicalOrder.ToDictionary(lane => lane.Identity.Id, lane => lane.GlobalLogicalIndex)).Value;

        private static IReadOnlyList<LegacyManiaSkinConfiguration> decode(string skinIni)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(skinIni));
            using var reader = new LineBufferedReader(stream);
            return new LegacyManiaSkinDecoder().Decode(reader);
        }

        private static Dictionary<string, string> createAllResourceValues(string prefix)
            => resource_fields.ToDictionary(field => field.SourceKey, field => $"{prefix}{field.LegacyField}");

        private static string createResourceLines(IReadOnlyDictionary<string, string> resources)
        {
            var builder = new StringBuilder();

            foreach (KeyValuePair<string, string> resource in resources)
                builder.Append(resource.Key).Append(": ").Append(resource.Value).Append('\n');

            return builder.ToString();
        }

        private static string sourceKeyFor(GameplaySkinLaneResourceField field, int sourceColumnIndex)
        {
            string token = sourceColumnIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);

            if (ReferenceEquals(field, GameplaySkinLaneResourceFieldCatalog.Note))
                return $"NoteImage{token}";

            if (ReferenceEquals(field, GameplaySkinLaneResourceFieldCatalog.LongNoteHead))
                return $"NoteImage{token}H";

            if (ReferenceEquals(field, GameplaySkinLaneResourceFieldCatalog.LongNoteBody))
                return $"NoteImage{token}L";

            if (ReferenceEquals(field, GameplaySkinLaneResourceFieldCatalog.LongNoteTail))
                return $"NoteImage{token}T";

            if (ReferenceEquals(field, GameplaySkinLaneResourceFieldCatalog.Key))
                return $"KeyImage{token}";

            if (ReferenceEquals(field, GameplaySkinLaneResourceFieldCatalog.KeyPressed))
                return $"KeyImage{token}D";

            throw new ArgumentException("Unknown lane resource field.", nameof(field));
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
    }
}
