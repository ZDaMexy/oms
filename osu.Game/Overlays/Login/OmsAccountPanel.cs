// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.IR;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.Overlays.Login
{
    public partial class OmsAccountPanel : FillFlowContainer
    {
        public Action? ShowProfile;
        public Action? ConnectionSettings;

        [Resolved]
        private OmsIrService ir { get; set; } = null!;

        private OsuTextFlowContainer status = null!;
        private readonly OmsIrAccount account;
        private readonly List<SettingsButton> buttons = new List<SettingsButton>();
        private CancellationTokenSource? operation;

        public OmsAccountPanel(OmsIrAccount account)
        {
            this.account = account;
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, SettingsSection.ITEM_SPACING);
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
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
                        new OsuSpriteText { Text = "已登录", Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold) },
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 48,
                            ColumnDimensions = new[] { new Dimension(GridSizeMode.Absolute, 60), new Dimension() },
                            Content = new[] { new Drawable[]
                            {
                                new Container
                                {
                                    Size = new Vector2(48),
                                    Masking = true,
                                    CornerRadius = 4,
                                    Children = new Drawable[]
                                    {
                                        new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background3 },
                                        new SpriteIcon { Icon = OsuIcon.Player, Size = new Vector2(26), Anchor = Anchor.Centre, Origin = Anchor.Centre },
                                    },
                                },
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Children = new Drawable[]
                                    {
                                        new TruncatingSpriteText { RelativeSizeAxes = Axes.X, Text = account.Username, Font = OsuFont.GetFont(size: 20) },
                                        new OsuSpriteText { Text = $"OMS #{account.Id}", Font = OsuFont.GetFont(size: 14) },
                                    },
                                },
                            } },
                        },
                        status = new OsuTextFlowContainer(sprite => sprite.Font = OsuFont.GetFont(size: 14))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                        },
                    },
                },
                button("个人资料", () => ShowProfile?.Invoke()),
                button("重试待交", () => run(ir.RetryPendingAsync)),
                button("连接设置", () => ConnectionSettings?.Invoke()),
                button("退出账号", () => run(ir.LogoutAsync)),
            };
            ir.StateChanged += stateChanged;
            applyState(ir.State);
        }

        private SettingsButton button(string label, Action action)
        {
            var result = new SettingsButton { Text = label, Action = action };
            buttons.Add(result);
            return result;
        }

        private void stateChanged(OmsIrState state) => Schedule(() => applyState(ir.State));

        private void applyState(OmsIrState state)
        {
            status.Text = $"{(state.Enabled ? "IR 已启用" : "IR 已关闭")} · {state.ServiceAddress}\n待交 {state.PendingCount} · 暂不能提交 {state.BlockedCount} · 其他账号待交 {state.WaitingOtherAccountCount}\n{state.Message}";
        }

        private async void run(Func<CancellationToken, Task> action)
        {
            if (operation != null)
                return;
            var cancellation = operation = new CancellationTokenSource();
            foreach (var item in buttons)
                item.Enabled.Value = false;
            try
            {
                await action(cancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (OmsIrException exception)
            {
                Schedule(() => { if (!cancellation.IsCancellationRequested) status.Text = exception.Message; });
            }
            finally
            {
                Schedule(() =>
                {
                    if (ReferenceEquals(operation, cancellation))
                    {
                        operation = null;
                        foreach (var item in buttons)
                            item.Enabled.Value = true;
                    }
                    cancellation.Dispose();
                });
            }
        }

        public void CancelAccountOperation()
        {
            operation?.Cancel();
            operation = null;
            foreach (var item in buttons)
                item.Enabled.Value = true;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (ir != null)
                ir.StateChanged -= stateChanged;
            CancelAccountOperation();
            base.Dispose(isDisposing);
        }
    }
}
