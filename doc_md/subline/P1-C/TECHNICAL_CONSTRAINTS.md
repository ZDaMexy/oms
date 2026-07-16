# P1-C 技术约束：判定语义与反馈边界

> 最后更新：2026-07-16
> 当前事实见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，历史见 [CHANGELOG.md](CHANGELOG.md)。

## 归线与产品边界

1. P1-C 维护判定 family、窗口/poor/release parity 与反馈语义，不得借题提前引入完整 FHS、dan、1P/2P flip、BSS/MSS。
2. HUD/skin 宿主和 slot/fallback 归 P1-A；真实谱 LN/CN/HCN 验校归 P1-E；人工结果汇总归 P1-G；Gimmick 视觉位置归 P1-L。
3. 常驻 `DefaultBmsSpeedFeedbackDisplay`、`GameplayFeedbackState`、FAST/SLOW、pacemaker、summary 与常驻 GN 已删除，不是当前能力。未来若重建必须另立专题，不得把历史接口当成兼容承诺。
4. 当前 GN/WN 只能描述 OMS `Normal/Floating/Classic Hi-Speed + Sudden/Hidden/Lift` 的现有 runtime surface，不得对外宣称完整 IIDX FHS。

## 判定 family 合同

1. `BmsJudgementSystemParityTest` 是 IIDX/LR2/beatoraja/OD 窗口、方向、scratch、long-note release 与 poor 语义的改动门；任何改数必须先让测试差异显式可审。
2. audit/source-backed 与 documented heuristic 必须区分：
   - IIDX 主窗口 `16.67/33.33/116.67/250`、LR2 四档与 beatoraja `JudgeProperty.SEVENKEYS` 缩放/非对称值属于已溯源基线。
   - beatoraja BAD base 早窗 `280`、晚窗 `220`；scratch 为 `290/230`，LN release 为 `280/220`。方向不得再次写反。
   - IIDX empty-poor `500/150` 与 IIDX CN release 沿用 note window 是 OMS documented heuristic，不得写成闭源官方精确值。
3. 跨 family 边界统一使用 `<= window + BmsJudgementSystem.BoundaryEpsilon`；新增 Evaluate 路径不得私设另一套压线规则。
4. scratch、long-note release、excessive poor 与 empty poor 必须保持 family-specific；不得用一套普通 note window 覆盖全部。
5. judge mode/rank 必须进入 runtime 与 score bucket；显示层不得反向决定判定 family。
6. HCN 允许 release 后 regrab；普通 CN 中途松开后不可接回。具体 long-note authority 见 [P1-E 约束](../P1-E/TECHNICAL_CONSTRAINTS.md)，P1-C 只消费结论。
7. 判定、计分和 replay 始终使用时间链；visual scroll、lane cover、skin/layout 或 P1-L 位置旁路不得改变结果。

## 当前反馈与 HUD 边界

1. 全局 `JudgementCounterDisplay` 承担当前判定计数；GN 只在现有调速 toast 与 pre-start overlay 出现。不得把已删除的常驻 card 描述为当前 fallback 或待接线组件。
2. 新反馈不得通过遍历 wrapped HUD 子节点、修改 `GaugeBar`/`ComboCounter` 或暗改 `IBmsHudLayoutDisplay` 三件套宿主植入。
3. 任何新常驻反馈都必须先在独立专题冻结产品价值、数据 authority、P1-A 宿主、fallback、验收和删除路径；不得复活旧 aggregate 规避设计审查。
4. `Sudden/Hidden/Lift` target/cycle/remember 行为与判定正交；`Lift` 是 geometry control，`Hidden` 是下遮挡，两者不得混写。
5. pre-start 视觉流速 preview 只能复用现有 visual/scroll authority；不得创建真实 `BmsHitObject`、使用 `DrawableBmsHitObject`、进入 `HitObjectContainer`，或触发 keysound、judgement、score、replay、autoplay side effect。

## Results 与验证边界

1. results 重建必须消费 Ruleset contract 传入的 already-modded playable beatmap，不得重复应用 beatmap mods；gauge history 与 clear lamp 必须由 owning processor 计算，panel/UI 不得重建 timeline 或灯级。
2. `PERFECT`/`FULL COMBO` 持久化必须先过 clear condition；HCN body tick 可独立影响 gauge，禁止只看聚合 judgement counts 推导灯级。
3. 判定 family、poor/release、反馈术语或当前 HUD surface 改动，必须同步本目录四件套；影响全局 gate 时再向 mainline 回写摘要。
