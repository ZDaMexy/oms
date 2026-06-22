# P1-L 变更日志：BMS 演出/Gimmick 谱视觉复刻

> 本文件记录 `P1-L` 相关的验证通过变更，按时间倒序排列。
> 当前进度见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，执行规划见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

---

## 2026-06-23

### Phase 5.2：BGA 转码加载体验与缓存治理（R1 开局即播 / R2·R3 会话级缓存 / R4 ultrafast 提速 / R5 扫描线进度揭示）

承接 2026-06-22「老式 `.mpg` BGA 可播」的用户确认。Phase 5.1 仍是**首播先静态、~十多秒后台转好才热替换**，且 `bga-video-cache/` 无清理长期累积。本轮按对齐结论落地全部 5 项需求（R1–R5；R5 进度 UI 用户随后以截图给出方向＝缩略图扫描线）。**设计对齐**：缓存＝会话级清空（用户选②）、提速＝仅 libx264 ultrafast（用户选"安全优先"、不纳 NVENC）、加载＝硬等转码但有 cap 超时回退静态、进度 UI＝仅 BMS 的扫描线（进度驱动 + 乒乓兜底）。

- **R1 开局即播（加载等转码）**：新增 [BmsBgaVideoPreloader](../../../osu.Game.Rulesets.Bms/UI/BmsBgaVideoPreloader.cs)（`Component`），由 [DrawableBmsRuleset.setupBgaPanel](../../../osu.Game.Rulesets.Bms/UI/DrawableBmsRuleset.cs) **直接挂进 `Overlays`（不进皮肤化的 `BmsBgaPanel`）**。其后台 `load()` 阻塞预热+等待——因为它在 `LoadComponentAsync(player)` 等待的子树上，阻塞它就推迟 player push（`PlayerLoader.readyForPush` 依赖 `CurrentPlayer.LoadState==Ready`），转码在第一帧前完成 → BGA 开局即播。**关键认知**：预热端与播放端共享的是**磁盘目录**不是实例（键 `SHA1(源|size|mtime|v4)`），preloader 转好落盘后 `BmsBgaPlayer` 加载时 `File.Exists` 直接 Ready；故 player 端**零改动地享受**（仅删掉它原来的 fire-and-forget 预热 loop，cache 仍按 play-time `Resolve` 用）。**安全网**：`PrewarmAndWait` cap 8s，超时放行 → 回落 Phase 5.1 的"静态→后台热替换"，**严格只增不减**；preloader dispose 时 cancel。
- **R2/R3 会话级缓存**：新增 `BmsBgaVideoCache.ClearSessionCacheOnce`（`lock` + `sessionCacheCleared` 每进程一次性清空 `bga-video-cache/`，取"启动期清"＝崩溃也保证下次干净），由 preloader 在**任何转码启动之前**调用（它是会话内唯一的 pre-gameplay 转码发起者，故清空绝不与写入竞态）。会话内重进同图仍命中缓存即时；跨会话不累积。清空逻辑抽 `ClearCacheDirectory`（无守卫、可单测）。
- **R4 ultrafast 提速**：`BuildTranscodeArguments` 的 libx264 分支加 `-preset ultrafast`（mpeg4 回退分支不带），仍 `-profile:v baseline`/yuv420p **不动可解码性**，预计 2–4× 提速。**硬件编码器（NVENC）按用户选择不纳入**（框架内置 FFmpeg 挑剔解码 + 跨机不确定）。`transcode_version` 3→**4** 失效旧缓存自动重转。
- **转码 Task 可 join + 跨实例去重**：`inProgress` 由 `ConcurrentDictionary<string,byte>` 改 `<string,Lazy<Task>>`，`startTranscode` 返回可 await 的 Task（`Lazy` 保证 `Task.Run` 每产物只触发一次）；14K 的 4 个 player + preloader 对同一产物 join **同一** Task，总耗时≈一次转码。唯一 Guid temp + 原子 `File.Move(overwrite)` 基线保留。新 `Prewarm`（返回 Task 或 null）/`PrewarmAndWait`（`Task.WaitAll(tasks, cap, ct)`，吞 timeout/cancel/aggregate）。
- **R5 扫描线进度揭示（替代 dim+转圈，用户截图给的方向）**：缩略图加载指示从「暗覆盖 + 中心转圈」换成**从左到右把初始暗覆盖扫亮的扫描线**。新 [ScanlineLoadingLayer](../../../osu.Game/Screens/Play/ScanlineLoadingLayer.cs)（`VisibilityContainer`）：底层亮 `Sprite` 不动，暗 `Box` 锚右、相对宽 `1-reveal` 随揭示收缩，亮扫描线（加色 + Glow）骑在 `reveal` 边缘；**有真实转码进度时按 % 一路揭示，无进度时乒乓（L→R 扫亮 / R→L 回暗）循环**（用户两个选择的自然合一）；`Show`→淡入起扫，`Hide`→最后一次扫满 + 整体淡出（正好接「图2」全亮态）。**作用范围＝仅 BMS**（复用 `BeatmapMetadataDisplay` 现成 `ruleset==bms` 门控，非 BMS 保留原 `LoadingLayer`）。**真实进度桥接**：新 [GameplayLoadProgress](../../../osu.Game/Screens/Play/GameplayLoadProgress.cs)（线程安全 `lock`，任意线程写、update 线程读）`[Cached]` 在 [PlayerLoader](../../../osu.Game/Screens/Play/PlayerLoader.cs)——既被其子 `BeatmapMetadataDisplay`（扫描线读）、又被 `LoadComponentAsync` 异步加载的 player 子树（`BmsBgaVideoPreloader` 写）解析到**同一实例**（跨 DI 作用域的关键）；`PlayerLoader.prepareNewPlayer` 每次加载前 `Reset`。**ffmpeg 实时进度**：`BmsBgaVideoCache` 转码委托加 `Action<double>? onProgress`，`runFfmpegWithEncoder` 改**逐行流式读 stderr**（解析一次性 `Duration:` 总时长 + 反复的 `time=`→`[0,1]`），`PrewarmAndWait` 跨多源取**均值**转发；preloader 把均值 `loadProgress.Report`。新静态 `TryParseFfmpegProgressLine`（可测）。**优雅降级**：无进度→乒乓（即用户要的兜底），故扫描线观感与转码是否真有进度无关。
- **测试/构建**：`BmsBgaVideoCacheTest` +6（`TestPrewarmAndWaitBlocksUntilTranscodeCompletes`＝等到完成后即 Ready / `TestPrewarmAndWaitReturnsAtCapWhenTranscodeIsSlow`＝慢转不超 cap / `TestPrewarmAndWaitDoesNotWaitForFriendlyOrCachedSources` / `TestClearCacheDirectoryDeletesAllFiles` / `TestParseFfmpegProgress*` ×2）+ 扩 `-preset ultrafast` 断言 + 注入 runner 改 4 参（带 onProgress）；不依赖真 ffmpeg。BMS 全量 **954/954**；`osu.Desktop.slnf` Release **0 错误**（生产代码 0 新增警告，2 个既有 test 警告未动）。**2026-06-23 用户实机初步未见异常**（开局即播 / 会话内重进即时 / 跨会话不留存 / 转码更快 / 扫描线观感 + 进度跟随）；逐谱视觉细验仍待办。约束见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md) Phase 5.2。

