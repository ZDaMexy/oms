// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Objects
{
    /// <summary>
    /// A non-hittable BGM event queued to the keysound system.
    /// </summary>
    public class BmsBgmEvent : HitObject
    {
        private HitObjectProperty<int?> keysoundId;
        private HitObjectProperty<BmsKeysoundSampleInfo?> keysoundSample;

        public int? KeysoundId
        {
            get => keysoundId.Value;
            set => keysoundId.Value = value;
        }

        public BmsKeysoundSampleInfo? KeysoundSample
        {
            get => keysoundSample.Value;
            set => keysoundSample.Value = value;
        }

        public bool AutoPlay => true;

        protected override HitWindows CreateHitWindows() => HitWindows.Empty;

        public override Judgement CreateJudgement() => new IgnoreJudgement();
    }
}
