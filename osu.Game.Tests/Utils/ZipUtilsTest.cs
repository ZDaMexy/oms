// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.IO.Compression;
using NUnit.Framework;
using osu.Game.Utils;

namespace osu.Game.Tests.Utils
{
    [TestFixture]
    public class ZipUtilsTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestArchiveProbePreservesCallerStream(bool hasEntry)
        {
            using var stream = new MemoryStream();

            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                if (hasEntry)
                {
                    using var entry = archive.CreateEntry("chart.txt").Open();
                    entry.WriteByte(1);
                }
            }

            Assert.That(ZipUtils.IsZipArchive(stream), Is.EqualTo(hasEntry));
            Assert.That(stream.CanRead, Is.True);
            Assert.That(stream.Position, Is.Zero);
            Assert.That(stream.ReadByte(), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void TestInvalidArchiveProbePreservesCallerStream()
        {
            using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

            Assert.That(ZipUtils.IsZipArchive(stream), Is.False);
            Assert.That(stream.CanRead, Is.True);
            Assert.That(stream.Position, Is.Zero);
            Assert.That(stream.ReadByte(), Is.EqualTo(1));
        }
    }
}
