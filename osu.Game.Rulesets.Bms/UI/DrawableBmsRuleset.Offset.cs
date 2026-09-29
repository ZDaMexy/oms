// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Configuration;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class DrawableBmsRuleset
    {
        private readonly Bindable<AutomaticOffsetStyle> automaticOffsetStyle = new Bindable<AutomaticOffsetStyle>();
        private readonly BindableDouble configuredVisualOffset = new BindableDouble();
        private BmsVisualOffsetTimeline? recordedVisualOffset;
        private BmsVisualOffsetTimeline? replayVisualOffset;

        private void initialiseOffset(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.AutomaticOffsetStyle, automaticOffsetStyle);
            config.BindWith(OsuSetting.BmsVisualOffset, configuredVisualOffset);
            automaticOffsetStyle.BindValueChanged(_ => refreshVisualOffsetMode());
            configuredVisualOffset.BindValueChanged(_ => refreshVisualOffsetMode());
            refreshVisualOffsetMode();
            FrameStableComponents.Add(new ReplayVisualOffsetUpdater(this));
        }

        private void refreshVisualOffsetMode()
        {
            Playfield.AutomaticVisualOffsetEnabled.Value = HasReplayLoaded.Value
                ? replayVisualOffset?.Changes.Count > 0
                : automaticOffsetStyle.Value == AutomaticOffsetStyle.Beatoraja;
            double offset = HasReplayLoaded.Value
                ? replayVisualOffset?.OffsetAt(FrameStableClock.CurrentTime) ?? 0
                : configuredVisualOffset.Value;

            if (Playfield.VisualOffset.Value == offset)
                return;

            Playfield.VisualOffset.Value = offset;
            if (!HasReplayLoaded.Value)
                recordedVisualOffset?.Record(FrameStableClock.CurrentTime, offset);
        }

        private void adjustVisualOffsetFromResult(JudgementResult result)
        {
            // Long notes supply their semantic samples from their owner, not from nested score/tick events.
            if (result.HitObject is BmsHitObject { AutoPlay: false } and not BmsHoldNote and not BmsHoldNoteHead and not BmsHoldNoteTailEvent)
                ApplyAutomaticOffsetSample(result.Type, result.TimeOffset);
        }

        internal void ApplyAutomaticOffsetSample(HitResult result, double error)
        {
            if (HasReplayLoaded.Value || IsPaused.Value || automaticOffsetStyle.Value != AutomaticOffsetStyle.Beatoraja)
                return;

            configuredVisualOffset.Value = AdjustVisualOffset(configuredVisualOffset.Value, result, error);
        }

        internal static double AdjustVisualOffset(double current, HitResult result, double error)
        {
            if (result is not (HitResult.Perfect or HitResult.Great or HitResult.Good) || Math.Abs(error) > 150)
                return current;

            // OMS errors are late-positive (the opposite of beatoraja's mfast).
            return Math.Clamp(current + Math.Sign(error) * Math.Floor((Math.Abs(error) + 15) / 30), -500, 500);
        }

        private partial class ReplayVisualOffsetUpdater : Component
        {
            private readonly DrawableBmsRuleset ruleset;

            public ReplayVisualOffsetUpdater(DrawableBmsRuleset ruleset)
            {
                this.ruleset = ruleset;
            }

            protected override void Update()
            {
                base.Update();
                if (ruleset.HasReplayLoaded.Value)
                    ruleset.Playfield.VisualOffset.Value = ruleset.replayVisualOffset?.OffsetAt(Time.Current) ?? 0;
            }
        }
    }
}
