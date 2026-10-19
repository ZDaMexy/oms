// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using oms.Input;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
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
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Scoring;
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
                        accepted = ((IOmsKeyboardEventSink)renderer.Input).HandleRawKeyPressed(InputKey.Z);
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
                    Assert.That(sink.HandleRawKeyPressed(InputKey.W), Is.True);
                    sink.ResetRawKeyboardState();
                    Assert.That(sink.HandleRawKeyPressed(InputKey.W), Is.True);
                    Assert.That(sink.HandleRawKeyReleased(InputKey.W), Is.True);
                    Assert.That(sink.HandleRawKeyPressed(InputKey.None), Is.False);
                    Assert.That(sink.HandleRawKeyPressed(InputKey.F24), Is.False);
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
                sink.HandleRawKeyPressed(InputKey.LShift);
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
                sink.HandleRawKeyPressed(InputKey.W);
                sink.HandleRawKeyReleased(InputKey.W);
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
                Assert.That(sink.HandleRawKeyPressed(first), Is.True);
                Assert.That(sink.HandleRawKeyPressed(InputKey.Z), Is.True);
            }).GetAwaiter().GetResult());
            AddUntilStep("the actual chord judges the note", () => note.Judged);
            AddAssert("the chord has a normal Perfect result", () => note.Result.Type == HitResult.Perfect);
            AddStep("transfer the held modifier to its other physical side", () => Task.Run(() =>
            {
                var sink = (IOmsKeyboardEventSink)renderer.Input;
                sink.HandleRawKeyPressed(second);
                sink.HandleRawKeyReleased(first);
            }).GetAwaiter().GetResult());
            AddAssert("the other modifier retains the key action", () => renderer.Input.KeyBindingContainer.PressedActions.Contains(BmsAction.Key1));
            AddStep("release the final physical modifier", () => Task.Run(() =>
            {
                var sink = (IOmsKeyboardEventSink)renderer.Input;
                sink.HandleRawKeyReleased(second);
                sink.HandleRawKeyReleased(InputKey.Z);
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
                sink.HandleRawKeyPressed(InputKey.LControl);
                sink.HandleRawKeyReleased(InputKey.LControl);
            }).GetAwaiter().GetResult();
        }

        private void arrange(bool rawAvailable = false, bool virtualChord = false, bool unsupportedChordKey = false)
        {
            AddStep("load a real BMS keyboard and note tree", () =>
            {
                keyboardSource.Available = rawAvailable;
                keyboardSource.Unsupported.Clear();
                if (unsupportedChordKey)
                    keyboardSource.Unsupported.Add(InputKey.Z);
                var decoded = new BmsBeatmapDecoder().DecodeText("#TITLE Raw keyboard\n#BPM 120\n#00111:01\n", "keyboard.bme");
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
                Child = new DependencyProvidingContainer
                {
                    CachedDependencies = new[] { (typeof(RealmAccess), (object)Realm) },
                    RelativeSizeAxes = Axes.Both,
                    Clock = new FramedClock(clock = new ManualClock { IsRunning = true }),
                    Child = renderer,
                };
            });
            AddUntilStep("note and input manager loaded", () => renderer.IsLoaded && renderer.Input.IsLoaded
                && renderer.ChildrenOfType<DrawableBmsHitObject>().Any());
            AddStep("seek to the note", () =>
            {
                note = renderer.ChildrenOfType<DrawableBmsHitObject>().Single();
                clock.CurrentTime = note.HitObject.StartTime;
            });
            AddUntilStep("the input clock is at the note", () => Precision.AlmostEquals(renderer.FrameStableClock.CurrentTime, note.HitObject.StartTime, 1));
        }

        private sealed class KeyboardSource : IOmsKeyboardEventSource
        {
            private IOmsKeyboardEventSink? sink;
            public bool Available { get; set; }
            public HashSet<InputKey> Unsupported { get; } = new HashSet<InputKey>();
            public void RegisterSink(IOmsKeyboardEventSink sink) => this.sink = sink;
            public void UnregisterSink(IOmsKeyboardEventSink sink)
            {
                if (ReferenceEquals(this.sink, sink))
                    this.sink = null;
            }

            public bool IsActiveFor(IOmsKeyboardEventSink sink) => Available && ReferenceEquals(this.sink, sink);
            public bool SupportsKey(InputKey key) => !Unsupported.Contains(key);
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
