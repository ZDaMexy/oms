// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using osu.Framework.Allocation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays.Wiki.Markdown;

namespace osu.Game.Overlays.Wiki
{
    public partial class WikiMainPage : FillFlowContainer
    {
        public string Markdown;
        private readonly IReadOnlyList<JObject> omsSections;
        private readonly Action<string> openOmsLink;

        public WikiMainPage()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        public WikiMainPage(IReadOnlyList<JObject> sections, Action<string> openLink)
            : this()
        {
            omsSections = sections;
            openOmsLink = openLink;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (omsSections != null)
            {
                createOmsPanels();
                return;
            }
            var html = new HtmlDocument();
            html.LoadHtml(Markdown);

            var panels = createPanels(html).ToArray();

            Children = new Drawable[]
            {
                createBlurb(html),
                new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    RowDimensions = Enumerable.Repeat(new Dimension(GridSizeMode.AutoSize), panels.Length).ToArray(),
                    Content = panels,
                },
            };
        }

        private void createOmsPanels()
        {
            var panels = new List<Drawable[]>();
            for (int index = 0; index < omsSections.Count; index += 2)
            {
                panels.Add(new Drawable[]
                {
                    createOmsPanel(omsSections[index]),
                    index + 1 < omsSections.Count ? createOmsPanel(omsSections[index + 1]) : null,
                });
            }
            Children = new Drawable[]
            {
                new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(Typeface.Inter, size: 16))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Text = "选择章节，查看 OMS 安装、谱库、玩法与在线服务的使用帮助。",
                    TextAnchor = Anchor.TopCentre,
                    Padding = new MarginPadding { Vertical = 30 },
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    RowDimensions = Enumerable.Repeat(new Dimension(GridSizeMode.AutoSize), panels.Count).ToArray(),
                    Content = panels.ToArray(),
                },
            };
        }

        private WikiPanelContainer createOmsPanel(JObject section)
        {
            string title = section["heading"].Value<string>();
            string link = "/help#" + section["id"].Value<string>();
            string summary = section["text"].Value<string>().Split('\n')[0];
            if (summary.Length > 220)
                summary = summary[..220] + "…";
            return new WikiPanelContainer("## " + OmsContentMarkdown.Link(title, link) + "\n\n"
                                          + OmsContentMarkdown.Escape(summary) + "\n\n" + OmsContentMarkdown.Link("阅读此章节", link),
                openLink: openOmsLink);
        }

        private Container createBlurb(HtmlDocument html)
        {
            var blurbNode = html.DocumentNode.SelectSingleNode("//div[contains(@class, 'wiki-main-page__blurb')]");

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding
                {
                    Vertical = 30,
                },
                Child = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(Typeface.Inter, size: 12, weight: FontWeight.Light))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Text = blurbNode.InnerText,
                    TextAnchor = Anchor.TopCentre,
                }
            };
        }

        private IEnumerable<Drawable[]> createPanels(HtmlDocument html)
        {
            var panelsNode = html.DocumentNode.SelectNodes("//div[contains(@class, 'wiki-main-page-panel')]").ToArray();

            Debug.Assert(panelsNode.Length > 1);

            int i = 0;

            while (i < panelsNode.Length)
            {
                bool isFullWidth = panelsNode[i].HasClass("wiki-main-page-panel--full");

                if (isFullWidth)
                {
                    yield return new Drawable[]
                    {
                        new WikiPanelContainer(panelsNode[i++].InnerText, true)
                        {
                            // This is required to fill up the space of "null" drawable below.
                            Width = 2,
                        },
                        null,
                    };
                }
                else
                {
                    yield return new Drawable[]
                    {
                        new WikiPanelContainer(panelsNode[i++].InnerText),
                        i < panelsNode.Length ? new WikiPanelContainer(panelsNode[i++].InnerText) : null,
                    };
                }
            }
        }
    }
}
