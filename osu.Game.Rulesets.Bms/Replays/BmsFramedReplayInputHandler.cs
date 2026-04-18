// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.StateChanges;
using osu.Game.Replays;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Replays;

namespace osu.Game.Rulesets.Bms.Replays
{
    internal class BmsFramedReplayInputHandler : FramedReplayInputHandler<BmsReplayFrame>
    {
        public BmsFramedReplayInputHandler(Replay replay)
            : base(replay)
        {
        }

        protected override bool IsImportant(BmsReplayFrame frame) => frame.Actions.Any(action => action.IsLaneAction());

        protected override void CollectReplayInputs(List<IInput> inputs)
            => inputs.Add(new ReplayState<BmsAction>
            {
                PressedActions = CurrentFrame?.Actions.Where(action => action.IsLaneAction()).ToList() ?? new List<BmsAction>()
            });
    }
}
