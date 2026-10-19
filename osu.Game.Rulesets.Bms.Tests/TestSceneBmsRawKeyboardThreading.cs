// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using oms.Input;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;
using osuTK.Input;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    public partial class TestSceneBmsRawKeyboardThreading : OsuManualInputManagerTestScene
    {
        private ManualClock clock = null!;
        private ProbedRuleset renderer = null!;
        private DrawableBmsHitObject note = null!;
        private TimestampedGameplayClock? timestampedClock;

        [Cached(typeof(IOmsKeyboardEventSource))]
        private readonly KeyboardSource keyboardSource = new KeyboardSource();

        protected override bool UseFreshStoragePerRun => true;

        [Test]
        public void TestWorkerInputIsJudgedOnTheFirstGameplayUpdate()
        {
            arrange();
            Exception? producerError = null;
            bool accepted = false;
            bool deliveredOnFirstUpdate = false;
            bool consumedOnProducer = false;

            AddStep("deliver a native keyboard transition from another thread", () =>
            {
                Task.Run(() =>
                {
                    try
                    {
                        accepted = ((IOmsKeyboardEventSink)renderer.Input).HandleRawKeyPressed(InputKey.Z, Stopwatch.GetTimestamp());
                    }
                    catch (Exception error)
                    {
                        producerError = error;
                    }
                }).GetAwaiter().GetResult();

                consumedOnProducer = note.Judged;
                if (producerError != null)
                    TestContext.Progress.WriteLine(producerError);
                renderer.Input.AfterNextUpdate = () => deliveredOnFirstUpdate = note.Result.Type == HitResult.Perfect;
            });
            AddAssert("the window thread never mutates a drawable or judges a note", () =>
                producerError == null && accepted && !consumedOnProducer);
            AddAssert("the very next input update performs the judgement", () => deliveredOnFirstUpdate);
        }

        [TestCase(1)]
        [TestCase(1.5)]
        public void TestTwoNativeTapsDuringAStalledFrameKeepTheirOwnJudgementAndReplayTimes(double rate)
        {
            arrange(rawAvailable: true, burstTimeline: true, gameplayRate: rate);
            var results = new List<(double ObjectTime, HitResult Type, double RawTime, double Error)>();
            var score = new Score();

            AddStep("record the actual loaded gameplay results and input", () =>
            {
                renderer.NewResult += result =>
                {
                    if (result.HitObject is BmsHitObject)
                    {
                        Assert.That(result.RawTime, Is.Not.Null);
                        results.Add((result.HitObject.StartTime, result.Type, result.RawTime!.Value, result.TimeOffset));
                    }
                };
                renderer.SetRecordTarget(score);
            });
            AddStep("receive two complete native taps while gameplay update is stalled", () =>
            {
                Assert.That(renderer.FrameStableClock.CurrentTime, Is.EqualTo(1000));
                Task.Run(() =>
                {
                    keyboardSource.EmitAt(1040 / rate, InputKey.Z, true);
                    keyboardSource.EmitAt(1045 / rate, InputKey.Z, false);
                    keyboardSource.EmitAt(1080 / rate, InputKey.Z, true);
                    keyboardSource.EmitAt(1085 / rate, InputKey.Z, false);
                }).GetAwaiter().GetResult();
                keyboardSource.TimestampMilliseconds = 1100 / rate;
                clock.CurrentTime = 1100;
            });
            AddUntilStep("the existing frame-stable clock catches up", () => renderer.FrameStableClock.CurrentTime == 1100);
            AddStep("both notes retain their independently specified event times", () =>
            {
                Assert.That(results.Select(result => result.ObjectTime), Is.EqualTo(new[] { 1040d, 1080d }));
                Assert.That(results.Select(result => result.Type), Is.EqualTo(new[] { HitResult.Perfect, HitResult.Perfect }));
                Assert.That(results.Select(result => result.RawTime), Is.EqualTo(new[] { 1040d, 1080d }).Within(0.0001));
                Assert.That(results.Select(result => result.Error), Is.All.EqualTo(0).Within(0.0001));
            });
            AddStep("recorded input keeps both tap instants instead of the first catch-up instant", () =>
            {
                double[] pressedTimes = score.Replay.Frames.OfType<BmsReplayFrame>()
                    .Where(frame => frame.LaneActions.Contains(BmsAction.Key1)).Select(frame => frame.Time).Distinct().ToArray();
                Assert.That(pressedTimes, Is.EqualTo(new[] { 1040d, 1080d }).Within(0.0001));
                renderer.SetRecordTarget(null!);
            });
        }

        [Test]
        public void TestSameTimeNativeChordEdgesKeepTheirReceiptOrder()
        {
            arrange(rawAvailable: true, virtualChord: true, burstTimeline: true);
            var edges = new List<(bool Pressed, double Time)>();
            AddStep("observe the actual chord action", () =>
            {
                renderer.Input.Router.ActionPressed += action => { if (action == OmsAction.Key1P_1) edges.Add((true, renderer.FrameStableClock.CurrentTime)); };
                renderer.Input.Router.ActionReleased += action => { if (action == OmsAction.Key1P_1) edges.Add((false, renderer.FrameStableClock.CurrentTime)); };
            });
            AddStep("receive a complete modifier handover at one instant", () =>
            {
                Task.Run(() =>
                {
                    keyboardSource.EmitAt(1040, InputKey.LControl, true);
                    keyboardSource.EmitAt(1040, InputKey.Z, true);
                    keyboardSource.EmitAt(1040, InputKey.RControl, true);
                    keyboardSource.EmitAt(1040, InputKey.LControl, false);
                    keyboardSource.EmitAt(1040, InputKey.Z, false);
                    keyboardSource.EmitAt(1040, InputKey.RControl, false);
                }).GetAwaiter().GetResult();
                keyboardSource.TimestampMilliseconds = 1050;
                clock.CurrentTime = 1050;
            });
            AddUntilStep("the frame-stable clock catches up", () => renderer.FrameStableClock.CurrentTime == 1050);
            AddAssert("one complete action retains the modifier ordering", () => edges.SequenceEqual(new[] { (true, 1040d), (false, 1040d) }));
            AddAssert("the note sees the same exact instant", () => note.Result.Type == HitResult.Perfect && note.Result.TimeOffset == 0);
            AddAssert("no physical or virtual side remains held", () => !renderer.Input.KeyBindingContainer.PressedActions.Contains(BmsAction.Key1));
        }

        [Test]
        public void TestFutureNativeEventsWaitAndLateEventsDoNotRewindGameplay()
        {
            arrange(rawAvailable: true, burstTimeline: true);
            var edges = new List<(bool Pressed, double Time)>();
            AddStep("queue input beyond the latest authority clock sample", () =>
            {
                renderer.Input.Router.ActionPressed += action => { if (action == OmsAction.UI_LaneCoverFocus) edges.Add((true, renderer.FrameStableClock.CurrentTime)); };
                renderer.Input.Router.ActionReleased += action => { if (action == OmsAction.UI_LaneCoverFocus) edges.Add((false, renderer.FrameStableClock.CurrentTime)); };
                keyboardSource.EmitAt(1040, InputKey.W, true);
                keyboardSource.EmitAt(1045, InputKey.W, false);
            });
            AddWaitStep("run unchanged gameplay frames", 3);
            AddAssert("future input cannot advance the gameplay clock", () => edges.Count == 0 && renderer.FrameStableClock.CurrentTime == 1000);
            AddStep("publish the next authoritative clock position", () =>
            {
                keyboardSource.TimestampMilliseconds = 1050;
                clock.CurrentTime = 1050;
            });
            AddUntilStep("both future edges are now delivered", () => edges.Count == 2 && renderer.FrameStableClock.CurrentTime == 1050);
            AddAssert("the delivered edges retain their actual instants", () => edges.SequenceEqual(new[] { (true, 1040d), (false, 1045d) }));
            AddStep("receive a delayed callback for an already published interval", () =>
            {
                keyboardSource.EmitAt(1040, InputKey.Z, true);
                keyboardSource.EmitAt(1045, InputKey.Z, false);
            });
            AddUntilStep("the delayed note is judged", () => note.Judged);
            AddAssert("late input uses the current timeline without changing old actions", () =>
                note.Result.RawTime == 1050 && note.Result.TimeOffset == 10 && renderer.FrameStableClock.CurrentTime == 1050
                && edges.SequenceEqual(new[] { (true, 1040d), (false, 1045d) }));
        }

        [Test]
        public void TestNativeResetInvalidatesTheUndeliveredPartOfAPreparedBatch()
        {
            arrange(rawAvailable: true, burstTimeline: true);
            int notesJudged = 0;
            bool reset = false;
            AddStep("reset from the first action after its batch has been prepared", () =>
            {
                renderer.NewResult += result => notesJudged += result.HitObject is BmsHitObject ? 1 : 0;
                renderer.Input.Router.ActionPressed += action =>
                {
                    if (action != OmsAction.Key1P_1 || reset)
                        return;

                    reset = true;
                    ((IOmsKeyboardEventSink)renderer.Input).ResetRawKeyboardState();
                };
                keyboardSource.EmitAt(1040, InputKey.Z, true);
                keyboardSource.EmitAt(1045, InputKey.Z, false);
                keyboardSource.EmitAt(1080, InputKey.Z, true);
                keyboardSource.EmitAt(1085, InputKey.Z, false);
                keyboardSource.TimestampMilliseconds = 1100;
                clock.CurrentTime = 1100;
            });
            AddUntilStep("gameplay catches up and releases the reset keyboard", () => renderer.FrameStableClock.CurrentTime == 1100
                && !renderer.Input.KeyBindingContainer.PressedActions.Contains(BmsAction.Key1));
            AddAssert("only the already dispatched note can receive a result", () => reset && notesJudged == 1 && note.Result.RawTime == 1040);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestClockEpochDropsStaleRawInputAndRetainsNewInput(bool seekBackwards)
        {
            arrange(rawAvailable: true, burstTimeline: true);
            var edges = new List<(bool Pressed, double Time)>();
            double destination = seekBackwards ? 900 : 2000;
            AddStep("cross a clock seek/reset epoch with old input still queued", () =>
            {
                renderer.Input.Router.ActionPressed += action => { if (action == OmsAction.UI_LaneCoverFocus) edges.Add((true, renderer.FrameStableClock.CurrentTime)); };
                renderer.Input.Router.ActionReleased += action => { if (action == OmsAction.UI_LaneCoverFocus) edges.Add((false, renderer.FrameStableClock.CurrentTime)); };
                keyboardSource.EmitAt(1040, InputKey.W, true);
                keyboardSource.EmitAt(1045, InputKey.W, false);
                keyboardSource.TimestampMilliseconds = 2000;
                timestampedClock!.BeginEpoch(destination);
                keyboardSource.EmitAt(2010, InputKey.W, true);
                keyboardSource.EmitAt(2015, InputKey.W, false);
                keyboardSource.TimestampMilliseconds = 2020;
                clock.CurrentTime = destination + 20;
            });
            AddUntilStep("the seek and the new input have completed", () => renderer.FrameStableClock.CurrentTime == destination + 20 && edges.Count == 2);
            AddStep("stale input never reaches the new timeline", () =>
            {
                double pressTime = seekBackwards ? destination + 20 : destination + 10;
                double releaseTime = seekBackwards ? destination + 20 : destination + 15;
                Assert.That(edges, Is.EqualTo(new[] { (true, pressTime), (false, releaseTime) }));
                Assert.That(renderer.Input.LaneCoverFocusPressed.Value, Is.False);
            });
        }

        [Test]
        public void TestPauseDiscardsPendingNativeInputBeforeResume()
        {
            arrange(rawAvailable: true, burstTimeline: true);
            var edges = new List<(bool Pressed, double Time)>();
            AddStep("pause with future native input pending", () =>
            {
                renderer.Input.Router.ActionPressed += action => { if (action == OmsAction.UI_LaneCoverFocus) edges.Add((true, renderer.FrameStableClock.CurrentTime)); };
                renderer.Input.Router.ActionReleased += action => { if (action == OmsAction.UI_LaneCoverFocus) edges.Add((false, renderer.FrameStableClock.CurrentTime)); };
                keyboardSource.EmitAt(1040, InputKey.W, true);
                timestampedClock!.BeginEpoch(1000, isPaused: true);
            });
            AddWaitStep("process paused outer frames", 3);
            AddAssert("paused input cannot judge or change a gameplay action", () => edges.Count == 0 && !note.Judged && renderer.FrameStableClock.CurrentTime == 1000);
            AddStep("resume and receive a fresh complete tap", () =>
            {
                keyboardSource.TimestampMilliseconds = 1100;
                timestampedClock!.BeginEpoch(1000);
                keyboardSource.EmitAt(1110, InputKey.W, true);
                keyboardSource.EmitAt(1115, InputKey.W, false);
                keyboardSource.TimestampMilliseconds = 1120;
                clock.CurrentTime = 1020;
            });
            AddUntilStep("fresh input is delivered after resume", () => edges.Count == 2 && renderer.FrameStableClock.CurrentTime == 1020);
            AddAssert("resume retains only the fresh action", () => edges.SequenceEqual(new[] { (true, 1010d), (false, 1015d) }));
        }

        [TestCase(1, 80)]
        [TestCase(1.5, -80)]
        public void TestNativeJudgementUsesTheAlreadyOffsetAdjustedGameplayClock(double rate, double offset)
        {
            arrange(rawAvailable: true, burstTimeline: true, gameplayRate: rate, gameplayOffset: offset);
            AddStep("receive a tap at the note's fixed gameplay instant", () =>
            {
                keyboardSource.EmitAt((1040 - offset) / rate, InputKey.Z, true);
                keyboardSource.EmitAt((1045 - offset) / rate, InputKey.Z, false);
                keyboardSource.TimestampMilliseconds = (1050 - offset) / rate;
                clock.CurrentTime = 1050;
            });
            AddUntilStep("the offset-adjusted timeline catches up", () => renderer.FrameStableClock.CurrentTime == 1050);
            AddStep("input does not apply the clock offsets a second time", () =>
            {
                Assert.That(note.Result.Type, Is.EqualTo(HitResult.Perfect));
                Assert.That(note.Result.RawTime, Is.EqualTo(1040).Within(0.0001));
                Assert.That(note.Result.TimeOffset, Is.Zero.Within(0.0001));
            });
        }

        [Test]
        public void TestReplayOwnershipDropsPendingNativeInput()
        {
            arrange(rawAvailable: true, burstTimeline: true);
            int presses = 0;
            AddStep("attach a replay with a native press pending", () =>
            {
                renderer.Input.Router.ActionPressed += action => presses += action == OmsAction.UI_LaneCoverFocus ? 1 : 0;
                keyboardSource.EmitAt(1040, InputKey.W, true);
                renderer.SetReplayScore(new Score());
                keyboardSource.TimestampMilliseconds = 1100;
                clock.CurrentTime = 1100;
            });
            AddWaitStep("process the replay-owned outer frames", 3);
            AddAssert("native input cannot leak into replay playback", () => presses == 0);
            AddStep("return input ownership to the player", () => renderer.SetReplayScore(null!));
            AddUntilStep("live gameplay catches up", () => renderer.FrameStableClock.CurrentTime == 1100);
            AddAssert("the previous native epoch remains discarded", () => presses == 0 && !renderer.Input.LaneCoverFocusPressed.Value);
        }

        [Test]
        public void TestReplayAttachAndDetachBetweenOuterFramesInvalidatesNativeInput()
        {
            arrange(rawAvailable: true, burstTimeline: true);
            var edges = new List<(bool Pressed, double Time)>();
            AddStep("attach then detach a replay without a gameplay update in between", () =>
            {
                renderer.Input.Router.ActionPressed += action => { if (action == OmsAction.UI_LaneCoverFocus) edges.Add((true, renderer.FrameStableClock.CurrentTime)); };
                renderer.Input.Router.ActionReleased += action => { if (action == OmsAction.UI_LaneCoverFocus) edges.Add((false, renderer.FrameStableClock.CurrentTime)); };
                keyboardSource.EmitAt(1040, InputKey.W, true);
                keyboardSource.EmitAt(1045, InputKey.W, false);
                renderer.SetReplayScore(new Score());
                renderer.SetReplayScore(null!);
                keyboardSource.TimestampMilliseconds = 1050;
                clock.CurrentTime = 1050;
            });
            AddUntilStep("the next live frame catches up", () => renderer.FrameStableClock.CurrentTime == 1050);
            AddAssert("both ownership changes invalidate the previous native batch", () => edges.Count == 0 && !renderer.Input.LaneCoverFocusPressed.Value);
            AddStep("a fresh tap still belongs to live gameplay", () =>
            {
                keyboardSource.EmitAt(1060, InputKey.W, true);
                keyboardSource.EmitAt(1065, InputKey.W, false);
                keyboardSource.TimestampMilliseconds = 1070;
                clock.CurrentTime = 1070;
            });
            AddUntilStep("fresh input completes", () => edges.Count == 2 && renderer.FrameStableClock.CurrentTime == 1070);
            AddAssert("only the new tap reaches the current owner", () => edges.SequenceEqual(new[] { (true, 1060d), (false, 1065d) }));
        }

        [Test]
        public void TestResetDiscardsPendingInputAndRetainsLaterOrderOnTheUpdateThread()
        {
            arrange();
            var events = new List<(OmsAction Action, bool Pressed, int Thread)>();
            int updateThread = 0;
            bool observedOnProducer = false;

            AddStep("capture keyboard transitions", () =>
            {
                updateThread = Environment.CurrentManagedThreadId;
                renderer.Input.Router.ActionPressed += action => events.Add((action, true, Environment.CurrentManagedThreadId));
                renderer.Input.Router.ActionReleased += action => events.Add((action, false, Environment.CurrentManagedThreadId));
            });
            AddStep("queue press, focus reset and a later key press", () =>
            {
                Task.Run(() =>
                {
                    var sink = (IOmsKeyboardEventSink)renderer.Input;
                    Assert.That(sink.HandleRawKeyPressed(InputKey.W, Stopwatch.GetTimestamp()), Is.True);
                    sink.ResetRawKeyboardState();
                    Assert.That(sink.HandleRawKeyPressed(InputKey.W, Stopwatch.GetTimestamp()), Is.True);
                    Assert.That(sink.HandleRawKeyReleased(InputKey.W, Stopwatch.GetTimestamp()), Is.True);
                    Assert.That(sink.HandleRawKeyPressed(InputKey.None, Stopwatch.GetTimestamp()), Is.False);
                    Assert.That(sink.HandleRawKeyPressed(InputKey.F24, Stopwatch.GetTimestamp()), Is.False);
                }).GetAwaiter().GetResult();
                observedOnProducer = events.Count != 0;
            });
            AddAssert("producer only enqueues", () => !observedOnProducer);
            AddAssert("reset discards stale pending input and retains later transitions", () =>
                events.Select(e => (e.Action, e.Pressed)).SequenceEqual(new[]
                {
                    (OmsAction.UI_LaneCoverFocus, true),
                    (OmsAction.UI_LaneCoverFocus, false),
                }) && !renderer.Input.LaneCoverFocusPressed.Value);
            AddAssert("all action callbacks belong to gameplay update", () => events.All(e => e.Thread == updateThread));
        }

        [Test]
        public void TestRawKeyboardResetPreservesAnotherInputSource()
        {
            arrange();
            AddStep("hold scratch from a different input source", () => renderer.Input.TriggerOmsActionPressed(OmsAction.Key1P_Scratch));
            AddStep("queue the keyboard scratch press and reset", () => Task.Run(() =>
            {
                var sink = (IOmsKeyboardEventSink)renderer.Input;
                sink.HandleRawKeyPressed(InputKey.LShift, Stopwatch.GetTimestamp());
                sink.ResetRawKeyboardState();
            }).GetAwaiter().GetResult());
            AddAssert("the other source still owns its held action", () => renderer.Input.Router.IsPressed(OmsAction.Key1P_Scratch));
            AddStep("release the remaining source", () => renderer.Input.TriggerOmsActionReleased(OmsAction.Key1P_Scratch));
            AddAssert("the final release clears the action", () => !renderer.Input.Router.IsPressed(OmsAction.Key1P_Scratch));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestNativeAndFrameworkCopiesOfACompleteTapAreDeliveredOnce(bool rawFirst)
        {
            arrange(rawAvailable: true);
            int presses = 0;
            int releases = 0;
            AddStep("observe the actual bound action", () =>
            {
                Assert.That(keyboardSource.IsActiveFor(renderer.Input), Is.True);
                renderer.Input.Router.ActionPressed += action => { if (action == OmsAction.UI_LaneCoverFocus) presses++; };
                renderer.Input.Router.ActionReleased += action => { if (action == OmsAction.UI_LaneCoverFocus) releases++; };
            });
            AddStep("deliver both complete copies of one physical key tap", () =>
            {
                if (rawFirst)
                    rawTap();
                InputManager.PressKey(Key.W);
                InputManager.ReleaseKey(Key.W);
                if (!rawFirst)
                    rawTap();
            });
            AddWaitStep("process both input streams", 2);
            AddAssert("one physical tap has exactly one press and one release", () => presses == 1 && releases == 1);

            void rawTap() => Task.Run(() =>
            {
                var sink = (IOmsKeyboardEventSink)renderer.Input;
                sink.HandleRawKeyPressed(InputKey.W, Stopwatch.GetTimestamp());
                sink.HandleRawKeyReleased(InputKey.W, Stopwatch.GetTimestamp());
            }).GetAwaiter().GetResult();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestFrameworkKeyboardRemainsPlayableWhenTheNativeListenerCannotProvideTheKey(bool listenerAvailable)
        {
            arrange(rawAvailable: listenerAvailable, unsupportedChordKey: true);
            AddStep("press the ordinary framework key", () => InputManager.PressKey(Key.Z));
            AddUntilStep("ordinary input judges the note", () => note.Judged);
            AddAssert("fallback uses the same judgement window", () => note.Result.Type == HitResult.Perfect);
            AddStep("release the ordinary key", () => InputManager.ReleaseKey(Key.Z));
        }

        [Test]
        public void TestLoadingANonPrimaryInputManagerCannotTakeTheGameplaySink()
        {
            arrange(rawAvailable: true);
            BmsInputManager extra = null!;
            AddStep("load an unrelated input manager", () => Add(extra = new BmsInputManager(new BmsRuleset().RulesetInfo, 8)));
            AddUntilStep("the unrelated manager is loaded", () => extra.IsLoaded);
            AddAssert("the renderer retains exact input ownership", () => keyboardSource.IsActiveFor(renderer.Input));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestNativePhysicalControlKeysUseThePersistedVirtualChord(bool rightFirst)
        {
            arrange(rawAvailable: true, virtualChord: true);
            InputKey first = rightFirst ? InputKey.RControl : InputKey.LControl;
            InputKey second = rightFirst ? InputKey.LControl : InputKey.RControl;
            AddStep("press the real modifier and chord key", () => Task.Run(() =>
            {
                var sink = (IOmsKeyboardEventSink)renderer.Input;
                Assert.That(sink.HandleRawKeyPressed(first, Stopwatch.GetTimestamp()), Is.True);
                Assert.That(sink.HandleRawKeyPressed(InputKey.Z, Stopwatch.GetTimestamp()), Is.True);
            }).GetAwaiter().GetResult());
            AddUntilStep("the actual chord judges the note", () => note.Judged);
            AddAssert("the chord has a normal Perfect result", () => note.Result.Type == HitResult.Perfect);
            AddStep("transfer the held modifier to its other physical side", () => Task.Run(() =>
            {
                var sink = (IOmsKeyboardEventSink)renderer.Input;
                sink.HandleRawKeyPressed(second, Stopwatch.GetTimestamp());
                sink.HandleRawKeyReleased(first, Stopwatch.GetTimestamp());
            }).GetAwaiter().GetResult());
            AddAssert("the other modifier retains the key action", () => renderer.Input.KeyBindingContainer.PressedActions.Contains(BmsAction.Key1));
            AddStep("release the final physical modifier", () => Task.Run(() =>
            {
                var sink = (IOmsKeyboardEventSink)renderer.Input;
                sink.HandleRawKeyReleased(second, Stopwatch.GetTimestamp());
                sink.HandleRawKeyReleased(InputKey.Z, Stopwatch.GetTimestamp());
            }).GetAwaiter().GetResult());
            AddAssert("the final side releases the gameplay action", () => !renderer.Input.KeyBindingContainer.PressedActions.Contains(BmsAction.Key1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestUnsupportedChordStaysOnOneKeyboardRoute(bool rawFirst)
        {
            arrange(rawAvailable: true, virtualChord: true, unsupportedChordKey: true);
            int presses = 0;
            int releases = 0;
            AddStep("observe the configured chord action", () =>
            {
                renderer.Input.Router.ActionPressed += action => presses += action == OmsAction.Key1P_1 ? 1 : 0;
                renderer.Input.Router.ActionReleased += action => releases += action == OmsAction.Key1P_1 ? 1 : 0;
            });
            AddStep("complete the same physical chord in both input streams", () =>
            {
                if (rawFirst)
                    rawModifierTap();
                InputManager.PressKey(Key.ControlLeft);
                InputManager.PressKey(Key.Z);
                InputManager.ReleaseKey(Key.Z);
                InputManager.ReleaseKey(Key.ControlLeft);
                if (!rawFirst)
                    rawModifierTap();
            });
            AddWaitStep("drain both streams", 4);
            AddAssert("the complete chord fires once without splitting its modifier", () => presses == 1 && releases == 1);
            AddAssert("the normal note receives the same Perfect result", () => note.Result.Type == HitResult.Perfect);

            void rawModifierTap() => Task.Run(() =>
            {
                var sink = (IOmsKeyboardEventSink)renderer.Input;
                sink.HandleRawKeyPressed(InputKey.LControl, Stopwatch.GetTimestamp());
                sink.HandleRawKeyReleased(InputKey.LControl, Stopwatch.GetTimestamp());
            }).GetAwaiter().GetResult();
        }

        private void arrange(bool rawAvailable = false, bool virtualChord = false, bool unsupportedChordKey = false,
                             bool burstTimeline = false, double gameplayRate = 1, double gameplayOffset = 0)
        {
            AddStep("load a real BMS keyboard and note tree", () =>
            {
                keyboardSource.Available = rawAvailable;
                keyboardSource.TimestampMilliseconds = burstTimeline ? (1000 - gameplayOffset) / gameplayRate : 0;
                keyboardSource.Unsupported.Clear();
                if (unsupportedChordKey)
                    keyboardSource.Unsupported.Add(InputKey.Z);
                string chart = burstTimeline
                    ? "#TITLE Native tap timing\n#BPM 3000\n#01311:0101\n"
                    : "#TITLE Raw keyboard\n#BPM 120\n#00111:01\n";
                var decoded = new BmsBeatmapDecoder().DecodeText(chart, "keyboard.bme");
                var beatmap = new BmsBeatmapConverter(new BmsDecodedBeatmap(decoded), new BmsRuleset()).Convert();
                renderer = new ProbedRuleset(beatmap) { RelativeSizeAxes = Axes.Both };
                Realm.Write(realm =>
                {
                    foreach (RealmKeyBinding binding in realm.All<RealmKeyBinding>()
                             .Where(binding => binding.RulesetName == BmsRuleset.SHORT_NAME && binding.Variant == renderer.Variant).ToArray())
                        realm.Remove(binding);
                    if (virtualChord)
                        realm.Add(new RealmKeyBinding(BmsAction.Key1, new KeyCombination(InputKey.Control, InputKey.Z), BmsRuleset.SHORT_NAME, renderer.Variant));
                });
                clock = new ManualClock { IsRunning = true, Rate = gameplayRate };
                Drawable content = renderer;
                timestampedClock = null;
                if (burstTimeline)
                    content = timestampedClock = new TimestampedGameplayClock(clock, keyboardSource) { Child = renderer };

                Child = new DependencyProvidingContainer
                {
                    CachedDependencies = new[] { (typeof(RealmAccess), (object)Realm) },
                    RelativeSizeAxes = Axes.Both,
                    Clock = new FramedClock(clock),
                    Child = content,
                };
            });
            AddUntilStep("note and input manager loaded", () => renderer.IsLoaded && renderer.Input.IsLoaded
                && renderer.ChildrenOfType<DrawableBmsHitObject>().Any());
            AddStep("seek to the note", () =>
            {
                note = renderer.ChildrenOfType<DrawableBmsHitObject>().OrderBy(drawable => drawable.HitObject.StartTime).First();
                clock.CurrentTime = burstTimeline ? 1000 : note.HitObject.StartTime;
            });
            AddUntilStep("the input clock reaches its initial position", () =>
                Precision.AlmostEquals(renderer.FrameStableClock.CurrentTime, burstTimeline ? 1000 : note.HitObject.StartTime, 1));
        }

        private sealed class KeyboardSource : IOmsKeyboardEventSource
        {
            private IOmsKeyboardEventSink? sink;
            public bool Available { get; set; }
            public double TimestampMilliseconds { get; set; }
            public HashSet<InputKey> Unsupported { get; } = new HashSet<InputKey>();
            public void RegisterSink(IOmsKeyboardEventSink sink) => this.sink = sink;
            public void UnregisterSink(IOmsKeyboardEventSink sink)
            {
                if (ReferenceEquals(this.sink, sink))
                    this.sink = null;
            }

            public bool IsActiveFor(IOmsKeyboardEventSink sink) => Available && ReferenceEquals(this.sink, sink);
            public bool SupportsKey(InputKey key) => !Unsupported.Contains(key);

            public void EmitAt(double timestampMilliseconds, InputKey key, bool pressed)
            {
                long timestamp = toTimestamp(timestampMilliseconds);
                bool accepted = pressed ? sink!.HandleRawKeyPressed(key, timestamp) : sink!.HandleRawKeyReleased(key, timestamp);
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

            public void BeginEpoch(double gameplayTime, bool isPaused = false)
            {
                epoch++;
                epochStartTimestamp = toTimestamp(keyboard.TimestampMilliseconds);
                source.CurrentTime = gameplayTime;
                source.IsRunning = !isPaused;
                paused.Value = isPaused;
            }

            protected override void Update()
            {
                base.Update();
                InputTime = new GameplayInputTime(CurrentTime, toTimestamp(keyboard.TimestampMilliseconds), Rate, IsRunning, epoch, epochStartTimestamp);
            }
        }

        private sealed partial class ProbedRuleset : DrawableBmsRuleset
        {
            public ProbedInput Input => (ProbedInput)KeyBindingInputManager;

            public ProbedRuleset(IBeatmap beatmap)
                : base(new BmsRuleset(), beatmap)
            {
                InitialiseCompatibilityLayoutForTesting();
            }

            protected override PassThroughInputManager CreateInputManager() => new ProbedInput(Ruleset.RulesetInfo, Variant);
        }

        private sealed partial class ProbedInput : BmsInputManager
        {
            public Action? AfterNextUpdate { get; set; }

            public ProbedInput(RulesetInfo ruleset, int variant)
                : base(ruleset, variant)
            {
            }

            protected override void Update()
            {
                base.Update();
                Action? callback = AfterNextUpdate;
                AfterNextUpdate = null;
                callback?.Invoke();
            }
        }
    }
}
