// Copyright (c) OMS contributors. Licensed under the MIT Licence.

namespace osu.Game.Rulesets.Bms.Configuration
{
    /// <summary>
    /// Gating for the BMS stop-motion visual scroll bypass (P1-L Phase 2). Controls whether the gimmick rendering path
    /// (faithful extreme-BPM snap / STOP freeze / measure-length placement) takes over from the normal forward-scroll
    /// path. The bypass only changes <b>visual positioning</b>; judgement/scoring are unaffected in every mode.
    /// </summary>
    public enum BmsGimmickScrollMode
    {
        /// <summary>Always use the normal forward-scroll path. The safe default: no chart's rendering changes.</summary>
        Off,

        /// <summary>Always use the stop-motion bypass when a chart has a scroll profile (explicit opt-in).</summary>
        On,

        /// <summary>Use the bypass only for charts auto-detected as stop-motion gimmicks — extreme-BPM snap or meaningful STOP freeze (see BmsScrollProfile.IsStopMotionGimmick).</summary>
        Auto,
    }
}
