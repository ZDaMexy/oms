# P1-I 技术约束：BMS 选歌筛选与搜索定制

> 最后更新：2026-06-16
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

> 8–15 是 2026-06-16 那一轮"失效"根因 + 性能/UX 收口沉淀的 backfill 硬约束；过程见 [CHANGELOG.md](CHANGELOG.md) 2026-06-16。

### 持久化 authority

1. RC/LN/SCR 过滤 authority 必须是同步可读的 persisted metadata ruleset-data；不得在 carousel 过滤阶段为每张候选谱面即时加载 playable beatmap 或重新跑 runtime analyzer。
2. 新 filter stats 必须在 import / rebuild / reuse 命中旧 set 的链路里一起写入；不得只在新导入谱面可用、旧谱面永远缺失。
3. 旧谱面缺失 filter stats 时，必须通过 backfill、重扫或等价路径补齐；在缺口未补齐前，不得 silently 把谱面错误过滤掉，也不得伪造默认 `0%` 参与匹配。**禁止把"自动补齐"退化成"要求用户人工重新导入"**：Phase 1 读已持久化 stats、Phase 2 后台自动补算并写回 Realm 是产品合同的一部分。
4. 若扩展 `BmsBeatmapMetadataData`，必须继续与当前 `chart_metadata` / `difficulty_table_entries` 共存；不得破坏既有 JSON 结构的向后兼容读取。
5. 新 filter stats 的 authority 必须是计数而不是预烘焙百分比；百分比默认由 `count / max(1, total_playable) * 100` 派生，避免双重 authority 漂移。
6. Song Select / matcher 读取新字段时应优先走 `BmsBeatmapMetadataData` 的 typed helper；除非在 core 兼容层别无选择，不得在 filter 链直接手写 `JObject.Parse(RulesetDataJson)`。
7. **匹配取值优先级**：`BmsFilterCriteria.Matches` 必须 `GetCachedStats(ID) ?? GetChartFilterStats()` —— 先用 `BmsChartFilterStatsBackfill` 预填充的权威缓存，detached carousel 快照的 `RulesetDataJson` 仅作兜底（它可能 stale）。不得反过来让快照覆盖缓存。

### backfill 链路红线

