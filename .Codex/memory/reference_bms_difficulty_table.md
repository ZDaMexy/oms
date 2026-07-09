---
name: reference-bms-difficulty-table
description: "BMS 难度表全链 + 全 Unrated 真根因：转谱星数(BmsPersistedMetadataData)与难度表(BmsBeatmapMetadataData)共用单个 BeatmapMetadata.RulesetData 列、整体覆盖写互相抹掉 → 修复=两侧 [JsonExtensionData]；写回用注入的 RealmAccess、carousel 中途不刷新需重启等坑。P1-H CONSTRAINTS #22"
metadata: 
  node_type: memory
  type: reference
  originSessionId: 903d0da1-1c0d-4abc-b1ad-0f0df5a3760d
---

# BMS Difficulty Table — chain, THE root cause (shared RulesetData column), write-back

## Chain (osu.Game.Rulesets.Bms/DifficultyTable/)
- `BmsDifficultyTableManager`: SQLite `bms-difficulty-tables/tables.db`; 7 presets from embedded `bms_table_presets.json` (default off, no auto network; first-run import opt-in). Parses bmstable HTML `<meta bmstable>` → header.json (`data_url`) → body array; http/https/local.
- Write-back: import/refresh/enable/disable/remove → `updatePersistedBeatmaps` writes `BeatmapInfo.Metadata` via `BmsBeatmapMetadataData` (a RulesetData payload) → `TableDataChanged`.
- Consumers read PERSISTED metadata: `BmsTableGroupMode.GetGroupDefinitions` (table→level; empty entries → "Unrated" group), `BmsNoteDistributionGraph`. `BeatmapInfo.MD5Hash` + table md5 both lowercase.
- **osu.Game-side READ-ONLY consumer (2026-06-22, song-select standalone-panel classification badge)**: `BmsPersistedMetadataResolver.GetDifficultyTableEntries` (osu.Game can't reference the BMS ruleset) reads entries from the cached `BmsPersistedMetadataData.ExtensionData["difficulty_table_entries"]`; osu.Game's `BmsPersistedDifficultyTableEntry` models only `TableName/LevelLabel/Level/TableSortOrder` and is **NEVER serialised back**. LANDMINE: do NOT promote `difficulty_table_entries` to an explicit field on osu.Game's *writable* `BmsPersistedMetadataData` — the converted-star write would then re-serialise via that partial DTO and drop `Symbol`/`Md5`, re-igniting the shared-column ping-pong below. Display formatting (order by `TableSortOrder`, `/`-join level labels) lives in `BeatmapLocalMetadataDisplayResolver.GetDisplayDifficultyTableClassification`. See [[project-oms-songselect-display-nav]] 2026-06-22 + P1-I CONSTRAINTS #19.

## THE root cause (fixed 2026-05-31, 2nd pass) — "group-by-table → all Unrated, intermittently / after star recompute"
**Two independent subsystems serialize DIFFERENT container classes into the SAME `BeatmapMetadata.RulesetData` column, and `BeatmapMetadata.SetRulesetData<T>` is a whole-object overwrite (single JSON string).**
- `BmsPersistedMetadataData` (osu.Game `BmsPersistedMetadataResolver`) = `{ chart_metadata, converted_star_ratings }` — K9/K10 BMS→mania star persistence.
- `BmsBeatmapMetadataData` (BMS ruleset `DifficultyTable/`) = `{ difficulty_table_entries, chart_metadata, chart_filter_stats }`.

Newtonsoft deserialize drops unknown members by default → each subsystem's write WIPES the other's exclusive fields:
- Converted-star recompute → `getPersistedData()` deserializes as `BmsPersistedMetadataData` → `difficulty_table_entries`/`chart_filter_stats` dropped → write-back → **all Unrated** (user saw this right after a "Reprocessing converted star rating (N of 11336)" run).
- Difficulty-table write → deserializes as `BmsBeatmapMetadataData` → `converted_star_ratings` dropped → next launch sees star "missing" → recomputes ~11k → wipes table again. **Destructive ping-pong**; "intermittent" = whichever wrote last.

**Fix**: `[JsonExtensionData] public IDictionary<string, JToken>? ExtensionData` on BOTH container classes → unknown fields round-trip preserved (Newtonsoft flattens extension data back to top level on serialize). CRITICAL: `BmsBeatmapMetadataData.IsEmpty` must also count `ExtensionData` — else `SetDifficultyTableEntries(empty)` → `SetRulesetData(null)` nulls the column and wipes the star payload. Two-direction regressions: `BmsDifficultyTableManagerTest.TestDifficultyTableWriteBackPreservesForeignRulesetDataFields` + `BmsStarRatingResolverTest.TestConvertedStarRatingWritePreservesDifficultyTableFields`.

**General rule (enforce going forward)**: the single `RulesetData` column is shared across subsystems AND assemblies. Any new BMS RulesetData payload MUST either share one container or carry `[JsonExtensionData]`. P1-H CONSTRAINTS #22.

**Verified (user, 2026-05-31)**: after the fix, converted-star recompute no longer recurs on an unchanged library (the ping-pong is broken — this is the definitive confirmation) and difficulty-table grouping is correct. RESIDUAL: a new build only prevents FUTURE overwrites — entries already wiped by the old build need ONE `disable→enable` of any table to rewrite (one mutation rewrites all matched beatmaps' full entries since `updatePersistedBeatmaps` uses the complete enabled-table lookup), then it sticks.

## Diagnosis playbook for "all Unrated"
- Restart → still Unrated? = persisted entries missing (this root cause, or never written). Fine after restart = a refresh/cache (carousel staleness) issue, NOT this.
- A "Reprocessing converted star rating (N)" notification firing on an unchanged library = the ping-pong (a table write wiped the star payload, forcing recompute). Must NOT happen after the extensionData fix.

## Write-back architecture (kept) / carousel staleness (deferred, NOT the main bug)
- Write-back uses the INJECTED global `RealmAccess` (never `new` a 2nd — its ctor's `cleanupPendingDeletions` over-reaches + races the global). `GetShared(storage, realmAccess)`; settings/first-run reflection bridge/importer pass it. enable/disable/remove have async entry points (off the update thread). MD5 normalized lowercase; `loadTableSource` depth cap; import/refresh build their return from in-hand data (no `GetSources().Single`).
- **Carousel mid-session staleness is intentionally NOT fixed**: difficulty-table data lives on the deep `Beatmaps.Metadata` link property; `RealmDetachedBeatmapStore` subscribes shallow `All<BeatmapSetInfo>()` (no keyPaths) so deep writes don't trigger re-detach. The earlier per-set `BeatmapSetInfo.DifficultyTableRevision` bump (realm schema 55) to force re-detach was **REVERTED**: at 5.7万-lib + 万级-entries a single table toggle matches thousands of sets → thousands of per-set re-detaches → 8万 update-reads + 2000 pending scheduler tasks → 1–2 min UI freeze (user-confirmed). Column retained (schema 55, currently unused) to avoid another migration. **Mid-session table enable/disable reflects in grouping after EXITING & RE-ENTERING song select (user-verified — lighter than a full app restart), or a restart; startup is always correct.** Also: the write-back itself still costs ~1 min at 5.7万 lib because `updatePersistedBeatmaps` does `realm.All<BeatmapInfo>().AsEnumerable().Where(...)` (full-table client-side filter + writes thousands of matched beatmaps; the #1 perf item — runs on a background thread so the UI isn't hard-blocked but frame rate dips; music keeps playing). Proper future fix = group via an in-memory MD5 index (live lookup) + one-shot re-filter, not persisted-metadata + carousel re-detach (and push the match filter down to realm instead of AsEnumerable).

## Test landmines
- `BmsDifficultyTableManagerTest` needs a real `RealmAccess` per test: `using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME)` right after `using var storage` (dispose order realm-then-storage). Don't use all-numeric MD5 in write-back regressions (hides casing + the field-overwrite bug). Full `osu.Game.Rulesets.Bms.Tests` = 869/869; `BmsStarRatingResolverTest` 13/13 (2026-05-31).
