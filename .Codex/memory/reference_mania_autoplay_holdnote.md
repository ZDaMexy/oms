---
name: reference-mania-autoplay-holdnote
description: Mania HoldNote nested过滤、note lock父对象寿命与同批松键诊断
metadata:
  node_type: memory
  type: reference
---

# Mania HoldNote nested judgement 地雷

权威合同：[P1-K CONSTRAINTS 的 K9/K12](../../doc_md/subline/P1-K/TECHNICAL_CONSTRAINTS.md)；当前状态见 [P1-K STATUS](../../doc_md/subline/P1-K/DEVELOPMENT_STATUS.md)，修复历史查 [P1-K CHANGELOG](../../doc_md/subline/P1-K/CHANGELOG.md) 2026-06-01/2026-06-23。

top-level `HoldNote.CreateJudgement()` 是 `IgnoreJudgement`，combo 位于 nested head/tail。任何只按 top-level `MaxResult.AffectsCombo()` 过滤的逻辑都会删除全部 HoldNote，同时原生 mania 与 BMS→mania 都受影响。

正确判据：对象自身 affects combo，或任一 nested judgement affects combo。sample-only BGM/scratch 自身 ignore 且无 nested，仍会被排除。

`OrderedHitPolicy`寻找下一个参与锁定的顶层对象也须考虑nested；但force-miss枚举仍只yield自身会计分的对象。把nested-aware predicate原样用于父对象force-miss，会提前判死Ignore parent，使其无法完成body，真实Player永远不completion。`ManiaAutoGenerator`的过滤与判定生命周期是不同用途，不能复制同一predicate便假定等价。

回归同时锁定：短/长 HoldNote autoplay、旁边存在 sample-only 对象、sample-only 不被 autoplay。Native BMS autoplay 是独立路径。

框架InputManager会连续Apply同批pending inputs，中间不运行DHO.Update。beginHoldAt已写Result，但IsHolding显示bindable可能尚未刷新；OnReleased读该bindable会吞同批up，断后重抓还能错给Perfect尾。真实Player冻结clock，以ManualInputManager.Key送同批物理tap，与跨Update控制比较body断条、即时combo及重抓尾上限；不要direct invoke DHO或写holding。证据归[性能与判定深审](../../doc_md/other/GAMEPLAY_PERFORMANCE_JUDGEMENT_20261019.md)。
