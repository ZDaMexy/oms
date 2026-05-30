// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Beatmaps.ControlPoints;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// A <see cref="TimingControlPoint"/> emitted by <see cref="BmsBeatmapConverter"/> to mark the start of a BMS
    /// <c>STOP</c> region. The point exists so the BMS playable surface can freeze scroll during the stop, but it is
    /// not a real BPM/timing change and must be stripped during BMS -> mania conversion (see K9 #16).
    /// </summary>
    /// <remarks>
    /// Using a dedicated subclass — rather than a sentinel <see cref="TimingControlPoint.BeatLength"/> value — avoids
    /// collisions with extreme but legal BMS BPMs. <see cref="TimingControlPoint.BeatLengthBindable"/> clamps to
    /// <c>[6, 60000]</c> ms, so a real BPM = 10000 chart writes <c>BeatLength = 6</c> and was previously
    /// indistinguishable from a value-tagged STOP-freeze marker.
    /// </remarks>
    public class BmsStopFreezeTimingControlPoint : TimingControlPoint
    {
    }
}
