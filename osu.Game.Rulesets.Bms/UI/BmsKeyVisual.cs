// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// The sole public key visual consumer for a lane. A separate controller lives outside the scrolling mask;
    /// legacy receptors remain attached to their hit target and follow its live Lift projection.
    /// </summary>
    public partial class BmsKeyVisual : Container, IGameplaySkinSpecialisedSceneConsumer
    {
        private readonly BmsGameplayLayoutSnapshot layout;
        private readonly BmsGameplayLayoutLane lane;
        private readonly BindableBool isPressed = new BindableBool();
        private GameplaySkinSceneRuntimeHost? sceneRuntime;
        private GameplaySkinSpecialisedSceneVisual? sceneVisual;
        private Sprite? textureVisual;
        private GameplaySkinResolvedMaterialKey? materialKey;
        private GameplaySkinSceneHostedSlot? visualGate;

        public GameplaySkinResolvedMaterialSet ResolvedMaterialSet
            => sceneRuntime?.MaterialSet
               ?? throw new InvalidOperationException("A compatibility BMS key visual has no exact material publication.");

        public GameplaySkinResolvedMaterialKey ResolvedMaterialKey
            => materialKey
               ?? throw new InvalidOperationException("A compatibility BMS key visual has no specialised KeyVisual key.");

        public GameplaySkinSceneHostedSlot SceneVisualGate
            => visualGate
               ?? throw new InvalidOperationException("A compatibility BMS key visual has no specialised KeyVisual gate.");

        public IReadOnlyList<string> AppliedSceneNodeIds { get; private set; } = Array.Empty<string>();

        internal bool UsesSeparateKeyArea { get; }

        internal BmsKeyVisual(BmsGameplayLayoutSnapshot layout, BmsGameplayLayoutLane lane, BindableBool pressed)
        {
            this.layout = layout;
            this.lane = lane;
            UsesSeparateKeyArea = !layout.KeyAreaRect.Equals(layout.HitTargetRect);
            RelativePositionAxes = Axes.Both;
            RelativeSizeAxes = Axes.Both;
            Position = UsesSeparateKeyArea ? new Vector2(lane.KeyVisualRect.X, lane.KeyVisualRect.Y) : Vector2.Zero;
            Size = UsesSeparateKeyArea ? new Vector2(lane.KeyVisualRect.Width, lane.KeyVisualRect.Height) : Vector2.One;
            isPressed.BindTo(pressed);
            isPressed.BindValueChanged(_ => updateState());
        }

        [BackgroundDependencyLoader(true)]
        private void load(GameplaySkinSceneRuntimeHost? runtime)
        {
            if (runtime == null)
                return;

            GameplaySkinLaneTopologyEntry entry = lane.NeutralLane.TopologyEntry;
            GameplaySkinLaneTopologyGroup group = layout.Neutral.Context.Topology.GroupsInLogicalOrder.Single(candidate =>
                candidate.Identity.Id.Equals(entry.Identity.Group.Id));
            var key = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.KeyVisual,
                GameplaySkinResolvedMaterialTarget.ForLane(group, entry));

            if (!runtime.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate) || gate == null)
                throw new InvalidOperationException("The exact BMS KeyVisual scene gate is missing from the committed publication.");

            sceneRuntime = runtime;
            materialKey = key;
            visualGate = gate;

            if (gate.Route != GameplaySkinSceneHostRoute.Specialised)
                return;

            sceneVisual = runtime.PrepareSpecialisedVisual(key, this);

            if (sceneVisual == null)
                return;

            AppliedSceneNodeIds = Array.AsReadOnly(sceneVisual.RuntimeNodes.Select(node => node.PreparedNode.InstanceId).ToArray());
            if (sceneVisual.RuntimeNodes.Count == 0)
            {
                textureVisual = (Sprite)sceneVisual.RootDrawables.Single();
                if (UsesSeparateKeyArea && lane.IsScratch)
                {
                    textureVisual.FillMode = FillMode.Fit;
                    textureVisual.Anchor = textureVisual.Origin = Anchor.Centre;
                }
            }
            sceneVisual.OnApply();
            updateState();
        }

        private void updateState()
        {
            // Authored scene nodes retain their public input bindings; plain key images also retain feedback.
            if (textureVisual != null)
                textureVisual.Alpha = isPressed.Value ? 1 : 0.65f;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                sceneVisual?.OnFree();
                isPressed.UnbindAll();
            }

            base.Dispose(isDisposing);
        }
    }
}
