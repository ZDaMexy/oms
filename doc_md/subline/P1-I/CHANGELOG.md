# P1-I 变动日志

## 2026-06-22（其四）：mania 选曲新增「难度表」分组（只显示 BMS 转谱 + 空结果指导）

用户要求：给 mania 选曲分组下拉新增「难度表」分组，行为同 BMS 模式难度表分组，但**只显示 BMS 转谱**（排除原生 mania）；若因转谱被禁用、或没有 BMS 谱面导致结果为空，给针对性指导说明。

- **跨 ruleset 取数（复用 osu.Game 侧只读 resolver）**：mania ruleset 不引用 BMS ruleset、拿不到 `BmsTableGroupMode`。新增 osu.Game 共享 `BmsConvertedDifficultyTableGrouping`（`Screens/Select/`），用 [[2026-06-22 其二]] 已建的 `BmsPersistedMetadataResolver.GetDifficultyTableEntries`（只读 ExtensionData）构建 表名→等级 的 `GroupDefinition` 两级树（无条目→Unrated），镜像 `BmsTableGroupMode`（含按 RulesetDataJson 内容键的有界解析缓存）。
- **「只显示转谱」机制 = grouping 丢弃法（零改 matching）**：关键发现——`BeatmapCarouselFilterGrouping.addHierarchicalGroups` 对**返回空 group 定义**的谱面直接**丢弃**，且「N matches」计数取 `Filters.Last()`（=grouping 阶段）。故 `BmsConvertedDifficultyTableGrouping` 对**非 BMS** 谱面（`Ruleset.ShortName!="bms"`，即原生 mania）返回空 → 被 grouping 丢弃，转谱按表分组。无需改 matching、无需 force ConvertedOnly、计数与列表一致。转谱是否出现仍由「显示转谱」设置在 matching 阶段把关（禁用→无转谱→丢光→空）。
- **mania ruleset 接线**（`ManiaRuleset`）：override `GetAvailableSongSelectGroupModes`（在「未分组」后插入 `GroupMode.DifficultyTable`）/`IsSongSelectGroupingHierarchical`（DifficultyTable→true，触发层级树 + 强制扁平 standalone）/`ShouldResetSongSelectGroupToRoot`（DifficultyTable→true，进选曲落根=表名层）/`GetSongSelectGroupDefinitions`（DifficultyTable→共享 helper）。`GroupMode.DifficultyTable` enum 与「难度表」文案早已存在（原 BMS 专用），直接复用。
- **空结果指导**（`NoResultsPlaceholder`）：mania+DifficultyTable+空时加专项说明——先 paragraph「难度表分组只显示 BMS 转谱谱面。」；再分两路：`ConvertedBeatmaps==Hidden`→bullet 可点链接「启用「显示转谱」」(→设 `Shown`)；否则（转谱已允许但仍空＝无 BMS 谱面/被其他筛选滤掉）→bullet「导入 BMS 谱面后即可在此按难度表分组查看。」。此上下文下**抑制**通用「enabling automatic conversion」hint（避免重复）。3 串走 `OmsSongSelectStrings`(+resx/zh.resx)。
- **回归**：`BmsConvertedDifficultyTableGroupingTest`（osu.Game.Tests，4 条：BMS 建树 / 无条目 Unrated / 非 BMS 空 / 非 BMS 带条目仍空）+ `ManiaDifficultyTableGroupingTest`（mania.Tests，3 条：暴露分组项 + 端到端「丢原生 mania·转谱按表分组」+ 未收录转谱进 Unrated）。`BeatmapCarouselFilterGroupingTest` 17/17 不回归；`osu.Desktop.slnf` Release **0 错误**。沉淀为 TECHNICAL_CONSTRAINTS 展示层级与层级导航约束 #21。**用户 2026-06-22 实机验收通过（观测暂无异常）。** 归属：主 `P1-I`（选曲分组面）；取数依赖 `P1-H`（难度表持久化）/复用 osu.Game resolver。

## 2026-06-22（其三）：mania 选曲「显示转谱」按钮改三态（禁用 / 启用 / 仅显示转谱）

