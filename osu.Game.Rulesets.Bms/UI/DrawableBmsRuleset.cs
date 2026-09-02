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
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK.Input;

namespace osu.Game.Rulesets.Bms.UI
{
    [Cached]
    public partial class DrawableBmsRuleset : DrawableScrollingRuleset<HitObject>
    {
        public const double MIN_TIME_RANGE = 290;

        // Derived from the official-cab reference HS 10 + WN 350 => GN 300.
        public const double MAX_TIME_RANGE = 100000d / 13d;

        public new BmsPlayfield Playfield => (BmsPlayfield)base.Playfield;

        public BmsLongNoteMode LongNoteMode => BmsScoreProcessor.GetLongNoteMode(Mods);

        public BmsJudgeMode JudgeMode => BmsJudgeModeExtensions.GetJudgeMode(Mods);

        public override int Variant => Beatmap is BmsBeatmap bmsBeatmap
            ? BmsRuleset.GetLaneCount(bmsBeatmap.BmsInfo.Keymode)
            : throw new InvalidOperationException("BMS variant requires parser-owned keymode authority.");

        protected override bool RelativeScaleBeatLengths => true;

        protected new BmsRulesetConfigManager Config => (BmsRulesetConfigManager)base.Config;

        private readonly Bindable<BmsHiSpeedMode> configHiSpeedMode = new Bindable<BmsHiSpeedMode>();
        private readonly BindableDouble configNormalHiSpeed = new BindableDouble();
        private readonly BindableDouble configFloatingHiSpeed = new BindableDouble();
        private readonly BindableDouble configClassicHiSpeed = new BindableDouble();
        private readonly BindableDouble selectedHiSpeed = new BindableDouble();
        private IBindable<double>? playfieldScrollLengthRatio;
        private readonly BindableBool laneCoverFocusPressed = new BindableBool();
        private readonly Bindable<BmsScrollSpeedMetrics> speedMetrics = new Bindable<BmsScrollSpeedMetrics>();
        private readonly Bindable<BmsGameplayAdjustmentTarget?> activeAdjustmentTarget = new Bindable<BmsGameplayAdjustmentTarget?>();
        private readonly BindableInt enabledAdjustmentTargetCount = new BindableInt();
        private readonly Bindable<int> activeAdjustmentTargetIndex = new Bindable<int>();
        private readonly BindableBool adjustmentTargetTemporarilyOverridden = new BindableBool();
        private readonly BindableBool allowAdjustmentWhilePaused = new BindableBool();
        private BmsGameplayAdjustmentTarget? currentGameplayAdjustmentTarget;
        private BmsPreStartSpeedPreview? preStartSpeedPreview;
        private BmsBgaPanel? bgaPanel;
        private BmsHudLayoutPanel? hudLayoutPanel;
        private readonly BindableBool showBga = new BindableBool(true);

        public IBindable<BmsScrollSpeedMetrics> SpeedMetrics => speedMetrics;

        public IBindable<BmsHiSpeedMode> HiSpeedMode => configHiSpeedMode;

        public IBindable<double> SelectedHiSpeed => selectedHiSpeed;

        public IBindable<BmsGameplayAdjustmentTarget?> ActiveAdjustmentTarget => activeAdjustmentTarget;

        public IBindable<int> EnabledAdjustmentTargetCount => enabledAdjustmentTargetCount;

        public IBindable<int> ActiveAdjustmentTargetIndex => activeAdjustmentTargetIndex;

        public IBindable<bool> IsAdjustmentTargetTemporarilyOverridden => adjustmentTargetTemporarilyOverridden;

        public bool IsPreStartSpeedPreviewVisible => preStartSpeedPreview?.IsPreviewVisible == true;

        public bool IsPreStartSpeedPreviewPaused => preStartSpeedPreview?.IsPreviewPaused == true;

        public int? PreStartSpeedPreviewLaneIndex => preStartSpeedPreview?.LaneIndex;

