// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    public static class BmsToManiaDrawableRepresentationFactory
    {
        // Only the sample-only objects (BGM / scratch) get a bespoke non-pooled drawable. Playable KEY notes
        // (BmsConvertedKeyNoteHitObject) are intentionally NOT claimed: returning a drawable for them would force the
        // non-pooled CreateDrawableRepresentation path. Leaving them unclaimed makes DrawableManiaRuleset return null, so
        // they are served by mania's pooled Note pool (base-type pool fallback) and their keysound is rerouted through
        // the shared store via IHasManiaKeysound / IManiaKeysoundStore in DrawableNote.PlaySamples (J6 / P1-J #10).
        public static bool CanCreate(ManiaHitObject hitObject) => hitObject is BmsConvertedScratchSampleHitObject or BmsConvertedBgmSampleHitObject;

        public static DrawableHitObject<ManiaHitObject>? Create(ManiaHitObject hitObject)
        {
            switch (hitObject)
            {
                case BmsConvertedScratchSampleHitObject scratchSampleHitObject:
                    return new DrawableBmsConvertedScratchSampleHitObject(scratchSampleHitObject);

                case BmsConvertedBgmSampleHitObject bgmSampleHitObject:
                    return new DrawableBmsConvertedBgmSampleHitObject(bgmSampleHitObject);

                default:
                    return null;
            }
        }
    }
}
