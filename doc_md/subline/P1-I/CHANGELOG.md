# P1-I 变动日志

## 2026-05-11

### 需求澄清与文档校正：谱面构成的三个值是最大占比

- 本轮把 `P1-I` 的 `谱面构成` 产品合同重新冻结为：**单轨、从左到右 `RC / LN / SCR` 三个可编辑上限段、尾段为空白容差、三段独立启用/禁用**。
- `RC / LN / SCR` 三个值现在明确表示各自的最大占比，不强制和为 `100%`；剩余尾段空白用于表达容差，而不是第四类真实谱面成分。
- shared `FilterControl` 里的 BMS `谱面构成` 仍只是“三条独立 range slider”原型；该形态不再被视为 `I3` 已完成交付，只保留为一次原型尝试。
- 文本 `rc/ln/scr` 语法仍继续保持完整范围匹配能力；visual UI 首轮只负责生成 enabled segment 的上限约束，不反向削弱 text query 语义。
- 本轮只做文档校正与状态回写，无代码变更、无新增测试执行。

### 首轮代码落地：read-model、custom search 与 BMS-only FilterControl

- `BmsBeatmapMetadataData` 现已新增 persisted `ChartFilterStats` typed metadata，`BmsImportedBeatmapFactory` 在导入时写入，`BmsFolderImporter` 在 reuse 命中旧 set 时按 MD5 自愈同步，确保 RC/LN/SCR authority 不再停留在 runtime analyzer。
- `BmsRuleset` 现已正式 override `CreateRulesetFilterCriteria()`，`BmsFilterCriteria` 已接入 `key/keys`、`rc`、`ln`、`scr` 与极少 alias；BMS custom search 继续复用 shared `FilterQueryParser` 的 ruleset hook，没有在 shared parser 新增 BMS-only switch。
- shared `FilterControl` 现已在现有 host 内切出 BMS-only product surface：BMS ruleset 显示 `谱面构成` 三段 range row 与 `键数` toggle row，非 BMS ruleset 继续保留原有 star slider。BMS 分支同时切断了隐藏 star slider 对 `UserStarDifficulty` 的幽灵写入。
- 首轮 focused regression 已补到 importer / statistics / ruleset criteria / BMS Song Select FilterControl。`dotnet test osu.Game.Rulesets.Bms.Tests -p:GenerateFullPaths=true --filter "FullyQualifiedName~BmsImportIntegrationTest|FullyQualifiedName~BmsBeatmapStatisticsTest|FullyQualifiedName~BmsFilterCriteriaTest|FullyQualifiedName~TestSceneBmsFilterControl"` **30/30** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### 子线正式建档

- 新建 `P1-I` 四件套，正式把 **BMS 选歌筛选与搜索定制** 从 `P1-A` / `P1-H` 的从属影响中独立出来，作为 Phase 1.x 的一条新子线维护。
- 当前文档已冻结首轮执行顺序：`read-model 建模` → `ruleset criteria / custom search` → `BMS-only FilterControl UI` → `focused regression`。
- 当前文档也已把两条关键前置写死：`键数` 已有现成 authority，而 `RC / LN / SCR` 仍缺 persisted filter stats；因此首轮不能跳过 metadata/read-model 直接做 UI。
- 第二轮复查已继续补齐首轮代码锚点、测试落点、`谱面构成` 交互降级路线与建议验证命令，并把两条全局技术纪律同步到 `OMS_COPILOT.md`：BMS filter data 必须走 typed metadata helper，BMS custom search 必须继续走 `IRulesetFilterCriteria`。
- 本轮仅完成文档治理与主线同步，无代码变更、无新增测试执行。
