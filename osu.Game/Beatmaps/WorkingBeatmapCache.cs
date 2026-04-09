// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Lists;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Statistics;
using osu.Game.Beatmaps.Formats;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.IO;
using osu.Game.Skinning;
using osu.Game.Storyboards;

namespace osu.Game.Beatmaps
{
    public class WorkingBeatmapCache : IBeatmapResourceProvider, IWorkingBeatmapCache
    {
        private readonly WeakList<BeatmapManagerWorkingBeatmap> workingCache = new WeakList<BeatmapManagerWorkingBeatmap>();

        /// <summary>
        /// Beatmap files may specify this filename to denote that they don't have an audio track.
        /// </summary>
        private const string virtual_track_filename = @"virtual";

        /// <summary>
        /// A default representation of a WorkingBeatmap to use when no beatmap is available.
        /// </summary>
        public readonly WorkingBeatmap DefaultBeatmap;

        private readonly AudioManager audioManager;
        private readonly IResourceStore<byte[]> resources;
        private readonly LargeTextureStore largeTextureStore;
        private readonly LargeTextureStore beatmapPanelTextureStore;
        private readonly Storage storage;
        private readonly ITrackStore trackStore;
        private readonly IResourceStore<byte[]> files;
        private readonly RealmAccess realm;
        private readonly IReadOnlyList<ICustomBeatmapLoader> customBeatmapLoaders;

        [CanBeNull]
        private readonly GameHost host;

        public WorkingBeatmapCache(ITrackStore trackStore, AudioManager audioManager, IResourceStore<byte[]> resources, Storage storage, IResourceStore<byte[]> files, WorkingBeatmap defaultBeatmap = null,
                                   GameHost host = null, RealmAccess realm = null, IEnumerable<ICustomBeatmapLoader> customBeatmapLoaders = null)
        {
            DefaultBeatmap = defaultBeatmap;

            this.audioManager = audioManager;
            this.resources = resources;
            this.host = host;
            this.storage = storage;
            this.files = files;
            largeTextureStore = new LargeTextureStore(host?.Renderer ?? new DummyRenderer(), host?.CreateTextureLoaderStore(files));
            beatmapPanelTextureStore = new LargeTextureStore(host?.Renderer ?? new DummyRenderer(), new BeatmapPanelBackgroundTextureLoaderStore(host?.CreateTextureLoaderStore(files)));
            this.trackStore = trackStore;
            this.realm = realm;
            this.customBeatmapLoaders = customBeatmapLoaders?.ToArray() ?? Array.Empty<ICustomBeatmapLoader>();
        }

        public void Invalidate(BeatmapSetInfo info)
        {
            foreach (var b in info.Beatmaps)
                Invalidate(b);
        }

        public void Invalidate(BeatmapInfo info)
        {
            lock (workingCache)
            {
                var working = workingCache.FirstOrDefault(w => info.Equals(w.BeatmapInfo));

                if (working != null)
                {
                    Logger.Log($"Invalidating working beatmap cache for {info}");
                    workingCache.Remove(working);
                    OnInvalidated?.Invoke(working);
                }
            }
        }

        public event Action<WorkingBeatmap> OnInvalidated;

        public virtual WorkingBeatmap GetWorkingBeatmap([CanBeNull] BeatmapInfo beatmapInfo)
        {
            if (beatmapInfo == null || ReferenceEquals(beatmapInfo, DefaultBeatmap.BeatmapInfo))
                return DefaultBeatmap;

            lock (workingCache)
            {
                var working = workingCache.FirstOrDefault(w => beatmapInfo.Equals(w.BeatmapInfo));

                if (working != null)
                    return working;

                beatmapInfo = beatmapInfo.Detach();

                // If this ever gets hit, a request has arrived with an outdated BeatmapInfo.
                // An outdated BeatmapInfo may contain a reference to a previous version of the beatmap's files on disk.
                Debug.Assert(confirmFileHashIsUpToDate(beatmapInfo), "working beatmap returned with outdated path");

                workingCache.Add(working = new BeatmapManagerWorkingBeatmap(beatmapInfo, createResourceProvider(beatmapInfo), customBeatmapLoaders));

                // best effort; may be higher than expected.
                GlobalStatistics.Get<int>("Beatmaps", $"Cached {nameof(WorkingBeatmap)}s").Value = workingCache.Count();

                return working;
            }
        }

