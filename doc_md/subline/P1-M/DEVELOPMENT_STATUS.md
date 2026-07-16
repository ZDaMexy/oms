# P1-M 开发进度：内置音乐播放器

> 最后更新：2026-07-17（主线优先级同步；尚未开工）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。执行规划见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，硬约束见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)。

## 当前阶段

- **阶段定位**：规划已与用户对齐，尚未开工；Phase 1 release gate 前不抢占主线 R3–R6。未来获准启动时，第一步仍是 Phase 0（PlayQueue 服务层、协调契约与测试网）的详细设计和最小落地。
- **范围**：全功能（M1 队列与播放模式 / M2 曲库组织 / M3 播放体验 / M4+ 沉浸展示与进阶）+ 播放源（mania/bms/both）可选 + mini 浮窗可展开全屏 + 分层 PlayQueue。

## 架构审查已确认事实（开工前基线，2026-06-15）

> 这些是规划所依据的当前链路真相，落地时若发现与代码不符须先更正本节。

- **唯一播放引擎 = [MusicController](../../../osu.Game/Overlays/MusicController.cs)**：持有 `CurrentTrack`(`DrawableTrack`)，负责 Play/Stop/Next/Prev/Seek/Shuffle/随机历史/Ducking/mod 音轨调整。
- **[NowPlayingOverlay](../../../osu.Game/Overlays/NowPlayingOverlay.cs) 只是视图 + 遥控器**，本身不播放；按钮调 controller，进度条 `SeekTo`，`TrackChanged` 事件换标题/封面。固定 400×130 浮窗。封面是静态 `beatmap.GetBackground()`。
- **song-select 试听 = 同一个 `MusicController` 实例**，不走 `PreviewTrackManager`（[Screens/Select 下零引用](../../../osu.Game/Screens/Select)）。机制：选谱 → 全局 `Beatmap.Value` 变 → `changeBeatmap` 换轨；[SongSelect.beginLooping](../../../osu.Game/Screens/Select/SongSelect.cs) 每次 `TrackChanged` 调 [WorkingBeatmap.PrepareTrackForPreview](../../../osu.Game/Beatmaps/WorkingBeatmap.cs)（`RestartPoint = Metadata.PreviewTime`、looping），`ensurePlayingSelected` 决定是否 `Play`。`ControlGlobalMusic` 是接管/释放全局音轨的总闸。
- **[PreviewTrackManager](../../../osu.Game/Audio/PreviewTrackManager.cs) 在 OMS 实质失活**：[OsuGameBase.cs](../../../osu.Game/OsuGameBase.cs) 用 `onlinePreviewEnabled = OnlineFeaturesEnabled`（`=> false`）实例化 → `trackStore` 从不创建 → `Get()` 永远返回 `DisabledPreviewTrack`（`GetTrack()=null`）。仅测试引用。**本线不接它**；保持失活并标注为离线无关分支。
- **[PlaylistOverlay.ItemSelected](../../../osu.Game/Overlays/Music/PlaylistOverlay.cs)** 直接改全局 `Beatmap.Value` + `Track.Restart()`，**绕过** controller 的 next/prev 语义（不进随机历史）。M1 需决定是否收口到 PlayQueue。
- **BMS 音频来源**（决定 bms 谱在播放器有没有声）：[BmsFolderImporter.detectFullMusicFile](../../../osu.Game.Rulesets.Bms/Beatmaps/BmsFolderImporter.cs)（≥1MB 非键音音频）→ `#PREVIEW` 回退；都没有则该谱在播放器是静音虚拟轨。BMS 谱多缺省 `PreviewTime` → 落到 `0.4 * Length`。
- **空音轨防死循环已是 OMS 现存补丁**：[MusicController](../../../osu.Game/Overlays/MusicController.cs) 的 `MAX_ENSURE_PLAYING_SKIP_COUNT=50`，防纯键音 BMS 库下 `EnsurePlayingSomething` 无限 next。上提导航策略时须保留此语义。

## 复用基础设施（已核实存在）

- ✅ [BeatmapCollection](../../../osu.Game/Collections/BeatmapCollection.cs)（realm）→ M2 收藏/自建歌单可复用。
- ✅ [LogoVisualisation](../../../osu.Game/Screens/Menu/LogoVisualisation.cs)（amplitude 频谱）→ M3 可视化可复用。
- ✅ [FullscreenOverlay](../../../osu.Game/Overlays/FullscreenOverlay.cs) + [ToolbarOverlayToggleButton](../../../osu.Game/Overlays/Toolbar/ToolbarOverlayToggleButton.cs)（`StateContainer` 一键接管 `ToggleVisibility`/`State`/`INamedOverlayComponent`）→ 展开视图壳体 + 顶栏 toggle 可复用。
- ✅ BMS `ShortName` 常量（`BmsRuleset.SHORT_NAME`）→ 播放源过滤谓词可行。
- ⚠️ 无任何 SMTC / 媒体键集成 → M6 是纯净新增（仅 `osu.Desktop`）。
- ⚠️ BGA 在播放器（M4）跨项目：osu.Game 不能引用 Bms 项目，需核心接口 + BMS 侧注册（Phase 4 spike）。

## 待决点

1. `FullscreenOverlay<T>` 子类化（带极简 music header）vs 同款式自建 `WaveOverlayContainer`（避开在线味 header / `IAPIProvider` 依赖）。
2. 展开播放器是否纳入 `OsuGame.informationalOverlays` 单窗互斥。
3. 持久化落点：scalar→config，队列/历史→新 realm 模型；歌单复用 collection 的边界。
4. playlist 点选是否收口到 PlayQueue（统一 next/prev 语义 vs 保留快捷直跳）。

## 红线状态

- 规划阶段，无代码改动 → 三条红线（song-select 试听、`AllowTrackControl`、离线只用本地轨）当前无风险。Phase 0 起每阶段须以回归测试证明。
