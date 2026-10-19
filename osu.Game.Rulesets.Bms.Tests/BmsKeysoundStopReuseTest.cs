// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Audio;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsKeysoundStopReuseTest
    {
        [Test]
        public void TestImmediatePlaybackAfterStopReusesTheNowIdlePool()
        {
            using var store = new BmsKeysoundStore(2);
            store.Play(new BmsKeysoundSampleInfo("one.wav"), 0, 1);
            store.Play(new BmsKeysoundSampleInfo("two.wav"), 0, 2);
            Assert.That(store.ChannelPool.All(channel => channel.RequestedPlaying), Is.True);

            store.StopAllPlayback();
            store.Play(new BmsKeysoundSampleInfo("three.wav"), 0, 3);

            Assert.Multiple(() =>
            {
                Assert.That(store.ActualConcurrentChannels, Is.EqualTo(2), "Stop/seek made the existing channels idle in this same update.");
                Assert.That(store.ChannelPool.Count(channel => channel.RequestedPlaying), Is.EqualTo(1));
            });
        }
    }
}
