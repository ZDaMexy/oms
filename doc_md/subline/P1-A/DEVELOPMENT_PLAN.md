# P1-A 开发计划：产品面、release gate 与皮肤边界

> 最后更新：2026-04-23
> 主线总规划见 [../../mainline/DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md)。本文件只拆解 `P1-A` 的执行顺序；`P1-C` 的反馈闭环计划见 [../P1-C/DEVELOPMENT_PLAN.md](../P1-C/DEVELOPMENT_PLAN.md)。

## 专题定位

| 维度 | 归属 | 说明 |
| --- | --- | --- |
| 主归属 | `P1-A` | 冻结 BMS HUD / skin boundary，确定 skin lookup、HUD 宿主与 fallback 的扩展边界 |
| 协作子线 | `P1-C` | 绿色数字、速度反馈与训练反馈闭环依赖本子线冻结的宿主合同 |
| 支线参考 | `other` | 受 [../../other/IIDX_REFERENCE_AUDIT.md](../../other/IIDX_REFERENCE_AUDIT.md) 约束，但当前不等价于完整 `FHS` 落地 |
| 明确不归线 | Phase 2 | `FHS`、`dan`、`1P/2P flip`、BSS / MSS、其他训练模式保持冻结 |

## 当前确认基线

- `BmsSkinTransformer` 的 BMS / mania 边界已收口，BMS lookup 与 fallback 语义已可作为稳定宿主合同继续扩展。
- `BmsScrollSpeedMetrics` 现已按 `Normal / Floating / Classic Hi-Speed + Sudden / Hidden / Lift` 计算 `VisibleLaneTime`、`WhiteNumber`、`GreenNumber`；其中 `Classic` 继续锁定官方 sample `HS 10 + WN 350 => GN 300`，`Floating` 目前为 initial-BPM anchored surface。
- 当前 runtime 已同时具备 `BmsSpeedMetricsToast` 与常驻 speed-feedback HUD；toast 退位为操作确认层，常驻 feedback card 承担权威表达。
- 内部 `BmsPlayfieldLayoutProfile` abstraction gate 仍保留，但当前 runtime geometry override surface 已冻结，不再通过设置页暴露会扰动 strict profile 的 layout sliders。
- 当前 `IBmsHudLayoutDisplay` 只接受 wrapped HUD、gauge bar、combo counter 三类组件；若直接扩签名，会打断现有 HUD provider 合同。
- judgement 基线与默认 gameplay feedback 摆位现已通过 `BmsGameplayFeedbackLayout` 收口为 shared position contract；后续若继续联动 judge display / feedback，应扩展这条合同，而不是重新散落新的位置常量。
- BMS mod ruleset-local memory surface 现已补齐 cold-start path：若 startup 首次 ruleset change 早于 `RulesetConfigCache.LoadComplete()`，宿主必须延后 replay 当前 ruleset 到 cache ready 后再做 restore；这条路径现已有 dedicated integration coverage。
- 首次启动向导、`Run setup wizard` 与无谱面引导这类共享 onboarding / settings-entry surface 归 `P1-A`；若页面只是复用外部 / 内部谱库或按键绑定面板，则 `P1-H` / `P1-B` 只记从属影响，不为此另开子线。
- 共享层 first-run wizard 若需触发 BMS-only runtime 能力，必须继续避开 `osu.Game -> osu.Game.Rulesets.Bms` 编译期依赖；当前难度表导入页使用反射加载 `BmsDifficultyTableManager`，这条边界应继续保持。

## 专题目标

1. 先冻结 BMS-owned feedback contract，避免绿色数字、lane cover focus、后续 `FAST/SLOW` 与 pacemaker 各走一条 ad-hoc HUD 链。
2. 把当前 OMS runtime 的速度反馈明确为“tri-mode runtime surface 下的权威表达”，同时明确 `Floating` 仍不是完整 `FHS`。
3. 保持对现有 BMS 用户皮肤和 HUD 布局接口的向后兼容，不为专题推进破坏既有 fallback 语义。

## 分期计划

### A0：文档与边界冻结

