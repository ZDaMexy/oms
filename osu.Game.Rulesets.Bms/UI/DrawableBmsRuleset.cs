// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.UI.Scrolling;

namespace osu.Game.Rulesets.Bms.UI
{
    [Cached]
    public partial class DrawableBmsRuleset : DrawableScrollingRuleset<HitObject>
    {
        public const double MIN_TIME_RANGE = 290;

        public const double MAX_TIME_RANGE = 11485;

        public new BmsPlayfield Playfield => (BmsPlayfield)base.Playfield;

        public BmsLongNoteMode LongNoteMode => BmsScoreProcessor.GetLongNoteMode(Mods);

        public BmsJudgeMode JudgeMode => BmsJudgeModeExtensions.GetJudgeMode(Mods);

        public override int Variant => BmsLaneLayout.CreateFor(Beatmap).Lanes.Count;

        protected override bool RelativeScaleBeatLengths => true;

        protected new BmsRulesetConfigManager Config => (BmsRulesetConfigManager)base.Config;

        private readonly Bindable<ScrollingDirection> configDirection = new Bindable<ScrollingDirection>();
        private readonly BindableDouble configScrollSpeed = new BindableDouble();
        private IBindable<double>? playfieldScrollLengthRatio;
        private readonly BindableInt configKeysoundConcurrentChannels = new BindableInt
        {
            Default = BmsKeysoundStore.DEFAULT_CONCURRENT_CHANNELS,
            MinValue = BmsKeysoundStore.MIN_CONCURRENT_CHANNELS,
            MaxValue = BmsKeysoundStore.MAX_CONCURRENT_CHANNELS,
            Precision = 1,
        };
        private readonly BindableBool laneCoverFocusPressed = new BindableBool();

        public DrawableBmsRuleset(BmsRuleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
            : base(ruleset, beatmap, mods)
        {
            LongNoteMode.ApplyToBeatmap(beatmap);
            JudgeMode.ApplyToBeatmap(beatmap);

            TimeRange.MinValue = MIN_TIME_RANGE;
            TimeRange.MaxValue = MAX_TIME_RANGE;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (KeyBindingInputManager is BmsInputManager inputManager)
                laneCoverFocusPressed.BindTo(inputManager.LaneCoverFocusPressed);

            laneCoverFocusPressed.BindValueChanged(_ => RefreshLaneCoverFocus(), true);

            Config.BindWith(BmsRulesetSetting.ScrollDirection, configDirection);
            configDirection.BindValueChanged(direction => Direction.Value = direction.NewValue, true);

            playfieldScrollLengthRatio = Playfield.ScrollLengthRatio.GetBoundCopy();
            playfieldScrollLengthRatio.BindValueChanged(_ => updateTimeRange(), true);

            Config.BindWith(BmsRulesetSetting.ScrollSpeed, configScrollSpeed);
            configScrollSpeed.BindValueChanged(_ => updateTimeRange(), true);

            Config.BindWith(BmsRulesetSetting.KeysoundConcurrentChannels, configKeysoundConcurrentChannels);
            configKeysoundConcurrentChannels.BindValueChanged(channels => Playfield.KeysoundStore.ConcurrentChannels = channels.NewValue, true);
        }

        public override PlayfieldAdjustmentContainer CreatePlayfieldAdjustmentContainer() => new BmsPlayfieldAdjustmentContainer();

        protected override Playfield CreatePlayfield() => new BmsPlayfield(Beatmap);

        public override DrawableHitObject<HitObject> CreateDrawableRepresentation(HitObject h)
            => h is BmsHoldNote holdNote ? new DrawableBmsHoldNote(holdNote) : new DrawableBmsHitObject(h);

        protected override void LoadComplete()
        {
            base.LoadComplete();
            RefreshLaneCoverFocus();
        }

        public bool AdjustLaneCover(float scrollDelta, bool preferBottom = false)
        {
            if (IsPaused.Value || scrollDelta == 0)
                return false;

            UpdateLaneCoverFocus(preferBottom);

            BmsModLaneCover? target = preferBottom
                ? Mods.OfType<BmsModLaneCoverBottom>().SingleOrDefault() ?? (BmsModLaneCover?)Mods.OfType<BmsModLaneCoverTop>().SingleOrDefault()
                : Mods.OfType<BmsModLaneCoverTop>().SingleOrDefault() ?? (BmsModLaneCover?)Mods.OfType<BmsModLaneCoverBottom>().SingleOrDefault();

            return target?.AdjustCoverPercent(scrollDelta) == true;
        }

        public void RefreshLaneCoverFocus() => UpdateLaneCoverFocus(laneCoverFocusPressed.Value);

        public void UpdateLaneCoverFocus(bool preferBottom)
        {
            BmsLaneCoverPosition? targetPosition = getLaneCoverTargetPosition(preferBottom);

            foreach (var laneCover in Playfield.LaneCovers)
                laneCover.IsFocused.Value = targetPosition == laneCover.CoverPosition;
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            if (e.ControlPressed || e.AltPressed || e.ShiftPressed || e.SuperPressed)
                return base.OnScroll(e);

            if (AdjustLaneCover((float)e.ScrollDelta.Y, laneCoverFocusPressed.Value))
                return true;

            return base.OnScroll(e);
        }

        protected override void AdjustScrollSpeed(int amount) => configScrollSpeed.Value += amount;

        public static double ComputeScrollTime(double scrollSpeed) => MAX_TIME_RANGE / scrollSpeed;

        private void updateTimeRange() => TimeRange.Value = ComputeScrollTime(configScrollSpeed.Value) * (playfieldScrollLengthRatio?.Value ?? 1);

        protected override PassThroughInputManager CreateInputManager() => new BmsInputManager(Ruleset.RulesetInfo, Variant);

        private BmsLaneCoverPosition? getLaneCoverTargetPosition(bool preferBottom)
        {
            bool hasTopCover = Playfield.LaneCovers.Any(cover => cover.CoverPosition == BmsLaneCoverPosition.Top);
            bool hasBottomCover = Playfield.LaneCovers.Any(cover => cover.CoverPosition == BmsLaneCoverPosition.Bottom);

            if (preferBottom)
            {
                if (hasBottomCover)
                    return BmsLaneCoverPosition.Bottom;

                if (hasTopCover)
                    return BmsLaneCoverPosition.Top;
            }
            else
            {
                if (hasTopCover)
                    return BmsLaneCoverPosition.Top;

                if (hasBottomCover)
                    return BmsLaneCoverPosition.Bottom;
            }

            return null;
        }
    }
}
