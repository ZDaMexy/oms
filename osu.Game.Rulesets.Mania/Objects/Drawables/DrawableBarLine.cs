// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.Mania.Skinning.Default;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.Objects.Drawables
{
    /// <summary>
    /// Visualises a <see cref="BarLine"/>. Although this derives DrawableManiaHitObject,
    /// this does not handle input/sound like a normal hit object.
    /// </summary>
    public partial class DrawableBarLine : DrawableManiaHitObject<BarLine>, IGameplaySkinSpecialisedSceneConsumer
    {
        public readonly Bindable<bool> Major = new Bindable<bool>();

        private SkinnableDrawable programmaticVisual = null!;
        private Container specialisedSceneOwner = null!;
        private GameplaySkinSpecialisedSceneVisual? specialisedSceneVisual;
        private IDisposable? programmaticVisualRegistration;

        [Resolved]
        private ManiaGameplaySkinStageContext layoutStageContext { get; set; } = null!;

        public GameplaySkinLayoutSnapshot StageLayoutSnapshot => layoutStageContext.Snapshot;

        public GameplaySkinResolvedMaterialSet ResolvedMaterialSet { get; private set; } = null!;

        public GameplaySkinResolvedMaterialKey ResolvedMaterialKey { get; private set; } = null!;

        public GameplaySkinSceneHostedSlot SceneVisualGate { get; private set; } = null!;

        public IReadOnlyList<string> AppliedSceneNodeIds { get; private set; } = Array.Empty<string>();

        [Resolved(CanBeNull = true)]
        private GameplaySkinSceneRuntimeHost? sceneRuntime { get; set; }

        [Resolved(CanBeNull = true)]
        private GameplaySkinResolvedMaterialSet? materialSet { get; set; }

        [Resolved(CanBeNull = true)]
        private IManiaGameplaySkinObjectIdentityProvider? gameplaySkinObjectIdentityProvider { get; set; }

        public DrawableBarLine()
            : this(null!)
        {
        }

        public DrawableBarLine(BarLine barLine)
            : base(barLine)
        {
            RelativeSizeAxes = Axes.X;
            Height = 1;
        }

        [BackgroundDependencyLoader(true)]
        private void load()
        {
            AddRangeInternal(new Drawable[]
            {
                programmaticVisual = new SkinnableDrawable(new ManiaSkinComponentLookup(ManiaSkinComponents.BarLine), _ => new DefaultBarLine())
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                },
                specialisedSceneOwner = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                },
            });

            if (sceneRuntime == null || materialSet == null
                                     || materialSet.ContractIdentity.Equals(GameplaySkinMaterialContractIdentity.CompatibilityEmpty))
                return;

            if (!ReferenceEquals(materialSet.Snapshot, layoutStageContext.Snapshot))
                throw new InvalidOperationException("A mania bar-line cannot mix scene, layout and material revisions.");

            ResolvedMaterialSet = materialSet;
            ResolvedMaterialKey = new GameplaySkinResolvedMaterialKey(
                GameplaySkinSlotCatalog.BarLine,
                GameplaySkinResolvedMaterialTarget.ForGroup(layoutStageContext.Group.TopologyGroup));

            if (!sceneRuntime.TryGetVisualGate(ResolvedMaterialKey, out GameplaySkinSceneHostedSlot? gate) || gate == null)
                throw new InvalidOperationException("The exact mania bar-line scene gate is missing from its committed publication.");

            SceneVisualGate = gate;

            if (gate.Route == GameplaySkinSceneHostRoute.Specialised)
                specialisedSceneVisual = sceneRuntime.PrepareSpecialisedVisual(ResolvedMaterialKey, specialisedSceneOwner);

            if (gate.Route == GameplaySkinSceneHostRoute.Suppressed || specialisedSceneVisual != null)
                programmaticVisualRegistration = sceneRuntime.RegisterProgrammaticVisual(ResolvedMaterialKey, programmaticVisual);

            if (specialisedSceneVisual != null)
            {
                AppliedSceneNodeIds = Array.AsReadOnly(
                    gate.RoutedNodes.Select(node => node.InstanceId).ToArray());
            }
        }

        protected override void OnApply()
        {
            base.OnApply();
            Major.BindTo(HitObject.MajorBindable);

            if (specialisedSceneVisual != null)
            {
                if (gameplaySkinObjectIdentityProvider == null || ResolvedMaterialKey.Target.GroupId == null)
                    throw new InvalidOperationException("A specialised mania bar-line scene requires the engine-owned stage usage identity.");

                specialisedSceneVisual.OnApply(gameplaySkinObjectIdentityProvider.GetObjectId(
                    HitObject,
                    ResolvedMaterialKey.Target.GroupId));
            }
        }

        protected override void OnFree()
        {
            specialisedSceneVisual?.OnFree();
            base.OnFree();
            Major.UnbindFrom(HitObject.MajorBindable);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                programmaticVisualRegistration?.Dispose();

            base.Dispose(isDisposing);
        }

        protected override void UpdateStartTimeStateTransforms() => this.FadeOut(150);
    }
}
