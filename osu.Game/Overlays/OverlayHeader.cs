// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays
{
    public abstract partial class OverlayHeader : Container
    {
        public OverlayTitle Title { get; }

        private float contentSidePadding;

        /// <summary>
        /// Horizontal padding of the header content.
        /// </summary>
        protected float ContentSidePadding
        {
            get => contentSidePadding;
            set
            {
                contentSidePadding = value;
                content.Padding = new MarginPadding
                {
                    Horizontal = value
                };
            }
        }

        private readonly Box titleBackground;
        private readonly Container content;
        private FillFlowContainer? pageActions;

        public void SetPageActions(Action back, Action refresh, Action website)
        {
            ClearPageActions();
            Title.Margin = new MarginPadding { Right = 120 };
            content.Add(pageActions = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Spacing = new Vector2(8, 0),
                Children = new Drawable[]
                {
                    new IconButton { Icon = FontAwesome.Solid.ArrowLeft, TooltipText = "返回", Action = back },
                    new IconButton { Icon = FontAwesome.Solid.Redo, TooltipText = "刷新", Action = refresh },
                    new IconButton { Icon = FontAwesome.Solid.ExternalLinkAlt, TooltipText = "在网页查看", Action = website },
                },
            });
        }

        public void ClearPageActions()
        {
            if (pageActions != null) content.Remove(pageActions, true);
            pageActions = null;
            Title.Margin = new MarginPadding();
        }

        protected readonly FillFlowContainer HeaderInfo;

        protected OverlayHeader()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            Add(new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Children = new[]
                {
                    HeaderInfo = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Depth = -float.MaxValue,
                        Children = new[]
                        {
                            CreateBackground(),
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Children = new Drawable[]
                                {
                                    titleBackground = new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = Color4.Gray,
                                    },
                                    content = new Container
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Child = Title = CreateTitle().With(title =>
                                        {
                                            title.Anchor = Anchor.CentreLeft;
                                            title.Origin = Anchor.CentreLeft;
                                        }),
                                    }
                                }
                            },
                        }
                    },
                    CreateContent()
                }
            });

            ContentSidePadding = WaveOverlayContainer.HORIZONTAL_PADDING;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            titleBackground.Colour = colourProvider.Dark5;
        }

        protected virtual Drawable CreateContent() => Empty();

        protected virtual Drawable CreateBackground() => Empty();

        protected abstract OverlayTitle CreateTitle();
    }
}
