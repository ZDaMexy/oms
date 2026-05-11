# P1-I 开发进度：BMS 选歌筛选与搜索定制

> 最后更新：2026-05-11
> 主线全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。本文件只记录 `P1-I` 的真实进展。

## 当前阶段

- **阶段定位**：`I1` / `I2` 已收口；`I3` 在首轮原型后进入“产品语义纠偏 + focused regression 维持”阶段。
- **代码状态**：BMS 当前已具备 persisted `ChartFilterStats` metadata、`BmsFilterCriteria`、`BmsRuleset.CreateRulesetFilterCriteria()` 与 shared `FilterControl` 内的 BMS-only row branch。`FilterControl` 对 BMS 规则集不再把隐藏 star slider state 写入 `UserStarDifficulty`；但 `谱面构成` 当前仍是三条彼此独立的 range slider 原型，不符合已冻结的“单轨上限段 + 尾段空白容差 + 独立启停”最终合同。
- **文档状态**：`P1-I` 四件套已建立，并已同步挂到主线与子线索引；当前文档还额外补齐了首轮代码锚点、测试落点、交互降级路线与建议验证命令，已可直接作为实现清单使用。

## 已确认事实

- BMS-only 搜索 / 筛选定制在现有架构下可行，但首轮最稳的落点是 shared `FilterControl` 内的 ruleset-aware branching，而不是新开 per-ruleset host。
- BMS custom search 应沿 mania 现有模式走 `IRulesetFilterCriteria`；当前 shared parser 已有合适扩展点，不需要新增 BMS-only switch。
- `键数` 已有稳定 authority；`谱面构成` 没有。若不先补 persisted read-model，就只能在过滤阶段临时加载 playable beatmap，这不适合作为 Song Select authority。
- 现有 note distribution 统计里的 scratch / LN summary 计数允许重叠；若直接挪用，会导致 `RC / LN / SCR` 三段不闭合。因此新 filter data 必须采用互斥分区。
- 当前 `谱面构成` 的真实产品目标仍是范围筛选，但 visual control 已补充冻结为：单轨 shared-track、`RC / LN / SCR` 三个可编辑上限段、尾段为空白容差、三段独立启用/禁用。
- 当前 visual UI 的三个值不再表示精确配比，而是各自的最大占比；visual control 首轮只负责生成 `rc/ln/scr` 的上限约束，完整范围表达仍保留给文本搜索。
- 当前 `FilterControl` 原型与最终合同仍有一处明确偏差：它是“三条独立 range slider”，而不是“一条带尾段容差的单轨上限控件”。
- 只隐藏 star slider 而不改 criteria 生成链，会留下 BMS 幽灵星数过滤；这是首轮必须避免的 shared-state 回归点。
- 首轮实现锚点已经收口：typed metadata helper 以 `BmsBeatmapMetadataData` 为中心，写入 authority 以 `BmsImportedBeatmapFactory` / `BmsFolderImporter` 为中心，UI branch 以 `FilterControl` 为中心，测试则以 `BmsImportIntegrationTest`、`TestSceneBmsSongSelectDifficultyTable`、`TestSceneBeatmapFilterControl` 与 `TestSceneSongSelectFiltering` 为首批落点。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 子线归线与四件套建档 | 已完成 | `P1-I` 已正式建立 |
| RC/LN/SCR read-model 建模 | 已完成 | `BmsBeatmapMetadataData.ChartFilterStats` + importer/reuse 自愈已落地 |
| BMS ruleset criteria / custom search | 已完成 | `BmsFilterCriteria` + `BmsRuleset.CreateRulesetFilterCriteria()` 已接通 |
| BMS-only FilterControl UI branch | 进行中 | `键数` row 已达目标；`谱面构成` 仍是三条独立 range slider 原型，需改成单轨上限控件并显式显示尾段容差 |
| focused regression 设计 | 进行中 | BMS importer/criteria/UI 重点切片已落地；shared visual test runner 与 I3 最终控件回归仍待补强 |

## 当前风险

- **authority 缺口风险**：如果不先补 persisted filter stats，后续 UI / custom search 只能建立在 runtime analyzer 或 details panel cache 上，无法稳定服务整个 carousel。
- **语义重叠风险**：scratch long note 若不先决定归属，`RC / LN / SCR` 百分比会出现和不为 `100%` 的不闭合状态。
- **产品合同错配风险**：若继续把当前“三条独立 range slider”当成已完成实现，后续 search semantics、UI 测试与用户预期会继续漂移。
- **无解组合风险**：`RC / LN / SCR` 三个值现在表达的是各自最大占比；若用户把三者都压得过低，筛选结果允许为空，不能再额外发明自动补偿语义去“凑满 100%”。
- **范围语义落差风险**：文本 `rc/ln/scr` 仍是完整范围语法，而 visual control 现只负责上限编辑；实现时必须明确 UI 负责的编辑子集，不能反向削弱文本语法。
- **共享状态污染风险**：若 BMS 只是隐藏 star slider 而不切断其 criteria 写入，切 ruleset 或重进选歌后容易留下看不见的星数过滤。
- **scope 漂移风险**：若为赶功能去重做 per-ruleset `FilterControl` host，会把本专题从 BMS-only Song Select 定制升级成共享层架构改造，超出首轮范围。

## 下一检查点

1. 用文档冻结 `谱面构成` 的最终合同：单轨上限段、尾段空白容差、三段独立启停、visual UI 只负责上限编辑。
2. 把当前三条独立 range slider 原型替换成单轨上限控件，同时保留现有 `键数` row 与 query 编译链。
3. 以新的最终控件重跑 BMS focused tests，并补 shared visual gate 能力。
4. 完成 UI 收口后，再评估是否需要追加 one-shot legacy backfill 工具链，而不是在启动路径硬塞全库扫描。

## 验证记录

- 2026-05-11：`dotnet test osu.Game.Rulesets.Bms.Tests -p:GenerateFullPaths=true --filter "FullyQualifiedName~BmsImportIntegrationTest|FullyQualifiedName~BmsBeatmapStatisticsTest|FullyQualifiedName~BmsFilterCriteriaTest|FullyQualifiedName~TestSceneBmsFilterControl"` **30/30** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。
- 2026-05-11：根据新增需求澄清复核 `P1-I` 文档：明确 `谱面构成` 的三个值表示各自最大占比，和不强制为 `100%`，剩余尾段空白用于表达容差；visual UI 只负责生成上限约束。本轮仅做文档校正，无代码变更、无新增测试执行。
- 2026-05-11：本轮完成 `P1-I` 子线建档后的第二轮补强，补齐首轮代码锚点、测试锚点、UI 降级路线、建议验证命令，并把相关技术纪律同步到 mainline `OMS_COPILOT.md`；无代码变更、无新增测试执行。
