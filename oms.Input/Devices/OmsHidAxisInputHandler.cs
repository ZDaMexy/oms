// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;

namespace oms.Input.Devices
{
    /// <summary>
    /// Resolves explicitly configured HID axes into actions. Motion modes preserve the existing
    /// per-poll pulse contract; position buttons remain held until their release threshold.
    /// </summary>
    public class OmsHidAxisInputHandler
    {
        private readonly Action<OmsAction> pressAction;
        private readonly Action<OmsAction> releaseAction;
        private readonly Action<OmsAxisMotion>? acceptedMotion;
        private readonly Dictionary<(string DeviceIdentifier, int AxisIndex), HidAxisTrigger[]> triggersByAxis;
        private readonly HidAxisTrigger[] triggers;
        private readonly Dictionary<OmsAction, int> actionPressCounts = new Dictionary<OmsAction, int>();
        private double? lastUpdateTime;

        public OmsHidAxisInputHandler(IEnumerable<OmsBinding> bindings, Action<OmsAction> pressAction, Action<OmsAction> releaseAction,
                                     Action<OmsAxisMotion>? acceptedMotion = null)
        {
            this.pressAction = pressAction;
            this.releaseAction = releaseAction;
            this.acceptedMotion = acceptedMotion;

            triggers = bindings
                       .SelectMany(binding => binding.HidAxisTriggers.Select(trigger => new HidAxisTrigger(binding.Action, trigger)))
                       .GroupBy(trigger => (trigger.Action, trigger.DeviceIdentifier, trigger.AxisIndex, trigger.Direction, trigger.Inverted, trigger.Settings))
                       .Select(group => group.First())
                       .ToArray();
            triggersByAxis = triggers.GroupBy(trigger => (trigger.DeviceIdentifier, trigger.AxisIndex))
                                     .ToDictionary(group => group.Key, group => group.ToArray());

            int sourceIndex = 0;
            foreach (var source in triggers.GroupBy(trigger => (trigger.Action, trigger.DeviceIdentifier, trigger.AxisIndex, trigger.Inverted, trigger.Settings)))
            {
                string sourceId = FormattableString.Invariant($"hid:{source.Key.DeviceIdentifier}:{source.Key.AxisIndex}:{(int)source.Key.Action}:{sourceIndex++}");
                foreach (var trigger in source)
                    trigger.SourceId = sourceId;
            }

            foreach (var trigger in triggers)
                actionPressCounts.TryAdd(trigger.Action, 0);
        }

        public void BeginPolling()
        {
        }

        public bool ApplyAxisDelta(string deviceIdentifier, int axisIndex, int delta)
            => ApplyAxisChange(new OmsHidAxisChange(deviceIdentifier, axisIndex, delta));

        public bool ApplyAxisChange(OmsHidAxisChange change)
        {
            if (!triggersByAxis.TryGetValue((change.DeviceIdentifier, change.AxisIndex), out var axisTriggers))
                return false;

            foreach (var trigger in axisTriggers)
                trigger.Apply(change);

            return applyTransitions(axisTriggers);
        }

        /// <summary>
        /// Advances stick-to-motion bindings using a monotonic clock in milliseconds. Call before
        /// applying a new batch, so a new position is not applied to time before its arrival.
        /// </summary>
        public bool Update(double currentTimeMilliseconds)
        {
            if (!double.IsFinite(currentTimeMilliseconds))
                throw new ArgumentOutOfRangeException(nameof(currentTimeMilliseconds));

            double elapsedSeconds = lastUpdateTime.HasValue ? (currentTimeMilliseconds - lastUpdateTime.Value) / 1000 : 0;
            lastUpdateTime = currentTimeMilliseconds;
            if (elapsedSeconds <= 0)
                return false;

            foreach (var trigger in triggers)
            {
                if (trigger.Settings.Mode == OmsAxisMode.StickToMotion)
                    trigger.Advance(elapsedSeconds);
            }

            return applyTransitions(triggers, stickOnly: true);
        }

        public bool FinishPolling()
        {
            bool changed = false;
            foreach (var trigger in triggers)
            {
                if (trigger.Settings.Mode != OmsAxisMode.CentredPosition)
                    changed |= deactivate(trigger);
            }

            return changed;
        }

        public bool ReleaseDevice(string deviceIdentifier)
        {
            string normalizedIdentifier = OmsHidDeviceIdentifier.Normalize(deviceIdentifier);
            bool changed = false;
            foreach (var trigger in triggers)
            {
                if (trigger.DeviceIdentifier != normalizedIdentifier)
                    continue;

                changed |= deactivate(trigger);
                trigger.Reset();
            }

            return changed;
        }

        public bool ReleaseAll()
        {
            bool changed = false;
            foreach (var trigger in triggers)
            {
                changed |= deactivate(trigger);
                trigger.Reset();
            }

            lastUpdateTime = null;
            return changed;
        }

        public bool Reset() => ReleaseAll();

        private bool applyTransitions(HidAxisTrigger[] axisTriggers, bool stickOnly = false)
        {
            bool changed = false;
            foreach (var trigger in axisTriggers)
            {
                if ((!stickOnly || trigger.Settings.Mode == OmsAxisMode.StickToMotion) && !trigger.ShouldBeActive)
                    changed |= deactivate(trigger);
            }

            // Release every old direction before pressing the new one, including when both
            // directions share an action and reverse within a single poll.
            foreach (var trigger in axisTriggers)
            {
                if ((stickOnly && trigger.Settings.Mode != OmsAxisMode.StickToMotion) || !trigger.ShouldBeActive)
                    continue;

                if (trigger.Movement != 0)
                {
                    acceptedMotion?.Invoke(new OmsAxisMotion(trigger.Action, trigger.Settings.Mode, trigger.Movement,
                        trigger.SourceId, trigger.DeviceIdentifier, trigger.Settings.StopTimeoutMilliseconds));
                }

                changed |= activate(trigger);
            }

            return changed;
        }

