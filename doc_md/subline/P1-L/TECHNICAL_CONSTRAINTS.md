# P1-L 技术约束：BMS 演出/Gimmick 谱视觉复刻

> 最后更新：2026-06-23
> 本文件记录 `P1-L` 的硬约束。若实现与本文冲突，先修正其一再继续开发。完整背景见 [../../other/BMS_GIMMICK_CHART_RENDERING.md](../../other/BMS_GIMMICK_CHART_RENDERING.md)。

## 红线（最高优先级，贯穿全线）

1. **不得改坏正常游玩链路**：mania 风格前进式滚动 + 判定/计分/gauge 必须保持现状、可独立运行、回归不变。
2. 演出渲染只能是**可检测/可开关的旁路**：建议按谱面特征自动判定 gimmick 模式或显式开关进入；非 gimmick 谱一律走现有路径。
3. **判定语义不变**：判定/计分/gauge 继续由现有按时间的链路负责；本线只接管**视觉定位**，不得改判定语义。
4. **不得为演出改写共享核心类型**（如 `TimingControlPoint` 的 `[6,60000]` beatLength 钳制）；BMS 专属滚动语义必须留在 BMS 侧旁路内。
5. 每阶段独立可落地、可回退；均需 focused 回归 + Release 门槛 + 正常链路无回归证明。

## 归线约束

1. 本线 authority 是**演出视觉渲染**（对象/小节线/特效定位）及其解析消费；不拥有判定/计分（P1-C/Scoring）、解析模型本体（P1-K，只消费）、运行时音频基线（P1-J，协同）。
2. 解析侧若需新增 typed 模型（如地雷伤害语义），归 P1-K 落地后由本线消费，不在本线另起第二套解析。

## Phase 1（地雷视觉）约束 —— 已落地，须长期保持

1. 地雷是**视觉-only、非判定、非计分**对象：必须用 `IgnoreJudgement` + 空 hit window，`DisplayResult=false`。
2. 地雷**不得进入 `beatmap.HitObjects`**：必须像小节线一样由 `BmsPlayfield` 直接加到对应 lane（`BmsBeatmap.Mines` 承载），从而不进入计分/统计/`TotalObjectCount`/judged-note 路径。
3. `DrawableBmsMine` **不得**继承 `DrawableBmsHitObject`：必须保持为 `DrawableHitObject<BmsMine>`，以便被 `OfType<DrawableBmsHitObject>`（empty-poor / 候选音符扫描）天然排除。
4. 地雷 channel → lane 映射固定为 `channel - 0xC0`（D1-D9→11-19、E1-E9→21-29），再经 `mapLaneIndex` + lane 范围校验；越界或非可玩通道的地雷丢弃，不得 mis-map 到错误 lane。**范围上界必须用轨道数 `BmsRuleset.GetLaneCount`（键 + scratch），不得用键数 `GetKeyCount`**——否则 scratch 占 lane 0 会使最右键轨道（如 7K lane 7）地雷被误丢（2026-05-29 修复）。
5. 地雷时间必须复用 converter 的 `eventTimes`（与音符同一时间轴），不得另算一套 timing。
6. 地雷**必须随 lane 重排（`Mirror` / `Random`）同步移动**：`BmsLaneRearrangement.applyPermutation` 用与音符相同的 lane 映射重排 `BmsBeatmap.Mines`，否则重排后地雷与谱面错位（2026-06-13 修复）。地雷仍**不得**因此进入 `beatmap.HitObjects`（守 #2/#3）——只就地改 `Mines` 元素的 `LaneIndex`。`S-RANDOM` 逐时刻散布、无单一列置换，地雷保持原位（已知边界，非 bug）。注意 `Mirror`/`Random` 既是 `IApplicableToBeatmap`、只能由 `GetPlayableBeatmap` 应用**一次**，`BmsBeatmapModApplicator` 不得再应用它们（lane 置换会复合成 P³）。

## Phase 2（演出旁路）约束 —— Step A–C 已落地，须长期保持

