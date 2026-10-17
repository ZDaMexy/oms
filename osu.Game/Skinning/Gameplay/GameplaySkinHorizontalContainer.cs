// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osuTK;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// A bounded local presentation row. Its carrier is still the exact prepared C3 rectangle; only its direct
    /// leaf envelopes use logical pixels. It owns no clock, native gameplay geometry, resource or text atlas.
    /// </summary>
    internal sealed partial class GameplaySkinHorizontalContainer : Container
    {
        private readonly float gap;
        private readonly List<Cell> cells = new List<Cell>();

        internal GameplaySkinHorizontalContainer(float gap)
        {
            this.gap = gap;
            RelativeSizeAxes = Axes.Both;
        }

        internal void RegisterCell(Drawable root, Drawable content, GameplaySkinSceneNode source)
        {
            root.RelativePositionAxes = Axes.None;
            root.RelativeSizeAxes = Axes.None;
            root.Anchor = Anchor.Centre;
            root.Origin = Anchor.TopLeft;
            float height = source.Properties.TryGetValue("height-px", out GameplaySkinScenePropertyValue? h) ? (float)h.NumberValue : 0;
            float? width = source.Properties.TryGetValue("width-px", out GameplaySkinScenePropertyValue? w) ? (float)w.NumberValue : null;
            cells.Add(new Cell(root, content, width, height));
        }

        internal void LayoutCells()
        {
            float cursor = 0;
            float left = 0;
            float right = 0;

            // Preserve source order even when depth sorting or a negative gap changes visual overlap order.
            foreach (Cell cell in cells)
            {
                Vector2 size = cell.Content switch
                {
                    GameplaySkinBitmapText text => text.NaturalSize,
                    Sprite sprite => new Vector2(cell.Width ?? cell.Height * sprite.Texture!.Width / sprite.Texture.Height, cell.Height),
                    _ => throw new InvalidOperationException("A horizontal row contains a non-leaf visual."),
                };
                cell.Root.Size = size;
                cell.Root.Position = new Vector2(cursor, -size.Y / 2);
                left = Math.Min(left, cursor);
                right = Math.Max(right, cursor + size.X);
                cursor += size.X + gap;
            }

            float offset = -(left + right) / 2;
            foreach (Cell cell in cells)
                cell.Root.X += offset;
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();
            LayoutCells();
        }

        private readonly record struct Cell(Drawable Root, Drawable Content, float? Width, float Height);
    }
}
