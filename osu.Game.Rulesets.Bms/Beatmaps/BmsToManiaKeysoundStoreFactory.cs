// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Configuration;
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
        /// Whether the converted beatmap carries any keysound that should route through a shared store. Includes
        /// playable KEY notes (<see cref="BmsConvertedKeyNoteHitObject"/>) so the store is hosted for every converted
        /// chart and their keysounds get per-WAV cut — not only charts that happen to carry BGM / scratch samples.
        /// </summary>
        public static bool ShouldHost(IBeatmap beatmap)
            => beatmap.HitObjects.Any(hitObject => hitObject is BmsConvertedBgmSampleHitObject or BmsConvertedScratchSampleHitObject or BmsConvertedKeyNoteHitObject);

        /// <summary>
        /// Creates the shared keysound store drawable. Returned as <see cref="Drawable"/> so the mania side does not
        /// need a compile-time reference to <see cref="BmsKeysoundStore"/>; it is cached under its runtime type.
        /// </summary>
        /// <remarks>
        /// The converted store carries the BMS BGM autoplay layer (channel <c>0x01</c>) plus scratch samples — a backing
        /// track that must play out in full, not a player-facing polyphony budget. A single long BGM segment (e.g. a
        /// chart's <c>bgm1</c>, triggered once near the start and spanning tens of seconds) shares the pool with the
        /// thousands of short BGM slices on the same channel; at the 32-channel default a dense chart saturates (measured
        /// concurrent peak ~36) and <see cref="BmsKeysoundStore"/>'s rotation steal recycles the long-held BGM channel
        /// mid-playback, silencing the rest of that segment (e.g. a vocal that only enters seconds into the file). The
        /// store is therefore floored well above the autoplay-layer peak so a long BGM is never stolen; a higher
        /// user-configured <see cref="BmsRulesetSetting.KeysoundConcurrentChannels"/> still wins (the value persists from
        /// BMS-native play; the mania settings UI doesn't expose it). <paramref name="configCache"/> may be null in
        /// isolated contexts (test scenes).
        /// </remarks>
        public static Drawable Create(IRulesetConfigCache? configCache)
        {
            var store = new BmsKeysoundStore();

            int configured = configCache?.GetConfigFor(new BmsRuleset()) is BmsRulesetConfigManager config
                ? config.Get<int>(BmsRulesetSetting.KeysoundConcurrentChannels)
                : BmsKeysoundStore.DEFAULT_CONCURRENT_CHANNELS;

            store.ConcurrentChannels = Math.Max(configured, converted_bgm_layer_min_channels);

            return store;
        }

        // The BGM autoplay layer must never have a long segment stolen by pool saturation (see Create remarks). Floored
        // well above the measured dense-chart concurrent peak (~36); a higher user KeysoundConcurrentChannels still wins.
        private const int converted_bgm_layer_min_channels = 128;
    }
}
