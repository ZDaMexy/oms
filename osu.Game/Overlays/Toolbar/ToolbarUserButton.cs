// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.IR;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarUserButton : ToolbarOverlayToggleButton
    {
        private UpdateableAvatar avatar = null!;

        private IBindable<APIUser> localUser = null!;

        private LoadingSpinner spinner = null!;

        private SpriteIcon failingIcon = null!;

        private IBindable<APIState> apiState = null!;

        private OsuSpriteText usernameText = null!;
        private readonly bool useOmsAccount;

        [Resolved]
        private OmsIrService? ir { get; set; }

        public ToolbarUserButton(bool useOmsAccount = false)
        {
            this.useOmsAccount = useOmsAccount;
            ButtonContent.AutoSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours, IAPIProvider api, LoginOverlay? login)
        {
            Drawable identityVisual = useOmsAccount ? new SpriteIcon
            {
                Icon = OsuIcon.Player,
                Size = new Vector2(20),
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            } : avatar = new UpdateableAvatar(isInteractive: false) { RelativeSizeAxes = Axes.Both };
            Flow.AddRange(new Drawable[]
            {
                usernameText = new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Margin = new MarginPadding { Right = 5 },
                },
                new Container
                {
                    Masking = true,
                    CornerRadius = 4,
                    Size = new Vector2(32),
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Shadow,
                        Radius = 4,
                        Colour = Color4.Black.Opacity(0.1f),
                    },
                    Children = new Drawable[]
                    {
                        identityVisual,
                        spinner = new LoadingLayer(dimBackground: true, withBox: false)
                        {
                            BlockPositionalInput = false,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            RelativeSizeAxes = Axes.Both,
                        },
                        failingIcon = new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Alpha = 0,
                            Size = new Vector2(0.3f),
                            Icon = FontAwesome.Solid.ExclamationTriangle,
                            RelativeSizeAxes = Axes.Both,
                            Colour = colours.YellowLight,
                        },
                    }
                }
            });

            StateContainer = login;

            if (useOmsAccount)
            {
                ir!.StateChanged += omsStateChanged;
                applyOmsState(ir.State);
                return;
            }

            Flow.Add(new TransientUserStatisticsUpdateDisplay { Alpha = 0 });

            apiState = api.State.GetBoundCopy();
            apiState.BindValueChanged(onlineStateChanged, true);

            localUser = api.LocalUser.GetBoundCopy();
            localUser.BindValueChanged(userChanged, true);
        }

        private void omsStateChanged(OmsIrState state) => Schedule(() => applyOmsState(ir!.State));

        private void applyOmsState(OmsIrState state)
        {
            usernameText.Text = state.Account?.Username ?? "登录";
            TooltipText = state.RequiresLogin && state.Account != null ? "请重新登录原账号，待交已保留"
                : state.Account == null ? "登录 OMS 账号"
                : $"OMS #{state.Account.Id} · 待上传 {state.PendingCount}";
            failingIcon.FadeTo(state.RequiresLogin && state.Account != null ? 1 : 0, 200, Easing.OutQuint);
            if (state.Busy)
                spinner.Show();
            else
                spinner.Hide();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (useOmsAccount && ir != null)
                ir.StateChanged -= omsStateChanged;
            base.Dispose(isDisposing);
        }

        private void userChanged(ValueChangedEvent<APIUser> user) => Schedule(() =>
        {
            usernameText.Text = user.NewValue.Username;
            avatar.User = user.NewValue;
        });

        private void onlineStateChanged(ValueChangedEvent<APIState> state) => Schedule(() =>
        {
            failingIcon.FadeTo(state.NewValue == APIState.Failing || state.NewValue == APIState.RequiresSecondFactorAuth ? 1 : 0, 200, Easing.OutQuint);

            switch (state.NewValue)
            {
                case APIState.Connecting:
                    TooltipText = ToolbarStrings.Connecting;
                    spinner.Show();
                    break;

                case APIState.Failing:
                    TooltipText = ToolbarStrings.AttemptingToReconnect;
                    spinner.Show();
                    failingIcon.Icon = FontAwesome.Solid.ExclamationTriangle;
                    break;

                case APIState.RequiresSecondFactorAuth:
                    TooltipText = ToolbarStrings.VerificationRequired;
                    spinner.Show();
                    failingIcon.Icon = FontAwesome.Solid.Key;
                    break;

                case APIState.Offline:
                case APIState.Online:
                    TooltipText = string.Empty;
                    spinner.Hide();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(state.NewValue));
            }
        });
    }
}
