// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
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
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Online.API;
using osu.Game.Online.Bms;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Toolbar;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select;
using osu.Game.Tests.Resources;
using osuTK.Input;
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
        private readonly ConcurrentQueue<LogEntry> logEntries = new ConcurrentQueue<LogEntry>();

        [Resolved]
        private GameHost host { get; set; } = null!;
        private BmsDownloadOverlay overlay => Game.BmsDownloads;
        private BmsDownloadHeader header => overlay.ChildrenOfType<BmsDownloadHeader>().Single();
        private BmsDownloadManager manager => ((DownloadGame)Game).Manager;
        private OsuDropdown<string> tableDropdown => header.ChildrenOfType<OsuDropdown<string>>().Single();
        private OsuDropdown<BmsDownloadHeader.TableLevel> levelDropdown => header.ChildrenOfType<OsuDropdown<BmsDownloadHeader.TableLevel>>().Single();

        protected override TestOsuGame CreateTestGame()
        {
            metadata = new StubHandler(defaultMetadata);
            packages = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(createPackage()) }));
            return new DownloadGame(LocalStorage, API, metadata, packages);
        }

        [SetUp]
        public void SetUpLogReceipt() => logEntries.Clear();

        [TearDown]
        public void TearDownLogReceipt() => Logger.NewEntry -= captureLog;

        private void captureLog(LogEntry entry) => logEntries.Enqueue(entry);

        [Test]
        public void TestEntrancesAreAvailableWithoutOfficialOnlineFeatures()
        {
            AddUntilStep("browser and toolbar loaded", () => overlay.IsLoaded && Game.Toolbar.IsLoaded);
            AddAssert("official online features stay disabled", () => !Game.OnlineFeaturesEnabled);
            AddAssert("startup made no source request", () => metadata.Requests, () => Is.Zero);
            AddStep("show toolbar", () => Game.Toolbar.Show());
            AddWaitStep("allow toolbar layout", 2);
            AddAssert("browse entrance is before music and notifications", () =>
                Game.Toolbar.ChildrenOfType<ToolbarBmsDownloadButton>().Single().ScreenSpaceDrawQuad.Centre.X
                < Game.Toolbar.ChildrenOfType<ToolbarMusicButton>().Single().ScreenSpaceDrawQuad.Centre.X
                && Game.Toolbar.ChildrenOfType<ToolbarMusicButton>().Single().ScreenSpaceDrawQuad.Centre.X
                < Game.Toolbar.ChildrenOfType<ToolbarClock>().Single().ScreenSpaceDrawQuad.Centre.X
                && Game.Toolbar.ChildrenOfType<ToolbarClock>().Single().ScreenSpaceDrawQuad.Centre.X
                < Game.Toolbar.ChildrenOfType<ToolbarNotificationButton>().Single().ScreenSpaceDrawQuad.Centre.X);
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
        public void TestFirstDownloadOffersExactChartBeforeRealmNotification(bool another)
        {
            string requestedMd5 = another ? another_md5 : normal_md5;
            Guid target = Guid.Empty;
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("open browser", () => overlay.Show());
            AddUntilStep("package and both charts loaded", () => overlay.Cards.SingleOrDefault()?.Package.Charts.Count == 2);
            AddUntilStep("card loaded", () => overlay.Cards.Single().IsLoaded);
            AddStep("complete first download before realm notifications", () =>
            {
                var card = overlay.Cards.Single();
                card.SelectChart(requestedMd5);
                Game.Realm.Realm.Refresh();
                Assert.That(Game.BeatmapManager.QueryBeatmap("Ruleset.ShortName == $0 AND MD5Hash == $1", "bms", requestedMd5), Is.Null);

                var task = manager.Download(card.Package, requestedMd5);
                Assert.That(task.Completion.Wait(TimeSpan.FromSeconds(10)), Is.True, "background import must finish without pumping the update thread");
                task.Completion.GetAwaiter().GetResult();
                Assert.That(task.Progress.State, Is.EqualTo(BmsDownloadState.Completed));
                target = task.Progress.Imported!.Single(chart => chart.Md5 == requestedMd5).BeatmapId;
                Assert.That(Game.BeatmapManager.QueryBeatmap("ID == $0", target), Is.Null, "the update realm has not yet observed the completed background write");

                // Run the overlay's actual completion update before the update thread processes Realm's notification.
                overlay.UpdateSubTree();
                Assert.That(card.ChildrenOfType<BeatmapDownloadCardButton>().Any(button => button.TooltipText.ToString() == "Open in song select"), Is.True);
                Assert.That(packages.Requests, Is.EqualTo(1));
            });
            AddUntilStep("single completion notification", () => Game.Notifications.ChildrenOfType<ProgressCompletionNotification>().Count() == 1);
            AddUntilStep("card accepts input", () => !overlay.ChildrenOfType<LoadingLayer>().Single().IsPresent);
            AddStep("click first completed download", () => click(cardButton(overlay.Cards.Single(), "Open in song select")));
            AddUntilStep("exact selected chart opens", () => Game.ScreenStack.CurrentScreen is SoloSongSelect && Game.Beatmap.Value.BeatmapInfo.ID == target);
            AddUntilStep("song list settled", () => Game.ScreenStack.CurrentScreen is SoloSongSelect select && select.CarouselItemsPresented && !select.IsFiltering);
            AddWaitStep("allow selection debounce", 5);
            AddAssert("settled selection keeps original chart", () => Game.Beatmap.Value.BeatmapInfo.ID, () => Is.EqualTo(target));
            AddAssert("first download was enough", () => packages.Requests, () => Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestCompletedUnavailableChartDoesNotOfferPlay(bool unavailable)
        {
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("open browser", () => overlay.Show());
            AddUntilStep("package and both charts loaded", () => overlay.Cards.SingleOrDefault()?.Package.Charts.Count == 2);
            AddUntilStep("card loaded", () => overlay.Cards.Single().IsLoaded);
            AddStep("make completed chart unavailable before card refresh", () =>
            {
                var card = overlay.Cards.Single();
                card.SelectChart(another_md5);
                Game.Realm.Realm.Refresh();
                var task = manager.Download(card.Package, another_md5);
                Assert.That(task.Completion.Wait(TimeSpan.FromSeconds(10)), Is.True);
                task.Completion.GetAwaiter().GetResult();
                Assert.That(task.Progress.State, Is.EqualTo(BmsDownloadState.Completed));
                Guid target = task.Progress.Imported!.Single(chart => chart.Md5 == another_md5).BeatmapId;
                var remove = Task.Run(() => Game.Realm.Write(realm =>
                {
                    var set = realm.Find<BeatmapInfo>(target)!.BeatmapSet!;
                    if (unavailable)
                        set.FilesystemUnavailable = true;
                    else
                        set.DeletePending = true;
                }));
                Assert.That(remove.Wait(TimeSpan.FromSeconds(10)), Is.True);
                remove.GetAwaiter().GetResult();

                overlay.UpdateSubTree();
                Assert.That(card.ChildrenOfType<BeatmapDownloadCardButton>().Any(button => button.TooltipText.ToString() == "Open in song select"), Is.False);
                Assert.That(cardButton(card, "Retry").Enabled.Value, Is.True);
                Assert.That(packages.Requests, Is.EqualTo(1));
            });
            AddUntilStep("completion notification arrives", () => Game.Notifications.ChildrenOfType<ProgressCompletionNotification>().Count() == 1);
            AddStep("open removed chart from completion notification", () => Game.Notifications.ChildrenOfType<ProgressCompletionNotification>().Single().Activated!());
            AddUntilStep("unavailable chart explains result", () => Game.Notifications.ChildrenOfType<SimpleNotification>().Any(notification =>
                notification.Text.ToString().Contains("no longer available", StringComparison.OrdinalIgnoreCase)));
            AddAssert("notification leaves menu current", () => Game.ScreenStack.CurrentScreen is MainMenu);
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
            AddUntilStep("loaded card accepts input", () => !overlay.ChildrenOfType<LoadingLayer>().Single().IsPresent);
            AddStep("click chart selector", () => click(cardButton(overlay.Cards.Single(), "Charts")));
            AddUntilStep("chart list expanded", () => overlay.Cards.Single().ChildrenOfType<BeatmapCardContent>().Single().Expanded.Value);
            AddWaitStep("allow expansion layout", 3);
            AddStep("click exact difficulty row", () => click(overlay.Cards.Single().ChildrenOfType<BmsDownloadChartRow>().Single(row => row.Chart.Md5 == requestedMd5)));
            AddAssert("clicked row selects original MD5", () => overlay.Cards.Single().SelectedMd5, () => Is.EqualTo(requestedMd5));
            AddStep("click download icon", () => click(cardButton(overlay.Cards.Single(), "Download")));
            AddUntilStep("package request started", () => packages.Requests == 1 && (task = manager.GetTask(overlay.Cards.Single().Package.Key)) != null);
            AddAssert("download uses clicked original difficulty", () => task!.RequestedMd5, () => Is.EqualTo(requestedMd5));
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
            AddUntilStep("card is ready to open", () => overlay.Cards.Single().ChildrenOfType<BeatmapDownloadCardButton>().Any(b => b.TooltipText.ToString().Contains("song select", StringComparison.OrdinalIgnoreCase)));
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
            AddAssert("missing package has no download action", () => !cardButton(overlay.Cards.Single(), "Download").Enabled.Value);
            AddUntilStep("missing-package card accepts input", () => !overlay.ChildrenOfType<LoadingLayer>().Single().IsPresent);
            AddStep("click unavailable download icon", () => click(cardButton(overlay.Cards.Single(), "Download")));
            AddAssert("unavailable action fetched no package", () => packages.Requests, () => Is.Zero);
        }

        [TestCase(BmsDownloadSource.Ginger)]
        [TestCase(BmsDownloadSource.Konmai)]
        public void TestSourceTableAndLevelSelectExactChartAndOpen(BmsDownloadSource source)
        {
            BmsDownloadTask? task = null;
            Guid target = Guid.Empty;
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("provide tables and select source", () =>
            {
                metadata.Response = tableMetadata;
                header.Source.Value = source;
                overlay.Show();
            });
            AddUntilStep("selected source tables loaded", () => tableDropdown.Items.Contains(tableId(source)));
            AddAssert("grades require a selected table", () => header.Level.Disabled);
            AddStep("select difficulty table", () => header.Table.Value = tableId(source));
            AddUntilStep("full table grades loaded", () => !header.Level.Disabled);
            AddAssert("table order retains custom and empty grades", () => levelDropdown.Items.Select(item => item.Value),
                () => Is.EqualTo(new string?[] { null, "2", "1", "?", "" }));
            AddStep("select table grade two", () => header.Level.Value = new BmsDownloadHeader.TableLevel("2"));
            AddUntilStep("only the matching original chart is shown", () => overlay.Cards.SingleOrDefault()?.Package.Charts.SingleOrDefault()?.Md5 == another_md5);
            AddAssert("other grade in same package stays out of results", () => overlay.Cards.Single().Package.Charts, () => Has.Count.EqualTo(1));
            if (source == BmsDownloadSource.Ginger)
                AddAssert("author play level remains separate", () => overlay.Cards.Single().Package.Charts.Single().Level, () => Is.EqualTo("7"));
            AddUntilStep("filter loading no longer intercepts buttons", () => !overlay.ChildrenOfType<LoadingLayer>().Single().IsPresent);
            AddStep("download from filtered card", () => click(cardButton(overlay.Cards.Single(), "Download")));
            AddUntilStep("filtered chart package imported", () => (task = manager.GetTask(overlay.Cards.Single().Package.Key))?.Completion.IsCompleted == true);
            AddAssert("download succeeded", () => task!.Progress.State, () => Is.EqualTo(BmsDownloadState.Completed));
            AddStep("record selected original chart", () => target = task!.Progress.Imported!.First(chart => chart.Md5 == another_md5).BeatmapId);
            AddUntilStep("filtered card can open exact chart", () => overlay.Cards.Single().ChildrenOfType<BeatmapDownloadCardButton>().Any(button => button.TooltipText.ToString().Contains("song select", StringComparison.OrdinalIgnoreCase)));
            AddStep("open selected chart", () => click(cardButton(overlay.Cards.Single(), "Open in song select")));
            AddUntilStep("song list settled on selected original chart", () => Game.ScreenStack.CurrentScreen is SoloSongSelect select && select.CarouselItemsPresented && !select.IsFiltering
                && select.ChildrenOfType<BeatmapCarousel>().Single().CurrentBeatmap?.ID == target && Game.Beatmap.Value.BeatmapInfo.ID == target);
            AddAssert("BMS play mode selected", () => Game.Ruleset.Value.ShortName, () => Is.EqualTo("bms"));
        }

        [Test]
        public void TestChangingTableAndSourceClearsGradeAndDiscardsLateTableData()
        {
            TaskCompletionSource<HttpResponseMessage> oldTable = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            bool oldTableStarted = false;
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("hold first table even after cancellation", () => metadata.Response = async (request, token) =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("selectDataList", StringComparison.Ordinal))
                {
                    using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token).ConfigureAwait(false));
                    if (body.RootElement.GetProperty("headerID").GetInt32() == 1)
                    {
                        oldTableStarted = true;
                        return await oldTable.Task.ConfigureAwait(false);
                    }
                }
                return await tableMetadata(request, token).ConfigureAwait(false);
            });
            AddStep("open browser", () => overlay.Show());
            AddUntilStep("tables loaded", () => tableDropdown.Items.Contains("1"));
            AddStep("select first table", () => header.Table.Value = "1");
            AddUntilStep("first table read pending", () => oldTableStarted);
            AddStep("select second table", () => header.Table.Value = "2");
            AddUntilStep("second table grade available", () => !header.Level.Disabled && levelDropdown.Items.Any(item => item.Value == "Other"));
            AddStep("select second table grade", () => header.Level.Value = new BmsDownloadHeader.TableLevel("Other"));
            AddStep("release old table", () => oldTable.SetResult(gingerTableData(1)));
            AddWaitStep("allow old response to finish", 3);
            AddAssert("old levels did not replace selected table", () => levelDropdown.Items.Select(item => item.Value), () => Is.EqualTo(new string?[] { null, "Other" }));
            AddAssert("second table grade remains selected", () => header.Level.Value.Value, () => Is.EqualTo("Other"));
            AddStep("return to all tables", () => header.Table.Value = string.Empty);
            AddAssert("clearing table clears and disables grade", () => header.Level.Value.Value == null && header.Level.Disabled);
            AddStep("switch source", () => header.Source.Value = BmsDownloadSource.Konmai);
            AddAssert("new source starts without table or grade", () => header.Table.Value.Length == 0 && header.Level.Value.Value == null && header.Level.Disabled);
            AddUntilStep("new source table list replaces old list", () => tableDropdown.Items.Contains(tableId(BmsDownloadSource.Konmai)) && !tableDropdown.Items.Contains("2"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestTableGradeReadFailureRequiresManualRetryAndUngradedIsSelectable(bool keyboard)
        {
            bool fail = true;
            int tableReads = 0;
            int failedReads = 0;
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("fail selected table metadata", () => metadata.Response = async (request, token) =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("selectDataList", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref tableReads);
                    if (fail)
                        return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                }
                return await tableMetadata(request, token).ConfigureAwait(false);
            });
            AddStep("open browser", () => overlay.Show());
            AddUntilStep("tables loaded", () => tableDropdown.Items.Contains("1"));
            AddStep("select table", () => header.Table.Value = "1");
            AddUntilStep("grade failure shown", () => levelDropdown.ChildrenOfType<OsuSpriteText>().Any(text => text.Text.ToString().Contains("Could not read levels", StringComparison.Ordinal)));
            AddStep("record failed table reads", () => failedReads = tableReads);
            AddWaitStep("leave failed table without retry", 5);
            AddAssert("failed table is not retried automatically", () => tableReads, () => Is.EqualTo(failedReads));
            AddAssert("grade remains unavailable", () => header.Level.Disabled && header.Level.Value.Value == null);
            AddStep("prepare manual retry", () =>
            {
                fail = false;
                if (keyboard)
                    header.FocusSearch();
            });
            AddStep(keyboard ? "retry with search Enter" : "retry with search icon", () =>
            {
                if (keyboard)
                    InputManager.Key(Key.Enter);
                else
                    click(header.ChildrenOfType<IconButton>().Single());
            });
            AddUntilStep("grade list available after retry", () => !header.Level.Disabled);
            AddStep("select genuinely empty grade", () => header.Level.Value = new BmsDownloadHeader.TableLevel(""));
            AddUntilStep("ungraded chart shown without other grades", () => overlay.Cards.SingleOrDefault()?.Package.Charts.SingleOrDefault()?.Md5 == normal_md5);
            AddAssert("empty grade differs from all levels", () => header.Level.Value.Value, () => Is.EqualTo(""));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestTableDirectoryFailureCanBeManuallyRetriedWithoutLosingSearchResults(bool keyboard)
        {
            bool fail = true;
            int tableReads = 0;
            int searchesBeforeRetry = 0;
            TaskCompletionSource releaseSongs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("fail the directory before songs finish", () => metadata.Response = async (request, token) =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("selectHeaderListWithFullInfo", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref tableReads);
                    if (Volatile.Read(ref fail))
                        return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                }
                if (request.RequestUri.AbsolutePath.EndsWith("selectList", StringComparison.Ordinal))
                    await releaseSongs.Task.WaitAsync(token).ConfigureAwait(false);
                return await tableMetadata(request, token).ConfigureAwait(false);
            });
            AddStep("open browser at a supported width", () =>
            {
                Game.Dependencies.Get<FrameworkConfigManager>().SetValue(FrameworkSetting.Locale, "en");
                overlay.RelativeSizeAxes = Axes.Y;
                overlay.Width = keyboard ? 800 : 420;
                overlay.Show();
            });
            AddUntilStep("directory failure remains visible during song loading", () => hasVisibleFallbackText("Tables unavailable"));
            AddAssert("directory retry warning fits the content width", () =>
            {
                var warning = overlay.ChildrenOfType<OsuSpriteText>().Single(sprite => sprite.IsPresent && sprite.Text.ToString().StartsWith("Tables unavailable", StringComparison.Ordinal));
                return warning.DrawWidth <= warning.Parent!.ChildSize.X;
            });
            AddStep("finish the successful song query", () => releaseSongs.SetResult());
            AddUntilStep("songs finish despite the directory failure", () => overlay.Cards.Count == 1 && !overlay.ChildrenOfType<LoadingLayer>().Single().IsPresent);
            AddAssert("song success does not clear the directory warning", () => hasVisibleFallbackText("Tables unavailable"));
            AddAssert("failed directory has no selectable table", () => !tableDropdown.Items.Contains("1"));
            AddWaitStep("leave the directory failure untouched", 5);
            AddAssert("failed directory does not retry itself", () => Volatile.Read(ref tableReads), () => Is.EqualTo(1));
            AddStep("recover the table service", () =>
            {
                Volatile.Write(ref fail, false);
                searchesBeforeRetry = metadata.SearchRequests;
                if (keyboard)
                    header.FocusSearch();
            });
            AddStep(keyboard ? "retry with Enter" : "retry with the search icon", () =>
            {
                if (keyboard)
                    InputManager.Key(Key.Enter);
                else
                    click(header.ChildrenOfType<IconButton>().Single());
            });
            AddUntilStep("manual retry sends a song query and restores the directory", () => metadata.SearchRequests > searchesBeforeRetry && tableDropdown.Items.Contains("1"));
            AddAssert("manual retry reads the directory exactly once", () => Volatile.Read(ref tableReads), () => Is.EqualTo(2));
            AddAssert("directory success clears its own warning", () => !hasVisibleFallbackText("Tables unavailable"));
            AddStep("search again after recovery", () => header.Query.Value = "OMS");
            AddUntilStep("normal search completes", () => overlay.Cards.Count == 1 && !overlay.ChildrenOfType<LoadingLayer>().Single().IsPresent);
            AddAssert("a healthy directory is not read on query changes", () => Volatile.Read(ref tableReads), () => Is.EqualTo(2));
            AddStep("select the recovered table", () => header.Table.Value = "1");
            AddUntilStep("recovered table levels become selectable", () => !header.Level.Disabled);
            AddStep("select the recovered table's level two", () => header.Level.Value = new BmsDownloadHeader.TableLevel("2"));
            AddUntilStep("only the chosen original difficulty is shown", () => overlay.Cards.SingleOrDefault()?.Package.Charts.SingleOrDefault()?.Md5 == another_md5);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestPendingDirectoryRetryDoesNotPublishAfterHidingOrChangingSource(bool changeSource)
        {
            int tableReads = 0;
            TaskCompletionSource<HttpResponseMessage> late = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("fail the directory then hold its retry despite cancellation", () => metadata.Response = async (request, token) =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("selectHeaderListWithFullInfo", StringComparison.Ordinal))
                {
                    int requestNumber = Interlocked.Increment(ref tableReads);
                    if (requestNumber == 1)
                        return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                    if (requestNumber == 2)
                        return await late.Task.ConfigureAwait(false);
                }
                return await tableMetadata(request, token).ConfigureAwait(false);
            });
            AddStep("open browser", () => overlay.Show());
            AddUntilStep("directory failed but songs remain usable", () => hasVisibleFallbackText("Tables unavailable") && overlay.Cards.Count == 1
                && !overlay.ChildrenOfType<LoadingLayer>().Single().IsPresent);
            AddStep("retry the directory", () => click(header.ChildrenOfType<IconButton>().Single()));
            AddUntilStep("directory retry is still pending", () => Volatile.Read(ref tableReads) == 2);
            AddStep(changeSource ? "choose a different source" : "close during directory retry", () =>
            {
                if (changeSource)
                    header.Source.Value = BmsDownloadSource.Konmai;
                else
                    overlay.Hide();
            });
            if (changeSource)
                AddUntilStep("new source's directory loaded", () => tableDropdown.Items.Contains(tableId(BmsDownloadSource.Konmai)));
            AddStep("release the old directory response", () => late.SetResult(json("[{\"id\":1,\"name\":\"Old Source Table\",\"originalURL\":\"\"}]")));
            AddWaitStep("allow the late response callback", 3);
            AddAssert("the old directory cannot repopulate the browser", () => !tableDropdown.Items.Contains("1"));
            if (changeSource)
            {
                AddAssert("new source keeps its own directory", () => tableDropdown.Items.Contains(tableId(BmsDownloadSource.Konmai)));
                AddAssert("old source's failure warning is cleared", () => !hasVisibleFallbackText("Tables unavailable"));
            }
            else
            {
                AddAssert("late response keeps the browser closed", () => overlay.State.Value, () => Is.EqualTo(Visibility.Hidden));
                AddStep("reopen after the abandoned retry", () => overlay.Show());
                AddUntilStep("fresh directory becomes available", () => tableDropdown.Items.Contains("1") && !hasVisibleFallbackText("Tables unavailable"));
                AddAssert("reopening made a fresh directory request", () => Volatile.Read(ref tableReads), () => Is.EqualTo(3));
            }
        }

        [Test]
        public void TestDirectoryRecoveryDoesNotHideAnIndependentSearchFailure()
        {
            bool failDirectory = true;
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("fail directory and song reads independently", () => metadata.Response = async (request, token) =>
            {
                string path = request.RequestUri!.AbsolutePath;
                if (path.EndsWith("selectList", StringComparison.Ordinal)
                    || (path.EndsWith("selectHeaderListWithFullInfo", StringComparison.Ordinal) && Volatile.Read(ref failDirectory)))
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                return await tableMetadata(request, token).ConfigureAwait(false);
            });
            AddStep("open browser", () => overlay.Show());
            AddUntilStep("both independent failures are visible", () => hasVisibleFallbackText("Tables unavailable") && hasVisibleFallbackText("Could not load this source"));
            AddStep("recover only the directory and retry", () =>
            {
                Volatile.Write(ref failDirectory, false);
                click(header.ChildrenOfType<IconButton>().Single());
            });
            AddUntilStep("directory is selectable and song search still failed", () => tableDropdown.Items.Contains("1") && hasVisibleFallbackText("Could not load this source"));
            AddAssert("only the recovered directory warning is cleared", () => !hasVisibleFallbackText("Tables unavailable"));
            AddAssert("song retry remains available", () => overlay.ChildrenOfType<ShowMoreButton>().Single().Text.ToString(), () => Is.EqualTo("Retry"));
        }

        [TestCase("tables", "Could not load BMS download source tables.")]
        [TestCase("search", "Could not search the selected BMS download source.")]
        [TestCase("levels", "Could not read the selected BMS table's levels.")]
        [TestCase("download", "BMS package download or import failed.")]
        public void TestHandledFailureKeepsFullNetworkLogWithoutDuplicateSystemNotification(string kind, string context)
        {
            var failure = new HttpRequestException("Full BMS source diagnostic " + new string('x', 400) + " end marker",
                new IOException("Full underlying source diagnostic."), HttpStatusCode.ServiceUnavailable);
            BmsDownloadTask? task = null;
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("observe full logging and use Chinese feedback", () =>
            {
                Logger.NewEntry += captureLog;
                Game.Dependencies.Get<FrameworkConfigManager>().SetValue(FrameworkSetting.Locale, "zh");
                metadata.Response = (request, token) =>
                {
                    string path = request.RequestUri!.AbsolutePath;
                    bool expectedFailure = kind switch
                    {
                        "tables" => path.EndsWith("selectHeaderListWithFullInfo", StringComparison.Ordinal),
                        "search" => path.EndsWith("selectList", StringComparison.Ordinal),
                        "levels" => path.EndsWith("selectDataList", StringComparison.Ordinal),
                        _ => false,
                    };
                    return expectedFailure ? Task.FromException<HttpResponseMessage>(failure) : tableMetadata(request, token);
                };
                if (kind == "download")
                    packages.Response = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            });
            AddStep("open browser", () => overlay.Show());
            switch (kind)
            {
                case "tables":
                    AddUntilStep("directory failure is explained in Chinese", () => hasVisibleLocalisedText("难度表暂不可用，点搜索重试。"));
                    break;

                case "search":
                    AddUntilStep("query failure is explained in Chinese", () => hasVisibleLocalisedText("暂时无法读取此来源，请重试。"));
                    break;

                case "levels":
                    AddUntilStep("table is available", () => tableDropdown.Items.Contains("1"));
                    AddStep("read selected table levels", () => header.Table.Value = "1");
                    AddUntilStep("level failure is explained in Chinese", () => hasVisibleLocalisedText("等级读取失败，点搜索重试。"));
                    break;

                case "download":
                    AddUntilStep("original package loaded", () => overlay.Cards.Count == 1);
                    AddStep("download a package whose server returns 503", () => task = manager.Download(overlay.Cards.Single().Package, normal_md5));
                    AddUntilStep("package failure settles", () => task!.Completion.IsCompleted && task.Progress.State == BmsDownloadState.Failed);
                    AddUntilStep("package failure is explained in Chinese", () => Game.Notifications.AllNotifications.OfType<SimpleErrorNotification>().Any(notification =>
                        Game.Dependencies.Get<LocalisationManager>().GetLocalisedString(notification.Text).Contains("下载或入库失败，请重试。", StringComparison.Ordinal)));
                    break;
            }
            AddUntilStep("complete exception is retained in the Network log", () => logEntries.Any(entry =>
                entry.Target == LoggingTarget.Network && entry.Level == LogLevel.Verbose
                                                     && (kind == "download"
                                                         ? entry.Message.StartsWith(context + "\nSystem.Net.Http.HttpRequestException:", StringComparison.Ordinal)
                                                           && entry.Message.Contains("503", StringComparison.Ordinal) && entry.Message.Contains(" at ", StringComparison.Ordinal)
                                                         : entry.Message == $"{context}\n{failure}")));
            AddWaitStep("allow general diagnostic forwarding", 3);
            AddAssert("handled failure has no raw system diagnostic", () => !Game.Notifications.AllNotifications.OfType<SimpleErrorNotification>().Any(notification =>
                notification.Text.ToString().Contains(context, StringComparison.Ordinal)));
            if (kind == "download")
                AddAssert("only one actionable download failure notification is posted", () => Game.Notifications.AllNotifications.OfType<SimpleErrorNotification>().Count(notification =>
                    Game.Dependencies.Get<LocalisationManager>().GetLocalisedString(notification.Text).Contains("下载或入库失败，请重试。", StringComparison.Ordinal)), () => Is.EqualTo(1));
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
            AddStep("download the already visible chart", () => click(cardButton(overlay.Cards.Single(), "Download")));
            AddUntilStep("download completes while metadata is pending", () => (task = manager.GetTask(overlay.Cards.Single().Package.Key))?.Completion.IsCompleted == true);
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
            AddStep("reveal pagination retry", () => overlay.ChildrenOfType<OverlayScrollContainer>().Single().ScrollToEnd(false));
            AddWaitStep("allow retry button layout", 2);
            AddStep("retry second page manually", () =>
            {
                pageFails = false;
                click(overlay.ChildrenOfType<ShowMoreButton>().Single());
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
        public void TestCancelAndRetryIconsKeepExactChartAndBackgroundCompletion()
        {
            TaskCompletionSource releasePackage = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            BmsDownloadTask? cancelled = null;
            BmsDownloadTask? failed = null;
            BmsDownloadTask? retried = null;
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("hold download response", () => packages.Response = async (_, token) =>
            {
                await releasePackage.Task.WaitAsync(token).ConfigureAwait(false);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(createPackage()) };
            });
            AddStep("open browser", () => overlay.Show());
            AddUntilStep("package and charts loaded", () => overlay.Cards.SingleOrDefault()?.Package.Charts.Count == 2);
            AddUntilStep("card accepts input", () => !overlay.ChildrenOfType<LoadingLayer>().Single().IsPresent);
            AddStep("select exact difficulty", () => overlay.Cards.Single().SelectChart(another_md5));
            AddStep("click download", () => click(cardButton(overlay.Cards.Single(), "Download")));
            AddUntilStep("download is cancellable", () => packages.Requests == 1 && overlay.Cards.Single().ChildrenOfType<BeatmapDownloadCardButton>().Any(button => button.TooltipText.ToString() == "Cancel"));
            AddStep("click cancel", () =>
            {
                cancelled = manager.GetTask(overlay.Cards.Single().Package.Key);
                click(cardButton(overlay.Cards.Single(), "Cancel"));
            });
            AddUntilStep("task cancelled", () => cancelled!.Completion.IsCompleted);
            AddAssert("cancelled task never imported", () => cancelled!.Progress.State, () => Is.EqualTo(BmsDownloadState.Cancelled));
            AddUntilStep("cancelled card can retry", () => overlay.Cards.Single().ChildrenOfType<BeatmapDownloadCardButton>().Any(button => button.TooltipText.ToString() == "Retry"));
            AddStep("retry with server failure", () =>
            {
                packages.Response = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                click(cardButton(overlay.Cards.Single(), "Retry"));
            });
            AddUntilStep("retry creates a new task", () => (failed = manager.GetTask(overlay.Cards.Single().Package.Key)) != cancelled);
            AddUntilStep("retry reports server failure", () => failed!.Completion.IsCompleted);
            AddAssert("failed retry preserves requested difficulty", () => failed!.RequestedMd5 == another_md5 && failed.Progress.State == BmsDownloadState.Failed);
            AddUntilStep("failed card can retry", () => overlay.Cards.Single().ChildrenOfType<BeatmapDownloadCardButton>().Any(button => button.TooltipText.ToString() == "Retry"));
            AddStep("retry held response again", () =>
            {
                packages.Response = async (_, token) =>
                {
                    await releasePackage.Task.WaitAsync(token).ConfigureAwait(false);
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(createPackage()) };
                };
                click(cardButton(overlay.Cards.Single(), "Retry"));
            });
            AddUntilStep("new retry is downloading", () => (retried = manager.GetTask(overlay.Cards.Single().Package.Key)) != failed && retried!.Progress.State == BmsDownloadState.Downloading);
            AddStep("close page during retry", () => overlay.Hide());
            AddStep("finish retry in background", () => releasePackage.SetResult());
            AddUntilStep("background retry imported", () => retried!.Completion.IsCompleted);
            AddAssert("completed retry has exact requested chart", () => retried!.Progress.State == BmsDownloadState.Completed
                && retried.RequestedMd5 == another_md5 && retried.Progress.Imported!.Any(chart => chart.Md5 == another_md5));
            AddAssert("background retry leaves menu current", () => Game.ScreenStack.CurrentScreen is MainMenu);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestExpandedRowsRemainClickableAcrossCardsAndAtBottom(bool narrow)
        {
            BmsDownloadCard? first = null;
            BmsDownloadCard? last = null;
            Task? screenshot = null;
            AddUntilStep("browser loaded", () => overlay.IsLoaded);
            AddStep("provide long titles and multiple result rows", () =>
            {
                metadata.Response = (request, token) => request.RequestUri!.AbsolutePath.EndsWith("selectList", StringComparison.Ordinal)
                    ? Task.FromResult(multiPackageResponse())
                    : defaultMetadata(request, token);
                overlay.RelativeSizeAxes = Axes.Y;
                overlay.Width = narrow ? 420 : 800;
                overlay.Show();
            });
            AddUntilStep("all packages loaded", () => overlay.Cards.Count == 12);
            AddUntilStep("result layout accepts input", () => !overlay.ChildrenOfType<LoadingLayer>().Single().IsPresent);
            AddStep("click first card chart selector", () =>
            {
                first = overlay.Cards.First();
                last = overlay.Cards.Last();
                click(cardButton(first, "Charts"));
            });
            AddUntilStep("first chart list expands", () => first!.ChildrenOfType<BeatmapCardContent>().Single().Expanded.Value);
            AddWaitStep("allow expansion animation", 3);
            AddAssert("difficulty is over a later row of cards", () => overlay.Cards.Skip(1).Any(card => card.ScreenSpaceDrawQuad.Contains(
                first!.ChildrenOfType<BmsDownloadChartRow>().Single(row => row.Chart.Md5 == another_md5).ScreenSpaceDrawQuad.Centre)));
            AddStep("click row covering later card", () => click(first!.ChildrenOfType<BmsDownloadChartRow>().Single(row => row.Chart.Md5 == another_md5)));
            AddAssert("covered row selects its exact original chart", () => first!.SelectedMd5, () => Is.EqualTo(another_md5));
            AddAssert("later cards did not receive chart click", () => overlay.Cards.Skip(1).All(card => card.SelectedMd5 == normal_md5));
            AddStep("scroll to final package", () => overlay.ChildrenOfType<OverlayScrollContainer>().Single().ScrollToEnd(false));
            AddWaitStep("allow final card layout", 2);
            AddStep("click final chart selector", () => click(cardButton(last!, "Charts")));
            AddUntilStep("final chart list expands", () => last!.ChildrenOfType<BeatmapCardContent>().Single().Expanded.Value);
            AddWaitStep("allow final expansion animation", 3);
            AddAssert("final difficulty is inside visible browser", () =>
            {
                var row = last!.ChildrenOfType<BmsDownloadChartRow>().Single(row => row.Chart.Md5 == another_md5);
                var scroll = overlay.ChildrenOfType<OverlayScrollContainer>().Single();
                return row.ScreenSpaceDrawQuad.GetVertices().ToArray().All(vertex => scroll.ScreenSpaceDrawQuad.Contains(vertex));
            });
            AddStep("click final package difficulty", () => click(last!.ChildrenOfType<BmsDownloadChartRow>().Single(row => row.Chart.Md5 == another_md5)));
            AddAssert("final package selects exact chart", () => last!.SelectedMd5, () => Is.EqualTo(another_md5));
            AddAssert("long title did not hide download action", () => cardButton(last!, "Download").IsPresent && cardButton(last!, "Download").Enabled.Value);
            AddStep("click download beside long title", () => click(cardButton(last!, "Download")));
            AddUntilStep("long-title card downloads clicked chart", () => manager.GetTask(last!.Package.Key)?.Completion.IsCompleted == true);
            AddAssert("long-title card imports exact chart", () => manager.GetTask(last!.Package.Key)!.Progress.State == BmsDownloadState.Completed
                && manager.GetTask(last.Package.Key)!.RequestedMd5 == another_md5);
            AddStep("capture long-title result layout if requested", () => screenshot = captureScreenshot(narrow ? "-narrow" : "-multi-row"));
            AddUntilStep("layout screenshot completes", () => screenshot!.IsCompleted);
            AddStep("surface layout screenshot failure", () => screenshot!.GetAwaiter().GetResult());
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
            AddAssert("both original difficulties are available in expanded list", () => overlay.Cards.Single().ChildrenOfType<BmsDownloadChartRow>().Select(row => row.Chart.Md5),
                () => Is.EquivalentTo(new[] { normal_md5, another_md5 }));
            AddAssert("download icon remains available with chart list open", () => cardButton(overlay.Cards.Single(), "Download").IsPresent && cardButton(overlay.Cards.Single(), "Download").Enabled.Value);
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
            AddAssert("unsupported difficulty cannot download", () => !cardButton(overlay.Cards.Single(), "Download").Enabled.Value);
            AddAssert("unsupported reason is visible", () => overlay.Cards.Single().ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString().Contains("not supported", StringComparison.OrdinalIgnoreCase)));
            AddUntilStep("unsupported card accepts input", () => !overlay.ChildrenOfType<LoadingLayer>().Single().IsPresent);
            AddStep("click unsupported download icon", () => click(cardButton(overlay.Cards.Single(), "Download")));
            AddAssert("unsupported action fetched no package", () => packages.Requests, () => Is.Zero);
            AddStep("select supported difficulty", () => overlay.Cards.Single().SelectChart(another_md5));
            AddAssert("supported difficulty can download", () => cardButton(overlay.Cards.Single(), "Download").Enabled.Value);
            AddAssert("no package fetched for unsupported chart", () => packages.Requests, () => Is.Zero);
        }

        private static BeatmapDownloadCardButton cardButton(BmsDownloadCard card, string tooltip) =>
            card.ChildrenOfType<BeatmapDownloadCardButton>().Single(button => button.TooltipText.ToString() == tooltip);

        private bool hasVisibleFallbackText(string text) => overlay.ChildrenOfType<OsuSpriteText>().Any(sprite => sprite.IsPresent && sprite.Text.ToString().Contains(text, StringComparison.Ordinal));

        private bool hasVisibleLocalisedText(string text) => overlay.ChildrenOfType<OsuSpriteText>().Any(sprite => sprite.IsPresent
            && Game.Dependencies.Get<LocalisationManager>().GetLocalisedString(sprite.Text).Contains(text, StringComparison.Ordinal));

        private void click(Drawable drawable)
        {
            InputManager.MoveMouseTo(drawable);
            InputManager.Click(MouseButton.Left);
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

        private static string tableId(BmsDownloadSource source) => source == BmsDownloadSource.Ginger ? "1" : "https://table.example/original";

        private static async Task<HttpResponseMessage> tableMetadata(HttpRequestMessage request, CancellationToken token)
        {
            string path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("selectHeaderListWithFullInfo", StringComparison.Ordinal))
                return json("[{\"id\":1,\"name\":\"Test Table\",\"originalURL\":\"\"},{\"id\":2,\"name\":\"Other Table\",\"originalURL\":\"\"}]");
            if (path.Contains("selectOneHeader/", StringComparison.Ordinal))
            {
                bool other = path.EndsWith("/2", StringComparison.Ordinal);
                return json(JsonSerializer.Serialize(new { id = other ? 2 : 1, symbol = other ? "◇" : "★", levelOrders = other ? "Other" : "2,1,?" }));
            }
            if (path.EndsWith("selectDataList", StringComparison.Ordinal))
            {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token).ConfigureAwait(false));
                return gingerTableData(body.RootElement.GetProperty("headerID").GetInt32());
            }
            if (path.Contains("files/package/", StringComparison.Ordinal))
                return json(JsonSerializer.Serialize(new
                {
                    id = 17,
                    fileName = "OMS Download Test.7z",
                    downloadURL = "https://gingerrush.com/packages/song.7z",
                    fileSize = createPackage().Length,
                    songs = new[]
                    {
                        new { md5 = normal_md5, fileName = "normal.bme", title = "OMS Download Test", artist = "OMS", subTitle = "[NORMAL]", mode = "BEAT_7K", playLevel = "3" },
                        new { md5 = another_md5, fileName = "another.bme", title = "OMS Download Test", artist = "OMS", subTitle = "[ANOTHER]", mode = "BEAT_7K", playLevel = "7" },
                    },
                }));
            if (path.EndsWith("/tables", StringComparison.Ordinal))
                return json(JsonSerializer.Serialize(new
                {
                    result = "success",
                    tables = new[] { new { diff_table_name = "Test Table", diff_table_url = tableId(BmsDownloadSource.Konmai), diff_table_full_local_url = "https://bms.alvorna.com/tables/test/header.json" } },
                }));
            if (path.EndsWith("header.json", StringComparison.Ordinal))
                return json("{\"symbol\":\"★\",\"level_order\":[2,1,\"?\"],\"data_url\":\"body.json\"}");
            if (path.EndsWith("body.json", StringComparison.Ordinal))
                return json(JsonSerializer.Serialize(tableEntries(1)));
            if (path.EndsWith("/hash", StringComparison.Ordinal))
            {
                bool another = request.RequestUri.Query.Contains(another_md5, StringComparison.Ordinal);
                return json(JsonSerializer.Serialize(new
                {
                    result = "success",
                    data = new
                    {
                        md5 = another ? another_md5 : normal_md5,
                        chart_name = another ? "OMS Download Test [ANOTHER]" : "OMS Download Test [NORMAL]",
                        title = "OMS Download Test",
                        artist = "OMS",
                        song_name = "OMS Download Test",
                        song_url = "https://bms.alvorna.com/bms/zipped/test.7z",
                    },
                }));
            }
            return await defaultMetadata(request, token).ConfigureAwait(false);
        }

        private static HttpResponseMessage gingerTableData(int table) => json(JsonSerializer.Serialize(new
        {
            page = 1,
            pageSize = 100,
            pageCount = 1,
            total = table == 1 ? 4 : 1,
            data = tableEntries(table),
        }));

        private static object[] tableEntries(int table) => table == 1
            ? new object[]
            {
                new { headerID = 1, md5 = normal_md5, level = "1", title = "OMS Download Test", artist = "OMS" },
                new { headerID = 1, md5 = another_md5, level = "2", title = "OMS Download Test", artist = "OMS" },
                new { headerID = 1, md5 = normal_md5, level = "?", title = "OMS Download Test", artist = "OMS" },
                new { headerID = 1, md5 = normal_md5, level = "", title = "OMS Download Test", artist = "OMS" },
            }
            : new object[] { new { headerID = 2, md5 = normal_md5, level = "Other", title = "OMS Download Test", artist = "OMS" } };

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

        private static HttpResponseMessage multiPackageResponse() => json(JsonSerializer.Serialize(new
        {
            page = 1,
            pageSize = 20,
            pageCount = 1,
            total = 12,
            data = Enumerable.Range(1, 12).Select(index => new
            {
                id = index,
                fileName = "很长的原始歌曲标题 Long original song title with extra information " + index + ".7z",
                downloadURL = "https://gingerrush.com/packages/song-" + index + ".7z",
                fileSize = createPackage().Length,
                songs = new[]
                {
                    new { md5 = normal_md5, fileName = "normal.bme", title = "很长的歌曲标题 Long original song title with extra information", artist = "很长的作者名字 Long original artist name", subTitle = "[NORMAL with a long original difficulty label]", mode = "BEAT_7K", playLevel = "3" },
                    new { md5 = another_md5, fileName = "another.bme", title = "很长的歌曲标题 Long original song title with extra information", artist = "很长的作者名字 Long original artist name", subTitle = "[ANOTHER with a long original difficulty label]", mode = "BEAT_7K", playLevel = "7" },
                },
            }).ToArray(),
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