8. **Realm 查询红线**：backfill 枚举 BMS 谱面**不得**在 Realm `IQueryable` 上对 link-traversal 属性（`b.Ruleset.ShortName`）做比较——Realm LINQ provider 翻译不了会抛 `left-hand side ... must be a direct access to a persisted property`。必须先 `.AsEnumerable()` 切到内存求值（统一走 `BmsChartFilterStatsBackfill.EnumerateBmsBeatmaps(Realm)`）。此异常曾被静默 catch 导致 Phase 1 整体失败 + Phase 2 跳过（`missingIds==0`）+ 缓存恒空，使过滤对全库 fail-open。回归 `BmsImportIntegrationTest.TestChartFilterStatsBackfillQueryDoesNotThrowAgainstRealm` 用真实 Realm 锁住——mock 数据测不出此类翻译崩溃。
9. **后台查询/补算的异常不得静默吞掉到无声失败**：`catch` 必须至少记日志（`LoggingTarget.Database`），否则像 #8 这样的链路级崩溃会表现为"功能整体失效但无任何报错"。
10. **一次性 bootstrap 与回调登记必须解耦**：`Initialise` 有多个调用点（`FilterControl` 传 `onCacheUpdated`，`BmsNoteDistributionGraph` 不传）。一次性 bootstrap 只能跑一次，但**每个 distinct `onCacheUpdated` 都必须被登记、绝不能被"首个调用者独占 guard"吞掉**；bootstrap 已开始/已完成后登记的迟到订阅者必须被**立即 invoke 一次**以拿到已填充缓存（后台 `notifyCacheUpdated` 仅在一次性 backfill 期间 fan-out，结束后不再触发）。回归 `TestSceneBmsFilterControl.TestLateCacheSubscriberStillReceivesRefresh`。
11. **cache-updated notify 必须节流**：每次 `notifyCacheUpdated()` 会让 song-select carousel 对全库做一次完整 match+sort+group+Y 重排（57k 量级单次 1–3s，跑在 update 线程）。Phase 1/2 的**周期性**刷新必须走 `notifyCacheUpdatedThrottled()`（当前 20s 一次），不得每 N 张就 notify（曾每 100 张 → 每 3.5s 重过滤 → update 线程 ~80% 占用 → 帧数骤降/卡死）。**阶段完成**路径仍直接 `notifyCacheUpdated()` 保证终态。
12. **后台 backfill 的诊断日志默认 `LogLevel.Verbose`**（写文件、不冒通知 toast）；仅"链路级失败"（如 #8 的 Phase 1 整体崩）允许 `Important`。例行进度/抽样用 Important 会被 osu 通知系统冒成"类报错弹窗"，污染用户体验。
13. **Phase 2 补算禁止逐张走 `BeatmapManager.GetWorkingBeatmap`**：`WorkingBeatmapCache.GetWorkingBeatmap` 在进程级 `lock (workingCache)` 内执行（song-select/carousel UI 同抢此锁）、`Detach()`、且 Debug 下逐张 Realm 读 hash。57k 次会在锁上串行化并阻塞 update 线程（实测速率被钉死 ~28 张/s、UI 卡顿，且与是否轻量 convert 无关）。必须**直读谱面文件夹的 .bms**（`computeStatsDirect`，复刻 `WorkingBeatmapCache.createResourceProvider`：external→`new NativeStorage(FilesystemStoragePath)`，managed→`gameStorage.GetStorageForDirectory(FilesystemStoragePath)`），仅在直读失败时回落 `GetWorkingBeatmap`。`Storage` 经 `Ruleset.OnSongSelectSetup(..., Storage, ...)` 注入，须与 `BeatmapManager` 同根（base data storage）否则 `chartbms/...` 解析失败。
14. **轻量计数解码必须与完整转谱的计数可证等价**：Phase 2 走 `ComputeFromDecodedChart`（`DecodeChart` 只解码、不转谱）。正确性依赖硬不变量——note 的 RC/LN/SCR 分类**只**由 `BmsObjectEvent.AutoPlay`（BGM 排除）与 `BmsBeatmapConverter.IsScratchLane(keymode, channel)` 决定，且转换器把每个非 autoplay ObjectEvent / LongNoteEvent **1:1** 变成带相同分类的 `BmsHitObject`/`BmsHoldNote`。**scratch 分类只能有一个真源 `IsScratchLane`，禁止在计数路径另写通道规则**。若将来转换器改变 note 来源/分类（新增非 1:1 的 HitObject 生成、或按 lane 过滤丢弃 note），必须同步轻量计数并更新等价回归 `TestLightweightChartFilterStatsMatchFullConversion`。
15. **一次性大批量 backfill 必须可见 + 低争用**：旧库首轮 Phase 2（~5万张）即使旁路 GetWorkingBeatmap 仍有可感负载。产品合同=要么流畅到不影响选歌、要么给**进度通知**。当前实现=Phase 2 起 `ProgressNotification`（done/total，完成转 Completed，`INotificationOverlay` 经 `OnSongSelectSetup` 注入，`missingIds==0` 不弹）+ Realm 写回**必须批量**（`flushPendingWrites`，每 `write_batch_size` 一个事务，`writeFlushLock` 串行），不得逐张开微事务争用游戏 realm。降本正道是直读旁路(#13)+轻量计数(#14)+批量写回，而非提高并行度或取消节流(#11)。

## 搜索语法约束

1. BMS-only custom keywords 必须通过 `IRulesetFilterCriteria` 接入，不得在 [../../osu.Game/Screens/Select/FilterQueryParser.cs](../../../osu.Game/Screens/Select/FilterQueryParser.cs) 里新增 BMS-only switch 分支。
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
4. 首轮测试落点优先固定在 [../../osu.Game.Rulesets.Bms.Tests/BmsImportIntegrationTest.cs](../../../osu.Game.Rulesets.Bms.Tests/BmsImportIntegrationTest.cs)、[../../osu.Game.Rulesets.Bms.Tests/TestSceneBmsSongSelectDifficultyTable.cs](../../../osu.Game.Rulesets.Bms.Tests/TestSceneBmsSongSelectDifficultyTable.cs)、[../../osu.Game.Tests/Visual/SongSelect/TestSceneBeatmapFilterControl.cs](../../../osu.Game.Tests/Visual/SongSelect/TestSceneBeatmapFilterControl.cs) 与 [../../osu.Game.Tests/Visual/SongSelect/TestSceneSongSelectFiltering.cs](../../../osu.Game.Tests/Visual/SongSelect/TestSceneSongSelectFiltering.cs)；不要等到实现尾声再临时拼测试面。
