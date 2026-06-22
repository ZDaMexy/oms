# P1-I 开发进度：BMS 选歌筛选与搜索定制

> 最后更新：2026-06-22
> 主线全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。本文件只记录 `P1-I` 的真实进展。

## 当前阶段

- **阶段定位**：`I1` / `I2` / `I3` 均已完成落地；`I4` 回归收口仍处于进行中（已有基础 focused tests，但 `BmsCompositionFilterControl` 单轨拖拽的 headless regression 覆盖与 shared visual gate 仍待补强）。`I5`–`I7`（展示层级 / 层级返回条 / 难度表分组解析缓存）已于 2026-06-16 落地并通过 focused 回归（详见 [CHANGELOG.md](CHANGELOG.md) 2026-06-16「其二」）。
- **I5–I7 当前态**：① 展示层级 = BMS-only 两档下拉「歌曲→谱面 / 谱面」，位于共享 sort/group/collection 行里「分组」与「收藏夹」之间的第 4 列（非 BMS 收 0 宽），经 `FilterCriteria.DisplayLevel` 收口到 `ShouldGroupBeatmapsTogether`；强制扁平分组（层级/Difficulty/RankAchieved/LastPlayed×LastPlayed）下锁定为「谱面」且不污染持久偏好；mania 零行为变化。② 层级返回条 = `FilterControl.GroupNavigationDisplay`（只显示面包屑路径 + 返回，无前缀），由 `BeatmapCarousel.CurrentExpandedGroup` 驱动、独立于 scoped-set，返回/Back 调 `CollapseExpandedGroupOneLevel` 逐级上退、cursor 停在刚折叠的组（不重新展开）。③ 分组解析缓存 = `BmsTableGroupMode` 按 `RulesetDataJson` 键缓存 `GroupDefinition[]`。④ 层级视觉区分 = `PanelGroup` 按 `Depth` 高对比分级（根=Background4+纹理+大亮标题；嵌套=Background6+纯平无纹理+小暗标题+缩进）。⑤（2026-06-18 实机修正）表名根组展开态修正 = `setExpandedGroup` 显式置路径根组 `IsExpanded`（原本只有子组被父组置位，根组永不置位 → 展开的表名组无 chevron、突出量错乱）；突出方向修正 = `Panel.AdditionalXOffset`（`PanelGroup`=`Depth*30`>25）让祖先组突出 ≥ 键盘选中的后代组。⑥（2026-06-21 实机修正）修两处 carousel「自动跳滑」——选谱 root-jump（`pendingRootGroupFocus` 在 mania 播放中进 BMS 时永不满足、长挂被首个选谱劫持 → 出现具体选中即放弃，约束 #17，与 #16 对称）+ 窗口还原 group-jump（最小化/还原改 `DrawSize` → base `Carousel.OnInvalidate` 无选中也 re-center 回退组头 → 限定 `currentSelection.CarouselItem != null` 才居中，shared base、mania-safe，约束 #18），**用户实机验收通过**。**剩余人工视觉验收（下拉锁定观感、返回条面包屑/Back 键、层级配色、展开箭头/突出层次、大库 perf 收益量化）待用户实机确认。**
- **选歌右键定位（2026-06-22）**：选歌右键菜单新增「打开歌曲文件位置」（歌曲条 `PanelBeatmapSet` / 单难度合并条 `PanelBeatmapStandalone`）与「打开谱面文件位置」（难度，经 `SoloSongSelect.GetForwardActions`），在系统资源管理器中打开并选中歌曲文件夹 / 难度文件。仅对 filesystem-backed 谱面（BMS + 直读 mania）显示；路径解析收口在共享 helper `FilesystemBeatmapLocation`，定位走 `GameHost.PresentFileExternally`（非 `Storage`，避免外部库绝对路径触发 traversal 守卫）。详见 [CHANGELOG.md](CHANGELOG.md) 2026-06-22 与约束 #7（实现边界）。**人工实机定位行为待用户确认。**
- **选曲展示/筛选增强三连（2026-06-22 其二/其三/其四，用户实机验收通过）**：① **难度表归类**——标准面板 `PanelBeatmapStandalone` 第 4 排在星级↔「展示全部难度」按钮间加难度表归类标签（如 `sl4` / `★8/sl4`），BMS 选曲与转谱-mania 都生效；osu.Game 不引用 BMS ruleset，经新增 `BmsPersistedMetadataResolver.GetDifficultyTableEntries`（**只读** ExtensionData，严禁建模成可写 DTO 否则抹 `Symbol`/`Md5`）+ `BeatmapLocalMetadataDisplayResolver.GetDisplayDifficultyTableClassification`（按 `TableSortOrder` `/`-join）。约束 #19。② **「显示转谱」三态**——mania 该按钮由二态升「禁用/启用/仅显示转谱」，单一 enum `ConvertedBeatmapsDisplay` 收口（`OsuSetting.ShowConvertedBeatmaps`(bool)→`ConvertedBeatmapsDisplay`(enum)、`FilterCriteria.AllowConvertedBeatmaps` 降 bool 投影、过滤行为 `BeatmapCarouselFilterMatching` 一处 switch）；mania 用三态循环钮 `ConvertedBeatmapsDisplayButton`、BMS 保留二态（echo 守卫双向同步）、设置面板改下拉；**红线**＝非 mania ruleset 把 `ConvertedOnly` 夹回 `Shown` 防清空列表。约束 #20。③ **mania 新增「难度表」分组**——只显示 BMS 转谱（排除原生 mania），新 osu.Game 共享 `BmsConvertedDifficultyTableGrouping`（复用 ① 的只读 resolver、构 表名→等级 树、无条目 Unrated），**「只显示转谱」用 grouping 丢弃法实现、零改 matching**（helper 对非 BMS 返回空 group → `addHierarchicalGroups` 丢弃、`MatchedBeatmapsCount` 取 grouping 阶段→计数与列表一致），`ManiaRuleset` override 4 分组虚方法，`NoResultsPlaceholder` 空结果指导（转谱禁用→可点启用 / 已开但空→提示导入 BMS）。约束 #21。详见 [CHANGELOG.md](CHANGELOG.md) 2026-06-22（其二/其三/其四）。
- **BMS 模式难度等级胶囊（2026-06-22 其五，选曲已用户确认 / 加载界面待确认）**：仅「BMS 模式 且 BMS 谱面」时，选曲星级胶囊（`PanelBeatmap` / `PanelBeatmapStandalone` / `BeatmapTitleWedge` 三处）+ **开始游玩的加载界面（`PlayerLoader` 的 `BeatmapMetadataDisplay`：标题剥尾括号 + 难度名走 `GetDisplayDifficultyName` + 星级换等级胶囊，门控同选曲）** 换成 IIDX 难度等级胶囊——保留原圆角背景，显示「标签 等级」（如 `NORMAL 7`，标签由 `#DIFFICULTY` 映射 UNKNOWN/BEGINNER/NORMAL/HYPER/ANOTHER/INSANE、等级用原始 `#PLAYLEVEL` 文本、无小数），背景按 `#DIFFICULTY` 走 IIDX 配色（白/绿/蓝/黄/红/紫，集中在 `OsuColour.ForBmsDifficultyLevel`）。文案集中在 `BeatmapLocalMetadataDisplayResolver.GetDisplayDifficultyLevel/GetBmsDifficultyTier`，新组件 `BmsDifficultyLevelDisplay`。**转谱-mania 视图保留真实转谱星级**（转谱星有意义）。实现＝新胶囊与原 `StarRatingDisplay` 并存、按门控 alpha 互斥；**原星级胶囊保持存活(alpha 0)**继续喂色 → 小星星 `starCounter`、分布点 `SpreadDisplay`、面板 tint/三角**全部保持现状**（用户选定）；wedge 难度名取色 BMS 时随新胶囊 IIDX 色。display-only，不动存库字段/MD5。约束 #22，详见 [CHANGELOG.md](CHANGELOG.md) 2026-06-22（其五）。
- **代码状态**：BMS 当前已具备 persisted `ChartFilterStats` metadata、`BmsFilterCriteria`、`BmsRuleset.CreateRulesetFilterCriteria()` 与 shared `FilterControl` 内完整的 BMS-only filter surface。`BmsCompositionFilterControl` 已以 BMS-local 私有单轨控件落地：`RC / LN / SCR` 三段可独立启停、各自表示最大占比、尾段为空白容差；`BmsCompositionHandle` 拖拽句柄可在三段边界间拖拽、并在句柄上显示当前数值；`BmsCompositionRowButton` 基于 `ShearedToggleButton`、激活时用段配色、非激活时用 `ColourProvider.Background3/Background1`（hover 效果可见）；`BmsKeyCountToggleButton` 提供 5K/7K/9K/14K 独立启停。`SearchHintTooltip` 已作为搜索框悬浮提示接入，展示全部 BMS 搜索语法；当前公开语法已统一为 `key/keys`、`rc/rice`、`ln`、`scr`，其中 `regular` 只保留为兼容 alias。`OverlayColourProvider` 通过构造函数传递，不依赖 global tooltip-layer DI scope。颜色方案：RC=蓝(94,190,255)、LN=黄(255,212,92)、SCR=橙(255,119,86)。
- **文档状态**：`P1-I` 四件套已更新到当前落地状态。

