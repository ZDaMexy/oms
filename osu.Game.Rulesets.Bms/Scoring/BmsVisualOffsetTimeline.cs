// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace osu.Game.Rulesets.Bms.Scoring
{
    /// <summary>Recorded visual state, independent of the replay's input and judgement times.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class BmsVisualOffsetTimeline
    {
        [JsonProperty("initial")]
        public double InitialOffset { get; set; }

        [JsonProperty("changes")]
        public List<Change> Changes { get; set; } = new List<Change>();

        [JsonProperty("automatic", NullValueHandling = NullValueHandling.Ignore)]
        public AutomaticState? Automatic { get; set; }

        public void Record(double time, double offset)
        {
            // A live rewind replaces the future of this recording rather than appending out-of-order changes.
            while (Changes.Count > 0 && Changes[^1].Time >= time)
                Changes.RemoveAt(Changes.Count - 1);

            if (offset != (Changes.Count == 0 ? InitialOffset : Changes[^1].Offset))
                Changes.Add(new Change { Time = time, Offset = offset });
        }

        public double OffsetAt(double time)
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

            return lower == 0 ? InitialOffset : Changes[lower - 1].Offset;
        }

        public void Validate()
        {
            if (!validOffset(InitialOffset) || Changes == null)
                throw new InvalidDataException("Invalid BMS replay visual offset.");

            double previousTime = double.NegativeInfinity;
            foreach (var change in Changes)
            {
                if (change == null || !double.IsFinite(change.Time) || change.Time <= previousTime || !validOffset(change.Offset))
                    throw new InvalidDataException("Invalid BMS replay visual offset timeline.");

                previousTime = change.Time;
            }
            Automatic?.Validate();
        }

        private static bool validOffset(double value) => double.IsFinite(value) && Math.Abs(value) <= 500;

        /// <summary>The real automatic mode, independent of numeric offset changes.</summary>
        [JsonObject(MemberSerialization.OptIn)]
        public class AutomaticState
        {
            [JsonProperty("initial")]
            public bool? Initial { get; set; }

            [JsonProperty("changes")]
            public List<AutomaticChange> Changes { get; set; } = new List<AutomaticChange>();

            internal bool Record(double time, bool enabled)
            {
                if (!double.IsFinite(time) || !Initial.HasValue)
                    throw new InvalidOperationException("Recording automatic offset mode requires a real initial state and finite gameplay time.");

                bool changed = false;
                while (Changes.Count > 0 && Changes[^1].Time >= time)
                {
                    Changes.RemoveAt(Changes.Count - 1);
                    changed = true;
                }
                if (enabled != (Changes.Count == 0 ? Initial : Changes[^1].Enabled))
                {
                    Changes.Add(new AutomaticChange { Time = time, Enabled = enabled });
                    changed = true;
                }
                return changed;
            }

            public bool? At(double time)
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
                return lower == 0 ? Initial : Changes[lower - 1].Enabled;
            }

            public void Validate()
            {
                if (!Initial.HasValue || Changes == null)
                    throw new InvalidDataException("A BMS automatic-offset history requires its actual initial mode and changes.");
                double previousTime = double.NegativeInfinity;
                foreach (var change in Changes)
                {
                    if (change == null || !change.Enabled.HasValue || !double.IsFinite(change.Time) || change.Time <= previousTime)
                        throw new InvalidDataException("Invalid BMS automatic-offset mode timeline.");
                    previousTime = change.Time;
                }
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        public class AutomaticChange
        {
            [JsonProperty("time", Required = Required.Always)]
            public double Time { get; set; }

            [JsonProperty("enabled")]
            public bool? Enabled { get; set; }
        }

        [JsonObject(MemberSerialization.OptIn)]
        public class Change
        {
            [JsonProperty("time")]
            public double Time { get; set; }

            [JsonProperty("offset")]
            public double Offset { get; set; }
        }
    }
}
