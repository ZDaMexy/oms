// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Platform;

namespace osu.Game.Online.IR
{
    /// <summary>The OMS account client. It does not enable or consume the legacy osu! API.</summary>
    public sealed class OmsIrService : IDisposable
    {
        public static Uri ServiceOrigin { get; } = new Uri("https://oms.zdamexy.work/");

        private const int maximum_response_bytes = 4 * 1024 * 1024;
        private static readonly TimeSpan[] retry_delays = { TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(45), TimeSpan.FromSeconds(120) };

        // HTTP ownership and durable local writes have separate gates: a finish must never wait for a login request.
        private readonly SemaphoreSlim networkSlot = new SemaphoreSlim(1, 1);
        private readonly object queueLock = new object();
        private readonly object stateLock = new object();
        private readonly object workerLock = new object();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly HttpClient http;
        private readonly bool ownsHttp;
        private readonly IOmsIrCredentialStore credentials;
        private readonly string credentialScope;
        private readonly OmsIrQueue? queue;
        private OmsIrSession? session;
        private bool requiresLogin;
        private bool busy;
        private string message = "登录 OMS 账号后上传新成绩，离线游玩无需账号。";
        private string? storageFailure;
        private Task? worker;
        private CancellationTokenSource? retryDelay;
        private volatile bool disposed;
        private OmsIrState state = new OmsIrState(ServiceOrigin.AbsoluteUri, false, null, 0, 0, 0, false, false, "登录 OMS 账号后上传新成绩，离线游玩无需账号。");

        /// <summary>Published from the completing thread. Drawables must schedule their own updates.</summary>
        public event Action<OmsIrState>? StateChanged;

        public OmsIrState State => Volatile.Read(ref state);

        public IReadOnlyList<OmsIrPendingSubmission> PendingSubmissions
        {
            get
            {
                lock (queueLock)
                    return queue?.Entries.Select(entry => new OmsIrPendingSubmission(entry.SubmissionId, entry.Target, entry.BlockedReason)).ToArray()
                           ?? Array.Empty<OmsIrPendingSubmission>();
            }
        }

        public OmsIrService(Storage storage, HttpClient? httpClient = null, IOmsIrCredentialStore? credentialStore = null)
        {
            credentialScope = hash(storage.GetFullPath(string.Empty).ToUpperInvariant());
            credentials = credentialStore ?? new OmsIrCredentialStore();
            ownsHttp = httpClient == null;
            http = httpClient ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
            {
                Timeout = TimeSpan.FromSeconds(20),
            };

            try
            {
                Storage irStorage = storage.GetStorageForDirectory("oms-ir");
                queue = new OmsIrQueue(irStorage);
                if (queue.RecoveryNotice != null)
                    message = queue.RecoveryNotice;
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                storageFailure = "待上传成绩无法读取，已暂停上传并保留原文件。本地游玩与保存照常。";
                message = storageFailure;
            }

            if (storageFailure == null)
            {
                try
                {
                    session = credentials.Read(credentialTarget(ServiceOrigin));
                    if (session != null && queue?.RecoveryNotice == null)
                        message = "已登录；原账号的待上传成绩会继续上传。";
                }
                catch (Exception exception) when (exception is Win32Exception or IOException or InvalidDataException or PlatformNotSupportedException)
                {
                    requiresLogin = true;
                    message = "Windows 无法读取 IR 凭据，请重新登录；待交和本地成绩已保留。";
                }
            }

            publish();
            wakeWorker();
        }

        /// <summary>Validates origins retained in existing submission records, never an API URL.</summary>
        public static Uri ParseServiceAddress(string address)
        {
            if (!Uri.TryCreate(address.Trim(), UriKind.Absolute, out Uri? uri) || !string.IsNullOrEmpty(uri.UserInfo)
                || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath != "/"
                || (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)))
                throw new OmsIrException("invalid_address", "请填写 HTTPS 站点地址，不含 /ir、API 路径或账号信息；本机试验可用 loopback HTTP。");

            return new Uri(uri.GetLeftPart(UriPartial.Authority) + "/");
        }

