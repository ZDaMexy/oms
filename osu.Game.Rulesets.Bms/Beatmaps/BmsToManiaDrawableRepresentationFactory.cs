// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    public static class BmsToManiaDrawableRepresentationFactory
    {
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