## 2026-06-22

### BGA 转码设置改名「ffmpeg完整BGA支持」+ 新增「检测 ffmpeg 安装状态 / 打开 ffmpeg 安装目录」按钮

用户要求：把「设置-游戏模式-BMS-转码无法解码的 BGA 视频」改名为 **「ffmpeg完整BGA支持」**，描述精简为「对老式BGA提供转码播放支持，需自行放置ffmpeg到数据目录」；并加两个功能按钮——**检测 ffmpeg 安装状态**、**打开 ffmpeg 安装目录**。

- **改名 + 精简描述**：`BmsSettingsSubsection` 的该 `FormCheckBox` Caption/HintText 更新（绑定的 `BmsRulesetSetting.BgaVideoTranscode` 不变，仅展示层文案）。
- **检测按钮**：新增 `BmsBgaVideoCache.FindAvailableFfmpeg(IReadOnlyList<string>? candidates)`（public static），**镜像运行时 `resolveFfmpeg` 的解析顺序**：先逐个候选文件存在性、再扫描系统 `PATH` 下 `ffmpeg.exe`/`ffmpeg`，返回解析到的路径或 null。设置面板点击后把结果写入一行状态文字（已检测到→显示路径；未检测到→提示放入数据目录或加 PATH）。候选列表与 `BmsBgaPlayer.setUpVideoCache` 一致＝`storage.GetFullPath("ffmpeg.exe")` + `AppContext.BaseDirectory/ffmpeg.exe`。
- **打开目录按钮**：`host.OpenFileExternally(storage.GetFullPath(string.Empty) + Path.DirectorySeparatorChar)` 打开 OMS 数据目录（用户放 ffmpeg.exe 处；尾分隔符让宿主按文件夹打开，沿用 `FilesystemBeatmapLocation` 同一手法）。`BmsSettingsSubsection` 新 `[Resolved] Storage`/`[Resolved] GameHost`。

**跟进修复（用户反馈：ffmpeg 已检测到，但 `flonne2_bga.mpg`（`#BMP01`）仍只显示静态图、beatoraja 能正常播放视频+miss BGA）**——诊断：转码失败 → `Resolve` 返回 `Unavailable` → 直开 `.mpg` faulted → 静态；且失败几乎无声（ffmpeg stderr 被读后**丢弃**、仅 `Debug` 记一句通用日志）。最可能根因＝用户的 `ffmpeg.exe` 是**精简/LGPL 版、缺 `libx264`**（转码硬编码 `-c:v libx264`）——beatoraja 用自带解码器，能播≠该 ffmpeg 有 libx264 编码器。三处治理：

- **编码器回退（治本）**：`runFfmpeg` 拆成 `runFfmpegWithEncoder`，先试 `libx264`、失败（且 ffmpeg 非缺失）再试**内置恒有的 `mpeg4`（MPEG-4 Part 2）**。关键认知：框架打不开的是老式**容器**（MPEG-1 program stream），不是编码器——转到干净 `.mp4` 后任一框架可解码的编码器都行，故缺 libx264 也能转。
- **失败可见化**：并发读 stdout/stderr（避免满缓冲死锁），非零退出/超时/异常时把 **ffmpeg exit code + stderr 末段** 记到 `LogLevel.Important`（runtime log 可见），从此「为何转码失败（如 Unknown encoder 'libx264'、No such file）」可诊断，不再静默退化静态图。
- **检测“真实化”**（回应用户疑点 1“只查文件存在”）：新增 `BmsBgaVideoCache.ProbeFfmpeg(candidates)` —— 实际运行 `ffmpeg -hide_banner -encoders`，区分 `NotFound / NotExecutable / ReadyWithoutH264（缺 libx264，将回退 mpeg4） / Ready（含 libx264）`；「检测」按钮改走 `ProbeFfmpeg`（后台 `Task.Run` + `Schedule` 回填，`GetResultSafely`），状态行据此给具体结论。`FindAvailableFfmpeg` 保留供 Probe 内部用。
- **测试/构建**：`BmsBgaVideoCacheTest` 新增 `TestFindAvailableFfmpegSkipsMissingAndReturnsExistingCandidate`（确定性候选解析）；编码器回退与 Probe 走真实进程、不在注入式单测覆盖内（既有注入 runner 用例不回归）。BMS 全量 **946/946**；代码编译 **0 错误**（osu.Desktop 拷贝因游戏运行锁定失败，非编译问题）。归属：主 `P1-L`（BGA 链路 / Phase 5.1 外部 ffmpeg 转码）。

