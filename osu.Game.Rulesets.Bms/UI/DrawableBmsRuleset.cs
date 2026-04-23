// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Input.Handlers;
using osu.Game.Overlays;
using osu.Game.Overlays.OSD;
using osu.Game.Replays;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osuTK.Input;

namespace osu.Game.Rulesets.Bms.UI
{
    [Cached]
    public partial class DrawableBmsRuleset : DrawableScrollingRuleset<HitObject>
    {
        public const double MIN_TIME_RANGE = 290;

        // Derived from the official-cab reference HS 10 + WN 350 => GN 300.
        public const double MAX_TIME_RANGE = 100000d / 13d;

        private const int max_recent_timing_feedbacks = 12;
        private const BmsDjLevel default_ex_score_pacemaker_target = BmsDjLevel.AAA;

        public new BmsPlayfield Playfield => (BmsPlayfield)base.Playfield;

        public BmsLongNoteMode LongNoteMode => BmsScoreProcessor.GetLongNoteMode(Mods);

        public BmsJudgeMode JudgeMode => BmsJudgeModeExtensions.GetJudgeMode(Mods);

        public override int Variant => BmsLaneLayout.CreateFor(Beatmap).Lanes.Count;

        protected override bool RelativeScaleBeatLengths => true;

        protected new BmsRulesetConfigManager Config => (BmsRulesetConfigManager)base.Config;

        private readonly Bindable<BmsHiSpeedMode> configHiSpeedMode = new Bindable<BmsHiSpeedMode>();
        private readonly BindableDouble configNormalHiSpeed = new BindableDouble();
        private readonly BindableDouble configFloatingHiSpeed = new BindableDouble();
        private readonly BindableDouble configClassicHiSpeed = new BindableDouble();
        private readonly BindableDouble selectedHiSpeed = new BindableDouble();
        private IBindable<double>? playfieldScrollLengthRatio;
        private readonly BindableInt configKeysoundConcurrentChannels = new BindableInt
        {
            Default = BmsKeysoundStore.DEFAULT_CONCURRENT_CHANNELS,
            MinValue = BmsKeysoundStore.MIN_CONCURRENT_CHANNELS,
            MaxValue = BmsKeysoundStore.MAX_CONCURRENT_CHANNELS,
            Precision = 1,
        };
        private readonly BindableBool laneCoverFocusPressed = new BindableBool();
        private readonly Bindable<BmsScrollSpeedMetrics> speedMetrics = new Bindable<BmsScrollSpeedMetrics>();
        private readonly Bindable<BmsGameplayAdjustmentTarget?> activeAdjustmentTarget = new Bindable<BmsGameplayAdjustmentTarget?>();
        private readonly BindableInt enabledAdjustmentTargetCount = new BindableInt();
        private readonly Bindable<int> activeAdjustmentTargetIndex = new Bindable<int>();
        private readonly BindableBool adjustmentTargetTemporarilyOverridden = new BindableBool();
        private readonly Bindable<BmsJudgementTimingFeedback?> latestJudgementFeedback = new Bindable<BmsJudgementTimingFeedback?>();
        private readonly BindableList<BmsJudgementTimingFeedback> recentJudgementFeedbacks = new BindableList<BmsJudgementTimingFeedback>();
        private readonly BindableDouble timingFeedbackVisualRange = new BindableDouble(1);
        private readonly Bindable<BmsExScorePacemakerInfo?> exScorePacemakerInfo = new Bindable<BmsExScorePacemakerInfo?>();
        private readonly Bindable<BmsGameplayFeedbackState> gameplayFeedbackState = new Bindable<BmsGameplayFeedbackState>();
        private readonly BindableBool allowAdjustmentWhilePaused = new BindableBool();
        private BmsGameplayAdjustmentTarget? currentGameplayAdjustmentTarget;
        private ulong judgementFeedbackOccurrenceId;
        private IBindable<long>? totalScore;

        public IBindable<BmsScrollSpeedMetrics> SpeedMetrics => speedMetrics;

        public IBindable<BmsHiSpeedMode> HiSpeedMode => configHiSpeedMode;

        public IBindable<double> SelectedHiSpeed => selectedHiSpeed;

        public IBindable<BmsGameplayAdjustmentTarget?> ActiveAdjustmentTarget => activeAdjustmentTarget;

        public IBindable<int> EnabledAdjustmentTargetCount => enabledAdjustmentTargetCount;

