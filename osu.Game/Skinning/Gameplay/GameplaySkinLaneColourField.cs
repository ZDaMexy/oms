// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Identifies one ruleset-neutral per-lane colour declaration understood by gameplay skin configuration adapters.
    /// </summary>
    /// <remarks>
    /// This descriptor is a closed process-local configuration taxonomy. It is not an author manifest, scene, slot or
    /// serialisation ABI, and a declaration does not imply that a renderer accepted or used the colour.
    /// </remarks>
    public sealed class GameplaySkinLaneColourField
    {
        public string Id { get; }

        internal GameplaySkinLaneColourField(string id)
        {
            GameplaySkinStableIdentityId.Validate(id, nameof(id));
            Id = id;
        }

        public override string ToString() => Id;
    }

    /// <summary>
    /// The closed per-lane colour field family currently shared with legacy mania configuration input.
    /// </summary>
    public static class GameplaySkinLaneColourFieldCatalog
    {
        public static GameplaySkinLaneColourField LaneBackground { get; } = new("playfield.lane.background-colour");
        public static GameplaySkinLaneColourField LaneLight { get; } = new("playfield.lane.light-colour");

        public static IReadOnlyList<GameplaySkinLaneColourField> All { get; } = Array.AsReadOnly(new[]
        {
            LaneBackground,
            LaneLight,
        });

        private static readonly IReadOnlyDictionary<string, GameplaySkinLaneColourField> by_id =
            All.ToDictionary(field => field.Id, StringComparer.Ordinal);

        internal static bool TryGet(string? id, [NotNullWhen(true)] out GameplaySkinLaneColourField? field)
        {
            if (id != null && by_id.TryGetValue(id, out GameplaySkinLaneColourField? found))
            {
                field = found;
                return true;
            }

            field = null;
            return false;
        }

        internal static bool IsCanonical(GameplaySkinLaneColourField field)
            => TryGet(field.Id, out GameplaySkinLaneColourField? canonical) && ReferenceEquals(field, canonical);
    }
}