**二次跟进（实机日志定位真因——上面的 libx264 假设是错的）**：用户用新构建实机后给出 runtime log，真因明确＝**输出写到 `<hash>.mp4.tmp`，ffmpeg 按文件扩展名推断输出容器，`.tmp` 不是已知格式 → 选不出 muxer**：`Unable to choose an output format for '...mp4.tmp'; ... Error initializing the muxer ... Invalid argument`。libx264 **一直可用**（错误发生在输入分析之后的 muxer 阶段，libx264 与回退的 mpeg4 报同一错，证实是容器/扩展名、非编码器）。**真正的修复＝转码命令显式加 `-f mp4`**（`BmsBgaVideoCache.BuildTranscodeArguments` 抽出可测，args = `-y -hide_banner -i <src> -an -c:v <enc> -pix_fmt yuv420p -movflags +faststart -f mp4 <dst.tmp>`）。诊断日志改造（上一条）是定位此 bug 的关键——原本失败被静默吞掉。顺带**降噪**：每编码器的完整 ffmpeg stderr 改记 `Verbose`（文件日志、不刷通知），`startTranscode` 每个失败源只发**一条 `Important`** 摘要（用 `failedDestinations.TryAdd` 去重，重试不再重复弹）。编码器回退（libx264→mpeg4）作为非完整版 ffmpeg 的防御保留。新增确定性回归 `TestTranscodeArgumentsSpecifyMp4MuxerForTmpOutput`（断言 args 含 `-f mp4`、编码器、末位为输出 tmp）。BMS 全量 **947/947**；编译 0 错误。

**三次跟进（`-f mp4` 后转码成功但视频全黑、runtime 日志暴涨到 3614 行）**：实机日志显示转码已成功产出 **H.264 mp4**（`-f mp4` 生效、不再报 muxer 错），但 osu!framework 的内置 FFmpeg **解不了**——先 D3D11VA 硬解 `Failed to send avcodec packet: Invalid data found (-1094995529)` → `Disabling hardware decoding` → 软解回退**仍每包失败**，0 帧 → 全黑；且每个失败包记一行（**3360/3614 行就是这个刷屏**，Verbose、仅日志文件无通知）。框架本身能放普通 H.264 mp4（2026-06-14 已验），故是**外部 ffmpeg 产出的码流对框架老版 FFmpeg/硬解路径不兼容**。修复＝把转码约束到**最广兼容的 H.264**：libx264 加 `-profile:v baseline`（无 B 帧/无 CABAC，HW 与老 SW 都稳解；High profile 默认输出在独立播放器正常、在框架内每包失败）、`-vf setpts=PTS-STARTPTS,setsar=1`（清掉老 .mpg ~0.44s 起始偏移避免 edit list + 方形像素）、`-map 0:v:0`（仅取视频流）。mpeg4 回退不加 profile（无 baseline 档）。**关键：旧的不可解码 .mp4 已缓存**——`Resolve` 命中 `File.Exists(destination)` 会直接返回旧坏文件、不重转，故 `cacheKey` 加 `transcode_version`（=2）随转码参数变更失效旧缓存、自动重转（旧文件成孤儿、可删 `<dataRoot>/bga-video-cache/` 回收）。回归 +1 `TestTranscodeArgumentsOmitH264ProfileForMpeg4Fallback`（mpeg4 不得带 `-profile:v`）+ 扩 `TestTranscodeArgumentsSpecifyMp4MuxerForTmpOutput`（断言 libx264 含 `-profile:v baseline`）。BMS 全量 **948/948**；编译 0 错误。

**四次跟进（baseline 仍解不了 + runtime 暴涨到 47948 行；实机日志确诊）**：实机日志显示 **baseline H.264 也解不了**——`opened hardware video decoder ... codec h264` → 每包 `Failed to send avcodec packet: Invalid data` → 软解回退仍每包失败 → 0 帧。**3360/3614→现 47948 行**的刷屏有两源：① 解不了的 .mp4 持续被解码重试；② 转码失败时 `createVideo` 直开原始 `.mpg` 兜底，老式容器同样每包失败再刷屏。**结论：这不是编码 profile 问题（High/baseline 都失败），而是框架内置 FFmpeg/硬解路径对该转码产物不兼容**（框架能放普通 .mp4，2026-06-14 已验；`Video` 仅 `Video(string)/Video(Stream)` 两 ctor、`TargetHardwareVideoDecoders` 在内部 `VideoDecoder` 上、**无法按视频强制软解**，只有全局 `FrameworkSetting.HardwareVideoDecoder`）。本轮做**干净降级**（不再黑屏/不再刷屏/不再弹通知），解码真因留待硬解诊断：
- **看门狗（`Video.FramesProcessed`）**：附着的视频过宽限期（1s）仍 0 帧 → 判为不可解码 → `dropFaultedVideo`（dispose 视频 → 停止解码线程 → **掐断每包刷屏**）→ 回退静态图。`isUndecodableVideo` + `activeVideoAttachTime`。
- **静态优先于黑**：`FramesProcessed==0` 期间一律显示静态回退（不黑屏），帧出来（>0）才切视频；`applyActiveEntry` 视频态先 `showFallback()`。
- **legacy 不再裸开**：`createVideo` 对 `RequiresTranscode` 源在转码不可用时直接回退静态、**不再 `new Video(.mpg)`**（消除 .mpg 每包刷屏源）。
- **转码失败日志降级 Important→Verbose**（BGA 纯视觉、已优雅降级静态，不再弹通知打扰；去重保留）。

