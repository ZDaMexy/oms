// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    [TestFixture]
    public class BmsAuthoredBgaLayoutTest
    {
        [TestCase(BmsKeymode.Key7K, BmsPlayfieldStyle.P1)]
        [TestCase(BmsKeymode.Key14K, BmsPlayfieldStyle.Center)]
        public void TestManualBgaLayoutExampleHasTwoUsableWindows(BmsKeymode keymode, BmsPlayfieldStyle style)
        {
            var decoder = new BmsSkinDecoder();
            decoder.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SkinManualExamples", "bga-layout", "skin.ini")));
            BmsSkinConfiguration bucket = decoder.Configurations.Single(configuration => configuration.Keymode == keymode);
            GameplaySkinConfigurationDeclaration<float> width = bucket.GetAcceptedGeometry(BmsSkinConfigurationLookups.PlayfieldWidth);
            Assert.That(width.IsDeclared, Is.True);
            Assert.That(bucket.BgaLayout, Is.Not.Null);

            BmsGameplayLayoutSnapshot layout = createProvider(keymode).PublishForTesting(style, new BmsGameplayLayoutConfiguration
            {
                PlayfieldWidth = width.Value,
                BgaLayout = bucket.BgaLayout,
                BgaLayoutDiagnostic = bucket.BgaLayoutDiagnostic,
            }, BmsGameplayLayoutEnvironment.Default);

            Assert.That(layout.BgaViewports, Has.Count.EqualTo(2));
            Assert.That(layout.BgaViewportScaleModes, Is.EqualTo(new[] { GameplaySkinBgaScaleMode.Fit, GameplaySkinBgaScaleMode.Fill }));
            Assert.That(layout.Neutral.Diagnostics, Is.Empty);
            Assert.That(layout.BgaViewports.All(rect => layout.Context.SafeBounds.Contains(rect)
                                                      && !rect.Intersects(layout.PlayfieldRect)
                                                      && !rect.Intersects(layout.GaugeRect)
                                                      && !rect.Intersects(layout.HudRect)), Is.True);
        }

        [Test]
        public void TestDecoderPendingBucketsDuplicatesAndInvalidInput()
        {
            var decoder = new BmsSkinDecoder();
            decoder.Parse("[Bms]\nBgaViewports: .7,.1,.2,.3,fill\nKeymode: 7K\n[Bms]\nKeymode: 14K\nBgaViewports: none\n");
            Assert.That(decoder.Configurations[0].BgaLayout!.ScaleModes.Single(), Is.EqualTo(GameplaySkinBgaScaleMode.Fill));
            Assert.That(decoder.Configurations[1].BgaLayout!.Viewports, Is.Empty);
            GameplaySkinBgaLayout accepted = decoder.Configurations[0].BgaLayout!;

            decoder.Parse("[Bms]\nKeymode: 7K\nBgaViewports: .7,.1,NaN,.3,fit\n");
            Assert.That(decoder.Configurations[0].BgaLayout, Is.SameAs(accepted));
            Assert.That(decoder.Configurations[0].BgaLayoutDiagnostic, Is.EqualTo("bms.layout.bga-viewports.invalid-number"));

            decoder.Parse("[Bms]\nKeymode: 7K\nBgaViewports: .7,.1,.2,.3,stretch\n");
            Assert.That(decoder.Configurations[0].BgaLayout!.ScaleModes.Single(), Is.EqualTo(GameplaySkinBgaScaleMode.Stretch));
            Assert.That(decoder.Configurations[0].BgaLayoutDiagnostic, Is.Null);
        }

        [TestCase(BmsPlayfieldStyle.P1, .70f, .55f)]
        [TestCase(BmsPlayfieldStyle.P1, .70f, 3.2f)]
        [TestCase(BmsPlayfieldStyle.P2, .02f, .55f)]
        [TestCase(BmsPlayfieldStyle.P2, .02f, 3.2f)]
        public void TestAuthorWindowUsesSafeCoordinatesAndIndependentRatio(BmsPlayfieldStyle style, float x, float aspect)
        {
            var provider = createProvider(BmsKeymode.Key7K);
            var safe = GameplaySkinLayoutRect.Create(.04f, .03f, .92f, .94f);
            var environment = new BmsGameplayLayoutEnvironment(GameplaySkinLayoutRect.Create(0, 0, 1, 1), safe, aspect, 1.5f);
            var declaration = parse(FormattableString.Invariant($"{x},.1,.2,.31,fill"));
            var baseline = provider.PublishForTesting(style, new BmsGameplayLayoutConfiguration(), environment);
            var authored = provider.PublishForTesting(style, new BmsGameplayLayoutConfiguration { BgaLayout = declaration, BgaWidth = 1, BgaHeight = 1 }, environment);

            Assert.That(authored.BgaViewports.Single(), Is.EqualTo(GameplaySkinLayoutRect.Create(safe.Left + x * safe.Width,
                safe.Top + .1f * safe.Height, .2f * safe.Width, .31f * safe.Height)));
            Assert.That(authored.BgaViewportScaleModes.Single(), Is.EqualTo(GameplaySkinBgaScaleMode.Fill));
            Assert.That(authored.BgaViewports, Is.SameAs(authored.Neutral.BgaViewports));
            Assert.That(authored.PlayfieldRect, Is.EqualTo(baseline.PlayfieldRect));
            Assert.That(authored.LanesInLogicalOrder.Select(lane => lane.Action), Is.EqualTo(baseline.LanesInLogicalOrder.Select(lane => lane.Action)));
            Assert.That(authored.BgaViewports.Single().Intersects(authored.GaugeRect), Is.False);
            Assert.That(authored.Neutral.Diagnostics, Is.Empty);
        }

        [Test]
        public void TestFourWindowsAreOnlyTheUndeclared14KDefault()
        {
            var provider = createProvider(BmsKeymode.Key14K);
            var defaults = provider.PublishForTesting(BmsPlayfieldStyle.Center, new BmsGameplayLayoutConfiguration());
            Assert.That(defaults.BgaViewports, Has.Count.EqualTo(4));
            Assert.That(defaults.BgaViewportScaleModes, Is.All.EqualTo(GameplaySkinBgaScaleMode.Fit));

            var authored = provider.PublishForTesting(BmsPlayfieldStyle.Center, new BmsGameplayLayoutConfiguration
            {
                PlayfieldWidth = .4f,
                BgaLayout = parse(".01,.1,.12,.16,fit; .85,.1,.12,.25,fill; .01,.5,.1,.12,stretch"),
            });
            Assert.That(authored.BgaViewports, Has.Count.EqualTo(3));
            Assert.That(authored.BgaViewportScaleModes, Is.EqualTo(new[] { GameplaySkinBgaScaleMode.Fit, GameplaySkinBgaScaleMode.Fill, GameplaySkinBgaScaleMode.Stretch }));
            Assert.That(authored.Neutral.Diagnostics, Is.Empty);
            Assert.That(() => ((IList<GameplaySkinBgaScaleMode>)authored.BgaViewportScaleModes)[0] = GameplaySkinBgaScaleMode.Fill, Throws.TypeOf<NotSupportedException>());
        }

        [TestCase(".1,.1,.1,.1,fit")]
        [TestCase(".1,.62,.1,.04,fit")]
        [TestCase(".1,.72,.1,.04,fit")]
        [TestCase(".7,.94,.1,.04,fit")]
        public void TestUnsafeWindowOmitsWholeArrangementWithoutMovingPlayfield(string unsafeWindow)
        {
            var provider = createProvider(BmsKeymode.Key7K);
            var baseline = provider.PublishForTesting(BmsPlayfieldStyle.P1, configuration(parse("none")));
            var authored = provider.PublishForTesting(BmsPlayfieldStyle.P1, configuration(parse(".7,.1,.1,.1,fit;" + unsafeWindow)));
            Assert.That(authored.BgaViewports, Is.Empty);
            Assert.That(authored.BgaViewportScaleModes, Is.Empty);
            Assert.That(authored.Neutral.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain("bms.layout.bga-viewports-unavailable"));
            Assert.That(authored.PlayfieldRect, Is.EqualTo(baseline.PlayfieldRect));
            Assert.That(authored.GaugeRect, Is.EqualTo(baseline.GaugeRect));

            static BmsGameplayLayoutConfiguration configuration(GameplaySkinBgaLayout layout) => new BmsGameplayLayoutConfiguration
            {
                PlayfieldWidth = .4f,
                PlayfieldHeight = .6f,
                KeyAreaHeight = .1f,
                GaugeHeight = .06f,
                BgaLayout = layout,
            };
        }

        [TestCase("none", false, 1.7777778f)]
        [TestCase("none", false, .55f)]
        [TestCase(".7,0,.2,.2,fit", true, 1.7777778f)]
        [TestCase(".7,0,.2,.2,fit", true, .55f)]
        public void TestNoWindowsPreservesIndependentSafeInformation(string declaration, bool unavailable, float aspect)
        {
            var environment = new BmsGameplayLayoutEnvironment(GameplaySkinLayoutRect.Create(0, 0, 1, 1),
                GameplaySkinLayoutRect.Create(0, 0, 1, 1), aspect, 1);
            var layout = createProvider(BmsKeymode.Key7K).PublishForTesting(BmsPlayfieldStyle.P1, new BmsGameplayLayoutConfiguration
            {
                BgaLayout = parse(declaration),
                BgaInformationHeight = .15f,
            }, environment);
            Assert.That(layout.BgaViewports, Is.Empty);
            var information = layout.Neutral.Surfaces.Where(surface => surface.Id.StartsWith("information.", StringComparison.Ordinal)).ToArray();
            Assert.That(information, Has.Length.EqualTo(4));
            Assert.That(information.All(surface => layout.Context.SafeBounds.Contains(surface.Rect)
                                                   && !surface.Rect.Intersects(layout.PlayfieldRect)
                                                   && !surface.Rect.Intersects(layout.GaugeRect)), Is.True);
            Assert.That(information.SelectMany((first, index) => information.Skip(index + 1).Select(second => first.Rect.Intersects(second.Rect))), Is.All.False);
            Assert.That(layout.Neutral.Diagnostics.Any(diagnostic => diagnostic.Code == "bms.layout.bga-viewports-unavailable"), Is.EqualTo(unavailable));
        }

        [Test]
        public void TestNoneWithoutInformationDeclarationKeepsInformationAbsent()
        {
            var layout = createProvider(BmsKeymode.Key7K).PublishForTesting(BmsPlayfieldStyle.P1,
                new BmsGameplayLayoutConfiguration { BgaLayout = parse("none") });
            Assert.That(layout.BgaViewports, Is.Empty);
            Assert.That(layout.Neutral.Surfaces.Any(surface => surface.Id.StartsWith("information.", StringComparison.Ordinal)), Is.False);
            Assert.That(layout.Neutral.Diagnostics, Is.Empty);
        }

        [Test]
        public void TestAuthorInformationRegionsUseTheSameArrangement()
        {
            var layout = createProvider(BmsKeymode.Key7K).PublishForTesting(BmsPlayfieldStyle.P1, new BmsGameplayLayoutConfiguration
            {
                BgaLayout = parse(".7,.1,.2,.2,fit; .7,.4,.15,.25,fill"),
                BgaInformationHeight = .15f,
            });
            Assert.That(layout.BgaViewports, Has.Count.EqualTo(2));
            Assert.That(layout.Neutral.GetSurface("information.song").Rect.Bottom, Is.EqualTo(layout.BgaViewports[0].Top).Within(.00001));
            Assert.That(layout.Neutral.GetSurface("information.judgements").Rect.Top, Is.EqualTo(layout.BgaViewports[1].Bottom).Within(.00001));
            Assert.That(layout.Neutral.Surfaces.All(surface => layout.Context.SafeBounds.Contains(surface.Rect)), Is.True);
            Assert.That(layout.Neutral.Diagnostics, Is.Empty);
        }

        private static GameplaySkinBgaLayout parse(string text)
        {
            Assert.That(GameplaySkinBgaLayout.TryParse(text, out GameplaySkinBgaLayout? layout, out string? diagnostic), Is.True, diagnostic);
            return layout!;
        }

        private static BmsGameplayLayoutProvider createProvider(BmsKeymode keymode)
            => new BmsGameplayLayoutProvider(new BmsBeatmap { BmsInfo = new BmsBeatmapInfo { Keymode = keymode } });
    }
}
