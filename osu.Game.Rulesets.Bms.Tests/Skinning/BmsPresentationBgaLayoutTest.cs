// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Audio;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    [TestFixture]
    public class BmsPresentationBgaLayoutTest
    {
        private const string generic_windows = ".44,.84,.12,.05,stretch";
        private const string p1_windows = ".355208333333,.1,.570833333333,.5,fit";
        private const string p2_windows = ".073958333333,.1,.570833333333,.5,fill";

        [Test]
        public void TestDecoderKeepsPendingAndDuplicateStyleDeclarationsInTheirExactBucket()
        {
            var decoder = new BmsSkinDecoder();
            decoder.Parse("[Bms]\nBgaViewportsP1: " + p1_windows + "\nKeymode: 7K\nBgaViewports: " + generic_windows
                          + "\n[Bms]\nKeymode: 7K\nBgaViewportsP2: none\n[Bms]\nKeymode: 14K\nBgaViewportsP1: none\n");
            BmsSkinConfiguration seven = decoder.Configurations.Single(bucket => bucket.Keymode == BmsKeymode.Key7K);
            BmsSkinConfiguration fourteen = decoder.Configurations.Single(bucket => bucket.Keymode == BmsKeymode.Key14K);
            Assert.Multiple(() =>
            {
                Assert.That(seven.BgaLayout!.ScaleModes.Single(), Is.EqualTo(GameplaySkinBgaScaleMode.Stretch));
                Assert.That(seven.BgaLayoutP1!.ScaleModes.Single(), Is.EqualTo(GameplaySkinBgaScaleMode.Fit));
                Assert.That(seven.BgaLayoutP2!.Viewports, Is.Empty);
                Assert.That(fourteen.BgaLayout, Is.Null);
                Assert.That(fourteen.BgaLayoutP1!.Viewports, Is.Empty);
                Assert.That(fourteen.BgaLayoutP2, Is.Null);
            });
        }

        [TestCase(BmsPlayfieldStyle.P1, "BgaViewportsP1")]
        [TestCase(BmsPlayfieldStyle.P2, "BgaViewportsP2")]
        public void TestInvalidDuplicateRetainsOnlyThatStylesLastAcceptedDeclaration(BmsPlayfieldStyle style, string key)
        {
            var decoder = new BmsSkinDecoder();
            decoder.Parse("[Bms]\nKeymode: 7K\nBgaViewports: " + generic_windows + "\nBgaViewportsP1: " + p1_windows + "\nBgaViewportsP2: " + p2_windows + "\n");
            BmsSkinConfiguration bucket = decoder.Configurations.Single();
            GameplaySkinBgaLayout generic = bucket.BgaLayout!;
            GameplaySkinBgaLayout p1 = bucket.BgaLayoutP1!;
            GameplaySkinBgaLayout p2 = bucket.BgaLayoutP2!;
            decoder.Parse("[Bms]\nKeymode: 7K\n" + key + ": .1,.1,NaN,.2,fit\n");
            Assert.Multiple(() =>
            {
                Assert.That(bucket.BgaLayout, Is.SameAs(generic));
                Assert.That(bucket.BgaLayoutP1, Is.SameAs(p1));
                Assert.That(bucket.BgaLayoutP2, Is.SameAs(p2));
                Assert.That(style == BmsPlayfieldStyle.P1 ? bucket.BgaLayoutP1Diagnostic : bucket.BgaLayoutP2Diagnostic,
                    Is.EqualTo("bms.layout.bga-viewports.invalid-number"));
                Assert.That(style == BmsPlayfieldStyle.P1 ? bucket.BgaLayoutP2Diagnostic : bucket.BgaLayoutP1Diagnostic, Is.Null);
            });
            decoder.Parse("[Bms]\nKeymode: 7K\n" + key + ": none\n");
            Assert.That(style == BmsPlayfieldStyle.P1 ? bucket.BgaLayoutP1!.Viewports : bucket.BgaLayoutP2!.Viewports, Is.Empty);
            Assert.That(style == BmsPlayfieldStyle.P1 ? bucket.BgaLayoutP1Diagnostic : bucket.BgaLayoutP2Diagnostic, Is.Null);
        }

        [TestCase("bgaviewportsp1")]
        [TestCase("BgaViewportsP01")]
        [TestCase("BgaViewportsP1,P2")]
        [TestCase("BgaViewportsP2Extra")]
        public void TestOnlyExactSideKeysCanDeclareAStyleLayout(string key)
        {
            var decoder = new BmsSkinDecoder();
            decoder.Parse("[Bms]\nKeymode: 7K\n" + key + ": none\n");
            Assert.That(decoder.Configurations.Single().BgaLayoutP1, Is.Null);
            Assert.That(decoder.Configurations.Single().BgaLayoutP2, Is.Null);
        }

        [TestCase("BgaViewports")]
        [TestCase("BgaViewportsP1")]
        [TestCase("BgaViewportsP2")]
        public void TestAllDeclarationKindsUseTheSharedParserAndSixteenWindowLimit(string key)
        {
            foreach (string invalid in new[]
                     {
                         ".1,.1,NaN,.2,fit", ".9,.1,.2,.2,fit", ".1,.1,.2,.2,unknown", "",
                         string.Join(';', Enumerable.Repeat(".1,.1,.1,.1,fit", 17)),
                     })
            {
                Assert.That(GameplaySkinBgaLayout.TryParse(invalid, out _, out string? expectedDiagnostic), Is.False);
                using var skin = new PresentationSkin(ini(BmsKeymode.Key7K, key + ": " + invalid + "\n"));
                BmsPlayfieldStyle? style = key == "BgaViewportsP1" ? BmsPlayfieldStyle.P1 : key == "BgaViewportsP2" ? BmsPlayfieldStyle.P2 : null;
                Assert.That(skin.GetAcceptedBgaLayout(BmsKeymode.Key7K, style), Is.Null);
                Assert.That(skin.GetBgaLayoutDiagnostic(BmsKeymode.Key7K, style), Is.EqualTo(expectedDiagnostic));
            }

            using var full = new PresentationSkin(ini(BmsKeymode.Key7K, key + ": " + string.Join(';', Enumerable.Repeat(".1,.1,.1,.1,fit", 16)) + "\n"));
            BmsPlayfieldStyle? fullStyle = key == "BgaViewportsP1" ? BmsPlayfieldStyle.P1 : key == "BgaViewportsP2" ? BmsPlayfieldStyle.P2 : null;
            Assert.That(full.GetAcceptedBgaLayout(BmsKeymode.Key7K, fullStyle)!.Viewports, Has.Count.EqualTo(16));
        }

        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.P1, 682, GameplaySkinBgaScaleMode.Fit)]
        [TestCase(BmsKeymode.Key5K, BmsPlayfieldStyle.P2, 142, GameplaySkinBgaScaleMode.Fill)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P1, 682, GameplaySkinBgaScaleMode.Fit)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P2, 142, GameplaySkinBgaScaleMode.Fill)]
        public void TestSelectedSideWindowsReachExact1920PixelPublication(BmsKeymode keymode, BmsPlayfieldStyle style, double left, GameplaySkinBgaScaleMode mode)
        {
            using var skin = new PresentationSkin(ini(keymode, allWindows()));
            BmsGameplayLayoutConfiguration configuration = BmsGameplayLayoutConfiguration.FromSkin(skin, keymode, skin);
            BmsGameplayLayoutProvider provider = createProvider(keymode);
            BmsGameplayLayoutSnapshot snapshot = provider.PublishForTesting(style, configuration);
            using var baselineSkin = new PresentationSkin(ini(keymode, "BgaViewports: " + generic_windows + "\n"));
            BmsGameplayLayoutSnapshot baseline = createProvider(keymode).PublishForTesting(style,
                BmsGameplayLayoutConfiguration.FromSkin(baselineSkin, keymode, baselineSkin));
            GameplaySkinLayoutRect rect = snapshot.BgaViewports.Single();
            Assert.Multiple(() =>
            {
                Assert.That(rect.Left * 1920, Is.EqualTo(left).Within(.002));
                Assert.That(rect.Width * 1920, Is.EqualTo(1096).Within(.002));
                Assert.That(rect.Top * 1080, Is.EqualTo(108).Within(.002));
                Assert.That(rect.Height * 1080, Is.EqualTo(540).Within(.002));
                Assert.That(snapshot.BgaViewportScaleModes.Single(), Is.EqualTo(mode));
                Assert.That(snapshot.BgaViewports, Is.SameAs(snapshot.Neutral.BgaViewports));
                Assert.That(provider.Current, Is.SameAs(snapshot));
                Assert.That(provider.RevisionOwner!.CurrentPublication!.Snapshot, Is.SameAs(snapshot.Neutral));
                Assert.That(snapshot.PlayfieldRect, Is.EqualTo(baseline.PlayfieldRect));
                Assert.That(snapshot.LanesInLogicalOrder.Select(lane => lane.Action), Is.EqualTo(baseline.LanesInLogicalOrder.Select(lane => lane.Action)));
                Assert.That(snapshot.Neutral.Diagnostics, Is.Empty);
            });
        }

        [TestCase(BmsPlayfieldStyle.P1, true)]
        [TestCase(BmsPlayfieldStyle.P2, true)]
        [TestCase(BmsPlayfieldStyle.P1, false)]
        [TestCase(BmsPlayfieldStyle.P2, false)]
        public void TestMissingSidePreservesGenericOrUndeclaredDefault(BmsPlayfieldStyle style, bool generic)
        {
            string genericField = generic ? "BgaViewports: " + generic_windows + "\n" : string.Empty;
            string other = style == BmsPlayfieldStyle.P1 ? "BgaViewportsP2: none\n" : "BgaViewportsP1: none\n";
            using var selected = new PresentationSkin(ini(BmsKeymode.Key7K, genericField + other));
            using var old = new PresentationSkin(ini(BmsKeymode.Key7K, genericField));
            BmsGameplayLayoutSnapshot actual = solve(selected, BmsKeymode.Key7K, style);
            BmsGameplayLayoutSnapshot baseline = solve(old, BmsKeymode.Key7K, style);
            Assert.That(actual.BgaViewports, Is.EqualTo(baseline.BgaViewports));
            Assert.That(actual.BgaViewportScaleModes, Is.EqualTo(baseline.BgaViewportScaleModes));
            Assert.That(actual.Neutral.Diagnostics, Is.Empty);
        }

        [TestCase(BmsPlayfieldStyle.P1)]
        [TestCase(BmsPlayfieldStyle.P2)]
        public void TestUnacceptedSideWithoutGenericKeepsOldDefaultAndReportsItsParseDiagnostic(BmsPlayfieldStyle style)
        {
            string key = style == BmsPlayfieldStyle.P1 ? "BgaViewportsP1" : "BgaViewportsP2";
            using var selected = new PresentationSkin(ini(BmsKeymode.Key7K, key + ": invalid\n"));
            using var old = new PresentationSkin(ini(BmsKeymode.Key7K, string.Empty));
            BmsGameplayLayoutSnapshot actual = solve(selected, BmsKeymode.Key7K, style);
            BmsGameplayLayoutSnapshot baseline = solve(old, BmsKeymode.Key7K, style);
            Assert.That(actual.BgaViewports, Is.EqualTo(baseline.BgaViewports));
            Assert.That(actual.BgaViewportScaleModes, Is.EqualTo(baseline.BgaViewportScaleModes));
            Assert.That(actual.Neutral.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain("bms.layout.bga-viewports.invalid-format"));
        }

        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.Center)]
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.CenterRightScratch)]
        [TestCase(BmsKeymode.Key14K, BmsPlayfieldStyle.P1)]
        [TestCase(BmsKeymode.Key14K, BmsPlayfieldStyle.P2)]
        [TestCase(BmsKeymode.Key14K, BmsPlayfieldStyle.Center)]
        [TestCase(BmsKeymode.Key9K_Bms, BmsPlayfieldStyle.P1)]
        public void TestNonDockedStylesKeepGenericWindows(BmsKeymode keymode, BmsPlayfieldStyle style)
        {
            using var selected = new PresentationSkin(ini(keymode, "BgaViewports: " + generic_windows + "\nBgaViewportsP1: none\nBgaViewportsP2: none\n"));
            BmsGameplayLayoutSnapshot layout = solve(selected, keymode, style);
            Assert.That(layout.BgaViewports.Single(), Is.EqualTo(parse(generic_windows).Viewports.Single()));
            Assert.That(layout.BgaViewportScaleModes.Single(), Is.EqualTo(GameplaySkinBgaScaleMode.Stretch));
            Assert.That(layout.Neutral.Diagnostics, Is.Empty);
        }

        [TestCase(BmsPlayfieldStyle.P1)]
        [TestCase(BmsPlayfieldStyle.P2)]
        public void TestExplicitSideNoneOverridesGenericAndDefault(BmsPlayfieldStyle style)
        {
            string key = style == BmsPlayfieldStyle.P1 ? "BgaViewportsP1" : "BgaViewportsP2";
            using var selected = new PresentationSkin(ini(BmsKeymode.Key7K, allWindows() + key + ": none\n"));
            BmsGameplayLayoutSnapshot layout = solve(selected, BmsKeymode.Key7K, style);
            Assert.That(layout.BgaViewports, Is.Empty);
            Assert.That(layout.BgaViewportScaleModes, Is.Empty);
            Assert.That(layout.Neutral.Diagnostics, Is.Empty);
        }

        [TestCase(BmsPlayfieldStyle.P1, ".05,.1,.1,.2,fit")]
        [TestCase(BmsPlayfieldStyle.P2, ".8,.1,.1,.2,fit")]
        public void TestConflictingSideArrangementDoesNotFallBackToGeneric(BmsPlayfieldStyle style, string conflicting)
        {
            string key = style == BmsPlayfieldStyle.P1 ? "BgaViewportsP1" : "BgaViewportsP2";
            using var selected = new PresentationSkin(ini(BmsKeymode.Key7K, "BgaViewports: " + generic_windows + "\n" + key + ": " + conflicting + "\n"));
            BmsGameplayLayoutSnapshot layout = solve(selected, BmsKeymode.Key7K, style);
            Assert.That(layout.BgaViewports, Is.Empty);
            Assert.That(layout.BgaViewportScaleModes, Is.Empty);
            Assert.That(layout.Neutral.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain("bms.layout.bga-viewports-unavailable"));
        }

        [Test]
        public void TestOnlyTheAppliedDeclarationContributesParseDiagnostics()
        {
            using var selected = new PresentationSkin(ini(BmsKeymode.Key7K,
                "BgaViewports: " + generic_windows + "\nBgaViewportsP1: .1,.1,NaN,.2,fit\nBgaViewportsP2: " + p2_windows + "\n"));
            BmsGameplayLayoutSnapshot p1 = solve(selected, BmsKeymode.Key7K, BmsPlayfieldStyle.P1);
            BmsGameplayLayoutSnapshot p2 = solve(selected, BmsKeymode.Key7K, BmsPlayfieldStyle.P2);
            BmsGameplayLayoutSnapshot center = solve(selected, BmsKeymode.Key7K, BmsPlayfieldStyle.Center);
            Assert.That(p1.BgaViewports.Single(), Is.EqualTo(parse(generic_windows).Viewports.Single()));
            Assert.That(p1.Neutral.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain("bms.layout.bga-viewports.invalid-number"));
            Assert.That(p2.Neutral.Diagnostics, Is.Empty);
            Assert.That(center.Neutral.Diagnostics, Is.Empty);

            using var overridden = new PresentationSkin(ini(BmsKeymode.Key7K, "BgaViewports: invalid\nBgaViewportsP1: " + p1_windows + "\n"));
            Assert.That(solve(overridden, BmsKeymode.Key7K, BmsPlayfieldStyle.P1).Neutral.Diagnostics, Is.Empty);
        }

        [Test]
        public void TestSelectedPackageAndKeymodeCannotBorrowAnotherSourcesSideWindows()
        {
            using var fallback = new PresentationSkin(ini(BmsKeymode.Key7K, allWindows()));
            using var selected = new PresentationSkin(ini(BmsKeymode.Key7K, string.Empty) + ini(BmsKeymode.Key14K, allWindows()));
            BmsGameplayLayoutConfiguration absent = BmsGameplayLayoutConfiguration.FromSkin(fallback, BmsKeymode.Key7K, selected);
            Assert.Multiple(() =>
            {
                Assert.That(absent.BgaLayout, Is.Null);
                Assert.That(absent.BgaLayoutP1, Is.Null);
                Assert.That(absent.BgaLayoutP2, Is.Null);
                Assert.That(absent.BgaLayoutP1Diagnostic, Is.Null);
                Assert.That(absent.BgaLayoutP2Diagnostic, Is.Null);
            });
            using var onlyGeneric = new PresentationSkin(ini(BmsKeymode.Key7K, "BgaViewports: none\n"));
            BmsGameplayLayoutConfiguration configuration = BmsGameplayLayoutConfiguration.FromSkin(fallback, BmsKeymode.Key7K, onlyGeneric);
            Assert.That(configuration.BgaLayout!.Viewports, Is.Empty);
            Assert.That(configuration.BgaLayoutP1, Is.Null);
            Assert.That(configuration.BgaLayoutP2, Is.Null);
            Assert.That(createProvider(BmsKeymode.Key7K).PublishForTesting(BmsPlayfieldStyle.P1, configuration).BgaViewports, Is.Empty);
        }

        private static string ini(BmsKeymode keymode, string fields)
            => "[Bms]\nKeymode: " + (keymode switch
            {
                BmsKeymode.Key5K => "5K",
                BmsKeymode.Key7K => "7K",
                BmsKeymode.Key14K => "14K",
                BmsKeymode.Key9K_Bms => "9K",
                _ => throw new ArgumentOutOfRangeException(nameof(keymode)),
            }) + "\nPlayfieldWidth: 0.229166667\nPlayfieldHeight: 0.6\nPlayfieldSideInset: 0.036979167\n" + fields;

        private static string allWindows() => "BgaViewports: " + generic_windows + "\nBgaViewportsP1: " + p1_windows + "\nBgaViewportsP2: " + p2_windows + "\n";

        private static GameplaySkinBgaLayout parse(string value)
        {
            Assert.That(GameplaySkinBgaLayout.TryParse(value, out GameplaySkinBgaLayout? layout, out string? diagnostic), Is.True, diagnostic);
            return layout!;
        }

        private static BmsGameplayLayoutSnapshot solve(BmsLegacySkin selected, BmsKeymode keymode, BmsPlayfieldStyle style)
            => createProvider(keymode).PublishForTesting(style, BmsGameplayLayoutConfiguration.FromSkin(selected, keymode, selected));

        private static BmsGameplayLayoutProvider createProvider(BmsKeymode keymode)
            => new BmsGameplayLayoutProvider(new BmsBeatmap { BmsInfo = new BmsBeatmapInfo { Keymode = keymode } });

        private sealed class PresentationSkin : BmsLegacySkin
        {
            public PresentationSkin(string configuration)
                : base(new SkinInfo { Name = "presentation BGA" }, new ResourceProvider(), new IniStore(configuration))
            {
            }
        }

        private sealed class ResourceProvider : IStorageResourceProvider
        {
            public IRenderer Renderer { get; } = new DummyRenderer();
            public AudioManager? AudioManager => null;
            public IResourceStore<byte[]> Files => Resources;
            public IResourceStore<byte[]> Resources { get; } = new ResourceStore<byte[]>();
            public RealmAccess RealmAccess => null!;
            public IResourceStore<TextureUpload>? CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore) => null;
        }

        private sealed class IniStore : IResourceStore<byte[]>
        {
            private readonly byte[] content;

            public IniStore(string ini) => content = Encoding.UTF8.GetBytes(ini);
            public byte[] Get(string name) => name == "skin.ini" ? content : null!;
            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(Get(name));
            public Stream GetStream(string name) => name == "skin.ini" ? new MemoryStream(content) : null!;
            public IEnumerable<string> GetAvailableResources() => new[] { "skin.ini" };
            public void Dispose() { }
        }
    }
}
