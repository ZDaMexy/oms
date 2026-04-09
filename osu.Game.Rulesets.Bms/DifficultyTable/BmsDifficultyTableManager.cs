// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;
using SQLitePCL;

namespace osu.Game.Rulesets.Bms.DifficultyTable
{
    public class BmsDifficultyTableManager
    {
        private const string database_name = "tables.db";
        private const string presets_resource_name = "bms_table_presets.json";

        private static readonly object shared_manager_lock = new object();
        private static readonly Dictionary<string, BmsDifficultyTableManager> shared_managers = new Dictionary<string, BmsDifficultyTableManager>(StringComparer.OrdinalIgnoreCase);

        private static readonly Regex bmstable_meta_regex = new Regex("<meta\\s+name=[\"']bmstable[\"']\\s+content=[\"'](?<path>[^\"']+)[\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex signed_integer_regex = new Regex("^-?\\d+$", RegexOptions.Compiled);
        private static readonly Regex numeric_level_regex = new Regex("-?\\d+", RegexOptions.Compiled);
        private static readonly Regex md5_regex = new Regex("^[0-9a-f]{32}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly Storage databaseStorage;

        public event Action? TableDataChanged;

        public static BmsDifficultyTableManager GetShared(Storage storage)
        {
            ArgumentNullException.ThrowIfNull(storage);

            string storagePath = Path.GetFullPath(storage.GetFullPath(string.Empty));

            lock (shared_manager_lock)
            {
                if (!shared_managers.TryGetValue(storagePath, out var manager))
                    shared_managers[storagePath] = manager = new BmsDifficultyTableManager(storage);

                return manager;
            }
        }

        public BmsDifficultyTableManager(Storage storage)
        {
            try
            {
                Batteries_V2.Init();
                raw.sqlite3_config(2);
            }
            catch
            {
            }

            databaseStorage = storage.GetStorageForDirectory("bms-difficulty-tables");

            ensureSchema();
            ensurePresetSources();
        }

        public IReadOnlyList<BmsDifficultyTableSourceInfo> GetSources()
        {
            using var connection = getConnection();
            connection.Open();

            var entriesBySource = loadEntries(connection)
                                .GroupBy(entry => entry.SourceId)
                                .ToDictionary(group => group.Key, group => (IReadOnlyList<BmsDifficultyTableEntry>)group.Select(entry => entry.Entry).ToList());

            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT `id`, `source_name`, `display_name`, `symbol`, `local_path`, `is_preset`, `enabled`, `sort_order`, `imported_at`, `last_refreshed`
                FROM `sources`
                ORDER BY `sort_order`, `display_name`, `source_name`
                """;

            using var reader = command.ExecuteReader();

            List<BmsDifficultyTableSourceInfo> results = new List<BmsDifficultyTableSourceInfo>();

            while (reader.Read())
            {
                Guid id = Guid.Parse(reader.GetString(0));

                results.Add(new BmsDifficultyTableSourceInfo(
                    id,
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.GetInt64(5) != 0,
                    reader.GetInt64(6) != 0,
                    reader.GetInt32(7),
                    DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(8)),
                    reader.IsDBNull(9) ? null : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(9)),
                        entriesBySource.GetValueOrDefault(id, Array.Empty<BmsDifficultyTableEntry>())));
            }

            return results;
        }

        public IReadOnlyList<BmsDifficultyTableEntry> GetAllEntries(bool onlyEnabled = true)
        {
            using var connection = getConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                SELECT `e`.`table_name`, `e`.`symbol`, `e`.`level`, `e`.`level_label`, `e`.`md5`, `s`.`sort_order`
                FROM `entries` AS `e`
                JOIN `sources` AS `s` ON `s`.`id` = `e`.`source_id`
                {(onlyEnabled ? "WHERE `s`.`enabled` = 1" : string.Empty)}
                ORDER BY `s`.`sort_order`, `e`.`level`, `e`.`level_label`, `e`.`md5`
                """;

            using var reader = command.ExecuteReader();
            return readEntries(reader);
        }

        public IReadOnlyList<BmsDifficultyTableEntry> GetEntriesForMd5(string md5, bool onlyEnabled = true)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(md5);

            using var connection = getConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                SELECT `e`.`table_name`, `e`.`symbol`, `e`.`level`, `e`.`level_label`, `e`.`md5`, `s`.`sort_order`
                FROM `entries` AS `e`
                JOIN `sources` AS `s` ON `s`.`id` = `e`.`source_id`
                WHERE `e`.`md5` = @md5
                {(onlyEnabled ? "AND `s`.`enabled` = 1" : string.Empty)}
                ORDER BY `s`.`sort_order`, `e`.`level`, `e`.`level_label`, `e`.`md5`
                """;
            command.Parameters.Add(new SqliteParameter("@md5", md5.Trim().ToLowerInvariant()));

            using var reader = command.ExecuteReader();
            return readEntries(reader);
        }

        public Task<BmsDifficultyTableSourceInfo> ImportFromPath(string path, Guid? sourceId = null, bool enabled = true, CancellationToken cancellationToken = default)
            => Task.Run(() => importFromPath(path, sourceId, enabled, cancellationToken), cancellationToken);

        public Task<BmsDifficultyTableSourceInfo> RefreshTable(Guid sourceId, CancellationToken cancellationToken = default)
            => Task.Run(() => refreshTable(sourceId, cancellationToken), cancellationToken);

        public Task RefreshAllTables(CancellationToken cancellationToken = default)
            => Task.Run(() => refreshAllTables(cancellationToken), cancellationToken);

        public void SetSourceEnabled(Guid sourceId, bool enabled)
        {
            bool changed = false;

            using var connection = getConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE `sources`
                SET `enabled` = @enabled
                WHERE `id` = @id AND `enabled` != @enabled
                """;
            command.Parameters.Add(new SqliteParameter("@enabled", enabled ? 1 : 0));
            command.Parameters.Add(new SqliteParameter("@id", sourceId.ToString("D")));
            changed = command.ExecuteNonQuery() > 0;

            if (changed)
                TableDataChanged?.Invoke();
        }

        public void RemoveSource(Guid sourceId)
        {
            using var connection = getConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();

            using (var deleteEntries = connection.CreateCommand())
            {
                deleteEntries.Transaction = transaction;
                deleteEntries.CommandText = "DELETE FROM `entries` WHERE `source_id` = @sourceId";
                deleteEntries.Parameters.Add(new SqliteParameter("@sourceId", sourceId.ToString("D")));
                deleteEntries.ExecuteNonQuery();
            }

            bool removed;

            using (var deleteSource = connection.CreateCommand())
            {
                deleteSource.Transaction = transaction;
                deleteSource.CommandText = "DELETE FROM `sources` WHERE `id` = @id";
                deleteSource.Parameters.Add(new SqliteParameter("@id", sourceId.ToString("D")));
                removed = deleteSource.ExecuteNonQuery() > 0;
            }

            transaction.Commit();

            if (removed)
                TableDataChanged?.Invoke();
        }

        private BmsDifficultyTableSourceInfo importFromPath(string path, Guid? sourceId, bool enabled, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string fullPath = Path.GetFullPath(path);
            ParsedTableSource parsedSource = loadTableSource(fullPath, cancellationToken);

            using var connection = getConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();

            SourceRow? existingSource = sourceId.HasValue
                ? findSource(connection, transaction, sourceId.Value)
                                : findSourceByLocalPath(connection, transaction, fullPath)
                                    ?? findImportPreset(connection, transaction, parsedSource);

            if (sourceId.HasValue && existingSource == null)
                throw new InvalidOperationException($"Could not find difficulty table source {sourceId.Value}.");

            Guid resolvedId;
            string sourceName;
            string displayName;
            string symbol;
            bool isPreset;
            int sortOrder;
            long importedAt;

            if (existingSource is SourceRow currentSource)
            {
                resolvedId = currentSource.ID;
                sourceName = currentSource.SourceName;
                displayName = currentSource.IsPreset && !string.IsNullOrWhiteSpace(currentSource.DisplayName)
                    ? currentSource.DisplayName
                    : parsedSource.DisplayName;
                symbol = string.IsNullOrWhiteSpace(parsedSource.Symbol)
                    ? currentSource.Symbol
                    : parsedSource.Symbol;
                isPreset = currentSource.IsPreset;
                sortOrder = currentSource.SortOrder;
                importedAt = currentSource.ImportedAtUnixMs;
            }
            else
            {
                resolvedId = Guid.NewGuid();
                sourceName = parsedSource.SourceName;
                displayName = parsedSource.DisplayName;
                symbol = parsedSource.Symbol;
                isPreset = false;
                sortOrder = getNextSortOrder(connection, transaction);
                importedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            long refreshedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var entries = buildEntries(displayName, symbol, sortOrder, parsedSource.Entries);

            upsertSource(connection, transaction, new SourceRow(resolvedId, sourceName, displayName, symbol, fullPath, isPreset, enabled, sortOrder, importedAt, refreshedAt));
            replaceEntries(connection, transaction, resolvedId, entries);

            transaction.Commit();

            TableDataChanged?.Invoke();
            return GetSources().Single(source => source.ID == resolvedId);
        }

        private BmsDifficultyTableSourceInfo refreshTable(Guid sourceId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var connection = getConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();

            SourceRow existingSource = findSource(connection, transaction, sourceId)
                                       ?? throw new InvalidOperationException($"Could not find difficulty table source {sourceId}.");

            if (string.IsNullOrWhiteSpace(existingSource.LocalPath))
                throw new InvalidOperationException($"Difficulty table source '{existingSource.DisplayName}' has no local path configured.");

            ParsedTableSource parsedSource = loadTableSource(existingSource.LocalPath, cancellationToken);

            string displayName = existingSource.IsPreset && !string.IsNullOrWhiteSpace(existingSource.DisplayName)
                ? existingSource.DisplayName
                : parsedSource.DisplayName;
            string symbol = string.IsNullOrWhiteSpace(parsedSource.Symbol)
                ? existingSource.Symbol
                : parsedSource.Symbol;

            var entries = buildEntries(displayName, symbol, existingSource.SortOrder, parsedSource.Entries);

            upsertSource(connection, transaction, existingSource with
            {
                Symbol = symbol,
                DisplayName = displayName,
                LastRefreshedUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            });
            replaceEntries(connection, transaction, sourceId, entries);

            transaction.Commit();

            TableDataChanged?.Invoke();
            return GetSources().Single(source => source.ID == sourceId);
        }

        private void refreshAllTables(CancellationToken cancellationToken)
        {
            List<Guid> refreshableSources;

            using (var connection = getConnection())
            {
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT `id` FROM `sources` WHERE `local_path` IS NOT NULL AND `local_path` != '' ORDER BY `sort_order`, `display_name`";

                using var reader = command.ExecuteReader();
                refreshableSources = new List<Guid>();

                while (reader.Read())
                    refreshableSources.Add(Guid.Parse(reader.GetString(0)));
            }

            bool anyRefreshed = false;

            foreach (Guid sourceId in refreshableSources)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    refreshTable(sourceId, cancellationToken);
                    anyRefreshed = true;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to refresh BMS difficulty table source {sourceId}.");
                }
            }

            if (!anyRefreshed)
                return;
        }

