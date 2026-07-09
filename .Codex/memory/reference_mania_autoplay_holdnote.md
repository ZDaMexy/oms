---
name: reference-mania-autoplay-holdnote
description: Mania autoplay/难度过滤 HoldNote 时必须检查 nested judgement
metadata:
  node_type: memory
  type: reference
---

# Mania HoldNote nested judgement 地雷

top-level `HoldNote.CreateJudgement()` 是 `IgnoreJudgement`，combo 位于 nested head/tail。任何只按 top-level `MaxResult.AffectsCombo()` 过滤的逻辑都会删除全部 HoldNote，同时原生 mania 与 BMS→mania 都受影响。

正确判据：对象自身 affects combo，或任一 nested judgement affects combo。sample-only BGM/scratch 自身 ignore 且无 nested，仍会被排除。

`OrderedHitPolicy` 因遍历 nested 所以同类 predicate 正确；`ManiaAutoGenerator` 只看 top-level 时必须显式 nested-aware。不要在二者间复制 predicate 而忽略遍历差异。

回归同时锁定：短/长 HoldNote autoplay、旁边存在 sample-only 对象、sample-only 不被 autoplay。Native BMS autoplay 是独立路径。
