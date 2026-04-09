// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking.Statistics
{
    /// <summary>
    /// Shared stateful host for results-style panels that manage status text and accent-driven shell styling.
    /// </summary>
    public abstract partial class DefaultResultsPanelDisplay<TState> : CompositeDrawable
        where TState : class
    {
        private readonly LocalisableString title;
        private readonly LocalisableString defaultStatus;

        private DefaultResultsPanelContainer panel = null!;
        private FillFlowContainer content = null!;
        private TState? state;

        protected DefaultResultsPanelDisplay(LocalisableString title, LocalisableString defaultStatus)
        {
            this.title = title;
            this.defaultStatus = defaultStatus;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        protected DefaultResultsPanelContainer Panel => panel;

        protected virtual Color4 PanelBackgroundColour => Color4.Black;

        protected virtual float EmptyBackgroundAccentOpacity => 0.12f;

        protected virtual float FilledBackgroundAccentOpacity => 0.18f;

        protected virtual float EmptyBorderAccentOpacity => 0.24f;

        protected virtual float FilledBorderAccentOpacity => 0.34f;

        protected virtual float AccentBarOpacity => 0.9f;

        protected abstract Color4 TitleColour { get; }

        protected abstract Color4 StatusColour { get; }

        protected abstract void LoadContent(FillFlowContainer content);

        protected abstract void UpdateContent(TState? state);

        protected abstract bool HasContent(TState? state);

        protected abstract LocalisableString GetStatusText(TState? state);

        protected abstract Color4 GetAccentColour(TState? state);

        protected void SetPanelState(TState? state)
        {
            this.state = state;
            applyState();
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = panel = new DefaultResultsPanelContainer(title, defaultStatus);

            panel.TitleText.Colour = TitleColour;
            panel.StatusText.Colour = StatusColour;

            panel.Content.Add(content = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 10),
                Alpha = 0,
            });

            LoadContent(content);
            applyState();
        }

        private void applyState()
        {
            if (panel == null || content == null)
                return;

            UpdateContent(state);

            bool hasContent = HasContent(state);
            Color4 accentColour = GetAccentColour(state);

            panel.Background.Colour = ColourInfo.GradientVertical(
                PanelBackgroundColour,
                accentColour.Opacity(hasContent ? FilledBackgroundAccentOpacity : EmptyBackgroundAccentOpacity));
            panel.AccentBar.Colour = accentColour.Opacity(AccentBarOpacity);
            panel.Panel.BorderColour = accentColour.Opacity(hasContent ? FilledBorderAccentOpacity : EmptyBorderAccentOpacity);

            if (!hasContent)
            {
                content.Hide();
                panel.StatusText.Text = GetStatusText(state);
                panel.StatusText.Show();
                return;
            }

            content.Show();
            panel.StatusText.Hide();
        }
    }
}