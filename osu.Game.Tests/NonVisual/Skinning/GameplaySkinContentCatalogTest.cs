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
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.Audio;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinContentCatalogTest
    {
        [Test]
        public void TestCapsuleCatalogRetainsAllChoicesAfterMutableSourcesAndPublicCopiesChange()
        {
            var fixture = twoChoicePackage();
            using SkinPackageRevisionCapsule capsule = fixture.CreateCapsule();
            GameplaySkinPreparedPackage package = captureCapsule(capsule);
            foreach (byte[] bytes in fixture.Files.Values)
                Array.Clear(bytes);
            fixture.Files.Clear();
            using IResourceStore<byte[]> publicView = capsule.CreateResourceView();
            Array.Clear(publicView.Get("first.png"));

            foreach ((string choice, Rgba32 expected) in new[] { ("first", new Rgba32(255, 0, 0)), ("second", new Rgba32(0, 0, 255)) })
            {
                GameplaySkinPreparedPackage selected = package.Select(GameplaySkinOptionValues.Resolve(package.Manifest!.Options,
                    new Dictionary<string, string> { ["frame"] = choice }, "bms", "7k"), CancellationToken.None);
                using Image<Rgba32> image = selected.Resources.Single().CreateImage();
                Assert.That(image[0, 0], Is.EqualTo(expected));
            }
        }

        [Test]
        public void TestRetiringAnIdenticalCapsuleDoesNotRetireTheActiveCatalogOrItsDecodedPixels()
        {
            var fixture = twoChoicePackage();
            using SkinPackageRevisionCapsule first = fixture.CreateCapsule();
            using SkinPackageRevisionCapsule second = fixture.CreateCapsule();
            Assert.That(second.ContentRevision, Is.EqualTo(first.ContentRevision));
            GameplaySkinPreparedPackage active = captureCapsule(first);
            GameplaySkinPreparedPackage retired = captureCapsule(second);
            GameplaySkinPreparedPackage alreadyDecoded = retired.Select(resolve(retired, "bms"), CancellationToken.None);
            second.Dispose();

            Assert.That(() => retired.Select(resolve(retired, "bms"), CancellationToken.None), Throws.TypeOf<ObjectDisposedException>());
            using Image<Rgba32> activePixels = active.Select(resolve(active, "bms"), CancellationToken.None).Resources.Single().CreateImage();
            using Image<Rgba32> retainedPixels = alreadyDecoded.Resources.Single().CreateImage();
            Assert.That(activePixels[0, 0], Is.EqualTo(new Rgba32(255, 0, 0)));
            Assert.That(retainedPixels[0, 0], Is.EqualTo(new Rgba32(255, 0, 0)), "Decoded upload pixels retain their independent ownership.");
        }

        [Test]
        public void TestMissingUnselectedCapsuleResourceCannotFallBackToAnotherReader()
        {
            var fixture = twoChoicePackage();
            byte[] alternate = fixture.Files["second.png"];
            fixture.Files.Remove("second.png");
            using SkinPackageRevisionCapsule capsule = fixture.CreateCapsule();
            fixture.Files["second.png"] = alternate;
            GameplaySkinScenePreparationException? failure = Assert.Throws<GameplaySkinScenePreparationException>(() =>
                GameplaySkinPreparedPackage.Prepare((path, _) => fixture.Files.GetValueOrDefault(path), capsule.ContentRevision,
                    Guid.NewGuid(), null, CancellationToken.None, capsule.CaptureResourceReference));
            Assert.That(failure!.Code, Is.EqualTo(GameplaySkinSceneDiagnosticCode.InvalidReference));
        }

        [Test]
        public void TestSkinPreparationUsesItsExactOwnedCapsuleForContentChoices()
        {
            var fixture = twoChoicePackage();
            SkinPackageRevisionCapsule capsule = fixture.CreateCapsule();
            using var store = new SkinPackageRevisionResourceStore(capsule);
            using var skin = new TestSkin(Guid.NewGuid(), store);
            GameplaySkinPreparedPackage package = skin.PrepareGameplaySkinPackage(CancellationToken.None);
            fixture.Files.Clear();
            GameplaySkinPreparedPackage selected = package.Select(resolve(package, "bms"), CancellationToken.None);
            using Image<Rgba32> image = selected.Resources.Single().CreateImage();
            Assert.That(image[0, 0], Is.EqualTo(new Rgba32(255, 0, 0)));
            skin.Dispose();
            Assert.That(() => package.Select(resolve(package, "bms"), CancellationToken.None), Throws.TypeOf<ObjectDisposedException>());
        }

        [Test]
        public void TestSharedImageAcrossManyAlternativesIsReadOnceDuringCapture()
        {
            var fixture = new Package();
            for (int i = 0; i < 12; i++)
            {
                string branch = "branch" + i;
                fixture.AddBranch(branch, new Rgba32(255, 0, 0), null);
                fixture.EditJson(branch + "/manifest.json", json => json["resources"]![0]!["path"] = "shared.png");
            }
            fixture.SetModule("frame", Enumerable.Range(0, 12).Select(i => fixture.Choice("n" + i, "branch" + i)), "n0");
            byte[] largeImage;
            using (var image = new Image<Rgba32>(512, 512))
            {
                using (var controlOutput = new MemoryStream())
                {
                    image.SaveAsPng(controlOutput);
                    fixture.Files["shared.png"] = controlOutput.ToArray();
                }
                var random = new Random(9231);
                for (int y = 0; y < image.Height; y++)
                    for (int x = 0; x < image.Width; x++)
                        image[x, y] = new Rgba32((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
                using var output = new MemoryStream();
                image.SaveAsPng(output);
                largeImage = output.ToArray();
            }
            // Keep scene/JSON work identical so this measures the extra encoded bytes, not fixed preparation costs.
            using SkinPackageRevisionCapsule control = fixture.CreateCapsule();
            _ = captureCapsule(control);
            long controlBefore = GC.GetAllocatedBytesForCurrentThread();
            _ = captureCapsule(control);
            long controlAllocated = GC.GetAllocatedBytesForCurrentThread() - controlBefore;
            long encodedGrowth = largeImage.LongLength - fixture.Files["shared.png"].LongLength;
            fixture.Files["shared.png"] = largeImage;
            using SkinPackageRevisionCapsule capsule = fixture.CreateCapsule();
            _ = GameplaySkinPreparedPackage.Identify(fixture.Files["shared.png"]);
            long before = GC.GetAllocatedBytesForCurrentThread();
            GameplaySkinPreparedPackage captured = captureCapsule(capsule);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(captured.Manifest!.Options.Single().Choices, Has.Count.EqualTo(12));
            Assert.That(allocated - controlAllocated, Is.LessThan(encodedGrowth * 2),
                "References to the same encoded image in inactive alternatives must not copy it per alternative.");
        }

        private static Package twoChoicePackage()
        {
            var fixture = new Package();
            fixture.AddBranch("first", new Rgba32(255, 0, 0), null);
            fixture.AddBranch("second", new Rgba32(0, 0, 255), null);
            fixture.SetModule("frame", new[] { fixture.Choice("first", "first"), fixture.Choice("second", "second") }, "first");
            return fixture;
        }

        [Test]
        public void TestSharedSceneUsesTheSelectedChoicesExactFrozenTextureManifest()
        {
            var fixture = twoChoicePackage();
            fixture.EditJson("second/manifest.json", manifest =>
            {
                manifest["resources"]![0]!["id"] = "first.texture";
                manifest["bitmapFonts"]![0]!["glyphs"]![0]!["resource"] = "first.texture";
            });
            fixture.Catalog["modules"]![0]!["choices"]![1]!["scene"] = "first/scene.json";
            GameplaySkinPreparedPackage captured = fixture.Capture();
            fixture.ForbidReads = true;
            foreach ((string choice, Rgba32 expected) in new[] { ("first", new Rgba32(255, 0, 0)), ("second", new Rgba32(0, 0, 255)) })
            {
                GameplaySkinPreparedPackage selected = captured.Select(GameplaySkinOptionValues.Resolve(captured.Manifest!.Options,
                    new Dictionary<string, string> { ["frame"] = choice }, "bms", "7k"), CancellationToken.None);
                Assert.That(selected.Resources.Single().Source.Id, Is.EqualTo("first.texture"));
                using Image<Rgba32> image = selected.Resources.Single().CreateImage();
                Assert.That(image[0, 0], Is.EqualTo(expected), "Shared geometry must retain each choice's own captured pixels.");
            }
        }

        private static GameplaySkinPreparedPackage captureCapsule(SkinPackageRevisionCapsule capsule)
        {
            using IResourceStore<byte[]> view = capsule.CreateResourceView();
            return GameplaySkinPreparedPackage.Prepare((path, _) =>
            {
                Assert.That(path, Is.EqualTo(GameplaySkinSceneContracts.MANIFEST_FILE_NAME),
                    "A catalog with a retained immutable source must not recapture alternatives through another reader.");
                return view.Get(path);
            }, capsule.ContentRevision, Guid.NewGuid(), null, CancellationToken.None, capsule.CaptureResourceReference);
        }

        [Test]
        public void TestSelectedPngWithAValidHeaderAndCorruptZlibDataFailsAsATypedResourceError()
        {
            var fixture = new Package();
            fixture.AddBranch("part", new Rgba32(255, 0, 0), null);
            fixture.SetModule("notes", new[] { fixture.Choice("standard", "part") }, "standard");
            fixture.Files["part.png"] = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVRiYWQtemxpYi1kYXRhyTzwQAAAAABJRU5ErkJggg==");
            GameplaySkinPreparedPackage captured = fixture.Capture();
            var error = Assert.Throws<GameplaySkinScenePreparationException>(() => captured.Select(resolve(captured, "bms"), CancellationToken.None));
            Assert.That(error!.Code, Is.EqualTo(GameplaySkinSceneDiagnosticCode.InvalidResource));
        }

        [Test]
        public void TestLargeCatalogPreparesOnlySelectedContentAndRetainsImmutableCapturedBytes()
        {
            var fixture = new Package();
            fixture.AddBranch("first", new Rgba32(255, 0, 0), 0.1);
            fixture.AddBranch("second", new Rgba32(0, 0, 255), 0.6);
            fixture.SetModule("frame", Enumerable.Range(0, 366).Select(index => fixture.Choice("frame.n" + index, index % 2 == 0 ? "first" : "second")), "frame.n0");
            GameplaySkinPreparedPackage captured = fixture.Capture();
            Assert.That(captured.Manifest!.Options.Single().Choices, Has.Count.EqualTo(366));
            Assert.That(captured.Manifest.Options.Single().IsContentCatalog, Is.True);
            Assert.That(captured.Resources, Is.Empty, "C2 captures encoded alternatives without allocating decoded image buffers");

            var saved = new Dictionary<string, string> { ["frame"] = "frame.n365" };
            GameplaySkinOptionValues firstChoices = GameplaySkinOptionValues.Resolve(captured.Manifest.Options, saved, "bms", "7k");
            // Neither a mutable read buffer, a removed file nor a changed preference can affect this retained revision.
            foreach (byte[] bytes in fixture.Files.Values)
                Array.Fill(bytes, (byte)0);
            fixture.Files.Clear();
            saved["frame"] = "frame.n0";
            fixture.ForbidReads = true;
            GameplaySkinPreparedPackage first = captured.Select(firstChoices, CancellationToken.None);
            GameplaySkinPreparedPackage second = captured.Select(GameplaySkinOptionValues.Resolve(captured.Manifest.Options, saved, "bms", "7k"), CancellationToken.None);
            using Image<Rgba32> firstImage = first.Resources.Single().CreateImage();
            using Image<Rgba32> secondImage = second.Resources.Single().CreateImage();
            Assert.Multiple(() =>
            {
                Assert.That(first.Resources.Single().Source.Id, Is.EqualTo("second.texture"));
                Assert.That(second.Resources.Single().Source.Id, Is.EqualTo("first.texture"));
                Assert.That(firstImage[0, 0], Is.EqualTo(new Rgba32(0, 0, 255)));
                Assert.That(secondImage[0, 0], Is.EqualTo(new Rgba32(255, 0, 0)));
                Assert.That(first.Manifest!.AuthorLayouts.Single().Stages.Single().Rect.X, Is.EqualTo(0.6).Within(0.000001));
                Assert.That(second.Manifest!.AuthorLayouts.Single().Stages.Single().Rect.X, Is.EqualTo(0.1).Within(0.000001));
                Assert.That(first.Manifest.BitmapFonts.Single().Id, Is.EqualTo("second.font"));
                Assert.That(first.Document!.Root.Children.Single().Id, Is.EqualTo("second.root"));
                Assert.That(first.MaterialDocument!.LegacySections.Single(section => section.Name == "Bms").Lines
                    .Single(line => line.Key == "PlayfieldWidth").Value, Is.EqualTo("0.6"));
                Assert.That(first.MaterialDocument.Sections.Single().Entries.Single().Value, Is.EqualTo("second.png"));
                Assert.That(firstChoices.Values["frame"].CanonicalValue, Is.EqualTo("frame.n365"));
            });
        }

        [Test]
        public void TestIndependentModulesComposeOneSceneAndOnlyApplicableSelectionsContribute()
        {
            var fixture = new Package();
            fixture.AddBranch("frame", new Rgba32(255, 0, 0), 0.1);
            fixture.AddBranch("note", new Rgba32(0, 255, 0), null);
            fixture.SetModule("frame", new[] { fixture.Choice("base", "frame") }, "base");
            fixture.SetModule("notes", new[] { new JObject { ["value"] = "none", ["label"] = "Native" }, fixture.Choice("green", "note") }, "green", "mania");
            GameplaySkinPreparedPackage captured = fixture.Capture();
            GameplaySkinPreparedPackage bms = captured.Select(resolve(captured, "bms"), CancellationToken.None);
            GameplaySkinPreparedPackage mania = captured.Select(resolve(captured, "mania"), CancellationToken.None);
            Assert.Multiple(() =>
            {
                Assert.That(bms.Resources.Select(resource => resource.Source.Id), Is.EqualTo(new[] { "frame.texture" }));
                Assert.That(mania.Resources.Select(resource => resource.Source.Id), Is.EqualTo(new[] { "frame.texture", "note.texture" }));
                Assert.That(mania.Document!.Root.Children.Select(child => child.Id), Is.EqualTo(new[] { "frame.root", "note.root" }));
                Assert.That(mania.Document.Bindings, Has.Count.EqualTo(2));
                Assert.That(mania.MaterialDocument!.Sections.SelectMany(section => section.Entries).Count(), Is.EqualTo(1));
            });
        }

        [TestCase("scene-reference")]
        [TestCase("ini-state")]
        [TestCase("missing-resource")]
        [TestCase("nested-catalog")]
        [TestCase("branch-options")]
        public void TestUnselectedBranchIsValidatedAtCapture(string reason)
        {
            var fixture = new Package();
            fixture.AddBranch("good", new Rgba32(255, 0, 0), 0.1);
            fixture.AddBranch("bad", new Rgba32(0, 0, 255), null);
            fixture.SetModule("frame", new[] { fixture.Choice("good", "good"), fixture.Choice("bad", "bad") }, "good");
            Assert.DoesNotThrow(() => fixture.Capture(), "The original branches must validate before mutating one unselected branch.");
            switch (reason)
            {
                case "scene-reference":
                    fixture.EditJson("bad/scene.json", json => json["root"]!["children"]![0]!["resource"] = "unknown.texture");
                    break;
                case "ini-state": fixture.Files["bad/materials.ini"] = Encoding.UTF8.GetBytes("[GameplaySkin.Common:1]\nTarget: Global ruleset=any keymode=any stage-mode=any\nhud.text: resource Invalid\n"); break;
                case "missing-resource": fixture.Files.Remove("bad.png"); break;
                case "nested-catalog": fixture.EditJson("bad/manifest.json", json => json["contentCatalog"] = "nested.json"); break;
                case "branch-options": fixture.EditJson("bad/manifest.json", json => json["options"] = new JArray(new JObject { ["id"] = "override", ["label"] = "Override", ["type"] = "toggle", ["default"] = true })); break;
            }
            Assert.Throws<GameplaySkinScenePreparationException>(() => fixture.Capture());
        }

        [TestCase("resource-id")]
        [TestCase("node-id")]
        [TestCase("ini-owner")]
        public void TestSelectedCombinationCannotSilentlyOverrideAnotherModule(string collision)
        {
            var fixture = new Package();
            fixture.AddBranch("first", new Rgba32(255, 0, 0), 0.1);
            fixture.AddBranch("second", new Rgba32(0, 0, 255), null);
            fixture.SetModule("frame", new[] { fixture.Choice("first", "first") }, "first");
            fixture.SetModule("notes", new[] { fixture.Choice("second", "second") }, "second");
            Assert.DoesNotThrow(() => fixture.Capture(), "Each original contribution must validate before introducing a cross-module collision.");
            switch (collision)
            {
                case "resource-id":
                    fixture.EditJson("second/manifest.json", json =>
                    {
                        json["resources"]![0]!["id"] = "first.texture";
                        json["bitmapFonts"]![0]!["glyphs"]![0]!["resource"] = "first.texture";
                    });
                    fixture.EditJson("second/scene.json", json => json["root"]!["children"]![0]!["resource"] = "first.texture");
                    break;
                case "node-id": fixture.EditJson("second/scene.json", json => json["root"]!["id"] = "first.root"); break;
                case "ini-owner": fixture.Files["second/materials.ini"] = fixture.Files["first/materials.ini"].ToArray(); break;
            }
            GameplaySkinPreparedPackage captured = fixture.Capture();
            Assert.Throws<GameplaySkinScenePreparationException>(() => captured.Select(resolve(captured, "bms"), CancellationToken.None));
        }

        [Test]
        public void TestSharedIdenticalResourceIsPreparedOnceAndActiveResourceLimitIsUnchanged()
        {
            var fixture = new Package();
            fixture.AddBranch("first", new Rgba32(255, 0, 0), 0.1);
            fixture.AddBranch("second", new Rgba32(0, 0, 255), null);
            fixture.SetModule("frame", new[] { fixture.Choice("first", "first") }, "first");
            fixture.SetModule("notes", new[] { fixture.Choice("second", "second") }, "second");
            fixture.EditJson("second/manifest.json", json =>
            {
                json["resources"]![0]!["id"] = "first.texture";
                json["resources"]![0]!["path"] = "first.png";
                json["bitmapFonts"]![0]!["glyphs"]![0]!["resource"] = "first.texture";
            });
            fixture.EditJson("second/scene.json", json => json["root"]!["children"]![0]!["resource"] = "first.texture");
            GameplaySkinPreparedPackage captured = fixture.Capture();
            Assert.That(captured.Select(resolve(captured, "bms"), CancellationToken.None).Resources, Has.Count.EqualTo(1));

            fixture = new Package();
            fixture.AddBranch("first", new Rgba32(255, 0, 0), null);
            fixture.AddBranch("second", new Rgba32(0, 0, 255), null);
            fixture.SetModule("first", new[] { fixture.Choice("first", "first") }, "first");
            fixture.SetModule("second", new[] { fixture.Choice("second", "second") }, "second");
            foreach (string branch in new[] { "first", "second" })
            {
                fixture.EditJson(branch + "/manifest.json", json =>
                {
                    var resources = (JArray)json["resources"]!;
                    for (int index = 1; index < 129; index++)
                    {
                        string path = branch + ".n" + index + ".png";
                        resources.Add(new JObject { ["id"] = branch + ".n" + index, ["type"] = "texture", ["path"] = path });
                        fixture.Files[path] = fixture.Files[branch + ".png"];
                    }
                });
            }
            captured = fixture.Capture();
            GameplaySkinScenePreparationException? failure = Assert.Throws<GameplaySkinScenePreparationException>(() => captured.Select(resolve(captured, "bms"), CancellationToken.None));
            Assert.That(failure!.Code, Is.EqualTo(GameplaySkinSceneDiagnosticCode.BudgetExceeded));
        }

        [TestCase("unsafe-path", GameplaySkinSceneDiagnosticCode.UnsafeResourcePath)]
        [TestCase("duplicate-field", GameplaySkinSceneDiagnosticCode.DuplicateField)]
        [TestCase("duplicate-value", GameplaySkinSceneDiagnosticCode.DuplicateField)]
        [TestCase("too-many-choices", GameplaySkinSceneDiagnosticCode.BudgetExceeded)]
        [TestCase("unknown-default", GameplaySkinSceneDiagnosticCode.InvalidPropertyValue)]
        [TestCase("half-pair", GameplaySkinSceneDiagnosticCode.InvalidReference)]
        public void TestCatalogBoundaryRejectsMalformedDirectories(string reason, GameplaySkinSceneDiagnosticCode expected)
        {
            var fixture = new Package();
            fixture.SetModule("frame", new[] { new JObject { ["value"] = "native", ["label"] = "Native" } }, "native");
            JObject json = fixture.Catalog;
            JObject module = (JObject)json["modules"]![0]!;
            JArray choices = (JArray)module["choices"]!;
            switch (reason)
            {
                case "unsafe-path": choices[0]!["ini"] = "../outside.ini"; break;
                case "duplicate-field":
                    string duplicate = json.ToString(Formatting.None).Replace("\"label\":\"Native\"", "\"label\":\"Native\",\"label\":\"Again\"", StringComparison.Ordinal);
                    Assert.That(GameplaySkinSceneCodec.DecodeContentCatalog(Encoding.UTF8.GetBytes(duplicate)).Diagnostics.Select(item => item.Code), Does.Contain(expected));
                    return;
                case "duplicate-value": choices.Add(choices[0]!.DeepClone()); break;
                case "too-many-choices":
                    for (int i = 1; i <= GameplaySkinContentBudgets.MAX_CHOICES_PER_MODULE; i++)
                        choices.Add(new JObject { ["value"] = "n" + i, ["label"] = "Choice " + i });
                    break;
                case "unknown-default": module["default"] = "absent"; break;
                case "half-pair": choices[0]!["manifest"] = "branch.json"; break;
            }
            GameplaySkinSceneDecodeResult<GameplaySkinContentCatalog> decoded = GameplaySkinSceneCodec.DecodeContentCatalog(Encoding.UTF8.GetBytes(json.ToString(Formatting.None)));
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
            Assert.That(decoded.Diagnostics.Select(item => item.Code), Does.Contain(expected));
        }

        [Test]
        public void TestAbsentCatalogKeepsTheOrdinaryPreparationAndInlineChoiceBudget()
        {
            var fixture = new Package();
            fixture.RootManifest.Remove("contentCatalog");
            GameplaySkinPreparedPackage package = fixture.Capture();
            Assert.That(package.ContentCatalog, Is.Null);
            Assert.That(package.Select(GameplaySkinOptionValues.Empty, CancellationToken.None), Is.SameAs(package));
            Assert.That(GameplaySkinSceneCodec.EncodeManifest(package.Manifest!), Is.EqualTo(fixture.RootManifest.ToString(Formatting.None)));
            Assert.That(GameplaySkinOptionBudgets.MAX_CHOICES, Is.EqualTo(32));
            Assert.That(GameplaySkinBitmapFontBudgets.MAX_FONTS, Is.EqualTo(10));
            Assert.That(GameplaySkinSceneBudgets.MAX_RESOURCES, Is.EqualTo(256));
            Assert.That(GameplaySkinSceneBudgets.MAX_SCENE_BYTES, Is.EqualTo(512 * 1024));
        }

        [TestCase("duplicate-option")]
        [TestCase("options-budget")]
        [TestCase("script")]
        [TestCase("transformed-root")]
        public void TestCatalogCannotEscapeItsDeclaredCompositionBoundary(string reason)
        {
            var fixture = new Package();
            fixture.SetModule("frame", new[] { new JObject { ["value"] = "native", ["label"] = "Native" } }, "native");
            switch (reason)
            {
                case "duplicate-option":
                    fixture.RootManifest["options"] = JArray.Parse("""[{"id":"frame","type":"toggle","label":"Duplicate","default":true}]""");
                    break;
                case "options-budget":
                    fixture.RootManifest["options"] = new JArray(Enumerable.Range(0, GameplaySkinOptionBudgets.MAX_OPTIONS).Select(index => new JObject
                    {
                        ["id"] = "n" + index,
                        ["type"] = "toggle",
                        ["label"] = "Option " + index,
                        ["default"] = true,
                    }));
                    break;
                case "script": fixture.RootManifest["script"] = GameplaySkinSceneContracts.SCRIPT_FILE_NAME; break;
                case "transformed-root": fixture.EditJson(GameplaySkinSceneContracts.SCENE_FILE_NAME, json => json["root"]!["properties"]!["scale-x"] = 2); break;
            }
            Assert.Throws<GameplaySkinScenePreparationException>(() => fixture.Capture());
        }

        [Test]
        public void TestSelectionRetainsExactOwnerRevisionAndOptionsThroughLayoutAndMaterialReads()
        {
            var fixture = new Package();
            fixture.AddBranch("frame", new Rgba32(255, 0, 0), 0.1);
            fixture.SetModule("frame", new[] { fixture.Choice("standard", "frame") }, "standard");
            GameplaySkinPreparedPackage captured = fixture.Capture();
            GameplaySkinOptionValues options = resolve(captured, "bms");
            GameplaySkinPreparedPackage active = captured.Select(options, CancellationToken.None);
            Guid recordId = Guid.NewGuid();
            using var owner = new TestSkin(recordId);
            using var sameIdOtherOwner = new TestSkin(recordId);
            var selection = new GameplaySkinContentSelection(owner, options, active, active.MaterialDocument!);
            GameplaySkinPackageRevision package = packageFor(owner, active.ContentRevision);
            GameplaySkinLayoutContext context = contextFor(package, options, selection);
            Assert.Multiple(() =>
            {
                Assert.That(context.OptionValues, Is.SameAs(options));
                Assert.That(context.ContentSelection, Is.SameAs(selection));
                Assert.That(owner.GetGameplaySkinDocument(context), Is.SameAs(active.MaterialDocument));
                Assert.Throws<InvalidOperationException>(() => sameIdOtherOwner.GetGameplaySkinDocument(context));
                Assert.Throws<InvalidOperationException>(() => contextFor(packageFor(sameIdOtherOwner, active.ContentRevision), options, selection));
                Assert.Throws<InvalidOperationException>(() => contextFor(packageFor(owner, "another-revision"), options, selection));
                Assert.Throws<ArgumentException>(() => contextFor(package, resolve(captured, "bms"), selection));
            });

            static GameplaySkinPackageRevision packageFor(Skin owner, string revision)
                => GameplaySkinPackageRevision.Create(new SkinCurrentRevision(9, owner.SkinInfo.ID, revision,
                    SkinCurrentRevisionSourceKind.ManagedFolder, owner, false, _ => { }));

            static GameplaySkinLayoutContext contextFor(GameplaySkinPackageRevision package, GameplaySkinOptionValues options, GameplaySkinContentSelection selection)
            {
                GameplaySkinLaneGroupIdentity group = GameplaySkinLaneGroupIdentity.Create(GameplaySkinLaneGroupId.Create("test.group"), GameplaySkinLaneSide.Neutral);
                GameplaySkinLaneTopologySnapshot topology = GameplaySkinLaneTopologySnapshot.Create(new[]
                {
                    GameplaySkinLaneTopologyGroup.Create(group, 0, 0, new[]
                    {
                        GameplaySkinLaneTopologyEntry.Create(GameplaySkinLaneIdentity.Create(GameplaySkinLaneId.Create("test.lane"), group, GameplaySkinLaneRole.Key), 0, 0, 0, 0),
                    }),
                });
                return GameplaySkinLayoutContext.Create("bms", "bms.chart", "7k", "p1", topology,
                    GameplaySkinLayoutRect.Create(0, 0, 1, 1), GameplaySkinLayoutRect.Create(0, 0, 1, 1), 16f / 9, 1,
                    GameplaySkinScrollDirection.Down, package, 1, 1, optionValues: options, contentSelection: selection);
            }
        }

        [Test]
        public void TestInvalidSavedCatalogValueUsesAuthorDefaultAndCancelledSelectionDoesNoWork()
        {
            var fixture = new Package();
            fixture.AddBranch("frame", new Rgba32(255, 0, 0), 0.1);
            fixture.SetModule("frame", new[] { fixture.Choice("standard", "frame") }, "standard");
            GameplaySkinPreparedPackage captured = fixture.Capture();
            GameplaySkinOptionValues options = GameplaySkinOptionValues.Resolve(captured.Manifest!.Options,
                new Dictionary<string, string> { ["frame"] = "removed-entry" }, "bms", "7k");
            Assert.That(options.Values["frame"].CanonicalValue, Is.EqualTo("standard"));
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => captured.Select(options, cancelled.Token));
        }

        [Test]
        public void TestContentDesignAndDiscreteSizeStayIndependentAndFrozen()
        {
            var fixture = new Package();
            fixture.RootManifest["options"] = JArray.Parse("""
                [{"id":"note-size","type":"choice","label":"Size","default":"normal",
                  "choices":[{"value":"normal","label":"Normal"},{"value":"long","label":"Thick"}]}]
                """);
            foreach (string branch in new[] { "standard.normal", "standard.long", "alternate.normal", "alternate.long" })
                fixture.AddBranch(branch, new Rgba32(255, 0, 0), null);
            var choices = new List<JObject>();
            foreach (string design in new[] { "standard", "alternate" })
            {
                JObject choice = fixture.Choice(design, design + ".normal");
                choice["variants"] = new JArray(variant(design + ".long", new JObject { ["note-size"] = "long" }));
                choices.Add(choice);
            }
            fixture.SetModule("notes", choices, "standard");
            GameplaySkinPreparedPackage captured = fixture.Capture();
            var saved = new Dictionary<string, string> { ["notes"] = "alternate", ["note-size"] = "long" };
            GameplaySkinOptionValues alternateThick = GameplaySkinOptionValues.Resolve(captured.Manifest!.Options, saved, "bms", "7k");
            saved["notes"] = "standard";
            GameplaySkinOptionValues standardThick = GameplaySkinOptionValues.Resolve(captured.Manifest.Options, saved, "bms", "7k");
            saved["note-size"] = "normal";
            GameplaySkinOptionValues standardNormal = GameplaySkinOptionValues.Resolve(captured.Manifest.Options, saved, "bms", "7k");
            fixture.ForbidReads = true;
            Assert.Multiple(() =>
            {
                Assert.That(captured.Select(alternateThick, CancellationToken.None).Resources.Single().Source.Id, Is.EqualTo("alternate.long.texture"));
                Assert.That(captured.Select(standardThick, CancellationToken.None).Resources.Single().Source.Id, Is.EqualTo("standard.long.texture"));
                Assert.That(captured.Select(standardNormal, CancellationToken.None).Resources.Single().Source.Id, Is.EqualTo("standard.normal.texture"));
                Assert.That(captured.Manifest.Options.Single(option => option.Id == "notes").Choices.Select(choice => choice.Value), Is.EqualTo(new[] { "standard", "alternate" }));
            });
        }

        [Test]
        public void TestToggleCanSelectVariantAndUnselectedVariantStillValidates()
        {
            var fixture = new Package();
            fixture.RootManifest["options"] = JArray.Parse("""[{"id":"alternate-colour","type":"toggle","label":"Alternate colour","default":false}]""");
            fixture.AddBranch("normal", new Rgba32(255, 0, 0), null);
            fixture.AddBranch("alternate", new Rgba32(0, 0, 255), null);
            JObject choice = fixture.Choice("standard", "normal");
            choice["variants"] = new JArray(variant("alternate", new JObject { ["alternate-colour"] = true }));
            fixture.SetModule("notes", new[] { choice }, "standard");
            GameplaySkinPreparedPackage captured = fixture.Capture();
            GameplaySkinOptionValues alternate = GameplaySkinOptionValues.Resolve(captured.Manifest!.Options,
                new Dictionary<string, string> { ["alternate-colour"] = "true" }, "mania", "8k");
            Assert.That(captured.Select(alternate, CancellationToken.None).Resources.Single().Source.Id, Is.EqualTo("alternate.texture"));
            Assert.That(captured.Select(resolve(captured, "bms"), CancellationToken.None).Resources.Single().Source.Id, Is.EqualTo("normal.texture"));
            fixture.EditJson("alternate/scene.json", json => json["root"]!["children"]![0]!["resource"] = "not.declared");
            Assert.Throws<GameplaySkinScenePreparationException>(() => fixture.Capture());
        }

        [Test]
        public void TestOrderedVariantsSelectSpecificLayoutBeforeGraphFallbackWhenLayoutIsOutOfScope()
        {
            var fixture = new Package();
            fixture.RootManifest["options"] = JArray.Parse("""
                [{"id":"layout","type":"choice","label":"Layout","default":"p1","keymodes":["7k"],
                  "choices":[{"value":"p1","label":"1P"},{"value":"p2","label":"2P"}]},
                 {"id":"graph-visible","type":"toggle","label":"Graph","default":false}]
                """);
            foreach (string branch in new[] { "dp", "dp.graph", "p1", "p1.graph", "p2", "p2.graph" })
                fixture.AddBranch(branch, new Rgba32(255, 0, 0), null);
            JObject choice = fixture.Choice("standard", "dp");
            choice["orderedVariants"] = true;
            choice["variants"] = new JArray(
                variant("p1.graph", new JObject { ["layout"] = "p1", ["graph-visible"] = true }),
                variant("p1", new JObject { ["layout"] = "p1", ["graph-visible"] = false }),
                variant("p2.graph", new JObject { ["layout"] = "p2", ["graph-visible"] = true }),
                variant("p2", new JObject { ["layout"] = "p2", ["graph-visible"] = false }),
                variant("dp.graph", new JObject { ["graph-visible"] = true }));
            fixture.SetModule("frame", new[] { choice }, "standard");
            GameplaySkinPreparedPackage captured = fixture.Capture();
            fixture.ForbidReads = true;
            foreach ((string mode, string layout, bool graph, string expected) in new[]
            {
                ("7k", "p1", true, "p1.graph"), ("7k", "p1", false, "p1"),
                ("7k", "p2", true, "p2.graph"), ("7k", "p2", false, "p2"),
                ("14k", "p1", true, "dp.graph"), ("14k", "p2", false, "dp"),
            })
            {
                GameplaySkinOptionValues selected = GameplaySkinOptionValues.Resolve(captured.Manifest!.Options,
                    new Dictionary<string, string> { ["layout"] = layout, ["graph-visible"] = graph ? "true" : "false" }, "bms", mode);
                Assert.That(captured.Select(selected, CancellationToken.None).Resources.Single().Source.Id,
                    Is.EqualTo(expected + ".texture"), $"{mode}, {layout}, graph={graph}");
            }
        }

        [TestCase("true")]
        [TestCase(1)]
        public void TestOrderedVariantFlagRequiresBoolean(object invalid)
        {
            var fixture = new Package();
            JObject choice = new JObject { ["value"] = "standard", ["label"] = "Standard", ["orderedVariants"] = JToken.FromObject(invalid) };
            fixture.SetModule("frame", new[] { choice }, "standard");
            Assert.Throws<GameplaySkinScenePreparationException>(() => fixture.Capture());
        }

        [TestCase("unknown")]
        [TestCase("self-catalog")]
        [TestCase("number")]
        [TestCase("invalid-value")]
        [TestCase("duplicate-tuple")]
        [TestCase("overlapping-keys")]
        [TestCase("empty-when")]
        [TestCase("too-many")]
        public void TestVariantSelectorsAreClosedTypedAndCannotOverlap(string reason)
        {
            var fixture = new Package();
            fixture.RootManifest["options"] = JArray.Parse("""
                [{"id":"size","type":"choice","label":"Size","default":"normal",
                  "choices":[{"value":"normal","label":"Normal"},{"value":"long","label":"Thick"}]},
                 {"id":"enabled","type":"toggle","label":"Enabled","default":true},
                 {"id":"amount","type":"number","label":"Amount","default":1,"min":0,"max":2,"step":1}]
                """);
            JObject choice = new JObject
            {
                ["value"] = "standard",
                ["label"] = "Standard",
                ["variants"] = new JArray(new JObject { ["when"] = new JObject { ["size"] = "long" } }),
            };
            var variants = (JArray)choice["variants"]!;
            fixture.SetModule("notes", new[] { choice }, "standard");
            Assert.DoesNotThrow(() => fixture.Capture(), "The original variant must be valid before changing its selector.");
            switch (reason)
            {
                case "unknown": variants[0]!["when"] = new JObject { ["missing"] = "normal" }; break;
                case "self-catalog": variants[0]!["when"] = new JObject { ["notes"] = "standard" }; break;
                case "number": variants[0]!["when"] = new JObject { ["amount"] = "normal" }; break;
                case "invalid-value": variants[0]!["when"]!["size"] = "wide"; break;
                case "duplicate-tuple": variants.Add(variants[0]!.DeepClone()); break;
                case "overlapping-keys": variants.Add(new JObject { ["when"] = new JObject { ["size"] = "long", ["enabled"] = true } }); break;
                case "empty-when": variants[0]!["when"] = new JObject(); break;
                case "too-many":
                    for (int index = 0; index < GameplaySkinContentBudgets.MAX_VARIANTS_PER_CHOICE; index++)
                        variants.Add(new JObject { ["when"] = new JObject { ["size"] = "n" + index } });
                    break;
            }
            Assert.Throws<GameplaySkinScenePreparationException>(() => fixture.Capture());
        }

        [Test]
        public void TestOtherCatalogChoicesAreFrozenSelectorsWithoutRecursiveContributionEvaluation()
        {
            var fixture = new Package();
            foreach (string branch in new[] { "frame.base", "frame.adjusted", "note.base", "note.adjusted" })
                fixture.AddBranch(branch, new Rgba32(255, 0, 0), null);
            JObject frameChoice = fixture.Choice("hd", "frame.base");
            frameChoice["variants"] = new JArray { variant("frame.adjusted", new JObject { ["notes"] = "alternate" }) };
            JObject noteChoice = fixture.Choice("alternate", "note.base");
            noteChoice["variants"] = new JArray { variant("note.adjusted", new JObject { ["frame"] = "hd" }) };
            fixture.SetModule("frame", new[] { frameChoice }, "hd");
            fixture.SetModule("notes", new[] { new JObject { ["value"] = "native", ["label"] = "Native" }, noteChoice }, "alternate");
            GameplaySkinPreparedPackage captured = fixture.Capture();
            var saved = new Dictionary<string, string> { ["notes"] = "alternate" };
            GameplaySkinOptionValues frozen = GameplaySkinOptionValues.Resolve(captured.Manifest!.Options, saved, "bms", "7k");
            GameplaySkinPreparedPackage active = captured.Select(frozen, CancellationToken.None);
            saved["notes"] = "native";
            GameplaySkinPreparedPackage next = captured.Select(GameplaySkinOptionValues.Resolve(captured.Manifest.Options, saved, "bms", "7k"), CancellationToken.None);
            Assert.Multiple(() =>
            {
                Assert.That(active.Resources.Select(resource => resource.Source.Id), Is.EqualTo(new[] { "frame.adjusted.texture", "note.adjusted.texture" }));
                Assert.That(next.Resources.Select(resource => resource.Source.Id), Is.EqualTo(new[] { "frame.base.texture" }));
                Assert.That(frozen.Values["notes"].CanonicalValue, Is.EqualTo("alternate"));
            });
        }

        [Test]
        public void TestSixtyFourFrameAndSizeTuplesAreValidButTheSixtyFifthIsRejected()
        {
            var fixture = new Package();
            fixture.RootManifest["options"] = JArray.Parse("""
                [{"id":"size","type":"choice","label":"Size","default":"normal","choices":[
                  {"value":"normal","label":"Normal"},{"value":"long","label":"Long"},
                  {"value":"short","label":"Short"},{"value":"very-short","label":"Very short"}]}]
                """);
            fixture.AddBranch("sd", new Rgba32(255, 0, 0), null);
            fixture.AddBranch("hd", new Rgba32(0, 0, 255), null);
            fixture.SetModule("frame", Enumerable.Range(0, 17).Select(index => new JObject { ["value"] = "n" + index, ["label"] = "Frame " + index }), "n0");
            JObject choice = fixture.Choice("standard", "sd");
            string[] sizes = { "normal", "long", "short", "very-short" };
            var variants = new JArray();
            for (int frameIndex = 0; frameIndex < 16; frameIndex++)
                foreach (string size in sizes)
                    variants.Add(variant(frameIndex < 8 ? "sd" : "hd", new JObject { ["frame"] = "n" + frameIndex, ["size"] = size }));
            choice["variants"] = variants;
            fixture.SetModule("notes", new[] { choice }, "standard");
            GameplaySkinPreparedPackage captured = fixture.Capture();
            Assert.That(GameplaySkinContentBudgets.MAX_VARIANTS_PER_CHOICE, Is.EqualTo(64));
            Assert.That(variants, Has.Count.EqualTo(64));
            Assert.That(captured.Resources, Is.Empty);
            var saved = new Dictionary<string, string> { ["frame"] = "n15", ["size"] = "very-short" };
            GameplaySkinOptionValues frozen = GameplaySkinOptionValues.Resolve(captured.Manifest!.Options, saved, "bms", "7k");
            saved["frame"] = "n0";
            GameplaySkinPreparedPackage active = captured.Select(frozen, CancellationToken.None);
            GameplaySkinPreparedPackage next = captured.Select(GameplaySkinOptionValues.Resolve(captured.Manifest.Options, saved, "bms", "7k"), CancellationToken.None);
            Assert.That(active.Resources.Single().Source.Id, Is.EqualTo("hd.texture"));
            Assert.That(next.Resources.Single().Source.Id, Is.EqualTo("sd.texture"));
            Assert.That(frozen.Values["frame"].CanonicalValue, Is.EqualTo("n15"));
            // This is a new, otherwise valid selector tuple. It is refused solely by the directory's explicit cap.
            variants.Add(variant("hd", new JObject { ["frame"] = "n16", ["size"] = "normal" }));
            GameplaySkinScenePreparationException? failure = Assert.Throws<GameplaySkinScenePreparationException>(() => fixture.Capture());
            Assert.That(failure!.Code, Is.EqualTo(GameplaySkinSceneDiagnosticCode.BudgetExceeded));
        }

        private static JObject variant(string branch, JObject when) => new JObject
        {
            ["when"] = when,
            ["manifest"] = branch + "/manifest.json",
            ["scene"] = branch + "/scene.json",
            ["ini"] = branch + "/materials.ini",
        };

        private static GameplaySkinOptionValues resolve(GameplaySkinPreparedPackage package, string ruleset)
            => GameplaySkinOptionValues.Resolve(package.Manifest!.Options, null, ruleset, "7k");

        private sealed class TestSkin : Skin
        {
            public TestSkin(Guid id) : base(new SkinInfo("Content owner fixture") { ID = id }, null) { }
            public TestSkin(Guid id, IResourceStore<byte[]> store)
                : base(new SkinInfo("Content owner fixture") { ID = id }, null, store, "skin.ini", useExactPackageStore: true) { }
            public override ISample? GetSample(ISampleInfo sampleInfo) => null;
            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;
            public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup) => null;
        }

        private sealed class Package
        {
            public Dictionary<string, byte[]> Files { get; } = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            public JObject RootManifest { get; } = manifest();
            public JObject Catalog { get; } = new JObject { ["contract"] = GameplaySkinContentBudgets.CONTRACT_ID, ["modules"] = new JArray() };
            public bool ForbidReads { get; set; }

            public Package()
            {
                RootManifest["contentCatalog"] = "content/catalog.json";
                put(GameplaySkinSceneContracts.SCENE_FILE_NAME, scene("base"));
                Files["skin.ini"] = Encoding.UTF8.GetBytes("[General]\nName: Content fixture\n");
            }

            public GameplaySkinPreparedPackage Capture()
            {
                put(GameplaySkinSceneContracts.MANIFEST_FILE_NAME, RootManifest);
                put("content/catalog.json", Catalog);
                return GameplaySkinPreparedPackage.Prepare((path, _) =>
                {
                    if (ForbidReads)
                        throw new AssertionException("An active root must never reopen the captured package.");
                    return Files.GetValueOrDefault(path);
                }, "exact.package-revision", Guid.NewGuid(), null, CancellationToken.None);
            }

            public SkinPackageRevisionCapsule CreateCapsule()
            {
                put(GameplaySkinSceneContracts.MANIFEST_FILE_NAME, RootManifest);
                put("content/catalog.json", Catalog);
                SkinPackageRevisionCapsuleCreationResult result = SkinPackageRevisionCapsuleFactory.Create(
                    Files.Select(pair => SkinPackageCapturedEntry.CreateFile(pair.Key, pair.Value)).ToArray());
                Assert.That(result.IsSuccess, Is.True, result.RejectionReason.ToString());
                return result.Capsule!;
            }

            public JObject Choice(string value, string branch) => new JObject
            {
                ["value"] = value,
                ["label"] = "Choice " + value,
                ["manifest"] = branch + "/manifest.json",
                ["scene"] = branch + "/scene.json",
                ["ini"] = branch + "/materials.ini",
            };

            public void SetModule(string id, IEnumerable<JObject> choices, string defaultValue, string? ruleset = null)
            {
                var module = new JObject { ["id"] = id, ["label"] = id, ["default"] = defaultValue, ["choices"] = new JArray(choices) };
                if (ruleset != null)
                    module["rulesets"] = new JArray(ruleset);
                ((JArray)Catalog["modules"]!).Add(module);
            }

            public void AddBranch(string id, Rgba32 colour, double? layoutX)
            {
                JObject definition = manifest();
                definition["resources"] = new JArray(new JObject { ["id"] = id + ".texture", ["type"] = "texture", ["path"] = id + ".png", ["filter"] = "nearest" });
                definition["bitmapFonts"] = new JArray(new JObject
                {
                    ["id"] = id + ".font",
                    ["lineHeight"] = 8,
                    ["glyphs"] = new JArray(new JObject { ["character"] = "0", ["resource"] = id + ".texture", ["advance"] = 8 }),
                });
                if (layoutX.HasValue)
                {
                    definition["layouts"] = new JArray(new JObject
                    {
                        ["id"] = id + ".layout",
                        ["ruleset"] = "bms",
                        ["keymode"] = "7k",
                        ["presentation"] = "p1",
                        ["stages"] = new JArray { new JObject { ["rect"] = new JArray(layoutX.Value, 0, 0.2, 0.6), ["lanes"] = new JArray { new JArray(0, 1) } } },
                    });
                }
                put(id + "/manifest.json", definition);
                JObject document = scene(id);
                ((JArray)document["root"]!["children"]!).Add(new JObject
                {
                    ["id"] = id + ".sprite",
                    ["type"] = "sprite",
                    ["target"] = new JObject { ["kind"] = "global" },
                    ["resource"] = id + ".texture",
                    ["blend"] = "alpha",
                    ["properties"] = new JObject(),
                    ["effects"] = new JArray(),
                    ["children"] = new JArray(),
                });
                ((JArray)document["bindings"]!).Add(new JObject { ["id"] = id + ".binding", ["target"] = id + ".sprite", ["property"] = "opacity", ["source"] = "timing.progress" });
                put(id + "/scene.json", document);
                Files[id + "/materials.ini"] = Encoding.UTF8.GetBytes(layoutX.HasValue
                    ? "[Bms]\nKeymode: 7K\nPlayfieldWidth: " + layoutX.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                      + "\n[GameplaySkin.Common:1]\nTarget: Global ruleset=any keymode=any stage-mode=any\nhud.text: resource Provide \"" + id + ".png\"\n"
                    : string.Empty);
                using var image = new Image<Rgba32>(1, 1, colour);
                using var stream = new MemoryStream();
                image.SaveAsPng(stream);
                Files[id + ".png"] = stream.ToArray();
            }

            public void EditJson(string path, Action<JObject> edit)
            {
                JObject json = JObject.Parse(Encoding.UTF8.GetString(Files[path]));
                edit(json);
                put(path, json);
            }

            private void put(string path, JObject json) => Files[path] = Encoding.UTF8.GetBytes(json.ToString(Formatting.None));

            private static JObject manifest() => JObject.Parse("""
                {"contract":"oms-gameplay-skin-manifest.v1","scene":"gameplay-skin.scene.json",
                 "sceneContract":"oms-gameplay-skin-scene.v1","eventContract":"oms-gameplay-skin-event.v1","resources":[]}
                """);

            private static JObject scene(string id) => JObject.Parse("""
                {"contract":"oms-gameplay-skin-scene.v1","root":{"id":"ROOT","type":"container","target":{"kind":"global"},
                 "blend":"inherit","properties":{},"effects":[],"children":[]},
                 "tracks":[],"stateMachines":[],"bindings":[],"variants":[],"templates":[],"instances":[]}
                """.Replace("ROOT", id + ".root", StringComparison.Ordinal));
        }
    }
}
