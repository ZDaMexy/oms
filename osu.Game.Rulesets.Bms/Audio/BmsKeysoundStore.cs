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

        private int nextChannelIndex;
        private int desiredConcurrentChannels;

        internal int ActualConcurrentChannels => channels.Count;

        internal IEnumerable<PausableSkinnableSound> ChannelPool => channels;

        internal void ApplyPendingChannelResize() => trimExcessChannels();

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

        public void Play(ISampleInfo sampleInfo, double balance)
        {
            var channel = getNextChannel();
            channel.PlaySingleSample(sampleInfo, balance);
        }

        public void Play(ISampleInfo[] sampleInfos, double balance)
        {
            if (sampleInfos.Length == 0)
                return;

            var channel = getNextChannel();
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

        private BmsKeysoundChannel getNextChannel()
        {
            int selectableChannels = Math.Min(ConcurrentChannels, channels.Count);

            if (selectableChannels == 0)
                throw new InvalidOperationException("BMS keysound playback requires at least one channel.");

            nextChannelIndex %= selectableChannels;

            var channel = channels[nextChannelIndex];
            nextChannelIndex = (nextChannelIndex + 1) % selectableChannels;
            return channel;
        }

        protected override void Update()
        {
            base.Update();

            ApplyPendingChannelResize();
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

                if (!canRemoveChannel(channel))
                    continue;

                channels.Remove(channel, false);
            }
        }

        private static bool canRemoveChannel(PausableSkinnableSound channel)
            => channel.LoadState >= LoadState.Ready ? !channel.IsPlaying : !channel.RequestedPlaying;

        private static BmsKeysoundChannel createChannel()
            => new BmsKeysoundChannel
            {
                MinimumSampleVolume = DrawableHitObject.MINIMUM_SAMPLE_VOLUME,
            };

        private sealed partial class BmsKeysoundChannel : PausableSkinnableSound
        {
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
