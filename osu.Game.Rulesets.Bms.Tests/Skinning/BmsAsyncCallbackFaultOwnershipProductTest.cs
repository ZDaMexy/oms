// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedPackageNoteProductTest
    {
        [Resolved]
        private GameHost asyncOwnershipGameHost { get; set; } = null!;

        [Test]
        public void TestBmsInitialRevisionWorkWaitsForOuterReadyBeforeGlobalCallback()
        {
            importAndSelect("BMS initial ready barrier", () => createOsk(null));

            BmsAsyncNoteDrawable host = null!;
            Drawable resolvedVisual = null!;
            var outerLoadHeld = new ManualResetEventSlim();
            var allowOuterLoad = new ManualResetEventSlim();
            var admissionStarted = new ManualResetEventSlim();
            var resolverStarted = new ManualResetEventSlim();
            var schedulerSentinel = new ManualResetEventSlim();

            AddStep("mount held real BMS async owner", () =>
            {
                host = new BmsAsyncNoteDrawable(createLookup(BmsNoteSkinElements.Note))
                {
                    RevisionWorkAdmissionTestHook = () => admissionStarted.Set(),
                    DrawableResolver = (_, _) =>
                    {
                        resolverStarted.Set();
                        return resolvedVisual = new Container();
                    },
                    LoadAsyncCompleteAfterSkinChangedTestHook = () =>
                    {
                        outerLoadHeld.Set();

                        if (!allowOuterLoad.Wait(TimeSpan.FromSeconds(10)))
                            throw new TimeoutException();
                    },
                };
                _ = LoadComponentAsync(host, loaded => Add(loaded));
            });
            AddUntilStep("hold outer BMS owner in loading", () => outerLoadHeld.IsSet);
            AddStep("queue BMS ready-admission scheduler sentinel", () =>
                asyncOwnershipGameHost.UpdateThread.Scheduler.AddDelayed(schedulerSentinel.Set, 50));
            AddUntilStep("let BMS ready-admission scheduler pass while loading", () => schedulerSentinel.IsSet);
            AddStep("assert BMS work remains outside admission while loading", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(host.LoadState, Is.LessThan(LoadState.Ready));
                    Assert.That(admissionStarted.IsSet, Is.False);
                    Assert.That(resolverStarted.IsSet, Is.False);
                });
            });
            AddStep("release outer BMS owner", () => allowOuterLoad.Set());
            AddUntilStep("outer BMS owner becomes ready", () => host.LoadState >= LoadState.Ready);
            AddUntilStep("deferred BMS work publishes", () => ReferenceEquals(host.Drawable, resolvedVisual));
            AddStep("assert no early global callback mutation", () =>
            {
                Assert.That(admissionStarted.IsSet, Is.True);
                Assert.That(Remove(host, disposeImmediately: true), Is.True);
                outerLoadHeld.Dispose();
                allowOuterLoad.Dispose();
                admissionStarted.Dispose();
                resolverStarted.Dispose();
                schedulerSentinel.Dispose();
            });
        }

        [Test]
        public void TestSkinnableInitialRevisionWorkWaitsForOuterReadyBeforeGlobalCallback()
        {
            importAndSelect("skinnable initial ready barrier", () => createOsk(null));

            SkinnableContainer host = null!;
            var outerLoadHeld = new ManualResetEventSlim();
            var allowOuterLoad = new ManualResetEventSlim();
            var admissionStarted = new ManualResetEventSlim();
            var schedulerSentinel = new ManualResetEventSlim();

            AddStep("mount held real skinnable owner", () =>
            {
                host = new SkinnableContainer(
                    new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.Results))
                {
                    RevisionWorkAdmissionTestHook = () => admissionStarted.Set(),
                    LoadAsyncCompleteAfterSkinChangedTestHook = () =>
                    {
                        outerLoadHeld.Set();

                        if (!allowOuterLoad.Wait(TimeSpan.FromSeconds(10)))
                            throw new TimeoutException();
                    },
                };
                _ = LoadComponentAsync(host, loaded => Add(loaded));
            });
            AddUntilStep("hold outer skinnable owner in loading", () => outerLoadHeld.IsSet);
            AddStep("queue skinnable ready-admission scheduler sentinel", () =>
                asyncOwnershipGameHost.UpdateThread.Scheduler.AddDelayed(schedulerSentinel.Set, 50));
            AddUntilStep("let skinnable ready-admission scheduler pass while loading", () => schedulerSentinel.IsSet);
            AddStep("assert skinnable work remains outside admission while loading", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(host.LoadState, Is.LessThan(LoadState.Ready));
                    Assert.That(admissionStarted.IsSet, Is.False);
                });
            });
            AddStep("release outer skinnable owner", () => allowOuterLoad.Set());
            AddUntilStep("outer skinnable owner becomes ready", () => host.LoadState >= LoadState.Ready);
            AddUntilStep("deferred skinnable work publishes", () => host.ComponentsLoaded);
            AddStep("assert no early global callback mutation", () =>
            {
                Assert.That(admissionStarted.IsSet, Is.True);
                Assert.That(Remove(host, disposeImmediately: true), Is.True);
                outerLoadHeld.Dispose();
                allowOuterLoad.Dispose();
                admissionStarted.Dispose();
                schedulerSentinel.Dispose();
            });
        }

        [Test]
        public void TestBmsAsyncNotePublishesAfterOwnerBecomesNonPresent()
        {
            importAndSelect("BMS async non-present callback", () => createOsk(null));

            SkinCurrentRevision revision = null!;
            NonRemovingContainer parent = null!;
            BmsAsyncNoteDrawable host = null!;
            Drawable resolvedVisual = null!;
            var prepareStarted = new ManualResetEventSlim();
            var allowPrepare = new ManualResetEventSlim();

            AddStep("mount visible real BMS async owner", () =>
            {
                revision = skinManager.CurrentRevision;
                host = new BmsAsyncNoteDrawable(createLookup(BmsNoteSkinElements.Note))
                {
                    DrawableResolver = (_, _) =>
                    {
                        prepareStarted.Set();

                        if (!allowPrepare.Wait(TimeSpan.FromSeconds(10)))
                            throw new TimeoutException();

                        return resolvedVisual = new Container();
                    },
                };
                Add(parent = new NonRemovingContainer { Child = host });
            });
            AddUntilStep("wait for real BMS prepare", () => prepareStarted.IsSet);
            AddStep("move owner outside its lifetime", () =>
                parent.LifetimeStart = Clock.CurrentTime + 60_000);
            AddUntilStep("wait for BMS owner to stop updating", () => !parent.IsAlive);
            AddStep("finish prepare after owner stopped updating", () => allowPrepare.Set());
            AddUntilStep("visibility-independent update barrier publishes", () =>
                ReferenceEquals(host.Drawable, resolvedVisual));
            AddUntilStep("exact BMS work lease detaches", () => revision.WorkDetached.IsCompleted);
            AddStep("assert non-present owner published on update thread", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(parent.IsAlive, Is.False);
                    Assert.That(host.Drawable, Is.SameAs(resolvedVisual));
                    Assert.That(resolvedVisual.LoadState, Is.GreaterThanOrEqualTo(LoadState.Ready));
                    Assert.That(revision.WorkDetached.IsCompletedSuccessfully, Is.True);
                });

                Assert.That(Remove(parent, disposeImmediately: true), Is.True);
                prepareStarted.Dispose();
                allowPrepare.Dispose();
            });
        }

        [Test]
        public void TestBmsSourceInvalidationPublishesWhileOwnerIsNonPresent()
        {
            importAndSelect("BMS non-present source invalidation", () => createOsk(null));

            SkinCurrentRevision revision = null!;
            NonRemovingContainer parent = null!;
            InvalidatableSkinDependencyHost sourceHost = null!;
            BmsAsyncNoteDrawable host = null!;
            DisposalTrackingDrawable? lastResolvedVisual = null;
            DisposalTrackingDrawable? initialVisual = null;
            int initialResolverCalls = 0;
            int resolverCalls = 0;

            AddStep("mount visible invalidatable BMS source", () =>
            {
                revision = skinManager.CurrentRevision;
                host = new BmsAsyncNoteDrawable(createLookup(BmsNoteSkinElements.Note))
                {
                    DrawableResolver = (_, _) =>
                    {
                        var resolved = new DisposalTrackingDrawable();
                        Interlocked.Increment(ref resolverCalls);
                        Volatile.Write(ref lastResolvedVisual, resolved);
                        return resolved;
                    },
                };
                sourceHost = new InvalidatableSkinDependencyHost(skinManager, host);
                Add(parent = new NonRemovingContainer { Child = sourceHost });
            });
            AddUntilStep("wait for initial BMS source publication", () =>
                Volatile.Read(ref resolverCalls) > 0
                && ReferenceEquals(host.Drawable, Volatile.Read(ref lastResolvedVisual))
                && revision.WorkDetached.IsCompleted);
            AddStep("capture exact initial source visual", () =>
            {
                initialVisual = (DisposalTrackingDrawable)host.Drawable!;
                initialResolverCalls = Volatile.Read(ref resolverCalls);
            });
            AddStep("move invalidatable BMS source outside lifetime", () =>
                parent.LifetimeStart = Clock.CurrentTime + 60_000);
            AddUntilStep("wait for invalidatable source to stop updating", () => !parent.IsAlive);
            AddStep("invalidate exact source while owner is non-present", () => sourceHost.Invalidate());
            AddUntilStep("visibility-independent source rebuild publishes", () =>
                Volatile.Read(ref resolverCalls) > initialResolverCalls
                && !ReferenceEquals(host.Drawable, initialVisual)
                && ReferenceEquals(host.Drawable, Volatile.Read(ref lastResolvedVisual)));
            AddUntilStep("invalidated BMS work lease detaches", () => revision.WorkDetached.IsCompleted);
            AddStep("assert old source visual detached exactly at replacement", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(parent.IsAlive, Is.False);
                    Assert.That(initialVisual?.DisposeCount, Is.EqualTo(1));
                    Assert.That(host.Drawable, Is.SameAs(Volatile.Read(ref lastResolvedVisual)));
                    Assert.That(revision.WorkDetached.IsCompletedSuccessfully, Is.True);
                });

                Assert.That(Remove(parent, disposeImmediately: true), Is.True);
            });
        }

        [Test]
        public void TestSkinnableSourceInvalidationPublishesWhileOwnerIsNonPresent()
        {
            importAndSelect("skinnable non-present source invalidation", () => createOsk(null));

            SkinCurrentRevision revision = null!;
            NonRemovingContainer parent = null!;
            InvalidatableSkinDependencyHost sourceHost = null!;
            SkinnableContainer host = null!;
            int admissionCount = 0;
            int publicationCount = 0;
            int initialAdmissionCount = 0;
            int initialPublicationCount = 0;

            AddStep("mount visible invalidatable skinnable source", () =>
            {
                revision = skinManager.CurrentRevision;
                host = new SkinnableContainer(
                    new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.Results))
                {
                    RevisionWorkAdmissionTestHook = () => Interlocked.Increment(ref admissionCount),
                };
                host.OnComponentsLoaded += _ => Interlocked.Increment(ref publicationCount);
                sourceHost = new InvalidatableSkinDependencyHost(skinManager, host);
                Add(parent = new NonRemovingContainer { Child = sourceHost });
            });
            AddUntilStep("wait for initial skinnable source publication", () =>
                Volatile.Read(ref admissionCount) > 0
                && Volatile.Read(ref publicationCount) > 0
                && host.ComponentsLoaded
                && revision.WorkDetached.IsCompleted);
            AddStep("capture initial skinnable source generation", () =>
            {
                initialAdmissionCount = Volatile.Read(ref admissionCount);
                initialPublicationCount = Volatile.Read(ref publicationCount);
            });
            AddStep("move skinnable source outside lifetime", () =>
                parent.LifetimeStart = Clock.CurrentTime + 60_000);
            AddUntilStep("wait for skinnable source to stop updating", () => !parent.IsAlive);
            AddStep("invalidate exact skinnable source while non-present", () => sourceHost.Invalidate());
            AddUntilStep("visibility-independent skinnable source rebuild publishes", () =>
                Volatile.Read(ref admissionCount) > initialAdmissionCount
                && Volatile.Read(ref publicationCount) > initialPublicationCount
                && host.ComponentsLoaded);
            AddUntilStep("invalidated skinnable work lease detaches", () => revision.WorkDetached.IsCompleted);
            AddStep("assert non-present skinnable source converged", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(parent.IsAlive, Is.False);
                    Assert.That(host.ComponentsLoaded, Is.True);
                    Assert.That(revision.WorkDetached.IsCompletedSuccessfully, Is.True);
                });

                Assert.That(Remove(parent, disposeImmediately: true), Is.True);
            });
        }

        [Test]
        public void TestBackgroundSourceInvalidationRejectsStaleBmsGenerationBeforeFreshPublication()
        {
            importAndSelect("BMS background source invalidation", () => createOsk(null));

            SkinCurrentRevision revision = null!;
            NonRemovingContainer parent = null!;
            InvalidatableSkinDependencyHost sourceHost = null!;
            BmsAsyncNoteDrawable host = null!;
            DisposalTrackingDrawable? initialVisual = null;
            DisposalTrackingDrawable? staleVisual = null;
            DisposalTrackingDrawable? freshVisual = null;
            Task backgroundInvalidation = null!;
            int resolverCalls = 0;
            var staleResolverStarted = new ManualResetEventSlim();
            var allowStaleResolver = new ManualResetEventSlim();
            var freshResolverStarted = new ManualResetEventSlim();
            var allowFreshResolver = new ManualResetEventSlim();
            var sourceChangeScheduled = new ManualResetEventSlim();
            var allowSourceChangeReturn = new ManualResetEventSlim();

            AddStep("mount background-invalidatable BMS owner", () =>
            {
                revision = skinManager.CurrentRevision;
                host = new BmsAsyncNoteDrawable(createLookup(BmsNoteSkinElements.Note))
                {
                    DrawableResolver = (_, _) =>
                    {
                        int call = Interlocked.Increment(ref resolverCalls);
                        var resolved = new DisposalTrackingDrawable();

                        if (call == 1)
                        {
                            Volatile.Write(ref initialVisual, resolved);
                            return resolved;
                        }

                        if (call == 2)
                        {
                            Volatile.Write(ref staleVisual, resolved);
                            staleResolverStarted.Set();

                            if (!allowStaleResolver.Wait(TimeSpan.FromSeconds(10)))
                                throw new TimeoutException();

                            return resolved;
                        }

                        Volatile.Write(ref freshVisual, resolved);

                        freshResolverStarted.Set();

                        if (!allowFreshResolver.Wait(TimeSpan.FromSeconds(10)))
                            throw new TimeoutException();

                        return resolved;
                    },
                };
                sourceHost = new InvalidatableSkinDependencyHost(skinManager, host);
                Add(parent = new NonRemovingContainer { Child = sourceHost });
            });
            AddUntilStep("wait for initial BMS generation", () =>
                Volatile.Read(ref initialVisual) != null
                && ReferenceEquals(host.Drawable, Volatile.Read(ref initialVisual))
                && revision.WorkDetached.IsCompleted);
            AddStep("start stale BMS generation", () => sourceHost.Invalidate());
            AddUntilStep("hold stale BMS resolver", () => staleResolverStarted.IsSet);
            AddStep("gate background BMS event after dispatch", () =>
            {
                host.SkinChangeScheduledTestHook = () =>
                {
                    sourceChangeScheduled.Set();

                    if (!allowSourceChangeReturn.Wait(TimeSpan.FromSeconds(10)))
                        throw new TimeoutException();
                };
                backgroundInvalidation = Task.Run(sourceHost.Invalidate);
            });
            AddUntilStep("background BMS event reaches dispatch gate", () => sourceChangeScheduled.IsSet);
            AddUntilStep("hold fresh BMS resolver before publication", () => freshResolverStarted.IsSet);
            AddStep("release background BMS event", () => allowSourceChangeReturn.Set());
            AddUntilStep("wait for background BMS invalidation", () => backgroundInvalidation.IsCompleted);
            AddStep("release fresh BMS generation after all source handlers", () => allowFreshResolver.Set());
            AddUntilStep("fresh BMS generation publishes after event returns", () =>
                Volatile.Read(ref freshVisual) != null
                && ReferenceEquals(host.Drawable, Volatile.Read(ref freshVisual)));
            AddStep("release stale BMS generation", () => allowStaleResolver.Set());
            AddUntilStep("stale BMS generation is reclaimed", () =>
                Volatile.Read(ref staleVisual)?.DisposeCount == 1
                && revision.WorkDetached.IsCompleted);
            AddStep("assert stale BMS generation never won publication", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(backgroundInvalidation.IsCompletedSuccessfully, Is.True);
                    Assert.That(Volatile.Read(ref resolverCalls), Is.EqualTo(3));
                    Assert.That(host.Drawable, Is.SameAs(Volatile.Read(ref freshVisual)));
                    Assert.That(host.Drawable, Is.Not.SameAs(Volatile.Read(ref staleVisual)));
                    Assert.That(Volatile.Read(ref staleVisual)?.Parent, Is.Null);
                    Assert.That(Volatile.Read(ref staleVisual)?.DisposeCount, Is.EqualTo(1));
                    Assert.That(Volatile.Read(ref initialVisual)?.DisposeCount, Is.EqualTo(1));
                });

                Assert.That(Remove(parent, disposeImmediately: true), Is.True);
                staleResolverStarted.Dispose();
                allowStaleResolver.Dispose();
                freshResolverStarted.Dispose();
                allowFreshResolver.Dispose();
                sourceChangeScheduled.Dispose();
                allowSourceChangeReturn.Dispose();
            });
        }

        [Test]
        public void TestBackgroundSourceInvalidationRejectsStaleSkinnableGenerationBeforeFreshPublication()
        {
            importAndSelect("skinnable background source invalidation", () => createOsk(null));

            SkinCurrentRevision revision = null!;
            NonRemovingContainer parent = null!;
            InvalidatableSkinDependencyHost sourceHost = null!;
            SkinnableContainer host = null!;
            GatedDisposalTrackingContainer stale = null!;
            Task backgroundInvalidation = null!;
            int admissionCount = 0;
            int staleAdmissionCount = 0;
            int stalePublicationCount = 0;
            var staleLoadStarted = new ManualResetEventSlim();
            var allowStaleLoad = new ManualResetEventSlim();
            var sourceChangeScheduled = new ManualResetEventSlim();
            var allowSourceChangeReturn = new ManualResetEventSlim();

            AddStep("mount background-invalidatable skinnable owner", () =>
            {
                revision = skinManager.CurrentRevision;
                host = new SkinnableContainer(
                    new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.Results))
                {
                    RevisionWorkAdmissionTestHook = () => Interlocked.Increment(ref admissionCount),
                };
                host.OnComponentsLoaded += _ =>
                {
                    if (ReferenceEquals(stale?.Parent, host))
                        Interlocked.Increment(ref stalePublicationCount);
                };
                sourceHost = new InvalidatableSkinDependencyHost(skinManager, host);
                Add(parent = new NonRemovingContainer { Child = sourceHost });
            });
            AddUntilStep("wait for initial skinnable generation", () =>
                host.ComponentsLoaded
                && Volatile.Read(ref admissionCount) > 0
                && revision.WorkDetached.IsCompleted);
            AddStep("start stale skinnable generation", () =>
            {
                host.Reload(stale = new GatedDisposalTrackingContainer(staleLoadStarted, allowStaleLoad));
                staleAdmissionCount = Volatile.Read(ref admissionCount);
            });
            AddUntilStep("hold stale skinnable loader", () => staleLoadStarted.IsSet);
            AddStep("cancel stale skinnable work before global callback can pump", () =>
            {
                host.SkinChangeScheduledTestHook = () =>
                {
                    sourceChangeScheduled.Set();

                    if (!allowSourceChangeReturn.Wait(TimeSpan.FromSeconds(10)))
                        throw new TimeoutException();
                };
                backgroundInvalidation = Task.Run(sourceHost.Invalidate);

                try
                {
                    Assert.That(
                        SpinWait.SpinUntil(() => sourceChangeScheduled.IsSet, TimeSpan.FromSeconds(10)),
                        Is.True);
                    allowStaleLoad.Set();
                    Assert.That(
                        SpinWait.SpinUntil(() => stale.DisposeCount == 1, TimeSpan.FromSeconds(10)),
                        Is.True,
                        "Source invalidation must cancel and reclaim stale work before its scheduled skin callback can run.");
                }
                finally
                {
                    allowSourceChangeReturn.Set();
                }

                Assert.That(backgroundInvalidation.Wait(TimeSpan.FromSeconds(10)), Is.True);
                Assert.That(backgroundInvalidation.IsCompletedSuccessfully, Is.True);
            });
            AddUntilStep("fresh skinnable generation publishes after event returns", () =>
                Volatile.Read(ref admissionCount) > staleAdmissionCount
                && host.ComponentsLoaded
                && revision.WorkDetached.IsCompleted);
            AddStep("assert stale skinnable generation never published", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(backgroundInvalidation.IsCompletedSuccessfully, Is.True);
                    Assert.That(Volatile.Read(ref stalePublicationCount), Is.Zero);
                    Assert.That(stale.Parent, Is.Null);
                    Assert.That(stale.DisposeCount, Is.EqualTo(1));
                    Assert.That(host.ComponentsLoaded, Is.True);
                });

                Assert.That(Remove(parent, disposeImmediately: true), Is.True);
                staleLoadStarted.Dispose();
                allowStaleLoad.Dispose();
                sourceChangeScheduled.Dispose();
                allowSourceChangeReturn.Dispose();
            });
        }

        [Test]
        public void TestQueuedGlobalSkinChangeIsCancelledByOwnerDisposal()
        {
            importAndSelect("BMS queued source disposal", () => createOsk(null));

            NonRemovingContainer parent = null!;
            InvalidatableSkinDependencyHost sourceHost = null!;
            BmsAsyncNoteDrawable host = null!;
            DisposalTrackingDrawable? initialVisual = null;
            int resolverCalls = 0;
            int skinChangedCallbacks = 0;
            int callbackBaseline = 0;
            var sourceChangeScheduled = new ManualResetEventSlim();
            var allowSourceChangeReturn = new ManualResetEventSlim();

            AddStep("mount disposable BMS source owner", () =>
            {
                host = new BmsAsyncNoteDrawable(createLookup(BmsNoteSkinElements.Note))
                {
                    DrawableResolver = (_, _) =>
                    {
                        Interlocked.Increment(ref resolverCalls);
                        return Volatile.Read(ref initialVisual)
                               ?? Interlocked.CompareExchange(
                                   ref initialVisual,
                                   new DisposalTrackingDrawable(),
                                   null)
                               ?? Volatile.Read(ref initialVisual)!;
                    },
                };
                host.OnSkinChanged += () => Interlocked.Increment(ref skinChangedCallbacks);
                sourceHost = new InvalidatableSkinDependencyHost(skinManager, host);
                Add(parent = new NonRemovingContainer { Child = sourceHost });
            });
            AddUntilStep("wait for disposable BMS initial generation", () =>
                Volatile.Read(ref initialVisual) != null
                && ReferenceEquals(host.Drawable, Volatile.Read(ref initialVisual)));
            AddStep("queue source event then dispose before global callback", () =>
            {
                callbackBaseline = Volatile.Read(ref skinChangedCallbacks);
                host.SkinChangeScheduledTestHook = () =>
                {
                    sourceChangeScheduled.Set();

                    if (!allowSourceChangeReturn.Wait(TimeSpan.FromSeconds(10)))
                        throw new TimeoutException();
                };
                Task invalidation = Task.Run(sourceHost.Invalidate);

                try
                {
                    Assert.That(
                        SpinWait.SpinUntil(() => sourceChangeScheduled.IsSet, TimeSpan.FromSeconds(10)),
                        Is.True);
                    Assert.That(Remove(parent, disposeImmediately: true), Is.True);
                }
                finally
                {
                    allowSourceChangeReturn.Set();
                }

                Assert.That(invalidation.Wait(TimeSpan.FromSeconds(10)), Is.True);
                Assert.That(invalidation.IsCompletedSuccessfully, Is.True);
            });
            AddWaitStep("allow cancelled global callback slot to pass", 2);
            AddStep("assert disposed owner admitted no queued rebuild", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(Volatile.Read(ref resolverCalls), Is.EqualTo(1));
                    Assert.That(Volatile.Read(ref skinChangedCallbacks), Is.EqualTo(callbackBaseline));
                    Assert.That(Volatile.Read(ref initialVisual)?.DisposeCount, Is.EqualTo(1));
                });

                sourceChangeScheduled.Dispose();
                allowSourceChangeReturn.Dispose();
            });
        }

        [Test]
        public void TestQueuedGlobalSkinnableSkinChangeCannotAdmitOrAdoptAfterPublicationShutdown()
        {
            SkinManager isolatedManager = null!;
            SkinManagerDependencyHost dependencyHost = null!;
            InvalidatableSkinDependencyHost sourceHost = null!;
            SkinnableContainer host = null!;
            int admissionCount = 0;
            int admissionBaseline = 0;
            int skinChangedCallbacks = 0;
            int callbackBaseline = 0;
            var sourceChangeScheduled = new ManualResetEventSlim();
            var allowSourceChangeReturn = new ManualResetEventSlim();

            AddStep("mount skinnable owner under isolated skin manager", () =>
            {
                isolatedManager = new SkinManager(
                    LocalStorage,
                    Realm,
                    asyncOwnershipGameHost,
                    Resources,
                    Audio,
                    Scheduler);
                host = new SkinnableContainer(
                    new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.Results))
                {
                    RevisionWorkAdmissionTestHook = () => Interlocked.Increment(ref admissionCount),
                };
                host.OnSkinChanged += () => Interlocked.Increment(ref skinChangedCallbacks);
                sourceHost = new InvalidatableSkinDependencyHost(isolatedManager, host);
                Add(dependencyHost = new SkinManagerDependencyHost(isolatedManager, sourceHost));
            });
            AddUntilStep("wait for isolated skinnable initial generation", () =>
                host.ComponentsLoaded
                && Volatile.Read(ref admissionCount) > 0
                && isolatedManager.CurrentRevision.WorkDetached.IsCompleted);
            AddStep("queue source event then shut publication before callback", () =>
            {
                admissionBaseline = Volatile.Read(ref admissionCount);
                callbackBaseline = Volatile.Read(ref skinChangedCallbacks);
                host.SkinChangeScheduledTestHook = () =>
                {
                    sourceChangeScheduled.Set();

                    if (!allowSourceChangeReturn.Wait(TimeSpan.FromSeconds(10)))
                        throw new TimeoutException();
                };
                Task invalidation = Task.Run(sourceHost.Invalidate);

                try
                {
                    Assert.That(
                        SpinWait.SpinUntil(() => sourceChangeScheduled.IsSet, TimeSpan.FromSeconds(10)),
                        Is.True);
                    isolatedManager.ShutdownManagedFolderMutations();
                }
                finally
                {
                    allowSourceChangeReturn.Set();
                }

                Assert.That(invalidation.Wait(TimeSpan.FromSeconds(10)), Is.True);
                Assert.That(invalidation.IsCompletedSuccessfully, Is.True);
            });
            AddWaitStep("allow shutdown-claimed global callback slot to pass", 2);
            AddStep("assert shutdown admitted no queued rebuild", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(Volatile.Read(ref admissionCount), Is.EqualTo(admissionBaseline));
                    Assert.That(Volatile.Read(ref skinChangedCallbacks), Is.EqualTo(callbackBaseline));
                    Assert.That(host.ComponentsLoaded, Is.True);
                    Assert.That(isolatedManager.CurrentRevision.WorkDetached.IsCompletedSuccessfully, Is.True);
                });

                Assert.That(Remove(dependencyHost, disposeImmediately: true), Is.True);
                isolatedManager.ShutdownManagedFolderMutations();
                sourceChangeScheduled.Dispose();
                allowSourceChangeReturn.Dispose();
            });
        }

        [Test]
        public void TestSkinnableContainerPublishesAfterParentBecomesNonPresent()
        {
            importAndSelect("skinnable non-present callback", () => createOsk(null));

            SkinCurrentRevision revision = null!;
            NonRemovingContainer parent = null!;
            SkinnableContainer host = null!;
            GatedLoadContainer provisional = null!;
            var prepareStarted = new ManualResetEventSlim();
            var allowPrepare = new ManualResetEventSlim();

            AddStep("mount visible real skinnable owner", () =>
            {
                revision = skinManager.CurrentRevision;
                Add(parent = new NonRemovingContainer
                {
                    Child = host = new SkinnableContainer(
                        new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.Results)),
                });
            });
            AddUntilStep("wait for initial skinnable publication", () =>
                host.IsLoaded && host.ComponentsLoaded && revision.WorkDetached.IsCompleted);
            AddStep("start gated skinnable replacement", () =>
                host.Reload(provisional = new GatedLoadContainer(prepareStarted, allowPrepare)));
            AddUntilStep("wait for real skinnable prepare", () => prepareStarted.IsSet);
            AddStep("move skinnable parent outside its lifetime", () =>
                parent.LifetimeStart = Clock.CurrentTime + 60_000);
            AddUntilStep("wait for skinnable parent to stop updating", () => !parent.IsAlive);
            AddStep("finish skinnable prepare after parent stopped updating", () => allowPrepare.Set());
            AddUntilStep("visibility-independent skinnable barrier publishes", () =>
                host.ComponentsLoaded && provisional.Parent == host);
            AddUntilStep("exact skinnable work lease detaches", () => revision.WorkDetached.IsCompleted);
            AddStep("assert non-present skinnable owner published on update thread", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(parent.IsAlive, Is.False);
                    Assert.That(provisional.Parent, Is.SameAs(host));
                    Assert.That(revision.WorkDetached.IsCompletedSuccessfully, Is.True);
                });

                Assert.That(Remove(parent, disposeImmediately: true), Is.True);
                prepareStarted.Dispose();
                allowPrepare.Dispose();
            });
        }

        [Test]
        public void TestSkinnableContainerSchedulerFaultReclaimsExactRevisionWorkOnce()
        {
            importAndSelect("skinnable callback fault", () => createOsk(null));

            SkinCurrentRevision revision = null!;
            SkinnableContainer host = null!;
            ThrowingLoadContainer provisional = null!;
            Task workDetached = null!;
            int participantBaseline = 0;
            var callbackScheduler = new Scheduler();

            AddStep("mount real skinnable container", () =>
            {
                revision = skinManager.CurrentRevision;
                Add(host = new SkinnableContainer(
                    new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.Results)));
            });
            AddUntilStep("wait for initial skinnable generation", () =>
                host.IsLoaded
                && host.ComponentsLoaded
                && revision.WorkDetached.IsCompleted);
            AddStep("start faulting skinnable replacement", () =>
            {
                participantBaseline = revision.ParticipantLeaseCount;
                host.ContentLoadCallbackScheduler = callbackScheduler;
                host.Reload(provisional = new ThrowingLoadContainer());
                workDetached = revision.WorkDetached;

                Assert.That(workDetached.IsCompleted, Is.False,
                    "The exact revision work lease must remain held through the queued framework callback.");
            });
            AddStep("wait for framework callback queue", () =>
            {
                Assert.That(
                    SpinWait.SpinUntil(
                        () => host.PendingContentLoadTask?.IsCompleted == true && callbackScheduler.HasPendingTasks,
                        TimeSpan.FromSeconds(10)),
                    Is.True);
            });
            AddStep("surface callback fault then run ownership sentinel", () =>
            {
                Assert.Throws<InvalidOperationException>(() => callbackScheduler.Update());
                callbackScheduler.Update();
            });
            AddUntilStep("wait for exact skinnable reclaim", () =>
                provisional.DisposeCount == 1
                && workDetached.IsCompleted
                && revision.WorkDetached.IsCompleted);
            AddStep("assert skinnable fault is unsplit and exactly once", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(host.ComponentsLoaded, Is.False);
                    Assert.That(provisional.DisposeCount, Is.EqualTo(1));
                    Assert.That(revision.ParticipantLeaseCount, Is.EqualTo(participantBaseline));
                });

                Assert.That(Remove(host, disposeImmediately: true), Is.True);
                Assert.That(provisional.DisposeCount, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestBmsAsyncNoteSchedulerFaultReclaimsOuterAndInnerRevisionWorkOnce()
        {
            importAndSelect("BMS async callback fault", () => createOsk(null));

            SkinCurrentRevision revision = null!;
            BmsAsyncNoteDrawable host = null!;
            ThrowingLoadDrawable provisionalVisual = null!;
            Task workDetached = null!;
            int participantBaseline = 0;
            int visualCreationCount = 0;
            var callbackScheduler = new Scheduler();

            AddStep("mount real BMS async note host", () =>
            {
                revision = skinManager.CurrentRevision;
                participantBaseline = revision.ParticipantLeaseCount;
                host = new BmsAsyncNoteDrawable(createLookup(BmsNoteSkinElements.Note))
                {
                    LoadCallbackScheduler = callbackScheduler,
                    DrawableResolver = (_, _) =>
                    {
                        Interlocked.Increment(ref visualCreationCount);
                        return provisionalVisual = new ThrowingLoadDrawable();
                    },
                };
                Add(host);
            });
            AddStep("wait for BMS framework callback queue", () =>
            {
                Assert.That(
                    SpinWait.SpinUntil(
                        () => Volatile.Read(ref visualCreationCount) == 1
                              && host.PendingLoadTask?.IsCompleted == true
                              && callbackScheduler.HasPendingTasks,
                        TimeSpan.FromSeconds(10)),
                    Is.True);

                workDetached = revision.WorkDetached;
                Assert.That(workDetached.IsCompleted, Is.False,
                    "Both the BMS outer worker and its transferred inner work lease must remain held through callback dispatch.");
            });
            AddStep("surface BMS callback fault then run ownership sentinel", () =>
            {
                Assert.Throws<InvalidOperationException>(() => callbackScheduler.Update());
                callbackScheduler.Update();
            });
            AddUntilStep("wait for BMS outer and inner reclaim", () =>
                provisionalVisual.DisposeCount == 1
                && workDetached.IsCompleted
                && revision.WorkDetached.IsCompleted);
            AddStep("assert protected visual and exact participant teardown", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(host.Drawable, Is.TypeOf<DefaultBmsNoteDisplay>());
                    Assert.That(provisionalVisual.DisposeCount, Is.EqualTo(1));
                    Assert.That(revision.ParticipantLeaseCount, Is.EqualTo(participantBaseline + 1));
                });

                Assert.That(Remove(host, disposeImmediately: true), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(provisionalVisual.DisposeCount, Is.EqualTo(1));
                    Assert.That(revision.ParticipantLeaseCount, Is.EqualTo(participantBaseline));
                });
            });
        }

        private sealed partial class ThrowingLoadContainer : Container
        {
            private int disposeCount;

            internal int DisposeCount => Volatile.Read(ref disposeCount);

            [BackgroundDependencyLoader]
            private void failLoad() => throw new InvalidOperationException("Intentional skinnable callback load fault.");

            protected override void Dispose(bool isDisposing)
            {
                Interlocked.Increment(ref disposeCount);
                base.Dispose(isDisposing);
            }
        }

        private sealed partial class GatedLoadContainer : Container
        {
            private readonly ManualResetEventSlim loadStarted;
            private readonly ManualResetEventSlim allowLoad;

            internal GatedLoadContainer(ManualResetEventSlim loadStarted, ManualResetEventSlim allowLoad)
            {
                this.loadStarted = loadStarted;
                this.allowLoad = allowLoad;
            }

            [BackgroundDependencyLoader]
            private void gateLoad()
            {
                loadStarted.Set();

                if (!allowLoad.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException();
            }
        }

        private sealed partial class GatedDisposalTrackingContainer : Container
        {
            private readonly ManualResetEventSlim loadStarted;
            private readonly ManualResetEventSlim allowLoad;
            private int disposeCount;

            internal int DisposeCount => Volatile.Read(ref disposeCount);

            internal GatedDisposalTrackingContainer(ManualResetEventSlim loadStarted, ManualResetEventSlim allowLoad)
            {
                this.loadStarted = loadStarted;
                this.allowLoad = allowLoad;
            }

            [BackgroundDependencyLoader]
            private void gateLoad()
            {
                loadStarted.Set();

                if (!allowLoad.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException();
            }

            protected override void Dispose(bool isDisposing)
            {
                Interlocked.Increment(ref disposeCount);
                base.Dispose(isDisposing);
            }
        }

        private sealed partial class NonRemovingContainer : Container
        {
            public override bool RemoveWhenNotAlive => false;
        }

        private sealed partial class InvalidatableSkinDependencyHost : SkinProvidingContainer
        {
            internal InvalidatableSkinDependencyHost(SkinManager skinManager, Drawable child)
                : base(skinManager.CurrentSkin.Value)
            {
                Child = child;
            }

            internal void Invalidate() => TriggerSourceChanged();
        }

        private sealed partial class SkinManagerDependencyHost : Container
        {
            private readonly SkinManager manager;

            internal SkinManagerDependencyHost(SkinManager manager, Drawable child)
            {
                this.manager = manager;
                Child = child;
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
                dependencies.Cache(manager);
                return dependencies;
            }
        }

        private sealed partial class ThrowingLoadDrawable : Drawable
        {
            private int disposeCount;

            internal int DisposeCount => Volatile.Read(ref disposeCount);

            [BackgroundDependencyLoader]
            private void failLoad() => throw new InvalidOperationException("Intentional BMS note callback load fault.");

            protected override void Dispose(bool isDisposing)
            {
                Interlocked.Increment(ref disposeCount);
                base.Dispose(isDisposing);
            }
        }

        private sealed partial class DisposalTrackingDrawable : Drawable
        {
            private int disposeCount;

            internal int DisposeCount => Volatile.Read(ref disposeCount);

            protected override void Dispose(bool isDisposing)
            {
                Interlocked.Increment(ref disposeCount);
                base.Dispose(isDisposing);
            }
        }
    }
}
