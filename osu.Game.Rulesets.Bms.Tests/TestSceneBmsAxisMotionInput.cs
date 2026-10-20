// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using oms.Input;
using oms.Input.Devices;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Replays;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD.ClicksPerSecond;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    public partial class TestSceneBmsAxisMotionInput : OsuTestScene
    {
        private const string device_identifier = "axis-fixture-unit";
        private const double stop_timeout = 50;
        private ManualClock clock = null!;
        private AxisRuleset renderer = null!;
        private AxisSource source = null!;
        private bool useHid;
        private BmsHoldNote hold = null!;
        private DrawableBmsHoldNote holdDrawable = null!;
        private DrawableBmsHitObject[] notes = null!;

        protected override bool UseFreshStoragePerRun => true;

        [TestCase(BmsLongNoteMode.LN, false)]
        [TestCase(BmsLongNoteMode.CN, false)]
        [TestCase(BmsLongNoteMode.HCN, false)]
        [TestCase(BmsLongNoteMode.LN, true)]
        [TestCase(BmsLongNoteMode.CN, true)]
        [TestCase(BmsLongNoteMode.HCN, true)]
        public void TestPureMovementCompletesAScratchLongNote(BmsLongNoteMode mode, bool hid)
        {
            arrange(mode, hid);
            moveThroughHold();
            AddStep("the actual long-note head and tail both succeed", () => Assert.Multiple(() =>
            {
                Assert.That(head().Result.Type, Is.EqualTo(HitResult.Perfect));
                Assert.That(tail().Result.Type, Is.EqualTo(mode.RequiresTailJudgement() ? HitResult.Perfect : HitResult.IgnoreHit));
                Assert.That(holdDrawable.AllJudged, Is.True);
            }));
        }

        [TestCase(BmsLongNoteMode.LN, false)]
        [TestCase(BmsLongNoteMode.CN, false)]
        [TestCase(BmsLongNoteMode.HCN, false)]
        [TestCase(BmsLongNoteMode.LN, true)]
        [TestCase(BmsLongNoteMode.CN, true)]
        [TestCase(BmsLongNoteMode.HCN, true)]
        public void TestStoppingMovementEndsOnlyAfterTheConfiguredWaitingTime(BmsLongNoteMode mode, bool hid)
        {
            arrange(mode, hid);
            seekTo(() => hold.StartTime);
            move();
            seekTo(() => hold.StartTime + stop_timeout - 1);
            AddAssert("the movement source still owns the long note before its deadline", () => renderer.Input.IsLaneHeld(BmsAction.Scratch1));
            AddAssert("the real long-note body remains held", () => holdDrawable.BodyState.Value == BmsLongNoteBodyState.Holding);
            seekTo(() => hold.StartTime + stop_timeout);
            AddAssert("the movement source releases at its deadline", () => !renderer.Input.IsLaneHeld(BmsAction.Scratch1));
            if (mode == BmsLongNoteMode.HCN)
            {
                AddAssert("HCN remains recoverable after movement stops", () => !tail().Judged && holdDrawable.BodyState.Value == BmsLongNoteBodyState.Broken);
                seekTo(() => hold.EndTime + 1);
            }
            AddAssert("stopping cannot receive a successful long-note tail", () =>
                tail().Result.Type == (mode.RequiresTailJudgement() ? HitResult.Miss : HitResult.IgnoreMiss));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestConsecutiveMovementPulsesStillHitOrdinaryScratchNotes(bool hid)
        {
            arrange(BmsLongNoteMode.LN, hid, longNote: false);
            seekTo(() => notes[0].HitObject.StartTime);
            move();
            AddAssert("first note receives an actual Perfect", () => notes[0].Result.Type == HitResult.Perfect);
            seekTo(() => notes[1].HitObject.StartTime);
            AddAssert("continuous movement is still held before the second attack", () => renderer.Input.IsLaneHeld(BmsAction.Scratch1));
            move();
            AddAssert("the second movement pulse is not suppressed by the held motion level", () => notes[1].Result.Type == HitResult.Perfect);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestScratchContinuityDoesNotInflateKeysPerSecond(bool hid)
        {
            ClicksPerSecondController counter = null!;
            arrange(BmsLongNoteMode.LN, hid, longNote: false);
            AddStep("attach the real gameplay input counter", () =>
            {
                counter = new ClicksPerSecondController();
                renderer.Input.Attach(counter);
                Add(new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[]
                    {
                        (typeof(IGameplayClock), renderer.FrameStableClock),
                        (typeof(IFrameStableClock), renderer.FrameStableClock),
                    },
                    Child = counter,
                });
            });
            AddUntilStep("the counter has loaded", () => counter.IsLoaded);
            seekTo(() => notes[0].HitObject.StartTime);
            move();
            AddAssert("one scratch attack is one input despite its separate motion level", () => counter.Value == 1);
            seekTo(() => notes[1].HitObject.StartTime);
            move();
            AddAssert("the next attack increments the counter once", () => counter.Value == 2);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestKeyboardAndMovementCanHandOverTheSameLongNote(bool hid)
        {
            arrange(BmsLongNoteMode.CN, hid);
            seekTo(() => hold.StartTime);
            AddStep("start the long note through the production keyboard handler", () => renderer.Input.TriggerKeyPressed(InputKey.LShift));
            seekTo(() => hold.StartTime + 25);
            move();
            AddStep("release only the keyboard source", () => renderer.Input.TriggerKeyReleased(InputKey.LShift));
            AddAssert("movement keeps the actual long-note tail pending", () => !tail().Judged && renderer.Input.IsLaneHeld(BmsAction.Scratch1));
            seekTo(() => hold.StartTime + 50);
            AddStep("transfer back to the keyboard before movement expires", () => renderer.Input.TriggerKeyPressed(InputKey.LShift));
            seekTo(() => hold.StartTime + 100);
            AddAssert("expired movement does not release the held keyboard", () => !tail().Judged && renderer.Input.IsLaneHeld(BmsAction.Scratch1));
            seekTo(() => hold.EndTime + 1);
            AddAssert("the handed-over long note succeeds", () => tail().Result.Type == HitResult.Perfect);
            AddStep("release the final physical key", () => renderer.Input.TriggerKeyReleased(InputKey.LShift));
        }

        [Test]
        public void TestHidDisconnectReleasesMovementOnTheSameGameplayTime()
        {
            arrange(BmsLongNoteMode.CN, hid: true);
            seekTo(() => hold.StartTime);
            move();
            AddStep("disconnect the actual fake device behind the production provider", () => source.Connected = false);
            AddUntilStep("the production manager removes device movement ownership", () => !renderer.Input.IsLaneHeld(BmsAction.Scratch1));
            AddStep("disconnect is not delayed until the movement timeout", () => Assert.Multiple(() =>
            {
                Assert.That(renderer.FrameStableClock.CurrentTime, Is.EqualTo(hold.StartTime));
                Assert.That(tail().Result.Type, Is.EqualTo(HitResult.Miss));
            }));
        }

        [Test]
        public void TestInputEpochResetDropsQueuedHidMovementAndAcceptsANewStroke()
        {
            arrange(BmsLongNoteMode.LN, hid: true, longNote: false);
            seekTo(() => notes[0].HitObject.StartTime);
            AddStep("queue a movement from the ending input epoch", () =>
            {
                source.Events.Enqueue(OmsHidDeviceChange.Axis(new OmsHidAxisChange(device_identifier, 0, 4)));
                ((IFrameStableInputHandler)renderer.Input).ResetInput();
            });
            AddUntilStep("the queued report is drained through the real provider", () => source.Consumed == 1);
            AddAssert("the expired epoch cannot hit a note or restart movement", () => !notes[0].Judged && !renderer.Input.IsLaneHeld(BmsAction.Scratch1));
            move();
            AddAssert("a new report in the next epoch remains playable", () => notes[0].Result.Type == HitResult.Perfect);
        }

        [Test]
        public void TestMotionOnlyReplayCannotHitAnOrdinaryNote()
        {
            arrange(BmsLongNoteMode.LN, hid: false, longNote: false);
            AddStep("load a replay with motion continuity and no scratch attacks", () => renderer.SetReplayScore(new Score
            {
                Replay = new Replay
                {
                    Frames =
                    {
                        new BmsReplayFrame(0),
                        new BmsReplayFrame(notes[0].HitObject.StartTime - 50, BmsAction.Scratch1Motion),
                        new BmsReplayFrame(notes[1].HitObject.StartTime + 50, BmsAction.Scratch1Motion),
                        new BmsReplayFrame(notes[1].HitObject.StartTime + 500),
                    },
                },
            }));
            seekTo(() => notes[1].HitObject.StartTime);
            AddAssert("motion level reaches the actual replay input manager", () => renderer.Input.IsLaneHeld(BmsAction.Scratch1));
            AddAssert("neither note accepts a continuity level as an attack", () => notes.All(note => !note.Judged));
            seekTo(() => notes[1].HitObject.StartTime + 250);
            AddAssert("both unplayed notes eventually miss", () => notes.All(note => note.Result.Type == HitResult.Miss));
        }

        [Test]
        public void TestRecordedMouseMovementSurvivesLegacyRoundTripAndReplayRewind()
        {
            arrange(BmsLongNoteMode.CN, hid: false);
            var recorded = new Score();
            (double Time, int Mask)[] stoppedFrames = null!;
            AddStep("record real production mouse movement actions", () => renderer.SetRecordTarget(recorded));
            moveThroughHold();
            AddAssert("the live long note succeeds before replaying", () => head().Result.Type == HitResult.Perfect && tail().Result.Type == HitResult.Perfect);
            AddStep("round-trip the actual recording through the legacy float format", () =>
            {
                renderer.SetRecordTarget(null!);
                Assert.That(recorded.Replay.Frames.OfType<BmsReplayFrame>().Any(frame => frame.InputActions.Contains(BmsAction.Scratch1Motion)), Is.True);
                recorded.Replay.Frames = recorded.Replay.Frames.OfType<BmsReplayFrame>().Select(frame =>
                {
                    var restored = new BmsReplayFrame(frame.Time);
                    restored.FromLegacy(frame.ToLegacy(renderer.Beatmap), renderer.Beatmap);
                    return (Rulesets.Replays.ReplayFrame)restored;
                }).ToList();
                stoppedFrames = recorded.Replay.Frames.Cast<BmsReplayFrame>().Select(frame => (frame.Time, frame.InputActionMask)).ToArray();
                Assert.That(stoppedFrames.Select(frame => frame.Time), Is.Ordered.Ascending);
                renderer.SetReplayScore(recorded);
            });
            seekTo(() => hold.StartTime - 100);
            seekTo(() => hold.StartTime + 150);
            AddAssert("replay restores real movement continuity in the long-note body", () =>
                renderer.Input.IsLaneHeld(BmsAction.Scratch1) && holdDrawable.BodyState.Value == BmsLongNoteBodyState.Holding);
            seekTo(() => hold.EndTime + 1);
            AddAssert("recording and replay retain the same actual head and tail results", () =>
                head().Result.Type == HitResult.Perfect && tail().Result.Type == HitResult.Perfect);
            AddAssert("the stopped recorder cannot append frames when its old lifetime is revisited", () =>
                recorded.Replay.Frames.Cast<BmsReplayFrame>().Select(frame => (frame.Time, frame.InputActionMask)).SequenceEqual(stoppedFrames));
        }

        private void arrange(BmsLongNoteMode mode, bool hid, bool longNote = true)
        {
            AddStep("load real BMS input using supplemental bindings saved in the renderer's Realm", () =>
            {
                useHid = hid;
                source = new AxisSource();
                var decoded = new BmsBeatmapDecoder().DecodeText(longNote
                    ? "#TITLE Axis long note\n#BPM 400\n#LNTYPE 1\n#00356:01000200\n"
                    : "#TITLE Axis stream\n#BPM 4000\n#03316:0101\n", "axis-input.bme");
                var beatmap = new BmsBeatmapConverter(new BmsDecodedBeatmap(decoded), new BmsRuleset()).Convert();
                Mod[] mods = mode switch
                {
                    BmsLongNoteMode.CN => new Mod[] { new BmsModChargeNote() },
                    BmsLongNoteMode.HCN => new Mod[] { new BmsModHellChargeNote() },
                    _ => Array.Empty<Mod>(),
                };
                renderer = new AxisRuleset(beatmap, mods, hid ? source : null) { RelativeSizeAxes = Axes.Both };
                var settings = new OmsAxisSettings(stopTimeoutMilliseconds: stop_timeout);
                OmsBindingTrigger trigger = hid
                    ? OmsBindingTrigger.HidAxis(device_identifier, 0, OmsAxisDirection.Positive, axisSettings: settings)
                    : OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive, axisSettings: settings);
                OmsBmsBindingSettingsStorage.SaveSupplementalBindings(Realm, renderer.Variant,
                    new[] { new OmsBinding(OmsAction.Key1P_Scratch, trigger) });
                Child = new DependencyProvidingContainer
                {
                    CachedDependencies = new[] { (typeof(RealmAccess), (object)Realm) },
                    RelativeSizeAxes = Axes.Both,
                    Clock = new FramedClock(clock = new ManualClock { IsRunning = true }),
                    Child = renderer,
                };
            });
            AddUntilStep("the actual notes and production input manager finish loading", () =>
                renderer.IsLoaded && renderer.Input.IsLoaded && renderer.Playfield.AllHitObjects.OfType<DrawableBmsHitObject>().Any());
            AddStep("retain the real scratch objects", () =>
            {
                notes = renderer.Playfield.AllHitObjects.OfType<DrawableBmsHitObject>()
                    .Where(note => note.HitObject is BmsHitObject { IsScratch: true } && note.HitObject is not BmsHoldNote)
                    .OrderBy(note => note.HitObject.StartTime).ToArray();
                if (!longNote)
                    return;
                holdDrawable = renderer.Playfield.AllHitObjects.OfType<DrawableBmsHoldNote>().Single();
                hold = (BmsHoldNote)holdDrawable.HitObject;
            });
        }

        private void moveThroughHold()
        {
            seekTo(() => hold.StartTime);
            move();
            for (int offset = 25; offset < 300; offset += 25)
            {
                int elapsed = offset;
                seekTo(() => hold.StartTime + elapsed);
                move();
            }
            seekTo(() => hold.EndTime + 1);
        }

        private void seekTo(Func<double> target)
        {
            AddStep("advance the real gameplay clock", () => clock.CurrentTime = target());
            AddUntilStep("frame-stable gameplay reaches the requested time", () => Precision.AlmostEquals(renderer.FrameStableClock.CurrentTime, target(), 0.01));
        }

        private void move()
        {
            int consumedBefore = 0;
            AddStep("deliver physical-axis movement through the production handler", () =>
            {
                if (useHid)
                {
                    consumedBefore = source.Consumed;
                    source.Events.Enqueue(OmsHidDeviceChange.Axis(new OmsHidAxisChange(device_identifier, 0, 4)));
                }
                else
                    Assert.That(renderer.Input.TriggerMouseAxisDelta(OmsMouseAxis.X, 4), Is.True);
            });
            AddUntilStep("the configured source has reached the live input manager", () =>
                !useHid || source.Consumed > consumedBefore);
        }

        private DrawableBmsHoldNoteHead head() => holdDrawable.NestedHitObjects.OfType<DrawableBmsHoldNoteHead>().Single();

        private DrawableBmsHoldNoteTail tail() => holdDrawable.NestedHitObjects.OfType<DrawableBmsHoldNoteTail>().Single();

        private sealed partial class AxisRuleset : DrawableBmsRuleset
        {
            private readonly AxisSource? axisSource;

            public BmsInputManager Input => (BmsInputManager)KeyBindingInputManager;

            public AxisRuleset(IBeatmap beatmap, IReadOnlyList<Mod> mods, AxisSource? axisSource)
                : base(new BmsRuleset(), beatmap, mods)
            {
                this.axisSource = axisSource;
                InitialiseCompatibilityLayoutForTesting();
            }

            protected override PassThroughInputManager CreateInputManager()
                => new BmsInputManager(Ruleset.RulesetInfo, Variant,
                    () => axisSource == null ? null : new AxisProvider(axisSource));
        }

        private sealed class AxisSource
        {
            public readonly Queue<OmsHidDeviceChange> Events = new Queue<OmsHidDeviceChange>();
            public bool Connected = true;
            public int Consumed;
        }

        private sealed class AxisProvider : IOmsHidButtonDeviceProvider
        {
            private readonly AxisSource source;

            public AxisProvider(AxisSource source) => this.source = source;

            public event EventHandler? DevicesChanged
            {
                add { }
                remove { }
            }

            public IEnumerable<IOmsHidButtonDevice> GetDevices(IEnumerable<string> deviceIdentifiers)
                => source.Connected && deviceIdentifiers.Contains(device_identifier)
                    ? new IOmsHidButtonDevice[] { new AxisDevice(source) }
                    : Array.Empty<IOmsHidButtonDevice>();

            public void Dispose()
            {
            }
        }

        private sealed class AxisDevice : IOmsHidButtonDevice
        {
            private readonly AxisSource source;
            private readonly OmsHidDeviceChange[] change = new OmsHidDeviceChange[1];
            private bool disposed;

            public AxisDevice(AxisSource source) => this.source = source;

            public string Identifier => device_identifier;

            public bool IsConnected => !disposed && source.Connected;

            public bool HasPendingChanges => source.Events.Count > 0;

            public bool TryReadChanges(out IReadOnlyList<OmsHidDeviceChange> changes)
            {
                if (IsConnected && source.Events.TryDequeue(out change[0]))
                {
                    source.Consumed++;
                    changes = change;
                    return true;
                }
                changes = Array.Empty<OmsHidDeviceChange>();
                return false;
            }

            public void Dispose() => disposed = true;
        }
    }
}
