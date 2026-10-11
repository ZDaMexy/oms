// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using Markdig.Syntax;
using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Containers.Markdown;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers.Markdown;
using osu.Game.Online.API;
using osu.Game.Online.IR;
using osu.Game.Overlays.Wiki.Markdown;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Wiki
{
    public partial class WikiPanelContainer : CompositeDrawable
    {
        private const float padding = 3;

        private readonly string text;
        private readonly bool isFullWidth;
        private readonly Action<string> openLink;

        public WikiPanelContainer(string text, bool isFullWidth = false, Action<string> openLink = null)
        {
            this.text = text;
            this.isFullWidth = isFullWidth;
            this.openLink = openLink;
        }

        private PanelBackground background;

        [BackgroundDependencyLoader]
        private void load(IAPIProvider api)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            InternalChildren = new Drawable[]
            {
                background = new PanelBackground
                {
                    BypassAutoSizeAxes = Axes.Both
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding(padding),
                    Child = new WikiPanelMarkdownContainer(isFullWidth, openLink)
                    {
                        CurrentPath = openLink == null ? $@"{api.Endpoints.WebsiteUrl}/wiki/" : new Uri(OmsIrService.ServiceOrigin, "help").AbsoluteUri,
                        Text = text,
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y
                    }
                }
            };
        }

        protected override void Update()
        {
            base.Update();
            background.Size = Parent!.DrawSize * new Vector2(Size.X, 1);
        }

        private partial class PanelBackground : CompositeDrawable
        {
            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                Padding = new MarginPadding(padding);
                InternalChild = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 4,
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Shadow,
                        Colour = Color4.Black.Opacity(25),
                        Offset = new Vector2(0, 1),
                        Radius = 3,
                    },
                    Child = new Box
                    {
                        Colour = colourProvider.Background4,
                        RelativeSizeAxes = Axes.Both,
                    }
                };
            }
        }

        private partial class WikiPanelMarkdownContainer : OmsMarkdownContainer
        {
            private readonly bool isFullWidth;
            private readonly Action<string> openLink;

            public WikiPanelMarkdownContainer(bool isFullWidth, Action<string> openLink)
                : base(openLink)
            {
                this.isFullWidth = isFullWidth;
                this.openLink = openLink;

                LineSpacing = 0;
                DocumentPadding = new MarginPadding(30);
                DocumentMargin = new MarginPadding(0);
            }

            public override SpriteText CreateSpriteText() => base.CreateSpriteText().With(t => t.Font = t.Font.With(Typeface.Torus, weight: FontWeight.Bold));

            public override OsuMarkdownTextFlowContainer CreateTextFlow() => base.CreateTextFlow().With(f => f.TextAnchor = Anchor.TopCentre);

            protected override MarkdownParagraph CreateParagraph(ParagraphBlock paragraphBlock, int level)
                => base.CreateParagraph(paragraphBlock, level).With(p => p.Margin = new MarginPadding { Bottom = 10 });

            protected override MarkdownHeading CreateHeading(HeadingBlock headingBlock) => new WikiPanelHeading(headingBlock, openLink)
            {
                IsFullWidth = isFullWidth,
            };
        }

        private partial class WikiPanelHeading : OsuMarkdownHeading
        {
            public bool IsFullWidth;
            private readonly Action<string> openLink;
            private readonly int level;

            public WikiPanelHeading(HeadingBlock headingBlock, Action<string> openLink)
                : base(headingBlock)
            {
                this.openLink = openLink;
                level = headingBlock.Level;
                Margin = new MarginPadding { Bottom = 40 };
            }

            public override MarkdownTextFlowContainer CreateTextFlow() => (openLink == null
                ? base.CreateTextFlow()
                : new OmsPanelHeadingTextFlow(openLink, GetFontSizeByLevel(level), GetFontWeightByLevel(level))).With(f =>
            {
                f.Anchor = Anchor.TopCentre;
                f.Origin = Anchor.TopCentre;
                f.TextAnchor = Anchor.TopCentre;
            });

            protected override FontWeight GetFontWeightByLevel(int level) => FontWeight.Light;

            protected override float GetFontSizeByLevel(int level) => base.GetFontSizeByLevel(IsFullWidth ? level : 3);
        }

        private partial class OmsPanelHeadingTextFlow : OmsMarkdownContainer.OmsMarkdownTextFlow
        {
            private readonly float fontSize;
            private readonly FontWeight fontWeight;

            public OmsPanelHeadingTextFlow(Action<string> openLink, float fontSize, FontWeight fontWeight)
                : base(openLink)
            {
                this.fontSize = fontSize;
                this.fontWeight = fontWeight;
            }

            protected override SpriteText CreateSpriteText()
                => base.CreateSpriteText().With(text => text.Font = text.Font.With(Typeface.Torus, size: fontSize, weight: fontWeight));
        }
    }
}
