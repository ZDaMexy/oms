---
name: feedback-workflow
description: OMS 用户协作偏好：中文、简洁实现、真机证据与交付闭环
metadata:
  node_type: memory
  type: feedback
---

# 协作偏好召回

协作流程与 Git 规则只维护于 [AGENTS](../../AGENTS.md)。这里保留用户偏好及现场方法：

- 中文沟通，报告先给结果和证据；已授权工作自主闭合，常规选择不反复追问。
- 用户明确反感为假想异常堆叠 guard、catch、默认值、fallback 和抽象。按 [实现取舍](../../AGENTS.md#实现取舍)直接表达业务流程；内部信任已验证合同，真实外部输入/数据/sandbox 边界仍须保护。
- 真机日志、用户复现和大库数据高于“代码上应该没问题”或单测假绿；有意行为和既定取舍不能当 bug 盲改。
- 五万级曲库问题允许持续取证。先抓实际 performance/database log，关注隐藏 timeout、`CacheNullValues=false`、大量异步任务、UI 线程和全局锁。
- Realm link-traversal predicate 曾失败或静默零结果；复杂查询先核对表达式支持及实际结果，必要时 materialize 后客户端过滤，并记录 Found N（包括 0）。
