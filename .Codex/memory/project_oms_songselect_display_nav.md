---
name: project-oms-songselect-display-nav
description: P1-I 选歌展示/导航的稳定合同与 UI/大库地雷
metadata:
  node_type: memory
  type: project
---

# BMS 选歌展示与导航召回

权威当前态：[P1-I STATUS](doc_md/subline/P1-I/DEVELOPMENT_STATUS.md)；详细历史：[P1-I CHANGELOG](doc_md/subline/P1-I/CHANGELOG.md)。

## 已有产品面

- BMS-only 展示层级（歌曲↔谱面）、层级返回条和 hierarchical grouping。
- 难度表/内外部谱库分组、难度表归类、converted-mania 三态展示与 mania 难度表分组。
- BMS 模式 IIDX 难度胶囊、preview 指示和文件位置入口。
- 分组定义按 persisted JSON 内容缓存，避免大库每次反序列化。

## 关键实现合同

- `BeatmapSetsGroupedTogether` 是歌曲/谱面折叠的单一收口点；不另建 per-ruleset FilterControl host。
- display-level 用户偏好与强制显示值分离；解锁后恢复偏好。修改 disabled bindable 前先解除 `Disabled`。
- group 返回与 scoped beatmap-set 是不同状态；Back 优先级：scoped set → group 上退 → 退出 Song Select。
- converted-mania 难度表只显示 BMS 转谱，应由 grouping 对非 BMS 返回空定义实现，不强改 matching 状态。
- `RulesetData` 的难度表条目在 osu.Game 侧只读；不得用不完整 DTO 写回。

## UI 地雷

- 池化 panel 的 init-only offset 不能逐项改；使用运行期 `AdditionalXOffset`。
- 根组 `IsExpanded` 需要显式同步；子组由父组驱动不会自动覆盖根。
- `Alpha=0` 的 child 在 AutoSize/FillFlow 中可能被视为 non-present。要隐藏图标但保留 lamp/占位，必须 `AlwaysPresent=true`。
- DrawSize 变化只应重居中已提交 selection；无 selection 时不要回退到 expanded group/group header。
- `pendingRootGroupFocus` 只服务 fresh-entry：当前谱不属于 BMS 时要抑制自动展开；用户已选 BMS 谱后要清掉延迟标记。

## 性能诊断

- 过滤/分组循环不得逐谱 `GetWorkingBeatmap`；read-model 与缓存优先，参见 [[reference_bms_composition_filter]]。
- 偶发掉帧没有现场数据时不归因。复现时抓 `Ctrl+F11` 的 Update/Draw/GC、Alt-Tab 行为和当场日志；优先排查 atlas/纹理绑定、窗口非活动限帧、GC，而不是先改事件驱动的分组代码。
- 5万级库通用地雷见 [[reference_song_select_perf]]。
