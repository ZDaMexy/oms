// Copyright (c) OMS contributors. Licensed under the MIT Licence.

namespace osu.Game.Rulesets.Bms.UI
{
    public enum BmsGameplayAdjustmentTarget
    {
        Sudden,
        Hidden,
        Lift,
    }

    public static class BmsGameplayAdjustmentTargetExtensions
    {
        public static string GetAbbreviation(this BmsGameplayAdjustmentTarget target)
            => target switch
            {
                BmsGameplayAdjustmentTarget.Sudden => @"SUD",
                BmsGameplayAdjustmentTarget.Hidden => @"HID",
                BmsGameplayAdjustmentTarget.Lift => @"LIFT",
                _ => @"AUTO",
            };
    }
}
