// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Models;
using osu.Game.Overlays;
using osu.Game.Screens.Backgrounds;
using osu.Game.Screens.Edit.Components;
using osu.Game.Storyboards;
using osu.Game.Utils;

namespace osu.Game.Screens.Edit.Setup
{
    public partial class ResourcesSection : SetupSection
    {
        // Match the asset size allowed for mania packages installed through the client.
        private const long maximum_video_bytes = 2L * 1024 * 1024 * 1024;

        // Only advertise containers that the editor's metadata reader can validate before saving.
        private static readonly string[] video_import_extensions = [".mp4", ".m4v", ".avi", ".mpg", ".wmv"];

        private FormBeatmapFileSelector audioTrackChooser = null!;
        private FormBeatmapFileSelector backgroundChooser = null!;
        private FormFileSelector videoChooser = null!;

        private readonly Bindable<EditorBeatmapSkin.SampleSet?> currentSampleSet = new Bindable<EditorBeatmapSkin.SampleSet?>();

        public override LocalisableString Title => EditorSetupStrings.ResourcesHeader;

        [Resolved]
        private MusicController music { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> working { get; set; } = null!;

        [Resolved]
        private Editor? editor { get; set; }

        [Resolved]
        private SetupScreen setupScreen { get; set; } = null!;

        private SetupScreenHeaderBackground headerBackground = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            headerBackground = new SetupScreenHeaderBackground
            {
                RelativeSizeAxes = Axes.X,
                Height = 110,
            };

            bool beatmapHasMultipleDifficulties = working.Value.BeatmapSetInfo.Beatmaps.Count > 1;

            Children = new Drawable[]
            {
                backgroundChooser = new FormBeatmapFileSelector(beatmapHasMultipleDifficulties, SupportedExtensions.IMAGE_EXTENSIONS)
                {
                    Caption = GameplaySettingsStrings.BackgroundHeader,
                    PlaceholderText = EditorSetupStrings.ClickToSelectBackground,
                },
                videoChooser = new FormFileSelector(video_import_extensions)
                {
                    Caption = EditorSetupStrings.Video,
                    PlaceholderText = EditorSetupStrings.ClickToSelectVideo,
                    HintText = EditorSetupStrings.VideoScope,
                    AllowClear = true,
                },
                audioTrackChooser = new FormBeatmapFileSelector(beatmapHasMultipleDifficulties, SupportedExtensions.AUDIO_EXTENSIONS)
                {
                    Caption = EditorSetupStrings.AudioTrack,
                    PlaceholderText = EditorSetupStrings.ClickToSelectTrack,
                },
                new FormSampleSetChooser
                {
                    Current = { BindTarget = currentSampleSet },
                },
                new FormSampleSet
                {
                    Current = { BindTarget = currentSampleSet },
                    SampleAddRequested = (file, targetName) =>
                    {
                        string actualFilename = string.Concat(targetName, file.Extension);
                        using var stream = file.OpenRead();
                        beatmaps.AddFile(working.Value.BeatmapSetInfo, stream, actualFilename);
                        return actualFilename;
                    },
                    SampleRemoveRequested = filename =>
                    {
                        var file = working.Value.BeatmapSetInfo.GetFile(filename);
                        if (file != null)
                            beatmaps.DeleteFile(working.Value.BeatmapSetInfo, file);
                    }
                },
            };

            backgroundChooser.PreviewContainer.Add(headerBackground);

            if (!string.IsNullOrEmpty(working.Value.Metadata.BackgroundFile))
                backgroundChooser.Current.Value = new FileInfo(working.Value.Metadata.BackgroundFile);

            if (!string.IsNullOrEmpty(working.Value.Metadata.AudioFile))
                audioTrackChooser.Current.Value = new FileInfo(working.Value.Metadata.AudioFile);

            var videoLayer = Beatmap.Storyboard.GetLayer("Video");
            if (videoLayer.Elements.OfType<StoryboardVideo>().FirstOrDefault() is StoryboardVideo video)
                videoChooser.Current.Value = new FileInfo(video.Path);
            videoChooser.Current.Disabled = !canEditVideo(videoLayer);

            backgroundChooser.Current.BindValueChanged(backgroundChanged);
            audioTrackChooser.Current.BindValueChanged(audioTrackChanged);
            videoChooser.Current.BindValueChanged(videoChanged);
        }