## 已确认事实

- `RC / LN / SCR` 三段已采用互斥分区：SCR 优先，LN 为非 scratch long note，RC 为剩余。
- `BmsCompositionFilterControl` 为 BMS-local 私有单轨控件，符合"单轨上限段 + 尾段空白容差 + 独立启停"产品合同。
- `SearchHintTooltip` crash 修复：根因是 `[Resolved] OverlayColourProvider` 在 global tooltip-layer 不在 DI 作用域；遵循 `ModTooltip` 构造函数注入模式，同时把 `GridContainer + AutoSizeAxes.Both` 布局替换为 `FillFlowContainer + Container(Width=160f)`。
- `CompositionValueTextBox` 可见时返回 `false` from `OnDragStart()`，让近旁句柄拖拽事件可以正常冒泡；隐藏状态下不消费 positional input。
- 当 RC/LN/SCR 恰好填满 100% 时，向右拖拽共享边界优先消耗尾段容差，然后才压缩相邻右段；数值输入与外部 bindable 赋值仍走 clamp。
- `ShearedButton.updateState()` 的 hover `Lighten(0.2f)` 要求底色非黑；`BmsCompositionRowButton` / `BmsKeyCountToggleButton` 非激活态已改用 `ColourProvider.Background3/Background1`。
- RC=蓝(94,190,255)、LN=黄(255,212,92)、SCR=橙(255,119,86) 已是冻结配色；tooltip BMS 强调色已同步改为蓝色匹配 RC。
### backfill 当前合同（2026-06-16 收口，用户实测确认正常）

