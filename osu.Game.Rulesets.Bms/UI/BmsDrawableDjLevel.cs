// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Scoring;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class BmsDrawableDjLevel : CompositeDrawable
    {
        public BmsDrawableDjLevel(BmsDjLevel level)
        {
            RelativeSizeAxes = Axes.Both;
            FillMode = FillMode.Fit;
            FillAspectRatio = 2;

            string text = BmsDjLevelDisplay.GetText(level);
            var fillColour = BmsDjLevelDisplay.GetFillColour(level);

            InternalChild = new DrawSizePreservingFillContainer
            {
                TargetDrawSize = new Vector2(64, 32),
                Strategy = DrawSizePreservationStrategy.Minimum,
                Child = new CircularContainer
                {
                    Masking = true,
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = fillColour,
                        },
                        new Triangles
                        {
                            RelativeSizeAxes = Axes.Both,
                            ColourDark = fillColour.Darken(0.1f),
                            ColourLight = fillColour.Lighten(0.1f),
                            Velocity = 0.25f,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Spacing = new Vector2(getLetterSpacing(text), 0),
                            Padding = new MarginPadding { Top = 5 },
                            Colour = BmsDjLevelDisplay.GetTextColour(level),
                            Font = OsuFont.Numeric.With(size: getFontSize(text)),
                            Text = text,
                            ShadowColour = Color4.Black.Opacity(0.3f),
                            ShadowOffset = new Vector2(0, 0.08f),
                            Shadow = true,
                        },
                    }
                }
            };
        }

        private static float getLetterSpacing(string text)
            => text.Length switch
            {
                1 => -3,
                2 => -2.6f,
                _ => -2.1f,
            };

        private static float getFontSize(string text)
            => text.Length switch
            {
                1 => 25,
                2 => 24,
                _ => 22,
            };
    }
}
