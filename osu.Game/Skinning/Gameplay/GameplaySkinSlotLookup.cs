// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// A catalogued gameplay skin lookup exposing its semantic descriptor.
    /// </summary>
    public interface IGameplaySkinSlotLookup
    {
        GameplaySkinSlotDescriptor Descriptor { get; }
    }

    /// <summary>
    /// Combines a stable semantic slot with ruleset-specific lookup context.
    /// </summary>
    /// <typeparam name="TContext">The lane, keymode, result or other context required to provide the slot.</typeparam>
    public sealed class GameplaySkinSlotLookup<TContext> : IGameplaySkinSlotLookup
        where TContext : notnull
    {
        public GameplaySkinSlotDescriptor Descriptor { get; }

        public TContext Context { get; }

        public GameplaySkinSlotLookup(GameplaySkinSlotDescriptor descriptor, TContext context)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            ArgumentNullException.ThrowIfNull(context);

            Descriptor = descriptor;
            Context = context;
        }

        /// <summary>
        /// Returns only the stable semantic ID. Ruleset context may contain private data and is deliberately excluded.
        /// </summary>
        public override string ToString() => Descriptor.Id;
    }
}
