# P1-I 技术约束：BMS 选歌筛选与搜索定制

> 最后更新：2026-05-18
> 本文件记录 `P1-I` 的硬约束。若实现与本文冲突，先修正文档或代码其中一边，再继续开发。

## 归线约束

1. 本子线属于 Phase 1.x 下的 `P1-I`；主 authority 是 BMS Song Select 的筛选产品面、搜索语法与匹配语义，不得回写成 `P1-A` 或 `P1-H` 的主线任务。
2. `P1-A` 只承接 BMS-only UI 分支、切 ruleset 回退与共享产品面从属影响；`P1-H` 只承接 persisted read-model / backfill authority。二者都不得再各自长出第二套筛选语义。

## 产品面与语义约束

1. BMS-only UI 改动必须严格跟随当前 ruleset；切回 mania 或其他 ruleset 时，筛选区必须恢复现有 shared star slider 与原有 dropdown/product surface。
2. 共享 `DisplayStarsMinimum` / `DisplayStarsMaximum` 继续只服务非 BMS 的 star slider 语义；BMS 分支启用时，不得让隐藏 slider 的旧 state 继续影响 `criteria.UserStarDifficulty`。
3. 本专题替换的是 BMS 的 visual filter surface，不是删除 shared `star:` 文本语法；除非另开产品决策，不得顺手改掉 shared parser 的星数关键字。
4. `RC` / `LN` / `SCR` 必须是互斥分区且和为 `100%`；不得沿用 note distribution summary 那种可重叠计数。首轮固定采用：`SCR` 优先于 `LN`，`LN` 只统计非 scratch long note，`RC` 为剩余 playable objects。
5. `谱面构成` 的最终 UI 必须是一条单行、单轨的共享控件；从左到右固定为 `RC / LN / SCR` 三个可编辑段，尾段为空白容差。当前三条独立 `range slider` 原型不得作为最终产品面交付。
6. `RC` / `LN` / `SCR` 三个值都可调，且各自表示该分类的最大占比；visual UI 不承担精确配比或 min/max 双端范围语义。
7. `RC / LN / SCR` 的 visual 上限值不强制和为 `100%`；剩余尾段空白用于表达容差，而不是第四类真实谱面成分。
8. `RC / LN / SCR` 三个上限值之和不得超过 `100%`；若拖拽或数值输入会造成溢出，当前编辑值必须被夹紧或阻止，不能让尾段容差为负。
9. `RC` / `LN` / `SCR` 必须各自拥有独立 enabled state；禁用某段时，该段不再从 visual UI 生成对应的筛选 authority。
10. `谱面构成` 的可见交互继续冻结为按钮式表面：默认显示 `RC / LN / SCR` 标签、hover 可见当前占比、区域足够宽时在段内居中显示当前占比、点击段位可进入数值输入。
11. `键数` 的 authority 必须继续来自 BMS keymode / `Difficulty.CircleSize` 的同步字段；首轮只公开 `5K`、`7K`、`9K`、`14K` 四档，不扩到其他模式或别名。
12. visual filter 与 custom search 首轮只要求共享同一套 criteria 语义；除非明确追加设计，不得为了“搜索词与 UI 双向同步”扩大到重写整个 search text ownership。

## read-model 约束

1. RC/LN/SCR 过滤 authority 必须是同步可读的 persisted metadata ruleset-data；不得在 carousel 过滤阶段为每张候选谱面即时加载 playable beatmap 或重新跑 runtime analyzer。
2. 新 filter stats 必须在 import / rebuild / reuse 命中旧 set 的链路里一起写入；不得只在新导入谱面可用、旧谱面永远缺失。
3. 旧谱面缺失 filter stats 时，必须通过 backfill、重扫或等价路径补齐；在缺口未补齐前，不得 silently 把谱面错误过滤掉，也不得伪造默认 `0%` 参与匹配。
4. 若扩展 `BmsBeatmapMetadataData`，必须继续与当前 `chart_metadata` / `difficulty_table_entries` 共存；不得破坏既有 JSON 结构的向后兼容读取。
5. 新 filter stats 的 authority 必须是计数而不是预烘焙百分比；百分比默认由 `count / max(1, total_playable) * 100` 派生，避免双重 authority 漂移。
6. Song Select / matcher 读取新字段时应优先走 `BmsBeatmapMetadataData` 的 typed helper；除非在 core 兼容层别无选择，不得在 filter 链直接手写 `JObject.Parse(RulesetDataJson)`。

