// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// An opaque stable lane identifier supplied by a ruleset adapter.
    /// </summary>
    /// <remarks>
    /// The value is stable only within one gameplay topology. A producer must not assign one lane ID to two distinct semantic lanes
    /// across its groups, and must reuse it for the same lane across presentation styles, geometry changes, skin reload and layout
    /// revisions that preserve the topology. Consumers must not reconstruct it from logical or visual index, geometry, a localised
    /// label or a ruleset CLR enum.
    /// The value must be a non-sensitive topology token and must not embed user, package, resource or path data. It is not an
    /// author-facing manifest token.
    /// </remarks>
    public sealed class GameplaySkinLaneId : IEquatable<GameplaySkinLaneId>
    {
        public string Value { get; }

        private GameplaySkinLaneId(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Creates an identifier from a lowercase ASCII dot-separated value.
        /// </summary>
        public static GameplaySkinLaneId Create(string value)
        {
            GameplaySkinStableIdentityId.Validate(value, nameof(value));
            return new GameplaySkinLaneId(value);
        }

        public bool Equals(GameplaySkinLaneId? other)
            => other != null && StringComparer.Ordinal.Equals(Value, other.Value);

        public override bool Equals(object? obj) => obj is GameplaySkinLaneId other && Equals(other);

        /// <remarks>
        /// The hash code is process-local and must not be persisted.
        /// </remarks>
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value;

        public static bool operator ==(GameplaySkinLaneId? left, GameplaySkinLaneId? right)
            => ReferenceEquals(left, right) || left?.Equals(right) == true;

        public static bool operator !=(GameplaySkinLaneId? left, GameplaySkinLaneId? right) => !(left == right);
    }

    /// <summary>
    /// The gameplay-semantic role of a lane.
    /// </summary>
    /// <remarks>
    /// Note, long-note and mine types belong to objects, not lane roles. A special key is still a key input and must
    /// never acquire scratch input semantics.
    /// </remarks>
    public enum GameplaySkinLaneRole
    {
        Unspecified = 0,
        Key = 1,
        SpecialKey = 2,
        Scratch = 3,
    }

    /// <summary>
    /// The stable ID, group membership and gameplay-semantic role of one engine-owned lane.
    /// </summary>
    /// <remarks>
    /// Use <see cref="Id"/> to correlate a lane across topology-preserving layout revisions. Its group ID and role must remain
    /// unchanged; only the group's presentation side may change. Value equality includes all current neutral metadata. This CLR shape
    /// is not a manifest or event serialisation ABI, and uniqueness or metadata conflicts remain the responsibility of the containing
    /// topology aggregate.
    /// </remarks>
    public sealed class GameplaySkinLaneIdentity : IEquatable<GameplaySkinLaneIdentity>
    {
        public GameplaySkinLaneId Id { get; }

        public GameplaySkinLaneGroupIdentity Group { get; }

        public GameplaySkinLaneRole Role { get; }

        public GameplaySkinLaneSide Side => Group.Side;

        private GameplaySkinLaneIdentity(GameplaySkinLaneId id, GameplaySkinLaneGroupIdentity group, GameplaySkinLaneRole role)
        {
            Id = id;
            Group = group;
            Role = role;
        }

        public static GameplaySkinLaneIdentity Create(GameplaySkinLaneId id, GameplaySkinLaneGroupIdentity group, GameplaySkinLaneRole role)
        {
            ArgumentNullException.ThrowIfNull(id);
            ArgumentNullException.ThrowIfNull(group);

            if (role is not GameplaySkinLaneRole.Key and not GameplaySkinLaneRole.SpecialKey and not GameplaySkinLaneRole.Scratch)
                throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown or unspecified gameplay skin lane role.");

            return new GameplaySkinLaneIdentity(id, group, role);
        }

        public bool Equals(GameplaySkinLaneIdentity? other)
            => other != null && Id == other.Id && Group == other.Group && Role == other.Role;

        public override bool Equals(object? obj) => obj is GameplaySkinLaneIdentity other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Id, Group, Role);

        /// <summary>
        /// Returns only the stable lane ID, without group or role metadata.
        /// </summary>
        public override string ToString() => Id.Value;

        public static bool operator ==(GameplaySkinLaneIdentity? left, GameplaySkinLaneIdentity? right)
            => ReferenceEquals(left, right) || left?.Equals(right) == true;

        public static bool operator !=(GameplaySkinLaneIdentity? left, GameplaySkinLaneIdentity? right) => !(left == right);
    }
}
