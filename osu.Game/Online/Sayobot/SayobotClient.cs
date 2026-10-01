// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace osu.Game.Online.Sayobot
{
    /// <summary>
    /// Reads Sayobot's public catalogue without enabling OMS account or online services.
    /// </summary>
    public sealed class SayobotClient : IDisposable
    {
        private const string api_url = "https://api.sayobot.cn/";
        private const int page_size = 20;
        private const int max_response_bytes = 8 * 1024 * 1024;

        private readonly HttpClient client;
        private readonly bool ownsClient;
        private bool disposed;

        public SayobotClient(HttpClient? client = null)
        {
            this.client = client ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
            ownsClient = client == null;
        }

        public async Task<SayobotSearchResult> SearchAsync(SayobotSearchQuery query, int offset, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(query.Text);
            ArgumentOutOfRangeException.ThrowIfNegative(offset);

            if (query.KeyCount is < 1 or > 18)
                throw new ArgumentOutOfRangeException(nameof(query), "The mania key count must be between 1 and 18.");

            if ((query.MinStars is double minimum && (!double.IsFinite(minimum) || minimum < 0))
                || (query.MaxStars is double maximum && (!double.IsFinite(maximum) || maximum < 0))
                || (query.MinStars ?? 0) > (query.MaxStars ?? 1000))
                throw new ArgumentOutOfRangeException(nameof(query), "The star rating range must be finite, non-negative and ordered.");

            if (!Enum.IsDefined(query.Category))
                throw new ArgumentOutOfRangeException(nameof(query), "The Sayobot category is invalid.");

            string keyword = query.Text.Trim();

            if (keyword.Length > 0 && keyword.All(char.IsAsciiDigit))
            {
                if (!int.TryParse(keyword, NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id < 1)
                    throw new InvalidDataException("The beatmap set ID must be a positive 32-bit integer.");

                SayobotBeatmapSet set = await GetBeatmapSetAsync(id, cancellationToken).ConfigureAwait(false);
                SayobotBeatmapSet? filtered = filterBeatmapSet(set, query);
                cancellationToken.ThrowIfCancellationRequested();
                return new SayobotSearchResult(filtered == null ? Array.Empty<SayobotBeatmapSet>() : new[] { filtered }, 0, false);
            }

            var parameters = new Dictionary<string, object>
            {
                ["cmd"] = "beatmaplist",
                ["type"] = "search",
                ["limit"] = page_size,
                ["offset"] = offset,
                ["mode"] = 8,
                ["keyword"] = keyword
            };

            if (query.KeyCount is int keys)
                parameters["cs"] = new[] { keys, keys };

            if (query.MinStars.HasValue || query.MaxStars.HasValue)
                parameters["stars"] = new[] { query.MinStars ?? 0, query.MaxStars ?? 1000 };

            if (query.Category != SayobotCategory.All)
                parameters["class"] = (int)query.Category;

            using var request = new HttpRequestMessage(HttpMethod.Post, api_url + "?post")
            {
                Content = new StringContent(JsonSerializer.Serialize(parameters), Encoding.UTF8, "application/json")
            };
            using var document = await sendAsync(request, cancellationToken).ConfigureAwait(false);
            JsonElement root = requireObject(document.RootElement, "search response");
            int status = readInteger(root, "status");

            if (status == -1)
            {
                if (root.TryGetProperty("data", out var emptyData) && requireArray(emptyData, "data").GetArrayLength() != 0)
                    throw new InvalidDataException("Sayobot returned results with a no-match status.");

                if (root.TryGetProperty("endid", out _) && readInteger(root, "endid", 0) != 0)
                    throw new InvalidDataException("Sayobot returned a continuation cursor with a no-match status.");

                cancellationToken.ThrowIfCancellationRequested();
                return new SayobotSearchResult(Array.Empty<SayobotBeatmapSet>(), 0, false);
            }

            if (status != 0)
                throw new InvalidDataException("Sayobot reported an unsuccessful catalogue response.");

            int nextOffset = readInteger(root, "endid", 0);

            if (nextOffset != 0 && nextOffset <= offset)
                throw new InvalidDataException("The Sayobot continuation cursor did not advance.");

            JsonElement data = requireArray(readRequired(root, "data"), "data");

            if (data.GetArrayLength() > page_size)
                throw new InvalidDataException("Sayobot returned more than 20 beatmap sets in one page.");

            var ids = new List<int>();
            var uniqueIds = new HashSet<int>();

            foreach (JsonElement item in data.EnumerateArray())
            {
                int id = readInteger(requireObject(item, "beatmap set"), "sid", 1);

                if (!uniqueIds.Add(id))
                    throw new InvalidDataException("Sayobot returned duplicate beatmap set IDs in one page.");

                ids.Add(id);
            }

            var resolved = new SayobotBeatmapSet?[ids.Count];

            await Parallel.ForEachAsync(Enumerable.Range(0, ids.Count), new ParallelOptions
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = cancellationToken
            }, async (index, token) =>
            {
                SayobotBeatmapSet? set = await getBeatmapSetAsync(ids[index], token).ConfigureAwait(false);

                if (set != null)
                    resolved[index] = filterBeatmapSet(set, query);
            }).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            return new SayobotSearchResult(resolved.OfType<SayobotBeatmapSet>().ToArray(), nextOffset, nextOffset != 0);
        }

        private static SayobotBeatmapSet? filterBeatmapSet(SayobotBeatmapSet set, SayobotSearchQuery query)
        {
            if (!matchesCategory(query.Category, set.Status))
                return null;

            // The remote filters match a set as a whole. A different mode or difficulty can satisfy each range.
            SayobotBeatmap[] matching = set.Beatmaps.Where(beatmap => (!query.KeyCount.HasValue || beatmap.KeyCount == query.KeyCount)
                && (!query.MinStars.HasValue || beatmap.StarRating >= query.MinStars)
                && (!query.MaxStars.HasValue || beatmap.StarRating <= query.MaxStars)).ToArray();

            return matching.Length == 0 ? null : set with { Beatmaps = matching };
        }

        public async Task<SayobotBeatmapSet> GetBeatmapSetAsync(int id, CancellationToken cancellationToken)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(id, 1);
            return await getBeatmapSetAsync(id, cancellationToken).ConfigureAwait(false)
                   ?? throw new InvalidDataException("The Sayobot beatmap set is no longer available.");
        }

        private async Task<SayobotBeatmapSet?> getBeatmapSetAsync(int id, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, api_url + "v2/beatmapinfo?0=" + id.ToString(CultureInfo.InvariantCulture));
            using var document = await sendAsync(request, cancellationToken).ConfigureAwait(false);
            JsonElement root = requireObject(document.RootElement, "detail response");
            int status = readInteger(root, "status");

            if (status == -1)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return null;
            }

            if (status != 0)
                throw new InvalidDataException("Sayobot reported an unsuccessful beatmap detail response.");

            JsonElement set = requireObject(readRequired(root, "data"), "data");

            if (readInteger(set, "sid", 1) != id)
                throw new InvalidDataException("Sayobot returned the details of a different beatmap set.");

            int approval = readInteger(set, "approved", -2, 4);
            string title = readString(set, "title");
            string artist = readString(set, "artist");
            string creator = readString(set, "creator");
            JsonElement beatmaps = requireArray(readRequired(set, "bid_data"), "bid_data");
            var nativeMania = new List<SayobotBeatmap>();
            var uniqueIds = new HashSet<int>();

            foreach (JsonElement item in beatmaps.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                JsonElement beatmap = requireObject(item, "beatmap");
                int beatmapId = readInteger(beatmap, "bid", 1);

                if (!uniqueIds.Add(beatmapId))
                    throw new InvalidDataException("Sayobot returned duplicate beatmap IDs.");

                if (readInteger(beatmap, "mode", 0, 3) != 3)
                    continue;

                double keyCount = readNumber(beatmap, "CS");

                if (keyCount < 1 || keyCount > 18 || keyCount != Math.Truncate(keyCount))
                    throw new InvalidDataException("The Sayobot mania key count must be an integer between 1 and 18.");

                double starRating = readNumber(beatmap, "star");

                if (starRating < 0)
                    throw new InvalidDataException("The Sayobot star rating must be non-negative.");

                nativeMania.Add(new SayobotBeatmap(beatmapId, readString(beatmap, "version"), (int)keyCount, starRating));
            }

            cancellationToken.ThrowIfCancellationRequested();
            string setId = id.ToString(CultureInfo.InvariantCulture);
            return new SayobotBeatmapSet(id, title, artist, creator, approval, nativeMania.ToArray(),
                new Uri("https://dl.sayobot.cn/beatmaps/download/novideo/" + setId),
                new Uri("https://a.sayobot.cn/beatmaps/" + setId + "/covers/cover.webp?0"));
        }

        private async Task<JsonDocument> sendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            request.Headers.UserAgent.ParseAdd("OMS/1.0 (+https://github.com/ZDaMexy/oms)");
            request.Headers.Referrer = new Uri("https://github.com/ZDaMexy/oms/");

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            if (client.Timeout != Timeout.InfiniteTimeSpan)
                deadline.CancelAfter(client.Timeout);

            CancellationToken requestToken = deadline.Token;

            try
            {
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                if (response.RequestMessage?.RequestUri is Uri finalUri && finalUri != request.RequestUri)
                    throw new InvalidDataException("The Sayobot metadata request was redirected.");

                if (response.Content.Headers.ContentLength > max_response_bytes)
                    throw new InvalidDataException("The Sayobot response exceeds the 8 MiB JSON limit.");

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
                        throw new HttpRequestException("The Sayobot response could not be read.", exception);
                    }

                    if (count == 0)
                        break;

                    if (body.Length + count > max_response_bytes)
                        throw new InvalidDataException("The Sayobot response exceeds the 8 MiB JSON limit.");

                    body.Write(buffer, 0, count);
                }

                requestToken.ThrowIfCancellationRequested();

                try
                {
                    JsonDocument document = JsonDocument.Parse(body.GetBuffer().AsMemory(0, (int)body.Length), new JsonDocumentOptions { MaxDepth = 32 });

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
                    throw new InvalidDataException("Sayobot returned invalid JSON.", exception);
                }
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new HttpRequestException("The Sayobot request timed out or was interrupted by the HTTP client.", exception);
            }
        }

        private static bool matchesCategory(SayobotCategory category, int status) => category switch
        {
            SayobotCategory.All => true,
            SayobotCategory.Ranked => status is 1 or 2,
            SayobotCategory.Qualified => status == 3,
            SayobotCategory.Loved => status == 4,
            SayobotCategory.Pending => status is -1 or 0,
            SayobotCategory.Graveyard => status == -2,
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };

        private static JsonElement requireObject(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException($"The Sayobot field '{name}' must be an object.");

            return element;
        }

        private static JsonElement requireArray(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException($"The Sayobot field '{name}' must be an array.");

            return element;
        }

        private static JsonElement readRequired(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value))
                throw new InvalidDataException($"The Sayobot response is missing '{name}'.");

            return value;
        }

        private static int readInteger(JsonElement element, string name, int minimum = int.MinValue, int maximum = int.MaxValue)
        {
            JsonElement value = readRequired(element, name);

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int number) || number < minimum || number > maximum)
                throw new InvalidDataException($"The Sayobot field '{name}' must be an integer between {minimum} and {maximum}.");

            return number;
        }

        private static double readNumber(JsonElement element, string name)
        {
            JsonElement value = readRequired(element, name);

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out double number) || !double.IsFinite(number))
                throw new InvalidDataException($"The Sayobot field '{name}' must be a finite number.");

            return number;
        }

        private static string readString(JsonElement element, string name)
        {
            JsonElement value = readRequired(element, name);

            if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
                throw new InvalidDataException($"The Sayobot field '{name}' must be a non-empty string.");

            return value.GetString()!;
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
