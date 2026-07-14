// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Whether a gameplay skin slot may be visually suppressed.
    /// </summary>
    public enum SkinSlotRequirement
    {
        /// <summary>
        /// The slot is part of the minimum playable layer and may not be suppressed.
        /// </summary>
        Critical = 0,

        /// <summary>
        /// The slot is visual-only and may be suppressed by a skin package.
        /// </summary>
        Optional = 1,
    }
}
