// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;

namespace osu.Game.Online.Sayobot
{
    public enum ManiaDownloadState
    {
        Queued,
        Downloading,
        Importing,
        Completed,
        Cancelled,
        Failed,
    }

    public record ManiaDownloadProgress(ManiaDownloadState State, long Bytes = 0, long? TotalBytes = null,
                                       IReadOnlyList<ManiaDownloadImportedBeatmap>? Imported = null);

    public sealed class ManiaDownloadTask
    {
        private readonly object cancellationLock = new object();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private bool finished;
        private ManiaDownloadProgress progress = new ManiaDownloadProgress(ManiaDownloadState.Queued);

        public SayobotBeatmapSet Set { get; }
        public int RequestedBeatmapId { get; }
        public ManiaDownloadProgress Progress => Volatile.Read(ref progress);
        public Task Completion { get; internal set; } = Task.CompletedTask;
        internal CancellationToken Token => cancellation.Token;

        internal ManiaDownloadTask(SayobotBeatmapSet set, int requestedBeatmapId)
        {
            Set = set;
            RequestedBeatmapId = requestedBeatmapId;
        }

        public void Cancel()
        {
            lock (cancellationLock)
            {
                if (!finished)
                    cancellation.Cancel();
            }
        }

        internal void Update(ManiaDownloadProgress value) => Volatile.Write(ref progress, value);

        internal void Finish()
        {
            lock (cancellationLock)
            {
                finished = true;
                cancellation.Dispose();
            }
        }
    }

    /// <summary>
    /// Game-owned, single-stream Sayobot downloads. Closing the browser only stops observing tasks.
    /// </summary>
    public sealed class ManiaDownloadManager : IDisposable
    {
        public const long MaximumPackageBytes = 2L * 1024 * 1024 * 1024;

        private readonly object taskLock = new object();
        private readonly Dictionary<string, ManiaDownloadTask> tasks = new Dictionary<string, ManiaDownloadTask>();
        private readonly SemaphoreSlim downloadSlot = new SemaphoreSlim(1);
        private readonly Storage storage;
        private readonly IManiaDownloadImporter importer;
        private readonly HttpClient downloads;
        private bool disposed;

        public SayobotClient Client { get; }
        public event Action<ManiaDownloadTask>? TaskChanged;

        public ManiaDownloadManager(Storage storage, IManiaDownloadImporter importer, SayobotClient? client = null, HttpClient? downloads = null)
        {
            this.storage = storage;
            this.importer = importer;
            Client = client ?? new SayobotClient();
            this.downloads = downloads ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            {
                Timeout = TimeSpan.FromMinutes(30),
            };
        }

        public ManiaDownloadTask? GetTask(string key)
        {
            lock (taskLock)
                return tasks.GetValueOrDefault(key);
        }

        public ManiaDownloadTask Download(SayobotBeatmapSet set, int requestedBeatmapId)
        {
            if (!set.Beatmaps.Any(b => b.Id == requestedBeatmapId))
                throw new ArgumentException("The requested difficulty is not part of the selected set.", nameof(requestedBeatmapId));

            ManiaDownloadTask task;

            lock (taskLock)
            {
                ObjectDisposedException.ThrowIf(disposed, this);

                if (tasks.TryGetValue(set.Key, out var existing)
                    && existing.Progress.State is ManiaDownloadState.Queued or ManiaDownloadState.Downloading or ManiaDownloadState.Importing)
                    return existing;

                tasks[set.Key] = task = new ManiaDownloadTask(set, requestedBeatmapId);
                task.Completion = Task.Run(() => runAsync(task));
            }

            TaskChanged?.Invoke(task);
            return task;
        }

