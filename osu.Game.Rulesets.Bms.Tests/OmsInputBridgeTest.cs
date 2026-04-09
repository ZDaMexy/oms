// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using oms.Input;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game;
using osu.Game.Database;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Bms.Input;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class OmsInputBridgeTest
    {
        [TestCase(6, 7)]
        [TestCase(8, 9)]
        [TestCase(9, 10)]
        [TestCase(16, 17)]
        public void TestBindingStoreExposesVariantProfile(int variant, int expectedBindingCount)
        {
            var store = new OmsBindingStore();
            var bindings = store.GetDefaultBindings(variant);

            Assert.That(bindings, Has.Count.EqualTo(expectedBindingCount));
        }

        [TestCase(6, OmsAction.Key1P_Scratch, BmsAction.Scratch1)]
        [TestCase(8, OmsAction.Key1P_7, BmsAction.Key7)]
        [TestCase(9, OmsAction.Key9K_5, BmsAction.Key5)]
        [TestCase(16, OmsAction.Key2P_Scratch, BmsAction.Scratch2)]
        [TestCase(16, OmsAction.Key2P_7, BmsAction.Key14)]
        public void TestOmsActionMapsToBmsActionByVariant(int variant, OmsAction omsAction, BmsAction expectedAction)
        {
            Assert.That(OmsBmsActionMap.TryMapToBmsAction(variant, omsAction, out var mappedAction), Is.True);
            Assert.That(mappedAction, Is.EqualTo(expectedAction));
        }

        [Test]
        public void TestInputManagerUpdatesLaneCoverFocusFromOmsAction()
        {
            var inputManager = new BmsInputManager(new BmsRuleset().RulesetInfo, 8);

            Assert.That(inputManager.LaneCoverFocusPressed.Value, Is.False);
            Assert.That(inputManager.TriggerOmsActionPressed(OmsAction.UI_LaneCoverFocus), Is.True);
            Assert.That(inputManager.LaneCoverFocusPressed.Value, Is.True);
            Assert.That(inputManager.TriggerOmsActionReleased(OmsAction.UI_LaneCoverFocus), Is.True);
            Assert.That(inputManager.LaneCoverFocusPressed.Value, Is.False);
        }

        [Test]
        public void TestRulesetDefaultBindingsStillExposeScratchDefaults()
        {
            var bindings = new BmsRuleset().GetDefaultKeyBindings(8).Where(binding => binding.Action is BmsAction.Scratch1).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(bindings, Has.Length.EqualTo(3));
                Assert.That(bindings.SelectMany(binding => binding.KeyCombination.Keys).OrderBy(key => key).ToArray(), Is.EqualTo(new[] { InputKey.A, InputKey.Q, InputKey.Joystick5 }));
            });
        }

        [TestCase(6, OmsAction.Key1P_Scratch, (int)JoystickButton.GamePadLeftShoulder)]
        [TestCase(6, OmsAction.Key1P_5, (int)JoystickButton.GamePadRightShoulder)]
        [TestCase(8, OmsAction.Key1P_1, (int)JoystickButton.GamePadX)]
        [TestCase(8, OmsAction.Key1P_7, (int)JoystickButton.GamePadRightTrigger)]
        public void TestBindingStoreExposesDefaultXInputButtonsForSinglePlayVariants(int variant, OmsAction action, int expectedButton)
        {
            var binding = new OmsBindingStore().GetDefaultBindings(variant).Single(candidate => candidate.Action == action);

            Assert.That(binding.XInputButtonTriggers.Select(trigger => trigger.ButtonIndex), Is.EquivalentTo(new[] { expectedButton }));
        }

        [Test]
        public void TestResolverParsesPersistedJoystickBindingAsXInputTrigger()
        {
            using var storage = new TemporaryNativeStorage($"oms-xinput-binding-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);

            realm.Write(r => r.Add(new RealmKeyBinding(BmsAction.Key1, new KeyCombination(InputKey.Joystick2), BmsRuleset.SHORT_NAME, 8)));

            var bindings = OmsBmsBindingResolver.GetBindingsOrDefault(realm, 8).Where(binding => binding.Action == OmsAction.Key1P_1).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(bindings, Has.Length.EqualTo(1));
                Assert.That(bindings[0].KeyboardCombinations, Is.Empty);
                Assert.That(bindings[0].XInputButtonTriggers.Select(trigger => trigger.ButtonIndex), Is.EqualTo(new[] { (int)JoystickButton.GamePadA }));
            });
        }

        [Test]
        public void TestResolverReturnsDefaultsWhenNoPersistedBindingsExist()
        {
            using var storage = new TemporaryNativeStorage($"oms-default-bindings-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);

            var bindings = OmsBmsBindingResolver.GetBindingsOrDefault(realm, 8);

            Assert.That(bindings.Select(binding => binding.Action), Is.EquivalentTo(new OmsBindingStore().GetDefaultBindings(8).Select(binding => binding.Action)));
        }

        [Test]
        public void TestResolverDoesNotFallbackToDefaultsWhenPersistedBindingsCannotBeConverted()
        {
            using var storage = new TemporaryNativeStorage($"oms-unsupported-binding-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);

            realm.Write(r => r.Add(new RealmKeyBinding(BmsAction.Key1, new KeyCombination(InputKey.Shift, InputKey.Joystick2), BmsRuleset.SHORT_NAME, 8)));

            var bindings = OmsBmsBindingResolver.GetBindingsOrDefault(realm, 8);

            Assert.Multiple(() =>
            {
                Assert.That(bindings, Is.Empty);
                Assert.That(bindings.Any(binding => binding.Action == OmsAction.Key1P_1), Is.False);
            });
        }

        [Test]
        public void TestSupplementalBindingStorageRoundTripsUnsupportedTriggers()
        {
            using var storage = new TemporaryNativeStorage($"oms-supplemental-bindings-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);

            OmsBmsBindingSettingsStorage.SaveSupplementalBindings(realm, 8, new[]
            {
                new OmsBinding(OmsAction.Key1P_1,
                    OmsBindingTrigger.Keyboard(InputKey.Z),
                    OmsBindingTrigger.XInputButton((int)JoystickButton.GamePadA),
                    OmsBindingTrigger.HidButton("hid:deck-a", 2),
                    OmsBindingTrigger.HidAxis("hid:deck-a", 1, OmsAxisDirection.Negative, axisInverted: true),
                    OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Positive))
            });

            var binding = OmsBmsBindingSettingsStorage.GetSupplementalBindings(realm, 8).Single();

            Assert.Multiple(() =>
            {
                Assert.That(binding.Action, Is.EqualTo(OmsAction.Key1P_1));
                Assert.That(binding.KeyboardCombinations, Is.Empty);
                Assert.That(binding.XInputButtonTriggers, Is.Empty);
                Assert.That(binding.HidButtonTriggers.Select(trigger => (trigger.DeviceIdentifier, trigger.ButtonIndex)), Is.EqualTo(new[] { ("hid:deck-a", 2) }));
                Assert.That(binding.HidAxisTriggers.Select(trigger => (trigger.DeviceIdentifier, trigger.AxisIndex, trigger.AxisDirection, trigger.AxisInverted)), Is.EqualTo(new[] { ("hid:deck-a", 1, OmsAxisDirection.Negative, true) }));
                Assert.That(binding.MouseAxisTriggers.Select(trigger => (trigger.MouseAxisKind, trigger.AxisDirection, trigger.AxisInverted)), Is.EqualTo(new[] { (OmsMouseAxis.X, OmsAxisDirection.Positive, false) }));
            });
        }

        [Test]
        public void TestResolverAppendsSupplementalBindingsToStandardBindings()
        {
            using var storage = new TemporaryNativeStorage($"oms-combined-bindings-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);

            realm.Write(r => r.Add(new RealmKeyBinding(BmsAction.Key1, new KeyCombination(InputKey.Z), BmsRuleset.SHORT_NAME, 8)));
            OmsBmsBindingSettingsStorage.SaveSupplementalBindings(realm, 8, new[]
            {
                new OmsBinding(OmsAction.Key1P_1, OmsBindingTrigger.HidButton("hid:controller", 4))
            });

            var bindings = OmsBmsBindingResolver.GetBindingsOrDefault(realm, 8).Where(binding => binding.Action == OmsAction.Key1P_1).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(bindings, Has.Length.EqualTo(2));
                Assert.That(bindings.Any(binding => binding.KeyboardCombinations.Any(combination => combination.Keys.SequenceEqual(new[] { InputKey.Z }))), Is.True);
                Assert.That(bindings.Any(binding => binding.HidButtonTriggers.Any(trigger => trigger.DeviceIdentifier == "hid:controller" && trigger.ButtonIndex == 4)), Is.True);
            });
        }
    }
}
