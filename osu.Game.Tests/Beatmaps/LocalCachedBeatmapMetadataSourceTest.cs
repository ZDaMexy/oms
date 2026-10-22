// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;

namespace osu.Game.Tests.Beatmaps
{
    [TestFixture]
    [NonParallelizable]
    public class LocalCachedBeatmapMetadataSourceTest
    {
        private TemporaryNativeStorage storage = null!;

        private const string checksum = "0123456789abcdef0123456789abcdef";
        private const string filename = "artist - title ['quoted'].osu";

        private static readonly DateTimeOffset last_updated = new DateTimeOffset(2026, 10, 1, 1, 2, 3, TimeSpan.FromHours(8));
        private static readonly DateTimeOffset date_submitted = last_updated.AddDays(-2);
        private static readonly DateTimeOffset date_ranked = last_updated.AddDays(-1);

        [SetUp]
        public void SetUp()
        {
            storage = new TemporaryNativeStorage($"sqlite-metadata-{Guid.NewGuid():N}");
            SQLitePCL.Batteries_V2.Init();
        }

        [TearDown]
        public void TearDown()
        {
            // Production metadata lookups use pooling, so release their file handles before
            // deleting this fixture's disposable storage on Windows.
            SqliteConnection.ClearAllPools();
            storage.Dispose();
        }

        [Test]
        public void TestActualNativeEngineIncludesSecurityFixes()
        {
            using var connection = openDatabase("native.db");
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT sqlite_version(), sqlite_source_id()";
            using var reader = command.ExecuteReader();

            Assert.That(reader.Read(), Is.True);
            Assert.That(Version.Parse(reader.GetString(0)), Is.GreaterThanOrEqualTo(new Version(3, 54, 1)),
                "Check the library actually loaded by Microsoft.Data.Sqlite, including published native asset selection.");
            Assert.That(reader.GetString(1), Does.Match(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} [0-9a-f]{64}$"));
        }

        [TestCase(2, false)]
        [TestCase(2, true)]
        [TestCase(3, false)]
        [TestCase(3, true)]
        public void TestExistingCacheSchemasAndParameterizedLookup(int schemaVersion, bool lookupByFilename)
        {
            createMetadataCache(schemaVersion);
            using var source = new LocalCachedBeatmapMetadataSource(storage, allowRemoteFetch: false);

            Assert.That(source.Available, Is.True);
            Assert.That(source.IsAtLeastVersion(schemaVersion), Is.True);
            Assert.That(source.IsAtLeastVersion(schemaVersion + 1), Is.False);

            var beatmap = createBeatmap(lookupByFilename ? string.Empty : checksum, lookupByFilename ? filename : "unmatched.osu");
            Assert.That(source.TryLookup(beatmap, out var metadata), Is.True);
            Assert.That(metadata, Is.Not.Null);
            Assert.That(metadata!.BeatmapID, Is.EqualTo(123));
            Assert.That(metadata.BeatmapSetID, Is.EqualTo(456));
            Assert.That(metadata.AuthorID, Is.EqualTo(789));
            Assert.That(metadata.BeatmapStatus, Is.EqualTo(BeatmapOnlineStatus.Ranked));
            Assert.That(metadata.BeatmapSetStatus, Is.EqualTo(BeatmapOnlineStatus.Ranked));
            Assert.That(metadata.MD5Hash, Is.EqualTo(checksum));
            Assert.That(metadata.LastUpdated, Is.EqualTo(last_updated));
            Assert.That(metadata.DateSubmitted, Is.EqualTo(date_submitted));
            Assert.That(metadata.DateRanked, Is.EqualTo(date_ranked));
            Assert.That(metadata.UserTags, Is.EqualTo(schemaVersion == 3 ? new[] { "鍵盤 'tag'" } : Array.Empty<string>()));

            var unmatched = createBeatmap("' OR 1=1 --", "' OR 1=1 --");
            Assert.That(source.TryLookup(unmatched, out var missing), Is.False, "Lookup values must remain literal SQL parameters.");
            Assert.That(missing, Is.Null);
        }

        [Test]
        public void TestWalCheckpointAndReopenPreserveCommittedRows()
        {
            const string database_name = "wal.db";

            using (var writer = openDatabase(database_name))
            {
                using (var journal = writer.CreateCommand())
                {
                    journal.CommandText = "PRAGMA journal_mode=WAL";
                    Assert.That(journal.ExecuteScalar(), Is.EqualTo("wal"));
                }

                using (var schema = writer.CreateCommand())
                {
                    schema.CommandText = "PRAGMA wal_autocheckpoint=0; CREATE TABLE entries (id INTEGER PRIMARY KEY, value TEXT NOT NULL)";
                    schema.ExecuteNonQuery();
                }

                using var reader = openDatabase(database_name);
                using (var snapshot = reader.BeginTransaction(deferred: true))
                {
                    Assert.That(countRows(reader, snapshot), Is.Zero);

                    using (var transaction = writer.BeginTransaction())
                    using (var insert = writer.CreateCommand())
                    {
                        insert.Transaction = transaction;
                        insert.CommandText = "INSERT INTO entries (id, value) VALUES (@id, @value)";
                        var id = insert.Parameters.Add("@id", SqliteType.Integer);
                        var value = insert.Parameters.Add("@value", SqliteType.Text);

                        for (int i = 1; i <= 16; i++)
                        {
                            id.Value = i;
                            value.Value = $"谱面 '{i}'";
                            insert.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }

                    using (var checkpoint = writer.CreateCommand())
                    {
                        checkpoint.CommandText = "PRAGMA wal_checkpoint(PASSIVE)";
                        using var result = checkpoint.ExecuteReader();
                        Assert.That(result.Read(), Is.True);
                        Assert.That(result.GetInt32(0), Is.Zero);
                    }

                    Assert.That(countRows(reader, snapshot), Is.Zero, "The active reader must retain its original snapshot.");
                    Assert.That(countRows(writer), Is.EqualTo(16));
                    snapshot.Commit();
                }

                using (var checkpoint = writer.CreateCommand())
                {
                    checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
                    using var result = checkpoint.ExecuteReader();
                    Assert.That(result.Read(), Is.True);
                    Assert.That(result.GetInt32(0), Is.Zero);
                }
            }

            using var reopened = openDatabase(database_name);
            Assert.That(countRows(reopened), Is.EqualTo(16));
            using var integrity = reopened.CreateCommand();
            integrity.CommandText = "PRAGMA integrity_check";
            Assert.That(integrity.ExecuteScalar(), Is.EqualTo("ok"));

            using var values = reopened.CreateCommand();
            values.CommandText = "SELECT id, value FROM entries ORDER BY id";
            using var rows = values.ExecuteReader();
            for (int i = 1; i <= 16; i++)
            {
                Assert.That(rows.Read(), Is.True);
                Assert.That(rows.GetInt32(0), Is.EqualTo(i));
                Assert.That(rows.GetString(1), Is.EqualTo($"谱面 '{i}'"));
            }

            Assert.That(rows.Read(), Is.False);
        }

        private SqliteConnection openDatabase(string filename)
        {
            var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = storage.GetFullPath(filename),
                Pooling = false,
            }.ToString());
            connection.Open();
            return connection;
        }

