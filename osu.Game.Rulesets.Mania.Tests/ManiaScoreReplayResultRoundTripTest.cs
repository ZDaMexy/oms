// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.IO.Legacy;
using osu.Game.Replays;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Tests.Beatmaps;
using SharpCompress.Compressors.LZMA;

namespace osu.Game.Rulesets.Mania.Tests
{
    [TestFixture]
    public class ManiaScoreReplayResultRoundTripTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestActualScoreFailureStateSurvivesReplayEncodingAndDecoding(bool failed)
        {
            var ruleset = new ManiaRuleset();
            var beatmap = new ManiaBeatmap(new StageDefinition(4))
            {
                HitObjects =
                {
                    new Note { StartTime = 1000, Column = 0 },
                    new Note { StartTime = 2000, Column = 1 },
                },
                Difficulty = { CircleSize = 4, OverallDifficulty = 5, DrainRate = 5 },
                BeatmapInfo =
                {
                    Ruleset = ruleset.RulesetInfo,
                    MD5Hash = new string('a', 32),
                    Hash = new string('b', 64),
                },
            };

            foreach (var note in beatmap.HitObjects)
                note.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            Mod[] mods = { new ManiaModHidden() };
            using var processor = new ManiaScoreProcessor();
            processor.Mods.Value = mods;
            processor.ApplyBeatmap(beatmap);
            processor.ApplyResult(new JudgementResult(beatmap.HitObjects[0], beatmap.HitObjects[0].Judgement) { Type = HitResult.Perfect });
            processor.ApplyResult(new JudgementResult(beatmap.HitObjects[1], beatmap.HitObjects[1].Judgement) { Type = HitResult.Miss });

            var original = new Score
            {
                ScoreInfo = new ScoreInfo
                {
                    BeatmapInfo = beatmap.BeatmapInfo,
                    BeatmapHash = beatmap.BeatmapInfo.Hash,
                    Ruleset = ruleset.RulesetInfo,
                    Mods = mods,
                    ClientVersion = "score-replay-result-contract",
                    Date = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero),
                    RulesetDataJson = "{\"retained_result_metadata\":7}",
                },
                Replay = new Replay
                {
                    Frames =
                    {
                        new ManiaReplayFrame(0),
                        new ManiaReplayFrame(1000, ManiaAction.Key1),
                        new ManiaReplayFrame(1020),
                        new ManiaReplayFrame(3000),
                    },
                },
            };
            original.ScoreInfo.Pauses.Add(1500);

            // Match the Player's real scoring entry point rather than fabricating an F rank while leaving Passed at its default.
            if (failed)
                processor.FailScore(original.ScoreInfo);
            else
                processor.PopulateScore(original.ScoreInfo);

            Assert.That(original.ScoreInfo.Passed, Is.EqualTo(!failed), "source score failure state");
            if (failed)
                Assert.That(original.ScoreInfo.Rank, Is.EqualTo(ScoreRank.F), "source rank from FailScore");

            using var encoded = new MemoryStream();
            new LegacyScoreEncoder(original, beatmap).Encode(encoded, true);
            encoded.Position = 0;
            var decoded = new NativeManiaScoreDecoder(beatmap).Parse(encoded);

