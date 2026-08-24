// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Threading;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [Test]
        public void TestShutdownClaimsBmsOwnerAcrossWorkAdmissionWindow()
        {
            SkinCurrentRevision revision = null!;
            LocalSkinDependencyHost ownerHost = null!;
            BmsAsyncNoteDrawable noteHost = null!;
            Drawable initialVisual = null!;
            ShutdownTrackingDrawable? provisionalVisual = null;
            Task? shutdown = null;
            int participantBaseline = 0;
            int participantDuringAdmission = 0;
            int admissionCount = 0;
            int visualCreationCount = 0;
            var heldCallbackScheduler = new Scheduler();

            AddStep("mount BMS owner held before pending publication", () =>
            {
                revision = manager.CurrentRevision;
                participantBaseline = revision.ParticipantLeaseCount;
                noteHost = new BmsAsyncNoteDrawable(
                    new BmsNoteSkinLookup(BmsNoteSkinElements.Note, 1, false, BmsKeymode.Key7K))
                {
                    LoadCallbackScheduler = heldCallbackScheduler,
                    DrawableResolver = (_, _) =>
                    {
                        Interlocked.Increment(ref visualCreationCount);
                        return provisionalVisual = new ShutdownTrackingDrawable();
                    },
                    RevisionWorkAdmissionTestHook = () =>
                    {
                        Interlocked.Increment(ref admissionCount);
                        Volatile.Write(ref participantDuringAdmission, revision.ParticipantLeaseCount);
                        Volatile.Write(ref shutdown, Task.Run(manager.ShutdownManagedFolderMutations));

                        if (!SpinWait.SpinUntil(currentRevisionWorkAdmissionIsClosed, TimeSpan.FromSeconds(10)))
                            throw new TimeoutException("Timed out waiting for shutdown to close revision work admission.");
                    },
                };
                initialVisual = noteHost.Drawable!;
                Add(ownerHost = new LocalSkinDependencyHost(manager, noteHost));
            });
            AddUntilStep("shutdown joins admitted BMS owner without callback update", () =>
                Volatile.Read(ref shutdown)?.IsCompleted == true
                && revision.WorkDetached.IsCompleted
                && revision.ParticipantLeaseCount == participantBaseline + 1);
            AddStep("assert BMS admission rollback kept the real visual participant", () =>
            {
                ShutdownTrackingDrawable? createdVisual = Volatile.Read(ref provisionalVisual);

                Assert.Multiple(() =>
                {
                    Assert.That(shutdown!.IsCompletedSuccessfully, Is.True);
                    Assert.That(admissionCount, Is.EqualTo(1));
                    Assert.That(participantDuringAdmission, Is.EqualTo(participantBaseline + 2),
                        "Ready admission must release the initial-load blocker before the real visual participant acquires its work lease.");
                    Assert.That(visualCreationCount, Is.InRange(0, 1));
                    Assert.That(createdVisual?.DisposeCount ?? 1, Is.EqualTo(1),
                        "Any provisional visual reached by the loader must be reclaimed exactly once.");
                    Assert.That(noteHost.PendingLoadTask, Is.Null);
                    Assert.That(noteHost.Drawable, Is.SameAs(initialVisual),
                        "The admission-raced provisional must never publish over the protected visual.");
                    Assert.That(ownerHost.Parent, Is.Not.Null);
                    Assert.That(revision.WorkDetached.IsCompletedSuccessfully, Is.True,
                        "Both the outer and transferred work leases must release through the real owner path.");
                    Assert.That(revision.ParticipantLeaseCount, Is.EqualTo(participantBaseline + 1),
                        "Shutdown must not impersonate the still-attached visual participant.");
                    Assert.That(revision.ConsumersDetached.IsCompleted, Is.False);
                });

                // Deliberately never update heldCallbackScheduler. Shutdown must not depend on that callback queue.
                Assert.That(Remove(ownerHost, disposeImmediately: true), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(revision.ParticipantLeaseCount, Is.EqualTo(participantBaseline));
                    Assert.That(createdVisual?.DisposeCount ?? 1, Is.EqualTo(1));
                });
            });
        }

        [Test]
        public void TestShutdownClaimsSkinnableOwnerAcrossWorkAdmissionWindow()
        {
            SkinCurrentRevision revision = null!;
            LocalSkinDependencyHost ownerHost = null!;
            SkinnableContainer host = null!;
            ShutdownTrackingContainer provisional = null!;
            Task? shutdown = null;
            int participantBeforeMount = 0;
            int participantBaseline = 0;
            int participantDuringAdmission = 0;
            int admissionCount = 0;
            int publicationCount = 0;
            var heldCallbackScheduler = new Scheduler();

            AddStep("mount isolated real skinnable participant", () =>
            {
                revision = manager.CurrentRevision;
                participantBeforeMount = revision.ParticipantLeaseCount;
                Add(ownerHost = new LocalSkinDependencyHost(
                    manager,
                    host = new SkinnableContainer(
                        new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.Results))));
            });
            AddUntilStep("wait for initial skinnable generation", () =>
                host.IsLoaded
                && host.ComponentsLoaded
                && revision.WorkDetached.IsCompleted
                && revision.ParticipantLeaseCount == participantBeforeMount + 1);
            AddStep("hold skinnable owner before pending publication", () =>
            {
                participantBaseline = revision.ParticipantLeaseCount;
                host.OnComponentsLoaded += _ => Interlocked.Increment(ref publicationCount);
                host.ContentLoadCallbackScheduler = heldCallbackScheduler;
                host.RevisionWorkAdmissionTestHook = () =>
                {
                    Interlocked.Increment(ref admissionCount);
                    Volatile.Write(ref participantDuringAdmission, revision.ParticipantLeaseCount);
                    Volatile.Write(ref shutdown, Task.Run(manager.ShutdownManagedFolderMutations));

                    if (!SpinWait.SpinUntil(currentRevisionWorkAdmissionIsClosed, TimeSpan.FromSeconds(10)))
                        throw new TimeoutException("Timed out waiting for shutdown to close revision work admission.");
                };
                host.Reload(provisional = new ShutdownTrackingContainer());
            });
            AddUntilStep("shutdown joins admitted skinnable owner without callback update", () =>
                Volatile.Read(ref shutdown)?.IsCompleted == true
                && revision.WorkDetached.IsCompleted
                && provisional.DisposeCount == 1
                && revision.ParticipantLeaseCount == participantBaseline);
            AddStep("assert skinnable admission rollback kept the real participant", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(shutdown!.IsCompletedSuccessfully, Is.True);
                    Assert.That(admissionCount, Is.EqualTo(1));
                    Assert.That(participantDuringAdmission, Is.EqualTo(participantBaseline + 1),
                        "The acquired work lease must be visible before pending ownership is published.");
                    Assert.That(publicationCount, Is.Zero);
                    Assert.That(host.PendingContentLoadTask, Is.Null);
                    Assert.That(host.ComponentsLoaded, Is.False,
                        "The admission-raced provisional must never publish into the live container.");
                    Assert.That(provisional.Parent, Is.Null);
                    Assert.That(provisional.DisposeCount, Is.EqualTo(1));
                    Assert.That(ownerHost.Parent, Is.Not.Null);
                    Assert.That(revision.WorkDetached.IsCompletedSuccessfully, Is.True);
                    Assert.That(revision.ParticipantLeaseCount, Is.EqualTo(participantBaseline),
                        "Shutdown must release only work while the real visual participant remains attached.");
                    Assert.That(revision.ConsumersDetached.IsCompleted, Is.False);
                });

                // Deliberately never update heldCallbackScheduler. Its callbacks cannot be a shutdown join dependency.
                Assert.That(Remove(ownerHost, disposeImmediately: true), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(revision.ParticipantLeaseCount, Is.EqualTo(participantBeforeMount));
                    Assert.That(provisional.DisposeCount, Is.EqualTo(1),
                        "Parent detach must not double-dispose an already reclaimed provisional.");
                });
            });
        }

        [Test]
        public void TestShutdownClaimsLiveBmsAsyncOwnerAndJoinsWithoutCallbackScheduler()
        {
            SkinCurrentRevision revision = null!;
            LocalSkinDependencyHost ownerHost = null!;
            BmsAsyncNoteDrawable noteHost = null!;
            ShutdownTrackingDrawable provisionalVisual = null!;
            Task shutdown = null!;
            int participantBaseline = 0;
            int visualCreationCount = 0;
            var heldCallbackScheduler = new Scheduler();

            AddStep("mount isolated live BMS owner with held callback", () =>
            {
                revision = manager.CurrentRevision;
                participantBaseline = revision.ParticipantLeaseCount;
                noteHost = new BmsAsyncNoteDrawable(
                    new BmsNoteSkinLookup(BmsNoteSkinElements.Note, 1, false, BmsKeymode.Key7K))
                {
                    LoadCallbackScheduler = heldCallbackScheduler,
                    DrawableResolver = (_, _) =>
                    {
                        Interlocked.Increment(ref visualCreationCount);
                        return provisionalVisual = new ShutdownTrackingDrawable();
                    },
                };
                Add(ownerHost = new LocalSkinDependencyHost(manager, noteHost));
            });
            AddStep("wait for completed BMS load behind undrained callback", () =>
            {
                Assert.That(
                    SpinWait.SpinUntil(
                        () => Volatile.Read(ref visualCreationCount) == 1
                              && noteHost.PendingLoadTask?.IsCompleted == true
                              && heldCallbackScheduler.HasPendingTasks
                              && revision.WorkDetached.IsCompleted == false,
                        TimeSpan.FromSeconds(10)),
                    Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(noteHost.Drawable, Is.TypeOf<DefaultBmsNoteDisplay>());
                    Assert.That(provisionalVisual.DisposeCount, Is.Zero);
                    Assert.That(revision.ParticipantLeaseCount, Is.EqualTo(participantBaseline + 3),
                        "One visual participant plus the outer and transferred work leases must remain exact.");
                });
            });
            AddStep("shutdown isolated manager while real BMS host remains attached", () =>
                shutdown = Task.Run(manager.ShutdownManagedFolderMutations));
            AddUntilStep("wait for exact BMS owner cancellation and work join", () =>
                shutdown.IsCompleted
                && revision.WorkDetached.IsCompleted
                && provisionalVisual.DisposeCount == 1);
            AddStep("assert BMS shutdown did not fake visual detach or drain callback", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(shutdown.IsCompletedSuccessfully, Is.True);
                    Assert.That(heldCallbackScheduler.HasPendingTasks, Is.True);
                    Assert.That(noteHost.Parent, Is.Not.Null);
                    Assert.That(noteHost.Drawable, Is.TypeOf<DefaultBmsNoteDisplay>());
                    Assert.That(provisionalVisual.DisposeCount, Is.EqualTo(1));
                    Assert.That(revision.WorkDetached.IsCompletedSuccessfully, Is.True);
                    Assert.That(revision.ParticipantLeaseCount, Is.EqualTo(participantBaseline + 1));
                    Assert.That(revision.ConsumersDetached.IsCompleted, Is.False);
                });

                heldCallbackScheduler.Update();
                heldCallbackScheduler.Update();

                Assert.Multiple(() =>
                {
                    Assert.That(heldCallbackScheduler.HasPendingTasks, Is.False);
                    Assert.That(noteHost.Drawable, Is.TypeOf<DefaultBmsNoteDisplay>());
                    Assert.That(provisionalVisual.DisposeCount, Is.EqualTo(1));
                });

                Assert.That(Remove(ownerHost, disposeImmediately: true), Is.True);
                Assert.That(revision.ParticipantLeaseCount, Is.EqualTo(participantBaseline));
            });
        }

        [Test]
        public void TestShutdownClaimsLiveSkinnableOwnerWithoutCallbackScheduler()
        {
            SkinCurrentRevision revision = null!;
            LocalSkinDependencyHost ownerHost = null!;
            SkinnableContainer host = null!;
            ShutdownTrackingContainer provisional = null!;
            Task shutdown = null!;
            int participantBaseline = 0;
            var heldCallbackScheduler = new Scheduler();

            AddStep("mount isolated real skinnable owner", () =>
            {
                revision = manager.CurrentRevision;
                Add(ownerHost = new LocalSkinDependencyHost(
                    manager,
                    host = new SkinnableContainer(
                        new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.Results))));
            });
            AddUntilStep("wait for initial skinnable generation", () =>
                host.IsLoaded
                && host.ComponentsLoaded
                && revision.WorkDetached.IsCompleted);
            AddStep("hold completed skinnable replacement callback", () =>
            {
                participantBaseline = revision.ParticipantLeaseCount;
                host.ContentLoadCallbackScheduler = heldCallbackScheduler;
                host.Reload(provisional = new ShutdownTrackingContainer());

                Assert.That(
                    SpinWait.SpinUntil(
                        () => host.PendingContentLoadTask?.IsCompleted == true
                              && heldCallbackScheduler.HasPendingTasks
                              && revision.WorkDetached.IsCompleted == false,
                        TimeSpan.FromSeconds(10)),
                    Is.True);
                Assert.That(revision.ParticipantLeaseCount, Is.EqualTo(participantBaseline + 1));
            });
            AddStep("shutdown isolated manager while skinnable host remains attached", () =>
                shutdown = Task.Run(manager.ShutdownManagedFolderMutations));
            AddUntilStep("wait for skinnable owner cancellation and work join", () =>
                shutdown.IsCompleted
                && revision.WorkDetached.IsCompleted
                && provisional.DisposeCount == 1);
            AddStep("assert skinnable visual detach was not impersonated", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(shutdown.IsCompletedSuccessfully, Is.True);
                    Assert.That(ownerHost.Parent, Is.Not.Null);
                    Assert.That(host.ComponentsLoaded, Is.False);
                    Assert.That(heldCallbackScheduler.HasPendingTasks, Is.True);
                    Assert.That(provisional.DisposeCount, Is.EqualTo(1));
                    Assert.That(revision.WorkDetached.IsCompletedSuccessfully, Is.True);
                    Assert.That(revision.ParticipantLeaseCount, Is.EqualTo(participantBaseline));
                    Assert.That(revision.ConsumersDetached.IsCompleted, Is.False);
                });

                heldCallbackScheduler.Update();
                heldCallbackScheduler.Update();
                Assert.That(heldCallbackScheduler.HasPendingTasks, Is.False);
                Assert.That(provisional.DisposeCount, Is.EqualTo(1));
                Assert.That(Remove(ownerHost, disposeImmediately: true), Is.True);
            });
        }

        private bool currentRevisionWorkAdmissionIsClosed()
        {
            try
            {
                manager.AcquireCurrentRevisionWorkLease().Dispose();
                return false;
            }
            catch (ObjectDisposedException)
            {
                return true;
            }
        }

        private sealed partial class LocalSkinDependencyHost : CompositeDrawable
        {
            [Cached]
            private readonly SkinManager skinManager;

            [Cached(typeof(ISkinSource))]
            private readonly ISkinSource skinSource;

            internal LocalSkinDependencyHost(SkinManager skinManager, Drawable child)
            {
                this.skinManager = skinManager;
                skinSource = skinManager;
                InternalChild = child;
            }
        }

        private sealed partial class ShutdownTrackingDrawable : Drawable
        {
            private int disposeCount;

            internal int DisposeCount => Volatile.Read(ref disposeCount);

            [BackgroundDependencyLoader]
            private void load()
            {
            }

            protected override void Dispose(bool isDisposing)
            {
                Interlocked.Increment(ref disposeCount);
                base.Dispose(isDisposing);
            }
        }

        private sealed partial class ShutdownTrackingContainer : Container
        {
            private int disposeCount;

            internal int DisposeCount => Volatile.Read(ref disposeCount);

            [BackgroundDependencyLoader]
            private void load()
            {
            }

            protected override void Dispose(bool isDisposing)
            {
                Interlocked.Increment(ref disposeCount);
                base.Dispose(isDisposing);
            }
        }
    }
}
