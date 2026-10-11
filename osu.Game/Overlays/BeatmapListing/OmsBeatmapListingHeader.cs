// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Localisation;

namespace osu.Game.Overlays.BeatmapListing
{
    /// <summary>Search the OMS source-chart catalogue within the shared beatmap browser.</summary>
    public partial class OmsBeatmapListingHeader : BeatmapBrowserHeader
    {
        public Action<string>? SearchRequested { get; set; }

        protected override LocalisableString SearchPlaceholder => "搜索曲名、艺术家或原谱 MD5…";

        protected override IEnumerable<Drawable> CreateBrowserFilters() => Array.Empty<Drawable>();

        protected override void CommitSearch() => SearchRequested?.Invoke(Query.Value);
    }
}
