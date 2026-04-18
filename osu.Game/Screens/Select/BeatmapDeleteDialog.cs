// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Screens.Select
{
    public partial class BeatmapDeleteDialog : DeletionDialog
    {
        private readonly BeatmapSetInfo beatmapSet;

        public BeatmapDeleteDialog(BeatmapSetInfo beatmapSet)
        {
            this.beatmapSet = beatmapSet;

            LocalisableString source = beatmapSet.IsExternalFilesystemStorage
                ? DeleteConfirmationContentStrings.ExternalBeatmapSource
                : DeleteConfirmationContentStrings.InternalBeatmapSource;

            LocalisableString deleteAction = beatmapSet.IsExternalFilesystemStorage
                ? DeleteConfirmationContentStrings.ExternalBeatmapDeleteAction
                : DeleteConfirmationContentStrings.InternalBeatmapDeleteAction;

            LocalisableString storagePath = !string.IsNullOrWhiteSpace(beatmapSet.FilesystemStoragePath)
                ? beatmapSet.FilesystemStoragePath!
                : DeleteConfirmationContentStrings.InternalBeatmapManagedStoragePath;

            BodyText = LocalisableString.Interpolate($"{beatmapSet.Metadata.GetDisplayTitleRomanisable(false)}\n\n{DeleteConfirmationContentStrings.BeatmapSource(source)}\n{DeleteConfirmationContentStrings.BeatmapDeleteAction(deleteAction)}\n{DeleteConfirmationContentStrings.BeatmapStoragePath(storagePath)}");
        }

        [BackgroundDependencyLoader]
        private void load(BeatmapManager beatmapManager)
        {
            DangerousAction = () => beatmapManager.Delete(beatmapSet);
        }
    }
}
