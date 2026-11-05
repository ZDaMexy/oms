// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace osu.Game.Skinning.Gameplay
{
    public static partial class GameplaySkinSceneCodec
    {
        private static IReadOnlyList<GameplaySkinScoreCrossingThreshold> parseScoreCrossing(JToken? token, DecodeContext context)
        {
            var result = new List<GameplaySkinScoreCrossingThreshold>();
            if (token == null)
                return result;
            if (token is not JObject crossing)
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                return result;
            }
            validateFields(crossing, context, new[] { "thresholds" });
            if (getArray(crossing, "thresholds", context) is not JArray thresholds)
                return result;
            if (thresholds.Count is < 1 or > GameplaySkinScoreCrossingThreshold.MAX_THRESHOLDS)
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidAnimation);

            var stable = new bool[context.NumericSources.Count];
            for (int i = 0; i < stable.Length; i++)
            {
                GameplaySkinNumericSource declaration = context.NumericSources[i];
                stable[i] = isStable(declaration.Left) && (declaration.Right == null || isStable(declaration.Right));
            }
            var seen = new HashSet<(string, GameplaySkinScoreCrossingComparison)>();
            foreach (JToken item in thresholds.Take(GameplaySkinScoreCrossingThreshold.MAX_THRESHOLDS))
            {
                if (item is not JObject threshold)
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                    continue;
                }
                validateFields(threshold, context, new[] { "source", "comparison" });
                string? source = getString(threshold, "source", context);
                GameplaySkinScoreCrossingComparison? comparison = getString(threshold, "comparison", context) switch
                {
                    "greater" => GameplaySkinScoreCrossingComparison.Greater,
                    "at-least" => GameplaySkinScoreCrossingComparison.AtLeast,
                    _ => null,
                };
                if (!comparison.HasValue)
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidAnimation);
                GameplaySkinNumericOperand? operand = source == null ? null : parseNumericOperand(threshold, "source", context.NumericSources, context);
                if (operand == null || !comparison.HasValue)
                    continue;
                if (!isStable(operand) || !seen.Add((source!, comparison.Value)))
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidAnimation);
                    continue;
                }
                result.Add(new GameplaySkinScoreCrossingThreshold(operand, comparison.Value));
            }
            return result;

            bool isStable(GameplaySkinNumericOperand operand)
                => operand.ReferenceIndex >= 0 ? stable[operand.ReferenceIndex]
                    : operand.Constant.HasValue || GameplaySkinScoreCrossingThreshold.IsStableSource(operand.Source);
        }

        private static JObject encodeScoreCrossing(IReadOnlyList<GameplaySkinScoreCrossingThreshold> thresholds)
            => new()
            {
                ["thresholds"] = new JArray(thresholds.Select(threshold => new JObject
                {
                    ["source"] = threshold.SourceId,
                    ["comparison"] = threshold.Comparison == GameplaySkinScoreCrossingComparison.Greater ? "greater" : "at-least",
                })),
            };
    }
}
