// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osuTK;
using osuTK.Graphics;
using Realms;

namespace osu.Game.Overlays.FirstRunSetup
{
    [LocalisableDescription(typeof(FirstRunSetupBeatmapScreenStrings), nameof(FirstRunSetupBeatmapScreenStrings.Header))]
    public partial class ScreenBeatmaps : WizardScreen
    {
        private OsuTextFlowContainer currentlyLoadedBeatmaps = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private RealmAccess realmAccess { get; set; } = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        private IDisposable? beatmapSubscription;

        [BackgroundDependencyLoader]
        private void load()
        {
            const string maniaOfficialUrl = "https://osu.ppy.sh/beatmapsets";
            const string maniaSayobotUrl = "https://osu.sayobot.cn/home";
            const string bmsDownloadUrl = "https://cloud.hakula.xyz/home?path=cloudreve%3A%2F%2F7aTX%40share";

            Vector2 buttonSize = new Vector2(320, 50);

            Content.Children = new Drawable[]
            {
                new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: CONTENT_FONT_SIZE))
                {
                    Colour = OverlayColourProvider.Content1,
                    Text = FirstRunSetupBeatmapScreenStrings.Description,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 30,
                    Children = new Drawable[]
                    {
                        currentlyLoadedBeatmaps = new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: HEADER_FONT_SIZE, weight: FontWeight.SemiBold))
                        {
                            Colour = OverlayColourProvider.Content2,
                            TextAnchor = Anchor.Centre,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            AutoSizeAxes = Axes.Both,
                        },
                    }
                },
                createSectionHeader(FirstRunSetupBeatmapScreenStrings.ManiaHeader),
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Full,
                    Spacing = new Vector2(12),
                    Children = new Drawable[]
                    {
                        createLinkButton(FirstRunSetupBeatmapScreenStrings.ManiaOfficialButton, maniaOfficialUrl, buttonSize, colours.Pink3),
                        createLinkButton(FirstRunSetupBeatmapScreenStrings.ManiaSayobotButton, maniaSayobotUrl, buttonSize, colours.Purple2),
                    }
                },
                createSectionHeader(FirstRunSetupBeatmapScreenStrings.BmsHeader),
                createLinkButton(FirstRunSetupBeatmapScreenStrings.BmsDownloadButton, bmsDownloadUrl, buttonSize, colours.Blue3),
                new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: CONTENT_FONT_SIZE))
                {
                    Colour = OverlayColourProvider.Content1,
                    Text = FirstRunSetupBeatmapScreenStrings.ImportInstructions,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y
                },
                new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: 12))
                {
                    Colour = OverlayColourProvider.Content2,
                    Text = FirstRunSetupBeatmapScreenStrings.ExternalLinkDisclaimer,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            beatmapSubscription = realmAccess.RegisterForNotifications(r => r.All<BeatmapSetInfo>().Where(s => !s.DeletePending && !s.Protected), beatmapsChanged);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            beatmapSubscription?.Dispose();
        }

        private void beatmapsChanged(IRealmCollection<BeatmapSetInfo> sender, ChangeSet? changes) => Schedule(() =>
        {
            currentlyLoadedBeatmaps.Text = FirstRunSetupBeatmapScreenStrings.CurrentlyLoadedBeatmaps(sender.Count);

            if (sender.Count == 0)
            {
                currentlyLoadedBeatmaps.FadeColour(colours.Red1, 500, Easing.OutQuint);
            }
            else if (changes != null && (changes.DeletedIndices.Any() || changes.InsertedIndices.Any()))
            {
                currentlyLoadedBeatmaps.FadeColour(colours.Yellow)
                                       .FadeColour(OverlayColourProvider.Content2, 1500, Easing.OutQuint);

                currentlyLoadedBeatmaps.ScaleTo(1.1f)
                                       .ScaleTo(1, 1500, Easing.OutQuint);
            }
        });

        private Drawable createSectionHeader(LocalisableString title)
            => new OsuSpriteText
            {
                Text = title,
                Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                Colour = OverlayColourProvider.Content2,
            };

        private SettingsButtonV2 createLinkButton(LocalisableString text, string url, Vector2 size, Colour4 colour)
            => new SettingsButtonV2
            {
                RelativeSizeAxes = Axes.None,
                AutoSizeAxes = Axes.None,
                Width = size.X,
                Height = size.Y,
                Text = text,
                BackgroundColour = colour,
                Action = () => host.OpenUrlExternally(url),
            };
    }
}