> 本节是"当前仍有效的事实"。这一轮的根因定位/性能迭代全过程见 [CHANGELOG.md](CHANGELOG.md) 2026-06-16。

- `BmsChartFilterStatsBackfill` 以 `Task.Run` 异步后台执行；刷新过滤的 cache-updated 回调走**订阅者列表**（不被 init 竞态吞掉，迟到订阅者立即 invoke 一次），且周期性刷新**时间节流 20s**（`notifyCacheUpdatedThrottled`），阶段完成仍直接刷新。
- **Phase 1**（枚举已持久化 stats 填缓存）必须用 `EnumerateBmsBeatmaps(Realm)`——`.AsEnumerable()` 内存求值，**禁止**在 Realm `IQueryable` 上比较 link-traversal 属性 `b.Ruleset.ShortName`（会抛"left-hand side must be a direct access to a persisted property"，曾被静默 catch 导致整链失效）。
- **Phase 2**（补算缺 stats 的旧库谱）= **直读 .bms**（`computeStatsDirect`，**禁逐张 `GetWorkingBeatmap`**——其进程级 `lock(workingCache)` 会和 UI 抢锁、阻塞 update 线程）+ **轻量计数解码**（`ComputeFromDecodedChart`，与完整转谱**可证等价**，`BmsBeatmapConverter.IsScratchLane` 唯一真源）+ **批量写回**（每 200 张一个 realm 事务）+ **进度通知**（`ProgressNotification` done/total，`missingIds==0` 不弹）。
- `Matches` 取值 `GetCachedStats(ID) ?? GetChartFilterStats()`（缓存优先、快照兜底）；fail-open（缺 stats 不静默隐藏）不动；生产匹配路径不碰 working-beatmap I/O。
- **自动化语义**：无需人工重导——Phase 1 读已持久化、Phase 2 后台自动补算并批量写回 Realm（跨会话持久）。旧库（2026-05-11 导入持久化前）首轮 Phase 2 补齐量大，过滤精度渐进收敛、有进度通知可见、补齐一次后永久。
- **"已处理"负缓存（2026-06-18 其三补）**：missing 判定＝`GetChartFilterStatsState().Resolved` 为假。Phase 2 处理过的谱面**无论是否产出可用 stats 都写回 resolved 标记**（`ResolveChartFilterStats`：非空存 stats、空只置 `ChartFilterStatsResolved`），否则空谱/不可算谱（`ChartFilterStats` 恒 `null`）每次启动被重新归类为 missing 并重算——即"每次启动跑一次构成计算、没有持久化"的根因。import/reuse/`GetOrBackfill` 同走 `ResolveChartFilterStats`；空谱在 `Matches` 仍按 `null` fail-open。详见 TECHNICAL_CONSTRAINTS #16。
- 诊断埋点保留（全 `Verbose`/`LoggingTarget.Database`，仅 Phase 1 失败为 Important）：Phase 1 cached/missing、Phase 2 per-item 计时分桶、匹配抽样（5s 节流）。

