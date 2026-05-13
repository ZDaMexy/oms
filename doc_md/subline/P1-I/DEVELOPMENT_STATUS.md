# P1-I 开发进度：BMS 选歌筛选与搜索定制

> 最后更新：2026-05-13
> 主线全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。本文件只记录 `P1-I` 的真实进展。

## 当前阶段

- **阶段定位**：`I1` / `I2` / `I3` 均已完成落地；`I4` 回归收口仍处于进行中（已有基础 focused tests，但 `BmsCompositionFilterControl` 单轨拖拽的 headless regression 覆盖与 shared visual gate 仍待补强）。
- **代码状态**：BMS 当前已具备 persisted `ChartFilterStats` metadata、`BmsFilterCriteria`、`BmsRuleset.CreateRulesetFilterCriteria()` 与 shared `FilterControl` 内完整的 BMS-only filter surface。`BmsCompositionFilterControl` 已以 BMS-local 私有单轨控件落地：`RC / LN / SCR` 三段可独立启停、各自表示最大占比、尾段为空白容差；`BmsCompositionHandle` 拖拽句柄可在三段边界间拖拽、并在句柄上显示当前数值；`BmsCompositionRowButton` 基于 `ShearedToggleButton`、激活时用段配色、非激活时用 `ColourProvider.Background3/Background1`（hover 效果可见）；`BmsKeyCountToggleButton` 提供 5K/7K/9K/14K 独立启停。`SearchHintTooltip` 已作为搜索框悬浮提示接入，展示全部 BMS 搜索语法；`OverlayColourProvider` 通过构造函数传递，不依赖 global tooltip-layer DI scope。颜色方案：RC=蓝(94,190,255)、LN=黄(255,212,92)、SCR=橙(255,119,86)。
- **文档状态**：`P1-I` 四件套已更新到当前落地状态。

## 已确认事实


- BMS-only 搜索 / 筛选定制已落在 shared `FilterControl` 内的 ruleset-aware branching，未为此新开 per-ruleset host。
- BMS custom search 沿 mania 现有模式走 `IRulesetFilterCriteria`；shared parser 没有新增 BMS-only switch。
- `RC / LN / SCR` 三段已采用互斥分区：SCR 优先，LN 为非 scratch long note，RC 为剩余。
- `BmsCompositionFilterControl` 为 BMS-local 私有单轨控件，符合"单轨上限段 + 尾段空白容差 + 独立启停"产品合同。
- `SearchHintTooltip` crash 修复：根因是 `[Resolved] OverlayColourProvider` 在 global tooltip-layer 不在 DI 作用域；遵循 `ModTooltip` 构造函数注入模式，同时把 `GridContainer + AutoSizeAxes.Both` 布局替换为 `FillFlowContainer + Container(Width=160f)`。
- `CompositionValueTextBox` 可见时返回 `false` from `OnDragStart()`，让近旁句柄拖拽事件可以正常冒泡；隐藏状态下不消费 positional input。
- 当 RC/LN/SCR 恰好填满 100% 时，向右拖拽共享边界优先消耗尾段容差，然后才压缩相邻右段；数值输入与外部 bindable 赋值仍走 clamp。
- `ShearedButton.updateState()` 的 hover `Lighten(0.2f)` 要求底色非黑；`BmsCompositionRowButton` / `BmsKeyCountToggleButton` 非激活态已改用 `ColourProvider.Background3/Background1`。
- RC=蓝(94,190,255)、LN=黄(255,212,92)、SCR=橙(255,119,86) 已是冻结配色；tooltip BMS 强调色已同步改为蓝色匹配 RC。
- `BmsChartFilterStatsBackfill` 以 `Task.Run` 异步后台执行，每约 100 次计算通过 callback 触发 `Scheduler.AddOnce(() => updateCriteria())`。
- 旧 BMS 谱面 backfill 路径：先尝试 raw `WorkingBeatmap.Beatmap`，若无可用 `BmsHitObjects` 则回落到 `GetPlayableBeatmap()`。
- BMS 分支已显式避免把隐藏的 star slider state 写入 `UserStarDifficulty`，不产生幽灵星数过滤。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 子线归线与四件套建档 | 已完成 | `P1-I` 已正式建立 |
| RC/LN/SCR read-model 建模 | 已完成 | `BmsBeatmapMetadataData.ChartFilterStats` + importer/reuse 自愈已落地 |
| BMS ruleset criteria / custom search | 已完成 | `BmsFilterCriteria` + `BmsRuleset.CreateRulesetFilterCriteria()` 已接通 |
| BMS-only FilterControl UI branch | 已完成 | `BmsCompositionFilterControl` 单轨控件已落地；`键数` row + `SearchHintTooltip` 均已完成 |
| 颜色方案与 hover 效果 | 已完成 | RC=蓝/LN=黄/SCR=橙，非激活 Background3/Background1，hover 效果可见 |
| `SearchHintTooltip` DI 崩溃修复 | 已完成 | 构造函数注入，GridContainer 替换为 FillFlowContainer+Container |
| focused regression | 进行中 | BMS importer/criteria/UI 重点切片已落地；单轨拖拽 headless regression 与 shared visual gate 待补强 |

## 当前风险

- **无解组合风险**：三个值各自表示最大占比；若用户把三者都压得过低，筛选结果允许为空，不能额外发明补偿语义。
- **范围语义落差风险**：文本 `rc/ln/scr` 保留完整范围语法；不得以贴合当前 visual 交互为由削弱文本语法能力。
- **拖拽回归缺口**：`BmsCompositionHandle` 共享边界拖拽语义尚无 headless automated coverage；在补测之前只依赖 visual test runner 验证。

## 下一检查点

1. 为 `BmsCompositionFilterControl` 单轨拖拽语义补 headless 断言覆盖（边界拖拽、填满 100% 时尾段优先压缩）。
2. 评估是否需要补充 shared visual gate（`TestSceneBeatmapFilterControl` BMS branch）。
3. 评估是否需要追加 one-shot legacy backfill 工具链，而不是在启动路径硬塞全库扫描。

## 验证记录

- 2026-05-11：`dotnet test osu.Game.Rulesets.Bms.Tests -p:GenerateFullPaths=true --filter "FullyQualifiedName~BmsImportIntegrationTest|FullyQualifiedName~BmsBeatmapStatisticsTest|FullyQualifiedName~BmsFilterCriteriaTest|FullyQualifiedName~TestSceneBmsFilterControl"` **30/30** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。
- 2026-05-13：`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` **0 error**；本轮修复：RC/LN/SCR/5K/7K/9K/14K 按钮 hover 效果（Background3/Background1）、颜色重排（RC=蓝/LN=黄/SCR=橙）、`SearchHintTooltip` DI 崩溃修复（构造函数注入 + FillFlowContainer+Container 布局）。
