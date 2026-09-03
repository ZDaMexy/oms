// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.Skinning
{
    /// <summary>
    /// Exposes independently gateable pieces of an existing mania visual without moving geometry or input ownership.
    /// </summary>
    internal interface IManiaGameplaySkinProgrammaticVisualPartProvider
    {
        IReadOnlyList<ManiaGameplaySkinProgrammaticVisualPart> GameplaySkinProgrammaticVisualParts { get; }
    }

    /// <summary>
    /// Signals that dependency-loaded native owners are available while a nested playfield is still at
    /// <see cref="LoadState.Ready"/>. Consumers schedule the actual registration on
    /// their update thread; the provider never mutates scene authority.
    /// </summary>
    internal interface IManiaGameplaySkinProgrammaticVisualPartReadinessSource
    {
        event Action GameplaySkinProgrammaticVisualPartsReady;
    }

    /// <summary>
    /// One native visual owner for one public slot. A lane index is only supplied by stage-wide legacy composites.
    /// </summary>
    internal readonly record struct ManiaGameplaySkinProgrammaticVisualPart
    {
        public GameplaySkinSlotDescriptor Slot { get; }

        public Drawable Owner { get; }

        public int? GroupLocalLaneIndex { get; }

        public ManiaGameplaySkinProgrammaticVisualPart(
            GameplaySkinSlotDescriptor slot,
            Drawable owner,
            int? groupLocalLaneIndex = null)
        {
            Slot = slot ?? throw new ArgumentNullException(nameof(slot));
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));

            if (groupLocalLaneIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(groupLocalLaneIndex));

            GroupLocalLaneIndex = groupLocalLaneIndex;
        }
    }

    internal static class ManiaGameplaySkinProgrammaticVisualPartTargetResolver
    {
        public static GameplaySkinResolvedMaterialTarget Resolve(
            ManiaGameplaySkinProgrammaticVisualPart part,
            GameplaySkinLaneTopologyGroup group,
            GameplaySkinLaneTopologyEntry? defaultLane = null)
        {
            if (ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.JudgementLine)
                || ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.StageBackground)
                || ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.PlayfieldBackdrop)
                || ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.PlayfieldBaseplate))
            {
                if (part.GroupLocalLaneIndex.HasValue)
                    throw new InvalidOperationException("A stage-scoped mania programmatic visual part cannot declare a lane index.");

                return GameplaySkinResolvedMaterialTarget.ForStage(group);
            }

            if (!ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.LaneSurface)
                && !ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.LaneDivider)
                && !ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.HitTarget)
                && !ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.KeyFlash))
            {
                throw new InvalidOperationException($"The mania programmatic visual part slot '{part.Slot.Id}' is not allowlisted.");
            }

            GameplaySkinLaneTopologyEntry lane;

            if (part.GroupLocalLaneIndex is int localIndex)
            {
                if ((uint)localIndex >= (uint)group.LanesInLogicalOrder.Count)
                    throw new InvalidOperationException("A lane-scoped mania programmatic visual part has an invalid group-local index.");

                lane = group.LanesInLogicalOrder[localIndex];

                if (defaultLane != null && !ReferenceEquals(defaultLane, lane))
                    throw new InvalidOperationException("A column-local mania programmatic visual part cannot target another lane.");
            }
            else
            {
                lane = defaultLane
                       ?? throw new InvalidOperationException("A stage-owned lane visual part requires an explicit group-local lane index.");
            }

            return GameplaySkinResolvedMaterialTarget.ForLane(group, lane);
        }
    }
}
