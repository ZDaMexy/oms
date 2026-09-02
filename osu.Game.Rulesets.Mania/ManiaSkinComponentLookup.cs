// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania
{
    public class ManiaSkinComponentLookup : SkinComponentLookup<ManiaSkinComponents>
    {
        /// <summary>
        /// The exact immutable C4 material publication carried by this lookup, or <see langword="null"/> for an
        /// explicitly unmigrated compatibility lookup.
        /// </summary>
        public GameplaySkinResolvedMaterialSet? ResolvedMaterialSet { get; }

        /// <summary>
        /// The exact catalog slot and stable topology target requested by this lookup.
        /// </summary>
        public GameplaySkinResolvedMaterialKey? ResolvedMaterialKey { get; }

        public GameplaySkinResolvedMaterialTarget? ResolvedMaterialTarget => ResolvedMaterialKey?.Target;

        /// <summary>
        /// Creates a new <see cref="ManiaSkinComponentLookup"/>.
        /// </summary>
        /// <param name="component">The component.</param>
        public ManiaSkinComponentLookup(ManiaSkinComponents component)
            : base(component)
        {
        }

        internal ManiaSkinComponentLookup(ManiaSkinComponents component, ManiaGameplaySkinMaterialContext context)
            : base(component)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (!context.UsesResolvedMaterial)
                return;

            ResolvedMaterialSet = context.MaterialSet;
            ResolvedMaterialKey = context.GetKey(component);
        }
    }

    public enum ManiaSkinComponents
    {
        ColumnBackground,
        HitTarget,
        KeyArea,
        Note,
        HoldNoteHead,
        HoldNoteTail,
        HoldNoteBody,
        HitExplosion,
        StageBackground,
        StageForeground,
        BarLine
    }
}
