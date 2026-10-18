// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace osu.Game.Skinning.Gameplay
{
    public static partial class GameplaySkinSceneCodec
    {
        public static GameplaySkinSceneDecodeResult<GameplaySkinContentCatalog> DecodeContentCatalog(ReadOnlyMemory<byte> content)
        {
            if (content.Length > GameplaySkinContentBudgets.MAX_CATALOG_BYTES)
                return invalid<GameplaySkinContentCatalog>(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            if (!tryDecodeUtf8(content.Span, out _))
                return invalid<GameplaySkinContentCatalog>(GameplaySkinSceneDiagnosticCode.InvalidUtf8);

            var context = new DecodeContext();
            JObject? root = parseRoot(content.Span, context);
            if (root == null)
                return context.Invalid<GameplaySkinContentCatalog>();
            validateFields(root, context, new[] { "contract", "modules" });
            validateExactContract(root, "contract", GameplaySkinContentBudgets.CONTRACT_ID, context);
            JArray? modules = getArray(root, "modules", context);
            var result = new List<GameplaySkinContentModule>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int totalChoices = 0;
            if (modules != null)
            {
                if (modules.Count is 0 or > GameplaySkinOptionBudgets.MAX_OPTIONS)
                    context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                foreach (JToken token in modules.Take(GameplaySkinOptionBudgets.MAX_OPTIONS))
                {
                    if (token is not JObject module)
                    {
                        context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                        continue;
                    }
                    validateFields(module, context, new[] { "id", "label", "default", "choices" }, new[] { "rulesets", "keymodes" });
                    string? id = getStableId(module, "id", context);
                    string? label = optionLabel(module, "label", context);
                    string? defaultValue = getString(module, "default", context);
                    string[] rulesets = optionScopes(module["rulesets"], context);
                    string[] keymodes = optionScopes(module["keymodes"], context);
                    if (rulesets.Any(value => value is not ("bms" or "mania")))
                        context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                    if (id != null && !ids.Add(id))
                        context.Add(GameplaySkinSceneDiagnosticCode.DuplicateStableId);

                    var choices = new List<GameplaySkinContentChoice>();
                    var values = new HashSet<string>(StringComparer.Ordinal);
                    if (getArray(module, "choices", context) is JArray choiceArray)
                    {
                        totalChoices += choiceArray.Count;
                        if (choiceArray.Count is 0 or > GameplaySkinContentBudgets.MAX_CHOICES_PER_MODULE
                            || totalChoices > GameplaySkinContentBudgets.MAX_TOTAL_CHOICES)
                            context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                        foreach (JToken choiceToken in choiceArray.Take(GameplaySkinContentBudgets.MAX_CHOICES_PER_MODULE))
                        {
                            if (choiceToken is not JObject choice)
                            {
                                context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                                continue;
                            }
                            validateFields(choice, context, new[] { "value", "label" }, new[] { "manifest", "scene", "ini", "variants" });
                            string? value = getStableId(choice, "value", context);
                            string? choiceLabel = optionLabel(choice, "label", context);
                            string? manifest = contentPath(choice, "manifest", context);
                            string? scene = contentPath(choice, "scene", context);
                            string? ini = contentPath(choice, "ini", context);
                            if (choice.ContainsKey("manifest") != choice.ContainsKey("scene"))
                                context.Add(GameplaySkinSceneDiagnosticCode.InvalidReference);
                            if (value != null && !values.Add(value))
                                context.Add(GameplaySkinSceneDiagnosticCode.DuplicateField);
                            IReadOnlyList<GameplaySkinContentVariant> variants = contentVariants(choice["variants"], context);
                            if (value != null && choiceLabel != null)
                                choices.Add(new GameplaySkinContentChoice(value, choiceLabel, manifest, scene, ini, variants));
                        }
                    }
                    if (id == null || label == null || defaultValue == null)
                        continue;
                    var option = new GameplaySkinOptionDefinition(id, label, GameplaySkinOptionKind.Choice, defaultValue,
                        choices.Select(choice => new GameplaySkinOptionChoice(choice.Value, choice.Label)), 0, 0, 0,
                        rulesets, keymodes, GameplaySkinOptionEffect.None, isContentCatalog: true);
                    if (!option.TryValidateValue(defaultValue, out _))
                        context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                    result.Add(new GameplaySkinContentModule(option, choices));
                }
            }
            return context.HasDiagnostics ? context.Invalid<GameplaySkinContentCatalog>() : valid(new GameplaySkinContentCatalog(result));
        }

        private static IReadOnlyList<GameplaySkinContentVariant> contentVariants(JToken? token, DecodeContext context)
        {
            var result = new List<GameplaySkinContentVariant>();
            if (token == null)
                return result;
            if (token is not JArray variants)
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                return result;
            }
            if (variants.Count is 0 or > GameplaySkinContentBudgets.MAX_VARIANTS_PER_CHOICE)
                context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            string[]? selectorKeys = null;
            var tuples = new HashSet<string>(StringComparer.Ordinal);
            foreach (JToken item in variants.Take(GameplaySkinContentBudgets.MAX_VARIANTS_PER_CHOICE))
            {
                if (item is not JObject variant)
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                    continue;
                }
                validateFields(variant, context, new[] { "when" }, new[] { "manifest", "scene", "ini" });
                var when = new Dictionary<string, string>(StringComparer.Ordinal);
                if (variant["when"] is not JObject conditions)
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                    continue;
                }
                if (conditions.Count is 0 or > GameplaySkinOptionBudgets.MAX_OPTIONS)
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidReference);
                foreach (JProperty condition in conditions.Properties().Take(GameplaySkinOptionBudgets.MAX_OPTIONS))
                {
                    string? id = getStableId(new JValue(condition.Name), context);
                    string? value = condition.Value.Type switch
                    {
                        JTokenType.Boolean => condition.Value.Value<bool>() ? "true" : "false",
                        JTokenType.String => getStableId(condition.Value, context),
                        _ => null,
                    };
                    if (value == null)
                        context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                    if (id != null && value != null)
                        when.Add(id, value);
                }
                string[] keys = [.. when.Keys.OrderBy(key => key, StringComparer.Ordinal)];
                if (selectorKeys == null)
                    selectorKeys = keys;
                else if (!selectorKeys.SequenceEqual(keys, StringComparer.Ordinal))
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidReference);
                string tuple = string.Join("\u001f", keys.Select(key => key + "=" + when[key]));
                if (!tuples.Add(tuple))
                    context.Add(GameplaySkinSceneDiagnosticCode.DuplicateField);
                string? manifest = contentPath(variant, "manifest", context);
                string? scene = contentPath(variant, "scene", context);
                string? ini = contentPath(variant, "ini", context);
                if (variant.ContainsKey("manifest") != variant.ContainsKey("scene"))
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidReference);
                result.Add(new GameplaySkinContentVariant(when, manifest, scene, ini));
            }
            return result;
        }

        private static string? contentPath(JObject value, string field, DecodeContext context)
        {
            if (!value.ContainsKey(field))
                return null;
            string? raw = getString(value, field, context);
            if (raw == null)
                return null;
            if (tryNormalizeRelativeResourcePath(raw, out string normalized))
                return normalized;
            context.Add(GameplaySkinSceneDiagnosticCode.UnsafeResourcePath);
            return null;
        }
    }
}
