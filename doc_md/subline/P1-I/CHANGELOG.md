# P1-I 变动日志

## 2026-06-16：谱面构成过滤"失效"根因修复 + Phase 2 backfill 性能/UX 全面收口

> 本日一整轮日志驱动调试（多次用户实测）。用户 2026-06-16 末确认"暂时正常、未发现异常"。最终落地合同见本条末尾 §E。06-15 的"回调竞态主因"判断在本条 §A 被推翻（仍是真实但次要缺陷，保留）。

### A. 真因 = backfill Phase 1 的 Realm 查询崩溃（用户 database.log 确诊）

- `BmsChartFilterStatsBackfill.Initialise` 的 Phase 1 用 `r.All<BeatmapInfo>().Where(b => b.Ruleset.ShortName == SHORT_NAME)` 直接在 Realm `IQueryable` 上做 **link-traversal** 比较，Realm LINQ provider 翻译不了，抛 `The left-hand side of the Equal operator must be a direct access to a persisted property` → 被 `catch` **静默吞掉** → 连锁：缓存恒空（`Cache size=0`）；`missingIds.Count==0` 触发早退 → **Phase 2 整体跳过**（旧库永不自动补算）；每轮匹配 `null-stats(fail-open)≈全量` → 过滤完全不收窄 = "失效"。
- 这解释了为何 06-15 的回调竞态/取值优先级修复"无效"——缓存从未被填过，那两处对"数据从未进缓存"是空操作。
- **修复**：抽 `EnumerateBmsBeatmaps(Realm)`，谓词改 `realm.All<BeatmapInfo>().AsEnumerable().Where(b => b.Ruleset.ShortName == SHORT_NAME)`——`.AsEnumerable()` 切到 LINQ-to-objects 避开 Realm 翻译（与 `BmsDifficultyTableManager` 既有生产路径一致）。
- **回归**：`BmsImportIntegrationTest.TestChartFilterStatsBackfillQueryDoesNotThrowAgainstRealm`（真实 `RealmAccess`+`BmsFolderImporter` 导入后调生产查询断言不抛、能读到 stats——mock 测不出此类 Realm-LINQ 崩溃）。

### B. 次因（06-15 已修，真实但缓存未填时对其空操作，保留）

- **刷新回调被竞态吞掉**：旧 `Initialise` 静态 guard `if (beatmapManager != null) return;` 让首个调用者独占 bootstrap；`BmsNoteDistributionGraph.load` 调 Initialise 不传 `onCacheUpdated`，`FilterControl` 传了"缓存填好后 `Scheduler.AddOnce(updateCriteria)` 重跑过滤"的回调。详情图先加载 → filter 回调永久丢弃。修复=订阅者列表(`cacheUpdatedCallbacks`)+bootstrap(`backfillStarted`)分离+`initLock`；迟到订阅者立即 invoke 一次（后台 `notifyCacheUpdated` 仅 backfill 期间 fan-out，结束不再触发，故迟到立即刷新是关键）。回归 `TestSceneBmsFilterControl.TestLateCacheSubscriberStillReceivesRefresh`。
- **`Matches` 取值优先级**：旧 `GetChartFilterStats() ?? GetCachedStats()` 先用不可靠 detached 快照；改 `GetCachedStats(ID) ?? GetChartFilterStats()`（缓存优先、快照兜底）。
- fail-open（缺 stats 不静默隐藏）与"`Matches` 生产路径不做按需重 backfill"红线不动。

### C. Phase 2 backfill 性能/UX 收口（多轮实测迭代，按发现顺序）

