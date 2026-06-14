# P1-M 技术约束：内置音乐播放器

> 最后更新：2026-06-15
> 本文件记录 `P1-M` 的硬约束。若实现与本文冲突，先修正其一再继续开发。规划见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，现状见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)。

## 红线（最高优先级，贯穿全线）

1. **不得改坏 song-select 试听链路**：选歌时仍须从 `Metadata.PreviewTime`（缺省 `0.4*Length`）起点 looping 试听、跟随选中谱面换轨。PlayQueue 在 song-select 接管全局音轨（`ControlGlobalMusic`）时必须「跟随」而非「驱动」。
2. **不得破坏 gameplay 全局音轨控制闸 `AllowTrackControl`**：所有播放器控制入口必须先查它；gameplay 中禁用用户控制的语义不变。
3. **离线优先 / 只用本地音轨**：播放器绝不接在线试听；`PreviewTrackManager` 保持失活并明确标注为离线无关分支，不得借本线复活。
4. **每阶段独立可落地、可回退**：均需 focused 回归 + Release 门槛 + 「song-select 试听 + `AllowTrackControl` 无回归」证明。

## 归线约束

1. 本线 authority = 音乐播放器**导航策略**（队列/重复模式/播放源过滤/随机历史）与**播放器 UI**（mini + 展开）。底层音轨生命周期归 `MusicController`（瘦身为纯引擎），不另起第二套音轨实现。
2. judgement / scoring / gauge 与 gameplay 音轨控制不归本线（只读 `AllowTrackControl`，不改语义）。
3. BMS BGA 渲染本体归 **P1-L**（Phase 5 `BmsBgaPlayer`）；M4 的 BGA-in-player 只做核心接口 + 跨项目桥接消费，不在本线另起第二套 BGA 渲染。
4. 持久化的 persisted metadata / 存储拓扑若有交集，归 **P1-H**；本线只新增播放器自有状态模型（队列/历史/模式）。
5. 播放源过滤与 song-select 的 BMS 筛选（**P1-I**）概念相邻但不同层：本线过滤的是「播放器音轨池」，不复用/不改 P1-I 的 song-select filter criteria。

## 架构约束（分层 PlayQueue）

1. **PlayQueue 是导航策略的唯一所有者**：`MusicController` 的 `next`/`prev`/`getNextRandom`/`getBeatmapSets`/`randomHistory`/`Shuffle` 上提到 PlayQueue 后，controller 不得再保留第二套并行导航逻辑。
2. **drive vs follow 仲裁必须显式**：只有 PlayQueue 自身 `Next/Prev` 时才驱动全局 `Beatmap`；外部（song-select 选谱 / playlist 点选 / 快捷键）改全局 beatmap 时 PlayQueue 只同步 current index，不重建队列、不抢轨。
3. **不得删除 `onTrackCompleted` 的 looping 守卫**（`if (!CurrentTrack.Looping && ...) NextTrack`）——它是 song-select 试听不被队列自动推进劫持的天然屏障。repeat-one 通过 PlayQueue 在 completed 时 restart 实现，不得改写该守卫去强行 advance。
4. **保留 `EnsurePlayingSomething` 的 skip-guard**（`MAX_ENSURE_PLAYING_SKIP_COUNT`）：纯键音 BMS 库无可播放音轨时不得无限 next。上提导航策略时此防死循环语义必须随迁。

## 播放源（mania/bms/both）约束

1. 过滤谓词按 `BeatmapSetInfo.Beatmaps` 的 `Ruleset.ShortName` 判定（bms = `BmsRuleset.SHORT_NAME` 常量，非硬编码字符串字面量散落）。
2. bms 源池天然受「有 `AudioFile`」既有过滤约束（纯键音谱无音频被排除）——这是预期行为，不得为「让 bms 谱都出现在播放器」而绕过音频过滤去播静音虚拟轨刷屏。
3. 播放源是持久化设置；切换源须即时重建可播放池且不打断当前正在播放的曲（除非当前曲已不在新池内）。

## UI 约束（展开视图复用 FullscreenOverlay）

1. 展开视图复用 `FullscreenOverlay<T>` 壳体模式（`WaveOverlayContainer` + `OverlayHeader` + `OverlayColourProvider` + 顶栏 `ToolbarOverlayToggleButton.StateContainer`）。该壳与联网无耦合（`IAPIProvider` 离线也在，为 dummy），但**不得借此引入任何在线功能入口**。
2. **`ToolbarMusicButton` 现 toggle 的是 mini `NowPlayingOverlay`，不得改其语义**；展开视图通过 mini 浮窗内的「展开」按钮（或新增独立 toggle）打开，mini 与展开共享同一 PlayQueue / MusicController 状态。
3. mini 浮窗（`NowPlayingOverlay`）作为常驻遥控器须保持现有轻量行为，不得因展开视图的丰富功能拖慢/改坏 mini。

## 验证约束

1. 改动落在 **core（osu.Game）**，回归跑 `osu.Game.Tests` 音乐相关 scene（`dotnet test --filter` 定向，注意 C# Dev Kit Test Explorer 在 osu.Game 上的已知 quirk），不是 BMS 套件。
2. 每阶段必须有「song-select 试听仍 preview-loop 且不被队列劫持」的回归测试；Phase 0 起即建立。
