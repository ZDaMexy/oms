// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Screens;
using osu.Game.Screens.Menu;
using osu.Game.Skinning;

namespace osu.Game.Tests.Visual.Menus
{
    [TestFixture]
    public partial class TestSceneIntroWelcome : IntroTestScene
    {
        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        private bool createMainMenu;
        private bool gateWelcomeSequence;
        private ManualResetEventSlim welcomeLoadStarted = null!;
        private ManualResetEventSlim allowWelcomeLoad = null!;
        private TrackingWelcomeIntroSequence? latestWelcomeSequence;

        protected override bool IntroReliesOnTrack => false;
        protected override IntroScreen CreateScreen() => new TestIntroWelcome(
            createMainMenu ? () => new MainMenu() : null,
            gateWelcomeSequence,
            welcomeLoadStarted,
            allowWelcomeLoad,
            sequence => latestWelcomeSequence = sequence);

        [SetUp]
        public void SetUp()
        {
            createMainMenu = false;
            gateWelcomeSequence = false;
            welcomeLoadStarted = new ManualResetEventSlim();
            allowWelcomeLoad = new ManualResetEventSlim(true);
            latestWelcomeSequence = null;
        }

        [TearDownSteps]
        public void TearDown()
        {
            AddUntilStep("release welcome sequence gate", () =>
            {
                allowWelcomeLoad?.Set();
                return latestWelcomeSequence?.BackgroundLoadFinished != false;
            });
            AddStep("dispose welcome sequence gates", () =>
            {
                welcomeLoadStarted?.Dispose();
                allowWelcomeLoad?.Dispose();
            });
        }

        public override void TestPlayIntro()
        {
            base.TestPlayIntro();

            AddUntilStep("wait for load", () => MusicController.TrackLoaded);
            AddAssert("correct track", () => Precision.AlmostEquals(MusicController.CurrentTrack.Length, 48000, 1));
            AddAssert("check if menu music loops", () => MusicController.CurrentTrack.Looping);
        }

        [Test]
        public void TestDisposeDuringRealMainMenuAsyncLoadDetachesRevisionParticipants()
        {
            int participantBaseline = 0;

            AddStep("capture revision participant baseline", () =>
                participantBaseline = skinManager.CurrentRevision.ParticipantLeaseCount);
            AddStep("enable real main menu candidate", () => createMainMenu = true);
            RestartIntro();
            AddUntilStep("wait for intro host load", () => Intro.IsLoaded);
            AddStep("start menu load and dispose host before callback", () =>
            {
                ((TestIntroWelcome)Intro).BeginMenuLoad();
                Remove(IntroStack, disposeImmediately: true);
            });
            AddUntilStep("all provisional and descendant participants detached", () =>
                skinManager.CurrentRevision.ParticipantLeaseCount == participantBaseline);
            AddStep("restore lightweight intro factory", () => createMainMenu = false);
        }

