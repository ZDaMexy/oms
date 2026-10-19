// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using oms.Input;
using oms.Input.Devices;
using osu.Framework.Input.Bindings;
using osu.Game.Rulesets.Bms.Input;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class OmsKeyboardInputHandlerTest
    {
        [Test]
        public void TestAlternateScratchBindingsReleaseOnlyAfterFinalKey()
        {
            var events = new List<string>();
            var handler = new OmsKeyboardInputHandler(new OmsBindingStore().GetDefaultBindings(8),
                action => events.Add($"+{action}"),
                action => events.Add($"-{action}"));

            Assert.Multiple(() =>
            {
                Assert.That(handler.GetActionsForKey(InputKey.LShift), Is.EquivalentTo(new[] { OmsAction.Key1P_Scratch }));
                Assert.That(handler.TriggerPressed(InputKey.LShift), Is.True);
                Assert.That(handler.TriggerPressed(InputKey.RShift), Is.False);
                Assert.That(handler.TriggerReleased(InputKey.LShift), Is.False);
                Assert.That(handler.TriggerReleased(InputKey.RShift), Is.True);
                Assert.That(events, Is.EqualTo(new[] { "+Key1P_Scratch", "-Key1P_Scratch" }));
            });
        }

        [Test]
        public void TestInputManagerKeyboardEntryPointUpdatesLaneCoverFocus()
        {
            var inputManager = new BmsInputManager(new BmsRuleset().RulesetInfo, 8);

            Assert.Multiple(() =>
            {
                Assert.That(inputManager.LaneCoverFocusPressed.Value, Is.False);
                Assert.That(inputManager.TriggerKeyPressed(InputKey.W), Is.True);
                Assert.That(inputManager.LaneCoverFocusPressed.Value, Is.True);
                Assert.That(inputManager.TriggerKeyReleased(InputKey.W), Is.True);
                Assert.That(inputManager.LaneCoverFocusPressed.Value, Is.False);
            });
        }

        [Test]
        public void TestInputManagerKeyboardEntryPointHonoursAlternateScratchBindings()
        {
            var inputManager = new BmsInputManager(new BmsRuleset().RulesetInfo, 8);

            Assert.Multiple(() =>
            {
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_Scratch), Is.False);
                Assert.That(inputManager.TriggerKeyPressed(InputKey.LShift), Is.True);
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_Scratch), Is.True);
                Assert.That(inputManager.TriggerKeyPressed(InputKey.RShift), Is.False);
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_Scratch), Is.True);
                Assert.That(inputManager.TriggerKeyReleased(InputKey.LShift), Is.False);
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_Scratch), Is.True);
                Assert.That(inputManager.TriggerKeyReleased(InputKey.RShift), Is.True);
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_Scratch), Is.False);
            });
        }

        [Test]
        public void TestRawKeyboardSinkDoesNotMutateStateBeforeAnInputUpdate()
        {
            var inputManager = new BmsInputManager(new BmsRuleset().RulesetInfo, 8);
            var sink = (IOmsKeyboardEventSink)inputManager;

            Assert.Multiple(() =>
            {
                Assert.That(inputManager.LaneCoverFocusPressed.Value, Is.False);
                Assert.That(sink.HandleRawKeyPressed(InputKey.W, Stopwatch.GetTimestamp()), Is.True);
                Assert.That(inputManager.LaneCoverFocusPressed.Value, Is.False);
                Assert.That(sink.HandleRawKeyReleased(InputKey.W, Stopwatch.GetTimestamp()), Is.True);
                Assert.That(inputManager.LaneCoverFocusPressed.Value, Is.False);
            });
        }

        [Test]
        public void TestDisposedRawKeyboardSinkRejectsLateWindowCallbacks()
        {
            var inputManager = new BmsInputManager(new BmsRuleset().RulesetInfo, 8);
            var sink = (IOmsKeyboardEventSink)inputManager;
            inputManager.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_Scratch), Is.False);
                Assert.That(sink.HandleRawKeyPressed(InputKey.LShift, Stopwatch.GetTimestamp()), Is.False);
                Assert.That(sink.HandleRawKeyReleased(InputKey.LShift, Stopwatch.GetTimestamp()), Is.False);
                sink.ResetRawKeyboardState();
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_Scratch), Is.False);
            });
        }

        [Test]
        public void TestChordBindingRequiresFullCombination()
        {
            var events = new List<string>();
            var handler = new OmsKeyboardInputHandler(new[]
            {
                new OmsBinding(OmsAction.UI_ModMenu, OmsBindingTrigger.Keyboard(InputKey.Control, InputKey.M))
            }, action => events.Add($"+{action}"), action => events.Add($"-{action}"));

            Assert.Multiple(() =>
            {
                Assert.That(handler.GetActionsForKey(InputKey.Control), Is.EquivalentTo(new[] { OmsAction.UI_ModMenu }));
                Assert.That(handler.TriggerPressed(InputKey.Control), Is.False);
                Assert.That(events, Is.Empty);
                Assert.That(handler.TriggerPressed(InputKey.M), Is.True);
                Assert.That(events, Is.EqualTo(new[] { "+UI_ModMenu" }));
                Assert.That(handler.TriggerReleased(InputKey.Control), Is.True);
                Assert.That(events, Is.EqualTo(new[] { "+UI_ModMenu", "-UI_ModMenu" }));
            });
        }

        [Test]
        public void TestSteadyChordTransitionsDoNotAllocatePerKey()
        {
            int presses = 0;
            int releases = 0;
            var handler = new OmsKeyboardInputHandler(new[]
            {
                new OmsBinding(OmsAction.UI_ModMenu, OmsBindingTrigger.Keyboard(InputKey.Control, InputKey.M)),
            }, _ => presses++, _ => releases++);

            for (int warmup = 0; warmup < 256; warmup++)
                cycle();
            presses = releases = 0;

            long before = GC.GetAllocatedBytesForCurrentThread();
            long started = Stopwatch.GetTimestamp();
            for (int repeat = 0; repeat < 16_384; repeat++)
                cycle();
            double elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Multiple(() =>
            {
                Assert.That(presses, Is.EqualTo(16_384));
                Assert.That(releases, Is.EqualTo(presses));
                Assert.That(allocated, Is.Zero, "Keyboard transitions must reuse their immutable binding data.");
            });
            TestContext.Progress.WriteLine($"keyboard-chord: transitions=65536 allocated={allocated} elapsedMs={elapsed:F3}");

            void cycle()
            {
                handler.TriggerPressed(InputKey.LControl);
                handler.TriggerPressed(InputKey.M);
                handler.TriggerReleased(InputKey.M);
                handler.TriggerReleased(InputKey.LControl);
            }
        }

        [TestCase(InputKey.Control, InputKey.LControl, InputKey.RControl)]
        [TestCase(InputKey.Shift, InputKey.LShift, InputKey.RShift)]
        [TestCase(InputKey.Alt, InputKey.LAlt, InputKey.RAlt)]
        [TestCase(InputKey.Super, InputKey.LSuper, InputKey.RSuper)]
        public void TestPhysicalModifiersSatisfyVirtualChordsUntilTheFinalSideIsReleased(InputKey modifier, InputKey left, InputKey right)
        {
            var events = new List<bool>();
            var handler = new OmsKeyboardInputHandler(new[]
            {
                new OmsBinding(OmsAction.UI_ModMenu, OmsBindingTrigger.Keyboard(modifier, InputKey.M)),
            }, _ => events.Add(true), _ => events.Add(false));

            Assert.That(handler.HasBindingForKey(left) && handler.HasBindingForKey(right), Is.True);
            Assert.That(handler.GetActionsForKey(left), Is.EqualTo(new[] { OmsAction.UI_ModMenu }));
            foreach ((InputKey first, InputKey second) in new[] { (left, right), (right, left) })
            {
                events.Clear();
                handler.TriggerPressed(first);
                handler.TriggerPressed(InputKey.M);
                handler.TriggerPressed(second);
                handler.TriggerReleased(first);
                Assert.That(events, Is.EqualTo(new[] { true }), "The remaining physical modifier still owns the chord.");
                handler.TriggerReleased(second);
                handler.TriggerReleased(InputKey.M);
                Assert.That(events, Is.EqualTo(new[] { true, false }));

                events.Clear();
                handler.TriggerPressed(InputKey.M);
                handler.TriggerPressed(first);
                handler.TriggerReleased(InputKey.M);
                handler.TriggerReleased(first);
                Assert.That(events, Is.EqualTo(new[] { true, false }), "The chord must also work when its ordinary key arrives first.");
            }
        }

        [Test]
        public void TestLiteralAndVirtualModifierChordsKeepSeparateOwnership()
        {
            var active = new HashSet<OmsAction>();
            var handler = new OmsKeyboardInputHandler(new[]
            {
                new OmsBinding(OmsAction.UI_ModMenu, OmsBindingTrigger.Keyboard(InputKey.Control, InputKey.M)),
                new OmsBinding(OmsAction.UI_LaneCoverFocus, OmsBindingTrigger.Keyboard(InputKey.LControl, InputKey.M)),
            }, action => active.Add(action), action => active.Remove(action));

            handler.TriggerPressed(InputKey.LControl);
            handler.TriggerPressed(InputKey.M);
            handler.TriggerPressed(InputKey.RControl);
            Assert.That(active, Is.EquivalentTo(new[] { OmsAction.UI_ModMenu, OmsAction.UI_LaneCoverFocus }));
            handler.TriggerReleased(InputKey.LControl);
            Assert.That(active, Is.EqualTo(new[] { OmsAction.UI_ModMenu }));
            handler.TriggerReleased(InputKey.RControl);
            Assert.That(active, Is.Empty);
            handler.TriggerReleased(InputKey.M);
        }
    }
}
