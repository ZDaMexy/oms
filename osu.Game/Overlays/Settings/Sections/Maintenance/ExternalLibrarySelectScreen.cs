// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.IO;
using osu.Framework.Localisation;
using osu.Framework.Screens;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Maintenance
{
    /// <summary>
    /// Full-screen directory picker for selecting an external beatmap library root.
    /// The caller receives the chosen <see cref="DirectoryInfo"/> via <see cref="Selected"/>.
    /// </summary>
    public partial class ExternalLibrarySelectScreen : DirectorySelectScreen
    {
        public override LocalisableString HeaderText => ExternalLibrarySettingsStrings.SelectExternalLibraryFolder;

        /// <summary>
        /// Invoked when the user confirms a directory.
        /// </summary>
        public System.Action<DirectoryInfo>? Selected;

        protected override void OnSelection(DirectoryInfo directory)
        {
            Selected?.Invoke(directory);
            this.Exit();
        }
    }
}
