// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// Allocation-free adapter from the converter-owned BMS timing/scroll profiles to the neutral read-only event seam.
    /// </summary>
    internal sealed class BmsGameplaySkinTimingProjection : IGameplaySkinTimingProjection
    {
        private readonly BmsTimingProfile timingProfile;
        private readonly BmsScrollProfile scrollProfile;

        public BmsGameplaySkinTimingProjection(BmsBeatmap beatmap)
        {
            ArgumentNullException.ThrowIfNull(beatmap);
            timingProfile = beatmap.TimingProfile
                            ?? throw new ArgumentException("A production BMS timing projection requires the converter-owned timing profile.", nameof(beatmap));
            scrollProfile = beatmap.ScrollProfile
                            ?? throw new ArgumentException("A production BMS timing projection requires the converter-owned scroll profile.", nameof(beatmap));
        }

        public GameplaySkinTimingStateSnapshot Sample(double gameplayTime)
        {
            BmsTimingSample timing = timingProfile.Sample(gameplayTime);
            double scroll = scrollProfile.PositionDelta(gameplayTime, gameplayTime + 1);

            // The public contract represents a visual freeze with IsStopped and requires a finite non-zero multiplier.
            if (Math.Abs(scroll) < 1e-9)
                scroll = 1e-9;

            return new GameplaySkinTimingStateSnapshot(timing.Beat, timing.BarIndex, timing.Bpm, timing.IsStopped, scroll);
        }
    }
}
