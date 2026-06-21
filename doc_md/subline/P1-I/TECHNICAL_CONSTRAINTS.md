# P1-I 技术约束：BMS 选歌筛选与搜索定制

> 最后更新：2026-06-21（「展示层级与层级导航约束」追加 #17 root-focus 放弃 / #18 DrawSize re-center 收口）
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

16. **"已处理"必须持久化，即使计算结果为空——否则 backfill 永不收敛**：`missingIds`＝`GetChartFilterStatsState().Resolved` 为假的谱面。**红线＝Phase 2 处理过一张谱面后，无论是否产出可用 stats，都必须写回一个持久标记**，否则计算结果为空/不可用（空谱＝只有 BGM/autoplay 的 0 playable 谱、或直读+回落双双失败）的那一批谱面，会因为 `ChartFilterStats` 恒为 `null` 而**每次启动都被重新归类为 missing 并重算**——用户表现＝"每次新启动进 BMS 选歌总会跑一次谱面构成计算、没有持久化"。实现＝`BmsBeatmapMetadataData.ChartFilterStatsResolved`（与 `chart_filter_stats` 同列、走同一 `[JsonExtensionData]` 兼容 converted_star）+ `ResolveChartFilterStats(stats)`（stats 非空＝存 stats，空＝只置 resolved 标记；二者都标记 `Resolved`）。`SetChartFilterStats`（纯 setter）只保留给测试与 `BmsBeatmap.GetStatistics` 的**内存**显示路径；**import / reuse / Phase 2 / GetOrBackfill 一律走 `ResolveChartFilterStats`**。空谱在 `Matches` 仍按 `null` fail-open（不隐藏谱面），标记只阻止重算、不参与过滤判定。`IsEmpty` 必须计入 `ChartFilterStatsResolved`（否则只有标记的 data 会被当空对象 null 掉、标记丢失）。回归 `BmsChartFilterStatsBackfillTest` 的 resolved-marker 四条（空结果持久化 / 未处理读回未 resolved / 有 stats 持久化 / 与 converted_star 共存）。

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

## 展示层级与层级导航约束

> 本节服务 I5–I7（展示层级 / 层级返回条 / 分组性能）。三项已于 2026-06-16 落地、2026-06-18 经用户实机反馈再修两轮（层级展开态/缩进 #15、非 BMS 进入误展开 #16）；本节为已落地实现的硬约束，实现与本节冲突时先改一边再继续。

### 展示层级（I5）

1. **首轮 BMS-only**：mania / 其他 ruleset 维持 `BeatmapCarouselFilterGrouping.ShouldGroupBeatmapsTogether` 现有启发式，BMS 的显式开关不得改变非 BMS 的默认布局。展示层级下拉是共享 sort/group/collection 行里「分组」与「收藏夹」之间的 BMS-only 第 4 列（`FilterControl.createSortGroupColumns`），非 BMS 时该列与其 gap 收 0 宽、下拉 `Alpha=0`，行布局与原先一致——不得为它新增一套独立行或让它在非 BMS 下占布局 authority。
2. **单一收口点**：两档「歌曲→谱面」/「谱面」必须经新增 `FilterCriteria.DisplayLevel`（nullable）→ `ShouldGroupBeatmapsTogether` → `BeatmapSetsGroupedTogether` 一处决定；`DisplayLevel==null` 即走旧启发式（mania 零行为变化）。scope、随机（set vs beatmap）、面板池化（`PanelBeatmap` vs `PanelBeatmapStandalone`）、间距逻辑都只能读 `BeatmapSetsGroupedTogether`，不得各自再判一次展示形态。
3. **强制扁平时锁定**：当 `BeatmapCarouselFilterGrouping.GroupingForcesStandaloneDifficulties` 为真——即层级分组（难度表 / 内外库）、`Group==Difficulty`、`Sort==Difficulty`、`Group==RankAchieved` 或 `Sort==LastPlayed && Group==LastPlayed`——展示层级必须锁定为「谱面」并禁用下拉（这些分组下同一 set 的不同难度会被拆进不同组，折叠形态无意义）。锁定期间**不得污染持久化偏好**：`FilterControl` 把 `displayLevelSetting`（config 持久偏好）与下拉 `Current`（显示值）分离，用 `suppressDisplayLevelSettingWrite` guard 实现"强制显示 Difficulties、离开后还原用户偏好"。`criteria.DisplayLevel` 始终传用户偏好（BMS）或 `null`（其他 ruleset）；真正的扁平判定由 `ShouldGroupBeatmapsTogether` 的 `GroupingForcesStandaloneDifficulties` 一处收口，UI 锁定仅为反馈。
4. **持久化隔离**：用新的 BMS-local `OsuSetting` 持久化展示层级，不得复用或污染 `SongSelectGroupMode` / `SongSelectSortingMode`。
5. 不得为展示层级新开 per-ruleset `FilterControl` host（沿用实现边界约束 #1）。

