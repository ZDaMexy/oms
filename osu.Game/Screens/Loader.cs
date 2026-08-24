// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Screens.Menu;
using osu.Game.Seasonal;
using osu.Game.Skinning;
using IntroSequence = osu.Game.Configuration.IntroSequence;

namespace osu.Game.Screens
{
    public partial class Loader : StartupScreen
    {
        public Loader()
        {
            ValidForResume = false;
        }

        private OsuScreen loadableScreen;
        private PendingAsyncDrawableOwnership<OsuScreen> pendingLoadableScreenOwnership;
        private CancellationTokenSource loadableScreenCancellation;
        private bool loadableScreenReady;
        private bool loadableScreenConsumed;
        private ShaderPrecompiler precompiler;

        private IntroSequence introSequence;
        private LoadingSpinner spinner;
        private ScheduledDelegate spinnerShow;

        [Resolved]
        private SkinManager skinManager { get; set; }

        protected virtual OsuScreen CreateLoadableScreen() => getIntroSequence();

        private IntroScreen getIntroSequence()
        {
            // Headless tests run too fast to load non-circles intros correctly.
            // They will hit the "audio can't play" notification and cause random test failures.
            if (SeasonalUIConfig.ENABLED && !DebugUtils.IsNUnitRunning)
                return new IntroChristmas(createMainMenu);

            if (introSequence == IntroSequence.Random)
                introSequence = (IntroSequence)RNG.Next(0, (int)IntroSequence.Random);

            switch (introSequence)
            {
                case IntroSequence.Circles:
                    return new IntroCircles(createMainMenu);

                case IntroSequence.Welcome:
                    return new IntroWelcome(createMainMenu);

                default:
                    return new IntroTriangles(createMainMenu);
            }

            static MainMenu createMainMenu() => new MainMenu();
        }

        protected virtual ShaderPrecompiler CreateShaderPrecompiler() => new ShaderPrecompiler();

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);

            LoadComponentAsync(precompiler = CreateShaderPrecompiler(), AddInternal);

            beginLoadableScreenLoad();

            LoadComponentAsync(spinner = new LoadingSpinner(true, true)
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                Margin = new MarginPadding(40),
            }, _ =>
            {
                AddInternal(spinner);
                spinnerShow = Scheduler.AddDelayed(spinner.Show, 200);
            });

            checkIfLoaded();
        }

        private void checkIfLoaded()
        {
            if (!loadableScreenReady || !precompiler.FinishedCompiling)
            {
                Schedule(checkIfLoaded);
                return;
            }

            spinnerShow?.Cancel();

            if (spinner.State.Value == Visibility.Visible)
            {
                spinner.Hide();
                Scheduler.AddDelayed(pushLoadableScreen, LoadingSpinner.TRANSITION_DURATION);
            }
            else
                pushLoadableScreen();
        }

        private void beginLoadableScreenLoad()
        {
            OsuScreen candidate = CreateLoadableScreen();
            loadableScreen = candidate;

            SkinRevisionParticipantRegistration initialParticipant;

            try
            {
                initialParticipant = skinManager.RegisterRevisionParticipant(
                    SkinRevisionParticipantKind.CoherentVisualConsumer,
                    $"{nameof(Loader)} intro (initial load)",
                    blocksRevisionPublication: true);
            }
            catch
            {
                loadableScreen = null;
                candidate.Dispose();
                throw;
            }

            var ownership = new PendingAsyncDrawableOwnership<OsuScreen>(candidate, initialParticipant.Dispose);
            var cancellation = new CancellationTokenSource();
            pendingLoadableScreenOwnership = ownership;
            loadableScreenCancellation = cancellation;

            try
            {
                var loadTask = LoadComponentAsync(ownership.Loadable, loaded =>
                {
                    if (!ReferenceEquals(pendingLoadableScreenOwnership, ownership)
                        || !ownership.TryTransfer(loaded, out OsuScreen transferred))
                    {
                        return;
                    }

                    pendingLoadableScreenOwnership = null;
                    loadableScreenCancellation?.Dispose();
                    loadableScreenCancellation = null;
                    loadableScreen = transferred;
                    loadableScreenReady = true;
                    ownership.CompleteTransfer();
                }, cancellation.Token);
                ownership.Attach(loadTask, Scheduler);
            }
            catch
            {
                if (ReferenceEquals(pendingLoadableScreenOwnership, ownership))
                    pendingLoadableScreenOwnership = null;

                loadableScreen = null;
                loadableScreenCancellation = null;
                cancellation.Dispose();
                ownership.ReclaimUnstarted();
                throw;
            }
        }

        private void pushLoadableScreen()
        {
            if (loadableScreenConsumed || !loadableScreenReady)
                return;

            OsuScreen screen = loadableScreen;

            try
            {
                this.Push(screen);
                loadableScreenConsumed = true;
            }
            catch
            {
                if (screen.Parent == null)
                {
                    loadableScreen = null;
                    screen.Dispose();
                }
                else
                    loadableScreenConsumed = true;

                throw;
            }
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            introSequence = config.Get<IntroSequence>(OsuSetting.IntroSequence);
        }

        protected override void Dispose(bool isDisposing)
        {
            PendingAsyncDrawableOwnership<OsuScreen> ownership =
                Interlocked.Exchange(ref pendingLoadableScreenOwnership, null);
            ownership?.Cancel();

            CancellationTokenSource cancellation = Interlocked.Exchange(ref loadableScreenCancellation, null);
            cancellation?.Cancel();
            cancellation?.Dispose();

            base.Dispose(isDisposing);
            ownership?.JoinAfterParentDisposal();

            if (isDisposing && !loadableScreenConsumed)
            {
                if (ownership == null)
                    loadableScreen?.Dispose();

                loadableScreen = null;
            }
        }

        /// <summary>
        /// Compiles a set of shaders before continuing. Attempts to draw some frames between compilation by limiting to one compile per draw frame.
        /// </summary>
        public partial class ShaderPrecompiler : Drawable
        {
            private readonly List<IShader> loadTargets = new List<IShader>();

            public bool FinishedCompiling { get; private set; }

            [BackgroundDependencyLoader]
            private void load(ShaderManager manager)
            {
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE));
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.BLUR));
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_3, FragmentShaderDescriptor.TEXTURE));

                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, @"TriangleBorder"));
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, @"FastCircle"));
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, @"CircularProgress"));
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, @"ArgonBarPath"));
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, @"ArgonBarPathBackground"));
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, @"SaturationSelectorBackground"));
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, @"HueSelectorBackground"));
                loadTargets.Add(manager.Load(@"LogoAnimation", @"LogoAnimation"));

                // Ruleset local shader usage (should probably move somewhere else).
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, @"SpinnerGlow"));
                loadTargets.Add(manager.Load(@"CursorTrail", FragmentShaderDescriptor.TEXTURE));
            }

            protected virtual bool AllLoaded => loadTargets.All(s => s.IsLoaded);

            protected override void Update()
            {
                base.Update();

                // if our target is null we are done.
                if (AllLoaded)
                {
                    FinishedCompiling = true;
                    Expire();
                }
            }
        }
    }
}
