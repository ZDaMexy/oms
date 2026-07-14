// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Defines one ruleset-neutral gameplay skin slot and whether it belongs to the minimum playable layer.
    /// </summary>
    /// <remarks>
    /// The stable <see cref="Id"/> identifies semantic visual content only. Lane, keymode, side, result and layout context
    /// belong to a separate lookup value and must not be encoded into the ID.
    /// </remarks>
    public sealed class GameplaySkinSlotDescriptor
    {
        /// <summary>
        /// The stable, non-localised internal taxonomy and diagnostic identifier.
        /// </summary>
        /// <remarks>
        /// Scene manifest tokens and their versioning have not yet been defined; this ID must not be advertised as an author-facing manifest ABI.
        /// </remarks>
        public string Id { get; }

        /// <summary>
        /// Whether the slot may be visually suppressed.
        /// </summary>
        public SkinSlotRequirement Requirement { get; }

        internal GameplaySkinSlotDescriptor(string id, SkinSlotRequirement requirement)
        {
            ArgumentNullException.ThrowIfNull(id);

            if (!isValidId(id))
                throw new ArgumentException("Gameplay skin slot IDs must contain lowercase ASCII dot-separated segments.", nameof(id));

            if (requirement is not SkinSlotRequirement.Critical and not SkinSlotRequirement.Optional)
                throw new ArgumentOutOfRangeException(nameof(requirement), requirement, "Unknown gameplay skin slot requirement.");

            Id = id;
            Requirement = requirement;
        }

        public override string ToString() => Id;

        private static bool isValidId(string id)
        {
            if (id.Length == 0)
                return false;

            foreach (string segment in id.Split('.'))
            {
                if (segment.Length == 0 || !isAsciiLower(segment[0]) || !isAsciiLowerOrDigit(segment[^1]))
                    return false;

                foreach (char character in segment)
                {
                    if (!isAsciiLowerOrDigit(character) && character != '-')
                        return false;
                }
            }

            return true;
        }

        private static bool isAsciiLower(char character) => character is >= 'a' and <= 'z';

        private static bool isAsciiLowerOrDigit(char character) => isAsciiLower(character) || character is >= '0' and <= '9';
    }
}
