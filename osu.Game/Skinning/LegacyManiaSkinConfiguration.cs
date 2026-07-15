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

    internal enum LegacyManiaSkinArrayField
    {
        ColumnLineWidth = 1 << 0,
        ColumnSpacing = 1 << 1,
        ColumnWidth = 1 << 2,
        ExplosionWidth = 1 << 3,
        HoldNoteLightWidth = 1 << 4,
    }

    internal enum LegacyManiaSkinKnownGlobalColourField
    {
        ColumnLine = 1 << 0,
        JudgementLine = 1 << 1,
        ComboBreak = 1 << 2,
        BarLine = 1 << 3,
    }

    internal enum LegacyManiaSkinPerColumnColourField
    {
        ColumnBackground = 1 << 0,
        ColumnLight = 1 << 1,
    }

    internal enum LegacyManiaSkinLaneResourceField
    {
        Note = 1 << 0,
        LongNoteHead = 1 << 1,
        LongNoteBody = 1 << 2,
        LongNoteTail = 1 << 3,
        Key = 1 << 4,
        KeyPressed = 1 << 5,
    }

    internal enum LegacyManiaSkinKnownGlobalResourceField
    {
        LightingN = 1 << 0,
        LightingL = 1 << 1,
        StageLeft = 1 << 2,
        StageRight = 1 << 3,
        StageBottom = 1 << 4,
        StageLight = 1 << 5,
        StageHint = 1 << 6,
        Hit0 = 1 << 7,
        Hit50 = 1 << 8,
        Hit100 = 1 << 9,
        Hit200 = 1 << 10,
        Hit300 = 1 << 11,
        Hit300g = 1 << 12,
    }

    public class LegacyManiaSkinConfiguration : IHasCustomColours
    {
        private readonly GameplaySkinConfigurationDeclaration<float>[] acceptedColumnLineWidth;
        private readonly GameplaySkinConfigurationDeclaration<float>[] acceptedColumnSpacing;
        private readonly GameplaySkinConfigurationDeclaration<float>[] acceptedColumnWidth;
        private readonly GameplaySkinConfigurationDeclaration<float>[] acceptedExplosionWidth;
        private readonly GameplaySkinConfigurationDeclaration<float>[] acceptedHoldNoteLightWidth;
        private readonly GameplaySkinConfigurationDeclaration<Color4>[] acceptedColumnBackgroundColours;
        private readonly GameplaySkinConfigurationDeclaration<Color4>[] acceptedColumnLightColours;
        private readonly GameplaySkinConfigurationDeclaration<string>[] acceptedNoteResources;
        private readonly GameplaySkinConfigurationDeclaration<string>[] acceptedLongNoteHeadResources;
        private readonly GameplaySkinConfigurationDeclaration<string>[] acceptedLongNoteBodyResources;
        private readonly GameplaySkinConfigurationDeclaration<string>[] acceptedLongNoteTailResources;
        private readonly GameplaySkinConfigurationDeclaration<string>[] acceptedKeyResources;
        private readonly GameplaySkinConfigurationDeclaration<string>[] acceptedKeyPressedResources;

        internal GameplaySkinConfigurationDeclaration<float> AcceptedWidthForNoteHeightScale { get; private set; }

        internal GameplaySkinConfigurationDeclaration<float> AcceptedHitPosition { get; private set; }

        internal GameplaySkinConfigurationDeclaration<float> AcceptedLightPosition { get; private set; }

        internal GameplaySkinConfigurationDeclaration<float> AcceptedComboPosition { get; private set; }

        internal GameplaySkinConfigurationDeclaration<float> AcceptedScorePosition { get; private set; }

        internal GameplaySkinConfigurationDeclaration<float> AcceptedBarLineHeight { get; private set; }

        internal GameplaySkinConfigurationDeclaration<bool> AcceptedShowJudgementLine { get; private set; }

        internal GameplaySkinConfigurationDeclaration<bool> AcceptedKeysUnderNotes { get; private set; }

        internal GameplaySkinConfigurationDeclaration<int> AcceptedLightFramePerSecond { get; private set; }

        internal GameplaySkinConfigurationDeclaration<LegacyNoteBodyStyle> AcceptedNoteBodyStyle { get; private set; }

        internal GameplaySkinConfigurationDeclaration<Color4> AcceptedColumnLineColour { get; private set; }

        internal GameplaySkinConfigurationDeclaration<Color4> AcceptedJudgementLineColour { get; private set; }

        internal GameplaySkinConfigurationDeclaration<Color4> AcceptedComboBreakColour { get; private set; }

        internal GameplaySkinConfigurationDeclaration<Color4> AcceptedBarLineColour { get; private set; }

        internal GameplaySkinConfigurationDeclaration<string> AcceptedLightingNResource { get; private set; }

        internal GameplaySkinConfigurationDeclaration<string> AcceptedLightingLResource { get; private set; }

        internal GameplaySkinConfigurationDeclaration<string> AcceptedStageLeftResource { get; private set; }

        internal GameplaySkinConfigurationDeclaration<string> AcceptedStageRightResource { get; private set; }

        internal GameplaySkinConfigurationDeclaration<string> AcceptedStageBottomResource { get; private set; }

        internal GameplaySkinConfigurationDeclaration<string> AcceptedStageLightResource { get; private set; }

        internal GameplaySkinConfigurationDeclaration<string> AcceptedStageHintResource { get; private set; }

        internal GameplaySkinConfigurationDeclaration<string> AcceptedHit0Resource { get; private set; }

        internal GameplaySkinConfigurationDeclaration<string> AcceptedHit50Resource { get; private set; }

        internal GameplaySkinConfigurationDeclaration<string> AcceptedHit100Resource { get; private set; }

        internal GameplaySkinConfigurationDeclaration<string> AcceptedHit200Resource { get; private set; }

        internal GameplaySkinConfigurationDeclaration<string> AcceptedHit300Resource { get; private set; }

        internal GameplaySkinConfigurationDeclaration<string> AcceptedHit300gResource { get; private set; }

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

            acceptedColumnLineWidth = new GameplaySkinConfigurationDeclaration<float>[ColumnLineWidth.Length];
            acceptedColumnSpacing = new GameplaySkinConfigurationDeclaration<float>[ColumnSpacing.Length];
            acceptedColumnWidth = new GameplaySkinConfigurationDeclaration<float>[ColumnWidth.Length];
            acceptedExplosionWidth = new GameplaySkinConfigurationDeclaration<float>[ExplosionWidth.Length];
            acceptedHoldNoteLightWidth = new GameplaySkinConfigurationDeclaration<float>[HoldNoteLightWidth.Length];
            acceptedColumnBackgroundColours = new GameplaySkinConfigurationDeclaration<Color4>[keys];
            acceptedColumnLightColours = new GameplaySkinConfigurationDeclaration<Color4>[keys];
            acceptedNoteResources = new GameplaySkinConfigurationDeclaration<string>[keys];
            acceptedLongNoteHeadResources = new GameplaySkinConfigurationDeclaration<string>[keys];
            acceptedLongNoteBodyResources = new GameplaySkinConfigurationDeclaration<string>[keys];
            acceptedLongNoteTailResources = new GameplaySkinConfigurationDeclaration<string>[keys];
            acceptedKeyResources = new GameplaySkinConfigurationDeclaration<string>[keys];
            acceptedKeyPressedResources = new GameplaySkinConfigurationDeclaration<string>[keys];

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

        /// <summary>
        /// Captures one note-body style immediately after the legacy decoder successfully parses its exact source key.
        /// The accepted enum value is preserved as-is, including values which are not named by
        /// <see cref="LegacyNoteBodyStyle"/>, to retain the decoder's existing <see cref="Enum.TryParse{TEnum}(string, out TEnum)"/>
        /// compatibility semantics. This declaration is not the version-derived effective production style.
        /// </summary>
        internal void AcceptNoteBodyStyle(LegacyNoteBodyStyle style)
        {
            GameplaySkinConfigurationDeclaration<LegacyNoteBodyStyle> declaration =
                GameplaySkinConfigurationDeclaration<LegacyNoteBodyStyle>.Declared(style);

            NoteBodyStyle = style;
            AcceptedNoteBodyStyle = declaration;
        }

        /// <summary>
        /// Writes one decoder-accepted array value to the mutable compatibility view and its declaration sidecar as one
        /// accepted operation. Field and index validation complete before either view is changed.
        /// This process-local sidecar is provenance for decoder output, not an authority or security boundary.
        /// </summary>
        internal void AcceptArrayValue(LegacyManiaSkinArrayField field, int index, float value)
        {
            float[] compatibilityValues = getArrayValues(field);
            GameplaySkinConfigurationDeclaration<float>[] acceptedValues = getAcceptedArrayValues(field);

            if ((uint)index >= (uint)compatibilityValues.Length)
                throw new ArgumentOutOfRangeException(nameof(index), index, "The legacy mania array index is outside the source bucket.");

            compatibilityValues[index] = value;
            acceptedValues[index] = GameplaySkinConfigurationDeclaration<float>.Declared(value);
        }

        internal int GetArrayLength(LegacyManiaSkinArrayField field) => getArrayValues(field).Length;

        internal GameplaySkinConfigurationDeclaration<float>[] CopyAcceptedArrayDeclarations(LegacyManiaSkinArrayField field)
            => getAcceptedArrayValues(field).ToArray();

        /// <summary>
        /// Captures one known global colour immediately after the legacy decoder successfully accepts its exact source key.
        /// Unknown and per-column <c>Colour*</c> keys remain part of the mutable compatibility view but are deliberately outside
        /// this closed provenance sidecar. No alpha compatibility transformation or visual validation is performed here.
        /// </summary>
        internal void AcceptKnownGlobalColour(LegacyManiaSkinKnownGlobalColourField field, Color4 value)
        {
            switch (field)
            {
                case LegacyManiaSkinKnownGlobalColourField.ColumnLine:
                    AcceptedColumnLineColour = GameplaySkinConfigurationDeclaration<Color4>.Declared(value);
                    break;

                case LegacyManiaSkinKnownGlobalColourField.JudgementLine:
                    AcceptedJudgementLineColour = GameplaySkinConfigurationDeclaration<Color4>.Declared(value);
                    break;

                case LegacyManiaSkinKnownGlobalColourField.ComboBreak:
                    AcceptedComboBreakColour = GameplaySkinConfigurationDeclaration<Color4>.Declared(value);
                    break;

                case LegacyManiaSkinKnownGlobalColourField.BarLine:
                    AcceptedBarLineColour = GameplaySkinConfigurationDeclaration<Color4>.Declared(value);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown legacy mania known global colour field.");
            }
        }

        /// <summary>
        /// Captures one exact legacy <c>Colour{n}</c> or <c>ColourLight{n}</c> declaration immediately after successful
        /// colour parsing. The zero-based source column remains decoder provenance and is not a stable gameplay lane identity.
        /// No alpha compatibility transformation, defaulting or visual validation is performed here.
        /// </summary>
        internal void AcceptPerColumnColour(LegacyManiaSkinPerColumnColourField field, int sourceColumnIndex, Color4 value)
        {
            GameplaySkinConfigurationDeclaration<Color4>[] acceptedColours = getAcceptedPerColumnColours(field);

            if ((uint)sourceColumnIndex >= (uint)acceptedColours.Length)
                throw new ArgumentOutOfRangeException(nameof(sourceColumnIndex), sourceColumnIndex, "The legacy mania colour index is outside the source bucket.");

            acceptedColours[sourceColumnIndex] = GameplaySkinConfigurationDeclaration<Color4>.Declared(value);
        }

        internal GameplaySkinConfigurationDeclaration<Color4>[] CopyAcceptedPerColumnColourDeclarations(LegacyManiaSkinPerColumnColourField field)
            => getAcceptedPerColumnColours(field).ToArray();

        /// <summary>
        /// Captures one exact per-column resource declaration when the legacy decoder accepts its canonical source key.
        /// The resource name may be empty and remains filename-unnormalised and unvalidated.
        /// </summary>
        internal void AcceptLaneResource(LegacyManiaSkinLaneResourceField field, int sourceColumnIndex, string resourceName)
        {
            ArgumentNullException.ThrowIfNull(resourceName);
            GameplaySkinConfigurationDeclaration<string>[] declarations = getAcceptedLaneResources(field);

            if ((uint)sourceColumnIndex >= (uint)declarations.Length)
                throw new ArgumentOutOfRangeException(nameof(sourceColumnIndex), sourceColumnIndex, "The legacy mania lane resource column is outside its Keys bucket.");

            GameplaySkinLaneResourceField canonicalField = field switch
            {
                LegacyManiaSkinLaneResourceField.Note => GameplaySkinLaneResourceFieldCatalog.Note,
                LegacyManiaSkinLaneResourceField.LongNoteHead => GameplaySkinLaneResourceFieldCatalog.LongNoteHead,
                LegacyManiaSkinLaneResourceField.LongNoteBody => GameplaySkinLaneResourceFieldCatalog.LongNoteBody,
                LegacyManiaSkinLaneResourceField.LongNoteTail => GameplaySkinLaneResourceFieldCatalog.LongNoteTail,
                LegacyManiaSkinLaneResourceField.Key => GameplaySkinLaneResourceFieldCatalog.Key,
                LegacyManiaSkinLaneResourceField.KeyPressed => GameplaySkinLaneResourceFieldCatalog.KeyPressed,
                _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown legacy mania lane resource field."),
            };
            string sourceKey = LegacyManiaGameplaySkinLaneResourceSnapshotFactory.GetImageLookupKey(
                canonicalField,
                sourceColumnIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
            GameplaySkinConfigurationDeclaration<string> declaration = GameplaySkinConfigurationDeclaration<string>.Declared(resourceName);

            ImageLookups[sourceKey] = resourceName;
            declarations[sourceColumnIndex] = declaration;
        }

        internal GameplaySkinConfigurationDeclaration<string> GetAcceptedLaneResource(
            LegacyManiaSkinLaneResourceField field,
            int sourceColumnIndex)
        {
            GameplaySkinConfigurationDeclaration<string>[] declarations = getAcceptedLaneResources(field);

            if ((uint)sourceColumnIndex >= (uint)declarations.Length)
                throw new ArgumentOutOfRangeException(nameof(sourceColumnIndex), sourceColumnIndex, "The legacy mania lane resource column is outside its Keys bucket.");

            return declarations[sourceColumnIndex];
        }

        private GameplaySkinConfigurationDeclaration<string>[] getAcceptedLaneResources(LegacyManiaSkinLaneResourceField field)
        {
            return field switch
            {
                LegacyManiaSkinLaneResourceField.Note => acceptedNoteResources,
                LegacyManiaSkinLaneResourceField.LongNoteHead => acceptedLongNoteHeadResources,
                LegacyManiaSkinLaneResourceField.LongNoteBody => acceptedLongNoteBodyResources,
                LegacyManiaSkinLaneResourceField.LongNoteTail => acceptedLongNoteTailResources,
                LegacyManiaSkinLaneResourceField.Key => acceptedKeyResources,
                LegacyManiaSkinLaneResourceField.KeyPressed => acceptedKeyPressedResources,
                _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown legacy mania lane resource field."),
            };
        }

        /// <summary>
        /// Captures one exact global image declaration immediately after the legacy decoder accepts its
        /// <c>SplitKeyVal</c>-trimmed compatibility string. The value may be empty and remains filename-unnormalised and
        /// unvalidated. It may contain a source-provided resource name or path and must not be written to diagnostics without
        /// sanitisation.
        /// </summary>
        internal void AcceptKnownGlobalResource(LegacyManiaSkinKnownGlobalResourceField field, string resourceName)
        {
            ArgumentNullException.ThrowIfNull(resourceName);
            string sourceKey = field switch
            {
                LegacyManiaSkinKnownGlobalResourceField.LightingN => "LightingN",
                LegacyManiaSkinKnownGlobalResourceField.LightingL => "LightingL",
                LegacyManiaSkinKnownGlobalResourceField.StageLeft => "StageLeft",
                LegacyManiaSkinKnownGlobalResourceField.StageRight => "StageRight",
                LegacyManiaSkinKnownGlobalResourceField.StageBottom => "StageBottom",
                LegacyManiaSkinKnownGlobalResourceField.StageLight => "StageLight",
                LegacyManiaSkinKnownGlobalResourceField.StageHint => "StageHint",
                LegacyManiaSkinKnownGlobalResourceField.Hit0 => "Hit0",
                LegacyManiaSkinKnownGlobalResourceField.Hit50 => "Hit50",
                LegacyManiaSkinKnownGlobalResourceField.Hit100 => "Hit100",
                LegacyManiaSkinKnownGlobalResourceField.Hit200 => "Hit200",
                LegacyManiaSkinKnownGlobalResourceField.Hit300 => "Hit300",
                LegacyManiaSkinKnownGlobalResourceField.Hit300g => "Hit300g",
                _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown legacy mania known global resource field."),
            };
            GameplaySkinConfigurationDeclaration<string> declaration = GameplaySkinConfigurationDeclaration<string>.Declared(resourceName);

            ImageLookups[sourceKey] = resourceName;

            switch (field)
            {
                case LegacyManiaSkinKnownGlobalResourceField.LightingN:
                    AcceptedLightingNResource = declaration;
                    break;

                case LegacyManiaSkinKnownGlobalResourceField.LightingL:
                    AcceptedLightingLResource = declaration;
                    break;

                case LegacyManiaSkinKnownGlobalResourceField.StageLeft:
                    AcceptedStageLeftResource = declaration;
                    break;

                case LegacyManiaSkinKnownGlobalResourceField.StageRight:
                    AcceptedStageRightResource = declaration;
                    break;

                case LegacyManiaSkinKnownGlobalResourceField.StageBottom:
                    AcceptedStageBottomResource = declaration;
                    break;

                case LegacyManiaSkinKnownGlobalResourceField.StageLight:
                    AcceptedStageLightResource = declaration;
                    break;

                case LegacyManiaSkinKnownGlobalResourceField.StageHint:
                    AcceptedStageHintResource = declaration;
                    break;

                case LegacyManiaSkinKnownGlobalResourceField.Hit0:
                    AcceptedHit0Resource = declaration;
                    break;

                case LegacyManiaSkinKnownGlobalResourceField.Hit50:
                    AcceptedHit50Resource = declaration;
                    break;

                case LegacyManiaSkinKnownGlobalResourceField.Hit100:
                    AcceptedHit100Resource = declaration;
                    break;

                case LegacyManiaSkinKnownGlobalResourceField.Hit200:
                    AcceptedHit200Resource = declaration;
                    break;

                case LegacyManiaSkinKnownGlobalResourceField.Hit300:
                    AcceptedHit300Resource = declaration;
                    break;

                case LegacyManiaSkinKnownGlobalResourceField.Hit300g:
                    AcceptedHit300gResource = declaration;
                    break;

                default:
                    throw new InvalidOperationException("The validated legacy mania global resource field was not captured.");
            }
        }

        private float[] getArrayValues(LegacyManiaSkinArrayField field)
        {
            return field switch
            {
                LegacyManiaSkinArrayField.ColumnLineWidth => ColumnLineWidth,
                LegacyManiaSkinArrayField.ColumnSpacing => ColumnSpacing,
                LegacyManiaSkinArrayField.ColumnWidth => ColumnWidth,
                LegacyManiaSkinArrayField.ExplosionWidth => ExplosionWidth,
                LegacyManiaSkinArrayField.HoldNoteLightWidth => HoldNoteLightWidth,
                _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown legacy mania array field."),
            };
        }

        private GameplaySkinConfigurationDeclaration<float>[] getAcceptedArrayValues(LegacyManiaSkinArrayField field)
        {
            return field switch
            {
                LegacyManiaSkinArrayField.ColumnLineWidth => acceptedColumnLineWidth,
                LegacyManiaSkinArrayField.ColumnSpacing => acceptedColumnSpacing,
                LegacyManiaSkinArrayField.ColumnWidth => acceptedColumnWidth,
                LegacyManiaSkinArrayField.ExplosionWidth => acceptedExplosionWidth,
                LegacyManiaSkinArrayField.HoldNoteLightWidth => acceptedHoldNoteLightWidth,
                _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown legacy mania array field."),
            };
        }

        private GameplaySkinConfigurationDeclaration<Color4>[] getAcceptedPerColumnColours(LegacyManiaSkinPerColumnColourField field)
        {
            return field switch
            {
                LegacyManiaSkinPerColumnColourField.ColumnBackground => acceptedColumnBackgroundColours,
                LegacyManiaSkinPerColumnColourField.ColumnLight => acceptedColumnLightColours,
                _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown legacy mania per-column colour field."),
            };
        }
    }
}
