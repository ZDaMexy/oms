// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Database;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Overlays.Settings.Sections.Maintenance;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    [TestFixture]
    public class FolderSkinWorkspaceUiTest
    {
        [Test]
        public void DeleteDialogRetainsDetachedIdAndImmutableLabel()
        {
            Guid recordId = Guid.NewGuid();
            const string label = "detached confirmation label";

            var dialog = new SkinSection.SkinDeleteDialog(recordId, label);

            Assert.Multiple(() =>
            {
                Assert.That(dialog.RecordId, Is.EqualTo(recordId));
                Assert.That(dialog.BodyText.ToString(), Is.EqualTo(label));
                Assert.That(declaredFieldsOf(dialog).Any(f => typeof(Skin).IsAssignableFrom(f.FieldType)), Is.False,
                    "A confirmation dialog must not retain a live Skin instance.");
            });
        }

        [Test]
        public void WorkspaceRowCopiesOnlyPathFreeProjection()
        {
            var row = createRow(new FolderSkinWorkspaceRecord(
                Guid.NewGuid(),
                "external author workspace",
                FolderSkinWorkspaceRecordKind.External,
                canOpenFolder: true,
                canImportManagedCopy: true,
                canUnregister: true,
                canRename: false,
                canDelete: false));

            FieldInfo[] fields = declaredFieldsOf(row);

            Assert.Multiple(() =>
            {
                Assert.That(fields.Any(f => f.FieldType == typeof(FolderSkinWorkspaceRecord)), Is.False,
                    "Rows must copy the DTO's detached fields rather than retaining the DTO.");
                Assert.That(fields.Any(f => typeof(Skin).IsAssignableFrom(f.FieldType)), Is.False);
                Assert.That(fields.Any(f => f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(Live<>)), Is.False);
                Assert.That(fields.Any(f => f.Name.Contains("path", StringComparison.OrdinalIgnoreCase)), Is.False,
                    "The UI row contract is deliberately path-free.");
                Assert.That(row.DisplayLabel, Is.EqualTo("external author workspace"));
                Assert.That(row.Kind, Is.EqualTo(FolderSkinWorkspaceRecordKind.External));
            });
        }

        [Test]
        public void WorkspaceRowsExposeKindSpecificCapabilities()
        {
            var external = createRow(new FolderSkinWorkspaceRecord(
                Guid.NewGuid(),
                "external",
                FolderSkinWorkspaceRecordKind.External,
                canOpenFolder: false,
                canImportManagedCopy: true,
                canUnregister: false,
                canRename: false,
                canDelete: false));

            var managed = createRow(new FolderSkinWorkspaceRecord(
                Guid.NewGuid(),
                "managed",
                FolderSkinWorkspaceRecordKind.Managed,
                canOpenFolder: true,
                canImportManagedCopy: false,
                canUnregister: false,
                canRename: false,
                canDelete: true));

            external.SetInteractionEnabled(true);
            managed.SetInteractionEnabled(true);

            Assert.Multiple(() =>
            {
                Assert.That(external.ActionButtons, Has.Count.EqualTo(3), "External rows expose open/import/unregister.");
                Assert.That(external.ActionButtons.Select(b => b.Enabled.Value), Is.EqualTo(new[] { false, true, false }));
                Assert.That(managed.ActionButtons, Has.Count.EqualTo(3), "Managed rows expose open/rename/delete.");
                Assert.That(managed.ActionButtons.Select(b => b.Enabled.Value), Is.EqualTo(new[] { true, false, true }));
            });

            external.SetInteractionEnabled(false);
            managed.SetInteractionEnabled(false);

            Assert.Multiple(() =>
            {
                Assert.That(external.ActionButtons.All(b => !b.Enabled.Value), Is.True);
                Assert.That(managed.ActionButtons.All(b => !b.Enabled.Value), Is.True);
            });
        }

        [Test]
        public void DirectRowActionsUseOnlyDetachedIdAndLabel()
        {
            Guid externalId = Guid.NewGuid();
            Guid managedId = Guid.NewGuid();
            Guid? opened = null;
            (Guid Id, string Label)? unregistered = null;
            (Guid Id, string Label)? deleted = null;

            var external = createRow(
                new FolderSkinWorkspaceRecord(externalId, "external label", FolderSkinWorkspaceRecordKind.External, true, true, true, false, false),
                open: id => opened = id,
                unregister: (id, label) => unregistered = (id, label));
            var managed = createRow(
                new FolderSkinWorkspaceRecord(managedId, "managed label", FolderSkinWorkspaceRecordKind.Managed, true, false, false, true, true),
                delete: (id, label) => deleted = (id, label));

            external.ActionButtons[0].Action?.Invoke();
            external.ActionButtons[2].Action?.Invoke();
            managed.ActionButtons[2].Action?.Invoke();

            Assert.Multiple(() =>
            {
                Assert.That(opened, Is.EqualTo(externalId));
                Assert.That(unregistered, Is.EqualTo((externalId, "external label")));
                Assert.That(deleted, Is.EqualTo((managedId, "managed label")));
            });
        }

        [Test]
        public void FolderPickerUsesDirectorySelectScreen()
        {
            FieldInfo? selectorField = typeof(DirectorySelectScreen).GetField("directorySelector", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.Multiple(() =>
            {
                Assert.That(new FolderSkinDirectorySelectScreen(), Is.InstanceOf<DirectorySelectScreen>());
                Assert.That(selectorField?.FieldType, Is.EqualTo(typeof(OsuDirectorySelector)),
                    "The shared directory-selection screen is backed by the real OsuDirectorySelector.");
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FolderNamePopoverStartsEmptyAndForwardsUntrimmedInput(bool external)
        {
            const string display_label = "immutable display label";
            const string submitted_name = "  target folder  ";
            string? submitted = null;
            FolderSkinWorkspaceRecordKind kind = external
                ? FolderSkinWorkspaceRecordKind.External
                : FolderSkinWorkspaceRecordKind.Managed;
            var row = createRow(
                new FolderSkinWorkspaceRecord(
                    Guid.NewGuid(),
                    display_label,
                    kind,
                    canOpenFolder: true,
                    canImportManagedCopy: external,
                    canUnregister: external,
                    canRename: !external,
                    canDelete: !external),
                import: (_, name) => submitted = name,
                rename: (_, name) => submitted = name);

            var action = (IHasPopover)row.ActionButtons[1];
            Popover popover = action.GetPopover()
                                ?? throw new AssertionException("Expected folder-name action to provide a popover.");
            var textBox = (FocusedTextBox)getDeclaredField(popover, "textBox").GetValue(popover)!;
            var submitButton = (RoundedButton)getDeclaredField(popover, "submitButton").GetValue(popover)!;

            Assert.Multiple(() =>
            {
                Assert.That(row.DisplayLabel, Is.EqualTo(display_label));
                Assert.That(textBox.Text, Is.Empty, "The immutable row label is display copy, not a target-name default.");
                Assert.That(submitButton.Text.ToString(), Is.Not.EqualTo(display_label));
            });

            textBox.Text = submitted_name;
            submitButton.Action?.Invoke();

            Assert.That(submitted, Is.EqualTo(submitted_name), "UI must not silently trim an authority-sensitive folder name.");
        }

        private static FolderSkinWorkspace.FolderSkinWorkspaceRow createRow(
            FolderSkinWorkspaceRecord record,
            Action<Guid>? open = null,
            Action<Guid, string>? import = null,
            Action<Guid, string>? unregister = null,
            Action<Guid, string>? rename = null,
            Action<Guid, string>? delete = null)
            => new FolderSkinWorkspace.FolderSkinWorkspaceRow(
                record,
                open ?? (_ => { }),
                import ?? ((_, _) => { }),
                unregister ?? ((_, _) => { }),
                rename ?? ((_, _) => { }),
                delete ?? ((_, _) => { }));

        private static FieldInfo[] declaredFieldsOf(object value)
            => value.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        private static FieldInfo getDeclaredField(object value, string name)
            => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
               ?? throw new AssertionException($"Missing expected UI field {name}.");
    }
}
