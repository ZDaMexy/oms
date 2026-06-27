# P1-L 开发进度：BMS 演出/Gimmick 谱视觉复刻

> 最后更新：2026-06-23
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。完整分析见 [../../other/BMS_GIMMICK_CHART_RENDERING.md](../../other/BMS_GIMMICK_CHART_RENDERING.md)。

## 当前阶段

- **阶段定位**：本子线由 `other/BMS_GIMMICK_CHART_RENDERING.md` 可行性分析升级而来。**Phase 1（地雷视觉呈现）已落地**；**Phase 2（BMS 专用滚动位置积分旁路）Step A–C 已落地**（门控默认 OFF、Normal 模式忠实、可显式开启），其内的 Step D（自动检测）与 DEAD SOUL 逐帧人工视觉验收（Phase 4）未完成；Phase 3（负向/反向）未开工。
- **当前主线**：**Phase 5（BGA 链路：背景图/背景动画）实现已落地（2026-06-14），自动化验证通过，实机逐谱人工视觉验收待办**——把此前 mainline Phase 2 future-scope 的 BGA 视频/动画激活并归入本线。设计与切片见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md) Phase 5、约束见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md) Phase 5。

## Phase 5（BGA）已落地事实

- **修复的缺口**：解析层（P1-K）产出完整 BGA 事件与定义，但此前转换层只取一个静态 `metadata.BackgroundFile`、BGA 时间线被丢弃；显示层 `BmsBackgroundLayer` 是静态占位件且挂在 `playfieldContainer` 内、位于不透明 lane 背板（`DefaultBmsLaneBackgroundDisplay`）之下 → 游玩时完全被遮挡。
- **已落地链路**：
  - 转换携带 [BmsBgaTimelineEntry](../../../osu.Game.Rulesets.Bms/Beatmaps/BmsBgaTimelineEntry.cs) → [BmsBeatmap.BgaTimeline](../../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs) + `PoorBgaMode`（[BmsBeatmapConverter](../../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs) 复用 `eventTimes`、`BitmapTable` 解析、扩展名判定视频、缺失跳过；**注册 BGA 事件进 `eventTimes`** 是关键——此前 `register(...)` 未含 BGA）；照 `Mines`/`ScrollProfile` 不进 `HitObjects`。
  - 运行时 [BmsBgaPlayer](../../../osu.Game.Rulesets.Bms/UI/BmsBgaPlayer.cs)：时间线驱动 base/layer/layer2 合成（overlay 层加色近似"黑透"），图片走自建 `TextureStore`（backing 委托 `WorkingBeatmap.GetStream`，直读 `chartbms/`、不经 hash store）、视频走 FFmpeg `Video`+`PlaybackPosition`（跟 frame-stable 游玩时钟，seek/retry 安全）；POOR 层按 `#POORBGA`（Default 替换 / Overlay 叠加 / Undisplayed 不显）在 miss flash；letterbox `FillMode.Fit`；选择逻辑 `GetActiveIndex` 二分、纯函数。
  - 皮肤浮窗 [BmsBgaPanel](../../../osu.Game.Rulesets.Bms/UI/BmsBgaPanel.cs)（`BmsSkinComponents.BgaPanel` + `IBmsBgaPanelDisplay` + `DefaultBmsBgaPanelDisplay`，`BmsSkinTransformer` 照 `StaticBackgroundLayer` 门控）挂 [DrawableBmsRuleset.Overlays](../../../osu.Game.Rulesets.Bms/UI/DrawableBmsRuleset.cs)（playfield 之上、不被遮挡）；默认 placement `ResolveDefaultPlacement` 走**屏幕角落**（`BmsBgaPlacement` 枚举 = `TopLeft/TopRight/BottomLeft/BottomRight/Center`；5/7/9K 单角镜像 playfield 侧：P1→右上/P2→左上/居中·9K→右上；**14K→四角各一个 BGA**，2026-06-20 由原"中缝"改）；无 BGA 时浮窗回退显示首选静态图；`ShowBga` 开关（[设置](../../../osu.Game.Rulesets.Bms/BmsSettingsSubsection.cs)）；miss 经 `HandleGameplayJudgementResult` 转发 `NotifyMiss`（14K 转发到四个 player）。
  - 退役：`BmsPlayfield` 不再挂被遮挡的 `BackgroundLayer`（property 保留作皮肤/元数据兼容）。
  - 全屏背景：[DefaultBmsPlayfieldBackdropDisplay](../../../osu.Game.Rulesets.Bms/UI/BmsDefaultPlayfieldShellDisplays.cs) 改为渲染谱面背景的**模糊+轻暗化**铺满整个背景面（同 song-select 观感），取代此前把背景挡黑的全屏不透明 Box；无背景回退原深色 Box。
  - BGA 浮窗默认尺寸：5/7/9K 单角 `side_size`(0.225×0.30)；14K 四角各 `corner_14k_size`(0.13×0.16) 贴窄双打侧边距、不压车道/gauge/进度条（2026-06-20）。避开 playfield 与顶部 HUD。
