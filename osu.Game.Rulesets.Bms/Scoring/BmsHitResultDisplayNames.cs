// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Diagnostics.CodeAnalysis;
using osu.Framework.Extensions;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Scoring
{
    internal static class BmsHitResultDisplayNames
    {
        public static string GetDisplayName(HitResult result)
            => TryGetCustomDisplayName(result, out string? displayName) ? displayName : result.GetDescription().ToUpperInvariant();

        public static bool TryGetCustomDisplayName(HitResult result, [NotNullWhen(true)] out string? displayName)
        {
            displayName = result switch
            {
                HitResult.Perfect => "PGREAT",
                HitResult.Meh => "BAD",
                HitResult.Miss => "POOR",
                HitResult.Ok => "EPOOR",
                HitResult.ComboBreak => "COMBO BREAK",
                _ => null,
            };

            return displayName != null;
        }
    }
}
