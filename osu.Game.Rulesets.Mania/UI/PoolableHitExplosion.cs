// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Game.Rulesets.Judgements;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.UI
{
    public partial class PoolableHitExplosion : PoolableDrawable, IGameplaySkinSpecialisedSceneConsumer
    {
        public const double DURATION = 200;

        public JudgementResult? Result { get; private set; }

        private readonly GameplaySkinSceneRuntimeHost? sceneRuntime;
        private readonly GameplaySkinResolvedMaterialKey? resolvedMaterialKey;
        private SkinnableDrawable skinnableExplosion = null!;
        private Container specialisedSceneOwner = null!;
        private GameplaySkinSpecialisedSceneVisual? specialisedSceneVisual;
        private GameplaySkinSceneHostedSlot? sceneVisualGate;
        private IDisposable? programmaticVisualRegistration;
        private IReadOnlyList<string> appliedSceneNodeIds = Array.Empty<string>();
        private long? boundObjectId;

        public GameplaySkinResolvedMaterialSet ResolvedMaterialSet
            => sceneRuntime?.MaterialSet
               ?? throw new InvalidOperationException("A compatibility mania hit explosion has no exact C4 material publication.");

        public GameplaySkinResolvedMaterialKey ResolvedMaterialKey
            => resolvedMaterialKey
               ?? throw new InvalidOperationException("A compatibility mania hit explosion has no specialised C5 material key.");

        public GameplaySkinSceneHostedSlot SceneVisualGate
            => sceneVisualGate
               ?? throw new InvalidOperationException("A compatibility mania hit explosion has no specialised C5 visual gate.");

        public IReadOnlyList<string> AppliedSceneNodeIds => appliedSceneNodeIds;

        public long? BoundObjectId => boundObjectId;

        internal GameplaySkinSpecialisedSceneVisual? SpecialisedSceneVisual => specialisedSceneVisual;

        internal Drawable ProgrammaticVisual => skinnableExplosion;

        public PoolableHitExplosion()
            : this(null, null)
        {
        }

        internal PoolableHitExplosion(
            GameplaySkinSceneRuntimeHost? sceneRuntime,
            GameplaySkinResolvedMaterialKey? resolvedMaterialKey)
        {
            if ((sceneRuntime == null) != (resolvedMaterialKey == null))
                throw new ArgumentException("An exact mania hit explosion requires both its scene runtime and material key.");

            this.sceneRuntime = sceneRuntime;
            this.resolvedMaterialKey = resolvedMaterialKey;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddRangeInternal(new Drawable[]
            {
                skinnableExplosion = new SkinnableDrawable(new ManiaSkinComponentLookup(ManiaSkinComponents.HitExplosion), _ => new DefaultHitExplosion())
                {
                    RelativeSizeAxes = Axes.Both,
                },
                specialisedSceneOwner = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                },
            });

            if (sceneRuntime == null || resolvedMaterialKey == null)
                return;

            if (!sceneRuntime.TryGetVisualGate(resolvedMaterialKey, out sceneVisualGate) || sceneVisualGate == null)
                throw new InvalidOperationException("The exact mania hit-explosion scene gate is missing from its committed publication.");

            if (sceneVisualGate.Route == GameplaySkinSceneHostRoute.Specialised)
            {
                specialisedSceneVisual = sceneRuntime.PrepareSpecialisedVisual(resolvedMaterialKey, specialisedSceneOwner);

                if (specialisedSceneVisual != null)
                {
                    appliedSceneNodeIds = Array.AsReadOnly(
                        specialisedSceneVisual.RuntimeNodes.Select(node => node.PreparedNode.InstanceId).ToArray());
                }
            }

            if (sceneVisualGate.Route == GameplaySkinSceneHostRoute.Suppressed || specialisedSceneVisual != null)
                programmaticVisualRegistration = sceneRuntime.RegisterProgrammaticVisual(resolvedMaterialKey, skinnableExplosion);
        }

        public void Apply(JudgementResult result, long objectId)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            ArgumentOutOfRangeException.ThrowIfNegative(objectId);
            boundObjectId = objectId;
            specialisedSceneVisual?.OnApply(objectId);
        }

        /// <summary>
        /// Compatibility-only entry for detached legacy previews which have no production event identity owner.
        /// </summary>
        public void Apply(JudgementResult result) => Apply(result, 0);

        protected override void PrepareForUse()
        {
            base.PrepareForUse();
            Alpha = 1;

            // Exact production pools must bind every lease to the judged gameplay object before any author-scene
            // state can observe it. Compatibility previews and legacy standalone hosts deliberately have no event
            // runtime or object identity authority and retain the historical load-only/programmatic fallback path.
            if (sceneRuntime != null && (Result == null || boundObjectId == null))
                throw new InvalidOperationException("A pooled mania hit explosion must receive one exact judgement identity before use.");

            LifetimeStart = Time.Current;

            if (Result != null)
                (skinnableExplosion?.Drawable as IHitExplosion)?.Animate(Result);

            this.Delay(DURATION).Then().Expire();
        }

        protected override void FreeAfterUse()
        {
            specialisedSceneVisual?.OnFree();
            Alpha = 0;
            boundObjectId = null;
            Result = null;
            base.FreeAfterUse();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                programmaticVisualRegistration?.Dispose();
                programmaticVisualRegistration = null;
            }

            base.Dispose(isDisposing);
        }
    }
}
