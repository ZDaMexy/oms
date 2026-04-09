// Copyright (c) OMS contributors. Licensed under the MIT Licence.

namespace osu.Game.Rulesets.Bms.DifficultyTable
{
    public record BmsDifficultyTableEntry(
        string TableName,
        string Symbol,
        int Level,
        string LevelLabel,
        string Md5,
        int TableSortOrder = 0);
}
