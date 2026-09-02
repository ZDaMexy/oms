// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Bms.Skinning;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// Explicit renderer result for a committed optional-slot Suppress entry.
    /// </summary>
    /// <remarks>
    /// This is deliberately a typed, non-null marker. A null drawable means that a legacy provider did not resolve a
    /// component and is therefore eligible for compatibility fallback; it must never encode the public C4 Suppress state.
    /// </remarks>
    internal sealed partial class BmsSuppressedNoteDrawable : CompositeDrawable
    {
        public BmsNoteSkinElements Element { get; }

        public BmsSuppressedNoteDrawable(BmsNoteSkinElements element)
        {
            if (element != BmsNoteSkinElements.LongNoteTail)
                throw new ArgumentOutOfRangeException(nameof(element), element, "Only the optional BMS long-note tail slot may be suppressed.");

            Element = element;
            RelativeSizeAxes = Axes.Both;
        }
    }

    /// <summary>
    /// Transparent first-load placeholder while a framework drawable is built from an already committed material payload.
    /// </summary>
    internal sealed partial class BmsPublishedNotePendingDrawable : CompositeDrawable
    {
        public BmsPublishedNotePendingDrawable()
        {
            RelativeSizeAxes = Axes.Both;
        }
    }
}
