// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterface;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.PlayerSettings;
using osu.Game.Tests.Visual;
using osuTK.Input;

namespace osu.Game.Rulesets.Mania.Tests
{
    public partial class TestSceneReplayRewinding : RateAdjustedBeatmapTestScene
    {
        private ReplayPlayer currentPlayer = null!;

        [Test]
        public void TestHoldingShiftDoublesReplayRateUntilRelease()
        {
            openReplayForPlaybackControls();
            MasterGameplayClockContainer playbackClock = null!;
            AddUntilStep("wait until playback starts", () => currentPlayer.ChildrenOfType<MasterGameplayClockContainer>().SingleOrDefault()?.IsRunning == true);

            AddStep("choose a slower playback speed", () =>
            {
                playbackClock = currentPlayer.ChildrenOfType<MasterGameplayClockContainer>().Single();
                playbackClock.UserPlaybackRate.Value = 0.6;
            });

            AddStep("hold Shift", () => InputManager.PressKey(Key.LShift));
            AddUntilStep("track plays twice as fast", () => Beatmap.Value.Track.AggregateFrequency.Value, () => Is.EqualTo(1.2).Within(0.001));

            AddStep("seek while fast forwarding", () => currentPlayer.Seek(8_000));
            AddAssert("seek preserves fast forwarding", () => Beatmap.Value.Track.AggregateFrequency.Value, () => Is.EqualTo(1.2).Within(0.001));

            AddStep("adjust base speed while Shift is held", () => playbackClock.UserPlaybackRate.Value = 1.25);
            AddUntilStep("fast forward respects the playback speed limit", () => Beatmap.Value.Track.AggregateFrequency.Value, () => Is.EqualTo(2));

            AddStep("release Shift", () => InputManager.ReleaseKey(Key.LShift));
            AddUntilStep("track returns to the selected speed", () => Beatmap.Value.Track.AggregateFrequency.Value, () => Is.EqualTo(1.25));

            AddStep("exit the replay", () => currentPlayer.Exit());
            AddUntilStep("track speed is restored after exit", () => Beatmap.Value.Track.AggregateFrequency.Value, () => Is.EqualTo(1));
        }

        [Test]
        public void TestCollapsingReplaySettingsReleasesSliderKeyboardFocus()
        {
            openReplayForPlaybackControls();

            AddUntilStep("the settings control is visible", () => settings().Alpha == 1);
            AddStep("hover the settings cog", () => InputManager.MoveMouseTo(settings().ChildrenOfType<IconButton>()
                .Single(button => button.Icon.Equals(FontAwesome.Solid.Cog))));
            AddUntilStep("settings expand", () => settings().Expanded.Value);
            AddUntilStep("focus the playback speed slider", () =>
            {
                InputManager.MoveMouseTo(speedSlider().Bar);
                InputManager.Click(MouseButton.Left);
                return speedSlider().Bar.HasFocus;
            });
            AddStep("move the mouse away from the settings", () => InputManager.MoveMouseTo(currentPlayer.ScreenSpaceDrawQuad.Centre));
            AddUntilStep("settings collapse", () => !settings().Expanded.Value);
            AddAssert("hidden slider has no focus", () => !speedSlider().Bar.HasFocus);

            PlayerSettingsOverlay settings() => currentPlayer.ChildrenOfType<PlayerSettingsOverlay>().Single();
            PlayerSliderBar<double> speedSlider() => settings().ChildrenOfType<PlaybackSettings>().Single().ChildrenOfType<PlayerSliderBar<double>>().Single();
        }

        private void openReplayForPlaybackControls()
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(4))
            {
                HitObjects = { new Note { StartTime = 30_000, Column = 0 } }
            };
            var score = new Score
            {
                Replay = new Replay
                {
                    Frames =
                    {
                        new ManiaReplayFrame(0),
                        new ManiaReplayFrame(30_000, ManiaAction.Key1),
                        new ManiaReplayFrame(30_010),
                    }
                },
                ScoreInfo = new ScoreInfo()
            };

