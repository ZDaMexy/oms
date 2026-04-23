# P1-C 技术约束：判定语义、绿色数字与反馈闭环

> 最后更新：2026-04-22
> 本文件记录 `P1-C` 的硬约束。若实现与本文冲突，先修正文档或代码其中一边，再继续开发。

## 归线约束

1. 本子线属于 Phase 1.x 下的 `P1-C`，不得借题把 `FHS`、`dan`、`1P/2P flip`、BSS / MSS 提前带入当前主交付。
2. `P1-C` 的 feedback family 必须建立在 `P1-A` 冻结的 BMS HUD 宿主与皮肤边界上，不得绕开 `P1-A` 直接改写旧版接口。

## 术语与产品约束

1. 当前 `GN / WN` 可以表述为 OMS 当前 `Normal / Floating / Classic Hi-Speed + Sudden / Hidden / Lift` runtime surface 的反馈，但不得对外宣称为完整 IIDX `FHS`。
2. 若引入常驻 GN HUD、toast 或 pre-start overlay，文案必须与当前 tri-mode surface 一致；settings 中不得显示 `GN / 可见毫秒`，也不得制造“已完整支持 BPM 补偿 / FHS 全语义”的错误预期。
3. `Lift` 继续是 geometry control；`Hidden` 继续是下遮挡。两者在命名、状态、HUD 表达与 pre-start hold 交互中都不得重新混写。
4. 当前 `Floating` 只允许按“initial-BPM anchored surface”对外表述，不得误写成完整 mid-song re-float parity。

## GN 公式约束（2026-04-20 经 iidx.org 外部参考验证）

1. **IIDX 原始公式**：`GN = 10 × (note 可见帧数 @60fps)` = `Round(VisibleLaneTime_ms × 0.6)`。当前 `BmsScrollSpeedMetrics.GreenNumber` 实现与此完全一致，**不得修改公式**除非 IIDX 参考本身变更。
2. **WhiteNumber 语义**：`WhiteNumber = SuddenUnits`（SUDDEN+ 遮挡面积占全高千分比），**不受 LIFT 影响**。这与 IIDX 语义一致；beatoraja 在有 LIFT 时的重算语义不同但不是 OMS 当前目标。
3. **VisibleLaneUnits 计算**：`1000 - SuddenUnits - HiddenUnits`。LIFT 不参与此计算（与 IIDX 一致），但 LIFT 通过几何链（`ScrollLengthRatio`）间接影响 `VisibleLaneTime` 和 `GreenNumber`。
4. **Soflan 与 GN 显示**：当前 HUD / pre-start overlay 只显示单个 runtime GN，不显示 soflan 范围。`Floating` 首轮虽然会按 initial BPM 重新锚定 base time，但在 full FHS 未实现前，仍不得引入 soflan GN 范围显示（IIDX 的 min-max GN 范围仅在完整 FHS 模式下有意义）。
5. **GN 典型值参考**：IIDX 玩家常用 GN 区间为 250（极快）– 330（偏慢），等价 VisibleLaneTime ≈ 417ms – 550ms。此数据仅供参考，不作为 OMS 硬编码限制。
6. **Hi-Speed 可调范围**：当前公开范围应保持 `Normal 1.0 - 20.0`、`Floating 0.5 - 10.0`、`Classic 0.5 - 10.0`；除非明确另开严格 HS / FHS 语义专题，不得悄悄修改这些用户可见范围。
7. **Classic base time 映射**：当前 `Classic` 的 `ComputeScrollTime(HS)` 必须保持为 `(100000 / 13) / HS`；`HS 10 + WN 350 => GN 300` 的官方 sample 必须持续成立。
8. **pre-start hold 调速窗口**：进入 BMS gameplay 后必须先有 5 秒 delayed-start 窗口；按住 `UI_PreStartHold` 时应阻塞开谱、显示当前模式与数值，并允许按键位奇数列加速、偶数列减速，同时 `UI_LaneCoverFocus`（click-to-cycle）/ 滚轮 / 中键可继续调节 `Sudden / Hidden / Lift` 与 target cycle。`UI_PreStartHold` 与 `UI_LaneCoverFocus` 已拆为独立动作：PreStartHold = 按住阻塞开谱，LaneCoverFocus = 单击循环目标。
9. **strict geometry 冻结**：当前运行时 geometry profile 已冻结；`Playfield Scale` 必须固定为 `1.0` 并保持不可配置，因为缩放会破坏皮肤编排，并把非权威几何缩放混入 `VisibleLaneTime` / `GreenNumber` 体感。
10. **GN 语义边界**：除 `Sudden / Hidden / Lift` 与当前 single-play `Playfield Style`（`1P（居左）` / `2P（居右）` / `居中（左皿）` / `居中（右皿）`，只改变 5K / 7K 的 playfield 停靠与 scratch 视觉侧别）外，用户可见 layout override 不得再进入 `VisibleLaneTime` / `GreenNumber` 语义链。
11. **BMS mod 记忆合同**：mod 选中状态与 remembered settings 只允许作为 BMS ruleset-local snapshot 持久化；切 ruleset / 重启可恢复，但不得把 mania / 全局 `SelectedMods` 变成隐式共享存储。
12. **gameplay adjustment 回写合同**：`Sudden / Hidden / Lift` 的局内滚轮调整只有在 mod-local `RememberGameplayChanges = true` 时才允许写回当前 BMS selected mod 与持久化快照；关闭时必须保持 current-play-only 语义。
13. **startup replay 时序合同**：`OsuGameBase` 在 startup 首次处理 BMS ruleset 时，若 `RulesetConfigCache` 尚未 ready，不得直接 `GetConfigFor()`；正确合同是允许无 config 的首轮 apply，并在 cache ready 后 replay 当前 ruleset 完成 `PersistedModState` restore。否则会同时造成冷启动 mod 记忆丢失与误报 ruleset failure。

## 反馈家族约束

1. speed feedback、`FAST/SLOW`、judge display、visual timing-offset、EX pacemaker 应尽量沿同一 feedback family 承载，不再各自新开 ad-hoc overlay。
2. 不得通过遍历 wrapped HUD 子节点、偷改 `GaugeBar`、偷改 `ComboCounter` 的方式植入反馈内容。
3. toast 可以保留为瞬时强调层，但不得继续承担唯一权威反馈职责。

## 判定语义约束

1. BRJ / LR2 parity 的补强必须与 feedback 验证链保持一致，不允许只改窗口不改训练反馈表达。
2. 任何改变 judge family 语义、反馈术语、results 训练表达或常驻 HUD 的改动，都必须同步更新本目录四件套以及受影响的 `../../mainline/` 文档。
