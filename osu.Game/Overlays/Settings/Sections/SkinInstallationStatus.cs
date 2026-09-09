// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Skinning;

namespace osu.Game.Overlays.Settings.Sections
{
    /// <summary>Persists the repair instructions in settings after the startup notification has closed.</summary>
    public partial class SkinInstallationStatus : OsuTextFlowContainer
    {
        private SkinManager skins = null!;

        public SkinInstallationStatus()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Padding = SettingsPanel.CONTENT_PADDING;
        }

        [BackgroundDependencyLoader]
        private void load(SkinManager skins)
        {
            this.skins = skins;
            skins.ManagedFolderJournalStateChanged += journalStateChanged;
            updateStatus();
        }

        private void journalStateChanged() => Schedule(() =>
        {
            if (!IsDisposed)
                updateStatus();
        });

        private void updateStatus()
        {
            Clear();
            if (!skins.IsGameplaySkinInstallationAvailable)
                AddText(skins.GameplaySkinInstallationRepairMessage);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (skins != null)
                skins.ManagedFolderJournalStateChanged -= journalStateChanged;
            base.Dispose(isDisposing);
        }
    }
}
