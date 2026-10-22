// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Input.Bindings;
using osu.Game.Models;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Skinning;
using Realms;

namespace osu.Game.Database
{
    public static class RealmObjectExtensions
    {
        /// <summary>
        /// Create a detached copy of the each item in the collection.
        /// </summary>
        /// <remarks>
        /// Items which are already detached (ie. not managed by realm) will not be modified.
        /// </remarks>
        /// <param name="items">A list of managed <see cref="RealmObject"/>s to detach.</param>
        /// <typeparam name="T">The type of object.</typeparam>
        /// <returns>A list containing non-managed copies of provided items.</returns>
        public static List<T> Detach<T>(this IEnumerable<T> items) where T : RealmObjectBase
        {
            var list = new List<T>();

            foreach (var obj in items)
                list.Add(obj.Detach());

            return list;
        }

        /// <summary>
        /// Create a detached copy of the item.
        /// </summary>
        /// <remarks>
        /// If the item if already detached (ie. not managed by realm) it will not be detached again and the original instance will be returned. This allows this method to be potentially called at multiple levels while only incurring the clone overhead once.
        /// </remarks>
        /// <param name="item">The managed <see cref="RealmObject"/> to detach.</param>
        /// <typeparam name="T">The type of object.</typeparam>
        /// <returns>A non-managed copy of provided item. Will return the provided item if already detached.</returns>
        public static T Detach<T>(this T item) where T : RealmObjectBase
        {
            if (!item.IsManaged)
                return item;

            // Only the persisted model graph is copied. In particular, do not follow arbitrary
            // getters, backlinks or ignored UI state, or recurse through BeatmapSet/Beatmap cycles.
            RealmObjectBase copy = item switch
            {
                BeatmapSetInfo set => detachBeatmapSet(set, false),
                BeatmapInfo beatmap => detachBeatmap(beatmap),
                BeatmapMetadata metadata => detachMetadata(metadata),
                BeatmapDifficulty difficulty => new BeatmapDifficulty(difficulty),
                BeatmapUserSettings settings => new BeatmapUserSettings { Offset = settings.Offset },
                RulesetInfo ruleset => ruleset.Clone(),
                RealmUser user => detachUser(user),
                RealmFile file => new RealmFile { Hash = file.Hash },
                RealmNamedFileUsage usage => detachFileUsage(usage),
                BeatmapCollection collection => new BeatmapCollection(collection.Name, new List<string>(collection.BeatmapMD5Hashes))
                {
                    ID = collection.ID,
                    Name = collection.Name,
                    LastModified = collection.LastModified,
                },
                RealmKeyBinding binding => new RealmKeyBinding(binding.ActionInt, string.Empty, binding.RulesetName, binding.Variant)
                {
                    ID = binding.ID,
                    KeyCombinationString = binding.KeyCombinationString,
                },
                SkinInfo skin => detachSkin(skin),
                ScoreInfo score => detachScore(score),
                _ => throw new NotSupportedException($"Detaching the Realm model {item.GetType().FullName} is not supported."),
            };
            return (T)copy;
        }

