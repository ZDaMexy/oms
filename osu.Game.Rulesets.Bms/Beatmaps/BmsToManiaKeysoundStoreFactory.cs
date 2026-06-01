// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Objects;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// Reflection seam (J6) used by <c>DrawableManiaRuleset</c> to host a shared <see cref="BmsKeysoundStore"/> when a
    /// BMS chart is played as mania. The converted sample-only objects (<see cref="BmsConvertedBgmSampleHitObject"/> /
    /// <see cref="BmsConvertedScratchSampleHitObject"/>) play their keysounds through this store instead of per-object
    /// one-shot samples, so playback honours pause / seek (the store stops on both) and a bounded channel pool — the
    /// same authority BMS-native gameplay uses (P1-J constraint #1). mania cannot reference the BMS assembly directly,
    /// so it discovers this factory by name and caches the returned drawable under its runtime type.
    /// </summary>
    public static class BmsToManiaKeysoundStoreFactory
    {
        /// <summary>
        /// Whether the converted beatmap carries any keysound that should route through a shared store.
        /// </summary>
        public static bool ShouldHost(IBeatmap beatmap)
            => beatmap.HitObjects.Any(hitObject => hitObject is BmsConvertedBgmSampleHitObject or BmsConvertedScratchSampleHitObject);

        /// <summary>
        /// Creates the shared keysound store drawable. Returned as <see cref="Drawable"/> so the mania side does not
        /// need a compile-time reference to <see cref="BmsKeysoundStore"/>; it is cached under its runtime type.
        /// </summary>
        public static Drawable Create() => new BmsKeysoundStore();
    }
}
