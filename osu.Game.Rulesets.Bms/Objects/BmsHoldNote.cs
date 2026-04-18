// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Objects
{
    /// <summary>
    /// A long note with independent head and tail keysound references.
    /// </summary>
    public class BmsHoldNote : BmsHitObject, IHasDuration
    {
        public const double DEFAULT_RELEASE_MISS_LENIENCE = 1.25;

        private HitObjectProperty<double> duration;
        private HitObjectProperty<int?> headKeysoundId;
        private HitObjectProperty<int?> tailKeysoundId;
        private HitObjectProperty<BmsKeysoundSampleInfo?> headKeysoundSample;
        private HitObjectProperty<BmsKeysoundSampleInfo?> tailKeysoundSample;
        private readonly List<BmsHoldNoteBodyTick> bodyTicks = new List<BmsHoldNoteBodyTick>();

        public BmsHoldNoteHead? Head { get; private set; }

        public BmsHoldNoteTailEvent? Tail { get; private set; }

        public IReadOnlyList<BmsHoldNoteBodyTick> BodyTicks => bodyTicks;

        public override double StartTime
        {
            get => base.StartTime;
            set
            {
                base.StartTime = value;

                if (Head != null)
                    Head.StartTime = value;

                if (Tail != null)
                    Tail.StartTime = EndTime;
            }
        }

        public override int LaneIndex
        {
            get => base.LaneIndex;
            set
            {
                base.LaneIndex = value;

                if (Head != null)
                    Head.LaneIndex = value;

                if (Tail != null)
                    Tail.LaneIndex = value;
            }
        }

        public override bool IsScratch
        {
            get => base.IsScratch;
            set
            {
                base.IsScratch = value;

                if (Head != null)
                    Head.IsScratch = value;

                if (Tail != null)
                    Tail.IsScratch = value;
            }
        }

        public override BmsKeymode Keymode
        {
            get => base.Keymode;
            set
            {
                base.Keymode = value;

                if (Head != null)
                    Head.Keymode = value;

                if (Tail != null)
                    Tail.Keymode = value;
            }
        }

        public override bool AutoPlay
        {
            get => base.AutoPlay;
            set
            {
                base.AutoPlay = value;

                if (Head != null)
                    Head.AutoPlay = value;

                if (Tail != null)
                    Tail.AutoPlay = value;
            }
        }

        public override bool CountsForScore
        {
            get => base.CountsForScore;
            set
            {
                base.CountsForScore = value;

                if (Head != null)
                    Head.CountsForScore = value;

                if (Tail != null)
                    Tail.CountsForScore = value;
            }
        }

        public double EndTime
        {
            get => StartTime + Duration;
            set => Duration = value - StartTime;
        }

        public double Duration
        {
            get => duration.Value;
            set
            {
                duration.Value = Math.Max(0, value);

                if (Tail != null)
                    Tail.StartTime = EndTime;
            }
        }

        public override double MaximumJudgementOffset => Tail?.MaximumJudgementOffset ?? 0;

        public override int? KeysoundId
        {
            get => HeadKeysoundId;
            set => HeadKeysoundId = value;
        }

        public override BmsKeysoundSampleInfo? KeysoundSample
        {
            get => HeadKeysoundSample;
            set => HeadKeysoundSample = value;
        }

        public int? HeadKeysoundId
        {
            get => headKeysoundId.Value;
            set => headKeysoundId.Value = value;
        }

        public BmsKeysoundSampleInfo? HeadKeysoundSample
        {
            get => headKeysoundSample.Value;
            set => headKeysoundSample.Value = value;
        }

        public int? TailKeysoundId
        {
            get => tailKeysoundId.Value;
            set => tailKeysoundId.Value = value;
        }

        public BmsKeysoundSampleInfo? TailKeysoundSample
        {
            get => tailKeysoundSample.Value;
            set => tailKeysoundSample.Value = value;
        }

        protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
        {
            base.CreateNestedHitObjects(cancellationToken);

            bodyTicks.Clear();

            AddNested(Head = new BmsHoldNoteHead
            {
                StartTime = StartTime,
                LaneIndex = LaneIndex,
                KeysoundId = HeadKeysoundId,
                KeysoundSample = HeadKeysoundSample,
                Keymode = Keymode,
                IsScratch = IsScratch,
                AutoPlay = AutoPlay,
                CountsForScore = CountsForScore,
            });

            Tail = null;

            AddNested(Tail = new BmsHoldNoteTailEvent
            {
                StartTime = EndTime,
                LaneIndex = LaneIndex,
                KeysoundId = TailKeysoundId,
                KeysoundSample = TailKeysoundSample,
                Keymode = Keymode,
                IsScratch = IsScratch,
                AutoPlay = AutoPlay,
                CountsForScore = CountsForScore,
            });

            for (double tickTime = StartTime + BmsHoldNoteBodyTick.TICK_QUANTUM; tickTime < EndTime; tickTime += BmsHoldNoteBodyTick.TICK_QUANTUM)
            {
                var bodyTick = new BmsHoldNoteBodyTick
                {
                    StartTime = tickTime,
                };

                bodyTicks.Add(bodyTick);
                AddNested(bodyTick);
            }
        }

        public override Judgement CreateJudgement() => new IgnoreJudgement();

        protected override HitWindows CreateHitWindows() => HitWindows.Empty;
    }
}
