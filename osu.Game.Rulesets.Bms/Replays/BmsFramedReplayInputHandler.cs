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
        private static readonly List<BmsAction> empty_actions = new List<BmsAction>();

        public BmsFramedReplayInputHandler(Replay replay)
            : base(replay)
        {
        }

        protected override bool IsImportant(BmsReplayFrame frame) => frame.LaneActionMask != 0;

        protected override void CollectReplayInputs(List<IInput> inputs)
            => inputs.Add(new ReplayState<BmsAction>
            {
                PressedActions = (List<BmsAction>?)CurrentFrame?.LaneActions ?? empty_actions
            });
    }
}