        public float PreStartSpeedPreviewProgress => preStartSpeedPreview?.PrimaryNoteProgress ?? -1;

        internal ulong SpeedMetricsToastDisplayCount { get; private set; }

        public BmsInputManager? GameplayInputManager => KeyBindingInputManager as BmsInputManager;

        public BmsGameplayLayoutSnapshot LayoutSnapshot => LayoutProvider.Current;

        public GameplaySkinResolvedMaterialSet ResolvedMaterialSet => LayoutProvider.CurrentMaterialSet;

        internal BmsGameplayLayoutProvider LayoutProvider { get; }

        internal void InitialiseCompatibilityLayoutForTesting(
            BmsPlayfieldStyle style = BmsPlayfieldStyle.Center,
            ISkin? skin = null,
            BmsGameplayLayoutEnvironment? environment = null)
            => Playfield.InitialiseCompatibilityForTesting(style, skin, environment);

        internal BmsGameplayLayoutSnapshot? PreStartSpeedPreviewLayoutSnapshot => preStartSpeedPreview?.LayoutSnapshot;

        internal GameplaySkinResolvedMaterialSet? PreStartSpeedPreviewMaterialSet => preStartSpeedPreview?.ResolvedMaterialSet;

        internal float PreStartSpeedPreviewNoteScreenSpaceHeight => preStartSpeedPreview?.PrimaryNoteScreenSpaceHeight ?? 0;

        internal BmsGameplayLayoutSnapshot? BgaLayoutSnapshot => bgaPanel?.LayoutSnapshot;

        internal GameplaySkinResolvedMaterialSet? BgaMaterialSet => bgaPanel?.ResolvedMaterialSet;

        internal BmsGameplayLayoutSnapshot? HudLayoutSnapshot => hudLayoutPanel?.LayoutSnapshot;

        internal GameplaySkinResolvedMaterialSet? HudMaterialSet => hudLayoutPanel?.Carrier?.ResolvedMaterialSet;

        internal GameplaySkinResolvedMaterialSet? GaugeMaterialSet => (hudLayoutPanel?.Carrier?.GaugeBar as BmsGaugeBar)?.ResolvedMaterialSet;

        internal GameplaySkinResolvedMaterialSet? ComboMaterialSet => (hudLayoutPanel?.Carrier?.ComboCounter as BmsComboCounter)?.ResolvedMaterialSet;

        [Resolved(CanBeNull = true)]
        private OnScreenDisplay? bmsOnScreenDisplay { get; set; }

        [Resolved(CanBeNull = true)]
        private IBindable<IReadOnlyList<Mod>>? selectedMods { get; set; }

