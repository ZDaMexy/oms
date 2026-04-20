// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using oms.Input;

namespace osu.Game.Rulesets.Bms.Input
{
    public static class OmsBmsActionMap
    {
        public static int NormalizeVariant(int variant)
            => variant is 6 or 8 or 9 or 16 ? variant : 8;

        public static bool TryMapToBmsAction(int variant, OmsAction omsAction, out BmsAction bmsAction)
        {
            variant = NormalizeVariant(variant);

            switch (variant)
            {
                case 6:
                    return tryMap5K(omsAction, out bmsAction);

                case 8:
                    return tryMap7K(omsAction, out bmsAction);

                case 9:
                    return tryMap9K(omsAction, out bmsAction);

                case 16:
                    return tryMap14K(omsAction, out bmsAction);
            }

            bmsAction = default;
            return false;
        }

        public static bool TryMapToOmsAction(int variant, BmsAction bmsAction, out OmsAction omsAction)
        {
            variant = NormalizeVariant(variant);

            if (bmsAction == BmsAction.LaneCoverFocus)
            {
                omsAction = OmsAction.UI_LaneCoverFocus;
                return true;
            }

            if (bmsAction == BmsAction.PreStartHold)
            {
                omsAction = OmsAction.UI_PreStartHold;
                return true;
            }

            switch (variant)
            {
                case 6:
                    return tryMapFrom5K(bmsAction, out omsAction);

                case 8:
                    return tryMapFrom7K(bmsAction, out omsAction);

                case 9:
                    return tryMapFrom9K(bmsAction, out omsAction);

                case 16:
                    return tryMapFrom14K(bmsAction, out omsAction);
            }

            omsAction = default;
            return false;
        }

        private static bool tryMap5K(OmsAction omsAction, out BmsAction bmsAction)
        {
            switch (omsAction)
            {
                case OmsAction.Key1P_Scratch:
                    bmsAction = BmsAction.Scratch1;
                    return true;

                case OmsAction.Key1P_1:
                    bmsAction = BmsAction.Key1;
                    return true;

                case OmsAction.Key1P_2:
                    bmsAction = BmsAction.Key2;
                    return true;

                case OmsAction.Key1P_3:
                    bmsAction = BmsAction.Key3;
                    return true;

                case OmsAction.Key1P_4:
                    bmsAction = BmsAction.Key4;
                    return true;

                case OmsAction.Key1P_5:
                    bmsAction = BmsAction.Key5;
                    return true;

                case OmsAction.UI_LaneCoverFocus:
                    bmsAction = BmsAction.LaneCoverFocus;
                    return true;

                case OmsAction.UI_PreStartHold:
                    bmsAction = BmsAction.PreStartHold;
                    return true;

                default:
                    bmsAction = default;
                    return false;
            }
        }

        private static bool tryMap7K(OmsAction omsAction, out BmsAction bmsAction)
        {
            switch (omsAction)
            {
                case OmsAction.Key1P_Scratch:
                    bmsAction = BmsAction.Scratch1;
                    return true;

                case OmsAction.Key1P_1:
                    bmsAction = BmsAction.Key1;
                    return true;

                case OmsAction.Key1P_2:
                    bmsAction = BmsAction.Key2;
                    return true;

                case OmsAction.Key1P_3:
                    bmsAction = BmsAction.Key3;
                    return true;

                case OmsAction.Key1P_4:
                    bmsAction = BmsAction.Key4;
                    return true;

                case OmsAction.Key1P_5:
                    bmsAction = BmsAction.Key5;
                    return true;

                case OmsAction.Key1P_6:
                    bmsAction = BmsAction.Key6;
                    return true;

                case OmsAction.Key1P_7:
                    bmsAction = BmsAction.Key7;
                    return true;

                case OmsAction.UI_LaneCoverFocus:
                    bmsAction = BmsAction.LaneCoverFocus;
                    return true;

                case OmsAction.UI_PreStartHold:
                    bmsAction = BmsAction.PreStartHold;
                    return true;

                default:
                    bmsAction = default;
                    return false;
            }
        }

