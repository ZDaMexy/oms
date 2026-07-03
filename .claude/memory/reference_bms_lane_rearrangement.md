---
name: reference_bms_lane_rearrangement
description: "BMS Random/Mirror lane-rearrangement chain — triple-application landmine, single-apply contract, and mines-follow-permutation rule"
metadata: 
  node_type: memory
  type: reference
  originSessionId: 59ea0ac7-e475-4fdb-a013-f32edeb58e80
---

BMS lane-rearrangement mods (`BmsModMirror`, `BmsModRandom` = RANDOM/R-RANDOM/S-RANDOM + custom fixed pattern). Logic in `BmsLaneRearrangement` (osu.Game.Rulesets.Bms/Mods). Mirror/Random are mutually `IncompatibleMods`.

**THE landmine (fixed 2026-06-13): lane permutation must be applied EXACTLY ONCE per playable beatmap.** Both mods implement `IApplicableToBeatmap`, so `WorkingBeatmap.GetPlayableBeatmap` applies them once (proven by `BmsPlayableBeatmapCacheTest.TestPrepareScoreInfoForResults...`). The playable beatmap instance is then reused unchanged (`DrawableRuleset` base ctor does `Beatmap = (Beatmap<T>)beatmap` — strong cast, NO clone), and `BmsBeatmapModApplicator.ApplyToBeatmap` was ALSO called on it twice more — in `DrawableBmsRuleset` ctor AND `BmsScoreProcessor.ApplyBeatmap` (both fed the same `playableBeatmap` from `Player`). Lane permutations COMPOSE → the realized arrangement was P∘P∘P, not P. Consequences: custom fixed pattern corrupted (non-involution → P³≠P; a 3-cycle pattern reverts to identity = no-op); RANDOM/R-RANDOM/S-RANDOM still random-looking so the bug was invisible there; difficulty calc (1 apply = P) disagreed with gameplay (P³); Mirror survived only by odd parity (reverse³=reverse). Unit tests only single-applied AND the custom-pattern test used the full-reverse (involution) "7654321", so the suite stayed green and masked it.

**Fix:** `BmsBeatmapModApplicator` no longer applies Mirror/Random — they ride the `GetPlayableBeatmap` `IApplicableToBeatmap` pipeline (single application). The applicator keeps only idempotent state-setters (`A-SCR`/`A-NOT`) and the LongNote/Judge modes (which must also apply a DEFAULT when no mod is selected — `BmsModJudgeMode` is only `IApplicableMod`, NOT `IApplicableToBeatmap`, so the applicator is genuinely needed for it). Do NOT re-add Mirror/Random to the applicator, and do NOT make the applicator the sole path (it runs twice → even count → Mirror would silently cancel).

**Mines follow the permutation (P1-L #6, fixed same day):** `BmsMine` lives in `BmsBeatmap.Mines`, deliberately OUTSIDE `beatmap.HitObjects` (P1-L #2/#3). `BmsLaneRearrangement.applyPermutation` now remaps `Mines` `LaneIndex` with the SAME lane mapping as notes (covers Mirror/RANDOM/R-RANDOM/custom — all go through applyPermutation; per-group maps are disjoint so 14K never double-remaps). `applyScatterRandom` (S-RANDOM) has no single column permutation → mines stay put (documented edge, not a bug). Mines must NOT be moved into HitObjects.

Regression tests in `BmsLaneRearrangementModTest`: `TestMirrorMovesMinesWithLanes`, `TestRandomCustomPatternMovesMinesWithNotes`, `TestBeatmapModApplicatorDoesNotReapplyRearrangement` (simulates GetPlayableBeatmap-once + applicator-twice, asserts lanes unchanged by applicator). Related: [[reference_converted_mania_keycount_display]] (lanes vs keymode), docs under P1-L (mines/gimmick).

**Custom-pattern UX (2026-06-13):** `CustomPattern` overrides RandomMode + Seed when valid; `SettingDescription` shows only the pattern when present. Input control = `BmsRandomCustomPatternSettingsControl` — composite (filtered text box + live preview line):
- char filter via shared `BmsLaneRearrangement.IsCustomPatternCharacter` (digits + `| / , ; -` + `S`), same set the parser strips;
- live preview/validation line validates against the SELECTED chart's real key count (`BmsRuleset.TryGetKeyCount` reads `BeatmapInfo.Difficulty.CircleSize` cheaply = 5/7/9/14; resolve `[Resolved(CanBeNull=true)] IBindable<WorkingBeatmap>` like osu's `DifficultyAdjustSettingsControl`; falls back to typed digit count when no BMS chart). Green "{K}K chart → {normalised}" or red "invalid". Logic = `BmsLaneRearrangement.TryNormaliseCustomPattern(keyCount, pattern)`, kept in sync with `tryCreateCustomPatterns`.
- **14K is TWO independent 1–7 sides, NOT a permutation of 1–14** (a 7-digit side mirrors to both). 5K=1–5, 7K=1–7, 9K=1–9. The user's intuition "contains 1–14" was wrong — surface this if it recurs.
- tooltip (SettingSource description, set as `SettingsItem.TooltipText` by `CreateSettingsControls`) lists per-keymode examples; placeholder = short override reminder.
- Composite `Current` forwards directly to the text box's single bindable (preview handler is read-only) → no binding feedback loop. (Earlier composite attempts with `BindableWithCurrent` two-way handlers StackOverflowed; the `CreateSettingsControls().ToList()` smoke test catches that.)

Apply-time: a non-empty BUT invalid pattern leaves the chart UNCHANGED (no silent random fallback). Mutual-exclusion is signalled via placeholder + live preview + tooltip, NOT by disabling RandomMode/Seed (see landmine below).

**LANDMINE (proven empirically): never set `Disabled = true` on a `[SettingSource]` bindable based on another setting.** `Mod.CopyFrom` (used by clone, `ResetSettingsToDefaults`, preset/score deserialize via `APIMod.ToMod` → `CopyAdjustedSetting`) transfers values with `target.BindTo(source)`, and BindTo writes `Value` into a disabled target → `InvalidOperationException`. Mods are cloned constantly (entering gameplay), so this crashes. That's why the whole osu codebase has zero conditional-disable of SettingSource bindables. Use placeholder/description/SettingDescription/a read-only preview line for "this overrides that" instead. Full BMS suite 887/887; `osu.Desktop.slnf` Release 0/0.