1. 逐对象位置积分旁路**绕开**而非**改写** osu! 的 `ScrollingHitObjectContainer` 与 `TimingControlPoint` 钳制：实现为 BMS 专用 `BmsStopMotionScrollAlgorithm : IScrollAlgorithm`，经 `BmsPlayfield.CreateChildDependencies` 重缓存的 `BmsScrollingInfo` 注入；**零核心文件改动**。新增/改动绝不可回退为修改 `TimingControlPoint` 钳制或 `ScrollingHitObjectContainer`。
2. 旁路启用必须门控（`BmsGimmickScrollMode`，默认 `Auto`）；`Off`/未命中检测时 `BmsScrollingInfo.Algorithm` 必须**逐实例跟随基类算法**，与当前前进式滚动逐像素一致（`BmsScrollingInfoTest` 锁定，不得弱化）。默认 `Auto` 下「非 gimmick 谱零改动」依赖 `IsStopMotionGimmick` **无误报**（阈值须保守：`MaxSlope ≥ 50 || FrozenFraction ≥ 0.05`，正常/中等 soflan 远低于此）；放宽阈值前须重新评估正常链路回归，且 `Off` 必须始终是可用的硬回退。
3. 判定/计分继续走 `HitObject.StartTime` 时间链路；旁路只接管**视觉定位**，position 不得回流判定。`BmsScrollProfile` 不得进入 `beatmap.HitObjects`。
4. `BmsScrollProfile` 必须用**原始未钳制** BPM/STOP/measure-length/scroll 构建（复用 `buildEventTimeline` 游走），不得改用钳制后的 `ControlPointInfo`；STOP 段距离零增长（真冻结）、负向滚动留待 Phase 3（当前 `D` 单调非减，`TimeAtDistance` 取最早达成时间）。
5. **base 刻度 = 非冻结时长最常见 BPM**（`computeBaseBpm`，DEAD SOUL=132）。注意 `GetMostCommonBeatLength` 对演出谱会被 STOP-freeze/钳制点拉成 6；旁路在默认 Normal hi-speed 模式下因 `timeRange` 与之无关而忠实，**不得**为对齐而改用 6 做 base（那会复现 squash）。Floating/Classic 绝对刻度标定归 Phase 4。
6. 极端谱（DEAD SOUL：5645 地雷、6522 knots、~1300 control point、390 STOP 帧）必须有对象池/生命周期预算（与 P1-J 协同），不得无界实例化导致正常链路卡顿。

## Phase 5（BGA 链路）约束 —— 规划已冻结（2026-06-14），实现期须守

1. **BGA 是视觉-only 旁路**：BGA 时间线（`BmsBeatmap.BgaTimeline`）照 `Mines`/`ScrollProfile` 模式挂 `BmsBeatmap`，**不得进入 `beatmap.HitObjects`**，不得回流判定/计分/统计/`TotalObjectCount`。BGA 浮窗不接收游玩输入。
2. **零核心改动 + 不被遮挡**：浮窗挂 BMS 侧 `DrawableBmsRuleset.Overlays`（渲染在 playfield 之上）；**不得**重新把可见背景塞回 `BmsPlayfield.playfieldContainer` 内被 lane 背板遮挡的旧位置。不得为 BGA 改写共享核心类型。
3. **时间轴复用**：BGA 事件时间必须复用 converter 的 `eventTimes`（与音符/地雷同一时间轴），不得另算一套 timing。
4. **资源直读 `chartbms/`**：BGA 图片经 beatmap 作用域 `TextureStore`、视频经 `WorkingBeatmap.GetStream` 加载（同 storyboard），**不经** hash-backed `files/` store、不转 `.osz`（守 mainline 红线）。
5. **视频时钟同步**：视频 drawable 必须跟随游玩时钟（`PlaybackPosition = clock - eventTime`），pause/seek/retry 同步，复用 `DrawableStoryboardVideo` 范式；不得让视频脱离 frame-stable 时钟自由播放。
6. **解码/缺失健壮性**：BGA 资源缺失或视频编解码失败必须**优雅降级**，不得抛异常或刷错误日志、不得拖垮正常游玩链路。具体合同：(a) 视频**优先按绝对文件路径**打开 `new Video(path,…)`——BMS 恒文件系统直读，FFmpeg 对真实文件的探测/seek 远好于基于 .NET-Stream 的 AVIO（后者对老式 **MPEG-1 program-stream `.mpg`** 会在 `avformat_open_input` 阶段直接 `AVERROR_INVALIDDATA` 打不开 → 全黑）；无路径时回退 stream。(b) 仍解不出时通过 `Video.IsFaulted` 检测，base 层**回退显示 STAGEFILE 静态图**（不是黑屏），overlay/poor 层隐藏。路径解析：external 库用绝对 `FilesystemStoragePath`，internal 用 `Storage.GetFullPath(相对路径)`。
7. **皮肤合同（迁移中）**：当前 `BgaPanel` 允许 display 接收 timeline 并创建 player，默认实现为屏幕角落布局（5/7/9K 单角；14K 四角四 player）。这只描述恢复基线，不再是 Skin V1 目标。目标合同由 P1-A `SV1-3` 拥有：decode/timeline/seek/POOR/clock 与唯一 content surface 留在引擎；皮肤只取得只读 surface、事件和 `BmsGameplayLayoutSnapshot` 提供的 viewport，可 frame/mask/letterbox/decorate。多个 mirror viewport 必须共享同一 content authority，禁止各建独立 player/clock。新合同落地前不得破坏当前 fallback；落地时同步本线 BGA/player/cache 测试。
8. **perf（与 P1-J 协同）**：纹理懒加载并缓存、每视频源单一 decode/content authority、热路径零分配；大视频/海量 `#BMP` 帧需实测 gen0:gen1 与 GC 暂停，不得引入开局阻塞或密谱卡顿。当前 14K 四 player 路线不得继续扩张。
9. **范围边界**：v1 仅 native BMS ruleset 路径；converted-mania（Mania ruleset 下游玩，无 `BmsPlayfield`/`Overlays`）不在本期，单独评估。overlay/layer 通道"黑=透明"与 `#ARGB` v1 近似，保真细化后续。

