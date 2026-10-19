// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Logging;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Rulesets.Bms;
using osu.Game.Screens.Select;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Settings
{
    /// <summary>Uses the real OsuGame selection subscriber so protected projections cannot rewrite saved scopes.</summary>
    [HeadlessTest]
    [Platform("Win")]
    public partial class TestSceneSkinScopedDeletion : OsuGameTestScene
    {
        private SkinManager skins => Game.Dependencies.Get<SkinManager>();
        private SkinSelectionPreferences preferences => skins.SelectionPreferences!;
        private readonly SkinSelectionScope scope = new SkinSelectionScope("bms", 7);
        private readonly SkinSelectionScope mode = new SkinSelectionScope("bms");
        private Live<SkinInfo> modeDefault = null!;
        private Live<SkinInfo> keySkin = null!;
        private Guid keySkinId;
        private string savedAssignments = string.Empty;
        private string managedDirectory = string.Empty;
        private SkinSection section => Game.Settings.SectionsContainer.ChildrenOfType<SkinSection>().Single();
        private readonly ConcurrentQueue<string> deletionLog = new();
        private readonly ConcurrentQueue<Exception> deletionErrors = new();

        [TearDown]
        public void TearDownDeletionLog() => Logger.NewEntry -= captureDeletionLog;

        private void captureDeletionLog(LogEntry entry)
        {
            deletionLog.Enqueue(entry.Message + (entry.Exception == null ? string.Empty : "\n" + entry.Exception));
            if (entry.Exception != null)
                deletionErrors.Enqueue(entry.Exception);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestConfirmingCurrentSkinDeletionFromSongSelectSettings(bool managed)
        {
            var timer = new Stopwatch();
            arrange(managed);
            confirmCurrentDeletionFromSongSelectSettings();
            AddStep("start bounded deletion observation", timer.Start);
            AddUntilStep("skin is deleted", () =>
            {
                bool deleted = managed ? skins.Query(info => info.ID == keySkinId) == null : keySkin.PerformRead(info => info.DeletePending);
                if (!deleted && timer.Elapsed.TotalSeconds > 8)
                    Assert.Fail(deletionState());
                return deleted;
            });
            AddStep("restart bounded card observation", timer.Restart);
            AddUntilStep("card follows its mode default", () =>
            {
                bool ready = section.IsTargetReady && section.DisplayedSkinId == modeDefault.ID;
                if (!ready && timer.Elapsed.TotalSeconds > 8)
                    Assert.Fail(deletionState());
                return ready;
            });
            AddAssert("saved assignments survive deletion", () => Game.LocalConfig.Get<string>(OsuSetting.SkinKeyOverrides) == savedAssignments
                && preferences.GetConfiguredSkinId(mode) == modeDefault.ID);
            AddAssert("protected runtime fallback is coherent", () => skins.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN
                && ReferenceEquals(skins.CurrentSkin.Value, skins.DefaultOmsSkin));
            if (managed)
                AddAssert("managed test directory was removed", () => !Directory.Exists(managedDirectory));
            AddStep("deletion and card refresh raised no errors", () => Assert.That(deletionErrors, Is.Empty, deletionState()));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestPopulatedSongSelectLayoutKeepsDeletionSafelyRejected(bool managed)
        {
            Task<bool> deletion = null!;
            SkinnableContainer layout = null!;
            SkinCurrentRevision originalRevision = null!;
            arrange(managed);
            PushAndConfirm(() => new SoloSongSelect());
            AddUntilStep("song select layout is ready", () => Game.ChildrenOfType<SkinnableContainer>().SingleOrDefault()?.ComponentsLoaded == true);
            AddStep("populate the actual layout", () =>
            {
                originalRevision = skins.CurrentRevision;
                layout = Game.ChildrenOfType<SkinnableContainer>().Single();
                layout.Reload(new Container { Child = new Box() });
            });
            AddUntilStep("the populated layout is loaded", () => layout.ComponentsLoaded);
            AddStep("request deletion with an unsupported populated layout", () => deletion = skins.DeleteSkinAsync(keySkinId));
            AddUntilStep("deletion is safely rejected", () => deletion.IsCompleted);
            AddStep("the original package and layout remain intact", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(deletion.GetAwaiter().GetResult(), Is.False);
                    Assert.That(skins.CurrentRevision, Is.SameAs(originalRevision));
                    Assert.That(skins.CurrentSkinInfo.Value.ID, Is.EqualTo(keySkinId));
                    Assert.That(skins.Query(info => info.ID == keySkinId && !info.DeletePending), Is.Not.Null);
                    Assert.That(layout.ChildrenOfType<Box>().Count(), Is.EqualTo(1));
                    Assert.That(Game.LocalConfig.Get<string>(OsuSetting.SkinKeyOverrides), Is.EqualTo(savedAssignments));
                    if (managed)
                        Assert.That(Directory.Exists(managedDirectory), Is.True);
                });
            });
        }

        [Test]
        public void TestLayoutChangeAfterPreparationCannotReleaseTheOriginalSkin()
        {
            Task<bool> deletion = null!;
            SkinCurrentRevision originalRevision = null!;
            SkinnableContainer layout = null!;
            Action<Action> originalSchedule = null!;
            var callbacks = new ConcurrentQueue<Action>();
            arrange(managed: false);
            PushAndConfirm(() => new SoloSongSelect());
            AddUntilStep("empty layout is ready", () => Game.ChildrenOfType<SkinnableContainer>().SingleOrDefault()?.ComponentsLoaded == true);
            AddStep("hold the actual prepared publication", () =>
            {
                originalRevision = skins.CurrentRevision;
                layout = Game.ChildrenOfType<SkinnableContainer>().Single();
                originalSchedule = skins.CurrentRevisionCompletionSchedule;
                skins.CurrentRevisionCompletionSchedule = callbacks.Enqueue;
                deletion = skins.DeleteSkinAsync(keySkinId);
            });
            AddUntilStep("the prepared commit is queued", () => !callbacks.IsEmpty);
            AddStep("change the layout before commit", () => layout.Reload(new Container { Child = new Box() }));
            AddUntilStep("new content is loaded", () => layout.ComponentsLoaded);
            AddStep("allow the held commit", () =>
            {
                skins.CurrentRevisionCompletionSchedule = originalSchedule;
                while (callbacks.TryDequeue(out Action? callback))
                    callback();
            });
            AddUntilStep("deletion rejects the stale empty proof", () => deletion.IsCompleted);
            AddStep("the current skin and files remain available", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(deletion.GetAwaiter().GetResult(), Is.False);
                    Assert.That(skins.CurrentRevision, Is.SameAs(originalRevision));
                    Assert.That(keySkin.PerformRead(info => info.DeletePending), Is.False);
                    Assert.That(layout.ChildrenOfType<Box>().Count(), Is.EqualTo(1));
                });
            });
        }

        private void confirmCurrentDeletionFromSongSelectSettings()
        {
            SkinSection.DeleteSkinButton deleteButton = null!;
            SkinSection.SkinDeleteDialog dialog = null!;
            PushAndConfirm(() => new SoloSongSelect());
            AddStep("open settings from song select", () => Game.Settings.Show());
            AddUntilStep("skin settings loaded", () => Game.Settings.SectionsContainer.ChildrenOfType<SkinSection>().SingleOrDefault()?.IsLoaded == true);
            AddStep("inspect the current seven-key skin", () =>
            {
                section.ScopeSelector.Current.Value = scope;
                Game.Settings.SectionsContainer.ScrollTo(section);
            });
            AddUntilStep("current skin and delete action ready", () => section.IsTargetReady && section.DisplayedSkinId == keySkinId
                && section.ChildrenOfType<SkinSection.DeleteSkinButton>().Single().IsLoaded);
            AddStep("scroll to delete", () =>
            {
                deleteButton = section.ChildrenOfType<SkinSection.DeleteSkinButton>().Single();
                Game.Settings.SectionsContainer.ScrollTo(deleteButton);
            });
            AddUntilStep("delete action is visible", () => deleteButton.Enabled.Value
                && !(bool)typeof(Drawable).GetProperty("IsMaskedAway", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(deleteButton)!);
            AddStep("click delete on the actual card", () =>
            {
                InputManager.MoveMouseTo(deleteButton);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("confirmation is ready", () =>
            {
                dialog = Game.ChildrenOfType<SkinSection.SkinDeleteDialog>().SingleOrDefault()!;
                return dialog?.RecordId == keySkinId && dialog.State.Value == Visibility.Visible
                    && dialog.ChildrenOfType<PopupDialogDangerousButton>().Single().IsLoaded;
            });
            AddStep("hold the real confirmation button", () =>
            {
                deletionLog.Clear();
                deletionErrors.Clear();
                Logger.NewEntry += captureDeletionLog;
                dialog.FinishTransforms(true);
                InputManager.MoveMouseTo(dialog.ChildrenOfType<PopupDialogDangerousButton>().Single());
                InputManager.PressButton(MouseButton.Left);
            });
            AddUntilStep("confirmation completes", () => dialog.State.Value == Visibility.Hidden);
            AddStep("release confirmation", () => InputManager.ReleaseButton(MouseButton.Left));
        }

        private string deletionState()
            => $"currentIsOriginal={skins.CurrentSkinInfo.Value.ID == keySkinId}; cardReady={section.IsTargetReady}; cardIsOriginal={section.DisplayedSkinId == keySkinId}; "
               + $"configuredIsModeDefault={skins.GetConfiguredSkin(scope).ID == modeDefault.ID}; managedResult={skins.LastManagedFolderDeleteResult?.Status}; "
               + $"notifications={string.Join(" | ", Game.Notifications.AllNotifications.Select(notification => notification.Text.ToString()))}\n"
               + string.Join("\n", deletionLog.TakeLast(25));

        [TestCase(false)]
        [TestCase(true)]
        public void TestShutdownImmediatelyAfterDeleteFallbackJoinsAndPreservesThePackage(bool managed)
        {
            Task<bool> deletion = null!;
            SkinCurrentRevision originalRevision = null!;
            Action? commit = null;
            Action<Action> originalSchedule = null!;
            arrange(managed);
            PushAndConfirm(() => new SoloSongSelect());
            AddUntilStep("song select layout is ready", () => Game.ChildrenOfType<SkinnableContainer>().SingleOrDefault()?.ComponentsLoaded == true);
            AddStep("hold the deletion fallback before commit", () =>
            {
                originalRevision = skins.CurrentRevision;
                originalSchedule = skins.CurrentRevisionCompletionSchedule;
                skins.CurrentRevisionCompletionSchedule = callback => System.Threading.Volatile.Write(ref commit, callback);
                deletion = skins.DeleteSkinAsync(keySkinId);
            });
            AddUntilStep("the fallback is prepared", () => System.Threading.Volatile.Read(ref commit) != null);
            AddStep("allow fallback admission", () =>
            {
                if (managed)
                    System.Threading.Interlocked.Exchange(ref commit, null)!();
            });
            if (managed)
                AddUntilStep("the actual fallback commit is prepared", () => System.Threading.Volatile.Read(ref commit) != null);
            AddStep("commit and close in the same update callback", () =>
            {
                skins.CurrentRevisionCompletionSchedule = originalSchedule;
                System.Threading.Interlocked.Exchange(ref commit, null)!();
                Assert.That(skins.CurrentSkin.Value, Is.SameAs(skins.DefaultOmsSkin));
                Assert.That(deletion.IsCompleted, Is.False);
                skins.ShutdownManagedFolderMutations();
            });
            AddStep("shutdown joined the real mutation and restored the undeleted choice", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(deletion.IsCompleted, Is.True);
                    Assert.That(deletion.GetAwaiter().GetResult(), Is.False);
                    Assert.That(skins.CurrentRevision, Is.SameAs(originalRevision));
                    Assert.That(skins.Query(info => info.ID == keySkinId && !info.DeletePending), Is.Not.Null);
                    Assert.That(Game.LocalConfig.Get<string>(OsuSetting.SkinKeyOverrides), Is.EqualTo(savedAssignments));
                    if (managed)
                        Assert.That(Directory.Exists(managedDirectory), Is.True);
                });
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestDeletingCurrentKeySkinPreservesAssignmentsAndFollowsModeDefault(bool managed)
        {
            Task<bool> deletion = null!;
            arrange(managed);
            AddStep("delete the current key-count skin through the manager", () => deletion = skins.DeleteSkinAsync(keySkinId));
            AddUntilStep("current deletion and protected transition complete", () => deletion.IsCompleted);
            AddStep("stored key skin remains distinct from the protected runtime projection", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(deletion.GetAwaiter().GetResult(), Is.True);
                    Assert.That(skins.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(skins.CurrentSkin.Value, Is.SameAs(skins.DefaultOmsSkin));
                    Assert.That(skins.CurrentSkinScope, Is.EqualTo(scope));
                    Assert.That(preferences.GetConfiguredSkinId(scope), Is.EqualTo(keySkinId));
                    Assert.That(preferences.GetConfiguredSkinId(mode), Is.EqualTo(modeDefault.ID));
                    Assert.That(Game.LocalConfig.Get<string>(OsuSetting.SkinKeyOverrides), Is.EqualTo(savedAssignments));
                    Assert.That(skins.GetConfiguredSkin(scope).ID, Is.EqualTo(modeDefault.ID));
                    Assert.That(skins.GetConfiguredSkin(new SkinSelectionScope("bms", 5)).ID, Is.EqualTo(modeDefault.ID));
                    Assert.That(preferences.GetConfiguredSkinId(new SkinSelectionScope("mania")), Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(skins.IsProjectingProtectedSkinTransition, Is.False);
                });
                if (managed)
                {
                    Assert.That(skins.Query(info => info.ID == keySkinId), Is.Null);
                    Assert.That(Directory.Exists(managedDirectory), Is.False);
                }
                else
                    Assert.That(keySkin.PerformRead(info => info.DeletePending), Is.True);
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestFailedRealmDeleteKeepsAssignmentsDuringFallbackAndRestoresExactOriginal(bool fromSongSelect)
        {
            Task<bool> deletion = null!;
            Skin original = null!;
            SkinCurrentRevision originalRevision = null!;
            string? assignmentsAtFallback = null;
            Guid? modeAtFallback = null;
            bool protectedPairObserved = false;
            arrange(managed: false);
            if (fromSongSelect)
                PushAndConfirm(() => new SoloSongSelect());
            AddStep("fail the existing Realm commit after the real protected fallback", () =>
            {
                original = skins.CurrentSkin.Value;
                originalRevision = skins.CurrentRevision;
                skins.CurrentRealmPackageDeleteBeforeRealmCommit = () =>
                {
                    protectedPairObserved = skins.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN
                                            && ReferenceEquals(skins.CurrentSkin.Value, skins.DefaultOmsSkin);
                    assignmentsAtFallback = Game.LocalConfig.Get<string>(OsuSetting.SkinKeyOverrides);
                    modeAtFallback = preferences.GetConfiguredSkinId(mode);
                    throw new IOException("test-only failure before the ordinary deletion Realm commit");
                };
                deletion = skins.DeleteSkinAsync(keySkinId);
            });
            AddUntilStep("failed deletion restores its exact original pair", () => deletion.IsCompleted);
            AddStep("fallback and rollback never replace the player's saved choice", () =>
            {
                try
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(deletion.GetAwaiter().GetResult(), Is.False);
                        Assert.That(protectedPairObserved, Is.True);
                        Assert.That(assignmentsAtFallback, Is.EqualTo(savedAssignments));
                        Assert.That(modeAtFallback, Is.EqualTo(modeDefault.ID));
                        Assert.That(skins.CurrentSkin.Value, Is.SameAs(original));
                        Assert.That(skins.CurrentRevision, Is.SameAs(originalRevision));
                        Assert.That(skins.CurrentSkinInfo.Value.ID, Is.EqualTo(keySkinId));
                        Assert.That(keySkin.PerformRead(info => info.DeletePending), Is.False);
                        Assert.That(preferences.GetConfiguredSkinId(scope), Is.EqualTo(keySkinId));
                        Assert.That(preferences.GetConfiguredSkinId(mode), Is.EqualTo(modeDefault.ID));
                        Assert.That(Game.LocalConfig.Get<string>(OsuSetting.SkinKeyOverrides), Is.EqualTo(savedAssignments));
                        Assert.That(skins.GetConfiguredSkin(scope).ID, Is.EqualTo(keySkinId));
                        Assert.That(skins.IsProjectingProtectedSkinTransition, Is.False);
                    });
                }
                finally
                {
                    skins.CurrentRealmPackageDeleteBeforeRealmCommit = () => { };
                }
            });
        }

        private void arrange(bool managed)
        {
            Task<Live<SkinInfo>?> importing = null!;
            Task<SkinManagedFolderScanResult>? scanning = null;
            Task<bool> selection = null!;
            AddStep("import the independent BMS mode default", () => importing = skins.Import(new ImportTask(archive("Deletion mode default A"), "mode-a.osk")));
            AddUntilStep("mode default import completes", () => importing.IsCompleted);
            AddStep("prepare the distinct per-key package", () =>
            {
                modeDefault = importing.GetAwaiter().GetResult()!;
                if (managed)
                {
                    managedDirectory = Game.Storage.GetFullPath("chartskin/scoped-delete-b");
                    Directory.CreateDirectory(managedDirectory);
                    File.WriteAllText(Path.Combine(managedDirectory, "skin.ini"), skinIni("Deletion key skin B"), new UTF8Encoding(false));
                    scanning = Game.RefreshManagedSkinFoldersAsync();
                }
                else
                    importing = skins.Import(new ImportTask(archive("Deletion key skin B"), "key-b.osk"));
            });
            AddUntilStep("key package import or actual game discovery completes", () => managed ? scanning!.IsCompleted : importing.IsCompleted);
            AddStep("assign mode A and key B without changing the mode default", () =>
            {
                if (managed)
                {
                    Assert.That(scanning!.GetAwaiter().GetResult().IsSuccess, Is.True);
                    keySkin = skins.Query(info => info.FilesystemStoragePath == "chartskin/scoped-delete-b");
                    Assert.That(keySkin, Is.Not.Null);
                }
                else
                    keySkin = importing.GetAwaiter().GetResult()!;
                keySkinId = keySkin.ID;
                Game.Ruleset.Value = new BmsRuleset().RulesetInfo;
                preferences.SetSkin(mode, modeDefault.ID);
                preferences.SetSkin(new SkinSelectionScope("mania"), SkinInfo.OMS_SKIN);
                preferences.SetSkin(scope, keySkinId);
                preferences.SetSkin(new SkinSelectionScope("bms", 14), keySkinId);
                savedAssignments = Game.LocalConfig.Get<string>(OsuSetting.SkinKeyOverrides);
                selection = skins.ApplySkinScopeAsync(scope);
            });
            AddUntilStep("real scoped selection has completed", () => selection.IsCompleted);
            AddStep("verify current B and separate stored mode A", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(selection.GetAwaiter().GetResult(), Is.True);
                    Assert.That(skins.CurrentSkinInfo.Value.ID, Is.EqualTo(keySkinId));
                    Assert.That(skins.CurrentSkinScope, Is.EqualTo(scope));
                    Assert.That(preferences.GetConfiguredSkinId(scope), Is.EqualTo(keySkinId));
                    Assert.That(preferences.GetConfiguredSkinId(mode), Is.EqualTo(modeDefault.ID));
                });
                DismissAnyNotifications();
            });
        }

        private static string skinIni(string name)
            => $"[General]\nName: {name}\nAuthor: OMS tests\nVersion: 2.7\n\n[Bms]\nKeymode: 7K\n";

        private static MemoryStream archive(string name)
        {
            var output = new MemoryStream();
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            using (var writer = new StreamWriter(zip.CreateEntry("skin.ini").Open(), new UTF8Encoding(false)))
                writer.Write(skinIni(name));
            output.Position = 0;
            return output;
        }
    }
}
