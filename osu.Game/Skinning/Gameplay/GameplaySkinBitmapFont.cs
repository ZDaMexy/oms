// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Graphics.Textures;
using osu.Framework.Text;
using osuTK;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>Fixed limits for author-provided numeric glyphs, independent of the system font atlas.</summary>
    public static class GameplaySkinBitmapFontBudgets
    {
        public const int MAX_FONTS = 8;
        public const int MAX_GLYPHS_PER_FONT = 16;
        public const int MAX_TEXT_CHARACTERS = 64;
        public const int MAX_GLYPH_DIMENSION = 512;
        public const double MIN_LINE_HEIGHT = 1;
        public const double MAX_LINE_HEIGHT = 512;
        public const double MIN_ADVANCE = 1;
        public const double MAX_ADVANCE = 512;

        public static bool IsAllowedCharacter(char character)
            => character is >= '0' and <= '9' or '.' or ',' or '%' or '+' or '-' or 'x';
    }

    /// <summary>One explicitly named existing texture, with its horizontal advance in source pixels.</summary>
    public sealed class GameplaySkinBitmapGlyph
    {
        public char Character { get; }

        public string ResourceId { get; }

        public double Advance { get; }

        internal GameplaySkinBitmapGlyph(char character, string resourceId, double advance)
        {
            Character = character;
            ResourceId = resourceId;
            Advance = advance;
        }
    }

    /// <summary>
    /// Immutable manifest declaration. The scene codec validates its IDs, unique numeric characters and finite,
    /// positive metrics before construction. Glyphs are top-aligned; there is no font file or fallback font.
    /// </summary>
    public sealed class GameplaySkinBitmapFont
    {
        public string Id { get; }

        public double LineHeight { get; }

        public IReadOnlyList<GameplaySkinBitmapGlyph> Glyphs { get; }

        private readonly bool[] declaredCharacters = new bool[128];

        internal GameplaySkinBitmapFont(string id, double lineHeight, IEnumerable<GameplaySkinBitmapGlyph> glyphs)
        {
            Id = id;
            LineHeight = lineHeight;
            GameplaySkinBitmapGlyph[] copiedGlyphs = glyphs.ToArray();
            Glyphs = Array.AsReadOnly(copiedGlyphs);

            foreach (GameplaySkinBitmapGlyph glyph in copiedGlyphs)
                declaredCharacters[glyph.Character] = true;
        }

        /// <summary>Used by the codec for literal text and by the runtime before assigning a bound value.</summary>
        internal bool CanRender(ReadOnlySpan<char> text)
        {
            if (text.Length > GameplaySkinBitmapFontBudgets.MAX_TEXT_CHARACTERS)
                return false;

            foreach (char character in text)
            {
                if (character >= declaredCharacters.Length || !declaredCharacters[character])
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Prepare-time numeric glyph lookup. Every glyph retains the exact scene resource texture; ownership and
    /// retirement stay with the prepared scene. This store neither opens resources nor creates an atlas.
    /// </summary>
    internal sealed class GameplaySkinPreparedBitmapFont : ITexturedGlyphLookupStore
    {
        public GameplaySkinBitmapFont Source { get; }

        private readonly ITexturedCharacterGlyph?[] glyphs = new ITexturedCharacterGlyph?[128];
        private readonly Task<ITexturedCharacterGlyph?>?[] glyphTasks = new Task<ITexturedCharacterGlyph?>?[128];
        private readonly float[] imageWidths = new float[128];
        private readonly float[] advances = new float[128];

        private static readonly Task<ITexturedCharacterGlyph?> absent_glyph = Task.FromResult<ITexturedCharacterGlyph?>(null);

        internal GameplaySkinPreparedBitmapFont(
            GameplaySkinBitmapFont source,
            IReadOnlyDictionary<string, GameplaySkinPreparedSceneResource> resources)
        {
            Source = source;

            foreach (GameplaySkinBitmapGlyph declaration in source.Glyphs)
            {
                GameplaySkinPreparedSceneResource resource = GameplaySkinPreparedSceneVariantCase.RequireTexture(declaration.ResourceId, resources);
                Texture texture = resource.Texture!;

                if (texture.Width <= 0 || texture.Height <= 0
                    || texture.Width > GameplaySkinBitmapFontBudgets.MAX_GLYPH_DIMENSION
                    || texture.Height > GameplaySkinBitmapFontBudgets.MAX_GLYPH_DIMENSION
                    || texture.Height > source.LineHeight)
                {
                    throw new GameplaySkinScenePreparationException(GameplaySkinSceneDiagnosticCode.InvalidResource);
                }

                var glyph = new TexturedCharacterGlyph(
                    new CharacterGlyph(declaration.Character, 0, 0, (float)declaration.Advance, (float)source.LineHeight, null),
                    texture, 1f / (float)source.LineHeight);
                glyphs[declaration.Character] = glyph;
                glyphTasks[declaration.Character] = Task.FromResult<ITexturedCharacterGlyph?>(glyph);
                imageWidths[declaration.Character] = texture.Width;
                advances[declaration.Character] = (float)declaration.Advance;
            }
        }

        /// <summary>Includes glyph image overhang while retaining the renderer's unchanged advances.</summary>
        internal Vector2 MeasureText(ReadOnlySpan<char> text, float fontSize)
        {
            float cursor = 0;
            float right = 0;
            foreach (char character in text)
            {
                right = Math.Max(right, cursor + imageWidths[character]);
                cursor += advances[character];
            }

            return new Vector2(Math.Max(cursor, right) * fontSize / (float)Source.LineHeight, fontSize);
        }

        public ITexturedCharacterGlyph? Get(string? fontName, char character)
        {
            if (fontName != Source.Id)
                return null;

            if (character >= glyphs.Length || glyphs[character] == null)
                throw new InvalidOperationException("An author bitmap font does not declare this character.");

            return glyphs[character];
        }

        public Task<ITexturedCharacterGlyph?> GetAsync(string fontName, char character)
        {
            if (fontName != Source.Id)
                return absent_glyph;

            // The synchronous path owns missing-character failure; no task or background work is created here.
            Get(fontName, character);
            return glyphTasks[character]!;
        }
    }
}
