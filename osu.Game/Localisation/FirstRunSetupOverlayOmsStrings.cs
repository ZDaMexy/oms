// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class FirstRunSetupOverlayOmsStrings
    {
        private const string prefix = @"osu.Game.Localisation.FirstRunSetupOverlay";

        /// <summary>
        /// "Welcome to the first-run setup guide!
        ///
        /// oms is a fork client based on osu!lazer. It adds direct support for the custom BMS ruleset and removes all modes except mania.
        /// This first-run setup guide helps you quickly configure a few important settings.
        /// Join our QQ group: 650530995
        /// Or find us on GitHub: ZDaMexy/oms
        /// Contact us to discuss, get a better gameplay experience, and share feedback and suggestions!
        /// welcome to oms!"
        /// </summary>
        public static LocalisableString WelcomeDescription => new TranslatableString(getKey(@"welcome_description"), "Welcome to the first-run setup guide!\r\n\r\noms is a fork client based on osu!lazer. It adds direct support for the custom BMS ruleset and removes all modes except mania.\r\nThis first-run setup guide helps you quickly configure a few important settings.\r\nJoin our QQ group: 650530995\r\nOr find us on GitHub: ZDaMexy/oms\r\nContact us to discuss, get a better gameplay experience, and share feedback and suggestions!\r\nwelcome to oms!");

        /// <summary>
        /// "难度表设置"
        /// </summary>
        public static LocalisableString DifficultyTableSetupTitle => new TranslatableString(getKey(@"difficulty_table_setup_title"), @"难度表设置");

        /// <summary>
        /// "按键绑定"
        /// </summary>
        public static LocalisableString KeyBindingSetupTitle => new TranslatableString(getKey(@"key_binding_setup_title"), @"按键绑定");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
