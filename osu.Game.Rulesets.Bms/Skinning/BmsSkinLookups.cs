// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Skinning
{
    public enum BmsPlayfieldSkinElements
    {
        Backdrop,
        Baseplate,
    }

    public sealed class BmsPlayfieldSkinLookup : ISkinComponentLookup
    {
        public BmsPlayfieldSkinElements Element { get; }

        public BmsKeymode Keymode { get; }

        public int LaneCount { get; }

        public BmsPlayfieldSkinLookup(BmsPlayfieldSkinElements element, BmsKeymode keymode, int laneCount)
        {
            Element = element;
            Keymode = keymode;
            LaneCount = laneCount;
        }

        public override string ToString() => $"[{nameof(BmsPlayfieldSkinLookup)} element:{Element} keymode:{Keymode} lanes:{LaneCount}]";
    }

    public enum BmsLaneSkinElements
    {
        Background,
        Divider,
        HitTarget,
        BarLine,
    }

    public sealed class BmsLaneSkinLookup : ISkinComponentLookup
    {
        public BmsLaneSkinElements Element { get; }

        public int LaneIndex { get; }

        public int LaneCount { get; }

        public bool IsScratch { get; }

        public BmsKeymode Keymode { get; }

        public bool IsMajorBarLine { get; }

        public GameplaySkinLaneId? LaneId { get; }

        internal BmsPlayfieldLayoutProfile? LayoutProfile { get; }

        internal BmsGameplayLayoutSnapshot? LayoutSnapshot { get; }

        public BmsLaneSkinLookup(
            BmsLaneSkinElements element,
            int laneIndex,
            int laneCount,
            bool isScratch,
            BmsKeymode keymode,
            bool isMajorBarLine = true,
            GameplaySkinLaneId? laneId = null,
            BmsPlayfieldLayoutProfile? layoutProfile = null,
            BmsGameplayLayoutSnapshot? layoutSnapshot = null)
        {
            Element = element;
            LaneIndex = laneIndex;
            LaneCount = laneCount;
            IsScratch = isScratch;
            Keymode = keymode;
            IsMajorBarLine = isMajorBarLine;
            LaneId = laneId;
            LayoutProfile = layoutProfile;
            LayoutSnapshot = layoutSnapshot;
        }

        public override string ToString() => $"[{nameof(BmsLaneSkinLookup)} element:{Element} lane:{LaneIndex}/{LaneCount} scratch:{IsScratch} keymode:{Keymode} major:{IsMajorBarLine}]";
    }

    public enum BmsNoteSkinElements
    {
        Note,
        LongNoteHead,
        LongNoteBody,
        LongNoteTail,
    }

    public sealed class BmsNoteSkinLookup : ISkinComponentLookup
    {
        public BmsNoteSkinElements Element { get; }

        public int LaneIndex { get; }

        public bool IsScratch { get; }

        public BmsKeymode Keymode { get; }

        public GameplaySkinLaneId? LaneId { get; }

        /// <summary>
        /// The exact committed C3 layout publication which supplied <see cref="LaneId"/> and every lane index carried by
        /// this lookup. Production resource compatibility must retain this reference rather than reconstructing an
        /// equivalent topology from keymode or drawable order.
        /// </summary>
        public BmsGameplayLayoutSnapshot? LayoutSnapshot { get; }

        /// <summary>
        /// The fully prepared material result paired with <see cref="LayoutSnapshot"/> by the same C2/C3 publication.
        /// Production consumers use this exact reference and never repeat candidate resolution after commit.
        /// </summary>
        public GameplaySkinResolvedMaterialSet? MaterialSet { get; }

        /// <summary>
        /// Whether <see cref="MaterialSet"/> is the final C4 catalog/codec/resolver publication rather than the
        /// explicitly empty compatibility carrier used by isolated legacy-layout hosts.
        /// </summary>
        public bool UsesResolvedMaterial
            => MaterialSet != null
               && !MaterialSet.ContractIdentity.Equals(GameplaySkinMaterialContractIdentity.CompatibilityEmpty);

        public BmsNoteSkinLookup(
            BmsNoteSkinElements element,
            int laneIndex,
            bool isScratch,
            BmsKeymode keymode,
            GameplaySkinLaneId? laneId = null,
            BmsGameplayLayoutSnapshot? layoutSnapshot = null,
            GameplaySkinResolvedMaterialSet? materialSet = null)
        {
            if (materialSet != null
                && (layoutSnapshot == null || !ReferenceEquals(materialSet.Snapshot, layoutSnapshot.Neutral)))
            {
                throw new System.ArgumentException(
                    "A BMS note lookup material set must retain the exact supplied layout snapshot.",
                    nameof(materialSet));
            }

            Element = element;
            LaneIndex = laneIndex;
            IsScratch = isScratch;
            Keymode = keymode;
            LaneId = laneId;
            LayoutSnapshot = layoutSnapshot;
            MaterialSet = materialSet;
        }

        public override string ToString() => $"[{nameof(BmsNoteSkinLookup)} element:{Element} lane:{LaneIndex} scratch:{IsScratch} keymode:{Keymode}]";
    }

    public sealed class BmsLaneCoverSkinLookup : ISkinComponentLookup
    {
        public BmsLaneCoverPosition Position { get; }

        public BmsLaneCoverSkinLookup(BmsLaneCoverPosition position)
        {
            Position = position;
        }

        public override string ToString() => $"[{nameof(BmsLaneCoverSkinLookup)} position:{Position}]";
    }

    public sealed class BmsJudgementSkinLookup : ISkinComponentLookup
    {
        public HitResult Result { get; }

        public string DisplayName => BmsHitResultDisplayNames.GetDisplayName(Result);

        public BmsJudgementSkinLookup(HitResult result)
        {
            Result = result;
        }

        public override string ToString() => $"[{nameof(BmsJudgementSkinLookup)} result:{Result} display:{DisplayName}]";
    }
}
