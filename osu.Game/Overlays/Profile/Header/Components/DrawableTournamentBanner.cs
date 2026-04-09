// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Game.Graphics.Containers;
using osu.Game.Online.API;
using osu.Game.Users;
using osuTK.Graphics;

namespace osu.Game.Overlays.Profile.Header.Components
{
    [LongRunningLoad]
    public partial class DrawableTournamentBanner : OsuClickableContainer
    {
        private const float banner_aspect_ratio = 60 / 1000f;
        private readonly TournamentBanner banner;

        public DrawableTournamentBanner(TournamentBanner banner)
        {
            this.banner = banner;
            RelativeSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load(LargeTextureStore textures, OsuGame? game, IAPIProvider api)
        {
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.FromHex("1f2533"),
                },
                new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Texture = textures.Get(banner.Image),
                }
            };

            if (game != null && game.OnlineFeaturesEnabled && !string.IsNullOrEmpty(api.Endpoints.WebsiteUrl))
                Action = () => game.OpenUrlExternally($@"{api.Endpoints.WebsiteUrl}/community/tournaments/{banner.TournamentId}");
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            this.FadeInFromZero(200);
        }

        protected override void Update()
        {
            base.Update();
            Height = DrawWidth * banner_aspect_ratio;
        }

        public override LocalisableString TooltipText => Action == null ? string.Empty : "view in browser";
    }
}
