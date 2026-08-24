// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Game.Skinning;

namespace osu.Game.Graphics.Backgrounds
{
    internal partial class SkinBackground : Background
    {
        private readonly Skin skin;
        private SkinRevisionParticipantRegistration? revisionHolder;

        public SkinBackground(Skin skin, string fallbackTextureName)
            : base(fallbackTextureName)
        {
            this.skin = skin;
        }

        internal SkinBackground(
            Skin skin,
            string fallbackTextureName,
            SkinRevisionParticipantRegistration revisionHolder)
            : this(skin, fallbackTextureName)
        {
            this.revisionHolder = revisionHolder ?? throw new ArgumentNullException(nameof(revisionHolder));
        }

        [BackgroundDependencyLoader]
        private void load(SkinManager skinManager)
        {
            revisionHolder ??= skinManager.RegisterRevisionHolderForOwner(skin, nameof(SkinBackground));

            if (revisionHolder == null)
            {
                Expire();
                return;
            }

            Sprite.Texture = skin.GetTexture("menu-background") ?? Sprite.Texture;
        }

        public override bool Equals(Background? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return other.GetType() == GetType()
                   && ReferenceEquals(((SkinBackground)other).skin, skin);
        }

        protected override void Dispose(bool isDisposing)
        {
            try
            {
                // The sprite/texture consumer belongs to the retained owner. Tear that graph down before issuing the
                // final detach which may synchronously retire and dispose the owner.
                base.Dispose(isDisposing);
            }
            finally
            {
                revisionHolder?.Dispose();
                revisionHolder = null;
            }
        }
    }
}
