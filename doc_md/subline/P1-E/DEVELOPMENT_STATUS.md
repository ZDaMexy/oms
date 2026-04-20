# P1-E 开发进度：gameplay 与长条真实谱面验校

> 最后更新：2026-04-20

## 当前阶段

- `P1-E` 已完成子线建档。
- 当前仓库已具备 LN / CN / HCN 运行时路径：`BmsGaugeProcessor` 的 `TotalHittableObjects` / `BaseRate` 已尊重 long-note 结构，`CN` / `HCN` 的 scored tail 会进入 gauge 分母，`HCN` body tick 仍保持 gauge-only。
- long-note release-window 已切到 judge-mode-aware 模型，但真实谱面长条边界、gameplay HUD 最小必要补强与人工验校仍未收口。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 子线建档 | 已完成 | 四件套已建立 |
| 真实谱面 checklist | 未开始 | 待整理真实 LN/CN/HCN checklist |
| 长条边界验校 | 进行中 | 核心运行时已接通，但仍缺真实谱面人工验校 |
| 结果回写主线 | 未开始 | 依赖验校完成 |

## 验证记录

- 本轮状态同步基于当前代码结构完成，沿用主线对 long-note / release-window 的既有已验证结论，未新增构建或测试命令。