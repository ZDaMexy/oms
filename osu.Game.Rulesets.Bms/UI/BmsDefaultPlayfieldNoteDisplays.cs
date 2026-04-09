// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;

namespace osu.Game.Rulesets.Bms.UI
{
    internal abstract partial class DefaultBmsNoteDisplayBase : Box
    {
        public bool IsScratch { get; }

        protected DefaultBmsNoteDisplayBase(bool isScratch)
        {
            IsScratch = isScratch;
            RelativeSizeAxes = Axes.Both;
        }
    }

    internal sealed partial class DefaultBmsNoteDisplay : DefaultBmsNoteDisplayBase
    {
        public DefaultBmsNoteDisplay(bool isScratch)
            : base(isScratch)
        {
            Colour = BmsDefaultPlayfieldPalette.GetNote(isScratch);
        }
    }

    internal sealed partial class DefaultBmsLongNoteHeadDisplay : DefaultBmsNoteDisplayBase
    {
        public DefaultBmsLongNoteHeadDisplay(bool isScratch)
            : base(isScratch)
        {
            Colour = BmsDefaultPlayfieldPalette.GetLongNoteHead(isScratch);
        }
    }

    internal sealed partial class DefaultBmsLongNoteBodyDisplay : DefaultBmsNoteDisplayBase
    {
        public DefaultBmsLongNoteBodyDisplay(bool isScratch)
            : base(isScratch)
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            Width = 0.42f;
            Alpha = 0.8f;
            Colour = BmsDefaultPlayfieldPalette.GetLongNoteBody(isScratch);
        }
    }

    internal sealed partial class DefaultBmsLongNoteTailDisplay : DefaultBmsNoteDisplayBase
    {
        public DefaultBmsLongNoteTailDisplay(bool isScratch)
            : base(isScratch)
        {
            Colour = BmsDefaultPlayfieldPalette.GetLongNoteTail(isScratch);
        }
    }
}
