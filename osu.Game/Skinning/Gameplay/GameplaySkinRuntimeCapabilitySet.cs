// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace osu.Game.Skinning.Gameplay
{
    [Flags]
    public enum GameplaySkinRuntimeSlotCapability
    {
        None = 0,
        Provide = 1 << 0,
        Suppress = 1 << 1,
    }

    /// <summary>
    /// Runtime support for one stable catalog slot. This does not alter catalog eligibility or authoring semantics.
    /// </summary>
    public sealed class GameplaySkinRuntimeSlotSupport
    {
        public GameplaySkinSlotDescriptor Descriptor { get; }

        public GameplaySkinRuntimeSlotCapability Capabilities { get; }

        private GameplaySkinRuntimeSlotSupport(GameplaySkinSlotDescriptor descriptor, GameplaySkinRuntimeSlotCapability capabilities)
        {
            Descriptor = descriptor;
            Capabilities = capabilities;
        }

        public static GameplaySkinRuntimeSlotSupport Create(GameplaySkinSlotDescriptor descriptor, GameplaySkinRuntimeSlotCapability capabilities)
        {
            ArgumentNullException.ThrowIfNull(descriptor);

            const GameplaySkinRuntimeSlotCapability known = GameplaySkinRuntimeSlotCapability.Provide | GameplaySkinRuntimeSlotCapability.Suppress;

            if (capabilities == GameplaySkinRuntimeSlotCapability.None || (capabilities & ~known) != 0)
                throw new ArgumentOutOfRangeException(nameof(capabilities));

            if ((capabilities & GameplaySkinRuntimeSlotCapability.Suppress) != 0
                && descriptor.SuppressEligibility != GameplaySkinSlotSuppressEligibility.Allowed)
                throw new ArgumentException("Runtime support cannot grant suppression which the public catalog forbids.", nameof(capabilities));

            return new GameplaySkinRuntimeSlotSupport(descriptor, capabilities);
        }
    }

    /// <summary>
    /// Immutable runtime capability view for one consumer or ruleset.
    /// </summary>
    public sealed class GameplaySkinRuntimeCapabilitySet
    {
        public IReadOnlyDictionary<string, GameplaySkinRuntimeSlotSupport> Support { get; }

        private GameplaySkinRuntimeCapabilitySet(IReadOnlyDictionary<string, GameplaySkinRuntimeSlotSupport> support)
        {
            Support = support;
        }

        public static GameplaySkinRuntimeCapabilitySet Create(IEnumerable<GameplaySkinRuntimeSlotSupport> support)
        {
            ArgumentNullException.ThrowIfNull(support);

            var result = new Dictionary<string, GameplaySkinRuntimeSlotSupport>(StringComparer.Ordinal);

            foreach (GameplaySkinRuntimeSlotSupport item in support)
            {
                ArgumentNullException.ThrowIfNull(item);

                if (!GameplaySkinSlotCatalog.TryGet(item.Descriptor.Id, out GameplaySkinSlotDescriptor? catalogued)
                    || !ReferenceEquals(catalogued, item.Descriptor))
                    throw new ArgumentException("Runtime capability entries must reference the exact public catalog descriptor.", nameof(support));

                if (!result.TryAdd(item.Descriptor.Id, item))
                    throw new ArgumentException("Runtime capability entries must be unique by stable slot ID.", nameof(support));
            }

            return new GameplaySkinRuntimeCapabilitySet(new ReadOnlyDictionary<string, GameplaySkinRuntimeSlotSupport>(result));
        }

        public bool TryGet(GameplaySkinSlotDescriptor descriptor, out GameplaySkinRuntimeSlotSupport? slotSupport)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            return Support.TryGetValue(descriptor.Id, out slotSupport);
        }

        public override string ToString() => $"{nameof(GameplaySkinRuntimeCapabilitySet)}:{Support.Count}";
    }
}
