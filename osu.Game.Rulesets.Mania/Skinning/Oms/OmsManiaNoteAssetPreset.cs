// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    internal sealed class OmsManiaNoteAssetPreset
    {
        public IReadOnlyList<string> NoteImages { get; }

        public IReadOnlyList<string> HoldHeadImages { get; }

        public IReadOnlyList<string> HoldTailImages { get; }

        public IReadOnlyList<string> HoldBodyImages { get; }

        private OmsManiaNoteAssetPreset(IReadOnlyList<string> noteImages, IReadOnlyList<string> holdHeadImages, IReadOnlyList<string> holdTailImages, IReadOnlyList<string> holdBodyImages)
        {
            NoteImages = noteImages;
            HoldHeadImages = holdHeadImages;
            HoldTailImages = holdTailImages;
            HoldBodyImages = holdBodyImages;
        }

        private static readonly IReadOnlyDictionary<int, OmsManiaNoteAssetPreset> presets = new Dictionary<int, OmsManiaNoteAssetPreset>
        {
            [4] = new OmsManiaNoteAssetPreset(
                noteImages: new[] { "mania-note1", "mania-note1", "mania-note1", "mania-note1" },
                holdHeadImages: new[] { "mania-note1", "mania-note1", "mania-note1", "mania-note1" },
                holdTailImages: new[] { "Notes4K\\LNBody", "Notes4K\\LNBody", "Notes4K\\LNBody", "Notes4K\\LNBody" },
                holdBodyImages: new[] { "A", "A", "A", "A" }),
            [5] = new OmsManiaNoteAssetPreset(
                noteImages: new[] { "mania-note1", "mania-note2", "mania-note1", "mania-note2", "mania-note1" },
                holdHeadImages: new[] { "mania-note1H", "mania-note2H", "mania-note1H", "mania-note2H", "mania-note1H" },
                holdTailImages: new[] { "mania-note1T", "mania-note2T", "mania-note1T", "mania-note2T", "mania-note1T" },
                holdBodyImages: new[] { "mania-note1L", "mania-note2L", "mania-note1L", "mania-note2L", "mania-note1L" }),
            [6] = new OmsManiaNoteAssetPreset(
                noteImages: new[] { "mania-note1", "mania-note2", "mania-note1", "mania-note1", "mania-note2", "mania-note1" },
                holdHeadImages: new[] { "mania-note1", "mania-note2", "mania-note1", "mania-note1", "mania-note2", "mania-note1" },
                holdTailImages: new[] { "Notes4K\\LNBody", "Notes4K\\LNBody", "Notes4K\\LNBody", "Notes4K\\LNBody", "Notes4K\\LNBody", "Notes4K\\LNBody" },
                holdBodyImages: new[] { "A", "A", "A", "A", "A", "A" }),
            [7] = new OmsManiaNoteAssetPreset(
                noteImages: new[] { "mania-note1", "mania-note2", "mania-note1", "mania-noteS", "mania-note1", "mania-note2", "mania-note1" },
                holdHeadImages: new[] { "mania-note1", "mania-note2", "mania-note1", "mania-noteS", "mania-note1", "mania-note2", "mania-note1" },
                holdTailImages: new[] { "Notes4K\\LNBody", "Notes4K\\LNBody", "Notes4K\\LNBody", "Notes4K\\LNBody", "Notes4K\\LNBody", "Notes4K\\LNBody", "Notes4K\\LNBody" },
                holdBodyImages: new[] { "A", "A", "A", "A", "A", "A", "A" }),
            [8] = new OmsManiaNoteAssetPreset(
                noteImages: new[] { "mania-noteS", "mania-note1", "mania-note2", "mania-note1", "mania-noteS", "mania-note1", "mania-note2", "mania-note1" },
                holdHeadImages: new[] { "mania-noteS", "mania-note1", "mania-note2", "mania-note1", "mania-noteS", "mania-note1", "mania-note2", "mania-note1" },
                holdTailImages: new[] { "Notes4K\\LNBody", "Notes4K\\LNBody", "Notes4K\\LNBody", "Notes4K\\LNBody", "Notes4K\\LNBody", "Notes4K\\LNBody", "Notes4K\\LNBody", "Notes4K\\LNBody" },
                holdBodyImages: new[] { "Notes4K\\LNTail", "Notes4K\\LNTail", "Notes4K\\LNTail", "Notes4K\\LNTail", "Notes4K\\LNTail", "Notes4K\\LNTail", "Notes4K\\LNTail", "Notes4K\\LNTail" }),
            [9] = new OmsManiaNoteAssetPreset(
                noteImages: new[] { "mania-noteS", "mania-note1", "mania-note2", "mania-note1", "mania-noteS", "mania-note1", "mania-note2", "mania-note1", "mania-noteS" },
                holdHeadImages: new[] { "mania-noteSH", "mania-note1H", "mania-note2H", "mania-note1H", "mania-noteSH", "mania-note1H", "mania-note2H", "mania-note1H", "mania-noteSH" },
                holdTailImages: new[] { "mania-noteST", "mania-note1T", "mania-note2T", "mania-note1T", "mania-noteST", "mania-note1T", "mania-note2T", "mania-note1T", "mania-noteST" },
                holdBodyImages: new[] { "mania-noteSL", "mania-note1L", "mania-note2L", "mania-note1L", "mania-noteSL", "mania-note1L", "mania-note2L", "mania-note1L", "mania-noteSL" }),
        };

        public static OmsManiaNoteAssetPreset? ForStageColumns(int stageColumns)
            => presets.GetValueOrDefault(stageColumns);

        public string GetNoteImage(int columnIndex)
            => NoteImages[columnIndex];

        public string GetHoldHeadImage(int columnIndex)
            => HoldHeadImages[columnIndex];

        public string GetHoldTailImage(int columnIndex)
            => HoldTailImages[columnIndex];

        public string GetHoldBodyImage(int columnIndex)
            => HoldBodyImages[columnIndex];
    }
}
