// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Framework.Input.Bindings;
using osu.Game.Input;

namespace osu.Game.Rulesets.Mania.Tests
{
    [TestFixture]
    public class NativeKeyboardDeviceStateTest
    {
        [Test]
        public void TestTwoPhysicalKeyboardsAndNativeRepeatsShareTheFinalLogicalRelease()
        {
            var events = new List<(InputKey Key, long Time, bool Pressed)>();
            var state = new NativeKeyboardDeviceState((key, timestamp, pressed) => events.Add((key, timestamp, pressed)));
            state.Apply(1, 0x20, InputKey.D, 10, true);
            state.Apply(1, 0x20, InputKey.D, 11, true);
            state.Apply(2, 0x20, InputKey.D, 12, true);
            state.Apply(1, 0x20, InputKey.D, 13, false);
            Assert.That(events, Is.EqualTo(new[] { (InputKey.D, 10L, true) }));
            state.Apply(2, 0x20, InputKey.D, 14, false);
            Assert.That(events, Is.EqualTo(new[] { (InputKey.D, 10L, true), (InputKey.D, 14L, false) }));
        }

        [Test]
        public void TestReleaseUsesTheDownIdentityWhenNumLockChangesWhileHeld()
        {
            var events = new List<(InputKey Key, long Time, bool Pressed)>();
            var state = new NativeKeyboardDeviceState((key, timestamp, pressed) => events.Add((key, timestamp, pressed)));
            state.Apply(1, 0x50, InputKey.Keypad2, 10, true);
            state.Apply(1, 0x50, InputKey.Down, 20, false);
            Assert.That(events, Is.EqualTo(new[] { (InputKey.Keypad2, 10L, true), (InputKey.Keypad2, 20L, false) }));
        }

        [Test]
        public void TestUnpluggingOneKeyboardOnlyReleasesItsOwnUnsharedKeys()
        {
            var events = new List<(InputKey Key, long Time, bool Pressed)>();
            var state = new NativeKeyboardDeviceState((key, timestamp, pressed) => events.Add((key, timestamp, pressed)));
            state.Apply(1, 0x20, InputKey.D, 10, true);
            state.Apply(1, 0x21, InputKey.F, 11, true);
            state.Apply(2, 0x20, InputKey.D, 12, true);
            state.RemoveDevice(1, 20);
            Assert.That(events, Is.EqualTo(new[] { (InputKey.D, 10L, true), (InputKey.F, 11L, true), (InputKey.F, 20L, false) }));
            state.RemoveDevice(2, 30);
            Assert.That(events[^1], Is.EqualTo((InputKey.D, 30L, false)));
            Assert.That(events.Count, Is.EqualTo(4));
        }

        [Test]
        public void TestEndingAnOwnerDropsItsPhysicalStateBeforeTheNextOwnerPresses()
        {
            var events = new List<(InputKey Key, long Time, bool Pressed)>();
            var state = new NativeKeyboardDeviceState((key, timestamp, pressed) => events.Add((key, timestamp, pressed)));
            state.Apply(1, 0x20, InputKey.D, 10, true);
            state.Clear();
            state.Apply(1, 0x20, InputKey.D, 11, false);
            state.Apply(1, 0x20, InputKey.D, 12, true);
            state.Apply(1, 0x20, InputKey.D, 13, false);
            Assert.That(events, Is.EqualTo(new[] { (InputKey.D, 10L, true), (InputKey.D, 12L, true), (InputKey.D, 13L, false) }));
        }

