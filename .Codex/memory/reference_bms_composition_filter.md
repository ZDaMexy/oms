---
name: reference_bms_composition_filter
description: "BMS song-select 谱面构成 (RC/LN/SCR) composition filter chain + backfill; the \"大曲库失效\" real root cause (Phase 1 Realm query crash) and the GetWorkingBeatmap-global-lock perf saga"
metadata: 
  node_type: memory
  type: reference
  originSessionId: 8870f65b-0709-4f8a-8517-998416dcf558
---

BMS song-select 谱面构成过滤（RC/LN/SCR 占比 + 键数 5/7/9/14K）链路与 backfill（P1-I）。2026-06-16 用户实测确认正常。

**链路（结构完整、单测覆盖在）**: UI `BmsCompositionFilterControl` rows + `BmsKeyCountToggleButton` → `FilterControl.createBmsVisualFilterQuery()` 编译成 query 串（`rc<=`/`ln<=`/`scr<=`/`keys=`，QueryKey="rc"/"ln"/"scr"）→ `FilterQueryParser.ApplyQueries` → `BmsFilterCriteria.TryParseCustomKeywordCriteria`（接受 rc/rice/regular、ln、scr/scratch、key/keys）→ `BeatmapCarouselFilterMatching.Matches` 调 `RulesetCriteria.Matches` → `BmsFilterCriteria.Matches`。`BmsFilterCriteria.ApplyVisualFilters` 是**死代码**（视觉控件全走 query 串）。

**stats 来源**: `BmsChartFilterStats`（regular/long/scratch 计数，百分比派生）存共享 `BeatmapMetadata.RulesetData` 列里的 `BmsBeatmapMetadataData.chart_filter_stats`（与 [[reference_bms_difficulty_table]] 的 difficulty-table、converted-star 同列，靠双边 `[JsonExtensionData]` 防互擦）。import 时 `BmsImportedBeatmapFactory` 写入（2026-05-11 起，故此前导入的旧库缺 stats）；详情面板 `BmsBeatmap.GetStatistics()` 懒算懒持久化；旧库靠 `BmsChartFilterStatsBackfill` 后台补齐。

**fail-open 设计（红线不动）**: `Matches` 中 `filterStats==null` → 该谱所有 composition 约束放行（缺 stats 不静默隐藏）；生产匹配路径**不**做按需 backfill，红线=过滤循环不碰 working-beatmap I/O（避免 5.7 万级 UI 冻结）。

## ★ "大曲库失效"根因（2026-06-16 用户 database.log 确诊）

= **backfill Phase 1 的 Realm 查询崩溃**。`Initialise` Phase 1 旧 `r.All<BeatmapInfo>().Where(b => b.Ruleset.ShortName == SHORT_NAME)` 在 Realm `IQueryable` 上比较 **link-traversal** 属性，Realm LINQ provider 翻译不了，抛 `The left-hand side of the Equal operator must be a direct access to a persisted property` → 被 `catch{}` **静默吞掉** → 连锁:(1) 缓存恒空 `Cache size=0`；(2) `missingIds.Count==0` → 早退把 **Phase 2 整体跳过**（旧库永不补算）；(3) 每轮匹配 `null-stats(fail-open)≈全量` → 过滤完全不收窄。修复 = `EnumerateBmsBeatmaps(Realm)` 用 `.AsEnumerable()` 把谓词切到内存求值（与 `BmsDifficultyTableManager` 一致；native `.Filter("Ruleset.ShortName == $0")` 亦可）。

**次因（06-15 已修，真实但缓存从未填过时对其空操作，保留）**: (a) **回调被竞态吞掉**——旧 `Initialise` 静态 guard `if(beatmapManager!=null) return;` 让首调用者独占 bootstrap；`BmsNoteDistributionGraph` 调 Initialise 不传 onCacheUpdated，`FilterControl` 传了"缓存填好后 `Scheduler.AddOnce(updateCriteria)` 重跑"的回调，详情图先加载→回调永久丢弃。修复=订阅者列表+bootstrap 分离+`initLock`，迟到订阅者立即 invoke 一次（后台 `notifyCacheUpdated` 仅 backfill 期间 fan-out）。(b) **取值优先级反了**——`Matches` 旧 `GetChartFilterStats() ?? GetCachedStats()` 先用不可靠 detached 快照；改 `GetCachedStats(ID) ?? GetChartFilterStats()`（缓存优先）。

