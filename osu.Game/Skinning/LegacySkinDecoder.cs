// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Globalization;
using osu.Game.Beatmaps.Formats;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Skinning
{
    public class LegacySkinDecoder : LegacyDecoder<SkinConfiguration>
    {
        public LegacySkinDecoder()
            : base(1)
        {
        }

        protected override void ParseLine(SkinConfiguration skin, Section section, string line)
            => parsePair(skin, section, SplitKeyVal(line));

        protected override void ParseLine(SkinConfiguration skin, Section section, GameplaySkinLegacyLine line)
        {
            if (TryGetRetainedKeyValue(line, out KeyValuePair<string, string> pair))
                parsePair(skin, section, pair);
        }

        private void parsePair(SkinConfiguration skin, Section section, KeyValuePair<string, string> pair)
        {
            if (section != Section.Colours)
            {
                switch (section)
                {
                    case Section.General:
                        switch (pair.Key)
                        {
                            case @"Name":
                                skin.SkinInfo.Name = pair.Value;
                                return;

                            case @"Author":
                                skin.SkinInfo.Creator = pair.Value;
                                return;

                            case @"Version":
                                if (pair.Value == "latest")
                                    skin.LegacyVersion = SkinConfiguration.LATEST_VERSION;
                                else if (decimal.TryParse(pair.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal version))
                                    skin.LegacyVersion = version;

                                return;
                        }

                        break;

                    // osu!catch section only has colour settings
                    // so no harm in handling the entire section
                    case Section.CatchTheBeat:
                        HandleColours(skin, pair, true);
                        return;
                }

                if (!string.IsNullOrEmpty(pair.Key))
                    skin.ConfigDictionary[pair.Key] = pair.Value;

                return;
            }

            HandleColours(skin, pair, false);
        }

        protected override SkinConfiguration CreateTemplateObject()
        {
            var config = base.CreateTemplateObject();
            config.LegacyVersion = 1.0m;
            return config;
        }
    }
}
