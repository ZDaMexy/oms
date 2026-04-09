// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    public partial class OmsHoldNoteTailPiece : OmsNotePiece
    {
        protected override void OnDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            base.OnDirectionChanged(direction.NewValue == ScrollingDirection.Up
                ? new ValueChangedEvent<ScrollingDirection>(ScrollingDirection.Down, ScrollingDirection.Down)
                : new ValueChangedEvent<ScrollingDirection>(ScrollingDirection.Up, ScrollingDirection.Up));
        }

        protected override Drawable? GetAnimation(ISkinSource skin)
        {
            return GetAnimationFromLookup(skin, LegacyManiaSkinConfigurationLookups.HoldNoteTailImage)
                   ?? GetAnimationFromLookup(skin, LegacyManiaSkinConfigurationLookups.HoldNoteHeadImage)
                   ?? GetAnimationFromLookup(skin, LegacyManiaSkinConfigurationLookups.NoteImage);
        }
    }
}