// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using osu.Game.Rulesets.Bms.Configuration;

namespace osu.Game.Rulesets.Bms.Scoring
{
    /// <summary>Completed reading adjustments on the replay's gameplay clock, separate from scoring input.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class BmsReadingSettingsTimeline
    {
        [JsonProperty("changes")]
        public List<Change> Changes { get; set; } = new List<Change>();

        internal void Record(double time, BmsReadingSettingsSnapshot settings, BmsReadingSettingsSnapshot initial)
        {
            if (!double.IsFinite(time) || Changes.Count > 0 && time < Changes[^1].Time)
                throw new InvalidOperationException("BMS reading adjustments must be recorded in gameplay-time order.");

            // Multiple completed operations can share one paused/preparation frame. Its final state owns that time.
            if (Changes.Count > 0 && Changes[^1].Time == time)
                Changes.RemoveAt(Changes.Count - 1);

            if (settings != (Changes.Count == 0 ? initial : Changes[^1].Settings))
                Changes.Add(new Change { Time = time, Settings = settings });
        }

        public BmsReadingSettingsSnapshot SettingsAt(double time, BmsReadingSettingsSnapshot initial)
        {
            int lower = 0;
            int upper = Changes.Count;
            while (lower < upper)
            {
                int middle = lower + (upper - lower) / 2;
                if (Changes[middle].Time <= time)
                    lower = middle + 1;
                else
                    upper = middle;
            }

            return lower == 0 ? initial : Changes[lower - 1].Settings;
        }

        public void Validate()
        {
            if (Changes == null)
                throw new InvalidDataException("Invalid BMS replay reading-settings timeline.");

            double previousTime = double.NegativeInfinity;
            foreach (var change in Changes)
            {
                if (change == null || !double.IsFinite(change.Time) || change.Time <= previousTime)
                    throw new InvalidDataException("BMS replay reading adjustments must have finite, strictly increasing times.");

                change.Settings.Validate();
                previousTime = change.Time;
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        public class Change
        {
            [JsonProperty("time")]
            public double Time { get; set; }

            [JsonProperty("settings")]
            public BmsReadingSettingsSnapshot Settings { get; set; }
        }
    }
}
