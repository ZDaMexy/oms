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

        internal int LaneActionMask { get; private set; }
        private readonly List<BmsAction> inputActions = new List<BmsAction>();
        internal IReadOnlyList<BmsAction> InputActions => inputActions;
        internal int InputActionMask { get; private set; }

        // The legacy position field is a float. These reserved bits stay below 2^24,
        // so both motion levels and all sixteen attack bits round-trip exactly.
        private const int scratch_1_motion_bit = 1 << 20;
        private const int scratch_2_motion_bit = 1 << 21;

        public BmsReplayFrame()
        {
        }

        public BmsReplayFrame(double time, params BmsAction[] actions)
            : base(time)
        {
            foreach (var action in actions)
                addAction(action);
        }

        internal BmsReplayFrame(double time, IReadOnlyList<BmsAction> liveActions)
            : base(time)
        {
            for (int i = 0; i < liveActions.Count; i++)
            {
                if (liveActions[i].IsGameplayInputAction())
                    addAction(liveActions[i]);
            }
        }

        public void FromLegacy(LegacyReplayFrame legacyFrame, IBeatmap beatmap, ReplayFrame? lastFrame = null)
        {
            Actions.Clear();
            LaneActionMask = 0;
            inputActions.Clear();
            InputActionMask = 0;

            var action = BmsAction.Scratch1;
            int activeActions = (int)(legacyFrame.MouseX ?? 0);

            while (activeActions > 0 && action <= BmsAction.Key14)
            {
                if ((activeActions & 1) > 0)
                    addAction(action);

                action++;
                activeActions >>= 1;
            }

            int mask = (int)(legacyFrame.MouseX ?? 0);
            if ((mask & scratch_1_motion_bit) != 0)
                addAction(BmsAction.Scratch1Motion);
            if ((mask & scratch_2_motion_bit) != 0)
                addAction(BmsAction.Scratch2Motion);
        }

        public LegacyReplayFrame ToLegacy(IBeatmap beatmap)
        {
            return new LegacyReplayFrame(Time, InputActionMask, null, ReplayButtonState.None);
        }

        public override bool IsEquivalentTo(ReplayFrame other)
            => other is BmsReplayFrame bmsFrame
               && Time == bmsFrame.Time
               && LaneActionMask == bmsFrame.LaneActionMask
               && Actions.SequenceEqual(bmsFrame.Actions);

        private void addAction(BmsAction action)
        {
            Actions.Add(action);

            if (action is BmsAction.Scratch1Motion or BmsAction.Scratch2Motion)
            {
                inputActions.Add(action);
                InputActionMask |= action == BmsAction.Scratch1Motion ? scratch_1_motion_bit : scratch_2_motion_bit;
                return;
            }

            if (!action.IsLaneAction())
                return;

            LaneActionMask |= 1 << (int)action;
            inputActions.Add(action);
            InputActionMask |= 1 << (int)action;
        }
    }
}
