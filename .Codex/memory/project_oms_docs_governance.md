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

完整规则以 [AGENTS.md](../../AGENTS.md) 和 [doc_md/README.md](../../doc_md/README.md) 为准；本文件不保存某轮治理前的临时交接状态。

## 2026-07-16 治理结果

- 已完成 mainline、subline、`other/` 与 memory 的分层归位：活动 STATUS 只保留当前事实/风险/下一门/最新验证，逐切过程和旧数字回到 CHANGELOG，memory 只做稳定地雷召回，`SKINNING.md` 只保留一个集中当前能力块。
- 已补齐参考索引、规范相对链接并移除仓库内不必要的本机会话标识与用户数据指纹；精确生产取证值只保存在仓库外脱敏恢复归档。
- 每次文档改动结束运行 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\CheckDocumentation.ps1`；它兼容 Windows PowerShell 5.1，固定检查链接、四件套/索引完整性、STATUS/README 预算、memory wiki 链、明确隐私残片和 PLAN 会话污染，再配合 `git diff --check`。通用路径、公开 checksum 或合法数字矩阵只能告警复核，不能靠模糊 regex 强迫删除。
- 本轮只治理文档，不修改代码、生产数据或 runtime，不计作产品功能，也不改变任何自动/人工 gate 结论。后续若 STATUS/PLAN 再混入逐切历史，按上述职责直接归回 CHANGELOG，不新建第二份“当前状态”。
