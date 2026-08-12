// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.Skinning.IO;

namespace osu.Game.Tests.Skins.IO
{
    [TestFixture]
    public class SkinArchiveReaderTest
    {
        [Test]
        public async Task TestValidArchiveIsReplayable()
        {
            byte[] content = Encoding.UTF8.GetBytes("deterministic skin resource");
            using var reader = await open(BuildZip(new ZipEntry("skin.ini", content)));

            Assert.That(reader.Filenames, Is.EqualTo(new[] { "skin.ini" }));

            using Stream first = reader.GetStream("skin.ini");
            using Stream second = reader.GetStream("SKIN.INI");
            Assert.That(readAll(first), Is.EqualTo(content));
            Assert.That(readAll(second), Is.EqualTo(content));
        }

        [Test]
        public async Task TestNonSeekableSourceIsSpoolBoundAndDisposed()
        {
            byte[] content = BuildZip(new ZipEntry("skin.ini", Encoding.UTF8.GetBytes("[General]"))).ToArray();
            var source = new NonSeekableStream(new MemoryStream(content, writable: false));

            using (var reader = await open(source))
                Assert.That(reader.Filenames.Single(), Is.EqualTo("skin.ini"));

            Assert.That(source.IsDisposed, Is.True);
        }

        [Test]
        public async Task TestNonSeekableSpoolCancellationDisposesSource()
        {
            byte[] content = BuildZip(new ZipEntry("skin.ini", new byte[128 * 1024])).ToArray();
            using var cancellation = new CancellationTokenSource();
            var source = new CancelAfterFirstReadStream(new MemoryStream(content, writable: false), cancellation);

            Assert.That(async () =>
            {
                using SkinArchiveReader reader = await open(source, cancellation.Token);
            }, Throws.InstanceOf<OperationCanceledException>());
            Assert.That(source.IsDisposed, Is.True);
        }

        [Test]
        public void TestSeekableRawLengthGatePrecedesRead()
        {
            var source = new OversizedSeekableStream(SkinArchiveImportLimits.MAX_ARCHIVE_BYTES + 1);

            var exception = Assert.ThrowsAsync<SkinArchiveImportException>(async () =>
            {
                using SkinArchiveReader reader = await open(source);
            });
            Assert.That(exception!.Reason, Is.EqualTo(SkinArchiveRejectionReason.ArchiveByteBudgetExceeded));
            Assert.That(source.ReadAttempted, Is.False);
            Assert.That(source.IsDisposed, Is.True);
        }

        [TestCase("../escape.png")]
        [TestCase("folder/../../escape.png")]
        [TestCase("C:/escape.png")]
        [TestCase("/rooted.png")]
        [TestCase("folder/NUL.txt")]
        [TestCase("folder/name. ")]
        public Task TestInvalidWindowsPathRejected(string name) => assertRejected(
            BuildZip(new ZipEntry(name, new byte[] { 1 })),
            SkinArchiveRejectionReason.InvalidEntryName);

        [Test]
        public Task TestNfcCaseFoldCollisionRejected() => assertRejected(
            BuildZip(
                new ZipEntry("Folder/e\u0301.png", new byte[] { 1 }),
                new ZipEntry("folder/\u00c9.PNG", new byte[] { 2 })),
            SkinArchiveRejectionReason.DuplicateEntryPath);

        [Test]
        public Task TestFileAncestorConflictRejected() => assertRejected(
            BuildZip(
                new ZipEntry("resource", new byte[] { 1 }),
                new ZipEntry("resource/child.png", new byte[] { 2 })),
            SkinArchiveRejectionReason.PathTypeConflict);

        [Test]
        public Task TestEncryptedEntryRejectedBeforeZipReader() => assertRejected(
            BuildZip(new ZipEntry("skin.ini", new byte[] { 1 }) { Flags = 0x0801 }),
            SkinArchiveRejectionReason.EncryptedEntry);

        [Test]
        public Task TestUnixSymlinkRejectedBeforeZipReader() => assertRejected(
            BuildZip(new ZipEntry("linked.png", new byte[] { 1 })
            {
                VersionMadeBy = (3 << 8) | 20,
                ExternalAttributes = 0xA1FF0000,
            }),
            SkinArchiveRejectionReason.UnsupportedEntryKind);

