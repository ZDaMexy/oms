// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using oms.Input;
using oms.Input.Devices;

namespace osu.Game.Rulesets.Bms
{
    /// <summary>Local settings preview. Uses the gameplay interpreters without forwarding to gameplay or recording.</summary>
    internal sealed class BmsInputDiagnostics : IDisposable
    {
        private readonly OmsBindingTrigger trigger;
        private readonly OmsHidAxisInputHandler? hidAxis;
        private readonly OmsMouseAxisInputHandler? mouseAxis;
        private readonly HashSet<int> pressedButtons = new HashSet<int>();
        private readonly Dictionary<int, OmsHidAxisChange> axes = new Dictionary<int, OmsHidAxisChange>();
        private double currentTime;
        private double lastMovementTime = double.NegativeInfinity;
        private double latestMovement;
        private double rawMouseX;
        private double rawMouseY;

        public int TriggerCount { get; private set; }

        public bool IsPressed { get; private set; }

        public double TotalMovement { get; private set; }

        public bool IsMoving => currentTime - lastMovementTime <= trigger.AxisSettings.StopTimeoutMilliseconds;

        public BmsInputDiagnostics(OmsAction action, OmsBindingTrigger trigger)
        {
            this.trigger = trigger;
            var bindings = new[] { new OmsBinding(action, trigger) };
            if (trigger.Kind == OmsBindingTriggerKind.HidAxis)
                hidAxis = new OmsHidAxisInputHandler(bindings, _ => onPress(), _ => IsPressed = false, onMotion);
            else if (trigger.Kind == OmsBindingTriggerKind.MouseAxis)
                mouseAxis = new OmsMouseAxisInputHandler(bindings, _ => onPress(), _ => IsPressed = false, onMotion);
        }

        public void BeginPoll(double milliseconds)
        {
            currentTime = milliseconds;
            hidAxis?.BeginPolling();
            hidAxis?.Update(milliseconds);
            mouseAxis?.BeginFrame();
        }

        public void Apply(OmsHidDeviceChange change)
        {
            if (change.Kind == OmsHidDeviceChangeKind.Button)
            {
                OmsHidButtonChange button = change.ButtonChange;
                if (button.Pressed)
                    pressedButtons.Add(button.ButtonIndex);
                else
                    pressedButtons.Remove(button.ButtonIndex);

                if (trigger.Kind == OmsBindingTriggerKind.HidButton && button.ButtonIndex == trigger.ButtonIndex)
                {
                    if (button.Pressed && !IsPressed)
                        onPress();
                    else if (!button.Pressed)
                        IsPressed = false;
                }
            }
            else
            {
                axes[change.AxisChange.AxisIndex] = change.AxisChange;
                hidAxis?.ApplyAxisChange(change.AxisChange);
            }
        }

        public void ApplyMouse(float x, float y)
        {
            rawMouseX += x;
            rawMouseY += y;
            mouseAxis?.ApplyAxisDelta(OmsMouseAxis.X, x);
            mouseAxis?.ApplyAxisDelta(OmsMouseAxis.Y, y);
        }

        public void EndPoll()
        {
            hidAxis?.FinishPolling();
            mouseAxis?.FinishFrame();
        }

        private void onPress()
        {
            TriggerCount++;
            IsPressed = true;
        }

        private void onMotion(OmsAxisMotion motion)
        {
            latestMovement = motion.Delta;
            TotalMovement += motion.Delta;
            lastMovementTime = currentTime;
        }

        public string Describe(bool connected)
        {
            string state = connected ? "已连接，持续监听中" : "设备未连接，等待重新连接";
            string result = trigger.Kind == OmsBindingTriggerKind.HidButton || trigger.AxisSettings.Mode == OmsAxisMode.CentredPosition
                ? $"绑定输出：{(IsPressed ? "按下" : "松开")}；累计触发 {TriggerCount} 次"
                : $"绑定输出：{(IsMoving ? "运动中" : "已停止")}；累计触发 {TriggerCount} 次\n"
                  + $"接受运动：{signed(latestMovement)}；累计 {signed(TotalMovement)} {motionUnit(trigger.AxisSettings.Mode)}";

            if (trigger.Kind == OmsBindingTriggerKind.MouseAxis)
                return $"鼠标持续测试；移动累计 X {signed(rawMouseX)}，Y {signed(rawMouseY)} counts\n{result}";

            string buttons = pressedButtons.Count == 0 ? "无" : string.Join(", ", pressedButtons.OrderBy(index => index));
            string axisText = string.Join("\n", axes.OrderBy(pair => pair.Key).Select(pair =>
            {
                OmsHidAxisChange axis = pair.Value;
                string value = axis.Value?.ToString(CultureInfo.InvariantCulture) ?? "相对输入";
                string range = axis.Minimum.HasValue && axis.Maximum.HasValue ? $" [{axis.Minimum} … {axis.Maximum}]" : string.Empty;
                return $"轴 {pair.Key}：{value}{range}，变化 {signed(axis.Delta)}";
            }));
            string? interpretationIssue = getInterpretationIssue();
            return $"{state}\n按下的按钮：{buttons}\n{axisText}\n"
                   + (interpretationIssue == null ? string.Empty : $"当前设置无法解释此轴：{interpretationIssue}\n") + result;
        }

        private string? getInterpretationIssue()
        {
            OmsAxisSettings settings = trigger.AxisSettings;
            if (trigger.Kind != OmsBindingTriggerKind.HidAxis || settings.Mode == OmsAxisMode.RelativeMotion
                || !axes.TryGetValue(trigger.AxisIndex, out OmsHidAxisChange axis))
                return null;

            if (axis.IsRelative || !axis.Value.HasValue)
                return "此模式需要绝对位置；当前报告只有相对变化，请确认轴用途。";

            int? minimum = settings.Minimum ?? axis.Minimum;
            int? maximum = settings.Maximum ?? axis.Maximum;
            if (!minimum.HasValue || !maximum.HasValue || minimum.Value >= maximum.Value)
                return "设备没有提供有效范围，请填写高级范围校准。";

            if (settings.Mode == OmsAxisMode.CircularPosition)
                return axis.Value.Value < minimum.Value || axis.Value.Value > maximum.Value ? "当前原始值超出校准范围，请核对最小值与最大值。" : null;

            double centre = settings.Centre ?? ((double)minimum.Value + maximum.Value) / 2;
            return centre <= minimum.Value || centre >= maximum.Value ? "中心值必须位于当前设备范围内部。" : null;
        }

        public void Disconnect()
        {
            pressedButtons.Clear();
            IsPressed = false;
            lastMovementTime = double.NegativeInfinity;
            hidAxis?.ReleaseAll();
            mouseAxis?.ReleaseAll();
        }

        public void Dispose() => Disconnect();

        private static string signed(double value) => value.ToString("+0.####;-0.####;0", CultureInfo.InvariantCulture);

        private static string motionUnit(OmsAxisMode mode) => mode == OmsAxisMode.RelativeMotion ? "counts" : "转";
    }
}
