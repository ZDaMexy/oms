// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Models;

namespace osu.Game.Tests.Database
{
    public class FileStoreTests : RealmTest
    {
        [Test]
        public void TestImportFile()
        {
            RunTestWithRealm((realmAccess, storage) =>
            {
                var realm = realmAccess.Realm;
                var files = new RealmFileStore(realmAccess, storage);

                var testData = new MemoryStream(new byte[] { 0, 1, 2, 3 });

                realm.Write(() => files.Add(testData, realm));

                Assert.True(files.Storage.Exists("0/05/054edec1d0211f624fed0cbca9d4f9400b0e491c43742af2c5b0abebf0c990d8"));
                Assert.True(files.Storage.Exists(realm.All<RealmFile>().First().GetStoragePath()));
            });
        }

        [Test]
        public void TestImportSameFileTwice()
        {
            RunTestWithRealm((realmAccess, storage) =>
            {
                var realm = realmAccess.Realm;
                var files = new RealmFileStore(realmAccess, storage);

                var testData = new MemoryStream(new byte[] { 0, 1, 2, 3 });

                realm.Write(() => files.Add(testData, realm));
                realm.Write(() => files.Add(testData, realm));

                Assert.AreEqual(1, realm.All<RealmFile>().Count());
            });
        }

        [Test]
        public void TestDontPurgeReferenced()
        {
            RunTestWithRealm((realmAccess, storage) =>
            {
                var realm = realmAccess.Realm;
                var files = new RealmFileStore(realmAccess, storage);

                var file = realm.Write(() => files.Add(new MemoryStream(new byte[] { 0, 1, 2, 3 }), realm));

                var timer = new Stopwatch();
                timer.Start();

                realm.Write(() =>
                {
                    // attach the file to an arbitrary beatmap
                    var beatmapSet = CreateBeatmapSet(CreateRuleset());

                    beatmapSet.Files.Add(new RealmNamedFileUsage(file, "arbitrary.resource"));

                    realm.Add(beatmapSet);
                });

                Logger.Log($"Import complete at {timer.ElapsedMilliseconds}");

                string path = file.GetStoragePath();

                Assert.True(realm.All<RealmFile>().Any());
                Assert.True(files.Storage.Exists(path));

                files.Cleanup();
                Logger.Log($"Cleanup complete at {timer.ElapsedMilliseconds}");

                Assert.True(realm.All<RealmFile>().Any());
                Assert.True(file.IsValid);
                Assert.True(files.Storage.Exists(path));
            });
        }

        [Test]
        public void TestPurgeUnreferenced()
        {
            RunTestWithRealm((realmAccess, storage) =>
            {
                var realm = realmAccess.Realm;
                var files = new RealmFileStore(realmAccess, storage);

                var file = realm.Write(() => files.Add(new MemoryStream(new byte[] { 0, 1, 2, 3 }), realm));

                string path = file.GetStoragePath();

                Assert.True(realm.All<RealmFile>().Any());
                Assert.True(files.Storage.Exists(path));

                files.Cleanup();

                Assert.False(realm.All<RealmFile>().Any());
                Assert.False(file.IsValid);
                Assert.False(files.Storage.Exists(path));
            });
        }

        [Test]
        public void TestOverlappingFailedImportScopesRemoveNewSharedHash()
        {
            RunTestWithRealm((realmAccess, storage) =>
            {
                byte[] content = Guid.NewGuid().ToByteArray();
                var firstStore = new RealmFileStore(realmAccess, storage);
                var secondStore = new RealmFileStore(realmAccess, storage);
                using var bothAdded = new CountdownEvent(2);
                using var release = new ManualResetEventSlim();

                Task first = runScopedAdd(firstStore, realmAccess, content, bothAdded, release, complete: false);
                Task second = runScopedAdd(secondStore, realmAccess, content, bothAdded, release, complete: false);

                Assert.That(bothAdded.Wait(TimeSpan.FromSeconds(10)), Is.True);
                release.Set();
                Task.WaitAll(first, second);

                string hash = new MemoryStream(content).ComputeSHA2Hash();
                string path = new RealmFile { Hash = hash }.GetStoragePath();
                realmAccess.Realm.Refresh();

                Assert.That(realmAccess.Realm.Find<RealmFile>(hash), Is.Null,
                    "The last failed participant must remove the zero-usage RealmFile committed by the overlapping imports.");
                Assert.That(firstStore.Storage.Exists(path), Is.False,
                    "The last failed participant must remove the blob jointly created by the overlapping imports.");
            });
        }

