// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Rulesets.UI;

namespace osu.Game.Screens.Play.HUD.ClicksPerSecond
{
    public partial class ClicksPerSecondController : Component
    {
        private readonly List<double> timestamps = new List<double>();

        [Resolved]
        private IGameplayClock gameplayClock { get; set; } = null!;

        [Resolved]
        private IFrameStableClock? frameStableClock { get; set; }

        public int Value { get; private set; }

        private IGameplayClock clock => frameStableClock ?? gameplayClock;

        public ClicksPerSecondController()
        {
            RelativeSizeAxes = Axes.Both;
        }

        public void AddInputTimestamp()
        {
            double time = clock.CurrentTime;
            discardFutureInputs(time);
            timestamps.Add(time);
        }

        protected override void Update()
        {
            base.Update();

            double latestValidTime = clock.CurrentTime;
            double earliestTimeValid = latestValidTime - 1000 * gameplayClock.GetTrueGameplayRate();

            discardFutureInputs(latestValidTime);
            Value = timestamps.Count - boundary(earliestTimeValid, includeEqual: false);
        }

        private void discardFutureInputs(double time)
        {
            if (timestamps.Count == 0 || timestamps[^1] <= time)
                return;

            int firstFuture = boundary(time, includeEqual: true);
            if (firstFuture < timestamps.Count)
                timestamps.RemoveRange(firstFuture, timestamps.Count - firstFuture);
        }

        // Keep prior inputs for rewind, but do not scan the entire song every frame.
        private int boundary(double time, bool includeEqual)
        {
            int first = 0;
            int last = timestamps.Count;
            while (first < last)
            {
                int middle = first + (last - first) / 2;
                if (timestamps[middle] < time || includeEqual && timestamps[middle] == time)
                    first = middle + 1;
                else
                    last = middle;
            }

            return first;
        }
    }
}
