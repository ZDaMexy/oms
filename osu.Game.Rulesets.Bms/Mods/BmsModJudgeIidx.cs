// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Rulesets.Bms.Scoring;

namespace osu.Game.Rulesets.Bms.Mods
{
    public class BmsModJudgeIidx : BmsModJudgeMode
    {
        public override string Name => "IIDX Judge";

        public override string Acronym => "IIDXJ";

        public override LocalisableString Description => BmsModStrings.JudgeIidxDescription;

        public override Type[] IncompatibleMods => new[] { typeof(BmsModJudgeMode), typeof(BmsModJudgeRank) };

        public override BmsJudgeMode JudgeMode => BmsJudgeMode.IIDX;
    }
}
