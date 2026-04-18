// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics.Colour;
using osu.Game.Graphics;
using osu.Game.Online.Leaderboards;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Scoring;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    internal readonly struct BmsDjLevelDisplayInfo
    {
        public long ExScore { get; }

        public long MaxExScore { get; }

        public double ExRatio { get; }

        public BmsDjLevel Level { get; }

        public BmsDjLevelDisplayInfo(long exScore, long maxExScore)
        {
            ExScore = exScore;
            MaxExScore = maxExScore;
            ExRatio = maxExScore > 0 ? exScore / (double)maxExScore : 0;
            Level = BmsDjLevelCalculator.Calculate(exScore, maxExScore);
        }

        public static BmsDjLevelDisplayInfo FromScore(ScoreInfo score)
            => new BmsDjLevelDisplayInfo(BmsScoreProcessor.CalculateExScore(score.Statistics), BmsScoreProcessor.CalculateMaxExScore(score.MaximumStatistics));
    }

    internal static class BmsDjLevelDisplay
    {
        public const string LabelText = "DJ LEVEL";

        public static readonly BmsDjLevel[] CircleLevels =
        {
            BmsDjLevel.F,
            BmsDjLevel.E,
            BmsDjLevel.D,
            BmsDjLevel.C,
            BmsDjLevel.B,
            BmsDjLevel.A,
            BmsDjLevel.AA,
            BmsDjLevel.AAA,
        };

        public static readonly BmsDjLevel[] BadgeLevels =
        {
            BmsDjLevel.E,
            BmsDjLevel.D,
            BmsDjLevel.C,
            BmsDjLevel.B,
            BmsDjLevel.A,
            BmsDjLevel.AA,
            BmsDjLevel.AAA,
        };

        public static string GetText(BmsDjLevel level) => level.ToString();

        public static double GetThreshold(BmsDjLevel level)
            => level switch
            {
                BmsDjLevel.AAA => 8d / 9,
                BmsDjLevel.AA => 7d / 9,
                BmsDjLevel.A => 6d / 9,
                BmsDjLevel.B => 5d / 9,
                BmsDjLevel.C => 4d / 9,
                BmsDjLevel.D => 3d / 9,
                BmsDjLevel.E => 2d / 9,
                _ => 0,
            };

        public static Color4 GetFillColour(BmsDjLevel level)
            => level switch
            {
                BmsDjLevel.AAA => OsuColour.ForRank(ScoreRank.X),
                BmsDjLevel.AA => OsuColour.ForRank(ScoreRank.S),
                BmsDjLevel.A => OsuColour.ForRank(ScoreRank.A),
                BmsDjLevel.B => OsuColour.ForRank(ScoreRank.B),
                BmsDjLevel.C => OsuColour.ForRank(ScoreRank.C),
                BmsDjLevel.D => OsuColour.ForRank(ScoreRank.D),
                BmsDjLevel.E => Color4Extensions.FromHex("ff7043"),
                _ => OsuColour.ForRank(ScoreRank.F),
            };

        public static ColourInfo GetTextColour(BmsDjLevel level)
            => level switch
            {
                BmsDjLevel.AAA => DrawableRank.GetRankLetterColour(ScoreRank.X),
                BmsDjLevel.AA => DrawableRank.GetRankLetterColour(ScoreRank.S),
                BmsDjLevel.A => DrawableRank.GetRankLetterColour(ScoreRank.A),
                BmsDjLevel.B => DrawableRank.GetRankLetterColour(ScoreRank.B),
                BmsDjLevel.C => DrawableRank.GetRankLetterColour(ScoreRank.C),
                BmsDjLevel.D => DrawableRank.GetRankLetterColour(ScoreRank.D),
                BmsDjLevel.E => Color4Extensions.FromHex("5d271f"),
                _ => DrawableRank.GetRankLetterColour(ScoreRank.F),
            };
    }
}
