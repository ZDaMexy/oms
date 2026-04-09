// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Database;

namespace osu.Game.Rulesets.Bms.DifficultyTable
{
    public class BmsTableMd5Index : IDisposable
    {
        private readonly BmsDifficultyTableManager tableManager;
        private readonly RealmAccess? realmAccess;

        private IReadOnlyDictionary<string, IReadOnlyList<BmsDifficultyTableEntry>> md5ToEntries =
            new Dictionary<string, IReadOnlyList<BmsDifficultyTableEntry>>(StringComparer.Ordinal);

        public event Action? IndexChanged;

        public BmsDifficultyTableManager TableManager => tableManager;

        public BmsTableMd5Index(BmsDifficultyTableManager tableManager, RealmAccess? realmAccess = null)
        {
            this.tableManager = tableManager;
            this.realmAccess = realmAccess;

            rebuild(updatePersistedBeatmaps: false);
            tableManager.TableDataChanged += handleTableDataChanged;
        }

        public IReadOnlyList<BmsDifficultyTableEntry> GetEntries(string? md5)
        {
            if (string.IsNullOrWhiteSpace(md5))
                return Array.Empty<BmsDifficultyTableEntry>();

            return md5ToEntries.TryGetValue(md5.Trim().ToLowerInvariant(), out var entries)
                ? entries
                : Array.Empty<BmsDifficultyTableEntry>();
        }

        public IReadOnlyList<BmsDifficultyTableEntry> GetEntries(IBeatmapInfo beatmapInfo)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);
            return GetEntries(beatmapInfo.MD5Hash);
        }

        public bool ApplyTo(BeatmapInfo beatmapInfo)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (beatmapInfo.Ruleset.ShortName != BmsRuleset.SHORT_NAME)
                return false;

            return beatmapInfo.Metadata.SetDifficultyTableEntries(GetEntries(beatmapInfo));
        }

        public int ApplyTo(IEnumerable<BeatmapInfo> beatmaps)
        {
            ArgumentNullException.ThrowIfNull(beatmaps);

            int updatedCount = 0;

            foreach (var beatmap in beatmaps)
            {
                if (ApplyTo(beatmap))
                    updatedCount++;
            }

            return updatedCount;
        }

        public void Rebuild(bool updatePersistedBeatmaps = true) => rebuild(updatePersistedBeatmaps);

        public void Dispose() => tableManager.TableDataChanged -= handleTableDataChanged;

        private void handleTableDataChanged() => rebuild(updatePersistedBeatmaps: true);

        private void rebuild(bool updatePersistedBeatmaps)
        {
            md5ToEntries = tableManager.GetAllEntries()
                                     .GroupBy(entry => entry.Md5, StringComparer.Ordinal)
                                     .ToDictionary(
                                         group => group.Key,
                                         group => (IReadOnlyList<BmsDifficultyTableEntry>)group.ToArray(),
                                         StringComparer.Ordinal);

            if (updatePersistedBeatmaps && realmAccess != null)
                updatePersistedBeatmapsInRealm();

            IndexChanged?.Invoke();
        }

        private void updatePersistedBeatmapsInRealm()
        {
            if (realmAccess == null)
                return;

            realmAccess.Write(realm =>
            {
                var beatmaps = realm.All<BeatmapInfo>()
                                   .AsEnumerable()
                                   .Where(beatmap => beatmap.Ruleset.ShortName == BmsRuleset.SHORT_NAME);

                ApplyTo(beatmaps);
            });
        }
    }
}
