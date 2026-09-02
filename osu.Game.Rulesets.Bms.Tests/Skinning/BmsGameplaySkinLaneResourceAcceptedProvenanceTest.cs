// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    [TestFixture]
    public sealed class BmsGameplaySkinLaneResourceAcceptedProvenanceTest
    {
        private static readonly (GameplaySkinLaneResourceField Field, string Prefix, string Suffix)[] compatibility_fields =
        {
            (GameplaySkinLaneResourceFieldCatalog.Note, "NoteImage", string.Empty),
            (GameplaySkinLaneResourceFieldCatalog.LongNoteHead, "NoteImage", "H"),
            (GameplaySkinLaneResourceFieldCatalog.LongNoteBody, "NoteImage", "L"),
            (GameplaySkinLaneResourceFieldCatalog.LongNoteTail, "NoteImage", "T"),
            (GameplaySkinLaneResourceFieldCatalog.Key, "KeyImage", string.Empty),
            (GameplaySkinLaneResourceFieldCatalog.KeyPressed, "KeyImage", "D"),
        };

        private static readonly GameplaySkinLaneResourceField[] hosted_fields =
        {
            GameplaySkinLaneResourceFieldCatalog.Note,
            GameplaySkinLaneResourceFieldCatalog.LongNoteHead,
            GameplaySkinLaneResourceFieldCatalog.LongNoteBody,
            GameplaySkinLaneResourceFieldCatalog.LongNoteTail,
        };

        [Test]
        public void TestAllSixDecoderFieldsRemainCompatibleButOnlyFourHostedNoteFieldsEnterSnapshot()
        {
            var builder = new StringBuilder();
            builder.AppendLine("[Bms]");
            builder.AppendLine("Keymode: 14K");

            string[] laneTokens = { "14", "S", "S2" };
            var expected = new Dictionary<(string Token, GameplaySkinLaneResourceField Field), string>();

            foreach (string laneToken in laneTokens)
            {
                foreach ((GameplaySkinLaneResourceField field, string prefix, string suffix) in compatibility_fields)
                {
                    string resourceName = $"resource-{laneToken}-{field.Id}";
                    builder.AppendLine($"{sourceKey(prefix, suffix, laneToken)}: {resourceName}");
                    expected.Add((laneToken, field), resourceName);
                }
            }

            BmsSkinConfiguration configuration = decode(builder.ToString()).Single();
            GameplaySkinLaneResourceSnapshot snapshot = createSnapshot(BmsKeymode.Key14K, configuration);

            Assert.Multiple(() =>
            {
                Assert.That(configuration.ImageLookups, Has.Count.EqualTo(expected.Count));
                Assert.That(snapshot.Declarations, Has.Count.EqualTo(laneTokens.Length * hosted_fields.Length));

                foreach (string laneToken in laneTokens)
                {
                    GameplaySkinLaneId laneId = laneToken switch
                    {
                        "14" => lane("bms.lane.key-14"),
                        "S" => lane("bms.lane.scratch-1"),
                        "S2" => lane("bms.lane.scratch-2"),
                        _ => throw new InvalidOperationException(),
                    };

                    foreach ((GameplaySkinLaneResourceField field, string prefix, string suffix) in compatibility_fields)
                    {
                        string resourceName = expected[(laneToken, field)];
                        string lookupKey = sourceKey(prefix, suffix, laneToken);

                        Assert.That(configuration.ImageLookups[lookupKey], Is.EqualTo(resourceName), lookupKey);
                        assertDeclared(configuration.GetAcceptedLaneResource(field, laneToken), resourceName, $"accepted {lookupKey}");

                        if (hosted_fields.Contains(field))
                            assertDeclared(snapshot.GetDeclaration(laneId, field), resourceName, $"projected {lookupKey}");
                        else
                            Assert.That(snapshot.GetDeclaration(laneId, field).IsDeclared, Is.False, $"unhosted {lookupKey}");
                    }
                }
            });
        }

        [Test]
        public void TestPendingDeclarationsExplicitEmptyAndDuplicateLastAreAccepted()
        {
            BmsSkinConfiguration configuration = decode(
                "[Bms]\n" +
                "NoteImage1H: pending-head\n" +
                "NoteImage1: pending-note\n" +
                "KeyImage1: pending-key\n" +
                "KeyImage1D: pending-key-down\n" +
                "Keymode: 7K\n" +
                "KeyImage1: final-key\n" +
                "NoteImage1: after-keymode\n" +
                "NoteImage1:\n" +
                "[Bms]\n" +
                "NoteImage1: duplicate-section-pending\n" +
                "Keymode: 7K\n" +
                "KeyImage1: merged-final-key\n" +
                "NoteImage1:\n").Single();
            GameplaySkinLaneResourceSnapshot snapshot = createSnapshot(BmsKeymode.Key7K, configuration);
            GameplaySkinLaneId key1 = lane("bms.lane.key-1");

            Assert.Multiple(() =>
            {
                Assert.That(configuration.ImageLookups["NoteImage1"], Is.Empty);
                Assert.That(configuration.ImageLookups["NoteImage1H"], Is.EqualTo("pending-head"));
                Assert.That(configuration.ImageLookups["KeyImage1"], Is.EqualTo("merged-final-key"));
                Assert.That(configuration.ImageLookups["KeyImage1D"], Is.EqualTo("pending-key-down"));
                assertDeclared(snapshot.GetDeclaration(key1, GameplaySkinLaneResourceFieldCatalog.Note), string.Empty, "explicit empty note");
                assertDeclared(snapshot.GetDeclaration(key1, GameplaySkinLaneResourceFieldCatalog.LongNoteHead), "pending-head", "pending head");
                assertDeclared(configuration.GetAcceptedLaneResource(GameplaySkinLaneResourceFieldCatalog.Key, "1"), "merged-final-key", "decoder compatibility key");
                assertDeclared(configuration.GetAcceptedLaneResource(GameplaySkinLaneResourceFieldCatalog.KeyPressed, "1"), "pending-key-down", "decoder compatibility pressed key");
                Assert.That(snapshot.GetDeclaration(key1, GameplaySkinLaneResourceFieldCatalog.Key).IsDeclared, Is.False);
                Assert.That(snapshot.GetDeclaration(key1, GameplaySkinLaneResourceFieldCatalog.KeyPressed).IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestCompatibilityOnlySuffixesAndLaneVisualKeysDoNotEnterSnapshot()
        {
            string[] compatibilityOnlyKeys =
            {
                "NoteImage1D",
                "KeyImage1H",
                "KeyImage1L",
                "KeyImage1T",
                "LaneBackgroundImage1",
                "LaneBackgroundImage1H",
                "LaneDividerImageS",
                "LaneDividerImageS2D",
            };
            var builder = new StringBuilder("[Bms]\nKeymode: 14K\n");

            foreach (string key in compatibilityOnlyKeys)
                builder.AppendLine($"{key}: compatibility-{key}");

            BmsSkinConfiguration configuration = decode(builder.ToString()).Single();
            GameplaySkinLaneResourceSnapshot snapshot = createSnapshot(BmsKeymode.Key14K, configuration);

            Assert.Multiple(() =>
            {
                Assert.That(configuration.ImageLookups, Has.Count.EqualTo(compatibilityOnlyKeys.Length));

                foreach (string key in compatibilityOnlyKeys)
                    Assert.That(configuration.ImageLookups[key], Is.EqualTo($"compatibility-{key}"), key);

                Assert.That(snapshot.Declarations, Is.Empty);

                foreach (string laneToken in new[] { "1", "S", "S2" })
                {
                    foreach ((GameplaySkinLaneResourceField field, _, _) in compatibility_fields)
                        Assert.That(configuration.GetAcceptedLaneResource(field, laneToken).IsDeclared, Is.False, $"{field.Id} at {laneToken}");
                }
            });
        }

        [Test]
        public void TestCaseLookalikesAndTokenlessKeysDoNotBecomeLaneDeclarations()
        {
            const string ini =
                "[Bms]\n" +
                "Keymode: 7K\n" +
                "NoteImage: tokenless-note\n" +
                "KeyImage: tokenless-key\n" +
                "LaneBackgroundImage: tokenless-background\n" +
                "LaneDividerImage: tokenless-divider\n" +
                "noteImage1: lower-prefix\n" +
                "NOTEIMAGE1: upper-prefix\n" +
                "Noteimage1: mixed-prefix\n" +
                "NoteImage1h: lower-suffix\n" +
                "KeyImage1d: lower-key-suffix\n" +
                "NoteImageS1: scratch-lookalike\n" +
                "NoteImageSS: scratch-lookalike-two\n" +
                "NoteImage1HH: doubled-suffix\n" +
                "NoteImage-1: signed-token\n";

            BmsSkinConfiguration configuration = decode(ini).Single();
            GameplaySkinLaneResourceSnapshot snapshot = createSnapshot(BmsKeymode.Key7K, configuration);

            Assert.Multiple(() =>
            {
                Assert.That(configuration.ImageLookups, Has.Count.EqualTo(4));
                Assert.That(configuration.ImageLookups["NoteImage"], Is.EqualTo("tokenless-note"));
                Assert.That(configuration.ImageLookups["KeyImage"], Is.EqualTo("tokenless-key"));
                Assert.That(configuration.ImageLookups["LaneBackgroundImage"], Is.EqualTo("tokenless-background"));
                Assert.That(configuration.ImageLookups["LaneDividerImage"], Is.EqualTo("tokenless-divider"));
                Assert.That(snapshot.Declarations, Is.Empty);
            });
        }

        [Test]
        public void TestManualCompatibilityDictionaryCannotForgeAcceptedDeclarations()
        {
            var configuration = new BmsSkinConfiguration(BmsKeymode.Key7K);

            foreach ((_, string prefix, string suffix) in compatibility_fields)
                configuration.ImageLookups[sourceKey(prefix, suffix, "1")] = $"manual-{prefix}-{suffix}";

            GameplaySkinLaneResourceSnapshot snapshot = createSnapshot(BmsKeymode.Key7K, configuration);

            Assert.Multiple(() =>
            {
                Assert.That(configuration.ImageLookups, Has.Count.EqualTo(compatibility_fields.Length));
                Assert.That(snapshot.Declarations, Is.Empty);

                foreach ((GameplaySkinLaneResourceField field, _, _) in compatibility_fields)
                    Assert.That(configuration.GetAcceptedLaneResource(field, "1").IsDeclared, Is.False, field.Id);
            });
        }

        [TestCase(CompatibilityMutation.Overwrite)]
        [TestCase(CompatibilityMutation.Remove)]
        [TestCase(CompatibilityMutation.Clear)]
        [TestCase(CompatibilityMutation.LateAdd)]
        public void TestCompatibilityDictionaryMutationCannotAlterAcceptedProvenance(CompatibilityMutation mutation)
        {
            var builder = new StringBuilder("[Bms]\nKeymode: 7K\n");

            foreach ((GameplaySkinLaneResourceField field, string prefix, string suffix) in compatibility_fields)
                builder.AppendLine($"{sourceKey(prefix, suffix, "1")}: original-{field.Id}");

            BmsSkinConfiguration configuration = decode(builder.ToString()).Single();

            switch (mutation)
            {
                case CompatibilityMutation.Overwrite:
                    configuration.ImageLookups["NoteImage1"] = "overwritten";
                    break;

                case CompatibilityMutation.Remove:
                    configuration.ImageLookups.Remove("NoteImage1H");
                    break;

                case CompatibilityMutation.Clear:
                    configuration.ImageLookups.Clear();
                    break;

                case CompatibilityMutation.LateAdd:
                    configuration.ImageLookups["KeyImage2"] = "late-added";
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
            }

            GameplaySkinLaneResourceSnapshot snapshot = createSnapshot(BmsKeymode.Key7K, configuration);
            GameplaySkinLaneId key1 = lane("bms.lane.key-1");
            GameplaySkinLaneId key2 = lane("bms.lane.key-2");

            Assert.Multiple(() =>
            {
                foreach ((GameplaySkinLaneResourceField field, _, _) in compatibility_fields)
                {
                    assertDeclared(configuration.GetAcceptedLaneResource(field, "1"), $"original-{field.Id}", $"accepted {field.Id}");

                    if (hosted_fields.Contains(field))
                        assertDeclared(snapshot.GetDeclaration(key1, field), $"original-{field.Id}", $"projected {field.Id}");
                    else
                        Assert.That(snapshot.GetDeclaration(key1, field).IsDeclared, Is.False, $"unhosted {field.Id}");
                }

                Assert.That(snapshot.GetDeclaration(key2, GameplaySkinLaneResourceFieldCatalog.Key).IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestNineKeyProjectionUsesRawZeroThroughEightWithoutNormalisingOtherTokens()
        {
            BmsSkinConfiguration configuration = decode(
                "[Bms]\n" +
                "Keymode: 9K\n" +
                "NoteImage0: raw-zero\n" +
                "NoteImage8: raw-eight\n" +
                "NoteImage9: raw-nine\n" +
                "NoteImage01: raw-leading-zero\n" +
                "NoteImage１: raw-fullwidth-one\n").Single();
            GameplaySkinLaneResourceSnapshot snapshot = createSnapshot(BmsKeymode.Key9K_Bms, configuration);

            Assert.Multiple(() =>
            {
                Assert.That(configuration.ImageLookups, Has.Count.EqualTo(5));
                assertDeclared(configuration.GetAcceptedLaneResource(GameplaySkinLaneResourceFieldCatalog.Note, "0"), "raw-zero", "raw token 0");
                assertDeclared(configuration.GetAcceptedLaneResource(GameplaySkinLaneResourceFieldCatalog.Note, "8"), "raw-eight", "raw token 8");
                assertDeclared(configuration.GetAcceptedLaneResource(GameplaySkinLaneResourceFieldCatalog.Note, "9"), "raw-nine", "raw token 9");
                assertDeclared(configuration.GetAcceptedLaneResource(GameplaySkinLaneResourceFieldCatalog.Note, "01"), "raw-leading-zero", "raw token 01");
                assertDeclared(configuration.GetAcceptedLaneResource(GameplaySkinLaneResourceFieldCatalog.Note, "１"), "raw-fullwidth-one", "raw fullwidth token 1");
                Assert.That(configuration.GetAcceptedLaneResource(GameplaySkinLaneResourceFieldCatalog.Note, "1").IsDeclared, Is.False);

                Assert.That(snapshot.Declarations, Has.Count.EqualTo(2));
                assertDeclared(snapshot.GetDeclaration(lane("bms.lane.key-1"), GameplaySkinLaneResourceFieldCatalog.Note), "raw-zero", "9K key 1");
                assertDeclared(snapshot.GetDeclaration(lane("bms.lane.key-9"), GameplaySkinLaneResourceFieldCatalog.Note), "raw-eight", "9K key 9");
                Assert.That(snapshot.GetDeclaration(lane("bms.lane.key-2"), GameplaySkinLaneResourceFieldCatalog.Note).IsDeclared, Is.False);
                Assert.That(snapshot.Declarations.Select(declaration => declaration.ResourceName),
                    Is.EquivalentTo(new[] { "raw-zero", "raw-eight" }));
            });
        }

        [Test]
        public void TestInvalidFieldTokenAndNullArgumentsAreRejectedAtomically()
        {
            var configuration = new BmsSkinConfiguration(BmsKeymode.Key7K);
            GameplaySkinLaneResourceField note = GameplaySkinLaneResourceFieldCatalog.Note;
            var nonCanonicalField = new GameplaySkinLaneResourceField(note.Id, note.Slot);

            configuration.AcceptLaneResource(note, "1", "original");

            Assert.Multiple(() =>
            {
                Assert.That(() => configuration.AcceptLaneResource(null!, "2", "bad"), Throws.ArgumentNullException);
                Assert.That(() => configuration.AcceptLaneResource(nonCanonicalField, "2", "bad"), Throws.ArgumentException);
                Assert.That(() => configuration.AcceptLaneResource(note, "2", null!), Throws.ArgumentNullException);
                Assert.That(() => configuration.AcceptLaneResource(note, null!, "bad"), Throws.ArgumentNullException);

                foreach (string invalidToken in new[] { string.Empty, " ", "S1", "s", "1a", "-1" })
                    Assert.That(() => configuration.AcceptLaneResource(note, invalidToken, "bad"), Throws.ArgumentException, invalidToken);

                Assert.That(() => configuration.GetAcceptedLaneResource(null!, "1"), Throws.ArgumentNullException);
                Assert.That(() => configuration.GetAcceptedLaneResource(nonCanonicalField, "1"), Throws.ArgumentException);
                Assert.That(() => configuration.GetAcceptedLaneResource(note, "S1"), Throws.ArgumentException);
            });

            Assert.Multiple(() =>
            {
                Assert.That(configuration.ImageLookups, Has.Count.EqualTo(1));
                Assert.That(configuration.ImageLookups["NoteImage1"], Is.EqualTo("original"));
                assertDeclared(configuration.GetAcceptedLaneResource(note, "1"), "original", "original accepted declaration");
                Assert.That(configuration.GetAcceptedLaneResource(note, "2").IsDeclared, Is.False);
                Assert.That(configuration.GetAcceptedLaneResource(GameplaySkinLaneResourceFieldCatalog.Key, "1").IsDeclared, Is.False);
            });
        }

        private static IReadOnlyList<BmsSkinConfiguration> decode(string skinIni)
        {
            var decoder = new BmsSkinDecoder();
            decoder.Parse(skinIni);
            return decoder.Configurations;
        }

        private static GameplaySkinLaneResourceSnapshot createSnapshot(BmsKeymode keymode, BmsSkinConfiguration configuration)
        {
            BmsGameplaySkinLaneTopologyProjection projection = BmsGameplaySkinLaneTopologyFactory.Create(BmsLaneLayout.CreateForKeymode(keymode));
            GameplaySkinConfigurationDeclaration<GameplaySkinLaneResourceSnapshot> declaration =
                BmsGameplaySkinLaneResourceSnapshotFactory.Create(new[] { configuration }, projection);

            Assert.That(declaration.IsDeclared, Is.True);
            return declaration.Value;
        }

        private static GameplaySkinLaneId lane(string id) => GameplaySkinLaneId.Create(id);

        private static string sourceKey(string prefix, string suffix, string laneToken) => $"{prefix}{laneToken}{suffix}";

        private static void assertDeclared(
            GameplaySkinConfigurationDeclaration<string> declaration,
            string expected,
            string message)
        {
            Assert.That(declaration.IsDeclared, Is.True, message);
            Assert.That(declaration.Value, Is.EqualTo(expected), message);
        }

        public enum CompatibilityMutation
        {
            Overwrite,
            Remove,
            Clear,
            LateAdd,
        }
    }
}
