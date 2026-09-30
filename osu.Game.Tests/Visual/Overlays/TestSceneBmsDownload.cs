// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input;
using osu.Game.Input.Bindings;
using osu.Game.Online.API;
using osu.Game.Online.Bms;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Toolbar;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select;
using osu.Game.Tests.Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.Visual.Overlays
{
    public partial class TestSceneBmsDownload : OsuGameTestScene
    {
        private const string normal_chart = "#TITLE OMS Download Test\n#ARTIST OMS\n#BPM 150\n#00119:0100\n";
        private const string another_chart = "#TITLE OMS Download Test Another\n#ARTIST OMS\n#BPM 150\n#00119:01000100\n";
        private static readonly string normal_md5 = hash(normal_chart);
        private static readonly string another_md5 = hash(another_chart);
        private StubHandler metadata = null!;
        private StubHandler packages = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;
        private BmsDownloadOverlay overlay => Game.BmsDownloads;
        private BmsDownloadHeader header => overlay.ChildrenOfType<BmsDownloadHeader>().Single();
        private BmsDownloadManager manager => ((DownloadGame)Game).Manager;

        protected override TestOsuGame CreateTestGame()
        {
            metadata = new StubHandler(defaultMetadata);
            packages = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(createPackage()) }));
            return new DownloadGame(LocalStorage, API, metadata, packages);
        }

        [Test]
        public void TestEntrancesAreAvailableWithoutOfficialOnlineFeatures()
        {
            AddUntilStep("browser and toolbar loaded", () => overlay.IsLoaded && Game.Toolbar.IsLoaded);
            AddAssert("official online features stay disabled", () => !Game.OnlineFeaturesEnabled);
            AddAssert("startup made no source request", () => metadata.Requests, () => Is.Zero);
            AddStep("open from menu browse", () => ((MainMenu)Game.ScreenStack.CurrentScreen).ChildrenOfType<ButtonSystem>().Single().OnBeatmapListing!());
            AddUntilStep("browser shows a result", () => overlay.State.Value == Visibility.Visible && overlay.Cards.Count == 1);
            AddStep("close browser", () => overlay.Hide());
            AddStep("open from toolbar", () => Game.Toolbar.ChildrenOfType<ToolbarBmsDownloadButton>().Single().Action!());
            AddUntilStep("toolbar opens browser", () => overlay.State.Value == Visibility.Visible);
            AddStep("close browser again", () => overlay.Hide());
            AddStep("use existing browse shortcut", () => Game.ChildrenOfType<GlobalActionContainer>().Single().TriggerPressed(GlobalAction.ToggleBeatmapListing));
            AddUntilStep("shortcut opens browser", () => overlay.State.Value == Visibility.Visible);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestDownloadContinuesWhileClosedAndOpensExactDifficulty(bool another)
        {
            string requestedMd5 = another ? another_md5 : normal_md5;
            TaskCompletionSource releasePackage = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            BmsDownloadTask? task = null;
            Guid target = Guid.Empty;
            Task? screenshot = null;
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("delay package response", () => packages.Response = async (_, token) =>
            {
                await releasePackage.Task.WaitAsync(token).ConfigureAwait(false);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(createPackage()) };
            });
            AddStep("open browser", () => overlay.Show());
            AddUntilStep("both pages merge into one package", () => overlay.Cards.SingleOrDefault()?.Package.Charts.Count == 2);
            AddStep("select difficulty and download", () =>
            {
                var card = overlay.Cards.Single();
                card.ToggleCharts();
                card.SelectChart(requestedMd5);
                card.StartDownload();
                task = manager.GetTask(card.Package.Key);
            });
            AddUntilStep("package request started", () => packages.Requests == 1);
            AddStep("capture downloading card if requested", () => screenshot = captureScreenshot("-downloading"));
            AddUntilStep("download screenshot completes", () => screenshot!.IsCompleted);
            AddStep("surface download screenshot failure", () => screenshot!.GetAwaiter().GetResult());
            AddStep("close while downloading", () => overlay.Hide());
            AddAssert("closing keeps task active", () => task!.Progress.State, () => Is.EqualTo(BmsDownloadState.Downloading));
            AddStep("finish download", () => releasePackage.SetResult());
            AddUntilStep("task finishes adding to library", () => task!.Completion.IsCompleted);
            AddAssert("download completed", () => task!.Progress.State, () => Is.EqualTo(BmsDownloadState.Completed));
            AddAssert("completion did not interrupt menu", () => Game.ScreenStack.CurrentScreen is MainMenu);
            AddStep("reopen browser", () => overlay.Show());
            AddUntilStep("card is ready to open", () => overlay.Cards.Single().ChildrenOfType<SettingsButtonV2>().Any(b => b.Text.ToString().Contains("song select", StringComparison.OrdinalIgnoreCase)));
            AddStep("capture completed card if requested", () => screenshot = captureScreenshot("-completed"));
            AddUntilStep("completion screenshot completes", () => screenshot!.IsCompleted);
            AddStep("surface completion screenshot failure", () => screenshot!.GetAwaiter().GetResult());
            AddStep("click completion notification", () =>
            {
                target = task!.Progress.Imported!.First(b => b.Md5 == requestedMd5).BeatmapId;
                Game.Notifications.ChildrenOfType<ProgressCompletionNotification>().Single().Activated!();
            });
            AddUntilStep("song select opened", () => Game.ScreenStack.CurrentScreen is SoloSongSelect);
            AddUntilStep("exact selected difficulty opened", () => Game.Beatmap.Value.BeatmapInfo.ID == target);
            AddUntilStep("song list is settled", () => Game.ScreenStack.CurrentScreen is SoloSongSelect select && select.CarouselItemsPresented && !select.IsFiltering);
            AddWaitStep("allow selection debounce", 5);
            AddAssert("settled list keeps requested difficulty", () => Game.Beatmap.Value.BeatmapInfo.ID, () => Is.EqualTo(target));
            AddAssert("BMS ruleset selected", () => Game.Ruleset.Value.ShortName, () => Is.EqualTo("bms"));
            AddAssert("original chart lives in chartbms", () => Game.Beatmap.Value.BeatmapInfo.BeatmapSet!.FilesystemStoragePath!.StartsWith("chartbms", StringComparison.Ordinal));
        }

        [TestCase(true, false)]
        [TestCase(false, false)]
        [TestCase(false, true)]
        public void TestOpenFromSettledSongSelect(bool switchRuleset, bool manualSelection)
        {
            BmsDownloadTask? task = null;
            Guid target = Guid.Empty;
            Guid normal = Guid.Empty;
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("open browser", () => overlay.Show());
            AddUntilStep("package loaded", () => overlay.Cards.SingleOrDefault()?.Package.Charts.Count == 2);
            AddStep("download selected difficulty", () => task = manager.Download(overlay.Cards.Single().Package, another_md5));
            AddUntilStep("package imported", () => task!.Completion.IsCompleted);
            AddStep("prepare previous song selection", () =>
            {
                target = task!.Progress.Imported!.First(b => b.Md5 == another_md5).BeatmapId;
                normal = task.Progress.Imported!.First(b => b.Md5 == normal_md5).BeatmapId;
                var previous = switchRuleset
                    ? Game.BeatmapManager.Import(TestResources.CreateTestBeatmapSetInfo(1))!.Value.Beatmaps.Single()
                    : Game.BeatmapManager.QueryBeatmap("ID == $0", normal)!;
                Game.Ruleset.Value = previous.Ruleset;
                Game.Beatmap.Value = Game.BeatmapManager.GetWorkingBeatmap(previous);
                overlay.Hide();
                DismissAnyNotifications();
            });
            PushAndConfirm(() => new SoloSongSelect());
            AddUntilStep("previous song list settled", () => Game.ScreenStack.CurrentScreen is SoloSongSelect select && select.CarouselItemsPresented && !select.IsFiltering);
            AddStep("open exact downloaded chart", () =>
            {
                var select = (SoloSongSelect)Game.ScreenStack.CurrentScreen;
                if (!switchRuleset)
                {
                    ((ISongSelect)select).Search("OMS Download Test");
                    Assert.That(select.IsFiltering, Is.True);
                }
                Game.PresentDownloadedBmsBeatmap(target);
            });
            AddUntilStep("downloaded chart is presented", () => Game.Beatmap.Value.BeatmapInfo.ID == target);
            if (manualSelection)
            {
                AddUntilStep("normal difficulty has a visible panel", () => ((SoloSongSelect)Game.ScreenStack.CurrentScreen).ChildrenOfType<Panel>().Any(panel =>
                    panel.Item?.IsVisible == true && panel.Item.Model is GroupedBeatmap beatmap && beatmap.Beatmap.ID == normal));
                AddStep("player selects normal while another filter is pending", () =>
                {
                    var select = (SoloSongSelect)Game.ScreenStack.CurrentScreen;
                    ((ISongSelect)select).Search("OMS");
                    Assert.That(select.IsFiltering, Is.True);
                    select.ChildrenOfType<Panel>().Single(panel => panel.Item?.IsVisible == true && panel.Item.Model is GroupedBeatmap beatmap && beatmap.Beatmap.ID == normal).TriggerClick();
                });
            }
            AddUntilStep("new song list settled", () => Game.ScreenStack.CurrentScreen is SoloSongSelect select && !select.IsFiltering);
            AddWaitStep("allow selection debounce", 5);
            AddAssert("settled global selection respects requested or manual chart", () => Game.Beatmap.Value.BeatmapInfo.ID, () => Is.EqualTo(manualSelection ? normal : target));
            AddAssert("carousel shows the same exact difficulty", () => ((SoloSongSelect)Game.ScreenStack.CurrentScreen).ChildrenOfType<BeatmapCarousel>().Single().CurrentBeatmap?.ID, () => Is.EqualTo(manualSelection ? normal : target));
            AddAssert("BMS ruleset selected", () => Game.Ruleset.Value.ShortName, () => Is.EqualTo("bms"));
        }

        [Test]
        public void TestSourceSwitchAndCloseDoNotPublishLateResults()
        {
            TaskCompletionSource<HttpResponseMessage> late = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("delay ginger query even after cancellation", () => metadata.Response = (request, token) =>
                request.RequestUri!.AbsolutePath.EndsWith("selectList", StringComparison.Ordinal) ? late.Task : defaultMetadata(request, token));
            AddStep("open ginger", () => overlay.Show());
            AddUntilStep("ginger query started", () => metadata.SearchRequests == 1);
            AddStep("switch source then close", () =>
            {
                header.Source.Value = BmsDownloadSource.Konmai;
                overlay.Hide();
            });
            AddStep("release cancelled source", () => late.SetResult(gingerResponse(1)));
            AddWaitStep("allow response callback", 3);
            AddAssert("old source cannot show cards", () => overlay.Cards.Count, () => Is.Zero);
            AddStep("reopen new source", () => overlay.Show());
            AddUntilStep("616 missing-package result appears", () => overlay.Cards.SingleOrDefault()?.Package.Source == BmsDownloadSource.Konmai);
            AddAssert("missing package stays visible and unavailable", () => !overlay.Cards.Single().Package.CanDownload);
            AddAssert("missing package has no download action", () => !overlay.Cards.Single().ChildrenOfType<SettingsButtonV2>().Single(b => b.Text.ToString() == "Download").Enabled.Value);
        }

        [Test]
        public void TestDownloadCompletionAndOpenDoNotInterruptGameplay()
        {
            TaskCompletionSource releasePackage = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            BmsDownloadTask? first = null;
            BmsDownloadTask? background = null;
            Guid target = Guid.Empty;
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("open browser", () => overlay.Show());
            AddUntilStep("package loaded", () => overlay.Cards.SingleOrDefault()?.Package.Charts.Count == 2);
            AddStep("download song to play", () => first = manager.Download(overlay.Cards.Single().Package, normal_md5));
            AddUntilStep("song is in library", () => first!.Completion.IsCompleted);
            AddStep("select song and start another download", () =>
            {
                target = first!.Progress.Imported!.First(b => b.Md5 == normal_md5).BeatmapId;
                var beatmap = Game.BeatmapManager.QueryBeatmap("ID == $0", target)!;
                Game.Beatmap.Value = Game.BeatmapManager.GetWorkingBeatmap(beatmap);
                Game.Ruleset.Value = beatmap.Ruleset;
                overlay.Hide();
                DismissAnyNotifications();
                packages.Response = async (_, token) =>
                {
                    await releasePackage.Task.WaitAsync(token).ConfigureAwait(false);
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(createPackage()) };
                };
                background = manager.Download(overlay.Cards.Single().Package with { Id = "background" }, another_md5);
            });
            PushAndConfirm(() => new TestPlayer(false, false));
            AddStep("finish background download during play", () => releasePackage.SetResult());
            AddUntilStep("background import finished", () => background!.Completion.IsCompleted);
            AddAssert("completion keeps player current", () => Game.ScreenStack.CurrentScreen is Player);
            AddAssert("completion keeps current difficulty", () => Game.Beatmap.Value.BeatmapInfo.ID, () => Is.EqualTo(target));
            AddAssert("open refuses to interrupt player", () => !Game.PresentDownloadedBmsBeatmap(background!.Progress.Imported!.First(b => b.Md5 == another_md5).BeatmapId));
            AddAssert("player remains current after open", () => Game.ScreenStack.CurrentScreen is Player);
        }

        [Test]
        public void TestLoadingNextPageKeepsExistingCardsInteractive()
        {
            TaskCompletionSource<HttpResponseMessage> nextPage = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            BmsDownloadTask? task = null;
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("hold the second metadata page", () => metadata.Response = async (request, token) =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("selectList", StringComparison.Ordinal))
                {
                    using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token).ConfigureAwait(false));
                    if (body.RootElement.GetProperty("pageRequest").GetProperty("page").GetInt32() == 2)
                        return await nextPage.Task.WaitAsync(token).ConfigureAwait(false);
                }
                return await defaultMetadata(request, token).ConfigureAwait(false);
            });
            AddStep("open browser", () => overlay.Show());
            AddUntilStep("next page is loading behind the first card", () => metadata.SearchRequests == 2 && overlay.Cards.Count == 1);
            AddAssert("next page does not block existing card input", () => overlay.ChildrenOfType<LoadingLayer>().Single().State.Value, () => Is.EqualTo(Visibility.Hidden));
            AddStep("download the already visible chart", () =>
            {
                var card = overlay.Cards.Single();
                card.ChildrenOfType<SettingsButtonV2>().Single(button => button.Text.ToString() == "Download").TriggerClick();
                task = manager.GetTask(card.Package.Key);
            });
            AddUntilStep("download completes while metadata is pending", () => task?.Completion.IsCompleted == true);
            AddAssert("visible chart is in the library", () => task!.Progress.State, () => Is.EqualTo(BmsDownloadState.Completed));
            AddStep("release next page", () => nextPage.SetResult(gingerResponse(2)));
            AddUntilStep("next page still merges its additional difficulty", () => overlay.Cards.SingleOrDefault()?.Package.Charts.Count == 2);
        }

        [Test]
        public void TestFailedPaginationRequiresManualRetry()
        {
            bool pageFails = true;
            int requestsAfterFailure = 0;
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("fail second page", () => metadata.Response = async (request, token) =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("selectList", StringComparison.Ordinal))
                {
                    using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token).ConfigureAwait(false));
                    if (body.RootElement.GetProperty("pageRequest").GetProperty("page").GetInt32() == 2 && pageFails)
                        return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                }
                return await defaultMetadata(request, token).ConfigureAwait(false);
            });
            AddStep("open browser", () => overlay.Show());
            AddUntilStep("second page failed", () => metadata.SearchRequests == 2);
            AddWaitStep("let failure render", 3);
            AddStep("record failed request count", () => requestsAfterFailure = metadata.SearchRequests);
            AddWaitStep("wait without clicking retry", 5);
            AddAssert("failure does not loop requests", () => metadata.SearchRequests, () => Is.EqualTo(requestsAfterFailure));
            AddStep("retry second page manually", () =>
            {
                pageFails = false;
                overlay.ChildrenOfType<SettingsButtonV2>().Last().Action!();
            });
            AddUntilStep("retry merges second chart", () => overlay.Cards.SingleOrDefault()?.Package.Charts.Count == 2);
        }

        [Test]
        public void TestDeletedCompletedChartDoesNotSelectAnotherDifficulty()
        {
            BmsDownloadTask? task = null;
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("open browser", () => overlay.Show());
            AddUntilStep("package loaded", () => overlay.Cards.SingleOrDefault()?.Package.Charts.Count == 2);
            AddStep("download package", () =>
            {
                var card = overlay.Cards.Single();
                task = manager.Download(card.Package, another_md5);
            });
            AddUntilStep("import finished", () => task!.Completion.IsCompleted);
            AddStep("delete target before opening", () =>
            {
                Guid id = task!.Progress.Imported!.First(b => b.Md5 == another_md5).BeatmapId;
                Game.Realm.Write(realm => realm.Find<BeatmapInfo>(id)!.BeatmapSet!.DeletePending = true);
                Game.PresentDownloadedBmsBeatmap(id);
            });
            AddWaitStep("allow navigation callback", 5);
            AddAssert("another difficulty was not selected", () => Game.Beatmap.Value.BeatmapInfo.MD5Hash != normal_md5);
            AddAssert("missing chart explains result", () => Game.Notifications.ChildrenOfType<SimpleNotification>().Any(n => n.Text.ToString().Contains("no longer available", StringComparison.OrdinalIgnoreCase)));
        }

        [Test]
        public void TestBrowserVisualLayout()
        {
            Task? screenshot = null;
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("use Chinese labels", () => Game.Dependencies.Get<FrameworkConfigManager>().SetValue(FrameworkSetting.Locale, "zh"));
            AddStep("open browser", () => overlay.Show());
            AddUntilStep("package and its charts loaded", () => overlay.Cards.SingleOrDefault()?.Package.Charts.Count == 2);
            AddStep("expand package", () => overlay.Cards.Single().ToggleCharts());
            AddWaitStep("allow layout", 4);
            AddAssert("card has visible width and height", () => overlay.Cards.Single().DrawWidth > 300 && overlay.Cards.Single().DrawHeight > 200);
            AddStep("capture browser if requested", () => screenshot = captureScreenshot());
            AddUntilStep("screenshot completes", () => screenshot!.IsCompleted);
            AddStep("surface screenshot failure", () => screenshot!.GetAwaiter().GetResult());
        }

        [TestCase(".bmson", "BEAT_7K")]
        [TestCase(".bme", "KEYBOARD_24K")]
        [TestCase(".bme", "KEYBOARD_24K_DOUBLE")]
        public void TestKnownUnsupportedChartExplainsAvailability(string extension, string mode)
        {
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("provide unsupported first difficulty", () => metadata.Response = async (request, token) =>
            {
                if (!request.RequestUri!.AbsolutePath.EndsWith("selectList", StringComparison.Ordinal))
                    return await defaultMetadata(request, token).ConfigureAwait(false);
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token).ConfigureAwait(false));
                return gingerResponse(body.RootElement.GetProperty("pageRequest").GetProperty("page").GetInt32(), extension, mode);
            });
            AddStep("open browser", () => overlay.Show());
            AddUntilStep("package loaded", () => overlay.Cards.SingleOrDefault()?.Package.Charts.Count == 2);
            AddAssert("unsupported difficulty cannot download", () => !overlay.Cards.Single().ChildrenOfType<SettingsButtonV2>().Single(b => b.Text.ToString() == "Download").Enabled.Value);
            AddAssert("unsupported reason is visible", () => overlay.Cards.Single().ChildrenOfType<osu.Game.Graphics.Sprites.OsuSpriteText>().Any(t => t.Text.ToString().Contains("not supported", StringComparison.OrdinalIgnoreCase)));
            AddStep("select supported difficulty", () => overlay.Cards.Single().SelectChart(another_md5));
            AddAssert("supported difficulty can download", () => overlay.Cards.Single().ChildrenOfType<SettingsButtonV2>().Single(b => b.Text.ToString() == "Download").Enabled.Value);
            AddAssert("no package fetched for unsupported chart", () => packages.Requests, () => Is.Zero);
        }

        private async Task captureScreenshot(string suffix = "")
        {
            string? path = Environment.GetEnvironmentVariable("OMS_BMS_DOWNLOAD_SCREENSHOT");
            if (string.IsNullOrEmpty(path) || host is HeadlessGameHost)
                return;
            if (suffix.Length != 0)
                path = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + suffix + Path.GetExtension(path));
            using Image<Rgba32>? pixels = await host.TakeScreenshotAsync().ConfigureAwait(false);
            Assert.That(pixels, Is.Not.Null);
            await pixels!.SaveAsPngAsync(path).ConfigureAwait(false);
        }

        private static async Task<HttpResponseMessage> defaultMetadata(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("selectList", StringComparison.Ordinal))
            {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token).ConfigureAwait(false));
                return gingerResponse(body.RootElement.GetProperty("pageRequest").GetProperty("page").GetInt32());
            }
            if (request.RequestUri.AbsolutePath.EndsWith("selectHeaderListWithFullInfo", StringComparison.Ordinal))
                return json("[]");
            if (request.RequestUri.AbsolutePath.EndsWith("tables", StringComparison.Ordinal))
                return json("{\"result\":\"success\",\"tables\":[]}");
            return json(JsonSerializer.Serialize(new
            {
                result = "success",
                page = 1,
                page_size = 20,
                total_pages = 1,
                total = 1,
                data = new[] { new { md5 = normal_md5, chart_name = "Unhosted chart", title = "Unhosted chart", artist = "OMS", song_name = "Song", song_url = (string?)null } }
            }));
        }

        private static HttpResponseMessage gingerResponse(int page, string extension = ".bme", string mode = "BEAT_7K") => json(JsonSerializer.Serialize(new
        {
            page,
            pageSize = 20,
            pageCount = 2,
            total = 21,
            data = new[]
            {
                new
                {
                    id = 17, fileName = "OMS Download Test.7z", downloadURL = "https://gingerrush.com/packages/song.7z", fileSize = createPackage().Length,
                    songs = new[] { new { md5 = page == 1 ? normal_md5 : another_md5, fileName = page == 1 ? "normal" + extension : "another.bme", title = "OMS Download Test", artist = "OMS", subTitle = page == 1 ? "[NORMAL]" : "[ANOTHER]", mode = page == 1 ? mode : "BEAT_7K", playLevel = page == 1 ? "3" : "7" } }
                }
            }
        }));

        private static HttpResponseMessage json(string body) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        private static byte[] createPackage()
        {
            using var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                write("Song/normal.bme", normal_chart);
                write("Song/another.bme", another_chart);
                void write(string name, string text)
                {
                    using var output = archive.CreateEntry(name).Open();
                    output.Write(Encoding.UTF8.GetBytes(text));
                }
            }
            return stream.ToArray();
        }

        private static string hash(string text) => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

        private partial class DownloadGame : TestOsuGame
        {
            private readonly StubHandler metadata;
            private readonly StubHandler packages;
            public BmsDownloadManager Manager { get; private set; } = null!;
            public override bool BmsDownloadsEnabled => true;

            public DownloadGame(Storage storage, IAPIProvider api, StubHandler metadata, StubHandler packages)
                : base(storage, api)
            {
                this.metadata = metadata;
                this.packages = packages;
            }

            protected override BmsDownloadManager CreateBmsDownloadManager() => Manager = new BmsDownloadManager(Storage,
                new BmsDownloadImporter(Storage, ClientRealm), new BmsDownloadClient(new HttpClient(metadata)), new HttpClient(packages));
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            private int requests;
            private int searchRequests;
            public int Requests => Volatile.Read(ref requests);
            public int SearchRequests => Volatile.Read(ref searchRequests);
            public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Response { get; set; }

            public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) => Response = response;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref requests);
                if (request.RequestUri!.AbsolutePath.EndsWith("selectList", StringComparison.Ordinal) || request.RequestUri.AbsolutePath.EndsWith("search", StringComparison.Ordinal))
                    Interlocked.Increment(ref searchRequests);
                return Response(request, cancellationToken);
            }
        }
    }
}
