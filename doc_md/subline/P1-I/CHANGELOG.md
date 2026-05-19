# P1-I 变动日志

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
