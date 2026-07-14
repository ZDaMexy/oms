// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// An opaque stable identifier used by gameplay skin capability negotiation.
    /// </summary>
    /// <remarks>
    /// Capability identifiers are non-sensitive lowercase ASCII tokens. They must never contain a package name, user data,
    /// resource name or path. This process-local CLR carrier does not define an author manifest, persistence or script ABI;
    /// those mappings remain versioned contracts for later Skin V1 stages. This carrier is not an untrusted parsing boundary:
    /// the future manifest parser must enforce ID length, request-count and package budgets before constructing IDs.
    /// </remarks>
    public sealed class GameplaySkinCapabilityId : IEquatable<GameplaySkinCapabilityId>
    {
        public string Value { get; }

        private GameplaySkinCapabilityId(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Creates an identifier from a lowercase ASCII dot-separated value.
        /// </summary>
        public static GameplaySkinCapabilityId Create(string value)
        {
            validate(value, nameof(value));
            return new GameplaySkinCapabilityId(value);
        }

        internal static void ValidateToken(string value, string parameterName) => validate(value, parameterName);

        public bool Equals(GameplaySkinCapabilityId? other)
            => other != null && StringComparer.Ordinal.Equals(Value, other.Value);

        public override bool Equals(object? obj) => obj is GameplaySkinCapabilityId other && Equals(other);

        /// <remarks>
        /// The hash code is process-local and must not be persisted.
        /// </remarks>
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value;

        public static bool operator ==(GameplaySkinCapabilityId? left, GameplaySkinCapabilityId? right)
            => ReferenceEquals(left, right) || left?.Equals(right) == true;

        public static bool operator !=(GameplaySkinCapabilityId? left, GameplaySkinCapabilityId? right) => !(left == right);

        private static void validate(string value, string parameterName)
        {
            ArgumentNullException.ThrowIfNull(value, parameterName);

            if (!isValid(value))
                throw new ArgumentException("Gameplay skin capability IDs must contain lowercase ASCII dot-separated segments.", parameterName);
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
