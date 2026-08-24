// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Caching;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osuTK;

namespace osu.Game.Skinning
{
    /// <summary>
    /// A drawable which can be skinned via an <see cref="ISkinSource"/>.
    /// </summary>
    public partial class SkinnableDrawable : SkinReloadableDrawable
    {
        /// <summary>
        /// The displayed component.
        /// </summary>
        public Drawable Drawable { get; private set; } = null!;

        /// <summary>
        /// Whether the drawable component should be centered in available space.
        /// Defaults to true.
        /// </summary>
        public bool CentreComponent = true;

        public new Axes AutoSizeAxes
        {
            get => base.AutoSizeAxes;
            set => base.AutoSizeAxes = value;
        }

        protected readonly ISkinComponentLookup ComponentLookup;

        private readonly ConfineMode confineMode;

        /// <summary>
        /// Create a new skinnable drawable.
        /// </summary>
        /// <param name="lookup">The namespace-complete resource name for this skinnable element.</param>
        /// <param name="defaultImplementation">A function to create the default skin implementation of this element.</param>
        /// <param name="confineMode">How (if at all) the <see cref="Drawable"/> should be resize to fit within our own bounds.</param>
        public SkinnableDrawable(ISkinComponentLookup lookup, Func<ISkinComponentLookup, Drawable>? defaultImplementation = null, ConfineMode confineMode = ConfineMode.NoScaling)
            : this(lookup, confineMode)
        {
            createDefault = defaultImplementation;
        }

        protected SkinnableDrawable(ISkinComponentLookup lookup, ConfineMode confineMode = ConfineMode.NoScaling)
        {
            ComponentLookup = lookup;
            this.confineMode = confineMode;

            RelativeSizeAxes = Axes.Both;
        }

        /// <summary>
        /// Seeks to the 0-th frame if the content of this <see cref="SkinnableDrawable"/> is an <see cref="IFramedAnimation"/>.
        /// </summary>
        public void ResetAnimation() => (Drawable as IFramedAnimation)?.GotoFrame(0);

        private readonly Func<ISkinComponentLookup, Drawable>? createDefault;

        private readonly Cached scaling = new Cached();

        private bool isDefault;

        protected virtual Drawable CreateDefault(ISkinComponentLookup lookup) => createDefault?.Invoke(lookup) ?? Empty();

        /// <summary>
        /// Whether to apply size restrictions (specified via <see cref="confineMode"/>) to the default implementation.
        /// </summary>
        protected virtual bool ApplySizeRestrictionsToDefault => false;

        protected override void SkinChanged(ISkinSource skin)
        {
            var retrieved = skin.GetDrawableComponent(ComponentLookup);
            Drawable replacement;
            bool replacementIsDefault;

            if (retrieved == null)
            {
                replacement = CreateDefault(ComponentLookup);
                replacementIsDefault = true;
            }
            else
            {
                replacement = retrieved;
                replacementIsDefault = false;
            }

            if (CentreComponent)
            {
                replacement.Origin = Anchor.Centre;
                replacement.Anchor = Anchor.Centre;
            }

            Drawable? previous = Drawable;

            if (!ReferenceEquals(previous, replacement))
            {
                bool replacementAttached = false;

                try
                {
                    AddInternal(replacement);
                    replacementAttached = true;

                    // InternalChild replacement asynchronously queues old-child disposal. This synchronous boundary
                    // must complete before SkinReloadableDrawable adopts B and can release the final A revision lease.
                    if (previous != null && !RemoveInternal(previous, disposeImmediately: true))
                        throw new InvalidOperationException("The previous skinnable drawable could not be detached.");
                }
                catch
                {
                    if (replacementAttached && replacement.Parent != null)
                        RemoveInternal(replacement, disposeImmediately: true);

                    throw;
                }

                Drawable = replacement;
            }

            isDefault = replacementIsDefault;
            scaling.Invalidate();
        }

        protected override void Update()
        {
            base.Update();

            if (!scaling.IsValid)
            {
                try
                {
                    if (isDefault && !ApplySizeRestrictionsToDefault) return;

                    switch (confineMode)
                    {
                        case ConfineMode.ScaleToFit:
                            Drawable.RelativeSizeAxes = Axes.Both;
                            Drawable.Size = Vector2.One;
                            Drawable.Scale = Vector2.One;
                            Drawable.FillMode = FillMode.Fit;
                            break;
                    }
                }
                finally
                {
                    scaling.Validate();
                }
            }
        }
    }

    public enum ConfineMode
    {
        /// <summary>
        /// Don't apply any scaling. This allows the user element to be of any size, exceeding specified bounds.
        /// </summary>
        NoScaling,
        ScaleToFit,
    }
}
