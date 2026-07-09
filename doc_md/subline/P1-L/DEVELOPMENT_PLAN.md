# P1-L 开发计划：BMS 演出/Gimmick 谱视觉复刻

> 最后更新：2026-07-10（Skin V1 的 BGA ownership/layout 边界同步）
> 全局计划见 [../../mainline/DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md)。完整可行性与架构分析见 [../../other/BMS_GIMMICK_CHART_RENDERING.md](../../other/BMS_GIMMICK_CHART_RENDERING.md)（本子线由该分析升级而来）。
> **红线（最高优先级）：任何阶段都不得改坏 OMS 正常游玩链路（mania 风格前进式滚动判定）。演出渲染只能作为可隔离、可关闭的旁路。**

## 子线定位

- 目标：在 OMS 内尽可能忠实复刻 DEAD SOUL [Revive] 这类**炫技/观赏（演出）谱**的视觉效果（定格动画式的凭空出现/消失、悬停、反向/双向滚动、LN 跳回等）。
- authority：演出**视觉渲染**（对象/小节线/特效的屏上定位、**BGA 背景演出**）与其所需的解析消费。判定/计分/gauge 继续归现有链路（P1-C/Scoring），本线不改判定语义。
- 不拥有：通用前进式滚动游玩链路（保持现状）；解析模型本体（归 P1-K，本线只消费）；运行时音频/性能基线（与 P1-J 协同）。
- 与 P1-A 的新边界：P1-L 继续拥有 BGA timeline、解码/转码、seek/retry/POOR 内容语义；P1-A `SV1-3/SV1-5` 拥有 gameplay layout snapshot、皮肤 viewport 与 scene API。当前向 skin display 传 raw timeline、14K 创建四个独立 player 只算迁移前实现，不是未来皮肤合同。

## 机理结论（详见可行性文档）

DEAD SOUL [Revive] 的所有效果是**定格动画（stop-motion）**：132 万 BPM 让滚动瞬间到位、measure-length 摆位置、STOP 定帧、大量地雷/音符作逐帧"像素"，全谱**无负值**。osu! 前进式滚动 + `TimingControlPoint.BeatLength` 钳制 `[6,60000]` + `RelativeScaleBeatLengths` 归一会把极端反差**压扁**，且 STOP 当前不真正视觉冻结、地雷此前根本不渲染——故现模型无法忠实复刻，需 beatoraja 风格的**逐对象位置积分旁路**。

## 分阶段计划

### Phase 0 — 解析完备性核对（前置，多数已在 P1-K 具备）
- 确认地雷（`MineEvents`）、measure-length、STOP、扩展/内联 BPM 已无损解码。结论：已具备（见 P1-K）。

### Phase 1 — 地雷视觉呈现（✅ 已落地，2026-05-29）
- 在**现有前进式滚动**下把地雷渲染为可视、非判定对象，零滚动模型改动、零正常链路风险。
- 落地文件：
  - [../../osu.Game.Rulesets.Bms/Objects/BmsMine.cs](../../../osu.Game.Rulesets.Bms/Objects/BmsMine.cs)（`HitObject`，`IgnoreJudgement` + 空 hit window，仅携 `LaneIndex`）
  - [../../osu.Game.Rulesets.Bms/UI/DrawableBmsMine.cs](../../../osu.Game.Rulesets.Bms/UI/DrawableBmsMine.cs)（仿小节线 drawable，非 `DrawableBmsHitObject`，Phase 1 用非皮肤简单圆形）
  - [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs](../../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs)（注册地雷时间键 + `buildMines`：`channel-0xC0` 映射回 lane、按时间排序）
  - [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs](../../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs)（`Mines` 列表，**不进 `HitObjects`**）
  - [../../osu.Game.Rulesets.Bms/UI/BmsPlayfield.cs](../../../osu.Game.Rulesets.Bms/UI/BmsPlayfield.cs)（`addMines`，仿 `addMeasureBarLines` 直接加到对应 lane）