        /// <summary>
        /// Copy changes in a detached beatmap back to realm.
        /// This is a temporary method to handle existing flows only. It should not be used going forward if we can avoid it.
        /// </summary>
        /// <param name="source">The detached beatmap to copy from.</param>
        /// <param name="destination">The live beatmap to copy to.</param>
        public static void CopyChangesToRealm(this BeatmapSetInfo source, BeatmapSetInfo destination)
        {
            copyBeatmapSetValues(source, destination);

            foreach (var beatmap in source.Beatmaps)
            {
                // Search all of Realm, including rows detached from this set's Beatmaps list.
                var existingBeatmap = destination.Realm!.Find<BeatmapInfo>(beatmap.ID);

                if (existingBeatmap != null)
                {
                    if (!destination.Beatmaps.Contains(existingBeatmap))
                    {
                        Debug.Fail("Beatmaps should never become detached under normal circumstances. If this ever triggers, it should be investigated further.");
                        Logger.Log("WARNING: One of the difficulties in a beatmap was detached from its set. Please save a copy of logs and report this to devs.", LoggingTarget.Database,
                            LogLevel.Important);
                        destination.Beatmaps.Add(existingBeatmap);
                    }
                }
                else
                {
                    existingBeatmap = new BeatmapInfo
                    {
                        ID = beatmap.ID,
                        BeatmapSet = destination,
                        Ruleset = destination.Realm.Find<RulesetInfo>(beatmap.Ruleset.ShortName)!
                    };
                    destination.Beatmaps.Add(existingBeatmap);
                }

                copyBeatmapValues(beatmap, existingBeatmap);
                existingBeatmap.Ruleset = destination.Realm.Find<RulesetInfo>(beatmap.Ruleset.ShortName)!;

                // Keep the managed nested objects and user settings; only their existing writable
                // values were updated by the old write map. Read-only lists are not write targets.
                if (beatmap.Difficulty != null && existingBeatmap.Difficulty != null)
                    beatmap.Difficulty.CopyTo(existingBeatmap.Difficulty);
                if (beatmap.Metadata != null && existingBeatmap.Metadata != null)
                {
                    copyMetadataValues(beatmap.Metadata, existingBeatmap.Metadata);
                    if (beatmap.Metadata.Author != null && existingBeatmap.Metadata.Author != null)
                        copyUserValues(beatmap.Metadata.Author, existingBeatmap.Metadata.Author);
                }
            }
        }

        private static BeatmapSetInfo detachBeatmapSet(BeatmapSetInfo source, bool includeFiles, BeatmapInfo? preferredSource = null, BeatmapInfo? preferredCopy = null)
        {
            var copy = new BeatmapSetInfo(null);
            copyBeatmapSetValues(source, copy);

            bool preferredUsed = false;
            foreach (var beatmap in source.Beatmaps)
            {
                BeatmapInfo detached;
                if (!preferredUsed && preferredSource != null && beatmap.Equals(preferredSource))
                {
                    detached = preferredCopy!;
                    preferredUsed = true;
                }
                else
                    detached = detachBeatmapValues(beatmap);

                detached.BeatmapSet = copy;
                copy.Beatmaps.Add(detached);
            }

            if (includeFiles)
                foreach (var file in source.Files)
                    copy.Files.Add(detachFileUsage(file));

            return copy;
        }

        private static BeatmapInfo detachBeatmap(BeatmapInfo source)
        {
            var copy = detachBeatmapValues(source);
            if (source.BeatmapSet != null)
                copy.BeatmapSet = detachBeatmapSet(source.BeatmapSet, true, source, copy);
            return copy;
        }

        private static BeatmapInfo detachBeatmapValues(BeatmapInfo source)
        {
            var copy = new BeatmapInfo
            {
                Ruleset = source.Ruleset?.Clone()!,
                Difficulty = source.Difficulty == null ? null! : new BeatmapDifficulty(source.Difficulty),
                Metadata = source.Metadata == null ? null! : detachMetadata(source.Metadata),
                UserSettings = source.UserSettings == null ? null! : new BeatmapUserSettings { Offset = source.UserSettings.Offset },
            };
            copyBeatmapValues(source, copy);
            return copy;
        }

        private static void copyBeatmapSetValues(BeatmapSetInfo source, BeatmapSetInfo destination)
        {
            destination.ID = source.ID;
            destination.OnlineID = source.OnlineID;
            destination.DateAdded = source.DateAdded;
            destination.DateSubmitted = source.DateSubmitted;
            destination.DateRanked = source.DateRanked;
            destination.FilesystemStoragePath = source.FilesystemStoragePath;
            destination.IsExternalFilesystemStorage = source.IsExternalFilesystemStorage;
            destination.FilesystemUnavailable = source.FilesystemUnavailable;
            destination.ExternalLibraryRootPath = source.ExternalLibraryRootPath;
            destination.DifficultyTableRevision = source.DifficultyTableRevision;
            destination.StatusInt = source.StatusInt;
            destination.DeletePending = source.DeletePending;
            destination.Hash = source.Hash;
            destination.Protected = source.Protected;
        }

