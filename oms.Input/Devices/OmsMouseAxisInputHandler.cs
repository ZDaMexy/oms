// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;

namespace oms.Input.Devices
{
    /// <summary>
    /// Resolves relative mouse movement into the existing per-frame action pulses, with an
    /// independent signed movement observation. Runtime processing reuses constructor-built storage.
    /// </summary>
    public class OmsMouseAxisInputHandler
    {
        private readonly Action<OmsAction> pressAction;
        private readonly Action<OmsAction> releaseAction;
        private readonly Action<OmsAxisMotion>? acceptedMotion;
        private readonly MouseAxisTrigger[] triggers;
        private readonly Dictionary<OmsMouseAxis, MouseAxisTrigger[]> triggersByAxis;
        private readonly Dictionary<OmsAction, int> actionPressCounts = new Dictionary<OmsAction, int>();

        public OmsMouseAxisInputHandler(IEnumerable<OmsBinding> bindings, Action<OmsAction> pressAction, Action<OmsAction> releaseAction,
                                       Action<OmsAxisMotion>? acceptedMotion = null)
        {
            this.pressAction = pressAction;
            this.releaseAction = releaseAction;
            this.acceptedMotion = acceptedMotion;

            triggers = bindings
                       .SelectMany(binding => binding.MouseAxisTriggers.Select(trigger => new MouseAxisTrigger(binding.Action, trigger)))
                       .GroupBy(trigger => (trigger.Action, trigger.Axis, trigger.Direction, trigger.Inverted, trigger.Settings))
                       .Select(group => group.First())
                       .ToArray();
            triggersByAxis = triggers.GroupBy(trigger => trigger.Axis)
                                     .ToDictionary(group => group.Key, group => group.ToArray());

            int sourceIndex = 0;
            foreach (var source in triggers.GroupBy(trigger => (trigger.Action, trigger.Axis, trigger.Inverted, trigger.Settings)))
            {
                string sourceId = FormattableString.Invariant($"mouse:{(int)source.Key.Axis}:{(int)source.Key.Action}:{sourceIndex++}");
                foreach (var trigger in source)
                    trigger.SourceId = sourceId;
            }

            foreach (var trigger in triggers)
                actionPressCounts.TryAdd(trigger.Action, 0);
        }

        public void BeginFrame()
        {
        }

        public bool ApplyAxisDelta(OmsMouseAxis axis, float delta)
        {
            if (!float.IsFinite(delta) || Math.Abs(delta) <= float.Epsilon || !triggersByAxis.TryGetValue(axis, out var axisTriggers))
                return false;

            foreach (var trigger in axisTriggers)
                trigger.Apply(delta);

            bool changed = false;
            foreach (var trigger in axisTriggers)
            {
                if (!trigger.ShouldBeActive)
                    changed |= deactivate(trigger);
            }

            // Reverse direction by releasing the old trigger before pressing the new one.
            foreach (var trigger in axisTriggers)
            {
                if (!trigger.ShouldBeActive)
                    continue;

                acceptedMotion?.Invoke(new OmsAxisMotion(trigger.Action, OmsAxisMode.RelativeMotion, trigger.Movement,
                    trigger.SourceId, null, trigger.Settings.StopTimeoutMilliseconds));
                changed |= activate(trigger);
            }

            return changed;
        }

        public bool FinishFrame()
        {
            bool changed = false;
            foreach (var trigger in triggers)
                changed |= deactivate(trigger);

            return changed;
        }

        public bool ReleaseAll()
        {
            bool changed = FinishFrame();
            foreach (var trigger in triggers)
                trigger.Reset();

            return changed;
        }

        public bool Reset() => ReleaseAll();

        private bool activate(MouseAxisTrigger trigger)
        {
            if (trigger.Active)
                return false;

            trigger.Active = true;
            int count = actionPressCounts[trigger.Action]++;
            if (count != 0)
                return false;

            pressAction(trigger.Action);
            return true;
        }

        private bool deactivate(MouseAxisTrigger trigger)
        {
            if (!trigger.Active)
                return false;

            trigger.Active = false;
            int count = --actionPressCounts[trigger.Action];
            if (count != 0)
                return false;

            releaseAction(trigger.Action);
            return true;
        }

        private sealed class MouseAxisTrigger
        {
            public OmsAction Action { get; }
            public OmsMouseAxis Axis { get; }
            public OmsAxisDirection Direction { get; }
            public bool Inverted { get; }
            public OmsAxisSettings Settings { get; }
            public string SourceId { get; set; } = string.Empty;
            public bool Active { get; set; }
            public bool ShouldBeActive { get; private set; }
            public double Movement { get; private set; }

            private double pendingMovement;

            public MouseAxisTrigger(OmsAction action, OmsBindingTrigger trigger)
            {
                Action = action;
                Axis = trigger.MouseAxisKind;
                Direction = trigger.AxisDirection;
                Inverted = trigger.AxisInverted;
                Settings = trigger.AxisSettings;
            }

            public void Apply(double delta)
            {
                pendingMovement += delta * Settings.Sensitivity * (Inverted ? -1 : 1);
                Movement = 0;
                ShouldBeActive = false;
                if (Math.Abs(pendingMovement) <= Settings.MovementThreshold)
                    return;

                double movement = pendingMovement;
                pendingMovement = 0;
                if (Direction == OmsAxisDirection.Positive ? movement > 0 : movement < 0)
                {
                    Movement = movement;
                    ShouldBeActive = true;
                }
            }

            public void Reset()
            {
                pendingMovement = Movement = 0;
                ShouldBeActive = false;
            }
        }
    }
}