### 层级返回条（I6）

6. **状态独立**：层级返回条必须由 carousel 的 `ExpandedGroup`（当前展开组路径）驱动，**独立于 `ScopedBeatmapSet`**。严禁把"层级退回"塞进 scoped-set 状态链——scope = 绕过筛选展开某个 set；退层 = 折叠分组路径，两者语义不同，缠在一起后续必出歧义。
7. **可见条件**：仅在层级分组激活且 `ExpandedGroup` 存在可退父级（depth > 0）时显示返回条；root 层隐藏。
8. **退层原语复用 + cursor 停在被折叠组**：返回动作 = `setExpandedGroup(ExpandedGroup.Parent)`，随后键盘 cursor 落在**刚折叠的那个组**（折叠态、不重新展开）并滚入视野，**而非其父组**——让用户逐级上退（`satellite/★7` → cursor 停 `★7` → 再退 cursor 停 `satellite`）。复用现有 `setExpandedGroup` / `ChangeKeyboardSelection` / `ScrollToSelection`，不得新造第二套展开 / 折叠状态机。
9. **Back 键优先级**：`GlobalAction.Back` 必须按 scoped-set 退出 > 层级退回 > 退出 song-select 排序，三者不得互相吞键。
10. banner 文案若改，顺手把 `SongSelectStrings.TemporarilyShowingAllBeatmapsIn` 的 BMS 口径从"谱面"消歧为"歌曲 / 谱面集"；该改动只动 display string，不得触碰存储 `Title` / 排序 / 搜索 / MD5。

### 分组性能（I7）

11. **已实现解析缓存（2026-06-16）**：`BmsTableGroupMode` 按 `RulesetDataJson` 内容键缓存计算好的 `GroupDefinition[]`，消除每次 refilter × 每张谱面的全量 `JsonConvert.DeserializeObject`（`GetDifficultyTableEntries` → `BeatmapMetadata.GetRulesetData<T>`，BeatmapMetadata.cs:86）。该优化 correctness-neutral（纯函数 + `GroupDefinition` immutable record，结果可跨谱面共享），由 `BmsTableGroupModeTest` 的"缓存复用 / stale-proof"两条回归锁住。大库（5 万+）的实际收益量化仍建议用户在真实库用 perf log 确认（headless 无大库）。
12. **解析缓存边界**：若加缓存，必须按 `RulesetDataJson` 内容键（stale-proof）且有界（避免 5 万+ distinct JSON 无界增长，库变更 / criteria 代际变化时清理）；不得改 `BeatmapMetadata.GetRulesetData<T>` 的 shared 语义、不得让缓存跨 ruleset 漏读。
13. 不得为分组性能在过滤阶段逐张 `GetWorkingBeatmap` 或重跑 analyzer（沿用 read-model 约束 #1 / #13）；分组结果优化前后必须逐谱一致。

### 层级分组视觉（I6 配套）

14. **层级深度的视觉区分必须经 `group.Depth`、且非层级分组零影响**：`PanelGroup.PrepareForUse` 按 `Depth` 多线索分级——根/表名组（depth 0）= `Background4` + 三角纹理可见 + `Heading2` + `Content1`；嵌套/等级组（depth≥1）= `Background6` + 三角纹理 `Alpha=0`（纯平）+ `Body SemiBold` + `Content2` + `Depth*24` 缩进。**单靠相邻 `Background` shade（5↔6）实机不可辨——必须多线索叠加（亮度跨档 + 纹理有无 + 字号 + 字色 + 缩进）才达到"对比明显"。** 因为 mania 等非层级分组所有组都是 depth 0，这条只影响 BMS 难度表/内外库这类多层分组；不得改成按 ruleset / group-mode 硬判定（破坏共享 `PanelGroup` 通用性）。`PanelXOffset` 是 `init`-only、池化面板不可逐项改，故用 shade/纹理/字号而非整面板缩进。

15. **层级展开态与缩进方向（2026-06-18 修正）**：① 路径**根组**的 `IsExpanded` 不被 `setExpansionStateOfGroup`（只设子组）覆盖，必须在 `setExpandedGroup` 显式置位/复位（`setGroupItemExpansion`），否则表名层即便展开也无 chevron、突出量错乱。② 突出（X 偏移）必须让浅层（祖先）≥ 深层（后代）：`Panel.AdditionalXOffset`（`PanelGroup` 返回 `Depth*30`）须 **> `active_x_offset`(25)** 以压过键盘选中偏移，保证展开的祖先组突出不少于其键盘选中的后代组（避免子组比父组更突出）。`AdditionalXOffset` 默认 0、只 `PanelGroup` override，非层级分组（depth 0）零影响。