**状态：已完成**

- 盘点 BMS skin boundary、HUD 宿主接口、`GN / WN / Lift` 计算链与 `Sudden / Hidden` 联动。
- 建立专题级计划 / 状态 / 技术约束文档。
- 把专题明确归线到 `P1-A / P1-C`，并同步挂接到主线文档。

### A0.5：首次启动向导与设置导流

**状态：已完成首轮落地**

- 首次启动设置已收口为六步 OMS flow：欢迎、UI 缩放、获取谱面、导入、难度表设置、按键绑定。
- `获取谱面` / `导入` / `难度表设置` / `按键绑定` 四页当前都属于共享产品表面：可复用现有 `ExternalLibrarySettings`、keybinding subsection 与 BMS difficulty-table runtime，但不应因此改写各自底层子线归属。
- 欢迎页、获取谱面页与导入页的可见文案若需覆盖上游翻译，必须使用 OMS-owned localisation namespace + `.resx`，不能只改 `*Strings.cs` fallback。

验收：

- 手动重新打开向导后各页可稳定加载，不因复用 settings 组件而在 load 阶段崩溃。
- focused first-run tests + Release build 通过。

### A1：反馈组件合同冻结

**状态：进行中**

目标：为当前和后续 gameplay feedback 建立统一入口，而不是继续把 speed feedback 固定在 toast，同时保持 release gate 与现有 HUD provider 不被打断。

建议交付：

1. 新增 `BmsSkinComponents.GameplayFeedbackDisplay`，作为 BMS-owned feedback 宿主组件。
2. 新增 `IBmsGameplayFeedbackDisplay`，由 ruleset 向组件推送稳定的 state 对象，而不是让组件反查 `DrawableBmsRuleset` 内部字段。
3. 保持现有 `IBmsHudLayoutDisplay` 不变，新增可选的 versioned HUD layout 接口或 wrapper contract，用于额外接入 feedback display。
4. `BmsSkinTransformer` 只在新接口存在时把 feedback display 交给 HUD layout；旧接口保持现状不破坏。

> 当前已完成的合同骨架：`IBmsHudLayoutDisplayWithGameplayFeedback`、legacy HUD overlay wrapper 与 `DefaultBmsHudLayoutDisplay` 默认 fallback 已落地；`BmsGameplayFeedbackLayout` 现已把默认 gameplay feedback 摆位与 judgement 基线收口到 shared position contract；`BmsGameplayFeedbackState` aggregate snapshot 也已完成两刀接线，当前已包含 `TimingFeedbackVisualRange`。剩余工作是决定 richer judge display / history 类状态继续如何分层，而不是回退到组件直接反查 ruleset 多组 bindable。

验收：

- 不破坏现有 `HudLayout` / `GaugeBar` / `ComboCounter` fallback。
- 当前用户皮肤不实现新接口时仍能正常显示旧 HUD。
- 新 feedback display 在 `OmsSkin` 默认路径下可独立 fallback。

### B1：权威绿色数字常驻反馈

**状态：已完成，后续围绕 tri-mode operator surface 继续稳态化**

目标：为 `P1-C` 的常驻 GN 与后续 feedback family 提供稳定宿主边界；当前常驻 GN 已落地，本子线继续维护其宿主、fallback 与 settings / overlay 产品边界。

建议交付：

1. 冻结 `GameplayFeedbackDisplay` 所需的最小宿主接口与 fallback 语义。
2. 冻结 HUD layout 的向后兼容扩展方式，不让 `P1-C` 再次修改旧接口。

### B2：tri-mode settings 与 pre-start hold operator surface

**状态：首轮实现已完成**

目标：在不破坏 HUD / skin boundary 与项目依赖边界的前提下，把三模式设置、runtime feedback 与 pre-start 调速窗口收口成同一条产品合同。

当前已完成：

