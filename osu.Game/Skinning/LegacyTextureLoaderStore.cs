// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.Skinning
{
    public class LegacyTextureLoaderStore : IResourceStore<TextureUpload>
    {
        private const int maximum_tiff_bytes = 16 * 1024 * 1024;
        private const int maximum_tiff_pixels = 16 * 1024 * 1024;
        private const int maximum_tiff_dimension = 8192;

        private static readonly DecoderOptions legacy_tiff_options = new DecoderOptions
        {
            Configuration = new SixLabors.ImageSharp.Configuration(new TiffConfigurationModule()),
            MaxFrames = 1,
            SkipMetadata = true,
        };

        private readonly IResourceStore<TextureUpload>? wrappedStore;

        public LegacyTextureLoaderStore(IResourceStore<TextureUpload>? wrappedStore)
        {
            this.wrappedStore = wrappedStore;
        }

        public TextureUpload Get(string name)
        {
            var textureUpload = wrappedStore?.Get(name) ?? getLegacyTiff(name);

            if (textureUpload == null)
                return null!;

            return shouldConvertToGrayscale(name)
                ? convertToGrayscale(textureUpload)
                : textureUpload;
        }

        public Task<TextureUpload> GetAsync(string name, CancellationToken cancellationToken = default)
            => Task.Run(() => Get(name), cancellationToken);

        private TextureUpload? getLegacyTiff(string name)
        {
            // Some legacy skins use TIFF data under a PNG sprite name. Keep the framework's
            // usual decoder list everywhere else and bound this fallback before decoding pixels.
            using Stream? stream = wrappedStore?.GetStream(name);
            if (stream?.CanSeek != true || stream.Length - stream.Position is < 4 or > maximum_tiff_bytes)
                return null;

            try
            {
                long start = stream.Position;
                Span<byte> signature = stackalloc byte[4];
                stream.ReadExactly(signature);
                if (!signature.SequenceEqual("II*\0"u8) && !signature.SequenceEqual("MM\0*"u8))
                    return null;

                stream.Position = start;
                ImageInfo info = Image.Identify(legacy_tiff_options, stream);
                if (info.Width is <= 0 or > maximum_tiff_dimension || info.Height is <= 0 or > maximum_tiff_dimension
                    || (long)info.Width * info.Height > maximum_tiff_pixels)
                    return null;

                stream.Position = start;
                return new TextureUpload(Image.Load<Rgba32>(legacy_tiff_options, stream));
            }
            catch (ImageFormatException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (EndOfStreamException)
            {
                return null;
            }
        }

        // https://github.com/peppy/osu-stable-reference/blob/013c3010a9d495e3471a9c59518de17006f9ad89/osu!/Graphics/Textures/TextureManager.cs#L91-L96
        private static readonly string[] grayscale_sprites =
        {
            @"taiko-bar-right",
            @"taikobigcircle",
            @"taikohitcircle",
            @"taikohitcircleoverlay"
        };

        private bool shouldConvertToGrayscale(string name)
        {
            foreach (string grayscaleSprite in grayscale_sprites)
            {
                // unfortunately at this level of lookup we can encounter `@2x` scale suffixes in the name,
                // so straight equality cannot be used.
                if (name.Equals(grayscaleSprite, StringComparison.OrdinalIgnoreCase)
                    || name.Equals($@"{grayscaleSprite}@2x", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private TextureUpload convertToGrayscale(TextureUpload textureUpload)
        {
            var image = Image.LoadPixelData(textureUpload.Data, textureUpload.Width, textureUpload.Height);

            // stable uses `0.299 * r + 0.587 * g + 0.114 * b`
            // (https://github.com/peppy/osu-stable-reference/blob/013c3010a9d495e3471a9c59518de17006f9ad89/osu!/Graphics/Textures/pTexture.cs#L138-L153)
            // which matches mode BT.601 (https://en.wikipedia.org/wiki/Grayscale#Luma_coding_in_video_systems)
            image.Mutate(i => i.Grayscale(GrayscaleMode.Bt601));

            return new TextureUpload(image);
        }

        public Stream? GetStream(string name) => wrappedStore?.GetStream(name);

        public IEnumerable<string> GetAvailableResources() => wrappedStore?.GetAvailableResources() ?? Array.Empty<string>();

        public void Dispose()
        {
            wrappedStore?.Dispose();
        }
    }
}
