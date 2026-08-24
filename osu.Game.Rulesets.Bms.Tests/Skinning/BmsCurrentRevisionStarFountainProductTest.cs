// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Audio;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Screens.Menu;
using osu.Game.Skinning;
using osu.Game.Storyboards;
using osu.Game.Storyboards.Drawables;
using osuTK;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [Test]
        public void TestRealStarFountainPublishesPreparedTextureOnlyAtReloadBarrier()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            FullSkinSettingsCallerHost caller = null!;
            CurrentRevisionStarFountainHost fountainHost = null!;
            CurrentRevisionHolderHost holderHost = null!;
            SkinCurrentRevision revisionA = null!;
            Skin ownerA = null!;
            Live<SkinInfo> selectionA = null!;
            Texture textureA = null!;
            int retiredA = 0;
            var beforeCommit = new ManualResetEventSlim();
            var allowCommit = new ManualResetEventSlim();

            AddStep("create and select StarFountain revision A", () =>
            {
                (packageRoot, candidate) = createCandidate(
                    root => writeStarFountainRevisionPackage(root, "A", new Rgba32(240, 40, 80, 255)),
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for exact StarFountain A pair", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && manager.CurrentSkin.Value is BmsLegacySkin
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount real reload caller, StarFountain and old owner holder", () =>
            {
                revisionA = manager.CurrentRevision;
                ownerA = manager.CurrentSkin.Value;
                selectionA = manager.CurrentSkinInfo.Value;
                manager.CurrentRevisionRetired += revision =>
                {
                    if (ReferenceEquals(revision, revisionA))
                        retiredA++;
                };

                Add(fountainHost = new CurrentRevisionStarFountainHost(manager));
                Add(holderHost = new CurrentRevisionHolderHost(manager, ownerA));
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for production StarFountain and reload caller", () =>
                fountainHost.Fountain.IsLoaded
                && fountainHost.Texture != null
                && holderHost.Holder.IsLoaded
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("capture exact A texture and pause after all participants prepare", () =>
            {
                textureA = fountainHost.Texture!;
                Assert.Multiple(() =>
                {
                    Assert.That(textureA.Width, Is.EqualTo(3));
                    Assert.That(textureA.Height, Is.EqualTo(5));
                    Assert.That(textureA, Is.SameAs(ownerA.GetTexture("Menu/fountain-star")));
                });

                manager.CurrentRevisionBeforeCommitSchedule = () =>
                {
                    beforeCommit.Set();

                    if (!allowCommit.Wait(TimeSpan.FromSeconds(30)))
                        throw new TimeoutException("Timed out waiting to release the StarFountain publication barrier.");
                };
            });
            AddStep("write B and invoke the real SkinSection reload button", () =>
            {
                writeStarFountainRevisionPackage(packageRoot, "B", new Rgba32(20, 210, 120, 255));
                caller.ReloadCurrentButton.TriggerClick();
                Assert.That(caller.ReloadCurrentButton.Enabled.Value, Is.False);
            });
            AddUntilStep("wait for prepared B before commit", () => beforeCommit.IsSet);
            AddStep("assert prepared B is invisible before the update-thread barrier", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(fountainHost.Texture, Is.SameAs(textureA));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(retiredA, Is.Zero);
                });

                allowCommit.Set();
            });
            AddUntilStep("wait for coherent B pair and StarFountain texture", () =>
                !ReferenceEquals(manager.CurrentRevision, revisionA)
                && !ReferenceEquals(manager.CurrentSkin.Value, ownerA)
                && !ReferenceEquals(fountainHost.Texture, textureA)
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("assert StarFountain committed B while A waits for the last holder", () =>
            {
                SkinCurrentRevision revisionB = manager.CurrentRevision;

                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(revisionB.RecordId, Is.EqualTo(revisionA.RecordId));
                    Assert.That(revisionB.ContentRevision, Is.Not.EqualTo(revisionA.ContentRevision));
                    Assert.That(revisionB.Owner, Is.SameAs(manager.CurrentSkin.Value));
                    Assert.That(fountainHost.Texture, Is.Not.SameAs(textureA));
                    Assert.That(fountainHost.Texture, Is.SameAs(revisionB.Owner.GetTexture("Menu/fountain-star")));
                    Assert.That(fountainHost.Texture!.Width, Is.EqualTo(3));
                    Assert.That(fountainHost.Texture.Height, Is.EqualTo(5));
                    Assert.That(revisionA.ConsumersDetached.IsCompleted, Is.False);
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(retiredA, Is.Zero);
                });

                // Match BackgroundScreenDefault.displayNext(): the old SkinBackground remains a real drawable and
                // exact revision holder for the full cross-revision fade, then detaches on expiry.
                holderHost.Holder.FadeOut(800, Easing.OutQuint);
                holderHost.Holder.Expire();
            });
            AddStep("assert A remains leased during the background fade", () =>
            {
                Assert.That(holderHost.Holder.Parent, Is.Not.Null);
                Assert.That(revisionA.Retired.IsCompleted, Is.False);
                Assert.That(retiredA, Is.Zero);
            });
            AddUntilStep("wait for background fade final detach and exactly-once retirement", () =>
                holderHost.Holder.Parent == null
                && revisionA.Retired.IsCompleted
                && retiredA == 1);
            AddStep("assert no duplicate A retirement", () =>
            {
                manager.CurrentRevisionBeforeCommitSchedule = () => { };
                Assert.That(retiredA, Is.EqualTo(1));
                beforeCommit.Dispose();
                allowCommit.Dispose();
            });
        }

        [Test]
        public void TestRealStarFountainPreparedReloadAbortKeepsExactATextureAndOwner()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            FullSkinSettingsCallerHost caller = null!;
            CurrentRevisionStarFountainHost fountainHost = null!;
            SkinRevisionParticipantRegistration rejectingParticipant = null!;
            SkinCurrentRevision revisionA = null!;
            Skin ownerA = null!;
            Live<SkinInfo> selectionA = null!;
            Texture textureA = null!;
            int prepareCount = 0;
            int retiredA = 0;

            AddStep("create and select abort-test StarFountain A", () =>
            {
                (packageRoot, candidate) = createCandidate(
                    root => writeStarFountainRevisionPackage(root, "A", new Rgba32(240, 40, 80, 255)),
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for abort-test A pair", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && manager.CurrentSkin.Value is BmsLegacySkin);
            AddStep("mount StarFountain before a rejecting staged participant", () =>
            {
                revisionA = manager.CurrentRevision;
                ownerA = manager.CurrentSkin.Value;
                selectionA = manager.CurrentSkinInfo.Value;
                manager.CurrentRevisionPrepareStarted = () => prepareCount++;
                manager.CurrentRevisionRetired += revision =>
                {
                    if (ReferenceEquals(revision, revisionA))
                        retiredA++;
                };

                Add(fountainHost = new CurrentRevisionStarFountainHost(manager));
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for abort-test StarFountain", () =>
                fountainHost.Fountain.IsLoaded
                && fountainHost.Texture != null
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("register rejection after StarFountain and capture A", () =>
            {
                textureA = fountainHost.Texture!;
                rejectingParticipant = manager.RegisterRevisionParticipant(
                    SkinRevisionParticipantKind.CoherentVisualConsumer,
                    "C2 StarFountain abort sentinel",
                    prepareCommit: (_, _) => Task.FromResult<SkinRevisionParticipantCommit>(null!));
            });
            AddStep("write B and invoke real reload into participant abort", () =>
            {
                writeStarFountainRevisionPackage(packageRoot, "B", new Rgba32(20, 210, 120, 255));
                caller.ReloadCurrentButton.TriggerClick();
                Assert.That(caller.ReloadCurrentButton.Enabled.Value, Is.False);
            });
            AddUntilStep("wait for participant rejection feedback boundary", () =>
                prepareCount == 1 && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("assert exact A pair and StarFountain texture survived abort", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(manager.CurrentRevision.Owner, Is.SameAs(ownerA));
                    Assert.That(fountainHost.Texture, Is.SameAs(textureA));
                    Assert.That(fountainHost.Texture, Is.SameAs(ownerA.GetTexture("Menu/fountain-star")));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(retiredA, Is.Zero);
                });

                rejectingParticipant.Dispose();
            });
        }

        [Test]
        public void TestRealStarFountainAttachAfterPrepareForcesFreshBarrierIncludingAttachedConsumer()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            FullSkinSettingsCallerHost caller = null!;
            CurrentRevisionStarFountainHost existingHost = null!;
            CurrentRevisionStarFountainHost attachedHost = null!;
            SkinCurrentRevision revisionA = null!;
            Skin ownerA = null!;
            Live<SkinInfo> selectionA = null!;
            Texture existingTextureA = null!;
            Texture attachedTextureA = null!;
            int prepareAttempts = 0;
            int beforeCommitCalls = 0;
            int retiredA = 0;
            var firstBarrierPrepared = new ManualResetEventSlim();
            var releaseFirstBarrier = new ManualResetEventSlim();

            AddStep("create and select dynamic-attach revision A", () =>
            {
                (packageRoot, candidate) = createCandidate(
                    root => writeStarFountainRevisionPackage(root, "A", new Rgba32(225, 55, 90, 255)),
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for dynamic-attach exact A pair", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount existing real StarFountain and reload caller", () =>
            {
                revisionA = manager.CurrentRevision;
                ownerA = manager.CurrentSkin.Value;
                selectionA = manager.CurrentSkinInfo.Value;
                manager.CurrentRevisionRetired += revision =>
                {
                    if (ReferenceEquals(revision, revisionA))
                        retiredA++;
                };

                Add(existingHost = new CurrentRevisionStarFountainHost(manager));
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for existing dynamic-attach consumer", () =>
                existingHost.Fountain.IsLoaded
                && existingHost.Texture != null
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("pause first commit schedule after every current participant prepared", () =>
            {
                existingTextureA = existingHost.Texture!;
                manager.CurrentRevisionPrepareStarted = () => Interlocked.Increment(ref prepareAttempts);
                manager.CurrentRevisionBeforeCommitSchedule = () =>
                {
                    int invocation = Interlocked.Increment(ref beforeCommitCalls);

                    if (invocation != 1)
                        return;

                    firstBarrierPrepared.Set();

                    if (!releaseFirstBarrier.Wait(TimeSpan.FromSeconds(30)))
                        throw new TimeoutException("Timed out waiting to attach the real StarFountain participant.");
                };
            });
            AddStep("write B and invoke real reload", () =>
            {
                writeStarFountainRevisionPackage(packageRoot, "B", new Rgba32(25, 205, 125, 255));
                caller.ReloadCurrentButton.TriggerClick();
            });
            AddUntilStep("wait until first real barrier is fully prepared", () => firstBarrierPrepared.IsSet);
            AddStep("attach a real StarFountain while the prepared barrier is held", () =>
                Add(attachedHost = new CurrentRevisionStarFountainHost(manager)));
            AddUntilStep("wait for attached consumer to register against exact A", () =>
                attachedHost.Fountain.IsLoaded
                && attachedHost.Texture != null
                && ReferenceEquals(manager.CurrentRevision, revisionA)
                && ReferenceEquals(manager.CurrentSkin.Value, ownerA));
            AddStep("capture attached A texture and release stale barrier", () =>
            {
                attachedTextureA = attachedHost.Texture!;
                Assert.Multiple(() =>
                {
                    Assert.That(existingHost.Texture, Is.SameAs(existingTextureA));
                    Assert.That(attachedTextureA, Is.SameAs(ownerA.GetTexture("Menu/fountain-star")));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                });

                releaseFirstBarrier.Set();
            });
            AddUntilStep("wait for fresh barrier to publish B to both real consumers", () =>
                Volatile.Read(ref prepareAttempts) >= 2
                && Volatile.Read(ref beforeCommitCalls) >= 2
                && !ReferenceEquals(manager.CurrentRevision, revisionA)
                && !ReferenceEquals(existingHost.Texture, existingTextureA)
                && !ReferenceEquals(attachedHost.Texture, attachedTextureA)
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("assert attached consumer was included coherently in retried barrier", () =>
            {
                SkinCurrentRevision revisionB = manager.CurrentRevision;
                Texture expectedB = revisionB.Owner.GetTexture("Menu/fountain-star")!;

                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(revisionB.RecordId, Is.EqualTo(revisionA.RecordId));
                    Assert.That(revisionB.Owner, Is.SameAs(manager.CurrentSkin.Value));
                    Assert.That(existingHost.Texture, Is.SameAs(expectedB));
                    Assert.That(attachedHost.Texture, Is.SameAs(expectedB));
                    Assert.That(existingHost.Texture, Is.Not.SameAs(existingTextureA));
                    Assert.That(attachedHost.Texture, Is.Not.SameAs(attachedTextureA));
                    Assert.That(prepareAttempts, Is.GreaterThanOrEqualTo(2));
                    Assert.That(beforeCommitCalls, Is.GreaterThanOrEqualTo(2));
                });
            });
            AddUntilStep("wait for dynamic-attach A exactly-once retirement", () =>
                revisionA.Retired.IsCompleted && retiredA == 1);
            AddStep("clear dynamic-attach barrier hooks", () =>
            {
                manager.CurrentRevisionBeforeCommitSchedule = () => { };
                firstBarrierPrepared.Dispose();
                releaseFirstBarrier.Dispose();
                Assert.That(retiredA, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestRealStarFountainDetachAfterPrepareForcesFreshBarrierWithoutDetachedConsumer()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            FullSkinSettingsCallerHost caller = null!;
            CurrentRevisionStarFountainHost survivingHost = null!;
            CurrentRevisionStarFountainHost detachingHost = null!;
            SkinCurrentRevision revisionA = null!;
            Skin ownerA = null!;
            Texture survivingTextureA = null!;
            Texture detachingTextureA = null!;
            int prepareAttempts = 0;
            int beforeCommitCalls = 0;
            int retiredA = 0;
            var firstBarrierPrepared = new ManualResetEventSlim();
            var releaseFirstBarrier = new ManualResetEventSlim();

            AddStep("create and select dynamic-detach revision A", () =>
            {
                (packageRoot, candidate) = createCandidate(
                    root => writeStarFountainRevisionPackage(root, "A", new Rgba32(215, 65, 105, 255)),
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for dynamic-detach exact A pair", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount surviving and detaching real StarFountain consumers", () =>
            {
                revisionA = manager.CurrentRevision;
                ownerA = manager.CurrentSkin.Value;
                manager.CurrentRevisionRetired += revision =>
                {
                    if (ReferenceEquals(revision, revisionA))
                        retiredA++;
                };

                Add(survivingHost = new CurrentRevisionStarFountainHost(manager));
                Add(detachingHost = new CurrentRevisionStarFountainHost(manager));
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for both dynamic-detach consumers", () =>
                survivingHost.Fountain.IsLoaded
                && detachingHost.Fountain.IsLoaded
                && survivingHost.Texture != null
                && detachingHost.Texture != null
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("pause first commit schedule after both consumers prepared", () =>
            {
                survivingTextureA = survivingHost.Texture!;
                detachingTextureA = detachingHost.Texture!;
                manager.CurrentRevisionPrepareStarted = () => Interlocked.Increment(ref prepareAttempts);
                manager.CurrentRevisionBeforeCommitSchedule = () =>
                {
                    int invocation = Interlocked.Increment(ref beforeCommitCalls);

                    if (invocation != 1)
                        return;

                    firstBarrierPrepared.Set();

                    if (!releaseFirstBarrier.Wait(TimeSpan.FromSeconds(30)))
                        throw new TimeoutException("Timed out waiting to detach the real StarFountain participant.");
                };
            });
            AddStep("write B and invoke real reload before detach", () =>
            {
                writeStarFountainRevisionPackage(packageRoot, "B", new Rgba32(35, 185, 215, 255));
                caller.ReloadCurrentButton.TriggerClick();
            });
            AddUntilStep("wait until detach barrier is fully prepared", () => firstBarrierPrepared.IsSet);
            AddStep("detach registered StarFountain while prepared barrier is held", () =>
            {
                Assert.That(detachingHost.Texture, Is.SameAs(detachingTextureA));
                detachingHost.Expire();
            });
            AddUntilStep("wait for real participant disposal before commit", () =>
                detachingHost.Parent == null && detachingHost.IsHostDisposed);
            AddStep("assert A is still exact and release stale detach barrier", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(survivingHost.Texture, Is.SameAs(survivingTextureA));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                });

                releaseFirstBarrier.Set();
            });
            AddUntilStep("wait for fresh barrier to publish B without detached consumer", () =>
                Volatile.Read(ref prepareAttempts) >= 2
                && Volatile.Read(ref beforeCommitCalls) >= 2
                && !ReferenceEquals(manager.CurrentRevision, revisionA)
                && !ReferenceEquals(survivingHost.Texture, survivingTextureA)
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("assert detach was removed safely from retried barrier", () =>
            {
                SkinCurrentRevision revisionB = manager.CurrentRevision;

                Assert.Multiple(() =>
                {
                    Assert.That(revisionB.RecordId, Is.EqualTo(revisionA.RecordId));
                    Assert.That(revisionB.Owner, Is.SameAs(manager.CurrentSkin.Value));
                    Assert.That(survivingHost.Texture, Is.SameAs(revisionB.Owner.GetTexture("Menu/fountain-star")));
                    Assert.That(detachingHost.Parent, Is.Null);
                    Assert.That(detachingHost.IsHostDisposed, Is.True);
                    Assert.That(prepareAttempts, Is.GreaterThanOrEqualTo(2));
                    Assert.That(beforeCommitCalls, Is.GreaterThanOrEqualTo(2));
                });
            });
            AddUntilStep("wait for dynamic-detach A exactly-once retirement", () =>
                revisionA.Retired.IsCompleted && retiredA == 1);
            AddStep("clear dynamic-detach barrier hooks", () =>
            {
                manager.CurrentRevisionBeforeCommitSchedule = () => { };
                firstBarrierPrepared.Dispose();
                releaseFirstBarrier.Dispose();
                Assert.That(retiredA, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestInvisibleProductionLoadRejectsReloadBeforePrepareUntilFormalParticipantRegisters()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            FullSkinSettingsCallerHost caller = null!;
            InvisibleLoadStarFountainHost invisibleHost = null!;
            SkinCurrentRevision revisionA = null!;
            SkinCurrentRevision revisionB = null!;
            Skin ownerA = null!;
            Live<SkinInfo> selectionA = null!;
            Texture textureA = null!;
            int prepareCalls = 0;
            int retiredA = 0;
            var loaderEntered = new ManualResetEventSlim();
            var allowLoader = new ManualResetEventSlim();

            AddStep("create and select invisible-load revision A", () =>
            {
                (packageRoot, candidate) = createCandidate(
                    root => writeStarFountainRevisionPackage(root, "A", new Rgba32(230, 50, 95, 255)),
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for invisible-load exact A pair", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount real async-load host and reload caller", () =>
            {
                revisionA = manager.CurrentRevision;
                ownerA = manager.CurrentSkin.Value;
                selectionA = manager.CurrentSkinInfo.Value;
                manager.CurrentRevisionPrepareStarted = () => Interlocked.Increment(ref prepareCalls);
                manager.CurrentRevisionRetired += revision =>
                {
                    if (ReferenceEquals(revision, revisionA))
                        retiredA++;
                };

                Add(invisibleHost = new InvisibleLoadStarFountainHost(manager, loaderEntered, allowLoader));
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for async-load host and real reload caller", () =>
                invisibleHost.IsLoaded
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("assert A has no consumer before invisible production load", () =>
            {
                textureA = ownerA.GetTexture("Menu/fountain-star")!;

                Assert.Multiple(() =>
                {
                    Assert.That(textureA, Is.Not.Null);
                    Assert.That(revisionA.ConsumersDetached.IsCompleted, Is.True);
                    Assert.That(revisionA.WorkDetached.IsCompleted, Is.True);
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                });

                invisibleHost.BeginLoad();
            });
            AddUntilStep("wait inside production StarFountain BDL before spewer lookup", () => loaderEntered.IsSet);
            AddStep("assert invisible initial-load participant retained exact A", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(invisibleHost.LoadCompleted, Is.False);
                    Assert.That(revisionA.ConsumersDetached.IsCompleted, Is.False);
                    Assert.That(revisionA.WorkDetached.IsCompleted, Is.True,
                        "The initial-load participant is a visual lease, not shutdown-joinable owner work.");
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(retiredA, Is.Zero);
                });
            });
            AddStep("write B and invoke reload while production loader is invisible", () =>
            {
                writeStarFountainRevisionPackage(packageRoot, "B", new Rgba32(30, 195, 220, 255));
                caller.ReloadCurrentButton.TriggerClick();
                Assert.That(caller.ReloadCurrentButton.Enabled.Value, Is.False);
            });
            AddUntilStep("wait for fail-closed reload feedback", () => caller.ReloadCurrentButton.Enabled.Value);
            AddStep("assert temporary participant rejected before source preparation", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(invisibleHost.LoadCompleted, Is.False);
                    Assert.That(revisionA.ConsumersDetached.IsCompleted, Is.False);
                    Assert.That(revisionA.WorkDetached.IsCompleted, Is.True);
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(Volatile.Read(ref prepareCalls), Is.Zero);
                    Assert.That(retiredA, Is.Zero);
                    Assert.That(caller.PostedNotifications, Has.Count.EqualTo(1));
                    Assert.That(
                        caller.PostedNotifications[0].Text.ToString(),
                        Is.EqualTo(Localisation.SkinSettingsStrings.CurrentSkinReloadRejected.ToString()),
                        "The temporary participant rejection must not be misreported as shutdown/failure.");
                });

                allowLoader.Set();
            });
            AddUntilStep("wait for formal registration and exact A rebuild", () =>
                invisibleHost.LoadTask.IsCompleted
                && invisibleHost.LoadCompleted
                && invisibleHost.Fountain.IsLoaded
                && invisibleHost.Texture != null
                && ReferenceEquals(invisibleHost.Texture, ownerA.GetTexture("Menu/fountain-star")));
            AddStep("assert formal participant replaced temporary blocker without a lease gap", () =>
            {
                invisibleHost.LoadTask.GetAwaiter().GetResult();

                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(invisibleHost.Texture, Is.SameAs(textureA));
                    Assert.That(revisionA.ConsumersDetached.IsCompleted, Is.False);
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(retiredA, Is.Zero);
                });
            });
            AddStep("retry B after formal participant registration", () => caller.ReloadCurrentButton.TriggerClick());
            AddUntilStep("wait for coherent B publication after load completes", () =>
                Volatile.Read(ref prepareCalls) == 1
                && !ReferenceEquals(manager.CurrentRevision, revisionA)
                && !ReferenceEquals(manager.CurrentSkin.Value, ownerA)
                && !ReferenceEquals(invisibleHost.Texture, textureA)
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("assert formal participant published exact B", () =>
            {
                revisionB = manager.CurrentRevision;

                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(revisionB.RecordId, Is.EqualTo(revisionA.RecordId));
                    Assert.That(revisionB.ContentRevision, Is.Not.EqualTo(revisionA.ContentRevision));
                    Assert.That(revisionB.Owner, Is.SameAs(manager.CurrentSkin.Value));
                    Assert.That(invisibleHost.Texture, Is.SameAs(revisionB.Owner.GetTexture("Menu/fountain-star")));
                });

                invisibleHost.Expire();
            });
            AddUntilStep("wait for A exactly-once retirement", () =>
                revisionA.ConsumersDetached.IsCompleted
                && revisionA.WorkDetached.IsCompleted
                && revisionA.Retired.IsCompleted
                && retiredA == 1);
            AddUntilStep("wait for late B consumer real detach", () =>
                invisibleHost.Parent == null
                && invisibleHost.IsHostDisposed
                && revisionB.ConsumersDetached.IsCompleted);
            AddStep("assert late detach did not double-retire A", () =>
            {
                Assert.That(retiredA, Is.EqualTo(1));
                loaderEntered.Dispose();
                allowLoader.Dispose();
            });
        }

        [Test]
        public void TestRealPoolableSamplePublishesBAndRetainsPlayingATailUntilStop()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            CurrentRevisionSampleHost sampleHost = null!;
            FullSkinSettingsCallerHost caller = null!;
            SkinCurrentRevision revisionA = null!;
            DrawableSample sampleA = null!;
            int retiredA = 0;
            bool sampleDisposedAtRetire = false;

            AddStep("create and select sample revision A", () =>
            {
                (packageRoot, candidate) = createCandidate(
                    root => writeSampleRevisionPackage(root, "A", sampleFrames: 22050),
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for sample A pair", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID);
            AddStep("mount production poolable sample and reload caller", () =>
            {
                revisionA = manager.CurrentRevision;
                manager.CurrentRevisionRetired += revision =>
                {
                    if (ReferenceEquals(revision, revisionA))
                    {
                        sampleDisposedAtRetire = sampleHost.SampleDrawable.IsOwnedSampleDisposed(sampleA);
                        retiredA++;
                    }
                };
                Add(sampleHost = new CurrentRevisionSampleHost(manager));
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for exact A sample", () =>
                sampleHost.SampleDrawable.IsLoaded
                && sampleHost.SampleDrawable.Sample != null
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("start looping A channel and write B", () =>
            {
                sampleA = sampleHost.SampleDrawable.Sample!;
                Assert.That(sampleA.Length, Is.GreaterThan(500));
                sampleHost.SampleDrawable.Looping = true;
                sampleHost.SampleDrawable.Play();
                Assert.That(sampleHost.SampleDrawable.Playing, Is.True);

                writeSampleRevisionPackage(packageRoot, "B", sampleFrames: 4410);
                caller.ReloadCurrentButton.TriggerClick();
            });
            AddUntilStep("wait for coherent B sample publication", () =>
                !ReferenceEquals(manager.CurrentRevision, revisionA)
                && !ReferenceEquals(sampleHost.SampleDrawable.Sample, sampleA)
                && sampleHost.SampleDrawable.Sample?.Length < 500
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("assert active A tail retains old owner", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(sampleHost.SampleDrawable.Sample, Is.Not.SameAs(sampleA));
                    Assert.That(sampleHost.SampleDrawable.Sample!.Length, Is.LessThan(500));
                    Assert.That(sampleHost.SampleDrawable.Playing, Is.True);
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(retiredA, Is.Zero);
                });

                sampleHost.SampleDrawable.Stop();
            });
            AddUntilStep("wait for A sample tail detach and exactly-once retire", () =>
                revisionA.Retired.IsCompleted && retiredA == 1);
            AddStep("assert sample A retirement is not duplicated", () =>
            {
                sampleHost.SampleDrawable.Stop();
                Assert.Multiple(() =>
                {
                    Assert.That(sampleDisposedAtRetire, Is.True);
                    Assert.That(retiredA, Is.EqualTo(1));
                });
            });
        }

        [Test]
        public void TestPoolableSampleDisposesDrawableHierarchyBeforeFinalOldRevisionDetach()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            CurrentRevisionSampleHost sampleHost = null!;
            FullSkinSettingsCallerHost caller = null!;
            SkinCurrentRevision revisionA = null!;
            DrawableSample sampleA = null!;
            bool sampleDisposedAtRetire = false;
            int retiredA = 0;

            AddStep("create and select sample-dispose revision A", () =>
            {
                (packageRoot, candidate) = createCandidate(
                    root => writeSampleRevisionPackage(root, "A", sampleFrames: 22050),
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for sample-dispose A pair", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount real sample-dispose host", () =>
            {
                revisionA = manager.CurrentRevision;
                manager.CurrentRevisionRetired += revision =>
                {
                    if (!ReferenceEquals(revision, revisionA))
                        return;

                    sampleDisposedAtRetire = sampleHost.SampleDrawable.IsOwnedSampleDisposed(sampleA);
                    Interlocked.Increment(ref retiredA);
                };
                Add(sampleHost = new CurrentRevisionSampleHost(manager));
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for real sample-dispose A", () =>
                sampleHost.SampleDrawable.IsLoaded
                && sampleHost.SampleDrawable.Sample != null
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("start A tail and publish B", () =>
            {
                sampleA = sampleHost.SampleDrawable.Sample!;
                sampleHost.SampleDrawable.Looping = true;
                sampleHost.SampleDrawable.Play();
                writeSampleRevisionPackage(packageRoot, "B", sampleFrames: 4410);
                caller.ReloadCurrentButton.TriggerClick();
            });
            AddUntilStep("wait for B with active A sample tail", () =>
                !ReferenceEquals(manager.CurrentRevision, revisionA)
                && !ReferenceEquals(sampleHost.SampleDrawable.Sample, sampleA)
                && revisionA.Retired.IsCompleted == false);
            AddStep("dispose complete real sample host", () => sampleHost.Expire());
            AddUntilStep("wait for final sample detach and A retirement", () =>
                sampleHost.Parent == null
                && revisionA.Retired.IsCompleted
                && Volatile.Read(ref retiredA) == 1);
            AddStep("assert drawable teardown preceded owner retirement", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(sampleHost.SampleDrawable.IsOwnedSampleDisposed(sampleA), Is.True);
                    Assert.That(sampleDisposedAtRetire, Is.True);
                    Assert.That(retiredA, Is.EqualTo(1));
                });
            });
        }

        [Test]
        public void TestUpdateThreadShutdownReapsLoopingSampleWorkBeforeFinalVisualDetach()
        {
            Live<SkinInfo> candidate = null!;
            CurrentRevisionSampleHost sampleHost = null!;
            SkinCurrentRevision revision = null!;
            DrawableSample sample = null!;
            bool sampleDisposedAtRetire = false;
            int retired = 0;

            AddStep("create and select looping-shutdown revision", () =>
            {
                (_, candidate) = createCandidate(
                    root => writeSampleRevisionPackage(root, "loop", sampleFrames: 22050),
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for looping-shutdown pair", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount exact looping sample owner", () =>
            {
                revision = manager.CurrentRevision;
                manager.CurrentRevisionRetired += retiredRevision =>
                {
                    if (!ReferenceEquals(retiredRevision, revision))
                        return;

                    sampleDisposedAtRetire = sampleHost.SampleDrawable.IsOwnedSampleDisposed(sample);
                    Interlocked.Increment(ref retired);
                };
                Add(sampleHost = new CurrentRevisionSampleHost(manager));
            });
            AddUntilStep("wait for exact looping sample", () =>
                sampleHost.SampleDrawable.IsLoaded
                && sampleHost.SampleDrawable.Sample != null);
            AddStep("start looping channel and shutdown on update thread", () =>
            {
                sample = sampleHost.SampleDrawable.Sample!;
                sampleHost.SampleDrawable.Looping = true;
                sampleHost.SampleDrawable.Play();

                Assert.Multiple(() =>
                {
                    Assert.That(sampleHost.SampleDrawable.Playing, Is.True);
                    Assert.That(revision.WorkDetached.IsCompleted, Is.False);
                    Assert.That(sampleHost.SampleDrawable.IsOwnedSampleDisposed(sample), Is.False);
                });

                manager.ShutdownManagedFolderMutations();

                Assert.Multiple(() =>
                {
                    Assert.That(sampleHost.SampleDrawable.Playing, Is.False);
                    Assert.That(sampleHost.SampleDrawable.Sample, Is.Null);
                    Assert.That(sampleHost.SampleDrawable.IsOwnedSampleDisposed(sample), Is.True);
                    Assert.That(revision.WorkDetached.IsCompletedSuccessfully, Is.True);
                    Assert.That(revision.ConsumersDetached.IsCompleted, Is.False);
                    Assert.That(revision.Retired.IsCompleted, Is.False);
                    Assert.That(retired, Is.Zero);
                });

                manager.ShutdownManagedFolderMutations();
                Assert.That(retired, Is.Zero);
            });
            AddWaitStep("run retained looping owner after graph reap", 1);
            AddStep("assert retained audio surface then detach visual owner", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(sampleHost.SampleDrawable.Volume.Value, Is.EqualTo(1));
                    Assert.That(sampleHost.SampleDrawable.Sample, Is.Null);
                    Assert.That(sampleHost.SampleDrawable.Playing, Is.False);
                    Assert.That(sampleHost.SampleDrawable.IsOwnedSampleDisposed(sample), Is.True);
                    Assert.That(revision.WorkDetached.IsCompletedSuccessfully, Is.True);
                    Assert.That(revision.ConsumersDetached.IsCompleted, Is.False);
                });

                Assert.That(Remove(sampleHost, disposeImmediately: true), Is.True);
            });
            AddUntilStep("wait for final looping owner retirement", () =>
                revision.ConsumersDetached.IsCompleted
                && revision.Retired.IsCompleted
                && Volatile.Read(ref retired) == 1);
            AddStep("assert looping owner retired once after graph reap", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(sampleDisposedAtRetire, Is.True);
                    Assert.That(retired, Is.EqualTo(1));
                });
            });
        }

        [Test]
        public void TestShutdownAbortsPreparedSampleSwapBeforeCommitWithoutSplitOrDoubleReap()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            CurrentRevisionSampleHost sampleHost = null!;
            Task<SkinCurrentRevisionReloadResult> reload = null!;
            Action? pendingCommit = null;
            SkinCurrentRevision revisionA = null!;
            Skin ownerA = null!;
            Live<SkinInfo> selectionA = null!;
            DrawableSample sampleA = null!;
            int retiredA = 0;

            AddStep("create and select prepared-sample revision A", () =>
            {
                (packageRoot, candidate) = createCandidate(
                    root => writeSampleRevisionPackage(root, "A", sampleFrames: 22050),
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for prepared-sample A pair", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount real sample owner and hold commit callback", () =>
            {
                revisionA = manager.CurrentRevision;
                ownerA = manager.CurrentSkin.Value;
                selectionA = manager.CurrentSkinInfo.Value;
                manager.CurrentRevisionRetired += retiredRevision =>
                {
                    if (ReferenceEquals(retiredRevision, revisionA))
                        Interlocked.Increment(ref retiredA);
                };
                manager.CurrentRevisionCompletionSchedule = callback =>
                    Volatile.Write(ref pendingCommit, callback);
                Add(sampleHost = new CurrentRevisionSampleHost(manager));
            });
            AddUntilStep("wait for real prepared-sample A", () =>
                sampleHost.SampleDrawable.IsLoaded
                && sampleHost.SampleDrawable.Sample != null);
            AddStep("prepare B without running commit", () =>
            {
                sampleA = sampleHost.SampleDrawable.Sample!;
                writeSampleRevisionPackage(packageRoot, "B", sampleFrames: 4410);
                reload = manager.ReloadCurrentRevisionAsync();
            });
            AddUntilStep("wait for held prepared-sample commit", () =>
                Volatile.Read(ref pendingCommit) != null
                && revisionA.WorkDetached.IsCompleted == false);
            AddStep("shutdown claims prepared callback and owner work", () =>
            {
                manager.ShutdownManagedFolderMutations();

                Assert.Multiple(() =>
                {
                    Assert.That(reload.IsCompletedSuccessfully, Is.True);
                    Assert.That(reload.GetAwaiter().GetResult(), Is.EqualTo(SkinCurrentRevisionReloadResult.Shutdown));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(manager.CurrentRevision.Owner, Is.SameAs(ownerA));
                    Assert.That(sampleHost.SampleDrawable.IsOwnedSampleDisposed(sampleA), Is.True);
                    Assert.That(revisionA.WorkDetached.IsCompletedSuccessfully, Is.True);
                    Assert.That(revisionA.ConsumersDetached.IsCompleted, Is.False);
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(retiredA, Is.Zero);
                });
            });
            AddStep("run stale commit and detach exact A participant", () =>
            {
                Interlocked.Exchange(ref pendingCommit, null)!();

                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(sampleHost.SampleDrawable.IsOwnedSampleDisposed(sampleA), Is.True);
                    Assert.That(retiredA, Is.Zero);
                });

                Assert.That(Remove(sampleHost, disposeImmediately: true), Is.True);
            });
            AddUntilStep("wait for exact A retirement", () =>
                revisionA.Retired.IsCompleted
                && Volatile.Read(ref retiredA) == 1);
            AddStep("assert stale commit did not double-reap", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(revisionA.Retired.IsCompletedSuccessfully, Is.True);
                    Assert.That(retiredA, Is.EqualTo(1));
                    Assert.That(sampleHost.SampleDrawable.IsOwnedSampleDisposed(sampleA), Is.True);
                });
            });
        }

        [Test]
        public void TestPendingSampleSwapDisposesPreparedBAndPreviousABeforeShutdownRetirement()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            CurrentRevisionSampleHost sampleHost = null!;
            Task<SkinCurrentRevisionReloadResult> reload = null!;
            Task shutdown = null!;
            Action? pendingCommit = null;
            SkinCurrentRevision revisionA = null!;
            SkinCurrentRevision revisionB = null!;
            DrawableSample sampleA = null!;
            DrawableSample sampleB = null!;
            int retiredA = 0;
            int retiredB = 0;
            bool sampleADisposedAtRetire = false;
            bool sampleBDisposedAtRetire = false;

            AddStep("create and select pending-sample revision A", () =>
            {
                (packageRoot, candidate) = createCandidate(
                    root => writeSampleRevisionPackage(root, "A", sampleFrames: 22050),
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for pending-sample A pair", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
            AddStep("mount real pending-sample host and hold commit callback", () =>
            {
                revisionA = manager.CurrentRevision;
                manager.CurrentRevisionRetired += revision =>
                {
                    if (ReferenceEquals(revision, revisionA))
                    {
                        sampleADisposedAtRetire = sampleHost.SampleDrawable.IsOwnedSampleDisposed(sampleA);
                        Interlocked.Increment(ref retiredA);
                    }

                    if (ReferenceEquals(revision, revisionB))
                    {
                        sampleBDisposedAtRetire = sampleHost.SampleDrawable.IsOwnedSampleDisposed(sampleB);
                        Interlocked.Increment(ref retiredB);
                    }
                };

                manager.CurrentRevisionCompletionSchedule = callback =>
                    Volatile.Write(ref pendingCommit, callback);
                Add(sampleHost = new CurrentRevisionSampleHost(manager));
            });
            AddUntilStep("wait for real pending-sample A", () =>
                sampleHost.SampleDrawable.IsLoaded
                && sampleHost.SampleDrawable.Sample != null);
            AddStep("write B and prepare held sample commit", () =>
            {
                sampleA = sampleHost.SampleDrawable.Sample!;
                writeSampleRevisionPackage(packageRoot, "B", sampleFrames: 4410);
                reload = manager.ReloadCurrentRevisionAsync();
            });
            AddUntilStep("wait for held sample commit", () => Volatile.Read(ref pendingCommit) != null);
            AddStep("commit B and start shutdown before the next drawable update", () =>
            {
                Action commit = Interlocked.Exchange(ref pendingCommit, null)!;
                commit();

                Assert.That(reload.Wait(TimeSpan.FromSeconds(10)), Is.True);
                Assert.That(reload.GetAwaiter().GetResult(), Is.EqualTo(SkinCurrentRevisionReloadResult.Success));

                revisionB = manager.CurrentRevision;
                sampleB = sampleHost.SampleDrawable.Sample!;

                Assert.Multiple(() =>
                {
                    Assert.That(revisionB, Is.Not.SameAs(revisionA));
                    Assert.That(sampleB, Is.Not.SameAs(sampleA));
                    Assert.That(sampleB.Length, Is.LessThan(500));
                    Assert.That(sampleHost.SampleDrawable.IsOwnedSampleDisposed(sampleA), Is.False);
                    Assert.That(sampleHost.SampleDrawable.IsOwnedSampleDisposed(sampleB), Is.False);
                    Assert.That(revisionA.WorkDetached.IsCompleted, Is.False,
                        "The pending swap must retain A until its old drawable graph is torn down.");
                });

                shutdown = Task.Run(() => manager.ShutdownManagedFolderMutations());

                // Stay within this update callback so PoolableSkinnableSample cannot drain the pending swap first.
                // Registration admission closes under the same publication lock as the shutdown claim, providing an
                // exact synchronization point rather than relying on a later frame or an arbitrary delay.
                bool publicationClaimed = SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            manager.RegisterRevisionParticipant(
                                       SkinRevisionParticipantKind.LifecycleHolder,
                                       "pending-sample shutdown probe")
                                   .Dispose();
                            return false;
                        }
                        catch (ObjectDisposedException)
                        {
                            return true;
                        }
                    },
                    TimeSpan.FromSeconds(10));

                Assert.Multiple(() =>
                {
                    Assert.That(publicationClaimed, Is.True);
                    Assert.That(shutdown.IsCompleted, Is.False,
                        "Background shutdown must stay joined until the exact owner reaches its update-thread reap.");
                });
            });
            AddUntilStep("wait for owner graph reap, A retirement and shutdown join", () =>
                shutdown.IsCompleted
                && revisionA.ConsumersDetached.IsCompleted
                && revisionA.WorkDetached.IsCompleted
                && revisionA.Retired.IsCompleted
                && Volatile.Read(ref retiredA) == 1
                && sampleHost.SampleDrawable.IsOwnedSampleDisposed(sampleA)
                && sampleHost.SampleDrawable.IsOwnedSampleDisposed(sampleB));
            AddStep("assert shutdown reaped work without faking B visual detach", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(shutdown.IsCompletedSuccessfully, Is.True);
                    Assert.That(sampleHost.SampleDrawable.IsOwnedSampleDisposed(sampleA), Is.True);
                    Assert.That(sampleHost.SampleDrawable.IsOwnedSampleDisposed(sampleB), Is.True);
                    Assert.That(sampleADisposedAtRetire, Is.True);
                    Assert.That(retiredA, Is.EqualTo(1));
                    Assert.That(revisionB.WorkDetached.IsCompletedSuccessfully, Is.True);
                    Assert.That(revisionB.ConsumersDetached.IsCompleted, Is.False);
                    Assert.That(revisionB.Retired.IsCompleted, Is.False);
                    Assert.That(retiredB, Is.Zero);
                    Assert.That(sampleHost.SampleDrawable.Volume.Value, Is.EqualTo(1));
                });

                Assert.That(Remove(sampleHost, disposeImmediately: true), Is.True,
                    "The retained participant must still detach through its real parent host.");
            });
            AddUntilStep("wait for B final detach and exactly-once retirement", () =>
                revisionB.ConsumersDetached.IsCompleted
                && revisionB.Retired.IsCompleted
                && Volatile.Read(ref retiredB) == 1);
            AddStep("assert both sample graphs preceded final owner retirement", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(sampleADisposedAtRetire, Is.True);
                    Assert.That(sampleBDisposedAtRetire, Is.True);
                    Assert.That(retiredA, Is.EqualTo(1));
                    Assert.That(retiredB, Is.EqualTo(1));
                });

                manager.ShutdownManagedFolderMutations();
                Assert.Multiple(() =>
                {
                    Assert.That(retiredA, Is.EqualTo(1));
                    Assert.That(retiredB, Is.EqualTo(1));
                });
            });
        }

        [Test]
        public void TestSkinSpriteStoryboardBlocksSameIdReloadUntilRealDetach()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            CurrentRevisionStoryboardHost storyboardHost = null!;
            FullSkinSettingsCallerHost caller = null!;
            SkinCurrentRevision revisionA = null!;
            Texture textureA = null!;
            int prepareCalls = 0;

            AddStep("create and select storyboard revision A", () =>
            {
                (packageRoot, candidate) = createCandidate(
                    root => writeStarFountainRevisionPackage(root, "A", new Rgba32(180, 60, 90, 255)),
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for storyboard A pair", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID);
            AddStep("mount real skin-sprite storyboard and reload caller", () =>
            {
                revisionA = manager.CurrentRevision;
                manager.CurrentRevisionPrepareStarted = () => prepareCalls++;
                Add(storyboardHost = new CurrentRevisionStoryboardHost(manager));
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for real storyboard drawable and reload caller", () =>
                storyboardHost.Sprite.IsLoaded
                && caller.ReloadCurrentButton.Enabled.Value);
            AddUntilStep("wait for storyboard skin texture", () => storyboardHost.Texture != null);
            AddStep("write B and request reload with storyboard attached", () =>
            {
                textureA = storyboardHost.Texture!;
                writeStarFountainRevisionPackage(packageRoot, "B", new Rgba32(20, 170, 210, 255));
                caller.ReloadCurrentButton.TriggerClick();
            });
            AddUntilStep("wait for deterministic storyboard rejection", () =>
                prepareCalls == 0 && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("assert storyboard rejection preserved exact A", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(revisionA.Owner));
                    Assert.That(storyboardHost.Texture, Is.SameAs(textureA));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(prepareCalls, Is.Zero,
                        "An unsupported storyboard participant must reject before package preparation begins.");
                    Assert.That(caller.PostedNotifications, Has.Count.EqualTo(1));
                    Assert.That(
                        caller.PostedNotifications[0].Text.ToString(),
                        Is.EqualTo(Localisation.SkinSettingsStrings.CurrentSkinReloadRejected.ToString()));
                });

                storyboardHost.Expire();
            });
            AddUntilStep("wait for storyboard real detach", () => storyboardHost.Parent == null);
            AddStep("retry reload after storyboard detach", () => caller.ReloadCurrentButton.TriggerClick());
            AddUntilStep("wait for B after storyboard detach", () =>
                !ReferenceEquals(manager.CurrentRevision, revisionA)
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("assert detached blocker allowed coherent B", () =>
                Assert.That(manager.CurrentRevision.ContentRevision, Is.Not.EqualTo(revisionA.ContentRevision)));
        }

        private static void writeStarFountainRevisionPackage(string packageRoot, string revision, Rgba32 colour)
        {
            writeRevisionPackage(packageRoot, revision, colour);
            File.WriteAllBytes(Path.Combine(packageRoot, "star2.png"), createPng(colour));
        }

        private static void writeSampleRevisionPackage(string packageRoot, string revision, int sampleFrames)
        {
            writeRevisionPackage(packageRoot, revision, new Rgba32(40, 80, 160, 255));
            File.WriteAllBytes(Path.Combine(packageRoot, "test-sample.wav"), createPcmWav(sampleFrames));
        }

        private static byte[] createPcmWav(int sampleFrames)
        {
            const int sample_rate = 22050;
            const short channels = 1;
            const short bits_per_sample = 16;
            int dataLength = checked(sampleFrames * channels * bits_per_sample / 8);
            using var stream = new MemoryStream(44 + dataLength);
            using var writer = new BinaryWriter(stream);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataLength);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channels);
            writer.Write(sample_rate);
            writer.Write(sample_rate * channels * bits_per_sample / 8);
            writer.Write((short)(channels * bits_per_sample / 8));
            writer.Write(bits_per_sample);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataLength);

            for (int i = 0; i < sampleFrames; i++)
                writer.Write((short)(Math.Sin(i * 2 * Math.PI * 440 / sample_rate) * short.MaxValue / 8));

            writer.Flush();
            return stream.ToArray();
        }

        private partial class CurrentRevisionStarFountainHost : CompositeDrawable
        {
            [Cached]
            private readonly SkinManager skinManager;

            [Cached(typeof(ISkinSource))]
            private readonly ISkinSource skinSource;

            public StarFountain Fountain { get; } = new StarFountain();

            public Texture? Texture => Fountain.ChildrenOfType<StarFountain.StarFountainSpewer>().SingleOrDefault()?.Texture;

            public bool IsHostDisposed => IsDisposed;

            public CurrentRevisionStarFountainHost(SkinManager skinManager)
            {
                this.skinManager = skinManager;
                skinSource = skinManager;
                RelativeSizeAxes = Axes.Both;
                InternalChild = Fountain;
            }
        }

        private partial class CurrentRevisionSampleHost : CompositeDrawable
        {
            [Cached]
            private readonly SkinManager skinManager;

            [Cached(typeof(ISkinSource))]
            private readonly ISkinSource skinSource;

            public PoolableSkinnableSample SampleDrawable { get; } = new PoolableSkinnableSample(
                new SampleInfo("test-sample"));

            public CurrentRevisionSampleHost(SkinManager skinManager)
            {
                this.skinManager = skinManager;
                skinSource = skinManager;
                InternalChild = SampleDrawable;
            }
        }

        private partial class InvisibleLoadStarFountainHost : CompositeDrawable
        {
            [Cached]
            private readonly SkinManager skinManager;

            [Cached(typeof(ISkinSource))]
            private readonly ISkinSource skinSource;

            private int loadStarted;
            private int loadCompleted;

            public GatedInitialLoadStarFountain Fountain { get; }

            public Task LoadTask { get; private set; } = Task.CompletedTask;

            public bool LoadCompleted => Volatile.Read(ref loadCompleted) != 0;

            public bool IsHostDisposed => IsDisposed;

            public Texture? Texture => Fountain.ChildrenOfType<StarFountain.StarFountainSpewer>().SingleOrDefault()?.Texture;

            public InvisibleLoadStarFountainHost(
                SkinManager skinManager,
                ManualResetEventSlim loaderEntered,
                ManualResetEventSlim allowLoader)
            {
                this.skinManager = skinManager;
                skinSource = skinManager;
                Fountain = new GatedInitialLoadStarFountain(loaderEntered, allowLoader);
                RelativeSizeAxes = Axes.Both;
            }

            public void BeginLoad()
            {
                if (Interlocked.Exchange(ref loadStarted, 1) != 0)
                    throw new InvalidOperationException("The invisible StarFountain load may only start once.");

                LoadTask = LoadComponentAsync(Fountain, loaded =>
                {
                    InternalChild = loaded;
                    Volatile.Write(ref loadCompleted, 1);
                });
            }
        }

        private partial class GatedInitialLoadStarFountain : StarFountain
        {
            private readonly ManualResetEventSlim loaderEntered;
            private readonly ManualResetEventSlim allowLoader;

            public GatedInitialLoadStarFountain(
                ManualResetEventSlim loaderEntered,
                ManualResetEventSlim allowLoader)
            {
                this.loaderEntered = loaderEntered;
                this.allowLoader = allowLoader;
            }

            protected override StarFountainSpewer CreateSpewer()
            {
                loaderEntered.Set();

                if (!allowLoader.Wait(TimeSpan.FromSeconds(30)))
                    throw new TimeoutException("Timed out waiting to release the invisible StarFountain loader.");

                return base.CreateSpewer();
            }
        }

        private partial class CurrentRevisionStoryboardHost : CompositeDrawable
        {
            [Cached]
            private readonly SkinManager skinManager;

            [Cached(typeof(ISkinSource))]
            private readonly ISkinSource skinSource;

            [Cached]
            private readonly Storyboard storyboard = new Storyboard { UseSkinSprites = true };

            public DrawableStoryboardSprite Sprite { get; }

            public Texture? Texture => Sprite.Texture;

            public CurrentRevisionStoryboardHost(SkinManager skinManager)
            {
                this.skinManager = skinManager;
                skinSource = skinManager;

                var sprite = new StoryboardSprite("Menu/fountain-star", Anchor.Centre, Vector2.Zero);
                sprite.Commands.AddAlpha(Easing.None, 0, 60_000, 1, 1);
                Sprite = new DrawableStoryboardSprite(sprite)
                {
                    LifetimeStart = double.MinValue,
                    LifetimeEnd = double.MaxValue
                };
                InternalChild = Sprite;
            }
        }
    }
}
