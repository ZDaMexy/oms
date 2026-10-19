// Copyright (c) OMS contributors. Licensed under the MIT Licence.

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// An optional, update-thread snapshot of the existing gameplay clock for timestamped live input.
    /// Native input producers only capture <see cref="System.Diagnostics.Stopwatch.GetTimestamp"/> values;
    /// they must not read the gameplay clock or its drawable hierarchy.
    /// </summary>
    public interface IGameplayInputTimeSource
    {
        GameplayInputTime? InputTime { get; }
    }

    /// <summary>
    /// A gameplay time, including all clock offsets, paired with a monotonic timestamp.
    /// Seek, pause, reset, source and offset discontinuities start a new epoch. Events predating its
    /// <see cref="EpochStartTimestamp"/> cannot be applied to the new timeline.
    /// </summary>
    public readonly record struct GameplayInputTime(double Time, long Timestamp, double Rate, bool IsRunning, long Epoch, long EpochStartTimestamp);
}
