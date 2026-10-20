// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.Bindings;

namespace osu.Game.Input
{
    /// <summary>
    /// Aggregates physical keyboards before their shared logical key stream reaches gameplay.
    /// A repeat is not another held source, and unplugging one keyboard releases only its own keys.
    /// Owned and cleared by the native input thread.
    /// </summary>
    public sealed class NativeKeyboardDeviceState
    {
        private readonly Action<InputKey, long, bool> dispatch;
        private readonly Dictionary<nint, Dictionary<int, PressedKey>> deviceKeys = new Dictionary<nint, Dictionary<int, PressedKey>>();
        private readonly Dictionary<InputKey, int> keyCounts = new Dictionary<InputKey, int>();
        private readonly List<(nint Device, int PhysicalKey)> releasedWhileInactive = new List<(nint, int)>();
        private long ownershipEpoch;

        public NativeKeyboardDeviceState(Action<InputKey, long, bool> dispatch)
        {
            this.dispatch = dispatch;
        }

        public void Apply(nint device, int physicalKey, InputKey key, long timestamp, bool pressed, bool suppressPress = false, int nativeKey = 0)
        {
            if (pressed)
            {
                if (!deviceKeys.TryGetValue(device, out Dictionary<int, PressedKey>? keys))
                    deviceKeys.Add(device, keys = new Dictionary<int, PressedKey>());

                if (!keys.TryAdd(physicalKey, new PressedKey(key, ownershipEpoch, suppressPress, nativeKey)))
                    return;

                if (suppressPress)
                    return;

                int count = keyCounts.GetValueOrDefault(key);
                keyCounts[key] = count + 1;
                if (count == 0)
                    dispatch(key, timestamp, true);
            }
            else
            {
                if (!deviceKeys.TryGetValue(device, out Dictionary<int, PressedKey>? keys) || !keys.Remove(physicalKey, out PressedKey applied))
                    return;

                // A previous owner or a make received while inactive cannot own this epoch's logical key.
                if (applied.Suppressed || applied.Epoch != ownershipEpoch)
                    return;

                key = applied.Key;
                int count = keyCounts[key];
                if (count > 1)
                    keyCounts[key] = count - 1;
                else
                {
                    keyCounts.Remove(key);
                    // Use the identity captured on down, even if NumLock/layout changed while held.
                    dispatch(key, timestamp, false);
                }
            }
        }

        public void RemoveDevice(nint device, long timestamp)
        {
            if (!deviceKeys.TryGetValue(device, out Dictionary<int, PressedKey>? keys))
                return;

            foreach (int physicalKey in keys.Keys.ToArray())
                Apply(device, physicalKey, InputKey.None, timestamp, false);
            deviceKeys.Remove(device);
        }

        /// <summary>
        /// Ends logical ownership. Existing physical holds stay suppressed until their real break, so an
        /// operating-system repeat cannot become a fresh attack after focus, pause or a sink change.
        /// The owner separately releases its already-applied gameplay state.
        /// </summary>
        public void Clear()
        {
            ownershipEpoch++;
            keyCounts.Clear();
        }

        /// <summary>
        /// Reconciles only previously seen, suppressed holds at a focus boundary. Native key codes are
        /// captured on down, independently from the physical gameplay identity and later layout changes.
        /// This removes releases which a foreground-only listener could not receive while inactive.
        /// </summary>
        public void ReconcileReleasedKeys(Func<int, bool> isNativeKeyDown)
        {
            releasedWhileInactive.Clear();
            foreach (var device in deviceKeys)
            {
                foreach (var key in device.Value)
                {
                    PressedKey pressed = key.Value;
                    if ((pressed.Suppressed || pressed.Epoch != ownershipEpoch) && pressed.NativeKey != 0 && !isNativeKeyDown(pressed.NativeKey))
                        releasedWhileInactive.Add((device.Key, key.Key));
                }
            }

            foreach (var released in releasedWhileInactive)
                deviceKeys[released.Device].Remove(released.PhysicalKey);
            releasedWhileInactive.Clear();
        }

        private readonly record struct PressedKey(InputKey Key, long Epoch, bool Suppressed, int NativeKey);
    }
}
