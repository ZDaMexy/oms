// Copyright (c) OMS contributors. Licensed under the MIT Licence.

namespace osu.Game.Rulesets.Bms.Audio
{
    /// <summary>
    /// A time-ordered keysound assignment for a single playable lane, used to resolve which keysound an empty
    /// (note-less) key press should play. Built from visible notes, long-note head/tail keysounds and invisible
    /// (channel 31-49) keysound objects.
    /// </summary>
    public readonly record struct BmsLaneKeysoundEntry(double Time, BmsKeysoundSampleInfo Sample);
}