## 当前验证基线

- importer / statistics / criteria / BMS-only FilterControl 的首轮 focused suite 当前保持 **30/30**；相关 build gate 当前可通过。
- `BmsFilterCriteriaTest` 当前保持 **4/4**，并已锁住公开搜索口径 `rc/rice` 与 `regular` 仅作兼容 alias 的合同。
- **最近一次验证（2026-06-22 其五）**：BMS 模式难度等级胶囊（星级→IIDX 难度标签＋#PLAYLEVEL）选曲三处 + 加载界面（`PlayerLoader`）——`BmsLocalMetadataDisplayResolverTest` **14/14**（+5：标签＋等级 `NORMAL 7` / UNKNOWN（`#DIFFICULTY` null 与 0）/ 原始 playlevel verbatim `ANOTHER 12+` / 仅标签无 playlevel `HYPER` / 非 BMS 空）；`osu.Desktop.slnf` Release **0 错误**。约束见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md) #22，详见 [CHANGELOG.md](CHANGELOG.md) 2026-06-22（其五）。**选曲三处用户已实机确认「符合预期」；加载界面（`BeatmapMetadataDisplay`）标题/难度名/等级胶囊用户已实机确认「暂时无异常」。两处均验收通过。** 上一轮「选曲展示/筛选增强三连」（难度表归类 #19 / 「显示转谱」三态 #20 / mania 难度表分组 #21）已于 2026-06-22 用户实机三次验收通过（详见 CHANGELOG 其二/其三/其四）。
- **filter / backfill 验证基线（2026-06-18 其三/其四，仍有效）**：BMS 全量 **922/922**，backfill resolved-marker 四条（`BmsChartFilterStatsBackfillTest`：空结果持久化 round-trip / 未处理读回未 resolved / 有 stats 持久化 / 与 converted_star 共存）+ 2026-06-16 三条回归（`TestChartFilterStatsBackfillQueryDoesNotThrowAgainstRealm` / `TestLateCacheSubscriberStillReceivesRefresh` / `TestLightweightChartFilterStatsMatchFullConversion`）；`osu.Game.Tests` converted-star/persisted-metadata 17/17。用户连跑两次启动实测「补齐一次后永久持久化」（第 2 次 `Phase 1 done: 57685 persisted, 0 missing`、补算/"正在分析"通知不再复发）。backfill 硬约束见 read-model #8–#16。

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
- **选歌偶发掉帧（未定位 / 已搁置，2026-06-18 其四）**：测 I5–I7 分组/展示层级行为时偶发 ~1000→~25FPS 持续波动、切分组层级不恢复、事后不可复现。I5–I7 链路经逐文件审查为事件驱动、未定位为成因；用户所给日志不含帧率/线程耗时数据，唯一 Global Statistics 快照非掉帧现场，证据不足以定因。捕获协议与标准候选见 [CHANGELOG.md](CHANGELOG.md) 2026-06-18 其四；下次复现需现场 `Ctrl+F11` 抓线程瓶颈 + 当场重导日志。

## 下一检查点

1. 为 `BmsCompositionFilterControl` 单轨拖拽语义补 headless 断言覆盖（边界拖拽、填满 100% 时尾段优先压缩）。
2. 评估是否需要补充 shared visual gate（`TestSceneBeatmapFilterControl` BMS branch）。
3. （可选）若用户偏好"更慢但完全零卡顿"，给 Phase 2 加低优先级档（并行度降到 1 / 每张让一让 CPU），用更长总时间换零冲击——当前已有进度通知+直读+批量写回，旧库首轮已"够用不碍事"，此为取舍旋钮非必须项。
4. （可选）诊断埋点（per-item 计时分桶）确认旁路稳定后可移除，保持后台路径精简。