        private static void copyBeatmapValues(BeatmapInfo source, BeatmapInfo destination)
        {
            destination.ID = source.ID;
            destination.DifficultyName = source.DifficultyName;
            destination.LocalFilePath = source.LocalFilePath;
            destination.StatusInt = source.StatusInt;
            destination.OnlineID = source.OnlineID;
            destination.Length = source.Length;
            destination.BPM = source.BPM;
            destination.Hash = source.Hash;
            destination.StarRating = source.StarRating;
            destination.MD5Hash = source.MD5Hash;
            destination.OnlineMD5Hash = source.OnlineMD5Hash;
            destination.LastLocalUpdate = source.LastLocalUpdate;
            destination.LastOnlineUpdate = source.LastOnlineUpdate;
            destination.Hidden = source.Hidden;
            destination.EndTimeObjectCount = source.EndTimeObjectCount;
            destination.TotalObjectCount = source.TotalObjectCount;
            destination.LastPlayed = source.LastPlayed;
            destination.BeatDivisor = source.BeatDivisor;
            destination.EditorTimestamp = source.EditorTimestamp;
        }

        private static BeatmapMetadata detachMetadata(BeatmapMetadata source)
        {
            var copy = new BeatmapMetadata { Author = source.Author == null ? null! : detachUser(source.Author) };
            copyMetadataValues(source, copy);
            foreach (string tag in source.UserTags)
                copy.UserTags.Add(tag);
            return copy;
        }

        private static void copyMetadataValues(BeatmapMetadata source, BeatmapMetadata destination)
        {
            destination.Title = source.Title;
            destination.TitleUnicode = source.TitleUnicode;
            destination.Artist = source.Artist;
            destination.ArtistUnicode = source.ArtistUnicode;
            destination.Source = source.Source;
            destination.Tags = source.Tags;
            destination.PreviewTime = source.PreviewTime;
            destination.AudioFile = source.AudioFile;
            destination.BackgroundFile = source.BackgroundFile;
            destination.RulesetDataJson = source.RulesetDataJson;
        }

        private static RealmUser detachUser(RealmUser source)
        {
            var copy = new RealmUser();
            copyUserValues(source, copy);
            return copy;
        }

        private static void copyUserValues(RealmUser source, RealmUser destination)
        {
            destination.OnlineID = source.OnlineID;
            destination.Username = source.Username;
            destination.CountryString = source.CountryString;
        }

        private static RealmNamedFileUsage detachFileUsage(RealmNamedFileUsage source)
        {
            RealmFile? file = source.File;
            var copy = new RealmNamedFileUsage(file == null ? new RealmFile() : new RealmFile { Hash = file.Hash }, string.Empty)
            {
                Filename = source.Filename,
            };
            if (file == null)
                copy.File = null!;
            return copy;
        }

        private static SkinInfo detachSkin(SkinInfo source)
        {
            var copy = new SkinInfo(source.Name, source.Creator, source.InstantiationInfo)
            {
                ID = source.ID,
                Name = source.Name,
                Creator = source.Creator,
                InstantiationInfo = source.InstantiationInfo,
                Hash = source.Hash,
                Protected = source.Protected,
                FilesystemStoragePath = source.FilesystemStoragePath,
                IsExternalFilesystemStorage = source.IsExternalFilesystemStorage,
                FilesystemStorageAuthorityOwner = source.FilesystemStorageAuthorityOwner,
                DeletePending = source.DeletePending,
            };
            foreach (var file in source.Files)
                copy.Files.Add(detachFileUsage(file));
            return copy;
        }