        [Test]
        public Task TestWindowsReparseSemanticRejectedBeforeZipReader() => assertRejected(
            BuildZip(new ZipEntry("linked.png", new byte[] { 1 }) { ExternalAttributes = 0x0400 }),
            SkinArchiveRejectionReason.UnsupportedEntryKind);

        [Test]
        public Task TestZip64ExtraRejectedBeforeZipReader() => assertRejected(
            BuildZip(new ZipEntry("skin.ini", new byte[] { 1 }) { Extra = new byte[] { 1, 0, 0, 0 } }),
            SkinArchiveRejectionReason.Zip64Archive);

        [Test]
        public async Task TestActualCrcGateRunsOnEntryRead()
        {
            using var archive = BuildZip(new ZipEntry("resource.png", new byte[] { 1, 2, 3 }) { DeclaredCrc = 0x12345678 });
            using var reader = await open(archive);

            var exception = Assert.Throws<SkinArchiveImportException>(() => reader.GetStream("resource.png"));
            Assert.That(exception!.Reason, Is.EqualTo(SkinArchiveRejectionReason.CrcMismatch));
        }

        [Test]
        public async Task TestActualLengthGateRunsOnEntryRead()
        {
            using var archive = BuildZip(new ZipEntry("resource.png", new byte[] { 1, 2, 3 }) { DeclaredExpandedSize = 2 });
            using var reader = await open(archive);

            var exception = Assert.Throws<SkinArchiveImportException>(() => reader.GetStream("resource.png"));
            Assert.That(exception!.Reason, Is.EqualTo(SkinArchiveRejectionReason.ActualSizeMismatch));
        }

        [Test]
        public async Task TestZeroLengthDeclarationWithActualContentRejectedOnRead()
        {
            using var archive = BuildZip(new ZipEntry("resource.png", new byte[] { 1, 2, 3 })
            {
                DeclaredCompressedSize = 3,
                DeclaredExpandedSize = 0,
            });
            using var reader = await open(archive);

            var exception = Assert.Throws<SkinArchiveImportException>(() => reader.GetStream("resource.png"));
            Assert.That(exception!.Reason, Is.EqualTo(SkinArchiveRejectionReason.ActualSizeMismatch));
        }

        [Test]
        public async Task TestDataDescriptorWithUnknownLocalSizesUsesFrozenCentralDirectory()
        {
            byte[] content = { 1, 2, 3 };
            using var archive = BuildZip(new ZipEntry("resource.png", content)
            {
                Flags = 0x0808,
                LocalDeclaredCrc = 0,
                LocalDeclaredCompressedSize = 0,
                LocalDeclaredExpandedSize = 0,
            });
            using var reader = await open(archive);

            using Stream stream = reader.GetStream("resource.png");
            Assert.That(readAll(stream), Is.EqualTo(content));
        }

        [TestCase(true)]
        [TestCase(false)]
        public Task TestLocalAndCentralDirectoryMismatchRejected(bool truncate)
        {
            using var archive = BuildZip(new ZipEntry("resource.png", new byte[] { 1, 2, 3 })
            {
                LocalDeclaredExpandedSize = 2,
            });
            byte[] bytes = archive.ToArray();
            if (truncate)
                Array.Resize(ref bytes, bytes.Length - 1);

            return assertRejected(new MemoryStream(bytes, writable: false),
                truncate ? SkinArchiveRejectionReason.InvalidArchive : SkinArchiveRejectionReason.DeclaredSizeMismatch);
        }

        [Test]
        public async Task TestCancellationDuringEntryMaterialisation()
        {
            byte[] content = new byte[256 * 1024];
            using var cancellation = new CancellationTokenSource();
            using var archive = BuildZip(new ZipEntry("resource.bin", content));
            using var reader = await open(archive, cancellation.Token);
            cancellation.Cancel();

            Assert.That(() => reader.GetStream("resource.bin"), Throws.InstanceOf<OperationCanceledException>());
        }

