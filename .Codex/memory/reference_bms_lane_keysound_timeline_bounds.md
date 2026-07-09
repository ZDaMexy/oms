---
name: reference_bms_lane_keysound_timeline_bounds
description: BMS lane keysound timeline 误用 key count 导致末端 lane 丢失的诊断与修复边界
metadata:
  node_type: memory
  type: reference
---

# BMS lane keysound timeline 上界地雷

权威状态/计划：[P1-K STATUS](../../doc_md/subline/P1-K/DEVELOPMENT_STATUS.md)、[P1-K PLAN](../../doc_md/subline/P1-K/DEVELOPMENT_PLAN.md)；运行时验证归 [P1-J PLAN](../../doc_md/subline/P1-J/DEVELOPMENT_PLAN.md)。

## 诊断

`BmsBeatmapConverter.buildLaneKeysoundTimelines()` 当前用 `GetKeyCount()` 作为 lane index 上界，但 BMS topology 还包含 scratch，正确边界是 `GetLaneCount()`。这会静默丢 5K K5、7K K7、14K K14 与 S2 的 lane keysound timeline；mine 构建已用 lane count，可作为对照。

## 修复纪律

1. converter 上界改为 lane count，但不要顺带改 lane identity、binding、判定或 sample-pool 语义。
2. focused fixture 必须覆盖 5K K5、7K K7、14K K14/S2，不能只用 7K 中间 lane 或只断言总数。
3. P1-K 证明 DTO/timeline 完整；P1-J 再证明玩家与 auto 路径进入同一 shared keysound store 并实际发声。
4. P1-A `SV1-3` 的全 keymode topology smoke 复用这些边界样本，避免“画出了 lane 但 lane 没有 armed keysound”。

## 相邻风险

sparse 7K/9K 可能因最高出现 channel 启发式而低估 keymode。该问题与 timeline 上界不同：前者要补 keymode source/诊断/显式纠正入口，不能靠扩大数组上界掩盖。
