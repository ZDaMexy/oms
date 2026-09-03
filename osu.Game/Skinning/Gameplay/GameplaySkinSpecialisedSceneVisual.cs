// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Read-only exact-publication contract implemented by native pooled gameplay visuals which consume a
    /// specialised author-scene route.
    /// </summary>
    /// <remarks>
    /// Implementations retain native ownership of scrolling geometry and lifetime. They may only expose the
    /// immutable material identity, gate and prepared node IDs received from the shared scene runtime; this
    /// interface deliberately exposes no parsing, resource lookup or mutation authority.
    /// </remarks>
    public interface IGameplaySkinSpecialisedSceneConsumer
    {
        GameplaySkinResolvedMaterialSet ResolvedMaterialSet { get; }

        GameplaySkinResolvedMaterialKey ResolvedMaterialKey { get; }

        GameplaySkinSceneHostedSlot SceneVisualGate { get; }

        IReadOnlyList<string> AppliedSceneNodeIds { get; }
    }

    /// <summary>
    /// One prebuilt author-scene visual owned by a native pooled Note/LN/Key/Mine/BarLine drawable.
    /// </summary>
    /// <remarks>
    /// The native owner remains the only authority for scrolling geometry and object lifetime. This visual consumes
    /// only immutable nodes/resources from the exact committed scene. <see cref="OnApply()"/> and <see cref="OnFree"/>
    /// perform no parsing, resolution, resource lookup, drawable construction or collection growth.
    /// </remarks>
    public sealed partial class GameplaySkinSpecialisedSceneVisual : CompositeDrawable
    {
        private readonly GameplaySkinSceneRuntimeHost host;

        internal int RuntimeInstanceCost { get; }
        internal int RuntimeEffectCost { get; }
        internal int RuntimeTextGlyphCost { get; }
        internal string RuntimeScopeId { get; }
        private bool failed;
        private bool disposed;

        public GameplaySkinResolvedMaterialKey Key { get; }

        public IReadOnlyList<GameplaySkinSceneRuntimeNode> RuntimeNodes { get; }

        public IReadOnlyList<Drawable> RootDrawables { get; }

        public bool IsApplied { get; private set; }

        public long? BoundObjectId { get; private set; }

        internal GameplaySkinSpecialisedSceneVisual(
            GameplaySkinSceneRuntimeHost host,
            GameplaySkinResolvedMaterialKey key,
            IEnumerable<Drawable> roots,
            IEnumerable<GameplaySkinSceneRuntimeNode> runtimeNodes,
            int runtimeInstanceCost,
            int runtimeEffectCost,
            int runtimeTextGlyphCost,
            string runtimeScopeId)
        {
            this.host = host;
            Key = key;
            RuntimeNodes = Array.AsReadOnly(runtimeNodes.ToArray());
            Drawable[] copiedRoots = roots.ToArray();
            RootDrawables = Array.AsReadOnly(copiedRoots);
            RelativeSizeAxes = Axes.Both;
            Alpha = 0;
            InternalChildren = copiedRoots;
            RuntimeInstanceCost = runtimeInstanceCost;
            RuntimeEffectCost = runtimeEffectCost;
            RuntimeTextGlyphCost = runtimeTextGlyphCost;
            RuntimeScopeId = runtimeScopeId;
        }

        public void OnApply()
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            if (failed)
                return;

            BoundObjectId = null;
            IsApplied = true;
            host.ApplySpecialisedVisual(this, null);
            RefreshVisibility(host.IsSceneReady);
        }

        /// <summary>
        /// Applies one pooled visual to the stable engine object whose read-only event state it may consume.
        /// </summary>
        /// <remarks>
        /// The host rebuilds machine/binding state from that object's current immutable snapshot. Declarative tracks
        /// deliberately keep the publication's authoritative gameplay-time phase and are freshly sampled next frame;
        /// this method never starts a renderer-local or wall-clock animation timeline.
        /// </remarks>
        public void OnApply(long objectId)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentOutOfRangeException.ThrowIfNegative(objectId);

            if (failed)
                return;

            BoundObjectId = objectId;
            IsApplied = true;
            host.ApplySpecialisedVisual(this, objectId);
            RefreshVisibility(host.IsSceneReady);
        }

        public void OnFree()
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            if (failed)
                return;

            BoundObjectId = null;
            IsApplied = false;
            host.FreeSpecialisedVisual(this);
            RefreshVisibility(false);
        }

        internal void RefreshVisibility(bool sceneReady)
            => Alpha = !failed && IsApplied && sceneReady ? 1 : 0;

        internal void MarkFailed()
        {
            if (failed)
                return;

            failed = true;
            BoundObjectId = null;
            IsApplied = false;
            Alpha = 0;

            foreach (GameplaySkinSceneRuntimeNode node in RuntimeNodes)
                node.BoundObjectId = null;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (!disposed)
            {
                disposed = true;

                if (isDisposing)
                    host.ReleaseSpecialisedVisual(this);
            }

            base.Dispose(isDisposing);
        }
    }

    public partial class GameplaySkinSceneRuntimeHost
    {
        private readonly Dictionary<GameplaySkinResolvedMaterialKey, int> specialisedVisualCounts =
            new Dictionary<GameplaySkinResolvedMaterialKey, int>();

        private readonly HashSet<GameplaySkinSpecialisedSceneVisual> specialisedVisuals =
            new HashSet<GameplaySkinSpecialisedSceneVisual>();

        private readonly HashSet<GameplaySkinResolvedMaterialKey> failedSpecialisedKeys =
            new HashSet<GameplaySkinResolvedMaterialKey>();

        private long nextSpecialisedVisualId;

        /// <summary>
        /// Prebuilds one reusable visual into an engine-owned local geometry container.
        /// </summary>
        /// <returns>
        /// A lifecycle handle when an authored scene (or a selected generic Mine/BarLine texture) was built;
        /// otherwise <see langword="null"/> so the existing typed/programmatic visual remains authoritative.
        /// </returns>
        public GameplaySkinSpecialisedSceneVisual? PrepareSpecialisedVisual(
            GameplaySkinResolvedMaterialKey key,
            Container nativeLocalOwner,
            int? bgaViewportIndex = null)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(nativeLocalOwner);

            ObjectDisposedException.ThrowIf(disposed, this);

            if (!hostedSlotsByKey.TryGetValue(key, out GameplaySkinSceneHostedSlot? gate))
                throw new ArgumentException("The specialised key is not part of this exact publication.", nameof(key));

            if (failedSpecialisedKeys.Contains(key))
                return null;

            if (gate.Route != GameplaySkinSceneHostRoute.Specialised)
                throw new InvalidOperationException("Only a specialised exact-key route can create a native local scene visual.");

            if (bgaViewportIndex is < 0)
                throw new ArgumentOutOfRangeException(nameof(bgaViewportIndex));

            int count = specialisedVisualCounts.GetValueOrDefault(key);

            if (count >= gate.PreparedRoute.SpecialisedPoolCapacity
                || RuntimeInstanceCount >= GameplaySkinPreparedSceneBudgets.MAX_RUNTIME_INSTANCES)
            {
                addFault("OMS-SKIN-SCENE-RUNTIME-004");

                if (!isPerViewportBgaSpecialisedKey(key))
                    failSpecialisedKey(key);

                return null;
            }

            long visualId = nextSpecialisedVisualId++;
            string runtimeScopeId = $"specialised.{visualId}";
            var roots = new List<Drawable>();
            var nodes = new List<GameplaySkinSceneRuntimeNode>();
            var builtPreparedIds = new HashSet<string>(StringComparer.Ordinal);
            int instanceCountBefore = RuntimeInstanceCount;
            int effectCountBefore = runtimeEffectCount;
            int textGlyphCountBefore = runtimeTextGlyphs;
            GameplaySkinSpecialisedSceneVisual? visual = null;
            bool visualRegistered = false;

            try
            {
                foreach (GameplaySkinPreparedSceneNode routed in gate.RoutedNodes)
                {
                    if (bgaViewportIndex.HasValue
                        && routed.ResolvedTarget.Kind == GameplaySkinSceneTargetKind.Bga
                        && (routed.ResolvedTarget.Index ?? 0) != bgaViewportIndex.Value)
                        continue;

                    if (!builtPreparedIds.Add(routed.InstanceId))
                        continue;

                    roots.Add(buildSpecialisedNode(key, routed, null, visualId, nodes, builtPreparedIds));
                }

                if (roots.Count == 0)
                {
                    if (gate.PreparedRoute.SpecialisedTexture == null)
                        return null;

                    roots.Add(new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Texture = gate.PreparedRoute.SpecialisedTexture,
                    });
                    RuntimeInstanceCount++;
                }

                visual = new GameplaySkinSpecialisedSceneVisual(
                    this,
                    key,
                    roots,
                    nodes,
                    RuntimeInstanceCount - instanceCountBefore,
                    runtimeEffectCount - effectCountBefore,
                    runtimeTextGlyphs - textGlyphCountBefore,
                    runtimeScopeId);
                nativeLocalOwner.Add(visual);
                specialisedVisuals.Add(visual);
                visualRegistered = true;
                specialisedVisualCounts[key] = count + 1;
                gate.IsReplacementReady = true;
                refreshRegisteredProgrammaticVisuals();
                return visual;
            }
            catch
            {
                if (!visualRegistered)
                {
                    rollbackSpecialisedNodes(nodes);
                    RuntimeInstanceCount = instanceCountBefore;
                    runtimeEffectCount = effectCountBefore;
                    runtimeTextGlyphs = textGlyphCountBefore;

                    if (visual != null)
                    {
                        if (ReferenceEquals(visual.Parent, nativeLocalOwner))
                            nativeLocalOwner.Remove(visual, false);

                        visual.MarkFailed();
                        visual.Dispose();
                    }
                    else
                    {
                        foreach (Drawable root in roots)
                            root.Dispose();
                    }
                }

                addFault("OMS-SKIN-SCENE-RUNTIME-005");

                // BGA viewport/frame clones share one global material key but own independent native surfaces.
                // A malformed or exhausted exact viewport keeps only that viewport's native fallback; other
                // already-prepared viewport clones remain valid. Object pools retain their key-wide failure policy.
                if (!isPerViewportBgaSpecialisedKey(key))
                    failSpecialisedKey(key);

                return null;
            }
        }

        private static bool isPerViewportBgaSpecialisedKey(GameplaySkinResolvedMaterialKey key)
            => ReferenceEquals(key.Slot, GameplaySkinSlotCatalog.BgaViewport)
               || ReferenceEquals(key.Slot, GameplaySkinSlotCatalog.BgaFrame);

        internal void ReleaseSpecialisedVisual(GameplaySkinSpecialisedSceneVisual visual)
        {
            if (!specialisedVisuals.Remove(visual))
                return;

            rollbackSpecialisedNodes(visual.RuntimeNodes);
            int untrackedInstances = visual.RuntimeInstanceCost - visual.RuntimeNodes.Count;

            if (untrackedInstances > 0)
                RuntimeInstanceCount = Math.Max(0, RuntimeInstanceCount - untrackedInstances);

            runtimeEffectCount = Math.Max(0, runtimeEffectCount - visual.RuntimeEffectCost);
            runtimeTextGlyphs = Math.Max(0, runtimeTextGlyphs - visual.RuntimeTextGlyphCost);
            int remaining = specialisedVisualCounts[visual.Key] - 1;

            if (remaining == 0)
            {
                specialisedVisualCounts.Remove(visual.Key);

                if (hostedSlotsByKey.TryGetValue(visual.Key, out GameplaySkinSceneHostedSlot? gate))
                    gate.IsReplacementReady = false;
            }
            else
                specialisedVisualCounts[visual.Key] = remaining;

            rebuildStateMachineScopes();
            refreshRegisteredProgrammaticVisuals();
        }

        private Drawable buildSpecialisedNode(
            GameplaySkinResolvedMaterialKey key,
            GameplaySkinPreparedSceneNode prepared,
            GameplaySkinLayoutRect? parentRect,
            long visualId,
            List<GameplaySkinSceneRuntimeNode> nodes,
            HashSet<string> builtPreparedIds)
        {
            if (prepared.MaterialTarget?.Equals(key.Target) != true
                || prepared.Slot != null && !ReferenceEquals(prepared.Slot, key.Slot))
                throw new InvalidOperationException("A specialised subtree cannot cross its exact target or public-slot ownership boundary.");

            if (RuntimeInstanceCount >= GameplaySkinPreparedSceneBudgets.MAX_RUNTIME_INSTANCES)
                throw new InvalidOperationException("The specialised visual exceeds the runtime instance budget.");

            Drawable content = createNodeContent(prepared);
            Drawable? root = null;

            try
            {
                Container transform = createNodeTransform(prepared, content);
                applyNodeProperties(transform, content, prepared.Source.Properties);
                Drawable effected = wrapEffects(transform, prepared.Source.Effects);
                root = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = effected,
                    Depth = (float)getNumber(prepared.Source.Properties, "z", 0),
                };

                if (parentRect.HasValue)
                    applyGeometryAndBlend(root, prepared.Rect, parentRect, prepared.Source.Blend);
                else
                {
                    root.RelativePositionAxes = Axes.Both;
                    root.RelativeSizeAxes = Axes.Both;
                    root.Position = default;
                    root.Size = new osuTK.Vector2(1);
                    root.Blending = ResolveBlend(prepared.Source.Blend);
                }

                string runtimeId = $"specialised.{visualId}/{prepared.InstanceId}";
                var runtime = new GameplaySkinSceneRuntimeNode(prepared, root, content, transform, runtimeId, $"specialised.{visualId}");
                runtimeNodes.Add(runtimeId, runtime);
                RuntimeInstanceCount++;

                if (!runtimeNodesBySourceId.TryGetValue(prepared.Source.Id, out List<GameplaySkinSceneRuntimeNode>? bySource))
                    runtimeNodesBySourceId.Add(prepared.Source.Id, bySource = new List<GameplaySkinSceneRuntimeNode>());

                bySource.Add(runtime);
                nodes.Add(runtime);
                registerStateMachineScopes(runtime);
                bindingStateFamiliesDirty |= GameplaySkinSceneStateFamily.All;

                foreach (GameplaySkinPreparedSceneNode child in prepared.Children)
                {
                    builtPreparedIds.Add(child.InstanceId);
                    Drawable childRoot = buildSpecialisedNode(key, child, prepared.Rect, visualId, nodes, builtPreparedIds);
                    transform.Add(childRoot);
                }

                return root;
            }
            catch
            {
                try
                {
                    (root ?? content).Dispose();
                }
                catch
                {
                    // Keep the stable local scene fault as the observable result. The outer transaction restores
                    // all registry and budget state and the publication owns prepared resource retirement.
                }

                throw;
            }
        }

        private void rollbackSpecialisedNodes(IEnumerable<GameplaySkinSceneRuntimeNode> nodes)
        {
            foreach (GameplaySkinSceneRuntimeNode node in nodes)
            {
                runtimeNodes.Remove(node.InstanceId);

                if (runtimeNodesBySourceId.TryGetValue(node.PreparedNode.Source.Id, out List<GameplaySkinSceneRuntimeNode>? bySource))
                {
                    bySource.Remove(node);

                    if (bySource.Count == 0)
                        runtimeNodesBySourceId.Remove(node.PreparedNode.Source.Id);
                }

                if (RuntimeInstanceCount > 0)
                    RuntimeInstanceCount--;
            }
        }

        private void refreshRegisteredProgrammaticVisuals()
        {
            foreach (RegisteredProgrammaticVisual registration in registeredProgrammaticVisuals)
                registration.Refresh(IsSceneReady);
        }

        private void refreshSpecialisedVisuals()
        {
            foreach (GameplaySkinSpecialisedSceneVisual visual in specialisedVisuals)
                visual.RefreshVisibility(IsSceneReady);
        }

        private void failSpecialisedKey(GameplaySkinResolvedMaterialKey key)
        {
            if (!failedSpecialisedKeys.Add(key))
                return;

            GameplaySkinSpecialisedSceneVisual[] failedVisuals = specialisedVisuals
                                                                  .Where(visual => visual.Key.Equals(key))
                                                                  .ToArray();

            foreach (GameplaySkinSpecialisedSceneVisual visual in failedVisuals)
            {
                specialisedVisuals.Remove(visual);
                rollbackSpecialisedNodes(visual.RuntimeNodes);
                int untrackedInstances = visual.RuntimeInstanceCost - visual.RuntimeNodes.Count;

                if (untrackedInstances > 0)
                    RuntimeInstanceCount = Math.Max(0, RuntimeInstanceCount - untrackedInstances);

                runtimeEffectCount = Math.Max(0, runtimeEffectCount - visual.RuntimeEffectCost);
                runtimeTextGlyphs = Math.Max(0, runtimeTextGlyphs - visual.RuntimeTextGlyphCost);
                visual.MarkFailed();
            }

            specialisedVisualCounts.Remove(key);

            if (hostedSlotsByKey.TryGetValue(key, out GameplaySkinSceneHostedSlot? gate))
            {
                gate.IsReplacementReady = false;
                gate.UsePreparedFailureRoute();
            }

            rebuildStateMachineScopes();
            markStateFamilyDirty(GameplaySkinSceneStateFamily.All);
            refreshRegisteredProgrammaticVisuals();
        }

        internal void ApplySpecialisedVisual(GameplaySkinSpecialisedSceneVisual visual, long? objectId)
        {
            if (!specialisedVisuals.Contains(visual))
                return;

            foreach (GameplaySkinSceneRuntimeNode node in visual.RuntimeNodes)
                node.BoundObjectId = objectId;

            resetSpecialisedStateMachineScope(visual.RuntimeScopeId);
            bindingStateFamiliesDirty |= GameplaySkinSceneStateFamily.Object | GameplaySkinSceneStateFamily.Judgement;
            visual.RefreshVisibility(IsSceneReady);
        }

        internal void FreeSpecialisedVisual(GameplaySkinSpecialisedSceneVisual visual)
        {
            if (!specialisedVisuals.Contains(visual))
                return;

            foreach (GameplaySkinSceneRuntimeNode node in visual.RuntimeNodes)
                node.BoundObjectId = null;

            resetSpecialisedStateMachineScope(visual.RuntimeScopeId);
            bindingStateFamiliesDirty |= GameplaySkinSceneStateFamily.Object | GameplaySkinSceneStateFamily.Judgement;
            visual.RefreshVisibility(false);
        }
    }
}
