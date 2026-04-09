// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Bindables;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Objects
{
    public class BmsBarLine : HitObject, IBarLine
    {
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
