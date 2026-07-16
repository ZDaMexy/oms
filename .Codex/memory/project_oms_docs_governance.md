---
name: project-oms-docs-governance
description: OMS 低噪声文档治理与易忘的防失真规则
metadata:
  node_type: memory
  type: project
---

# 文档治理召回

默认读取：mainline STATUS → mainline PLAN → 子线 STATUS/相关 CONSTRAINTS；OMS_COPILOT/CHANGELOG 只定点搜索。

协作入口只有 `AGENTS.md`；`CLAUDE.md` 等适配文件只跳转，不复制规则。memory 通过 `MEMORY.md` 定点选择，禁止整库加载。

一个事实一个落点：

- STATUS＝当前事实/风险/下一门/唯一最新验证。
- PLAN＝未完成工作/依赖/验收/冻结项。
- CONSTRAINTS＝稳定合同与红线。
- CHANGELOG＝日期化实现、命令、旧数字和调查史。
- memory＝踩坑与诊断，不证明当前实现。

易忘规则：STATUS 建议 ≤120 行；mainline 不复制子线长段；测试数字只在当前 STATUS 和当次 CHANGELOG 各一份；子线只有影响全局优先级/release gate/硬约束才回写 mainline。

memory 模板：权威链接 → 稳定合同 → 地雷/诊断 → 未闭合项。逐日实现史、回退过程和旧数字进入 CHANGELOG/Git；文件名尽量稳定以保护 wiki 链接，单行建议 ≤800 字符。

完整规则以 `AGENTS.md` 和 `doc_md/README.md` 为准。2026-07-10 已完成主线/P1-K PLAN、活动 STATUS 与 Codex memory 降噪，历史仍在 Git/CHANGELOG。

## 2026-07-16 P1-A 健康治理交接

- 当前 P1-A STATUS/PLAN 明显超过自身低噪声预算，并重复保存逐切实现史、旧测试数字和已失效的“下一切”指令；`SKINNING.md` 也混有当前能力、目标能力与历史切片计数。
- 下一新对话先做结构治理：保持所有产品合同、链接和 gate 语义不变；STATUS 收敛到当前事实/风险/下一门/唯一最新验证，PLAN 收敛到未来步骤/依赖/验收，历史数字迁入 CHANGELOG，memory 只留稳定地雷，派生作者文档只保留一个醒目的当前能力块。
- 该治理不修改代码、生产数据或 runtime，不把文档整理计作产品功能，也不能把 managed `.osk` 新动画实机 gate 标记为通过。治理结束后重新冻结执行入口，再决定后续实现。
