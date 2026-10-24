// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public class GameplaySkinAuthorLayoutTest
    {
        [Test]
        public void TestAuthorLayoutRoundTripRetainsEmptySpansAndOutwardKeys()
        {
            JArray json = JArray.Parse("""
                                      [{"id":"layout.five","ruleset":"bms","keymode":"5k","presentation":"p1",
                                        "keyAreaHeight":0.1,"gaugeHeight":0.1,"hitPosition":1,
                                        "stages":[{"rect":[0.1,0,0.3,0.6],
                                          "lanes":[[0.01,0.2],[0.22,0.1],[0.33,0.1],[0.44,0.1],[0.55,0.1],[0.66,0.1]],
                                          "keys":[[-0.1,0.31],[0.22,0.1],[0.33,0.1],[0.44,0.1],[0.55,0.1],[0.66,0.1]]}]}]
                                      """);
            var diagnostics = new List<GameplaySkinSceneDiagnostic>();
            IReadOnlyList<GameplaySkinAuthorLayout> values = GameplaySkinAuthorLayoutCodec.Decode(json, diagnostics);
            Assert.That(diagnostics, Is.Empty);
            IReadOnlyList<GameplaySkinAuthorLayout> roundtrip = GameplaySkinAuthorLayoutCodec.Decode(GameplaySkinAuthorLayoutCodec.Encode(values), diagnostics);
            Assert.Multiple(() =>
            {
                Assert.That(diagnostics, Is.Empty);
                Assert.That(roundtrip, Has.Count.EqualTo(1));
                Assert.That(roundtrip[0].Stages[0].Lanes, Is.EqualTo(values[0].Stages[0].Lanes));
                Assert.That(roundtrip[0].Stages[0].Keys, Is.EqualTo(values[0].Stages[0].Keys));
                Assert.That(roundtrip[0].Stages[0].Keys[0].X, Is.LessThan(0));
                Assert.That(roundtrip[0].Stages[0].Lanes, Has.Count.EqualTo(6));
                Assert.That(roundtrip[0].Stages[0].Lanes.Max(s => s.X + s.Width), Is.LessThan(1));
                Assert.That(roundtrip[0].HitPosition, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestGaugeBandIncludesItsActualGapInSafeBoundaryValidation()
        {
            var stage = new GameplaySkinAuthorLayoutStage(GameplaySkinLayoutRect.Create(0.1f, 0, 0.2f, 0.8f),
                new[] { new GameplaySkinAuthorLaneSpan(0, 1) });
            Assert.That(() => new GameplaySkinAuthorLayout("edge", "mania", "1k", "any", new[] { stage }, 0.1, 0.1),
                Throws.ArgumentException);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestOptionalModsRectangleRoundTripPreservesLegacyOmission(bool declared)
        {
            var profile = new JObject
            {
                ["id"] = "mods",
                ["ruleset"] = "bms",
                ["keymode"] = "5k",
                ["presentation"] = "p1",
                ["stages"] = JArray.Parse("""[{"rect":[0.1,0,0.2,0.6],"lanes":[[0,1]]}]"""),
            };
            if (declared)
                profile["modsRect"] = new JArray(0.05, 0.1, 0.25, 0.08);
            var diagnostics = new List<GameplaySkinSceneDiagnostic>();
            GameplaySkinAuthorLayout decoded = GameplaySkinAuthorLayoutCodec.Decode(new JArray(profile), diagnostics).Single();
            JArray encoded = GameplaySkinAuthorLayoutCodec.Encode(new[] { decoded });
            GameplaySkinAuthorLayout roundtrip = GameplaySkinAuthorLayoutCodec.Decode(encoded, diagnostics).Single();
            Assert.Multiple(() =>
            {
                Assert.That(diagnostics, Is.Empty);
                Assert.That(roundtrip.ModsRect, Is.EqualTo(decoded.ModsRect));
                Assert.That(encoded[0]!["modsRect"] != null, Is.EqualTo(declared));
                Assert.That(roundtrip.ModsRect.HasValue, Is.EqualTo(declared));
            });
            if (declared)
                Assert.That(roundtrip.ModsRect, Is.EqualTo(GameplaySkinLayoutRect.Create(0.05f, 0.1f, 0.25f, 0.08f)));
        }

        [TestCase("[-0.01,0,0.2,0.1]")]
        [TestCase("[0,-0.01,0.2,0.1]")]
        [TestCase("[0,0,0,0.1]")]
        [TestCase("[0,0,0.2,-0.1]")]
        [TestCase("[0.9,0,0.2,0.1]")]
        [TestCase("[0,0.9,0.2,0.2]")]
        [TestCase("[0,0,1e100,0.1]")]
        [TestCase("[0,0,0.2]")]
        [TestCase("null")]
        [TestCase("\"invalid\"")]
        [TestCase("nan")]
        [TestCase("infinity")]
        public void TestInvalidModsRectangleRejectsTheProfileAtTheManifestBoundary(string value)
        {
            var profile = new JObject
            {
                ["id"] = "mods",
                ["ruleset"] = "mania",
                ["keymode"] = "1k",
                ["presentation"] = "any",
                ["stages"] = JArray.Parse("""[{"rect":[0.1,0,0.2,0.6],"lanes":[[0,1]]}]"""),
                ["modsRect"] = value switch
                {
                    "nan" => new JArray(double.NaN, 0, 0.2, 0.1),
                    "infinity" => new JArray(0, 0, double.PositiveInfinity, 0.1),
                    _ => JToken.Parse(value),
                },
            };
            var diagnostics = new List<GameplaySkinSceneDiagnostic>();
            Assert.That(GameplaySkinAuthorLayoutCodec.Decode(new JArray(profile), diagnostics), Is.Empty);
            Assert.That(diagnostics, Is.Not.Empty);
        }

        [Test]
        public void TestExactPresentationWinsOverAnyAndNativeKeymodeNeverAliasesByLaneCount()
        {
            var stage = new GameplaySkinAuthorLayoutStage(GameplaySkinLayoutRect.Create(0.1f, 0, 0.2f, 0.6f),
                new[] { new GameplaySkinAuthorLaneSpan(0, 1) });
            var any = new GameplaySkinAuthorLayout("any", "mania", "7k-7k", "any", new[] { stage });
            var exact = new GameplaySkinAuthorLayout("exact", "mania", "7k-7k", "p1", new[] { stage });
            Assert.Multiple(() =>
            {
                Assert.That(GameplaySkinAuthorLayout.Select(new[] { any, exact }, "mania", "7k-7k", "p1"), Is.SameAs(exact));
                Assert.That(GameplaySkinAuthorLayout.Select(new[] { any, exact }, "mania", "7k-7k", "mania-dual"), Is.SameAs(any));
                Assert.That(GameplaySkinAuthorLayout.Select(new[] { any, exact }, "bms", "14k", "center-p1"), Is.Null);
                Assert.That(GameplaySkinAuthorLayout.Select(new[] { any, exact }, "mania", "14k", "mania-single"), Is.Null);
                Assert.That(GameplaySkinAuthorLayoutCodec.Decode(null, new List<GameplaySkinSceneDiagnostic>()), Is.Empty);
            });
        }

        [TestCase("overlap")]
        [TestCase("offscreen-key")]
        [TestCase("unknown")]
        [TestCase("duplicate")]
        [TestCase("budget")]
        public void TestMalformedOrOverBudgetLayoutIsReportedAtTheManifestBoundary(string reason)
        {
            var item = new JObject
            {
                ["id"] = "layout",
                ["ruleset"] = "mania",
                ["keymode"] = "2k",
                ["presentation"] = "any",
                ["stages"] = JArray.Parse("""[{"rect":[0.1,0,0.2,0.6],"lanes":[[0,0.4],[0.5,0.4]]}]"""),
            };
            var array = new JArray(item);
            switch (reason)
            {
                case "overlap": item["stages"]![0]!["lanes"] = JArray.Parse("[[0,0.8],[0.5,0.4]]"); break;
                case "offscreen-key": item["stages"]![0]!["keys"] = JArray.Parse("[[-1,0.4],[0.5,0.4]]"); break;
                case "unknown": item["exec"] = "invalid"; break;
                case "duplicate": array.Add(item.DeepClone()); break;
                case "budget": while (array.Count <= GameplaySkinAuthorLayout.MAX_PROFILES) array.Add(item.DeepClone()); break;
            }
            var diagnostics = new List<GameplaySkinSceneDiagnostic>();
            GameplaySkinAuthorLayoutCodec.Decode(array, diagnostics);
            Assert.That(diagnostics, Is.Not.Empty);
            if (reason == "budget")
                Assert.That(diagnostics.Any(d => d.Code == GameplaySkinSceneDiagnosticCode.BudgetExceeded), Is.True);
        }
    }
}
