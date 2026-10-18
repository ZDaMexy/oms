// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace osu.Game.Skinning.Gameplay
{
    public static partial class GameplaySkinSceneCodec
    {
        internal static IReadOnlyList<GameplaySkinRecordedAnimationDefinition> ParseRecordedAnimations(
            JToken? token, IReadOnlyList<GameplaySkinSceneResource> resources, ICollection<GameplaySkinSceneDiagnostic> diagnostics)
        {
            var result = new List<GameplaySkinRecordedAnimationDefinition>();
            if (token == null)
                return result;
            if (token is not JArray array || array.Count > GameplaySkinRecordedAnimationBudgets.MAX_PROGRAMS)
            {
                diagnostics.Add(new GameplaySkinSceneDiagnostic(GameplaySkinSceneDiagnosticCode.BudgetExceeded));
                return result;
            }

            var resourceIds = resources.Select(resource => resource.Id).ToHashSet(StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JToken item in array)
            {
                if (item is not JObject definition || definition.Properties().Any(property => property.Name is not ("id" or "path" or "resources"))
                    || definition["id"]?.Type != JTokenType.String || definition["path"]?.Type != JTokenType.String
                    || definition["resources"] is not JArray references)
                {
                    diagnostics.Add(new GameplaySkinSceneDiagnostic(GameplaySkinSceneDiagnosticCode.InvalidReference));
                    continue;
                }

                string id = definition["id"]!.Value<string>()!;
                string path = definition["path"]!.Value<string>()!;
                string[] names = [.. references.Where(reference => reference.Type == JTokenType.String).Values<string>().Select(name => name!)];
                bool stableId = id.Length is > 0 and <= 128 && id.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');
                bool safePath = tryNormalizeRelativeResourcePath(path, out string normalized) && normalized == path;
                if (!stableId || !safePath || !(path.EndsWith(".json", StringComparison.Ordinal) || path.EndsWith(".json.br", StringComparison.Ordinal))
                    || !ids.Add(id) || !paths.Add(path) || names.Length != references.Count
                    || names.Length > GameplaySkinSceneBudgets.MAX_RESOURCES || names.Distinct(StringComparer.Ordinal).Count() != names.Length
                    || names.Any(name => !resourceIds.Contains(name)))
                {
                    diagnostics.Add(new GameplaySkinSceneDiagnostic(GameplaySkinSceneDiagnosticCode.InvalidReference));
                    continue;
                }

                result.Add(new GameplaySkinRecordedAnimationDefinition(id, path, names));
            }
            return result;
        }

        internal static JArray EncodeRecordedAnimations(IEnumerable<GameplaySkinRecordedAnimationDefinition> animations)
            => new(animations.Select(animation => new JObject
            {
                ["id"] = animation.Id,
                ["path"] = animation.Path,
                ["resources"] = new JArray(animation.Resources),
            }));
    }
}
