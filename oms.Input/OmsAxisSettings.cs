// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace oms.Input
{
    public enum OmsAxisMode
    {
        RelativeMotion,
        CircularPosition,
        CentredPosition,
        StickToMotion,
    }

    /// <summary>
    /// Explicit interpretation of a bound axis. Construction validates saved or edited settings once.
    /// Device capabilities do not choose a mode on the player's behalf.
    /// </summary>
    public sealed record OmsAxisSettings
    {
        public static OmsAxisSettings Default { get; } = new OmsAxisSettings();

        public OmsAxisMode Mode { get; }

        /// <summary>Motion multiplier, from 0.01 to 100. Does not affect position-button thresholds.</summary>
        public double Sensitivity { get; }

        /// <summary>
        /// Accumulate displacement until it exceeds this magnitude, after sensitivity.
        /// Opposite movement cancels pending displacement; zero preserves every nonzero input.
        /// Units are input counts for relative motion and revolutions for circular/stick motion.
        /// </summary>
        public double MovementThreshold { get; }

        /// <summary>Neutral deadzone as a fraction of stick travel, from 0 to 0.99. Stick-to-motion only.</summary>
        public double Deadzone { get; }

        /// <summary>Position-button press threshold as a fraction of travel from centre, from 0.01 to 1.</summary>
        public double ActivationThreshold { get; }

        /// <summary>Position-button release threshold, from 0 to the activation threshold.</summary>
        public double ReleaseThreshold { get; }

        /// <summary>Stick-to-motion speed at full travel before sensitivity, from 0.01 to 100 revolutions/second.</summary>
        public double RevolutionsPerSecond { get; }

        /// <summary>Motion stop waiting time, from 1 to 250 milliseconds. Not an input delay.</summary>
        public double StopTimeoutMilliseconds { get; }

        /// <summary>Optional explicit calibration; otherwise use the report's declared range.</summary>
        public int? Minimum { get; }

        public int? Maximum { get; }

        /// <summary>Optional neutral position in device units; otherwise use the midpoint of the range.</summary>
        public double? Centre { get; }

        public OmsAxisSettings(
            OmsAxisMode mode = OmsAxisMode.RelativeMotion,
            double sensitivity = 1,
            double movementThreshold = 0,
            double deadzone = 0.1,
            double activationThreshold = 0.5,
            double releaseThreshold = 0.4,
            double revolutionsPerSecond = 1,
            double stopTimeoutMilliseconds = 50,
            int? minimum = null,
            int? maximum = null,
            double? centre = null)
        {
            if (!Enum.IsDefined(mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (!double.IsFinite(sensitivity) || sensitivity < 0.01 || sensitivity > 100)
                throw new ArgumentOutOfRangeException(nameof(sensitivity));
            if (!double.IsFinite(movementThreshold) || movementThreshold < 0 || movementThreshold > 1_000_000)
                throw new ArgumentOutOfRangeException(nameof(movementThreshold));
            if (!double.IsFinite(deadzone) || deadzone < 0 || deadzone > 0.99)
                throw new ArgumentOutOfRangeException(nameof(deadzone));
            if (!double.IsFinite(activationThreshold) || activationThreshold < 0.01 || activationThreshold > 1)
                throw new ArgumentOutOfRangeException(nameof(activationThreshold));
            if (!double.IsFinite(releaseThreshold) || releaseThreshold < 0 || releaseThreshold > activationThreshold)
                throw new ArgumentOutOfRangeException(nameof(releaseThreshold));
            if (!double.IsFinite(revolutionsPerSecond) || revolutionsPerSecond < 0.01 || revolutionsPerSecond > 100)
                throw new ArgumentOutOfRangeException(nameof(revolutionsPerSecond));
            if (!double.IsFinite(stopTimeoutMilliseconds) || stopTimeoutMilliseconds < 1 || stopTimeoutMilliseconds > 250)
                throw new ArgumentOutOfRangeException(nameof(stopTimeoutMilliseconds));
            if (minimum.HasValue != maximum.HasValue || minimum >= maximum)
                throw new ArgumentException("Axis calibration requires both minimum and maximum, with minimum less than maximum.");
            if (centre.HasValue && (!double.IsFinite(centre.Value) || (minimum.HasValue && (centre <= minimum || centre >= maximum))))
                throw new ArgumentOutOfRangeException(nameof(centre));

            Mode = mode;
            Sensitivity = sensitivity;
            MovementThreshold = movementThreshold;
            Deadzone = deadzone;
            ActivationThreshold = activationThreshold;
            ReleaseThreshold = releaseThreshold;
            RevolutionsPerSecond = revolutionsPerSecond;
            StopTimeoutMilliseconds = stopTimeoutMilliseconds;
            Minimum = minimum;
            Maximum = maximum;
            Centre = centre;
        }
    }

    /// <summary>
    /// Accepted, signed movement for an action, with binding inversion already applied.
    /// Relative deltas use input counts; circular and stick deltas use revolutions.
    /// This observation does not alter the action's press/release contract.
    /// </summary>
    public readonly struct OmsAxisMotion
    {
        public OmsAction Action { get; }

        public OmsAxisMode Mode { get; }

        public double Delta { get; }

        public string SourceId { get; }

        public string? DeviceIdentifier { get; }

        public double StopTimeoutMilliseconds { get; }

        public OmsAxisMotion(OmsAction action, OmsAxisMode mode, double delta, string sourceId, string? deviceIdentifier, double stopTimeoutMilliseconds)
        {
            Action = action;
            Mode = mode;
            Delta = delta;
            SourceId = sourceId;
            DeviceIdentifier = deviceIdentifier;
            StopTimeoutMilliseconds = stopTimeoutMilliseconds;
        }
    }
}
