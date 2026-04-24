// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.ComponentModel;
using osu.Game.Rulesets.Bms.Difficulty;

namespace osu.Game.Rulesets.Bms.UI
{
    public enum BmsPlayfieldStyle
    {
        [Description(@"1P（居左）")]
        P1,

        [Description(@"2P（居右）")]
        P2,

        [Description(@"居中（左皿）")]
        Center,

        [Description(@"居中（右皿）")]
        CenterRightScratch,
    }

    public static class BmsPlayfieldStyleExtensions
    {
        public static BmsPlayfieldStyle GetAppliedStyle(this BmsPlayfieldStyle style, BmsKeymode keymode)
            => keymode switch
            {
                BmsKeymode.Key5K or BmsKeymode.Key7K => style,
                _ => BmsPlayfieldStyle.Center,
            };

        public static bool UsesScratchVisualRight(this BmsPlayfieldStyle style)
            => style is BmsPlayfieldStyle.P2 or BmsPlayfieldStyle.CenterRightScratch;
    }
}
