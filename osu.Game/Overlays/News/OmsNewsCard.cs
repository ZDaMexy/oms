// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.IR;
using osu.Game.Utils;

namespace osu.Game.Overlays.News
{
    public sealed record OmsNewsPost(string Slug, string Title, string Excerpt, DateTimeOffset PublishedAt, JArray Body)
    {
        public static OmsNewsPost FromJson(JObject post)
        {
            if (!DateTimeOffset.TryParseExact(post["published_at"]!.Value<string>(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                             DateTimeStyles.AssumeUniversal, out DateTimeOffset published))
                throw new OmsIrException("invalid_content", "新闻发布日期不符合约定，请稍后刷新。");
            return new OmsNewsPost(post["slug"]!.Value<string>()!, post["title"]!.Value<string>()!,
                post["excerpt"]!.Value<string>()!, published, (JArray)post["body"]!);
        }
    }

    public partial class OmsNewsCard : OsuHoverContainer
    {
        protected override IEnumerable<Drawable> EffectTargets => new[] { background };

        private readonly OmsNewsPost post;
        private readonly Box background;
        private readonly OsuTextFlowContainer text;
        private readonly OsuSpriteText date;

        public OmsNewsCard(OmsNewsPost post, Action openArticle)
            : base(HoverSampleSet.Button)
        {
            this.post = post;
            Action = openArticle;
            TooltipText = "阅读新闻";
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Masking = true;
            CornerRadius = 6;
            Children = new Drawable[]
            {
                background = new Box { RelativeSizeAxes = Axes.Both },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding { Horizontal = 20, Vertical = 18 },
                    Children = new Drawable[]
                    {
                        date = new OsuSpriteText
                        {
                            Text = post.PublishedAt.ToLocalisedMediumDate(),
                            Font = OsuFont.GetFont(size: 12),
                            Margin = new MarginPadding { Bottom = 12 },
                        },
                        text = new OsuTextFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                        },
                    },
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            IdleColour = colours.Background4;
            HoverColour = colours.Background3;
            date.Colour = colours.Light2;
            text.AddParagraph(post.Title, t => t.Font = OsuFont.GetFont(size: 24, weight: FontWeight.SemiBold));
            text.AddParagraph(post.Excerpt, t => t.Font = OsuFont.GetFont(size: 16));
        }
    }
}
