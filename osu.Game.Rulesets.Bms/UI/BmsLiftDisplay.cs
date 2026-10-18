// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// Optional stage-local presentation of the native hit-area controller's already-applied LIFT offset.
    /// The complete stage canvas is cropped rather than resized into the covered region. No input, padding,
    /// hit position, scroll algorithm or timing is owned here.
    /// </summary>
    internal sealed partial class BmsLiftDisplay : Container
    {
        private readonly BmsHitObjectArea hitArea;

        internal GameplaySkinResolvedMaterialTarget Target { get; }

        internal Container FillClip { get; }

        internal Container FillSceneOwner { get; }

        internal Container DecorationSceneOwner { get; }

        internal BmsLiftDisplayPart? FillPart { get; }

        internal BmsLiftDisplayPart? DecorationPart { get; }

        internal float AppliedOffsetRatio => hitArea.AppliedOffsetRatio;

        internal bool AppliedReverse => hitArea.AppliedReverse;

        public override bool PropagateNonPositionalInputSubTree => false;

        internal static BmsLiftDisplay? TryCreate(
            GameplaySkinLaneTopologyGroup group,
            BmsHitObjectArea hitArea,
            BmsGameplayLayoutSnapshot snapshot,
            GameplaySkinResolvedMaterialSet materials,
            GameplaySkinSceneRuntimeHost? runtime)
        {
            var target = GameplaySkinResolvedMaterialTarget.ForStage(group);
            GameplaySkinResolvedMaterialEntry? fill = selectedEntry(GameplaySkinSlotCatalog.LiftFill);
            GameplaySkinResolvedMaterialEntry? decoration = selectedEntry(GameplaySkinSlotCatalog.LiftDecoration);
            if (fill == null && decoration == null)
                return null;

            if (runtime == null || !ReferenceEquals(runtime.Publication.Snapshot, snapshot.Neutral)
                                || !ReferenceEquals(runtime.MaterialSet, materials)
                                || !ReferenceEquals(materials.Snapshot, snapshot.Neutral)
                                || !ReferenceEquals(hitArea.LayoutSnapshot, snapshot))
                throw new InvalidOperationException("An authored BMS LIFT visual requires its exact native layout/material publication.");

            return new BmsLiftDisplay(target, hitArea, snapshot, materials, runtime, fill, decoration);

            GameplaySkinResolvedMaterialEntry? selectedEntry(GameplaySkinSlotDescriptor slot)
                => materials.TryGet(new GameplaySkinResolvedMaterialKey(slot, target), out GameplaySkinResolvedMaterialEntry? entry)
                   && entry.State == GameplaySkinResolvedMaterialState.Provide && entry.Source.IsSelectedDocumentDeclaration ? entry : null;
        }

        private BmsLiftDisplay(
            GameplaySkinResolvedMaterialTarget target,
            BmsHitObjectArea hitArea,
            BmsGameplayLayoutSnapshot snapshot,
            GameplaySkinResolvedMaterialSet materials,
            GameplaySkinSceneRuntimeHost runtime,
            GameplaySkinResolvedMaterialEntry? fill,
            GameplaySkinResolvedMaterialEntry? decoration)
        {
            Target = target;
            this.hitArea = hitArea;
            GameplaySkinLayoutRect groupRect = snapshot.Neutral.GetGroup(target.GroupId!).Rect;
            GameplaySkinLayoutRect playfield = snapshot.PlayfieldRect;
            RelativePositionAxes = Axes.Both;
            RelativeSizeAxes = Axes.Both;
            Position = new Vector2((groupRect.X - playfield.X) / playfield.Width, (groupRect.Y - playfield.Y) / playfield.Height);
            Size = new Vector2(groupRect.Width / playfield.Width, groupRect.Height / playfield.Height);
            Masking = true;
            Alpha = 0;
            Children = new Drawable[]
            {
                FillClip = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Height = 0,
                    Masking = true,
                    Child = FillSceneOwner = new Container { RelativeSizeAxes = Axes.X },
                },
                DecorationSceneOwner = new Container
                {
                    RelativePositionAxes = Axes.Y,
                    RelativeSizeAxes = Axes.Both,
                },
            };
            if (fill != null)
                FillSceneOwner.Add(FillPart = new BmsLiftDisplayPart(materials, fill.Key, runtime));
            if (decoration != null)
                DecorationSceneOwner.Add(DecorationPart = new BmsLiftDisplayPart(materials, decoration.Key, runtime));
        }

        /// <summary>Called after all native lane hit-area controllers have updated for this frame.</summary>
        internal void RefreshProjection()
        {
            float ratio = hitArea.AppliedOffsetRatio;
            bool reverse = hitArea.AppliedReverse;
            Alpha = hitArea.HasAppliedProjection && hitArea.AppliedHeight > 0 && ratio > 0 ? 1 : 0;
            FillClip.Anchor = FillClip.Origin = reverse ? Anchor.TopLeft : Anchor.BottomLeft;
            FillClip.Height = ratio;
            FillSceneOwner.Anchor = FillSceneOwner.Origin = reverse ? Anchor.TopLeft : Anchor.BottomLeft;
            FillSceneOwner.Height = DrawHeight;
            // The decoration's local y=0 is the native LIFT boundary in either direction. Author nodes can keep
            // readable text upright and use the read-only direction source for distinct top/bottom placement.
            DecorationSceneOwner.Y = reverse ? ratio : 1 - ratio;
        }
    }

    /// <summary>One prepared visual per exact stage/slot. There is no aggregate or inherited fallback surface.</summary>
    internal sealed partial class BmsLiftDisplayPart : Container, IGameplaySkinSpecialisedSceneConsumer
    {
        public GameplaySkinResolvedMaterialSet ResolvedMaterialSet { get; }
        public GameplaySkinResolvedMaterialKey ResolvedMaterialKey { get; }
        public GameplaySkinSceneHostedSlot SceneVisualGate { get; }
        public IReadOnlyList<string> AppliedSceneNodeIds { get; }
        internal GameplaySkinSpecialisedSceneVisual? SpecialisedSceneVisual { get; }

        internal BmsLiftDisplayPart(GameplaySkinResolvedMaterialSet materials, GameplaySkinResolvedMaterialKey key, GameplaySkinSceneRuntimeHost runtime)
        {
            ResolvedMaterialSet = materials;
            ResolvedMaterialKey = key;
            RelativeSizeAxes = Axes.Both;
            if (!runtime.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate) || gate == null
                || gate.Route != GameplaySkinSceneHostRoute.Specialised || gate.SpecialisedPoolCapacity != 1)
                throw new InvalidOperationException("An explicit BMS LIFT material requires one exact specialised stage visual.");

            SceneVisualGate = gate;
            SpecialisedSceneVisual = runtime.PrepareSpecialisedVisual(key, this);
            AppliedSceneNodeIds = Array.AsReadOnly(SpecialisedSceneVisual?.RuntimeNodes.Select(node => node.PreparedNode.InstanceId).ToArray() ?? []);
            SpecialisedSceneVisual?.OnApply();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                SpecialisedSceneVisual?.Dispose();
            base.Dispose(isDisposing);
        }
    }
}