        public DrawableBmsRuleset(BmsRuleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
            : base(ruleset, beatmap, mods)
        {
            if (beatmap is not BmsBeatmap)
                throw new ArgumentException("Drawable BMS gameplay requires a converted BmsBeatmap.", nameof(beatmap));

            BmsBeatmapModApplicator.ApplyToBeatmap(beatmap, mods);
            LayoutProvider = Playfield.LayoutProvider;
            Direction.Value = ScrollingDirection.Down;

            TimeRange.MinValue = MIN_TIME_RANGE;
            TimeRange.MaxValue = MAX_TIME_RANGE;
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.Cache(LayoutProvider);

            if (parent.TryGet(out GameplaySkinLayoutRevisionOwner owner)
                && owner.CurrentPublication is GameplaySkinLayoutPublication publication)
            {
                LayoutProvider.AttachRevisionOwner(owner);
                BmsGameplayLayoutSnapshot adapter = publication.GetAdapter<BmsGameplayLayoutSnapshot>();

                if (!ReferenceEquals(adapter.Neutral, publication.Snapshot)
                    || !ReferenceEquals(publication.MaterialSet.Snapshot, publication.Snapshot)
                    || !ReferenceEquals(publication.MaterialSet.PackageRevision, owner.PackageRevision)
                    || !ReferenceEquals(publication.Snapshot.Context.PackageRevision, owner.PackageRevision)
                    || publication.Snapshot.Context.RulesetId != "bms")
                {
                    throw new InvalidOperationException("The BMS gameplay root does not retain its exact layout/material publication.");
                }

                dependencies.Cache(publication.MaterialSet);
            }

            return dependencies;
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

            // The keysound channel count is no longer user-configurable: the shared pool (created with its default
            // baseline) auto-grows on demand, so the playfield store is left at its constructed default here.
            getSuddenMod()?.CoverPercent.BindValueChanged(_ => refreshSpeedMetrics(), true);
            getHiddenMod()?.CoverPercent.BindValueChanged(_ => refreshSpeedMetrics(), true);
            getLiftMod()?.LiftUnits.BindValueChanged(_ => refreshSpeedMetrics(), true);

            setupBgaPanel();
            setupHudLayout();
        }

        private void setupHudLayout()
        {
            // Detached compatibility drawables do not own the Player health/score dependency graph. HUD layout is
            // exercised there through its explicit carrier fixture; the production root always has a non-compat exact
            // package and mounts the complete gauge/combo graph below that dependency scope.
            if (LayoutProvider.Current.Context.PackageRevision.SourceKind == GameplaySkinPackageSourceKind.Compatibility)
                return;

            hudLayoutPanel = new BmsHudLayoutPanel(LayoutProvider);
            Overlays.Add(hudLayoutPanel);
        }

        // Mounts the skinnable BGA panel above the playfield (in Overlays, so lanes never occlude it) and keeps its
        // default placement mirrored to the playfield style (P1-L Phase 5).
        private void setupBgaPanel()
        {
            if (Beatmap is not BmsBeatmap bmsBeatmap)
                return;

            bgaPanel = new BmsBgaPanel(bmsBeatmap.BgaTimeline, bmsBeatmap.PoorBgaMode, LayoutProvider);
            Overlays.Add(bgaPanel);

            // Transcode legacy BGA videos during loading so the BGA plays from the first frame (P1-L Phase 5.2 R1).
            // Mounted directly here (not inside the skinnable panel) so its blocking background load is part of the
            // tree the player push awaits; it self-gates (no-op without legacy video / with transcoding disabled).
            Overlays.Add(new BmsBgaVideoPreloader(bmsBeatmap.BgaTimeline));

            updateBgaPlacement();

            Config.BindWith(BmsRulesetSetting.ShowBga, showBga);
            showBga.BindValueChanged(visible => bgaPanel.Alpha = visible.NewValue ? 1 : 0, true);
        }

        private void updateBgaPlacement()
        {
            if (bgaPanel == null)
                return;

            bgaPanel.SetLayout(BmsBgaPanel.ResolveDefaultPlacement(Playfield.LayoutSnapshot.Keymode, Playfield.LayoutSnapshot.Style));
        }

        public override PlayfieldAdjustmentContainer CreatePlayfieldAdjustmentContainer() => new BmsPlayfieldAdjustmentContainer();

        protected override Playfield CreatePlayfield() => new BmsPlayfield(Beatmap);

        public override DrawableHitObject<HitObject> CreateDrawableRepresentation(HitObject h)
        {
            if (Mods.OfType<BmsModAutoplay>().Any() && h is BmsHitObject bmsHitObject)
                bmsHitObject.AutoPlay = true;

            return h is BmsHoldNote holdNote
                ? new DrawableBmsHoldNote(holdNote, Playfield.LayoutSnapshot, LayoutProvider.CurrentMaterialSet)
                : new DrawableBmsHitObject(h, Playfield.LayoutSnapshot, LayoutProvider.CurrentMaterialSet);
        }

        protected override ReplayInputHandler CreateReplayInputHandler(Replay replay)
            => Mods.OfType<BmsModAutoplay>().Any()
                ? new BmsAutoplayReplayInputHandler(replay)
                : new BmsFramedReplayInputHandler(replay);

        protected override ReplayRecorder CreateReplayRecorder(Score score) => new BmsReplayRecorder(score);

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Preload every distinct keysound at load (player mode included, not just autoplay): a fully-keysounded
            // chart otherwise cold-decodes hundreds of WAVs DURING gameplay, and on the converted-mania side the
            // resulting transient large buffers / promotion bursts were measured triggering blocking gen2 full GCs
            // (~220ms freezes) early in the play (P1-J, 2026-06-11). Preloading before gameplay matches LR2/beatoraja.
            Playfield.PrewarmKeysounds(getBeatmapKeysoundSamples());

            NewResult += HandleGameplayJudgementResult;

            RefreshLaneCoverFocus();
            refreshSpeedMetrics();
            initialisePreStartSpeedPreview();
        }

