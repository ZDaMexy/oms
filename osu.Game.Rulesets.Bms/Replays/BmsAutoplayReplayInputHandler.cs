// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.StateChanges;
using osu.Game.Input.Handlers;
using osu.Game.Replays;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Replays;

namespace osu.Game.Rulesets.Bms.Replays
{
    internal class BmsAutoplayReplayInputHandler : ReplayInputHandler
    {
        private static readonly List<BmsAction> empty_actions = new List<BmsAction>();

        private readonly Replay replay;

        private List<ReplayFrame> frames => replay.Frames;

        private int currentFrameIndex = -1;
        private BmsReplayFrame? stateFrame;
        private ReplayState<BmsAction>? state;

        private BmsReplayFrame? currentFrame => currentFrameIndex < 0 || currentFrameIndex >= frames.Count ? null : (BmsReplayFrame)frames[currentFrameIndex];

        public override bool IsActive => frames.Count != 0;

        public BmsAutoplayReplayInputHandler(Replay replay)
        {
            replay.Frames = replay.Frames.OrderBy(frame => frame.Time).ToList();
            this.replay = replay;
        }

        public override double? SetFrameFromTime(double time)
        {
            if (frames.Count == 0)
            {
                if (replay.HasReceivedAllFrames)
                    return time;

                return null;
            }

            currentFrameIndex = findCurrentFrameIndex(time);
            return time;
        }

        public override void CollectPendingInputs(List<IInput> inputs)
        {
            base.CollectPendingInputs(inputs);

            BmsReplayFrame? frame = currentFrame;
            if (state == null || !ReferenceEquals(stateFrame, frame))
            {
                stateFrame = frame;
                state = new ReplayState<BmsAction> { PressedActions = (List<BmsAction>?)frame?.InputActions ?? empty_actions };
            }

            inputs.Add(state);

            if (frame?.Header != null)
                inputs.Add(new ReplayStatisticsFrameInput { Frame = frame });
        }

        private int findCurrentFrameIndex(double time)
        {
            if (time < frames[0].Time)
                return -1;

            int low = 0;
            int high = frames.Count - 1;

            while (low <= high)
            {
                int mid = low + (high - low) / 2;

                if (frames[mid].Time <= time)
                    low = mid + 1;
                else
                    high = mid - 1;
            }

            return high;
        }
    }
}
