# P1-I 当前状态：BMS 选歌筛选与搜索

> 最后更新：2026-09-29（软件验证完成；保留人工体验门）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。

## 当前阶段

I1/I2 与 I5–I7 主链存在；**I3 单轨产品面与 I4 单轨/搜索自动回归已完成，人工体验门仍保留**。当前 `FilterControl` 已用单行单轨 RC/LN/SCR 上限段替代三行双端原型。本轮验证使用当前产物；历史“三行双端”及早期误记单轨的审查结论保留在 [CHANGELOG](CHANGELOG.md)，不能复用作新控件交付证据。

## 已落地能力

- BMS-only 分组、排序、搜索与 key-count/filter surface。
- `RC / LN / SCR` 共用单条比例轨道：各段最大占比、独立启停、合计不超过 100%；拖动段右界或点击数值编辑，超出当前可用额度时夹紧当前段，其他段不变。
- 默认三项均关闭，不额外过滤；禁用保留数值和轨道额度，重新启用恢复该上限。单行固定 RC/LN/SCR 入口确保零宽段仍能编辑；尾部空白表示尚可分配额度，不是第四种谱面成分。
- 三项全启用且上限合计不足 100% 时，已具构成统计的谱面不匹配；合计恰为 100% 时只匹配精确配比。控件提示和编辑浮层如实说明，可关闭某项放宽限制；缺统计仍暂时展示。
- persisted `ChartFilterStats`、旧库后台 backfill、进度通知与 resolved 负缓存。
- 展示层级切换、层级返回条、难度表/内外部谱库层级分组。
- 难度表归类、converted-mania 三态展示、BMS→mania 难度表分组、IIDX 难度胶囊及文件位置入口。难度表整批写入后的列表分组与可见谱卡标记已按通知更新，不要求重进选歌；持久化/通知归属 [P1-H](../P1-H/DEVELOPMENT_STATUS.md)。

实现边界统一见 [TECHNICAL_CONSTRAINTS](TECHNICAL_CONSTRAINTS.md)，包括互斥计数、旧库 backfill、缺统计 fail-open 与文本范围能力。

## 最近一次验证

2026-09-29 当前产物的 `filter-final` 为 10/10 通过；shared `TestSearch` 已通过，历史通知依赖阻塞解除。`core-complete-final` 为 34 通过、2 个既有排序失败；恢复改动前 carousel 与 fixture 后同样复现这 2 个失败。BMS full 为 2394 通过、29 个既有失败、16 跳过；29 个失败的错误与堆栈和 auto-keysound 基线逐字一致，不按失败数量推定基线。Release 构建通过（0 错误、2 个原有警告），两个源码格式验证入口均通过。精确命令、产物和最终结果集中维护于[本轮验收记录](../../other/EXPERIENCE_CLOSURE_20260929.md)。人工手感、窄窗口及真实大库未签收。

## 当前代码与测试证据

- [FilterControl](../../../osu.Game/Screens/Select/FilterControl.cs)：单轨分段、共享预算、零宽入口与数值浮层；`appendRangeQuery` 仅输出启用段上限，文本 parser 不变。
- [TestSceneBmsFilterControl](../../../osu.Game.Rulesets.Bms.Tests/TestSceneBmsFilterControl.cs)：已更新测试源，覆盖默认关闭、上限匹配、完整文本范围、共享额度、零宽恢复、禁用/重启、拖拽、数值编辑和 BMS→mania→BMS 往返；迟到 cache subscriber 回归保留。
- [shared FilterControl scene](../../../osu.Game.Tests/Visual/SongSelect/TestSceneBeatmapFilterControl.cs)：已补通知依赖，以及真实标题匹配、排除和清空后恢复断言。BMS/mania 往返在能加载两套 ruleset 的 BMS scene 验证，shared scene 专注公共搜索。

## 当前风险

- 单轨/搜索回归通过，但完整测试仍有已逐项对照的基线失败；人工拖拽、窄窗口显示与真实大库体验未签收，不能将自动通过写成体验已获认可。
- 文本范围或单轨最大占比可组成合法无解条件，不额外补偿；不能把空白尾段解释成必然放宽匹配。
- 大库偶发掉帧没有现场线程数据，当前不归因；复现时先抓 `Ctrl+F11`/线程瓶颈和当场日志。

## 下一检查点

1. 人工核对单轨拖拽、零宽入口、数值提交、开关反馈、窄窗口与模式往返，确认空结果提示可理解。
2. 仅在真机大库再次复现时启动性能诊断，不做无证据优化。

## 文档治理验证

2026-10-04：主合同不再将单轨控件误列未实现，本线约束将“尾段容差”统一为可分配额度，保持真实匹配语义；元数据记忆分清标题清理与已共用的署名 resolver。仅文档核对，产品验证仍为 2026-09-29，人工交互/大库门不变，见 [CHANGELOG](CHANGELOG.md)。统一健康检查归 [主线日志](../../mainline/CHANGELOG.md#项目进度与文档记忆一致性复核)。
