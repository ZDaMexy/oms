// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// A fixed judgement-line visual leased only by a real native long-note owner.
    /// State and animation time come from that exact object's event snapshot.
    /// </summary>
    internal partial class BmsLongNoteEffect : PoolableDrawable, IGameplaySkinSpecialisedSceneConsumer
    {
        private readonly GameplaySkinSceneRuntimeHost? sceneRuntime;
        private readonly GameplaySkinResolvedMaterialKey? materialKey;
        private GameplaySkinSpecialisedSceneVisual? visual;
        private Container owner = null!;
        private bool finishing;
        private long finishEpoch;

        public GameplaySkinResolvedMaterialSet ResolvedMaterialSet => sceneRuntime!.MaterialSet;
        public GameplaySkinResolvedMaterialKey ResolvedMaterialKey => materialKey!;
        public GameplaySkinSceneHostedSlot SceneVisualGate { get; private set; } = null!;
        public IReadOnlyList<string> AppliedSceneNodeIds { get; private set; } = Array.Empty<string>();
        public long? BoundObjectId { get; private set; }

        public BmsLongNoteEffect()
        {
            RelativeSizeAxes = Axes.Both;
        }

        internal BmsLongNoteEffect(GameplaySkinSceneRuntimeHost runtime, GameplaySkinResolvedMaterialKey key)
            : this()
        {
            sceneRuntime = runtime;
            materialKey = key;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (sceneRuntime == null || materialKey == null
                || !sceneRuntime.TryGetVisualGate(materialKey, out GameplaySkinSceneHostedSlot? gate)
                || gate?.Route != GameplaySkinSceneHostRoute.Specialised
                || gate.SpecialisedPoolCapacity != GameplaySkinPreparedSceneBudgets.MAX_LONG_NOTE_EFFECT_VISUALS_PER_KEY)
                throw new InvalidOperationException("A long-note effect requires its exact bounded native scene publication.");

            SceneVisualGate = gate;
            owner = new Container { RelativeSizeAxes = Axes.Both };
            InternalChild = owner;
            visual = sceneRuntime.PrepareSpecialisedVisual(materialKey, owner);
            if (visual != null)
                AppliedSceneNodeIds = Array.AsReadOnly(visual.RuntimeNodes.Select(node => node.PreparedNode.InstanceId).ToArray());
        }

        internal void Apply(long objectId)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(objectId);
            BoundObjectId = objectId;
            finishing = false;
            visual?.OnApply(objectId);
        }

        internal void Finish()
        {
            SetHolding(false);
            visual?.FinishLongNoteEffect();
            finishing = true;
            finishEpoch = sceneRuntime!.CurrentEpoch;
            // Lifetime is a bounded native lease. The artwork still samples the real terminal event origin.
            LifetimeEnd = Time.Current + (visual?.RequestedLongNoteOutroDuration ?? 0);
        }

        internal void SetHolding(bool holding)
        {
            if (visual?.RuntimeNodes.Count == 0)
                owner.Alpha = holding ? 1 : 0;
        }

        protected override void Update()
        {
            base.Update();
            if (finishing && finishEpoch != sceneRuntime!.CurrentEpoch)
                Release();
        }

        internal void Release()
        {
            visual?.OnFree();
            Alpha = 0;
            Expire();
        }

        protected override void PrepareForUse()
        {
            base.PrepareForUse();
            if (!BoundObjectId.HasValue)
                throw new InvalidOperationException("A long-note effect lease requires one real object identity.");
            Alpha = 1;
            LifetimeStart = double.MinValue;
            LifetimeEnd = double.MaxValue;
        }

        protected override void FreeAfterUse()
        {
            visual?.OnFree();
            BoundObjectId = null;
            finishing = false;
            owner.Alpha = 1;
            Alpha = 0;
            base.FreeAfterUse();
        }
    }
}
