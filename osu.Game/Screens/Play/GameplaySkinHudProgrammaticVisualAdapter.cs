// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// Identifies HUD drawables which already have one exact ruleset-owned gameplay-skin registration.
    /// The shared adapter must not clone, remove or register these owners a second time.
    /// </summary>
    internal interface IGameplaySkinIndependentlyRegisteredHudOwnerSource
    {
        IReadOnlyList<Drawable> GameplaySkinIndependentlyRegisteredHudOwners { get; }
    }

    /// <summary>
    /// Registers real core HUD visuals against exact gameplay-skin keys without taking over their data authority.
    /// Multi-stage compatibility visuals are reproduced as non-overlapping clips whose union is the original visual.
    /// </summary>
    internal sealed partial class GameplaySkinHudProgrammaticVisualAdapter : IDisposable
    {
        private readonly GameplaySkinSceneRuntimeHost sceneRuntime;
        private readonly IReadOnlyList<SkinnableContainer> componentContainers;
        private readonly Container partitionHost;
        private readonly List<IDisposable> registrations = new List<IDisposable>();
        private readonly List<GameplaySkinHudProgrammaticVisualPartition> gaugePartitions = new List<GameplaySkinHudProgrammaticVisualPartition>();
        private readonly List<GameplaySkinHudProgrammaticVisualPartition> textPartitions = new List<GameplaySkinHudProgrammaticVisualPartition>();
        private readonly List<GameplaySkinHudProgrammaticVisualPartition> comboPartitions = new List<GameplaySkinHudProgrammaticVisualPartition>();
        private readonly List<GameplaySkinHudProgrammaticVisualPartition> judgementPartitions = new List<GameplaySkinHudProgrammaticVisualPartition>();
        private readonly List<GameplaySkinHudProgrammaticVisualPartition> decorationPartitions = new List<GameplaySkinHudProgrammaticVisualPartition>();
        private readonly List<GameplaySkinHudProgrammaticVisualResidual> residualPartitions = new List<GameplaySkinHudProgrammaticVisualResidual>();
        private readonly List<MovedSource> movedSources = new List<MovedSource>();
        private bool built;
        private bool disposed;

        public IReadOnlyList<GameplaySkinHudProgrammaticVisualPartition> GaugePartitions => gaugePartitions.AsReadOnly();

        public IReadOnlyList<GameplaySkinHudProgrammaticVisualPartition> TextPartitions => textPartitions.AsReadOnly();

        public IReadOnlyList<GameplaySkinHudProgrammaticVisualPartition> ComboPartitions => comboPartitions.AsReadOnly();

        public IReadOnlyList<GameplaySkinHudProgrammaticVisualPartition> JudgementPartitions => judgementPartitions.AsReadOnly();

        public IReadOnlyList<GameplaySkinHudProgrammaticVisualPartition> DecorationPartitions => decorationPartitions.AsReadOnly();

        public IReadOnlyList<GameplaySkinHudProgrammaticVisualResidual> ResidualPartitions => residualPartitions.AsReadOnly();

        public GameplaySkinHudProgrammaticVisualAdapter(
            GameplaySkinSceneRuntimeHost sceneRuntime,
            IReadOnlyList<SkinnableContainer> componentContainers,
            Container partitionHost)
        {
            this.sceneRuntime = sceneRuntime ?? throw new ArgumentNullException(nameof(sceneRuntime));
            ArgumentNullException.ThrowIfNull(componentContainers);

            if (componentContainers.Count == 0 || componentContainers.Any(container => container == null))
                throw new ArgumentException("At least one concrete HUD source container is required.", nameof(componentContainers));

            this.componentContainers = Array.AsReadOnly(componentContainers.Distinct().ToArray());
            this.partitionHost = partitionHost ?? throw new ArgumentNullException(nameof(partitionHost));

            foreach (SkinnableContainer container in this.componentContainers)
                container.OnComponentsLoaded += onComponentsLoaded;

            if (this.componentContainers.All(container => container.ComponentsLoaded))
                rebuildRegistrations();
        }

        private void onComponentsLoaded(Drawable _)
        {
            if (!built && componentContainers.All(container => container.ComponentsLoaded))
                rebuildRegistrations();
        }

        private void rebuildRegistrations()
        {
            if (disposed || built)
                return;

            clearRegistrations();

            if (sceneRuntime.MaterialSet.ContractIdentity.Equals(GameplaySkinMaterialContractIdentity.CompatibilityEmpty))
            {
                built = true;
                return;
            }

            GameplaySkinPreparedHudPlan hudPlan = sceneRuntime.PreparedScene.HudPlan;

            if (!ReferenceEquals(hudPlan.Snapshot, sceneRuntime.PreparedScene.Snapshot)
                || !ReferenceEquals(hudPlan.MaterialSet, sceneRuntime.MaterialSet))
            {
                throw new InvalidOperationException("The HUD adapter requires the exact background-prepared scene publication plan.");
            }

            SourceOwner[] components = componentContainers
                .SelectMany(owner =>
                {
                    IReadOnlyList<Drawable> independentlyRegistered = owner is IGameplaySkinIndependentlyRegisteredHudOwnerSource source
                        ? source.GameplaySkinIndependentlyRegisteredHudOwners
                        : Array.Empty<Drawable>();

                    return owner.Components.OfType<Drawable>()
                                .Where(component => !independentlyRegistered.Contains(component)
                                                    && !(component.Parent is IGameplaySkinIndependentlyRegisteredHudOwnerSource nestedSource
                                                         && nestedSource.GameplaySkinIndependentlyRegisteredHudOwners.Contains(component))
                                                    && component is IGameplaySkinHudProgrammaticVisualSource)
                                .Select(component => new SourceOwner(
                                    owner,
                                    component,
                                    ((IGameplaySkinHudProgrammaticVisualSource)component).GameplaySkinHudRole));
                })
                .ToArray();

            SourceOwner[] routed = components.Where(component => hudPlan.GetRole(component.Role).RequiresRouting).ToArray();

            if (routed.Length > hudPlan.MaximumSourceOwners
                || routed.GroupBy(component => component.Role).Any(group => group.Count() > hudPlan.GetRole(group.Key).MaximumSourceOwners)
                || routed.Sum(component => hudPlan.GetRole(component.Role).Partitions.Count) > GameplaySkinPreparedSceneBudgets.MAX_HUD_PARTITION_RECORDS
                || routed.Sum(component => hudPlan.GetRole(component.Role).Residuals.Count) > GameplaySkinPreparedSceneBudgets.MAX_HUD_RESIDUAL_RECORDS)
            {
                throw new InvalidOperationException("The exact prepared HUD source/partition budget was exceeded.");
            }

            try
            {
                foreach (IGrouping<GameplaySkinPreparedHudRole, SourceOwner> group in routed.GroupBy(component => component.Role).OrderBy(group => group.Key))
                    createRoleSurface(hudPlan.GetRole(group.Key), group.ToArray(), partitionsFor(group.Key));

                built = true;
            }
            catch
            {
                restoreMovedSources();
                clearRegistrations();
                throw;
            }
        }

        private ICollection<GameplaySkinHudProgrammaticVisualPartition> partitionsFor(GameplaySkinPreparedHudRole role) => role switch
        {
            GameplaySkinPreparedHudRole.Gauge => gaugePartitions,
            GameplaySkinPreparedHudRole.Text => textPartitions,
            GameplaySkinPreparedHudRole.Combo => comboPartitions,
            GameplaySkinPreparedHudRole.Judgement => judgementPartitions,
            GameplaySkinPreparedHudRole.Decoration => decorationPartitions,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown prepared HUD role."),
        };

        private void createRoleSurface(
            GameplaySkinPreparedHudRolePlan plan,
            IReadOnlyList<SourceOwner> sources,
            ICollection<GameplaySkinHudProgrammaticVisualPartition> target)
        {
            if (!plan.RequiresRouting || sources.Count == 0)
                return;

            var capture = new GameplaySkinHudCaptureSurface();
            partitionHost.Add(capture);

            foreach (SourceOwner source in sources)
            {
                if (source.Source is not ISerialisableDrawable serialisable)
                    throw new InvalidOperationException("An allowlisted HUD source must retain its skinnable owner identity.");

                source.Owner.Remove(serialisable, disposeImmediately: false);
                movedSources.Add(new MovedSource(source.Owner, serialisable, source.Source));
                capture.Add(source.Source);
            }

            for (int partitionIndex = 0; partitionIndex < plan.Partitions.Count; partitionIndex++)
            {
                GameplaySkinPreparedHudPartition prepared = plan.Partitions[partitionIndex];
                Container clip = createClip(capture, prepared.RelativeStart, prepared.RelativeWidth);
                partitionHost.Add(clip);
                registrations.Add(sceneRuntime.RegisterProgrammaticVisual(prepared.ControllingKeys, clip));

                for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
                {
                    target.Add(new GameplaySkinHudProgrammaticVisualPartition(
                        sourceIndex,
                        prepared.StageKey,
                        prepared.ControllingKeys.ToArray(),
                        clip,
                        sources[sourceIndex].Source,
                        prepared.RelativeStart,
                        prepared.RelativeWidth));
                }
            }

            foreach (GameplaySkinPreparedHudResidual prepared in plan.Residuals)
            {
                Container clip = createClip(capture, prepared.RelativeStart, prepared.RelativeWidth);
                partitionHost.Add(clip);
                registrations.Add(sceneRuntime.RegisterResidualProgrammaticVisual(prepared.AnyKeys, prepared.AllKeys, clip));

                for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
                {
                    residualPartitions.Add(new GameplaySkinHudProgrammaticVisualResidual(
                        sourceIndex,
                        plan.Slot,
                        clip,
                        prepared.RelativeStart,
                        prepared.RelativeWidth));
                }
            }
        }

        private static Container createClip(BufferedContainer capture, float start, float width)
        {
            if (!float.IsFinite(start) || !float.IsFinite(width) || start < 0 || width <= 0 || start + width > 1.0001f)
                throw new InvalidOperationException("A prepared HUD clip must remain inside the exact screen bounds.");

            BufferedContainerView<Drawable> view = capture.CreateView();
            var fullCanvas = new Container
            {
                RelativeSizeAxes = Axes.Both,
                RelativePositionAxes = Axes.X,
                X = -start / width,
                Width = 1 / width,
                Child = view,
            };
            return new Container
            {
                RelativeSizeAxes = Axes.Both,
                RelativePositionAxes = Axes.X,
                X = start,
                Width = width,
                Masking = true,
                Child = fullCanvas,
            };
        }

        private void restoreMovedSources()
        {
            for (int index = movedSources.Count - 1; index >= 0; index--)
            {
                MovedSource moved = movedSources[index];
                (moved.Source.Parent as Container)?.Remove(moved.Source, disposeImmediately: false);
                moved.Owner.Add(moved.Serialisable);
            }

            movedSources.Clear();
        }

        private void clearRegistrations(bool clearMountedPartitions = true)
        {
            foreach (IDisposable registration in registrations)
                registration.Dispose();

            registrations.Clear();

            if (clearMountedPartitions)
                partitionHost.Clear(disposeChildren: true);

            gaugePartitions.Clear();
            textPartitions.Clear();
            comboPartitions.Clear();
            judgementPartitions.Clear();
            decorationPartitions.Clear();
            residualPartitions.Clear();

            if (clearMountedPartitions)
                movedSources.Clear();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            foreach (SkinnableContainer container in componentContainers)
                container.OnComponentsLoaded -= onComponentsLoaded;
            clearRegistrations(clearMountedPartitions: false);
        }

        private sealed partial class GameplaySkinHudCaptureSurface : BufferedContainer
        {
            public GameplaySkinHudCaptureSurface()
                : base(cachedFrameBuffer: true)
            {
                Name = "gameplay-skin.hud-capture";
                RelativeSizeAxes = Axes.Both;
                RelativePositionAxes = Axes.X;
                X = 2;
                AlwaysPresent = true;
            }

            protected override void Update()
            {
                ForceRedraw();
                base.Update();
            }
        }

        private readonly record struct SourceOwner(
            SkinnableContainer Owner,
            Drawable Source,
            GameplaySkinPreparedHudRole Role);

        private readonly record struct MovedSource(
            SkinnableContainer Owner,
            ISerialisableDrawable Serialisable,
            Drawable Source);
    }

    internal sealed class GameplaySkinHudProgrammaticVisualPartition
    {
        public int SourceIndex { get; }

        public GameplaySkinResolvedMaterialKey StageKey { get; }

        public IReadOnlyList<GameplaySkinResolvedMaterialKey> ControllingKeys { get; }

        public Container Owner { get; }

        public Drawable Visual { get; }

        public float RelativeStart { get; }

        public float RelativeWidth { get; }

        public GameplaySkinHudProgrammaticVisualPartition(
            int sourceIndex,
            GameplaySkinResolvedMaterialKey stageKey,
            GameplaySkinResolvedMaterialKey[] controllingKeys,
            Container owner,
            Drawable visual,
            float relativeStart,
            float relativeWidth)
        {
            SourceIndex = sourceIndex;
            StageKey = stageKey ?? throw new ArgumentNullException(nameof(stageKey));
            ControllingKeys = Array.AsReadOnly(controllingKeys ?? throw new ArgumentNullException(nameof(controllingKeys)));
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Visual = visual ?? throw new ArgumentNullException(nameof(visual));
            RelativeStart = relativeStart;
            RelativeWidth = relativeWidth;
        }
    }

    internal sealed class GameplaySkinHudProgrammaticVisualResidual
    {
        public int SourceIndex { get; }

        public GameplaySkinSlotDescriptor Slot { get; }

        public Container Owner { get; }

        public float RelativeStart { get; }

        public float RelativeWidth { get; }

        public GameplaySkinHudProgrammaticVisualResidual(
            int sourceIndex,
            GameplaySkinSlotDescriptor slot,
            Container owner,
            float relativeStart,
            float relativeWidth)
        {
            SourceIndex = sourceIndex;
            Slot = slot ?? throw new ArgumentNullException(nameof(slot));
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            RelativeStart = relativeStart;
            RelativeWidth = relativeWidth;
        }
    }

}
