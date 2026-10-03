---
name: reference_bms_lane_keysound_timeline_bounds
description: BMS lane keysound timeline 误用 key count 导致末端 lane 丢失的诊断与修复边界
metadata:
  node_type: memory
  type: reference
---

# BMS lane keysound timeline 上界地雷

权威状态/计划：[P1-K STATUS](../../doc_md/subline/P1-K/DEVELOPMENT_STATUS.md)、[P1-K PLAN](../../doc_md/subline/P1-K/DEVELOPMENT_PLAN.md)；修复与 production host 证据见 [2026-08-30 历史](../../doc_md/subline/P1-K/CHANGELOG.md#2026-08-30)，runtime 回归和真实谱 smoke 归 [P1-J](../../doc_md/subline/P1-J/DEVELOPMENT_STATUS.md)。

## 历史诊断

`BmsBeatmapConverter.buildLaneKeysoundTimelines()` 曾用 `GetKeyCount()` 作为 lane index 上界，但 BMS topology 还包含 scratch，正确边界是 `GetLaneCount()`。旧实现会静默丢 5K K5、7K K7、14K K14 与 S2 的 lane keysound timeline；mine 构建已用 lane count，是定位该错误的关键对照。

## 回归诊断

- 定位末端丢声时对照 5K K5、7K K7、9K 全 lane、14K K14/S2；逐类查 visible、LN head/tail armed、invisible 与相邻 mine，不能只查中间 lane 或 timeline 总数。
- DTO/timeline 完整和 production 玩家/auto 实际请求 source WAV 是两层证据：画出 lane 不证明它有 armed keysound；跨玩法仍要核同一 shared store。
- 上界来自 parser-owned keymode，不能通过扩大数组、drawable 宽度或最高对象 lane 二次猜测来掩盖问题；改边界不顺带改变 lane identity、binding、判定或 sample-pool。

## 相邻风险

sparse 7K/9K 与 timeline 上界是两个问题：前者现由 parser-owned source/evidence/稳定诊断与host/importer显式 override seam治理；证据不足或冲突时 fail-closed，不能靠扩大数组上界或 layout 猜测掩盖。普通loader尚无终端用户纠正UI，不能把API seam误报为已交付用户能力。