BMS 全量 **948/948**；编译 0 错误。

**五次跟进（真因确诊——前面所有假设都错了）**：用户给出 `h264 ... Error splitting the input into NAL units / Invalid NAL unit size (garbage > small)` + 提议查 ffmpeg。直接动手验证：① 用户 `D:\oms\data\ffmpeg.exe` ＝完整 gyan.dev 8.x 构建、含 libx264，**没问题**；② **用户自己的 ffmpeg 解不了缓存里的 .mp4**（`Invalid NAL unit size (0 > 25614)`）→ 文件本身就是**坏的**；③ 用同一组参数**全新转码一次**→**解码干净**（参数没问题）；④ **模拟两个 ffmpeg 并发写同一个 temp** → 复现出**一模一样的 `Invalid NAL unit size (0 > 25614)`**。**真因＝并发转码写同一个固定 temp 路径 `<hash>.mp4.tmp` 互相穿插污染**：转码 Task 在 BgaPlayer dispose 时不取消，用户退出/快速重放→新一局又对同一 temp 起第二个 ffmpeg→产物字节交错→不可解码。一旦坏文件落缓存，`Resolve` 命中 `File.Exists` 永远端出坏文件。**这同时解释了之前所有现象**（黑屏/静态/HW 失败/SW 失败/High vs baseline 都一样）——根本不是 profile/HW/demux，是文件被并发写坏了。**修复**：① temp 文件名加 `Guid`（`<hash>.<guid>.tmp`）——并发也各写各的、永不穿插；② `inProgress` 改 **static**（跨 cache 实例去重，孤儿/重放不再起第二个 ffmpeg）；③ 发布用 `File.Move(tmp,dest,overwrite:true)` 原子覆盖；④ `transcode_version`→**3** 失效旧坏缓存自动重转。BMS 全量 **948/948**。**实机验收待用户确认（干净构建+清 `bga-video-cache/`+重放，视频应真正播放）。教训：缓存产物损坏会被 `File.Exists` 永久端出、把每次诊断引偏——先验证缓存文件本身是否可解码（用产出它的同一 ffmpeg 解一遍）再怀疑解码端。**

## 2026-06-20

### BGA 默认摆位：14K 镜像到屏幕**四角**（用户实机三连改之三 + 一次返工修正）

14K 双打 playfield 几乎占满屏宽，原 BGA 默认放**居中 gap**会压游玩区/combo。用户要求 **14K BGA 同时出现在屏幕四角**（左下/左上/右下/右上）。**首版返工**：误读成"单角"只放了右上角、且 keymode 检测（`WorkingBeatmap.Beatmap as BmsBeatmap`）失败退回大尺寸 `side_size` 遮挡 playfield。修正后改 `BmsBgaPanel`：

- **枚举改四角**：`BmsBgaPlacement` 由 `Left/Right/Center` 改为 `TopLeft/TopRight/BottomLeft/BottomRight/Center`（`Center` 保留给皮肤覆盖）。
- **14K 四角镜像**：`DefaultBmsBgaPanelDisplay` 重构为 `framesContainer` + 多个 frame——14K 时在**四角各 mount 一个 `BmsBgaPlayer`**（同一 timeline、clock 同步），非 14K 单角（仍镜像 playfield 侧 P1→TopRight / P2→TopLeft）。`NotifyMiss` 转发到所有 player。
- **keymode 可靠解析**：改用 `[Resolved] GameplayState` + `BmsLaneLayout.CreateFor(gameplayState.Beatmap).Keymode`（与 gauge 同一可靠源，取代之前失败的 `WorkingBeatmap.Beatmap as BmsBeatmap`）。
- **14K 紧凑尺寸贴边**：14K 四角用更小的 `corner_14k_size`（0.13×0.16，配 `bottom_inset 0.06`），恰好落在窄双打侧边距、不压车道/gauge/进度条；非 14K 单角仍 `side_size`（0.225×0.30）。
- **代价提示**：14K 四 player 对视频 BGA = 4 解码器（图片 BGA 廉价、共享纹理）；如视频卡顿可后续优化为单解码器镜像。仅 BGA 摆位/挂载，不碰解码逻辑。回归 `TestSceneBmsBgaPanelLayout`（14K=4 player、单打=1）+ `BmsBgaPlayerTest`（ResolveDefaultPlacement）。验证：BMS 全套 **933/933**、`osu.Desktop.slnf` Release **0 错误**。**人工实机视觉验收待用户确认**。

## 2026-06-15

### Phase 5.1：老式 BGA 视频外部 ffmpeg 转码播放（opt-in，缓存 + 本场热替换）

承接 2026-06-14 实测结论——框架捆绑的 FFmpeg 视频管线打不开老式 MPEG-1 program-stream `.mpg`（及 `.wmv/.avi/.flv`），此前只能回退静态图。用户拍板用**外部 ffmpeg** 让其真正播放。

