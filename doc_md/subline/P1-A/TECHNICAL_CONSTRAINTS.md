# P1-A 技术约束：产品面、release gate 与皮肤边界

> 最后更新：2026-04-23
> 本文件记录该专题的硬约束。若实现与本文冲突，先修正文档或代码其中一边，再继续开发。

## 归线约束

1. 本子线属于 Phase 1.x 下的 `P1-A`，但与 `P1-C` 强耦合；不得借题把 `FHS`、`dan`、`1P/2P flip`、BSS / MSS 提前带入当前主交付。
2. `P1-A` 负责边界、lookup、HUD 宿主与 fallback 合同；`P1-C` 只能在这条边界内承接 runtime 反馈表达。

## 术语与产品约束

1. 当前 `GN / WN` 可以表述为 OMS 当前 `Normal / Floating / Classic Hi-Speed + Sudden / Hidden / Lift` runtime surface 的反馈，但不得对外宣称为完整 IIDX `FHS`。
2. settings 页必须只暴露 Hi-Speed 模式与当前模式数值；不得在 settings 中显示 `GN / 可见毫秒`，也不得制造“已完整支持 BPM 补偿 / FHS 全语义”的错误预期。
3. `Lift` 继续是 geometry control；`Hidden` 继续是下遮挡。两者在命名、状态、HUD 表达和 pre-start overlay 中都不得重新混写。
4. 当前公开 Hi-Speed 范围必须保持：`Normal 1.0 - 20.0`、`Floating 0.5 - 10.0`、`Classic 0.5 - 10.0`；其中 `Classic` 的 base time 映射应保持 `TimeRange = (100000 / 13) / HS`，官方 sample `HS 10 + WN 350 => GN 300` 必须持续成立。
5. 当前运行时 geometry profile 已冻结；`Playfield Scale` 必须固定为 `1.0` 并保持不可配置，因为缩放会破坏皮肤编排并扭曲权威 visual-speed surface。
6. 除 `Sudden / Hidden / Lift` 与当前 single-play `Playfield Style`（`1P（居左）` / `2P（居右）` / `居中（左皿）` / `居中（右皿）`，仅作用于 5K / 7K 的 playfield 停靠与 scratch 视觉侧别，不改 binding flip）外，旧的 playfield / receptor / bar-line layout config 不得继续作为用户可见 contract 影响速度或几何语义。
7. `UI_PreStartHold` 必须承担 pre-start hold gate；`UI_LaneCoverFocus` 必须保持为 click-to-cycle 持久 target 的独立动作。该窗口必须表现为正式 runtime operator surface，不得退化成 debug overlay 或无 fallback 的临时实现。
8. BMS mod 选项与配置记忆必须保持 ruleset-local；当前 `PersistedModState` 只允许作用于 BMS，不得让 mania / 全局 `SelectedMods` 获得隐式共享持久化。
9. 冷启动时不得在 `RulesetConfigCache` 未 ready 前直接调用 `GetConfigFor()` 去构建 BMS mod persistence；正确合同是先允许无 config 的首轮 ruleset apply，再在 cache ready 后 replay 当前 ruleset 完成 restore。否则会同时打破 startup release gate（误报 ruleset issue）与 BMS mod 冷启动记忆。
10. 对实现 `IPreserveSettingsWhenDisabled` 的 configurable BMS mod，停用只意味着 inactive，不等同于 reset；除显式重置入口或配置迁移外，不得在 mod 菜单关闭时清空其最后配置。
11. `首次启动向导`、`Run setup wizard` 与无谱面引导这类共享 onboarding / settings-entry surface 默认归 `P1-A`；若页面只是复用外部 / 内部谱库与按键绑定面板，其存储 / 输入语义仍分别归 `P1-H` / `P1-B`，不得为暴露面调整另开主线。
12. 共享层首次启动向导若需触发 BMS-only runtime 能力，必须保持 `osu.Game` 不直接引用 `osu.Game.Rulesets.Bms`；可用反射 / 抽象边界，但模块缺失时页面需优雅退化，而不是在构造或 load 阶段抛异常。
13. 首次启动向导中用户可见的 OMS 文案，若需覆盖上游翻译，必须使用 OMS-owned localisation namespace + 对应 `.resx`；只改 `*Strings.cs` fallback 不足以覆盖简中等非英文资源。

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