1. **re-filter 风暴（FPS 主凶）**：Phase 2 每补算 100 张 `notifyCacheUpdated()`（≈每 3.5s），每次触发 carousel 对全库 57k 完整 match+sort+group+Y 重排（单次 1–3.5s）→ update 线程 ~80% 在重过滤。修复=`notifyCacheUpdatedThrottled()` 时间节流 20s（CAS），阶段**完成**路径仍直接 notify。
2. **诊断日志冒成通知 toast**：`LogLevel.Important` 被 osu 通知系统冒成"类报错弹窗"。所有例行日志降 `Verbose`（写文件不弹窗），仅 Phase 1 **失败**保留 Important。
3. **轻量计数解码**（可证等价）：把补算从完整转谱换成只解码数 note。零发散论证——note 分类只依赖 `BmsObjectEvent.AutoPlay`(BGM→`BmsBgmEvent` 不计)+`BmsBeatmapConverter.IsScratchLane`，转换器把每个非 autoplay ObjectEvent/每个 LongNoteEvent **1:1** 变同分类 `BmsHitObject`/`BmsHoldNote`(`BmsHoldNote:BmsHitObject`)，故"解码后数事件"≡`FromBeatmap(完整转谱)`。实现=`IsScratchLane` 提 internal static 唯一真源 + `BmsImportedBeatmapFactory.DecodeChart`(只 `decoder.Decode`) + `ComputeFromDecodedChart`。回归 `TestLightweightChartFilterStatsMatchFullConversion`。**结果：大幅降 GC，但 Phase 2 速率仍 ~28 张/s（与全转谱完全相同）——决定性信号：瓶颈不在 convert。**
4. **真瓶颈 = `GetWorkingBeatmap` 全局锁**（加 per-item 计时埋点定位）：`computeStats` 仍逐张走 `BeatmapManager.GetWorkingBeatmap`，而 `WorkingBeatmapCache.GetWorkingBeatmap` 在**进程级 `lock(workingCache)`**（song-select/carousel UI 同抢）内 `Detach()`+（Debug）逐张 Realm 读 hash assert。57k 次猛敲→锁上串行化+阻塞 update 线程=速率钉死+UI 卡。修复=**Phase 2 绕过 GetWorkingBeatmap 直读 .bms**：`computeStatsDirect` 复刻 `WorkingBeatmapCache.createResourceProvider`（external→`new NativeStorage(FilesystemStoragePath)`；managed→`gameStorage.GetStorageForDirectory(FilesystemStoragePath)`）→`GetStream(Path)`→解码计数，无锁/无重复 detach/无 Debug Realm 读；失败回落 GetWorkingBeatmap。`Storage` 经 `OnSongSelectSetup` 注入。
5. **一次性任务的透明化 + 降载**（用户产品判断："要么流畅要么给进度提示"）：
   - **进度通知**：Phase 2 起 `ProgressNotification`（`正在分析 BMS 谱面构成… done/total`，完成转 `Completed`）；`INotificationOverlay` 经 `OnSongSelectSetup` 注入；`missingIds==0`（后续启动）不弹=零噪声。
   - **批量写回**：Phase 2 不再逐张 `realmAccess.Write`（~5万微事务），改算完入 `ConcurrentQueue`+每 `write_batch_size=200` 张一个事务 `flushPendingWrites`（`writeFlushLock` 串行 drain）+收尾 flush。~5万事务→几百个，降 realm 争用；in-mem `cachedStats` 仍即时更新（过滤即时生效），中途退出仅丢未 flush 一小批、下次重算。
   - Phase 2 直接走 `computeStats`（直读）+自管缓存/入队，不再经 `GetOrBackfill`（其 per-id 锁/二次校验对 Phase 2 不必要；`GetOrBackfill` 仍服务按需/测试路径）。

### D. 核心管线改动（osu.Game core）

- `Ruleset.OnSongSelectSetup(BeatmapManager, RealmAccess, Storage, INotificationOverlay, Action?)`——加 `Storage` + `INotificationOverlay` 两参（OMS 自有方法，非上游）。`FilterControl` / `BmsNoteDistributionGraph` 从 DI 取并传入。`Storage` 须 base data storage（与 BeatmapManager 同根，`chartbms/...` 解析才一致）。

### E. 当前合同（最终落地）

1. Phase 1 ruleset 枚举走 `EnumerateBmsBeatmaps`（`.AsEnumerable()` 内存求值，禁止 IQueryable 上比较 link 属性）；后台 catch 至少记日志。
2. `Matches` 取值 `GetCachedStats(ID) ?? GetChartFilterStats()`（缓存优先）；fail-open 不动；生产路径不碰 working-beatmap I/O。
3. 回调登记/bootstrap 分离，迟到订阅者立即 invoke。
4. Phase 2 补算=直读 .bms（`computeStatsDirect`，禁逐张 GetWorkingBeatmap）+ 轻量计数（`ComputeFromDecodedChart`，与完整转谱可证等价，`IsScratchLane` 唯一真源）+ 批量写回 + 进度通知。
5. 诊断埋点保留（全 `Verbose`/`LoggingTarget.Database`）：Phase 1 cached/missing、Phase 2 per-item 计时分桶（GetWorkingBeatmap/decode-count/realmRead/realmWrite + lightweight/fallback）、匹配抽样（5s 节流）。

**验证**：BMS 全量 `dotnet test osu.Game.Rulesets.Bms.Tests --no-restore` **910/910**；`osu.Desktop.slnf` Debug 0 error。用户 2026-06-16 实测确认正常（过滤生效 + 选歌可用 + 进度通知显示）。

## 2026-06-15

### P1-I：谱面构成过滤大曲库"失效"修复——回调竞态 + 取值优先级

