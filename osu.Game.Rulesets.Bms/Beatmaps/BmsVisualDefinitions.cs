// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    public enum BmsPoorBgaMode
    {
        Default = 0,
        Overlay = 1,
        Undisplayed = 2,
    }

    /// <summary>
    /// A parsed #BGAxx definition that trims and repositions a bitmap reference.
    /// </summary>
    public readonly record struct BmsBgaDefinition
    {
        public int Index { get; }

        public string BitmapReference { get; }

        public int SourceX1 { get; }

        public int SourceY1 { get; }

        public int SourceX2 { get; }

        public int SourceY2 { get; }

        public int DestinationX { get; }

        public int DestinationY { get; }

        public BmsBgaDefinition(int index, string bitmapReference, int sourceX1, int sourceY1, int sourceX2, int sourceY2, int destinationX, int destinationY)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), @"BGA definition index must be zero or greater.");

            if (string.IsNullOrWhiteSpace(bitmapReference))
                throw new ArgumentException(@"Bitmap reference must not be empty.", nameof(bitmapReference));

            Index = index;
            BitmapReference = bitmapReference;
            SourceX1 = sourceX1;
            SourceY1 = sourceY1;
            SourceX2 = sourceX2;
            SourceY2 = sourceY2;
            DestinationX = destinationX;
            DestinationY = destinationY;
        }
    }

    /// <summary>
    /// A parsed #@BGAxx definition that trims and repositions a bitmap reference using width / height syntax.
    /// </summary>
    public readonly record struct BmsAtBgaDefinition
    {
        public int Index { get; }

        public string BitmapReference { get; }

        public int SourceX { get; }

        public int SourceY { get; }

        public int Width { get; }

        public int Height { get; }

        public int DestinationX { get; }

        public int DestinationY { get; }

        public BmsAtBgaDefinition(int index, string bitmapReference, int sourceX, int sourceY, int width, int height, int destinationX, int destinationY)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), @"@BGA definition index must be zero or greater.");

            if (string.IsNullOrWhiteSpace(bitmapReference))
                throw new ArgumentException(@"Bitmap reference must not be empty.", nameof(bitmapReference));

            Index = index;
            BitmapReference = bitmapReference;
            SourceX = sourceX;
            SourceY = sourceY;
            Width = width;
            Height = height;
            DestinationX = destinationX;
            DestinationY = destinationY;
        }
    }

    /// <summary>
    /// A parsed #ARGBxx definition applied to a BGA layer family.
    /// </summary>
    public readonly record struct BmsArgbDefinition
    {
        public int Index { get; }

        public int Alpha { get; }

        public int Red { get; }

        public int Green { get; }

        public int Blue { get; }

        public BmsArgbDefinition(int index, int alpha, int red, int green, int blue)
        {
            if (index <= 0)
                throw new ArgumentOutOfRangeException(nameof(index), @"ARGB definition index must be greater than zero.");

            validateColourComponent(alpha, nameof(alpha));
            validateColourComponent(red, nameof(red));
            validateColourComponent(green, nameof(green));
            validateColourComponent(blue, nameof(blue));

            Index = index;
            Alpha = alpha;
            Red = red;
            Green = green;
            Blue = blue;
        }

        private static void validateColourComponent(int value, string parameterName)
        {
            if (value is < 0 or > 255)
                throw new ArgumentOutOfRangeException(parameterName, @"Colour component must be in the range [0, 255].");
        }
    }

    /// <summary>
    /// A parsed #SWBGAxx definition for key-bound visual animation.
    /// </summary>
    public readonly record struct BmsSwBgaDefinition
    {
        public int Index { get; }

        public int FrameDurationMilliseconds { get; }

        public int TotalDurationMilliseconds { get; }

        public int LineChannel { get; }

        public bool Loop { get; }

        public int Alpha { get; }

        public int Red { get; }

        public int Green { get; }

        public int Blue { get; }

        public string Pattern { get; }

        public BmsSwBgaDefinition(int index, int frameDurationMilliseconds, int totalDurationMilliseconds, int lineChannel, bool loop, int alpha, int red, int green, int blue, string pattern)
        {
            if (index <= 0)
                throw new ArgumentOutOfRangeException(nameof(index), @"SWBGA definition index must be greater than zero.");

            if (frameDurationMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(frameDurationMilliseconds), @"Frame duration must be greater than zero.");

            if (totalDurationMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(totalDurationMilliseconds), @"Total duration must be zero or greater.");

            if (lineChannel <= 0)
                throw new ArgumentOutOfRangeException(nameof(lineChannel), @"Line channel must be greater than zero.");

            if (string.IsNullOrWhiteSpace(pattern))
                throw new ArgumentException(@"Animation pattern must not be empty.", nameof(pattern));

            BmsArgbDefinition definition = new BmsArgbDefinition(1, alpha, red, green, blue);

            Index = index;
            FrameDurationMilliseconds = frameDurationMilliseconds;
            TotalDurationMilliseconds = totalDurationMilliseconds;
            LineChannel = lineChannel;
            Loop = loop;
            Alpha = definition.Alpha;
            Red = definition.Red;
            Green = definition.Green;
            Blue = definition.Blue;
            Pattern = pattern;
        }
    }

    /// <summary>
    /// A combined view over same-index visual definition headers.
    /// </summary>
    public readonly record struct BmsVisualDefinitionProjection
    {
        public int Index { get; }

        public BmsBgaDefinition? BgaDefinition { get; }

        public BmsAtBgaDefinition? AtBgaDefinition { get; }

        public BmsArgbDefinition? ArgbDefinition { get; }

        public BmsSwBgaDefinition? SwBgaDefinition { get; }

        public BmsPoorBgaMode? PoorBgaMode { get; }

        public BmsVisualDefinitionProjection(int index, BmsBgaDefinition? bgaDefinition, BmsAtBgaDefinition? atBgaDefinition, BmsArgbDefinition? argbDefinition, BmsSwBgaDefinition? swBgaDefinition, BmsPoorBgaMode? poorBgaMode)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), @"Visual definition index must be zero or greater.");

            Index = index;
            BgaDefinition = bgaDefinition;
            AtBgaDefinition = atBgaDefinition;
            ArgbDefinition = argbDefinition;
            SwBgaDefinition = swBgaDefinition;
            PoorBgaMode = poorBgaMode;
        }
    }
}
