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
- 总物理宽只改 `BmsPlayfieldLayoutProfile.CreateDefault().PlayfieldWidth`；scratch:key 比例改 `ScratchLaneRelativeWidth/NormalLaneRelativeWidth`。
- `BmsRulesetSetting.PlayfieldWidth` 不覆盖 strict profile；不要把已删除的 scale/offset config 接回来。
- 当前关键值：scratch:key `1.5:1`，playfield height `0.92`，LN body width 默认 `0.5775`；具体断言以测试/代码为准。`LongNoteBodyWidth` 是当前唯一已有字段级安全域的 geometry：只接受 finite 且 `0 < width <= 1`，缺失/非法按 typed 原因回到默认；其余 geometry 与 screen-space/cross-field 仍未统一验证。
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
- 几何由 `BmsPlayfield` 读取配置后重建 profile；无 override 时必须保持原 profile 字节/行为一致。
- `LongNoteBody` 是 source-bound 窄例外：素材帧与 resolved `LongNoteBodyWidth` 同 package revision 发布，renderer 不再从 aggregate skin 重读宽度；selected 坏 body 不能与下层裸同名纹理/裸宽度拼件。这个例外不等于已建立完整 `BmsGameplayLayoutSnapshot`。
- 相关测试：lane layout、skin geometry、LN state、gauge placement/visibility、HUD strip。旧精确数字和演进过程查 P1-A CHANGELOG。
