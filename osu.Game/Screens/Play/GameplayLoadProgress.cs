// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// A thread-safe channel for reporting determinate progress of a slow gameplay-load step (currently a BMS BGA video
    /// transcode) from deep in the loading component tree up to the loading screen's progress indicator
    /// (<see cref="ScanlineLoadingLayer"/>). Cached by <see cref="PlayerLoader"/> so both its child metadata display and
    /// the async-loaded player subtree resolve the same instance. When nothing reports progress the indicator stays
    /// indeterminate. Written from any thread (the transcode runs on a background task); read on the update thread.
    /// </summary>
    public class GameplayLoadProgress
    {
        private readonly object sync = new object();
        private bool active;
        private float fraction;

        /// <summary>Reports a determinate progress fraction (clamped to [0,1]) and marks the channel active.</summary>
        public void Report(double value)
        {
            lock (sync)
            {
                active = true;
                fraction = (float)Math.Clamp(value, 0, 1);
            }
        }

        /// <summary>Clears any reported progress (the indicator returns to indeterminate). Called at the start of each load.</summary>
        public void Reset()
        {
            lock (sync)
            {
                active = false;
                fraction = 0;
            }
        }

        /// <summary>Gets the current fraction; returns false when no determinate progress has been reported.</summary>
        public bool TryGet(out float value)
        {
            lock (sync)
            {
                value = fraction;
                return active;
            }
        }
    }
}