- 新增 [BmsBgaVideoCache](../../../osu.Game.Rulesets.Bms/UI/BmsBgaVideoCache.cs)：`RequiresTranscode` 判老式扩展名（`.mpg/.mpeg/.avi/.wmv/.flv/.m1v/.m2v/.mkv`；`.mp4/.m4v/.mov/.webm` 直接放行）；`Resolve(srcAbs)→{Ready,Pending,Unavailable}`；ffmpeg 解析候选 = `<dataRoot>/ffmpeg.exe`、`<exeDir>/ffmpeg.exe`、PATH `ffmpeg`（首次 `Win32Exception` 即标记本会话不可用）；缓存目录 `<dataRoot>/bga-video-cache/`、文件名 = `SHA1(源路径|size|mtime).mp4`（源变即重转）；转码命令 `ffmpeg -y -i <src> -an -c:v libx264 -pix_fmt yuv420p -movflags +faststart <dst>.tmp`（`-an` 丢音频，**先写 .tmp 再原子改名**，故 `File.Exists(dst)` 即完整）；`Task.Run` 后台 + `inProgress` 去重 + 120s 超时；转码执行器可注入委托便于单测。
- 配置：[BmsRulesetConfigManager](../../../osu.Game.Rulesets.Bms/Configuration/BmsRulesetConfigManager.cs) 加 `BgaVideoTranscode`（默认 true）；[BmsSettingsSubsection](../../../osu.Game.Rulesets.Bms/BmsSettingsSubsection.cs) 加「转码无法解码的 BGA 视频」开关（注明需自备 ffmpeg）。
- [BmsBgaPlayer](../../../osu.Game.Rulesets.Bms/UI/BmsBgaPlayer.cs) 集成：load() 建 cache 并对 timeline 内 distinct 老式视频**预热转码**（争取 5s pre-start 内转好）；`createVideo` 经 cache：Ready→`Video(缓存mp4)`、Pending→静态回退占位、Unavailable→直开（老格式 fault→静态）；layer display 对 Pending **节流 1s 重试**，转好后**本场热替换**换入视频；faulted 永久标记不重试（不再黑屏）。
- **验证**：新增 `BmsBgaVideoCacheTest` 13（扩展名判定 / 友好格式直放 / 无缓存目录 Unavailable / 转码成功→Ready+缓存命中同路径 / 失败→Unavailable 且不留半成品）；BMS 全套 **946/946**；`osu.Desktop.slnf` Release 0 错（生产代码 0 警告）。**实机端到端需用户装 ffmpeg**（`winget install ffmpeg` 或放 `ffmpeg.exe` 进数据目录）后验 `flonne2_bga.mpg`。无 ffmpeg/关闭转码时＝静态图（无回归），`.mp4` 谱不受影响。约束见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md) Phase 5.1。

---

## 2026-06-14

### Phase 5（BGA 链路：背景图/背景动画）归线 + 落地（自动化通过；人工视觉验收待办）

审查「BMS 模式游玩时 background image/animation 链路」后定论：解析层（P1-K）已完整产出 BGA 事件与定义，但转换层只取一个静态 `metadata.BackgroundFile`、整条 BGA 时间线被丢弃；显示层 `BmsBackgroundLayer` 是静态占位件且挂在 `playfieldContainer` 内被不透明 lane 背板完全遮挡（14K DP 中缝可坐实）。把此前 mainline Phase 2 future-scope 的 BGA 视频/动画**正式激活并归入 P1-L**（演出视觉复刻），新增 **Phase 5** 并落地全链路。

