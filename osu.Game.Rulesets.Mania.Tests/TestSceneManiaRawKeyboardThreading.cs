// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Database;
using osu.Game.Input;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.Visual;
using osuTK.Input;

namespace osu.Game.Rulesets.Mania.Tests
{
    [HeadlessTest]
    public partial class TestSceneManiaRawKeyboardThreading : OsuManualInputManagerTestScene
    {
        [Cached(typeof(IOmsKeyboardEventSource))]
        private readonly KeyboardSource keyboardSource = new KeyboardSource();

        [Cached]
        private readonly GameplaySkinLayoutRevisionOwner layoutOwner = GameplaySkinLayoutRevisionOwner.CreateCompatibility();

        private ProbedRuleset renderer = null!;
        private ManualClock clock = null!;
        private TimestampedGameplayClock gameplayClock = null!;
        private Score score = null!;
        private DrawableHoldNote holdDrawable = null!;
        private readonly List<ObservedJudgement> judgements = new List<ObservedJudgement>();
        private double rate;
        private double offset;

        protected override bool UseFreshStoragePerRun => true;

        [TestCase(1, 0)]
        [TestCase(1.5, 0)]
        [TestCase(1, 37)]
        [TestCase(1.5, -23)]
        public void TestStalledFrameTapsKeepTheirActualJudgementAndReplayTimes(double gameplayRate, double gameplayOffset)
        {
            arrange(gameplayRate: gameplayRate, gameplayOffset: gameplayOffset);
            AddStep("receive two complete taps while gameplay update is stalled", () =>
            {
                Task.Run(() =>
                {
                    emitAt(1040, true);
                    emitAt(1045, false);
                    emitAt(1080, true);
                    emitAt(1085, false);
                }).GetAwaiter().GetResult();
                advanceTo(1100);
            });
            waitForTime(1100);
            AddStep("both real notes and their recording retain the individual input instants", () =>
            {
                ObservedJudgement[] results = judgements.Where(result => result.Object is Note).ToArray();
                Assert.That(results.Select(result => result.Object.StartTime), Is.EqualTo(new[] { 1040d, 1080d }));
                Assert.That(results.Select(result => result.Type), Is.All.EqualTo(HitResult.Perfect));
                Assert.That(results.Select(result => result.RawTime), Is.EqualTo(new[] { 1040d, 1080d }).Within(0.0001));
                Assert.That(results.Select(result => result.Error), Is.All.EqualTo(0).Within(0.0001));
                Assert.That(pressedTimes(), Is.EqualTo(new[] { 1040d, 1080d }).Within(0.0001));
            });
        }

