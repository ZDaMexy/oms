# P1-I 变动日志

## 2026-05-11

### 子线正式建档

- 新建 `P1-I` 四件套，正式把 **BMS 选歌筛选与搜索定制** 从 `P1-A` / `P1-H` 的从属影响中独立出来，作为 Phase 1.x 的一条新子线维护。
- 当前文档已冻结首轮执行顺序：`read-model 建模` → `ruleset criteria / custom search` → `BMS-only FilterControl UI` → `focused regression`。
- 当前文档也已把两条关键前置写死：`键数` 已有现成 authority，而 `RC / LN / SCR` 仍缺 persisted filter stats；因此首轮不能跳过 metadata/read-model 直接做 UI。
- 第二轮复查已继续补齐首轮代码锚点、测试落点、`谱面构成` 交互降级路线与建议验证命令，并把两条全局技术纪律同步到 `OMS_COPILOT.md`：BMS filter data 必须走 typed metadata helper，BMS custom search 必须继续走 `IRulesetFilterCriteria`。
- 本轮仅完成文档治理与主线同步，无代码变更、无新增测试执行。
