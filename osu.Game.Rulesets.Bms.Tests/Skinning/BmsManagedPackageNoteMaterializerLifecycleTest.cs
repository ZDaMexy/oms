// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Audio;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    [TestFixture]
    public class BmsManagedPackageNoteMaterializerLifecycleTest
    {
        [Test]
        public void TestExactRevisionRetiresOnlyAfterEveryBorrowReleases()
        {
            (BmsLegacySkin skin, GatedExactPackageStore store, SkinPackageRevisionCapsule capsule) = createOwner(
                requiredGates: 0,
                includeNoteDeclaration: false);
            BmsManagedPackageNoteRevisionBorrow? first = null;
            BmsManagedPackageNoteRevisionBorrow? second = null;

            try
            {
                BmsGameplayLayoutSnapshot layout = createCompatibilityLayout();
                first = skin.GetOrPrepareManagedPackageNotes(layout, CancellationToken.None);
                second = skin.GetOrPrepareManagedPackageNotes(layout, CancellationToken.None);
                BmsManagedPackageNoteRevision revision = first.Revision;

                Assert.Multiple(() =>
                {
                    Assert.That(second.Revision, Is.SameAs(revision));
                    Assert.That(skin.ActiveExactManagedPackageNotePreparationCount, Is.EqualTo(1));
                    Assert.That(skin.ActiveExactManagedPackageNoteBorrowCount, Is.EqualTo(2));
                    Assert.That(revision.IsDisposed, Is.False);
                });

                first.Dispose();
                first = null;

                Assert.Multiple(() =>
                {
                    Assert.That(skin.ActiveExactManagedPackageNotePreparationCount, Is.EqualTo(1));
                    Assert.That(skin.ActiveExactManagedPackageNoteBorrowCount, Is.EqualTo(1));
                    Assert.That(revision.IsDisposed, Is.False);
                });

                second.Dispose();
                second = null;

                Assert.Multiple(() =>
                {
                    Assert.That(skin.ActiveExactManagedPackageNotePreparationCount, Is.Zero);
                    Assert.That(skin.ActiveExactManagedPackageNoteBorrowCount, Is.Zero);
                    Assert.That(revision.IsDisposed, Is.True);
                });
            }
            finally
            {
                first?.Dispose();
                second?.Dispose();
                skin.Dispose();
                store.Dispose();
                store.DisposeEvents();
                capsule.Dispose();
            }
        }

        [Test]
        public void TestSkinDisposeDefersCompletedExactRevisionUntilLastBorrowReleases()
        {
            (BmsLegacySkin skin, GatedExactPackageStore store, SkinPackageRevisionCapsule capsule) = createOwner(
                requiredGates: 0,
                includeNoteDeclaration: false);
            BmsManagedPackageNoteRevisionBorrow? borrow = null;

            try
            {
                BmsGameplayLayoutSnapshot layout = createCompatibilityLayout();

                borrow = skin.GetOrPrepareManagedPackageNotes(layout, CancellationToken.None);
                BmsManagedPackageNoteRevision revision = borrow.Revision;

                Assert.Multiple(() =>
                {
                    Assert.That(skin.ActiveExactManagedPackageNotePreparationCount, Is.EqualTo(1));
                    Assert.That(skin.ActiveExactManagedPackageNoteBorrowCount, Is.EqualTo(1));
                    Assert.That(revision.IsDisposed, Is.False);
                });

                skin.Dispose();

                Assert.Multiple(() =>
                {
                    Assert.That(skin.ActiveExactManagedPackageNotePreparationCount, Is.Zero);
                    Assert.That(skin.ActiveExactManagedPackageNoteBorrowCount, Is.EqualTo(1));
                    Assert.That(revision.IsDisposed, Is.False,
                        "Skin retirement must not dispose a successful exact revision still owned by a publication borrow.");
                });

                borrow.Dispose();
                borrow = null;

                Assert.Multiple(() =>
                {
                    Assert.That(skin.ActiveExactManagedPackageNoteBorrowCount, Is.Zero);
                    Assert.That(revision.IsDisposed, Is.True);
                });
            }
            finally
            {
                borrow?.Dispose();
                skin.Dispose();
                store.Dispose();
                store.DisposeEvents();
                capsule.Dispose();
            }
        }

        [Test]
        public void TestProductionLeaseTransferKeepsRevisionAttachedUntilCancelledMaterializerActuallyStops()
        {
            (BmsLegacySkin skin, GatedExactPackageStore store, SkinPackageRevisionCapsule capsule) = createOwner(requiredGates: 1);
            using var requestCancellation = new CancellationTokenSource();
            Task request = null!;
            int retirementCount = 0;

            var revision = new SkinCurrentRevision(
                generation: 1,
                skin.SkinInfo.ID,
                store.ContentRevision,
                SkinCurrentRevisionSourceKind.ManagedFolder,
                skin,
                keepsReusableOwner: false,
                retired =>
                {
                    Interlocked.Increment(ref retirementCount);
                    retired.RetireOwner();
                });
            var transfer = new SkinCurrentRevisionLeaseTransfer(revision.AcquireWorkLease());

            try
            {
                store.Enabled = true;
                request = startMaterializerRequest(skin, requestCancellation.Token, transfer);
                Assert.That(store.FirstEntered.Wait(TimeSpan.FromSeconds(10)), Is.True,
                    "The production load-context transfer never reached exact package IO.");

                requestCancellation.Cancel();
                Assert.That(request.Wait(TimeSpan.FromSeconds(10)), Is.True,
                    "The cancelled drawable-side wait must be allowed to finish before uncooperative package IO.");

                // BmsLegacySkin claimed the transfer when it created the generation. Disposing the now-empty transfer
                // models BmsAsyncNoteDrawable's outer completion without releasing the generation's exact work lease.
                transfer.Dispose();
                revision.ReleaseManagerLease();

                Assert.Multiple(() =>
                {
                    Assert.That(revision.WorkDetached.IsCompleted, Is.False);
                    Assert.That(revision.ConsumersDetached.IsCompleted, Is.False);
                    Assert.That(revision.Detached.IsCompleted, Is.False);
                    Assert.That(revision.Retired.IsCompleted, Is.False);
                    Assert.That(retirementCount, Is.Zero);
                    Assert.That(store.DisposeCount, Is.Zero);
                    Assert.That(store.AccessAfterDisposeCount, Is.Zero);
                    Assert.DoesNotThrow(() => capsule.CreateResourceView().Dispose());
                });

                store.Release.Set();
                Assert.That(revision.WorkDetached.Wait(TimeSpan.FromSeconds(10)), Is.True);
                Assert.That(revision.ConsumersDetached.Wait(TimeSpan.FromSeconds(10)), Is.True);
                Assert.That(revision.Retired.Wait(TimeSpan.FromSeconds(10)), Is.True);

                revision.ReleaseManagerLease();
                transfer.Dispose();

                Assert.Multiple(() =>
                {
                    Assert.That(retirementCount, Is.EqualTo(1));
                    Assert.That(store.DisposeCount, Is.EqualTo(1));
                    Assert.That(store.AccessAfterDisposeCount, Is.Zero);
                    Assert.That(() => capsule.CreateResourceView(), Throws.TypeOf<ObjectDisposedException>());
                });
            }
            finally
            {
                store.Release.Set();
                requestCancellation.Cancel();
                request?.Wait(TimeSpan.FromSeconds(10));
                transfer.Dispose();
                revision.ReleaseManagerLease();
                skin.Dispose();
                store.Dispose();
                store.DisposeEvents();
            }
        }

        [Test]
        public void TestCancelledMaterializerIsJoinedByFinalRevisionDetachBeforeOwnerRetire()
        {
            (BmsLegacySkin skin, GatedExactPackageStore store, SkinPackageRevisionCapsule capsule) = createOwner(requiredGates: 1);
            using var requestCancellation = new CancellationTokenSource();
            SkinCurrentRevisionLease? finalHolder = null;
            Task? finalDetach = null;
            Task request = null!;
            int retirementCount = 0;

            try
            {
                store.Enabled = true;
                request = startMaterializerRequest(skin, requestCancellation.Token);
                Assert.That(store.FirstEntered.Wait(TimeSpan.FromSeconds(10)), Is.True, "The exact-package materializer never entered its gated read.");

                requestCancellation.Cancel();
                Assert.That(request.Wait(TimeSpan.FromSeconds(10)), Is.True, "The cancelled caller must detach without waiting for uncooperative package IO.");

                var revision = new SkinCurrentRevision(
                    generation: 1,
                    skin.SkinInfo.ID,
                    store.ContentRevision,
                    SkinCurrentRevisionSourceKind.ManagedFolder,
                    skin,
                    keepsReusableOwner: false,
                    retired =>
                    {
                        Interlocked.Increment(ref retirementCount);
                        retired.RetireOwner();
                    });

                finalHolder = revision.AcquireParticipantLease();
                revision.ReleaseManagerLease();

                finalDetach = Task.Run(finalHolder.Dispose);
                Assert.That(SpinWait.SpinUntil(() => Volatile.Read(ref retirementCount) == 1, TimeSpan.FromSeconds(10)), Is.True,
                    "Final detach never claimed retirement.");

                Assert.Multiple(() =>
                {
                    Assert.That(finalDetach.IsCompleted, Is.False, "Owner retirement must synchronously join the abandoned materializer.");
                    Assert.That(revision.Retired.IsCompleted, Is.False);
                    Assert.That(store.DisposeCount, Is.Zero, "The exact capsule owner must remain alive until the task stops touching it.");
                    Assert.That(store.AccessAfterDisposeCount, Is.Zero);
                    Assert.DoesNotThrow(() => capsule.CreateResourceView().Dispose());
                });

                store.Release.Set();
                Assert.That(finalDetach.Wait(TimeSpan.FromSeconds(10)), Is.True);
                Assert.That(revision.Retired.Wait(TimeSpan.FromSeconds(10)), Is.True);

                finalHolder.Dispose();
                revision.RetireOwner();

                Assert.Multiple(() =>
                {
                    Assert.That(retirementCount, Is.EqualTo(1));
                    Assert.That(store.DisposeCount, Is.EqualTo(1));
                    Assert.That(store.AccessAfterDisposeCount, Is.Zero);
                    Assert.That(() => capsule.CreateResourceView(), Throws.TypeOf<ObjectDisposedException>());
                });
            }
            finally
            {
                store.Release.Set();
                requestCancellation.Cancel();
                request?.Wait(TimeSpan.FromSeconds(10));
                finalDetach?.Wait(TimeSpan.FromSeconds(10));
                finalHolder?.Dispose();
                skin.Dispose();
                store.Dispose();
                store.DisposeEvents();
            }
        }

        [Test]
        public void TestPublicationShutdownClaimsAndJoinsEveryAbandonedMaterializerGeneration()
        {
            (BmsLegacySkin skin, GatedExactPackageStore store, SkinPackageRevisionCapsule capsule) = createOwner(requiredGates: 2);
            using var firstCancellation = new CancellationTokenSource();
            using var secondCancellation = new CancellationTokenSource();
            Task firstRequest = null!;
            Task secondRequest = null!;
            Task? shutdown = null;
            int retirementCount = 0;

            try
            {
                var publication = new SkinCurrentRevisionPublication(
                    skin,
                    store.ContentRevision,
                    SkinCurrentRevisionSourceKind.ManagedFolder,
                    keepsReusableOwner: false,
                    retired =>
                    {
                        Interlocked.Increment(ref retirementCount);
                        retired.RetireOwner();
                    });
                SkinRevisionParticipantRegistration holder = publication.RegisterExactOwner(
                    skin,
                    SkinRevisionParticipantKind.LifecycleHolder,
                    "bms.materializer.shutdown-holder")!;

                store.Enabled = true;
                firstRequest = startMaterializerRequest(skin, firstCancellation.Token);
                Assert.That(store.FirstEntered.Wait(TimeSpan.FromSeconds(10)), Is.True);
                firstCancellation.Cancel();
                Assert.That(firstRequest.Wait(TimeSpan.FromSeconds(10)), Is.True);

                secondRequest = startMaterializerRequest(skin, secondCancellation.Token);
                Assert.That(store.SecondEntered.Wait(TimeSpan.FromSeconds(10)), Is.True,
                    "A new request must not inherit the cancelled generation.");
                secondCancellation.Cancel();
                Assert.That(secondRequest.Wait(TimeSpan.FromSeconds(10)), Is.True);

                IReadOnlyList<SkinRevisionParticipantRegistration> claimed = publication.ShutdownAndClaimParticipants();
                Assert.That(claimed, Has.Count.EqualTo(1));

                shutdown = Task.Run(() =>
                {
                    foreach (SkinRevisionParticipantRegistration participant in claimed)
                        publication.UnregisterAndDetach(participant)?.Dispose();

                    publication.Current.ReleaseManagerLease();
                });

                Assert.That(SpinWait.SpinUntil(() => Volatile.Read(ref retirementCount) == 1, TimeSpan.FromSeconds(10)), Is.True,
                    "Shutdown never reached final revision retirement.");
                Assert.Multiple(() =>
                {
                    Assert.That(shutdown.IsCompleted, Is.False, "Shutdown must join both abandoned owner-internal tasks.");
                    Assert.That(publication.Current.Retired.IsCompleted, Is.False);
                    Assert.That(store.DisposeCount, Is.Zero);
                    Assert.That(store.AccessAfterDisposeCount, Is.Zero);
                });

                store.Release.Set();
                Assert.That(shutdown.Wait(TimeSpan.FromSeconds(10)), Is.True);
                Assert.That(publication.Current.Retired.Wait(TimeSpan.FromSeconds(10)), Is.True);

                holder.Dispose();

                Assert.Multiple(() =>
                {
                    Assert.That(retirementCount, Is.EqualTo(1));
                    Assert.That(store.DisposeCount, Is.EqualTo(1));
                    Assert.That(store.AccessAfterDisposeCount, Is.Zero);
                    Assert.That(() => capsule.CreateResourceView(), Throws.TypeOf<ObjectDisposedException>());
                });
            }
            finally
            {
                store.Release.Set();
                firstCancellation.Cancel();
                secondCancellation.Cancel();
                firstRequest?.Wait(TimeSpan.FromSeconds(10));
                secondRequest?.Wait(TimeSpan.FromSeconds(10));
                shutdown?.Wait(TimeSpan.FromSeconds(10));
                skin.Dispose();
                store.Dispose();
                store.DisposeEvents();
            }
        }

        private static Task startMaterializerRequest(BmsLegacySkin skin, CancellationToken cancellationToken)
            => Task.Run(() =>
            {
                try
                {
                    skin.GetOrPrepareManagedPackageNotes(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // The public caller is allowed to detach as soon as its own request is cancelled. The owner remains
                    // responsible for the still-running, package-touching generation until true task completion.
                }
            });

        private static Task startMaterializerRequest(
            BmsLegacySkin skin,
            CancellationToken cancellationToken,
            SkinCurrentRevisionLeaseTransfer revisionLeaseTransfer)
            => Task.Run(() =>
            {
                using (BmsManagedPackageNoteLoadContext.Enter(cancellationToken, revisionLeaseTransfer))
                {
                    try
                    {
                        skin.GetOrPrepareManagedPackageNotes(cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        // The owner-internal generation retains the transferred work lease until its true Task ends.
                    }
                }
            });

        private static (BmsLegacySkin Skin, GatedExactPackageStore Store, SkinPackageRevisionCapsule Capsule) createOwner(
            int requiredGates,
            bool includeNoteDeclaration = true)
        {
            byte[] configuration = Encoding.UTF8.GetBytes(
                "[General]\n" +
                "Name: lifecycle\n" +
                "Version: 2.7\n" +
                "[Bms]\n" +
                "Keymode: 7K\n" +
                (includeNoteDeclaration ? "NoteImage1: notes/note\n" : string.Empty));
            SkinPackageRevisionCapsuleCreationResult creation = SkinPackageRevisionCapsuleFactory.Create(new[]
            {
                SkinPackageCapturedEntry.CreateFile("skin.ini", configuration),
                SkinPackageCapturedEntry.CreateFile("notes/note.png", new byte[] { 0x4f, 0x4d, 0x53, 0x00 }),
            });
            Assert.That(creation.Capsule, Is.Not.Null);

            SkinPackageRevisionCapsule capsule = creation.Capsule!;
            var store = new GatedExactPackageStore(new SkinPackageRevisionResourceStore(capsule), requiredGates);

            try
            {
                var skin = new BmsLegacySkin(
                    new SkinInfo
                    {
                        ID = Guid.NewGuid(),
                        Name = "materializer lifecycle",
                        FilesystemStoragePath = "chartskin/materializer-lifecycle",
                    },
                    new TestResourceProvider(),
                    store,
                    useExactPackageStore: true);

                return (skin, store, capsule);
            }
            catch
            {
                store.Dispose();
                store.DisposeEvents();
                throw;
            }
        }

        private static BmsGameplayLayoutSnapshot createCompatibilityLayout()
        {
            var beatmap = new BmsBeatmap
            {
                BmsInfo = new BmsBeatmapInfo { Keymode = BmsKeymode.Key7K },
            };

            return new BmsGameplayLayoutProvider(beatmap).PublishForTesting(
                BmsPlayfieldStyle.Center,
                new BmsGameplayLayoutConfiguration());
        }

        private sealed class TestResourceProvider : IStorageResourceProvider
        {
            public IRenderer Renderer { get; } = new DummyRenderer();
            public AudioManager? AudioManager => null;
            public IResourceStore<byte[]> Files => throw new AssertionException("An exact package materializer must not access Realm files.");
            public IResourceStore<byte[]> Resources { get; } = new ResourceStore<byte[]>();
            public RealmAccess RealmAccess => null!;
            public IResourceStore<TextureUpload>? CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore) => null;
        }

        private sealed class GatedExactPackageStore : ISkinPackageRevisionResourceStore
        {
            private readonly ISkinPackageRevisionResourceStore inner;
            private readonly int requiredGates;
            private int gatedReads;
            private int disposed;
            private int accessAfterDisposeCount;

            public readonly ManualResetEventSlim FirstEntered = new ManualResetEventSlim();
            public readonly ManualResetEventSlim SecondEntered = new ManualResetEventSlim();
            public readonly ManualResetEventSlim Release = new ManualResetEventSlim();

            public bool Enabled { get; set; }
            public int DisposeCount { get; private set; }
            public int AccessAfterDisposeCount => Volatile.Read(ref accessAfterDisposeCount);
            public string ContentRevision => inner.ContentRevision;
            public IReadOnlyList<SkinPackageFileRevision> Files => inner.Files;

            public GatedExactPackageStore(ISkinPackageRevisionResourceStore inner, int requiredGates)
            {
                this.inner = inner;
                this.requiredGates = requiredGates;
            }

            public byte[] Get(string name)
            {
                noteAccess();
                return inner.Get(name);
            }

            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default)
            {
                noteAccess();
                return inner.GetAsync(name, cancellationToken);
            }

            public Stream? GetStream(string name)
            {
                if (Enabled)
                {
                    int index = Interlocked.Increment(ref gatedReads);

                    if (index <= requiredGates)
                    {
                        (index == 1 ? FirstEntered : SecondEntered).Set();

                        if (!Release.Wait(TimeSpan.FromSeconds(30)))
                            throw new TimeoutException("The test did not release the exact-package materializer gate.");
                    }
                }

                noteAccess();
                return inner.GetStream(name);
            }

            public IEnumerable<string> GetAvailableResources()
            {
                noteAccess();
                return inner.GetAvailableResources();
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) != 0)
                    return;

                DisposeCount++;
                inner.Dispose();
            }

            public void DisposeEvents()
            {
                FirstEntered.Dispose();
                SecondEntered.Dispose();
                Release.Dispose();
            }

            private void noteAccess()
            {
                if (Volatile.Read(ref disposed) != 0)
                    Interlocked.Increment(ref accessAfterDisposeCount);
            }
        }
    }
}