        public OmsIrSubmissionTarget? CaptureSubmissionTarget()
        {
            OmsIrState snapshot = State;
            return snapshot.Enabled && !snapshot.RequiresLogin && snapshot.Account != null
                ? new OmsIrSubmissionTarget(new Uri(snapshot.ServiceAddress), snapshot.Account.Id) : null;
        }

        public Task LoginAsync(string username, string password, bool register = false, CancellationToken cancellationToken = default) => withNetworkAsync(async token =>
        {
            Uri origin = requireOrigin();
            if (username.Length is < 3 or > 24 || !username.All(character => character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')
                || password.Length is < 10 or > 128)
                throw new OmsIrException("invalid_credentials_format", "账号为 3–24 位字母、数字或下划线；密码为 10–128 个字符。密码不会被裁剪。");

            var body = new JObject { ["username"] = username, ["password"] = password, ["transport"] = "desktop" };
            JObject response = await sendAsync(origin, HttpMethod.Post, register ? "auth/register" : "auth/login", body.ToString(Formatting.None), null, token).ConfigureAwait(false);
            OmsIrSession loggedIn = decodeSession(response);
            // A response can finish after the account panel closed, even when HTTP observes the cancellation late.
            token.ThrowIfCancellationRequested();
            writeCredential(origin, loggedIn);
            lock (stateLock)
            {
                session = loggedIn;
                requiresLogin = false;
                message = "已登录；本局选定账号的成绩会在本地保存后补交。";
            }
            wakeWorker();
            return true;
        }, cancellationToken);

        public Task LogoutAsync(CancellationToken cancellationToken = default) => withNetworkAsync(async token =>
        {
            Uri origin = ServiceOrigin;
            OmsIrSession? previous = session;
            string resultMessage = "已退出登录；待交仍绑定原账号，本地游玩与保存照常。";
            if (previous != null)
            {
                try
                {
                    await sendAsync(origin, HttpMethod.Post, "auth/logout", "{}", previous.AccessToken, token).ConfigureAwait(false);
                }
                catch (OmsIrException exception)
                {
                    resultMessage = exception.StatusCode == HttpStatusCode.Unauthorized
                        ? "已退出登录；远端会话已失效，原账号待交保留。"
                        : "已退出本机登录；未能确认远端会话撤销，原账号待交保留。";
                }
                deleteCredential(origin);
            }
            lock (stateLock)
            {
                session = null;
                requiresLogin = false;
                message = resultMessage;
            }
            wakeWorker();
            return true;
        }, cancellationToken);

        /// <summary>Completes after local atomic publication, even if another account now owns the UI or HTTP is busy.</summary>
        public async Task QueueSubmissionAsync(OmsIrSubmissionTarget target, OmsIrSubmission submission, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
            await Task.Run(() =>
            {
                linked.Token.ThrowIfCancellationRequested();
                lock (queueLock)
                {
                    if (queue == null || storageFailure != null)
                        throw new OmsIrException("storage_error", storageFailure ?? "IR 待交存储不可用；本地成绩已保存。");
                    try
                    {
                        queue.Enqueue(target, submission);
                    }
                    catch (InvalidDataException exception)
                    {
                        throw new OmsIrException("invalid_submission", "本局待交字段或大小不符合要求；本地成绩与原待交已保留。", innerException: exception);
                    }
                }
            }, linked.Token).ConfigureAwait(false);
            setMessage("本地成绩已保存，IR 待交已保全并绑定开局时的账号。");
            publish();
            wakeWorker();
        }

        public Task RetryPendingAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            OmsIrState snapshot = State;
            if (!snapshot.Enabled || snapshot.Account == null || snapshot.RequiresLogin)
                throw new OmsIrException("login_required", "请登录原账号后重试。待上传成绩仍已保留。");

            lock (queueLock)
            {
                if (queue == null || storageFailure != null)
                    throw new OmsIrException("storage_error", storageFailure ?? "IR 待交存储不可用。");
                foreach (OmsIrQueueEntry entry in queue.Entries.Where(entry => matches(entry, new Uri(snapshot.ServiceAddress), snapshot.Account.Id)).ToArray())
                    queue.Replace(entry with { Attempts = 0, NextAttempt = DateTimeOffset.UtcNow, BlockedReason = null });
            }
            setMessage("已安排原账号待交重试；其他账号的待交不会改归属。");
            publish();
            wakeWorker();
            return Task.CompletedTask;
        }

