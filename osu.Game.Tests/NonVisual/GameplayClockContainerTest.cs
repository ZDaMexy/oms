// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Timing;
using osu.Game.Screens.Play;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public partial class GameplayClockContainerTest
    {
        [TestCase(0)]
        [TestCase(1)]
        public void TestTrueGameplayRateWithGameplayAdjustment(double underlyingClockRate)
        {
            var trackVirtual = new TrackVirtual(60000) { Frequency = { Value = underlyingClockRate } };
            var gameplayClock = new TestGameplayClockContainer(trackVirtual);

            Assert.That(gameplayClock.GetTrueGameplayRate(), Is.EqualTo(2));
        }

        [Test]
        public void TestStoppedStartRequestCannotRestartTheClockDuringVisualOnlyFailure()
        {
            using var clock = new DeferredStartProbe();
            clock.Start();
            clock.Stop();
            clock.SoftUnpause();
            clock.DrainStartRequests();
            Assert.That(clock.IsPaused.Value, Is.False, "The stopped chart must still draw its native failure outro.");
            Assert.That(clock.SourceStarts, Is.Zero, "SoftUnpause cannot resurrect the preceding source-clock request.");

            clock.Stop();
            clock.Start();
            clock.DrainStartRequests();
            Assert.That(clock.SourceStarts, Is.EqualTo(1), "A later explicit start remains valid.");
        }

        private partial class DeferredStartProbe : GameplayClockContainer
        {
            public int SourceStarts { get; private set; }

            public DeferredStartProbe()
                : base(new StopwatchClock(), false, false)
            {
            }

            public void DrainStartRequests() => SchedulerAfterChildren.Update();

            protected override void StartGameplayClock() => SourceStarts++;

            protected override void StopGameplayClock()
            {
            }
        }

        private partial class TestGameplayClockContainer : GameplayClockContainer
        {
            public TestGameplayClockContainer(IClock underlyingClock)
                : base(underlyingClock, false, false)
            {
                AdjustmentsFromMods.AddAdjustment(AdjustableProperty.Frequency, new BindableDouble(2.0));
            }
        }
    }
}
