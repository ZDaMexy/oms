// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps.Formats;
using osu.Game.Skinning.Gameplay;
using osuTK.Graphics;

namespace osu.Game.Skinning
{
    internal enum LegacyManiaSkinScalarField
    {
        WidthForNoteHeightScale = 1 << 0,
        HitPosition = 1 << 1,
        LightPosition = 1 << 2,
        ComboPosition = 1 << 3,
        ScorePosition = 1 << 4,
        BarLineHeight = 1 << 5,
        ShowJudgementLine = 1 << 6,
        KeysUnderNotes = 1 << 7,
        LightFramePerSecond = 1 << 8,
    }

    public class LegacyManiaSkinConfiguration : IHasCustomColours
    {
        internal GameplaySkinConfigurationDeclaration<float> AcceptedWidthForNoteHeightScale { get; private set; }

        internal GameplaySkinConfigurationDeclaration<float> AcceptedHitPosition { get; private set; }

        internal GameplaySkinConfigurationDeclaration<float> AcceptedLightPosition { get; private set; }

        internal GameplaySkinConfigurationDeclaration<float> AcceptedComboPosition { get; private set; }

        internal GameplaySkinConfigurationDeclaration<float> AcceptedScorePosition { get; private set; }

        internal GameplaySkinConfigurationDeclaration<float> AcceptedBarLineHeight { get; private set; }

        internal GameplaySkinConfigurationDeclaration<bool> AcceptedShowJudgementLine { get; private set; }

        internal GameplaySkinConfigurationDeclaration<bool> AcceptedKeysUnderNotes { get; private set; }

        internal GameplaySkinConfigurationDeclaration<int> AcceptedLightFramePerSecond { get; private set; }

        /// <summary>
        /// Conversion factor from converting legacy positioning values (based in x480 dimensions) to x768.
        /// </summary>
        public const float POSITION_SCALE_FACTOR = 1.6f;

        /// <summary>
        /// Size of a legacy column in the default skin, used for determining relative scale factors.
        /// </summary>
        public const float DEFAULT_COLUMN_SIZE = 30 * POSITION_SCALE_FACTOR;

        public const float DEFAULT_HIT_POSITION = (480 - 402) * POSITION_SCALE_FACTOR;

        public readonly int Keys;

        public Dictionary<string, Color4> CustomColours { get; } = new Dictionary<string, Color4>();

        public Dictionary<string, string> ImageLookups = new Dictionary<string, string>();

        public float WidthForNoteHeightScale;

        public readonly float[] ColumnLineWidth;
        public readonly float[] ColumnSpacing;
        public readonly float[] ColumnWidth;
        public readonly float[] ExplosionWidth;
        public readonly float[] HoldNoteLightWidth;

        public float HitPosition = DEFAULT_HIT_POSITION;
        public float LightPosition = (480 - 413) * POSITION_SCALE_FACTOR;
        public float ComboPosition = 111 * POSITION_SCALE_FACTOR;
        public float ScorePosition = 300 * POSITION_SCALE_FACTOR;
        public float BarLineHeight = 1;
        public bool ShowJudgementLine = true;
        public bool KeysUnderNotes;
        public int LightFramePerSecond = 60;

        public LegacyNoteBodyStyle? NoteBodyStyle;

        public LegacyManiaSkinConfiguration(int keys)
        {
            Keys = keys;

            ColumnLineWidth = new float[keys + 1];
            ColumnSpacing = new float[keys - 1];
            ColumnWidth = new float[keys];
            ExplosionWidth = new float[keys];
            HoldNoteLightWidth = new float[keys];

            ColumnLineWidth.AsSpan().Fill(2);
            ColumnWidth.AsSpan().Fill(DEFAULT_COLUMN_SIZE);
        }

        public float MinimumColumnWidth => ColumnWidth.Min();

        /// <summary>
        /// Captures the current compatibility value after the legacy decoder successfully accepts one scalar declaration.
        /// This process-local sidecar is provenance for decoder output, not an authority or security boundary.
        /// </summary>
        internal void MarkScalarDeclared(LegacyManiaSkinScalarField field)
        {
            switch (field)
            {
                case LegacyManiaSkinScalarField.WidthForNoteHeightScale:
                    AcceptedWidthForNoteHeightScale = GameplaySkinConfigurationDeclaration<float>.Declared(WidthForNoteHeightScale);
                    break;

                case LegacyManiaSkinScalarField.HitPosition:
                    AcceptedHitPosition = GameplaySkinConfigurationDeclaration<float>.Declared(HitPosition);
                    break;

                case LegacyManiaSkinScalarField.LightPosition:
                    AcceptedLightPosition = GameplaySkinConfigurationDeclaration<float>.Declared(LightPosition);
                    break;

                case LegacyManiaSkinScalarField.ComboPosition:
                    AcceptedComboPosition = GameplaySkinConfigurationDeclaration<float>.Declared(ComboPosition);
                    break;

                case LegacyManiaSkinScalarField.ScorePosition:
                    AcceptedScorePosition = GameplaySkinConfigurationDeclaration<float>.Declared(ScorePosition);
                    break;

                case LegacyManiaSkinScalarField.BarLineHeight:
                    AcceptedBarLineHeight = GameplaySkinConfigurationDeclaration<float>.Declared(BarLineHeight);
                    break;

                case LegacyManiaSkinScalarField.ShowJudgementLine:
                    AcceptedShowJudgementLine = GameplaySkinConfigurationDeclaration<bool>.Declared(ShowJudgementLine);
                    break;

                case LegacyManiaSkinScalarField.KeysUnderNotes:
                    AcceptedKeysUnderNotes = GameplaySkinConfigurationDeclaration<bool>.Declared(KeysUnderNotes);
                    break;

                case LegacyManiaSkinScalarField.LightFramePerSecond:
                    AcceptedLightFramePerSecond = GameplaySkinConfigurationDeclaration<int>.Declared(LightFramePerSecond);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown legacy mania scalar field.");
            }
        }
    }
}
