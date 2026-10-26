// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.NonVisual.Skinning
{
    public sealed partial class GameplaySkinContentCatalogTest
    {
        [Test]
        public void TestCapturedSelectedCombinationAcceptsTwelveFontsAndRejectsThirteenWithoutReplacingItsValidSelection()
        {
            var fixture = new Package();
            fixture.AddBranch("frame", new Rgba32(255, 0, 0), null);
            fixture.AddBranch("judgement", new Rgba32(0, 255, 0), null);
            fixture.AddBranch("excess", new Rgba32(0, 0, 255), null);
            addFonts("frame", 6);
            addFonts("judgement", 6);
            addFonts("excess", 7);
            fixture.SetModule("frame", new[] { fixture.Choice("standard", "frame") }, "standard");
            fixture.SetModule("judgement", new[] { fixture.Choice("four-palettes", "judgement"), fixture.Choice("too-many", "excess") }, "four-palettes");
            using SkinPackageRevisionCapsule capsule = fixture.CreateCapsule();
            GameplaySkinPreparedPackage captured = captureCapsule(capsule);
            fixture.Files.Clear();
            fixture.ForbidReads = true;
            GameplaySkinOptionValues valid = resolve(captured, "bms");
            GameplaySkinPreparedPackage active = captured.Select(valid, CancellationToken.None);
            string[] activeFonts = active.Manifest!.BitmapFonts.Select(font => font.Id).ToArray();
            Assert.That(activeFonts, Has.Length.EqualTo(12));
            Assert.That(active.Resources, Has.Count.EqualTo(2));

            GameplaySkinOptionValues excess = GameplaySkinOptionValues.Resolve(captured.Manifest!.Options,
                new Dictionary<string, string> { ["judgement"] = "too-many" }, "bms", "7k");
            GameplaySkinScenePreparationException? failure = Assert.Throws<GameplaySkinScenePreparationException>(() => captured.Select(excess, CancellationToken.None));
            Assert.That(failure!.Code, Is.EqualTo(GameplaySkinSceneDiagnosticCode.BudgetExceeded));
            Assert.That(active.Manifest.BitmapFonts.Select(font => font.Id), Is.EqualTo(activeFonts));
            Assert.That(captured.Select(valid, CancellationToken.None).Manifest!.BitmapFonts.Select(font => font.Id), Is.EqualTo(activeFonts));
            Assert.That(GameplaySkinBitmapFontBudgets.MAX_GLYPHS_PER_FONT, Is.EqualTo(16));

            void addFonts(string branch, int count)
                => fixture.EditJson(branch + "/manifest.json", manifest =>
                {
                    var fonts = (JArray)manifest["bitmapFonts"]!;
                    for (int index = 1; index < count; index++)
                    {
                        var font = (JObject)fonts[0]!.DeepClone();
                        font["id"] = branch + ".font-" + index;
                        font["lineHeight"] = 8 + index;
                        fonts.Add(font);
                    }
                });
        }
    }
}
