// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK.Graphics;

namespace osu.Game.Beatmaps.Drawables
{
    /// <summary>
    /// A pill that displays an IIDX-style BMS difficulty level (e.g. "NORMAL 7") in place of the numeric
    /// <see cref="StarRatingDisplay"/>. Used in BMS mode only — the background colour encodes the <c>#DIFFICULTY</c>
    /// tier and the text is the category label plus the raw <c>#PLAYLEVEL</c>. Mirrors the star pill's rounded shape.
    /// </summary>
    public partial class BmsDifficultyLevelDisplay : CompositeDrawable
    {
        private readonly Box background;
        private readonly OsuSpriteText levelText;

        private int tier;

        /// <summary>
        /// The difficulty colour currently displayed (the pill background).
        /// </summary>
        public Color4 DisplayedDifficultyColour => background.Colour;

        /// <summary>
        /// The text colour currently displayed.
        /// </summary>
        public Color4 DisplayedDifficultyTextColour => levelText.Colour;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public BmsDifficultyLevelDisplay(StarRatingDisplaySize size = StarRatingDisplaySize.Regular)
        {
            AutoSizeAxes = Axes.Both;

            MarginPadding margin = default;

            switch (size)
            {
                case StarRatingDisplaySize.Small:
                    margin = new MarginPadding { Horizontal = 7f };
                    break;

                case StarRatingDisplaySize.Range:
                    margin = new MarginPadding { Horizontal = 8f };
                    break;

                case StarRatingDisplaySize.Regular:
                    margin = new MarginPadding { Horizontal = 8f, Vertical = 2f };
                    break;
            }

            InternalChild = new CircularContainer
            {
                Masking = true,
                AutoSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    levelText = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Margin = margin,
                        Font = OsuFont.Torus.With(size: 14.4f, weight: FontWeight.Bold),
                        Shadow = false,
                    },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            applyColours();
        }

        /// <summary>
        /// Sets the pill content. <paramref name="tier"/> is the <c>#DIFFICULTY</c> tier (0 = UNKNOWN, 1-5 = the
        /// defined categories) which selects the IIDX colour; <paramref name="text"/> is the label-plus-level string.
        /// </summary>
        public void SetDifficulty(LocalisableString text, int tier)
        {
            this.tier = tier;
            levelText.Text = text;

            if (IsLoaded)
                applyColours();
        }

        private void applyColours()
        {
            background.Colour = colours.ForBmsDifficultyLevel(tier);
            levelText.Colour = colours.ForBmsDifficultyLevelText(tier);
        }
    }
}
