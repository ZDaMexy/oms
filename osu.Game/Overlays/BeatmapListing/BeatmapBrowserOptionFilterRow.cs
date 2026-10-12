// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;

namespace osu.Game.Overlays.BeatmapListing
{
    /// <summary>Source-supported options in lazer's compact, wrapping filter row.</summary>
    public partial class BeatmapBrowserOptionFilterRow<T> : BeatmapSearchFilterRow<T>
    {
        private OptionFilter filter = null!;

        public BeatmapBrowserOptionFilterRow(LocalisableString header, IEnumerable<T> values, Func<T, LocalisableString> label) : base(header)
        {
            filter.Label = label;
            filter.Clear();
            foreach (T value in values) filter.AddItem(value);
        }

        protected override Drawable CreateFilter() => filter = new OptionFilter();

        private partial class OptionFilter : BeatmapSearchFilter
        {
            public Func<T, LocalisableString> Label { get; set; } = null!;
            protected override TabItem<T> CreateTabItem(T value) => new OptionTab(value, item => Label(item));
        }

        private partial class OptionTab : FilterTabItem<T>
        {
            private readonly Func<T, LocalisableString> label;
            public OptionTab(T value, Func<T, LocalisableString> label) : base(value) => this.label = label;
            protected override LocalisableString LabelFor(T value) => label(value);
        }
    }
}
