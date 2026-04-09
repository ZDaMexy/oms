// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using osu.Framework.Extensions;
using osu.Game.Audio;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Bms.Audio
{
    public sealed class BmsKeysoundSampleInfo : HitSampleInfo, IEquatable<BmsKeysoundSampleInfo>
    {
        private const string internal_sample_name = "__oms_bms_keysound__";

        public string Filename { get; }

        public BmsKeysoundSampleInfo(string filename, int volume = 100)
            : this(normaliseFilename(filename), volume, true)
        {
        }

        private BmsKeysoundSampleInfo(string normalisedFilename, int volume, bool _)
            : base(internal_sample_name, volume: volume, useBeatmapSamples: true)
        {
            Filename = normalisedFilename;
        }

        public override IEnumerable<string> LookupNames
        {
            get
            {
                yield return Filename;

                string filenameWithoutExtension = Path.ChangeExtension(Filename, null)?.ToStandardisedPath() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(filenameWithoutExtension) && !string.Equals(filenameWithoutExtension, Filename, StringComparison.OrdinalIgnoreCase))
                    yield return filenameWithoutExtension;
            }
        }

        public sealed override HitSampleInfo With(Optional<string> newName = default, Optional<string> newBank = default, Optional<string?> newSuffix = default, Optional<int> newVolume = default,
                                                  Optional<bool> newEditorAutoBank = default, Optional<bool> newUseBeatmapSamples = default)
            => new BmsKeysoundSampleInfo(Filename, newVolume.GetOr(Volume));

        public bool Equals(BmsKeysoundSampleInfo? other)
            => other != null && base.Equals(other) && StringComparer.OrdinalIgnoreCase.Equals(Filename, other.Filename);

        public override bool Equals(object? obj)
            => obj is BmsKeysoundSampleInfo other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(base.GetHashCode(), StringComparer.OrdinalIgnoreCase.GetHashCode(Filename));

        public static bool TryCreate(string? filename, [NotNullWhen(true)] out BmsKeysoundSampleInfo? sampleInfo, int volume = 100)
        {
            sampleInfo = null;

            if (!TryNormaliseFilename(filename, out string? normalisedFilename))
                return false;

            sampleInfo = new BmsKeysoundSampleInfo(normalisedFilename, volume, true);
            return true;
        }

        public static bool TryNormaliseFilename(string? filename, [NotNullWhen(true)] out string? normalisedFilename)
        {
            normalisedFilename = null;

            if (string.IsNullOrWhiteSpace(filename) || FilesystemSanityCheckHelpers.IncursPathTraversalRisk(filename))
                return false;

            string standardisedPath = filename.ToStandardisedPath().Trim('/');

            if (string.IsNullOrWhiteSpace(standardisedPath))
                return false;

            normalisedFilename = standardisedPath;
            return true;
        }

        private static string normaliseFilename(string filename)
        {
            if (TryNormaliseFilename(filename, out string? normalisedFilename))
                return normalisedFilename;

            throw new ArgumentException("BMS keysound filenames must resolve to a relative path inside the beatmap directory.", nameof(filename));
        }
    }
}
