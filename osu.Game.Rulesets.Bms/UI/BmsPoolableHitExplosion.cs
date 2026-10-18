// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Judgements;
using osu.Game.Skinning.Gameplay;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// One bounded, reusable BMS judgement effect attached to the engine-owned hit-target geometry.
    /// </summary>
    public partial class BmsPoolableHitExplosion : PoolableDrawable, IGameplaySkinSpecialisedSceneConsumer
    {
        public const double DURATION = 200;

        private readonly BmsGameplayLayoutSnapshot? layoutSnapshot;
        private readonly GameplaySkinResolvedMaterialSet? materialSet;
        private readonly BmsGameplayLayoutLane? layoutLane;
        private readonly Container programmaticVisual;
        private readonly Circle programmaticPulse;
        private readonly Container specialisedSceneOwner;
        private GameplaySkinResolvedMaterialKey? resolvedMaterialKey;
        private GameplaySkinSceneHostedSlot? sceneVisualGate;
        private IDisposable? programmaticVisualRegistration;
        private JudgementResult? result;

        public GameplaySkinResolvedMaterialSet ResolvedMaterialSet
            => materialSet ?? throw new InvalidOperationException("A compatibility BMS hit explosion has no exact C4 material publication.");

        public GameplaySkinResolvedMaterialKey ResolvedMaterialKey
            => resolvedMaterialKey ?? throw new InvalidOperationException("A compatibility BMS hit explosion has no specialised C5 material key.");

        public GameplaySkinSceneHostedSlot SceneVisualGate
            => sceneVisualGate ?? throw new InvalidOperationException("A compatibility BMS hit explosion has no specialised C5 visual gate.");

        public IReadOnlyList<string> AppliedSceneNodeIds { get; private set; } = Array.Empty<string>();

        public long? BoundObjectId { get; private set; }

        internal GameplaySkinSpecialisedSceneVisual? SpecialisedSceneVisual { get; private set; }

        internal Drawable ProgrammaticVisual => programmaticVisual;

        public BmsPoolableHitExplosion()
            : this(null, null, null)
        {
        }

        internal BmsPoolableHitExplosion(
            BmsGameplayLayoutSnapshot? layoutSnapshot,
            GameplaySkinResolvedMaterialSet? materialSet,
            BmsGameplayLayoutLane? layoutLane)
        {
            if (materialSet != null
                && (layoutSnapshot == null || layoutLane == null || !ReferenceEquals(materialSet.Snapshot, layoutSnapshot.Neutral)))
            {
                throw new ArgumentException("A BMS hit explosion must retain its exact layout/material/lane publication.", nameof(materialSet));
            }

            this.layoutSnapshot = layoutSnapshot;
            this.materialSet = materialSet;
            this.layoutLane = layoutLane;
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                programmaticVisual = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = programmaticPulse = new Circle
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Size = new Vector2(0.7f),
                        Alpha = 0,
                        Colour = layoutLane?.IsScratch == true
                            ? new Color4(255, 186, 104, 210)
                            : new Color4(120, 196, 255, 210),
                        Blending = BlendingParameters.Additive,
                    },
                },
                specialisedSceneOwner = new Container { RelativeSizeAxes = Axes.Both },
            };
        }

        [BackgroundDependencyLoader(true)]
        private void loadGameplaySkinScene(GameplaySkinSceneRuntimeHost? sceneRuntime)
        {
            if (sceneRuntime == null || layoutSnapshot == null || materialSet == null || layoutLane == null)
                return;

            GameplaySkinLaneTopologyEntry lane = layoutLane.NeutralLane.TopologyEntry;
            GameplaySkinLaneTopologyGroup group = layoutSnapshot.Neutral.Context.Topology.GroupsInLogicalOrder.Single(candidate =>
                candidate.Identity.Id.Equals(lane.Identity.Group.Id));
            resolvedMaterialKey = new GameplaySkinResolvedMaterialKey(
                GameplaySkinSlotCatalog.HitExplosion,
                GameplaySkinResolvedMaterialTarget.ForLane(group, lane));

            if (!sceneRuntime.TryGetVisualGate(resolvedMaterialKey, out sceneVisualGate) || sceneVisualGate == null)
                throw new InvalidOperationException("The exact BMS hit-explosion scene gate is missing from its committed publication.");

            if (sceneVisualGate.SpecialisedPoolCapacity != GameplaySkinPreparedSceneBudgets.MAX_HIT_EXPLOSION_VISUALS_PER_KEY
                && sceneVisualGate.Route == GameplaySkinSceneHostRoute.Specialised)
            {
                throw new InvalidOperationException("The BMS hit-explosion pool must match its immutable prepared capacity.");
            }

            if (sceneVisualGate.Route == GameplaySkinSceneHostRoute.Specialised)
            {
                SpecialisedSceneVisual = sceneRuntime.PrepareSpecialisedVisual(resolvedMaterialKey, specialisedSceneOwner);

                if (SpecialisedSceneVisual != null)
                {
                    AppliedSceneNodeIds = Array.AsReadOnly(
                        SpecialisedSceneVisual.RuntimeNodes.Select(node => node.PreparedNode.InstanceId).ToArray());
                }
            }

            if (sceneVisualGate.Route == GameplaySkinSceneHostRoute.Suppressed || SpecialisedSceneVisual != null)
                programmaticVisualRegistration = sceneRuntime.RegisterProgrammaticVisual(resolvedMaterialKey, programmaticVisual);
        }

        internal void Apply(JudgementResult judgementResult, long objectId)
        {
            result = judgementResult ?? throw new ArgumentNullException(nameof(judgementResult));
            ArgumentOutOfRangeException.ThrowIfNegative(objectId);
            BoundObjectId = objectId;
            SpecialisedSceneVisual?.OnApply(objectId);
        }

        protected override void PrepareForUse()
        {
            base.PrepareForUse();
            Alpha = 1;

            if (result == null || BoundObjectId == null)
                throw new InvalidOperationException("A pooled BMS hit explosion must receive one exact judgement identity before use.");

            LifetimeStart = Time.Current;

            programmaticPulse.ClearTransforms();
            programmaticPulse.Alpha = 1;
            programmaticPulse.Scale = new Vector2(0.35f);
            programmaticPulse.ScaleTo(1.6f, DURATION, Easing.OutQuint);
            programmaticPulse.FadeOut(DURATION, Easing.Out);
            this.Delay(SpecialisedSceneVisual?.RequestedJudgementDuration ?? DURATION).Then().Expire();
        }

        protected override void FreeAfterUse()
        {
            SpecialisedSceneVisual?.OnFree();
            Alpha = 0;
            BoundObjectId = null;
            programmaticPulse.ClearTransforms();
            programmaticPulse.Alpha = 0;
            result = null;
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
