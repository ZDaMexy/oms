// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Engine-owned provenance for a judgement which measured a player input, including recorded replay input.
    /// </summary>
    /// <remarks>
    /// This only distinguishes input timing from automatic completion. Consumers must still apply their
    /// ruleset's display qualification and exclude automated play; it does not change the judgement or its offset.
    /// </remarks>
    internal interface IGameplaySkinJudgementTimingMetadata
    {
        bool HasPlayerTiming { get; }
    }

    public enum GameplaySkinJudgementTimingDirection
    {
        Fast = -1,
        None = 0,
        Slow = 1,
    }

    /// <summary>
    /// An already-qualified timing hint at its real gameplay time.
    /// </summary>
    /// <remarks>
    /// Qualification, replacement and display lifetime belong to the producer. A hint never rewrites the
    /// source grade or offset, infers a direction from an ineligible result, or advances using wall-clock time.
    /// </remarks>
    public readonly struct GameplaySkinJudgementTimingHint
    {
        public GameplaySkinJudgementTimingDirection Direction { get; }

        public double AppliedTime { get; }

        internal GameplaySkinJudgementTimingHint(GameplaySkinJudgementTimingDirection direction, double appliedTime)
        {
            if (direction is not GameplaySkinJudgementTimingDirection.Fast and not GameplaySkinJudgementTimingDirection.Slow)
                throw new ArgumentOutOfRangeException(nameof(direction));
            if (!double.IsFinite(appliedTime))
                throw new ArgumentOutOfRangeException(nameof(appliedTime));

            Direction = direction;
            AppliedTime = appliedTime;
        }
    }

    /// <summary>
    /// The latest combined, key and scratch hints for one exact gameplay group.
    /// </summary>
    /// <remarks>
    /// Applying an eligible hint updates its semantic channel and the combined channel in delivery order.
    /// A default snapshot contains no hints and is used at an epoch reset.
    /// </remarks>
    public readonly struct GameplaySkinJudgementTimingStateSnapshot
    {
        public GameplaySkinJudgementTimingHint? Combined { get; }

        public GameplaySkinJudgementTimingHint? Keys { get; }

        public GameplaySkinJudgementTimingHint? Scratch { get; }

        private GameplaySkinJudgementTimingStateSnapshot(GameplaySkinJudgementTimingHint? combined,
                                                         GameplaySkinJudgementTimingHint? keys,
                                                         GameplaySkinJudgementTimingHint? scratch)
        {
            Combined = combined;
            Keys = keys;
            Scratch = scratch;
        }

        internal static GameplaySkinJudgementTimingStateSnapshot Restore(GameplaySkinJudgementTimingHint? combined,
                                                                         GameplaySkinJudgementTimingHint? keys,
                                                                         GameplaySkinJudgementTimingHint? scratch)
        {
            validateHint(combined);
            validateHint(keys);
            validateHint(scratch);
            return new GameplaySkinJudgementTimingStateSnapshot(combined, keys, scratch);
        }

        internal void ValidateForGameplayTime(double gameplayTime)
        {
            if (Combined?.AppliedTime > gameplayTime || Keys?.AppliedTime > gameplayTime || Scratch?.AppliedTime > gameplayTime)
                throw new ArgumentException("Judgement timing state cannot contain an application from the future.", nameof(gameplayTime));
        }

        private static void validateHint(GameplaySkinJudgementTimingHint? hint)
        {
            if (hint is { } value)
                _ = new GameplaySkinJudgementTimingHint(value.Direction, value.AppliedTime);
        }

        internal GameplaySkinJudgementTimingStateSnapshot Apply(GameplaySkinLaneRole role, GameplaySkinJudgementTimingHint hint)
            => role switch
            {
                GameplaySkinLaneRole.Key or GameplaySkinLaneRole.SpecialKey => new GameplaySkinJudgementTimingStateSnapshot(hint, hint, Scratch),
                GameplaySkinLaneRole.Scratch => new GameplaySkinJudgementTimingStateSnapshot(hint, Keys, hint),
                _ => throw new ArgumentOutOfRangeException(nameof(role)),
            };

    }

    /// <summary>Complete timing channels for one exact gameplay group, independent of the current grade display.</summary>
    public readonly struct GameplaySkinJudgementTimingGroupStateSnapshot
    {
        public GameplaySkinLaneGroupId GroupId { get; }

        public GameplaySkinJudgementTimingStateSnapshot State { get; }

        internal GameplaySkinJudgementTimingGroupStateSnapshot(GameplaySkinLaneGroupId groupId, GameplaySkinJudgementTimingStateSnapshot state)
        {
            ArgumentNullException.ThrowIfNull(groupId);
            GroupId = groupId;
            State = GameplaySkinJudgementTimingStateSnapshot.Restore(state.Combined, state.Keys, state.Scratch);
        }
    }
}
