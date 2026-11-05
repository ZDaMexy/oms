// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Screens.Play
{
    /// <summary>Player-owned failure presentation. It cannot advance audio, replay, input or scoring clocks.</summary>
    internal sealed class GameplaySkinFailurePresentationController
    {
        private double duration;

        public double? Elapsed { get; private set; }

        public bool Finished => Elapsed.HasValue && Elapsed.Value >= duration;

        public bool IsActive => Elapsed.HasValue && !Finished;

        public void Begin(double durationMilliseconds)
        {
            if (!double.IsFinite(durationMilliseconds) || durationMilliseconds <= 0 || durationMilliseconds > GameplaySkinSceneBudgets.MAX_FAILURE_OUTRO)
                throw new ArgumentOutOfRangeException(nameof(durationMilliseconds));
            if (Elapsed.HasValue)
                throw new InvalidOperationException("Failure presentation can begin once before cancellation.");
            duration = durationMilliseconds;
            Elapsed = 0;
        }

        /// <summary>Called by the native Player outside the stopped frame-stable gameplay clock.</summary>
        public void Advance(double elapsedMilliseconds, bool paused)
        {
            if (!double.IsFinite(elapsedMilliseconds) || elapsedMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));
            if (IsActive && !paused)
                Elapsed = Math.Min(duration, Elapsed!.Value + elapsedMilliseconds);
        }

        /// <summary>Skip an active presentation without creating a failure or changing gameplay time.</summary>
        public void Finish()
        {
            if (IsActive)
                Elapsed = duration;
        }

        public void Cancel()
        {
            Elapsed = null;
            duration = 0;
        }
    }
}
