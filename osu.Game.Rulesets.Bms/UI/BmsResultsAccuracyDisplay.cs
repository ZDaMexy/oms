// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking;
using osu.Game.Screens.Ranking.Expanded.Accuracy;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class BmsResultsAccuracyDisplay : CompositeDrawable
    {
        private const float accuracy_circle_radius = 0.2f;

        private readonly ScoreInfo score;
        private readonly bool withFlair;
        private readonly BmsDjLevelDisplayInfo levelInfo;

        [Resolved]
        private ResultsScreen? resultsScreen { get; set; }

        private CircularProgress accuracyCircle = null!;
        private DjLevelGradedCircles gradedCircles = null!;
        private Container<DjLevelBadge> badges = null!;
        private DjLevelText rankText = null!;

        private PoolableSkinnableSample? scoreTickSound;
        private PoolableSkinnableSample? badgeTickSound;
        private PoolableSkinnableSample? badgeMaxSound;
        private PoolableSkinnableSample? swooshUpSound;
        private PoolableSkinnableSample? rankImpactSound;

        private readonly Bindable<double> tickPlaybackRate = new Bindable<double>();

        private double lastTickPlaybackTime;
        private bool isTicking;

        public BmsResultsAccuracyDisplay(ScoreInfo score, bool withFlair = false)
        {
            this.score = score;
            this.withFlair = withFlair;
            levelInfo = BmsDjLevelDisplayInfo.FromScore(score);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var badgeLevels = BmsDjLevelDisplay.BadgeLevels;

            InternalChildren = new Drawable[]
            {
                new CircularProgress
                {
                    Name = "Background circle",
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Colour = OsuColour.Gray(47),
                    Alpha = 0.5f,
                    InnerRadius = accuracy_circle_radius + 0.01f,
                    Progress = 1,
                },
                accuracyCircle = new CircularProgress
                {
                    Name = "Accuracy circle",
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Colour = ColourInfo.GradientVertical(Color4Extensions.FromHex("#7CF6FF"), Color4Extensions.FromHex("#BAFFA9")),
                    InnerRadius = accuracy_circle_radius,
                },
                new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Size = new Vector2(0.8f),
                    Padding = new MarginPadding(2.5f),
                    Child = gradedCircles = new DjLevelGradedCircles
                    {
                        RelativeSizeAxes = Axes.Both,
                    }
                },
                badges = new Container<DjLevelBadge>
                {
                    Name = "DJ level badges",
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Vertical = -15, Horizontal = -20 },
                    Children = badgeLevels.Select(level => new DjLevelBadge(BmsDjLevelDisplay.GetThreshold(level), getBadgePosition(level), level)).ToArray(),
                },
                rankText = new DjLevelText(levelInfo.Level),
            };

            if (withFlair)
            {
                AddRangeInternal(new Drawable[]
                {
                    rankImpactSound = new PoolableSkinnableSample(new SampleInfo(impactSampleName)),
                    scoreTickSound = new PoolableSkinnableSample(new SampleInfo(@"Results/score-tick")),
                    badgeTickSound = new PoolableSkinnableSample(new SampleInfo(@"Results/badge-dink")),
                    badgeMaxSound = new PoolableSkinnableSample(new SampleInfo(@"Results/badge-dink-max")),
                    swooshUpSound = new PoolableSkinnableSample(new SampleInfo(@"Results/swoosh-up")),
                });
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            this.ScaleTo(0).Then().ScaleTo(1, AccuracyCircle.APPEAR_DURATION, Easing.OutQuint);

            if (withFlair)
            {
                const double swoosh_pre_delay = 443f;
                const double swoosh_volume = 0.4f;

                this.Delay(swoosh_pre_delay).Schedule(() =>
                {
                    swooshUpSound!.VolumeTo(swoosh_volume);
                    swooshUpSound!.Play();
                });
            }

            using (BeginDelayedSequence(AccuracyCircle.RANK_CIRCLE_TRANSFORM_DELAY))
                gradedCircles.TransformTo(nameof(DjLevelGradedCircles.Progress), 1.0, AccuracyCircle.RANK_CIRCLE_TRANSFORM_DURATION, AccuracyCircle.ACCURACY_TRANSFORM_EASING);

            using (BeginDelayedSequence(AccuracyCircle.ACCURACY_TRANSFORM_DELAY))
            {
                double targetAccuracy = levelInfo.ExRatio;
                double[] notchPercentages =
                {
                    BmsDjLevelDisplay.GetThreshold(BmsDjLevel.E),
                    BmsDjLevelDisplay.GetThreshold(BmsDjLevel.D),
                    BmsDjLevelDisplay.GetThreshold(BmsDjLevel.C),
                    BmsDjLevelDisplay.GetThreshold(BmsDjLevel.B),
                    BmsDjLevelDisplay.GetThreshold(BmsDjLevel.A),
                    BmsDjLevelDisplay.GetThreshold(BmsDjLevel.AA),
                    BmsDjLevelDisplay.GetThreshold(BmsDjLevel.AAA),
                };

                foreach (double p in notchPercentages)
                {
                    if (Precision.AlmostEquals(p, targetAccuracy, AccuracyCircle.GRADE_SPACING_PERCENTAGE / 2))
                    {
                        int tippingDirection = targetAccuracy - p >= 0 ? 1 : -1;
                        targetAccuracy = p + tippingDirection * (AccuracyCircle.GRADE_SPACING_PERCENTAGE / 2);
                        break;
                    }
                }

                const double visual_alignment_offset = 0.001;

                if (targetAccuracy < 1 && targetAccuracy >= visual_alignment_offset)
                    targetAccuracy -= visual_alignment_offset;

                accuracyCircle.ProgressTo(targetAccuracy, AccuracyCircle.ACCURACY_TRANSFORM_DURATION, AccuracyCircle.ACCURACY_TRANSFORM_EASING);

                if (withFlair)
                {
                    Schedule(() =>
                    {
                        const double score_tick_debounce_rate_start = 18f;
                        const double score_tick_debounce_rate_end = 300f;
                        const double score_tick_volume_start = 0.6f;
                        const double score_tick_volume_end = 1.0f;

                        this.TransformBindableTo(tickPlaybackRate, score_tick_debounce_rate_start);
                        this.TransformBindableTo(tickPlaybackRate, score_tick_debounce_rate_end, AccuracyCircle.ACCURACY_TRANSFORM_DURATION, Easing.OutSine);

                        scoreTickSound!.FrequencyTo(1 + targetAccuracy, AccuracyCircle.ACCURACY_TRANSFORM_DURATION, Easing.OutSine);
                        scoreTickSound!.VolumeTo(score_tick_volume_start).Then().VolumeTo(score_tick_volume_end, AccuracyCircle.ACCURACY_TRANSFORM_DURATION, Easing.OutSine);

                        isTicking = true;
                    });
                }

                var visibleBadges = badges.Where(b => b.Accuracy <= levelInfo.ExRatio).ToArray();

                for (int i = 0; i < visibleBadges.Length; i++)
                {
                    var badge = visibleBadges[i];

                    using (BeginDelayedSequence(inverseEasing(AccuracyCircle.ACCURACY_TRANSFORM_EASING, badge.Accuracy / Math.Max(targetAccuracy, 0.0001)) * AccuracyCircle.ACCURACY_TRANSFORM_DURATION))
                    {
                        badge.Appear();

                        if (withFlair)
                        {
                            bool lastVisibleBadge = i == visibleBadges.Length - 1;

                            Schedule(() =>
                            {
                                var dink = lastVisibleBadge ? badgeMaxSound : badgeTickSound;

                                dink!.FrequencyTo(1 + i * 0.05);
                                dink!.Play();
                            });
                        }
                    }
                }

                using (BeginDelayedSequence(AccuracyCircle.TEXT_APPEAR_DELAY))
                {
                    rankText.Appear();

                    if (withFlair)
                    {
                        Schedule(() =>
                        {
                            isTicking = false;
                            rankImpactSound!.Play();
                        });

                        const double applause_pre_delay = 545f;

                        using (BeginDelayedSequence(applause_pre_delay))
                            Schedule(() => resultsScreen?.PlayApplause(score.Rank));
                    }
                }
            }
        }

        protected override void Update()
        {
            base.Update();

            if (isTicking && Clock.CurrentTime - lastTickPlaybackTime >= tickPlaybackRate.Value)
            {
                scoreTickSound?.Play();
                lastTickPlaybackTime = Clock.CurrentTime;
            }
        }

        private string impactSampleName
        {
            get
            {
                switch (score.Rank)
                {
                    default:
                    case ScoreRank.D:
                    case ScoreRank.F:
                        return @"Results/rank-impact-fail-d";

                    case ScoreRank.C:
                    case ScoreRank.B:
                        return @"Results/rank-impact-fail";

                    case ScoreRank.A:
                    case ScoreRank.S:
                    case ScoreRank.SH:
                        return @"Results/rank-impact-pass";

                    case ScoreRank.X:
                    case ScoreRank.XH:
                        return @"Results/rank-impact-pass-ss";
                }
            }
        }

        private static double getBadgePosition(BmsDjLevel level)
            => level switch
            {
                BmsDjLevel.E => Interpolation.Lerp(BmsDjLevelDisplay.GetThreshold(BmsDjLevel.E), BmsDjLevelDisplay.GetThreshold(BmsDjLevel.D), 0.5),
                BmsDjLevel.D => Interpolation.Lerp(BmsDjLevelDisplay.GetThreshold(BmsDjLevel.D), BmsDjLevelDisplay.GetThreshold(BmsDjLevel.C), 0.5),
                BmsDjLevel.C => Interpolation.Lerp(BmsDjLevelDisplay.GetThreshold(BmsDjLevel.C), BmsDjLevelDisplay.GetThreshold(BmsDjLevel.B), 0.5),
                BmsDjLevel.B => Interpolation.Lerp(BmsDjLevelDisplay.GetThreshold(BmsDjLevel.B), BmsDjLevelDisplay.GetThreshold(BmsDjLevel.A), 0.5),
                BmsDjLevel.A => Interpolation.Lerp(BmsDjLevelDisplay.GetThreshold(BmsDjLevel.A), BmsDjLevelDisplay.GetThreshold(BmsDjLevel.AA), 0.25),
                BmsDjLevel.AA => Interpolation.Lerp(BmsDjLevelDisplay.GetThreshold(BmsDjLevel.AA), BmsDjLevelDisplay.GetThreshold(BmsDjLevel.AAA), 0.25),
                BmsDjLevel.AAA => 1.0,
                _ => 0,
            };

        private double inverseEasing(Easing easing, double targetValue)
        {
            double test = 0;
            double result = 0;
            int count = 2;

            while (Math.Abs(result - targetValue) > 0.005)
            {
                int dir = Math.Sign(targetValue - result);

                test += dir * 1.0 / count;
                result = Interpolation.ApplyEasing(easing, test);

                count++;
            }

            return test;
        }

        private partial class DjLevelBadge : CompositeDrawable
        {
            public readonly double Accuracy;

            private readonly double displayPosition;
            private readonly BmsDjLevel level;

            private Drawable rankContainer = null!;
            private Drawable overlay = null!;

            public DjLevelBadge(double accuracy, double position, BmsDjLevel level)
            {
                Accuracy = accuracy;
                displayPosition = position;
                this.level = level;

                RelativeSizeAxes = Axes.Both;
                Alpha = 0;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChild = rankContainer = new Container
                {
                    Origin = Anchor.Centre,
                    Size = new Vector2(28, 14),
                    Children = new Drawable[]
                    {
                        new BmsDrawableDjLevel(level),
                        overlay = new CircularContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Blending = BlendingParameters.Additive,
                            Masking = true,
                            EdgeEffect = new EdgeEffectParameters
                            {
                                Type = EdgeEffectType.Glow,
                                Colour = BmsDjLevelDisplay.GetFillColour(level).Opacity(0.2f),
                                Radius = 10,
                            },
                            Child = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Alpha = 0,
                                AlwaysPresent = true,
                            }
                        }
                    }
                };
            }

            public void Appear()
            {
                this.FadeIn(50);
                overlay.FadeIn().FadeOut(500, Easing.In);
            }

            protected override void Update()
            {
                base.Update();
                rankContainer.Position = circlePosition(-MathF.PI / 2 - (1 - (float)displayPosition) * MathF.PI * 2);
            }

            private Vector2 circlePosition(float t)
                => DrawSize / 2 + new Vector2(MathF.Cos(t), MathF.Sin(t)) * DrawSize / 2;
        }

        private partial class DjLevelText : CompositeDrawable
        {
            private const float label_offset_y = -34;
            private const float rank_offset_y = 10;

            private readonly BmsDjLevel level;
            private readonly string text;

            private Container rankContainer = null!;
            private BufferedContainer flash = null!;
            private BufferedContainer superFlash = null!;
            private GlowingSpriteText rankText = null!;

            public DjLevelText(BmsDjLevel level)
            {
                this.level = level;
                text = BmsDjLevelDisplay.GetText(level);

                Anchor = Anchor.Centre;
                Origin = Anchor.Centre;

                Alpha = 0;
                Size = new Vector2(240, 160);
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                var glowColour = BmsDjLevelDisplay.GetFillColour(level);
                var spacing = new Vector2(getLetterSpacing(text), 0);
                float fontSize = getFontSize(text);

                InternalChildren = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Y = label_offset_y,
                        Text = BmsDjLevelDisplay.LabelText,
                        Font = OsuFont.Torus.With(size: 12),
                        Spacing = new Vector2(0.5f, 0),
                        Colour = Color4.White.Opacity(0.75f),
                    },
                    rankContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            rankText = new GlowingSpriteText
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Y = rank_offset_y,
                                GlowColour = glowColour,
                                Spacing = spacing,
                                Text = text,
                                Font = OsuFont.Numeric.With(size: fontSize),
                                UseFullGlyphHeight = false,
                            },
                            superFlash = new BufferedContainer(cachedFrameBuffer: true)
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Y = rank_offset_y,
                                BlurSigma = new Vector2(85),
                                Size = new Vector2(600),
                                Blending = BlendingParameters.Additive,
                                Alpha = 0,
                                Children = new[]
                                {
                                    new Box
                                    {
                                        Colour = Color4.White,
                                        Size = new Vector2(150),
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                    },
                                },
                            },
                            flash = new BufferedContainer(cachedFrameBuffer: true)
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Y = rank_offset_y,
                                BlurSigma = new Vector2(35),
                                BypassAutoSizeAxes = Axes.Both,
                                Size = new Vector2(240),
                                Blending = BlendingParameters.Additive,
                                Alpha = 0,
                                Scale = new Vector2(1.8f),
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Spacing = spacing,
                                        Text = text,
                                        Font = OsuFont.Numeric.With(size: fontSize),
                                        UseFullGlyphHeight = false,
                                        Shadow = false,
                                    },
                                },
                            },
                        }
                    },
                };
            }

            public void Appear()
            {
                this.FadeIn();

                if (level < BmsDjLevel.A)
                {
                    rankContainer.MoveToOffset(new Vector2(0, -20))
                                 .MoveToOffset(new Vector2(0, 20), 200, Easing.OutBounce);

                    if (level <= BmsDjLevel.D)
                    {
                        rankContainer.Delay(700)
                                     .RotateTo(5, 150, Easing.In)
                                     .MoveToOffset(new Vector2(0, 3), 150, Easing.In);
                    }

                    this.FadeInFromZero(200, Easing.OutQuint);
                    return;
                }

                flash.Colour = BmsDjLevelDisplay.GetFillColour(level);

                if (level >= BmsDjLevel.AA)
                    rankText.ScaleTo(1.05f).ScaleTo(1, 3000, Easing.OutQuint);

                if (level >= BmsDjLevel.AAA)
                {
                    flash.FadeOutFromOne(3000);
                    superFlash.FadeOutFromOne(800, Easing.OutQuint);
                }
                else
                {
                    flash.FadeOutFromOne(1200, Easing.OutQuint);
                }
            }

            private static float getLetterSpacing(string displayText)
                => displayText.Length switch
                {
                    1 => -15,
                    2 => -11,
                    _ => -8,
                };

            private static float getFontSize(string displayText)
                => displayText.Length switch
                {
                    1 => 76,
                    2 => 72,
                    _ => 64,
                };
        }

        private partial class DjLevelGradedCircles : CompositeDrawable
        {
            private double progress;

            public double Progress
            {
                get => progress;
                set
                {
                    progress = value;

                    foreach (var circle in circles)
                        circle.RevealProgress = value;
                }
            }

            private readonly Container<GradedCircle> circles;

            public DjLevelGradedCircles()
            {
                InternalChild = circles = new Container<GradedCircle>
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = BmsDjLevelDisplay.CircleLevels.Select((level, index) =>
                    {
                        double start = BmsDjLevelDisplay.GetThreshold(level);
                        double end = index == BmsDjLevelDisplay.CircleLevels.Length - 1 ? 1.0 : BmsDjLevelDisplay.GetThreshold(BmsDjLevelDisplay.CircleLevels[index + 1]);

                        return new GradedCircle(start, end)
                        {
                            Colour = BmsDjLevelDisplay.GetFillColour(level),
                        };
                    }).ToArray(),
                };
            }

            private partial class GradedCircle : CircularProgress
            {
                public double RevealProgress
                {
                    set => Progress = Math.Clamp(value, startProgress, endProgress) - startProgress;
                }

                private readonly double startProgress;
                private readonly double endProgress;

                public GradedCircle(double startProgress, double endProgress)
                {
                    this.startProgress = startProgress + AccuracyCircle.GRADE_SPACING_PERCENTAGE * 0.5;
                    this.endProgress = endProgress - AccuracyCircle.GRADE_SPACING_PERCENTAGE * 0.5;

                    Anchor = Anchor.Centre;
                    Origin = Anchor.Centre;
                    RelativeSizeAxes = Axes.Both;
                    InnerRadius = AccuracyCircle.RANK_CIRCLE_RADIUS;
                    Rotation = (float)this.startProgress * 360;
                }
            }
        }
    }
}
