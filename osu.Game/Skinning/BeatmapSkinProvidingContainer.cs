// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Audio;
using osu.Game.Configuration;
using osu.Game.Skinning.Gameplay;
using osu.Game.Storyboards;

namespace osu.Game.Skinning
{
    /// <summary>
    /// A container which overrides existing skin options with beatmap-local values.
    /// </summary>
    public partial class BeatmapSkinProvidingContainer : SkinProvidingContainer
    {
        private Bindable<bool> beatmapSkins = null!;
        private Bindable<bool> beatmapColours = null!;
        private Bindable<bool> beatmapHitsounds = null!;

        protected override bool AllowConfigurationLookup
        {
            get
            {
                if (beatmapSkins == null)
                    throw new InvalidOperationException($"{nameof(BeatmapSkinProvidingContainer)} needs to be loaded before being consumed.");

                return beatmapSkins.Value;
            }
        }

        protected override bool AllowColourLookup
        {
            get
            {
                if (beatmapColours == null)
                    throw new InvalidOperationException($"{nameof(BeatmapSkinProvidingContainer)} needs to be loaded before being consumed.");

                return beatmapColours.Value;
            }
        }

        protected override bool AllowDrawableLookup(ISkinComponentLookup lookup)
        {
            if (beatmapSkins == null)
                throw new InvalidOperationException($"{nameof(BeatmapSkinProvidingContainer)} needs to be loaded before being consumed.");

            return beatmapSkins.Value;
        }

        protected override bool AllowTextureLookup(string componentName)
        {
            if (beatmapSkins == null)
                throw new InvalidOperationException($"{nameof(BeatmapSkinProvidingContainer)} needs to be loaded before being consumed.");

            return beatmapSkins.Value;
        }

        protected override bool AllowSampleLookup(ISampleInfo sampleInfo)
        {
            if (beatmapSkins == null)
                throw new InvalidOperationException($"{nameof(BeatmapSkinProvidingContainer)} needs to be loaded before being consumed.");

            return sampleInfo is StoryboardSampleInfo || beatmapHitsounds.Value;
        }

        private readonly ISkin skin;
        private readonly ISkin? compatibilityFallback;
        private readonly Func<IReadOnlyDependencyContainer, GameplaySkinLayoutPreparationResult>? prepareGameplayLayout;
        private readonly Drawable? delayedChild;
        private readonly bool affectsGameplayLayoutPublication;
        private IReadOnlyDependencyContainer? childDependencies;

        private Bindable<Skin> currentSkin = null!;

        public BeatmapSkinProvidingContainer(
            ISkin skin,
            ISkin? compatibilityFallback = null,
            Func<IReadOnlyDependencyContainer, GameplaySkinLayoutPreparationResult>? prepareGameplayLayout = null,
            Drawable? delayedChild = null,
            bool affectsGameplayLayoutPublication = false)
            : base(skin)
        {
            this.skin = skin;
            this.compatibilityFallback = compatibilityFallback;
            this.prepareGameplayLayout = prepareGameplayLayout;
            this.delayedChild = delayedChild;
            this.affectsGameplayLayoutPublication = affectsGameplayLayoutPublication;
        }

        private protected override bool AffectsGameplayLayoutPublication => affectsGameplayLayoutPublication;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var config = parent.Get<OsuConfigManager>();

            beatmapSkins = config.GetBindable<bool>(OsuSetting.BeatmapSkins);
            beatmapColours = config.GetBindable<bool>(OsuSetting.BeatmapColours);
            beatmapHitsounds = config.GetBindable<bool>(OsuSetting.BeatmapHitsounds);

            return childDependencies = base.CreateChildDependencies(parent);
        }

        [BackgroundDependencyLoader]
        private void load(SkinManager skins)
        {
            beatmapSkins.BindValueChanged(_ => TriggerSourceChanged());
            beatmapColours.BindValueChanged(_ => TriggerSourceChanged());
            beatmapHitsounds.BindValueChanged(_ => TriggerSourceChanged());

            currentSkin = skins.CurrentSkin.GetBoundCopy();
            currentSkin.BindValueChanged(_ =>
            {
                bool userSkinIsLegacy = skins.CurrentSkin.Value is LegacySkin;
                bool beatmapProvidingResources = skin is LegacySkinTransformer legacySkin && legacySkin.IsProvidingLegacyResources;

                // Some beatmaps provide a limited selection of skin elements to add some visual flair.
                // In stable, these elements will take lookup priority over the selected skin (whether that be a user skin or default).
                //
                // To replicate this we need to pay special attention to the fallback order.
                // If a user has a non-legacy skin (argon, triangles) selected, the game won't normally fall back to a legacy skin.
                // In turn this can create an unexpected visual experience.
                //
                // So here, check what skin the user has selected. If it's already a legacy skin then we don't need to do anything special.
                // If it isn't, we insert the OMS compatibility fallback. Note that this is only done if the beatmap seems to be providing skin elements,
                // as we only want to override the user's (non-legacy) skin choice when required for beatmap skin visuals.
                if (!userSkinIsLegacy && beatmapProvidingResources && compatibilityFallback != null)
                    SetSources(new[] { skin, compatibilityFallback });
                else
                    SetSources(new[] { skin });
            }, true);

            // The gameplay child is deliberately not attached by the parent container's object initializer. This
            // background-loader boundary first establishes the exact beatmap/ruleset source chain, then prepares and
            // commits the package/layout pair, and only afterwards attaches the renderer subtree. This ordering also
            // handles gameplay renderers which were constructed before the provider itself was loaded.
            if (prepareGameplayLayout != null)
            {
                if (childDependencies == null)
                    throw new InvalidOperationException("Gameplay layout preparation requires the exact child dependency scope.");

                const int maximum_fresh_barrier_attempts = 8;
                bool prepared = false;

                for (int attempt = 0; attempt < maximum_fresh_barrier_attempts; attempt++)
                {
                    switch (prepareGameplayLayout(childDependencies))
                    {
                        case GameplaySkinLayoutPreparationResult.Prepared:
                            prepared = true;
                            break;

                        case GameplaySkinLayoutPreparationResult.Rejected:
                            return;

                        case GameplaySkinLayoutPreparationResult.Retry:
                            continue;

                        default:
                            throw new InvalidOperationException("Unknown gameplay layout preparation result.");
                    }

                    break;
                }

                if (!prepared)
                    return;
            }

            if (delayedChild != null)
                Child = delayedChild;
        }
    }
}
