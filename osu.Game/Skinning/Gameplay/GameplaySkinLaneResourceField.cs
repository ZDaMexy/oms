// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Identifies one ruleset-neutral, per-lane resource declaration understood by the legacy mania/BMS adapters.
    /// </summary>
    /// <remarks>
    /// This descriptor is a process-local configuration taxonomy and is not an author manifest or serialisation ABI.
    /// Its associated <see cref="Slot"/> defines the eventual gameplay visual family, but a declared resource is only
    /// source provenance: it is not a validated slot <c>Provide</c> result.
    /// </remarks>
    public sealed class GameplaySkinLaneResourceField
    {
        public string Id { get; }

        public GameplaySkinSlotDescriptor Slot { get; }

        internal GameplaySkinLaneResourceField(string id, GameplaySkinSlotDescriptor slot)
        {
            ArgumentNullException.ThrowIfNull(slot);
            GameplaySkinStableIdentityId.Validate(id, nameof(id));

            Id = id;
            Slot = slot;
        }

        public override string ToString() => Id;
    }

    /// <summary>
    /// The closed field family shared by the existing mania and BMS lane-resource configuration inputs.
    /// </summary>
    public static class GameplaySkinLaneResourceFieldCatalog
    {
        public static GameplaySkinLaneResourceField Note { get; } = create("object.note.resource", GameplaySkinSlotCatalog.Note);
        public static GameplaySkinLaneResourceField LongNoteHead { get; } = create("object.long-note.head.resource", GameplaySkinSlotCatalog.LongNoteHead);
        public static GameplaySkinLaneResourceField LongNoteBody { get; } = create("object.long-note.body.resource", GameplaySkinSlotCatalog.LongNoteBody);
        public static GameplaySkinLaneResourceField LongNoteTail { get; } = create("object.long-note.tail.resource", GameplaySkinSlotCatalog.LongNoteTail);
        public static GameplaySkinLaneResourceField Key { get; } = create("playfield.key.resource", GameplaySkinSlotCatalog.KeyVisual);
        public static GameplaySkinLaneResourceField KeyPressed { get; } = create("playfield.key.pressed-resource", GameplaySkinSlotCatalog.KeyVisual);

        public static IReadOnlyList<GameplaySkinLaneResourceField> All { get; } = Array.AsReadOnly(new[]
        {
            Note,
            LongNoteHead,
            LongNoteBody,
            LongNoteTail,
            Key,
            KeyPressed,
        });

        private static readonly IReadOnlyDictionary<string, GameplaySkinLaneResourceField> by_id =
            All.ToDictionary(field => field.Id, StringComparer.Ordinal);

        public static bool TryGet(string? id, [NotNullWhen(true)] out GameplaySkinLaneResourceField? field)
        {
            if (id != null && by_id.TryGetValue(id, out GameplaySkinLaneResourceField? found))
            {
                field = found;
                return true;
            }

            field = null;
            return false;
        }

        internal static bool IsCanonical(GameplaySkinLaneResourceField field)
            => TryGet(field.Id, out GameplaySkinLaneResourceField? canonical) && ReferenceEquals(field, canonical);

        private static GameplaySkinLaneResourceField create(string id, GameplaySkinSlotDescriptor slot) => new GameplaySkinLaneResourceField(id, slot);
    }
}