        public bool ChangeBackgroundImage(FileInfo source, bool applyToAllDifficulties)
        {
            if (!source.Exists)
                return false;

            changeResource(source, applyToAllDifficulties, @"bg",
                metadata => metadata.BackgroundFile,
                (metadata, name) => metadata.BackgroundFile = name);

            headerBackground.UpdateBackground();
            editor?.ApplyToBackground(bg => ((EditorBackgroundScreen)bg).RefreshBackground());
            return true;
        }

        public bool ChangeAudioTrack(FileInfo source, bool applyToAllDifficulties)
        {
            if (!source.Exists)
                return false;

            string artist;
            string title;

            try
            {
                using (var tagSource = TagLibUtils.GetTagLibFile(source.FullName))
                {
                    artist = tagSource.Tag.JoinedAlbumArtists ?? tagSource.Tag.JoinedPerformers;
                    title = tagSource.Tag.Title;
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "The selected audio track appears to be corrupted. Please select another one.");
                return false;
            }

            changeResource(source, applyToAllDifficulties, @"audio",
                metadata => metadata.AudioFile,
                (metadata, name) =>
                {
                    metadata.AudioFile = name;

                    if (!string.IsNullOrWhiteSpace(artist))
                    {
                        metadata.ArtistUnicode = artist;
                        metadata.Artist = MetadataUtils.StripNonRomanisedCharacters(metadata.ArtistUnicode);
                    }

                    if (!string.IsNullOrEmpty(title))
                    {
                        metadata.TitleUnicode = title;
                        metadata.Title = MetadataUtils.StripNonRomanisedCharacters(metadata.TitleUnicode);
                    }
                });

            music.ReloadCurrentTrack();
            setupScreen.MetadataChanged?.Invoke();
            return true;
        }

        public bool ChangeVideo(FileInfo? source)
        {
            var set = working.Value.BeatmapSetInfo;
            var layer = Beatmap.Storyboard.GetLayer("Video");

            if (editor == null || !canEditVideo(layer))
                return false;

            var originalElements = layer.Elements;
            var existing = originalElements.OfType<StoryboardVideo>().SingleOrDefault();
            int index = existing == null ? originalElements.Count : originalElements.IndexOf(existing);
            StoryboardVideo? replacement = null;

            if (source != null)
            {
                string extension = source.Extension.ToLowerInvariant();
                if (!video_import_extensions.Contains(extension))
                    return false;

                string filename = NamingUtils.GetNextBestFilename(set.Files.Select(file => file.Filename), $"video{extension}");

                try
                {
                    using var input = source.OpenRead();
                    if (input.Length is <= 0 or > maximum_video_bytes)
                        return false;

                    using (var metadata = TagLibUtils.GetTagLibFile(source.FullName))
                    {
                        if (metadata.Properties.VideoWidth <= 0 || metadata.Properties.VideoHeight <= 0)
                            return false;
                    }

                    beatmaps.AddFile(set, input, filename);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TagLib.CorruptFileException or TagLib.UnsupportedFormatException)
                {
                    Logger.Error(ex, "The selected video could not be read.");
                    return false;
                }

                replacement = existing?.WithPath(filename) ?? new StoryboardVideo(filename, 0);
            }

            // A pending background load can still be iterating the original storyboard elements.
            var updatedElements = originalElements.ToList();
            if (existing != null)
                updatedElements.RemoveAt(index);
            if (replacement != null)
                updatedElements.Insert(index, replacement);
            layer.Elements = updatedElements;

            if (!editor.Save())
            {
                layer.Elements = originalElements;
                if (replacement != null && set.GetFile(replacement.Path) is RealmNamedFileUsage added)
                    beatmaps.DeleteFile(set, added);
                return false;
            }

            // The old file may still be referenced by a sibling difficulty or the shared .osb.
            editor.ApplyToBackground(bg => ((EditorBackgroundScreen)bg).RefreshBackground());
            return true;
        }

        private bool canEditVideo(StoryboardLayer layer)
        {
            if (editor?.CanSaveBeatmap != true || working.Value.BeatmapSetInfo.IsExternalFilesystemStorage || Beatmap.Storyboard.HasUnsupportedBeatmapEvents)
                return false;

            return layer.Elements.OfType<StoryboardVideo>().Take(2).ToArray() switch
            {
                [] => true,
                [{ Source: StoryboardElementSource.Beatmap }] => true,
                _ => false,
            };
        }