- **冻结产品决策**（用户拍板）：图序列 + 视频一起做；默认镜像对侧（居左→BGA 右 / 居右→左 / 居中→右 / 14K DP→中缝）；letterbox（`FillMode.Fit`）；皮肤可定制控件 `BgaPanel` + 自定义皮肤接口预留；浮窗挂 `DrawableRuleset.Overlays`（非 OS 窗口）；仅 native 路径、converted-mania 不在 v1。
- **L5-1 数据携带**：新增 [BmsBgaTimelineEntry](../../../osu.Game.Rulesets.Bms/Beatmaps/BmsBgaTimelineEntry.cs)（time/layer/asset/isVideo + `IsVideoAsset` 扩展名判定）；[BmsBeatmap](../../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs) 加只读 `BgaTimeline` + `PoorBgaMode`；[BmsBeatmapConverter](../../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs) `buildBgaTimeline` 复用 `eventTimes`→绝对时间、`BitmapTable` 解析文件名、缺失跳过、按时间排序，照 `Mines`/`ScrollProfile` 不进 `HitObjects`。**关键修复**：`buildEventTimeline` 的 `register(...)` 此前不含 BGA 事件 → BGA 时刻不会进 `eventTimes`；已补 `register(decodedChart.BgaEvents...)`。
- **L5-2 运行时播放器**：新增 [BmsBgaPlayer](../../../osu.Game.Rulesets.Bms/UI/BmsBgaPlayer.cs)：时间线驱动 base/layer/layer2 合成（overlay 加色近似"黑透"）；图片走自建 `TextureStore`（backing `WorkingBeatmapFileStore` 委托 `WorkingBeatmap.GetStream`，直读 `chartbms/`、不经 hash store）、视频走 FFmpeg `Video`+`PlaybackPosition`（跟 frame-stable 游玩时钟，因 `Overlays` 在 `FrameStabilityContainer` 内，seek/retry 安全，复用 `DrawableStoryboardVideo` 范式）；POOR 层按 `#POORBGA` 在 miss flash；letterbox；纯函数 `GetActiveIndex` 二分；解码失败/缺失优雅降级（log Debug、不刷错误）。
- **L5-3 皮肤浮窗 + 挂载 + 布局**：新增 `BmsSkinComponents.BgaPanel`、[BmsBgaPanel](../../../osu.Game.Rulesets.Bms/UI/BmsBgaPanel.cs)（`IBmsBgaPanelDisplay` + `DefaultBmsBgaPanelDisplay`，`BmsSkinTransformer` 照 `StaticBackgroundLayer` 门控）；挂 [DrawableBmsRuleset.Overlays](../../../osu.Game.Rulesets.Bms/UI/DrawableBmsRuleset.cs)，默认 placement `ResolveDefaultPlacement` 镜像 playfield；无 BGA 时浮窗回退首选静态图；`ShowBga` 开关；miss 经 `HandleGameplayJudgementResult` 转发。`BmsPlayfield` 退役被遮挡的 `BackgroundLayer` 挂点（property 保留作兼容）。
- **实机验收（用户）+ 微调**：视频 BGA 实机确认在浮窗内正常播放 ✅。按反馈两项微调：① **全屏背景**——此前 [DefaultBmsPlayfieldBackdropDisplay](../../../osu.Game.Rulesets.Bms/UI/BmsDefaultPlayfieldShellDisplays.cs) 是覆盖全屏的不透明深色 Box，把谱面背景完全挡黑（游玩时 `BlurAmount=0` 的全局背景也被它盖住）；改为渲染谱面背景的**模糊（sigma≈20，半分辨率 BufferedContainer，同 song-select）+ 轻暗化（黑 0.4）**铺满整个背景面，play strip（baseplate+lanes）与 BGA 浮窗仍在其上，无背景时回退原深色 Box。② **BGA 浮窗**——默认尺寸缩到 ~75%、由居中侧锚改为**顶角锚**（右→右上 / 左→左上 / 中→顶中），让出 playfield 且避开顶部 gauge/combo。
- **老式 MPEG-1 `.mpg` BGA 全黑 → 改为静态图回退**（用户报 `Love & Justice/_05_flonne_bt4_god.bme`，BGA=`flonne2_bga.mpg`）。runtime 日志实锤根因＝框架 `VideoDecoder.prepareDecoding()` 在打开阶段 `AVERROR_INVALIDDATA`（非我方链路问题，base 事件 `#00004:01` 正确建立；文件头 `00 00 01 BA` 是合法 MPEG program-stream、beatoraja 能播）。[BmsBgaPlayer](../../../osu.Game.Rulesets.Bms/UI/BmsBgaPlayer.cs) 两层处理：① 视频**优先按绝对文件路径** `new Video(path,…)`（BMS 恒文件系统直读，external 用绝对 `FilesystemStoragePath`、internal 用 `Storage.GetFullPath`），无路径回退 stream；② `Video.IsFaulted` 检测，base 层**回退 STAGEFILE 静态图**（不再黑屏），overlay/poor 隐藏。**实测结论**：该 `.mpg` 按路径打开后**仍 `INVALIDDATA`**——框架捆绑的 FFmpeg（avformat-58/4.x）视频管线对这种老式 program-stream 的打开/探测就失败，**非 BMS 侧可修**（需框架 `VideoDecoder` fork）；filename 路径对它无效，真正生效的是静态图回退。现代 `.mp4`/H.264 BGA 正常播放、无回归。若要让老 `.mpg` 真正播放，唯一现实路径是导入期/工具 transcode `.mpg→.mp4`（独立后续项，未做）。约束 #6 已细化。
- **验证**：BMS 全套 **933/933**（新增 17：converter 2 + player 13 + skin 2）；`osu.Desktop.slnf` Release **0 错误、0 警告**。剩余人工项：图序列/POOR/14K 中缝/seek 同步逐谱抽验。约束见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md) Phase 5。

---

## 2026-06-13

### 修复：地雷不随 `Mirror` / `Random` 重排移动（重排后与谱面错位）

审查 BMS `Random` mod 全链路时发现：地雷（`BmsBeatmap.Mines`，按 Phase 1 #2 刻意在 `beatmap.HitObjects` 之外）此前**完全不参与** `Mirror`/`Random` 的 lane 重排——`BmsLaneRearrangement` 只遍历 `HitObjects.OfType<BmsHitObject>()`，音符移动后地雷仍留原轨，与重排后的谱面错位（与 beatmania「整列连同地雷一起换」不符）。地雷此处为视觉-only、不扣血/不计分，故属还原度缺口而非计分 bug。**修复**：`applyPermutation`（覆盖 `Mirror`/`RANDOM`/`R-RANDOM`/自定义 pattern 全部置换模式）在重排音符后，用**同一份 lane 映射**同步重排 `BmsBeatmap.Mines` 的 `LaneIndex`；地雷仍不进 `HitObjects`（守住 Phase 1 #2/#3），per-group 映射不相交故 14K 不会双重重排。`S-RANDOM` 逐时刻散布、无单一列置换，故地雷保持原位（已注释为已知边界）。同批修复 applicator 重复应用导致的 `Random` 失真（详见 [mainline CHANGELOG](../../mainline/CHANGELOG.md) 2026-06-13）。验证：新增 `TestMirrorMovesMinesWithLanes` / `TestRandomCustomPatternMovesMinesWithNotes`（mines 切片）；同日 `Random` 重排修复 + custom-pattern UX 切片一并收口后，BMS 全套 **887/887**、`osu.Desktop.slnf` Release 0 错误。新增约束 Phase 1 #6。

---

## 2026-05-29

### Phase 2 Step D：演出谱自动检测（`Auto` 可用，默认仍 OFF）

让门控 `Auto` 仅对自动识别为演出谱的谱启用旁路。检测为 `BmsScrollProfile` 的纯函数，基于两条保守信号（实测 DEAD SOUL vs 正常谱区分度极大）：

