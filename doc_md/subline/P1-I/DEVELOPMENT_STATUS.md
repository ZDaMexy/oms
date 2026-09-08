# P1-I 当前状态：BMS 选歌筛选与搜索

> 最后更新：2026-09-09（代码审查与 shared visual 定点运行；未改变产品实现）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。

## 当前阶段

I1/I2 与 I5–I7 主链存在；**I3 单轨产品面尚未满足现行合同，I4 未闭合**。当前 `FilterControl` 实际为 RC/LN/SCR 三行独立双端范围 slider，既有测试也验证该实现。历史“单轨已落、只缺拖拽证明”的结论不能作为当前进度；单轨三段最大占比的产品决定保留，下一步先实现它，再补证明。

## 已落地能力

- BMS-only 分组、排序、搜索与 key-count/filter surface。
- `RC / LN / SCR` 三行范围过滤：每行独立启停、LowerBound/UpperBound，经 query 输出上下限；尚无单轨共享边界、三段总和预算或尾段容差。
- persisted `ChartFilterStats`、旧库后台 backfill、进度通知与 resolved 负缓存。
- 展示层级切换、层级返回条、难度表/内外部谱库层级分组。
- 难度表归类、converted-mania 三态展示、BMS→mania 难度表分组、IIDX 难度胶囊及文件位置入口。

## 必须保留的实现合同

- RC/LN/SCR 互斥：SCR 优先，LN 是非 scratch long note，RC 为剩余。
- Realm Phase 1 枚举必须先 `.AsEnumerable()`；禁止在 Realm `IQueryable` 上比较 link-traversal 的 `Ruleset.ShortName`。
- 旧库 Phase 2 直读 `.bms` + 轻量计数 + 批量写回；禁止逐张 `GetWorkingBeatmap` 与 UI 抢全局锁。
- 无 stats 的已处理谱也要写 `ChartFilterStatsResolved`，避免每次启动重复补算。
- 匹配 fail-open：缺 stats 不静默隐藏谱面。
- 公开文本搜索继续支持完整范围语法；visual 控件不能反向削弱文本能力。

## 最近一次验证

2026-09-09使用本轮 fresh core Release 产物运行 `TestSceneBeatmapFilterControl.TestSearch`，因未注册 `INotificationOverlay` 依赖而失败，shared visual gate 仍未通过。先修复 fixture 注入，再重跑并补 BMS/mania 切换矩阵；不能把 fixture 失败当作已定位产品搜索错误。本轮其它运行与精确命令由主线审查记录汇总，历史结果见 [CHANGELOG.md](CHANGELOG.md)。历史单轨记录或其它子线全量绿测均不构成单轨交付证明。

## 当前代码与测试证据

- [FilterControl](../../../osu.Game/Screens/Select/FilterControl.cs) 的 `BmsCompositionFilterControl` 创建 vertical flow、三个 `BmsCompositionRangeSlider`；`appendRangeQuery` 同时读取上下限。
- [TestSceneBmsFilterControl](../../../osu.Game.Rulesets.Bms.Tests/TestSceneBmsFilterControl.cs) 已有范围匹配、min/max 拖拽和迟到 cache subscriber 的 headless 用例；它们证明的是现存三行控件，不能被改称单轨 proof。
- [shared FilterControl scene](../../../osu.Game.Tests/Visual/SongSelect/TestSceneBeatmapFilterControl.cs) 仅有通用搜索用例，尚未补齐 BMS/mania 切换矩阵。criteria/backfill、层级分组、元数据和文件定位另有对应测试源。

## 当前风险

- I3 实际实现与已确认的单轨合同不符；现有双端拖拽测试不能提前关闭单轨实现或 I4。
- shared visual 的通用 `TestSearch` 当前阻于 fixture 缺少 `INotificationOverlay`；修复后才可核验搜索断言与新增分支。
- 文本范围或未来单轨最大占比可组成无解条件；结果为空是合法语义，不额外补偿。
- 大库偶发掉帧没有现场线程数据，当前不归因；复现时先抓 `Ctrl+F11`/线程瓶颈和当场日志。

## 下一检查点

1. 按既有合同把三行双端原型替换为单轨上限段，再验证共享边界、100% 填满时尾段优先压缩与数值输入。
2. 修复 `TestSceneBeatmapFilterControl` 的 `INotificationOverlay` fixture 注入并重跑 `TestSearch`，再补 BMS branch shared visual gate。
3. 仅在真机大库再次复现时启动性能诊断，不做无证据优化。

## 文档治理验证

2026-09-09按本地 production、测试源与上述失败运行更正 I3/I4 完成边界；只改文档/召回。全仓审查与实际运行结果由本次主线记录汇总，未新增视觉签收。
