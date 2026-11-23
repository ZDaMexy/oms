// Copyright (c) OMS contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Tests.Resources;
using osu.Game.Utils;

namespace osu.Game.Tests.Utils
{
    [TestFixture]
    public class TagLibUtilsTest
    {
        [Test]
        public void TestReadsAudioWithUppercaseExtension()
        {
            using var storage = new TemporaryNativeStorage(nameof(TagLibUtilsTest));
            const string filename = "audio.MP3";

            using (var source = TestResources.OpenResource("Samples/test-sample.mp3"))
            using (var destination = storage.CreateFileSafely(filename))
                source.CopyTo(destination);

            using var audio = TagLibUtils.GetTagLibFile(storage.GetFullPath(filename));
            Assert.That(audio.Properties.Duration, Is.GreaterThan(TimeSpan.Zero));
        }
    }
}