        private void ensureSchema()
        {
            using var connection = getConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
                """
                PRAGMA foreign_keys = ON;

                CREATE TABLE IF NOT EXISTS `sources`
                (
                    `id` TEXT PRIMARY KEY NOT NULL,
                    `source_name` TEXT NOT NULL,
                    `display_name` TEXT NOT NULL,
                    `symbol` TEXT NOT NULL,
                    `local_path` TEXT,
                    `is_preset` INTEGER NOT NULL,
                    `enabled` INTEGER NOT NULL,
                    `sort_order` INTEGER NOT NULL,
                    `imported_at` INTEGER NOT NULL,
                    `last_refreshed` INTEGER
                );

                CREATE TABLE IF NOT EXISTS `entries`
                (
                    `source_id` TEXT NOT NULL,
                    `table_name` TEXT NOT NULL,
                    `symbol` TEXT NOT NULL,
                    `level` INTEGER NOT NULL,
                    `level_label` TEXT NOT NULL,
                    `md5` TEXT NOT NULL,
                    FOREIGN KEY(`source_id`) REFERENCES `sources`(`id`) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS `idx_sources_source_name` ON `sources`(`source_name`, `is_preset`);
                CREATE INDEX IF NOT EXISTS `idx_sources_local_path` ON `sources`(`local_path`);
                CREATE INDEX IF NOT EXISTS `idx_entries_source_id` ON `entries`(`source_id`);
                CREATE INDEX IF NOT EXISTS `idx_entries_md5` ON `entries`(`md5`);
                """;
            command.ExecuteNonQuery();
        }

