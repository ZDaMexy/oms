// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    internal sealed class OmsManiaHoldNoteBodyPreset
    {
        public static readonly OmsManiaHoldNoteBodyPreset Shared = new OmsManiaHoldNoteBodyPreset(
            lightImage: "lightingL",
            lightScale: 1f,
            bodyStyle: LegacyNoteBodyStyle.Stretch
        );

        public string LightImage { get; }

        public float LightScale { get; }

        public LegacyNoteBodyStyle BodyStyle { get; }

        private OmsManiaHoldNoteBodyPreset(string lightImage, float lightScale, LegacyNoteBodyStyle bodyStyle)
        {
            LightImage = lightImage;
            LightScale = lightScale;
            BodyStyle = bodyStyle;
        }
    }
}
