// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class DrawableBmsBarLine : DrawableHitObject<BmsBarLine>, IGameplaySkinSpecialisedSceneConsumer
    {
        private readonly Container sceneVisualContainer;
        private readonly GameplaySkinResolvedMaterialSet? materialSet;
        private GameplaySkinSpecialisedSceneVisual? sceneVisual;
        private IDisposable? programmaticVisualRegistration;

        [Resolved(CanBeNull = true)]
        private DrawableBmsRuleset? drawableRuleset { get; set; }

        private GameplaySkinResolvedMaterialKey? resolvedMaterialKey;
        private GameplaySkinSceneHostedSlot? sceneVisualGate;

        public override bool DisplayResult => false;

        protected override double InitialLifetimeOffset => 2000;

        internal BmsGameplayLayoutSnapshot? LayoutSnapshot { get; }

        internal GameplaySkinLayoutGroup? LayoutGroup { get; }

        internal Drawable ProgrammaticVisual { get; }

        public GameplaySkinResolvedMaterialSet ResolvedMaterialSet
            => materialSet ?? throw new InvalidOperationException("A compatibility BMS bar line has no exact C4 material publication.");

        public GameplaySkinResolvedMaterialKey ResolvedMaterialKey
            => resolvedMaterialKey ?? throw new InvalidOperationException("A compatibility BMS bar line has no specialised C5 material key.");

        public GameplaySkinSceneHostedSlot SceneVisualGate
            => sceneVisualGate ?? throw new InvalidOperationException("A compatibility BMS bar line has no specialised C5 visual gate.");

        public IReadOnlyList<string> AppliedSceneNodeIds { get; private set; } = Array.Empty<string>();

        // Required only by DrawablePool's type constraint. Production pools always use the exact constructor below.
        public DrawableBmsBarLine()
            : base(new BmsBarLine { Major = true })
        {
            HandleUserInput = false;
            Anchor = Origin = Anchor.BottomLeft;
            RelativeSizeAxes = Axes.X;
            Width = 1;
            Height = BmsPlayfieldLayoutProfile.CreateDefault(BmsKeymode.Key7K, 8).BarLineHeight;

            ProgrammaticVisual = new SkinnableDrawable(
                new BmsLaneSkinLookup(BmsLaneSkinElements.BarLine, 0, 8, true, BmsKeymode.Key7K, true),
                _ => new DefaultBmsBarLineDisplay(true, BmsKeymode.Key7K))
            {
                RelativeSizeAxes = Axes.Both,
                CentreComponent = false,
            };
            sceneVisualContainer = new Container { RelativeSizeAxes = Axes.Both };
            AddRangeInternal(new[] { ProgrammaticVisual, sceneVisualContainer });
        }

        internal DrawableBmsBarLine(
            BmsBarLine hitObject,
            GameplaySkinLayoutGroup group,
            BmsGameplayLayoutSnapshot layoutSnapshot,
            GameplaySkinResolvedMaterialSet materialSet)
            : base(hitObject)
        {
            ArgumentNullException.ThrowIfNull(group);
            ArgumentNullException.ThrowIfNull(layoutSnapshot);
            ArgumentNullException.ThrowIfNull(materialSet);

            if (!ReferenceEquals(materialSet.Snapshot, layoutSnapshot.Neutral)
                || !ReferenceEquals(layoutSnapshot.Neutral.GetGroup(group.GroupId), group)
                || hitObject.GroupLogicalIndex != group.TopologyGroup.LogicalIndex
                || hitObject.GroupId == null
                || !hitObject.GroupId.Equals(group.GroupId))
            {
                throw new ArgumentException("A BMS bar line must retain its exact C3 group and C4 material publication.", nameof(hitObject));
            }

            LayoutSnapshot = layoutSnapshot;
            LayoutGroup = group;
            this.materialSet = materialSet;
            HandleUserInput = false;
            Anchor = Origin = Anchor.BottomLeft;
            RelativeSizeAxes = Axes.Both;
            Width = 1;
            Height = layoutSnapshot.ProjectVerticalProfileMetric(layoutSnapshot.Profile.BarLineHeight);

            ProgrammaticVisual = createGroupFallback(hitObject, group, layoutSnapshot);
            sceneVisualContainer = new Container { RelativeSizeAxes = Axes.Both };
            AddRangeInternal(new[] { ProgrammaticVisual, sceneVisualContainer });
        }

        private static Container createGroupFallback(
            BmsBarLine hitObject,
            GameplaySkinLayoutGroup group,
            BmsGameplayLayoutSnapshot layoutSnapshot)
        {
            BmsGameplayLayoutLane[] lanes = layoutSnapshot.LanesInLogicalOrder
                                                          .Where(lane => lane.NeutralLane.TopologyEntry.Identity.Group.Id.Equals(group.GroupId))
                                                          .OrderBy(lane => lane.VisualIndex)
                                                          .ToArray();

            if (lanes.Length == 0)
                throw new InvalidOperationException("An exact BMS bar-line group cannot be empty.");

            return new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = lanes.Select(lane =>
                {
                    GameplaySkinLayoutRect laneRect = lane.NeutralLane.Rect;

                    return new Container
                    {
                        RelativePositionAxes = Axes.X,
                        RelativeSizeAxes = Axes.Both,
                        X = (laneRect.X - group.Rect.X) / group.Rect.Width,
                        Width = laneRect.Width / group.Rect.Width,
                        Child = new SkinnableDrawable(new BmsLaneSkinLookup(
                                BmsLaneSkinElements.BarLine,
                                lane.LogicalIndex,
                                layoutSnapshot.LanesInLogicalOrder.Count,
                                lane.IsScratch,
                                layoutSnapshot.Keymode,
                                hitObject.Major,
                                lane.LaneId),
                            _ => new DefaultBmsBarLineDisplay(hitObject.Major, layoutSnapshot.Keymode))
                        {
                            RelativeSizeAxes = Axes.Both,
                            CentreComponent = false,
                        },
                    };
                }).ToArray(),
            };
        }

        [BackgroundDependencyLoader(true)]
        private void loadGameplaySkinScene(GameplaySkinSceneRuntimeHost? runtime)
        {
            if (runtime == null || LayoutSnapshot == null || materialSet == null || LayoutGroup == null)
                return;

            var key = new GameplaySkinResolvedMaterialKey(
                GameplaySkinSlotCatalog.BarLine,
                GameplaySkinResolvedMaterialTarget.ForGroup(LayoutGroup.TopologyGroup));

            if (!runtime.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate) || gate == null)
                throw new InvalidOperationException("The exact BMS bar-line scene gate is missing from the committed publication.");

            resolvedMaterialKey = key;
            sceneVisualGate = gate;

            if (gate.Route == GameplaySkinSceneHostRoute.Specialised)
            {
                sceneVisual = runtime.PrepareSpecialisedVisual(key, sceneVisualContainer);

                if (sceneVisual != null)
                    AppliedSceneNodeIds = Array.AsReadOnly(sceneVisual.RuntimeNodes.Select(node => node.PreparedNode.InstanceId).ToArray());
            }

            if (gate.Route == GameplaySkinSceneHostRoute.Suppressed || sceneVisual != null)
                programmaticVisualRegistration = runtime.RegisterProgrammaticVisual(key, ProgrammaticVisual);
        }

        protected override void OnApply()
        {
            base.OnApply();

            if (LayoutGroup != null
                && (HitObject.GroupLogicalIndex != LayoutGroup.TopologyGroup.LogicalIndex
                    || HitObject.GroupId == null
                    || !HitObject.GroupId.Equals(LayoutGroup.GroupId)))
            {
                throw new InvalidOperationException("A pooled BMS bar line cannot cross its exact C3 group owner.");
            }

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
