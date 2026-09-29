// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
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

        public BmsReplayRecorder(Score score)
            : this(score, null)
        {
        }

        public BmsReplayRecorder(Score score, BmsVisualOffsetTimeline? visualOffset)
            : base(score)
        {
            this.score = score;
            this.visualOffset = visualOffset;
            BmsScoreInfoData.InitialiseNewPlay(score.ScoreInfo);
        }

        public override void EndRecording()
        {
            var data = score.ScoreInfo.GetRulesetData<BmsScoreInfoData>()!;
            data.VisualOffset = visualOffset;
            score.ScoreInfo.SetRulesetData(data);
        }

        protected override ReplayFrame HandleFrame(Vector2 mousePosition, List<BmsAction> actions, ReplayFrame previousFrame)
            => new BmsReplayFrame(Time.Current, actions.Where(action => action.IsLaneAction()).ToArray());
    }
}
