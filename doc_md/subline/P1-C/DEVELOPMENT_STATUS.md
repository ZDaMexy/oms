# P1-C 当前状态：判定语义与反馈闭环

> 最后核对：2026-11-05（反馈入口与版本合同勘误；产品验证日期及人工门保持）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，HUD/skin 宿主边界归 [P1-A](../P1-A/DEVELOPMENT_STATUS.md)。

## 当前阶段

跨轨结果已按全局实际应用顺序逆序撤销，修复BMS/mania血量快照恢复错误；BMS全自动结算按CountsForScore保留回血分母，辅助仍排除。失败回放增加可选passed，旧F恢复失败且已存F重导不受Realm ignored bool默认值影响；普通mania同批松键和note lock归[P1-E](../P1-E/DEVELOPMENT_STATUS.md)。未更换窗口数表、TOTAL身份或新局OMSIR合同，范围见[本轮深审](../../other/GAMEPLAY_PERFORMANCE_JUDGEMENT_20261019.md)。

「自动调整偏移」已统一为 `关闭 / osu!lazer style / beatoraja style` 单选，旧开启设置迁移为 lazer。玩家可在同一音频设置区查看/修改 BMS 显示偏移；beatoraja 在真实 BMS 演奏中按有效时机调整，关闭后保留固定值，mania 不执行该算法。LN 合并采样、CN/HCN 真实松键、辅助过滤、回放只读轨迹已接入；回放不覆盖个人设置。具体合同见[自动调整偏移](TECHNICAL_CONSTRAINTS.md#自动调整偏移合同2026-09-29)。

真实自动开关与数值变化已分开记录，Replay按记录时刻恢复；旧无开关记录及无来源GeneratedAutoplay保持unknown，皮肤不能用numeric变化数伪装ON/OFF。算法、判定/计分及个人配置合同不变，软件基线取下方验证，新原生与设备门保持；范围取[日志](CHANGELOG.md#2026-10-28自动偏移开关独立记录)。

2026-09-22 TOTAL 修正：已接入合法作者值优先、Beatoraja/LR2 各自缺省、辅助前后物量分离与 v7 新局身份；v7之前（含v6）及无版本历史结果使用旧缺省，已存终值/灯保留。精确来源、失败身份与验证边界统一见[TOTAL报告](../../other/BMS_TOTAL_RULES_AUDIT_20260922.md)。现有 Gauge Mod/默认规则、GAS 与 HCN 持续速率保持。

IIDX/LR2/beatoraja/OD 判定家族与主要边界 parity 已落地并由契约测试守门。旧常驻速度反馈卡及其聚合管线已按产品决定删除；当前皮肤的 FAST/SLOW、GN 与本地 PB/NEXT 沿 P1-A 的原生只读状态提供。卡片删除不表示现有作者能力不存在，也不构成恢复旧卡的待办。

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
- GN、Hi-Speed 与挡板读数由原生只读状态和皮肤承接，旧调速 toast 已删除；字段与实际倍率可用性只取[P1-A读谱合同](../P1-A/TECHNICAL_CONSTRAINTS.md#既有产品面硬约束未被-skin-v1-取代)。
- `Sudden/Hidden/Lift` 的 target/cycle/remember-gameplay-changes 基线保留。
- `UI_PreStartHold` 负责前 5 秒阻止开始和全程调速修饰；视觉流速 preview 不得接入判定链。
- 被删除的 `GameplayFeedbackState` 与旧常驻 feedback card 不恢复；现有单次/累计 FAST/SLOW 及本地 PB/NEXT 的资格和来源见[作者参考](../../../skin-authoring/docs/REFERENCE.md#动画状态机绑定变体模板)，不从历史删除决定否定这些独立只读能力。

## 最近一次验证

2026-10-28：正式f427包/r23运行时三组演奏、Classic往返、新导出Replay与旧r17导入自动断言通过；13字段/calculation0、Normal8和旧OSR字节保全。真实Replay只读/拒绝live Toggle，旧诊断不改签。r23 BMS full通过，未重跑Core/mania、无新交付，范围取[P1-A日志](../P1-A/CHANGELOG.md#2026-10-28omniauto状态与最终候选续验)，软件输入不代签实体设备/听感。

2026-10-19：真实手动回放跨轨回退、LN/CN/HCN全自动实况/结算与持久化数据、失败回放新旧元数据控制均有修前失败和修后证明；另补固定混合谱验证BMS四家族与mania六类判定到完成成绩的统计、准确率、连击与分数。完整回归、旧失败身份及当前构建取[集中记录](../../other/GAMEPLAY_PERFORMANCE_JUDGEMENT_20261019.md#验证证据)，不将本地Rank展示误写成Passed持久化；真实设备收敛、听感和外部播放器逐帧对照仍未签收。

自动调整偏移、TOTAL与判定窗口的9月原验证见 [CHANGELOG](CHANGELOG.md) 和 [TOTAL报告](../../other/BMS_TOTAL_RULES_AUDIT_20260922.md)。

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
4. 超出现有只读 FAST/SLOW、本地 PB/NEXT 的新训练需求，先定义产品价值、宿主和最小状态合同，不复活已删 aggregate。

## 文档治理验证

2026-11-05对照当前读谱控件、计算器和皮肤状态生产链，移除仍列为当前能力的调速toast，区分旧反馈卡删除与已交付只读FAST/SLOW/PB/NEXT，并回链版本0/1合同。仅文档勘误，产品测试、实机和原版证据均保留原日期；具体来源与治理验证见[本线记录](CHANGELOG.md#2026-11-05反馈与读谱合同勘误)。