        private void ensurePresetSources()
        {
            var presets = loadPresets();

            using var connection = getConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();

            for (int i = 0; i < presets.Count; i++)
            {
                PresetDefinition preset = presets[i];
                SourceRow? existingPreset = findPreset(connection, transaction, preset.SourceName);

                if (existingPreset == null)
                {
                    upsertSource(connection, transaction, new SourceRow(
                        Guid.NewGuid(),
                        preset.SourceName,
                        preset.DisplayName,
                        preset.Symbol,
                        null,
                        true,
                        false,
                        i,
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        null));

                    continue;
                }

                if (existingPreset is SourceRow presetSource)
                {
                    upsertSource(connection, transaction, presetSource with
                    {
                        DisplayName = preset.DisplayName,
                        Symbol = preset.Symbol,
                        SortOrder = i,
                    });
                }
            }

            transaction.Commit();
        }

        private SqliteConnection getConnection()
            => new SqliteConnection($"Data Source={databaseStorage.GetFullPath(database_name, true)}");

        private static SourceRow? findSource(SqliteConnection connection, SqliteTransaction transaction, Guid sourceId)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                SELECT `id`, `source_name`, `display_name`, `symbol`, `local_path`, `is_preset`, `enabled`, `sort_order`, `imported_at`, `last_refreshed`
                FROM `sources`
                WHERE `id` = @id
                LIMIT 1
                """;
            command.Parameters.Add(new SqliteParameter("@id", sourceId.ToString("D")));

            using var reader = command.ExecuteReader();
            return reader.Read() ? readSource(reader) : null;
        }

        private static SourceRow? findSourceByLocalPath(SqliteConnection connection, SqliteTransaction transaction, string fullPath)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                SELECT `id`, `source_name`, `display_name`, `symbol`, `local_path`, `is_preset`, `enabled`, `sort_order`, `imported_at`, `last_refreshed`
                FROM `sources`
                WHERE `local_path` = @path
                LIMIT 1
                """;
            command.Parameters.Add(new SqliteParameter("@path", fullPath));

