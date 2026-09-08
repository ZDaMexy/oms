---
name: reference-song-select-perf
description: 大库选歌定位：受限星级触发查表、同步命中、失败负缓存、Realm与最终filter请求
metadata:
  node_type: memory
  type: reference
---

# 大库 Song Select 性能诊断

现行read-model/筛选合同见 [P1-I CONSTRAINTS](../../doc_md/subline/P1-I/TECHNICAL_CONSTRAINTS.md)，进度读 [STATUS](../../doc_md/subline/P1-I/DEVELOPMENT_STATUS.md)。以下来自历史5万级谱库诊断，不是当前benchmark或新增优化清单。

## 先定位卡在哪一段

Carousel.performFilter依次debounce→update线程snapshot→Matching/Sorting/Grouping→updateYPositions→Items ready for display。日志按op ID看最终未被取代请求；旧op被新请求取消是正常情况。

- 全范围star slider使HasFilter=false，可跳过converted difficulty lookup；收紧任意范围才触发。因此“仅受限slider无限加载”先查getStarRatings，不直接归因UI拖拽。
- matchItems本身是同步遍历；区分等待star lookup、匹配遍历、分组和最终draw/layout，不把总耗时都算到同一阶段。
- 记录实际库量、版本、操作、cache hit/miss、时延/GC与线程；不把历史毫秒数或设备上限变成永久门槛。

## 已踩过的成本与缓存坑

| 症状 | 原因与诊断 |
| --- | --- |
| 全部已persisted仍创建大量Task | Task.WhenAll(Select(async ...))即使内部同步返回也分配每谱state machine；现有sync-first路径用TryGetCachedDifficulty，仅miss进入异步，先确认是否真正走到该路径。 |
| JSON反序列化看起来很重 | BmsPersistedMetadataResolver已有按完整JSON键的parsedDataCache，converted-star写会驱逐旧键；不能再当无缓存重做。其它writer的新JSON可能留旧键，只有分配/存活证据成立才考虑有界策略。 |
| 固定某谱反复约10s取消 | DifficultyCalculator在caller传None/default时会用自身10s timeout；病态谱可能稳定超时，批量/导入无caller取消的失败要写persisted Failed，不能盲目重试。 |
| 失败谱每次又算 | MemoryCachingComponent默认不cache null。read层对已有persisted失败同步fallback，避免反复排异步；fallback显示是既有容错，不等于成功算出mania星数。 |

不要把caller请求取消也持久成计算失败；timeout、真正失败与用户取消必须按该路径合同区分。converted状态见 [[reference_converted_star_persistence]]。

## Realm 与 UI 分开查

- link-traversal predicate可能抛错或静默零结果；BmsChartFilterStatsBackfill先AsEnumerable，再用ruleset helper客户端过滤。Found N应在zero early-return前记，避免只能用缺日志猜流程。
- BmsTableGroupMode有独立分组缓存，不是parsedDataCache的owner；不要跨缓存误归因。
- 滚到极端keysound谱时的stutter曾与TextureAtlas size exceeded相关，独立于star resolution；需现场线程/日志确认。
- native BMS现在显示作者等级/胶囊，converted-mania另走星数；先确认ruleset/实际panel，再用旧“星级动画”截图安排修复。

没有现场复现就保持现状；本节点不要求重复全量benchmark。