> ⚠️ 本条当日判断的"主因 = 回调竞态"已被 **2026-06-16 §A 推翻**：真因是 Phase 1 的 Realm 查询崩溃（缓存从未填过）。本条两处修复（回调竞态 / 取值优先级）仍是**真实但次要**的缺陷，已保留。本条作为当日历史记录保留。

- 现象：大曲库（用户实测 ~5.8 万谱面）下设置 RC/LN/SCR 谱面构成过滤后看不到任何过滤效果。
- 链路审查结论：UI→query→`BmsFilterCriteria.TryParseCustomKeywordCriteria`→`BeatmapCarouselFilterMatching.Matches`→`BmsFilterCriteria.Matches` 整链结构完整、单测覆盖在；`ApplyVisualFilters` 已是死代码（视觉控件经 query 字符串走 parser）。失效来自两个真实缺陷叠加：
  1. **刷新回调被静默丢弃（主因）**：`BmsChartFilterStatsBackfill.Initialise` 旧实现用静态 guard `if (beatmapManager != null) return;`，**首个调用者独占一次性 bootstrap**。`BmsNoteDistributionGraph.load`（详情分布图）调用 `Initialise` 时**不传** `onCacheUpdated`，而 `FilterControl` 传了真正用于"缓存填好后重跑过滤"的回调。若详情图先加载，FilterControl 的回调被永久丢弃 → 后台 Phase 1 把 stats 装入缓存后过滤器**不会自动重跑** → 用户设了过滤却无反应。
  2. **`Matches` 取值优先级与注释相悖**：注释明确说 detached carousel 快照的 `RulesetDataJson` 可能 stale、应优先用预填充缓存，但代码 `GetChartFilterStats() ?? GetCachedStats()` 先用了不可靠快照。
- 修复：
  - `BmsChartFilterStatsBackfill`：改为**订阅者列表 + 一次性 bootstrap 分离**（`initLock` / `cacheUpdatedCallbacks` / `backfillStarted`）。每个 distinct 回调都登记，绝不被竞态吞掉；**bootstrap 已开始/已完成后再登记的迟到订阅者会被立即 invoke 一次**，让它拿到已填充的缓存（迟到订阅者是后台任务结束后唯一的刷新来源——后台 `notifyCacheUpdated` 仅在一次性 backfill 期间 fan-out）。`clearCache`（测试隔离）同步复位新状态。
  - `BmsFilterCriteria.Matches`：取值改为 `GetCachedStats(ID) ?? GetChartFilterStats()`，优先权威缓存、快照仅兜底，与注释意图一致。
  - 不动 fail-open 语义（缺 stats 不静默隐藏）与 `Matches` 生产路径不做按需重 backfill（红线：过滤循环不碰 working-beatmap I/O，避免 5.7 万级 UI 冻结）。
- 回归：`TestSceneBmsFilterControl.TestLateCacheSubscriberStillReceivesRefresh` 新增——在真实 headless song-select（含真实 BeatmapManager/RealmAccess）里，模拟迟到调用 `Initialise` 并断言回调被立即 invoke，把"回调不被吞"锁进契约。
- 验证：BMS 全量 `dotnet test osu.Game.Rulesets.Bms.Tests --no-restore` **908/908** 通过（含新增用例）。

## 2026-05-18

### P1-I：BMS 搜索语法公开口径改为 `rc / rice`

- `SearchHintTooltip` 的 BMS 段落已把 `rc / regular` 更正为 `rc / rice`，与社区常用术语保持一致；当前公开搜索口径统一为 `key/keys`、`rc/rice`、`ln`、`scr`。
- `BmsFilterCriteria` 已同步支持 `rice` 关键字；`regular` 继续只作为向后兼容 alias 保留，避免既有查询失效，但不再作为 tooltip 或文档里的公开写法。
- `BmsFilterCriteriaTest` 中与构成比例相关的 query 已切到 `rice>=...`，把这次语义口径直接锁进 focused parser/matcher regression。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter FullyQualifiedName~BmsFilterCriteriaTest` **4/4** 通过。

## 2026-05-13

### P1-I：UI 视觉收口——hover 效果、颜色重排、Tooltip DI 崩溃修复

- `BmsCompositionRowButton` 与 `BmsKeyCountToggleButton` 非激活态改为使用 `ColourProvider.Background3` / `Background1`，替换原来的 `Color4.Black.Opacity(0.35f/0.20f)`。`ShearedButton.updateState()` 内置的 `Lighten(0.2f)` hover 机制需要底色非黑才能产生可见色变，改动后鼠标悬浮效果与排序/分组/收藏夹下拉控件的行为一致。
- `BmsCompositionFilterControl` 颜色重排：RC 改为蓝 `(94,190,255)`、LN 改为黄 `(255,212,92)`、SCR 改为橙 `(255,119,86)`。`SearchHintTooltip` BMS 段落的强调色也同步更新为蓝色，与 RC 保持一致。
- `SearchHintTooltip` DI 崩溃修复：根因是 `[Resolved] OverlayColourProvider` 写在 tooltip class 内，但 tooltip 由全局 `OsuTooltipContainer` 在 global scene graph 层渲染，该层不包含 SongSelect 的 `OverlayColourProvider` DI 注册，导致依赖解析失败抛出 unhandled error。修复方案遵循 `ModTooltip` 的构造函数注入模式：在 `SongSelectSearchTextBox`（确在 SongSelect DI 作用域内）通过 `[Resolved]` 取得 `OverlayColourProvider`，然后在 `GetCustomTooltip()` 时通过构造函数参数传入 `SearchHintTooltip`，tooltip class 本身不再使用 `[Resolved]`。同时把 `createSection()` 与 `createBmsSection()` 内的 `GridContainer + AutoSizeAxes.Both + absolute column dimension` 布局替换为 `FillFlowContainer + Container(Width=160f)` 的稳定两列对齐方案。
- 配合以上视觉与依赖注入收口，`I3` 当前可视为已完成交付；剩余工作已收窄到 `I4` focused regression（单轨拖拽 headless regression + shared visual gate）。
- 验证：`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过，0 error。

