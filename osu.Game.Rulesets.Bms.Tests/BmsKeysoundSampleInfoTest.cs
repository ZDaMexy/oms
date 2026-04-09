// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Audio;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsKeysoundSampleInfoTest
    {
        [Test]
        public void TestConstructorNormalisesFilename()
        {
            var info = new BmsKeysoundSampleInfo("BGM\\intro.wav");
            // ToStandardisedPath normalises separators but preserves case
            Assert.That(info.Filename, Is.EqualTo("BGM/intro.wav"));
        }

        [Test]
        public void TestConstructorThrowsOnPathTraversal()
        {
            Assert.That(() => new BmsKeysoundSampleInfo("../secret.wav"), Throws.ArgumentException);
        }

        [Test]
        public void TestConstructorThrowsOnEmptyFilename()
        {
            Assert.That(() => new BmsKeysoundSampleInfo(""), Throws.ArgumentException);
            Assert.That(() => new BmsKeysoundSampleInfo("   "), Throws.ArgumentException);
        }

        [TestCase("drum.wav", 100)]
        [TestCase("samples/kick.ogg", 80)]
        public void TestVolumeIsPreserved(string filename, int volume)
        {
            var info = new BmsKeysoundSampleInfo(filename, volume);
            Assert.That(info.Volume, Is.EqualTo(volume));
        }

        [Test]
        public void TestLookupNamesYieldsFilenameAndWithoutExtension()
        {
            var info = new BmsKeysoundSampleInfo("bgm/intro.wav");
            var names = info.LookupNames.ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(names, Has.Length.EqualTo(2));
                Assert.That(names[0], Is.EqualTo("bgm/intro.wav"));
                Assert.That(names[1], Is.EqualTo("bgm/intro"));
            });
        }

        [Test]
        public void TestLookupNamesWithoutExtensionYieldsSingleEntry()
        {
            var info = new BmsKeysoundSampleInfo("drum");
            var names = info.LookupNames.ToArray();

            Assert.That(names, Has.Length.EqualTo(1));
            Assert.That(names[0], Is.EqualTo("drum"));
        }

        [Test]
        public void TestWithPreservesFilenameAndAppliesNewVolume()
        {
            var original = new BmsKeysoundSampleInfo("kick.wav", 100);
            var copy = original.With(newVolume: 50);

            Assert.Multiple(() =>
            {
                Assert.That(copy, Is.InstanceOf<BmsKeysoundSampleInfo>());
                Assert.That(((BmsKeysoundSampleInfo)copy).Filename, Is.EqualTo("kick.wav"));
                Assert.That(copy.Volume, Is.EqualTo(50));
            });
        }

        [Test]
        public void TestEqualsSameFilenameAndVolume()
        {
            var a = new BmsKeysoundSampleInfo("kick.wav", 100);
            var b = new BmsKeysoundSampleInfo("kick.wav", 100);

            Assert.Multiple(() =>
            {
                Assert.That(a.Equals(b), Is.True);
                Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            });
        }

        [Test]
        public void TestEqualsCaseInsensitive()
        {
            var a = new BmsKeysoundSampleInfo("Kick.WAV");
            var b = new BmsKeysoundSampleInfo("kick.wav");

            Assert.Multiple(() =>
            {
                Assert.That(a.Equals(b), Is.True);
                Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            });
        }

        [Test]
        public void TestNotEqualDifferentFilename()
        {
            var a = new BmsKeysoundSampleInfo("kick.wav");
            var b = new BmsKeysoundSampleInfo("snare.wav");

            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void TestEqualDespiteDifferentVolume()
        {
            // HitSampleInfo.Equals does not compare volume — same sample at different volumes is equal
            var a = new BmsKeysoundSampleInfo("kick.wav", 100);
            var b = new BmsKeysoundSampleInfo("kick.wav", 50);

            Assert.That(a.Equals(b), Is.True);
        }

        [Test]
        public void TestEqualsNullReturnsFalse()
        {
            var a = new BmsKeysoundSampleInfo("kick.wav");
            Assert.That(a.Equals(null), Is.False);
        }

        [TestCase("valid.wav", true)]
        [TestCase("sub/folder/valid.ogg", true)]
        [TestCase("", false)]
        [TestCase(null, false)]
        [TestCase("   ", false)]
        [TestCase("../escape.wav", false)]
        [TestCase("..\\escape.wav", false)]
        public void TestTryCreate(string? filename, bool expectedSuccess)
        {
            bool result = BmsKeysoundSampleInfo.TryCreate(filename, out var sampleInfo);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(expectedSuccess));

                if (expectedSuccess)
                    Assert.That(sampleInfo, Is.Not.Null);
                else
                    Assert.That(sampleInfo, Is.Null);
            });
        }

        [TestCase("valid.wav", true)]
        [TestCase("sub\\folder\\valid.ogg", true)]
        [TestCase("", false)]
        [TestCase(null, false)]
        [TestCase("../escape.wav", false)]
        public void TestTryNormaliseFilename(string? filename, bool expectedSuccess)
        {
            bool result = BmsKeysoundSampleInfo.TryNormaliseFilename(filename, out string? normalised);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(expectedSuccess));

                if (expectedSuccess)
                {
                    Assert.That(normalised, Is.Not.Null);
                    Assert.That(normalised, Does.Not.Contain("\\"));
                }
                else
                {
                    Assert.That(normalised, Is.Null);
                }
            });
        }

        [Test]
        public void TestTryCreateWithCustomVolume()
        {
            bool result = BmsKeysoundSampleInfo.TryCreate("kick.wav", out var sampleInfo, 75);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(sampleInfo!.Volume, Is.EqualTo(75));
                Assert.That(sampleInfo.Filename, Is.EqualTo("kick.wav"));
            });
        }
    }
}
