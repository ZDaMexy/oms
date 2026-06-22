// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Utils;
using osuTK.Graphics;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// Loading indicator for the <see cref="BeatmapMetadataDisplay"/> thumbnail (OMS, BMS mode): instead of a dim
    /// overlay + spinner, a scanline sweeps left→right "brightening" the initially-dimmed thumbnail. When a
    /// <see cref="GameplayLoadProgress"/> reports a determinate fraction (a BGA video transcode), the scanline tracks
    /// it; otherwise it ping-pongs (bright L→R, dim R→L) as an indeterminate activity indicator. On hide it sweeps to
    /// fully-bright and fades out, leaving the bright thumbnail underneath. Visual-only.
    /// </summary>
    public partial class ScanlineLoadingLayer : VisibilityContainer
    {
        private const double pingpong_period = 2400;
        private const float dim_alpha = 0.55f;

        private readonly Box dim;
        private readonly Container scanline;

        // 0 = fully dim, 1 = fully bright (revealed up to here from the left).
        private float reveal;
        private bool completing;

        [Resolved(CanBeNull = true)]
        private GameplayLoadProgress? loadProgress { get; set; }

        public ScanlineLoadingLayer()
        {
            RelativeSizeAxes = Axes.Both;
            Alpha = 0;

            InternalChildren = new Drawable[]
            {
                // Covers the not-yet-revealed RIGHT portion [reveal, 1]; recedes rightwards as reveal grows.
                dim = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Colour = Color4.Black,
                    Alpha = dim_alpha,
                },
                // The bright scan edge, riding the boundary between revealed (left) and dim (right).
                scanline = new Container
                {
                    RelativeSizeAxes = Axes.Y,
                    RelativePositionAxes = Axes.X,
                    Width = 3,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.Centre,
                    Masking = true,
                    Blending = BlendingParameters.Additive,
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Glow,
                        Colour = new Color4(1f, 1f, 1f, 0.6f),
                        Radius = 9,
                    },
                    Child = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White },
                },
            };
        }

        protected override void PopIn()
        {
            completing = false;
            reveal = 0;
            apply();
            this.FadeIn(150, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            // Final left→right sweep to fully bright (driven in Update) while the whole overlay fades, revealing the
            // bright thumbnail beneath — matching the brief fully-lit frame before gameplay starts.
            completing = true;
            this.FadeOut(260, Easing.OutQuint);
        }

        protected override void Update()
        {
            base.Update();

            float target;

            if (completing)
                target = 1;
            else if (loadProgress != null && loadProgress.TryGet(out float fraction))
                target = fraction;
            else
            {
                // Indeterminate ping-pong: scan bright L→R then dim back R→L, smoothly looping.
                double t = (Time.Current % pingpong_period) / pingpong_period;
                double triangle = t < 0.5 ? t * 2 : 2 - t * 2;
                reveal = (float)Interpolation.ApplyEasing(Easing.InOutSine, triangle);
                apply();
                return;
            }

            // Determinate / completing: ease toward the target so progress jumps and the final sweep stay smooth.
            reveal = (float)Interpolation.DampContinuously(reveal, target, completing ? 40 : 70, Math.Abs(Time.Elapsed));
            apply();
        }

        private void apply()
        {
            dim.Width = Math.Clamp(1 - reveal, 0, 1);
            scanline.X = Math.Clamp(reveal, 0, 1);
            // Nothing to "scan" once the edge reaches either border.
            scanline.Alpha = reveal <= 0.001f || reveal >= 0.999f ? 0 : 1;
        }
    }
}
