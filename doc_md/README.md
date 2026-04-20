# OMS 文档总索引

除仓库根目录的项目入口 README 外，仓库治理文档统一收口到 `doc_md/`。

## 目录分层

- [mainline/README.md](mainline/README.md)：主线治理文档。固定承载全局开发计划、开发进度、变动日志与权威开发约束。
- [subline/README.md](subline/README.md)：主线子方向文档。每条 `P1-*` 子线固定使用独立目录，并维护 `DEVELOPMENT_PLAN.md`、`DEVELOPMENT_STATUS.md`、`CHANGELOG.md`、`TECHNICAL_CONSTRAINTS.md`。
- [other/README.md](other/README.md)：其他重要参考文档。承载外部审计、发行说明、皮肤说明、上游同步等不会直接替代主线治理的材料。
- [mini/README.md](mini/README.md)：与主线无关的独立记录。每个 mini 事项也必须固定维护 `DEVELOPMENT_PLAN.md`、`DEVELOPMENT_STATUS.md`、`CHANGELOG.md`、`TECHNICAL_CONSTRAINTS.md`。

## 联动更新规则

1. 任何开发开始前，先判断归属 `mainline`、`subline`、`other` 还是 `mini`。
2. 任何实现、调研、修复、验收一旦改变计划、状态、约束或验证结论，必须在同次改动中同步更新对应文档。
3. `subline` 或 `mini` 的变化若影响全局优先级、全局状态或硬约束，必须反向同步 `mainline`。
4. `other` 中的参考结论若升级为正式约束或执行优先级，也必须回写 `mainline` 与相关 `subline`。
5. 不允许只改代码不改文档，也不允许保留已经失真的文档叙事。

## 推荐阅读顺序

1. [../README.md](../README.md)
2. [mainline/OMS_COPILOT.md](mainline/OMS_COPILOT.md)
3. [mainline/DEVELOPMENT_PLAN.md](mainline/DEVELOPMENT_PLAN.md)
4. [mainline/DEVELOPMENT_STATUS.md](mainline/DEVELOPMENT_STATUS.md)
5. 按开发线进入 [subline/README.md](subline/README.md)
6. 按参考材料进入 [other/README.md](other/README.md)
7. 处理与主线无关事项时进入 [mini/README.md](mini/README.md)