- **产品决策（已落地）**：图序列 + 视频一起做；默认走屏幕角落镜像 playfield 侧（5/7/9K 单角 P1→右上/P2→左上/居中→右上；**14K DP→四角各一个 BGA**，2026-06-20 由"中缝"改，因中缝压游玩区/combo；keymode 用 `GameplayState` 可靠解析）；letterbox（`FillMode.Fit`）；皮肤可定制控件（`BgaPanel`）+ 接口预留；仅 native 路径、converted-mania 不在 v1。
- **验收**：视频 BGA（.mp4/H.264）实机播放已用户确认 ✅（2026-06-14）；剩图序列/POOR/seek 同步逐谱抽验交接人工。**14K 四角摆位 2026-06-20 落地**（回归 `TestSceneBmsBgaPanelLayout`，实机视觉待确认；代价：14K 视频 BGA = 4 解码器）。overlay"黑透" + `#ARGB` 当前为加色近似，保真后续。
- **Phase 5.1（老式视频转码，2026-06-15 落地，opt-in）**：框架 FFmpeg 打不开老式 MPEG-1 `.mpg`（实测 `AVERROR_INVALIDDATA`，2026-06-14 已回退静态图）。新增 [BmsBgaVideoCache](../../../osu.Game.Rulesets.Bms/UI/BmsBgaVideoCache.cs) 用**外部 ffmpeg**（用户自备：PATH 或放进数据目录）把老式视频转 `.mp4` 缓存（`<dataRoot>/bga-video-cache/`，键 = `SHA1(路径|size|mtime)`），[BmsBgaPlayer](../../../osu.Game.Rulesets.Bms/UI/BmsBgaPlayer.cs) 预热转码 + 转好后本场热替换；`BgaVideoTranscode` 开关默认开，无 ffmpeg/关闭时＝静态图回退（无回归）。实机端到端待用户装 ffmpeg 后验。**设置 UI（2026-06-22）**：该开关在设置面板改名「ffmpeg完整BGA支持」、描述精简为「对老式BGA提供转码播放支持，需自行放置ffmpeg到数据目录」（config 键 `BgaVideoTranscode` 不变，仅展示层）；新增两个按钮——「检测 ffmpeg 安装状态」与「打开 ffmpeg 安装目录」（`host.OpenFileExternally` 打开数据目录根）。**转码健壮性跟进（用户反馈 .mpg 仍静态）**：① 失败可见化＝转码失败把 ffmpeg 输出记日志（不再静默）；② 「检测」改走 `BmsBgaVideoCache.ProbeFfmpeg`（**真跑** `ffmpeg -encoders` 验证可执行 + 是否含 libx264），回应「检测只查文件存在」疑点；③ 编码器回退 libx264→内置 mpeg4（防御非完整版 ffmpeg）。**真因（实机日志定位）＝输出写 `<hash>.mp4.tmp`，ffmpeg 按扩展名推断容器、`.tmp` 选不出 muxer → `Unable to choose an output format`（libx264 其实一直可用，与回退 mpeg4 同错，证实是容器/扩展名非编码器）。真修＝转码命令加 `-f mp4`**（`BuildTranscodeArguments` 抽出可测）。降噪＝完整 ffmpeg stderr 记 Verbose、每失败源只发一条 Important 摘要（去重）。**三次跟进（`-f mp4` 后转码成功但视频全黑 + runtime 3614 行刷屏）＝产出 H.264 mp4 但框架内置 FFmpeg 解不了（D3D11VA 硬解每包 `Invalid data` → 软解回退仍每包失败 → 0 帧黑，3360 行刷屏）。修＝转码约束最广兼容 H.264：libx264 `-profile:v baseline`（无 B 帧/CABAC）+ `-vf setpts=PTS-STARTPTS,setsar=1` + `-map 0:v:0`；并 `cacheKey` 加 `transcode_version` 失效旧坏缓存自动重转。** 回归 `TestTranscodeArgumentsSpecifyMp4MuxerForTmpOutput`（+baseline 断言）/`TestTranscodeArgumentsOmitH264ProfileForMpeg4Fallback`。**真因确诊（五次跟进，前面 profile/HW/demux 假设全错）＝并发转码写同一个固定 temp `<hash>.mp4.tmp` 互相穿插→产物字节交错不可解码（`Invalid NAL unit size`）；坏文件落缓存后被 `File.Exists` 永久端出**。证据：用户 ffmpeg（完整 gyan.dev 8.x 含 libx264）解不了自己缓存的 .mp4；同参数全新转码解码干净；模拟两 ffmpeg 并发写同一 temp 复现出一模一样的 `Invalid NAL unit size (0 > 25614)`。根源＝转码 Task 在 BgaPlayer dispose 不取消、退出/快速重放对同一 temp 起第二个 ffmpeg。**修复**：temp 加 Guid（并发各写各的）+ `inProgress` 改 static（跨实例去重）+ `File.Move(...,overwrite:true)` 原子发布 + `transcode_version`→3 失效旧坏缓存。配套上一轮的干净降级（看门狗 `Video.FramesProcessed` 1s 内 0 帧 drop+dispose 掐刷屏 / 静态优先于黑 / legacy 不裸开 .mpg / 转码失败日志 Verbose）仍在。详见 [CHANGELOG.md](CHANGELOG.md) 2026-06-22。**2026-06-22 用户确认老 `.mpg` BGA 可正常播放 ✅。**
- **Phase 5.2（转码加载体验与缓存治理，2026-06-23 落地 R1/R2/R3/R4，实机验收待办）**：① **R1 开局即播**＝新增 [BmsBgaVideoPreloader](../../../osu.Game.Rulesets.Bms/UI/BmsBgaVideoPreloader.cs) 直接挂 `DrawableBmsRuleset.Overlays`，后台 `load()` 阻塞预热+等待（cap 8s）推迟 player push 到转码完成 → BGA 第一帧即播（预热端/播放端共享磁盘目录、`File.Exists` 命中即 Ready；超时回落 Phase 5.1 静态→热替换、只增不减）。② **R2/R3 会话级缓存**＝`BmsBgaVideoCache.ClearSessionCacheOnce` 每进程一次性清空 `bga-video-cache/`（转码前清、`lock` 防竞态），会话内重进即时、跨会话不累积（用户选②）。③ **R4 提速**＝libx264 `-preset ultrafast`（不动 baseline 可解码性），`transcode_version`→4；**NVENC 不纳入**（用户选安全优先）。④ 转码 `inProgress` 改 `Lazy<Task>`、`startTranscode` 可 join，14K 4 player+preloader 对同一产物 join 同一 Task（总耗时≈一次转码）。⑤ **R5 加载扫描线进度揭示**（用户截图给的方向）＝缩略图加载指示从「暗覆盖+转圈」换成**从左到右扫亮的扫描线**（新 [ScanlineLoadingLayer](../../../osu.Game/Screens/Play/ScanlineLoadingLayer.cs)，**仅 BMS** 门控、非 BMS 保留原 `LoadingLayer`）；**有真实转码进度按 % 揭示、无进度乒乓循环**；进度跨 DI 桥＝新 [GameplayLoadProgress](../../../osu.Game/Screens/Play/GameplayLoadProgress.cs)（`[Cached]` 于 `PlayerLoader`，被其子 `BeatmapMetadataDisplay` 与异步加载的 player 子树同享、线程安全），`BmsBgaVideoCache` 逐行解析 ffmpeg stderr `Duration:`+`time=` 推进度（无进度→乒乓兜底，故观感不依赖进度）。**2026-06-23 用户实机初步未见异常，逐谱视觉细验待办。**详见 [CHANGELOG.md](CHANGELOG.md) 2026-06-23、约束 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md) Phase 5.2。
- **红线状态**：Phase 2 落地**绕开而非改写**共享核心——不动 `TimingControlPoint` 钳制、不动 `ScrollingHitObjectContainer`，注入全在 BMS 侧（`BmsPlayfield.CreateChildDependencies` 重缓存 `IScrollingInfo`）；判定/计分继续走 `HitObject.StartTime` 时间链路、语义不变；门控默认 OFF，对所有谱渲染零变化、可一键回退。

