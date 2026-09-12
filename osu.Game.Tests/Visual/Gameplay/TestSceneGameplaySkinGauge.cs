// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.NonVisual.Skinning;

namespace osu.Game.Tests.Visual.Gameplay
{
    [HeadlessTest]
    public partial class TestSceneGameplaySkinGauge : OsuTestScene
    {
        [Test]
        public void TestGaugeRevealsFixedArtworkAndRetainsInactiveTrackAtZero()
        {
            GaugeHost host = null!;
            Container visual = null!;
            Container reveal = null!;
            Sprite track = null!;
            Sprite fill = null!;
            AddStep("mount the real shared gauge renderer", () => Child = host = new GaugeHost());
            AddUntilStep("gauge sprites load with actual parent geometry", () =>
                host.Scene?.Layers.HudForeground.ChildrenOfType<Sprite>().Count(sprite => sprite.IsLoaded) == 2);
            AddStep("capture full track and clipped fill", () =>
            {
                visual = (Container)host.Scene.Layers.HudForeground.Children.Single();
                track = visual.Children.OfType<Sprite>().Single();
                reveal = visual.Children.OfType<Container>().Single();
                fill = reveal.Children.OfType<Sprite>().Single();
            });
            double sequence = 0;
            foreach (double value in new[] { 0, 0.2, 0.8, 1, 0 })
            {
                double fraction = value;
                AddStep($"publish actual gauge value {fraction}", () => host.SetGauge(++sequence, fraction));
                AddStep("fixed artwork and border never compress", () =>
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(track.DrawWidth, Is.EqualTo(visual.DrawWidth).Within(0.001));
                        Assert.That(track.DrawHeight, Is.EqualTo(visual.DrawHeight).Within(0.001));
                        Assert.That(track.Alpha, Is.EqualTo(1));
                        Assert.That(track.Colour.AverageColour.Linear.R, Is.LessThan(fill.Colour.AverageColour.Linear.R));
                        Assert.That(track.Texture, Is.SameAs(fill.Texture));
                        Assert.That(reveal.Masking, Is.True);
                        Assert.That(reveal.DrawWidth, Is.EqualTo(visual.DrawWidth * fraction).Within(0.001));
                        if (fraction > 0)
                        {
                            Assert.That(fill.DrawWidth, Is.EqualTo(track.DrawWidth).Within(0.001),
                                "Full-width texture keeps every segment and threshold at the same position.");
                            Assert.That(fill.DrawHeight, Is.EqualTo(track.DrawHeight).Within(0.001));
                            Assert.That(fill.ScreenSpaceDrawQuad.TopLeft, Is.EqualTo(track.ScreenSpaceDrawQuad.TopLeft));
                        }
                        else
                            Assert.That(reveal.DrawWidth, Is.Zero, "Only the inactive full-width track remains at zero.");
                    });
                });
            }
        }

        private partial class GaugeHost : Container
        {
            public GameplaySkinSceneRuntimeHost Scene { get; private set; } = null!;
            private GameplaySkinEventStream stream = null!;
            private GameplaySkinEventProducer producer = null!;

            public GaugeHost() => RelativeSizeAxes = Axes.Both;

            [BackgroundDependencyLoader]
            private void load(IRenderer renderer)
            {
                GameplaySkinLayoutPublication publication = GameplaySkinSceneRuntimeHostTest.CreateGaugePublication(renderer.WhitePixel);
                stream = new GameplaySkinEventStream(publication, 0, publication.PreparedScene.InitialEventState);
                producer = stream.CreateProducer();
                Scene = new GameplaySkinSceneRuntimeHost(publication, stream);
                Children = new Drawable[] { Scene, Scene.Layers.HudForeground };
                Scene.MarkLayersMounted();
            }

            public void SetGauge(double sequence, double value)
                => stream.Publish(producer, sequence, GameplaySkinEventValue.Score(GameplaySkinEventKind.GaugeChanged,
                    new GameplaySkinScoreStateSnapshot(0, 0, 0, 1, value)), null, null);

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                if (isDisposing)
                {
                    producer?.Dispose();
                    stream?.Dispose();
                }
            }
        }
    }
}
