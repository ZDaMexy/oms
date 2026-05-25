// Copyright (c) OMS contributors. Licensed under the MIT Licence.

#nullable disable

using osu.Game.Rulesets;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// Provides a source-bound cache for reusable modless playable beatmaps.
    /// </summary>
    public interface ICachedModlessPlayableBeatmapSource
    {
        bool TryGetCachedModlessPlayableBeatmap(IRulesetInfo ruleset, out IBeatmap beatmap);

        void CacheModlessPlayableBeatmap(IRulesetInfo ruleset, IBeatmap beatmap);
    }
}