            using var reader = command.ExecuteReader();
            return reader.Read() ? readSource(reader) : null;
        }

        private static SourceRow? findImportPreset(SqliteConnection connection, SqliteTransaction transaction, ParsedTableSource parsedSource)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                SELECT `id`, `source_name`, `display_name`, `symbol`, `local_path`, `is_preset`, `enabled`, `sort_order`, `imported_at`, `last_refreshed`
                FROM `sources`
                WHERE `is_preset` = 1
                  AND (`local_path` IS NULL OR `local_path` = '')
                  AND (`source_name` = @sourceName COLLATE NOCASE OR `display_name` = @displayName COLLATE NOCASE)
                ORDER BY CASE WHEN `source_name` = @sourceName COLLATE NOCASE THEN 0 ELSE 1 END,
                         `sort_order`,
                         `display_name`
                LIMIT 1
                """;
            command.Parameters.Add(new SqliteParameter("@sourceName", parsedSource.SourceName));
            command.Parameters.Add(new SqliteParameter("@displayName", parsedSource.DisplayName));

            using var reader = command.ExecuteReader();
            return reader.Read() ? readSource(reader) : null;
        }

        private static SourceRow? findPreset(SqliteConnection connection, SqliteTransaction transaction, string sourceName)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                SELECT `id`, `source_name`, `display_name`, `symbol`, `local_path`, `is_preset`, `enabled`, `sort_order`, `imported_at`, `last_refreshed`
                FROM `sources`
                WHERE `source_name` = @sourceName AND `is_preset` = 1
                LIMIT 1
                """;
            command.Parameters.Add(new SqliteParameter("@sourceName", sourceName));

