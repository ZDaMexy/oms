// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.Bms;
using osu.Game.Online.Sayobot;
using osu.Game.Overlays;
using osu.Game.Overlays.BeatmapListing;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Overlays
{
    public partial class TestSceneManiaDownload : OsuGameTestScene
    {
        private const int set_id = 17;
        private const int normal_bid = 171;
        private const int another_bid = 172;
        private StubHandler metadata = null!;
        private StubHandler packages = null!;
        private StubHandler bmsMetadata = null!;
        private StubHandler covers = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        private ManiaDownloadOverlay overlay => Game.ManiaDownloads;
        private ManiaDownloadHeader header => overlay.Header;
        private ManiaDownloadManager manager => ((DownloadGame)Game).Manager;

        protected override TestOsuGame CreateTestGame()
        {
            metadata = new StubHandler((request, token) => catalogue(request, token));
            packages = new StubHandler((request, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(createPackage(int.Parse(request.RequestUri!.Segments.Last(), CultureInfo.InvariantCulture))),
            }));
            bmsMetadata = new StubHandler((request, _) => Task.FromResult(json(request.RequestUri!.AbsolutePath.EndsWith("selectHeaderListWithFullInfo", StringComparison.Ordinal)
                ? "[]" : "{\"page\":1,\"pageSize\":20,\"pageCount\":1,\"total\":0,\"data\":[]}")));
            covers = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
            return new DownloadGame(LocalStorage, API, metadata, packages, bmsMetadata, covers);
        }

        [Test]
        public void TestFilteringDuringASlowCoverKeepsTheNewCardUsable()
        {
            var releaseCover = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            ManiaDownloadCard? original = null;
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("hold cover response", () => covers.Response = async (_, token) =>
            {
                await releaseCover.Task.WaitAsync(token).ConfigureAwait(false);
                using var image = new Image<Rgba32>(32, 32, new Rgba32(50, 150, 200));
                using var png = new MemoryStream();
                await image.SaveAsPngAsync(png, token).ConfigureAwait(false);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(png.ToArray()) };
            });
            AddStep("open mania", showMania);
            AddUntilStep("first card waits for cover", () => overlay.Cards.Count == 1 && covers.Requests == 1);
            AddStep("filter while old cover is pending", () =>
            {
                original = overlay.Cards.Single();
                header.Keys.Value = 4;
            });
            AddUntilStep("filtered card replaces old card", () => original!.Parent == null && overlay.Cards.Count == 1 && overlay.Cards.Single() != original
                && overlay.Cards.Single().Set.Beatmaps.Count == 2 && overlay.Cards.Single().IsLoaded);
            AddWaitStep("allow new card to await the same image", 3);
            AddAssert("old card cancellation leaves the shared read running", () => covers.Requests, () => Is.EqualTo(1));
            AddStep("finish cover", () => releaseCover.SetResult());
            AddUntilStep("new card displays completed cover", () => overlay.Cards.Single().ChildrenOfType<Sprite>()
                .Single(sprite => sprite.RelativeSizeAxes == Axes.Both && sprite.FillMode == FillMode.Fill).Texture != null);
            AddStep("expand new card", () => click(cardButton(overlay.Cards.Single(), "Charts")));
            AddUntilStep("filtered difficulties remain usable", () => overlay.Cards.Single().ChildrenOfType<BeatmapCardContent>().Single().Expanded.Value);
        }

        [Test]
        public void TestCoverShutdownCancelsIndependentPendingImages()
        {
            var neverReleased = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            BeatmapDownloadCoverStore? store = null;
            Task<Texture>? first = null;
            Task<Texture>? second = null;
            int cancelledReads = 0;
            AddUntilStep("game and renderer loaded", () => overlay.IsLoaded);
            AddStep("hold independent image reads", () =>
            {
                covers.Response = async (_, token) =>
                {
                    try
                    {
                        await neverReleased.Task.WaitAsync(token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        Interlocked.Increment(ref cancelledReads);
                        throw;
                    }
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                };
                store = new BeatmapDownloadCoverStore(host, new BeatmapDownloadCoverResourceStore(covers));
                first = store.GetAsync("https://a.sayobot.cn/beatmaps/17/covers/cover.jpg", CancellationToken.None);
                second = store.GetAsync("https://a.sayobot.cn/beatmaps/18/covers/cover.jpg", CancellationToken.None);
            });
            AddUntilStep("different covers read concurrently", () => covers.Requests == 2);
            AddStep("release image owner during shutdown", () => store!.Dispose());
            AddUntilStep("shutdown cancels both pending reads", () => first!.IsCanceled && second!.IsCanceled);
            AddAssert("owner cancelled actual image reads", () => Volatile.Read(ref cancelledReads), () => Is.EqualTo(2));
            AddStep("owner release is idempotent", () => store!.Dispose());
        }

        [Test]
        public void TestModeTabsOnlyQueryTheViewedCatalogue()
        {
            int bmsRequests = 0;
            AddUntilStep("download browsers loaded", () => Game.BmsDownloads.IsLoaded && overlay.IsLoaded);
            AddAssert("startup performs no catalogue requests", () => metadata.Requests == 0 && bmsMetadata.Requests == 0);
            AddAssert("official online features remain disabled", () => !Game.OnlineFeaturesEnabled);
            AddStep("open existing browse entrance", () => ((MainMenu)Game.ScreenStack.CurrentScreen).ChildrenOfType<ButtonSystem>().Single().OnBeatmapListing!());
            AddUntilStep("BMS catalogue alone has loaded", () => Game.BmsDownloads.State.Value == Visibility.Visible && bmsMetadata.Requests > 0
                && !Game.BmsDownloads.ChildrenOfType<LoadingLayer>().Single().IsPresent);
            AddAssert("mania was not queried by BMS browsing", () => metadata.Requests, () => Is.Zero);
            AddStep("record BMS requests and click mania tab", () =>
            {
                bmsRequests = bmsMetadata.Requests;
                click(modeTab(Game.BmsDownloads.Header, BeatmapDownloadMode.Mania));
            });
            AddUntilStep("mania catalogue loaded", () => overlay.State.Value == Visibility.Visible && overlay.Cards.Count == 1);
            AddAssert("mode switch hides BMS browser", () => Game.BmsDownloads.State.Value, () => Is.EqualTo(Visibility.Hidden));
            AddAssert("mania uses Sayobot on demand", () => metadata.SearchRequests, () => Is.EqualTo(1));
            AddAssert("mode switch does not query BMS again", () => bmsMetadata.Requests, () => Is.EqualTo(bmsRequests));
            AddStep("click BMS tab", () => click(modeTab(header, BeatmapDownloadMode.Bms)));
            AddUntilStep("BMS browser returns", () => Game.BmsDownloads.State.Value == Visibility.Visible && overlay.State.Value == Visibility.Hidden);
            AddAssert("switching back issues no mania request", () => metadata.SearchRequests, () => Is.EqualTo(1));
        }

        [Test]
        public void TestKeywordKeysStarsAndCategorySelectTheSameOriginalDifficulty()
        {
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("use loved catalogue", () => metadata.Response = (request, token) => catalogue(request, token, approval: 4));
            AddStep("open mania", showMania);
            AddUntilStep("initial set loaded", () => overlay.Cards.Count == 1);
            AddStep("search with linked filters", () =>
            {
                header.Query.Value = "月光 & #%?";
                header.Keys.Value = 4;
                header.Stars.Value = ManiaDownloadHeader.StarRange.TwoToThree;
                header.Category.Value = SayobotCategory.Loved;
            });
            AddUntilStep("only matching native difficulty remains", () => overlay.Cards.SingleOrDefault()?.Set.Beatmaps.SingleOrDefault()?.Id == another_bid);
            AddAssert("source receives the player's query and combined filters", () =>
            {
                using var body = JsonDocument.Parse(metadata.SearchBodies.Last());
                var query = body.RootElement;
                return query.GetProperty("keyword").GetString() == "月光 & #%?" && query.GetProperty("mode").GetInt32() == 8
                    && query.GetProperty("cs").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 4, 4 })
                    && query.GetProperty("stars").EnumerateArray().Select(value => value.GetDouble()).SequenceEqual(new[] { 2.0, 3.0 })
                    && query.GetProperty("class").GetInt32() == 4;
            });
            AddAssert("selected original bid is retained", () => overlay.Cards.Single().SelectedBeatmapId, () => Is.EqualTo(another_bid));
            AddAssert("key and stars were not matched across separate difficulties", () => overlay.Cards.Single().Set.Beatmaps, () => Has.Count.EqualTo(1));
        }

        [Test]
        public void TestSwitchingModeDiscardsACancelledLateCatalogue()
        {
            TaskCompletionSource<HttpResponseMessage> late = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            AddUntilStep("browsers loaded", () => overlay.IsLoaded && Game.BmsDownloads.IsLoaded);
            AddStep("hold mania search even after cancellation", () => metadata.Response = (request, token) =>
                request.Method == HttpMethod.Post && metadata.SearchRequests == 1 ? late.Task : catalogue(request, token));
            AddStep("open mania", showMania);
            AddUntilStep("mania search started", () => metadata.SearchRequests == 1);
            AddStep("click BMS while mania is loading", () => click(modeTab(header, BeatmapDownloadMode.Bms)));
            AddUntilStep("BMS takes over", () => Game.BmsDownloads.State.Value == Visibility.Visible && overlay.State.Value == Visibility.Hidden);
            AddStep("release old mania results", () => late.SetResult(list(0, set_id)));
            AddWaitStep("allow late response callback", 3);
            AddAssert("late results cannot repopulate hidden browser", () => overlay.Cards.Count, () => Is.Zero);
            AddStep("reopen mania through tab", () => click(modeTab(Game.BmsDownloads.Header, BeatmapDownloadMode.Mania)));
            AddUntilStep("fresh mania results loaded", () => overlay.Cards.SingleOrDefault()?.Set.Id == set_id && overlay.State.Value == Visibility.Visible);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestReadFailureRequiresManualSearchRetry(bool keyboard)
        {
            bool fail = true;
            int failedRequests = 0;
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("fail source reads", () => metadata.Response = (request, token) => request.Method == HttpMethod.Post && fail
                ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)) : catalogue(request, token));
            AddStep("open mania", showMania);
            AddUntilStep("source failure offers retry", () => overlay.ChildrenOfType<ShowMoreButton>().Single().Text.ToString() == "Retry");
            AddAssert("query failure stays in the browse page", () => !Game.Notifications.ChildrenOfType<SimpleErrorNotification>().Any(notification =>
                notification.Text.ToString().Contains("Could not search the Sayobot mania catalogue.", StringComparison.Ordinal)));
            AddStep("record failed requests", () => failedRequests = metadata.SearchRequests);
            AddWaitStep("leave failed search untouched", 5);
            AddAssert("source failure is not retried automatically", () => metadata.SearchRequests, () => Is.EqualTo(failedRequests));
            AddStep("prepare retry", () =>
            {
                fail = false;
                header.FocusSearch();
            });
            AddStep(keyboard ? "retry with Enter" : "retry with search icon", () =>
            {
                if (keyboard)
                    InputManager.Key(Key.Enter);
                else
                    click(header.ChildrenOfType<IconButton>().Single(button => button.TooltipText.ToString() == "Search"));
            });
            AddUntilStep("manual retry loaded original set", () => overlay.Cards.SingleOrDefault()?.Set.Id == set_id);
        }

        [TestCase("0")]
        [TestCase("2147483648")]
        public void TestInvalidNumericQueryCanBeCorrectedWithoutLeavingTheBrowser(string query)
        {
            int requests = 0;
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("open mania", showMania);
            AddUntilStep("initial results loaded", () => overlay.Cards.Count == 1);
            AddStep("enter invalid numeric ID", () =>
            {
                requests = metadata.Requests;
                header.Query.Value = query;
            });
            AddUntilStep("invalid input offers retry", () => overlay.ChildrenOfType<ShowMoreButton>().Single().Text.ToString() == "Retry");
            AddAssert("invalid input does not raise a source system notification", () => !Game.Notifications.ChildrenOfType<SimpleErrorNotification>().Any(notification =>
                notification.Text.ToString().Contains("Could not search the Sayobot mania catalogue.", StringComparison.Ordinal)));
            AddAssert("old results are cleared", () => overlay.Cards.Count, () => Is.Zero);
            AddAssert("invalid ID makes no source request", () => metadata.Requests, () => Is.EqualTo(requests));
            AddWaitStep("leave input untouched", 3);
            AddAssert("input is not retried automatically", () => metadata.Requests, () => Is.EqualTo(requests));
            AddStep("correct original set ID", () => header.Query.Value = set_id.ToString(CultureInfo.InvariantCulture));
            AddUntilStep("valid original set returns", () => overlay.Cards.SingleOrDefault()?.Set.Id == set_id);
        }

        [Test]
        public void TestPaginationUsesOriginalCursorAndKeepsExistingCardsInteractive()
        {
            TaskCompletionSource<HttpResponseMessage> nextPage = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            bool retry = false;
            int failedRequests = 0;
            ManiaDownloadTask? task = null;
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("hold second cursor page", () => metadata.Response = async (request, token) =>
            {
                if (request.Method == HttpMethod.Post)
                {
                    using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token).ConfigureAwait(false));
                    int offset = body.RootElement.GetProperty("offset").GetInt32();
                    return offset == 0 ? list(3903, set_id) : retry ? list(0, 18) : await nextPage.Task.WaitAsync(token).ConfigureAwait(false);
                }
                return await catalogue(request, token).ConfigureAwait(false);
            });
            AddStep("open mania", showMania);
            AddUntilStep("first card loaded", () => overlay.Cards.Count == 1);
            AddStep("browse to next page", () => overlay.ChildrenOfType<OverlayScrollContainer>().Single().ScrollToEnd(false));
            AddUntilStep("second page request pending", () => metadata.SearchRequests == 2);
            AddAssert("pagination sends returned cursor", () => metadata.SearchBodies.Select(body => JsonSerializer.Deserialize<JsonElement>(body).GetProperty("offset").GetInt32()),
                () => Is.EqualTo(new[] { 0, 3903 }));
            AddAssert("pending page does not block card input", () => !overlay.ChildrenOfType<LoadingLayer>().Single().IsPresent);
            AddStep("download existing card while page is pending", () =>
            {
                overlay.Cards.Single().SelectBeatmap(another_bid);
                click(cardButton(overlay.Cards.Single(), "Download"));
            });
            AddUntilStep("existing card imported before pagination finishes", () => (task = manager.GetTask(overlay.Cards.Single().Set.Key))?.Completion.IsCompleted == true);
            AddAssert("existing card remains downloadable", () => task!.Progress.State, () => Is.EqualTo(ManiaDownloadState.Completed));
            AddStep("fail next page", () => nextPage.SetResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
            AddUntilStep("pagination offers retry", () => overlay.ChildrenOfType<ShowMoreButton>().Single().Text.ToString() == "Retry");
            AddStep("record failed pagination requests", () => failedRequests = metadata.SearchRequests);
            AddWaitStep("leave failed page untouched", 5);
            AddAssert("failed pagination does not loop", () => metadata.SearchRequests, () => Is.EqualTo(failedRequests));
            AddStep("show retry at bottom", () => overlay.ChildrenOfType<OverlayScrollContainer>().Single().ScrollToEnd(false));
            AddWaitStep("allow retry layout", 2);
            AddStep("retry exact cursor", () =>
            {
                retry = true;
                click(overlay.ChildrenOfType<ShowMoreButton>().Single());
            });
            AddUntilStep("nonempty final page appended", () => overlay.Cards.Count == 2 && overlay.Cards.Any(card => card.Set.Id == 18));
            AddAssert("append preserves clicked original difficulty", () => overlay.Cards.Single(card => card.Set.Id == set_id).SelectedBeatmapId, () => Is.EqualTo(another_bid));
            AddAssert("retry preserves the same cursor", () => JsonSerializer.Deserialize<JsonElement>(metadata.SearchBodies.Last()).GetProperty("offset").GetInt32(), () => Is.EqualTo(3903));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestCancelRetryAndBackgroundDownloadOpensExactOriginalDifficulty(bool notification)
        {
            TaskCompletionSource releasePackage = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            ManiaDownloadTask? cancelled = null;
            ManiaDownloadTask? failed = null;
            ManiaDownloadTask? completed = null;
            Guid target = Guid.Empty;
            Task? screenshot = null;
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("hold original package", () => packages.Response = async (_, token) =>
            {
                await releasePackage.Task.WaitAsync(token).ConfigureAwait(false);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(createPackage(set_id)) };
            });
            AddStep("open mania", showMania);
            AddUntilStep("set and original difficulties loaded", () => overlay.Cards.SingleOrDefault()?.Set.Beatmaps.Count == 3);
            AddUntilStep("card accepts input", () => !overlay.ChildrenOfType<LoadingLayer>().Single().IsPresent);
            AddStep("click difficulty selector", () => click(cardButton(overlay.Cards.Single(), "Charts")));
            AddUntilStep("difficulty list expands", () => overlay.Cards.Single().ChildrenOfType<BeatmapCardContent>().Single().Expanded.Value);
            AddWaitStep("allow expansion layout", 3);
            AddStep("click original another difficulty", () => click(overlay.Cards.Single().ChildrenOfType<ManiaDownloadDifficultyRow>().Single(row => row.Beatmap.Id == another_bid)));
            AddAssert("clicked row selects original bid", () => overlay.Cards.Single().SelectedBeatmapId, () => Is.EqualTo(another_bid));
            AddStep("click download icon", () => click(cardButton(overlay.Cards.Single(), "Download")));
            AddUntilStep("download offers cancel", () => packages.Requests == 1 && hasCardAction("Cancel"));
            AddStep("click cancel icon", () =>
            {
                cancelled = manager.GetTask(overlay.Cards.Single().Set.Key);
                click(cardButton(overlay.Cards.Single(), "Cancel"));
            });
            AddUntilStep("task is cancelled", () => cancelled!.Completion.IsCompleted);
            AddAssert("cancel did not install package", () => cancelled!.Progress.State, () => Is.EqualTo(ManiaDownloadState.Cancelled));
            AddUntilStep("cancelled card can retry", () => hasCardAction("Retry"));
            AddStep("retry with service failure", () =>
            {
                packages.Response = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                click(cardButton(overlay.Cards.Single(), "Retry"));
            });
            AddUntilStep("retry creates new task", () => (failed = manager.GetTask(overlay.Cards.Single().Set.Key)) != cancelled);
            AddUntilStep("failed retry settles", () => failed!.Completion.IsCompleted);
            AddAssert("failed retry retains selected bid", () => failed!.RequestedBeatmapId == another_bid && failed.Progress.State == ManiaDownloadState.Failed);
            AddUntilStep("failed card can retry", () => hasCardAction("Retry"));
            AddUntilStep("player-facing download failure explains retry", () => Game.Notifications.ChildrenOfType<SimpleErrorNotification>().Any(notification =>
                notification.Text.ToString().Contains("OMS Mania Download", StringComparison.Ordinal)
                && notification.Text.ToString().Contains("Download or import failed. Please retry.", StringComparison.Ordinal)));
            AddAssert("download failure has no duplicate source diagnostic", () => !Game.Notifications.ChildrenOfType<SimpleErrorNotification>().Any(notification =>
                notification.Text.ToString().Contains("Sayobot mania package download or import failed.", StringComparison.Ordinal)));
            AddStep("retry original package again", () =>
            {
                packages.Response = async (_, token) =>
                {
                    await releasePackage.Task.WaitAsync(token).ConfigureAwait(false);
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(createPackage(set_id)) };
                };
                click(cardButton(overlay.Cards.Single(), "Retry"));
            });
            AddUntilStep("retried package is downloading", () => (completed = manager.GetTask(overlay.Cards.Single().Set.Key)) != failed && completed!.Progress.State == ManiaDownloadState.Downloading);
            AddStep("capture downloading card if requested", () => screenshot = captureScreenshot("-downloading"));
            AddUntilStep("downloading screenshot completes", () => screenshot!.IsCompleted);
            AddStep("surface screenshot failure", () => screenshot!.GetAwaiter().GetResult());
            AddStep("close page during retry", () => overlay.Hide());
            AddStep("finish retry in background", () => releasePackage.SetResult());
            AddUntilStep("background import completed", () => completed!.Completion.IsCompleted);
            AddAssert("completed without leaving menu", () => completed!.Progress.State == ManiaDownloadState.Completed && Game.ScreenStack.CurrentScreen is MainMenu);
            AddStep("record original selected chart", () => target = completed!.Progress.Imported!.Single(chart => chart.OnlineId == another_bid).BeatmapId);
            AddAssert("native mania package is in direct-read library", () =>
            {
                var beatmap = Game.BeatmapManager.QueryBeatmap("ID == $0", target)!;
                return beatmap.OnlineID == another_bid && beatmap.Ruleset.ShortName == "mania"
                    && beatmap.BeatmapSet!.FilesystemStoragePath!.StartsWith("chartmania", StringComparison.Ordinal);
            });
            AddStep("reopen mania", () => overlay.Show());
            AddUntilStep("downloaded card can open", () => hasCardAction("Open in song select"));
            AddStep("capture completed card if requested", () => screenshot = captureScreenshot("-completed"));
            AddUntilStep("completed screenshot completes", () => screenshot!.IsCompleted);
            AddStep("surface completed screenshot failure", () => screenshot!.GetAwaiter().GetResult());
            AddStep(notification ? "activate completed notification" : "click exact chart open icon", () =>
            {
                if (notification)
                    Game.Notifications.ChildrenOfType<ProgressCompletionNotification>().Single().Activated!();
                else
                    click(cardButton(overlay.Cards.Single(), "Open in song select"));
            });
            AddUntilStep("song select presents exact original difficulty", () => Game.ScreenStack.CurrentScreen is SoloSongSelect select
                && select.CarouselItemsPresented && !select.IsFiltering && Game.Beatmap.Value.BeatmapInfo.ID == target
                && select.ChildrenOfType<BeatmapCarousel>().Single().CurrentBeatmap?.ID == target);
            AddWaitStep("allow selection debounce", 5);
            AddAssert("settled list keeps exact bid and mania mode", () => Game.Beatmap.Value.BeatmapInfo.ID == target && Game.Beatmap.Value.BeatmapInfo.OnlineID == another_bid && Game.Ruleset.Value.ShortName == "mania");
        }

        [Test]
        public void TestBackgroundCompletionAndOpenDoNotInterruptGameplay()
        {
            TaskCompletionSource releasePackage = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<SayobotBeatmapSet>? nextSet = null;
            ManiaDownloadTask? playing = null;
            ManiaDownloadTask? background = null;
            Guid selected = Guid.Empty;
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("open mania", showMania);
            AddUntilStep("original set loaded", () => overlay.Cards.Count == 1);
            AddStep("install chart to play", () => playing = manager.Download(overlay.Cards.Single().Set, normal_bid));
            AddUntilStep("playable chart imported", () => playing!.Completion.IsCompleted);
            AddStep("read next native set", () => nextSet = manager.Client.GetBeatmapSetAsync(18, CancellationToken.None));
            AddUntilStep("next set metadata loaded", () => nextSet!.IsCompleted);
            AddStep("select current chart and start held background package", () =>
            {
                selected = playing!.Progress.Imported!.Single(chart => chart.OnlineId == normal_bid).BeatmapId;
                var beatmap = Game.BeatmapManager.QueryBeatmap("ID == $0", selected)!;
                Game.Ruleset.Value = beatmap.Ruleset;
                Game.Beatmap.Value = Game.BeatmapManager.GetWorkingBeatmap(beatmap);
                overlay.Hide();
                DismissAnyNotifications();
                packages.Response = async (_, token) =>
                {
                    await releasePackage.Task.WaitAsync(token).ConfigureAwait(false);
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(createPackage(18)) };
                };
                background = manager.Download(nextSet!.GetAwaiter().GetResult(), 182);
            });
            PushAndConfirm(() => new TestPlayer(false, false));
            AddStep("finish download while playing", () => releasePackage.SetResult());
            AddUntilStep("background import completed", () => background!.Completion.IsCompleted);
            AddAssert("completed package leaves gameplay current", () => background!.Progress.State == ManiaDownloadState.Completed && Game.ScreenStack.CurrentScreen is Player);
            AddAssert("completion keeps current difficulty", () => Game.Beatmap.Value.BeatmapInfo.ID, () => Is.EqualTo(selected));
            AddAssert("open refuses to interrupt current song", () => !Game.PresentDownloadedManiaBeatmap(background!.Progress.Imported!.Single(chart => chart.OnlineId == 182).BeatmapId));
            AddAssert("refused open keeps gameplay and original selection", () => Game.ScreenStack.CurrentScreen is Player && Game.Beatmap.Value.BeatmapInfo.ID == selected);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestLongTitlesAndExpandedFinalCardsRemainOperable(bool narrow)
        {
            ManiaDownloadCard? first = null;
            ManiaDownloadCard? last = null;
            Task? screenshot = null;
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("provide several rows of long original titles", () =>
            {
                metadata.Response = (request, token) => request.Method == HttpMethod.Post ? Task.FromResult(list(0, Enumerable.Range(1, 12).ToArray()))
                    : catalogue(request, token, longTitles: true);
                Game.Dependencies.Get<FrameworkConfigManager>().SetValue(FrameworkSetting.Locale, "zh");
                overlay.RelativeSizeAxes = Axes.Y;
                overlay.Width = narrow ? 420 : 800;
                showMania();
            });
            AddUntilStep("multiple result rows loaded", () => overlay.Cards.Count == 12);
            AddUntilStep("results accept input", () => !overlay.ChildrenOfType<LoadingLayer>().Single().IsPresent);
            AddStep("browse past filters to first card", () =>
            {
                first = overlay.Cards.First();
                last = overlay.Cards.Last();
                overlay.ChildrenOfType<OverlayScrollContainer>().Single().ScrollTo(header.DrawHeight, false);
            });
            AddWaitStep("allow first card layout", 2);
            AddStep("click first difficulty selector", () => click(cardButton(first!, "Charts")));
            AddUntilStep("first difficulty list expands", () => first!.ChildrenOfType<BeatmapCardContent>().Single().Expanded.Value);
            AddWaitStep("allow first expansion layout", 3);
            AddAssert("expanded choice overlaps a later result row", () => overlay.Cards.Skip(1).Any(card => card.ScreenSpaceDrawQuad.Contains(
                first!.ChildrenOfType<ManiaDownloadDifficultyRow>().ElementAt(1).ScreenSpaceDrawQuad.Centre)));
            AddStep("click difficulty over later card", () => click(first!.ChildrenOfType<ManiaDownloadDifficultyRow>().ElementAt(1)));
            AddAssert("overlap selects original difficulty on first card", () => first!.SelectedBeatmapId, () => Is.EqualTo(first!.Set.Id * 10 + 2));
            AddAssert("other cards did not receive expanded-row click", () => overlay.Cards.Skip(1).All(card => card.SelectedBeatmapId == card.Set.Beatmaps[0].Id));
            AddStep("scroll to last card", () => overlay.ChildrenOfType<OverlayScrollContainer>().Single().ScrollToEnd(false));
            AddWaitStep("allow last card layout", 2);
            AddStep("click final difficulty selector", () => click(cardButton(last!, "Charts")));
            AddUntilStep("final difficulty list expands", () => last!.ChildrenOfType<BeatmapCardContent>().Single().Expanded.Value);
            AddWaitStep("allow final expansion layout", 3);
            AddAssert("final expanded choice remains inside browser", () =>
            {
                var row = last!.ChildrenOfType<ManiaDownloadDifficultyRow>().ElementAt(1);
                var scroll = overlay.ChildrenOfType<OverlayScrollContainer>().Single();
                return row.ScreenSpaceDrawQuad.GetVertices().ToArray().All(vertex => scroll.ScreenSpaceDrawQuad.Contains(vertex));
            });
            AddStep("click final original difficulty", () => click(last!.ChildrenOfType<ManiaDownloadDifficultyRow>().ElementAt(1)));
            AddAssert("final row selects exact original bid", () => last!.SelectedBeatmapId, () => Is.EqualTo(last!.Set.Id * 10 + 2));
            AddStep("download beside long title", () => click(cardButton(last!, "Download")));
            AddUntilStep("last native package imported", () => manager.GetTask(last!.Set.Key)?.Completion.IsCompleted == true);
            AddAssert("last package retains clicked bid", () => manager.GetTask(last!.Set.Key)!.Progress.State == ManiaDownloadState.Completed
                && manager.GetTask(last.Set.Key)!.RequestedBeatmapId == last.SelectedBeatmapId);
            AddStep("capture Chinese long-title layout if requested", () => screenshot = captureScreenshot(narrow ? "-narrow" : "-multi-row"));
            AddUntilStep("layout screenshot completes", () => screenshot!.IsCompleted);
            AddStep("surface layout screenshot failure", () => screenshot!.GetAwaiter().GetResult());
        }

        private void showMania()
        {
            Game.DownloadMode.Value = BeatmapDownloadMode.Mania;
            ((MainMenu)Game.ScreenStack.CurrentScreen).ChildrenOfType<ButtonSystem>().Single().OnBeatmapListing!();
        }

        private static FilterTabItem<BeatmapDownloadMode> modeTab(Drawable parent, BeatmapDownloadMode mode) =>
            parent.ChildrenOfType<FilterTabItem<BeatmapDownloadMode>>().Single(tab => tab.Value == mode);

        private static BeatmapDownloadCardButton cardButton(ManiaDownloadCard card, string tooltip) =>
            card.ChildrenOfType<BeatmapDownloadCardButton>().Single(button => button.TooltipText.ToString() == tooltip);

        private bool hasCardAction(string tooltip) => overlay.Cards.Single().ChildrenOfType<BeatmapDownloadCardButton>().Any(button => button.TooltipText.ToString() == tooltip);

        private void click(Drawable drawable)
        {
            InputManager.MoveMouseTo(drawable);
            InputManager.Click(MouseButton.Left);
        }

        private async Task captureScreenshot(string suffix = "")
        {
            string? path = Environment.GetEnvironmentVariable("OMS_MANIA_DOWNLOAD_SCREENSHOT");
            if (string.IsNullOrEmpty(path) || host is HeadlessGameHost)
                return;
            if (suffix.Length != 0)
                path = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + suffix + Path.GetExtension(path));
            using Image<Rgba32>? pixels = await host.TakeScreenshotAsync().ConfigureAwait(false);
            Assert.That(pixels, Is.Not.Null);
            await pixels!.SaveAsPngAsync(path).ConfigureAwait(false);
        }

        private static Task<HttpResponseMessage> catalogue(HttpRequestMessage request, CancellationToken token, int approval = 1, bool longTitles = false)
        {
            token.ThrowIfCancellationRequested();
            if (request.Method == HttpMethod.Post)
                return Task.FromResult(list(0, set_id));
            int id = int.Parse(request.RequestUri!.Query[3..], CultureInfo.InvariantCulture);
            string title = longTitles ? "很长的歌曲标题 Original mania song title with additional author information " + id : "OMS Mania Download";
            return Task.FromResult(json(JsonSerializer.Serialize(new
            {
                status = 0,
                data = new
                {
                    sid = id,
                    approved = approval,
                    title,
                    artist = longTitles ? "很长的作者名字 Original artist name with extra information" : "OMS",
                    creator = "OMS Test Mapper",
                    bid_data = new[]
                    {
                        new { bid = id * 10 + 1, version = "Normal 7K", CS = 7, star = 1.5, mode = 3 },
                        new { bid = id * 10 + 2, version = "Another 4K with original difficulty name", CS = 4, star = 2.5, mode = 3 },
                        new { bid = id * 10 + 3, version = "Hard 4K", CS = 4, star = 4.5, mode = 3 },
                    },
                },
            })));
        }

        private static HttpResponseMessage list(int nextOffset, params int[] ids) => json(JsonSerializer.Serialize(new
        {
            status = 0,
            endid = nextOffset,
            data = ids.Select(id => new { sid = id }).ToArray(),
        }));

        private static HttpResponseMessage json(string body) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        private static byte[] createPackage(int setId)
        {
            using var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                write("Song/normal.osu", Encoding.UTF8.GetBytes(createChart(setId, setId * 10 + 1, 7, "Normal 7K")));
                write("Song/another.osu", Encoding.UTF8.GetBytes(createChart(setId, setId * 10 + 2, 4, "Another 4K")));
                write("Song/hard.osu", Encoding.UTF8.GetBytes(createChart(setId, setId * 10 + 3, 4, "Hard 4K")));
                write("Song/audio.wav", createAudio());
                void write(string name, byte[] content)
                {
                    using var output = zip.CreateEntry(name).Open();
                    output.Write(content);
                }
            }
            return stream.ToArray();
        }

        private static string createChart(int setId, int bid, int keys, string difficulty) => $@"osu file format v14

