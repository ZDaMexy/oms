# P1-E 开发进度：gameplay 与长条真实谱面验校

> 最后更新：2026-07-16（文档健康治理；功能状态未改变）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，当前执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

## 当前阶段

- 当前仓库已具备 LN / CN / HCN 运行时路径：`BmsGaugeProcessor` 的 `TotalHittableObjects` / `BaseRate` 已尊重 long-note 结构，`CN` / `HCN` 的 scored tail 会进入 gauge 分母，`HCN` body tick 仍保持 gauge-only。
- 长条语义已冻结：LN 中途松开即终结；CN 有计分尾判但中途松开后不可接回；HCN 有持续 gauge body 与计分尾判，并且是唯一允许重按恢复的模式。实现地雷见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)，更正过程见 [CHANGELOG](CHANGELOG.md) 2026-06-21。
- long-note release-window 已切到 judge-mode-aware 模型，但真实谱面长条边界、gameplay HUD 最小必要补强与人工验校仍未收口。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 真实谱面 checklist | 未开始 | 待整理真实 LN/CN/HCN checklist |
| 长条边界验校 | 进行中 | 核心运行时已接通，但仍缺真实谱面人工验校 |
| 结果回写主线 | 未开始 | 依赖验校完成 |

## 当前验证基线

- 当前仅完成基于代码结构的状态同步，并沿用主线对 long-note / release-window 的既有已验证结论，尚无新增构建或测试执行。
- 后续若出现按日期展开的实现或验证，统一写入 [CHANGELOG.md](CHANGELOG.md)。