## backfill 最终合同（2026-06-16 收口）

- **Phase 1**: `EnumerateBmsBeatmaps`(`.AsEnumerable()` 内存求值) 读已持久化 stats 填缓存。
- **Phase 2**(补算缺 stats 旧库) = **直读 .bms**(`computeStatsDirect`) + **轻量计数解码**(`ComputeFromDecodedChart`) + **批量写回** + **进度通知**:
  - 直读旁路:**禁逐张 `GetWorkingBeatmap`**——`WorkingBeatmapCache.GetWorkingBeatmap` 在**进程级 `lock(workingCache)`**(song-select/carousel UI 同抢)内 `Detach()`+Debug 逐张 Realm 读 hash assert，57k 次猛敲→锁上串行化+阻塞 update 线程=速率钉死 ~28张/s+UI 卡(且与轻量 convert 无关——速率纹丝不动是定位线索)。`computeStatsDirect` 复刻 `WorkingBeatmapCache.createResourceProvider`(external→`new NativeStorage(FilesystemStoragePath)`，managed→`gameStorage.GetStorageForDirectory(FilesystemStoragePath)`)→`GetStream(Path)`→解码计数；失败回落 GetWorkingBeatmap。
  - 轻量计数:`DecodeChart`(只 `decoder.Decode`)+`ComputeFromDecodedChart`(遍历 ObjectEvents 跳 AutoPlay + LongNoteEvents，用 `IsScratchLane` 分类)。**可证等价** `FromBeatmap(完整转谱)`:note 分类只依赖 `AutoPlay`(BGM→`BmsBgmEvent` 不计)+`BmsBeatmapConverter.IsScratchLane`，转换器把每个非 autoplay ObjectEvent/LongNoteEvent **1:1** 变同分类 `BmsHitObject`/`BmsHoldNote`(`BmsHoldNote:BmsHitObject`)。**`IsScratchLane` 唯一真源，禁在计数路径另写通道规则。**
  - 批量写回:不再逐张 `realmAccess.Write`(~5万微事务)，改入 `ConcurrentQueue`+每 200 张一个事务 `flushPendingWrites`(`writeFlushLock` 串行 drain)+收尾 flush；in-mem `cachedStats` 即时更新(过滤即时生效)，中途退出仅丢未 flush 一小批。
  - 进度通知:`ProgressNotification`(done/total，完成转 Completed)，`INotificationOverlay` 经 `OnSongSelectSetup` 注入，`missingIds==0` 不弹=零噪声。从后台 Task 线程更新——沿用 osu importer 既有安全模式。
  - notify 节流:周期性 `notifyCacheUpdatedThrottled()` 20s(CAS)，阶段完成仍直接 notify。
- **core 管线改动**: `Ruleset.OnSongSelectSetup(BeatmapManager, RealmAccess, Storage, INotificationOverlay, Action?)` 注入 Storage+notifications；`Storage` 须 base data storage 同根(否则 `chartbms/...` 解析失败)。
- **诊断埋点**(保留，全 `Verbose`/`LoggingTarget.Database`，仅 Phase 1 失败 Important): Phase 1 cached/missing、Phase 2 per-item 计时分桶(GetWorkingBeatmap/decode-count/realmRead/realmWrite + lightweight/fallback)、匹配抽样(5s 节流)。看 `D:\oms\data\logs\*.database.log` grep `[BmsCompositionFilter]`。
- **自动化语义**: 无需人工重导——跨会话持久、增量刷新；旧库首轮 Phase 2 补齐量大、渐进收敛、有进度通知、补齐一次后永久。

## ★ "每次启动都重算一次构成、没有持久化"根因（2026-06-18 其三，已修）

