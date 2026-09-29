---
name: reference_bms_composition_filter
description: RC/LN/SCR 过滤 read-model、旧库 backfill 与 Realm/大库地雷
metadata:
  node_type: memory
  type: reference
---

# BMS 谱面构成过滤召回

权威当前态：[P1-I STATUS](../../doc_md/subline/P1-I/DEVELOPMENT_STATUS.md)；约束/历史位于同目录。

## 链路

`BmsCompositionFilterControl` → query (`rc/ln/scr/keys`) → `BmsFilterCriteria` → carousel matching。统计存 `RulesetData.chart_filter_stats`；import 时写入，旧库由 `BmsChartFilterStatsBackfill` 补齐。

- 分类互斥：SCR 优先，LN 为非 scratch long note，RC 为剩余。
- 缺 stats 时 fail-open，不隐藏谱；匹配循环不做 working-beatmap I/O。
- `ApplyVisualFilters` 不是生产入口，visual UI 编译为 query 字符串。
- 不凭历史“单轨已落”判断 UI：当前控件形态与未完成产品门只看 [P1-I STATUS](../../doc_md/subline/P1-I/DEVELOPMENT_STATUS.md)。历史 `TestRangeFilterAppliesBothBounds` 和 min/max 拖拽结果只证明当时的三行双端原型，不能挪作后续单轨验收证据。

## 单轨空结果诊断

- 三类真实占比合计 100%，但控件编辑的是各类上限：对已有构成统计的谱面，三项全启用且上限和不足 100% 必然无解，等于 100% 则只剩精确配比。看到空列表先查启用状态与条件交集，不要把尾段空白误当成会放宽匹配的容差，也不要用 fallback 隐藏真实条件。
- 缺统计的谱暂时展示可能与上述空结果不同，这是既有 fail-open 行为；先查 backfill 是否完成。
- 单轨零宽段的恢复应走固定标签入口；不能因轨道段体不可命中就断言数值丢失。

## Backfill 诊断

完整行为合同由 [P1-I CONSTRAINTS](../../doc_md/subline/P1-I/TECHNICAL_CONSTRAINTS.md) 的 read-model 章节拥有；排查时先区分以下症状：

- 每次启动都重新补算空谱：检查 `ChartFilterStatsResolved` 是否持久化，不能只检查 stats 是否为 null。
- 缓存已有值但列表仍不刷新：检查一次性 bootstrap 是否误挡迟到订阅者；缓存首次填充与回调登记不是同一个生命周期。
- 补算中周期性卡顿：按 notify/refilter 时序定位，不先提高补算并行度；阶段完成强制刷新与中间节流职责不同。

## 关键地雷

- Realm `IQueryable` 比较 `b.Ruleset.ShortName` 会翻译失败；曾被空 catch 吞掉，导致缓存恒空、Phase 2 跳过、过滤看似失效。
- 大库逐谱 `GetWorkingBeatmap` 会争进程级 cache lock，卡 UI；批处理用 external `NativeStorage` / managed child storage 直读。
- 后台 catch 必须记录日志；周期诊断用 Verbose，避免 Important 变成用户通知。
- `RulesetData` 与 converted star/难度表共享，DTO 必须保留 ExtensionData。

诊断 grep database log 的 `[BmsCompositionFilter]`；当前 full gate 看主线 STATUS，旧测试数字与实机过程查 P1-I CHANGELOG。
