// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    internal sealed class OmsManiaColumnColourPreset
    {
        public Color4 ColumnLineColour { get; }

        public Color4 JudgementLineColour { get; }

        public IReadOnlyList<Color4> ColumnBackgroundColours { get; }

        public IReadOnlyList<Color4> ColumnLightColours { get; }

        private OmsManiaColumnColourPreset(Color4 columnLineColour, Color4 judgementLineColour, IReadOnlyList<Color4> columnBackgroundColours, IReadOnlyList<Color4> columnLightColours)
        {
            ColumnLineColour = columnLineColour;
            JudgementLineColour = judgementLineColour;
            ColumnBackgroundColours = columnBackgroundColours;
            ColumnLightColours = columnLightColours;
        }

        private static readonly IReadOnlyDictionary<int, OmsManiaColumnColourPreset> presets = new Dictionary<int, OmsManiaColumnColourPreset>
        {
            [4] = new OmsManiaColumnColourPreset(
                columnLineColour: Color4.White,
                judgementLineColour: Color4.White,
                columnBackgroundColours: new[] { black(), black(), black(), black() },
                columnLightColours: new[] { transparentWhite(), transparentWhite(), transparentWhite(), transparentWhite() }),
            [5] = new OmsManiaColumnColourPreset(
                columnLineColour: Color4.White,
                judgementLineColour: Color4.White,
                columnBackgroundColours: new[] { black(), black(), black(), black(), black() },
                columnLightColours: new[] { Color4.White, Color4.White, Color4.White, Color4.White, Color4.White }),
            [6] = new OmsManiaColumnColourPreset(
                columnLineColour: Color4.White,
                judgementLineColour: Color4.White,
                columnBackgroundColours: new[] { black(), black(), black(), black(), black(), black() },
                columnLightColours: new[] { Color4.White, Color4.White, Color4.White, Color4.White, Color4.White, Color4.White }),
            [7] = new OmsManiaColumnColourPreset(
                columnLineColour: Color4.White,
                judgementLineColour: Color4.White,
                columnBackgroundColours: new[] { black(), black(), black(), black(), black(), black(), black() },
                columnLightColours: new[] { Color4.White, Color4.White, Color4.White, Color4.White, Color4.White, Color4.White, Color4.White }),
            [8] = new OmsManiaColumnColourPreset(
                columnLineColour: Color4.White,
                judgementLineColour: Color4.White,
                columnBackgroundColours: new[] { black(), black(), black(), black(), black(), black(), black(), black() },
                columnLightColours: new[] { Color4.White, Color4.White, Color4.White, Color4.White, Color4.White, Color4.White, Color4.White, Color4.White }),
            [9] = new OmsManiaColumnColourPreset(
                columnLineColour: Color4.White,
                judgementLineColour: Color4.White,
                columnBackgroundColours: new[]
                {
                    black(),
                    grey(15, 15, 15),
                    black(),
                    grey(15, 15, 15),
                    grey(15, 15, 5),
                    grey(15, 15, 15),
                    black(),
                    grey(15, 15, 15),
                    black(),
                },
                columnLightColours: new[] { Color4.White, Color4.White, Color4.White, Color4.White, Color4.White, Color4.White, Color4.White, Color4.White, Color4.White }),
        };

        public static OmsManiaColumnColourPreset? ForStageColumns(int stageColumns)
            => presets.GetValueOrDefault(stageColumns);

        public Color4 GetColumnBackgroundColour(int columnIndex)
            => ColumnBackgroundColours[columnIndex];

        public Color4 GetColumnLightColour(int columnIndex)
            => ColumnLightColours[columnIndex];

        private static Color4 black() => new Color4(0, 0, 0, 255);

        private static Color4 transparentWhite() => new Color4(255, 255, 255, 0);

        private static Color4 grey(byte red, byte green, byte blue) => new Color4(red, green, blue, 255);
    }
}
