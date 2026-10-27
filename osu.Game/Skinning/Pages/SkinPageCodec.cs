// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using osu.Game.Skinning.Gameplay;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Skinning.Pages
{
    /// <summary>One strict decoder shared by package preparation, rendering and the public author CLI.</summary>
    public static class SkinPageCodec
    {
        public static SkinPageDocument Decode(ReadOnlyMemory<byte> bytes)
        {
            if (bytes.Length == 0 || bytes.Length > SkinPageContracts.MAX_DOCUMENT_BYTES)
                throw invalid("document-budget");

            try
            {
                using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
                JsonElement root = json.RootElement;
                properties(root, "contract", "results", "songSelect");
                if (text(root, "contract") != SkinPageContracts.CONTRACT)
                    throw invalid("contract");
                SkinResultsPageDefinition? results = root.TryGetProperty("results", out JsonElement page) ? decodeResults(page) : null;
                SkinSongSelectPageDefinition? songSelect = root.TryGetProperty("songSelect", out JsonElement songPage) ? decodeSongSelect(songPage) : null;
                if (results == null && songSelect == null)
                    throw invalid("empty-document");
                var document = new SkinPageDocument(results, songSelect);
                if (document.Resources.Count > SkinPageContracts.MAX_RESOURCES
                    || document.Resources.Select(resource => resource.Id).Distinct(StringComparer.Ordinal).Count() != document.Resources.Count
                    || document.Resources.Select(resource => resource.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != document.Resources.Count)
                    throw invalid("resource");
                return document;
            }
            catch (JsonException error)
            {
                throw new InvalidDataException("OMS-SKIN-PAGE-json", error);
            }
            catch (KeyNotFoundException error)
            {
                throw new InvalidDataException("OMS-SKIN-PAGE-required-property", error);
            }
        }

        private static IReadOnlyList<SkinPageResource> decodeResources(JsonElement page)
        {
            JsonElement resources = array(page, "resources", SkinPageContracts.MAX_RESOURCES);
            var definitions = new List<SkinPageResource>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonElement resource in resources.EnumerateArray())
            {
                properties(resource, "id", "path", "filtering");
                string id = identifier(resource);
                string path = text(resource, "path");
                if (!ids.Add(id) || !paths.Add(path)
                    || !SkinPackageResourceNameValidator.TryNormalise(path, out string normalised, out _)
                    || !string.Equals(path, normalised, StringComparison.Ordinal)
                    || path.Length > 240
                    || Path.GetExtension(path).ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg" or ".webp"))
                    throw invalid("resource");
                string filtering = resource.TryGetProperty("filtering", out JsonElement filter) ? stringValue(filter) : "linear";
                if (filtering is not ("linear" or "nearest"))
                    throw invalid("filtering");
                definitions.Add(new SkinPageResource(id, path, filtering == "nearest"));
            }
            return definitions;
        }

        private static SkinSongSelectPageDefinition decodeSongSelect(JsonElement page)
        {
            properties(page, "resources", "background", "listEdge", "row", "selectedRow", "brand");
            var resources = decodeResources(page);
            var used = new HashSet<string>(StringComparer.Ordinal);
            string reference(string name)
            {
                string id = text(page, name);
                if (!resources.Any(resource => resource.Id == id))
                    throw invalid("resource-reference");
                used.Add(id);
                return id;
            }
            string background = reference("background");
            string edge = reference("listEdge");
            string row = reference("row");
            string selected = reference("selectedRow");
            string? brand = page.TryGetProperty("brand", out _) ? reference("brand") : null;
            if (used.Count != resources.Count)
                throw invalid("unused-resource");
            return new SkinSongSelectPageDefinition(resources, background, edge, row, selected, brand);
        }

        private static SkinResultsPageDefinition decodeResults(JsonElement page)
        {
            properties(page, "width", "height", "resources", "elements");
            double width = number(page, "width", 320, 3840);
            double height = number(page, "height", 240, 2160);
            JsonElement elements = array(page, "elements", SkinPageContracts.MAX_ELEMENTS);
            if (elements.GetArrayLength() == 0)
                throw invalid("empty-page");

            var resourceDefinitions = decodeResources(page);
            var resourceIds = resourceDefinitions.Select(resource => resource.Id).ToHashSet(StringComparer.Ordinal);

            var elementDefinitions = new List<SkinPageElement>();
            var elementIds = new HashSet<string>(StringComparer.Ordinal);
            var usedResources = new HashSet<string>(StringComparer.Ordinal);
            int textElements = 0;
            int nativeStatistics = 0;
            long glyphPixels = 0;
            foreach (JsonElement element in elements.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                    throw invalid("object");
                string type = text(element, "type");
                switch (type)
                {
                    case "sprite":
                        properties(element, "id", "type", "x", "y", "width", "height", "resource", "opacity", "colour", "visibleWhen");
                        break;
                    case "text":
                        properties(element, "id", "type", "x", "y", "width", "height", "source", "fontSize", "align", "opacity", "colour", "visibleWhen");
                        break;
                    case "native-statistics":
                        properties(element, "id", "type", "x", "y", "width", "height", "visibleWhen");
                        if (++nativeStatistics > 1)
                            throw invalid("statistics-count");
                        break;
                    default:
                        throw invalid("element-type");
                }

                string id = identifier(element);
                if (!elementIds.Add(id))
                    throw invalid("element-id");
                var rectangle = new SkinPageRectangle(number(element, "x", 0, width), number(element, "y", 0, height),
                    number(element, "width", 1, width), number(element, "height", 1, height));
                if (rectangle.X + rectangle.Width > width || rectangle.Y + rectangle.Height > height)
                    throw invalid("rectangle");
                double opacity = element.TryGetProperty("opacity", out _) ? number(element, "opacity", 0, 1) : 1;
                uint colour = 0xffffffff;
                if (element.TryGetProperty("colour", out JsonElement colourElement))
                {
                    string hex = stringValue(colourElement);
                    if (hex.Length != 8 || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out colour))
                        throw invalid("colour");
                }

                string? resource = null;
                string? source = null;
                double fontSize = 16;
                SkinPageTextAlignment alignment = SkinPageTextAlignment.Left;
                SkinPageVisibilityCondition? visibilityCondition = null;
                if (element.TryGetProperty("visibleWhen", out JsonElement visibleWhen))
                {
                    properties(visibleWhen, "source", "value");
                    string visibilitySource = text(visibleWhen, "source");
                    if (visibilitySource is not ("score.passed" or "score.cleared")
                        || !visibleWhen.TryGetProperty("value", out JsonElement expected)
                        || expected.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                        throw invalid("visibility");
                    visibilityCondition = new SkinPageVisibilityCondition(visibilitySource, expected.GetBoolean());
                }
                if (type == "sprite")
                {
                    resource = text(element, "resource");
                    if (!resourceIds.Contains(resource))
                        throw invalid("resource-reference");
                    usedResources.Add(resource);
                }
                else if (type == "text")
                {
                    source = text(element, "source");
                    if (!SkinResultsPageData.IsAllowedSource(source))
                        throw invalid("text-source");
                    fontSize = number(element, "fontSize", 1, 128);
                    string align = element.TryGetProperty("align", out JsonElement alignElement) ? stringValue(alignElement) : "left";
                    alignment = align switch
                    {
                        "left" => SkinPageTextAlignment.Left,
                        "centre" => SkinPageTextAlignment.Centre,
                        "right" => SkinPageTextAlignment.Right,
                        _ => throw invalid("alignment"),
                    };
                    glyphPixels += SkinPageContracts.MAX_TEXT_CHARACTERS * (long)Math.Pow(Math.Ceiling(fontSize) + 16, 2);
                    if (++textElements > SkinPageContracts.MAX_TEXT_ELEMENTS || glyphPixels > SkinPageContracts.MAX_TEXT_GLYPH_PIXELS)
                        throw invalid("text-budget");
                }
                elementDefinitions.Add(new SkinPageElement(id, type == "sprite" ? SkinPageElementType.Sprite
                    : type == "text" ? SkinPageElementType.Text : SkinPageElementType.NativeStatistics,
                    rectangle, resource, source, fontSize, colour, opacity, alignment, visibilityCondition));
            }
            if (usedResources.Count != resourceIds.Count)
                throw invalid("unused-resource");
            return new SkinResultsPageDefinition(width, height, resourceDefinitions, elementDefinitions);
        }

        internal static SkinPagePreparedPackage? Prepare(Func<string, int, byte[]?> read, string revision, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[]? bytes = read(SkinPageContracts.FILE_NAME, SkinPageContracts.MAX_DOCUMENT_BYTES);
            if (bytes == null)
                return null;
            SkinPageDocument document = Decode(bytes);
            var identified = new List<(SkinPageResource Source, byte[] Bytes, ImageInfo Info)>();
            long encoded = 0;
            long pixels = 0;
            foreach (SkinPageResource resource in document.Resources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] imageBytes = read(resource.Path, SkinPageContracts.MAX_RESOURCE_BYTES) ?? throw invalid("missing-resource");
                if (imageBytes.Length > SkinPageContracts.MAX_RESOURCE_BYTES)
                    throw invalid("resource-budget");
                ImageInfo info;
                try
                {
                    info = Image.Identify(new DecoderOptions { MaxFrames = 1 }, imageBytes) ?? throw invalid("texture");
                }
                catch (ImageFormatException error)
                {
                    throw new InvalidDataException("OMS-SKIN-PAGE-texture", error);
                }
                long area = (long)info.Width * info.Height;
                encoded += imageBytes.Length;
                pixels += area;
                if (info.Width <= 0 || info.Height <= 0 || info.Width > 8192 || info.Height > 8192
                    || area > SkinPageContracts.MAX_TOTAL_TEXTURE_PIXELS || pixels > SkinPageContracts.MAX_TOTAL_TEXTURE_PIXELS
                    || encoded > SkinPageContracts.MAX_TOTAL_RESOURCE_BYTES)
                    throw invalid("texture-budget");
                identified.Add((resource, imageBytes, info));
            }

            var captured = new List<GameplaySkinCapturedSceneResource>();
            foreach (var item in identified)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using Image<Rgba32> image = Image.Load<Rgba32>(new DecoderOptions { MaxFrames = 1 }, item.Bytes);
                    if (image.Width != item.Info.Width || image.Height != item.Info.Height)
                        throw invalid("texture-dimensions");
                    byte[] rgba = new byte[checked(image.Width * image.Height * 4)];
                    image.CopyPixelDataTo(rgba);
                    var source = new GameplaySkinSceneResource(item.Source.Id, GameplaySkinSceneResourceType.Texture, item.Source.Path,
                        item.Source.Nearest ? GameplaySkinSceneTextureFiltering.Nearest : GameplaySkinSceneTextureFiltering.Linear);
                    captured.Add(new GameplaySkinCapturedSceneResource(source, item.Bytes.Length,
                        Convert.ToHexString(SHA256.HashData(item.Bytes)).ToLowerInvariant(), image.Width, image.Height, rgba));
                }
                catch (ImageFormatException error)
                {
                    throw new InvalidDataException("OMS-SKIN-PAGE-texture", error);
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new SkinPagePreparedPackage(document, revision, captured);
        }

        private static void properties(JsonElement element, params string[] allowed)
        {
            if (element.ValueKind != JsonValueKind.Object)
                throw invalid("object");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
                if (!names.Add(property.Name) || !allowed.Contains(property.Name, StringComparer.Ordinal))
                    throw invalid("property");
        }

        private static string identifier(JsonElement element)
        {
            string id = text(element, "id");
            if (id.Length > 64 || id.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or '-')))
                throw invalid("identifier");
            return id;
        }

        private static string text(JsonElement element, string property) => stringValue(element.GetProperty(property));

        private static string stringValue(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.String)
                throw invalid("string");
            string value = element.GetString()!;
            if (value.Length == 0 || value.Any(char.IsControl))
                throw invalid("string");
            return value;
        }

        private static double number(JsonElement element, string property, double minimum, double maximum)
        {
            JsonElement value = element.GetProperty(property);
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out double number)
                || !double.IsFinite(number) || number < minimum || number > maximum)
                throw invalid("number");
            return number;
        }

        private static JsonElement array(JsonElement element, string property, int maximum)
        {
            JsonElement value = element.GetProperty(property);
            if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > maximum)
                throw invalid("array-budget");
            return value;
        }

        private static InvalidDataException invalid(string code) => new InvalidDataException($"OMS-SKIN-PAGE-{code}");
    }
}
