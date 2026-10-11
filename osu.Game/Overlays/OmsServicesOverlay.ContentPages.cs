// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.IR;

namespace osu.Game.Overlays
{
    public partial class OmsServicesOverlay
    {
        private async Task<Action> LoadChartsAsync(OmsPageRoute requested, CancellationToken token)
        {
            JObject response = await service.GetSourceChartsAsync(requested.Query, requested.Page, token).ConfigureAwait(false);
            var charts = Array(response, "items").Select(item => item as JObject ?? throw new JsonSerializationException("Invalid chart entry.")).ToArray();
            foreach (JObject entry in charts)
            {
                string md5 = String(Object(entry, "chart"), "md5");
                if (md5.Length != 32 || !md5.All(Uri.IsHexDigit) || String(entry, "ruleset") is not ("bms" or "mania"))
                    throw new JsonSerializationException("Invalid OMS chart identity.");
                NullableChartText(Object(entry, "chart"), "title");
                NullableChartText(Object(entry, "chart"), "artist");
                NullableChartText(Object(entry, "chart"), "difficulty");
            }
            if (charts.Length > 20 || Integer(response, "page") != requested.Page || Integer(response, "limit") != 20)
                throw new JsonSerializationException("Invalid chart pagination.");
            return () =>
            {
                AddText("谱面榜", 26);
                AddText("输入曲名、作者或原谱 MD5，查看已有公开成绩与历史目录。", 16);
                var query = new OsuTextBox { RelativeSizeAxes = Axes.X, Height = 40, Text = requested.Query, LengthLimit = 200, PlaceholderText = "曲名 / 作者 / 原谱 MD5" };
                rows.Add(query);
                AddAction("搜索", () => ReplaceRoute(requested with { Query = query.Text, Page = 1 }));
                if (charts.Length == 0) AddText("没有找到已收录谱面。可修改搜索，或前往谱面下载。", 16);
                foreach (JObject entry in charts)
                {
                    JObject chart = Object(entry, "chart");
                    string title = NullableChartText(chart, "title") ?? String(chart, "md5");
                    AddAction(title, () => Navigate(new OmsPageRoute
                    { Kind = OmsPageKind.Chart, Md5 = String(chart, "md5"), Title = title, Ruleset = String(entry, "ruleset") }));
                    AddText((String(entry, "ruleset") == "bms" ? "BMS" : "mania") + " · " + (NullableChartText(chart, "artist") ?? "—") + " · " + (NullableChartText(chart, "difficulty") ?? "—"), 15);
                }
                AddPagination(requested, Integer(response, "total"), Integer(response, "limit"));
            };
        }

        private void RenderLibrary(OmsPageRoute requested)
        {
            AddText("查找与下载谱面", 26);
            AddText("谱包由原站提供。下载完成后沿用客户端现有的入库流程。", 16);
            var query = new OsuTextBox { RelativeSizeAxes = Axes.X, Height = 40, Text = requested.Query, LengthLimit = 200, PlaceholderText = "曲名 / 作者 / BMS MD5（可留空浏览）" };
            rows.Add(query);
            AddAction("浏览 BMS（Ginger Rush / 616）", () => game?.ShowOmsChartDownload(query.Text, true));
            AddAction("浏览 mania（Sayobot）", () => game?.ShowOmsChartDownload(query.Text, false));
        }

        private void RenderAccount()
        {
            AddText("OMS 账号", 26);
            AddAction("注册 / 登录 / 退出", () => game?.ShowOmsAccount());
            if (service.State.Account is OmsIrAccount account)
            {
                AddAction("我的公开资料", () => Navigate((game?.CurrentOmsRoute(OmsPageKind.Profile) ?? new OmsPageRoute { Kind = OmsPageKind.Profile }) with { UserId = account.Id }));
                AddAction("我的完整游玩记录", () => game?.ShowOwnOmsProfile());
            }
            AddText("其他播放器的专用密钥在网页账号页管理。网页与客户端需要分别登录。", 16);
            AddAction("在网页创建 / 撤销播放器密钥", () => game?.OpenUrlExternally(new Uri(new Uri(service.State.ServiceAddress), "account").AbsoluteUri));
        }