        private static ScoreInfo detachScore(ScoreInfo source)
        {
            var copy = new ScoreInfo
            {
                ID = source.ID,
                BeatmapInfo = source.BeatmapInfo == null ? null : detachBeatmap(source.BeatmapInfo),
                Ruleset = source.Ruleset?.Clone()!,
                RealmUser = source.RealmUser == null ? null! : detachUser(source.RealmUser),
                ClientVersion = source.ClientVersion,
                BeatmapHash = source.BeatmapHash,
                Hash = source.Hash,
                DeletePending = source.DeletePending,
                TotalScore = source.TotalScore,
                TotalScoreWithoutMods = source.TotalScoreWithoutMods,
                TotalScoreVersion = source.TotalScoreVersion,
                LegacyTotalScore = source.LegacyTotalScore,
                BackgroundReprocessingFailed = source.BackgroundReprocessingFailed,
                MaxCombo = source.MaxCombo,
                Accuracy = source.Accuracy,
                Date = source.Date,
                PP = source.PP,
                OnlineID = source.OnlineID,
                LegacyOnlineID = source.LegacyOnlineID,
                ModsJson = source.ModsJson,
                StatisticsJson = source.StatisticsJson,
                MaximumStatisticsJson = source.MaximumStatisticsJson,
                RulesetDataJson = source.RulesetDataJson,
                RankInt = source.RankInt,
                Combo = source.Combo,
                IsLegacyScore = source.IsLegacyScore,
            };
            foreach (var file in source.Files)
                copy.Files.Add(detachFileUsage(file));
            foreach (int pause in source.Pauses)
                copy.Pauses.Add(pause);
            return copy;
        }

        public static List<Live<T>> ToLiveUnmanaged<T>(this IEnumerable<T> realmList)
            where T : RealmObject, IHasGuidPrimaryKey
        {
            return realmList.Select(l => new RealmLiveUnmanaged<T>(l)).Cast<Live<T>>().ToList();
        }

        public static Live<T> ToLiveUnmanaged<T>(this T realmObject)
            where T : RealmObject, IHasGuidPrimaryKey
        {
            return new RealmLiveUnmanaged<T>(realmObject);
        }

        public static Live<T> ToLive<T>(this T realmObject, RealmAccess realm)
            where T : RealmObject, IHasGuidPrimaryKey
        {
            return new RealmLive<T>(realmObject, realm);
        }

#pragma warning disable RS0030 // mentioning banned symbols in documentation
        /// <summary>
        /// Register a callback to be invoked each time this <see cref="IRealmCollection{T}" /> changes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This adds osu! specific thread and managed state safety checks on top of <see cref="IRealmCollection{T}.SubscribeForNotifications"/>.
        /// </para>
        /// <para>
        /// The first callback will be invoked with the initial <see cref="IRealmCollection{T}" /> after the asynchronous query completes,
        /// and then called again after each write transaction which changes either any of the objects in the collection, or
        /// which objects are in the collection. The <c>changes</c> parameter will
        /// be <c>null</c> the first time the callback is invoked with the initial results. For each call after that,
        /// it will contain information about which rows in the results were added, removed or modified.
        /// </para>
        /// <para>
        /// If a write transaction did not modify any objects in this <see cref="IRealmCollection{T}" />, the callback is not invoked at all.
        /// If an error occurs the callback will be invoked with <c>null</c> for the <c>sender</c> parameter and a non-<c>null</c> <c>error</c>.
        /// Currently, the only errors that can occur are when opening the <see cref="Realm" /> on the background worker thread.
        /// </para>
        /// <para>
        /// At the time when the block is called, the <see cref="IRealmCollection{T}" /> object will be fully evaluated
        /// and up-to-date, and as long as you do not perform a write transaction on the same thread
        /// or explicitly call <see cref="Realm.Refresh" />, accessing it will never perform blocking work.
        /// </para>
        /// <para>
        /// Notifications are delivered via the standard event loop, and so can't be delivered while the event loop is blocked by other activity.
        /// When notifications can't be delivered instantly, multiple notifications may be coalesced into a single notification.
        /// This can include the notification with the initial collection.
        /// </para>
        /// </remarks>
        /// <param name="collection">The <see cref="IRealmCollection{T}"/> to observe for changes.</param>
        /// <param name="callback">The callback to be invoked with the updated <see cref="IRealmCollection{T}" />.</param>
        /// <returns>
        /// A subscription token. It must be kept alive for as long as you want to receive change notifications.
        /// To stop receiving notifications, call <see cref="IDisposable.Dispose" />.
        /// </returns>
        /// <seealso cref="Realms.CollectionExtensions.SubscribeForNotifications{T}(IList{T}, NotificationCallbackDelegate{T},KeyPathsCollection?)" />
        /// <seealso cref="Realms.CollectionExtensions.SubscribeForNotifications{T}(IQueryable{T}, NotificationCallbackDelegate{T},KeyPathsCollection?)" />
#pragma warning restore RS0030
        public static IDisposable QueryAsyncWithNotifications<T>(this IRealmCollection<T> collection, NotificationCallbackDelegate<T> callback)
            where T : RealmObjectBase
        {
            if (!RealmAccess.CurrentThreadSubscriptionsAllowed)
                throw new InvalidOperationException($"Make sure to call {nameof(RealmAccess)}.{nameof(RealmAccess.RegisterForNotifications)}");

            bool initial = true;
            return collection.SubscribeForNotifications(((sender, changes) =>
            {
                if (initial)
                {
                    initial = false;

                    // Realm might coalesce the initial callback, meaning we never receive a `ChangeSet` of `null` marking the first callback.
                    // Let's decouple it for simplicity in handling.
                    if (changes != null)
                    {
                        callback(sender, null);
                        return;
                    }
                }

                callback(sender, changes);
            }));
        }

