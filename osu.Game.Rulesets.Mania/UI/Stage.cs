// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.Mania.UI.Components;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK;

namespace osu.Game.Rulesets.Mania.UI
{
    /// <summary>
    /// A collection of <see cref="Column"/>s.
    /// </summary>
    public partial class Stage : ScrollingPlayfield
    {
        [Cached]
        public readonly StageDefinition Definition;

        public Column[] Columns => columnFlow.Content;
        private readonly ColumnFlow<Column> columnFlow;
        private readonly Container gameplaySkinLaneCoverLayer;

        private readonly JudgementContainer<DrawableManiaJudgement> judgements;
        private readonly JudgementPooler<DrawableManiaJudgement> judgementPooler;
        private readonly SkinnableDrawable stageBackground;
        private readonly SkinnableDrawable stageForeground;
        private IDisposable? stageForegroundProgrammaticRegistration;
        private IDisposable? judgementProgrammaticRegistration;
        private readonly List<IDisposable> programmaticVisualPartRegistrations = new List<IDisposable>();
        private readonly HashSet<Drawable> registeredProgrammaticVisualPartOwners = new HashSet<Drawable>();
        private GameplaySkinSceneRuntimeHost? sceneRuntime;
        private IManiaGameplaySkinProgrammaticVisualPartReadinessSource? stageBackgroundReadinessSource;

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
        {
            foreach (var c in Columns)
            {
                if (c.ReceivePositionalInputAt(screenSpacePos))
                    return true;
            }

            return false;
        }

        private readonly int firstColumnIndex;

        private ManiaGameplaySkinStageContext layoutStageContext = null!;

        public GameplaySkinLayoutSnapshot LayoutSnapshot => layoutStageContext.Snapshot;

        public GameplaySkinResolvedMaterialSet ResolvedMaterialSet { get; private set; } = null!;

        public GameplaySkinLaneGroupId LayoutGroupId => layoutStageContext.Group.GroupId;

        public Stage(int firstColumnIndex, StageDefinition definition, ref ManiaAction columnStartAction)
        {
            this.firstColumnIndex = firstColumnIndex;
            Definition = definition;

            Name = "Stage";

            Anchor = Anchor.TopLeft;
            Origin = Anchor.TopLeft;
            RelativeSizeAxes = Axes.Both;

            Container columnBackgrounds;
            Container topLevelContainer;

            InternalChildren = new Drawable[]
            {
                new Container
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        stageBackground = new SkinnableDrawable(new ManiaSkinComponentLookup(ManiaSkinComponents.StageBackground), _ => new DefaultStageBackground())
                        {
                            RelativeSizeAxes = Axes.Both
                        },
                        columnBackgrounds = new Container
                        {
                            Name = "Column backgrounds",
                            RelativeSizeAxes = Axes.Both,
                        },
                        new Container
                        {
                            Name = "Barlines mask",
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            RelativeSizeAxes = Axes.Both,
                            Masking = true,
                            Child = new HitPositionPaddedContainer
                            {
                                Name = "Bar lines",
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                RelativeSizeAxes = Axes.Both,
                                Child = HitObjectContainer,
                            }
                        },
                        columnFlow = new ColumnFlow<Column>(definition)
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
                        gameplaySkinLaneCoverLayer = new Container
                        {
                            Name = "Gameplay skin lane-cover layer",
                            RelativeSizeAxes = Axes.Both,
                        },
                        stageForeground = new SkinnableDrawable(new ManiaSkinComponentLookup(ManiaSkinComponents.StageForeground))
                        {
                            RelativeSizeAxes = Axes.Both
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = judgements = new JudgementContainer<DrawableManiaJudgement>
                            {
                                RelativeSizeAxes = Axes.Both,
                            },
                        },
                        topLevelContainer = new Container { RelativeSizeAxes = Axes.Both }
                    }
                }
            };

            for (int i = 0; i < definition.Columns; i++)
            {
                bool isSpecial = definition.IsSpecialColumn(i);

                var action = columnStartAction;
                columnStartAction++;
                var column = CreateColumn(firstColumnIndex + i, isSpecial).With(c =>
                {
                    c.RelativeSizeAxes = Axes.Both;
                    c.Action.Value = action;
                });

                topLevelContainer.Add(column.TopLevelContainer.CreateProxy());
                columnBackgrounds.Add(column.BackgroundContainer.CreateProxy());
                columnFlow.SetContentForColumn(i, column);
                AddNested(column);
            }

            var hitWindows = new ManiaHitWindows();

            AddInternal(judgementPooler = new JudgementPooler<DrawableManiaJudgement>(Enum.GetValues<HitResult>().Where(hitWindows.IsHitResultAllowed)));

