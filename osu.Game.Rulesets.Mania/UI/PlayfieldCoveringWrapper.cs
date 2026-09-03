// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Utils;
using osu.Game.Rulesets.UI.Scrolling;
using osuTK;
using osuTK.Graphics;
using Container = osu.Framework.Graphics.Containers.Container;

namespace osu.Game.Rulesets.Mania.UI
{
    /// <summary>
    /// A <see cref="Container"/> that has its contents partially hidden by an adjustable "cover". This is intended to be used in a playfield.
    /// </summary>
    public partial class PlayfieldCoveringWrapper : CompositeDrawable
    {
        /// <summary>
        /// The relative area that should be completely covered. This does not include the fade.
        /// </summary>
        public readonly BindableFloat Coverage = new BindableFloat();

        /// <summary>
        /// The complete cover, including gradient and fill.
        /// </summary>
        private readonly Container cover;

        /// <summary>
        /// Visible author-scene geometry. This deliberately sits outside the buffered alpha-subtraction pass: authored
        /// lane-cover visuals may decorate the engine-owned cover, but can never alter which gameplay content is clipped.
        /// </summary>
        private readonly Container sceneCover;

        private readonly Container sceneClip;

        internal Container GameplaySkinFillSceneOwner { get; }

        internal Container GameplaySkinDecorationSceneOwner { get; }

        internal float GameplaySkinSceneCoverageHeight => sceneClip.Height;

        internal float GameplaySkinSceneRotation => sceneCover.Rotation;

        internal Vector2 GameplaySkinSceneScale => sceneCover.Scale;

        /// <summary>
        /// The gradient portion of the cover.
        /// </summary>
        private readonly Box gradient;

        /// <summary>
        /// The fully-opaque portion of the cover.
        /// </summary>
        private readonly Box filled;

        private readonly IBindable<ScrollingDirection> scrollDirection = new Bindable<ScrollingDirection>();

        private float currentCoverageHeight;

        public PlayfieldCoveringWrapper(Drawable content)
        {
            GameplaySkinFillSceneOwner = new Container { RelativeSizeAxes = Axes.Both };
            GameplaySkinDecorationSceneOwner = new Container { RelativeSizeAxes = Axes.Both };

            InternalChildren = new Drawable[]
            {
                new BufferedContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new[]
                    {
                        content,
                        cover = new Container
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            RelativeSizeAxes = Axes.Both,
                            Blending = new BlendingParameters
                            {
                                // Don't change the destination colour.
                                RGBEquation = BlendingEquation.Add,
                                Source = BlendingType.Zero,
                                Destination = BlendingType.One,
                                // Subtract the cover's alpha from the destination (points with alpha 1 should make the destination completely transparent).
                                AlphaEquation = BlendingEquation.Add,
                                SourceAlpha = BlendingType.Zero,
                                DestinationAlpha = BlendingType.OneMinusSrcAlpha
                            },
                            Children = new Drawable[]
                            {
                                gradient = new Box
                                {
                                    Anchor = Anchor.BottomLeft,
                                    Origin = Anchor.BottomLeft,
                                    RelativeSizeAxes = Axes.Both,
                                    RelativePositionAxes = Axes.Both,
                                    Height = 0.25f,
                                    Colour = ColourInfo.GradientVertical(
                                        Color4.White.Opacity(0f),
                                        Color4.White.Opacity(1f)
                                    )
                                },
                                filled = new Box
                                {
                                    Anchor = Anchor.BottomLeft,
                                    Origin = Anchor.BottomLeft,
                                    RelativeSizeAxes = Axes.Both,
                                    Height = 0
                                }
                            }
                        }
                    }
                },
                sceneCover = new Container
                {
                    Name = "Gameplay skin lane-cover scene geometry",
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Child = sceneClip = new Container
                    {
                        Name = "Gameplay skin lane-cover scene clip",
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        RelativeSizeAxes = Axes.Both,
                        Height = 0,
                        Alpha = 0,
                        Masking = true,
                        Children = new Drawable[]
                        {
                            GameplaySkinFillSceneOwner,
                            GameplaySkinDecorationSceneOwner,
                        }
                    }
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(IScrollingInfo scrollingInfo)
        {
            scrollDirection.BindTo(scrollingInfo.Direction);
            scrollDirection.BindValueChanged(onScrollDirectionChanged, true);
            Coverage.BindValueChanged(coverage =>
            {
                // A pooled/inactive stage does not receive Update(), but its exact authored cover must not retain
                // stale pixels after the engine producer closes coverage (notably at a break boundary). Snap while
                // inactive, and always close zero immediately; visible non-zero changes retain the native damping.
                if (LoadState < LoadState.Loaded || !IsPresent || coverage.NewValue <= 0 || Time.Elapsed <= 0)
                    updateCoverSize(true);
            });

            // Initialise both the native mask and the visible author clip from the already-bound mod value during
            // dependency loading. Nested mania stages may remain at Ready until first active draw, but must still
            // expose one coherent cover geometry before any prepared scene visual is attached.
            updateCoverSize(true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            updateCoverSize(true);
        }

        protected override void Update()
        {
            base.Update();
            updateCoverSize(false);
        }

        private void updateCoverSize(bool instant)
        {
            float targetCoverage;
            float targetAlpha;

            if (instant)
            {
                targetCoverage = Coverage.Value;
                targetAlpha = Coverage.Value > 0 ? 1 : 0;
            }
            else
            {
                targetCoverage = (float)Interpolation.DampContinuously(currentCoverageHeight, Coverage.Value, 25, Math.Abs(Time.Elapsed));
                targetAlpha = (float)Interpolation.DampContinuously(gradient.Alpha, Coverage.Value > 0 ? 1 : 0, 25, Math.Abs(Time.Elapsed));
            }

            float coverageHeight = GetHeight(targetCoverage);
            filled.Height = coverageHeight;
            gradient.Y = -coverageHeight;
            gradient.Alpha = targetAlpha;
            sceneClip.Height = coverageHeight;
            sceneClip.Alpha = coverageHeight > 0 ? 1 : 0;

            currentCoverageHeight = targetCoverage;
        }

        protected virtual float GetHeight(float coverage) => coverage;

        private void onScrollDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            float rotation = direction.NewValue == ScrollingDirection.Up ? 0 : 180f;
            cover.Rotation = rotation;
            sceneCover.Rotation = rotation;
        }

        /// <summary>
        /// The direction in which the cover expands.
        /// </summary>
        public CoverExpandDirection Direction
        {
            set
            {
                Vector2 scale = value == CoverExpandDirection.AlongScroll ? Vector2.One : new Vector2(1, -1);
                cover.Scale = scale;
                sceneCover.Scale = scale;
            }
        }
    }

    public enum CoverExpandDirection
    {
        /// <summary>
        /// The cover expands along the scrolling direction.
        /// </summary>
        [Description("Along scroll")]
        AlongScroll,

        /// <summary>
        /// The cover expands against the scrolling direction.
        /// </summary>
        [Description("Against scroll")]
        AgainstScroll
    }
}
