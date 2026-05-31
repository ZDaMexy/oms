// Copyright (c) OMS contributors. Licensed under the MIT Licence.

namespace osu.Game.Rulesets.Bms.Audio
{
    /// <summary>
    /// A time-ordered keysound assignment for a single playable lane, used to resolve which keysound an empty
    /// (note-less) key press should play. Built from visible notes, long-note head/tail keysounds and invisible
    /// (channel 31-49) keysound objects. <see cref="KeysoundId"/> is the BMS WAV slot (#WAVxx / object id), used as
    /// the per-WAV cut group so a re-struck lane sound restarts its own slot without grouping different slots that
    /// happen to share a file.
    /// </summary>
    public readonly record struct BmsLaneKeysoundEntry(double Time, int KeysoundId, BmsKeysoundSampleInfo Sample);
}
