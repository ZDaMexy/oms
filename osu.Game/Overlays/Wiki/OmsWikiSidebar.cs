// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using Newtonsoft.Json.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.Overlays.Wiki
{
    public partial class OmsWikiSidebar : OverlaySidebar
    {
        private FillFlowContainer content = null!;

        public OmsWikiSidebar(JObject[] sections, string selected, Action<string> openLink)
        {
            content.Add(new OsuSpriteText
            {
                Text = "帮助章节",
                Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                Margin = new MarginPadding { Bottom = 10 },
            });
            foreach (JObject section in sections)
            {
                var link = new LinkFlowContainer(t => t.Font = OsuFont.GetFont(size: 15,
                    weight: section["id"]!.Value<string>() == selected ? FontWeight.SemiBold : FontWeight.Regular))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                };
                link.AddLink(section["heading"]!.Value<string>()!, () => openLink("/help#" + section["id"]!.Value<string>()));
                content.Add(link);
            }
        }

        protected override Drawable CreateContent() => content = new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 8),
        };
    }
}
