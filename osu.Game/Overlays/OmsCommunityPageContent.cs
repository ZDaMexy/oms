// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.IR;
using osuTK;

namespace osu.Game.Overlays
{
    public partial class OmsCommunityPageContent : OmsPageContent
    {
        protected override Task<Action> LoadPageAsync(OmsPageRoute requested, CancellationToken token)
            => requested.Kind == OmsPageKind.Topic ? LoadTopicAsync(requested, token) : LoadCommunityAsync(requested, token);
        private readonly Dictionary<string, CommunityDraft> drafts = new Dictionary<string, CommunityDraft>();
        private static readonly string[] categories = { "discussion", "help", "showcase", "development" };

        private sealed class CommunityDraft
        {
            public long Owner;
            public Guid SubmissionId = Guid.NewGuid();
            public string Title = string.Empty;
            public string Body = string.Empty;
            public string Category = "discussion";
            public JObject? Submitted;
        }

        private async Task<Action> LoadCommunityAsync(OmsPageRoute requested, CancellationToken token)
        {
            string query = "posts?page=" + requested.Page.ToString(CultureInfo.InvariantCulture) + "&limit=20&q=" + Uri.EscapeDataString(requested.Query);
            if (requested.Category != null) query += "&category=" + Uri.EscapeDataString(requested.Category);
            JObject response = await service.GetCommunityAsync(query, token).ConfigureAwait(false);
            JObject[] items = ReadCommunityPage(response, requested.Page, isPost: true, detail: false);
            return () =>
            {
                var search = new OsuTextBox { RelativeSizeAxes = Axes.X, Height = 40, Text = requested.Query, LengthLimit = 80, PlaceholderText = "搜索标题与正文" };
                rows.Add(new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    ColumnDimensions = new[] { new Dimension(), new Dimension(GridSizeMode.Absolute, 100) },
                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                    Content = new[] { new Drawable[] { search, new RoundedButton { Text = "搜索", Width = 96, Height = 40,
                        Action = () => ReplaceRoute(requested with { Query = search.Text, Page = 1 }) } } },
                });
                AddCategorySelector(requested.Category, value => ReplaceRoute(requested with { Category = value, Page = 1 }), true);
                if (items.Length == 0) AddText("当前范围没有帖子。");
                foreach (JObject post in items)
                {
                    long id = Integer(post, "id");
                    JObject author = Object(post, "author");
                    rows.Add(new TopicCard(String(post, "title"), String(post, "excerpt"),
                        $"{String(author, "username")} · {CategoryLabel(String(post, "category"))} · {Integer(post, "reply_count")} 条回复",
                        () => Navigate(new OmsPageRoute { Kind = OmsPageKind.Topic, PostId = id })));
                }
                AddPagination(requested, Integer(response, "total"), Integer(response, "limit"));
                if (service.State.Account != null && !service.State.RequiresLogin)
                {
                    bool opened = drafts.ContainsKey("post");
                    if (opened) AddDraftEditor("post", "posts", true);
                    AddAction("发表帖子", () => { if (opened) return; opened = true; AddDraftEditor("post", "posts", true); });
                }
                else AddAction("登录后发表帖子", () => game?.ShowOmsAccount());
            };
        }

        private partial class TopicCard : OsuClickableContainer
        {
            private readonly Box background;

