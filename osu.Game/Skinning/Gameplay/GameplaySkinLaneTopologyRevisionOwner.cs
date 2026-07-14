// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Owns the monotonic publication revision for one native gameplay lane topology attachment.
    /// </summary>
    /// <remarks>
    /// Ruleset adapters must provide an immutable, non-sensitive native context and an exact equality predicate. A successful
    /// first publication receives revision zero; every later successful topology-preserving publication increments by one.
    /// Native-context mismatch, comparison failure, neutral topology rejection and revision overflow leave the current
    /// publication unchanged. This process-local bridge is not thread-safe, a security boundary, a manifest or a wire ABI.
    /// </remarks>
    public sealed class GameplaySkinLaneTopologyRevisionOwner<TNativeContext>
        where TNativeContext : notnull
    {
        private readonly Func<TNativeContext, TNativeContext, bool> nativeContextEquals;
        private TNativeContext currentNativeContext = default!;
        private bool hasCurrent;

        public GameplaySkinLaneTopologyPublication? Current { get; private set; }

        public GameplaySkinLaneTopologyRevisionOwner(Func<TNativeContext, TNativeContext, bool> nativeContextEquals)
        {
            ArgumentNullException.ThrowIfNull(nativeContextEquals);

            this.nativeContextEquals = nativeContextEquals;
        }

        internal GameplaySkinLaneTopologyRevisionOwner(
            Func<TNativeContext, TNativeContext, bool> nativeContextEquals,
            TNativeContext currentNativeContext,
            GameplaySkinLaneTopologyPublication current)
            : this(nativeContextEquals)
        {
            ArgumentNullException.ThrowIfNull(currentNativeContext);
            ArgumentNullException.ThrowIfNull(current);

            this.currentNativeContext = currentNativeContext;
            Current = current;
            hasCurrent = true;
        }

        /// <summary>
        /// Validates and publishes the next topology for this native attachment.
        /// </summary>
        /// <exception cref="ArgumentNullException">The context or topology is null.</exception>
        /// <exception cref="ArgumentException">The native context or neutral topology changed.</exception>
        /// <exception cref="OverflowException">The current revision cannot be incremented.</exception>
        public GameplaySkinLaneTopologyPublication Publish(
            TNativeContext nativeContext,
            GameplaySkinLaneTopologySnapshot topology)
        {
            ArgumentNullException.ThrowIfNull(nativeContext);
            ArgumentNullException.ThrowIfNull(topology);

            if (!hasCurrent)
            {
                GameplaySkinLaneTopologyPublication initial = GameplaySkinLaneTopologyPublication.Create(0, topology);

                currentNativeContext = nativeContext;
                Current = initial;
                hasCurrent = true;

                return initial;
            }

            GameplaySkinLaneTopologyPublication previous = Current!;

            if (!nativeContextEquals(currentNativeContext, nativeContext))
                throw new ArgumentException("A gameplay skin lane topology publication cannot change native context within one owner.", nameof(nativeContext));

            GameplaySkinLaneTopologyTransitionValidator.Validate(previous.Topology, topology);

            long nextRevision = checked(previous.Revision + 1);
            GameplaySkinLaneTopologyPublication next = GameplaySkinLaneTopologyPublication.Create(nextRevision, topology);

            currentNativeContext = nativeContext;
            Current = next;

            return next;
        }

        /// <summary>
        /// Returns only the owner type and never includes native context or topology data.
        /// </summary>
        public override string ToString() => nameof(GameplaySkinLaneTopologyRevisionOwner<TNativeContext>);
    }
}