= **空/不可算谱面没有"已处理"负缓存**。backfill 只在算出**非空** stats 时写回（`sanitise` 把空结果折叠成 `null`、`SetChartFilterStats(null)` 不写盘）。于是计算结果为空/不可用的谱面——genuinely 空谱（只有 BGM/autoplay channel-01、0 playable）、或 `computeStatsDirect` 直读 + `GetWorkingBeatmap` 回落**双双失败**——`ChartFilterStats` 恒 `null` 且**无任何已处理痕迹**，每次启动 Phase 1 重新算进 `missingIds` → Phase 2 重算 → "正在分析 BMS 谱面构成"通知每启动一冒。可算谱面首轮已正确写回并跳过，这批"空/失败"谱构成**永不收敛的重算子集**。**排除过的方向（都不是因）**：写回失败（`RealmAccess.Write` 后台线程取 thread-local realm 提交正常）；共享 `RulesetData` 列被 converted_star 并发 clobber（Realm 写事务串行 + 写内读 live 值 → JsonExtensionData 顺序保留有效，不互擦）；`Detach()` 丢 `BeatmapSet`（mapper MaxDepth(2) 带 BeatmapSet，直读路径对多数谱可用）。

**修复 = 持久化负缓存标记**：`BmsBeatmapMetadataData.ChartFilterStatsResolved`（`[JsonProperty("chart_filter_stats_resolved")]`，同列、`IsEmpty` **必须**计入它否则只有标记的 data 被当空 null 掉、`[JsonExtensionData]` 仍兼容 converted_star）+ `ResolveChartFilterStats(stats)`（非空存 stats、空只置标记，都标记已处理）+ `GetChartFilterStatsState()`（单次反序列化取 `(Stats, Resolved)`，`Resolved = ChartFilterStatsResolved || ChartFilterStats!=null`）。Phase 1 用 state 归类（有 stats 缓存 / 无 stats 且 `!Resolved` missing / 无 stats 但 `Resolved` **跳过**）；Phase 2 对每张已 Detach 谱入队（stats 可 `null`）、`flushPendingWrites` 走 `ResolveChartFilterStats`（空也落标记）；import(`BmsImportedBeatmapFactory`)/reuse(`BmsFolderImporter.syncChartFilterStats`)/`GetOrBackfill` 一律改走 `ResolveChartFilterStats`（`GetOrBackfill` 加 `Resolved` 早退）。`SetChartFilterStats`（纯 setter）只留给测试 + `BmsBeatmap.GetStatistics` 的**内存**显示路径。空谱在 `Matches` 仍按 `null` **fail-open**（标记只阻止重算、不隐藏谱、不参与过滤判定）。写回 `catch{}` 改记 `Important` 日志。沉淀为 P1-I CONSTRAINTS #16。

## 通用教训

1. **Realm 查询禁止在 `IQueryable` 上比较 link-traversal 属性**(`b.Ruleset.ShortName`)——会抛 translate 异常；先 `.AsEnumerable()` 切 LINQ-to-objects。
2. **后台 catch 必须记日志**——否则链路级崩溃表现为"功能整体失效但无任何报错"。
3. **大批量后台处理禁止逐张走 `GetWorkingBeatmap`**——它有进程级全局锁会阻塞 UI；要批处理就直读文件。
4. **诊断日志用 `Verbose`**——`Important` 会被 osu 通知系统冒成"类报错弹窗"。

**回归**: `TestChartFilterStatsBackfillQueryDoesNotThrowAgainstRealm`(真实 Realm+importer 锁 Phase 1 查询不崩，mock 测不出) + `TestLightweightChartFilterStatsMatchFullConversion`(轻量计数 ≡ 完整转谱) + `TestSceneBmsFilterControl.TestLateCacheSubscriberStillReceivesRefresh`(回调不被吞) + `BmsChartFilterStatsBackfillTest` resolved-marker 四条(空结果持久化 round-trip / 未处理读回未 resolved / 有 stats 持久化 / 与 converted_star 共存)。BMS 全量 **922/922**（2026-06-18 其三）；`osu.Game.Tests` converted-star 17/17。P1-I CONSTRAINTS read-model #8–#16（详见 doc_md/subline/P1-I）。

**实机验收通过（2026-06-18 其四）**：用户连跑两次启动——第 1 次仍跑一次补算（旧库一次性 Phase 2），第 2 次启动日志 `Phase 1 done: 57685 beatmaps already have persisted ..., 0 are missing`，补算/通知不再复发 → "每次启动重算"已收敛为"补齐一次后永久持久化"。注：该用户库构成全可算，普通非空 stats 写回即足以让第二次 0 missing；resolved 负缓存标记保障的是"空/不可算"子集（该库恰无此子集，标记逻辑正确但本次未被触发）。
