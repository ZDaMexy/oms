# P1-M 开发计划：内置音乐播放器

> 最后更新：2026-07-17（主线优先级同步；产品规划未改变）
> 全局计划见 [../../mainline/DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md)。架构审查结论见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，硬约束见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)。
> **红线：① 不得改坏 song-select 试听链路；② 不得破坏 gameplay 全局音轨控制闸 `AllowTrackControl`；③ 离线优先——播放器只用本地音轨，绝不接在线试听。**

## 子线定位

当前只保存已对齐的未来设计；在 mainline R3–R6/release gate 完成或产品显式改序前，本线保持未开工，不与活动皮肤、硬件和发行收尾并行抢占。

- 目标：把现有「全局音轨 + 右上角 mini 浮窗（`NowPlayingOverlay`）+ 扁平 playlist」升级为一个**真正意义上的内置音乐播放器**——真队列、重复/随机、曲库搜索排序、收藏/自建歌单、可展开全屏视图、可视化、**播放源（mania / bms / both）可选**、跨会话状态恢复、Windows 媒体键。
- authority：音乐播放器的**导航策略**（队列 / 重复模式 / 播放源过滤 / 随机历史）与**播放器 UI**（mini 浮窗 + 展开视图）。底层音轨生命周期仍归 `MusicController`（瘦身为纯播放引擎）。
- 不拥有：song-select 试听语义（保留独立路径，本线只保证「不打架」）；judgement / gameplay 音轨控制（`AllowTrackControl`，只读不破坏）；BMS BGA 渲染本体（归 P1-L，M4 的 BGA-in-player 只做跨项目桥接消费）。

## 架构总览（分层 PlayQueue）

```
┌──────────────────────────────────────────────────────────────┐
│  PlayQueue  (新增 cached 服务层 —— 导航策略的唯一所有者)        │
│  • Queue: BindableList<QueueItem>   • Repeat: Off/All/One       │
│  • Shuffle (从 MusicController 迁入) • Source: Mania/Bms/Both    │
│  • Next()/Prev()/Enqueue/PlayNext/Reorder/Remove/Clear         │
│  • 持久化(队列 + 模式 + 源 + 位置)                              │
└───────────────┬───────────────────────────────┬───────────────┘
        驱动 ↓ (Next/Prev/源过滤)        跟随 ↑ (外部换轨时同步 index)
┌──────────────────────────────────────────────────────────────┐
│  MusicController  (瘦身 → 纯音轨引擎)                          │
│  保留: Play/Stop/Seek/Duck/changeTrack/adjustments/            │
│        AllowTrackControl/TrackChanged/EnsurePlayingSomething   │
│  移出: next/prev/getNextRandom/getBeatmapSets/randomHistory →  │
│        全部上提到 PlayQueue                                     │
└───────┬──────────────────────────────────────┬────────────────┘
        ▼ (视图 + 遥控)                          ▼ (preview 路径**不变**)
┌──────────────────┐              ┌──────────────────────────────┐
│ NowPlayingOverlay│              │ SongSelect (ControlGlobalMusic)│
│ mini ⇆ 展开/全屏 │              │ PrepareTrackForPreview+looping │
└──────────────────┘              └──────────────────────────────┘
```

**协调契约（本设计命门，Phase 0 必须钉死）：**
- song-select 试听靠 `Looping=true`（[SongSelect.beginLooping](../../../osu.Game/Screens/Select/SongSelect.cs) → `PrepareTrackForPreview`），而 [MusicController.onTrackCompleted](../../../osu.Game/Overlays/MusicController.cs) 已有 `if (!Looping) → next` 守卫 → **进选歌时队列自动推进天然被抑制**。这条守卫不能动。
- 选歌 / playlist 点选改了全局 `Beatmap` 时，PlayQueue 走「**跟随**」：只同步自己的 current index，不重建队列、不抢轨。只有自己 `Next/Prev` 时才「**驱动**」。
- 红线回归测试：进选歌仍在 preview 点循环、队列不劫持全局轨。

**播放源过滤**：[MusicController.getBeatmapSets](../../../osu.Game/Overlays/MusicController.cs)（待上提到 PlayQueue）的谓词加一档，按 `BeatmapSetInfo.Beatmaps` 的 `Ruleset.ShortName`（bms 常量 `BmsRuleset.SHORT_NAME` / 非 bms）过滤。注意 BMS 纯键音谱已被既有「有 `AudioFile`」过滤掉（[BmsFolderImporter.detectFullMusicFile](../../../osu.Game.Rulesets.Bms/Beatmaps/BmsFolderImporter.cs)：≥1MB 非键音音频 → `#PREVIEW` 回退）→ bms 源池 = 检出了音乐文件的 bms 谱。

## 已对齐产品决策（用户拍板 2026-06-15）

1. 全功能：**M1 队列与播放模式 / M2 曲库组织 / M3 播放体验 / M4+ 沉浸展示与进阶**，全要。
2. **播放源可选：mania / bms / both**（硬需求）。
3. UI 形态：**mini 浮窗 + 可展开全屏**。
4. 展开视图**复用 `FullscreenOverlay<T>` 壳体**（[FullscreenOverlay.cs](../../../osu.Game/Overlays/FullscreenOverlay.cs)：`WaveOverlayContainer` 波浪弹入 + `OverlayHeader` + `OverlayColourProvider` + 顶栏 `ToolbarOverlayToggleButton.StateContainer` 一键 toggle）——即那批被离线隐藏的在线 overlay（Changelog/Wiki/News/...）共享的展开壳；壳本身与联网无耦合。
5. 架构：**分层 PlayQueue 服务**，song-select preview 路径独立保留。

