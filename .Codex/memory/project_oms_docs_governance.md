---
name: project-oms-docs-governance
description: OMS mandatory documentation discipline — code changes must sync the layered doc_md governance files
metadata: 
  node_type: memory
  type: project
  originSessionId: d9f40bda-cfd5-4076-8bbf-1f08a85c9e5c
---

OMS enforces strict documentation governance under `doc_md/`. Any development that changes plan, status, constraints, or verification conclusions MUST update the owning doc in the same change — no code-only changes, no stale narrative allowed.

**Why:** The repo treats docs as authoritative governance, not afterthoughts. README explicitly forbids code-only or narrative-only changes.

**How to apply:**
- Doc layers: `doc_md/mainline` (global plan/status/changelog/constraints — authoritative), `doc_md/subline/P1-*` (per-direction, each keeps DEVELOPMENT_PLAN/STATUS/CHANGELOG/TECHNICAL_CONSTRAINTS), `doc_md/other` (external audits/release/skin/upstream refs), `doc_md/mini` (independent items).
- First decide ownership: mainline / subline / other / mini.
- Subline or mini changes that affect global priority/status/hard constraints must back-sync mainline's four-file set.
- `DEVELOPMENT_STATUS.md` holds only currently-relevant state; dated implementation slices and build/regression commands go to the same dir's `CHANGELOG.md`.
- Authoritative product-constraint doc: `doc_md/mainline/OMS_COPILOT.md` (~1500 lines — read targeted sections via Grep, don't load whole).
- Active sublines (authoritative list in CLAUDE.md / `doc_md/subline/README.md`): P1-A (product surface/release gate, skin wrap-up + F-series 素材+ini skin-authoring ecosystem 立项 2026-06-27, see [[project-oms-bms-skin-authoring]]), P1-B (input/hardware), P1-C (judge semantics + feedback loop, staged-closed; always-on feedback card removed), P1-I (BMS song-select filter/search, in I4), P1-J (gameplay runtime perf/audio timing), P1-K (parse-chain governance, staged-closed at K9; K12 converted-star fix 06-23), P1-L (gimmick/BGA visual repro — BGA chain landed, see [[reference-bms-bga-chain]]), P1-M (built-in music player — planning, see [[project-oms-music-player]]), P1-D (controller calibration, next priority); P1-E/F/G/H are support lines.
- **Known blind spot (observed 2026-06-27 health pass):** `mainline/DEVELOPMENT_STATUS.md` carries the only *volatile-snapshot* fields — the header `最后更新` date+narrative, the metrics table (BMS/mania test counts), the single `最近一次验证` block, and the Phase 1.1 matrix. These reliably go stale after subline work: contributors update the subline four-file set (and even memory) but forget to back-sync these mainline fields, so mainline ends up *behind* its own sublines. When wrapping any subline slice, explicitly re-write: latest 全量 test count, newest 最近一次验证 snapshot (keep ONE, older drop to CHANGELOG), and flip any 已修 item still parked under 后置/遗留. CHANGELOG/PLAN/OMS_COPILOT are safe — they deliberately hold no volatile snapshot (PLAN/COPILOT carry zero test numbers by design).