        [Test]
        public Task TestEntryExpandedBudgetUsesFrozenDeclaration() => assertRejected(
            BuildZip(new ZipEntry("resource.png", Array.Empty<byte>())
            {
                DeclaredCompressedSize = 1,
                DeclaredExpandedSize = checked((uint)(SkinArchiveImportLimits.MAX_ENTRY_EXPANDED_BYTES + 1)),
            }),
            SkinArchiveRejectionReason.EntryExpandedByteBudgetExceeded);

        [Test]
        public Task TestCompressionRatioBudgetUsesFrozenDeclaration() => assertRejected(
            BuildZip(new ZipEntry("resource.png", new byte[] { 1 })
            {
                DeclaredExpandedSize = 2 * 1024 * 1024,
            }),
            SkinArchiveRejectionReason.CompressionRatioExceeded);

        [Test]
        public Task TestConfigurationAggregateBudgetUsesFrozenDeclaration()
        {
            var entries = Enumerable.Range(0, 17)
                                    .Select(i => new ZipEntry($"config-{i}.json", Array.Empty<byte>())
                                    {
                                        DeclaredCompressedSize = 1024 * 1024,
                                        DeclaredExpandedSize = 1024 * 1024,
                                    })
                                    .ToArray();

            return assertRejected(BuildZip(entries), SkinArchiveRejectionReason.ConfigurationByteBudgetExceeded);
        }

