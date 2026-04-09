// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;

namespace oms.Input.Devices
{
    /// <summary>
    /// Resolves HID axis deltas into <see cref="OmsAction"/> transitions.
    /// Axis-bound actions emit a press/release pulse for each poll that
    /// observes matching movement and are released at poll end.
    /// </summary>
    public class OmsHidAxisInputHandler
    {
        private readonly Action<OmsAction> pressAction;
        private readonly Action<OmsAction> releaseAction;
        private readonly Dictionary<string, HidAxisTrigger> hidTriggersBySignature;
        private readonly Dictionary<string, string[]> triggerSignaturesByAxis;
        private readonly HashSet<string> activeTriggerSignatures = new HashSet<string>();
        private readonly Dictionary<OmsAction, int> actionPressCounts = new Dictionary<OmsAction, int>();

        public OmsHidAxisInputHandler(IEnumerable<OmsBinding> bindings, Action<OmsAction> pressAction, Action<OmsAction> releaseAction)
        {
            this.pressAction = pressAction;
            this.releaseAction = releaseAction;

            var hidTriggers = bindings
                              .SelectMany(binding => binding.HidAxisTriggers.Select(trigger => new HidAxisTrigger(binding.Action, OmsHidDeviceIdentifier.Normalize(trigger.DeviceIdentifier!), trigger.AxisIndex, trigger.AxisDirection, trigger.AxisInverted)))
                              .GroupBy(trigger => trigger.Signature)
                              .Select(group => group.First())
                              .ToArray();

            hidTriggersBySignature = hidTriggers.ToDictionary(trigger => trigger.Signature);
            triggerSignaturesByAxis = hidTriggers
                                      .GroupBy(trigger => trigger.DeviceAxisSignature)
                                      .ToDictionary(group => group.Key, group => group.Select(trigger => trigger.Signature).ToArray());
        }

        public void BeginPolling()
        {
        }

        public bool ApplyAxisDelta(string deviceIdentifier, int axisIndex, int delta)
        {
            if (!triggerSignaturesByAxis.TryGetValue(buildDeviceAxisSignature(deviceIdentifier, axisIndex), out var signatures))
                return false;

            var signaturesToActivate = new List<string>();
            var signaturesToDeactivate = new List<string>();

            foreach (var signature in signatures)
            {
                var trigger = hidTriggersBySignature[signature];
                bool isActive = activeTriggerSignatures.Contains(signature);
                bool shouldBeActive = shouldTrigger(delta, trigger.AxisDirection, trigger.AxisInverted);

                if (isActive == shouldBeActive)
                    continue;

                if (shouldBeActive)
                    signaturesToActivate.Add(signature);
                else
                    signaturesToDeactivate.Add(signature);
            }

            bool changed = false;

            foreach (var signature in signaturesToDeactivate)
                changed |= deactivate(signature);

            // Direction reversals within the same poll should emit a fresh pulse.
            foreach (var signature in signaturesToActivate)
                changed |= activate(signature);

            return changed;
        }

        public bool FinishPolling()
        {
            bool changed = false;

            foreach (var signature in activeTriggerSignatures.ToArray())
                changed |= deactivate(signature);

            return changed;
        }

        public bool ReleaseDevice(string deviceIdentifier)
        {
            string normalizedIdentifier = OmsHidDeviceIdentifier.Normalize(deviceIdentifier);
            bool changed = false;

            foreach (var signature in activeTriggerSignatures.ToArray())
            {
                if (hidTriggersBySignature[signature].DeviceIdentifier != normalizedIdentifier)
                    continue;

                changed |= deactivate(signature);
            }

            return changed;
        }

        private bool activate(string signature)
        {
            if (!activeTriggerSignatures.Add(signature))
                return false;

            var action = hidTriggersBySignature[signature].Action;
            int count = actionPressCounts.GetValueOrDefault(action);
            actionPressCounts[action] = count + 1;

            if (count > 0)
                return false;

            pressAction(action);
            return true;
        }

        private bool deactivate(string signature)
        {
            if (!activeTriggerSignatures.Remove(signature))
                return false;

            var action = hidTriggersBySignature[signature].Action;
            int count = actionPressCounts.GetValueOrDefault(action);

            if (count <= 1)
            {
                actionPressCounts.Remove(action);

                if (count == 1)
                {
                    releaseAction(action);
                    return true;
                }

                return false;
            }

            actionPressCounts[action] = count - 1;
            return false;
        }

        private static bool shouldTrigger(int delta, OmsAxisDirection direction, bool axisInverted)
        {
            int effectiveDelta = axisInverted ? -delta : delta;

            return direction == OmsAxisDirection.Positive
                ? effectiveDelta > 0
                : effectiveDelta < 0;
        }

        private static string buildDeviceAxisSignature(string deviceIdentifier, int axisIndex)
            => $"{OmsHidDeviceIdentifier.Normalize(deviceIdentifier)}:{axisIndex}";

        private readonly struct HidAxisTrigger
        {
            public OmsAction Action { get; }

            public string DeviceIdentifier { get; }

            public int AxisIndex { get; }

            public OmsAxisDirection AxisDirection { get; }

            public bool AxisInverted { get; }

            public string DeviceAxisSignature => buildDeviceAxisSignature(DeviceIdentifier, AxisIndex);

            public string Signature => $"{(int)Action}:{DeviceAxisSignature}:{(int)AxisDirection}:{AxisInverted}";

            public HidAxisTrigger(OmsAction action, string deviceIdentifier, int axisIndex, OmsAxisDirection axisDirection, bool axisInverted)
            {
                Action = action;
                DeviceIdentifier = deviceIdentifier;
                AxisIndex = axisIndex;
                AxisDirection = axisDirection;
                AxisInverted = axisInverted;
            }
        }
    }
}
