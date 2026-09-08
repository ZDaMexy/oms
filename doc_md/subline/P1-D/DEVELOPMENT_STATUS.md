# P1-D 开发进度：控制器校准与诊断

> 最后更新：2026-09-09（本地代码/测试源码审查；产品验证未刷新）
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

- 当前仅完成基于代码结构的状态同步，尚无新增构建或测试执行。
- 后续若出现按日期展开的实现或验证，统一写入 [CHANGELOG.md](CHANGELOG.md)。

## 文档治理验证

2026-09-09：核对 [BmsSettingsSubsection](../../../osu.Game.Rulesets.Bms/BmsSettingsSubsection.cs) 到 [supplemental editor](../../../osu.Game.Rulesets.Bms/BmsSupplementalBindingSettingsSection.cs) 的真实入口及[对应测试源码](../../../osu.Game.Rulesets.Bms.Tests/TestSceneBmsSupplementalBindingSettingsSection.cs)。按键/轴捕获、方向反转与保存已存在，独立 deadzone/sensitivity 校准及持续 diagnostics 面板仍未交付；本节仅记录源码审查；全局实测见主线最新验证。
