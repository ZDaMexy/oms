// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Skinning.Gameplay
{
    internal static class GameplaySkinStableIdentityId
    {
        public static void Validate(string value, string parameterName)
        {
            ArgumentNullException.ThrowIfNull(value, parameterName);

            if (!isValid(value))
                throw new ArgumentException("Gameplay skin identity IDs must contain lowercase ASCII dot-separated segments.", parameterName);
        }

        private static bool isValid(string value)
        {
            if (value.Length == 0)
                return false;

            foreach (string segment in value.Split('.'))
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
