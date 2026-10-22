// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.IR;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;
using WebCommonStrings = osu.Game.Resources.Localisation.Web.CommonStrings;

namespace osu.Game.Online.Chat
{
    public partial class ExternalLinkOpener : Component
    {
        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved]
        private INotificationOverlay? notificationOverlay { get; set; }

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OmsIrService? omsIr { get; set; }

        private Bindable<bool> externalLinkWarning = null!;

        [BackgroundDependencyLoader(true)]
        private void load(OsuConfigManager config)
        {
            externalLinkWarning = config.GetBindable<bool>(OsuSetting.ExternalLinkWarning);
        }

        public void OpenUrlExternally(string url, LinkWarnMode warnMode = LinkWarnMode.Default)
        {
            if (url.StartsWith('/'))
                url = $"{api.Endpoints.WebsiteUrl}{url}";

            if (!url.CheckIsValidUrl())
            {
                notificationOverlay?.Post(new SimpleErrorNotification
                {
                    Text = NotificationsStrings.UnsupportedOrDangerousUrlProtocol(url),
                });

                return;
            }

            bool isTrustedDomain = IsTrustedOrigin(url, api.Endpoints.WebsiteUrl)
                                   || IsTrustedOrigin(url, omsIr?.State.ServiceAddress);
            bool shouldWarn;

            switch (warnMode)
            {
                case LinkWarnMode.Default:
                    shouldWarn = externalLinkWarning.Value && !isTrustedDomain;
                    break;

                case LinkWarnMode.AlwaysWarn:
                    shouldWarn = true;
                    break;

                case LinkWarnMode.NeverWarn:
                    shouldWarn = false;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(warnMode), warnMode, null);
            }

            if (dialogOverlay != null && shouldWarn)
                dialogOverlay.Push(new ExternalLinkDialog(url, () => host.OpenUrlExternally(url), () => game?.CopyToClipboard(url)));
            else
                host.OpenUrlExternally(url);
        }

        internal static bool IsTrustedOrigin(string url, string? origin)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? target)
                || !Uri.TryCreate(origin, UriKind.Absolute, out Uri? trusted)
                || (trusted.Scheme != Uri.UriSchemeHttp && trusted.Scheme != Uri.UriSchemeHttps)
                || target.UserInfo.Length != 0 || trusted.UserInfo.Length != 0)
                return false;

            return target.Scheme.Equals(trusted.Scheme, StringComparison.OrdinalIgnoreCase)
                   && target.IdnHost.Equals(trusted.IdnHost, StringComparison.OrdinalIgnoreCase)
                   && target.Port == trusted.Port;
        }

        public partial class ExternalLinkDialog : PopupDialog
        {
            public ExternalLinkDialog(string url, Action openExternalLinkAction, Action copyExternalLinkAction)
            {
                HeaderText = DialogStrings.CautionHeaderText;
                BodyText = $"Are you sure you want to open the following link in a web browser?\n\n{url}";

                Icon = FontAwesome.Solid.ExclamationTriangle;

                Buttons = new PopupDialogButton[]
                {
                    new PopupDialogOkButton
                    {
                        Text = @"Open in browser",
                        Action = openExternalLinkAction
                    },
                    new PopupDialogCancelButton
                    {
                        Text = CommonStrings.CopyLink,
                        Action = copyExternalLinkAction
                    },
                    new PopupDialogCancelButton
                    {
                        Text = WebCommonStrings.ButtonsCancel,
                    },
                };
            }
        }
    }
}
