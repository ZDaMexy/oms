// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Skinning.Pages
{
    /// <summary>Closed, optional author contract for non-gameplay pages in the same skin package.</summary>
    public static class SkinPageContracts
    {
        public const string FILE_NAME = "skin-pages.json";
        public const string CONTRACT = "oms-skin-pages.v1";
        public const int MAX_DOCUMENT_BYTES = 64 * 1024;
        public const int MAX_ELEMENTS = 128;
        public const int MAX_RESOURCES = 32;
        public const int MAX_TEXT_ELEMENTS = 32;
        public const int MAX_TEXT_CHARACTERS = 256;
        public const int MAX_RESOURCE_BYTES = 8 * 1024 * 1024;
        public const int MAX_TOTAL_RESOURCE_BYTES = 32 * 1024 * 1024;
        public const long MAX_TOTAL_TEXTURE_PIXELS = 16 * 1024 * 1024;
        public const long MAX_TEXT_GLYPH_PIXELS = 64 * 1024 * 1024;
    }

    public enum SkinPageElementType
    {
        Sprite,
        Text,
        NativeStatistics,
    }

    public enum SkinPageTextAlignment
    {
        Left,
        Centre,
        Right,
    }

    public readonly record struct SkinPageRectangle(double X, double Y, double Width, double Height);

    public readonly record struct SkinPageVisibilityCondition(string Source, bool Value);

    public sealed class SkinPageResource
    {
        public string Id { get; }
        public string Path { get; }
        public bool Nearest { get; }

        internal SkinPageResource(string id, string path, bool nearest)
        {
            Id = id;
            Path = path;
            Nearest = nearest;
        }
    }

    public sealed class SkinPageElement
    {
        public string Id { get; }
        public SkinPageElementType Type { get; }
        public SkinPageRectangle Rectangle { get; }
        public string? Resource { get; }
        public string? Source { get; }
        public double FontSize { get; }
        public uint Colour { get; }
        public double Opacity { get; }
        public SkinPageTextAlignment Alignment { get; }
        public SkinPageVisibilityCondition? VisibilityCondition { get; }

        internal SkinPageElement(string id, SkinPageElementType type, SkinPageRectangle rectangle, string? resource,
                                 string? source, double fontSize, uint colour, double opacity, SkinPageTextAlignment alignment, SkinPageVisibilityCondition? visibilityCondition)
        {
            Id = id;
            Type = type;
            Rectangle = rectangle;
            Resource = resource;
            Source = source;
            FontSize = fontSize;
            Colour = colour;
            Opacity = opacity;
            Alignment = alignment;
            VisibilityCondition = visibilityCondition;
        }
    }

    public sealed class SkinResultsPageDefinition
    {
        public double Width { get; }
        public double Height { get; }
        public IReadOnlyList<SkinPageResource> Resources { get; }
        public IReadOnlyList<SkinPageElement> Elements { get; }

        internal SkinResultsPageDefinition(double width, double height, IEnumerable<SkinPageResource> resources, IEnumerable<SkinPageElement> elements)
        {
            Width = width;
            Height = height;
            Resources = Array.AsReadOnly(resources.ToArray());
            Elements = Array.AsReadOnly(elements.ToArray());
        }
    }

    /// <summary>Art for existing native hosts only. No list, metadata, action or layout replacement.</summary>
    public sealed class SkinSongSelectPageDefinition
    {
        public IReadOnlyList<SkinPageResource> Resources { get; }
        public string Background { get; }
        public string ListEdge { get; }
        public string Row { get; }
        public string SelectedRow { get; }
        public string? Brand { get; }

        internal SkinSongSelectPageDefinition(IEnumerable<SkinPageResource> resources, string background, string listEdge,
                                             string row, string selectedRow, string? brand)
        {
            Resources = Array.AsReadOnly(resources.ToArray());
            Background = background;
            ListEdge = listEdge;
            Row = row;
            SelectedRow = selectedRow;
            Brand = brand;
        }
    }

    public sealed class SkinPageDocument
    {
        public SkinResultsPageDefinition? Results { get; }
        public SkinSongSelectPageDefinition? SongSelect { get; }
        public IReadOnlyList<SkinPageResource> Resources { get; }

        internal SkinPageDocument(SkinResultsPageDefinition? results, SkinSongSelectPageDefinition? songSelect = null)
        {
            Results = results;
            SongSelect = songSelect;
            Resources = Array.AsReadOnly((results?.Resources ?? Array.Empty<SkinPageResource>())
                .Concat(songSelect?.Resources ?? Array.Empty<SkinPageResource>()).ToArray());
        }
    }

    /// <summary>The page declaration and pixels captured from the exact same immutable package revision.</summary>
    internal sealed class SkinPagePreparedPackage
    {
        public SkinPageDocument Document { get; }
        public string ContentRevision { get; }
        public IReadOnlyList<GameplaySkinCapturedSceneResource> Resources { get; }

        internal SkinPagePreparedPackage(SkinPageDocument document, string contentRevision, IEnumerable<GameplaySkinCapturedSceneResource> resources)
        {
            Document = document;
            ContentRevision = contentRevision;
            Resources = Array.AsReadOnly(resources.ToArray());
        }
    }
}
