# P1-H 开发计划：存储拓扑支撑线

> 最后更新：2026-04-20
> 主线总规划见 [../../mainline/DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md)。

## 子线目标

- 维持 `chartbms/`、`chartmania/`、便携模式与外部多目录谱库扫描这条存储拓扑支撑线稳定。
- 为后续导入、发布与本地优先策略提供稳定地基。

## 当前执行顺序

1. 维持现有存储拓扑不回退。
2. 补齐删除 / 失效语义、path identity dedup 与重扫策略。
3. 把影响导入、发布或本地数据根的结论同步到主线与发行文档。