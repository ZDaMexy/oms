// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// An immutable snapshot of the note-body style declaration accepted for one legacy mania <c>Keys:</c> bucket.
    /// </summary>
    /// <remarks>
    /// The value is the legacy decoder's parsed enum, including unnamed numeric values accepted by its existing
    /// <see cref="Enum.TryParse{TEnum}(string, out TEnum)"/> compatibility behaviour. It is not the effective style that
    /// <see cref="LegacySkin"/> derives from the global legacy skin version when this field is absent. This source-specific
    /// process-local carrier is not a neutral configuration, manifest or serialisation ABI.
    /// </remarks>
    public sealed class LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot
    {
        public int SourceColumnCount { get; }

        public GameplaySkinConfigurationDeclaration<LegacyNoteBodyStyle> NoteBodyStyle { get; }

        private LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot(
            int sourceColumnCount,
            GameplaySkinConfigurationDeclaration<LegacyNoteBodyStyle> noteBodyStyle)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceColumnCount);

            SourceColumnCount = sourceColumnCount;
            NoteBodyStyle = noteBodyStyle;
        }

        internal static LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot Create(
            int sourceColumnCount,
            GameplaySkinConfigurationDeclaration<LegacyNoteBodyStyle> noteBodyStyle)
            => new(sourceColumnCount, noteBodyStyle);

        /// <summary>
        /// Returns only the carrier type and never includes the accepted style or source data.
        /// </summary>
        public override string ToString() => nameof(LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot);
    }
}
