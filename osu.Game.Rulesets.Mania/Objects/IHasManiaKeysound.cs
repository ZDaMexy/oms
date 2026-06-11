// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using osu.Game.Audio;

namespace osu.Game.Rulesets.Mania.Objects
{
    /// <summary>
    /// Implemented by converted hit objects (e.g. BMS) that carry a keysound to be played through a hosted
    /// <see cref="IManiaKeysoundStore"/> rather than mania's per-object one-shot samples. Lets a pooled
    /// <see cref="Drawables.DrawableNote"/> reroute playback without a compile-time reference to the source ruleset
    /// (J6 / P1-J #10), so a converted KEY note stays a fully pooled <see cref="Drawables.DrawableNote"/> instead of a
    /// per-note non-pooled drawable (the dense-chart allocation/retention cost this avoids).
    /// </summary>
    public interface IHasManiaKeysound
    {
        /// <summary>The keysound to play through the store, or null to fall back to the object's normal samples.</summary>
        ISampleInfo? KeysoundSample { get; }

        /// <summary>The source WAV slot used as the per-WAV cut group, or null for uncut playback.</summary>
        int? KeysoundCutGroup { get; }
    }
}