[General]
AudioFilename: audio.wav
Mode: 3

[Metadata]
Title: OMS Mania Download
Artist: OMS
Creator: OMS Test Mapper
Version: {difficulty}
BeatmapID: {bid}
BeatmapSetID: {setId}

[Difficulty]
HPDrainRate: 5
CircleSize: {keys}
OverallDifficulty: 5
ApproachRate: 5
SliderMultiplier: 1.4
SliderTickRate: 1

[TimingPoints]
0,500,4,1,0,100,1,0

[HitObjects]
64,192,1000,1,0,0:0:0:0:
192,192,2000,1,0,0:0:0:0:
";

        private static byte[] createAudio()
        {
            const int dataBytes = 44100 * 2 * 5;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(dataBytes + 36);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(44100);
            writer.Write(88200);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataBytes);
            writer.Write(new byte[dataBytes]);
            return stream.ToArray();
        }

        private partial class DownloadGame : TestOsuGame
        {
            private readonly StubHandler metadata;
            private readonly StubHandler packages;
            private readonly StubHandler bmsMetadata;
            private readonly StubHandler covers;
            public ManiaDownloadManager Manager { get; private set; } = null!;
            public override bool BmsDownloadsEnabled => true;
            public override bool ManiaDownloadsEnabled => true;

            public DownloadGame(Storage storage, IAPIProvider api, StubHandler metadata, StubHandler packages, StubHandler bmsMetadata, StubHandler covers)
                : base(storage, api)
            {
                this.metadata = metadata;
                this.packages = packages;
                this.bmsMetadata = bmsMetadata;
                this.covers = covers;
            }

            protected override ManiaDownloadManager CreateManiaDownloadManager() => Manager = new ManiaDownloadManager(Storage,
                new ManiaDownloadImporter(Storage, ClientRealm), new SayobotClient(new HttpClient(metadata)), new HttpClient(packages));

            protected override BmsDownloadManager CreateBmsDownloadManager() => new BmsDownloadManager(Storage,
                new BmsDownloadImporter(Storage, ClientRealm), new BmsDownloadClient(new HttpClient(bmsMetadata)), new HttpClient(new StubHandler((_, _) =>
                    Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)))));

            protected override ManiaDownloadOverlay CreateManiaDownloadOverlay() => new ManiaDownloadOverlay(new BeatmapDownloadCoverResourceStore(covers));
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            private int requests;
            private int searchRequests;
            public int Requests => Volatile.Read(ref requests);
            public int SearchRequests => Volatile.Read(ref searchRequests);
            public readonly ConcurrentQueue<string> SearchBodies = new ConcurrentQueue<string>();
            public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Response { get; set; }

            public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) => Response = response;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref requests);
                if (request.Method == HttpMethod.Post)
                {
                    SearchBodies.Enqueue(await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
                    Interlocked.Increment(ref searchRequests);
                }
                return await Response(request, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