- [BmsScrollProfile](../../../osu.Game.Rulesets.Bms/Beatmaps/BmsScrollProfile.cs) 新增纯指标 `MaxSlope`（最快段相对 base 的倍率：base≈1、STOP=0、132 万 BPM snap≈10000）与 `FrozenFraction`（STOP 冻结时长占比），及 `IsStopMotionGimmick = MaxSlope ≥ 50 || FrozenFraction ≥ 0.05`。正常/中等 soflan（< ~10×、~0% 冻结）稳不触发；DEAD SOUL（10000×、43%）必触发。
- [BmsPlayfield.updateGimmickScroll](../../../osu.Game.Rulesets.Bms/UI/BmsPlayfield.cs)：`Auto` → `profile.IsStopMotionGimmick` 才 engage；`On` 恒 engage、`Off` 恒不。
- **默认改为 `Auto`**（用户拍板）：特效/变速谱开箱即用，正常谱因保守检测不命中而走常规路径、零改动；`Off` 仍是回退开关。设置面板文案改为发行向（caption「特效谱滚动（实验性）」+「若出现异常调回 Off」）。**红线**：默认 Auto 下「非 gimmick 谱零改动」依赖检测无误报（已保守）+ `Off` 兜底。
- 验证：新增 `BmsScrollProfileTest` 检测项（snap/freeze-only → true，normal/moderate-soflan → false）+ converter 端真实检测（extreme-BPM 谱 true、单 BPM 谱 false）；默认改 Auto 后 BMS 全套 **860/860**（gameplay TestScene 默认 Auto、简单测试谱不命中检测 → 行为不变）；Release 0 错误、生产代码 0 新增警告。

### 修复：最右键轨道地雷不显示（Phase 1 既有 off-by-one，Phase 2 暴露）

人工验证 DEAD SOUL 时发现 7K **最右键轨道（lane 7）地雷不渲染**。根因：`BmsBeatmapConverter.buildMines` 用 `BmsRuleset.GetKeyCount`（**键数**=7）做 `laneIndex >= bound` 丢弃上界，但 scratch 占 lane 0 使最右键映射到 lane index 7（`mapLaneIndex(7K,0x19)=7`），`7>=7` 被误丢；音符路径无此检查故不受影响。5K(lane5)/14K(lane14,15) 同类受害。

- 新增权威 [BmsRuleset.GetLaneCount](../../../osu.Game.Rulesets.Bms/BmsRuleset.cs)`(keymode)`（键 + scratch：5K=6/7K=8/9K=9/14K=16），`buildMines` 改用之；越界（如单打谱里的 P2 通道）仍丢弃，不 mis-map。
- DRY：[BmsLaneLayout.getExpectedLaneCount](../../../osu.Game.Rulesets.Bms/UI/BmsLaneLayout.cs) 委托给 `BmsRuleset.GetLaneCount`，消除两份重复的 keymode→轨道数映射（单一真源，防再犯）。
- 回归：新增 `TestBuildsMineOnRightmostKeyLane`（7K channel D9 → 地雷落在 lane 7 不被丢）；BMS 全套 **855/855**；Release 0 错误、生产代码 0 新增警告。

### Phase 2（Step A–C）：BMS 专用滚动位置积分旁路落地（门控默认 OFF）

落地 beatoraja 风格的逐对象位置积分旁路，让 DEAD SOUL [Revive] 这类定格动画演出谱的「瞬移 snap / STOP 真冻结 / measure-length 任意定高」成立。**绕开而非改写**共享核心：不动 `TimingControlPoint` 的 `[6,60000]` 钳制、不动 `ScrollingHitObjectContainer`，注入全在 BMS 侧；**判定/计分继续走 `HitObject.StartTime` 时间链路，语义不变**；**门控默认 OFF，对所有谱（含演出谱）渲染零变化**。

**Step A — 位置积分数据模型（零渲染改动）**
- 新增 [../../../osu.Game.Rulesets.Bms/Beatmaps/BmsScrollProfile.cs](../../../osu.Game.Rulesets.Bms/Beatmaps/BmsScrollProfile.cs)：纯函数分段线性 `D(t)`（`DistanceAt`/`PositionDelta`/`TimeAtDistance`，二分 + 端点外推），无 framework 依赖、可独立单测。
- [BmsBeatmapConverter.buildEventTimeline](../../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs) 在既有时间游走里**并行积分** D 的 knots（用原始未钳制 BPM/STOP/measure-length/scroll；STOP 段 `dD=0` 真冻结、132 万 BPM 段斜率暴涨 = snap），经 `TimelineBuildResult` 挂到 [BmsBeatmap.ScrollProfile](../../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs)（不进 `HitObjects`）。距离按非冻结时长最常见 BPM 缩放成 base-BPM ms（`computeBaseBpm`）。

**Step B — stop-motion 算法 + IScrollingInfo 重缓存（门控 OFF）**
- 新增 [BmsStopMotionScrollAlgorithm](../../../osu.Game.Rulesets.Bms/UI/Scrolling/BmsStopMotionScrollAlgorithm.cs)`: IScrollAlgorithm`（包 `BmsScrollProfile`，5 个接口方法以 D/D⁻¹ 实现，与 `ConstantScrollAlgorithm` 同形而以距离替代时间）。
- 新增 [BmsScrollingInfo](../../../osu.Game.Rulesets.Bms/UI/Scrolling/BmsScrollingInfo.cs)`: IScrollingInfo`：包裹基类，Direction/TimeRange 透传，Algorithm 默认 `GetBoundCopy` 逐实例跟随基类，仅 `EngageStopMotion`/`Disengage` 切换。
- [BmsPlayfield.CreateChildDependencies](../../../osu.Game.Rulesets.Bms/UI/BmsPlayfield.cs) 把 `BmsScrollingInfo` `CacheAs<IScrollingInfo>` 给子节点（lanes + 容器）——**零核心文件改动的注入点**；解析不到基类 info 时退回基类行为（防御）。
- 新增门控 [BmsGimmickScrollMode](../../../osu.Game.Rulesets.Bms/Configuration/BmsGimmickScrollMode.cs)`{ Off, On, Auto }` + `BmsRulesetSetting.GimmickScrollMode` 默认 `Off`；`BmsPlayfield.updateGimmickScroll` 按门控 engage/disengage。

