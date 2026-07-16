---
name: reference-song-select-perf
description: "Song-select carousel performance lessons for 50k+ beatmap libraries — what's actually slow, what the upstream landmines are, and the patterns that fix it"
metadata: 
  node_type: memory
  type: reference
---

Hard-won lessons from getting OMS song select usable with a 58k+ BMS + 28k mania library (K10 second slice, 2026-05-28). Concrete bottlenecks and the patterns that fixed them — keep these in mind when touching anything carousel/filter/difficulty-cache adjacent.

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
`BmsPersistedMetadataResolver.getPersistedData` calls `JsonConvert.DeserializeObject<BmsPersistedMetadataData>(metadata.RulesetDataJson)` on each access. For 57k BMS lookups per filter op, deserialization dominates. Mitigation explored but not implemented in K10 — the sync-first refactor + immediate-path optimisation made it tolerable. If a future regression points here, a `ConcurrentDictionary<string, BmsPersistedMetadataData>` keyed on the JSON content is the obvious cache.

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

- **Link-traversal predicate translation is unreliable in Realm 20.1.0**. `r.All<BeatmapInfo>().Where(b => b.Ruleset.ShortName == "bms")` silently returned zero matches against a real 58k-beatmap library. Filter `b.BeatmapSet != null` server-side, then call the helper (`BmsStarRatingResolver.IsBmsBeatmap(b)`) client-side. This DID work the same way in `BmsChartFilterStatsBackfill.cs` and `BmsDifficultyTableManager.cs` but those use `.ToList()` which may force materialisation early — risk profile differs.
- **Always log `Found N` BEFORE the early-return on zero**. Otherwise "notification didn't appear" debugging requires correlating absence-of-log with logic flow.

## Carousel UI follow-ups (panel layer, not data layer)

These were observed in 58k testing but are independent UI concerns:
- High-difficulty star numbers display via an incrementing sprite-text animation on panels. Expected: instant display. Fix would live in `PanelBeatmapStandalone` / star-counter component, not in any data path.
- Extreme charts (huge keysound count, stress-test maps) trigger noticeable stutter when scrolling to them — correlates with `TextureAtlas size exceeded` messages in performance log. Independent of star/difficulty resolution.

## How to verify perf after touching this area

- `BeatmapDifficultyCache: i:X h:Y m:Z N%` performance log line: for a fully-persisted BMS library, expect `i < 20, h+m < 30`. Most BMS lookups should NOT touch this cache (they hit the sync immediate path).
- `Carousel[op X] N ms: Items ready for display`: every filter op should reach this line, not "Cancelled". N should be <1000ms for an 86k library on modern hardware.
- Filter ops in log should reach `Performing FilterMatching → FilterSorting → FilterGrouping` in sequence, not stall between phases.