        private bool activate(HidAxisTrigger trigger)
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

        private bool deactivate(HidAxisTrigger trigger)
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

        private sealed class HidAxisTrigger
        {
            public OmsAction Action { get; }
            public string DeviceIdentifier { get; }
            public int AxisIndex { get; }
            public OmsAxisDirection Direction { get; }
            public bool Inverted { get; }
            public OmsAxisSettings Settings { get; }
            public string SourceId { get; set; } = string.Empty;
            public bool Active { get; set; }
            public bool ShouldBeActive { get; private set; }
            public double Movement { get; private set; }

            private int? previousPosition;
            private int previousMinimum;
            private int previousMaximum;
            private double stickDeflection;
            private double pendingMovement;

            public HidAxisTrigger(OmsAction action, OmsBindingTrigger trigger)
            {
                Action = action;
                DeviceIdentifier = OmsHidDeviceIdentifier.Normalize(trigger.DeviceIdentifier!);
                AxisIndex = trigger.AxisIndex;
                Direction = trigger.AxisDirection;
                Inverted = trigger.AxisInverted;
                Settings = trigger.AxisSettings;
            }

            public void Apply(OmsHidAxisChange change)
            {
                Movement = 0;
                ShouldBeActive = false;
                if (Settings.Mode == OmsAxisMode.RelativeMotion)
                {
                    applyMovement(change.Delta);
                    return;
                }

                if (!tryGetPosition(change, out int value, out int minimum, out int maximum))
                {
                    Reset();
                    return;
                }

                if (Settings.Mode == OmsAxisMode.CircularPosition)
                {
                    // Resetting the consumer does not reset the device's absolute-position baseline.
                    // A real, nonzero raw delta can recover that previous position in the same range;
                    // a new device's zero-delta report remains calibration only.
                    if (!previousPosition.HasValue && change.Delta != 0)
                    {
                        long reportedPrevious = (long)value - change.Delta;
                        if (reportedPrevious >= minimum && reportedPrevious <= maximum)
                        {
                            previousPosition = (int)reportedPrevious;
                            previousMinimum = minimum;
                            previousMaximum = maximum;
                        }
                    }

                    if (previousPosition.HasValue && previousMinimum == minimum && previousMaximum == maximum)
                    {
                        double period = (long)maximum - minimum + 1;
                        double delta = (long)value - previousPosition.Value;
                        if (delta > period / 2)
                            delta -= period;
                        else if (delta < -period / 2)
                            delta += period;

                        applyMovement(delta / period);
                    }
                    else
                        pendingMovement = 0;

                    previousPosition = value;
                    previousMinimum = minimum;
                    previousMaximum = maximum;
                    return;
                }

                double centre = Settings.Centre ?? ((double)minimum + maximum) / 2;
                if (centre <= minimum || centre >= maximum)
                {
                    Reset();
                    return;
                }

                double deflection = value >= centre ? (value - centre) / (maximum - centre) : (value - centre) / (centre - minimum);
                if (Settings.Mode == OmsAxisMode.CentredPosition)
                {
                    double travel = (Inverted ? -deflection : deflection) * (int)Direction;
                    ShouldBeActive = Active
                        ? travel > 0 && travel >= Settings.ReleaseThreshold
                        : travel >= Settings.ActivationThreshold;
                }
                else
                {
                    double magnitude = Math.Abs(deflection);
                    stickDeflection = magnitude <= Settings.Deadzone ? 0 : Math.CopySign((magnitude - Settings.Deadzone) / (1 - Settings.Deadzone), deflection);
                    ShouldBeActive = Active && matchesDirection(Inverted ? -stickDeflection : stickDeflection);
                    if (stickDeflection == 0)
                        pendingMovement = 0;
                }
            }

            public void Advance(double elapsedSeconds)
                => applyMovement(stickDeflection * Settings.RevolutionsPerSecond * elapsedSeconds);

            public void Reset()
            {
                previousPosition = null;
                stickDeflection = pendingMovement = Movement = 0;
                ShouldBeActive = false;
            }

            private void applyMovement(double delta)
            {
                pendingMovement += delta * Settings.Sensitivity * (Inverted ? -1 : 1);
                Movement = 0;
                ShouldBeActive = false;
                if (Math.Abs(pendingMovement) <= Settings.MovementThreshold)
                    return;

                double movement = pendingMovement;
                pendingMovement = 0;
                if (matchesDirection(movement))
                {
                    Movement = movement;
                    ShouldBeActive = true;
                }
            }

            private bool matchesDirection(double value)
                => Direction == OmsAxisDirection.Positive ? value > 0 : value < 0;

            private bool tryGetPosition(OmsHidAxisChange change, out int value, out int minimum, out int maximum)
            {
                value = change.Value.GetValueOrDefault();
                int? reportMinimum = Settings.Minimum ?? change.Minimum;
                int? reportMaximum = Settings.Maximum ?? change.Maximum;
                minimum = reportMinimum.GetValueOrDefault();
                maximum = reportMaximum.GetValueOrDefault();
                if (change.IsRelative || !change.Value.HasValue || !reportMinimum.HasValue || !reportMaximum.HasValue || minimum >= maximum)
                    return false;

                if (Settings.Mode == OmsAxisMode.CircularPosition)
                    return value >= minimum && value <= maximum;

                value = Math.Clamp(value, minimum, maximum);
                return true;
            }
        }
    }
}
