// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Testing;
using osu.Game.Skinning.Gameplay;
using osuTK;

namespace osu.Game.Tests.Visual.Skinning
{
    [HeadlessTest]
    public partial class TestSceneGameplaySkinProjectiveGeometry : OsuTestScene
    {
        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        [Test]
        public void TestMovingCornersInvalidateTheLoadedSpriteCullingBounds()
        {
            Container canvas = null!;
            GameplaySkinProjectiveSprite sprite = null!;
            AddStep("load a masked canvas with a carrier outside its bounds", () =>
                Add(canvas = new Container
                {
                    Size = new Vector2(100),
                    Masking = true,
                    Child = sprite = new GameplaySkinProjectiveSprite
                    {
                        Position = new Vector2(200, 0),
                        Size = new Vector2(100),
                        Texture = renderer.WhitePixel,
                    },
                }));
            AddUntilStep("projective shader and sprite loaded", () => sprite.IsLoaded);
            AddStep("project the actual corners back inside the canvas", () =>
                sprite.ApplyQuad(new GameplaySkinProjectiveQuad(new(-2, 0, 1), new(-1, 0, 1), new(-1, 1, 1), new(-2, 1, 1), 1)));
            AddAssert("culling follows the visible projected corners", () =>
                sprite.ScreenSpaceDrawQuad.TopLeft == canvas.ToScreenSpace(Vector2.Zero)
                && sprite.ScreenSpaceDrawQuad.BottomRight == canvas.ToScreenSpace(new Vector2(100)));
            AddStep("change the same loaded sprite's next frame", () =>
                sprite.ApplyQuad(new GameplaySkinProjectiveQuad(new(-1.5, 0, 1), new(-0.5, 0, 1), new(-0.5, 1, 1), new(-1.5, 1, 1), 1)));
            AddAssert("the previous masking bound is invalidated", () =>
                sprite.ScreenSpaceDrawQuad.TopLeft == canvas.ToScreenSpace(new Vector2(50, 0))
                && sprite.ScreenSpaceDrawQuad.BottomRight == canvas.ToScreenSpace(new Vector2(150, 100)));
        }
    }
}
