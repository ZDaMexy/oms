# P1-I 当前状态：BMS 选歌筛选与搜索

> 最后更新：2026-10-23（隐藏转谱时隐藏并清除mania难度表分组；人工门保留）
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
- mania隐藏转谱时不提供难度表分组，当前若为该项则保存为未分组；初载、从BMS切换及显示设置即时变化均适用。恢复显示只恢复入口，保留其它有效分组，BMS入口不受影响。
- 难度表归类、converted-mania 三态展示、BMS→mania 难度表分组、IIDX 难度胶囊及文件位置入口。难度表整批写入后的列表分组与可见谱卡标记已按通知更新，不要求重进选歌；持久化/通知归属 [P1-H](../P1-H/DEVELOPMENT_STATUS.md)。

实现边界统一见 [TECHNICAL_CONSTRAINTS](TECHNICAL_CONSTRAINTS.md)，包括互斥计数、旧库 backfill、缺统计 fail-open 与文本范围能力。

## 最近一次验证

2026-10-23：BMS `TestSceneBmsFilterControl` 15项、Core转谱分组4项、mania分组3项Release通过；覆盖初始旧配置、BMS→mania、Shown/ConvertedOnly→Hidden及恢复、保留其它分组与BMS入口。Desktop Debug/Release编译通过，详情见[本轮记录](CHANGELOG.md#2026-10-23mania隐藏转谱时收起难度表分组)。只改可用分组与失效选择，不改匹配、表数据或大库统计。

9月29日单轨/搜索及当时完整回归、逐项基线对照取[原验收记录](../../other/EXPERIENCE_CLOSURE_20260929.md)，不复用作本轮全仓通过或新前台体验证据。人工手感、窄窗口及真实大库仍未签收。

## 当前代码与测试证据

- [FilterControl](../../../osu.Game/Screens/Select/FilterControl.cs)：单轨分段、共享预算、零宽入口与数值浮层；`appendRangeQuery` 仅输出启用段上限，文本 parser 不变。
- [TestSceneBmsFilterControl](../../../osu.Game.Rulesets.Bms.Tests/TestSceneBmsFilterControl.cs)：已更新测试源，覆盖默认关闭、上限匹配、完整文本范围、共享额度、零宽恢复、禁用/重启、拖拽、数值编辑和 BMS→mania→BMS 往返；迟到 cache subscriber 回归保留。
- [shared FilterControl scene](../../../osu.Game.Tests/Visual/SongSelect/TestSceneBeatmapFilterControl.cs)：已补通知依赖，以及真实标题匹配、排除和清空后恢复断言。BMS/mania 往返在能加载两套 ruleset 的 BMS scene 验证，shared scene 专注公共搜索。

## 当前风险

- 本轮分组切换回归通过，未重新执行全仓测试；较早完整结果按原日期保留。人工拖拽、窄窗口显示与真实大库体验未签收，不能将自动通过写成体验已获认可。
- 文本范围或单轨最大占比可组成合法无解条件，不额外补偿；不能把空白尾段解释成必然放宽匹配。
- 大库偶发掉帧没有现场线程数据，当前不归因；复现时先抓 `Ctrl+F11`/线程瓶颈和当场日志。

## 下一检查点

1. 人工核对单轨拖拽、零宽入口、数值提交、开关反馈、窄窗口与模式往返，确认空结果提示可理解。
2. 仅在真机大库再次复现时启动性能诊断，不做无证据优化。

## 文档治理验证

2026-10-04：主合同不再将单轨控件误列未实现，本线约束将“尾段容差”统一为可分配额度，保持真实匹配语义；元数据记忆分清标题清理与已共用的署名 resolver。仅文档核对，产品验证仍为 2026-09-29，人工交互/大库门不变，见 [CHANGELOG](CHANGELOG.md)。统一健康检查归 [主线日志](../../mainline/CHANGELOG.md#项目进度与文档记忆一致性复核)。
