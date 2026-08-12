// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Settings.Sections.Maintenance;
using osu.Game.Screens;
using osu.Game.Skinning;
using osuTK;
using Realms;
using WebCommonStrings = osu.Game.Resources.Localisation.Web.CommonStrings;

namespace osu.Game.Overlays.Settings.Sections
{
    /// <summary>
    /// The author-facing workspace for folder-backed skins.
    /// </summary>
    /// <remarks>
    /// This component deliberately consumes only <see cref="FolderSkinWorkspaceRecord"/> projections. It never
    /// reads Realm skin fields to decide whether a row is external, managed or an ordinary packaged skin.
    /// </remarks>
    internal partial class FolderSkinWorkspace : SettingsSubsection
    {
        protected override LocalisableString Header => SkinSettingsStrings.FolderSkinWorkspaceHeader;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private IPerformFromScreenRunner? performer { get; set; }

        [Resolved(CanBeNull = true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notificationOverlay { get; set; }

        private FillFlowContainer recordsFlow = null!;
        private SettingsButtonV2 registerExternalFolderButton = null!;
        private SettingsButtonV2 retryRecoveryButton = null!;
        private SettingsNote journalSupportNote = null!;

        private readonly List<FolderSkinWorkspaceRow> rows = new List<FolderSkinWorkspaceRow>();
        private readonly CancellationTokenSource readLifetimeCancellation = new CancellationTokenSource();
        private IDisposable? realmSubscription;
        private Task? activeOperation;
        private Task? recordsRefreshTask;
        private Task? journalRefreshTask;
        private int recordsRefreshSequence;
        private int journalRefreshSequence;
        private bool journalStateSubscribed;

        internal IReadOnlyList<FolderSkinWorkspaceRow> Rows => rows;

        internal bool OperationInProgress => activeOperation != null;

        [BackgroundDependencyLoader]
        private void load()
        {
            AddRange(new Drawable[]
            {
                new OsuTextFlowContainer(text => text.Font = OsuFont.Default.With(size: 14))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Text = SkinSettingsStrings.FolderSkinWorkspaceDescription,
                },
                registerExternalFolderButton = new SettingsButtonV2
                {
                    Text = SkinSettingsStrings.RegisterExternalFolder,
                    Action = showDirectoryPicker,
                },
                recordsFlow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, SettingsSection.ITEM_SPACING_V2),
                },
                new OsuSpriteText
                {
                    Text = SkinSettingsStrings.ManagedFolderRecoverySupport,
                    Font = OsuFont.Default.With(size: 16, weight: FontWeight.SemiBold),
                    Margin = SettingsPanel.CONTENT_PADDING,
                },
                journalSupportNote = new SettingsNote
                {
                    RelativeSizeAxes = Axes.X,
                },
                retryRecoveryButton = new SettingsButtonV2
                {
                    Text = SkinSettingsStrings.RetryManagedFolderRecovery,
                    Action = retryRecovery,
                    Enabled = { Value = false },
                },
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Realm notifications are only an invalidation signal. The UI never projects or classifies Realm rows;
            // every refresh goes back through SkinManager's path-free workspace DTO query.
            realmSubscription = realm.RegisterForNotifications(_ => realm.Realm.All<SkinInfo>(), recordsChanged);
            skins.ManagedFolderJournalStateChanged += journalStateChanged;
            journalStateSubscribed = true;

            refreshRecords();
            refreshJournalSupport();
        }

        private void journalStateChanged()
        {
            // The manager event is a path-free invalidation signal. Inspection remains read-only and off update.
            Schedule(() =>
            {
                if (!IsDisposed)
                    refreshJournalSupport();
            });
        }

        private void recordsChanged(IRealmCollection<SkinInfo> sender, ChangeSet? changes)
        {
            if (IsDisposed)
                return;

            refreshRecords();
            refreshJournalSupport();
        }

        private void showDirectoryPicker()
        {
            if (activeOperation != null || performer == null)
                return;

            performer.PerformFromScreen(screen =>
            {
                var picker = new FolderSkinDirectorySelectScreen
                {
                    Selected = directory => Schedule(() =>
                    {
                        if (!IsDisposed)
                            runOperation(() => skins.RegisterExternalFolderAsync(directory.FullName));
                    }),
                };

                screen.Push(picker);
            });
        }

        private void refreshRecords()
        {
            int sequence = Interlocked.Increment(ref recordsRefreshSequence);
            recordsRefreshTask = refreshRecordsAsync(sequence);
        }

        private async Task refreshRecordsAsync(int sequence)
        {
            try
            {
                IReadOnlyList<FolderSkinWorkspaceRecord> records = await skins.GetFolderSkinWorkspaceRecordsAsync(readLifetimeCancellation.Token).ConfigureAwait(false);

                Schedule(() =>
                {
                    if (IsDisposed || sequence != recordsRefreshSequence)
                        return;

                    rebuildRows(records);
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                Logger.Log("The folder skin workspace could not be refreshed.", level: LogLevel.Error);

                Schedule(() =>
                {
                    if (IsDisposed || sequence != recordsRefreshSequence)
                        return;

                    recordsFlow.Clear();
                    rows.Clear();
                    recordsFlow.Add(createMessage(SkinSettingsStrings.FolderSkinOperationFailed));
                });
            }
        }

        private void rebuildRows(IReadOnlyList<FolderSkinWorkspaceRecord> records)
        {
            recordsFlow.Clear();
            rows.Clear();

            if (records.Count == 0)
            {
                recordsFlow.Add(createMessage(SkinSettingsStrings.NoFolderSkins));
                return;
            }

            foreach (FolderSkinWorkspaceRecord record in records)
            {
                var row = new FolderSkinWorkspaceRow(
                    record,
                    openFolder: id => runOperation(() => skins.OpenFolderAsync(id), refreshAfter: false),
                    importManagedCopy: (id, targetName) => runOperation(() => skins.ImportManagedCopyAsync(id, targetName)),
                    unregister: showUnregisterConfirmation,
                    rename: (id, targetName) => runOperation(async () => (await skins.RenameManagedFolderAsync(id, targetName).ConfigureAwait(false)).IsSuccess),
                    delete: showDeleteConfirmation);

                row.SetInteractionEnabled(activeOperation == null);
                rows.Add(row);
                recordsFlow.Add(row);
            }
        }

        private static Drawable createMessage(LocalisableString text) => new OsuTextFlowContainer(sprite => sprite.Font = OsuFont.Default.With(size: 14))
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Padding = SettingsPanel.CONTENT_PADDING,
            Text = text,
        };

        private void showUnregisterConfirmation(Guid recordId, string immutableLabel)
            => dialogOverlay?.Push(new UnregisterFolderSkinDialog(recordId, immutableLabel, id =>
                runOperation(() => skins.UnregisterExternalFolderAsync(id))));

        private void showDeleteConfirmation(Guid recordId, string immutableLabel)
            => dialogOverlay?.Push(new SkinSection.SkinDeleteDialog(recordId, immutableLabel)
            {
                DeleteRequested = id => runOperation(() => skins.DeleteSkinAsync(id)),
            });

        private void retryRecovery()
        {
            if (!retryRecoveryButton.Enabled.Value)
                return;

            runOperation(() => skins.RetryManagedFolderJournalRecoveryAsync());
        }

        private void runOperation(Func<Task<bool>> operation, bool refreshAfter = true)
        {
            if (activeOperation != null)
                return;

            setInteractionEnabled(false);
            activeOperation = performOperationAsync(operation, refreshAfter);
        }

        private async Task performOperationAsync(Func<Task<bool>> operation, bool refreshAfter)
        {
            try
            {
                bool success = await operation().ConfigureAwait(false);

                if (!success)
                {
                    Schedule(() => notificationOverlay?.Post(new SimpleErrorNotification
                    {
                        Text = SkinSettingsStrings.FolderSkinOperationRejected,
                    }));
                }
            }
            catch
            {
                Logger.Log("A folder skin workspace operation failed.", level: LogLevel.Error);

                Schedule(() => notificationOverlay?.Post(new SimpleErrorNotification
                {
                    Text = SkinSettingsStrings.FolderSkinOperationFailed,
                }));
            }
            finally
            {
                Schedule(() =>
                {
                    if (IsDisposed)
                        return;

                    activeOperation = null;
                    setInteractionEnabled(true);

                    if (refreshAfter)
                        refreshRecords();
                });
            }
        }

        private void setInteractionEnabled(bool enabled)
        {
            registerExternalFolderButton.Enabled.Value = enabled && performer != null;

            foreach (FolderSkinWorkspaceRow row in rows)
                row.SetInteractionEnabled(enabled);

            if (!enabled)
                retryRecoveryButton.Enabled.Value = false;
            else
                refreshJournalSupport();
        }

        private void refreshJournalSupport()
        {
            int sequence = Interlocked.Increment(ref journalRefreshSequence);
            journalRefreshTask = refreshJournalSupportAsync(sequence);
        }

        private async Task refreshJournalSupportAsync(int sequence)
        {
            try
            {
                FolderSkinJournalSupportSnapshot snapshot = await skins.GetManagedFolderJournalSupportSnapshotAsync(readLifetimeCancellation.Token).ConfigureAwait(false);

                Schedule(() =>
                {
                    if (IsDisposed || sequence != journalRefreshSequence)
                        return;

                    journalSupportNote.Current.Value = new SettingsNote.Data(
                        SkinSettingsStrings.ManagedFolderRecoveryDetails(snapshot.Status, snapshot.Reason, snapshot.DiagnosticBundle),
                        snapshot.CanRetry ? SettingsNote.Type.Warning : SettingsNote.Type.Informational);
                    retryRecoveryButton.Enabled.Value = activeOperation == null && snapshot.CanRetry;
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                Logger.Log("The managed-folder recovery support state could not be read.", level: LogLevel.Error);

                Schedule(() =>
                {
                    if (IsDisposed || sequence != journalRefreshSequence)
                        return;

                    journalSupportNote.Current.Value = new SettingsNote.Data(SkinSettingsStrings.FolderSkinOperationFailed, SettingsNote.Type.Critical);
                    retryRecoveryButton.Enabled.Value = false;
                });
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                Interlocked.Increment(ref recordsRefreshSequence);
                Interlocked.Increment(ref journalRefreshSequence);
                readLifetimeCancellation.Cancel();
                realmSubscription?.Dispose();

                if (journalStateSubscribed)
                {
                    skins.ManagedFolderJournalStateChanged -= journalStateChanged;
                    journalStateSubscribed = false;
                }
            }

            base.Dispose(isDisposing);
        }

        internal partial class FolderSkinWorkspaceRow : CompositeDrawable
        {
            private readonly bool canOpenFolder;
            private readonly bool canImportManagedCopy;
            private readonly bool canUnregister;
            private readonly bool canRename;
            private readonly bool canDelete;

            private readonly SettingsButtonV2 openFolderButton;
            private readonly SettingsButtonV2? importManagedCopyButton;
            private readonly SettingsButtonV2? unregisterButton;
            private readonly SettingsButtonV2? renameButton;
            private readonly SettingsButtonV2? deleteButton;

            internal Guid RecordId { get; }

            internal string DisplayLabel { get; }

            internal FolderSkinWorkspaceRecordKind Kind { get; }

            internal IReadOnlyList<SettingsButtonV2> ActionButtons { get; }

            public FolderSkinWorkspaceRow(
                FolderSkinWorkspaceRecord record,
                Action<Guid> openFolder,
                Action<Guid, string> importManagedCopy,
                Action<Guid, string> unregister,
                Action<Guid, string> rename,
                Action<Guid, string> delete)
            {
                // Copy the immutable, path-free projection fields. Never retain the DTO or any Realm/live object.
                RecordId = record.RecordId;
                DisplayLabel = record.DisplayLabel;
                Kind = record.Kind;
                canOpenFolder = record.CanOpenFolder;
                canImportManagedCopy = record.CanImportManagedCopy;
                canUnregister = record.CanUnregister;
                canRename = record.CanRename;
                canDelete = record.CanDelete;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;

                openFolderButton = createButton(SkinSettingsStrings.OpenFolder, () => openFolder(RecordId), 112);

                var buttons = new List<SettingsButtonV2> { openFolderButton };

                if (Kind == FolderSkinWorkspaceRecordKind.External)
                {
                    buttons.Add(importManagedCopyButton = new FolderNameActionButton(
                        SkinSettingsStrings.ImportManagedCopy,
                        name => importManagedCopy(RecordId, name),
                        158));
                    buttons.Add(unregisterButton = createDangerousButton(SkinSettingsStrings.UnregisterFolder, () => unregister(RecordId, DisplayLabel), 104));
                }
                else
                {
                    buttons.Add(renameButton = new FolderNameActionButton(
                        SkinSettingsStrings.RenameFolder,
                        name => rename(RecordId, name),
                        126));
                    buttons.Add(deleteButton = createDangerousButton(WebCommonStrings.ButtonsDelete, () => delete(RecordId, DisplayLabel), 90));
                }

                ActionButtons = buttons;

                InternalChild = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Masking = true,
                    CornerRadius = 5,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Colour4.Black.Opacity(0.2f),
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 5),
                            Padding = new MarginPadding
                            {
                                Horizontal = SettingsPanel.CONTENT_MARGINS,
                                Vertical = 8,
                            },
                            Children = new Drawable[]
                            {
                                new OsuTextFlowContainer(text => text.Font = OsuFont.Default.With(size: 15, weight: FontWeight.SemiBold))
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Text = DisplayLabel,
                                },
                                new OsuSpriteText
                                {
                                    Text = Kind == FolderSkinWorkspaceRecordKind.External
                                        ? SkinSettingsStrings.ExternalFolder
                                        : SkinSettingsStrings.ManagedFolder,
                                    Font = OsuFont.Default.With(size: 12),
                                },
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(5, 0),
                                    Children = buttons.ToArray(),
                                },
                            },
                        },
                    },
                };
            }

