// Copyright (c) OMS contributors. Licensed under the MIT Licence.

namespace osu.Game.Rulesets.Bms.Scoring
{
    public static class BmsDjLevelCalculator
    {
        public static BmsDjLevel Calculate(long exScore, long maxExScore)
        {
            if (maxExScore <= 0)
                return BmsDjLevel.F;

            return Calculate(exScore / (double)maxExScore);
        }

        public static BmsDjLevel Calculate(double exRatio)
        {
            if (exRatio >= 8d / 9)
                return BmsDjLevel.AAA;

            if (exRatio >= 7d / 9)
                return BmsDjLevel.AA;

            if (exRatio >= 6d / 9)
                return BmsDjLevel.A;

            if (exRatio >= 5d / 9)
                return BmsDjLevel.B;

            if (exRatio >= 4d / 9)
                return BmsDjLevel.C;

            if (exRatio >= 3d / 9)
                return BmsDjLevel.D;

            if (exRatio >= 2d / 9)
                return BmsDjLevel.E;

            return BmsDjLevel.F;
        }
    }
}
