// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using osu.Game.Beatmaps;
using osu.Game.Scoring;

namespace osu.Game.Online.IR
{
    /// <summary>
    /// The immutable chart identity and actual playable conditions captured for a new Player.
    /// Final score fields are read only after the local import has succeeded.
    /// </summary>
    public sealed class OmsIrSubmissionContext
    {
        private readonly JObject chart;
        private readonly int? bmsJudgeRank;

        public string Ruleset { get; }

        public string Keymode { get; }

        public string ChartMd5 { get; }

        public string ChartSha256 { get; }

        public OmsIrSubmissionContext(string ruleset, string keymode, IBeatmapInfo sourceBeatmapInfo, int? bmsJudgeRank = null)
        {
            Ruleset = ruleset;
            Keymode = keymode;
            ChartMd5 = sourceBeatmapInfo.MD5Hash.ToLowerInvariant();
            ChartSha256 = sourceBeatmapInfo.Hash.ToLowerInvariant();

            if (ChartMd5.Length != 32 || ChartSha256.Length != 64 || !ChartMd5.All(Uri.IsHexDigit) || !ChartSha256.All(Uri.IsHexDigit))
                throw new InvalidOperationException("原谱内容身份不完整，本局只能保存在本地。");

            this.bmsJudgeRank = bmsJudgeRank;

            chart = new JObject
            {
                ["md5"] = ChartMd5,
                ["sha256"] = ChartSha256,
                ["title"] = sourceBeatmapInfo.Metadata.Title,
                ["artist"] = sourceBeatmapInfo.Metadata.Artist,
                ["difficulty"] = sourceBeatmapInfo.DifficultyName,
            };
        }

        /// <summary>
        /// Creates a detached payload from the final, successfully imported score.
        /// Unsupported mods and parameters remain present so a service rejection can explain the pending play.
        /// </summary>
        public OmsIrSubmission Create(ScoreInfo savedScore)
        {
            if (savedScore.Ruleset.ShortName != Ruleset || !string.Equals(savedScore.BeatmapHash, ChartSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The saved score does not belong to this captured play.");

            JObject? bmsResult = Ruleset == "bms" ? createBmsResult(savedScore) : null;

            var payload = new JObject
            {
                ["schema_version"] = 1,
                ["submission_id"] = savedScore.ID.ToString("D"),
                ["ruleset"] = Ruleset,
                ["chart"] = chart.DeepClone(),
                ["keymode"] = Keymode,
                ["played_at"] = savedScore.Date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                ["client_version"] = savedScore.ClientVersion,
                ["total_score_version"] = savedScore.TotalScoreVersion,
                ["total_score"] = savedScore.TotalScore,
                ["accuracy"] = savedScore.Accuracy,
                ["max_combo"] = savedScore.MaxCombo,
                // Local completion can be true while a finished normal-gauge play has a FAILED lamp.
                ["passed"] = bmsResult != null ? bmsResult.Value<int>("clear_lamp") >= 2 : savedScore.Passed,
                ["statistics"] = JObject.FromObject(savedScore.Statistics.Where(pair => pair.Value != 0).ToDictionary(pair => pair.Key, pair => pair.Value)),
                ["maximum_statistics"] = JObject.FromObject(savedScore.MaximumStatistics.Where(pair => pair.Value != 0).ToDictionary(pair => pair.Key, pair => pair.Value)),
                ["mods"] = new JArray(savedScore.APIMods.Select(mod => new JObject
                {
                    ["acronym"] = mod.Acronym,
                    ["settings"] = JObject.FromObject(mod.Settings),
                })),
                ["ruleset_data"] = bmsResult != null ? bmsResult : JValue.CreateNull(),
                ["bms_chart"] = bmsJudgeRank.HasValue
                    ? new JObject { ["judge_rank"] = bmsJudgeRank.Value, ["branch_policy"] = "fixed-1-v1" }
                    : JValue.CreateNull(),
            };

            return new OmsIrSubmission(savedScore.ID, payload);
        }

        private static JObject createBmsResult(ScoreInfo score)
        {
            if (string.IsNullOrEmpty(score.RulesetDataJson))
                throw new InvalidOperationException("BMS IR requires the final prepared score.");

            var finalData = JObject.Parse(score.RulesetDataJson);

            if (finalData["clear_lamp"]?.Type is null or JTokenType.Null || finalData["final_gauge"]?.Type is null or JTokenType.Null)
                throw new InvalidOperationException("BMS IR requires the final clear lamp and gauge.");

            var result = new JObject();

            // The persisted BMS data also contains local visual-offset history, which is not an IR field.
            foreach (string field in new[]
                     {
                         "version", "gauge_type", "gauge_rules_family", "gauge_auto_shift", "starting_gauge_type",
                         "floor_gauge_type", "long_note_mode", "judge_mode", "clear_lamp", "final_gauge",
                     })
            {
                result[field] = finalData[field]?.DeepClone()
                                ?? throw new InvalidOperationException($"The final BMS score is missing {field}.");
            }

            return result;
        }
    }
}