        private void changeResource(FileInfo source, bool applyToAllDifficulties, string baseFilename, Func<BeatmapMetadata, string> readFilename, Action<BeatmapMetadata, string> writeMetadata)
        {
            var set = working.Value.BeatmapSetInfo;
            var beatmap = working.Value.BeatmapInfo;

            var otherBeatmaps = set.Beatmaps.Where(b => !b.Equals(beatmap));

            // First, clean up files which will no longer be used.
            if (applyToAllDifficulties)
            {
                foreach (var b in set.Beatmaps)
                {
                    if (set.GetFile(readFilename(b.Metadata)) is RealmNamedFileUsage otherExistingFile)
                        beatmaps.DeleteFile(set, otherExistingFile);
                }
            }
            else
            {
                RealmNamedFileUsage? oldFile = set.GetFile(readFilename(working.Value.Metadata));

                if (oldFile != null)
                {
                    bool oldFileUsedInOtherDiff = otherBeatmaps
                        .Any(b => readFilename(b.Metadata) == oldFile.Filename);
                    if (!oldFileUsedInOtherDiff)
                        beatmaps.DeleteFile(set, oldFile);
                }
            }

            // Choose a new filename that doesn't clash with any other existing files.
            string newFilename = $"{baseFilename}{source.Extension.ToLowerInvariant()}";

            if (set.GetFile(newFilename) != null)
            {
                string[] existingFilenames = set.Files.Select(f => f.Filename).Where(f =>
                    f.StartsWith(baseFilename, StringComparison.OrdinalIgnoreCase) &&
                    f.EndsWith(source.Extension, StringComparison.OrdinalIgnoreCase)).ToArray();
                newFilename = NamingUtils.GetNextBestFilename(existingFilenames, newFilename);
            }

            using (var stream = source.OpenRead())
                beatmaps.AddFile(set, stream, newFilename);

            if (applyToAllDifficulties)
            {
                foreach (var b in otherBeatmaps)
                {
                    writeMetadata(b.Metadata, newFilename);

                    // save the difficulty to re-encode the .osu file, updating any reference of the old filename.
                    //
                    // note that this triggers a full save flow, including triggering a difficulty calculation.
                    // this is not a cheap operation and should be reconsidered in the future.
                    var beatmapWorking = beatmaps.GetWorkingBeatmap(b);
                    beatmaps.Save(b, beatmapWorking.GetPlayableBeatmap(b.Ruleset), beatmapWorking.GetSkin(), beatmapWorking.Storyboard);
                }
            }

            writeMetadata(beatmap.Metadata, newFilename);

            // editor change handler cannot be aware of any file changes or other difficulties having their metadata modified.
            // for simplicity's sake, trigger a save when changing any resource to ensure the change is correctly saved.
            editor?.Save();
        }

        // to avoid scaring users, both background & audio choosers use fake `FileInfo`s with user-friendly filenames
        // when displaying an imported beatmap rather than the actual SHA-named file in storage.
        // however, that means that when a background or audio file is chosen that is broken or doesn't exist on disk when switching away from the fake files,
        // the rollback could enter an infinite loop, because the fake `FileInfo`s *also* don't exist on disk - at least not in the fake location they indicate.
        // to circumvent this issue, just allow rollback to proceed always without actually running any of the change logic to ensure visual consistency.
        // note that this means that `Change{BackgroundImage,AudioTrack}()` are required to not have made any modifications to the beatmap files
        // (or at least cleaned them up properly themselves) if they return `false`.
        private bool rollingBackBackgroundChange;
        private bool rollingBackAudioChange;
        private bool rollingBackVideoChange;

        private void backgroundChanged(ValueChangedEvent<FileInfo?> file)
        {
            if (rollingBackBackgroundChange)
                return;

            if (file.NewValue == null || !ChangeBackgroundImage(file.NewValue, backgroundChooser.ApplyToAllDifficulties.Value))
            {
                rollingBackBackgroundChange = true;
                backgroundChooser.Current.Value = file.OldValue;
                rollingBackBackgroundChange = false;
            }
        }

        private void audioTrackChanged(ValueChangedEvent<FileInfo?> file)
        {
            if (rollingBackAudioChange)
                return;

            if (file.NewValue == null || !ChangeAudioTrack(file.NewValue, audioTrackChooser.ApplyToAllDifficulties.Value))
            {
                rollingBackAudioChange = true;
                audioTrackChooser.Current.Value = file.OldValue;
                rollingBackAudioChange = false;
            }
        }

        private void videoChanged(ValueChangedEvent<FileInfo?> file)
        {
            if (rollingBackVideoChange || ChangeVideo(file.NewValue))
                return;

            rollingBackVideoChange = true;
            videoChooser.Current.Value = file.OldValue;
            rollingBackVideoChange = false;
        }
    }
}
