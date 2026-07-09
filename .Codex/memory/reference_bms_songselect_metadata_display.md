---
name: reference-bms-songselect-metadata-display
description: "How BMS title/difficulty-name/artist/creator are displayed in song select (both bms and converted-mania modes), via BeatmapLocalMetadataDisplayResolver"
metadata: 
  node_type: memory
  type: reference
  originSessionId: e2eb1021-fe7c-4ffa-830e-2a6241d809e4
---

BMS song-select info display (曲名/难度名/曲师/谱师) is centralized in `BeatmapLocalMetadataDisplayResolver` (osu.Game/Beatmaps, internal static). It keys on `beatmap.Ruleset.ShortName == "bms"`.

**Both modes show identically.** The carousel item's `beatmap.Ruleset` stays `bms` even under the mania ruleset (converted-mania), and the resolver keys on that — so bms-mode and converted-mania-mode display the same title/difficulty/artist/creator. The only per-mode differences are star rating and the key badge. Carousel panels (`PanelBeatmapStandalone` single-diff, `PanelBeatmapSet` + `PanelBeatmap` multi-diff) and `BeatmapTitleWedge` are shared between modes.

**Field sources:**
- 曲师 (artist): `GetDisplayArtist`/`GetDisplayArtistUnicode` — strips the BMS creator suffix (e.g. `/obj:NAME`) from `#ARTIST`.
- 谱师 (creator): `GetDisplayCreator` → `Metadata.Author.Username` (set at import from `#SUBARTIST`/`#COMMENT`/`#ARTIST` extraction); `"-"` if none.
- 曲名 (title): `GetDisplayTitle`/`GetDisplayTitleUnicode` — BMS embeds difficulty in the `#TITLE` tail bracket (`GOODBOUNCE [ANOTHER]`, `Song -HYPER-`). **Only when `#SUBTITLE` is absent**, strips the trailing paired bracket (`[]()<>` + fullwidth/CJK `（）【】〈〉［］〔〕`) or symmetric wrapper (`-X-`/`~X~`, must end with the wrapper so `Re-Loaded` is safe). Set-level row uses `BeatmapSetInfoExtensions.GetDisplayMetadataTitleRomanisable`.
- 难度名 (difficulty name): `GetDisplayDifficultyName` — priority **charter's explicit name first**: `#SUBTITLE`/title-bracket text → `#DIFFICULTY` category label (1-5 → Beginner/Normal/Hyper/Another/Insane) → drop a bare numeric play level (returns empty; star conveys the level), keep symbolic/textual stored names. The category label MUST NOT override an explicit name — real case `Dead Soul [Revive]` with `#DIFFICULTY 5` must show "Revive", not "Insane" (label-first was the initial wrong version, corrected after user test; many charts had their real names overridden by Normal/Hyper/Another/Insane).

**Key design rules** (P1-K CONSTRAINTS #21/#22, added 2026-06-13):
- DISPLAY-LAYER ONLY. Stored `Metadata.Title` and `BeatmapInfo.DifficultyName` are NOT mutated — sort/group/search still run on the raw values, MD5 is unaffected, and existing libraries get the fix with no reimport.
- Reads persisted `chart_metadata` (HeaderDifficulty/Subtitle) via core-side `BmsPersistedMetadataResolver.GetChartMetadata` (osu.Game can't reference the BMS plugin; it has its own `BmsPersistedChartMetadata` mirror that round-trips the same JSON the BMS-side `BmsChartMetadata` writes via `SetChartMetadata`).
- The central `BeatmapInfoExtensions.GetDisplayTitleRomanisable` (now-playing/results) was intentionally NOT changed — out of song-select scope. Extend later if app-wide consistency is wanted.

Stored values (import, `BmsBeatmapConverter.populateMetadata`): `DifficultyName = GetInternalLevelDisplay()` = label+playlevel or bare playlevel or "BMS"; `Title` = raw `#TITLE`. The resolver overrides these for display.

Test guard: `BmsLocalMetadataDisplayResolverTest` (incl. `TestTitleTagBeatsHeaderDifficultyLabel` = the Dead Soul [Revive] regression). Related: [[reference_converted_mania_keycount_display]], [[reference_bms_difficulty_table]].
