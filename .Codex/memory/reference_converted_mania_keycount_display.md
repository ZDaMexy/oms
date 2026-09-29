---
name: reference-converted-mania-keycount-display
description: BMS 转 mania 键数误落入通用转谱启发式；统一使用存储的 CircleSize，避免逐面板补丁
metadata:
  node_type: memory
  type: reference
---

# BMS 转 mania 键数显示地雷

权威合同见 [P1-K CONSTRAINTS](../../doc_md/subline/P1-K/TECHNICAL_CONSTRAINTS.md) 的 K9；当前展示面见 [P1-I STATUS](../../doc_md/subline/P1-I/DEVELOPMENT_STATUS.md)。历史故障是 5K/7K/9K/14K 在 mania 选歌中显示成随 LN 密度与 OD 变化的 6K/7K。

- panel 的 `[NK]`、详情 `KC` 与 mania 键数过滤最终都经过 [ManiaBeatmapConverter.getColumnCount](../../osu.Game.Rulesets.Mania/Beatmaps/ManiaBeatmapConverter.cs)。BMS 源的 `SourceRuleset` 仍是 `bms`，不会因当前选择 mania 自动变成 `mania`。
- 原逻辑只信任 mania 的 `CircleSize`，BMS 因而落入通用转谱的 LN 比例/OD 启发式。BMS 导入时已经把 keymode 写为 `CircleSize`，不应重新猜测；现行收口同时信任 mania 与 BMS 源并返回至少 1 的取整键数。
- BMS→mania 的 `IsForCurrentRuleset=false`，14K 不走 native mania 的双舞台显示拆分。不要为修同一症状分别改 panel、wedge 或 `ManiaRuleset`，否则键数过滤会继续与显示分叉。
- BMS 当前模式下的 panel 分支只服务 native BMS 展示，不能拿它证明 converted-mania 已正确。

回归入口：[BmsToManiaBeatmapConverterTest.TestSongSelectKeyCountUsesStoredBmsKeymodeNotConvertHeuristic](../../osu.Game.Rulesets.Mania.Tests/BmsToManiaBeatmapConverterTest.cs)，覆盖 5K、7K、两种 9K 与 14K。键数与星数是不同链路；星数诊断见 [[reference_converted_star_persistence]]，表内分组见 [[reference_bms_difficulty_table]]。
