// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;
using osu.Game.Rulesets.Bms.Scoring;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModJudgeBeatoraja : BmsModJudgeMode
    {
        public override string Name => "beatoraja Judge";

        public override string Acronym => "BRJ";

        public override LocalisableString Description => @"Uses beatoraja timing windows.";

        public override BmsJudgeMode JudgeMode => BmsJudgeMode.Beatoraja;
    }
}