            RegisterPool<BarLine, DrawableBarLine>(50, 200);
        }

        internal void AddGameplaySkinLaneCoverHost(ManiaGameplaySkinLaneCoverHost host)
        {
            ArgumentNullException.ThrowIfNull(host);

            if (gameplaySkinLaneCoverLayer.Count != 0)
                throw new InvalidOperationException("A mania stage cannot own more than one active playfield-cover scene host.");

            gameplaySkinLaneCoverLayer.Add(host);
        }

        [BackgroundDependencyLoader(true)]
        private void load(GameplaySkinSceneRuntimeHost? sceneRuntime)
        {
            if (sceneRuntime == null
                || ResolvedMaterialSet.ContractIdentity.Equals(GameplaySkinMaterialContractIdentity.CompatibilityEmpty))
                return;

            this.sceneRuntime = sceneRuntime;

            if (stageBackground.Drawable is IManiaGameplaySkinProgrammaticVisualPartReadinessSource readinessSource)
            {
                stageBackgroundReadinessSource = readinessSource;
                readinessSource.GameplaySkinProgrammaticVisualPartsReady += onProgrammaticVisualPartsReady;
            }

            registerStageBackgroundProgrammaticVisuals();

            if (!stageBackground.Drawable.IsLoaded)
                stageBackground.Drawable.OnLoadComplete += onStageBackgroundLoaded;

            GameplaySkinResolvedMaterialTarget target = GameplaySkinResolvedMaterialTarget.ForStage(layoutStageContext.Group.TopologyGroup);
            stageForegroundProgrammaticRegistration = sceneRuntime.RegisterProgrammaticVisual(
                new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.StageForeground, target),
                stageForeground);
            judgementProgrammaticRegistration = sceneRuntime.RegisterProgrammaticVisual(
                new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.JudgementDisplay, target),
                judgements);
        }

        [Pure]
        protected virtual Column CreateColumn(int index, bool isSpecial) => new Column(index, isSpecial);

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            IReadOnlyDependencyContainer effectiveParent = parent;
            parent.TryGet(out GameplaySkinLayoutRevisionOwner layoutOwner);

            if (!parent.TryGet(out GameplaySkinLayoutSnapshot snapshot))
            {
                if (layoutOwner == null || layoutOwner.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility)
                {
                    throw new InvalidOperationException(
                        "A standalone mania stage requires an explicitly cached compatibility layout owner.");
                }

                GameplaySkinScrollDirection direction = parent.TryGet(out IScrollingInfo scrollingInfo)
                    && scrollingInfo.Direction.Value == ScrollingDirection.Up
                        ? GameplaySkinScrollDirection.Up
                        : GameplaySkinScrollDirection.Down;
                ManiaGameplaySkinLayout compatibility = ManiaGameplaySkinLayout.CreateCompatibility(
                    new[] { Definition }, parent.Get<ISkinSource>(), direction, useSkinGeometry: false);
                var compatibilityDependencies = new DependencyContainer(parent);
                compatibilityDependencies.Cache(compatibility);
                compatibilityDependencies.Cache(compatibility.Snapshot);
                compatibilityDependencies.Cache(GameplaySkinResolvedMaterialSet.CreateEmpty(compatibility.Snapshot));
                effectiveParent = compatibilityDependencies;
                snapshot = compatibility.Snapshot;
            }

            ManiaGameplaySkinLayout.ValidateConsumerCarrier(snapshot, layoutOwner, "stage");

            GameplaySkinLaneTopologyGroup topologyGroup = snapshot.Context.Topology.GroupsInLogicalOrder.FirstOrDefault(group =>
                group.LanesInLogicalOrder[0].GlobalLogicalIndex == firstColumnIndex)
                                                        ?? throw new InvalidOperationException("The mania stage could not resolve its explicit topology group.");

            if (topologyGroup.LogicalIndex >= snapshot.Context.Topology.GroupsInLogicalOrder.Count
                || !ReferenceEquals(snapshot.Context.Topology.GroupsInLogicalOrder[topologyGroup.LogicalIndex], topologyGroup)
                || topologyGroup.LanesInLogicalOrder.Count != Definition.Columns)
            {
                throw new InvalidOperationException("The mania stage topology group is not coherent with its native stage definition.");
            }

            for (int localIndex = 0; localIndex < Definition.Columns; localIndex++)
            {
                GameplaySkinLaneTopologyEntry lane = topologyGroup.LanesInLogicalOrder[localIndex];

                if (lane.GroupLocalLogicalIndex != localIndex
                    || lane.GlobalLogicalIndex != firstColumnIndex + localIndex)
                {
                    throw new InvalidOperationException("The mania stage lane mapping lost its explicit global or group-local logical index.");
                }
            }

            var dependencies = new DependencyContainer(base.CreateChildDependencies(effectiveParent));
            layoutStageContext = new ManiaGameplaySkinStageContext(snapshot, topologyGroup);
            dependencies.Cache(layoutStageContext);

            if (!effectiveParent.TryGet(out GameplaySkinResolvedMaterialSet materialSet)
                || !ReferenceEquals(materialSet.Snapshot, snapshot))
            {
                throw new InvalidOperationException("The mania stage cannot mix layout and material revisions.");
            }

            ResolvedMaterialSet = materialSet;
            return dependencies;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            NewResult += OnNewResult;
        }

        private void onStageBackgroundLoaded(Drawable _)
        {
            stageBackground.Drawable.OnLoadComplete -= onStageBackgroundLoaded;
            registerStageBackgroundProgrammaticVisuals();
        }

        private void onProgrammaticVisualPartsReady() => registerStageBackgroundProgrammaticVisuals();

        private void registerStageBackgroundProgrammaticVisuals()
        {
            if (sceneRuntime == null)
                return;

            GameplaySkinLaneTopologyGroup group = layoutStageContext.Group.TopologyGroup;
            GameplaySkinResolvedMaterialTarget stageTarget = GameplaySkinResolvedMaterialTarget.ForStage(group);

            if (stageBackground.Drawable is IManiaGameplaySkinProgrammaticVisualPartProvider provider)
            {
                if (provider.GameplaySkinProgrammaticVisualParts.Count == 0)
                    return;

                foreach (ManiaGameplaySkinProgrammaticVisualPart part in provider.GameplaySkinProgrammaticVisualParts)
                {
                    if (!registeredProgrammaticVisualPartOwners.Add(part.Owner))
                        continue;

                    if (ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.StageBackground)
                        || ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.PlayfieldBackdrop))
                    {
                        // The two stage-sized public surfaces have distinct native owners. A partial declaration must
                        // never hide its un-authored sibling or any lane/target child of a legacy composite parent.
                        programmaticVisualPartRegistrations.Add(sceneRuntime.RegisterProgrammaticVisual(
                            new GameplaySkinResolvedMaterialKey(part.Slot, stageTarget),
                            part.Owner));
                        continue;
                    }

                    if (ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.PlayfieldBaseplate))
                    {
                        // The baseplate has a distinct native owner. Replacing it must not suppress the stage-side
                        // shell or backdrop compatibility owner which occupies another public slot.
                        programmaticVisualPartRegistrations.Add(sceneRuntime.RegisterProgrammaticVisual(
                            new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.PlayfieldBaseplate, stageTarget),
                            part.Owner));
                        continue;
                    }

                    GameplaySkinResolvedMaterialTarget target = ManiaGameplaySkinProgrammaticVisualPartTargetResolver.Resolve(part, group);
                    programmaticVisualPartRegistrations.Add(sceneRuntime.RegisterProgrammaticVisual(
                        new GameplaySkinResolvedMaterialKey(part.Slot, target),
                        part.Owner));
                }

                if (!provider.GameplaySkinProgrammaticVisualParts.Any(part => ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.StageBackground)))
                    throw new InvalidOperationException("A mania stage background provider did not publish an independently gateable native shell.");

                if (!provider.GameplaySkinProgrammaticVisualParts.Any(part => ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.PlayfieldBackdrop)))
                    throw new InvalidOperationException("A mania stage background provider did not publish an independently gateable playfield backdrop.");

                if (!provider.GameplaySkinProgrammaticVisualParts.Any(part => ReferenceEquals(part.Slot, GameplaySkinSlotCatalog.PlayfieldBaseplate)))
                    throw new InvalidOperationException("A mania stage background provider did not publish an independently gateable native baseplate.");

                return;
            }

            throw new InvalidOperationException("A mania stage background must expose an independently gateable native shell.");
        }

        public override void Add(HitObject hitObject) => Columns[((ManiaHitObject)hitObject).Column - firstColumnIndex].Add(hitObject);

        public override bool Remove(HitObject hitObject) => Columns[((ManiaHitObject)hitObject).Column - firstColumnIndex].Remove(hitObject);

        public override void Add(DrawableHitObject h) => Columns[((ManiaHitObject)h.HitObject).Column - firstColumnIndex].Add(h);

        public override bool Remove(DrawableHitObject h) => Columns[((ManiaHitObject)h.HitObject).Column - firstColumnIndex].Remove(h);

        public void Add(BarLine barLine) => base.Add(barLine);

        internal void OnNewResult(DrawableHitObject judgedObject, JudgementResult result)
        {
            if (!judgedObject.DisplayResult || !DisplayJudgements.Value)
                return;

            judgements.Clear(false);
            judgements.Add(judgementPooler.Get(result.Type, j => j.Apply(result, judgedObject))!);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                // must happen before children are disposed in base call to prevent illegal accesses to the judgement pool.
                NewResult -= OnNewResult;
                stageBackground.Drawable.OnLoadComplete -= onStageBackgroundLoaded;
                if (stageBackgroundReadinessSource != null)
                    stageBackgroundReadinessSource.GameplaySkinProgrammaticVisualPartsReady -= onProgrammaticVisualPartsReady;
                stageForegroundProgrammaticRegistration?.Dispose();
                judgementProgrammaticRegistration?.Dispose();

                foreach (IDisposable registration in programmaticVisualPartRegistrations)
                    registration.Dispose();

                programmaticVisualPartRegistrations.Clear();
            }

            base.Dispose(isDisposing);
        }
    }
}
