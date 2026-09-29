# P1-C 当前状态：判定语义与反馈闭环

> 最后核对：2026-09-29（自动调整偏移互斥 style；真实设备体验待验收）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，HUD/skin 宿主边界归 [P1-A](../P1-A/DEVELOPMENT_STATUS.md)。

## 当前阶段

「自动调整偏移」已统一为 `关闭 / osu!lazer style / beatoraja style` 单选，旧开启设置迁移为 lazer。玩家可在同一音频设置区查看/修改 BMS 显示偏移；beatoraja 在真实 BMS 演奏中按有效时机调整，关闭后保留固定值，mania 不执行该算法。LN 合并采样、CN/HCN 真实松键、辅助过滤、回放只读轨迹已接入；回放不覆盖个人设置。具体合同见[自动调整偏移](TECHNICAL_CONSTRAINTS.md#自动调整偏移合同2026-09-29)。

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

2026-09-29 自动调整偏移：BMS focused 58通过，core offset focused 35通过、mania replay 331通过；Release 成功（0错误、2个既有测试警告），文档及 diff 检查通过；BMS full 2354通过、29失败、16跳过。29项失败与9月22日留存 full 的名称、错误消息和路径根标准化后的堆栈逐项相同，没有新增失败，旧皮肤工作区/编辑器预期维护仍归 P1-A。命令、工件与其他验证记录见 [CHANGELOG](CHANGELOG.md)。真实设备收敛、听感和外部播放器逐帧对照未签收。

TOTAL 与判定窗口历史验证见 [CHANGELOG](CHANGELOG.md) 和 [TOTAL报告](../../other/BMS_TOTAL_RULES_AUDIT_20260922.md)。

## 当前风险

- 自动调整偏移尚未签收键盘/真实控制器与普通、皿、LN/CN/HCN、STOP 实谱的收敛和听感；软件验证不能证明最佳个人偏移。已有音频/谱面偏移不因切换 style 自动归零；两种 style 的自动行为互斥，固定值可继续叠加。
- IIDX 闭源细节只能作为 documented heuristic，不能伪装成完全精确复刻。
- 改窗口时若只看一个家族，会破坏 scratch/release/empty-poor 的组合真值表。
- HUD 反馈需求不得偷塞进 gauge/combo 或破坏 `IBmsHudLayoutDisplay` 三件套宿主合同。
- 视觉滚动、lane cover 和判定结果必须保持正交。

## 下一检查点

1. 按 PLAN 的自动调整偏移人工门验证稳定谱/和弦/皿/长条/变速、关闭固定、重启保存与回放；发现样本或显示问题只在该专题修复，不扩训练卡。
2. 任何窗口/poor/release 改动先扩 parity test，再改实现。
3. 把剩余真实谱判定体验与 LN/CN/HCN 人工结果交给 P1-E/P1-G。
4. 若用户重新需要 FAST/SLOW 或 pacemaker，先重新定义产品价值、宿主和最小状态合同，不复活已删 aggregate。

## 文档治理验证

2026-09-29 自动调整偏移交付后复核：代码与当前互斥、迁移、采样及回放合同一致；回读四个最终 TRX 和 Release 日志，确认上述产品验证记录及29项旧失败身份。已完成七步规划移入 CHANGELOG，PLAN 只留真实设备/实谱验收；记忆只保留诊断与权威回链。未改产品、未重跑测试或 Release、未新增人工签收；原统计/TOTAL审查见 [CHANGELOG](CHANGELOG.md)。
