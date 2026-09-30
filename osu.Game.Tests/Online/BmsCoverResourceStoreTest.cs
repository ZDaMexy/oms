// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Online.Bms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class BmsCoverResourceStoreTest
    {
        private const string cover_url = "https://gingerrush.com/covers/example.png";

        [TestCase("https://gingerrush.com/covers/example.png")]
        [TestCase("https://pixeldrain.net/api/file/example/thumbnail")]
        [TestCase("https://bms.alvorna.com/covers/example.png")]
        public async Task TestAllowedSourcesReturnDecodableCover(string url)
        {
            byte[] bytes = createImage(3, 2);
            var handler = new TestHttpHandler((_, _) => Task.FromResult(createResponse(bytes)));
            using var store = new BmsCoverResourceStore(handler);

            byte[] result = await store.GetAsync(url).ConfigureAwait(false);

            using Image<Rgba32> image = Image.Load<Rgba32>(result);
            Assert.Multiple(() =>
            {
                Assert.That(image.Width, Is.EqualTo(3));
                Assert.That(image.Height, Is.EqualTo(2));
                Assert.That(handler.LastUri, Is.EqualTo(new Uri(url)));
                Assert.That(handler.Requests, Is.EqualTo(1));
            });
        }

        [TestCase("")]
        [TestCase("covers/example.png")]
        [TestCase("file:///F:/oms/example.png")]
        [TestCase("http://gingerrush.com/example.png")]
        [TestCase("https://user:password@gingerrush.com/example.png")]
        [TestCase("https://gingerrush.com:8443/example.png")]
        [TestCase("https://localhost/example.png")]
        [TestCase("https://127.0.0.1/example.png")]
        [TestCase("https://[::1]/example.png")]
        [TestCase("https://gingerrush.com.example.org/example.png")]
        [TestCase("https://example.org/example.png")]
        public async Task TestUnsupportedAddressMakesNoRequest(string url)
        {
            var handler = new TestHttpHandler((_, _) => throw new InvalidOperationException("An unsupported cover address must not reach the HTTP handler."));
            using var store = new BmsCoverResourceStore(handler);

            Assert.That(await store.GetAsync(url).ConfigureAwait(false), Is.Null);
            Assert.That(handler.Requests, Is.Zero);
        }

        [TestCase(HttpStatusCode.NotFound)]
        [TestCase(HttpStatusCode.Forbidden)]
        [TestCase(HttpStatusCode.Redirect)]
        public async Task TestUnavailableResponseHasNoCover(HttpStatusCode status)
        {
            var handler = new TestHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)
            {
                Headers = { Location = new Uri("https://localhost/redirect.png") },
                Content = new ByteArrayContent(createImage(1, 1)),
            }));
            using var store = new BmsCoverResourceStore(handler);

            Assert.That(await store.GetAsync(cover_url).ConfigureAwait(false), Is.Null);
            Assert.That(handler.Requests, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task TestActualBytesEnforceBudgetEvenWithMissingOrFalseLength(bool falseLength)
        {
            const int max_bytes = 8 * 1024 * 1024;
            using var stream = new BudgetProbeStream(max_bytes + 1024 * 1024);
            var handler = new TestHttpHandler((_, _) =>
            {
                var content = new StreamContent(stream);
                if (falseLength)
                    content.Headers.ContentLength = 1;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            });
            using var store = new BmsCoverResourceStore(handler);

            Assert.That(await store.GetAsync(cover_url).ConfigureAwait(false), Is.Null);
            Assert.That(stream.BytesRead, Is.EqualTo(max_bytes + 1));
        }

        [Test]
        public async Task TestDeclaredOversizedCoverIsNotRead()
        {
            using var stream = new BudgetProbeStream(1);
            var handler = new TestHttpHandler((_, _) =>
            {
                var content = new StreamContent(stream);
                content.Headers.ContentLength = 8 * 1024 * 1024 + 1;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            });
            using var store = new BmsCoverResourceStore(handler);

            Assert.That(await store.GetAsync(cover_url).ConfigureAwait(false), Is.Null);
            Assert.That(stream.BytesRead, Is.Zero);
        }

        [TestCase(8193, 1)]
        [TestCase(4097, 4097)]
        public async Task TestOversizedImageHasNoCover(int width, int height)
        {
            byte[] bytes = createImage(width, height);
            var handler = new TestHttpHandler((_, _) => Task.FromResult(createResponse(bytes)));
            using var store = new BmsCoverResourceStore(handler);

            Assert.That(await store.GetAsync(cover_url).ConfigureAwait(false), Is.Null);
        }

        [Test]
        public async Task TestMaximumWidthIsAccepted()
        {
            byte[] bytes = createImage(8192, 1);
            var handler = new TestHttpHandler((_, _) => Task.FromResult(createResponse(bytes)));
            using var store = new BmsCoverResourceStore(handler);

            byte[] result = await store.GetAsync(cover_url).ConfigureAwait(false);

            Assert.That(Image.Identify(result)!.Width, Is.EqualTo(8192));
        }

        [Test]
        public async Task TestInvalidImageHasNoCover()
        {
            var handler = new TestHttpHandler((_, _) => Task.FromResult(createResponse(new byte[] { 1, 2, 3 })));
            using var store = new BmsCoverResourceStore(handler);

            Assert.That(await store.GetAsync(cover_url).ConfigureAwait(false), Is.Null);
        }

        [Test]
        public async Task TestIncompleteImageHasNoCover()
        {
            byte[] bytes = createImage(3, 2);
            var handler = new TestHttpHandler((_, _) => Task.FromResult(createResponse(bytes[..33])));
            using var store = new BmsCoverResourceStore(handler);

            Assert.That(await store.GetAsync(cover_url).ConfigureAwait(false), Is.Null);
        }

        [Test]
        public async Task TestAnimatedCoverReturnsOnlyOneStaticFrame()
        {
            using var animated = new Image<Rgba32>(2, 2);
            animated.Frames.AddFrame(animated.Frames.RootFrame);
            using var encoded = new MemoryStream();
            animated.SaveAsGif(encoded);
            var handler = new TestHttpHandler((_, _) => Task.FromResult(createResponse(encoded.ToArray())));
            using var store = new BmsCoverResourceStore(handler);

            byte[] result = await store.GetAsync(cover_url).ConfigureAwait(false);

            using Image<Rgba32> cover = Image.Load<Rgba32>(result);
            Assert.That(cover.Frames.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task TestNetworkFailureHasNoCover()
        {
            var handler = new TestHttpHandler((_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("Expected network failure.")));
            using var store = new BmsCoverResourceStore(handler);

            Assert.That(await store.GetAsync(cover_url).ConfigureAwait(false), Is.Null);
        }

        [Test]
        public async Task TestTimeoutHasNoCover()
        {
            var handler = new TestHttpHandler((_, _) => Task.FromException<HttpResponseMessage>(new TaskCanceledException("Expected timeout.")));
            using var store = new BmsCoverResourceStore(handler);

            Assert.That(await store.GetAsync(cover_url).ConfigureAwait(false), Is.Null);
        }

        [Test]
        public void TestCallerCancellationIsPreserved()
        {
            using var cancellation = new CancellationTokenSource();
            var handler = new TestHttpHandler(async (_, token) =>
            {
                cancellation.Cancel();
                await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
                throw new InvalidOperationException("A cancelled request cannot produce a response.");
            });
            using var store = new BmsCoverResourceStore(handler);

            Assert.That(async () => { await store.GetAsync(cover_url, cancellation.Token).ConfigureAwait(false); }, Throws.InstanceOf<OperationCanceledException>());
        }

        [Test]
        public async Task TestStoreDisposalCancelsActiveRequest()
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var handler = new TestHttpHandler(async (_, token) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
                throw new InvalidOperationException("A disposed store cannot produce a response.");
            });
            using var store = new BmsCoverResourceStore(handler);
            Task<byte[]> result = store.GetAsync(cover_url);
            await started.Task.ConfigureAwait(false);

            store.Dispose();

            Assert.That(async () => { await result.ConfigureAwait(false); }, Throws.InstanceOf<OperationCanceledException>());
        }

        [Test]
        public void TestSynchronousStreamReturnsCover()
        {
            var handler = new TestHttpHandler((_, _) => Task.FromResult(createResponse(createImage(2, 3))));
            using var store = new BmsCoverResourceStore(handler);
            using Stream? result = store.GetStream(cover_url);

            Assert.That(result, Is.Not.Null);
            using Image<Rgba32> image = Image.Load<Rgba32>(result!);
            Assert.That(image.Height, Is.EqualTo(3));
        }

        private static byte[] createImage(int width, int height)
        {
            using var image = new Image<Rgba32>(width, height);
            using var stream = new MemoryStream();
            image.SaveAsPng(stream);
            return stream.ToArray();
        }

        private static HttpResponseMessage createResponse(byte[] bytes) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes),
        };

        private sealed class TestHttpHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response;

            public int Requests { get; private set; }

            public Uri? LastUri { get; private set; }

            public TestHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response)
            {
                this.response = response;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests++;
                LastUri = request.RequestUri;
                return response(request, cancellationToken);
            }
        }

        private sealed class BudgetProbeStream : Stream
        {
            private readonly int length;

            public int BytesRead { get; private set; }

            public BudgetProbeStream(int length)
            {
                this.length = length;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => BytesRead;
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int read = Math.Min(count, length - BytesRead);
                Array.Clear(buffer, offset, read);
                BytesRead += read;
                return read;
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = Math.Min(buffer.Length, length - BytesRead);
                buffer.Span[..read].Clear();
                BytesRead += read;
                return new ValueTask<int>(read);
            }

            public override void Flush() => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
