// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Audio;
using osu.Game.Rulesets.Bms.Diagnostics;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Screens.Play;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.Audio
{
    /// <summary>
    /// Plays BMS keysounds through a shared channel pool so dense charts do not create an unbounded number of independent sample players.
    /// </summary>
    public partial class BmsKeysoundStore : CompositeDrawable, IManiaKeysoundStore
    {
        private IBindable<bool>? gameplayPaused;
        private GameplayClockContainer? gameplayClockContainer;

        public const int MIN_CONCURRENT_CHANNELS = 1;

        public const int DEFAULT_CONCURRENT_CHANNELS = 32;

        public const int MAX_CONCURRENT_CHANNELS = 256;

        public int ConcurrentChannels
        {
            get => desiredConcurrentChannels == 0 ? channels.Count : desiredConcurrentChannels;
            set => updateConcurrentChannels(Math.Clamp(value, MIN_CONCURRENT_CHANNELS, MAX_CONCURRENT_CHANNELS));
        }

        private readonly Container<BmsKeysoundChannel> channels = new Container<BmsKeysoundChannel>();

        // Channels currently known to be idle, rebuilt once per frame (reclaimIdleChannels). getNextChannel drains
        // this first so a still-playing sample is never recycled while an idle channel exists. Pre-sized to the hard
        // channel cap so the per-frame rebuild never reallocates on the gameplay hot path.
        private readonly Stack<BmsKeysoundChannel> freeChannels = new Stack<BmsKeysoundChannel>(MAX_CONCURRENT_CHANNELS);

        // Maps a BMS WAV slot (#WAVxx / KeysoundId) to the channel last playing it, for per-WAV (per-keysound) monophony:
        // re-triggering a slot that is still sounding reuses its channel so it cleanly restarts (cuts the prior instance)
        // instead of stacking overlapping copies and starving the pool. Keyed by the WAV SLOT, NOT the filename — so a
        // chart that duplicates one audio file across several #WAV slots for intentional overlap keeps overlapping
        // (matches LR2/beatoraja). Stale entries are harmless — they fail the "still busy with this slot" guard in
        // getChannelForCutGroup and fall back to a new channel. Bounded by the chart's distinct WAV-slot count.
        private readonly Dictionary<int, BmsKeysoundChannel> activeSampleChannels = new Dictionary<int, BmsKeysoundChannel>();

        private int nextChannelIndex;
        private int desiredConcurrentChannels;

        // The channel-selection outcome of the most recent play, recorded into the playback log for diagnostics.
        // Set by the selection methods (getNextChannel / getChannelForCutGroup) on every play; a single field write
        // with no allocation, so it stays cheap on the dense-chart hot path (P1-J #8) whether or not logging is on.
        private KeysoundPlaybackDecision lastDecision;

        // Opt-in, test-only cumulative record of every play (slot, decision, channel, filename, time). Null in
        // production -> recordPlayback is a single null-check with no allocation (so the diagnostic hook never taxes
        // the gameplay hot path). A test enables it on the resolved store before advancing the clock, then reads the
        // sequence back to characterise per-WAV cut / saturation-steal / silence without timing races.
        private List<KeysoundPlaybackRecord>? playbackLog;

        internal int ActualConcurrentChannels => channels.Count;

        internal IEnumerable<PausableSkinnableSound> ChannelPool => channels;

        internal void ApplyPendingChannelResize() => trimExcessChannels();

        internal void ReclaimIdleChannelsForTesting() => reclaimIdleChannels();

        internal void EnablePlaybackLogForTesting() => playbackLog ??= new List<KeysoundPlaybackRecord>();

        internal void ClearPlaybackLogForTesting() => playbackLog?.Clear();

        internal IReadOnlyList<KeysoundPlaybackRecord> PlaybackLogForTesting => playbackLog ?? (IReadOnlyList<KeysoundPlaybackRecord>)Array.Empty<KeysoundPlaybackRecord>();

        // Diagnostic counters (P1-J J6 perf hunt; read by BmsGameplayStallDiagnostics). Cheap: a HashSet add + two
        // increments on the gameplay keysound path. seenCutGroups tracks which WAV slots have played, so the FIRST play
        // of each slot — the one that may trigger a cold sample decode — can be counted via ColdKeysoundFirstPlayCount.
        private readonly HashSet<int> seenCutGroups = new HashSet<int>();
        internal long ColdKeysoundFirstPlayCount { get; private set; }
        internal long TotalKeysoundPlays { get; private set; }
        internal int DistinctKeysoundSlotsPlayed => seenCutGroups.Count;

        // Managed bytes allocated inside the keysound Play path (update thread), to attribute the gameplay allocation
        // rate that BmsGameplayStallDiagnostics measures. Confirms whether the per-play SkinnableSound.updateSamples
        // churn (sample-drawable rebuild on every Samples reassignment) is the dominant allocator vs mania per-frame work.
        internal long PlayPathAllocatedBytes { get; private set; }

        public BmsKeysoundStore(int concurrentChannels = DEFAULT_CONCURRENT_CHANNELS)
        {
            AddInternal(channels);

            // Long-term diagnostics seam: silent unless real gameplay stalls / GCs (see BmsGameplayStallDiagnostics).
            AddInternal(new BmsGameplayStallDiagnostics(this));

            ConcurrentChannels = concurrentChannels;
        }

        [BackgroundDependencyLoader(true)]
        private void load(GameplayClockContainer? gameplayClockContainer)
        {
            this.gameplayClockContainer = gameplayClockContainer;

            if (gameplayClockContainer == null)
                return;

            gameplayPaused = gameplayClockContainer.IsPaused.GetBoundCopy();
            gameplayPaused.BindValueChanged(paused =>
            {
                if (paused.NewValue)
                    StopAllPlayback();
            });

            gameplayClockContainer.OnSeek += StopAllPlayback;
        }

        public void Play(IEnumerable<ISampleInfo> sampleInfos, double balance)
        {
            if (sampleInfos is ISampleInfo[] sampleArray)
            {
                Play(sampleArray, balance);
                return;
            }

            var samples = sampleInfos.ToArray();

            Play(samples, balance);
        }

        /// <summary>
        /// Plays a single keysound on a channel chosen for its BMS WAV slot (<paramref name="cutGroup"/>), so that
        /// re-triggering the same still-sounding slot restarts it (per-WAV cut) rather than stacking a copy.
        /// </summary>
        public void Play(ISampleInfo sampleInfo, double balance, int cutGroup)
        {
            long allocBefore = GC.GetAllocatedBytesForCurrentThread();

            TotalKeysoundPlays++;
            if (seenCutGroups.Add(cutGroup))
                ColdKeysoundFirstPlayCount++;

            var channel = getChannelForCutGroup(cutGroup);
            channel.CurrentCutGroup = cutGroup;
            activeSampleChannels[cutGroup] = channel;
            recordPlayback(cutGroup, channel, sampleInfo);
            channel.PlaySingleSample(sampleInfo, balance);

            PlayPathAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocBefore;
        }

        /// <summary>
        /// Plays a single keysound without a WAV-slot cut group (always a fresh channel). For callers/tests that do
        /// not carry a slot id; gameplay paths use the <c>cutGroup</c> overload.
        /// </summary>
        public void Play(ISampleInfo sampleInfo, double balance)
        {
            long allocBefore = GC.GetAllocatedBytesForCurrentThread();

            TotalKeysoundPlays++;

            var channel = getNextChannel();
            channel.CurrentCutGroup = null;
            recordPlayback(null, channel, sampleInfo);
            channel.PlaySingleSample(sampleInfo, balance);

            PlayPathAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocBefore;
        }

        // IManiaKeysoundStore: lets a pooled mania DrawableNote (a converted KEY note) play its keysound through this
        // shared store (per-WAV cut, pause/seek aware) without the mania assembly referencing this type. Bridges to the
        // cut-group / no-cut single-sample overloads (J6 / P1-J #10).
        void IManiaKeysoundStore.Play(ISampleInfo sample, double balance, int? cutGroup)
        {
            if (cutGroup is int group)
                Play(sample, balance, group);
            else
                Play(sample, balance);
        }

        public void Play(ISampleInfo[] sampleInfos, double balance)
        {
            if (sampleInfos.Length == 0)
                return;

            long allocBefore = GC.GetAllocatedBytesForCurrentThread();

            TotalKeysoundPlays++;

            var channel = getNextChannel();
            channel.CurrentCutGroup = null;
            recordPlayback(null, channel, sampleInfos[0]);
            channel.PlaySampleArray(sampleInfos, balance);

            PlayPathAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocBefore;
        }

        public void StopAllPlayback()
        {
            foreach (var channel in channels)
                channel.Stop();

            nextChannelIndex = 0;
        }

        private BmsKeysoundChannel getChannelForCutGroup(int cutGroup)
        {
            // Per-WAV cut: if this WAV slot is still sounding on its channel, reuse that channel so the re-trigger
            // restarts the single voice instead of allocating a second overlapping copy. The busy guard
            // (`!isChannelAvailable`) plus the CurrentCutGroup match confirms the channel is still that very slot.
            if (activeSampleChannels.TryGetValue(cutGroup, out var existing)
                && !existing.Retired
                && !isChannelAvailable(existing)
                && existing.CurrentCutGroup == cutGroup)
            {
                lastDecision = KeysoundPlaybackDecision.PerWavCutReuse;
                return existing;
            }

            return getNextChannel();
        }

        private BmsKeysoundChannel getNextChannel()
        {
            int selectableChannels = Math.Min(ConcurrentChannels, channels.Count);

            if (selectableChannels == 0)
                throw new InvalidOperationException("BMS keysound playback requires at least one channel.");

            // Prefer a channel known to be idle so a long sample is never cut while idle channels still exist. The
            // free set is reconciled once per frame; entries stay valid until popped because only playback (which
            // pops first) makes a pooled channel busy. The retired/availability guards are cheap defensive checks.
            while (freeChannels.TryPop(out var freeChannel))
            {
                if (!freeChannel.Retired && isChannelAvailable(freeChannel))
                {
                    lastDecision = KeysoundPlaybackDecision.IdleChannel;
                    return freeChannel;
                }
            }

            // Every channel is busy (genuine polyphony saturation): steal in rotation, which approximates oldest-first
            // and stays O(1) on the dense-chart hot path rather than rescanning the whole pool per trigger.
            nextChannelIndex %= selectableChannels;

            lastDecision = KeysoundPlaybackDecision.RotationSteal;
            var channel = channels[nextChannelIndex];
            nextChannelIndex = (nextChannelIndex + 1) % selectableChannels;
            return channel;
        }

        // Appends one entry to the test-only playback log (no-op + zero allocation when logging is off). Captures the
        // WAV slot (cut group), the channel-selection outcome (lastDecision), the channel index and the sample's
        // filename so a harness can replay exactly what the store did for each trigger.
        private void recordPlayback(int? cutGroup, BmsKeysoundChannel channel, ISampleInfo sampleInfo)
        {
            if (playbackLog == null)
                return;

            int channelIndex = -1;

            for (int i = 0; i < channels.Count; i++)
            {
                if (ReferenceEquals(channels[i], channel))
                {
                    channelIndex = i;
                    break;
                }
            }

            string? filename = (sampleInfo as BmsKeysoundSampleInfo)?.Filename ?? sampleInfo.LookupNames.FirstOrDefault();

            // Prefer the gameplay clock time so log entries line up with hit-object event times for diagnosis; the
            // store's own Drawable clock can run above the frame-stability boundary (≈ wall clock) and would not.
            double time = gameplayClockContainer?.CurrentTime ?? Time.Current;

            playbackLog.Add(new KeysoundPlaybackRecord(time, cutGroup, lastDecision, channelIndex, filename));
        }

        protected override void Update()
        {
            base.Update();

            ApplyPendingChannelResize();
            reclaimIdleChannels();
        }

        // Rebuilds the idle-channel free set for the current frame. Channels popped and played during the frame turn
        // busy and naturally drop out of the next rebuild, so truncation only happens under genuine saturation. O(N)
        // reads with no allocation (Clear retains capacity, Push stays within the pre-sized bound).
        private void reclaimIdleChannels()
        {
            freeChannels.Clear();

            int selectableChannels = Math.Min(ConcurrentChannels, channels.Count);

            // Push high-to-low so the lowest-index idle channel is popped first (stable, predictable allocation).
            for (int i = selectableChannels - 1; i >= 0; i--)
            {
                var channel = channels[i];

                if (!channel.Retired && isChannelAvailable(channel))
                    freeChannels.Push(channel);
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            gameplayPaused?.UnbindAll();

            if (gameplayClockContainer != null)
                gameplayClockContainer.OnSeek -= StopAllPlayback;

            base.Dispose(isDisposing);
        }

        private void updateConcurrentChannels(int concurrentChannels)
        {
            if (ConcurrentChannels == concurrentChannels && channels.Count <= concurrentChannels)
                return;

            desiredConcurrentChannels = concurrentChannels;

            while (channels.Count < concurrentChannels)
                channels.Add(createChannel());

            nextChannelIndex = ConcurrentChannels == 0 ? 0 : nextChannelIndex % ConcurrentChannels;
            ApplyPendingChannelResize();
        }

        private void trimExcessChannels()
        {
            for (int i = channels.Count - 1; i >= ConcurrentChannels; i--)
            {
                var channel = channels[i];

                if (!isChannelAvailable(channel))
                    continue;

                // Mark retired before removal so any lingering free-set reference is skipped, then dispose so the
                // shrink actually reclaims the channel instead of leaking a detached, undisposed sound drawable.
                channel.Retired = true;
                channels.Remove(channel, true);
            }
        }

        private static bool isChannelAvailable(BmsKeysoundChannel channel)
            => channel.LoadState >= LoadState.Ready ? !channel.IsPlaying : !channel.RequestedPlaying;

        private static BmsKeysoundChannel createChannel()
            => new BmsKeysoundChannel
            {
                MinimumSampleVolume = DrawableHitObject.MINIMUM_SAMPLE_VOLUME,
            };

        private sealed partial class BmsKeysoundChannel : PausableSkinnableSound
        {
            // Set when the channel has been trimmed out of the pool and disposed; guards against reuse of a stale
            // free-set reference after a live channel-count shrink.
            internal bool Retired;

            // The BMS WAV slot (#WAVxx / KeysoundId) this channel is currently assigned (null for the no-cut / multi
            // sample paths). Used with the busy guard to implement per-WAV cut: a re-trigger of the same still-sounding
            // slot restarts this channel rather than spawning an overlapping copy.
            internal int? CurrentCutGroup;

            private readonly ISampleInfo[][] singleSampleBuffers =
            {
                new ISampleInfo[1],
                new ISampleInfo[1],
            };

            private int nextSingleSampleBufferIndex;

            // The single sample currently applied to this channel via PlaySingleSample. Lets a re-trigger of the SAME
            // keysound on the same channel skip the Samples reassignment entirely: assigning a new array reference runs
            // SkinnableSound.updateSamples — a full sample-drawable teardown/rebuild (RemoveAll + Clear +
            // GetPooledSample + Add, measured ~30KB of mid-lived allocation per play, plus a whole new drawable
            // construction whenever the pool's instance is still attached elsewhere). Per-WAV cut pins a slot to its
            // channel and the converter memoizes one BmsKeysoundSampleInfo per slot, so same-slot re-triggers — the
            // dominant gameplay path — hit this fast path and become a plain Stop+Play restart (cut semantics
            // unchanged, matching how a native mania note replays its persistent loaded sound). This churn was the main
            // converted-BMS per-trigger allocation driver behind the dense-section gen1 promotion storm (P1-J).
            private ISampleInfo? currentSingleSample;

            public void PlaySingleSample(ISampleInfo sampleInfo, double balance)
            {
                Balance.Value = balance;

                if (!EqualityComparer<ISampleInfo?>.Default.Equals(currentSingleSample, sampleInfo))
                {
                    var sampleBuffer = singleSampleBuffers[nextSingleSampleBufferIndex];
                    nextSingleSampleBufferIndex = (nextSingleSampleBufferIndex + 1) % singleSampleBuffers.Length;

                    sampleBuffer[0] = sampleInfo;
                    Samples = sampleBuffer;
                    currentSingleSample = sampleInfo;
                }

                Play();
            }

            public void PlaySampleArray(ISampleInfo[] sampleInfos, double balance)
            {
                Balance.Value = balance;

                // Invalidate the single-sample fast path: Samples now holds an arbitrary array, so a future
                // PlaySingleSample must reassign rather than assume the channel still carries its last single keysound.
                currentSingleSample = null;

                Samples = sampleInfos;
                Play();
            }
        }
    }

    /// <summary>
    /// The channel-selection outcome of a single <see cref="BmsKeysoundStore"/> play, captured for diagnostics.
    /// </summary>
    internal enum KeysoundPlaybackDecision
    {
        /// <summary>A free (idle) channel was taken from the per-frame free set; nothing was cut.</summary>
        IdleChannel,

        /// <summary>Per-WAV cut: the channel still sounding this WAV slot was reused, restarting (cutting) it.</summary>
        PerWavCutReuse,

        /// <summary>Pool saturated (every channel busy): a channel was stolen in rotation, truncating whatever it played.</summary>
        RotationSteal,
    }

    /// <summary>
    /// One entry in <see cref="BmsKeysoundStore"/>'s test-only playback log: what the store did for a single trigger.
    /// </summary>
    internal readonly record struct KeysoundPlaybackRecord(double Time, int? CutGroup, KeysoundPlaybackDecision Decision, int ChannelIndex, string? Filename);
}
