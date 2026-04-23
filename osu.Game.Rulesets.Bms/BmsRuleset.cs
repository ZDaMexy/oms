// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.DifficultyTable;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Bms.SongSelect;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.Replays.Types;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Scoring;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;
using osu.Game.Screens.Ranking.Statistics;
using osu.Game.Skinning;
using osu.Framework.Bindables;
using oms.Input;

namespace osu.Game.Rulesets.Bms
{
    public class BmsRuleset : Ruleset
    {
        private static readonly OmsBindingStore defaultBindingStore = new OmsBindingStore();

        public const string SHORT_NAME = "bms";

        public override string Description => "BMS";

        public override string ShortName => SHORT_NAME;

        public override string PlayingVerb => "Playing BMS";

        public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null) =>
            new DrawableBmsRuleset(this, beatmap, mods);

        public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap) => new BmsBeatmapConverter(beatmap, this);

        public override ScoreProcessor CreateScoreProcessor() => new BmsScoreProcessor();

        public override HealthProcessor CreateHealthProcessor(double drainStartTime) => new BmsGaugeProcessor(drainStartTime);

        public override ISkin? CreateSkinTransformer(ISkin skin, IBeatmap beatmap)
        {
            ISkin transformer = new BmsSkinTransformer(skin);

            return skin is OmsSkin ? new OmsSkinTransformer(transformer) : transformer;
        }

        public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) => new BmsDifficultyCalculator(RulesetInfo, beatmap);

        public override IRulesetConfigManager CreateConfig(SettingsStore? settings) => new BmsRulesetConfigManager(settings, RulesetInfo);

        public override IRulesetModStatePersistence? CreateModStatePersistence(IRulesetConfigManager? configManager)
            => configManager is BmsRulesetConfigManager bmsConfig ? new BmsModStatePersistence(this, bmsConfig) : null;

        public override IConvertibleReplayFrame CreateConvertibleReplayFrame() => new BmsReplayFrame();

        public override RulesetSettingsSubsection CreateSettings() => new BmsSettingsSubsection(this);

        public override IEnumerable<Drawable> CreateKeyBindingSections() =>
            new Drawable[]
            {
                new BmsSupplementalBindingSettingsSection(this),
            };

        public override IEnumerable<int> AvailableVariants => new[] { 6, 8, 9, 16 };

        public override IEnumerable<Mod> GetModsFor(ModType type)
        {
            switch (type)
            {
                case ModType.DifficultyIncrease:
                    return new Mod[]
                    {
                        new BmsModGaugeHard(),
                        new BmsModGaugeExHard(),
                        new BmsModGaugeHazard(),
                    };

                case ModType.DifficultyReduction:
                    return new Mod[]
                    {
                        new BmsModGaugeAssistEasy(),
                        new BmsModGaugeEasy(),
                        new BmsModGaugeAutoShift(),
                        new BmsModAutoScratch(),
                        new BmsModAutoNote(),
                        new BmsModAutoplay(),
                    };

                case ModType.Conversion:
                    return new Mod[]
                    {
                        new BmsModMirror(),
                        new BmsModRandom(),
                        new BmsModChargeNote(),
                        new BmsModHellChargeNote(),
                        new BmsModSudden(),
                        new BmsModHidden(),
                        new BmsModLift(),
                        new BmsModGaugeRulesBeatoraja(),
                        new BmsModGaugeRulesLr2(),
                        new BmsModGaugeRulesIidx(),
                        new BmsModJudgeBeatoraja(),
                        new BmsModJudgeLr2(),
                        new BmsModJudgeIidx(),
                        new BmsModJudgeRank(),
                    };

                default:
                    return Array.Empty<Mod>();
            }
        }

        public override IEnumerable<KeyBinding> GetDefaultKeyBindings(int variant = 0)
        {
            variant = OmsBmsActionMap.NormalizeVariant(variant);

            foreach (var binding in defaultBindingStore.GetDefaultBindings(variant))
            {
                if (!OmsBmsActionMap.TryMapToBmsAction(variant, binding.Action, out var action))
                    continue;

                foreach (var keyCombination in binding.KeyboardCombinations)
                    yield return new KeyBinding(keyCombination.Keys.ToArray(), action);

                foreach (var trigger in binding.XInputButtonTriggers)
                {
                    if (OmsBindingStore.TryGetJoystickButtonInputKey(trigger.ButtonIndex, out var inputKey))
                        yield return new KeyBinding(inputKey, action);
                }
            }
        }

        public override LocalisableString GetVariantName(int variant)
            => variant switch
            {
                6 => "5K",
                8 => "7K",
                9 => "9K",
                16 => "14K",
                _ => base.GetVariantName(variant),
            };

        public override IEnumerable<RulesetBeatmapAttribute> GetBeatmapAttributesForDisplay(IBeatmapInfo beatmapInfo, IReadOnlyCollection<Mod> mods)
        {
            if (TryGetKeyCount(beatmapInfo, out int keyCount))
            {
                yield return new RulesetBeatmapAttribute(SongSelectStrings.KeyCount, @"KC", keyCount, keyCount, 18)
                {
                    Description = "Affects the number of key columns on the playfield."
                };
            }

            var chartJudgeRank = BmsJudgeRankExtensions.GetBeatmapJudgeRank(beatmapInfo);
            var judgeMode = BmsJudgeModeExtensions.GetJudgeMode(mods);
            var judgeRankOverride = judgeMode.SupportsJudgeDifficulty() ? BmsJudgeRankExtensions.GetJudgeRankOverride(mods) : null;
            var appliedJudgeRank = judgeRankOverride ?? chartJudgeRank;
            double appliedOverallDifficulty = judgeMode.SupportsJudgeDifficulty() ? appliedJudgeRank.ToOverallDifficulty() : 0;
            var colours = new osu.Game.Graphics.OsuColour();

            yield return new RulesetBeatmapAttribute(SongSelectStrings.Accuracy, @"RANK", 4 - chartJudgeRank.ToHeaderValue(), 4 - appliedJudgeRank.ToHeaderValue(), 4)
            {
                DisplayValue = judgeMode.SupportsJudgeDifficulty() ? appliedJudgeRank.GetDisplayName() : "FIXED",
                Description = getJudgeAttributeDescription(judgeMode, judgeRankOverride.HasValue),
                AdditionalMetrics = createJudgeAttributeMetrics(judgeMode, chartJudgeRank, appliedJudgeRank, judgeRankOverride, appliedOverallDifficulty, colours).ToArray(),
            };
        }

        public override SongSelectPanelAccent? GetSongSelectPanelAccent(ScoreInfo score)
        {
            var scoreData = score.GetRulesetData<BmsScoreInfoData>();

            if (scoreData?.HasResultStatistics != true)
                return null;

            return BmsSongSelectLampPalette.GetAccent(scoreData.ClearLamp!.Value);
        }

        public override Drawable? CreateBeatmapDetailsComponent(IBindable<WorkingBeatmap> beatmap) => new BmsNoteDistributionGraph(beatmap);

        public override IReadOnlyList<SortMode> GetAvailableSongSelectSortModes() =>
        [
            SortMode.Title,
            SortMode.Artist,
            SortMode.BPM,
            SortMode.Length,
            SortMode.Difficulty,
            SortMode.ClearLamp,
            SortMode.Accuracy,
            SortMode.Misses,
        ];

        public override IReadOnlyList<GroupMode> GetAvailableSongSelectGroupModes() =>
        [
            GroupMode.DifficultyTable,
            GroupMode.Artist,
            GroupMode.Author,
            GroupMode.BPM,
            GroupMode.Difficulty,
            GroupMode.LastPlayed,
            GroupMode.Length,
            GroupMode.RankAchieved,
            GroupMode.Title,
        ];

        public override bool ShouldResetSongSelectGroupToRoot(GroupMode mode)
            => GetAvailableSongSelectGroupModes().Contains(mode);

        public override int CompareSongSelectScores(SortMode sort, ScoreInfo? a, ScoreInfo? b)
        {
            if (sort != SortMode.ClearLamp)
                return base.CompareSongSelectScores(sort, a, b);

            return getLampOrder(b).CompareTo(getLampOrder(a));

            static int getLampOrder(ScoreInfo? score)
            {
                var scoreData = score?.GetRulesetData<BmsScoreInfoData>();

                if (scoreData?.HasResultStatistics != true || scoreData.ClearLamp == null)
                    return (int)BmsClearLamp.NoPlay;

                return (int)scoreData.ClearLamp.Value;
            }
        }

        public override IEnumerable<GroupDefinition> GetSongSelectGroupDefinitions(GroupMode mode, IBeatmapInfo beatmapInfo)
        {
            if (mode != GroupMode.DifficultyTable)
                return base.GetSongSelectGroupDefinitions(mode, beatmapInfo);

            return BmsTableGroupMode.GetGroupDefinitions(beatmapInfo);
        }

        public override void PrepareScoreInfoForResults(ScoreInfo score, IBeatmap playableBeatmap)
        {
            BmsBeatmapModApplicator.ApplyToBeatmap(playableBeatmap, score.Mods);
            score.SetRulesetData(BmsClearLampProcessor.CreatePersistentData(score, playableBeatmap));
        }

        public override Drawable CreateResultsAccuracyDisplay(ScoreInfo score, bool withFlair = false) => new BmsResultsAccuracyDisplay(score, withFlair);

        public override Drawable CreateResultsRankBadge(ScoreInfo score) => new BmsDrawableDjLevel(BmsDjLevelDisplayInfo.FromScore(score).Level);

        public override LocalisableString? GetResultsScoreLabel(ScoreInfo score) => "EX-SCORE";

        public override string GetScoreDisplayBucket(ScoreInfo score)
            => getScoreDisplayBucket(BmsJudgeModeExtensions.GetJudgeMode(score), BmsScoreProcessor.GetLongNoteMode(score), BmsJudgeRankExtensions.GetJudgeRankOverride(score));

        public override string GetScoreDisplayBucket(IReadOnlyList<Mod>? mods)
            => getScoreDisplayBucket(BmsJudgeModeExtensions.GetJudgeMode(mods), BmsScoreProcessor.GetLongNoteMode(mods), BmsJudgeRankExtensions.GetJudgeRankOverride(mods));

        public override StatisticItem[] CreateStatisticsForScore(ScoreInfo score, IBeatmap playableBeatmap)
        {
            var summary = createResultsSummaryData(score, playableBeatmap);

            return new[]
            {
                new StatisticItem(string.Empty, () => new SkinnableBmsResultsSummaryPanelDisplay(summary)),
                new StatisticItem(string.Empty, () => new SkinnableBmsGaugeHistoryPanelDisplay(BmsClearLampProcessor.CreateGaugeHistory(score, playableBeatmap)), requiresHitEvents: true)
            };
        }

        private static BmsResultsSummaryData createResultsSummaryData(ScoreInfo score, IBeatmap playableBeatmap)
        {
            long exScore = BmsScoreProcessor.CalculateExScore(score.Statistics);
            long maxExScore = BmsScoreProcessor.CalculateMaxExScore(score.MaximumStatistics);
            BmsClearLampData? clearLamp = null;

            if (BmsClearLampProcessor.TryCalculate(score, playableBeatmap, out BmsClearLamp clearLampValue, out double finalGauge))
            {
                clearLamp = new BmsClearLampData(clearLampValue, BmsClearLampProcessor.GetDisplayName(clearLampValue), finalGauge);
            }

            return new BmsResultsSummaryData(
                BmsGaugeProcessor.GetGaugeType(score),
                BmsGaugeProcessor.GetGaugeDisplayName(score),
                BmsGaugeProcessor.GetGaugeRulesFamily(score),
                BmsJudgeModeExtensions.GetJudgeMode(score),
                BmsScoreProcessor.GetLongNoteMode(score),
                exScore,
                maxExScore,
                BmsScoreProcessor.GetEmptyPoorCount(score),
                BmsScoreProcessor.GetComboBreakCount(score),
                score.Accuracy,
                BmsDjLevelCalculator.Calculate(exScore, maxExScore),
                clearLamp);
        }

        public override IEnumerable<(HitResult result, LocalisableString displayName)> GetHitResultsForDisplay()
        {
            foreach (var result in new[]
                     {
                         HitResult.Perfect,
                         HitResult.Great,
                         HitResult.Good,
                         HitResult.Meh,
                         HitResult.Miss,
                         HitResult.Ok,
                         HitResult.ComboBreak,
                     })
            {
                yield return (result, GetDisplayNameForHitResult(result));
            }
        }

        public override int GetDisplayCountForHitResult(ScoreInfo score, HitResult result)
        {
            if (!BmsScoreProcessor.UsesSeparatedEmptyPoorStatistics(score))
            {
                return result switch
                {
                    HitResult.Ok => score.Statistics.GetValueOrDefault(HitResult.ComboBreak),
                    HitResult.ComboBreak => 0,
                    _ => base.GetDisplayCountForHitResult(score, result),
                };
            }

            return base.GetDisplayCountForHitResult(score, result);
        }

        public override LocalisableString GetDisplayNameForHitResult(HitResult result)
            => BmsHitResultDisplayNames.GetDisplayName(result);

        public override Drawable CreateIcon() => new SpriteIcon { Icon = FontAwesome.Solid.Music };

        public override string RulesetAPIVersionSupported => CURRENT_RULESET_API_VERSION;

        public static bool TryGetKeyCount(IBeatmapInfo beatmapInfo, out int keyCount)
        {
            int storedKeyCount = (int)Math.Round(beatmapInfo.Difficulty.CircleSize);

            if (storedKeyCount is 5 or 7 or 9 or 14)
            {
                keyCount = storedKeyCount;
                return true;
            }

            keyCount = 0;
            return false;
        }

        public static int GetKeyCount(BmsKeymode keymode)
            => keymode switch
            {
                BmsKeymode.Key5K => 5,
                BmsKeymode.Key7K => 7,
                BmsKeymode.Key9K_Bms => 9,
                BmsKeymode.Key9K_Pms => 9,
                BmsKeymode.Key14K => 14,
                _ => 7,
            };

        private static string getScoreDisplayBucket(BmsJudgeMode judgeMode, BmsLongNoteMode longNoteMode)
            => getScoreDisplayBucket(judgeMode, longNoteMode, null);

        private static string getScoreDisplayBucket(BmsJudgeMode judgeMode, BmsLongNoteMode longNoteMode, BmsJudgeRank? judgeRankOverride)
        {
            string bucket = $"judge-mode:{judgeMode.GetDisplayName()}|long-note-mode:{longNoteMode}";

            if (judgeMode.SupportsJudgeDifficulty() && judgeRankOverride.HasValue)
                bucket += $"|judge-rank:{judgeRankOverride.Value.GetBucketToken()}";

            return bucket;
        }

        private static string getJudgeAttributeDescription(BmsJudgeMode judgeMode, bool hasJudgeRankOverride)
        {
            string description = judgeMode switch
            {
                BmsJudgeMode.Beatoraja => "Uses the current OMS beatoraja timing preset.",
                BmsJudgeMode.LR2 => "Uses the current OMS LR2 timing preset.",
                BmsJudgeMode.IIDX => "Uses the current OMS fixed IIDX timing preset. Chart #RANK and Judge Difficulty overrides do not apply.",
                _ => "Uses the BMS #RANK preset mapped to osu!mania OD timing windows.",
            };

            if (hasJudgeRankOverride)
                description += " Judge Difficulty mod is overriding the chart's default timing tier.";

            return description;
        }

        private static IEnumerable<RulesetBeatmapAttribute.AdditionalMetric> createJudgeAttributeMetrics(BmsJudgeMode judgeMode, BmsJudgeRank chartJudgeRank, BmsJudgeRank appliedJudgeRank, BmsJudgeRank? judgeRankOverride, double overallDifficulty, osu.Game.Graphics.OsuColour colours)
        {
            yield return new RulesetBeatmapAttribute.AdditionalMetric("Judge system", judgeMode.GetDisplayName());

            if (!judgeMode.SupportsJudgeDifficulty())
            {
                yield return new RulesetBeatmapAttribute.AdditionalMetric("Chart #RANK", $"{chartJudgeRank.ToHeaderValue()} ({chartJudgeRank.GetDisplayName()})");
                yield return new RulesetBeatmapAttribute.AdditionalMetric("Applied difficulty", "FIXED (IIDX)");
            }
            else if (judgeRankOverride.HasValue)
            {
                yield return new RulesetBeatmapAttribute.AdditionalMetric("Chart #RANK", $"{chartJudgeRank.ToHeaderValue()} ({chartJudgeRank.GetDisplayName()})");
                yield return new RulesetBeatmapAttribute.AdditionalMetric("Applied difficulty", appliedJudgeRank.GetDisplayName());
            }
            else
            {
                yield return new RulesetBeatmapAttribute.AdditionalMetric("Judge difficulty", $"{appliedJudgeRank.GetDisplayName()} (#RANK {chartJudgeRank.ToHeaderValue()})");
            }

            var judgementSystem = judgeMode.CreateJudgementSystem();
            judgementSystem.SetDifficulty(overallDifficulty);

            foreach (var result in new[] { HitResult.Perfect, HitResult.Great, HitResult.Good, HitResult.Meh, HitResult.Miss })
            {
                string label = result == HitResult.Miss
                    ? $"{BmsHitResultDisplayNames.GetDisplayName(result)} miss boundary"
                    : $"{BmsHitResultDisplayNames.GetDisplayName(result)} hit window";

                yield return new RulesetBeatmapAttribute.AdditionalMetric(label, formatJudgeWindow(judgementSystem, result), colours.ForHitResult(result));
            }

            double? excessivePoorEarlyWindow = judgementSystem.GetExcessivePoorEarlyWindow();
            double? excessivePoorLateWindow = judgementSystem.GetExcessivePoorLateWindow();

            if (excessivePoorEarlyWindow.HasValue && excessivePoorLateWindow.HasValue)
            {
                yield return new RulesetBeatmapAttribute.AdditionalMetric(
                    $"{BmsHitResultDisplayNames.GetDisplayName(HitResult.Ok)} window",
                    formatJudgeWindow(excessivePoorEarlyWindow.Value, excessivePoorLateWindow.Value),
                    colours.ForHitResult(HitResult.Ok));
            }
        }

        private static string formatJudgeWindow(BmsJudgementSystem judgementSystem, HitResult result)
        {
            double earlyWindow = judgementSystem.GetEarlyWindow(result);
            double lateWindow = judgementSystem.GetLateWindow(result);

            return formatJudgeWindow(earlyWindow, lateWindow);
        }

        private static string formatJudgeWindow(double earlyWindow, double lateWindow)
        {

            return Math.Abs(earlyWindow - lateWindow) <= 0.001
                ? $@"±{lateWindow:0.##} ms"
                : $@"-{earlyWindow:0.##} / +{lateWindow:0.##} ms";
        }
    }
}
