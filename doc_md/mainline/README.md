# 主线文档索引

这里收口仓库级的权威治理文档，决定项目叙事、执行顺序、技术约束与验证历史。

## 固定四件套

- [OMS_COPILOT.md](OMS_COPILOT.md)：权威开发约束、技术纪律、release gate 与不可违背的 fallback 规则。
- [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)：总览开发计划、阶段、主线、子主线、验收标准与强制先后手。
- [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)：总览开发进度、当前仓库真实状态、最近验证与遗留问题。
- [CHANGELOG.md](CHANGELOG.md)：总览变动日志，按日期倒序记录已验证通过的变更摘要。

## 联动要求

1. 任何开发若改变主线优先级、主线状态、硬约束或已验证结论，必须同步更新本目录四件套。
2. `subline`、`other`、`mini` 的内容一旦上升为全局事实，也必须回写这里。