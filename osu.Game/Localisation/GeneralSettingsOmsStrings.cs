// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class GeneralSettingsOmsStrings
    {
        private const string prefix = @"osu.Game.Localisation.GeneralSettings";

        /// <summary>
        /// "Open OMS folder"
        /// </summary>
        public static LocalisableString OpenOmsFolder => new TranslatableString(getKey(@"open_oms_folder"), @"Open OMS folder");

        /// <summary>
        /// "Change data directory..."
        /// </summary>
        public static LocalisableString ChangeDataDirectoryLocation => new TranslatableString(getKey(@"change_data_directory_location"), @"Change data directory...");

        /// <summary>
        /// "Quickly adjust important OMS settings."
        /// </summary>
        public static LocalisableString RunSetupWizardTooltip => new TranslatableString(getKey(@"run_setup_wizard_tooltip"), @"Quickly adjust important OMS settings.");

        /// <summary>
        /// "Learn more about OMS"
        /// </summary>
        public static LocalisableString LearnMoreAboutOms => new TranslatableString(getKey(@"learn_more_about_oms"), @"Learn more about OMS");

        /// <summary>
        /// "Visit the OMS GitHub repository and project overview."
        /// </summary>
        public static LocalisableString LearnMoreAboutOmsTooltip => new TranslatableString(getKey(@"learn_more_about_oms_tooltip"), @"Visit the OMS GitHub repository and project overview.");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
