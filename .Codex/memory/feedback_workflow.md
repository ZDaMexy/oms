---
name: feedback-workflow
description: OMS 用户协作偏好：中文、真机证据、验证/文档/记忆/提交闭环
metadata:
  node_type: memory
  type: feedback
---

# 协作偏好召回

- 中文沟通。
- 标准任务弧：归线 → 审查/取证 → 最小修复 → focused/full/build → doc + memory → 当前分支提交。
- 不新建分支、不走 PR；**push 必须按 AGENTS 取得用户确认**。
- 真机日志、用户复现和大库数据优先于“代码上应该没问题”或单测假绿。
- 设计合同变化先解释取舍，不把有意行为当 bug 盲改。
- STATUS 只更新当前事实和唯一最新验证；命令、数字和过程进 owning CHANGELOG。

## 大库/Realm 特别偏好

- 5万级问题允许多轮取证；先看真实 performance/database log，再优化。
- Realm link-traversal predicate 可能失败或静默零结果；复杂条件 materialize 后客户端过滤，并记录 Found N（包括 0）。
- 关注隐藏 timeout、`CacheNullValues=false`、海量 async task allocation 与 UI 线程/全局锁，而不只看算法表面。

权威协作规则始终以 [AGENTS.md](AGENTS.md) 为准；本文件只记录用户偏好和易忘的现场方法。
