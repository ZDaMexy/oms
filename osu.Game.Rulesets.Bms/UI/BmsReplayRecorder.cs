// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class BmsReplayRecorder : ReplayRecorder<BmsAction>
    {
        private readonly Score score;
        private readonly BmsVisualOffsetTimeline? visualOffset;
        private readonly BmsReadingSettingsSnapshot? readingSettings;
        private readonly BmsReadingSettingsTimeline readingSettingsTimeline = new BmsReadingSettingsTimeline();

        public BmsReplayRecorder(Score score)
            : this(score, null)
        {
        }

        public BmsReplayRecorder(Score score, BmsVisualOffsetTimeline? visualOffset, BmsReadingSettingsSnapshot? readingSettings = null, int readingCalculationVersion = 0)
            : base(score)
        {
            this.score = score;
            this.visualOffset = visualOffset;
            this.readingSettings = readingSettings;
            if (readingCalculationVersion is not (0 or BmsHiSpeedRuntimeCalculator.CURRENT_VERSION))
                throw new ArgumentOutOfRangeException(nameof(readingCalculationVersion));
            if (readingCalculationVersion != 0 && readingSettings?.NormalHiSpeedMultiplier == null)
                throw new ArgumentException("A native reading recording requires its resolved Normal Hi-Speed multiplier.", nameof(readingSettings));
            BmsScoreInfoData.InitialiseNewPlay(score.ScoreInfo, readingSettings, readingCalculationVersion);
        }

        public override void EndRecording()
        {
            var data = score.ScoreInfo.GetRulesetData<BmsScoreInfoData>()!;
            data.VisualOffset = visualOffset;
            data.ReadingSettings = readingSettings;
            data.ReadingSettingsTimeline = readingSettingsTimeline.Changes.Count > 0 ? readingSettingsTimeline : null;
            score.ScoreInfo.SetRulesetData(data);
            base.EndRecording();
        }

        internal void RecordReadingSettings(double time, BmsReadingSettingsSnapshot settings)
        {
            if (RecordingEnded)
                return;

            if (readingSettings is not BmsReadingSettingsSnapshot initial)
                throw new InvalidOperationException("A reading-settings recording requires its initial snapshot.");

            readingSettingsTimeline.Record(time, settings, initial);
            // Preserve this actual gameplay-clock boundary even when no lane input changed. The frame contains
            // only the existing lane/motion state; the reading operation itself remains ruleset metadata.
            RecordFrame(true);
        }

        internal void RecordAutomaticOffsetMode(double time, bool enabled)
        {
            if (RecordingEnded)
                return;
            if (visualOffset?.Automatic is not BmsVisualOffsetTimeline.AutomaticState automatic)
                throw new InvalidOperationException("Automatic offset recording requires its actual opening mode.");
            if (automatic.Record(time, enabled))
                RecordFrame(true);
        }

        protected override ReplayFrame HandleFrame(Vector2 mousePosition, List<BmsAction> actions, ReplayFrame previousFrame)
            => new BmsReplayFrame(Time.Current, actions);
    }
}
