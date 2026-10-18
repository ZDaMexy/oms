// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace osu.Game.Skinning.Gameplay
{
    public static partial class GameplaySkinSceneCodec
    {
        private static List<GameplaySkinOptionDefinition> parseOptions(JToken? token, DecodeContext context)
        {
            var result = new List<GameplaySkinOptionDefinition>();
            if (token == null)
                return result;
            if (token is not JArray options)
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                return result;
            }

            if (options.Count > GameplaySkinOptionBudgets.MAX_OPTIONS)
                context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (JToken item in options.Take(GameplaySkinOptionBudgets.MAX_OPTIONS))
            {
                if (item is not JObject option)
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                    continue;
                }

                string? type = getString(option, "type", context);
                string[] typeFields = type switch
                {
                    "choice" => new[] { "choices" },
                    "number" => new[] { "min", "max", "step" },
                    _ => Array.Empty<string>(),
                };
                validateFields(option, context, new[] { "id", "type", "label", "default" }.Concat(typeFields).ToArray(),
                    new[] { "rulesets", "keymodes", "effect" });
                string? id = getStableId(option, "id", context);
                string? label = optionLabel(option, "label", context);
                if (id != null && !ids.Add(id))
                    context.Add(GameplaySkinSceneDiagnosticCode.DuplicateStableId);
                string[] rulesets = optionScopes(option["rulesets"], context);
                string[] keymodes = optionScopes(option["keymodes"], context);
                if (rulesets.Any(value => value is not ("bms" or "mania")))
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);

                GameplaySkinOptionEffect effect = GameplaySkinOptionEffect.None;
                if (option.ContainsKey("effect"))
                {
                    string? effectId = getString(option, "effect", context);
                    if (effectId == "presentation")
                        effect = GameplaySkinOptionEffect.Presentation;
                    else
                        context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                }

                var choices = new List<GameplaySkinOptionChoice>();
                string? defaultValue = null;
                GameplaySkinOptionKind kind;
                double minimum = 0, maximum = 0, step = 0;
                switch (type)
                {
                    case "choice":
                        kind = GameplaySkinOptionKind.Choice;
                        defaultValue = getString(option, "default", context);
                        if (getArray(option, "choices", context) is JArray choiceArray)
                        {
                            if (choiceArray.Count == 0)
                                context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                            if (choiceArray.Count > GameplaySkinOptionBudgets.MAX_CHOICES)
                                context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                            var values = new HashSet<string>(StringComparer.Ordinal);
                            foreach (JToken choiceToken in choiceArray.Take(GameplaySkinOptionBudgets.MAX_CHOICES))
                            {
                                if (choiceToken is not JObject choice)
                                {
                                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                                    continue;
                                }
                                validateFields(choice, context, new[] { "value", "label" }, new[] { "preview" });
                                string? value = getStableId(choice, "value", context);
                                string? choiceLabel = optionLabel(choice, "label", context);
                                string? preview = optionPreviewPath(choice, context);
                                if (value != null && !values.Add(value))
                                    context.Add(GameplaySkinSceneDiagnosticCode.DuplicateField);
                                if (value != null && choiceLabel != null)
                                    choices.Add(new GameplaySkinOptionChoice(value, choiceLabel, preview));
                            }
                        }
                        break;

                    case "toggle":
                        kind = GameplaySkinOptionKind.Toggle;
                        bool? boolean = getBoolean(option, "default", context);
                        if (boolean.HasValue)
                            defaultValue = boolean.Value ? "true" : "false";
                        break;

                    case "number":
                        kind = GameplaySkinOptionKind.Number;
                        double? number = getNumber(option, "default", context);
                        double? min = getNumber(option, "min", context);
                        double? max = getNumber(option, "max", context);
                        double? increment = getNumber(option, "step", context);
                        if (!number.HasValue || !min.HasValue || !max.HasValue || !increment.HasValue)
                            continue;
                        minimum = min.Value;
                        maximum = max.Value;
                        step = increment.Value;
                        if (Math.Abs(minimum) > GameplaySkinOptionBudgets.MAX_ABSOLUTE_NUMBER
                            || Math.Abs(maximum) > GameplaySkinOptionBudgets.MAX_ABSOLUTE_NUMBER
                            || minimum >= maximum || step <= 0)
                        {
                            context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                            continue;
                        }
                        double steps = (maximum - minimum) / step;
                        if (!double.IsFinite(steps) || steps > GameplaySkinOptionBudgets.MAX_NUMBER_STEPS)
                        {
                            context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                            continue;
                        }
                        if (steps < 1 || Math.Abs(steps - Math.Round(steps)) > 0.0000001)
                        {
                            context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                            continue;
                        }
                        defaultValue = number.Value.ToString("G17", CultureInfo.InvariantCulture);
                        break;

                    default:
                        context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                        continue;
                }

                if (effect == GameplaySkinOptionEffect.Presentation
                    && (kind != GameplaySkinOptionKind.Choice || choices.Any(choice => choice.Value is not ("p1" or "p2" or "center-p1" or "center-p2"))))
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                }
                if (id == null || label == null || defaultValue == null)
                    continue;
                var definition = new GameplaySkinOptionDefinition(id, label, kind, defaultValue, choices, minimum, maximum, step, rulesets, keymodes, effect);
                if (!definition.TryValidateValue(defaultValue, out GameplaySkinOptionValue validated))
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                    continue;
                }
                if (effect == GameplaySkinOptionEffect.Presentation && result.Any(other => other.Effect == effect
                        && scopesOverlap(other.Rulesets, rulesets) && scopesOverlap(other.Keymodes, keymodes)))
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                }
                result.Add(new GameplaySkinOptionDefinition(id, label, kind, validated.CanonicalValue, choices, minimum, maximum, step, rulesets, keymodes, effect));
            }
            return result;
        }

        private static bool scopesOverlap(IReadOnlyList<string> first, IReadOnlyList<string> second)
            => first.Count == 0 || second.Count == 0 || first.Intersect(second, StringComparer.Ordinal).Any();

        private static string? optionPreviewPath(JObject choice, DecodeContext context)
        {
            string? path = contentPath(choice, "preview", context);
            if (path != null && !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                return null;
            }
            return path;
        }

        private static string? optionLabel(JObject obj, string field, DecodeContext context)
        {
            string? label = getString(obj, field, context);
            if (label != null && (string.IsNullOrWhiteSpace(label) || label.Length > GameplaySkinOptionBudgets.MAX_LABEL_LENGTH || label.Any(char.IsControl)))
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                return null;
            }
            return label;
        }

        private static string[] optionScopes(JToken? token, DecodeContext context)
        {
            if (token == null)
                return Array.Empty<string>();
            if (token is not JArray values)
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                return Array.Empty<string>();
            }
            if (values.Count == 0)
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
            if (values.Count > GameplaySkinOptionBudgets.MAX_APPLICABILITY_TOKENS)
                context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (JToken value in values.Take(GameplaySkinOptionBudgets.MAX_APPLICABILITY_TOKENS))
            {
                string? scope = getString(value, context);
                if (scope == null)
                    continue;
                if (scope.Length is 0 or > 80 || scope.Any(character => character is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '.')))
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                else if (!result.Add(scope))
                    context.Add(GameplaySkinSceneDiagnosticCode.DuplicateField);
            }
            return result.ToArray();
        }

        private static JArray encodeOptions(IReadOnlyList<GameplaySkinOptionDefinition> options) => new JArray(options.Select(option =>
        {
            var value = new JObject
            {
                ["id"] = option.Id,
                ["type"] = option.Kind switch { GameplaySkinOptionKind.Choice => "choice", GameplaySkinOptionKind.Toggle => "toggle", _ => "number" },
                ["label"] = option.Label,
                ["default"] = option.Kind switch
                {
                    GameplaySkinOptionKind.Choice => new JValue(option.DefaultValue),
                    GameplaySkinOptionKind.Toggle => new JValue(option.DefaultValue == "true"),
                    _ => new JValue(double.Parse(option.DefaultValue, CultureInfo.InvariantCulture)),
                },
            };
            if (option.Kind == GameplaySkinOptionKind.Choice)
                value["choices"] = new JArray(option.Choices.Select(choice =>
                {
                    var declaration = new JObject { ["value"] = choice.Value, ["label"] = choice.Label };
                    if (choice.PreviewPath != null)
                        declaration["preview"] = choice.PreviewPath;
                    return declaration;
                }));
            else if (option.Kind == GameplaySkinOptionKind.Number)
            {
                value["min"] = option.Minimum;
                value["max"] = option.Maximum;
                value["step"] = option.Step;
            }
            if (option.Rulesets.Count > 0)
                value["rulesets"] = new JArray(option.Rulesets);
            if (option.Keymodes.Count > 0)
                value["keymodes"] = new JArray(option.Keymodes);
            if (option.Effect == GameplaySkinOptionEffect.Presentation)
                value["effect"] = "presentation";
            return value;
        }));

        private static GameplaySkinOptionDefinition? optionSource(string source, GameplaySkinSceneManifest? manifest)
        {
            if (source.StartsWith("option.", StringComparison.Ordinal) && manifest != null
                && manifest.TryGetOption(source[7..], out GameplaySkinOptionDefinition option))
                return option;
            return null;
        }

        private static bool isKnownBindingSource(string source, GameplaySkinSceneManifest? manifest)
            => binding_source_ids.Contains(source) || optionSource(source, manifest) != null;

        private static bool tryGetVariantKeys(string source, GameplaySkinSceneManifest? manifest, out HashSet<string>? keys)
        {
            if (variant_source_keys.TryGetValue(source, out keys))
                return true;
            GameplaySkinOptionDefinition? definition = optionSource(source, manifest);
            keys = definition?.Kind switch
            {
                GameplaySkinOptionKind.Choice => new HashSet<string>(definition.Choices.Select(choice => choice.Value), StringComparer.Ordinal),
                GameplaySkinOptionKind.Toggle => new HashSet<string>(new[] { "true", "false" }, StringComparer.Ordinal),
                _ => null,
            };
            return keys != null;
        }
    }
}
