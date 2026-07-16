# P1-C 当前计划：判定语义与反馈边界

> 最后更新：2026-07-16
> 主线顺序见 [../../mainline/DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md)。当前事实见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，稳定合同见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)，已完成实现与删除记录见 [CHANGELOG.md](CHANGELOG.md)。

## 子线职责

P1-C 只维护 BMS 判定家族、窗口/poor/release parity、判定结果到当前产品反馈面的语义边界，以及相关回归门。

- HUD/skin 宿主归 P1-A；P1-C 不扩写旧宿主接口。
- 真实 LN/CN/HCN 与谱面体验归 P1-E，最终人工结果由 P1-G 汇总。
- 视觉滚动与 Gimmick 位置映射归 P1-L，不得改变 P1-C 的时间判定结果。
- 完整 FHS、dan、1P/2P flip、BSS/MSS 不进入本线当前交付。

## 当前基线

- IIDX/LR2/beatoraja/OD 的主要窗口、边界、scratch、release 与 excessive/empty-poor 合同已落，并由 parity test 守门。
- 常驻 `DefaultBmsSpeedFeedbackDisplay`、`GameplayFeedbackState` 及其 FAST/SLOW、pacemaker、summary、常驻 GN 已按产品决定删除，不是当前能力。
- 当前反馈面只有全局 `JudgementCounterDisplay`、调速 toast、pre-start overlay 与既有 target/cycle/remember 行为。
- pre-start 纯视觉流速 preview 已存在，但不得进入 hit object、判定、计分、键音、replay 或 autoplay authority。

完成阶段的设计和删除经过不在本页展开，按日期查 [CHANGELOG](CHANGELOG.md)。

## 当前执行顺序

### 1. 保持 parity gate

本项是守门职责，不主动扩功能：

1. 任何窗口、非对称方向、scratch、long-note release、excessive/empty poor 改动，先修改 `BmsJudgementSystemParityTest` 让差异可审，再改实现。
2. 明确区分 audit/source-backed 数值与 IIDX 闭源 documented heuristic；不得把启发式写成完整精确复刻。
3. 保持跨家族 inclusive/epsilon 边界一致，同时保留各家族的早晚非对称和 poor/release 差异。
4. 判定时间链与视觉滚动正交；P1-L 旁路、lane cover、皮肤或 HUD 变化不得改变 score/replay truth。

验收：受影响的 parity focused 通过；涉及 gameplay/LN 时补 BMS relevant/full，并确认现有 counter/results 消费没有语义倒退。

### 2. 把真实谱人工证明交给 P1-E/P1-G

1. P1-E 维护代表性 LN/CN/HCN、scratch/release/poor 真实谱 checklist，并记录谱面与预期语义。
2. P1-G 汇总设备、真实谱与当前可见反馈的人工结果；P1-C 只解释判定合同，不重复建立验收总表。
3. 人工发现窗口/规则缺陷时重新归回 P1-C；显示、输入或长条 runtime 问题分别回 P1-A/B/D/E。

验收：每个反馈都有谱面、模式、输入条件、期望/实际和归线结论，不能用单一 headless 数字替代真实组合证明。

### 3. 未来反馈需求必须另立专题

只有用户重新确认 FAST/SLOW、pacemaker 或其它训练反馈的产品价值后，才允许新建实施专题；不得直接复活已删除 aggregate/card。

新专题必须先冻结：

1. 玩家问题与最小用户可见结果。
2. 数据 authority、生命周期与是否需要持久化。
3. P1-A 提供的宿主/skin slot 与 fallback 粒度。
4. 与全局 `JudgementCounterDisplay`、toast、pre-start overlay、results 的去重边界。
5. 自动与人工验收、回退路径和删除策略。

未完成以上决议前，反馈需求不得偷塞进 gauge、combo、wrapped HUD 子节点或旧 `IBmsHudLayoutDisplay` 扩展。

## 当前不做

- 不恢复常驻 GN/FAST-SLOW/pacemaker/summary card，也不把其历史设计当作待实现清单。
- 不为展示需求修改判定窗口、score bucket、gauge truth 或 replay。
- 不把完整 Floating/FHS、训练系统或新 gameplay mod 混入 parity 维护。
- 不复制 P1-E/P1-G 的人工验收状态到本页。
