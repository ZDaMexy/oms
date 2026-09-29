# P1-D 开发进度：控制器校准与诊断

> 最后核对：2026-09-29（设备枚举留存证据复核；校准/实机仍未签收）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，当前执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

## 当前阶段

- 当前仓库仅有 supplemental bindings 与 live capture 基线：`BmsSettingsSubsection` 已接入 variant-aware supplemental editor，并为 HID button / HID axis / mouse-axis 提供 per-row live capture。
- 通用输入设置仍只有上游层面的 joystick deadzone 等基础项；BMS 专属 calibration UI、scratch 模式说明、live diagnostics 面板都还没有正式产品面。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| diagnostics state 盘点 | 进行中 | supplemental editor / live capture 基线已存在，但缺统一产品面 |
| calibration UI | 未开始 | 当前尚无 BMS 专属 deadzone / sensitivity / diagnostics UI |
| 对外说明文案 | 未开始 | scratch 模式说明与设备诊断口径仍缺正式入口 |

## 最近一次验证

2026-09-22 BMS full 留存 TRX 中，`TestDeviceDiscoveryRunsOffCallingThread` 通过；它只证明设备枚举离开调用线程，不是 live capture、完整设置 UI 或真实校准体验验收。完整回归与其既有失败见 [P1-C 最新验证](../P1-C/DEVELOPMENT_STATUS.md#最近一次验证)。

## 文档治理验证

2026-09-29：对照 [BmsSettingsSubsection](../../../osu.Game.Rulesets.Bms/BmsSettingsSubsection.cs)、[supplemental editor](../../../osu.Game.Rulesets.Bms/BmsSupplementalBindingSettingsSection.cs) 与[设备枚举测试](../../../osu.Game.Rulesets.Bms.Tests/TestSceneBmsSupplementalBindingSettingsSection.cs)，区分源码可见的按键/轴捕获、反转/保存与留存测试实际覆盖范围。未运行新的产品测试；独立校准及持续 diagnostics 产品面仍未交付。此前审查见 [CHANGELOG](CHANGELOG.md)。
