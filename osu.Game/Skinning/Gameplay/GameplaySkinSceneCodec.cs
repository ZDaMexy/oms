// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// The only codec for the V1 gameplay skin manifest and declarative scene document.
    /// It consumes already-captured bytes and performs no package, filesystem, network, reflection or runtime work.
    /// </summary>
    public static class GameplaySkinSceneCodec
    {
        private static readonly UTF8Encoding strict_utf8 = new UTF8Encoding(false, true);

        private static readonly SearchValues<char> hexadecimal_characters = SearchValues.Create("0123456789abcdefABCDEF");

        private static readonly HashSet<string> anchor_values = new HashSet<string>(StringComparer.Ordinal)
        {
            "top-left", "top-centre", "top-right", "centre-left", "centre", "centre-right", "bottom-left", "bottom-centre", "bottom-right",
        };

        private static readonly HashSet<string> colour_properties = new HashSet<string>(StringComparer.Ordinal)
        {
            "colour",
        };

        private static readonly HashSet<string> number_properties = new HashSet<string>(StringComparer.Ordinal)
        {
            "opacity", "x", "y", "width", "height", "scale-x", "scale-y", "rotation", "z", "font-size", "corner-radius",
        };

        private static readonly HashSet<string> boolean_properties = new HashSet<string>(StringComparer.Ordinal)
        {
            "visible",
        };

        private static readonly HashSet<string> event_ids = new HashSet<string>(StringComparer.Ordinal)
        {
            "gameplay.attach", "gameplay.loaded", "gameplay.start", "gameplay.pause", "gameplay.complete", "gameplay.fail",
            "input.key.down", "input.key.up", "object.spawn", "object.state",
            "judgement.hit", "timing.stop", "bga.state",
        };

        private static readonly HashSet<string> binding_source_ids = new HashSet<string>(StringComparer.Ordinal)
        {
            "layout.stage", "layout.group", "layout.lane", "input.pressed", "object.state", "judgement.result", "judgement.offset",
            "score.value", "combo.value", "gauge.value", "timing.beat", "timing.measure", "timing.bpm", "bga.content-state",
        };

        private static readonly IReadOnlyDictionary<string, HashSet<string>> variant_source_keys =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["object.state"] = new HashSet<string>(
                    new[] { "scheduled", "visible", "holding", "hit", "missed", "completed", "despawned" },
                    StringComparer.Ordinal),
                ["judgement.result"] = new HashSet<string>(
                    new[] { "miss", "meh", "ok", "good", "great", "perfect" },
                    StringComparer.Ordinal),
                ["bga.content-state"] = new HashSet<string>(
                    new[] { "empty", "ready", "playing", "paused", "failed" },
                    StringComparer.Ordinal),
            };

        public static GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> DecodeManifest(string? content)
        {
            if (content == null)
                return absent<GameplaySkinSceneManifest>();

            try
            {
                return decodeManifest(strict_utf8.GetBytes(content));
            }
            catch (EncoderFallbackException)
            {
                return invalid<GameplaySkinSceneManifest>(GameplaySkinSceneDiagnosticCode.InvalidUtf8);
            }
        }

        public static GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> DecodeManifest(ReadOnlyMemory<byte> content)
            => decodeManifest(content);

        public static GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> DecodeScene(string? content, GameplaySkinSceneManifest? manifest)
        {
            if (content == null)
                return absent<GameplaySkinSceneDocument>();

            try
            {
                return decodeScene(strict_utf8.GetBytes(content), manifest);
            }
            catch (EncoderFallbackException)
            {
                return invalid<GameplaySkinSceneDocument>(GameplaySkinSceneDiagnosticCode.InvalidUtf8);
            }
        }

        public static GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> DecodeScene(ReadOnlyMemory<byte> content, GameplaySkinSceneManifest? manifest)
            => decodeScene(content, manifest);

        public static string EncodeManifest(GameplaySkinSceneManifest manifest)
        {
            ArgumentNullException.ThrowIfNull(manifest);

            var root = new JObject
            {
                ["contract"] = manifest.Contract,
                ["scene"] = manifest.SceneFile,
                ["sceneContract"] = manifest.SceneContract,
                ["eventContract"] = manifest.EventContract,
                ["resources"] = new JArray(manifest.Resources.Select(resource => new JObject
                {
                    ["id"] = resource.Id,
                    ["type"] = encodeResourceType(resource.Type),
                    ["path"] = resource.Path,
                })),
            };

            return root.ToString(Formatting.None);
        }

        public static byte[] EncodeManifestUtf8(GameplaySkinSceneManifest manifest) => strict_utf8.GetBytes(EncodeManifest(manifest));

        public static string EncodeScene(GameplaySkinSceneDocument scene)
        {
            ArgumentNullException.ThrowIfNull(scene);

            var root = new JObject
            {
                ["contract"] = scene.Contract,
                ["root"] = encodeNode(scene.Root),
                ["tracks"] = new JArray(scene.Tracks.Select(encodeTrack)),
                ["stateMachines"] = new JArray(scene.StateMachines.Select(encodeStateMachine)),
                ["bindings"] = new JArray(scene.Bindings.Select(binding => new JObject
                {
                    ["id"] = binding.Id,
                    ["target"] = binding.TargetNodeId,
                    ["property"] = binding.PropertyId,
                    ["source"] = binding.SourceId,
                })),
                ["variants"] = new JArray(scene.Variants.Select(variant => new JObject
                {
                    ["id"] = variant.Id,
                    ["target"] = variant.TargetNodeId,
                    ["property"] = "resource",
                    ["source"] = variant.SourceId,
                    ["default"] = variant.DefaultResourceId,
                    ["cases"] = new JArray(variant.Cases.Select(item => new JObject
                    {
                        ["id"] = item.Id,
                        ["key"] = item.Key,
                        ["resource"] = item.ResourceId,
                    })),
                })),
                ["templates"] = new JArray(scene.Templates.Select(template => new JObject
                {
                    ["id"] = template.Id,
                    ["root"] = encodeNode(template.Root),
                })),
                ["instances"] = new JArray(scene.Instances.Select(instance => new JObject
                {
                    ["id"] = instance.Id,
                    ["template"] = instance.TemplateId,
                    ["target"] = encodeTarget(instance.Target),
                })),
            };

            return root.ToString(Formatting.None);
        }

        public static byte[] EncodeSceneUtf8(GameplaySkinSceneDocument scene) => strict_utf8.GetBytes(EncodeScene(scene));

        private static GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> decodeManifest(ReadOnlyMemory<byte> content)
        {
            if (content.Length > GameplaySkinSceneBudgets.MAX_MANIFEST_BYTES)
                return invalid<GameplaySkinSceneManifest>(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

            if (!tryDecodeUtf8(content.Span, out _))
                return invalid<GameplaySkinSceneManifest>(GameplaySkinSceneDiagnosticCode.InvalidUtf8);

            var context = new DecodeContext();
            JObject? root = parseRoot(content.Span, context);

            if (root == null)
                return context.Invalid<GameplaySkinSceneManifest>();

            validateFields(root, context, new[] { "contract", "scene", "sceneContract", "eventContract", "resources" });
            validateExactContract(root, "contract", GameplaySkinSceneContracts.MANIFEST_CONTRACT_ID, context);
            validateExactContract(root, "scene", GameplaySkinSceneContracts.SCENE_FILE_NAME, context);
            validateExactContract(root, "sceneContract", GameplaySkinSceneContracts.SCENE_CONTRACT_ID, context);
            validateExactContract(root, "eventContract", GameplaySkinSceneContracts.EVENT_CONTRACT_ID, context);

            var resources = new List<GameplaySkinSceneResource>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (getArray(root, "resources", context) is JArray resourceArray)
            {
                if (resourceArray.Count > GameplaySkinSceneBudgets.MAX_RESOURCES)
                    context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

                foreach (JToken token in resourceArray.Take(GameplaySkinSceneBudgets.MAX_RESOURCES))
                {
                    if (token is not JObject resourceObject)
                    {
                        context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                        continue;
                    }

                    validateFields(resourceObject, context, new[] { "id", "type", "path" });
                    string? id = getStableId(resourceObject, "id", context);
                    GameplaySkinSceneResourceType? type = parseResourceType(resourceObject["type"], context);
                    string? rawPath = getString(resourceObject, "path", context);
                    string? path = null;

                    if (id != null && !ids.Add(id))
                        context.Add(GameplaySkinSceneDiagnosticCode.DuplicateStableId);

                    if (rawPath != null)
                    {
                        if (!tryNormalizeRelativeResourcePath(rawPath, out path))
                        {
                            context.Add(GameplaySkinSceneDiagnosticCode.UnsafeResourcePath);
                        }
                        else if (!paths.Add(path))
                        {
                            context.Add(GameplaySkinSceneDiagnosticCode.DuplicateNormalizedPath);
                        }
                    }

                    if (id != null && type.HasValue && path != null)
                        resources.Add(new GameplaySkinSceneResource(id, type.Value, path));
                }
            }

            if (context.HasDiagnostics)
                return context.Invalid<GameplaySkinSceneManifest>();

            return valid(new GameplaySkinSceneManifest(resources));
        }

        private static GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decodeScene(ReadOnlyMemory<byte> content, GameplaySkinSceneManifest? manifest)
        {
            if (content.Length > GameplaySkinSceneBudgets.MAX_SCENE_BYTES)
                return invalid<GameplaySkinSceneDocument>(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

            if (!tryDecodeUtf8(content.Span, out _))
                return invalid<GameplaySkinSceneDocument>(GameplaySkinSceneDiagnosticCode.InvalidUtf8);

            var context = new DecodeContext(manifest);
            JObject? rootObject = parseRoot(content.Span, context);

            if (rootObject == null)
                return context.Invalid<GameplaySkinSceneDocument>();

            validateFields(
                rootObject,
                context,
                new[] { "contract", "root", "tracks", "stateMachines", "bindings", "templates", "instances" },
                new[] { "variants" });
            validateExactContract(rootObject, "contract", GameplaySkinSceneContracts.SCENE_CONTRACT_ID, context);

            GameplaySkinSceneNode? root = rootObject["root"] is JObject nodeObject
                ? parseNode(nodeObject, context, 1)
                : missingOrInvalidObject<GameplaySkinSceneNode>(rootObject, "root", context);

            // Template nodes join the same stable node namespace before any property program is resolved. Tracks,
            // state assignments, bindings and variants may therefore target a template source once and deterministically
            // fan out to every prepared instance without a second runtime lookup authority.
            List<GameplaySkinSceneTemplate> templates = parseTemplates(getArray(rootObject, "templates", context), context);
            List<GameplaySkinSceneInstance> instances = parseInstances(getArray(rootObject, "instances", context), context);
            List<GameplaySkinSceneTrack> tracks = parseTracks(getArray(rootObject, "tracks", context), context);
            List<GameplaySkinSceneStateMachine> stateMachines = parseStateMachines(getArray(rootObject, "stateMachines", context), context);
            List<GameplaySkinSceneBinding> bindings = parseBindings(getArray(rootObject, "bindings", context), context);
            List<GameplaySkinSceneVariant> variants = parseVariants(rootObject["variants"], context);

            long expandedNodeCount = context.MainNodeCount;
            var targetFanout = new Dictionary<string, int>(StringComparer.Ordinal);

            if (root != null)
                addNodeFanout(root, targetFanout);

            foreach (GameplaySkinSceneInstance instance in instances)
            {
                GameplaySkinSceneTemplate? template = templates.SingleOrDefault(candidate => candidate.Id == instance.TemplateId);

                if (template == null)
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidReference);
                    continue;
                }

                expandedNodeCount += countNodes(template.Root);
                addNodeFanout(template.Root, targetFanout);

                if (expandedNodeCount > GameplaySkinSceneBudgets.MAX_EXPANDED_TEMPLATE_NODES)
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                    break;
                }
            }

            long perFramePropertyApplications = tracks.Sum(track => (long)targetFanout.GetValueOrDefault(track.TargetNodeId))
                                                + bindings.Sum(binding => (long)targetFanout.GetValueOrDefault(binding.TargetNodeId))
                                                + variants.Sum(variant => (long)targetFanout.GetValueOrDefault(variant.TargetNodeId));
            long perEventStateApplications = stateMachines
                                             .SelectMany(machine => machine.States)
                                             .SelectMany(state => state.Assignments)
                                             .Sum(assignment => (long)targetFanout.GetValueOrDefault(assignment.TargetNodeId));

            if (perFramePropertyApplications > GameplaySkinSceneBudgets.MAX_PROPERTY_APPLICATIONS_PER_FRAME
                || perEventStateApplications > GameplaySkinSceneBudgets.MAX_STATE_PROPERTY_APPLICATIONS_PER_EVENT)
            {
                context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            }

            if (context.HasDiagnostics || root == null)
                return context.Invalid<GameplaySkinSceneDocument>();

            return valid(new GameplaySkinSceneDocument(root, tracks, stateMachines, bindings, variants, templates, instances));
        }

        private static void addNodeFanout(GameplaySkinSceneNode node, IDictionary<string, int> fanout)
        {
            fanout.TryGetValue(node.Id, out int current);
            fanout[node.Id] = checked(current + 1);

            foreach (GameplaySkinSceneNode child in node.Children)
                addNodeFanout(child, fanout);
        }

        private static GameplaySkinSceneNode? parseNode(JObject node, DecodeContext context, int depth, bool isTemplate = false)
        {
            context.TotalNodeCount++;

            if (!isTemplate)
                context.MainNodeCount++;

            if (context.TotalNodeCount > GameplaySkinSceneBudgets.MAX_NODES || depth > GameplaySkinSceneBudgets.MAX_NODE_DEPTH)
                context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

            validateFields(node, context, new[] { "id", "type", "target", "blend", "properties", "effects", "children" }, new[] { "slot", "resource" });
            string? id = getStableId(node, "id", context);

            if (id != null && !context.AllStableIds.Add(id))
                context.Add(GameplaySkinSceneDiagnosticCode.DuplicateStableId);

            GameplaySkinSceneNodeType? parsedType = parseNodeType(node["type"], context);
            GameplaySkinSceneNodeType type = parsedType ?? GameplaySkinSceneNodeType.Container;
            GameplaySkinSceneTarget? target = node["target"] is JObject targetObject
                ? parseTarget(targetObject, context)
                : missingOrInvalidObject<GameplaySkinSceneTarget>(node, "target", context);
            GameplaySkinSceneBlendMode? blend = parseBlend(node["blend"], context);
            string? slotId = null;
            string? resourceId = null;

            if (node.TryGetValue("slot", out JToken? slotToken))
            {
                slotId = getStableId(slotToken, context);

                if (slotId != null && !GameplaySkinSlotCatalog.TryGet(slotId, out _))
                    context.Add(GameplaySkinSceneDiagnosticCode.UnknownSlot);
            }

            if (node.TryGetValue("resource", out JToken? resourceToken))
            {
                resourceId = getStableId(resourceToken, context);

                if (resourceId != null && !isKnownResource(resourceId, context.Manifest))
                    context.Add(GameplaySkinSceneDiagnosticCode.UnknownResource);

                if (parsedType.HasValue && parsedType.Value != GameplaySkinSceneNodeType.Sprite)
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidResource);
            }

            IReadOnlyDictionary<string, GameplaySkinScenePropertyValue> properties = node["properties"] is JObject propertiesObject
                ? parseNodeProperties(propertiesObject, type, context)
                : missingOrInvalidProperties(node, "properties", context);
            List<GameplaySkinSceneEffect> effects = parseEffects(node["effects"], context);
            var children = new List<GameplaySkinSceneNode>();

            if (getArray(node, "children", context) is JArray childArray)
            {
                if (childArray.Count > GameplaySkinSceneBudgets.MAX_CHILDREN_PER_NODE)
                    context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

                foreach (JToken childToken in childArray.Take(GameplaySkinSceneBudgets.MAX_CHILDREN_PER_NODE))
                {
                    if (childToken is not JObject childObject)
                    {
                        context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                        continue;
                    }

                    GameplaySkinSceneNode? child = parseNode(childObject, context, depth + 1, isTemplate);

                    if (child != null)
                        children.Add(child);
                }
            }

            if (id != null && parsedType.HasValue)
                context.NodeTypes.TryAdd(id, parsedType.Value);

            if (id == null || !parsedType.HasValue || target == null || !blend.HasValue)
                return null;

            return new GameplaySkinSceneNode(id, type, target, slotId, resourceId, blend.Value, properties, effects, children);
        }

        private static IReadOnlyDictionary<string, GameplaySkinScenePropertyValue> parseNodeProperties(
            JObject properties,
            GameplaySkinSceneNodeType nodeType,
            DecodeContext context)
        {
            var result = new Dictionary<string, GameplaySkinScenePropertyValue>(StringComparer.Ordinal);

            if (properties.Count > GameplaySkinSceneBudgets.MAX_PROPERTIES_PER_OBJECT)
                context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

            foreach (JProperty property in properties.Properties().Take(GameplaySkinSceneBudgets.MAX_PROPERTIES_PER_OBJECT))
            {
                if (!isAllowedNodeProperty(nodeType, property.Name))
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.UnknownProperty);
                    continue;
                }

                GameplaySkinScenePropertyValue? value = parsePropertyValue(property.Name, property.Value, context);

                if (value != null)
                    result.Add(property.Name, value);
            }

            return result;
        }

        private static List<GameplaySkinSceneEffect> parseEffects(JToken? token, DecodeContext context)
        {
            var effects = new List<GameplaySkinSceneEffect>();

            if (token is not JArray array)
            {
                context.Add(token == null ? GameplaySkinSceneDiagnosticCode.MissingField : GameplaySkinSceneDiagnosticCode.InvalidValueType);
                return effects;
            }

            if (array.Count > GameplaySkinSceneBudgets.MAX_EFFECTS_PER_NODE)
                context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

            foreach (JToken effectToken in array.Take(GameplaySkinSceneBudgets.MAX_EFFECTS_PER_NODE))
            {
                context.TotalEffectCount++;

                if (context.TotalEffectCount > GameplaySkinSceneBudgets.MAX_EFFECTS)
                    context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

                if (effectToken is not JObject effectObject)
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                    continue;
                }

                validateFields(effectObject, context, new[] { "id", "type", "properties" });
                string? id = getStableId(effectObject, "id", context);

                if (id != null && !context.AllStableIds.Add(id))
                    context.Add(GameplaySkinSceneDiagnosticCode.DuplicateStableId);

                string? type = getString(effectObject, "type", context);
                bool knownType = type is "blur" or "glow" or "outline" or "shadow";

                if (type != null && !knownType)
                    context.Add(GameplaySkinSceneDiagnosticCode.UnknownEffect);

                var properties = new Dictionary<string, GameplaySkinScenePropertyValue>(StringComparer.Ordinal);

                if (effectObject["properties"] is JObject propertyObject)
                {
                    if (propertyObject.Count > GameplaySkinSceneBudgets.MAX_PROPERTIES_PER_OBJECT)
                        context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

                    foreach (JProperty property in propertyObject.Properties().Take(GameplaySkinSceneBudgets.MAX_PROPERTIES_PER_OBJECT))
                    {
                        if (type == null || !isAllowedEffectProperty(type, property.Name))
                        {
                            context.Add(GameplaySkinSceneDiagnosticCode.UnknownProperty);
                            continue;
                        }

                        GameplaySkinScenePropertyValue? value = parseEffectPropertyValue(property.Name, property.Value, context);

                        if (value != null)
                            properties.Add(property.Name, value);
                    }
                }
                else
                {
                    context.Add(effectObject["properties"] == null ? GameplaySkinSceneDiagnosticCode.MissingField : GameplaySkinSceneDiagnosticCode.InvalidValueType);
                }

                if (id != null && type != null && knownType)
                    effects.Add(new GameplaySkinSceneEffect(id, type, properties));
            }

            return effects;
        }

        private static List<GameplaySkinSceneTrack> parseTracks(JArray? array, DecodeContext context)
        {
            var tracks = new List<GameplaySkinSceneTrack>();

            if (array == null)
                return tracks;

            if (array.Count > GameplaySkinSceneBudgets.MAX_TRACKS)
                context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

            foreach (JToken token in array.Take(GameplaySkinSceneBudgets.MAX_TRACKS))
            {
                if (token is not JObject trackObject)
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                    continue;
                }

                validateFields(trackObject, context, new[] { "id", "type", "target", "property", "easing", "loop", "keyframes" });
                string? id = getStableId(trackObject, "id", context);

                if (id != null && !context.AllStableIds.Add(id))
                    context.Add(GameplaySkinSceneDiagnosticCode.DuplicateStableId);

                GameplaySkinSceneTrackType? type = parseTrackType(trackObject["type"], context);
                string? target = getStableId(trackObject, "target", context);
                string? property = getString(trackObject, "property", context);
                GameplaySkinSceneEasing? easing = parseEasing(trackObject["easing"], context);
                bool? loop = getBoolean(trackObject, "loop", context);

                if (target != null && !context.NodeTypes.ContainsKey(target))
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidReference);

                if (property != null && target != null && context.NodeTypes.TryGetValue(target, out GameplaySkinSceneNodeType nodeType)
                    && property != "resource" && !isAllowedNodeProperty(nodeType, property))
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.UnknownProperty);
                }

                if (type == GameplaySkinSceneTrackType.Frame
                    && target != null
                    && context.NodeTypes.TryGetValue(target, out GameplaySkinSceneNodeType frameTargetType)
                    && frameTargetType != GameplaySkinSceneNodeType.Sprite)
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidAnimation);
                }

                var keyframes = new List<GameplaySkinSceneKeyframe>();

                if (getArray(trackObject, "keyframes", context) is JArray keyframeArray)
                {
                    if (keyframeArray.Count == 0)
                        context.Add(GameplaySkinSceneDiagnosticCode.InvalidAnimation);

                    if (keyframeArray.Count > GameplaySkinSceneBudgets.MAX_KEYFRAMES_PER_TRACK)
                        context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

                    context.TotalKeyframeCount += keyframeArray.Count;

                    if (context.TotalKeyframeCount > GameplaySkinSceneBudgets.MAX_KEYFRAMES)
                        context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

                    double previousTime = -1;

                    foreach (JToken keyframeToken in keyframeArray.Take(GameplaySkinSceneBudgets.MAX_KEYFRAMES_PER_TRACK))
                    {
                        if (keyframeToken is not JObject keyframeObject)
                        {
                            context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                            continue;
                        }

                        validateFields(keyframeObject, context, new[] { "id", "time", "value" });
                        string? keyframeId = getStableId(keyframeObject, "id", context);

                        if (keyframeId != null && !context.AllStableIds.Add(keyframeId))
                            context.Add(GameplaySkinSceneDiagnosticCode.DuplicateStableId);

                        double? time = getNumber(keyframeObject, "time", context);
                        GameplaySkinScenePropertyValue? value = null;

                        if (keyframeObject.TryGetValue("value", out JToken? valueToken))
                        {
                            value = type == GameplaySkinSceneTrackType.Tween && property != null
                                ? parsePropertyValue(property, valueToken, context)
                                : parsePrimitive(valueToken, context);
                        }
                        else
                            missingPrimitive(context);

                        if (time.HasValue && (time.Value < 0
                                             || time.Value > GameplaySkinSceneBudgets.MAX_TRACK_TIME
                                             || time.Value <= previousTime))
                            context.Add(GameplaySkinSceneDiagnosticCode.InvalidAnimation);

                        if (time.HasValue)
                            previousTime = time.Value;

                        if (type == GameplaySkinSceneTrackType.Frame)
                        {
                            if (property != "resource" || value?.Kind != GameplaySkinScenePropertyValueKind.String)
                                context.Add(GameplaySkinSceneDiagnosticCode.InvalidAnimation);
                            else if (!isKnownResource(value.StringValue!, context.Manifest))
                                context.Add(GameplaySkinSceneDiagnosticCode.UnknownResource);
                        }
                        else if (type == GameplaySkinSceneTrackType.Tween)
                        {
                            if (property == "resource"
                                || property != null && !number_properties.Contains(property)
                                || value?.Kind != GameplaySkinScenePropertyValueKind.Number)
                                context.Add(GameplaySkinSceneDiagnosticCode.InvalidAnimation);
                        }

                        if (keyframeId != null && time.HasValue && value != null)
                            keyframes.Add(new GameplaySkinSceneKeyframe(keyframeId, time.Value, value));
                    }
                }

                if (id != null && type.HasValue && target != null && property != null && easing.HasValue && loop.HasValue)
                    tracks.Add(new GameplaySkinSceneTrack(id, type.Value, target, property, easing.Value, loop.Value, keyframes));
            }

            return tracks;
        }

        private static List<GameplaySkinSceneStateMachine> parseStateMachines(JArray? array, DecodeContext context)
        {
            var machines = new List<GameplaySkinSceneStateMachine>();

            if (array == null)
                return machines;

            if (array.Count > GameplaySkinSceneBudgets.MAX_STATE_MACHINES)
                context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

            foreach (JToken token in array.Take(GameplaySkinSceneBudgets.MAX_STATE_MACHINES))
            {
                if (token is not JObject machineObject)
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                    continue;
                }

                validateFields(machineObject, context, new[] { "id", "initial", "states", "transitions" });
                string? id = getStableId(machineObject, "id", context);

                if (id != null && !context.AllStableIds.Add(id))
                    context.Add(GameplaySkinSceneDiagnosticCode.DuplicateStableId);

                string? initial = getStableId(machineObject, "initial", context);
                var states = new List<GameplaySkinSceneState>();
                var machineStateIds = new HashSet<string>(StringComparer.Ordinal);

                if (getArray(machineObject, "states", context) is JArray stateArray)
                {
                    context.TotalStateCount += stateArray.Count;

                    if (context.TotalStateCount > GameplaySkinSceneBudgets.MAX_STATES || stateArray.Count == 0)
                        context.Add(stateArray.Count == 0 ? GameplaySkinSceneDiagnosticCode.InvalidStateMachine : GameplaySkinSceneDiagnosticCode.BudgetExceeded);

                    foreach (JToken stateToken in stateArray.Take(GameplaySkinSceneBudgets.MAX_STATES))
                    {
                        if (stateToken is not JObject stateObject)
                        {
                            context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                            continue;
                        }

                        validateFields(stateObject, context, new[] { "id", "set" });
                        string? stateId = getStableId(stateObject, "id", context);

                        if (stateId != null && (!machineStateIds.Add(stateId) || !context.AllStableIds.Add(stateId)))
                            context.Add(GameplaySkinSceneDiagnosticCode.DuplicateStableId);

                        var assignments = new List<GameplaySkinSceneStateAssignment>();

                        if (getArray(stateObject, "set", context) is JArray assignmentArray)
                        {
                            context.TotalStateAssignmentCount += assignmentArray.Count;

                            if (context.TotalStateAssignmentCount > GameplaySkinSceneBudgets.MAX_STATE_ASSIGNMENTS)
                                context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

                            foreach (JToken assignmentToken in assignmentArray.Take(GameplaySkinSceneBudgets.MAX_STATE_ASSIGNMENTS))
                            {
                                if (assignmentToken is not JObject assignmentObject)
                                {
                                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                                    continue;
                                }

                                validateFields(assignmentObject, context, new[] { "id", "target", "property", "value" });
                                string? assignmentId = getStableId(assignmentObject, "id", context);

                                if (assignmentId != null && !context.AllStableIds.Add(assignmentId))
                                    context.Add(GameplaySkinSceneDiagnosticCode.DuplicateStableId);

                                string? target = getStableId(assignmentObject, "target", context);
                                string? property = getString(assignmentObject, "property", context);
                                GameplaySkinScenePropertyValue? value = null;

                                if (target != null)
                                {
                                    if (!context.NodeTypes.TryGetValue(target, out GameplaySkinSceneNodeType nodeType))
                                        context.Add(GameplaySkinSceneDiagnosticCode.InvalidReference);
                                    else if (property != null && property != "resource" && !isAllowedNodeProperty(nodeType, property)
                                             || property == "resource" && nodeType != GameplaySkinSceneNodeType.Sprite)
                                        context.Add(GameplaySkinSceneDiagnosticCode.UnknownProperty);
                                }

                                if (assignmentObject.TryGetValue("value", out JToken? assignmentValue))
                                {
                                    if (property == "resource")
                                    {
                                        value = parsePrimitive(assignmentValue, context);

                                        if (value?.Kind != GameplaySkinScenePropertyValueKind.String)
                                            context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                                        else if (!isKnownResource(value.StringValue!, context.Manifest))
                                            context.Add(GameplaySkinSceneDiagnosticCode.UnknownResource);
                                    }
                                    else if (property != null)
                                        value = parsePropertyValue(property, assignmentValue, context);
                                    else
                                        value = parsePrimitive(assignmentValue, context);
                                }
                                else
                                    context.Add(GameplaySkinSceneDiagnosticCode.MissingField);

                                if (assignmentId != null && target != null && property != null && value != null)
                                    assignments.Add(new GameplaySkinSceneStateAssignment(assignmentId, target, property, value));
                            }
                        }

                        if (stateId != null)
                            states.Add(new GameplaySkinSceneState(stateId, assignments));
                    }
                }

                if (initial != null && !machineStateIds.Contains(initial))
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidStateMachine);

                var transitions = new List<GameplaySkinSceneTransition>();

                if (getArray(machineObject, "transitions", context) is JArray transitionArray)
                {
                    context.TotalTransitionCount += transitionArray.Count;

                    if (context.TotalTransitionCount > GameplaySkinSceneBudgets.MAX_TRANSITIONS)
                        context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

                    foreach (JToken transitionToken in transitionArray.Take(GameplaySkinSceneBudgets.MAX_TRANSITIONS))
                    {
                        if (transitionToken is not JObject transitionObject)
                        {
                            context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                            continue;
                        }

                        validateFields(transitionObject, context, new[] { "id", "from", "to", "event" });
                        string? transitionId = getStableId(transitionObject, "id", context);

                        if (transitionId != null && !context.AllStableIds.Add(transitionId))
                            context.Add(GameplaySkinSceneDiagnosticCode.DuplicateStableId);

                        string? from = getStableId(transitionObject, "from", context);
                        string? to = getStableId(transitionObject, "to", context);
                        string? eventId = getString(transitionObject, "event", context);

                        if (from != null && !machineStateIds.Contains(from) || to != null && !machineStateIds.Contains(to))
                            context.Add(GameplaySkinSceneDiagnosticCode.InvalidStateMachine);

                        if (eventId != null && !event_ids.Contains(eventId))
                            context.Add(GameplaySkinSceneDiagnosticCode.UnknownEvent);

                        if (transitionId != null && from != null && to != null && eventId != null)
                            transitions.Add(new GameplaySkinSceneTransition(transitionId, from, to, eventId));
                    }
                }

                if (id != null && initial != null)
                    machines.Add(new GameplaySkinSceneStateMachine(id, initial, states, transitions));
            }

            return machines;
        }

        private static List<GameplaySkinSceneBinding> parseBindings(JArray? array, DecodeContext context)
        {
            var bindings = new List<GameplaySkinSceneBinding>();

            if (array == null)
                return bindings;

            if (array.Count > GameplaySkinSceneBudgets.MAX_BINDINGS)
                context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

            foreach (JToken token in array.Take(GameplaySkinSceneBudgets.MAX_BINDINGS))
            {
                if (token is not JObject bindingObject)
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                    continue;
                }

                validateFields(bindingObject, context, new[] { "id", "target", "property", "source" });
                string? id = getStableId(bindingObject, "id", context);

                if (id != null && !context.AllStableIds.Add(id))
                    context.Add(GameplaySkinSceneDiagnosticCode.DuplicateStableId);

                string? target = getStableId(bindingObject, "target", context);
                string? property = getString(bindingObject, "property", context);
                string? source = getString(bindingObject, "source", context);

                if (target != null)
                {
                    if (!context.NodeTypes.TryGetValue(target, out GameplaySkinSceneNodeType nodeType))
                        context.Add(GameplaySkinSceneDiagnosticCode.InvalidReference);
                    else if (property != null && !isAllowedNodeProperty(nodeType, property))
                        context.Add(GameplaySkinSceneDiagnosticCode.UnknownProperty);
                }

                if (source != null && !binding_source_ids.Contains(source))
                    context.Add(GameplaySkinSceneDiagnosticCode.UnknownBindingSource);

                if (property != null && source != null && binding_source_ids.Contains(source)
                    && !bindingSourceCanDriveProperty(source, property))
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                }

                if (id != null && target != null && property != null && source != null)
                    bindings.Add(new GameplaySkinSceneBinding(id, target, property, source));
            }

            return bindings;
        }

        private static List<GameplaySkinSceneVariant> parseVariants(JToken? token, DecodeContext context)
        {
            var variants = new List<GameplaySkinSceneVariant>();

            if (token == null)
                return variants;

            if (token is not JArray array)
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                return variants;
            }

            if (array.Count > GameplaySkinSceneBudgets.MAX_VARIANTS)
                context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

            foreach (JToken variantToken in array.Take(GameplaySkinSceneBudgets.MAX_VARIANTS))
            {
                if (variantToken is not JObject variantObject)
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                    continue;
                }

                validateFields(variantObject, context, new[] { "id", "target", "property", "source", "default", "cases" });
                string? id = getStableId(variantObject, "id", context);

                if (id != null && !context.AllStableIds.Add(id))
                    context.Add(GameplaySkinSceneDiagnosticCode.DuplicateStableId);

                string? target = getStableId(variantObject, "target", context);
                string? property = getString(variantObject, "property", context);
                string? source = getString(variantObject, "source", context);
                string? defaultResource = getStableId(variantObject, "default", context);

                if (target != null
                    && (!context.NodeTypes.TryGetValue(target, out GameplaySkinSceneNodeType nodeType)
                        || nodeType != GameplaySkinSceneNodeType.Sprite))
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidReference);
                }

                if (property != "resource")
                    context.Add(GameplaySkinSceneDiagnosticCode.UnknownProperty);

                HashSet<string>? allowedKeys = null;

                if (source == null || !variant_source_keys.TryGetValue(source, out allowedKeys))
                    context.Add(GameplaySkinSceneDiagnosticCode.UnknownBindingSource);

                if (defaultResource != null && !isKnownResource(defaultResource, context.Manifest))
                    context.Add(GameplaySkinSceneDiagnosticCode.UnknownResource);

                var cases = new List<GameplaySkinSceneVariantCase>();
                var keys = new HashSet<string>(StringComparer.Ordinal);

                if (getArray(variantObject, "cases", context) is JArray caseArray)
                {
                    if (caseArray.Count == 0)
                        context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);

                    if (caseArray.Count > GameplaySkinSceneBudgets.MAX_VARIANT_CASES_PER_VARIANT)
                        context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

                    context.TotalVariantCaseCount += caseArray.Count;

                    if (context.TotalVariantCaseCount > GameplaySkinSceneBudgets.MAX_VARIANT_CASES)
                        context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

                    foreach (JToken caseToken in caseArray.Take(GameplaySkinSceneBudgets.MAX_VARIANT_CASES_PER_VARIANT))
                    {
                        if (caseToken is not JObject caseObject)
                        {
                            context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                            continue;
                        }

                        validateFields(caseObject, context, new[] { "id", "key", "resource" });
                        string? caseId = getStableId(caseObject, "id", context);

                        if (caseId != null && !context.AllStableIds.Add(caseId))
                            context.Add(GameplaySkinSceneDiagnosticCode.DuplicateStableId);

                        string? key = getString(caseObject, "key", context);
                        string? resource = getStableId(caseObject, "resource", context);

                        if (key != null && (allowedKeys == null || !allowedKeys.Contains(key)))
                            context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);

                        if (key != null && !keys.Add(key))
                            context.Add(GameplaySkinSceneDiagnosticCode.DuplicateField);

                        if (resource != null && !isKnownResource(resource, context.Manifest))
                            context.Add(GameplaySkinSceneDiagnosticCode.UnknownResource);

                        if (caseId != null && key != null && resource != null)
                            cases.Add(new GameplaySkinSceneVariantCase(caseId, key, resource));
                    }
                }

                if (id != null && target != null && property == "resource" && source != null
                    && variant_source_keys.ContainsKey(source) && defaultResource != null)
                {
                    variants.Add(new GameplaySkinSceneVariant(id, target, source, defaultResource, cases));
                }
            }

            return variants;
        }

        private static List<GameplaySkinSceneTemplate> parseTemplates(JArray? array, DecodeContext context)
        {
            var templates = new List<GameplaySkinSceneTemplate>();

            if (array == null)
                return templates;

            if (array.Count > GameplaySkinSceneBudgets.MAX_TEMPLATES)
                context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

            foreach (JToken token in array.Take(GameplaySkinSceneBudgets.MAX_TEMPLATES))
            {
                if (token is not JObject templateObject)
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                    continue;
                }

                validateFields(templateObject, context, new[] { "id", "root" });
                string? id = getStableId(templateObject, "id", context);

                if (id != null && !context.AllStableIds.Add(id))
                    context.Add(GameplaySkinSceneDiagnosticCode.DuplicateStableId);

                GameplaySkinSceneNode? root = templateObject["root"] is JObject rootObject
                    ? parseNode(rootObject, context, 1, true)
                    : missingOrInvalidObject<GameplaySkinSceneNode>(templateObject, "root", context);

                if (id != null && root != null)
                    templates.Add(new GameplaySkinSceneTemplate(id, root));
            }

            return templates;
        }

        private static List<GameplaySkinSceneInstance> parseInstances(JArray? array, DecodeContext context)
        {
            var instances = new List<GameplaySkinSceneInstance>();

            if (array == null)
                return instances;

            if (array.Count > GameplaySkinSceneBudgets.MAX_INSTANCES)
                context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

            foreach (JToken token in array.Take(GameplaySkinSceneBudgets.MAX_INSTANCES))
            {
                if (token is not JObject instanceObject)
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                    continue;
                }

                validateFields(instanceObject, context, new[] { "id", "template", "target" });
                string? id = getStableId(instanceObject, "id", context);

                if (id != null && !context.AllStableIds.Add(id))
                    context.Add(GameplaySkinSceneDiagnosticCode.DuplicateStableId);

                string? template = getStableId(instanceObject, "template", context);
                GameplaySkinSceneTarget? target = instanceObject["target"] is JObject targetObject
                    ? parseTarget(targetObject, context)
                    : missingOrInvalidObject<GameplaySkinSceneTarget>(instanceObject, "target", context);

                if (id != null && template != null && target != null)
                    instances.Add(new GameplaySkinSceneInstance(id, template, target));
            }

            return instances;
        }

        private static GameplaySkinSceneTarget? parseTarget(JObject target, DecodeContext context)
        {
            string? kindToken = getString(target, "kind", context);
            GameplaySkinSceneTargetKind? kind = kindToken switch
            {
                "global" => GameplaySkinSceneTargetKind.Global,
                "stage" => GameplaySkinSceneTargetKind.Stage,
                "group" => GameplaySkinSceneTargetKind.Group,
                "lane" => GameplaySkinSceneTargetKind.Lane,
                "hud" => GameplaySkinSceneTargetKind.Hud,
                "bga" => GameplaySkinSceneTargetKind.Bga,
                null => null,
                _ => (GameplaySkinSceneTargetKind)(-1),
            };

            if (kind == (GameplaySkinSceneTargetKind)(-1))
            {
                context.Add(GameplaySkinSceneDiagnosticCode.UnknownTarget);
                kind = null;
            }

            if (!kind.HasValue)
            {
                validateFields(target, context, new[] { "kind" });
                return null;
            }

            switch (kind.Value)
            {
                case GameplaySkinSceneTargetKind.Stage:
                case GameplaySkinSceneTargetKind.Group:
                case GameplaySkinSceneTargetKind.Lane:
                    validateFields(target, context, new[] { "kind", "id", "index" });
                    string? stableId = getStableId(target, "id", context);
                    int? index = getNonNegativeInt(target, "index", context);
                    return stableId != null && index.HasValue
                        ? new GameplaySkinSceneTarget(kind.Value, stableId, index)
                        : null;

                case GameplaySkinSceneTargetKind.Hud:
                    // HUD may address the global HUD band or one exact stage deck. Stage-local identity is a
                    // pair: accepting only one half would make canonical encode/decode ambiguous.
                    bool hasHudId = target["id"] != null;
                    bool hasHudIndex = target["index"] != null;

                    validateFields(
                        target,
                        context,
                        hasHudId && hasHudIndex ? new[] { "kind", "id", "index" } : new[] { "kind" },
                        hasHudId && hasHudIndex ? null : new[] { "id", "index" });

                    if (hasHudId != hasHudIndex)
                    {
                        context.Add(hasHudIndex
                            ? GameplaySkinSceneDiagnosticCode.InvalidStableId
                            : GameplaySkinSceneDiagnosticCode.InvalidIndex);
                        return null;
                    }

                    if (!hasHudId)
                        return new GameplaySkinSceneTarget(kind.Value, null, null);

                    string? hudId = getStableId(target, "id", context);
                    int? hudIndex = getNonNegativeInt(target, "index", context);
                    return hudId != null && hudIndex.HasValue
                        ? new GameplaySkinSceneTarget(kind.Value, hudId, hudIndex)
                        : null;

                case GameplaySkinSceneTargetKind.Bga:
                    // BGA viewport zero is the deterministic omitted-index default; higher viewports are explicit.
                    validateFields(
                        target,
                        context,
                        target["index"] == null ? new[] { "kind" } : new[] { "kind", "index" },
                        target["index"] == null ? new[] { "index" } : null);
                    int? bgaIndex = target["index"] == null ? null : getNonNegativeInt(target, "index", context);
                    return target["index"] == null || bgaIndex.HasValue
                        ? new GameplaySkinSceneTarget(kind.Value, null, bgaIndex)
                        : null;

                case GameplaySkinSceneTargetKind.Global:
                    validateFields(target, context, new[] { "kind" });
                    return new GameplaySkinSceneTarget(kind.Value, null, null);

                default:
                    context.Add(GameplaySkinSceneDiagnosticCode.UnknownTarget);
                    return null;
            }
        }

        private static GameplaySkinScenePropertyValue? parsePropertyValue(string property, JToken token, DecodeContext context)
        {
            GameplaySkinScenePropertyValue? value = parsePrimitive(token, context);

            if (value == null)
                return null;

            if (!propertyValueHasExpectedKind(property, value))
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                return null;
            }

            if (value.Kind == GameplaySkinScenePropertyValueKind.Number
                && !GameplaySkinSceneNumericRange.IsAllowed(GameplaySkinSceneVocabulary.ParseProperty(property), value.NumberValue))
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                return null;
            }

            if (property is "anchor" or "origin" && !anchor_values.Contains(value.StringValue!))
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                return null;
            }

            if (property == "colour" && !isCanonicalColour(value.StringValue!))
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                return null;
            }

            if (property == "fill-mode" && value.StringValue is not ("stretch" or "fit" or "fill")
                || property == "alignment" && value.StringValue is not ("left" or "centre" or "right")
                || property == "mask-mode" && value.StringValue != "ellipse"
                || property == "clip-mode" && value.StringValue is not ("bounds" or "rounded"))
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                return null;
            }

            if (property == "text")
            {
                context.TotalTextCharacters += value.StringValue!.Length;

                if (context.TotalTextCharacters > GameplaySkinSceneBudgets.MAX_TEXT_CHARACTERS)
                    context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            }

            return value;
        }

        private static GameplaySkinScenePropertyValue? parseEffectPropertyValue(string property, JToken token, DecodeContext context)
        {
            GameplaySkinScenePropertyValue? value = parsePrimitive(token, context);

            if (value == null)
                return null;

            if (property == "colour")
            {
                if (value.Kind != GameplaySkinScenePropertyValueKind.String || !isCanonicalColour(value.StringValue!))
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                    return null;
                }
            }
            else if (value.Kind != GameplaySkinScenePropertyValueKind.Number)
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                return null;
            }
            else if (!effectValueIsAllowed(property, value.NumberValue))
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                return null;
            }

            return value;
        }

        private static GameplaySkinScenePropertyValue? parsePrimitive(JToken token, DecodeContext context)
        {
            switch (token.Type)
            {
                case JTokenType.Boolean:
                    return GameplaySkinScenePropertyValue.FromBoolean(token.Value<bool>());

                case JTokenType.Integer:
                case JTokenType.Float:
                    double number;

                    try
                    {
                        number = token.Value<double>();
                    }
                    catch (Exception exception) when (exception is FormatException or OverflowException)
                    {
                        context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                        return null;
                    }

                    if (!double.IsFinite(number))
                    {
                        context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                        return null;
                    }

                    return GameplaySkinScenePropertyValue.FromNumber(number);

                case JTokenType.String:
                    return GameplaySkinScenePropertyValue.FromString(token.Value<string>()!);

                default:
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                    return null;
            }
        }

        private static GameplaySkinScenePropertyValue? missingPrimitive(DecodeContext context)
        {
            context.Add(GameplaySkinSceneDiagnosticCode.MissingField);
            return null;
        }

        private static bool propertyValueHasExpectedKind(string property, GameplaySkinScenePropertyValue value)
        {
            if (number_properties.Contains(property))
                return value.Kind == GameplaySkinScenePropertyValueKind.Number;

            if (boolean_properties.Contains(property))
                return value.Kind == GameplaySkinScenePropertyValueKind.Boolean;

            return value.Kind == GameplaySkinScenePropertyValueKind.String;
        }

        private static bool effectValueIsAllowed(string property, double value)
        {
            if (!double.IsFinite(value))
                return false;

            return property switch
            {
                "radius" or "blur" => value is >= 0 and <= GameplaySkinSceneBudgets.MAX_EFFECT_BLUR_RADIUS,
                "width" => value is >= 0 and <= GameplaySkinSceneBudgets.MAX_EFFECT_OUTLINE_WIDTH,
                "x" or "y" => Math.Abs(value) <= GameplaySkinSceneBudgets.MAX_EFFECT_SHADOW_OFFSET,
                "strength" => value is >= 0 and <= GameplaySkinSceneBudgets.MAX_EFFECT_STRENGTH,
                _ => false,
            };
        }

        private static bool bindingSourceCanDriveProperty(string source, string property)
        {
            if (property == "text")
                return true;

            bool sourceIsNumber = source is "judgement.offset" or "score.value" or "combo.value" or "gauge.value"
                or "timing.beat" or "timing.measure" or "timing.bpm";
            bool sourceIsBoolean = source == "input.pressed";
            return sourceIsNumber && number_properties.Contains(property)
                   || sourceIsBoolean && boolean_properties.Contains(property);
        }

        private static bool isAllowedNodeProperty(GameplaySkinSceneNodeType type, string property)
        {
            if (property is "opacity" or "visible" or "x" or "y" or "width" or "height" or "scale-x" or "scale-y"
                or "rotation" or "z" or "anchor" or "origin" or "colour")
            {
                return true;
            }

            return type switch
            {
                GameplaySkinSceneNodeType.Sprite => property == "fill-mode",
                GameplaySkinSceneNodeType.Text => property is "text" or "font-size" or "alignment",
                GameplaySkinSceneNodeType.Mask => property == "mask-mode",
                GameplaySkinSceneNodeType.Clip => property is "clip-mode" or "corner-radius",
                _ => false,
            };
        }

        private static bool isAllowedEffectProperty(string type, string property) => type switch
        {
            "blur" => property == "radius",
            "glow" => property is "radius" or "strength" or "colour",
            "outline" => property is "width" or "colour",
            "shadow" => property is "x" or "y" or "blur" or "colour",
            _ => false,
        };

        private static GameplaySkinSceneNodeType? parseNodeType(JToken? token, DecodeContext context)
        {
            string? value = getString(token, context);
            GameplaySkinSceneNodeType? result = value switch
            {
                "sprite" => GameplaySkinSceneNodeType.Sprite,
                "container" => GameplaySkinSceneNodeType.Container,
                "text" => GameplaySkinSceneNodeType.Text,
                "mask" => GameplaySkinSceneNodeType.Mask,
                "clip" => GameplaySkinSceneNodeType.Clip,
                null => null,
                _ => (GameplaySkinSceneNodeType)(-1),
            };

            if (result == (GameplaySkinSceneNodeType)(-1))
            {
                context.Add(GameplaySkinSceneDiagnosticCode.UnknownNodeType);
                return null;
            }

            return result;
        }

        private static GameplaySkinSceneBlendMode? parseBlend(JToken? token, DecodeContext context)
        {
            string? value = getString(token, context);
            GameplaySkinSceneBlendMode? result = value switch
            {
                "inherit" => GameplaySkinSceneBlendMode.Inherit,
                "alpha" => GameplaySkinSceneBlendMode.Alpha,
                "additive" => GameplaySkinSceneBlendMode.Additive,
                "multiply" => GameplaySkinSceneBlendMode.Multiply,
                "screen" => GameplaySkinSceneBlendMode.Screen,
                null => null,
                _ => (GameplaySkinSceneBlendMode)(-1),
            };

            if (result == (GameplaySkinSceneBlendMode)(-1))
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                return null;
            }

            return result;
        }

        private static GameplaySkinSceneTrackType? parseTrackType(JToken? token, DecodeContext context)
        {
            string? value = getString(token, context);
            GameplaySkinSceneTrackType? result = value switch
            {
                "frame" => GameplaySkinSceneTrackType.Frame,
                "tween" => GameplaySkinSceneTrackType.Tween,
                null => null,
                _ => (GameplaySkinSceneTrackType)(-1),
            };

            if (result == (GameplaySkinSceneTrackType)(-1))
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidAnimation);
                return null;
            }

            return result;
        }

        private static GameplaySkinSceneEasing? parseEasing(JToken? token, DecodeContext context)
        {
            string? value = getString(token, context);
            GameplaySkinSceneEasing? result = value switch
            {
                "step" => GameplaySkinSceneEasing.Step,
                "linear" => GameplaySkinSceneEasing.Linear,
                "in" => GameplaySkinSceneEasing.In,
                "out" => GameplaySkinSceneEasing.Out,
                "in-out" => GameplaySkinSceneEasing.InOut,
                null => null,
                _ => (GameplaySkinSceneEasing)(-1),
            };

            if (result == (GameplaySkinSceneEasing)(-1))
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidAnimation);
                return null;
            }

            return result;
        }

        private static GameplaySkinSceneResourceType? parseResourceType(JToken? token, DecodeContext context)
        {
            string? value = getString(token, context);
            GameplaySkinSceneResourceType? result = value switch
            {
                "texture" => GameplaySkinSceneResourceType.Texture,
                null => null,
                _ => (GameplaySkinSceneResourceType)(-1),
            };

            if (result == (GameplaySkinSceneResourceType)(-1))
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidResource);
                return null;
            }

            return result;
        }

        private static string encodeResourceType(GameplaySkinSceneResourceType type) => type switch
        {
            GameplaySkinSceneResourceType.Texture => "texture",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };

        private static JObject encodeNode(GameplaySkinSceneNode node)
        {
            var result = new JObject
            {
                ["id"] = node.Id,
                ["type"] = encodeNodeType(node.Type),
                ["target"] = encodeTarget(node.Target),
            };

            if (node.SlotId != null)
                result["slot"] = node.SlotId;

            if (node.ResourceId != null)
                result["resource"] = node.ResourceId;

            result["blend"] = encodeBlend(node.Blend);
            result["properties"] = encodeProperties(node.Properties);
            result["effects"] = new JArray(node.Effects.Select(effect => new JObject
            {
                ["id"] = effect.Id,
                ["type"] = effect.Type,
                ["properties"] = encodeProperties(effect.Properties),
            }));
            result["children"] = new JArray(node.Children.Select(encodeNode));
            return result;
        }

        private static JObject encodeTarget(GameplaySkinSceneTarget target)
        {
            var result = new JObject { ["kind"] = encodeTargetKind(target.Kind) };

            switch (target.Kind)
            {
                case GameplaySkinSceneTargetKind.Stage:
                case GameplaySkinSceneTargetKind.Group:
                case GameplaySkinSceneTargetKind.Lane:
                    if (target.StableId == null || !target.Index.HasValue)
                        throw new ArgumentException("An indexed scene target is not canonical.", nameof(target));

                    result["id"] = target.StableId;
                    result["index"] = target.Index.Value;
                    break;

                case GameplaySkinSceneTargetKind.Hud:
                    if ((target.StableId == null) != !target.Index.HasValue)
                        throw new ArgumentException("A HUD target identity must be absent or complete.", nameof(target));

                    if (target.StableId != null)
                    {
                        result["id"] = target.StableId;
                        result["index"] = target.Index!.Value;
                    }

                    break;

                case GameplaySkinSceneTargetKind.Bga:
                    if (target.StableId != null)
                        throw new ArgumentException("A BGA target cannot carry a stable lane/group ID.", nameof(target));

                    if (target.Index.HasValue)
                        result["index"] = target.Index.Value;

                    break;

                case GameplaySkinSceneTargetKind.Global:
                    if (target.StableId != null || target.Index.HasValue)
                        throw new ArgumentException("A global target cannot carry identity.", nameof(target));

                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(target), target.Kind, null);
            }

            return result;
        }

        private static JObject encodeProperties(IReadOnlyDictionary<string, GameplaySkinScenePropertyValue> properties)
        {
            var result = new JObject();

            foreach ((string id, GameplaySkinScenePropertyValue value) in properties.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                result[id] = encodeValue(value);

            return result;
        }

        private static JObject encodeTrack(GameplaySkinSceneTrack track) => new JObject
        {
            ["id"] = track.Id,
            ["type"] = track.Type == GameplaySkinSceneTrackType.Frame ? "frame" : "tween",
            ["target"] = track.TargetNodeId,
            ["property"] = track.PropertyId,
            ["easing"] = encodeEasing(track.Easing),
            ["loop"] = track.Loop,
            ["keyframes"] = new JArray(track.Keyframes.Select(keyframe => new JObject
            {
                ["id"] = keyframe.Id,
                ["time"] = keyframe.Time,
                ["value"] = encodeValue(keyframe.Value),
            })),
        };

        private static JObject encodeStateMachine(GameplaySkinSceneStateMachine machine) => new JObject
        {
            ["id"] = machine.Id,
            ["initial"] = machine.InitialStateId,
            ["states"] = new JArray(machine.States.Select(state => new JObject
            {
                ["id"] = state.Id,
                ["set"] = new JArray(state.Assignments.Select(assignment => new JObject
                {
                    ["id"] = assignment.Id,
                    ["target"] = assignment.TargetNodeId,
                    ["property"] = assignment.PropertyId,
                    ["value"] = encodeValue(assignment.Value),
                })),
            })),
            ["transitions"] = new JArray(machine.Transitions.Select(transition => new JObject
            {
                ["id"] = transition.Id,
                ["from"] = transition.FromStateId,
                ["to"] = transition.ToStateId,
                ["event"] = transition.EventId,
            })),
        };

        private static JValue encodeValue(GameplaySkinScenePropertyValue value) => value.Kind switch
        {
            GameplaySkinScenePropertyValueKind.Boolean => new JValue(value.BooleanValue),
            GameplaySkinScenePropertyValueKind.Number => new JValue(value.NumberValue),
            GameplaySkinScenePropertyValueKind.String => new JValue(value.StringValue),
            _ => throw new ArgumentOutOfRangeException(nameof(value), value.Kind, null),
        };

        private static string encodeNodeType(GameplaySkinSceneNodeType type) => type switch
        {
            GameplaySkinSceneNodeType.Sprite => "sprite",
            GameplaySkinSceneNodeType.Container => "container",
            GameplaySkinSceneNodeType.Text => "text",
            GameplaySkinSceneNodeType.Mask => "mask",
            GameplaySkinSceneNodeType.Clip => "clip",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };

        private static string encodeTargetKind(GameplaySkinSceneTargetKind kind) => kind switch
        {
            GameplaySkinSceneTargetKind.Global => "global",
            GameplaySkinSceneTargetKind.Stage => "stage",
            GameplaySkinSceneTargetKind.Group => "group",
            GameplaySkinSceneTargetKind.Lane => "lane",
            GameplaySkinSceneTargetKind.Hud => "hud",
            GameplaySkinSceneTargetKind.Bga => "bga",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        private static string encodeBlend(GameplaySkinSceneBlendMode blend) => blend switch
        {
            GameplaySkinSceneBlendMode.Inherit => "inherit",
            GameplaySkinSceneBlendMode.Alpha => "alpha",
            GameplaySkinSceneBlendMode.Additive => "additive",
            GameplaySkinSceneBlendMode.Multiply => "multiply",
            GameplaySkinSceneBlendMode.Screen => "screen",
            _ => throw new ArgumentOutOfRangeException(nameof(blend), blend, null),
        };

        private static string encodeEasing(GameplaySkinSceneEasing easing) => easing switch
        {
            GameplaySkinSceneEasing.Step => "step",
            GameplaySkinSceneEasing.Linear => "linear",
            GameplaySkinSceneEasing.In => "in",
            GameplaySkinSceneEasing.Out => "out",
            GameplaySkinSceneEasing.InOut => "in-out",
            _ => throw new ArgumentOutOfRangeException(nameof(easing), easing, null),
        };

        private static JObject? parseRoot(ReadOnlySpan<byte> json, DecodeContext context)
        {
            try
            {
                var reader = new Utf8JsonReader(json, new JsonReaderOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    // The codec applies the tighter public budget from structured token depth below. This parser
                    // ceiling only protects itself and is never used to classify a public diagnostic.
                    MaxDepth = GameplaySkinSceneBudgets.MAX_JSON_DEPTH * 2,
                });
                using var writer = new JTokenWriter();
                var objectFields = new Stack<HashSet<string>?>();

                while (reader.Read())
                {
                    if (reader.CurrentDepth > GameplaySkinSceneBudgets.MAX_JSON_DEPTH)
                    {
                        context.Add(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                        return null;
                    }

                    switch (reader.TokenType)
                    {
                        case JsonTokenType.StartObject:
                            writer.WriteStartObject();
                            objectFields.Push(new HashSet<string>(StringComparer.Ordinal));
                            break;

                        case JsonTokenType.EndObject:
                            writer.WriteEndObject();
                            objectFields.Pop();
                            break;

                        case JsonTokenType.StartArray:
                            writer.WriteStartArray();
                            objectFields.Push(null);
                            break;

                        case JsonTokenType.EndArray:
                            writer.WriteEndArray();
                            objectFields.Pop();
                            break;

                        case JsonTokenType.PropertyName:
                        {
                            string name = reader.GetString()!;

                            if (objectFields.Count == 0
                                || objectFields.Peek() == null
                                || !objectFields.Peek()!.Add(name))
                            {
                                context.Add(GameplaySkinSceneDiagnosticCode.DuplicateField);
                                return null;
                            }

                            writer.WritePropertyName(name);
                            break;
                        }

                        case JsonTokenType.String:
                            writer.WriteValue(reader.GetString());
                            break;

                        case JsonTokenType.Number:
                            if (reader.TryGetInt64(out long integer))
                                writer.WriteValue(integer);
                            else if (reader.TryGetDouble(out double number) && double.IsFinite(number))
                                writer.WriteValue(number);
                            else
                            {
                                context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                                return null;
                            }

                            break;

                        case JsonTokenType.True:
                            writer.WriteValue(true);
                            break;

                        case JsonTokenType.False:
                            writer.WriteValue(false);
                            break;

                        case JsonTokenType.Null:
                            writer.WriteNull();
                            break;

                        default:
                            context.Add(GameplaySkinSceneDiagnosticCode.InvalidJson);
                            return null;
                    }
                }

                if (objectFields.Count != 0 || writer.Token is not JObject root)
                {
                    context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                    return null;
                }

                return root;
            }
            catch (System.Text.Json.JsonException)
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidJson);
                return null;
            }
        }

        private static void validateFields(JObject value, DecodeContext context, IEnumerable<string> required, IEnumerable<string>? optional = null)
        {
            string[] requiredFields = required.ToArray();
            var allowed = new HashSet<string>(requiredFields, StringComparer.Ordinal);

            if (optional != null)
                allowed.UnionWith(optional);

            foreach (JProperty property in value.Properties())
            {
                if (!allowed.Contains(property.Name))
                    context.Add(GameplaySkinSceneDiagnosticCode.UnknownField);
            }

            foreach (string field in requiredFields)
            {
                if (!value.ContainsKey(field))
                    context.Add(GameplaySkinSceneDiagnosticCode.MissingField);
            }
        }

        private static void validateExactContract(JObject value, string property, string expected, DecodeContext context)
        {
            string? actual = getString(value, property, context);

            if (actual != null && actual != expected)
                context.Add(GameplaySkinSceneDiagnosticCode.UnsupportedContract);
        }

        private static JArray? getArray(JObject value, string property, DecodeContext context)
        {
            if (!value.TryGetValue(property, out JToken? token))
            {
                context.Add(GameplaySkinSceneDiagnosticCode.MissingField);
                return null;
            }

            if (token is not JArray array)
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                return null;
            }

            return array;
        }

        private static string? getString(JObject value, string property, DecodeContext context)
        {
            if (!value.TryGetValue(property, out JToken? token))
            {
                context.Add(GameplaySkinSceneDiagnosticCode.MissingField);
                return null;
            }

            return getString(token, context);
        }

        private static string? getString(JToken? token, DecodeContext context)
        {
            if (token?.Type != JTokenType.String)
            {
                context.Add(token == null ? GameplaySkinSceneDiagnosticCode.MissingField : GameplaySkinSceneDiagnosticCode.InvalidValueType);
                return null;
            }

            return token.Value<string>();
        }

        private static string? getStableId(JObject value, string property, DecodeContext context)
        {
            if (!value.TryGetValue(property, out JToken? token))
            {
                context.Add(GameplaySkinSceneDiagnosticCode.MissingField);
                return null;
            }

            return getStableId(token, context);
        }

        private static string? getStableId(JToken token, DecodeContext context)
        {
            string? value = getString(token, context);

            if (value == null)
                return null;

            try
            {
                if (value.Length > GameplaySkinSceneBudgets.MAX_STABLE_ID_LENGTH)
                    throw new ArgumentException();

                GameplaySkinStableIdentityId.Validate(value, nameof(value));
                return value;
            }
            catch (ArgumentException)
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidStableId);
                return null;
            }
        }

        private static bool? getBoolean(JObject value, string property, DecodeContext context)
        {
            if (!value.TryGetValue(property, out JToken? token))
            {
                context.Add(GameplaySkinSceneDiagnosticCode.MissingField);
                return null;
            }

            if (token.Type != JTokenType.Boolean)
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                return null;
            }

            return token.Value<bool>();
        }

        private static double? getNumber(JObject value, string property, DecodeContext context)
        {
            if (!value.TryGetValue(property, out JToken? token))
            {
                context.Add(GameplaySkinSceneDiagnosticCode.MissingField);
                return null;
            }

            if (token.Type is not (JTokenType.Integer or JTokenType.Float))
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                return null;
            }

            double result;

            try
            {
                result = token.Value<double>();
            }
            catch (Exception exception) when (exception is FormatException or OverflowException)
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidValueType);
                return null;
            }

            if (!double.IsFinite(result))
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);
                return null;
            }

            return result;
        }

        private static int? getNonNegativeInt(JObject value, string property, DecodeContext context)
        {
            if (!value.TryGetValue(property, out JToken? token))
            {
                context.Add(GameplaySkinSceneDiagnosticCode.MissingField);
                return null;
            }

            if (token.Type != JTokenType.Integer)
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidIndex);
                return null;
            }

            long raw;

            try
            {
                raw = token.Value<long>();
            }
            catch (Exception exception) when (exception is FormatException or OverflowException)
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidIndex);
                return null;
            }

            if (raw < 0 || raw > int.MaxValue)
            {
                context.Add(GameplaySkinSceneDiagnosticCode.InvalidIndex);
                return null;
            }

            return (int)raw;
        }

        private static T? missingOrInvalidObject<T>(JObject parent, string field, DecodeContext context)
            where T : class
        {
            context.Add(parent.ContainsKey(field) ? GameplaySkinSceneDiagnosticCode.InvalidValueType : GameplaySkinSceneDiagnosticCode.MissingField);
            return null;
        }

        private static IReadOnlyDictionary<string, GameplaySkinScenePropertyValue> missingOrInvalidProperties(
            JObject parent,
            string field,
            DecodeContext context)
        {
            context.Add(parent.ContainsKey(field) ? GameplaySkinSceneDiagnosticCode.InvalidValueType : GameplaySkinSceneDiagnosticCode.MissingField);
            return new Dictionary<string, GameplaySkinScenePropertyValue>();
        }

        private static bool tryDecodeUtf8(ReadOnlySpan<byte> content, out string value)
        {
            try
            {
                value = strict_utf8.GetString(content);
                return true;
            }
            catch (DecoderFallbackException)
            {
                value = string.Empty;
                return false;
            }
        }

        private static bool tryNormalizeRelativeResourcePath(string raw, out string normalized)
        {
            normalized = string.Empty;

            if (raw.Length == 0 || raw.Length > GameplaySkinSceneBudgets.MAX_RESOURCE_PATH_LENGTH)
                return false;

            if (!SkinPackageResourceNameValidator.TryNormalise(raw, out string candidate, out _)
                || candidate.Length == 0
                || candidate.Length > GameplaySkinSceneBudgets.MAX_RESOURCE_PATH_LENGTH)
                return false;

            normalized = candidate;
            return true;
        }

        private static bool isCanonicalColour(string value)
        {
            if (value.Length is not (7 or 9) || value[0] != '#')
                return false;

            return value.AsSpan(1).IndexOfAnyExcept(hexadecimal_characters) < 0;
        }

        private static bool isKnownResource(string id, GameplaySkinSceneManifest? manifest)
            => manifest != null && manifest.TryGetResource(id, out _);

        private static int countNodes(GameplaySkinSceneNode root)
        {
            int count = 1;

            foreach (GameplaySkinSceneNode child in root.Children)
                count = checked(count + countNodes(child));

            return count;
        }

        private static GameplaySkinSceneDecodeResult<T> absent<T>()
            where T : class
            => new GameplaySkinSceneDecodeResult<T>(GameplaySkinSceneDecodeStatus.Absent, null, Array.Empty<GameplaySkinSceneDiagnostic>());

        private static GameplaySkinSceneDecodeResult<T> invalid<T>(GameplaySkinSceneDiagnosticCode code)
            where T : class
            => new GameplaySkinSceneDecodeResult<T>(GameplaySkinSceneDecodeStatus.Invalid, null, new[] { new GameplaySkinSceneDiagnostic(code) });

        private static GameplaySkinSceneDecodeResult<T> valid<T>(T value)
            where T : class
            => new GameplaySkinSceneDecodeResult<T>(GameplaySkinSceneDecodeStatus.Valid, value, Array.Empty<GameplaySkinSceneDiagnostic>());

        private sealed class DecodeContext
        {
            private readonly List<GameplaySkinSceneDiagnostic> diagnostics = new List<GameplaySkinSceneDiagnostic>();
            private readonly HashSet<GameplaySkinSceneDiagnosticCode> diagnosticCodes = new HashSet<GameplaySkinSceneDiagnosticCode>();

            public GameplaySkinSceneManifest? Manifest { get; }

            public HashSet<string> AllStableIds { get; } = new HashSet<string>(StringComparer.Ordinal);

            public Dictionary<string, GameplaySkinSceneNodeType> NodeTypes { get; } = new Dictionary<string, GameplaySkinSceneNodeType>(StringComparer.Ordinal);

            public int TotalNodeCount { get; set; }

            public int MainNodeCount { get; set; }

            public int TotalEffectCount { get; set; }

            public int TotalKeyframeCount { get; set; }

            public int TotalStateCount { get; set; }

            public int TotalStateAssignmentCount { get; set; }

            public int TotalTransitionCount { get; set; }

            public int TotalTextCharacters { get; set; }

            public int TotalVariantCaseCount { get; set; }

            public bool HasDiagnostics => diagnostics.Count > 0;

            public DecodeContext(GameplaySkinSceneManifest? manifest = null)
            {
                Manifest = manifest;
            }

            public void Add(GameplaySkinSceneDiagnosticCode code)
            {
                if (diagnosticCodes.Add(code))
                    diagnostics.Add(new GameplaySkinSceneDiagnostic(code));
            }

            public GameplaySkinSceneDecodeResult<T> Invalid<T>()
                where T : class
            {
                if (diagnostics.Count == 0)
                    Add(GameplaySkinSceneDiagnosticCode.InvalidJson);

                return new GameplaySkinSceneDecodeResult<T>(GameplaySkinSceneDecodeStatus.Invalid, null, diagnostics);
            }
        }
    }
}
