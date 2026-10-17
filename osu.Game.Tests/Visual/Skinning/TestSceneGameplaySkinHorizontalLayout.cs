// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.NonVisual.Skinning;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using osuTK;

namespace osu.Game.Tests.Visual.Skinning
{
    [HeadlessTest]
    public partial class TestSceneGameplaySkinHorizontalLayout : OsuTestScene
    {
        [Test]
        public void TestLoadedRowsKeepRealGlyphOverhangCenteredWhenTheValueChanges()
        {
            RowHost host = null!;
            AddStep("mount an actual scene row and its prepared textures", () => Child = host = new RowHost());
            AddUntilStep("author glyph text loads", () => host.Scene?.TryGetRuntimeNode("row.yellow.digits", out GameplaySkinSceneRuntimeNode? node) == true
                                                         && node!.ContentDrawable.IsLoaded);
            foreach (int combo in new[] { 1, 12, 123, 1234 })
            {
                int value = combo;
                AddStep($"publish actual combo {value}", () => host.SetCombo(value));
                AddStep("loaded boxes enclose the same glyph images and share the row centre", () =>
                {
                    Assert.That(host.Scene.TryGetRuntimeNode("row.yellow", out GameplaySkinSceneRuntimeNode? row), Is.True);
                    Assert.That(host.Scene.TryGetRuntimeNode("row.yellow.label", out GameplaySkinSceneRuntimeNode? label), Is.True);
                    Assert.That(host.Scene.TryGetRuntimeNode("row.yellow.digits", out GameplaySkinSceneRuntimeNode? digits), Is.True);
                    var text = (GameplaySkinBitmapText)digits!.ContentDrawable;
                    float naturalWidth = 74 + 55 * (value.ToString().Length - 1);
                    Vector2 rowCentre = row!.TransformDrawable.ToScreenSpace(row.TransformDrawable.DrawSize / 2);
                    Vector2 left = label!.RootDrawable.ScreenSpaceDrawQuad.TopLeft;
                    Vector2 right = digits.RootDrawable.ScreenSpaceDrawQuad.BottomRight;
                    Assert.Multiple(() =>
                    {
                        Assert.That(text.Text.ToString(), Is.EqualTo(value.ToString()), "Loaded text uses the same author glyph store.");
                        Assert.That(text.NaturalSize.X, Is.EqualTo(naturalWidth).Within(0.001));
                        Assert.That(digits.RootDrawable.DrawWidth, Is.EqualTo(naturalWidth).Within(0.001));
                        Assert.That(text.DrawWidth, Is.EqualTo(value.ToString().Length * 55).Within(0.001),
                            "The row adds the final image extent without changing the renderer's existing advances.");
                        Assert.That((left.X + right.X) / 2, Is.EqualTo(rowCentre.X).Within(0.01));
                        Assert.That((left.Y + right.Y) / 2, Is.EqualTo(rowCentre.Y).Within(0.01));
                        Assert.That(text.ScreenSpaceDrawQuad.TopLeft.X, Is.EqualTo(digits.RootDrawable.ScreenSpaceDrawQuad.TopLeft.X).Within(0.01));
                        Assert.That(host.Scene.RuntimeFaults, Is.Empty);
                    });
                });
            }
        }

        private partial class RowHost : Container
        {
            internal GameplaySkinSceneRuntimeHost Scene { get; private set; } = null!;
            private GameplaySkinLayoutPublication publication = null!;
            private GameplaySkinEventStream stream = null!;
            private GameplaySkinEventProducer producer = null!;
            private Texture[] textures = Array.Empty<Texture>();
            private double time;

            internal RowHost() => RelativeSizeAxes = Axes.Both;

            [BackgroundDependencyLoader]
            private void load(IRenderer renderer)
            {
                textures = new[] { create(250, 100), create(204, 100), create(160, 100), create(74, 100) };
                publication = GameplaySkinHorizontalLayoutTest.CreatePublication(textures[0], textures[1], textures[2], textures[3]);
                stream = new GameplaySkinEventStream(publication, 0, publication.PreparedScene.InitialEventState);
                producer = stream.CreateProducer();
                Scene = new GameplaySkinSceneRuntimeHost(publication, stream);
                Children = new Drawable[] { Scene, Scene.Layers.HudForeground };
                Scene.MarkLayersMounted();

                Texture create(int width, int height)
                {
                    Texture result = renderer.CreateTexture(width, height);
                    result.SetData(new TextureUpload(new Image<Rgba32>(width, height, new Rgba32(255, 255, 255, 255))));
                    return result;
                }
            }

            internal void SetCombo(int combo)
                => stream.Publish(producer, ++time, GameplaySkinEventValue.Score(GameplaySkinEventKind.ScoreChanged,
                    new GameplaySkinScoreStateSnapshot(combo, combo, combo, 1, 0.5)), null, null);

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                if (isDisposing)
                {
                    producer?.Dispose();
                    stream?.Dispose();
                    publication?.Dispose();
                    foreach (Texture texture in textures)
                        texture.Dispose();
                }
            }
        }
    }
}