- 隔离保证：地雷不进 `beatmap.HitObjects`；`DrawableBmsMine` 非 `DrawableBmsHitObject`，被 `OfType<DrawableBmsHitObject>` 天然排除（empty-poor / 键音时间线不受影响）；`IgnoreJudgement` + `DisplayResult=false`（不计分/不显示判定）。
- 后续小项（非阻塞）：地雷皮肤化（接入 `BmsLaneSkinElements`）、触雷伤害语义（接 gauge，跨 P1-C/Scoring，需单独评估）、极端谱地雷数量的对象池/性能（与 P1-J 协同）。

### Phase 2 — BMS 专用滚动位置积分旁路（核心，Step A–D ✅ 已落地 2026-05-29 / 门控默认 Auto / 逐帧人工视觉验收待办）
- 目标：忠实复刻"瞬移摆位 + 真 STOP 冻结 + 任意定高"，让 DEAD SOUL 的定格动画成立。
- **落地状态**：Step A（`BmsScrollProfile` + converter 并行积分 `D(t)`）、Step B（`BmsStopMotionScrollAlgorithm` + `BmsScrollingInfo` 重缓存 + `GimmickScrollMode` 门控）、Step C（标定 + 设置面板下拉 + 端到端冻结测试）、Step D（`IsStopMotionGimmick` 自动检测）均已落地。**门控默认 `Auto`**（特效/变速谱开箱即用，正常谱不命中检测、零改动；`Off` 回退），Normal 模式零标定即忠实；详见 [CHANGELOG.md](CHANGELOG.md) / [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)。DEAD SOUL 逐帧人工视觉验收（Phase 4）待办。
- 设计草案（已按此落地）：
  1. **BMS 滚动模型**：把 BPM（不封顶、绝对值/带符号）、STOP（真冻结）、measure-length、`#SCROLL`/`#SPEED` 归一成一个分段 `scrollVelocity(t)`，预计算累计距离 `D(t)`（分段，二分 O(log n)）。
  2. **逐对象定位**：对象屏上位置 = `D(objectTime) - D(currentTime)`（可负=在判定线另一侧/反向）；STOP 段 `velocity=0` → 真冻结；瞬移段 velocity 极大 → snap。
  3. **专用视觉容器**：用 BMS 自有容器/定位逻辑承载对象与小节线定位，**绕开** osu! `ScrollingHitObjectContainer` 与 `TimingControlPoint` 的 `[6,60000]` 钳制（不动共享核心类型）。
  4. **门控**：仅在"演出渲染模式"（按谱面特征自动判定 gimmick，或显式开关）启用；非 gimmick 谱一律走现有前进式滚动路径，零改动。
  5. **判定不变**：判定/计分继续由现有按时间的链路负责；本旁路只接管视觉定位。
- 验收：DEAD SOUL [Revive] 逐帧对照 beatoraja。

### Phase 3 — 反向/双向与自定义 LN
- 负向滚动、双向/正反并存小节线、LN 头"跳回"（自定义 LN 视觉或短 LN 重画）；覆盖使用负 BPM/`#SCROLL` 的其它 gimmick 谱。

### Phase 4 — 人工验收
- DEAD SOUL 等真谱与 beatoraja/LR2 逐帧对照（P1-E/P1-G 风格）。

### Phase 5 — BGA 链路（背景图 / 背景动画）—— ✅ 已落地（2026-06-14；自动化通过、人工视觉验收待办）
> 独立轨道，不依赖 Phase 2/3 滚动旁路。把此前标为 mainline Phase 2 future-scope 的 BGA 视频/动画正式激活并归入本线。
>
> **V1 边界（2026-07-10）**：本节保留的是当前播放能力与历史验收。布局和 skin ownership 已转交 P1-A Skin V1；“skin 自建 player / raw timeline 接口 / 14K 四 player”均不得作为新外部 API 继续扩张。