        private static bool tryMap9K(OmsAction omsAction, out BmsAction bmsAction)
        {
            switch (omsAction)
            {
                case OmsAction.Key9K_1:
                    bmsAction = BmsAction.Key1;
                    return true;

                case OmsAction.Key9K_2:
                    bmsAction = BmsAction.Key2;
                    return true;

                case OmsAction.Key9K_3:
                    bmsAction = BmsAction.Key3;
                    return true;

                case OmsAction.Key9K_4:
                    bmsAction = BmsAction.Key4;
                    return true;

                case OmsAction.Key9K_5:
                    bmsAction = BmsAction.Key5;
                    return true;

                case OmsAction.Key9K_6:
                    bmsAction = BmsAction.Key6;
                    return true;

                case OmsAction.Key9K_7:
                    bmsAction = BmsAction.Key7;
                    return true;

                case OmsAction.Key9K_8:
                    bmsAction = BmsAction.Key8;
                    return true;

                case OmsAction.Key9K_9:
                    bmsAction = BmsAction.Key9;
                    return true;

                case OmsAction.UI_LaneCoverFocus:
                    bmsAction = BmsAction.LaneCoverFocus;
                    return true;

                case OmsAction.UI_PreStartHold:
                    bmsAction = BmsAction.PreStartHold;
                    return true;

                default:
                    bmsAction = default;
                    return false;
            }
        }

        private static bool tryMap14K(OmsAction omsAction, out BmsAction bmsAction)
        {
            switch (omsAction)
            {
                case OmsAction.Key1P_Scratch:
                    bmsAction = BmsAction.Scratch1;
                    return true;

                case OmsAction.Key1P_1:
                    bmsAction = BmsAction.Key1;
                    return true;

                case OmsAction.Key1P_2:
                    bmsAction = BmsAction.Key2;
                    return true;

                case OmsAction.Key1P_3:
                    bmsAction = BmsAction.Key3;
                    return true;

                case OmsAction.Key1P_4:
                    bmsAction = BmsAction.Key4;
                    return true;

                case OmsAction.Key1P_5:
                    bmsAction = BmsAction.Key5;
                    return true;

                case OmsAction.Key1P_6:
                    bmsAction = BmsAction.Key6;
                    return true;

                case OmsAction.Key1P_7:
                    bmsAction = BmsAction.Key7;
                    return true;

                case OmsAction.Key2P_Scratch:
                    bmsAction = BmsAction.Scratch2;
                    return true;

                case OmsAction.Key2P_1:
                    bmsAction = BmsAction.Key8;
                    return true;

                case OmsAction.Key2P_2:
                    bmsAction = BmsAction.Key9;
                    return true;

                case OmsAction.Key2P_3:
                    bmsAction = BmsAction.Key10;
                    return true;

                case OmsAction.Key2P_4:
                    bmsAction = BmsAction.Key11;
                    return true;

                case OmsAction.Key2P_5:
                    bmsAction = BmsAction.Key12;
                    return true;

                case OmsAction.Key2P_6:
                    bmsAction = BmsAction.Key13;
                    return true;

                case OmsAction.Key2P_7:
                    bmsAction = BmsAction.Key14;
                    return true;

                case OmsAction.UI_LaneCoverFocus:
                    bmsAction = BmsAction.LaneCoverFocus;
                    return true;

                case OmsAction.UI_PreStartHold:
                    bmsAction = BmsAction.PreStartHold;
                    return true;

                default:
                    bmsAction = default;
                    return false;
            }
        }

        private static bool tryMapFrom5K(BmsAction bmsAction, out OmsAction omsAction)
        {
            switch (bmsAction)
            {
                case BmsAction.Scratch1:
                    omsAction = OmsAction.Key1P_Scratch;
                    return true;

                case BmsAction.Key1:
                    omsAction = OmsAction.Key1P_1;
                    return true;

                case BmsAction.Key2:
                    omsAction = OmsAction.Key1P_2;
                    return true;

                case BmsAction.Key3:
                    omsAction = OmsAction.Key1P_3;
                    return true;

                case BmsAction.Key4:
                    omsAction = OmsAction.Key1P_4;
                    return true;

                case BmsAction.Key5:
                    omsAction = OmsAction.Key1P_5;
                    return true;

                default:
                    omsAction = default;
                    return false;
            }
        }

