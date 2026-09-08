// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Game.Skinning.Gameplay;
using osu.Game.Skinning.Gameplay.Scripting;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinScriptAuthorizationTest
    {
        private string root = null!;
        private NativeStorage storage = null!;
        private GameplaySkinScriptAuthorizationStore authorizations = null!;
        private static readonly Guid record = Guid.Parse("a1050200-fae6-4ea7-a63b-73290b312312");
        private static readonly string revision = new string('a', 64);
        private static readonly ScriptCapabilityRequest[] requests =
        {
            new ScriptCapabilityRequest("gameplay.snapshot.read", ScriptCapabilityMode.Required),
            new ScriptCapabilityRequest("scene.numeric.write", ScriptCapabilityMode.Required),
            new ScriptCapabilityRequest("math.random.read", ScriptCapabilityMode.Optional),
        };

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "oms-c6-auth-" + Guid.NewGuid().ToString("N"));
            storage = new NativeStorage(root);
            authorizations = new GameplaySkinScriptAuthorizationStore(storage);
        }

        [TearDown]
        public void TearDown()
        {
            authorizations.Shutdown();
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        [Test]
        public async Task TestExplicitConsentPersistsButNewPackageOrRecordDoesNotInheritIt()
        {
            GameplaySkinScriptAuthorization token = authorizations.Bind(record, revision, requests);
            Assert.That(token.RequiredSatisfied, Is.False);
            Assert.That(token.IsGranted("math.random.read"), Is.False);
            Assert.That(await token.SetAsync("gameplay.snapshot.read", GameplaySkinScriptAuthorizationChoice.Granted), Is.True);
            Assert.That(token.RequiredSatisfied, Is.False);
            Assert.That(await token.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Granted), Is.True);
            Assert.That(token.RequiredSatisfied, Is.True);
            Assert.That(token.IsGranted("math.random.read"), Is.False, "Optional capability is not baseline consent.");

            authorizations.Shutdown();
            authorizations = new GameplaySkinScriptAuthorizationStore(storage);
            Assert.That(authorizations.Bind(record, revision, requests).RequiredSatisfied, Is.True);
            Assert.That(authorizations.Bind(record, new string('b', 64), requests).RequiredSatisfied, Is.False);
            Assert.That(authorizations.Bind(Guid.NewGuid(), revision, requests).RequiredSatisfied, Is.False);
        }

        [Test]
        public async Task TestRevokeImmediatelyInvalidatesEveryExistingTokenAndRestart()
        {
            GameplaySkinScriptAuthorization token = authorizations.Bind(record, revision, requests);
            GameplaySkinScriptAuthorization alreadyAttached = authorizations.Bind(record, revision, requests);
            await token.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Granted);
            Assert.That(alreadyAttached.IsGranted("scene.numeric.write"), Is.True);
            long oldVersion = alreadyAttached.Version;
            Task<bool> write = token.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.NotDecided);
            Assert.That(alreadyAttached.IsGranted("scene.numeric.write"), Is.False, "Revocation cannot wait for disk or reload.");
            Assert.That(alreadyAttached.Version, Is.GreaterThan(oldVersion));
            Assert.That(await write, Is.True);
            authorizations.Shutdown();
            authorizations = new GameplaySkinScriptAuthorizationStore(storage);
            Assert.That(authorizations.Bind(record, revision, requests).IsGranted("scene.numeric.write"), Is.False);
        }

        [Test]
        public async Task TestHardDenyExplicitDenyUnknownAndMissingHostCannotGrant()
        {
            ScriptCapabilityRequest[] unsafeRequests =
            {
                new ScriptCapabilityRequest("network.read", ScriptCapabilityMode.Required),
                new ScriptCapabilityRequest("unknown.read", ScriptCapabilityMode.Optional),
                new ScriptCapabilityRequest("scene.numeric.write", ScriptCapabilityMode.Deny),
                new ScriptCapabilityRequest("gameplay.snapshot.read", ScriptCapabilityMode.Required),
            };
            GameplaySkinScriptAuthorization token = authorizations.Bind(record, revision, unsafeRequests, Array.Empty<string>());
            Assert.That(await token.SetAsync("network.read", GameplaySkinScriptAuthorizationChoice.Granted), Is.False);
            Assert.That(await token.SetAsync("unknown.read", GameplaySkinScriptAuthorizationChoice.Granted), Is.False);
            Assert.That(await token.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Granted), Is.False);
            Assert.That(await token.SetAsync("gameplay.snapshot.read", GameplaySkinScriptAuthorizationChoice.Granted), Is.True);
            Assert.That(token.GetChoice("gameplay.snapshot.read"), Is.EqualTo(GameplaySkinScriptAuthorizationChoice.Granted));
            Assert.That(token.IsGranted("gameplay.snapshot.read"), Is.False, "Consent cannot manufacture host support.");
            Assert.That(token.RequiredSatisfied, Is.False);
        }

        [Test]
        public async Task TestExplicitUserDenialIsQueryableAndDurable()
        {
            GameplaySkinScriptAuthorization token = authorizations.Bind(record, revision, requests);
            Assert.That(await token.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Denied), Is.True);
            authorizations.Shutdown();
            authorizations = new GameplaySkinScriptAuthorizationStore(storage);
            GameplaySkinScriptAuthorization restored = authorizations.Bind(record, revision, requests);
            Assert.That(restored.GetChoice("scene.numeric.write"), Is.EqualTo(GameplaySkinScriptAuthorizationChoice.Denied));
            Assert.That(restored.IsGranted("scene.numeric.write"), Is.False);
        }

        [TestCase("{\"contract\":\"future\",\"entries\":{}}")]
        [TestCase("{\"contract\":\"oms-skin-script-authorizations.v1\",\"entries\":{},\"entries\":{}}")]
        [TestCase("invalid")]
        [TestCase("{\"contract\":{},\"entries\":{}}")]
        [TestCase("{\"contract\":[],\"entries\":{}}")]
        [TestCase("{\"contract\":1,\"entries\":{}}")]
        public void TestMalformedOrFuturePersistenceFailsClosed(string content)
        {
            authorizations.Shutdown();
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, GameplaySkinScriptAuthorizationStore.FILE_NAME), content);
            authorizations = new GameplaySkinScriptAuthorizationStore(storage);
            GameplaySkinScriptAuthorization token = authorizations.Bind(record, revision, requests);
            Assert.That(token.RequiredSatisfied, Is.False);
            Assert.That(token.PersistenceError, Is.EqualTo("OMS-SKIN-AUTH-INVALID-STORAGE"));
        }

        [Test]
        public async Task TestShutdownRevokesAndJoinsTheLastWrite()
        {
            GameplaySkinScriptAuthorization token = authorizations.Bind(record, revision, requests);
            Task<bool> pending = token.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Granted);
            authorizations.Shutdown();
            Assert.That(pending.IsCompleted, Is.True);
            Assert.That(token.IsGranted("scene.numeric.write"), Is.False);
            Assert.That(await token.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Granted), Is.False);
        }

        [Test]
        public async Task TestInterruptedReplacementCannotResurrectAnOldGrant()
        {
            GameplaySkinScriptAuthorization token = authorizations.Bind(record, revision, requests);
            await token.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Granted);
            authorizations.Shutdown();
            File.WriteAllText(Path.Combine(root, GameplaySkinScriptAuthorizationStore.FILE_NAME + ".pending"), "interrupted replacement");
            authorizations = new GameplaySkinScriptAuthorizationStore(storage);
            GameplaySkinScriptAuthorization restarted = authorizations.Bind(record, revision, requests);
            Assert.That(restarted.IsGranted("scene.numeric.write"), Is.False);
            Assert.That(restarted.PersistenceError, Is.EqualTo("OMS-SKIN-AUTH-INVALID-STORAGE"));
        }

        [Test]
        public async Task TestRealReplacementFailureRevokesExistingRuntimeAndFailsClosedOnRestart()
        {
            GameplaySkinScriptAuthorization token = authorizations.Bind(record, revision, requests);
            await token.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Granted);
            string path = Path.Combine(root, GameplaySkinScriptAuthorizationStore.FILE_NAME);
            using (var heldFile = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Task<bool> revoke = token.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.NotDecided);
                Assert.That(token.IsGranted("scene.numeric.write"), Is.False);
                Assert.That(await revoke, Is.False);
                Assert.That(token.PersistenceError, Is.EqualTo("OMS-SKIN-AUTH-WRITE-FAILED"));
            }

            Assert.That(File.Exists(path + ".pending"), Is.True);
            authorizations.Shutdown();
            authorizations = new GameplaySkinScriptAuthorizationStore(storage);
            GameplaySkinScriptAuthorization restarted = authorizations.Bind(record, revision, requests);
            Assert.That(restarted.IsGranted("scene.numeric.write"), Is.False);
            Assert.That(restarted.PersistenceError, Is.EqualTo("OMS-SKIN-AUTH-INVALID-STORAGE"));

            // The user's next explicit decision replaces the interrupted file; no unrelated old grants are restored.
            Assert.That(await restarted.SetAsync("gameplay.snapshot.read", GameplaySkinScriptAuthorizationChoice.Granted), Is.True);
            Assert.That(restarted.IsGranted("gameplay.snapshot.read"), Is.True);
            Assert.That(restarted.IsGranted("scene.numeric.write"), Is.False);
            Assert.That(File.Exists(path + ".pending"), Is.False);
        }

        [Test]
        public async Task TestFailedGrantNeverEnablesAlreadyAttachedToken()
        {
            GameplaySkinScriptAuthorization token = authorizations.Bind(record, revision, requests);
            await token.SetAsync("gameplay.snapshot.read", GameplaySkinScriptAuthorizationChoice.Granted);
            GameplaySkinScriptAuthorization attached = authorizations.Bind(record, revision, requests);
            string path = Path.Combine(root, GameplaySkinScriptAuthorizationStore.FILE_NAME);
            using (var heldFile = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.That(await token.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Granted), Is.False);
                Assert.That(attached.IsGranted("scene.numeric.write"), Is.False);
                Assert.That(attached.RequiredSatisfied, Is.False);
            }

            Assert.That(await token.SetAsync("math.random.read", GameplaySkinScriptAuthorizationChoice.Granted), Is.True);
            Assert.That(attached.IsGranted("math.random.read"), Is.True);
            Assert.That(attached.IsGranted("scene.numeric.write"), Is.False, "A later unrelated save must not activate a grant whose persistence failed.");
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task TestQueuedGrantResultsUseOnlySuccessfullyPersistedDecisions(bool firstWriteFails)
        {
            GameplaySkinScriptAuthorization token = authorizations.Bind(record, revision, requests);
            await token.SetAsync("gameplay.snapshot.read", GameplaySkinScriptAuthorizationChoice.Granted);
            using var firstEntered = new ManualResetEventSlim();
            using var firstRelease = new ManualResetEventSlim();
            using var secondEntered = new ManualResetEventSlim();
            using var secondRelease = new ManualResetEventSlim();
            int operation = 0;
            authorizations.BeforePersistence = () =>
            {
                int current = Interlocked.Increment(ref operation);
                if (current > 2)
                    return;
                (current == 1 ? firstEntered : secondEntered).Set();
                if (!(current == 1 ? firstRelease : secondRelease).Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Timed out observing the actual consent persistence boundary.");
            };

            string path = Path.Combine(root, GameplaySkinScriptAuthorizationStore.FILE_NAME);
            FileStream? heldFile = firstWriteFails ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None) : null;
            try
            {
                Task<bool> first = token.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Granted);
                Assert.That(firstEntered.Wait(TimeSpan.FromSeconds(10)), Is.True);
                Task<bool> second = token.SetAsync("math.random.read", GameplaySkinScriptAuthorizationChoice.Granted);
                firstRelease.Set();
                Assert.That(await first, Is.EqualTo(!firstWriteFails));
                Assert.That(secondEntered.Wait(TimeSpan.FromSeconds(10)), Is.True);
                if (firstWriteFails)
                {
                    heldFile!.Dispose();
                    heldFile = null;
                }
                else
                    heldFile = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
                secondRelease.Set();
                Assert.That(await second, Is.EqualTo(firstWriteFails));
                Assert.That(token.IsGranted("scene.numeric.write"), Is.EqualTo(!firstWriteFails),
                    "A queued unrelated save must neither resurrect a failed grant nor discard a successful one.");
                Assert.That(token.IsGranted("math.random.read"), Is.EqualTo(firstWriteFails));
            }
            finally
            {
                firstRelease.Set();
                secondRelease.Set();
                heldFile?.Dispose();
                authorizations.BeforePersistence = null;
            }

            // Recover the real failed atomic-write marker by explicitly saving the current decision again.
            Assert.That(await token.SetAsync("gameplay.snapshot.read", GameplaySkinScriptAuthorizationChoice.Granted), Is.True);
            authorizations.Shutdown();
            authorizations = new GameplaySkinScriptAuthorizationStore(storage);
            GameplaySkinScriptAuthorization restored = authorizations.Bind(record, revision, requests);
            Assert.That(restored.IsGranted("scene.numeric.write"), Is.EqualTo(!firstWriteFails));
            Assert.That(restored.IsGranted("math.random.read"), Is.EqualTo(firstWriteFails));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task TestQueuedRevokeCannotBeTemporarilyUndoneByAnEarlierSuccessfulGrant(bool regrantAfterRevoke)
        {
            GameplaySkinScriptAuthorization token = authorizations.Bind(record, revision, requests);
            using var firstEntered = new ManualResetEventSlim();
            using var firstRelease = new ManualResetEventSlim();
            using var secondEntered = new ManualResetEventSlim();
            using var secondRelease = new ManualResetEventSlim();
            int operation = 0;
            authorizations.BeforePersistence = () =>
            {
                int current = Interlocked.Increment(ref operation);
                if (current > 2)
                    return;
                (current == 1 ? firstEntered : secondEntered).Set();
                if (!(current == 1 ? firstRelease : secondRelease).Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Timed out observing queued grant and revoke.");
            };
            try
            {
                Task<bool> grant = token.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Granted);
                Assert.That(firstEntered.Wait(TimeSpan.FromSeconds(10)), Is.True);
                Task<bool> revoke = token.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.NotDecided);
                Task<bool>? regrant = regrantAfterRevoke
                    ? token.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Granted) : null;
                Assert.That(token.IsGranted("scene.numeric.write"), Is.False);
                firstRelease.Set();
                Assert.That(await grant, Is.True);
                Assert.That(secondEntered.Wait(TimeSpan.FromSeconds(10)), Is.True);
                Assert.That(token.IsGranted("scene.numeric.write"), Is.False,
                    "An earlier durable grant cannot override the user's already effective later revocation.");
                secondRelease.Set();
                Assert.That(await revoke, Is.True);
                if (regrant != null)
                    Assert.That(await regrant, Is.True);
                Assert.That(token.IsGranted("scene.numeric.write"), Is.EqualTo(regrantAfterRevoke));
            }
            finally
            {
                firstRelease.Set();
                secondRelease.Set();
                authorizations.BeforePersistence = null;
                authorizations.Shutdown();
            }
        }

        [Test]
        public async Task TestRuntimeRightsRecogniseRevokeRegrantButIgnoreUnrelatedOrRepeatedDecisions()
        {
            GameplaySkinScriptAuthorization token = authorizations.Bind(record, revision, requests);
            await token.SetAsync("gameplay.snapshot.read", GameplaySkinScriptAuthorizationChoice.Granted);
            await token.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Granted);
            var granted = token.RuntimeRights;
            Assert.That(granted.GrantMask, Is.EqualTo(5));
            Assert.That(granted.RequiredSatisfied, Is.True);

            GameplaySkinScriptAuthorization otherSkin = authorizations.Bind(Guid.NewGuid(), revision, requests);
            Assert.That(await otherSkin.SetAsync("math.random.read", GameplaySkinScriptAuthorizationChoice.Granted), Is.True);
            Assert.That(token.RuntimeRights, Is.EqualTo(granted), "An unrelated skin cannot invalidate a running script's state.");
            Assert.That(await token.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Granted), Is.True);
            Assert.That(token.RuntimeRights, Is.EqualTo(granted), "Saving unchanged consent is not a new runtime authority.");

            Task<bool> revoke = token.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.NotDecided);
            var revoked = token.RuntimeRights;
            Assert.That(revoked.Epoch, Is.GreaterThan(granted.Epoch));
            Assert.That(revoked.GrantMask, Is.EqualTo(1));
            Assert.That(revoked.RequiredSatisfied, Is.False);
            Assert.That(await revoke, Is.True);
            Assert.That(await token.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Granted), Is.True);
            var regranted = token.RuntimeRights;
            Assert.That(regranted.Epoch, Is.GreaterThan(revoked.Epoch));
            Assert.That(regranted.GrantMask, Is.EqualTo(granted.GrantMask));
            Assert.That(regranted.RequiredSatisfied, Is.True);
        }

        [Test]
        public async Task TestEveryQueuedSuccessfulRightsChangeHasItsOwnRuntimeIdentity()
        {
            GameplaySkinScriptAuthorization token = authorizations.Bind(record, revision, requests);
            using var firstEntered = new ManualResetEventSlim();
            using var firstRelease = new ManualResetEventSlim();
            using var secondEntered = new ManualResetEventSlim();
            using var secondRelease = new ManualResetEventSlim();
            int operation = 0;
            authorizations.BeforePersistence = () =>
            {
                int current = Interlocked.Increment(ref operation);
                (current == 1 ? firstEntered : secondEntered).Set();
                if (!(current == 1 ? firstRelease : secondRelease).Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Timed out observing successive runtime rights publications.");
            };
            try
            {
                Task<bool> first = token.SetAsync("gameplay.snapshot.read", GameplaySkinScriptAuthorizationChoice.Granted);
                Assert.That(firstEntered.Wait(TimeSpan.FromSeconds(10)), Is.True);
                Task<bool> second = token.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Granted);
                firstRelease.Set();
                Assert.That(await first, Is.True);
                Assert.That(secondEntered.Wait(TimeSpan.FromSeconds(10)), Is.True);
                var partiallyGranted = token.RuntimeRights;
                Assert.That(partiallyGranted.GrantMask, Is.EqualTo(1));
                Assert.That(partiallyGranted.RequiredSatisfied, Is.False);
                secondRelease.Set();
                Assert.That(await second, Is.True);
                var fullyGranted = token.RuntimeRights;
                Assert.That(fullyGranted.Epoch, Is.GreaterThan(partiallyGranted.Epoch));
                Assert.That(fullyGranted.GrantMask, Is.EqualTo(5));
                Assert.That(fullyGranted.RequiredSatisfied, Is.True);
            }
            finally
            {
                firstRelease.Set();
                secondRelease.Set();
                authorizations.BeforePersistence = null;
                authorizations.Shutdown();
            }
        }

        [Test]
        public async Task TestQueuedDecisionsPersistLatestRevocationAndPreserveAnotherSkin()
        {
            GameplaySkinScriptAuthorization token = authorizations.Bind(record, revision, requests);
            Guid secondRecord = Guid.NewGuid();
            GameplaySkinScriptAuthorization secondSkin = authorizations.Bind(secondRecord, revision, requests);
            Task<bool> anotherSkinWrite = secondSkin.SetAsync("math.random.read", GameplaySkinScriptAuthorizationChoice.Granted);
            Task<bool>[] writes = Enumerable.Range(0, 32).Select(i => token.SetAsync("scene.numeric.write",
                i % 2 == 0 ? GameplaySkinScriptAuthorizationChoice.Granted : GameplaySkinScriptAuthorizationChoice.NotDecided)).ToArray();
            Assert.That(token.IsGranted("scene.numeric.write"), Is.False);
            Assert.That(await anotherSkinWrite, Is.True);
            Assert.That(await Task.WhenAll(writes), Is.All.True);
            Assert.That(secondSkin.IsGranted("math.random.read"), Is.True);
            Assert.That(token.IsGranted("scene.numeric.write"), Is.False);
            authorizations.Shutdown();
            authorizations = new GameplaySkinScriptAuthorizationStore(storage);
            Assert.That(authorizations.Bind(secondRecord, revision, requests).IsGranted("math.random.read"), Is.True);
            Assert.That(authorizations.Bind(record, revision, requests).IsGranted("scene.numeric.write"), Is.False);
        }

        [Test]
        public async Task TestDirectoryBlockingPendingReplacementCannotRestorePriorGrant()
        {
            GameplaySkinScriptAuthorization token = authorizations.Bind(record, revision, requests);
            await token.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.Granted);
            Directory.CreateDirectory(Path.Combine(root, GameplaySkinScriptAuthorizationStore.FILE_NAME + ".pending"));
            Assert.That(await token.SetAsync("scene.numeric.write", GameplaySkinScriptAuthorizationChoice.NotDecided), Is.False);
            Assert.That(token.IsGranted("scene.numeric.write"), Is.False);
            authorizations.Shutdown();
            authorizations = new GameplaySkinScriptAuthorizationStore(storage);
            Assert.That(authorizations.Bind(record, revision, requests).IsGranted("scene.numeric.write"), Is.False);
            Assert.That(authorizations.PersistenceError, Is.EqualTo("OMS-SKIN-AUTH-INVALID-STORAGE"));
        }

        [TestCase("{\"contract\":\"oms-skin-script-authorizations.v1\",\"entries\":{\"broken\":{}}}")]
        [TestCase("{\"contract\":\"oms-skin-script-authorizations.v1\",\"entries\":[]}")]
        [TestCase("{\"contract\":\"oms-skin-script-authorizations.v1\",\"entries\":{}} {}")]
        public void TestUntrustedPersistenceKeysAndTrailingContentAreRejected(string content)
        {
            TestMalformedOrFuturePersistenceFailsClosed(content);
        }

        [Test]
        public void TestPersistenceSizeAndInvalidUtf8AreRejectedBeforeGrant()
        {
            authorizations.Shutdown();
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, GameplaySkinScriptAuthorizationStore.FILE_NAME);
            File.WriteAllBytes(path, new byte[1024 * 1024 + 1]);
            authorizations = new GameplaySkinScriptAuthorizationStore(storage);
            Assert.That(authorizations.Bind(record, revision, requests).RequiredSatisfied, Is.False);
            Assert.That(authorizations.PersistenceError, Is.EqualTo("OMS-SKIN-AUTH-INVALID-STORAGE"));
            authorizations.Shutdown();
            File.WriteAllBytes(path, new byte[] { 0xff });
            authorizations = new GameplaySkinScriptAuthorizationStore(storage);
            Assert.That(authorizations.Bind(record, revision, requests).RequiredSatisfied, Is.False);
            Assert.That(authorizations.PersistenceError, Is.EqualTo("OMS-SKIN-AUTH-INVALID-STORAGE"));
        }
    }
}
