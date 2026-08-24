// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Threading;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Screens;
using osu.Game.Screens.Backgrounds;
using osu.Game.Skinning;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [Resolved]
        private OsuConfigManager backgroundConfig { get; set; } = null!;

        [Test]
        public void TestRealBackgroundScreenReloadRetainsAThroughFadeThenDetachesExactlyOnce()
        {
            var context = new CurrentRevisionProductContext();
            CurrentRevisionBackgroundScreenHost backgroundHost = null!;
            FullSkinSettingsCallerHost caller = null!;
            SkinBackground backgroundA = null!;
            SkinCurrentRevision revisionA = null!;
            int retiredA = 0;

            AddStep("create and select background revision A", () =>
            {
                (context.PackageRoot, context.Candidate) = createCandidate(
                    root => writeBackgroundRevisionPackage(root, "A", new Rgba32(225, 55, 90, 255)),
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = context.Candidate;
            });
            AddUntilStep("wait for exact background A pair", () =>
                manager.CurrentSkinInfo.Value.ID == context.Candidate.ID
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("enable real skin background and mount callers", () =>
            {
                ((DummyAPIAccess)API).LocalUser.Value = new APIUser
                {
                    IsSupporter = true,
                    Id = API.LocalUser.Value.Id + 1,
                };
                backgroundConfig.SetValue(OsuSetting.MenuBackgroundSource, BackgroundSource.Skin);

                revisionA = manager.CurrentRevision;
                manager.CurrentRevisionRetired += revision =>
                {
                    if (ReferenceEquals(revision, revisionA))
                        Interlocked.Increment(ref retiredA);
                };
                Add(backgroundHost = new CurrentRevisionBackgroundScreenHost(manager));
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for real A background and reload button", () =>
                backgroundHost.Screen.IsLoaded
                && backgroundHost.SkinBackgrounds.Length == 1
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("capture A background and invoke real reload", () =>
            {
                backgroundA = backgroundHost.SkinBackgrounds.Single();
                writeBackgroundRevisionPackage(context.PackageRoot, "B", new Rgba32(30, 195, 220, 255));
                caller.ReloadCurrentButton.TriggerClick();
            });
            AddUntilStep("wait for B authority while A is still displayed", () =>
                !ReferenceEquals(manager.CurrentRevision, revisionA)
                && caller.ReloadCurrentButton.Enabled.Value
                && backgroundHost.SkinBackgrounds.Contains(backgroundA));
            AddStep("assert delayed candidate and fade retain exact A", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentRevision.Owner, Is.SameAs(manager.CurrentSkin.Value));
                    Assert.That(manager.CurrentRevision.RecordId, Is.EqualTo(revisionA.RecordId));
                    Assert.That(revisionA.ConsumersDetached.IsCompleted, Is.False);
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(retiredA, Is.Zero);
                });
            });
            AddUntilStep("wait for real B background to enter fade graph", () =>
                backgroundHost.SkinBackgrounds.Any(candidate => !ReferenceEquals(candidate, backgroundA)));
            AddStep("assert A remains alive during real cross-fade", () =>
            {
                Assert.That(backgroundHost.SkinBackgrounds, Does.Contain(backgroundA));
                Assert.That(revisionA.Retired.IsCompleted, Is.False);
                Assert.That(retiredA, Is.Zero);
            });
            AddUntilStep("wait for A fade detach and exactly-once retire", () =>
                !backgroundHost.SkinBackgrounds.Contains(backgroundA)
                && revisionA.Retired.IsCompleted
                && Volatile.Read(ref retiredA) == 1);
            AddStep("assert background retirement remains exactly once", () =>
            {
                Assert.That(retiredA, Is.EqualTo(1));
                backgroundConfig.SetValue(OsuSetting.MenuBackgroundSource, BackgroundSource.Beatmap);
            });
        }

        [Test]
        public void TestBackgroundReloadBCancellationReclaimsPendingBExactlyOnce()
        {
            var gateEntered = new ManualResetEventSlim();
            var releaseGate = new ManualResetEventSlim();
            var context = new CurrentRevisionProductContext();
            CurrentRevisionBackgroundScreenHost backgroundHost = null!;
            FullSkinSettingsCallerHost caller = null!;
            GatedSkinBackground backgroundB = null!;
            SkinCurrentRevision revisionA = null!;
            SkinCurrentRevision revisionB = null!;
            int skinBackgroundCreations = 0;
            int retiredB = 0;
            bool backgroundDisposedAtRetire = false;

            AddStep("create and select background-cancel revision A", () =>
            {
                (context.PackageRoot, context.Candidate) = createCandidate(
                    root => writeBackgroundRevisionPackage(root, "A", new Rgba32(225, 55, 90, 255)),
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = context.Candidate;
            });
            AddUntilStep("wait for background-cancel A pair", () =>
                manager.CurrentSkinInfo.Value.ID == context.Candidate.ID
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount gated production background screen", () =>
            {
                ((DummyAPIAccess)API).LocalUser.Value = new APIUser
                {
                    IsSupporter = true,
                    Id = API.LocalUser.Value.Id + 1,
                };
                backgroundConfig.SetValue(OsuSetting.MenuBackgroundSource, BackgroundSource.Skin);
                revisionA = manager.CurrentRevision;
                manager.CurrentRevisionRetired += revision =>
                {
                    if (!ReferenceEquals(revision, revisionB))
                        return;

                    backgroundDisposedAtRetire = backgroundB.DisposeCount == 1;
                    Interlocked.Increment(ref retiredB);
                };

                Add(backgroundHost = new CurrentRevisionBackgroundScreenHost(
                    manager,
                    screen => screen.SkinBackgroundFactory = (owner, fallback, holder) =>
                    {
                        if (Interlocked.Increment(ref skinBackgroundCreations) == 2)
                            return backgroundB = new GatedSkinBackground(owner, fallback, holder, gateEntered, releaseGate);

                        return new SkinBackground(owner, fallback, holder);
                    }));
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for displayed A and reload caller", () =>
                backgroundHost.SkinBackgrounds.Length == 1
                && Volatile.Read(ref skinBackgroundCreations) == 1
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("publish B through real reload caller", () =>
            {
                writeBackgroundRevisionPackage(context.PackageRoot, "B", new Rgba32(30, 195, 220, 255));
                caller.ReloadCurrentButton.TriggerClick();
            });
            AddUntilStep("wait for B authority", () =>
                !ReferenceEquals(manager.CurrentRevision, revisionA)
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("capture exact B", () => revisionB = manager.CurrentRevision);
            AddUntilStep("wait for provisional B background load", () => gateEntered.IsSet);
            AddStep("publish C while provisional B remains in its loader", () =>
            {
                writeBackgroundRevisionPackage(context.PackageRoot, "C", new Rgba32(70, 220, 100, 255));
                caller.ReloadCurrentButton.TriggerClick();
            });
            AddUntilStep("wait for C to supersede B", () =>
                !ReferenceEquals(manager.CurrentRevision, revisionB)
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("assert cancelled B remains retained until its loader stops", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(backgroundB.Parent, Is.Null);
                    Assert.That(backgroundB.DisposeCount, Is.Zero);
                    Assert.That(revisionB.Retired.IsCompleted, Is.False);
                    Assert.That(retiredB, Is.Zero);
                });

                releaseGate.Set();
            });
            AddUntilStep("wait for cancelled B reclaim and exactly-once retirement", () =>
                backgroundB.DisposeCount == 1
                && revisionB.ConsumersDetached.IsCompleted
                && revisionB.Retired.IsCompleted
                && Volatile.Read(ref retiredB) == 1);
            AddUntilStep("wait for C background display", () =>
                Volatile.Read(ref skinBackgroundCreations) >= 3
                && backgroundHost.SkinBackgrounds.Any(candidate => !ReferenceEquals(candidate, backgroundB)));
            AddStep("assert cancelled B never entered the display graph", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(backgroundHost.SkinBackgrounds, Does.Not.Contain(backgroundB));
                    Assert.That(backgroundDisposedAtRetire, Is.True);
                    Assert.That(backgroundB.DisposeCount, Is.EqualTo(1));
                    Assert.That(retiredB, Is.EqualTo(1));
                });

                backgroundConfig.SetValue(OsuSetting.MenuBackgroundSource, BackgroundSource.Beatmap);
                gateEntered.Dispose();
                releaseGate.Dispose();
            });
        }

        [Test]
        public void TestBackgroundScreenDisposeDuringLoadReclaimsPendingOwnerExactlyOnce()
        {
            var gateEntered = new ManualResetEventSlim();
            var releaseGate = new ManualResetEventSlim();
            var context = new CurrentRevisionProductContext();
            CurrentRevisionBackgroundScreenHost backgroundHost = null!;
            FullSkinSettingsCallerHost caller = null!;
            GatedSkinBackground backgroundB = null!;
            SkinCurrentRevision revisionA = null!;
            SkinCurrentRevision revisionB = null!;
            Task shutdown = null!;
            var callbackScheduler = new Scheduler();
            int skinBackgroundCreations = 0;
            int retiredB = 0;
            bool backgroundDisposedAtRetire = false;

            AddStep("create and select background-dispose revision A", () =>
            {
                (context.PackageRoot, context.Candidate) = createCandidate(
                    root => writeBackgroundRevisionPackage(root, "A", new Rgba32(225, 55, 90, 255)),
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = context.Candidate;
            });
            AddUntilStep("wait for background-dispose A pair", () =>
                manager.CurrentSkinInfo.Value.ID == context.Candidate.ID
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount dispose-gated production background screen", () =>
            {
                ((DummyAPIAccess)API).LocalUser.Value = new APIUser
                {
                    IsSupporter = true,
                    Id = API.LocalUser.Value.Id + 1,
                };
                backgroundConfig.SetValue(OsuSetting.MenuBackgroundSource, BackgroundSource.Skin);
                revisionA = manager.CurrentRevision;
                manager.CurrentRevisionRetired += revision =>
                {
                    if (!ReferenceEquals(revision, revisionB))
                        return;

                    backgroundDisposedAtRetire = backgroundB.DisposeCount == 1;
                    Interlocked.Increment(ref retiredB);
                };

                Add(backgroundHost = new CurrentRevisionBackgroundScreenHost(
                    manager,
                    screen =>
                    {
                        screen.SkinBackgroundFactory = (owner, fallback, holder) =>
                        {
                            if (Interlocked.Increment(ref skinBackgroundCreations) == 2)
                            {
                                screen.BackgroundLoadCallbackScheduler = callbackScheduler;
                                return backgroundB = new GatedSkinBackground(owner, fallback, holder, gateEntered, releaseGate);
                            }

                            return new SkinBackground(owner, fallback, holder);
                        };
                    }));
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for dispose-test A background", () =>
                backgroundHost.SkinBackgrounds.Length == 1
                && Volatile.Read(ref skinBackgroundCreations) == 1
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("publish dispose-test B", () =>
            {
                writeBackgroundRevisionPackage(context.PackageRoot, "B", new Rgba32(30, 195, 220, 255));
                caller.ReloadCurrentButton.TriggerClick();
            });
            AddUntilStep("wait for dispose-test B authority", () =>
                !ReferenceEquals(manager.CurrentRevision, revisionA)
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("capture dispose-test B", () => revisionB = manager.CurrentRevision);
            AddUntilStep("wait for dispose-test B loader", () => gateEntered.IsSet);
            AddStep("release B worker while holding its scheduled callback", releaseGate.Set);
            AddUntilStep("wait for load-complete-before-callback window", () =>
                backgroundB.BackgroundLoadPrepared
                && backgroundHost.Screen.PendingBackgroundLoadTask?.IsCompleted == true
                && backgroundB.Parent?.Parent == null
                && backgroundB.DisposeCount == 0);
            AddStep("dispose screen inside the skipped-callback window", () =>
            {
                Assert.That(Remove(backgroundHost, disposeImmediately: true), Is.True);
                callbackScheduler.Update();

                Assert.Multiple(() =>
                {
                    Assert.That(backgroundB.Parent, Is.Null);
                    Assert.That(backgroundB.DisposeCount, Is.EqualTo(1));
                    Assert.That(revisionB.Retired.IsCompleted, Is.False,
                        "The current B manager lease remains authoritative until shutdown.");
                    Assert.That(retiredB, Is.Zero);
                });

                shutdown = Task.Run(() => manager.ShutdownManagedFolderMutations());
            });
            AddUntilStep("wait for dispose-time B reclaim and retirement", () =>
                shutdown.IsCompleted
                && backgroundB.DisposeCount == 1
                && revisionB.ConsumersDetached.IsCompleted
                && revisionB.Retired.IsCompleted
                && Volatile.Read(ref retiredB) == 1);
            AddStep("assert dispose-time reclaim stayed exactly once", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(backgroundDisposedAtRetire, Is.True);
                    Assert.That(shutdown.IsCompletedSuccessfully, Is.True);
                    Assert.That(backgroundB.DisposeCount, Is.EqualTo(1));
                    Assert.That(retiredB, Is.EqualTo(1));
                });

                manager.ShutdownManagedFolderMutations();
                Assert.That(retiredB, Is.EqualTo(1));
                gateEntered.Dispose();
                releaseGate.Dispose();
            });
        }

        private static void writeBackgroundRevisionPackage(string packageRoot, string revision, Rgba32 colour)
        {
            writeRevisionPackage(packageRoot, revision, colour);
            System.IO.File.WriteAllBytes(
                System.IO.Path.Combine(packageRoot, "menu-background.png"),
                createPng(colour));
        }

        private partial class CurrentRevisionBackgroundScreenHost : CompositeDrawable
        {
            [Cached]
            private readonly SkinManager skinManager;

            [Cached(typeof(ISkinSource))]
            private readonly ISkinSource skinSource;

            private readonly BackgroundScreenStack stack = new BackgroundScreenStack();

            public BackgroundScreenDefault Screen { get; } = new BackgroundScreenDefault();

            public SkinBackground[] SkinBackgrounds
                => Screen.ChildrenOfType<SkinBackground>().ToArray();

            public CurrentRevisionBackgroundScreenHost(
                SkinManager skinManager,
                Action<BackgroundScreenDefault>? configure = null)
            {
                this.skinManager = skinManager;
                skinSource = skinManager;
                configure?.Invoke(Screen);
                RelativeSizeAxes = Axes.Both;
                InternalChild = stack;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                stack.Push(Screen);
            }
        }

        private partial class GatedSkinBackground : SkinBackground
        {
            private readonly ManualResetEventSlim loaderEntered;
            private readonly ManualResetEventSlim allowLoader;
            private int disposeCount;

            public int DisposeCount => Volatile.Read(ref disposeCount);

            public bool BackgroundLoadPrepared { get; private set; }

            public GatedSkinBackground(
                Skin skin,
                string fallbackTextureName,
                SkinRevisionParticipantRegistration revisionHolder,
                ManualResetEventSlim loaderEntered,
                ManualResetEventSlim allowLoader)
                : base(skin, fallbackTextureName, revisionHolder)
            {
                this.loaderEntered = loaderEntered;
                this.allowLoader = allowLoader;
            }

            [BackgroundDependencyLoader]
            private void waitForTestGate()
            {
                loaderEntered.Set();

                if (!allowLoader.Wait(TimeSpan.FromSeconds(30)))
                    throw new TimeoutException("Timed out waiting to release the background load gate.");

                BackgroundLoadPrepared = true;
            }

            protected override void Dispose(bool isDisposing)
            {
                Interlocked.Increment(ref disposeCount);
                base.Dispose(isDisposing);
            }
        }
    }
}