        [Test]
        public void TestSuccessfulOverlappingImportScopePreservesSharedHash()
        {
            RunTestWithRealm((realmAccess, storage) =>
            {
                byte[] content = Guid.NewGuid().ToByteArray();
                var firstStore = new RealmFileStore(realmAccess, storage);
                var secondStore = new RealmFileStore(realmAccess, storage);
                using var bothAdded = new CountdownEvent(2);
                using var release = new ManualResetEventSlim();

                Task successful = runScopedAdd(firstStore, realmAccess, content, bothAdded, release, complete: true);
                Task failed = runScopedAdd(secondStore, realmAccess, content, bothAdded, release, complete: false);

                Assert.That(bothAdded.Wait(TimeSpan.FromSeconds(10)), Is.True);
                release.Set();
                Task.WaitAll(successful, failed);

                string hash = new MemoryStream(content).ComputeSHA2Hash();
                string path = new RealmFile { Hash = hash }.GetStoragePath();
                realmAccess.Realm.Refresh();

                Assert.That(realmAccess.Realm.Find<RealmFile>(hash), Is.Not.Null);
                Assert.That(firstStore.Storage.Exists(path), Is.True);
            });
        }

        [Test]
        public void TestRealUsagePreservesHashWhenAllOverlappingScopesFail()
        {
            RunTestWithRealm((realmAccess, storage) =>
            {
                byte[] content = Guid.NewGuid().ToByteArray();
                var firstStore = new RealmFileStore(realmAccess, storage);
                var secondStore = new RealmFileStore(realmAccess, storage);
                using var bothAdded = new CountdownEvent(2);
                using var release = new ManualResetEventSlim();

                Task first = runScopedAdd(firstStore, realmAccess, content, bothAdded, release, complete: false);
                Task second = runScopedAdd(secondStore, realmAccess, content, bothAdded, release, complete: false);

                Assert.That(bothAdded.Wait(TimeSpan.FromSeconds(10)), Is.True);

                string hash = new MemoryStream(content).ComputeSHA2Hash();
                realmAccess.Realm.Refresh();
                RealmFile file = realmAccess.Realm.Find<RealmFile>(hash)!;

                realmAccess.Realm.Write(() =>
                {
                    var beatmapSet = CreateBeatmapSet(CreateRuleset());
                    beatmapSet.Files.Add(new RealmNamedFileUsage(file, "shared.resource"));
                    realmAccess.Realm.Add(beatmapSet);
                });

                release.Set();
                Task.WaitAll(first, second);
                realmAccess.Realm.Refresh();

                string path = new RealmFile { Hash = hash }.GetStoragePath();
                RealmFile? preserved = realmAccess.Realm.Find<RealmFile>(hash);
                Assert.That(preserved, Is.Not.Null);
                Assert.That(preserved!.Usages.Count(), Is.EqualTo(1));
                Assert.That(firstStore.Storage.Exists(path), Is.True);
            });
        }

        [Test]
        public void TestCleanupSkipsWhileImportScopeIsActive()
        {
            RunTestWithRealm((realmAccess, storage) =>
            {
                var files = new RealmFileStore(realmAccess, storage);
                byte[] content = Guid.NewGuid().ToByteArray();
                RealmFile file = realmAccess.Realm.Write(() => files.Add(new MemoryStream(content), realmAccess.Realm));

                using (files.BeginImportScope())
                    files.Cleanup();

                Assert.That(realmAccess.Realm.Find<RealmFile>(file.Hash), Is.Not.Null);
                Assert.That(files.Storage.Exists(file.GetStoragePath()), Is.True);
            });
        }

