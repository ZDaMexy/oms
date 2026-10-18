// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Game.Screens.Play;
using osu.Game.Skinning.Gameplay;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinAuxiliaryDisplayTest
    {
        [Test]
        public void TestExplicitAdmissionRoundTripsAndAbsentAdmissionKeepsTheOrdinaryManifest()
        {
            JObject json = manifestJson();
            GameplaySkinSceneManifest manifest = decode(json);
            GameplaySkinAuxiliaryDisplayDefinition definition = manifest.AuxiliaryDisplay!;
            Assert.That(definition.EnabledOption, Is.EqualTo("auxiliary-display"));
            Assert.That(definition.ConcentrationOption, Is.EqualTo("concentration"));
            Assert.That(definition.BrightnessOption, Is.EqualTo("concentration-brightness"));
            string encoded = GameplaySkinSceneCodec.EncodeManifest(manifest);
            Assert.That(GameplaySkinSceneCodec.EncodeManifest(decode(JObject.Parse(encoded))), Is.EqualTo(encoded));
            Assert.That(JToken.DeepEquals(json["auxiliaryDisplay"], JObject.Parse(encoded)["auxiliaryDisplay"]), Is.True);

            json.Remove("auxiliaryDisplay");
            GameplaySkinSceneManifest ordinary = decode(json);
            Assert.That(ordinary.AuxiliaryDisplay, Is.Null);
            Assert.That(JObject.Parse(GameplaySkinSceneCodec.EncodeManifest(ordinary)).ContainsKey("auxiliaryDisplay"), Is.False);
            json.Remove("options");
            GameplaySkinSceneManifest legacy = decode(json);
            Assert.That(legacy.AuxiliaryDisplay, Is.Null);
            Assert.That(JObject.Parse(GameplaySkinSceneCodec.EncodeManifest(legacy)).ContainsKey("auxiliaryDisplay"), Is.False);
        }

        [TestCase("null", GameplaySkinSceneDiagnosticCode.InvalidValueType)]
        [TestCase("array", GameplaySkinSceneDiagnosticCode.InvalidValueType)]
        [TestCase("missing-reference", GameplaySkinSceneDiagnosticCode.MissingField)]
        [TestCase("unknown-field", GameplaySkinSceneDiagnosticCode.UnknownField)]
        [TestCase("wrong-reference-type", GameplaySkinSceneDiagnosticCode.InvalidValueType)]
        [TestCase("invalid-id", GameplaySkinSceneDiagnosticCode.InvalidStableId)]
        [TestCase("unknown-option", GameplaySkinSceneDiagnosticCode.InvalidReference)]
        [TestCase("enabled-choice", GameplaySkinSceneDiagnosticCode.InvalidReference)]
        [TestCase("concentration-number", GameplaySkinSceneDiagnosticCode.InvalidReference)]
        [TestCase("brightness-toggle", GameplaySkinSceneDiagnosticCode.InvalidReference)]
        [TestCase("negative-brightness", GameplaySkinSceneDiagnosticCode.InvalidReference)]
        [TestCase("excessive-brightness", GameplaySkinSceneDiagnosticCode.InvalidReference)]
        [TestCase("different-rulesets", GameplaySkinSceneDiagnosticCode.InvalidReference)]
        [TestCase("different-keymodes", GameplaySkinSceneDiagnosticCode.InvalidReference)]
        public void TestInvalidAuxiliaryControlsAreRejectedAtTheManifestBoundary(string change, GameplaySkinSceneDiagnosticCode expected)
        {
            JObject json = manifestJson();
            decode(json);
            var declaration = (JObject)json["auxiliaryDisplay"]!;
            var options = (JArray)json["options"]!;
            switch (change)
            {
                case "null": json["auxiliaryDisplay"] = JValue.CreateNull(); break;
                case "array": json["auxiliaryDisplay"] = new JArray(); break;
                case "missing-reference": declaration.Remove("enabledOption"); break;
                case "unknown-field": declaration["mainArea"] = true; break;
                case "wrong-reference-type": declaration["enabledOption"] = false; break;
                case "invalid-id": declaration["enabledOption"] = "../auxiliary-display"; break;
                case "unknown-option": declaration["enabledOption"] = "not-declared"; break;
                case "enabled-choice":
                    options[0]!["type"] = "choice";
                    options[0]!["default"] = "closed";
                    options[0]!["choices"] = new JArray { new JObject { ["value"] = "closed", ["label"] = "Closed" } };
                    break;
                case "concentration-number":
                    options[1]!["type"] = "number";
                    options[1]!["default"] = 0;
                    options[1]!["min"] = 0;
                    options[1]!["max"] = 1;
                    options[1]!["step"] = 1;
                    break;
                case "brightness-toggle":
                    options[2]!["type"] = "toggle";
                    options[2]!["default"] = true;
                    ((JObject)options[2]!).Remove("min");
                    ((JObject)options[2]!).Remove("max");
                    ((JObject)options[2]!).Remove("step");
                    break;
                case "negative-brightness": options[2]!["min"] = -1; break;
                case "excessive-brightness": options[2]!["max"] = 2; break;
                case "different-rulesets": options[1]!["rulesets"] = new JArray("bms"); break;
                case "different-keymodes": options[1]!["keymodes"] = new JArray("7k"); break;
                default: throw new ArgumentOutOfRangeException(nameof(change));
            }
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> result = GameplaySkinSceneCodec.DecodeManifest(json.ToString(Formatting.None));
            Assert.That(result.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
            Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(expected));
        }

        [Test]
        public void TestThePanelUsesFrozenValidatedChoicesWithoutObservingLaterPreferences()
        {
            GameplaySkinSceneManifest manifest = decode(manifestJson());
            var preferences = new Dictionary<string, string>
            {
                ["auxiliary-display"] = "true",
                ["concentration"] = "true",
                ["concentration-brightness"] = "0.25",
            };
            GameplaySkinOptionValues first = GameplaySkinOptionValues.Resolve(manifest.Options, preferences, "bms", "7k");
            preferences["auxiliary-display"] = "false";
            preferences["concentration"] = "false";
            preferences["concentration-brightness"] = "1";
            Assert.That(manifest.AuxiliaryDisplay!.TryResolve(first, out GameplaySkinAuxiliaryDisplaySettings firstSettings), Is.True);
            Assert.That(firstSettings, Is.EqualTo(new GameplaySkinAuxiliaryDisplaySettings(true, true, 0.25f)));
            GameplaySkinOptionValues next = GameplaySkinOptionValues.Resolve(manifest.Options, preferences, "bms", "7k");
            Assert.That(manifest.AuxiliaryDisplay.TryResolve(next, out GameplaySkinAuxiliaryDisplaySettings nextSettings), Is.True);
            Assert.That(nextSettings, Is.EqualTo(new GameplaySkinAuxiliaryDisplaySettings(false, false, 1)));

            preferences["auxiliary-display"] = "invalid";
            preferences["concentration-brightness"] = "2";
            GameplaySkinOptionValues invalid = GameplaySkinOptionValues.Resolve(manifest.Options, preferences, "bms", "7k");
            Assert.That(manifest.AuxiliaryDisplay.TryResolve(invalid, out GameplaySkinAuxiliaryDisplaySettings fallback), Is.True);
            Assert.That(fallback.InitiallyOpen, Is.False);
            Assert.That(fallback.Brightness, Is.EqualTo(0.5f));
        }

        [TestCase("bms", "5k", true)]
        [TestCase("bms", "7k", true)]
        [TestCase("bms", "14k", false)]
        [TestCase("mania", "7k", false)]
        public void TestScopesAdmitAllThreeControlsTogetherWithoutCrossModeInheritance(string ruleset, string keymode, bool admitted)
        {
            JObject json = manifestJson();
            foreach (JToken option in (JArray)json["options"]!)
            {
                option["rulesets"] = new JArray("bms");
                option["keymodes"] = new JArray("5k", "7k");
            }
            // Scope order is not semantic.
            json["options"]![1]!["keymodes"] = new JArray("7k", "5k");
            GameplaySkinSceneManifest manifest = decode(json);
            GameplaySkinOptionValues values = GameplaySkinOptionValues.Resolve(manifest.Options,
                new Dictionary<string, string> { ["auxiliary-display"] = "true" }, ruleset, keymode);
            Assert.That(manifest.AuxiliaryDisplay!.TryResolve(values, out _), Is.EqualTo(admitted));
            Assert.That(values.Values, Has.Count.EqualTo(admitted ? 3 : 0));
        }

        [Test]
        public void TestTouchRestorePersistsThroughPauseBreakAndResumeButNextSessionResetsIt()
        {
            var state = new GameplaySkinAuxiliaryPresentationState(new GameplaySkinAuxiliaryDisplaySettings(false, true, 0.25f));
            state.SetPlaying(true);
            Assert.That(state.IsConcentrating, Is.False, "Loading a Player does not start a song.");
            state.StartSession();
            Assert.That(state.IsConcentrating, Is.True);
            Assert.That(state.IsOpen, Is.False);
            state.ToggleOpen();
            Assert.That(state.IsOpen, Is.True);
            state.SetPlaying(false);
            Assert.That(state.IsConcentrating, Is.False);
            state.SetPlaying(true);
            Assert.That(state.IsConcentrating, Is.True);
            state.RestoreNormalForSession();
            Assert.That(state.IsConcentrating, Is.False);
            state.SetPlaying(false);
            state.SetPlaying(true);
            Assert.That(state.IsConcentrating, Is.False, "Resume must preserve this song's touch restoration.");
            state.ToggleOpen();
            state.ToggleOpen();
            Assert.That(state.IsConcentrating, Is.False, "Opening the viewer is not a new gameplay session.");
            state.StartSession();
            Assert.That(state.IsConcentrating, Is.True, "The next song or retry resets touch restoration.");
            Assert.That(state.IsOpen, Is.True);
            Assert.That(state.Brightness, Is.EqualTo(0.25f));
        }

        [Test]
        public void TestTheViewerSwitchesOnlyItsOwnWrappersAndPreservesSourceVisibilityAndColour()
        {
            var normal = new Container { Alpha = 0.4f, Colour = Color4.Red };
            var concentration = new Container { Alpha = 0.7f, Colour = Color4.Blue };
            var normalColour = normal.Colour;
            var concentrationColour = concentration.Colour;
            using var panel = new GameplaySkinAuxiliaryPanel(new GameplaySkinAuxiliaryDisplaySettings(false, true, 0.25f), normal, concentration);
            Assert.That(panel.NormalMount.Child, Is.SameAs(normal));
            Assert.That(panel.ConcentrationMount.Child, Is.SameAs(concentration));
            Assert.That(panel.Canvas.Size, Is.EqualTo(new Vector2(768 * 16f / 9, 768)));
            Assert.That(panel.Canvas.Scale.X, Is.EqualTo(panel.Width / GameplaySkinAuxiliaryPanel.LOGICAL_WIDTH));
            Assert.That(panel.Viewport.Height, Is.EqualTo(panel.Width * 9 / 16));
            Assert.That(panel.Viewport.Alpha, Is.Zero);
            Assert.That(panel.Height, Is.EqualTo(32));
            Assert.That(panel.PropagateNonPositionalInputSubTree, Is.False);

            panel.SetPlaying(true);
            panel.StartSession();
            panel.ToggleOpen();
            Assert.That(panel.Viewport.Alpha, Is.EqualTo(1));
            Assert.That(panel.Height, Is.EqualTo(38 + panel.Width * 9 / 16));
            Assert.That(panel.NormalMount.Alpha, Is.Zero);
            Assert.That(panel.ConcentrationMount.Alpha, Is.EqualTo(1));
            var dimmed = panel.ConcentrationMount.Colour;
            panel.RestoreNormalForSession();
            Assert.That(panel.NormalMount.Alpha, Is.EqualTo(1));
            Assert.That(panel.ConcentrationMount.Alpha, Is.Zero);
            Assert.That(panel.ConcentrationMount.Colour, Is.EqualTo(dimmed));
            Assert.That(normal.Alpha, Is.EqualTo(0.4f));
            Assert.That(concentration.Alpha, Is.EqualTo(0.7f));
            Assert.That(normal.Colour, Is.EqualTo(normalColour));
            Assert.That(concentration.Colour, Is.EqualTo(concentrationColour));
        }

        [Test]
        public void TestAViewerWithoutConcentrationAlwaysShowsTheNormalOutput()
        {
            using var panel = new GameplaySkinAuxiliaryPanel(new GameplaySkinAuxiliaryDisplaySettings(true, false, 0), new Container(), new Container());
            panel.SetPlaying(true);
            panel.StartSession();
            Assert.That(panel.Presentation.IsConcentrating, Is.False);
            Assert.That(panel.NormalMount.Alpha, Is.EqualTo(1));
            Assert.That(panel.ConcentrationMount.Alpha, Is.Zero);
        }

        private static GameplaySkinSceneManifest decode(JObject json)
        {
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> result = GameplaySkinSceneCodec.DecodeManifest(json.ToString(Formatting.None));
            Assert.That(result.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid), string.Join(",", result.Diagnostics.Select(diagnostic => diagnostic.Code)));
            return result.Value!;
        }

        private static JObject manifestJson() => JObject.Parse("""
            {"contract":"oms-gameplay-skin-manifest.v1","scene":"gameplay-skin.scene.json",
             "sceneContract":"oms-gameplay-skin-scene.v1","eventContract":"oms-gameplay-skin-event.v1","resources":[],
             "options":[
               {"id":"auxiliary-display","label":"Auxiliary","type":"toggle","default":false},
               {"id":"concentration","label":"Concentration","type":"toggle","default":true},
               {"id":"concentration-brightness","label":"Brightness","type":"number","default":0.5,"min":0,"max":1,"step":0.25}],
             "auxiliaryDisplay":{"enabledOption":"auxiliary-display","concentrationOption":"concentration","brightnessOption":"concentration-brightness"}}
            """);
    }
}
