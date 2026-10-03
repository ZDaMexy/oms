// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online.IR;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking;

namespace osu.Game.Tests.Visual.Gameplay
{
    /// <summary>Synthetic mania play through the real Player and Realm score importer, with an in-process IR transport.</summary>
    public partial class TestSceneOmsIrPlayerSave : PlayerTestScene
    {
        private const string service_origin = "https://ir-player.example.test/";
        private const string chart_md5 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string chart_sha256 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private readonly ConcurrentQueue<Receipt> receipts = new ConcurrentQueue<Receipt>();
        private RulesetStore rulesets = null!;
        private BeatmapManager beatmaps = null!;
        private OmsIrService ir = null!;
        private HttpClient http = null!;
        private ManiaBeatmap chart = null!;
        private Task? accountOperation;
        private bool holdImport;

        private new SavingPlayer Player => (SavingPlayer)base.Player;

        protected override bool HasCustomSteps => true;
        protected override bool AllowFail => true;
        protected override Ruleset CreatePlayerRuleset() => new ManiaRuleset();
        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset) => chart;
        protected override TestPlayer CreatePlayer(Ruleset ruleset) => new SavingPlayer(holdImport);

        [BackgroundDependencyLoader]
        private void load(GameHost host, AudioManager audio)
        {
            Dependencies.Cache(rulesets = new RealmRulesetStore(Realm));
            Dependencies.Cache(beatmaps = new BeatmapManager(LocalStorage, Realm, null, audio, Resources, host, Beatmap.Default));
            Dependencies.Cache(new ScoreManager(rulesets, () => beatmaps, LocalStorage, Realm, API));
            Dependencies.Cache(Realm);
            http = new HttpClient(new IrHandler(receipts, readStoredHash));
            Dependencies.Cache(ir = new OmsIrService(LocalStorage, http, new MemoryCredentials()));
        }

        public override void SetUpSteps()
        {
            base.SetUpSteps();
            AddStep("reset fixture", () =>
            {
                receipts.Clear();
                holdImport = false;
            });
            AddStep("register synthetic 4K chart in Realm", () =>
            {
                chart = new ManiaBeatmap(new StageDefinition(4))
                {
                    BeatmapInfo = new BeatmapInfo(new ManiaRuleset().RulesetInfo)
                    {
                        Hash = chart_sha256,
                        MD5Hash = chart_md5,
                        DifficultyName = "Synthetic player-save fixture",
                        Metadata = new BeatmapMetadata { Title = "IR player fixture", Artist = "Test fixture" },
                        Difficulty = new BeatmapDifficulty { CircleSize = 4, OverallDifficulty = 5, DrainRate = 5 },
                        Length = 3000,
                    },
                    HitObjects =
                    {
                        new Note { StartTime = 1000, Column = 0 },
                        new Note { StartTime = 2000, Column = 1 },
                        new Note { StartTime = 3000, Column = 2 },
                    },
                };
                Realm.Write(realm =>
                {
                    chart.BeatmapInfo.Ruleset = realm.Find<RulesetInfo>(ManiaRuleset.SHORT_NAME)!;
                    var set = new BeatmapSetInfo(new[] { chart.BeatmapInfo }) { Hash = chart_sha256 };
                    chart.BeatmapInfo.BeatmapSet = set;
                    realm.Add(set);
                    chart.BeatmapInfo = chart.BeatmapInfo.Detach();
                });
            });
        }

        [Test]
        public void SavedScoreExistsInRealmBeforeTheIrReceivesItsActualUuid()
        {
            configureAccount(enabled: true, login: true);
            AddStep("hold local score import", () => holdImport = true);
            CreateTest();
            finishPlay();
            AddUntilStep("real Player reached its local import", () => Player.ImportStarted);
            AddAssert("IR has not received a score before save", () => receipts.IsEmpty);
            AddAssert("nothing queued before local save", () => ir.State.PendingCount == 0);
            AddAssert("candidate score is not in Realm", () => Player.CandidateScore != null && readStoredHash(Player.CandidateScore.ID) == null);
            AddStep("permit the real local importer", () => Player.ReleaseImport());
            AddUntilStep("local score import completed", () => Player.ImportCompleted);
            AddUntilStep("IR received the saved score", () => receipts.Count == 1);
            AddAssert("HTTP receipt saw a stored score hash", () => !string.IsNullOrEmpty(receipts.Single().StoredHash));
            AddAssert("submitted UUID is the importer returned UUID", () => receipts.Single().SubmissionId == Player.SavedScore!.ID);
            AddAssert("submitted actual playable columns", () => (string?)receipts.Single().Payload["keymode"] == "mania_4k");
            AddAssert("submitted original chart identity", () => (string?)receipts.Single().Payload["chart"]?["md5"] == chart_md5
                                                                  && (string?)receipts.Single().Payload["chart"]?["sha256"] == chart_sha256);
            AddUntilStep("receipt cleared durable pending", () => ir.State.PendingCount == 0 && !ir.State.Busy);
            AddUntilStep("results are displayed", () => Player.GetChildScreen() is ResultsScreen);
        }

        [Test]
        public void DisabledIrStillStoresTheFinishedPlayWithoutSubmission()
        {
            configureAccount(enabled: false, login: false);
            CreateTest();
            finishPlay();
            AddUntilStep("real local score import completed", () => Player.ImportCompleted);
            AddUntilStep("results are displayed", () => Player.GetChildScreen() is ResultsScreen);
            AddAssert("local saved score has a hash", () => !string.IsNullOrEmpty(readStoredHash(Player.SavedScore!.ID)));
            AddAssert("IR never received a submission", () => receipts.IsEmpty);
            AddAssert("IR has no pending play", () => ir.State.PendingCount == 0);
        }

