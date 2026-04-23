// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModJudgeRank : Mod, IApplicableToDifficulty, IPreserveSettingsWhenDisabled
    {
        public override string Name => "Judge Difficulty";

        public override string Acronym => "JD";

        public override LocalisableString Description => BmsModStrings.JudgeRankDescription;

        public override ModType Type => ModType.Conversion;

        public override double ScoreMultiplier => 1;

        public override bool Ranked => true;

        public override bool RequiresConfiguration => true;

        public override Type[] IncompatibleMods => new[] { typeof(BmsModJudgeRank), typeof(BmsModJudgeIidx) };

        [SettingSource(typeof(BmsModStrings), nameof(BmsModStrings.JudgeDifficulty), nameof(BmsModStrings.JudgeDifficultyDescription))]
        public Bindable<BmsJudgeRank> JudgeRank { get; } = new Bindable<BmsJudgeRank>(BmsJudgeRank.Normal);

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                if (!JudgeRank.IsDefault)
                    yield return (BmsModStrings.JudgeDifficulty, JudgeRank.Value.GetLocalisableDescription());
            }
        }

        public void ApplyToDifficulty(BeatmapDifficulty difficulty)
            => difficulty.OverallDifficulty = JudgeRank.Value.ToOverallDifficulty();
    }
}
