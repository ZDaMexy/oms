---
name: reference-song-select-perf
description: "Song-select carousel performance lessons for 50k+ beatmap libraries — what's actually slow, what the upstream landmines are, and the patterns that fix it"
metadata: 
  node_type: memory
  type: reference
---

权威当前态见 [P1-I STATUS](../../doc_md/subline/P1-I/DEVELOPMENT_STATUS.md)，converted-star合同见 [[reference_converted_star_persistence]]。下列是历史大库诊断方法，不是当前版本的新benchmark或尚未实现清单。

## Carousel filter pipeline at a glance

`Carousel.performFilter` (osu.Game/Graphics/Carousel/Carousel.cs) runs filters serially:
1. Debounce (`DebounceDelay` ~100ms)
2. Snapshot items on update thread
3. `Task.Run` → for each filter (`BeatmapCarouselFilterMatching`, `Sorting`, `Grouping`) → log "Performing X" then `await filter.Run()`
4. `updateYPositions` → "Items ready for display"

`BeatmapCarouselFilterMatching.Run` (osu.Game/Screens/Select/BeatmapCarouselFilterMatching.cs):
1. `requiresStarRatingLookup(criteria)` decides whether to call `getStarRatings` — true when `criteria.AllowConvertedBeatmaps && (StarDifficulty.HasFilter || UserStarDifficulty.HasFilter)`. False → uses `empty_star_ratings` and skips lookup entirely. **Critical**: an unrestricted star slider (full range) leaves `HasFilter` false and bypasses lookup; ANY restricted range engages the lookup path. This is why "infinite loading" only appears with restricted slider values when the lookup is slow.
2. `await getStarRatings(...)` builds the per-beatmap dict.
3. `Task.Run` → `matchItems` iterates all items synchronously calling `CheckCriteriaMatch` per beatmap. No cancellation token threaded into the iteration — once started it runs to completion.

## The four landmines at 57k BMS scale

### 1. Per-beatmap async Task allocation in getStarRatings
`Task.WhenAll(beatmaps.Select(async b => await GetDifficultyAsync(b)))` allocates 57k async state machines + Tasks **even when every call returns `Task.FromResult` synchronously**. Use **sync-first**: iterate with `TryGetCachedDifficulty` (sync) collecting misses into a side list, then `Task.WhenAll` only the misses. For a fully persisted library, zero Task allocations.

### 2. JSON deserialization per lookup
`BmsPersistedMetadataResolver.getPersistedData`已有按完整JSON内容键缓存的 `parsedDataCache`；converted-star写入会驱逐旧JSON键，不能再次以“尚无解析缓存”为由重复实现。缓存是静态dictionary，来自其它metadata写方的新JSON仍可能留下旧键；若现场确有增长，再按分配/存活证据确定有界策略。BmsTableGroupMode另有自身分组定义缓存，两者不是同一个owner。

### 3. DifficultyCalculator's hidden 10-second internal timeout
`DifficultyCalculator.Calculate(IEnumerable<Mod>, CancellationToken)` does:
```csharp
using var timedCancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
if (!cancellationToken.CanBeCanceled)
    cancellationToken = timedCancellationSource.Token;
```
If you pass `default`/`None`, you silently get a 10s cap. BMS charts with pathological event timelines genuinely exceed this. The `OperationCanceledException` is deterministic per beatmap, not transient — in batch and import-time paths that don't pass a token, persist it as `Failed` (`BmsPersistedMetadataResolver.SetConvertedStarRatingFailure`) so consumers can short-circuit.

### 4. MemoryCachingComponent doesn't cache null
`BeatmapDifficultyCache` (and `MemoryCachingComponent` upstream default behaviour here) has `CacheNullValues => false`. Failed computes return null and are NOT cached. Every subsequent lookup re-runs the slow compute. **Workaround at the read layer**: `tryGetImmediateDifficulty` synchronously returns a fallback whenever persisted state exists (even if Failed), so the carousel never queues async compute for known-failing beatmaps. For BMS→mania, the fallback is `beatmapInfo.StarRating` (the BMS playlevel) — acceptable cosmetic compromise for the handful of charts that fail.

## Realm landmines

- Realm link-traversal predicate可能报错或静默零结果；先materialize，再用ruleset helper做客户端过滤。当前 `BmsChartFilterStatsBackfill.EnumerateBmsBeatmaps`明确先 `.AsEnumerable()`，不要按旧记忆误写成仍依赖Realm link predicate。
- **Always log `Found N` BEFORE the early-return on zero**. Otherwise "notification didn't appear" debugging requires correlating absence-of-log with logic flow.

## Carousel UI follow-ups (panel layer, not data layer)

These were observed in 58k testing but are independent UI concerns:
- BMS当前显示作者等级/难度胶囊，converted-mania另走mania星数；先确认实际ruleset和显示分支，再判断数字动画是否仍是当前产品问题，不凭旧星级panel截图安排修复。
- Extreme charts (huge keysound count, stress-test maps) trigger noticeable stutter when scrolling to them — correlates with `TextureAtlas size exceeded` messages in performance log. Independent of star/difficulty resolution.

## How to verify perf after touching this area

- 对比同一库/版本/操作的difficulty-cache命中与miss；已persisted BMS应主要走同步读取，具体次数不作为跨设备固定门槛。
- `Carousel[op X] ... Items ready for display`用于确认最终未被取代的过滤请求完成；被新请求取消的旧op可以正常取消。记录实际库量、时延/GC与线程，不复用历史毫秒数当当前性能结论。
- Filter ops in log should reach `Performing FilterMatching → FilterSorting → FilterGrouping` in sequence, not stall between phases.