        [Test]
        public void TestEndingOwnershipSuppressesRawRepeatsUntilTheActualPhysicalBreak()
        {
            var firstOwner = new List<(InputKey Key, bool Pressed)>();
            var nextOwner = new List<(InputKey Key, bool Pressed)>();
            bool ownerChanged = false;
            var state = new NativeKeyboardDeviceState((key, _, pressed) => (ownerChanged ? nextOwner : firstOwner).Add((key, pressed)));
            state.Apply(1, 0x20, InputKey.D, 10, true);
            state.Clear(); // Focus/pause ends the first owner's logical holding separately.
            state.Apply(1, 0x20, InputKey.D, 11, true); // A raw autorepeat has no Repeat bit.
            Assert.That(firstOwner, Is.EqualTo(new[] { (InputKey.D, true) }));

            ownerChanged = true;
            state.Clear(); // The new gameplay sink also must not inherit the same physical hold.
            state.Apply(1, 0x20, InputKey.D, 12, true);
            state.Apply(1, 0x20, InputKey.D, 13, false);
            Assert.That(nextOwner, Is.Empty);
            state.Apply(1, 0x20, InputKey.D, 14, true);
            state.Apply(1, 0x20, InputKey.D, 15, false);
            Assert.That(nextOwner, Is.EqualTo(new[] { (InputKey.D, true), (InputKey.D, false) }));
        }

        [Test]
        public void TestSuppressedOldKeyboardsCannotReleaseANewKeyboardsLogicalHold()
        {
            var events = new List<(InputKey Key, bool Pressed)>();
            var state = new NativeKeyboardDeviceState((key, _, pressed) => events.Add((key, pressed)));
            state.Apply(1, 0x20, InputKey.D, 10, true);
            state.Apply(2, 0x20, InputKey.D, 11, true);
            state.Clear();
            events.Clear();

            state.Apply(1, 0x20, InputKey.D, 12, true);
            state.Apply(2, 0x20, InputKey.D, 13, true);
            Assert.That(events, Is.Empty);
            state.Apply(3, 0x20, InputKey.D, 14, true);
            state.Apply(1, 0x20, InputKey.D, 15, false);
            state.RemoveDevice(2, 16);
            Assert.That(events, Is.EqualTo(new[] { (InputKey.D, true) }));
            state.Apply(3, 0x20, InputKey.D, 17, false);
            Assert.That(events, Is.EqualTo(new[] { (InputKey.D, true), (InputKey.D, false) }));
        }

        [Test]
        public void TestInactivePressDoesNotOccupyAnotherKeyboardsActiveReference()
        {
            var events = new List<(InputKey Key, bool Pressed)>();
            var state = new NativeKeyboardDeviceState((key, _, pressed) => events.Add((key, pressed)));
            state.Apply(1, 0x20, InputKey.D, 10, true, suppressPress: true);
            state.Apply(1, 0x20, InputKey.D, 11, true); // Resume while the same physical key remains held.
            Assert.That(events, Is.Empty);
            state.Apply(2, 0x20, InputKey.D, 12, true);
            state.Apply(1, 0x20, InputKey.D, 13, false);
            Assert.That(events, Is.EqualTo(new[] { (InputKey.D, true) }));
            state.Apply(2, 0x20, InputKey.D, 14, false);
            state.Apply(1, 0x20, InputKey.D, 15, true);
            state.Apply(1, 0x20, InputKey.D, 16, false);
            Assert.That(events, Is.EqualTo(new[] { (InputKey.D, true), (InputKey.D, false), (InputKey.D, true), (InputKey.D, false) }));
        }

        [Test]
        public void TestSuppressedReleaseUsesItsPhysicalIdentityAfterNumLockChanges()
        {
            var events = new List<(InputKey Key, bool Pressed)>();
            var state = new NativeKeyboardDeviceState((key, _, pressed) => events.Add((key, pressed)));
            state.Apply(1, 0x50, InputKey.Keypad2, 10, true);
            state.Clear();
            events.Clear();
            state.Apply(1, 0x50, InputKey.Down, 11, true);
            state.Apply(1, 0x50, InputKey.Down, 12, false);
            Assert.That(events, Is.Empty);
            state.Apply(1, 0x50, InputKey.Down, 13, true);
            state.Apply(1, 0x50, InputKey.Down, 14, false);
            Assert.That(events, Is.EqualTo(new[] { (InputKey.Down, true), (InputKey.Down, false) }));
        }

