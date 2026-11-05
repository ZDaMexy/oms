// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace osu.Game.Skinning.Gameplay
{
    public static partial class GameplaySkinSceneCodec
    {
        private static List<GameplaySkinNumericSource> parseNumericSources(JToken? token, DecodeContext context)
        {
            var result = new List<GameplaySkinNumericSource>();
            if (token == null)
                return result;
            if (token is not JArray array)
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                return result;
            }
            if (array.Count > GameplaySkinNumericSources.MAX_DECLARATIONS)
                context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            foreach (JToken item in array.Take(GameplaySkinNumericSources.MAX_DECLARATIONS))
            {
                if (item is not JObject declaration)
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                    continue;
                }
                string? id = getStableId(declaration, "id", context);
                string? operationId = getString(declaration, "operation", context);
                GameplaySkinNumericOperation? operation = operationId switch
                {
                    "add" => GameplaySkinNumericOperation.Add,
                    "subtract" => GameplaySkinNumericOperation.Subtract,
                    "multiply" => GameplaySkinNumericOperation.Multiply,
                    "divide" => GameplaySkinNumericOperation.Divide,
                    "min" => GameplaySkinNumericOperation.Min,
                    "max" => GameplaySkinNumericOperation.Max,
                    "floor" => GameplaySkinNumericOperation.Floor,
                    "ceil" => GameplaySkinNumericOperation.Ceil,
                    "float32" => GameplaySkinNumericOperation.Float32,
                    "multiply32" => GameplaySkinNumericOperation.Multiply32,
                    "divide32" => GameplaySkinNumericOperation.Divide32,
                    _ => null,
                };
                bool unary = operation is GameplaySkinNumericOperation.Floor or GameplaySkinNumericOperation.Ceil or GameplaySkinNumericOperation.Float32;
                validateFields(declaration, context, unary ? new[] { "id", "operation", "left" } : new[] { "id", "operation", "left", "right" });
                if (id != null && !context.AllStableIds.Add(id))
                    context.Add(GameplaySkinSceneDiagnosticCode.DuplicateStableId);
                if (!operation.HasValue)
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                GameplaySkinNumericOperand? left = parseNumericOperand(declaration, "left", result, context);
                GameplaySkinNumericOperand? right = unary ? null : parseNumericOperand(declaration, "right", result, context);
                if (id == null || !operation.HasValue || left == null || !unary && right == null)
                    continue;
                if (operation == GameplaySkinNumericOperation.Divide && right!.ConstantResult == 0
                    || operation == GameplaySkinNumericOperation.Divide32 && right!.ConstantResult is double denominator && (float)denominator == 0)
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                    continue;
                }
                var value = new GameplaySkinNumericSource(id, operation.Value, left, right);
                if (value.Depth > GameplaySkinNumericSources.MAX_DEPTH)
                    context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                if (left.ConstantResult.HasValue && (unary || right!.ConstantResult.HasValue) && !value.ConstantResult.HasValue)
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                result.Add(value);
            }
            return result;
        }

        private static GameplaySkinNumericOperand? parseNumericOperand(JObject declaration, string field,
                                                                       IReadOnlyList<GameplaySkinNumericSource> preceding, DecodeContext context)
        {
            JToken? token = declaration[field];
            if (token?.Type is JTokenType.Float or JTokenType.Integer)
            {
                double? number = getNumber(declaration, field, context);
                return number.HasValue ? new GameplaySkinNumericOperand(number.Value) : null;
            }
            string? sourceId = getString(declaration, field, context);
            if (sourceId == null)
                return null;
            if (sourceId.StartsWith("value.", StringComparison.Ordinal))
            {
                for (int i = 0; i < preceding.Count; i++)
                    if (preceding[i].Id == sourceId[6..])
                        return new GameplaySkinNumericOperand(sourceId, i, preceding[i]);
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidReference);
                return null;
            }
            if (optionSource(sourceId, context.Manifest) is { Kind: GameplaySkinOptionKind.Number } option)
                return new GameplaySkinNumericOperand(sourceId, option.Minimum == Math.Truncate(option.Minimum) && option.Step == Math.Truncate(option.Step), option.Minimum < 0);
            GameplaySkinSceneBindingSource source = GameplaySkinSceneVocabulary.ParseBindingSource(sourceId);
            if (GameplaySkinNumericSources.IsGlobalSource(source))
                return new GameplaySkinNumericOperand(sourceId, GameplaySkinNumericSources.IsIntegerSource(source), false);
            context.Add(GameplaySkinSceneDiagnosticCode.UnknownBindingSource);
            return null;
        }

        private static JArray encodeNumericSources(IReadOnlyList<GameplaySkinNumericSource> sources) => new JArray(sources.Select(source =>
        {
            var result = new JObject
            {
                ["id"] = source.Id,
                ["operation"] = source.Operation.ToString().ToLowerInvariant(),
                ["left"] = numericOperand(source.Left),
            };
            if (source.Right != null)
                result["right"] = numericOperand(source.Right);
            return result;
        }));

        private static JToken numericOperand(GameplaySkinNumericOperand operand)
            => operand.Constant.HasValue ? new JValue(operand.Constant.Value) : new JValue(operand.SourceId!);

        private static GameplaySkinNumericSource? numericSource(string source, DecodeContext context)
            => source.StartsWith("value.", StringComparison.Ordinal)
                ? context.NumericSources.FirstOrDefault(value => value.Id == source[6..]) : null;

        private static bool isKnownBindingSource(string source, DecodeContext context)
            => isKnownBindingSource(source, context.Manifest) || numericSource(source, context) != null;

        private static bool bindingSourceCanDriveProperty(string source, string property, DecodeContext context)
            => numericSource(source, context) != null
                ? property == "text" || number_properties.Contains(property) && !isStaticProperty(property)
                : bindingSourceCanDriveProperty(source, property, context.Manifest);
    }
}
