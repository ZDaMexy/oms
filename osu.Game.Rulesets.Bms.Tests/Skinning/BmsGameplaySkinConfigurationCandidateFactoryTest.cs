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

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    [TestFixture]
    public sealed class BmsGameplaySkinConfigurationCandidateFactoryTest
    {
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.P1, 6, 5)]
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.P2, 6, 5)]
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.Center, 6, 5)]
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.CenterRightScratch, 6, 5)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P1, 8, 7)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P2, 8, 7)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.Center, 8, 7)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.CenterRightScratch, 8, 7)]
        public void TestSinglePlayCandidateOrderAndBuckets(
            BmsKeymode keymode,
            BmsPlayfieldStyle style,
            int fullKeys,
            int keyOnlyKeys)
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(keymode, style);

            Assert.Multiple(() =>
            {
                Assert.That(plan.Candidates.Select(candidate => candidate.Source), Is.EqualTo(new[]
                {
                    BmsGameplaySkinConfigurationCandidateSource.BmsRoleOverride,
                    BmsGameplaySkinConfigurationCandidateSource.ManiaFullVisualLane,
                    BmsGameplaySkinConfigurationCandidateSource.ManiaKeyOnly,
                    BmsGameplaySkinConfigurationCandidateSource.CanonicalFallback,
                }));
                Assert.That(plan.Candidates.Select(candidate => candidate.ManiaKeys), Is.EqualTo(new int?[] { null, fullKeys, keyOnlyKeys, null }));
                Assert.That(plan.Keymode, Is.EqualTo(keymode));
                Assert.That(plan.AppliedStyle, Is.EqualTo(style));
                Assert.That(plan.Candidates[^1].Snapshot.IsDeclared, Is.False);
            });
        }

        [TestCase(BmsKeymode.Key9K_Bms)]
        [TestCase(BmsKeymode.Key9K_Pms)]
        public void TestNineKeyUsesOneFullBucketWithoutDuplicateKeyOnlyCandidate(BmsKeymode keymode)
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(keymode, BmsPlayfieldStyle.Center);

            Assert.Multiple(() =>
            {
                Assert.That(plan.Candidates.Select(candidate => candidate.Source), Is.EqualTo(new[]
                {
                    BmsGameplaySkinConfigurationCandidateSource.BmsRoleOverride,
                    BmsGameplaySkinConfigurationCandidateSource.ManiaFullVisualLane,
                    BmsGameplaySkinConfigurationCandidateSource.CanonicalFallback,
                }));
                Assert.That(plan.Candidates[1].ManiaKeys, Is.EqualTo(9));
                Assert.That(plan.Keymode, Is.EqualTo(keymode));
                Assert.That(plan.AppliedStyle, Is.EqualTo(BmsPlayfieldStyle.Center));
            });
        }

        [Test]
        public void TestFourteenKeyCandidateOrderFreezesDeckBeforeKeyOnly()
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(BmsKeymode.Key14K, BmsPlayfieldStyle.Center);

            Assert.Multiple(() =>
            {
                Assert.That(plan.Candidates.Select(candidate => candidate.Source), Is.EqualTo(new[]
                {
                    BmsGameplaySkinConfigurationCandidateSource.BmsRoleOverride,
                    BmsGameplaySkinConfigurationCandidateSource.ManiaFullVisualLane,
                    BmsGameplaySkinConfigurationCandidateSource.ManiaEightColumnDeck,
                    BmsGameplaySkinConfigurationCandidateSource.ManiaKeyOnly,
                    BmsGameplaySkinConfigurationCandidateSource.CanonicalFallback,
                }));
                Assert.That(plan.Candidates.Select(candidate => candidate.ManiaKeys), Is.EqualTo(new int?[] { null, 16, 8, 14, null }));
                Assert.That(plan.Topology.GroupsInLogicalOrder, Has.Count.EqualTo(2));
                Assert.That(plan.Topology.LanesInLogicalOrder, Has.Count.EqualTo(16));
            });
        }

        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.P1, 0)]
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.Center, 0)]
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.P2, 5)]
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.CenterRightScratch, 5)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P1, 0)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.Center, 0)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P2, 7)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.CenterRightScratch, 7)]
        public void TestFullVisualAndKeyOnlyMappingsFollowResolvedStyle(
            BmsKeymode keymode,
            BmsPlayfieldStyle style,
            int scratchVisualColumn)
        {
            int fullKeys = BmsRuleset.GetLaneCount(keymode);
            int keyOnlyKeys = fullKeys - 1;
            string maniaIni = createManiaNoteBucket(fullKeys, "full") + createManiaNoteBucket(keyOnlyKeys, "keys");
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(keymode, style, maniaIni: maniaIni);
            BmsGameplaySkinConfigurationCandidate full = candidate(plan, BmsGameplaySkinConfigurationCandidateSource.ManiaFullVisualLane);
            BmsGameplaySkinConfigurationCandidate keyOnly = candidate(plan, BmsGameplaySkinConfigurationCandidateSource.ManiaKeyOnly);
            GameplaySkinLaneId scratch = GameplaySkinLaneId.Create("bms.lane.scratch-1");
            GameplaySkinLaneId key1 = GameplaySkinLaneId.Create("bms.lane.key-1");
            int key1FullColumn = scratchVisualColumn == 0 ? 1 : 0;

            Assert.Multiple(() =>
            {
                Assert.That(resource(full, scratch, GameplaySkinLaneResourceFieldCatalog.Note).Value,
                    Is.EqualTo($"full-{scratchVisualColumn}"));
                Assert.That(resource(full, key1, GameplaySkinLaneResourceFieldCatalog.Note).Value,
                    Is.EqualTo($"full-{key1FullColumn}"));
                Assert.That(resource(keyOnly, key1, GameplaySkinLaneResourceFieldCatalog.Note).Value, Is.EqualTo("keys-0"));
                Assert.That(resource(keyOnly, scratch, GameplaySkinLaneResourceFieldCatalog.Note).IsDeclared, Is.False);
                Assert.That(plan.Topology.TryGetLane(scratch, out GameplaySkinLaneTopologyEntry? scratchLane), Is.True);
                Assert.That(scratchLane!.GlobalVisualIndex, Is.EqualTo(scratchVisualColumn));
            });
        }

        [Test]
        public void TestFourteenKeyFullDeckAndKeyOnlyMappings()
        {
            string maniaIni = createManiaNoteBucket(16, "full")
                              + createManiaNoteBucket(8, "deck")
                              + createManiaNoteBucket(14, "keys");
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                BmsKeymode.Key14K, BmsPlayfieldStyle.Center, maniaIni: maniaIni);
            BmsGameplaySkinConfigurationCandidate full = candidate(plan, BmsGameplaySkinConfigurationCandidateSource.ManiaFullVisualLane);
            BmsGameplaySkinConfigurationCandidate deck = candidate(plan, BmsGameplaySkinConfigurationCandidateSource.ManiaEightColumnDeck);
            BmsGameplaySkinConfigurationCandidate keyOnly = candidate(plan, BmsGameplaySkinConfigurationCandidateSource.ManiaKeyOnly);
            GameplaySkinLaneId scratch1 = GameplaySkinLaneId.Create("bms.lane.scratch-1");
            GameplaySkinLaneId key1 = GameplaySkinLaneId.Create("bms.lane.key-1");
            GameplaySkinLaneId key8 = GameplaySkinLaneId.Create("bms.lane.key-8");
            GameplaySkinLaneId key14 = GameplaySkinLaneId.Create("bms.lane.key-14");
            GameplaySkinLaneId scratch2 = GameplaySkinLaneId.Create("bms.lane.scratch-2");

            Assert.Multiple(() =>
            {
                Assert.That(resource(full, scratch1, GameplaySkinLaneResourceFieldCatalog.Note).Value, Is.EqualTo("full-0"));
                Assert.That(resource(full, key1, GameplaySkinLaneResourceFieldCatalog.Note).Value, Is.EqualTo("full-1"));
                Assert.That(resource(full, key8, GameplaySkinLaneResourceFieldCatalog.Note).Value, Is.EqualTo("full-8"));
                Assert.That(resource(full, scratch2, GameplaySkinLaneResourceFieldCatalog.Note).Value, Is.EqualTo("full-15"));

                Assert.That(resource(deck, scratch1, GameplaySkinLaneResourceFieldCatalog.Note).Value, Is.EqualTo("deck-0"));
                Assert.That(resource(deck, key1, GameplaySkinLaneResourceFieldCatalog.Note).Value, Is.EqualTo("deck-1"));
                Assert.That(resource(deck, key8, GameplaySkinLaneResourceFieldCatalog.Note).Value, Is.EqualTo("deck-0"));
                Assert.That(resource(deck, scratch2, GameplaySkinLaneResourceFieldCatalog.Note).Value, Is.EqualTo("deck-7"));

                Assert.That(resource(keyOnly, scratch1, GameplaySkinLaneResourceFieldCatalog.Note).IsDeclared, Is.False);
                Assert.That(resource(keyOnly, key1, GameplaySkinLaneResourceFieldCatalog.Note).Value, Is.EqualTo("keys-0"));
                Assert.That(resource(keyOnly, key8, GameplaySkinLaneResourceFieldCatalog.Note).Value, Is.EqualTo("keys-7"));
                Assert.That(resource(keyOnly, key14, GameplaySkinLaneResourceFieldCatalog.Note).Value, Is.EqualTo("keys-13"));
                Assert.That(resource(keyOnly, scratch2, GameplaySkinLaneResourceFieldCatalog.Note).IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestBmsRoleOverrideProjectsAllSixFieldsAndScratchTokens()
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                BmsKeymode.Key14K,
                BmsPlayfieldStyle.Center,
                bmsIni:
                    "[Bms]\n" +
                    "Keymode: 14K\n" +
                    "NoteImageS: scratch-one\n" +
                    "NoteImage1: note\n" +
                    "NoteImage1H: head\n" +
                    "NoteImage1L: body\n" +
                    "NoteImage1T: tail\n" +
                    "KeyImage1: key\n" +
                    "KeyImage1D: key-down\n" +
                    "NoteImageS2: scratch-two\n");
            BmsGameplaySkinConfigurationCandidate bms = candidate(plan, BmsGameplaySkinConfigurationCandidateSource.BmsRoleOverride);
            GameplaySkinLaneId key1 = GameplaySkinLaneId.Create("bms.lane.key-1");

            Assert.Multiple(() =>
            {
                Assert.That(resource(bms, GameplaySkinLaneId.Create("bms.lane.scratch-1"), GameplaySkinLaneResourceFieldCatalog.Note).Value,
                    Is.EqualTo("scratch-one"));
                Assert.That(resource(bms, GameplaySkinLaneId.Create("bms.lane.scratch-2"), GameplaySkinLaneResourceFieldCatalog.Note).Value,
                    Is.EqualTo("scratch-two"));
                Assert.That(resource(bms, key1, GameplaySkinLaneResourceFieldCatalog.Note).Value, Is.EqualTo("note"));
                Assert.That(resource(bms, key1, GameplaySkinLaneResourceFieldCatalog.LongNoteHead).Value, Is.EqualTo("head"));
                Assert.That(resource(bms, key1, GameplaySkinLaneResourceFieldCatalog.LongNoteBody).Value, Is.EqualTo("body"));
                Assert.That(resource(bms, key1, GameplaySkinLaneResourceFieldCatalog.LongNoteTail).Value, Is.EqualTo("tail"));
                Assert.That(resource(bms, key1, GameplaySkinLaneResourceFieldCatalog.Key).Value, Is.EqualTo("key"));
                Assert.That(resource(bms, key1, GameplaySkinLaneResourceFieldCatalog.KeyPressed).Value, Is.EqualTo("key-down"));
            });
        }

        [TestCase(BmsKeymode.Key9K_Bms)]
        [TestCase(BmsKeymode.Key9K_Pms)]
        public void TestCurrentUnversionedNineKeyBmsInputUsesLegacyZeroBasedTokens(BmsKeymode keymode)
        {
            string keymodeToken = keymode == BmsKeymode.Key9K_Bms ? "9K_BMS" : "9K_PMS";
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                keymode,
                BmsPlayfieldStyle.Center,
                bmsIni:
                    "[Bms]\n" +
                    $"Keymode: {keymodeToken}\n" +
                    "NoteImage0: first\n" +
                    "NoteImage8: ninth\n" +
                    "NoteImage9: canonical-future-token\n");
            BmsGameplaySkinConfigurationCandidate bms = candidate(plan, BmsGameplaySkinConfigurationCandidateSource.BmsRoleOverride);

            Assert.Multiple(() =>
            {
                Assert.That(resource(bms, GameplaySkinLaneId.Create("bms.lane.key-1"), GameplaySkinLaneResourceFieldCatalog.Note).Value,
                    Is.EqualTo("first"));
                Assert.That(resource(bms, GameplaySkinLaneId.Create("bms.lane.key-9"), GameplaySkinLaneResourceFieldCatalog.Note).Value,
                    Is.EqualTo("ninth"));
                Assert.That(bms.Snapshot.Value.Declarations.Select(declaration => declaration.ResourceName),
                    Does.Not.Contain("canonical-future-token"));
            });
        }

        [Test]
        public void TestDeclaredEmptyBucketsAndFieldsDoNotEraseLaterCandidates()
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                BmsKeymode.Key7K,
                BmsPlayfieldStyle.P1,
                bmsIni:
                    "[Bms]\n" +
                    "Keymode: 7K\n" +
                    "NoteImage1:\n",
                maniaIni:
                    "[Mania]\n" +
                    "Keys: 8\n" +
                    "KeyImage1: full-key\n");
            BmsGameplaySkinConfigurationCandidate bms = candidate(plan, BmsGameplaySkinConfigurationCandidateSource.BmsRoleOverride);
            BmsGameplaySkinConfigurationCandidate full = candidate(plan, BmsGameplaySkinConfigurationCandidateSource.ManiaFullVisualLane);
            BmsGameplaySkinConfigurationCandidate keyOnly = candidate(plan, BmsGameplaySkinConfigurationCandidateSource.ManiaKeyOnly);
            GameplaySkinLaneId key1 = GameplaySkinLaneId.Create("bms.lane.key-1");

            Assert.Multiple(() =>
            {
                Assert.That(bms.Snapshot.IsDeclared, Is.True);
                Assert.That(resource(bms, key1, GameplaySkinLaneResourceFieldCatalog.Note).IsDeclared, Is.True);
                Assert.That(resource(bms, key1, GameplaySkinLaneResourceFieldCatalog.Note).Value, Is.Empty);
                Assert.That(resource(bms, key1, GameplaySkinLaneResourceFieldCatalog.Key).IsDeclared, Is.False);
                Assert.That(full.Snapshot.IsDeclared, Is.True);
                Assert.That(resource(full, key1, GameplaySkinLaneResourceFieldCatalog.Key).Value, Is.EqualTo("full-key"));
                Assert.That(keyOnly.Snapshot.IsDeclared, Is.False);
                Assert.That(plan.Candidates[^1].Source, Is.EqualTo(BmsGameplaySkinConfigurationCandidateSource.CanonicalFallback));
            });
        }

        [Test]
        public void TestMissingBucketAndExplicitEmptyBucketRemainDistinctPerLayer()
        {
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                BmsKeymode.Key7K,
                BmsPlayfieldStyle.P1,
                bmsIni: "[Bms]\nKeymode: 7K\n",
                maniaIni: "[Mania]\nKeys: 8\n");

            Assert.Multiple(() =>
            {
                Assert.That(candidate(plan, BmsGameplaySkinConfigurationCandidateSource.BmsRoleOverride).Snapshot.IsDeclared, Is.True);
                Assert.That(candidate(plan, BmsGameplaySkinConfigurationCandidateSource.BmsRoleOverride).Snapshot.Value.Declarations, Is.Empty);
                Assert.That(candidate(plan, BmsGameplaySkinConfigurationCandidateSource.ManiaFullVisualLane).Snapshot.IsDeclared, Is.True);
                Assert.That(candidate(plan, BmsGameplaySkinConfigurationCandidateSource.ManiaFullVisualLane).Snapshot.Value.Declarations, Is.Empty);
                Assert.That(candidate(plan, BmsGameplaySkinConfigurationCandidateSource.ManiaKeyOnly).Snapshot.IsDeclared, Is.False);
                Assert.That(candidate(plan, BmsGameplaySkinConfigurationCandidateSource.CanonicalFallback).Snapshot.IsDeclared, Is.False);
            });
        }

        [Test]
        public void TestCandidateSnapshotsDetachFromMutableDecoderOutputs()
        {
            BmsSkinConfiguration bmsConfiguration = decodeBms("[Bms]\nKeymode: 7K\nNoteImage1: original-bms\n").Single();
            LegacyManiaSkinConfiguration maniaConfiguration = decodeMania("[Mania]\nKeys: 8\nNoteImage1: original-mania\n").Single();
            BmsGameplaySkinConfigurationCandidatePlan plan = BmsGameplaySkinConfigurationCandidateFactory.Create(
                BmsLaneLayout.CreateForKeymode(BmsKeymode.Key7K),
                new[] { bmsConfiguration },
                new[] { maniaConfiguration });
            GameplaySkinLaneId key1 = GameplaySkinLaneId.Create("bms.lane.key-1");

            bmsConfiguration.ImageLookups["NoteImage1"] = "mutated-bms";
            maniaConfiguration.ImageLookups["NoteImage1"] = "mutated-mania";

            Assert.Multiple(() =>
            {
                Assert.That(resource(candidate(plan, BmsGameplaySkinConfigurationCandidateSource.BmsRoleOverride),
                    key1, GameplaySkinLaneResourceFieldCatalog.Note).Value, Is.EqualTo("original-bms"));
                Assert.That(resource(candidate(plan, BmsGameplaySkinConfigurationCandidateSource.ManiaFullVisualLane),
                    key1, GameplaySkinLaneResourceFieldCatalog.Note).Value, Is.EqualTo("original-mania"));
            });
        }

        [Test]
        public void TestInvalidFactoryInputsFailClosed()
        {
            BmsLaneLayout layout = BmsLaneLayout.CreateForKeymode(BmsKeymode.Key7K);
            var duplicateBms = new[]
            {
                new BmsSkinConfiguration(BmsKeymode.Key7K),
                new BmsSkinConfiguration(BmsKeymode.Key7K),
            };
            var duplicateMania = new[]
            {
                new LegacyManiaSkinConfiguration(8),
                new LegacyManiaSkinConfiguration(8),
            };

            Assert.Multiple(() =>
            {
                Assert.That(() => BmsGameplaySkinConfigurationCandidateFactory.Create(null!, Array.Empty<BmsSkinConfiguration>(), Array.Empty<LegacyManiaSkinConfiguration>()), Throws.ArgumentNullException);
                Assert.That(() => BmsGameplaySkinConfigurationCandidateFactory.Create(layout, null!, Array.Empty<LegacyManiaSkinConfiguration>()), Throws.ArgumentNullException);
                Assert.That(() => BmsGameplaySkinConfigurationCandidateFactory.Create(layout, Array.Empty<BmsSkinConfiguration>(), null!), Throws.ArgumentNullException);
                Assert.That(() => BmsGameplaySkinConfigurationCandidateFactory.Create(layout, duplicateBms, Array.Empty<LegacyManiaSkinConfiguration>()), Throws.ArgumentException);
                Assert.That(() => BmsGameplaySkinConfigurationCandidateFactory.Create(layout, Array.Empty<BmsSkinConfiguration>(), duplicateMania), Throws.ArgumentException);
                Assert.That(() => BmsGameplaySkinConfigurationCandidateFactory.Create(
                    BmsLaneLayout.CreateForKeymode(BmsKeymode.Key7K, minimumLaneCount: 9),
                    Array.Empty<BmsSkinConfiguration>(), Array.Empty<LegacyManiaSkinConfiguration>()), Throws.ArgumentException);
                Assert.That(() => BmsGameplaySkinConfigurationCandidateFactory.Create(
                    BmsLaneLayout.CreateForKeymode((BmsKeymode)99),
                    Array.Empty<BmsSkinConfiguration>(), Array.Empty<LegacyManiaSkinConfiguration>()), Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void TestCandidateAndPlanRejectContradictoryConstruction()
        {
            BmsGameplaySkinConfigurationCandidatePlan valid = createPlan(BmsKeymode.Key7K, BmsPlayfieldStyle.P1);
            BmsGameplaySkinConfigurationCandidatePlan declared = createPlan(
                BmsKeymode.Key7K, BmsPlayfieldStyle.P1, bmsIni: "[Bms]\nKeymode: 7K\n");
            BmsGameplaySkinConfigurationCandidatePlan alternateTopology = createPlan(BmsKeymode.Key7K, BmsPlayfieldStyle.P2);
            BmsGameplaySkinConfigurationCandidatePlan nineKey = createPlan(BmsKeymode.Key9K_Bms, BmsPlayfieldStyle.Center);
            BmsGameplaySkinConfigurationCandidate canonical = valid.Candidates[^1];
            BmsGameplaySkinConfigurationCandidate bms = valid.Candidates[0];

            Assert.Multiple(() =>
            {
                Assert.That(() => new BmsGameplaySkinConfigurationCandidate(
                    BmsGameplaySkinConfigurationCandidateSource.ManiaFullVisualLane, null,
                    GameplaySkinConfigurationDeclaration<GameplaySkinLaneResourceSnapshot>.Absent), Throws.ArgumentException);
                Assert.That(() => new BmsGameplaySkinConfigurationCandidate(
                    BmsGameplaySkinConfigurationCandidateSource.BmsRoleOverride, 8,
                    GameplaySkinConfigurationDeclaration<GameplaySkinLaneResourceSnapshot>.Absent), Throws.ArgumentException);
                Assert.That(() => new BmsGameplaySkinConfigurationCandidate(
                    BmsGameplaySkinConfigurationCandidateSource.ManiaKeyOnly, 0,
                    GameplaySkinConfigurationDeclaration<GameplaySkinLaneResourceSnapshot>.Absent), Throws.ArgumentException);
                Assert.That(() => new BmsGameplaySkinConfigurationCandidate(
                    (BmsGameplaySkinConfigurationCandidateSource)99, null,
                    GameplaySkinConfigurationDeclaration<GameplaySkinLaneResourceSnapshot>.Absent), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => new BmsGameplaySkinConfigurationCandidatePlan(
                    valid.Keymode, valid.AppliedStyle, valid.Topology, new[] { canonical, bms, canonical }), Throws.ArgumentException);
                Assert.That(() => new BmsGameplaySkinConfigurationCandidatePlan(
                    declared.Keymode, declared.AppliedStyle, alternateTopology.Topology, declared.Candidates.ToArray()), Throws.ArgumentException);
                Assert.That(() => new BmsGameplaySkinConfigurationCandidatePlan(
                    nineKey.Keymode, BmsPlayfieldStyle.P1, nineKey.Topology, nineKey.Candidates.ToArray()), Throws.ArgumentException);
                Assert.That(() => new BmsGameplaySkinConfigurationCandidatePlan(
                    (BmsKeymode)99, BmsPlayfieldStyle.Center, valid.Topology, valid.Candidates.ToArray()), Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void TestCompatibilityTypesRemainInternalAndSafeStringsHideValues()
        {
            const string private_value = "private/package/resource";
            BmsGameplaySkinConfigurationCandidatePlan plan = createPlan(
                BmsKeymode.Key7K,
                BmsPlayfieldStyle.P1,
                bmsIni: $"[Bms]\nKeymode: 7K\nNoteImage1: {private_value}\n");
            BmsGameplaySkinConfigurationCandidate bms = candidate(plan, BmsGameplaySkinConfigurationCandidateSource.BmsRoleOverride);

            Assert.Multiple(() =>
            {
                Assert.That(typeof(BmsGameplaySkinConfigurationCandidateFactory).IsNotPublic, Is.True);
                Assert.That(typeof(BmsGameplaySkinConfigurationCandidatePlan).IsNotPublic, Is.True);
                Assert.That(typeof(BmsGameplaySkinConfigurationCandidate).IsNotPublic, Is.True);
                Assert.That(typeof(BmsGameplaySkinConfigurationCandidateSource).IsNotPublic, Is.True);
                Assert.That(bms.ToString(), Does.Not.Contain(private_value));
                Assert.That(bms.ToString(), Does.Contain("BmsRoleOverride").And.Contain("Declared"));
                Assert.That(plan.Candidates[^1].ToString(), Does.Contain("CanonicalFallback").And.Contain("Absent"));
            });
        }

        private static BmsGameplaySkinConfigurationCandidatePlan createPlan(
            BmsKeymode keymode,
            BmsPlayfieldStyle style,
            string bmsIni = "",
            string maniaIni = "")
        {
            return BmsGameplaySkinConfigurationCandidateFactory.Create(
                BmsLaneLayout.CreateForKeymode(keymode, style: style),
                decodeBms(bmsIni),
                decodeMania(maniaIni));
        }

        private static BmsGameplaySkinConfigurationCandidate candidate(
            BmsGameplaySkinConfigurationCandidatePlan plan,
            BmsGameplaySkinConfigurationCandidateSource source)
            => plan.Candidates.Single(candidate => candidate.Source == source);

        private static GameplaySkinConfigurationDeclaration<string> resource(
            BmsGameplaySkinConfigurationCandidate candidate,
            GameplaySkinLaneId laneId,
            GameplaySkinLaneResourceField field)
        {
            Assert.That(candidate.Snapshot.IsDeclared, Is.True, $"Candidate {candidate.Source} did not contain a declared source bucket.");
            return candidate.Snapshot.Value.GetDeclaration(laneId, field);
        }

        private static string createManiaNoteBucket(int keys, string prefix)
        {
            var builder = new StringBuilder();
            builder.AppendLine("[Mania]");
            builder.AppendLine($"Keys: {keys}");

            for (int column = 0; column < keys; column++)
                builder.AppendLine($"NoteImage{column}: {prefix}-{column}");

            return builder.ToString();
        }

        private static IReadOnlyList<BmsSkinConfiguration> decodeBms(string skinIni)
        {
            var decoder = new BmsSkinDecoder();
            decoder.Parse(skinIni);
            return decoder.Configurations;
        }

        private static IReadOnlyList<LegacyManiaSkinConfiguration> decodeMania(string skinIni)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(skinIni));
            using var reader = new LineBufferedReader(stream);
            return new LegacyManiaSkinDecoder().Decode(reader);
        }
    }
}