        private bool confirmFileHashIsUpToDate(BeatmapInfo beatmapInfo)
        {
            string refetchPath = realm.Run(r => r.Find<BeatmapInfo>(beatmapInfo.ID)?.File?.File.Hash);
            return refetchPath == null || refetchPath == beatmapInfo.File?.File.Hash;
        }

        #region IResourceStorageProvider

        TextureStore IBeatmapResourceProvider.LargeTextureStore => largeTextureStore;
        TextureStore IBeatmapResourceProvider.BeatmapPanelTextureStore => beatmapPanelTextureStore;
        ITrackStore IBeatmapResourceProvider.Tracks => trackStore;
        IRenderer IStorageResourceProvider.Renderer => host?.Renderer ?? new DummyRenderer();
        AudioManager IStorageResourceProvider.AudioManager => audioManager;
        RealmAccess IStorageResourceProvider.RealmAccess => realm;
        IResourceStore<byte[]> IStorageResourceProvider.Files => files;
        IResourceStore<byte[]> IStorageResourceProvider.Resources => resources;
        IResourceStore<TextureUpload> IStorageResourceProvider.CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore) => host?.CreateTextureLoaderStore(underlyingStore);

        #endregion

        private IBeatmapResourceProvider createResourceProvider(BeatmapInfo beatmapInfo)
        {
            string filesystemStoragePath = beatmapInfo.BeatmapSet?.FilesystemStoragePath;

            if (string.IsNullOrEmpty(filesystemStoragePath))
                return this;

            return new FilesystemBackedBeatmapResourceProvider(this, storage.GetStorageForDirectory(filesystemStoragePath));
        }

        private class BeatmapManagerWorkingBeatmap : WorkingBeatmap
        {
            [NotNull]
            private readonly IBeatmapResourceProvider resources;
            private readonly IReadOnlyList<ICustomBeatmapLoader> customBeatmapLoaders;

            public BeatmapManagerWorkingBeatmap(BeatmapInfo beatmapInfo, [NotNull] IBeatmapResourceProvider resources, IReadOnlyList<ICustomBeatmapLoader> customBeatmapLoaders)
                : base(beatmapInfo, resources.AudioManager)
            {
                this.resources = resources;
                this.customBeatmapLoaders = customBeatmapLoaders;
            }

            protected override IBeatmap GetBeatmap()
            {
                if (BeatmapInfo.Path == null)
                    return new Beatmap { BeatmapInfo = BeatmapInfo };

                try
                {
                    string fileStorePath = resolveStoragePath(BeatmapInfo.Path);

                    var stream = fileStorePath != null ? GetStream(fileStorePath) : null;

                    if (stream == null)
                    {
                        Logger.Log($"Beatmap failed to load (file {BeatmapInfo.Path} not found on disk at expected location {fileStorePath}).", level: LogLevel.Error);
                        return null;
                    }

                    string streamMD5 = stream.ComputeMD5Hash();
                    string streamSHA2 = stream.ComputeSHA2Hash();

                    if (streamMD5 != BeatmapInfo.MD5Hash)
                    {
                        Logger.Log($"Beatmap failed to load (file {BeatmapInfo.Path} does not have the expected hash).", level: LogLevel.Error);
                        return null;
                    }

                    IBeatmap beatmap;

                    if (tryLoadCustomBeatmap(stream, out var customBeatmap))
                    {
                        beatmap = customBeatmap;
                    }
                    else
                    {
                        using var reader = new LineBufferedReader(stream);
                        beatmap = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
                    }

                    beatmap.BeatmapInfo.MD5Hash = streamMD5;
                    beatmap.BeatmapInfo.Hash = streamSHA2;
                    beatmap.BeatmapInfo.UpdateStatisticsFromBeatmap(beatmap);

                    return beatmap;
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Beatmap failed to load");
                    return null;
                }
            }

