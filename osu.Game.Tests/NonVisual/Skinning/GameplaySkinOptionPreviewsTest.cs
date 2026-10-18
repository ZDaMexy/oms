// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinOptionPreviewsTest
    {
        [Test]
        public void TestInlinePreviewRoundTripAndOldChoiceHasNoInventedPreview()
        {
            JObject json = GameplaySkinOptionsTest.ManifestJson();
            json["options"]![1]!["choices"]![0]!["preview"] = "previews/standard.png";
            GameplaySkinSceneManifest manifest = GameplaySkinOptionsTest.DecodeManifest(json);
            JObject encoded = JObject.Parse(GameplaySkinSceneCodec.EncodeManifest(manifest));
            Assert.That(manifest.Options[1].Choices[0].PreviewPath, Is.EqualTo("previews/standard.png"));
            Assert.That(encoded["options"]![1]!["choices"]![0]!["preview"]!.Value<string>(), Is.EqualTo("previews/standard.png"));
            Assert.That(((JObject)encoded["options"]![1]!["choices"]![1]!).ContainsKey("preview"), Is.False);
            Assert.That(GameplaySkinOptionsTest.DecodeManifest(encoded).Options[1].Choices[0].PreviewPath, Is.EqualTo("previews/standard.png"));
        }

        [TestCase("../outside.png")]
        [TestCase("C:/outside.png")]
        [TestCase("/outside.png")]
        [TestCase("https://example.com/preview.png")]
        [TestCase("preview.png:payload")]
        [TestCase("preview.jpg")]
        [TestCase("")]
        public void TestInlineAndCatalogPreviewPathsCannotEscapeThePackageOrSelectAnotherFormat(string path)
        {
            JObject inline = GameplaySkinOptionsTest.ManifestJson();
            inline["options"]![1]!["choices"]![0]!["preview"] = path;
            Assert.That(GameplaySkinSceneCodec.DecodeManifest(inline.ToString(Formatting.None)).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));

            var fixture = new Package();
            fixture.AddChoice("normal", path, png(new Rgba32(255, 0, 0)));
            Assert.That(GameplaySkinSceneCodec.DecodeContentCatalog(Encoding.UTF8.GetBytes(fixture.Catalog.ToString(Formatting.None))).Status,
                Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
        }

        [Test]
        public void TestCatalogPreviewsUseTheExactCapturedRevisionAndNeverBecomeGameplayResources()
        {
            var fixture = new Package();
            fixture.AddChoice("red", "previews/red.png", png(new Rgba32(255, 0, 0)));
            fixture.AddChoice("blue", "previews/blue.png", png(new Rgba32(0, 0, 255)));
            fixture.AddChoice("red-again", "previews/red.png", fixture.Files["previews/red.png"]);
            fixture.Choices.Add(new JObject { ["value"] = "none", ["label"] = "No thumbnail" });
            GameplaySkinPreparedPackage first = fixture.Capture();
            Assert.That(first.Manifest!.Options.Single().Choices.First().PreviewPath, Is.EqualTo("previews/red.png"));
            Assert.That(first.OptionPreviews.TryGet("notes", "red", out GameplaySkinOptionPreview red), Is.True);
            Assert.That(first.OptionPreviews.TryGet("notes", "red-again", out GameplaySkinOptionPreview repeated), Is.True);
            Assert.That(repeated, Is.SameAs(red), "One encoded thumbnail is retained once even when several choices use it.");
            Assert.That(fixture.ReadCounts["previews/red.png"], Is.EqualTo(1));
            Assert.That(first.Resources, Is.Empty);

            // The next package has the same option/choice identifiers and a different thumbnail. Selecting it must
            // not mutate the first package, and settings must not consult any mutable source after preparation.
            fixture.Files["previews/red.png"] = png(new Rgba32(0, 255, 0));
            GameplaySkinPreparedPackage next = fixture.Capture();
            foreach (byte[] bytes in fixture.Files.Values)
                Array.Fill(bytes, (byte)0);
            fixture.Files.Clear();
            fixture.ForbidReads = true;
            Assert.That(next.OptionPreviews.TryGet("notes", "red", out GameplaySkinOptionPreview green), Is.True);
            using Image<Rgba32> oldImage = red.CreateImage();
            using Image<Rgba32> nextImage = green.CreateImage();
            Assert.That(oldImage[0, 0], Is.EqualTo(new Rgba32(255, 0, 0)));
            Assert.That(nextImage[0, 0], Is.EqualTo(new Rgba32(0, 255, 0)));
            Assert.That(red.ContentRevision, Is.Not.EqualTo(green.ContentRevision));
            Assert.That(first.OptionPreviews.TryGet("notes", "none", out _), Is.False);
            Assert.That(first.OptionPreviews.TryGet("other-option", "red", out _), Is.False);
            Assert.That(first.OptionPreviews.TryGet("notes", "unknown-choice", out _), Is.False);

            GameplaySkinPreparedPackage selected = first.Select(GameplaySkinOptionValues.Resolve(first.Manifest.Options, null, "bms", "7k"), CancellationToken.None);
            Assert.That(selected.Resources, Is.Empty, "Settings artwork must not allocate a gameplay texture or consume its resource budget.");
            Assert.That(selected.OptionPreviews, Is.SameAs(first.OptionPreviews));
        }

        [Test]
        public void TestInlinePreviewIsCapturedAndDecodedIndependentlyOfSceneResources()
        {
            var fixture = new Package();
            fixture.Root.Remove("contentCatalog");
            fixture.Root["options"] = JArray.Parse("""
                [{"id":"turntable","label":"Turntable","type":"choice","default":"standard","choices":[
                  {"value":"standard","label":"Standard","preview":"previews/disc.png"}]}]
                """);
            fixture.Files["previews/disc.png"] = png(new Rgba32(4, 5, 6), GameplaySkinOptionPreviewBudgets.MAX_DIMENSION, 1);
            GameplaySkinPreparedPackage package = fixture.Capture();
            fixture.Files.Clear();
            Assert.That(package.OptionPreviews.TryGet("turntable", "standard", out GameplaySkinOptionPreview preview), Is.True);
            using Image<Rgba32> image = preview.CreateImage();
            Assert.That(image.Width, Is.EqualTo(GameplaySkinOptionPreviewBudgets.MAX_DIMENSION));
            Assert.That(image[0, 0], Is.EqualTo(new Rgba32(4, 5, 6)));
            Assert.That(package.Resources, Is.Empty);
        }

        [TestCase("missing", GameplaySkinSceneDiagnosticCode.InvalidReference)]
        [TestCase("not-png", GameplaySkinSceneDiagnosticCode.InvalidResource)]
        [TestCase("bad-header", GameplaySkinSceneDiagnosticCode.InvalidResource)]
        [TestCase("oversized-bytes", GameplaySkinSceneDiagnosticCode.BudgetExceeded)]
        [TestCase("oversized-dimensions", GameplaySkinSceneDiagnosticCode.BudgetExceeded)]
        public void TestUnselectedDeclaredThumbnailsAreValidatedBeforePublication(string reason, GameplaySkinSceneDiagnosticCode expected)
        {
            var fixture = new Package();
            fixture.Choices.Add(new JObject { ["value"] = "native", ["label"] = "Native" });
            fixture.AddChoice("unselected", "previews/other.png", png(new Rgba32(1, 2, 3)));
            switch (reason)
            {
                case "missing": fixture.Files.Remove("previews/other.png"); break;
                case "not-png": fixture.Files["previews/other.png"] = Encoding.UTF8.GetBytes("not a PNG image"); break;
                case "bad-header": fixture.Files["previews/other.png"] = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }; break;
                case "oversized-bytes": fixture.Files["previews/other.png"] = new byte[GameplaySkinOptionPreviewBudgets.MAX_ENCODED_BYTES + 1]; break;
                case "oversized-dimensions": fixture.Files["previews/other.png"] = png(new Rgba32(1, 2, 3), GameplaySkinOptionPreviewBudgets.MAX_DIMENSION + 1, 1); break;
            }
            GameplaySkinScenePreparationException error = Assert.Throws<GameplaySkinScenePreparationException>(() => fixture.Capture())!;
            Assert.That(error.Code, Is.EqualTo(expected));
        }

        [Test]
        public void TestEncodedPreviewTotalHasItsOwnBoundAndSharedPathsDoNotCountTwice()
        {
            var fixture = new Package();
            byte[] padded = new byte[GameplaySkinOptionPreviewBudgets.MAX_ENCODED_BYTES];
            png(new Rgba32(1, 2, 3)).CopyTo(padded, 0);
            int maximum = checked((int)(GameplaySkinOptionPreviewBudgets.MAX_TOTAL_ENCODED_BYTES / padded.Length));
            for (int index = 0; index < maximum; index++)
                fixture.AddChoice("choice.n" + index, "previews/n" + index + ".png", padded);
            fixture.AddChoice("shared", "previews/n0.png", padded);
            Assert.DoesNotThrow(() => fixture.Capture());
            fixture.AddChoice("excess", "previews/excess.png", padded);
            GameplaySkinScenePreparationException error = Assert.Throws<GameplaySkinScenePreparationException>(() => fixture.Capture())!;
            Assert.That(error.Code, Is.EqualTo(GameplaySkinSceneDiagnosticCode.BudgetExceeded));
        }

        [Test]
        public void TestCorruptPixelDataIsTypedAndAuthorValidationChecksInactiveThumbnails()
        {
            var fixture = new Package();
            fixture.Choices.Add(new JObject { ["value"] = "native", ["label"] = "Native" });
            fixture.AddChoice("broken", "previews/broken.png", Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVRiYWQtemxpYi1kYXRhyTzwQAAAAABJRU5ErkJggg=="));
            GameplaySkinPreparedPackage package = fixture.Capture();
            GameplaySkinScenePreparationException error = Assert.Throws<GameplaySkinScenePreparationException>(
                () => package.OptionPreviews.ValidateAll(CancellationToken.None))!;
            Assert.That(error.Code, Is.EqualTo(GameplaySkinSceneDiagnosticCode.InvalidResource));
        }

        [Test]
        public void TestCancellationDoesNotDecodeOrCaptureAnotherThumbnail()
        {
            var fixture = new Package();
            fixture.AddChoice("normal", "previews/normal.png", png(new Rgba32(1, 2, 3)));
            GameplaySkinPreparedPackage package = fixture.Capture();
            Assert.That(package.OptionPreviews.TryGet("notes", "normal", out GameplaySkinOptionPreview preview), Is.True);
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => preview.CreateImage(cancelled.Token));
            Assert.Throws<OperationCanceledException>(() => GameplaySkinOptionPreviews.Capture(package.Manifest!.Options,
                (_, _) => throw new AssertionException("Cancelled preparation cannot read the package."), cancelled.Token));
        }

        private static byte[] png(Rgba32 colour, int width = 1, int height = 1)
        {
            using var image = new Image<Rgba32>(width, height, colour);
            using var output = new MemoryStream();
            image.SaveAsPng(output);
            return output.ToArray();
        }

        private sealed class Package
        {
            public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);
            public Dictionary<string, int> ReadCounts { get; } = new(StringComparer.Ordinal);
            public JObject Root { get; } = JObject.Parse("""
                {"contract":"oms-gameplay-skin-manifest.v1","scene":"gameplay-skin.scene.json",
                 "sceneContract":"oms-gameplay-skin-scene.v1","eventContract":"oms-gameplay-skin-event.v1",
                 "resources":[],"contentCatalog":"catalog.json"}
                """);
            public JArray Choices { get; } = new();
            public JObject Catalog => new()
            {
                ["contract"] = GameplaySkinContentBudgets.CONTRACT_ID,
                ["modules"] = new JArray(new JObject { ["id"] = "notes", ["label"] = "Notes", ["default"] = Choices[0]!["value"]!.DeepClone(), ["choices"] = Choices.DeepClone() }),
            };
            public bool ForbidReads { get; set; }

            public Package()
            {
                Files["skin.ini"] = Encoding.UTF8.GetBytes("[General]\nName: Settings preview fixture\n");
                Files["gameplay-skin.scene.json"] = Encoding.UTF8.GetBytes("""
                    {"contract":"oms-gameplay-skin-scene.v1","root":{"id":"root","type":"container",
                     "target":{"kind":"global"},"blend":"inherit","properties":{},"effects":[],"children":[]},
                     "tracks":[],"stateMachines":[],"bindings":[],"variants":[],"templates":[],"instances":[]}
                    """);
            }

            public void AddChoice(string value, string path, byte[] bytes)
            {
                Choices.Add(new JObject { ["value"] = value, ["label"] = value, ["preview"] = path });
                Files[path] = bytes;
            }

            public GameplaySkinPreparedPackage Capture()
            {
                Files[GameplaySkinSceneContracts.MANIFEST_FILE_NAME] = Encoding.UTF8.GetBytes(Root.ToString(Formatting.None));
                if (Root.ContainsKey("contentCatalog"))
                    Files["catalog.json"] = Encoding.UTF8.GetBytes(Catalog.ToString(Formatting.None));
                return GameplaySkinPreparedPackage.Prepare((path, limit) =>
                {
                    if (ForbidReads)
                        throw new AssertionException("The captured preview cannot reopen its source.");
                    ReadCounts[path] = ReadCounts.GetValueOrDefault(path) + 1;
                    if (path.StartsWith("previews/", StringComparison.Ordinal))
                        Assert.That(limit, Is.EqualTo(GameplaySkinOptionPreviewBudgets.MAX_ENCODED_BYTES));
                    return Files.GetValueOrDefault(path);
                }, "package-revision", Guid.NewGuid(), null, CancellationToken.None);
            }
        }
    }
}
