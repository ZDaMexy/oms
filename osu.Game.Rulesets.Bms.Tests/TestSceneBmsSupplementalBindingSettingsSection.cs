// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using oms.Input;
using oms.Input.Devices;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Handlers.Mouse;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class TestSceneBmsSupplementalBindingSettingsSection
    {
        [Test]
        public async Task TestDeviceDiscoveryRunsOffCallingThread()
        {
            int callingThreadId = Environment.CurrentManagedThreadId;
            int providerThreadId = 0;

            var section = new BmsSupplementalBindingSettingsSection(new BmsRuleset(), () =>
            {
                providerThreadId = Environment.CurrentManagedThreadId;
                return Array.Empty<OmsHidDeviceInfo>();
            });

            var devices = await section.EnumerateDetectedDevicesAsync().ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(devices, Is.Empty);
                Assert.That(providerThreadId, Is.Not.EqualTo(0));
                Assert.That(providerThreadId, Is.Not.EqualTo(callingThreadId));
            });
        }

        [Test]
        public void TestSavedOfflineDeviceIsRetainedAndLateInventoryResolvesTheSameEditedRow()
        {
            var row = new BmsSupplementalBindingSettingsSection.HidButtonBindingRow(new[] { BmsAction.Key1 }, BmsAction.Key1,
                OmsBindingTrigger.HidButton("hid:model", 7));
            row.UpdateDevices(Array.Empty<OmsHidDeviceInfo>());
            Assert.That(row.TryCreateBinding(8, out _, out OmsBindingTrigger offline, out _), Is.True);
            Assert.That(offline.DeviceIdentifier, Is.EqualTo("hid:model"));
            row.ButtonIndex.Value = "12";
            row.UpdateDevices(new[] { new OmsHidDeviceInfo("dinput:instance_one", "IIDX Controller", "hid:model") });
            Assert.That(row.TryCreateBinding(8, out _, out OmsBindingTrigger online, out _), Is.True);
            Assert.That(online.DeviceIdentifier, Is.EqualTo("dinput:instance_one"));
            Assert.That(online.ButtonIndex, Is.EqualTo(12));
        }

        [Test]
        public void TestAmbiguousLegacyDeviceBlocksBothCaptureAndApplyUntilOneUnitIsSelected()
        {
            var inventory = new[]
            {
                new OmsHidDeviceInfo("dinput:instance_one", "IIDX Controller", "hid:model"),
                new OmsHidDeviceInfo("dinput:instance_two", "IIDX Controller", "hid:model"),
            };
            var row = new BmsSupplementalBindingSettingsSection.HidAxisBindingRow(new[] { BmsAction.Scratch1 }, BmsAction.Scratch1,
                OmsBindingTrigger.HidAxis("hid:model", 1, OmsAxisDirection.Positive));
            row.UpdateDevices(inventory);
            Assert.That(row.TryCreateBinding(8, out _, out _, out string? applyError), Is.False);
            Assert.That(applyError, Does.Contain("多个同型号"));
            Assert.That(row.TryPrepareHidCapture(inventory, out _, out _), Is.False);
            row.DeviceIdentifier.Value = inventory[1].Identifier;
            Assert.That(row.TryCreateBinding(8, out _, out OmsBindingTrigger selected, out _), Is.True);
            Assert.That(selected.DeviceIdentifier, Is.EqualTo(inventory[1].Identifier));
            Assert.That(row.TryPrepareHidCapture(inventory, out string capturedDevice, out _), Is.True);
            Assert.That(capturedDevice, Is.EqualTo(inventory[1].Identifier));
        }

        [Test]
        public void TestInitialAbsoluteAxisSnapshotCannotCaptureAStationaryAxis()
        {
            var row = new BmsSupplementalBindingSettingsSection.HidAxisBindingRow(new[] { BmsAction.Scratch1 }, BmsAction.Scratch1);
            var initial = new OmsHidAxisChange("device", 0, 0, 32768, 0, 65535, false);
            Assert.That(row.TryApplyCapturedChange(OmsHidDeviceChange.Axis(initial), out _), Is.False);
            Assert.That(row.DeviceIdentifier.Value, Is.Empty);
            var movement = new OmsHidAxisChange("device", 3, -20, 32748, 0, 65535, false);
            Assert.That(row.TryApplyCapturedChange(OmsHidDeviceChange.Axis(movement), out _), Is.True);
            Assert.That(row.AxisIndex.Value, Is.EqualTo("3"));
            Assert.That(row.AxisDirection.Value, Is.EqualTo(OmsAxisDirection.Negative));
        }

        [TestCase(1, -98, OmsAxisDirection.Positive, false)]
        [TestCase(99, 98, OmsAxisDirection.Negative, false)]
        [TestCase(1, -98, OmsAxisDirection.Positive, true)]
        public void TestCircularAxisCapturePreservesTheMovementDirectionAcrossTheSeam(int value, int rawDelta, OmsAxisDirection direction, bool calibrated)
        {
            var row = new BmsSupplementalBindingSettingsSection.HidAxisBindingRow(new[] { BmsAction.Scratch1 }, BmsAction.Scratch1);
            row.AxisSettings.Mode.Value = OmsAxisMode.CircularPosition;
            if (calibrated)
            {
                row.AxisSettings.Advanced.Value = true;
                row.AxisSettings.Minimum.Value = "0";
                row.AxisSettings.Maximum.Value = "99";
            }
            Assert.That(row.TryApplyCapturedChange(OmsHidDeviceChange.Axis(new OmsHidAxisChange("device", 2, rawDelta, value, 0, calibrated ? 65535 : 99, false)), out _), Is.True);
            Assert.That(row.AxisIndex.Value, Is.EqualTo("2"));
            Assert.That(row.AxisDirection.Value, Is.EqualTo(direction));
            Assert.That(row.TryCreateBinding(8, out _, out OmsBindingTrigger trigger, out _), Is.True);
            using var preview = new BmsInputDiagnostics(OmsAction.Key1P_Scratch, trigger);
            preview.BeginPoll(0);
            preview.Apply(OmsHidDeviceChange.Axis(new OmsHidAxisChange("device", 2, 0, value - rawDelta, 0, calibrated ? 65535 : 99, false)));
            preview.EndPoll();
            preview.BeginPoll(10);
            preview.Apply(OmsHidDeviceChange.Axis(new OmsHidAxisChange("device", 2, rawDelta, value, 0, calibrated ? 65535 : 99, false)));
            preview.EndPoll();
            Assert.That(preview.TriggerCount, Is.EqualTo(1), "The captured direction must actually accept the same movement in the gameplay interpreter.");
        }

        [Test]
        public void TestPositionThresholdsUsePercentagesAndRejectInvertedHysteresis()
        {
            var editor = new BmsSupplementalBindingSettingsSection.AxisSettingsEditor(OmsAxisSettings.Default, false);
            editor.Mode.Value = OmsAxisMode.CentredPosition;
            editor.Activation.Value = "65";
            editor.Release.Value = "35";
            Assert.That(editor.TryBuild(out OmsAxisSettings settings, out _), Is.True);
            Assert.That(settings.ActivationThreshold, Is.EqualTo(0.65));
            Assert.That(settings.ReleaseThreshold, Is.EqualTo(0.35));
            Assert.That(settings.Minimum, Is.Null);
            Assert.That(settings.Maximum, Is.Null);
            editor.Release.Value = "70";
            Assert.That(editor.TryBuild(out _, out string? error), Is.False);
            Assert.That(error, Does.Contain("释放阈值"));
        }

        [Test]
        public void TestCalibrationRemainsOptionalAndIsValidatedOnlyForItsMode()
        {
            var editor = new BmsSupplementalBindingSettingsSection.AxisSettingsEditor(OmsAxisSettings.Default, false);
            editor.Mode.Value = OmsAxisMode.StickToMotion;
            editor.Deadzone.Value = "20";
            editor.RevolutionsPerSecond.Value = "2.5";
            editor.Sensitivity.Value = "1.5";
            editor.StopTimeout.Value = "75";
            Assert.That(editor.TryBuild(out OmsAxisSettings settings, out _), Is.True);
            Assert.That(settings.Deadzone, Is.EqualTo(0.2));
            Assert.That(settings.RevolutionsPerSecond, Is.EqualTo(2.5));
            Assert.That(settings.StopTimeoutMilliseconds, Is.EqualTo(75));
            editor.Advanced.Value = true;
            editor.Minimum.Value = "-100";
            Assert.That(editor.TryBuild(out _, out _), Is.False);
            editor.Maximum.Value = "100";
            editor.Centre.Value = "0";
            Assert.That(editor.TryBuild(out settings, out _), Is.True);
            Assert.That(settings.Minimum, Is.EqualTo(-100));
            editor.Mode.Value = OmsAxisMode.RelativeMotion;
            editor.Minimum.Value = "invalid hidden position calibration";
            Assert.That(editor.TryBuild(out settings, out _), Is.True);
            Assert.That(settings.Minimum, Is.Null);
        }

        [TestCase(0)]
        [TestCase(251)]
        public void TestMotionStopTimeoutRejectsInvalidBoundaries(int timeout)
        {
            var editor = new BmsSupplementalBindingSettingsSection.AxisSettingsEditor(OmsAxisSettings.Default, false);
            editor.StopTimeout.Value = timeout.ToString();
            Assert.That(editor.TryBuild(out _, out string? error), Is.False);
            Assert.That(error, Does.Contain("停止等待"));
        }

        [Test]
        public void TestDiagnosticsUsesRealCircularInterpretationAndDisplaysRawSignals()
        {
            using var preview = new BmsInputDiagnostics(OmsAction.Key1P_Scratch,
                OmsBindingTrigger.HidAxis("device", 0, OmsAxisDirection.Positive, false,
                    new OmsAxisSettings(OmsAxisMode.CircularPosition, movementThreshold: 0.01)));
            preview.BeginPoll(0);
            preview.Apply(OmsHidDeviceChange.Axis(new OmsHidAxisChange("device", 0, 0, 99, 0, 99, false)));
            preview.EndPoll();
            Assert.That(preview.TriggerCount, Is.Zero);
            preview.BeginPoll(10);
            preview.Apply(OmsHidDeviceChange.Axis(new OmsHidAxisChange("device", 0, -98, 1, 0, 99, false)));
            preview.EndPoll();
            Assert.That(preview.TriggerCount, Is.EqualTo(1));
            Assert.That(preview.TotalMovement, Is.EqualTo(0.02).Within(0.00001));
            Assert.That(preview.Describe(true), Does.Contain("变化 -98"));
            Assert.That(preview.Describe(true), Does.Contain("运动中"));
            preview.BeginPoll(100);
            preview.EndPoll();
            Assert.That(preview.IsMoving, Is.False);
        }

        [Test]
        public void TestDiagnosticsPreservesPositionButtonHysteresisAndDisconnectReleasesIt()
        {
            using var preview = new BmsInputDiagnostics(OmsAction.Key1P_1,
                OmsBindingTrigger.HidAxis("device", 0, OmsAxisDirection.Positive, false,
                    new OmsAxisSettings(OmsAxisMode.CentredPosition, activationThreshold: 0.6, releaseThreshold: 0.3)));
            void sample(int value)
            {
                preview.BeginPoll(value + 200);
                preview.Apply(OmsHidDeviceChange.Axis(new OmsHidAxisChange("device", 0, 0, value, -100, 100, false)));
                preview.EndPoll();
            }
            sample(70);
            Assert.That(preview.IsPressed, Is.True);
            sample(40);
            Assert.That(preview.IsPressed, Is.True);
            sample(20);
            Assert.That(preview.IsPressed, Is.False);
            sample(70);
            preview.Disconnect();
            Assert.That(preview.IsPressed, Is.False);
            Assert.That(preview.Describe(false), Does.Contain("设备未连接"));
        }

        [Test]
        public void TestDiagnosticsExplainsWhyPositionModesCannotInterpretTheReportedAxis()
        {
            using var noPosition = new BmsInputDiagnostics(OmsAction.Key1P_Scratch,
                OmsBindingTrigger.HidAxis("device", 0, OmsAxisDirection.Positive, axisSettings: new OmsAxisSettings(OmsAxisMode.CircularPosition)));
            noPosition.BeginPoll(0);
            noPosition.Apply(OmsHidDeviceChange.Axis(new OmsHidAxisChange("device", 0, 20)));
            noPosition.EndPoll();
            Assert.That(noPosition.Describe(true), Does.Contain("需要绝对位置"));

            using var noRange = new BmsInputDiagnostics(OmsAction.Key1P_1,
                OmsBindingTrigger.HidAxis("device", 0, OmsAxisDirection.Positive, axisSettings: new OmsAxisSettings(OmsAxisMode.CentredPosition)));
            noRange.BeginPoll(0);
            noRange.Apply(OmsHidDeviceChange.Axis(new OmsHidAxisChange("device", 0, 0, 20, null, null, false)));
            noRange.EndPoll();
            Assert.That(noRange.Describe(true), Does.Contain("没有提供有效范围"));

            using var badCentre = new BmsInputDiagnostics(OmsAction.Key1P_1,
                OmsBindingTrigger.HidAxis("device", 0, OmsAxisDirection.Positive, axisSettings: new OmsAxisSettings(OmsAxisMode.CentredPosition, centre: 200)));
            badCentre.BeginPoll(0);
            badCentre.Apply(OmsHidDeviceChange.Axis(new OmsHidAxisChange("device", 0, 0, 70, -100, 100, false)));
            badCentre.EndPoll();
            Assert.That(badCentre.IsPressed, Is.False);
            Assert.That(badCentre.Describe(true), Does.Contain("中心值必须位于当前设备范围内部"));
        }

        [Test]
        public void TestMouseDiagnosticsAccumulatesMovementAndUsesTheEditedThresholdAndInversion()
        {
            using var preview = new BmsInputDiagnostics(OmsAction.Key1P_Scratch,
                OmsBindingTrigger.MouseAxis(OmsMouseAxis.X, OmsAxisDirection.Negative, true,
                    new OmsAxisSettings(sensitivity: 2, movementThreshold: 5)));
            preview.BeginPoll(0);
            preview.ApplyMouse(1, 0);
            preview.EndPoll();
            Assert.That(preview.TriggerCount, Is.Zero);
            preview.BeginPoll(10);
            preview.ApplyMouse(2, 0);
            preview.EndPoll();
            Assert.That(preview.TriggerCount, Is.EqualTo(1));
            Assert.That(preview.TotalMovement, Is.EqualTo(-6));
            Assert.That(preview.Describe(true), Does.Contain("移动累计 X +3"));
        }
    }

    [HeadlessTest]
    public partial class TestSceneBmsInputConfiguration : OsuManualInputManagerTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        protected override bool UseFreshStoragePerRun => true;

        private TaskCompletionSource<bool>? pendingDriverRelease;
        private MouseHandler? testMouseHandler;

        [TearDownSteps]
        public void QueueDiagnosticResourceCleanup()
        {
            // NUnit TearDown runs before TestScene's AfterTest executes the queued UI steps.
            // Release fixture gates after those steps, with final cleanup if a step fails.
            AddStep("release diagnostic fixture resources", ReleaseDiagnosticResources);
        }

        [OneTimeTearDown]
        public void ReleaseDiagnosticResources()
        {
            pendingDriverRelease?.TrySetResult(true);
            pendingDriverRelease = null;
            testMouseHandler?.Dispose();
            testMouseHandler = null;
        }

        [TestCase(OmsAxisMode.RelativeMotion)]
        [TestCase(OmsAxisMode.CircularPosition)]
        [TestCase(OmsAxisMode.CentredPosition)]
        [TestCase(OmsAxisMode.StickToMotion)]
        public void TestOnlyTheSelectedAxisModeShowsItsControls(OmsAxisMode mode)
        {
            BmsSupplementalBindingSettingsSection.AxisSettingsEditor editor = null!;
            AddStep("open the real axis settings controls", () => Child = new Container
            {
                Width = 480,
                AutoSizeAxes = Axes.Y,
                Child = editor = new BmsSupplementalBindingSettingsSection.AxisSettingsEditor(OmsAxisSettings.Default, false),
            });
            AddUntilStep("axis controls are loaded", () => editor.IsLoaded);
            AddStep("select the intended axis purpose", () => editor.Mode.Value = mode);
            AddWaitStep("load mode-specific fields", 2);
            AddStep("only relevant adjustment controls are visible", () =>
            {
                string[] captions = editor.ChildrenOfType<FormTextBox>().Select(box => box.Caption.ToString()).ToArray();
                bool position = mode == OmsAxisMode.CentredPosition;
                Assert.That(captions.Contains("触发阈值（%）"), Is.EqualTo(position));
                Assert.That(captions.Contains("释放阈值（%）"), Is.EqualTo(position));
                Assert.That(captions.Contains("灵敏度（倍）"), Is.EqualTo(!position));
                Assert.That(captions.Contains("停止等待（ms）"), Is.EqualTo(!position));
                Assert.That(captions.Contains("满偏转速度（转/秒）"), Is.EqualTo(mode == OmsAxisMode.StickToMotion));
                Assert.That(captions.Any(caption => caption.StartsWith("最小值", StringComparison.Ordinal)), Is.False);
            });
            AddStep("expand advanced calibration", () => editor.Advanced.Value = true);
            AddWaitStep("load advanced fields", 2);
            AddStep("relative motion has no position calibration", () =>
            {
                string[] captions = editor.ChildrenOfType<FormTextBox>().Select(box => box.Caption.ToString()).ToArray();
                Assert.That(captions.Contains("最小值（留空自动）"), Is.EqualTo(mode != OmsAxisMode.RelativeMotion));
                Assert.That(captions.Contains("中心值（留空自动）"), Is.EqualTo(mode == OmsAxisMode.CentredPosition || mode == OmsAxisMode.StickToMotion));
            });
        }

        [Test]
        public void TestBackgroundDiscoveryUpdatesExistingDeviceChoicesWithoutLosingEdits()
        {
            var scan = new TaskCompletionSource<IReadOnlyList<OmsHidDeviceInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var devices = new[]
            {
                new OmsHidDeviceInfo("dinput:instance_one", "Controller one", "hid:model"),
                new OmsHidDeviceInfo("dinput:instance_two", "Controller two", "hid:model"),
            };
            BmsSupplementalBindingSettingsSection section = null!;
            BmsSupplementalBindingSettingsSection.HidButtonBindingRow row = null!;
            AddStep("open the device editor with discovery still waiting", () => Child = host(section = new BmsSupplementalBindingSettingsSection(
                new BmsRuleset(), () => scan.Task.GetAwaiter().GetResult())));
            AddUntilStep("the editor is interactive during background discovery", () => section.IsLoaded && buttons(section, "添加 HID 按钮").Any());
            AddStep("add and edit a binding before discovery returns", () => buttons(section, "添加 HID 按钮").Single().Action?.Invoke());
            AddUntilStep("the new binding row is loaded", () => section.ChildrenOfType<BmsSupplementalBindingSettingsSection.HidButtonBindingRow>().Any(row => row.IsLoaded));
            AddStep("keep the current edit while finishing the scan", () =>
            {
                row = section.ChildrenOfType<BmsSupplementalBindingSettingsSection.HidButtonBindingRow>().Single();
                row.ButtonIndex.Value = "7";
                scan.SetResult(devices);
            });
            AddUntilStep("the same live dropdown receives both detected units", () =>
                row.ChildrenOfType<FormDropdown<string>>().Single().Items.Contains(devices[1].Identifier));
            AddStep("select an exact detected unit and retain the unsaved button edit", () =>
            {
                Assert.That(section.ChildrenOfType<BmsSupplementalBindingSettingsSection.HidButtonBindingRow>().Single(), Is.SameAs(row));
                row.DeviceIdentifier.Value = devices[1].Identifier;
                Assert.That(row.TryCreateBinding(6, out _, out OmsBindingTrigger trigger, out _), Is.True);
                Assert.That(trigger.DeviceIdentifier, Is.EqualTo(devices[1].Identifier));
                Assert.That(trigger.ButtonIndex, Is.EqualTo(7));
            });
        }

        [Test]
        public void TestContinuousDeviceTestingCapturesMultipleSignalsAndReleasesEveryReplacedSession()
        {
            const string device_id = "dinput:instance_test";
            const string other_device_id = "dinput:instance_other";
            var inventory = new[] { new OmsHidDeviceInfo(device_id, "Test Controller"), new OmsHidDeviceInfo(other_device_id, "Other Controller") };
            var providers = new List<DiagnosticDeviceProvider>();
            BmsSupplementalBindingSettingsSection section = null!;
            BmsSupplementalBindingSettingsSection.HidButtonBindingRow row = null!;
            AddStep("open the real editor with an isolated device provider", () => Child = host(section = new BmsSupplementalBindingSettingsSection(
                new BmsRuleset(), () => inventory, id =>
                {
                    var provider = new DiagnosticDeviceProvider(id);
                    providers.Add(provider);
                    return new OmsHidDeviceCaptureSession(id, provider);
                })));
            AddUntilStep("device discovery has completed", () => section.IsLoaded && section.DetectedDevicesSummary.Contains("Test Controller"));
            AddStep("add a button binding", () => buttons(section, "添加 HID 按钮").Single().Action?.Invoke());
            AddUntilStep("the real binding controls are loaded", () => section.ChildrenOfType<BmsSupplementalBindingSettingsSection.HidButtonBindingRow>().Any(row => row.IsLoaded));
            AddStep("select its device and start continuous testing", () =>
            {
                row = section.ChildrenOfType<BmsSupplementalBindingSettingsSection.HidButtonBindingRow>().Single();
                row.DeviceIdentifier.Value = device_id;
                row.ButtonIndex.Value = "3";
                buttons(row, "测试设备信号").Single().Action?.Invoke();
            });
            AddUntilStep("the diagnostics session has connected", () => providers.Count == 1 && providers[0].DiscoveryCount > 0);
            AddStep("press the isolated physical button", () => providers[0].Device.Changes.Enqueue(new[]
            {
                OmsHidDeviceChange.Button(new OmsHidButtonChange(device_id, 3, true)),
            }));
            AddUntilStep("the real test panel shows the press", () => string.Concat(row.ChildrenOfType<OsuSpriteText>().Select(text => text.Text.ToString())).Contains("绑定输出：按下"));
            AddStep("release the physical button", () => providers[0].Device.Changes.Enqueue(new[]
            {
                OmsHidDeviceChange.Button(new OmsHidButtonChange(device_id, 3, false)),
            }));
            AddUntilStep("the same continuous panel shows the release", () => string.Concat(row.ChildrenOfType<OsuSpriteText>().Select(text => text.Text.ToString())).Contains("绑定输出：松开"));
            AddAssert("the test did not stop after the first signal", () => buttons(row, "停止设备测试").Any());
            AddStep("select a different controller while testing", () => row.DeviceIdentifier.Value = other_device_id);
            AddUntilStep("the test has moved to the selected controller", () => providers.Count == 2 && providers[0].Disposed && providers[1].DiscoveryCount > 0);
            AddStep("press a button on the newly selected controller", () => providers[1].Device.Changes.Enqueue(new[]
            {
                OmsHidDeviceChange.Button(new OmsHidButtonChange(other_device_id, 3, true)),
            }));
            AddUntilStep("the panel now interprets the new controller", () => rowText(row).Contains("绑定输出：按下"));
            AddStep("switch from testing to one-shot capture", () => buttons(row, "开始捕获").Single().Action?.Invoke());
            AddUntilStep("the previous test provider is released", () => providers.Count == 3 && providers[1].Disposed);
            AddStep("switch the actual variant selector", () => section.ChildrenOfType<FormDropdown<int>>().Single().Current.Value = new BmsRuleset().AvailableVariants.Last());
            AddUntilStep("variant switching releases the capture provider too", () => providers[2].Disposed);
            AddAssert("the previous rows are removed", () => !section.ChildrenOfType<BmsSupplementalBindingSettingsSection.HidButtonBindingRow>().Any());
        }

        [Test]
        public void TestDisposingDeviceSettingsDoesNotWaitForTheRetiringDriver()
        {
            const string device_id = "dinput:instance_retiring";
            var releaseDriver = pendingDriverRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            DiagnosticDeviceProvider provider = null!;
            BmsSupplementalBindingSettingsSection section = null!;
            BmsSupplementalBindingSettingsSection.HidButtonBindingRow row = null!;
            Container sectionHost = null!;
            Task? panelDisposal = null;
            AddStep("open a panel whose driver takes time to release", () => Child = sectionHost = (Container)host(section = new BmsSupplementalBindingSettingsSection(
                new BmsRuleset(), () => new[] { new OmsHidDeviceInfo(device_id, "Retiring Controller") }, id =>
                {
                    provider = new DiagnosticDeviceProvider(id, () => releaseDriver.Task.GetAwaiter().GetResult());
                    return new OmsHidDeviceCaptureSession(id, provider);
                })));
            AddUntilStep("device discovery has completed", () => section.IsLoaded && section.DetectedDevicesSummary.Contains("Retiring Controller"));
            AddStep("add a test binding", () => buttons(section, "添加 HID 按钮").Single().Action?.Invoke());
            AddUntilStep("the row is loaded", () => section.ChildrenOfType<BmsSupplementalBindingSettingsSection.HidButtonBindingRow>().Any(row => row.IsLoaded));
            AddStep("start a real diagnostics session", () =>
            {
                row = section.ChildrenOfType<BmsSupplementalBindingSettingsSection.HidButtonBindingRow>().Single();
                row.DeviceIdentifier.Value = device_id;
                buttons(row, "测试设备信号").Single().Action?.Invoke();
            });
            AddUntilStep("the diagnostics driver is connected", () => provider != null && provider.DiscoveryCount > 0);
            AddStep("detach and dispose the panel independently of the update thread", () =>
            {
                sectionHost.Remove(section, false);
                panelDisposal = Task.Run(section.Dispose);
            });
            AddUntilStep("the panel disposal returns while the driver is still blocked", () => panelDisposal?.IsCompleted == true);
            AddStep("panel cleanup itself succeeded", () => panelDisposal!.GetAwaiter().GetResult());
            AddUntilStep("driver cleanup has started in the background", () => provider.DisposalStarted);
            AddStep("the driver remains owned until it can finish", () => Assert.Multiple(() =>
            {
                Assert.That(releaseDriver.Task.IsCompleted, Is.False, "The fixture must not open its driver gate before the assertion.");
                Assert.That(provider.DisposalStarted, Is.True, "The background task must have reached actual driver cleanup.");
                Assert.That(provider.Disposed, Is.False, "Driver cleanup must remain owned and pending until the blocked driver returns.");
            }));
            AddStep("allow the driver to finish releasing", () => releaseDriver.TrySetResult(true));
            AddUntilStep("the retired driver is finally released", () => provider.Disposed);
        }

        [Test]
        public void TestContinuousMouseTestingUsesRelativeSignalsAtTheCursorBoundaryAndReleasesItsSubscription()
        {
            var source = new DiagnosticMouseSource();
            BmsSupplementalBindingSettingsSection section = null!;
            BmsSupplementalBindingSettingsSection.MouseAxisBindingRow row = null!;
            Vector2 cursor = default;
            AddStep("open the real mouse editor with the shared relative source", () => Child = host(section = new BmsSupplementalBindingSettingsSection(
                new BmsRuleset(), () => Array.Empty<OmsHidDeviceInfo>()), source));
            AddUntilStep("the mouse editor has loaded", () => section.IsLoaded && buttons(section, "添加鼠标轴").Any());
            AddStep("add a mouse binding", () => buttons(section, "添加鼠标轴").Single().Action?.Invoke());
            AddUntilStep("the mouse binding controls are loaded", () => section.ChildrenOfType<BmsSupplementalBindingSettingsSection.MouseAxisBindingRow>().Any(row => row.IsLoaded));
            AddStep("test an inverted axis with a cumulative threshold", () =>
            {
                row = section.ChildrenOfType<BmsSupplementalBindingSettingsSection.MouseAxisBindingRow>().Single();
                row.AxisDirection.Value = OmsAxisDirection.Negative;
                row.AxisInverted.Value = true;
                row.AxisSettings.Sensitivity.Value = "2";
                row.AxisSettings.MovementThreshold.Value = "5";
                InputManager.MoveMouseTo(Vector2.Zero);
                buttons(row, "测试设备信号").Single().Action?.Invoke();
            });
            AddWaitStep("apply the cursor boundary position", 1);
            AddStep("remember the unchanged cursor and send the first physical movement", () =>
            {
                cursor = InputManager.CurrentState.Mouse.Position;
                _ = Task.Run(() => source.Emit(1, 0));
            });
            AddUntilStep("the continuous panel receives a raw movement below the threshold", () => rowText(row).Contains("移动累计 X +1") && rowText(row).Contains("累计触发 0 次"));
            AddStep("send further relative movement without moving the cursor", () => source.Emit(2, 0));
            AddUntilStep("the edited settings produce the expected accepted motion", () => rowText(row).Contains("累计 -6 counts") && rowText(row).Contains("累计触发 1 次"));
            AddAssert("cursor clamping did not participate in signal testing", () => InputManager.CurrentState.Mouse.Position == cursor);
            AddStep("reset discards pending pre-focus-change movement", () =>
            {
                source.Emit(100, 0);
                source.Reset();
                source.Emit(2, 0);
            });
            AddUntilStep("only the post-reset movement is counted", () => rowText(row).Contains("移动累计 X +5") && rowText(row).Contains("累计触发 1 次"));
            AddStep("switch from continuous testing to one-shot capture", () => buttons(row, "开始捕获").Single().Action?.Invoke());
            AddAssert("only the new capture owns the source", () => source.MovementSubscribers == 1 && source.ResetSubscribers == 1 && source.CaptureLeases == 1);
            AddStep("capture a relative Y movement", () => source.Emit(0, -9));
            AddUntilStep("the capture is complete and has unsubscribed", () => source.MovementSubscribers == 0 && source.ResetSubscribers == 0 && source.CaptureLeases == 0);
            AddAssert("the captured axis and direction match the raw signal", () => row.MouseAxis.Value == OmsMouseAxis.Y && row.AxisDirection.Value == OmsAxisDirection.Negative);
            AddStep("start another continuous test", () => buttons(row, "测试设备信号").Single().Action?.Invoke());
            AddAssert("the new test owns exactly one subscription", () => source.MovementSubscribers == 1);
            AddStep("changing variant closes the test", () => section.ChildrenOfType<FormDropdown<int>>().Single().Current.Value = new BmsRuleset().AvailableVariants.Last());
            AddAssert("no raw movement observer or capture remains", () => source.MovementSubscribers == 0 && source.ResetSubscribers == 0 && source.CaptureLeases == 0);
        }

        [Test]
        public void TestUnavailableRelativeMouseSourceDoesNotSubstituteCursorMovement()
        {
            var source = new DiagnosticMouseSource { IsActive = false };
            BmsSupplementalBindingSettingsSection section = null!;
            BmsSupplementalBindingSettingsSection.MouseAxisBindingRow row = null!;
            AddStep("open mouse settings while relative input is unavailable", () => Child = host(section = new BmsSupplementalBindingSettingsSection(
                new BmsRuleset(), () => Array.Empty<OmsHidDeviceInfo>()), source));
            AddUntilStep("the editor is loaded", () => section.IsLoaded && buttons(section, "添加鼠标轴").Any());
            AddStep("add a mouse binding", () => buttons(section, "添加鼠标轴").Single().Action?.Invoke());
            AddUntilStep("the row is loaded", () => section.ChildrenOfType<BmsSupplementalBindingSettingsSection.MouseAxisBindingRow>().Any(row => row.IsLoaded));
            AddStep("start testing while only cursor movement is available", () =>
            {
                row = section.ChildrenOfType<BmsSupplementalBindingSettingsSection.MouseAxisBindingRow>().Single();
                buttons(row, "测试设备信号").Single().Action?.Invoke();
                InputManager.MoveMouseTo(new Vector2(100, 100));
                source.Emit(20, 0);
            });
            AddUntilStep("the unavailable source is explained while waiting for capture to activate", () => rowText(row).Contains("相对鼠标输入当前不可用"));
            AddAssert("the waiting test retains one capture lease", () => source.MovementSubscribers == 1 && source.CaptureLeases == 1);
            AddStep("activate the source and send a new raw movement", () =>
            {
                source.IsActive = true;
                source.Emit(2, 0);
            });
            AddUntilStep("only active relative input is measured", () => rowText(row).Contains("移动累计 X +2"));
            AddStep("stop the test", () => buttons(row, "停止设备测试").Single().Action?.Invoke());
            AddAssert("the waiting capture also releases correctly", () => source.MovementSubscribers == 0 && source.ResetSubscribers == 0 && source.CaptureLeases == 0);
        }

        [Test]
        public void TestMousePrecisionToggleUsesTheExistingHandlerSettingAndActivatesTheWaitingTest()
        {
            BmsSupplementalBindingSettingsSection section = null!;
            BmsSupplementalBindingSettingsSection.MouseAxisBindingRow row = null!;
            DiagnosticMouseSource source = null!;
            FormCheckBox precision = null!;
            AddStep("open the editor with high precision previously disabled", () =>
            {
                testMouseHandler = new MouseHandler();
                testMouseHandler.UseRelativeMode.Value = false;
                source = new DiagnosticMouseSource(testMouseHandler);
                Child = host(section = new BmsSupplementalBindingSettingsSection(new BmsRuleset(), () => Array.Empty<OmsHidDeviceInfo>(),
                    configuredMouseHandler: testMouseHandler), source);
            });
            AddUntilStep("the narrow precision setting is loaded", () => section.IsLoaded && section.ChildrenOfType<FormCheckBox>().Any(check => check.IsLoaded && check.Caption.ToString() == "高精度鼠标输入"));
            AddStep("read the actual checkbox", () => precision = section.ChildrenOfType<FormCheckBox>().Single(check => check.Caption.ToString() == "高精度鼠标输入"));
            AddAssert("opening settings preserves the old disabled value", () => !precision.Current.Value && !testMouseHandler!.UseRelativeMode.Value);
            AddStep("add a mouse binding", () => buttons(section, "添加鼠标轴").Single().Action?.Invoke());
            AddUntilStep("the mouse row is loaded", () => section.ChildrenOfType<BmsSupplementalBindingSettingsSection.MouseAxisBindingRow>().Any(row => row.IsLoaded));
            AddStep("start testing without automatically enabling high precision", () =>
            {
                row = section.ChildrenOfType<BmsSupplementalBindingSettingsSection.MouseAxisBindingRow>().Single();
                buttons(row, "测试设备信号").Single().Action?.Invoke();
            });
            AddUntilStep("the explicit test is waiting for relative input", () => rowText(row).Contains("相对鼠标输入当前不可用"));
            AddAssert("a waiting test owns capture without changing the saved preference", () => source.CaptureLeases == 1 && !testMouseHandler!.UseRelativeMode.Value && !source.IsActive);
            AddStep("enable the visible precision checkbox", () => precision.Current.Value = true);
            AddAssert("the same handler setting activates the relative source", () => testMouseHandler!.UseRelativeMode.Value && source.IsActive);
            AddStep("move the physical mouse through the active source", () => source.Emit(4, 0));
            AddUntilStep("the existing waiting test now measures input", () => rowText(row).Contains("移动累计 X +4"));
            AddStep("stop the device test", () => buttons(row, "停止设备测试").Single().Action?.Invoke());
            AddAssert("temporary capture ends and the chosen preference remains enabled", () => source.CaptureLeases == 0 && !source.IsActive && testMouseHandler!.UseRelativeMode.Value);
            AddStep("an external settings change updates the same checkbox", () => testMouseHandler!.UseRelativeMode.Value = false);
            AddAssert("the narrow control has no independent configuration value", () => !precision.Current.Value);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestMousePrecisionControlExplainsAnUnavailableHost(bool hasRelativeSource)
        {
            BmsSupplementalBindingSettingsSection section = null!;
            AddStep("open a host missing a required mouse capability", () =>
            {
                testMouseHandler = hasRelativeSource ? null : new MouseHandler();
                Child = host(section = new BmsSupplementalBindingSettingsSection(new BmsRuleset(), () => Array.Empty<OmsHidDeviceInfo>(),
                    configuredMouseHandler: testMouseHandler), hasRelativeSource ? new DiagnosticMouseSource() : null);
            });
            AddUntilStep("the host limitation is explained", () => section.IsLoaded && rowText(section).Contains("当前宿主未提供可配置的相对鼠标输入"));
            AddAssert("no disconnected precision setting is presented", () => !section.ChildrenOfType<FormCheckBox>().Any(check => check.Caption.ToString() == "高精度鼠标输入"));
        }

        private Drawable host(Drawable section, IOmsMouseAxisSource? mouseSource = null) => new DependencyProvidingContainer
        {
            CachedDependencies = mouseSource == null
                ? new[] { (typeof(RealmAccess), (object)Realm) }
                : new[] { (typeof(RealmAccess), (object)Realm), (typeof(IOmsMouseAxisSource), (object)mouseSource) },
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Child = section,
        };

        private static IEnumerable<SettingsButtonV2> buttons(CompositeDrawable parent, string label)
            => parent.ChildrenOfType<SettingsButtonV2>().Where(button => button.Text.ToString() == label);

        private static string rowText(CompositeDrawable parent)
            => string.Concat(parent.ChildrenOfType<OsuSpriteText>().Select(text => text.Text.ToString()));

        private sealed class DiagnosticMouseSource : IOmsMouseAxisSource
        {
            private readonly MouseHandler? mouseHandler;
            private int captureLeases;
            private bool active = true;
            public bool IsActive
            {
                get => active && CaptureLeases > 0 && (mouseHandler?.UseRelativeMode.Value ?? true);
                set => active = value;
            }
            public event Action<OmsRelativeMouseMovement>? RelativeMouseMoved;
            public event Action? InputReset;
            public int MovementSubscribers => RelativeMouseMoved?.GetInvocationList().Length ?? 0;
            public int ResetSubscribers => InputReset?.GetInvocationList().Length ?? 0;
            public int CaptureLeases => Volatile.Read(ref captureLeases);

            public DiagnosticMouseSource(MouseHandler? mouseHandler = null) => this.mouseHandler = mouseHandler;

            public IDisposable AcquireCapture()
            {
                Interlocked.Increment(ref captureLeases);
                return new CaptureLease(() => Interlocked.Decrement(ref captureLeases));
            }

            public void Emit(float x, float y) => RelativeMouseMoved?.Invoke(new OmsRelativeMouseMovement(x, y, Stopwatch.GetTimestamp()));

            public void Reset() => InputReset?.Invoke();

            private sealed class CaptureLease : IDisposable
            {
                private Action? release;

                public CaptureLease(Action release) => this.release = release;

                public void Dispose() => Interlocked.Exchange(ref release, null)?.Invoke();
            }
        }

        private sealed class DiagnosticDeviceProvider : IOmsHidButtonDeviceProvider
        {
            public DiagnosticDevice Device { get; }
            private readonly Action? disposeAction;
            private int disposed;
            private int disposalStarted;
            private int discoveryCount;
            public bool Disposed => Volatile.Read(ref disposed) != 0;
            public bool DisposalStarted => Volatile.Read(ref disposalStarted) != 0;
            public int DiscoveryCount => Volatile.Read(ref discoveryCount);
            public event EventHandler? DevicesChanged { add { } remove { } }

            public DiagnosticDeviceProvider(string identifier, Action? disposeAction = null)
            {
                Device = new DiagnosticDevice(identifier);
                this.disposeAction = disposeAction;
            }

            public IEnumerable<IOmsHidButtonDevice> GetDevices(IEnumerable<string> identifiers)
            {
                Interlocked.Increment(ref discoveryCount);
                return new[] { Device };
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref disposalStarted, 1);
                disposeAction?.Invoke();
                Interlocked.Exchange(ref disposed, 1);
            }
        }

        private sealed class DiagnosticDevice : IOmsHidButtonDevice
        {
            public ConcurrentQueue<IReadOnlyList<OmsHidDeviceChange>> Changes { get; } = new ConcurrentQueue<IReadOnlyList<OmsHidDeviceChange>>();
            public string Identifier { get; }
            public bool IsConnected { get; private set; } = true;
            public bool HasPendingChanges => !Changes.IsEmpty;
            public DiagnosticDevice(string identifier) => Identifier = identifier;
            public bool TryReadChanges(out IReadOnlyList<OmsHidDeviceChange> changes)
            {
                if (Changes.TryDequeue(out IReadOnlyList<OmsHidDeviceChange>? next))
                {
                    changes = next;
                    return true;
                }
                changes = Array.Empty<OmsHidDeviceChange>();
                return false;
            }
            public void Dispose() => IsConnected = false;
        }
    }

}
