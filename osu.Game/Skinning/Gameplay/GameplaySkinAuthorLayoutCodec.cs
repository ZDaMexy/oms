// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>Typed layouts field decoding, invoked only by the existing gameplay manifest codec.</summary>
    public static class GameplaySkinAuthorLayoutCodec
    {
        public static IReadOnlyList<GameplaySkinAuthorLayout> Decode(JToken? token, ICollection<GameplaySkinSceneDiagnostic> diagnostics)
        {
            var result = new List<GameplaySkinAuthorLayout>();
            if (token == null)
                return result.AsReadOnly();
            if (token is not JArray profiles)
            {
                add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                return result.AsReadOnly();
            }

            if (profiles.Count > GameplaySkinAuthorLayout.MAX_PROFILES)
                add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var selectors = new HashSet<string>(StringComparer.Ordinal);
            foreach (JToken item in profiles.Take(GameplaySkinAuthorLayout.MAX_PROFILES))
            {
                if (item is not JObject profile)
                {
                    add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                    continue;
                }
                fields(profile, "id", "ruleset", "keymode", "presentation", "stages", "keyAreaHeight", "gaugeHeight", "hitPosition");
                string? id = text(profile, "id"), ruleset = text(profile, "ruleset"), keymode = text(profile, "keymode"), presentation = text(profile, "presentation");
                double keyAreaHeight = number(profile, "keyAreaHeight", 0), gaugeHeight = number(profile, "gaugeHeight", 0), hitPosition = number(profile, "hitPosition", 1);
                var stages = new List<GameplaySkinAuthorLayoutStage>();
                if (profile["stages"] is not JArray sourceStages)
                {
                    add(profile["stages"] == null ? GameplaySkinSceneDiagnosticCode.MissingField : GameplaySkinSceneDiagnosticCode.InvalidValueType);
                    continue;
                }
                if (sourceStages.Count > GameplaySkinAuthorLayout.MAX_STAGES)
                    add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                foreach (JToken stageToken in sourceStages.Take(GameplaySkinAuthorLayout.MAX_STAGES))
                {
                    if (stageToken is not JObject stage)
                    {
                        add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                        continue;
                    }
                    fields(stage, "rect", "lanes", "keys");
                    double[]? rect = tuple(stage["rect"], 4);
                    IReadOnlyList<GameplaySkinAuthorLaneSpan>? lanes = spans(stage["lanes"]);
                    IReadOnlyList<GameplaySkinAuthorLaneSpan>? keys = stage["keys"] == null ? null : spans(stage["keys"]);
                    if (rect == null || lanes == null || stage["keys"] != null && keys == null)
                        continue;
                    try
                    {
                        stages.Add(new GameplaySkinAuthorLayoutStage(
                            GameplaySkinLayoutRect.Create((float)rect[0], (float)rect[1], (float)rect[2], (float)rect[3]), lanes, keys));
                    }
                    catch (ArgumentException)
                    {
                        add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                    }
                }
                if (id == null || ruleset == null || keymode == null || presentation == null)
                    continue;
                if (!ids.Add(id) || !selectors.Add($"{ruleset}\0{keymode}\0{presentation}"))
                    add(GameplaySkinSceneDiagnosticCode.DuplicateStableId);
                try
                {
                    result.Add(new GameplaySkinAuthorLayout(id, ruleset, keymode, presentation, stages, keyAreaHeight, gaugeHeight, hitPosition));
                }
                catch (ArgumentException)
                {
                    add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                }
            }
            return result.AsReadOnly();

            void add(GameplaySkinSceneDiagnosticCode code)
            {
                if (!diagnostics.Any(d => d.Code == code))
                    diagnostics.Add(new GameplaySkinSceneDiagnostic(code));
            }

            void fields(JObject value, params string[] allowed)
            {
                if (value.Properties().Any(p => !allowed.Contains(p.Name, StringComparer.Ordinal)))
                    add(GameplaySkinSceneDiagnosticCode.UnknownField);
            }

            string? text(JObject value, string name)
            {
                if (value[name] == null)
                {
                    add(GameplaySkinSceneDiagnosticCode.MissingField);
                    return null;
                }
                if (value[name]?.Type != JTokenType.String)
                {
                    add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                    return null;
                }
                string? parsed = value[name]!.Value<string>();
                if (!GameplaySkinAuthorLayout.IsToken(parsed))
                {
                    add(GameplaySkinSceneDiagnosticCode.InvalidStableId);
                    return null;
                }
                return parsed;
            }

            double number(JObject value, string name, double fallback)
            {
                if (value[name] == null)
                    return fallback;
                if (value[name]!.Type is JTokenType.Float or JTokenType.Integer)
                    return value[name]!.Value<double>();
                add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                return double.NaN;
            }

            double[]? tuple(JToken? value, int count)
            {
                if (value is not JArray array || array.Count != count
                    || array.Any(t => t.Type is not (JTokenType.Integer or JTokenType.Float)))
                {
                    add(value == null ? GameplaySkinSceneDiagnosticCode.MissingField : GameplaySkinSceneDiagnosticCode.InvalidValueType);
                    return null;
                }
                double[] numbers = array.Select(t => t.Value<double>()).ToArray();
                if (numbers.Any(n => !double.IsFinite(n)))
                {
                    add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                    return null;
                }
                return numbers;
            }

            IReadOnlyList<GameplaySkinAuthorLaneSpan>? spans(JToken? value)
            {
                if (value is not JArray array)
                {
                    add(value == null ? GameplaySkinSceneDiagnosticCode.MissingField : GameplaySkinSceneDiagnosticCode.InvalidValueType);
                    return null;
                }
                if (array.Count > GameplaySkinAuthorLayout.MAX_LANES_PER_STAGE)
                    add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                var values = new List<GameplaySkinAuthorLaneSpan>();
                foreach (JToken span in array.Take(GameplaySkinAuthorLayout.MAX_LANES_PER_STAGE))
                {
                    double[]? pair = tuple(span, 2);
                    if (pair != null)
                        values.Add(new GameplaySkinAuthorLaneSpan(pair[0], pair[1]));
                }
                return values.AsReadOnly();
            }
        }

        public static JArray Encode(IReadOnlyList<GameplaySkinAuthorLayout> layouts)
            => new JArray(layouts.Select(layout => new JObject
            {
                ["id"] = layout.Id,
                ["ruleset"] = layout.RulesetId,
                ["keymode"] = layout.KeymodeId,
                ["presentation"] = layout.Presentation,
                ["keyAreaHeight"] = layout.KeyAreaHeight,
                ["gaugeHeight"] = layout.GaugeHeight,
                ["hitPosition"] = layout.HitPosition,
                ["stages"] = new JArray(layout.Stages.Select(stage =>
                {
                    var value = new JObject
                    {
                        ["rect"] = new JArray(stage.Rect.X, stage.Rect.Y, stage.Rect.Width, stage.Rect.Height),
                        ["lanes"] = new JArray(stage.Lanes.Select(span => new JArray(span.X, span.Width))),
                    };
                    if (stage.HasExplicitKeys)
                        value["keys"] = new JArray(stage.Keys.Select(span => new JArray(span.X, span.Width)));
                    return value;
                })),
            }));
    }
}
