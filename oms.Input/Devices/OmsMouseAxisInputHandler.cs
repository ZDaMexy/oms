// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;

namespace oms.Input.Devices
{
    /// <summary>
    /// Resolves mouse axis deltas into <see cref="OmsAction"/> transitions.
    /// Axis-bound actions emit a press/release pulse for each frame that
    /// observes matching movement and are released at frame end.
    /// </summary>
    public class OmsMouseAxisInputHandler
    {
        private readonly Action<OmsAction> pressAction;
        private readonly Action<OmsAction> releaseAction;
        private readonly Dictionary<string, MouseAxisTrigger> mouseTriggersBySignature;
        private readonly Dictionary<OmsMouseAxis, string[]> triggerSignaturesByAxis;
        private readonly HashSet<string> activeTriggerSignatures = new HashSet<string>();
        private readonly Dictionary<OmsAction, int> actionPressCounts = new Dictionary<OmsAction, int>();

        public OmsMouseAxisInputHandler(IEnumerable<OmsBinding> bindings, Action<OmsAction> pressAction, Action<OmsAction> releaseAction)
        {
            this.pressAction = pressAction;
            this.releaseAction = releaseAction;

            var mouseTriggers = bindings
                                .SelectMany(binding => binding.MouseAxisTriggers.Select(trigger => new MouseAxisTrigger(binding.Action, trigger.MouseAxisKind, trigger.AxisDirection, trigger.AxisInverted)))
                                .GroupBy(trigger => trigger.Signature)
                                .Select(group => group.First())
                                .ToArray();

            mouseTriggersBySignature = mouseTriggers.ToDictionary(trigger => trigger.Signature);
            triggerSignaturesByAxis = mouseTriggers
                                      .GroupBy(trigger => trigger.Axis)
                                      .ToDictionary(group => group.Key, group => group.Select(trigger => trigger.Signature).ToArray());
        }

        public void BeginFrame()
        {
        }

        public bool ApplyAxisDelta(OmsMouseAxis axis, float delta)
        {
            if (Math.Abs(delta) <= float.Epsilon || !triggerSignaturesByAxis.TryGetValue(axis, out var signatures))
                return false;

            var signaturesToActivate = new List<string>();
            var signaturesToDeactivate = new List<string>();

            foreach (var signature in signatures)
            {
                var trigger = mouseTriggersBySignature[signature];
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

            // Direction reversals within the same frame should emit a fresh pulse.
            foreach (var signature in signaturesToActivate)
                changed |= activate(signature);

            return changed;
        }

        public bool FinishFrame()
        {
            bool changed = false;

            foreach (var signature in activeTriggerSignatures.ToArray())
                changed |= deactivate(signature);

            return changed;
        }

        private bool activate(string signature)
        {
            if (!activeTriggerSignatures.Add(signature))
                return false;

            var action = mouseTriggersBySignature[signature].Action;
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

            var action = mouseTriggersBySignature[signature].Action;
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

        private static bool shouldTrigger(float delta, OmsAxisDirection direction, bool axisInverted)
        {
            float effectiveDelta = axisInverted ? -delta : delta;

            return direction == OmsAxisDirection.Positive
                ? effectiveDelta > 0
                : effectiveDelta < 0;
        }

        private readonly struct MouseAxisTrigger
        {
            public OmsAction Action { get; }

            public OmsMouseAxis Axis { get; }

            public OmsAxisDirection AxisDirection { get; }

            public bool AxisInverted { get; }

            public string Signature => $"{(int)Action}:{(int)Axis}:{(int)AxisDirection}:{AxisInverted}";

            public MouseAxisTrigger(OmsAction action, OmsMouseAxis axis, OmsAxisDirection axisDirection, bool axisInverted)
            {
                Action = action;
                Axis = axis;
                AxisDirection = axisDirection;
                AxisInverted = axisInverted;
            }
        }
    }
}
