# P1-E 开发进度：gameplay 与长条真实谱面验校

> 最后核对：2026-10-19（补齐mania同批松键和相邻音符锁定；真实谱人工门保持）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，当前执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

## 当前阶段

- 当前仓库已具备 LN / CN / HCN 运行时路径：`BmsGaugeProcessor` 的 `TotalHittableObjects` / `BaseRate` 已尊重 long-note 结构，`CN` / `HCN` 的 scored tail 会进入 gauge 分母，`HCN` body tick 仍保持 gauge-only。
- TOTAL 缺省预算使用辅助前、按运行时长条模式计数的物量；实际回血分母使用辅助后保留CountsForScore的判定点，全自动不删计分。结果一致性归[P1-C合同](../P1-C/TECHNICAL_CONSTRAINTS.md#results-与验证边界)，HCN持续速率未调整，真实谱人工门未关闭。
- 长条语义已冻结：LN 中途松开即终结；CN 有计分尾判但中途松开后不可接回；HCN 有持续 gauge body 与计分尾判，并且是唯一允许重按恢复的模式。实现地雷见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)，更正过程见 [CHANGELOG](CHANGELOG.md) 2026-06-21。
- 2026-09-30 已优化长条 tick 推进及模式读取，撤销结果和池化复用会重置推进位置；未删除嵌套判定对象或改变血量结果。性能与回退软件证据归 [P1-J](../P1-J/DEVELOPMENT_STATUS.md)，不将该实现重新列为待开发，也不据此关闭真实谱长条验校。
- 完成后回退到body会按既有entry、真实replay按键与自动辅助恢复holding，倒退输入不创建新判定；LN/CN终结后仍不可重抓。HCN漏头后实际接住的body可恢复gauge，head仍Miss，松开继续扣血；不改变窗口、计分尾或TOTAL。
- long-note release-window 已切到 judge-mode-aware 模型，真实谱面长条边界与人工验校仍未收口。展示验收归 [P1-A](../P1-A/DEVELOPMENT_STATUS.md#当前风险与未完成项)和 [P1-C](../P1-C/DEVELOPMENT_STATUS.md#当前反馈产品面)，不从旧“HUD 补强”字样推导新的训练反馈开发任务。
- 普通mania的同批down/up已按Result真实持有状态松键，立即记录body断条并保留重抓后的原尾判上限，不再因显示状态滞后错给Perfect。相邻note/hold的note lock识别计分后代，force-miss仍保留Ignore父对象完成body/tail的生命周期。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 真实谱面 checklist | 未开始 | 待整理真实 LN/CN/HCN checklist |
| 长条边界验校 | 进行中 | 核心运行时已接通，但仍缺真实谱面人工验校 |
| 实谱结果记录与人工汇总 | 未开始 | 依赖验校完成；本线记录、P1-G 汇总，影响全局门时摘要回主线 |

## 最近一次验证

2026-10-19后续深审：真实mania Player的同批/跨帧、断后重抓/不重抓及严格相邻note/hold判定均有有效修前/修后证明；完整结果取[性能与判定记录](../../other/GAMEPLAY_PERFORMANCE_JUDGEMENT_20261019.md#验证证据)。不直接调用DHO判定、不写内部holding或放宽时差断言。

2026-10-19真实Player/手动replay覆盖LN/CN/HCN完成后回退、持有/释放/未按，HCN漏头接回的实际tick/gauge，以及无实体动作的AutoNote/AutoScratch恢复。有效修前/修后与完整结果集中见[游玩审查](../../other/GAMEPLAY_REVIEW_20261019.md#验证证据)，不能以自动用例替代真实谱手感与设备签收。

2026-09-30 长条稳态与 LN/CN/HCN 的 tick 撤销、重新判定和池化复用专项通过；后续 BGA/作者工作最终 BMS full 仍通过上述三种模式的回退/复用及 HCN body gauge 用例。前后测量见 [性能验证](../../other/GAMEPLAY_PERFORMANCE_20260930.md)，最新完整回归的既有失败边界见 [作者能力验证](../../other/BGA_SKIN_AUTHORING_20260930.md)。这些自动结果不替代真实谱 checklist、长条手感或输入组合签收。

2026-09-22 TOTAL 的 LN/CN/HCN 物量与辅助前后计数证据仍按原日期保留，见 [TOTAL 原验证](../../other/BMS_TOTAL_RULES_AUDIT_20260922.md#验证状态)及本线 [CHANGELOG](CHANGELOG.md)。

## 文档治理验证

2026-10-08：清除旧训练反馈与泛化 HUD 补强的待办歧义，按本线验校、P1-G 汇总及 P1-A/P1-C 展示合同归线；真实 checklist 和人工门保持。此前 10 月 1 日长条优化证据复核保留原日期，见 [CHANGELOG](CHANGELOG.md)。仅文档与记忆检查，结果取 [主线治理日志](../../mainline/CHANGELOG.md#项目进度与文档记忆健康复核)，未重跑产品或实机。
