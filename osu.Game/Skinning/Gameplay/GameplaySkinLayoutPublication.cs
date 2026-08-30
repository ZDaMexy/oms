// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// A ruleset-native, immutable projection of the one neutral gameplay layout snapshot.
    /// </summary>
    /// <remarks>
    /// Implementations may retain renderer-facing metrics which were produced by the same solver, but must not own a
    /// second current revision or independently recompute geometry.
    /// </remarks>
    public interface IGameplaySkinLayoutAdapter
    {
        GameplaySkinLayoutSnapshot Snapshot { get; }
    }

    /// <summary>
    /// The indivisible publication unit committed by one gameplay layout owner.
    /// </summary>
    /// <remarks>
    /// Keeping the neutral snapshot and its ruleset-native adapter in this one immutable object prevents a consumer
    /// from observing a newly committed neutral revision with an adapter from the previous revision.
    /// </remarks>
    public sealed class GameplaySkinLayoutPublication
    {
        public GameplaySkinLayoutSnapshot Snapshot { get; }

        public IGameplaySkinLayoutAdapter Adapter { get; }

        private GameplaySkinLayoutPublication(GameplaySkinLayoutSnapshot snapshot, IGameplaySkinLayoutAdapter adapter)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));

            if (!ReferenceEquals(adapter.Snapshot, snapshot))
                throw new ArgumentException("A gameplay layout adapter must retain the exact neutral snapshot being published.", nameof(adapter));
        }

        public static GameplaySkinLayoutPublication Create<TAdapter>(TAdapter adapter)
            where TAdapter : class, IGameplaySkinLayoutAdapter
        {
            ArgumentNullException.ThrowIfNull(adapter);
            return new GameplaySkinLayoutPublication(adapter.Snapshot, adapter);
        }

        public TAdapter GetAdapter<TAdapter>()
            where TAdapter : class, IGameplaySkinLayoutAdapter
            => Adapter as TAdapter
               ?? throw new InvalidOperationException($"The committed gameplay layout adapter is not {typeof(TAdapter).Name}.");

        internal static GameplaySkinLayoutPublication CreateNeutral(GameplaySkinLayoutSnapshot snapshot)
            => new GameplaySkinLayoutPublication(snapshot, new NeutralAdapter(snapshot));

        private sealed class NeutralAdapter : IGameplaySkinLayoutAdapter
        {
            public GameplaySkinLayoutSnapshot Snapshot { get; }

            public NeutralAdapter(GameplaySkinLayoutSnapshot snapshot)
            {
                Snapshot = snapshot;
            }
        }
    }
}