        /// <summary>
        /// A convenience method that casts <see cref="IQueryable{T}"/> to <see cref="IRealmCollection{T}"/> and subscribes for change notifications.
        /// </summary>
        /// <remarks>
        /// This adds osu! specific thread and managed state safety checks on top of <see cref="IRealmCollection{T}.SubscribeForNotifications"/>.
        /// </remarks>
        /// <param name="list">The <see cref="IQueryable{T}"/> to observe for changes.</param>
        /// <typeparam name="T">Type of the elements in the list.</typeparam>
        /// <seealso cref="IRealmCollection{T}.SubscribeForNotifications"/>
        /// <param name="callback">The callback to be invoked with the updated <see cref="IRealmCollection{T}"/>.</param>
        /// <returns>
        /// A subscription token. It must be kept alive for as long as you want to receive change notifications.
        /// To stop receiving notifications, call <see cref="IDisposable.Dispose"/>.
        ///
        /// May be null in the case the provided collection is not managed.
        /// </returns>
        public static IDisposable? QueryAsyncWithNotifications<T>(this IQueryable<T> list, NotificationCallbackDelegate<T> callback)
            where T : RealmObjectBase
        {
            // Subscribing to non-managed instances doesn't work.
            // In this usage, the instance may be non-managed in tests.
            if (!(list is IRealmCollection<T> realmCollection))
                return null;

            return QueryAsyncWithNotifications(realmCollection, callback);
        }

        /// <summary>
        /// A convenience method that casts <see cref="IList{T}"/> to <see cref="IRealmCollection{T}"/> and subscribes for change notifications.
        /// </summary>
        /// <remarks>
        /// This adds osu! specific thread and managed state safety checks on top of <see cref="IRealmCollection{T}.SubscribeForNotifications"/>.
        /// </remarks>
        /// <param name="list">The <see cref="IList{T}"/> to observe for changes.</param>
        /// <typeparam name="T">Type of the elements in the list.</typeparam>
        /// <seealso cref="IRealmCollection{T}.SubscribeForNotifications"/>
        /// <param name="callback">The callback to be invoked with the updated <see cref="IRealmCollection{T}"/>.</param>
        /// <returns>
        /// A subscription token. It must be kept alive for as long as you want to receive change notifications.
        /// To stop receiving notifications, call <see cref="IDisposable.Dispose"/>.
        ///
        /// May be null in the case the provided collection is not managed.
        /// </returns>
        public static IDisposable? QueryAsyncWithNotifications<T>(this IList<T> list, NotificationCallbackDelegate<T> callback)
            where T : RealmObjectBase
        {
            // Subscribing to non-managed instances doesn't work.
            // In this usage, the instance may be non-managed in tests.
            if (!(list is IRealmCollection<T> realmCollection))
                return null;

            return QueryAsyncWithNotifications(realmCollection, callback);
        }
    }
}
