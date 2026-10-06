// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterface.PageSelector;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.IR;
using osu.Game.Overlays;
using osu.Game.Overlays.Login;
using osu.Game.Overlays.Profile;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Toolbar;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Overlays
{
    public partial class TestSceneOmsAccount : OsuManualInputManagerTestScene
    {
        private OmsIrService service = null!;
        private HttpClient http = null!;
        private LoginOverlay login = null!;
        private UserProfileOverlay profile = null!;
        private ToolbarUserButton userButton = null!;
        private int requests;
        private int legacyRequests;
        private int historyRequests;
        private int loginRequests;
        private string lastLoginOrigin = string.Empty;
        private bool holdLogin;
        private bool holdHistory;
        private bool wrongHistoryOwner;
        private bool delayedLoginWasCancelled;
        private TaskCompletionSource<JObject>? delayedLogin;
        private TaskCompletionSource<JObject>? delayedHistory;

        [SetUpSteps]
        public void SetUp()
        {
            AddStep("create native OMS account controls", () =>
            {
                service?.Dispose();
                http?.Dispose();
                requests = legacyRequests = historyRequests = loginRequests = 0;
                lastLoginOrigin = string.Empty;
                holdLogin = holdHistory = wrongHistoryOwner = false;
                delayedLoginWasCancelled = false;
                delayedLogin = delayedHistory = null;
                API.Logout();
                ((DummyAPIAccess)API).HandleRequest = _ => { Interlocked.Increment(ref legacyRequests); return false; };
                http = new HttpClient(new Handler(response));
                service = new OmsIrService(LocalStorage.GetStorageForDirectory("oms-account-synthetic-" + Guid.NewGuid()), http, new Credentials());
                login = new LoginOverlay(true) { Anchor = Anchor.TopRight, Origin = Anchor.TopRight, Y = Toolbar.HEIGHT };
                profile = new UserProfileOverlay(true);
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[] { (typeof(OmsIrService), service), (typeof(LoginOverlay), login) },
                    Children = new Drawable[]
                    {
                        profile,
                        login,
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = Toolbar.HEIGHT,
                            Child = userButton = new ToolbarUserButton(true) { Anchor = Anchor.TopRight, Origin = Anchor.TopRight },
                        },
                    },
                };
            });
            AddUntilStep("native account and profile loaded", () => login.IsLoaded && profile.IsLoaded && userButton.IsLoaded);
        }

        [Test]
        public void TestNativeLoginRegistrationAndPrivateProfile()
        {
            OsuPasswordTextBox savedPassword = null!;
            AddAssert("offline startup makes no request", () => requests, () => Is.Zero);
            AddAssert("default service stays empty", () => service.State.ServiceAddress, () => Is.Empty);
            AddStep("open original user menu", () => userButton.TriggerClick());
            AddUntilStep("original login form visible", () => login.State.Value == Visibility.Visible && login.ChildrenOfType<LoginForm>().Any(form => form.IsLoaded));
            AddStep("save explicit OMS origin", configureFromForm);
            AddUntilStep("IR enabled without a network request", () => service.State.Enabled && accountButton("登录").Enabled.Value);
            AddAssert("connection settings alone stay request driven", () => requests, () => Is.Zero);
            AddStep("open original menu after connection settings", showAccountMenu);
            AddUntilStep("login form is actually visible", () => login.State.Value == Visibility.Visible && accountButton("登录").Enabled.Value);
            AddStep("login through original form controls", () =>
            {
                usernameBox().Text = "player_one";
                savedPassword = login.ChildrenOfType<OsuPasswordTextBox>().Single();
                savedPassword.Text = "synthetic-password-123";
                accountButton("登录").TriggerClick();
            });
            AddUntilStep("shared account menu signed in", () => service.State.Account?.Id == 1 && login.ChildrenOfType<OmsAccountPanel>().Any(panel => panel.IsLoaded));
            AddAssert("password is cleared", () => savedPassword.Text, () => Is.Empty);
            AddAssert("toolbar shows the real OMS identity", () => userButton.ChildrenOfType<OsuSpriteText>().Any(text => text.Text.ToString() == "player_one"));
            AddAssert("legacy API remains offline", () => API.State.Value, () => Is.EqualTo(APIState.Offline));
            AddStep("open native own profile", () => { login.Hide(); profile.ShowOwnOmsUser(); });
            AddUntilStep("UUID play history shown", () => hasProfileText("合成 OMS 新局 1") && hasProfileText("本局 ID：") && hasProfileText("共 21 条"));
            AddAssert("only true OMS profile facts", () => ((OmsIrProfileHeader)profile.Header).Account.Value, () => Is.EqualTo(new OmsIrAccount(1, "player_one")));
            AddAssert("no fabricated upstream avatar", () => profile.ChildrenOfType<UpdateableAvatar>().Any(), () => Is.False);
            AddAssert("actual web profile route has no credentials", () => profile.ChildrenOfType<ExternalLinkButton>().Single().Link,
                () => Is.EqualTo("https://ir.example.test/users/?id=1"));
            AddAssert("saved result has this play's lamp and final gauge", () => hasProfileText("本局灯：CLEAR") && hasProfileText("85.0"));
            AddAssert("public play links to comparable and reference boards", () => profile.ChildrenOfType<SettingsButton>().Any(button => button.Text.ToString() == "查看同条件榜")
                                                                                  && profile.ChildrenOfType<SettingsButton>().Any(button => button.Text.ToString() == "查看参考混榜"));
            AddStep("read second history page", () => profile.ChildrenOfType<PageSelectorPageButton>().Single(button => button.PageNumber == 2).TriggerClick());
            AddUntilStep("server pagination is used", () => hasProfileText("合成 OMS 新局 21") && historyRequests == 2
                                                           && !profile.ChildrenOfType<OsuTextFlowContainer>().Any(flow => string.Concat(flow.ChildrenOfType<SpriteText>().Select(text => text.Text.ToString())) == "合成 OMS 新局 1"));
            AddStep("logout from original account menu", () => { profile.Hide(); login.Show(); accountButton("退出账号").TriggerClick(); });
            AddUntilStep("old private records and user are cleared", () => service.State.Account == null && !hasProfileText("本局 ID：") && login.ChildrenOfType<LoginForm>().Any(form => form.IsLoaded));
            AddAssert("old profile link is cleared", () => profile.ChildrenOfType<ExternalLinkButton>().Single().Link, () => Is.Null);
            AddStep("open original menu to register", showAccountMenu);
            AddUntilStep("registration form is actually visible", () => login.State.Value == Visibility.Visible && accountButton("注册并登录").Enabled.Value);
            AddStep("register through original form", () =>
            {
                usernameBox().Text = "player_two";
                login.ChildrenOfType<OsuPasswordTextBox>().Single().Text = "synthetic-password-123";
                accountButton("注册并登录").TriggerClick();
            });
            AddUntilStep("registered account is the shared identity", () => service.State.Account?.Id == 2 && login.ChildrenOfType<OmsAccountPanel>().Any(panel => panel.IsLoaded));
            AddStep("read new account history", () => { login.Hide(); profile.ShowOwnOmsUser(); });
            AddUntilStep("only new account records appear", () => hasProfileText("player_two 的新局") && !hasProfileText("合成 OMS 新局"));
            AddAssert("neither account enabled old API requests", () => legacyRequests, () => Is.Zero);
        }

        [Test]
        public void TestUnsavedConnectionCannotSendCredentials()
        {
            AddAssert("startup stays offline", () => requests == 0 && !service.State.Enabled && service.State.ServiceAddress.Length == 0);
            AddStep("open original login form", () => userButton.TriggerClick());
            AddUntilStep("login form loaded", () => login.State.Value == Visibility.Visible && login.ChildrenOfType<LoginForm>().Any(form => form.IsLoaded));
            AddAssert("offline form cannot login or register", () => !accountButton("登录").Enabled.Value && !accountButton("注册并登录").Enabled.Value);
            AddStep("save first explicit connection", configureFromForm);
            AddUntilStep("first connection saved", () => service.State.Enabled && accountButton("登录").Enabled.Value);
            AddAssert("saving connection makes no HTTP request", () => requests, () => Is.Zero);
            AddStep("edit the address without saving", () =>
            {
                login.ChildrenOfType<OsuTextBox>().Single(box => box.Name == "IR service address").Text = "https://next-ir.example.test";
                usernameBox().Text = "player_one";
                var password = login.ChildrenOfType<OsuPasswordTextBox>().Single();
                password.Text = "synthetic-password-123";
                InputManager.MoveMouseTo(password);
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("unsaved address disables login and registration", () => !accountButton("登录").Enabled.Value && !accountButton("注册并登录").Enabled.Value);
            AddAssert("unsaved address is explained", () => login.ChildrenOfType<OsuTextFlowContainer>()
                .Any(flow => string.Concat(flow.ChildrenOfType<SpriteText>().Select(text => text.Text.ToString())).Contains("连接设置尚未保存", StringComparison.Ordinal)));
            AddUntilStep("password input is focused", () => login.ChildrenOfType<OsuPasswordTextBox>().Single().HasFocus);
            AddStep("try password Enter with unsaved address", () => InputManager.Key(Key.Enter));
            AddAssert("Enter does not send credentials to the saved origin", () => requests == 0 && loginRequests == 0 && service.State.ServiceAddress == "https://ir.example.test/");
            AddAssert("blocked attempt keeps the password available", () => login.ChildrenOfType<OsuPasswordTextBox>().Single().Text, () => Is.EqualTo("synthetic-password-123"));
            AddStep("restore saved address but edit the switch", () =>
            {
                login.ChildrenOfType<OsuTextBox>().Single(box => box.Name == "IR service address").Text = service.State.ServiceAddress;
                login.ChildrenOfType<SettingsCheckbox>().Single().Current.Value = false;
            });
            AddAssert("unsaved switch also disables login and registration", () => !accountButton("登录").Enabled.Value && !accountButton("注册并登录").Enabled.Value);
            AddStep("save the new explicit origin and enabled switch", () =>
            {
                login.ChildrenOfType<OsuTextBox>().Single(box => box.Name == "IR service address").Text = "https://next-ir.example.test";
                login.ChildrenOfType<SettingsCheckbox>().Single().Current.Value = true;
                accountButton("保存连接设置").TriggerClick();
            });
            AddUntilStep("new connection saved and form ready", () => service.State.ServiceAddress == "https://next-ir.example.test/" && accountButton("登录").Enabled.Value);
            AddAssert("new connection alone still makes no request", () => requests, () => Is.Zero);
            AddStep("login using the saved new origin", () =>
            {
                usernameBox().Text = "player_one";
                login.ChildrenOfType<OsuPasswordTextBox>().Single().Text = "synthetic-password-123";
                accountButton("登录").TriggerClick();
            });
            AddUntilStep("new origin login succeeds", () => service.State.Account?.Id == 1);
            AddAssert("only the saved new origin receives login", () => loginRequests == 1 && lastLoginOrigin == "https://next-ir.example.test");
            AddAssert("old API remains unused", () => legacyRequests, () => Is.Zero);
        }

        [Test]
        public void TestHiddenLoginAndLatePrivateReadCannotResurrect()
        {
            AddStep("open login and configure", () => login.Show());
            AddUntilStep("form loaded", () => login.ChildrenOfType<LoginForm>().Any(form => form.IsLoaded));
            AddStep("save connection", configureFromForm);
            AddUntilStep("connection saved", () => service.State.Enabled && accountButton("登录").Enabled.Value);
            AddStep("open original menu before delayed login", showAccountMenu);
            AddUntilStep("visible login form ready", () => login.State.Value == Visibility.Visible && accountButton("登录").Enabled.Value);
            AddStep("start delayed login", () =>
            {
                holdLogin = true;
                usernameBox().Text = "player_one";
                login.ChildrenOfType<OsuPasswordTextBox>().Single().Text = "synthetic-password-123";
                accountButton("登录").TriggerClick();
            });
            AddUntilStep("login held", () => delayedLogin != null);
            AddAssert("login window stays visible during request", () => login.State.Value, () => Is.EqualTo(Visibility.Visible));
            AddStep("close original login panel", () => login.Hide());
            AddUntilStep("login window is actually closed", () => login.State.Value == Visibility.Hidden);
            AddStep("release late login", () => delayedLogin!.TrySetResult(session(1)));
            AddUntilStep("late login stops", () => !service.State.Busy);
            AddAssert("closing the form cancels the HTTP request", () => delayedLoginWasCancelled, () => Is.True);
            AddAssert("closed login does not acquire an account", () => service.State.Account, () => Is.Null);
            AddStep("reopen and login", () => login.Show());
            AddUntilStep("form ready after cancellation", () => login.State.Value == Visibility.Visible && accountButton("登录").Enabled.Value);
            AddStep("login normally", () =>
            {
                login.ChildrenOfType<OsuPasswordTextBox>().Single().Text = "synthetic-password-123";
                accountButton("登录").TriggerClick();
            });
            AddUntilStep("account active", () => service.State.Account?.Id == 1);
            AddStep("start delayed private history", () => { login.Hide(); holdHistory = true; profile.ShowOwnOmsUser(); });
            AddUntilStep("private response held", () => delayedHistory != null);
            AddStep("close profile before response", () => profile.Hide());
            AddUntilStep("profile window is actually closed", () => profile.State.Value == Visibility.Hidden);
            AddStep("release old private response", () => delayedHistory!.TrySetResult(history(1, 1)));
            AddUntilStep("private request settles", () => !service.State.Busy);
            AddAssert("late closed profile stays empty", () => !hasProfileText("本局 ID：") && !hasProfileText("合成 OMS 新局"));
            AddStep("read profile again", () => profile.ShowOwnOmsUser());
            AddUntilStep("fresh own history works", () => hasProfileText("本局 ID："));
            AddStep("reject different account in server response", () =>
            {
                wrongHistoryOwner = true;
                profile.ChildrenOfType<SettingsButton>().Single(button => button.Text.ToString() == "刷新本人记录").TriggerClick();
            });
            AddUntilStep("wrong owner reports failure and clears rows", () => hasProfileText("本人记录格式不正确") && !hasProfileText("本局 ID："));
            AddAssert("legacy API remains unused after delayed requests", () => legacyRequests, () => Is.Zero);
        }

        private void configureFromForm()
        {
            login.ChildrenOfType<OsuTextBox>().Single(box => box.Name == "IR service address").Text = "https://ir.example.test";
            login.ChildrenOfType<SettingsCheckbox>().Single().Current.Value = true;
            accountButton("保存连接设置").TriggerClick();
        }

        private void showAccountMenu()
        {
            if (login.State.Value == Visibility.Hidden)
                userButton.TriggerClick();
        }

        private OsuTextBox usernameBox() => login.ChildrenOfType<OsuTextBox>().Single(box => box.Name == "OMS username");
        private SettingsButton accountButton(string text) => login.ChildrenOfType<SettingsButton>().Single(button => button.Text.ToString() == text);

        private bool hasProfileText(string part) => profile.ChildrenOfType<OsuTextFlowContainer>()
                                                          .Any(flow => string.Concat(flow.ChildrenOfType<SpriteText>().Select(text => text.Text.ToString())).Contains(part, StringComparison.Ordinal))
                                                  || profile.ChildrenOfType<SpriteText>().Any(text => text.Text.ToString().Contains(part, StringComparison.Ordinal));

        private async Task<JObject> response(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref requests);
            string path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/auth/login", StringComparison.Ordinal) || path.EndsWith("/auth/register", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref loginRequests);
                lastLoginOrigin = request.RequestUri.GetLeftPart(UriPartial.Authority);
                string username = (string)JObject.Parse(await request.Content!.ReadAsStringAsync(token))["username"]!;
                long id = username == "player_two" ? 2 : 1;
                if (holdLogin)
                {
                    holdLogin = false;
                    delayedLogin = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
                    JObject result = await delayedLogin.Task;
                    delayedLoginWasCancelled = token.IsCancellationRequested;
                    return result;
                }
                return session(id);
            }
            if (path.EndsWith("/auth/logout", StringComparison.Ordinal))
                return new JObject();
            if (path.Contains("/scores/user/", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref historyRequests);
                long id = long.Parse(path[(path.LastIndexOf('/') + 1)..], System.Globalization.CultureInfo.InvariantCulture);
                int page = request.RequestUri.Query.Contains("page=2", StringComparison.Ordinal) ? 2 : 1;
                if (holdHistory)
                {
                    holdHistory = false;
                    delayedHistory = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
                    return await delayedHistory.Task;
                }
                return history(wrongHistoryOwner ? 2 : id, page);
            }
            throw new InvalidOperationException("Unexpected synthetic OMS request: " + path);
        }

        private static JObject session(long id) => new JObject
        {
            ["user"] = new JObject { ["id"] = id, ["username"] = id == 1 ? "player_one" : "player_two" },
            ["access_token"] = "synthetic-access-player-" + id,
            ["refresh_token"] = "synthetic-refresh-player-" + id,
            ["expires_in"] = 3600,
        };

        private static JObject history(long id, int page) => new JObject
        {
            ["items"] = new JArray(Enumerable.Range(page == 2 ? 21 : 1, id == 1 && page == 1 ? 20 : 1).Select(index => new JObject
            {
                ["id"] = index + (id == 2 ? 100 : 0), ["user_id"] = id, ["submission_id"] = $"00000000-0000-4000-a000-{index + (id == 2 ? 100 : 0):000000000000}", ["ruleset"] = "bms",
                ["chart"] = new JObject { ["md5"] = new string('a', 32), ["title"] = id == 1 ? "合成 OMS 新局 " + index : "player_two 的新局" },
                ["ex_score"] = 175, ["max_ex_score"] = 200, ["accuracy"] = 0.875, ["max_combo"] = 100, ["passed"] = true, ["public_board"] = true,
                ["group_id"] = new string('c', 64), ["group_label"] = "7K · 普通 · 合成条件", ["played_at"] = "2026-10-05T00:00:00Z",
                ["statistics"] = new JObject { ["perfect"] = 75, ["great"] = 25 },
                ["ruleset_data"] = new JObject { ["clear_lamp"] = 4, ["final_gauge"] = 0.85 },
            })),
            ["total"] = id == 1 ? 21 : 1, ["limit"] = 20, ["page"] = page,
        };

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            service?.Dispose();
            http?.Dispose();
        }

        private sealed class Handler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<JObject>> handler;
            public Handler(Func<HttpRequestMessage, CancellationToken, Task<JObject>> handler) => this.handler = handler;
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent((await handler(request, cancellationToken)).ToString(), Encoding.UTF8, "application/json"),
            };
        }

        private sealed class Credentials : IOmsIrCredentialStore
        {
            private readonly Dictionary<string, OmsIrSession> values = new Dictionary<string, OmsIrSession>();
            public OmsIrSession? Read(string target) => values.GetValueOrDefault(target);
            public void Write(string target, OmsIrSession session) => values[target] = session;
            public void Delete(string target) => values.Remove(target);
        }
    }
}
