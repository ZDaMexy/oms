// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics.Sprites;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>Fits visible text to its author-owned container while retaining the complete bound string.</summary>
    internal partial class GameplaySkinBoundedSpriteText : OsuSpriteText
    {
        public GameplaySkinBoundedSpriteText()
        {
            ((SpriteText)this).Truncate = true;
        }

        protected override void Update()
        {
            if (Parent != null)
                MaxWidth = Parent.ChildSize.X;
            base.Update();
        }
    }
}
