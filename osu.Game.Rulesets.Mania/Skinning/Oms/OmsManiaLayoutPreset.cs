// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    internal sealed class OmsManiaLayoutPreset
    {
        public float HitPosition { get; }

        public float StagePaddingTop { get; }

        public float StagePaddingBottom { get; }

        public IReadOnlyList<float> ColumnWidths { get; }

        public IReadOnlyList<float> ColumnSpacing { get; }

        public float NoteHeightReferenceWidth { get; }

        private OmsManiaLayoutPreset(float hitPosition, float stagePaddingTop, float stagePaddingBottom, IReadOnlyList<float> columnWidths, IReadOnlyList<float>? columnSpacing = null,
                                     float? noteHeightReferenceWidth = null)
        {
            HitPosition = hitPosition;
            StagePaddingTop = stagePaddingTop;
            StagePaddingBottom = stagePaddingBottom;
            ColumnWidths = columnWidths;
            ColumnSpacing = columnSpacing ?? Array.Empty<float>();
            NoteHeightReferenceWidth = noteHeightReferenceWidth ?? columnWidths.Min();
        }

        private static readonly IReadOnlyDictionary<int, OmsManiaLayoutPreset> presets = new Dictionary<int, OmsManiaLayoutPreset>
        {
            [4] = new OmsManiaLayoutPreset(toHitPosition(470), 0, 0, new[] { 69f, 69f, 69f, 69f }, noteHeightReferenceWidth: 60 * LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR),
            [5] = new OmsManiaLayoutPreset(toHitPosition(392), 0, 0, new[] { 46f, 40f, 46f, 40f, 46f }),
            [6] = new OmsManiaLayoutPreset(toHitPosition(415), 0, 0, new[] { 40f, 40f, 40f, 40f, 40f, 40f }),
            [7] = new OmsManiaLayoutPreset(toHitPosition(475), 0, 0, new[] { 47f, 47f, 47f, 47f, 47f, 47f, 47f }, noteHeightReferenceWidth: 35 * LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR),
            [8] = new OmsManiaLayoutPreset(toHitPosition(415), 0, 0, new[] { 43f, 36f, 36f, 36f, 36f, 36f, 36f, 36f }),
            [9] = new OmsManiaLayoutPreset(toHitPosition(415), 0, 0, new[] { 34f, 34f, 34f, 34f, 34f, 34f, 34f, 34f, 34f }),
        };

        public static OmsManiaLayoutPreset? ForStageColumns(int stageColumns)
            => presets.GetValueOrDefault(stageColumns);

        public float GetColumnWidth(int columnIndex)
            => ColumnWidths[columnIndex];

        public float GetLeftColumnSpacing(int columnIndex)
        {
            if (columnIndex == 0 || ColumnSpacing.Count == 0)
                return 0;

            return ColumnSpacing[columnIndex - 1] / 2;
        }

        public float GetRightColumnSpacing(int columnIndex)
        {
            if (columnIndex >= ColumnWidths.Count - 1 || ColumnSpacing.Count == 0)
                return 0;

            return ColumnSpacing[columnIndex] / 2;
        }

        private static float toHitPosition(float value)
            => (480 - Math.Clamp(value, 240, 480)) * LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR;
    }
}
