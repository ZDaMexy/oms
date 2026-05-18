// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Replays.Legacy;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Replays.Types;

namespace osu.Game.Rulesets.Bms.Replays
{
    public class BmsReplayFrame : ReplayFrame, IConvertibleReplayFrame
    {
        public List<BmsAction> Actions = new List<BmsAction>();

        private readonly List<BmsAction> laneActions = new List<BmsAction>();
        internal IReadOnlyList<BmsAction> LaneActions => laneActions;
        internal int LaneActionMask { get; private set; }

        public BmsReplayFrame()
        {
        }

        public BmsReplayFrame(double time, params BmsAction[] actions)
            : base(time)
        {
            foreach (var action in actions)
                addAction(action);
        }

        public void FromLegacy(LegacyReplayFrame legacyFrame, IBeatmap beatmap, ReplayFrame? lastFrame = null)
        {
            Actions.Clear();
            laneActions.Clear();
            LaneActionMask = 0;

            var action = BmsAction.Scratch1;
            int activeActions = (int)(legacyFrame.MouseX ?? 0);

            while (activeActions > 0 && action <= BmsAction.Key14)
            {
                if ((activeActions & 1) > 0)
                    addAction(action);

                action++;
                activeActions >>= 1;
            }
        }

        public LegacyReplayFrame ToLegacy(IBeatmap beatmap)
        {
            return new LegacyReplayFrame(Time, LaneActionMask, null, ReplayButtonState.None);
        }

        public override bool IsEquivalentTo(ReplayFrame other)
            => other is BmsReplayFrame bmsFrame
               && Time == bmsFrame.Time
               && LaneActionMask == bmsFrame.LaneActionMask
               && Actions.SequenceEqual(bmsFrame.Actions);

        private void addAction(BmsAction action)
        {
            Actions.Add(action);

            if (!action.IsLaneAction())
                return;

            laneActions.Add(action);
            LaneActionMask |= 1 << (int)action;
        }
    }
}
