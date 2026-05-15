// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using oms.Input;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Sprites;
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
using osuTK.Input;

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
            double initialHiSpeed = 0;

            AddAssert("pre-start overlay exists", () => player.PreStartOverlay != null);
            AddAssert("underlying clock not running before delay elapses", () => !player.GameplayClockContainer.IsRunning);
            AddStep("capture initial hi-speed", () => initialHiSpeed = player.DrawableBmsRuleset!.SelectedHiSpeed.Value);

            AddStep("press pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay visible while holding", () => player.PreStartOverlay?.State.Value == Visibility.Visible);

            AddStep("release pre-start hold before delay elapses", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay hidden after early release", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
            AddAssert("underlying clock still not running", () => !player.GameplayClockContainer.IsRunning);
            AddStep("try raising hi-speed after early release", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.Key1P_1), Is.True));
            AddStep("release odd lane key", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.Key1P_1), Is.True));
            AddAssert("hi-speed stays unchanged after early release", () => Math.Abs(player.DrawableBmsRuleset!.SelectedHiSpeed.Value - initialHiSpeed) <= 0.0001);

            AddStep("expire the pre-start delay", () => player.ExpirePreStartDelay());
            AddUntilStep("gameplay starts only after delay expires", () => player.GameplayClockContainer.IsRunning);
        }

        [Test]
        public void TestPressingHoldAgainResetsPreStartDelay()
        {
            AddAssert("one initial pre-start delay scheduled", () => player.ScheduledPreStartDelayCount == 1);
            AddAssert("no initial pre-start delays are cancelled", () => player.CancelledScheduledPreStartDelayCount == 0);

            AddStep("press pre-start hold once", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay visible after first hold", () => player.PreStartOverlay?.State.Value == Visibility.Visible);
            AddAssert("first hold reschedules delay", () => player.ScheduledPreStartDelayCount == 2 && player.CancelledScheduledPreStartDelayCount == 1);

            AddStep("release first hold before delay elapses", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay hidden after first release", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
            AddAssert("gameplay still waits after first release", () => !player.GameplayClockContainer.IsRunning);

            AddStep("press pre-start hold again", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay visible after second hold", () => player.PreStartOverlay?.State.Value == Visibility.Visible);
            AddAssert("second hold reschedules delay again", () => player.ScheduledPreStartDelayCount == 3 && player.CancelledScheduledPreStartDelayCount == 2);

            AddStep("expire the oldest scheduled delay", () => player.ExpireNextScheduledPreStartDelay());
            AddAssert("oldest cancelled delay does not start gameplay", () => !player.GameplayClockContainer.IsRunning);
            AddAssert("latest delay still pending after oldest expiry", () => player.HasPendingActivePreStartDelay);

            AddStep("expire the second scheduled delay", () => player.ExpireNextScheduledPreStartDelay());
            AddAssert("second cancelled delay does not start gameplay", () => !player.GameplayClockContainer.IsRunning);
            AddAssert("latest delay still pending after second expiry", () => player.HasPendingActivePreStartDelay);

            AddStep("expire the latest scheduled delay while hold remains active", () => player.ExpirePreStartDelay());
            AddAssert("latest delay elapsed but gameplay stays held", () => !player.GameplayClockContainer.IsRunning);

            AddStep("release second hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay hidden after second release", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
            AddUntilStep("gameplay starts only after latest reset delay elapses and hold releases", () => player.GameplayClockContainer.IsRunning);
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
        public void TestPauseDuringPreStartPreventsGameplayStartingUnderPauseOverlay()
        {
            AddAssert("underlying clock not running before delay elapses", () => !player.GameplayClockContainer.IsRunning);
            AddAssert("pause overlay hidden initially", () => player.PauseOverlayState == Visibility.Hidden);

            AddStep("pause during pre-start", () => Assert.That(player.Pause(), Is.True));
            AddUntilStep("pause overlay visible", () => player.PauseOverlayState == Visibility.Visible);

            AddStep("expire the pre-start delay while paused", () => player.ExpirePreStartDelay());
            AddAssert("gameplay does not start under pause overlay", () => !player.GameplayClockContainer.IsRunning);
            AddAssert("pause overlay remains visible", () => player.PauseOverlayState == Visibility.Visible);

            AddStep("resume from pause", () => player.Resume());
            AddUntilStep("gameplay starts after resume", () => player.GameplayClockContainer.IsRunning);
            AddUntilStep("pause overlay hidden after resume", () => player.PauseOverlayState == Visibility.Hidden);
        }

        [Test]
        public void TestKeyboardPreStartHoldBindingStillBlocksDelayedGameplayStart()
        {
            AddAssert("underlying clock not running before delay elapses", () => !player.GameplayClockContainer.IsRunning);
            AddAssert("pre-start overlay hidden initially", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);

            AddStep("press keyboard pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerKeyPressed(InputKey.Q), Is.True));
            AddAssert("input manager sees keyboard pre-start hold", () => player.GameplayInputManager!.PreStartHoldPressed.Value);
            AddUntilStep("overlay visible while keyboard hold active", () => player.PreStartOverlay?.State.Value == Visibility.Visible);

            AddStep("expire the pre-start delay while keyboard hold is active", () => player.ExpirePreStartDelay());
            AddAssert("gameplay stays held by keyboard pre-start hold", () => !player.GameplayClockContainer.IsRunning);

            AddStep("release keyboard pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerKeyReleased(InputKey.Q), Is.True));
            AddAssert("input manager clears keyboard pre-start hold", () => !player.GameplayInputManager!.PreStartHoldPressed.Value);
            AddUntilStep("overlay hidden after keyboard hold release", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
            AddUntilStep("gameplay starts after keyboard hold release", () => player.GameplayClockContainer.IsRunning);
        }

        [Test]
        public void TestKeyboardPreStartHoldStillEnablesHiSpeedAdjustment()
        {
            double initialHiSpeed = 0;
            double hiSpeedStep = 0;

            AddAssert("pre-start overlay exists", () => player.PreStartOverlay != null);
            AddStep("capture initial hi-speed", () =>
            {
                initialHiSpeed = player.DrawableBmsRuleset!.SelectedHiSpeed.Value;
                hiSpeedStep = player.DrawableBmsRuleset.HiSpeedMode.Value.GetAdjustmentStep();
            });

            AddStep("press keyboard pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerKeyPressed(InputKey.Q), Is.True));
            AddUntilStep("overlay visible while keyboard hold active", () => player.PreStartOverlay?.State.Value == Visibility.Visible);

            AddStep("raise hi-speed with keyboard lane key", () => Assert.That(player.GameplayInputManager!.TriggerKeyPressed(InputKey.Z), Is.True));
            AddStep("release keyboard lane key", () => Assert.That(player.GameplayInputManager!.TriggerKeyReleased(InputKey.Z), Is.True));
            AddAssert("keyboard hold enables hi-speed increase", () => Math.Abs(player.DrawableBmsRuleset!.SelectedHiSpeed.Value - (initialHiSpeed + hiSpeedStep)) <= 0.0001);

            AddStep("lower hi-speed with second keyboard lane key", () => Assert.That(player.GameplayInputManager!.TriggerKeyPressed(InputKey.X), Is.True));
            AddStep("release second keyboard lane key", () => Assert.That(player.GameplayInputManager!.TriggerKeyReleased(InputKey.X), Is.True));
            AddAssert("keyboard hold enables hi-speed decrease", () => Math.Abs(player.DrawableBmsRuleset!.SelectedHiSpeed.Value - initialHiSpeed) <= 0.0001);
        }

        [Test]
        public void TestManualKeyboardPreStartHoldWorksThroughScreenInput()
        {
            AddAssert("pre-start overlay hidden initially", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);

            AddStep("press Q via manual input", () => InputManager.PressKey(Key.Q));
            AddUntilStep("manual input reaches pre-start hold", () => player.GameplayInputManager!.PreStartHoldPressed.Value);
            AddUntilStep("overlay visible from manual pre-start hold", () => player.PreStartOverlay?.State.Value == Visibility.Visible);

            AddStep("release Q via manual input", () => InputManager.ReleaseKey(Key.Q));
            AddUntilStep("manual input clears pre-start hold", () => !player.GameplayInputManager!.PreStartHoldPressed.Value);
            AddUntilStep("overlay hidden after manual release", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestManualKeyboardPreStartHoldStillWorksWhilePausedDuringPreStart()
        {
            double initialHiSpeed = 0;
            double hiSpeedStep = 0;

            AddStep("capture initial hi-speed", () =>
            {
                initialHiSpeed = player.DrawableBmsRuleset!.SelectedHiSpeed.Value;
                hiSpeedStep = player.DrawableBmsRuleset.HiSpeedMode.Value.GetAdjustmentStep();
            });

            AddStep("pause via escape", () => InputManager.Key(Key.Escape));
            AddUntilStep("pause overlay visible", () => player.PauseOverlayState == Visibility.Visible);

            AddStep("press Q via manual input while paused", () => InputManager.PressKey(Key.Q));
            AddUntilStep("manual input reaches pre-start hold while paused", () => player.GameplayInputManager!.PreStartHoldPressed.Value);

            AddStep("press Z via manual input while paused", () => InputManager.Key(Key.Z));
            AddAssert("manual paused pre-start hold still adjusts hi-speed", () => Math.Abs(player.DrawableBmsRuleset!.SelectedHiSpeed.Value - (initialHiSpeed + hiSpeedStep)) <= 0.0001);

            AddStep("release Q via manual input while paused", () => InputManager.ReleaseKey(Key.Q));
            AddUntilStep("manual paused pre-start hold clears on release", () => !player.GameplayInputManager!.PreStartHoldPressed.Value);
        }

        [Test]
        public void TestResumeDuringPreStartRestoresInteractivePreStartState()
        {
            AddAssert("underlying clock not running before delay elapses", () => !player.GameplayClockContainer.IsRunning);

            AddStep("pause during pre-start", () => Assert.That(player.Pause(), Is.True));
            AddUntilStep("pause overlay visible", () => player.PauseOverlayState == Visibility.Visible);

            AddStep("resume before delay expires", () => player.Resume());
            AddUntilStep("pause overlay hidden", () => player.PauseOverlayState == Visibility.Hidden);
            AddUntilStep("pre-start state restored", () => !player.GameplayClockContainer.IsPaused.Value && !player.GameplayClockContainer.IsRunning);

            AddStep("expire the pre-start delay", () => player.ExpirePreStartDelay());
            AddUntilStep("gameplay starts after delay expires", () => player.GameplayClockContainer.IsRunning);
        }

        [Test]
        public void TestKeyboardPreStartHoldStillWorksAfterPauseResumeDuringPreStart()
        {
            AddAssert("underlying clock not running before delay elapses", () => !player.GameplayClockContainer.IsRunning);

            AddStep("pause during pre-start", () => Assert.That(player.Pause(), Is.True));
            AddUntilStep("pause overlay visible", () => player.PauseOverlayState == Visibility.Visible);

            AddStep("resume before delay expires", () => player.Resume());
            AddUntilStep("pre-start state restored after resume", () => !player.GameplayClockContainer.IsPaused.Value && !player.GameplayClockContainer.IsRunning);

            AddStep("press keyboard pre-start hold after resume", () => Assert.That(player.GameplayInputManager!.TriggerKeyPressed(InputKey.Q), Is.True));
            AddAssert("input manager sees keyboard pre-start hold after resume", () => player.GameplayInputManager!.PreStartHoldPressed.Value);
            AddUntilStep("overlay visible after resume hold", () => player.PreStartOverlay?.State.Value == Visibility.Visible);

            AddStep("expire the pre-start delay while holding after resume", () => player.ExpirePreStartDelay());
            AddAssert("gameplay stays held after resume while keyboard hold remains active", () => !player.GameplayClockContainer.IsRunning);

            AddStep("release keyboard pre-start hold after resume", () => Assert.That(player.GameplayInputManager!.TriggerKeyReleased(InputKey.Q), Is.True));
            AddAssert("input manager clears keyboard pre-start hold after resume", () => !player.GameplayInputManager!.PreStartHoldPressed.Value);
            AddUntilStep("overlay hidden after releasing post-resume hold", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
            AddUntilStep("gameplay starts after releasing post-resume hold", () => player.GameplayClockContainer.IsRunning);
        }

        [Test]
        public void TestGameplayStartResetsTrackEvenIfSourceTrackWasAdvanced()
        {
            double simulatedPreviewProgress = 0;

            AddAssert("underlying clock not running before delay elapses", () => !player.GameplayClockContainer.IsRunning);

            AddStep("simulate source track already being at preview progress", () =>
            {
                simulatedPreviewProgress = Math.Min(800, Beatmap.Value.Track.Length * 0.8);
                Beatmap.Value.Track.Seek(simulatedPreviewProgress);
            });

            AddAssert("source track advanced away from start time", () => Math.Abs(Beatmap.Value.Track.CurrentTime - player.GameplayClockContainer.StartTime) > 100);

            AddStep("expire the pre-start delay", () => player.ExpirePreStartDelay());
            AddUntilStep("gameplay starts after delay expires", () => player.GameplayClockContainer.IsRunning);
            AddAssert("gameplay clock is closer to start time than simulated preview progress", () =>
            {
                double currentTime = player.GameplayClockContainer.CurrentTime;
                double distanceFromStart = Math.Abs(currentTime - player.GameplayClockContainer.StartTime);
                double distanceFromPreview = Math.Abs(currentTime - simulatedPreviewProgress);
                return distanceFromStart < distanceFromPreview;
            });
            AddAssert("gameplay clock did not continue from simulated preview progress",
                () => player.GameplayClockContainer.CurrentTime,
                () => Is.LessThan(simulatedPreviewProgress - 100));
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

        [Test]
        public void TestHoldKeepsHiSpeedAdjustmentsEnabledAfterDelayExpires()
        {
            double initialHiSpeed = 0;
            double hiSpeedStep = 0;

            AddStep("capture initial hi-speed", () =>
            {
                initialHiSpeed = player.DrawableBmsRuleset!.SelectedHiSpeed.Value;
                hiSpeedStep = player.DrawableBmsRuleset.HiSpeedMode.Value.GetAdjustmentStep();
            });

            AddStep("press pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay visible while holding", () => player.PreStartOverlay?.State.Value == Visibility.Visible);

            AddStep("expire the pre-start delay while still holding", () => player.ExpirePreStartDelay());
            AddAssert("gameplay still blocked while holding", () => !player.GameplayClockContainer.IsRunning);

            AddStep("raise hi-speed after delay has elapsed", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.Key1P_1), Is.True));
            AddStep("release odd lane key", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.Key1P_1), Is.True));
            AddAssert("hi-speed still adjusts after delay elapsed", () => Math.Abs(player.DrawableBmsRuleset!.SelectedHiSpeed.Value - (initialHiSpeed + hiSpeedStep)) <= 0.0001);

            AddStep("release pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay hidden after releasing hold", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
            AddUntilStep("gameplay starts after releasing hold", () => player.GameplayClockContainer.IsRunning);
        }

        [Test]
        public void TestHoldEnablesInGameAdjustmentsAndKeepsSpeedToastVisible()
        {
            double initialHiSpeed = 0;
            double hiSpeedStep = 0;
            ulong initialToastDisplayCount = 0;

            AddStep("expire the pre-start delay", () => player.ExpirePreStartDelay());
            AddUntilStep("gameplay starts after delay expires", () => player.GameplayClockContainer.IsRunning);

            AddStep("capture initial hi-speed", () =>
            {
                initialHiSpeed = player.DrawableBmsRuleset!.SelectedHiSpeed.Value;
                hiSpeedStep = player.DrawableBmsRuleset.HiSpeedMode.Value.GetAdjustmentStep();
                initialToastDisplayCount = player.DrawableBmsRuleset.SpeedMetricsToastDisplayCount;
            });

            AddStep("press ingame start hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold), Is.True));
            AddAssert("pre-start overlay stays hidden during gameplay hold", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
            AddUntilStep("speed toast refresh requests continue while holding", () => player.DrawableBmsRuleset!.SpeedMetricsToastDisplayCount >= initialToastDisplayCount + 2);

            AddStep("raise hi-speed with odd lane key during gameplay", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.Key1P_1), Is.True));
            AddStep("release odd lane key", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.Key1P_1), Is.True));
            AddAssert("hi-speed increases during gameplay hold", () => Math.Abs(player.DrawableBmsRuleset!.SelectedHiSpeed.Value - (initialHiSpeed + hiSpeedStep)) <= 0.0001);
            AddAssert("speed toast keeps being requested while holding", () => player.DrawableBmsRuleset!.SpeedMetricsToastDisplayCount > initialToastDisplayCount + 2);

            AddStep("release ingame start hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_PreStartHold), Is.True));
        }

        [Test]
        public void TestPreStartOverlayReflectsHiSpeedModeAndValueInRealPlayerFlow()
        {
            double initialHiSpeed = 0;
            double hiSpeedStep = 0;
            BmsHiSpeedMode hiSpeedMode = default;

            AddStep("capture hi-speed surface", () =>
            {
                hiSpeedMode = player.DrawableBmsRuleset!.HiSpeedMode.Value;
                initialHiSpeed = player.DrawableBmsRuleset.SelectedHiSpeed.Value;
                hiSpeedStep = hiSpeedMode.GetAdjustmentStep();
            });

            AddStep("press pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay visible while holding", () => player.PreStartOverlay?.State.Value == Visibility.Visible);
            AddAssert("overlay mode text matches current mode", () => hasOverlayText(getOverlayModeText(hiSpeedMode)));
            AddAssert("overlay value text matches current hi-speed", () => hasOverlayText(hiSpeedMode.FormatValue(initialHiSpeed)));

            AddStep("raise hi-speed with odd lane key", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.Key1P_1), Is.True));
            AddStep("release odd lane key", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.Key1P_1), Is.True));
            AddAssert("overlay value text updates after increase", () => hasOverlayText(hiSpeedMode.FormatValue(initialHiSpeed + hiSpeedStep)));

            AddStep("lower hi-speed with even lane key", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionPressed(OmsAction.Key1P_2), Is.True));
            AddStep("release even lane key", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.Key1P_2), Is.True));
            AddAssert("overlay value text returns after decrease", () => hasOverlayText(hiSpeedMode.FormatValue(initialHiSpeed)));

            AddStep("release pre-start hold", () => Assert.That(player.GameplayInputManager!.TriggerOmsActionReleased(OmsAction.UI_PreStartHold), Is.True));
            AddUntilStep("overlay hidden after releasing hold", () => player.PreStartOverlay?.State.Value == Visibility.Hidden);
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

        private bool hasOverlayText(string text)
            => tryGetOverlayText(text) != null;

        private OsuSpriteText? tryGetOverlayText(string text)
            => player.PreStartOverlay?.ChildrenOfType<OsuSpriteText>().SingleOrDefault(drawable => drawable.Text.ToString() == text);

        private static string getOverlayModeText(BmsHiSpeedMode mode)
            => mode switch
            {
                BmsHiSpeedMode.Normal => @"Normal Hi-Speed",
                BmsHiSpeedMode.Floating => @"Floating Hi-Speed",
                BmsHiSpeedMode.Classic => @"Classic Hi-Speed",
                _ => @"Hi-Speed",
            };

        private partial class TestBmsSoloPlayer : BmsSoloPlayer
        {
            private readonly List<(ScheduledDelegate Delegate, Action Callback)> scheduledPreStartDelays = new List<(ScheduledDelegate Delegate, Action Callback)>();

            public DrawableBmsRuleset? DrawableBmsRuleset => base.DrawableRuleset as DrawableBmsRuleset;

            public new GameplayClockContainer GameplayClockContainer => base.GameplayClockContainer;

            public BmsInputManager? GameplayInputManager => DrawableBmsRuleset?.GameplayInputManager;

            public BmsPreStartHiSpeedOverlay? PreStartOverlay => base.PreStartHiSpeedOverlay;

            public Visibility PauseOverlayState => base.PauseOverlay.State.Value;

            public int ScheduledPreStartDelayCount => scheduledPreStartDelays.Count;

            public int CancelledScheduledPreStartDelayCount => scheduledPreStartDelays.Count(entry => entry.Delegate.Cancelled);

            public bool HasPendingActivePreStartDelay => scheduledPreStartDelays.Any(entry => !entry.Delegate.Cancelled);

            public void ForceGameplayClockStart() => GameplayClockContainer.Start();

            public void ExpirePreStartDelay()
            {
                int activeIndex = scheduledPreStartDelays.FindLastIndex(entry => !entry.Delegate.Cancelled);

                if (activeIndex < 0)
                    return;

                var scheduledDelay = scheduledPreStartDelays[activeIndex];
                scheduledPreStartDelays.RemoveAt(activeIndex);
                scheduledDelay.Callback();
            }

            public void ExpireNextScheduledPreStartDelay()
            {
                if (scheduledPreStartDelays.Count == 0)
                    return;

                var scheduledDelay = scheduledPreStartDelays[0];
                scheduledPreStartDelays.RemoveAt(0);

                if (!scheduledDelay.Delegate.Cancelled)
                    scheduledDelay.Callback();
            }

            protected override ScheduledDelegate SchedulePreStartDelay(Action onElapsed)
            {
                var scheduledDelay = Scheduler.AddDelayed(() => { }, double.MaxValue);
                scheduledPreStartDelays.Add((scheduledDelay, onElapsed));
                return scheduledDelay;
            }

            protected override Task ImportScore(Score score) => Task.CompletedTask;
        }
    }
}
