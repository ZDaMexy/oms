---
name: project-oms-songselect-display-nav
description: P1-I 选歌展示/导航的状态分离、池化布局与大库诊断地雷
metadata:
  node_type: memory
  type: project
---

# BMS 选歌展示与导航召回

权威当前态：[P1-I STATUS](../../doc_md/subline/P1-I/DEVELOPMENT_STATUS.md)；详细历史：[P1-I CHANGELOG](../../doc_md/subline/P1-I/CHANGELOG.md)。

## 修改前先找的状态边界

- `BeatmapSetsGroupedTogether` 是歌曲/谱面折叠的单一收口点；不另建 per-ruleset FilterControl host。
- display-level 用户偏好与强制显示值分离；解锁后恢复偏好。修改 disabled bindable 前先解除 `Disabled`。
- group 返回与 scoped beatmap-set 是不同状态；Back 优先级：scoped set → group 上退 → 退出 Song Select。
- converted-mania 难度表只显示 BMS 转谱，应由 grouping 对非 BMS 返回空定义实现，不强改 matching 状态。
- `RulesetData` 的难度表条目在 osu.Game 侧只读；不得用不完整 DTO 写回。
- 分组定义按 persisted JSON 内容缓存；大库 JSON 成本先查实际 cache hit/miss，不把该路径当作每次反序列化重做。

## UI 地雷

- 池化 panel 的 init-only offset 不能逐项改；使用运行期 `AdditionalXOffset`。
- 根组 `IsExpanded` 需要显式同步；子组由父组驱动不会自动覆盖根。
- `Alpha=0` 的 child 在 AutoSize/FillFlow 中可能被视为 non-present。要隐藏图标但保留 lamp/占位，必须 `AlwaysPresent=true`。反过来，child 自身 `IsPresent` 不能证明祖先容器可见；验证 ruleset 分支要检查对应容器/祖先链，排除整个筛选面板折叠态。
- DrawSize 变化只应重居中已提交 selection；无 selection 时不要回退到 expanded group/group header。
- `pendingRootGroupFocus` 只服务 fresh-entry：当前谱不属于 BMS 时要抑制自动展开；用户已选 BMS 谱后要清掉延迟标记。

## 难度表标记未更新时

难度表批次写入后应先检查 `RealmAccess` 的谱面 ID 通知、detached store 最新 JSON 和 `Filter(..., clearExistingPanels: true)` 是否串通；只改 JSON 而复用旧可见 panel，会让 `PrepareForUse` 中取一次的标签留旧值。不要重新引入“重进选歌即可”的旧 workaround；通知主合同见 [P1-H CONSTRAINTS](../../doc_md/subline/P1-H/TECHNICAL_CONSTRAINTS.md) 第 21 条。

## 性能诊断

- 过滤/分组循环不得逐谱 `GetWorkingBeatmap`；read-model 与缓存优先，参见 [[reference_bms_composition_filter]]。
- 偶发掉帧没有现场数据时不归因。复现时抓 `Ctrl+F11` 的 Update/Draw/GC、Alt-Tab 行为和当场日志；按现场时序区分 atlas/纹理绑定、窗口非活动限帧、GC、metadata 批次重新分组或 draw/layout，不预设某一条必是根因。
- 5万级库通用地雷见 [[reference_song_select_perf]]。