## Phase 5.1（老式视频转码）约束 —— 已落地（2026-06-15），须守

1. **opt-in + 优雅降级**：转码是 `BgaVideoTranscode` 开关下的增强；**无外部 ffmpeg / 关闭 / 转码失败时必须等价于 Phase 5 的静态图回退**，绝不黑屏、绝不抛异常或刷错误日志、绝不拖垮正常游玩链路。OMS **不分发 ffmpeg**（用户自备：PATH 或放进数据目录），避免打包/授权负担。
2. **仅转码框架打不开的格式**：老式集合 `{.mpg,.mpeg,.avi,.wmv,.flv,.m1v,.m2v,.mkv}`；框架友好集合 `{.mp4,.m4v,.mov,.webm}` 一律直开不转码（不得无谓转码已能播的视频）。
3. **缓存正确性**：输出落 `<dataRoot>/bga-video-cache/`，文件名键 = `SHA1(源绝对路径|size|mtime)`（源变即重转）；**先写 `<dst>.tmp` 再原子改名**，`File.Exists(dst)` 必须只在文件完整时为真；转码失败必须清掉 .tmp、不得发布半成品。
4. **不阻塞游玩**：转码走后台 `Task.Run` + 按目标去重；游玩线程只做 `File.Exists`/状态查询与节流（~1s）重试热替换，**不得**在 update 线程同步转码或每帧打盘。
5. **视频-only**：转码命令必须 `-an`（BGA 不带音轨，音频是谱面键音）；编码到 H.264/yuv420p/mp4（框架确定能解）。**改任何转码参数（`BuildTranscodeArguments`）必须 bump `transcode_version`**——否则 `Resolve` 命中 `File.Exists` 会把旧参数产出的（可能不可解码）缓存当成功端出（2026-06-22 惨案的反复教训）。
6. 缓存治理见 **Phase 5.2**（已由「无清理」升级为会话级清空）；缓存仍只落 `<dataRoot>/bga-video-cache/`，不得写进谱面文件夹或 hash-backed `files/` store。

## Phase 5.2（转码加载体验与缓存治理）约束 —— R1/R2/R3/R4 已落地（2026-06-23），须守