            using var reader = command.ExecuteReader();
            return reader.Read() ? readSource(reader) : null;
        }

        private static int getNextSortOrder(SqliteConnection connection, SqliteTransaction transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT COALESCE(MAX(`sort_order`), -1) + 1 FROM `sources`";
            return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        private static void upsertSource(SqliteConnection connection, SqliteTransaction transaction, SourceRow source)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO `sources`
                    (`id`, `source_name`, `display_name`, `symbol`, `local_path`, `is_preset`, `enabled`, `sort_order`, `imported_at`, `last_refreshed`)
                VALUES
                    (@id, @sourceName, @displayName, @symbol, @localPath, @isPreset, @enabled, @sortOrder, @importedAt, @lastRefreshed)
                ON CONFLICT(`id`) DO UPDATE SET
                    `source_name` = excluded.`source_name`,
                    `display_name` = excluded.`display_name`,
                    `symbol` = excluded.`symbol`,
                    `local_path` = excluded.`local_path`,
                    `is_preset` = excluded.`is_preset`,
                    `enabled` = excluded.`enabled`,
                    `sort_order` = excluded.`sort_order`,
                    `imported_at` = excluded.`imported_at`,
                    `last_refreshed` = excluded.`last_refreshed`
                """;

            command.Parameters.Add(new SqliteParameter("@id", source.ID.ToString("D")));
            command.Parameters.Add(new SqliteParameter("@sourceName", source.SourceName));
            command.Parameters.Add(new SqliteParameter("@displayName", source.DisplayName));
            command.Parameters.Add(new SqliteParameter("@symbol", source.Symbol));
            command.Parameters.Add(new SqliteParameter("@localPath", (object?)source.LocalPath ?? DBNull.Value));
            command.Parameters.Add(new SqliteParameter("@isPreset", source.IsPreset ? 1 : 0));
            command.Parameters.Add(new SqliteParameter("@enabled", source.Enabled ? 1 : 0));
            command.Parameters.Add(new SqliteParameter("@sortOrder", source.SortOrder));
            command.Parameters.Add(new SqliteParameter("@importedAt", source.ImportedAtUnixMs));
            command.Parameters.Add(new SqliteParameter("@lastRefreshed", source.LastRefreshedUnixMs.HasValue ? source.LastRefreshedUnixMs.Value : DBNull.Value));
            command.ExecuteNonQuery();
        }

        private static void replaceEntries(SqliteConnection connection, SqliteTransaction transaction, Guid sourceId, IReadOnlyList<BmsDifficultyTableEntry> entries)
        {
            using (var deleteCommand = connection.CreateCommand())
            {
                deleteCommand.Transaction = transaction;
                deleteCommand.CommandText = "DELETE FROM `entries` WHERE `source_id` = @sourceId";
                deleteCommand.Parameters.Add(new SqliteParameter("@sourceId", sourceId.ToString("D")));
                deleteCommand.ExecuteNonQuery();
            }

            foreach (var entry in entries)
            {
                using var insertCommand = connection.CreateCommand();
                insertCommand.Transaction = transaction;
                insertCommand.CommandText =
                    """
                    INSERT INTO `entries` (`source_id`, `table_name`, `symbol`, `level`, `level_label`, `md5`)
                    VALUES (@sourceId, @tableName, @symbol, @level, @levelLabel, @md5)
                    """;
                insertCommand.Parameters.Add(new SqliteParameter("@sourceId", sourceId.ToString("D")));
                insertCommand.Parameters.Add(new SqliteParameter("@tableName", entry.TableName));
                insertCommand.Parameters.Add(new SqliteParameter("@symbol", entry.Symbol));
                insertCommand.Parameters.Add(new SqliteParameter("@level", entry.Level));
                insertCommand.Parameters.Add(new SqliteParameter("@levelLabel", entry.LevelLabel));
                insertCommand.Parameters.Add(new SqliteParameter("@md5", entry.Md5));
                insertCommand.ExecuteNonQuery();
            }
        }

        private static IReadOnlyList<BmsDifficultyTableEntry> readEntries(SqliteDataReader reader)
        {
            List<BmsDifficultyTableEntry> entries = new List<BmsDifficultyTableEntry>();

            while (reader.Read())
            {
                entries.Add(new BmsDifficultyTableEntry(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetInt32(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetInt32(5)));
            }

            return entries;
        }

        private static List<EntryRow> loadEntries(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT `e`.`source_id`, `e`.`table_name`, `e`.`symbol`, `e`.`level`, `e`.`level_label`, `e`.`md5`, `s`.`sort_order`
                FROM `entries` AS `e`
                JOIN `sources` AS `s` ON `s`.`id` = `e`.`source_id`
                ORDER BY `s`.`sort_order`, `e`.`level`, `e`.`level_label`, `e`.`md5`
                """;

            using var reader = command.ExecuteReader();
            List<EntryRow> rows = new List<EntryRow>();

            while (reader.Read())
            {
                rows.Add(new EntryRow(
                    Guid.Parse(reader.GetString(0)),
                    new BmsDifficultyTableEntry(
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetInt32(3),
                        reader.GetString(4),
                        reader.GetString(5),
                        reader.GetInt32(6))));
            }

            return rows;
        }

        private static SourceRow readSource(SqliteDataReader reader) => new SourceRow(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetInt64(5) != 0,
            reader.GetInt64(6) != 0,
            reader.GetInt32(7),
            reader.GetInt64(8),
            reader.IsDBNull(9) ? null : reader.GetInt64(9));

        private static IReadOnlyList<BmsDifficultyTableEntry> buildEntries(string displayName, string symbol, int tableSortOrder, IReadOnlyList<ParsedTableEntry> parsedEntries)
            => parsedEntries.Select(entry => new BmsDifficultyTableEntry(displayName, symbol, entry.Level, buildLevelLabel(entry.LevelLabel, symbol), entry.Md5, tableSortOrder))
                            .GroupBy(entry => (entry.Md5, entry.Level, entry.LevelLabel))
                            .Select(group => group.First())
                            .OrderBy(entry => entry.Level)
                            .ThenBy(entry => entry.LevelLabel)
                            .ThenBy(entry => entry.Md5)
                            .ToList();

        private static string buildLevelLabel(string rawLabel, string symbol)
        {
            string trimmed = rawLabel.Trim();

            if (trimmed.Length == 0)
                return symbol;

            return signed_integer_regex.IsMatch(trimmed) && !string.IsNullOrWhiteSpace(symbol)
                ? $"{symbol}{trimmed}"
                : trimmed;
        }

        private static int parseLevel(string levelLabel)
        {
            Match match = numeric_level_regex.Match(levelLabel);

            if (!match.Success)
                return int.MaxValue;

            return int.Parse(match.Value, CultureInfo.InvariantCulture);
        }

        private static ParsedTableSource loadTableSource(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Directory.Exists(path))
                return loadTableSourceFromDirectory(path, cancellationToken);

            if (File.Exists(path))
                return loadTableSourceFromFile(path, getSourceNameFromPath(path), cancellationToken);

            throw new FileNotFoundException($"Could not find BMS difficulty table source at '{path}'.", path);
        }

        private static ParsedTableSource loadTableSourceFromDirectory(string directory, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string fallbackName = getSourceNameFromPath(directory);

            foreach (string htmlFile in Directory.EnumerateFiles(directory, "*.htm*").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    return loadTableSourceFromHtml(htmlFile, fallbackName, cancellationToken);
                }
                catch (InvalidOperationException)
                {
                }
            }

            string headerPath = Path.Combine(directory, "header.json");

            if (File.Exists(headerPath))
                return loadTableSourceFromFile(headerPath, fallbackName, cancellationToken);

            foreach (string jsonFile in Directory.EnumerateFiles(directory, "*.json").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    return loadTableSourceFromFile(jsonFile, fallbackName, cancellationToken);
                }
                catch (InvalidOperationException)
                {
                }
            }

            throw new InvalidOperationException($"No supported BMS difficulty table files were found in '{directory}'.");
        }

        private static ParsedTableSource loadTableSourceFromFile(string filePath, string fallbackName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string extension = Path.GetExtension(filePath);

            if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
                return loadTableSourceFromJson(filePath, fallbackName, cancellationToken);

            return loadTableSourceFromHtml(filePath, fallbackName, cancellationToken);
        }

        private static ParsedTableSource loadTableSourceFromHtml(string filePath, string fallbackName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string html = File.ReadAllText(filePath);
            Match match = bmstable_meta_regex.Match(html);

            if (!match.Success)
                throw new InvalidOperationException($"No bmstable meta tag was found in '{filePath}'.");

            string baseDirectory = Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException($"Could not resolve base directory for '{filePath}'.");
            string referencedPath = resolveLocalPath(baseDirectory, match.Groups["path"].Value);
            return loadTableSourceFromFile(referencedPath, fallbackName, cancellationToken);
        }

        private static ParsedTableSource loadTableSourceFromJson(string filePath, string fallbackName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            JToken token = JToken.Parse(File.ReadAllText(filePath));

            switch (token.Type)
            {
                case JTokenType.Array:
                    return createParsedTableSource(fallbackName, fallbackName, string.Empty, (JArray)token);

                case JTokenType.Object:
                {
                    JObject obj = (JObject)token;

                    if (obj["data_url"] != null)
                    {
                        string? displayName = obj.Value<string>("name")?.Trim();

                        if (string.IsNullOrWhiteSpace(displayName))
                            displayName = fallbackName;

                        string symbol = obj.Value<string>("symbol")?.Trim() ?? string.Empty;
                        string dataUrl = obj.Value<string>("data_url")?.Trim() ?? throw new InvalidOperationException($"Difficulty table header '{filePath}' is missing data_url.");
                        string baseDirectory = Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException($"Could not resolve base directory for '{filePath}'.");
                        string bodyPath = resolveLocalPath(baseDirectory, dataUrl);
                        JToken bodyToken = JToken.Parse(File.ReadAllText(bodyPath));

                        if (bodyToken is not JArray bodyArray)
                            throw new InvalidOperationException($"Difficulty table body '{bodyPath}' must be a JSON array.");

                        return createParsedTableSource(fallbackName, displayName, symbol, bodyArray);
                    }

                    throw new InvalidOperationException($"Unsupported BMS difficulty table JSON format in '{filePath}'.");
                }

                default:
                    throw new InvalidOperationException($"Unsupported BMS difficulty table JSON token in '{filePath}'.");
            }
        }

        private static ParsedTableSource createParsedTableSource(string sourceName, string displayName, string symbol, JArray bodyArray)
        {
            List<ParsedTableEntry> entries = new List<ParsedTableEntry>();

            foreach (JObject? entry in bodyArray.OfType<JObject>())
            {
                string md5 = entry.Value<string>("md5")?.Trim().ToLowerInvariant() ?? string.Empty;

                if (!md5_regex.IsMatch(md5))
                    continue;

                string levelLabel = entry.Value<string>("level")?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(levelLabel))
                    continue;

                entries.Add(new ParsedTableEntry(md5, parseLevel(levelLabel), levelLabel));
            }

            if (entries.Count == 0)
                throw new InvalidOperationException($"Difficulty table '{displayName}' did not contain any valid chart entries.");

            return new ParsedTableSource(sourceName, displayName, symbol, entries);
        }

        private static string resolveLocalPath(string baseDirectory, string referencedPath)
        {
            if (referencedPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || referencedPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Online difficulty table URLs are not supported during the offline Phase 1 implementation: '{referencedPath}'.");

            if (Uri.TryCreate(referencedPath, UriKind.Absolute, out var uri) && uri.IsFile)
                return uri.LocalPath;

            return Path.GetFullPath(Path.Combine(baseDirectory, referencedPath));
        }

        private static string getSourceNameFromPath(string path)
        {
            string fileName = Path.GetFileName(path);

            if (string.IsNullOrWhiteSpace(fileName))
                fileName = new DirectoryInfo(path).Name;

            return Path.GetFileNameWithoutExtension(fileName);
        }

        private static List<PresetDefinition> loadPresets()
        {
            using var store = new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(BmsDifficultyTableManager).Assembly), "Resources");
            byte[] bytes = store.Get(presets_resource_name) ?? throw new InvalidOperationException($"Could not load '{presets_resource_name}' from BMS resources.");

            return JsonConvert.DeserializeObject<List<PresetDefinition>>(System.Text.Encoding.UTF8.GetString(bytes))
                   ?? throw new InvalidOperationException($"Could not parse '{presets_resource_name}'.");
        }

        private sealed record PresetDefinition(
            [property: JsonProperty("source_name")] string SourceName,
            [property: JsonProperty("display_name")] string DisplayName,
            [property: JsonProperty("symbol")] string Symbol);

        private readonly record struct ParsedTableEntry(string Md5, int Level, string LevelLabel);

        private sealed record ParsedTableSource(string SourceName, string DisplayName, string Symbol, IReadOnlyList<ParsedTableEntry> Entries);

        private readonly record struct EntryRow(Guid SourceId, BmsDifficultyTableEntry Entry);

        private readonly record struct SourceRow(
            Guid ID,
            string SourceName,
            string DisplayName,
            string Symbol,
            string? LocalPath,
            bool IsPreset,
            bool Enabled,
            int SortOrder,
            long ImportedAtUnixMs,
            long? LastRefreshedUnixMs);
    }
}
