// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using osu.Game.Beatmaps.Formats;

namespace osu.Game.Skinning
{
    public class LegacyManiaSkinDecoder : LegacyDecoder<List<LegacyManiaSkinConfiguration>>
    {
        public LegacyManiaSkinDecoder()
            : base(1)
        {
        }

        private readonly List<string> pendingLines = new List<string>();
        private LegacyManiaSkinConfiguration currentConfig;

        protected override void OnBeginNewSection(Section section)
        {
            base.OnBeginNewSection(section);

            // If a new section is reached with pending lines remaining, they can all be discarded as there isn't a valid configuration to parse them into.
            pendingLines.Clear();
            currentConfig = null;
        }

        protected override void ParseLine(List<LegacyManiaSkinConfiguration> output, Section section, string line)
        {
            switch (section)
            {
                case Section.Mania:
                    var pair = SplitKeyVal(line);

                    switch (pair.Key)
                    {
                        case "Keys":
                            currentConfig = new LegacyManiaSkinConfiguration(int.Parse(pair.Value, CultureInfo.InvariantCulture));

                            // Silently ignore duplicate configurations.
                            if (output.All(c => c.Keys != currentConfig.Keys))
                                output.Add(currentConfig);

                            // All existing lines can be flushed now that we have a valid configuration.
                            flushPendingLines();
                            break;

                        default:
                            pendingLines.Add(line);

                            // Hold all lines until a "Keys" item is found.
                            if (currentConfig != null)
                                flushPendingLines();
                            break;
                    }

                    break;
            }
        }

