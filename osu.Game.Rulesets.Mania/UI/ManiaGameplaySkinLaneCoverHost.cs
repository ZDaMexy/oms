// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.UI
{
    /// <summary>
    /// Stage-local C5 author visual mounted into the native mania playfield-cover geometry.
    /// </summary>
    /// <remarks>
    /// The wrapped <see cref="PlayfieldCoveringWrapper"/> remains the sole owner of coverage, scroll-direction and
    /// clipping geometry. Its alpha-subtraction pass continues to own note visibility; the author scene is a distinct
    /// visible layer and therefore cannot expose notes, change judgement/input, or acquire gameplay authority.
    /// </remarks>
    internal sealed partial class ManiaGameplaySkinLaneCoverHost : CompositeDrawable
    {
        private readonly List<GameplaySkinSpecialisedSceneVisual> sceneVisuals = new List<GameplaySkinSpecialisedSceneVisual>();

        internal PlayfieldCoveringWrapper Cover { get; }

        internal IReadOnlyList<GameplaySkinSpecialisedSceneVisual> SceneVisuals => sceneVisuals;

        public ManiaGameplaySkinLaneCoverHost(PlayfieldCoveringWrapper cover)
        {
            Cover = cover ?? throw new ArgumentNullException(nameof(cover));
            RelativeSizeAxes = Axes.Both;
            InternalChild = cover;
        }

        [BackgroundDependencyLoader(true)]
        private void load(GameplaySkinSceneRuntimeHost? sceneRuntime, ManiaGameplaySkinStageContext stageContext)
        {
            if (sceneRuntime == null
                || sceneRuntime.MaterialSet.ContractIdentity.Equals(GameplaySkinMaterialContractIdentity.CompatibilityEmpty))
                return;

            if (!ReferenceEquals(sceneRuntime.Publication.Snapshot, stageContext.Snapshot)
                || !ReferenceEquals(sceneRuntime.MaterialSet.Snapshot, stageContext.Snapshot))
            {
                throw new InvalidOperationException("A mania lane-cover host cannot mix scene, material and stage layout revisions.");
            }

            GameplaySkinResolvedMaterialTarget target = GameplaySkinResolvedMaterialTarget.ForStage(stageContext.Group.TopologyGroup);
            prepare(GameplaySkinSlotCatalog.LaneCoverFill, Cover.GameplaySkinFillSceneOwner);
            prepare(GameplaySkinSlotCatalog.LaneCoverDecoration, Cover.GameplaySkinDecorationSceneOwner);

            void prepare(GameplaySkinSlotDescriptor slot, Container owner)
            {
                var key = new GameplaySkinResolvedMaterialKey(slot, target);

                if (!sceneRuntime.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate) || gate == null)
                    throw new InvalidOperationException("The exact mania lane-cover scene gate is missing from its committed publication.");

                // Suppress only means that this optional authored visual remains absent. The engine-owned cover mask
                // is intentionally never registered as a programmatic slot visual and cannot be disabled by a skin.
                if (gate.Route == GameplaySkinSceneHostRoute.Suppressed)
                    return;

                if (gate.Route != GameplaySkinSceneHostRoute.Specialised)
                    throw new InvalidOperationException("A mania lane-cover visual must use its prepared native-geometry route.");

                GameplaySkinSpecialisedSceneVisual? visual = sceneRuntime.PrepareSpecialisedVisual(key, owner);

                if (visual == null)
                    return;

                visual.OnApply();
                sceneVisuals.Add(visual);
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                foreach (GameplaySkinSpecialisedSceneVisual visual in sceneVisuals)
                    visual.Dispose();

                sceneVisuals.Clear();
            }

            base.Dispose(isDisposing);
        }
    }
}
