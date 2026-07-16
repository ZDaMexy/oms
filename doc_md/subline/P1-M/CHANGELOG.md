# P1-M 变更日志：内置音乐播放器

> 本文件记录 `P1-M` 相关的验证通过变更，按时间倒序排列。
> 当前进度见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，执行规划见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

---

## 2026-07-16

### 文档健康复核：冻结顺序显式化

- STATUS/PLAN 保留 2026-06-15 已对齐的架构与产品决策，同时明确 P1-M 尚未开工，Phase 1 release gate 前不抢占 mainline R2–R6；没有把未来 PlayQueue 设计误写成当前能力。
- 本次仅改文档，未改代码，未运行产品测试或 Release。

## 2026-06-15

### 子线建立 + 规划对齐（尚未开工，纯文档）

审查「右上角正在播放浮窗 + song-select 试听」音乐播放器链路后，用户决定将其升级为真正意义上的**内置音乐播放器**，新建子线 **P1-M** 并完成四件套与规划对齐。

- **架构审查结论**（开工前基线，详见 STATUS）：唯一播放引擎 = `MusicController`；`NowPlayingOverlay` 仅视图遥控；song-select 试听走同一 `MusicController` + `WorkingBeatmap.PrepareTrackForPreview`（preview 点 + looping），**不走** `PreviewTrackManager`（后者因 `OnlineFeaturesEnabled=false` 在 OMS 实质失活）；`PlaylistOverlay` 点选直改全局 beatmap 绕过 next/prev；BMS 音频来源靠 importer 的 `detectFullMusicFile`/`#PREVIEW`，纯键音谱为静音虚拟轨；`EnsurePlayingSomething` 的 skip-guard 是现存 OMS 补丁。
- **已对齐产品决策**（用户拍板）：① 全功能 M1–M4+；② **播放源可选 mania/bms/both**（硬需求）；③ mini 浮窗 + 可展开全屏；④ 展开视图复用 `FullscreenOverlay<T>` 壳体（那批离线隐藏的在线 overlay 共享的展开壳，与联网无耦合）；⑤ 分层 PlayQueue 服务，song-select preview 路径独立保留。
- **复用核实**：`BeatmapCollection`（歌单）、`LogoVisualisation`（可视化）、`FullscreenOverlay`+`ToolbarOverlayToggleButton`（展开壳+顶栏 toggle）、`BmsRuleset.SHORT_NAME`（源过滤）均存在可用；SMTC（M6）与 BGA-in-player（M4）为净新增/跨项目。
- **分阶段**：Phase 0 地基（PlayQueue + 协调契约 + 测试网，行为对等）→ Phase 1 核心（M1 + 播放源 + M2 基础）→ Phase 2 展开视图 + 体验 → Phase 3 组织 + 历史 → Phase 4 BGA + SMTC。
- **红线**：不改坏 song-select 试听；不破坏 `AllowTrackControl`；离线只用本地轨。约束见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)。
- 反向同步 mainline（[DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md) 新增子线登记 + [DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md) 当前主线表新增 P1-M 行）、[CLAUDE.md](../../../CLAUDE.md) 与 [subline/README.md](../README.md) 索引。