        private void flushPendingLines()
        {
            Debug.Assert(currentConfig != null);

            foreach (string line in pendingLines)
            {
                var pair = SplitKeyVal(line);

                switch (pair.Key)
                {
                    case "ColumnLineWidth":
                        parseArrayValue(pair.Value, LegacyManiaSkinArrayField.ColumnLineWidth);
                        break;

                    case "ColumnSpacing":
                        parseArrayValue(pair.Value, LegacyManiaSkinArrayField.ColumnSpacing);
                        break;

                    case "ColumnWidth":
                        parseArrayValue(pair.Value, LegacyManiaSkinArrayField.ColumnWidth);
                        break;

                    case "BarlineHeight":
                        currentConfig.BarLineHeight = float.Parse(pair.Value, CultureInfo.InvariantCulture);
                        currentConfig.MarkScalarDeclared(LegacyManiaSkinScalarField.BarLineHeight);
                        break;

                    case "HitPosition":
                        currentConfig.HitPosition = (480 - Math.Clamp(float.Parse(pair.Value, CultureInfo.InvariantCulture), 240, 480)) * LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR;
                        currentConfig.MarkScalarDeclared(LegacyManiaSkinScalarField.HitPosition);
                        break;

                    case "LightPosition":
                        currentConfig.LightPosition = (480 - float.Parse(pair.Value, CultureInfo.InvariantCulture)) * LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR;
                        currentConfig.MarkScalarDeclared(LegacyManiaSkinScalarField.LightPosition);
                        break;

                    case "ComboPosition":
                        currentConfig.ComboPosition = (float.Parse(pair.Value, CultureInfo.InvariantCulture)) * LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR;
                        currentConfig.MarkScalarDeclared(LegacyManiaSkinScalarField.ComboPosition);
                        break;

                    case "ScorePosition":
                        currentConfig.ScorePosition = (float.Parse(pair.Value, CultureInfo.InvariantCulture)) * LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR;
                        currentConfig.MarkScalarDeclared(LegacyManiaSkinScalarField.ScorePosition);
                        break;

                    case "JudgementLine":
                        currentConfig.ShowJudgementLine = pair.Value == "1";
                        currentConfig.MarkScalarDeclared(LegacyManiaSkinScalarField.ShowJudgementLine);
                        break;

                    case "KeysUnderNotes":
                        currentConfig.KeysUnderNotes = pair.Value == "1";
                        currentConfig.MarkScalarDeclared(LegacyManiaSkinScalarField.KeysUnderNotes);
                        break;

                    case "LightingNWidth":
                        parseArrayValue(pair.Value, LegacyManiaSkinArrayField.ExplosionWidth);
                        break;

                    case "LightingLWidth":
                        parseArrayValue(pair.Value, LegacyManiaSkinArrayField.HoldNoteLightWidth);
                        break;

                    case "NoteBodyStyle":
                        if (Enum.TryParse<LegacyNoteBodyStyle>(pair.Value, out var style))
                            currentConfig.AcceptNoteBodyStyle(style);
                        break;

                    case "WidthForNoteHeightScale":
                        currentConfig.WidthForNoteHeightScale = (float.Parse(pair.Value, CultureInfo.InvariantCulture)) * LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR;
                        currentConfig.MarkScalarDeclared(LegacyManiaSkinScalarField.WidthForNoteHeightScale);
                        break;

                    case "LightFramePerSecond":
                        int lightFramePerSecond = int.Parse(pair.Value, CultureInfo.InvariantCulture);
                        currentConfig.LightFramePerSecond = lightFramePerSecond > 0 ? lightFramePerSecond : 24;
                        currentConfig.MarkScalarDeclared(LegacyManiaSkinScalarField.LightFramePerSecond);
                        break;

                    case string when pair.Key.StartsWith("Colour", StringComparison.Ordinal):
                        HandleColours(currentConfig, line, true);

                        var acceptedColour = currentConfig.CustomColours[pair.Key];

                        if (tryGetPerColumnColour(pair.Key, currentConfig.Keys, out LegacyManiaSkinPerColumnColourField perColumnColour, out int sourceColumnIndex))
                            currentConfig.AcceptPerColumnColour(perColumnColour, sourceColumnIndex, acceptedColour);

                        LegacyManiaSkinKnownGlobalColourField? knownGlobalColour = pair.Key switch
                        {
                            "ColourColumnLine" => LegacyManiaSkinKnownGlobalColourField.ColumnLine,
                            "ColourJudgementLine" => LegacyManiaSkinKnownGlobalColourField.JudgementLine,
                            "ColourBreak" => LegacyManiaSkinKnownGlobalColourField.ComboBreak,
                            "ColourBarline" => LegacyManiaSkinKnownGlobalColourField.BarLine,
                            _ => null,
                        };

                        if (knownGlobalColour.HasValue)
                            currentConfig.AcceptKnownGlobalColour(knownGlobalColour.Value, acceptedColour);

                        break;

                    // Custom sprite paths
                    case string when pair.Key.StartsWith("NoteImage", StringComparison.Ordinal):
                    case string when pair.Key.StartsWith("KeyImage", StringComparison.Ordinal):
                    case string when pair.Key.StartsWith("Hit", StringComparison.Ordinal):
                    case string when pair.Key.StartsWith("Stage", StringComparison.Ordinal):
                    case string when pair.Key.StartsWith("Lighting", StringComparison.Ordinal):
                        if (tryGetLaneResource(
                                pair.Key,
                                currentConfig.Keys,
                                out LegacyManiaSkinLaneResourceField laneResourceField,
                                out int laneSourceColumnIndex))
                        {
                            currentConfig.AcceptLaneResource(laneResourceField, laneSourceColumnIndex, pair.Value);
                            break;
                        }

                        LegacyManiaSkinKnownGlobalResourceField? knownGlobalResource = pair.Key switch
                        {
                            "LightingN" => LegacyManiaSkinKnownGlobalResourceField.LightingN,
                            "LightingL" => LegacyManiaSkinKnownGlobalResourceField.LightingL,
                            "StageLeft" => LegacyManiaSkinKnownGlobalResourceField.StageLeft,
                            "StageRight" => LegacyManiaSkinKnownGlobalResourceField.StageRight,
                            "StageBottom" => LegacyManiaSkinKnownGlobalResourceField.StageBottom,
                            "StageLight" => LegacyManiaSkinKnownGlobalResourceField.StageLight,
                            "StageHint" => LegacyManiaSkinKnownGlobalResourceField.StageHint,
                            "Hit0" => LegacyManiaSkinKnownGlobalResourceField.Hit0,
                            "Hit50" => LegacyManiaSkinKnownGlobalResourceField.Hit50,
                            "Hit100" => LegacyManiaSkinKnownGlobalResourceField.Hit100,
                            "Hit200" => LegacyManiaSkinKnownGlobalResourceField.Hit200,
                            "Hit300" => LegacyManiaSkinKnownGlobalResourceField.Hit300,
                            "Hit300g" => LegacyManiaSkinKnownGlobalResourceField.Hit300g,
                            _ => null,
                        };

                        if (knownGlobalResource.HasValue)
                            currentConfig.AcceptKnownGlobalResource(knownGlobalResource.Value, pair.Value);
                        else
                            currentConfig.ImageLookups[pair.Key] = pair.Value;

                        break;
                }
            }

            pendingLines.Clear();
        }

