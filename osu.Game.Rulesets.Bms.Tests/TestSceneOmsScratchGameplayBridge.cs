// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using oms.Input;
using oms.Input.Devices;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneOmsScratchGameplayBridge : OsuTestScene
    {
        private const string hidTurntableIdentifier = "turntable";

        private readonly BmsBeatmapDecoder decoder = new BmsBeatmapDecoder();

        private ManualClock clock = null!;
        private TestableDrawableBmsRuleset drawableRuleset = null!;
        private OmsMouseAxisInputHandler mouseAxisHandler = null!;
        private OmsHidAxisInputHandler hidAxisHandler = null!;
        private OmsXInputButtonInputHandler? customXInputButtonHandler;
        private int customXInputButtonIndex;
        private BmsHoldNote scratchHold = null!;
        private BmsHitObject[] scratchNotes = null!;
        private DrawableBmsHoldNote scratchHoldDrawable = null!;
        private DrawableBmsHitObject[] scratchDrawables = null!;

        [Test]
        public void TestMouseAxisGameplayBridgeResolvesScratchStreamNotes()
        {
            setupScene(createScratchStreamBeatmap());

            seekTo(() => scratchNotes[0].StartTime);
            pulseScratch("pulse first note", 4);
            AddAssert("first scratch judged perfect", () => scratchDrawables[0].Result.Type == HitResult.Perfect);
            AddAssert("second scratch still pending", () => !scratchDrawables[1].Judged);

            seekTo(() => scratchNotes[1].StartTime);
            pulseScratch("pulse second note", 3);
            AddAssert("second scratch judged perfect", () => scratchDrawables[1].Result.Type == HitResult.Perfect);
        }

        [Test]
        public void TestMouseAxisGameplayBridgeLateHitForcesEarlierScratchMiss()
        {
            setupScene(createScratchStreamBeatmap("DDDD00000000000000000000"));

            AddAssert("second note stays inside earlier poor window", () => scratchNotes[1].StartTime - scratchNotes[0].StartTime < scratchNotes[0].HitWindows.WindowFor(HitResult.Miss));
            seekTo(() => scratchNotes[1].StartTime);
            pulseScratch("late scratch pulse", 4);

            AddAssert("earlier scratch forced to miss", () => scratchDrawables[0].Result.Type == HitResult.Miss);
            AddAssert("later scratch judged perfect", () => scratchDrawables[1].Result.Type == HitResult.Perfect);
        }

        [Test]
        public void TestInvertedMouseAxisGameplayBridgeResolvesScratchStreamNotes()
        {
            setupScene(createScratchStreamBeatmap(), mouseTrigger: createInvertedMouseScratchTrigger());

            seekTo(() => scratchNotes[0].StartTime);
            pulseScratch("pulse first inverted mouse scratch note", -4);
            AddAssert("first inverted mouse scratch judged perfect", () => scratchDrawables[0].Result.Type == HitResult.Perfect);
            AddAssert("second inverted mouse scratch still pending", () => !scratchDrawables[1].Judged);

            seekTo(() => scratchNotes[1].StartTime);
            pulseScratch("pulse second inverted mouse scratch note", -3);
            AddAssert("second inverted mouse scratch judged perfect", () => scratchDrawables[1].Result.Type == HitResult.Perfect);
        }

        [Test]
        public void TestInvertedMouseAxisGameplayBridgeLateHitForcesEarlierScratchMiss()
        {
            setupScene(createScratchStreamBeatmap("DDDD00000000000000000000"), mouseTrigger: createInvertedMouseScratchTrigger());

            AddAssert("second inverted mouse note stays inside earlier poor window", () => scratchNotes[1].StartTime - scratchNotes[0].StartTime < scratchNotes[0].HitWindows.WindowFor(HitResult.Miss));
            seekTo(() => scratchNotes[1].StartTime);
            pulseScratch("late inverted mouse scratch pulse", -4);

            AddAssert("earlier inverted mouse scratch forced to miss", () => scratchDrawables[0].Result.Type == HitResult.Miss);
            AddAssert("later inverted mouse scratch judged perfect", () => scratchDrawables[1].Result.Type == HitResult.Perfect);
        }

        [Test]
        public void TestHidAxisGameplayBridgeResolvesScratchStreamNotes()
        {
            setupScene(createScratchStreamBeatmap());

            seekTo(() => scratchNotes[0].StartTime);
            pulseHidScratch("pulse first HID scratch note", 4);
            AddAssert("first HID scratch judged perfect", () => scratchDrawables[0].Result.Type == HitResult.Perfect);
            AddAssert("second HID scratch still pending", () => !scratchDrawables[1].Judged);

            seekTo(() => scratchNotes[1].StartTime);
            pulseHidScratch("pulse second HID scratch note", 3);
            AddAssert("second HID scratch judged perfect", () => scratchDrawables[1].Result.Type == HitResult.Perfect);
        }

        [Test]
        public void TestHidAxisGameplayBridgeLateHitForcesEarlierScratchMiss()
        {
            setupScene(createScratchStreamBeatmap("DDDD00000000000000000000"));

            AddAssert("second HID note stays inside earlier poor window", () => scratchNotes[1].StartTime - scratchNotes[0].StartTime < scratchNotes[0].HitWindows.WindowFor(HitResult.Miss));
            seekTo(() => scratchNotes[1].StartTime);
            pulseHidScratch("late HID scratch pulse", 4);

            AddAssert("earlier HID scratch forced to miss", () => scratchDrawables[0].Result.Type == HitResult.Miss);
            AddAssert("later HID scratch judged perfect", () => scratchDrawables[1].Result.Type == HitResult.Perfect);
        }

        [Test]
        public void TestInvertedHidAxisGameplayBridgeResolvesScratchStreamNotes()
        {
            setupScene(createScratchStreamBeatmap(), hidTrigger: createInvertedHidScratchTrigger());

            seekTo(() => scratchNotes[0].StartTime);
            pulseHidScratch("pulse first inverted HID scratch note", -4);
            AddAssert("first inverted HID scratch judged perfect", () => scratchDrawables[0].Result.Type == HitResult.Perfect);
            AddAssert("second inverted HID scratch still pending", () => !scratchDrawables[1].Judged);

            seekTo(() => scratchNotes[1].StartTime);
            pulseHidScratch("pulse second inverted HID scratch note", -3);
            AddAssert("second inverted HID scratch judged perfect", () => scratchDrawables[1].Result.Type == HitResult.Perfect);
        }

        [Test]
        public void TestInvertedHidAxisGameplayBridgeLateHitForcesEarlierScratchMiss()
        {
            setupScene(createScratchStreamBeatmap("DDDD00000000000000000000"), hidTrigger: createInvertedHidScratchTrigger());

            AddAssert("second inverted HID note stays inside earlier poor window", () => scratchNotes[1].StartTime - scratchNotes[0].StartTime < scratchNotes[0].HitWindows.WindowFor(HitResult.Miss));
            seekTo(() => scratchNotes[1].StartTime);
            pulseHidScratch("late inverted HID scratch pulse", -4);

            AddAssert("earlier inverted HID scratch forced to miss", () => scratchDrawables[0].Result.Type == HitResult.Miss);
            AddAssert("later inverted HID scratch judged perfect", () => scratchDrawables[1].Result.Type == HitResult.Perfect);
        }

        [Test]
        public void TestSecondScratchMouseAxisGameplayBridgeResolvesScratchStreamNotes()
        {
            setupScene(createSecondScratchStreamBeatmap(), scratchAction: OmsAction.Key2P_Scratch);

            AddAssert("second scratch notes land on lane 8", () => scratchNotes.All(note => note.LaneIndex == 8));

            seekTo(() => scratchNotes[0].StartTime);
            pulseScratch("pulse first second scratch mouse note", 4);
            AddAssert("first second scratch mouse note judged perfect", () => scratchDrawables[0].Result.Type == HitResult.Perfect);
            AddAssert("second second scratch mouse note still pending", () => !scratchDrawables[1].Judged);

            seekTo(() => scratchNotes[1].StartTime);
            pulseScratch("pulse second second scratch mouse note", 3);
            AddAssert("second second scratch mouse note judged perfect", () => scratchDrawables[1].Result.Type == HitResult.Perfect);
        }

        [Test]
        public void TestSecondScratchMouseAxisGameplayBridgeLateHitForcesEarlierScratchMiss()
        {
            setupScene(createSecondScratchStreamBeatmap("DDDD00000000000000000000"), scratchAction: OmsAction.Key2P_Scratch);

            AddAssert("second scratch late-hit mouse notes land on lane 8", () => scratchNotes.All(note => note.LaneIndex == 8));
            AddAssert("second scratch mouse note stays inside earlier poor window", () => scratchNotes[1].StartTime - scratchNotes[0].StartTime < scratchNotes[0].HitWindows.WindowFor(HitResult.Miss));
            seekTo(() => scratchNotes[1].StartTime);
            pulseScratch("late second scratch mouse pulse", 4);

            AddAssert("earlier second scratch mouse note forced to miss", () => scratchDrawables[0].Result.Type == HitResult.Miss);
            AddAssert("later second scratch mouse note judged perfect", () => scratchDrawables[1].Result.Type == HitResult.Perfect);
        }

        [Test]
        public void TestSecondScratchHidAxisGameplayBridgeResolvesScratchStreamNotes()
        {
            setupScene(createSecondScratchStreamBeatmap(), scratchAction: OmsAction.Key2P_Scratch);

            AddAssert("second scratch HID notes land on lane 8", () => scratchNotes.All(note => note.LaneIndex == 8));

            seekTo(() => scratchNotes[0].StartTime);
            pulseHidScratch("pulse first second scratch HID note", 4);
            AddAssert("first second scratch HID note judged perfect", () => scratchDrawables[0].Result.Type == HitResult.Perfect);
            AddAssert("second second scratch HID note still pending", () => !scratchDrawables[1].Judged);

            seekTo(() => scratchNotes[1].StartTime);
            pulseHidScratch("pulse second second scratch HID note", 3);
            AddAssert("second second scratch HID note judged perfect", () => scratchDrawables[1].Result.Type == HitResult.Perfect);
        }

        [Test]
        public void TestSecondScratchHidAxisGameplayBridgeLateHitForcesEarlierScratchMiss()
        {
            setupScene(createSecondScratchStreamBeatmap("DDDD00000000000000000000"), scratchAction: OmsAction.Key2P_Scratch);

            AddAssert("second scratch late-hit HID notes land on lane 8", () => scratchNotes.All(note => note.LaneIndex == 8));
            AddAssert("second scratch HID note stays inside earlier poor window", () => scratchNotes[1].StartTime - scratchNotes[0].StartTime < scratchNotes[0].HitWindows.WindowFor(HitResult.Miss));
            seekTo(() => scratchNotes[1].StartTime);
            pulseHidScratch("late second scratch HID pulse", 4);

            AddAssert("earlier second scratch HID note forced to miss", () => scratchDrawables[0].Result.Type == HitResult.Miss);
            AddAssert("later second scratch HID note judged perfect", () => scratchDrawables[1].Result.Type == HitResult.Perfect);
        }

        [Test]
        public void TestInvertedSecondScratchMouseAxisGameplayBridgeResolvesScratchStreamNotes()
        {
            setupScene(createSecondScratchStreamBeatmap(), mouseTrigger: createInvertedMouseScratchTrigger(), scratchAction: OmsAction.Key2P_Scratch);

            AddAssert("inverted second scratch mouse notes land on lane 8", () => scratchNotes.All(note => note.LaneIndex == 8));

            seekTo(() => scratchNotes[0].StartTime);
            pulseScratch("pulse first inverted second scratch mouse note", -4);
            AddAssert("first inverted second scratch mouse note judged perfect", () => scratchDrawables[0].Result.Type == HitResult.Perfect);
            AddAssert("second inverted second scratch mouse note still pending", () => !scratchDrawables[1].Judged);

            seekTo(() => scratchNotes[1].StartTime);
            pulseScratch("pulse second inverted second scratch mouse note", -3);
            AddAssert("second inverted second scratch mouse note judged perfect", () => scratchDrawables[1].Result.Type == HitResult.Perfect);
        }

        [Test]
        public void TestInvertedSecondScratchMouseAxisGameplayBridgeLateHitForcesEarlierScratchMiss()
        {
            setupScene(createSecondScratchStreamBeatmap("DDDD00000000000000000000"), mouseTrigger: createInvertedMouseScratchTrigger(), scratchAction: OmsAction.Key2P_Scratch);

            AddAssert("inverted second scratch late-hit mouse notes land on lane 8", () => scratchNotes.All(note => note.LaneIndex == 8));
            AddAssert("inverted second scratch mouse note stays inside earlier poor window", () => scratchNotes[1].StartTime - scratchNotes[0].StartTime < scratchNotes[0].HitWindows.WindowFor(HitResult.Miss));
            seekTo(() => scratchNotes[1].StartTime);
            pulseScratch("late inverted second scratch mouse pulse", -4);

            AddAssert("earlier inverted second scratch mouse note forced to miss", () => scratchDrawables[0].Result.Type == HitResult.Miss);
            AddAssert("later inverted second scratch mouse note judged perfect", () => scratchDrawables[1].Result.Type == HitResult.Perfect);
        }

        [Test]
        public void TestInvertedSecondScratchHidAxisGameplayBridgeResolvesScratchStreamNotes()
        {
            setupScene(createSecondScratchStreamBeatmap(), hidTrigger: createInvertedHidScratchTrigger(), scratchAction: OmsAction.Key2P_Scratch);

            AddAssert("inverted second scratch HID notes land on lane 8", () => scratchNotes.All(note => note.LaneIndex == 8));

            seekTo(() => scratchNotes[0].StartTime);
            pulseHidScratch("pulse first inverted second scratch HID note", -4);
            AddAssert("first inverted second scratch HID note judged perfect", () => scratchDrawables[0].Result.Type == HitResult.Perfect);
            AddAssert("second inverted second scratch HID note still pending", () => !scratchDrawables[1].Judged);

            seekTo(() => scratchNotes[1].StartTime);
            pulseHidScratch("pulse second inverted second scratch HID note", -3);
            AddAssert("second inverted second scratch HID note judged perfect", () => scratchDrawables[1].Result.Type == HitResult.Perfect);
        }

        [Test]
        public void TestInvertedSecondScratchHidAxisGameplayBridgeLateHitForcesEarlierScratchMiss()
        {
            setupScene(createSecondScratchStreamBeatmap("DDDD00000000000000000000"), hidTrigger: createInvertedHidScratchTrigger(), scratchAction: OmsAction.Key2P_Scratch);

            AddAssert("inverted second scratch late-hit HID notes land on lane 8", () => scratchNotes.All(note => note.LaneIndex == 8));
            AddAssert("inverted second scratch HID note stays inside earlier poor window", () => scratchNotes[1].StartTime - scratchNotes[0].StartTime < scratchNotes[0].HitWindows.WindowFor(HitResult.Miss));
            seekTo(() => scratchNotes[1].StartTime);
            pulseHidScratch("late inverted second scratch HID pulse", -4);

            AddAssert("earlier inverted second scratch HID note forced to miss", () => scratchDrawables[0].Result.Type == HitResult.Miss);
            AddAssert("later inverted second scratch HID note judged perfect", () => scratchDrawables[1].Result.Type == HitResult.Perfect);
        }

        [Test]
        public void TestSecondScratchXInputGameplayBridgeResolvesScratchStreamNotes()
        {
            setupScene(createSecondScratchStreamBeatmap(), scratchAction: OmsAction.Key2P_Scratch, xInputButtonIndex: (int)osu.Framework.Input.JoystickButton.GamePadRightShoulder);

            AddAssert("second scratch XInput notes land on lane 8", () => scratchNotes.All(note => note.LaneIndex == 8));

            seekTo(() => scratchNotes[0].StartTime);
            pressXInputScratch("press first second scratch XInput note");
            AddAssert("first second scratch XInput note judged perfect", () => scratchDrawables[0].Result.Type == HitResult.Perfect);
            AddAssert("first second scratch XInput keeps single action", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch2) == 1);
            releaseXInputScratch("release first second scratch XInput note");
            AddAssert("second scratch released after first XInput note", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch2));
            AddAssert("second second scratch XInput note still pending", () => !scratchDrawables[1].Judged);

            seekTo(() => scratchNotes[1].StartTime);
            pressXInputScratch("press second second scratch XInput note");
            AddAssert("second second scratch XInput note judged perfect", () => scratchDrawables[1].Result.Type == HitResult.Perfect);
            releaseXInputScratch("release second second scratch XInput note");
            AddAssert("second scratch released after second XInput note", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch2));
        }

        [Test]
        public void TestSecondScratchXInputGameplayBridgeLateHitForcesEarlierScratchMiss()
        {
            setupScene(createSecondScratchStreamBeatmap("DDDD00000000000000000000"), scratchAction: OmsAction.Key2P_Scratch, xInputButtonIndex: (int)osu.Framework.Input.JoystickButton.GamePadRightShoulder);

            AddAssert("second scratch late-hit XInput notes land on lane 8", () => scratchNotes.All(note => note.LaneIndex == 8));
            AddAssert("second scratch XInput note stays inside earlier poor window", () => scratchNotes[1].StartTime - scratchNotes[0].StartTime < scratchNotes[0].HitWindows.WindowFor(HitResult.Miss));
            seekTo(() => scratchNotes[1].StartTime);
            pressXInputScratch("press late second scratch XInput note");

            AddAssert("earlier second scratch XInput note forced to miss", () => scratchDrawables[0].Result.Type == HitResult.Miss);
            AddAssert("later second scratch XInput note judged perfect", () => scratchDrawables[1].Result.Type == HitResult.Perfect);
            releaseXInputScratch("release late second scratch XInput note");
        }

        [Test]
        public void TestKeyboardHeldScratchSuppressesHidPulseGameplayEdgeUntilFinalRelease()
        {
            setupScene(createScratchStreamBeatmap("DDDD00000000000000000000"));

            AddStep("press keyboard scratch", () => Assert.That(drawableRuleset.InputManager.TriggerKeyPressed(osu.Framework.Input.Bindings.InputKey.Q), Is.True));
            AddAssert("keyboard scratch held once", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1) == 1);

            seekTo(() => scratchNotes[0].StartTime);
            pulseHidScratch("pulse HID scratch while keyboard held", 4);

            AddAssert("held keyboard suppresses extra gameplay hit edge", () => !scratchDrawables[0].Judged);
            AddAssert("HID pulse does not release keyboard-held scratch", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1) == 1);

            AddStep("release keyboard scratch", () => Assert.That(drawableRuleset.InputManager.TriggerKeyReleased(osu.Framework.Input.Bindings.InputKey.Q), Is.True));
            AddAssert("scratch released after final source releases", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch1));
        }

        [Test]
        public void TestKeyboardHeldScratchSuppressesMousePulseGameplayEdgeUntilFinalRelease()
        {
            setupScene(createScratchStreamBeatmap("DDDD00000000000000000000"));

            AddStep("press keyboard scratch", () => Assert.That(drawableRuleset.InputManager.TriggerKeyPressed(osu.Framework.Input.Bindings.InputKey.Q), Is.True));
            AddAssert("keyboard scratch held once", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1) == 1);

            seekTo(() => scratchNotes[0].StartTime);
            pulseScratch("pulse mouse scratch while keyboard held", 4);

            AddAssert("held keyboard suppresses extra mouse gameplay hit edge", () => !scratchDrawables[0].Judged);
            AddAssert("mouse pulse does not release keyboard-held scratch", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1) == 1);

            AddStep("release keyboard scratch", () => Assert.That(drawableRuleset.InputManager.TriggerKeyReleased(osu.Framework.Input.Bindings.InputKey.Q), Is.True));
            AddAssert("scratch released after mouse and keyboard final release", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch1));
        }

        [Test]
        public void TestKeyboardHeldScratchSuppressesXInputGameplayEdgeUntilFinalRelease()
        {
            setupScene(createScratchStreamBeatmap());

            AddStep("press keyboard scratch", () => Assert.That(drawableRuleset.InputManager.TriggerKeyPressed(osu.Framework.Input.Bindings.InputKey.Q), Is.True));
            AddAssert("keyboard scratch held once", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1) == 1);

            seekTo(() => scratchNotes[0].StartTime);
            AddStep("press XInput scratch while keyboard held", () => Assert.That(drawableRuleset.InputManager.TriggerXInputButtonPressed((int)osu.Framework.Input.JoystickButton.GamePadLeftShoulder), Is.True));

            AddAssert("held keyboard suppresses extra XInput gameplay hit edge", () => !scratchDrawables[0].Judged);
            AddAssert("XInput press keeps single scratch action", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1) == 1);

            AddStep("release keyboard scratch", () => Assert.That(drawableRuleset.InputManager.TriggerKeyReleased(osu.Framework.Input.Bindings.InputKey.Q), Is.True));
            AddAssert("XInput still holds scratch after keyboard release", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch1));

            AddStep("release XInput scratch", () => Assert.That(drawableRuleset.InputManager.TriggerXInputButtonReleased((int)osu.Framework.Input.JoystickButton.GamePadLeftShoulder), Is.True));
            AddAssert("scratch released after keyboard and XInput final release", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch1));
        }

        [Test]
        public void TestKeyboardHeldScratchSuppressesInvertedHidPulseGameplayEdgeUntilFinalRelease()
        {
            setupScene(createScratchStreamBeatmap("DDDD00000000000000000000"), hidTrigger: createInvertedHidScratchTrigger());

            AddStep("press keyboard scratch", () => Assert.That(drawableRuleset.InputManager.TriggerKeyPressed(osu.Framework.Input.Bindings.InputKey.Q), Is.True));
            AddAssert("keyboard scratch held once", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1) == 1);

            seekTo(() => scratchNotes[0].StartTime);
            pulseHidScratch("pulse inverted HID scratch while keyboard held", -4);

            AddAssert("held keyboard suppresses extra inverted HID gameplay hit edge", () => !scratchDrawables[0].Judged);
            AddAssert("inverted HID pulse does not release keyboard-held scratch", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1) == 1);

            AddStep("release keyboard scratch", () => Assert.That(drawableRuleset.InputManager.TriggerKeyReleased(osu.Framework.Input.Bindings.InputKey.Q), Is.True));
            AddAssert("scratch released after inverted HID and keyboard final release", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch1));
        }

        [Test]
        public void TestKeyboardHeldScratchSuppressesInvertedMousePulseGameplayEdgeUntilFinalRelease()
        {
            setupScene(createScratchStreamBeatmap("DDDD00000000000000000000"), mouseTrigger: createInvertedMouseScratchTrigger());

            AddStep("press keyboard scratch", () => Assert.That(drawableRuleset.InputManager.TriggerKeyPressed(osu.Framework.Input.Bindings.InputKey.Q), Is.True));
            AddAssert("keyboard scratch held once", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1) == 1);

            seekTo(() => scratchNotes[0].StartTime);
            pulseScratch("pulse inverted mouse scratch while keyboard held", -4);

            AddAssert("held keyboard suppresses extra inverted mouse gameplay hit edge", () => !scratchDrawables[0].Judged);
            AddAssert("inverted mouse pulse does not release keyboard-held scratch", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1) == 1);

            AddStep("release keyboard scratch", () => Assert.That(drawableRuleset.InputManager.TriggerKeyReleased(osu.Framework.Input.Bindings.InputKey.Q), Is.True));
            AddAssert("scratch released after inverted mouse and keyboard final release", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch1));
        }

        [Test]
        public void TestKeyboardHeldSecondScratchSuppressesHidPulseGameplayEdgeUntilFinalRelease()
        {
            setupScene(createSecondScratchStreamBeatmap("DDDD00000000000000000000"), scratchAction: OmsAction.Key2P_Scratch);

            AddAssert("second scratch suppression notes land on lane 8", () => scratchNotes.All(note => note.LaneIndex == 8));
            AddStep("press second scratch keyboard", () => Assert.That(drawableRuleset.InputManager.TriggerKeyPressed(osu.Framework.Input.Bindings.InputKey.P), Is.True));
            AddAssert("second scratch keyboard held once", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch2) == 1);

            seekTo(() => scratchNotes[0].StartTime);
            pulseHidScratch("pulse second scratch HID while keyboard held", 4);

            AddAssert("held second scratch keyboard suppresses extra HID gameplay hit edge", () => !scratchDrawables[0].Judged);
            AddAssert("second scratch HID pulse does not release keyboard-held action", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch2) == 1);

            AddStep("release second scratch keyboard", () => Assert.That(drawableRuleset.InputManager.TriggerKeyReleased(osu.Framework.Input.Bindings.InputKey.P), Is.True));
            AddAssert("second scratch released after final source releases", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch2));
        }

        [Test]
        public void TestKeyboardHeldSecondScratchSuppressesMousePulseGameplayEdgeUntilFinalRelease()
        {
            setupScene(createSecondScratchStreamBeatmap("DDDD00000000000000000000"), scratchAction: OmsAction.Key2P_Scratch);

            AddAssert("second scratch mouse suppression notes land on lane 8", () => scratchNotes.All(note => note.LaneIndex == 8));
            AddStep("press second scratch keyboard", () => Assert.That(drawableRuleset.InputManager.TriggerKeyPressed(osu.Framework.Input.Bindings.InputKey.P), Is.True));
            AddAssert("second scratch keyboard held once", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch2) == 1);

            seekTo(() => scratchNotes[0].StartTime);
            pulseScratch("pulse second scratch mouse while keyboard held", 4);

            AddAssert("held second scratch keyboard suppresses extra mouse gameplay hit edge", () => !scratchDrawables[0].Judged);
            AddAssert("second scratch mouse pulse does not release keyboard-held action", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch2) == 1);

            AddStep("release second scratch keyboard", () => Assert.That(drawableRuleset.InputManager.TriggerKeyReleased(osu.Framework.Input.Bindings.InputKey.P), Is.True));
            AddAssert("second scratch released after mouse and keyboard final release", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch2));
        }

        [Test]
        public void TestKeyboardHeldSecondScratchSuppressesXInputGameplayEdgeUntilFinalRelease()
        {
            setupScene(createSecondScratchStreamBeatmap(), scratchAction: OmsAction.Key2P_Scratch, xInputButtonIndex: (int)osu.Framework.Input.JoystickButton.GamePadRightShoulder);

            AddAssert("second scratch XInput suppression notes land on lane 8", () => scratchNotes.All(note => note.LaneIndex == 8));
            AddStep("press second scratch keyboard", () => Assert.That(drawableRuleset.InputManager.TriggerKeyPressed(osu.Framework.Input.Bindings.InputKey.P), Is.True));
            AddAssert("second scratch keyboard held once", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch2) == 1);

            seekTo(() => scratchNotes[0].StartTime);
            pressXInputScratch("press second scratch XInput while keyboard held");

            AddAssert("held second scratch keyboard suppresses extra XInput gameplay hit edge", () => !scratchDrawables[0].Judged);
            AddAssert("second scratch XInput press keeps single action", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch2) == 1);

            AddStep("release second scratch keyboard", () => Assert.That(drawableRuleset.InputManager.TriggerKeyReleased(osu.Framework.Input.Bindings.InputKey.P), Is.True));
            AddAssert("second scratch XInput still holds action after keyboard release", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch2));

            releaseXInputScratch("release second scratch XInput");
            AddAssert("second scratch released after keyboard and XInput final release", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch2));
        }

        [Test]
        public void TestKeyboardHeldSecondScratchSuppressesInvertedHidPulseGameplayEdgeUntilFinalRelease()
        {
            setupScene(createSecondScratchStreamBeatmap("DDDD00000000000000000000"), hidTrigger: createInvertedHidScratchTrigger(), scratchAction: OmsAction.Key2P_Scratch);

            AddAssert("inverted second scratch HID suppression notes land on lane 8", () => scratchNotes.All(note => note.LaneIndex == 8));
            AddStep("press second scratch keyboard", () => Assert.That(drawableRuleset.InputManager.TriggerKeyPressed(osu.Framework.Input.Bindings.InputKey.P), Is.True));
            AddAssert("second scratch keyboard held once", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch2) == 1);

            seekTo(() => scratchNotes[0].StartTime);
            pulseHidScratch("pulse inverted second scratch HID while keyboard held", -4);

            AddAssert("held second scratch keyboard suppresses extra inverted HID gameplay hit edge", () => !scratchDrawables[0].Judged);
            AddAssert("inverted second scratch HID pulse does not release keyboard-held action", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch2) == 1);

            AddStep("release second scratch keyboard", () => Assert.That(drawableRuleset.InputManager.TriggerKeyReleased(osu.Framework.Input.Bindings.InputKey.P), Is.True));
            AddAssert("second scratch released after inverted HID and keyboard final release", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch2));
        }

        [Test]
        public void TestKeyboardHeldSecondScratchSuppressesInvertedMousePulseGameplayEdgeUntilFinalRelease()
        {
            setupScene(createSecondScratchStreamBeatmap("DDDD00000000000000000000"), mouseTrigger: createInvertedMouseScratchTrigger(), scratchAction: OmsAction.Key2P_Scratch);

            AddAssert("inverted second scratch mouse suppression notes land on lane 8", () => scratchNotes.All(note => note.LaneIndex == 8));
            AddStep("press second scratch keyboard", () => Assert.That(drawableRuleset.InputManager.TriggerKeyPressed(osu.Framework.Input.Bindings.InputKey.P), Is.True));
            AddAssert("second scratch keyboard held once", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch2) == 1);

            seekTo(() => scratchNotes[0].StartTime);
            pulseScratch("pulse inverted second scratch mouse while keyboard held", -4);

            AddAssert("held second scratch keyboard suppresses extra inverted mouse gameplay hit edge", () => !scratchDrawables[0].Judged);
            AddAssert("inverted second scratch mouse pulse does not release keyboard-held action", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch2) == 1);

            AddStep("release second scratch keyboard", () => Assert.That(drawableRuleset.InputManager.TriggerKeyReleased(osu.Framework.Input.Bindings.InputKey.P), Is.True));
            AddAssert("second scratch released after inverted mouse and keyboard final release", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch2));
        }

        [Test]
        public void TestXInputGameplayBridgeResolvesScratchStreamNotes()
        {
            setupScene(createScratchStreamBeatmap());

            seekTo(() => scratchNotes[0].StartTime);
            pressXInputScratch("press first XInput scratch note");
            AddAssert("first XInput scratch judged perfect", () => scratchDrawables[0].Result.Type == HitResult.Perfect);
            AddAssert("first XInput scratch keeps single action", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1) == 1);
            releaseXInputScratch("release first XInput scratch note");
            AddAssert("scratch released after first XInput note", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch1));
            AddAssert("second XInput scratch still pending", () => !scratchDrawables[1].Judged);

            seekTo(() => scratchNotes[1].StartTime);
            pressXInputScratch("press second XInput scratch note");
            AddAssert("second XInput scratch judged perfect", () => scratchDrawables[1].Result.Type == HitResult.Perfect);
            releaseXInputScratch("release second XInput scratch note");
            AddAssert("scratch released after second XInput note", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch1));
        }

        [Test]
        public void TestXInputGameplayBridgeLateHitForcesEarlierScratchMiss()
        {
            setupScene(createScratchStreamBeatmap("DDDD00000000000000000000"));

            AddAssert("second XInput note stays inside earlier poor window", () => scratchNotes[1].StartTime - scratchNotes[0].StartTime < scratchNotes[0].HitWindows.WindowFor(HitResult.Miss));
            seekTo(() => scratchNotes[1].StartTime);
            pressXInputScratch("press late XInput scratch note");

            AddAssert("earlier XInput scratch forced to miss", () => scratchDrawables[0].Result.Type == HitResult.Miss);
            AddAssert("later XInput scratch judged perfect", () => scratchDrawables[1].Result.Type == HitResult.Perfect);
            releaseXInputScratch("release late XInput scratch note");
        }

        [Test]
        public void TestXInputScratchHoldResolvesTail()
        {
            setupScratchHoldScene(createScratchHoldBeatmap());

            seekTo(() => scratchHold.StartTime);
            pressXInputScratch("press XInput scratch at hold head");
            AddAssert("XInput scratch hold head judged perfect", () => getScratchHoldHeadDrawable().Result.Type == HitResult.Perfect);
            AddAssert("XInput scratch hold action held once", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1) == 1);

            seekTo(() => scratchHold.EndTime);
            AddUntilStep("XInput scratch hold tail judged", () => getScratchHoldTailDrawable().Judged);
            AddAssert("XInput scratch hold tail resolved via held path", () => getScratchHoldTailDrawable().Result.Type == HitResult.IgnoreHit);
            AddAssert("XInput scratch hold fully judged", () => scratchHoldDrawable.AllJudged);

            releaseXInputScratch("release XInput scratch after hold");
            AddAssert("scratch released after XInput hold final release", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch1));
        }

        [Test]
        public void TestSecondScratchXInputScratchHoldResolvesTail()
        {
            setupScratchHoldScene(createSecondScratchHoldBeatmap(), scratchAction: OmsAction.Key2P_Scratch, xInputButtonIndex: (int)osu.Framework.Input.JoystickButton.GamePadRightShoulder);

            AddAssert("second scratch XInput hold lands on lane 8", () => scratchHold.LaneIndex == 8);

            seekTo(() => scratchHold.StartTime);
            pressXInputScratch("press second scratch XInput at hold head");
            AddAssert("second scratch XInput hold head judged perfect", () => getScratchHoldHeadDrawable().Result.Type == HitResult.Perfect);
            AddAssert("second scratch XInput hold action held once", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch2) == 1);

            seekTo(() => scratchHold.EndTime);
            AddUntilStep("second scratch XInput hold tail judged", () => getScratchHoldTailDrawable().Judged);
            AddAssert("second scratch XInput hold tail resolved via held path", () => getScratchHoldTailDrawable().Result.Type == HitResult.IgnoreHit);
            AddAssert("second scratch XInput hold fully judged", () => scratchHoldDrawable.AllJudged);

            releaseXInputScratch("release second scratch XInput after hold");
            AddAssert("second scratch released after XInput hold final release", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch2));
        }

        [Test]
        public void TestKeyboardHeldScratchHoldTransfersToXInputAndResolvesTail()
        {
            setupScratchHoldScene(createScratchHoldBeatmap());

            seekTo(() => scratchHold.StartTime);
            AddStep("press keyboard scratch at hold head", () => Assert.That(drawableRuleset.InputManager.TriggerKeyPressed(osu.Framework.Input.Bindings.InputKey.Q), Is.True));
            AddAssert("scratch hold head judged perfect", () => getScratchHoldHeadDrawable().Result.Type == HitResult.Perfect);
            AddAssert("keyboard scratch hold action held once", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1) == 1);

            seekTo(() => scratchHold.StartTime + 50);
            pressXInputScratch("press XInput scratch during held hold");
            AddAssert("XInput press keeps single scratch action during hold", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1) == 1);

            AddStep("release keyboard scratch during XInput hold", () => Assert.That(drawableRuleset.InputManager.TriggerKeyReleased(osu.Framework.Input.Bindings.InputKey.Q), Is.True));
            AddAssert("XInput still holds scratch after keyboard release", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch1));
            AddAssert("scratch hold tail still pending after XInput takeover", () => !getScratchHoldTailDrawable().Judged);

            seekTo(() => scratchHold.EndTime);
            AddUntilStep("scratch hold tail judged after XInput takeover", () => getScratchHoldTailDrawable().Judged);
            AddAssert("scratch hold tail resolved via XInput held path", () => getScratchHoldTailDrawable().Result.Type == HitResult.IgnoreHit);
            AddAssert("scratch hold fully judged after XInput takeover", () => scratchHoldDrawable.AllJudged);

            releaseXInputScratch("release XInput scratch after takeover hold");
            AddAssert("scratch released after XInput takeover final release", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch1));
        }

        [Test]
        public void TestKeyboardHeldScratchHoldSurvivesMousePulseAndResolvesTail()
        {
            setupScratchHoldScene(createScratchHoldBeatmap());

            seekTo(() => scratchHold.StartTime);
            AddStep("press keyboard scratch at hold head", () => Assert.That(drawableRuleset.InputManager.TriggerKeyPressed(osu.Framework.Input.Bindings.InputKey.Q), Is.True));
            AddAssert("scratch hold head judged perfect", () => getScratchHoldHeadDrawable().Result.Type == HitResult.Perfect);
            AddAssert("scratch hold action held once", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1) == 1);

            seekTo(() => scratchHold.StartTime + 50);
            pulseScratch("pulse mouse scratch during held hold", 4);

            AddAssert("mouse pulse does not break held scratch hold", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1) == 1);
            AddAssert("scratch hold tail still pending after mouse pulse", () => !getScratchHoldTailDrawable().Judged);

            seekTo(() => scratchHold.EndTime);
            AddUntilStep("scratch hold tail judged after mouse pulse", () => getScratchHoldTailDrawable().Judged);
            AddAssert("scratch hold tail resolved via held path after mouse pulse", () => getScratchHoldTailDrawable().Result.Type == HitResult.IgnoreHit);
            AddAssert("scratch hold fully judged after mouse pulse", () => scratchHoldDrawable.AllJudged);

            AddStep("release keyboard scratch after mouse hold", () => Assert.That(drawableRuleset.InputManager.TriggerKeyReleased(osu.Framework.Input.Bindings.InputKey.Q), Is.True));
            AddAssert("scratch released after mouse hold final release", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch1));
        }

        [Test]
        public void TestKeyboardHeldScratchHoldSurvivesInvertedMousePulseAndResolvesTail()
        {
            setupScratchHoldScene(createScratchHoldBeatmap(), mouseTrigger: createInvertedMouseScratchTrigger());

            seekTo(() => scratchHold.StartTime);
            AddStep("press keyboard scratch at hold head", () => Assert.That(drawableRuleset.InputManager.TriggerKeyPressed(osu.Framework.Input.Bindings.InputKey.Q), Is.True));
            AddAssert("scratch hold head judged perfect", () => getScratchHoldHeadDrawable().Result.Type == HitResult.Perfect);
            AddAssert("scratch hold action held once", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1) == 1);

            seekTo(() => scratchHold.StartTime + 50);
            pulseScratch("pulse inverted mouse scratch during held hold", -4);

            AddAssert("inverted mouse pulse does not break held scratch hold", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1) == 1);
            AddAssert("scratch hold tail still pending after inverted mouse pulse", () => !getScratchHoldTailDrawable().Judged);

            seekTo(() => scratchHold.EndTime);
            AddUntilStep("scratch hold tail judged after inverted mouse pulse", () => getScratchHoldTailDrawable().Judged);
            AddAssert("scratch hold tail resolved via held path after inverted mouse pulse", () => getScratchHoldTailDrawable().Result.Type == HitResult.IgnoreHit);
            AddAssert("scratch hold fully judged after inverted mouse pulse", () => scratchHoldDrawable.AllJudged);

            AddStep("release keyboard scratch after inverted mouse hold", () => Assert.That(drawableRuleset.InputManager.TriggerKeyReleased(osu.Framework.Input.Bindings.InputKey.Q), Is.True));
            AddAssert("scratch released after inverted mouse hold final release", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch1));
        }

        [Test]
        public void TestKeyboardHeldScratchHoldSurvivesHidPulseAndResolvesTail()
        {
            setupScratchHoldScene(createScratchHoldBeatmap());

            seekTo(() => scratchHold.StartTime);
            AddStep("press keyboard scratch at hold head", () => Assert.That(drawableRuleset.InputManager.TriggerKeyPressed(osu.Framework.Input.Bindings.InputKey.Q), Is.True));
            AddAssert("scratch hold head judged perfect", () => getScratchHoldHeadDrawable().Result.Type == HitResult.Perfect);
            AddAssert("scratch hold action held once", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1) == 1);

            seekTo(() => scratchHold.StartTime + 50);
            pulseHidScratch("pulse HID scratch during held hold", 4);

            AddAssert("HID pulse does not break held scratch hold", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1) == 1);
            AddAssert("scratch hold tail still pending after HID pulse", () => !getScratchHoldTailDrawable().Judged);

            seekTo(() => scratchHold.EndTime);
            AddUntilStep("scratch hold tail judged after HID pulse", () => getScratchHoldTailDrawable().Judged);
            AddAssert("scratch hold tail resolved via held path after HID pulse", () => getScratchHoldTailDrawable().Result.Type == HitResult.IgnoreHit);
            AddAssert("scratch hold fully judged after HID pulse", () => scratchHoldDrawable.AllJudged);

            AddStep("release keyboard scratch after HID hold", () => Assert.That(drawableRuleset.InputManager.TriggerKeyReleased(osu.Framework.Input.Bindings.InputKey.Q), Is.True));
            AddAssert("scratch released after HID hold final release", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch1));
        }

        [Test]
        public void TestKeyboardHeldScratchHoldSurvivesInvertedHidPulseAndResolvesTail()
        {
            setupScratchHoldScene(createScratchHoldBeatmap(), hidTrigger: createInvertedHidScratchTrigger());

            seekTo(() => scratchHold.StartTime);
            AddStep("press keyboard scratch at hold head", () => Assert.That(drawableRuleset.InputManager.TriggerKeyPressed(osu.Framework.Input.Bindings.InputKey.Q), Is.True));
            AddAssert("scratch hold head judged perfect", () => getScratchHoldHeadDrawable().Result.Type == HitResult.Perfect);
            AddAssert("scratch hold action held once", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1) == 1);

            seekTo(() => scratchHold.StartTime + 50);
            pulseHidScratch("pulse inverted HID scratch during held hold", -4);

            AddAssert("inverted HID pulse does not break held scratch hold", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1) == 1);
            AddAssert("scratch hold tail still pending after inverted HID pulse", () => !getScratchHoldTailDrawable().Judged);

            seekTo(() => scratchHold.EndTime);
            AddUntilStep("scratch hold tail judged after inverted HID pulse", () => getScratchHoldTailDrawable().Judged);
            AddAssert("scratch hold tail resolved via held path after inverted HID pulse", () => getScratchHoldTailDrawable().Result.Type == HitResult.IgnoreHit);
            AddAssert("scratch hold fully judged after inverted HID pulse", () => scratchHoldDrawable.AllJudged);

            AddStep("release keyboard scratch after inverted HID hold", () => Assert.That(drawableRuleset.InputManager.TriggerKeyReleased(osu.Framework.Input.Bindings.InputKey.Q), Is.True));
            AddAssert("scratch released after inverted HID hold final release", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch1));
        }

        [Test]
        public void TestKeyboardHeldSecondScratchHoldTransfersToXInputAndResolvesTail()
        {
            setupScratchHoldScene(createSecondScratchHoldBeatmap(), scratchAction: OmsAction.Key2P_Scratch, xInputButtonIndex: (int)osu.Framework.Input.JoystickButton.GamePadRightShoulder);

            AddAssert("second scratch hold lands on lane 8", () => scratchHold.LaneIndex == 8);

            seekTo(() => scratchHold.StartTime);
            AddStep("press second scratch keyboard at hold head", () => Assert.That(drawableRuleset.InputManager.TriggerKeyPressed(osu.Framework.Input.Bindings.InputKey.P), Is.True));
            AddAssert("second scratch hold head judged perfect", () => getScratchHoldHeadDrawable().Result.Type == HitResult.Perfect);
            AddAssert("second scratch hold action held once", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch2) == 1);

            seekTo(() => scratchHold.StartTime + 50);
            pressXInputScratch("press second scratch XInput during held hold");
            AddAssert("second scratch XInput keeps single action during hold", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch2) == 1);

            AddStep("release second scratch keyboard during XInput hold", () => Assert.That(drawableRuleset.InputManager.TriggerKeyReleased(osu.Framework.Input.Bindings.InputKey.P), Is.True));
            AddAssert("second scratch XInput still holds after keyboard release", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch2));
            AddAssert("second scratch hold tail still pending after XInput takeover", () => !getScratchHoldTailDrawable().Judged);

            seekTo(() => scratchHold.EndTime);
            AddUntilStep("second scratch hold tail judged after XInput takeover", () => getScratchHoldTailDrawable().Judged);
            AddAssert("second scratch hold tail resolved via XInput held path", () => getScratchHoldTailDrawable().Result.Type == HitResult.IgnoreHit);
            AddAssert("second scratch hold fully judged after XInput takeover", () => scratchHoldDrawable.AllJudged);

            releaseXInputScratch("release second scratch XInput after takeover hold");
            AddAssert("second scratch released after XInput takeover final release", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch2));
        }

        [Test]
        public void TestKeyboardHeldSecondScratchHoldSurvivesMousePulseAndResolvesTail()
        {
            setupScratchHoldScene(createSecondScratchHoldBeatmap(), scratchAction: OmsAction.Key2P_Scratch);

            AddAssert("second scratch mouse hold lands on lane 8", () => scratchHold.LaneIndex == 8);

            seekTo(() => scratchHold.StartTime);
            AddStep("press second scratch keyboard at mouse hold head", () => Assert.That(drawableRuleset.InputManager.TriggerKeyPressed(osu.Framework.Input.Bindings.InputKey.P), Is.True));
            AddAssert("second scratch hold head judged perfect", () => getScratchHoldHeadDrawable().Result.Type == HitResult.Perfect);
            AddAssert("second scratch hold action held once", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch2) == 1);

            seekTo(() => scratchHold.StartTime + 50);
            pulseScratch("pulse second scratch mouse during held hold", 4);

            AddAssert("second scratch mouse pulse does not break held hold", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch2) == 1);
            AddAssert("second scratch hold tail still pending after mouse pulse", () => !getScratchHoldTailDrawable().Judged);

            seekTo(() => scratchHold.EndTime);
            AddUntilStep("second scratch hold tail judged after mouse pulse", () => getScratchHoldTailDrawable().Judged);
            AddAssert("second scratch hold tail resolved via held path after mouse pulse", () => getScratchHoldTailDrawable().Result.Type == HitResult.IgnoreHit);
            AddAssert("second scratch hold fully judged after mouse pulse", () => scratchHoldDrawable.AllJudged);

            AddStep("release second scratch keyboard after mouse hold", () => Assert.That(drawableRuleset.InputManager.TriggerKeyReleased(osu.Framework.Input.Bindings.InputKey.P), Is.True));
            AddAssert("second scratch released after mouse hold final release", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch2));
        }

        [Test]
        public void TestKeyboardHeldSecondScratchHoldSurvivesHidPulseAndResolvesTail()
        {
            setupScratchHoldScene(createSecondScratchHoldBeatmap(), scratchAction: OmsAction.Key2P_Scratch);

            AddAssert("second scratch HID hold lands on lane 8", () => scratchHold.LaneIndex == 8);

            seekTo(() => scratchHold.StartTime);
            AddStep("press second scratch keyboard at HID hold head", () => Assert.That(drawableRuleset.InputManager.TriggerKeyPressed(osu.Framework.Input.Bindings.InputKey.P), Is.True));
            AddAssert("second scratch hold head judged perfect", () => getScratchHoldHeadDrawable().Result.Type == HitResult.Perfect);
            AddAssert("second scratch hold action held once", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch2) == 1);

            seekTo(() => scratchHold.StartTime + 50);
            pulseHidScratch("pulse second scratch HID during held hold", 4);

            AddAssert("second scratch HID pulse does not break held hold", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch2) == 1);
            AddAssert("second scratch hold tail still pending after HID pulse", () => !getScratchHoldTailDrawable().Judged);

            seekTo(() => scratchHold.EndTime);
            AddUntilStep("second scratch hold tail judged after HID pulse", () => getScratchHoldTailDrawable().Judged);
            AddAssert("second scratch hold tail resolved via held path after HID pulse", () => getScratchHoldTailDrawable().Result.Type == HitResult.IgnoreHit);
            AddAssert("second scratch hold fully judged after HID pulse", () => scratchHoldDrawable.AllJudged);

            AddStep("release second scratch keyboard after HID hold", () => Assert.That(drawableRuleset.InputManager.TriggerKeyReleased(osu.Framework.Input.Bindings.InputKey.P), Is.True));
            AddAssert("second scratch released after HID hold final release", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch2));
        }

        [Test]
        public void TestKeyboardHeldSecondScratchHoldSurvivesInvertedMousePulseAndResolvesTail()
        {
            setupScratchHoldScene(createSecondScratchHoldBeatmap(), mouseTrigger: createInvertedMouseScratchTrigger(), scratchAction: OmsAction.Key2P_Scratch);

            AddAssert("inverted second scratch mouse hold lands on lane 8", () => scratchHold.LaneIndex == 8);

            seekTo(() => scratchHold.StartTime);
            AddStep("press second scratch keyboard at inverted mouse hold head", () => Assert.That(drawableRuleset.InputManager.TriggerKeyPressed(osu.Framework.Input.Bindings.InputKey.P), Is.True));
            AddAssert("second scratch hold head judged perfect", () => getScratchHoldHeadDrawable().Result.Type == HitResult.Perfect);
            AddAssert("second scratch hold action held once", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch2) == 1);

            seekTo(() => scratchHold.StartTime + 50);
            pulseScratch("pulse inverted second scratch mouse during held hold", -4);

            AddAssert("inverted second scratch mouse pulse does not break held hold", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch2) == 1);
            AddAssert("second scratch hold tail still pending after inverted mouse pulse", () => !getScratchHoldTailDrawable().Judged);

            seekTo(() => scratchHold.EndTime);
            AddUntilStep("second scratch hold tail judged after inverted mouse pulse", () => getScratchHoldTailDrawable().Judged);
            AddAssert("second scratch hold tail resolved via held path after inverted mouse pulse", () => getScratchHoldTailDrawable().Result.Type == HitResult.IgnoreHit);
            AddAssert("second scratch hold fully judged after inverted mouse pulse", () => scratchHoldDrawable.AllJudged);

            AddStep("release second scratch keyboard after inverted mouse hold", () => Assert.That(drawableRuleset.InputManager.TriggerKeyReleased(osu.Framework.Input.Bindings.InputKey.P), Is.True));
            AddAssert("second scratch released after inverted mouse hold final release", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch2));
        }

        [Test]
        public void TestKeyboardHeldSecondScratchHoldSurvivesInvertedHidPulseAndResolvesTail()
        {
            setupScratchHoldScene(createSecondScratchHoldBeatmap(), hidTrigger: createInvertedHidScratchTrigger(), scratchAction: OmsAction.Key2P_Scratch);

            AddAssert("inverted second scratch HID hold lands on lane 8", () => scratchHold.LaneIndex == 8);

            seekTo(() => scratchHold.StartTime);
            AddStep("press second scratch keyboard at inverted HID hold head", () => Assert.That(drawableRuleset.InputManager.TriggerKeyPressed(osu.Framework.Input.Bindings.InputKey.P), Is.True));
            AddAssert("second scratch hold head judged perfect", () => getScratchHoldHeadDrawable().Result.Type == HitResult.Perfect);
            AddAssert("second scratch hold action held once", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch2) == 1);

            seekTo(() => scratchHold.StartTime + 50);
            pulseHidScratch("pulse inverted second scratch HID during held hold", -4);

            AddAssert("inverted second scratch HID pulse does not break held hold", () => drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch2) == 1);
            AddAssert("second scratch hold tail still pending after inverted HID pulse", () => !getScratchHoldTailDrawable().Judged);

            seekTo(() => scratchHold.EndTime);
            AddUntilStep("second scratch hold tail judged after inverted HID pulse", () => getScratchHoldTailDrawable().Judged);
            AddAssert("second scratch hold tail resolved via held path after inverted HID pulse", () => getScratchHoldTailDrawable().Result.Type == HitResult.IgnoreHit);
            AddAssert("second scratch hold fully judged after inverted HID pulse", () => scratchHoldDrawable.AllJudged);

            AddStep("release second scratch keyboard after inverted HID hold", () => Assert.That(drawableRuleset.InputManager.TriggerKeyReleased(osu.Framework.Input.Bindings.InputKey.P), Is.True));
            AddAssert("second scratch released after inverted HID hold final release", () => !drawableRuleset.InputManager.KeyBindingContainer.PressedActions.Contains(BmsAction.Scratch2));
        }

        private void setupScene(BmsBeatmap beatmap, OmsBindingTrigger? mouseTrigger = null, OmsBindingTrigger? hidTrigger = null, OmsAction scratchAction = OmsAction.Key1P_Scratch, int? xInputButtonIndex = null)
        {
            AddStep("setup scratch bridge scene", () =>
            {
                var resolvedMouseTrigger = mouseTrigger ?? createDefaultMouseScratchTrigger();
                var resolvedHidTrigger = hidTrigger ?? createDefaultHidScratchTrigger();

                customXInputButtonHandler = null;
                customXInputButtonIndex = xInputButtonIndex ?? 0;

                scratchNotes = beatmap.HitObjects.OfType<BmsHitObject>()
                                    .Where(hitObject => hitObject.IsScratch && hitObject is not BmsHoldNote)
                                    .OrderBy(hitObject => hitObject.StartTime)
                                    .ToArray();

                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Clock = new FramedClock(clock = new ManualClock { IsRunning = true }),
                    Child = drawableRuleset = new TestableDrawableBmsRuleset(new BmsRuleset(), beatmap)
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                };

                mouseAxisHandler = new OmsMouseAxisInputHandler(
                    new[] { new OmsBinding(scratchAction, resolvedMouseTrigger) },
                    action => drawableRuleset.InputManager.TriggerOmsActionPressed(action),
                    action => drawableRuleset.InputManager.TriggerOmsActionReleased(action));

                hidAxisHandler = new OmsHidAxisInputHandler(
                    new[] { new OmsBinding(scratchAction, resolvedHidTrigger) },
                    action => drawableRuleset.InputManager.TriggerOmsActionPressed(action),
                    action => drawableRuleset.InputManager.TriggerOmsActionReleased(action));

                if (xInputButtonIndex.HasValue)
                {
                    customXInputButtonHandler = new OmsXInputButtonInputHandler(
                        new[] { new OmsBinding(scratchAction, OmsBindingTrigger.XInputButton(xInputButtonIndex.Value)) },
                        action => drawableRuleset.InputManager.TriggerOmsActionPressed(action),
                        action => drawableRuleset.InputManager.TriggerOmsActionReleased(action));
                }
            });

            AddUntilStep("scratch drawables loaded", () => getScratchDrawables().Length == 2);
            AddStep("cache scratch drawables", () => scratchDrawables = getScratchDrawables());
        }

        private void setupScratchHoldScene(BmsBeatmap beatmap, OmsBindingTrigger? mouseTrigger = null, OmsBindingTrigger? hidTrigger = null, OmsAction scratchAction = OmsAction.Key1P_Scratch, int? xInputButtonIndex = null)
        {
            AddStep("setup scratch hold bridge scene", () =>
            {
                var resolvedMouseTrigger = mouseTrigger ?? createDefaultMouseScratchTrigger();
                var resolvedHidTrigger = hidTrigger ?? createDefaultHidScratchTrigger();

                customXInputButtonHandler = null;
                customXInputButtonIndex = xInputButtonIndex ?? 0;

                scratchHold = beatmap.HitObjects.OfType<BmsHoldNote>().Single(hold => hold.IsScratch);

                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Clock = new FramedClock(clock = new ManualClock { IsRunning = true }),
                    Child = drawableRuleset = new TestableDrawableBmsRuleset(new BmsRuleset(), beatmap)
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                };

                mouseAxisHandler = new OmsMouseAxisInputHandler(
                    new[] { new OmsBinding(scratchAction, resolvedMouseTrigger) },
                    action => drawableRuleset.InputManager.TriggerOmsActionPressed(action),
                    action => drawableRuleset.InputManager.TriggerOmsActionReleased(action));

                hidAxisHandler = new OmsHidAxisInputHandler(
                    new[] { new OmsBinding(scratchAction, resolvedHidTrigger) },
                    action => drawableRuleset.InputManager.TriggerOmsActionPressed(action),
                    action => drawableRuleset.InputManager.TriggerOmsActionReleased(action));

                if (xInputButtonIndex.HasValue)
                {
                    customXInputButtonHandler = new OmsXInputButtonInputHandler(
                        new[] { new OmsBinding(scratchAction, OmsBindingTrigger.XInputButton(xInputButtonIndex.Value)) },
                        action => drawableRuleset.InputManager.TriggerOmsActionPressed(action),
                        action => drawableRuleset.InputManager.TriggerOmsActionReleased(action));
                }
            });

            AddUntilStep("scratch hold drawable loaded", () => getScratchHoldDrawable() != null);
            AddStep("cache scratch hold drawable", () => scratchHoldDrawable = getScratchHoldDrawable());
        }

        private void seekTo(System.Func<double> timeProvider)
        {
            AddStep("seek clock", () => clock.CurrentTime = timeProvider());
            AddUntilStep("wait for frame-stable seek", () => Precision.AlmostEquals(drawableRuleset.FrameStableClock.CurrentTime, timeProvider(), 1));
        }

        private void pulseScratch(string stepName, float delta)
        {
            AddStep(stepName, () =>
            {
                mouseAxisHandler.BeginFrame();
                Assert.That(mouseAxisHandler.ApplyAxisDelta(OmsMouseAxis.X, delta), Is.True);
                Assert.That(mouseAxisHandler.FinishFrame(), Is.True);
            });
        }

        private void pulseHidScratch(string stepName, int delta)
        {
            AddStep(stepName, () =>
            {
                hidAxisHandler.BeginPolling();
                Assert.That(hidAxisHandler.ApplyAxisDelta(hidTurntableIdentifier, 0, delta), Is.True);
                Assert.That(hidAxisHandler.FinishPolling(), Is.True);
            });
        }

        private void pressXInputScratch(string stepName)
            => AddStep(stepName, () =>
            {
                bool changed = customXInputButtonHandler != null
                    ? customXInputButtonHandler.TriggerPressed(customXInputButtonIndex)
                    : drawableRuleset.InputManager.TriggerXInputButtonPressed((int)osu.Framework.Input.JoystickButton.GamePadLeftShoulder);

                Assert.That(changed, Is.True);
            });

        private void releaseXInputScratch(string stepName)
            => AddStep(stepName, () =>
            {
                bool changed = customXInputButtonHandler != null
                    ? customXInputButtonHandler.TriggerReleased(customXInputButtonIndex)
                    : drawableRuleset.InputManager.TriggerXInputButtonReleased((int)osu.Framework.Input.JoystickButton.GamePadLeftShoulder);

                Assert.That(changed, Is.True);
            });

        private static OmsBindingTrigger createDefaultMouseScratchTrigger()
            => OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive);

        private static OmsBindingTrigger createDefaultHidScratchTrigger()
            => OmsBindingTrigger.HidAxis(hidTurntableIdentifier, 0, OmsAxisDirection.Positive);

        private static OmsBindingTrigger createInvertedMouseScratchTrigger()
            => OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive, axisInverted: true);

        private static OmsBindingTrigger createInvertedHidScratchTrigger()
            => OmsBindingTrigger.HidAxis(hidTurntableIdentifier, 0, OmsAxisDirection.Positive, axisInverted: true);

        private DrawableBmsHitObject[] getScratchDrawables()
            => drawableRuleset.Playfield.AllHitObjects.OfType<DrawableBmsHitObject>()
                              .Where(hitObject => hitObject.HitObject is BmsHitObject { IsScratch: true } && hitObject.HitObject is not BmsHoldNote)
                              .OrderBy(hitObject => hitObject.HitObject.StartTime)
                              .ToArray();

        private DrawableBmsHoldNote getScratchHoldDrawable()
            => drawableRuleset.Playfield.AllHitObjects.OfType<DrawableBmsHoldNote>()
                              .Single(drawable => drawable.HitObject is BmsHoldNote { IsScratch: true });

        private DrawableBmsHoldNoteHead getScratchHoldHeadDrawable()
            => scratchHoldDrawable.NestedHitObjects.OfType<DrawableBmsHoldNoteHead>().Single();

        private DrawableBmsHoldNoteTail getScratchHoldTailDrawable()
            => scratchHoldDrawable.NestedHitObjects.OfType<DrawableBmsHoldNoteTail>().Single();

        private BmsBeatmap createScratchStreamBeatmap(string scratchLaneData = "DDDD", int rank = 2)
            => createBeatmapFromText($@"
#TITLE Scratch Stream Stub
#BPM 120
#RANK {rank}
#00101:AA00
#WAVAA bgm.wav
#WAVDD scratch.wav
    #00116:{scratchLaneData}
", "scratch-stream-stub.bme");

        private BmsBeatmap createSecondScratchStreamBeatmap(string scratchLaneData = "DDDD", int rank = 2)
            => createBeatmapFromText($@"
#TITLE Second Scratch Stream Stub
#BPM 120
#RANK {rank}
#00101:AA00
#WAVAA bgm.wav
#WAVDD scratch.wav
    #00126:{scratchLaneData}
", "second-scratch-stream-stub.bms");

        private BmsBeatmap createScratchHoldBeatmap(int rank = 2)
            => createBeatmapFromText($@"
#TITLE Scratch Hold Stub
#BPM 120
#RANK {rank}
#00101:AA00
#WAVAA bgm.wav
#WAVEE hold/head.ogg
#WAVFF hold/tail.wav
#LNTYPE 1
    #00156:EE00FF00
", "scratch-hold-stub.bme");

        private BmsBeatmap createSecondScratchHoldBeatmap(int rank = 2)
            => createBeatmapFromText($@"
#TITLE Second Scratch Hold Stub
#BPM 120
#RANK {rank}
#00101:AA00
#WAVAA bgm.wav
#WAVEE hold/head.ogg
#WAVFF hold/tail.wav
#LNTYPE 1
    #00166:EE00FF00
", "second-scratch-hold-stub.bms");

        private BmsBeatmap createBeatmapFromText(string text, string path)
        {
            var decodedChart = decoder.DecodeText(text, path);
            return (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decodedChart), new BmsRuleset()).Convert();
        }

        private sealed partial class TestableDrawableBmsRuleset : DrawableBmsRuleset
        {
            public BmsInputManager InputManager => (BmsInputManager)KeyBindingInputManager;

            public TestableDrawableBmsRuleset(BmsRuleset ruleset, IBeatmap beatmap)
                : base(ruleset, beatmap)
            {
            }
        }
    }
}
