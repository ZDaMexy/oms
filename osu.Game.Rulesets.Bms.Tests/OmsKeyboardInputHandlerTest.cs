// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using NUnit.Framework;
using oms.Input;
using oms.Input.Devices;
using osu.Framework.Input.Bindings;
using osu.Game.Rulesets.Bms;
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
                Assert.That(handler.GetActionsForKey(InputKey.Q), Is.EquivalentTo(new[] { OmsAction.Key1P_Scratch }));
                Assert.That(handler.TriggerPressed(InputKey.Q), Is.True);
                Assert.That(handler.TriggerPressed(InputKey.A), Is.False);
                Assert.That(handler.TriggerReleased(InputKey.Q), Is.False);
                Assert.That(handler.TriggerReleased(InputKey.A), Is.True);
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
                Assert.That(inputManager.TriggerKeyPressed(InputKey.Tab), Is.True);
                Assert.That(inputManager.LaneCoverFocusPressed.Value, Is.True);
                Assert.That(inputManager.TriggerKeyReleased(InputKey.Tab), Is.True);
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
                Assert.That(inputManager.TriggerKeyPressed(InputKey.Q), Is.True);
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_Scratch), Is.True);
                Assert.That(inputManager.TriggerKeyPressed(InputKey.A), Is.False);
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_Scratch), Is.True);
                Assert.That(inputManager.TriggerKeyReleased(InputKey.Q), Is.False);
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_Scratch), Is.True);
                Assert.That(inputManager.TriggerKeyReleased(InputKey.A), Is.True);
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_Scratch), Is.False);
            });
        }

        [Test]
        public void TestRawKeyboardSinkEntryPointUpdatesLaneCoverFocus()
        {
            var inputManager = new BmsInputManager(new BmsRuleset().RulesetInfo, 8);
            var sink = (IOmsKeyboardEventSink)inputManager;

            Assert.Multiple(() =>
            {
                Assert.That(inputManager.LaneCoverFocusPressed.Value, Is.False);
                Assert.That(sink.HandleRawKeyPressed(InputKey.Tab), Is.True);
                Assert.That(inputManager.LaneCoverFocusPressed.Value, Is.True);
                Assert.That(sink.HandleRawKeyReleased(InputKey.Tab), Is.True);
                Assert.That(inputManager.LaneCoverFocusPressed.Value, Is.False);
            });
        }

        [Test]
        public void TestRawKeyboardSinkResetReleasesPressedActions()
        {
            var inputManager = new BmsInputManager(new BmsRuleset().RulesetInfo, 8);
            var sink = (IOmsKeyboardEventSink)inputManager;

            Assert.Multiple(() =>
            {
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_Scratch), Is.False);
                Assert.That(sink.HandleRawKeyPressed(InputKey.Q), Is.True);
                Assert.That(inputManager.Router.IsPressed(OmsAction.Key1P_Scratch), Is.True);
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
    }
}
