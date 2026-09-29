# P1-I 当前计划：BMS 选歌筛选与搜索

> 最后更新：2026-09-29（软件验证完成后保留人工验收）
> 当前事实见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，稳定筛选/read-model 合同见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)，I0～I3/I5～I7 的实现史按日期查 [CHANGELOG.md](CHANGELOG.md)。

## 子线职责

P1-I 拥有 BMS Song Select 的分组、搜索、筛选、展示层级和其同步 persisted read-model；不拥有谱库扫描、解析 truth、全局 carousel 新承诺或 gameplay/results 产品面。

## 已完成基线

| 阶段 | 结果 |
| --- | --- |
| I0 | 归线与 RC/LN/SCR 互斥语义冻结 |
| I1 | persisted 构成 read-model、import/reuse/backfill 主链 |
| I2 | BMS criteria 与完整文本搜索语法 |
| I3 实现 | BMS-only key-count 与单行单轨 composition 上限段；验证边界见 STATUS |
| I5 | 歌曲↔谱面展示层级与 BMS-local 持久化 |
| I6 | 层级返回条、Back 优先级与 scope 解耦 |
| I7 | 难度表分组解析缓存与大库基线 |

当前实现事实见 [STATUS](DEVELOPMENT_STATUS.md)，剩余工作是人工体验签收；不扩张新 filter family。

## 当前执行顺序

### 1. 人工交互与显示验收

1. 核对单行单轨拖拽手感、共享边界与窄窗口显示；零宽段必须可经固定入口编辑恢复。
2. 核对数值编辑聚焦、提交、超预算夹紧的反馈以及独立开关；禁用不改数值，重新启用恢复同一上限。
3. 核对三项全开时的无解/精确配比提示，确认玩家能理解空白只是可分配额度；默认进入不筛空列表。
4. 核对模式往返恢复各自筛选区；展示层级、返回条、scope 与 Back 原行为不因筛选改动受损。

验收：记录实际体验与尚未签收项；自动控件测试通过不等于玩家已认可手感。

### 2. 大库性能只按现场证据继续

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
