// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Skinning.Gameplay;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// Visual-only drawable for a <see cref="BmsMine"/>. Mirrors the bar-line pattern: it scrolls in its lane,
    /// never accepts player input, and resolves through an ignore-judgement so it stays out of the scoring /
    /// combo / statistics path. The C5 host may replace the visual through the prepared
    /// scene/publication; the compatibility path remains a simple circle when no exact
    /// gameplay-skin publication is attached.
    /// </summary>
    public partial class DrawableBmsMine : DrawableHitObject<BmsMine>, IGameplaySkinSpecialisedSceneConsumer
    {
        private readonly Drawable programmaticVisual;
        private readonly Container sceneVisualContainer;
        private readonly BmsGameplayLayoutSnapshot? layoutSnapshot;
        private readonly GameplaySkinResolvedMaterialSet? materialSet;
        private readonly BmsGameplayLayoutLane? layoutLane;
        private GameplaySkinSpecialisedSceneVisual? sceneVisual;
        private IDisposable? programmaticVisualRegistration;

        [Resolved(CanBeNull = true)]
        private DrawableBmsRuleset? drawableRuleset { get; set; }
        private GameplaySkinResolvedMaterialKey? resolvedMaterialKey;
        private GameplaySkinSceneHostedSlot? sceneVisualGate;
        private IReadOnlyList<string> appliedSceneNodeIds = Array.Empty<string>();

        public override bool DisplayResult => false;

        protected override double InitialLifetimeOffset => 2000;

        public GameplaySkinResolvedMaterialSet ResolvedMaterialSet
            => materialSet ?? throw new InvalidOperationException("A compatibility BMS mine has no exact C4 material publication.");

        public GameplaySkinResolvedMaterialKey ResolvedMaterialKey
            => resolvedMaterialKey ?? throw new InvalidOperationException("A compatibility BMS mine has no specialised C5 material key.");

        public GameplaySkinSceneHostedSlot SceneVisualGate
            => sceneVisualGate ?? throw new InvalidOperationException("A compatibility BMS mine has no specialised C5 visual gate.");

        public IReadOnlyList<string> AppliedSceneNodeIds => appliedSceneNodeIds;

        public DrawableBmsMine()
            : this(new BmsMine())
        {
        }

        public DrawableBmsMine(
            BmsMine hitObject,
            BmsGameplayLayoutSnapshot? layoutSnapshot = null,
            GameplaySkinResolvedMaterialSet? materialSet = null,
            BmsGameplayLayoutLane? layoutLane = null)
            : base(hitObject)
        {
            if (materialSet != null
                && (layoutSnapshot == null || layoutLane == null || !ReferenceEquals(materialSet.Snapshot, layoutSnapshot.Neutral)))
            {
                throw new ArgumentException("A BMS mine material set must retain its exact gameplay layout and lane.", nameof(materialSet));
            }

            this.layoutSnapshot = layoutSnapshot;
            this.materialSet = materialSet;
            this.layoutLane = layoutLane;
            HandleUserInput = false;

            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            RelativeSizeAxes = Axes.X;
            Width = 1;
            Height = 18;

            programmaticVisual = new Circle
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0.55f, 0.55f, 0.6f, 1f),
            };
            sceneVisualContainer = new Container { RelativeSizeAxes = Axes.Both };
            AddRangeInternal(new[]
            {
                programmaticVisual,
                sceneVisualContainer,
            });
        }

        [BackgroundDependencyLoader(true)]
        private void loadGameplaySkinScene(GameplaySkinSceneRuntimeHost? runtime)
        {
            if (runtime == null || layoutSnapshot == null || materialSet == null || layoutLane == null)
                return;

            GameplaySkinLaneTopologyEntry lane = layoutLane.NeutralLane.TopologyEntry;
            GameplaySkinLaneTopologyGroup group = layoutSnapshot.Neutral.Context.Topology.GroupsInLogicalOrder.Single(candidate =>
                candidate.Identity.Id.Equals(lane.Identity.Group.Id));
            var key = new GameplaySkinResolvedMaterialKey(
                GameplaySkinSlotCatalog.Mine,
                GameplaySkinResolvedMaterialTarget.ForLane(group, lane));

            if (!runtime.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate) || gate == null)
                throw new InvalidOperationException("The exact BMS mine scene gate is missing from the committed publication.");

            resolvedMaterialKey = key;
            sceneVisualGate = gate;

            if (gate.Route == GameplaySkinSceneHostRoute.Specialised)
            {
                sceneVisual = runtime.PrepareSpecialisedVisual(key, sceneVisualContainer);

                if (sceneVisual != null)
                    appliedSceneNodeIds = Array.AsReadOnly(sceneVisual.RuntimeNodes.Select(node => node.PreparedNode.InstanceId).ToArray());
            }

            if (gate.Route == GameplaySkinSceneHostRoute.Suppressed || sceneVisual != null)
                programmaticVisualRegistration = runtime.RegisterProgrammaticVisual(key, programmaticVisual);
        }

        protected override void OnApply()
        {
            base.OnApply();

            if (sceneVisual != null)
            {
                if (drawableRuleset != null)
                    sceneVisual.OnApply(drawableRuleset.GetGameplaySkinObjectId(HitObject));
                else
                    sceneVisual.OnApply();
            }
        }

        protected override void OnFree()
        {
            sceneVisual?.OnFree();
            base.OnFree();
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (timeOffset >= 0)
                ApplyMaxResult();
        }

        protected override void UpdateHitStateTransforms(ArmedState state)
        {
            base.UpdateHitStateTransforms(state);

            if (state == ArmedState.Hit || state == ArmedState.Miss)
                this.FadeOut(150).Expire();
        }

        protected override void Dispose(bool isDisposing)
        {
            programmaticVisualRegistration?.Dispose();
            programmaticVisualRegistration = null;
            base.Dispose(isDisposing);
        }
    }
}
