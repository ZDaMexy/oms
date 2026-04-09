// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Graphics;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    public partial class OmsHoldNoteHeadPiece : OmsNotePiece
    {
        protected override Drawable? GetAnimation(ISkinSource skin)
        {
            return GetAnimationFromLookup(skin, LegacyManiaSkinConfigurationLookups.HoldNoteHeadImage)
                   ?? GetAnimationFromLookup(skin, LegacyManiaSkinConfigurationLookups.NoteImage);
        }
    }
}