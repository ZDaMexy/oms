// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using oms.Input;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsSoloPlayerPreStart : ScreenTestScene
    {
        private static readonly RulesetInfo bms_ruleset_info = new BmsRuleset().RulesetInfo;

        private readonly BmsBeatmapDecoder decoder = new BmsBeatmapDecoder();
        private readonly BmsDecodedBeatmap sourceBeatmap;

        private TestBmsSoloPlayer player = null!;
        private Mod[] selectedMods = null!;

        public TestSceneBmsSoloPlayerPreStart()
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
            AddStep("load BMS solo player", () => LoadScreen(player = new TestBmsSoloPlayer()));
            AddUntilStep("wait for BMS solo player", () => player.IsCurrentScreen() && player.IsLoaded && player.DrawableBmsRuleset?.IsLoaded == true && player.GameplayInputManager != null);
        }

        [Test]
        public void TestGameplayStartsAfterDelayWithoutHold()
        {
            AddAssert("pre-start overlay exists", () => player.PreStartOverlay != null);
            AddAssert("underlying clock not running before delay elapses", () => !player.GameplayClockContainer.IsRunning);
            AddAssert("pre-start overlay stays hidden", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);

            AddStep("expire the pre-start delay", () => player.ExpirePreStartDelay());

            AddUntilStep("gameplay starts after the delay", () => player.GameplayClockContainer.IsRunning);
            AddAssert("pre-start overlay remains hidden after auto-start", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestHoldBlocksGameplayStartAndEnablesPreStartAdjustments()
        {
            bool adjustedLaneCoverWhilePaused = false;
            double initialHiSpeed = 0;
            double hiSpeedStep = 0;
            BmsModSudden runtimeSuddenMod = null!;

            AddAssert("pre-start overlay exists", () => player.PreStartOverlay != null);
            AddStep("capture initial hi-speed", () =>
            {
                initialHiSpeed = player.DrawableBmsRuleset!.SelectedHiSpeed.Value;
                hiSpeedStep = player.DrawableBmsRuleset.HiSpeedMode.Value.GetAdjustmentStep();
                runtimeSuddenMod = player.DrawableBmsRuleset.Mods.OfType<BmsModSudden>().Single();
            });

            AddStep("try paused lane cover adjustment without hold", () => adjustedLaneCoverWhilePaused = player.DrawableBmsRuleset!.AdjustLaneCover(1));
            AddAssert("paused lane cover adjustment is blocked before hold", () => !adjustedLaneCoverWhilePaused && runtimeSuddenMod.CoverPercent.Value == runtimeSuddenMod.CoverPercent.Default);

            AddStep("press pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay visible while holding", () => player.PreStartOverlay?.State.Value == Visibility.Visible);

            AddStep("raise hi-speed with an odd lane key", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.Key1P_1), Is.True));
            AddStep("release odd lane key", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.Key1P_1), Is.True));
            AddAssert("overlay-driven hi-speed adjustment succeeds", () => Math.Abs(player.DrawableBmsRuleset!.SelectedHiSpeed.Value - (initialHiSpeed + hiSpeedStep)) <= 0.0001);

            AddStep("lower hi-speed with an even lane key", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.Key1P_2), Is.True));
            AddStep("release even lane key", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.Key1P_2), Is.True));
            AddAssert("overlay-driven hi-speed decrease succeeds", () => Math.Abs(player.DrawableBmsRuleset!.SelectedHiSpeed.Value - initialHiSpeed) <= 0.0001);

            AddStep("adjust lane cover while hold is active", () => adjustedLaneCoverWhilePaused = player.DrawableBmsRuleset!.AdjustLaneCover(1));
            AddAssert("paused lane cover adjustment succeeds during hold", () => adjustedLaneCoverWhilePaused && runtimeSuddenMod.CoverPercent.Value == runtimeSuddenMod.CoverPercent.Default + 1);

            AddStep("expire the pre-start delay while still holding", () => player.ExpirePreStartDelay());
            AddAssert("gameplay stays held while hold remains active", () => !player.GameplayClockContainer.IsRunning);

            AddStep("release pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay hidden after releasing hold", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
            AddUntilStep("gameplay starts after releasing hold", () => player.GameplayClockContainer.IsRunning);
        }

        [Test]
        public void TestReleasingHoldBeforeDelayStillWaitsForDelayedStart()
        {
            AddAssert("pre-start overlay exists", () => player.PreStartOverlay != null);
            AddAssert("underlying clock not running before delay elapses", () => !player.GameplayClockContainer.IsRunning);

            AddStep("press pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay visible while holding", () => player.PreStartOverlay?.State.Value == Visibility.Visible);

            AddStep("release pre-start hold before delay elapses", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay hidden after early release", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
            AddAssert("underlying clock still not running", () => !player.GameplayClockContainer.IsRunning);

            AddStep("expire the pre-start delay", () => player.ExpirePreStartDelay());
            AddUntilStep("gameplay starts only after delay expires", () => player.GameplayClockContainer.IsRunning);
        }

        [Test]
        public void TestUnexpectedClockStartIsSuppressedUntilDelayExpires()
        {
            AddAssert("underlying clock not running before delay elapses", () => !player.GameplayClockContainer.IsRunning);
            AddAssert("pre-start overlay stays hidden", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);

            AddStep("force gameplay clock start while pre-start is pending", () => player.ForceGameplayClockStart());
            AddAssert("underlying clock still not running after external start attempt", () => !player.GameplayClockContainer.IsRunning);
            AddAssert("pre-start overlay remains hidden after suppression", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);

            AddStep("expire the pre-start delay", () => player.ExpirePreStartDelay());
            AddUntilStep("gameplay starts after delay expires", () => player.GameplayClockContainer.IsRunning);
        }

        [Test]
        public void TestHoldPreservesTemporaryBottomOverrideWhilePersistentTargetCycles()
        {
            selectedMods = new Mod[] { new BmsModSudden(), new BmsModHidden(), new BmsModLift() };

            AddAssert("three adjustment targets available", () => player.DrawableBmsRuleset?.EnabledAdjustmentTargetCount.Value == 3);
            AddAssert("persistent target starts at sudden", () => player.DrawableBmsRuleset?.ActiveAdjustmentTarget.Value == BmsGameplayAdjustmentTarget.Sudden);

            AddStep("press pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay visible while holding", () => player.PreStartOverlay?.State.Value == Visibility.Visible);
            AddAssert("displayed target remains at persistent (sudden)", () => hasTargetState(BmsGameplayAdjustmentTarget.Sudden, 0, false));

            AddStep("press lane cover focus to cycle to hidden", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_LaneCoverFocus), Is.True));
            AddAssert("persistent target cycled to hidden", () => hasTargetState(BmsGameplayAdjustmentTarget.Hidden, 1, false));
            AddStep("release lane cover focus", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_LaneCoverFocus), Is.True));
            AddAssert("persistent target stays at hidden after release", () => hasTargetState(BmsGameplayAdjustmentTarget.Hidden, 1, false));

            AddStep("press lane cover focus to cycle to lift", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_LaneCoverFocus), Is.True));
            AddAssert("persistent target cycled to lift", () => hasLiftPersistentTargetState());
            AddStep("release lane cover focus", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_LaneCoverFocus), Is.True));
            AddAssert("persistent target stays at lift after release", () => hasLiftPersistentTargetState());

            AddStep("press lane cover focus to cycle back to sudden", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_LaneCoverFocus), Is.True));
            AddAssert("persistent target cycled back to sudden", () => hasTargetState(BmsGameplayAdjustmentTarget.Sudden, 0, false));
            AddStep("release lane cover focus", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_LaneCoverFocus), Is.True));

            AddAssert("overlay still visible while pre-start hold active", () => player.PreStartOverlay?.State.Value == Visibility.Visible);

            AddStep("release pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay hidden after releasing pre-start hold", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
            AddAssert("gameplay still waits for delay", () => !player.GameplayClockContainer.IsRunning);

            AddStep("expire the pre-start delay", () => player.ExpirePreStartDelay());
            AddUntilStep("gameplay starts after delay expires", () => player.GameplayClockContainer.IsRunning);
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

        private bool hasTargetState(BmsGameplayAdjustmentTarget expectedTarget, int expectedIndex, bool expectedTemporaryOverride)
        {
            if (player.DrawableBmsRuleset == null)
                return false;

            return player.DrawableBmsRuleset.ActiveAdjustmentTarget.Value == expectedTarget
                   && player.DrawableBmsRuleset.ActiveAdjustmentTargetIndex.Value == expectedIndex
                   && player.DrawableBmsRuleset.IsAdjustmentTargetTemporarilyOverridden.Value == expectedTemporaryOverride;
        }

        private bool hasLiftPersistentTargetState()
        {
            if (player.DrawableBmsRuleset == null)
                return false;

            return player.DrawableBmsRuleset.ActiveAdjustmentTarget.Value == BmsGameplayAdjustmentTarget.Lift
                   && player.DrawableBmsRuleset.ActiveAdjustmentTargetIndex.Value == 2
                   && !player.DrawableBmsRuleset.IsAdjustmentTargetTemporarilyOverridden.Value
                   && player.DrawableBmsRuleset.Playfield.LaneCovers.All(cover => !cover.IsFocused.Value);
        }

        private partial class TestBmsSoloPlayer : BmsSoloPlayer
        {
            private Action? expirePreStartDelay;

            public DrawableBmsRuleset? DrawableBmsRuleset => base.DrawableRuleset as DrawableBmsRuleset;

            public new GameplayClockContainer GameplayClockContainer => base.GameplayClockContainer;

            public BmsInputManager? GameplayInputManager => DrawableBmsRuleset?.GameplayInputManager;

            public BmsPreStartHiSpeedOverlay? PreStartOverlay => base.PreStartHiSpeedOverlay;

            public void ForceGameplayClockStart() => GameplayClockContainer.Start();

            public void ExpirePreStartDelay()
            {
                expirePreStartDelay?.Invoke();
                expirePreStartDelay = null;
            }

            protected override ScheduledDelegate SchedulePreStartDelay(Action onElapsed)
            {
                expirePreStartDelay = onElapsed;
                return Scheduler.AddDelayed(() => { }, double.MaxValue);
            }

            protected override Task ImportScore(Score score) => Task.CompletedTask;
        }
    }
}
