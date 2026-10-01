// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics.Textures;
using osu.Framework.Platform;

namespace osu.Game.Online
{
    public sealed class BeatmapDownloadCoverStore : IDisposable
    {
        private readonly BeatmapDownloadCoverResourceStore source;
        private readonly TextureStore textures;
        private readonly Dictionary<string, Task<Texture>> pending = new Dictionary<string, Task<Texture>>();
        private bool disposed;

        public BeatmapDownloadCoverStore(GameHost host, BeatmapDownloadCoverResourceStore source)
        {
            this.source = source;
            textures = new TextureStore(host.Renderer, host.CreateTextureLoaderStore(source));
        }

        public async Task<Texture> GetAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Task<Texture> retrieval;
            lock (pending)
            {
                if (disposed)
                    throw new OperationCanceledException("The beatmap cover store is shutting down.");

                // TextureStore.GetAsync cannot safely await another in-progress lookup of the same key.
                // Card cancellation only stops this waiter, leaving the shared retrieval for the new card.
                if (!pending.TryGetValue(name, out retrieval!))
                    pending.Add(name, retrieval = textures.GetAsync(name, CancellationToken.None));
            }

            try
            {
                var texture = await retrieval.WaitAsync(cancellationToken).ConfigureAwait(false);
                // The framework image loader represents a cancelled source read as a missing image.
                // A retiring owner must still report cancellation to its active card waiters.
                lock (pending)
                {
                    if (disposed)
                        throw new OperationCanceledException("The beatmap cover store is shutting down.");
                }

                return texture;
            }
            finally
            {
                lock (pending)
                {
                    if (retrieval.IsCompleted && pending.TryGetValue(name, out var current) && current == retrieval)
                        pending.Remove(name);
                }
            }
        }

        public void Dispose()
        {
            Task<Texture>[] retrievals;
            lock (pending)
            {
                if (disposed)
                    return;

                disposed = true;
                retrievals = pending.Values.ToArray();
            }

            source.CancelPendingRequests();
            try
            {
                // The source honours cancellation without using the game thread. Drain uploads before
                // releasing its loader and the host renderer during game shutdown.
                Task.WhenAll(retrievals).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                textures.Dispose();
            }
        }
    }
}
