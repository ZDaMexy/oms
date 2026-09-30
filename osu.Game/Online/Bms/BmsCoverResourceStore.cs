// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Online.Bms
{
    /// <summary>
    /// Supplies static, bounded covers for the explicitly supported BMS download sources.
    /// </summary>
    public sealed class BmsCoverResourceStore : IResourceStore<byte[]>
    {
        private const int max_download_bytes = 8 * 1024 * 1024;
        private const int max_image_dimension = 8192;
        private const long max_image_pixels = 16_000_000;

        private readonly HttpClient client;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private int disposed;

        public BmsCoverResourceStore(HttpMessageHandler? handler = null)
        {
            client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false })
            {
                Timeout = Timeout.InfiniteTimeSpan,
            };
        }

        public byte[] Get(string name) => GetAsync(name).GetAwaiter().GetResult();

        public async Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Uri.TryCreate(name, UriKind.Absolute, out Uri? uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || !string.IsNullOrEmpty(uri.UserInfo)
                || !uri.IsDefaultPort
                || !(uri.Host.Equals("pixeldrain.net", StringComparison.OrdinalIgnoreCase)
                     || uri.Host.Equals("gingerrush.com", StringComparison.OrdinalIgnoreCase)
                     || uri.Host.Equals("bms.alvorna.com", StringComparison.OrdinalIgnoreCase)))
                return unavailable("unsupported-address");

            using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            requestCancellation.CancelAfter(TimeSpan.FromSeconds(30));
            CancellationToken requestToken = requestCancellation.Token;

            try
            {
                using HttpResponseMessage response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, requestToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    return unavailable($"http-{(int)response.StatusCode}");

                if (response.Content.Headers.ContentLength > max_download_bytes)
                    return unavailable("download-budget");

                using Stream input = await response.Content.ReadAsStreamAsync(requestToken).ConfigureAwait(false);
                using var downloaded = new MemoryStream();
                byte[] buffer = new byte[64 * 1024];

                while (true)
                {
                    int read = await input.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, max_download_bytes + 1 - (int)downloaded.Length)), requestToken).ConfigureAwait(false);
                    if (read == 0)
                        break;

                    if (downloaded.Length + read > max_download_bytes)
                        return unavailable("download-budget");

                    downloaded.Write(buffer, 0, read);
                }

                requestToken.ThrowIfCancellationRequested();
                byte[] bytes = downloaded.ToArray();
                ImageInfo? info = Image.Identify(new DecoderOptions { MaxFrames = 1 }, bytes);

                if (info == null)
                    return unavailable("invalid-image");

                if (info.Width <= 0 || info.Height <= 0
                    || info.Width > max_image_dimension || info.Height > max_image_dimension
                    || (long)info.Width * info.Height > max_image_pixels)
                    return unavailable("image-budget");

                // The framework loader receives one validated static frame, so animated inputs cannot
                // allocate an unbounded sequence of decoded frames after passing the header check.
                using Image<Rgba32> image = Image.Load<Rgba32>(new DecoderOptions { MaxFrames = 1 }, bytes);
                using var png = new MemoryStream();
                await image.SaveAsPngAsync(png, requestToken).ConfigureAwait(false);
                return png.ToArray();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !lifetime.IsCancellationRequested)
            {
                return unavailable("timeout");
            }
            catch (HttpRequestException)
            {
                return unavailable("network");
            }
            catch (IOException)
            {
                return unavailable("stream");
            }
            catch (UnknownImageFormatException)
            {
                return unavailable("invalid-image");
            }
            catch (InvalidImageContentException)
            {
                return unavailable("invalid-image");
            }
        }

        public Stream? GetStream(string name)
        {
            byte[]? bytes = Get(name);
            return bytes == null ? null : new MemoryStream(bytes, writable: false);
        }

        public IEnumerable<string> GetAvailableResources() => Array.Empty<string>();

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;

            try
            {
                lifetime.Cancel();
            }
            finally
            {
                client.Dispose();
                lifetime.Dispose();
            }
        }

        private static byte[] unavailable(string reason)
        {
            Logger.Log($"BMS download cover unavailable ({reason}).", LoggingTarget.Network);
            return null!;
        }
    }
}