        private async Task runAsync(ManiaDownloadTask task)
        {
            bool entered = false;
            string? staging = null;
            ManiaDownloadProgress? terminal = null;
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(task.Token);
            CancellationToken token = budget.Token;

            try
            {
                await downloadSlot.WaitAsync(task.Token).ConfigureAwait(false);
                entered = true;
                budget.CancelAfter(TimeSpan.FromMinutes(30));

                staging = storage.GetFullPath(Path.Combine("mania-downloads", Guid.NewGuid().ToString("N")));
                Directory.CreateDirectory(staging);
                string archivePath = Path.Combine(staging, "package.osz");

                update(task, new ManiaDownloadProgress(ManiaDownloadState.Downloading));
                using (var response = await requestPackage(task.Set.DownloadUrl, token).ConfigureAwait(false))
                {
                    long? length = response.Content.Headers.ContentLength;
                    if (length > MaximumPackageBytes || length == 0)
                        throw new InvalidDataException("Invalid package size.");

                    update(task, new ManiaDownloadProgress(ManiaDownloadState.Downloading, TotalBytes: length));

                    using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                    using var output = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true);
                    byte[] buffer = new byte[65536];
                    long copied = 0;
                    long nextReport = 0;

                    int read;
                    while ((read = await input.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
                    {
                        copied = checked(copied + read);
                        if (copied > MaximumPackageBytes)
                            throw new InvalidDataException("The package exceeds the download size limit.");

                        await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);

                        if (copied >= nextReport)
                        {
                            update(task, new ManiaDownloadProgress(ManiaDownloadState.Downloading, copied, length));
                            nextReport = copied + 256 * 1024;
                        }
                    }

                    if (copied == 0 || (length.HasValue && copied != length.Value))
                        throw new InvalidDataException("The downloaded package is incomplete.");

                    await output.FlushAsync(token).ConfigureAwait(false);
                    update(task, new ManiaDownloadProgress(ManiaDownloadState.Downloading, copied, copied));
                }

                token.ThrowIfCancellationRequested();
                update(task, task.Progress with { State = ManiaDownloadState.Importing });
                var imported = await importer.ImportAsync(archivePath, task.Set.Id, task.RequestedBeatmapId, token).ConfigureAwait(false);
                terminal = task.Progress with { State = ManiaDownloadState.Completed, Imported = imported };
            }
            catch (OperationCanceledException)
            {
                terminal = task.Progress with { State = task.Token.IsCancellationRequested ? ManiaDownloadState.Cancelled : ManiaDownloadState.Failed };
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException)
            {
                Logger.Log($"Sayobot mania package download or import failed.\n{ex}", LoggingTarget.Network);
                terminal = task.Progress with { State = ManiaDownloadState.Failed };
            }
            finally
            {
                if (staging != null)
                {
                    try
                    {
                        Directory.Delete(staging, true);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        Logger.Error(ex, "Could not remove the completed mania download staging directory.");
                    }
                }

                if (entered)
                    downloadSlot.Release();

                task.Finish();
                if (terminal != null)
                    update(task, terminal);
            }
        }

        private async Task<HttpResponseMessage> requestPackage(Uri uri, CancellationToken cancellationToken)
        {
            for (int redirect = 0; ; redirect++)
            {
                bool approvedAddress = uri.Scheme == Uri.UriSchemeHttps && uri.UserInfo.Length == 0
                                       && (((uri.Host is "dl.sayobot.cn" or "txy1.sayobot.cn") && uri.Port == 443)
                                           || (redirect > 0 && (uri.Host is "tc1.sayobot.cn" or "tc2.sayobot.cn") && uri.Port == 25225));

                if (!approvedAddress)
                    throw new InvalidDataException("The package URL is outside the authorised Sayobot download addresses.");

                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.UserAgent.ParseAdd("OMS/1.0 (+https://github.com/ZDaMexy/oms)");
                request.Headers.Referrer = new Uri("https://github.com/ZDaMexy/oms/");
                var response = await downloads.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.SeeOther
                    or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
                {
                    var location = response.Headers.Location;
                    response.Dispose();
                    if (location == null)
                        throw new InvalidDataException("The download redirect has no destination.");
                    if (redirect == 5)
                        throw new InvalidDataException("Too many download redirects.");

                    uri = new Uri(uri, location);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var status = response.StatusCode;
                    response.Dispose();
                    throw new HttpRequestException("The download server rejected the package request.", null, status);
                }

                return response;
            }
        }

        private void update(ManiaDownloadTask task, ManiaDownloadProgress progress)
        {
            task.Update(progress);
            TaskChanged?.Invoke(task);
        }

        public void Dispose()
        {
            ManiaDownloadTask[] pending;

            lock (taskLock)
            {
                if (disposed)
                    return;

                disposed = true;
                pending = tasks.Values.ToArray();
            }

            foreach (var task in pending)
                task.Cancel();

            // Imports use background Realm contexts. Join them before the game disposes Realm.
            try
            {
                Task.WhenAll(pending.Select(t => t.Completion)).GetAwaiter().GetResult();
            }
            finally
            {
                Client.Dispose();
                downloads.Dispose();
                downloadSlot.Dispose();
            }
        }
    }
}
