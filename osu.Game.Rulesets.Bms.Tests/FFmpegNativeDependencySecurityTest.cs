// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using FFmpeg.AutoGen;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Video;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    [Platform(Include = "Win")]
    public class FFmpegNativeDependencySecurityTest
    {
        [Test]
        public void TestActualManagedDecoderLoadsTheCompletePatchedSet()
        {
            Assert.That(RuntimeInformation.ProcessArchitecture, Is.EqualTo(Architecture.X64));
            // Constructing the real framework decoder installs its SafeDirectories
            // native resolver. Exercise that path instead of AutoGen's default loader.
            using var decoder = new VideoDecoder(new DummyRenderer(), new MemoryStream());
            Assert.That(ffmpeg.av_version_info(), Is.EqualTo("4.4.8-oms.20261022"));
            Assert.That(version(ffmpeg.avutil_version()), Is.EqualTo("56.70.100"));
            Assert.That(version(ffmpeg.avcodec_version()), Is.EqualTo("58.134.100"));
            Assert.That(version(ffmpeg.avformat_version()), Is.EqualTo("58.76.100"));
            Assert.That(version(ffmpeg.swscale_version()), Is.EqualTo("5.9.100"));

            using JsonDocument manifest = readManifest();
            JsonElement libraries = manifest.RootElement.GetProperty("libraries");
            string[] nativeFiles = Directory.EnumerateFiles(AppContext.BaseDirectory, "*", SearchOption.AllDirectories)
                                            .Where(path => Regex.IsMatch(Path.GetFileName(path), @"^(lib)?(avcodec|avformat|avutil|swscale)[.-]"))
                                            .ToArray();
            Assert.That(nativeFiles.Length, Is.EqualTo(4), "Portable and RID-specific outputs must not carry extra vulnerable native copies.");

            using Process process = Process.GetCurrentProcess();
            foreach (JsonProperty library in libraries.EnumerateObject())
            {
                string expectedHash = library.Value.GetProperty("sha256").GetString()!;
                string outputPath = nativeFiles.Single(path => Path.GetFileName(path).Equals(library.Name, StringComparison.OrdinalIgnoreCase));
                Assert.That(hash(outputPath), Is.EqualTo(expectedHash), outputPath);
                string loadedPath = process.Modules.Cast<ProcessModule>()
                                           .Single(module => module.ModuleName.Equals(library.Name, StringComparison.OrdinalIgnoreCase)).FileName;
                Assert.That(Path.GetFullPath(loadedPath), Is.EqualTo(Path.GetFullPath(outputPath)).IgnoreCase,
                    "The managed decoder must resolve the patched output, rather than another installed FFmpeg.");
                Assert.That(hash(loadedPath), Is.EqualTo(expectedHash), loadedPath);
            }
        }

        [Test]
        public void TestActualManagedLayoutsMatchTheCompiledNativeHeaderProbe()
        {
            using JsonDocument manifest = readManifest();
            JsonElement nativeFields = manifest.RootElement.GetProperty("managedAbiVerified");
            foreach (JsonProperty field in nativeFields.EnumerateObject())
            {
                string[] name = field.Name.Split('.');
                bool size = name[0] == "sizeof";
                Type type = typeof(AVFrame).Assembly.GetType("FFmpeg.AutoGen." + (size ? name[1] : name[0]), throwOnError: true)!;
                long actual = size ? Marshal.SizeOf(type) : Marshal.OffsetOf(type, name[1]).ToInt64();
                Assert.That(actual, Is.EqualTo(field.Value.GetInt64()), field.Name);
            }
        }

        private static JsonDocument readManifest()
            => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "licenses", "ffmpeg", "runtime-manifest.json")));

        private static string version(uint packed)
            => $"{packed >> 16}.{(packed >> 8) & 255}.{packed & 255}";

        private static string hash(string path)
        {
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
    }
}
