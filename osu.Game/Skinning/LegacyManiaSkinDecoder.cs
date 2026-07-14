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
                            currentConfig.NoteBodyStyle = style;
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

                        LegacyManiaSkinKnownGlobalColourField? knownGlobalColour = pair.Key switch
                        {
                            "ColourColumnLine" => LegacyManiaSkinKnownGlobalColourField.ColumnLine,
                            "ColourJudgementLine" => LegacyManiaSkinKnownGlobalColourField.JudgementLine,
                            "ColourBreak" => LegacyManiaSkinKnownGlobalColourField.ComboBreak,
                            "ColourBarline" => LegacyManiaSkinKnownGlobalColourField.BarLine,
                            _ => null,
                        };

                        if (knownGlobalColour.HasValue)
                            currentConfig.AcceptKnownGlobalColour(knownGlobalColour.Value, currentConfig.CustomColours[pair.Key]);

                        break;

                    // Custom sprite paths
                    case string when pair.Key.StartsWith("NoteImage", StringComparison.Ordinal):
                    case string when pair.Key.StartsWith("KeyImage", StringComparison.Ordinal):
                    case string when pair.Key.StartsWith("Hit", StringComparison.Ordinal):
                    case string when pair.Key.StartsWith("Stage", StringComparison.Ordinal):
                    case string when pair.Key.StartsWith("Lighting", StringComparison.Ordinal):
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
    }
}
