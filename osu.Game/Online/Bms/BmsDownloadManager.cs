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

namespace osu.Game.Online.Bms
{
    public enum BmsDownloadState
    {
        Queued,
        Downloading,
        Importing,
        Completed,
        Cancelled,
        Failed,
    }

    public record BmsDownloadProgress(BmsDownloadState State, long Bytes = 0, long? TotalBytes = null,
                                     IReadOnlyList<BmsDownloadImportedBeatmap>? Imported = null);

    public sealed class BmsDownloadTask
    {
        private readonly object cancellationLock = new object();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private bool finished;
        private BmsDownloadProgress progress = new BmsDownloadProgress(BmsDownloadState.Queued);

        public BmsDownloadPackage Package { get; }
        public string RequestedMd5 { get; }
        public BmsDownloadProgress Progress => Volatile.Read(ref progress);
        public Task Completion { get; internal set; } = Task.CompletedTask;
        internal CancellationToken Token => cancellation.Token;

        internal BmsDownloadTask(BmsDownloadPackage package, string requestedMd5)
        {
            Package = package;
            RequestedMd5 = requestedMd5;
        }

        public void Cancel()
        {
            lock (cancellationLock)
            {
                if (!finished)
                    cancellation.Cancel();
            }
        }

        internal void Update(BmsDownloadProgress value) => Volatile.Write(ref progress, value);

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
    /// Game-owned downloads. Browser cards only observe tasks; closing the browser does not cancel them.
    /// </summary>
    public sealed class BmsDownloadManager : IDisposable
    {
        public const long MaximumPackageBytes = 2L * 1024 * 1024 * 1024;

        private readonly object taskLock = new object();
        private readonly Dictionary<string, BmsDownloadTask> tasks = new Dictionary<string, BmsDownloadTask>();
        private readonly SemaphoreSlim downloadSlot = new SemaphoreSlim(1);
        private readonly Storage storage;
        private readonly IBmsDownloadImporter importer;
        private readonly HttpClient downloads;
        private bool disposed;

        public BmsDownloadClient Client { get; }
        public event Action<BmsDownloadTask>? TaskChanged;

        public BmsDownloadManager(Storage storage, IBmsDownloadImporter importer, BmsDownloadClient? client = null, HttpClient? downloads = null)
        {
            this.storage = storage;
            this.importer = importer;
            Client = client ?? new BmsDownloadClient();
            this.downloads = downloads ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            {
                Timeout = TimeSpan.FromMinutes(30),
            };
        }

        public BmsDownloadTask? GetTask(string key)
        {
            lock (taskLock)
                return tasks.GetValueOrDefault(key);
        }

        public BmsDownloadTask Download(BmsDownloadPackage package, string requestedMd5)
        {
            if (!package.CanDownload)
                throw new InvalidOperationException("This source has no package for the chart.");

            if (!package.Charts.Any(c => c.Md5 == requestedMd5))
                throw new ArgumentException("The requested chart is not part of the selected package.", nameof(requestedMd5));

            BmsDownloadTask task;

            lock (taskLock)
            {
                ObjectDisposedException.ThrowIf(disposed, this);

                if (tasks.TryGetValue(package.Key, out var existing)
                    && existing.Progress.State is BmsDownloadState.Queued or BmsDownloadState.Downloading or BmsDownloadState.Importing)
                    return existing;

                tasks[package.Key] = task = new BmsDownloadTask(package, requestedMd5);
                task.Completion = Task.Run(() => runAsync(task));
            }

            TaskChanged?.Invoke(task);
            return task;
        }

        private async Task runAsync(BmsDownloadTask task)
        {
            bool entered = false;
            string? staging = null;
            BmsDownloadProgress? terminal = null;
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(task.Token);
            CancellationToken token = budget.Token;

            try
            {
                await downloadSlot.WaitAsync(task.Token).ConfigureAwait(false);
                entered = true;
                budget.CancelAfter(TimeSpan.FromMinutes(30));

                if (task.Package.Size > MaximumPackageBytes)
                    throw new InvalidDataException("The package exceeds the download size limit.");

                staging = storage.GetFullPath(Path.Combine("bms-downloads", Guid.NewGuid().ToString("N")));
                Directory.CreateDirectory(staging);
                // 616 names songs rather than archive files. The importer validates the actual archive format.
                string archivePath = Path.Combine(staging, "package");

                update(task, new BmsDownloadProgress(BmsDownloadState.Downloading, TotalBytes: task.Package.Size));
                using (var response = await requestPackage(task.Package.DownloadUrl!, token).ConfigureAwait(false))
                {
                    long? length = response.Content.Headers.ContentLength;
                    if (length > MaximumPackageBytes || length == 0)
                        throw new InvalidDataException("Invalid package size.");

                    update(task, new BmsDownloadProgress(BmsDownloadState.Downloading, TotalBytes: length ?? task.Package.Size));

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
                            update(task, new BmsDownloadProgress(BmsDownloadState.Downloading, copied, length ?? task.Package.Size));
                            nextReport = copied + 256 * 1024;
                        }
                    }

                    if (copied == 0 || (length.HasValue && copied != length.Value))
                        throw new InvalidDataException("The downloaded package is incomplete.");

                    await output.FlushAsync(token).ConfigureAwait(false);
                    update(task, new BmsDownloadProgress(BmsDownloadState.Downloading, copied, copied));
                }

                token.ThrowIfCancellationRequested();
                update(task, task.Progress with { State = BmsDownloadState.Importing });
                var imported = await importer.ImportAsync(archivePath, new[] { task.RequestedMd5 }, token).ConfigureAwait(false);
                terminal = task.Progress with { State = BmsDownloadState.Completed, Imported = imported };
            }
            catch (OperationCanceledException)
            {
                terminal = task.Progress with { State = task.Token.IsCancellationRequested ? BmsDownloadState.Cancelled : BmsDownloadState.Failed };
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException)
            {
                Logger.Error(ex, "BMS package download or import failed.");
                terminal = task.Progress with { State = BmsDownloadState.Failed };
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
                        Logger.Error(ex, "Could not remove the completed BMS download staging directory.");
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
            for (int redirect = 0; redirect <= 5; redirect++)
            {
                if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.UserInfo.Length != 0
                    || uri.Host is not ("gingerrush.com" or "pixeldrain.net" or "bms.alvorna.com"))
                    throw new InvalidDataException("The package URL is outside the authorised download hosts.");

                var response = await downloads.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
                {
                    var location = response.Headers.Location;
                    response.Dispose();
                    if (location == null)
                        throw new InvalidDataException("The download redirect has no destination.");

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

            throw new InvalidDataException("Too many download redirects.");
        }

        private void update(BmsDownloadTask task, BmsDownloadProgress progress)
        {
            task.Update(progress);
            TaskChanged?.Invoke(task);
        }

        public void Dispose()
        {
            BmsDownloadTask[] pending;

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
