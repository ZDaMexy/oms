// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Skinning.Pages;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class SkinSongSelectPageCodecTest
    {
        internal static JObject SongDocument(bool withResults = false)
        {
            JObject document = withResults ? SkinPageCodecTest.PageJson() : new JObject { ["contract"] = SkinPageContracts.CONTRACT };
            document["songSelect"] = new JObject
            {
                ["resources"] = new JArray(new[] { "background", "edge", "row", "selected", "brand" }.Select(name => new JObject
                {
                    ["id"] = "ss-" + name,
                    ["path"] = "pages/songselect/" + name + ".png",
                })),
                ["background"] = "ss-background",
                ["listEdge"] = "ss-edge",
                ["row"] = "ss-row",
                ["selectedRow"] = "ss-selected",
                ["brand"] = "ss-brand",
            };
            return document;
        }

        internal static Dictionary<string, byte[]> SongFiles(JObject? document = null)
        {
            document ??= SongDocument();
            var original = SkinPageCodecTest.PageFiles();
            var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
            {
                [SkinPageContracts.FILE_NAME] = Encoding.UTF8.GetBytes(document.ToString()),
            };
            foreach (var page in new[] { document["results"], document["songSelect"] }.Where(page => page != null))
                foreach (var resource in page!["resources"]!)
                {
                    byte[] pixels = original["pages/result.png"].ToArray();
                    if (resource["id"]!.Value<string>() == "ss-selected")
                    {
                        using var selected = new Image<Rgba32>(7, 5, new Rgba32(11, 199, 233, 255));
                        using var stream = new MemoryStream();
                        selected.SaveAsPng(stream);
                        pixels = stream.ToArray();
                    }
                    files.Add(resource["path"]!.Value<string>()!, pixels);
                }
            return files;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestOptionalResultsAndSongSelectShareOneBoundedDocument(bool withResults)
        {
            SkinPageDocument document = SkinPageCodec.Decode(Encoding.UTF8.GetBytes(SongDocument(withResults).ToString()));
            Assert.That(document.Results != null, Is.EqualTo(withResults));
            Assert.That(document.SongSelect!.SelectedRow, Is.EqualTo("ss-selected"));
            Assert.That(document.Resources.Count, Is.EqualTo(withResults ? 6 : 5));
        }

        [TestCase("tracks")]
        [TestCase("actions")]
        [TestCase("elements")]
        [TestCase("width")]
        public void TestNativeChromeDoesNotAdmitAnAuthorListActionsOrLayout(string property)
        {
            JObject document = SongDocument();
            document["songSelect"]![property] = new JArray();
            Assert.Throws<InvalidDataException>(() => SkinPageCodec.Decode(Encoding.UTF8.GetBytes(document.ToString())));
        }

        [TestCase("background")]
        [TestCase("listEdge")]
        [TestCase("row")]
        [TestCase("selectedRow")]
        public void TestEveryRequiredNativeHostHasADeclaredReference(string slot)
        {
            JObject document = SongDocument();
            document["songSelect"]![slot] = "missing";
            Assert.Throws<InvalidDataException>(() => SkinPageCodec.Decode(Encoding.UTF8.GetBytes(document.ToString())));
        }

        [Test]
        public void TestAtLeastOnePageAndEveryCopiedResourceMustBeUsed()
        {
            Assert.Throws<InvalidDataException>(() => SkinPageCodec.Decode(Encoding.UTF8.GetBytes("{\"contract\":\"oms-skin-pages.v1\"}")));
            JObject document = SongDocument();
            ((JObject)document["songSelect"]!).Remove("brand");
            Assert.Throws<InvalidDataException>(() => SkinPageCodec.Decode(Encoding.UTF8.GetBytes(document.ToString())));
            ((JArray)document["songSelect"]!["resources"]!).RemoveAt(4);
            Assert.That(SkinPageCodec.Decode(Encoding.UTF8.GetBytes(document.ToString())).SongSelect!.Brand, Is.Null);
        }

        [Test]
        public void TestBothPagesConsumeTheSame32ResourceBudget()
        {
            JObject document = SongDocument(true);
            var page = (JObject)document["results"]!;
            JObject prototype = (JObject)page["elements"]![0]!;
            page["resources"] = new JArray(Enumerable.Range(0, 28).Select(index => new JObject
            {
                ["id"] = "result-" + index,
                ["path"] = "pages/result-" + index + ".png",
            }));
            page["elements"] = new JArray(Enumerable.Range(0, 28).Select(index =>
            {
                var element = (JObject)prototype.DeepClone();
                element["id"] = "result-" + index;
                element["resource"] = "result-" + index;
                return element;
            }));
            Assert.Throws<InvalidDataException>(() => SkinPageCodec.Decode(Encoding.UTF8.GetBytes(document.ToString())));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestSongOnlyAndCombinedPagesRetainAllExactPixelsAfterCapsuleRetires(bool withResults)
        {
            var files = SongFiles(SongDocument(withResults));
            var capture = SkinPackageRevisionCapsuleFactory.Create(files.Select(file => SkinPackageCapturedEntry.CreateFile(file.Key, file.Value)).ToArray());
            GameplaySkinPreparedPackage prepared;
            using (var capsule = capture.Capsule!)
            {
                prepared = GameplaySkinPreparedPackage.Prepare((path, _) =>
                {
                    Assert.That(path, Is.EqualTo(GameplaySkinSceneContracts.MANIFEST_FILE_NAME));
                    return null;
                }, capsule.ContentRevision, Guid.NewGuid(), null, CancellationToken.None, capsule.CaptureResourceReference);
            }
            foreach (var bytes in files.Values)
                Array.Clear(bytes);
            Assert.That(prepared.Manifest, Is.Null);
            Assert.That(prepared.Pages!.Resources.Count, Is.EqualTo(withResults ? 6 : 5));
            foreach (var resource in prepared.Pages.Resources)
            {
                using var image = resource.CreateImage();
                Assert.That((image.Width, image.Height), Is.EqualTo(resource.Source.Id == "ss-selected" ? (7, 5) : (4, 3)));
            }
        }

        [Test]
        public void TestAnAbsentSongImageCannotBeBorrowedFromAnotherSource()
        {
            var files = SongFiles();
            files.Remove("pages/songselect/selected.png");
            var capture = SkinPackageRevisionCapsuleFactory.Create(files.Select(file => SkinPackageCapturedEntry.CreateFile(file.Key, file.Value)).ToArray());
            using var capsule = capture.Capsule!;
            Assert.Throws<InvalidDataException>(() => GameplaySkinPreparedPackage.Prepare((path, _) =>
            {
                Assert.That(path, Is.EqualTo(GameplaySkinSceneContracts.MANIFEST_FILE_NAME));
                return null;
            }, capsule.ContentRevision, Guid.NewGuid(), null, CancellationToken.None, capsule.CaptureResourceReference));
        }
    }
}