        [Test]
        public void TestFailedGroupFinalizerDoesNotBlockRemainingGroupsAndCanBeRetried()
        {
            RunTestWithRealm((realmAccess, storage) =>
            {
                byte[] firstContent = Guid.NewGuid().ToByteArray();
                byte[] secondContent = Guid.NewGuid().ToByteArray();
                string firstHash = new MemoryStream(firstContent).ComputeSHA2Hash();
                string secondHash = new MemoryStream(secondContent).ComputeSHA2Hash();
                var files = new RealmFileStore(realmAccess, storage);
                string? faultedHash = null;
                int finalizerCalls = 0;

                using (RealmFileStore.ImportScope scope = files.BeginImportScope())
                {
                    scope.FinaliseGroupTestHook = hash =>
                    {
                        finalizerCalls++;
                        if (faultedHash == null)
                        {
                            faultedHash = hash;
                            throw new IOException("Deterministic receipt rollback failure.");
                        }
                    };

                    realmAccess.Write(realm =>
                    {
                        files.Add(new MemoryStream(firstContent, writable: false), realm);
                        files.Add(new MemoryStream(secondContent, writable: false), realm);
                    });
                }

                Assert.That(finalizerCalls, Is.EqualTo(2), "A failed finalizer must not prevent later groups from rolling back.");
                Assert.That(faultedHash, Is.AnyOf(firstHash, secondHash));

                string completedHash = faultedHash == firstHash ? secondHash : firstHash;
                realmAccess.Realm.Refresh();
                Assert.That(realmAccess.Realm.Find<RealmFile>(completedHash), Is.Null);
                Assert.That(files.Storage.Exists(new RealmFile { Hash = completedHash }.GetStoragePath()), Is.False);

                byte[] retryContent = faultedHash == firstHash ? firstContent : secondContent;
                using (files.BeginImportScope())
                    realmAccess.Write(realm => files.Add(new MemoryStream(retryContent, writable: false), realm));

                realmAccess.Realm.Refresh();
                Assert.That(realmAccess.Realm.Find<RealmFile>(faultedHash!), Is.Null,
                    "Resetting Finalizing must allow the retained receipt to roll back on a later scoped add.");
                Assert.That(files.Storage.Exists(new RealmFile { Hash = faultedHash! }.GetStoragePath()), Is.False);
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestFailedImportScopeRestoresAsymmetricRealmAndBlobBaseline(bool blobExisted)
        {
            RunTestWithRealm((realmAccess, storage) =>
            {
                byte[] content = Guid.NewGuid().ToByteArray();
                string hash = new MemoryStream(content).ComputeSHA2Hash();
                var files = new RealmFileStore(realmAccess, storage);
                var file = new RealmFile { Hash = hash };
                string path = file.GetStoragePath();

                if (blobExisted)
                {
                    using Stream output = files.Storage.CreateFileSafely(path);
                    output.Write(content);
                }
                else
                {
                    realmAccess.Write(realm => realm.Add(file));
                }

                using (files.BeginImportScope())
                    realmAccess.Write(realm => files.Add(new MemoryStream(content, writable: false), realm));

                realmAccess.Realm.Refresh();
                Assert.Multiple(() =>
                {
                    Assert.That(realmAccess.Realm.Find<RealmFile>(hash) != null, Is.EqualTo(!blobExisted),
                        "The failed scope must restore the exact Realm-record side of the asymmetric baseline.");
                    Assert.That(files.Storage.Exists(path), Is.EqualTo(blobExisted),
                        "The failed scope must restore the exact blob side of the asymmetric baseline.");
                });
            });
        }

        private static Task runScopedAdd(RealmFileStore store, RealmAccess realmAccess, byte[] content,
                                         CountdownEvent bothAdded, ManualResetEventSlim release, bool complete)
            => Task.Run(() =>
            {
                using RealmFileStore.ImportScope scope = store.BeginImportScope();
                realmAccess.Write(realm => store.Add(new MemoryStream(content, writable: false), realm));
                bothAdded.Signal();

                if (!release.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Timed out waiting to finish the overlapping import receipt test.");

                if (complete)
                    scope.Complete();
            });
    }
}
