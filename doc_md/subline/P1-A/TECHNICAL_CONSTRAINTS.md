# P1-A 技术约束：产品面、release gate 与皮肤边界

> 最后更新：2026-04-20
> 本文件记录该专题的硬约束。若实现与本文冲突，先修正文档或代码其中一边，再继续开发。

## 归线约束

1. 本子线属于 Phase 1.x 下的 `P1-A`，但与 `P1-C` 强耦合；不得借题把 `FHS`、`dan`、`1P/2P flip`、BSS / MSS 提前带入当前主交付。
2. `P1-A` 负责边界、lookup、HUD 宿主与 fallback 合同；`P1-C` 只能在这条边界内承接 runtime 反馈表达。

## 术语与产品约束

1. 当前 `GN / WN` 可以表述为 OMS 当前 `Normal / Floating / Classic Hi-Speed + Sudden / Hidden / Lift` runtime surface 的反馈，但不得对外宣称为完整 IIDX `FHS`。
2. settings 页必须只暴露 Hi-Speed 模式与当前模式数值；不得在 settings 中显示 `GN / 可见毫秒`，也不得制造“已完整支持 BPM 补偿 / FHS 全语义”的错误预期。
3. `Lift` 继续是 geometry control；`Hidden` 继续是下遮挡。两者在命名、状态、HUD 表达和 pre-start overlay 中都不得重新混写。
4. 当前公开 Hi-Speed 范围必须保持：`Normal 1.0 - 20.0`、`Floating 0.5 - 10.0`、`Classic 0.5 - 10.0`；其中 `Classic` 的 base time 映射应保持 `TimeRange = (100000 / 13) / HS`，官方 sample `HS 10 + WN 350 => GN 300` 必须持续成立。
5. 当前运行时 geometry profile 已冻结；除 `Sudden / Hidden / Lift` 外，旧的 playfield / receptor / bar-line layout config 不得继续作为用户可见 contract 影响速度或几何语义。
6. `UI_LaneCoverFocus` 允许复用为 pre-start hold gate，但该窗口必须表现为正式 runtime operator surface，不得退化成 debug overlay 或无 fallback 的临时实现。

## 皮肤边界约束

1. 新的 gameplay feedback 必须是 BMS-owned skinnable component，不得复用 mania lookup，也不得回落到上游默认皮肤语义。
2. 若新增纹理、采样或 config key，必须使用 BMS 专属命名空间，不得借用 legacy mania 资源键名。
3. 不得通过遍历 wrapped HUD 子节点、偷改 `GaugeBar`、偷改 `ComboCounter` 的方式植入 speed feedback。
4. 任何更改皮肤边界、HUD 宿主、fallback 语义或 release gate 的改动，都必须同步更新本目录四件套以及受影响的 `../../mainline/` 文档。

## HUD 宿主约束

1. 不得直接破坏现有 `IBmsHudLayoutDisplay` 签名。若需要额外组件，必须使用 versioned optional interface、wrapper contract，或等价的向后兼容方案。
2. 旧版 HUD provider 在未实现新接口时必须保持可用；新反馈组件应由 `OmsSkin` 默认路径独立 fallback。
3. 默认 HUD 不得依赖 Debug overlay、临时 Box 或只在 toast 中可见的链路来维持功能完整。

## 反馈家族约束

1. 当前专题的第一阶段只收口 speed feedback；后续 `FAST/SLOW`、judge display、visual timing-offset、EX pacemaker 必须尽量沿同一 feedback family 承载，不再各自新开 ad-hoc overlay。
2. judgement 位置若需要与 feedback 联动，必须新增显式 BMS 位置合同；不得继续复制新的硬编码偏移值。
3. toast 可以保留为瞬时强调层，但不得继续承担唯一权威反馈职责。

## 测试与发布约束

1. 任何新增 feedback component 都必须补 fallback 回归：`OmsSkin` 默认路径、无该组件的用户皮肤路径、实现旧版 HUD 接口的用户皮肤路径。
2. 数值回归必须同时锁定 tri-mode 当前合同：`Classic` sample、mode-aware `GN / WN` 语义、以及 pre-start odd/even key 调速映射，直到明确进入新的速度语义专题。
3. 只有当默认路径、fallback 路径、HUD 宿主兼容性和文档同步全部完成后，才允许把该专题标记为已落地。