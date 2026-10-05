// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.IR;
using osu.Game.Overlays.Settings;
using osu.Game.Resources.Localisation.Web;
using osuTK;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Login
{
    public partial class LoginForm : FillFlowContainer
    {
        private TextBox username = null!;
        private TextBox password = null!;
        private ShakeContainer shakeSignIn = null!;
        private readonly bool useOmsAccount;
        private readonly Bindable<string> serviceAddress = new Bindable<string>(string.Empty);
        private readonly Bindable<bool> irEnabled = new Bindable<bool>();
        private readonly List<SettingsButton> omsButtons = new List<SettingsButton>();
        private ErrorTextFlowContainer? omsError;
        private OsuTextFlowContainer? omsStatus;
        private CancellationTokenSource? accountOperation;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OmsIrService? ir { get; set; }

        public Action? RequestHide;

        public override bool AcceptsFocus => true;

        public LoginForm(bool useOmsAccount = false)
        {
            this.useOmsAccount = useOmsAccount;
        }

        [BackgroundDependencyLoader(permitNulls: true)]
        private void load(OsuConfigManager config, AccountCreationOverlay accountCreation)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, SettingsSection.ITEM_SPACING);

            if (useOmsAccount)
            {
                loadOmsAccount();
                return;
            }

            bool hasWebsiteRoot = !string.IsNullOrEmpty(api.Endpoints.WebsiteUrl);

            ErrorTextFlowContainer errorText;
            LinkFlowContainer forgottenPasswordLink;

            Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Horizontal = SettingsPanel.CONTENT_MARGINS },
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0f, SettingsSection.ITEM_SPACING),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = LoginPanelStrings.Account.ToUpper(),
                            Font = OsuFont.GetFont(weight: FontWeight.Bold),
                        },
                        username = new OsuTextBox
                        {
                            InputProperties = new TextInputProperties(TextInputType.Username, false),
                            PlaceholderText = UsersStrings.LoginUsername.ToLower(),
                            RelativeSizeAxes = Axes.X,
                            Text = api.ProvidedUsername,
                            TabbableContentContainer = this
                        },
                        password = new OsuPasswordTextBox
                        {
                            PlaceholderText = UsersStrings.LoginPassword.ToLower(),
                            RelativeSizeAxes = Axes.X,
                            TabbableContentContainer = this,
                        },
                        errorText = new ErrorTextFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Alpha = 0,
                        },
                    },
                },
                new SettingsCheckbox
                {
                    LabelText = LoginPanelStrings.RememberUsername,
                    Current = config.GetBindable<bool>(OsuSetting.SaveUsername),
                },
                new SettingsCheckbox
                {
                    LabelText = LoginPanelStrings.StaySignedIn,
                    Current = config.GetBindable<bool>(OsuSetting.SavePassword),
                },
                forgottenPasswordLink = new LinkFlowContainer
                {
                    Padding = new MarginPadding { Horizontal = SettingsPanel.CONTENT_MARGINS },
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Children = new Drawable[]
                    {
                        shakeSignIn = new ShakeContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Child = new SettingsButton
                            {
                                Text = UsersStrings.LoginButton,
                                Action = performLogin
                            },
                        }
                    }
                },
                new SettingsButton
                {
                    Text = LoginPanelStrings.Register,
                    Action = () =>
                    {
                        RequestHide?.Invoke();
                        accountCreation.Show();
                    }
                }
            };

            if (hasWebsiteRoot)
                forgottenPasswordLink.AddLink(LayoutStrings.PopupLoginLoginForgot, $"{api.Endpoints.WebsiteUrl}/home/password-reset");

            password.OnCommit += (_, _) => performLogin();

            if (api.LastLoginError?.Message is string error)
            {
                errorText.Alpha = 1;
                errorText.AddErrors(new[] { error });
            }
        }

        private void performLogin()
        {
            if (useOmsAccount)
            {
                loginOmsAccount(false);
                return;
            }
            if (!string.IsNullOrEmpty(username.Text) && !string.IsNullOrEmpty(password.Text))
                api.Login(username.Text, password.Text);
            else
                shakeSignIn.Shake();
        }

        private void loadOmsAccount()
        {
            OmsIrState state = ir!.State;
            serviceAddress.Value = state.ServiceAddress;
            irEnabled.Value = state.Enabled;

            Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Horizontal = SettingsPanel.CONTENT_MARGINS },
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, SettingsSection.ITEM_SPACING),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText { Text = "OMS 账号", Font = OsuFont.GetFont(weight: FontWeight.Bold) },
                        new OsuTextBox
                        {
                            Name = "IR service address",
                            PlaceholderText = "HTTPS 服务地址",
                            RelativeSizeAxes = Axes.X,
                            Current = serviceAddress,
                            TabbableContentContainer = this,
                        },
                        username = new OsuTextBox
                        {
                            Name = "OMS username",
                            InputProperties = new TextInputProperties(TextInputType.Username, false),
                            PlaceholderText = "账号名",
                            RelativeSizeAxes = Axes.X,
                            Text = state.Account?.Username ?? string.Empty,
                            TabbableContentContainer = this,
                        },
                        password = new OsuPasswordTextBox
                        {
                            PlaceholderText = "密码",
                            RelativeSizeAxes = Axes.X,
                            TabbableContentContainer = this,
                        },
                        omsError = new ErrorTextFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Alpha = 0 },
                        omsStatus = new OsuTextFlowContainer(sprite => sprite.Font = OsuFont.GetFont(size: 14))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Text = state.RequiresLogin && state.Account != null ? $"请重新登录 {state.Account.Username}。原账号待交已保留。" : "离线游玩无需账号。连接后只提交登录后开始的新局。",
                        },
                    },
                },
                new SettingsCheckbox { LabelText = "启用 IR", Current = irEnabled },
                omsButton("保存连接设置", () => runAccountOperation(token => ir.ConfigureAsync(serviceAddress.Value, irEnabled.Value, token))),
                omsButton("登录", () => loginOmsAccount(false)),
                omsButton("注册并登录", () => loginOmsAccount(true)),
            };
            password.OnCommit += (_, _) => performLogin();
        }

        private SettingsButton omsButton(string label, Action action)
        {
            var result = new SettingsButton { Text = label, Action = action };
            omsButtons.Add(result);
            return result;
        }

        private void loginOmsAccount(bool register)
        {
            string account = username.Text;
            string secret = password.Text;
            password.Text = string.Empty;
            runAccountOperation(token => ir!.LoginAsync(account, secret, register, token));
        }

        private async void runAccountOperation(Func<CancellationToken, Task> action)
        {
            if (accountOperation != null)
                return;
            var cancellation = accountOperation = new CancellationTokenSource();
            foreach (var button in omsButtons)
                button.Enabled.Value = false;
            omsError!.Clear();
            omsError.Alpha = 0;
            try
            {
                await action(cancellation.Token).ConfigureAwait(false);
                Schedule(() => { if (!cancellation.IsCancellationRequested) omsStatus!.Text = ir!.State.Message; });
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (OmsIrException exception)
            {
                Schedule(() => showAccountError(exception.Message, cancellation));
            }
            catch (IOException exception)
            {
                Schedule(() => showAccountError($"连接设置无法保存：{exception.Message}", cancellation));
            }
            finally
            {
                Schedule(() =>
                {
                    if (ReferenceEquals(accountOperation, cancellation))
                    {
                        accountOperation = null;
                        foreach (var button in omsButtons)
                            button.Enabled.Value = true;
                    }
                    cancellation.Dispose();
                });
            }
        }

        private void showAccountError(string message, CancellationTokenSource cancellation)
        {
            if (cancellation.IsCancellationRequested || IsDisposed)
                return;
            omsError!.Alpha = 1;
            omsError.AddErrors(new[] { message });
        }

        public void CancelAccountOperation()
        {
            if (!useOmsAccount || !IsLoaded)
                return;
            password.Text = string.Empty;
            accountOperation?.Cancel();
            accountOperation = null;
            foreach (var button in omsButtons)
                button.Enabled.Value = true;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (IsLoaded)
                CancelAccountOperation();
            base.Dispose(isDisposing);
        }

        protected override bool OnClick(ClickEvent e) => true;

        protected override void OnFocus(FocusEvent e)
        {
            Schedule(() => { GetContainingFocusManager()!.ChangeFocus(string.IsNullOrEmpty(username.Text) ? username : password); });
        }
    }
}
