// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace osu.Game.Online.Bms
{
    /// <summary>
    /// Reads the two explicitly enabled public BMS catalogues. It does not depend on the OMS API or change local tables.
    /// </summary>
    public sealed class BmsDownloadClient : IDisposable
    {
        private const string ginger_base_url = "https://gingerrush.com/api/v1/";
        private const string konmai_base_url = "https://bms.alvorna.com/api/";
        private const int page_size = 20;
        private const int table_page_size = 100;
        private const int max_response_bytes = 8 * 1024 * 1024;

        private readonly HttpClient client;
        private readonly bool ownsClient;
        private bool disposed;

        public BmsDownloadClient(HttpClient? client = null)
        {
            this.client = client ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
            ownsClient = client == null;
        }

        public async Task<BmsDownloadSearchResult> SearchAsync(BmsDownloadSource source, string query, int page, string? tableId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);

            using HttpRequestMessage request = source switch
            {
                BmsDownloadSource.Ginger => gingerSearchRequest(query, page, tableId),
                BmsDownloadSource.Konmai => konmaiSearchRequest(query, page, tableId),
                _ => throw new ArgumentOutOfRangeException(nameof(source))
            };

            using var document = await sendAsync(request, false, cancellationToken).ConfigureAwait(false);
            JsonElement root = requireObject(document!.RootElement, "search response");
            bool ginger = source == BmsDownloadSource.Ginger;

            if (!ginger)
                requireKonmaiSuccess(root);

            int returnedPage = readInt(root, "page", 1);
            int returnedPageSize = readInt(root, ginger ? "pageSize" : "page_size", 1);
            int totalPages = readInt(root, ginger ? "pageCount" : "total_pages", 0);
            int total = readInt(root, "total", 0);
            JsonElement data = requireArray(readRequired(root, "data"), "data");

            if (returnedPage != page || returnedPageSize != page_size || totalPages != (total + (long)page_size - 1) / page_size
                || data.GetArrayLength() > Math.Min(page_size, Math.Max(0, total - (page - 1L) * page_size)))
                throw new InvalidDataException("The BMS source returned inconsistent pagination.");

            var packages = new Dictionary<string, BmsDownloadPackage>(StringComparer.Ordinal);

            foreach (JsonElement item in data.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                addPackage(packages, ginger ? readGingerPackage(item) : readKonmaiPackage(item));
            }

            cancellationToken.ThrowIfCancellationRequested();

            // 616's total counts chart rows, even when several rows form one card.
            return new BmsDownloadSearchResult(packages.Values.ToArray(), returnedPage, totalPages, total);
        }

        public async Task<IReadOnlyList<BmsDownloadTable>> GetTablesAsync(BmsDownloadSource source, CancellationToken cancellationToken)
        {
            using HttpRequestMessage request = source switch
            {
                BmsDownloadSource.Ginger => gingerPost("table/selectHeaderListWithFullInfo", new { type = "TABLE" }),
                BmsDownloadSource.Konmai => new HttpRequestMessage(HttpMethod.Get, konmai_base_url + "tables"),
                _ => throw new ArgumentOutOfRangeException(nameof(source))
            };

            using var document = await sendAsync(request, false, cancellationToken).ConfigureAwait(false);
            JsonElement data;

            if (source == BmsDownloadSource.Ginger)
                data = requireArray(document!.RootElement, "tables");
            else
            {
                JsonElement root = requireObject(document!.RootElement, "tables response");
                requireKonmaiSuccess(root);
                data = requireArray(readRequired(root, "tables"), "tables");
            }

            var tables = new List<BmsDownloadTable>();

            foreach (JsonElement item in data.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                JsonElement table = requireObject(item, "table");

                if (source == BmsDownloadSource.Ginger)
                {
                    string id = readInt(table, "id", 1).ToString(CultureInfo.InvariantCulture);
                    string originalUrl = readOptionalUrl(table, "originalURL")?.OriginalString ?? "";
                    tables.Add(new BmsDownloadTable(id, readString(table, "name"), originalUrl));
                }
                else
                {
                    string originalUrl = readString(table, "diff_table_url");
                    requireWebUrl(originalUrl, "diff_table_url");
                    tables.Add(new BmsDownloadTable(originalUrl, readString(table, "diff_table_name"), originalUrl,
                        readOptionalUrl(table, "diff_table_full_local_url")));
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            return tables.ToArray();
        }

        public Task<BmsDownloadTableData> GetTableDataAsync(BmsDownloadSource source, BmsDownloadTable table, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(table);
            cancellationToken.ThrowIfCancellationRequested();

            return source switch
            {
                BmsDownloadSource.Ginger => getGingerTableDataAsync(table, cancellationToken),
                BmsDownloadSource.Konmai => getKonmaiTableDataAsync(table, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(source))
            };
        }

        public async Task<BmsDownloadSearchResult> SearchTableLevelAsync(BmsDownloadSource source, BmsDownloadTableData data, string level, string query, int page,
                                                                       CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(level);
            ArgumentNullException.ThrowIfNull(query);
            ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);

            if (source != BmsDownloadSource.Ginger && source != BmsDownloadSource.Konmai)
                throw new ArgumentOutOfRangeException(nameof(source));

            cancellationToken.ThrowIfCancellationRequested();
            string keyword = query.Trim();
            BmsDownloadTableEntry[] matchingEntries = data.Entries
                .Where(entry => entry.Level == level && (entry.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || entry.Artist.Contains(keyword, StringComparison.OrdinalIgnoreCase) || entry.Md5.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                .DistinctBy(entry => entry.Md5).ToArray();
            int total = matchingEntries.Length;
            BmsDownloadTableEntry[] pageEntries = matchingEntries.Skip((int)Math.Min(total, (page - 1L) * page_size)).Take(page_size).ToArray();
            var resolved = new BmsDownloadPackage[pageEntries.Length];

            await Parallel.ForEachAsync(Enumerable.Range(0, pageEntries.Length), new ParallelOptions
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = cancellationToken
            }, async (index, token) =>
            {
                BmsDownloadTableEntry entry = pageEntries[index];
                BmsDownloadPackage? package = await ResolveAsync(source, entry.Md5, token).ConfigureAwait(false);

                if (package == null)
                {
                    string title = string.IsNullOrWhiteSpace(entry.Title) ? entry.Md5 : entry.Title;
                    package = new BmsDownloadPackage(source, "chart:" + entry.Md5, title, null,
                        new[] { new BmsDownloadChart(entry.Md5, title, entry.Artist) });
                }
                else
                    package = package with { Charts = package.Charts.Where(chart => chart.Md5 == entry.Md5).ToArray() };

                resolved[index] = package;
            }).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            var packages = new Dictionary<string, BmsDownloadPackage>(StringComparer.Ordinal);

            foreach (BmsDownloadPackage package in resolved)
                addPackage(packages, package);

            return new BmsDownloadSearchResult(packages.Values.ToArray(), page, (int)((total + (long)page_size - 1) / page_size), total);
        }

        private async Task<BmsDownloadTableData> getGingerTableDataAsync(BmsDownloadTable table, CancellationToken cancellationToken)
        {
            if (!int.TryParse(table.Id, NumberStyles.None, CultureInfo.InvariantCulture, out int tableId) || tableId < 1)
                throw new ArgumentException("Ginger table IDs must be positive integers.", nameof(table));

            var budget = new TableReadBudget();
            string symbol;
            string[] orderedLevels;

            using (var headerRequest = new HttpRequestMessage(HttpMethod.Get, ginger_base_url + "table/selectOneHeader/" + table.Id))
            using (var document = await sendAsync(headerRequest, false, cancellationToken, budget).ConfigureAwait(false))
            {
                JsonElement header = requireObject(document!.RootElement, "table header");

                if (readInt(header, "id", 1) != tableId)
                    throw new InvalidDataException("Ginger returned the header of a different difficulty table.");

                symbol = readOptionalString(header, "symbol") ?? "";
                orderedLevels = (readOptionalString(header, "levelOrders") ?? "").Split(',').Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            }

            var entries = new List<BmsDownloadTableEntry>();
            int total = 0;
            int totalPages = 1;

            for (int page = 1; page <= totalPages; page++)
            {
                using var request = gingerPost("table/selectDataList", new
                {
                    pageRequest = new { page, pageSize = table_page_size },
                    headerID = tableId,
                    fuzzyKeyword = (string?)null
                });
                using var document = await sendAsync(request, false, cancellationToken, budget).ConfigureAwait(false);
                JsonElement root = requireObject(document!.RootElement, "table data response");
                int returnedPage = readInt(root, "page", 1);
                int returnedPageSize = readInt(root, "pageSize", 1);
                int returnedTotalPages = readInt(root, "pageCount", 0);
                int returnedTotal = readInt(root, "total", 0);
                JsonElement data = requireArray(readRequired(root, "data"), "data");

                if (returnedPage != page || returnedPageSize != table_page_size
                    || returnedTotalPages != (returnedTotal + (long)table_page_size - 1) / table_page_size
                    || data.GetArrayLength() != Math.Min(table_page_size, Math.Max(0, returnedTotal - (page - 1L) * table_page_size))
                    || (page > 1 && (returnedTotal != total || returnedTotalPages != totalPages)))
                    throw new InvalidDataException("Ginger returned incomplete or changing difficulty table pagination.");

                total = returnedTotal;
                totalPages = returnedTotalPages;

                foreach (JsonElement item in data.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    JsonElement entry = requireObject(item, "table entry");

                    if (readInt(entry, "headerID", 1) != tableId)
                        throw new InvalidDataException("Ginger returned an entry of a different difficulty table.");

                    entries.Add(readTableEntry(entry));
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            return tableData(symbol, orderedLevels, entries);
        }

        private async Task<BmsDownloadTableData> getKonmaiTableDataAsync(BmsDownloadTable table, CancellationToken cancellationToken)
        {
            if (table.HeaderUrl == null)
                throw new InvalidDataException("616 did not provide a mirrored difficulty table header URL.");

            Uri headerUrl = requireKonmaiTableUrl(table.HeaderUrl);
            var budget = new TableReadBudget();
            string symbol;
            string[] orderedLevels;
            Uri bodyUrl;

            using (var request = new HttpRequestMessage(HttpMethod.Get, headerUrl))
            using (var document = await sendAsync(request, false, cancellationToken, budget).ConfigureAwait(false))
            {
                JsonElement header = requireObject(document!.RootElement, "table header");
                symbol = readOptionalString(header, "symbol") ?? "";
                string dataUrl = readString(header, "data_url");

                if (dataUrl != dataUrl.Trim() || dataUrl.Any(char.IsControl) || !Uri.TryCreate(headerUrl, dataUrl, out Uri? uri))
                    throw new InvalidDataException("616 returned an invalid mirrored difficulty table data URL.");

                bodyUrl = requireKonmaiTableUrl(uri);
                orderedLevels = !header.TryGetProperty("level_order", out JsonElement order) || order.ValueKind == JsonValueKind.Null
                    ? Array.Empty<string>()
                    : requireArray(order, "level_order").EnumerateArray().Select(readTableLevel).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            }

            using var bodyRequest = new HttpRequestMessage(HttpMethod.Get, bodyUrl);
            using var bodyDocument = await sendAsync(bodyRequest, false, cancellationToken, budget).ConfigureAwait(false);
            var entries = new List<BmsDownloadTableEntry>();

            foreach (JsonElement item in requireArray(bodyDocument!.RootElement, "table data").EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                entries.Add(readTableEntry(requireObject(item, "table entry")));
            }

            cancellationToken.ThrowIfCancellationRequested();
            return tableData(symbol, orderedLevels, entries);
        }

        private static Uri requireKonmaiTableUrl(Uri url)
        {
            if (!url.IsAbsoluteUri || url.Scheme != Uri.UriSchemeHttps || !url.IsDefaultPort || url.UserInfo.Length != 0
                || !url.Host.Equals("bms.alvorna.com", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("616 difficulty table metadata must remain on its HTTPS mirror at bms.alvorna.com.");

            return url;
        }

        private static BmsDownloadTableEntry readTableEntry(JsonElement entry) => new BmsDownloadTableEntry(readMd5(entry),
            readTableLevel(readRequired(entry, "level")), readOptionalString(entry, "title") ?? "", readOptionalString(entry, "artist") ?? "");

        private static string readTableLevel(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()!,
            JsonValueKind.Number => value.GetRawText(),
            _ => throw new InvalidDataException("A difficulty table level must be a string or number.")
        };

        private static BmsDownloadTableData tableData(string symbol, IEnumerable<string> orderedLevels, List<BmsDownloadTableEntry> entries)
        {
            string[] actualLevels = entries.Select(entry => entry.Level).Distinct(StringComparer.Ordinal).ToArray();
            var actual = new HashSet<string>(actualLevels, StringComparer.Ordinal);
            string[] levels = orderedLevels.Where(actual.Contains).Concat(actualLevels).Distinct(StringComparer.Ordinal).ToArray();
            return new BmsDownloadTableData(symbol, levels, entries.ToArray());
        }

        public async Task<BmsDownloadPackage?> ResolveAsync(BmsDownloadSource source, string md5, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(md5);
            string normalised = md5.Trim().ToLowerInvariant();

            if (!isMd5(normalised))
                throw new ArgumentException("A BMS chart MD5 must contain exactly 32 hexadecimal characters.", nameof(md5));

            using HttpRequestMessage request = source switch
            {
                BmsDownloadSource.Ginger => new HttpRequestMessage(HttpMethod.Get, ginger_base_url + "files/package/" + normalised),
                BmsDownloadSource.Konmai => new HttpRequestMessage(HttpMethod.Get, konmai_base_url + "hash?md5=" + normalised),
                _ => throw new ArgumentOutOfRangeException(nameof(source))
            };

            using var document = await sendAsync(request, true, cancellationToken).ConfigureAwait(false);

            if (document == null)
                return null;

            BmsDownloadPackage package;

            if (source == BmsDownloadSource.Ginger)
                package = readGingerPackage(document.RootElement);
            else
            {
                JsonElement root = requireObject(document.RootElement, "hash response");
                requireKonmaiSuccess(root);
                package = readKonmaiPackage(readRequired(root, "data"));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!package.Charts.Any(chart => chart.Md5 == normalised))
                throw new InvalidDataException("The BMS source returned a package without the requested chart MD5.");

            return package;
        }

        private static HttpRequestMessage gingerSearchRequest(string query, int page, string? tableId)
        {
            int? table = null;

            if (tableId != null)
            {
                if (!int.TryParse(tableId, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < 1)
                    throw new ArgumentException("Ginger table IDs must be positive integers.", nameof(tableId));

                table = value;
            }

            return gingerPost("files/selectList", new
            {
                pageRequest = new { page, pageSize = page_size },
                fuzzyKeyword = query.Trim(),
                md5 = "",
                tableID = table
            });
        }

        private static HttpRequestMessage konmaiSearchRequest(string query, int page, string? tableId)
        {
            string url = konmai_base_url + "search?name=" + Uri.EscapeDataString(query.Trim())
                         + "&page=" + page.ToString(CultureInfo.InvariantCulture) + "&page_size=" + page_size;

            if (tableId != null)
            {
                if (!tryWebUrl(tableId, out _))
                    throw new ArgumentException("616 table IDs must be the original HTTP or HTTPS table URL.", nameof(tableId));

                url += "&table=" + Uri.EscapeDataString(tableId);
            }

            return new HttpRequestMessage(HttpMethod.Get, url);
        }

        private static HttpRequestMessage gingerPost(string route, object payload) => new HttpRequestMessage(HttpMethod.Post, ginger_base_url + route)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        private async Task<JsonDocument?> sendAsync(HttpRequestMessage request, bool allowNotFound, CancellationToken cancellationToken, TableReadBudget? tableBudget = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (client.Timeout != Timeout.InfiniteTimeSpan)
                deadline.CancelAfter(client.Timeout);
            CancellationToken requestToken = deadline.Token;

            try
            {
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestToken).ConfigureAwait(false);
                requestToken.ThrowIfCancellationRequested();

                if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
                    return null;

                response.EnsureSuccessStatusCode();

                if (tableBudget != null && response.RequestMessage?.RequestUri is Uri finalUri && finalUri != request.RequestUri)
                    throw new InvalidDataException("The difficulty table metadata request was redirected.");

                if (response.Content.Headers.ContentLength > max_response_bytes)
                    throw new InvalidDataException("The BMS source response exceeds the 8 MiB JSON limit.");

                using var stream = await response.Content.ReadAsStreamAsync(requestToken).ConfigureAwait(false);
                using var body = new MemoryStream();
                byte[] buffer = new byte[16384];

                while (true)
                {
                    requestToken.ThrowIfCancellationRequested();
                    int count;

                    try
                    {
                        count = await stream.ReadAsync(buffer.AsMemory(), requestToken).ConfigureAwait(false);
                    }
                    catch (IOException exception)
                    {
                        requestToken.ThrowIfCancellationRequested();
                        throw new HttpRequestException("The BMS source response could not be read.", exception);
                    }

                    if (count == 0)
                        break;

                    if (body.Length + count > max_response_bytes)
                        throw new InvalidDataException("The BMS source response exceeds the 8 MiB JSON limit.");

                    tableBudget?.Consume(count);

                    body.Write(buffer, 0, count);
                }

                requestToken.ThrowIfCancellationRequested();

                try
                {
                    var document = JsonDocument.Parse(body.GetBuffer().AsMemory(0, (int)body.Length), new JsonDocumentOptions { MaxDepth = 32 });

                    if (requestToken.IsCancellationRequested)
                    {
                        document.Dispose();
                        requestToken.ThrowIfCancellationRequested();
                    }

                    return document;
                }
                catch (JsonException exception)
                {
                    requestToken.ThrowIfCancellationRequested();
                    throw new InvalidDataException("The BMS source returned invalid JSON.", exception);
                }
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new HttpRequestException("The BMS source request timed out or was interrupted by the HTTP client.", exception);
            }
        }

        private static BmsDownloadPackage readGingerPackage(JsonElement item)
        {
            JsonElement package = requireObject(item, "package");
            string id = readInt(package, "id", 1).ToString(CultureInfo.InvariantCulture);
            string name = readString(package, "fileName");
            JsonElement songs = requireArray(readRequired(package, "songs"), "songs");

            if (songs.GetArrayLength() == 0)
                throw new InvalidDataException("The BMS source returned a package without any charts.");

            BmsDownloadChart[] charts = songs.EnumerateArray().Select(readGingerChart).DistinctBy(chart => chart.Md5).ToArray();
            Uri? banner = readOptionalUrl(package, "bannerURL");
            Uri? stage = readOptionalUrl(package, "stageFileURL");
            return new BmsDownloadPackage(BmsDownloadSource.Ginger, id, name, readOptionalUrl(package, "downloadURL"), charts,
                readOptionalSize(package, "fileSize"), stage ?? banner);
        }

        private static BmsDownloadChart readGingerChart(JsonElement item)
        {
            JsonElement chart = requireObject(item, "chart");
            string fileName = readString(chart, "fileName");
            string? title = readOptionalString(chart, "title");
            string? subtitle = readOptionalString(chart, "subTitle");
            string? mode = readOptionalString(chart, "mode");
            string? level = readOptionalString(chart, "playLevel");
            int? keyCount = mode switch
            {
                "BEAT_5K" or "POPN_5K" => 5,
                "BEAT_7K" => 7,
                "POPN_9K" => 9,
                "BEAT_10K" => 10,
                "BEAT_14K" => 14,
                "KEYBOARD_24K" => 24,
                "KEYBOARD_24K_DOUBLE" => 48,
                _ => null
            };

            return new BmsDownloadChart(readMd5(chart), string.IsNullOrWhiteSpace(title) ? fileName : title,
                readOptionalString(chart, "artist") ?? "", string.IsNullOrWhiteSpace(subtitle) ? fileName : subtitle,
                keyCount, string.IsNullOrWhiteSpace(level) ? null : level, FileName: fileName);
        }

        private static BmsDownloadPackage readKonmaiPackage(JsonElement item)
        {
            JsonElement row = requireObject(item, "chart");
            string chartName = readString(row, "chart_name");
            string? title = readOptionalString(row, "title");
            string? songName = readOptionalString(row, "song_name");
            var chart = new BmsDownloadChart(readMd5(row), string.IsNullOrWhiteSpace(title) ? chartName : title,
                readOptionalString(row, "artist") ?? "", chartName, PreviewUrl: readOptionalUrl(row, "song_preview_url"));
            Uri? downloadUrl = readOptionalUrl(row, "song_url");

            return new BmsDownloadPackage(BmsDownloadSource.Konmai, downloadUrl?.OriginalString ?? "chart:" + chart.Md5,
                string.IsNullOrWhiteSpace(songName) ? chart.Title : songName, downloadUrl, new[] { chart });
        }

        private static void addPackage(Dictionary<string, BmsDownloadPackage> packages, BmsDownloadPackage package)
        {
            if (packages.TryGetValue(package.Key, out var existing))
            {
                if (existing.DownloadUrl != package.DownloadUrl || existing.Size != package.Size)
                    throw new InvalidDataException("The BMS source returned conflicting data for the same resource package.");

                packages[package.Key] = existing with { Charts = existing.Charts.Concat(package.Charts).DistinctBy(chart => chart.Md5).ToArray() };
            }
            else
                packages.Add(package.Key, package);
        }

        private static void requireKonmaiSuccess(JsonElement root)
        {
            if (readString(root, "result") != "success")
                throw new InvalidDataException("616 reported an unsuccessful catalogue response.");
        }

        private static JsonElement requireObject(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException($"The BMS source field '{name}' must be an object.");

            return element;
        }

        private static JsonElement requireArray(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException($"The BMS source field '{name}' must be an array.");

            return element;
        }

        private static JsonElement readRequired(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value))
                throw new InvalidDataException($"The BMS source response is missing '{name}'.");

            return value;
        }

        private static string readString(JsonElement element, string name)
        {
            string? value = readOptionalString(element, name);

            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidDataException($"The BMS source field '{name}' must be a non-empty string.");

            return value;
        }

        private static string? readOptionalString(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
                return null;

            if (value.ValueKind != JsonValueKind.String)
                throw new InvalidDataException($"The BMS source field '{name}' must be a string or null.");

            return value.GetString();
        }

        private static int readInt(JsonElement element, string name, int minimum)
        {
            JsonElement value = readRequired(element, name);

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int number) || number < minimum)
                throw new InvalidDataException($"The BMS source field '{name}' must be an integer of at least {minimum}.");

            return number;
        }

        private static long? readOptionalSize(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
                return null;

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out long size) || size < 0)
                throw new InvalidDataException($"The BMS source field '{name}' must be a non-negative byte count.");

            return size;
        }

        private static string readMd5(JsonElement element)
        {
            string md5 = readString(element, "md5");

            if (!isMd5(md5))
                throw new InvalidDataException("The BMS source returned an invalid chart MD5.");

            return md5.ToLowerInvariant();
        }

        private static bool isMd5(string value) => value.Length == 32 && value.All(character => char.IsAsciiHexDigit(character));

        private static Uri? readOptionalUrl(JsonElement element, string name)
        {
            string? value = readOptionalString(element, name);
            return value == null || value.Length == 0 ? null : requireWebUrl(value, name);
        }

        private static Uri requireWebUrl(string value, string name)
        {
            if (!tryWebUrl(value, out Uri? uri))
                throw new InvalidDataException($"The BMS source field '{name}' must be an absolute HTTP or HTTPS URL without credentials.");

            return uri!;
        }

        private static bool tryWebUrl(string value, out Uri? uri)
        {
            uri = null;
            return value == value.Trim() && !value.Any(char.IsControl) && Uri.TryCreate(value, UriKind.Absolute, out uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) && uri.Host.Length > 0 && uri.UserInfo.Length == 0;
        }

        private sealed class TableReadBudget
        {
            private int bytesRead;

            public void Consume(int count)
            {
                if (count > max_response_bytes - bytesRead)
                    throw new InvalidDataException("The complete difficulty table exceeds the 8 MiB JSON limit.");

                bytesRead += count;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            if (ownsClient)
                client.Dispose();
        }
    }
}
