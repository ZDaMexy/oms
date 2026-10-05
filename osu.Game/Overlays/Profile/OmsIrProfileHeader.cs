// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.IR;
using osuTK;

namespace osu.Game.Overlays.Profile
{
    /// <summary>The existing profile header layout, with only facts provided by the OMS account service.</summary>
    public partial class OmsIrProfileHeader : ProfileHeader
    {
        public readonly Bindable<OmsIrAccount?> Account = new Bindable<OmsIrAccount?>();
        public readonly Bindable<string> ServiceAddress = new Bindable<string>(string.Empty);

        private OsuSpriteText username = null!;
        private OsuSpriteText identity = null!;
        private ExternalLinkButton externalLink = null!;
        private Box accountBackground = null!;

        protected override Drawable CreateContent() => new Container
        {
            RelativeSizeAxes = Axes.X,
            Height = 85,
            Children = new Drawable[]
            {
                accountBackground = new Box { RelativeSizeAxes = Axes.Both },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 85,
                    Padding = new MarginPadding { Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING, Vertical = 10 },
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(10, 0),
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            Size = new Vector2(65),
                            Masking = true,
                            CornerRadius = 20,
                            Children = new Drawable[]
                            {
                                new Box { RelativeSizeAxes = Axes.Both, Alpha = 0.08f },
                                new SpriteIcon { Icon = OsuIcon.Player, Size = new Vector2(34), Anchor = Anchor.Centre, Origin = Anchor.Centre },
                            },
                        },
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Direction = FillDirection.Vertical,
                            Children = new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(8, 0),
                                    Children = new Drawable[]
                                    {
                                        username = new OsuSpriteText { Font = OsuFont.GetFont(size: 24) },
                                        externalLink = new ExternalLinkButton { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft },
                                    },
                                },
                                identity = new OsuSpriteText { Font = OsuFont.GetFont(size: 14) },
                            },
                        },
                    },
                },
            },
        };

        protected override Drawable CreateTabControlContent() => Empty();

        protected override OverlayTitle CreateTitle() => new OmsProfileTitle();

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            accountBackground.Colour = colours.Background3;
            Account.BindValueChanged(_ => updateAccount(), true);
            ServiceAddress.BindValueChanged(_ => updateAccount());
        }

        private void updateAccount()
        {
            OmsIrAccount? account = Account.Value;
            username.Text = account?.Username ?? "未登录";
            identity.Text = account == null ? "完整游玩记录仅本人可见" : $"OMS #{account.Id}";
            externalLink.Link = account != null && ServiceAddress.Value.Length != 0
                ? new System.Uri(new System.Uri(ServiceAddress.Value), "users/?id=" + account.Id.ToString(CultureInfo.InvariantCulture)).AbsoluteUri
                : null;
            externalLink.Alpha = externalLink.Link == null ? 0 : 1;
        }

        private partial class OmsProfileTitle : OverlayTitle
        {
            public OmsProfileTitle()
            {
                Title = "个人资料";
                Icon = OsuIcon.Player;
            }
        }
    }
}
