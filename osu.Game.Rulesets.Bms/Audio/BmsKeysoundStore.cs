// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Audio;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Screens.Play;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.Audio
{
    /// <summary>
    /// Plays BMS keysounds through a shared channel pool so dense charts do not create an unbounded number of independent sample players.
    /// </summary>
    public partial class BmsKeysoundStore : CompositeDrawable
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

        internal int ActualConcurrentChannels => channels.Count;

        internal IEnumerable<PausableSkinnableSound> ChannelPool => channels;

        internal void ApplyPendingChannelResize() => trimExcessChannels();

        internal void ReclaimIdleChannelsForTesting() => reclaimIdleChannels();

        public BmsKeysoundStore(int concurrentChannels = DEFAULT_CONCURRENT_CHANNELS)
        {
            InternalChild = channels;

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
            var channel = getChannelForCutGroup(cutGroup);
            channel.CurrentCutGroup = cutGroup;
            activeSampleChannels[cutGroup] = channel;
            channel.PlaySingleSample(sampleInfo, balance);
        }

        /// <summary>
        /// Plays a single keysound without a WAV-slot cut group (always a fresh channel). For callers/tests that do
        /// not carry a slot id; gameplay paths use the <c>cutGroup</c> overload.
        /// </summary>
        public void Play(ISampleInfo sampleInfo, double balance)
        {
            var channel = getNextChannel();
            channel.CurrentCutGroup = null;
            channel.PlaySingleSample(sampleInfo, balance);
        }

        public void Play(ISampleInfo[] sampleInfos, double balance)
        {
            if (sampleInfos.Length == 0)
                return;

            var channel = getNextChannel();
            channel.CurrentCutGroup = null;
            channel.Balance.Value = balance;
            channel.Samples = sampleInfos;
            channel.Play();
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
                return existing;

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
                    return freeChannel;
            }

            // Every channel is busy (genuine polyphony saturation): steal in rotation, which approximates oldest-first
            // and stays O(1) on the dense-chart hot path rather than rescanning the whole pool per trigger.
            nextChannelIndex %= selectableChannels;

            var channel = channels[nextChannelIndex];
            nextChannelIndex = (nextChannelIndex + 1) % selectableChannels;
            return channel;
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

            public void PlaySingleSample(ISampleInfo sampleInfo, double balance)
            {
                Balance.Value = balance;

                var sampleBuffer = singleSampleBuffers[nextSingleSampleBufferIndex];
                nextSingleSampleBufferIndex = (nextSingleSampleBufferIndex + 1) % singleSampleBuffers.Length;

                sampleBuffer[0] = sampleInfo;
                Samples = sampleBuffer;
                Play();
            }
        }
    }
}
