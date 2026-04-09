// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using osu.Framework.Allocation;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings.Sections.Maintenance;

namespace osu.Game.Rulesets.Bms.DifficultyTable
{
    public partial class BmsDifficultyTableDirectorySelectScreen : DirectorySelectScreen
    {
        private readonly Action<string> onSelection;

        [Resolved]
        private Storage storage { get; set; } = null!;

        protected override OverlayActivation InitialOverlayActivationMode => OverlayActivation.Disabled;

        protected override DirectoryInfo InitialPath => new DirectoryInfo(storage.GetFullPath(string.Empty));

        public override bool AllowExternalScreenChange => false;

        public override bool DisallowExternalBeatmapRulesetChanges => true;

        public override bool HideOverlaysOnEnter => true;

        public override LocalisableString HeaderText => "Select a local BMS difficulty table directory";

        public BmsDifficultyTableDirectorySelectScreen(Action<string> onSelection)
        {
            this.onSelection = onSelection;
        }

        protected override void OnSelection(DirectoryInfo directory)
        {
            onSelection(directory.FullName);
            this.Exit();
        }
    }
}
