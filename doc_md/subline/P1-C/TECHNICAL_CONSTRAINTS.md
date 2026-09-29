# P1-C 技术约束：判定语义与反馈边界

> 最后核对：2026-09-29（自动调整偏移的迁移、采样与回放合同核对；判定/TOTAL 合同不变）
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
8. Empty Poor 使用 `HitResult.Ok` 与真实 combo break 分开统计，不计 EX-SCORE/accuracy、不断当前 combo，但影响 gauge 与 FC/PERFECT 资格；`BmsScoreProcessorTest.TestEmptyPoorDoesNotBreakComboWithoutAffectingExScoreOrAccuracy` 是该边界的回归入口。不能从旧表或 `BmsPoorJudgement` 类型名推断它等同 Miss。

## 当前反馈与 HUD 边界

1. 全局 `JudgementCounterDisplay` 与皮肤 scene 的只读 `GameplaySkinJudgementStatistics` 共同消费 score statistics；静线已显示 PG/GR/GD/BD/PR/EP/CB，COMBO BREAK 不要求额外伪造 judgement event。GN 只在现有调速 toast 与 pre-start overlay 出现；不得把已删除的常驻 card 描述为当前 fallback 或待接线组件。
2. 新反馈不得通过遍历 wrapped HUD 子节点、修改 `GaugeBar`/`ComboCounter` 或暗改 `IBmsHudLayoutDisplay` 三件套宿主植入。
3. 任何新常驻反馈都必须先在独立专题冻结产品价值、数据 authority、P1-A 宿主、fallback、验收和删除路径；不得复活旧 aggregate 规避设计审查。
4. `Sudden/Hidden/Lift` target/cycle/remember 行为与判定正交；`Lift` 是 geometry control，`Hidden` 是下遮挡，两者不得混写。
5. pre-start 视觉流速 preview 只能复用现有 visual/scroll authority；不得创建真实 `BmsHitObject`、使用 `DrawableBmsHitObject`、进入 `HitObjectContainer`，或触发 keysound、judgement、score、replay、autoplay side effect。

## 自动调整偏移合同（2026-09-29）

- 产品入口为音频→偏移的「自动调整偏移」，单选 `关闭 / osu!lazer style / beatoraja style`；新枚举是唯一运行时 authority。旧布尔仅在首次缺失新键时迁移，已有 Off 不得被旧 true 复活。lazer 仍按上一局中位误差和 UR 自动校准谱面偏移；全局推荐偏移仍手动应用。
- beatoraja style 仅 BMS；mania 不自动使用该算法。固定 BMS 显示偏移与全局音频/谱面偏移独立，切换/关闭保留数值，±500ms、1ms 步长。关闭自动不等于归零固定值，用户可在同区恢复 0。
- 普通键/皿 PG/GR/GD、绝对误差 ≤150ms 按 `sign(error)*floor((abs(error)+15)/30)` ms 推动，OMS error 晚正早负；单次变更限幅。LN 在完成时只采一个合并样本；CN/HCN 头与真实释放分别采样。OMS 已有自动到尾行为不改，自动到尾不采帧延迟，不能据此声称长条 runtime 与原版完全相同。AutoPlay/辅助音、持续 tick、空 POOR、parent Ignore 均不学习。
- 显示修正只作用于 BMS 显示时间，不能改变音符时间、判定、成绩、gauge、输入、键音/BGM/BGA。BMS 侧算法包装同步小节线/地雷，嵌套长条位置与长度不重复偏移。真实演奏中，beatoraja 自动调整未启用且固定偏移为 0ms 时保留原算法实例；自动启用时保持同一包装跨零变化，避免逐音重算全谱。额外预加载对象不能扩大空 POOR 候选或提前屏蔽辅助列空按声音。
- 真实演奏持续更新用户配置，失败/退出/重试保留。回放只读成绩中的初始值和变更时间线，seek 按时间查询；记录含变化轨迹时保持显示包装跨零，不受个人 style 选择影响；旧记录缺失字段按 0ms，不学习、不写个人配置。轨迹随 BMS ruleset data 保存，TOTAL version=7 语义不变；停止录制、score clone 前一次序列化，不能逐判定重序列化整个历史。导入轨迹验证有限数、偏移范围和严格递增时间。
- 来源：[JudgeManager](https://github.com/exch-bms2/beatoraja/blob/9aea9471f4490732cbbd6b71ca639ee823d20635/src/bms/player/beatoraja/play/JudgeManager.java)、[LaneRenderer](https://github.com/exch-bms2/beatoraja/blob/9aea9471f4490732cbbd6b71ca639ee823d20635/src/bms/player/beatoraja/play/LaneRenderer.java)。独立实现行为，不复制播放器 runtime。

## Results 与验证边界

TOTAL 合同（2026-09-22）：合法作者值优先；缺省 Beatoraja 使用 `max(260,760.5*N/(N+650))`，LR2 按 LR2oraja 兼容依据使用 `160+(N+clamp(N-400,0,200))*0.16`，Legacy 维持200，IIDX不消费TOTAL。缺省物量N包含辅助音符且遵循运行时LN模式；回血/Hard修正物量排除辅助，HCN body不进分母。v7新游玩明确初始化身份；v7之前（含v6）及无版本历史使用旧缺省200与旧低物量修正舍入，已存终值/灯不得覆盖。自动播放须由新生成回放身份区分，不能仅凭无成绩数据认定新局。来源与边界见[取证报告](../../other/BMS_TOTAL_RULES_AUDIT_20260922.md)。

1. results 重建必须消费 Ruleset contract 传入的 already-modded playable beatmap，不得重复应用 beatmap mods；gauge history 与 clear lamp 必须由 owning processor 计算，panel/UI 不得重建 timeline 或灯级。
2. `PERFECT`/`FULL COMBO` 持久化必须先过 clear condition；HCN body tick 可独立影响 gauge，禁止只看聚合 judgement counts 推导灯级。
3. 判定 family、poor/release、反馈术语或当前 HUD surface 改动，同步本线实际受影响的状态、计划、约束和验证记录；影响全局优先级、release gate 或硬约束时再向 mainline 回写摘要与链接，规则以 [AGENTS](../../../AGENTS.md#权威与文档) 为准。
4. `BmsGaugeRulesFamily` 与 gauge type、judge family 各有自己的选择和消费链；默认 Legacy 的 TOTAL 倍数/阈值不代表 Beatoraja/LR2/IIDX。GAS 只运行当前 active gauge，降级初始化为新 gauge 的起始值，历史按实际激活段绘制；最终 lamp 由最终 active gauge 与 clear 条件决定，不能在 results 再并行模拟各 gauge 取最高灯。
