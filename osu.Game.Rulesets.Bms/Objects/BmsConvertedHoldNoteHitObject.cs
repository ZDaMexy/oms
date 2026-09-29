// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Audio;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Bms.Objects
{
    /// <summary>
    /// Carries the original BMS head keysound and WAV slot for automatic playback.
    /// The ordinary mania hold, nested objects and manual sample playback remain unchanged.
    /// </summary>
    public class BmsConvertedHoldNoteHitObject : HoldNote, IHasManiaKeysound
    {
        public BmsKeysoundSampleInfo? KeysoundSample { get; set; }

        public int? KeysoundId { get; set; }

        ISampleInfo? IHasManiaKeysound.KeysoundSample => KeysoundSample;

        int? IHasManiaKeysound.KeysoundCutGroup => KeysoundId;
    }
}
