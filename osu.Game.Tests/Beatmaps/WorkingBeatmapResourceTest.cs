// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Testing;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Mania;

namespace osu.Game.Tests.Beatmaps
{
    [TestFixture]
    public class WorkingBeatmapResourceTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestFilesystemChartValidationWithoutPlaybackOrRealm(bool changeSource)
        {
            using var storage = new TemporaryNativeStorage($"filesystem-chart-resources-{Guid.NewGuid():N}");
            const string chart = "#TITLE Resource Test\n#BPM 150\n#00111:0100\n#00212:0001\n";
            byte[] source = Encoding.UTF8.GetBytes(chart);
            using (var stream = storage.GetStream("chart.bme", FileAccess.Write, FileMode.CreateNew))
                stream.Write(changeSource ? Encoding.UTF8.GetBytes(chart + "#00313:0101\n") : source);

            var info = new BeatmapInfo(new BmsRuleset().RulesetInfo)
            {
                LocalFilePath = "chart.bme",
                MD5Hash = Convert.ToHexString(MD5.HashData(source)).ToLowerInvariant(),
                BeatmapSet = new BeatmapSetInfo { FilesystemStoragePath = storage.GetFullPath("."), IsExternalFilesystemStorage = true },
            };
            using var empty = new ResourceStore<byte[]>();
            var cache = new WorkingBeatmapCache(null, null, empty, storage, empty, defaultBeatmap: new FlatWorkingBeatmap(new Beatmap()),
                customBeatmapLoaders: new[] { new BmsBeatmapLoader() });
            var working = cache.GetWorkingBeatmap(info);

            if (changeSource)
                Assert.That(working.Beatmap, Is.Not.TypeOf<BmsDecodedBeatmap>(), "Modified source bytes must still fail the MD5 check.");
            else
            {
                Assert.That(working.Beatmap, Is.TypeOf<BmsDecodedBeatmap>());
                Assert.That(new ManiaRuleset().CreateDifficultyCalculator(working).Calculate().StarRating, Is.GreaterThan(0));
            }
        }

        [Test]
        public void TestChartReadDoesNotCreatePlaybackOrImageStores()
        {
            using var storage = new TemporaryNativeStorage($"chart-only-resources-{Guid.NewGuid():N}");
            byte[] chart = Encoding.UTF8.GetBytes("#TITLE Resource Test\n#BPM 150\n#00111:0100\n");
            using (var stream = storage.GetStream("chart.bms", FileAccess.Write, FileMode.CreateNew))
                stream.Write(chart);

            var fallback = new Mock<IBeatmapResourceProvider>(MockBehavior.Strict);
            IBeatmapResourceProvider resources = new WorkingBeatmapCache.FilesystemBackedBeatmapResourceProvider(fallback.Object, storage);

            Assert.That(resources.Files.Get("chart.bms"), Is.EqualTo(chart));
            fallback.VerifyNoOtherCalls();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestImageStoreIsCreatedOnceOnFirstUse(bool panel)
        {
            using var storage = new TemporaryNativeStorage($"image-resources-{Guid.NewGuid():N}");
            var fallback = new Mock<IBeatmapResourceProvider>(MockBehavior.Strict);
            fallback.SetupGet(r => r.Renderer).Returns(new DummyRenderer());
            fallback.Setup(r => r.CreateTextureLoaderStore(It.IsAny<IResourceStore<byte[]>>())).Returns(new ResourceStore<TextureUpload>());
            IBeatmapResourceProvider resources = new WorkingBeatmapCache.FilesystemBackedBeatmapResourceProvider(fallback.Object, storage);

            fallback.VerifyNoOtherCalls();
            var stores = new TextureStore[32];
            Parallel.For(0, stores.Length, i => stores[i] = panel ? resources.BeatmapPanelTextureStore : resources.LargeTextureStore);

            Assert.That(stores, Is.All.SameAs(stores[0]));
            fallback.VerifyGet(r => r.Renderer, Times.Once);
            fallback.Verify(r => r.CreateTextureLoaderStore(It.IsAny<IResourceStore<byte[]>>()), Times.Once);
            fallback.VerifyNoOtherCalls();
            stores[0].Dispose();
        }

        [Test]
        public void TestTrackStoreIsCreatedOnceOnFirstUse()
        {
            using var storage = new TemporaryNativeStorage($"track-resources-{Guid.NewGuid():N}");
            using var empty = new ResourceStore<byte[]>();
            var audio = new AudioManager(new AudioThread(), empty, empty, null);
            var fallback = new Mock<IBeatmapResourceProvider>(MockBehavior.Strict);
            fallback.SetupGet(r => r.AudioManager).Returns(audio);
            IBeatmapResourceProvider resources = new WorkingBeatmapCache.FilesystemBackedBeatmapResourceProvider(fallback.Object, storage);

            fallback.VerifyNoOtherCalls();
            var stores = new ITrackStore[32];
            Parallel.For(0, stores.Length, i => stores[i] = resources.Tracks);

            Assert.That(stores, Is.All.SameAs(stores[0]));
            fallback.VerifyGet(r => r.AudioManager, Times.Once);
            fallback.VerifyNoOtherCalls();
            audio.Dispose();
        }
    }
}
