// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Rulesets.Bms.Beatmaps;

namespace osu.Game.Rulesets.Bms.Tests.Beatmaps
{
    [TestFixture]
    [NonParallelizable]
    public class BmsArchiveReaderSecurityTest
    {
        private const string chart = "#TITLE Local archive\n#BPM 150\n#00119:0100\n";
        private const uint chart_limit = 32 * 1024 * 1024;

        private readonly BmsArchiveReader reader = new BmsArchiveReader();
        private string[] existingTemporaryImports = null!;

        [SetUp]
        public void SetUp() => existingTemporaryImports = temporaryImports();

        [TearDown]
        public void TearDown() => Assert.That(temporaryImports(), Is.EquivalentTo(existingTemporaryImports), "Rejected and disposed imports must remove only their temporary files.");

        [TestCase(false)]
        [TestCase(true)]
        public void TestArchiveStreamIsExtractedAndDisposed(bool nonSeekable)
        {
            byte[] bytes = createZip(("Pack/Song/chart.bme", chart), ("Pack/Song/sound.wav", "audio"));
            using MemoryStream input = nonSeekable ? new NonSeekableInput(bytes) : new MemoryStream(bytes);
            string? cleanupPath;

            using (var prepared = reader.Prepare(new ImportTask(input, "local.zip")))
            {
                cleanupPath = prepared.CleanupPath;
                Assert.Multiple(() =>
                {
                    Assert.That(prepared.FolderTasks, Has.Count.EqualTo(1));
                    Assert.That(File.ReadAllText(Path.Combine(prepared.FolderTasks.Single().Path, "chart.bme")), Is.EqualTo(chart));
                    Assert.That(File.ReadAllText(Path.Combine(prepared.FolderTasks.Single().Path, "sound.wav")), Is.EqualTo("audio"));
                    Assert.That(prepared.DeleteSourceOnSuccess, Is.False);
                    Assert.That(input.CanRead, Is.False);
                });
            }

            Assert.That(Directory.Exists(cleanupPath!), Is.False);
        }

        [TestCase("../outside.bms")]
        [TestCase("/outside.bms")]
        [TestCase("Song/../outside.bms")]
        [TestCase("Song/sound.wav:payload")]
        [TestCase("Song/CON.txt")]
        [TestCase("Song/trailing./chart.bms")]
        public void TestInvalidWindowsPathsAreRejected(string name)
        {
            using var input = new MemoryStream(createZip(("Song/chart.bme", chart), (name, "data")));
            Assert.Throws<InvalidDataException>(() => reader.Prepare(new ImportTask(input, "local.zip")));
        }

        [TestCase("Song/sound.wav", "song/sound.wav")]
        [TestCase("Song/sound.wav", "Song/sound.wav")]
        [TestCase("Song/assets", "Song/assets/stage.png")]
        public void TestDuplicateOrConflictingPathsAreRejected(string first, string second)
        {
            using var input = new MemoryStream(createZip(("Song/chart.bme", chart), (first, "one"), (second, "two")));
            Assert.Throws<InvalidDataException>(() => reader.Prepare(new ImportTask(input, "local.zip")));
        }

        [TestCase(unchecked((int)0xa0000000))]
        [TestCase((int)FileAttributes.ReparsePoint)]
        public void TestArchiveLinksAreRejected(int attributes)
        {
            using var bytes = new MemoryStream();
            using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, true))
            {
                var entry = zip.CreateEntry("Song/link.bme");
                entry.ExternalAttributes = attributes;
                using var output = new StreamWriter(entry.Open());
                output.Write(chart);
            }