## 搜索语法约束

1. BMS-only custom keywords 必须通过 `IRulesetFilterCriteria` 接入，不得在 [../../osu.Game/Screens/Select/FilterQueryParser.cs](../../osu.Game/Screens/Select/FilterQueryParser.cs) 里新增 BMS-only switch 分支。
2. 首轮关键字只允许覆盖 `key` / `keys`、`rc` / `rice`、`ln`、`scr` 及极少数一一对应 alias；不得在没有明确产品定义前扩成自由别名集合。
3. `key` / `keys` 应尽量与 mania 的比较操作符语义一致；同名关键字在不同 ruleset 下允许各自解释，但不得改变 mania 现有行为。
4. `rc` / `rice` / `ln` / `scr` 必须按百分比范围语义实现；若 UI 首轮只暴露局部交互，文本语法仍必须完整保留范围匹配能力。
5. `rice` 是 `rc` 的公开长写；`regular` 只允许作为向后兼容 alias 留在 parser 内，不得再写进 tooltip、README 或 `P1-I` 文档口径。
6. 首轮 `FilterMayChangeFromMods()` 必须保持保守：在当前 BMS filters 不依赖 mods 的前提下返回 `false`，避免无意义的 mod-driven refilter 噪音。
7. `谱面构成` visual control 首轮只负责生成 enabled segment 的上限约束，即 `rc<=...` / `ln<=...` / `scr<=...` 这类 query fragment；最小值或更复杂组合仍由文本语法承担。
8. 不得为了贴合当前 visual control 的交互冻结点而削弱文本语法的范围能力；视觉控件可以只覆盖冻结过的编辑语义，再编译成等价 query 片段。

## 实现边界约束

1. 首轮继续在现有 shared `FilterControl` 中做 ruleset-aware row branching；不得为此新开一套 per-ruleset `FilterControl` host、更改 `SongSelect` 构造链，或引入高风险的 shared lifecycle 改造。
2. 不得为追求 UI 速度而把 RC/LN/SCR authority 偷塞到 `BmsNoteDistributionGraph` 的 runtime cache；Song Select 筛选与右侧详情面板必须共享同一份 persisted truth，而不是各算各的。
3. `谱面构成` 行必须继续维持单行 product footprint；不得通过新增大块展开面板破坏右上筛选区当前的 search / sort / group / collection 结构。
4. 任何改变 BMS Song Select 筛选语义的改动，都必须同步更新本目录四件套、`../../mainline/DEVELOPMENT_PLAN.md`、`../../mainline/DEVELOPMENT_STATUS.md` 与 `../../mainline/CHANGELOG.md`。
5. 首轮 UI 若需要新控件，优先接受 BMS-local 私有控件，而不是抢先抽象 shared generic segmented filter component；只有当第二个 ruleset 确认复用时，才值得上提共享层。
6. 若 shared 抽象提炼来不及，允许直接写 BMS-local 私有 segmented control；但不得再以三个彼此独立的 `ShearedRangeSlider` 拼排原型充当最终交付。

## 测试与发布约束

1. 至少补齐三层 focused coverage：metadata/importer、ruleset criteria/parser、Song Select UI / integration。
2. 规则切换回归必须显式锁定：BMS 显示 custom rows，mania 显示原有 star slider，双方 criteria 不串线。
3. 只有当 legacy beatmap backfill、BMS-only UI branch、custom search 语义与 Release 构建都完成后，才允许把该专题标记为已落地。
4. 首轮测试落点优先固定在 [../../osu.Game.Rulesets.Bms.Tests/BmsImportIntegrationTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsImportIntegrationTest.cs)、[../../osu.Game.Rulesets.Bms.Tests/TestSceneBmsSongSelectDifficultyTable.cs](../../osu.Game.Rulesets.Bms.Tests/TestSceneBmsSongSelectDifficultyTable.cs)、[../../osu.Game.Tests/Visual/SongSelect/TestSceneBeatmapFilterControl.cs](../../osu.Game.Tests/Visual/SongSelect/TestSceneBeatmapFilterControl.cs) 与 [../../osu.Game.Tests/Visual/SongSelect/TestSceneSongSelectFiltering.cs](../../osu.Game.Tests/Visual/SongSelect/TestSceneSongSelectFiltering.cs)；不要等到实现尾声再临时拼测试面。
