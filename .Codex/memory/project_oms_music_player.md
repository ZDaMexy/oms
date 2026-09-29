---
name: project_oms_music_player
description: P1-M 全局音轨与选歌试听共用控制器、BMS PREVIEW 输入及队列接入地雷
metadata: 
  node_type: memory
  type: project
---

权威进度与未完成门见 [P1-M STATUS](../../doc_md/subline/P1-M/DEVELOPMENT_STATUS.md)，产品决定见 [PLAN](../../doc_md/subline/P1-M/DEVELOPMENT_PLAN.md)。本页只召回把全局音轨/mini/试听升级为播放器时的协调地雷，不替代实施状态。

## 先确认实际播放链

- `MusicController` 持有音轨；`NowPlayingOverlay` 只是视图与遥控。现存 mini/playlist 不能作为完整播放器已交付的证据。
- Song Select 也使用同一控制器，`WorkingBeatmap.PrepareTrackForPreview` 设置试听点与 looping，`ControlGlobalMusic` 接管/释放全局音轨。增加队列时先查共用音轨的控制权，不另建试听引擎。
- `PreviewTrackManager` 仍有共享 overlay/在线遗留引用，但 OMS 以 `OnlineFeaturesEnabled=false` 构造、返回 `DisabledPreviewTrack`；它不是本地试听入口，也不是“仅测试引用”。

## 队列接入容易破坏的行为

- `onTrackCompleted` 的 `!CurrentTrack.Looping` 守卫使试听不自动跳曲；`AllowTrackControl` 则保护 gameplay。两者都不能被新导航绕过。未来队列的 drive/follow 与 repeat 合同只在 [P1-M CONSTRAINTS](../../doc_md/subline/P1-M/TECHNICAL_CONSTRAINTS.md) 维护。
- `PlaylistOverlay.ItemSelected` 直接改全局 `Beatmap` 并 restart，绕过 next/prev 与随机历史。接入队列时须把这个真实外部入口计入协调回归。
- BMS importer 只接受有效显式 `#PREVIEW` 为 `AudioFile`，从 0 播放；旧的 ≥1MB 非键音整曲探测已删除。无 preview 的纯键音谱不进有音频的候选池，不能把 preview 当完整 keysound/BGM 混音。
- `EnsurePlayingSomething` 的 `MAX_ENSURE_PLAYING_SKIP_COUNT=50` 保护无可播轨场景；迁移导航时不要让纯键音库重新陷入无限 next。
- core 使用已有 `BmsStarRatingResolver` 识别 BMS；不能为常量或 BGA 反向引用 BMS 工程。BGA 诊断见 [[reference_bms_bga_chain]]。
- 展开壳体已确定复用 `FullscreenOverlay<T>`；它的离线 `IAPIProvider` 注入不构成重新选型理由。产品范围、分期与待决 header/窗口互斥只查 PLAN，不在召回另存版本。