            Assert.Multiple(() =>
            {
                Assert.That(decoded.ScoreInfo.Passed, Is.EqualTo(original.ScoreInfo.Passed), "failure must not turn into a passed score on replay import");
                Assert.That(decoded.ScoreInfo.Rank, Is.EqualTo(original.ScoreInfo.Rank), "stored rank");
                Assert.That(decoded.ScoreInfo.TotalScore, Is.EqualTo(original.ScoreInfo.TotalScore), "stored score");
                Assert.That(decoded.ScoreInfo.TotalScoreWithoutMods, Is.EqualTo(original.ScoreInfo.TotalScoreWithoutMods), "unmultiplied score");
                Assert.That(decoded.ScoreInfo.Accuracy, Is.EqualTo(original.ScoreInfo.Accuracy).Within(1e-12), "accuracy");
                Assert.That(decoded.ScoreInfo.MaxCombo, Is.EqualTo(original.ScoreInfo.MaxCombo), "maximum combo");
                Assert.That(decoded.ScoreInfo.Statistics, Is.EquivalentTo(original.ScoreInfo.Statistics.Where(pair => pair.Value != 0).ToDictionary()), "judgements");
                Assert.That(decoded.ScoreInfo.MaximumStatistics, Is.EquivalentTo(original.ScoreInfo.MaximumStatistics.Where(pair => pair.Value != 0).ToDictionary()), "maximum judgements");
                Assert.That(decoded.ScoreInfo.APIMods.Select(mod => JsonConvert.SerializeObject(mod)),
                    Is.EqualTo(original.ScoreInfo.APIMods.Select(mod => JsonConvert.SerializeObject(mod))), "actual mod settings");
                Assert.That(decoded.ScoreInfo.ClientVersion, Is.EqualTo(original.ScoreInfo.ClientVersion), "client identity");
                Assert.That(decoded.ScoreInfo.RulesetDataJson, Is.EqualTo(original.ScoreInfo.RulesetDataJson), "owning result metadata");
                Assert.That(decoded.ScoreInfo.Ruleset.ShortName, Is.EqualTo("mania"), "ruleset identity");
                Assert.That(decoded.ScoreInfo.BeatmapHash, Is.EqualTo(original.ScoreInfo.BeatmapHash), "source chart identity");
                Assert.That(decoded.ScoreInfo.Pauses, Is.EquivalentTo(original.ScoreInfo.Pauses), "recorded pauses");
                Assert.That(decoded.Replay.Frames.Select(frame => frame.Time), Is.EqualTo(original.Replay.Frames.Select(frame => frame.Time)), "input timing");
                Assert.That(decoded.Replay.Frames.Cast<ManiaReplayFrame>().Select(frame => string.Join(",", frame.Actions)),
                    Is.EqualTo(original.Replay.Frames.Cast<ManiaReplayFrame>().Select(frame => string.Join(",", frame.Actions))), "input actions");
            });
        }

        [Test]
        public void TestExportOfPersistedFailedRankDoesNotWriteIgnoredPassedDefaultAsSuccess()
        {
            var (score, beatmap) = createStoredScore(ScoreRank.F);
            Assert.That(score.ScoreInfo.Passed, Is.True, "Passed is ignored by Realm and starts at its constructor default after retrieval.");

            using var encoded = encodeStoredScore(score, beatmap);
            var decoded = new NativeManiaScoreDecoder(beatmap).Parse(encoded);

            Assert.Multiple(() =>
            {
                Assert.That(decoded.ScoreInfo.Rank, Is.EqualTo(ScoreRank.F));
                Assert.That(decoded.ScoreInfo.Passed, Is.False, "The persisted failure rank must survive another export and import.");
            });
        }

        [Test]
        public void TestExplicitPassedFieldIsNotReplacedByLegacyRankFallback()
        {
            var (score, beatmap) = createStoredScore(ScoreRank.D);
            score.ScoreInfo.Passed = false;
            using var encoded = encodeStoredScore(score, beatmap);
            var decoded = new NativeManiaScoreDecoder(beatmap).Parse(encoded);

            Assert.Multiple(() =>
            {
                Assert.That(decoded.ScoreInfo.Rank, Is.EqualTo(ScoreRank.D));
                Assert.That(decoded.ScoreInfo.Passed, Is.False, "New payloads retain the explicit state; Rank is only the fallback for an absent field.");
            });
        }

        [TestCase(ScoreRank.F, false)]
        [TestCase(ScoreRank.D, true)]
        public void TestOldPayloadWithoutPassedUsesRecordedRankRatherThanNoFailMod(ScoreRank storedRank, bool expectedPassed)
        {
            var (score, beatmap) = createStoredScore(storedRank);
            using var encoded = encodeStoredScore(score, beatmap, omitPassed: true);
            var decoded = new NativeManiaScoreDecoder(beatmap).Parse(encoded);

            Assert.Multiple(() =>
            {
                Assert.That(decoded.ScoreInfo.Rank, Is.EqualTo(storedRank));
                Assert.That(decoded.ScoreInfo.Passed, Is.EqualTo(expectedPassed));
                Assert.That(decoded.ScoreInfo.Mods.OfType<ManiaModNoFail>().Count(), Is.EqualTo(1), "Mods do not overwrite recorded failure evidence.");
            });
        }

        [Test]
        public void TestOldPayloadWithoutPassedOrRankRetainsUnspecifiedPassDefault()
        {
            var (score, beatmap) = createStoredScore(ScoreRank.F);
            using var encoded = encodeStoredScore(score, beatmap, omitPassed: true, omitRank: true);
            var decoded = new NativeManiaScoreDecoder(beatmap).Parse(encoded);

            Assert.That(decoded.ScoreInfo.Passed, Is.True, "A format with no stored failure evidence cannot have its completion inferred from mods or misses.");
        }

        private static (Score Score, ManiaBeatmap Beatmap) createStoredScore(ScoreRank rank)
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(4))
            {
                HitObjects = { new Note { StartTime = 1000, Column = 0 } },
                Difficulty = { CircleSize = 4 },
                BeatmapInfo = { Ruleset = new ManiaRuleset().RulesetInfo, MD5Hash = new string('c', 32), Hash = new string('d', 64) },
            };
            var score = new Score
            {
                ScoreInfo = new ScoreInfo
                {
                    BeatmapInfo = beatmap.BeatmapInfo,
                    BeatmapHash = beatmap.BeatmapInfo.Hash,
                    Ruleset = beatmap.BeatmapInfo.Ruleset,
                    Rank = rank,
                    Mods = new Mod[] { new ManiaModNoFail() },
                    Date = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero),
                    Statistics = { [HitResult.Miss] = 1 },
                    MaximumStatistics = { [HitResult.Perfect] = 1 },
                },
            };
            return (score, beatmap);
        }

        private static MemoryStream encodeStoredScore(Score score, ManiaBeatmap beatmap, bool omitPassed = false, bool omitRank = false)
        {
            var encoded = new MemoryStream();
            new LegacyScoreEncoder(score, beatmap).Encode(encoded, true);

            if (omitPassed || omitRank)
            {
                // Keep the actual encoder's header and replay unchanged; only remove metadata fields that
                // were absent from older lazer payloads. This exercises the full decoder, including its fallbacks.
                using var encodedInput = new MemoryStream(encoded.ToArray());
                using var reader = new SerializationReader(encodedInput);
                reader.ReadByte(); // ruleset
                reader.ReadInt32(); // version
                reader.ReadString(); // chart MD5
                reader.ReadString(); // username
                reader.ReadString(); // replay MD5
                for (int i = 0; i < 6; i++)
                    reader.ReadUInt16();
                reader.ReadInt32(); // score
                reader.ReadUInt16(); // maximum combo
                reader.ReadBoolean(); // full combo
                reader.ReadInt32(); // legacy mods
                reader.ReadString(); // HP graph
                reader.ReadDateTime();
                reader.ReadByteArray(); // replay frames
                reader.ReadInt64(); // legacy online ID

                long metadataPosition = encodedInput.Position;
                byte[] compressed = reader.ReadByteArray();
                using var compressedInput = new MemoryStream(compressed);
                byte[] properties = new byte[5];
                compressedInput.ReadExactly(properties);
                using var lengthReader = new BinaryReader(compressedInput, Encoding.UTF8, true);
                long uncompressedLength = lengthReader.ReadInt64();
                JObject metadata;
                using (var decompressed = new LzmaStream(properties, compressedInput, compressedInput.Length - compressedInput.Position, uncompressedLength))
                using (var textReader = new StreamReader(decompressed))
                    metadata = JObject.Parse(textReader.ReadToEnd());

                if (omitPassed)
                    metadata.Remove("passed");
                if (omitRank)
                    metadata.Remove("rank");

                byte[] content = Encoding.UTF8.GetBytes(metadata.ToString(Formatting.None));
                using var replacement = new MemoryStream();
                using (var compressor = new LzmaStream(new LzmaEncoderProperties(false, 1 << 21, 255), false, replacement))
                {
                    replacement.Write(compressor.Properties);
                    using var lengthWriter = new BinaryWriter(replacement, Encoding.UTF8, true);
                    lengthWriter.Write((long)content.Length);
                    compressor.Write(content);
                }

                encoded.SetLength(metadataPosition);
                encoded.Position = metadataPosition;
                using var writer = new SerializationWriter(encoded, true);
                writer.WriteByteArray(replacement.ToArray());
            }

            encoded.Position = 0;
            return encoded;
        }

        private sealed class NativeManiaScoreDecoder : LegacyScoreDecoder
        {
            private readonly ManiaBeatmap beatmap;

            public NativeManiaScoreDecoder(ManiaBeatmap beatmap)
            {
                this.beatmap = beatmap;
            }

            protected override Ruleset GetRuleset(int rulesetId)
                => rulesetId == 3 ? new ManiaRuleset() : throw new InvalidDataException("The replay must retain the native mania ruleset ID.");

            protected override WorkingBeatmap GetBeatmap(string md5Hash)
            {
                if (md5Hash != beatmap.BeatmapInfo.MD5Hash)
                    throw new InvalidDataException("The replay must retain the source chart MD5.");

                return new TestWorkingBeatmap(beatmap);
            }
        }
    }
}
