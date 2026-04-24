// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using NUnit.Framework;
using oms.Input;
using oms.Input.Devices;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Bms.Input;
using System.Linq;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class OmsInputRouterTest
    {
        [Test]
        public void TestDuplicatePressesReleaseOnlyAfterFinalSignal()
        {
            var events = new List<string>();
            var router = new OmsInputRouter();
            router.ActionPressed += action => events.Add($"+{action}");
            router.ActionReleased += action => events.Add($"-{action}");

            Assert.Multiple(() =>
            {
                Assert.That(router.TriggerPressed(OmsAction.Key1P_Scratch), Is.True);
                Assert.That(router.TriggerPressed(OmsAction.Key1P_Scratch), Is.False);
                Assert.That(router.IsPressed(OmsAction.Key1P_Scratch), Is.True);
                Assert.That(router.TriggerReleased(OmsAction.Key1P_Scratch), Is.False);
                Assert.That(router.IsPressed(OmsAction.Key1P_Scratch), Is.True);
                Assert.That(router.TriggerReleased(OmsAction.Key1P_Scratch), Is.True);
                Assert.That(router.IsPressed(OmsAction.Key1P_Scratch), Is.False);
                Assert.That(events, Is.EqualTo(new[] { "+Key1P_Scratch", "-Key1P_Scratch" }));
            });
        }

        [Test]
        public void TestKeyboardAndMouseAxisSignalsShareScratchStateUntilFinalRelease()
        {
            var router = new OmsInputRouter();
            var bindings = new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.Keyboard(InputKey.LShift),
                    OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive))
            };

            var keyboardHandler = new OmsKeyboardInputHandler(bindings, action => router.TriggerPressed(action), action => router.TriggerReleased(action));
            var mouseAxisHandler = new OmsMouseAxisInputHandler(bindings, action => router.TriggerPressed(action), action => router.TriggerReleased(action));

            Assert.Multiple(() =>
            {
                Assert.That(router.IsPressed(OmsAction.Key1P_Scratch), Is.False);

                Assert.That(keyboardHandler.TriggerPressed(InputKey.LShift), Is.True);
                Assert.That(router.IsPressed(OmsAction.Key1P_Scratch), Is.True);

                mouseAxisHandler.BeginFrame();
                Assert.That(mouseAxisHandler.ApplyAxisDelta(OmsMouseAxis.X, 4), Is.True);
                Assert.That(router.IsPressed(OmsAction.Key1P_Scratch), Is.True);

                Assert.That(keyboardHandler.TriggerReleased(InputKey.LShift), Is.True);
                Assert.That(router.IsPressed(OmsAction.Key1P_Scratch), Is.True);

                Assert.That(mouseAxisHandler.FinishFrame(), Is.True);
                Assert.That(router.IsPressed(OmsAction.Key1P_Scratch), Is.False);
            });
        }

        [Test]
        public void TestInputManagerKeepsScratchPressedAcrossKeyboardAndXInputSources()
        {
            var inputManager = new BmsInputManager(new BmsRuleset().RulesetInfo, 8);

            Assert.Multiple(() =>
            {
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_Scratch), Is.False);

                Assert.That(inputManager.TriggerKeyPressed(InputKey.LShift), Is.True);
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_Scratch), Is.True);

                Assert.That(inputManager.TriggerXInputButtonPressed((int)JoystickButton.GamePadLeftShoulder), Is.True);
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_Scratch), Is.True);

                Assert.That(inputManager.TriggerKeyReleased(InputKey.LShift), Is.True);
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_Scratch), Is.True);

                Assert.That(inputManager.TriggerXInputButtonReleased((int)JoystickButton.GamePadLeftShoulder), Is.True);
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_Scratch), Is.False);
            });
        }

        [Test]
        public void TestInputManagerKeyBindingStateStaysPressedAcrossKeyboardAndXInputSources()
        {
            var inputManager = new BmsInputManager(new BmsRuleset().RulesetInfo, 8);

            Assert.Multiple(() =>
            {
                Assert.That(inputManager.KeyBindingContainer.PressedActions, Does.Not.Contain(BmsAction.Scratch1));

                Assert.That(inputManager.TriggerKeyPressed(InputKey.LShift), Is.True);
                Assert.That(inputManager.KeyBindingContainer.PressedActions, Does.Contain(BmsAction.Scratch1));

                Assert.That(inputManager.TriggerXInputButtonPressed((int)JoystickButton.GamePadLeftShoulder), Is.True);
                Assert.That(inputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1), Is.EqualTo(1));

                Assert.That(inputManager.TriggerKeyReleased(InputKey.LShift), Is.True);
                Assert.That(inputManager.KeyBindingContainer.PressedActions, Does.Contain(BmsAction.Scratch1));

                Assert.That(inputManager.TriggerXInputButtonReleased((int)JoystickButton.GamePadLeftShoulder), Is.True);
                Assert.That(inputManager.KeyBindingContainer.PressedActions, Does.Not.Contain(BmsAction.Scratch1));
            });
        }

        [Test]
        public void TestInputManagerMouseAxisPulseReleasesScratchActionAtFrameEnd()
        {
            var inputManager = new BmsInputManager(new BmsRuleset().RulesetInfo, 8);
            var mouseAxisHandler = new OmsMouseAxisInputHandler(
                new[] { new OmsBinding(OmsAction.Key1P_Scratch, OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive)) },
                action => inputManager.TriggerOmsActionPressed(action),
                action => inputManager.TriggerOmsActionReleased(action));

            Assert.Multiple(() =>
            {
                Assert.That(inputManager.KeyBindingContainer.PressedActions, Does.Not.Contain(BmsAction.Scratch1));

                mouseAxisHandler.BeginFrame();
                Assert.That(mouseAxisHandler.ApplyAxisDelta(OmsMouseAxis.X, 4), Is.True);
                Assert.That(inputManager.KeyBindingContainer.PressedActions, Does.Contain(BmsAction.Scratch1));

                Assert.That(mouseAxisHandler.FinishFrame(), Is.True);
                Assert.That(inputManager.KeyBindingContainer.PressedActions, Does.Not.Contain(BmsAction.Scratch1));

                mouseAxisHandler.BeginFrame();
                Assert.That(mouseAxisHandler.ApplyAxisDelta(OmsMouseAxis.X, 3), Is.True);
                Assert.That(inputManager.KeyBindingContainer.PressedActions, Does.Contain(BmsAction.Scratch1));
            });
        }

        [Test]
        public void TestInputManagerMouseAxisPulseDoesNotReleaseHeldKeyboardScratch()
        {
            var inputManager = new BmsInputManager(new BmsRuleset().RulesetInfo, 8);
            var mouseAxisHandler = new OmsMouseAxisInputHandler(
                new[] { new OmsBinding(OmsAction.Key1P_Scratch, OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive)) },
                action => inputManager.TriggerOmsActionPressed(action),
                action => inputManager.TriggerOmsActionReleased(action));

            Assert.Multiple(() =>
            {
                Assert.That(inputManager.TriggerKeyPressed(InputKey.LShift), Is.True);
                Assert.That(inputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1), Is.EqualTo(1));

                mouseAxisHandler.BeginFrame();
                Assert.That(mouseAxisHandler.ApplyAxisDelta(OmsMouseAxis.X, 4), Is.True);
                Assert.That(inputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1), Is.EqualTo(1));

                Assert.That(mouseAxisHandler.FinishFrame(), Is.True);
                Assert.That(inputManager.KeyBindingContainer.PressedActions, Does.Contain(BmsAction.Scratch1));

                Assert.That(inputManager.TriggerKeyReleased(InputKey.LShift), Is.True);
                Assert.That(inputManager.KeyBindingContainer.PressedActions, Does.Not.Contain(BmsAction.Scratch1));
            });
        }

        [Test]
        public void TestInputManagerMouseAxisDirectionFlipWithinFrameRetriggersScratchAction()
        {
            var events = new List<string>();
            var inputManager = new BmsInputManager(new BmsRuleset().RulesetInfo, 8);
            inputManager.Router.ActionPressed += action => events.Add($"+{action}");
            inputManager.Router.ActionReleased += action => events.Add($"-{action}");

            var mouseAxisHandler = new OmsMouseAxisInputHandler(
                new[]
                {
                    new OmsBinding(OmsAction.Key1P_Scratch,
                        OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive),
                        OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Negative))
                },
                action => inputManager.TriggerOmsActionPressed(action),
                action => inputManager.TriggerOmsActionReleased(action));

            Assert.Multiple(() =>
            {
                mouseAxisHandler.BeginFrame();

                Assert.That(mouseAxisHandler.ApplyAxisDelta(OmsMouseAxis.X, 4), Is.True);
                Assert.That(mouseAxisHandler.ApplyAxisDelta(OmsMouseAxis.X, -4), Is.True);
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_Scratch), Is.True);
                Assert.That(inputManager.KeyBindingContainer.PressedActions.Count(action => action == BmsAction.Scratch1), Is.EqualTo(1));
                Assert.That(events, Is.EqualTo(new[] { "+Key1P_Scratch", "-Key1P_Scratch", "+Key1P_Scratch" }));

                Assert.That(mouseAxisHandler.FinishFrame(), Is.True);
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_Scratch), Is.False);
                Assert.That(inputManager.KeyBindingContainer.PressedActions, Does.Not.Contain(BmsAction.Scratch1));
                Assert.That(events, Is.EqualTo(new[] { "+Key1P_Scratch", "-Key1P_Scratch", "+Key1P_Scratch", "-Key1P_Scratch" }));
            });
        }
    }
}
