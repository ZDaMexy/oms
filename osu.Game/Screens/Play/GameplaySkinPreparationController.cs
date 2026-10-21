// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// Player-owned preparation time. It never reads or advances the audio, replay or judgement clocks.
    /// Consumers can only obtain a value snapshot through the player's cached instance.
    /// </summary>
    internal sealed class GameplaySkinPreparationController
    {
        private const double ready_duration = 1500;

        private double duration;
        private double elapsed;
        private double remaining;
        private bool available;
        private bool active;

        public bool IsConfigured => duration > 0;

        public bool IsHeld { get; private set; }

        public GameplaySkinPreparationStateSnapshot State
        {
            get
            {
                bool ready = active && !IsHeld && remaining <= ready_duration;
                return available
                    ? new GameplaySkinPreparationStateSnapshot(true, active, elapsed, active ? remaining : 0,
                        ready, ready ? ready_duration - remaining : 0)
                    : default;
            }
        }

        public void Configure(double durationMilliseconds)
        {
            if (!double.IsFinite(durationMilliseconds) || durationMilliseconds < 0 || durationMilliseconds > 10000)
                throw new ArgumentOutOfRangeException(nameof(durationMilliseconds));

            duration = durationMilliseconds;
            Cancel();
        }

        public void Begin()
        {
            if (!IsConfigured || available)
                throw new InvalidOperationException("Preparation can begin once, from a configured player.");

            available = active = true;
            elapsed = 0;
            remaining = duration;
            IsHeld = false;
        }

        /// <summary>
        /// Called once by the parent Player update, outside FrameStabilityContainer.
        /// A hold extends only the countdown; already completed artwork keeps its elapsed position.
        /// </summary>
        public bool Advance(double elapsedMilliseconds, bool paused)
        {
            if (!double.IsFinite(elapsedMilliseconds) || elapsedMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));

            if (!active || paused)
                return false;

            elapsed += elapsedMilliseconds;
            remaining = Math.Max(0, remaining - elapsedMilliseconds);
            return remaining == 0 && !IsHeld;
        }

        public void SetHold(bool held)
        {
            if (!active || held == IsHeld)
                return;

            // Preserve BMS's original contract: a press starts a fresh countdown;
            // releasing a hold which outlasted that countdown starts one more.
            if (held || remaining == 0)
                remaining = duration;

            IsHeld = held;
        }

        public void Complete()
        {
            if (!active)
                return;

            active = false;
            remaining = 0;
            IsHeld = false;
        }

        public void Cancel()
        {
            available = active = IsHeld = false;
            elapsed = remaining = 0;
        }
    }
}
