# OMS 主线治理入口

主线只回答四个问题：产品不能做什么、当前在哪里、下一步先做什么、最近一次验证是否可信。

| 需求 | 打开文件 | 读取方式 |
| --- | --- | --- |
| 当前状态与阻塞 | [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md) | 默认首先读取，可整篇读取 |
| 执行顺序与验收门 | [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md) | 读取当前阶段与相关子线 |
| 产品/架构硬约束 | [OMS_COPILOT.md](OMS_COPILOT.md) | 用 `rg` 按主题定位，勿默认整篇加载 |
| 历史与旧验证 | [CHANGELOG.md](CHANGELOG.md) | 用日期、`P1-X` 或关键词定点搜索 |

详细的文件职责、行数预算和联动规则见 [../README.md](../README.md)。子线状态路由见 [../subline/README.md](../subline/README.md)；皮肤任务的恢复边界由该路由指向当前 P1-A 与恢复审计。
