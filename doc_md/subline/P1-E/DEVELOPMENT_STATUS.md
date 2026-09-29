# P1-E 开发进度：gameplay 与长条真实谱面验校

> 最后核对：2026-09-29（源码与长条留存证据复核；产品/人工验证日期不变）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，当前执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

## 当前阶段

- 当前仓库已具备 LN / CN / HCN 运行时路径：`BmsGaugeProcessor` 的 `TotalHittableObjects` / `BaseRate` 已尊重 long-note 结构，`CN` / `HCN` 的 scored tail 会进入 gauge 分母，`HCN` body tick 仍保持 gauge-only。
- TOTAL 缺省预算使用辅助前、按运行时长条模式计数的物量；实际回血分母使用辅助后手动判定点。2026-09-23对齐[P1-C合同](../P1-C/TECHNICAL_CONSTRAINTS.md#results-与验证边界)，HCN持续速率未调整，真实谱人工门未关闭。
- 长条语义已冻结：LN 中途松开即终结；CN 有计分尾判但中途松开后不可接回；HCN 有持续 gauge body 与计分尾判，并且是唯一允许重按恢复的模式。实现地雷见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)，更正过程见 [CHANGELOG](CHANGELOG.md) 2026-06-21。
- long-note release-window 已切到 judge-mode-aware 模型，但真实谱面长条边界、gameplay HUD 最小必要补强与人工验校仍未收口。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 真实谱面 checklist | 未开始 | 待整理真实 LN/CN/HCN checklist |
| 长条边界验校 | 进行中 | 核心运行时已接通，但仍缺真实谱面人工验校 |
| 结果回写主线 | 未开始 | 依赖验校完成 |

## 最近一次验证

2026-09-22 BMS full 留存 TRX 中，`BmsDrawableRulesetTest` 所含 runtime 用例通过；TOTAL 专项同时覆盖 LN/CN/HCN 物量与辅助前后计数。完整回归的既有失败及专项边界见 [P1-C 最新验证](../P1-C/DEVELOPMENT_STATUS.md#最近一次验证)。这些自动结果不替代真实谱 checklist、长条手感或输入组合签收。

## 文档治理验证

2026-09-29：回读9月22日留存 TRX，并核对 [DrawableBmsHoldNote](../../../osu.Game.Rulesets.Bms/UI/DrawableBmsHoldNote.cs) 的 release/regrab、[BmsGaugeProcessor](../../../osu.Game.Rulesets.Bms/Scoring/BmsGaugeProcessor.cs) 的辅助前后物量；LN/CN/HCN 三轴合同及未闭合人工门保持。未重跑产品测试或实机；此前审查见 [CHANGELOG](CHANGELOG.md)。
