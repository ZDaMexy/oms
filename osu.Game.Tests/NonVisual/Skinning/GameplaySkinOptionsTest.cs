// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinOptionsTest
    {
        internal static JObject ManifestJson() => JObject.Parse("""
            {"contract":"oms-gameplay-skin-manifest.v1","scene":"gameplay-skin.scene.json",
             "sceneContract":"oms-gameplay-skin-scene.v1","eventContract":"oms-gameplay-skin-event.v1","resources":[
               {"id":"texture.normal","type":"texture","path":"normal.png"},
               {"id":"texture.alternate","type":"texture","path":"alternate.png"}],
             "options":[
               {"id":"layout","label":"布局","type":"choice","default":"p1","effect":"presentation","rulesets":["bms"],"keymodes":["7k"],
                "choices":[{"value":"p1","label":"1P"},{"value":"p2","label":"2P"}]},
               {"id":"notes","label":"音符","type":"choice","default":"normal",
                "choices":[{"value":"normal","label":"原版"},{"value":"alternate","label":"另一套"}]},
               {"id":"bomb","label":"击打效果","type":"toggle","default":true},
               {"id":"cover","label":"挡板","type":"number","default":0.25,"min":0,"max":1,"step":0.05}]}
            """);

        internal static GameplaySkinSceneManifest DecodeManifest(JObject? json = null)
        {
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> decoded = GameplaySkinSceneCodec.DecodeManifest((json ?? ManifestJson()).ToString(Formatting.None));
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid), string.Join(",", decoded.Diagnostics.Select(d => d.Code)));
            return decoded.Value!;
        }

        [Test]
        public void TestTypedDefaultsCanonicalOverridesAndImmutableRootChoices()
        {
            GameplaySkinSceneManifest manifest = DecodeManifest();
            var saved = new Dictionary<string, string>
            {
                ["layout"] = "p2",
                ["notes"] = "alternate",
                ["bomb"] = "false",
                ["cover"] = "0.5000000000000001",
                ["unrecognised"] = "ignored",
            };
            GameplaySkinOptionValues first = GameplaySkinOptionValues.Resolve(manifest.Options, saved, "bms", "7k");
            saved["layout"] = "p1";
            saved["notes"] = "normal";
            saved["cover"] = "0.75";
            GameplaySkinOptionValues second = GameplaySkinOptionValues.Resolve(manifest.Options, saved, "bms", "7k");
            Assert.Multiple(() =>
            {
                Assert.That(first.Presentation, Is.EqualTo("p2"));
                Assert.That(first.Values["notes"].StringValue, Is.EqualTo("alternate"));
                Assert.That(first.Values["bomb"].BooleanValue, Is.False);
                Assert.That(first.Values["cover"].NumberValue, Is.EqualTo(0.5));
                Assert.That(first.Values["cover"].CanonicalValue, Is.EqualTo("0.5"));
                Assert.That(first.Values, Has.Count.EqualTo(4));
                Assert.That(second.Presentation, Is.EqualTo("p1"));
                Assert.That(second.Values["cover"].NumberValue, Is.EqualTo(0.75));
                Assert.Throws<NotSupportedException>(() => ((IDictionary<string, GameplaySkinOptionValue>)first.Values).Clear());
            });
        }

        [TestCase("layout", "p3", "p1")]
        [TestCase("notes", "ALTERNATE", "normal")]
        [TestCase("bomb", "True", "true")]
        [TestCase("cover", "NaN", "0.25")]
        [TestCase("cover", "Infinity", "0.25")]
        [TestCase("cover", "1.05", "0.25")]
        [TestCase("cover", "0.26", "0.25")]
        [TestCase("cover", "0,5", "0.25")]
        public void TestInvalidPersistedValueUsesThisAuthorDefault(string id, string saved, string expected)
        {
            GameplaySkinSceneManifest manifest = DecodeManifest();
            GameplaySkinOptionValues values = GameplaySkinOptionValues.Resolve(manifest.Options, new Dictionary<string, string> { [id] = saved }, "bms", "7k");
            Assert.That(values.Values[id].CanonicalValue, Is.EqualTo(expected));
        }

        [TestCase("bms", "5k")]
        [TestCase("mania", "7k")]
        [TestCase("mania", "6k")]
        public void TestScopeDoesNotCreatePresentationOrReadInapplicableSavedValue(string ruleset, string keymode)
        {
            GameplaySkinOptionValues values = GameplaySkinOptionValues.Resolve(DecodeManifest().Options,
                new Dictionary<string, string> { ["layout"] = "p2" }, ruleset, keymode);
            Assert.That(values.Presentation, Is.Null);
            Assert.That(values.Values.ContainsKey("layout"), Is.False);
            Assert.That(values.Values["notes"].CanonicalValue, Is.EqualTo("normal"));
        }

        [Test]
        public void TestManifestRoundTripRetainsTypedChoicesAndOldManifestOmitsNewFields()
        {
            GameplaySkinSceneManifest manifest = DecodeManifest();
            string encoded = GameplaySkinSceneCodec.EncodeManifest(manifest);
            GameplaySkinSceneManifest roundTrip = DecodeManifest(JObject.Parse(encoded));
            Assert.That(GameplaySkinSceneCodec.EncodeManifest(roundTrip), Is.EqualTo(encoded));
            Assert.That(roundTrip.Options[2].DefaultValue, Is.EqualTo("true"));
            Assert.That(JObject.Parse(encoded)["options"]![2]!["default"]!.Type, Is.EqualTo(JTokenType.Boolean));
            Assert.That(JObject.Parse(encoded)["options"]![3]!["default"]!.Type, Is.EqualTo(JTokenType.Float));

            JObject legacy = ManifestJson();
            legacy.Remove("options");
            GameplaySkinSceneManifest old = DecodeManifest(legacy);
            Assert.That(old.Options, Is.Empty);
            Assert.That(old.AuthorLayouts, Is.Empty);
            Assert.That(GameplaySkinOptionValues.Resolve(old.Options, new Dictionary<string, string> { ["layout"] = "p2" }, "bms", "7k"), Is.SameAs(GameplaySkinOptionValues.Empty));
            JObject oldEncoded = JObject.Parse(GameplaySkinSceneCodec.EncodeManifest(old));
            Assert.That(oldEncoded.ContainsKey("options") || oldEncoded.ContainsKey("layouts"), Is.False);
            Assert.That(JToken.DeepEquals(legacy, oldEncoded), Is.True);
        }

        [TestCase("missing-default", GameplaySkinSceneDiagnosticCode.MissingField)]
        [TestCase("unknown-field", GameplaySkinSceneDiagnosticCode.UnknownField)]
        [TestCase("unknown-kind", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("duplicate-id", GameplaySkinSceneDiagnosticCode.DuplicateStableId)]
        [TestCase("duplicate-choice", GameplaySkinSceneDiagnosticCode.DuplicateField)]
        [TestCase("invalid-id", GameplaySkinSceneDiagnosticCode.InvalidStableId)]
        [TestCase("empty-label", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("large-label", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("toggle-string", GameplaySkinSceneDiagnosticCode.InvalidValueType)]
        [TestCase("number-string", GameplaySkinSceneDiagnosticCode.InvalidValueType)]
        [TestCase("unknown-default", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("off-step-default", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("off-step-maximum", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("zero-step", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("inverted-range", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("large-range", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("too-many-steps", GameplaySkinSceneDiagnosticCode.BudgetExceeded)]
        [TestCase("unknown-effect", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("number-presentation", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("unknown-presentation", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("overlapping-presentation", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("empty-rulesets", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("unknown-ruleset", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("duplicate-keymode", GameplaySkinSceneDiagnosticCode.DuplicateField)]
        [TestCase("invalid-keymode", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("too-many-keymodes", GameplaySkinSceneDiagnosticCode.BudgetExceeded)]
        [TestCase("too-many-options", GameplaySkinSceneDiagnosticCode.BudgetExceeded)]
        [TestCase("too-many-choices", GameplaySkinSceneDiagnosticCode.BudgetExceeded)]
        public void TestInvalidAuthorOptionsFailAtTheManifestBoundary(string change, GameplaySkinSceneDiagnosticCode expected)
        {
            JObject json = ManifestJson();
            var options = (JArray)json["options"]!;
            var layout = (JObject)options[0]!;
            var notes = (JObject)options[1]!;
            var number = (JObject)options[3]!;
            switch (change)
            {
                case "missing-default": notes.Remove("default"); break;
                case "unknown-field": notes["path"] = "external.json"; break;
                case "unknown-kind": notes["type"] = "script"; break;
                case "duplicate-id": notes["id"] = "layout"; break;
                case "duplicate-choice": ((JArray)notes["choices"]!).Add(notes["choices"]![0]!.DeepClone()); break;
                case "invalid-id": notes["id"] = "../notes"; break;
                case "empty-label": notes["label"] = " "; break;
                case "large-label": notes["label"] = new string('a', GameplaySkinOptionBudgets.MAX_LABEL_LENGTH + 1); break;
                case "toggle-string": options[2]!["default"] = "true"; break;
                case "number-string": number["default"] = "0.25"; break;
                case "unknown-default": notes["default"] = "absent"; break;
                case "off-step-default": number["default"] = 0.26; break;
                case "off-step-maximum": number["max"] = 1.01; break;
                case "zero-step": number["step"] = 0; break;
                case "inverted-range": number["max"] = -1; break;
                case "large-range": number["max"] = GameplaySkinOptionBudgets.MAX_ABSOLUTE_NUMBER + 1; break;
                case "too-many-steps": number["step"] = 0.00001; break;
                case "unknown-effect": notes["effect"] = "change-input"; break;
                case "number-presentation": number["effect"] = "presentation"; break;
                case "unknown-presentation": layout["choices"]![0]!["value"] = "mirror-input"; break;
                case "overlapping-presentation": var other = (JObject)layout.DeepClone(); other["id"] = "another-layout"; options.Add(other); break;
                case "empty-rulesets": notes["rulesets"] = new JArray(); break;
                case "unknown-ruleset": notes["rulesets"] = new JArray("osu"); break;
                case "duplicate-keymode": notes["keymodes"] = new JArray("7k", "7k"); break;
                case "invalid-keymode": notes["keymodes"] = new JArray("7k+7k"); break;
                case "too-many-keymodes": notes["keymodes"] = new JArray(Enumerable.Range(1, GameplaySkinOptionBudgets.MAX_APPLICABILITY_TOKENS + 1).Select(value => value + "k")); break;
                case "too-many-options":
                    for (int i = 0; options.Count <= GameplaySkinOptionBudgets.MAX_OPTIONS; i++)
                    {
                        var extra = (JObject)notes.DeepClone(); extra["id"] = "extra.n" + i; options.Add(extra);
                    }
                    break;
                case "too-many-choices":
                    for (int i = 0; ((JArray)notes["choices"]!).Count <= GameplaySkinOptionBudgets.MAX_CHOICES; i++)
                        ((JArray)notes["choices"]!).Add(new JObject { ["value"] = "n" + i, ["label"] = "choice" });
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(change));
            }
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> result = GameplaySkinSceneCodec.DecodeManifest(json.ToString(Formatting.None));
            Assert.That(result.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
            Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(expected));
        }

        [Test]
        public void TestPresentationScopesMayBeDisjointAndNumbersAreCultureIndependent()
        {
            JObject json = ManifestJson();
            var other = (JObject)json["options"]![0]!.DeepClone();
            other["id"] = "mania-layout";
            other["rulesets"] = new JArray("mania");
            ((JArray)json["options"]!).Add(other);
            GameplaySkinSceneManifest manifest = DecodeManifest(json);
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Assert.That(manifest.Options[3].TryValidateValue("0.5", out GameplaySkinOptionValue value), Is.True);
                Assert.That(value.CanonicalValue, Is.EqualTo("0.5"));
                Assert.That(manifest.Options[3].TryValidateValue("0,5", out _), Is.False);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Test]
        public void TestAuthorBudgetEndpointsAndLongTokensAreUsableAndImmutable()
        {
            JObject json = ManifestJson();
            var options = (JArray)json["options"]!;
            JToken choice = options[1]!;
            string longId = new string('a', GameplaySkinSceneBudgets.MAX_STABLE_ID_LENGTH);
            string longValue = new string('v', GameplaySkinSceneBudgets.MAX_STABLE_ID_LENGTH);
            choice["id"] = longId;
            choice["label"] = new string('测', GameplaySkinOptionBudgets.MAX_LABEL_LENGTH);
            choice["default"] = longValue;
            choice["choices"] = new JArray(Enumerable.Range(0, GameplaySkinOptionBudgets.MAX_CHOICES).Select(index => new JObject
            {
                ["value"] = index == 0 ? longValue : "n" + index,
                ["label"] = "choice",
            }));
            choice["keymodes"] = new JArray(Enumerable.Range(1, GameplaySkinOptionBudgets.MAX_APPLICABILITY_TOKENS).Select(value => value + "k"));
            options[3]!["min"] = -GameplaySkinOptionBudgets.MAX_ABSOLUTE_NUMBER;
            options[3]!["max"] = GameplaySkinOptionBudgets.MAX_ABSOLUTE_NUMBER;
            options[3]!["step"] = 2 * GameplaySkinOptionBudgets.MAX_ABSOLUTE_NUMBER / GameplaySkinOptionBudgets.MAX_NUMBER_STEPS;
            options[3]!["default"] = 0;
            while (options.Count < GameplaySkinOptionBudgets.MAX_OPTIONS)
                options.Add(new JObject { ["id"] = "extra.n" + options.Count, ["label"] = "extra", ["type"] = "toggle", ["default"] = false });
            GameplaySkinSceneManifest manifest = DecodeManifest(json);
            GameplaySkinOptionValues values = GameplaySkinOptionValues.Resolve(manifest.Options, null, "mania", "6k");
            Assert.That(values.Values[longId].CanonicalValue, Is.EqualTo(longValue));
            Assert.That(manifest.Options[3].TryValidateValue("1000000", out GameplaySkinOptionValue maximum), Is.True);
            Assert.That(maximum.NumberValue, Is.EqualTo(GameplaySkinOptionBudgets.MAX_ABSOLUTE_NUMBER));
            Assert.That(manifest.Options[3].TryValidateValue("-1000000", out GameplaySkinOptionValue minimum), Is.True);
            Assert.That(minimum.NumberValue, Is.EqualTo(-GameplaySkinOptionBudgets.MAX_ABSOLUTE_NUMBER));
            Assert.That(manifest.Options, Has.Count.EqualTo(GameplaySkinOptionBudgets.MAX_OPTIONS));

            choice["choices"]![0]!["label"] = "changed raw JSON";
            Assert.That(manifest.Options[1].Choices[0].Label, Is.EqualTo("choice"));
            Assert.Throws<NotSupportedException>(() => ((IList<GameplaySkinOptionDefinition>)manifest.Options).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<GameplaySkinOptionChoice>)manifest.Options[1].Choices).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<string>)manifest.Options[1].Keymodes).Clear());
            Assert.That(GameplaySkinSceneCodec.EncodeManifest(DecodeManifest(JObject.Parse(GameplaySkinSceneCodec.EncodeManifest(manifest)))),
                Is.EqualTo(GameplaySkinSceneCodec.EncodeManifest(manifest)));
        }
    }
}
