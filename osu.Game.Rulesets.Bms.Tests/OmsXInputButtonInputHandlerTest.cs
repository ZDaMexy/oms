// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using NUnit.Framework;
using oms.Input;
using oms.Input.Devices;
using osu.Framework.Input;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Bms.Input;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class OmsXInputButtonInputHandlerTest
    {
        [Test]
        public void TestAlternateButtonsReleaseOnlyAfterFinalButton()
        {
            var events = new List<string>();
            var handler = new OmsXInputButtonInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.XInputButton((int)JoystickButton.GamePadLeftShoulder),
                    OmsBindingTrigger.XInputButton((int)JoystickButton.GamePadRightShoulder))
            }, action => events.Add($"+{action}"), action => events.Add($"-{action}"));

            Assert.Multiple(() =>
            {
                Assert.That(handler.GetActionsForButton((int)JoystickButton.GamePadLeftShoulder), Is.EquivalentTo(new[] { OmsAction.Key1P_Scratch }));
                Assert.That(handler.TriggerPressed((int)JoystickButton.GamePadLeftShoulder), Is.True);
                Assert.That(handler.TriggerPressed((int)JoystickButton.GamePadRightShoulder), Is.False);
                Assert.That(handler.TriggerReleased((int)JoystickButton.GamePadLeftShoulder), Is.False);
                Assert.That(handler.TriggerReleased((int)JoystickButton.GamePadRightShoulder), Is.True);
                Assert.That(events, Is.EqualTo(new[] { "+Key1P_Scratch", "-Key1P_Scratch" }));
            });
        }

        [Test]
        public void TestDistinctButtonsStayIsolated()
        {
            var events = new List<string>();
            var handler = new OmsXInputButtonInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_1, OmsBindingTrigger.XInputButton((int)JoystickButton.GamePadA)),
                new OmsBinding(OmsAction.Key1P_2, OmsBindingTrigger.XInputButton((int)JoystickButton.GamePadB))
            }, action => events.Add($"+{action}"), action => events.Add($"-{action}"));

            Assert.Multiple(() =>
            {
                Assert.That(handler.TriggerPressed((int)JoystickButton.GamePadA), Is.True);
                Assert.That(handler.TriggerPressed((int)JoystickButton.GamePadB), Is.True);
                Assert.That(events, Is.EqualTo(new[] { "+Key1P_1", "+Key1P_2" }));
                Assert.That(handler.TriggerReleased((int)JoystickButton.GamePadA), Is.True);
                Assert.That(handler.TriggerReleased((int)JoystickButton.GamePadB), Is.True);
                Assert.That(events, Is.EqualTo(new[] { "+Key1P_1", "+Key1P_2", "-Key1P_1", "-Key1P_2" }));
            });
        }

        [Test]
        public void TestInputManagerXInputEntryPointRoutesOmsAction()
        {
            var inputManager = new BmsInputManager(new BmsRuleset().RulesetInfo, 8);

            Assert.Multiple(() =>
            {
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_1), Is.False);
                Assert.That(inputManager.TriggerXInputButtonPressed((int)JoystickButton.GamePadStart), Is.False);
            });

            var customHandler = new OmsXInputButtonInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_1, OmsBindingTrigger.XInputButton((int)JoystickButton.GamePadA))
            }, action => inputManager.TriggerOmsActionPressed(action), action => inputManager.TriggerOmsActionReleased(action));

            Assert.Multiple(() =>
            {
                Assert.That(customHandler.TriggerPressed((int)JoystickButton.GamePadA), Is.True);
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_1), Is.True);
                Assert.That(customHandler.TriggerReleased((int)JoystickButton.GamePadA), Is.True);
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_1), Is.False);
            });
        }

        [Test]
        public void TestInputManagerDefault7KXInputBindingUpdatesRouter()
        {
            var inputManager = new BmsInputManager(new BmsRuleset().RulesetInfo, 8);

            Assert.Multiple(() =>
            {
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_Scratch), Is.False);
                Assert.That(inputManager.TriggerXInputButtonPressed((int)JoystickButton.GamePadLeftShoulder), Is.True);
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_Scratch), Is.True);
                Assert.That(inputManager.TriggerXInputButtonReleased((int)JoystickButton.GamePadLeftShoulder), Is.True);
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_Scratch), Is.False);
            });
        }
    }
}
