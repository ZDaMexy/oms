# P1-D 开发进度：控制器校准与诊断

> 最后更新：2026-04-20

## 当前阶段

- `P1-D` 已完成子线建档。
- 当前仓库仅有 supplemental bindings 与 live capture 基线：`BmsSettingsSubsection` 已接入 variant-aware supplemental editor，并为 HID button / HID axis / mouse-axis 提供 per-row live capture。
- 通用输入设置仍只有上游层面的 joystick deadzone 等基础项；BMS 专属 calibration UI、scratch 模式说明、live diagnostics 面板都还没有正式产品面。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 子线建档 | 已完成 | 四件套已建立 |
| diagnostics state 盘点 | 进行中 | supplemental editor / live capture 基线已存在，但缺统一产品面 |
| calibration UI | 未开始 | 当前尚无 BMS 专属 deadzone / sensitivity / diagnostics UI |
| 对外说明文案 | 未开始 | scratch 模式说明与设备诊断口径仍缺正式入口 |

## 验证记录

- 本轮状态同步基于当前代码结构完成，未新增构建或测试命令。