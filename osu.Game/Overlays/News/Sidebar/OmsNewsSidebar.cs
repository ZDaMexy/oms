// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osuTK;

namespace osu.Game.Overlays.News.Sidebar
{
    public partial class OmsNewsSidebar : OverlaySidebar
    {
        private readonly Action<int?> showYear;
        private readonly Action<OmsNewsPost> showArticle;
        private FillFlowContainer content = null!;

        public OmsNewsSidebar(Action<int?> showYear, Action<OmsNewsPost> showArticle)
        {
            this.showYear = showYear;
            this.showArticle = showArticle;
        }

        protected override Drawable CreateContent() => content = new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 20),
        };

        public void ClearPosts() => content.Clear();

        public void SetPosts(OmsNewsPost[] posts, int? selectedYear)
        {
            content.Clear();
            var years = new LinkFlowContainer(t => t.Font = OsuFont.GetFont(size: 13))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding(10),
            };
            years.AddLink("全部", () => showYear(null));
            foreach (int year in posts.Select(post => post.PublishedAt.Year).Distinct().OrderDescending())
            {
                years.AddText("   ");
                years.AddLink(year.ToString(), () => showYear(year));
            }
            content.Add(new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Masking = true,
                CornerRadius = 6,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Alpha = 0.08f },
                    years,
                },
            });
            var groups = posts.Where(post => selectedYear == null || post.PublishedAt.Year == selectedYear)
                              .GroupBy(post => (post.PublishedAt.Year, post.PublishedAt.Month))
                              .OrderByDescending(group => group.Key.Year).ThenByDescending(group => group.Key.Month);
            bool first = true;
            foreach (var group in groups)
            {
                content.Add(new MonthSection(group.Key.Month, group.Key.Year, group, showArticle)
                {
                    Expanded = { Value = first },
                });
                first = false;
            }
        }
    }
}
