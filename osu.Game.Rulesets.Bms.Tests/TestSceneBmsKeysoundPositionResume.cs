// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ManagedBass;
using ManagedBass.Mix;
using NUnit.Framework;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Audio;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using SampleInfo = osu.Game.Audio.SampleInfo;

namespace osu.Game.Rulesets.Bms.Tests
{
    /// <summary>
    /// Reads actual BASS sample positions, rather than treating retained playback requests as proof of resume.
    /// The native handle access is deliberately confined to this test for the pinned framework version:
    /// SampleChannel does not expose a public position API. Production only uses frequency adjustments.
    /// </summary>
    [HeadlessTest]
    public partial class TestSceneBmsKeysoundPositionResume : OsuTestScene
    {
        private GameplayClockContainer gameplay = null!;
        private BmsKeysoundStore store = null!;
        private ISampleStore samples = null!;
        private SampleChannel playingChannel = null!;
        private int handle;
        private long pausedPosition;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            samples = Audio.GetSampleStore(new WaveStore());
            Child = new SkinProvidingContainer(new AudioSkin(samples))
            {
                RelativeSizeAxes = Axes.Both,
                Child = gameplay = new GameplayClockContainer(new StopwatchClock(), applyOffsets: false, requireDecoupling: false)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = store = new BmsKeysoundStore(2),
                },
            };
        });

        // NUnit TearDown runs before TestScene's AfterTest executes the visual steps. Scheduling Clear
        // there would detach gameplay before its deferred StartGameplayClock can run.
        [TearDownSteps]
        public void TearDown() => AddStep("release audio fixture", () =>
        {
            Clear();
            samples.Dispose();
        });

        [TestCase(1)]
        [TestCase(1.5)]
        public void TestPausePreservesNativePositionAndResumeUsesSameChannel(double rate)
        {
            startSample(rate);
            AddUntilStep("real audio has progressed", () => Bass.ChannelGetPosition(handle) > 44100);
            AddStep("pause gameplay", () => gameplay.Stop());
            AddUntilStep("native mixer paused", () => (BassMix.ChannelFlags(handle, 0, 0) & BassFlags.MixerChanPause) != 0);
            AddStep("capture paused position", () => pausedPosition = Bass.ChannelGetPosition(handle));
            AddWaitStep("remain paused", 5);
            AddAssert("native position remains exact", () => Bass.ChannelGetPosition(handle), () => Is.EqualTo(pausedPosition));
            AddAssert("paused channel remains owned", () => getActiveChannel(), () => Is.SameAs(playingChannel));
            AddStep("resume gameplay", () => gameplay.Start());
            AddUntilStep("native position continues beyond pause", () => Bass.ChannelGetPosition(handle) > pausedPosition);
            AddAssert("resume did not create a fresh channel", () => getActiveChannel(), () => Is.SameAs(playingChannel));
            AddAssert("original playback rate restored", () => playingChannel.AggregateFrequency.Value, () => Is.EqualTo(rate));
        }

        [Test]
        public void TestSeekWhilePausedDiscardsOldVoiceInsteadOfResumingIt()
        {
            startSample(1);
            AddUntilStep("real audio has progressed", () => Bass.ChannelGetPosition(handle) > 44100);
            AddStep("pause", () => gameplay.Stop());
            AddUntilStep("native mixer paused", () => (BassMix.ChannelFlags(handle, 0, 0) & BassFlags.MixerChanPause) != 0);
            AddStep("seek", () => gameplay.Seek(5000));
            AddUntilStep("old request discarded", () => store.ChannelPool.All(c => !c.RequestedPlaying));
            AddStep("resume", () => gameplay.Start());
            AddWaitStep("settle audio", 5);
            AddAssert("old voice cannot resume after seek", () => !playingChannel.Playing);
        }

        private void startSample(double rate)
        {
            AddUntilStep("store ready", () => store.ChannelPool.All(c => c.LoadState >= LoadState.Ready));
            AddStep("start clock and set rate", () =>
            {
                foreach (var channel in store.ChannelPool)
                    channel.Frequency.Value = rate;
                gameplay.Start();
            });
            AddUntilStep("clock running", () => gameplay.IsRunning);
            AddStep("play generated long WAV", () => store.Play(new SampleInfo("long.wav"), 0, 1));
            AddUntilStep("real channel created", () => getActiveChannel() != null);
            AddStep("read native sample handle", () =>
            {
                playingChannel = getActiveChannel()!;
                Assert.That(playingChannel.GetType().Name, Is.EqualTo("SampleChannelBass"));
            });
            AddUntilStep("native handle allocated", () =>
            {
                handle = (int)playingChannel.GetType().GetField("channel", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(playingChannel)!;
                return handle != 0;
            });
        }

        private SampleChannel? getActiveChannel()
            => store.ChildrenOfType<PoolableSkinnableSample>()
                    .Select(s => (SampleChannel?)typeof(PoolableSkinnableSample).GetField("activeChannel", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(s))
                    .SingleOrDefault(c => c != null);

        private sealed class AudioSkin : Skin
        {
            private readonly ISampleStore samples;

            public AudioSkin(ISampleStore samples)
                : base(new SkinInfo(name: nameof(AudioSkin)), null)
            {
                this.samples = samples;
            }

            public override ISample? GetSample(ISampleInfo sampleInfo) => samples.Get("long.wav");
            public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup) => null;
            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;
            public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup) => null;
        }

        private sealed class WaveStore : IResourceStore<byte[]>
        {
            private readonly byte[] wave = createWave();
            public byte[] Get(string name) => wave;
            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(wave);
            public Stream GetStream(string name) => new MemoryStream(wave, false);
            public IEnumerable<string> GetAvailableResources() => new[] { "long.wav" };
            public void Dispose() { }

            private static byte[] createWave()
            {
                const int samples = 44100 * 30;
                using var stream = new MemoryStream();
                using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + samples * 2);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(44100);
                writer.Write(88200);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(samples * 2);
                for (int i = 0; i < samples; i++)
                    writer.Write((short)(Math.Sin(i * 2 * Math.PI * 440 / 44100) * 1000));
                return stream.ToArray();
            }
        }
    }
}
