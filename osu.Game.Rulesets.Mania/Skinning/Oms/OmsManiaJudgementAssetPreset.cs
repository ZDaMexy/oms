// Copyright (c) OMS contributors. Licensed under the MIT Licence.

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    internal sealed class OmsManiaJudgementAssetPreset
    {
        public static readonly OmsManiaJudgementAssetPreset Shared = new OmsManiaJudgementAssetPreset(
            hit300gImage: "mania-hit300g",
            hit300Image: "mania-hit300",
            hit200Image: "mania-hit200",
            hit100Image: "mania-hit100",
            hit50Image: "mania-hit50",
            hit0Image: "mania-hit0"
        );

        public string Hit300gImage { get; }

        public string Hit300Image { get; }

        public string Hit200Image { get; }

        public string Hit100Image { get; }

        public string Hit50Image { get; }

        public string Hit0Image { get; }

        private OmsManiaJudgementAssetPreset(string hit300gImage, string hit300Image, string hit200Image, string hit100Image, string hit50Image, string hit0Image)
        {
            Hit300gImage = hit300gImage;
            Hit300Image = hit300Image;
            Hit200Image = hit200Image;
            Hit100Image = hit100Image;
            Hit50Image = hit50Image;
            Hit0Image = hit0Image;
        }
    }
}