## 已确认事实

- 机理：DEAD SOUL [Revive] 是**定格动画**演出谱（132 万 BPM 瞬移 + measure-length 摆位 + STOP 定帧 + 大量地雷作像素，全谱无负值）。osu! 前进式滚动 + `BeatLength` 钳制 `[6,60000]` + `RelativeScaleBeatLengths` 会压扁极端反差，故现模型无法忠实复刻（详见可行性文档第 3-4 节）。
- [../../osu.Game.Rulesets.Bms/Objects/BmsMine.cs](../../../osu.Game.Rulesets.Bms/Objects/BmsMine.cs)：地雷为 `HitObject` + `IgnoreJudgement` + 空 hit window，仅携 `LaneIndex`。
- [../../osu.Game.Rulesets.Bms/UI/DrawableBmsMine.cs](../../../osu.Game.Rulesets.Bms/UI/DrawableBmsMine.cs)：仿小节线 drawable（非 `DrawableBmsHitObject`），Phase 1 用非皮肤简单圆形；`DisplayResult=false`、`HandleUserInput=false`、`CheckForResult` 走 ignore-judgement。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs](../../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs)：注册地雷时间键到 `eventTimes`，`buildMines` 按 `channel-0xC0` 映射回 lane、范围校验、按时间排序，写入 `BmsBeatmap.Mines`。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs](../../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs)：`Mines` 列表**不进 `HitObjects`**。
- [../../osu.Game.Rulesets.Bms/UI/BmsPlayfield.cs](../../../osu.Game.Rulesets.Bms/UI/BmsPlayfield.cs)：`addMines` 仿 `addMeasureBarLines` 直接把 `DrawableBmsMine` 加到对应 lane。
- 隔离验证：地雷不进 `HitObjects`（`TotalObjectCount`/统计/计分不受影响）；`DrawableBmsMine` 非 `DrawableBmsHitObject`（empty-poor / 键音时间线不受影响）。

