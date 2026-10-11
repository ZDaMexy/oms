// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Online.IR;
using osu.Game.Overlays;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Overlays
{
    public partial class TestSceneOmsCommunityOverlay : OsuManualInputManagerTestScene
    {
        private const string topic_title = "公开讨论";
        private const string old_topic_title = "迟到的旧帖子";
        private const string first_username = "first_player";
        private const string second_username = "second_player";

        private readonly ConcurrentQueue<RequestSnapshot> requests = new ConcurrentQueue<RequestSnapshot>();
        private OmsIrService service = null!;
        private HttpClient http = null!;
        private OmsCommunityOverlay overlay = null!;
        private DependencyProvidingContainer scope = null!;
        private HeadlessClipboard clipboard = null!;
        private JObject? publishedPost;
        private volatile bool failNextWrite;
        private volatile bool holdNextRead;
        private volatile bool failNextRead;
        private volatile TaskCompletionSource<JObject>? delayedRead;
        private CancellationToken delayedToken;

        protected override bool UseFreshStoragePerRun => true;

        [SetUpSteps]
        public void SetUp()
        {
            AddStep("create isolated native services", () =>
            {
                delayedRead?.TrySetResult(communityPage(1, old_topic_title));
                service?.Dispose();
                http?.Dispose();
                requests.Clear();
                publishedPost = null;
                failNextWrite = holdNextRead = failNextRead = false;
                delayedRead = null;
                delayedToken = default;
                clipboard = new HeadlessClipboard();
                http = new HttpClient(new Handler(response));
                service = new OmsIrService(LocalStorage.GetStorageForDirectory("oms-community-synthetic-" + Guid.NewGuid()), http, new Credentials());
                Child = scope = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[]
                    {
                        (typeof(OmsIrService), service),
                        (typeof(Clipboard), clipboard),
                        (typeof(OverlayColourProvider), new OverlayColourProvider(OverlayColourScheme.Blue)),
                    },
                    Child = overlay = new OmsCommunityOverlay(),
                };
            });
            AddUntilStep("overlay loaded but hidden", () => overlay.IsLoaded && overlay.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestCommunityDraftOwnerAndOriginalSubmissionRetry()
        {
            AddStep("login first OMS account", () => service.LoginAsync(first_username, "synthetic-password-123").GetAwaiter().GetResult());
            AddUntilStep("hidden account change completes before opening", () => service.State.Account?.Id == 1);
            AddStep("open community", () => overlay.ShowOmsPage(new OmsPageRoute { Kind = OmsPageKind.Community }));
            AddUntilStep("community can create a draft", () => !page.IsLoading && buttonExists("发表帖子"));
            AddStep("open the native post editor", () => click("发表帖子"));
            AddUntilStep("actual paragraph input loaded", () => overlay.ChildrenOfType<OmsParagraphEditor>().Any()
                && bodyInput.IsLoaded && titleInput.IsLoaded);
            AddStep("write a draft", () => { titleInput.Text = "保留原提交"; bodyInput.Text = "原账号的草稿正文"; });
            AddStep("simulate a lost write response", () => { failNextWrite = true; click("发布"); });
            AddUntilStep("failure keeps an explicit retry", () => !page.IsLoading && buttonExists("重试这次保存") && writes.Length == 1);
            AddAssert("draft text and an actual submission UUID survive failure", () => bodyInput.Text == "原账号的草稿正文"
                && Guid.TryParse(writes[0].Body!["submission_id"]?.Value<string>(), out _));
            AddStep("switch to a second OMS account", () => service.LoginAsync(second_username, "synthetic-password-123").GetAwaiter().GetResult());
            AddUntilStep("account switch clears the old view", () => service.State.Account?.Id == 2 && hasText("请刷新当前页面") && !buttonExists("发布"));
            AddStep("refresh for the new account", () => overlay.ShowOmsPage(page.Route));
            AddUntilStep("old draft stays visibly owned by its original account", () => !page.IsLoading
                && hasText("草稿属于 OMS #1") && hasText("当前账号与草稿账号不同") && buttonExists("发布"));
            AddStep("try to publish the old draft as the new account", () => click("发布"));
            AddUntilStep("cross-account write is rejected", () => hasText("请切回原账号") && !page.IsLoading);
            AddAssert("no old draft was sent by the second account", () => writes.Length, () => Is.EqualTo(1));
            AddStep("switch back to the original account", () => service.LoginAsync(first_username, "synthetic-password-123").GetAwaiter().GetResult());
            AddUntilStep("original account change reflected", () => service.State.Account?.Id == 1 && hasText("请刷新当前页面"));
            AddStep("restore original draft", () => overlay.ShowOmsPage(page.Route));
            AddUntilStep("original retry and text restored", () => !page.IsLoading
                && buttonExists("重试原提交（保留原内容与 ID）") && bodyInput.Text == "原账号的草稿正文");
            AddStep("retry the original submission", () => click("重试原提交（保留原内容与 ID）"));
            AddUntilStep("successful original receipt opens its topic", () => !page.IsLoading && page.Route.Kind == OmsPageKind.Topic
                && page.Route.PostId == 42 && hasText("原账号的草稿正文"));
            AddAssert("retry preserves every payload field and original UUID", () => writes.Length == 2
                && JToken.DeepEquals(writes[0].Body, writes[1].Body)
                && writes.All(write => write.Bearer == "synthetic-access-1"));
        }

        [Test]
        public void TestHideCancelsReadAndLateCompletionCannotReplaceNewScope()
        {
            AddStep("hold a real community read", () =>
            {
                holdNextRead = true;
                overlay.ShowOmsPage(new OmsPageRoute { Kind = OmsPageKind.Community, Query = "old" });
            });
            AddUntilStep("read is in flight", () => delayedRead != null && page.IsLoading);
            AddStep("close while reading", () => overlay.Hide());
            AddUntilStep("hidden immediately and cancellation reached HTTP", () => overlay.State.Value == Visibility.Hidden
                && !page.IsLoading && delayedToken.IsCancellationRequested);
            AddStep("open a new community scope", () => overlay.ShowOmsPage(new OmsPageRoute { Kind = OmsPageKind.Community, Query = "new" }));
            AddStep("finish the old HTTP response late", () => delayedRead!.TrySetResult(communityPage(1, old_topic_title)));
            AddUntilStep("new scope is ready", () => !page.IsLoading && hasText(topic_title));
            AddUntilStep("old service read settles", () => !service.State.Busy);
            AddWaitStep("allow late scheduled work to run", 2);
            AddAssert("late data cannot replace the new page", () => page.Route.Kind == OmsPageKind.Community && page.Route.Query == "new"
                && !buttonExists(old_topic_title) && !hasText(old_topic_title) && requests.Count == 2);
        }

        [Test]
        public void TestAccountChangeRejectsLateCommunityView()
        {
            Task switchAccount = null!;
            AddStep("login original account", () => service.LoginAsync(first_username, "synthetic-password-123").GetAwaiter().GetResult());
            AddUntilStep("hidden original account is ready", () => service.State.Account?.Id == 1);
            AddStep("hold community navigation", () => { holdNextRead = true; overlay.ShowOmsPage(new OmsPageRoute { Kind = OmsPageKind.Community }); });
            AddUntilStep("old page is held with navigation history", () => delayedRead != null && page.IsLoading);
            AddStep("queue the next account behind the serial HTTP read", () => switchAccount = service.LoginAsync(second_username, "synthetic-password-123"));
            AddAssert("account change waits for the existing request", () => !switchAccount.IsCompleted);
            AddStep("complete HTTP and account change before the next UI frame", () =>
            {
                delayedRead!.TrySetResult(communityPage(1, old_topic_title));
                // Both operations complete off the update thread. Any already scheduled old render
                // must see the new account before this step allows the next UI update.
                switchAccount.GetAwaiter().GetResult();
            });
            AddUntilStep("new account invalidates the old page", () => service.State.Account?.Id == 2 && hasText("账号已变化") && !page.IsLoading);
            AddWaitStep("late render callbacks have run", 2);
            AddAssert("previous account history and late content are cleared", () => !buttonExists(old_topic_title) && !hasText(old_topic_title));
        }

        [Test]
        public void TestRestoreKeepsScopeAndScrollAndFailedRestoreCannotMoveTheNextPage()
        {
            var original = new OmsPageRoute { Kind = OmsPageKind.Community, Query = "原谱 & #", Category = "help" };
            int beforeBack = 0;
            float scroll = 0;
            AddStep("open a real community scope", () => overlay.ShowOmsPage(original));
            AddUntilStep("native discussion cards are loaded", () => !page.IsLoading && hasText(topic_title));
            AddStep("scroll the actual discussion list", () => overlay.OmsScroll = 180);
            AddUntilStep("list scroll applied", () => overlay.OmsScroll > 100);
            AddStep("record position and open the topic", () => { scroll = overlay.OmsScroll; overlay.ShowOmsPage(new OmsPageRoute { Kind = OmsPageKind.Topic, PostId = 42 }); });
            AddUntilStep("topic body loaded", () => !page.IsLoading && page.Route.Kind == OmsPageKind.Topic && hasText("帖子正文"));
            AddStep("restore the cached native scope", () => { beforeBack = requests.Count; overlay.RestoreOmsPage(original, scroll); });
            AddUntilStep("native controls and original scroll restored", () => !page.IsLoading && page.Route == original && Math.Abs(overlay.OmsScroll - scroll) < 2
                && overlay.ChildrenOfType<OsuTextBox>().Single(input => input.PlaceholderText.ToString() == "搜索标题与正文").Text == original.Query
                && overlay.ChildrenOfType<OsuDropdown<string>>().Single().Current.Value == "help");
            AddAssert("return reused the previous anonymous page", () => requests.Count, () => Is.EqualTo(beforeBack));
            AddStep("fail a cold return read", () => { failNextRead = true; overlay.RestoreOmsPage(original with { Query = "uncached" }, 200); });
            AddUntilStep("failed return has an explicit failure", () => !page.IsLoading && hasText("HTTP 503"));
            AddStep("open a normal new scope after failure", () => overlay.ShowOmsPage(original with { Query = "new" }));
            AddUntilStep("normal navigation starts at the top", () => !page.IsLoading && hasText(topic_title) && overlay.OmsScroll < 2);
        }

        [Test]
        public void TestParagraphEnterAndMultilinePastePreserveText()
        {
            OmsParagraphEditor editor = null!;
            string text = string.Empty;
            AddStep("open the actual paragraph editor", () => scope.Child = new OsuScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = editor = new OmsParagraphEditor("原始段\n保留后段", 12000, value => text = value),
            });
            AddUntilStep("paragraph controls loaded", () => paragraphInputs(editor).Length == 2 && paragraphInputs(editor).All(input => input.IsLoaded));
            AddStep("focus first paragraph", () => paragraphInputs(editor)[0].TakeFocus());
            AddUntilStep("first paragraph focused", () => paragraphInputs(editor)[0].HasFocus);
            AddStep("press Enter", () => InputManager.Key(Key.Enter));
            AddUntilStep("Enter inserts and focuses a new paragraph", () => paragraphInputs(editor).Length == 3
                && paragraphInputs(editor)[1].HasFocus && text == "原始段\n\n保留后段");
            AddStep("paste mixed newline text into the focused paragraph", () => paste(paragraphInputs(editor)[1], "插入一\r\n插入二\r末行"));
            AddUntilStep("multiline paste preserves existing neighbours and focuses its last line", () => paragraphInputs(editor).Length == 5
                && paragraphInputs(editor)[3].HasFocus && text == "原始段\n插入一\n插入二\n末行\n保留后段");
            AddStep("back leaves the focused paragraph intact", () => Assert.That(paragraphInputs(editor)[3].OnPressed(
                new KeyBindingPressEvent<GlobalAction>(InputManager.CurrentState, GlobalAction.Back)), Is.False));
            AddAssert("back retains the full draft", () => text == "原始段\n插入一\n插入二\n末行\n保留后段");
            AddStep("reject an oversized multiline paste", () => paste(paragraphInputs(editor)[3], new string('x', 12001) + "\n结尾"));
            AddUntilStep("oversized input has an explicit message", () => hasText(editor, "粘贴内容超过正文长度"));
            AddAssert("rejected paste loses no previous text", () => text == "原始段\n插入一\n插入二\n末行\n保留后段"
                && paragraphInputs(editor).Select(input => input.Text).SequenceEqual(new[] { "原始段", "插入一", "插入二", "末行", "保留后段" }));
        }

        [Test]
        public void TestLongParagraphTextUsesBoundedPagesWithoutTruncation()
        {
            OmsParagraphEditor editor = null!;
            string text = string.Empty;
            string[] paragraphs = Enumerable.Range(1, 205).Select(index => "段落 " + index.ToString(CultureInfo.InvariantCulture)).ToArray();
            AddStep("open a document longer than two hundred paragraphs", () => scope.Child = new OsuScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = editor = new OmsParagraphEditor(string.Join('\n', paragraphs), 12000, value => text = value),
            });
            AddUntilStep("only twenty paragraph inputs are created", () => paragraphInputs(editor).Length == 20 && paragraphInputs(editor).All(input => input.IsLoaded));
            AddAssert("all initial paragraphs are retained", () => text, () => Is.EqualTo(string.Join('\n', paragraphs)));
            AddStep("edit the first page", () => { paragraphs[0] = "开头已修改"; paragraphInputs(editor)[0].Text = paragraphs[0]; });
            AddStep("go to the next twenty paragraphs", () => editorButton(editor, "后 20 段").TriggerClick());
            AddUntilStep("second page shows original paragraph twenty-one", () => paragraphInputs(editor).Length == 20 && paragraphInputs(editor)[0].Text == paragraphs[20]);
            AddStep("edit a middle page", () => { paragraphs[20] = "中段已修改"; paragraphInputs(editor)[0].Text = paragraphs[20]; });
            AddRepeatStep("continue to the last page", () => editorButton(editor, "后 20 段").TriggerClick(), 9);
            AddUntilStep("last five paragraphs remain editable", () => paragraphInputs(editor).Length == 5 && paragraphInputs(editor)[0].Text == paragraphs[200]);
            AddStep("edit the last paragraph", () => { paragraphs[^1] = "末段已修改"; paragraphInputs(editor)[^1].Text = paragraphs[^1]; });
            AddAssert("paging keeps the complete edited document", () => text, () => Is.EqualTo(string.Join('\n', paragraphs)));
            AddAssert("complete preview includes the final edit", () => hasText(editor, "末段已修改"));
            AddRepeatStep("return through all previous pages", () => editorButton(editor, "前 20 段").TriggerClick(), 10);
            AddUntilStep("first page edit restored", () => paragraphInputs(editor).Length == 20 && paragraphInputs(editor)[0].Text == "开头已修改");
            AddAssert("return still keeps every line and all three edits", () => text, () => Is.EqualTo(string.Join('\n', paragraphs)));
            AddAssert("hidden startup and local text editing make no network request", () => requests.Count, () => Is.Zero);
        }

        private OmsCommunityPageContent page => overlay.ChildrenOfType<OmsCommunityPageContent>().Single();
        private RequestSnapshot[] writes => requests.Where(request => request.Path == "/api/ir/v1/community/posts" && request.Method == "POST").ToArray();
        private OsuTextBox titleInput => overlay.ChildrenOfType<OsuTextBox>().Single(input => input.PlaceholderText.ToString() == "帖子标题");
        private FocusedTextBox bodyInput => overlay.ChildrenOfType<OmsParagraphEditor>().Single().ChildrenOfType<FocusedTextBox>().Single();
        private bool buttonExists(string label) => overlay.ChildrenOfType<OsuButton>().Any(button => button.Text.ToString() == label);
        private void click(string label) => overlay.ChildrenOfType<OsuButton>().Single(button => button.Text.ToString() == label).TriggerClick();
        private bool hasText(string part) => hasText(overlay, part);

        private static bool hasText(Drawable drawable, string part)
            => drawable.ChildrenOfType<OsuTextFlowContainer>().Any(flow => string.Concat(flow.ChildrenOfType<SpriteText>().Select(sprite => sprite.Text.ToString())).Contains(part, StringComparison.Ordinal))
                || drawable.ChildrenOfType<SpriteText>().Any(sprite => sprite.Text.ToString().Contains(part, StringComparison.Ordinal));

        private static FocusedTextBox[] paragraphInputs(OmsParagraphEditor editor) => editor.ChildrenOfType<FocusedTextBox>().ToArray();
        private static OsuButton editorButton(OmsParagraphEditor editor, string label) => editor.ChildrenOfType<OsuButton>().Single(button => button.Text.ToString() == label);

        private void paste(FocusedTextBox input, string value)
        {
            clipboard.SetText(value);
            Assert.That(input.OnPressed(new KeyBindingPressEvent<PlatformAction>(InputManager.CurrentState, PlatformAction.Paste)), Is.True);
        }

        private async Task<HttpResponseMessage> response(HttpRequestMessage request, CancellationToken token)
        {
            string path = request.RequestUri!.AbsolutePath;
            JObject? body = request.Content == null ? null : JObject.Parse(await request.Content.ReadAsStringAsync(token));
            requests.Enqueue(new RequestSnapshot(path, Uri.UnescapeDataString(request.RequestUri.Query), request.Method.Method, body, request.Headers.Authorization?.Parameter));
            if (path == "/api/ir/v1/auth/login")
            {
                string username = body!["username"]!.Value<string>()!;
                long id = username == second_username ? 2 : 1;
                return json(new JObject
                {
                    ["user"] = new JObject { ["id"] = id, ["username"] = username },
                    ["access_token"] = "synthetic-access-" + id,
                    ["refresh_token"] = "synthetic-refresh-" + id,
                    ["expires_in"] = 3600,
                });
            }
            if (path == "/api/ir/v1/auth/logout") return json(new JObject());
            if (path == "/api/ir/v1/community/posts" && request.Method == HttpMethod.Post)
            {
                long owner = request.Headers.Authorization?.Parameter == "synthetic-access-2" ? 2 : 1;
                publishedPost = post(42, body!["title"]!.Value<string>()!, body["body"]!.Value<string>()!, owner);
                publishedPost["category"] = body["category"]!.DeepClone();
                if (failNextWrite)
                {
                    failNextWrite = false;
                    throw new HttpRequestException("Synthetic accepted write with lost response.");
                }
                return json(new JObject { ["post"] = publishedPost.DeepClone() });
            }
            if (path == "/api/ir/v1/community/posts" && request.Method == HttpMethod.Get)
            {
                int page = int.Parse(query(request)["page"], CultureInfo.InvariantCulture);
                if (failNextRead)
                {
                    failNextRead = false;
                    var failure = json(new JObject { ["error"] = new JObject { ["code"] = "service_unavailable", ["message"] = "社区暂时不可用，请稍后重试。" } });
                    failure.StatusCode = HttpStatusCode.ServiceUnavailable;
                    return failure;
                }
                if (holdNextRead)
                {
                    holdNextRead = false;
                    delayedToken = token;
                    var pending = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
                    delayedRead = pending;
                    // Intentionally do not observe cancellation, so the UI sees a real late HTTP completion.
                    return json(await pending.Task.ConfigureAwait(false));
                }
                return json(communityPage(page, topic_title));
            }
            if (path == "/api/ir/v1/community/posts/42/replies")
                return json(new JObject { ["items"] = new JArray(), ["page"] = 1, ["limit"] = 20, ["total"] = 0 });
            if (path == "/api/ir/v1/community/posts/42")
                return json(new JObject { ["post"] = publishedPost?.DeepClone() ?? post(42, topic_title, "帖子正文") });
            throw new InvalidOperationException("Unexpected synthetic OMS request: " + request.Method + " " + path);
        }

        private static Dictionary<string, string> query(HttpRequestMessage request)
            => request.RequestUri!.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Split('=', 2)).ToDictionary(value => value[0], value => Uri.UnescapeDataString(value[1]), StringComparer.Ordinal);

        private static JObject communityPage(int page, string title) => new JObject
        {
            ["items"] = new JArray(Enumerable.Range(0, page == 1 ? 20 : 1).Select(index => post(42 + index, index == 0 ? title : "讨论 " + index, "帖子正文"))),
            ["page"] = page,
            ["limit"] = 20,
            ["total"] = 21,
        };

        private static JObject post(long id, string title, string body, long owner = 1) => new JObject
        {
            ["id"] = id,
            ["title"] = title,
            ["body"] = body,
            ["excerpt"] = body,
            ["category"] = "help",
            ["author"] = new JObject { ["id"] = owner, ["username"] = owner == 1 ? first_username : second_username },
            ["created_at"] = "2026-10-11T00:00:00Z",
            ["updated_at"] = "2026-10-11T00:00:00Z",
            ["deleted"] = false,
            ["reply_count"] = 0,
        };

        private static HttpResponseMessage json(JObject value) => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(value.ToString(Formatting.None), Encoding.UTF8, "application/json") };

        protected override void Dispose(bool isDisposing)
        {
            delayedRead?.TrySetResult(communityPage(1, old_topic_title));
            base.Dispose(isDisposing);
            service?.Dispose();
            http?.Dispose();
        }

        private sealed record RequestSnapshot(string Path, string Query, string Method, JObject? Body, string? Bearer);

        private sealed class Handler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond;
            public Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) => this.respond = respond;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request, cancellationToken);
        }

        private sealed class Credentials : IOmsIrCredentialStore
        {
            private OmsIrSession? session;
            public OmsIrSession? Read(string target) => session;
            public void Write(string target, OmsIrSession value) => session = value;
            public void Delete(string target) => session = null;
        }
    }
}
