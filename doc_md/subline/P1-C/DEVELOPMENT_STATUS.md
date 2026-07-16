# P1-C 当前状态：判定语义与反馈闭环

> 最后更新：2026-07-16（文档健康治理；功能状态未改变）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，HUD/skin 宿主边界归 [P1-A](../P1-A/DEVELOPMENT_STATUS.md)。

## 当前阶段

IIDX/LR2/beatoraja/OD 判定家族与主要边界 parity 已落地并由契约测试守门。常驻速度反馈卡及其 FAST/SLOW、pacemaker、summary、常驻 GN 已按产品决定整体删除；当前工作是保持判定合同稳定，并只补仍有真实用户价值的展示/人工证明。

## 当前有效判定合同

- judge mode 与 judge-rank 进入 runtime 和 score bucket。
- IIDX、LR2 四档与 beatoraja 缩放/早晚非对称由 `BmsJudgementSystemParityTest` 守门。
- 跨家族边界按当前 inclusive/epsilon 合同统一；beatoraja BAD 早/晚方向错误已修复。
- scratch、long-note release、excessive/empty poor 使用家族特定语义，不能用一套窗口覆盖全部。
- HCN 允许 release 后 regrab；普通 CN 不具备该语义。LN body 的 Idle/Holding/Broken 状态与此保持一致。
- 判定、计分和 replay 继续依赖时间链；P1-L 滚动旁路不得改变这些结果。

## 当前反馈产品面

- 全局 `JudgementCounterDisplay` 承担判定计数，COMBO BREAK 已纳入。
- GN 仅在调速 toast 与 pre-start overlay 显示，不常驻 HUD。
- `Sudden/Hidden/Lift` 的 target/cycle/remember-gameplay-changes 基线保留。
- `UI_PreStartHold` 负责前 5 秒阻止开始和全程调速修饰；视觉流速 preview 不得接入判定链。
- 被删除的 `GameplayFeedbackState`、常驻 feedback card、FAST/SLOW/pacemaker 管线不是当前能力；如重建必须另立专题。

## 当前验证

- 全局最新产品验证统一见 [mainline STATUS 的“最近一次验证”](../../mainline/DEVELOPMENT_STATUS.md#最近一次验证)；2026-07-16 仅治理文档，未运行产品测试或 Release。
- 本线最后一次 parity/BMS 验证、历史细分数字和窗口溯源记录只保留在 [CHANGELOG.md](CHANGELOG.md) 与 [判定记忆](../../../.Codex/memory/reference_bms_judgement_parity.md)，不冒充当前全局 gate。

## 当前风险

- IIDX 闭源细节只能作为 documented heuristic，不能伪装成完全精确复刻。
- 改窗口时若只看一个家族，会破坏 scratch/release/empty-poor 的组合真值表。
- HUD 反馈需求不得偷塞进 gauge/combo 或破坏 `IBmsHudLayoutDisplay` 三件套宿主合同。
- 视觉滚动、lane cover 和判定结果必须保持正交。

## 下一检查点

1. 任何窗口/poor/release 改动先扩 parity test，再改实现。
2. 把剩余真实谱判定体验与 LN/CN/HCN 人工结果交给 P1-E/P1-G。
3. 若用户重新需要 FAST/SLOW 或 pacemaker，先重新定义产品价值、宿主和最小状态合同，不复活已删 aggregate。
