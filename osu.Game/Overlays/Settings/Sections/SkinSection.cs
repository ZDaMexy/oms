// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets;
using osu.Game.Skinning;
using osuTK;
using Realms;
using WebCommonStrings = osu.Game.Resources.Localisation.Web.CommonStrings;

namespace osu.Game.Overlays.Settings.Sections
{
    public partial class SkinSection : SettingsSection
    {
        private readonly List<ModeSkinControl> modeSkinControls = new List<ModeSkinControl>();
        private SettingsButtonV2 layoutEditorButton;
        private SkinAuthorActionsContainer authorActions;
        private RenameSkinButton renameButton;
        private ExportSkinButton exportButton;
        private DeleteSkinButton deleteButton;
        private SkinScriptSettings scriptSettings;
        private Bindable<Skin> currentSkin;
        private Bindable<Live<SkinInfo>> committedSelection;
        private bool synchronisingDropdownSelection;
        private bool synchronisingModeConfiguration;
        private bool dropdownItemsLoading = true;
        private bool committedSelectionDisabled;

        public override LocalisableString Header => SkinSettingsStrings.SkinSectionHeader;

        public override Drawable CreateIcon() => new SpriteIcon
        {
            Icon = OsuIcon.SkinB
        };

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "skins" });

        private readonly List<Live<SkinInfo>> dropdownItems = new List<Live<SkinInfo>>();
        private int dropdownRefreshSequence;

        [Resolved]
        private SkinManager skins { get; set; }

        [Resolved]
        private RealmAccess realm { get; set; }

        [Resolved(CanBeNull = true)]
        private INotificationOverlay notificationOverlay { get; set; }

        [Resolved(CanBeNull = true)]
        private OsuConfigManager config { get; set; }

        [Resolved(CanBeNull = true)]
        private IBindable<RulesetInfo> currentRuleset { get; set; }

        private IDisposable realmSubscription;

        [BackgroundDependencyLoader(permitNulls: true)]
        private void load([CanBeNull] SkinEditorOverlay skinEditor)
        {
            modeSkinControls.Clear();
            modeSkinControls.Add(createModeSkinControl("bms", SkinSettingsStrings.BmsSkin, OsuSetting.SkinBms));
            modeSkinControls.Add(createModeSkinControl("mania", SkinSettingsStrings.ManiaSkin, OsuSetting.SkinMania));

            Children = new Drawable[]
            {
                new SkinInstallationStatus(),
                new SettingsItemV2(modeSkinControls[0].Dropdown)
                {
                    // There is no separate player-facing global default anymore; choosing OMS is the explicit reset.
                    ShowRevertToDefaultButton = false,
                },
                new SettingsItemV2(modeSkinControls[1].Dropdown)
                {
                    ShowRevertToDefaultButton = false,
                },
                new ReloadCurrentSkinButton(),
                new OpenSkinFolderButton(),
                scriptSettings = new SkinScriptSettings(),
                authorActions = new SkinAuthorActionsContainer
                {
                    Children = new Drawable[]
                    {
                        renameButton = new RenameSkinButton { Padding = new MarginPadding { Right = 2.5f }, RelativeSizeAxes = Axes.X, Width = 1 / 3f },
                        exportButton = new ExportSkinButton { Padding = new MarginPadding { Horizontal = 2.5f }, RelativeSizeAxes = Axes.X, Width = 1 / 3f },
                        deleteButton = new DeleteSkinButton { Padding = new MarginPadding { Left = 2.5f }, RelativeSizeAxes = Axes.X, Width = 1 / 3f },
                    }
                },
                layoutEditorButton = new SettingsButtonV2
                {
                    Text = SkinSettingsStrings.SkinLayoutEditor,
                    TooltipText = "打开皮肤编辑器；修改会保存为独立皮肤副本。",
                    Action = () => skinEditor?.ToggleVisibility(),
                },
            };
        }

        private ModeSkinControl createModeSkinControl(string rulesetShortName, LocalisableString caption, OsuSetting setting)
        {
            Bindable<string> sourceConfiguration = config?.GetBindable<string>(setting);
            Live<SkinInfo> initialSelection = resolveConfiguredSelection(sourceConfiguration?.Value);
            var configuration = sourceConfiguration?.GetBoundCopy() ?? new Bindable<string>(initialSelection.ID.ToString());
            var selection = new Bindable<Live<SkinInfo>>(initialSelection);
            var dropdown = new SkinDropdown
            {
                AlwaysShowSearchBar = false,
                AllowNonContiguousMatching = true,
                Caption = caption,
                Current = selection,
                Items = new[] { initialSelection },
            };

            return new ModeSkinControl(rulesetShortName, configuration, selection, dropdown);
        }

        private Live<SkinInfo> resolveConfiguredSelection(string configured)
        {
            if (string.IsNullOrWhiteSpace(configured) || !Guid.TryParse(configured, out Guid id))
            {
                string global = config?.Get<string>(OsuSetting.Skin);
                if (!string.IsNullOrWhiteSpace(global) && !string.Equals(global, configured, StringComparison.Ordinal))
                    return resolveConfiguredSelection(global);

                return skins.CurrentSkinInfo.Value;
            }

            if (id == SkinInfo.OMS_SKIN)
                return skins.DefaultOmsSkin.SkinInfo;

            return skins.Query(s => s.ID == id && !s.Protected && !s.DeletePending) ?? skins.DefaultOmsSkin.SkinInfo;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            updateDropdownDisabled();

            committedSelection = skins.CurrentSkinInfo.GetBoundCopy();
            committedSelection.BindValueChanged(skin => syncCommittedSelection(skin.NewValue), true);
            committedSelection.BindDisabledChanged(disabled =>
            {
                committedSelectionDisabled = disabled;
                updateDropdownDisabled();
            }, true);

            foreach (ModeSkinControl control in modeSkinControls)
            {
                control.Selection.BindValueChanged(selection => skinSelectionChanged(control, selection.NewValue));
                control.Configuration.BindValueChanged(_ => configurationChanged(control));
            }

            currentRuleset?.BindValueChanged(_ => syncModeSelections(), true);

            currentSkin = skins.CurrentSkin.GetBoundCopy();
            currentSkin.BindValueChanged(_ => updateAuthoringState(), true);
            currentSkin.BindDisabledChanged(_ => updateAuthoringState(), true);

            realmSubscription = realm.RegisterForNotifications(_ => realm.Realm.All<SkinInfo>()
                                                                         .Where(s => !s.DeletePending)
                                                                         .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase), skinsChanged);

            refreshDropdownItems();

        }

        private void syncCommittedSelection(Live<SkinInfo> selection)
        {
            ModeSkinControl active = getActiveModeControl();

            if (active == null)
            {
                foreach (ModeSkinControl control in modeSkinControls)
                    setDropdownSelection(control, selection);

                return;
            }

            setDropdownSelection(active, selection);

            if (!synchronisingModeConfiguration && active.Configuration != null)
            {
                synchronisingModeConfiguration = true;
                try
                {
                    active.Configuration.Value = selection.ID.ToString();
                }
                finally
                {
                    synchronisingModeConfiguration = false;
                }
            }
        }

        private void skinSelectionChanged(ModeSkinControl control, Live<SkinInfo> selection)
        {
            if (synchronisingDropdownSelection)
                return;

            if (!isActiveMode(control))
            {
                control.Configuration.Value = selection.ID.ToString();
                return;
            }

            if (selection.ID == SkinInfo.RANDOM_SKIN)
            {
                setDropdownSelection(control, skins.CurrentSkinInfo.Value);
                skins.SelectRandomSkin();
                return;
            }

            skins.CurrentSkinInfo.Value = selection;

            if (skins.LastSelectionRejectionReason == SkinSelectionRejectionReason.LiveGameplayActive)
            {
                notificationOverlay?.Post(new SimpleErrorNotification
                {
                    Text = SkinSettingsStrings.CurrentSkinReloadGameplayActive,
                });
            }

            // Filesystem-backed requests prepare asynchronously and rejected requests never commit. Keep the
            // control on the last committed value until SkinManager publishes a coherent pair.
            setDropdownSelection(control, skins.CurrentSkinInfo.Value);
        }

        private void configurationChanged(ModeSkinControl control)
        {
            if (synchronisingModeConfiguration || isActiveMode(control))
                return;

            setDropdownSelection(control, resolveConfiguredSelection(control.Configuration.Value));
        }

        private void syncModeSelections()
        {
            foreach (ModeSkinControl control in modeSkinControls)
            {
                if (!isActiveMode(control))
                    setDropdownSelection(control, resolveConfiguredSelection(control.Configuration.Value));
            }
        }

        private ModeSkinControl getActiveModeControl()
            => currentRuleset?.Value == null ? null : modeSkinControls.FirstOrDefault(isActiveMode);

        private bool isActiveMode(ModeSkinControl control)
            => currentRuleset?.Value == null
                || string.Equals(currentRuleset.Value.ShortName, control.RulesetShortName, StringComparison.Ordinal);

        private void setDropdownSelection(ModeSkinControl control, Live<SkinInfo> selection)
        {
            synchronisingDropdownSelection = true;
            bool wasDisabled = control.Selection.Disabled;

            try
            {
                if (wasDisabled)
                    control.Selection.Disabled = false;

                control.Selection.Value = selection;
            }
            finally
            {
                if (wasDisabled)
                    control.Selection.Disabled = true;

                synchronisingDropdownSelection = false;
            }
        }

        private void updateDropdownDisabled()
        {
            foreach (ModeSkinControl control in modeSkinControls)
                control.Selection.Disabled = dropdownItemsLoading || committedSelectionDisabled;
        }

        private void updateAuthoringState()
        {
            bool canModify = !currentSkin.Disabled && skins.CanModify(currentSkin.Value.SkinInfo);
            bool canExport = !currentSkin.Disabled && skins.CanExport(currentSkin.Value.SkinInfo);
            bool canDelete = !currentSkin.Disabled && skins.CanDelete(currentSkin.Value.SkinInfo.ID);

            authorActions.CanBeShown.Value = canModify || canExport || canDelete;

            renameButton.CanBeShown.Value = canModify;
            exportButton.CanBeShown.Value = canExport;
            deleteButton.CanBeShown.Value = canDelete;

            int visibleActionCount = (canModify ? 1 : 0) + (canExport ? 1 : 0) + (canDelete ? 1 : 0);
            float actionWidth = visibleActionCount == 0 ? 1 : 1f / visibleActionCount;
            renameButton.Width = actionWidth;
            exportButton.Width = actionWidth;
            deleteButton.Width = actionWidth;

            bool editorAvailable = SkinAuthoringAvailability.LegacyEditorAvailable && !currentSkin.Disabled;
            layoutEditorButton.Enabled.Value = editorAvailable;
            layoutEditorButton.CanBeShown.Value = editorAvailable;
        }

        private void skinsChanged(IRealmCollection<SkinInfo> sender, ChangeSet changes)
        {
            // This can only mean that realm is recycling, else we would see the protected skins.
            // Because we are using `Live<>` in this class, we don't need to worry about this scenario too much.
            if (!sender.Any())
                return;

            refreshDropdownItems();
        }

        private void refreshDropdownItems()
            => _ = refreshDropdownItemsAsync();

        private async System.Threading.Tasks.Task refreshDropdownItemsAsync()
        {
            int refreshSequence = Interlocked.Increment(ref dropdownRefreshSequence);

            try
            {
                var items = await skins.GetAllUsableSkinsAsync().ConfigureAwait(false);

                Schedule(() =>
                {
                    if (IsDisposed || refreshSequence != dropdownRefreshSequence)
                        return;

                    dropdownItems.Clear();
                    dropdownItems.AddRange(items);

                    foreach (ModeSkinControl control in modeSkinControls)
                        control.Dropdown.Items = dropdownItems.ToList();
                    dropdownItemsLoading = false;
                    updateDropdownDisabled();
                    syncModeSelections();
                });
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to populate the settings skin dropdown.");

                Schedule(() =>
                {
                    if (IsDisposed || refreshSequence != dropdownRefreshSequence)
                        return;

                    foreach (ModeSkinControl control in modeSkinControls)
                        control.Dropdown.Items = new[] { control.Selection.Value };
                    dropdownItemsLoading = false;
                    updateDropdownDisabled();
                });
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                Interlocked.Increment(ref dropdownRefreshSequence);
                committedSelection?.UnbindAll();

                foreach (ModeSkinControl control in modeSkinControls)
                {
                    control.Selection.UnbindAll();
                    control.Configuration.UnbindAll();
                }
            }

            base.Dispose(isDisposing);

            realmSubscription?.Dispose();
        }

        private partial class SkinDropdown : FormDropdown<Live<SkinInfo>>
        {
            protected override LocalisableString GenerateItemText(Live<SkinInfo> item) => item.ToString();
        }

        private sealed class ModeSkinControl
        {
            internal readonly string RulesetShortName;
            internal readonly Bindable<string> Configuration;
            internal readonly Bindable<Live<SkinInfo>> Selection;
            internal readonly SkinDropdown Dropdown;

            internal ModeSkinControl(
                string rulesetShortName,
                Bindable<string> configuration,
                Bindable<Live<SkinInfo>> selection,
                SkinDropdown dropdown)
            {
                RulesetShortName = rulesetShortName;
                Configuration = configuration;
                Selection = selection;
                Dropdown = dropdown;
            }
        }

        private partial class SkinAuthorActionsContainer : FillFlowContainer
        {
            public BindableBool CanBeShown { get; } = new BindableBool(true);

            public SkinAuthorActionsContainer()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Direction = FillDirection.Horizontal;
                Padding = SettingsPanel.CONTENT_PADDING;
                AlwaysPresent = true;

                CanBeShown.BindValueChanged(_ => Invalidate(Invalidation.Presence));
            }

            public override bool IsPresent => base.IsPresent && CanBeShown.Value;
        }

        public partial class OpenSkinFolderButton : SettingsButtonV2
        {
            [BackgroundDependencyLoader]
            private void load(Storage storage)
            {
                Text = SkinSettingsStrings.OpenSkinFolder;
                TooltipText = SkinSettingsStrings.SkinFolderHint;
                Action = () => storage.GetStorageForDirectory(SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY).PresentExternally();
            }
        }

        public partial class ReloadCurrentSkinButton : SettingsButtonV2
        {
            [Resolved]
            private SkinManager skins { get; set; }

            [Resolved(CanBeNull = true)]
            private INotificationOverlay notificationOverlay { get; set; }

            [Resolved(CanBeNull = true)]
            private OsuGame game { get; set; }

            private Bindable<Skin> currentSkin;
            private System.Threading.Tasks.Task activeReload;

            [BackgroundDependencyLoader]
            private void load()
            {
                Text = SkinSettingsStrings.RefreshSkins;
                Action = reload;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                currentSkin = skins.CurrentSkin.GetBoundCopy();
                currentSkin.BindValueChanged(_ => updateState());
                currentSkin.BindDisabledChanged(_ => updateState(), true);
            }

            private void updateState()
                => Enabled.Value = activeReload == null
                                   && !currentSkin.Disabled
                                   && (game != null || skins.CanReloadCurrentRevision);

            private void reload()
            {
                if (activeReload != null)
                    return;

                Enabled.Value = false;
                activeReload = observeReloadAsync();
            }

            private async System.Threading.Tasks.Task observeReloadAsync()
            {
                SkinCurrentRevisionReloadResult result;

                try
                {
                    if (game != null)
                    {
                        SkinManagedFolderScanResult scan = await game.RefreshManagedSkinFoldersAsync().ConfigureAwait(false);
                        if (!scan.IsSuccess)
                        {
                            Schedule(() => finishFolderRefresh(false));
                            return;
                        }
                    }

                    if (!skins.CanReloadCurrentRevision)
                    {
                        Schedule(() => finishFolderRefresh(true));
                        return;
                    }

                    result = await skins.ReloadCurrentRevisionAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    result = SkinCurrentRevisionReloadResult.Cancelled;
                }
                catch
                {
                    Logger.Log("Failed to reload the current skin revision.");
                    result = SkinCurrentRevisionReloadResult.Failed;
                }

                Schedule(() =>
                {
                    switch (result)
                    {
                        case SkinCurrentRevisionReloadResult.Success:
                            notificationOverlay?.Post(new SimpleNotification { Text = SkinSettingsStrings.CurrentSkinReloaded });
                            break;

                        case SkinCurrentRevisionReloadResult.NoChange:
                            notificationOverlay?.Post(new SimpleNotification { Text = SkinSettingsStrings.SkinsRefreshed });
                            break;

                        case SkinCurrentRevisionReloadResult.LiveGameplayActive:
                            notificationOverlay?.Post(new SimpleErrorNotification { Text = SkinSettingsStrings.CurrentSkinReloadGameplayActive });
                            break;

                        case SkinCurrentRevisionReloadResult.Superseded:
                        case SkinCurrentRevisionReloadResult.Cancelled:
                            break;

                        case SkinCurrentRevisionReloadResult.ParticipantRejected:
                        case SkinCurrentRevisionReloadResult.SourceChanged:
                            notificationOverlay?.Post(new SimpleErrorNotification { Text = SkinSettingsStrings.CurrentSkinReloadRejected });
                            break;

                        default:
                            notificationOverlay?.Post(new SimpleErrorNotification { Text = SkinSettingsStrings.CurrentSkinReloadFailed });
                            break;
                    }

                    activeReload = null;

                    if (!IsDisposed)
                        updateState();
                });
            }

            private void finishFolderRefresh(bool success)
            {
                activeReload = null;
                if (IsDisposed)
                    return;

                notificationOverlay?.Post(success
                    ? new SimpleNotification { Text = SkinSettingsStrings.SkinsRefreshed }
                    : new SimpleErrorNotification { Text = SkinSettingsStrings.SkinFolderRefreshFailed });
                updateState();
            }
        }

        public partial class RenameSkinButton : SettingsButtonV2, IHasPopover
        {
            [Resolved]
            private SkinManager skins { get; set; }

            private Bindable<Skin> currentSkin;

            [BackgroundDependencyLoader]
            private void load()
            {
                Text = CommonStrings.Rename;
                Action = this.ShowPopover;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                currentSkin = skins.CurrentSkin.GetBoundCopy();
                currentSkin.BindValueChanged(_ => updateState());
                currentSkin.BindDisabledChanged(_ => updateState(), true);
            }

            private void updateState() => Enabled.Value = !currentSkin.Disabled && skins.CanModify(currentSkin.Value.SkinInfo);

            public Popover GetPopover()
            {
                return new RenameSkinPopover();
            }
        }

        public partial class ExportSkinButton : SettingsButtonV2
        {
            [Resolved]
            private SkinManager skins { get; set; }

            private Bindable<Skin> currentSkin;

            [BackgroundDependencyLoader]
            private void load()
            {
                Text = CommonStrings.Export;
                Action = export;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                currentSkin = skins.CurrentSkin.GetBoundCopy();
                currentSkin.BindValueChanged(_ => updateState());
                currentSkin.BindDisabledChanged(_ => updateState(), true);
            }

            private void updateState() => Enabled.Value = !currentSkin.Disabled && skins.CanExport(currentSkin.Value.SkinInfo);

            private void export()
            {
                try
                {
                    skins.ExportCurrentSkin();
                }
                catch (Exception e)
                {
                    Logger.Log($"Could not export current skin: {e.Message}", level: LogLevel.Error);
                }
            }
        }

        public partial class DeleteSkinButton : DangerousSettingsButtonV2
        {
            [Resolved]
            private SkinManager skins { get; set; }

            [Resolved(CanBeNull = true)]
            private IDialogOverlay dialogOverlay { get; set; }

            [Resolved(CanBeNull = true)]
            private INotificationOverlay notificationOverlay { get; set; }

            private Bindable<Skin> currentSkin;
            private System.Threading.Tasks.Task activeDeletion;

            [BackgroundDependencyLoader]
            private void load()
            {
                Text = WebCommonStrings.ButtonsDelete;
                Action = delete;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                currentSkin = skins.CurrentSkin.GetBoundCopy();
                currentSkin.BindValueChanged(_ => updateState());
                currentSkin.BindDisabledChanged(_ => updateState(), true);
            }

            private void updateState() => Enabled.Value = activeDeletion == null
                                                          && !currentSkin.Disabled
                                                          && skins.CanDelete(currentSkin.Value.SkinInfo.ID);

            private void delete()
            {
                Skin current = currentSkin.Value;
                dialogOverlay?.Push(new SkinDeleteDialog(current.SkinInfo.ID, current.SkinInfo.Value.Name)
                {
                    DeleteRequested = startDeletion,
                });
            }

            private void startDeletion(Guid recordId)
            {
                if (activeDeletion != null)
                    return;

                Enabled.Value = false;
                activeDeletion = observeDeletionAsync(recordId);
            }

            private async System.Threading.Tasks.Task observeDeletionAsync(Guid recordId)
            {
                try
                {
                    bool success = await skins.DeleteSkinAsync(recordId).ConfigureAwait(false);

                    if (!success)
                    {
                        Schedule(() => notificationOverlay?.Post(new SimpleErrorNotification
                        {
                            Text = SkinSettingsStrings.FolderSkinOperationRejected,
                        }));
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to delete the current skin from settings.");

                    Schedule(() => notificationOverlay?.Post(new SimpleErrorNotification
                    {
                        Text = SkinSettingsStrings.FolderSkinOperationFailed,
                    }));
                }
                finally
                {
                    Schedule(() =>
                    {
                        activeDeletion = null;

                        if (!IsDisposed)
                            updateState();
                    });
                }
            }
        }

        public partial class SkinDeleteDialog : DeletionDialog
        {
            internal Guid RecordId { get; }

            internal Action<Guid> DeleteRequested { private get; init; }

            public SkinDeleteDialog(Guid recordId, string immutableLabel)
            {
                RecordId = recordId;
                BodyText = immutableLabel;
            }

            [BackgroundDependencyLoader]
            private void load(SkinManager manager)
            {
                // The dialog deliberately retains only immutable confirmation data. The manager re-reads all
                // authoritative fields after confirmation and owns the complete asynchronous operation lifetime.
                DangerousAction = () =>
                {
                    if (DeleteRequested != null)
                    {
                        DeleteRequested(RecordId);
                        return;
                    }

                    deletionTask ??= observeDeletionAsync(manager);
                };
            }

            private System.Threading.Tasks.Task deletionTask;

            private async System.Threading.Tasks.Task observeDeletionAsync(SkinManager manager)
            {
                try
                {
                    bool success = await manager.DeleteSkinAsync(RecordId).ConfigureAwait(false);

                    if (!success)
                        Logger.Log("The detached skin deletion request was rejected.", level: LogLevel.Important);
                }
                catch
                {
                    Logger.Log("A detached skin deletion request failed.", level: LogLevel.Error);
                }
            }
        }

        public partial class RenameSkinPopover : OsuPopover
        {
            [Resolved]
            private SkinManager skins { get; set; }

            private readonly FocusedTextBox textBox;

            public RenameSkinPopover()
            {
                AutoSizeAxes = Axes.Both;
                Origin = Anchor.TopCentre;

                RoundedButton renameButton;

                Child = new FillFlowContainer
                {
                    Direction = FillDirection.Vertical,
                    AutoSizeAxes = Axes.Y,
                    Width = 250,
                    Spacing = new Vector2(10f),
                    Children = new Drawable[]
                    {
                        textBox = new FocusedTextBox
                        {
                            PlaceholderText = SkinSettingsStrings.SkinName,
                            FontSize = OsuFont.DEFAULT_FONT_SIZE,
                            RelativeSizeAxes = Axes.X,
                            SelectAllOnFocus = true,
                        },
                        renameButton = new RoundedButton
                        {
                            Height = 40,
                            RelativeSizeAxes = Axes.X,
                            MatchingFilter = true,
                            Text = WebCommonStrings.ButtonsSave,
                        }
                    }
                };

                renameButton.Action += rename;
                textBox.OnCommit += (_, _) => rename();
            }

            protected override void PopIn()
            {
                textBox.Text = skins.CurrentSkinInfo.Value.Value.Name;
                textBox.TakeFocus();

                base.PopIn();
            }

            private void rename()
            {
                skins.Rename(skins.CurrentSkinInfo.Value, textBox.Text);
                PopOut();
            }
        }
    }
}
