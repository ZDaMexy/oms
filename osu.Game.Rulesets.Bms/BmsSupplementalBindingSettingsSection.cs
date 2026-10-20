// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using oms.Input;
using oms.Input.Devices;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Handlers.Mouse;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Bms.Input;
using osuTK;

namespace osu.Game.Rulesets.Bms
{
    public partial class BmsSupplementalBindingSettingsSection : CompositeDrawable, IFilterable
    {
        private const float mouse_capture_threshold = 8;
        private static WeakReference<BmsSupplementalBindingSettingsSection>? captureOwner;

        private readonly BmsRuleset ruleset;
        private readonly Func<IReadOnlyList<OmsHidDeviceInfo>> hidDeviceProvider;
        private readonly Func<string, OmsHidDeviceCaptureSession> captureSessionFactory;
        private readonly MouseHandler? configuredMouseHandler;
        private readonly Bindable<int> selectedVariant = new BindableInt();
        private readonly Bindable<SettingsNote.Data?> statusNote = new Bindable<SettingsNote.Data?>();
        private ActiveHidCapture? activeHidCapture;
        private ActiveMouseCapture? activeMouseCapture;
        private int hidDiscoverySequence;
        private bool discoveryPending;
        private IBindable<Visibility>? settingsVisibility;
        private Bindable<bool>? highPrecisionMouseInput;
        private IReadOnlyList<OmsHidDeviceInfo> connectedDevices = Array.Empty<OmsHidDeviceInfo>();

        private OsuTextFlowContainer detectedDevicesText = null!;
        private FillFlowContainer hidButtonRows = null!;
        private FillFlowContainer hidAxisRows = null!;
        private FillFlowContainer mouseAxisRows = null!;
        private OsuSpriteText hidButtonEmptyState = null!;
        private OsuSpriteText hidAxisEmptyState = null!;
        private OsuSpriteText mouseAxisEmptyState = null!;

        public IEnumerable<LocalisableString> FilterTerms => new LocalisableString[]
        {
            "OMS 扩展绑定",
            "OMS supplemental bindings",
            "扩展绑定",
            "supplemental",
            "绑定",
            "trigger",
            "触发器",
            "hid",
            "controller",
            "控制器",
            "mouse",
            "鼠标",
            "mouse axis",
            "鼠标轴",
            "高精度鼠标输入",
            "high precision",
            "relative input",
            "xinput",
            "信号测试",
            "诊断",
            "轴用途",
            "转盘",
            "摇杆",
            "deadzone",
        };

        private bool matchingFilter = true;

        public bool MatchingFilter
        {
            get => matchingFilter;
            set
            {
                if (matchingFilter == value)
                    return;

                matchingFilter = value;
                if (!value)
                    stopActiveCapture();
                this.FadeTo(value ? 1 : 0);
            }
        }

        public bool FilteringActive { get; set; }

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private SettingsOverlay? settingsOverlay { get; set; }

        [Resolved(canBeNull: true)]
        private INotificationOverlay? notificationOverlay { get; set; }

        [Resolved(canBeNull: true)]
        private IOmsMouseAxisSource? mouseAxisSource { get; set; }

        [Resolved(canBeNull: true)]
        private GameHost? gameHost { get; set; }

        public BmsSupplementalBindingSettingsSection(BmsRuleset ruleset)
            : this(ruleset, OmsHidDeviceDiscovery.GetConnectedDevices)
        {
        }

        internal BmsSupplementalBindingSettingsSection(BmsRuleset ruleset, Func<IReadOnlyList<OmsHidDeviceInfo>> hidDeviceProvider,
                                                      Func<string, OmsHidDeviceCaptureSession>? captureSessionFactory = null,
                                                      MouseHandler? configuredMouseHandler = null)
        {
            this.ruleset = ruleset;
            this.hidDeviceProvider = hidDeviceProvider;
            this.captureSessionFactory = captureSessionFactory ?? (identifier => new OmsHidDeviceCaptureSession(identifier));
            this.configuredMouseHandler = configuredMouseHandler;
            selectedVariant.Value = ruleset.AvailableVariants.First();
        }