## 分阶段计划

### Phase 0 — 地基：PlayQueue 服务层 + 协调契约 + 测试网
- 新增 `PlayQueue`（cached 服务）：队列 / 重复模式 / 随机 / 播放源 / current index 的唯一所有者。
- `MusicController` 瘦身：把 `next`/`prev`/`getNextRandom`/`getBeatmapSets`/`randomHistory`/`Shuffle` 导航策略上提到 PlayQueue；`NextTrack`/`PreviousTrack`/`onTrackCompleted` 改为向 PlayQueue 询问候选（repeat-one → restart；否则按 repeat-all/off 取 next）。保留 Play/Stop/Seek/Duck/changeTrack/adjustments/`AllowTrackControl`/`TrackChanged`/`EnsurePlayingSomething`。
- 协调契约落地：drive vs follow 仲裁；守住 `onTrackCompleted` 的 looping 守卫。
- 测试：PlayQueue 单测（重复模式 / 随机整合 / 源过滤 / 队列顺序 / 持久化占位）+ **song-select preview 回归测试**（进选歌仍 preview-loop、不被队列劫持）。
- **本阶段行为对等，无用户可见变化**。这是后面所有模块的地基，风险最高，必须先做。

### Phase 1 — 核心播放器（M1 + 播放源 + M2 基础）
- M1：重复模式三态按钮（Off/All/One）、真队列（enqueue / play-next / reorder / remove / clear）、shuffle 整合进队列。
- **播放源选择器**（mania / bms / both）：UI 分段控件 + 可播放池过滤谓词 + 持久化。
- M2 基础：[Playlist](../../../osu.Game/Overlays/Music/Playlist.cs) / [PlaylistOverlay](../../../osu.Game/Overlays/Music/PlaylistOverlay.cs) 内搜索框 + 排序（标题 / 曲师 / 时长 / BPM / 导入时间 / 播放次数）。
- 持久化：scalar（模式 / 源 / shuffle）走 [OsuConfigManager](../../../osu.Game/Configuration/OsuConfigManager.cs) 新增 `OsuSetting`；队列 / 位置走新 realm 状态模型。

### Phase 2 — 展开视图（M4 展示）+ 体验（M3）
- 展开视图 = 新建 `FullscreenOverlay<MusicPlayerHeader>` 子类（或同款式自建 `WaveOverlayContainer`）；mini [NowPlayingOverlay](../../../osu.Game/Overlays/NowPlayingOverlay.cs) 加「展开」按钮 toggle 之，两者共享 PlayQueue / MusicController 状态。决定是否纳入 `OsuGame.informationalOverlays` 单窗互斥。
- 大封面 + 完整元数据（BPM / 时长 / 谱师 / star / key count，复用 `BeatmapLocalMetadataDisplayResolver`）。
- M3：内联音量、可视化（复用 [LogoVisualisation](../../../osu.Game/Screens/Menu/LogoVisualisation.cs) 频谱）、倍速（track tempo 调整）、A-B 循环、淡入淡出。
- 队列面板 / 搜索排序 / 源选择器都进展开视图。

### Phase 3 — 组织（M2 进阶）+ 状态历史（M5）
- 收藏 + 自建歌单：复用 [BeatmapCollection](../../../osu.Game/Collections/BeatmapCollection.cs)（realm）作播放器歌单源，边界待定（收藏 = 保留集合 / 用户歌单 = 集合在播放器侧的呈现）。
- 播放历史 / 最近播放 / 播放次数：新 realm 模型。
- 跨会话恢复上次位置 / 队列 / 模式：扩展 Phase 0/1 的持久化。

### Phase 4 — 进阶集成（M4 BGA + M6 SMTC）
- **BGA-in-player spike**（跨项目）：osu.Game 不能引用 Bms 项目。需在核心定义接口（如 `IBackgroundAnimationProvider` / skinnable 组件），由 BMS ruleset 注册实现，让展开视图在当前曲为 bms 时复用 [BmsBgaPlayer](../../../osu.Game.Rulesets.Bms/UI/BmsBgaPlayer.cs)（P1-L Phase 5）。前期展开视图先用静态封面（mania/bms 通用）。
- **Windows SMTC 媒体键**：纯净新增，仅 `osu.Desktop`（Windows 入口）；锁屏 + 键盘媒体键控制 + 元数据上报。

## 验证顺序（每阶段强制）
1. 先 focused 单测（PlayQueue 模式/源/顺序、preview 回归）。
2. `osu.Desktop.slnf` Release 门槛 + 相关 `osu.Game.Tests`（**改的是 core，不是 BMS 套件**；用 `dotnet test --filter` 定向跑音乐相关 scene）。
3. **每阶段都必须证明 song-select 试听 + gameplay 音轨控制无回归**；任一阶段不达标不得推进下一阶段。
