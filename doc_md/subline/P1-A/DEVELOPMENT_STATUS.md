# P1-A 开发进度：产品面、release gate 与皮肤边界

> 最后更新：2026-04-20
> 主线全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。本文件只记录 `P1-A` 的真实进展；`P1-C` 的反馈闭环进展见 [../P1-C/DEVELOPMENT_STATUS.md](../P1-C/DEVELOPMENT_STATUS.md)。

## 当前阶段

- **阶段定位**：子线建档完成，HUD 宿主与边界冻结已基本稳定，当前进入“tri-mode operator surface 挂接后的稳态化”阶段。
- **代码状态**：向后兼容的 HUD 宿主扩展已落地：`IBmsHudLayoutDisplayWithGameplayFeedback`、legacy HUD overlay wrapper 与 `DefaultBmsHudLayoutDisplay` 默认摆位已接通；`BmsGameplayFeedbackLayout` 已把默认 gameplay feedback 摆位与 judgement 基线收口到同一条显式位置合同；当前 `TimingFeedbackVisualRange`、compact judgement counts 与 live EX progress 已并入 `BmsGameplayFeedbackState` aggregate snapshot，`DefaultBmsSpeedFeedbackDisplay` 现在已收口为消费单个 BMS-owned state contract 加一条 recent-history 列表，而不是继续直接拼接多个 ruleset scalar bindable；同一张 feedback card 也开始基于既有 judgement counts 在 display 侧派生带紧凑原因标签的 live `PERFECT / FC / FC LOST` 状态线，并在 EX 进度线上显示原始 EX 分子/分母。除此之外，当前 BMS 速度产品面已扩到 tri-mode：settings 现提供 `Normal / Floating / Classic Hi-Speed` 下拉和当前模式 slider，`BmsSoloPlayer` / `BmsPreStartHiSpeedOverlay` 已把 5 秒 pre-start hold 调速窗口接到正式 gameplay 流程，且 paused pre-start 状态下仍可继续使用 `Sudden / Hidden / Lift` 调整链；对应 headless integration coverage 现已补到“提前松开仍等待 delayed-start”与“hold 期间 persistent target cycle 不破坏临时 bottom override”这两条 start-sequence 分支。
- **文档状态**：`P1-A` 的计划、状态、变动日志、技术约束已与当前宿主合同实现同步，并已挂接到主线文档。

## 已确认事实

- BMS 皮肤边界已足够封闭，可继续向 BMS-owned feedback component 扩展。
- 当前 `GN` / `WN` 来自 `BmsScrollSpeedMetrics`，其输入现已覆盖 `Normal / Floating / Classic Hi-Speed`、`ScrollLengthRatio`、`Sudden`、`Hidden`、`Lift`。
- 当前 tri-mode Hi-Speed surface 已完成首轮产品接线：`Normal` 走默认 settings surface，`Floating` 提供 initial-BPM anchored runtime surface，`Classic` 继续锁定 `HS 10 + WN 350 => GN 300`；这仍不等价于完整 FHS。
- settings 页现只显示 mode + value，不再把 `GN / ms` 写入用户可见 contract。
- `UI_LaneCoverFocus` 现已复用为 pre-start hold gate，但 HUD / skin boundary 与 legacy fallback 合同保持未破坏。
- 当前 `IBmsHudLayoutDisplay` 原签名仍被保留；额外 gameplay feedback 通过 `IBmsHudLayoutDisplayWithGameplayFeedback` 与 legacy overlay wrapper 向后兼容接入。
- `BmsSkinTransformer` 已在 `MainHUDComponents` 路径统一组装 gauge / combo / speed feedback；不实现新接口的旧 HUD layout 仍能正常工作。
- `BmsGameplayFeedbackLayout` 已收口默认 gameplay feedback 摆位与 judgement 基线；后续若继续联动 judge display / feedback，必须扩这条合同，而不是再复制位置常量。
- `DrawableBmsRuleset` 现已额外暴露 `GameplayFeedbackState` aggregate bindable；当前 display scalar state 已收口到单个 snapshot，recent timing history 仍保持独立状态流，而 `TimingFeedbackVisualRange`、compact judgement counts 与 live EX progress 已并入 aggregate snapshot，避免继续保留无必要的额外 scalar 直连。
- `DefaultBmsSpeedFeedbackDisplay` 现已开始直接利用既有 `BmsJudgementCounts` 与其派生 helper 显示带紧凑原因标签的 live `PERFECT / FC / FC LOST` 状态线，证明一部分 display-only 训练语义不必继续扩大 `GameplayFeedbackState`。
- 当前 IIDX 参考文档仍明确要求：不要把现有 OMS speed feedback 对外包装成完整 `FHS`。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
- 子线归线到 `P1-A` | 已完成 | 主线文档已挂接 |
- 皮肤边界与 HUD 宿主审计 | 已完成 | 已明确当前模型与风险点 |
- 子线级计划 / 状态 / 约束文档 | 已完成 | 文档位于当前目录 |
| `GameplayFeedbackDisplay` 合同设计 | 进行中 | HUD 宿主兼容扩展、default fallback、shared position contract 与 aggregate scalar state contract 四刀已落地；基于既有 snapshot 的派生 status line 也已验证可继续收口 richer judge display 而无需新字段；剩余 richer judge display 语义继续收口 |
| 常驻 GN HUD | 进行中 | 默认 feedback container 已由 `P1-C` 接通；本子线继续维护宿主与边界合同 |
| `Sudden / Hidden / Lift` HUD 联动 | 进行中 | 宿主与默认摆位已稳定，具体状态语义由 `P1-C` 继续推进 |
| `FAST/SLOW` / judge display / pacemaker 统一承载 | 进行中 | 统一 feedback container 已存在，shared judgement/feedback position contract 已落地 |