        private IEnumerable<BmsKeysoundSampleInfo> getBeatmapKeysoundSamples()
        {
            foreach (var hitObject in Beatmap.HitObjects)
            {
                switch (hitObject)
                {
                    case BmsHoldNote holdNote:
                        if (holdNote.HeadKeysoundSample != null)
                            yield return holdNote.HeadKeysoundSample;

                        if (holdNote.TailKeysoundSample != null)
                            yield return holdNote.TailKeysoundSample;

                        break;

                    case BmsHitObject { KeysoundSample: not null } bmsHitObject:
                        yield return bmsHitObject.KeysoundSample;
                        break;

                    case BmsBgmEvent { KeysoundSample: not null } bgmEvent:
                        yield return bgmEvent.KeysoundSample;
                        break;
                }
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            NewResult -= HandleGameplayJudgementResult;
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

        public void SetPreStartSpeedPreviewState(bool active, bool paused = false)
            => preStartSpeedPreview?.UpdateState(active, paused);

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

        public void DisplaySpeedMetricsToast(BmsGameplayAdjustmentTarget? target = null)
            => showSpeedMetricsToast(target);

        private void refreshHiSpeedConfiguration()
        {
            selectedHiSpeed.Value = getSelectedHiSpeedBindable().Value;
            updateTimeRange();
            refreshSpeedMetrics();
        }

        private void initialisePreStartSpeedPreview()
        {
            if (preStartSpeedPreview != null)
                return;

            BmsLane? previewLane = Playfield.Lanes.FirstOrDefault(lane => !lane.IsScratch);

            if (previewLane == null)
                return;

            previewLane.PreviewContainer.Add(preStartSpeedPreview = new BmsPreStartSpeedPreview(
                previewLane.LayoutSnapshotLane ?? throw new InvalidOperationException("Pre-start preview requires the exact gameplay layout lane."),
                Playfield.LayoutSnapshot.Keymode,
                SpeedMetrics,
                Playfield.LayoutSnapshot,
                LayoutProvider.CurrentMaterialSet));
        }

        private void updateTimeRange() => TimeRange.Value = BmsHiSpeedRuntimeCalculator.ComputeBaseTimeRange(configHiSpeedMode.Value, selectedHiSpeed.Value, Beatmap.GetMostCommonBeatLength(), getInitialBeatLength(), Beatmap.Difficulty.SliderMultiplier) * (playfieldScrollLengthRatio?.Value ?? 1);

        private void refreshSpeedMetrics()
        {
            speedMetrics.Value = GetScrollSpeedMetrics();
        }

        internal void HandleGameplayJudgementResult(JudgementResult judgementResult)
        {
            // A miss (POOR) flashes the BGA poor layer per the chart's #POORBGA mode.
            if (judgementResult.Type == HitResult.Miss)
                bgaPanel?.NotifyMiss();
        }

        private float getLaneCoverUnits(BmsLaneCoverPosition position)
            => Playfield.LaneCovers.FirstOrDefault(cover => cover.CoverPosition == position)?.CoverPercent.Value ?? 0;

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
        {
            SpeedMetricsToastDisplayCount++;
            bmsOnScreenDisplay?.Display(new BmsSpeedMetricsToast(GetScrollSpeedMetrics(), target ?? getPersistentGameplayAdjustmentTarget()));
        }

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
