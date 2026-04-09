// Copyright (c) OMS contributors. Licensed under the MIT Licence.

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    internal sealed class OmsManiaShellAssetPreset
    {
        public static readonly OmsManiaShellAssetPreset Shared = new OmsManiaShellAssetPreset(
            leftStageImage: "mania-stage-left",
            rightStageImage: "mania-stage-right",
            bottomStageImage: "mania-stage-bottom",
            hitTargetImage: "mania-stage-hint",
            lightImage: "mania-stage-light",
            keysUnderNotes: false
        );

        public string LeftStageImage { get; }

        public string RightStageImage { get; }

        public string BottomStageImage { get; }

        public string HitTargetImage { get; }

        public string LightImage { get; }

        public bool KeysUnderNotes { get; }

        private OmsManiaShellAssetPreset(string leftStageImage, string rightStageImage, string bottomStageImage, string hitTargetImage, string lightImage, bool keysUnderNotes)
        {
            LeftStageImage = leftStageImage;
            RightStageImage = rightStageImage;
            BottomStageImage = bottomStageImage;
            HitTargetImage = hitTargetImage;
            LightImage = lightImage;
            KeysUnderNotes = keysUnderNotes;
        }
    }
}
