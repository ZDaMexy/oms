// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace osu.Game.Skinning.Gameplay
{
    public enum GameplaySkinBgaScaleMode
    {
        Fit,
        Fill,
        Stretch,
    }

    /// <summary>
    /// An immutable, validated author declaration for BGA windows, relative to the gameplay safe bounds.
    /// The ruleset solver must still check these requested windows against the actual playfield and HUD.
    /// </summary>
    public sealed class GameplaySkinBgaLayout
    {
        public const string CONFIGURATION_KEY = "BgaViewports";

        public IReadOnlyList<GameplaySkinLayoutRect> Viewports { get; }

        public IReadOnlyList<GameplaySkinBgaScaleMode> ScaleModes { get; }

        private GameplaySkinBgaLayout(GameplaySkinLayoutRect[] viewports, GameplaySkinBgaScaleMode[] scaleModes)
        {
            Viewports = Array.AsReadOnly(viewports);
            ScaleModes = Array.AsReadOnly(scaleModes);
        }

        /// <summary>
        /// Parses <c>x,y,width,height,fit|fill|stretch;...</c>, or <c>none</c> for no windows.
        /// Shared by the package decoder and authoring checker, without a ruleset dependency or a second INI tokenizer.
        /// </summary>
        public static bool TryParse(string value, out GameplaySkinBgaLayout? layout, out string? diagnosticCode)
        {
            ArgumentNullException.ThrowIfNull(value);
            layout = null;
            diagnosticCode = null;

            if (value.Trim() == "none")
            {
                layout = new GameplaySkinBgaLayout(Array.Empty<GameplaySkinLayoutRect>(), Array.Empty<GameplaySkinBgaScaleMode>());
                return true;
            }

            string[] windows = value.Split(';', StringSplitOptions.TrimEntries);

            if (windows.Length > GameplaySkinEventBudgets.MAX_BGA_VIEWPORTS)
            {
                diagnosticCode = "bms.layout.bga-viewports.budget-exceeded";
                return false;
            }

            var viewports = new GameplaySkinLayoutRect[windows.Length];
            var modes = new GameplaySkinBgaScaleMode[windows.Length];

            for (int i = 0; i < windows.Length; i++)
            {
                string[] fields = windows[i].Split(',', StringSplitOptions.TrimEntries);

                if (fields.Length != 5)
                {
                    diagnosticCode = "bms.layout.bga-viewports.invalid-format";
                    return false;
                }

                if (!tryNumber(fields[0], out float x) || !tryNumber(fields[1], out float y)
                    || !tryNumber(fields[2], out float width) || !tryNumber(fields[3], out float height))
                {
                    diagnosticCode = "bms.layout.bga-viewports.invalid-number";
                    return false;
                }

                if (x < 0 || y < 0 || width <= 0 || height <= 0 || x + width > 1 || y + height > 1)
                {
                    diagnosticCode = "bms.layout.bga-viewports.invalid-bounds";
                    return false;
                }

                GameplaySkinBgaScaleMode? mode = fields[4] switch
                {
                    "fit" => GameplaySkinBgaScaleMode.Fit,
                    "fill" => GameplaySkinBgaScaleMode.Fill,
                    "stretch" => GameplaySkinBgaScaleMode.Stretch,
                    _ => null,
                };

                if (mode == null)
                {
                    diagnosticCode = "bms.layout.bga-viewports.invalid-mode";
                    return false;
                }

                viewports[i] = GameplaySkinLayoutRect.Create(x, y, width, height);
                modes[i] = mode.Value;
            }

            layout = new GameplaySkinBgaLayout(viewports, modes);
            return true;
        }

        private static bool tryNumber(string text, out float value)
            => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && float.IsFinite(value);
    }
}