        public void ReportLocalFailure(string failure)
        {
            setMessage(failure);
            publish();
        }

        public Task<JObject> GetChartsAsync(int page = 1, CancellationToken cancellationToken = default) => withNetworkAsync(token =>
            sendAsync(requireOrigin(), HttpMethod.Get, "charts?page=" + validPage(page) + "&limit=20", null, null, token), cancellationToken);

        public Task<JObject> GetChartScoresAsync(string md5, string group, int page = 1, CancellationToken cancellationToken = default)
        {
            if (md5.Length != 32 || group.Length != 64 || !md5.Concat(group).All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'))
                throw new OmsIrException("invalid_board", "请先选择谱面和游玩条件。");
            return withNetworkAsync(token => sendAsync(requireOrigin(), HttpMethod.Get,
                "scores/chart/" + md5 + "?group=" + group + "&page=" + validPage(page) + "&limit=20", null, null, token), cancellationToken);
        }

        public Task<JObject> GetSourcesAsync(CancellationToken cancellationToken = default) => withNetworkAsync(token =>
            sendAsync(requireOrigin(), HttpMethod.Get, "sources", null, null, token, "v2"), cancellationToken);

        public Task<JObject> GetSourceChartsAsync(string query = "", int page = 1, CancellationToken cancellationToken = default)
        {
            if (query.Length > 200)
                throw new OmsIrException("invalid_search", "请将谱名、作者或原谱 MD5 限制在 200 字以内。");
            return withNetworkAsync(token => sendAsync(requireOrigin(), HttpMethod.Get,
                "charts?q=" + Uri.EscapeDataString(query) + "&page=" + validPage(page) + "&limit=20", null, null, token, "v2"), cancellationToken);
        }

        public Task<JObject> GetSourceChartAsync(string md5, CancellationToken cancellationToken = default)
        {
            validateChartMd5(md5);
            return withNetworkAsync(token => sendAsync(requireOrigin(), HttpMethod.Get, "charts/" + md5, null, null, token, "v2"), cancellationToken);
        }

        public Task<JObject> GetSourceChartScoresAsync(string md5, IReadOnlyList<string>? sources = null, string mode = "reference",
                                                      string? condition = null, int page = 1, CancellationToken cancellationToken = default)
        {
            validateChartMd5(md5);
            if (mode is not ("reference" or "comparable") || (mode == "comparable" && string.IsNullOrEmpty(condition))
                || (mode == "reference" && condition != null) || condition?.Length > 100)
                throw new OmsIrException("invalid_conditions", "请主动选择同条件组，或返回参考混榜。");
            if (sources != null && (sources.Count > 16 || sources.Any(source => source.Length is < 1 or > 32
                    || !source.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '.'))))
                throw new OmsIrException("invalid_sources", "请选择服务提供的来源。");
            string path = "scores/chart/" + md5 + "?mode=" + mode + "&page=" + validPage(page) + "&limit=20";
            if (sources != null)
                path += "&sources=" + Uri.EscapeDataString(string.Join(',', sources.Distinct(StringComparer.Ordinal)));
            if (condition != null)
                path += "&condition=" + Uri.EscapeDataString(condition);
            return withNetworkAsync(token => session != null
                ? authenticatedAsync(HttpMethod.Get, path, null, token, "v2")
                : sendAsync(requireOrigin(), HttpMethod.Get, path, null, null, token, "v2"), cancellationToken);
        }

        private static void validateChartMd5(string md5)
        {
            if (md5.Length != 32 || !md5.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'))
                throw new OmsIrException("invalid_chart", "请先选择具有原谱内容身份的谱面。");
        }

        public Task<JObject> GetMyScoresAsync(int page = 1, CancellationToken cancellationToken = default) => withNetworkAsync(token =>
        {
            requireOrigin();
            OmsIrSession owner = requireSession();
            return authenticatedAsync(HttpMethod.Get, "scores/user/" + owner.UserId.ToString(CultureInfo.InvariantCulture)
                + "?page=" + validPage(page) + "&limit=20", null, token);
        }, cancellationToken);

        private async Task<T> withNetworkAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
            await networkSlot.WaitAsync(linked.Token).ConfigureAwait(false);
            try
            {
                setBusy(true);
                return await operation(linked.Token).ConfigureAwait(false);
            }
            catch (OmsIrException exception)
            {
                setMessage(exception.Message);
                throw;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                const string failure = "IR 数据无法安全保存；原文件和本地成绩已保留。请检查保存目录。";
                setMessage(failure);
                throw new OmsIrException("storage_error", failure, innerException: exception);
            }
            finally
            {
                setBusy(false);
                networkSlot.Release();
            }
        }

