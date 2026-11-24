// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Skinning;
using osu.Game.Tests.Resources;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Skins
{
    [HeadlessTest]
    public partial class TestSceneSkinResources : OsuTestScene
    {
        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Test]
        public void TestManiaHitsoundsAndEffectsHaveIndependentVolume()
        {
            PoolableSkinnableSample hitsound = null!;
            PoolableSkinnableSample effect = null!;
            SampleChannel hitsoundChannel = null!;
            SampleChannel effectChannel = null!;
            double originalMaster = 0;
            double originalEffects = 0;
            double originalHitsounds = 0;

            AddStep("load a hitsound and effect from the same skin asset", () =>
            {
                originalMaster = Audio.Volume.Value;
                originalEffects = Audio.VolumeSample.Value;
                originalHitsounds = config.Get<double>(OsuSetting.ManiaHitsoundVolume);

                var skin = importSkinFromArchives(@"ogg-skin.osk");
                Add(new SkinProvidingContainer(skin)
                {
                    Children = new[]
                    {
                        hitsound = new PoolableSkinnableSample(new TestHitSampleInfo(@"sample", 50)),
                        effect = new PoolableSkinnableSample(new SampleInfo(@"sample")),
                    },
                });
            });

            AddUntilStep("both samples are loaded", () => hitsound.Sample != null && effect.Sample != null);
            AddStep("start both playback channels and set separate volumes", () =>
            {
                hitsoundChannel = hitsound.Sample.GetChannel();
                effectChannel = effect.Sample.GetChannel();
                hitsoundChannel.Looping = effectChannel.Looping = true;
                hitsoundChannel.Play();
                effectChannel.Play();

                Audio.Volume.Value = 0.8;
                Audio.VolumeSample.Value = 0.2;
                config.SetValue(OsuSetting.ManiaHitsoundVolume, 0.3);
            });
            AddUntilStep("hitsound uses master, hitsound and per-object volume", () => hitsoundChannel.AggregateVolume.Value, () => Is.EqualTo(0.12).Within(0.001));
            AddUntilStep("effect uses master and effect volume", () => effectChannel.AggregateVolume.Value, () => Is.EqualTo(0.16).Within(0.001));

            AddStep("mute effects and turn up the hitsounds", () =>
            {
                Audio.VolumeSample.Value = 0;
                config.SetValue(OsuSetting.ManiaHitsoundVolume, 0.7);
            });
            AddUntilStep("effect is muted", () => effectChannel.AggregateVolume.Value, () => Is.EqualTo(0));
            AddAssert("hitsound keeps playing on its own control", () => hitsoundChannel.AggregateVolume.Value, () => Is.EqualTo(0.28).Within(0.001));

            AddStep("restore fixture volumes", () =>
            {
                hitsoundChannel.Stop();
                effectChannel.Stop();
                Audio.Volume.Value = originalMaster;
                Audio.VolumeSample.Value = originalEffects;
                config.SetValue(OsuSetting.ManiaHitsoundVolume, originalHitsounds);
            });
        }

        [Test]
        public void TestRetrieveOggSample()
        {
            ISkin skin = null!;

            AddStep("import skin", () => skin = importSkinFromArchives(@"ogg-skin.osk"));
            AddAssert("sample is non-null", () => skin.GetSample(new SampleInfo(@"sample")) != null);
        }

        [Test]
        public void TestRetrievalWithConflictingFilenames()
        {
            ISkin skin = null!;

            AddStep("import skin", () => skin = importSkinFromArchives(@"conflicting-filenames-skin.osk"));
            AddAssert("texture is non-null", () => skin.GetTexture(@"spinner-osu") != null);
            AddAssert("sample is non-null", () => skin.GetSample(new SampleInfo(@"spinner-osu")) != null);
        }

        [Test]
        public void TestSampleRetrievalOrder()
        {
            Mock<IStorageResourceProvider> mockResourceProvider = null!;
            Mock<IResourceStore<byte[]>> mockResourceStore = null!;
            List<string> lookedUpFileNames = null!;

            AddStep("setup mock providers provider", () =>
            {
                lookedUpFileNames = new List<string>();
                mockResourceProvider = new Mock<IStorageResourceProvider>();
                mockResourceProvider.Setup(m => m.AudioManager).Returns(Audio);
                mockResourceProvider.Setup(m => m.Renderer).Returns(new DummyRenderer());
                mockResourceStore = new Mock<IResourceStore<byte[]>>();
                mockResourceStore.Setup(r => r.Get(It.IsAny<string>()))
                                 .Callback<string>(n => lookedUpFileNames.Add(n))
                                 .Returns<byte>(null);
            });

            AddStep("query sample", () =>
            {
                TestSkin testSkin = new TestSkin(new SkinInfo(), mockResourceProvider.Object, new ResourceStore<byte[]>(mockResourceStore.Object));
                testSkin.GetSample(new SampleInfo());
            });

            AddAssert("sample lookups were in correct order", () =>
            {
                string[] lookups = lookedUpFileNames.Where(f => f.StartsWith(TestSkin.SAMPLE_NAME, StringComparison.Ordinal)).ToArray();
                return Path.GetExtension(lookups[0]) == string.Empty
                       && Path.GetExtension(lookups[1]) == ".wav"
                       && Path.GetExtension(lookups[2]) == ".mp3"
                       && Path.GetExtension(lookups[3]) == ".ogg";
            });
        }

        private Skin importSkinFromArchives(string filename)
        {
            var imported = skins.Import(new ImportTask(TestResources.OpenResource($@"Archives/{filename}"), filename)).GetResultSafely();
            return imported.PerformRead(skinInfo => skins.GetSkin(skinInfo));
        }

        private class TestSkin : Skin
        {
            public const string SAMPLE_NAME = "test-sample";

            public TestSkin(SkinInfo skin, IStorageResourceProvider? resources, IResourceStore<byte[]>? fallbackStore = null, string configurationFilename = "skin.ini")
                : base(skin, resources, fallbackStore, configurationFilename)
            {
            }

            public override Texture GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => throw new NotImplementedException();

            public override IBindable<TValue> GetConfig<TLookup, TValue>(TLookup lookup) => throw new NotImplementedException();

            public override ISample GetSample(ISampleInfo sampleInfo) => Samples.AsNonNull().Get(SAMPLE_NAME);
        }

        private sealed class TestHitSampleInfo : HitSampleInfo
        {
            public TestHitSampleInfo(string name, int volume)
                : base(name, volume: volume)
            {
            }

            public override IEnumerable<string> LookupNames => [Name];
        }
    }
}
