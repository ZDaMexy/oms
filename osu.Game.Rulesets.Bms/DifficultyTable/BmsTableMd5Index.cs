// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;

namespace osu.Game.Rulesets.Bms.DifficultyTable
{
    public class BmsTableMd5Index : IDisposable
    {
        private readonly BmsDifficultyTableManager tableManager;

        private IReadOnlyDictionary<string, IReadOnlyList<BmsDifficultyTableEntry>> md5ToEntries =
            new Dictionary<string, IReadOnlyList<BmsDifficultyTableEntry>>(StringComparer.Ordinal);

        public event Action? IndexChanged;

        public BmsDifficultyTableManager TableManager => tableManager;

        public BmsTableMd5Index(BmsDifficultyTableManager tableManager)
        {
            this.tableManager = tableManager;

            rebuild();
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

        public void Rebuild() => rebuild();

        public void Dispose() => tableManager.TableDataChanged -= handleTableDataChanged;

        private void handleTableDataChanged() => rebuild();

        private void rebuild()
        {
            md5ToEntries = tableManager.CreateMd5Lookup();

            IndexChanged?.Invoke();
        }
    }
}
