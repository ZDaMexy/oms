---
name: reference_bms_lnobj_decoding
description: "BMS #LNOBJ long-note decoding: pair each tail with the immediately-preceding note (single head per lane), NOT a stack — a stack fabricates overlapping same-lane long notes ('tap inside the LN')"
metadata: 
  node_type: memory
  type: reference
  originSessionId: 7c49e0d4-4fbb-406a-a077-769f249b4a85
---

`#LNOBJ xx` long notes in `BmsBeatmapDecoder` (P1-K). The chart marks LN tails by placing the LNOBJ object value (e.g. `01`) in a key channel; the note **immediately before it in the same lane** is the LN head.

**THE rule (don't regress)**: each lane keeps **one** pending head = the single most-recent normal note (`pendingLnObjHeads: Dictionary<int,int>`). A normal note OVERWRITES the candidate (the previous note is thereby committed as a plain tap). An LNOBJ tail consumes the head and CLEARS the lane. A second consecutive tail (`note note 01 01`) then finds no head → it's an **orphan** (dropped + warning), and the earlier note stays a tap. This matches the LNOBJ spec (hitkey) and beatoraja.

**THE bug that was here (fixed 2026-06-22)**: the decoder used a per-lane **LIFO stack** (`Dictionary<int,List<int>>`) of ALL un-consumed notes. So `7O 7P 01 01`: 1st `01` pops 7P (correct), 2nd `01` pops the EARLIER 7O (wrong — 7O should already be a committed tap) → fabricates a 2nd long note `LN(7O)` that **fully overlaps/contains** `LN(7P)`. Two simultaneous holds on one lane is physically impossible; the short inner LN renders as a stray **"tap inside the long note"**. Hit on `Stella/st4/Grayed Out -Antifront-/spf.bml` ch14 ~12.3-12.9s; appeared in BOTH bms mode and converted-mania (shared decode output). Fix = stack → single head. Verified: that chart's LN count 1110→1109 (only the 1 fabricated LN removed, 0 overlaps); **user confirmed in-game 2026-06-22 (no anomalies)**.

**Diagnosis method that nailed it**: re-implemented OMS's exact LNOBJ logic in a throwaway Python script over the real `.bml` bytes (shift_jis), computed every LN's [head,tail] per lane, and flagged any same-lane temporal overlap — overlap count went 1→0 between stack and single-head logic, pinpointing the one spurious LN. Same technique works for any "is it the chart or the parser" BMS question: model the decoder, run it on the bytes, look for the impossible structure.

Coexists with P1-K CONSTRAINTS 键音呈现与控制流 #6 (LNOBJ head **removal** must be O(n) index-mark + single rebuild, not O(n²) scan) — #6 is removal, #7 is pairing; both live on `pendingLnObjHeads`. Regression: `BmsBeatmapDecoderTest.TestConsecutiveLnObjTailsDoNotFabricateOverlappingLongNote` (`#00111:AABBZZZZ` → one LN BB→ZZ + AA stays tap + orphan warning). Related LN-decoder gotcha (LNTYPE default→type1 truncation): [[reference_bms_keysound_chain]].