1. **会话级缓存（R2/R3，用户拍板）**：转码产物每会话内持久（会话内重进同图即时命中），但**不得跨会话累积**。实现＝`BmsBgaVideoCache.ClearSessionCacheOnce` 每进程**一次性清空** `bga-video-cache/`；**必须在本会话任何转码启动之前**清（取"启动期清"而非"退出清"，崩溃也保证下次启动干净）。清空走 `lock` + `sessionCacheCleared` 守卫——第二个调用方等第一个清完再继续，**绝不可在清空与新转码之间留竞态**（否则重蹈并发写坏文件）。清空只删 `bga-video-cache/` 内文件，best-effort、不抛。
2. **加载等转码（R1）**：转码预热+等待由专用 `BmsBgaVideoPreloader` 负责，它**必须直接挂在 `DrawableBmsRuleset.Overlays`、不得塞进皮肤化的 `BmsBgaPanel` 内**——因为只有直接挂在被 `LoadComponentAsync` 等待的子树上，阻塞它的后台 `load()` 才能真正推迟 player push、让 BGA 开局即播；塞进 `SkinnableDrawable` 则其内容可能异步加载、不阻塞 push。
3. **等待必须有上限且可回退（R1 安全网）**：`PrewarmAndWait` 的 cap（当前 8s）是硬上限；**超时必须放行**，回落到 Phase 5.1 既有的"静态→后台转好热替换"路径，使本特性**严格只增不减**（命中/快转→开局播；慢转/失败→等价于今天）。不得移除 cap 让加载无限等待；preloader dispose 必须 cancel 等待。
4. **转码任务跨实例去重（不得乘以并发数）**：`inProgress` 必须 **static** 且值为 `Lazy<Task>`——preloader 与（14K 的 4 个）`BmsBgaPlayer` 对同一产物只 join **同一个**后台转码 Task，总耗时≈一次转码；`Lazy` 保证 `Task.Run` 副作用每产物只触发一次。唯一 temp（`<hash>.<guid>.tmp`）+ 原子 `File.Move(overwrite)` 仍是并发安全的基线，不得回退。
5. **提速仅用 libx264 `-preset ultrafast`（R4，用户拍板"安全优先"）**：仍 `-profile:v baseline`/yuv420p、不动可解码性；mpeg4 回退分支不得带 `-preset`/`-profile:v`。**硬件编码器（`h264_nvenc` 等）显式不纳入**——产出码流对框架挑剔的内置 FFmpeg 有兼容风险（黑屏惨案底色）、跨机/驱动不确定；若未来要加须 opt-in + 可用性探测 + 失败回退 + 实测可解码 + bump version。
6. **R5（加载扫描线进度揭示）已落地（2026-06-23）**，须守：
   - **仅 BMS**：`BeatmapMetadataDisplay` 的扫描线（`ScanlineLoadingLayer`）必须门控在现成的 `ruleset==bms` 条件（与难度胶囊同一门控）；**非 BMS 必须保留原 `LoadingLayer`（dim+spinner）**，不得改坏 mania/其它加载观感。
   - **优雅降级**：扫描线在**无真实进度时必须乒乓（indeterminate）**、有进度时按 % 揭示；即使进度通道全程沉默，观感也要成立（不得依赖进度才工作）。
   - **进度通道线程安全 + 跨 DI**：`GameplayLoadProgress` 由 `PlayerLoader` `[Cached]`（这样它的子 `BeatmapMetadataDisplay` 与 `LoadComponentAsync` 异步加载的 player 子树解析到同一实例——是 BMS 侧 `BmsBgaVideoPreloader` 能把进度送到加载界面的**唯一**桥）；写在转码后台线程、读在 update 线程，必须线程安全（`lock`），消费方一律 `CanBeNull`（隔离测试/非 PlayerLoader 路径下为 null）。`PlayerLoader` 每次加载前 `Reset`。
   - **ffmpeg 进度解析只在真实路径**：逐行流式读 stderr 解析 `Duration:`+`time=`，**不得**破坏注入式 runner 测试（注入 runner 不产进度，扫描线退乒乓）；改 stderr 读取方式不得回到一次性 `ReadToEnd` 而丢失实时性，也不得阻塞/死锁（stdout 仍并发 drain）。
   - **纯视觉**：扫描线/进度通道不得影响判定/计分/加载成败语义；它只是加载指示。

## 测试与发布约束

1. 每阶段至少补 focused 回归：Phase 1 已锁 converter 地雷构建 + "不泄漏判定路径"（`HitObjects` 无 `BmsMine`、`TotalObjectCount` 不含地雷）。
2. Release（`osu.Desktop.slnf`）构建 0 错误、生产代码 0 新增警告，是每阶段门槛。
3. 任一阶段改动若触及 P1-C 判定、P1-K 解析合同或 P1-J runtime hot-path，必须先停下拆分归线，再继续。
