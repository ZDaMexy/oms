// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinSceneCodecTest
    {
        private const string valid_manifest = """
                                              {
                                                "contract": "oms-gameplay-skin-manifest.v1",
                                                "scene": "gameplay-skin.scene.json",
                                                "sceneContract": "oms-gameplay-skin-scene.v1",
                                                "eventContract": "oms-gameplay-skin-event.v1",
                                                "resources": [
                                                  { "id": "texture.note", "type": "texture", "path": "Textures/Note.png" },
                                                  { "id": "texture.frame-2", "type": "texture", "path": "Textures/Note-2.png" }
                                                ]
                                              }
                                              """;

        private const string valid_scene = """
                                           {
                                             "contract": "oms-gameplay-skin-scene.v1",
                                             "root": {
                                               "id": "node.root",
                                               "type": "container",
                                               "target": { "kind": "global" },
                                               "slot": "decoration",
                                               "blend": "alpha",
                                               "properties": { "opacity": 1.0, "visible": true },
                                               "effects": [],
                                               "children": [
                                                 {
                                                   "id": "node.note",
                                                   "type": "sprite",
                                                   "target": { "kind": "lane", "id": "bms.lane.key-1", "index": 0 },
                                                   "slot": "object.note",
                                                   "resource": "texture.note",
                                                   "blend": "additive",
                                                   "properties": { "anchor": "centre", "x": 0.5, "y": 0.75, "colour": "#ffffffff" },
                                                   "effects": [
                                                     { "id": "effect.note-glow", "type": "glow", "properties": { "radius": 8.0, "strength": 0.75, "colour": "#80ffffff" } }
                                                   ],
                                                   "children": []
                                                 },
                                                 {
                                                   "id": "node.text",
                                                   "type": "text",
                                                   "target": { "kind": "hud" },
                                                   "slot": "hud.text",
                                                   "blend": "alpha",
                                                   "properties": { "text": "0", "font-size": 32.0, "alignment": "centre" },
                                                   "effects": [],
                                                   "children": []
                                                 },
                                                 {
                                                   "id": "node.mask",
                                                   "type": "mask",
                                                   "target": { "kind": "stage", "id": "bms.group.deck-1", "index": 0 },
                                                   "slot": "stage.background",
                                                   "blend": "multiply",
                                                   "properties": { "mask-mode": "ellipse" },
                                                   "effects": [],
                                                   "children": []
                                                 },
                                                 {
                                                   "id": "node.clip",
                                                   "type": "clip",
                                                   "target": { "kind": "bga" },
                                                   "slot": "bga.viewport",
                                                   "blend": "screen",
                                                   "properties": { "clip-mode": "bounds", "width": 1.0, "height": 1.0 },
                                                   "effects": [],
                                                   "children": []
                                                 }
                                               ]
                                             },
                                             "tracks": [
                                               {
                                                 "id": "track.note-frame",
                                                 "type": "frame",
                                                 "target": "node.note",
                                                 "property": "resource",
                                                 "easing": "step",
                                                 "loop": true,
                                                 "keyframes": [
                                                   { "id": "keyframe.note-0", "time": 0.0, "value": "texture.note" },
                                                   { "id": "keyframe.note-1", "time": 16.6666667, "value": "texture.frame-2" }
                                                 ]
                                               },
                                               {
                                                 "id": "track.note-opacity",
                                                 "type": "tween",
                                                 "target": "node.note",
                                                 "property": "opacity",
                                                 "easing": "in-out",
                                                 "loop": false,
                                                 "keyframes": [
                                                   { "id": "keyframe.opacity-0", "time": 0.0, "value": 0.25 },
                                                   { "id": "keyframe.opacity-1", "time": 100.0, "value": 1.0 }
                                                 ]
                                               }
                                             ],
                                             "stateMachines": [
                                               {
                                                 "id": "machine.key",
                                                 "initial": "state.idle",
                                                 "states": [
                                                    {
                                                      "id": "state.idle",
                                                      "set": [
                                                        { "id": "assignment.idle-opacity", "target": "node.note", "property": "opacity", "value": 0.25 }
                                                      ]
                                                    },
                                                    {
                                                      "id": "state.pressed",
                                                      "set": [
                                                        { "id": "assignment.pressed-opacity", "target": "node.note", "property": "opacity", "value": 1.0 }
                                                      ]
                                                    }
                                                 ],
                                                 "transitions": [
                                                   { "id": "transition.press", "from": "state.idle", "to": "state.pressed", "event": "input.key.down" },
                                                   { "id": "transition.release", "from": "state.pressed", "to": "state.idle", "event": "input.key.up" }
                                                 ]
                                               }
                                             ],
                                             "bindings": [
                                               { "id": "binding.combo", "target": "node.text", "property": "text", "source": "combo.value" }
                                             ],
                                             "templates": [
                                               {
                                                 "id": "template.lane",
                                                 "root": {
                                                   "id": "template-node.root",
                                                   "type": "container",
                                                   "target": { "kind": "lane", "id": "bms.lane.key-1", "index": 0 },
                                                   "slot": "playfield.lane-surface",
                                                   "blend": "inherit",
                                                   "properties": {},
                                                   "effects": [],
                                                   "children": []
                                                 }
                                               }
                                             ],
                                             "instances": [
                                               { "id": "instance.lane-1", "template": "template.lane", "target": { "kind": "lane", "id": "bms.lane.key-1", "index": 0 } }
                                             ]
                                           }
                                           """;

        [Test]
        public void TestContractsFilenamesAndAbsentAreStable()
        {
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> absentManifest = GameplaySkinSceneCodec.DecodeManifest((string?)null);
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> absentScene = GameplaySkinSceneCodec.DecodeScene((string?)null, null);

            Assert.Multiple(() =>
            {
                Assert.That(GameplaySkinSceneContracts.MANIFEST_CONTRACT_ID, Is.EqualTo("oms-gameplay-skin-manifest.v1"));
                Assert.That(GameplaySkinSceneContracts.SCENE_CONTRACT_ID, Is.EqualTo("oms-gameplay-skin-scene.v1"));
                Assert.That(GameplaySkinSceneContracts.EVENT_CONTRACT_ID, Is.EqualTo("oms-gameplay-skin-event.v1"));
                Assert.That(GameplaySkinSceneContracts.MANIFEST_FILE_NAME, Is.EqualTo("gameplay-skin.json"));
                Assert.That(GameplaySkinSceneContracts.SCENE_FILE_NAME, Is.EqualTo("gameplay-skin.scene.json"));
                Assert.That(absentManifest.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Absent));
                Assert.That(absentManifest.Value, Is.Null);
                Assert.That(absentManifest.Diagnostics, Is.Empty);
                Assert.That(absentScene.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Absent));
            });
        }

        [Test]
        public void TestManifestCanonicalRoundTripAndImmutability()
        {
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> first = decodeManifest(valid_manifest);
            string canonical = GameplaySkinSceneCodec.EncodeManifest(first.Value!);
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> second = decodeManifest(canonical);

            Assert.Multiple(() =>
            {
                Assert.That(first.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid));
                Assert.That(first.Diagnostics, Is.Empty);
                Assert.That(first.Value!.SceneFile, Is.EqualTo(GameplaySkinSceneContracts.SCENE_FILE_NAME));
                Assert.That(first.Value.SceneContract, Is.EqualTo(GameplaySkinSceneContracts.SCENE_CONTRACT_ID));
                Assert.That(first.Value.EventContract, Is.EqualTo(GameplaySkinSceneContracts.EVENT_CONTRACT_ID));
                Assert.That(first.Value.Resources.Select(resource => resource.Id),
                    Is.EqualTo(new[] { "texture.note", "texture.frame-2" }));
                Assert.That(first.Value.Resources[0].Path, Is.EqualTo("Textures/Note.png"));
                Assert.That(GameplaySkinSceneCodec.EncodeManifest(second.Value!), Is.EqualTo(canonical));
                Assert.That(() => ((IList<GameplaySkinSceneResource>)first.Value.Resources).Clear(), Throws.TypeOf<NotSupportedException>());
            });
        }

        [Test]
        public void TestManifestRejectsUnknownDuplicateAndUnsafePaths()
        {
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> duplicateField = decodeManifest(
                valid_manifest.Replace("\"scene\": \"gameplay-skin.scene.json\",", "\"scene\": \"gameplay-skin.scene.json\", \"scene\": \"gameplay-skin.scene.json\","));
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> unknownField = decodeManifest(
                valid_manifest.Replace("\"resources\":", "\"privateApi\": true, \"resources\":"));
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> unknownVersion = decodeManifest(
                valid_manifest.Replace(GameplaySkinSceneContracts.MANIFEST_CONTRACT_ID, "oms-gameplay-skin-manifest.v2"));
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> absolute = manifestWithPaths("C:/secret.png");
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> unc = manifestWithPaths("//server/share.png");
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> traversal = manifestWithPaths("textures/../secret.png");
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> device = manifestWithPaths("textures/CON.png");
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> wildcard = manifestWithPaths("textures/note*.png");
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> normalizedCollision = manifestWithPaths("Textures/é.png", "textures/e\u0301.PNG");
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> unsupportedFont = decodeManifest(
                valid_manifest.Replace("\"type\": \"texture\"", "\"type\": \"font\"", StringComparison.Ordinal));

            Assert.Multiple(() =>
            {
                assertInvalidWith(duplicateField, GameplaySkinSceneDiagnosticCode.DuplicateField);
                assertInvalidWith(unknownField, GameplaySkinSceneDiagnosticCode.UnknownField);
                assertInvalidWith(unknownVersion, GameplaySkinSceneDiagnosticCode.UnsupportedContract);
                assertInvalidWith(absolute, GameplaySkinSceneDiagnosticCode.UnsafeResourcePath);
                assertInvalidWith(unc, GameplaySkinSceneDiagnosticCode.UnsafeResourcePath);
                assertInvalidWith(traversal, GameplaySkinSceneDiagnosticCode.UnsafeResourcePath);
                assertInvalidWith(device, GameplaySkinSceneDiagnosticCode.UnsafeResourcePath);
                assertInvalidWith(wildcard, GameplaySkinSceneDiagnosticCode.UnsafeResourcePath);
                assertInvalidWith(normalizedCollision, GameplaySkinSceneDiagnosticCode.DuplicateNormalizedPath);
                assertInvalidWith(unsupportedFont, GameplaySkinSceneDiagnosticCode.InvalidResource);
            });
        }

        [Test]
        public void TestJsonGrammarAndUtf8AreStrict()
        {
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> comment = decodeManifest(
                valid_manifest.Replace("\"resources\":", "/* comment */ \"resources\":"));
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> trailingComma = decodeManifest(
                valid_manifest.Insert(valid_manifest.LastIndexOf('}'), ","));
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> multipleDocuments = decodeManifest(valid_manifest + "{}");
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> invalidUtf8 = GameplaySkinSceneCodec.DecodeManifest(new byte[] { 0xc3, 0x28 });
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> invalidUnicodeString = decodeManifest("\ud800");

            Assert.Multiple(() =>
            {
                assertInvalidWith(comment, GameplaySkinSceneDiagnosticCode.InvalidJson);
                assertInvalidWith(trailingComma, GameplaySkinSceneDiagnosticCode.InvalidJson);
                assertInvalidWith(multipleDocuments, GameplaySkinSceneDiagnosticCode.InvalidJson);
                assertInvalidWith(invalidUtf8, GameplaySkinSceneDiagnosticCode.InvalidUtf8);
                assertInvalidWith(invalidUnicodeString, GameplaySkinSceneDiagnosticCode.InvalidUtf8);
            });
        }

        [Test]
        public void TestSceneSupportsAllowlistedGraphAnimationStateBindingAndTemplate()
        {
            GameplaySkinSceneManifest manifest = decodeManifest(valid_manifest).Value!;
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> first = decodeScene(valid_scene, manifest);
            Assert.That(first.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid),
                string.Join(",", first.Diagnostics.Select(diagnostic => diagnostic.Id)));
            string canonical = GameplaySkinSceneCodec.EncodeScene(first.Value!);
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> second = decodeScene(canonical, manifest);

            Assert.Multiple(() =>
            {
                Assert.That(first.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid));
                Assert.That(first.Diagnostics, Is.Empty);
                Assert.That(first.Value!.Root.Type, Is.EqualTo(GameplaySkinSceneNodeType.Container));
                Assert.That(first.Value.Root.SlotId, Is.EqualTo(GameplaySkinSlotCatalog.Decoration.Id));
                Assert.That(first.Value.Root.Children.Select(node => node.SlotId), Is.EqualTo(new[]
                {
                    GameplaySkinSlotCatalog.Note.Id,
                    GameplaySkinSlotCatalog.TextHud.Id,
                    GameplaySkinSlotCatalog.StageBackground.Id,
                    GameplaySkinSlotCatalog.BgaViewport.Id,
                }));
                Assert.That(first.Value.Root.Children.Select(node => node.Type), Is.EqualTo(new[]
                {
                    GameplaySkinSceneNodeType.Sprite,
                    GameplaySkinSceneNodeType.Text,
                    GameplaySkinSceneNodeType.Mask,
                    GameplaySkinSceneNodeType.Clip,
                }));
                Assert.That(first.Value.Tracks.Select(track => track.Type), Is.EqualTo(new[]
                {
                    GameplaySkinSceneTrackType.Frame,
                    GameplaySkinSceneTrackType.Tween,
                }));
                Assert.That(first.Value.StateMachines.Single().Transitions.First().EventId, Is.EqualTo("input.key.down"));
                Assert.That(first.Value.Bindings.Single().SourceId, Is.EqualTo("combo.value"));
                Assert.That(first.Value.Templates.Single().Id, Is.EqualTo("template.lane"));
                Assert.That(first.Value.Instances.Single().TemplateId, Is.EqualTo("template.lane"));
                Assert.That(GameplaySkinSceneCodec.EncodeScene(second.Value!), Is.EqualTo(canonical));
                Assert.That(() => ((IList<GameplaySkinSceneTrack>)first.Value.Tracks).Clear(), Throws.TypeOf<NotSupportedException>());
                Assert.That(() => ((IDictionary<string, GameplaySkinScenePropertyValue>)first.Value.Root.Properties).Clear(), Throws.TypeOf<NotSupportedException>());
            });
        }

        [TestCase("gameplay.loaded", GameplaySkinSceneEvent.GameplayLoaded)]
        [TestCase("gameplay.complete", GameplaySkinSceneEvent.GameplayComplete)]
        [TestCase("gameplay.fail", GameplaySkinSceneEvent.GameplayFailed)]
        public void TestLifecycleSceneEventsRemainDistinct(string eventId, GameplaySkinSceneEvent expected)
        {
            GameplaySkinSceneManifest manifest = decodeManifest(valid_manifest).Value!;
            JObject scene = JObject.Parse(valid_scene);
            var transition = (JObject)((JArray)((JObject)((JArray)scene["stateMachines"]!)[0]!)["transitions"]!)[0]!;
            transition["event"] = eventId;

            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> result = decodeScene(scene.ToString(), manifest);

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid));
                Assert.That(result.Value!.StateMachines.Single().Transitions.First().Event, Is.EqualTo(expected));
                Assert.That(result.Value.StateMachines.Single().Transitions.First().EventId, Is.EqualTo(eventId));
            });
        }

        [TestCase("gameplay.resume")]
        [TestCase("gameplay.reset")]
        [TestCase("layout.committed")]
        [TestCase("object.despawn")]
        [TestCase("score.changed")]
        [TestCase("combo.changed")]
        [TestCase("gauge.changed")]
        [TestCase("timing.beat")]
        [TestCase("timing.measure")]
        [TestCase("timing.bpm")]
        [TestCase("timing.scroll")]
        public void TestStateMachineRejectsEventsWhichCannotBeProjectedFromCompleteSnapshot(string eventId)
        {
            GameplaySkinSceneManifest manifest = decodeManifest(valid_manifest).Value!;
            JObject scene = JObject.Parse(valid_scene);
            var transition = (JObject)((JArray)((JObject)((JArray)scene["stateMachines"]!)[0]!)["transitions"]!)[0]!;
            transition["event"] = eventId;

            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> result = decodeScene(scene.ToString(), manifest);

            assertInvalidWith(result, GameplaySkinSceneDiagnosticCode.UnknownEvent);
        }

        [Test]
        public void TestSceneReportsUnknownNodePropertyEffectAndEvent()
        {
            GameplaySkinSceneManifest manifest = decodeManifest(valid_manifest).Value!;
            JObject scene = JObject.Parse(valid_scene);
            JObject note = (JObject)((JArray)scene["root"]!["children"]!)[0]!;
            note["type"] = "shader";
            note["properties"]!["private-transform"] = 1;
            ((JObject)((JArray)note["effects"]!)[0]!)["type"] = "arbitrary-shader";
            // P1-L retains BGA POOR/timeline authority; C5 exposes only the engine-owned viewport/content-state summary.
            ((JObject)((JArray)((JObject)((JArray)scene["stateMachines"]!)[0]!)["transitions"]!)[0]!)["event"] = "bga.poor";

            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> result = decodeScene(scene.ToString(), manifest);

            Assert.Multiple(() =>
            {
                assertInvalidWith(result, GameplaySkinSceneDiagnosticCode.UnknownNodeType);
                assertInvalidWith(result, GameplaySkinSceneDiagnosticCode.UnknownProperty);
                assertInvalidWith(result, GameplaySkinSceneDiagnosticCode.UnknownEffect);
                assertInvalidWith(result, GameplaySkinSceneDiagnosticCode.UnknownEvent);
            });
        }

        [Test]
        public void TestSceneReportsInvalidTypesIndicesTargetsAndResources()
        {
            GameplaySkinSceneManifest manifest = decodeManifest(valid_manifest).Value!;

            JObject badType = JObject.Parse(valid_scene);
            badType["root"]!["properties"]!["opacity"] = "one";
            JObject badIndex = JObject.Parse(valid_scene);
            ((JObject)((JArray)badIndex["root"]!["children"]!)[0]!["target"]!)["index"] = -1;
            JObject badTarget = JObject.Parse(valid_scene);
            ((JObject)((JArray)badTarget["root"]!["children"]!)[0]!["target"]!)["kind"] = "filesystem";
            JObject badResource = JObject.Parse(valid_scene);
            ((JObject)((JArray)badResource["root"]!["children"]!)[0]!)["resource"] = "texture.missing";
            JObject badSlot = JObject.Parse(valid_scene);
            ((JObject)((JArray)badSlot["root"]!["children"]!)[0]!)["slot"] = "object.unadvertised";

            Assert.Multiple(() =>
            {
                assertInvalidWith(decodeScene(badType.ToString(), manifest), GameplaySkinSceneDiagnosticCode.InvalidValueType);
                assertInvalidWith(decodeScene(badIndex.ToString(), manifest), GameplaySkinSceneDiagnosticCode.InvalidIndex);
                assertInvalidWith(decodeScene(badTarget.ToString(), manifest), GameplaySkinSceneDiagnosticCode.UnknownTarget);
                assertInvalidWith(decodeScene(badResource.ToString(), manifest), GameplaySkinSceneDiagnosticCode.UnknownResource);
                assertInvalidWith(decodeScene(badSlot.ToString(), manifest), GameplaySkinSceneDiagnosticCode.UnknownSlot);
            });
        }

        [Test]
        public void TestSceneRejectsDuplicateJsonFieldsAndStableIds()
        {
            GameplaySkinSceneManifest manifest = decodeManifest(valid_manifest).Value!;
            string duplicateField = valid_scene.Replace("\"contract\": \"oms-gameplay-skin-scene.v1\",", "\"contract\": \"oms-gameplay-skin-scene.v1\", \"contract\": \"oms-gameplay-skin-scene.v1\",");
            JObject duplicateIds = JObject.Parse(valid_scene);
            ((JObject)((JArray)duplicateIds["root"]!["children"]!)[1]!)["id"] = "node.note";

            Assert.Multiple(() =>
            {
                assertInvalidWith(decodeScene(duplicateField, manifest), GameplaySkinSceneDiagnosticCode.DuplicateField);
                assertInvalidWith(decodeScene(duplicateIds.ToString(), manifest), GameplaySkinSceneDiagnosticCode.DuplicateStableId);
            });
        }

        [Test]
        public void TestDecodeBudgetsAreHardAndReported()
        {
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> manifestBytes = GameplaySkinSceneCodec.DecodeManifest(
                new byte[GameplaySkinSceneBudgets.MAX_MANIFEST_BYTES + 1]);
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> sceneBytes = GameplaySkinSceneCodec.DecodeScene(
                new byte[GameplaySkinSceneBudgets.MAX_SCENE_BYTES + 1], null);
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> resources = decodeManifest(createManifestWithResourceCount(GameplaySkinSceneBudgets.MAX_RESOURCES + 1));
            GameplaySkinSceneManifest manifest = decodeManifest(valid_manifest).Value!;
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> depth = decodeScene(createSceneWithDepth(GameplaySkinSceneBudgets.MAX_NODE_DEPTH + 1), manifest);
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> keyframes = decodeScene(createSceneWithKeyframes(GameplaySkinSceneBudgets.MAX_KEYFRAMES_PER_TRACK + 1), manifest);
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> text = decodeScene(createSceneWithText(GameplaySkinSceneBudgets.MAX_TEXT_CHARACTERS + 1), manifest);

            Assert.Multiple(() =>
            {
                assertInvalidWith(manifestBytes, GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                assertInvalidWith(sceneBytes, GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                assertInvalidWith(resources, GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                assertInvalidWith(depth, GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                assertInvalidWith(keyframes, GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                assertInvalidWith(text, GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            });
        }

        [Test]
        public void TestTemplateExpansionNodeTrackBindingAndEffectBudgetsAreEnforced()
        {
            GameplaySkinSceneManifest manifest = decodeManifest(valid_manifest).Value!;
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> nodeCount = decodeScene(createSceneExceedingNodeCount(), manifest);
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> templateExpansion = decodeScene(createSceneExceedingTemplateExpansion(), manifest);
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> trackCount = decodeScene(createSceneExceedingTrackCount(), manifest);
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> bindingCount = decodeScene(createSceneExceedingBindingCount(), manifest);
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> effectsPerNode = decodeScene(createSceneExceedingEffectsPerNode(), manifest);
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> totalEffects = decodeScene(createSceneExceedingTotalEffects(), manifest);

            Assert.Multiple(() =>
            {
                Assert.That(GameplaySkinSceneBudgets.MAX_NODES, Is.Positive);
                Assert.That(GameplaySkinSceneBudgets.MAX_EXPANDED_TEMPLATE_NODES, Is.GreaterThan(GameplaySkinSceneBudgets.MAX_NODES));
                Assert.That(GameplaySkinSceneBudgets.MAX_TRACKS, Is.Positive);
                Assert.That(GameplaySkinSceneBudgets.MAX_KEYFRAMES, Is.GreaterThan(GameplaySkinSceneBudgets.MAX_TRACKS));
                Assert.That(GameplaySkinSceneBudgets.MAX_BINDINGS, Is.Positive);
                Assert.That(GameplaySkinSceneBudgets.MAX_EFFECTS, Is.Positive);
                Assert.That(GameplaySkinSceneBudgets.MAX_INSTANCES, Is.Positive);
                assertInvalidWith(nodeCount, GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                assertInvalidWith(templateExpansion, GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                assertInvalidWith(trackCount, GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                assertInvalidWith(bindingCount, GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                assertInvalidWith(effectsPerNode, GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                assertInvalidWith(totalEffects, GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            });
        }

        [Test]
        public void TestDiagnosticsAreStableAndRedacted()
        {
            const string private_value = "C:/Users/author/private.png";
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> result = manifestWithPaths(private_value);

            Assert.Multiple(() =>
            {
                Assert.That(result.Diagnostics, Is.Not.Empty);
                Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Id),
                    Is.All.Matches<string>(id => id.StartsWith("OMS-SKIN-SCENE-", StringComparison.Ordinal)));
                Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.ToString()), Has.None.Contains(private_value));
                Assert.That(result.ToString(), Does.Not.Contain(private_value));
            });
        }

        [Test]
        public void TestPublicModelsExposeNoRuntimeAuthorityOrDynamicTypeSurface()
        {
            Type[] modelTypes =
            {
                typeof(GameplaySkinSceneDiagnostic),
                typeof(GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument>),
                typeof(GameplaySkinSceneResource),
                typeof(GameplaySkinSceneManifest),
                typeof(GameplaySkinScenePropertyValue),
                typeof(GameplaySkinSceneTarget),
                typeof(GameplaySkinSceneEffect),
                typeof(GameplaySkinSceneNode),
                typeof(GameplaySkinSceneKeyframe),
                typeof(GameplaySkinSceneTrack),
                typeof(GameplaySkinSceneState),
                typeof(GameplaySkinSceneTransition),
                typeof(GameplaySkinSceneStateMachine),
                typeof(GameplaySkinSceneBinding),
                typeof(GameplaySkinSceneTemplate),
                typeof(GameplaySkinSceneInstance),
                typeof(GameplaySkinSceneDocument),
            };
            string[] forbiddenTypeFragments = { "System.Type", "System.Reflection", "System.IO.", "System.Net.", "Drawable", "Bindable", "JToken", "JObject", "Delegate" };

            Assert.Multiple(() =>
            {
                Assert.That(modelTypes, Is.Not.Empty);
                Assert.That(modelTypes.SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                                          .Select(property => property.SetMethod), Is.All.Null);
                Assert.That(modelTypes.SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                                          .Select(property => property.PropertyType.FullName ?? string.Empty),
                    Has.None.Matches<string>(name => forbiddenTypeFragments.Any(fragment => name.Contains(fragment, StringComparison.Ordinal))));
                Assert.That(typeof(GameplaySkinSceneCodec).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                          .Select(method => method.Name),
                    Has.None.EqualTo("LoadFromFile"));
            });
        }

        [Test]
        public void TestCanonicalEncodingIsCultureIndependent()
        {
            GameplaySkinSceneManifest manifest = decodeManifest(valid_manifest).Value!;
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> result = decodeScene(valid_scene, manifest);
            Assert.That(result.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid),
                string.Join(",", result.Diagnostics.Select(diagnostic => diagnostic.Id)));
            GameplaySkinSceneDocument scene = result.Value!;
            string invariant = GameplaySkinSceneCodec.EncodeScene(scene);
            CultureInfo originalCulture = CultureInfo.CurrentCulture;

            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Assert.That(GameplaySkinSceneCodec.EncodeScene(scene), Is.EqualTo(invariant));
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        private static GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> decodeManifest(string? json)
            => GameplaySkinSceneCodec.DecodeManifest(json);

        private static GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decodeScene(string? json, GameplaySkinSceneManifest? manifest)
            => GameplaySkinSceneCodec.DecodeScene(json, manifest);

        private static void assertInvalidWith<T>(GameplaySkinSceneDecodeResult<T> result, GameplaySkinSceneDiagnosticCode code)
            where T : class
        {
            Assert.That(result.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(code));
        }

        private static GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> manifestWithPaths(params string[] paths)
        {
            var resources = new JArray(paths.Select((path, index) => new JObject
            {
                ["id"] = $"texture.resource-{index}",
                ["type"] = "texture",
                ["path"] = path,
            }));

            return decodeManifest(new JObject
            {
                ["contract"] = GameplaySkinSceneContracts.MANIFEST_CONTRACT_ID,
                ["scene"] = GameplaySkinSceneContracts.SCENE_FILE_NAME,
                ["sceneContract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
                ["eventContract"] = GameplaySkinSceneContracts.EVENT_CONTRACT_ID,
                ["resources"] = resources,
            }.ToString());
        }

        private static string createManifestWithResourceCount(int count)
        {
            var resources = new JArray(Enumerable.Range(0, count).Select(index => new JObject
            {
                ["id"] = $"texture.r-{index}",
                ["type"] = "texture",
                ["path"] = $"textures/{index}.png",
            }));

            return new JObject
            {
                ["contract"] = GameplaySkinSceneContracts.MANIFEST_CONTRACT_ID,
                ["scene"] = GameplaySkinSceneContracts.SCENE_FILE_NAME,
                ["sceneContract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
                ["eventContract"] = GameplaySkinSceneContracts.EVENT_CONTRACT_ID,
                ["resources"] = resources,
            }.ToString();
        }

        private static string createSceneWithDepth(int depth)
        {
            JObject node = simpleNode($"node.depth-{depth}");

            for (int index = depth - 1; index >= 0; index--)
            {
                JObject parent = simpleNode($"node.depth-{index}");
                parent["children"] = new JArray(node);
                node = parent;
            }

            return emptyScene(node).ToString();
        }

        private static string createSceneWithKeyframes(int count)
        {
            JObject scene = emptyScene(simpleNode("node.root"));
            scene["tracks"] = new JArray(new JObject
            {
                ["id"] = "track.opacity",
                ["type"] = "tween",
                ["target"] = "node.root",
                ["property"] = "opacity",
                ["easing"] = "linear",
                ["loop"] = false,
                ["keyframes"] = new JArray(Enumerable.Range(0, count).Select(index => new JObject
                {
                    ["id"] = $"keyframe.k-{index}",
                    ["time"] = index,
                    ["value"] = 1.0,
                })),
            });
            return scene.ToString();
        }

        private static string createSceneWithText(int length)
        {
            JObject root = simpleNode("node.root", "text");
            root["properties"]!["text"] = new string('x', length);
            return emptyScene(root).ToString();
        }

        private static string createSceneExceedingNodeCount()
        {
            JObject root = simpleNode("node.root");
            var children = new JArray();

            for (int childIndex = 0; childIndex < GameplaySkinSceneBudgets.MAX_CHILDREN_PER_NODE; childIndex++)
            {
                JObject child = simpleNode($"node.child-{childIndex}");
                child["children"] = new JArray(Enumerable.Range(0, 16)
                                                         .Select(grandchildIndex => simpleNode($"node.child-{childIndex}.grandchild-{grandchildIndex}")));
                children.Add(child);
            }

            root["children"] = children;
            return emptyScene(root).ToString(Newtonsoft.Json.Formatting.None);
        }

        private static string createSceneExceedingTemplateExpansion()
        {
            JObject scene = emptyScene(simpleNode("node.root"));
            JObject templateRoot = simpleNode("template-node.root");
            JObject cursor = templateRoot;

            for (int index = 1; index < 8; index++)
            {
                JObject child = simpleNode($"template-node.child-{index}");
                cursor["children"] = new JArray(child);
                cursor = child;
            }

            scene["templates"] = new JArray(new JObject
            {
                ["id"] = "template.expanded",
                ["root"] = templateRoot,
            });
            scene["instances"] = new JArray(Enumerable.Range(0, GameplaySkinSceneBudgets.MAX_INSTANCES)
                                                      .Select(index => new JObject
                                                      {
                                                          ["id"] = $"instance.i-{index}",
                                                          ["template"] = "template.expanded",
                                                          ["target"] = new JObject { ["kind"] = "global" },
                                                      }));
            return scene.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static string createSceneExceedingTrackCount()
        {
            JObject scene = emptyScene(simpleNode("node.root"));
            scene["tracks"] = new JArray(Enumerable.Range(0, GameplaySkinSceneBudgets.MAX_TRACKS + 1)
                                                   .Select(index => new JObject
                                                   {
                                                       ["id"] = $"track.t-{index}",
                                                       ["type"] = "tween",
                                                       ["target"] = "node.root",
                                                       ["property"] = "opacity",
                                                       ["easing"] = "linear",
                                                       ["loop"] = false,
                                                       ["keyframes"] = new JArray(new JObject
                                                       {
                                                           ["id"] = $"keyframe.t-{index}",
                                                           ["time"] = 0,
                                                           ["value"] = 1,
                                                       }),
                                                   }));
            return scene.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static string createSceneExceedingBindingCount()
        {
            JObject scene = emptyScene(simpleNode("node.root"));
            scene["bindings"] = new JArray(Enumerable.Range(0, GameplaySkinSceneBudgets.MAX_BINDINGS + 1)
                                                     .Select(index => new JObject
                                                     {
                                                         ["id"] = $"binding.b-{index}",
                                                         ["target"] = "node.root",
                                                         ["property"] = "opacity",
                                                         ["source"] = "gauge.value",
                                                     }));
            return scene.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static string createSceneExceedingEffectsPerNode()
        {
            JObject root = simpleNode("node.root");
            root["effects"] = createEffects(GameplaySkinSceneBudgets.MAX_EFFECTS_PER_NODE + 1, "root");
            return emptyScene(root).ToString(Newtonsoft.Json.Formatting.None);
        }

        private static string createSceneExceedingTotalEffects()
        {
            JObject root = simpleNode("node.root");
            int childCount = GameplaySkinSceneBudgets.MAX_EFFECTS / GameplaySkinSceneBudgets.MAX_EFFECTS_PER_NODE + 1;
            root["children"] = new JArray(Enumerable.Range(0, childCount).Select(index =>
            {
                JObject child = simpleNode($"node.effect-host-{index}");
                child["effects"] = createEffects(GameplaySkinSceneBudgets.MAX_EFFECTS_PER_NODE, $"host-{index}");
                return child;
            }));
            return emptyScene(root).ToString(Newtonsoft.Json.Formatting.None);
        }

        private static JArray createEffects(int count, string prefix) => new JArray(Enumerable.Range(0, count).Select(index => new JObject
        {
            ["id"] = $"effect.{prefix}-{index}",
            ["type"] = "blur",
            ["properties"] = new JObject { ["radius"] = 1 },
        }));

        private static JObject emptyScene(JObject root) => new JObject
        {
            ["contract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
            ["root"] = root,
            ["tracks"] = new JArray(),
            ["stateMachines"] = new JArray(),
            ["bindings"] = new JArray(),
            ["templates"] = new JArray(),
            ["instances"] = new JArray(),
        };

        private static JObject simpleNode(string id, string type = "container") => new JObject
        {
            ["id"] = id,
            ["type"] = type,
            ["target"] = new JObject { ["kind"] = "global" },
            ["blend"] = "alpha",
            ["properties"] = new JObject(),
            ["effects"] = new JArray(),
            ["children"] = new JArray(),
        };
    }
}
