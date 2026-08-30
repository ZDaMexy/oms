---
name: reference_bms_lane_keysound_timeline_bounds
description: BMS lane keysound timeline 误用 key count 导致末端 lane 丢失的诊断与修复边界
metadata:
  node_type: memory
  type: reference
---

# BMS lane keysound timeline 上界地雷（已闭合）

权威状态/计划：[P1-K STATUS](../../doc_md/subline/P1-K/DEVELOPMENT_STATUS.md)、[P1-K PLAN](../../doc_md/subline/P1-K/DEVELOPMENT_PLAN.md)；运行时验证归 [P1-J PLAN](../../doc_md/subline/P1-J/DEVELOPMENT_PLAN.md)。

## 历史诊断

`BmsBeatmapConverter.buildLaneKeysoundTimelines()` 曾用 `GetKeyCount()` 作为 lane index 上界，但 BMS topology 还包含 scratch，正确边界是 `GetLaneCount()`。旧实现会静默丢 5K K5、7K K7、14K K14 与 S2 的 lane keysound timeline；mine 构建已用 lane count，是定位该错误的关键对照。

## 当前闭合（2026-08-30）

- converter 已以 `BmsRuleset.GetLaneCount()` 为唯一上界；5K/7K 最右键、9K 全 lane、14K K14/Scratch2 均有回归。
- fixture 逐类覆盖 visible note、LN head/tail armed entry、invisible object 与相邻 mine，不能再用“timeline 总数正确”替代末端 lane 断言。
- parser-owned `BmsKeymodeResolution` 原样进入 converter/layout；layout、skin、runtime 禁止重读 BMS 或按 drawable 宽度、最高对象 lane 二次推导边界。
- native BMS player/autoplay 与 converted Mania 的 production host 已证明末端/目标 lane 请求进入同一 shared store；详细证据见 [P1-K CHANGELOG 2026-08-30](../../doc_md/subline/P1-K/CHANGELOG.md#2026-08-30)。

## 修复纪律

1. converter 上界改为 lane count，但不要顺带改 lane identity、binding、判定或 sample-pool 语义。
2. focused fixture 必须覆盖 5K K5、7K K7、9K 全 lane、14K K14/S2，不能只用 7K 中间 lane 或只断言总数。
3. P1-K 先证明 DTO/timeline 完整，production sound test 再证明玩家与 auto 路径进入同一 shared keysound store 并实际请求 source WAV；两层证据缺一不可。
4. P1-A `SV1-3` 的全 keymode topology smoke 复用这些边界样本，避免“画出了 lane 但 lane 没有 armed keysound”。

## 相邻风险

sparse 7K/9K 与 timeline 上界是两个问题：前者现由 parser-owned source/evidence/稳定诊断与host/importer显式 override seam治理；证据不足或冲突时 fail-closed，不能靠扩大数组上界或 layout 猜测掩盖。普通loader尚无终端用户纠正UI，不能把API seam误报为已交付用户能力。