        private Uri requireOrigin()
        {
            if (storageFailure != null)
                throw new OmsIrException("storage_error", storageFailure);
            return ServiceOrigin;
        }

        private OmsIrSession requireSession() => session ?? throw new OmsIrException("login_required", "请登录原账号；待交仍已保留。");

        private async Task<JObject> authenticatedAsync(HttpMethod method, string path, string? body, CancellationToken token, string apiVersion = "v1")
        {
            Uri origin = requireOrigin();
            OmsIrSession owner = requireSession();
            if (owner.AccessExpiresAt <= DateTimeOffset.UtcNow)
                owner = await refreshAsync(origin, owner, token).ConfigureAwait(false);
            try
            {
                return await sendAsync(origin, method, path, body, owner.AccessToken, token, apiVersion).ConfigureAwait(false);
            }
            catch (OmsIrException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
            {
                owner = await refreshAsync(origin, owner, token).ConfigureAwait(false);
                try
                {
                    return await sendAsync(origin, method, path, body, owner.AccessToken, token, apiVersion).ConfigureAwait(false);
                }
                catch (OmsIrException retryException) when (retryException.StatusCode == HttpStatusCode.Unauthorized)
                {
                    invalidateSession(origin);
                    throw new OmsIrException("login_required", "登录已失效，请重新登录原账号；待交仍已保留。", HttpStatusCode.Unauthorized);
                }
            }
        }

        private async Task<OmsIrSession> refreshAsync(Uri origin, OmsIrSession previous, CancellationToken token)
        {
            JObject response;
            try
            {
                response = await sendAsync(origin, HttpMethod.Post, "auth/refresh",
                    new JObject { ["refresh_token"] = previous.RefreshToken }.ToString(Formatting.None), null, token).ConfigureAwait(false);
            }
            catch (OmsIrException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
            {
                invalidateSession(origin);
                throw new OmsIrException("login_required", "登录已失效，请重新登录原账号；待交仍已保留。", HttpStatusCode.Unauthorized);
            }

            OmsIrSession refreshed = decodeSession(response);
            if (refreshed.UserId != previous.UserId)
            {
                invalidateSession(origin);
                throw new OmsIrException("session_identity_changed", "服务返回了不同账号，会话已停止；请重新登录原账号，待交仍已保留。", HttpStatusCode.Unauthorized);
            }
            writeCredential(origin, refreshed);
            lock (stateLock)
                session = refreshed;
            return refreshed;
        }

        private void invalidateSession(Uri origin)
        {
            lock (stateLock)
            {
                session = null;
                requiresLogin = true;
            }
            deleteCredential(origin);
        }

        private async Task<JObject> sendAsync(Uri origin, HttpMethod method, string path, string? body, string? accessToken, CancellationToken token, string apiVersion = "v1")
        {
            using var requestDeadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            requestDeadline.CancelAfter(TimeSpan.FromSeconds(20));
            CancellationToken requestToken = requestDeadline.Token;
            using var request = new HttpRequestMessage(method, new Uri(origin, "api/ir/" + apiVersion + "/" + path));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (accessToken != null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            if (body != null)
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            try
            {
                using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestToken).ConfigureAwait(false);
                if ((int)response.StatusCode is >= 300 and < 400)
                    throw new OmsIrException("redirect_rejected", "OMSIR 返回了异常跳转，登录信息不会被转发。请稍后重试。", response.StatusCode);
                if (response.StatusCode == HttpStatusCode.NoContent)
                    return new JObject();
                if (response.Content.Headers.ContentLength > maximum_response_bytes)
                    throw new OmsIrException("invalid_response", "IR 响应超过预算，待交会保留。", response.StatusCode);

                using Stream input = await response.Content.ReadAsStreamAsync(requestToken).ConfigureAwait(false);
                using var output = new MemoryStream();
                byte[] buffer = new byte[16 * 1024];
                int read;
                while ((read = await input.ReadAsync(buffer, requestToken).ConfigureAwait(false)) > 0)
                {
                    if (output.Length + read > maximum_response_bytes)
                        throw new OmsIrException("invalid_response", "IR 响应超过预算，待交会保留。", response.StatusCode);
                    output.Write(buffer, 0, read);
                }

                JObject document;
                try
                {
                    document = OmsIrJson.Parse(new UTF8Encoding(false, true).GetString(output.GetBuffer(), 0, (int)output.Length));
                }
                catch (Exception exception) when (exception is InvalidDataException or DecoderFallbackException)
                {
                    throw new OmsIrException("invalid_response", "IR 返回内容无法读取，待交会保留。", response.StatusCode, innerException: exception);
                }

                if (!response.IsSuccessStatusCode)
                {
                    string code = document["error"] is JObject error && error["code"]?.Type == JTokenType.String
                        ? OmsIrJson.String(error, "code") : "http_error";
                    TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta;
                    if (retryAfter == null && response.Headers.RetryAfter?.Date is DateTimeOffset date)
                        retryAfter = date - DateTimeOffset.UtcNow;
                    throw new OmsIrException(code, errorMessage(code, response.StatusCode), response.StatusCode, retryAfter);
                }
                return document;
            }
            catch (HttpRequestException exception)
            {
                token.ThrowIfCancellationRequested();
                throw new OmsIrException("connection_failed", "暂时连不上 IR；本地成绩与待交已保留。", innerException: exception);
            }
            catch (IOException exception)
            {
                token.ThrowIfCancellationRequested();
                throw new OmsIrException("connection_failed", "IR 响应中途断开；本地成绩与待交已保留。", innerException: exception);
            }
            catch (OperationCanceledException exception) when (!token.IsCancellationRequested)
            {
                throw new OmsIrException("request_timeout", "IR 请求超时；本地成绩与待交已保留。", innerException: exception);
            }
        }

        private static OmsIrSession decodeSession(JObject document)
        {
            try
            {
                if (document["user"] is not JObject user)
                    throw new InvalidDataException("Missing account.");
                long id = OmsIrJson.Integer(user, "id");
                string username = OmsIrJson.String(user, "username");
                string access = OmsIrJson.String(document, "access_token");
                string refresh = OmsIrJson.String(document, "refresh_token");
                long expires = OmsIrJson.Integer(document, "expires_in");
                if (expires is < 1 or > 3600)
                    throw new InvalidDataException("Invalid account or token fields.");
                return new OmsIrSession(id, username, access, refresh, DateTimeOffset.UtcNow.AddSeconds(expires));
            }
            catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
            {
                throw new OmsIrException("invalid_response", "IR 登录响应不完整，请检查服务；凭据未保存。", innerException: exception);
            }
        }

        private void writeCredential(Uri origin, OmsIrSession value)
        {
            try
            {
                credentials.Write(credentialTarget(origin), value);
            }
            catch (Exception exception) when (exception is Win32Exception or IOException or InvalidDataException or PlatformNotSupportedException)
            {
                throw new OmsIrException("credential_store", "Windows 无法保存 IR 凭据，此次登录未在本机生效。", innerException: exception);
            }
        }

        private void deleteCredential(Uri origin)
        {
            try
            {
                credentials.Delete(credentialTarget(origin));
            }
            catch (Exception exception) when (exception is Win32Exception or IOException or InvalidDataException or PlatformNotSupportedException)
            {
                throw new OmsIrException("credential_store", "Windows 无法清除 IR 凭据，请检查凭据管理器后重试。", innerException: exception);
            }
        }

        private string credentialTarget(Uri origin) => "OMS.IR.v1:" + credentialScope + ":" + hash(origin.AbsoluteUri);
        private static string hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
        private static string validPage(int page) => page > 0 ? page.ToString(CultureInfo.InvariantCulture)
            : throw new OmsIrException("invalid_page", "页码必须从 1 开始。");

        private static string errorMessage(string code, HttpStatusCode status) => code switch
        {
            "invalid_credentials" => "账号名字或密码不正确。",
            "username_taken" => "账号名字已被使用。",
            "invalid_session" or "invalid_refresh" => "登录已失效，请重新登录原账号；待交仍已保留。",
            "rate_limited" or "password_busy" => "IR 服务正忙，请稍后重试。",
            "unsupported_mod" => "本局的 Mod 或参数暂不支持；本地成绩与待交已保留。",
            "invalid_mod_conditions" => "本局的选项与保存后的实际规则不一致；本地成绩与待交已保留。",
            "unsupported_rules_version" or "unsupported_score_version" or "unsupported_schema" => "本局的成绩版本暂不支持；本地成绩与待交已保留。",
            "submission_conflict" => "同一局 ID 已有不同内容；本地成绩与待交已保留，请检查来源。",
            "chart_identity_conflict" => "谱面 MD5 对应的内容或玩法不同；本地成绩与待交已保留。",
            "history_private" => "完整记录仅账号本人可查看。",
            "board_not_found" => "这个游玩条件还没有公开榜单。",
            "chart_not_found" => "这个谱面还没有成绩或历史目录。",
            "unsupported_source" => "所选来源未知或尚未开放，请重新读取来源列表。",
            "condition_required" or "unexpected_condition" => "请主动选择已证明的同条件组，或返回参考混榜。",
            "bms_reference_only" => "参考混榜用于 BMS；mania 请使用原计分条件榜。",
            "archive_unavailable" or "archive_version" => "历史榜暂时无法安全读取，请联系维护者；这不是成功的空榜。",
            _ => status == HttpStatusCode.UnprocessableEntity ? "成绩字段未通过服务检查；本地成绩与待交已保留。"
                : "IR 请求失败（HTTP " + ((int)status).ToString(CultureInfo.InvariantCulture) + "）；本地成绩与待交已保留。",
        };

        private void setMessage(string value)
        {
            lock (stateLock)
                message = value;
        }

        private void setBusy(bool value)
        {
            lock (stateLock)
                busy = value;
            publish();
        }

        private void publish()
        {
            OmsIrState snapshot;
            lock (stateLock)
            {
                lock (queueLock)
                {
                    OmsIrQueueEntry[] entries = queue?.Entries.ToArray() ?? Array.Empty<OmsIrQueueEntry>();
                    snapshot = new OmsIrState(ServiceOrigin.AbsoluteUri, session != null && !requiresLogin && storageFailure == null,
                        session == null ? null : new OmsIrAccount(session.UserId, session.Username),
                        entries.Length, entries.Count(entry => entry.BlockedReason != null),
                        entries.Count(entry => session == null || !matches(entry, ServiceOrigin, session.UserId)), requiresLogin, busy, message);
                }
                Volatile.Write(ref state, snapshot);
            }
            StateChanged?.Invoke(snapshot);
        }

        private static bool matches(OmsIrQueueEntry entry, Uri origin, long userId) => entry.Target.ServiceUri == origin && entry.Target.UserId == userId;

        // These checks always run under workerLock, so an enqueue cannot be lost as the worker becomes idle.
        private OmsIrQueueEntry? nextEntry()
        {
            lock (stateLock)
            {
                if (session == null || requiresLogin || storageFailure != null)
                    return null;
                lock (queueLock)
                    return queue?.Entries.Where(entry => entry.BlockedReason == null && entry.Attempts < 5 && matches(entry, ServiceOrigin, session.UserId))
                                .OrderBy(entry => entry.NextAttempt).ThenBy(entry => entry.SubmissionId).FirstOrDefault();
            }
        }

        private void wakeWorker()
        {
            lock (workerLock)
            {
                retryDelay?.Cancel();
                if (!disposed && (worker == null || worker.IsCompleted) && nextEntry() != null)
                    worker = Task.Run(processPendingAsync);
            }
        }

        private async Task processPendingAsync()
        {
            try
            {
                while (!lifetime.IsCancellationRequested)
                {
                    OmsIrQueueEntry? entry;
                    CancellationTokenSource? delay = null;
                    lock (workerLock)
                    {
                        entry = nextEntry();
                        if (entry == null)
                        {
                            worker = null;
                            return;
                        }
                        if (entry.NextAttempt > DateTimeOffset.UtcNow)
                            retryDelay = delay = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                    }

                    if (delay != null)
                    {
                        try
                        {
                            TimeSpan remaining = entry.NextAttempt - DateTimeOffset.UtcNow;
                            if (remaining > TimeSpan.Zero)
                                await Task.Delay(TimeSpan.FromSeconds(Math.Min(remaining.TotalSeconds, 300)), delay.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (delay.IsCancellationRequested)
                        {
                            // A manual retry, account change, or shutdown wakes the one real pending queue.
                        }
                        finally
                        {
                            lock (workerLock)
                            {
                                retryDelay = null;
                                delay.Dispose();
                            }
                        }
                        continue;
                    }

                    await networkSlot.WaitAsync(lifetime.Token).ConfigureAwait(false);
                    try
                    {
                        // The account may have changed while this request waited behind a login or query.
                        lock (workerLock)
                            entry = nextEntry();
                        if (entry == null || entry.NextAttempt > DateTimeOffset.UtcNow)
                            continue;
                        setMessage("正在补交已保存的成绩……");
                        setBusy(true);
                        try
                        {
                            JObject response = await authenticatedAsync(HttpMethod.Post, "scores/submit", entry.Body, lifetime.Token).ConfigureAwait(false);
                            validateAcknowledgement(response, entry);
                            lock (queueLock)
                                queue!.Remove(entry);
                            setMessage("IR 已收到本局成绩；重复发送也只保留同一局。");
                        }
                        catch (OmsIrException exception)
                        {
                            setMessage(exception.Message);
                            if (exception.StatusCode != HttpStatusCode.Unauthorized)
                            {
                                bool permanent = (int?)exception.StatusCode is >= 400 and < 500
                                    && exception.StatusCode != HttpStatusCode.TooManyRequests;
                                try
                                {
                                    lock (queueLock)
                                    {
                                        // Manual retry may have reset attempts while this HTTP request was in flight.
                                        OmsIrQueueEntry current = queue!.Get(entry);
                                        int attempts = current.Attempts + 1;
                                        TimeSpan wait = exception.RetryAfter ?? (attempts <= retry_delays.Length ? retry_delays[attempts - 1] : TimeSpan.Zero);
                                        wait = TimeSpan.FromSeconds(Math.Clamp(wait.TotalSeconds, 1, 300));
                                        string? reason = permanent ? exception.Code + "：" + exception.Message
                                            : attempts >= 5 ? "已暂停自动补交：" + exception.Message + " 请手动重试。" : null;
                                        queue.Replace(current with { Attempts = attempts, NextAttempt = DateTimeOffset.UtcNow + wait, BlockedReason = reason });
                                    }
                                }
                                catch (Exception storageException) when (storageException is IOException or UnauthorizedAccessException)
                                {
                                    stopForStorageFailure();
                                }
                            }
                        }
                        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                        {
                            stopForStorageFailure();
                        }
                    }
                    finally
                    {
                        setBusy(false);
                        networkSlot.Release();
                    }
                }
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
                // Every enqueued payload was already atomically published before the request started.
            }
            lock (workerLock)
                worker = null;
        }

        private static void validateAcknowledgement(JObject response, OmsIrQueueEntry entry)
        {
            try
            {
                if (response["duplicate"]?.Type != JTokenType.Boolean || response["score"] is not JObject score
                    || OmsIrJson.Integer(score, "id") <= 0 || OmsIrJson.Integer(score, "user_id") != entry.Target.UserId
                    || OmsIrJson.String(score, "submission_id") != entry.SubmissionId.ToString())
                    throw new InvalidDataException("The OMS IR acknowledgement has a different owner or submission.");
            }
            catch (InvalidDataException exception)
            {
                throw new OmsIrException("invalid_response", "IR 收据与本局不一致；待交保留，仍用原 ID 重试。", innerException: exception);
            }
        }

        private void stopForStorageFailure()
        {
            lock (stateLock)
            {
                storageFailure = "IR 待交无法安全写入，已暂停 IR 并保留原文件；本地成绩不受影响。";
                message = storageFailure;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            lifetime.Cancel();
            lock (workerLock)
                retryDelay?.Cancel();
            if (ownsHttp)
                http.Dispose();
        }
    }
}