        internal string DetectedDevicesSummary { get; private set; } = string.Empty;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            highPrecisionMouseInput = (configuredMouseHandler ?? gameHost?.AvailableInputHandlers.OfType<MouseHandler>().FirstOrDefault())?.UseRelativeMode.GetBoundCopy();

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, SettingsSection.ITEM_SPACING_V2),
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = SettingsPanel.CONTENT_PADDING,
                        Child = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 6),
                            Children = new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Text = "OMS 扩展绑定",
                                    Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                                },
                                new OsuTextFlowContainer(text => text.Font = OsuFont.GetFont(size: 14))
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Text = "键盘和手柄按键在标准输入面板中配置；这里配置专用 HID 按钮、HID 轴和鼠标移动。每条绑定可单次捕获，也可持续测试原始信号及当前设置的效果。修改后按“应用扩展绑定”保存到当前变体。",
                                },
                            }
                        }
                    },
                    new SettingsItemV2(new VariantDropdown(ruleset)
                    {
                        Caption = "变体",
                        Current = { BindTarget = selectedVariant },
                    })
                    {
                        ShowRevertToDefaultButton = false,
                        Keywords = new[] { "变体", "variant", "5k", "7k", "9k", "14k", "绑定", "bindings", "hid", "mouse", "鼠标" },
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = SettingsPanel.CONTENT_PADDING,
                        Child = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Full,
                            Spacing = new Vector2(10),
                            Children = new Drawable[]
                            {
                                createActionButton("刷新 HID 设备", refreshDetectedDevices, 150),
                                createActionButton("重新载入当前变体", reloadCurrentVariant, 150),
                                createActionButton("应用扩展绑定", applyCurrentVariant, 190),
                                createDangerousActionButton("清空当前变体", clearCurrentVariant, 150),
                            }
                        }
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = SettingsPanel.CONTENT_PADDING,
                        Child = detectedDevicesText = new OsuTextFlowContainer(text => text.Font = OsuFont.GetFont(size: 14))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                        }
                    },
                    new SettingsNote
                    {
                        RelativeSizeAxes = Axes.X,
                        Current = { BindTarget = statusNote },
                    },
                    createBindingSection(
                        "HID 按钮",
                        "绑定专用 HID 设备的数字按钮。",
                        "添加 HID 按钮",
                        addHidButtonRow,
                        out hidButtonRows,
                        out hidButtonEmptyState,
                        "尚未配置 HID 按钮绑定。"),
                    createBindingSection(
                        "HID 轴",
                        "选择轴的实际用途：相对移动、循环转盘、位置按键或摇杆模拟转动，再用设备信号测试确认方向和阈值。",
                        "添加 HID 轴",
                        addHidAxisRow,
                        out hidAxisRows,
                        out hidAxisEmptyState,
                        "尚未配置 HID 轴绑定。"),
                    createBindingSection(
                        "鼠标轴",
                        "当你希望用鼠标移动本身来触发动作时，可在这里绑定鼠标移动方向。",
                        "添加鼠标轴",
                        addMouseAxisRow,
                        out mouseAxisRows,
                        out mouseAxisEmptyState,
                        "尚未配置鼠标轴绑定。",
                        createMousePrecisionControl()),
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            selectedVariant.BindValueChanged(_ => reloadCurrentVariant(), true);
            if (settingsOverlay != null)
            {
                settingsVisibility = settingsOverlay.State.GetBoundCopy();
                settingsVisibility.BindValueChanged(value =>
                {
                    if (value.NewValue == Visibility.Hidden)
                        stopActiveCapture();
                });
            }
        }

        protected override void Update()
        {
            base.Update();
            pollActiveCapture();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                Interlocked.Increment(ref hidDiscoverySequence);
                settingsVisibility?.UnbindAll();
                stopActiveCapture();
                highPrecisionMouseInput?.UnbindAll();
            }

            base.Dispose(isDisposing);
        }

        private Drawable createMousePrecisionControl()
        {
            if (mouseAxisSource == null || highPrecisionMouseInput == null)
                return new OsuTextFlowContainer(text => text.Font = OsuFont.GetFont(size: 14))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Text = "当前宿主未提供可配置的相对鼠标输入；已保存的鼠标绑定仍会保留。",
                };

            return new SettingsItemV2(new FormCheckBox
            {
                Caption = "高精度鼠标输入",
                HintText = "鼠标轴游玩、捕获和测试需要开启。此项复用客户端原有鼠标设置并自动保存；仅在游玩或主动测试期间请求临时鼠标捕获。",
                Current = highPrecisionMouseInput,
            })
            {
                ShowRevertToDefaultButton = false,
                Keywords = new[] { "高精度", "鼠标", "relative", "raw", "mouse" },
            };
        }

        private Drawable createBindingSection(string title, string description, string addButtonText, Action addAction, out FillFlowContainer rowsContainer,
                                              out OsuSpriteText emptyStateText, string emptyText, Drawable? additionalControl = null)
        {
            rowsContainer = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 10),
            };

            emptyStateText = new OsuSpriteText
            {
                Text = emptyText,
                Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                Alpha = 0.5f,
            };

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = SettingsPanel.CONTENT_PADDING,
                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 8),
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Child = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(10, 0),
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Text = title,
                                        Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                                    },
                                    createActionButton(addButtonText, addAction, 130),
                                }
                            }
                        },
                        new OsuTextFlowContainer(text => text.Font = OsuFont.GetFont(size: 14))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Text = description,
                        },
                    }.Concat(additionalControl == null ? Array.Empty<Drawable>() : new Drawable[] { additionalControl })
                     .Append(emptyStateText).Append(rowsContainer).ToArray(),
                }
            };
        }

        private SettingsButtonV2 createActionButton(LocalisableString text, Action action, float width)
            => new SettingsButtonV2
            {
                RelativeSizeAxes = Axes.None,
                AutoSizeAxes = Axes.None,
                Width = width,
                Height = 40,
                Text = text,
                Action = action,
            };

        private DangerousSettingsButtonV2 createDangerousActionButton(LocalisableString text, Action action, float width)
            => new DangerousSettingsButtonV2
            {
                RelativeSizeAxes = Axes.None,
                AutoSizeAxes = Axes.None,
                Width = width,
                Height = 40,
                Text = text,
                Action = action,
            };

        private void refreshDetectedDevices()
            => _ = refreshDetectedDevicesAsync();

        private async Task refreshDetectedDevicesAsync()
        {
            int refreshSequence = Interlocked.Increment(ref hidDiscoverySequence);
            discoveryPending = true;

            updateDetectedDevicesSummary("检测到的 HID 设备：扫描中...");
            statusNote.Value = new SettingsNote.Data("正在扫描已连接的 HID 设备...", SettingsNote.Type.Informational);

            try
            {
                var devices = await EnumerateDetectedDevicesAsync().ConfigureAwait(false);

                Schedule(() =>
                {
                    if (IsDisposed || refreshSequence != hidDiscoverySequence)
                        return;

                    connectedDevices = devices;
                    discoveryPending = false;
                    updateRowDevices();
                    updateDetectedDevicesSummary(buildDetectedDeviceSummary());

                    if (connectedDevices.Count == 0)
                    {
                        statusNote.Value = new SettingsNote.Data("当前没有连接可用的 HID 设备；已保存的离线绑定会保留。", SettingsNote.Type.Informational);
                        return;
                    }

                    statusNote.Value = null;
                });
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to enumerate connected HID devices for the BMS supplemental bindings editor.");

                Schedule(() =>
                {
                    if (IsDisposed || refreshSequence != hidDiscoverySequence)
                        return;

                    connectedDevices = Array.Empty<OmsHidDeviceInfo>();
                    discoveryPending = false;
                    updateRowDevices();
                    updateDetectedDevicesSummary("检测到的 HID 设备：不可用。已保存的离线绑定会保留。");
                    statusNote.Value = new SettingsNote.Data("扫描 HID 设备失败，已保存的离线绑定会保留。", SettingsNote.Type.Warning);
                });
            }
        }

        private void updateDetectedDevicesSummary(string summary)
        {
            DetectedDevicesSummary = summary;
            detectedDevicesText.Text = summary;
        }

        internal Task<IReadOnlyList<OmsHidDeviceInfo>> EnumerateDetectedDevicesAsync()
            => Task.Run(hidDeviceProvider);

        private string buildDetectedDeviceSummary()
        {
            if (connectedDevices.Count == 0)
                return "检测到的 HID 设备：无。连接控制器后请点击“刷新 HID 设备”。";

            return "检测到的 HID 设备：\n" + string.Join("\n", connectedDevices.Select(device => $@"- {device.DisplayName}"));
        }

        private void reloadCurrentVariant()
        {
            stopActiveCapture();
            refreshDetectedDevices();

            hidButtonRows.Clear(true);
            hidAxisRows.Clear(true);
            mouseAxisRows.Clear(true);

            var actions = getAvailableActions();

            foreach (var binding in OmsBmsBindingSettingsStorage.GetSupplementalBindings(realm, selectedVariant.Value))
            {
                if (!OmsBmsActionMap.TryMapToBmsAction(selectedVariant.Value, binding.Action, out var action))
                    continue;

                foreach (var trigger in binding.HidButtonTriggers)
                    addHidButtonRow(actions, action, trigger);

                foreach (var trigger in binding.HidAxisTriggers)
                    addHidAxisRow(actions, action, trigger);

                foreach (var trigger in binding.MouseAxisTriggers)
                    addMouseAxisRow(actions, action, trigger);
            }

            updateEmptyStates();
        }

        private void clearCurrentVariant()
        {
            OmsBmsBindingSettingsStorage.SaveSupplementalBindings(realm, selectedVariant.Value, Array.Empty<OmsBinding>());
            reloadCurrentVariant();
            postInfo("已清空当前 BMS 变体的扩展绑定。");
        }

        private void applyCurrentVariant()
        {
            if (discoveryPending && hidButtonRows.Count + hidAxisRows.Count > 0)
            {
                postWarning("请等待 HID 设备扫描完成后再应用，避免把有多台候选设备的旧绑定保存为已确认状态。");
                return;
            }

            var errors = new List<string>();
            var triggersByAction = new Dictionary<OmsAction, List<OmsBindingTrigger>>();

            collectBindings(hidButtonRows.Children.OfType<SupplementalBindingRow>(), triggersByAction, errors);
            collectBindings(hidAxisRows.Children.OfType<SupplementalBindingRow>(), triggersByAction, errors);
            collectBindings(mouseAxisRows.Children.OfType<SupplementalBindingRow>(), triggersByAction, errors);

            if (errors.Count > 0)
            {
                statusNote.Value = new SettingsNote.Data(string.Join("\n", errors.Distinct()), SettingsNote.Type.Warning);
                notificationOverlay?.Post(new SimpleNotification
                {
                    Text = errors[0],
                });
                return;
            }

            var bindings = triggersByAction.Select(group => new OmsBinding(group.Key, group.Value.DistinctBy(getTriggerSignature).ToArray()))
                                           .OrderBy(binding => binding.Action)
                                           .ToArray();

            OmsBmsBindingSettingsStorage.SaveSupplementalBindings(realm, selectedVariant.Value, bindings);
            reloadCurrentVariant();
            postInfo("已保存当前 BMS 变体的扩展绑定。");
        }

        private void collectBindings(IEnumerable<SupplementalBindingRow> rows, Dictionary<OmsAction, List<OmsBindingTrigger>> triggersByAction, List<string> errors)
        {
            foreach (var row in rows)
            {
                if (!row.TryCreateBinding(selectedVariant.Value, out var action, out var trigger, out string? error))
                {
                    if (!string.IsNullOrWhiteSpace(error))
                        errors.Add(error);

                    continue;
                }

                if (!triggersByAction.TryGetValue(action, out var triggers))
                {
                    triggers = new List<OmsBindingTrigger>();
                    triggersByAction[action] = triggers;
                }

                triggers.Add(trigger);
            }
        }

        private void postInfo(string message)
        {
            statusNote.Value = new SettingsNote.Data(message, SettingsNote.Type.Informational);
            notificationOverlay?.Post(new SimpleNotification
            {
                Text = message,
            });
        }

        private IReadOnlyList<BmsAction> getAvailableActions()
            => ruleset.GetDefaultKeyBindings(selectedVariant.Value)
                      .Select(binding => binding.GetAction<BmsAction>())
                      .Distinct()
                      .ToArray();

        private void addHidButtonRow()
            => addHidButtonRow(getAvailableActions(), getAvailableActions().First());

        private void addHidButtonRow(IReadOnlyList<BmsAction> actions, BmsAction action)
            => addHidButtonRow(actions, action, null);

        private void addHidButtonRow(IReadOnlyList<BmsAction> actions, BmsAction action, OmsBindingTrigger? initialTrigger)
        {
            var row = initialTrigger.HasValue
                ? new HidButtonBindingRow(actions, action, initialTrigger.Value)
                : new HidButtonBindingRow(actions, action);

            row.CaptureRequested = toggleCapture;
            row.DiagnosticsRequested = toggleDiagnostics;
            row.UpdateDevices(connectedDevices);

            row.RemoveRequested = () =>
            {
                if (isActiveCaptureRow(row))
                    stopActiveCapture();

                hidButtonRows.Remove(row, true);
                updateEmptyStates();
            };

            hidButtonRows.Add(row);
            updateEmptyStates();
        }

        private void addHidAxisRow()
            => addHidAxisRow(getAvailableActions(), getAvailableActions().First());

        private void addHidAxisRow(IReadOnlyList<BmsAction> actions, BmsAction action)
            => addHidAxisRow(actions, action, null);

        private void addHidAxisRow(IReadOnlyList<BmsAction> actions, BmsAction action, OmsBindingTrigger? initialTrigger)
        {
            var row = initialTrigger.HasValue
                ? new HidAxisBindingRow(actions, action, initialTrigger.Value)
                : new HidAxisBindingRow(actions, action);

            row.CaptureRequested = toggleCapture;
            row.DiagnosticsRequested = toggleDiagnostics;
            row.UpdateDevices(connectedDevices);

            row.RemoveRequested = () =>
            {
                if (isActiveCaptureRow(row))
                    stopActiveCapture();

                hidAxisRows.Remove(row, true);
                updateEmptyStates();
            };

            hidAxisRows.Add(row);
            updateEmptyStates();
        }

        private void addMouseAxisRow()
            => addMouseAxisRow(getAvailableActions(), getAvailableActions().First());

        private void addMouseAxisRow(IReadOnlyList<BmsAction> actions, BmsAction action)
            => addMouseAxisRow(actions, action, null);

        private void addMouseAxisRow(IReadOnlyList<BmsAction> actions, BmsAction action, OmsBindingTrigger? initialTrigger)
        {
            var row = initialTrigger.HasValue
                ? new MouseAxisBindingRow(actions, action, initialTrigger.Value)
                : new MouseAxisBindingRow(actions, action);

            row.CaptureRequested = toggleCapture;
            row.DiagnosticsRequested = toggleDiagnostics;
            row.UpdateDevices(connectedDevices);

            row.RemoveRequested = () =>
            {
                if (isActiveCaptureRow(row))
                    stopActiveCapture();

                mouseAxisRows.Remove(row, true);
                updateEmptyStates();
            };

            mouseAxisRows.Add(row);
            updateEmptyStates();
        }

        private void updateEmptyStates()
        {
            hidButtonEmptyState.Alpha = hidButtonRows.Count == 0 ? 0.5f : 0;
            hidAxisEmptyState.Alpha = hidAxisRows.Count == 0 ? 0.5f : 0;
            mouseAxisEmptyState.Alpha = mouseAxisRows.Count == 0 ? 0.5f : 0;
        }

        private bool isActiveCaptureRow(SupplementalBindingRow row)
            => activeHidCapture?.Row == row || activeMouseCapture?.Row == row;

        private void toggleCapture(SupplementalBindingRow row)
        {
            if (!row.SupportsCapture)
                return;

            if (activeHidCapture?.Row == row && !activeHidCapture.IsDiagnostic || activeMouseCapture?.Row == row && !activeMouseCapture.IsDiagnostic)
            {
                stopActiveCapture();
                statusNote.Value = new SettingsNote.Data("已取消实时捕获。", SettingsNote.Type.Informational);
                return;
            }

            switch (row.CaptureKind)
            {
                case SupplementalBindingCaptureKind.Hid:
                    beginHidCapture(row);
                    break;

                case SupplementalBindingCaptureKind.MouseAxis:
                    beginMouseCapture(row);
                    break;
            }
        }

        private void toggleDiagnostics(SupplementalBindingRow row)
        {
            if (activeHidCapture?.Row == row && activeHidCapture.IsDiagnostic || activeMouseCapture?.Row == row && activeMouseCapture.IsDiagnostic)
            {
                stopActiveCapture();
                return;
            }

            if (row.CaptureKind == SupplementalBindingCaptureKind.Hid)
                beginHidCapture(row, diagnostic: true);
            else if (row.CaptureKind == SupplementalBindingCaptureKind.MouseAxis)
                beginMouseCapture(row, diagnostic: true);
        }

        private void beginHidCapture(SupplementalBindingRow row, bool diagnostic = false)
        {
            if (discoveryPending)
            {
                postWarning("正在扫描设备，请扫描完成后再开始捕获或测试。");
                return;
            }

            if (!row.TryPrepareHidCapture(connectedDevices, out string deviceIdentifier, out string? validationError))
            {
                postWarning(validationError ?? "无法为这一行启动 HID 捕获。");
                return;
            }

            stopActiveCapture();
            claimCaptureOwnership();
            activeHidCapture = new ActiveHidCapture(row, captureSessionFactory(deviceIdentifier), diagnostic);
            row.SetCaptureState(!diagnostic);
            row.SetDiagnosticsState(diagnostic);
            if (diagnostic)
            {
                row.SetDiagnosticsText("正在连接设备；调整本行设置会重新开始预览计数。测试不会发送游玩输入。");
                return;
            }
            statusNote.Value = new SettingsNote.Data($@"正在等待来自 {deviceIdentifier} 的 HID 输入。请按下或拨动你要绑定的控件。", SettingsNote.Type.Informational);
        }

        private void beginMouseCapture(SupplementalBindingRow row, bool diagnostic = false)
        {
            if (mouseAxisSource == null)
            {
                postWarning("当前宿主没有提供相对鼠标输入，鼠标轴捕获和测试不可用。");
                return;
            }

            stopActiveCapture();
            claimCaptureOwnership();
            activeMouseCapture = new ActiveMouseCapture(row, mouseAxisSource, diagnostic);
            row.SetCaptureState(!diagnostic);
            row.SetDiagnosticsState(diagnostic);
            if (diagnostic)
            {
                row.SetDiagnosticsText("正在测试鼠标移动；调整本行设置会重新开始预览计数。测试不会发送游玩输入。");
                return;
            }
            statusNote.Value = new SettingsNote.Data("正在等待鼠标移动。请朝你要绑定的方向移动鼠标。", SettingsNote.Type.Informational);
        }

        private void pollActiveCapture()
        {
            pollMouseCapture();
            pollHidCapture();
        }

        private void pollHidCapture()
        {
            if (activeHidCapture == null)
                return;

            ActiveHidCapture active = activeHidCapture;
            if (active.Row is HidBindingRow hidRow && hidRow.DeviceIdentifier.Value != active.Session.DeviceIdentifier)
            {
                stopActiveCapture();
                beginHidCapture(hidRow, active.IsDiagnostic);
                return;
            }
            IReadOnlyList<OmsHidDeviceChange> changes = active.Session.PollOnce();
            if (!active.IsDiagnostic)
            {
                foreach (var change in changes)
                {
                    if (!active.Row.TryApplyCapturedChange(change, out string? successMessage))
                        continue;
                    stopActiveCapture();
                    postInfo(successMessage ?? "已捕获 HID 输入。");
                    return;
                }
                return;
            }

            double now = Stopwatch.GetTimestamp() * (1000.0 / Stopwatch.Frequency);
            if (!active.Session.HasConnectedDevice)
            {
                active.Preview?.Disconnect();
                active.LastAxes.Clear();
                active.LastButtons.Clear();
                active.Row.SetDiagnosticsText(active.Session.IsDiscovering ? "正在后台查找设备…" : "设备未连接，等待重新连接。");
                return;
            }

            foreach (OmsHidDeviceChange change in changes)
                if (change.Kind == OmsHidDeviceChangeKind.Axis)
                    active.LastAxes[change.AxisChange.AxisIndex] = change.AxisChange;
                else
                    active.LastButtons[change.ButtonChange.ButtonIndex] = change.ButtonChange;

            if (!preparePreview(active, out string? error))
            {
                active.Row.SetDiagnosticsText(error ?? "当前设置无效，请先修正本行设置。");
                return;
            }

            active.Preview!.BeginPoll(now);
            foreach (OmsHidDeviceChange change in changes)
            {
                active.Preview.Apply(change);
            }
            active.Preview.EndPoll();
            if (now >= active.NextTextUpdate)
            {
                active.NextTextUpdate = now + 100;
                active.Row.SetDiagnosticsText(active.Preview.Describe(connected: true));
            }
        }

        private bool preparePreview(ActiveDiagnostic active, out string? error)
        {
            if (!active.Row.TryCreateBinding(selectedVariant.Value, out OmsAction action, out OmsBindingTrigger trigger, out error))
            {
                active.Preview?.Dispose();
                active.Preview = null;
                return false;
            }

            if (active.Preview == null || active.Action != action || getTriggerSignature(active.Trigger) != getTriggerSignature(trigger))
            {
                active.Preview?.Dispose();
                active.Preview = new BmsInputDiagnostics(action, trigger);
                active.Action = action;
                active.Trigger = trigger;
                active.NextTextUpdate = 0;
                if (active is ActiveHidCapture hid)
                {
                    foreach (OmsHidAxisChange axis in hid.LastAxes.Values)
                    {
                        if (axis.Value is int value)
                            active.Preview.Apply(OmsHidDeviceChange.Axis(new OmsHidAxisChange(axis.DeviceIdentifier, axis.AxisIndex, 0, value, axis.Minimum, axis.Maximum, axis.IsRelative)));
                    }
                    foreach (OmsHidButtonChange button in hid.LastButtons.Values)
                        active.Preview.Apply(OmsHidDeviceChange.Button(button));
                }
            }
            return true;
        }

        private void pollMouseCapture()
        {
            if (activeMouseCapture == null)
                return;

            ActiveMouseCapture active = activeMouseCapture;
            bool reset = active.DrainMovements();
            if (reset || !active.Source.IsActive)
            {
                active.Preview?.Disconnect();
                active.AccumulatedDelta = Vector2.Zero;
            }
            if (!active.Source.IsActive)
            {
                active.ClearMovements();
                if (active.IsDiagnostic)
                    active.Row.SetDiagnosticsText("相对鼠标输入当前不可用；请保持游戏窗口激活并启用高精度鼠标输入。等待恢复。");
                else if (!active.WaitingForSource)
                    statusNote.Value = new SettingsNote.Data("正在等待相对鼠标输入；请保持游戏窗口激活并启用高精度鼠标输入。", SettingsNote.Type.Informational);
                active.WaitingForSource = true;
                return;
            }
            active.WaitingForSource = false;

            if (active.IsDiagnostic)
            {
                if (!preparePreview(active, out string? error))
                {
                    active.Row.SetDiagnosticsText(error ?? "当前设置无效，请先修正本行设置。");
                    return;
                }
                double now = Stopwatch.GetTimestamp() * (1000.0 / Stopwatch.Frequency);
                active.Preview!.BeginPoll(now);
                foreach (OmsRelativeMouseMovement movement in active.Movements)
                    active.Preview.ApplyMouse(movement.X, movement.Y);
                active.Preview.EndPoll();
                if (now >= active.NextTextUpdate)
                {
                    active.NextTextUpdate = now + 100;
                    active.Row.SetDiagnosticsText(active.Preview.Describe(connected: true));
                }
                return;
            }

            foreach (OmsRelativeMouseMovement movement in active.Movements)
            {
                active.AccumulatedDelta += new Vector2(movement.X, movement.Y);
                if (!OmsMouseAxisCapture.TryResolve(active.AccumulatedDelta.X, active.AccumulatedDelta.Y, mouse_capture_threshold, out var axis, out var direction))
                    continue;
                if (!active.Row.TryApplyCapturedMouseAxis(axis, direction, out string? successMessage))
                    continue;
                stopActiveCapture();
                postInfo(successMessage ?? "已捕获鼠标轴输入。");
                return;
            }
        }

        private void stopActiveCapture()
        {
            if (captureOwner?.TryGetTarget(out BmsSupplementalBindingSettingsSection? owner) == true && ReferenceEquals(owner, this))
                captureOwner = null;
            if (activeHidCapture != null)
            {
                activeHidCapture.Row.SetCaptureState(false);
                activeHidCapture.Row.SetDiagnosticsState(false);
                activeHidCapture.Preview?.Dispose();
                retireSession(activeHidCapture.Session);
                activeHidCapture = null;
            }

            if (activeMouseCapture != null)
            {
                activeMouseCapture.Row.SetCaptureState(false);
                activeMouseCapture.Row.SetDiagnosticsState(false);
                activeMouseCapture.Preview?.Dispose();
                activeMouseCapture.Dispose();
                activeMouseCapture = null;
            }
        }

        private void claimCaptureOwnership()
        {
            if (captureOwner?.TryGetTarget(out BmsSupplementalBindingSettingsSection? previous) == true && !ReferenceEquals(previous, this))
                previous.stopActiveCapture();
            captureOwner = new WeakReference<BmsSupplementalBindingSettingsSection>(this);
        }

        private void postWarning(string message)
        {
            statusNote.Value = new SettingsNote.Data(message, SettingsNote.Type.Warning);
            notificationOverlay?.Post(new SimpleNotification
            {
                Text = message,
            });
        }

        private void updateRowDevices()
        {
            foreach (SupplementalBindingRow row in hidButtonRows.Children.Concat(hidAxisRows.Children).OfType<SupplementalBindingRow>())
                row.UpdateDevices(connectedDevices);
        }

        private static (OmsBindingTriggerKind Kind, string? Device, int Index, OmsMouseAxis Mouse, OmsAxisDirection Direction, bool Inverted, OmsAxisSettings Settings)
            getTriggerSignature(OmsBindingTrigger trigger)
            => (trigger.Kind, trigger.DeviceIdentifier, trigger.Kind == OmsBindingTriggerKind.HidButton ? trigger.ButtonIndex : trigger.AxisIndex,
                trigger.MouseAxisKind, trigger.AxisDirection, trigger.AxisInverted, trigger.AxisSettings);

        private static void retireSession(OmsHidDeviceCaptureSession session)
        {
            // The task owns the closed session until discovery and driver cleanup finish. Closing
            // or disposing a settings panel must not wait for a driver to return on its caller.
            _ = Task.Run(session.Dispose).ContinueWith(static task => Logger.Error(task.Exception!, "Failed to release an input diagnostics session."),
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }

        private abstract class ActiveDiagnostic
        {
            public SupplementalBindingRow Row { get; }
            public bool IsDiagnostic { get; }
            public BmsInputDiagnostics? Preview { get; set; }
            public OmsAction Action { get; set; }
            public OmsBindingTrigger Trigger { get; set; }
            public double NextTextUpdate { get; set; }

            protected ActiveDiagnostic(SupplementalBindingRow row, bool diagnostic)
            {
                Row = row;
                IsDiagnostic = diagnostic;
            }
        }

        private sealed class ActiveHidCapture : ActiveDiagnostic
        {
            public OmsHidDeviceCaptureSession Session { get; }
            public Dictionary<int, OmsHidAxisChange> LastAxes { get; } = new Dictionary<int, OmsHidAxisChange>();
            public Dictionary<int, OmsHidButtonChange> LastButtons { get; } = new Dictionary<int, OmsHidButtonChange>();

            public ActiveHidCapture(SupplementalBindingRow row, OmsHidDeviceCaptureSession session, bool diagnostic)
                : base(row, diagnostic)
            {
                Session = session;
            }
        }

        private sealed class ActiveMouseCapture : ActiveDiagnostic, IDisposable
        {
            private readonly object gate = new object();
            private readonly Queue<OmsRelativeMouseMovement> pendingMovements = new Queue<OmsRelativeMouseMovement>();
            private readonly IDisposable captureLease;
            private bool resetPending;
            private bool closed;

            public IOmsMouseAxisSource Source { get; }
            public List<OmsRelativeMouseMovement> Movements { get; } = new List<OmsRelativeMouseMovement>();
            public Vector2 AccumulatedDelta { get; set; }
            public bool WaitingForSource { get; set; }

            public ActiveMouseCapture(SupplementalBindingRow row, IOmsMouseAxisSource source, bool diagnostic)
                : base(row, diagnostic)
            {
                Source = source;
                captureLease = source.AcquireCapture();
                source.RelativeMouseMoved += enqueueMovement;
                source.InputReset += reset;
            }

            private void enqueueMovement(OmsRelativeMouseMovement movement)
            {
                lock (gate)
                    if (!closed)
                        pendingMovements.Enqueue(movement);
            }

            private void reset()
            {
                lock (gate)
                {
                    if (closed)
                        return;
                    pendingMovements.Clear();
                    resetPending = true;
                }
            }

            public bool DrainMovements()
            {
                Movements.Clear();
                lock (gate)
                {
                    bool reset = resetPending;
                    resetPending = false;
                    while (pendingMovements.TryDequeue(out OmsRelativeMouseMovement movement))
                        Movements.Add(movement);
                    return reset;
                }
            }

            public void ClearMovements()
            {
                lock (gate)
                    pendingMovements.Clear();
                Movements.Clear();
            }

            public void Dispose()
            {
                lock (gate)
                {
                    closed = true;
                    pendingMovements.Clear();
                }
                Source.RelativeMouseMoved -= enqueueMovement;
                Source.InputReset -= reset;
                captureLease.Dispose();
            }
        }

        internal enum SupplementalBindingCaptureKind
        {
            None,
            Hid,
            MouseAxis,
        }

        internal abstract partial class SupplementalBindingRow : CompositeDrawable
        {
            private readonly IReadOnlyList<BmsAction> availableActions;
            private SettingsButtonV2? captureButton;
            private SettingsButtonV2? diagnosticsButton;
            private OsuTextFlowContainer diagnosticsText = null!;
            private string diagnosticsSummary = string.Empty;

            protected readonly Bindable<BmsAction> SelectedAction = new Bindable<BmsAction>();

            public Action? RemoveRequested { get; set; }
            public Action<SupplementalBindingRow>? CaptureRequested { get; set; }
            public Action<SupplementalBindingRow>? DiagnosticsRequested { get; set; }

            public virtual SupplementalBindingCaptureKind CaptureKind => SupplementalBindingCaptureKind.None;

            public bool SupportsCapture => CaptureKind != SupplementalBindingCaptureKind.None;

            protected abstract string RowTitle { get; }

            protected SupplementalBindingRow(IReadOnlyList<BmsAction> availableActions, BmsAction initialAction)
            {
                this.availableActions = availableActions;
                SelectedAction.Value = initialAction;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChild = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, SettingsSection.ITEM_SPACING_V2),
                    Children = new[] { createHeader() }.Concat(CreateFields()).Append(new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = SettingsPanel.CONTENT_PADDING,
                        Child = diagnosticsText = new OsuTextFlowContainer(text => text.Font = OsuFont.GetFont(size: 14))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Alpha = 0,
                        },
                    }).ToArray()
                };
            }

            private Drawable createHeader()
            {
                var children = new List<Drawable>
                {
                    new OsuSpriteText
                    {
                        Text = RowTitle,
                        Font = OsuFont.GetFont(size: 16, weight: FontWeight.SemiBold),
                    }
                };

                if (SupportsCapture)
                {
                    children.Add(captureButton = new SettingsButtonV2
                    {
                        RelativeSizeAxes = Axes.None,
                        AutoSizeAxes = Axes.None,
                        Width = 110,
                        Height = 36,
                        Text = "开始捕获",
                        Action = () => CaptureRequested?.Invoke(this),
                    });
                }

                children.Add(diagnosticsButton = new SettingsButtonV2
                {
                    RelativeSizeAxes = Axes.None,
                    AutoSizeAxes = Axes.None,
                    Width = 150,
                    Height = 36,
                    Text = "测试设备信号",
                    Action = () => DiagnosticsRequested?.Invoke(this),
                });

                children.Add(new DangerousSettingsButtonV2
                {
                    RelativeSizeAxes = Axes.None,
                    AutoSizeAxes = Axes.None,
                    Width = 80,
                    Height = 36,
                    Text = "移除",
                    Action = () => RemoveRequested?.Invoke(),
                });

                return new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Full,
                        Spacing = new Vector2(10, 6),
                        Children = children.ToArray()
                    }
                };
            }

            public virtual void UpdateDevices(IReadOnlyList<OmsHidDeviceInfo> devices) { }

            public void SetDiagnosticsState(bool active)
            {
                if (diagnosticsButton != null)
                    diagnosticsButton.Text = active ? "停止设备测试" : "测试设备信号";
                if (diagnosticsText != null)
                    diagnosticsText.Alpha = active ? 1 : 0;
            }

            public void SetDiagnosticsText(string text)
            {
                if (diagnosticsSummary != text)
                {
                    diagnosticsSummary = text;
                    diagnosticsText.Text = text;
                }
            }

            protected abstract IEnumerable<Drawable> CreateFields();

            protected SettingsItemV2 CreateSetting(IFormControl control)
                => new SettingsItemV2(control)
                {
                    ShowRevertToDefaultButton = false,
                };

            protected SettingsItemV2 CreateActionSetting()
                => CreateSetting(new BmsActionDropdown(availableActions)
                {
                    Caption = "动作",
                    Current = { BindTarget = SelectedAction },
                });

            public bool TryCreateBinding(int variant, out OmsAction action, out OmsBindingTrigger trigger, out string? validationError)
            {
                if (!OmsBmsActionMap.TryMapToOmsAction(variant, SelectedAction.Value, out action))
                {
                    trigger = default;
                    validationError = $@"{RowTitle}：当前选中的动作不适用于当前变体。";
                    return false;
                }

                return TryCreateTrigger(out trigger, out validationError);
            }

            public virtual bool TryPrepareHidCapture(IReadOnlyList<OmsHidDeviceInfo> connectedDevices, out string deviceIdentifier, out string? validationError)
            {
                deviceIdentifier = string.Empty;
                validationError = null;
                return false;
            }

            public virtual bool TryApplyCapturedChange(OmsHidDeviceChange change, out string? successMessage)
            {
                successMessage = null;
                return false;
            }

            public virtual bool TryApplyCapturedMouseAxis(OmsMouseAxis axis, OmsAxisDirection direction, out string? successMessage)
            {
                successMessage = null;
                return false;
            }

            public void SetCaptureState(bool active)
            {
                if (captureButton != null)
                    captureButton.Text = active ? "取消捕获" : "开始捕获";
            }

            protected abstract bool TryCreateTrigger(out OmsBindingTrigger trigger, out string? validationError);
        }

        internal abstract partial class HidBindingRow : SupplementalBindingRow
        {
            internal readonly Bindable<string> DeviceIdentifier = new Bindable<string>(string.Empty);
            private IReadOnlyList<OmsHidDeviceInfo> devices = Array.Empty<OmsHidDeviceInfo>();
            private HidDeviceDropdown? deviceDropdown;

            public override SupplementalBindingCaptureKind CaptureKind => SupplementalBindingCaptureKind.Hid;

            protected HidBindingRow(IReadOnlyList<BmsAction> actions, BmsAction action)
                : base(actions, action)
            {
                DeviceIdentifier.BindValueChanged(value => deviceDropdown?.UpdateSelectionHint(value.NewValue));
            }

            public override void UpdateDevices(IReadOnlyList<OmsHidDeviceInfo> inventory)
            {
                devices = inventory;
                deviceDropdown?.SetDevices(devices, DeviceIdentifier.Value);
            }

            protected Drawable CreateDeviceSetting()
            {
                deviceDropdown = new HidDeviceDropdown(devices, DeviceIdentifier.Value)
                {
                    Caption = "设备",
                    Current = { BindTarget = DeviceIdentifier },
                };
                return CreateSetting(deviceDropdown);
            }

            protected bool TryResolveDevice(bool requireConnected, out string identifier, out string? error)
            {
                identifier = OmsHidDeviceIdentifier.Normalize(DeviceIdentifier.Value);
                if (identifier.Length == 0)
                {
                    if (requireConnected && devices.Count == 1)
                        identifier = devices[0].Identifier;
                    else
                    {
                        error = "请先从设备列表选择这一条绑定使用的设备。";
                        return false;
                    }
                }

                if (OmsHidDeviceDiscovery.TryResolveIdentifier(identifier, devices, out string canonical, out string? diagnostic))
                {
                    identifier = canonical;
                    DeviceIdentifier.Value = canonical;
                    error = null;
                    return true;
                }

                // A missing saved device is retained; a model-level binding with several candidates is ambiguous.
                int candidates = 0;
                foreach (OmsHidDeviceInfo device in devices)
                    if (device.LegacyIdentifier == identifier)
                        candidates++;
                if (candidates > 1 || requireConnected)
                {
                    error = diagnostic;
                    return false;
                }
                error = null;
                return true;
            }

            public override bool TryPrepareHidCapture(IReadOnlyList<OmsHidDeviceInfo> inventory, out string identifier, out string? error)
            {
                UpdateDevices(inventory);
                return TryResolveDevice(requireConnected: true, out identifier, out error);
            }
        }

        internal sealed partial class HidButtonBindingRow : HidBindingRow
        {
            internal readonly Bindable<string> ButtonIndex = new Bindable<string>("0");
            protected override string RowTitle => "HID 按钮绑定";

            public HidButtonBindingRow(IReadOnlyList<BmsAction> actions, BmsAction action, OmsBindingTrigger? trigger = null)
                : base(actions, action)
            {
                if (trigger.HasValue)
                {
                    DeviceIdentifier.Value = trigger.Value.DeviceIdentifier ?? string.Empty;
                    ButtonIndex.Value = trigger.Value.ButtonIndex.ToString(CultureInfo.InvariantCulture);
                }
            }

            protected override IEnumerable<Drawable> CreateFields()
            {
                yield return CreateActionSetting();
                yield return CreateDeviceSetting();
                yield return CreateSetting(new FormNumberBox
                {
                    Caption = "按钮索引",
                    HintText = "从 0 开始；也可以点击“开始捕获”后按下设备按钮。",
                    Current = { BindTarget = ButtonIndex },
                });
            }

            protected override bool TryCreateTrigger(out OmsBindingTrigger trigger, out string? error)
            {
                trigger = default;
                if (!TryResolveDevice(false, out string identifier, out error))
                    return false;
                if (!int.TryParse(ButtonIndex.Value, out int index) || index < 0)
                {
                    error = "HID 按钮索引必须是从 0 开始的整数。";
                    return false;
                }
                trigger = OmsBindingTrigger.HidButton(identifier, index);
                return true;
            }

            public override bool TryApplyCapturedChange(OmsHidDeviceChange change, out string? message)
            {
                message = null;
                if (change.Kind != OmsHidDeviceChangeKind.Button || !change.ButtonChange.Pressed)
                    return false;
                DeviceIdentifier.Value = change.ButtonChange.DeviceIdentifier;
                ButtonIndex.Value = change.ButtonChange.ButtonIndex.ToString(CultureInfo.InvariantCulture);
                message = $"已捕获 HID 按钮 {change.ButtonChange.ButtonIndex}。";
                return true;
            }
        }

        internal sealed partial class HidAxisBindingRow : HidBindingRow
        {
            internal readonly Bindable<string> AxisIndex = new Bindable<string>("0");
            internal readonly Bindable<OmsAxisDirection> AxisDirection = new Bindable<OmsAxisDirection>(OmsAxisDirection.Positive);
            internal readonly Bindable<bool> AxisInverted = new BindableBool();
            internal readonly AxisSettingsEditor AxisSettings;
            protected override string RowTitle => "HID 轴绑定";

            public HidAxisBindingRow(IReadOnlyList<BmsAction> actions, BmsAction action, OmsBindingTrigger? trigger = null)
                : base(actions, action)
            {
                AxisSettings = new AxisSettingsEditor(trigger?.AxisSettings ?? OmsAxisSettings.Default, mouseOnly: false);
                if (trigger.HasValue)
                {
                    DeviceIdentifier.Value = trigger.Value.DeviceIdentifier ?? string.Empty;
                    AxisIndex.Value = trigger.Value.AxisIndex.ToString(CultureInfo.InvariantCulture);
                    AxisDirection.Value = trigger.Value.AxisDirection;
                    AxisInverted.Value = trigger.Value.AxisInverted;
                }
            }

            protected override IEnumerable<Drawable> CreateFields()
            {
                yield return CreateActionSetting();
                yield return CreateDeviceSetting();
                yield return CreateSetting(new FormNumberBox
                {
                    Caption = "轴索引",
                    HintText = "从 0 开始；捕获会等待你实际移动轴，不采用首次静止采样。",
                    Current = { BindTarget = AxisIndex },
                });
                yield return CreateSetting(new AxisDirectionDropdown { Caption = "触发方向", Current = { BindTarget = AxisDirection } });
                yield return CreateSetting(new FormCheckBox
                {
                    Caption = "反转轴向",
                    HintText = "先反转输入，再匹配触发方向。请用设备测试核对。",
                    Current = { BindTarget = AxisInverted },
                });
                yield return AxisSettings;
            }

            protected override bool TryCreateTrigger(out OmsBindingTrigger trigger, out string? error)
            {
                trigger = default;
                if (!TryResolveDevice(false, out string identifier, out error))
                    return false;
                if (!int.TryParse(AxisIndex.Value, out int index) || index < 0)
                {
                    error = "HID 轴索引必须是从 0 开始的整数。";
                    return false;
                }
                if (!AxisSettings.TryBuild(out OmsAxisSettings settings, out error))
                    return false;
                trigger = OmsBindingTrigger.HidAxis(identifier, index, AxisDirection.Value, AxisInverted.Value, settings);
                return true;
            }

            public override bool TryApplyCapturedChange(OmsHidDeviceChange change, out string? message)
            {
                message = null;
                if (change.Kind != OmsHidDeviceChangeKind.Axis || change.AxisChange.Delta == 0)
                    return false;
                double delta = change.AxisChange.Delta;
                if (AxisSettings.Mode.Value == OmsAxisMode.CircularPosition && AxisSettings.TryBuild(out OmsAxisSettings settings, out _))
                {
                    OmsHidAxisChange axis = change.AxisChange;
                    int? minimum = settings.Minimum ?? axis.Minimum;
                    int? maximum = settings.Maximum ?? axis.Maximum;
                    if (!axis.IsRelative && axis.Value.HasValue && minimum.HasValue && maximum.HasValue && minimum.Value < maximum.Value)
                    {
                        long previousValue = (long)axis.Value.Value - axis.Delta;
                        if (previousValue >= minimum.Value && previousValue <= maximum.Value
                            && axis.Value.Value >= minimum.Value && axis.Value.Value <= maximum.Value)
                        {
                            double period = (long)maximum.Value - minimum.Value + 1;
                            // Capture the physical direction across the circular seam, as gameplay does.
                            if (delta > period / 2)
                                delta -= period;
                            else if (delta < -period / 2)
                                delta += period;
                        }
                    }
                }
                if (delta == 0)
                    return false;
                DeviceIdentifier.Value = change.AxisChange.DeviceIdentifier;
                AxisIndex.Value = change.AxisChange.AxisIndex.ToString(CultureInfo.InvariantCulture);
                AxisDirection.Value = delta > 0 ? OmsAxisDirection.Positive : OmsAxisDirection.Negative;
                AxisInverted.Value = false;
                message = $"已捕获 HID 轴 {change.AxisChange.AxisIndex}（{getAxisDirectionText(AxisDirection.Value)}）；请确认轴模式后测试。";
                return true;
            }
        }

        internal sealed partial class MouseAxisBindingRow : SupplementalBindingRow
        {
            internal readonly Bindable<OmsMouseAxis> MouseAxis = new Bindable<OmsMouseAxis>(OmsMouseAxis.X);
            internal readonly Bindable<OmsAxisDirection> AxisDirection = new Bindable<OmsAxisDirection>(OmsAxisDirection.Positive);
            internal readonly Bindable<bool> AxisInverted = new BindableBool();
            internal readonly AxisSettingsEditor AxisSettings;
            public override SupplementalBindingCaptureKind CaptureKind => SupplementalBindingCaptureKind.MouseAxis;
            protected override string RowTitle => "鼠标移动绑定";

            public MouseAxisBindingRow(IReadOnlyList<BmsAction> actions, BmsAction action, OmsBindingTrigger? trigger = null)
                : base(actions, action)
            {
                AxisSettings = new AxisSettingsEditor(trigger?.AxisSettings ?? OmsAxisSettings.Default, mouseOnly: true);
                if (trigger.HasValue)
                {
                    MouseAxis.Value = trigger.Value.MouseAxisKind;
                    AxisDirection.Value = trigger.Value.AxisDirection;
                    AxisInverted.Value = trigger.Value.AxisInverted;
                }
            }

            protected override IEnumerable<Drawable> CreateFields()
            {
                yield return CreateActionSetting();
                yield return CreateSetting(new MouseAxisDropdown { Caption = "鼠标轴", Current = { BindTarget = MouseAxis } });
                yield return CreateSetting(new AxisDirectionDropdown { Caption = "触发方向", Current = { BindTarget = AxisDirection } });
                yield return CreateSetting(new FormCheckBox { Caption = "反转轴向", Current = { BindTarget = AxisInverted } });
                yield return AxisSettings;
            }

            protected override bool TryCreateTrigger(out OmsBindingTrigger trigger, out string? error)
            {
                trigger = default;
                if (!AxisSettings.TryBuild(out OmsAxisSettings settings, out error))
                    return false;
                trigger = OmsBindingTrigger.MouseAxis(MouseAxis.Value, AxisDirection.Value, AxisInverted.Value, settings);
                return true;
            }

            public override bool TryApplyCapturedMouseAxis(OmsMouseAxis axis, OmsAxisDirection direction, out string? message)
            {
                MouseAxis.Value = axis;
                AxisDirection.Value = direction;
                AxisInverted.Value = false;
                message = $"已捕获鼠标 {getMouseAxisText(axis)}（{getAxisDirectionText(direction)}）。";
                return true;
            }
        }

        private sealed partial class HidDeviceDropdown : FormDropdown<string>
        {
            private readonly Dictionary<string, string> labels = new Dictionary<string, string>(StringComparer.Ordinal);

            public HidDeviceDropdown(IReadOnlyList<OmsHidDeviceInfo> devices, string selected)
            {
                SetDevices(devices, selected);
            }

            public void SetDevices(IReadOnlyList<OmsHidDeviceInfo> devices, string selected)
            {
                labels.Clear();
                labels.Add(string.Empty, "请选择设备");
                int ordinal = 0;
                foreach (OmsHidDeviceInfo device in devices)
                {
                    string name = device.DisplayName;
                    string trailingIdentifier = $" ({device.Identifier})";
                    if (name.EndsWith(trailingIdentifier, StringComparison.Ordinal))
                        name = name[..^trailingIdentifier.Length];
                    string identity = device.Identifier.StartsWith("dinput:instance_", StringComparison.Ordinal)
                        ? device.Identifier["dinput:instance_".Length..] : device.Identifier;
                    if (identity.Length > 8)
                        identity = identity[..8];
                    labels[device.Identifier] = $"{name} · 设备 {++ordinal:00} [{identity}]";
                }
                if (!string.IsNullOrWhiteSpace(selected) && !labels.ContainsKey(selected))
                {
                    bool resolved = OmsHidDeviceDiscovery.TryResolveIdentifier(selected, devices, out string canonical, out _);
                    labels[selected] = resolved ? $"旧绑定 → {labels[canonical]}" : devices.Count(device => device.LegacyIdentifier == selected) > 1
                        ? "旧绑定匹配多台设备，请重新选择" : $"未连接 · {selected}";
                }
                Items = labels.Keys.ToArray();
                UpdateSelectionHint(selected);
            }

            public void UpdateSelectionHint(string identifier)
                => HintText = $"同型号设备按各自标识区分，请用设备测试确认。离线绑定会保留；旧绑定匹配多台时需重新选择。\n当前设备：{identifier}";

            protected override LocalisableString GenerateItemText(string item) => labels[item];
        }

        internal sealed partial class AxisSettingsEditor : CompositeDrawable
        {
            internal readonly Bindable<OmsAxisMode> Mode;
            internal readonly Bindable<string> Sensitivity;
            internal readonly Bindable<string> MovementThreshold;
            internal readonly Bindable<string> Deadzone;
            internal readonly Bindable<string> Activation;
            internal readonly Bindable<string> Release;
            internal readonly Bindable<string> RevolutionsPerSecond;
            internal readonly Bindable<string> StopTimeout;
            internal readonly Bindable<string> Minimum;
            internal readonly Bindable<string> Maximum;
            internal readonly Bindable<string> Centre;
            internal readonly Bindable<bool> Advanced = new BindableBool();
            private readonly bool mouseOnly;
            private FillFlowContainer? fields;

            public AxisSettingsEditor(OmsAxisSettings settings, bool mouseOnly)
            {
                this.mouseOnly = mouseOnly;
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Mode = new Bindable<OmsAxisMode>(mouseOnly ? OmsAxisMode.RelativeMotion : settings.Mode);
                Sensitivity = text(settings.Sensitivity);
                MovementThreshold = text(settings.MovementThreshold);
                Deadzone = text(settings.Deadzone * 100);
                Activation = text(settings.ActivationThreshold * 100);
                Release = text(settings.ReleaseThreshold * 100);
                RevolutionsPerSecond = text(settings.RevolutionsPerSecond);
                StopTimeout = text(settings.StopTimeoutMilliseconds);
                Minimum = new Bindable<string>(settings.Minimum?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                Maximum = new Bindable<string>(settings.Maximum?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                Centre = new Bindable<string>(settings.Centre?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                Advanced.Value = settings.Minimum.HasValue || settings.Centre.HasValue;
                Mode.BindValueChanged(_ => rebuildFields());
                Advanced.BindValueChanged(_ => rebuildFields());
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                var content = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, SettingsSection.ITEM_SPACING_V2),
                };
                if (!mouseOnly)
                    content.Add(setting(new AxisModeDropdown { Caption = "轴用途", Current = { BindTarget = Mode } }));
                content.Add(fields = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, SettingsSection.ITEM_SPACING_V2),
                });
                InternalChild = content;
                rebuildFields();
            }

            private void rebuildFields()
            {
                if (fields == null)
                    return;
                fields.Clear(true);
                fields.Add(new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Child = new OsuTextFlowContainer(text => text.Font = OsuFont.GetFont(size: 14))
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Text = Mode.Value switch
                        {
                            OmsAxisMode.RelativeMotion => "按上报的移动差值触发；位移阈值使用 counts。鼠标使用这种模式。",
                            OmsAxisMode.CircularPosition => "用于首尾相连的绝对位置转盘，跨范围边界时取最短方向；位移阈值使用转。",
                            OmsAxisMode.CentredPosition => "偏离中心达到触发阈值时按下，低于释放阈值才松开；回到中心始终松开。停在偏转处会持续按住。",
                            OmsAxisMode.StickToMotion => "将摇杆偏转转换成持续转动；偏转越大转得越快，回到中心死区后停止。",
                            _ => throw new ArgumentOutOfRangeException(),
                        },
                    },
                });
                if (Mode.Value == OmsAxisMode.CentredPosition)
                {
                    fields.Add(number("触发阈值（%）", Activation, "偏离中心的百分比，范围 1～100。"));
                    fields.Add(number("释放阈值（%）", Release, "低于此阈值才松开；回到中心始终松开。必须小于或等于触发阈值。"));
                }
                else
                {
                    fields.Add(number("灵敏度（倍）", Sensitivity, "位移或转动速度的倍数，范围 0.01～100。"));
                    fields.Add(number(Mode.Value == OmsAxisMode.RelativeMotion ? "累计位移阈值（counts）" : "累计位移阈值（转）",
                        MovementThreshold, "灵敏度调整后的累计移动超过阈值后触发；反向移动会抵消尚未触发的位移，0 保留每次非零移动。"));
                    if (Mode.Value == OmsAxisMode.StickToMotion)
                    {
                        fields.Add(number("中心死区（%）", Deadzone, "0～99；消除摇杆中心漂移。"));
                        fields.Add(number("满偏转速度（转/秒）", RevolutionsPerSecond, "摇杆完全偏转时的速度，之后再乘灵敏度，范围 0.01～100。"));
                    }
                    fields.Add(number("停止等待（ms）", StopTimeout, "连续运动停顿多久后视为停止，范围 1～250；不会推迟命中。"));
                }
                if (Mode.Value == OmsAxisMode.RelativeMotion)
                    return;
                fields.Add(setting(new FormCheckBox
                {
                    Caption = "高级范围／中心校准",
                    HintText = "留空采用设备报告的范围和范围中点；只有设备报告不合适时才需要填写。",
                    Current = { BindTarget = Advanced },
                }));
                if (!Advanced.Value)
                    return;
                fields.Add(number("最小值（留空自动）", Minimum, "设备原始整数；最小值与最大值必须一起填写。"));
                fields.Add(number("最大值（留空自动）", Maximum, "必须大于最小值。"));
                if (Mode.Value != OmsAxisMode.CircularPosition)
                    fields.Add(number("中心值（留空自动）", Centre, "设备原始值；必须严格位于实际范围内部。只填写中心值时，范围仍取自设备，可用设备测试核对。"));
            }

            private static Drawable setting(IFormControl control) => new SettingsItemV2(control) { ShowRevertToDefaultButton = false };

            private static Drawable number(string caption, Bindable<string> value, string hint) => setting(new FormNumberBox(allowDecimals: true)
            {
                Caption = caption,
                HintText = hint,
                Current = { BindTarget = value },
            });

            private static Bindable<string> text(double value) => new Bindable<string>(value.ToString(CultureInfo.InvariantCulture));

            internal bool TryBuild(out OmsAxisSettings settings, out string? error)
            {
                settings = null!;
                error = null;
                bool position = Mode.Value == OmsAxisMode.CentredPosition;
                bool stick = Mode.Value == OmsAxisMode.StickToMotion;
                double sensitivity = 1, threshold = 0, deadzone = 10, activation = 50, release = 40, speed = 1, stop = 50;
                if (mouseOnly && Mode.Value != OmsAxisMode.RelativeMotion)
                {
                    error = "鼠标移动只支持相对移动模式。";
                    return false;
                }
                if (position)
                {
                    if (!read(Activation, "触发阈值", out activation, out error) || !read(Release, "释放阈值", out release, out error))
                        return false;
                }
                else
                {
                    if (!read(Sensitivity, "灵敏度", out sensitivity, out error) || !read(MovementThreshold, "累计位移阈值", out threshold, out error)
                        || !read(StopTimeout, "停止等待", out stop, out error))
                        return false;
                    if (stick && (!read(Deadzone, "中心死区", out deadzone, out error) || !read(RevolutionsPerSecond, "满偏转速度", out speed, out error)))
                        return false;
                }

                int? minimum = null, maximum = null;
                double? centre = null;
                if (Advanced.Value && Mode.Value != OmsAxisMode.RelativeMotion)
                {
                    if (!string.IsNullOrWhiteSpace(Minimum.Value))
                    {
                        if (!int.TryParse(Minimum.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                        {
                            error = "最小值必须是设备原始整数，或留空采用设备范围。";
                            return false;
                        }
                        minimum = parsed;
                    }
                    if (!string.IsNullOrWhiteSpace(Maximum.Value))
                    {
                        if (!int.TryParse(Maximum.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                        {
                            error = "最大值必须是设备原始整数，或留空采用设备范围。";
                            return false;
                        }
                        maximum = parsed;
                    }
                    if (Mode.Value != OmsAxisMode.CircularPosition && !string.IsNullOrWhiteSpace(Centre.Value))
                    {
                        if (!read(Centre, "中心值", out double parsed, out error))
                            return false;
                        centre = parsed;
                    }
                }
                try
                {
                    settings = new OmsAxisSettings(Mode.Value, sensitivity, threshold, deadzone / 100, activation / 100, release / 100,
                        speed, stop, minimum, maximum, centre);
                    return true;
                }
                catch (ArgumentException exception)
                {
                    error = exception.ParamName switch
                    {
                        "sensitivity" => "灵敏度必须在 0.01～100 倍之间。",
                        "movementThreshold" => "累计位移阈值必须在 0～1000000 之间。",
                        "deadzone" => "中心死区必须在 0～99% 之间。",
                        "activationThreshold" => "触发阈值必须在 1～100% 之间。",
                        "releaseThreshold" => "释放阈值必须在 0 与触发阈值之间。",
                        "revolutionsPerSecond" => "满偏转速度必须在 0.01～100 转/秒之间。",
                        "stopTimeoutMilliseconds" => "停止等待必须在 1～250 ms 之间。",
                        "centre" => "中心值必须在设备最小值与最大值之间。",
                        _ => "最小值与最大值必须一起填写，且最小值小于最大值。",
                    };
                    return false;
                }
            }

            private static bool read(Bindable<string> value, string caption, out double number, out string? error)
            {
                if (double.TryParse(value.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number))
                {
                    error = null;
                    return true;
                }
                error = $"{caption}必须填写有效数字，小数使用小数点。";
                return false;
            }
        }

        private sealed partial class AxisModeDropdown : FormDropdown<OmsAxisMode>
        {
            public AxisModeDropdown() => Items = Enum.GetValues<OmsAxisMode>();

            protected override LocalisableString GenerateItemText(OmsAxisMode item) => item switch
            {
                OmsAxisMode.RelativeMotion => "相对移动",
                OmsAxisMode.CircularPosition => "循环位置（转盘）",
                OmsAxisMode.CentredPosition => "位置按键（带回差）",
                OmsAxisMode.StickToMotion => "摇杆模拟转动",
                _ => throw new ArgumentOutOfRangeException(nameof(item)),
            };
        }

        private sealed partial class VariantDropdown : FormDropdown<int>
        {
            private readonly BmsRuleset ruleset;

            public VariantDropdown(BmsRuleset ruleset)
            {
                this.ruleset = ruleset;
                Items = ruleset.AvailableVariants.ToList();
            }

            protected override LocalisableString GenerateItemText(int item)
                => ruleset.GetVariantName(item);
        }

        private sealed partial class BmsActionDropdown : FormDropdown<BmsAction>
        {
            public BmsActionDropdown(IEnumerable<BmsAction> actions)
            {
                Items = actions.ToList();
            }

            protected override LocalisableString GenerateItemText(BmsAction item)
                => item.GetLocalisableDescription();
        }

        private sealed partial class AxisDirectionDropdown : FormDropdown<OmsAxisDirection>
        {
            public AxisDirectionDropdown()
            {
                Items = new[] { OmsAxisDirection.Positive, OmsAxisDirection.Negative };
            }

            protected override LocalisableString GenerateItemText(OmsAxisDirection item)
                => getAxisDirectionText(item);
        }

        private sealed partial class MouseAxisDropdown : FormDropdown<OmsMouseAxis>
        {
            public MouseAxisDropdown()
            {
                Items = new[] { OmsMouseAxis.X, OmsMouseAxis.Y };
            }

            protected override LocalisableString GenerateItemText(OmsMouseAxis item)
                => getMouseAxisText(item);
        }

        private static string getAxisDirectionText(OmsAxisDirection item)
            => item == OmsAxisDirection.Positive ? "正向" : "负向";

        private static string getMouseAxisText(OmsMouseAxis item)
            => item == OmsMouseAxis.X ? "X 轴" : "Y 轴";
    }
}