            public TopicCard(string title, string excerpt, string metadata, Action open)
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Masking = true;
                CornerRadius = 5;
                Action = open;
                Children = new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Direction = FillDirection.Vertical,
                        Padding = new MarginPadding(16), Spacing = new Vector2(0, 6),
                        Children = new Drawable[] { Text(title, 22), Text(metadata, 14), Text(excerpt, 16) },
                    },
                };
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colour) => background.Colour = colour.Background4;
        }

        private async Task<Action> LoadTopicAsync(OmsPageRoute requested, CancellationToken token)
        {
            string prefix = "posts/" + requested.PostId.ToString(CultureInfo.InvariantCulture);
            JObject post = Object(await service.GetCommunityAsync(prefix, token).ConfigureAwait(false), "post");
            ValidateCommunityItem(post, isPost: true, detail: true);
            if (Integer(post, "id") != requested.PostId) throw new JsonSerializationException("OMS topic identity mismatch.");
            JObject response = await service.GetCommunityAsync(prefix + "/replies?page=" + requested.Page.ToString(CultureInfo.InvariantCulture) + "&limit=20", token).ConfigureAwait(false);
            JObject[] replies = ReadCommunityPage(response, requested.Page, isPost: false, detail: true);
            if (replies.Any(reply => Integer(reply, "post_id") != requested.PostId)) throw new JsonSerializationException("OMS reply topic mismatch.");
            return () =>
            {
                AddText(String(post, "title"), 26);
                AddAuthor(Object(post, "author"));
                AddText(CategoryLabel(String(post, "category")) + " · " + String(post, "created_at"), 15);
                bool deleted = Boolean(post, "deleted");
                AddText(deleted ? "这篇帖子已由作者删除。" : String(post, "body"));
                if (!deleted && service.State.Account?.Id == Integer(Object(post, "author"), "id"))
                    AddOwnActions("posts/" + requested.PostId, post, true);
                AddText("回复", 23);
                if (replies.Length == 0) AddText("目前没有回复。");
                foreach (JObject reply in replies)
                {
                    if (Integer(reply, "post_id") != requested.PostId) throw new JsonSerializationException("OMS reply topic mismatch.");
                    AddAuthor(Object(reply, "author"));
                    AddText(String(reply, "created_at"), 15);
                    bool removed = Boolean(reply, "deleted");
                    AddText(removed ? "这层回复已由作者删除。" : String(reply, "body"));
                    if (!removed && service.State.Account?.Id == Integer(Object(reply, "author"), "id"))
                        AddOwnActions("replies/" + Integer(reply, "id"), reply, false, canEdit: !deleted);
                }
                AddPagination(requested, Integer(response, "total"), Integer(response, "limit"));
                if (!deleted)
                {
                    if (service.State.Account != null && !service.State.RequiresLogin)
                        AddDraftEditor("reply:" + requested.PostId, prefix + "/replies", false);
                    else AddAction("登录后回复", () => game?.ShowOmsAccount());
                }
            };
        }

        private static JObject[] ReadCommunityPage(JObject response, int page, bool isPost, bool detail)
        {
            JObject[] items = Array(response, "items").Select(item => item as JObject ?? throw new JsonSerializationException("Invalid community entry.")).ToArray();
            long total = Integer(response, "total");
            if (Integer(response, "page") != page || Integer(response, "limit") != 20 || total < 0 || items.Length > 20 || items.Length > total)
                throw new JsonSerializationException("Invalid community pagination.");
            foreach (JObject item in items) ValidateCommunityItem(item, isPost, detail);
            return items;
        }

        private static void ValidateCommunityItem(JObject item, bool isPost, bool detail)
        {
            JObject author = Object(item, "author");
            if (Integer(item, "id") <= 0 || Integer(author, "id") <= 0) throw new JsonSerializationException("Invalid community identity.");
            String(author, "username");
            String(item, "created_at");
            Boolean(item, "deleted");
            if (detail) String(item, "body");
            if (isPost)
            {
                String(item, "title");
                CategoryLabel(String(item, "category"));
                String(item, "excerpt");
                if (Integer(item, "reply_count") < 0) throw new JsonSerializationException("Invalid community reply count.");
            }
            else if (Integer(item, "post_id") <= 0) throw new JsonSerializationException("Invalid community topic identity.");
        }

        private void AddAuthor(JObject author)
        {
            long id = Integer(author, "id");
            AddAction(String(author, "username"), () => Navigate(new OmsPageRoute { Kind = OmsPageKind.Profile, UserId = id, Sources = "oms" }));
        }

        private static string CategoryLabel(string category) => category switch
        { "discussion" => "讨论", "help" => "求助", "development" => "开发记录", "showcase" => "作品展示", _ => throw new JsonSerializationException("Invalid community category.") };

        private void AddCategorySelector(string? value, Action<string?> changed, bool allowAll)
        {
            var selector = new CommunityCategoryDropdown { Width = 260 };
            selector.Items = allowAll ? new[] { string.Empty }.Concat(categories) : categories;
            selector.Current.Value = value ?? (allowAll ? string.Empty : "discussion");
            selector.Current.BindValueChanged(change => changed(change.NewValue.Length == 0 ? null : change.NewValue));
            rows.Add(selector);
        }

        private partial class CommunityCategoryDropdown : OsuDropdown<string>
        {
            protected override LocalisableString GenerateItemText(string item) => item.Length == 0 ? "全部分类" : CategoryLabel(item);
        }

        private void AddDraftEditor(string key, string path, bool isPost, JObject? original = null)
        {
            long currentOwner = service.State.Account!.Id;
            if (!drafts.TryGetValue(key, out CommunityDraft? draft))
            {
                if (drafts.Count >= 8) { AddText("已保留八份未完成草稿，请先完成或放弃已有草稿。"); return; }
                draft = new CommunityDraft { Owner = currentOwner };
                if (original != null)
                {
                    draft.Body = String(original, "body");
                    if (isPost) { draft.Title = String(original, "title"); draft.Category = String(original, "category"); }
                }
                drafts.Add(key, draft);
            }
            CommunityDraft saved = draft;
            AddText(original == null ? isPost ? "发表帖子" : "写回复" : "编辑内容", 23);
            AddText($"草稿属于 OMS #{saved.Owner}。关闭页面仍保留，退出客户端后不保存。\nEnter 新增段落；多行粘贴替换当前段落。", 15);
            if (saved.Owner != currentOwner)
            {
                AddText(original == null ? "当前账号与草稿账号不同。请切回原账号，或核对原发布结果后明确改用当前账号。" : "这份编辑草稿属于原作者，请切回原账号后保存。");
                if (original == null) AddAction("用当前账号创建新提交", () =>
                {
                    if (IsLoading) return;
                    saved.Owner = currentOwner;
                    saved.SubmissionId = Guid.NewGuid();
                    saved.Submitted = null;
                    Reload();
                });
            }
            if (isPost)
            {
                var titleInput = new OsuTextBox { RelativeSizeAxes = Axes.X, Height = 40, Text = saved.Title, LengthLimit = 100, PlaceholderText = "帖子标题" };
                titleInput.Current.BindValueChanged(value => saved.Title = value.NewValue);
                rows.Add(titleInput);
                AddCategorySelector(saved.Category, value => saved.Category = value!, false);
            }
            rows.Add(new OmsParagraphEditor(saved.Body, isPost ? 12000 : 6000, value => saved.Body = value));
            AddAction(original == null ? "发布" : "保存修改", () => SubmitDraft(key, path, saved, isPost, original != null));
            if (saved.Submitted != null)
            {
                AddAction("重试原提交（保留原内容与 ID）", () => WriteCommunity(path, saved.Submitted!, saved.Owner, key, saved));
                AddAction("将当前文字作为新提交", () => { if (IsLoading) return; saved.SubmissionId = Guid.NewGuid(); saved.Submitted = null; SubmitDraft(key, path, saved, isPost, original != null); });
            }
            AddAction("放弃这份草稿", () => { drafts.Remove(key); Reload(); });
        }

        private void SubmitDraft(string key, string path, CommunityDraft draft, bool isPost, bool editing)
        {
            if (IsLoading) return;
            if (string.IsNullOrWhiteSpace(draft.Body) || draft.Body.Length > (isPost ? 12000 : 6000)
                || (isPost && (string.IsNullOrWhiteSpace(draft.Title) || draft.Title.Length > 100)))
            { status.Text = "请填写有效标题 / 正文，并保持在显示的长度限制以内。"; return; }
            var payload = new JObject { ["body"] = draft.Body };
            if (isPost) { payload["title"] = draft.Title; payload["category"] = draft.Category; }
            if (!editing) payload["submission_id"] = draft.SubmissionId.ToString();
            if (draft.Submitted != null && !JToken.DeepEquals(draft.Submitted, payload))
            { status.Text = "原提交尚未确认，请先重试原内容，或明确选择将当前文字作为新提交。"; return; }
            draft.Submitted = payload;
            WriteCommunity(path, payload, draft.Owner, key, draft);
        }

        private void AddOwnActions(string path, JObject original, bool isPost, bool canEdit = true)
        {
            bool editing = false;
            if (canEdit) AddAction("编辑我的内容", () =>
            {
                if (editing) return;
                editing = true;
                AddDraftEditor("edit:" + path, path + "/edit", isPost, original);
            });
            bool confirming = false;
            AddAction("删除我的内容", () =>
            {
                if (confirming) return;
                confirming = true;
                AddText("确认删除这份内容？删除后保留作者与楼层占位。");
                AddAction("确认删除", () => WriteCommunity(path + "/delete", new JObject(), Integer(Object(original, "author"), "id")));
                AddAction("取消删除", Reload);
            });
        }

        private async void WriteCommunity(string path, JObject payload, long expectedOwner, string? key = null, CommunityDraft? draft = null)
        {
            if (IsLoading) return;
            if (DateTimeOffset.UtcNow < retryAfter) { status.Text = "服务要求稍后重试；草稿与原 ID 已保留。"; return; }
            if (service.State.Account?.Id != expectedOwner || service.State.RequiresLogin)
            { status.Text = "账号已变化，请切回原账号；草稿与原提交 ID 已保留。"; return; }
            CancelOperation();
            int requestedRevision = revision;
            var cancellation = operation = new CancellationTokenSource();
            IsLoading = true;
            status.Text = "正在保存…";
            try
            {
                JObject response = await service.PostCommunityAsync(path, payload, expectedOwner, cancellation.Token).ConfigureAwait(false);
                JObject receipt = response["post"] as JObject ?? Object(response, "reply");
                if (Integer(receipt, "id") <= 0 || Integer(Object(receipt, "author"), "id") != expectedOwner)
                    throw new JsonSerializationException("Community receipt owner mismatch.");
                Schedule(() =>
                {
                    if (!Current(requestedRevision, expectedOwner, cancellation)) return;
                    if (key != null && drafts.TryGetValue(key, out var saved) && ReferenceEquals(saved, draft)
                        && JToken.DeepEquals(saved.Submitted, payload)
                        && saved.Body == payload["body"]?.Value<string>() && (payload["title"] == null
                            || (saved.Title == payload["title"]!.Value<string>() && saved.Category == payload["category"]!.Value<string>())))
                        drafts.Remove(key);
                    InvalidatePages();
                    if (path == "posts") ReplaceRoute(new OmsPageRoute { Kind = OmsPageKind.Topic, PostId = Integer(receipt, "id") });
                    else Reload();
                });
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (OmsIrException exception)
            {
                Schedule(() =>
                {
                    if (!Current(requestedRevision, expectedOwner, cancellation)) return;
                    if (exception.RetryAfter is TimeSpan wait) retryAfter = DateTimeOffset.UtcNow + wait;
                    status.Text = exception.Message + " 草稿与原提交 ID 已保留。";
                    AddAction("重试这次保存", () => WriteCommunity(path, payload, expectedOwner, key, draft));
                });
            }
            catch (JsonException)
            {
                Schedule(() => { if (Current(requestedRevision, expectedOwner, cancellation)) status.Text = "保存收据不符合约定；请核对发布结果，再用原 ID 重试。"; });
            }
            finally
            {
                Schedule(() =>
                {
                    if (ReferenceEquals(operation, cancellation)) { operation = null; IsLoading = false; }
                    cancellation.Dispose();
                });
            }
        }
    }
}
