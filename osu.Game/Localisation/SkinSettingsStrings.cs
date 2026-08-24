// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class SkinSettingsStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.SkinSettings";

        /// <summary>
        /// "Skin"
        /// </summary>
        public static LocalisableString SkinSectionHeader => new TranslatableString(getKey(@"skin_section_header"), @"Skin");

        /// <summary>
        /// "Current skin"
        /// </summary>
        public static LocalisableString CurrentSkin => new TranslatableString(getKey(@"current_skin"), @"Current skin");

        /// <summary>
        /// "Skin name"
        /// </summary>
        public static LocalisableString SkinName => new TranslatableString(getKey(@"skin_name"), @"Skin name");

        /// <summary>
        /// "Skin layout editor"
        /// </summary>
        public static LocalisableString SkinLayoutEditor => new TranslatableString(getKey(@"skin_layout_editor"), @"Skin layout editor");

        /// <summary>
        /// "Skin authoring is unavailable until edited skins can be activated through the protected revision protocol."
        /// </summary>
        public static LocalisableString SkinAuthoringUnavailable => new TranslatableString(
            getKey(@"skin_authoring_unavailable"),
            @"Skin authoring is unavailable until edited skins can be activated through the protected revision protocol.");

        /// <summary>
        /// "Folder skin workspace"
        /// </summary>
        public static LocalisableString FolderSkinWorkspaceHeader => new TranslatableString(getKey(@"folder_skin_workspace_header"), @"Folder skin workspace");

        /// <summary>
        /// "Manage registered external skin folders and OMS-managed folder skins. Packaged .osk skins remain in the current skin picker."
        /// </summary>
        public static LocalisableString FolderSkinWorkspaceDescription => new TranslatableString(getKey(@"folder_skin_workspace_description"),
            @"Manage registered external skin folders and OMS-managed folder skins. Packaged .osk skins remain in the current skin picker.");

        /// <summary>
        /// "Register external folder"
        /// </summary>
        public static LocalisableString RegisterExternalFolder => new TranslatableString(getKey(@"register_external_folder"), @"Register external folder");

        /// <summary>
        /// "Select an external skin folder"
        /// </summary>
        public static LocalisableString SelectExternalSkinFolder => new TranslatableString(getKey(@"select_external_skin_folder"), @"Select an external skin folder");

        /// <summary>
        /// "Choose the folder itself. OMS registers it in place and does not copy or modify its contents."
        /// </summary>
        public static LocalisableString SelectExternalSkinFolderDescription => new TranslatableString(getKey(@"select_external_skin_folder_description"),
            @"Choose the folder itself. OMS registers it in place and does not copy or modify its contents.");

        /// <summary>
        /// "No registered folder skins."
        /// </summary>
        public static LocalisableString NoFolderSkins => new TranslatableString(getKey(@"no_folder_skins"), @"No registered folder skins.");

        /// <summary>
        /// "External folder"
        /// </summary>
        public static LocalisableString ExternalFolder => new TranslatableString(getKey(@"external_folder"), @"External folder");

        /// <summary>
        /// "Managed folder"
        /// </summary>
        public static LocalisableString ManagedFolder => new TranslatableString(getKey(@"managed_folder"), @"Managed folder");

        /// <summary>
        /// "Open folder"
        /// </summary>
        public static LocalisableString OpenFolder => new TranslatableString(getKey(@"open_folder"), @"Open folder");

        /// <summary>
        /// "Import managed copy"
        /// </summary>
        public static LocalisableString ImportManagedCopy => new TranslatableString(getKey(@"import_managed_copy"), @"Import managed copy");

        /// <summary>
        /// "Unregister"
        /// </summary>
        public static LocalisableString UnregisterFolder => new TranslatableString(getKey(@"unregister_folder"), @"Unregister");

        /// <summary>
        /// "Rename folder"
        /// </summary>
        public static LocalisableString RenameFolder => new TranslatableString(getKey(@"rename_folder"), @"Rename folder");

        /// <summary>
        /// "Folder name"
        /// </summary>
        public static LocalisableString FolderName => new TranslatableString(getKey(@"folder_name"), @"Folder name");

        /// <summary>
        /// "Unregister external folder?"
        /// </summary>
        public static LocalisableString UnregisterExternalFolderHeader => new TranslatableString(getKey(@"unregister_external_folder_header"), @"Unregister external folder?");

        /// <summary>
        /// "{0} will be removed from OMS. Files in the external folder will not be deleted."
        /// </summary>
        public static LocalisableString UnregisterExternalFolderBody(string label) => new TranslatableString(getKey(@"unregister_external_folder_body"),
            @"{0} will be removed from OMS. Files in the external folder will not be deleted.", label);

        /// <summary>
        /// "Managed-folder recovery support"
        /// </summary>
        public static LocalisableString ManagedFolderRecoverySupport => new TranslatableString(getKey(@"managed_folder_recovery_support"), @"Managed-folder recovery support");

        /// <summary>
        /// "Status: {0}\nReason: {1}\nDiagnostic bundle: {2}"
        /// </summary>
        public static LocalisableString ManagedFolderRecoveryDetails(string status, string reason, string diagnosticBundle) => new TranslatableString(
            getKey(@"managed_folder_recovery_details"), @"Status: {0}\nReason: {1}\nDiagnostic bundle: {2}", status, reason, diagnosticBundle);

        /// <summary>
        /// "Retry recovery"
        /// </summary>
        public static LocalisableString RetryManagedFolderRecovery => new TranslatableString(getKey(@"retry_managed_folder_recovery"), @"Retry recovery");

        /// <summary>
        /// "The skin operation failed. No further action was started."
        /// </summary>
        public static LocalisableString FolderSkinOperationFailed => new TranslatableString(getKey(@"folder_skin_operation_failed"),
            @"The skin operation failed. No further action was started.");

        /// <summary>
        /// "The skin operation could not be completed. The target may have changed or another operation may be in progress."
        /// </summary>
        public static LocalisableString FolderSkinOperationRejected => new TranslatableString(getKey(@"folder_skin_operation_rejected"),
            @"The skin operation could not be completed. The target may have changed or another operation may be in progress.");

        /// <summary>
        /// "Reload current skin"
        /// </summary>
        public static LocalisableString ReloadCurrentSkin => new TranslatableString(getKey(@"reload_current_skin"), @"Reload current skin");

        /// <summary>
        /// "The current skin was reloaded."
        /// </summary>
        public static LocalisableString CurrentSkinReloaded => new TranslatableString(getKey(@"current_skin_reloaded"), @"The current skin was reloaded.");

        /// <summary>
        /// "No skin file changes were found."
        /// </summary>
        public static LocalisableString CurrentSkinReloadNoChanges => new TranslatableString(getKey(@"current_skin_reload_no_changes"), @"No skin file changes were found.");

        /// <summary>
        /// "Exit gameplay or gameplay preview, then try reloading the skin again."
        /// </summary>
        public static LocalisableString CurrentSkinReloadGameplayActive => new TranslatableString(
            getKey(@"current_skin_reload_gameplay_active"), @"Exit gameplay or gameplay preview, then try reloading the skin again.");

        /// <summary>
        /// "The current screen or skin source cannot reload safely right now. Return to the main menu and try again; the previous revision is still active."
        /// </summary>
        public static LocalisableString CurrentSkinReloadRejected => new TranslatableString(
            getKey(@"current_skin_reload_rejected"),
            @"The current screen or skin source cannot reload safely right now. Return to the main menu and try again; the previous revision is still active.");

        /// <summary>
        /// "The skin could not be reloaded. The previous revision is still active."
        /// </summary>
        public static LocalisableString CurrentSkinReloadFailed => new TranslatableString(
            getKey(@"current_skin_reload_failed"), @"The skin could not be reloaded. The previous revision is still active.");

        /// <summary>
        /// "Gameplay cursor size"
        /// </summary>
        public static LocalisableString GameplayCursorSize => new TranslatableString(getKey(@"gameplay_cursor_size"), @"Gameplay cursor size");

        /// <summary>
        /// "Adjust gameplay cursor size based on current beatmap"
        /// </summary>
        public static LocalisableString AutoCursorSize => new TranslatableString(getKey(@"auto_cursor_size"), @"Adjust gameplay cursor size based on current beatmap");

        /// <summary>
        /// "Show gameplay cursor during touch input"
        /// </summary>
        public static LocalisableString GameplayCursorDuringTouch => new TranslatableString(getKey(@"gameplay_cursor_during_touch"), @"Show gameplay cursor during touch input");

        /// <summary>
        /// "Beatmap skins"
        /// </summary>
        public static LocalisableString BeatmapSkins => new TranslatableString(getKey(@"beatmap_skins"), @"Beatmap skins");

        /// <summary>
        /// "Beatmap colours"
        /// </summary>
        public static LocalisableString BeatmapColours => new TranslatableString(getKey(@"beatmap_colours"), @"Beatmap colours");

        /// <summary>
        /// "Beatmap hitsounds"
        /// </summary>
        public static LocalisableString BeatmapHitsounds => new TranslatableString(getKey(@"beatmap_hitsounds"), @"Beatmap hitsounds");

        private static string getKey(string key) => $"{prefix}:{key}";
    }
}
