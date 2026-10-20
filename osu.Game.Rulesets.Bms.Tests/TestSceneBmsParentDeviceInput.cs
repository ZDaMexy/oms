// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using oms.Input;
using oms.Input.Devices;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Input;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;
using osuTK.Input;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    public partial class TestSceneBmsParentDeviceInput : OsuManualInputManagerTestScene
    {
        private const string device_identifier = "parent-input-fixture";
        private ManualClock clock = null!;
        private TimestampedClock? timestampedClock;
        private DeviceRuleset renderer = null!;
        private DeviceSource device = null!;
        private readonly List<(OmsAction Action, bool Pressed, double Time)> edges = new();

        [Cached(typeof(IOmsMouseAxisSource))]
        private readonly RelativeMouseSource mouse = new RelativeMouseSource();

        protected override bool UseFreshStoragePerRun => true;

        [Test]
        public void TestRealParentKeyboardAndHidShareExactlyOneHeldAction()
        {
            arrange();
            seek(1040);
            AddStep("press the real parent SDL keyboard route", () => InputManager.PressKey(Key.Z));
            AddUntilStep("the production note and router both receive the press", () =>
                renderer.Input.Router.IsPressed(OmsAction.Key1P_1) && renderer.Input.IsLaneHeld(BmsAction.Key1));
            AddAssert("the real key note is judged", () => renderer.Playfield.AllHitObjects.OfType<DrawableBmsHitObject>()
                .Single(note => note.HitObject is BmsHitObject { IsScratch: false } && note.HitObject.StartTime == 1040).Result.Type == HitResult.Perfect);
            AddStep("the physical HID button takes over the same action", () => device.Events.Enqueue(button(true)));
            AddUntilStep("the HID batch has been captured", () => device.Consumed > 0);
            AddStep("release only the SDL keyboard", () => InputManager.ReleaseKey(Key.Z));
            AddWaitStep("deliver keyboard release", 2);
            AddAssert("HID retains ownership after keyboard release", () => renderer.Input.IsLaneHeld(BmsAction.Key1));
            AddStep("release the final HID source", () => device.Events.Enqueue(button(false)));
            AddUntilStep("the actual lane and router are both released", () =>
                !renderer.Input.IsLaneHeld(BmsAction.Key1) && !renderer.Input.Router.IsPressed(OmsAction.Key1P_1));
            AddAssert("source takeover creates exactly one complete action", () => edges.Where(edge => edge.Action == OmsAction.Key1P_1)
                .Select(edge => edge.Pressed).SequenceEqual(new[] { true, false }));
        }

        [Test]
        public void TestRealParentJoystickRouteHasBalancedRouterAndGameplayEdges()
        {
            arrange();
            seek(1040);
            AddStep("press the actual parent joystick event", () => InputManager.PressJoystickButton(JoystickButton.GamePadLeftShoulder));
            AddUntilStep("both action consumers see the joystick", () =>
                renderer.Input.Router.IsPressed(OmsAction.Key1P_Scratch) && renderer.Input.IsLaneHeld(BmsAction.Scratch1));
            AddStep("release the actual parent joystick event", () => InputManager.ReleaseJoystickButton(JoystickButton.GamePadLeftShoulder));
            AddUntilStep("neither consumer retains a duplicate reference", () =>
                !renderer.Input.Router.IsPressed(OmsAction.Key1P_Scratch) && !renderer.Input.IsLaneHeld(BmsAction.Scratch1));
            AddAssert("one physical joystick tap produces one action", () => edges.Where(edge => edge.Action == OmsAction.Key1P_Scratch)
                .Select(edge => edge.Pressed).SequenceEqual(new[] { true, false }));
        }

        [Test]
        public void TestPolledHidEdgesUseOuterObservationTimeDuringCatchUpAndReplayRecording()
        {
            arrange();
            var score = new Score();
            AddStep("record two complete HID taps before a long outer frame", () =>
            {
                renderer.SetRecordTarget(score);
                device.Events.Enqueue(button(true));
                device.Events.Enqueue(button(false));
                device.Events.Enqueue(button(true));
                device.Events.Enqueue(button(false));
                clock.CurrentTime = 1100;
            });
            AddUntilStep("catch-up reaches the actual outer observation", () => renderer.FrameStableClock.CurrentTime == 1100 && edges.Count == 4);
            AddAssert("polled reports are never backdated into historical subframes", () => edges.All(edge => edge.Time == 1100));
            AddAssert("the complete buffered taps remain ordered", () => edges.Select(edge => edge.Pressed).SequenceEqual(new[] { true, false, true, false }));
            AddStep("replay recording preserves every transition at that same honest observation", () =>
            {
                var frames = score.Replay.Frames.OfType<BmsReplayFrame>().Where(frame => frame.Time == 1100).ToArray();
                Assert.That(frames.Select(frame => frame.InputActions.Contains(BmsAction.Key1)), Is.EqualTo(new[] { true, false, true, false }));
                renderer.SetRecordTarget(null!);
            });
        }

        [Test]
        public void TestRelativeMouseSamplesRemainDistinctAtAFixedCursorPosition()
        {
            arrange();
            seek(1040);
            AddStep("hold the pointer at a fixed location and emit two physical relative samples", () =>
            {
                InputManager.MoveMouseTo(renderer);
                mouse.Emit(4);
                mouse.Emit(4);
            });
            AddUntilStep("both samples produce independent scratch attacks", () => edges.Count(edge => edge.Action == OmsAction.Key1P_Scratch && edge.Pressed) == 2);
            AddAssert("ordinary pulses released while movement continuity remains held", () =>
                !renderer.Input.Router.IsPressed(OmsAction.Key1P_Scratch) && renderer.Input.IsLaneHeld(BmsAction.Scratch1));
            AddStep("return capture and invalidate a not-yet-dispatched sample", () =>
            {
                edges.Clear();
                mouse.Emit(4);
                mouse.Reset();
            });
            AddWaitStep("drain the reset boundary", 2);
            AddAssert("pending movement cannot attack after capture reset", () => edges.All(edge => !edge.Pressed)
                && !renderer.Input.IsLaneHeld(BmsAction.Scratch1));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void TestMovementStopWaitRemainsFiftyPhysicalMillisecondsAtDifferentRates(double rate)
        {
            arrange(rate, timestamped: true);
            seek(1040);
            AddStep("deliver an actual HID movement report", () => device.Events.Enqueue(
                OmsHidDeviceChange.Axis(new OmsHidAxisChange(device_identifier, 0, 4))));
            AddUntilStep("the production movement level is active", () => renderer.Input.IsLaneHeld(BmsAction.Scratch1));
            seek(1040 + 49 * rate);
            AddAssert("49 physical milliseconds retain movement", () => renderer.Input.IsLaneHeld(BmsAction.Scratch1));
            seek(1040 + 50 * rate);
            AddAssert("the physical 50ms deadline releases at either music rate", () => !renderer.Input.IsLaneHeld(BmsAction.Scratch1));
        }

        [Test]
        public void TestProjectedMovementExpiryIsConsumedOnceAndCatchUpContinues()
        {
            const double rate = 0.5;
            const double movement_time = 65521.50269999999;
            const double first_anchor_time = 65526.77569999999;
            const double second_anchor_time = 65551.61034999999;
            const double expiry_time = 65546.50269999998;
            long movementTimestamp = toTimestamp(38522.785);
            long firstAnchorTimestamp = toTimestamp(38533.331);
            long secondAnchorTimestamp = toTimestamp(38583.0003);
            var score = new Score();
            int expiryVisits = 0;
            (bool Held, double? NextInput)? firstExpiryVisit = null;
            arrange(rate, timestamped: true, initialSample: new GameplayInputTime(movement_time, movementTimestamp, rate, true, 0, 0));
            AddStep("record the real movement actions and count visits to the published expiry", () =>
            {
                renderer.SetRecordTarget(score);
                renderer.Input.AfterUpdate = time =>
                {
                    if (time == expiry_time)
                    {
                        expiryVisits++;
                        firstExpiryVisit ??= (renderer.Input.IsScratchMotionHeld(BmsAction.Scratch1),
                            ((IFrameStableInputHandler)renderer.Input).NextInputTime);
                    }
                };
            });
            AddStep("consume a buffered relative sample using the first QPC anchor", () =>
            {
                mouse.EmitAt(4, movementTimestamp);
                clock.CurrentTime = first_anchor_time;
                timestampedClock!.Timestamp = firstAnchorTimestamp;
            });
            AddUntilStep("the movement is held after the first anchored outer frame", () =>
                renderer.FrameStableClock.CurrentTime == first_anchor_time && renderer.Input.IsScratchMotionHeld(BmsAction.Scratch1));
            AddStep("verify this is the real past-deadline round-trip boundary", () =>
            {
                double ticksToMilliseconds = 1000.0 / Stopwatch.Frequency;
                double expiry = firstAnchorTimestamp * ticksToMilliseconds + (movement_time - first_anchor_time) / rate + 50;
                double projected = second_anchor_time + (expiry - secondAnchorTimestamp * ticksToMilliseconds) * rate;
                double roundTrip = secondAnchorTimestamp * ticksToMilliseconds + (projected - second_anchor_time) / rate;
                Assert.Multiple(() =>
                {
                    Assert.That(projected, Is.EqualTo(expiry_time));
                    Assert.That(projected, Is.LessThan(second_anchor_time));
                    Assert.That(roundTrip, Is.LessThan(expiry));
                    Assert.That(((IFrameStableInputHandler)renderer.Input).NextInputTime, Is.EqualTo(expiry_time));
                    Assert.That(edges.Where(edge => edge.Action == OmsAction.Key1P_Scratch && edge.Pressed).Select(edge => edge.Time),
                        Is.EqualTo(new[] { movement_time }));
                });
            });
            AddStep("publish the next same-epoch anchor and keep that reference fixed", () =>
            {
                clock.CurrentTime = second_anchor_time;
                timestampedClock!.Timestamp = secondAnchorTimestamp;
            });
            AddUntilStep("the production input manager visits the published expiry", () => firstExpiryVisit.HasValue);
            AddStep("the first expiry update consumes its own published stop", () =>
            {
                var visited = firstExpiryVisit!.Value;
                Assert.Multiple(() =>
                {
                    Assert.That(expiryVisits, Is.EqualTo(1));
                    Assert.That(visited.Held, Is.False);
                    Assert.That(visited.NextInput, Is.Not.EqualTo(expiry_time));
                });
            });
            AddUntilStep("frame-stable gameplay catches up to the unchanged reference", () => renderer.FrameStableClock.CurrentTime == second_anchor_time);
            AddStep("the real recorder preserves the single motion release at that exact stop", () =>
            {
                BmsReplayFrame[] frames = score.Replay.Frames.OfType<BmsReplayFrame>().ToArray();
                BmsReplayFrame[] releases = frames.Skip(1).Where((frame, index) =>
                    frames[index].InputActions.Contains(BmsAction.Scratch1Motion)
                    && !frame.InputActions.Contains(BmsAction.Scratch1Motion)).ToArray();
                Assert.Multiple(() =>
                {
                    Assert.That(expiryVisits, Is.EqualTo(1));
                    Assert.That(renderer.Input.IsScratchMotionHeld(BmsAction.Scratch1), Is.False);
                    Assert.That(((IFrameStableInputHandler)renderer.Input).NextInputTime, Is.Null);
                    Assert.That(releases, Has.Length.EqualTo(1));
                    Assert.That(releases.Select(frame => frame.Time), Is.EqualTo(new[] { expiry_time }));
                    Assert.That(frames.First(frame => frame.InputActions.Contains(BmsAction.Scratch1Motion)).Time, Is.EqualTo(movement_time));
                });
                renderer.SetRecordTarget(null!);
                renderer.Input.AfterUpdate = null;
            });
        }

        [TestCase(1)]
        [TestCase(2)]
        public void TestPreviouslyDeliveredStickKeepsMovingDuringCatchUpUntilItsObservedReturn(double rate)
        {
            arrange(rate, timestamped: true, stick: true);
            seek(1040);
            AddStep("deliver full absolute stick deflection", () => device.Events.Enqueue(
                OmsHidDeviceChange.Axis(new OmsHidAxisChange(device_identifier, 0, 100, 100, -100, 100, false))));
            AddWaitStep("allow the position report to be delivered", 2);
            seek(1040 + 100 * rate);
            AddAssert("catch-up uses the old observed deflection continuously", () => renderer.Input.IsLaneHeld(BmsAction.Scratch1));
            AddStep("observe the stick returning to its centre", () => device.Events.Enqueue(
                OmsHidDeviceChange.Axis(new OmsHidAxisChange(device_identifier, 0, -100, 0, -100, 100, false))));
            AddWaitStep("deliver the return without treating it as reverse movement", 2);
            seek(1040 + 149 * rate);
            AddAssert("returning to centre starts the configured stopping wait", () => renderer.Input.IsLaneHeld(BmsAction.Scratch1));
            seek(1040 + 150 * rate);
            AddAssert("the stopped stick no longer owns the scratch", () => !renderer.Input.IsLaneHeld(BmsAction.Scratch1));
        }

        private void arrange(double rate = 1, bool timestamped = false, bool stick = false, GameplayInputTime? initialSample = null)
        {
            AddStep("load production parent input and a buffered physical device", () =>
            {
                edges.Clear();
                device = new DeviceSource();
                var decoded = new BmsBeatmapDecoder().DecodeText("#TITLE Parent input\n#BPM 3000\n#01311:0101\n#01316:0101\n", "parent-input.bme");
                var beatmap = new BmsBeatmapConverter(new BmsDecodedBeatmap(decoded), new BmsRuleset()).Convert();
                renderer = new DeviceRuleset(beatmap, device) { RelativeSizeAxes = Axes.Both };
                OmsBmsBindingSettingsStorage.SaveSupplementalBindings(Realm, renderer.Variant, new[]
                {
                    new OmsBinding(OmsAction.Key1P_1, OmsBindingTrigger.HidButton(device_identifier, 0)),
                    new OmsBinding(OmsAction.Key1P_Scratch, OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive)),
                    new OmsBinding(OmsAction.Key1P_Scratch, OmsBindingTrigger.HidAxis(device_identifier, 0, OmsAxisDirection.Positive,
                        axisSettings: new OmsAxisSettings(stick ? OmsAxisMode.StickToMotion : OmsAxisMode.RelativeMotion))),
                });
                clock = new ManualClock { IsRunning = true, Rate = rate, CurrentTime = initialSample?.Time ?? 0 };
                timestampedClock = timestamped ? new TimestampedClock(clock) { Timestamp = initialSample?.Timestamp, Child = renderer } : null;
                Drawable content = timestampedClock ?? (Drawable)renderer;
                Child = new DependencyProvidingContainer
                {
                    CachedDependencies = new[] { (typeof(RealmAccess), (object)Realm) },
                    RelativeSizeAxes = Axes.Both,
                    Clock = new FramedClock(clock),
                    Child = content,
                };
            });
            AddUntilStep("production input and notes are loaded", () => renderer.IsLoaded && renderer.Input.IsLoaded
                && renderer.Playfield.AllHitObjects.OfType<DrawableBmsHitObject>().Any());
            seek(initialSample?.Time ?? 1000);
            AddUntilStep("gameplay owns the relative stream capture", () => mouse.Leases == 1);
            AddStep("observe actual router transitions with the gameplay clock", () =>
            {
                renderer.Input.Router.ActionPressed += action => edges.Add((action, true, renderer.FrameStableClock.CurrentTime));
                renderer.Input.Router.ActionReleased += action => edges.Add((action, false, renderer.FrameStableClock.CurrentTime));
            });
        }

        private void seek(double target)
        {
            AddStep("advance the real outer clock", () => clock.CurrentTime = target);
            AddUntilStep("frame-stable gameplay reaches that clock", () => renderer.FrameStableClock.CurrentTime == target);
        }

        private static OmsHidDeviceChange button(bool pressed)
            => OmsHidDeviceChange.Button(new OmsHidButtonChange(device_identifier, 0, pressed));

        private static long toTimestamp(double milliseconds) => (long)Math.Round(milliseconds * Stopwatch.Frequency / 1000);

        private sealed partial class DeviceRuleset : DrawableBmsRuleset
        {
            private readonly DeviceSource source;
            public ProbedDeviceInput Input => (ProbedDeviceInput)KeyBindingInputManager;
            public DeviceRuleset(IBeatmap beatmap, DeviceSource source) : base(new BmsRuleset(), beatmap)
            {
                this.source = source;
                InitialiseCompatibilityLayoutForTesting();
            }
            protected override PassThroughInputManager CreateInputManager()
                => new ProbedDeviceInput(Ruleset.RulesetInfo, Variant, () => new DeviceProvider(source));
        }

        private sealed partial class ProbedDeviceInput : BmsInputManager
        {
            public Action<double>? AfterUpdate { get; set; }

            public ProbedDeviceInput(RulesetInfo ruleset, int variant, Func<IOmsHidButtonDeviceProvider?> createProvider)
                : base(ruleset, variant, createProvider)
            {
            }

            protected override void Update()
            {
                base.Update();
                AfterUpdate?.Invoke(Time.Current);
            }
        }

        private sealed class DeviceSource
        {
            public readonly Queue<OmsHidDeviceChange> Events = new();
            public int Consumed;
        }

        private sealed class DeviceProvider : IOmsHidButtonDeviceProvider
        {
            private readonly DeviceSource source;
            public DeviceProvider(DeviceSource source) => this.source = source;
            public event EventHandler? DevicesChanged { add { } remove { } }
            public IEnumerable<IOmsHidButtonDevice> GetDevices(IEnumerable<string> deviceIdentifiers)
                => deviceIdentifiers.Contains(device_identifier) ? new[] { new Device(source) } : Array.Empty<IOmsHidButtonDevice>();
            public void Dispose() { }
        }

        private sealed class Device : IOmsHidButtonDevice
        {
            private readonly DeviceSource source;
            private readonly OmsHidDeviceChange[] buffer = new OmsHidDeviceChange[1];
            public Device(DeviceSource source) => this.source = source;
            public string Identifier => device_identifier;
            public bool IsConnected => true;
            public bool HasPendingChanges => source.Events.Count != 0;
            public bool TryReadChanges(out IReadOnlyList<OmsHidDeviceChange> changes)
            {
                if (source.Events.TryDequeue(out buffer[0]))
                {
                    source.Consumed++;
                    changes = buffer;
                    return true;
                }
                changes = Array.Empty<OmsHidDeviceChange>();
                return false;
            }
            public void Dispose() { }
        }

        [Cached(typeof(IGameplayClock))]
        private sealed partial class TimestampedClock : Container, IGameplayClock, IGameplayInputTimeSource
        {
            private readonly ManualClock source;
            public TimestampedClock(ManualClock source) { this.source = source; RelativeSizeAxes = Axes.Both; }
            public GameplayInputTime? InputTime { get; private set; }
            public long? Timestamp { get; set; }
            public IBindable<bool> IsPaused { get; } = new BindableBool();
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
            protected override void Update()
            {
                base.Update();
                InputTime = new GameplayInputTime(CurrentTime, Timestamp ?? (long)Math.Round(CurrentTime / Rate * Stopwatch.Frequency / 1000), Rate, IsRunning, 0, 0);
            }
        }

        private sealed class RelativeMouseSource : IOmsMouseAxisSource
        {
            public int Leases;
            public bool IsActive => Leases != 0;
            public event Action<OmsRelativeMouseMovement>? RelativeMouseMoved;
            public event Action? InputReset;
            public IDisposable AcquireCapture() { Leases++; return new Lease(this); }
            public void Emit(float x)
                => EmitAt(x, Stopwatch.GetTimestamp());

            public void EmitAt(float x, long timestamp)
            {
                Assert.That(IsActive, Is.True);
                RelativeMouseMoved?.Invoke(new OmsRelativeMouseMovement(x, 0, timestamp));
            }
            public void Reset() => InputReset?.Invoke();
            private sealed class Lease : IDisposable
            {
                private RelativeMouseSource? source;
                public Lease(RelativeMouseSource source) => this.source = source;
                public void Dispose()
                {
                    if (source == null) return;
                    source.Leases--;
                    source = null;
                }
            }
        }
    }
}
