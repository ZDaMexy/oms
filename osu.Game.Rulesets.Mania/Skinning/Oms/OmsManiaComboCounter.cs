// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    public partial class OmsManiaComboCounter : CompositeDrawable, ISerialisableDrawable
    {
        public bool UsesFixedAnchor { get; set; }

        public Bindable<int> Current { get; } = new BindableInt { MinValue = 0 };

        public virtual int DisplayedCount
        {
            get => displayedCount;
            private set
            {
                if (displayedCount.Equals(value))
                    return;

                displayedCountText.Alpha = value == 0 ? 0 : 1;
                displayedCountText.Text = value.ToString(CultureInfo.InvariantCulture);
                counterContainer.Size = displayedCountText.Size;

                displayedCount = value;
            }
        }

        private int displayedCount;

        private int previousValue;

        private const float increment_scale = 1.15f;
        private const double increment_pulse_duration = 180;

        private static readonly FontUsage counter_font = OsuFont.Numeric.With(size: 34, fixedWidth: true);

        private Container counterContainer = null!;
        private OsuSpriteText displayedCountText = null!;

        [BackgroundDependencyLoader]
        private void load(ScoreProcessor scoreProcessor)
        {
            AutoSizeAxes = Axes.Both;

            InternalChildren = new[]
            {
                counterContainer = new Container
                {
                    AlwaysPresent = true,
                    Children = new[]
                    {
                        displayedCountText = createCounterText(),
                    }
                }
            };

            Current.BindTo(scoreProcessor.Combo);
        }

        private static OsuSpriteText createCounterText() => new OsuSpriteText
        {
            Font = counter_font,
            Shadow = false,
            Alpha = 0,
            AlwaysPresent = true,
            BypassAutoSizeAxes = Axes.Both,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Colour = Color4.White,
        };

        [Resolved]
        private IScrollingInfo scrollingInfo { get; set; } = null!;

        private IBindable<ScrollingDirection> direction = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            displayedCountText.Text = Current.Value.ToString(CultureInfo.InvariantCulture);

            Current.BindValueChanged(combo => updateCount(combo.NewValue), true);

            counterContainer.Size = displayedCountText.Size;

            direction = scrollingInfo.Direction.GetBoundCopy();
            direction.BindValueChanged(_ => updateAnchor());

            Schedule(() => Schedule(updateAnchor));
        }

        private void updateAnchor()
        {
            if (Anchor.HasFlag(Anchor.y1))
                return;

            Anchor &= ~(Anchor.y0 | Anchor.y2);
            Anchor |= direction.Value == ScrollingDirection.Up ? Anchor.y2 : Anchor.y0;

            Y = Math.Abs(Y) * (direction.Value == ScrollingDirection.Up ? -1 : 1);
        }

        private void updateCount(int newValue)
        {
            int previousCount = previousValue;
            previousValue = newValue;

            if (!IsLoaded)
                return;

            displayedCountText.ClearTransforms();
            displayedCountText.Scale = Vector2.One;

            DisplayedCount = newValue;

            if (newValue > 0 && previousCount + 1 == newValue)
            {
                displayedCountText.ScaleTo(new Vector2(1f, increment_scale))
                                  .ScaleTo(Vector2.One, increment_pulse_duration, Easing.OutQuint);
            }
        }
    }
}
