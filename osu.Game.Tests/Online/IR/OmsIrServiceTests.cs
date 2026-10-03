// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Online.IR;

namespace osu.Game.Tests.Online.IR
{
    [TestFixture]
    public class OmsIrServiceTests
    {
        private const string origin = "https://ir.example.test/";
        private const string password = "OMS-local-test-password!";
        private TemporaryNativeStorage storage = null!;
        private MemoryCredentials credentials = null!;

        [SetUp]
        public void SetUp()
        {
            storage = new TemporaryNativeStorage("oms-ir-service-" + Guid.NewGuid().ToString("N"));
            credentials = new MemoryCredentials();
        }

        [TearDown]
        public void TearDown() => storage.Dispose();

        [Test]
        public async Task DefaultHasNoAddressAndMakesNoRequest()
        {
            int requests = 0;
            using var http = new HttpClient(new Handler((_, _) =>
            {
                Interlocked.Increment(ref requests);
                return Task.FromResult(json(new JObject()));
            }));
            using var service = new OmsIrService(storage, http, credentials);
            Assert.That(service.State.Enabled, Is.False);
            Assert.That(service.State.ServiceAddress, Is.Empty);
            Assert.That(service.CaptureSubmissionTarget(), Is.Null);
            await Task.Delay(100);
            Assert.That(requests, Is.Zero);
        }

        [TestCase("http://ir.example.test/")]
        [TestCase("https://account:secret@ir.example.test/")]
        [TestCase("https://ir.example.test/ir/")]
        [TestCase("https://ir.example.test/?secret=x")]
        [TestCase("https://ir.example.test/#part")]
        public void RejectsNonTlsRemoteOrCredentialAndApiAddresses(string address)
        {
            OmsIrException exception = Assert.Throws<OmsIrException>(() => OmsIrService.ParseServiceAddress(address))!;
            Assert.That(exception.Code, Is.EqualTo("invalid_address"));
        }

        [Test]
        public void LoopbackDevelopmentAddressIsAllowed() =>
            Assert.That(OmsIrService.ParseServiceAddress("http://127.0.0.1:8081"), Is.EqualTo(new Uri("http://127.0.0.1:8081/")));

        [Test]
        public async Task CredentialsAreOnlyWrittenToTheCredentialStoreAndRestoredFromThere()
        {
            using var http = standardHttp();
            using (var service = new OmsIrService(storage, http, credentials))
            {
                await service.ConfigureAsync(origin, true);
                await service.LoginAsync("player_one", password, register: true);
                Assert.That(service.State.Account, Is.EqualTo(new OmsIrAccount(1, "player_one")));
                OmsIrSubmissionTarget target = service.CaptureSubmissionTarget()!;
                await service.ConfigureAsync(origin, false);
                await service.QueueSubmissionAsync(target, submission());
                Assert.That(credentials.Values.Single().Value.ToString(), Is.EqualTo("OMS IR credential record"));
            }
            using var restored = new OmsIrService(storage, http, credentials);
            Assert.That(restored.State.Enabled, Is.False);
            Assert.That(restored.State.Account?.Id, Is.EqualTo(1));
            Assert.That(restored.State.PendingCount, Is.EqualTo(1));
            foreach (string path in Directory.EnumerateFiles(storage.GetFullPath("oms-ir"), "*", SearchOption.AllDirectories))
            {
                string contents = File.ReadAllText(path);
                Assert.That(contents, Does.Not.Contain(password));
                Assert.That(contents, Does.Not.Contain(accessFor(1)));
                Assert.That(contents, Does.Not.Contain(refreshFor(1)));
            }
        }

