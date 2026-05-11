# P1-I 开发进度：BMS 选歌筛选与搜索定制

> 最后更新：2026-05-11
> 主线全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。本文件只记录 `P1-I` 的真实进展。

## 当前阶段

- **阶段定位**：子线建档完成，当前仍停留在“语义冻结 + read-model 前置确认”阶段，尚未进入代码落地。
- **代码状态**：共享 [../../osu.Game/Screens/Select/FilterControl.cs](../../osu.Game/Screens/Select/FilterControl.cs) 仍使用 star slider；BMS 当前没有 `CreateRulesetFilterCriteria()` override；键数已可从 [../../osu.Game.Rulesets.Bms/BmsRuleset.cs](../../osu.Game.Rulesets.Bms/BmsRuleset.cs) 读取，但 RC/LN/SCR 构成统计仍只存在于 [../../osu.Game.Rulesets.Bms/SongSelect/BmsNoteDistributionGraph.cs](../../osu.Game.Rulesets.Bms/SongSelect/BmsNoteDistributionGraph.cs) 的 on-demand analyzer 链，不是 Song Select 可同步消费的 persisted filter key。
- **文档状态**：`P1-I` 四件套已建立，并已同步挂到主线与子线索引；当前文档还额外补齐了首轮代码锚点、测试落点、交互降级路线与建议验证命令，已可直接作为实现清单使用。

## 已确认事实

- BMS-only 搜索 / 筛选定制在现有架构下可行，但首轮最稳的落点是 shared `FilterControl` 内的 ruleset-aware branching，而不是新开 per-ruleset host。
- BMS custom search 应沿 mania 现有模式走 `IRulesetFilterCriteria`；当前 shared parser 已有合适扩展点，不需要新增 BMS-only switch。
- `键数` 已有稳定 authority；`谱面构成` 没有。若不先补 persisted read-model，就只能在过滤阶段临时加载 playable beatmap，这不适合作为 Song Select authority。
- 现有 note distribution 统计里的 scratch / LN summary 计数允许重叠；若直接挪用，会导致 `RC / LN / SCR` 三段不闭合。因此新 filter data 必须采用互斥分区。
- 只隐藏 star slider 而不改 criteria 生成链，会留下 BMS 幽灵星数过滤；这是首轮必须避免的 shared-state 回归点。
- 首轮实现锚点已经收口：typed metadata helper 以 `BmsBeatmapMetadataData` 为中心，写入 authority 以 `BmsImportedBeatmapFactory` / `BmsFolderImporter` 为中心，UI branch 以 `FilterControl` 为中心，测试则以 `BmsImportIntegrationTest`、`TestSceneBmsSongSelectDifficultyTable`、`TestSceneBeatmapFilterControl` 与 `TestSceneSongSelectFiltering` 为首批落点。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 子线归线与四件套建档 | 已完成 | `P1-I` 已正式建立 |
| RC/LN/SCR read-model 建模 | 未开始 | persisted filter stats 尚不存在 |
| BMS ruleset criteria / custom search | 未开始 | 当前无 `BmsFilterCriteria` |
| BMS-only FilterControl UI branch | 未开始 | 当前仍为 shared star slider surface |
| focused regression 设计 | 未开始 | 尚无专用测试切片 |

## 当前风险

- **authority 缺口风险**：如果不先补 persisted filter stats，后续 UI / custom search 只能建立在 runtime analyzer 或 details panel cache 上，无法稳定服务整个 carousel。
- **语义重叠风险**：scratch long note 若不先决定归属，`RC / LN / SCR` 百分比会出现和不为 `100%` 的不闭合状态。
- **共享状态污染风险**：若 BMS 只是隐藏 star slider 而不切断其 criteria 写入，切 ruleset 或重进选歌后容易留下看不见的星数过滤。
- **scope 漂移风险**：若为赶功能去重做 per-ruleset `FilterControl` host，会把本专题从 BMS-only Song Select 定制升级成共享层架构改造，超出首轮范围。

## 下一检查点

1. 冻结 filter stats 的实际承载模型：继续复用 `BmsBeatmapMetadataData`，还是拆成独立嵌套 data object。
2. 在代码实现前先把 `RC / LN / SCR` 的互斥分类写成测试基线，优先落在 `BmsImportIntegrationTest` 或等价 typed-data test，避免 UI 先行后再回改数据口径。
3. 以 `I1 -> I2 -> I3` 顺序推进，避免先做 UI 再倒逼 shared parser 或 metadata 层补洞。
4. 若 segmented-range 交互一轮内抽不稳，直接按文档降级路线落 BMS-local 私有控件，不再为 shared 抽象阻塞首版交付。

## 验证记录

- 2026-05-11：本轮完成 `P1-I` 子线建档后的第二轮补强，补齐首轮代码锚点、测试锚点、UI 降级路线、建议验证命令，并把相关技术纪律同步到 mainline `OMS_COPILOT.md`；无代码变更、无新增测试执行。