## 当前风险

- **接口破坏风险**：如果直接改 `IBmsHudLayoutDisplay.SetComponents(...)` 签名，会立刻打断现有自定义 HUD provider。
- **术语冒进风险**：如果把当前常驻 GN 直接写成完整 `FHS`，会与 IIDX 参考约束冲突，也会误导用户对当前模型的预期。
- **边界污染风险**：如果为了赶功能把 speed feedback 偷塞进 `GaugeBar`、`ComboCounter` 或 wrapped HUD 子节点，后续 `FAST/SLOW` / pacemaker 将继续复制同类问题。
- **布局扩散风险**：如果不先冻结 judgement / feedback 的位置合同，后续容易继续用新的硬编码偏移叠层。

## 下一检查点

1. 在现有 `GameplayFeedbackState` 已并入 compact judgement counts 与 live EX progress 的基础上，继续评估后续 richer judge display state 是否进入同一 contract，还是保持与 recent history 分层；当前 live `PERFECT / FC` 资格线与 EX 原始分子/分母文案都已证明一部分 display-only 语义可直接从既有 snapshot 派生。
2. 继续为 tri-mode settings 与 pre-start hold overlay 补 visual / input-path integration coverage；当前已锁住提前松开 delayed-start 与 persistent target cycle 分支，剩余重点转向更完整 host boundary、fallback 与真实输入事件路径。
3. 维持 `OmsSkin` 默认路径、legacy HUD wrapper 与 fallback 语义稳定，并把 remaining full Floating parity 缺口明确留在后续路线，不在 `P1-A` 里误写成已完成。

## 验证记录

