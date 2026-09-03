// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
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
    internal partial class ManiaGameplayHudComponentsContainer : DefaultSkinComponentsContainer, IGameplaySkinIndependentlyRegisteredHudOwnerSource
    {
        private readonly Action<ManiaGameplayHudComponentsContainer> applyDefaults;
        private readonly StageDefinition[] compatibilityStages;
        private readonly ISkin compatibilitySkin;
        private readonly List<StageLocalComboOwner> stageLocalComboOwners = new List<StageLocalComboOwner>();
        private readonly List<IDisposable> sceneRegistrations = new List<IDisposable>();
        private GameplaySkinSceneRuntimeHost? sceneRuntime;

        public GameplaySkinLayoutSnapshot LayoutSnapshot { get; private set; } = null!;

        public GameplaySkinResolvedMaterialSet ResolvedMaterialSet { get; private set; } = null!;

        IReadOnlyList<Drawable> IGameplaySkinIndependentlyRegisteredHudOwnerSource.GameplaySkinIndependentlyRegisteredHudOwners
            => stageLocalComboOwners.Select(owner => owner.Drawable).ToArray();

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
        private void load(GameplaySkinLayoutRevisionOwner? owner, DrawableRuleset? drawableRuleset)
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
                ResolvedMaterialSet = publication.MaterialSet;
                ManiaGameplaySkinLayout.ValidateConsumerCarrier(LayoutSnapshot, owner, "HUD");

                if (drawableRuleset is DrawableManiaRuleset maniaRuleset)
                {
                    if (!ReferenceEquals(maniaRuleset.LayoutRevisionOwner.CurrentPublication, publication))
                        throw new InvalidOperationException("The mania HUD cannot consume a scene runtime from another exact publication.");

                    sceneRuntime = maniaRuleset.GameplaySkinSceneRuntime;
                }

                return;
            }

            if (owner == null || owner.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility)
            {
                throw new InvalidOperationException(
                    "A standalone mania HUD requires an explicitly cached compatibility layout owner.");
            }

            // Explicit isolation-only path for component test hosts which do not mount a managed gameplay provider.
            LayoutSnapshot = ManiaGameplaySkinLayout.CreateCompatibility(compatibilityStages, compatibilitySkin).Snapshot;
            ResolvedMaterialSet = GameplaySkinResolvedMaterialSet.CreateEmpty(LayoutSnapshot);
            ManiaGameplaySkinLayout.ValidateConsumerCarrier(LayoutSnapshot, owner, "HUD");
        }

        public void ApplyComboPlacement(Drawable combo, int stageIndex = 0)
        {
            ArgumentNullException.ThrowIfNull(combo);

            if (stageIndex < 0 || stageIndex >= LayoutSnapshot.Context.Topology.GroupsInLogicalOrder.Count)
                throw new ArgumentOutOfRangeException(nameof(stageIndex));

            GameplaySkinLaneTopologyGroup group = LayoutSnapshot.Context.Topology.GroupsInLogicalOrder[stageIndex];
            GameplaySkinResolvedMaterialTarget target = GameplaySkinResolvedMaterialTarget.ForStage(group);
            var key = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.ComboDisplay, target);
            GameplaySkinLayoutRect comboRect;

            if (sceneRuntime != null)
            {
                if (!sceneRuntime.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate) || gate == null)
                    throw new InvalidOperationException("The exact mania combo surface was not frozen by scene preparation.");

                comboRect = gate.PreparedRect;
            }
            else
            {
                // Compatibility-only component hosts have no committed scene carrier. They still consume the exact
                // C3 combo band and group rectangle instead of invoking the C5 preparation resolver at runtime.
                GameplaySkinLayoutRect groupRect = LayoutSnapshot.GetGroup(group.Identity.Id).Rect;
                GameplaySkinLayoutRect comboBand = LayoutSnapshot.GetSurface(ManiaGameplaySkinLayout.COMBO_SURFACE).Rect;
                comboRect = GameplaySkinLayoutRect.Create(groupRect.Left, comboBand.Top, groupRect.Width, comboBand.Height);
            }

            combo.Anchor = Anchor.TopLeft;
            combo.Origin = Anchor.Centre;
            combo.RelativePositionAxes = Axes.Both;
            combo.Position = new Vector2(comboRect.Left + comboRect.Width / 2, comboRect.Top + comboRect.Height / 2);
            stageLocalComboOwners.Add(new StageLocalComboOwner(combo, target));
        }

        private void applyLayoutDefaults()
        {
            stageLocalComboOwners.Clear();
            applyDefaults(this);

            if (sceneRuntime == null)
                return;

            foreach (StageLocalComboOwner owner in stageLocalComboOwners)
            {
                sceneRegistrations.Add(sceneRuntime.RegisterProgrammaticVisual(
                    new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.ComboDisplay, owner.Target),
                    owner.Drawable));
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                foreach (IDisposable registration in sceneRegistrations)
                    registration.Dispose();

                sceneRegistrations.Clear();
            }

            base.Dispose(isDisposing);
        }

        private readonly record struct StageLocalComboOwner(
            Drawable Drawable,
            GameplaySkinResolvedMaterialTarget Target);
    }
}