        private void parseArrayValue(string value, LegacyManiaSkinArrayField field)
        {
            string[] values = value.Split(',');
            bool applyScaleFactor = field switch
            {
                LegacyManiaSkinArrayField.ColumnLineWidth => false,
                LegacyManiaSkinArrayField.ColumnSpacing or
                    LegacyManiaSkinArrayField.ColumnWidth or
                    LegacyManiaSkinArrayField.ExplosionWidth or
                    LegacyManiaSkinArrayField.HoldNoteLightWidth => true,
                _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown legacy mania array field."),
            };

            for (int i = 0; i < values.Length; i++)
            {
                if (i >= currentConfig.GetArrayLength(field))
                    break;

                if (!float.TryParse(values[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedValue))
                    // some skins may provide incorrect entries in array values. to match stable behaviour, read such entries as zero.
                    // see: https://github.com/ppy/osu/issues/26464, stable code: https://github.com/peppy/osu-stable-reference/blob/3ea48705eb67172c430371dcfc8a16a002ed0d3d/osu!/Graphics/Skinning/Components/Section.cs#L134-L137
                    parsedValue = 0;

                if (applyScaleFactor)
                    parsedValue *= LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR;

                currentConfig.AcceptArrayValue(field, i, parsedValue);
            }
        }

        private static bool tryGetLaneResource(
            string key,
            int sourceColumnCount,
            out LegacyManiaSkinLaneResourceField field,
            out int sourceColumnIndex)
        {
            field = default;
            sourceColumnIndex = -1;

            const string note_prefix = "NoteImage";
            const string key_prefix = "KeyImage";

            string sourceIndexToken;
            LegacyManiaSkinLaneResourceField candidateField;

            if (key.StartsWith(note_prefix, StringComparison.Ordinal))
            {
                sourceIndexToken = key[note_prefix.Length..];
                candidateField = LegacyManiaSkinLaneResourceField.Note;

                if (sourceIndexToken.Length > 0)
                {
                    candidateField = sourceIndexToken[^1] switch
                    {
                        'H' => LegacyManiaSkinLaneResourceField.LongNoteHead,
                        'L' => LegacyManiaSkinLaneResourceField.LongNoteBody,
                        'T' => LegacyManiaSkinLaneResourceField.LongNoteTail,
                        _ => candidateField,
                    };

                    if (candidateField != LegacyManiaSkinLaneResourceField.Note)
                        sourceIndexToken = sourceIndexToken[..^1];
                }
            }
            else if (key.StartsWith(key_prefix, StringComparison.Ordinal))
            {
                sourceIndexToken = key[key_prefix.Length..];
                candidateField = LegacyManiaSkinLaneResourceField.Key;

                if (sourceIndexToken.EndsWith('D'))
                {
                    candidateField = LegacyManiaSkinLaneResourceField.KeyPressed;
                    sourceIndexToken = sourceIndexToken[..^1];
                }
            }
            else
                return false;

            if (!int.TryParse(sourceIndexToken, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedSourceColumn)
                || parsedSourceColumn < 0
                || parsedSourceColumn >= sourceColumnCount
                || !string.Equals(sourceIndexToken, parsedSourceColumn.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                return false;

            field = candidateField;
            sourceColumnIndex = parsedSourceColumn;
            return true;
        }

        private static bool tryGetPerColumnColour(
            string key,
            int sourceColumnCount,
            out LegacyManiaSkinPerColumnColourField field,
            out int sourceColumnIndex)
        {
            field = default;
            sourceColumnIndex = -1;

            const string background_prefix = "Colour";
            const string light_prefix = "ColourLight";

            string prefix;
            LegacyManiaSkinPerColumnColourField candidateField;

            if (key.StartsWith(light_prefix, StringComparison.Ordinal))
            {
                prefix = light_prefix;
                candidateField = LegacyManiaSkinPerColumnColourField.ColumnLight;
            }
            else if (key.StartsWith(background_prefix, StringComparison.Ordinal))
            {
                prefix = background_prefix;
                candidateField = LegacyManiaSkinPerColumnColourField.ColumnBackground;
            }
            else
                return false;

            string sourceIndexToken = key[prefix.Length..];

            if (!int.TryParse(sourceIndexToken, NumberStyles.None, CultureInfo.InvariantCulture, out int oneBasedSourceColumn)
                || oneBasedSourceColumn < 1
                || oneBasedSourceColumn > sourceColumnCount
                || !string.Equals(sourceIndexToken, oneBasedSourceColumn.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                return false;

            field = candidateField;
            sourceColumnIndex = oneBasedSourceColumn - 1;
            return true;
        }
    }
}
