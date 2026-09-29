# P1-C 当前状态：判定语义与反馈闭环

> 最后核对：2026-09-29（反馈计划/合同与留存证据复核；产品验证日期不变）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，HUD/skin 宿主边界归 [P1-A](../P1-A/DEVELOPMENT_STATUS.md)。

## 当前阶段

2026-09-22 TOTAL 修正：已接入合法作者值优先、Beatoraja/LR2 各自缺省、辅助前后物量分离与 v7 新局身份；v7之前（含v6）及无版本历史结果使用旧缺省，已存终值/灯保留。精确来源、失败身份与验证边界统一见[TOTAL报告](../../other/BMS_TOTAL_RULES_AUDIT_20260922.md)。现有 Gauge Mod/默认规则、GAS 与 HCN 持续速率保持。

IIDX/LR2/beatoraja/OD 判定家族与主要边界 parity 已落地并由契约测试守门。常驻速度反馈卡及其 FAST/SLOW、pacemaker、summary、常驻 GN 已按产品决定整体删除；当前工作是保持判定合同稳定，并只补仍有真实用户价值的展示/人工证明。

## 当前有效判定合同

- judge mode 与 judge-rank 进入 runtime 和 score bucket。
- EX-SCORE 由继承 core `ScoreProcessor` 的 `BmsScoreProcessor` 实现；gauge 另有 Legacy/Beatoraja/LR2/IIDX family，与 gauge type、judge family 分离，不能把默认 Legacy 的数表当全模式通式。
- IIDX、LR2 四档与 beatoraja 缩放/早晚非对称由 `BmsJudgementSystemParityTest` 守门。
- 跨家族边界按当前 inclusive/epsilon 合同统一；beatoraja BAD 早/晚方向错误已修复。
- scratch、long-note release、excessive/empty poor 使用家族特定语义，不能用一套窗口覆盖全部。
- HCN 允许 release 后 regrab；普通 CN 不具备该语义。LN body 的 Idle/Holding/Broken 状态与此保持一致。
- Empty Poor 以 `HitResult.Ok` 单独记录，影响 gauge 与 FC/PERFECT 资格，不增加 EX-SCORE、不影响 accuracy、不断当前 combo；BAD/Miss 才产生断连语义。
- 判定、计分和 replay 继续依赖时间链；P1-L 滚动旁路不得改变这些结果。

## 当前反馈产品面

- 全局 `JudgementCounterDisplay` 承担判定计数，COMBO BREAK 已纳入。
- 静线还通过普通皮肤 scene 绑定通用 `GameplaySkinJudgementStatistics`，在 BGA 下方显示实时 PG/GR/GD/BD/PR/EP/CB。计数来自 score statistics，COMBO BREAK 不要求额外伪造 judgement event；这是只读展示，不恢复已删除的反馈卡或改变判定/计分合同，布局归 [P1-A](../P1-A/DEVELOPMENT_STATUS.md)。
- GN 仅在调速 toast 与 pre-start overlay 显示，不常驻 HUD。
- `Sudden/Hidden/Lift` 的 target/cycle/remember-gameplay-changes 基线保留。
- `UI_PreStartHold` 负责前 5 秒阻止开始和全程调速修饰；视觉流速 preview 不得接入判定链。
- 被删除的 `GameplayFeedbackState`、常驻 feedback card、FAST/SLOW/pacemaker 管线不是当前能力；如重建必须另立专题。

## 最近一次验证

2026-09-22 TOTAL 专项 Debug 250/250；BMS full 2300通过、29失败、16跳过；29项失败在修改前45d8613逐项复现，属于旧皮肤工作区/编辑器预期，后续维护归P1-A。Release成功（0错误、2个既有测试警告）。来源、命令、失败身份及未做原版播放器实机对照的边界见[TOTAL报告](../../other/BMS_TOTAL_RULES_AUDIT_20260922.md)。

2026-09-23只回读三个留存TRX核对上述测试数字和失败身份，未重跑产品测试或Release；临时构建日志已不可回读，Release结论保留为9月22日执行记录。此前判定窗口溯源与专项历史见[CHANGELOG](CHANGELOG.md)。

## 当前风险

- IIDX 闭源细节只能作为 documented heuristic，不能伪装成完全精确复刻。
- 改窗口时若只看一个家族，会破坏 scratch/release/empty-poor 的组合真值表。
- HUD 反馈需求不得偷塞进 gauge/combo 或破坏 `IBmsHudLayoutDisplay` 三件套宿主合同。
- 视觉滚动、lane cover 和判定结果必须保持正交。

## 下一检查点

1. 任何窗口/poor/release 改动先扩 parity test，再改实现。
2. 把剩余真实谱判定体验与 LN/CN/HCN 人工结果交给 P1-E/P1-G。
3. 若用户重新需要 FAST/SLOW 或 pacemaker，先重新定义产品价值、宿主和最小状态合同，不复活已删 aggregate。

## 文档治理验证

2026-09-29：对照当前统计绑定、TOTAL 成绩版本和9月22日三个留存 TRX，修正 PLAN 仍漏报静线实时统计的旧表述，合同与 memory 同步其只读边界及历史版本默认值；29项旧失败的名称、错误信息和路径根标准化后堆栈仍逐项一致。本次未改产品、未重跑测试或实机，Release 仍只有历史执行记录。此前审查见 [CHANGELOG](CHANGELOG.md)。
