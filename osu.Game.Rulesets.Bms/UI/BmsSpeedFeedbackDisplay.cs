// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Threading;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    public interface IBmsSpeedFeedbackDisplay : ISerialisableDrawable
    {
    }

    public partial class DefaultBmsSpeedFeedbackDisplay : CompositeDrawable, IBmsSpeedFeedbackDisplay
    {
        private const float card_width = 172;
        private const double judgement_feedback_display_duration = 1200;

        private readonly bool resolveGameplayFeedbackStateFromRuleset;
        private readonly bool resolveRecentJudgementFeedbacksFromRuleset;
        private IBindable<BmsGameplayFeedbackState> gameplayFeedbackState = new Bindable<BmsGameplayFeedbackState>();
        private readonly IBindableList<BmsJudgementTimingFeedback> recentJudgementFeedbacks = new BindableList<BmsJudgementTimingFeedback>();
        private readonly BindableDouble timingFeedbackVisualRange = new BindableDouble(1);

        private Container card = null!;
        private Box accentBar = null!;
        private OsuSpriteText greenNumberText = null!;
        private OsuSpriteText visibleTimeText = null!;
        private OsuSpriteText detailText = null!;
        private OsuSpriteText pacemakerText = null!;
        private OsuSpriteText exScoreProgressText = null!;
        private OsuSpriteText liveRunStatusText = null!;
        private OsuSpriteText judgementSummaryPrimaryText = null!;
        private OsuSpriteText judgementSummarySecondaryText = null!;
        private OsuSpriteText judgementFeedbackText = null!;
        private ScheduledDelegate? judgementFeedbackExpiryDelegate;
        private bool judgementFeedbackVisible;
        private ulong visibleJudgementFeedbackOccurrenceId;

        [Resolved(CanBeNull = true)]
        private DrawableBmsRuleset? drawableRuleset { get; set; }

        public bool UsesFixedAnchor { get; set; }

        public DefaultBmsSpeedFeedbackDisplay(IBindable<BmsGameplayFeedbackState>? gameplayFeedbackState = null,
                                              IBindableList<BmsJudgementTimingFeedback>? recentJudgementFeedbacks = null)
        {
            resolveGameplayFeedbackStateFromRuleset = gameplayFeedbackState == null;
            resolveRecentJudgementFeedbacksFromRuleset = recentJudgementFeedbacks == null;
            this.gameplayFeedbackState = gameplayFeedbackState?.GetBoundCopy() ?? this.gameplayFeedbackState;

            if (recentJudgementFeedbacks != null)
                this.recentJudgementFeedbacks.BindTo(recentJudgementFeedbacks);

            AutoSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (drawableRuleset != null)
            {
                if (resolveGameplayFeedbackStateFromRuleset)
                    gameplayFeedbackState = drawableRuleset.GameplayFeedbackState.GetBoundCopy();

                if (resolveRecentJudgementFeedbacksFromRuleset)
                    recentJudgementFeedbacks.BindTo(drawableRuleset.RecentJudgementFeedbacks);
            }

            InternalChild = card = new Container
            {
                Width = card_width,
                AutoSizeAxes = Axes.Y,
                Masking = true,
                CornerRadius = 10,
                BorderThickness = 1,
                BorderColour = BmsDefaultHudPalette.SurfaceBorder,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientVertical(BmsDefaultHudPalette.SurfaceBackground, BmsDefaultHudPalette.SurfaceBackgroundAccent),
                    },
                    accentBar = new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 5,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 2),
                        Padding = new MarginPadding
                        {
                            Top = 10,
                            Right = 12,
                            Bottom = 10,
                            Left = 16,
                        },
                        Children = new Drawable[]
                        {
                            greenNumberText = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 22, weight: FontWeight.Bold),
                                Colour = BmsDefaultHudPalette.SpeedFeedbackAccent,
                            },
                            visibleTimeText = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                                Colour = BmsDefaultHudPalette.SurfaceText,
                            },
                            detailText = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 11, weight: FontWeight.Medium),
                                Colour = BmsDefaultHudPalette.SurfaceSubtext,
                            },
                            pacemakerText = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 10, weight: FontWeight.SemiBold),
                                Colour = BmsDefaultHudPalette.SurfaceSubtext,
                            },
                            exScoreProgressText = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 10, weight: FontWeight.Medium),
                                Colour = BmsDefaultHudPalette.SurfaceSubtext,
                            },
                            liveRunStatusText = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 10, weight: FontWeight.SemiBold),
                                Colour = BmsDefaultResultsPalette.LampPerfectAccent,
                            },
                            judgementSummaryPrimaryText = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 10, weight: FontWeight.Medium),
                                Colour = BmsDefaultHudPalette.SurfaceSubtext,
                            },
                            judgementSummarySecondaryText = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 10, weight: FontWeight.Medium),
                                Colour = BmsDefaultHudPalette.SurfaceSubtext,
                            },
                            judgementFeedbackText = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 10, weight: FontWeight.SemiBold),
                                Colour = BmsDefaultHudPalette.SurfaceText,
                            },
                            new BmsTimingOffsetSparkline(recentJudgementFeedbacks, timingFeedbackVisualRange),
                        }
                    }
                }
            };

            gameplayFeedbackState.BindValueChanged(onGameplayFeedbackStateChanged, true);
        }

        private void updateState()
        {
            BmsGameplayFeedbackState state = gameplayFeedbackState.Value;
            BmsScrollSpeedMetrics metrics = state.SpeedMetrics;
            bool unreadable = metrics.VisibleLaneUnits <= 0 || metrics.VisibleLaneTime <= 0;

            Color4 accentColour = unreadable ? BmsDefaultHudPalette.SpeedFeedbackWarning : BmsDefaultHudPalette.SpeedFeedbackAccent;

            greenNumberText.Text = unreadable
                ? @"GN ---"
                : $@"GN {metrics.GreenNumber}";
            greenNumberText.Colour = accentColour;

            visibleTimeText.Text = unreadable
                ? $@"0ms | WN {metrics.WhiteNumber}"
                : $@"{metrics.VisibleLaneTime:0}ms | WN {metrics.WhiteNumber}";

            detailText.Text = formatDetailLine(metrics, state.ActiveAdjustmentTarget, state.EnabledAdjustmentTargetCount, state.ActiveAdjustmentTargetIndex, state.IsAdjustmentTargetTemporarilyOverridden);
            pacemakerText.Text = formatExScorePacemaker(state.ExScorePacemakerInfo);
            exScoreProgressText.Text = formatExScoreProgress(state.ExScoreProgressInfo);
            liveRunStatusText.Text = formatLiveRunStatus(state.JudgementCounts);
            judgementSummaryPrimaryText.Text = formatJudgementSummaryPrimaryLine(state.JudgementCounts);
            judgementSummarySecondaryText.Text = formatJudgementSummarySecondaryLine(state.JudgementCounts);
            pacemakerText.Colour = getPacemakerColour(state.ExScorePacemakerInfo);
            liveRunStatusText.Colour = getLiveRunStatusColour(state.JudgementCounts);
            judgementFeedbackText.Text = judgementFeedbackVisible ? formatJudgementFeedback(state.LatestJudgementFeedback) : string.Empty;

            accentBar.Colour = accentColour;
            card.BorderColour = accentColour.Opacity(0.32f);
        }

        private void onGameplayFeedbackStateChanged(ValueChangedEvent<BmsGameplayFeedbackState> state)
        {
            timingFeedbackVisualRange.Value = state.NewValue.TimingFeedbackVisualRange;

            if (state.OldValue.LatestJudgementFeedback != state.NewValue.LatestJudgementFeedback)
            {
                updateJudgementFeedbackVisibility();
                return;
            }

            updateState();
        }

        private void updateJudgementFeedbackVisibility()
        {
            judgementFeedbackExpiryDelegate?.Cancel();

            BmsJudgementTimingFeedback? latestJudgementFeedback = gameplayFeedbackState.Value.LatestJudgementFeedback;

            judgementFeedbackVisible = latestJudgementFeedback.HasValue;

            if (judgementFeedbackVisible)
                visibleJudgementFeedbackOccurrenceId = latestJudgementFeedback!.Value.OccurrenceId;

            updateState();

            if (!judgementFeedbackVisible)
                return;

            ulong occurrenceId = visibleJudgementFeedbackOccurrenceId;

            judgementFeedbackExpiryDelegate = Scheduler.AddDelayed(() =>
            {
                if (gameplayFeedbackState.Value.LatestJudgementFeedback?.OccurrenceId != occurrenceId)
                    return;

                judgementFeedbackVisible = false;
                updateState();
            }, judgement_feedback_display_duration);
        }

        private static string formatDetailLine(BmsScrollSpeedMetrics metrics, BmsGameplayAdjustmentTarget? target, int enabledTargetCount, int activeTargetIndex, bool temporaryOverrideActive)
        {
            string targetText = temporaryOverrideActive && target != null
                ? $@"{target.Value.GetAbbreviation()} HOLD"
                : enabledTargetCount switch
            {
                <= 0 when target == null => @"NONE",
                <= 0 => target!.Value.GetAbbreviation(),
                1 when target != null => $@"{target.Value.GetAbbreviation()} ONLY",
                _ when target != null && activeTargetIndex >= 0 => $@"{target.Value.GetAbbreviation()} {activeTargetIndex + 1}/{enabledTargetCount}",
                _ when target != null => target.Value.GetAbbreviation(),
                _ => @"AUTO",
            };

            return $@"{metrics.HiSpeedMode.GetShortLabel()} {metrics.HiSpeedMode.FormatValue(metrics.ScrollSpeed)} | {targetText}";
        }

        private static string formatJudgementFeedback(BmsJudgementTimingFeedback? feedback)
        {
            if (!feedback.HasValue)
                return string.Empty;

            BmsJudgementTimingFeedback currentFeedback = feedback.Value;
            string resultText = BmsHitResultDisplayNames.GetDisplayName(currentFeedback.Result);

            if (!currentFeedback.ShowsTimingDirection)
                return resultText;

            string timingText = currentFeedback.TimeOffset switch
            {
                < 0 => $@"FAST {System.Math.Abs(currentFeedback.TimeOffset):0.#}ms",
                > 0 => $@"SLOW {System.Math.Abs(currentFeedback.TimeOffset):0.#}ms",
                _ => @"0ms",
            };

            return $@"{resultText} | {timingText}";
        }

        private static string formatExScorePacemaker(BmsExScorePacemakerInfo? pacemakerInfo)
        {
            if (!pacemakerInfo.HasValue)
                return string.Empty;

            long delta = pacemakerInfo.Value.Delta;
            string deltaText = delta >= 0 ? $@"+{delta}" : delta.ToString();
            return $@"PAC {pacemakerInfo.Value.TargetLevel} {deltaText}";
        }

        private static string formatExScoreProgress(BmsExScoreProgressInfo? exScoreProgressInfo)
        {
            if (!exScoreProgressInfo.HasValue)
                return string.Empty;

            return $@"DJ {BmsDjLevelDisplay.GetText(exScoreProgressInfo.Value.DjLevel)} | EX {exScoreProgressInfo.Value.CurrentExScore}/{exScoreProgressInfo.Value.MaximumExScore} {exScoreProgressInfo.Value.ExRatio * 100:0.00}%";
        }

        private static string formatLiveRunStatus(BmsJudgementCounts counts)
            => counts.CanStillPerfect
                ? @"LIVE PERFECT"
                : counts.CanStillFullCombo
                    ? $@"LIVE FC | GR {counts.GreatCount}"
                    : $@"FC LOST | {formatCompactJudgementLabel(counts.LeastSevereFullComboBreakResult ?? HitResult.Good)} {counts.LeastSevereFullComboBreakCount}";

        private static string formatJudgementSummaryPrimaryLine(BmsJudgementCounts counts)
            => $@"PGR {counts.PerfectCount} GR {counts.GreatCount} GD {counts.GoodCount}";

        private static string formatJudgementSummarySecondaryLine(BmsJudgementCounts counts)
            => $@"BD {counts.BadCount} PR {counts.PoorCount} EP {counts.EmptyPoorCount}";

        private static string formatCompactJudgementLabel(HitResult result)
            => result switch
            {
                HitResult.Perfect => @"PGR",
                HitResult.Great => @"GR",
                HitResult.Good => @"GD",
                HitResult.Meh => @"BD",
                HitResult.Miss => @"PR",
                HitResult.Ok => @"EP",
                _ => BmsHitResultDisplayNames.GetDisplayName(result),
            };

        private static Color4 getLiveRunStatusColour(BmsJudgementCounts counts)
            => counts.CanStillPerfect
                ? BmsDefaultResultsPalette.LampPerfectAccent
                : counts.CanStillFullCombo
                    ? BmsDefaultResultsPalette.LampFullComboAccent
                    : BmsDefaultHudPalette.SpeedFeedbackWarning;

        private static Color4 getPacemakerColour(BmsExScorePacemakerInfo? pacemakerInfo)
            => pacemakerInfo.HasValue && pacemakerInfo.Value.Delta >= 0
                ? BmsDefaultHudPalette.SpeedFeedbackAccent
                : BmsDefaultHudPalette.SpeedFeedbackWarning;
    }
}