        [Test]
        public async Task LocalPublicationDoesNotWaitForLoginAndKeepsTheOpeningAccount()
        {
            var loginStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseLogin = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var submittedOwners = new ConcurrentQueue<long>();
            using var http = new HttpClient(new Handler(async (request, token) =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("/auth/login", StringComparison.Ordinal))
                {
                    string name = (string)JObject.Parse(await request.Content!.ReadAsStringAsync(token))["username"]!;
                    if (name == "player_two")
                    {
                        loginStarted.TrySetResult();
                        await releaseLogin.Task.WaitAsync(token);
                    }
                    return json(loginResponse(name == "player_one" ? 1 : 2));
                }
                if (request.RequestUri.AbsolutePath.EndsWith("/scores/submit", StringComparison.Ordinal))
                {
                    long owner = request.Headers.Authorization!.Parameter == accessFor(1) ? 1 : 2;
                    submittedOwners.Enqueue(owner);
                    return acknowledge(JObject.Parse(await request.Content!.ReadAsStringAsync(token)), owner);
                }
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }));
            using var service = new OmsIrService(storage, http, credentials);
            await service.ConfigureAsync(origin, true);
            await service.LoginAsync("player_one", password);
            OmsIrSubmissionTarget target = service.CaptureSubmissionTarget()!;
            Task login = service.LoginAsync("player_two", password);
            await loginStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await service.QueueSubmissionAsync(target, submission()).WaitAsync(TimeSpan.FromSeconds(1));
            Assert.That(service.State.PendingCount, Is.EqualTo(1));
            Assert.That(submittedOwners, Is.Empty);
            releaseLogin.TrySetResult();
            await login;
            await waitUntil(() => !service.State.Busy);
            Assert.That(service.State.WaitingOtherAccountCount, Is.EqualTo(1));
            Assert.That(submittedOwners, Is.Empty);
            await service.LoginAsync("player_one", password);
            await waitUntil(() => service.State.PendingCount == 0);
            Assert.That(submittedOwners, Is.EqualTo(new[] { 1L }));
        }

        [Test]
        public async Task LostResponseAndRestartResubmitExactlyTheSameBodyAndId()
        {
            var seen = new ConcurrentQueue<string>();
            var stored = new HashSet<string>();
            bool loseResponse = true;
            using var http = standardHttp(async (request, token) =>
            {
                string body = await request.Content!.ReadAsStringAsync(token);
                seen.Enqueue(body);
                string id = (string)JObject.Parse(body)["submission_id"]!;
                bool duplicate = !stored.Add(id);
                if (loseResponse)
                    throw new HttpRequestException("A synthetic response was lost after commit.");
                return acknowledge(JObject.Parse(body), 1, duplicate);
            });
            OmsIrSubmission play = submission();
            using (var service = new OmsIrService(storage, http, credentials))
            {
                await service.ConfigureAsync(origin, true);
                await service.LoginAsync("player_one", password);
                await service.QueueSubmissionAsync(service.CaptureSubmissionTarget()!, play);
                await waitUntil(() => seen.Count == 1 && !service.State.Busy);
                Assert.That(service.State.PendingCount, Is.EqualTo(1));
            }
            loseResponse = false;
            using var restarted = new OmsIrService(storage, http, credentials);
            await restarted.RetryPendingAsync();
            await waitUntil(() => restarted.State.PendingCount == 0);
            Assert.That(seen.Count, Is.EqualTo(2));
            Assert.That(seen.ToArray()[1], Is.EqualTo(seen.ToArray()[0]));
            Assert.That(stored, Is.EqualTo(new[] { play.SubmissionId.ToString() }));
        }

        [Test]
        public async Task LosingResponseBodyAfterHeadersKeepsIrEnabledAndResubmitsTheSamePlay()
        {
            var seen = new ConcurrentQueue<string>();
            var committed = new HashSet<string>();
            bool loseBody = true;
            using var http = standardHttp(async (request, token) =>
            {
                string body = await request.Content!.ReadAsStringAsync(token);
                seen.Enqueue(body);
                JObject payload = JObject.Parse(body);
                bool duplicate = !committed.Add((string)payload["submission_id"]!);
                if (loseBody)
                    return new HttpResponseMessage(HttpStatusCode.Created) { Content = new StreamContent(new BrokenResponseStream()) };
                return acknowledge(payload, 1, duplicate);
            });
            using var service = new OmsIrService(storage, http, credentials);
            await service.ConfigureAsync(origin, true);
            await service.LoginAsync("player_one", password);
            OmsIrSubmission play = submission();
            await service.QueueSubmissionAsync(service.CaptureSubmissionTarget()!, play);
            await waitUntil(() => seen.Count == 1 && !service.State.Busy && service.State.Message.Contains("中途断开", StringComparison.Ordinal));
            Assert.That(service.State.Enabled, Is.True);
            Assert.That(service.State.PendingCount, Is.EqualTo(1));
            Assert.That(service.State.BlockedCount, Is.Zero);
            loseBody = false;
            await service.RetryPendingAsync();
            await waitUntil(() => service.State.PendingCount == 0);
            Assert.That(seen.Count, Is.EqualTo(2));
            Assert.That(seen.ToArray()[1], Is.EqualTo(seen.ToArray()[0]));
            Assert.That(committed, Is.EqualTo(new[] { play.SubmissionId.ToString() }));
        }

        [Test]
        public async Task InvalidRefreshRequiresTheOriginalAccountAndPreservesPending()
        {
            int submits = 0;
            using var http = new HttpClient(new Handler((request, _) =>
            {
                string path = request.RequestUri!.AbsolutePath;
                if (path.EndsWith("/auth/login", StringComparison.Ordinal))
                    return Task.FromResult(json(loginResponse(1)));
                if (path.EndsWith("/auth/refresh", StringComparison.Ordinal))
                    return Task.FromResult(error(HttpStatusCode.Unauthorized, "invalid_refresh"));
                if (path.EndsWith("/scores/submit", StringComparison.Ordinal))
                    Interlocked.Increment(ref submits);
                return Task.FromResult(error(HttpStatusCode.Unauthorized, "invalid_session"));
            }));
            using var service = new OmsIrService(storage, http, credentials);
            await service.ConfigureAsync(origin, true);
            await service.LoginAsync("player_one", password);
            OmsIrSubmissionTarget target = service.CaptureSubmissionTarget()!;
            await service.QueueSubmissionAsync(target, submission());
            await waitUntil(() => service.State.RequiresLogin && !service.State.Busy);
            Assert.That(service.State.PendingCount, Is.EqualTo(1));
            Assert.That(service.PendingSubmissions.Single().Target, Is.EqualTo(target));
            Assert.That(credentials.Values, Is.Empty);
            await Task.Delay(100);
            Assert.That(submits, Is.EqualTo(1));
        }

        [Test]
        public async Task FailedWindowsCredentialRemovalStillStopsTheInvalidSessionAndKeepsPending()
        {
            int submits = 0;
            using var http = new HttpClient(new Handler((request, _) =>
            {
                string path = request.RequestUri!.AbsolutePath;
                if (path.EndsWith("/auth/login", StringComparison.Ordinal))
                    return Task.FromResult(json(loginResponse(1)));
                if (path.EndsWith("/auth/refresh", StringComparison.Ordinal))
                    return Task.FromResult(error(HttpStatusCode.Unauthorized, "invalid_refresh"));
                if (path.EndsWith("/scores/submit", StringComparison.Ordinal))
                    Interlocked.Increment(ref submits);
                return Task.FromResult(error(HttpStatusCode.Unauthorized, "invalid_session"));
            }));
            using var service = new OmsIrService(storage, http, credentials);
            await service.ConfigureAsync(origin, true);
            await service.LoginAsync("player_one", password);
            credentials.FailDelete = true;
            OmsIrSubmissionTarget target = service.CaptureSubmissionTarget()!;
            await service.QueueSubmissionAsync(target, submission());
            await waitUntil(() => service.State.RequiresLogin && !service.State.Busy);
            Assert.That(service.State.Account, Is.Null);
            Assert.That(service.CaptureSubmissionTarget(), Is.Null);
            Assert.That(service.State.Message, Does.Contain("无法清除"));
            Assert.That(service.State.PendingCount, Is.EqualTo(1));
            Assert.That(service.PendingSubmissions.Single().Target, Is.EqualTo(target));
            Assert.That(credentials.Values, Has.Count.EqualTo(1));
            await Task.Delay(100);
            Assert.That(submits, Is.EqualTo(1));
        }

        [TestCase(HttpStatusCode.UnprocessableEntity, "unsupported_mod")]
        [TestCase(HttpStatusCode.Conflict, "submission_conflict")]
        public async Task UnsupportedAndConflictingSubmissionsStayBlockedUntilManualRetry(HttpStatusCode status, string code)
        {
            int requests = 0;
            bool supported = false;
            using var http = standardHttp(async (request, token) =>
            {
                Interlocked.Increment(ref requests);
                return supported ? acknowledge(JObject.Parse(await request.Content!.ReadAsStringAsync(token)), 1)
                    : error(status, code);
            });
            using var service = new OmsIrService(storage, http, credentials);
            await service.ConfigureAsync(origin, true);
            await service.LoginAsync("player_one", password);
            await service.QueueSubmissionAsync(service.CaptureSubmissionTarget()!, submission());
            await waitUntil(() => service.State.BlockedCount == 1 && !service.State.Busy);
            Assert.That(service.PendingSubmissions.Single().BlockedReason, Does.Contain(code));
            await Task.Delay(100);
            Assert.That(requests, Is.EqualTo(1));
            supported = true;
            await service.RetryPendingAsync();
            await waitUntil(() => service.State.PendingCount == 0);
            Assert.That(requests, Is.EqualTo(2));
        }

        [Test]
        public async Task SameLocalIdCannotBeReplacedByChangedPayload()
        {
            using var http = standardHttp();
            using var service = new OmsIrService(storage, http, credentials);
            await service.ConfigureAsync(origin, true);
            await service.LoginAsync("player_one", password);
            OmsIrSubmissionTarget target = service.CaptureSubmissionTarget()!;
            await service.ConfigureAsync(origin, false);
            OmsIrSubmission original = submission();
            await service.QueueSubmissionAsync(target, original);
            await service.QueueSubmissionAsync(target, original);
            var changed = new OmsIrSubmission(original.SubmissionId, new JObject { ["submission_id"] = original.SubmissionId.ToString(), ["marker"] = "changed" });
            OmsIrException rejected = Assert.ThrowsAsync<OmsIrException>(async () => await service.QueueSubmissionAsync(target, changed))!;
            Assert.That(rejected.Code, Is.EqualTo("invalid_submission"));
            Assert.That(service.State.PendingCount, Is.EqualTo(1));
            string queued = File.ReadAllText(Directory.EnumerateFiles(storage.GetFullPath("oms-ir/pending"), "*.json").Single());
            Assert.That(queued, Does.Not.Contain("changed"));
        }

        [Test]
        public async Task CancellingAnInFlightSubmissionLeavesItsDurableRecord()
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var http = standardHttp(async (_, token) =>
            {
                started.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    cancelled.TrySetResult();
                    throw;
                }
                throw new InvalidOperationException("The submission should have been cancelled.");
            });
            var service = new OmsIrService(storage, http, credentials);
            await service.ConfigureAsync(origin, true);
            await service.LoginAsync("player_one", password);
            await service.QueueSubmissionAsync(service.CaptureSubmissionTarget()!, submission());
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            service.Dispose();
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await waitUntil(() => !service.State.Busy);
            Assert.That(service.PendingSubmissions, Has.Count.EqualTo(1));
            Assert.That(Directory.EnumerateFiles(storage.GetFullPath("oms-ir/pending"), "*.json"), Has.Exactly(1).Items);
        }

        [Test]
        public async Task CorruptPrimaryRecoversTheLastCompleteQueueWithoutErasingEvidence()
        {
            int failed = 0;
            using var http = standardHttp((_, _) =>
            {
                Interlocked.Increment(ref failed);
                throw new HttpRequestException("Synthetic offline.");
            });
            OmsIrSubmission play = submission();
            using (var service = new OmsIrService(storage, http, credentials))
            {
                await service.ConfigureAsync(origin, true);
                await service.LoginAsync("player_one", password);
                await service.QueueSubmissionAsync(service.CaptureSubmissionTarget()!, play);
                await waitUntil(() => failed == 1 && !service.State.Busy);
                await service.ConfigureAsync(origin, false);
            }
            string pending = storage.GetFullPath("oms-ir/pending");
            string primary = Directory.EnumerateFiles(pending, "*.json").Single();
            Assert.That(File.Exists(primary + ".backup"), Is.True);
            File.WriteAllText(primary, "{\"version\":");
            using var restored = new OmsIrService(storage, http, credentials);
            Assert.That(restored.State.Enabled, Is.False);
            Assert.That(restored.State.PendingCount, Is.EqualTo(1));
            Assert.That(restored.PendingSubmissions.Single().SubmissionId, Is.EqualTo(play.SubmissionId));
            Assert.That(restored.State.Message, Does.Contain("恢复"));
            Assert.That(Directory.EnumerateFiles(pending, "*.corrupt-*"), Has.Exactly(1).Items);
            Assert.That(File.ReadAllText(Directory.EnumerateFiles(pending, "*.corrupt-*").Single()), Is.EqualTo("{\"version\":"));
        }

        [Test]
        public async Task FullyWrittenUnpublishedQueueIsRecoveredAfterRestart()
        {
            using var http = standardHttp();
            Guid id;
            using (var service = new OmsIrService(storage, http, credentials))
            {
                await service.ConfigureAsync(origin, true);
                await service.LoginAsync("player_one", password);
                OmsIrSubmissionTarget target = service.CaptureSubmissionTarget()!;
                await service.ConfigureAsync(origin, false);
                OmsIrSubmission play = submission();
                id = play.SubmissionId;
                await service.QueueSubmissionAsync(target, play);
            }
            string primary = Directory.EnumerateFiles(storage.GetFullPath("oms-ir/pending"), "*.json").Single();
            File.Move(primary, primary + ".tmp-" + Guid.NewGuid().ToString("N"));
            using var restored = new OmsIrService(storage, http, credentials);
            Assert.That(restored.State.PendingCount, Is.EqualTo(1));
            Assert.That(restored.PendingSubmissions.Single().SubmissionId, Is.EqualTo(id));
            Assert.That(File.Exists(primary), Is.True);
        }

        [Test]
        public async Task UnrecoverableQueueStopsIrAndPreservesTheOriginalFile()
        {
            using var http = standardHttp();
            using (var service = new OmsIrService(storage, http, credentials))
            {
                await service.ConfigureAsync(origin, true);
                await service.LoginAsync("player_one", password);
                OmsIrSubmissionTarget target = service.CaptureSubmissionTarget()!;
                await service.ConfigureAsync(origin, false);
                await service.QueueSubmissionAsync(target, submission());
            }
            string path = Directory.EnumerateFiles(storage.GetFullPath("oms-ir/pending"), "*.json").Single();
            const string damaged = "broken pending data";
            File.WriteAllText(path, damaged);
            using var stopped = new OmsIrService(storage, http, credentials);
            Assert.That(stopped.State.Enabled, Is.False);
            Assert.That(stopped.CaptureSubmissionTarget(), Is.Null);
            Assert.That(stopped.State.Message, Does.Contain("原文件"));
            Assert.That(File.ReadAllText(path), Is.EqualTo(damaged));
            OmsIrException exception = Assert.ThrowsAsync<OmsIrException>(async () => await stopped.QueueSubmissionAsync(new OmsIrSubmissionTarget(new Uri(origin), 1), submission()))!;
            Assert.That(exception.Code, Is.EqualTo("storage_error"));
        }

        [Test]
        public async Task InvalidSettingsDoNotEnableIrOrMasqueradeAsSuccessfulEmptyState()
        {
            string path = storage.GetFullPath("oms-ir/settings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            const string damaged = "{\"version\":1,\"service_address\":\"https://ir.example.test/\",\"enabled\":\"true\"}";
            File.WriteAllText(path, damaged);
            using var http = standardHttp();
            using var service = new OmsIrService(storage, http, credentials);
            Assert.That(service.State.Enabled, Is.False);
            Assert.That(service.State.Message, Does.Contain("暂停"));
            Assert.That(File.ReadAllText(path), Is.EqualTo(damaged));
            Assert.ThrowsAsync<OmsIrException>(async () => await service.ConfigureAsync(origin, true));
            await Task.CompletedTask;
        }

        [Test]
        public async Task PublicBoardIsAnExplicitReadAndOwnHistoryUsesBearer()
        {
            var requests = new ConcurrentQueue<(string Path, string? Bearer)>();
            using var http = new HttpClient(new Handler((request, _) =>
            {
                requests.Enqueue((request.RequestUri!.PathAndQuery, request.Headers.Authorization?.Parameter));
                return Task.FromResult(request.RequestUri.AbsolutePath.EndsWith("/auth/login", StringComparison.Ordinal)
                    ? json(loginResponse(1)) : json(new JObject { ["items"] = new JArray(), ["total"] = 0 }));
            }));
            using var service = new OmsIrService(storage, http, credentials);
            await service.ConfigureAsync(origin, true);
            await service.GetChartsAsync();
            await service.GetChartScoresAsync(new string('a', 32), new string('b', 64));
            await service.LoginAsync("player_one", password);
            await service.GetMyScoresAsync();
            Assert.That(requests.First(value => value.Path.Contains("/charts?", StringComparison.Ordinal)).Bearer, Is.Null);
            Assert.That(requests.First(value => value.Path.Contains("/scores/chart/", StringComparison.Ordinal)).Bearer, Is.Null);
            Assert.That(requests.Last().Path, Is.EqualTo("/api/ir/v1/scores/user/1?page=1&limit=20"));
            Assert.That(requests.Last().Bearer, Is.EqualTo(accessFor(1)));
            int count = requests.Count;
            await Task.Delay(100);
            Assert.That(requests.Count, Is.EqualTo(count));
        }

        [Test]
        public async Task IncorrectPasswordShowsAuthenticationFailureWithoutLeakingPassword()
        {
            using var http = new HttpClient(new Handler((_, _) => Task.FromResult(error(HttpStatusCode.Unauthorized, "invalid_credentials"))));
            using var service = new OmsIrService(storage, http, credentials);
            await service.ConfigureAsync(origin, true);
            OmsIrException exception = Assert.ThrowsAsync<OmsIrException>(async () => await service.LoginAsync("player_one", password))!;
            Assert.That(exception.Code, Is.EqualTo("invalid_credentials"));
            Assert.That(service.State.Message, Does.Contain("密码不正确"));
            Assert.That(service.State.Message, Does.Not.Contain(password));
            Assert.That(service.State.Account, Is.Null);
            Assert.That(credentials.Values, Is.Empty);
        }

        [Test]
        public async Task MismatchedAcknowledgementCannotDeletePending()
        {
            using var http = standardHttp((_, _) => Task.FromResult(acknowledge(submission().Payload, 2)));
            using var service = new OmsIrService(storage, http, credentials);
            await service.ConfigureAsync(origin, true);
            await service.LoginAsync("player_one", password);
            OmsIrSubmission play = submission();
            await service.QueueSubmissionAsync(service.CaptureSubmissionTarget()!, play);
            await waitUntil(() => service.State.Message.Contains("收据", StringComparison.Ordinal) && !service.State.Busy);
            Assert.That(service.PendingSubmissions.Single().SubmissionId, Is.EqualTo(play.SubmissionId));
            Assert.That(service.State.PendingCount, Is.EqualTo(1));
        }

        [Test]
        public async Task AccessRefreshRotatesStoredCredentialsAndRetainsTheSubmissionIdentity()
        {
            const string rotatedAccess = "rotated-access-player-0000000000";
            const string rotatedRefresh = "rotated-refresh-player-0000000000";
            int refreshes = 0;
            var attempts = new ConcurrentQueue<(string Body, string? Token)>();
            using var http = new HttpClient(new Handler(async (request, token) =>
            {
                string path = request.RequestUri!.AbsolutePath;
                if (path.EndsWith("/auth/login", StringComparison.Ordinal))
                    return json(loginResponse(1));
                if (path.EndsWith("/auth/refresh", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref refreshes);
                    Assert.That(request.Headers.Authorization, Is.Null);
                    JObject sent = JObject.Parse(await request.Content!.ReadAsStringAsync(token));
                    Assert.That((string?)sent["refresh_token"], Is.EqualTo(refreshFor(1)));
                    JObject refreshed = loginResponse(1);
                    refreshed["access_token"] = rotatedAccess;
                    refreshed["refresh_token"] = rotatedRefresh;
                    return json(refreshed);
                }
                string body = await request.Content!.ReadAsStringAsync(token);
                string? bearer = request.Headers.Authorization?.Parameter;
                attempts.Enqueue((body, bearer));
                return bearer == rotatedAccess ? acknowledge(JObject.Parse(body), 1) : error(HttpStatusCode.Unauthorized, "invalid_session");
            }));
            using var service = new OmsIrService(storage, http, credentials);
            await service.ConfigureAsync(origin, true);
            await service.LoginAsync("player_one", password);
            await service.QueueSubmissionAsync(service.CaptureSubmissionTarget()!, submission());
            await waitUntil(() => service.State.PendingCount == 0);
            Assert.That(refreshes, Is.EqualTo(1));
            Assert.That(attempts.Count, Is.EqualTo(2));
            Assert.That(attempts.ToArray()[1].Body, Is.EqualTo(attempts.ToArray()[0].Body));
            Assert.That(credentials.Values.Single().Value.AccessToken, Is.EqualTo(rotatedAccess));
            Assert.That(credentials.Values.Single().Value.RefreshToken, Is.EqualTo(rotatedRefresh));
        }

        [Test]
        public async Task RedirectIsReportedBeforeAnyCredentialForwarding()
        {
            var destinations = new ConcurrentQueue<string>();
            using var http = new HttpClient(new Handler((request, _) =>
            {
                destinations.Enqueue(request.RequestUri!.AbsoluteUri);
                if (request.RequestUri.AbsolutePath.EndsWith("/auth/login", StringComparison.Ordinal))
                    return Task.FromResult(json(loginResponse(1)));
                var redirect = new HttpResponseMessage(HttpStatusCode.Found);
                redirect.Headers.Location = new Uri("https://other.example.test/");
                return Task.FromResult(redirect);
            }));
            using var service = new OmsIrService(storage, http, credentials);
            await service.ConfigureAsync(origin, true);
            await service.LoginAsync("player_one", password);
            OmsIrException exception = Assert.ThrowsAsync<OmsIrException>(async () => await service.GetMyScoresAsync())!;
            Assert.That(exception.Code, Is.EqualTo("redirect_rejected"));
            Assert.That(destinations.All(destination => destination.StartsWith(origin, StringComparison.Ordinal)), Is.True);
        }

        [Test]
        [Platform("Win")]
        public void WindowsCredentialStoreRoundTripAndRemoval()
        {
            var native = new OmsIrCredentialStore();
            string target = "OMS.IR.synthetic-test:" + Guid.NewGuid().ToString("N");
            var value = new OmsIrSession(1, "player_one", accessFor(1), refreshFor(1), DateTimeOffset.UtcNow.AddMinutes(30));
            try
            {
                Assert.That(native.Read(target), Is.Null);
                native.Write(target, value);
                OmsIrSession restored = native.Read(target)!;
                Assert.That(restored.UserId, Is.EqualTo(value.UserId));
                Assert.That(restored.AccessToken, Is.EqualTo(value.AccessToken));
                Assert.That(restored.RefreshToken, Is.EqualTo(value.RefreshToken));
                native.Delete(target);
                Assert.That(native.Read(target), Is.Null);
            }
            finally
            {
                native.Delete(target);
            }
        }

        private static OmsIrSubmission submission()
        {
            Guid id = Guid.NewGuid();
            return new OmsIrSubmission(id, new JObject { ["submission_id"] = id.ToString(), ["marker"] = "synthetic saved play" });
        }

        private static async Task waitUntil(Func<bool> predicate)
        {
            var watch = Stopwatch.StartNew();
            while (!predicate())
            {
                if (watch.Elapsed > TimeSpan.FromSeconds(5))
                    Assert.Fail("The expected IR state did not arrive within the test budget.");
                await Task.Delay(10);
            }
        }

        private static HttpClient standardHttp(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? submit = null) =>
            new HttpClient(new Handler(async (request, token) =>
            {
                string path = request.RequestUri!.AbsolutePath;
                if (path.EndsWith("/auth/login", StringComparison.Ordinal) || path.EndsWith("/auth/register", StringComparison.Ordinal))
                {
                    string username = (string)JObject.Parse(await request.Content!.ReadAsStringAsync(token))["username"]!;
                    return json(loginResponse(username == "player_two" ? 2 : 1));
                }
                if (path.EndsWith("/scores/submit", StringComparison.Ordinal))
                    return submit != null ? await submit(request, token) : acknowledge(JObject.Parse(await request.Content!.ReadAsStringAsync(token)), 1);
                if (path.EndsWith("/auth/logout", StringComparison.Ordinal))
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                if (path.EndsWith("/auth/refresh", StringComparison.Ordinal))
                    return json(loginResponse(1));
                return json(new JObject { ["items"] = new JArray(), ["total"] = 0 });
            }));

        private static string accessFor(long id) => "access-player-" + id + "-0000000000000000";
        private static string refreshFor(long id) => "refresh-player-" + id + "-0000000000000000";

        private static JObject loginResponse(long id) => new JObject
        {
            ["user"] = new JObject { ["id"] = id, ["username"] = id == 1 ? "player_one" : "player_two" },
            ["access_token"] = accessFor(id),
            ["refresh_token"] = refreshFor(id),
            ["expires_in"] = 3600,
        };

        private static HttpResponseMessage acknowledge(JObject payload, long owner, bool duplicate = false) => json(new JObject
        {
            ["duplicate"] = duplicate,
            ["score"] = new JObject { ["id"] = 17, ["user_id"] = owner, ["submission_id"] = (string)payload["submission_id"]! },
        }, duplicate ? HttpStatusCode.OK : HttpStatusCode.Created);

        private static HttpResponseMessage error(HttpStatusCode status, string code) =>
            json(new JObject { ["error"] = new JObject { ["code"] = code, ["message"] = "synthetic test error" } }, status);

        private static HttpResponseMessage json(JObject document, HttpStatusCode status = HttpStatusCode.OK) => new HttpResponseMessage(status)
        {
            Content = new StringContent(document.ToString(Formatting.None), Encoding.UTF8, "application/json"),
        };

        private sealed class Handler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response;
            public Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) => this.response = response;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => response(request, cancellationToken);
        }

        private sealed class MemoryCredentials : IOmsIrCredentialStore
        {
            public Dictionary<string, OmsIrSession> Values { get; } = new Dictionary<string, OmsIrSession>();
            public bool FailDelete { get; set; }
            public OmsIrSession? Read(string target) => Values.GetValueOrDefault(target);
            public void Write(string target, OmsIrSession session) => Values[target] = session;
            public void Delete(string target)
            {
                if (FailDelete)
                    throw new Win32Exception(5, "Synthetic Windows credential deletion denied.");
                Values.Remove(target);
            }
        }

        private sealed class BrokenResponseStream : Stream
        {
            private static readonly byte[] prefix = Encoding.UTF8.GetBytes("{\"duplicate\":false,\"score\":");
            private bool prefixRead;
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (prefixRead)
                    throw new IOException("A synthetic connection closed midway through the response body.");
                prefix.AsSpan().CopyTo(buffer.Span);
                prefixRead = true;
                return ValueTask.FromResult(prefix.Length);
            }

            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override void Flush() => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin seekOrigin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
