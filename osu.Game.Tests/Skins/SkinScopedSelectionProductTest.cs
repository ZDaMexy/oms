// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Skinning.Windows;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Skins
{
    /// <summary>Exercises actual manager preparation/publication; no settings controls or synthetic publisher are used.</summary>
    [HeadlessTest]
    [TestFixture]
    [Platform("Win")]
    public partial class SkinScopedSelectionProductTest : OsuTestScene
    {
        [Resolved]
        private GameHost host { get; set; } = null!;

        private SkinManager manager = null!;
        private OsuConfigManager config = null!;
        private SkinSelectionPreferences preferences = null!;
        private Live<SkinInfo> bmsDefault = null!;
        private Live<SkinInfo> bmsSeven = null!;
        private Live<SkinInfo> maniaDefault = null!;
        private SkinSettingsPreview? preview;
        private SkinRevisionParticipantRegistration? liveParticipant;
        private CancellationTokenSource? cancellation;
        private readonly SkinSelectionScope sevenScope = new SkinSelectionScope("bms", 7);

        protected override bool UseFreshStoragePerRun => true;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create isolated manager and mode/key assignments", () =>
            {
                manager = new SkinManager(LocalStorage, Realm, host, Resources, Audio, Scheduler);
                config = new OsuConfigManager(LocalStorage);
                config.SetValue(OsuSetting.SkinKeyOverrides, "{}");
                preferences = new SkinSelectionPreferences(config);
                manager.SelectionPreferences = preferences;
                bmsDefault = createRecord("BMS mode default");
                bmsSeven = createRecord("BMS seven override");
                maniaDefault = createRecord("mania mode default");
                preferences.SetSkin(new SkinSelectionScope("bms"), bmsDefault.ID);
                preferences.SetSkin(sevenScope, bmsSeven.ID);
                preferences.SetSkin(new SkinSelectionScope("mania"), maniaDefault.ID);
            });
        }

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("release settings/host ownership and stop manager", () =>
            {
                preview?.Dispose();
                preview = null;
                liveParticipant?.Dispose();
                liveParticipant = null;
                cancellation?.Dispose();
                cancellation = null;
                manager.ShutdownManagedFolderMutations();
                config.Dispose();
            });
        }

        [Test]
        public void TestSettingsPreparationDoesNotPublishOrRewriteAnyScope()
        {
            Skin original = null!;
            SkinCurrentRevision revision = null!;
            Task<SkinSettingsPreview> preparing = null!;
            int sourceChanges = 0;

            AddStep("prepare a noncurrent settings object", () =>
            {
                original = manager.CurrentSkin.Value;
                revision = manager.CurrentRevision;
                manager.SourceChanged += () => sourceChanges++;
                preparing = manager.PrepareSettingsSkinAsync(bmsSeven);
            });
            AddUntilStep("settings snapshot prepared", () => preparing.IsCompleted);
            AddStep("verify separate owner and exact retained global", () =>
            {
                preview = preparing.GetAwaiter().GetResult();
                Assert.Multiple(() =>
                {
                    Assert.That(preview.Skin.SkinInfo.ID, Is.EqualTo(bmsSeven.ID));
                    Assert.That(preview.Skin, Is.Not.SameAs(original));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(original));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revision));
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(original.SkinInfo.ID));
                    Assert.That(sourceChanges, Is.Zero);
                    Assert.That(preferences.GetConfiguredSkinId(sevenScope), Is.EqualTo(bmsSeven.ID));
                    Assert.That(preferences.GetConfiguredSkinId(new SkinSelectionScope("bms")), Is.EqualTo(bmsDefault.ID));
                });
            });
            AddStep("dispose settings snapshot without deleting package", () =>
            {
                preview!.Dispose();
                preview = null;
                Assert.That(manager.Query(info => info.ID == bmsSeven.ID && !info.DeletePending), Is.Not.Null);
                Assert.That(manager.CurrentSkin.Value, Is.SameAs(original));
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestSettingsPreparationRechecksTargetAfterWaitingForDeletion(bool removeRecord)
        {
            Task<SkinSettingsPreview> preparing = null!;
            SkinCurrentRevision originalRevision = null!;
            AddStep("queue a settings read behind the deleting owner", () =>
            {
                originalRevision = manager.CurrentRevision;
                using SkinManagedFolderOperationCoordinator.Lease reservation = manager.ManagedFolderOperationCoordinator.EnterMutation();
                preparing = manager.PrepareSettingsSkinAsync(bmsSeven);
                Realm.Run(realm => realm.Write(() =>
                {
                    SkinInfo record = realm.Find<SkinInfo>(bmsSeven.ID)!;
                    if (removeRecord)
                        realm.Remove(record);
                    else
                        record.DeletePending = true;
                }));
            });
            AddUntilStep("stale read completes", () => preparing.IsCompleted);
            AddStep("reject the removed target without publishing or saving", () =>
            {
                Assert.Throws<SkinSettingsTargetUnavailableException>(() => preparing.GetAwaiter().GetResult());
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentRevision, Is.SameAs(originalRevision));
                    Assert.That(preferences.GetConfiguredSkinId(sevenScope), Is.EqualTo(bmsSeven.ID));
                    Assert.That(manager.GetConfiguredSkin(sevenScope).ID, Is.EqualTo(bmsDefault.ID));
                });
            });
        }

        [Test]
        public void TestActualKeyScopePublishesWithoutReplacingEitherModeDefault()
        {
            Task<bool> selection = null!;
            AddStep("apply stored seven-key scope", () => selection = manager.ApplySkinScopeAsync(sevenScope));
            AddUntilStep("scope transaction completes", () => selection.IsCompleted);
            AddStep("verify skin and sparse preferences", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(selection.GetAwaiter().GetResult(), Is.True);
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(bmsSeven.ID));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(bmsSeven.ID));
                    Assert.That(manager.CurrentSkinScope, Is.EqualTo(sevenScope));
                    Assert.That(preferences.GetConfiguredSkinId(new SkinSelectionScope("bms")), Is.EqualTo(bmsDefault.ID));
                    Assert.That(preferences.GetConfiguredSkinId(new SkinSelectionScope("mania")), Is.EqualTo(maniaDefault.ID));
                    Assert.That(manager.GetConfiguredSkin(new SkinSelectionScope("bms", 5)).ID, Is.EqualTo(bmsDefault.ID));
                });
            });
        }

        [Test]
        public void TestLiveGameplayRejectsExplicitScopeChangeWithoutSavingThenAllowsAfterDetach()
        {
            Skin original = null!;
            SkinCurrentRevision revision = null!;
            Task<bool> selection = null!;
            AddStep("attach actual live manager participant", () =>
            {
                original = manager.CurrentSkin.Value;
                revision = manager.CurrentRevision;
                liveParticipant = manager.RegisterRevisionParticipant(SkinRevisionParticipantKind.LiveGameplayHost, "scoped selection product test");
                selection = manager.SelectSkinForScopeAsync(sevenScope, maniaDefault);
            });
            AddUntilStep("live rejection completes", () => selection.IsCompleted);
            AddStep("verify exact original pair and assignment remain", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(selection.GetAwaiter().GetResult(), Is.False);
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(original));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revision));
                    Assert.That(preferences.GetConfiguredSkinId(sevenScope), Is.EqualTo(bmsSeven.ID));
                    Assert.That(manager.IsApplyingSkinScope, Is.False);
                });
                liveParticipant!.Dispose();
                liveParticipant = null;
                selection = manager.SelectSkinForScopeAsync(sevenScope, maniaDefault);
            });
            AddUntilStep("selection commits after detach", () => selection.IsCompleted);
            AddStep("only requested key assignment changes after successful pair", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(selection.GetAwaiter().GetResult(), Is.True);
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(maniaDefault.ID));
                    Assert.That(preferences.GetConfiguredSkinId(sevenScope), Is.EqualTo(maniaDefault.ID));
                    Assert.That(preferences.GetConfiguredSkinId(new SkinSelectionScope("bms")), Is.EqualTo(bmsDefault.ID));
                    Assert.That(preferences.GetConfiguredSkinId(new SkinSelectionScope("mania")), Is.EqualTo(maniaDefault.ID));
                });
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestFailedOrSupersededManagedPreparationCannotSaveItsScope(bool superseded)
        {
            var completions = new ConcurrentQueue<Action>();
            Task<bool> selection = null!;
            Skin expected = null!;
            SkinCurrentRevision expectedRevision = null!;
            AddStep("create a managed package through scanner and hold preparation completion", () =>
            {
                string directory = LocalStorage.GetFullPath("chartskin/scoped-candidate");
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "skin.ini"), "[General]\nName: Scope candidate\nAuthor: OMS tests\n");
                var scanner = new SkinManagedFolderScanner(Realm, new WindowsSkinManagedFolderDiscoverySource(LocalStorage), manager.ManagedFolderOperationCoordinator);
                Assert.That(scanner.Scan().IsSuccess, Is.True);
                Live<SkinInfo> candidate = manager.Query(info => info.FilesystemStoragePath == "chartskin/scoped-candidate");
                Assert.That(candidate, Is.Not.Null);
                manager.ManagedFolderCompletionSchedule = action => completions.Enqueue(action);
                if (!superseded)
                    manager.ManagedFolderCapture = (_, _) => SkinManagedPackageCaptureResult.Reject(SkinManagedPackageCaptureRejectionReason.SourceBusy);
                expected = manager.CurrentSkin.Value;
                expectedRevision = manager.CurrentRevision;
                selection = manager.SelectSkinForScopeAsync(sevenScope, candidate);
            });
            AddUntilStep("background preparation reaches held update callback", () => !completions.IsEmpty);
            AddStep("the pending candidate has not been published or saved", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(selection.IsCompleted, Is.False);
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(expected));
                    Assert.That(manager.CurrentRevision, Is.SameAs(expectedRevision));
                    Assert.That(preferences.GetConfiguredSkinId(sevenScope), Is.EqualTo(bmsSeven.ID));
                });
                if (superseded)
                {
                    manager.CurrentSkinInfo.Value = maniaDefault;
                    expected = manager.CurrentSkin.Value;
                    expectedRevision = manager.CurrentRevision;
                }
                Assert.That(completions.TryDequeue(out Action? callback), Is.True);
                callback!();
            });
            AddUntilStep("failed or superseded scope completes", () => selection.IsCompleted);
            AddStep("late callback cannot replace the winning pair or stored scope", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(selection.GetAwaiter().GetResult(), Is.False);
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(expected));
                    Assert.That(manager.CurrentRevision, Is.SameAs(expectedRevision));
                    Assert.That(preferences.GetConfiguredSkinId(sevenScope), Is.EqualTo(bmsSeven.ID));
                    Assert.That(preferences.GetConfiguredSkinId(new SkinSelectionScope("bms")), Is.EqualTo(bmsDefault.ID));
                    Assert.That(manager.IsApplyingSkinScope, Is.False);
                });
            });
        }

        [Test]
        public void TestAlreadyCurrentScopeSupersedesAHeldManagedCandidate()
        {
            var completions = new ConcurrentQueue<Action>();
            Task<bool> older = null!;
            Task<bool> newer = null!;
            Skin expected = null!;
            SkinCurrentRevision expectedRevision = null!;
            var latestScope = new SkinSelectionScope("mania", 4);
            AddStep("hold a prepared managed candidate for the old scope", () =>
            {
                string directory = LocalStorage.GetFullPath("chartskin/superseded-scoped-candidate");
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "skin.ini"), "[General]\nName: Superseded scope candidate\nAuthor: OMS tests\n");
                var scanner = new SkinManagedFolderScanner(Realm, new WindowsSkinManagedFolderDiscoverySource(LocalStorage), manager.ManagedFolderOperationCoordinator);
                Assert.That(scanner.Scan().IsSuccess, Is.True);
                Live<SkinInfo> candidate = manager.Query(info => info.FilesystemStoragePath == "chartskin/superseded-scoped-candidate");
                manager.ManagedFolderCompletionSchedule = action => completions.Enqueue(action);
                expected = manager.CurrentSkin.Value;
                expectedRevision = manager.CurrentRevision;
                older = manager.SelectSkinForScopeAsync(sevenScope, candidate);
            });
            AddUntilStep("old candidate reaches its held callback", () => !completions.IsEmpty);
            AddStep("select the already current skin for another scope before releasing the old callback", () =>
            {
                newer = manager.SelectSkinForScopeAsync(latestScope, manager.CurrentSkinInfo.Value);
                Assert.That(completions.TryDequeue(out Action? callback), Is.True);
                callback!();
            });
            AddUntilStep("both scope requests complete", () => older.IsCompleted && newer.IsCompleted);
            AddStep("the late candidate cannot replace the latest scope or either saved assignment", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(older.GetAwaiter().GetResult(), Is.False);
                    Assert.That(newer.GetAwaiter().GetResult(), Is.True);
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(expected));
                    Assert.That(manager.CurrentRevision, Is.SameAs(expectedRevision));
                    Assert.That(manager.CurrentSkinScope, Is.EqualTo(latestScope));
                    Assert.That(preferences.GetConfiguredSkinId(sevenScope), Is.EqualTo(bmsSeven.ID));
                    Assert.That(preferences.GetConfiguredSkinId(latestScope), Is.EqualTo(expected.SkinInfo.ID));
                    Assert.That(manager.IsApplyingSkinScope, Is.False);
                });
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestReentrantScopeCannotReplaceACommitWhoseObserversAreStillRunning(bool sourceChanged)
        {
            Task<bool> outer = null!;
            Task<bool>? inner = null;
            var rejectedScope = new SkinSelectionScope("mania", 4);
            AddStep("request another scope from a real committed observer", () =>
            {
                void reenter()
                {
                    if (manager.CurrentSkinInfo.Value.ID == maniaDefault.ID && inner == null)
                        inner = manager.SelectSkinForScopeAsync(rejectedScope, manager.CurrentSkinInfo.Value);
                }

                if (sourceChanged)
                    manager.SourceChanged += reenter;
                else
                    manager.CurrentSkin.BindValueChanged(_ => reenter());
                outer = manager.SelectSkinForScopeAsync(sevenScope, maniaDefault);
            });
            AddUntilStep("both observers finish their scope requests", () => outer.IsCompleted && inner?.IsCompleted == true);
            AddStep("only the committed outer pair and assignment survive", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(outer.GetAwaiter().GetResult(), Is.True);
                    Assert.That(inner!.GetAwaiter().GetResult(), Is.False);
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(maniaDefault.ID));
                    Assert.That(manager.CurrentSkinScope, Is.EqualTo(sevenScope));
                    Assert.That(preferences.GetConfiguredSkinId(sevenScope), Is.EqualTo(maniaDefault.ID));
                    Assert.That(preferences.GetConfiguredSkinId(rejectedScope), Is.Null);
                    Assert.That(manager.IsApplyingSkinScope, Is.False);
                });
            });
        }

        [Test]
        public void TestMissingKeyAssignmentFallsBackThroughItsOwnModeWithoutChangingOtherScopes()
        {
            AddStep("make only key override unavailable", () => bmsSeven.PerformWrite(info => info.DeletePending = true));
            AddAssert("seven-key scope falls back to BMS mode default", () => manager.GetConfiguredSkin(sevenScope).ID == bmsDefault.ID);
            AddStep("make mode default unavailable", () => bmsDefault.PerformWrite(info => info.DeletePending = true));
            AddAssert("missing mode falls back to protected ordinary package", () => manager.GetConfiguredSkin(sevenScope).ID == SkinInfo.OMS_SKIN);
            AddAssert("mania default remains independent", () => manager.GetConfiguredSkin(new SkinSelectionScope("mania", 7)).ID == maniaDefault.ID);
            AddAssert("reads do not rewrite unavailable assignments", () => preferences.GetConfiguredSkinId(sevenScope) == bmsSeven.ID);
        }

        [Test]
        public void TestCancellationPreventsPreparedCallbackAheadOfUpdateCleanupFromPublishingOrSaving()
        {
            var completions = new ConcurrentQueue<Action>();
            Task<bool> selection = null!;
            Skin expected = null!;
            SkinCurrentRevision expectedRevision = null!;
            int sourceChanges = 0;
            AddStep("prepare managed candidate and hold its completed callback", () =>
            {
                string directory = LocalStorage.GetFullPath("chartskin/cancelled-scoped-candidate");
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "skin.ini"), "[General]\nName: Cancelled scope candidate\nAuthor: OMS tests\n");
                var scanner = new SkinManagedFolderScanner(Realm, new WindowsSkinManagedFolderDiscoverySource(LocalStorage), manager.ManagedFolderOperationCoordinator);
                Assert.That(scanner.Scan().IsSuccess, Is.True);
                Live<SkinInfo> candidate = manager.Query(info => info.FilesystemStoragePath == "chartskin/cancelled-scoped-candidate");
                Assert.That(candidate, Is.Not.Null);
                manager.ManagedFolderCompletionSchedule = action => completions.Enqueue(action);
                manager.SourceChanged += () => sourceChanges++;
                expected = manager.CurrentSkin.Value;
                expectedRevision = manager.CurrentRevision;
                cancellation = new CancellationTokenSource();
                selection = manager.SelectSkinForScopeAsync(sevenScope, candidate, cancellation.Token);
            });
            AddUntilStep("native capture completes before cancellation", () => !completions.IsEmpty);
            AddStep("cancel then run prepared completion before queued cancellation cleanup", () =>
            {
                cancellation!.Cancel();
                Assert.That(completions.TryDequeue(out Action? callback), Is.True);
                callback!();
            });
            AddUntilStep("caller observes cancellation", () => selection.IsCompleted);
            AddStep("cancelled preparation cannot publish or persist a skin", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(selection.IsCanceled, Is.True);
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(expected));
                    Assert.That(manager.CurrentRevision, Is.SameAs(expectedRevision));
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(expected.SkinInfo.ID));
                    Assert.That(preferences.GetConfiguredSkinId(sevenScope), Is.EqualTo(bmsSeven.ID));
                    Assert.That(preferences.GetConfiguredSkinId(new SkinSelectionScope("bms")), Is.EqualTo(bmsDefault.ID));
                    Assert.That(sourceChanges, Is.Zero);
                    Assert.That(manager.IsApplyingSkinScope, Is.False);
                });
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestCancellationAfterCommitClaimReportsTheCommittedPairAndAssignment(bool alreadyCurrent)
        {
            Task<bool> selection = null!;
            int notifications = 0;
            AddStep("cancel from the committed notification while applying an explicit scope", () =>
            {
                cancellation = new CancellationTokenSource();
                if (alreadyCurrent)
                {
                    manager.CurrentSkinInfo.Value = maniaDefault;
                    preferences.Changed += () =>
                    {
                        notifications++;
                        cancellation.Cancel();
                    };
                }
                else
                {
                    manager.SourceChanged += () =>
                    {
                        Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(maniaDefault.ID));
                        notifications++;
                        cancellation.Cancel();
                    };
                }
                selection = manager.SelectSkinForScopeAsync(sevenScope, maniaDefault, cancellation.Token);
            });
            AddUntilStep("scope transaction completes", () => selection.IsCompleted);
            AddStep("committed result cannot be replaced by a late cancellation", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(cancellation!.IsCancellationRequested, Is.True);
                    Assert.That(notifications, Is.EqualTo(1));
                    Assert.That(selection.GetAwaiter().GetResult(), Is.True);
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(maniaDefault.ID));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(maniaDefault.ID));
                    Assert.That(manager.CurrentSkinScope, Is.EqualTo(sevenScope));
                    Assert.That(preferences.GetConfiguredSkinId(sevenScope), Is.EqualTo(maniaDefault.ID));
                    Assert.That(preferences.GetConfiguredSkinId(new SkinSelectionScope("bms")), Is.EqualTo(bmsDefault.ID));
                    Assert.That(manager.IsApplyingSkinScope, Is.False);
                });
            });
        }

        private Live<SkinInfo> createRecord(string name)
        {
            Guid id = Guid.NewGuid();
            Realm.Write(realm => realm.Add(new SkinInfo(name, "OMS tests", typeof(LegacySkin).AssemblyQualifiedName) { ID = id }));
            return manager.Query(info => info.ID == id);
        }
    }
}
