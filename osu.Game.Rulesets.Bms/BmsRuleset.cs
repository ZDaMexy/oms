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
using osu.Game.Rulesets.Bms.SongSelect;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
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
                        new BmsModLaneCoverTop(),
                        new BmsModLaneCoverBottom(),
                        new BmsModGaugeAutoShift(),
                        new BmsModGaugeHard(),
                        new BmsModGaugeExHard(),
                        new BmsModGaugeHazard(),
                    };

                case ModType.DifficultyReduction:
                    return new Mod[]
                    {
                        new BmsModGaugeAssistEasy(),
                        new BmsModGaugeEasy(),
                    };

                case ModType.Conversion:
                    return new Mod[]
                    {
                        new BmsModJudgeBeatoraja(),
                        new BmsModJudgeLr2(),
                        new BmsModChargeNote(),
                        new BmsModHellChargeNote(),
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

            var originalDifficulty = beatmapInfo.Difficulty;
            var adjustedDifficulty = GetAdjustedDisplayDifficulty(beatmapInfo, mods);

            yield return new RulesetBeatmapAttribute(SongSelectStrings.Accuracy, @"OD", originalDifficulty.OverallDifficulty, adjustedDifficulty.OverallDifficulty, 10)
            {
                Description = "Affects timing requirements for notes."
            };
        }

        public override Drawable? CreateBeatmapDetailsComponent(IBindable<WorkingBeatmap> beatmap) => new BmsNoteDistributionGraph(beatmap);

        public override IReadOnlyList<GroupMode> GetAvailableSongSelectGroupModes()
            => base.GetAvailableSongSelectGroupModes().Append(GroupMode.DifficultyTable).ToArray();

        public override IEnumerable<GroupDefinition> GetSongSelectGroupDefinitions(GroupMode mode, IBeatmapInfo beatmapInfo)
        {
            if (mode != GroupMode.DifficultyTable)
                return base.GetSongSelectGroupDefinitions(mode, beatmapInfo);

            return BmsTableGroupMode.GetGroupDefinitions(beatmapInfo);
        }

        public override void PrepareScoreInfoForResults(ScoreInfo score, IBeatmap playableBeatmap)
        {
            BmsScoreProcessor.GetLongNoteMode(score).ApplyToBeatmap(playableBeatmap);
            BmsJudgeModeExtensions.GetJudgeMode(score).ApplyToBeatmap(playableBeatmap);
            score.SetRulesetData(BmsClearLampProcessor.CreatePersistentData(score, playableBeatmap));
        }

        public override string GetScoreDisplayBucket(ScoreInfo score)
            => getScoreDisplayBucket(BmsJudgeModeExtensions.GetJudgeMode(score), BmsScoreProcessor.GetLongNoteMode(score));

        public override string GetScoreDisplayBucket(IReadOnlyList<Mod>? mods)
            => getScoreDisplayBucket(BmsJudgeModeExtensions.GetJudgeMode(mods), BmsScoreProcessor.GetLongNoteMode(mods));

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
                BmsJudgeModeExtensions.GetJudgeMode(score),
                BmsScoreProcessor.GetLongNoteMode(score),
                exScore,
                maxExScore,
                BmsScoreProcessor.GetEmptyPoorCount(score.Statistics),
                score.Accuracy,
                BmsDjLevelCalculator.Calculate(exScore, maxExScore),
                clearLamp);
        }

        public override LocalisableString GetDisplayNameForHitResult(HitResult result)
            => BmsHitResultDisplayNames.TryGetCustomDisplayName(result, out string? displayName) ? displayName : base.GetDisplayNameForHitResult(result);

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
            => $"judge-mode:{judgeMode.GetDisplayName()}|long-note-mode:{longNoteMode}";
    }
}
