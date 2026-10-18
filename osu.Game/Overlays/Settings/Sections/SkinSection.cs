// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK;
using WebCommonStrings = osu.Game.Resources.Localisation.Web.CommonStrings;

namespace osu.Game.Overlays.Settings.Sections
{
    public partial class SkinSection : SettingsSection
    {
        public override LocalisableString Header => SkinSettingsStrings.SkinSectionHeader;
        public override Drawable CreateIcon() => new SpriteIcon { Icon = OsuIcon.SkinB };
        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "skins", "键数", "皮肤配置", "默认" });

        public SkinScopeSelector ScopeSelector { get; private set; }
        public SkinSelectionScope EditingScope => scope.Value;
        public bool IsTargetReady => inspectedSkin.Value != null && !targetInfo.Disabled;
        public Guid DisplayedSkinId => targetInfo.Value.ID;

        private readonly Bindable<SkinSelectionScope> scope = new(new SkinSelectionScope("bms"));
        private readonly Bindable<Skin> inspectedSkin = new();
        private readonly Bindable<Live<SkinInfo>> targetInfo = new();
        private readonly Bindable<Live<SkinInfo>> dropdownSelection = new();
        private readonly List<Live<SkinInfo>> dropdownItems = new();
        private SkinDropdown dropdown;
        private OsuTextFlowContainer targetTitle;
        private OsuTextFlowContainer assignmentStatus;
        private OsuTextFlowContainer overridesSummary;
        private OsuTextFlowContainer preparationStatus;
        private SettingsButtonV2 followDefaultButton;
        private SettingsButtonV2 moreButton;
        private SettingsButtonV2 layoutEditorButton;
        private SkinAuthorActionsContainer authorActions;
        private RenameSkinButton renameButton;
        private ExportSkinButton exportButton;
        private DeleteSkinButton deleteButton;
        private SkinSettingsPreview preview;
        private readonly object previewGate = new();
        private readonly HashSet<SkinSettingsPreview> pendingPreviews = new();
        private bool stopping;
        private CancellationTokenSource preparationCancellation;
        private Bindable<Skin> committedSkin;
        private IDisposable realmSubscription;
        private int preparationSequence;
        private int dropdownRefreshSequence;
        private bool dropdownItemsLoading = true;
        private bool synchronisingDropdown;
        private bool savingAssignment;
        private bool moreExpanded;
        private Live<SkinInfo> requestedTarget;

        [Resolved] private SkinManager skins { get; set; }
        [Resolved] private RealmAccess realm { get; set; }
        [Resolved(CanBeNull = true)] private SkinOptionPreferences options { get; set; }
        [Resolved(CanBeNull = true)] private INotificationOverlay notificationOverlay { get; set; }
        [Resolved(CanBeNull = true)] private IBindable<RulesetInfo> currentRuleset { get; set; }
        [Resolved(CanBeNull = true)] private SkinEditorOverlay skinEditor { get; set; }

        [BackgroundDependencyLoader(permitNulls: true)]
        private void load()
        {
            string initialMode = currentRuleset?.Value?.ShortName;
            scope.Value = new SkinSelectionScope(initialMode == "mania" ? "mania" : "bms");
            targetInfo.Value = skins.GetConfiguredSkin(scope.Value);
            dropdownSelection.Value = targetInfo.Value;

            Children = new Drawable[]
            {
                new SkinInstallationStatus(),
                ScopeSelector = new SkinScopeSelector { HasCustomisation = hasCustomisation },
                assignmentStatus = text(string.Empty),
                overridesSummary = text(string.Empty),
                followDefaultButton = new SettingsButtonV2
                {
                    Text = "皮肤选择恢复跟随模式默认",
                    Action = () =>
                    {
                        if (skins.SelectionPreferences?.GetConfiguredSkinId(scope.Value) != null)
                            skins.SelectionPreferences.Reset(scope.Value);
                        else if (IsTargetReady)
                            skins.SelectionPreferences?.SetSkin(scope.Value, targetInfo.Value.ID);
                    },
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = content_padding,
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Masking = true,
                        CornerRadius = 8,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = new osuTK.Graphics.Color4(34, 33, 44, 255) },
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Padding = new MarginPadding(12),
                                Spacing = new Vector2(0, 8),
                                Children = new Drawable[]
                                {
                                    targetTitle = text(string.Empty, false),
                                    dropdown = new SkinDropdown
                                    {
                                        Caption = "更换此范围的皮肤",
                                        AlwaysShowSearchBar = true,
                                        AllowNonContiguousMatching = true,
                                        Current = dropdownSelection,
                                        Items = new[] { targetInfo.Value },
                                    },
                                    preparationStatus = text(string.Empty, false),
                                    moreButton = new SettingsButtonV2
                                    {
                                        Text = "皮肤管理与编辑",
                                        Padding = new MarginPadding(),
                                        Action = () => { moreExpanded = !moreExpanded; updateAuthoringState(); },
                                    },
                                    authorActions = new SkinAuthorActionsContainer
                                    {
                                        Padding = new MarginPadding(),
                                        Direction = FillDirection.Vertical,
                                        Children = new Drawable[]
                                        {
                                            new FillFlowContainer
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                Direction = FillDirection.Horizontal,
                                                Children = new Drawable[]
                                                {
                                                    renameButton = new RenameSkinButton { TargetSkinInfo = targetInfo, RelativeSizeAxes = Axes.X, Width = 1 / 3f, Padding = new MarginPadding { Right = 3 } },
                                                    exportButton = new ExportSkinButton { TargetSkinInfo = targetInfo, RelativeSizeAxes = Axes.X, Width = 1 / 3f, Padding = new MarginPadding { Horizontal = 3 } },
                                                    deleteButton = new DeleteSkinButton { TargetSkinInfo = targetInfo, RelativeSizeAxes = Axes.X, Width = 1 / 3f, Padding = new MarginPadding { Left = 3 } },
                                                },
                                            },
                                            layoutEditorButton = new SettingsButtonV2
                                            {
                                                Text = SkinSettingsStrings.SkinLayoutEditor,
                                                Padding = new MarginPadding(),
                                                TooltipText = "以这里的模式与键数打开布局编辑器；修改保存为独立副本。",
                                                Action = () => _ = openEditorAsync(),
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
                new SkinOptionsSettings(inspectedSkin, scope),
                new SkinScriptSettings(inspectedSkin),
                new ReloadCurrentSkinButton { RefreshCompleted = () => refreshTarget(force: true) },
                new OpenSkinFolderButton(),
            };
            ScopeSelector.Current.BindTo(scope);
        }

        private static readonly MarginPadding content_padding = SettingsPanel.CONTENT_PADDING;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            scope.BindValueChanged(_ => refreshTarget(), true);
            dropdownSelection.BindValueChanged(change =>
            {
                if (!synchronisingDropdown)
                    _ = prepareTargetAsync(change.NewValue, save: true);
            });
            if (skins.SelectionPreferences != null)
                skins.SelectionPreferences.Changed += assignmentsChanged;
            if (options != null)
                options.Changed += optionPreferencesChanged;
            committedSkin = skins.CurrentSkin.GetBoundCopy();
            committedSkin.BindValueChanged(_ => Schedule(() =>
            {
                if (requestedTarget == null)
                    refreshTarget(force: true);
            }));
            committedSkin.BindDisabledChanged(_ => updateAuthoringState());
            realmSubscription = realm.RegisterForNotifications(_ => realm.Realm.All<SkinInfo>().Where(s => !s.DeletePending)
                .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase), (_, _) => refreshDropdownItems());
            refreshDropdownItems();
        }

        private void assignmentsChanged()
        {
            if (!savingAssignment)
                Schedule(() => refreshTarget());
        }

        private void optionPreferencesChanged() => Schedule(updateAssignmentSummary);

        private bool hasCustomisation(SkinSelectionScope target)
        {
            if (skins.SelectionPreferences?.GetConfiguredSkinId(target) != null)
                return true;
            return target.KeyCount != null && options?.GetLocalOverrides(skins.GetConfiguredSkin(target).ID, target.RulesetId, target.KeyCount).Count > 0;
        }

        private void updateAssignmentSummary()
        {
            SkinSelectionScope target = scope.Value;
            Guid? assigned = skins.SelectionPreferences?.GetConfiguredSkinId(target);
            assignmentStatus.Text = "正在设置：" + target + (target.KeyCount == null ? string.Empty
                : assigned == null ? " · 跟随模式默认" : assigned == targetInfo.Value.ID ? " · 单独设置" : " · 原指定不可用，跟随模式默认");
            int[] customised = SkinSelectionScope.GetSupportedKeyCounts(target.RulesetId)
                .Where(keys => hasCustomisation(new SkinSelectionScope(target.RulesetId, keys))).ToArray();
            overridesSummary.Text = customised.Length == 0 ? "所有键数跟随模式默认。"
                : "已单独设置：" + string.Join("、", customised.Select(keys => keys + "K")) + "；其余键数跟随默认。";
            followDefaultButton.CanBeShown.Value = target.KeyCount != null;
            followDefaultButton.Enabled.Value = skins.SelectionPreferences != null && (assigned != null || IsTargetReady);
            followDefaultButton.Text = assigned == null ? $"将当前皮肤设为 {target.KeyCount}K 专用" : "皮肤选择恢复跟随模式默认";
            ScopeSelector.RefreshAssignments();
        }

        private void refreshTarget(bool force = false)
        {
            if (!IsDisposed)
            {
                Live<SkinInfo> target = skins.GetConfiguredSkin(scope.Value);
                if (!force && IsTargetReady && target.ID == preview.Skin.SkinInfo.ID)
                {
                    targetTitle.Text = target.PerformRead(info => info.Name + (string.IsNullOrWhiteSpace(info.Creator) ? string.Empty : "\n" + info.Creator));
                    synchroniseDropdown(target);
                    updateAssignmentSummary();
                    return;
                }
                _ = prepareTargetAsync(target, save: false);
            }
        }

        private async Task prepareTargetAsync(Live<SkinInfo> target, bool save)
        {
            int sequence = ++preparationSequence;
            SkinSelectionScope requestedScope = scope.Value;
            requestedTarget = target;
            preparationCancellation?.Cancel();
            preparationCancellation?.Dispose();
            preparationCancellation = new CancellationTokenSource();
            CancellationToken token = preparationCancellation.Token;
            targetInfo.Disabled = true;
            dropdownSelection.Disabled = true;
            inspectedSkin.Value = null;
            preparationStatus.Text = "正在读取皮肤与配置…";
            targetTitle.Text = target.ToString();
            updateAssignmentSummary();
            updateAuthoringState();

            try
            {
                SkinSettingsPreview prepared = await skins.PrepareSettingsSkinAsync(target, token).ConfigureAwait(false);
                lock (previewGate)
                {
                    if (stopping || token.IsCancellationRequested || sequence != preparationSequence)
                    {
                        prepared.Dispose();
                        return;
                    }
                    pendingPreviews.Add(prepared);
                }
                Schedule(() =>
                {
                    lock (previewGate)
                    {
                        if (!pendingPreviews.Remove(prepared))
                            return;
                    }
                    if (IsDisposed || token.IsCancellationRequested || sequence != preparationSequence || scope.Value != requestedScope)
                    {
                        prepared.Dispose();
                        return;
                    }
                    if (save && skins.SelectionPreferences != null)
                    {
                        savingAssignment = true;
                        try { skins.SelectionPreferences.SetSkin(requestedScope, target.ID); }
                        finally { savingAssignment = false; }
                    }
                    SkinSettingsPreview oldPreview = preview;
                    preview = prepared;
                    requestedTarget = null;
                    targetInfo.Disabled = false;
                    targetInfo.Value = target;
                    inspectedSkin.Value = prepared.Skin;
                    targetTitle.Text = target.PerformRead(info => info.Name + (string.IsNullOrWhiteSpace(info.Creator) ? string.Empty : "\n" + info.Creator));
                    preparationStatus.Text = "配置和下方操作均对应此皮肤。";
                    synchroniseDropdown(target);
                    dropdownSelection.Disabled = dropdownItemsLoading;
                    updateAssignmentSummary();
                    updateAuthoringState();
                    oldPreview?.Dispose();
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or UnauthorizedAccessException or GameplaySkinScenePreparationException)
            {
                Logger.Error(exception, "Failed to prepare the settings skin target.");
                Schedule(() =>
                {
                    if (IsDisposed || sequence != preparationSequence)
                        return;
                    preparationStatus.Text = "此皮肤未能读取，当前改动未保存。请选择其他皮肤或刷新后重试。";
                    requestedTarget = null;
                    dropdownSelection.Disabled = false;
                    synchroniseDropdown(skins.GetConfiguredSkin(requestedScope));
                });
            }
        }

        private void synchroniseDropdown(Live<SkinInfo> target)
        {
            synchronisingDropdown = true;
            bool disabled = dropdownSelection.Disabled;
            dropdownSelection.Disabled = false;
            dropdownSelection.Value = target;
            dropdownSelection.Disabled = disabled;
            synchronisingDropdown = false;
        }

        private void updateAuthoringState()
        {
            Live<SkinInfo> target = targetInfo.Value;
            if (target == null)
                return;
            bool ready = IsTargetReady && committedSkin?.Disabled != true;
            followDefaultButton.Enabled.Value = skins.SelectionPreferences != null
                                               && (skins.SelectionPreferences.GetConfiguredSkinId(scope.Value) != null || ready);
            bool modify = ready && skins.CanModify(target);
            bool export = ready && skins.CanExport(target);
            bool delete = ready && skins.CanDelete(target.ID);
            bool editor = ready && skinEditor != null && SkinAuthoringAvailability.LegacyEditorAvailable;
            moreButton.Enabled.Value = modify || export || delete || editor;
            moreButton.Text = moreExpanded ? "收起皮肤管理与编辑" : "皮肤管理与编辑";
            authorActions.CanBeShown.Value = moreExpanded && (modify || export || delete || editor);
            renameButton.CanBeShown.Value = modify;
            exportButton.CanBeShown.Value = export;
            deleteButton.CanBeShown.Value = delete;
            int count = (modify ? 1 : 0) + (export ? 1 : 0) + (delete ? 1 : 0);
            renameButton.Width = exportButton.Width = deleteButton.Width = count == 0 ? 1 : 1f / count;
            layoutEditorButton.CanBeShown.Value = editor;
            layoutEditorButton.Enabled.Value = editor;
        }

        private async Task openEditorAsync()
        {
            SkinEditorOpenResult result = await skinEditor.OpenForScopeAsync(scope.Value).ConfigureAwait(false);
            string message = result switch
            {
                SkinEditorOpenResult.Opened or SkinEditorOpenResult.AlreadyOpen => null,
                SkinEditorOpenResult.DifferentGameplayMode => "请先在选曲中切换到这里的玩法，再编辑此皮肤。",
                SkinEditorOpenResult.DifferentKeyCount => "请先选择这里所设置键数的谱面，再编辑对应布局。",
                SkinEditorOpenResult.CompatibleChartRequired => "请先选择可用于布局预览的谱面。",
                SkinEditorOpenResult.GameplayActive => "请结束当前游玩后再打开布局编辑器。",
                _ => "未能打开此皮肤的布局编辑器，请刷新后重试。",
            };
            if (message != null)
                Schedule(() => notificationOverlay?.Post(new SimpleErrorNotification { Text = message }));
        }

        private void refreshDropdownItems() => _ = refreshDropdownItemsAsync();

        private async Task refreshDropdownItemsAsync()
        {
            int sequence = ++dropdownRefreshSequence;
            var items = await skins.GetAllUsableSkinsAsync().ConfigureAwait(false);
            Schedule(() =>
            {
                if (IsDisposed || sequence != dropdownRefreshSequence)
                    return;
                dropdownItems.Clear();
                dropdownItems.AddRange(items);
                synchronisingDropdown = true;
                try { dropdown.Items = dropdownItems; }
                finally { synchronisingDropdown = false; }
                dropdownItemsLoading = false;
                dropdownSelection.Disabled = !IsTargetReady;
                if (requestedTarget == null || !dropdownItems.Any(item => item.ID == requestedTarget.ID))
                    refreshTarget();
            });
        }

        private static OsuTextFlowContainer text(string value, bool padded = true) => new()
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Padding = padded ? content_padding : new MarginPadding(),
            Text = value,
        };

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                SkinSettingsPreview[] pending;
                lock (previewGate)
                {
                    stopping = true;
                    pending = pendingPreviews.ToArray();
                    pendingPreviews.Clear();
                }
                foreach (SkinSettingsPreview prepared in pending)
                    prepared.Dispose();
                ++preparationSequence;
                ++dropdownRefreshSequence;
                preparationCancellation?.Cancel();
                preparationCancellation?.Dispose();
                if (skins?.SelectionPreferences != null)
                    skins.SelectionPreferences.Changed -= assignmentsChanged;
                if (options != null)
                    options.Changed -= optionPreferencesChanged;
                committedSkin?.UnbindAll();
                scope.UnbindAll();
                dropdownSelection.UnbindAll();
                realmSubscription?.Dispose();
            }
            base.Dispose(isDisposing);
            preview?.Dispose();
        }

        private partial class SkinDropdown : FormDropdown<Live<SkinInfo>>
        {
            protected override LocalisableString GenerateItemText(Live<SkinInfo> item) => item.ToString();
        }

        private partial class SkinAuthorActionsContainer : FillFlowContainer, IConditionalFilterable
        {
            public readonly BindableBool CanBeShown = new(true);
            IBindable<bool> IConditionalFilterable.CanBeShown => CanBeShown;
            public IEnumerable<LocalisableString> FilterTerms => Array.Empty<LocalisableString>();
            public bool MatchingFilter { set => this.FadeTo(value ? 1 : 0); }
            public bool FilteringActive { get; set; }
            public SkinAuthorActionsContainer()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
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
            public Action RefreshCompleted { get; init; }
            [Resolved]
            private SkinManager skins { get; set; }

            [Resolved(CanBeNull = true)]
            private INotificationOverlay notificationOverlay { get; set; }

            [Resolved(CanBeNull = true)]
            private OsuGame game { get; set; }

            private Bindable<Skin> currentSkin;
            private Task activeReload;

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

            private async Task observeReloadAsync()
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

                    RefreshCompleted?.Invoke();

                    if (!IsDisposed)
                        updateState();
                });
            }

            private void finishFolderRefresh(bool success)
            {
                activeReload = null;
                if (IsDisposed)
                    return;

                if (success)
                    RefreshCompleted?.Invoke();

                notificationOverlay?.Post(success
                    ? new SimpleNotification { Text = SkinSettingsStrings.SkinsRefreshed }
                    : new SimpleErrorNotification { Text = SkinSettingsStrings.SkinFolderRefreshFailed });
                updateState();
            }
        }

        public partial class RenameSkinButton : SettingsButtonV2, IHasPopover
        {
            public IBindable<Live<SkinInfo>> TargetSkinInfo { get; init; }
            private IBindable<Live<SkinInfo>> target;
            private Live<SkinInfo> targetInfo => target?.Value ?? currentSkin.Value.SkinInfo;
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
                target = TargetSkinInfo?.GetBoundCopy();
                target?.BindValueChanged(_ => updateState(), true);
                target?.BindDisabledChanged(_ => updateState());
            }

            private void updateState() => Enabled.Value = !currentSkin.Disabled && target?.Disabled != true && skins.CanModify(targetInfo);

            public Popover GetPopover()
            {
                return new RenameSkinPopover(targetInfo);
            }

            protected override void Dispose(bool isDisposing)
            {
                currentSkin?.UnbindAll();
                target?.UnbindAll();
                base.Dispose(isDisposing);
            }
        }

        public partial class ExportSkinButton : SettingsButtonV2
        {
            public IBindable<Live<SkinInfo>> TargetSkinInfo { get; init; }
            private IBindable<Live<SkinInfo>> target;
            private Live<SkinInfo> targetInfo => target?.Value ?? currentSkin.Value.SkinInfo;
            [Resolved]
            private SkinManager skins { get; set; }

            [Resolved(CanBeNull = true)]
            private INotificationOverlay notificationOverlay { get; set; }

            private Bindable<Skin> currentSkin;
            private Task activeExport;

            [BackgroundDependencyLoader]
            private void load()
            {
                Text = "导出皮肤包";
                Action = export;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                currentSkin = skins.CurrentSkin.GetBoundCopy();
                currentSkin.BindValueChanged(_ => updateState());
                currentSkin.BindDisabledChanged(_ => updateState(), true);
                target = TargetSkinInfo?.GetBoundCopy();
                target?.BindValueChanged(_ => updateState(), true);
                target?.BindDisabledChanged(_ => updateState());
            }

            private void updateState() => Enabled.Value = activeExport == null && !currentSkin.Disabled && target?.Disabled != true && skins.CanExport(targetInfo);

            private void export()
            {
                if (activeExport != null)
                    return;

                Live<SkinInfo> selected = targetInfo;
                Enabled.Value = false;
                activeExport = observeExportAsync(selected);
            }

            private async Task observeExportAsync(Live<SkinInfo> selected)
            {
                try
                {
                    await skins.ExportSkin(selected).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Could not export the inspected skin.");
                    Schedule(() => notificationOverlay?.Post(new SimpleErrorNotification { Text = "未能导出此皮肤，请刷新后重试。" }));
                }
                finally
                {
                    Schedule(() =>
                    {
                        activeExport = null;
                        if (!IsDisposed)
                            updateState();
                    });
                }
            }

            protected override void Dispose(bool isDisposing)
            {
                currentSkin?.UnbindAll();
                target?.UnbindAll();
                base.Dispose(isDisposing);
            }
        }

        public partial class DeleteSkinButton : DangerousSettingsButtonV2
        {
            public IBindable<Live<SkinInfo>> TargetSkinInfo { get; init; }
            private IBindable<Live<SkinInfo>> target;
            private Live<SkinInfo> targetInfo => target?.Value ?? currentSkin.Value.SkinInfo;
            [Resolved]
            private SkinManager skins { get; set; }

            [Resolved(CanBeNull = true)]
            private IDialogOverlay dialogOverlay { get; set; }

            [Resolved(CanBeNull = true)]
            private INotificationOverlay notificationOverlay { get; set; }

            private Bindable<Skin> currentSkin;
            private Task activeDeletion;

            [BackgroundDependencyLoader]
            private void load()
            {
                Text = "删除皮肤文件";
                Action = delete;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                currentSkin = skins.CurrentSkin.GetBoundCopy();
                currentSkin.BindValueChanged(_ => updateState());
                currentSkin.BindDisabledChanged(_ => updateState(), true);
                target = TargetSkinInfo?.GetBoundCopy();
                target?.BindValueChanged(_ => updateState(), true);
                target?.BindDisabledChanged(_ => updateState());
            }

            private void updateState() => Enabled.Value = activeDeletion == null
                                                          && !currentSkin.Disabled
                                                          && target?.Disabled != true
                                                          && skins.CanDelete(targetInfo.ID);

            private void delete()
            {
                Live<SkinInfo> target = targetInfo;
                string usages = skins.SelectionPreferences == null ? string.Empty
                    : string.Join("、", skins.SelectionPreferences.GetUsages(target.ID));
                string label = target.PerformRead(info => info.Name);
                if (!string.IsNullOrEmpty(usages))
                    label += "\n\n正在用于：" + usages + "\n删除皮肤文件后，这些设置将回落到模式默认或静线。";
                dialogOverlay?.Push(new SkinDeleteDialog(target.ID, label)
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

            private async Task observeDeletionAsync(Guid recordId)
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

            protected override void Dispose(bool isDisposing)
            {
                currentSkin?.UnbindAll();
                target?.UnbindAll();
                base.Dispose(isDisposing);
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

            private Task deletionTask;

            private async Task observeDeletionAsync(SkinManager manager)
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
            private readonly Live<SkinInfo> explicitTarget;
            [Resolved]
            private SkinManager skins { get; set; }

            private readonly FocusedTextBox textBox;

            public RenameSkinPopover(Live<SkinInfo> explicitTarget = null)
            {
                this.explicitTarget = explicitTarget;
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
                textBox.Text = (explicitTarget ?? skins.CurrentSkinInfo.Value).Value.Name;
                textBox.TakeFocus();

                base.PopIn();
            }

            private void rename()
            {
                skins.Rename(explicitTarget ?? skins.CurrentSkinInfo.Value, textBox.Text);
                PopOut();
            }
        }
    }
}
