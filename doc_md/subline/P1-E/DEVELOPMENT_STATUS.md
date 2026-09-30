# P1-E 开发进度：gameplay 与长条真实谱面验校

> 最后核对：2026-10-01（同步 9 月 30 日长条优化证据；真实谱人工门不变）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，当前执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

## 当前阶段

- 当前仓库已具备 LN / CN / HCN 运行时路径：`BmsGaugeProcessor` 的 `TotalHittableObjects` / `BaseRate` 已尊重 long-note 结构，`CN` / `HCN` 的 scored tail 会进入 gauge 分母，`HCN` body tick 仍保持 gauge-only。
- TOTAL 缺省预算使用辅助前、按运行时长条模式计数的物量；实际回血分母使用辅助后手动判定点。2026-09-23对齐[P1-C合同](../P1-C/TECHNICAL_CONSTRAINTS.md#results-与验证边界)，HCN持续速率未调整，真实谱人工门未关闭。
- 长条语义已冻结：LN 中途松开即终结；CN 有计分尾判但中途松开后不可接回；HCN 有持续 gauge body 与计分尾判，并且是唯一允许重按恢复的模式。实现地雷见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)，更正过程见 [CHANGELOG](CHANGELOG.md) 2026-06-21。
- 2026-09-30 已优化长条 tick 推进及模式读取，撤销结果和池化复用会重置推进位置；未删除嵌套判定对象或改变血量结果。性能与回退软件证据归 [P1-J](../P1-J/DEVELOPMENT_STATUS.md)，不将该实现重新列为待开发，也不据此关闭真实谱长条验校。
- long-note release-window 已切到 judge-mode-aware 模型，但真实谱面长条边界、gameplay HUD 最小必要补强与人工验校仍未收口。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 真实谱面 checklist | 未开始 | 待整理真实 LN/CN/HCN checklist |
| 长条边界验校 | 进行中 | 核心运行时已接通，但仍缺真实谱面人工验校 |
| 结果回写主线 | 未开始 | 依赖验校完成 |

## 最近一次验证

2026-09-30 长条稳态与 LN/CN/HCN 的 tick 撤销、重新判定和池化复用专项通过；后续 BGA/作者工作最终 BMS full 仍通过上述三种模式的回退/复用及 HCN body gauge 用例。前后测量见 [性能验证](../../other/GAMEPLAY_PERFORMANCE_20260930.md)，最新完整回归的既有失败边界见 [作者能力验证](../../other/BGA_SKIN_AUTHORING_20260930.md)。这些自动结果不替代真实谱 checklist、长条手感或输入组合签收。

2026-09-22 TOTAL 的 LN/CN/HCN 物量与辅助前后计数证据仍按原日期保留，见 [P1-C 验证](../P1-C/DEVELOPMENT_STATUS.md#最近一次验证)及本线 [CHANGELOG](CHANGELOG.md)。

## 文档治理验证

2026-10-01：对照 [DrawableBmsHoldNote](../../../osu.Game.Rulesets.Bms/UI/DrawableBmsHoldNote.cs) 的 tick 游标与重置点、9 月 30 日专项和最终 full TRX，补齐已落优化与最新软件证据；三轴合同、实际 checklist 及人工门保持。仅文档复核，未重跑产品测试或实机；此前审查见 [CHANGELOG](CHANGELOG.md)。
