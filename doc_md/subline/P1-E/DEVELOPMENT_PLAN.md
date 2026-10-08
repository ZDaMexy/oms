# P1-E 开发计划：gameplay 与长条真实谱面验校

> 最后更新：2026-10-08（删除旧训练反馈目标歧义；真实谱验校顺序与产品验证日期不变）
> 主线总规划见 [../../mainline/DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md)。

## 子线目标

- 用真实谱面收口 LN / CN / HCN、长条边界与 gameplay 边角语义。
- 为现有判定与长条语义提供真实谱面验证基线；训练反馈的停止边界仍按 [P1-C 计划](../P1-C/DEVELOPMENT_PLAN.md#3-未来反馈需求必须另立专题)执行。

## 当前执行顺序

1. 建立真实谱面 checklist。
2. 收口 LN / CN / HCN 边界与尾判场景。
3. 将结果记录在本线状态与历史，由 P1-G 汇总人工验收；影响全局优先级、release gate 或硬约束时才向主线回写摘要与链接。
