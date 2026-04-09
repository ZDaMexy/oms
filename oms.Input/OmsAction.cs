// Copyright (c) OMS contributors. Licensed under the MIT Licence.

namespace oms.Input
{
    /// <summary>
    /// All input hardware maps to this enum.
    /// The game layer never reads hardware signals directly.
    /// </summary>
    public enum OmsAction
    {
        // BMS 7K+1 (1P)
        Key1P_Scratch,
        Key1P_1,
        Key1P_2,
        Key1P_3,
        Key1P_4,
        Key1P_5,
        Key1P_6,
        Key1P_7,

        // BMS 7K+1 (2P)
        Key2P_Scratch,
        Key2P_1,
        Key2P_2,
        Key2P_3,
        Key2P_4,
        Key2P_5,
        Key2P_6,
        Key2P_7,

        // 9K
        Key9K_1,
        Key9K_2,
        Key9K_3,
        Key9K_4,
        Key9K_5,
        Key9K_6,
        Key9K_7,
        Key9K_8,
        Key9K_9,

        // UI / System
        UI_Confirm,
        UI_Back,
        UI_ModMenu,
        UI_LaneCoverAdjust,
        UI_LaneCoverFocus,
    }

    public static class OmsActionExtensions
    {
        public static bool IsGameplayAction(this OmsAction action)
            => action >= OmsAction.Key1P_Scratch && action <= OmsAction.Key9K_9;
    }
}
