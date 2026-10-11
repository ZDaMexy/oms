// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Resources.Localisation.Web;

namespace osu.Game.Overlays.News
{
    public partial class NewsHeader : BreadcrumbControlOverlayHeader
    {
        public static LocalisableString FrontPageString => NewsStrings.IndexTitleInfo;

        public Action ShowFrontPage;

        private readonly Bindable<string> article = new Bindable<string>();
        private bool changingOmsPage;

        public NewsHeader()
        {
            TabControl.AddItem(FrontPageString);

            article.BindValueChanged(onArticleChanged, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Current.BindValueChanged(e =>
            {
                if (!changingOmsPage && e.NewValue == FrontPageString)
                    ShowFrontPage?.Invoke();
            });
        }

        public void SetFrontPage() => article.Value = null;

        public void SetArticle(string slug) => article.Value = slug;

        public void SetOmsPage(string title = null)
        {
            ((NewsHeaderTitle)Title).UseOmsTitle();
            changingOmsPage = true;
            article.Value = title;
            changingOmsPage = false;
        }

        private void onArticleChanged(ValueChangedEvent<string> e)
        {
            if (e.OldValue != null)
                TabControl.RemoveItem(e.OldValue);

            if (e.NewValue != null)
            {
                TabControl.AddItem(e.NewValue);
                Current.Value = e.NewValue;
            }
            else
            {
                Current.Value = FrontPageString;
            }
        }

        protected override Drawable CreateBackground() => new OverlayHeaderBackground(@"Headers/news");

        protected override OverlayTitle CreateTitle() => new NewsHeaderTitle();

        private partial class NewsHeaderTitle : OverlayTitle
        {
            public NewsHeaderTitle()
            {
                Title = PageTitleStrings.MainNewsControllerDefault;
                Description = NamedOverlayComponentStrings.NewsDescription;
                Icon = OsuIcon.News;
            }

            public void UseOmsTitle()
            {
                Title = "新闻";
                Description = "OMS 新闻与发行说明";
            }
        }
    }
}