            using var input = new MemoryStream(bytes.ToArray());
            Assert.Throws<InvalidDataException>(() => reader.Prepare(new ImportTask(input, "local.zip")));
        }

        [Test]
        public void TestOversizedCompressedSourceIsRejectedBeforeReading()
        {
            using var input = new OversizedArchiveInput(createZip(("Song/chart.bme", chart)));
            Assert.Throws<InvalidDataException>(() => reader.Prepare(new ImportTask(input, "local.zip")));
            Assert.That(input.ReadCount, Is.Zero);
        }

        [Test]
        public void TestDeclaredChartBudgetIsCheckedBeforeExtracting()
        {
            byte[] bytes = createZip(("Song/chart.bme", chart));
            setDeclaredSize(bytes, chart_limit + 1);
            using var input = new MemoryStream(bytes);
            Assert.Throws<InvalidDataException>(() => reader.Prepare(new ImportTask(input, "local.zip")));
        }

        [Test]
        public void TestExpandedBytesCannotExceedForgedChartDeclaration()
        {
            using var bytes = new MemoryStream();
            using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, true))
            using (var output = zip.CreateEntry("Song/chart.bme").Open())
            {
                byte[] buffer = new byte[4096];

                for (long remaining = chart_limit + 1; remaining > 0;)
                {
                    int count = (int)Math.Min(remaining, buffer.Length);
                    output.Write(buffer, 0, count);
                    remaining -= count;
                }
            }

            byte[] forged = bytes.ToArray();
            setDeclaredSize(forged, chart_limit);
            using var input = new MemoryStream(forged);
            Assert.Throws<InvalidDataException>(() => reader.Prepare(new ImportTask(input, "local.zip")));
        }

        [Test]
        public void TestNonSeekableChartInputHasAnActualByteBudget()
        {
            using var input = new RepeatingInput(chart_limit + 1);
            Assert.Throws<InvalidDataException>(() => reader.Prepare(new ImportTask(input, "chart.bme")));
            Assert.Multiple(() =>
            {
                Assert.That(input.BytesRead, Is.EqualTo(chart_limit + 1));
                Assert.That(input.Disposed, Is.True);
            });
        }

        [Test]
        public void TestCancellationDuringSpoolingPreservesCancellationAndCleansUp()
        {
            using var cancellation = new CancellationTokenSource();
            using var input = new NonSeekableInput(createZip(("Song/chart.bme", chart)), cancellation.Cancel);
            var error = Assert.Throws<OperationCanceledException>(() => reader.Prepare(new ImportTask(input, "local.zip"), cancellation.Token));

            Assert.Multiple(() =>
            {
                Assert.That(error!.CancellationToken, Is.EqualTo(cancellation.Token));
                Assert.That(input.ReadCount, Is.EqualTo(1));
                Assert.That(input.CanRead, Is.False);
            });
        }

        [Test]
        public void TestRejectedFileIsPreservedAndItsHandleReleased()
        {
            using var storage = new TemporaryNativeStorage($"local-bms-archive-{Guid.NewGuid():N}");
            string path = storage.GetFullPath("source.zip");
            byte[] bytes = createZip(("../outside.bme", chart));
            File.WriteAllBytes(path, bytes);

            Assert.Throws<InvalidDataException>(() => reader.Prepare(new ImportTask(path)));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
            using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.That(exclusive.Length, Is.EqualTo(bytes.Length));
        }

        private static byte[] createZip(params (string Name, string Content)[] entries)
        {
            using var bytes = new MemoryStream();

            using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, true))
            {
                foreach (var entry in entries)
                {
                    using var writer = new StreamWriter(zip.CreateEntry(entry.Name).Open(), Encoding.UTF8);
                    writer.Write(entry.Content);
                }
            }

            return bytes.ToArray();
        }

        private static void setDeclaredSize(byte[] bytes, uint size)
        {
            for (int offset = 0; offset <= bytes.Length - 46; offset++)
            {
                if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset)) != 0x02014b50)
                    continue;

                int local = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 42)));
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(local + 22), size);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset + 24), size);
                return;
            }

            throw new InvalidOperationException("The fixture has no ZIP central-directory entry.");
        }

        private static string[] temporaryImports()
        {
            string root = Path.Combine(Path.GetTempPath(), "oms-bms-import");
            return Directory.Exists(root) ? Directory.GetDirectories(root) : Array.Empty<string>();
        }

        private sealed class NonSeekableInput : MemoryStream
        {
            private readonly Action? afterRead;
            public int ReadCount { get; private set; }
            public override bool CanSeek => false;

            public NonSeekableInput(byte[] bytes, Action? afterRead = null)
                : base(bytes)
            {
                this.afterRead = afterRead;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                ReadCount++;
                int result = base.Read(buffer, offset, count);
                afterRead?.Invoke();
                return result;
            }
        }

        private sealed class OversizedArchiveInput : MemoryStream
        {
            public int ReadCount { get; private set; }
            public override long Length => 2L * 1024 * 1024 * 1024 + 1;

            public OversizedArchiveInput(byte[] bytes)
                : base(bytes)
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                ReadCount++;
                return base.Read(buffer, offset, count);
            }
        }

        private sealed class RepeatingInput : Stream
        {
            private readonly long size;
            public long BytesRead { get; private set; }
            public bool Disposed { get; private set; }

            public RepeatingInput(long size) => this.size = size;

            public override int Read(byte[] buffer, int offset, int count)
            {
                int read = (int)Math.Min(size - BytesRead, count);
                Array.Clear(buffer, offset, read);
                BytesRead += read;
                return read;
            }

            protected override void Dispose(bool disposing)
            {
                Disposed = true;
                base.Dispose(disposing);
            }

            public override bool CanRead => !Disposed;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }
            public override void Flush() => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
