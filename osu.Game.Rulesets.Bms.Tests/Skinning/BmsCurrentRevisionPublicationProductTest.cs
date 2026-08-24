// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestCurrentRevisionReloadButtonPublishesSameIdPackageAtomically(bool external)
        {
            var context = new CurrentRevisionProductContext();
            FullSkinSettingsCallerHost caller = null!;
            SkinCurrentRevision revisionA = null!;
            Skin ownerA = null!;
            Live<SkinInfo> selectionA = null!;
            int prepareCount = 0;

            addSelectRevisionA(context, external);
            AddStep("mount real skin settings reload caller", () => Add(caller = new FullSkinSettingsCallerHost(manager)));
            AddUntilStep("wait for reload affordance", () => caller.ReloadCurrentButton.IsLoaded && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("capture exact A pair", () =>
            {
                revisionA = manager.CurrentRevision;
                ownerA = manager.CurrentSkin.Value;
                selectionA = manager.CurrentSkinInfo.Value;
                manager.CurrentRevisionPrepareStarted = () => prepareCount++;

                Assert.Multiple(() =>
                {
                    Assert.That(selectionA.ID, Is.EqualTo(context.Candidate.ID));
                    Assert.That(revisionA.Owner, Is.SameAs(ownerA));
                });
            });
            AddStep("replace source with same-set B revision", () => writeRevisionPackage(context.PackageRoot, "B", new Rgba32(20, 210, 120, 255)));
            AddStep("invoke real reload button", () =>
            {
                caller.ReloadCurrentButton.TriggerClick();
                Assert.That(caller.ReloadCurrentButton.Enabled.Value, Is.False);
            });
            AddUntilStep("wait for coherent B publication", () =>
                !ReferenceEquals(manager.CurrentRevision, revisionA)
                && !ReferenceEquals(manager.CurrentSkin.Value, ownerA)
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("assert same-ID A to B pair", () =>
            {
                SkinCurrentRevision revisionB = manager.CurrentRevision;

                Assert.Multiple(() =>
                {
                    Assert.That(prepareCount, Is.EqualTo(1));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(revisionA.RecordId));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(revisionA.RecordId));
                    Assert.That(revisionB.RecordId, Is.EqualTo(revisionA.RecordId));
                    Assert.That(revisionB.Owner, Is.SameAs(manager.CurrentSkin.Value));
                    Assert.That(revisionB.ContentRevision, Is.Not.EqualTo(revisionA.ContentRevision));
                    Assert.That(revisionB.SourceKind, Is.EqualTo(external
                        ? SkinCurrentRevisionSourceKind.ExternalFolder
                        : SkinCurrentRevisionSourceKind.ManagedFolder));
                    Assert.That(revisionB.Owner.PackageContentRevision, Is.EqualTo(revisionB.ContentRevision));
                });
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestCurrentRevisionReloadPrepareFailureKeepsExactAPair(bool external)
        {
            var context = new CurrentRevisionProductContext();
            FullSkinSettingsCallerHost caller = null!;
            SkinCurrentRevision revisionA = null!;
            Skin ownerA = null!;
            Live<SkinInfo> selectionA = null!;
            int prepareCount = 0;
            int retiredA = 0;

            addSelectRevisionA(context, external);
            AddStep("mount prepare failure reload caller", () => Add(caller = new FullSkinSettingsCallerHost(manager)));
            AddUntilStep("wait for prepare failure reload affordance", () => caller.ReloadCurrentButton.Enabled.Value);
            AddStep("capture A and break source metadata", () =>
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
                File.Delete(Path.Combine(context.PackageRoot, "skin.ini"));
            });
            AddStep("invoke reload into prepare failure", () =>
            {
                caller.ReloadCurrentButton.TriggerClick();
                Assert.That(caller.ReloadCurrentButton.Enabled.Value, Is.False);
            });
            AddUntilStep("wait for prepare failure feedback boundary", () => prepareCount == 1 && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("assert exact A survived prepare failure", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(manager.CurrentRevision.Owner, Is.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision.ContentRevision, Is.EqualTo(revisionA.ContentRevision));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(retiredA, Is.Zero);
                });
            });
        }

        [Test]
        public void TestExternalReloadFinalRealmAuthorityDriftRetiresProvisionalKeepsAAndRetriesB()
        {
            var context = new CurrentRevisionProductContext();
            FullSkinSettingsCallerHost caller = null!;
            CurrentRevisionStarFountainHost visual = null!;
            SkinCurrentRevision revisionA = null!;
            SkinCurrentRevision? retiredProvisional = null;
            Skin ownerA = null!;
            Live<SkinInfo> selectionA = null!;
            ExternalRealmRecordSnapshot recordA = null!;
            FolderInventorySnapshot sourceB = default;
            string physicalDigestB = string.Empty;
            object textureA = null!;
            int finalBoundaryCalls = 0;
            int provisionalRetireCount = 0;
            int retiredACount = 0;

            addSelectRevisionA(context, external: true);
            AddStep("capture external A and mount production consumer plus caller", () =>
            {
                revisionA = manager.CurrentRevision;
                ownerA = manager.CurrentSkin.Value;
                selectionA = manager.CurrentSkinInfo.Value;
                recordA = captureExternalRealmRecordSnapshot(context.Candidate.ID);
                manager.CurrentRevisionRetired += revision =>
                {
                    if (ReferenceEquals(revision, revisionA))
                    {
                        Interlocked.Increment(ref retiredACount);
                        return;
                    }

                    if (revision.RecordId == context.Candidate.ID)
                    {
                        retiredProvisional = revision;
                        Interlocked.Increment(ref provisionalRetireCount);
                    }
                };
                Add(visual = new CurrentRevisionStarFountainHost(manager));
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for production consumer and reload affordance", () =>
                visual.Fountain.IsLoaded
                && visual.Texture != null
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("write external B and install final Realm authority drift", () =>
            {
                textureA = visual.Texture!;
                writeRevisionPackage(context.PackageRoot, "B", new Rgba32(20, 210, 120, 255));
                sourceB = captureFolderInventory(context.PackageRoot);
                physicalDigestB = captureExternalRootPhysicalDigest(context.PackageRoot);
                manager.CurrentRevisionBeforeCommitSchedule = () =>
                {
                    if (Interlocked.Increment(ref finalBoundaryCalls) != 1)
                        return;

                    Realm.Write(realm =>
                    {
                        SkinInfo current = realm.Find<SkinInfo>(context.Candidate.ID)!;
                        current.Creator = "test-only final authority drift";
                    });
                };
            });
            AddStep("invoke real external reload into final authority drift", () =>
                caller.ReloadCurrentButton.TriggerClick());
            AddUntilStep("wait for external final-drift failure and provisional retirement", () =>
                finalBoundaryCalls == 1
                && provisionalRetireCount == 1
                && retiredProvisional?.Retired.IsCompleted == true
                && caller.ReloadCurrentButton.Enabled.Value
                && caller.PostedNotifications.Count == 1);
            AddStep("assert external A and exact captured source survived final drift", () =>
            {
                ExternalRealmRecordSnapshot drifted = captureExternalRealmRecordSnapshot(context.Candidate.ID);

                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(manager.CurrentRevision.Owner, Is.SameAs(ownerA));
                    Assert.That(visual.Texture, Is.SameAs(textureA));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(retiredACount, Is.Zero);
                    Assert.That(provisionalRetireCount, Is.EqualTo(1));
                    Assert.That(retiredProvisional, Is.Not.Null);
                    Assert.That(retiredProvisional!.Retired.IsCompletedSuccessfully, Is.True);
                    Assert.That(drifted with { Creator = recordA.Creator }, Is.EqualTo(recordA));
                    Assert.That(captureFolderInventory(context.PackageRoot), Is.EqualTo(sourceB));
                    Assert.That(captureExternalRootPhysicalDigest(context.PackageRoot), Is.EqualTo(physicalDigestB));
                    Assert.That(
                        caller.PostedNotifications[0].Text.ToString(),
                        Is.EqualTo(SkinSettingsStrings.CurrentSkinReloadRejected.ToString()));
                });
            });
            AddStep("restore exact external registry record for retry", () =>
            {
                Realm.Write(realm =>
                {
                    SkinInfo current = realm.Find<SkinInfo>(context.Candidate.ID)!;
                    current.Creator = recordA.Creator;
                });
                manager.CurrentRevisionBeforeCommitSchedule = () => { };
                Assert.That(captureExternalRealmRecordSnapshot(context.Candidate.ID), Is.EqualTo(recordA));
            });
            AddStep("retry external B through real reload caller", () => caller.ReloadCurrentButton.TriggerClick());
            AddUntilStep("wait for coherent external B retry", () =>
                !ReferenceEquals(manager.CurrentRevision, revisionA)
                && !ReferenceEquals(manager.CurrentSkin.Value, ownerA)
                && revisionA.Retired.IsCompleted
                && retiredACount == 1
                && caller.ReloadCurrentButton.Enabled.Value
                && caller.PostedNotifications.Count == 2);
            AddStep("assert retry published exact same-ID external B", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentRevision.RecordId, Is.EqualTo(revisionA.RecordId));
                    Assert.That(manager.CurrentRevision.SourceKind, Is.EqualTo(SkinCurrentRevisionSourceKind.ExternalFolder));
                    Assert.That(manager.CurrentRevision.ContentRevision, Is.Not.EqualTo(revisionA.ContentRevision));
                    Assert.That(manager.CurrentRevision.ContentRevision, Is.EqualTo(manager.CurrentSkin.Value.PackageContentRevision));
                    Assert.That(manager.CurrentSkin.Value.Configuration.SkinInfo.Name, Is.EqualTo("current revision B"));
                    Assert.That(captureExternalRealmRecordSnapshot(context.Candidate.ID), Is.EqualTo(recordA));
                    Assert.That(captureFolderInventory(context.PackageRoot), Is.EqualTo(sourceB));
                    Assert.That(captureExternalRootPhysicalDigest(context.PackageRoot), Is.EqualTo(physicalDigestB));
                    Assert.That(provisionalRetireCount, Is.EqualTo(1));
                    Assert.That(retiredACount, Is.EqualTo(1));
                    Assert.That(
                        caller.PostedNotifications[1].Text.ToString(),
                        Is.EqualTo(SkinSettingsStrings.CurrentSkinReloaded.ToString()));
                });
            });
        }

        [Test]
        public void TestLiveRulesetProviderRejectsReloadBeforePrepareAndKeepsPair()
        {
            var context = new CurrentRevisionProductContext();
            FullSkinSettingsCallerHost caller = null!;
            JourneyRendererHost gameplay = null!;
            SkinCurrentRevision revisionA = null!;
            Skin ownerA = null!;
            Live<SkinInfo> selectionA = null!;
            int prepareCount = 0;

            addSelectRevisionA(context, external: false);
            AddStep("mount live BMS ruleset provider", () =>
            {
                revisionA = manager.CurrentRevision;
                ownerA = manager.CurrentSkin.Value;
                selectionA = manager.CurrentSkinInfo.Value;
                manager.CurrentRevisionPrepareStarted = () => prepareCount++;
                Add(gameplay = new JourneyRendererHost(manager, Clock.CurrentTime + 60_000, Clock.CurrentTime + 5_000));
            });
            AddUntilStep("wait for live host load", () => gameplay.IsLoaded);
            AddStep("attach live BMS provider", () => gameplay.ShowBms());
            AddUntilStep("wait for live BMS artifacts", () => gameplay.BmsArtifactsLoaded);
            AddStep("mount live blocker reload caller", () => Add(caller = new FullSkinSettingsCallerHost(manager)));
            AddUntilStep("wait for live blocker reload affordance", () => caller.ReloadCurrentButton.Enabled.Value);
            AddStep("write blocked B revision", () => writeRevisionPackage(context.PackageRoot, "B", new Rgba32(20, 210, 120, 255)));
            AddStep("invoke reload with live provider", () =>
            {
                caller.ReloadCurrentButton.TriggerClick();
                Assert.That(caller.ReloadCurrentButton.Enabled.Value, Is.False);
            });
            AddUntilStep("wait for live rejection boundary", () => caller.ReloadCurrentButton.Enabled.Value);
            AddStep("assert live rejection preserved A", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(prepareCount, Is.Zero);
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(caller.PostedNotifications, Has.Count.EqualTo(1));
                    Assert.That(
                        caller.PostedNotifications[0].Text.ToString(),
                        Is.EqualTo(SkinSettingsStrings.CurrentSkinReloadGameplayActive.ToString()));
                });

                gameplay.Expire();
            });
            AddUntilStep("wait for live host detach", () => gameplay.Parent == null);
        }

        [Test]
        public void TestLastProductionHolderDetachRetiresOldRevisionExactlyOnce()
        {
            var context = new CurrentRevisionProductContext();
            FullSkinSettingsCallerHost caller = null!;
            CurrentRevisionHolderHost holderHost = null!;
            SkinCurrentRevision revisionA = null!;
            int retiredA = 0;

            addSelectRevisionA(context, external: false);
            AddStep("mount production revision holder and reload caller", () =>
            {
                revisionA = manager.CurrentRevision;
                manager.CurrentRevisionRetired += revision =>
                {
                    if (ReferenceEquals(revision, revisionA))
                        retiredA++;
                };
                Add(holderHost = new CurrentRevisionHolderHost(manager, revisionA.Owner));
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for holder and reload caller", () => holderHost.Holder.IsLoaded && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("write B and invoke reload", () =>
            {
                writeRevisionPackage(context.PackageRoot, "B", new Rgba32(20, 210, 120, 255));
                caller.ReloadCurrentButton.TriggerClick();
            });
            AddUntilStep("wait for B with A holder", () =>
                !ReferenceEquals(manager.CurrentRevision, revisionA)
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("assert A waits for last holder", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(revisionA.ConsumersDetached.IsCompleted, Is.False);
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(retiredA, Is.Zero);
                });
                holderHost.Expire();
            });
            AddUntilStep("wait for final detach retirement", () =>
                holderHost.Parent == null
                && revisionA.Retired.IsCompleted
                && retiredA == 1);
            AddStep("assert exactly once retirement", () =>
            {
                holderHost.Dispose();
                Assert.That(retiredA, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestCurrentManagedDeleteWaitsForHolderDetachBeforeC1Mutation()
        {
            var context = new CurrentRevisionProductContext();
            FullSkinSettingsCallerHost caller = null!;
            CurrentRevisionHolderHost holderHost = null!;
            SkinCurrentRevision revisionA = null!;
            int retiredA = 0;

            addSelectRevisionA(context, external: false);
            AddStep("supply managed exact-set identity", () =>
                context.Candidate.PerformWrite(info => info.Hash = "current-managed-delete-held-revision"));
            AddStep("mount current delete caller and old owner holder", () =>
            {
                revisionA = manager.CurrentRevision;
                manager.CurrentRevisionRetired += revision =>
                {
                    if (ReferenceEquals(revision, revisionA))
                        retiredA++;
                };
                Add(holderHost = new CurrentRevisionHolderHost(manager, revisionA.Owner));
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for current delete affordance", () =>
                holderHost.Holder.IsLoaded && caller.CurrentDeleteButton.Enabled.Value);
            AddStep("open current managed delete dialog", () => caller.CurrentDeleteButton.TriggerClick());
            AddUntilStep("wait for current managed delete dialog", () =>
                caller.DialogOverlay.CurrentDialog is SkinSection.SkinDeleteDialog dialog
                && dialog.RecordId == context.Candidate.ID);
            AddStep("confirm current managed delete", () =>
                caller.DialogOverlay.CurrentDialog!.PerformAction<PopupDialogDangerousButton>());
            AddUntilStep("wait for protected fallback publication", () =>
                manager.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN
                && ReferenceEquals(manager.CurrentSkin.Value, manager.DefaultOmsSkin));
            AddStep("assert C1 mutation has not started before holder detach", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(Directory.Exists(context.PackageRoot), Is.True);
                    Assert.That(Realm.Run(realm => realm.Find<SkinInfo>(context.Candidate.ID) != null), Is.True);
                    Assert.That(new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(manager.IsManagedFolderDeleteRunning, Is.False);
                    Assert.That(revisionA.ConsumersDetached.IsCompleted, Is.False);
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(retiredA, Is.Zero);
                });
                holderHost.Expire();
            });
            AddUntilStep("wait for detach then C1 delete convergence", () =>
                holderHost.Parent == null
                && revisionA.Retired.IsCompleted
                && retiredA == 1
                && !manager.IsManagedFolderDeleteRunning
                && !Directory.Exists(context.PackageRoot)
                && Realm.Run(realm => realm.Find<SkinInfo>(context.Candidate.ID) == null));
            AddStep("assert detached current delete completed cleanly", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.LastManagedFolderDeleteResult.IsSuccess, Is.True);
                    Assert.That(retiredA, Is.EqualTo(1));
                    Assert.That(new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(manager.DefaultOmsSkin));
                });
            });
        }

        [Test]
        public void TestCurrentRevisionReloadLatestRequestWinsWithoutPublishingIntermediateSource()
        {
            var context = new CurrentRevisionProductContext();
            var firstPrepareEntered = new ManualResetEventSlim();
            var releaseFirstPrepare = new ManualResetEventSlim();
            Task<SkinCurrentRevisionReloadResult> firstReload = null!;
            Task<SkinCurrentRevisionReloadResult> secondReload = null!;
            SkinCurrentRevision revisionA = null!;
            Skin ownerA = null!;
            Live<SkinInfo> selectionA = null!;
            string revisionB = string.Empty;
            string revisionC = string.Empty;
            int prepareCount = 0;
            int retiredA = 0;

            addSelectRevisionA(context, external: false);
            AddStep("capture A and block first reload before capture", () =>
            {
                revisionA = manager.CurrentRevision;
                ownerA = manager.CurrentSkin.Value;
                selectionA = manager.CurrentSkinInfo.Value;
                manager.CurrentRevisionRetired += revision =>
                {
                    if (ReferenceEquals(revision, revisionA))
                        retiredA++;
                };
                manager.CurrentRevisionPrepareStarted = () =>
                {
                    if (Interlocked.Increment(ref prepareCount) != 1)
                        return;

                    firstPrepareEntered.Set();

                    if (!releaseFirstPrepare.Wait(TimeSpan.FromSeconds(30)))
                        throw new TimeoutException("The first reload preparation was not released.");
                };
            });
            AddStep("write B and start first reload", () =>
            {
                writeRevisionPackage(context.PackageRoot, "B", new Rgba32(220, 70, 30, 255));
                revisionB = captureManagedContentRevision(context.Candidate);
                firstReload = manager.ReloadCurrentRevisionAsync();
            });
            AddUntilStep("wait for first reload preparation", () => firstPrepareEntered.IsSet);
            AddStep("write C and start superseding reload", () =>
            {
                writeRevisionPackage(context.PackageRoot, "C", new Rgba32(40, 120, 245, 255));
                revisionC = captureManagedContentRevision(context.Candidate);
                secondReload = manager.ReloadCurrentRevisionAsync();
                releaseFirstPrepare.Set();
            });
            AddUntilStep("wait for latest reload convergence", () =>
                firstReload.IsCompleted
                && secondReload.IsCompleted
                && manager.CurrentRevision.ContentRevision == revisionC);
            AddStep("assert first superseded and only C published", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(firstReload.GetAwaiter().GetResult(), Is.EqualTo(SkinCurrentRevisionReloadResult.Superseded));
                    Assert.That(secondReload.GetAwaiter().GetResult(), Is.EqualTo(SkinCurrentRevisionReloadResult.Success));
                    Assert.That(prepareCount, Is.EqualTo(2));
                    Assert.That(revisionB, Is.Not.EqualTo(revisionC));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.Not.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision, Is.Not.SameAs(revisionA));
                    Assert.That(manager.CurrentRevision.ContentRevision, Is.EqualTo(revisionC));
                    Assert.That(manager.CurrentRevision.ContentRevision, Is.Not.EqualTo(revisionB));
                    Assert.That(manager.CurrentRevision.Owner, Is.SameAs(manager.CurrentSkin.Value));
                    Assert.That(retiredA, Is.EqualTo(1));
                });

                firstPrepareEntered.Dispose();
                releaseFirstPrepare.Dispose();
            });
        }

        [Test]
        public void TestSupersededUncooperativeReloadKeepsMutationAdmissionUntilItsWorkerExits()
        {
            var context = new CurrentRevisionProductContext();
            var firstPrepareEntered = new ManualResetEventSlim();
            var releaseFirstPrepare = new ManualResetEventSlim();
            Task<SkinCurrentRevisionReloadResult> firstReload = null!;
            Task<SkinCurrentRevisionReloadResult> secondReload = null!;
            Task<bool> blockedRegistration = null!;
            Task<bool> retriedRegistration = null!;
            string externalRoot = string.Empty;
            int prepareCount = 0;

            addSelectRevisionA(context, external: false);
            AddStep("block first reload without observing cancellation", () =>
            {
                manager.CurrentRevisionPrepareStarted = () =>
                {
                    if (Interlocked.Increment(ref prepareCount) != 1)
                        return;

                    firstPrepareEntered.Set();

                    if (!releaseFirstPrepare.Wait(TimeSpan.FromSeconds(30)))
                        throw new TimeoutException("The superseded worker was not released.");
                };
                externalRoot = createExternalPackage(createCompletePackage);
            });
            AddStep("start uncooperative B worker", () =>
            {
                writeRevisionPackage(context.PackageRoot, "B", new Rgba32(210, 70, 35, 255));
                firstReload = manager.ReloadCurrentRevisionAsync();
            });
            AddUntilStep("wait for uncooperative B prepare", () => firstPrepareEntered.IsSet);
            AddStep("publish latest C while B remains blocked", () =>
            {
                writeRevisionPackage(context.PackageRoot, "C", new Rgba32(35, 120, 225, 255));
                secondReload = manager.ReloadCurrentRevisionAsync();
            });
            AddUntilStep("wait for C without releasing B", () =>
                secondReload.IsCompleted
                && secondReload.GetAwaiter().GetResult() == SkinCurrentRevisionReloadResult.Success
                && firstReload.IsCompleted == false);
            AddStep("assert real workspace mutation remains admitted out", () =>
            {
                blockedRegistration = manager.RegisterExternalFolderAsync(externalRoot);
                Assert.Multiple(() =>
                {
                    Assert.That(blockedRegistration.IsCompleted, Is.True);
                    Assert.That(blockedRegistration.GetAwaiter().GetResult(), Is.False);
                    Assert.That(firstReload.IsCompleted, Is.False);
                });

                releaseFirstPrepare.Set();
            });
            AddUntilStep("wait for superseded worker exit", () => firstReload.IsCompleted);
            AddStep("retry mutation after every reload worker exits", () =>
                retriedRegistration = manager.RegisterExternalFolderAsync(externalRoot));
            AddUntilStep("wait for admitted mutation retry", () => retriedRegistration.IsCompleted);
            AddStep("assert admission count converged exactly", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(firstReload.GetAwaiter().GetResult(), Is.EqualTo(SkinCurrentRevisionReloadResult.Superseded));
                    Assert.That(retriedRegistration.GetAwaiter().GetResult(), Is.True);
                    Assert.That(prepareCount, Is.EqualTo(2));
                });

                firstPrepareEntered.Dispose();
                releaseFirstPrepare.Dispose();
            });
        }

        [Test]
        public void TestThrowingSupersededCancellationObserverCannotStrandLatestAdmission()
        {
            var context = new CurrentRevisionProductContext();
            var firstPrepareEntered = new ManualResetEventSlim();
            var cancellationObserved = new ManualResetEventSlim();
            var releaseFirstPrepare = new TaskCompletionSource<SkinRevisionParticipantCommit>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            SkinRevisionParticipantRegistration participant = null!;
            Task<SkinCurrentRevisionReloadResult> firstReload = null!;
            Task<SkinCurrentRevisionReloadResult> secondReload = null!;
            Task<bool> admittedMutation = null!;
            string externalRoot = string.Empty;
            int prepareCalls = 0;

            addSelectRevisionA(context, external: false);
            AddStep("register throwing cancellation participant", () =>
            {
                participant = manager.RegisterRevisionParticipant(
                    SkinRevisionParticipantKind.CoherentVisualConsumer,
                    "throwing cancellation product participant",
                    prepareCommit: (_, token) =>
                    {
                        if (Interlocked.Increment(ref prepareCalls) != 1)
                        {
                            return Task.FromResult(
                                new SkinRevisionParticipantCommit(() => { }, () => { }));
                        }

                        token.Register(() =>
                        {
                            cancellationObserved.Set();
                            throw new InvalidOperationException("deterministic cancellation observer fault");
                        });
                        firstPrepareEntered.Set();
                        return releaseFirstPrepare.Task;
                    });
                externalRoot = createExternalPackage(createCompletePackage);
            });
            AddStep("start cancellation-fault B reload", () =>
            {
                writeRevisionPackage(context.PackageRoot, "B", new Rgba32(200, 80, 35, 255));
                firstReload = manager.ReloadCurrentRevisionAsync();
            });
            AddUntilStep("wait for cancellation-fault prepare", () => firstPrepareEntered.IsSet);
            AddStep("start latest B despite throwing superseded cancellation", () =>
            {
                Assert.DoesNotThrow(() => secondReload = manager.ReloadCurrentRevisionAsync());
            });
            AddUntilStep("wait for cancellation fault and latest B", () =>
                cancellationObserved.IsSet
                && secondReload.IsCompleted
                && secondReload.GetAwaiter().GetResult() == SkinCurrentRevisionReloadResult.Success);
            AddStep("release superseded participant prepare", () =>
                releaseFirstPrepare.TrySetResult(new SkinRevisionParticipantCommit(() => { }, () => { })));
            AddUntilStep("wait for cancellation-fault worker join", () => firstReload.IsCompleted);
            AddStep("prove mutation admission returned to zero", () =>
            {
                participant.Dispose();
                admittedMutation = manager.RegisterExternalFolderAsync(externalRoot);
            });
            AddUntilStep("wait for post-fault admitted mutation", () => admittedMutation.IsCompleted);
            AddStep("assert cancellation observer could not leak admission", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(firstReload.GetAwaiter().GetResult(), Is.EqualTo(SkinCurrentRevisionReloadResult.Superseded));
                    Assert.That(admittedMutation.GetAwaiter().GetResult(), Is.True);
                    Assert.That(prepareCalls, Is.EqualTo(2));
                });

                firstPrepareEntered.Dispose();
                cancellationObserved.Dispose();
            });
        }

        [Test]
        public void TestShutdownJoinsSupersededUncooperativeReloadAfterLatestCompletes()
        {
            var context = new CurrentRevisionProductContext();
            var firstPrepareEntered = new ManualResetEventSlim();
            var releaseFirstPrepare = new TaskCompletionSource<SkinRevisionParticipantCommit>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            SkinRevisionParticipantRegistration participant = null!;
            Task<SkinCurrentRevisionReloadResult> firstReload = null!;
            Task<SkinCurrentRevisionReloadResult> secondReload = null!;
            Task shutdown = null!;
            SkinCurrentRevision revisionA = null!;
            SkinCurrentRevision? provisionalB = null;
            SkinCurrentRevision? latestRevision = null;
            SkinCurrentRevision? participantVisibleRevision = null;
            int participantPrepareCount = 0;
            int retiredA = 0;
            int retiredProvisionalB = 0;
            int retiredLatest = 0;

            addSelectRevisionA(context, external: false);
            AddStep("register uncooperative coherent participant", () =>
            {
                revisionA = manager.CurrentRevision;
                participantVisibleRevision = revisionA;
                manager.CurrentRevisionRetired += revision =>
                {
                    if (ReferenceEquals(revision, revisionA))
                        Interlocked.Increment(ref retiredA);
                    if (ReferenceEquals(revision, Volatile.Read(ref provisionalB)))
                        Interlocked.Increment(ref retiredProvisionalB);
                    if (ReferenceEquals(revision, Volatile.Read(ref latestRevision)))
                        Interlocked.Increment(ref retiredLatest);
                };
                participant = manager.RegisterRevisionParticipant(
                    SkinRevisionParticipantKind.CoherentVisualConsumer,
                    "uncooperative shutdown product participant",
                    prepareCommit: (nextRevision, _) =>
                    {
                        if (Interlocked.Increment(ref participantPrepareCount) == 1)
                        {
                            Volatile.Write(ref provisionalB, nextRevision);
                            firstPrepareEntered.Set();
                            return releaseFirstPrepare.Task;
                        }

                        Volatile.Write(ref latestRevision, nextRevision);
                        SkinCurrentRevision previousVisibleRevision = Volatile.Read(ref participantVisibleRevision)!;
                        return Task.FromResult(new SkinRevisionParticipantCommit(
                            () => Volatile.Write(ref participantVisibleRevision, nextRevision),
                            () => Volatile.Write(ref participantVisibleRevision, previousVisibleRevision)));
                    });
            });
            AddStep("start uncooperative provisional B worker", () =>
            {
                writeRevisionPackage(context.PackageRoot, "B", new Rgba32(190, 70, 45, 255));
                firstReload = manager.ReloadCurrentRevisionAsync();
            });
            AddUntilStep("wait for provisional B participant prepare", () =>
                firstPrepareEntered.IsSet && Volatile.Read(ref provisionalB) != null);
            AddStep("publish latest B while R1 ignores cancellation", () =>
                secondReload = manager.ReloadCurrentRevisionAsync());
            AddUntilStep("wait for exact latest revision before shutdown", () =>
                secondReload.IsCompleted
                && secondReload.GetAwaiter().GetResult() == SkinCurrentRevisionReloadResult.Success
                && firstReload.IsCompleted == false);
            AddStep("prove old worker still owns mutation admission", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(latestRevision, Is.Not.Null.And.Not.SameAs(revisionA));
                    Assert.That(latestRevision, Is.Not.SameAs(provisionalB));
                    Assert.That(manager.CurrentRevision, Is.SameAs(latestRevision));
                    Assert.That(latestRevision, Is.SameAs(Volatile.Read(ref participantVisibleRevision)));
                    Assert.That(latestRevision!.Owner, Is.SameAs(manager.CurrentSkin.Value));
                    Assert.That(manager.RenameManagedFolderAsync(context.Candidate.ID, "blocked-by-reload")
                        .GetAwaiter().GetResult().Status, Is.EqualTo(SkinManagedFolderRenameOperationStatus.Busy));
                    Assert.That(Directory.Exists(context.PackageRoot), Is.True);
                    Assert.That(Realm.Run(realm => realm.Find<SkinInfo>(context.Candidate.ID) != null), Is.True);
                    Assert.That(Volatile.Read(ref retiredA), Is.EqualTo(1));
                    Assert.That(Volatile.Read(ref retiredProvisionalB), Is.Zero);
                    Assert.That(Volatile.Read(ref retiredLatest), Is.Zero);
                });

                // Model the real drawable graph detaching before manager shutdown. The committed C owner must then
                // remain alive solely through the manager lease until the all-worker join has completed.
                participant.Dispose();
                Assert.That(participant.Detached.IsCompletedSuccessfully, Is.True);
                shutdown = Task.Run(manager.ShutdownManagedFolderMutations);
            });
            AddUntilStep("prove shutdown entered but still joins R1", () =>
                manager.RenameManagedFolderAsync(context.Candidate.ID, "shutdown-must-not-mutate")
                    .GetAwaiter().GetResult().Status == SkinManagedFolderRenameOperationStatus.Shutdown
                && shutdown.IsCompleted == false
                && firstReload.IsCompleted == false);
            AddStep("release uncooperative provisional B", () =>
                Assert.That(releaseFirstPrepare.TrySetResult(
                    new SkinRevisionParticipantCommit(() => { }, () => { })), Is.True));
            AddUntilStep("wait for all-worker join and exact owner reap", () =>
                shutdown.IsCompleted
                && firstReload.IsCompleted
                && Volatile.Read(ref retiredProvisionalB) == 1
                && Volatile.Read(ref retiredLatest) == 1);
            AddStep("assert shutdown joined and reaped every owner exactly once", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(shutdown.IsCompletedSuccessfully, Is.True);
                    Assert.That(firstReload.GetAwaiter().GetResult(), Is.EqualTo(SkinCurrentRevisionReloadResult.Superseded));
                    Assert.That(secondReload.GetAwaiter().GetResult(), Is.EqualTo(SkinCurrentRevisionReloadResult.Success));
                    Assert.That(Volatile.Read(ref provisionalB), Is.Not.Null);
                    Assert.That(Volatile.Read(ref provisionalB)!.Retired.IsCompletedSuccessfully, Is.True);
                    Assert.That(latestRevision!.Retired.IsCompletedSuccessfully, Is.True);
                    Assert.That(participantPrepareCount, Is.EqualTo(2));
                    Assert.That(Volatile.Read(ref retiredA), Is.EqualTo(1));
                    Assert.That(Volatile.Read(ref retiredProvisionalB), Is.EqualTo(1));
                    Assert.That(Volatile.Read(ref retiredLatest), Is.EqualTo(1));
                    Assert.That(Directory.Exists(context.PackageRoot), Is.True);
                    Assert.That(Realm.Run(realm => realm.Find<SkinInfo>(context.Candidate.ID) != null), Is.True);
                });

                participant.Dispose();
                manager.ShutdownManagedFolderMutations();
                Assert.That(releaseFirstPrepare.TrySetResult(
                    new SkinRevisionParticipantCommit(() => { }, () => { })), Is.False);
                Assert.Multiple(() =>
                {
                    Assert.That(Volatile.Read(ref retiredA), Is.EqualTo(1));
                    Assert.That(Volatile.Read(ref retiredProvisionalB), Is.EqualTo(1));
                    Assert.That(Volatile.Read(ref retiredLatest), Is.EqualTo(1));
                });

                firstPrepareEntered.Dispose();
            });
        }

        [Test]
        public void TestCurrentRevisionReloadSourceChangedObserverCanReenterWithoutSplitOrDoubleRetire()
        {
            var context = new CurrentRevisionProductContext();
            Task<SkinCurrentRevisionReloadResult> firstReload = null!;
            Task<SkinCurrentRevisionReloadResult>? reentrantReload = null;
            SkinCurrentRevision revisionA = null!;
            Skin ownerA = null!;
            Live<SkinInfo> selectionA = null!;
            string revisionB = string.Empty;
            int reentrantRequests = 0;
            int retiredA = 0;
            bool observerSawCoherentPair = false;

            addSelectRevisionA(context, external: false);
            AddStep("capture A and install reentrant production observer", () =>
            {
                revisionA = manager.CurrentRevision;
                ownerA = manager.CurrentSkin.Value;
                selectionA = manager.CurrentSkinInfo.Value;
                manager.CurrentRevisionRetired += revision =>
                {
                    if (ReferenceEquals(revision, revisionA))
                        retiredA++;
                };
                manager.SourceChanged += () =>
                {
                    if (Interlocked.Increment(ref reentrantRequests) != 1)
                        return;

                    observerSawCoherentPair = ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value)
                                              && manager.CurrentSkinInfo.Value.ID == manager.CurrentRevision.RecordId;
                    reentrantReload = manager.ReloadCurrentRevisionAsync();
                };
            });
            AddStep("write B and start reload", () =>
            {
                writeRevisionPackage(context.PackageRoot, "B", new Rgba32(70, 210, 110, 255));
                revisionB = captureManagedContentRevision(context.Candidate);
                firstReload = manager.ReloadCurrentRevisionAsync();
            });
            AddUntilStep("wait for reentrant reload completion", () =>
                firstReload.IsCompleted && reentrantReload?.IsCompleted == true);
            AddStep("assert reentrant no-change stayed coherent", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(firstReload.GetAwaiter().GetResult(), Is.EqualTo(SkinCurrentRevisionReloadResult.Success));
                    Assert.That(reentrantReload!.GetAwaiter().GetResult(), Is.EqualTo(SkinCurrentRevisionReloadResult.NoChange));
                    Assert.That(reentrantRequests, Is.EqualTo(1));
                    Assert.That(observerSawCoherentPair, Is.True);
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.Not.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision, Is.Not.SameAs(revisionA));
                    Assert.That(manager.CurrentRevision.ContentRevision, Is.EqualTo(revisionB));
                    Assert.That(manager.CurrentRevision.Owner, Is.SameAs(manager.CurrentSkin.Value));
                    Assert.That(retiredA, Is.EqualTo(1));
                });
            });
        }

        [Test]
        public void TestCurrentRevisionReloadCancellationBeforeCommitKeepsExactAAndRetiresProvisional()
        {
            var context = new CurrentRevisionProductContext();
            var beforeCommit = new ManualResetEventSlim();
            var releaseCommit = new ManualResetEventSlim();
            var cancellation = new CancellationTokenSource();
            Task<SkinCurrentRevisionReloadResult> reload = null!;
            SkinCurrentRevision revisionA = null!;
            Skin ownerA = null!;
            Live<SkinInfo> selectionA = null!;
            SkinCurrentRevision? retiredProvisional = null;
            string revisionB = string.Empty;
            int provisionalRetireCount = 0;

            addSelectRevisionA(context, external: false);
            AddStep("capture A and pause prepared B before commit schedule", () =>
            {
                revisionA = manager.CurrentRevision;
                ownerA = manager.CurrentSkin.Value;
                selectionA = manager.CurrentSkinInfo.Value;
                manager.CurrentRevisionRetired += revision =>
                {
                    if (revision.ContentRevision != revisionB || ReferenceEquals(revision, revisionA))
                        return;

                    retiredProvisional = revision;
                    provisionalRetireCount++;
                };
                manager.CurrentRevisionBeforeCommitSchedule = () =>
                {
                    beforeCommit.Set();

                    if (!releaseCommit.Wait(TimeSpan.FromSeconds(30)))
                        throw new TimeoutException("The paused current revision commit was not released.");
                };
            });
            AddStep("write B and start cancellable reload", () =>
            {
                writeRevisionPackage(context.PackageRoot, "B", new Rgba32(210, 80, 150, 255));
                revisionB = captureManagedContentRevision(context.Candidate);
                reload = manager.ReloadCurrentRevisionAsync(cancellation.Token);
            });
            AddUntilStep("wait for provisional B", () => beforeCommit.IsSet);
            AddStep("cancel before commit and release worker", () =>
            {
                cancellation.Cancel();
                releaseCommit.Set();
            });
            AddUntilStep("wait for cancelled provisional retirement", () =>
                reload.IsCompleted && provisionalRetireCount == 1);
            AddStep("assert exact A survived cancellation", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(reload.GetAwaiter().GetResult(), Is.EqualTo(SkinCurrentRevisionReloadResult.Cancelled));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(retiredProvisional, Is.Not.Null);
                    Assert.That(retiredProvisional!.Retired.IsCompleted, Is.True);
                    Assert.That(provisionalRetireCount, Is.EqualTo(1));
                });

                cancellation.Dispose();
                beforeCommit.Dispose();
                releaseCommit.Dispose();
            });
        }

        [Test]
        public void TestCurrentRevisionReloadCancellationAfterCommitCannotRollBackB()
        {
            var context = new CurrentRevisionProductContext();
            var cancellation = new CancellationTokenSource();
            Task<SkinCurrentRevisionReloadResult> reload = null!;
            SkinCurrentRevision revisionA = null!;
            Live<SkinInfo> selectionA = null!;
            string revisionB = string.Empty;
            int commitObserverCount = 0;
            int retiredA = 0;
            bool observerSawBPair = false;

            addSelectRevisionA(context, external: false);
            AddStep("capture A and cancel from committed source observer", () =>
            {
                revisionA = manager.CurrentRevision;
                selectionA = manager.CurrentSkinInfo.Value;
                manager.CurrentRevisionRetired += revision =>
                {
                    if (ReferenceEquals(revision, revisionA))
                        retiredA++;
                };
                manager.SourceChanged += () =>
                {
                    if (Interlocked.Increment(ref commitObserverCount) != 1)
                        return;

                    observerSawBPair = manager.CurrentRevision.ContentRevision == revisionB
                                       && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value)
                                       && manager.CurrentSkinInfo.Value.ID == manager.CurrentRevision.RecordId;
                    cancellation.Cancel();
                };
            });
            AddStep("write B and start reload with late cancellation", () =>
            {
                writeRevisionPackage(context.PackageRoot, "B", new Rgba32(180, 130, 40, 255));
                revisionB = captureManagedContentRevision(context.Candidate);
                reload = manager.ReloadCurrentRevisionAsync(cancellation.Token);
            });
            AddUntilStep("wait for committed B despite cancellation", () =>
                reload.IsCompleted
                && cancellation.IsCancellationRequested
                && manager.CurrentRevision.ContentRevision == revisionB);
            AddStep("assert committed B is indivisible", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(reload.GetAwaiter().GetResult(), Is.EqualTo(SkinCurrentRevisionReloadResult.Success));
                    Assert.That(commitObserverCount, Is.EqualTo(1));
                    Assert.That(observerSawBPair, Is.True);
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentRevision.Owner, Is.SameAs(manager.CurrentSkin.Value));
                    Assert.That(manager.CurrentRevision.ContentRevision, Is.EqualTo(revisionB));
                    Assert.That(retiredA, Is.EqualTo(1));
                });

                cancellation.Dispose();
            });
        }

        [Test]
        public void TestCurrentRevisionReloadSchedulerFaultKeepsExactAAndRetiresProvisional()
        {
            var context = new CurrentRevisionProductContext();
            Task<SkinCurrentRevisionReloadResult> reload = null!;
            SkinCurrentRevision revisionA = null!;
            Skin ownerA = null!;
            Live<SkinInfo> selectionA = null!;
            SkinCurrentRevision? retiredProvisional = null;
            string revisionB = string.Empty;
            int provisionalRetireCount = 0;

            addSelectRevisionA(context, external: false);
            AddStep("capture A and fault commit scheduler", () =>
            {
                revisionA = manager.CurrentRevision;
                ownerA = manager.CurrentSkin.Value;
                selectionA = manager.CurrentSkinInfo.Value;
                manager.CurrentRevisionRetired += revision =>
                {
                    if (revision.ContentRevision != revisionB || ReferenceEquals(revision, revisionA))
                        return;

                    retiredProvisional = revision;
                    provisionalRetireCount++;
                };
                manager.CurrentRevisionCompletionSchedule = _ => throw new InvalidOperationException("deterministic scheduler fault");
            });
            AddStep("write B and start scheduler-faulted reload", () =>
            {
                writeRevisionPackage(context.PackageRoot, "B", new Rgba32(150, 70, 225, 255));
                revisionB = captureManagedContentRevision(context.Candidate);
                reload = manager.ReloadCurrentRevisionAsync();
            });
            AddUntilStep("wait for scheduler failure and provisional retirement", () =>
                reload.IsCompleted && provisionalRetireCount == 1);
            AddStep("assert scheduler failure preserved exact A", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(reload.GetAwaiter().GetResult(), Is.EqualTo(SkinCurrentRevisionReloadResult.SchedulerFailed));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(retiredProvisional, Is.Not.Null);
                    Assert.That(retiredProvisional!.Retired.IsCompleted, Is.True);
                    Assert.That(provisionalRetireCount, Is.EqualTo(1));
                });

                manager.CurrentRevisionCompletionSchedule = callback => Scheduler.Add(callback);
            });
        }

        [Test]
        public void TestCurrentRevisionReloadShutdownClaimsPendingCommitAndJoinsWorker()
        {
            var context = new CurrentRevisionProductContext();
            Task<SkinCurrentRevisionReloadResult> reload = null!;
            Task shutdown = null!;
            Action? pendingCommit = null;
            SkinCurrentRevision revisionA = null!;
            Skin ownerA = null!;
            Live<SkinInfo> selectionA = null!;
            SkinCurrentRevision? retiredProvisional = null;
            string revisionB = string.Empty;
            int provisionalRetireCount = 0;

            addSelectRevisionA(context, external: false);
            AddStep("capture A and hold scheduled commit", () =>
            {
                revisionA = manager.CurrentRevision;
                ownerA = manager.CurrentSkin.Value;
                selectionA = manager.CurrentSkinInfo.Value;
                manager.CurrentRevisionRetired += revision =>
                {
                    if (revision.ContentRevision != revisionB || ReferenceEquals(revision, revisionA))
                        return;

                    retiredProvisional = revision;
                    provisionalRetireCount++;
                };
                manager.CurrentRevisionCompletionSchedule = callback => pendingCommit = callback;
            });
            AddStep("write B and start reload with held commit", () =>
            {
                writeRevisionPackage(context.PackageRoot, "B", new Rgba32(30, 175, 220, 255));
                revisionB = captureManagedContentRevision(context.Candidate);
                reload = manager.ReloadCurrentRevisionAsync();
            });
            AddUntilStep("wait for pending commit claim", () => pendingCommit != null);
            AddStep("shutdown and join publication worker", () =>
                shutdown = Task.Run(() => manager.ShutdownManagedFolderMutations()));
            AddUntilStep("wait for shutdown reap and join", () =>
                shutdown.IsCompleted
                && reload.IsCompleted
                && provisionalRetireCount == 1);
            AddStep("run stale scheduled callback after shutdown", () => pendingCommit!());
            AddStep("assert shutdown claimed callback exactly once", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(shutdown.IsCompletedSuccessfully, Is.True);
                    Assert.That(reload.GetAwaiter().GetResult(), Is.EqualTo(SkinCurrentRevisionReloadResult.Shutdown));
                    Assert.That(manager.ReloadCurrentRevisionAsync().GetAwaiter().GetResult(), Is.EqualTo(SkinCurrentRevisionReloadResult.Shutdown));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(retiredProvisional, Is.Not.Null);
                    Assert.That(retiredProvisional!.Retired.IsCompleted, Is.True);
                    Assert.That(provisionalRetireCount, Is.EqualTo(1));
                });
            });
        }

        [Test]
        public void TestCurrentRevisionReloadObserverFailureStillExposesExactCommittedPairToEveryGuardedCopy()
        {
            var context = new CurrentRevisionProductContext();
            FullSkinSettingsCallerHost caller = null!;
            Bindable<Skin> throwingOwnerCopy = null!;
            Bindable<Skin> readingOwnerCopy = null!;
            SkinCurrentRevision revisionA = null!;
            Skin ownerA = null!;
            Live<SkinInfo> selectionA = null!;
            Skin? ownerReadDuringFailure = null;
            string revisionB = string.Empty;
            int throwingObserverCalls = 0;
            int retiredA = 0;
            bool observerSawExactCommittedPair = false;

            addSelectRevisionA(context, external: false);
            AddStep("mount real reload caller and guarded owner copies", () =>
            {
                revisionA = manager.CurrentRevision;
                ownerA = manager.CurrentSkin.Value;
                selectionA = manager.CurrentSkinInfo.Value;
                throwingOwnerCopy = manager.CurrentSkin.GetBoundCopy();
                readingOwnerCopy = manager.CurrentSkin.GetBoundCopy();
                manager.CurrentRevisionRetired += revision =>
                {
                    if (ReferenceEquals(revision, revisionA))
                        retiredA++;
                };
                throwingOwnerCopy.ValueChanged += change =>
                {
                    if (ReferenceEquals(change.NewValue, ownerA))
                        return;

                    throwingObserverCalls++;
                    ownerReadDuringFailure = readingOwnerCopy.Value;
                    observerSawExactCommittedPair = ReferenceEquals(change.NewValue, ownerReadDuringFailure)
                                                    && ReferenceEquals(change.NewValue, manager.CurrentSkin.Value)
                                                    && ReferenceEquals(change.NewValue, manager.CurrentRevision.Owner)
                                                    && manager.CurrentSkinInfo.Value.ID == manager.CurrentRevision.RecordId;
                    throw new InvalidOperationException("deterministic guarded-copy observer failure");
                };
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for observer failure reload affordance", () =>
                caller.ReloadCurrentButton.IsLoaded && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("write B and invoke real reload caller", () =>
            {
                writeRevisionPackage(context.PackageRoot, "B", new Rgba32(35, 205, 130, 255));
                revisionB = captureManagedContentRevision(context.Candidate);
                caller.ReloadCurrentButton.TriggerClick();
            });
            AddUntilStep("wait for B after guarded-copy observer failure", () =>
                caller.ReloadCurrentButton.Enabled.Value
                && manager.CurrentRevision.ContentRevision == revisionB
                && retiredA == 1);
            AddStep("assert observer failure could not split authoritative or copied values", () =>
            {
                SkinCurrentRevision committed = manager.CurrentRevision;

                Assert.Multiple(() =>
                {
                    Assert.That(throwingObserverCalls, Is.EqualTo(1));
                    Assert.That(observerSawExactCommittedPair, Is.True);
                    Assert.That(ownerReadDuringFailure, Is.SameAs(committed.Owner));
                    Assert.That(readingOwnerCopy.Value, Is.SameAs(committed.Owner));
                    Assert.That(throwingOwnerCopy.Value, Is.SameAs(committed.Owner));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(committed.Owner));
                    Assert.That(committed.Owner, Is.Not.SameAs(ownerA));
                    Assert.That(committed.RecordId, Is.EqualTo(selectionA.ID));
                    Assert.That(committed.ContentRevision, Is.EqualTo(revisionB));
                    Assert.That(revisionA.Retired.IsCompleted, Is.True);
                    Assert.That(retiredA, Is.EqualTo(1));
                });
            });
        }

        [Test]
        public void TestCurrentSkinProjectionObserverCannotReentrantlySelectAnotherRecord()
        {
            var context = new CurrentRevisionProductContext();
            FullSkinSettingsCallerHost caller = null!;
            Live<SkinInfo> selectionA = null!;
            Skin ownerA = null!;
            SkinCurrentRevision revisionA = null!;
            Live<SkinInfo> selectionC = null!;
            string packageRootC = string.Empty;
            string revisionB = string.Empty;
            SkinSelectionRejectionReason reentrantRejection = SkinSelectionRejectionReason.None;
            int observerCalls = 0;
            int retiredA = 0;
            bool observerSawExactB = false;

            addSelectRevisionA(context, external: false);
            AddStep("create unselected C and mount real reload caller", () =>
            {
                (packageRootC, selectionC) = createCandidate(
                    root => writeRevisionPackage(root, "C", new Rgba32(215, 80, 190, 255)),
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                selectionA = manager.CurrentSkinInfo.Value;
                ownerA = manager.CurrentSkin.Value;
                revisionA = manager.CurrentRevision;
                manager.CurrentRevisionRetired += revision =>
                {
                    if (ReferenceEquals(revision, revisionA))
                        retiredA++;
                };
                manager.CurrentSkin.ValueChanged += change =>
                {
                    if (ReferenceEquals(change.NewValue, ownerA))
                        return;

                    observerCalls++;
                    observerSawExactB = ReferenceEquals(change.NewValue, manager.CurrentSkin.Value)
                                        && ReferenceEquals(change.NewValue, manager.CurrentRevision.Owner)
                                        && manager.CurrentSkinInfo.Value.ID == manager.CurrentRevision.RecordId
                                        && manager.CurrentRevision.ContentRevision == revisionB;
                    manager.CurrentSkinInfo.Value = selectionC;
                    reentrantRejection = manager.LastSelectionRejectionReason;
                };
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for reentrant projection reload affordance", () =>
                caller.ReloadCurrentButton.IsLoaded && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("write B and invoke real reload caller", () =>
            {
                writeRevisionPackage(context.PackageRoot, "B", new Rgba32(65, 185, 235, 255));
                revisionB = captureManagedContentRevision(context.Candidate);
                caller.ReloadCurrentButton.TriggerClick();
            });
            AddUntilStep("wait for exact B after rejected reentrant C", () =>
                caller.ReloadCurrentButton.Enabled.Value
                && manager.CurrentRevision.ContentRevision == revisionB
                && retiredA == 1);
            AddStep("assert projection reentry was rejected without publishing C", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(observerCalls, Is.EqualTo(1));
                    Assert.That(observerSawExactB, Is.True);
                    Assert.That(reentrantRejection, Is.EqualTo(SkinSelectionRejectionReason.ManagedFolderOperationInProgress));
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.ManagedFolderOperationInProgress));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(manager.CurrentRevision.Owner));
                    Assert.That(manager.CurrentSkin.Value, Is.Not.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision.RecordId, Is.EqualTo(selectionA.ID));
                    Assert.That(manager.CurrentRevision.ContentRevision, Is.EqualTo(revisionB));
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.Not.EqualTo(selectionC.ID));
                    Assert.That(Directory.Exists(packageRootC), Is.True);
                    Assert.That(Realm.Run(realm => realm.Find<SkinInfo>(selectionC.ID) != null), Is.True);
                    Assert.That(revisionA.Retired.IsCompleted, Is.True);
                    Assert.That(retiredA, Is.EqualTo(1));
                });
            });
        }

        [Test]
        public void TestCurrentPairRootAndBoundCopyBypassesAreFailClosed()
        {
            var context = new CurrentRevisionProductContext();
            Bindable<Skin> ownerCopy = null!;
            Bindable<Live<SkinInfo>> selectionCopy = null!;
            Live<SkinInfo> selectionA = null!;
            Skin ownerA = null!;
            SkinCurrentRevision revisionA = null!;
            Live<SkinInfo> forgedSameRecordSelection = null!;
            Live<SkinInfo> invalidDifferentRecordSelection = null!;
            FolderInventorySnapshot sourceA = default;
            CurrentRevisionRecordSnapshot recordA = default;
            Guid[] realmIdsA = Array.Empty<Guid>();
            int sourceChangesA = 0;
            int retired = 0;

            addSelectRevisionA(context, external: false);
            AddStep("capture exact A and guarded root copies", () =>
            {
                selectionA = manager.CurrentSkinInfo.Value;
                ownerA = manager.CurrentSkin.Value;
                revisionA = manager.CurrentRevision;
                ownerCopy = manager.CurrentSkin.GetBoundCopy();
                selectionCopy = manager.CurrentSkinInfo.GetBoundCopy();
                sourceA = captureFolderInventory(context.PackageRoot);
                recordA = CurrentRevisionRecordSnapshot.Capture(selectionA);
                realmIdsA = Realm.Run(realm => realm.All<SkinInfo>()
                    .ToArray()
                    .Select(info => info.ID)
                    .OrderBy(id => id)
                    .ToArray());
                sourceChangesA = sourceChangedCount;
                manager.CurrentRevisionRetired += _ => retired++;
                forgedSameRecordSelection = new SkinInfo("forged same-record projection", "C2 red test", typeof(BmsLegacySkin).GetInvariantInstantiationInfo())
                {
                    ID = selectionA.ID,
                    FilesystemStoragePath = "chartskin/../forged-projection-bypass",
                    FilesystemStorageAuthorityOwner = SkinManagedFolderScanner.AUTHORITY_OWNER,
                }.ToLiveUnmanaged();
                invalidDifferentRecordSelection = new SkinInfo("invalid different-record projection", "C2 red test", typeof(BmsLegacySkin).GetInvariantInstantiationInfo())
                {
                    FilesystemStoragePath = "chartskin/../invalid-projection-bypass",
                    FilesystemStorageAuthorityOwner = SkinManagedFolderScanner.AUTHORITY_OWNER,
                }.ToLiveUnmanaged();
            });
            AddStep("reject trigger disable and owner assignment bypasses", () =>
            {
                InvalidOperationException rootOwnerTrigger = Assert.Throws<InvalidOperationException>(() =>
                    manager.CurrentSkin.TriggerChange())!;
                InvalidOperationException copyOwnerTrigger = Assert.Throws<InvalidOperationException>(() =>
                    ownerCopy.TriggerChange())!;
                InvalidOperationException rootSelectionTrigger = Assert.Throws<InvalidOperationException>(() =>
                    manager.CurrentSkinInfo.TriggerChange())!;
                InvalidOperationException copySelectionTrigger = Assert.Throws<InvalidOperationException>(() =>
                    selectionCopy.TriggerChange())!;
                InvalidOperationException rootOwnerDisable = Assert.Throws<InvalidOperationException>(() =>
                    manager.CurrentSkin.Disabled = true)!;
                InvalidOperationException copyOwnerDisable = Assert.Throws<InvalidOperationException>(() =>
                    ownerCopy.Disabled = true)!;
                InvalidOperationException rootSelectionDisable = Assert.Throws<InvalidOperationException>(() =>
                    manager.CurrentSkinInfo.Disabled = true)!;
                InvalidOperationException copySelectionDisable = Assert.Throws<InvalidOperationException>(() =>
                    selectionCopy.Disabled = true)!;
                InvalidOperationException rootOwnerAssignment = Assert.Throws<InvalidOperationException>(() =>
                    manager.CurrentSkin.Value = ownerA)!;
                InvalidOperationException copyOwnerAssignment = Assert.Throws<InvalidOperationException>(() =>
                    ownerCopy.Value = ownerA)!;

                Assert.Multiple(() =>
                {
                    Assert.That(rootOwnerTrigger.Message, Is.EqualTo(SkinInstanceBindable.DIRECT_ASSIGNMENT_DISABLED_DIAGNOSTIC));
                    Assert.That(copyOwnerTrigger.Message, Is.EqualTo(SkinInstanceBindable.DIRECT_ASSIGNMENT_DISABLED_DIAGNOSTIC));
                    Assert.That(rootSelectionTrigger.Message, Is.EqualTo(SkinSelectionBindable.UNPREPARED_CHANGE_DISABLED_DIAGNOSTIC));
                    Assert.That(copySelectionTrigger.Message, Is.EqualTo(SkinSelectionBindable.UNPREPARED_CHANGE_DISABLED_DIAGNOSTIC));
                    Assert.That(rootOwnerDisable.Message, Is.EqualTo(SkinInstanceBindable.DISABLE_DISABLED_DIAGNOSTIC));
                    Assert.That(copyOwnerDisable.Message, Is.EqualTo(SkinInstanceBindable.DISABLE_DISABLED_DIAGNOSTIC));
                    Assert.That(rootSelectionDisable.Message, Is.EqualTo(SkinSelectionBindable.DISABLE_DISABLED_DIAGNOSTIC));
                    Assert.That(copySelectionDisable.Message, Is.EqualTo(SkinSelectionBindable.DISABLE_DISABLED_DIAGNOSTIC));
                    Assert.That(rootOwnerAssignment.Message, Is.EqualTo(SkinInstanceBindable.DIRECT_ASSIGNMENT_DISABLED_DIAGNOSTIC));
                    Assert.That(copyOwnerAssignment.Message, Is.EqualTo(SkinInstanceBindable.DIRECT_ASSIGNMENT_DISABLED_DIAGNOSTIC));
                });
            });
            AddStep("reject root and bound-copy unprepared selection assignments", () =>
            {
                manager.CurrentSkinInfo.Value = forgedSameRecordSelection;
                selectionCopy.Value = forgedSameRecordSelection;

                Assert.Multiple(() =>
                {
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.None));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(selectionCopy.Value, Is.SameAs(selectionA));
                });

                manager.CurrentSkinInfo.Value = invalidDifferentRecordSelection;
                Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.FilesystemDeclarationRejected));

                selectionCopy.Value = invalidDifferentRecordSelection;
                Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.FilesystemDeclarationRejected));
            });
            AddStep("assert every bypass left pair retirement and stores exact", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkin.Disabled, Is.False);
                    Assert.That(ownerCopy.Disabled, Is.False);
                    Assert.That(manager.CurrentSkinInfo.Disabled, Is.False);
                    Assert.That(selectionCopy.Disabled, Is.False);
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(selectionCopy.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(ownerCopy.Value, Is.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(manager.CurrentRevision.Owner, Is.SameAs(ownerA));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(retired, Is.Zero);
                    Assert.That(sourceChangedCount, Is.EqualTo(sourceChangesA));
                    Assert.That(captureFolderInventory(context.PackageRoot), Is.EqualTo(sourceA));
                    Assert.That(CurrentRevisionRecordSnapshot.Capture(selectionA), Is.EqualTo(recordA));
                    Assert.That(Realm.Run(realm => realm.All<SkinInfo>()
                        .ToArray()
                        .Select(info => info.ID)
                        .OrderBy(id => id)
                        .ToArray()), Is.EqualTo(realmIdsA));
                });
            });
        }

        [Test]
        public void TestCurrentPairAuthorityBindingsCannotBeReplacedOrDetached()
        {
            var context = new CurrentRevisionProductContext();
            Bindable<Skin> ownerCopy = null!;
            Bindable<Live<SkinInfo>> selectionCopy = null!;
            SkinInstanceBindable unrelatedOwner = null!;
            SkinSelectionBindable unrelatedSelection = null!;
            Live<SkinInfo> selectionA = null!;
            Skin ownerA = null!;
            SkinCurrentRevision revisionA = null!;
            Live<SkinInfo> unresolvableSelection = null!;
            FolderInventorySnapshot sourceA = default;
            CurrentRevisionRecordSnapshot recordA = default;
            Guid[] realmIdsA = Array.Empty<Guid>();
            int sourceChangesA = 0;
            int retired = 0;

            addSelectRevisionA(context, external: false);
            AddStep("capture authoritative pair and independent guarded bindables", () =>
            {
                selectionA = manager.CurrentSkinInfo.Value;
                ownerA = manager.CurrentSkin.Value;
                revisionA = manager.CurrentRevision;
                ownerCopy = manager.CurrentSkin.GetBoundCopy();
                selectionCopy = manager.CurrentSkinInfo.GetBoundCopy();
                unrelatedOwner = new SkinInstanceBindable();
                unrelatedSelection = new SkinSelectionBindable(
                    new SkinInfo("unrelated selection", "C2 red test").ToLiveUnmanaged());
                unresolvableSelection = new SkinInfo(
                    "unresolvable guarded-copy request",
                    "C2 red test",
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo())
                {
                    FilesystemStoragePath = "chartskin/../unresolvable-authority-request",
                    FilesystemStorageAuthorityOwner = SkinManagedFolderScanner.AUTHORITY_OWNER,
                }.ToLiveUnmanaged();
                sourceA = captureFolderInventory(context.PackageRoot);
                recordA = CurrentRevisionRecordSnapshot.Capture(selectionA);
                realmIdsA = Realm.Run(realm => realm.All<SkinInfo>()
                    .ToArray()
                    .Select(info => info.ID)
                    .OrderBy(id => id)
                    .ToArray());
                sourceChangesA = sourceChangedCount;
                manager.CurrentRevisionRetired += _ => retired++;
            });
            AddStep("reject owner authority binding replacement and detach", () =>
            {
                InvalidOperationException rootCopyToRoot = Assert.Throws<InvalidOperationException>(() =>
                    manager.CurrentSkin.CopyTo(manager.CurrentSkin))!;
                InvalidOperationException copyCopyToRoot = Assert.Throws<InvalidOperationException>(() =>
                    ownerCopy.CopyTo(manager.CurrentSkin))!;
                InvalidOperationException rootBindToUnrelated = Assert.Throws<InvalidOperationException>(() =>
                    manager.CurrentSkin.BindTo(unrelatedOwner))!;
                InvalidOperationException rootUnbindFromUnrelated = Assert.Throws<InvalidOperationException>(() =>
                    manager.CurrentSkin.UnbindFrom(unrelatedOwner))!;
                InvalidOperationException copyUnbindFromRoot = Assert.Throws<InvalidOperationException>(() =>
                    ownerCopy.UnbindFrom(manager.CurrentSkin))!;

                Assert.Multiple(() =>
                {
                    Assert.That(rootCopyToRoot.Message, Is.EqualTo(SkinInstanceBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(copyCopyToRoot.Message, Is.EqualTo(SkinInstanceBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(rootBindToUnrelated.Message, Is.EqualTo(SkinInstanceBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(rootUnbindFromUnrelated.Message, Is.EqualTo(SkinInstanceBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(copyUnbindFromRoot.Message, Is.EqualTo(SkinInstanceBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                });
            });
            AddStep("reject selection authority binding replacement and detach", () =>
            {
                InvalidOperationException rootCopyToRoot = Assert.Throws<InvalidOperationException>(() =>
                    manager.CurrentSkinInfo.CopyTo(manager.CurrentSkinInfo))!;
                InvalidOperationException copyCopyToRoot = Assert.Throws<InvalidOperationException>(() =>
                    selectionCopy.CopyTo(manager.CurrentSkinInfo))!;
                InvalidOperationException rootBindToUnrelated = Assert.Throws<InvalidOperationException>(() =>
                    manager.CurrentSkinInfo.BindTo(unrelatedSelection))!;
                InvalidOperationException rootUnbindFromUnrelated = Assert.Throws<InvalidOperationException>(() =>
                    manager.CurrentSkinInfo.UnbindFrom(unrelatedSelection))!;
                InvalidOperationException copyUnbindFromRoot = Assert.Throws<InvalidOperationException>(() =>
                    selectionCopy.UnbindFrom(manager.CurrentSkinInfo))!;

                Assert.Multiple(() =>
                {
                    Assert.That(rootCopyToRoot.Message, Is.EqualTo(SkinSelectionBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(copyCopyToRoot.Message, Is.EqualTo(SkinSelectionBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(rootBindToUnrelated.Message, Is.EqualTo(SkinSelectionBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(rootUnbindFromUnrelated.Message, Is.EqualTo(SkinSelectionBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(copyUnbindFromRoot.Message, Is.EqualTo(SkinSelectionBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                });
            });
            AddStep("bound selection still routes through manager rejection gate", () =>
            {
                selectionCopy.Value = unresolvableSelection;

                Assert.Multiple(() =>
                {
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.FilesystemDeclarationRejected));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(selectionCopy.Value, Is.SameAs(selectionA));
                    Assert.That(Realm.Run(realm => realm.Find<SkinInfo>(unresolvableSelection.ID) == null), Is.True);
                });
            });
            AddStep("assert authority providers pair and stores stayed exact", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(selectionCopy.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(ownerCopy.Value, Is.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(manager.CurrentRevision.Owner, Is.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision.RecordId, Is.EqualTo(selectionA.ID));
                    Assert.That(manager.CurrentRevision.ContentRevision, Is.EqualTo(revisionA.ContentRevision));
                    Assert.That(manager.CurrentRevision.SourceKind, Is.EqualTo(SkinCurrentRevisionSourceKind.ManagedFolder));
                    Assert.That(manager.CanReloadCurrentRevision, Is.True);
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(retired, Is.Zero);
                    Assert.That(sourceChangedCount, Is.EqualTo(sourceChangesA));
                    Assert.That(captureFolderInventory(context.PackageRoot), Is.EqualTo(sourceA));
                    Assert.That(CurrentRevisionRecordSnapshot.Capture(selectionA), Is.EqualTo(recordA));
                    Assert.That(Realm.Run(realm => realm.All<SkinInfo>()
                        .ToArray()
                        .Select(info => info.ID)
                        .OrderBy(id => id)
                        .ToArray()), Is.EqualTo(realmIdsA));
                });
            });
        }

        private void addSelectRevisionA(CurrentRevisionProductContext context, bool external)
        {
            if (external)
            {
                AddStep("create and register external revision A", () =>
                {
                    context.PackageRoot = createExternalPackage(root =>
                        writeRevisionPackage(root, "A", new Rgba32(240, 40, 80, 255)));
                    context.RegistrationTask = manager.RegisterExternalFolderAsync(context.PackageRoot);
                });
                AddUntilStep("wait for external A registration", () => context.RegistrationTask?.IsCompleted == true);
                AddStep("query registered external A", () =>
                {
                    Assert.That(context.RegistrationTask!.GetAwaiter().GetResult(), Is.True);
                    context.DropdownTask = manager.GetAllUsableSkinsAsync();
                });
                AddUntilStep("wait for external A dropdown", () => context.DropdownTask?.IsCompleted == true);
                AddStep("select external A", () =>
                {
                    context.Candidate = context.DropdownTask!.GetAwaiter().GetResult()
                                                       .Single(record => record.PerformRead(info =>
                                                           info.IsExternalFilesystemStorage
                                                           && string.Equals(info.FilesystemStoragePath, context.PackageRoot, StringComparison.OrdinalIgnoreCase)));
                    manager.CurrentSkinInfo.Value = context.Candidate;
                });
            }
            else
            {
                AddStep("create and select managed revision A", () =>
                {
                    (context.PackageRoot, context.Candidate) = createCandidate(
                        root => writeRevisionPackage(root, "A", new Rgba32(240, 40, 80, 255)),
                        typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                    manager.CurrentSkinInfo.Value = context.Candidate;
                });
            }

            AddUntilStep("wait for exact A pair", () =>
                context.Candidate != null
                && manager.CurrentSkinInfo.Value.ID == context.Candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == context.Candidate.ID
                && manager.CurrentSkin.Value is BmsLegacySkin
                && ReferenceEquals(manager.CurrentRevision.Owner, manager.CurrentSkin.Value));
        }

        private static void writeRevisionPackage(string packageRoot, string revision, Rgba32 noteColour)
        {
            createCompletePackage(packageRoot);
            string skinIniPath = Path.Combine(packageRoot, "skin.ini");
            string skinIni = File.ReadAllText(skinIniPath).Replace(
                "Name: managed folder product test",
                $"Name: current revision {revision}",
                StringComparison.Ordinal);
            File.WriteAllText(skinIniPath, skinIni);
            File.WriteAllBytes(Path.Combine(packageRoot, "notes", "note.png"), createPng(noteColour));
        }

        private string captureManagedContentRevision(Live<SkinInfo> candidate)
        {
            SkinFilesystemStorageResolution resolution = candidate.PerformRead(
                info => SkinFilesystemStorageResolver.ResolveExisting(info, LocalStorage));
            SkinManagedPackageCaptureResult capture = SkinManagedPackageCapture.Capture(resolution.ManagedCaptureRequest!);
            Assert.That(capture.IsSuccess, Is.True);

            using SkinPackageRevisionCapsule capsule = capture.Capsule!;
            return capsule.ContentRevision;
        }

        private sealed class CurrentRevisionProductContext
        {
            public string PackageRoot { get; set; } = string.Empty;

            public Live<SkinInfo> Candidate { get; set; } = null!;

            public Task<bool>? RegistrationTask { get; set; }

            public Task<IList<Live<SkinInfo>>>? DropdownTask { get; set; }
        }

        private readonly record struct CurrentRevisionRecordSnapshot(
            Guid Id,
            string Name,
            string Creator,
            string InstantiationInfo,
            string Hash,
            string? FilesystemStoragePath,
            bool IsExternalFilesystemStorage,
            string? FilesystemStorageAuthorityOwner,
            bool DeletePending,
            int FileCount)
        {
            public static CurrentRevisionRecordSnapshot Capture(Live<SkinInfo> selection)
                => selection.PerformRead(info => new CurrentRevisionRecordSnapshot(
                    info.ID,
                    info.Name,
                    info.Creator,
                    info.InstantiationInfo,
                    info.Hash,
                    info.FilesystemStoragePath,
                    info.IsExternalFilesystemStorage,
                    info.FilesystemStorageAuthorityOwner,
                    info.DeletePending,
                    info.Files.Count));
        }

        private partial class CurrentRevisionHolderHost : CompositeDrawable
        {
            [Cached]
            private readonly SkinManager skinManager;

            public SkinBackground Holder { get; }

            public CurrentRevisionHolderHost(SkinManager skinManager, Skin owner)
            {
                this.skinManager = skinManager;
                RelativeSizeAxes = Axes.Both;
                InternalChild = Holder = new SkinBackground(owner, string.Empty);
            }
        }

        private partial class FullSkinSettingsCallerHost
        {
            public SkinSection.ReloadCurrentSkinButton ReloadCurrentButton
                => Section.ChildrenOfType<SkinSection.ReloadCurrentSkinButton>().Single();
        }
    }
}
