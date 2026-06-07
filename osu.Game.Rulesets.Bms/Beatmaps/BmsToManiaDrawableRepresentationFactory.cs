// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    public static class BmsToManiaDrawableRepresentationFactory
    {
        public static bool CanCreate(ManiaHitObject hitObject) => hitObject is BmsConvertedScratchSampleHitObject or BmsConvertedBgmSampleHitObject or BmsConvertedKeyNoteHitObject;

        public static DrawableHitObject<ManiaHitObject>? Create(ManiaHitObject hitObject)
        {
            switch (hitObject)
            {
                case BmsConvertedScratchSampleHitObject scratchSampleHitObject:
                    return new DrawableBmsConvertedScratchSampleHitObject(scratchSampleHitObject);

                case BmsConvertedBgmSampleHitObject bgmSampleHitObject:
                    return new DrawableBmsConvertedBgmSampleHitObject(bgmSampleHitObject);

                // Playable KEY note routed through the shared store for per-WAV cut (P1-J #10 / #1).
                case BmsConvertedKeyNoteHitObject keyNoteHitObject:
                    return new DrawableBmsConvertedKeyNote(keyNoteHitObject);

                default:
                    return null;
            }
        }
    }
}
