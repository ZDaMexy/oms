// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning
{
    /// <summary>
    /// Applies mania-owned HUD geometry from the exact gameplay layout publication.
    /// </summary>
    /// <remarks>
    /// The supplied defaults action remains responsible for component selection, resources and effects. Geometry
    /// owned by the gameplay layout must be projected through methods on this container instead of being read again
    /// from the skin transformer.
    /// </remarks>
    internal partial class ManiaGameplayHudComponentsContainer : DefaultSkinComponentsContainer
    {
        private readonly Action<ManiaGameplayHudComponentsContainer> applyDefaults;
        private readonly StageDefinition[] compatibilityStages;
        private readonly ISkin compatibilitySkin;

        public GameplaySkinLayoutSnapshot LayoutSnapshot { get; private set; } = null!;

        public ManiaGameplayHudComponentsContainer(
            IEnumerable<StageDefinition> stageDefinitions,
            ISkin compatibilitySkin,
            Action<ManiaGameplayHudComponentsContainer> applyDefaults)
            : base(static container => ((ManiaGameplayHudComponentsContainer)container).applyLayoutDefaults())
        {
            ArgumentNullException.ThrowIfNull(stageDefinitions);
            this.compatibilitySkin = compatibilitySkin ?? throw new ArgumentNullException(nameof(compatibilitySkin));
            this.applyDefaults = applyDefaults ?? throw new ArgumentNullException(nameof(applyDefaults));
            compatibilityStages = stageDefinitions.Select(stage => new StageDefinition(stage.Columns)).ToArray();

            if (compatibilityStages.Length is < 1 or > 2)
                throw new ArgumentException("A mania HUD requires one or two native stages.", nameof(stageDefinitions));
        }

        [BackgroundDependencyLoader(true)]
        private void load(GameplaySkinLayoutRevisionOwner? owner)
        {
            GameplaySkinLayoutPublication? publication = owner?.CurrentPublication;

            if (publication != null)
            {
                ManiaGameplaySkinLayout adapter = publication.GetAdapter<ManiaGameplaySkinLayout>();

                if (!ReferenceEquals(adapter.Snapshot, publication.Snapshot)
                    || !ReferenceEquals(publication.Snapshot.Context.PackageRevision, owner!.PackageRevision)
                    || publication.Snapshot.Context.RulesetId != "mania")
                {
                    throw new InvalidOperationException("The mania HUD did not retain the exact gameplay layout publication.");
                }

                LayoutSnapshot = publication.Snapshot;
                ManiaGameplaySkinLayout.ValidateConsumerCarrier(LayoutSnapshot, owner, "HUD");
                return;
            }

            if (owner == null || owner.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility)
            {
                throw new InvalidOperationException(
                    "A standalone mania HUD requires an explicitly cached compatibility layout owner.");
            }

            // Explicit isolation-only path for component test hosts which do not mount a managed gameplay provider.
            LayoutSnapshot = ManiaGameplaySkinLayout.CreateCompatibility(compatibilityStages, compatibilitySkin).Snapshot;
            ManiaGameplaySkinLayout.ValidateConsumerCarrier(LayoutSnapshot, owner, "HUD");
        }

        public void ApplyComboPlacement(Drawable combo)
        {
            ArgumentNullException.ThrowIfNull(combo);
            GameplaySkinLayoutRect comboRect = LayoutSnapshot.GetSurface(ManiaGameplaySkinLayout.COMBO_SURFACE).Rect;

            combo.Anchor = Anchor.TopLeft;
            combo.Origin = Anchor.Centre;
            combo.RelativePositionAxes = Axes.Both;
            combo.Position = new Vector2(comboRect.Left + comboRect.Width / 2, comboRect.Top + comboRect.Height / 2);
        }

        private void applyLayoutDefaults() => applyDefaults(this);
    }
}
