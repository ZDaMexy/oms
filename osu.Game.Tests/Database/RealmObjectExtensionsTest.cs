// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Input.Bindings;
using osu.Game.Models;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Skinning;
using Realms;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class RealmObjectExtensionsTest : RealmTest
    {
        [TestCase("set")]
        [TestCase("beatmap")]
        [TestCase("metadata")]
        [TestCase("difficulty")]
        [TestCase("settings")]
        [TestCase("ruleset")]
        [TestCase("user")]
        [TestCase("file")]
        [TestCase("file-usage")]
        [TestCase("collection")]
        [TestCase("binding")]
        [TestCase("skin")]
        [TestCase("score")]
        public void TestDetachedModelsPreserveAllPersistedValuesAndOwnedCollections(string model)
        {
            RunTestWithRealm((access, _) => access.Write(realm =>
            {
                RealmObjectBase source = createManagedModel(realm, model);
                RealmObjectBase copy = source.Detach();

                // Reflect only in the regression verifier: an omitted persisted scalar or collection
                // must fail even when a future model field is added. Production copies are explicit.
                assertDetachedGraph(source, copy, new HashSet<(RealmObjectBase, RealmObjectBase)>(), model == "set");
                Assert.That(copy.Detach(), Is.SameAs(copy));
            }));
        }

        [Test]
        public void TestBeatmapRootIsReusedInsideDetachedSetAndFilesAreIndependent()
        {
            RunTestWithRealm((access, _) => access.Write(realm =>
            {
                var source = realm.Add(populate(createSet()));
                BeatmapInfo managed = source.Beatmaps[0];
                var copy = managed.Detach();

                Assert.That(copy.BeatmapSet, Is.Not.Null);
                Assert.That(copy.BeatmapSet!.Beatmaps[0], Is.SameAs(copy));
                Assert.That(copy.BeatmapSet.Beatmaps.All(b => ReferenceEquals(b.BeatmapSet, copy.BeatmapSet)), Is.True);
                Assert.That(copy.BeatmapSet.Files.Count, Is.EqualTo(source.Files.Count));

                string originalFilename = source.Files[0].Filename;
                string originalTitle = managed.Metadata.Title;
                copy.BeatmapSet.Files[0].Filename = "detached-file.png";
                copy.Metadata.Title = "detached-title";
                copy.Metadata.UserTags.Clear();

                Assert.That(source.Files[0].Filename, Is.EqualTo(originalFilename));
                Assert.That(managed.Metadata.Title, Is.EqualTo(originalTitle));
                Assert.That(managed.Metadata.UserTags, Is.Not.Empty);
            }));
        }

        [Test]
        public void TestNullableRelationshipsAndIgnoredRuntimeValues()
        {
            RunTestWithRealm((access, _) => access.Write(realm =>
            {
                var beatmap = new BeatmapInfo { BeatmapSet = null, LocalFilePath = null, LastPlayed = null, EditorTimestamp = null };
                var score = realm.Add(new ScoreInfo(beatmap) { BeatmapInfo = null, LegacyTotalScore = null, PP = null });
                score.HasOnlineReplay = true;
                score.Ranked = true;
                score.Passed = false;
                score.Position = 4;

                ScoreInfo copy = score.Detach();
                Assert.That(copy.BeatmapInfo, Is.Null);
                Assert.That(copy.LegacyTotalScore, Is.Null);
                Assert.That(copy.PP, Is.Null);
                Assert.That(copy.HasOnlineReplay, Is.False);
                Assert.That(copy.Ranked, Is.False);
                Assert.That(copy.Passed, Is.True);
                Assert.That(copy.Position, Is.Null);

                var managedBeatmap = realm.Add(beatmap);
                var detachedBeatmap = managedBeatmap.Detach();
                Assert.That(detachedBeatmap.BeatmapSet, Is.Null);
                Assert.That(detachedBeatmap.LocalFilePath, Is.Null);
                Assert.That(detachedBeatmap.LastPlayed, Is.Null);
                Assert.That(detachedBeatmap.EditorTimestamp, Is.Null);
            }));
        }

        [Test]
        public void TestUnmanagedAndEnumerableDetachKeepExistingContract()
        {
            var unmanaged = createSet();
            Assert.That(unmanaged.Detach(), Is.SameAs(unmanaged));
            Assert.That(new[] { unmanaged, unmanaged }.Detach(), Is.EqualTo(new[] { unmanaged, unmanaged }));

            RunTestWithRealm((access, _) => access.Write(realm =>
            {
                var managed = realm.Add(populate(createSet()));
                var copies = new[] { managed, managed }.Detach();
                Assert.That(copies.Count, Is.EqualTo(2));
                Assert.That(copies.All(c => !c.IsManaged && c.ID == managed.ID), Is.True);
                Assert.That(copies[0], Is.Not.SameAs(copies[1]));
            }));
        }

        [Test]
        public void TestCopyChangesUpdatesExistingRowsAndKeepsManagedRelationshipsAndReadOnlyLists()
        {
            RunTestWithRealm((access, _) =>
            {
                BeatmapSetInfo source = null!;
                BeatmapSetInfo destination = null!;
                BeatmapMetadata originalMetadata = null!;
                BeatmapDifficulty originalDifficulty = null!;
                RealmUser originalAuthor = null!;
                double originalOffset = 0;
                string[] originalTags = null!;
                Guid retainedBeatmapId = Guid.Empty;
                int originalFileCount = 0;

                access.Write(realm =>
                {
                    destination = realm.Add(populate(createSet()));
                    source = destination.Detach();
                    originalMetadata = destination.Beatmaps[0].Metadata;
                    originalDifficulty = destination.Beatmaps[0].Difficulty;
                    originalAuthor = originalMetadata.Author;
                    originalOffset = destination.Beatmaps[0].UserSettings.Offset;
                    originalTags = originalMetadata.UserTags.ToArray();
                    retainedBeatmapId = destination.Beatmaps[1].ID;
                    originalFileCount = destination.Files.Count;
                });

                source.Hash = "updated-set";
                source.Beatmaps[0].DifficultyName = "updated-difficulty";
                source.Beatmaps[0].Metadata.Title = "updated-title";
                source.Beatmaps[0].Metadata.Author.Username = "updated-author";
                source.Beatmaps[0].Difficulty.DrainRate = 9.5f;
                source.Beatmaps[0].UserSettings.Offset = originalOffset + 100;
                source.Beatmaps[0].Metadata.UserTags.Clear();
                source.Beatmaps.RemoveAt(1);
                source.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = "ignored-file" }, "ignored.png"));
                var added = new BeatmapInfo(source.Beatmaps[0].Ruleset) { DifficultyName = "added-difficulty", BeatmapSet = source };
                source.Beatmaps.Add(added);

                access.Write(realm =>
                {
                    source.CopyChangesToRealm(destination);
                    Assert.That(destination.Hash, Is.EqualTo("updated-set"));
                    Assert.That(destination.Beatmaps.Count, Is.EqualTo(3));
                    Assert.That(destination.Beatmaps.Any(b => b.ID == retainedBeatmapId), Is.True);
                    BeatmapInfo updated = destination.Beatmaps[0];
                    Assert.That(updated.DifficultyName, Is.EqualTo("updated-difficulty"));
                    Assert.That(EqualityComparer<BeatmapMetadata>.Default.Equals(updated.Metadata, originalMetadata), Is.True);
                    Assert.That(EqualityComparer<BeatmapDifficulty>.Default.Equals(updated.Difficulty, originalDifficulty), Is.True);
                    Assert.That(EqualityComparer<RealmUser>.Default.Equals(updated.Metadata.Author, originalAuthor), Is.True);
                    Assert.That(updated.Metadata.Title, Is.EqualTo("updated-title"));
                    Assert.That(updated.Metadata.Author.Username, Is.EqualTo("updated-author"));
                    Assert.That(updated.Difficulty.DrainRate, Is.EqualTo(9.5f));
                    Assert.That(updated.UserSettings.Offset, Is.EqualTo(originalOffset));
                    Assert.That(updated.Metadata.UserTags, Is.EqualTo(originalTags));
                    Assert.That(destination.Files.Count, Is.EqualTo(originalFileCount));
                    Assert.That(updated.Ruleset, Is.EqualTo(realm.Find<RulesetInfo>(updated.Ruleset.ShortName)));
                    BeatmapInfo newRow = realm.Find<BeatmapInfo>(added.ID)!;
                    Assert.That(newRow.IsManaged, Is.True);
                    Assert.That(newRow.BeatmapSet, Is.EqualTo(destination));
                    Assert.That(newRow.DifficultyName, Is.EqualTo("added-difficulty"));
                });
            });
        }

        private static RealmObjectBase createManagedModel(Realm realm, string model) => model switch
        {
            "set" => realm.Add(populate(createSet())),
            "beatmap" => realm.Add(populate(createSet())).Beatmaps[0],
            "metadata" => realm.Add(populate(new BeatmapMetadata { UserTags = { "first", "second" } })),
            "difficulty" => realm.Add(populate(new BeatmapInfo())).Difficulty,
            "settings" => realm.Add(populate(new BeatmapInfo())).UserSettings,
            "ruleset" => realm.Add(populate(new RulesetInfo())),
            "user" => realm.Add(populate(new BeatmapMetadata())).Author,
            "file" => realm.Add(populate(new RealmFile())),
            "file-usage" => realm.Add(populate(createSkin())).Files[0],
            "collection" => realm.Add(populate(new BeatmapCollection("Collection", new[] { "one", "two" }))),
            "binding" => realm.Add(populate(new RealmKeyBinding(1, string.Empty))),
            "skin" => realm.Add(populate(createSkin())),
            "score" => realm.Add(populate(new ScoreInfo(createSet().Beatmaps[0]) { Pauses = { 123, 456 }, Files = { createFile("replay.osr") } })),
            _ => throw new ArgumentOutOfRangeException(nameof(model)),
        };

        private static BeatmapSetInfo createSet()
        {
            var source = new BeatmapSetInfo(null)
            {
                Beatmaps =
                {
                    new BeatmapInfo { Metadata = new BeatmapMetadata { UserTags = { "first", "second" } } },
                    new BeatmapInfo { Metadata = new BeatmapMetadata { UserTags = { "third" } } },
                },
                Files = { createFile("first.osu"), createFile("second.osu") },
            };
            foreach (var beatmap in source.Beatmaps)
                beatmap.BeatmapSet = source;
            return source;
        }

        private static SkinInfo createSkin() => new SkinInfo { Files = { createFile("note.png"), createFile("beam.png") } };

        private static RealmNamedFileUsage createFile(string filename) => new RealmNamedFileUsage(new RealmFile { Hash = Guid.NewGuid().ToString("N") }, filename);

        private static T populate<T>(T root) where T : RealmObjectBase
        {
            var queue = new Queue<RealmObjectBase>();
            var visited = new HashSet<RealmObjectBase>(ReferenceEqualityComparer.Instance);
            queue.Enqueue(root);

            while (queue.TryDequeue(out var source))
            {
                if (!visited.Add(source))
                    continue;

                foreach (PropertyInfo property in persistedProperties(source.GetType()))
                {
                    Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                    if (property.SetMethod?.IsPublic == true)
                    {
                        object? value = type == typeof(string) ? $"{property.Name}-{Guid.NewGuid():N}"
                            : type == typeof(Guid) ? Guid.NewGuid()
                            : type == typeof(int) ? 37
                            : type == typeof(long) ? 123456789L
                            : type == typeof(double) ? 23.75
                            : type == typeof(float) ? 7.25f
                            : type == typeof(bool) ? true
                            : type == typeof(DateTimeOffset) ? DateTimeOffset.Parse("2026-10-22T11:00:00Z")
                            : null;
                        if (value != null)
                            property.SetValue(source, value);
                    }

                    object? child = property.GetValue(source);
                    if (child is RealmObjectBase related)
                        queue.Enqueue(related);
                    else if (child is IEnumerable children && child is not string)
                        foreach (object entry in children)
                            if (entry is RealmObjectBase relatedEntry)
                                queue.Enqueue(relatedEntry);
                }
            }

            return root;
        }

        private static IEnumerable<PropertyInfo> persistedProperties(Type type) => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetMethod?.IsPublic == true
                               && property.GetCustomAttribute<IgnoredAttribute>() == null
                               && property.GetCustomAttribute<BacklinkAttribute>() == null
                               && property.GetCustomAttribute<IgnoreDataMemberAttribute>() == null
                               && (property.SetMethod?.IsPublic == true
                                   || property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(IList<>)));

        private static void assertDetachedGraph(RealmObjectBase expected, RealmObjectBase actual, HashSet<(RealmObjectBase, RealmObjectBase)> visited, bool omitFiles = false)
        {
            if (!visited.Add((expected, actual)))
                return;

            Assert.That(actual.IsManaged, Is.False);
            Assert.That(actual, Is.Not.SameAs(expected));
            foreach (PropertyInfo property in persistedProperties(expected.GetType()))
            {
                object? sourceValue = property.GetValue(expected);
                object? copyValue = property.GetValue(actual);
                if (omitFiles && property.Name == nameof(BeatmapSetInfo.Files))
                {
                    Assert.That((IEnumerable)copyValue!, Is.Empty);
                    continue;
                }

                if (sourceValue is RealmObjectBase sourceObject)
                {
                    Assert.That(copyValue, Is.InstanceOf<RealmObjectBase>(), property.Name);
                    assertDetachedGraph(sourceObject, (RealmObjectBase)copyValue!, visited);
                }
                else if (sourceValue is IEnumerable sourceList && sourceValue is not string)
                {
                    object[] expectedItems = sourceList.Cast<object>().ToArray();
                    object[] actualItems = ((IEnumerable)copyValue!).Cast<object>().ToArray();
                    Assert.That(actualItems.Length, Is.EqualTo(expectedItems.Length), property.Name);
                    for (int i = 0; i < expectedItems.Length; i++)
                    {
                        if (expectedItems[i] is RealmObjectBase sourceEntry)
                            assertDetachedGraph(sourceEntry, (RealmObjectBase)actualItems[i], visited);
                        else
                            Assert.That(actualItems[i], Is.EqualTo(expectedItems[i]), property.Name);
                    }
                }
                else
                    Assert.That(copyValue, Is.EqualTo(sourceValue), $"{expected.GetType().Name}.{property.Name}");
            }
        }
    }
}
