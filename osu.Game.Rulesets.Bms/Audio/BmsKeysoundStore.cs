// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Audio;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.Audio
{
    /// <summary>
    /// Plays BMS keysounds through a shared channel pool so dense charts do not create an unbounded number of independent sample players.
    /// </summary>
    public partial class BmsKeysoundStore : CompositeDrawable
    {
        public const int MIN_CONCURRENT_CHANNELS = 1;

        public const int DEFAULT_CONCURRENT_CHANNELS = 16;

        public const int MAX_CONCURRENT_CHANNELS = 256;

        public int ConcurrentChannels
        {
            get => channels.Count;
            set => updateConcurrentChannels(Math.Clamp(value, MIN_CONCURRENT_CHANNELS, MAX_CONCURRENT_CHANNELS));
        }

        private readonly Container<PausableSkinnableSound> channels = new Container<PausableSkinnableSound>();

        private int nextChannelIndex;

        public BmsKeysoundStore(int concurrentChannels = DEFAULT_CONCURRENT_CHANNELS)
        {
            InternalChild = channels;

            ConcurrentChannels = concurrentChannels;
        }

        public void Play(IEnumerable<ISampleInfo> sampleInfos, double balance)
        {
            var samples = sampleInfos.ToArray();

            if (samples.Length == 0)
                return;

            Schedule(() =>
            {
                var channel = getNextChannel();
                channel.Balance.Value = balance;
                channel.Samples = samples;
                channel.Play();
            });
        }

        private PausableSkinnableSound getNextChannel()
        {
            if (channels.Count == 0)
                throw new InvalidOperationException("BMS keysound playback requires at least one channel.");

            var channel = channels[nextChannelIndex];
            nextChannelIndex = (nextChannelIndex + 1) % channels.Count;
            return channel;
        }

        private void updateConcurrentChannels(int concurrentChannels)
        {
            if (channels.Count == concurrentChannels)
                return;

            nextChannelIndex = 0;
            channels.Clear();

            for (int i = 0; i < concurrentChannels; i++)
            {
                channels.Add(new PausableSkinnableSound
                {
                    MinimumSampleVolume = DrawableHitObject.MINIMUM_SAMPLE_VOLUME,
                });
            }
        }
    }
}