        [Test]
        public void TestSuspendingDuringWelcomeSequenceLoadReclaimsTemporaryParticipantAndRetries()
        {
            int participantBaseline = 0;
            TrackingWelcomeIntroSequence? cancelledSequence = null;

            AddStep("capture revision participant baseline", () =>
                participantBaseline = skinManager.CurrentRevision.ParticipantLeaseCount);
            AddStep("gate real welcome sequence load", () =>
            {
                gateWelcomeSequence = true;
                allowWelcomeLoad.Reset();
            });
            RestartIntro();
            AddUntilStep("wait for real sequence BDL gate", () => welcomeLoadStarted.IsSet);
            AddStep("capture provisional sequence", () => cancelledSequence = latestWelcomeSequence);
            AddAssert("temporary participant attached", () =>
                skinManager.CurrentRevision.ParticipantLeaseCount == participantBaseline + 1);

            AddStep("push next screen", () => IntroStack.Push(new SuspensionTargetScreen()));
            AddUntilStep("intro suspended", () => !Intro.IsCurrentScreen());
            AddStep("release sequence BDL", () => allowWelcomeLoad.Set());
            AddUntilStep("temporary participant detached", () =>
                skinManager.CurrentRevision.ParticipantLeaseCount == participantBaseline);
            AddUntilStep("candidate disposed exactly once", () => cancelledSequence?.DisposeCalls == 1);
            AddAssert("cancelled candidate was not transferred", () => cancelledSequence?.Parent, () => Is.Null);

            AddStep("prepare retry", () =>
            {
                Remove(IntroStack, disposeImmediately: true);
                IntroStack = null!;
                welcomeLoadStarted.Dispose();
                allowWelcomeLoad.Dispose();
                gateWelcomeSequence = false;
                welcomeLoadStarted = new ManualResetEventSlim();
                allowWelcomeLoad = new ManualResetEventSlim(true);
                latestWelcomeSequence = null;
            });
            AddAssert("disposal join did not double-dispose candidate", () => cancelledSequence?.DisposeCalls == 1);
            RestartIntro();
            AddUntilStep("retry sequence transferred", () => latestWelcomeSequence?.Parent != null);
            AddAssert("retry sequence remains owned by intro", () => latestWelcomeSequence?.DisposeCalls == 0);
        }

        private partial class TestIntroWelcome : IntroWelcome
        {
            private readonly bool gateWelcomeSequence;
            private readonly ManualResetEventSlim welcomeLoadStarted;
            private readonly ManualResetEventSlim allowWelcomeLoad;
            private readonly Action<TrackingWelcomeIntroSequence> sequenceCreated;

            internal TestIntroWelcome(
                Func<MainMenu>? createNextScreen,
                bool gateWelcomeSequence,
                ManualResetEventSlim welcomeLoadStarted,
                ManualResetEventSlim allowWelcomeLoad,
                Action<TrackingWelcomeIntroSequence> sequenceCreated)
                : base(createNextScreen)
            {
                this.gateWelcomeSequence = gateWelcomeSequence;
                this.welcomeLoadStarted = welcomeLoadStarted;
                this.allowWelcomeLoad = allowWelcomeLoad;
                this.sequenceCreated = sequenceCreated;
            }

            internal void BeginMenuLoad()
            {
                PrepareMenuLoad();
                LoadMenu();
            }

            protected override WelcomeIntroSequence CreateWelcomeIntroSequence()
            {
                var sequence = new TrackingWelcomeIntroSequence(
                    gateWelcomeSequence,
                    welcomeLoadStarted,
                    allowWelcomeLoad);
                sequenceCreated(sequence);
                return sequence;
            }
        }

        private partial class TrackingWelcomeIntroSequence : IntroWelcome.WelcomeIntroSequence
        {
            private readonly bool gateLoad;
            private readonly ManualResetEventSlim loadStarted;
            private readonly ManualResetEventSlim allowLoad;
            private int disposeCalls;
            private int backgroundLoadFinished;

            internal int DisposeCalls => Volatile.Read(ref disposeCalls);
            internal bool BackgroundLoadFinished => Volatile.Read(ref backgroundLoadFinished) != 0;

            internal TrackingWelcomeIntroSequence(
                bool gateLoad,
                ManualResetEventSlim loadStarted,
                ManualResetEventSlim allowLoad)
            {
                this.gateLoad = gateLoad;
                this.loadStarted = loadStarted;
                this.allowLoad = allowLoad;
            }

            [BackgroundDependencyLoader]
            private void gateBackgroundLoad()
            {
                try
                {
                    loadStarted.Set();

                    if (gateLoad && !allowLoad.Wait(TimeSpan.FromSeconds(10)))
                        throw new TimeoutException();
                }
                finally
                {
                    Volatile.Write(ref backgroundLoadFinished, 1);
                }
            }

            protected override void Dispose(bool isDisposing)
            {
                Interlocked.Increment(ref disposeCalls);
                base.Dispose(isDisposing);
            }
        }

        private partial class SuspensionTargetScreen : OsuScreen
        {
        }
    }
}