        public IBindable<int> ActiveAdjustmentTargetIndex => activeAdjustmentTargetIndex;

        public IBindable<bool> IsAdjustmentTargetTemporarilyOverridden => adjustmentTargetTemporarilyOverridden;

        public IBindable<BmsJudgementTimingFeedback?> LatestJudgementFeedback => latestJudgementFeedback;

        public IBindableList<BmsJudgementTimingFeedback> RecentJudgementFeedbacks => recentJudgementFeedbacks;

        public IBindable<double> TimingFeedbackVisualRange => timingFeedbackVisualRange;

        public IBindable<BmsExScorePacemakerInfo?> ExScorePacemakerInfo => exScorePacemakerInfo;

        public IBindable<BmsGameplayFeedbackState> GameplayFeedbackState => gameplayFeedbackState;

        public BmsInputManager? GameplayInputManager => KeyBindingInputManager as BmsInputManager;

        [Resolved(CanBeNull = true)]
        private OnScreenDisplay? bmsOnScreenDisplay { get; set; }

        [Resolved(CanBeNull = true)]
        private GameplayClockContainer? gameplayClockContainer { get; set; }

        [Resolved(CanBeNull = true)]
        private IBindable<IReadOnlyList<Mod>>? selectedMods { get; set; }

        [Resolved(CanBeNull = true)]
        private ScoreProcessor? scoreProcessor { get; set; }

