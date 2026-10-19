// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.StateChanges;
using osu.Game.Replays;
using osu.Game.Rulesets.Replays;

namespace osu.Game.Rulesets.Mania.Replays
{
    internal class ManiaFramedReplayInputHandler : FramedReplayInputHandler<ManiaReplayFrame>
    {
        private static readonly List<ManiaAction> empty_actions = new List<ManiaAction>();
        private ManiaReplayFrame? stateFrame;
        private ReplayState<ManiaAction>? state;

        public ManiaFramedReplayInputHandler(Replay replay)
            : base(replay)
        {
        }

        protected override bool IsImportant(ManiaReplayFrame frame) => frame.Actions.Any();

        protected override void CollectReplayInputs(List<IInput> inputs)
        {
            ManiaReplayFrame? frame = CurrentFrame;
            if (state == null || !ReferenceEquals(stateFrame, frame))
            {
                stateFrame = frame;
                state = new ReplayState<ManiaAction> { PressedActions = frame?.Actions ?? empty_actions };
            }

            inputs.Add(state);
        }
    }
}
