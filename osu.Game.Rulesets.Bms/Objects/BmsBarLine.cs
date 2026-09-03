// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Bindables;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Objects
{
    public class BmsBarLine : HitObject, IBarLine
    {
        /// <summary>
        /// Final C3 group target. A measure marker spans exactly one deck/stage and must never acquire lane identity.
        /// </summary>
        public int GroupLogicalIndex { get; set; }

        /// <summary>
        /// Stable group identity paired with <see cref="GroupLogicalIndex"/> by the exact C3 topology.
        /// </summary>
        public GameplaySkinLaneGroupId? GroupId { get; set; }

        private HitObjectProperty<bool> major;

        public Bindable<bool> MajorBindable => major.Bindable;

        public bool Major
        {
            get => major.Value;
            set => major.Value = value;
        }

        protected override HitWindows CreateHitWindows() => HitWindows.Empty;

        public override Judgement CreateJudgement() => new IgnoreJudgement();
    }
}
