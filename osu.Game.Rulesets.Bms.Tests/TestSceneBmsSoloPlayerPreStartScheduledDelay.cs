// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;
using osuTK.Input;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsSoloPlayerPreStartScheduledDelay : ScreenTestScene
    {
        private static readonly RulesetInfo bms_ruleset_info = new BmsRuleset().RulesetInfo;

        private readonly BmsBeatmapDecoder decoder = new BmsBeatmapDecoder();
        private readonly BmsDecodedBeatmap sourceBeatmap;

        private ScheduledDelayTestBmsSoloPlayer player = null!;
        private Mod[] selectedMods = null!;

        public TestSceneBmsSoloPlayerPreStartScheduledDelay()
        {
            sourceBeatmap = createSourceBeatmap(@"
#TITLE Pre-start Hi-Speed Stub
#BPM 60
#RANK 2
#00311:AA00
#WAVAA key1.wav
", "pre-start-hi-speed-stub.bme");
        }

        protected override Ruleset CreateRuleset() => new BmsRuleset();

        [SetUp]
        public void SetUp() => selectedMods = new Mod[] { new BmsModSudden() };

        [SetUpSteps]
        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("set ruleset", () => Ruleset.Value = bms_ruleset_info);
            AddStep("set beatmap", () => Beatmap.Value = CreateWorkingBeatmap(sourceBeatmap));
            AddStep("set lane cover mods", () => SelectedMods.Value = selectedMods);
            AddStep("load BMS solo player with real scheduled delay", () => LoadScreen(player = new ScheduledDelayTestBmsSoloPlayer()));
            AddUntilStep("wait for BMS solo player", () => player.IsCurrentScreen() && player.IsLoaded && player.DrawableBmsRuleset?.IsLoaded == true && player.GameplayInputManager != null);
        }

        [Test]
        public void TestKeyboardHoldBlocksRealScheduledDelayStart()
        {
            int refreshedDelayVersion = 0;

            AddAssert("underlying clock not running before delay elapses", () => !player.GameplayClockContainer.IsRunning);

            AddStep("press Q via manual input", () => InputManager.PressKey(Key.Q));
            AddUntilStep("manual pre-start hold active", () => player.GameplayInputManager!.PreStartHoldPressed.Value);
            AddUntilStep("real scheduled delay elapsed", () => player.DelayElapsedCount > 0);
            AddAssert("gameplay stays held after real delay elapses", () => !player.GameplayClockContainer.IsRunning);

            AddStep("release Q via manual input", () => InputManager.ReleaseKey(Key.Q));
            AddUntilStep("release schedules a fresh real delay", () => player.ScheduledDelayVersion >= 3);
            AddStep("capture refreshed real delay version", () => refreshedDelayVersion = player.ScheduledDelayVersion);
            AddAssert("gameplay still waits after releasing Q", () => !player.GameplayClockContainer.IsRunning);

            AddUntilStep("refreshed real scheduled delay elapses", () => player.DelayElapsedCount >= 2);
            AddAssert("refreshed delay is the one that elapsed", () => player.LastElapsedDelayVersion == refreshedDelayVersion);
            AddUntilStep("gameplay starts after refreshed delay elapses", () => player.GameplayClockContainer.IsRunning);
        }

        [Test]
        public void TestPressingHoldAgainResetsRealScheduledDelay()
        {
            int firstResetDelayVersion = 0;
            int secondResetDelayVersion = 0;

            AddAssert("initial real delay scheduled", () => player.ScheduledDelayVersion == 1);

            AddStep("press Q for first reset", () => InputManager.PressKey(Key.Q));
            AddUntilStep("first reset schedules a new real delay", () => player.ScheduledDelayVersion >= 2);
            AddStep("capture first reset delay version", () => firstResetDelayVersion = player.ScheduledDelayVersion);

            AddStep("release Q after first reset", () => InputManager.ReleaseKey(Key.Q));
            AddStep("press Q for second reset", () => InputManager.PressKey(Key.Q));
            AddUntilStep("second reset schedules another real delay", () => player.ScheduledDelayVersion >= firstResetDelayVersion + 1);
            AddStep("capture second reset delay version", () => secondResetDelayVersion = player.ScheduledDelayVersion);

            AddUntilStep("latest real delay elapses", () => player.DelayElapsedCount == 1);
            AddAssert("latest reset delay is the one that elapsed", () => player.LastElapsedDelayVersion == secondResetDelayVersion);
            AddAssert("first reset delay never elapses", () => player.LastElapsedDelayVersion != firstResetDelayVersion);
            AddAssert("gameplay stays held while second hold remains active", () => !player.GameplayClockContainer.IsRunning);

            AddStep("release Q after second reset", () => InputManager.ReleaseKey(Key.Q));
            AddUntilStep("release schedules another real delay", () => player.ScheduledDelayVersion >= secondResetDelayVersion + 1);
            AddAssert("gameplay still waits after releasing second reset hold", () => !player.GameplayClockContainer.IsRunning);
            AddUntilStep("refreshed post-release delay elapses", () => player.DelayElapsedCount == 2);
            AddUntilStep("gameplay starts after refreshed second-reset delay", () => player.GameplayClockContainer.IsRunning);
        }

        private BmsDecodedBeatmap createSourceBeatmap(string text, string path)
        {
            var beatmap = new BmsDecodedBeatmap(decoder.DecodeText(text, path))
            {
                BeatmapInfo =
                {
                    Ruleset = bms_ruleset_info,
                }
            };

            return beatmap;
        }

        private partial class ScheduledDelayTestBmsSoloPlayer : BmsSoloPlayer
        {
            public int DelayElapsedCount { get; private set; }

            public int ScheduledDelayVersion { get; private set; }

            public int LastElapsedDelayVersion { get; private set; }

            protected override double PreStartDelay => 1000;

            public DrawableBmsRuleset? DrawableBmsRuleset => base.DrawableRuleset as DrawableBmsRuleset;

            public new GameplayClockContainer GameplayClockContainer => base.GameplayClockContainer;

            public BmsInputManager? GameplayInputManager => DrawableBmsRuleset?.GameplayInputManager;

            protected override ScheduledDelegate SchedulePreStartDelay(Action onElapsed)
            {
                int delayVersion = ++ScheduledDelayVersion;

                return base.SchedulePreStartDelay(() =>
                {
                    DelayElapsedCount++;
                    LastElapsedDelayVersion = delayVersion;
                    onElapsed();
                });
            }

            protected override Task ImportScore(Score score) => Task.CompletedTask;
        }
    }
}
