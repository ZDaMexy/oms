---
name: feedback-workflow
description: How the user wants work done in OMS — fix → build → focused tests → sync docs & memory; respect design contracts; respond in Chinese
metadata: 
  node_type: memory
  type: feedback
  originSessionId: d9f40bda-cfd5-4076-8bbf-1f08a85c9e5c
---

Observed working preferences for this user/repo:

- **Commit & push directly on the current branch. Never create a new branch for commits.** User doesn't use a PR workflow; branching creates friction. Just `git commit` + `git push` on whatever branch is active. (2026-05-30)


- **Language**: user writes in Chinese and explicitly asked to "说中文" — respond in Chinese for OMS work.
- **Standard task arc**: "审查 → 全量修复并验证 → 同步文档和记忆". After a code review, the user expects: implement the safe fixes, run `osu.Desktop.slnf` build + focused tests, then sync the governance docs AND memory. Treat doc+memory sync as part of "done", not optional.

**Why:** The repo enforces strict doc governance ([[project-oms-docs-governance]]) and the user follows it through to memory.

**How to apply:**
- When fixing review findings, distinguish safe refactors from design-contract changes. The user accepted NOT "fixing" intentional design (e.g. star-rating computed inside `BmsToManiaBeatmapConverter.ConvertBeatmap`, the deliberate two-stage source re-conversion) because tests/contracts depend on them — explain the tradeoff instead of blindly changing. This judgment call was validated, not corrected.
- Doc sync pattern that was accepted: add a dated entry to the owning subline CHANGELOG (e.g. `doc_md/subline/P1-K/CHANGELOG.md`), mirror a condensed entry into `doc_md/mainline/CHANGELOG.md`, replace the single "最近一次验证" snapshot in `doc_md/mainline/DEVELOPMENT_STATUS.md` (older detail already archived in CHANGELOG), and bump "最后更新" dates. Keep STATUS to current-relevant state only.
- Always include the exact verification commands + pass counts in CHANGELOG entries (matches existing style).

**Testing-loop pattern observed during K10 (2026-05-28)**: user runs the game on their real 58k+ BMS library and provides export-log bundles (`<id>.runtime.log`, `.performance.log`, etc. from `<storage>/exports/compressed-logs/`). The logs are authoritative — when the user said "可能正常" (possibly OK) but symptoms persisted, the actual log diff revealed the next bottleneck each time. Don't trust "fix should work, library too big to test in CLI" reasoning when real-library logs are available — read the perf log's `BeatmapDifficultyCache i:X h:Y m:Z` line and carousel op timings to confirm. Multiple iterations may be needed:
1. JSON deserialisation cost (per-call parse for 57k beatmaps)
2. Task allocation cost (57k async lambdas)
3. Upstream hidden timeouts (`DifficultyCalculator` internal 10s)
4. Upstream cache behaviour quirks (`CacheNullValues => false`)
Each was invisible in unit tests but obvious in real-library logs.

**Realm 20.1.0 link-traversal predicate landmine**: `r.All<BeatmapInfo>().Where(b => b.Ruleset.ShortName == "literal")` may silently return zero in Realm 20.1.0 against real datasets. The "optimization" of moving filtering server-side is NOT safe for link-traversal queries. Always filter the server-side query by translatable predicates only (`b.BeatmapSet != null`) and apply complex predicates client-side (`IsBmsBeatmap(b)`). Add a "Found N" log even when N=0 so silent regressions don't recur invisibly.
