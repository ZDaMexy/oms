// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;

namespace osu.Game.Skinning.Gameplay
{
    public partial class GameplaySkinSceneRuntimeHost
    {
        private readonly Dictionary<GameplaySkinLaneGroupId, GameplaySkinJudgementTimingStateSnapshot> judgementTimingGroups = [];

        private string judgementTimingDirection(GameplaySkinSceneBindingSource source, GameplaySkinSceneRuntimeNode node)
        {
            // Timing belongs to an exact gameplay group. A global or unbound visual must not borrow the
            // first player's hint, and a second player must not observe the other group's scratch input.
            GameplaySkinLaneGroupId? group = node.PreparedNode.MaterialTarget?.GroupId;
            if (group == null || !judgementTimingGroups.TryGetValue(group, out GameplaySkinJudgementTimingStateSnapshot state))
                return "none";

            GameplaySkinJudgementTimingHint? hint = source switch
            {
                GameplaySkinSceneBindingSource.JudgementTimingDirection => state.Combined,
                GameplaySkinSceneBindingSource.JudgementKeysTimingDirection => state.Keys,
                GameplaySkinSceneBindingSource.JudgementScratchTimingDirection => state.Scratch,
                _ => throw new ArgumentOutOfRangeException(nameof(source)),
            };

            // The consumer displays the authoritative latest eligible input rather than applying the
            // transient grade's expiry to it. Replacement/reset policy belongs to the producer.
            if (hint is not { } timing)
                return "none";

            return timing.Direction == GameplaySkinJudgementTimingDirection.Fast ? "fast" : "slow";
        }
    }
}
