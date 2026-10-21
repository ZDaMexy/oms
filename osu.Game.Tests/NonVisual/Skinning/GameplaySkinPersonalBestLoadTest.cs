// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Screens.Play;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [HeadlessTest]
    public partial class GameplaySkinPersonalBestLoadTest : OsuTestScene
    {
        protected override bool UseFreshStoragePerRun => true;

        private RealmAccess? database;
        private RealmRulesetStore? rulesets;
        private ScoreManager scores = null!;
        private BeatmapInfo scoreBeatmap = null!;
        private NativeHost loaded = null!;
        private long expected;
        private GameHost gameHost = null!;

        [BackgroundDependencyLoader]
        private void load(GameHost host) => gameHost = host;

        [SetUpSteps]
        public void CreateDatabaseSteps()
        {
            AddStep("create isolated saved-score database", () =>
            {
                database = new RealmAccess(LocalStorage, "skin-personal-best.realm", gameHost.UpdateThread);
                rulesets = new RealmRulesetStore(database);
                scores = new ScoreManager(rulesets,
                    () => throw new InvalidOperationException("This model import does not decode a replay or query a beatmap manager."), LocalStorage, database, API);
                var info = new BeatmapInfo(new ManiaRuleset().RulesetInfo)
                {
                    Hash = new string('a', 64),
                    MD5Hash = new string('b', 32),
                    DifficultyName = "Personal best loading fixture",
                    Metadata = new BeatmapMetadata { Title = "Saved score fixture", Artist = "OMS tests" },
                };
                database.Write(r =>
                {
                    info.Ruleset = r.Find<RulesetInfo>(ManiaRuleset.SHORT_NAME)!;
                    r.Add(info);
                    scoreBeatmap = info.Detach();
                });
            });
        }

        [TearDownSteps]
        public void CloseDatabaseSteps()
            => AddStep("detach gameplay before closing its isolated database", closeDatabase);

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            rulesets?.Dispose();
            database?.Dispose();
        }

        private void closeDatabase()
        {
            Clear();
            rulesets?.Dispose();
            rulesets = null;
            database?.Dispose();
            database = null;
        }

        [TestCase("score.personal-best")]
        [TestCase("score.personal-best-progress")]
        [TestCase("score.personal-best-available")]
        [TestCase("track")]
        public void TestSavedPersonalBestIsLoadedForEachActualConsumerAndFrozenWithoutFurtherStorageReads(string consumer)
        {
            AddStep("save an actual scored mania play through ScoreManager", () =>
            {
                ScoreInfo played = createScoredModel(HitResult.Great);
                expected = played.TotalScore;
                Assert.That(expected, Is.GreaterThan(0));
                save(played);
                Assert.That(database!.Run(r => r.Find<ScoreInfo>(played.ID)!.MaximumStatistics.Count), Is.GreaterThan(0));
            });
            mount(consumer);
            AddStep("close storage after the production load", () =>
            {
                Assert.That(readPersonalBest(), Is.EqualTo(expected));
                database!.Dispose();
            });
            AddRepeatStep("advance production frames after storage is closed", () => loaded.ClockHost.Sample(1500), 3);
            AddStep("reset and late-attach retain the loaded numeric target", () =>
            {
                loaded.Events.RequestReset(GameplaySkinEventResetReason.ConsumerRebuilt);
                loaded.ClockHost.Sample(1500);
                Assert.That(readPersonalBest(), Is.EqualTo(expected));
            });
        }

        [TestCase(RejectedCandidate.Mods)]
        [TestCase(RejectedCandidate.User)]
        [TestCase(RejectedCandidate.BeatmapIdentity)]
        [TestCase(RejectedCandidate.BeatmapHash)]
        [TestCase(RejectedCandidate.OldScoreVersion)]
        [TestCase(RejectedCandidate.MissingMaximumStatistics)]
        [TestCase(RejectedCandidate.Deleted)]
        [TestCase(RejectedCandidate.Failed)]
        [TestCase(RejectedCandidate.Ruleset)]
        [TestCase(RejectedCandidate.ReprocessingFailed)]
        public void TestHigherStoredButIncompatibleScoreCannotReplaceTheCurrentPlayersValidTarget(RejectedCandidate reason)
        {
            AddStep("persist one valid play and one higher rejected candidate", () =>
            {
                ScoreInfo valid = createScoredModel(HitResult.Great);
                expected = valid.TotalScore;
                save(valid);
                Guid invalid = save(createScoredModel(HitResult.Perfect, reason == RejectedCandidate.Failed));
                Assert.That(database!.Run(r => r.Find<ScoreInfo>(invalid)!.TotalScore), Is.GreaterThan(expected));
                reject(invalid, reason);
            });
            mount("score.personal-best");
            AddAssert("only the compatible saved score becomes the target", () => readPersonalBest() == expected);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestEmptyOrEntirelyIncompatibleSavedScoresRemainUnavailable(bool rejectedCandidates)
        {
            AddStep("prepare no compatible history", () =>
            {
                if (!rejectedCandidates)
                    return;
                foreach (RejectedCandidate reason in Enum.GetValues<RejectedCandidate>())
                    reject(save(createScoredModel(HitResult.Perfect, reason == RejectedCandidate.Failed)), reason);
            });
            mount("score.personal-best-available");
            AddAssert("unavailable history is not a fabricated zero target", () => readPersonalBest() == null);
        }

        [Test]
        public void TestOrdinaryProgramWithoutPersonalBestConsumersNeverReadsRealm()
        {
            AddStep("dispose the isolated realm before an ordinary gameplay load", () =>
            {
                save(createScoredModel(HitResult.Perfect));
                database!.Dispose();
            });
            mount(null);
            AddAssert("ordinary skin loaded without touching the disposed database", () => readPersonalBest() == null);
        }

        private void mount(string? consumer)
        {
            AddStep("mount actual score processor and event-runtime loading chain", () => Child = loaded = new NativeHost(database!, scoreBeatmap, consumer));
            AddUntilStep("event runtime completed its production load", () => loaded.Events?.IsLoaded == true);
        }

        private long? readPersonalBest()
        {
            using GameplaySkinEventSubscription subscription = loaded.Events.EventStream.Subscribe();
            Assert.That(subscription.TryDequeue(out GameplaySkinEventEnvelope? envelope), Is.True);
            return ((GameplaySkinStateEventPayload)envelope!.Payload).State.ChartPresentation!.PersonalBestScore;
        }

        private ScoreInfo createScoredModel(HitResult result, bool failed = false)
        {
            var beatmap = chart(scoreBeatmap);
            using var processor = new ManiaScoreProcessor();
            processor.Mods.Value = currentMods();
            foreach (Note note in beatmap.HitObjects)
                note.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
            processor.ApplyBeatmap(beatmap);
            foreach (Note note in beatmap.HitObjects)
                processor.ApplyResult(new JudgementResult(note, note.Judgement) { Type = result });
            var score = new ScoreInfo(scoreBeatmap, scoreBeatmap.Ruleset)
            {
                User = API.LocalUser.Value,
                Hash = Guid.NewGuid().ToString("N"),
                Mods = new Mod[] { new ManiaModHidden(), new ManiaModNoFail() },
            };
            if (failed)
                processor.FailScore(score);
            else
                processor.PopulateScore(score);
            return score;
        }

        private Guid save(ScoreInfo score)
        {
            Live<ScoreInfo>? stored = scores.Import(score);
            Assert.That(stored, Is.Not.Null, "The real score importer must save the model, including statistics JSON.");
            return stored!.ID;
        }

        private void reject(Guid id, RejectedCandidate reason)
        {
            database!.Write(r =>
            {
                ScoreInfo stored = r.Find<ScoreInfo>(id)!;
                switch (reason)
                {
                    case RejectedCandidate.Mods: stored.ModsJson = string.Empty; break;
                    case RejectedCandidate.User: stored.RealmUser.OnlineID = API.LocalUser.Value.Id + 1; break;
                    case RejectedCandidate.BeatmapIdentity:
                        stored.BeatmapInfo = r.Add(new BeatmapInfo(r.Find<RulesetInfo>(ManiaRuleset.SHORT_NAME)!) { Hash = scoreBeatmap.Hash });
                        break;
                    case RejectedCandidate.BeatmapHash: stored.BeatmapHash = new string('c', 64); break;
                    case RejectedCandidate.OldScoreVersion: stored.TotalScoreVersion = LegacyScoreEncoder.LATEST_VERSION - 1; break;
                    case RejectedCandidate.MissingMaximumStatistics: stored.MaximumStatisticsJson = "{}"; break;
                    case RejectedCandidate.Deleted: stored.DeletePending = true; break;
                    case RejectedCandidate.Failed: Assert.That(stored.Rank, Is.EqualTo(ScoreRank.F)); break;
                    case RejectedCandidate.Ruleset: stored.Ruleset = r.Find<RulesetInfo>(BmsRuleset.SHORT_NAME)!; break;
                    case RejectedCandidate.ReprocessingFailed: stored.BackgroundReprocessingFailed = true; break;
                    default: throw new ArgumentOutOfRangeException(nameof(reason));
                }
            });
        }

        private static Mod[] currentMods() => new Mod[] { new ManiaModNoFail(), new ManiaModHidden() };

        private static Beatmap<Note> chart(BeatmapInfo info)
            => new Beatmap<Note>
            {
                BeatmapInfo = info,
                HitObjects = { new Note { StartTime = 1000 }, new Note { StartTime = 3000 } },
            };

        public enum RejectedCandidate
        {
            Mods,
            User,
            BeatmapIdentity,
            BeatmapHash,
            OldScoreVersion,
            MissingMaximumStatistics,
            Deleted,
            Failed,
            Ruleset,
            ReprocessingFailed,
        }

        private partial class NativeHost : Container
        {
            [Cached]
            private readonly RealmAccess database;

            [Cached(typeof(ScoreProcessor))]
            private readonly ManiaScoreProcessor score = new ManiaScoreProcessor();

            private readonly Beatmap<Note> beatmap;
            private readonly string? consumer;
            private GameplaySkinLayoutPublication? publication;
            public NativeClock ClockHost { get; } = new NativeClock();
            public GameplaySkinEventRuntimeHost Events { get; private set; } = null!;

            public NativeHost(RealmAccess database, BeatmapInfo info, string? consumer)
            {
                this.database = database;
                beatmap = chart(info);
                this.consumer = consumer;
                RelativeSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(IRenderer renderer)
            {
                foreach (Note note in beatmap.HitObjects)
                    note.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
                score.Mods.Value = currentMods();
                score.ApplyBeatmap(beatmap);
                publication = GameplaySkinSceneRuntimeHostTest.CreateEssentialInformationPublication(renderer.WhitePixel, consumer != null,
                    bindingSource: consumer == "track" ? null : consumer, trackClock: consumer == "track" ? "score.personal-best" : null);
                Events = new GameplaySkinEventRuntimeHost(publication, beatmap);
                ClockHost.Child = Events;
                Child = ClockHost;
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                score.Dispose();
                publication?.Dispose();
            }
        }

        private partial class NativeClock : GameplayClockContainer
        {
            private readonly AdjustableClock source;

            public NativeClock()
                : this(new AdjustableClock { CurrentTime = 500 })
            {
            }

            private NativeClock(AdjustableClock source)
                : base(source, false, false)
            {
                this.source = source;
                SoftUnpause();
            }

            public void Sample(double time)
            {
                source.CurrentTime = time;
                UpdateSubTree();
            }
        }

        private class AdjustableClock : ManualClock, IAdjustableClock
        {
            public void Start() => IsRunning = true;
            public void Stop() => IsRunning = false;
            public bool Seek(double position)
            {
                CurrentTime = position;
                return true;
            }
            public void Reset()
            {
                IsRunning = false;
                CurrentTime = 0;
            }
            public void ResetSpeedAdjustments() => Rate = 1;
        }
    }
}
