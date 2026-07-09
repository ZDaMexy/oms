// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;

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

        public BmsLaneSkinLookup(BmsLaneSkinElements element, int laneIndex, int laneCount, bool isScratch, BmsKeymode keymode, bool isMajorBarLine = true)
        {
            Element = element;
            LaneIndex = laneIndex;
            LaneCount = laneCount;
            IsScratch = isScratch;
            Keymode = keymode;
            IsMajorBarLine = isMajorBarLine;
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

        public BmsNoteSkinLookup(BmsNoteSkinElements element, int laneIndex, bool isScratch, BmsKeymode keymode = BmsKeymode.Key7K)
        {
            Element = element;
            LaneIndex = laneIndex;
            IsScratch = isScratch;
            Keymode = keymode;
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
