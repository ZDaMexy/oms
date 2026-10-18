// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Game.Graphics.Sprites;
using osu.Game.Skinning.Gameplay;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Settings.Sections
{
    /// <summary>Loaded by a visibility wrapper, so only visible, bounded settings thumbnails allocate pixels.</summary>
    internal partial class SkinOptionPreviewImage : CompositeDrawable
    {
        private readonly GameplaySkinOptionPreview preview;
        private readonly Sprite sprite;
        private readonly OsuSpriteText failure;
        private Texture? texture;

        public SkinOptionPreviewImage(GameplaySkinOptionPreview preview)
        {
            this.preview = preview;
            RelativeSizeAxes = Axes.Both;
            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(18, 20, 28, 255) },
                sprite = new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Size = new Vector2(0.9f),
                    FillMode = FillMode.Fit,
                },
                failure = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = "无法预览",
                    Alpha = 0,
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(IRenderer renderer)
        {
            TextureUpload upload;
            try
            {
                upload = new TextureUpload(preview.CreateImage());
            }
            catch (GameplaySkinScenePreparationException)
            {
                failure.Alpha = 1;
                Logger.Log("A captured skin option thumbnail could not be decoded.", level: LogLevel.Important);
                return;
            }

            try
            {
                texture = renderer.CreateTexture(upload.Width, upload.Height);
                texture.SetData(upload);
                sprite.Texture = texture;
            }
            catch
            {
                upload.Dispose();
                texture?.Dispose();
                texture = null;
                throw;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            texture?.Dispose();
        }
    }
}