1. `BmsSettingsSubsection` 现提供 `Normal / Floating / Classic Hi-Speed` 下拉与当前模式 slider，settings 不再显示 `GN / ms`。
2. `BmsSoloPlayer` / `BmsPreStartHiSpeedOverlay` 已把 5 秒 delayed start、`UI_LaneCoverFocus` hold gate、奇偶键调速与 paused pre-start `Sudden / Hidden / Lift` 调整链接入正式 gameplay 流程。
3. `SoloSongSelect` 通过反射创建 `BmsSoloPlayer`，避免 `osu.Game` 对 `osu.Game.Rulesets.Bms` 新增编译期依赖。

后续检查点：

1. 补更多 dedicated integration / visual coverage，锁定 pre-start hold overlay 与 start sequencing。
2. 持续守住 tri-mode settings、HUD 与 overlay 的 BMS-owned fallback 合同。
3. 保留 toast 作为补充反馈层，但不再让任何新功能直接依赖 toast 作为唯一宿主。
4. 把数值 state 的具体字段集留给 [../P1-C/DEVELOPMENT_PLAN.md](../P1-C/DEVELOPMENT_PLAN.md) 继续细化。

验收：

- `P1-C` 可以在不破坏现有 HUD provider 的前提下安全接入 feedback display。
- HUD 默认实现与皮肤 fallback 均可稳定显示，不依赖 Debug overlay。

### B2：`Sudden / Hidden / Lift` 联动收口

**状态：未开始**

目标：让 lane cover focus、当前 target、geometry-effect 与 HUD feedback 表达统一，避免视觉上各说各话。

建议交付：

1. 当前 target 与 lane cover focus state 必须一一对应，HUD 不允许出现“焦点已切换但 display 仍停留旧 target”的状态。
2. `Lift` 继续保持 geometry control，不与 `Hidden` 混写；HUD 文案与状态表达必须延续这条边界。
3. 对“仅启用 1 个 target”“无 target 可切换”“当前 target 因 mod 未启用而失效”给出明确显示策略。

验收：

- `Sudden / Hidden / Lift` 三项在启用 / 禁用 / 切换时的 HUD 行为可预测。
- 焦点与 HUD 指示一致。

### C1：扩展到统一 gameplay feedback 家族

**状态：未开始**

目标：在 speed feedback 合同稳定后，把 `FAST/SLOW`、judge display、visual timing-offset、EX pacemaker 纳入同一反馈家族，而不是再开新的临时 overlay。

建议交付：

1. `FAST/SLOW` 与 judge display 优先进入同一 feedback container，而不是和 judgement piece 互相硬耦合。
2. visual timing-offset 与 EX pacemaker 也沿同一状态流接入，避免每个功能单独占用 HUD 注入点。
3. judgement 位置如果需要与 feedback 排布联动，应显式新增位置合同，不继续扩散硬编码偏移值。

> shared position contract 已落地；后续这一步的重点不再是“先抽常量”，而是决定如何在不破坏现有 skin/judgement 生命周期的前提下继续扩 judge display 的语义与排布。

验收：

- gameplay feedback 家族拥有稳定宿主，不再依赖临时 toast 链。
- 新功能的加入不需要继续修改旧版 HUD layout 接口签名。

### D1：作者文档与 release gate 收口

**状态：未开始**

目标：把这条专题从“实现中合同”变成“可维护的 authoring / release gate 文档”。

建议交付：

1. 在 [../../other/SKINNING.md](../../other/SKINNING.md) 中补齐 `GameplayFeedbackDisplay` 的 authoring 入口、fallback 粒度与状态合同。
2. 在 [../../mainline/OMS_COPILOT.md](../../mainline/OMS_COPILOT.md) 中把接口和命名边界收口成硬约束。
3. 在 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md) 中记录实现状态，在 [../../mainline/CHANGELOG.md](../../mainline/CHANGELOG.md) 中记录验证结果。

## 当前优先顺序

1. `A1` 反馈组件合同冻结
2. 与 [../P1-C/DEVELOPMENT_PLAN.md](../P1-C/DEVELOPMENT_PLAN.md) 对齐常驻绿色数字与速度反馈字段集
3. `B2` `Sudden / Hidden / Lift` 联动收口
4. `C1` 扩展到统一 gameplay feedback 家族
5. `D1` 作者文档与 release gate 收口