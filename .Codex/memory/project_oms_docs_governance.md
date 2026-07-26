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

## 持续防回潮

- 文档/记忆同步只改变治理事实，不得冒充runtime、产品测试或人工gate；产品代码基线和最新验证只由STATUS指向，旧数字留在CHANGELOG。
- 每次文档改动结束运行`powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\CheckDocumentation.ps1`；它兼容Windows PowerShell 5.1，检查链接、四件套/索引完整性、STATUS/README预算、memory wiki链、明确隐私残片与PLAN会话污染，再配合`git diff --check`。
- 若STATUS/PLAN再次混入逐切历史，直接归回CHANGELOG；若memory重复当前进度或另一memory的详细合同，改成权威链接/交叉路由，不新建第二份当前状态。
- 通用路径、公开checksum或合法数字矩阵只能告警复核，不能靠模糊regex强迫删除；精确生产取证值只保存在仓库外脱敏恢复归档。
