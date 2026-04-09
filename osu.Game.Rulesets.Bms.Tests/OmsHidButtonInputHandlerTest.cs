// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using NUnit.Framework;
using oms.Input;
using oms.Input.Devices;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Bms.Input;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class OmsHidButtonInputHandlerTest
    {
        [Test]
        public void TestAlternateButtonsReleaseOnlyAfterFinalButton()
        {
            var events = new List<string>();
            var handler = new OmsHidButtonInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_Scratch,
                    OmsBindingTrigger.HidButton("iidx-pad", 0),
                    OmsBindingTrigger.HidButton("iidx-pad", 1))
            }, action => events.Add($"+{action}"), action => events.Add($"-{action}"));

            Assert.Multiple(() =>
            {
                Assert.That(handler.GetActionsForButton("iidx-pad", 0), Is.EquivalentTo(new[] { OmsAction.Key1P_Scratch }));
                Assert.That(handler.TriggerPressed("iidx-pad", 0), Is.True);
                Assert.That(handler.TriggerPressed("iidx-pad", 1), Is.False);
                Assert.That(handler.TriggerReleased("iidx-pad", 0), Is.False);
                Assert.That(handler.TriggerReleased("iidx-pad", 1), Is.True);
                Assert.That(events, Is.EqualTo(new[] { "+Key1P_Scratch", "-Key1P_Scratch" }));
            });
        }

        [Test]
        public void TestButtonsOnDifferentDevicesStayIsolated()
        {
            var events = new List<string>();
            var handler = new OmsHidButtonInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_1, OmsBindingTrigger.HidButton("iidx-left", 0)),
                new OmsBinding(OmsAction.Key1P_2, OmsBindingTrigger.HidButton("iidx-right", 0))
            }, action => events.Add($"+{action}"), action => events.Add($"-{action}"));

            Assert.Multiple(() =>
            {
                Assert.That(handler.TriggerPressed("iidx-left", 0), Is.True);
                Assert.That(handler.TriggerPressed("iidx-right", 0), Is.True);
                Assert.That(events, Is.EqualTo(new[] { "+Key1P_1", "+Key1P_2" }));
                Assert.That(handler.TriggerReleased("iidx-left", 0), Is.True);
                Assert.That(handler.TriggerReleased("iidx-right", 0), Is.True);
                Assert.That(events, Is.EqualTo(new[] { "+Key1P_1", "+Key1P_2", "-Key1P_1", "-Key1P_2" }));
            });
        }

        [Test]
        public void TestInputManagerHidEntryPointRoutesOmsAction()
        {
            var inputManager = new BmsInputManager(new BmsRuleset().RulesetInfo, 8);

            Assert.Multiple(() =>
            {
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_1), Is.False);
                Assert.That(inputManager.TriggerHidButtonPressed("iidx-pad", 0), Is.False);
            });

            var customHandler = new OmsHidButtonInputHandler(new[]
            {
                new OmsBinding(OmsAction.Key1P_1, OmsBindingTrigger.HidButton("iidx-pad", 0))
            }, action => inputManager.TriggerOmsActionPressed(action), action => inputManager.TriggerOmsActionReleased(action));

            Assert.Multiple(() =>
            {
                Assert.That(customHandler.TriggerPressed("iidx-pad", 0), Is.True);
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_1), Is.True);
                Assert.That(customHandler.TriggerReleased("iidx-pad", 0), Is.True);
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_1), Is.False);
            });
        }
    }
}
