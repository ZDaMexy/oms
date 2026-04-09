// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK.Graphics;

namespace osu.Game.Overlays.Profile.Sections.Recent
{
    [LongRunningLoad]
    public partial class MedalIcon : Container
    {
        private readonly string slug;
        private readonly Sprite sprite;

        private string url => $@"https://s.ppy.sh/images/medals-client/{slug}@2x.png";

        public MedalIcon(string slug)
        {
            this.slug = slug;

            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.FromHex("1f2533"),
                },
                sprite = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load(LargeTextureStore textures)
        {
            sprite.Texture = textures.Get(url);
        }
    }
}
