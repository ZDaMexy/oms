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
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Skinning;
using osuTK;
using Realms;
using WebCommonStrings = osu.Game.Resources.Localisation.Web.CommonStrings;

namespace osu.Game.Overlays.Settings.Sections
{
    public partial class SkinSection : SettingsSection
    {
        private SkinDropdown skinDropdown;
        private SettingsButtonV2 layoutEditorButton;
        private Bindable<Skin> currentSkin;
        private Bindable<Live<SkinInfo>> dropdownSelection;
        private Bindable<Live<SkinInfo>> committedSelection;
        private bool synchronisingDropdownSelection;
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

        private IDisposable realmSubscription;

        [BackgroundDependencyLoader(permitNulls: true)]
        private void load([CanBeNull] SkinEditorOverlay skinEditor)
        {
            Children = new Drawable[]
            {
                new SettingsItemV2(skinDropdown = new SkinDropdown
                {
                    AlwaysShowSearchBar = true,
                    AllowNonContiguousMatching = true,
                    Caption = SkinSettingsStrings.CurrentSkin,
                    Current = dropdownSelection = new Bindable<Live<SkinInfo>>(skins.CurrentSkinInfo.Value),
                    Items = new[] { skins.CurrentSkinInfo.Value },
                }),
                new ReloadCurrentSkinButton(),
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Horizontal,
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Children = new Drawable[]
                    {
                        // This is all super-temporary until we move skin settings to their own panel / overlay.
                        new RenameSkinButton { Padding = new MarginPadding { Right = 2.5f }, RelativeSizeAxes = Axes.X, Width = 1 / 3f },
                        new ExportSkinButton { Padding = new MarginPadding { Horizontal = 2.5f }, RelativeSizeAxes = Axes.X, Width = 1 / 3f },
                        new DeleteSkinButton { Padding = new MarginPadding { Left = 2.5f }, RelativeSizeAxes = Axes.X, Width = 1 / 3f },
                    }
                },
                layoutEditorButton = new SettingsButtonV2
                {
                    Text = SkinSettingsStrings.SkinLayoutEditor,
                    TooltipText = SkinSettingsStrings.SkinAuthoringUnavailable,
                    Action = () => skinEditor?.ToggleVisibility(),
                },
                new FolderSkinWorkspace(),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            updateDropdownDisabled();

            committedSelection = skins.CurrentSkinInfo.GetBoundCopy();
            committedSelection.BindValueChanged(skin => setDropdownSelection(skin.NewValue), true);
            committedSelection.BindDisabledChanged(disabled =>
            {
                committedSelectionDisabled = disabled;
                updateDropdownDisabled();
            }, true);

            dropdownSelection.BindValueChanged(selection =>
            {
                if (synchronisingDropdownSelection)
                    return;

                if (selection.NewValue.ID == SkinInfo.RANDOM_SKIN)
                {
                    // Restore the committed selection before choosing so random selection can exclude it.
                    setDropdownSelection(skins.CurrentSkinInfo.Value);
                    skins.SelectRandomSkin();
                    return;
                }

                skins.CurrentSkinInfo.Value = selection.NewValue;

                if (skins.LastSelectionRejectionReason == SkinSelectionRejectionReason.LiveGameplayActive)
                {
                    notificationOverlay?.Post(new SimpleErrorNotification
                    {
                        Text = SkinSettingsStrings.CurrentSkinReloadGameplayActive,
                    });
                }

                // Filesystem-backed requests prepare asynchronously and rejected requests never commit. Keep the
                // control on the last committed value until SkinManager publishes a coherent pair.
                setDropdownSelection(skins.CurrentSkinInfo.Value);
            });

            currentSkin = skins.CurrentSkin.GetBoundCopy();
            currentSkin.BindValueChanged(_ => updateLayoutEditorState(), true);
            currentSkin.BindDisabledChanged(_ => updateLayoutEditorState(), true);

            realmSubscription = realm.RegisterForNotifications(_ => realm.Realm.All<SkinInfo>()
                                                                         .Where(s => !s.DeletePending)
                                                                         .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase), skinsChanged);

            refreshDropdownItems();

        }

        private void setDropdownSelection(Live<SkinInfo> selection)
        {
            synchronisingDropdownSelection = true;
            bool wasDisabled = dropdownSelection.Disabled;

            try
            {
                if (wasDisabled)
                    dropdownSelection.Disabled = false;

                dropdownSelection.Value = selection;
            }
            finally
            {
                if (wasDisabled)
                    dropdownSelection.Disabled = true;

                synchronisingDropdownSelection = false;
            }
        }

        private void updateDropdownDisabled()
            => skinDropdown.Current.Disabled = dropdownItemsLoading || committedSelectionDisabled;

        private void updateLayoutEditorState()
            => layoutEditorButton.Enabled.Value = SkinAuthoringAvailability.LegacyEditorAvailable
                                                  && !currentSkin.Disabled
                                                  && skins.CanModify(currentSkin.Value.SkinInfo);

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

                    skinDropdown.Items = dropdownItems.ToList();
                    dropdownItemsLoading = false;
                    updateDropdownDisabled();
                });
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to populate the settings skin dropdown.");

                Schedule(() =>
                {
                    if (IsDisposed || refreshSequence != dropdownRefreshSequence)
                        return;

                    skinDropdown.Items = new[] { skins.CurrentSkinInfo.Value };
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
            }

            base.Dispose(isDisposing);

            realmSubscription?.Dispose();
        }

        private partial class SkinDropdown : FormDropdown<Live<SkinInfo>>
        {
            protected override LocalisableString GenerateItemText(Live<SkinInfo> item) => item.ToString();
        }

        public partial class ReloadCurrentSkinButton : SettingsButtonV2
        {
            [Resolved]
            private SkinManager skins { get; set; }

            [Resolved(CanBeNull = true)]
            private INotificationOverlay notificationOverlay { get; set; }

            private Bindable<Skin> currentSkin;
            private System.Threading.Tasks.Task activeReload;

            [BackgroundDependencyLoader]
            private void load()
            {
                Text = SkinSettingsStrings.ReloadCurrentSkin;
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
                                   && skins.CanReloadCurrentRevision;

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
                    result = await skins.ReloadCurrentRevisionAsync().ConfigureAwait(false);
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
                            notificationOverlay?.Post(new SimpleNotification { Text = SkinSettingsStrings.CurrentSkinReloadNoChanges });
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
