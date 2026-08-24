// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Screens.Menu;
using osu.Game.Skinning;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Navigation
{
    public partial class TestSceneEditDefaultSkin : OsuGameTestScene
    {
        private SkinManager skinManager => Game.Dependencies.Get<SkinManager>();
        private SkinEditorOverlay skinEditor => Game.Dependencies.Get<SkinEditorOverlay>();
        private RealmAccess realm => Game.Dependencies.Get<RealmAccess>();

        [Test]
        public void TestLegacyAuthoringAndExternalEditRemainFailClosedThroughRealGameGraph()
        {
            GlobalActionContainer globalActions = null!;
            ButtonSystem buttonSystem = null!;
            ExternalEditOverlay externalEditOverlay = null!;
            Live<SkinInfo> selectionA = null!;
            Skin ownerA = null!;
            SkinCurrentRevision revisionA = null!;
            CurrentSkinRecordSnapshot recordA = default;
            string realmA = string.Empty;
            Task<Task> beginExternalEdit = null!;

            AddUntilStep("wait for real main-menu authoring graph", () =>
            {
                globalActions = Game.ChildrenOfType<GlobalActionContainer>().SingleOrDefault()!;
                buttonSystem = Game.ChildrenOfType<ButtonSystem>().SingleOrDefault()!;
                externalEditOverlay = Game.ChildrenOfType<ExternalEditOverlay>().SingleOrDefault()!;

                return globalActions?.IsLoaded == true
                       && buttonSystem?.IsLoaded == true
                       && skinEditor.IsLoaded
                       && externalEditOverlay?.IsLoaded == true
                       && Game.Notifications.IsLoaded;
            });
            AddStep("capture exact protected fallback authority", () =>
            {
                selectionA = skinManager.CurrentSkinInfo.Value;
                ownerA = skinManager.CurrentSkin.Value;
                revisionA = skinManager.CurrentRevision;
                recordA = CurrentSkinRecordSnapshot.Capture(selectionA);
                realmA = captureRealmSkinRecords();

                Assert.Multiple(() =>
                {
                    Assert.That(selectionA.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(ownerA, Is.SameAs(skinManager.DefaultOmsSkin));
                    Assert.That(revisionA.Owner, Is.SameAs(ownerA));
                    Assert.That(revisionA.SourceKind, Is.EqualTo(SkinCurrentRevisionSourceKind.ProtectedFallback));
                });
            });
            AddStep("assert real main menu has no skin-editor affordance", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(buttonSystem.SkinEditorEnabled, Is.False);
                    Assert.That(
                        buttonSystem.ChildrenOfType<MainMenuButton>()
                                    .Any(button => button.VisibleStateMin == ButtonSystemState.Edit
                                                   && button.VisibleStateMax == ButtonSystemState.Edit
                                                   && button.TriggerKeys.Contains(Key.S)),
                        Is.False);
                });
            });
            AddStep("trigger real global skin-editor action", () =>
                globalActions.TriggerPressed(GlobalAction.ToggleSkinEditor));
            AddUntilStep("wait for stable unavailable notification", () =>
                Game.Notifications.AllNotifications.Any(notification =>
                    notification.Text.ToString() == SkinSettingsStrings.SkinAuthoringUnavailable.ToString()));
            AddStep("assert global action did not expose or mutate authoring", () =>
            {
                Assert.That(skinEditor.State.Value, Is.EqualTo(Visibility.Hidden));
                assertExactAuthorityUnchanged(selectionA, ownerA, revisionA, recordA, realmA);
            });
            AddStep("invoke indirect overlay Show caller", () => skinEditor.Show());
            AddUntilStep("wait for indirect overlay rejection", () =>
                skinEditor.State.Value == Visibility.Hidden);
            AddStep("assert direct Show did not create a mutable skin", () =>
                assertExactAuthorityUnchanged(selectionA, ownerA, revisionA, recordA, realmA));
            AddStep("begin external edit through real registered overlay", () =>
            {
                SkinInfo detachedCurrent = selectionA.PerformRead(info => info.Detach());
                beginExternalEdit = externalEditOverlay.Begin(detachedCurrent);

                Assert.That(beginExternalEdit.IsCompletedSuccessfully, Is.True,
                    "The UI gate must reject synchronously, before the legacy delayed mount path.");
            });
            AddStep("assert external edit returned the stable fault before mount", () =>
            {
                Task rejectedOperation = beginExternalEdit.GetAwaiter().GetResult();
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                    rejectedOperation.GetAwaiter().GetResult())!;

                Assert.Multiple(() =>
                {
                    Assert.That(rejectedOperation.IsFaulted, Is.True);
                    Assert.That(exception.Message, Is.EqualTo(ExternalEditOverlay.EXTERNAL_EDITING_DISABLED_DIAGNOSTIC));
                    Assert.That(skinManager.CurrentSkin.Disabled, Is.False);
                    Assert.That(externalEditOverlay.ChildrenOfType<Graphics.Sprites.OsuSpriteText>()
                                                           .Any(text => text.Text.ToString()
                                                               == SkinEditorStrings.ExternalEditingUnavailable.ToString()),
                        Is.True);
                });

                assertExactAuthorityUnchanged(selectionA, ownerA, revisionA, recordA, realmA);
            });
            AddUntilStep("wait for unavailable overlay to close", () =>
                externalEditOverlay.State.Value == Visibility.Hidden);
            AddStep("assert delayed close did not activate update-import", () =>
                assertExactAuthorityUnchanged(selectionA, ownerA, revisionA, recordA, realmA));
        }

        private void assertExactAuthorityUnchanged(
            Live<SkinInfo> selection,
            Skin owner,
            SkinCurrentRevision revision,
            CurrentSkinRecordSnapshot record,
            string realmSnapshot)
        {
            Assert.Multiple(() =>
            {
                Assert.That(skinManager.CurrentSkinInfo.Value, Is.SameAs(selection));
                Assert.That(skinManager.CurrentSkin.Value, Is.SameAs(owner));
                Assert.That(skinManager.CurrentRevision, Is.SameAs(revision));
                Assert.That(skinManager.CurrentRevision.Owner, Is.SameAs(owner));
                Assert.That(skinManager.CurrentRevision.RecordId, Is.EqualTo(record.Id));
                Assert.That(skinManager.CurrentRevision.ContentRevision, Is.EqualTo(revision.ContentRevision));
                Assert.That(skinManager.CurrentRevision.SourceKind, Is.EqualTo(revision.SourceKind));
                Assert.That(revision.Retired.IsCompleted, Is.False);
                Assert.That(CurrentSkinRecordSnapshot.Capture(selection), Is.EqualTo(record));
                Assert.That(captureRealmSkinRecords(), Is.EqualTo(realmSnapshot));
            });
        }

        private string captureRealmSkinRecords()
            => realm.Run(r => string.Join(
                '\n',
                r.All<SkinInfo>()
                 .ToArray()
                 .OrderBy(info => info.ID)
                 .Select(info =>
                     $"{info.ID:N}|{info.Name}|{info.Creator}|{info.InstantiationInfo}|{info.Hash}|{info.Protected}|{info.DeletePending}|"
                     + $"{info.FilesystemStoragePath}|{info.IsExternalFilesystemStorage}|{info.FilesystemStorageAuthorityOwner}|"
                     + string.Join(',', info.Files
                                            .OrderBy(file => file.Filename, StringComparer.Ordinal)
                                            .Select(file => $"{file.Filename}:{file.File.Hash}")))));

        private readonly record struct CurrentSkinRecordSnapshot(
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
            int FileCount)
        {
            public static CurrentSkinRecordSnapshot Capture(Live<SkinInfo> selection)
                => selection.PerformRead(info => new CurrentSkinRecordSnapshot(
                    info.ID,
                    info.Name,
                    info.Creator,
                    info.InstantiationInfo,
                    info.Hash,
                    info.Protected,
                    info.DeletePending,
                    info.FilesystemStoragePath,
                    info.IsExternalFilesystemStorage,
                    info.FilesystemStorageAuthorityOwner,
                    info.Files.Count));
        }
    }
}
