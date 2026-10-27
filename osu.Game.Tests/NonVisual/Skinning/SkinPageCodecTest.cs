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
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Skinning.Pages;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class SkinPageCodecTest
    {
        internal static JObject PageJson() => JObject.Parse("""
            {"contract":"oms-skin-pages.v1","results":{"width":1920,"height":1080,
             "resources":[{"id":"frame","path":"pages/result.png","filtering":"nearest"}],
             "elements":[
              {"id":"frame","type":"sprite","x":0,"y":0,"width":1920,"height":1080,"resource":"frame"},
              {"id":"title","type":"text","x":80,"y":40,"width":1000,"height":80,"source":"beatmap.title","fontSize":36},
              {"id":"score","type":"text","x":80,"y":150,"width":500,"height":80,"source":"score.value","fontSize":48,"align":"right","colour":"ffeeddff"},
              {"id":"count","type":"text","x":80,"y":250,"width":500,"height":60,"source":"hit.perfect","fontSize":24}
             ]}}
            """);

        internal static byte[] DocumentBytes(JObject? document = null) => Encoding.UTF8.GetBytes((document ?? PageJson()).ToString(Formatting.None));

        internal static Dictionary<string, byte[]> PageFiles(JObject? document = null)
        {
            using var image = new Image<Rgba32>(4, 3, new Rgba32(23, 77, 131, 255));
            using var encoded = new MemoryStream();
            image.SaveAsPng(encoded);
            return new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [SkinPageContracts.FILE_NAME] = DocumentBytes(document),
                ["pages/result.png"] = encoded.ToArray(),
            };
        }

        [Test]
        public void TestClosedPageRetainsOrderedImmutableGeometryAndTypedSources()
        {
            JObject source = PageJson();
            SkinPageDocument document = SkinPageCodec.Decode(DocumentBytes(source));
            source["results"]!["width"] = 500;
            Assert.Multiple(() =>
            {
                Assert.That(document.Results!.Width, Is.EqualTo(1920));
                Assert.That(document.Results!.Elements.Select(element => element.Id), Is.EqualTo(new[] { "frame", "title", "score", "count" }));
                Assert.That(document.Results!.Elements[2].Alignment, Is.EqualTo(SkinPageTextAlignment.Right));
                Assert.That(document.Results!.Elements[2].Colour, Is.EqualTo(0xffeeddff));
                Assert.That(document.Results!.Resources[0].Nearest, Is.True);
                Assert.Throws<NotSupportedException>(() => ((IList<SkinPageElement>)document.Results!.Elements).Clear());
                Assert.Throws<NotSupportedException>(() => ((IList<SkinPageResource>)document.Results!.Resources).Clear());
            });
        }

        [TestCase("../result.png")]
        [TestCase("pages\\result.png")]
        [TestCase("C:/result.png")]
        [TestCase("/result.png")]
        [TestCase("pages/CON.png")]
        [TestCase("pages/result.dll")]
        public void TestResourcePathsStayInsideTheDeclaredPackage(string path)
        {
            JObject json = PageJson();
            json["results"]!["resources"]![0]!["path"] = path;
            Assert.Throws<InvalidDataException>(() => SkinPageCodec.Decode(DocumentBytes(json)));
        }

        [TestCase("option.layout")]
        [TestCase("object.note")]
        [TestCase("score.fake")]
        [TestCase("hit.ignore_hit")]
        [TestCase("hit.unknown")]
        [TestCase("file.pages/result.png")]
        public void TestTextCannotReadGameplayOrUndeclaredSources(string source)
        {
            JObject json = PageJson();
            json["results"]!["elements"]![1]!["source"] = source;
            Assert.Throws<InvalidDataException>(() => SkinPageCodec.Decode(DocumentBytes(json)));
        }

        [TestCase("contract")]
        [TestCase("unknown-field")]
        [TestCase("duplicate-id")]
        [TestCase("unknown-resource")]
        [TestCase("outside-canvas")]
        [TestCase("negative-opacity")]
        [TestCase("numeric-source")]
        [TestCase("text-budget")]
        [TestCase("element-type")]
        [TestCase("duplicate-statistics")]
        public void TestInvalidPageIsRejectedBeforeAnyResourceRead(string failure)
        {
            JObject json = PageJson();
            var elements = (JArray)json["results"]!["elements"]!;
            switch (failure)
            {
                case "contract": json["contract"] = "future"; break;
                case "unknown-field": json["results"]!["script"] = "arbitrary"; break;
                case "duplicate-id": elements[1]!["id"] = "frame"; break;
                case "unknown-resource": elements[0]!["resource"] = "missing"; break;
                case "outside-canvas": elements[0]!["x"] = 1; break;
                case "negative-opacity": elements[0]!["opacity"] = -1; break;
                case "numeric-source": elements[1]!["source"] = 42; break;
                case "element-type": elements.Add(42); break;
                case "duplicate-statistics":
                    var statistics = JObject.Parse("""{"id":"stats","type":"native-statistics","x":0,"y":0,"width":500,"height":500}""");
                    elements.Add(statistics);
                    JObject second = (JObject)statistics.DeepClone();
                    second["id"] = "stats2";
                    elements.Add(second);
                    break;
                case "text-budget":
                    for (int index = 0; index < 20; index++)
                    {
                        JObject text = (JObject)elements[1]!.DeepClone();
                        text["id"] = "large" + index;
                        text["fontSize"] = 128;
                        elements.Add(text);
                    }
                    break;
            }
            int resourceReads = 0;
            Assert.Throws<InvalidDataException>(() => SkinPageCodec.Prepare((path, _) =>
            {
                if (path == SkinPageContracts.FILE_NAME)
                    return DocumentBytes(json);
                resourceReads++;
                return null;
            }, "revision", CancellationToken.None));
            Assert.That(resourceReads, Is.Zero);
        }

        [Test]
        public void TestDuplicatePropertiesAreRejected()
            => Assert.Throws<InvalidDataException>(() => SkinPageCodec.Decode(Encoding.UTF8.GetBytes(
                """{"contract":"oms-skin-pages.v1","contract":"oms-skin-pages.v1","results":{}}""")));

        [TestCase("score.passed", true)]
        [TestCase("score.passed", false)]
        [TestCase("score.cleared", true)]
        [TestCase("score.cleared", false)]
        public void TestOutcomeVisibilityIsAnExactBooleanCondition(string source, bool expected)
        {
            JObject json = PageJson();
            json["results"]!["elements"]![0]!["visibleWhen"] = new JObject { ["source"] = source, ["value"] = expected };
            Assert.That(SkinPageCodec.Decode(DocumentBytes(json)).Results!.Elements[0].VisibilityCondition, Is.EqualTo(new SkinPageVisibilityCondition(source, expected)));
        }

        [TestCase("score.rank", true)]
        [TestCase("score.passed", "true")]
        [TestCase("score.passed", 1)]
        public void TestVisibilityCannotBecomeAnExpressionOrAnUntypedValue(string source, object expected)
        {
            JObject json = PageJson();
            json["results"]!["elements"]![0]!["visibleWhen"] = new JObject { ["source"] = source, ["value"] = JToken.FromObject(expected) };
            Assert.Throws<InvalidDataException>(() => SkinPageCodec.Decode(DocumentBytes(json)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestWholePackageCapturesPagesEvenWithoutGameplayDeclarationAndOutlivesItsCapsule(bool retained)
        {
            Dictionary<string, byte[]> files = PageFiles();
            var capture = SkinPackageRevisionCapsuleFactory.Create(files.Select(file => SkinPackageCapturedEntry.CreateFile(file.Key, file.Value)).ToArray());
            Assert.That(capture.IsSuccess, Is.True);
            GameplaySkinPreparedPackage prepared;
            string revision;
            using (var capsule = capture.Capsule!)
            using (var view = capsule.CreateResourceView())
            {
                revision = capsule.ContentRevision;
                prepared = GameplaySkinPreparedPackage.Prepare((path, _) =>
                {
                    if (retained)
                        Assert.That(path, Is.EqualTo(GameplaySkinSceneContracts.MANIFEST_FILE_NAME), "Page declarations and images must use the retained capsule.");
                    return view.Get(path);
                }, revision, Guid.NewGuid(), null, CancellationToken.None, retained ? capsule.CaptureResourceReference : null);
            }
            Array.Clear(files["pages/result.png"]);
            files.Clear();
            Assert.That(prepared.Manifest, Is.Null);
            Assert.That(prepared.Pages!.ContentRevision, Is.EqualTo(revision));
            using Image<Rgba32> image = prepared.Pages.Resources.Single().CreateImage();
            Assert.That((image.Width, image.Height), Is.EqualTo((4, 3)));
            Assert.That(image[0, 0], Is.EqualTo(new Rgba32(23, 77, 131, 255)));
        }

        [Test]
        public void TestRetainedPageSourceDoesNotFallBackForAnAbsentDeclarationOrImage()
        {
            Dictionary<string, byte[]> files = PageFiles();
            files.Remove(SkinPageContracts.FILE_NAME);
            var capture = SkinPackageRevisionCapsuleFactory.Create(files.Select(file => SkinPackageCapturedEntry.CreateFile(file.Key, file.Value)).ToArray());
            using (var capsule = capture.Capsule!)
            {
                GameplaySkinPreparedPackage absent = GameplaySkinPreparedPackage.Prepare((path, _) =>
                {
                    Assert.That(path, Is.EqualTo(GameplaySkinSceneContracts.MANIFEST_FILE_NAME));
                    return null;
                }, capsule.ContentRevision, Guid.NewGuid(), null, CancellationToken.None, capsule.CaptureResourceReference);
                Assert.That(absent.Pages, Is.Null);
            }
            files = PageFiles();
            files.Remove("pages/result.png");
            capture = SkinPackageRevisionCapsuleFactory.Create(files.Select(file => SkinPackageCapturedEntry.CreateFile(file.Key, file.Value)).ToArray());
            using (var capsule = capture.Capsule!)
            {
                Assert.Throws<InvalidDataException>(() => GameplaySkinPreparedPackage.Prepare((path, _) =>
                {
                    Assert.Fail("An incomplete retained page cannot fetch its image through another reader.");
                    return null;
                }, capsule.ContentRevision, Guid.NewGuid(), null, CancellationToken.None, capsule.CaptureResourceReference));
            }
        }

        [Test]
        public void TestRetiringAnIdenticalCapsuleCannotRetireTheActivePagesOrItsDecodedPixels()
        {
            Dictionary<string, byte[]> files = PageFiles();
            var entries = files.Select(file => SkinPackageCapturedEntry.CreateFile(file.Key, file.Value)).ToArray();
            using var first = SkinPackageRevisionCapsuleFactory.Create(entries).Capsule!;
            using var second = SkinPackageRevisionCapsuleFactory.Create(entries).Capsule!;
            Assert.That(first.ContentRevision, Is.EqualTo(second.ContentRevision));
            GameplaySkinPreparedPackage prepare(SkinPackageRevisionCapsule capsule) => GameplaySkinPreparedPackage.Prepare((path, _) =>
            {
                Assert.That(path, Is.EqualTo(GameplaySkinSceneContracts.MANIFEST_FILE_NAME));
                return null;
            }, capsule.ContentRevision, Guid.NewGuid(), null, CancellationToken.None, capsule.CaptureResourceReference);
            GameplaySkinPreparedPackage active = prepare(first);
            GameplaySkinPreparedPackage retired = prepare(second);
            second.Dispose();
            foreach (byte[] bytes in files.Values)
                Array.Clear(bytes);
            using Image<Rgba32> activePixels = active.Pages!.Resources.Single().CreateImage();
            using Image<Rgba32> retiredPixels = retired.Pages!.Resources.Single().CreateImage();
            Assert.That(activePixels[0, 0], Is.EqualTo(new Rgba32(23, 77, 131, 255)));
            Assert.That(retiredPixels[0, 0], Is.EqualTo(activePixels[0, 0]), "Decoded page pixels have independent ownership after their captured source retires.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestMissingOrInvalidDeclaredImageCannotProduceAPartialPage(bool corrupt)
        {
            Dictionary<string, byte[]> files = PageFiles();
            if (corrupt)
                files["pages/result.png"] = Encoding.UTF8.GetBytes("not image pixels");
            else
                files.Remove("pages/result.png");
            Assert.Throws<InvalidDataException>(() => GameplaySkinPreparedPackage.Prepare((path, _) => files.GetValueOrDefault(path),
                "revision", Guid.NewGuid(), null, CancellationToken.None));
        }

        [Test]
        public void TestAbsentPagesDoNotReadAnyPageImage()
        {
            var reads = new List<string>();
            GameplaySkinPreparedPackage prepared = GameplaySkinPreparedPackage.Prepare((path, _) =>
            {
                reads.Add(path);
                return null;
            }, "revision", Guid.NewGuid(), null, CancellationToken.None);
            Assert.That(prepared.Pages, Is.Null);
            Assert.That(reads, Is.EqualTo(new[] { SkinPageContracts.FILE_NAME, GameplaySkinSceneContracts.MANIFEST_FILE_NAME }));
        }
    }
}
