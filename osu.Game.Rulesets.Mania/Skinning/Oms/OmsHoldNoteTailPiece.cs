// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Graphics;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    public partial class OmsHoldNoteTailPiece : OmsNotePiece
    {
        protected override ScrollingDirection GetDisplayDirection(ScrollingDirection direction) => direction == ScrollingDirection.Up
            ? ScrollingDirection.Down
            : ScrollingDirection.Up;

        protected override Drawable? GetAnimation(ISkinSource skin)
        {
            return GetAnimationFromLookup(skin, LegacyManiaSkinConfigurationLookups.HoldNoteTailImage)
                   ?? GetAnimationFromLookup(skin, LegacyManiaSkinConfigurationLookups.HoldNoteHeadImage)
                   ?? GetAnimationFromLookup(skin, LegacyManiaSkinConfigurationLookups.NoteImage);
        }
    }
}