        [Test]
        public void LoggingInDuringAnAnonymousPlayDoesNotClaimTheFinishedScore()
        {
            configureAccount(enabled: true, login: false);
            CreateTest();
            AddAssert("play began without an IR account", () => ir.CaptureSubmissionTarget() == null);
            AddStep("log in after the player captured its target", () => accountOperation = ir.LoginAsync("ir_player", "Synthetic-player-password!"));
            AddUntilStep("IR account operation completed", () => accountOperation!.IsCompleted);
            AddAssert("IR login succeeded", () => accountOperation!.IsCompletedSuccessfully && ir.State.Account?.Id == 17);
            finishPlay();
            AddUntilStep("real local score import completed", () => Player.ImportCompleted);
            AddUntilStep("results are displayed", () => Player.GetChildScreen() is ResultsScreen);
            AddAssert("anonymous play is still saved locally", () => !string.IsNullOrEmpty(readStoredHash(Player.SavedScore!.ID)));
            AddAssert("later login did not claim the anonymous play", () => receipts.IsEmpty && ir.State.PendingCount == 0);
        }

        private void configureAccount(bool enabled, bool login)
        {
            AddStep("configure independent IR", () => accountOperation = configureAsync(enabled, login));
            AddUntilStep("IR account operation completed", () => accountOperation!.IsCompleted);
            AddAssert("IR account operation succeeded", () => accountOperation!.IsCompletedSuccessfully);
        }

        private async Task configureAsync(bool enabled, bool login)
        {
            await ir.LogoutAsync().ConfigureAwait(false);
            await ir.ConfigureAsync(service_origin, enabled).ConfigureAwait(false);
            if (login)
                await ir.LoginAsync("ir_player", "Synthetic-player-password!").ConfigureAwait(false);
        }

        private void finishPlay()
        {
            AddUntilStep("synthetic track is running", () => Beatmap.Value.Track.IsRunning);
            AddStep("seek to the final native mania object", () => Player.GameplayClockContainer.Seek(Player.DrawableRuleset.Objects.Last().GetEndTime()));
        }

        private string? readStoredHash(Guid id) => Realm.Run(realm => realm.Find<ScoreInfo>(id)?.Hash);

        protected override void Dispose(bool isDisposing)
        {
            ir?.Dispose();
            http?.Dispose();
            base.Dispose(isDisposing);
            rulesets?.Dispose();
        }

        private sealed record Receipt(Guid SubmissionId, JObject Payload, string? StoredHash);

        private partial class SavingPlayer : TestPlayer
        {
            private readonly TaskCompletionSource importAllowed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            public volatile bool ImportStarted;
            public volatile bool ImportCompleted;
            public ScoreInfo? CandidateScore { get; private set; }
            public ScoreInfo? SavedScore { get; private set; }

            public SavingPlayer(bool holdImport)
                : base(false)
            {
                if (!holdImport)
                    importAllowed.TrySetResult();
            }

            public void ReleaseImport() => importAllowed.TrySetResult();

            protected override async Task ImportScore(Score score)
            {
                CandidateScore = score.ScoreInfo;
                ImportStarted = true;
                await importAllowed.Task.ConfigureAwait(false);
                await base.ImportScore(score).ConfigureAwait(false);
                SavedScore = score.ScoreInfo;
                ImportCompleted = true;
            }
        }

        private sealed class IrHandler : HttpMessageHandler
        {
            private readonly ConcurrentQueue<Receipt> receipts;
            private readonly Func<Guid, string?> readStoredHash;

            public IrHandler(ConcurrentQueue<Receipt> receipts, Func<Guid, string?> readStoredHash)
            {
                this.receipts = receipts;
                this.readStoredHash = readStoredHash;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("/auth/logout", StringComparison.Ordinal))
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                JObject response;
                if (request.RequestUri.AbsolutePath.EndsWith("/auth/login", StringComparison.Ordinal))
                {
                    response = new JObject
                    {
                        ["user"] = new JObject { ["id"] = 17, ["username"] = "ir_player" },
                        ["access_token"] = "synthetic-player-access-0000000000",
                        ["refresh_token"] = "synthetic-player-refresh-0000000000",
                        ["expires_in"] = 3600,
                    };
                }
                else if (request.RequestUri.AbsolutePath.EndsWith("/scores/submit", StringComparison.Ordinal))
                {
                    JObject body = JObject.Parse(await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
                    Guid id = Guid.Parse((string)body["submission_id"]!);
                    receipts.Enqueue(new Receipt(id, body, readStoredHash(id)));
                    response = new JObject
                    {
                        ["duplicate"] = false,
                        ["score"] = new JObject { ["id"] = 1, ["user_id"] = 17, ["submission_id"] = id.ToString() },
                    };
                }
                else
                    throw new InvalidOperationException("Unexpected synthetic IR route.");

                return new HttpResponseMessage(request.RequestUri.AbsolutePath.EndsWith("/scores/submit", StringComparison.Ordinal)
                    ? HttpStatusCode.Created : HttpStatusCode.OK)
                {
                    Content = new StringContent(response.ToString(Formatting.None), Encoding.UTF8, "application/json"),
                };
            }
        }

        private sealed class MemoryCredentials : IOmsIrCredentialStore
        {
            private readonly Dictionary<string, OmsIrSession> values = new Dictionary<string, OmsIrSession>();
            public OmsIrSession? Read(string target) => values.GetValueOrDefault(target);
            public void Write(string target, OmsIrSession session) => values[target] = session;
            public void Delete(string target) => values.Remove(target);
        }
    }
}
