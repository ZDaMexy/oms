# P1-I 当前计划：BMS 选歌筛选与搜索

> 最后更新：2026-07-16
> 当前事实见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，稳定筛选/read-model 合同见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)，I0～I3/I5～I7 的实现史按日期查 [CHANGELOG.md](CHANGELOG.md)。

## 子线职责

P1-I 拥有 BMS Song Select 的分组、搜索、筛选、展示层级和其同步 persisted read-model；不拥有谱库扫描、解析 truth、全局 carousel 新承诺或 gameplay/results 产品面。

## 已完成基线

| 阶段 | 结果 |
| --- | --- |
| I0 | 归线与 RC/LN/SCR 互斥语义冻结 |
| I1 | persisted 构成 read-model、import/reuse/backfill 主链 |
| I2 | BMS criteria 与完整文本搜索语法 |
| I3 | BMS-only composition/key-count visual filter |
| I5 | 歌曲↔谱面展示层级与 BMS-local 持久化 |
| I6 | 层级返回条、Back 优先级与 scope 解耦 |
| I7 | 难度表分组解析缓存与大库基线 |

当前只收口 I4 自动/视觉证明与现场性能证据，不扩张新 filter family。

## 当前执行顺序

### 1. 共享边界拖拽 headless proof

1. 覆盖 `BmsCompositionHandle` 三段共享边界拖拽，保证 RC/LN/SCR 独立启停与最大占比语义不漂移。
2. 覆盖总和达到 100% 时尾段优先压缩，以及跨边界、零宽、禁用段和重新启用。
3. visual control 与文本 criteria 必须对同一集合给出一致结果；控件不能反向削弱完整范围语法。
4. 缺失 stats 继续 fail-open，不因 headless fixture 变成静默隐藏。

验收：纯 headless 断言覆盖拖拽状态、criteria 输出和 bindable 往返，不依赖人工像素观察。

### 2. shared visual gate

1. 在 `TestSceneBeatmapFilterControl` 覆盖 BMS branch：composition/key-count 行显示，mania 保持原 star surface。
2. 切换 ruleset 后 visual state、criteria、persisted BMS setting 不串线。
3. 展示层级锁定、返回条、scope 与 Back 优先级只补当前缺口，不重写 carousel host。

验收：共享 visual test 可重复通过；BMS-only 行不占用 mania/其它 ruleset 布局 authority。

### 3. 大库性能只按现场证据继续

1. 仅在当前版本再次复现掉帧时采集 `Ctrl+F11`、线程/GC、refilter/backfill 阶段与当场日志。
2. 先区分 Realm、直读 backfill、JSON/grouping、carousel draw 或其它 owner，再确定最小切片。
3. 不在过滤阶段逐谱 `GetWorkingBeatmap`、重跑 analyzer 或持有全局锁。
4. 结果为空可以是三个最大占比组成的合法无解条件，不新增补偿语义掩盖真实筛选。

验收：同一大库、同一操作给出前后时延和结果一致性；无证据时保持现状。

## 必须保持的边界

- SCR 优先；LN 是非 scratch long note；RC 是剩余 playable object，三者互斥。
- Realm Phase 1 先 `.AsEnumerable()`，不在 Realm `IQueryable` 上比较 link-traversal 的 ruleset short name。
- Phase 2 直读 `.bms` 轻量计数并批量写回；处理过但无 stats 的谱仍要持久化 resolved 标记。
- text search 是完整能力，visual controls 只是安全子集。
- 不新建 per-ruleset `FilterControl` host，不扩 mania UI，不把本线变成选歌总体重构。
