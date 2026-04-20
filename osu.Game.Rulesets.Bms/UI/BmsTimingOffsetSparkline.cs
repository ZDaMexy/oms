// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    internal sealed partial class BmsTimingOffsetSparkline : CompositeDrawable
    {
        private const float sparkline_width = 144;
        private const float sparkline_height = 22;
        private const float horizontal_padding = 6;
        private const float vertical_padding = 3;
        private const float marker_size = 4;

        private readonly IBindableList<BmsJudgementTimingFeedback> recentJudgementFeedbacks = new BindableList<BmsJudgementTimingFeedback>();
        private readonly IBindable<double> timingFeedbackVisualRange;

        private Container markerContainer = null!;

        public BmsTimingOffsetSparkline(IBindableList<BmsJudgementTimingFeedback> recentJudgementFeedbacks, IBindable<double> timingFeedbackVisualRange)
        {
            this.timingFeedbackVisualRange = timingFeedbackVisualRange.GetBoundCopy();

            this.recentJudgementFeedbacks.BindTo(recentJudgementFeedbacks);

            Size = new Vector2(sparkline_width, sparkline_height);

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding
                {
                    Horizontal = horizontal_padding,
                    Vertical = vertical_padding,
                },
                Children = new Drawable[]
                {
                    new Box
                    {
                        Name = "timing-offset-axis",
                        RelativeSizeAxes = Axes.X,
                        Height = 1,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Colour = BmsDefaultHudPalette.SurfaceBorder.Opacity(0.75f),
                    },
                    new Box
                    {
                        Name = "timing-offset-centre",
                        Width = 1,
                        RelativeSizeAxes = Axes.Y,
                        Height = 0.7f,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Colour = BmsDefaultHudPalette.SurfaceText.Opacity(0.35f),
                    },
                    markerContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            recentJudgementFeedbacks.BindCollectionChanged((_, _) => updateMarkers(), true);
            timingFeedbackVisualRange.BindValueChanged(_ => updateMarkers(), true);
        }

        private void updateMarkers()
        {
            markerContainer.Clear();

            var timedFeedbacks = recentJudgementFeedbacks.Where(feedback => feedback.ShowsTimingDirection).ToArray();

            if (timedFeedbacks.Length == 0)
                return;

            double range = System.Math.Max(timingFeedbackVisualRange.Value, 1);
            float usableWidth = sparkline_width - horizontal_padding * 2 - marker_size;
            float usableHeight = sparkline_height - vertical_padding * 2 - marker_size;
            float halfWidth = usableWidth / 2;

            for (int i = 0; i < timedFeedbacks.Length; i++)
            {
                BmsJudgementTimingFeedback feedback = timedFeedbacks[i];
                float clampedRatio = (float)System.Math.Clamp(feedback.TimeOffset / range, -1, 1);
                float x = halfWidth + clampedRatio * halfWidth;
                float y = timedFeedbacks.Length == 1 ? usableHeight / 2 : usableHeight * i / (timedFeedbacks.Length - 1);

                markerContainer.Add(new Circle
                {
                    Name = "timing-offset-marker",
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Position = new Vector2(x, y),
                    Size = new Vector2(marker_size),
                    Colour = getMarkerColour(feedback, i == timedFeedbacks.Length - 1),
                });
            }
        }

        private static Color4 getMarkerColour(BmsJudgementTimingFeedback feedback, bool isLatest)
        {
            Color4 baseColour = feedback.TimeOffset switch
            {
                < 0 => BmsDefaultHudPalette.SpeedFeedbackAccent,
                > 0 => BmsDefaultHudPalette.SpeedFeedbackWarning,
                _ => BmsDefaultHudPalette.SurfaceText,
            };

            return isLatest ? baseColour : baseColour.Opacity(0.55f);
        }
    }
}