        [Test]
        public Task TestEntryCountBudgetReadFromEocd()
        {
            using var archive = BuildZip();
            byte[] bytes = archive.ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8, 2), checked((ushort)(SkinArchiveImportLimits.MAX_ENTRY_COUNT + 1)));
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10, 2), checked((ushort)(SkinArchiveImportLimits.MAX_ENTRY_COUNT + 1)));
            return assertRejected(new MemoryStream(bytes, writable: false), SkinArchiveRejectionReason.EntryCountBudgetExceeded);
        }

        [Test]
        public Task TestCentralDirectoryBudgetReadFromEocd()
        {
            using var archive = BuildZip();
            byte[] bytes = archive.ToArray();
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12, 4), checked((uint)(SkinArchiveImportLimits.MAX_CENTRAL_DIRECTORY_BYTES + 1)));
            return assertRejected(new MemoryStream(bytes, writable: false), SkinArchiveRejectionReason.CentralDirectoryBudgetExceeded);
        }

        [Test]
        public Task TestFileCountCannotBypassEntryCountBudget()
        {
            var entries = Enumerable.Range(0, SkinArchiveImportLimits.MAX_FILE_COUNT + 1)
                                    .Select(i => new ZipEntry($"{i}.png", Array.Empty<byte>()))
                                    .ToArray();
            // File and total-entry limits are intentionally equal. The cheaper EOCD entry-count gate must reject
            // this archive before central-directory enumeration reaches the redundant file-count defence.
            return assertRejected(BuildZip(entries), SkinArchiveRejectionReason.EntryCountBudgetExceeded);
        }

        [Test]
        public Task TestDepthAndRawNameBudgets()
        {
            string tooDeep = string.Join('/', Enumerable.Repeat("a", SkinArchiveImportLimits.MAX_DEPTH + 1)) + ".png";
            return assertRejected(BuildZip(new ZipEntry(tooDeep, Array.Empty<byte>())), SkinArchiveRejectionReason.EntryDepthBudgetExceeded);
        }

        [Test]
        public Task TestRawNameBudget()
        {
            string tooLong = new string('a', SkinArchiveImportLimits.MAX_RAW_NAME_BYTES + 1);
            return assertRejected(BuildZip(new ZipEntry(tooLong, Array.Empty<byte>())), SkinArchiveRejectionReason.EntryNameBudgetExceeded);
        }

        [Test]
        public Task TestShortenedPathIsRevalidated()
        {
            string prefix = new string('p', 250);
            string segment = new string('s', 256);
            return assertRejected(BuildZip(
                new ZipEntry($"{prefix}/{segment}/one.png", Array.Empty<byte>()),
                new ZipEntry($"{prefix}/{segment}/two.png", Array.Empty<byte>())),
                SkinArchiveRejectionReason.EntryNameBudgetExceeded);
        }

        [TestCase(true)]
        [TestCase(false)]
        public Task TestTotalDeclaredByteBudgets(bool expanded)
        {
            const uint perEntry = 64 * 1024 * 1024;
            var entries = Enumerable.Range(0, 9).Select(i => new ZipEntry($"{i}.bin", Array.Empty<byte>())
            {
                DeclaredCompressedSize = expanded ? 6 * 1024 * 1024u : perEntry,
                DeclaredExpandedSize = expanded ? perEntry : 6 * 1024 * 1024u,
            }).ToArray();

            return assertRejected(BuildZip(entries), expanded
                ? SkinArchiveRejectionReason.TotalExpandedByteBudgetExceeded
                : SkinArchiveRejectionReason.TotalCompressedByteBudgetExceeded);
        }

        [TestCase(null, "Legacy")]
        [TestCase("", "Legacy")]
        [TestCase("System.String, System.Private.CoreLib", "Legacy")]
        [TestCase("osu.Game.Skinning.DefaultSkin, osu.Game", "Triangles")]
        [TestCase("osu.Game.Skinning.ArgonSkin, osu.Game", "Argon")]
        [TestCase("osu.Game.Skinning.OmsSkin, osu.Game", "Oms")]
        public void TestInstantiationPolicyIsExact(string? value, string expected)
            => Assert.That(SkinArchiveInstantiationPolicy.Resolve(value).ToString(), Is.EqualTo(expected));

        [Test]
        public async Task TestSkinInfoPolicyIsResolvedBeforeFilesAreExposed()
        {
            const string json = "{\"InstantiationInfo\":\"System.String, System.Private.CoreLib\"}";
            using var archive = BuildZip(
                new ZipEntry("My Skin/skininfo.json", Encoding.UTF8.GetBytes(json)),
                new ZipEntry("My Skin/skin.ini", Encoding.UTF8.GetBytes("[General]")));
            using var reader = await open(archive);

            Assert.That(reader.InstantiationKind, Is.EqualTo(SkinArchiveInstantiationKind.Legacy));
            Assert.That(reader.Filenames, Is.EquivalentTo(new[] { "My Skin/skininfo.json", "My Skin/skin.ini" }));
        }

        private static async Task<SkinArchiveReader> open(Stream source, CancellationToken cancellationToken = default)
            => (SkinArchiveReader)await SkinArchiveReader.OpenAsync(new ImportTask(source, "test.osk"), cancellationToken);

        private static async Task assertRejected(Stream source, SkinArchiveRejectionReason expected)
        {
            try
            {
                using SkinArchiveReader reader = await open(source);
                Assert.Fail($"Archive was accepted; files: {string.Join(", ", reader.Filenames)}");
            }
            catch (SkinArchiveImportException exception)
            {
                Assert.That(exception.Reason, Is.EqualTo(expected));
            }
        }

        private static byte[] readAll(Stream stream)
        {
            using var result = new MemoryStream();
            stream.CopyTo(result);
            return result.ToArray();
        }

        internal static MemoryStream BuildZip(params ZipEntry[] entries)
        {
            var result = new MemoryStream();
            using var writer = new BinaryWriter(result, Encoding.UTF8, true);
            var offsets = new List<uint>(entries.Length);

            foreach (ZipEntry entry in entries)
            {
                byte[] name = Encoding.UTF8.GetBytes(entry.Name);
                uint crc = entry.DeclaredCrc ?? computeCrc(entry.Content);
                uint compressed = entry.DeclaredCompressedSize ?? checked((uint)entry.Content.Length);
                uint expanded = entry.DeclaredExpandedSize ?? checked((uint)entry.Content.Length);
                offsets.Add(checked((uint)result.Position));

                writer.Write(0x04034b50u);
                writer.Write(entry.VersionNeeded);
                writer.Write(entry.Flags);
                writer.Write(entry.Method);
                writer.Write(0u);
                writer.Write(entry.LocalDeclaredCrc ?? crc);
                writer.Write(entry.LocalDeclaredCompressedSize ?? compressed);
                writer.Write(entry.LocalDeclaredExpandedSize ?? expanded);
                writer.Write(checked((ushort)name.Length));
                writer.Write(checked((ushort)entry.Extra.Length));
                writer.Write(name);
                writer.Write(entry.Extra);
                writer.Write(entry.Content);
            }

            uint centralOffset = checked((uint)result.Position);

            for (int i = 0; i < entries.Length; i++)
            {
                ZipEntry entry = entries[i];
                byte[] name = Encoding.UTF8.GetBytes(entry.Name);
                uint crc = entry.DeclaredCrc ?? computeCrc(entry.Content);
                uint compressed = entry.DeclaredCompressedSize ?? checked((uint)entry.Content.Length);
                uint expanded = entry.DeclaredExpandedSize ?? checked((uint)entry.Content.Length);

                writer.Write(0x02014b50u);
                writer.Write(checked((ushort)entry.VersionMadeBy));
                writer.Write(entry.VersionNeeded);
                writer.Write(entry.Flags);
                writer.Write(entry.Method);
                writer.Write(0u);
                writer.Write(crc);
                writer.Write(compressed);
                writer.Write(expanded);
                writer.Write(checked((ushort)name.Length));
                writer.Write(checked((ushort)entry.Extra.Length));
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                writer.Write(entry.ExternalAttributes);
                writer.Write(offsets[i]);
                writer.Write(name);
                writer.Write(entry.Extra);
            }

            uint centralSize = checked((uint)result.Position - centralOffset);
            writer.Write(0x06054b50u);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write(checked((ushort)entries.Length));
            writer.Write(checked((ushort)entries.Length));
            writer.Write(centralSize);
            writer.Write(centralOffset);
            writer.Write((ushort)0);
            result.Position = 0;
            return result;
        }

        private static uint computeCrc(ReadOnlySpan<byte> content)
        {
            uint crc = uint.MaxValue;

            foreach (byte value in content)
            {
                crc ^= value;
                for (int bit = 0; bit < 8; bit++)
                    crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }

            return ~crc;
        }

        internal sealed class ZipEntry
        {
            public string Name { get; }
            public byte[] Content { get; }
            public ushort VersionNeeded { get; set; } = 20;
            public int VersionMadeBy { get; set; } = 20;
            public ushort Flags { get; set; } = 0x0800;
            public ushort Method { get; set; }
            public uint ExternalAttributes { get; set; }
            public byte[] Extra { get; set; } = Array.Empty<byte>();
            public uint? DeclaredCrc { get; set; }
            public uint? DeclaredCompressedSize { get; set; }
            public uint? DeclaredExpandedSize { get; set; }
            public uint? LocalDeclaredCrc { get; set; }
            public uint? LocalDeclaredCompressedSize { get; set; }
            public uint? LocalDeclaredExpandedSize { get; set; }

            public ZipEntry(string name, byte[] content)
            {
                Name = name;
                Content = content;
            }
        }

        private class NonSeekableStream : Stream
        {
            protected readonly Stream Inner;

            public bool IsDisposed { get; private set; }

            public NonSeekableStream(Stream inner)
            {
                Inner = inner;
            }

            public override int Read(byte[] buffer, int offset, int count) => Inner.Read(buffer, offset, count);

            public override int Read(Span<byte> buffer) => Inner.Read(buffer);

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => Inner.ReadAsync(buffer, offset, count, cancellationToken);

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
                => Inner.ReadAsync(buffer, cancellationToken);

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    IsDisposed = true;
                    Inner.Dispose();
                }

                base.Dispose(disposing);
            }

            public override void Flush() => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }
        }

        private sealed class CancelAfterFirstReadStream : NonSeekableStream
        {
            private readonly CancellationTokenSource cancellation;
            private int reads;

            public CancelAfterFirstReadStream(Stream inner, CancellationTokenSource cancellation)
                : base(inner)
            {
                this.cancellation = cancellation;
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                int read = Inner.Read(buffer.Span);
                if (++reads == 1)
                    cancellation.Cancel();
                return ValueTask.FromResult(read);
            }
        }

        private sealed class OversizedSeekableStream : Stream
        {
            private long position;

            public bool ReadAttempted { get; private set; }
            public bool IsDisposed { get; private set; }
            public override long Length { get; }

            public OversizedSeekableStream(long length)
            {
                Length = length;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                ReadAttempted = true;
                return 0;
            }

            protected override void Dispose(bool disposing)
            {
                IsDisposed = true;
                base.Dispose(disposing);
            }

            public override void Flush() => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => position = offset;
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;

            public override long Position
            {
                get => position;
                set => position = value;
            }
        }
    }
}
