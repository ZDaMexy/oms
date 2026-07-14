// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// An opaque stable lane-group identifier supplied by a ruleset adapter.
    /// </summary>
    /// <remarks>
    /// The value is stable only within one gameplay topology. A producer must not assign one group ID to two distinct semantic groups,
    /// and must reuse it for the same group across presentation styles, geometry changes, skin reload and topology-preserving layout
    /// revisions. Consumers must not reconstruct it from visual order, geometry, a localised label or a ruleset CLR enum. The value
    /// must be a non-sensitive topology token and must not embed user, package, resource or path data. It is not an author-facing
    /// manifest token.
    /// </remarks>
    public sealed class GameplaySkinLaneGroupId : IEquatable<GameplaySkinLaneGroupId>
    {
        public string Value { get; }

        private GameplaySkinLaneGroupId(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Creates an identifier from a lowercase ASCII dot-separated value.
        /// </summary>
        public static GameplaySkinLaneGroupId Create(string value)
        {
            GameplaySkinStableIdentityId.Validate(value, nameof(value));
            return new GameplaySkinLaneGroupId(value);
        }

        public bool Equals(GameplaySkinLaneGroupId? other)
            => other != null && StringComparer.Ordinal.Equals(Value, other.Value);

        public override bool Equals(object? obj) => obj is GameplaySkinLaneGroupId other && Equals(other);

        /// <remarks>
        /// The hash code is process-local and must not be persisted.
        /// </remarks>
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value;

        public static bool operator ==(GameplaySkinLaneGroupId? left, GameplaySkinLaneGroupId? right)
            => ReferenceEquals(left, right) || left?.Equals(right) == true;

        public static bool operator !=(GameplaySkinLaneGroupId? left, GameplaySkinLaneGroupId? right) => !(left == right);
    }

    /// <summary>
    /// The logical player or deck presentation side of a lane group.
    /// </summary>
    /// <remarks>
    /// Primary and secondary are not screen-left and screen-right. A centred group may still be primary or secondary,
    /// and its side may change across a topology-preserving layout revision without changing its stable group or lane IDs.
    /// </remarks>
    public enum GameplaySkinLaneSide
    {
        Unspecified = 0,
        Neutral = 1,
        Primary = 2,
        Secondary = 3,
    }

    /// <summary>
    /// The stable ID and current logical presentation side of one engine-owned lane group.
    /// </summary>
    /// <remarks>
    /// Use <see cref="Id"/> to correlate a group across topology-preserving layout revisions. Value equality includes the current
    /// <see cref="Side"/>, which may change while the ID remains stable. This CLR shape is not a manifest or event serialisation ABI.
    /// </remarks>
    public sealed class GameplaySkinLaneGroupIdentity : IEquatable<GameplaySkinLaneGroupIdentity>
    {
        public GameplaySkinLaneGroupId Id { get; }

        public GameplaySkinLaneSide Side { get; }

        private GameplaySkinLaneGroupIdentity(GameplaySkinLaneGroupId id, GameplaySkinLaneSide side)
        {
            Id = id;
            Side = side;
        }

        public static GameplaySkinLaneGroupIdentity Create(GameplaySkinLaneGroupId id, GameplaySkinLaneSide side)
        {
            ArgumentNullException.ThrowIfNull(id);

            if (side is not GameplaySkinLaneSide.Neutral and not GameplaySkinLaneSide.Primary and not GameplaySkinLaneSide.Secondary)
                throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown or unspecified gameplay skin lane side.");

            return new GameplaySkinLaneGroupIdentity(id, side);
        }

        public bool Equals(GameplaySkinLaneGroupIdentity? other)
            => other != null && Id == other.Id && Side == other.Side;

        public override bool Equals(object? obj) => obj is GameplaySkinLaneGroupIdentity other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Id, Side);

        /// <summary>
        /// Returns only the stable group ID, without presentation metadata.
        /// </summary>
        public override string ToString() => Id.Value;

        public static bool operator ==(GameplaySkinLaneGroupIdentity? left, GameplaySkinLaneGroupIdentity? right)
            => ReferenceEquals(left, right) || left?.Equals(right) == true;

        public static bool operator !=(GameplaySkinLaneGroupIdentity? left, GameplaySkinLaneGroupIdentity? right) => !(left == right);
    }
}
