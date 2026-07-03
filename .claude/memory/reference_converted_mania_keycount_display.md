---
name: reference-converted-mania-keycount-display
description: "Why converted-mania key-count display was wrong/varied in song select, and the single-point fix (trust CircleSize for bms source)"
metadata: 
  node_type: memory
  type: reference
  originSessionId: e2eb1021-fe7c-4ffa-830e-2a6241d809e4
---

Converted-mania (BMS shown under the mania ruleset) song-select **key-count display** was wrong and "五花八门" (varied) — fixed 2026-06-13.

**Symptom**: in mania ruleset, the carousel `[NK]` badge and the wedge/details `KC` attribute bar showed key counts unrelated to the real keymode — any 5K/7K/9K/14K collapsed to **6 or 7**.

**Chain**: `PanelBeatmap`/`PanelBeatmapStandalone.updateKeyCount` (the `ruleset.OnlineID == 3` branch) calls `ManiaRuleset.GetKeyCount`; the wedge `KC` bar calls `ManiaRuleset.GetBeatmapAttributesForDisplay`. Both → `ManiaBeatmapConverter.GetColumnCount` → private `getColumnCount`. That function only trusts `CircleSize` directly when `SourceRuleset.ShortName == "mania"`. A stored BMS `BeatmapInfo.Ruleset` is `bms` (via `LegacyBeatmapConversionDifficultyInfo.FromBeatmapInfo` → `beatmapInfo.Ruleset`), so it fell into the osu!stable **convert column heuristic** (long-note ratio + OD).

**Root cause / why "varied"**: OMS deleted osu/taiko/catch, so the only rulesets are mania + bms → the heuristic branch is **only ever hit by BMS, and is always wrong**. For BMS, stored `CircleSize ∈ {5,7,9,14}` is always `>= 5`, so: `percentSpecial < 0.2 -> 7`, else `OD > 5 ? 7 : 6`. Result is always 6/7, flipping with LN density + rank. It's dead-but-harmful code.

**Fix (single point)**: in `ManiaBeatmapConverter.getColumnCount`, treat `bms` source like `mania` — return `(int)Math.Max(1, roundedCircleSize)`. BMS `CircleSize` is authoritative keymode columns (set by `BmsBeatmapConverter.populateMetadata` = `BmsRuleset.GetKeyCount(keymode)`, 5/7/9/14). Because badge, `KC` bar, AND `ManiaFilterCriteria` key filter all go through `GetColumnCount`, one change fixes all three. `14K` here returns 14 (IsForCurrentRuleset=false for bms→mania, so no MAX_STAGE_KEYS dual-split), matching native BMS display.

**Did NOT** edit panels/wedge: the panels' existing `else if (ruleset.ShortName == "bms" ...)` branch only serves the bms-ruleset-selected case (`OnlineID != 3`) and stays. Per-surface special-casing would spread "bms" knowledge into more files (incl. `ManiaRuleset` for the wedge) — messier.

Constraint: P1-K TECHNICAL_CONSTRAINTS **K9 #18** (sibling of #11 which governs converted **star** display going through resolved lookup). Test guard: `BmsToManiaBeatmapConverterTest.TestSongSelectKeyCountUsesStoredBmsKeymodeNotConvertHeuristic` (5K/7K/9K_Bms/9K_Pms/14K). Related: [[reference_converted_star_persistence]], [[reference_bms_difficulty_table]].
