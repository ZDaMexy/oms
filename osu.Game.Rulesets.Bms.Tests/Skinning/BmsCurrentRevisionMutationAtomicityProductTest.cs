// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Models;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning;
using SharpCompress.Archives.Zip;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [Test]
        public void TestGuardedCurrentPairBindablePublicSurfaceCannotSplitProjectionOrConfig()
        {
            var context = new CurrentRevisionProductContext();
            Bindable<Skin> ownerCopy = null!;
            Bindable<Skin> otherOwnerCopy = null!;
            Bindable<Live<SkinInfo>> selectionCopy = null!;
            Bindable<Live<SkinInfo>> otherSelectionCopy = null!;
            Bindable<string> configSkin = null!;
            Action<ValueChangedEvent<Live<SkinInfo>>> persistSelection = null!;
            Live<SkinInfo> selectionA = null!;
            Skin ownerA = null!;
            SkinCurrentRevision revisionA = null!;
            Live<SkinInfo> rejected = null!;
            int projectionEvents = 0;

            addSelectRevisionA(context, external: false);
            AddStep("capture guarded public bindables and production config projection", () =>
            {
                selectionA = manager.CurrentSkinInfo.Value;
                ownerA = manager.CurrentSkin.Value;
                revisionA = manager.CurrentRevision;
                ownerCopy = manager.CurrentSkin.GetBoundCopy();
                otherOwnerCopy = manager.CurrentSkin.GetBoundCopy();
                selectionCopy = manager.CurrentSkinInfo.GetBoundCopy();
                otherSelectionCopy = manager.CurrentSkinInfo.GetBoundCopy();
                configSkin = backgroundConfig.GetBindable<string>(OsuSetting.Skin);
                configSkin.Value = selectionA.ID.ToString();
                persistSelection = change =>
                {
                    projectionEvents++;
                    configSkin.Value = change.NewValue.ID.ToString();
                };
                manager.CurrentSkinInfo.ValueChanged += persistSelection;
                rejected = new SkinInfo(
                    "unresolvable guarded-copy request",
                    "C2 authority regression",
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo())
                {
                    FilesystemStoragePath = "chartskin/../unresolvable-authority-request",
                    FilesystemStorageAuthorityOwner = SkinManagedFolderScanner.AUTHORITY_OWNER,
                }.ToLiveUnmanaged();
            });
            AddStep("reject self cross-copy and authoritative root detach surfaces", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(() => selectionCopy.CopyTo(selectionCopy),
                        Throws.TypeOf<InvalidOperationException>()
                              .With.Message.EqualTo(SkinSelectionBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(() => selectionCopy.BindTo(selectionCopy),
                        Throws.TypeOf<InvalidOperationException>()
                              .With.Message.EqualTo(SkinSelectionBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(() => selectionCopy.CopyTo(otherSelectionCopy),
                        Throws.TypeOf<InvalidOperationException>()
                              .With.Message.EqualTo(SkinSelectionBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(() => otherSelectionCopy.BindTo(selectionCopy),
                        Throws.TypeOf<InvalidOperationException>()
                              .With.Message.EqualTo(SkinSelectionBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(() => ownerCopy.CopyTo(ownerCopy),
                        Throws.TypeOf<InvalidOperationException>()
                              .With.Message.EqualTo(SkinInstanceBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(() => ownerCopy.BindTo(ownerCopy),
                        Throws.TypeOf<InvalidOperationException>()
                              .With.Message.EqualTo(SkinInstanceBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(() => ownerCopy.CopyTo(otherOwnerCopy),
                        Throws.TypeOf<InvalidOperationException>()
                              .With.Message.EqualTo(SkinInstanceBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(() => otherOwnerCopy.BindTo(ownerCopy),
                        Throws.TypeOf<InvalidOperationException>()
                              .With.Message.EqualTo(SkinInstanceBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(() => manager.CurrentSkinInfo.UnbindEvents(),
                        Throws.TypeOf<InvalidOperationException>()
                              .With.Message.EqualTo(SkinSelectionBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(() => manager.CurrentSkinInfo.UnbindBindings(),
                        Throws.TypeOf<InvalidOperationException>()
                              .With.Message.EqualTo(SkinSelectionBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(() => manager.CurrentSkinInfo.UnbindAll(),
                        Throws.TypeOf<InvalidOperationException>()
                              .With.Message.EqualTo(SkinSelectionBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(() => manager.CurrentSkin.UnbindEvents(),
                        Throws.TypeOf<InvalidOperationException>()
                              .With.Message.EqualTo(SkinInstanceBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(() => manager.CurrentSkin.UnbindBindings(),
                        Throws.TypeOf<InvalidOperationException>()
                              .With.Message.EqualTo(SkinInstanceBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(() => manager.CurrentSkin.UnbindAll(),
                        Throws.TypeOf<InvalidOperationException>()
                              .With.Message.EqualTo(SkinInstanceBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(() => selectionCopy.UnbindFrom(manager.CurrentSkinInfo),
                        Throws.TypeOf<InvalidOperationException>()
                              .With.Message.EqualTo(SkinSelectionBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(() => ownerCopy.UnbindFrom(manager.CurrentSkin),
                        Throws.TypeOf<InvalidOperationException>()
                              .With.Message.EqualTo(SkinInstanceBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                });
            });
            AddStep("normally detach guarded copies without downgrading them", () =>
            {
                selectionCopy.UnbindAll();
                ownerCopy.UnbindAll();

                Assert.Multiple(() =>
                {
                    Assert.That(selectionCopy.Value, Is.SameAs(selectionA));
                    Assert.That(ownerCopy.Value, Is.SameAs(ownerA));
                    Assert.That(() => selectionCopy.BindTo(manager.CurrentSkinInfo),
                        Throws.TypeOf<InvalidOperationException>()
                              .With.Message.EqualTo(SkinSelectionBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(() => ownerCopy.BindTo(manager.CurrentSkin),
                        Throws.TypeOf<InvalidOperationException>()
                              .With.Message.EqualTo(SkinInstanceBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(() => selectionCopy.UnbindFrom(otherSelectionCopy),
                        Throws.TypeOf<InvalidOperationException>()
                              .With.Message.EqualTo(SkinSelectionBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                    Assert.That(() => ownerCopy.UnbindFrom(otherOwnerCopy),
                        Throws.TypeOf<InvalidOperationException>()
                              .With.Message.EqualTo(SkinInstanceBindable.AUTHORITY_BINDING_DISABLED_DIAGNOSTIC));
                });

                selectionCopy.Value = rejected;
            });
            AddStep("assert fake request produced no event config or pair split", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(projectionEvents, Is.Zero);
                    Assert.That(configSkin.Value, Is.EqualTo(selectionA.ID.ToString()));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(selectionCopy.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(ownerCopy.Value, Is.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(manager.CurrentRevision.Owner, Is.SameAs(ownerA));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                });
            });
            AddStep("publish legitimate fallback after failed root unbind attempts", () =>
                manager.CurrentSkinInfo.Value = manager.DefaultOmsSkin.SkinInfo);
            AddStep("assert exact fallback pair committed", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.LastSelectionRejectionReason, Is.EqualTo(SkinSelectionRejectionReason.None));
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(manager.CurrentRevision.Owner, Is.SameAs(manager.CurrentSkin.Value));
                    Assert.That(selectionCopy.Value, Is.SameAs(manager.CurrentSkinInfo.Value));
                    Assert.That(ownerCopy.Value, Is.SameAs(manager.CurrentSkin.Value));
                });
            });
            AddStep("assert root event and config survived failed unbind attempts", () =>
            {
                try
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(projectionEvents, Is.EqualTo(1));
                        Assert.That(configSkin.Value, Is.EqualTo(SkinInfo.OMS_SKIN.ToString()));
                    });
                }
                finally
                {
                    manager.CurrentSkinInfo.ValueChanged -= persistSelection;
                }
            });
        }

        [Test]
        public void TestRealSkinSectionDisposeDetachesGuardedCopyWithoutChangingCurrentPair()
        {
            var context = new CurrentRevisionProductContext();
            FullSkinSettingsCallerHost callerHost = null!;
            Live<SkinInfo> selectionA = null!;
            Skin ownerA = null!;
            SkinCurrentRevision revisionA = null!;
            int sourceChangesA = 0;

            addSelectRevisionA(context, external: false);
            AddStep("mount real skin settings section", () =>
            {
                selectionA = manager.CurrentSkinInfo.Value;
                ownerA = manager.CurrentSkin.Value;
                revisionA = manager.CurrentRevision;
                sourceChangesA = sourceChangedCount;
                Add(callerHost = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for real skin settings bindings", () =>
                callerHost.ReloadCurrentButton.IsLoaded && callerHost.ReloadCurrentButton.Enabled.Value);
            AddStep("dispose real skin settings host immediately", () =>
                Assert.That(Remove(callerHost, disposeImmediately: true), Is.True));
            AddStep("assert settings disposal preserved exact current pair", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(callerHost.IsHostDisposed, Is.True);
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(manager.CurrentRevision.Owner, Is.SameAs(ownerA));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(sourceChangedCount, Is.EqualTo(sourceChangesA));
                });
            });
        }

        private partial class FullSkinSettingsCallerHost
        {
            public bool IsHostDisposed => IsDisposed;
        }

        [Test]
        public void TestCurrentExternalWorkspaceUnregisterWaitsForExactOldDetachAndNeverTouchesSource()
        {
            ExternalMutationContext context = addCurrentExternalPackage("current unregister detach");
            FullSkinSettingsCallerHost callerHost = null!;
            SkinCurrentRevision revisionA = null!;
            SkinCurrentRevision fallbackRevision = null!;
            SkinCurrentRevisionLease oldHolder = null!;
            SkinCurrentRevisionLease lateHolder = null!;
            FolderInventorySnapshot sourceBefore = default;
            string physicalDigestBefore = string.Empty;
            int beforeRealmCommitCalls = 0;
            int hookSawDetachedExactBoundary = 0;

            AddStep("capture exact current external revision and source", () =>
            {
                revisionA = manager.CurrentRevision;
                oldHolder = manager.AcquireCurrentRevisionHolderLease();
                sourceBefore = captureFolderInventory(context.PackageRoot);
                physicalDigestBefore = captureExternalRootPhysicalDigest(context.PackageRoot);
                manager.CurrentExternalUnregisterBeforeRealmCommit = () =>
                {
                    Interlocked.Increment(ref beforeRealmCommitCalls);

                    bool exactBoundary = revisionA.ConsumersDetached.IsCompleted
                                         && manager.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN
                                         && ReferenceEquals(manager.CurrentSkin.Value, manager.DefaultOmsSkin)
                                         && manager.CurrentRevision.SourceKind == SkinCurrentRevisionSourceKind.ProtectedFallback
                                         && Realm.Run(realm =>
                                         {
                                             realm.Refresh();
                                             SkinInfo record = realm.Find<SkinInfo>(context.RecordId)!;
                                             return record != null
                                                    && record.IsExternalFilesystemStorage
                                                    && string.Equals(
                                                        record.FilesystemStorageAuthorityOwner,
                                                        SkinExternalFolderRegistry.AUTHORITY_OWNER,
                                                        StringComparison.Ordinal);
                                         });

                    if (exactBoundary)
                        Interlocked.Exchange(ref hookSawDetachedExactBoundary, 1);
                };
                Add(callerHost = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for current external unregister row", () =>
                callerHost.Workspace.Rows.SingleOrDefault(row => row.RecordId == context.RecordId)
                          ?.ActionButtons[2].Enabled.Value == true);
            AddStep("open current external unregister dialog", () =>
                callerHost.Workspace.Rows.Single(row => row.RecordId == context.RecordId)
                          .ActionButtons[2]
                          .TriggerClick());
            AddUntilStep("wait for current external unregister dialog", () =>
                callerHost.DialogOverlay.CurrentDialog != null);
            AddStep("confirm current external unregister", () =>
                callerHost.DialogOverlay.CurrentDialog!.PerformAction<PopupDialogDangerousButton>());
            AddUntilStep("wait for protected fallback publication", () =>
                callerHost.Workspace.OperationInProgress
                && manager.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN
                && ReferenceEquals(manager.CurrentSkin.Value, manager.DefaultOmsSkin)
                && manager.CurrentRevision.SourceKind == SkinCurrentRevisionSourceKind.ProtectedFallback);
            AddStep("attach late holder to committed fallback", () =>
            {
                fallbackRevision = manager.CurrentRevision;
                lateHolder = manager.AcquireCurrentRevisionHolderLease();

                Assert.Multiple(() =>
                {
                    Assert.That(lateHolder.Revision, Is.SameAs(fallbackRevision));
                    Assert.That(revisionA.ConsumersDetached.IsCompleted, Is.False);
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(beforeRealmCommitCalls, Is.Zero);
                    Assert.That(Realm.Run(realm => realm.Find<SkinInfo>(context.RecordId)), Is.Not.Null);
                    Assert.That(captureFolderInventory(context.PackageRoot), Is.EqualTo(sourceBefore));
                });
            });
            AddStep("detach final exact old holder", () => oldHolder.Dispose());
            AddUntilStep("wait for pure Realm unregister", () =>
                !callerHost.Workspace.OperationInProgress
                && Realm.Run(realm => realm.Find<SkinInfo>(context.RecordId) == null));
            AddUntilStep("wait for exact old owner retirement", () => revisionA.Retired.IsCompleted);
            AddStep("assert late fallback holder did not block old retirement or touch source", () =>
            {
                try
                {
                    FolderInventorySnapshot sourceAfter = captureFolderInventory(context.PackageRoot);
                    string physicalDigestAfter = captureExternalRootPhysicalDigest(context.PackageRoot);

                    Assert.Multiple(() =>
                    {
                        Assert.That(beforeRealmCommitCalls, Is.EqualTo(1));
                        Assert.That(hookSawDetachedExactBoundary, Is.EqualTo(1));
                        Assert.That(revisionA.Retired.IsCompletedSuccessfully, Is.True);
                        Assert.That(manager.CurrentRevision, Is.SameAs(fallbackRevision));
                        Assert.That(sourceAfter, Is.EqualTo(sourceBefore));
                        Assert.That(physicalDigestAfter, Is.EqualTo(physicalDigestBefore));
                    });
                }
                finally
                {
                    manager.CurrentExternalUnregisterBeforeRealmCommit = () => { };
                    lateHolder.Dispose();
                }
            });
        }

        [Test]
        public void TestCurrentExternalWorkspaceUnregisterRejectsRealVisualParticipantBeforeFallbackAndRetriesAfterDetach()
        {
            ExternalMutationContext context = addCurrentExternalPackage("current unregister participant rejection");
            FullSkinSettingsCallerHost callerHost = null!;
            CurrentRevisionStoryboardHost storyboardHost = null!;
            Live<SkinInfo> selectionA = null!;
            Skin ownerA = null!;
            SkinCurrentRevision revisionA = null!;
            object textureA = null!;
            FolderInventorySnapshot sourceA = default;
            string physicalDigestA = string.Empty;
            ExternalRealmRecordSnapshot recordA = null!;
            int realmCommitAttempts = 0;
            int retiredA = 0;

            AddStep("mount real skin-sprite storyboard and current external workspace", () =>
            {
                selectionA = manager.CurrentSkinInfo.Value;
                ownerA = manager.CurrentSkin.Value;
                revisionA = manager.CurrentRevision;
                sourceA = captureFolderInventory(context.PackageRoot);
                physicalDigestA = captureExternalRootPhysicalDigest(context.PackageRoot);
                recordA = captureExternalRealmRecordSnapshot(context.RecordId);
                manager.CurrentExternalUnregisterBeforeRealmCommit = () =>
                    Interlocked.Increment(ref realmCommitAttempts);
                manager.CurrentRevisionRetired += revision =>
                {
                    if (ReferenceEquals(revision, revisionA))
                        retiredA++;
                };

                Add(storyboardHost = new CurrentRevisionStoryboardHost(manager));
                Add(callerHost = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for real storyboard texture and external unregister row", () =>
                storyboardHost.Sprite.IsLoaded
                && storyboardHost.Texture != null
                && callerHost.Workspace.Rows.SingleOrDefault(row => row.RecordId == context.RecordId)
                             ?.ActionButtons[2].Enabled.Value == true);
            AddStep("capture exact real visual A and confirm unregister", () =>
            {
                textureA = storyboardHost.Texture!;
                callerHost.Workspace.Rows.Single(row => row.RecordId == context.RecordId)
                          .ActionButtons[2]
                          .TriggerClick();
            });
            AddUntilStep("wait for external unregister confirmation", () =>
                callerHost.DialogOverlay.CurrentDialog != null);
            AddStep("confirm unregister against real visual blocker", () =>
                callerHost.DialogOverlay.CurrentDialog!.PerformAction<PopupDialogDangerousButton>());
            AddUntilStep("wait for deterministic workspace rejection feedback", () =>
                callerHost.PostedNotifications.Count == 1
                && !callerHost.Workspace.OperationInProgress);
            AddStep("assert participant rejection never published fallback or mutated authority", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(manager.CurrentRevision.Owner, Is.SameAs(ownerA));
                    Assert.That(storyboardHost.Texture, Is.SameAs(textureA));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(retiredA, Is.Zero);
                    Assert.That(realmCommitAttempts, Is.Zero,
                        "A rejecting visual participant must stop before protected fallback or Realm commit.");
                    Assert.That(captureExternalRealmRecordSnapshot(context.RecordId), Is.EqualTo(recordA));
                    Assert.That(captureFolderInventory(context.PackageRoot), Is.EqualTo(sourceA));
                    Assert.That(captureExternalRootPhysicalDigest(context.PackageRoot), Is.EqualTo(physicalDigestA));
                    Assert.That(
                        callerHost.PostedNotifications[0].Text.ToString(),
                        Is.EqualTo(Localisation.SkinSettingsStrings.FolderSkinOperationRejected.ToString()));
                });

                storyboardHost.Expire();
            });
            AddUntilStep("wait for real visual participant detach and retry row", () =>
                storyboardHost.Parent == null
                && callerHost.Workspace.Rows.SingleOrDefault(row => row.RecordId == context.RecordId)
                             ?.ActionButtons[2].Enabled.Value == true);
            AddStep("open external unregister retry", () =>
                callerHost.Workspace.Rows.Single(row => row.RecordId == context.RecordId)
                          .ActionButtons[2]
                          .TriggerClick());
            AddUntilStep("wait for external unregister retry confirmation", () =>
                callerHost.DialogOverlay.CurrentDialog != null);
            AddStep("confirm external unregister retry", () =>
                callerHost.DialogOverlay.CurrentDialog!.PerformAction<PopupDialogDangerousButton>());
            AddUntilStep("wait for pure-Realm retry convergence", () =>
                !callerHost.Workspace.OperationInProgress
                && Realm.Run(realm => realm.Find<SkinInfo>(context.RecordId) == null)
                && revisionA.Retired.IsCompleted);
            AddStep("assert detached retry retired A exactly once and left external source untouched", () =>
            {
                try
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                        Assert.That(manager.CurrentSkin.Value, Is.SameAs(manager.DefaultOmsSkin));
                        Assert.That(manager.CurrentRevision.SourceKind, Is.EqualTo(SkinCurrentRevisionSourceKind.ProtectedFallback));
                        Assert.That(revisionA.Retired.IsCompletedSuccessfully, Is.True);
                        Assert.That(retiredA, Is.EqualTo(1));
                        Assert.That(realmCommitAttempts, Is.EqualTo(1));
                        Assert.That(captureFolderInventory(context.PackageRoot), Is.EqualTo(sourceA));
                        Assert.That(captureExternalRootPhysicalDigest(context.PackageRoot), Is.EqualTo(physicalDigestA));
                    });
                }
                finally
                {
                    manager.CurrentExternalUnregisterBeforeRealmCommit = () => { };
                }
            });
        }

        [Test]
        public void TestCurrentExternalRealmFailureRollbackWaitsForHalfLoadedConsumerAndRestoresExactOldPair()
        {
            ExternalMutationContext context = addCurrentExternalPackage(
                "current unregister half-loaded rollback",
                root => writeStarFountainRevisionPackage(root, "A", new Rgba32(235, 55, 95, 255)));
            FullSkinSettingsCallerHost callerHost = null!;
            InvisibleLoadStarFountainHost invisibleHost = null!;
            Live<SkinInfo> selectionA = null!;
            Skin ownerA = null!;
            SkinCurrentRevision revisionA = null!;
            object textureA = null!;
            FolderInventorySnapshot sourceA = default;
            string physicalDigestA = string.Empty;
            int realmCommitAttempts = 0;
            var fallbackReached = new ManualResetEventSlim();
            var allowRealmFailure = new ManualResetEventSlim();
            var loaderEntered = new ManualResetEventSlim();
            var allowLoader = new ManualResetEventSlim();

            AddStep("capture exact external A and install held Realm failure", () =>
            {
                selectionA = manager.CurrentSkinInfo.Value;
                ownerA = manager.CurrentSkin.Value;
                revisionA = manager.CurrentRevision;
                textureA = ownerA.GetTexture("Menu/fountain-star")!;
                sourceA = captureFolderInventory(context.PackageRoot);
                physicalDigestA = captureExternalRootPhysicalDigest(context.PackageRoot);
                Assert.That(textureA, Is.Not.Null);
                manager.CurrentExternalUnregisterBeforeRealmCommit = () =>
                {
                    Interlocked.Increment(ref realmCommitAttempts);
                    fallbackReached.Set();

                    if (!allowRealmFailure.Wait(TimeSpan.FromSeconds(30)))
                        throw new TimeoutException("Timed out waiting to inject the current external Realm failure.");

                    throw new IOException("test-only Realm failure after half-loaded consumer attachment");
                };

                Add(invisibleHost = new InvisibleLoadStarFountainHost(manager, loaderEntered, allowLoader));
                Add(callerHost = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for real current external unregister row", () =>
                invisibleHost.IsLoaded
                && callerHost.Workspace.Rows.SingleOrDefault(row => row.RecordId == context.RecordId)
                             ?.ActionButtons[2].Enabled.Value == true);
            AddStep("open current external unregister dialog", () =>
                callerHost.Workspace.Rows.Single(row => row.RecordId == context.RecordId)
                          .ActionButtons[2]
                          .TriggerClick());
            AddUntilStep("wait for current external unregister confirmation", () =>
                callerHost.DialogOverlay.CurrentDialog != null);
            AddStep("confirm unregister into held Realm boundary", () =>
                callerHost.DialogOverlay.CurrentDialog!.PerformAction<PopupDialogDangerousButton>());
            AddUntilStep("wait for committed fallback before Realm mutation", () =>
                fallbackReached.IsSet
                && manager.CurrentRevision.SourceKind == SkinCurrentRevisionSourceKind.ProtectedFallback
                && ReferenceEquals(manager.CurrentSkin.Value, manager.DefaultOmsSkin));
            AddStep("start production StarFountain load against fallback", () => invisibleHost.BeginLoad());
            AddUntilStep("wait inside hidden StarFountain BDL", () => loaderEntered.IsSet);
            AddStep("release Realm failure with temporary participant attached", () => allowRealmFailure.Set());
            AddWaitStep("allow rollback to reach temporary detach boundary", 10);
            AddStep("assert rollback remains on fallback while hidden consumer cannot stage", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(realmCommitAttempts, Is.EqualTo(1));
                    Assert.That(callerHost.Workspace.OperationInProgress, Is.True);
                    Assert.That(manager.CurrentRevision.SourceKind, Is.EqualTo(SkinCurrentRevisionSourceKind.ProtectedFallback));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(manager.DefaultOmsSkin));
                    Assert.That(invisibleHost.LoadCompleted, Is.False);
                    Assert.That(revisionA.Retired.IsCompleted, Is.False,
                        "The rollback operation lease must retain exact A while the temporary participant blocks restore.");
                    Assert.That(Realm.Run(realm => realm.Find<SkinInfo>(context.RecordId)), Is.Not.Null);
                    Assert.That(captureFolderInventory(context.PackageRoot), Is.EqualTo(sourceA));
                });

                allowLoader.Set();
            });
            AddUntilStep("wait for formal consumer and exact A rollback", () =>
                invisibleHost.LoadTask.IsCompleted
                && invisibleHost.LoadCompleted
                && ReferenceEquals(manager.CurrentSkinInfo.Value, selectionA)
                && ReferenceEquals(manager.CurrentSkin.Value, ownerA)
                && ReferenceEquals(manager.CurrentRevision, revisionA)
                && !callerHost.Workspace.OperationInProgress);
            AddStep("assert failed mutation restored exact old pair after staged formal attach", () =>
            {
                try
                {
                    invisibleHost.LoadTask.GetAwaiter().GetResult();

                    Assert.Multiple(() =>
                    {
                        Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                        Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                        Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                        Assert.That(manager.CurrentRevision.Owner, Is.SameAs(ownerA));
                        Assert.That(invisibleHost.Texture, Is.SameAs(textureA),
                            "The formal StarFountain participant must finish its staged rebuild against exact A, not retain fallback.");
                        Assert.That(revisionA.Retired.IsCompleted, Is.False);
                        Assert.That(Realm.Run(realm => realm.Find<SkinInfo>(context.RecordId)), Is.Not.Null);
                        Assert.That(captureFolderInventory(context.PackageRoot), Is.EqualTo(sourceA));
                        Assert.That(captureExternalRootPhysicalDigest(context.PackageRoot), Is.EqualTo(physicalDigestA));
                    });
                }
                finally
                {
                    manager.CurrentExternalUnregisterBeforeRealmCommit = () => { };
                    fallbackReached.Dispose();
                    allowRealmFailure.Dispose();
                    loaderEntered.Dispose();
                    allowLoader.Dispose();
                }
            });
        }

        [TestCase("service-owner")]
        [TestCase("record-field")]
        [TestCase("registry-declaration")]
        public void TestCurrentExternalWorkspaceUnregisterFinalRealmDriftRestoresExactRevisionAndRetries(string driftKind)
        {
            const string drifted_owner = "test-only.foreign-owner";
            const string drifted_creator = "test-only-final-record-drift";

            ExternalMutationContext context = addCurrentExternalPackage($"current unregister final {driftKind} drift");
            FullSkinSettingsCallerHost callerHost = null!;
            Live<SkinInfo> selectionA = null!;
            Skin ownerA = null!;
            SkinCurrentRevision revisionA = null!;
            SkinCurrentRevision fallbackAttempt = null!;
            ExternalRealmRecordSnapshot recordA = null!;
            ExternalRealmRecordSnapshot expectedDriftedRecord = null!;
            FolderInventorySnapshot sourceA = default;
            string physicalDigestA = string.Empty;
            string declarationDriftPath = string.Empty;
            int boundaryCalls = 0;
            int exactBoundaryObserved = 0;
            int retiredA = 0;
            int retiredFallback = 0;

            AddStep("capture exact external A and install final Realm drift", () =>
            {
                selectionA = manager.CurrentSkinInfo.Value;
                ownerA = manager.CurrentSkin.Value;
                revisionA = manager.CurrentRevision;
                recordA = captureExternalRealmRecordSnapshot(context.RecordId);
                sourceA = captureFolderInventory(context.PackageRoot);
                physicalDigestA = captureExternalRootPhysicalDigest(context.PackageRoot);
                declarationDriftPath = Path.GetFullPath($"{context.PackageRoot}-final-registry-drift-{Guid.NewGuid():N}");
                expectedDriftedRecord = driftKind switch
                {
                    "service-owner" => recordA with { FilesystemStorageAuthorityOwner = drifted_owner },
                    "record-field" => recordA with { Creator = drifted_creator },
                    "registry-declaration" => recordA with { FilesystemStoragePath = declarationDriftPath },
                    _ => throw new ArgumentOutOfRangeException(nameof(driftKind), driftKind, null),
                };

                manager.CurrentRevisionRetired += revision =>
                {
                    if (ReferenceEquals(revision, revisionA))
                        retiredA++;

                    if (fallbackAttempt != null && ReferenceEquals(revision, fallbackAttempt))
                        retiredFallback++;
                };
                manager.CurrentExternalUnregisterBeforeRealmCommit = () =>
                {
                    int call = Interlocked.Increment(ref boundaryCalls);

                    if (call != 1)
                        return;

                    fallbackAttempt = manager.CurrentRevision;

                    if (revisionA.ConsumersDetached.IsCompleted
                        && manager.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN
                        && ReferenceEquals(manager.CurrentSkin.Value, manager.DefaultOmsSkin)
                        && fallbackAttempt.SourceKind == SkinCurrentRevisionSourceKind.ProtectedFallback)
                    {
                        Interlocked.Exchange(ref exactBoundaryObserved, 1);
                    }

                    context.Selection.PerformWrite(info =>
                    {
                        switch (driftKind)
                        {
                            case "service-owner":
                                info.FilesystemStorageAuthorityOwner = drifted_owner;
                                break;

                            case "record-field":
                                info.Creator = drifted_creator;
                                break;

                            case "registry-declaration":
                                info.FilesystemStoragePath = declarationDriftPath;
                                break;

                            default:
                                throw new ArgumentOutOfRangeException(nameof(driftKind), driftKind, null);
                        }
                    });
                };
                Add(callerHost = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for final-drift external unregister row", () =>
                callerHost.Workspace.Rows.SingleOrDefault(row => row.RecordId == context.RecordId)
                          ?.ActionButtons[2].Enabled.Value == true);
            AddStep("open final-drift external unregister dialog", () =>
                callerHost.Workspace.Rows.Single(row => row.RecordId == context.RecordId)
                          .ActionButtons[2]
                          .TriggerClick());
            AddUntilStep("wait for final-drift confirmation", () =>
                callerHost.DialogOverlay.CurrentDialog != null);
            AddStep("confirm unregister into final Realm drift", () =>
                callerHost.DialogOverlay.CurrentDialog!.PerformAction<PopupDialogDangerousButton>());
            AddUntilStep("wait for final-drift rejection and exact A rollback", () =>
                !callerHost.Workspace.OperationInProgress
                && callerHost.PostedNotifications.Count == 1
                && ReferenceEquals(manager.CurrentSkinInfo.Value, selectionA)
                && ReferenceEquals(manager.CurrentSkin.Value, ownerA)
                && ReferenceEquals(manager.CurrentRevision, revisionA));
            AddUntilStep("wait for failed fallback attempt retirement", () =>
                fallbackAttempt != null && fallbackAttempt.Retired.IsCompleted);
            AddStep("assert final compare retained drifted record, exact A and source", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(boundaryCalls, Is.EqualTo(1));
                    Assert.That(exactBoundaryObserved, Is.EqualTo(1));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(manager.CurrentRevision.Owner, Is.SameAs(ownerA));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(retiredA, Is.Zero);
                    Assert.That(fallbackAttempt.Retired.IsCompletedSuccessfully, Is.True);
                    Assert.That(retiredFallback, Is.EqualTo(1));
                    Assert.That(captureExternalRealmRecordSnapshot(context.RecordId), Is.EqualTo(expectedDriftedRecord));
                    Assert.That(captureFolderInventory(context.PackageRoot), Is.EqualTo(sourceA));
                    Assert.That(captureExternalRootPhysicalDigest(context.PackageRoot), Is.EqualTo(physicalDigestA));
                    Assert.That(Directory.Exists(declarationDriftPath), Is.False);
                    Assert.That(
                        callerHost.PostedNotifications[0].Text.ToString(),
                        Is.EqualTo(Localisation.SkinSettingsStrings.FolderSkinOperationRejected.ToString()));
                });
            });
            AddStep("restore only the concurrent Realm field", () =>
            {
                context.Selection.PerformWrite(info =>
                {
                    switch (driftKind)
                    {
                        case "service-owner":
                            info.FilesystemStorageAuthorityOwner = recordA.FilesystemStorageAuthorityOwner;
                            break;

                        case "record-field":
                            info.Creator = recordA.Creator;
                            break;

                        case "registry-declaration":
                            info.FilesystemStoragePath = recordA.FilesystemStoragePath;
                            break;

                        default:
                            throw new ArgumentOutOfRangeException(nameof(driftKind), driftKind, null);
                    }
                });

                Assert.That(Remove(callerHost, disposeImmediately: true), Is.True);
                Add(callerHost = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for restored external unregister row in reopened settings", () =>
                callerHost.Workspace.Rows.SingleOrDefault(row => row.RecordId == context.RecordId)
                          ?.ActionButtons[2].Enabled.Value == true);
            AddStep("open restored external unregister retry", () =>
                callerHost.Workspace.Rows.Single(row => row.RecordId == context.RecordId)
                          .ActionButtons[2]
                          .TriggerClick());
            AddUntilStep("wait for restored external retry confirmation", () =>
                callerHost.DialogOverlay.CurrentDialog != null);
            AddStep("confirm restored external unregister retry", () =>
                callerHost.DialogOverlay.CurrentDialog!.PerformAction<PopupDialogDangerousButton>());
            AddUntilStep("wait for restored external pure-Realm unregister", () =>
                !callerHost.Workspace.OperationInProgress
                && Realm.Run(realm => realm.Find<SkinInfo>(context.RecordId) == null)
                && revisionA.Retired.IsCompleted);
            AddStep("assert final-drift retry retired exact A and never touched source", () =>
            {
                try
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(boundaryCalls, Is.EqualTo(2));
                        Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                        Assert.That(manager.CurrentSkin.Value, Is.SameAs(manager.DefaultOmsSkin));
                        Assert.That(manager.CurrentRevision.SourceKind, Is.EqualTo(SkinCurrentRevisionSourceKind.ProtectedFallback));
                        Assert.That(revisionA.Retired.IsCompletedSuccessfully, Is.True);
                        Assert.That(retiredA, Is.EqualTo(1));
                        Assert.That(retiredFallback, Is.EqualTo(1));
                        Assert.That(captureFolderInventory(context.PackageRoot), Is.EqualTo(sourceA));
                        Assert.That(captureExternalRootPhysicalDigest(context.PackageRoot), Is.EqualTo(physicalDigestA));
                        Assert.That(Directory.Exists(declarationDriftPath), Is.False);
                    });
                }
                finally
                {
                    manager.CurrentExternalUnregisterBeforeRealmCommit = () => { };
                }
            });
        }

        [TestCase("missing")]
        [TestCase("drift")]
        public void TestCurrentExternalUnregisterRealmFailureRestoresExactRevisionAndRetriesWithoutSourceAuthority(string sourceState)
        {
            const string drift_marker = "\n// current unregister source drift remains untouched\n";
            ExternalMutationContext context = addCurrentExternalPackage($"current unregister {sourceState}");
            Live<SkinInfo> selectionB = null!;
            Skin ownerB = null!;
            SkinCurrentRevision revisionB = null!;
            Task<SkinCurrentRevisionReloadResult>? reload = null;
            Task<bool>? failedUnregister = null;
            Task<bool>? retryUnregister = null;
            int realmCommitAttempts = 0;
            FolderInventorySnapshot driftedSource = default;
            string registryRevisionA = string.Empty;

            AddStep("write B and start same-ID external reload", () =>
            {
                registryRevisionA = Realm.Run(realm => realm.Find<SkinInfo>(context.RecordId)!.Hash);
                writeRevisionPackage(context.PackageRoot, "B", new Rgba32(35, 205, 145, 255));
                reload = manager.ReloadCurrentRevisionAsync();
            });
            AddUntilStep("wait for same-ID external B", () => reload?.IsCompleted == true);
            AddStep("capture exact reloaded B and change only external source", () =>
            {
                Assert.That(reload!.GetAwaiter().GetResult(), Is.EqualTo(SkinCurrentRevisionReloadResult.Success));
                selectionB = manager.CurrentSkinInfo.Value;
                ownerB = manager.CurrentSkin.Value;
                revisionB = manager.CurrentRevision;

                Assert.Multiple(() =>
                {
                    Assert.That(selectionB, Is.SameAs(context.Selection));
                    Assert.That(revisionB.RecordId, Is.EqualTo(context.RecordId));
                    Assert.That(revisionB.Owner, Is.SameAs(ownerB));
                    Assert.That(revisionB.ContentRevision, Is.Not.EqualTo(registryRevisionA));
                    Assert.That(
                        Realm.Run(realm => realm.Find<SkinInfo>(context.RecordId)!.Hash),
                        Is.EqualTo(registryRevisionA),
                        "The external registry fingerprint is not the live immutable owner revision.");
                });

                if (sourceState == "missing")
                {
                    Directory.Delete(context.PackageRoot, recursive: true);
                }
                else
                {
                    File.AppendAllText(Path.Combine(context.PackageRoot, "skin.ini"), drift_marker);
                    driftedSource = captureFolderInventory(context.PackageRoot);
                }

                manager.CurrentExternalUnregisterBeforeRealmCommit = () =>
                {
                    if (Interlocked.Increment(ref realmCommitAttempts) == 1)
                        throw new IOException("test-only current external Realm failure");
                };
            });
            AddStep("request current external unregister into Realm failure", () =>
                failedUnregister = manager.UnregisterExternalFolderAsync(context.RecordId));
            AddUntilStep("wait for exact external rollback", () => failedUnregister?.IsCompleted == true);
            AddStep("assert exact B pair revision and record survived", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(failedUnregister!.GetAwaiter().GetResult(), Is.False);
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionB));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerB));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionB));
                    Assert.That(revisionB.Retired.IsCompleted, Is.False);
                    Assert.That(Realm.Run(realm => realm.Find<SkinInfo>(context.RecordId)), Is.Not.Null);
                    Assert.That(Directory.Exists(context.PackageRoot), Is.EqualTo(sourceState != "missing"));

                    if (sourceState == "drift")
                    {
                        Assert.That(captureFolderInventory(context.PackageRoot), Is.EqualTo(driftedSource));
                        Assert.That(File.ReadAllText(Path.Combine(context.PackageRoot, "skin.ini")), Does.EndWith(drift_marker));
                    }
                });
            });
            AddStep("retry exact current external unregister", () =>
                retryUnregister = manager.UnregisterExternalFolderAsync(context.RecordId));
            AddUntilStep("wait for successful external retry", () => retryUnregister?.IsCompleted == true);
            AddUntilStep("wait for retried old revision retirement", () => revisionB.Retired.IsCompleted);
            AddStep("assert retry removed only Realm record", () =>
            {
                try
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(retryUnregister!.GetAwaiter().GetResult(), Is.True);
                        Assert.That(realmCommitAttempts, Is.EqualTo(2));
                        Assert.That(Realm.Run(realm => realm.Find<SkinInfo>(context.RecordId)), Is.Null);
                        Assert.That(Directory.Exists(context.PackageRoot), Is.EqualTo(sourceState != "missing"));

                        if (sourceState == "drift")
                            Assert.That(captureFolderInventory(context.PackageRoot), Is.EqualTo(driftedSource));
                    });
                }
                finally
                {
                    manager.CurrentExternalUnregisterBeforeRealmCommit = () => { };
                }
            });
        }

        [Test]
        public void TestCurrentOrdinaryOskRealmFailureRestoresExactPairRevisionRecordAndBlobs()
        {
            MemoryStream archive = null!;
            Task<Live<SkinInfo>>? importTask = null;
            Live<SkinInfo> selectionA = null!;
            Skin ownerA = null!;
            SkinCurrentRevision revisionA = null!;
            RealmPackageAtomicSnapshot packageA = null!;
            FullSkinSettingsCallerHost callerHost = null!;
            int realmCommitAttempts = 0;

            AddStep("import real ordinary osk", () =>
            {
                archive = createCurrentMutationOsk();
                importTask = manager.Import(new ImportTask(archive, $"current-delete-{Guid.NewGuid():N}.osk"));
            });
            AddUntilStep("wait for ordinary osk import", () => importTask?.IsCompleted == true);
            AddStep("select real ordinary osk", () =>
            {
                selectionA = importTask!.GetAwaiter().GetResult();
                manager.CurrentSkinInfo.Value = selectionA;
            });
            AddUntilStep("wait for ordinary osk current pair", () =>
                manager.CurrentSkinInfo.Value.ID == selectionA.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == selectionA.ID
                && manager.CurrentSkin.Value is BmsLegacySkin);
            AddStep("capture exact ordinary A record revision and blobs", () =>
            {
                ownerA = manager.CurrentSkin.Value;
                revisionA = manager.CurrentRevision;
                packageA = captureRealmPackageAtomicSnapshot(selectionA);
                manager.CurrentRealmPackageDeleteBeforeRealmCommit = () =>
                {
                    if (Interlocked.Increment(ref realmCommitAttempts) == 1)
                        throw new IOException("test-only current ordinary Realm failure");
                };
                Add(callerHost = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for real ordinary delete caller", () => callerHost.CurrentDeleteButton.Enabled.Value);
            AddStep("open ordinary delete confirmation", () => callerHost.CurrentDeleteButton.TriggerClick());
            AddUntilStep("wait for ordinary delete confirmation", () =>
                callerHost.DialogOverlay.CurrentDialog is SkinSection.SkinDeleteDialog dialog
                && dialog.RecordId == selectionA.ID);
            AddStep("confirm ordinary delete into Realm failure", () =>
                callerHost.DialogOverlay.CurrentDialog!.PerformAction<PopupDialogDangerousButton>());
            AddUntilStep("wait for failed ordinary deletion observation", () =>
                realmCommitAttempts == 1
                && callerHost.CurrentDeleteButton.Enabled.Value
                && ReferenceEquals(manager.CurrentRevision, revisionA));
            AddStep("assert ordinary failure restored exact A and every blob", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(selectionA.PerformRead(record => record.DeletePending), Is.False);
                    Assert.That(captureRealmPackageAtomicSnapshot(selectionA), Is.EqualTo(packageA));
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
            AddStep("retry ordinary delete through the same real caller", () => callerHost.CurrentDeleteButton.TriggerClick());
            AddUntilStep("wait for retry ordinary delete confirmation", () =>
                callerHost.DialogOverlay.CurrentDialog is SkinSection.SkinDeleteDialog dialog
                && dialog.RecordId == selectionA.ID);
            AddStep("confirm ordinary delete retry", () =>
                callerHost.DialogOverlay.CurrentDialog!.PerformAction<PopupDialogDangerousButton>());
            AddUntilStep("wait for ordinary retry convergence", () =>
                realmCommitAttempts == 2
                && selectionA.PerformRead(record => record.DeletePending)
                && manager.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN
                && ReferenceEquals(manager.CurrentSkin.Value, manager.DefaultOmsSkin));
            AddStep("assert soft delete retained exact imported blobs", () =>
            {
                try
                {
                    RealmPackageAtomicSnapshot deleted = captureRealmPackageAtomicSnapshot(selectionA);
                    Assert.Multiple(() =>
                    {
                        Assert.That(deleted with { DeletePending = false }, Is.EqualTo(packageA));
                        Assert.That(revisionA.Retired.IsCompletedSuccessfully, Is.True);
                        Assert.That(
                            new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                            Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                    });
                }
                finally
                {
                    manager.CurrentRealmPackageDeleteBeforeRealmCommit = () => { };
                    archive.Dispose();
                }
            });
        }

        [Test]
        public void TestCurrentOrdinaryFallbackAdmissionRejectsEveryRetainedRealmMutationBypass()
        {
            MemoryStream archive = null!;
            Task<Live<SkinInfo>>? importTask = null;
            Task<bool>? deleteTask = null;
            Live<SkinInfo> selectionA = null!;
            Skin retainedOwnerA = null!;
            Skin ownerA = null!;
            SkinCurrentRevision revisionA = null!;
            SkinInfo retainedRecordA = null!;
            RealmNamedFileUsage retainedFileA = null!;
            RealmPackageAtomicSnapshot packageA = null!;
            RealmPackageAtomicSnapshot packageAtFallback = null!;
            var rejections = new InvalidOperationException?[5];
            Exception? unexpectedMutationFailure = null;
            bool fallbackBoundaryObserved = false;
            bool retainedDeleteResult = true;

            AddStep("import and select retained-mutation ordinary osk", () =>
            {
                archive = createCurrentMutationOsk();
                importTask = manager.Import(new ImportTask(archive, $"current-retained-mutation-{Guid.NewGuid():N}.osk"));
            });
            AddUntilStep("wait for retained-mutation import", () => importTask?.IsCompleted == true);
            AddStep("select retained-mutation ordinary osk", () =>
            {
                selectionA = importTask!.GetAwaiter().GetResult();
                manager.CurrentSkinInfo.Value = selectionA;
            });
            AddUntilStep("wait for retained-mutation current pair", () =>
                manager.CurrentSkinInfo.Value.ID == selectionA.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == selectionA.ID
                && manager.CurrentSkin.Value is BmsLegacySkin);
            AddStep("retain old package handles and install fallback race", () =>
            {
                ownerA = manager.CurrentSkin.Value;
                revisionA = manager.CurrentRevision;
                retainedRecordA = selectionA.PerformRead(info => info.Detach());
                retainedFileA = retainedRecordA.Files.First();
                retainedOwnerA = selectionA.PerformRead(info => info.CreateInstance(manager));
                packageA = captureRealmPackageAtomicSnapshot(selectionA);

                manager.CurrentRealmPackageDeleteBeforeRealmCommit = () =>
                {
                    fallbackBoundaryObserved = manager.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN
                                               && ReferenceEquals(manager.CurrentSkin.Value, manager.DefaultOmsSkin)
                                               && manager.CurrentRevision.SourceKind == SkinCurrentRevisionSourceKind.ProtectedFallback;

                    void attempt(int index, Action mutation)
                    {
                        try
                        {
                            mutation();
                            unexpectedMutationFailure ??= new AssertionException($"retained mutation {index} was accepted");
                        }
                        catch (InvalidOperationException exception)
                        {
                            rejections[index] = exception;
                        }
                        catch (Exception exception)
                        {
                            unexpectedMutationFailure ??= exception;
                        }
                    }

                    attempt(0, () => manager.Save(retainedOwnerA));
                    attempt(1, () => manager.Rename(selectionA, "retained rename must not commit"));

                    using (var addContents = new MemoryStream(new byte[] { 0x01, 0x02 }))
                        attempt(2, () => manager.AddFile(retainedRecordA, addContents, "retained-bypass.bin"));

                    attempt(3, () => manager.DeleteFile(retainedRecordA, retainedFileA));

                    using (var replaceContents = new MemoryStream(new byte[] { 0x03, 0x04 }))
                        attempt(4, () => manager.ReplaceFile(retainedRecordA, retainedFileA, replaceContents));

                    retainedDeleteResult = manager.Delete(retainedRecordA);
                    manager.Undelete(retainedRecordA);
                    packageAtFallback = captureRealmPackageAtomicSnapshot(selectionA);
                    throw new IOException("test-only rollback after retained mutation attempts");
                };
            });
            AddStep("delete current ordinary package into retained mutation race", () =>
                deleteTask = manager.DeleteSkinAsync(selectionA.ID));
            AddUntilStep("wait for retained mutation rollback", () => deleteTask?.IsCompleted == true);
            AddStep("assert admission rejected stale owner record and file bypasses", () =>
            {
                try
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(deleteTask!.GetAwaiter().GetResult(), Is.False);
                        Assert.That(fallbackBoundaryObserved, Is.True);
                        Assert.That(unexpectedMutationFailure, Is.Null);
                        Assert.That(rejections, Has.All.Not.Null);
                        Assert.That(
                            rejections.Select(exception => exception!.Message),
                            Is.All.EqualTo(SkinManager.REALM_PACKAGE_MUTATION_BUSY_DIAGNOSTIC));
                        Assert.That(retainedDeleteResult, Is.False);
                        Assert.That(packageAtFallback, Is.EqualTo(packageA));
                        Assert.That(captureRealmPackageAtomicSnapshot(selectionA), Is.EqualTo(packageA));
                        Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                        Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                        Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                        Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    });
                }
                finally
                {
                    manager.CurrentRealmPackageDeleteBeforeRealmCommit = () => { };
                    retainedOwnerA.Dispose();
                    archive.Dispose();
                }
            });
        }

        [Test]
        public void TestRealmMutationBoundaryRejectsCurrentDeleteAndAllowsNestedImporterFileWrites()
        {
            MemoryStream currentArchive = null!;
            MemoryStream otherArchive = null!;
            Task<Live<SkinInfo>>? currentImportTask = null;
            Task<Live<SkinInfo>>? otherImportTask = null;
            Task<bool>? saveTask = null;
            Task<bool>? deleteTask = null;
            Live<SkinInfo> current = null!;
            Live<SkinInfo> other = null!;
            SkinCurrentRevision revisionA = null!;
            Skin ownerA = null!;
            var boundaryEntered = new ManualResetEventSlim();
            var allowBoundaryToProceed = new ManualResetEventSlim();

            AddStep("import current and non-current ordinary packages", () =>
            {
                currentArchive = createCurrentMutationOsk("current admission package");
                otherArchive = createCurrentMutationOsk("non-current admission package");
                currentImportTask = manager.Import(new ImportTask(currentArchive, $"current-admission-{Guid.NewGuid():N}.osk"));
                otherImportTask = manager.Import(new ImportTask(otherArchive, $"other-admission-{Guid.NewGuid():N}.osk"));
            });
            AddUntilStep("wait for both ordinary imports", () =>
                currentImportTask?.IsCompleted == true && otherImportTask?.IsCompleted == true);
            AddStep("select current and prepare real non-current save", () =>
            {
                current = currentImportTask!.GetAwaiter().GetResult();
                other = otherImportTask!.GetAwaiter().GetResult();
                Assert.That(other.ID, Is.Not.EqualTo(current.ID));
                manager.CurrentSkinInfo.Value = current;
            });
            AddUntilStep("wait for admission current pair", () =>
                manager.CurrentSkinInfo.Value.ID == current.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == current.ID);
            AddStep("hold outer Realm package boundary before backend write", () =>
            {
                revisionA = manager.CurrentRevision;
                ownerA = manager.CurrentSkin.Value;
                manager.RealmPackageMutationBoundaryEntered = () =>
                {
                    boundaryEntered.Set();
                    Assert.That(allowBoundaryToProceed.Wait(TimeSpan.FromSeconds(10)), Is.True);
                };
                saveTask = Task.Run(() => other.PerformRead(info =>
                {
                    using Skin workerOwner = info.CreateInstance(manager);
                    return manager.Save(workerOwner);
                }));
            });
            AddUntilStep("wait for ordinary boundary admission", () => boundaryEntered.IsSet);
            AddStep("reject current delete while ordinary boundary is held", () =>
            {
                deleteTask = manager.DeleteSkinAsync(current.ID);

                Assert.Multiple(() =>
                {
                    Assert.That(deleteTask.IsCompleted, Is.True);
                    Assert.That(deleteTask.GetAwaiter().GetResult(), Is.False);
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(current));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(current.PerformRead(info => info.DeletePending), Is.False);
                });
            });
            AddStep("release boundary into real nested SkinImporter writes", () => allowBoundaryToProceed.Set());
            AddUntilStep("wait for non-current save backend", () => saveTask?.IsCompleted == true);
            AddStep("assert nested file callbacks completed without self-contention", () =>
            {
                try
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(saveTask!.Exception, Is.Null);
                        Assert.That(saveTask.IsCompletedSuccessfully, Is.True);
                        Assert.That(
                            Realm.Run(realm =>
                            {
                                realm.Refresh();
                                return realm.Find<SkinInfo>(other.ID)!.Files.Any(file => file.Filename == "skininfo.json");
                            }),
                            Is.True);
                        Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(current));
                        Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                        Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    });
                }
                finally
                {
                    manager.RealmPackageMutationBoundaryEntered = () => { };
                    allowBoundaryToProceed.Set();
                    boundaryEntered.Dispose();
                    allowBoundaryToProceed.Dispose();
                    currentArchive.Dispose();
                    otherArchive.Dispose();
                }
            });
        }

        [Test]
        public void TestCurrentManagedDeleteRejectsSourceDriftUntilExplicitSameIdReload()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            Live<SkinInfo> selectionA = null!;
            Skin ownerA = null!;
            SkinCurrentRevision revisionA = null!;
            SkinCurrentRevision revisionC = null!;
            FolderInventorySnapshot sourceC = default;
            Task<bool>? staleDelete = null;
            Task<SkinCurrentRevisionReloadResult>? reload = null;
            Task<bool>? reloadedDelete = null;
            string realmRegistrationFingerprint = string.Empty;

            AddStep("create and select managed revision A", () =>
            {
                (packageRoot, candidate) = createCandidate(
                    root => writeRevisionPackage(root, "A", new Rgba32(240, 40, 80, 255)),
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "managed-registration-fingerprint-A");
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for exact managed A", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && manager.CurrentSkin.Value is BmsLegacySkin);
            AddStep("capture exact managed A and drift source to C", () =>
            {
                selectionA = manager.CurrentSkinInfo.Value;
                ownerA = manager.CurrentSkin.Value;
                revisionA = manager.CurrentRevision;
                realmRegistrationFingerprint = candidate.PerformRead(info => info.Hash);

                Assert.Multiple(() =>
                {
                    Assert.That(revisionA.Owner, Is.SameAs(ownerA));
                    Assert.That(ownerA.PackageContentRevision, Is.EqualTo(revisionA.ContentRevision));
                    Assert.That(realmRegistrationFingerprint, Is.Not.EqualTo(revisionA.ContentRevision));
                });

                writeRevisionPackage(packageRoot, "C", new Rgba32(45, 120, 235, 255));
                sourceC = captureFolderInventory(packageRoot);
            });
            AddStep("request delete while live owner remains A and source is C", () =>
                staleDelete = manager.DeleteSkinAsync(candidate.ID));
            AddUntilStep("wait for stale managed delete rejection", () => staleDelete?.IsCompleted == true);
            AddStep("assert stale delete changed no pair source record or journal", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(staleDelete!.GetAwaiter().GetResult(), Is.False);
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(captureFolderInventory(packageRoot), Is.EqualTo(sourceC));
                    Assert.That(
                        Realm.Run(realm => realm.Find<SkinInfo>(candidate.ID)!.Hash),
                        Is.EqualTo(realmRegistrationFingerprint));
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
            AddStep("explicitly reload same ID to exact source C", () =>
                reload = manager.ReloadCurrentRevisionAsync());
            AddUntilStep("wait for managed C publication", () => reload?.IsCompleted == true);
            AddStep("capture exact managed C", () =>
            {
                Assert.That(reload!.GetAwaiter().GetResult(), Is.EqualTo(SkinCurrentRevisionReloadResult.Success));
                revisionC = manager.CurrentRevision;

                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(revisionC.RecordId, Is.EqualTo(candidate.ID));
                    Assert.That(revisionC.ContentRevision, Is.Not.EqualTo(revisionA.ContentRevision));
                    Assert.That(revisionC.ContentRevision, Is.EqualTo(captureManagedContentRevision(candidate)));
                    Assert.That(
                        Realm.Run(realm => realm.Find<SkinInfo>(candidate.ID)!.Hash),
                        Is.EqualTo(realmRegistrationFingerprint),
                        "Realm Hash remains the registration fingerprint, not the live capsule revision.");
                });
            });
            AddStep("delete explicitly reloaded current managed C", () =>
                reloadedDelete = manager.DeleteSkinAsync(candidate.ID));
            AddUntilStep("wait for managed C fallback publication", () =>
                manager.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN
                && ReferenceEquals(manager.CurrentSkin.Value, manager.DefaultOmsSkin)
                && manager.CurrentRevision.SourceKind == SkinCurrentRevisionSourceKind.ProtectedFallback);
            AddUntilStep("wait for exact managed C consumer detach", () =>
                revisionC.ConsumersDetached.IsCompleted);
            AddUntilStep("wait for exact managed C delete", () => reloadedDelete?.IsCompleted == true);
            AddUntilStep("wait for exact managed C retirement", () => revisionC.Retired.IsCompleted);
            AddStep("assert reload made the exact delete eligible", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(reloadedDelete!.GetAwaiter().GetResult(), Is.True);
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(manager.DefaultOmsSkin));
                    Assert.That(
                        manager.LastManagedFolderDeleteResult.FallbackCommitResult,
                        Is.EqualTo(SkinManagedFolderProtectedFallbackCommitResult.NotRequired),
                        "the C1 callback observes the already-published C2 fallback without weakening its boundary");
                    Assert.That(revisionC.Retired.IsCompletedSuccessfully, Is.True);
                    Assert.That(Directory.Exists(packageRoot), Is.False);
                    Assert.That(Realm.Run(realm => realm.Find<SkinInfo>(candidate.ID)), Is.Null);
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
        }

        [Test]
        public void TestCurrentManagedDeleteIsolatesThrowingSourceObserverAndRetiresAfterRealHolderDetaches()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            FullSkinSettingsCallerHost callerHost = null!;
            CurrentRevisionStarFountainHost consumerHost = null!;
            CurrentRevisionHolderHost holderHost = null!;
            SkinCurrentRevision revisionA = null!;
            Skin ownerA = null!;
            object textureA = null!;
            int throwingObserverCalls = 0;
            int retiredA = 0;
            Action throwingObserver = () =>
            {
                Interlocked.Increment(ref throwingObserverCalls);
                throw new InvalidOperationException("test-only current managed delete source observer failure");
            };

            AddStep("create and select throwing-observer managed A", () =>
            {
                (packageRoot, candidate) = createCandidate(
                    root => writeStarFountainRevisionPackage(root, "A", new Rgba32(235, 55, 95, 255)),
                    typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "current-managed-throwing-source-observer");
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for throwing-observer managed A", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && manager.CurrentSkin.Value is BmsLegacySkin);
            AddStep("mount throwing predecessor real consumer holder and caller", () =>
            {
                revisionA = manager.CurrentRevision;
                ownerA = manager.CurrentSkin.Value;
                manager.CurrentRevisionRetired += revision =>
                {
                    if (ReferenceEquals(revision, revisionA))
                        Interlocked.Increment(ref retiredA);
                };
                manager.SourceChanged += throwingObserver;

                Add(consumerHost = new CurrentRevisionStarFountainHost(manager));
                Add(holderHost = new CurrentRevisionHolderHost(manager, ownerA));
                Add(callerHost = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for real managed delete graph", () =>
                consumerHost.Texture != null
                && callerHost.Workspace.Rows.SingleOrDefault(row => row.RecordId == candidate.ID)
                             ?.ActionButtons[2].Enabled.Value == true);
            AddStep("capture A texture and open managed delete", () =>
            {
                textureA = consumerHost.Texture!;
                callerHost.Workspace.Rows.Single(row => row.RecordId == candidate.ID)
                          .ActionButtons[2]
                          .TriggerClick();
            });
            AddUntilStep("wait for managed delete confirmation", () =>
                callerHost.DialogOverlay.CurrentDialog is SkinSection.SkinDeleteDialog dialog
                && dialog.RecordId == candidate.ID);
            AddStep("confirm managed delete through real caller", () =>
                callerHost.DialogOverlay.CurrentDialog!.PerformAction<PopupDialogDangerousButton>());
            AddUntilStep("wait for fallback behind exact A holder", () =>
                callerHost.Workspace.OperationInProgress
                && manager.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN
                && ReferenceEquals(manager.CurrentSkin.Value, manager.DefaultOmsSkin)
                && manager.CurrentRevision.SourceKind == SkinCurrentRevisionSourceKind.ProtectedFallback
                && !ReferenceEquals(consumerHost.Texture, textureA)
                && Volatile.Read(ref throwingObserverCalls) == 1);
            AddStep("assert throw isolation precedes physical mutation", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(revisionA.ConsumersDetached.IsCompleted, Is.False);
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(retiredA, Is.Zero);
                    Assert.That(Directory.Exists(packageRoot), Is.True);
                    Assert.That(Realm.Run(realm => realm.Find<SkinInfo>(candidate.ID)), Is.Not.Null);
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
            AddStep("detach final real A holder", () => holderHost.Expire());
            AddUntilStep("wait for managed physical delete and exact retire", () =>
                holderHost.Parent == null
                && !callerHost.Workspace.OperationInProgress
                && !Directory.Exists(packageRoot)
                && Realm.Run(realm => realm.Find<SkinInfo>(candidate.ID) == null)
                && revisionA.Retired.IsCompleted
                && Volatile.Read(ref retiredA) == 1);
            AddStep("assert isolated observer could not split managed delete", () =>
            {
                try
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(throwingObserverCalls, Is.EqualTo(1));
                        Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                        Assert.That(manager.CurrentSkin.Value, Is.SameAs(manager.DefaultOmsSkin));
                        Assert.That(manager.CurrentRevision.Owner, Is.SameAs(manager.DefaultOmsSkin));
                        Assert.That(revisionA.ConsumersDetached.IsCompletedSuccessfully, Is.True);
                        Assert.That(revisionA.Retired.IsCompletedSuccessfully, Is.True);
                        Assert.That(retiredA, Is.EqualTo(1));
                        Assert.That(manager.LastManagedFolderDeleteResult?.IsSuccess, Is.True);
                        Assert.That(
                            new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                            Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                    });
                }
                finally
                {
                    manager.SourceChanged -= throwingObserver;
                }
            });
        }

        [Test]
        public void TestCurrentManagedDeleteParticipantFailureDoesNotStartJournalOrPhysicalMutationAndRetries()
        {
            string packageRoot = string.Empty;
            Live<SkinInfo> candidate = null!;
            FullSkinSettingsCallerHost callerHost = null!;
            SkinRevisionParticipantRegistration rejectingParticipant = null!;
            Live<SkinInfo> selectionA = null!;
            Skin ownerA = null!;
            SkinCurrentRevision revisionA = null!;
            FolderInventorySnapshot sourceA = default;
            int prepareCalls = 0;

            AddStep("create and select current managed delete target", () =>
            {
                (packageRoot, candidate) = createCandidate(createCompletePackage, typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
                candidate.PerformWrite(info => info.Hash = "current-managed-participant-failure");
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for current managed target", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID);
            AddStep("mount real workspace and rejecting publication participant", () =>
            {
                selectionA = manager.CurrentSkinInfo.Value;
                ownerA = manager.CurrentSkin.Value;
                revisionA = manager.CurrentRevision;
                sourceA = captureFolderInventory(packageRoot);
                rejectingParticipant = manager.RegisterRevisionParticipant(
                    SkinRevisionParticipantKind.LifecycleHolder,
                    "current-managed-delete-failure",
                    _ =>
                    {
                        Interlocked.Increment(ref prepareCalls);
                        return Task.FromResult(false);
                    });
                Add(callerHost = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for current managed workspace row", () =>
                callerHost.Workspace.Rows.SingleOrDefault(row => row.RecordId == candidate.ID)
                          ?.ActionButtons[2].Enabled.Value == true);
            AddStep("open current managed workspace delete dialog", () =>
                callerHost.Workspace.Rows.Single(row => row.RecordId == candidate.ID)
                          .ActionButtons[2]
                          .TriggerClick());
            AddUntilStep("wait for current managed workspace delete dialog", () =>
                callerHost.DialogOverlay.CurrentDialog is SkinSection.SkinDeleteDialog dialog
                && dialog.RecordId == candidate.ID);
            AddStep("confirm current managed delete into participant failure", () =>
                callerHost.DialogOverlay.CurrentDialog!.PerformAction<PopupDialogDangerousButton>());
            AddUntilStep("wait for rejected managed workspace operation", () =>
                prepareCalls == 1 && !callerHost.Workspace.OperationInProgress);
            AddStep("assert no journal or physical mutation preceded publication", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(selectionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(Directory.Exists(packageRoot), Is.True);
                    Assert.That(captureFolderInventory(packageRoot), Is.EqualTo(sourceA));
                    Assert.That(Realm.Run(realm => realm.Find<SkinInfo>(candidate.ID)), Is.Not.Null);
                    Assert.That(manager.IsManagedFolderDeleteRunning, Is.False);
                    Assert.That(manager.LastManagedFolderDeleteResult, Is.Null);
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
            AddStep("detach failed participant for retry", () => rejectingParticipant.Dispose());
            AddUntilStep("wait for retry managed workspace row", () =>
                callerHost.Workspace.Rows.SingleOrDefault(row => row.RecordId == candidate.ID)
                          ?.ActionButtons[2].Enabled.Value == true);
            AddStep("open retry managed workspace delete dialog", () =>
                callerHost.Workspace.Rows.Single(row => row.RecordId == candidate.ID)
                          .ActionButtons[2]
                          .TriggerClick());
            AddUntilStep("wait for retry managed workspace delete dialog", () =>
                callerHost.DialogOverlay.CurrentDialog is SkinSection.SkinDeleteDialog dialog
                && dialog.RecordId == candidate.ID);
            AddStep("confirm managed delete retry", () =>
                callerHost.DialogOverlay.CurrentDialog!.PerformAction<PopupDialogDangerousButton>());
            AddUntilStep("wait for managed delete retry fallback publication", () =>
                manager.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN
                && ReferenceEquals(manager.CurrentSkin.Value, manager.DefaultOmsSkin)
                && manager.CurrentRevision.SourceKind == SkinCurrentRevisionSourceKind.ProtectedFallback);
            AddUntilStep("wait for retried old revision consumer detach", () =>
                revisionA.ConsumersDetached.IsCompleted);
            AddUntilStep("wait for managed delete retry convergence", () =>
                !callerHost.Workspace.OperationInProgress
                && !Directory.Exists(packageRoot)
                && Realm.Run(realm => realm.Find<SkinInfo>(candidate.ID) == null));
            AddStep("assert retried current managed delete retired exact old revision", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(manager.DefaultOmsSkin));
                    Assert.That(revisionA.Retired.IsCompletedSuccessfully, Is.True);
                    Assert.That(manager.LastManagedFolderDeleteResult?.IsSuccess, Is.True);
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
        }

        private ExternalMutationContext addCurrentExternalPackage(string label, Action<string>? populate = null)
        {
            var context = new ExternalMutationContext();

            AddStep($"register {label} external package", () =>
            {
                context.PackageRoot = createExternalPackage(populate ?? createCompletePackage);
                context.RegistrationTask = manager.RegisterExternalFolderAsync(context.PackageRoot);
            });
            AddUntilStep($"wait for {label} external registration", () => context.RegistrationTask?.IsCompleted == true);
            AddStep($"query {label} external selection", () =>
            {
                Assert.That(context.RegistrationTask!.GetAwaiter().GetResult(), Is.True);
                context.DropdownTask = manager.GetAllUsableSkinsAsync();
            });
            AddUntilStep($"wait for {label} external selection", () => context.DropdownTask?.IsCompleted == true);
            AddStep($"select {label} external package", () =>
            {
                context.Selection = context.DropdownTask!.GetAwaiter().GetResult()
                                                   .Single(record => record.PerformRead(info => info.IsExternalFilesystemStorage));
                context.RecordId = context.Selection.ID;
                manager.CurrentSkinInfo.Value = context.Selection;
            });
            AddUntilStep($"wait for {label} current pair", () =>
                context.RecordId != Guid.Empty
                && manager.CurrentSkinInfo.Value.ID == context.RecordId
                && manager.CurrentSkin.Value.SkinInfo.ID == context.RecordId
                && manager.CurrentSkin.Value is BmsLegacySkin);

            return context;
        }

        private RealmPackageAtomicSnapshot captureRealmPackageAtomicSnapshot(Live<SkinInfo> record)
        {
            RealmPackageAtomicSnapshot snapshot = record.PerformRead(info => new RealmPackageAtomicSnapshot(
                info.ID,
                info.Name,
                info.Creator,
                info.InstantiationInfo,
                info.Hash,
                info.Protected,
                info.DeletePending,
                info.Files.Select(file => new RealmPackageAtomicFile(
                              file.Filename,
                              file.File.Hash,
                              readRealmBlobDigest(file.File.Hash)))
                    .OrderBy(file => file.Filename, StringComparer.Ordinal)
                    .ThenBy(file => file.Hash, StringComparer.Ordinal)
                    .ToArray()));

            Assert.That(snapshot.Files, Is.Not.Empty);
            return snapshot;
        }

        private ExternalRealmRecordSnapshot captureExternalRealmRecordSnapshot(Guid recordId)
            => Realm.Run(realm =>
            {
                realm.Refresh();
                SkinInfo record = realm.Find<SkinInfo>(recordId)!;
                Assert.That(record, Is.Not.Null);
                return new ExternalRealmRecordSnapshot(
                    record.ID,
                    record.Name,
                    record.Creator,
                    record.InstantiationInfo,
                    record.Hash,
                    record.Protected,
                    record.DeletePending,
                    record.FilesystemStoragePath,
                    record.IsExternalFilesystemStorage,
                    record.FilesystemStorageAuthorityOwner,
                    record.Files.Count);
            });

        private string readRealmBlobDigest(string hash)
        {
            var fileStore = new RealmFileStore(Realm, LocalStorage);
            using Stream stream = fileStore.Storage.GetStream(new RealmFile { Hash = hash }.GetStoragePath())!;
            Assert.That(stream, Is.Not.Null);
            return Convert.ToHexString(SHA256.HashData(stream));
        }

        private static MemoryStream createCurrentMutationOsk(string name = "current ordinary mutation atomicity")
        {
            string skinIni =
                "[General]\n" +
                $"Name: {name}\n" +
                "Author: OMS tests\n" +
                "Version: 2.7\n" +
                "\n" +
                "[Bms]\n" +
                "Keymode: 7K\n" +
                "NoteImage1: notes/note\n";
            var output = new MemoryStream();
            var entryStreams = new List<MemoryStream>();

            try
            {
                using var archive = ZipArchive.Create();
                var ini = new MemoryStream(Encoding.UTF8.GetBytes(skinIni));
                var note = new MemoryStream(createPng(new Rgba32(180, 75, 220, 255)), writable: false);
                entryStreams.Add(ini);
                entryStreams.Add(note);
                archive.AddEntry("skin.ini", ini);
                archive.AddEntry("notes/note.png", note);
                archive.SaveTo(output);
            }
            finally
            {
                foreach (MemoryStream stream in entryStreams)
                    stream.Dispose();
            }

            output.Position = 0;
            return output;
        }

        private sealed class ExternalMutationContext
        {
            public string PackageRoot { get; set; } = string.Empty;
            public Guid RecordId { get; set; }
            public Live<SkinInfo> Selection { get; set; } = null!;
            public Task<bool>? RegistrationTask { get; set; }
            public Task<IList<Live<SkinInfo>>>? DropdownTask { get; set; }
        }

        private sealed record RealmPackageAtomicSnapshot(
            Guid Id,
            string Name,
            string Creator,
            string InstantiationInfo,
            string Hash,
            bool Protected,
            bool DeletePending,
            RealmPackageAtomicFile[] Files)
        {
            public bool Equals(RealmPackageAtomicSnapshot? other)
                => other != null
                   && Id == other.Id
                   && string.Equals(Name, other.Name, StringComparison.Ordinal)
                   && string.Equals(Creator, other.Creator, StringComparison.Ordinal)
                   && string.Equals(InstantiationInfo, other.InstantiationInfo, StringComparison.Ordinal)
                   && string.Equals(Hash, other.Hash, StringComparison.Ordinal)
                   && Protected == other.Protected
                   && DeletePending == other.DeletePending
                   && Files.SequenceEqual(other.Files);

            public override int GetHashCode() => HashCode.Combine(Id, Name, Creator, InstantiationInfo, Hash, Protected, DeletePending);
        }

        private sealed record ExternalRealmRecordSnapshot(
            Guid Id,
            string Name,
            string Creator,
            string InstantiationInfo,
            string Hash,
            bool Protected,
            bool DeletePending,
            string? FilesystemStoragePath,
            bool IsExternalFilesystemStorage,
            string? FilesystemStorageAuthorityOwner,
            int FileCount);

        private sealed record RealmPackageAtomicFile(string Filename, string Hash, string BlobDigest);
    }
}
