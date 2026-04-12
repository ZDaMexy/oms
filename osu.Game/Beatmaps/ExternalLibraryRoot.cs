// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// Represents a user-registered external beatmap directory root.
    /// Persisted as part of <see cref="ExternalLibraryConfig"/>.
    /// </summary>
    public class ExternalLibraryRoot
    {
        public string Path { get; set; } = string.Empty;

        [JsonConverter(typeof(StringEnumConverter))]
        public ExternalLibraryRootType Type { get; set; }

        public bool Enabled { get; set; } = true;

        public DateTimeOffset? LastScanTime { get; set; }
    }

    public enum ExternalLibraryRootType
    {
        BMS,
        Mania,
    }
}
