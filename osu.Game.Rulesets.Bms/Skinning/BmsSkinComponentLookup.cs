// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.Skinning
{
    public class BmsSkinComponentLookup : SkinComponentLookup<BmsSkinComponents>
    {
        public BmsSkinComponentLookup(BmsSkinComponents component)
            : base(component)
        {
        }
    }

    public enum BmsSkinComponents
    {
        HudLayout,
        GaugeBar,
        ComboCounter,
        ClearLamp,
        GaugeHistoryPanel,
        GaugeHistory,
        ResultsSummaryPanel,
        ResultsSummary,
        NoteDistributionPanel,
        NoteDistribution,
        StaticBackgroundLayer,
    }
}
