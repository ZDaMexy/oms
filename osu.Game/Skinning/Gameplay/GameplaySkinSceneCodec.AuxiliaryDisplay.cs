// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace osu.Game.Skinning.Gameplay
{
    public static partial class GameplaySkinSceneCodec
    {
        private static GameplaySkinAuxiliaryDisplayDefinition? parseAuxiliaryDisplay(JToken? token,
                                                                                    IReadOnlyList<GameplaySkinOptionDefinition> options, DecodeContext context)
        {
            if (token == null)
                return null;
            if (token is not JObject declaration)
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                return null;
            }
            validateFields(declaration, context, new[] { "enabledOption", "concentrationOption", "brightnessOption" });
            string? enabledId = getStableId(declaration, "enabledOption", context);
            string? concentrationId = getStableId(declaration, "concentrationOption", context);
            string? brightnessId = getStableId(declaration, "brightnessOption", context);
            GameplaySkinOptionDefinition? enabled = options.FirstOrDefault(option => option.Id == enabledId);
            GameplaySkinOptionDefinition? concentration = options.FirstOrDefault(option => option.Id == concentrationId);
            GameplaySkinOptionDefinition? brightness = options.FirstOrDefault(option => option.Id == brightnessId);
            if (enabled is not { Kind: GameplaySkinOptionKind.Toggle } || concentration is not { Kind: GameplaySkinOptionKind.Toggle }
                || brightness is not { Kind: GameplaySkinOptionKind.Number } || brightness.Minimum < 0 || brightness.Maximum > 1
                || !sameScopes(enabled, concentration) || !sameScopes(enabled, brightness))
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidReference);
                return null;
            }
            return new GameplaySkinAuxiliaryDisplayDefinition(enabled.Id, concentration.Id, brightness.Id);

            static bool sameScopes(GameplaySkinOptionDefinition first, GameplaySkinOptionDefinition second)
                => first.Rulesets.OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(second.Rulesets.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal)
                   && first.Keymodes.OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(second.Keymodes.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal);
        }
    }
}