        [Test]
        public void TestACompleteNativeTapCannotBecomeAHeldLongNote()
        {
            arrange(hold: true);
            AddStep("receive press and release in the same stalled batch", () =>
            {
                emitAt(1040, true);
                emitAt(1045, false);
                advanceTo(1100);
            });
            waitForTime(1100);
            AddAssert("the real hold head was hit and the early release broke its body", () =>
                judgementFor(holdObject.Head).Type == HitResult.Perfect && judgementFor(holdObject.Head).RawTime == 1040
                && !holdDrawable.IsHolding.Value && holdWasBroken
                && !renderer.Input.KeyBindingContainer.PressedActions.Any());
            AddStep("the recording retains both edges", () =>
            {
                ManiaReplayFrame[] frames = score.Replay.Frames.OfType<ManiaReplayFrame>().ToArray();
                Assert.That(frames.Any(frame => frame.Time == 1040 && frame.Actions.Contains(ManiaAction.Key1)), Is.True);
                Assert.That(frames.Any(frame => frame.Time == 1045 && !frame.Actions.Contains(ManiaAction.Key1)), Is.True);
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestACompleteSdlEchoCannotPlayTheNativeTapAgain(bool sdlFirst)
        {
            arrange();
            AddStep("receive matching complete native and SDL taps", () =>
            {
                if (sdlFirst)
                    InputManager.Key(Key.D);
                emitAt(1040, true);
                emitAt(1045, false);
                if (!sdlFirst)
                    InputManager.Key(Key.D);
                advanceTo(1100);
            });
            waitForTime(1100);
            AddStep("only the native tap played and was recorded", () =>
            {
                ManiaHitObject[] notes = renderer.Beatmap.HitObjects.ToArray();
                Assert.That(judgementFor(notes[0]).Type, Is.EqualTo(HitResult.Perfect));
                Assert.That(judgementFor(notes[0]).RawTime, Is.EqualTo(1040).Within(0.0001));
                Assert.That(hasJudgement(notes[1]), Is.False);
                Assert.That(pressedTimes(), Is.EqualTo(new[] { 1040d }).Within(0.0001));
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestNativeKeyboardAndGamepadShareTheFinalRelease(bool nativeReleasedFirst)
        {
            arrange(hold: true, gamepadBinding: true);
            AddStep("hit a real hold through the native keyboard", () =>
            {
                emitAt(1040, true);
                advanceTo(1100);
            });
            waitForTime(1100);
            AddStep("also hold the same action through the ordinary gamepad binding", () => InputManager.PressJoystickButton(JoystickButton.Button1));
            AddWaitStep("process the real gamepad press", 2);
            AddStep("release only one source", () =>
            {
                if (nativeReleasedFirst)
                    emitAt(1120, false);
                else
                    InputManager.ReleaseJoystickButton(JoystickButton.Button1);
                advanceTo(1200);
            });
            waitForTime(1200);
            AddAssert("the real long note stays held by its remaining source", () =>
                judgementFor(holdObject.Head).Type == HitResult.Perfect && holdDrawable.IsHolding.Value
                && !holdWasBroken && renderer.Input.KeyBindingContainer.PressedActions.Contains(ManiaAction.Key1));
            AddStep("reach the real tail", () => advanceTo(1800));
            waitForTime(1800);
            AddStep("release the remaining source at the tail", () =>
            {
                if (nativeReleasedFirst)
                    InputManager.ReleaseJoystickButton(JoystickButton.Button1);
                else
                    emitAt(1800, false);
            });
            AddUntilStep("the real tail resolves and the action finally releases", () => hasJudgement(holdObject.Tail)
                && !renderer.Input.KeyBindingContainer.PressedActions.Any());
            AddAssert("the source handoff did not break or downgrade the tail", () => judgementFor(holdObject.Tail).Type == HitResult.Perfect && !holdWasBroken);
            AddStep("the source handoff never created another attack", () => Assert.That(pressedTimes(), Is.EqualTo(new[] { 1040d }).Within(0.0001)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestTwoPhysicalKeyboardsKeepTheSameKeyHeldUntilTheFinalSourceReleases(bool unplugFirst)
        {
            arrange(hold: true);
            AddStep("hold the same key on two physical keyboards", () =>
            {
                keyboardSource.DeviceState.Apply(1, 0x20, InputKey.D, timestampFor(1040), true);
                keyboardSource.DeviceState.Apply(2, 0x20, InputKey.D, timestampFor(1050), true);
                // Native repeats do not create more physical sources.
                keyboardSource.DeviceState.Apply(1, 0x20, InputKey.D, timestampFor(1060), true);
                advanceTo(1100);
            });
            waitForTime(1100);
            AddStep("release or unplug the first keyboard", () =>
            {
                if (unplugFirst)
                    keyboardSource.DeviceState.RemoveDevice(1, timestampFor(1120));
                else
                    keyboardSource.DeviceState.Apply(1, 0x20, InputKey.D, timestampFor(1120), false);
                advanceTo(1200);
            });
            waitForTime(1200);
            AddAssert("the second physical keyboard still holds the real long note", () => holdDrawable.IsHolding.Value
                && !holdWasBroken && renderer.Input.KeyBindingContainer.PressedActions.Contains(ManiaAction.Key1));
            AddStep("release the final keyboard at the tail", () =>
            {
                keyboardSource.DeviceState.Apply(2, 0x20, InputKey.D, timestampFor(1800), false);
                advanceTo(1850);
            });
            waitForTime(1850);
            AddAssert("only the final physical release resolved the real tail", () => judgementFor(holdObject.Tail).Type == HitResult.Perfect
                && !holdWasBroken && !renderer.Input.KeyBindingContainer.PressedActions.Any());
        }

        [TestCase("focus")]
        [TestCase("seek")]
        [TestCase("replay")]
        public void TestAnEndedInputEpochCannotReplayItsQueuedTap(string boundary)
        {
            arrange();
            AddStep("queue a stale tap then end its ownership or clock epoch", () =>
            {
                emitAt(1040, true);
                emitAt(1045, false);
                switch (boundary)
                {
                    case "focus":
                        keyboardSource.Reset();
                        break;
                    case "seek":
                        keyboardSource.TimestampMilliseconds = receiptTime(1050);
                        gameplayClock.BeginEpoch(1000);
                        break;
                    case "replay":
                        renderer.SetReplayScore(new Score());
                        renderer.SetReplayScore(null!);
                        break;
                }
                emitAt(1080, true);
                emitAt(1085, false);
                advanceTo(1100);
            });
            waitForTime(1100);
            AddStep("only input from the new epoch can judge or be recorded", () =>
            {
                Assert.That(pressedTimes(), Is.EqualTo(new[] { 1080d }).Within(0.0001));
                Assert.That(judgementFor(renderer.Beatmap.HitObjects[0]).RawTime, Is.EqualTo(1080).Within(0.0001));
                Assert.That(judgementFor(renderer.Beatmap.HitObjects[0]).Type, Is.EqualTo(HitResult.Miss));
                Assert.That(judgementFor(renderer.Beatmap.HitObjects[1]).Type, Is.EqualTo(HitResult.Perfect));
            });
        }

        [Test]
        public void TestPauseDropsOldAndPausedTapsButRetainsFreshInputAfterResume()
        {
            arrange();
            AddStep("pause with an old native tap still queued", () =>
            {
                emitAt(1040, true);
                emitAt(1045, false);
                gameplayClock.BeginEpoch(1000, isPaused: true);
            });
            AddWaitStep("process paused outer frames", 3);
            AddStep("receive a tap during pause", () =>
            {
                emitAt(1060, true);
                emitAt(1065, false);
            });
            AddWaitStep("process the paused tap", 2);
            AddAssert("neither tap judged or entered the recording", () => pressedTimes().Length == 0 && judgements.Count == 0);
            AddStep("resume on a new timeline epoch", () =>
            {
                keyboardSource.TimestampMilliseconds = receiptTime(1100);
                gameplayClock.BeginEpoch(1000);
            });
            AddStep("receive a fresh tap after resume", () =>
            {
                keyboardSource.EmitAt(receiptTime(1140), InputKey.D, true);
                keyboardSource.EmitAt(receiptTime(1145), InputKey.D, false);
                keyboardSource.TimestampMilliseconds = receiptTime(1160);
                clock.CurrentTime = 1060;
            });
            waitForTime(1060);
            AddStep("the resumed tap uses the resumed clock and actual note", () =>
            {
                ObservedJudgement first = judgementFor(renderer.Beatmap.HitObjects[0]);
                Assert.That(first.Type, Is.EqualTo(HitResult.Perfect));
                Assert.That(first.RawTime, Is.EqualTo(1040).Within(0.0001));
                Assert.That(pressedTimes(), Is.EqualTo(new[] { 1040d }).Within(0.0001));
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestUnavailableNativeSourceOrUnsupportedChordRetainsFrameworkKeyboard(bool unsupportedChord)
        {
            arrange(rawAvailable: unsupportedChord, unsupportedChord: unsupportedChord);
            AddStep("bring the ordinary gameplay clock to the note", () => advanceTo(1040));
            waitForTime(1040);
            AddStep("play through the ordinary keyboard stream", () =>
            {
                if (unsupportedChord)
                    InputManager.PressKey(Key.LControl);
                InputManager.Key(Key.D);
                if (unsupportedChord)
                    InputManager.ReleaseKey(Key.LControl);
            });
            AddUntilStep("the actual note accepts the fallback tap", () => hasJudgement(renderer.Beatmap.HitObjects[0]));
            AddStep("fallback keeps the real judgment and one recording", () =>
            {
                ObservedJudgement first = judgementFor(renderer.Beatmap.HitObjects[0]);
                Assert.That(first.Type, Is.EqualTo(HitResult.Perfect));
                Assert.That(first.RawTime, Is.EqualTo(1040).Within(0.0001));
                Assert.That(pressedTimes(), Is.EqualTo(new[] { 1040d }).Within(0.0001));
            });
        }

        [Test]
        public void TestFrameworkKeyCanReleaseAfterNativeOwnershipBeginsAndTheNextNativeTapStillHits()
        {
            int nativeTransitionsBeforePress = 0;
            arrange(rawAvailable: false);
            AddStep("bring the framework keyboard to the first note", () => advanceTo(1040));
            waitForTime(1040);
            AddStep("hold D through SDL while the native physical make is suppressed", () =>
            {
                nativeTransitionsBeforePress = keyboardSource.DeviceStateTransitionCount;
                keyboardSource.DeviceState.Apply(1, 0x20, InputKey.D, timestampFor(1040), true, suppressPress: true);
                InputManager.PressKey(Key.D);
            });
            AddUntilStep("the first real note was hit by the fallback press", () => hasJudgement(renderer.Beatmap.HitObjects[0])
                && renderer.Input.KeyBindingContainer.PressedActions.Contains(ManiaAction.Key1));
            AddAssert("the fallback hit was perfect and its suppressed make did not enter the native queue", () =>
                judgementFor(renderer.Beatmap.HitObjects[0]).Type == HitResult.Perfect
                && keyboardSource.DeviceStateTransitionCount == nativeTransitionsBeforePress);
            AddStep("enter native gameplay then release the already held framework key", () =>
            {
                keyboardSource.Available = true;
                keyboardSource.DeviceState.Apply(1, 0x20, InputKey.D, timestampFor(1060), false);
                InputManager.ReleaseKey(Key.D);
                advanceTo(1060);
            });
            waitForTime(1060);
            AddAssert("the old physical break stayed suppressed instead of pretending to be a native release", () =>
                keyboardSource.DeviceStateTransitionCount == nativeTransitionsBeforePress);
            AddUntilStep("the earlier framework press releases after native ownership changes", () =>
                !renderer.Input.KeyBindingContainer.PressedActions.Any());
            AddStep("play the next real note with a fresh physical native tap", () =>
            {
                keyboardSource.DeviceState.Apply(1, 0x20, InputKey.D, timestampFor(1080), true);
                keyboardSource.DeviceState.Apply(1, 0x20, InputKey.D, timestampFor(1085), false);
                advanceTo(1100);
            });
            waitForTime(1100);
            AddStep("the handover preserves the next judgment and exactly two recorded attacks", () =>
            {
                ObservedJudgement next = judgementFor(renderer.Beatmap.HitObjects[1]);
                Assert.That(next.Type, Is.EqualTo(HitResult.Perfect));
                Assert.That(next.RawTime, Is.EqualTo(1080).Within(0.0001));
                Assert.That(pressedTimes(), Is.EqualTo(new[] { 1040d, 1080d }).Within(0.0001));
                Assert.That(keyboardSource.DeviceStateTransitionCount, Is.EqualTo(nativeTransitionsBeforePress + 2));
                Assert.That(renderer.Input.KeyBindingContainer.PressedActions, Is.Empty);
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestFrameworkAndNativeSameKeyHandoverKeepsItsFinalKeyboardAndGamepadReferences(bool frameworkReleasedFirst)
        {
            int nativeTransitionsBeforePress = 0;
            arrange(hold: true, gamepadBinding: true, rawAvailable: false);
            AddStep("bring the fallback keyboard to the hold head", () => advanceTo(1040));
            waitForTime(1040);
            AddStep("start the real hold with an SDL key whose raw make is suppressed", () =>
            {
                nativeTransitionsBeforePress = keyboardSource.DeviceStateTransitionCount;
                keyboardSource.DeviceState.Apply(1, 0x20, InputKey.D, timestampFor(1040), true, suppressPress: true);
                InputManager.PressKey(Key.D);
            });
            AddUntilStep("the real fallback head is held", () => hasJudgement(holdObject.Head) && holdDrawable.IsHolding.Value);
            AddAssert("the actual head was hit perfectly", () => judgementFor(holdObject.Head).Type == HitResult.Perfect);
            AddStep("enable native ownership and hold the same key on another physical keyboard", () =>
            {
                keyboardSource.Available = true;
                keyboardSource.DeviceState.Apply(2, 0x20, InputKey.D, timestampFor(1120), true);
                advanceTo(1200);
            });
            waitForTime(1200);
            AddStep("also hold the action with the real framework gamepad binding", () => InputManager.PressJoystickButton(JoystickButton.Button1));
            AddWaitStep("process the separate gamepad source", 2);
            AddStep("release just one keyboard source", () =>
            {
                if (frameworkReleasedFirst)
                {
                    keyboardSource.DeviceState.Apply(1, 0x20, InputKey.D, timestampFor(1300), false);
                    InputManager.ReleaseKey(Key.D);
                }
                else
                    keyboardSource.DeviceState.Apply(2, 0x20, InputKey.D, timestampFor(1300), false);

                advanceTo(1400);
            });
            waitForTime(1400);
            AddAssert("the remaining keyboard and gamepad still own the real hold", () => holdDrawable.IsHolding.Value
                && !holdWasBroken && renderer.Input.KeyBindingContainer.PressedActions.Contains(ManiaAction.Key1));
            AddStep("release the gamepad before the final keyboard", () =>
            {
                InputManager.ReleaseJoystickButton(JoystickButton.Button1);
                advanceTo(1500);
            });
            waitForTime(1500);
            AddAssert("the final keyboard alone keeps the real hold active", () => holdDrawable.IsHolding.Value
                && !holdWasBroken && renderer.Input.KeyBindingContainer.PressedActions.Contains(ManiaAction.Key1));
            AddStep("reach the actual hold tail", () => advanceTo(1800));
            waitForTime(1800);
            AddStep("release the last keyboard at the real tail", () =>
            {
                if (frameworkReleasedFirst)
                    keyboardSource.DeviceState.Apply(2, 0x20, InputKey.D, timestampFor(1800), false);
                else
                {
                    keyboardSource.DeviceState.Apply(1, 0x20, InputKey.D, timestampFor(1800), false);
                    InputManager.ReleaseKey(Key.D);
                }

                advanceTo(1850);
            });
            waitForTime(1850);
            AddStep("all three sources resolve one perfect tail without an extra attack", () =>
            {
                ObservedJudgement tail = judgementFor(holdObject.Tail);
                Assert.That(tail.Type, Is.EqualTo(HitResult.Perfect));
                Assert.That(tail.RawTime, Is.EqualTo(1800).Within(0.0001));
                Assert.That(holdWasBroken, Is.False);
                Assert.That(pressedTimes(), Is.EqualTo(new[] { 1040d }).Within(0.0001));
                Assert.That(keyboardSource.DeviceStateTransitionCount, Is.EqualTo(nativeTransitionsBeforePress + 2));
                Assert.That(renderer.Input.KeyBindingContainer.PressedActions, Is.Empty);
            });
        }

        private HoldNote holdObject => (HoldNote)renderer.Beatmap.HitObjects.Single();

        private bool holdWasBroken => judgements.Any(result => ReferenceEquals(result.Object, holdObject.Body) && result.Type == HitResult.ComboBreak);

        private bool hasJudgement(ManiaHitObject hitObject) => judgements.Any(result => ReferenceEquals(result.Object, hitObject));

        private ObservedJudgement judgementFor(ManiaHitObject hitObject) => judgements.Single(result => ReferenceEquals(result.Object, hitObject));

        private readonly record struct ObservedJudgement(ManiaHitObject Object, HitResult Type, double RawTime, double Error);

        private double[] pressedTimes()
        {
            var times = new List<double>();
            bool previous = false;
            foreach (ManiaReplayFrame frame in score.Replay.Frames.OfType<ManiaReplayFrame>())
            {
                bool pressed = frame.Actions.Contains(ManiaAction.Key1);
                if (pressed && !previous)
                    times.Add(frame.Time);
                previous = pressed;
            }

            return times.ToArray();
        }

        private double receiptTime(double gameplayTime) => (gameplayTime - offset) / rate;

        private long timestampFor(double gameplayTime) => toTimestamp(receiptTime(gameplayTime));

        private void emitAt(double gameplayTime, bool pressed) => keyboardSource.EmitAt(receiptTime(gameplayTime), InputKey.D, pressed);

        private void advanceTo(double gameplayTime)
        {
            keyboardSource.TimestampMilliseconds = receiptTime(gameplayTime);
            clock.CurrentTime = gameplayTime;
        }

        private void waitForTime(double gameplayTime)
            => AddUntilStep($"gameplay catches up to {gameplayTime}", () => Math.Abs(renderer.FrameStableClock.CurrentTime - gameplayTime) < 0.0001);

        private void arrange(bool hold = false, bool gamepadBinding = false, bool rawAvailable = true, bool unsupportedChord = false,
                             double gameplayRate = 1, double gameplayOffset = 0)
        {
            int registrationsBeforeLoad = 0;
            AddStep("load the actual mania keyboard and note tree", () =>
            {
                rate = gameplayRate;
                offset = gameplayOffset;
                judgements.Clear();
                keyboardSource.Available = rawAvailable;
                keyboardSource.Unsupported.Clear();
                keyboardSource.Reset();
                if (unsupportedChord)
                    keyboardSource.Unsupported.Add(InputKey.D);
                registrationsBeforeLoad = keyboardSource.RegisteredCount;
                keyboardSource.TimestampMilliseconds = receiptTime(1000);
                var ruleset = new ManiaRuleset();
                var beatmap = new ManiaBeatmap(new StageDefinition(4))
                {
                    Difficulty = { CircleSize = 4, OverallDifficulty = 5, DrainRate = 5 },
                    BeatmapInfo = { Ruleset = ruleset.RulesetInfo },
                };
                if (hold)
                    beatmap.HitObjects.Add(new HoldNote { StartTime = 1040, EndTime = 1800, Column = 0 });
                else
                {
                    beatmap.HitObjects.Add(new Note { StartTime = 1040, Column = 0 });
                    beatmap.HitObjects.Add(new Note { StartTime = 1080, Column = 0 });
                }
                foreach (ManiaHitObject hitObject in beatmap.HitObjects)
                    hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

                Realm.Write(realm =>
                {
                    foreach (RealmKeyBinding binding in realm.All<RealmKeyBinding>().Where(binding => binding.RulesetName == ManiaRuleset.SHORT_NAME && binding.Variant == 4).ToArray())
                        realm.Remove(binding);
                    realm.Add(new RealmKeyBinding(ManiaAction.Key1,
                        unsupportedChord ? new KeyCombination(InputKey.Control, InputKey.D) : new KeyCombination(InputKey.D), ManiaRuleset.SHORT_NAME, 4));
                    if (gamepadBinding)
                        realm.Add(new RealmKeyBinding(ManiaAction.Key1, new KeyCombination(InputKey.Joystick1), ManiaRuleset.SHORT_NAME, 4));
                });
                renderer = new ProbedRuleset(ruleset, beatmap);
                // Pooled drawables can leave the tree and reset their Result immediately after a hit.
                // Snapshot the real engine events against the original chart objects before any input runs.
                renderer.NewResult += result => judgements.Add(new ObservedJudgement(
                    (ManiaHitObject)result.HitObject, result.Type, result.RawTime!.Value, result.TimeOffset));
                clock = new ManualClock { IsRunning = true, Rate = rate };
                gameplayClock = new TimestampedGameplayClock(clock, keyboardSource) { Child = renderer };
                Child = new DependencyProvidingContainer
                {
                    CachedDependencies = new[] { (typeof(RealmAccess), (object)Realm) },
                    RelativeSizeAxes = Axes.Both,
                    Clock = new FramedClock(clock),
                    Child = gameplayClock,
                };
            });
            AddUntilStep("the real mania input and notes are loaded", () => renderer.IsLoaded && renderer.Input.IsLoaded
                && renderer.ChildrenOfType<DrawableNote>().Any());
            AddStep("start the real recording and establish the clock anchor", () =>
            {
                if (hold)
                    holdDrawable = renderer.Playfield.Stages.Single().Columns[0].HitObjectContainer.AliveObjects.OfType<DrawableHoldNote>().Single();
                score = new Score();
                renderer.SetRecordTarget(score);
                advanceTo(1000);
            });
            waitForTime(1000);
            AddAssert("the primary gameplay input owns the native source instead of the resume overlay", () =>
                keyboardSource.IsActiveFor(renderer.Input) == rawAvailable && keyboardSource.RegisteredCount == registrationsBeforeLoad + 1);
        }

        private sealed class KeyboardSource : IOmsKeyboardEventSource
        {
            private IOmsKeyboardEventSink? sink;
            public bool Available { get; set; }
            public double TimestampMilliseconds { get; set; }
            public int RegisteredCount { get; private set; }
            public int DeviceStateTransitionCount { get; private set; }
            public HashSet<InputKey> Unsupported { get; } = new HashSet<InputKey>();
            public NativeKeyboardDeviceState DeviceState { get; }

            public KeyboardSource()
            {
                DeviceState = new NativeKeyboardDeviceState((key, timestamp, pressed) =>
                {
                    DeviceStateTransitionCount++;
                    bool accepted = pressed ? sink!.HandleRawKeyPressed(key, timestamp) : sink!.HandleRawKeyReleased(key, timestamp);
                    Assert.That(accepted, Is.True);
                });
            }

            public void RegisterSink(IOmsKeyboardEventSink target)
            {
                sink = target;
                RegisteredCount++;
            }

            public void UnregisterSink(IOmsKeyboardEventSink target)
            {
                if (ReferenceEquals(sink, target))
                    sink = null;
            }

            public bool IsActiveFor(IOmsKeyboardEventSink target) => Available && ReferenceEquals(sink, target);

            public bool SupportsKey(InputKey key) => !Unsupported.Contains(key);

            public void Reset()
            {
                DeviceState.Clear();
                sink?.ResetRawKeyboardState();
            }

            public void EmitAt(double milliseconds, InputKey key, bool pressed)
            {
                bool accepted = pressed ? sink!.HandleRawKeyPressed(key, toTimestamp(milliseconds)) : sink!.HandleRawKeyReleased(key, toTimestamp(milliseconds));
                Assert.That(accepted, Is.True);
            }
        }

        private static long toTimestamp(double milliseconds) => (long)Math.Round(milliseconds * Stopwatch.Frequency / 1000);

        [Cached(typeof(IGameplayClock))]
        private sealed partial class TimestampedGameplayClock : Container, IGameplayClock, IGameplayInputTimeSource
        {
            private readonly ManualClock source;
            private readonly KeyboardSource keyboard;
            private readonly BindableBool paused = new BindableBool();
            private long epoch;
            private long epochStartTimestamp;

            public TimestampedGameplayClock(ManualClock source, KeyboardSource keyboard)
            {
                this.source = source;
                this.keyboard = keyboard;
                RelativeSizeAxes = Axes.Both;
            }

            public GameplayInputTime? InputTime { get; private set; }
            public IBindable<bool> IsPaused => paused;
            public bool IsRewinding => Clock.ElapsedFrameTime < 0;
            public double CurrentTime => source.CurrentTime;
            public double Rate => source.Rate;
            public bool IsRunning => source.IsRunning;
            public double ElapsedFrameTime => Clock.ElapsedFrameTime;
            public double FramesPerSecond => Clock.FramesPerSecond;
            public double StartTime => 0;
            public double GameplayStartTime => double.MinValue;
            public IAdjustableAudioComponent AdjustmentsFromMods { get; } = new AudioAdjustments();

            public void ProcessFrame() { }

            public void BeginEpoch(double time, bool isPaused = false)
            {
                epoch++;
                epochStartTimestamp = toTimestamp(keyboard.TimestampMilliseconds);
                source.CurrentTime = time;
                source.IsRunning = !isPaused;
                paused.Value = isPaused;
            }

            protected override void Update()
            {
                base.Update();
                InputTime = new GameplayInputTime(CurrentTime, toTimestamp(keyboard.TimestampMilliseconds), Rate, IsRunning, epoch, epochStartTimestamp);
            }
        }

        private sealed partial class ProbedRuleset : DrawableManiaRuleset
        {
            public ManiaInputManager Input => (ManiaInputManager)KeyBindingInputManager;

            public ProbedRuleset(ManiaRuleset ruleset, ManiaBeatmap beatmap)
                : base(ruleset, beatmap)
            {
            }
        }
    }
}
