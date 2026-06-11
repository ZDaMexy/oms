// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using osu.Game.Audio;

namespace osu.Game.Rulesets.Mania.Objects
{
    /// <summary>
    /// A shared keysound playback store that a converted chart (e.g. BMS) can host so a pooled
    /// <see cref="Drawables.DrawableNote"/> routes its keysound through a bounded, pause/seek-aware channel pool with
    /// per-WAV cut, instead of mania's per-object one-shot sample. The hosting ruleset caches the store under this
    /// interface; note drawables resolve it optionally. Defined in mania so the note drawables need no compile-time
    /// reference to the source ruleset assembly (J6 / P1-J #10).
    /// </summary>
    public interface IManiaKeysoundStore
    {
        /// <summary>
        /// Plays a single keysound. When <paramref name="cutGroup"/> (the source WAV slot) is non-null it enables
        /// per-WAV cut: re-triggering the same still-sounding slot restarts it rather than stacking an overlapping copy.
        /// </summary>
        /// <param name="sample">The keysound to play.</param>
        /// <param name="balance">Stereo balance in the range [-1, 1].</param>
        /// <param name="cutGroup">The per-WAV cut group (source WAV slot), or null for uncut playback.</param>
        void Play(ISampleInfo sample, double balance, int? cutGroup);
    }
}
