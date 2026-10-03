// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.IR;
using osu.Game.Overlays;

namespace osu.Game.Tests.Visual.Overlays
{
    public partial class TestSceneOmsIrOverlay : OsuManualInputManagerTestScene
    {
        private OmsIrService service = null!;
        private HttpClient http = null!;
        private OmsIrOverlay overlay = null!;
        private int requests;
        private bool malformed;

        [BackgroundDependencyLoader]
        private void load()
        {
            http = new HttpClient(new Handler(request =>
            {
                Interlocked.Increment(ref requests);
                string path = request.RequestUri!.AbsolutePath;
                if (path.EndsWith("/auth/login", StringComparison.Ordinal))
                    return new JObject
                    {
                        ["user"] = new JObject { ["id"] = 1, ["username"] = "ir_ui_synthetic" },
                        ["access_token"] = "synthetic-access",
                        ["refresh_token"] = "synthetic-refresh",
                        ["expires_in"] = 3600,
                    };
                if (path.EndsWith("/auth/logout", StringComparison.Ordinal))
                    return new JObject();
                if (malformed)
                    return new JObject { ["items"] = new JArray("incorrect server record"), ["total"] = 1, ["limit"] = 20 };
                JObject response = new JObject { ["page"] = 1, ["limit"] = 20, ["total"] = 1 };
                if (path.EndsWith("/charts", StringComparison.Ordinal))
                    response["items"] = new JArray(new JObject
                    {
                        ["chart"] = chart(),
                        ["groups"] = new JArray(new JObject { ["id"] = new string('c', 64), ["label"] = "7K · 普通 · 合成条件" }),
                    });
                else if (path.Contains("/scores/chart/", StringComparison.Ordinal))
                    response["items"] = new JArray(new JObject
                    {
                        ["rank"] = 1,
                        ["user"] = new JObject { ["id"] = 1, ["username"] = "ir_ui_synthetic" },
                        ["score"] = score(),
                        ["best_lamp"] = 8,
                        ["best_lamp_score_id"] = 2,
                    });
                else
                    response["items"] = new JArray(score());
                return response;
            }));
            Dependencies.Cache(service = new OmsIrService(LocalStorage.GetStorageForDirectory("ir-ui-synthetic"), http, new Credentials()));
        }

        [Test]
        public void TestOptInLoginBoardHistoryAndMalformedResponse()
        {
            AddStep("show IR", () =>
            {
                Child = overlay = new OmsIrOverlay();
                overlay.Show();
            });
            AddUntilStep("IR controls loaded", () => overlay.IsLoaded);
            AddAssert("default address stays empty", () => service.State.ServiceAddress, () => Is.Empty);
            AddAssert("default view makes no request", () => requests, () => Is.Zero);
            AddStep("save opt-in connection", () =>
            {
                overlay.ChildrenOfType<FormTextBox>().Single(box => box.Caption.ToString() == "服务地址").Current.Value = "https://ir.example.test";
                overlay.ChildrenOfType<FormCheckBox>().Single().Current.Value = true;
                click("保存连接设置");
            });
            AddUntilStep("connection enabled", () => service.State.Enabled && button("登录").Enabled.Value);
            AddStep("login from the form", () =>
            {
                overlay.ChildrenOfType<FormTextBox>().Single(box => box.Caption.ToString() == "账号名").Current.Value = "ir_ui_synthetic";
                overlay.ChildrenOfType<OsuPasswordTextBox>().Single().Text = "synthetic-password-123";
                click("登录");
            });
            AddUntilStep("account visible and history available", () => service.State.Account != null && button("本人记录").Enabled.Value);
            AddAssert("password cleared after login", () => overlay.ChildrenOfType<OsuPasswordTextBox>().Single().Text, () => Is.Empty);
            AddStep("read chart catalogue", () => click("读取谱面"));
            AddUntilStep("actual condition appears", () => hasText("合成 IR 谱面") && buttonExists("7K · 普通 · 合成条件") && button("读取谱面").Enabled.Value);
            AddStep("open same-condition board", () => click("7K · 普通 · 合成条件"));
            AddUntilStep("best score and separate lamp appear", () => hasText("EX 175 / 200") && hasText("FULL COMBO") && hasText("最佳灯来自另一局"));
            AddUntilStep("board request completed", () => button("本人记录").Enabled.Value);
            AddStep("open own history", () => click("本人记录"));
            AddUntilStep("history links back to its board", () => buttonExists("查看同条件榜") && button("本人记录").Enabled.Value);
            AddStep("read saved score details", () => click("查看这局详情"));
            AddAssert("final gauge and this play's lamp are shown", () => hasText("85.0") && hasText("本局灯：CLEAR"));
            AddStep("return to board through history", () => click("查看同条件榜"));
            AddUntilStep("board returns", () => hasText("最佳灯来自另一局") && button("本人记录").Enabled.Value);
            AddStep("return malformed server response", () => { malformed = true; click("刷新"); });
            AddUntilStep("bad response reports failure without breaking the overlay", () => hasText("列表格式不正确") && button("本人记录").Enabled.Value);
            AddStep("restore service and read own history", () => { malformed = false; click("本人记录"); });
            AddUntilStep("own records loaded", () => buttonExists("查看同条件榜") && button("本人记录").Enabled.Value);
            AddStep("logout", () => click("退出账号"));
            AddUntilStep("private history is cleared", () => service.State.Account == null && !buttonExists("查看这局详情"));
            AddStep("close and reopen", () => { click("关闭"); overlay.Show(); });
            AddAssert("overlay remains usable after completed operations", () => overlay.State.Value, () => Is.EqualTo(Visibility.Visible));
        }

        private bool hasText(string part) => overlay.ChildrenOfType<OsuTextFlowContainer>().Any(flow => string.Concat(flow.ChildrenOfType<SpriteText>().Select(sprite => sprite.Text.ToString())).Contains(part, StringComparison.Ordinal));
        private bool buttonExists(string label) => overlay.ChildrenOfType<FormButton>().Any(item => item.ButtonText.ToString() == label);
        private FormButton button(string label) => overlay.ChildrenOfType<FormButton>().Single(item => item.ButtonText.ToString() == label);
        private void click(string label) => button(label).ChildrenOfType<Button>().Single().TriggerClick();

        private static JObject chart() => new JObject { ["title"] = "合成 IR 谱面", ["md5"] = new string('a', 32) };
        private static JObject score() => new JObject
        {
            ["id"] = 1, ["ruleset"] = "bms", ["chart"] = chart(), ["ex_score"] = 175, ["max_ex_score"] = 200,
            ["accuracy"] = 0.875, ["max_combo"] = 100, ["passed"] = true, ["public_board"] = true,
            ["group_id"] = new string('c', 64), ["group_label"] = "7K · 普通 · 合成条件", ["played_at"] = "2026-10-02T12:00:00Z",
            ["statistics"] = new JObject { ["perfect"] = 75, ["great"] = 25 },
            ["ruleset_data"] = new JObject { ["clear_lamp"] = 4, ["final_gauge"] = 0.85 },
        };

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            service?.Dispose();
            http?.Dispose();
        }

        private sealed class Handler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, JObject> response;
            public Handler(Func<HttpRequestMessage, JObject> response) => this.response = response;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response(request).ToString(), Encoding.UTF8, "application/json"),
            });
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