        private static bool tryMapFrom7K(BmsAction bmsAction, out OmsAction omsAction)
        {
            switch (bmsAction)
            {
                case BmsAction.Scratch1:
                    omsAction = OmsAction.Key1P_Scratch;
                    return true;

                case BmsAction.Key1:
                    omsAction = OmsAction.Key1P_1;
                    return true;

                case BmsAction.Key2:
                    omsAction = OmsAction.Key1P_2;
                    return true;

                case BmsAction.Key3:
                    omsAction = OmsAction.Key1P_3;
                    return true;

                case BmsAction.Key4:
                    omsAction = OmsAction.Key1P_4;
                    return true;

                case BmsAction.Key5:
                    omsAction = OmsAction.Key1P_5;
                    return true;

                case BmsAction.Key6:
                    omsAction = OmsAction.Key1P_6;
                    return true;

                case BmsAction.Key7:
                    omsAction = OmsAction.Key1P_7;
                    return true;

                default:
                    omsAction = default;
                    return false;
            }
        }

        private static bool tryMapFrom9K(BmsAction bmsAction, out OmsAction omsAction)
        {
            switch (bmsAction)
            {
                case BmsAction.Key1:
                    omsAction = OmsAction.Key9K_1;
                    return true;

                case BmsAction.Key2:
                    omsAction = OmsAction.Key9K_2;
                    return true;

                case BmsAction.Key3:
                    omsAction = OmsAction.Key9K_3;
                    return true;

                case BmsAction.Key4:
                    omsAction = OmsAction.Key9K_4;
                    return true;

                case BmsAction.Key5:
                    omsAction = OmsAction.Key9K_5;
                    return true;

                case BmsAction.Key6:
                    omsAction = OmsAction.Key9K_6;
                    return true;

                case BmsAction.Key7:
                    omsAction = OmsAction.Key9K_7;
                    return true;

                case BmsAction.Key8:
                    omsAction = OmsAction.Key9K_8;
                    return true;

                case BmsAction.Key9:
                    omsAction = OmsAction.Key9K_9;
                    return true;

                default:
                    omsAction = default;
                    return false;
            }
        }

        private static bool tryMapFrom14K(BmsAction bmsAction, out OmsAction omsAction)
        {
            switch (bmsAction)
            {
                case BmsAction.Scratch1:
                    omsAction = OmsAction.Key1P_Scratch;
                    return true;

                case BmsAction.Key1:
                    omsAction = OmsAction.Key1P_1;
                    return true;

                case BmsAction.Key2:
                    omsAction = OmsAction.Key1P_2;
                    return true;

                case BmsAction.Key3:
                    omsAction = OmsAction.Key1P_3;
                    return true;

                case BmsAction.Key4:
                    omsAction = OmsAction.Key1P_4;
                    return true;

                case BmsAction.Key5:
                    omsAction = OmsAction.Key1P_5;
                    return true;

                case BmsAction.Key6:
                    omsAction = OmsAction.Key1P_6;
                    return true;

                case BmsAction.Key7:
                    omsAction = OmsAction.Key1P_7;
                    return true;

                case BmsAction.Scratch2:
                    omsAction = OmsAction.Key2P_Scratch;
                    return true;

                case BmsAction.Key8:
                    omsAction = OmsAction.Key2P_1;
                    return true;

                case BmsAction.Key9:
                    omsAction = OmsAction.Key2P_2;
                    return true;

                case BmsAction.Key10:
                    omsAction = OmsAction.Key2P_3;
                    return true;

                case BmsAction.Key11:
                    omsAction = OmsAction.Key2P_4;
                    return true;

                case BmsAction.Key12:
                    omsAction = OmsAction.Key2P_5;
                    return true;

                case BmsAction.Key13:
                    omsAction = OmsAction.Key2P_6;
                    return true;

                case BmsAction.Key14:
                    omsAction = OmsAction.Key2P_7;
                    return true;

                default:
                    omsAction = default;
                    return false;
            }
        }
    }
}
