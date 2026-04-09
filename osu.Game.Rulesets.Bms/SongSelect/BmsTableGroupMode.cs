// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.DifficultyTable;
using osu.Game.Screens.Select;

namespace osu.Game.Rulesets.Bms.SongSelect
{
    public static class BmsTableGroupMode
    {
        private const string unrated_group_title = "Unrated";

        public static IEnumerable<GroupDefinition> GetGroupDefinitions(IBeatmapInfo beatmapInfo)
        {
            var entries = beatmapInfo.Metadata.GetDifficultyTableEntries();

            if (entries.Count == 0)
                return new[] { new GroupDefinition(int.MaxValue, unrated_group_title) };

            return entries.GroupBy(entry => (entry.TableSortOrder, entry.TableName, entry.Level, entry.LevelLabel))
                         .Select(group => group.First())
                         .Select(entry =>
                         {
                             var tableGroup = new GroupDefinition(entry.TableSortOrder, entry.TableName);
                             return new GroupDefinition(entry.Level, entry.LevelLabel, tableGroup);
                         })
                         .ToArray();
        }
    }
}
