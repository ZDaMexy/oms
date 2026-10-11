// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace osu.Game.Online.IR
{
    public static class OmsClientContent
    {
        public static void Validate(JObject content)
        {
            if (content["version"]?.Type != JTokenType.Integer || content["version"]!.ToString() != "1"
                || content["news"] is not JArray news || news.Count > 32 || content["help"] is not JArray help || help.Count > 32)
                invalid();
            var slugs = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (JToken item in (JArray)content["news"]!)
            {
                if (item is not JObject post || post["body"] is not JArray sections || sections.Count > 32) invalid();
                var article = (JObject)item;
                string slug = text(article, "slug", 100);
                if (!slug.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') || !slugs.Add(slug)) invalid();
                text(article, "title", 300);
                text(article, "published_at", 40);
                text(article, "excerpt", 2000);
                foreach (JToken section in (JArray)article["body"]!) validateSection(section);
            }
            var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (JToken section in (JArray)content["help"]!)
            {
                validateSection(section);
                string id = text((JObject)section, "id", 100);
                if (!id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') || !ids.Add(id)) invalid();
            }
        }

        private static void validateSection(JToken token)
        {
            if (token is not JObject) invalid();
            var section = (JObject)token;
            if (section["heading"] != null) text(section, "heading", 300);
            text(section, "text", 12000);
            if (section["links"] == null) return;
            if (section["links"] is not JArray links || links.Count > 16) invalid();
            foreach (JToken entry in (JArray)section["links"]!)
            {
                if (entry is not JObject) invalid();
                var link = (JObject)entry;
                text(link, "label", 300);
                string href = text(link, "href", 4096);
                if (!(href.StartsWith('/') && !href.StartsWith("//", StringComparison.Ordinal) && !href.Contains('\\'))
                    && !(Uri.TryCreate(href, UriKind.Absolute, out Uri? uri) && uri.Scheme == "https" && uri.UserInfo.Length == 0)) invalid();
            }
        }

        private static string text(JObject value, string key, int limit)
        {
            if (value[key]?.Type != JTokenType.String) invalid();
            string result = value[key]!.Value<string>()!;
            if (result.Length is 0 || result.Length > limit || Regex.IsMatch(result, @"<\s*/?\s*[A-Za-z][^>]*>")) invalid();
            return result;
        }

        private static void invalid() => throw new OmsIrException("invalid_content", "网站内容格式不符合约定，请稍后刷新。");
    }
}
