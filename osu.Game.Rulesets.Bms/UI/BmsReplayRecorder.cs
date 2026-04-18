// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class BmsReplayRecorder : ReplayRecorder<BmsAction>
    {
        public BmsReplayRecorder(Score score)
            : base(score)
        {
        }

        protected override ReplayFrame HandleFrame(Vector2 mousePosition, List<BmsAction> actions, ReplayFrame previousFrame)
            => new BmsReplayFrame(Time.Current, actions.Where(action => action.IsLaneAction()).ToArray());
    }
}
