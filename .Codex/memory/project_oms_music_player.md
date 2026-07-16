---
name: project_oms_music_player
description: P1-M 内置音乐播放器子线——分层 PlayQueue 架构、双角色张力、FullscreenOverlay 复用、播放源过滤；规划已对齐未开工
metadata: 
  node_type: memory
  type: project
---

P1-M「内置音乐播放器」子线 2026-06-15 建线（四件套 `doc_md/subline/P1-M/`）。把「全局音轨 + 右上角 mini 浮窗 `NowPlayingOverlay` + song-select 试听」升级为真音乐播放器。**规划已与用户对齐，未开工**。

**架构审查的关键真相（开工前基线）**：
- 唯一播放引擎 = `MusicController`（osu.Game/Overlays）；`NowPlayingOverlay` 只是视图+遥控，不播放。
- song-select 试听 = **同一个 `MusicController`** + `WorkingBeatmap.PrepareTrackForPreview`（RestartPoint=PreviewTime, looping），**不走** `PreviewTrackManager`（后者因 `OnlineFeaturesEnabled=>false` 在 OMS 实质失活，仅测试引用 → 本线不接它）。`ControlGlobalMusic` 是 song-select 接管/释放全局轨的总闸。
- 双角色张力 = 真播放器要的「持久队列+单曲循环+歌单」与 song-select 要的「preview 点 looping 试听」语义冲突 → 这是整个设计的命门。
- BMS 音频来源：importer `detectFullMusicFile`(≥1MB 非键音)→`#PREVIEW` 回退；纯键音谱=静音虚拟轨。`EnsurePlayingSomething` 的 `MAX_ENSURE_PLAYING_SKIP_COUNT=50` 是现存 OMS 防死循环补丁。

**已对齐决策（用户 2026-06-15 拍板）**：全功能 M1–M4+；**播放源可选 mania/bms/both**（硬需求，按 `Ruleset.ShortName`=`BmsRuleset.SHORT_NAME` 过滤）；mini 浮窗+可展开全屏；展开视图**复用 `FullscreenOverlay<T>` 壳体**（那批离线隐藏的在线 overlay 共享的展开壳，与联网无耦合；`ToolbarMusicButton` 没被隐藏、它 toggle mini 浮窗）；**分层 PlayQueue 服务**（song-select preview 路径独立保留）。

**协调契约（命门）**：PlayQueue 自身 Next/Prev 时「驱动」全局 beatmap；外部换轨（选歌/playlist点选/快捷键）时只「跟随」同步 index。`onTrackCompleted` 的 `if(!Looping)→next` 守卫**不能动**（它让 song-select 试听不被队列自动推进劫持；repeat-one 经 PlayQueue 在 completed 时 restart）。

**复用核实**：`BeatmapCollection`(歌单)、`LogoVisualisation`(可视化频谱)、`FullscreenOverlay`+`ToolbarOverlayToggleButton.StateContainer`(展开壳+顶栏toggle) 均存在；SMTC 媒体键(M6)=净新增仅 osu.Desktop；BGA-in-player(M4)=跨项目（osu.Game 不能引用 Bms 项目，需核心接口 + BMS 侧注册复用 [[reference_bms_bga_chain]] 的 `BmsBgaPlayer`）。

**分期**：P0 地基(PlayQueue+协调契约+测试网,行为对等)→P1 核心(队列/重复模式/播放源/搜索排序/持久化)→P2 展开视图+体验(可视化/音量/倍速)→P3 组织+历史→P4 BGA+SMTC。红线：不改坏 song-select 试听、不破坏 `AllowTrackControl`、离线只用本地轨。关联 [[project_oms_overview]] [[project_oms_docs_governance]]。
