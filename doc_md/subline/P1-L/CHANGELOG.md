# P1-L 变更日志：BMS 演出/Gimmick 谱视觉复刻

> 本文件记录 `P1-L` 相关的验证通过变更，按时间倒序排列。
> 当前进度见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，执行规划见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

---

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
