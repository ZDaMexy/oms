---
name: reference_bms_gameplay_track_mute
description: BMS 选歌 preview 泄漏进 gameplay 的 mute 合同、预览策略与 backfill 地雷
metadata:
  node_type: memory
  type: reference
---

# BMS gameplay Track 静音召回

权威合同：[P1-J CONSTRAINTS](../../doc_md/subline/P1-J/TECHNICAL_CONSTRAINTS.md#bmsmania-音频合同)；当前状态见 [P1-J STATUS](../../doc_md/subline/P1-J/DEVELOPMENT_STATUS.md)。本页记录预览叠播与 backfill 的诊断经验，不另立音频或数据迁移合同。

## 根因

BMS gameplay 音频由 keysound 驱动，但 importer 曾把 `Metadata.AudioFile` 设为 song-select preview。`MasterGameplayClockContainer` 仍以 `working.Track` 作时钟并从头播放，于是 preview 叠到 native BMS/converted-mania、autoplay/玩家游玩。

## 正确修复

- core `Ruleset.PlayBeatmapTrackDuringGameplay` 默认 true，BMS override false。
- MGCC 继续使用真实 `working.Track` 作时钟，只在 adjustments 中加 `Volume=0`；离场移除，Song Select preview 恢复可听。
- mute 要在 `MusicController.ResetTrackAdjustments()` 之后添加，否则被清掉。
- gate 看 beatmap native ruleset，因此 converted-mania 也命中。

**禁止换虚拟 Track/source**：会破坏测试/真实 clock authority，且不能保证原 Track 停止；曾两次回退。

## 当前预览策略

- 只有 BMS `#PREVIEW` 写入 `Metadata.AudioFile`，`PreviewTime=0`；不再猜“≥1MB 未引用音频”为整曲。
- 旧库由一次性 `BmsPreviewAudioBackfill` 更新：启动 marker、单次 Realm snapshot、文件直读、批量写回、进度通知；崩溃无 marker 则下次幂等继续。
- backfill 禁止每候选 Realm Find/Detach，也不能每次进入 Song Select 重跑；首版曾导致 5万库持续掉帧。

虚拟 Track 单测对真实发声/静音是盲区，必须真机验证。键音侧另见 [[reference_bms_keysound_chain]]。
