// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;

namespace osu.Game.Rulesets.Bms.DifficultyTable
{
    public record BmsDifficultyTableSourceInfo(
        Guid ID,
        string SourceName,
        string DisplayName,
        string Symbol,
        string? LocalPath,
        bool IsPreset,
        bool Enabled,
        int SortOrder,
        DateTimeOffset ImportedAt,
        DateTimeOffset? LastRefreshed,
        IReadOnlyList<BmsDifficultyTableEntry> Entries);
}
