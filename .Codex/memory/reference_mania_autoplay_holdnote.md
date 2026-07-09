---
name: reference-mania-autoplay-holdnote
description: Mania autoplay HoldNote landmine — top-level-only AffectsCombo filters silently drop ALL long notes; the IgnoreJudgement trap and the ManiaAutoGenerator-vs-OrderedHitPolicy asymmetry
metadata: 
  node_type: memory
  type: reference
  originSessionId: 81b931e4-f471-4017-a8a1-fa288fcd67d8
---

THE landmine: mania `HoldNote.CreateJudgement()` returns `IgnoreJudgement` (`MaxResult = IgnoreHit`, `AffectsCombo()` == **false**). A long note's combo lives entirely in its NESTED `HeadNote`/`TailNote` (both `ManiaJudgement` → Perfect → affects combo). So any predicate that filters TOP-LEVEL `Beatmap.HitObjects` by the object's own `MaxResult.AffectsCombo()` silently drops **every** long note (native mania AND BMS→mania converted — both go through the same `ManiaAutoGenerator`; converter emits BMS LN as mania `HoldNote`). Regular `Note` survives (`ManiaJudgement` affects combo).

**The asymmetry to remember:** `OrderedHitPolicy.canParticipateInLocking` (note-lock) ALSO filters by `AffectsCombo`, but it is CORRECT because it additionally walks `obj.NestedHitObjects` and applies the predicate to each — so a hold note participates via its nested head/tail. `ManiaAutoGenerator` only iterates top-level objects and does NOT descend into nested, so the SAME `AffectsCombo` predicate that's fine for note-lock breaks autoplay holds. When copying a "cross-cutting AffectsCombo contract" between these two, you MUST account for the nested walk.

**Correct autoplay predicate** (matches the fix): `o.Judgement.MaxResult.AffectsCombo() || o.NestedHitObjects.Any(n => n.Judgement.MaxResult.AffectsCombo())`. This keeps sample-only objects skipped — `BmsConvertedScratchSampleHitObject` / `BmsConvertedBgmSampleHitObject` are `IgnoreJudgement` AND have **no nested objects at all**, so they stay excluded; that "no nested" property (not the IgnoreHit alone) is what distinguishes them from a hold note.

History: the bad `canParticipateInAutoplay(o) => o.Judgement.MaxResult.AffectsCombo()` was the K9 "autoplay skips ignore-only sample objects" contract (P1-K CONSTRAINTS #12), committed inside the `4aa76f0` "P1-L Phase 2 accumulated WIP snapshot". It slipped because that commit only ran the BMS suite, not mania — the pre-existing upstream guard `TestPerfectScoreOnShortHoldNote` was silently failing. Fixed 2026-06-01 (nested-aware predicate); user-verified by manual real-play — native mania AND BMS→mania converted long-note autoplay both confirmed correct in mania mode. Guards: `osu.Game.Rulesets.Mania.Tests/Mods/TestSceneManiaModAutoplay.cs` (`TestPerfectScoreOnShortHoldNote` + `TestAutoplayHoldsLongNoteAlongsideSampleOnlyObject` lock hold participation; `TestAutoplayIgnoresSampleOnlyScratchObjects` locks sample skip).

Native BMS autoplay (`BmsAutoGenerator`) is a SEPARATE path — filters by `OfType<BmsHitObject>()`, has its own `BmsHoldNote` release branch, no `AffectsCombo` predicate — so this bug never touched it. Related: the BMS→mania sample-only objects and K11 converter chain are in [[reference-bms-keysound-chain]].