16. **fresh-entry root-focus 抑制必须覆盖 invalid 当前谱面（2026-06-18 修正）**：`SongSelect.ensureGlobalBeatmapValid` 中 `shouldSuppressGroupedAutoSelection()`（`pendingRootGroupFocus && CurrentGroupedBeatmap==null`）必须在 valid/invalid 分支**之前**短路返回，否则当前全局谱面对本 ruleset invalid 时（如 mania 曲目播放中直接进入 / 切到 BMS）会经 invalid 回退（`SetDefault→IsDefault→NextRandom`）自动选中并 `setExpandedGroup` 展开某分组（误展开 Unrated）。抑制只在 `ShouldResetSongSelectGroupToRoot` 为真的 ruleset（BMS）的 fresh-entry 期间生效；其他 ruleset `pendingRootGroupFocus` 永不置真、零影响。回归 `TestNonBmsPlayingBeatmapDoesNotExpandGroupOnEntry`。

17. **fresh-entry root-focus 必须在出现具体选中后放弃（2026-06-21 修正，与 #16 对称）**：`SongSelect.tryFocusRootGroupForCurrentBeatmap` 必须在 `carousel.CurrentGroupedBeatmap != null` 时清掉 `pendingRootGroupFocus` 并直接返回，**不得**再执行 `FocusRootGroupForBeatmap`。否则当前全局谱面对本 ruleset invalid（mania 播放中进 BMS）时 root-focus 永远满足不了、标记长挂，等用户**手动选中第一张 BMS 谱**、`updateVariousState` 因全局 `Beatmap` 变更再次调用本方法时，会把视图劫持滚到该谱的最外层（表名）分组。语义＝root focus 只在「fresh-entry 且尚无具体选中」期间有效，用户一旦选谱即作废。#16（抑制 invalid 期间自动选中）与 #17（放弃 invalid 永不满足的延迟聚焦）是同一 `pendingRootGroupFocus` 生命周期的两个互补收口点，改其一须复核另一。回归 `TestSelectingChartWhileNonBmsPlayingDoesNotJumpToRootGroup`。

18. **DrawSize re-center 必须限定在存在具体选中时（2026-06-21 修正，shared base、mania-safe）**：base `Carousel.OnInvalidate` 的 `Invalidation.DrawSize` 分支只在 `currentSelection.CarouselItem != null` 时才 `selectionValid.Invalidate()`。该分支的语义是「窗口尺寸/纵横比变化时保持**选中项**居中」；窗口最小化/还原同样改变 `DrawSize`，若无条件重跑选中滚动，而此时没有具体选中（BMS 难度表里只展开了组、键盘光标停在组头、`currentSelection` 为 null），`BeatmapCarousel.GetScrollTarget` 的回退（键盘光标 / `ExpandedGroup` 位置）会把用户自由滚走的视图猛拽回组头。**不得**改成无条件 re-center，也**不得**改成读 `currentKeyboardSelection`（键盘光标可能就是组头、正是要避开的目标）——必须用已提交的 `currentSelection`。mania 安全性来自其选歌恒有已提交选中（`ShouldActivateOnKeyboardSelection` 使方向键浏览即 `Activate` 提交），故 `currentSelection.CarouselItem` 始终非空、resize 行为零变化；「键盘光标可停在组头而不提交选中」是 BMS 层级分组特性，也是本 bug 的成因边界。回归 `TestDrawSizeChangeDoesNotRecentreWithoutSelection` ＋守护 `TestDrawSizeChangeRecentresCommittedSelection`（osu.Game.Tests，generic carousel）。

## 测试与发布约束

1. 至少补齐三层 focused coverage：metadata/importer、ruleset criteria/parser、Song Select UI / integration。
2. 规则切换回归必须显式锁定：BMS 显示 custom rows，mania 显示原有 star slider，双方 criteria 不串线。
3. 只有当 legacy beatmap backfill、BMS-only UI branch、custom search 语义与 Release 构建都完成后，才允许把该专题标记为已落地。
4. 首轮测试落点优先固定在 [../../osu.Game.Rulesets.Bms.Tests/BmsImportIntegrationTest.cs](../../../osu.Game.Rulesets.Bms.Tests/BmsImportIntegrationTest.cs)、[../../osu.Game.Rulesets.Bms.Tests/TestSceneBmsSongSelectDifficultyTable.cs](../../../osu.Game.Rulesets.Bms.Tests/TestSceneBmsSongSelectDifficultyTable.cs)、[../../osu.Game.Tests/Visual/SongSelect/TestSceneBeatmapFilterControl.cs](../../../osu.Game.Tests/Visual/SongSelect/TestSceneBeatmapFilterControl.cs) 与 [../../osu.Game.Tests/Visual/SongSelect/TestSceneSongSelectFiltering.cs](../../../osu.Game.Tests/Visual/SongSelect/TestSceneSongSelectFiltering.cs)；不要等到实现尾声再临时拼测试面。
