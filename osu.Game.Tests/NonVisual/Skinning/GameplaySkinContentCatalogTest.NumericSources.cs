// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.NonVisual.Skinning
{
    public sealed partial class GameplaySkinContentCatalogTest
    {
        [Test]
        public void TestSelectedContentRetainsOnlyItsOwnNumericSourcesAndBindings()
        {
            var fixture = twoChoicePackage();
            foreach (string branch in new[] { "first", "second" })
                fixture.EditJson(branch + "/scene.json", scene =>
                {
                    scene["numericSources"] = declarations(branch, 1);
                    scene["bindings"]![0]!["source"] = "value." + branch + ".n0";
                });
            GameplaySkinPreparedPackage captured = fixture.Capture();
            foreach (string branch in new[] { "first", "second" })
            {
                GameplaySkinOptionValues values = GameplaySkinOptionValues.Resolve(captured.Manifest!.Options,
                    new System.Collections.Generic.Dictionary<string, string> { ["frame"] = branch }, "bms", "7k");
                GameplaySkinPreparedPackage selected = captured.Select(values, CancellationToken.None);
                Assert.That(selected.Document!.NumericSources.Select(source => source.Id), Is.EqualTo(new[] { branch + ".n0" }));
                Assert.That(selected.Document.Bindings.Single().SourceId, Is.EqualTo("value." + branch + ".n0"));
            }
        }

        [Test]
        public void TestIndependentSelectedModulesCannotExceedCombinedNumericBudget()
        {
            var fixture = new Package();
            foreach (string branch in new[] { "first", "second" })
            {
                fixture.AddBranch(branch, new Rgba32(255, 255, 255), null);
                fixture.SetModule(branch, new[] { fixture.Choice("selected", branch) }, "selected");
                fixture.EditJson(branch + "/scene.json", scene => scene["numericSources"] = declarations(branch, 33));
            }
            GameplaySkinPreparedPackage captured = fixture.Capture();
            GameplaySkinScenePreparationException? failure = Assert.Throws<GameplaySkinScenePreparationException>(() =>
                captured.Select(resolve(captured, "bms"), CancellationToken.None));
            Assert.That(failure!.Code, Is.EqualTo(GameplaySkinSceneDiagnosticCode.BudgetExceeded));
        }

        private static JArray declarations(string prefix, int count) => new JArray(Enumerable.Range(0, count).Select(i => new JObject
        {
            ["id"] = $"{prefix}.n{i}",
            ["operation"] = "divide",
            ["left"] = "score.value",
            ["right"] = 100,
        }));
    }
}