        private async Task<Action> LoadChartAsync(OmsPageRoute requested, CancellationToken token)
        {
            JObject? response = null;
            try { response = await service.GetSourceChartAsync(requested.Md5!, token).ConfigureAwait(false); }
            catch (OmsIrException exception) when (exception.Code == "chart_not_found" && exception.StatusCode == HttpStatusCode.NotFound) { }
            JObject? chart = response == null ? null : Object(response, "chart");
            if (response != null && (String(chart!, "md5") != requested.Md5 || String(response, "ruleset") != requested.Ruleset))
                throw new JsonSerializationException("OMS chart identity mismatch.");
            return () =>
            {
                string title = (chart == null ? null : NullableChartText(chart, "title")) ?? requested.Title ?? requested.Md5!;
                AddText(title, 26);
                AddText(requested.Ruleset == "bms" ? "BMS 谱面" : "原生 mania 谱面");
                AddText("原谱 MD5：" + requested.Md5, 15);
                if (chart != null)
                {
                    foreach (string name in new[] { "artist", "difficulty" })
                        if (chart[name]?.Type == JTokenType.String) AddText((name == "artist" ? "作者：" : "难度：") + String(chart, name));
                    AddAction("查看成绩榜", () => Navigate(requested with { Kind = OmsPageKind.Board, Title = title }));
                }
                else AddText("这张谱面尚未收录成绩或历史榜。可在本地游玩，或主动查找批准源的原包。", 16);
                if (requested.Ruleset == "bms")
                    AddAction("在端内查找该 MD5 的 BMS 原包", () => game?.ShowOmsChartDownload(requested.Md5!, true));
                else
                {
                    AddText("镜像目录未提供原谱 MD5 反查。按歌名查找只用于寻找候选，不能确认是同一张谱面。", 15);
                    if (title != requested.Md5) AddAction("在端内按歌名查找 mania 原包", () => game?.ShowOmsChartDownload(title, false));
                }
            };
        }

        private static string? NullableChartText(JObject chart, string field) => chart[field]?.Type switch
        {
            JTokenType.Null => null,
            JTokenType.String => chart[field]!.Value<string>(),
            _ => throw new JsonSerializationException("Invalid chart " + field),
        };

        private async Task<Action> LoadContentAsync(OmsPageRoute requested, CancellationToken token)
        {
            JObject content = await service.GetClientContentAsync(token).ConfigureAwait(false);
            JArray news = Array(content, "news");
            JObject? article = requested.Kind == OmsPageKind.Article
                ? news.OfType<JObject>().SingleOrDefault(post => String(post, "slug") == requested.Slug) : null;
            if (requested.Kind == OmsPageKind.Article && article == null)
                throw new OmsIrException("article_not_found", "这篇新闻不存在，请返回新闻列表。");
            return () =>
            {
                if (requested.Kind == OmsPageKind.Help)
                {
                    AddText("OMS 帮助", 26);
                    var sections = Array(content, "help").Cast<JObject>().ToArray();
                    foreach (JObject section in sections)
                        AddAction(String(section, "heading"), () => ReplaceRoute(requested with { HelpAnchor = String(section, "id") }));
                    if (requested.HelpAnchor != null) AddAction("查看完整帮助", () => ReplaceRoute(requested with { HelpAnchor = null }));
                    foreach (JObject section in sections.Where(section => requested.HelpAnchor == null || String(section, "id") == requested.HelpAnchor))
                        AddContentSection(section);
                    if (requested.HelpAnchor != null && !sections.Any(section => String(section, "id") == requested.HelpAnchor))
                        AddText("这节帮助不存在，请选择上方章节。", 16);
                }
                else if (article != null)
                {
                    AddText(String(article, "title"), 26);
                    AddText(String(article, "published_at"), 15);
                    foreach (JObject section in Array(article, "body")) AddContentSection(section);
                }
                else
                {
                    AddText("OMS 新闻", 26);
                    if (news.Count == 0) AddText("目前没有新闻。");
                    foreach (JObject post in news)
                    {
                        AddAction(String(post, "title"), () => Navigate(new OmsPageRoute { Kind = OmsPageKind.Article, Slug = String(post, "slug") }));
                        AddText(String(post, "published_at") + " · " + String(post, "excerpt"), 16);
                    }
                }
            };
        }

        private void AddContentSection(JObject section)
        {
            if (section["heading"]?.Type == JTokenType.String) AddText(String(section, "heading"), 23);
            AddText(String(section, "text"), 18);
            if (section["links"] is JArray links)
                foreach (JObject link in links)
                    AddAction(String(link, "label"), () =>
                    {
                        string href = String(link, "href");
                        if (OmsPageRoute.TryParse(service.State.ServiceAddress, href, out var next)) OpenRelated(next);
                        else game?.OpenOmsLink(href);
                    });
        }
    }
}
