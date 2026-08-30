---
name: reference_bms_lane_rearrangement
description: BMS Mirror/Random 单次应用、地雷随置换与 custom-pattern UI 地雷
metadata:
  node_type: memory
  type: reference
---

# BMS lane 重排召回

## 单次应用合同

Mirror/Random 实现 `IApplicableToBeatmap`，由 `GetPlayableBeatmap` 应用一次。`BmsBeatmapModApplicator` 不得再次应用；playable 会被 DrawableRuleset/ScoreProcessor 复用，重复调用会组合成 P³。Mirror 因 reverse³=reverse 会掩盖 bug，custom 3-cycle 才能暴露。

Applicator 只保留需要默认值/状态设置的 judge、LN、A-SCR/A-NOT 等幂等逻辑。

## Armed timeline 与 LaneId 合同（2026-08-30 闭合）

- lane group/keymode 只能来自 parser-owned `BmsBeatmap.BmsInfo.Keymode`；非 `BmsBeatmap` 必须 fail-closed，不得按最高 hitobject lane、总 lane 数或 layout 宽度猜测。
- Mirror/RANDOM/R-RANDOM/custom pattern 有单一 bijective permutation：playable object、mine 与 `LaneKeysoundTimelines` 必须用同一 exact mapping 搬移。固定 playfield topology 与稳定 LaneId 集合不变，变化的是对象的 post-mod target lane；keysound 与 skin lookup 最终必须落到该 target 的同一 LaneId。
- S-RANDOM 按对象/时间分配，没有可诚实应用于 armed timeline 的单一 permutation：保留 scratch/非受影响 group，只禁用散列 group 的 armed timeline并写稳定 token `bms.keysound.timeline.disabled-s-random`。对象自身 WAV 不丢，player/autoplay 仍从 post-mod lane 进入 shared store；不得把禁用 token 误称为 timeline 搬移。
- 回归不能停在 DTO：production test 要从 decode/converter 经 mod、完整 drawable ruleset/skin snapshot 到真实 lane/shared store，断 source WAV、target LaneId 与 skin lane 一致。最终证据见 [P1-K CHANGELOG 2026-08-30](../../doc_md/subline/P1-K/CHANGELOG.md#2026-08-30)。

## Mines

`BmsMine` 在 `BmsBeatmap.Mines`，不进 `HitObjects`。Mirror/RANDOM/R-RANDOM/custom 通过同一 permutation 映射 mine lane；S-RANDOM 没有单一 column permutation，mine 保持原位。

## Custom pattern

- 5K=1–5、7K=1–7、9K=1–9；14K 是两个独立 1–7 side，不是 1–14。
- 非空但非法 pattern 保持谱面不变，不静默回退随机。
- SettingSource bindable 不要条件性 `Disabled=true`：mod clone/CopyFrom 的 BindTo 会向 disabled target 写值并抛异常。用 placeholder、说明和只读 preview 表达 override。
- composite control 的 `Current` 直接转发单一 text bindable，避免双向 handler 递归。

回归至少包含：applicator 不重排、custom 非 involution、mine 随置换、14K 双侧。历史见 P1-L/P1-C CHANGELOG。
