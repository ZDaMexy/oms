// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Bms.Difficulty;

namespace osu.Game.Rulesets.Bms.UI
{
    internal abstract partial class DefaultBmsNoteDisplayBase : Box
    {
        public int LaneIndex { get; }

        public bool IsScratch { get; }

        public BmsKeymode Keymode { get; }

        protected DefaultBmsNoteDisplayBase(int laneIndex, bool isScratch, BmsKeymode keymode)
        {
            LaneIndex = laneIndex;
            IsScratch = isScratch;
            Keymode = keymode;
            RelativeSizeAxes = Axes.Both;
        }
    }

    internal sealed partial class DefaultBmsNoteDisplay : DefaultBmsNoteDisplayBase
    {
        public DefaultBmsNoteDisplay(int laneIndex, bool isScratch, BmsKeymode keymode)
            : base(laneIndex, isScratch, keymode)
        {
            Colour = BmsDefaultPlayfieldPalette.GetNote(laneIndex, isScratch, keymode);
        }
    }

    internal sealed partial class DefaultBmsLongNoteHeadDisplay : DefaultBmsNoteDisplayBase
    {
        public DefaultBmsLongNoteHeadDisplay(int laneIndex, bool isScratch, BmsKeymode keymode)
            : base(laneIndex, isScratch, keymode)
        {
            Colour = BmsDefaultPlayfieldPalette.GetLongNoteHead(laneIndex, isScratch, keymode);
        }
    }

    internal sealed partial class DefaultBmsLongNoteBodyDisplay : DefaultBmsNoteDisplayBase
    {
        public DefaultBmsLongNoteBodyDisplay(int laneIndex, bool isScratch, BmsKeymode keymode)
            : base(laneIndex, isScratch, keymode)
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            // Long-note body width: 0.525 = 0.42 + 25% (relative to the lane width).
            Width = 0.525f;
            Alpha = 0.8f;
            Colour = BmsDefaultPlayfieldPalette.GetLongNoteBody(laneIndex, isScratch, keymode);
        }
    }

    internal sealed partial class DefaultBmsLongNoteTailDisplay : DefaultBmsNoteDisplayBase
    {
        public DefaultBmsLongNoteTailDisplay(int laneIndex, bool isScratch, BmsKeymode keymode)
            : base(laneIndex, isScratch, keymode)
        {
            Colour = BmsDefaultPlayfieldPalette.GetLongNoteTail(laneIndex, isScratch, keymode);

            // No distinct tail end-cap marker: the long-note body already spans the full hold up to the
            // release end, so the default tail renders nothing (tail-less long-note look). Tail judgement
            // is unaffected — this is the visual element only.
            Alpha = 0;
        }
    }
}
