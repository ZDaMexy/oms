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

        private List<ReplayFrame> Frames => replay.Frames;

        private int currentFrameIndex = -1;

        private BmsReplayFrame? CurrentFrame => currentFrameIndex < 0 || currentFrameIndex >= Frames.Count ? null : (BmsReplayFrame)Frames[currentFrameIndex];

        public override bool IsActive => Frames.Count != 0;

        public BmsAutoplayReplayInputHandler(Replay replay)
        {
            replay.Frames = replay.Frames.OrderBy(frame => frame.Time).ToList();
            this.replay = replay;
        }

        public override double? SetFrameFromTime(double time)
        {
            if (Frames.Count == 0)
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

            inputs.Add(new ReplayState<BmsAction>
            {
                PressedActions = (List<BmsAction>?)CurrentFrame?.LaneActions ?? empty_actions
            });

            if (CurrentFrame?.Header != null)
                inputs.Add(new ReplayStatisticsFrameInput { Frame = CurrentFrame });
        }

        private int findCurrentFrameIndex(double time)
        {
            if (time < Frames[0].Time)
                return -1;

            int low = 0;
            int high = Frames.Count - 1;

            while (low <= high)
            {
                int mid = low + (high - low) / 2;

                if (Frames[mid].Time <= time)
                    low = mid + 1;
                else
                    high = mid - 1;
            }

            return high;
        }
    }
}