        private static long countRows(SqliteConnection connection, SqliteTransaction? transaction = null)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT COUNT(*) FROM entries";
            return (long)command.ExecuteScalar()!;
        }

        private void createMetadataCache(int schemaVersion)
        {
            using var connection = openDatabase("online.db");
            using var command = connection.CreateCommand();
            command.CommandText = """
                                  CREATE TABLE schema_version (number INTEGER NOT NULL);
                                  INSERT INTO schema_version VALUES (@schemaVersion);
                                  CREATE TABLE osu_beatmapsets (beatmapset_id INTEGER PRIMARY KEY, submit_date TEXT, approved_date TEXT);
                                  INSERT INTO osu_beatmapsets VALUES (456, @submitted, @ranked);
                                  CREATE TABLE osu_beatmaps (beatmap_id INTEGER PRIMARY KEY, beatmapset_id INTEGER, approved INTEGER,
                                                           user_id INTEGER, checksum TEXT, filename TEXT, last_update TEXT);
                                  INSERT INTO osu_beatmaps VALUES (123, 456, @status, 789, @checksum, @filename, @updated);
                                  CREATE TABLE tags (id INTEGER PRIMARY KEY, name TEXT);
                                  INSERT INTO tags VALUES (1, @tag);
                                  CREATE TABLE beatmap_tags (beatmap_id INTEGER, tag_id INTEGER);
                                  INSERT INTO beatmap_tags VALUES (123, 1);
                                  """;
            command.Parameters.AddWithValue("@schemaVersion", schemaVersion);
            command.Parameters.AddWithValue("@submitted", date_submitted);
            command.Parameters.AddWithValue("@ranked", date_ranked);
            command.Parameters.AddWithValue("@status", (int)BeatmapOnlineStatus.Ranked);
            command.Parameters.AddWithValue("@checksum", checksum);
            command.Parameters.AddWithValue("@filename", filename);
            command.Parameters.AddWithValue("@updated", last_updated);
            command.Parameters.AddWithValue("@tag", "鍵盤 'tag'");
            command.ExecuteNonQuery();
        }

        private static BeatmapInfo createBeatmap(string md5, string path)
        {
            var set = new BeatmapSetInfo(null);
            var beatmap = new BeatmapInfo
            {
                MD5Hash = md5,
                LocalFilePath = path,
                BeatmapSet = set,
            };
            set.Beatmaps.Add(beatmap);
            return beatmap;
        }
    }
}
