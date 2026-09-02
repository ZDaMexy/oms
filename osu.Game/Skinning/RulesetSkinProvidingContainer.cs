// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Skinning
{
    /// <summary>
    /// A type of <see cref="SkinProvidingContainer"/> specialized for <see cref="DrawableRuleset"/> and other gameplay-related components.
    /// Providing access to parent skin sources and the beatmap skin each surrounded with the ruleset legacy skin transformer.
    /// </summary>
    public partial class RulesetSkinProvidingContainer : SkinProvidingContainer
    {
        protected readonly Ruleset Ruleset;
        protected readonly IBeatmap Beatmap;

        [CanBeNull]
        private readonly ISkin beatmapSkin;

        private readonly bool prepareGameplaySkinLayout;

        /// <remarks>
        /// This container already re-exposes all parent <see cref="ISkinSource"/> sources in a ruleset-usable form.
        /// Therefore disallow falling back to any parent <see cref="ISkinSource"/> any further.
        /// </remarks>
        protected override bool AllowFallingBackToParent => false;

        /// <remarks>
        /// BMS playfield geometry and several core/mania gameplay consumers are load-time structures. Keeping this
        /// root attached therefore establishes the deterministic product boundary: current revision publication is
        /// rejected until gameplay (including pre-start and embedded author-preview players) has detached.
        /// </remarks>
        private protected override SkinRevisionParticipantKind RevisionParticipantKind
            => SkinRevisionParticipantKind.LiveGameplayHost;

        private protected override bool AffectsGameplayLayoutPublication => prepareGameplaySkinLayout;

        protected override Container<Drawable> Content { get; } = new Container
        {
            RelativeSizeAxes = Axes.Both,
        };

        public RulesetSkinProvidingContainer(
            Ruleset ruleset,
            IBeatmap beatmap,
            [CanBeNull] ISkin beatmapSkin,
            bool prepareGameplaySkinLayout = false)
        {
            Ruleset = ruleset;
            Beatmap = beatmap;
            this.beatmapSkin = beatmapSkin;
            this.prepareGameplaySkinLayout = prepareGameplaySkinLayout;
        }

        [BackgroundDependencyLoader]
        private void load(SkinManager skinManager)
        {
            InternalChild = new BeatmapSkinProvidingContainer(
                GetRulesetTransformedSkin(beatmapSkin),
                GetRulesetTransformedSkin(skinManager.DefaultOmsSkin),
                prepareGameplaySkinLayout ? prepareGameplayLayout : null,
                Content,
                affectsGameplayLayoutPublication: prepareGameplaySkinLayout);
        }

        private GameplaySkinLayoutPreparationResult prepareGameplayLayout(
            IReadOnlyDependencyContainer dependencies,
            CancellationToken cancellationToken)
        {
            if (Ruleset is IGameplaySkinLayoutPreparer preparer)
                return preparer.PrepareGameplaySkinLayout(Beatmap, dependencies, cancellationToken);

            return GameplaySkinLayoutPreparationResult.Rejected;
        }

        private ResourceStoreBackedSkin rulesetResourcesSkin;
        private GameplaySkinLayoutRevisionOwner gameplaySkinLayoutRevisionOwner;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            if (Ruleset.CreateResourceStore() is IResourceStore<byte[]> resources)
                rulesetResourcesSkin = new ResourceStoreBackedSkin(resources, parent.Get<GameHost>(), parent.Get<AudioManager>());

            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

            // A gameplay tree is a live publication barrier. Cache the exact package revision acquired by that same
            // registration so every ruleset layout adapter and renderer consumes one package/layout pair. Isolated
            // visual tests without a SkinManager receive an explicit compatibility revision rather than guessing one.
            GameplaySkinPackageRevision packageRevision = GameplayPackageRevision ?? GameplaySkinPackageRevision.CreateCompatibility();
            dependencies.Cache(packageRevision);
            SkinRevisionParticipantRegistration revisionParticipant = GameplayRevisionParticipant;
            GameHost gameHost = parent.Get<GameHost>();
            gameplaySkinLayoutRevisionOwner = new GameplaySkinLayoutRevisionOwner(
                packageRevision,
                validateRoot: () => revisionParticipant == null
                                    ? packageRevision.SourceKind == GameplaySkinPackageSourceKind.Compatibility
                                    : revisionParticipant.TryGetCurrentRevision(out SkinCurrentRevision currentRevision)
                                      && packageRevision.RetainsExact(currentRevision),
                acquireWorkLease: () => revisionParticipant?.AcquireWorkLease(),
                captureParticipantGeneration: () =>
                {
                    if (revisionParticipant == null)
                        return packageRevision.SourceKind == GameplaySkinPackageSourceKind.Compatibility ? 0 : null;

                    return revisionParticipant.TryCapturePublicationGeneration(out long generation) ? generation : null;
                },
                validateParticipantGeneration: generation => revisionParticipant == null
                    ? packageRevision.SourceKind == GameplaySkinPackageSourceKind.Compatibility && generation == 0
                    : revisionParticipant.IsPublicationGenerationCurrent(generation),
                commitAtParticipantGeneration: (generation, commit) => revisionParticipant == null
                    ? commitCompatibilityLayout(packageRevision, generation, commit)
                    : revisionParticipant.TryCommitAtPublicationGeneration(generation, commit),
                dispatchCommit: commit => dispatchGameplayLayoutCommit(
                    gameHost,
                    revisionParticipant,
                    packageRevision.SourceKind == GameplaySkinPackageSourceKind.Compatibility,
                    commit));
            dependencies.Cache(gameplaySkinLayoutRevisionOwner);

            return dependencies;
        }

        private static bool commitCompatibilityLayout(
            GameplaySkinPackageRevision packageRevision,
            long generation,
            Action commit)
        {
            if (packageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility || generation != 0)
                return false;

            commit();
            return true;
        }

        /// <summary>
        /// Joins a background layout preparation to the host update scheduler. The dispatcher is synchronous from the
        /// caller's perspective: returning false guarantees that the carrier can be aborted without a late commit.
        /// </summary>
        private static bool dispatchGameplayLayoutCommit(
            GameHost gameHost,
            SkinRevisionParticipantRegistration revisionParticipant,
            bool compatibility,
            Action commit)
        {
            ArgumentNullException.ThrowIfNull(commit);

            if (compatibility || ThreadSafety.IsUpdateThread)
            {
                commit();
                return true;
            }

            if (gameHost == null || revisionParticipant == null || revisionParticipant.IsDisposed)
                return false;

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ScheduledDelegate scheduled = null;
            int admission = 0;

            const int pending = 0;
            const int callback_owned = 1;
            const int cancelled = 2;
            const int completed = 3;

            void exitRequested()
            {
                if (Interlocked.CompareExchange(ref admission, cancelled, pending) == pending)
                    completion.TrySetResult(false);
            }

            gameHost.ExitRequested += exitRequested;

            try
            {
                if (Volatile.Read(ref admission) != pending || revisionParticipant.IsDisposed)
                    return false;

                try
                {
                    scheduled = gameHost.UpdateThread.Scheduler.Add(() =>
                    {
                        // The queued callback and every cancellation path compete for one admission claim. Once the
                        // callback owns it, the background loader joins its result; once cancellation owns it, this
                        // callback is a no-op and can never publish after the caller has disposed the carrier.
                        if (Interlocked.CompareExchange(ref admission, callback_owned, pending) != pending)
                            return;

                        if (revisionParticipant.IsDisposed)
                        {
                            Volatile.Write(ref admission, completed);
                            completion.TrySetResult(false);
                            return;
                        }

                        try
                        {
                            commit();
                            Volatile.Write(ref admission, completed);
                            completion.TrySetResult(true);
                        }
                        catch (Exception exception)
                        {
                            Volatile.Write(ref admission, completed);
                            completion.TrySetException(exception);
                        }
                    });
                }
                catch
                {
                    if (Interlocked.CompareExchange(ref admission, cancelled, pending) == pending)
                        completion.TrySetResult(false);

                    return false;
                }

                Task.WhenAny(completion.Task, revisionParticipant.Detached).GetAwaiter().GetResult();

                if (!completion.Task.IsCompleted
                    && Interlocked.CompareExchange(ref admission, cancelled, pending) == pending)
                {
                    scheduled?.Cancel();
                    completion.TrySetResult(false);
                }

                // If the callback already claimed admission, detach/exit must join it rather than returning false
                // while it can still publish. Every return therefore observes one terminal completion receipt.
                bool committed = completion.Task.GetAwaiter().GetResult();

                if (!committed)
                    scheduled?.Cancel();

                return committed;
            }
            finally
            {
                gameHost.ExitRequested -= exitRequested;
            }
        }

        protected override void RefreshSources()
        {
            // Populate a local list first so we can adjust the returned order as we go.
            var sources = new List<ISkin>();

            Debug.Assert(ParentSource != null);

            foreach (var source in ParentSource.AllSources)
            {
                switch (source)
                {
                    case Skin skin:
                        sources.Add(GetRulesetTransformedSkin(skin));
                        break;

                    default:
                        sources.Add(source);
                        break;
                }
            }

            int lastBuiltInSkinIndex = getLastBuiltInSkinIndex(sources);

            // Ruleset resources should override the product-facing built-in fallback chain,
            // but should still sit behind any user-selected skin layers.
            if (lastBuiltInSkinIndex >= 0)
                sources.Insert(lastBuiltInSkinIndex, rulesetResourcesSkin);
            else
                sources.Add(rulesetResourcesSkin);

            SetSources(sources);
        }

        private static int getLastBuiltInSkinIndex(IReadOnlyList<ISkin> sources)
        {
            for (int i = sources.Count - 1; i >= 0; i--)
            {
                if (isProtectedBuiltInSkinSource(sources[i]))
                    return i;
            }

            return -1;
        }

        private static bool isProtectedBuiltInSkinSource(ISkin source)
            => unwrapSkin(source) is Skin skin && skin.SkinInfo.PerformRead(s => s.Protected);

        private static ISkin unwrapSkin(ISkin skin)
        {
            while (skin is ISkinTransformer transformer)
                skin = transformer.Skin;

            return skin;
        }

        protected ISkin GetRulesetTransformedSkin(ISkin skin)
        {
            if (skin == null)
                return null;

            var rulesetTransformed = Ruleset.CreateSkinTransformer(skin, Beatmap);
            if (rulesetTransformed != null)
                return rulesetTransformed;

            return skin;
        }

        protected override void Dispose(bool isDisposing)
        {
            try
            {
                base.Dispose(isDisposing);
            }
            finally
            {
                try
                {
                    // The publication may own texture-backed package resources. Retire it only after the renderer
                    // subtree has completed disposal through the base container lifecycle.
                    gameplaySkinLayoutRevisionOwner?.Dispose();
                }
                finally
                {
                    rulesetResourcesSkin?.Dispose();
                }
            }
        }
    }
}
