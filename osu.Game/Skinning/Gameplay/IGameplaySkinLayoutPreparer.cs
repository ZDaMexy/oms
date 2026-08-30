// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Game.Beatmaps;

namespace osu.Game.Skinning.Gameplay
{
    public enum GameplaySkinLayoutPreparationResult
    {
        Prepared,
        Retry,
        Rejected,
    }

    /// <summary>
    /// Signals that a fully solved candidate lost only its participant-generation admission and may be recomputed
    /// against a fresh work lease. No publication reference has changed when this exception is raised.
    /// </summary>
    public sealed class GameplaySkinLayoutParticipantBarrierChangedException : InvalidOperationException
    {
        internal GameplaySkinLayoutParticipantBarrierChangedException()
            : base("The gameplay layout participant barrier changed during background preparation.")
        {
        }
    }

    /// <summary>
    /// A ruleset root which prepares its one gameplay layout after the final ruleset/beatmap skin source exists, but
    /// before any production renderer child is allowed to load.
    /// </summary>
    /// <remarks>
    /// The hook runs from the enclosing beatmap skin provider's background loader. Implementations may perform only
    /// preparation here and must publish through the exact <see cref="GameplaySkinLayoutRevisionOwner"/> supplied by
    /// the dependency scope; its commit dispatcher is the sole update-thread publication boundary.
    /// </remarks>
    public interface IGameplaySkinLayoutPreparer
    {
        GameplaySkinLayoutPreparationResult PrepareGameplaySkinLayout(IBeatmap beatmap, IReadOnlyDependencyContainer dependencies);
    }
}