            internal void SetInteractionEnabled(bool enabled)
            {
                openFolderButton.Enabled.Value = enabled && canOpenFolder;

                if (importManagedCopyButton != null)
                    importManagedCopyButton.Enabled.Value = enabled && canImportManagedCopy;

                if (unregisterButton != null)
                    unregisterButton.Enabled.Value = enabled && canUnregister;

                if (renameButton != null)
                    renameButton.Enabled.Value = enabled && canRename;

                if (deleteButton != null)
                    deleteButton.Enabled.Value = enabled && canDelete;
            }

            private static SettingsButtonV2 createButton(LocalisableString text, Action action, float width) => new SettingsButtonV2
            {
                Text = text,
                Action = action,
                RelativeSizeAxes = Axes.None,
                Width = width,
                Height = 40,
                Padding = new MarginPadding(),
            };

            private static DangerousSettingsButtonV2 createDangerousButton(LocalisableString text, Action action, float width) => new DangerousSettingsButtonV2
            {
                Text = text,
                Action = action,
                RelativeSizeAxes = Axes.None,
                Width = width,
                Height = 40,
                Padding = new MarginPadding(),
            };
        }

        private partial class FolderNameActionButton : SettingsButtonV2, IHasPopover
        {
            private readonly LocalisableString submitText;
            private readonly Action<string> submitted;

            public FolderNameActionButton(LocalisableString text, Action<string> submitted, float width)
            {
                submitText = text;
                this.submitted = submitted;

                Text = text;
                Action = this.ShowPopover;
                RelativeSizeAxes = Axes.None;
                Width = width;
                Height = 40;
                Padding = new MarginPadding();
            }

            public Popover GetPopover() => new FolderNamePopover(submitText, submit);

            private void submit(string targetName)
            {
                if (string.IsNullOrWhiteSpace(targetName))
                    return;

                submitted(targetName);
            }
        }

        private partial class FolderNamePopover : OsuPopover
        {
            private readonly FocusedTextBox textBox;
            private readonly RoundedButton submitButton;
            private readonly Action<string> submitted;

            public FolderNamePopover(LocalisableString submitText, Action<string> submitted)
            {
                this.submitted = submitted;

                AutoSizeAxes = Axes.Both;
                Origin = Anchor.TopCentre;

                Child = new FillFlowContainer
                {
                    Direction = FillDirection.Vertical,
                    AutoSizeAxes = Axes.Y,
                    Width = 260,
                    Spacing = new Vector2(10),
                    Children = new Drawable[]
                    {
                        textBox = new FocusedTextBox
                        {
                            PlaceholderText = SkinSettingsStrings.FolderName,
                            FontSize = OsuFont.DEFAULT_FONT_SIZE,
                            RelativeSizeAxes = Axes.X,
                            SelectAllOnFocus = true,
                        },
                        submitButton = new RoundedButton
                        {
                            Height = 40,
                            RelativeSizeAxes = Axes.X,
                            MatchingFilter = true,
                            Text = submitText,
                            Action = submit,
                        },
                    },
                };

                textBox.OnCommit += (_, _) => submit();
                textBox.Current.BindValueChanged(_ => updateSubmitState(), true);
            }

            protected override void PopIn()
            {
                textBox.TakeFocus();
                base.PopIn();
            }

            private void updateSubmitState() => submitButton.Enabled.Value = !string.IsNullOrWhiteSpace(textBox.Text);

            private void submit()
            {
                if (string.IsNullOrWhiteSpace(textBox.Text))
                    return;

                PopOut();
                submitted(textBox.Text);
            }
        }

        private partial class UnregisterFolderSkinDialog : DangerousActionDialog
        {
            public UnregisterFolderSkinDialog(Guid recordId, string immutableLabel, Action<Guid> unregister)
            {
                HeaderText = SkinSettingsStrings.UnregisterExternalFolderHeader;
                BodyText = SkinSettingsStrings.UnregisterExternalFolderBody(immutableLabel);
                DangerousAction = () => unregister(recordId);
            }
        }
    }

    internal partial class FolderSkinDirectorySelectScreen : DirectorySelectScreen
    {
        public override LocalisableString HeaderText => SkinSettingsStrings.SelectExternalSkinFolder;

        protected override bool ShowDescription => true;

        protected override LocalisableString DescriptionText => SkinSettingsStrings.SelectExternalSkinFolderDescription;

        public Action<DirectoryInfo>? Selected { get; init; }

        protected override void OnSelection(DirectoryInfo directory)
        {
            Selected?.Invoke(directory);
            this.Exit();
        }
    }
}
