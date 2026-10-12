// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps.Drawables.Cards.Buttons;
using osu.Game.Online;
using osu.Game.Overlays.BeatmapListing;

namespace osu.Game.Overlays
{
    public abstract partial class BeatmapDownloadCard : BeatmapBrowserCard
    {
        private readonly BeatmapDownloadCoverStore covers;
        private readonly Uri? coverUrl;
        private readonly CancellationTokenSource coverCancellation = new CancellationTokenSource();

        [Resolved]
        protected OsuGame Game { get; private set; } = null!;

        protected BeatmapDownloadCard(BeatmapDownloadCoverStore covers, Uri? coverUrl)
        {
            this.covers = covers;
            this.coverUrl = coverUrl;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            RebuildCharts();
            Refresh();
            if (coverUrl != null) loadCover(coverUrl);
        }

        private async void loadCover(Uri uri)
        {
            try
            {
                var texture = await covers.GetAsync(uri.AbsoluteUri, coverCancellation.Token).ConfigureAwait(false);
                if (coverCancellation.IsCancellationRequested) return;
                Schedule(() =>
                {
                    if (!IsDisposed) SetCover(texture);
                });
            }
            catch (OperationCanceledException)
            {
            }
        }

        protected abstract void RebuildCharts();
        public abstract void Refresh();

        protected override void Dispose(bool isDisposing)
        {
            coverCancellation.Cancel();
            coverCancellation.Dispose();
            base.Dispose(isDisposing);
        }
    }

    public partial class BeatmapDownloadCardButton : BeatmapCardIconButton
    {
        private IconUsage buttonIcon;

        public IconUsage ButtonIcon
        {
            get => buttonIcon;
            set
            {
                buttonIcon = value;
                if (IsLoaded) Icon.Icon = value;
            }
        }

        public BeatmapDownloadCardButton(IconUsage icon) => buttonIcon = icon;

        [BackgroundDependencyLoader]
        private void load() => Icon.Icon = buttonIcon;
    }
}
