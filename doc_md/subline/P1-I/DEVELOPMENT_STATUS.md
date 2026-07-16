# P1-I 当前状态：BMS 选歌筛选与搜索

> 最后更新：2026-07-16（文档健康治理；功能状态未改变）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。

## 当前阶段

I1–I3 与 I5–I7 主功能已落地；I4 focused regression 仍未完全闭合。当前不继续扩张产品面，优先补单轨拖拽 headless 覆盖、shared visual gate 和大库现场证据。

## 已落地能力

- BMS-only 分组、排序、搜索与 key-count/filter surface。
- `RC / LN / SCR` 单轨构成过滤：三段独立启停，表示各自最大占比，尾段为空白容差。
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

## 当前验证

- 全局最新产品验证统一见 [mainline STATUS 的“最近一次验证”](../../mainline/DEVELOPMENT_STATUS.md#最近一次验证)；2026-07-16 仅治理文档，未运行产品测试或 Release。
- importer/statistics/criteria/UI 的本线历史 focused/full 数字只保留在 [CHANGELOG.md](CHANGELOG.md)，不冒充当前全局 gate。

## 当前风险

- `BmsCompositionHandle` 共享边界拖拽仍缺 headless 自动化。
- 三个最大占比可组成无解条件；结果为空是合法语义，不额外补偿。
- 大库偶发掉帧没有现场线程数据，当前不归因；复现时先抓 `Ctrl+F11`/线程瓶颈和当场日志。

## 下一检查点

1. 为共享边界拖拽、100% 填满时尾段优先压缩补 headless 断言。
2. 补 `TestSceneBeatmapFilterControl` BMS branch shared visual gate。
3. 仅在真机大库再次复现时启动性能诊断，不做无证据优化。
