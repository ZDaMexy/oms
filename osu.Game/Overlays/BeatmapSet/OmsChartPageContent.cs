// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Online.IR;
using osu.Game.Overlays.BeatmapListing;
using osuTK;

namespace osu.Game.Overlays.BeatmapSet
{
    /// <summary>Actual OMS MD5 chart data in the native listing and detail page bodies.</summary>
    public partial class OmsChartPageContent : OmsPageContent
    {
        [Resolved]
        private IBindable<WorkingBeatmap> currentBeatmap { get; set; } = null!;

        protected override Task<Action> LoadPageAsync(OmsPageRoute requested, CancellationToken token) => requested.Kind switch
        {
            OmsPageKind.Charts => loadChartsAsync(requested, token),
            OmsPageKind.Chart => loadChartAsync(requested, token),
            _ => throw new ArgumentException("This content requires a chart or charts route.", nameof(requested)),
        };

        private async Task<Action> loadChartsAsync(OmsPageRoute requested, CancellationToken token)
        {
            JObject response = await service.GetSourceChartsAsync(requested.Query, requested.Page, token).ConfigureAwait(false);
            OmsChartSummary[] charts = Array(response, "items").Select(item => readChart(item as JObject
                ?? throw new JsonSerializationException("Invalid OMS chart entry."))).ToArray();
            long total = Integer(response, "total");
            long limit = Integer(response, "limit");
            if (charts.Length > 20 || Integer(response, "page") != requested.Page || limit != 20 || total < charts.Length)
                throw new JsonSerializationException("Invalid OMS chart pagination.");

            return () =>
            {
                if (charts.Length == 0)
                    AddText("没有找到已收录成绩的谱面。可以修改搜索，或到谱面下载页查找原包。", 16);
                var cards = new ChartCardFlow();
                foreach (OmsChartSummary chart in charts)
                {
                    cards.Add(new OmsChartCard(chart, () => Navigate(new OmsPageRoute
                    {
                        Kind = OmsPageKind.Chart,
                        Md5 = chart.Md5,
                        Ruleset = chart.Ruleset,
                        Title = chart.Title,
                    })));
                }
                rows.Add(cards);
                AddPagination(requested, total, limit);
            };
        }

        private async Task<Action> loadChartAsync(OmsPageRoute requested, CancellationToken token)
        {
            if (requested.Md5?.Length != 32 || !requested.Md5.All(Uri.IsHexDigit) || requested.Ruleset is not ("bms" or "mania"))
                throw new ArgumentException("OMS chart details require an actual source MD5 and ruleset.", nameof(requested));

            OmsChartSummary? chart = null;
            try
            {
                chart = readChart(await service.GetSourceChartAsync(requested.Md5, token).ConfigureAwait(false));
            }
            catch (OmsIrException exception) when (exception.Code == "chart_not_found" && exception.StatusCode == HttpStatusCode.NotFound)
            {
            }

            if (chart != null && (!string.Equals(chart.Md5, requested.Md5, StringComparison.OrdinalIgnoreCase) || chart.Ruleset != requested.Ruleset))
                throw new JsonSerializationException("OMS chart identity mismatch.");
            bool hasPublicChart = chart != null;
            chart ??= new OmsChartSummary(requested.Md5.ToLowerInvariant(), requested.Ruleset, null, null, null);
            OmsChartSummary actual = chart;
            return () =>
            {
                WorkingBeatmap? local = currentBeatmap.Value;
                if (local?.BeatmapInfo is not { } localInfo
                    || !string.Equals(localInfo.MD5Hash, actual.Md5, StringComparison.OrdinalIgnoreCase)
                    || localInfo.Ruleset.ShortName != actual.Ruleset)
                    local = null;
                string title = OmsChartInfo.TitleFor(actual, local?.BeatmapInfo);
                rows.Add(new OmsChartInfo(actual, hasPublicChart, local,
                    () => Navigate(requested with { Kind = OmsPageKind.Board, Title = title }),
                    () => game?.ShowOmsChartDownload(actual.Ruleset == "bms" ? actual.Md5 : title, actual.Ruleset == "bms")));
            };
        }

        private static OmsChartSummary readChart(JObject entry)
        {
            JObject chart = Object(entry, "chart");
            string md5 = String(chart, "md5");
            string ruleset = String(entry, "ruleset");
            if (md5.Length != 32 || !md5.All(Uri.IsHexDigit) || ruleset is not ("bms" or "mania"))
                throw new JsonSerializationException("Invalid OMS chart identity.");
            return new OmsChartSummary(md5.ToLowerInvariant(), ruleset, chartText(chart, "title"), chartText(chart, "artist"), chartText(chart, "difficulty"));
        }

        private static string? chartText(JObject chart, string field) => chart[field]?.Type switch
        {
            JTokenType.Null => null,
            JTokenType.String => string.IsNullOrWhiteSpace(chart[field]!.Value<string>()) ? null : chart[field]!.Value<string>(),
            _ => throw new JsonSerializationException("Invalid OMS chart " + field),
        };

        private partial class ChartCardFlow : FillFlowContainer<OmsChartCard>
        {
            public ChartCardFlow()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Direction = FillDirection.Full;
                Spacing = new Vector2(10);
            }

            protected override void Update()
            {
                base.Update();
                int columns = Math.Clamp((int)((DrawWidth + Spacing.X) / 420), 1, 3);
                float width = Math.Max(0, (DrawWidth - Spacing.X * (columns - 1)) / columns);
                foreach (OmsChartCard card in this)
                    card.Width = width;
            }
        }
    }
}
