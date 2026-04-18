// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Scoring
{
    public class BeatorajaJudgementSystem : BmsJudgementSystem
    {
        private WindowProfile noteProfile;
        private WindowProfile scratchProfile;
        private WindowProfile longNoteReleaseProfile;
        private WindowProfile longScratchReleaseProfile;

        public override IReadOnlyList<double> Windows => noteProfile.Windows;

        public override IReadOnlyList<double> ScratchWindows => scratchProfile.Windows;

        public override IReadOnlyList<double> LongNoteReleaseWindows => longNoteReleaseProfile.Windows;

        public override IReadOnlyList<double> LongScratchReleaseWindows => longScratchReleaseProfile.Windows;

        public BeatorajaJudgementSystem()
            => SetDifficulty(BmsJudgeRank.Normal.ToOverallDifficulty());

        public override void SetDifficulty(double overallDifficulty)
        {
            int scale = getJudgeRankScale(BmsJudgeRankExtensions.FromOverallDifficulty(overallDifficulty));

            noteProfile = createProfile(20, 60, 150, 220, 280, scale, excessivePoorEarly: 500, excessivePoorLate: 150);
            scratchProfile = createProfile(30, 70, 160, 230, 290, scale, excessivePoorEarly: 500, excessivePoorLate: 160);
            longNoteReleaseProfile = createProfile(120, 160, 200, 220, 280, scale);
            longScratchReleaseProfile = createProfile(130, 170, 210, 230, 290, scale);
        }

        public override HitResult Evaluate(double offsetMs, bool isLongNoteRelease = false, bool isScratch = false)
        {
            var profile = getProfile(isLongNoteRelease, isScratch);

            double absoluteOffset = Math.Abs(offsetMs);

            if (absoluteOffset <= profile.Windows[0])
                return HitResult.Perfect;

            if (absoluteOffset <= profile.Windows[1])
                return HitResult.Great;

            if (absoluteOffset <= profile.Windows[2])
                return HitResult.Good;

            if (offsetMs < 0)
                return -offsetMs <= profile.BadEarlyWindow ? HitResult.Meh : HitResult.None;

            return offsetMs <= profile.BadLateWindow ? HitResult.Meh : HitResult.None;
        }

        public override double GetEarlyWindow(HitResult result, bool isLongNoteRelease = false, bool isScratch = false)
            => getProfile(isLongNoteRelease, isScratch).GetEarlyWindow(result);

        public override double GetLateWindow(HitResult result, bool isLongNoteRelease = false, bool isScratch = false)
            => getProfile(isLongNoteRelease, isScratch).GetLateWindow(result);

        public override double? GetExcessivePoorEarlyWindow(bool isScratch = false)
            => getProfile(isLongNoteRelease: false, isScratch).ExcessivePoorEarlyWindow;

        public override double? GetExcessivePoorLateWindow(bool isScratch = false)
            => getProfile(isLongNoteRelease: false, isScratch).ExcessivePoorLateWindow;

        private WindowProfile getProfile(bool isLongNoteRelease, bool isScratch)
            => isLongNoteRelease
                ? isScratch ? longScratchReleaseProfile : longNoteReleaseProfile
                : isScratch ? scratchProfile : noteProfile;

        private static WindowProfile createProfile(int perfect, int great, int good, int badEarly, int badLate, int scale, int? excessivePoorEarly = null, int? excessivePoorLate = null)
        {
            double scaledPerfect = scaleWindow(perfect, scale);
            double scaledGreat = scaleWindow(great, scale);
            double scaledGood = scaleWindow(good, scale);
            double scaledBadEarly = scaleWindow(badEarly, scale);
            double scaledBadLate = scaleWindow(badLate, scale);

            return new WindowProfile(
                new[] { scaledPerfect, scaledGreat, scaledGood, scaledBadLate, scaledBadLate },
                scaledBadEarly,
                scaledBadLate,
                excessivePoorEarly,
                excessivePoorLate);
        }

        private static int getJudgeRankScale(BmsJudgeRank judgeRank)
            => judgeRank switch
            {
                BmsJudgeRank.VeryHard => 25,
                BmsJudgeRank.Hard => 50,
                BmsJudgeRank.Easy => 100,
                BmsJudgeRank.VeryEasy => 125,
                _ => 75,
            };

        private static double scaleWindow(int value, int scale)
            => Math.Truncate(value * scale / 100.0);

        private readonly struct WindowProfile
        {
            public readonly IReadOnlyList<double> Windows;
            public readonly double BadEarlyWindow;
            public readonly double BadLateWindow;
            public readonly double? ExcessivePoorEarlyWindow;
            public readonly double? ExcessivePoorLateWindow;

            public WindowProfile(IReadOnlyList<double> windows, double badEarlyWindow, double badLateWindow, double? excessivePoorEarlyWindow, double? excessivePoorLateWindow)
            {
                Windows = windows;
                BadEarlyWindow = badEarlyWindow;
                BadLateWindow = badLateWindow;
                ExcessivePoorEarlyWindow = excessivePoorEarlyWindow;
                ExcessivePoorLateWindow = excessivePoorLateWindow;
            }

            public double GetEarlyWindow(HitResult result)
                => result == HitResult.Meh ? BadEarlyWindow : getWindow(result);

            public double GetLateWindow(HitResult result)
                => result == HitResult.Meh ? BadLateWindow : getWindow(result);

            private double getWindow(HitResult result)
                => result switch
                {
                    HitResult.Perfect => Windows[0],
                    HitResult.Great => Windows[1],
                    HitResult.Good => Windows[2],
                    HitResult.Meh => Windows[3],
                    HitResult.Miss => Windows[4],
                    _ => throw new ArgumentOutOfRangeException(nameof(result), result, null),
                };
        }
    }
}