- **问题**：native BMS 游玩当前没有真正 BGA。解析层（P1-K）已完整产出 `BmsDecodedChart.BgaEvents`（通道 `04`base/`06`poor/`07`layer/`0A`layer2）+ `#BGA/#@BGA/#ARGB/#SWBGA/#POORBGA` typed 定义，但**转换层只取一个静态 `metadata.BackgroundFile`，整条 BGA 时间线被丢弃**；显示层 `BmsBackgroundLayer` 是静态占位件且挂在 `playfieldContainer` 内、位于不透明 lane 背板之下 → 游玩时被完全遮挡（14K DP 中缝可坐实）。
- **目标**：BGA 在一个**独立、不被遮挡、皮肤可定制**的浮窗控件中按时间线播放（图序列 + 视频），为自定义皮肤预留接口；默认皮肤自动适配 playfield 居左/居中/居右排列。
- **当期实现决策（不冻结 V1 layout）**：① 图序列 + 视频一起做；② 单打默认在 playfield 对侧，14K 后续改成四角复制；③ 等比完整 + 黑边；④ 游戏内 overlay 而非 OS 窗口；⑤ 仅 native BMS。V1 的最终 viewport 数量/位置由 `BmsGameplayLayoutSnapshot` 决定，内容时钟与解码 authority 必须只有一个。
- **实现切片**：
  - **L5-1 数据携带**：`BmsBgaTimelineEntry`（time/layer/asset/isVideo）；`BmsBeatmap` 加只读 `BgaTimeline` + `PoorBgaMode`；`BmsBeatmapConverter` 复用 `eventTimes`（与音符同一时间轴）把 `BgaEvents`→绝对时间、`BitmapTable` 解析文件名、扩展名判定视频、缺失跳过、按时间排序。**照 `Mines`/`ScrollProfile` 模式，不进 `HitObjects`。**
  - **L5-2 运行时播放器**：`UI/BmsBgaPlayer` 时间线驱动 base/layer/overlay/layer2 合成；图片走 `Sprite`+beatmap `TextureStore`、视频走 `DrawableVideo`（`WorkingBeatmap.GetStream` + `PlaybackPosition` 同步、pause/seek/retry 跟随）；POOR 层按 `#POORBGA` 在 miss 显示；letterbox；perf 走 P1-J 纪律。
  - **L5-3 皮肤浮窗 + 挂载 + 布局**：`BgaPanel` skin 组件（`IBmsBgaPanelDisplay` + `DefaultBmsBgaPanelDisplay` + `BmsSkinTransformer` 分支，`OmsSkin` 门控，照 `StaticBackgroundLayer` 写法）；挂 `DrawableBmsRuleset.Overlays`，默认位置随 `PlayfieldStyle`+keymode；退役被遮挡的 `BmsBackgroundLayer` 挂点（无 BGA 时浮窗显示首选静态图）；`ShowBga` 开关。
  - **L5-4 验证 + 收尾**：实机视频/图序列/POOR/四布局/seek 同步/perf；STATUS + CHANGELOG + memory 收尾。
- **验收**：截图的视频谱（`ふりんとどっとらいん`）与静态谱（`ibuprofen`）BGA 正确显示、不被遮挡、letterbox、POOR 切换、四布局位置正确、seek 同步、正常游玩链路零回归。

### Phase 5.1 — 老式 BGA 视频外部 ffmpeg 转码播放（✅ 已落地 2026-06-15，opt-in）
> 实机发现框架捆绑 FFmpeg 打不开老式 MPEG-1 program-stream `.mpg`（及 `.wmv/.avi/.flv`），`avformat_open_input` 阶段 `AVERROR_INVALIDDATA`（按 stream / 按文件路径均失败）。用户拍板用**外部 ffmpeg** 让其真正播放，无 ffmpeg 时保持 Phase 5 的静态图回退。

- `BmsBgaVideoCache`：老式扩展名判定 + `Resolve→{Ready/Pending/Unavailable}` + ffmpeg 候选解析（dataRoot/exeDir/PATH）+ 磁盘缓存（键 `SHA1(源路径|size|mtime).mp4`，先 .tmp 后原子改名）+ 后台 `Task.Run` 去重 + 可注入 runner 单测。
- 转码命令 `ffmpeg -y -i <src> -an -c:v libx264 -pix_fmt yuv420p -movflags +faststart <dst>`（视频-only、H.264/yuv420p/mp4 = 框架确定能解）。
- `BgaVideoTranscode` 开关（默认开）+ 设置项；`BmsBgaPlayer` 预热转码 + Pending 节流重试 + 本场热替换；faulted 永久标记不重试。
- 验收：单测 `BmsBgaVideoCacheTest` 全绿 + BMS 全套绿 + Release 0 错。**实机端到端 2026-06-22 用户确认老 `.mpg` BGA 可正常播放 ✅**（经一长串误判后真因＝**并发转码写同一固定 temp 互相穿插污染产物**、坏文件被 `File.Exists` 永久端出；修＝temp 加 Guid + `inProgress` 改 static 跨实例去重 + 原子 overwrite move + `transcode_version`→3，详见 [CHANGELOG.md](CHANGELOG.md) 2026-06-22 五次跟进）。

