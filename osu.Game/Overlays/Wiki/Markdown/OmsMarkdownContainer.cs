// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Text;
using Markdig.Syntax.Inlines;
using Newtonsoft.Json.Linq;
using osu.Game.Graphics.Containers.Markdown;
using osu.Game.Online.IR;

namespace osu.Game.Overlays.Wiki.Markdown
{
    /// <summary>Native markdown presentation for the website's validated plain-text content.</summary>
    public partial class OmsMarkdownContainer : WikiMarkdownContainer
    {
        private readonly Action<string>? openLink;

        public OmsMarkdownContainer(Action<string>? openLink = null)
        {
            this.openLink = openLink;
            if (openLink != null)
                RootUrl = DocumentUrl = OmsIrService.ServiceOrigin.AbsoluteUri;
        }

        public override OsuMarkdownTextFlowContainer CreateTextFlow()
            => openLink == null ? base.CreateTextFlow() : new OmsMarkdownTextFlow(openLink);

        public partial class OmsMarkdownTextFlow : OsuMarkdownTextFlowContainer
        {
            private readonly Action<string> openLink;

            public OmsMarkdownTextFlow(Action<string> openLink)
            {
                this.openLink = openLink;
            }

            protected override void AddLinkText(string text, LinkInline linkInline)
                => AddDrawable(new OmsMarkdownLink(text, linkInline, openLink));

            private partial class OmsMarkdownLink : OsuMarkdownLinkText
            {
                private readonly Action<string> openLink;
                private readonly string href;

                public OmsMarkdownLink(string text, LinkInline linkInline, Action<string> openLink)
                    : base(text, linkInline)
                {
                    this.openLink = openLink;
                    href = linkInline.Url!;
                }

                protected override void OnLinkPressed() => openLink(href);
            }
        }
    }

    public static class OmsContentMarkdown
    {
        public static string Escape(string text)
        {
            var result = new StringBuilder(text.Length);
            foreach (char character in text.Replace("\r\n", "\n").Replace('\r', '\n'))
            {
                if (character is >= '!' and <= '/' or >= ':' and <= '@' or >= '[' and <= '`' or >= '{' and <= '~')
                    result.Append('\\');
                result.Append(character);
            }
            return result.ToString().Replace("\n", "  \n");
        }

        public static string Link(string label, string url)
            => "[" + Escape(label) + "](<" + url.Replace("<", "%3C").Replace(">", "%3E") + ">)";

        public static string Paragraphs(JArray paragraphs)
        {
            var result = new StringBuilder();
            foreach (JObject section in paragraphs)
                result.Append(Section(section)).Append("\n\n");
            return result.ToString();
        }

        public static string Section(JObject section)
        {
            var result = new StringBuilder();
            if (section["heading"]?.Type == JTokenType.String)
                result.Append("## ").Append(Escape(section["heading"]!.Value<string>()!)).Append("\n\n");
            result.Append(Escape(section["text"]!.Value<string>()!));
            if (section["links"] is JArray links)
            {
                result.Append("\n\n");
                foreach (JObject link in links)
                    result.Append(Link(link["label"]!.Value<string>()!, link["href"]!.Value<string>()!)).Append("  \n");
            }
            return result.ToString();
        }
    }
}
