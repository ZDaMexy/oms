// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
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
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
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

        private readonly BmsRuleset ruleset;
        private readonly Func<IReadOnlyList<OmsHidDeviceInfo>> hidDeviceProvider;
        private readonly Bindable<int> selectedVariant = new BindableInt();
        private readonly Bindable<SettingsNote.Data?> statusNote = new Bindable<SettingsNote.Data?>();
        private ActiveHidCapture? activeHidCapture;
        private ActiveMouseCapture? activeMouseCapture;
        private int hidDiscoverySequence;
        private string detectedDevicesSummary = string.Empty;

        private IReadOnlyList<OmsHidDeviceInfo> connectedDevices = Array.Empty<OmsHidDeviceInfo>();
        private InputManager? inputManager;

        private OsuTextFlowContainer detectedDevicesText = null!;
        private FillFlowContainer hidButtonRows = null!;
        private FillFlowContainer hidAxisRows = null!;
        private FillFlowContainer mouseAxisRows = null!;
        private OsuSpriteText hidButtonEmptyState = null!;
        private OsuSpriteText hidAxisEmptyState = null!;
        private OsuSpriteText mouseAxisEmptyState = null!;

        public IEnumerable<LocalisableString> FilterTerms => new LocalisableString[]
        {
            "OMS supplemental bindings",
            "supplemental",
            "trigger",
            "hid",
            "controller",
            "mouse",
            "mouse axis",
            "xinput",
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
                this.FadeTo(value ? 1 : 0);
            }
        }

        public bool FilteringActive { get; set; }

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private INotificationOverlay? notificationOverlay { get; set; }

        public BmsSupplementalBindingSettingsSection(BmsRuleset ruleset)
            : this(ruleset, OmsHidDeviceDiscovery.GetConnectedDevices)
        {
        }

        internal BmsSupplementalBindingSettingsSection(BmsRuleset ruleset, Func<IReadOnlyList<OmsHidDeviceInfo>> hidDeviceProvider)
        {
            this.ruleset = ruleset;
            this.hidDeviceProvider = hidDeviceProvider;
            selectedVariant.Value = ruleset.AvailableVariants.First();
        }

        internal string DetectedDevicesSummary => detectedDevicesSummary;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

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
                                    Text = "OMS supplemental bindings",
                                    Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                                },
                                new OsuTextFlowContainer(text => text.Font = OsuFont.GetFont(size: 14))
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Text = "Keyboard and XInput bindings stay in the standard input panel. Use this editor for HID buttons, HID axes, and mouse-axis triggers that cannot be stored in regular key bindings. Changes stay local until you press Apply supplemental bindings.",
                                },
                            }
                        }
                    },
                    new SettingsItemV2(new VariantDropdown(ruleset)
                    {
                        Caption = "Variant",
                        Current = { BindTarget = selectedVariant },
                    })
                    {
                        ShowRevertToDefaultButton = false,
                        Keywords = new[] { "variant", "5k", "7k", "9k", "14k", "bindings", "hid", "mouse" },
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
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(10, 0),
                            Children = new Drawable[]
                            {
                                createActionButton("Refresh HID devices", refreshDetectedDevices, 150),
                                createActionButton("Reload current variant", reloadCurrentVariant, 150),
                                createActionButton("Apply supplemental bindings", applyCurrentVariant, 190),
                                createDangerousActionButton("Clear current variant", clearCurrentVariant, 150),
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
                        "HID buttons",
                        "Bind digital buttons from dedicated HID devices.",
                        "Add HID button",
                        addHidButtonRow,
                        out hidButtonRows,
                        out hidButtonEmptyState,
                        "No HID button bindings configured."),
                    createBindingSection(
                        "HID axes",
                        "Bind one direction of an HID axis. Axis direction and inversion are evaluated exactly as the gameplay input bridge uses them.",
                        "Add HID axis",
                        addHidAxisRow,
                        out hidAxisRows,
                        out hidAxisEmptyState,
                        "No HID axis bindings configured."),
                    createBindingSection(
                        "Mouse axes",
                        "Bind mouse movement directions when you want movement itself to trigger an action.",
                        "Add mouse axis",
                        addMouseAxisRow,
                        out mouseAxisRows,
                        out mouseAxisEmptyState,
                        "No mouse-axis bindings configured."),
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            inputManager = GetContainingInputManager();

            selectedVariant.BindValueChanged(_ => reloadCurrentVariant(), true);
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
                stopActiveCapture();
            }

            base.Dispose(isDisposing);
        }

        private Drawable createBindingSection(string title, string description, string addButtonText, Action addAction, out FillFlowContainer rowsContainer, out OsuSpriteText emptyStateText, string emptyText)
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
                        emptyStateText,
                        rowsContainer,
                    }
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

            updateDetectedDevicesSummary("Detected HID devices: scanning...");
            statusNote.Value = new SettingsNote.Data("Scanning connected HID devices...", SettingsNote.Type.Informational);

            try
            {
                var devices = await enumerateDetectedDevicesAsync().ConfigureAwait(false);

                Schedule(() =>
                {
                    if (IsDisposed || refreshSequence != hidDiscoverySequence)
                        return;

                    connectedDevices = devices;
                    updateDetectedDevicesSummary(buildDetectedDeviceSummary());

                    if (connectedDevices.Count == 0)
                    {
                        statusNote.Value = new SettingsNote.Data("No compatible HID devices are currently connected. You can still enter a device identifier manually.", SettingsNote.Type.Informational);
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
                    updateDetectedDevicesSummary("Detected HID devices: unavailable. You can still enter a device identifier manually.");
                    statusNote.Value = new SettingsNote.Data("Failed to enumerate HID devices. Manual identifier entry is still available.", SettingsNote.Type.Warning);
                });
            }
        }

        private void updateDetectedDevicesSummary(string summary)
        {
            detectedDevicesSummary = summary;
            detectedDevicesText.Text = summary;
        }

        internal Task<IReadOnlyList<OmsHidDeviceInfo>> enumerateDetectedDevicesAsync()
            => Task.Run(hidDeviceProvider);

        private string buildDetectedDeviceSummary()
        {
            if (connectedDevices.Count == 0)
                return "Detected HID devices: none. Use Refresh HID devices after connecting a controller, or type a device identifier manually.";

            return "Detected HID devices:\n" + string.Join("\n", connectedDevices.Select(device => $@"- {device.DisplayName}"));
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
            postInfo("Cleared supplemental bindings for the current BMS variant.");
        }

        private void applyCurrentVariant()
        {
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
            postInfo("Saved supplemental bindings for the current BMS variant.");
        }

        private void collectBindings(IEnumerable<SupplementalBindingRow> rows, Dictionary<OmsAction, List<OmsBindingTrigger>> triggersByAction, List<string> errors)
        {
            foreach (var row in rows)
            {
                if (!row.TryCreateBinding(selectedVariant.Value, out var action, out var trigger, out var error))
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

            if (isActiveCaptureRow(row))
            {
                stopActiveCapture();
                statusNote.Value = new SettingsNote.Data("Cancelled live capture.", SettingsNote.Type.Informational);
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

        private void beginHidCapture(SupplementalBindingRow row)
        {
            refreshDetectedDevices();

            if (!row.TryPrepareHidCapture(connectedDevices, out string deviceIdentifier, out string? validationError))
            {
                postWarning(validationError ?? "Unable to start HID capture for this row.");
                return;
            }

            stopActiveCapture();
            activeHidCapture = new ActiveHidCapture(row, new OmsHidDeviceCaptureSession(deviceIdentifier));
            row.SetCaptureState(true);
            statusNote.Value = new SettingsNote.Data($@"Waiting for HID input from {deviceIdentifier}. Press or move the control you want to bind.", SettingsNote.Type.Informational);
        }

        private void beginMouseCapture(SupplementalBindingRow row)
        {
            inputManager ??= GetContainingInputManager();

            if (inputManager == null)
            {
                postWarning("Mouse-axis capture is unavailable before the settings panel is attached to an input manager.");
                return;
            }

            stopActiveCapture();
            activeMouseCapture = new ActiveMouseCapture(row, inputManager.CurrentState.Mouse.Position);
            row.SetCaptureState(true);
            statusNote.Value = new SettingsNote.Data("Waiting for mouse movement. Move the mouse in the direction you want to bind.", SettingsNote.Type.Informational);
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

            foreach (var change in activeHidCapture.Session.PollOnce())
            {
                if (!activeHidCapture.Row.TryApplyCapturedChange(change, out string? successMessage))
                    continue;

                stopActiveCapture();
                postInfo(successMessage ?? "Captured HID input.");
                return;
            }
        }

        private void pollMouseCapture()
        {
            if (activeMouseCapture == null || inputManager == null)
                return;

            Vector2 currentPosition = inputManager.CurrentState.Mouse.Position;
            activeMouseCapture.AccumulatedDelta += currentPosition - activeMouseCapture.LastMousePosition;
            activeMouseCapture.LastMousePosition = currentPosition;

            if (!OmsMouseAxisCapture.TryResolve(activeMouseCapture.AccumulatedDelta.X, activeMouseCapture.AccumulatedDelta.Y, mouse_capture_threshold, out var axis, out var direction))
                return;

            if (!activeMouseCapture.Row.TryApplyCapturedMouseAxis(axis, direction, out string? successMessage))
                return;

            stopActiveCapture();
            postInfo(successMessage ?? "Captured mouse-axis input.");
        }

        private void stopActiveCapture()
        {
            if (activeHidCapture != null)
            {
                activeHidCapture.Row.SetCaptureState(false);
                activeHidCapture.Session.Dispose();
                activeHidCapture = null;
            }

            if (activeMouseCapture != null)
            {
                activeMouseCapture.Row.SetCaptureState(false);
                activeMouseCapture = null;
            }
        }

        private void postWarning(string message)
        {
            statusNote.Value = new SettingsNote.Data(message, SettingsNote.Type.Warning);
            notificationOverlay?.Post(new SimpleNotification
            {
                Text = message,
            });
        }

        private static string getTriggerSignature(OmsBindingTrigger trigger)
        {
            switch (trigger.Kind)
            {
                case OmsBindingTriggerKind.HidButton:
                    return $@"{trigger.Kind}:{trigger.DeviceIdentifier}:{trigger.ButtonIndex}";

                case OmsBindingTriggerKind.HidAxis:
                    return $@"{trigger.Kind}:{trigger.DeviceIdentifier}:{trigger.AxisIndex}:{(int)trigger.AxisDirection}:{trigger.AxisInverted}";

                case OmsBindingTriggerKind.MouseAxis:
                    return $@"{trigger.Kind}:{(int)trigger.MouseAxisKind}:{(int)trigger.AxisDirection}:{trigger.AxisInverted}";

                default:
                    return trigger.Kind.ToString();
            }
        }

        private static bool tryResolveCaptureDeviceIdentifier(Bindable<string> deviceIdentifier, IReadOnlyList<OmsHidDeviceInfo> connectedDevices, out string resolvedIdentifier, out string? validationError)
        {
            resolvedIdentifier = OmsHidDeviceIdentifier.Normalize(deviceIdentifier.Value);

            if (string.IsNullOrWhiteSpace(resolvedIdentifier) && connectedDevices.Count == 1)
                resolvedIdentifier = connectedDevices[0].Identifier;

            if (string.IsNullOrWhiteSpace(resolvedIdentifier))
            {
                validationError = connectedDevices.Count == 0
                    ? "Connect a HID device or enter a device identifier before starting capture."
                    : "Enter a device identifier before starting capture, or leave it blank with exactly one connected HID device.";
                return false;
            }

            deviceIdentifier.Value = resolvedIdentifier;
            validationError = null;
            return true;
        }

        private sealed class ActiveHidCapture
        {
            public SupplementalBindingRow Row { get; }

            public OmsHidDeviceCaptureSession Session { get; }

            public ActiveHidCapture(SupplementalBindingRow row, OmsHidDeviceCaptureSession session)
            {
                Row = row;
                Session = session;
            }
        }

        private sealed class ActiveMouseCapture
        {
            public SupplementalBindingRow Row { get; }

            public Vector2 LastMousePosition { get; set; }

            public Vector2 AccumulatedDelta { get; set; }

            public ActiveMouseCapture(SupplementalBindingRow row, Vector2 lastMousePosition)
            {
                Row = row;
                LastMousePosition = lastMousePosition;
                AccumulatedDelta = Vector2.Zero;
            }
        }

        private enum SupplementalBindingCaptureKind
        {
            None,
            Hid,
            MouseAxis,
        }

        private abstract partial class SupplementalBindingRow : CompositeDrawable
        {
            private readonly IReadOnlyList<BmsAction> availableActions;
            private SettingsButtonV2? captureButton;

            protected readonly Bindable<BmsAction> SelectedAction = new Bindable<BmsAction>();

            public Action? RemoveRequested { get; set; }
            public Action<SupplementalBindingRow>? CaptureRequested { get; set; }

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
                    Children = new[] { createHeader() }.Concat(CreateFields()).ToArray()
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
                        Width = 140,
                        Height = 36,
                        Text = "Start capture",
                        Action = () => CaptureRequested?.Invoke(this),
                    });
                }

                children.Add(new DangerousSettingsButtonV2
                {
                    RelativeSizeAxes = Axes.None,
                    AutoSizeAxes = Axes.None,
                    Width = 120,
                    Height = 36,
                    Text = "Remove",
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
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(10, 0),
                        Children = children.ToArray()
                    }
                };
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
                    Caption = "Action",
                    Current = { BindTarget = SelectedAction },
                });

            public bool TryCreateBinding(int variant, out OmsAction action, out OmsBindingTrigger trigger, out string? validationError)
            {
                if (!OmsBmsActionMap.TryMapToOmsAction(variant, SelectedAction.Value, out action))
                {
                    trigger = default;
                    validationError = $@"{RowTitle}: action {SelectedAction.Value} is not available for the current variant.";
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
                    captureButton.Text = active ? "Cancel capture" : "Start capture";
            }

            protected abstract bool TryCreateTrigger(out OmsBindingTrigger trigger, out string? validationError);
        }

        private sealed partial class HidButtonBindingRow : SupplementalBindingRow
        {
            private readonly Bindable<string> deviceIdentifier = new Bindable<string>(string.Empty);
            private readonly Bindable<string> buttonIndex = new Bindable<string>("0");

            public override SupplementalBindingCaptureKind CaptureKind => SupplementalBindingCaptureKind.Hid;

            protected override string RowTitle => "HID button binding";

            public HidButtonBindingRow(IReadOnlyList<BmsAction> availableActions, BmsAction initialAction)
                : base(availableActions, initialAction)
            {
            }

            public HidButtonBindingRow(IReadOnlyList<BmsAction> availableActions, BmsAction initialAction, OmsBindingTrigger trigger)
                : this(availableActions, initialAction)
            {
                deviceIdentifier.Value = trigger.DeviceIdentifier ?? string.Empty;
                buttonIndex.Value = trigger.ButtonIndex.ToString();
            }

            protected override IEnumerable<Drawable> CreateFields()
            {
                yield return CreateActionSetting();
                yield return CreateSetting(new FormTextBox
                {
                    Caption = "Device identifier",
                    HintText = "Use a detected HID identifier such as hid:vid_1209&pid_2301.",
                    PlaceholderText = "hid:vid_####&pid_####",
                    Current = { BindTarget = deviceIdentifier },
                });
                yield return CreateSetting(new FormNumberBox
                {
                    Caption = "Button index",
                    HintText = "Zero-based HID button index.",
                    PlaceholderText = "0",
                    Current = { BindTarget = buttonIndex },
                });
            }

            protected override bool TryCreateTrigger(out OmsBindingTrigger trigger, out string? validationError)
            {
                string identifier = deviceIdentifier.Value?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(identifier))
                {
                    trigger = default;
                    validationError = "HID button binding requires a device identifier.";
                    return false;
                }

                if (!int.TryParse(buttonIndex.Value, out int parsedButtonIndex) || parsedButtonIndex < 0)
                {
                    trigger = default;
                    validationError = "HID button binding requires a non-negative button index.";
                    return false;
                }

                trigger = OmsBindingTrigger.HidButton(identifier, parsedButtonIndex);
                validationError = null;
                return true;
            }

            public override bool TryPrepareHidCapture(IReadOnlyList<OmsHidDeviceInfo> connectedDevices, out string resolvedDeviceIdentifier, out string? validationError)
                => tryResolveCaptureDeviceIdentifier(deviceIdentifier, connectedDevices, out resolvedDeviceIdentifier, out validationError);

            public override bool TryApplyCapturedChange(OmsHidDeviceChange change, out string? successMessage)
            {
                if (change.Kind != OmsHidDeviceChangeKind.Button || !change.ButtonChange.Pressed)
                {
                    successMessage = null;
                    return false;
                }

                deviceIdentifier.Value = change.ButtonChange.DeviceIdentifier;
                buttonIndex.Value = change.ButtonChange.ButtonIndex.ToString();
                successMessage = $@"Captured HID button {change.ButtonChange.ButtonIndex} from {change.ButtonChange.DeviceIdentifier}.";
                return true;
            }
        }

        private sealed partial class HidAxisBindingRow : SupplementalBindingRow
        {
            private readonly Bindable<string> deviceIdentifier = new Bindable<string>(string.Empty);
            private readonly Bindable<string> axisIndex = new Bindable<string>("0");
            private readonly Bindable<OmsAxisDirection> axisDirection = new Bindable<OmsAxisDirection>(OmsAxisDirection.Positive);
            private readonly Bindable<bool> axisInverted = new BindableBool(false);

            public override SupplementalBindingCaptureKind CaptureKind => SupplementalBindingCaptureKind.Hid;

            protected override string RowTitle => "HID axis binding";

            public HidAxisBindingRow(IReadOnlyList<BmsAction> availableActions, BmsAction initialAction)
                : base(availableActions, initialAction)
            {
            }

            public HidAxisBindingRow(IReadOnlyList<BmsAction> availableActions, BmsAction initialAction, OmsBindingTrigger trigger)
                : this(availableActions, initialAction)
            {
                deviceIdentifier.Value = trigger.DeviceIdentifier ?? string.Empty;
                axisIndex.Value = trigger.AxisIndex.ToString();
                axisDirection.Value = trigger.AxisDirection;
                axisInverted.Value = trigger.AxisInverted;
            }

            protected override IEnumerable<Drawable> CreateFields()
            {
                yield return CreateActionSetting();
                yield return CreateSetting(new FormTextBox
                {
                    Caption = "Device identifier",
                    HintText = "Use a detected HID identifier such as hid:vid_1209&pid_2301.",
                    PlaceholderText = "hid:vid_####&pid_####",
                    Current = { BindTarget = deviceIdentifier },
                });
                yield return CreateSetting(new FormNumberBox
                {
                    Caption = "Axis index",
                    HintText = "Zero-based axis index as reported by the HID parser.",
                    PlaceholderText = "0",
                    Current = { BindTarget = axisIndex },
                });
                yield return CreateSetting(new AxisDirectionDropdown
                {
                    Caption = "Axis direction",
                    Current = { BindTarget = axisDirection },
                });
                yield return CreateSetting(new FormCheckBox
                {
                    Caption = "Invert axis",
                    HintText = "Invert the reported delta before matching the selected direction.",
                    Current = { BindTarget = axisInverted },
                });
            }

            protected override bool TryCreateTrigger(out OmsBindingTrigger trigger, out string? validationError)
            {
                string identifier = deviceIdentifier.Value?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(identifier))
                {
                    trigger = default;
                    validationError = "HID axis binding requires a device identifier.";
                    return false;
                }

                if (!int.TryParse(axisIndex.Value, out int parsedAxisIndex) || parsedAxisIndex < 0)
                {
                    trigger = default;
                    validationError = "HID axis binding requires a non-negative axis index.";
                    return false;
                }

                trigger = OmsBindingTrigger.HidAxis(identifier, parsedAxisIndex, axisDirection.Value, axisInverted.Value);
                validationError = null;
                return true;
            }

            public override bool TryPrepareHidCapture(IReadOnlyList<OmsHidDeviceInfo> connectedDevices, out string resolvedDeviceIdentifier, out string? validationError)
                => tryResolveCaptureDeviceIdentifier(deviceIdentifier, connectedDevices, out resolvedDeviceIdentifier, out validationError);

            public override bool TryApplyCapturedChange(OmsHidDeviceChange change, out string? successMessage)
            {
                if (change.Kind != OmsHidDeviceChangeKind.Axis)
                {
                    successMessage = null;
                    return false;
                }

                deviceIdentifier.Value = change.AxisChange.DeviceIdentifier;
                axisIndex.Value = change.AxisChange.AxisIndex.ToString();
                axisDirection.Value = change.AxisChange.Delta >= 0 ? OmsAxisDirection.Positive : OmsAxisDirection.Negative;
                axisInverted.Value = false;

                successMessage = $@"Captured HID axis {change.AxisChange.AxisIndex} ({axisDirection.Value}) from {change.AxisChange.DeviceIdentifier}.";
                return true;
            }
        }

        private sealed partial class MouseAxisBindingRow : SupplementalBindingRow
        {
            private readonly Bindable<OmsMouseAxis> mouseAxis = new Bindable<OmsMouseAxis>(OmsMouseAxis.X);
            private readonly Bindable<OmsAxisDirection> axisDirection = new Bindable<OmsAxisDirection>(OmsAxisDirection.Positive);
            private readonly Bindable<bool> axisInverted = new BindableBool(false);

            public override SupplementalBindingCaptureKind CaptureKind => SupplementalBindingCaptureKind.MouseAxis;

            protected override string RowTitle => "Mouse-axis binding";

            public MouseAxisBindingRow(IReadOnlyList<BmsAction> availableActions, BmsAction initialAction)
                : base(availableActions, initialAction)
            {
            }

            public MouseAxisBindingRow(IReadOnlyList<BmsAction> availableActions, BmsAction initialAction, OmsBindingTrigger trigger)
                : this(availableActions, initialAction)
            {
                mouseAxis.Value = trigger.MouseAxisKind;
                axisDirection.Value = trigger.AxisDirection;
                axisInverted.Value = trigger.AxisInverted;
            }

            protected override IEnumerable<Drawable> CreateFields()
            {
                yield return CreateActionSetting();
                yield return CreateSetting(new MouseAxisDropdown
                {
                    Caption = "Mouse axis",
                    Current = { BindTarget = mouseAxis },
                });
                yield return CreateSetting(new AxisDirectionDropdown
                {
                    Caption = "Axis direction",
                    Current = { BindTarget = axisDirection },
                });
                yield return CreateSetting(new FormCheckBox
                {
                    Caption = "Invert axis",
                    HintText = "Invert the reported mouse delta before matching the selected direction.",
                    Current = { BindTarget = axisInverted },
                });
            }

            protected override bool TryCreateTrigger(out OmsBindingTrigger trigger, out string? validationError)
            {
                trigger = OmsBindingTrigger.MouseAxis(mouseAxis.Value, axisDirection.Value, axisInverted.Value);
                validationError = null;
                return true;
            }

            public override bool TryApplyCapturedMouseAxis(OmsMouseAxis capturedAxis, OmsAxisDirection capturedDirection, out string? successMessage)
            {
                mouseAxis.Value = capturedAxis;
                axisDirection.Value = capturedDirection;
                axisInverted.Value = false;
                successMessage = $@"Captured mouse axis {capturedAxis} ({capturedDirection}).";
                return true;
            }
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
                => item == OmsAxisDirection.Positive ? "Positive" : "Negative";
        }

        private sealed partial class MouseAxisDropdown : FormDropdown<OmsMouseAxis>
        {
            public MouseAxisDropdown()
            {
                Items = new[] { OmsMouseAxis.X, OmsMouseAxis.Y };
            }

            protected override LocalisableString GenerateItemText(OmsMouseAxis item)
                => item == OmsMouseAxis.X ? "X" : "Y";
        }
    }
}
