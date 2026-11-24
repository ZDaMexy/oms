// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mania;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Setup;
using osu.Game.Screens.Select;
using osu.Game.Storyboards;
using osu.Game.Tests.Resources;
using osu.Game.Tests.Visual;
using osuTK.Input;

namespace osu.Game.Tests.Editing
{
    public partial class TestSceneManiaEditorVideo : EditorSavingTestScene
    {
        protected override Ruleset CreateRuleset() => new ManiaRuleset();

        [Test]
        public void TestChooseVideoAndReopenDifficulty()
        {
            enterSetup();

            AddStep("choose a video with an uppercase extension", () =>
            {
                string path = Game.Storage.GetFullPath("song-video.MP4");
                using (var content = TestResources.OpenResource("Videos/test-video.mp4"))
                using (var target = File.Create(path))
                    content.CopyTo(target);

                videoChooser().Current.Value = new FileInfo(path);
            });

            AddAssert("video is stored with the difficulty", () => primaryVideo()?.Path, () => Is.EqualTo("video.mp4"));
            AddAssert("video file exists", () => Game.Beatmap.Value.BeatmapSetInfo.GetFile("video.mp4") != null);
            ReloadEditorToSameBeatmap();
            AddAssert("video reopens with the difficulty", () => primaryVideo()?.Path, () => Is.EqualTo("video.mp4"));

            enterSetup();
            AddStep("clear the video", () => videoChooser().Current.Value = null);

            // The editor reopened above was pushed from song select, which resumes when it exits.
            Guid difficultyId = Guid.Empty;
            AddStep("exit to song select", () =>
            {
                difficultyId = EditorBeatmap.BeatmapInfo.ID;
                InputManager.Key(Key.Escape);
            });
            AddUntilStep("wait for song select to resume", () => Game.ScreenStack.CurrentScreen is SoloSongSelect);
            AddStep("reopen the same difficulty", () =>
            {
                var difficulty = Game.BeatmapManager.QueryBeatmap(b => b.ID == difficultyId)!;
                ((SoloSongSelect)Game.ScreenStack.CurrentScreen!).Edit(difficulty);
            });
            AddUntilStep("wait for the editor to reload the difficulty",
                () => Game.ScreenStack.CurrentScreen is Editor { IsLoaded: true } currentEditor
                      && ReferenceEquals(currentEditor, Editor)
                      && EditorBeatmap.BeatmapInfo.ID == difficultyId);
            AddAssert("the reopened difficulty has no video", () => primaryVideo() == null);
        }

        private void enterSetup()
        {
            AddStep("enter setup", () => Editor.Mode.Value = EditorScreenMode.SongSetup);
            AddUntilStep("wait for video selector", () => Editor.ChildrenOfType<SetupScreen>().Any(setup => setup.ChildrenOfType<FormFileSelector>().Any(form => form.AllowClear)));
        }

        private FormFileSelector videoChooser() => Editor.ChildrenOfType<SetupScreen>().Single().ChildrenOfType<FormFileSelector>().Single(form => form.AllowClear);

        private StoryboardVideo? primaryVideo() => EditorBeatmap.Storyboard.GetLayer("Video").Elements.OfType<StoryboardVideo>().SingleOrDefault();
    }
}
