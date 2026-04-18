// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Rulesets.Bms.Scoring;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModJudgeLr2 : BmsModJudgeMode
    {
        public override string Name => "LR2 Judge";

        public override string Acronym => "LR2";

        public override LocalisableString Description => BmsModStrings.JudgeLr2Description;

        public override BmsJudgeMode JudgeMode => BmsJudgeMode.LR2;
    }
}
