// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Bms.Difficulty;

namespace osu.Game.Rulesets.Bms.UI
{
    internal sealed partial class DefaultBmsPlayfieldBackdropDisplay : Box
    {
        public DefaultBmsPlayfieldBackdropDisplay()
        {
            RelativeSizeAxes = Axes.Both;
            Colour = BmsDefaultPlayfieldPalette.PlayfieldBackdrop;
        }
    }

    internal sealed partial class DefaultBmsPlayfieldBaseplateDisplay : Box
    {
        public DefaultBmsPlayfieldBaseplateDisplay()
        {
            RelativeSizeAxes = Axes.Both;
            Colour = BmsDefaultPlayfieldPalette.PlayfieldBaseplate;
        }
    }

    internal sealed partial class DefaultBmsLaneBackgroundDisplay : Box
    {
        public int LaneIndex { get; }

        public bool IsScratch { get; }

        public DefaultBmsLaneBackgroundDisplay(int laneIndex, bool isScratch, BmsKeymode keymode = BmsKeymode.Key7K)
        {
            LaneIndex = laneIndex;
            IsScratch = isScratch;
            RelativeSizeAxes = Axes.Both;
            Colour = BmsDefaultPlayfieldPalette.GetLaneBackground(laneIndex, isScratch, keymode);
        }
    }

    internal sealed partial class DefaultBmsLaneDividerDisplay : Box
    {
        public bool IsScratch { get; }

        public DefaultBmsLaneDividerDisplay(bool isScratch)
        {
            IsScratch = isScratch;
            Anchor = Anchor.CentreRight;
            Origin = Anchor.CentreRight;
            RelativeSizeAxes = Axes.Y;
            Width = 1;
            Colour = BmsDefaultPlayfieldPalette.GetLaneDivider(isScratch);
        }
    }
}
