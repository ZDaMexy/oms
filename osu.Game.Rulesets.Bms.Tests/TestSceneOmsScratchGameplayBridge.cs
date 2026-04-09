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

        private void setupScene(BmsBeatmap beatmap)
        {
            AddStep("setup scratch bridge scene", () =>
            {
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
                    new[] { new OmsBinding(OmsAction.Key1P_Scratch, OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive)) },
                    action => drawableRuleset.InputManager.TriggerOmsActionPressed(action),
                    action => drawableRuleset.InputManager.TriggerOmsActionReleased(action));

                hidAxisHandler = new OmsHidAxisInputHandler(
                    new[] { new OmsBinding(OmsAction.Key1P_Scratch, OmsBindingTrigger.HidAxis(hidTurntableIdentifier, 0, OmsAxisDirection.Positive)) },
                    action => drawableRuleset.InputManager.TriggerOmsActionPressed(action),
                    action => drawableRuleset.InputManager.TriggerOmsActionReleased(action));
            });

            AddUntilStep("scratch drawables loaded", () => getScratchDrawables().Length == 2);
            AddStep("cache scratch drawables", () => scratchDrawables = getScratchDrawables());
        }

        private void setupScratchHoldScene(BmsBeatmap beatmap)
        {
            AddStep("setup scratch hold bridge scene", () =>
            {
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
                    new[] { new OmsBinding(OmsAction.Key1P_Scratch, OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive)) },
                    action => drawableRuleset.InputManager.TriggerOmsActionPressed(action),
                    action => drawableRuleset.InputManager.TriggerOmsActionReleased(action));

                hidAxisHandler = new OmsHidAxisInputHandler(
                    new[] { new OmsBinding(OmsAction.Key1P_Scratch, OmsBindingTrigger.HidAxis(hidTurntableIdentifier, 0, OmsAxisDirection.Positive)) },
                    action => drawableRuleset.InputManager.TriggerOmsActionPressed(action),
                    action => drawableRuleset.InputManager.TriggerOmsActionReleased(action));
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
            => AddStep(stepName, () => Assert.That(drawableRuleset.InputManager.TriggerXInputButtonPressed((int)osu.Framework.Input.JoystickButton.GamePadLeftShoulder), Is.True));

        private void releaseXInputScratch(string stepName)
            => AddStep(stepName, () => Assert.That(drawableRuleset.InputManager.TriggerXInputButtonReleased((int)osu.Framework.Input.JoystickButton.GamePadLeftShoulder), Is.True));

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