            private bool tryLoadCustomBeatmap(Stream stream, out IBeatmap beatmap)
            {
                beatmap = null;

                if (BeatmapInfo.Path == null)
                    return false;

                var loader = customBeatmapLoaders.FirstOrDefault(l => l.CanLoad(BeatmapInfo, BeatmapInfo.Path));

                if (loader == null)
                    return false;

                if (stream.CanSeek)
                    stream.Seek(0, SeekOrigin.Begin);

                beatmap = loader.Load(stream, BeatmapInfo.Path, BeatmapInfo);
                return true;
            }

            public override Texture GetPanelBackground() => getBackgroundFromStore(resources.BeatmapPanelTextureStore);

            public override Texture GetBackground() => getBackgroundFromStore(resources.LargeTextureStore);

            private Texture getBackgroundFromStore(TextureStore store)
            {
                if (string.IsNullOrEmpty(Metadata?.BackgroundFile))
                    return null;

                try
                {
                    string fileStorePath = resolveStoragePath(Metadata.BackgroundFile);
                    var texture = fileStorePath != null ? store.Get(fileStorePath) : null;

                    if (texture == null)
                    {
                        Logger.Log($"Beatmap background failed to load (file {Metadata.BackgroundFile} not found on disk at expected location {fileStorePath}).");
                        return null;
                    }

                    return texture;
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Background failed to load");
                    return null;
                }
            }

            protected override Track GetBeatmapTrack()
            {
                if (string.IsNullOrEmpty(Metadata?.AudioFile))
                    return null;

                if (Metadata.AudioFile == virtual_track_filename)
                    return null;

                try
                {
                    string fileStorePath = resolveStoragePath(Metadata.AudioFile);
                    var track = fileStorePath != null ? resources.Tracks.Get(fileStorePath) : null;

                    if (track == null)
                    {
                        Logger.Log($"Beatmap failed to load (file {Metadata.AudioFile} not found on disk at expected location {fileStorePath}).", level: LogLevel.Error);
                        return null;
                    }

                    return track;
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Track failed to load");
                    return null;
                }
            }

            protected override Waveform GetWaveform()
            {
                if (string.IsNullOrEmpty(Metadata?.AudioFile))
                    return null;

                if (Metadata.AudioFile == virtual_track_filename)
                    return null;

                try
                {
                    string fileStorePath = resolveStoragePath(Metadata.AudioFile);

                    var trackData = fileStorePath != null ? GetStream(fileStorePath) : null;

                    if (trackData == null)
                    {
                        Logger.Log($"Beatmap waveform failed to load (file {Metadata.AudioFile} not found on disk at expected location {fileStorePath}).", level: LogLevel.Error);
                        return null;
                    }

                    return new Waveform(trackData);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Waveform failed to load");
                    return null;
                }
            }