            AddStep("open a mania replay", () =>
            {
                Beatmap.Value = CreateWorkingBeatmap(beatmap);
                Ruleset.Value = beatmap.BeatmapInfo.Ruleset;
                LoadScreen(currentPlayer = new ReplayPlayer(score));
            });
            AddUntilStep("wait until replay is current", () => currentPlayer.IsCurrentScreen());
        }

        [Test]
        public void TestRewindingToMiddleOfHoldNote()
        {
            Score score = null!;

            var beatmap = new ManiaBeatmap(new StageDefinition(4))
            {
                HitObjects =
                {
                    new HoldNote
                    {
                        StartTime = 500,
                        EndTime = 1500,
                        Column = 2
                    }
                }
            };

            AddStep(@"create replay", () => score = new Score
            {
                Replay = new Replay
                {
                    Frames =
                    {
                        new ManiaReplayFrame(500, ManiaAction.Key3),
                        new ManiaReplayFrame(1500),
                    }
                },
                ScoreInfo = new ScoreInfo()
            });

            AddStep(@"set beatmap", () => Beatmap.Value = CreateWorkingBeatmap(beatmap));
            AddStep(@"set ruleset", () => Ruleset.Value = beatmap.BeatmapInfo.Ruleset);
            AddStep(@"push player", () => LoadScreen(currentPlayer = new ReplayPlayer(score)));

            AddUntilStep(@"wait until player is loaded", () => currentPlayer.IsCurrentScreen());
            AddUntilStep(@"wait for hold to be judged", () => currentPlayer.ChildrenOfType<IFrameStableClock>().Single().CurrentTime, () => Is.GreaterThan(1600));
            AddStep(@"seek to middle of hold note", () => currentPlayer.Seek(1000));
            AddUntilStep(@"wait for gameplay to complete", () => currentPlayer.GameplayState.HasCompleted);
            AddAssert(@"no misses registered", () => currentPlayer.GameplayState.ScoreProcessor.Statistics.GetValueOrDefault(HitResult.Miss), () => Is.Zero);

            AddStep(@"exit player", () => currentPlayer.Exit());
        }

        [Test]
        public void TestCorrectComboAccountingForConcurrentObjects()
        {
            Score score = null!;

            var beatmap = new ManiaBeatmap(new StageDefinition(4))
            {
                HitObjects =
                {
                    new Note
                    {
                        StartTime = 500,
                        Column = 0,
                    },
                    new Note
                    {
                        StartTime = 500,
                        Column = 2,
                    },
                    new HoldNote
                    {
                        StartTime = 1000,
                        EndTime = 1500,
                        Column = 1,
                    }
                }
            };

            AddStep(@"create replay", () => score = new Score
            {
                Replay = new Replay
                {
                    Frames =
                    {
                        new ManiaReplayFrame(500, ManiaAction.Key1, ManiaAction.Key3),
                        new ManiaReplayFrame(520),
                        new ManiaReplayFrame(1000, ManiaAction.Key2),
                        new ManiaReplayFrame(1500),
                    }
                },
                ScoreInfo = new ScoreInfo()
            });

            AddStep(@"set beatmap", () => Beatmap.Value = CreateWorkingBeatmap(beatmap));
            AddStep(@"set ruleset", () => Ruleset.Value = beatmap.BeatmapInfo.Ruleset);
            AddStep(@"push player", () => LoadScreen(currentPlayer = new ReplayPlayer(score)));

            AddUntilStep(@"wait until player is loaded", () => currentPlayer.IsCurrentScreen());
            AddUntilStep(@"wait for objects to be judged", () => currentPlayer.ChildrenOfType<IFrameStableClock>().Single().CurrentTime, () => Is.GreaterThan(1600));
            AddStep(@"stop gameplay", () => currentPlayer.ChildrenOfType<GameplayClockContainer>().Single().Stop());
            AddStep(@"seek to start", () => currentPlayer.Seek(0));
            AddAssert(@"combo is 0", () => currentPlayer.GameplayState.ScoreProcessor.Combo.Value, () => Is.Zero);

            AddStep(@"exit player", () => currentPlayer.Exit());
        }
    }
}