## Phase 2 已确认事实（Step A–C）

- **位置积分模型**：[BmsScrollProfile](../../../osu.Game.Rulesets.Bms/Beatmaps/BmsScrollProfile.cs) 是分段线性 `D(t)`，由 `BmsBeatmapConverter.buildEventTimeline` 在既有时间游走里**并行积分**（原始未钳制 BPM/STOP/measure-length/scroll；STOP 段 `dD=0` 真冻结、132 万 BPM 段斜率暴涨 = snap），挂 [BmsBeatmap.ScrollProfile](../../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs)，不进 `HitObjects`。距离单位 = base-BPM ms（base = 非冻结时长最常见 BPM）。
- **注入机制（零核心改动）**：[BmsScrollingInfo](../../../osu.Game.Rulesets.Bms/UI/Scrolling/BmsScrollingInfo.cs) 包裹基类 `IScrollingInfo`（Direction/TimeRange 透传，Algorithm 默认逐实例跟随基类），由 [BmsPlayfield.CreateChildDependencies](../../../osu.Game.Rulesets.Bms/UI/BmsPlayfield.cs) 重缓存给 lanes；门控 ON 时 `EngageStopMotion([BmsStopMotionScrollAlgorithm](../../../osu.Game.Rulesets.Bms/UI/Scrolling/BmsStopMotionScrollAlgorithm.cs))`。
- **门控**：[BmsGimmickScrollMode](../../../osu.Game.Rulesets.Bms/Configuration/BmsGimmickScrollMode.cs)`{ Off(默认), On, Auto }`，设置面板「演出谱滚动（实验性）」下拉可切。`On` 恒启用；`Auto`（**默认**，Step D 已实现）仅对 `BmsScrollProfile.IsStopMotionGimmick`（`MaxSlope ≥ 50 || FrozenFraction ≥ 0.05`，保守区分特效/变速谱与正常/中等 soflan）命中的谱启用；`Off` 恒不启用、为回退开关。默认 Auto 下「非 gimmick 谱零改动」依赖检测无误报（已保守）+ `Off` 兜底。
- **标定结论（实测 DEAD SOUL [Revive]）**：STOP 冻结占 **43.1%**、snap 斜率 **10000×**、地雷 5645、knots 6522；`GetMostCommonBeatLength` 实测 **6（BPM 10000）**（被 STOP-freeze/钳制点拉低）——正常链路对 132 段 squash 实锤。**但默认 Normal hi-speed 模式 `timeRange` 与 `GetMostCommonBeatLength` 无关**（modeScale=1），profile base=454.5（132）使 `D≈t`，故 **Normal 模式零标定即忠实**；Floating/Classic 绝对刻度偏差归 Phase 4。

