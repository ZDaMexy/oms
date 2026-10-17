// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Globalization;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Text;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Numeric scene text using only the author's already-prepared textures. Numeric changes allocate at most
    /// one bounded string; unchanged values allocate nothing. Missing glyphs are a component fault, never a
    /// system-font fallback.
    /// </summary>
    internal sealed partial class GameplaySkinBitmapText : OsuSpriteText
    {
        private readonly GameplaySkinPreparedBitmapFont bitmapFont;

        private bool loadCompleted;

        /// <summary>
        /// The requested text, also available before drawable loading. The base SpriteText stays empty until
        /// LoadComplete so its loader cannot pre-cache these characters in the global system-font atlas.
        /// </summary>
        internal string AuthorText { get; private set; } = string.Empty;

        internal Vector2 NaturalSize => bitmapFont.MeasureText(AuthorText.AsSpan(), Font.Size);

        internal GameplaySkinBitmapText(GameplaySkinPreparedBitmapFont bitmapFont, float fontSize)
        {
            this.bitmapFont = bitmapFont;
            Font = new FontUsage(bitmapFont.Source.Id, fontSize);
            Shadow = false;
            UseFullGlyphHeight = true;
            AllowMultiline = false;
        }

        internal void SetAuthorText(string value)
        {
            if (!bitmapFont.Source.CanRender(value.AsSpan()))
                throw new InvalidOperationException("Author bitmap text exceeds its character budget or references a missing glyph.");

            if (AuthorText == value)
                return;

            AuthorText = value;

            if (loadCompleted)
                Text = value;
        }

        internal void SetNumber(double value, string format)
        {
            ReadOnlySpan<char> numericFormat = format switch
            {
                "percent" or "integer" => "0",
                "fixed-2" => "0.00",
                "" => "0.###",
                _ => throw new InvalidOperationException("This format cannot be used with an author bitmap font."),
            };

            if (format == "percent")
                value *= 100;

            Span<char> buffer = stackalloc char[GameplaySkinBitmapFontBudgets.MAX_TEXT_CHARACTERS];

            if (!double.IsFinite(value) || !value.TryFormat(buffer, out int written, numericFormat, CultureInfo.InvariantCulture))
                throw new InvalidOperationException("A bitmap text number exceeds its finite, bounded display range.");

            if (format == "percent")
            {
                if (written == buffer.Length)
                    throw new InvalidOperationException("A bitmap percentage exceeds its character budget.");

                buffer[written++] = '%';
            }

            ReadOnlySpan<char> display = buffer[..written];

            if (!bitmapFont.Source.CanRender(display))
                throw new InvalidOperationException("A bound bitmap text number references a missing author glyph.");

            if (display.SequenceEqual(AuthorText.AsSpan()))
                return;

            AuthorText = new string(display);

            if (loadCompleted)
                Text = AuthorText;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            loadCompleted = true;
            Text = AuthorText;
        }

        protected override TextBuilder CreateTextBuilder(ITexturedGlyphLookupStore store)
            => base.CreateTextBuilder(bitmapFont);
    }
}
