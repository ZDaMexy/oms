// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Graphics;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Shared OMS transformer shell for global skinnable container layers.
    /// </summary>
    public class OmsSkinTransformer : SkinTransformer
    {
        public OmsSkinTransformer(ISkin skin)
            : base(skin)
        {
        }

        public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
        {
            if (lookup is not GlobalSkinnableContainerLookup containerLookup)
                return base.GetDrawableComponent(lookup);

            Drawable? skinnedComponent = base.GetDrawableComponent(lookup);

            if (skinnedComponent != null)
                return skinnedComponent;

            return containerLookup.Lookup switch
            {
                GlobalSkinnableContainers.MainHUDComponents when containerLookup.Ruleset == null => createShellContainer(),
                GlobalSkinnableContainers.SongSelect when containerLookup.Ruleset == null => createShellContainer(),
                GlobalSkinnableContainers.Results when containerLookup.Ruleset == null => createShellContainer(),
                GlobalSkinnableContainers.Playfield => createShellContainer(),
                _ => null,
            };
        }

        private static DefaultSkinComponentsContainer createShellContainer()
            => new DefaultSkinComponentsContainer(static _ =>
            {
            });
    }
}