        [Test]
        public void TestFocusReconciliationRemovesAnUnobservedBackgroundReleaseBeforeFreshDown()
        {
            var events = new List<(InputKey Key, bool Pressed)>();
            var queriedNativeKeys = new List<int>();
            var state = new NativeKeyboardDeviceState((key, _, pressed) => events.Add((key, pressed)));
            const int native_virtual_y = 0x59; // Physical Z on a QWERTZ layout; this is not (int)InputKey.Z.
            state.Apply(1, 0x2C, InputKey.Z, 10, true, nativeKey: native_virtual_y);
            state.Clear();
            events.Clear();

            // A foreground-only listener receives no raw break when the player releases in another app.
            state.ReconcileReleasedKeys(virtualKey =>
            {
                queriedNativeKeys.Add(virtualKey);
                return false;
            });
            state.Apply(1, 0x2C, InputKey.Z, 20, true, nativeKey: native_virtual_y);
            state.Apply(1, 0x2C, InputKey.Z, 21, false);
            Assert.Multiple(() =>
            {
                Assert.That(queriedNativeKeys, Is.EqualTo(new[] { native_virtual_y }));
                Assert.That(events, Is.EqualTo(new[] { (InputKey.Z, true), (InputKey.Z, false) }));
            });
        }

        [Test]
        public void TestFocusReconciliationKeepsStillHeldKeysSuppressedAndDoesNotQueryUnseenKeys()
        {
            var events = new List<(InputKey Key, bool Pressed)>();
            var queriedNativeKeys = new List<int>();
            var state = new NativeKeyboardDeviceState((key, _, pressed) => events.Add((key, pressed)));
            state.Apply(1, 0x20, InputKey.D, 10, true, nativeKey: 0x44);
            state.Clear();
            events.Clear();
            state.ReconcileReleasedKeys(virtualKey =>
            {
                queriedNativeKeys.Add(virtualKey);
                return true;
            });
            state.Apply(1, 0x20, InputKey.D, 11, true, nativeKey: 0x44);
            state.Apply(1, 0x20, InputKey.D, 12, false);
            Assert.That(events, Is.Empty);
            Assert.That(queriedNativeKeys, Is.EqualTo(new[] { 0x44 }));
            state.Apply(1, 0x20, InputKey.D, 13, true, nativeKey: 0x44);
            state.Apply(1, 0x20, InputKey.D, 14, false);
            Assert.That(events, Is.EqualTo(new[] { (InputKey.D, true), (InputKey.D, false) }));
        }

        [Test]
        public void TestRemovingASuppressedDeviceForgetsItsHeldPhysicalKeys()
        {
            var events = new List<bool>();
            var state = new NativeKeyboardDeviceState((_, _, pressed) => events.Add(pressed));
            state.Apply(1, 0x20, InputKey.D, 10, true);
            state.Clear();
            events.Clear();
            state.RemoveDevice(1, 11);
            state.Apply(1, 0x20, InputKey.D, 12, true);
            state.Apply(1, 0x20, InputKey.D, 13, false);
            Assert.That(events, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void TestRepeatedKeyboardTapsReuseTheConnectedDeviceState()
        {
            int edges = 0;
            var state = new NativeKeyboardDeviceState((_, _, _) => edges++);
            for (int i = 0; i < 128; i++)
                tap(i);
            edges = 0;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 6000; i++)
                tap(i);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Multiple(() =>
            {
                Assert.That(edges, Is.EqualTo(12000));
                Assert.That(allocated, Is.Zero);
            });

            void tap(long time)
            {
                state.Apply(1, 0x20, InputKey.D, time, true);
                state.Apply(1, 0x20, InputKey.D, time, false);
            }
        }
    }
}
