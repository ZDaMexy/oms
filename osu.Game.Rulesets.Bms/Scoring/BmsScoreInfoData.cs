// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace osu.Game.Rulesets.Bms.Scoring
{
    [JsonObject(MemberSerialization.OptIn)]
    public class BmsScoreInfoData
    {
        public const int EMPTY_POOR_SEPARATION_VERSION = 6;

        [JsonProperty("version")]
        public int Version { get; set; } = EMPTY_POOR_SEPARATION_VERSION;

        [JsonProperty("gauge_auto_shift")]
        public bool UsesGaugeAutoShift { get; set; }

        [JsonProperty("starting_gauge_type")]
        [JsonConverter(typeof(StringEnumConverter))]
        public BmsGaugeType StartingGaugeType { get; set; } = BmsGaugeType.Normal;

        [JsonProperty("floor_gauge_type")]
        [JsonConverter(typeof(StringEnumConverter))]
        public BmsGaugeType FloorGaugeType { get; set; } = BmsGaugeType.Normal;

        [JsonProperty("gauge_type")]
        [JsonConverter(typeof(StringEnumConverter))]
        public BmsGaugeType GaugeType { get; set; } = BmsGaugeType.Normal;

        [JsonProperty("gauge_rules_family")]
        [JsonConverter(typeof(StringEnumConverter))]
        public BmsGaugeRulesFamily GaugeRulesFamily { get; set; } = BmsGaugeRulesFamily.Legacy;

        [JsonProperty("long_note_mode")]
        [JsonConverter(typeof(StringEnumConverter))]
        public BmsLongNoteMode LongNoteMode { get; set; } = BmsLongNoteMode.LN;

        [JsonProperty("judge_mode")]
        [JsonConverter(typeof(StringEnumConverter))]
        public BmsJudgeMode JudgeMode { get; set; } = BmsJudgeMode.OD;

        [JsonProperty("final_gauge")]
        public double? FinalGauge { get; set; }

        [JsonProperty("clear_lamp")]
        public BmsClearLamp? ClearLamp { get; set; }

        [JsonIgnore]
        public bool HasResultStatistics => FinalGauge.HasValue && ClearLamp.HasValue;
    }
}
