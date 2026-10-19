// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using osu.Framework.Input.StateChanges;
using osu.Framework.Input.StateChanges.Events;
using osu.Framework.Input.States;
using osu.Game.Input.Handlers;
using osu.Game.Replays;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class ReplayInputSteadyPerformanceTest
    {
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void TestBmsSteadyReplayInputDoesNotAllocate(bool autoplay, bool held)
        {
            var replay = new Replay
            {
                Frames = new List<Rulesets.Replays.ReplayFrame>
                {
                    new BmsReplayFrame(0, held ? new[] { BmsAction.Key1 } : Array.Empty<BmsAction>()),
                    new BmsReplayFrame(100000),
                },
            };
            ReplayInputHandler handler = autoplay ? new BmsAutoplayReplayInputHandler(replay) : new BmsFramedReplayInputHandler(replay);
            measure<BmsAction>(handler, held, autoplay ? "bms-autoplay" : "bms-replay");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestManiaSteadyReplayInputDoesNotAllocate(bool held)
        {
            var replay = new Replay
            {
                Frames = new List<Rulesets.Replays.ReplayFrame>
                {
                    new ManiaReplayFrame(0, held ? new[] { ManiaAction.Key1 } : Array.Empty<ManiaAction>()),
                    new ManiaReplayFrame(100000),
                },
            };
            // Exercise the actual internal handler without widening the mania production API for this fixture.
            Type type = typeof(ManiaReplayFrame).Assembly.GetType("osu.Game.Rulesets.Mania.Replays.ManiaFramedReplayInputHandler", throwOnError: true)!;
            var handler = (ReplayInputHandler)Activator.CreateInstance(type, replay)!;
            measure<ManiaAction>(handler, held, "mania-replay");
        }

        private static void measure<T>(ReplayInputHandler handler, bool held, string scenario)
            where T : struct
        {
            using (handler)
            {
                var state = new RulesetInputManagerInputState<T>(new InputState());
                var changes = new ActionChanges<T>();
                var inputs = new List<IInput>(4);
                if (handler.SetFrameFromTime(0) != 0)
                    throw new InvalidOperationException("The initial replay boundary was not reached.");
                for (int i = 0; i < 256; i++)
                    apply(1000);

                // The actual ruleset handler reset clears this field; an unchanged frame must then re-apply its keys.
                state.LastReplayState = null;
                apply(1000);
                Assert.That(changes.Pressed, Is.EqualTo(held ? 2 : 0));

                const int frames = 65536;
                long start = Stopwatch.GetTimestamp();
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < frames; i++)
                    apply(1000 + i * 0.001);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                double elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

                apply(100000);
                TestContext.Progress.WriteLine($"replay-input: scenario={scenario} held={held} frames={frames} allocated={allocated} elapsedMs={elapsedMs:F3}");
                Assert.Multiple(() =>
                {
                    Assert.That(allocated, Is.Zero);
                    Assert.That(changes.Pressed, Is.EqualTo(held ? 2 : 0));
                    Assert.That(changes.Released, Is.EqualTo(held ? 1 : 0));
                    Assert.That(state.LastReplayState!.PressedActions, Is.Empty);
                });

                void apply(double time)
                {
                    if (handler.SetFrameFromTime(time) != time)
                        throw new InvalidOperationException("This steady-state scenario must advance at its exact requested time.");
                    inputs.Clear();
                    handler.CollectPendingInputs(inputs);
                    foreach (IInput input in inputs)
                        input.Apply(state, changes);
                }
            }
        }

        private sealed class ActionChanges<T> : IInputStateChangeHandler
            where T : struct
        {
            public int Pressed { get; private set; }
            public int Released { get; private set; }

            public void HandleInputStateChange(InputStateChangeEvent inputStateChange)
            {
                if (inputStateChange is ReplayInputHandler.ReplayStateChangeEvent<T> actions)
                {
                    Pressed += actions.PressedActions.Length;
                    Released += actions.ReleasedActions.Length;
                }
            }
        }
    }
}
