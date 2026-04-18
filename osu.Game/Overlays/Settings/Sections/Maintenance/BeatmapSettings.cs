// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Maintenance
{
    public partial class BeatmapSettings : SettingsSubsection
    {
        protected override LocalisableString Header => CommonStrings.Beatmaps;

        private SettingsButtonV2 deleteInternalBeatmapsButton = null!;
        private SettingsButtonV2 deleteExternalBeatmapsButton = null!;
        private SettingsButtonV2 deleteBeatmapVideosButton = null!;
        private SettingsButtonV2 resetOffsetsButton = null!;
        private SettingsButtonV2 restoreButton = null!;
        private SettingsButtonV2 undeleteButton = null!;

        private RealmAccess realm = null!;

        [BackgroundDependencyLoader]
        private void load(BeatmapManager beatmaps, RealmAccess realm, IDialogOverlay? dialogOverlay)
        {
            this.realm = realm;

            Add(deleteInternalBeatmapsButton = new DangerousSettingsButtonV2
            {
                Text = MaintenanceSettingsStrings.DeleteAllInternalBeatmaps,
                Action = () =>
                {
                    dialogOverlay?.Push(new MassDeleteConfirmationDialog(() =>
                    {
                        deleteInternalBeatmapsButton.Enabled.Value = false;
                        Task.Run(() => beatmaps.Delete(s => !s.IsExternalFilesystemStorage)).ContinueWith(_ => Schedule(() =>
                        {
                            deleteInternalBeatmapsButton.Enabled.Value = true;
                            refreshDeleteBeatmapButtonText();
                        }));
                    }, DeleteConfirmationContentStrings.InternalBeatmaps));
                }
            });

            Add(deleteExternalBeatmapsButton = new DangerousSettingsButtonV2
            {
                Text = MaintenanceSettingsStrings.DeleteAllExternalBeatmaps,
                Action = () =>
                {
                    dialogOverlay?.Push(new MassDeleteConfirmationDialog(() =>
                    {
                        deleteExternalBeatmapsButton.Enabled.Value = false;
                        Task.Run(() => beatmaps.Delete(s => s.IsExternalFilesystemStorage)).ContinueWith(_ => Schedule(() =>
                        {
                            deleteExternalBeatmapsButton.Enabled.Value = true;
                            refreshDeleteBeatmapButtonText();
                        }));
                    }, DeleteConfirmationContentStrings.ExternalBeatmaps));
                }
            });

            Add(deleteBeatmapVideosButton = new DangerousSettingsButtonV2
            {
                Text = MaintenanceSettingsStrings.DeleteAllBeatmapVideos,
                Action = () =>
                {
                    dialogOverlay?.Push(new MassDeleteConfirmationDialog(() =>
                    {
                        deleteBeatmapVideosButton.Enabled.Value = false;
                        Task.Run(beatmaps.DeleteAllVideos).ContinueWith(_ => Schedule(() => deleteBeatmapVideosButton.Enabled.Value = true));
                    }, DeleteConfirmationContentStrings.BeatmapVideos));
                }
            });

            Add(resetOffsetsButton = new DangerousSettingsButtonV2
            {
                Text = MaintenanceSettingsStrings.ResetAllOffsets,
                Action = () =>
                {
                    dialogOverlay?.Push(new MassDeleteConfirmationDialog(() =>
                    {
                        resetOffsetsButton.Enabled.Value = false;
                        Task.Run(beatmaps.ResetAllOffsets).ContinueWith(_ => Schedule(() => resetOffsetsButton.Enabled.Value = true));
                    }, DeleteConfirmationContentStrings.Offsets));
                }
            });

            AddRange(new Drawable[]
            {
                restoreButton = new SettingsButtonV2
                {
                    Text = MaintenanceSettingsStrings.RestoreAllHiddenDifficulties,
                    Action = () =>
                    {
                        restoreButton.Enabled.Value = false;
                        Task.Run(beatmaps.RestoreAll).ContinueWith(_ => Schedule(() => restoreButton.Enabled.Value = true));
                    }
                },
                undeleteButton = new SettingsButtonV2
                {
                    Text = MaintenanceSettingsStrings.RestoreAllRecentlyDeletedBeatmaps,
                    Action = () =>
                    {
                        undeleteButton.Enabled.Value = false;
                        Task.Run(beatmaps.UndeleteAll).ContinueWith(_ => Schedule(() =>
                        {
                            undeleteButton.Enabled.Value = true;
                            refreshDeleteBeatmapButtonText();
                        }));
                    }
                }
            });

            refreshDeleteBeatmapButtonText();
        }

        private void refreshDeleteBeatmapButtonText()
        {
            (int internalCount, int externalCount) = realm.Run(r =>
            {
                var usableBeatmaps = r.All<BeatmapSetInfo>().Where(s => !s.DeletePending && !s.Protected);
                return (usableBeatmaps.Where(s => !s.IsExternalFilesystemStorage).Count(), usableBeatmaps.Where(s => s.IsExternalFilesystemStorage).Count());
            });

            deleteInternalBeatmapsButton.Text = MaintenanceSettingsStrings.DeleteAllInternalBeatmapsWithCount(internalCount);
            deleteExternalBeatmapsButton.Text = MaintenanceSettingsStrings.DeleteAllExternalBeatmapsWithCount(externalCount);
        }
    }
}
