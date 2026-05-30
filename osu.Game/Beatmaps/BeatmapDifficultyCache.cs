// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Textures;
using osu.Framework.Lists;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osu.Game.Storyboards;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// A component which performs and acts as a central cache for difficulty calculations of beatmap/ruleset/mod combinations.
    /// Currently not persisted between game sessions.
    /// </summary>
    public partial class BeatmapDifficultyCache : MemoryCachingComponent<BeatmapDifficultyCache.DifficultyCacheLookup, StarDifficulty?>
    {
        // Too many simultaneous updates can lead to stutters. One thread seems to work fine for song select display purposes.
        private readonly ThreadedTaskScheduler updateScheduler = new ThreadedTaskScheduler(1, nameof(BeatmapDifficultyCache));

        /// <summary>
        /// All bindables that should be updated along with the current ruleset + mods.
        /// </summary>
        private readonly WeakList<BindableStarDifficulty> trackedBindables = new WeakList<BindableStarDifficulty>();

        /// <summary>
        /// Cancellation sources used by tracked bindables.
        /// </summary>
        private readonly List<CancellationTokenSource> linkedCancellationSources = new List<CancellationTokenSource>();

        /// <summary>
        /// Lock to be held when operating on <see cref="trackedBindables"/> or <see cref="linkedCancellationSources"/>.
        /// </summary>
        private readonly object bindableUpdateLock = new object();

        private CancellationTokenSource trackedUpdateCancellationSource = new CancellationTokenSource();

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private RealmAccess realmAccess { get; set; } = null!;

        [Resolved]
        private Bindable<RulesetInfo> currentRuleset { get; set; } = null!;

        [Resolved]
        private Bindable<IReadOnlyList<Mod>> currentMods { get; set; } = null!;

        [Resolved]
        private IRulesetStore rulesetStore { get; set; } = null!;

        private ModSettingChangeTracker? modSettingChangeTracker;
        private ScheduledDelegate? debouncedModSettingsChange;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            currentRuleset.BindValueChanged(_ => Scheduler.AddOnce(updateTrackedBindables));

            currentMods.BindValueChanged(mods =>
            {
                // A change in bindable here doesn't guarantee that mods have actually changed.
                // However, we *do* want to make sure that the mod *references* are the same;
                // `SequenceEqual()` without a comparer would fall back to `IEquatable`.
                // Failing to ensure reference equality can cause setting change tracking to fail later.
                if (mods.OldValue.SequenceEqual(mods.NewValue, ReferenceEqualityComparer.Instance))
                    return;

                modSettingChangeTracker?.Dispose();

                Scheduler.AddOnce(updateTrackedBindables);

                modSettingChangeTracker = new ModSettingChangeTracker(mods.NewValue);
                modSettingChangeTracker.SettingChanged += _ =>
                {
                    lock (bindableUpdateLock)
                    {
                        debouncedModSettingsChange?.Cancel();
                        debouncedModSettingsChange = Scheduler.AddDelayed(updateTrackedBindables, 100);
                    }
                };
            }, true);
        }

        /// <summary>
        /// Notify this cache that a beatmap has been invalidated/updated.
        /// </summary>
        /// <param name="oldBeatmap">The old beatmap model.</param>
        /// <param name="newBeatmap">The updated beatmap model.</param>
        public void Invalidate(IBeatmapInfo oldBeatmap, IBeatmapInfo newBeatmap)
        {
            base.Invalidate(lookup => lookup.BeatmapInfo.Equals(oldBeatmap));

            lock (bindableUpdateLock)
            {
                bool trackedBindablesRefreshRequired = false;

                foreach (var bsd in trackedBindables.Where(bsd => bsd.BeatmapInfo.Equals(oldBeatmap)))
                {
                    bsd.BeatmapInfo = newBeatmap;
                    trackedBindablesRefreshRequired = true;
                }

                if (trackedBindablesRefreshRequired)
                    Scheduler.AddOnce(updateTrackedBindables);
            }
        }

        /// <summary>
        /// Retrieves a bindable containing the star difficulty of a <see cref="BeatmapInfo"/> that follows the currently-selected ruleset and mods.
        /// </summary>
        /// <param name="beatmapInfo">The <see cref="BeatmapInfo"/> to get the difficulty of.</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken"/> which stops updating the star difficulty for the given <see cref="BeatmapInfo"/>.</param>
        /// <param name="computationDelay">A delay in milliseconds before performing the </param>
        /// <returns>A bindable that is updated to contain the star difficulty when it becomes available. May be an approximation while in an initial calculating state.</returns>
        public IBindable<StarDifficulty> GetBindableDifficulty(IBeatmapInfo beatmapInfo, CancellationToken cancellationToken = default, int computationDelay = 0)
        {
            var bindable = new BindableStarDifficulty(beatmapInfo, cancellationToken)
            {
                // Start with an approximate known value instead of zero.
                Value = getInitialDifficulty(beatmapInfo, currentRuleset.Value, currentMods.Value)
            };

            updateBindable(bindable, currentRuleset.Value, currentMods.Value, cancellationToken, computationDelay);

            lock (bindableUpdateLock)
                trackedBindables.Add(bindable);

            return bindable;
        }

        /// <summary>
        /// Retrieves the difficulty of a <see cref="IBeatmapInfo"/>.
        /// </summary>
        /// <param name="beatmapInfo">The <see cref="IBeatmapInfo"/> to get the difficulty of.</param>
        /// <param name="rulesetInfo">The <see cref="IRulesetInfo"/> to get the difficulty with.</param>
        /// <param name="mods">The <see cref="Mod"/>s to get the difficulty with.</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken"/> which stops computing the star difficulty.</param>
        /// <param name="computationDelay">In the case a cached lookup was not possible, a value in milliseconds of to wait until performing potentially intensive lookup.</param>
        /// <returns>
        /// The requested <see cref="StarDifficulty"/>, if non-<see langword="null"/>.
        /// A <see langword="null"/> return value indicates that the difficulty process failed or was interrupted early,
        /// and as such there is no usable star difficulty value to be returned.
        /// </returns>
        public virtual Task<StarDifficulty?> GetDifficultyAsync(IBeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo = null, IEnumerable<Mod>? mods = null,
                                                                CancellationToken cancellationToken = default, int computationDelay = 0)
        {
            // In the case that the user hasn't given us a ruleset, use the beatmap's default ruleset.
            rulesetInfo ??= beatmapInfo.Ruleset;

            if (tryGetImmediateDifficulty(beatmapInfo, rulesetInfo, mods, out var immediateDifficulty))
                return Task.FromResult(immediateDifficulty);

            var localBeatmapInfo = beatmapInfo as BeatmapInfo;
            var localRulesetInfo = rulesetInfo as RulesetInfo;

            // Difficulty can only be computed if the beatmap and ruleset are locally available.
            if (localBeatmapInfo == null || localRulesetInfo == null)
            {
                // If not, fall back to the existing star difficulty (e.g. from an online source).
                return Task.FromResult<StarDifficulty?>(new StarDifficulty(beatmapInfo.StarRating, (beatmapInfo as IBeatmapOnlineInfo)?.MaxCombo ?? 0));
            }

            return GetAsync(new DifficultyCacheLookup(localBeatmapInfo, localRulesetInfo, mods), cancellationToken, computationDelay);
        }

        public bool TryGetCachedDifficulty(IBeatmapInfo beatmapInfo, out StarDifficulty? starDifficulty, IRulesetInfo? rulesetInfo = null, IEnumerable<Mod>? mods = null)
        {
            // In the case that the user hasn't given us a ruleset, use the beatmap's default ruleset.
            rulesetInfo ??= beatmapInfo.Ruleset;

            if (tryGetImmediateDifficulty(beatmapInfo, rulesetInfo, mods, out starDifficulty))
                return true;

            var localBeatmapInfo = beatmapInfo as BeatmapInfo;
            var localRulesetInfo = rulesetInfo as RulesetInfo;

            // Difficulty can only be computed if the beatmap and ruleset are locally available.
            if (localBeatmapInfo == null || localRulesetInfo == null)
            {
                starDifficulty = new StarDifficulty(beatmapInfo.StarRating, (beatmapInfo as IBeatmapOnlineInfo)?.MaxCombo ?? 0);
                return true;
            }

            return CheckExists(new DifficultyCacheLookup(localBeatmapInfo, localRulesetInfo, mods), out starDifficulty);
        }

        protected override Task<StarDifficulty?> ComputeValueAsync(DifficultyCacheLookup lookup, CancellationToken cancellationToken = default)
        {
            return Task.Factory.StartNew(() =>
            {
                if (CheckExists(lookup, out var existing))
                    return existing;

                return computeDifficulty(lookup, cancellationToken);
            }, cancellationToken, TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously, updateScheduler);
        }

        protected override bool CacheNullValues => false;

        public Task<List<TimedDifficultyAttributes>> GetTimedDifficultyAttributesAsync(IWorkingBeatmap beatmap, Ruleset ruleset, Mod[] mods, CancellationToken cancellationToken = default)
        {
            if (ruleset.ShortName == BmsStarRatingResolver.RulesetShortName)
                return Task.FromResult(new List<TimedDifficultyAttributes>());

            return Task.Factory.StartNew(() => ruleset.CreateDifficultyCalculator(beatmap).CalculateTimed(mods, cancellationToken),
                cancellationToken,
                TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously,
                updateScheduler);
        }

        /// <summary>
        /// Updates all tracked <see cref="BindableStarDifficulty"/> using the current ruleset and mods.
        /// </summary>
        private void updateTrackedBindables()
        {
            lock (bindableUpdateLock)
            {
                cancelTrackedBindableUpdate();

                foreach (var b in trackedBindables)
                {
                    var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(trackedUpdateCancellationSource.Token, b.CancellationToken);
                    linkedCancellationSources.Add(linkedSource);

                    updateBindable(b, currentRuleset.Value, currentMods.Value, linkedSource.Token);
                }
            }
        }

        /// <summary>
        /// Cancels the existing update of all tracked <see cref="BindableStarDifficulty"/> via <see cref="updateTrackedBindables"/>.
        /// </summary>
        private void cancelTrackedBindableUpdate()
        {
            lock (bindableUpdateLock)
            {
                debouncedModSettingsChange?.Cancel();
                debouncedModSettingsChange = null;

                trackedUpdateCancellationSource.Cancel();
                trackedUpdateCancellationSource = new CancellationTokenSource();

                foreach (var c in linkedCancellationSources)
                    c.Dispose();

                linkedCancellationSources.Clear();
            }
        }

        /// <summary>
        /// Updates the value of a <see cref="BindableStarDifficulty"/> with a given ruleset + mods.
        /// </summary>
        /// <param name="bindable">The <see cref="BindableStarDifficulty"/> to update.</param>
        /// <param name="rulesetInfo">The <see cref="IRulesetInfo"/> to update with.</param>
        /// <param name="mods">The <see cref="Mod"/>s to update with.</param>
        /// <param name="cancellationToken">A token that may be used to cancel this update.</param>
        /// <param name="computationDelay">In the case a cached lookup was not possible, a value in milliseconds of to wait until performing potentially intensive lookup.</param>
        private void updateBindable(BindableStarDifficulty bindable, IRulesetInfo? rulesetInfo, IEnumerable<Mod>? mods, CancellationToken cancellationToken = default, int computationDelay = 0)
        {
            // GetDifficultyAsync will fall back to existing data from IBeatmapInfo if not locally available
            // (contrary to GetAsync)
            GetDifficultyAsync(bindable.BeatmapInfo, rulesetInfo, mods, cancellationToken, computationDelay)
                .ContinueWith(task =>
                {
                    // We're on a threadpool thread, but we should exit back to the update thread so consumers can safely handle value-changed events.
                    Schedule(() =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        StarDifficulty? starDifficulty = task.GetResultSafely();

                        if (starDifficulty != null)
                            bindable.Value = starDifficulty.Value;
                    });
                }, cancellationToken);
        }

        /// <summary>
        /// Computes the difficulty defined by a <see cref="DifficultyCacheLookup"/> key, and stores it to the timed cache.
        /// </summary>
        /// <param name="key">The <see cref="DifficultyCacheLookup"/> that defines the computation parameters.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The <see cref="StarDifficulty"/>.</returns>
        private StarDifficulty? computeDifficulty(in DifficultyCacheLookup key, CancellationToken cancellationToken = default)
        {
            // In the case that the user hasn't given us a ruleset, use the beatmap's default ruleset.
            var beatmapInfo = key.BeatmapInfo;
            var rulesetInfo = key.Ruleset;

            // Captured outside the try so a definitive conversion failure can be persisted with the correct difficulty version.
            int difficultyVersion = 0;

            try
            {
                var ruleset = rulesetInfo.CreateInstance();
                Debug.Assert(ruleset != null);

                PlayableCachedWorkingBeatmap workingBeatmap = new PlayableCachedWorkingBeatmap(beatmapManager.GetWorkingBeatmap(key.BeatmapInfo));

                var difficultyCalculator = ruleset.CreateDifficultyCalculator(workingBeatmap);
                difficultyVersion = difficultyCalculator.Version;

                IBeatmap playableBeatmap = workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo, key.OrderedMods, cancellationToken);

                var difficulty = difficultyCalculator.Calculate(key.OrderedMods, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                persistConvertedStarRatingIfApplicable(key.BeatmapInfo, ruleset.RulesetInfo, key.OrderedMods, difficulty.StarRating, difficultyVersion);

                var performanceCalculator = ruleset.CreatePerformanceCalculator();
                if (performanceCalculator == null)
                    return new StarDifficulty(difficulty, new PerformanceAttributes());

                ScoreProcessor scoreProcessor = ruleset.CreateScoreProcessor();
                scoreProcessor.Mods.Value = key.OrderedMods;
                scoreProcessor.ApplyBeatmap(playableBeatmap);
                cancellationToken.ThrowIfCancellationRequested();

                ScoreInfo perfectScore = new ScoreInfo(key.BeatmapInfo, ruleset.RulesetInfo)
                {
                    Passed = true,
                    Accuracy = 1,
                    Mods = key.OrderedMods,
                    MaxCombo = scoreProcessor.MaximumCombo,
                    Combo = scoreProcessor.MaximumCombo,
                    TotalScore = scoreProcessor.MaximumTotalScore,
                    Statistics = scoreProcessor.MaximumStatistics,
                    MaximumStatistics = scoreProcessor.MaximumStatistics
                };

                var performance = performanceCalculator.Calculate(perfectScore, difficulty);
                cancellationToken.ThrowIfCancellationRequested();

                return new StarDifficulty(difficulty, performance);
            }
            catch (OperationCanceledException)
            {
                // no need to log, cancellations are expected as part of normal operation.
                return null;
            }
            catch (BeatmapInvalidForRulesetException invalidForRuleset)
            {
                if (rulesetInfo.Equals(beatmapInfo.Ruleset))
                    Logger.Error(invalidForRuleset, $"Failed to convert {beatmapInfo.OnlineID} to the beatmap's default ruleset ({beatmapInfo.Ruleset}).");
                else
                    // A cross-ruleset conversion being invalid is deterministic, so persist it as a failed converted star rating.
                    // This keeps behaviour consistent with the background reprocessor and avoids re-attempting it on every lookup.
                    persistConvertedStarRatingFailureIfApplicable(beatmapInfo, rulesetInfo, key.OrderedMods, difficultyVersion);

                return null;
            }
            catch (Exception unknownException)
            {
                Logger.Error(unknownException, "Failed to calculate beatmap difficulty");

                return null;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            modSettingChangeTracker?.Dispose();

            cancelTrackedBindableUpdate();
            updateScheduler.Dispose();
        }

        public readonly struct DifficultyCacheLookup : IEquatable<DifficultyCacheLookup>
        {
            public readonly BeatmapInfo BeatmapInfo;
            public readonly RulesetInfo Ruleset;
            public readonly Mod[] OrderedMods;

            public DifficultyCacheLookup(BeatmapInfo beatmapInfo, RulesetInfo? ruleset, IEnumerable<Mod>? mods)
            {
                BeatmapInfo = beatmapInfo;
                // In the case that the user hasn't given us a ruleset, use the beatmap's default ruleset.
                Ruleset = ruleset ?? BeatmapInfo.Ruleset;
                OrderedMods = mods?.OrderBy(m => m.Acronym).Select(mod => mod.DeepClone()).ToArray() ?? Array.Empty<Mod>();
            }

            public bool Equals(DifficultyCacheLookup other)
                => BeatmapInfo.Equals(other.BeatmapInfo)
                   && Ruleset.Equals(other.Ruleset)
                   && OrderedMods.SequenceEqual(other.OrderedMods);

            public override int GetHashCode()
            {
                var hashCode = new HashCode();

                hashCode.Add(BeatmapInfo.ID);
                hashCode.Add(Ruleset.ShortName);

                foreach (var mod in OrderedMods)
                    hashCode.Add(mod);

                return hashCode.ToHashCode();
            }
        }

        private class BindableStarDifficulty : Bindable<StarDifficulty>
        {
            public IBeatmapInfo BeatmapInfo;
            public readonly CancellationToken CancellationToken;

            public BindableStarDifficulty(IBeatmapInfo beatmapInfo, CancellationToken cancellationToken)
            {
                BeatmapInfo = beatmapInfo;
                CancellationToken = cancellationToken;
            }
        }

        private StarDifficulty getInitialDifficulty(IBeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo, IEnumerable<Mod>? mods)
            => rulesetInfo != null && tryGetImmediateDifficulty(beatmapInfo, rulesetInfo, mods, out var initialDifficulty) && initialDifficulty != null
                ? initialDifficulty.Value
                : new StarDifficulty(beatmapInfo.StarRating, 0);

        private static bool tryGetImmediateDifficulty(IBeatmapInfo beatmapInfo, IRulesetInfo rulesetInfo, IEnumerable<Mod>? mods, out StarDifficulty? starDifficulty)
        {
            if (rulesetInfo.ShortName == beatmapInfo.Ruleset.ShortName
                && BmsStarRatingResolver.IsBmsBeatmap(beatmapInfo))
            {
                starDifficulty = new StarDifficulty(BmsStarRatingResolver.ResolveOrDefault(beatmapInfo), (beatmapInfo as IBeatmapOnlineInfo)?.MaxCombo ?? 0);
                return true;
            }

            if (canUsePersistedConvertedStarRating(beatmapInfo, rulesetInfo, mods))
            {
                if (BmsStarRatingResolver.TryResolvePersistedConvertedStarRating(beatmapInfo, rulesetInfo, out double persistedConvertedStarRating))
                {
                    starDifficulty = new StarDifficulty(persistedConvertedStarRating, (beatmapInfo as IBeatmapOnlineInfo)?.MaxCombo ?? 0);
                    return true;
                }

                // Even when we couldn't resolve a successful converted star, if the persisted state is "current" then
                // the beatmap is known to have failed conversion (e.g., exceeded DifficultyCalculator's internal 10-second
                // budget). Return a fallback synchronously so the carousel never queues another async compute that would
                // re-hit the same slow conversion path on every filter operation requiring star-range lookup.
                if (beatmapInfo.Metadata is BeatmapMetadata metadata
                    && BmsPersistedMetadataResolver.HasCurrentConvertedStarRatingState(metadata, rulesetInfo))
                {
                    starDifficulty = new StarDifficulty(beatmapInfo.StarRating, (beatmapInfo as IBeatmapOnlineInfo)?.MaxCombo ?? 0);
                    return true;
                }
            }

            starDifficulty = null;
            return false;
        }

        /// <summary>
        /// Persists the freshly computed converted star rating to the BMS metadata payload.
        /// </summary>
        /// <remarks>
        /// Called from <see cref="computeDifficulty"/> on a thread-pool task with no ambient realm write transaction.
        /// The detached-metadata write only runs when the caller-supplied <paramref name="beatmapInfo"/> carries a
        /// non-managed <see cref="BeatmapMetadata"/>; otherwise we would write to a live realm row outside an active
        /// write transaction and throw. The realm-managed live persistence always happens via the explicit
        /// <see cref="RealmAccess.Write(System.Action{Realms.Realm})"/> below.
        /// </remarks>
        private void persistConvertedStarRatingIfApplicable(BeatmapInfo beatmapInfo, RulesetInfo rulesetInfo, IEnumerable<Mod> mods, double starRating, int difficultyVersion)
        {
            if (!canUsePersistedConvertedStarRating(beatmapInfo, rulesetInfo, mods))
                return;

            if (beatmapInfo.Metadata is BeatmapMetadata detachedMetadata && !detachedMetadata.IsManaged)
                BmsPersistedMetadataResolver.SetConvertedStarRating(detachedMetadata, rulesetInfo, starRating, difficultyVersion);

            realmAccess.Write(realm =>
            {
                if (realm.Find<BeatmapInfo>(beatmapInfo.ID) is not BeatmapInfo liveBeatmap)
                    return;

                BmsPersistedMetadataResolver.SetConvertedStarRating(liveBeatmap.Metadata, rulesetInfo, starRating, difficultyVersion);
            });
        }

        /// <summary>
        /// Persists a deterministic conversion failure (e.g. <see cref="BeatmapInvalidForRulesetException"/> from
        /// scratch-only / empty source charts, or the upstream calculator's 10s budget timeout) so it isn't retried
        /// on every star lookup. See <see cref="persistConvertedStarRatingIfApplicable"/> for the IsManaged guard
        /// rationale.
        /// </summary>
        private void persistConvertedStarRatingFailureIfApplicable(BeatmapInfo beatmapInfo, RulesetInfo rulesetInfo, IEnumerable<Mod> mods, int difficultyVersion)
        {
            if (!canUsePersistedConvertedStarRating(beatmapInfo, rulesetInfo, mods))
                return;

            if (beatmapInfo.Metadata is BeatmapMetadata detachedMetadata && !detachedMetadata.IsManaged)
                BmsPersistedMetadataResolver.SetConvertedStarRatingFailure(detachedMetadata, rulesetInfo, difficultyVersion);

            realmAccess.Write(realm =>
            {
                if (realm.Find<BeatmapInfo>(beatmapInfo.ID) is not BeatmapInfo liveBeatmap)
                    return;

                BmsPersistedMetadataResolver.SetConvertedStarRatingFailure(liveBeatmap.Metadata, rulesetInfo, difficultyVersion);
            });
        }

        /// <summary>
        /// Synchronously computes and persists the converted star rating for a BMS beatmap into its metadata, so that
        /// converted-ruleset display/sorting is import-time-ready (mirroring how the beatmap's native star rating is
        /// computed during import). This avoids the carousel falling back to the raw BMS play-level star and then
        /// re-sorting once the value is computed asynchronously.
        /// </summary>
        /// <remarks>
        /// Best-effort: any failure is logged and must never propagate to the caller (the import pipeline).
        /// Must be called from within an active realm write transaction that owns <paramref name="beatmapInfo"/>'s
        /// metadata, because it writes directly to the live metadata rather than opening its own nested write.
        /// </remarks>
        public void EnsureConvertedStarRatingPersisted(BeatmapInfo beatmapInfo, IWorkingBeatmap working)
        {
            if (!BmsStarRatingResolver.IsBmsBeatmap(beatmapInfo) || beatmapInfo.Metadata is not BeatmapMetadata metadata)
                return;

            if (rulesetStore.AvailableRulesets.SingleOrDefault(r => BmsPersistedMetadataResolver.SupportsConvertedStarRatings(r)) is not RulesetInfo targetRuleset)
                return;

            // Already current for this conversion/difficulty version - nothing to do.
            if (BmsPersistedMetadataResolver.HasCurrentConvertedStarRatingState(metadata, targetRuleset))
                return;

            int difficultyVersion;
            DifficultyCalculator calculator;

            try
            {
                calculator = targetRuleset.CreateInstance().CreateDifficultyCalculator(working);
                difficultyVersion = calculator.Version;
            }
            catch (Exception e)
            {
                Logger.Log($"Failed to set up converted difficulty calculation for {beatmapInfo}: {e}");
                return;
            }

            try
            {
                double starRating = calculator.Calculate().StarRating;
                BmsPersistedMetadataResolver.SetConvertedStarRating(metadata, targetRuleset, starRating, difficultyVersion);
            }
            catch (BeatmapInvalidForRulesetException)
            {
                // Deterministic non-convertibility: persist a failure so it is not retried on every import/startup.
                BmsPersistedMetadataResolver.SetConvertedStarRatingFailure(metadata, targetRuleset, difficultyVersion);
            }
            catch (OperationCanceledException)
            {
                // Calculate() is invoked without a cancellation token, so DifficultyCalculator's internal 10-second guard
                // timer is the only source of cancellation here. That is deterministic per beatmap (genuine slow conversion
                // that the upstream calculator refuses to wait for), so persist as failed rather than letting song-select
                // filter operations re-attempt the same slow async compute every time star-range filtering is active.
                BmsPersistedMetadataResolver.SetConvertedStarRatingFailure(metadata, targetRuleset, difficultyVersion);
            }
            catch (Exception e)
            {
                // Transient failure: do not persist, so the startup reprocessor can retry later.
                Logger.Log($"Failed to compute converted star rating for {beatmapInfo}: {e}");
            }
        }

        private static bool canUsePersistedConvertedStarRating(IBeatmapInfo beatmapInfo, IRulesetInfo rulesetInfo, IEnumerable<Mod>? mods)
            => BmsStarRatingResolver.IsBmsBeatmap(beatmapInfo)
               && BmsPersistedMetadataResolver.SupportsConvertedStarRatings(rulesetInfo)
               && (mods == null || !mods.Any());

        /// <summary>
        /// A working beatmap that caches its playable representation.
        /// This is intended as single-use for when it is guaranteed that the playable beatmap can be reused.
        /// </summary>
        private class PlayableCachedWorkingBeatmap : IWorkingBeatmap
        {
            private readonly IWorkingBeatmap working;
            private IBeatmap? playable;

            public PlayableCachedWorkingBeatmap(IWorkingBeatmap working)
            {
                this.working = working;
            }

            public IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods)
                => playable ??= working.GetPlayableBeatmap(ruleset, mods);

            public IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken cancellationToken)
                => playable ??= working.GetPlayableBeatmap(ruleset, mods, cancellationToken);

            IBeatmapInfo IWorkingBeatmap.BeatmapInfo => working.BeatmapInfo;
            bool IWorkingBeatmap.BeatmapLoaded => working.BeatmapLoaded;
            bool IWorkingBeatmap.TrackLoaded => working.TrackLoaded;
            IBeatmap IWorkingBeatmap.Beatmap => working.Beatmap;
            Texture IWorkingBeatmap.GetBackground() => working.GetBackground();
            Texture IWorkingBeatmap.GetPanelBackground() => working.GetPanelBackground();
            Waveform IWorkingBeatmap.Waveform => working.Waveform;
            Storyboard IWorkingBeatmap.Storyboard => working.Storyboard;
            ISkin IWorkingBeatmap.Skin => working.Skin;
            Track IWorkingBeatmap.Track => working.Track;
            Track IWorkingBeatmap.LoadTrack() => working.LoadTrack();
            Stream IWorkingBeatmap.GetStream(string storagePath) => working.GetStream(storagePath);
            void IWorkingBeatmap.BeginAsyncLoad() => working.BeginAsyncLoad();
            void IWorkingBeatmap.CancelAsyncLoad() => working.CancelAsyncLoad();
            void IWorkingBeatmap.PrepareTrackForPreview(bool looping, double offsetFromPreviewPoint) => working.PrepareTrackForPreview(looping, offsetFromPreviewPoint);
        }
    }
}
