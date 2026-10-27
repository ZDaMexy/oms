// Copyright (c) OMS contributors. Licensed under the MIT Licence.

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

        public BmsReplayRecorder(Score score)
            : this(score, null)
        {
        }

        public BmsReplayRecorder(Score score, BmsVisualOffsetTimeline? visualOffset, BmsReadingSettingsSnapshot? readingSettings = null)
            : base(score)
        {
            this.score = score;
            this.visualOffset = visualOffset;
            this.readingSettings = readingSettings;
            BmsScoreInfoData.InitialiseNewPlay(score.ScoreInfo, readingSettings);
        }

        public override void EndRecording()
        {
            var data = score.ScoreInfo.GetRulesetData<BmsScoreInfoData>()!;
            data.VisualOffset = visualOffset;
            data.ReadingSettings = readingSettings;
            score.ScoreInfo.SetRulesetData(data);
            base.EndRecording();
        }

        protected override ReplayFrame HandleFrame(Vector2 mousePosition, List<BmsAction> actions, ReplayFrame previousFrame)
            => new BmsReplayFrame(Time.Current, actions);
    }
}