            protected override Storyboard GetStoryboard()
            {
                Storyboard storyboard;

                if (BeatmapInfo.Path == null)
                    return new Storyboard();

                try
                {
                    string fileStorePath = resolveStoragePath(BeatmapInfo.Path);
                    var beatmapFileStream = fileStorePath != null ? GetStream(fileStorePath) : null;

                    if (beatmapFileStream == null)
                    {
                        Logger.Log($"Beatmap failed to load (file {BeatmapInfo.Path} not found on disk at expected location {fileStorePath})", level: LogLevel.Error);
                        return new Storyboard();
                    }

                    using (var reader = new LineBufferedReader(beatmapFileStream))
                    {
                        var decoder = Decoder.GetDecoder<Storyboard>(reader);

                        Stream storyboardFileStream = null;

                        string mainStoryboardFilename = getMainStoryboardFilename(BeatmapSetInfo.Metadata);

                        if (resolveStoragePath(mainStoryboardFilename) is string storyboardFilename)
                        {
                            storyboardFileStream = GetStream(storyboardFilename);

                            if (storyboardFileStream == null)
                                Logger.Log($"Storyboard failed to load (file {mainStoryboardFilename} not found on disk at expected location {storyboardFilename})", level: LogLevel.Error);
                        }

                        if (storyboardFileStream != null)
                        {
                            // Stand-alone storyboard was found, so parse in addition to the beatmap's local storyboard.
                            using (var secondaryReader = new LineBufferedReader(storyboardFileStream))
                                storyboard = decoder.Decode(reader, secondaryReader);
                        }
                        else
                            storyboard = decoder.Decode(reader);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Storyboard failed to load");
                    storyboard = new Storyboard();
                }

                storyboard.BeatmapInfo = BeatmapInfo;

                return storyboard;
            }

            protected internal override ISkin GetSkin()
            {
                try
                {
                    return new LegacyBeatmapSkin(BeatmapInfo, resources);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Skin failed to load");
                    return null;
                }
            }

            public override Stream GetStream(string storagePath) => resources.Files.GetStream(storagePath);

            private string resolveStoragePath(string filename)
            {
                if (string.IsNullOrEmpty(filename))
                    return null;

                if (!string.IsNullOrEmpty(BeatmapSetInfo.FilesystemStoragePath))
                    return filename.ToStandardisedPath();

                return BeatmapSetInfo.GetPathForFile(filename);
            }

            private string getMainStoryboardFilename(IBeatmapMetadataInfo metadata)
            {
                // Matches stable implementation, because it's probably simpler than trying to do anything else.
                // This may need to be reconsidered after we begin storing storyboards in the new editor.
                string baseFilename = (metadata.Artist.Length > 0 ? metadata.Artist + @" - " + metadata.Title : Path.GetFileNameWithoutExtension(metadata.AudioFile))
                                      + (metadata.Author.Username.Length > 0 ? @" (" + metadata.Author.Username + @")" : string.Empty)
                                      + @".osb";
                return baseFilename.GetValidFilename();
            }
        }

        private class FilesystemBackedBeatmapResourceProvider : IBeatmapResourceProvider
        {
            private readonly IBeatmapResourceProvider fallback;
            private readonly IResourceStore<byte[]> files;
            private readonly TextureStore largeTextureStore;
            private readonly TextureStore beatmapPanelTextureStore;
            private readonly ITrackStore tracks;

            public FilesystemBackedBeatmapResourceProvider(IBeatmapResourceProvider fallback, Storage storage)
            {
                this.fallback = fallback;

                files = new StorageBackedResourceStore(storage);
                largeTextureStore = new LargeTextureStore(fallback.Renderer, fallback.CreateTextureLoaderStore(files));
                beatmapPanelTextureStore = new LargeTextureStore(fallback.Renderer, new BeatmapPanelBackgroundTextureLoaderStore(fallback.CreateTextureLoaderStore(files)));
                tracks = fallback.AudioManager.GetTrackStore(files);
            }

            TextureStore IBeatmapResourceProvider.LargeTextureStore => largeTextureStore;
            TextureStore IBeatmapResourceProvider.BeatmapPanelTextureStore => beatmapPanelTextureStore;
            ITrackStore IBeatmapResourceProvider.Tracks => tracks;
            IRenderer IStorageResourceProvider.Renderer => fallback.Renderer;
            AudioManager IStorageResourceProvider.AudioManager => fallback.AudioManager;
            RealmAccess IStorageResourceProvider.RealmAccess => fallback.RealmAccess;
            IResourceStore<byte[]> IStorageResourceProvider.Files => files;
            IResourceStore<byte[]> IStorageResourceProvider.Resources => fallback.Resources;
            IResourceStore<TextureUpload> IStorageResourceProvider.CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore) => fallback.CreateTextureLoaderStore(underlyingStore);
        }
    }
}
