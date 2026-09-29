// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mania.UI;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    public static class BmsToManiaDrawableRepresentationFactory
    {
        // Called through the existing mania-to-BMS reflection seam. Sample-only objects retain their ordinary
        // scrolling lifetime and Ignore judgement, but only the live window needs a drawable. Playable notes/holds
        // continue to use mania's normal base-type pools; all sound remains owned by the shared keysound store.
        public static void RegisterPools(Column column)
        {
            column.RegisterPool<BmsConvertedScratchSampleHitObject, DrawableBmsConvertedScratchSampleHitObject>(0);
            column.RegisterPool<BmsConvertedBgmSampleHitObject, DrawableBmsConvertedBgmSampleHitObject>(0);
        }
    }
}