## 当前验证基线

- focused（滚动/地雷）：`BmsScrollProfileTest` 11、`BmsStopMotionScrollAlgorithmTest` 5、`BmsScrollingInfoTest` 4，`BmsBeatmapConverterTest` 含 profile 冻结/退化/snap/端到端冻结。
- focused（BGA Phase 5）：`BmsBeatmapConverterTest` 新增 2（时间线层/时刻/视频判定/缺失跳过/PoorBgaMode）；`BmsBgaPlayerTest` 13（`GetActiveIndex` 边界/tie/seek + `ResolveDefaultPlacement` 8 布局映射）；`BmsSkinTransformerTest` 新增 2（`BgaPanel` OMS fallback / 非 OMS null）。
- focused（BGA Phase 5.1）：`BmsBgaVideoCacheTest` 13（扩展名判定 / 友好格式直放 / 无缓存目录 Unavailable / 转码成功→Ready+缓存命中同路径 / 失败→Unavailable 且不留半成品；注入 runner 不依赖真 ffmpeg）。
- 更宽：`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj -c Release` **954/954**（Phase 5.2 验证基线；同日稍后 P1-J #12 + K12 使全量增至 **961/961**，见 [P1-K](../P1-K/DEVELOPMENT_STATUS.md) / [P1-J](../P1-J/DEVELOPMENT_STATUS.md)）；`dotnet build osu.Desktop.slnf -p:Configuration=Release` **0 错误**（生产代码 0 新增警告；2 个既有 test 警告未动）。最近一次验证：2026-06-23（BGA Phase 5.2 R1–R5 落地）。
- **正常链路无回归证明**：默认 OFF 时 `BmsScrollingInfo.Algorithm` 逐实例跟随基类（`BmsScrollingInfoTest` 锁定）；全部 Player 系 gameplay TestScene（真实加载 DrawableBmsRuleset→BmsPlayfield→lanes，实跑 DI 重缓存路径）全绿。

## 已知限制 / 下一步

- **DEAD SOUL 逐帧人工视觉验收未做**（交接给人工 / Phase 4）：自动化已证明转换链路产出正确的 freeze/snap 且 Normal 模式 base 段忠实，且用户实跑反馈观感已对路；但「与 beatoraja 逐帧对照」仍需人工。验证方式：设置 → BMS →「演出谱滚动」选 On（或 Auto），进 DEAD SOUL 实跑。
- **默认已改 `Auto`**（用户拍板）：特效/变速谱开箱即用，正常谱不命中检测、走常规路径；`Off` 为回退开关。
- **Floating/Classic 模式绝对刻度标定**、**负向/反向滚动（Phase 3）** 未做。
- 非阻塞后续：地雷皮肤化、触雷伤害语义（跨 P1-C/Scoring）、极端谱（5645 地雷/6522 knots）地雷与对象池性能（P1-J 协同）。
