// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// The complete C4 lane-resource compatibility surface hosted by BMS.
    /// </summary>
    /// <remarks>
    /// Key visuals remain decoder-visible legacy compatibility data, but BMS does not host the public KeyVisual slot
    /// before C5. Keeping this list separate from the shared mania field catalogue prevents a process-local fixture
    /// from silently expanding the BMS production resolver surface.
    /// </remarks>
    internal static class BmsGameplaySkinNoteResourceFields
    {
        public static IReadOnlyList<GameplaySkinLaneResourceField> All { get; } = Array.AsReadOnly(new[]
        {
            GameplaySkinLaneResourceFieldCatalog.Note,
            GameplaySkinLaneResourceFieldCatalog.LongNoteHead,
            GameplaySkinLaneResourceFieldCatalog.LongNoteBody,
            GameplaySkinLaneResourceFieldCatalog.LongNoteTail,
        });

        public static bool Contains(GameplaySkinLaneResourceField field)
        {
            ArgumentNullException.ThrowIfNull(field);

            foreach (GameplaySkinLaneResourceField candidate in All)
            {
                if (ReferenceEquals(candidate, field))
                    return true;
            }

            return false;
        }
    }
}