**Step C — 标定 + 显式 ON 入口 + 端到端冻结验证**
- **标定关键发现（实测 DEAD SOUL）**：因 STOP 冻结占 43.1%（141s 中 60.8s，stop-freeze 点 beatLength=6）+ 132 万 BPM 钳到 6，`GetMostCommonBeatLength` 返回 **6（BPM 10000）而非 132**；正常链路因此把 132 段压成 multiplier≈0.013（squash 实锤）。但**默认 Normal hi-speed 模式下 `timeRange` 与 `GetMostCommonBeatLength` 无关**（modeScale=1，见 `BmsHiSpeedRuntimeCalculator`），而 profile `baseBeatLength=454.5`（原始 132）使 base 段 `D≈t` → 旁路 base 段 PositionAt 与正常 132 谱同速：**Normal 模式零标定即忠实**。Floating/Classic 的 modeScale 用了 6，绝对刻度偏差留 Phase 4 标定。
- 设置面板 [BmsSettingsSubsection](../../../osu.Game.Rulesets.Bms/BmsSettingsSubsection.cs) 新增「演出谱滚动（实验性）」下拉（Off/On/Auto），供显式开启与人工验证。
- 端到端测试：真实 converter 产出的 profile 喂进算法，断言转换链路在 STOP 区间真冻结。

**验证**：新增 `BmsScrollProfileTest`(11)、`BmsStopMotionScrollAlgorithmTest`(5)、`BmsScrollingInfoTest`(4) + 扩 `BmsBeatmapConverterTest`（profile 冻结/退化/snap/端到端 4 项）；`dotnet test osu.Game.Rulesets.Bms.Tests` **854/854**；`dotnet build osu.Desktop.slnf -p:Configuration=Release` **0 错误**、生产代码 0 新增警告。**正常链路无回归**：默认 OFF 时 `BmsScrollingInfo` 逐实例跟随基类（单测锁定）+ 全部 Player 系 gameplay TestScene（真实加载 DI 重缓存路径）全绿。

**待办（非本次）**：① DEAD SOUL 逐帧对照 beatoraja 的**人工视觉验收**（交接，归 Phase 4）；② Step D 自动检测（`Auto` 当前等同 `Off`）；③ Floating/Classic 绝对刻度标定；④ 负向/反向滚动（Phase 3）。

### 子线建线 + Phase 1：地雷视觉呈现落地

由 [../../other/BMS_GIMMICK_CHART_RENDERING.md](../../other/BMS_GIMMICK_CHART_RENDERING.md) 可行性分析升级为正式子线 `P1-L`，并落地 Phase 1（地雷渲染），目标是为后续忠实复刻 DEAD SOUL [Revive] 这类演出谱打地基。**本轮零滚动模型改动、零判定/计分改动、零正常游玩链路风险。**

**地雷渲染（视觉-only，仿小节线、完全隔离）**

此前地雷（channel D/E）解码进 `MineEvents` 但**从不渲染**（演出谱的主要"像素"直接缺失）。本轮把地雷渲染为可视、非判定对象：

- 新增 [../../osu.Game.Rulesets.Bms/Objects/BmsMine.cs](../../../osu.Game.Rulesets.Bms/Objects/BmsMine.cs)：`HitObject` + `IgnoreJudgement` + 空 hit window，仅携 `LaneIndex`。
- 新增 [../../osu.Game.Rulesets.Bms/UI/DrawableBmsMine.cs](../../../osu.Game.Rulesets.Bms/UI/DrawableBmsMine.cs)：仿 `DrawableBmsBarLine` 的 drawable（**非 `DrawableBmsHitObject`**），`HandleUserInput=false`、`DisplayResult=false`、`CheckForResult` 走 ignore-judgement；Phase 1 用非皮肤简单圆形（皮肤化后置）。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs](../../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs)：把 `MineEvents` 的 `(measure,fraction)` 注册进 `eventTimes`；新增 `buildMines` 按 `channel-0xC0` 把 D1-D9/E1-E9 映射回可见通道 11-19/21-29 → `mapLaneIndex` → lane 范围校验 → 按时间排序，写入 `BmsBeatmap.Mines`。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs](../../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs)：新增 `Mines` 列表，**刻意不进 `HitObjects`**。
- [../../osu.Game.Rulesets.Bms/UI/BmsPlayfield.cs](../../../osu.Game.Rulesets.Bms/UI/BmsPlayfield.cs)：新增 `addMines`，仿 `addMeasureBarLines` 在构造期把 `DrawableBmsMine` 直接加到对应 lane。

**隔离保证（红线落地）**：地雷不进 `beatmap.HitObjects` → 不影响 `TotalObjectCount`/统计/计分/judged-note；`DrawableBmsMine` 非 `DrawableBmsHitObject` → 被 `OfType<DrawableBmsHitObject>`（empty-poor / 候选音符扫描 / 键音时间线）天然排除；`IgnoreJudgement` + `DisplayResult=false` → 不计分、不弹判定。完全复用小节线已验证安全的模式。

**验证**：新增 focused `TestBuildsMinePlacementsWithoutLeakingIntoJudgedObjects`（锁地雷 lane=1/time=2500 + `HitObjects` 无 `BmsMine` + `TotalObjectCount` 不含地雷）；`BmsBeatmapConverterTest` **16/16**；`dotnet test osu.Game.Rulesets.Bms.Tests` **831/831**；`dotnet build osu.Desktop.slnf -p:Configuration=Release` **0 错误**、生产代码 0 新增警告。

**已知限制**：Phase 1 仅让地雷可见；在现有前进式滚动下 DEAD SOUL 的"瞬移定格"仍被 squash，故该谱尚未忠实复刻（预期内，需 Phase 2 专用滚动旁路）。后续非阻塞项：地雷皮肤化、触雷伤害语义、极端谱地雷性能。
