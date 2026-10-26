// Copyright (c) OMS contributors. Licensed under the MIT Licence.

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>Read-only OMS NEXT policy over a matched personal best, independent of skin options.</summary>
    internal static class GameplaySkinPacemakerNext
    {
        private static readonly string[] labels = { "LV A", "LV A+", "LV AA", "LV AA+", "LV AAA", "LV AAA+", "DJ AUTO" };

        public static GameplaySkinPacemakerNextTarget Calculate(bool usesExScore, long maximum, long? personalBest)
        {
            if (!usesExScore || maximum <= 0 || personalBest is not long best || best < 0 || best > maximum)
                return new GameplaySkinPacemakerNextTarget(0, 0, string.Empty, false);

            for (int numerator = 12; numerator < 18; numerator++)
            {
                // Split the multiplication before taking the ceiling. Every intermediate and the final
                // threshold fit in Int64 even at long.MaxValue; no floating point selects the next level.
                long threshold = maximum / 18 * numerator + (maximum % 18 * numerator + 17) / 18;
                if (threshold > best)
                    return new GameplaySkinPacemakerNextTarget(threshold, (double)threshold / maximum, labels[numerator - 12], true);
            }

            return new GameplaySkinPacemakerNextTarget(maximum, 1, labels[6], true);
        }
    }

    internal readonly struct GameplaySkinPacemakerNextTarget
    {
        public long Value { get; }
        public double Progress { get; }
        public string Label { get; }
        public bool Available { get; }

        public GameplaySkinPacemakerNextTarget(long value, double progress, string label, bool available)
        {
            Value = value;
            Progress = progress;
            Label = label;
            Available = available;
        }
    }
}
