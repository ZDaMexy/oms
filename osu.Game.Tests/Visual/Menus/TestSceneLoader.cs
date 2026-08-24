// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterface;
using osu.Game.Screens;
using osu.Game.Screens.Menu;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Tests.Visual.Menus
{
    [TestFixture]
    public partial class TestSceneLoader : ScreenTestScene
    {
        private TestLoader loader;

        [Resolved]
        private SkinManager skinManager { get; set; }

        [Cached]
        private OsuLogo logo;

        public TestSceneLoader()
        {
            Child = logo = new OsuLogo
            {
                Alpha = 0,
                Depth = float.MinValue
            };
        }

        [Test]
        public void TestInstantLoad()
        {
            AddStep("load immediately", () =>
            {
                loader = new TestLoader();
                loader.AllowLoad.Set();

                LoadScreen(loader);
            });

            spinnerNotPresentOrHidden();

            AddUntilStep("loaded", () => loader.ScreenLoaded);
            AddUntilStep("not current", () => !loader.IsCurrentScreen());

            spinnerNotPresentOrHidden();
        }

        private void spinnerNotPresentOrHidden() =>
            AddAssert("spinner did not display", () => loader.LoadingSpinner == null || loader.LoadingSpinner.Alpha == 0);

        [Test]
        public void TestDelayedLoad()
        {
            AddStep("begin loading", () => LoadScreen(loader = new TestLoader()));
            AddUntilStep("wait for spinner visible", () => loader.LoadingSpinner?.Alpha > 0);
            AddStep("finish loading", () => loader.AllowLoad.Set());
            AddUntilStep("spinner gone", () => loader.LoadingSpinner?.Alpha == 0);
            AddUntilStep("loaded", () => loader.ScreenLoaded);
            AddUntilStep("not current", () => !loader.IsCurrentScreen());
        }

        [Test]
        public void TestExitDuringRealIntroScreenLoadDetachesTemporaryRevisionParticipantAndRetries()
        {
            int participantBaseline = 0;

            AddStep("capture revision participant baseline", () =>
                participantBaseline = skinManager.CurrentRevision.ParticipantLeaseCount);
            AddStep("begin gated intro screen load", () =>
            {
                loader = new TestLoader(gateScreenLoad: true);
                loader.AllowLoad.Set();
                LoadScreen(loader);
            });
            AddUntilStep("wait for loader current", () => loader.IsCurrentScreen());
            AddUntilStep("wait for real screen BDL gate", () => loader.ScreenLoadStarted.IsSet);
            AddAssert("temporary participant attached", () =>
                skinManager.CurrentRevision.ParticipantLeaseCount == participantBaseline + 1);
            AddStep("exit while screen load is pending", () =>
            {
                TestLoader exitingLoader = loader;
                _ = Task.Run(() =>
                {
                    Thread.Sleep(50);
                    exitingLoader.AllowScreenLoad.Set();
                });
                exitingLoader.Exit();
            });
            AddUntilStep("wait for loader exit", () => !loader.IsCurrentScreen());
            AddUntilStep("temporary participant detached", () =>
                skinManager.CurrentRevision.ParticipantLeaseCount == participantBaseline);

            AddStep("retry screen load", () =>
            {
                loader = new TestLoader();
                loader.AllowLoad.Set();
                LoadScreen(loader);
            });
            AddUntilStep("retry screen entered", () => loader.ScreenLoaded);
            AddAssert("retry has no provisional participant", () =>
                skinManager.CurrentRevision.ParticipantLeaseCount == participantBaseline);
        }

        private partial class TestLoader : Loader
        {
            public readonly ManualResetEventSlim AllowLoad = new ManualResetEventSlim();
            public readonly ManualResetEventSlim AllowScreenLoad = new ManualResetEventSlim();
            public readonly ManualResetEventSlim ScreenLoadStarted = new ManualResetEventSlim();

            public LoadingSpinner LoadingSpinner => this.ChildrenOfType<LoadingSpinner>().FirstOrDefault();
            private TestScreen screen;

            public bool ScreenLoaded => screen?.IsCurrentScreen() == true;

            public TestLoader(bool gateScreenLoad = false)
            {
                if (!gateScreenLoad)
                    AllowScreenLoad.Set();
            }

            protected override OsuScreen CreateLoadableScreen() => screen = new TestScreen(ScreenLoadStarted, AllowScreenLoad);
            protected override ShaderPrecompiler CreateShaderPrecompiler() => new TestShaderPrecompiler(AllowLoad);

            private partial class TestShaderPrecompiler : ShaderPrecompiler
            {
                private readonly ManualResetEventSlim allowLoad;

                public TestShaderPrecompiler(ManualResetEventSlim allowLoad)
                {
                    this.allowLoad = allowLoad;
                }

                protected override bool AllLoaded => allowLoad.IsSet;
            }

            private partial class TestScreen : OsuScreen
            {
                private readonly ManualResetEventSlim loadStarted;
                private readonly ManualResetEventSlim allowLoad;

                public TestScreen(ManualResetEventSlim loadStarted, ManualResetEventSlim allowLoad)
                {
                    this.loadStarted = loadStarted;
                    this.allowLoad = allowLoad;
                    InternalChild = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.DarkSlateGray,
                        Alpha = 0,
                    };
                }

                [BackgroundDependencyLoader]
                private void load()
                {
                    loadStarted.Set();

                    if (!allowLoad.Wait(System.TimeSpan.FromSeconds(10)))
                        throw new System.TimeoutException();
                }

                protected override void LogoArriving(OsuLogo logo, bool resuming)
                {
                    base.LogoArriving(logo, resuming);
                    InternalChild.FadeInFromZero(200);
                }
            }
        }
    }
}
