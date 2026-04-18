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

        public BmsReplayFrame()
        {
        }

        public BmsReplayFrame(double time, params BmsAction[] actions)
            : base(time)
        {
            Actions.AddRange(actions);
        }

        public void FromLegacy(LegacyReplayFrame legacyFrame, IBeatmap beatmap, ReplayFrame? lastFrame = null)
        {
            var action = BmsAction.Scratch1;
            int activeActions = (int)(legacyFrame.MouseX ?? 0);

            while (activeActions > 0 && action <= BmsAction.Key14)
            {
                if ((activeActions & 1) > 0)
                    Actions.Add(action);

                action++;
                activeActions >>= 1;
            }
        }

        public LegacyReplayFrame ToLegacy(IBeatmap beatmap)
        {
            int keys = 0;

            foreach (var action in Actions.Where(action => action.IsLaneAction()))
                keys |= 1 << (int)action;

            return new LegacyReplayFrame(Time, keys, null, ReplayButtonState.None);
        }

        public override bool IsEquivalentTo(ReplayFrame other)
            => other is BmsReplayFrame bmsFrame && Time == bmsFrame.Time && Actions.SequenceEqual(bmsFrame.Actions);
    }
}
