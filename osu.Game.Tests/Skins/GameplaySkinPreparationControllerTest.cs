// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using NUnit.Framework;
using osu.Game.Screens.Play;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    public class GameplaySkinPreparationControllerTest
    {
        [Test]
        public void TestOrdinarySkinHasNoPreparationStateOrTimer()
        {
            var controller = new GameplaySkinPreparationController();
            controller.Configure(0);

            Assert.Multiple(() =>
            {
                Assert.That(controller.IsConfigured, Is.False);
                Assert.That(controller.State, Is.EqualTo(default(GameplaySkinPreparationStateSnapshot)));
                Assert.That(controller.Advance(5000, false), Is.False);
                Assert.Throws<InvalidOperationException>(controller.Begin);
            });
        }

        [Test]
        public void TestReadyUsesTheLastOneAndHalfSecondsAndPauseFreezesBothClocks()
        {
            GameplaySkinPreparationController controller = createController();
            Assert.That(controller.State.Available, Is.True);
            Assert.That(controller.State.Ready, Is.False);
            Assert.That(controller.Advance(3500, false), Is.False);

            GameplaySkinPreparationStateSnapshot ready = controller.State;
            Assert.Multiple(() =>
            {
                Assert.That(ready.Elapsed, Is.EqualTo(3500));
                Assert.That(ready.Remaining, Is.EqualTo(1500));
                Assert.That(ready.Ready, Is.True);
                Assert.That(ready.ReadyElapsed, Is.Zero);
            });

            Assert.That(controller.Advance(20000, true), Is.False);
            Assert.That(controller.State, Is.EqualTo(ready));
            Assert.That(controller.Advance(750, false), Is.False);
            Assert.That(controller.State.ReadyElapsed, Is.EqualTo(750));
            Assert.That(controller.Advance(750, false), Is.True);

            controller.Complete();
            Assert.Multiple(() =>
            {
                Assert.That(controller.State.Available, Is.True);
                Assert.That(controller.State.Active, Is.False);
                Assert.That(controller.State.Elapsed, Is.EqualTo(5000));
                Assert.That(controller.State.Remaining, Is.Zero);
                Assert.That(controller.State.Ready, Is.False);
                Assert.That(controller.State.ReadyElapsed, Is.Zero);
                Assert.That(controller.Advance(5000, false), Is.False);
                Assert.That(controller.State.Elapsed, Is.EqualTo(5000));
            });
        }

        [Test]
        public void TestHoldingThroughExpiryRestartsTheCountdownOnReleaseWithoutReplayingArt()
        {
            GameplaySkinPreparationController controller = createController();
            controller.Advance(3000, false);
            controller.SetHold(true);

            Assert.That(controller.State.Elapsed, Is.EqualTo(3000));
            Assert.That(controller.State.Remaining, Is.EqualTo(5000));
            Assert.That(controller.Advance(5100, false), Is.False);
            Assert.That(controller.State.Ready, Is.False);
            Assert.That(controller.State.Remaining, Is.Zero);

            controller.SetHold(false);
            Assert.Multiple(() =>
            {
                Assert.That(controller.State.Elapsed, Is.EqualTo(8100));
                Assert.That(controller.State.Remaining, Is.EqualTo(5000));
                Assert.That(controller.State.Ready, Is.False);
                Assert.That(controller.State.ReadyElapsed, Is.Zero);
            });

            Assert.That(controller.Advance(3500, false), Is.False);
            Assert.That(controller.State.Elapsed, Is.EqualTo(11600));
            Assert.That(controller.State.Ready, Is.True);
            Assert.That(controller.State.ReadyElapsed, Is.Zero);
            Assert.That(controller.Advance(1500, false), Is.True);
        }

        [Test]
        public void TestEarlyHoldReleaseKeepsTheCurrentCountdownAndANewPressResetsIt()
        {
            GameplaySkinPreparationController controller = createController();
            controller.Advance(1000, false);
            controller.SetHold(true);
            controller.Advance(1000, false);
            controller.SetHold(false);
            Assert.That(controller.State.Remaining, Is.EqualTo(4000));

            controller.SetHold(true);
            Assert.That(controller.State.Remaining, Is.EqualTo(5000));
            Assert.That(controller.State.Elapsed, Is.EqualTo(2000));
            controller.SetHold(true);
            controller.Advance(500, false);
            Assert.That(controller.State.Remaining, Is.EqualTo(4500));
        }

        [Test]
        public void TestShortPreparationSamplesTheExistingReadyExitRatherThanLengtheningTheWait()
        {
            GameplaySkinPreparationController controller = createController(1000);
            Assert.That(controller.State.Elapsed, Is.Zero);
            Assert.That(controller.State.Ready, Is.True);
            Assert.That(controller.State.ReadyElapsed, Is.EqualTo(500));
            Assert.That(controller.Advance(1000, false), Is.True);
        }

        [Test]
        public void TestCancelRemovesTheWholePublishedPhaseAndNoFurtherUpdatesCanRestartIt()
        {
            GameplaySkinPreparationController controller = createController();
            controller.Advance(4200, false);
            controller.Cancel();
            controller.SetHold(true);
            Assert.That(controller.Advance(5000, false), Is.False);
            Assert.That(controller.State, Is.EqualTo(default(GameplaySkinPreparationStateSnapshot)));
        }

        [TestCase(-1)]
        [TestCase(10001)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void TestDurationMustRespectTheRealAuthorBoundary(double duration)
        {
            var controller = new GameplaySkinPreparationController();
            Assert.Throws<ArgumentOutOfRangeException>(() => controller.Configure(duration));
        }

        private static GameplaySkinPreparationController createController(double duration = 5000)
        {
            var controller = new GameplaySkinPreparationController();
            controller.Configure(duration);
            Assert.That(controller.State, Is.EqualTo(default(GameplaySkinPreparationStateSnapshot)));
            controller.Begin();
            return controller;
        }
    }
}