用户要求：mania 模式下选曲筛选区的「显示转谱」从「启用/禁用」二态改为「启用/禁用/**仅显示转谱**」三态切换按钮；设置面板同步改三态（用户选定 Q1=三态 enum 收口）；BMS 模式那个按钮保持二态（用户选定 Q2，BMS 无转谱可显示、空操作）。

- **三态语义（mania）**：禁用＝只显示原生 mania；启用＝原生 + 转谱（BMS→mania）；**仅显示转谱**＝只显示转谱、隐藏原生 mania（让"只想在 mania 模式玩 BMS 谱"的人不被原生谱干扰）。
- **单一收口 enum**：新增 `ConvertedBeatmapsDisplay { Hidden, Shown, ConvertedOnly }`（`osu.Game/Configuration/`，`[LocalisableDescription]` → `OmsSongSelectStrings` 三串 禁用/启用/仅显示转谱）。`OsuSetting.ShowConvertedBeatmaps`(bool) **改名为** `ConvertedBeatmapsDisplay`(enum，默认 `Shown`)——旧 bool 配置键作废（升级后重置为默认"启用"，离线 pre-release 可接受）。`FilterCriteria` 加 `ConvertedBeatmaps` enum 字段 + 把 `AllowConvertedBeatmaps` 保留为 enum 的 **bool 投影属性**（`get => != Hidden`；`set` 映射 Shown/Hidden）——既给"是否允许转谱"消费方（star-lookup 门）一个稳定 bool，又零改动现有测试。
- **过滤收口点**＝`BeatmapCarouselFilterMatching` 第 70 行：原 `AllowGameplayWithRuleset(ruleset, allowConverted)` 改 `switch(ConvertedBeatmaps)`——Hidden=`allowConversion:false`(原生)、Shown=`allowConversion:true`(原生+转谱)、ConvertedOnly=`Ruleset!=当前 && allowConversion:true`(仅转谱)。star-lookup 两处门(matching:179 / grouping:421)继续读派生 `AllowConvertedBeatmaps`(ConvertedOnly 时仍 true、转谱照常取解析星数)，零改动。
- **UI**：新增 `ConvertedBeatmapsDisplayButton`(继承 `ShearedButton`，点击循环 Hidden→Shown→ConvertedOnly，文案+高亮色随态：Hidden=灰"显示转谱"、Shown=Highlight1"显示转谱"、ConvertedOnly=Colour1/2 强调色"仅显示转谱")。`FilterControl` 标准(mania)布局换成它(`Current` 双绑 enum)；BMS 布局保留二态 `ShearedToggleButton`，与 enum 做**带 echo 守卫的双向同步**(`Active = enum!=Hidden`；toggle→Shown/Hidden；`if(e.NewValue==convertsAllowed)return` 断环)。设置面板 `FormCheckBox`→`FormEnumDropdown<ConvertedBeatmapsDisplay>`。
- **BMS 安全（红线）**：BMS 无转谱，`仅转谱` 在 BMS 下会清空列表。故 `FilterControl.CreateCriteria` 对**非 mania** ruleset 把 `ConvertedOnly` 夹回 `Shown`（`mania_ruleset_short_name` 常量，OMS 唯一转谱目标=mania）；BMS 二态按钮也只产出 Hidden/Shown。即使 mania 设 ConvertedOnly 后切到 BMS，列表也不会空。
- **改动文件**：新增 `ConvertedBeatmapsDisplay.cs` / `ConvertedBeatmapsDisplayButton.cs` + `OmsSongSelectStrings`(+resx/zh.resx 三串)；改 `OsuConfigManager` / `FilterCriteria` / `BeatmapCarouselFilterMatching` / `FilterControl` / `SongSelect`(×2 派生) / `OsuGame` / `NoResultsPlaceholder` / 两个 `SpreadDisplay`(派生) / `SongSelectSettings`。回归：`FilterMatchingTest` 新增 4 条（Hidden 只原生 / Shown 原生+转谱 / ConvertedOnly 隐原生留转谱 / AllowConvertedBeatmaps 投影），全套 98/98；carousel sort+group 25/25；`osu.Desktop.slnf` Release **0 错误**。沉淀为 TECHNICAL_CONSTRAINTS 展示层级与层级导航约束 #20。**用户 2026-06-22 实机验收通过（观测暂无异常）。** 归属：主 `P1-I`（选曲筛选面）。

## 2026-06-22（其二）：标准面板第 4 排加难度表归类（星级 ↔ 按钮 之间，BMS + 转谱-mania）

用户要求：展示层级＝「谱面」时，`PanelBeatmapStandalone`（选歌列表扁平行 / 兼歌曲条）第 4 排由「星级 + 临时展示所有难度按钮」改为「星级 + **难度表难度归类** + 临时展示所有难度按钮」，BMS 选曲与转谱-mania 选曲都生效；被多个难度表索引则按选曲列表的难度表顺序、用 `/` 一一列举（如 Satellite-sl4 显示 `sl4`；若还属于发狂难易度表-★8 则显示 `★8/sl4`）。

- **取值链（关键红线＝只读）**：难度表条目持久化在 BMS ruleset 写入的 `BmsBeatmapMetadataData.difficulty_table_entries`（与 converted-star 共用单个 `BeatmapMetadata.RulesetData` 列）。osu.Game 不引用 BMS ruleset，故新增 `BmsPersistedMetadataResolver.GetDifficultyTableEntries`：复用既有按 JSON 内容键缓存的 `getPersistedData`，**只从 `BmsPersistedMetadataData.ExtensionData` 里读 `difficulty_table_entries`**（osu.Game 侧 `BmsPersistedDifficultyTableEntry` 只建模 `TableName/LevelLabel/Level/TableSortOrder`、**绝不回写**）。**没有**把该字段建模成可写 DTO 字段——否则 converted-star 写回会按缺 `Symbol`/`Md5` 的 DTO 重序列化、抹掉这两字段，重蹈 P1-H #22「共享 RulesetData 列互相 clobber」的破坏性 ping-pong（既有 `TestConvertedStarRatingWritePreservesDifficultyTableFields` 证明 ExtensionData 已忠实保留该字段，本次只读它）。
- **展示逻辑**：`BeatmapLocalMetadataDisplayResolver.GetDisplayDifficultyTableClassification`（display-only，键于 `Ruleset.ShortName=="bms"`，BMS-mode 与转谱-mania 同一 BMS 谱面取值一致）→ 按 `TableSortOrder` 升序、每个表取一个 `LevelLabel`、`/` 连接。`LevelLabel` 已含符号前缀（`buildLevelLabel`：`★8` / `sl4`），直接显示。表顺序＝当前 `TableSortOrder`（导入/启用序），日后难度表管理改造改顺序时本展示自动跟随。
- **面板挂载**：`PanelBeatmapStandalone` 第 4 排 FillFlow 在 `starRatingDisplay` 与 `SpreadDisplay` 之间插 `difficultyTableText`（`Caption1 SemiBold` + `colourProvider.Content1`）；`PrepareForUse` 取归类串、非空才 `Alpha=1`，空（非 BMS / Unrated）则 `Alpha=0` 借 FillFlow「非 present 子项不排布」消除星级↔按钮的幽灵间距；`FreeAfterUse` 复位。只在 `PrepareForUse` 取一次（归类只依赖谱面自身条目、不随 ruleset/mod 变；中途改难度表仍沿用既有重进选歌/重启生效的 carousel staleness 边界）。
- **回归**：新增 `BeatmapLocalMetadataDisplayResolverTest` 4 条（单表→`sl4` / 多表按 sort order→`★8/sl4` / Unrated→空 / 非 BMS→空），全套 7/7 通过；`osu.Game` Release 编译 0 错误 0 警告。沉淀为 TECHNICAL_CONSTRAINTS 展示层级与层级导航约束 #19。**用户 2026-06-22 实机验收通过（观测暂无异常）。** 归属：主 `P1-I`（选歌标准面板展示面）；取数依赖 `P1-H`（难度表持久化 + 共享 RulesetData 列合同）、复用 `P1-K` 的 `BeatmapLocalMetadataDisplayResolver` 展示解析器。

## 2026-06-22：选歌右键「打开歌曲/谱面文件位置」（在资源管理器中定位）

用户要求在 BMS 选歌右键菜单加两项「在系统资源管理器中定位」：歌曲条 → 打开歌曲所在文件夹并选中该歌曲文件夹；难度 → 打开歌曲文件夹并选中该难度文件。

- **范围**：凡**文件系统直读**谱面（`BeatmapSetInfo.FilesystemStoragePath` 非空 —— BMS chartbms/ + 直读 mania chartmania/）都显示；hash 库（导入 .osz，无真实文件夹）不显示。即「所有本地文件夹谱面」，不限 BMS（用户选定）。
- **路径解析（复用 `BmsBgaPlayer.tryGetAbsolutePath` 范式）**：新增共享 helper `osu.Game/Beatmaps/FilesystemBeatmapLocation.cs` —— `TryGetSetDirectory`（external＝绝对 `FilesystemStoragePath` 原样、managed＝`storage.GetFullPath(相对)`）、`TryGetBeatmapFile`（set 目录 + `BeatmapInfo.LocalFilePath`，标准化 `/` 转原生分隔符）、`IsFilesystemBacked`、`CreateOpenSongFolderItem`/`CreateOpenChartFileItem`（非 filesystem-backed 返回 null）、`Reveal`。
- **定位 API（红线）**：统一走 `GameHost.PresentFileExternally(绝对路径)`（Windows＝`explorer /select,路径`，打开父目录并选中目标）。**不得用 `Storage.PresentFileExternally`** —— 它对越出数据根的外部库绝对路径会抛 traversal 异常。目标不存在（外部库被移动）时优雅退回 `OpenFileExternally(父目录)`，再不行静默。外部目录只读打开、绝不改动（符合 external「只读」合同）。
- **挂载点（3 处共享 osu.Game 面板，各 `[Resolved] GameHost` + `Storage`，仅 filesystem-backed 时追加项，中文硬编码标签）**：歌曲条 `PanelBeatmapSet.ContextMenuItems`（删除项之前）→「打开歌曲文件位置」；难度 `SoloSongSelect.GetForwardActions`（Edit 之后）→「打开谱面文件位置」（自动覆盖 `PanelBeatmap` 行 + `PanelBeatmapStandalone` + footer Options 弹层）；单难度合并条 `PanelBeatmapStandalone.ContextMenuItems` 另补「打开歌曲文件位置」（它兼作歌曲条）。
- **回归**：新增 `FilesystemBeatmapLocationTest`（6 条：managed→GetFullPath / external→绝对原样 / 难度文件 join + 分隔符规整 / hash 库与无 LocalFilePath 返回 false / 菜单项 gating）；`osu.Game.Tests` 的 `TestScenePanelSet` + `TestScenePanelBeatmapStandalone`（4 条）确认新增 `[Resolved]` 不破坏面板加载。验证：BMS 全套 **942/942**、`osu.Game.Tests` 面板 4/4、`osu.Desktop.slnf` Release **0 错误**。**人工实机定位行为待用户确认**。归属：主 `P1-I`（选歌右键产品面），路径解析依赖 `P1-H`（`FilesystemStoragePath`/`LocalFilePath` 存储拓扑）。

## 2026-06-21：选歌两处「自动跳滑」修复（chart 选中 root-jump / 窗口还原 group-jump）

用户实机发现两处 carousel 视图被自动滚走的 bug，均出现在「非 BMS（mania）曲目正在播放时进入 BMS 难度表分组」的场景，但触发机制与归属各不相同：

**Bug 1：进 BMS 后选中任一谱面，画面自动跳滑到该谱面所属的最外层分组（表名层，非难度层）。** 复刻：mania 正在播放 → 进 BMS 难度表分组 → 手动进难度表/难度/选歌列表点任一谱面 → 画面滑到该谱所属「表」（如 satellite-sl0 的谱跳到 Satellite 表头），而非停在所选谱。根因＝`pendingRootGroupFocus`（fresh-entry 想把当前谱面的 root group 滚到顶）在当前全局谱面对 BMS **invalid**（mania）时**永远满足不了**——`FocusRootGroupForBeatmap(maniaBeatmap)` 在 BMS carousel 里找不到该谱、恒返回 false → 标记一直挂着。等用户手动选第一张 BMS 谱、全局 `Beatmap` 变更触发 `updateVariousState → tryFocusRootGroupForCurrentBeatmap` 时，标记仍为真且此时谱面已可聚焦 → 把视图劫持到那张谱的 root group。修复＝`tryFocusRootGroupForCurrentBeatmap` 在 `carousel.CurrentGroupedBeatmap != null`（已存在具体选中）时**直接放弃** pending focus（清标记、返回 false）——fresh-entry root focus 只在「尚无具体选中」期间有意义，一旦用户选了谱就作废。与 2026-06-18（其二）#16 的抑制是对称补角：#16 抑制 invalid 期间的自动选中，本次放弃 invalid 永不满足的延迟聚焦。回归 `TestSelectingChartWhileNonBmsPlayingDoesNotJumpToRootGroup`（去修复即跳到 "Satellite"、已验证失败）。

**Bug 2：在选歌列表任意滚动位置按 Win 键最小化再返回，画面自动跳滑到当前展开层级的组头。** 复刻：展开 sl5（或仅展开 satellite 表）后自由滚到别处 → Win 最小化 → 返回即跳到 sl5（或 satellite）组头。根因＝窗口最小化/还原会改变 carousel `DrawSize`，触发 base `Carousel.OnInvalidate` 的 `Invalidation.DrawSize` 分支**无条件** `selectionValid.Invalidate()` → 下一帧 `ScrollToSelection()` 重新居中。该 re-center 的语义是「窗口尺寸/纵横比变化时保持选中项居中」，但当没有具体选中（只展开了组、键盘光标停在组头、`currentSelection` 为 null）时，`BeatmapCarousel.GetScrollTarget` 会回退到键盘光标 / `ExpandedGroup` 的位置 → 把自由滚走的视图猛拽回组头。修复＝base `Carousel.OnInvalidate` 把 DrawSize re-center **限定在存在具体选中时**（`currentSelection.CarouselItem != null`）才执行；无具体选中时不重跑选中滚动，保留用户当前滚动位置。**mania 安全**：mania 选歌恒有已提交选中（`ShouldActivateOnKeyboardSelection` 使方向键浏览即 `Activate` 提交），`currentSelection.CarouselItem` 始终非空 → resize 仍照常居中，零行为变化；BMS 难度表是「键盘光标可停在组头而不提交选中」的少数场景，恰是本 bug 的成因。回归 `TestDrawSizeChangeDoesNotRecentreWithoutSelection`（去修复即偏移 ~281px、已验证失败）＋守护 `TestDrawSizeChangeRecentresCommittedSelection`（有选中时 resize 仍居中、防过度收口）。

改动文件＝`osu.Game/Screens/Select/SongSelect.cs`（bug 1）＋`osu.Game/Graphics/Carousel/Carousel.cs`（bug 2，shared base、mania-safe）。沉淀为 TECHNICAL_CONSTRAINTS 展示层级与层级导航约束 #17（root-focus 放弃）/ #18（DrawSize re-center 收口）。验证：BMS 选歌分组全套（难度表+内外库）11/11；新增 3 条回归全过；`osu.Game.Tests` carousel 套件 18 条 pre-existing 失败经 baseline（git stash）比对确认与本次改动无关（headless 输入驱动/排序稳定性既有 flaky）。Release 编译 0 错误。**用户 2026-06-21 实机跑完两个原始复现场景（mania 播放中进 BMS 难度表→选谱；列表任意位置 Win 最小化→还原），确认观感正常、验收通过。**

---

## 2026-06-18（其四）：谱面构成持久化「其三」实机验收通过 + 选歌偶发掉帧观察（已搁置）

**其三 backfill 持久化修复 = 实机验收通过。** 用户连跑两次启动：第 1 次仍执行一次谱面构成补算（旧库一次性 Phase 2），第 2 次启动日志 `[BmsCompositionFilter] backfill Phase 1 done: 57685 beatmaps already have persisted chart-filter-stats, 0 are missing`，未再触发补算或"正在分析"通知。确认"每次启动重算"已收敛为"补齐一次后永久持久化"，与「其三」合同一致。（注：该用户库 57685 张构成全部可算，普通非空 stats 写回即足以让第二次 0 missing；`ChartFilterStatsResolved` 负缓存标记保障的是"空/不可算"子集不再每启动被重归 missing——该库恰无此子集，故标记在本次实测中未被触发但逻辑正确。）本轮无代码改动。

**选歌偶发掉帧观察（未定位 / 已搁置）。** 用户在测试 I5–I7 分组/展示层级行为时，偶发帧数从 ~1000 暴降到 ~25FPS 并持续波动，切换分组/层级不恢复；事后无法复现。逐文件审查 I5–I7（展示层级下拉 / 面包屑导航 / `PanelGroup` 深度渲染 / `BmsTableGroupMode` 分组缓存 / `Panel.AdditionalXOffset`）全为**事件驱动**（仅 group/level 变更时执行），filter/grouping 跑后台线程，**未定位其为成因**——"切分组层级不恢复"恰反证不是这条只在变更时执行的管线。用户所给日志**不含任何帧率/帧时长/线程耗时**，唯一一份 Global Statistics 快照恰在打开 SettingsOverlay 瞬间（纹理上传队列冲到 1200、TextureAtlas 已扩 7 页），非分组掉帧现场。**结论：证据不足以定因，已与用户约定搁置。** 下次复现的捕获协议：现场 `Ctrl+F11` 看 Update/Draw/GC 哪条线程标红 + Alt-Tab 切走切回是否回升（验"窗口非活动→帧率限制器"，2 秒可证伪）+ 当场重导日志拿掉帧期间的 Global Statistics。标准候选（按吻合度排序）：① 纹理图集耗尽走非图集路径致 Draw 绑定暴增（吻合 Draw-bound + 不恢复，既有大库特性、非本次提交引入）；② 窗口被判非活动 → 帧率限制器（吻合"切分组无效"）；③ GC / drawable 池泄漏（暂无证据）。无代码改动。

---

## 2026-06-18（其三）：谱面构成 backfill 对"空/不可算"谱面补持久化标记（每次启动重算根因）

用户反馈："每次新启动游戏进入 BMS 选歌总会进行一次谱面构成计算，没有持久化。"

**根因**＝composition-filter backfill 只在计算出**非空** stats 时才写回（`sanitise` 把空结果折叠成 `null`、`SetChartFilterStats(null)` 不写任何东西）。于是凡是计算结果为空/不可用的谱面——genuinely 空谱（只有 BGM/autoplay channel-01 对象、0 playable）、或 `computeStatsDirect` 直读与 `GetWorkingBeatmap` 回落双双失败的谱面——`ChartFilterStats` 恒为 `null` 且**无任何"已处理"痕迹**。下次启动 Phase 1 把它们重新算进 `missingIds` → Phase 2 重算 → "正在分析 BMS 谱面构成"通知**每次启动都冒一次**。可算谱面在首轮已正确写回并跳过，但这批"空/失败"谱面构成**永不收敛的重算子集**。（已排除写回失败：`RealmAccess.Write` 后台线程取 thread-local realm 提交正常、Realm 事务串行＋写内 live 读使共享 `RulesetData` 列与 converted_star 互不 clobber；`Detach()` 也带 `BeatmapSet`，故直读路径对多数谱面可用。）

**修复**＝引入持久化的"已处理"负缓存标记：
- `BmsBeatmapMetadataData` 新增 `[JsonProperty("chart_filter_stats_resolved")] bool ChartFilterStatsResolved`（与 `chart_filter_stats` 同列、`IsEmpty` 计入它、`[JsonExtensionData]` 仍兼容 converted_star 共存）。
- 新 `ResolveChartFilterStats(stats)`：stats 非空＝存 stats，空＝只置 resolved 标记；二者都标记已处理。新 `GetChartFilterStatsState()`＝单次反序列化返回 `(Stats, Resolved)`，`Resolved = ChartFilterStatsResolved || ChartFilterStats != null`。
- Phase 1 用 `GetChartFilterStatsState()` 归类：有 stats→缓存；无 stats 且 `!Resolved`→missing；无 stats 但 `Resolved`→**跳过**（已处理）。
- Phase 2 对**每张**已 `Detach` 的谱面入队（stats 可为 `null`），`flushPendingWrites` 走 `ResolveChartFilterStats` 写回——空结果也落 resolved 标记。完成日志补 `processed - computed` 计数（"resolved to no usable stats"）。
- import（`BmsImportedBeatmapFactory`）/ reuse（`BmsFolderImporter.syncChartFilterStats`）/ `GetOrBackfill` 一律改走 `ResolveChartFilterStats`；`GetOrBackfill` 增 `Resolved` 早退（不再每次重算空谱）。`SetChartFilterStats`（纯 setter）只留给测试与 `BmsBeatmap.GetStatistics` 的内存显示路径。
- backfill 内被静默吞的写回 `catch{}` 改为记 `Important` 日志（符合 #9 / "后台 catch 必记日志"）。

空谱在 `Matches` 仍按 `null` **fail-open**（标记只阻止重算、不隐藏谱面、不参与过滤判定）。

**验证**：`BmsChartFilterStatsBackfillTest` 新增 resolved-marker 四条（空结果持久化 round-trip / 未处理读回未 resolved / 有 stats 持久化 / 与 converted_star 共存）；BMS 全套 **922/922**；`osu.Game.Tests` converted-star/persisted-metadata **17/17**。沉淀为 TECHNICAL_CONSTRAINTS #16。

---

## 2026-06-18（其二）：非 BMS 播放谱面进入 BMS 不再误展开分组

启动时游戏会自动随机播放一首曲目；若该曲目是非 BMS（mania）谱面，**直接进入或从 mania 切到 BMS（难度表分组）时总会误展开某个分组**（用户观察为 Unrated）。根因＝`SongSelect.ensureGlobalBeatmapValid` 的 `shouldSuppressGroupedAutoSelection()`（fresh-entry root-focus 期间抑制自动选中）**只在 `if (validSelection)` 分支内被检查**；当前全局谱面是 mania（对 BMS invalid）时走 invalid 回退（`SetDefault → IsDefault → NextRandom`）自动选中一张 BMS 谱面并 `setExpandedGroup` 展开其分组，而 `FocusRootGroupForBeatmap` 又聚焦不到 mania 谱面（不在 BMS carousel）→ `pendingRootGroupFocus` 一直为真——抑制本应生效却被 invalid 回退绕过。修复＝把 `shouldSuppressGroupedAutoSelection()` 提前到 valid/invalid 分支**之前**短路返回，使 fresh-entry root-focus 期间抑制**所有**自动选中（含 invalid 回退）、保持 root 层不展开；兼容谱面的 root group 聚焦仍由 `tryFocusRootGroupForCurrentBeatmap` 单独处理。只影响 `ShouldResetSongSelectGroupToRoot` 为真的 ruleset（BMS）；mania 等 `pendingRootGroupFocus` 永不置真、零影响。回归 `TestNonBmsPlayingBeatmapDoesNotExpandGroupOnEntry`（临时去掉修复即失败、已验证）。验证：BMS 全套 **918/918**。

---

## 2026-06-18：层级分组展开态 / 缩进方向修正（I6 跟进，用户实机反馈）

用户实机发现两处不符合直觉，均为层级分组（难度表 表名→等级）下**路径根组**的处理缺口：

1. **展开的表名组不显示展开箭头**：`setExpandedGroup` 只通过"父组的 `setExpansionStateOfGroup` 设子组 `IsExpanded`"传播展开态，而**路径根组没有父组、其自身 `IsExpanded` 从不被置位** → 表名组即便展开（其下等级可见）也无 chevron、且吃"未展开"的右推偏移。修复＝`setExpandedGroup` 显式管理路径根组的 `item.IsExpanded`（进入时 true、离开时复位 false，`setGroupItemExpansion` 经 `grouping.ItemMap`）。回归 `TestExpandedTableHeaderSharesExpandedStateWithLevel`。
2. **子组（等级）比父组（表名）突出得更多**：`Panel.updateXOffset` 的突出量只看 expanded/selected/keyboard-selected、不看层级深度，导致键盘选中的子组比未选中的父组更靠左（突出更多），与"子组在父组之内"的直觉相反。修复＝`Panel` 新增 `protected virtual float AdditionalXOffset`（默认 0，在 `updateXOffset` 叠加），`PanelGroup` override 返回 `group.Depth * 30f`；`30 > active_x_offset(25)` 保证展开的祖先组始终突出 ≥ 其（键盘选中的）后代组、并略多一点。非层级分组（mania 等）全 depth 0 → `AdditionalXOffset==0`、零影响。

验证：`TestSceneBmsSongSelectDifficultyTable` 6/6（含新增）、BMS 全套 **917/917**、共享 `TestScenePanelGroup`/`TestScenePanelSet`/`TestScenePanelBeatmap` 10/10。Release 编译干净（`osu.Desktop` 最终拷贝步被正在运行的游戏进程锁住 = MSB3021 文件锁，非代码问题）。

---

## 2026-06-16（其二）：选歌展示层级 + 层级返回条 + 难度表分组解析缓存（I5–I7）

> 用户授权 detailed 实现（"你详细规划，开工吧"）。三项均落地并通过 focused 回归，`osu.Desktop.slnf` Release 0 错误。本主题区别于本日「其一」的 backfill 收口。

### I5：展示层级（歌曲↔谱面）显式控制

- 新增 BMS-only `展示层级` 下拉（`OmsSongSelectStrings.DisplayLevel`，挂在 `FilterControl.createBmsFilters` 的 BMS 滤镜块内，非 BMS 不显示），两档 `DisplayLevel.Songs`（歌曲→谱面）/`DisplayLevel.Difficulties`（谱面）。
- 新增 `FilterCriteria.DisplayLevel`（nullable `osu.Game.Screens.Select.Filter.DisplayLevel`）。`ShouldGroupBeatmapsTogether` 重构为：先判 `GroupingForcesStandaloneDifficulties`（提取自原启发式：层级分组 / `Group==Difficulty` / `Sort==Difficulty` / `RankAchieved` / `LastPlayed×LastPlayed`）→ 强制扁平；否则 `DisplayLevel!=null` → `==Songs` 决定折叠/扁平；否则（mania/其他，`DisplayLevel==null`）走原默认 `true`。**mania 零行为变化**（criteria 永远 `null`，逻辑等价改写）。
- 持久化用新 `OsuSetting.BmsSongSelectDisplayLevel`（默认 `Songs`）。强制扁平分组下下拉锁定为「谱面」并禁用：`displayLevelSetting`(config 偏好) 与下拉 `Current`(显示值) 分离 + `suppressDisplayLevelSettingWrite` guard，**锁定不污染持久化偏好、离开后还原**。`criteria.DisplayLevel` 只在 BMS 时取偏好、否则 `null`。
- 回归：`BmsDisplayLevelGroupingTest`（3 条：显式两档生效 / 强制扁平忽略 DisplayLevel / `null` 保留默认启发式）。

### I6：层级分组导航（面包屑返回条 + Back 键）

- `BeatmapCarousel` 暴露只读 `IBindable<GroupDefinition?> CurrentExpandedGroup`（在 `setExpandedGroup` 更新）+ `CollapseExpandedGroupOneLevel()`（`setExpandedGroup(ExpandedGroup.Parent)` + `ChangeKeyboardSelection`/`ScrollToSelection` 滚到父组头或被折叠组的 root header）。`ISongSelect` 同步加这两个成员，`SongSelect` 转发给 carousel。
- 新增 `FilterControl.GroupNavigationDisplay`（仿 `ScopedBeatmapSetDisplay` 视觉，挂在其后）：`CurrentExpandedGroup!=null` 时显示「当前层级：<面包屑路径>」+ 返回按钮；**由 `ExpandedGroup` 驱动、与 `ScopedBeatmapSet` 状态独立**。`GlobalAction.Back` 仅在 `expandedGroup!=null && ScopedBeatmapSet==null` 时消费 → scope 退出优先于层级退回（`FocusedTextBox` 仅在有搜索词时吞 Back，空搜索时自然冒泡到 banner）。
- 回归：`TestSceneBmsSongSelectDifficultyTable.TestCollapseExpandedGroupWalksUpHierarchy`（叶级 ★1 → 表根 Satellite(depth0) → root null 逐级退）。

### I7：难度表分组解析缓存

- `BmsTableGroupMode` 按 `RulesetDataJson` 内容键缓存计算好的 `GroupDefinition[]`（`ComputeGroupDefinitions` 提为 internal），消除每次 refilter × 每张谱面的全量 `JsonConvert.DeserializeObject`。correctness-neutral（纯函数 + immutable record，跨谱面共享同一数组安全）、stale-proof（JSON 变即换 key）、有界（`cache_soft_cap=200_000` 安全阀）。
- 回归：`BmsTableGroupModeTest` 新增"缓存复用同一实例 + 内容等价""改 entries 后 stale-proof"两条。

### 二次调整（同日，用户反馈四点）

1. **展示层级下拉移入共享行**：从 BMS 滤镜块挪到 sort/group/collection 那一排，成为「分组」与「收藏夹」之间的 BMS-only 第 4 列（`createSortGroupColumns(showDisplayLevel)` 在 `updateRulesetSpecificFilters` 按 ruleset 切换列宽：BMS=maxSize 180 + gap 5、其他 ruleset=该列与 gap 收 0 + 下拉 `Alpha=0`，非 BMS 行布局与原先一致）。
2. **层级退回改为停在被折叠的组**：`CollapseExpandedGroupOneLevel` 在 `setExpandedGroup(parent)` 后把键盘 cursor 落在**刚折叠的那个组**（仍折叠、不重新展开）而非其父组——例如在 `satellite/★7` 点返回 → 展开降到 satellite 级、cursor 停在 `★7`（折叠态），再点 → cursor 停在 `satellite`。回归 `TestCollapseExpandedGroupWalksUpHierarchy` 增 cursor 停留断言。
3. **分组层级视觉区分**（初版 `Background5/6` 实机几乎看不出差异，改为高对比方案）：`PanelGroup.PrepareForUse` 按 `group.Depth` 多线索分级——根/表名组（depth 0）= `Background4`（更亮）+ 保留三角纹理 + `Heading2` + `Content1`（亮）；嵌套/等级组（depth≥1）= `Background6`（深）+ **三角纹理 `Alpha=0`（纯平）** + 更小的 `Body SemiBold` + `Content2`（暗）+ `Depth*24` 缩进。四重线索叠加一眼可分。mania 等非层级分组全为 depth 0 → 不受影响。
4. **删除"当前层级"前缀**：`GroupNavigationDisplay` 只显示面包屑路径（`発狂BMS難易度表 › ★1`）+ 返回按钮；移除 `OmsSongSelectStrings.CurrentGroupLevel` 及其 resx 条目。

### 验证

```powershell
dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter "FullyQualifiedName~BmsTableGroupModeTest|FullyQualifiedName~BmsDisplayLevelGroupingTest|FullyQualifiedName~TestSceneBmsSongSelectDifficultyTable|FullyQualifiedName~TestSceneBmsFilterControl"
dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --filter "FullyQualifiedName~TestScenePanelGroup"
dotnet build osu.Desktop.slnf -p:Configuration=Release
```

- BMS：`BmsTableGroupModeTest` 4/4、`BmsDisplayLevelGroupingTest` 3/3、`TestSceneBmsSongSelectDifficultyTable` 5/5、`TestSceneBmsFilterControl` 8/8；BMS 全套 **916/916**。共享 `TestScenePanelGroup` 6/6。`osu.Desktop.slnf` Release 0 错误。
- shared `BeatmapCarouselFilterGroupingTest` 通过；`TestSceneSongSelectGrouping` 中 `TestCollectionGrouping`/`TestMyMapsGrouping*`/`TestRankAchievedGrouping` 6 条失败是**既有 OMS 分歧**（这些测试 `ImportBeatmapForRuleset(..., 0)` 取 OnlineID==0 的 osu! 标准模式，OMS 已删除 → 空数组 → `TestResources.getRuleset` `% 0` DivideByZero，发生在 import 配置阶段、与本改动无关）。

---

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