## 2026-05-12

### P1-I：I3 交互收口——CompositionValueTextBox 拖拽修复与边界拖拽语义

- `CompositionValueTextBox`（数值编辑框）在可见状态下现在会从 `OnDragStart()` 返回 `false`，让用户点击段位区域打开数字输入后，近旁的句柄拖拽仍可正常冒泡到 `BmsCompositionHandle`；隐藏状态下完全不消费 positional input，避免截断拖拽起始事件。
- 句柄拖拽语义：当 RC/LN/SCR 三段当前总和恰好等于 100%（即尾段容差为零）时，向右拖拽共享边界会优先"消耗"右邻段的空间而不是拒绝拖拽；数值输入与外部 bindable 直接赋值的路径仍保持 clamp（不链式压缩后续段）。
- `BmsChartFilterStatsBackfill` 旧谱面 backfill 路径收口：先尝试 raw `WorkingBeatmap.Beatmap` 中的 `BmsHitObjects` 直接计算，若无可用对象则回落到 `GetPlayableBeatmap()`；避免 legacy library 因 raw 流中无 BMS note 而误被判定为空谱面后被错误过滤。

## 2026-05-12 / 2026-05-11

### P1-I：I3 主体落地——BmsCompositionFilterControl 单轨控件

- `BmsCompositionFilterControl` 以 BMS-local 私有单轨控件形式落地，替换原来的三条彼此独立的 range slider 原型：
  - 单轨从左到右固定为 `RC / LN / SCR` 三个可编辑上限段，尾段为空白容差。
  - 三段各自拥有独立 `Enabled` bindable；禁用某段时，该段不再从 visual UI 生成对应的 `rc<=` / `ln<=` / `scr<=` query fragment。
  - `BmsCompositionHandle` 句柄承载段间拖拽：`BmsCompositionHandle.GetTrackScreenSpacePosition()` 提供轨道内坐标映射，`handle_half_width = ShearedNub.EXPANDED_SIZE / 2f` 做端点内缩防止与邻近 UI 重叠；句柄上同步显示当前边界百分比数值文本。
  - `BmsCompositionRowButton` 基于 `ShearedToggleButton`：激活时使用段配色（Darken/Lighten 0.1f），非激活时使用 `ColourProvider.Background3/Background1`，确保 hover Lighten(0.2f) 可见。
  - `BmsKeyCountToggleButton` 提供 5K / 7K / 9K / 14K 独立启停，默认全部激活。
  - `RulesetFilterLabel` 以 `Background3` 填充背景，视觉重量与排序/分组/收藏夹下拉控件标签保持一致。
  - `SearchHintTooltip` 绑定到搜索框（通过 `IHasCustomTooltip<bool>`）：搜索框为空时显示，展示所有通用与 BMS 专属搜索语法。
- BMS 分支的 criteria 编译链 `createBmsVisualFilterQuery()` 已明确只在对应行 `Enabled == true` 时生成对应 query fragment，不再把 segment `UpperBound.IsDefault` 作为生效判断。
- BMS 分支的 `OnSongSelectSetup` / `BmsRuleset.OnSongSelectSetup` callback 已接通：`BmsChartFilterStatsBackfill` 在后台以 `Task.Run` 执行，每约 100 次计算通过 `onCacheUpdated` 触发 `Scheduler.AddOnce(() => updateCriteria())`。

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
