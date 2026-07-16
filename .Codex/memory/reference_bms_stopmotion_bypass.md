---
name: reference-bms-stopmotion-bypass
description: "P1-L Phase 2 gimmick 谱忠实渲染：BmsScrollProfile D(t) 积分器、IScrollingInfo re-cache 注入(零核心改动)、GimmickScrollMode 门控、GetMostCommonBeatLength=6 标定坑 + 为何 Normal 模式本就忠实"
metadata:
  type: reference
---

# BMS stop-motion scroll bypass (P1-L Phase 2)

Faithful rendering of stop-motion gimmick charts (DEAD SOUL [Revive]) — extreme-BPM snap, true STOP freeze, measure-length placement — without breaking the normal forward-scroll chain.

## Architecture（已落地；当前默认 Auto）
- **Position integrator** `BmsScrollProfile` (osu.Game.Rulesets.Bms/Beatmaps): pure piecewise-linear `D(t)` (DistanceAt / PositionDelta / TimeAtDistance, binary search + end extrapolation). Built in `BmsBeatmapConverter.buildEventTimeline` by accumulating distance **in parallel with the existing time-walk**, using **raw UNCLAMPED** BPM/STOP/measure-length/scroll. STOP region → `dD=0` (freeze); extreme BPM → steep slope (snap). Attached to `BmsBeatmap.ScrollProfile`; never enters `HitObjects`.
- **Algorithm** `BmsStopMotionScrollAlgorithm : IScrollAlgorithm` (UI/Scrolling): same form as `ConstantScrollAlgorithm` but with chart time replaced by `D(t)`. For a normal chart `D(t)≈t` so it degenerates to constant scroll.
- **Injection = zero core changes**: `BmsScrollingInfo : IScrollingInfo` wraps the base ruleset info (Direction/TimeRange pass through; Algorithm `GetBoundCopy`-follows base instance-for-instance until engaged). `BmsPlayfield.CreateChildDependencies` re-caches it via `CacheAs<IScrollingInfo>` so lanes resolve the BMS one. This **bypasses** (does NOT modify) the shared `TimingControlPoint [6,60000]` clamp and `ScrollingHitObjectContainer`. Guarded: if no base IScrollingInfo, skip re-cache (parent-less playfield keeps base behaviour).
- **Gate** `BmsGimmickScrollMode {Off,On,Auto}` + `BmsRulesetSetting.GimmickScrollMode`; settings dropdown「演出谱滚动（实验性）」. `BmsPlayfield.updateGimmickScroll` engages/disengages. Judgement/scoring stay on `HitObject.StartTime` time path — bypass only does visual positioning.
- **Auto-detection (Step D)**: `BmsScrollProfile.MaxSlope` (fastest segment speed vs base: base≈1, STOP=0, DEAD SOUL snap≈10000) + `FrozenFraction` (STOP freeze % of timeline) → `IsStopMotionGimmick = MaxSlope>=50 || FrozenFraction>=0.05` (conservative; normal/moderate-soflan stay well under). `Auto` engages only when true. **Default gate is `Auto`** (user decision): gimmick/soflan charts work out of the box, normal charts don't match detection → unchanged. `Off` is the hard fallback (settings hint tells users to switch to Off if issues). With default Auto, the "normal charts unchanged" guarantee relies on the detector having NO false positives — keep thresholds conservative; re-evaluate regression before loosening.
- **Lane-bound landmine (fixed 2026-05-29)**: `BmsBeatmapConverter.buildMines` must bound laneIndex by `BmsRuleset.GetLaneCount` (keys+scratch: 7K=8) NOT `GetKeyCount` (keys: 7) — scratch on lane 0 shifts keys to 1..n, so the rightmost key = lane index keyCount and was wrongly dropped. `BmsLaneLayout.getExpectedLaneCount` delegates to `GetLaneCount` (single source of truth).

## Landmines / calibration (verified on real DEAD SOUL)
- **`GetMostCommonBeatLength()` returns 6 (BPM 10000) for DEAD SOUL**, NOT 132 — because STOP-freeze points (beatLength 6) occupy ~43% of the timeline and extreme-BPM points clamp to 6, dominating the duration-weighting. This is the documented squash (normal path renders 132 sections at multiplier ~0.013).
- **baseBeatLength = raw most-common BPM by NON-STOP time** (`computeBaseBpm`), i.e. 132 → 454.5, NOT the clamped GetMostCommonBeatLength. Do NOT switch base to 6 to "align" — that reproduces the squash.
- **Why it's faithful anyway in Normal hi-speed mode (default)**: `BmsHiSpeedRuntimeCalculator.ComputeBaseTimeRange` Normal mode has modeScale=1, so `timeRange` is independent of GetMostCommonBeatLength; with D≈t at base 132, base sections render at the same speed as a normal 132 chart. Floating/Classic modes scale timeRange by GetMostCommonBeatLength(=6) → absolute-scale mismatch, deferred to Phase 4 calibration.
- Distance accumulated in scroll-weighted beats during the walk, multiplied by baseBeatLength AFTER (global constant; base BPM only known after full walk).

## Verification
验证必须覆盖 BMS relevant/full、Release，以及 OFF 模式逐实例跟随 base 的 `BmsScrollingInfoTest` 和 Player-based gameplay TestScenes（用于触发 DI re-cache）。历史数字查 P1-L CHANGELOG，不能复用为当前 gate。

## Pending
DEAD SOUL frame-by-frame visual acceptance vs beatoraja (Phase 4, human — user's initial run looked right); Floating/Classic absolute-scale calibration; negative/reverse scroll (Phase 3); extreme-chart object pooling (P1-J).
