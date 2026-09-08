---
name: reference_bms_default_skin_geometry
description: BMS 默认几何旋钮、归一化地雷与 HUD/gauge 视觉合同
metadata:
  node_type: memory
  type: reference
---

# BMS 默认几何召回

权威当前态：[P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)；皮肤约束：[P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)。本文件只记反直觉旋钮。

## 车道与音符

- 物理 lane 宽 = `RelativeWidth / TotalRelativeWidth × PlayfieldWidth`。同比缩放所有 relative width 会被归一化抵消。
- production总物理宽由唯一`BmsGameplayLayoutSolver`读取`BmsGameplayLayoutConfiguration`后结合safe bounds、aspect/DPI求解；scratch:key比例来自该配置的`ScratchLaneRelativeWidth/NormalLaneRelativeWidth`。`BmsPlayfieldLayoutProfile`只是同一solver产出的兼容view，不能另调`CreateDefault()`形成第二几何authority。
- `BmsRulesetSetting.PlayfieldWidth` 不覆盖 strict profile；不要把已删除的 scale/offset config 接回来。
- 当前关键值：scratch:key `1.5:1`，playfield height默认`0.92`，LN body width默认`0.5775`；最终像素尺寸服从solver的safe-area/aspect/DPI与BGA/HUD留位。C3已统一geometry字段的finite/range及screen-space/cross-field验证；`LongNoteBodyWidth`保留独立标量规则：finite且`0 < width <= 1`，缺失/非法按typed原因回到默认。
- playfield 顶边贴屏，`HitTargetVerticalOffset=0`。改变高度只改像素路程，不改 GN/TimeRange；不要重加整体向下 offset。

## LN 视觉

- managed source-bound body 与程序化默认 body 共用同一个真实保持状态宿主：Idle/Holding 保留 active material 色彩、alpha `0.8`，Broken 灰暗、alpha `0.32`，约 `80ms` tint/fade。仅 HCN regrab 可从 Broken 回 Holding，CN/LN 不可；异步 material 在状态改变后到达时必须立即继承当前状态。
- tail 默认 `Alpha=0` 只是视觉；tail judgement 仍存在，皮肤 lookup 仍保留。

## HUD 合同

- groove gauge 是判定线下方、与 playfield 等宽的 HUD child；`IBmsHudLayoutDisplay.SetComponents(wrappedHud,gauge,combo)` 签名不可改。
- combo 位于 playfield 中心，仅标签+数字，无背景块。
- `BmsGaugeBar : HealthDisplay` 会被 `HUDOverlay.ShowHealthBar=false` 淡出；BMS gauge 必须在自身订阅中重申可见。测试必须使用真实 HUDOverlay，裸 DI 容器复现不了。
- upstream 默认 combo 是 `LegacyDefaultComboCounter`，不是 `ComboCounter`。从 wrapped HUD 配置树移除时要同时匹配它与 leaderboard；用户要求“移除”，不要用 `Alpha=0` 假隐藏。

## 皮肤读取

- 有贴图时贴图主导，不再叠程序化 colour；无贴图才走 ini colour/palette。
- `BmsGameplayLayoutProvider.TryPrepareExact()`在background owner lease内读取配置、调用唯一solver并准备material/scene；`BmsPlayfield`只消费同一publication的`BmsGameplayLayoutSnapshot`，不重建profile或重读aggregate geometry。
- `LongNoteBody`素材帧与resolved width和其余geometry/material/scene共同绑定exact publication；selected坏body不能与下层裸同名纹理/裸宽度拼件。完整layout已由C3闭合，C4/C5在其上扩展material与scene/event，不能把早期body纵切的局限当作当前状态。
- 代码证据：`BmsGameplayLayoutSolver.Solve()`、`BmsGameplayLayoutProvider.TryPrepareExact()`、`BmsPlayfield.initialiseLayoutGraph()`；实际renderer矩阵见`BmsGameplayLayoutCurrentRevisionProductTest`和`BmsAllKeymodeSceneProductionMatrixProductTest`。2026-09-09核对生产链与测试源码；本轮实际测试范围和结果见[项目审查记录](../../doc_md/other/PROJECT_PROGRESS_AUDIT_20260909.md)。
- 相关测试：lane layout、skin geometry、LN state、gauge placement/visibility、HUD strip。旧精确数字和演进过程查 P1-A CHANGELOG。