        public DrawableBmsRuleset(BmsRuleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
            : base(ruleset, beatmap, mods)
        {
            BmsBeatmapModApplicator.ApplyToBeatmap(beatmap, mods);
            Direction.Value = ScrollingDirection.Down;

            TimeRange.MinValue = MIN_TIME_RANGE;
            TimeRange.MaxValue = MAX_TIME_RANGE;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (KeyBindingInputManager is BmsInputManager inputManager)
                laneCoverFocusPressed.BindTo(inputManager.LaneCoverFocusPressed);

            laneCoverFocusPressed.BindValueChanged(e =>
            {
                if (e.NewValue)
                    CycleGameplayAdjustmentTarget();
            });

            playfieldScrollLengthRatio = Playfield.ScrollLengthRatio.GetBoundCopy();
            playfieldScrollLengthRatio.BindValueChanged(_ => updateTimeRange(), true);

            Config.BindWith(BmsRulesetSetting.HiSpeedMode, configHiSpeedMode);
            Config.BindWith(BmsRulesetSetting.ScrollSpeed, configNormalHiSpeed);
            Config.BindWith(BmsRulesetSetting.FloatingHiSpeed, configFloatingHiSpeed);
            Config.BindWith(BmsRulesetSetting.ClassicHiSpeed, configClassicHiSpeed);

            configHiSpeedMode.BindValueChanged(_ => refreshHiSpeedConfiguration(), true);
            configNormalHiSpeed.BindValueChanged(_ => refreshHiSpeedConfiguration());
            configFloatingHiSpeed.BindValueChanged(_ => refreshHiSpeedConfiguration());
            configClassicHiSpeed.BindValueChanged(_ => refreshHiSpeedConfiguration());

            Config.BindWith(BmsRulesetSetting.KeysoundConcurrentChannels, configKeysoundConcurrentChannels);
            configKeysoundConcurrentChannels.BindValueChanged(channels => Playfield.KeysoundStore.ConcurrentChannels = channels.NewValue, true);

            getSuddenMod()?.CoverPercent.BindValueChanged(_ => refreshSpeedMetrics(), true);
            getHiddenMod()?.CoverPercent.BindValueChanged(_ => refreshSpeedMetrics(), true);
            getLiftMod()?.LiftUnits.BindValueChanged(_ => refreshSpeedMetrics(), true);
        }

        public override PlayfieldAdjustmentContainer CreatePlayfieldAdjustmentContainer() => new BmsPlayfieldAdjustmentContainer();

        protected override Playfield CreatePlayfield() => new BmsPlayfield(Beatmap);

        public override DrawableHitObject<HitObject> CreateDrawableRepresentation(HitObject h)
            => h is BmsHoldNote holdNote ? new DrawableBmsHoldNote(holdNote) : new DrawableBmsHitObject(h);

        protected override ReplayInputHandler CreateReplayInputHandler(Replay replay) => new BmsFramedReplayInputHandler(replay);

        protected override ReplayRecorder CreateReplayRecorder(Score score) => new BmsReplayRecorder(score);

        protected override void LoadComplete()
        {
            base.LoadComplete();
            NewResult += HandleGameplayJudgementResult;

            if (scoreProcessor != null)
            {
                scoreProcessor.NewJudgement += handleScoreProcessorJudgementChanged;
                scoreProcessor.JudgementReverted += handleScoreProcessorJudgementChanged;

                totalScore = scoreProcessor.TotalScore.GetBoundCopy();
                totalScore.BindValueChanged(_ => refreshExScorePacemaker(), true);
            }
            else
                refreshExScorePacemaker();

            if (gameplayClockContainer != null)
                gameplayClockContainer.OnSeek += clearJudgementTimingFeedback;

            RefreshLaneCoverFocus();
            refreshSpeedMetrics();
            refreshTimingFeedbackVisualRange();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            NewResult -= HandleGameplayJudgementResult;

            if (scoreProcessor != null)
            {
                scoreProcessor.NewJudgement -= handleScoreProcessorJudgementChanged;
                scoreProcessor.JudgementReverted -= handleScoreProcessorJudgementChanged;
            }

            totalScore?.UnbindAll();

            if (gameplayClockContainer != null)
                gameplayClockContainer.OnSeek -= clearJudgementTimingFeedback;
        }

        public bool AdjustLaneCover(float scrollDelta, bool preferBottom = false)
        {
            if (!canAdjustGameplaySettings || scrollDelta == 0)
                return false;

            bool adjusted = adjustGameplayAdjustment(
                scrollDelta,
                preferBottom
                    ? getFirstEnabledGameplayAdjustmentTarget(BmsGameplayAdjustmentTarget.Hidden, BmsGameplayAdjustmentTarget.Sudden)
                    : getFirstEnabledGameplayAdjustmentTarget(BmsGameplayAdjustmentTarget.Sudden, BmsGameplayAdjustmentTarget.Hidden),
                refreshLaneCoverFocus: false);

            if (adjusted)
                UpdateLaneCoverFocus(preferBottom);

            return adjusted;
        }

        public bool AdjustGameplayAdjustment(float scrollDelta)
        {
            if (!canAdjustGameplaySettings || scrollDelta == 0)
                return false;

            return adjustGameplayAdjustment(scrollDelta, getPersistentGameplayAdjustmentTarget());
        }

        public bool CycleGameplayAdjustmentTarget()
        {
            if (!canAdjustGameplaySettings)
                return false;

            var enabledTargets = getEnabledGameplayAdjustmentTargets();

            if (enabledTargets.Count <= 1)
                return false;

            var currentTarget = getPersistentGameplayAdjustmentTarget(enabledTargets) ?? enabledTargets[0];
            int currentIndex = enabledTargets.IndexOf(currentTarget);

            currentGameplayAdjustmentTarget = enabledTargets[(currentIndex + 1) % enabledTargets.Count];
            RefreshLaneCoverFocus();
            showSpeedMetricsToast(currentGameplayAdjustmentTarget);
            return true;
        }

        public void RefreshLaneCoverFocus() => updateLaneCoverFocus(getDisplayedGameplayAdjustmentTarget());

        public void UpdateLaneCoverFocus(bool preferBottom)
            => updateLaneCoverFocus(preferBottom
                ? getFirstEnabledGameplayAdjustmentTarget(BmsGameplayAdjustmentTarget.Hidden, BmsGameplayAdjustmentTarget.Sudden)
                : getFirstEnabledGameplayAdjustmentTarget(BmsGameplayAdjustmentTarget.Sudden, BmsGameplayAdjustmentTarget.Hidden));

        private void updateLaneCoverFocus(BmsGameplayAdjustmentTarget? target)
        {
            var enabledTargets = getEnabledGameplayAdjustmentTargets();
            var persistentTarget = getPersistentGameplayAdjustmentTarget(enabledTargets);

            enabledAdjustmentTargetCount.Value = enabledTargets.Count;
            activeAdjustmentTargetIndex.Value = target.HasValue ? enabledTargets.IndexOf(target.Value) : -1;
            adjustmentTargetTemporarilyOverridden.Value = target != persistentTarget;

            BmsLaneCoverPosition? targetPosition = getLaneCoverTargetPosition(target);

            activeAdjustmentTarget.Value = target;

            foreach (var laneCover in Playfield.LaneCovers)
                laneCover.IsFocused.Value = targetPosition == laneCover.CoverPosition;

            refreshGameplayFeedbackState();
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            if (e.ControlPressed || e.AltPressed || e.ShiftPressed || e.SuperPressed)
                return base.OnScroll(e);

            if (adjustGameplayAdjustment((float)e.ScrollDelta.Y, getDisplayedGameplayAdjustmentTarget()))
                return true;

            return base.OnScroll(e);
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (!e.ControlPressed && !e.AltPressed && !e.ShiftPressed && !e.SuperPressed && e.Button == MouseButton.Middle && CycleGameplayAdjustmentTarget())
                return true;

            return base.OnMouseDown(e);
        }

        protected override void AdjustScrollSpeed(int amount)
        {
            AdjustSelectedHiSpeed(amount);
        }

        public static double ComputeScrollTime(double scrollSpeed) => MAX_TIME_RANGE / scrollSpeed;

        public void SetAllowAdjustmentWhilePaused(bool allow)
        {
            allowAdjustmentWhilePaused.Value = allow;
            RefreshLaneCoverFocus();
        }

        public bool AdjustSelectedHiSpeed(int amount)
        {
            if (amount == 0)
                return false;

            BindableDouble bindable = getSelectedHiSpeedBindable();
            double previous = bindable.Value;

            bindable.Value += amount * configHiSpeedMode.Value.GetAdjustmentStep();

            if (previous == bindable.Value)
                return false;

            showSpeedMetricsToast(getPersistentGameplayAdjustmentTarget());
            return true;
        }

        internal BmsScrollSpeedMetrics GetScrollSpeedMetrics()
            => BmsScrollSpeedMetrics.FromRuntime(
                configHiSpeedMode.Value,
                selectedHiSpeed.Value,
                playfieldScrollLengthRatio?.Value ?? 1,
                getModeTimeRangeScale(configHiSpeedMode.Value),
                getLaneCoverUnits(BmsLaneCoverPosition.Sudden),
                getLaneCoverUnits(BmsLaneCoverPosition.Hidden),
                Playfield.LiftUnits.Value);

        private void refreshHiSpeedConfiguration()
        {
            selectedHiSpeed.Value = getSelectedHiSpeedBindable().Value;
            updateTimeRange();
            refreshSpeedMetrics();
        }

        private void updateTimeRange() => TimeRange.Value = BmsHiSpeedRuntimeCalculator.ComputeBaseTimeRange(configHiSpeedMode.Value, selectedHiSpeed.Value, Beatmap.GetMostCommonBeatLength(), getInitialBeatLength(), Beatmap.Difficulty.SliderMultiplier) * (playfieldScrollLengthRatio?.Value ?? 1);

        private void refreshSpeedMetrics()
        {
            speedMetrics.Value = GetScrollSpeedMetrics();
            refreshGameplayFeedbackState();
        }

        private void refreshTimingFeedbackVisualRange()
        {
            timingFeedbackVisualRange.Value = FirstAvailableHitWindows.GetAllAvailableWindows()
                                                             .Where(window => window.result != HitResult.Miss && window.result != HitResult.None)
                                                             .Select(window => window.length)
                                                             .DefaultIfEmpty(1)
                                                             .Max();

            refreshGameplayFeedbackState();
        }

        private void refreshExScorePacemaker()
        {
            if (scoreProcessor is not BmsScoreProcessor bmsScoreProcessor)
            {
                exScorePacemakerInfo.Value = null;
                refreshGameplayFeedbackState();
                return;
            }

            exScorePacemakerInfo.Value = BmsExScorePacemakerInfo.Create(
                default_ex_score_pacemaker_target,
                bmsScoreProcessor.CurrentExScore,
                bmsScoreProcessor.JudgedHits,
                bmsScoreProcessor.MaximumExScore);

            refreshGameplayFeedbackState();
        }

        internal void HandleGameplayJudgementResult(JudgementResult judgementResult)
        {
            BmsJudgementTimingFeedback? feedback = BmsJudgementTimingFeedback.FromResult(judgementResult, ++judgementFeedbackOccurrenceId);

            if (feedback.HasValue)
            {
                latestJudgementFeedback.Value = feedback.Value;

                if (feedback.Value.ShowsTimingDirection)
                    pushRecentTimingFeedback(feedback.Value);

                refreshGameplayFeedbackState();
            }
        }

        private float getLaneCoverUnits(BmsLaneCoverPosition position)
            => Playfield.LaneCovers.FirstOrDefault(cover => cover.CoverPosition == position)?.CoverPercent.Value ?? 0;

        private void clearJudgementTimingFeedback()
        {
            latestJudgementFeedback.Value = null;
            recentJudgementFeedbacks.Clear();
            refreshGameplayFeedbackState();
        }

        private void pushRecentTimingFeedback(BmsJudgementTimingFeedback feedback)
        {
            if (recentJudgementFeedbacks.Count >= max_recent_timing_feedbacks)
                recentJudgementFeedbacks.RemoveAt(0);

            recentJudgementFeedbacks.Add(feedback);
        }

        private void refreshGameplayFeedbackState()
        {
            BmsExScoreProgressInfo? exScoreProgressInfo = scoreProcessor is BmsScoreProcessor bmsScoreProcessor
                ? BmsExScoreProgressInfo.Create(bmsScoreProcessor.CurrentExScore, bmsScoreProcessor.MaximumExScore)
                : null;

            gameplayFeedbackState.Value = new BmsGameplayFeedbackState(
                speedMetrics.Value,
                activeAdjustmentTarget.Value,
                enabledAdjustmentTargetCount.Value,
                activeAdjustmentTargetIndex.Value,
                adjustmentTargetTemporarilyOverridden.Value,
                latestJudgementFeedback.Value,
                BmsJudgementCounts.Create(scoreProcessor?.Statistics),
                exScoreProgressInfo,
                exScorePacemakerInfo.Value,
                timingFeedbackVisualRange.Value);
        }

        private void handleScoreProcessorJudgementChanged(JudgementResult _) => refreshExScorePacemaker();

        protected override PassThroughInputManager CreateInputManager() => new BmsInputManager(Ruleset.RulesetInfo, Variant);

        private bool adjustGameplayAdjustment(float scrollDelta, BmsGameplayAdjustmentTarget? target, bool refreshLaneCoverFocus = true)
        {
            Mod? adjustedMod = getModForAdjustmentTarget(target);

            bool adjusted = target switch
            {
                BmsGameplayAdjustmentTarget.Sudden => getSuddenMod()?.AdjustCoverPercent(scrollDelta) == true,
                BmsGameplayAdjustmentTarget.Hidden => getHiddenMod()?.AdjustCoverPercent(scrollDelta) == true,
                BmsGameplayAdjustmentTarget.Lift => getLiftMod()?.AdjustLiftUnits(scrollDelta) == true,
                _ => false,
            };

            if (!adjusted)
                return false;

            rememberGameplayAdjustment(adjustedMod);

            if (refreshLaneCoverFocus)
                RefreshLaneCoverFocus();

            showSpeedMetricsToast(target);
            return true;
        }

        private List<BmsGameplayAdjustmentTarget> getEnabledGameplayAdjustmentTargets()
        {
            var targets = new List<BmsGameplayAdjustmentTarget>(3);

            if (getSuddenMod() != null)
                targets.Add(BmsGameplayAdjustmentTarget.Sudden);

            if (getHiddenMod() != null)
                targets.Add(BmsGameplayAdjustmentTarget.Hidden);

            if (getLiftMod() != null)
                targets.Add(BmsGameplayAdjustmentTarget.Lift);

            return targets;
        }

        private BmsGameplayAdjustmentTarget? getPersistentGameplayAdjustmentTarget(IReadOnlyList<BmsGameplayAdjustmentTarget>? enabledTargets = null)
        {
            enabledTargets ??= getEnabledGameplayAdjustmentTargets();

            if (enabledTargets.Count == 0)
                return null;

            if (currentGameplayAdjustmentTarget == null || !enabledTargets.Contains(currentGameplayAdjustmentTarget.Value))
                currentGameplayAdjustmentTarget = enabledTargets[0];

            return currentGameplayAdjustmentTarget;
        }

        private BmsGameplayAdjustmentTarget? getFirstEnabledGameplayAdjustmentTarget(params BmsGameplayAdjustmentTarget[] orderedTargets)
        {
            foreach (var target in orderedTargets)
            {
                if (getModForAdjustmentTarget(target) != null)
                    return target;
            }

            return null;
        }

        private BmsGameplayAdjustmentTarget? getDisplayedGameplayAdjustmentTarget()
        {
            return getPersistentGameplayAdjustmentTarget();
        }

        private BmsModSudden? getSuddenMod() => Mods.OfType<BmsModSudden>().SingleOrDefault();

        private BmsModHidden? getHiddenMod() => Mods.OfType<BmsModHidden>().SingleOrDefault();

        private BmsModLift? getLiftMod() => Mods.OfType<BmsModLift>().SingleOrDefault();

        private void rememberGameplayAdjustment(Mod? adjustedMod)
        {
            if (adjustedMod is not IBmsGameplayAdjustmentMod gameplayAdjustmentMod || !gameplayAdjustmentMod.RememberGameplayChanges.Value)
                return;

            var selectedMod = selectedMods?.Value.SingleOrDefault(mod => mod.GetType() == adjustedMod.GetType());

            if (selectedMod == null || ReferenceEquals(selectedMod, adjustedMod))
                return;

            selectedMod.CopyFrom(adjustedMod);
        }

        private Mod? getModForAdjustmentTarget(BmsGameplayAdjustmentTarget? target)
            => target switch
            {
                BmsGameplayAdjustmentTarget.Sudden => getSuddenMod(),
                BmsGameplayAdjustmentTarget.Hidden => getHiddenMod(),
                BmsGameplayAdjustmentTarget.Lift => getLiftMod(),
                _ => null,
            };

        private static BmsLaneCoverPosition? getLaneCoverTargetPosition(BmsGameplayAdjustmentTarget? target)
            => target switch
            {
                BmsGameplayAdjustmentTarget.Sudden => BmsLaneCoverPosition.Sudden,
                BmsGameplayAdjustmentTarget.Hidden => BmsLaneCoverPosition.Hidden,
                _ => null,
            };

        private void showSpeedMetricsToast(BmsGameplayAdjustmentTarget? target = null)
            => bmsOnScreenDisplay?.Display(new BmsSpeedMetricsToast(GetScrollSpeedMetrics(), target ?? getPersistentGameplayAdjustmentTarget()));

        private bool canAdjustGameplaySettings => (!IsPaused.Value && (FrameStableClock?.IsRunning ?? true)) || allowAdjustmentWhilePaused.Value;

        private BindableDouble getSelectedHiSpeedBindable()
            => configHiSpeedMode.Value switch
            {
                BmsHiSpeedMode.Normal => configNormalHiSpeed,
                BmsHiSpeedMode.Floating => configFloatingHiSpeed,
                BmsHiSpeedMode.Classic => configClassicHiSpeed,
                _ => configNormalHiSpeed,
            };

        private double getModeTimeRangeScale(BmsHiSpeedMode mode)
            => mode switch
            {
                BmsHiSpeedMode.Normal => 1,
                BmsHiSpeedMode.Floating => Beatmap.GetMostCommonBeatLength() / getInitialBeatLength(),
                BmsHiSpeedMode.Classic => Beatmap.GetMostCommonBeatLength() / ((Beatmap.Difficulty.SliderMultiplier > 0 ? Beatmap.Difficulty.SliderMultiplier : 1) * TimingControlPoint.DEFAULT_BEAT_LENGTH),
                _ => 1,
            };

        private double getInitialBeatLength()
        {
            double referenceTime = Beatmap.HitObjects.Count > 0 ? Math.Max(0, Beatmap.HitObjects[0].StartTime) : 0;
            double beatLength = Beatmap.ControlPointInfo.TimingPointAt(referenceTime).BeatLength;
            return beatLength > 0 ? beatLength : TimingControlPoint.DEFAULT_BEAT_LENGTH;
        }

        private partial class BmsSpeedMetricsToast : Toast
        {
            public BmsSpeedMetricsToast(BmsScrollSpeedMetrics metrics, BmsGameplayAdjustmentTarget? target)
                : base(@"BMS speed", $@"GN {metrics.GreenNumber} ({metrics.VisibleLaneTime:0}ms)")
            {
                string targetText = target == null ? @"AUTO" : target.Value.GetAbbreviation();
                ExtraText = $@"Target {targetText} | {metrics.HiSpeedMode.GetShortLabel()} {metrics.HiSpeedMode.FormatValue(metrics.ScrollSpeed)} | WN {metrics.WhiteNumber} | HID {metrics.HiddenUnits} | LIFT {metrics.LiftUnits}";
            }
        }
    }
}