- 2026-04-20：完成 tri-mode Hi-Speed settings / runtime surface 与 pre-start hold operator surface 首轮接线；`BmsHiSpeedMode`、mode dropdown + current-mode slider、`BmsSoloPlayer` delayed start、`BmsPreStartHiSpeedOverlay` 与 paused pre-start 调整链均已落地，且 `SoloSongSelect` 通过反射保持了 `osu.Game` 与 `osu.Game.Rulesets.Bms` 的项目边界。定向 `BmsScrollSpeedMetricsTest`、`BmsRulesetConfigurationTest`、`BmsGameplayFeedbackStateTest`、`TestSceneBmsSpeedFeedbackDisplay`、`BmsDrawableRulesetTest` 合计 97/97 通过；`Build osu! (Release)` 通过。
- 2026-04-20：扩展 `TestSceneBmsSoloPlayerPreStart`，新增“提前松开 hold 仍等待 delayed-start”“hold 期间 persistent target cycle 不破坏临时 bottom override”两条 headless integration 回归，并补上奇偶列调速双向断言；定向 `TestSceneBmsSoloPlayerPreStart` **5/5** 通过。
- 2026-04-20：新增 `BmsGameplayFeedbackLayout`，把默认 gameplay feedback 摆位与 judgement 基线收口为 shared position contract；`DrawableBmsJudgement` 与 `DefaultBmsHudLayoutDisplay` 已统一通过该合同设置默认位置；定向 `BmsGameplayFeedbackLayoutTest`、`TestSceneBmsJudgementDisplayPosition`、`BmsSkinTransformerTest`、`TestSceneBmsSpeedFeedbackDisplay` 合计 117/117 通过；`Build osu! (Debug)` 通过。
- 2026-04-20：新增 `BmsGameplayFeedbackState` aggregate snapshot，`DrawableBmsRuleset` 开始发布单个 gameplay feedback scalar contract，`DefaultBmsSpeedFeedbackDisplay` 已改为优先消费该 snapshot；定向 `BmsRulesetModTest`、`TestSceneBmsSpeedFeedbackDisplay`、`BmsSkinTransformerTest`、`BmsGameplayFeedbackLayoutTest`、`TestSceneBmsJudgementDisplayPosition` 合计 154/154 通过；`Build osu! (Debug)` 通过。
- 2026-04-20：把 `TimingFeedbackVisualRange` 并入 `BmsGameplayFeedbackState`，`DefaultBmsSpeedFeedbackDisplay` 现已收口为消费 aggregate snapshot + recent history list 的 contract 形状；新增 `BmsGameplayFeedbackStateTest` 回归并扩展 `BmsRulesetModTest`、`TestSceneBmsSpeedFeedbackDisplay`、`BmsSkinTransformerTest`，定向合计 153/153 通过；`Build osu! (Debug)` 通过。
- 2026-04-20：把 compact judgement counts 也并入 `BmsGameplayFeedbackState`，`DefaultBmsSpeedFeedbackDisplay` 继续沿同一 state contract 呈现两行 compact live judge summary；新增 `BmsJudgementCountsTest`，并扩展 `BmsGameplayFeedbackStateTest`、`BmsRulesetModTest`、`TestSceneBmsSpeedFeedbackDisplay`，定向合计 59/59 通过；`Build osu! (Debug)` 通过。
- 2026-04-20：把 live EX progress 也并入 `BmsGameplayFeedbackState`，`DefaultBmsSpeedFeedbackDisplay` 继续沿同一 state contract 呈现 live `DJ LEVEL + EX %`；新增 `BmsExScoreProgressInfoTest`，并扩展 `BmsGameplayFeedbackStateTest`、`BmsRulesetModTest`、`TestSceneBmsSpeedFeedbackDisplay`，定向合计 66/66 通过；`Build osu! (Debug)` 通过。
- 2026-04-20：沿现有 aggregate snapshot 跟进一条 display-only 训练状态线；`BmsJudgementCounts` 新增 `CanStillPerfect / CanStillFullCombo`，随后又补入 `LeastSevereFullComboBreakResult / LeastSevereFullComboBreakCount`，`DefaultBmsSpeedFeedbackDisplay` 现可在不扩 `BmsGameplayFeedbackState` 的前提下显示带紧凑原因标签的 live `PERFECT / FC / FC LOST` 状态线；扩展 `BmsJudgementCountsTest`、`TestSceneBmsSpeedFeedbackDisplay` 后，定向合计 69/69 通过；`Build osu! (Debug)` 通过。
- 2026-04-20：沿现有 live EX progress snapshot 跟进一条 display-only 文案增强；`DefaultBmsSpeedFeedbackDisplay` 的 EX 进度线现显示 `EX current/max percentage`，直接复用 `BmsExScoreProgressInfo` 里的 `CurrentExScore / MaximumExScore / ExRatio`，无需继续扩大 `BmsGameplayFeedbackState`；沿同一聚焦回归基线定向 69/69 通过；`Build osu! (Debug)` 通过。
- 2026-04-20：把当前 Classic Hi-Speed surface 进一步收口为 strict profile；`DrawableBmsRuleset` 现改用官方 sample 对齐的 `(100000 / 13) / HS` base time，`BmsPlayfield` 不再消费用户可见 geometry override，`BmsSettingsSubsection` 也已移除相关 layout sliders。新增 cab sample 与 strict-profile layout 回归后，定向 `BmsScrollSpeedMetricsTest`、`BmsRulesetConfigurationTest`、`TestSceneBmsSpeedFeedbackDisplay`、`TestSceneBmsPlayfieldLayoutConfig`、`BmsLaneLayoutTest`、`BmsDrawableRulesetTest` 合计 91/91 通过；`Build osu! (Release)` 通过。