---
name: project-oms-docs-governance
description: OMS 低噪声文档治理：当前/计划/约束/历史分离，默认短路径读取，memory 只作召回
metadata:
  node_type: memory
  type: project
---

# OMS 文档治理记忆

## 默认读取路径

1. `doc_md/mainline/DEVELOPMENT_STATUS.md`：当前阶段、最新验证、风险。
2. `doc_md/mainline/DEVELOPMENT_PLAN.md`：活动顺序与验收门。
3. `doc_md/subline/README.md` 路由到所属子线，只读该线 STATUS 与任务相关 CONSTRAINTS。
4. `OMS_COPILOT.md`、大型 PLAN/CHANGELOG 只用 `rg` 定点搜索，不整篇加载。

## 一个事实一个落点

- STATUS：当前事实、当前风险、下一检查点、唯一最新验证。
- PLAN：未完成工作、依赖、验收条件、冻结项。
- TECHNICAL_CONSTRAINTS：稳定合同、红线、必须重跑的验证面。
- CHANGELOG：日期化实现、调查过程、命令、旧测试数字。
- README：只做路由和一句话结论。
- memory：踩坑/诊断/偏好快速召回，不证明当前实现。

## 低噪声纪律

- STATUS 建议 ≤120 行，抬头只能是最后更新与上级入口；禁止塞“此前……”调查史。
- PLAN 不写逐刀实现和旧测试数字；完成项不再影响依赖时删除，历史由 Git/CHANGELOG 保存。
- mainline 只抄子线一句摘要和链接，不复制子线全文。
- 测试数字只在当前 STATUS 最新验证和当次 CHANGELOG 各出现一次。
- 带日期结论不再影响决策时，从 STATUS/PLAN 删除。
- 大型 CHANGELOG 允许增长，但通过 `rg -n "YYYY-MM-DD|P1-X|关键词"` 读取。

## 联动与权威

- 先归线；跨线指定一个主归属，其余只链接。
- 子线变化只有影响全局优先级、release gate 或硬约束时才回写 mainline。
- other 结论升级为正式决策时进入相应 PLAN/STATUS/CONSTRAINTS。
- 新踩坑同次更新 memory 与 `MEMORY.md` 索引。
- 冲突顺序：当前代码/测试/真机反馈 → mainline → subline → other → memory。

## 2026-07-10 整理结果

- mainline PLAN 1412→113 行，STATUS 243→97 行。
- P1-K PLAN 504→91 行；完成切片保留矩阵，逐文件历史只查 CHANGELOG。
- P1-A/C/H/I/J/K/L STATUS 已统一成短模板，逐日历史留在 CHANGELOG/Git。
- 入口顺序已同步到 AGENTS/CLAUDE、doc_md 索引和 subline 路由。
- 皮肤任务仍先读 `reference_skin_recovery_20260710.md`。