### Phase 5.2 — BGA 转码加载体验与缓存治理（R1–R5 ✅ 已落地 2026-06-23，用户实机初步未见异常，逐谱视觉细验待办）
> Phase 5.1 转码可播后用户提出 4 项体验/治理需求 + R5 进度显示，2026-06-23 按对齐结论全部落地（用户实机初步未见异常，逐谱视觉细验待办）。详见 [CHANGELOG.md](CHANGELOG.md) 2026-06-23、约束 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md) Phase 5.2。

- **R1 一进游戏即播（✅）**：新增 `BmsBgaVideoPreloader` 直接挂 `DrawableBmsRuleset.Overlays`，其后台 `load()` 阻塞预热+等待 → 推迟 player push 到转码完成 → BGA 开局即播。预热端/播放端共享磁盘目录（非实例），player 加载时 `File.Exists` 命中即 Ready。cap 8s 超时回落 Phase 5.1 静态→热替换（只增不减）。
- **R2/R3 会话级缓存（✅，用户选②）**：`ClearSessionCacheOnce` 每进程一次性清空 `bga-video-cache/`（启动期清、转码前清、`lock` 防竞态）；会话内重进即时命中、跨会话不累积。
- **R4 转码提速（✅，用户选"安全优先"）**：libx264 加 `-preset ultrafast`（不动 baseline 可解码性），`transcode_version`→4 失效旧缓存。**硬件编码器（NVENC）不纳入**（框架解码兼容风险 + 跨机不确定）。
- **R5 加载阶段进度显示（✅，用户截图给的方向＝扫描线）**：缩略图加载指示改成**从左到右把暗覆盖扫亮的扫描线**（替代 dim+转圈）。新 `ScanlineLoadingLayer`，**仅 BMS** 门控；**有真实转码进度时按 % 揭示、无进度时乒乓循环**（用户两选自然合一）。进度跨 DI 桥＝`GameplayLoadProgress`（`[Cached]` 于 `PlayerLoader`，被其子 `BeatmapMetadataDisplay` 与异步加载的 player 子树共享）；`BmsBgaVideoCache` 逐行解析 ffmpeg stderr `Duration:`+`time=` 推进度。
- **开放设计问题（已对齐结论）**：缓存策略＝**会话级清空**（非持久有界、非不缓存）；加载＝**硬等转码 + cap 超时回退静态**；硬件编码器＝**不纳入**（仅 ultrafast libx264）；进度 UI 落点＝**加载界面缩略图扫描线**（仅 BMS，进度驱动 + 乒乓兜底）。

### Phase 5.3 — Skin V1 BGA 内容/视图解耦（规划；协作 P1-A `SV1-3/SV1-5`）

1. 将 timeline、texture/video decode、playback clock、seek/retry 与 POOR 切换收敛为一个 engine-owned content session；同一谱面不得因多个 viewport 重复创建 player/decoder authority。
2. skin-facing API 只暴露只读 content handle/proxy、状态事件与 layout snapshot 中的 named viewport；不再交付 raw timeline、resource store 或可改写时钟。
3. 5K/7K 的 P1/P2/CenterP1/CenterP2、9K BMS/PMS、14K DP 全部消费 P1-A 的最终 rect；四角可以是多个只读镜像 viewport，但不是四个独立内容播放器。
4. focused tests 锁住单内容源、seek/POOR 同步、多 viewport 不重复解码；人工验收补宽高比/DPI/BGA 不遮 lane。

## 验证顺序（每阶段强制）
1. 先 focused 单测（如 Phase 1 的 converter 地雷构建 + 不泄漏判定路径）。
2. BMS 全套 + `osu.Desktop.slnf` Release 门槛。
3. **每阶段都必须证明正常游玩链路无回归**；任一阶段不达标不得推进下一阶段。
