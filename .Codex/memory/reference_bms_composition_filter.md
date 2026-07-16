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

## Backfill 合同

1. Phase 1 先 `.AsEnumerable()` 再按 ruleset 过滤，读取 persisted stats 填缓存。
2. Phase 2 直读 `.bms`，轻量 decoder 计数，按 200 左右批量 Realm 写回并更新进度。
3. 轻量分类必须复用 `BmsBeatmapConverter.IsScratchLane`，不得复制 channel 规则。
4. 不可算/空谱也写 `ChartFilterStatsResolved` 负缓存；否则每次启动重复补算。
5. cache-updated 用订阅者列表，迟到订阅者立即收到一次；阶段完成强制刷新，中间刷新节流。

## 关键地雷

- Realm `IQueryable` 比较 `b.Ruleset.ShortName` 会翻译失败；曾被空 catch 吞掉，导致缓存恒空、Phase 2 跳过、过滤看似失效。
- 大库逐谱 `GetWorkingBeatmap` 会争进程级 cache lock，卡 UI；批处理用 external `NativeStorage` / managed child storage 直读。
- 后台 catch 必须记录日志；周期诊断用 Verbose，避免 Important 变成用户通知。
- `RulesetData` 与 converted star/难度表共享，DTO 必须保留 ExtensionData。

诊断 grep database log 的 `[BmsCompositionFilter]`；当前 full gate 看主线 STATUS，旧测试数字与实机过程查 P1-I CHANGELOG。
