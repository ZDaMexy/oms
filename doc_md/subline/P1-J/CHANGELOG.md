# P1-J 变更日志：BMS gameplay runtime 性能与音频时序治理

> 本文件记录 `P1-J` 相关的验证通过变更，按时间倒序排列。
> 当前进度见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，执行规划见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

---

## 2026-07-16

### 文档健康治理：PLAN 收敛到四个未完 gate，音频约束去除事故流水账

- PLAN 只保留末端 lane runtime proof、转谱 LN、50k 证据驱动治理和 P1-G 人工清单；已修复的普通密度/试听故障及逐日回退过程仍按原日期保留在本文件。
- STATUS 与 PLAN 统一为四个未完 gate；CONSTRAINTS 将超长更新头和事故/旧测试数字归回历史，同时保留原生 BMS、shared store、转谱-mania、track/preview、验证及 `getNextChannel()` O(1)/不得 per-Play 全池扫描的稳定热路径合同。
- 将历史案例中的本机谱库绝对路径改为 `<chartRoot>` 脱敏占位，保留谱面层级与诊断含义。
- 当前功能、缺口和产品 gate 未变；本次仅改文档，未改代码，未运行产品测试或 Release。

## 2026-07-10（Skin V1 topology 运行时 gate 入计划；无代码改动）

- P1-K 修复 converter lane 上界后，P1-J 必须用玩家/auto 两条路径证明 5K K5、7K K7、14K K14/S2 进入同一 shared keysound store 并发声，不能只验证 DTO 数量。
- 此 gate 复用 P1-A `SV1-3` 全 keymode topology fixture，不改变既有 sample pool、判定或 autoplay 语义。

## 2026-06-23（选歌试听音频泄漏进游玩开头 已修：beatmap track 在 BMS 游玩静音；mute 方案取代此前已回退的虚拟轨方案）

> 起因：用户以 `<chartRoot>\Stella\st4\Lyrith -迷宮リリス-\_7INSANE.bms` 为例，反映「songselect 下部分 BMS 谱面有 preview 音频，autoplay 或正常游玩在游戏开头都会播这段 preview 音频」，并判断是可能广泛存在的 bug。归属主 `P1-J`（音频时序治理），即 STATUS 既有开放遗留 ④。

### 根因（日志 + 代码链路确诊）

- BMS 游玩音频**全部**来自键音：`BmsBeatmapConverter` 把每个对象转成带 `KeysoundSample` 的 `BmsBgmEvent`/`BmsHitObject`/`BmsHoldNote`，**从不引用 `Metadata.AudioFile`**。
- 但 `BmsFolderImporter.createBeatmapSet` 会把 `Metadata.AudioFile` 设成**选歌试听源**：`detectFullMusicFile`（≥1MB 且未被任何 `#WAV` 键音引用的音频，取最大）`?? resolvePreviewFile`（`#PREVIEW` 头文件）。Lyrith `_7INSANE.bms` **无 `#PREVIEW` 头**，但文件夹有 `_preview.wav`(3.8MB) 未被键音引用 → 被 `detectFullMusicFile` 当「完整音乐」选中设为 AudioFile。
- 选歌：`MusicController` 播 `working.Track`(=`_preview.wav`) = 用户听到的「preview 音频」（符合预期）。游玩：`MasterGameplayClockContainer` 也以 `working.Track` 为时钟源并从 0 播放 → **开局把试听音频叠在键音上**。`SoloSongSelect.createPlayer` 中 autoplay 走通用 `ReplayPlayer`、正常游玩走 `BmsSoloPlayer`，两者都经 `Player`→MGCC，故四种组合全中招；转谱-mania 因 beatmap 仍 `Ruleset==bms` 同样中招。
- 框架 `Video`（BGA）只解码视频无音轨，已排除 BGA 为声源。

### 修复（mute 方案，最小行为面）

- 新增核心 `Ruleset.PlayBeatmapTrackDuringGameplay`（virtual，默认 `true`）；`BmsRuleset` override `false`（注释说明 BMS 音频全键音驱动、AudioFile 仅试听）。
- `MasterGameplayClockContainer` **仍以 `working.Track` 作时钟源**（时序、pause/resume/seek 语义、`TestSceneBmsPlayerAudioSemantics` 用 ManualClock 驱动游玩时钟全部不变）；仅在 `shouldPlayBeatmapTrack(working)`（读谱面**原生** ruleset `working.BeatmapInfo.Ruleset.CreateInstance().PlayBeatmapTrackDuringGameplay`，解析失败 fail-open=true）为 false 时，于 `addAdjustmentsToTrack` 加 `Volume=0` 调整、`removeAdjustmentsFromTrack` 移除。关键顺序：mute 必须加在 `musicController.ResetTrackAdjustments()`（清空所有 Volume 调整）**之后**，否则被清掉；移除在退出游玩时把共享试听轨还原可闻。
- 门控用谱面原生 ruleset → bms-mode 与转谱-mania（beatmap 仍 bms）都静音，原生 mania/.osu（track 即音乐）不受影响。

### 为何不用此前已回退的虚拟轨方案（STATUS ④ 旧叙事 + 本轮亲历）

- 把 MGCC 源换成 `TrackVirtual`：① **破坏 audio-semantics 测试的确定性驱时**——`TestSceneBmsPlayerAudioSemantics` 用 `advanceReferenceClockBy` 推 `working.Track` 背后的 ManualClock 来驱动游玩时钟，换成实时虚拟轨后这套驱时失效（本轮先试虚拟轨，正是这两条测试超时把我引回 mute）；② 当年实测「在真实耦合时钟下无效」——换源不会停掉原 `working.Track`（它仍被启动着播放），且虚拟轨单测假阳性。**mute 直接静音真正在播的那条 track，不论谁启动它都生效**，是更稳的层级。

### 验证

- `osu.Desktop.slnf` Release **0 警告 0 错误**；BMS 全量 **957/957**（含新增 `TestSceneBmsGameplayTrackMuting` 2 条：bms 谱面 track `AggregateVolume==0`、标准 ruleset `==1` 作正对照；此前因虚拟轨试验失败的 `TestSceneBmsPlayerAudioSemantics` 2 条改 mute 后回绿）。
- **真实 app 已取证 ✅（2026-06-23 用户实机确认）**：autoplay 与正常开始 原受影响谱面开头不再有试听音频、暂无异常（选歌试听仍正常）。

### Follow-up（同日，用户要求）：选歌试听策略收紧 —— 只有 `#PREVIEW` 才有试听且从头播，其它一律无试听

> 审查 song-select preview 链路后用户决定：**只有 `#PREVIEW` 头时才播试听、且从文件头播；其它情况都不播 preview**。

- **`BmsFolderImporter` 改动**：① `Metadata.AudioFile` **只**来自 `resolvePreviewFile`（`#PREVIEW` 头），删掉此前排在前面的 `detectFullMusicFile`（≥1MB 未被键音引用的音频取最大）；② 命中 `#PREVIEW` 时同时设 `Metadata.PreviewTime = 0` → `WorkingBeatmap.PrepareTrackForPreview` 的 `RestartPoint=0`（从文件头播，取代 `PreviewTime=-1` 的 `0.4*Length` 兜底）；③ 删除 `detectFullMusicFile` 方法与仅供它使用的 `allKeysoundFiles` 排除集构建（含跨扩展名变体）——死代码清理。
- **链路原理回顾**（确认两改协同）：选歌试听 = `MusicController` 播 `working.Track` + `PrepareTrackForPreview` 设 RestartPoint/looping（[`SongSelect.ensurePlayingSelected`/`beginLooping`](../../../osu.Game/Screens/Select/SongSelect.cs) → [`WorkingBeatmap.PrepareTrackForPreview`](../../../osu.Game/Beatmaps/WorkingBeatmap.cs)）。`Play(newTrack)` → `RestartAsync()` seek 到 RestartPoint。`MusicController.EnsurePlayingSomething` 已有 OMS 防护（`ensurePlayingSkipCount`）防纯键音静音库无限 NextTrack 空转。
- **后果**：无 `#PREVIEW` 的谱（含 Lyrith `_7INSANE.bms`——有 `_preview.wav` 但无 `#PREVIEW` 头）**从此无选歌试听、AudioFile 空** → 选歌/游玩皆虚拟静音轨；mute 修复（#12）对它们 moot，但对真 `#PREVIEW` 谱仍生效（游玩静音）。两改互补、无冲突。
- **存量谱回写 `BmsPreviewAudioBackfill`**（导入改动只对新导入生效；用户要求存量谱也立即套用新策略）：新 `BmsPreviewAudioBackfill`（仿 `BmsChartFilterStatsBackfill`），挂在 `BmsRuleset.OnSongSelectSetup`（与 chart-filter-stats backfill 并列）、后台 `Task`。候选 = **非空 `AudioFile`** 的 BMS 谱（空的已合规 → 跳过）。逐候选：直读 `.bms`（managed→`gameStorage.GetStorageForDirectory`、external→`NativeStorage`，仿 `computeStatsDirect`）→ 解码取 `#PREVIEW` → `Storage.Exists` 版 `resolveReferencedFile` 在谱文件夹解析（试 alt 音频扩展名）→ 仅当 `(AudioFile,PreviewTime)` 变化才回写（批量 200/事务、**注入** RealmAccess、绝不 new 第二个）。
- **首版每启动跑 + 逐项 Realm 读 → 掉帧（用户报告，同日修）**：初版无完成标记（每进选歌都跑）、且逐候选 `realm.Run(Find+Detach)`，57k 库每进选歌重解码所有 AudioFile 谱 → 持续掉帧（日志 33s 内无 `[BmsPreviewAudio] done`＝仍在磨）。**重做**：① **一次性完成标记** `bms-preview-audio-backfill-v1.marker`（Initialise 查标记有则整段跳过、Task 完成写标记、崩溃中断下次续跑且幂等；改逻辑 bump 文件名版本号）；② **单次 Realm 读**把候选快照成 `record struct Candidate`（id/path/fsPath/isExternal/audioFile/previewTime），解码循环零 Realm（只批量写回碰 Realm）→ 大减与 carousel 更新线程的争用；③ **进度通知** `ProgressNotification`（`INotificationOverlay`，begin/update/complete，首启示「正在更新 BMS 选歌预览（一次性）…X/Y」、完成「修正 N 张」；用户面术语用「选歌预览」非「试听」）+ 每 500 张 Verbose 进度日志。测试 seam `internal RunForTesting`。
- **测试/构建**：BMS 全量 **961/961**（+4 `BmsPreviewAudioBackfillTest`：① 导入设 `AudioFile=preview.ogg`+`PreviewTime=0` 且 backfill no-op；② 无 `#PREVIEW` 即便文件夹有 2MB `song.ogg` 也 AudioFile 空〔验 `detectFullMusicFile` 已删〕；③ backfill 把 stale `fullsong.ogg`/`-1` 纠正回 `preview.ogg`/`0`；④ backfill 清空无 `#PREVIEW` 谱的 AudioFile）——同时覆盖了导入侧改动（此前 `BmsFolderImporter` 无单测）；`osu.Game.Rulesets.Bms` 与 `osu.Desktop.slnf` Release **0/0**。**用户 2026-06-23 实机已运行、暂未见异常**（图示首启进度通知 ~700/3056）：存量库重启后无 `#PREVIEW` 谱不再预览、`#PREVIEW` 谱从头预览、首启有进度通知。

## 2026-06-22（删除「键音通道数（基线）」设置项：自动化已收口、正确默认化、不留用户配置隐患）

用户要求：键音池自动增长（[2026-06-21] #8）落地后，「设置-游戏模式-BMS-键音通道数（基线）」已无调节必要，删除该 UI 选项；并**正确处理默认化**——不要只隐藏 UI 而让旧用户配置继续生效留下隐患。

- **彻底退役该设置，行为回落到硬编码默认**：删除 `BmsRulesetSetting.KeysoundConcurrentChannels` enum 成员 + `SetDefault` + 设置面板 `FormSliderBar<int>`；删除 `DrawableBmsRuleset` 的 `configKeysoundConcurrentChannels` bindable 与对 `Playfield.KeysoundStore.ConcurrentChannels` 的绑定。原生 BMS 播放的 `BmsKeysoundStore` 现保留**构造默认基线 32**（`DEFAULT_CONCURRENT_CHANNELS`）+ 自动增长（封顶 256）；转谱-mania 的 `BmsToManiaKeysoundStoreFactory.Create` 不再读配置、直接 `Math.Max(DEFAULT, 128)` 楼底（BGM 自动层不被偷）。
- **无隐患的删除（关键）**：ruleset 配置按 **enum 成员名**（`RealmRulesetSetting.Key = lookup.ToString()`）持久化，非序号——删除中间成员**不会移位**其它设置；且**所有消费方已移除**，旧库里残留的 `KeysoundConcurrentChannels` 行永不再被读取（惰性、无害），用户旧自定义值不再影响行为。`BmsToManiaKeysoundStoreFactory.Create(IRulesetConfigCache?)` **签名保留**（`DrawableManiaRuleset` 以 `Func<IRulesetConfigCache,Drawable>` 反射绑定，参数现未用但不可改签名）。
- **测试**：删除已失效的 `TestSceneBmsKeysoundChannelConfigBinding`（2 条，验证配置→store 绑定，功能已不存在）；`BmsRulesetConfigurationTest` 去掉该项默认值断言。`BmsKeysoundStore` 自动增长/per-WAV cut 等行为测试不受影响。BMS 全量 **945/945**；`osu.Desktop.slnf` 代码编译 **0 错误**（仅因游戏运行中锁定 osu.Desktop 输出 dll 导致拷贝步骤失败，与编译无关）。归属：主 `P1-J`（键音池治理）。**2026-06-22 用户实机确认「表现均正常」、验收通过。**

## 2026-06-21（原生 BMS 键音保真两改：autoplay=完美游玩 + 键音池自动增长；构建+全测通过，用户实机实测确认 ✅）

> 起因：用户对照 beatoraja 反映 autoplay 的「音乐演奏」不正确（疑似不发声/重复/发错/截断），并提出两条判据——**(A) autoplay 必须等同 100% 完美游玩**（否则真实游玩也有问题）；**(B)「键音通道数」是否好设计、是否该换成智能自动**。本轮按这两条判据审查链路并落地修复。

### 审查结论（用判据 A 反推归属）

- autoplay 与完美游玩**当前不等价**：完美游玩中音符命中消费按键、lane 不发声、每音符一次声；autoplay 把音符设 `AutoPlay=true`（退出输入）→ replay 合成按键直达 `BmsLane` → lane armed 键音**叠**音符 auto-apply 键音 = **每音符双触发**（per-WAV cut 多数掩盖，armed≠音符槽或异步通道状态时露馅重复/发错）。**这是 autoplay/auto-lane 专属差异**。
- 其余两项**非 autoplay 专属、玩家完美游玩同样存在**（同一条 store 播放路径）：① 键音池默认 32 通道饱和即偷断 → 截音；② 键音从游戏更新线程同步触发、无样本级前瞻调度 → 帧抖动/GC 时挤堆（架构性，本轮不动，后置）。

### ✅ 改一：autoplay = 100% 完美游玩（消除 lane 双触发，CONSTRAINTS 新增 #3b）

`BmsLane.playCurrentLaneKeysound()` 在**本 lane 存在自动音符**（`HitObjectContainer.AliveObjects` 中有 `!AcceptsPlayerInput` 的 `DrawableBmsHitObject`）时**抑制** armed 键音，发声交给音符自身 → autoplay 每音符只经自身 `PlaySamples` 出一次声，与完美游玩逐次等价。玩家 lane 不匹配（音符接受输入、命中即消费按键），真·空击键音不受影响。回归 `TestAutoPlayNoteSuppressesRedundantLaneKeysound`（对照 `TestLaneReplayTriggersSharedKeysoundImmediately`：玩家音符 lane 仍发声）。

### ✅ 改二：键音池「固定上限 32 + 偷取」→「饱和自动增长（封顶 256）」（CONSTRAINTS #4/#8/产品#2 重写）

- **真因**：原 `getNextChannel` 在全通道繁忙时轮转**偷断**仍在播的样本；默认 32 远低于真实 BMS 复音（叠层 BGM + 长衰减样本，转谱侧实测峰值 27–36>32），转谱-mania 早 floor 128（#10）而**原生只有 32**——同谱原生更糟，且 settings hint 自承「缺音就调高」= 把工程取舍甩给用户。
- **修复**：饱和（freeChannels 空）时**新增一个通道**（直到 `MAX_CONCURRENT_CHANNELS`=256）而非偷断，仅 256 仍饱和才轮转偷取。保真**单调**（只补不截）、自然有界（≤ 同时发声不同槽数）、O(1) 热路径（空集即判定饱和→增长，不扫描全池，守 #8 旧红线）；增长复用「live 调高 ConcurrentChannels」的运行时建通道路径。`ConcurrentChannels` 降级为「起始/常驻基线」：调高即时扩容、调低 non-destructive（闲置即收、发声延后收），自动增长到基线以上的通道**不随常规播放裁剪**（避免增长/收缩抖动）。settings tooltip 同步为「基线、按需自动增长、通常无需调整」。
- **遗留（本轮不动）**：同步触发挤堆（架构性，需样本级前瞻调度，后置）；解析/转谱少键导致的「固定缺音」属 P1-K，另查。

### 验证

- `osu.Desktop.slnf` Release **0 错 0 警告**；完整 BMS **936/936**。
- 受影响单测：改写 `TestSharedKeysoundStoreSingleSamplePathRotatesBuffers`（不再依赖「1 通道强制偷取」旧语义，改 stop+reclaim+复用同通道测双缓冲轮换）；新增 `TestSharedKeysoundStoreGrowsUnderSaturationInsteadOfStealing`（基线 2 + 3 个不同槽 → 池长到 3、三者全 RequestedPlaying、无偷取）与 `TestAutoPlayNoteSuppressesRedundantLaneKeysound`；既有 `TestSceneBmsKeysoundChannelConfigBinding` / `TestSharedKeysoundStoreShrink*` / `TestSceneBmsAutoplayReplayPlayback`（autoplay 非忽略判定仍全 Perfect）全过。
- **实机听感（对照 beatoraja）用户实测确认 ✅（2026-06-21）**：用户回报「优化及其明显、暂时无异常」。（注：虚拟轨测试对「是否真发声/真不截」是盲区，故此保真结论以用户实机为准。）

---

## 2026-06-11（J6 转谱游玩期帧抖动真因确诊修复 + 220ms gen2 冻结确诊 + prewarm 放开玩家模式；用户实测 ✅）

### ✅ 「越后越抖 / 按键挂钩 / 休息段恢复 / 规律一顿一顿」确诊修复：每键音触发的 sample-drawable 重建 churn → 晋升风暴

用户实测推翻早期两个方向（「通用 mania 高帧 GC 特性」「冷解码主因」——原生 mania 顶级密度全程 1000fps 平稳，本谱密度远不及）后，经 `BmsGameplayStallDiagnostics` 探针三轮取证 + 代码审查锁定真因链：

- **取证**：分配归因显示 store Play 路径仅占总分配 ~10%（185MB/1.7GB），排除「分配体量」单因；决定性异常 = 后段 **gen0:gen1 锁死 1:1、每 ~40KB 触发一次回收（正常预算 MB 级）、~100 次/秒**——「晋升风暴」签名（中寿命对象逢 gen0 必晋升），gen1 暂停叠成 15–30ms 帧尖峰、规律「一顿一顿」；密度升 → 触发率升 → 越后越糟；休息段触发为零 → 立即平滑。
- **代码审查**：store 通道 `PlaySingleSample` 每次换 `Samples` 数组引用 → 每次触发跑 `SkinnableSound.updateSamples()` 全量重建（RemoveAll+Clear+GetPooledSample+Add，实测 ~30KB/次、池 miss 时现场构造全新 drawable）；对照原生 mania 音符 `PausableSkinnableSound` 持久加载、重播仅 `Stop+Play` 零重建——**这正是同密度原生平稳、转谱抖的不对称来源**。次级项（已记录暂不动）：mania 按键反馈 `GameplaySampleTriggerSource.GetMostValidObject` 对转谱 column 0 数千 BGM 实体的每按重扫（缓存被 ~30/s 自动判定的 BGM 持续失效）。
- **修复 = 通道同样本快路径**：`BmsKeysoundChannel` 记住 `currentSingleSample`，同槽重触发（per-WAV cut 钉同通道 + 转谱器同槽 memo 同实例 → 游玩主路径）跳过 `Samples` 赋值、直接重启（cut 语义不变）；多样本入口 `PlaySampleArray` 显式失效缓存防误跳过。
- **实测 ✅（2026-06-11 用户同谱回归）**：同段密集区 maxFrame 15–30ms → **5–10ms**，gen1 回收单次降至亚毫秒（gcPause ~70–120ms/2s 摊在 120+ 次上、不可感知），用户回报「基本全程稳定高帧低延迟」。

### ✅ 偶发 ~220ms 冻结确诊：开局段阻塞式 gen2 全量 GC（键音游玩中冷解码所致）→ prewarm 放开玩家模式

- 探针抓到 3 次 `STALL+GEN2`（t=16.1/21.1/27.9s，212–229ms，frame 与 gen2+=1 同帧），全部集中开局前 30s；结尾 gen2 为后台并发仅 6ms。玩家模式此前**无预热** → 全场 362 个 WAV 全部游玩中冷解码（`coldKeysoundLoads=362`，集中前 ~55s），瞬时大缓冲/晋升突发把 gen2 预算顶爆。
- **修复**：keysound prewarm 去掉 `ModAutoplay` 门控（BMS 原生 `DrawableBmsRuleset.LoadComplete` 与转谱-mania `prewarmConvertedKeysounds` 两侧对等），数百解码全部移到加载边界——对齐 LR2/beatoraja「进谱前全量预载」；代价 = 加载期变长（预期取舍）。CONSTRAINTS #7 已重写。
- 注：用户自述其机器原生 lazer 也偶发莫名卡顿——同为 gen2 冻结机制；本修复拔除转谱侧最大的可控触发器，无法保证根除所有 gen2（剩余属 lazer 通用面）。

### 诊断探针（经用户确认留作长期 seam）

`BmsGameplayStallDiagnostics` 挂在 `BmsKeysoundStore` 下（真实游玩才激活、测试场景静默）：逐帧测 update 线程帧时长，仅在 stall（≥40ms）/gen2/2s 心跳时写 `performance.log`（gen 计数、alloc、`GC.GetTotalPauseDuration` 增量、promoted/pinned、store plays、冷解码数）；store 加 `PlayPathAllocatedBytes`/`TotalKeysoundPlays`/`ColdKeysoundFirstPlayCount` 归因计数器。三轮取证依次排除「音频对象数」「冷解码主因」「分配体量单因」，最终钉死晋升风暴 + gen2 冻结两条根因。

- **验证**：`osu.Desktop.slnf` Release 0 错；焦点回归（路由 2 + shared timing + drawable ruleset + lifecycle + player audio semantics）**78/78**；完整 BMS **871/871**（快路径轮）→ prewarm 放开后再跑 **871/871** + mania 转谱/autoplay **22/22**；**用户三轮实测全确认 ✅**：快路径轮「基本全程稳定」（密集区 maxFrame 15–30ms→5–10ms）；prewarm 轮同谱回归 **游玩中 stalls=0、阻塞 gen2=0**（上轮 4 stalls 含 3 次 ~220ms 冻结 → 0；唯一 gen2 在结尾过场、后台并发仅 8ms；全程 maxFrame 2–12ms @ ~1000fps），用户判定「合格」。注：探针 `coldKeysounds` 计的是「每槽首次经 store 播放」非解码——解码已被预热移到加载屏，故计数仍 362 但冻结消失。

---

## 2026-06-10（J6 性能优化：转谱 tap KEY note 改池化 + 补回 BGM/scratch 预热）

### ✅ 转谱 tap KEY note 从非池化改为池化（消除 CONSTRAINTS #10「当前态」的 🔴 非池化 perf）

承接本轮「转谱-mania 游玩期性能审查」。审查结论：转谱-mania 的 drawable 策略与原生 BMS 基本持平（均非池化），唯一相对原生 mania 更重之处是**每个转谱 tap KEY note 一个常驻非池化 drawable**（`DrawableBmsConvertedKeyNote`），在 `loadObjects` 即全部构造并常驻整局 → 加载期构造 + 内存 + GC 大堆代价（疑 once-per-run 致命卡顿来源）。这正是 CONSTRAINTS #10 标注的「perf 回归时正解：让池化 `DrawableNote` 走 shared store」。

- **方案（Option E，无反射注册池、无核心 BMS 概念泄漏）**：在 mania 定义两个**自有接口** `IManiaKeysoundStore`（`Play(ISampleInfo, double, int?)`）与 `IHasManiaKeysound`（`KeysoundSample`/`KeysoundCutGroup`）。`BmsKeysoundStore` 显式实现前者（桥接到既有 cut/no-cut 重载）；`BmsConvertedKeyNoteHitObject`（仍 `: Note`）显式实现后者。`DrawableManiaRuleset.CreateChildDependencies` 在原有 `Cache(BmsKeysoundStore)` 之外**追加 `CacheAs<IManiaKeysoundStore>`**。`DrawableNote.PlaySamples` 重写：`keysoundStore != null && HitObject is IHasManiaKeysound ks && ks.KeysoundSample != null` 时经接口 `Play(...)`，否则 `base.PlaySamples()`（原生 mania / 无 store 上下文不变）。
- **关键**：转谱 drawable 工厂（`BmsToManiaDrawableRepresentationFactory`）**不再认领 KEY note** → `CreateDrawableRepresentation` 返回 null → playfield 经框架 `Playfield.prepareDrawableHitObjectPool` 的**基类型池回退**（`typeof(Note).IsInstanceOfType(convertedKeyNote)`）命中 mania `Note` 池 → 发**池化 `DrawableNote`**。删除 `DrawableBmsConvertedKeyNote`。
- **音频语义零改动**：`PlaySamples` 在命中（`ArmedState.Hit`）时被核心调用，与改造前同一时机、同一 store、同一 cutGroup；per-WAV cut 跨 BGM↔KEY+KEY↔KEY 不变。守 (c)（未新长 per-note/per-lane sample player）；tap 无嵌套对象，不触 (a) LN 嵌套头池化坑（LN 仍后置）。
- **同轮补回预热缺口**：`prewarmConvertedKeysounds` 现额外按 `IHasManiaKeysound.KeysoundSample` 预热（BGM/scratch/KEY 全覆盖）——修掉 2026-06-08 bgm1 修复置空 BGM/scratch `Samples` 后、它们在 autoplay 下不再被预热的首播冷解码抖动（BGM/scratch `Samples` 空、仅此一途）；同步修正 `prewarmConvertedKeysounds` 已失真的注释。
- **验证**：`osu.Desktop.slnf` Release **0 错**；`TestSceneBmsToManiaKeyNoteStoreRouting`（#10(b) 要求的 player-level harness，断言每文件 store Play 次数 key_a=2/key_b=1/bgm=2/scratch=1 + floor≥128）**2/2**；`BmsToManiaBeatmapConverterTest`+`TestSceneManiaModAutoplay` **22/22**；完整 `osu.Game.Rulesets.Bms.Tests` **871/871**；完整 mania 套件 **778 通过**，4 个 `TestSceneAutoGeneration` HoldNote 失败经 `git stash -u` 回基线对照确认为**既有失败**（更早 `ManiaAutoGenerator` 改动遗留、与本轮无关），本轮**0 新失败**。
- **明确后置**（避免拿已实测通过的音频修复赌投机微优化）：① BGM/scratch sample-only 对象仍非池化（每帧 alive 隐形 drawable + scroll 更新），「调度器化」消除每帧开销属更大改动、须先 profile alive 占比；② 转谱 store 128 通道 floor 的每帧扫描/常驻 sound drawable——下调会回归已实测的长 BGM 偷断修复，治本须长样本分池（碰 #8 红线）；③ 稳态高密段渲染/更新预算（与同密度原生 mania 持平那部分），须先 profile 再定主攻方向。

---

## 2026-06-08（根因确诊 + 修复 + 用户实测确认）

### ✅ 解决：转谱-mania「按 key1 触发 bgm1 / 胡乱按键长音重叠 / 暂停不停」——真凶 = mania 按键音效反馈 `GameplaySampleTriggerSource`

承接 2026-06-07b 移交的最高优先级 bug。**用户实测最终钉死**：转谱-mania 下只按 key1（最左列）就反复触发 `bgm1.ogg`，多按则先后重叠、暂停不停。经谱面三重重解析（`bgm1`=`#WAVYX` 全谱仅 `#00101:YX` 一次、BGM-only、无任何 KEY 通道引用、无同文件跨槽冲突）+ 解码/转谱链通读，确认**解析与转谱完全正确、无键音/BGM 粘连**。先后在 store、`DrawableHitObject.PlaySamples`、KEY/BGM drawable 多处埋点均**抓不到** key1 播 bgm1；最终在最底层发声点 `PoolableSkinnableSample.Play()` 加调用栈探针，定位真凶。

- **根因（调用栈铁证）**：mania `Column.OnPressed`（Column.cs:192）**每次按键都调 `GameplaySampleTriggerSource.Play()`**（按键音效反馈），它播放**本列 `HitObjectContainer` 中下一个对象的 `Samples`**，用自己一池**非循环、不受 store 暂停管**的 `PausableSkinnableSound`。转谱时 **BGM/scratch sample-only 对象被钉在可玩列**（BGM→column 0）、且其 `Samples` 里装着键音（bgm1）→ 按 key1 → 反馈取到 column 0 的 BGM 对象 → 播 bgm1；反复按则反馈池轮转重叠；非循环且绕开 store → 暂停不停。**一个根因解释全部现象**，也解释了为何 store/一次性埋点全抓不到（它既不经 store 也不经 hit-object 的 `PlaySamples`）。
- **修复（已落地、已验证）**：`BmsToManiaBeatmapConverter` 把 `BmsConvertedBgmSampleHitObject` 与 `BmsConvertedScratchSampleHitObject` 的 `Samples` **置空**。这些 sample-only 对象本就经 shared store 用 `KeysoundSample` 自动发声，`Samples` 对其实际播放是多余的，只会（错误地）被按键反馈取用。置空后：自动 BGM 照常经 store 播放/暂停，按键反馈再也取不到 bgm1。改动定位在转谱器、不碰 osu 核心。
- **验证**：用户真实 app 实测「按 key1 不再触发 bgm1」**并在其他原本同问题的谱面一并复现修复成立**；日志佐证（所有 `[KEYHIT]` 含 col=0 播的都是鼓/water、无 bgm1，bgm1 仅在自动层 `[BGMAUTO]` 出现）。回归守卫：`BmsToManiaBeatmapConverterTest` 新增「BGM/scratch 的 `Samples` 必须为空、键音在 `KeysoundSample`」断言。`BmsToManiaBeatmapConverterTest` **19/19**、BMS **871/871**、`osu.Desktop.slnf` Release **0 错**。
- **作废之前的错误方向**（本会话先后基于错误诊断试过、均已回退/否定，留作认知演变）：① store「脱挂留响（orphan-on-reuse）」假设——曾在 `PlaySingleSample` 加 `Stop()`，**已回退**（非本 bug；orphan 是否真实存在属独立待证遗留）；② 转谱 LN head 经 mania 一次性致重叠假设——**否定**（日志 `[ONESHOT]`=0，本谱按键期间无 LN head 发声）；③「长 BGM 当一次性样本无法 resume」属**另一条独立问题**（暂停停掉后恢复截断，native+转谱通用），与本 key-trigger bug 无关、仍后置。
- **诊断方法教训**：当某发声路径既不经已知 store、也不经 hit-object `PlaySamples` 时，**直接在最底层 `PoolableSkinnableSample.Play()` 加调用栈探针**是定位"隐藏发声路径"最快的手段；按来源标签（store / 一次性 / 反馈）分层埋点 + 哨兵静音隔离实验，是把"用户确信 vs 代码看似不可能"对撞用数据终结的正确流程。
- **遗留（未动、非本 bug）**：长 BGM resume 截断（需把长 BGM 改走时钟驱动 Track 才能保位暂停/续播）；per-WAV cut 的 orphan-on-reuse（traced，未验证，后置）；BGM/scratch 的 autoplay prewarm（`prewarmConvertedKeysounds` 遍历 `Samples`，置空后 BGM/scratch 不再被预热——仅 `ModAutoplay` 路径受影响、store 仍按需加载，影响小，后置）。

---

## 2026-06-07b（同日晚些，用户 mania 实测）

### 梳理（当时未解、移交新对话；✅ **已于 2026-06-08 确诊+修复**，真凶 = `GameplaySampleTriggerSource`，见上方 2026-06-08 条）：`bgm1.ogg` 长 BGM 事件被胡乱按键错误触发 + 重叠 + 不暂停（转谱-mania & BMS 原生）

用户在 `macchitodoncho_SP_HYPER.bms` 剖析 + 转谱-mania/BMS 原生双侧实测，定位出一个**清晰但未解**的 BGM 事件触发/暂停 bug。**因上下文过长，完整梳理移交新对话**——权威 RESUME-HERE 记忆：`reference_bms_bgm1_pause_keytrigger_bug`。

- **对象**：`bgm1.ogg`（**44s 长音频**）= `#WAVYX`，仅 `#00101:YX` 一次（channel 01=BGM 自动层）→ **规范上 100% 是 BGM 事件、不是键音、不该被按键触发**。
- **已确认事实**：① 转谱-mania 不操作：bgm1 正常播 + 暂停同步停 ✅（BGM 事件暂停在此路径正确）；② 转谱-mania **胡乱按键 → 一堆 bgm1 长音频重叠 + 暂停不停 ✗**（BGM 被错误按键触发！）；③ BMS 原生不操作：bgm1 **非确定性播放 ✗**；④ BMS 原生 bgm1 被触发后暂停 **仍继续播 ✗**。
- **核心未解问题**：转谱-mania 胡乱按键到底经哪条路径触发了 bgm1？——按规范根本不该（BGM→`BmsConvertedBgmSampleHitObject` sample-only/auto/按键不可触发；mania 无空击键音机制）。候选：解析误塞 lane/键音时间线 / 转谱误转可触发对象 / store 跨槽通道污染（tap-note 路由进同一 store 的 confound）/ BGM 对象被重判。
- **暂停未停候选**：触发实例走了一次性 `PlaySamples` 回退（不随暂停停）/ 长样本被通道偷取后脱离 `channels` 跟踪 / BMS 原生 32 通道（非转谱 floor 128）饱和偷取长 bgm1。
- **用户洞察（采纳）**：键音短、即放即完、**可能根本无需暂停**；真正需暂停的是 BGM（长）；混乱根因 = 长 BGM 误入"按键触发的键音"路径。涉及 scratch 的 mania 仅自动播放特殊处理。
- **三大排查方向**（含代码入口）+ **建议新对话第一步**（store/BGM-object 加运行时日志实测胡乱按键时谁调了 `Play(bgm1)`；先回退 tap-note 路由去 confound；音频发声/静音必须真实 app 实测——虚拟轨单测是盲区）：详见 RESUME-HERE 记忆。
- **当前代码状态**：tap-note→store 路由"转正"仍 IN（疑普通谱性能 confound、建议先回退）；J6 store/prewarm/floor128 IN；Track 静音修复已回退；本 bgm1 bug + Track 预览泄漏 + 转谱键音 per-WAV cut 重复 均未修。**"结算无法跳转"已澄清=卡顿本身**（`PlaybackRateValid=false` 只取消离线提交、不阻断结算）。

### 回退：上条音轨静音修复（`StopUsingBeatmapClock`）实测无效 + 暴露普通谱转谱-mania 性能回归

用户在 mania 转谱实测并导出日志（`1780836423`），推翻上条"已修复"，并报新性能问题。

- **音轨静音修复无效、已回退**：日志中游玩期仍报 `FrameStabilityContainer:169` "BASS invalid time"（`referenceClock` 独立于游戏帧跳变 >500ms）→ **耦合时钟的真实 app 里时钟源仍是真实 BASS 音轨，`StopUsingBeatmapClock()` 没把它换成虚拟轨**。上条 874/874 单测**全是假阳性**——测试 `Beatmap.Value.Track` 本就是虚拟轨（`TestWorkingBeatmap`），对"音轨是否真静音"是盲区。**已回退**：删 `BmsSoloPlayer.silenceBeatmapTrack` + 其 2 处调用、删 `BmsConvertedSoloPlayer` + `TestSceneBmsConvertedManiaTrackSilence`、`SoloSongSelect.tryCreateRulesetSpecificSoloPlayer` 复原（仅 `ruleset=bms` → `BmsSoloPlayer`）、删 `TestSceneBmsSoloPlayerPreStart.TestSongSelectPreviewTrackIsSilencedOnGameplayStart`。pre-start 套件回到 **24/24**。**Track 泄漏（preview/暂停音频）仍未修**——须先真实 app 取证（为何 StopUsingBeatmapClock 对耦合时钟无效；暂停残留音源是 Track 还是键音 BGM）再重做。**教训**：音频静音类改动在虚拟轨测试环境是盲区，必须真实 app 实测。
- **结算无法跳转的真因 = 卡顿本身**：日志的 `Playback discrepancy` + "Score submission cancelled" **只取消在线提交（OMS 离线本就禁用）、不阻断结算**；无法跳转结算系密集卡顿致谱面跑不到结尾/被迫退出。
- **⚠️ 新性能回归（普通谱、非 dense）**：用户明确"刚刚是普通谱面也掉帧+涨延迟"。"涨延迟"=累积特征，**首要嫌疑 = tap-note→store 路由转正引入的非池化 `DrawableBmsConvertedKeyNote` 累积**（mania 精确类型池化、`Note` 子类必非池化）。按用户"一件一件处理"：本轮先回退音轨静音（移除 confound + 不 ship 坏修复）；下一步待用户重测 → 若 perf 仍回归则回退 tap-note 路由（已知 perf 风险、当时已标红）。

## 2026-06-07

### 代码 / 测试：修复**选歌预览/主音轨泄漏进游玩**（preview/暂停音频 bug，BMS 原生 + 转谱-mania 双路径，运行时确认并修复）

承接用户报"有预览音频的谱面进入游玩时播放预览音频 + 暂停不停的音频"，且警示"可能造成回归与牵连"。彻查 → 运行时确认 → 双路径修复。

- **根因**：`BmsFolderImporter`（行407-413）检测整曲音乐（`detectFullMusicFile`）/预览文件（`resolvePreviewFile`）→ 设 `Metadata.AudioFile` → 这类谱面 `working.Track` 是**真实音频**；osu 游玩基建（`MasterGameplayClockContainer` 包 `working.Track`）把 beatmap Track 当游玩音乐，但 **BMS/转谱的音频是键音**——两条游玩路径都没让 Track 静音（BMS ruleset 全局无 `Track.Stop`/`StopUsingBeatmapClock`/mute）。
- **运行时确认**：新增 `TestSceneBmsSoloPlayerPreStart.TestSongSelectPreviewTrackIsSilencedOnGameplayStart`（模拟选歌预览在播→进游玩→`Beatmap.Value.Track.IsRunning`）——修复前断言 `==false` 失败（Track 仍在播）。
- **关键机制坑**：游玩时钟**每帧保持其源 Track 运行**（即便"解耦"），裸 `Track.Stop()` 立即被覆盖 → 必须先 `MasterGameplayClockContainer.StopUsingBeatmapClock()` 把时钟源换成虚拟轨（仍供 timing），再停孤立的真实 Track。
- **两路径 + 两修复**：
  - **BMS 原生**（`BmsSoloPlayer`，ruleset=bms）：`StartGameplay` `Reset` **解耦**时钟 → Track 孤立在 preview 位置（注释早写了"may still be playing from song select"却没停）。修复：新增 `silenceBeatmapTrack()`（`StopUsingBeatmapClock()` + `Track.Stop()`），在 `StartGameplay`（pre-start 静音）与 `attemptStartGameplay`（实际游玩开始）各调一次。
  - **转谱-mania**（标准 `SoloPlayer`，ruleset=mania）：游玩时钟**驱动** Track（从头播当音乐）叠加转谱键音——**与 J6/键音 per-WAV-cut "重复音"调查直接纠缠**（检测到 AudioFile 的转谱谱面"整曲 Track + 键音"双层，易被误判为键音 bug）。修复：新增 `BmsConvertedSoloPlayer : SoloPlayer`（`StartGameplay` → base + `silenceBeatmapTrack` 同机制）；`SoloSongSelect.tryCreateRulesetSpecificSoloPlayer` 增分支——以**非-bms** ruleset 游玩**bms-native beatmap**（`Beatmap.Value.BeatmapInfo.Ruleset.ShortName=="bms"`）时返回 `BmsConvertedSoloPlayer`。
- **牵连消解**：这条 Track 泄漏是**独立于键音 per-WAV-cut 的另一重叠来源**（song + 键音双层），修掉后键音工作的"重复音"验证不再被它污染。**非本会话键音改动引入**（既有问题）。
- **验证**：完整 BMS **874/874**（新增 2 个 track-silence regression 测试：`TestSongSelectPreviewTrackIsSilencedOnGameplayStart` + `TestSceneBmsConvertedManiaTrackSilence`；pre-start 全套 **25/25** 无回归，证明 `StopUsingBeatmapClock` 在 pre-start 期不破坏时钟逻辑）；mania `BmsToManiaBeatmapConverterTest`+`TestSceneManiaModAutoplay` **23/23**；`osu.Desktop.slnf` Release **0 错**（`osu!.dll` 产出）。`SoloSongSelect`（osu.Game）改动为 OMS-specific、对非-BMS 谱零影响（gated 在 bms-native 判断）。

### 代码 / 测试：**tap-note→store 路由转正为生产默认**（缺陷③/#1 转谱键音重复的 tap-note 部分已修，待用户真实谱实测；LN 仍后置）

承接同日 tap-note 路由 harness 实测「无静音」结论，按用户决定把路由转正为生产默认（去掉实验 gate），让转谱可游玩 KEY 音符的键音与 BGM/scratch 一样经单一 `BmsKeysoundStore` 获得跨通道 per-WAV cut。

- **改动**：① 删 `BmsToManiaBeatmapConverter.RouteKeyNotesThroughStoreForTesting` flag——`case BmsHitObject` **无条件**发 `BmsConvertedKeyNoteHitObject`（带 `KeysoundSample`/`KeysoundId`，仍保留 `Samples` 供 prewarm + store 缺席回退）；② `BmsToManiaKeysoundStoreFactory.ShouldHost` 增 `BmsConvertedKeyNoteHitObject` → store 现对**每张转谱**都 host（不再只在有 BGM/scratch 时）；③ `BmsConvertedKeyNoteHitObject`/`DrawableBmsConvertedKeyNote`/工厂注释由 EXPERIMENTAL 改生产口径。
- **效果**：per-WAV cut 现**跨 BGM↔KEY 与 KEY↔KEY（tap）统一生效**——同一 WAV 槽在 BGM 层与 KEY 音符之间、或同槽连续 KEY 之间，干净掐断而非叠加重复一次性副本（对齐 BMS 原生单 store）。转谱键音重复的 **tap-note 部分已修**。
- **🔴 性能警示（显著、已记入对象注释）**：mania 按**精确类型**池化（`Column.RegisterPool<Note,DrawableNote>`），`Note` 子类必走 `CreateDrawableRepresentation` **非池化**路径（[`Playfield.cs:437`] base-type fallback 仅服务池化路径、对返回非空的 BMS 类型不触发）。故现在**每个转谱 tap 音符都是非池化 drawable**，叠加在既有非池化 BGM/scratch 之上 → 对 dense 谱（P1-J 未解的 **D** 卡顿，瓶颈疑似 drawable 数量）可能加压。**pooling-preserving 替代**（让普通池化 `DrawableNote` 从 mania core 走 shared store、保留池化）= 若 perf 回归的后置正解；不碰第 1 条（用 shared store、非新长 per-note player）。
- **LN 未动**：LN head 仍走 mania 一次性 `NodeSamples[0]`（per-WAV cut 仍不跨 LN 头）——LN 走 store 须**池化嵌套头**避开 2026-06-06 必崩红线（约束 (a)），后置。
- **测试**：phase-1 的 baseline split 测试 `TestSceneBmsToManiaKeysoundPlayback`（断言"KEY 音符不进 store"）已随转正过时，**合并删除**，其独有覆盖（floor 128 / sample-only 计数）并入 `TestSceneBmsToManiaKeyNoteStoreRouting`（含 sentinel+settle 健壮模式，断言 `key_a=2`/`key_b=1`/`bgm=2`/`scratch=1` 全部经 store + combo≥4）。**验证**：mania `BmsToManiaBeatmapConverterTest`+`TestSceneManiaModAutoplay`+`TestSceneDrawableManiaHitObject` **26/26**（子类 `: Note` 与 `OfType<Note>()`/计数/star 全兼容）、**完整 BMS 871/871**、`osu.Desktop.slnf` Release **0 错**（`osu!.dll` 产出）。
- **⚠️ 待用户实测**：headless 测不到 per-WAV-cut **复用**路径（长样本仍播时同槽重触发）+ dense perf；须用户在 `macchitodoncho` 等真实谱验：(1) 键音重复是否消除、(2) per-WAV-cut 复用是否产生意外静音、(3) dense 谱性能是否可接受。若 (2)/(3) 出问题 → git 回退本次转正或转 pooling-preserving 方案。

### 代码 / 测试：用 harness 翻开实验性 **tap-note→store 路由（gated）**，定性 2026-06-06「tap-note 走 store 静音(0 次播放)」——**实测路由本身无静音**，旧静音系 harness 假象/LN 旁路所致

承接同日 harness 落地，按 CONSTRAINTS #10「先测后改」翻开实验性 tap-note→store 路由并实测。**只做 tap-note**（LN 的非池化嵌套头崩溃属约束 (a) 禁区，本轮不碰）。

- **新增（gated，默认 off → 生产零影响）**：`BmsConvertedKeyNoteHitObject : Note`（BMS 程序集，带 `KeysoundSample`/`KeysoundId`）+ `DrawableBmsConvertedKeyNote : DrawableNote`（重写 `PlaySamples` 走 `store.Play(sample, balance, cutGroup)`，store 缺席回退 base）+ 工厂 `CanCreate/Create` 注册 + `BmsToManiaBeatmapConverter.RouteKeyNotesThroughStoreForTesting`（internal static seam）。flag 关闭时 KEY 音符仍是普通 mania `Note`（`case ... when flag` 落到原分支），转换器/工厂行为零变化。
- **`TestSceneBmsToManiaKeyNoteStoreRouting`**（实验 fixture）+ 控制组：同一谱（长 BGM(AA)／同槽连续 KEY(BB×2)／BGM↔KEY 跨路径共享槽(AA)／scratch(CC)／末尾哨兵 EE）跑 mania autoplay，按 per-file 统计 store `Play` 次数。
- **关键诊断路径（harness 保真度三连坑，已逐一排除）**：① 单次大跳进 clock → bulk catch-up 可能用 `force=true` 状态判定**跳过 `PlaySamples`**（`DrawableHitObject.cs:493` `if (!force && newState==Hit) PlaySamples()`）；② 连续小步推进但不等 frame-stable → frame-stable 滞后参考时钟 ~2000ms、永远到不了后段音符；③ **manual-clock autoplay 必丢时间上最后一个对象**（控制组普通池化 `Note` 同样 combo=3 → 与路由无关的 harness 假象）。修法：逐段「推进 + AddUntilStep 等 frame-stable 追上」settle + **末尾哨兵音符**吸收"丢最后一个"。
- **结论（实测）**：修正保真度后，tap-note→store 路由**完全正常、无静音**——`key_a.wav=2`（同槽 BB 重触发两次都进 store）、`bgm.wav=2`（BGM auto + 跨路径 KEY AA 都进 store）、`key_b.wav=1`、`scratch.wav=1`，每个被命中的 tap 音符都触发了 `store.Play`；控制组证明非池化 `BmsConvertedKeyNoteHitObject` drawable **不破坏命中/出声**，且子类**安全穿过 BMS→mania 转换**（未被降级为普通 `Note`）。**2026-06-06 的「0 次播放」未能复现** → 不是 tap-note 路由机制的根本缺陷；旧静音最可能是 (i) 当时生产侧的观测假象（与本轮 harness「丢最后一个对象」同型），或 (ii) 同批 LN 子类改动的连带（LN drawable 崩溃/破坏 playfield），或 (iii) headless 测不到的 per-WAV-cut **复用**路径（长样本仍在播时同槽重触发 → `PerWavCutReuse`）。
- **headless 局限（明确）**：本 harness 所有 `Play` 决策均为 `IdleChannel`——headless 无音频设备、测试样本零长，样本在下一个音符前已结束，故**未走到 per-WAV-cut 复用分支**。store 的 per-WAV cut 复用逻辑由既有单测 `TestSharedKeysoundStoreCutsSameSlotRetrigger` 单独覆盖（通过）；其在 live player + 长样本下的交互需真实音频/真实谱验证。
- **验证**：BMS keysound/store/player/converter 子集 + 两个新 fixture **112/112**；mania 侧 `BmsToManiaBeatmapConverterTest` + `TestSceneManiaModAutoplay` **23/23**（flag 默认 off、默认路径不变）。flag 泄漏已防御（baseline fixture `[SetUp]` 同步复位 + 实验 fixture `[TearDown]` 复位）。
- **下一步（待用户定）**：(A) 把 tap-note→store 路由转正（默认或设置项 + 扩 `ShouldHost` 含 KEY 音符），由用户在 `macchitodoncho` 实测 per-WAV-cut 复用路径；(B) 停在特征化、保留 gated 脚手架 + 测试作根基；(C) 啃 LN 路径（须用池化嵌套头，避开崩溃）。

### 代码 / 测试：转谱音频链路全面审查 + 搭建 **per-WAV cut 缺失（#1 遗留）的 player-level 集成测试 + 运行时日志 harness**（满足 CONSTRAINTS #10 的"再尝试前置(b)"）

承接用户报告"部分其他谱面游玩时 BGM 仍先后重叠"，先做**全链路审查**裁决两个假设，再按 CONSTRAINTS #10 要求搭建"先测后改"所需的 harness（不改 runtime 行为）。

- **审查裁决**：
  - **假设「BGM 被按键触发」= 否**。`DrawableBmsConvertedBgmSampleHitObject.CheckForResult` 在 `userTriggered` 时直接早退，只在 `timeOffset≥0` 的 auto 路径 `playKeysound()` 一次后 `ApplyMinResult()` 移除；且 BGM 对象 `IgnoreJudgement`+空窗+无嵌套 → autoplay/note-lock 双双跳过。按键无法让 BGM 出声。
  - **真因 = per-WAV cut 在「音符/LN 头」路径整体缺失 + store(BGM/scratch)↔mania 一次性(note) 跨路径无法互掐**（= 已知缺陷③/#1）。用户感知的"BGM 重叠"实为**同一 WAV 槽既作 BGM 背景层、又作 KEY 音符**时两路径各播一份（原生单 store 会互掐、转谱不会）。
  - **排除「BGM 自重叠」**：`BmsBgmEvent.KeysoundId` 恒 = `objectEvent.ObjectId`（[`BmsBeatmapConverter:140`]），BGM 永远带 cutGroup → store 内 BGM 同槽必互掐；无 within-object double（BGM 仅 store 路径、LN 头只播 `NodeSamples[0]`、`DrawableHoldNote.PlaySamples` 空操作）。
- **harness（本次落地）**：
  - **`BmsKeysoundStore` 运行时播放日志**（`internal`、opt-in、**生产零开销**）：`EnablePlaybackLogForTesting()` 后每次 `Play` 记一条 `KeysoundPlaybackRecord(time, cutGroup, decision, channelIndex, filename)`；`decision ∈ {IdleChannel, PerWavCutReuse, RotationSteal}` 由选择方法（`getNextChannel`/`getChannelForCutGroup`）写一个枚举字段提供；日志关闭时 `recordPlayback` 仅一次 null 检查、无分配（守住 dense 热路径 #8）。时间戳用 `GameplayClockContainer.CurrentTime`（gameplay 量级，便于与事件时刻对齐；大跳进下略超子步事件时刻，细步推进更准）。
  - **`TestSceneBmsToManiaKeysoundPlayback`**（Bms.Tests，mania-autoplay `PlayerTestScene` + manual reference clock）：用一段同时含「长 BGM(AA)／同槽连续 KEY(BB)／BGM↔KEY 跨路径共享槽(AA)／scratch(CC)」的 BMS 谱转 mania 跑 autoplay，t=0 抓 host 的 store 开日志、推进 clock、dump+断言。**实测日志**：store 仅 2 条 = `scratch.wav`(slot 444) + `bgm.wav`(slot 370)，均 `IdleChannel`；**KEY 音符(key1/key2)完全缺席**；slot 370 即使在 t=5000 又被当 KEY 音符敲一次，store 仍**只 1 条** → per-WAV cut 不跨两路径，与审查结论一致。
- **意义**：CONSTRAINTS #10 要求"再尝试 note/LN 走 store 前先有 player-level 集成测试 + 运行时日志定性静音真因"——本 harness 即该前置；下一步可在此基座上（先 tap-note、避开 LN 非池化嵌套头红线）翻开实验性 note→store 路由，用日志定性 2026-06-06「`punai` 切片静音(0 次播放)」真因。缺陷③仍未修（维持 J6 v1：note/LN 走 mania 一次性）。
- **验证**：`osu.Game.Rulesets.Bms.Tests` Debug 编译 0 错；store/keysound/player 回归子集 **109/109**（含 `BmsDrawableRulesetTest` per-WAV cut 单测、`TestSceneBmsKeysoundPlaybackLifecycle`、`TestSceneBmsPlayerAudioSemantics`、新 `TestSceneBmsToManiaKeysoundPlayback`）——store 改动纯增量、日志关闭时 no-op，无回归。

### 代码 / 测试：autoplay/游玩 BGM 人声丢失**真因确诊 + 修复（用户实测确认 ✅）**——长 BGM 被 store 32 通道饱和偷取掐断，转谱 store 通道 floored 到 128

承接下一条（prewarm/通道接配置落地后用户实测"仍没回来"），用户给出决定性线索：丢的是单个文件 `bgm1.ogg`、**开头正常播、文件第 16s 的人声才消失**（播一半被掐，非不播）。据此**推翻"惰性 LoadSamples"方向**并静态确诊。

- **静态铁证（解析 `macchitodoncho_SP_HYPER.bms`，Shift-JIS）**：`bgm1` = #WAV 槽 `YX`（定义 `bgm1.wav`、去扩展名 fallback 到 712KB 的 `bgm1.ogg`），在 channel 01 **整首只触发 1 次**（measure 1）；`bgm2/3/4` 同理各 1 次（measure 41/79/116）——是 4 段长背景音轨。该谱 **channel 01（BGM 自动层）共 4032 个事件**（大量鼓/钢琴/贝斯短切片也挂在 BGM 层）。
- **排除法 → 唯一真因**：单次触发（排除 per-WAV cut/重触发）+ 开头能播（排除加载/触发）+ autoplay 不 seek（排除 `StopAllPlayback`）+ `IsPlaying`（不被 idle 回收/shrink）→ 唯一剩 **`BmsKeysoundStore` 32 通道饱和时的轮转偷取**：`bgm1` 占 1 通道达 16s+，期间短切片密集触发，并发峰值实测 27–36 > 32 → 饱和 → `getNextChannel` 轮转偷取掐断占通道最久的 `bgm1` → 16s 人声及之后静音。
- **为何 #2/#3 没解**：#2 prewarm 治"首次加载延迟"（无关，bgm1 开头能播）；#3 让 mania 复用 BMS `KeysoundConcurrentChannels`，但用户值若是默认 32 则"复用一个本就不够的值 = 没改"。
- **修复**：`BmsToManiaKeysoundStoreFactory.Create` 改为 `store.ConcurrentChannels = Math.Max(configured, 128)`（常量 `converted_bgm_layer_min_channels = 128`）。论证：mania 转谱 store 专播 **BGM 自动层 + scratch**（必须完整的背景音乐、非玩家 polyphony 预算），不该被 32（为玩家键音并发设的值）掐断长 BGM；128 远超峰值 36 → 长 BGM 永不被偷，用户调更高 config（≤256）仍生效。**性质 = 治标的务实近似（提高 polyphony 到远超峰值）**；治本（长 BGM 不进可偷池 / BGM 与 keysound 分池 / 偷取保护长样本）碰 dense hot-path 红线、后续；floor=128 对峰值>128 的极端谱仍可能偷（调 config 到 256）。
- **BMS 原生未动**：其 store 兼播玩家键音、config 是玩家 polyphony 偏好；用户"BMS 正常"基准疑为 beatoraja（polyphony 高、不偷不同 WAV），OMS BMS 模式 32 其实也可能偷 bgm1、只是未逐秒比对。
- **连带澄清并作废**：之前"autoplay ~note130 / KEY-note 键音路径丢声 / 惰性 LoadSamples 时序窗口"方向**全部作废**（争议人声是 BGM `bgm1` 长样本、非 KEY note）；2026-06-06 诊断"不操作下 BGM 100% fire"只验证了**触发**、未验证长样本是否**中途被掐**，故 autoplay-vs-不操作的"矛盾"消解（两种模式其实都丢）；memory resume 清单（定位第130 KEY note / 埋 KEY-note 路径）不再需要。**#1 跨路径 per-WAV cut / 转谱键音重复是独立遗留（与本次 BGM 丢失无关）、仍后置。**
- **验证**：`osu.Desktop.slnf` Release **0 错 0 警告**；mania autoplay + 转谱 + drawable **30/30**；**用户实测 2026-06-07：autoplay 与游玩下 `bgm1` 16s 人声均恢复 ✅**。

### 代码 / 测试：转谱音频链路全链路审查 → 落地两条**确定结构缺陷**修复（mania 转谱 keysound prewarm + store 通道接配置），均不动对象模型；autoplay 人声丢失仍待实测确认

承接 2026-06-06 的 autoplay 人声丢失未决项，本轮先做**全链路静态审查**（converter → BGM/scratch object+drawable → store → sample info → mania 注入 → mania note/LN/head/tail 键音时序 → `DrawableHitObject` 基类 → `ManiaAutoGenerator` → BMS 原生基线+prewarm → `GameplayClockContainer.OnSeek`），把诊断从"真因未定"收敛为**三条确定结构缺陷 + 一条强假设 + 排除一个误区**，并落地其中两条低风险、可静态验证的修复。

**审查核心模型**：转谱把音频分成冷热不均两条路径——(A) **store 路径**（BGM/scratch）：`DrawableBmsConverted*.CheckForResult`（`timeOffset≥0`，与 autoplay 无关）直接 `store.Play(KeysoundSample,0,cutGroup)`，**绕过惰性 LoadSamples**、常驻通道池、per-WAV cut、pause/seek 停；(B) **一次性路径**（KEY note=mania `Note`、LN head=嵌套 `DrawableHoldNoteHead` 播 `NodeSamples[0]`）：`updateState(Hit)→PlaySamples()`，**依赖惰性 `LoadSamples`**（`DrawableHitObject.Update` 首帧才装样本，回收时 `samplesLoaded=false`+`ClearSamples`）、无 per-WAV cut。BMS 原生 `DrawableBmsHitObject` 则**全部键音走 store**且 autoplay 时 `PrewarmKeysounds` 预热全样本。

**确定缺陷①（已修）— mania 转谱完全缺 keysound prewarm**：BMS 原生 autoplay 在 `DrawableBmsRuleset.LoadComplete` 调 `Playfield.PrewarmKeysounds(getBeatmapKeysoundSamples())` → 每个 keysound 经 `PrepareSamplePool` 预建池加载到 BASS；mania 侧此前**无任何 prewarm** → KEY note 每个键音首次播放即首次磁盘加载，dense autoplay 段集中加载 → 卡 + 丢声。**修复**：`DrawableManiaRuleset.LoadComplete()` 新增 `prewarmConvertedKeysounds()`——gate 在「converted-BMS（`sharedKeysoundStore != null`）+ `ModAutoplay`」（对齐 BMS 原生只在 autoplay 预热），遍历 `Beatmap.HitObjects` 的标准 `Samples` 与 `HoldNote.NodeSamples` 调 `Playfield.PrepareSamplePool`（纯 osu.Game 代码、无需 BMS 类型引用；BGM/scratch sample-only 对象的 keysound 镜像在 `Samples` 里 → store 路径首播也一并预热）。

**确定缺陷②（已修）— mania 转谱 store 固定 32 通道、不接配置**：`BmsToManiaKeysoundStoreFactory.Create()` 此前 = `new BmsKeysoundStore()`（默认 32），mania 没绑 `KeysoundConcurrentChannels`（BMS 原生 `DrawableBmsRuleset:175` 绑了、可到 256）→ dense BGM（实测峰值 27–36 近 32）饱和偷通道截断且用户无法调高。**修复**：`Create(IRulesetConfigCache?)` 重载在 BMS 程序集内部读 `BmsRulesetSetting.KeysoundConcurrentChannels` 设给 store（复用玩家在 BMS 模式持久的值，mania UI 不暴露但语义一致；cache 缺席的隔离测试场景保留默认）；mania 反射委托改传 `IRulesetConfigCache`（osu.Game 类型，两侧均可引用）。

**确定缺陷③（未修，仍是已知遗留）— 跨/同路径无 per-WAV cut（重复音）**：KEY note/LN head 走一次性 PlaySamples，与 BGM/scratch（store）之间、KEY 彼此之间同槽不互掐 → BMS 原生没有的重复。唯一修复路径=「让 note/LN 走 store」已于 2026-06-06 **两次回归**（非池化嵌套 head 崩溃 / tap-note 走 store 静音真因未定），且 CONSTRAINTS #10 要求**先有 player-level 集成测试 + 运行时日志定性静音真因**再做。本轮不重做，维持已知遗留。

**强假设（待 autoplay 实测）— autoplay 人声丢失真因**：缺陷① + 惰性 LoadSamples 时序窗口（dense 段对象 DePool 后首帧装样本与被 autoplay 命中触发 PlaySamples 的窗口极窄 → Samples 尚空/未就绪 → 无声）。本轮 prewarm **缓解了"首次加载延迟"成分**，但若真因主要是"`LoadSamples` 调用时序"而非加载延迟，则未必完全修到 → **必须在 autoplay 下实测 `macchitodoncho SP_HYPER` ~note130 是否恢复**才能定论；本轮不宣称该缺陷已修复。

**排除的误区**：「时钟 resync / playback discrepancy 掐断 store BGM」——否定。`MasterGameplayClockContainer.checkPlaybackValidity` 只 `Logger.Log` 不调 `Seek`（行 177–194），`OnSeek` 只在真实 Seek（skip / 暂停 resume）触发；2026-06-06 日志里 28s 那条 535ms discrepancy 不会让 store `StopAllPlayback`。

**验证**：`osu.Desktop.slnf` Release **0 错误**（2 个既有测试文件警告）；mania autoplay `TestSceneManiaModAutoplay` **9/9**（其 `TestAutoplayHoldsLongNoteAlongsideSampleOnlyObject` / `TestAutoplayIgnoresSampleOnlyScratchObjects` 含 converted sample-only 对象 + `Autoplay=true` + 完整 game DI → 实际覆盖新的 store 创建+config 读取+prewarm 路径，不崩、判定不回归）；`BmsToManiaBeatmapConverterTest`+`TestSceneDrawableManiaHitObject` **22/22**；完整 `osu.Game.Rulesets.Bms.Tests` **869/869**。**下一步**：autoplay 实测验证强假设；若仍丢声则按 resume 计划埋 KEY-note PlaySamples 路径取证（指向缺陷③的 store 路由，须先补 player-level 集成测试）。

## 2026-06-06

### 代码 / 测试：转谱 note/LN keysound 走共享 store 的尝试——**因运行时回归全部回退到 J6 v1**（崩溃 + 静音两次回归，静音真因待运行时排查）

承接 J6 首版遗留的「mania `Note`/`HoldNote` 自身键音仍走 per-drawable 一次性样本、无 per-WAV cut」残留。用户用真实键音谱 `[Juka_Box]macchitodoncho`（`SP_HYPER` 7K / `9K_HYPER`）实测：转谱后 BMS 原生没有的键音重复/回声、BMS 侧正常。本次尝试把转谱 note/LN 也路由到共享 store（per-WAV cut），**两次引入运行时回归，最终全部回退到 J6 v1**。

- **重复根因（实测确认，结论仍有效）**：BMS 原生全程单一 `BmsKeysoundStore` + per-WAV cut（同槽仍发声时再触发 → 掐断重启 = 单声部）。J6 首版只把 BGM/scratch 路由到 store，可演奏 note/LN head 走 mania 每对象独立 `SkinnableSound`（跨对象/跨路径都不 cut）。脚本统计 `SP_HYPER`：同槽近距重触发 ≤0.4s 212 次 / ≤0.8s 395 次，BGM↔KEY 跨路径共享槽 150 个，完全同时刻双放 0 处 → 根因是缺 per-WAV cut。该谱"人声"实为 `punai_Guitar_*` 吉他切片（252 片段：**246 纯 BGM + 仅 7 个 BGM+KEY 跨路径**）。
- **尝试 1（LN）→ 崩溃 → 回退**：把 LN 做成 store-routed 子类（`BmsConvertedHoldNote` + **非池化自定义嵌套 head**）→ 运行 `InvalidOperationException: Cannot call InternalChild ... (currently 0)` at `DrawableHoldNote.Update()→Head`。根因：mania `DrawableHoldNote.Update()` 无条件访问 `Head`（`headContainer.Child`），非池化自定义嵌套 head 破坏了 mania 嵌套 hold 的加载/挂载时序。→ LN 回退普通 `HoldNote`。
- **尝试 2（tap note）→ 静音回归 → 回退**：把 tap note 做成 `BmsConvertedNote`（mania `Note` 子类）走 store + per-WAV cut。用户实测：**原本重复的那个声音变成完全静音**（不是单声部，而是 0 次播放）——异常。
  - **未能定性根因**：(a) 通道饱和被数据否定——BGM-only 在 0.2–0.3s 窗口峰值已 27–36（接近/超过 32 通道上限）、加 KEY 仅升到 32–41，增量太小；且修复前 BGM 独占 store 也接近上限却能响。(b) 单个 BGM+KEY 同槽 per-WAV cut 逻辑上应得「1 次播放」而非「0 次」，无法由 store/cut 逻辑静态推出静音。→ 真因需**运行时排查**（store 对该 punai 槽的实际通道分配 / cut 时机 / 同帧多次 `Play` 交互），不能再靠静态推理在生产里试错。→ tap note 回退 mania 一次性。
- **当前状态：完整回退到 J6 v1**——仅 BGM/scratch 走 store，note/LN 走 mania 一次性（**转谱键音重复仍是已知遗留**，但音频完整、不崩，与本次改动前一致）。删除 `BmsConvertedNote`/`BmsConvertedHoldNote`/`BmsConvertedHeadNote` 及其 drawable；converter / factory / `ShouldHost` / classdoc 均还原。
- **验证**：`osu.Desktop.slnf` Release **0 错误**（2 个既有测试文件警告）；`BmsToManiaBeatmapConverterTest` **19/19**；完整 `osu.Game.Rulesets.Bms.Tests` **869/869**。

### 诊断结论（同日，运行时日志）：J6 v1 转谱 BGM 播放**客观正确、无丢声**，"密集段缺人声"非转谱 bug

回退到 J6 v1 后，用户又报"`SP_HYPER` 转 mania 20–40s 没人声、40s+ 才出现（全程不操作）"。为定性，临时给 BGM 播放路径 + store 加运行时日志（哨兵 gate，**诊断后已全部移除**），用户实跑两次（第二次开 NoFail）。日志（14152 行）结论：

- **BGM 准时且完整播放、无丢声**：start 落在 0–50s 的每个时间窗，applied 对象 **100% 都 fire**（50–60s 有 68% 未 fire 仅因 ~54s 退出、尾部未到点）；用户关注的"人声"实为 `punai_Perc_*`，在 **3–5s 即精准触发**（先前按固定 BPM 估到 ~37s 是 soflan 错位）。
- **fire 时序完美**：delta(t−start) 平均 **0.6ms**、最大 15ms、**0 个**晚于 50ms。
- **通道饱和彻底否定**：store 全程仅 **6 次 SATURATED**；per-WAV cut 占 9%（154/1761），均落在被重复敲击的持续音乐器槽（正确、与 native 一致）。
- 唯一客观异常：游戏运行日志 gameplay ~28s 一条 `Playback discrepancy 535ms`（密集段卡顿 = **D**），一次时钟 resync 抖动，非丢声。

~~**因此 J6 v1 的"密集段缺人声"不是转谱播放 bug**~~（**此结论已被用户实测推翻，见下方更正**）。**已移除全部诊断埋点、回到干净 J6 v1**（再次 Release 0 错误）。

### ⚠️ 更正（同日，以用户 autoplay 实测为准）：上面"无丢声"结论作废，autoplay 下人声确实丢失

用户再以 **autoplay mod** 复测：本应在 **~note 130** 出现的那段"人声"在 mania 转谱下**完全丢失**（用户"以我的为准"）。我的诊断有两个致命盲区导致误判"无 bug"：

- **盲区 1：只埋了 BGM→store 路径**（`DrawableBmsConvertedBgmSampleHitObject` + `BmsKeysoundStore`），**没埋 mania 可演奏 KEY note 的键音路径**。J6 v1 里转谱 KEY note 是普通 mania `Note`、键音在 `Samples`、走 mania 核心 `DrawableNote.PlaySamples` 一次性、**根本不经 store**——若争议"人声"是 KEY note 键音，日志完全看不到它（"BGM 100% fire"与它无关）。
- **盲区 2：两次诊断 run 都是"全程不操作 + NoFail"、不是 autoplay**。不操作 → KEY note 全 miss → mania 一次性键音本就不响（mania miss 不发声）；只有 autoplay → KEY note 全命中 → 才会触发该键音。故诊断**未覆盖** KEY-note 键音、也**未覆盖** autoplay 场景。

**当前真相：J6 v1 在 autoplay 下 ~note130 人声丢失，是真实音频缺陷、待查**（疑在转谱 KEY-note 键音路径；可能与本日"tap-note 走 store 致某声音完全静音"同源——都指向"转谱 KEY note 键音在某些情况下不发声"）。**Resume 计划**（详见 memory `reference_bms_keysound_chain`）：① 脚本定位 `macchitodoncho SP_HYPER` 第 130 个 KEY note（11-19，忽略 scratch 16）的槽/文件/通道（**有 soflan，勿用固定 BPM 估时**）；② 诊断埋点必须**同时覆盖 KEY-note 键音路径**（临时埋 mania `DrawableNote.PlaySamples`）并保留 BGM/store 埋点；③ **务必在 autoplay mod 下复跑**对照争议槽是否 fire。**总教训：dense+soflan 谱音频问题靠耳朵跨模式比对极不可靠；诊断埋点必须覆盖全部播放路径（BGM-store + KEY-note 一次性）并在 autoplay 下取证，否则会像本轮一样误判。**
- **再尝试前置条件（重要）**：先补 **player-level 集成测试 + 运行时日志**（mania 跑转谱 autoplay：断言不崩 + 观察 store 对共享槽的实际播放/cut），定性静音真因后再设计安全方案；**非池化自定义嵌套 hold drawable 是已知禁区**（崩溃）。

## 2026-06-01

### 代码 / 测试：mania 转谱 BGM/scratch 走共享 `BmsKeysoundStore` 落地（J6 首版：E 实测修复 / D 仍未解后置）

承接 P1-K 对 `BMS -> mania` 转谱音频链路的审查（见 [P1-K CHANGELOG](../P1-K/CHANGELOG.md) 2026-06-01）：BGM 补全的"对象发什么"归 P1-K（K11），但补出的 BGM sample-only 对象在 **mania runtime 如何发声、dense-BGM 是否卡顿**归本子线。规划 J6：

- **播放路由差异（须知）**：BMS 原生 BGM 走 shared `BmsKeysoundStore`（32-256 通道、idle-first、per-WAV cut）；mania 侧 sample-only 对象走非池化 `CreateDrawableRepresentation` + 每对象独立 `SkinnableSound`。dense 键音谱 BGM 常数千事件 → 潜在 alloc/GC/首帧懒初始化卡顿。mania 侧无 `BmsKeysoundStore` 等价预热设施。
- **保真合同**：mania 转谱 BGM 必须 autoplay 出声（与 BMS 原生模式音频一致）；LN 尾在 mania 也须静音（对齐第 3a 条；转谱器不得把尾 keysound 放进 `NodeSamples[1]`，由 K11 落实）。
- **性能策略（分级）**：先复用 sample-only drawable 范式 + mania 对象池/滚动窗口（只活窗口内对象），实测 dense BGM 谱；若不达标再评估 mania 侧共享样本通道池（复用 `BmsKeysoundStore` 思路，并补 BGM per-WAV cut，`BmsBgmEvent.KeysoundId` 已可用），不得为此新长出 per-note/per-lane 独立 player（沿用约束 1）。

上游 P1-K `K11` 已于同日落地解决「BGM 能否出声」。本子线 **J6 首版实现**：转谱 BGM / scratch sample 不再走 per-object 一次性 `SkinnableSound`，改为经一个**复用的 `BmsKeysoundStore`** 播放——`DrawableManiaRuleset` 检测到 converted-BMS beatmap 时（反射 `BmsToManiaKeysoundStoreFactory.ShouldHost/Create`）创建该 store，在 `CreateChildDependencies` 里 `Cache`（按 runtime 类型 `BmsKeysoundStore`，mania 不能编译期命名它）、`load()` 里 `AddInternal` 到游玩树以解析 `GameplayClockContainer`；转谱对象携带 `KeysoundSample` + `KeysoundId`，drawable `[Resolved(CanBeNull = true)]` 该 store 并 `Play(sample, 0, cutGroup)`（store 缺席则安全回退 `PlaySamples`）。这样暂停 / seek 由 store 统一 `StopAllPlayback`（修 **E**），通道有上限 + idle-first 复用、不再每个 BGM 一个 `SkinnableSound`（降低音频对象数，原意缓解 **D**——实测见下未达预期），并白送 per-WAV cut。

涉及：新增 `BmsToManiaKeysoundStoreFactory`；`BmsConvertedBgmSampleHitObject` / `BmsConvertedScratchSampleHitObject` 加 `KeysoundSample` / `KeysoundId`；两个 converted-sample drawable 改走 store；`BmsToManiaBeatmapConverter` 设这两字段；`DrawableManiaRuleset` 加反射宿主 + `CreateChildDependencies` 缓存 + `load` 挂载（仅 converted-BMS 触发，普通 mania 无影响、BMS 缺席为 no-op）。

验证：`dotnet build osu.Desktop.slnf -c Release` **0 错误 0 警告**；`BmsToManiaBeatmapConverterTest` **19/19**（含 BGM 携带 slot/sample 断言）；完整 `osu.Game.Rulesets.Bms.Tests` **869/869** 无回归。**2026-06-01 用户人工实测**：E 已修复（暂停立即停 BGM）✅、B 的 scratch 长条 double 消失 ✅、普通 mania 无回归 ✅；**D 仍未解**——dense 极端谱高密段仍极度缓慢。J6 共享 store 已把音频从数千 `SkinnableSound` 收成 32 通道，但既然 dense 仍极慢，**说明瓶颈不在音频对象数**（疑 drawable 数量 / 转换链 / 渲染），D 后置、日后处理（需先 profile 定位真瓶颈，再决定归 P1-J 后续切片）。已知残留：mania `Note` / `HoldNote` 自身键音仍走 per-drawable 一次性样本（非本 store），暂停期间长音符键音仍可能播完，属较小残留（用户 E 反馈为连续 BGM、已解）。

## 2026-05-31

### 代码 / 测试：per-WAV cut 改按 WAV 槽号归组（不再按文件名）

- 审查"误判为截断的两轮修复有无副作用"时发现：per-WAV cut 此前按 `BmsKeysoundSampleInfo` 文件名值相等归组。但谱师常把同一音频文件挂到多个 #WAV 槽专门做自重叠（hi-hat/拍手），按文件名归组会**错误掐断**这些本应并发的声音（GOODBOUNCE 因每槽独立文件未中招，但通用性有缺）。
- 修复：cut 组键从文件名换成 **WAV 槽号（`KeysoundId`）**。`activeSampleChannels` 改为 `Dictionary<int, _>`，通道记 `CurrentCutGroup`（int?），新增 `Play(sample, balance, int cutGroup)` 承载槽号；槽号由 note/head/BGM 的 `KeysoundId` 与新增的 `BmsLaneKeysoundEntry.KeysoundId`（空击 armed）提供。无槽入口（2 参 `Play` / 数组）走不 cut 路径。对齐 LR2/beatoraja「按槽 cut、不同槽即使同文件也独立重叠」。
- 涉及文件：`BmsKeysoundStore`、`BmsLaneKeysoundEntry`（+`KeysoundId`）、`BmsBeatmapConverter.buildLaneKeysoundTimelines`、`BmsLane.resolveArmedKeysound`、`DrawableBmsHitObject`（`getKeysoundCutGroup`）。`TECHNICAL_CONSTRAINTS.md` 第 9 条已改写并加红线。
- 测试：`TestSharedKeysoundStoreCutsSameSlotRetrigger`（同槽重触发只占 1 通道）+ 新增 `TestSharedKeysoundStoreDoesNotCutDifferentSlotsSharingAFile`（不同槽同文件 → 并发 2 通道）。完整 `osu.Game.Rulesets.Bms.Tests` **866/866**（Debug）通过，Release 0 警告 0 错误。
- 说明：另两处"误判轮"的副作用核查结论——idle-first / shrink dispose 为净改进无副作用；pressed-POOR 出声为刻意行为变化（用户确认保留），不改判定/分数。

### 代码 / 测试：LN tail 不再发声（对齐 LR2/beatoraja「长条只头发声」）

- 承接 P1-K 修复缺省 `#LNTYPE`（长条恢复解析）后，用户实测 GOODBOUNCE 的 scratch 长条出现 "stomp your fee feet"——LNTYPE1 长条尾对象重复了头 WAV（`7H`），OMS 此前在长条尾命中/autoplay 时会再播一次尾 keysound，与头叠成 double，叠加 per-WAV cut 还会掐断头。
- 修复：`DrawableBmsHoldNoteTail.PlaySamples()` 重写为空（不再自动播放尾 keysound，含 release / autoplay），对齐 LR2/beatoraja「长条只头发声」。尾对象 keysound 仍保留在 object 模型（`TailKeysoundSample` / `GetSamples()`）以 arm 空击 keysound 时间线，仅不再 auto-play。`TECHNICAL_CONSTRAINTS.md` 第 3 条拆出 3a 明确该合同。
- 测试：新增 `TestSceneBmsSharedKeysoundTiming.TestHoldNoteTailKeysoundStaysSilentWhileHeadSounds`（长条头发声 `lnhead.wav`、尾静音）。`TestSceneBmsSharedKeysoundTiming` **5/5**、完整 `osu.Game.Rulesets.Bms.Tests` **865/865**（Debug）通过；`osu.Game.Rulesets.Bms` Release 0 警告 0 错误。

## 2026-05-30

### 代码 / 测试：键音链路审查修复——idle-first 通道分配、pressed-POOR 补播、shrink 真 dispose

- 审查 bms-play 键音链路，定位到两处"截断"与一处资源缺口：
  - **提前截断（核心）**：`BmsKeysoundStore.getNextChannel()` 自 initial commit 起为纯 round-robin（不是性能优化引入的回归，但确为热路径），会在远低于复音上限、仍有空闲通道时就回收正在播放的通道——长样本（尤其 layered BGM 长 sustain）被提前切断。改为 **idle-first**：先取空闲通道，只有全部繁忙（真正复音饱和）才按轮转偷取近似最旧者。空闲集由 `reclaimIdleChannels()` 每帧重建（O(N) 读、零分配；`Stack` 预留 `MAX_CONCURRENT_CHANNELS`、`Clear()` 保留容量），`getNextChannel()` 保持 O(1)，不回退 dense-chart 热路径。
  - **pressed-POOR 静音**：osu! 基类仅在 `ArmedState.Hit` 调 `PlaySamples()`，因此按了键但判为 POOR/miss 并消费了输入的 note 完全静音（lane 回退也因消费而不触发）——偏离 IIDX/LR2/beatoraja"按键必出声"。新增 `DrawableBmsHitObject.PlayKeysoundFromPress()`，在 `OnPressed` 判定为非命中、以及 LN head 在 `TryApplyHeadPress` 非命中时于 key-down 补播该 note keysound；clean hit 仍只走 `PlaySamples`、不 double；未按键的自然 miss 与 tail release miss 仍静音。
  - **shrink 不释放**：live channel shrink 原用 `Remove(channel, false)`，留下脱挂未 dispose 的 sound drawable。改为先置 `Retired` 再 `Remove(channel, true)` 真 dispose，分配路径跳过 retired 引用。
- 同日追加 **per-WAV cut（每键音单声部）**：用户反馈 256 通道下仍听到截断后，确认还缺经典 BMS 的"同一 WAV 重触发掐断前一实例"语义。`BmsKeysoundStore` 新增 `activeSampleChannels`（按 `BmsKeysoundSampleInfo` 文件名值相等归组）+ 通道 `CurrentSample`：`getChannelForSample()` 在该通道仍 busy 且 `CurrentSample` 值相等时复用之，令同 WAV 重触发干净重启而非叠加。对齐 BM98/LR2/beatoraja，并缓解同音连打饿死通道池。`TECHNICAL_CONSTRAINTS.md` 新增第 9 条。
- 文档同步：`TECHNICAL_CONSTRAINTS.md` 第 3 条改写键音播放语义合同（key-down 必出声），新增第 8 条 idle-first 分配 + dispose 合同、第 9 条 per-WAV cut；`DEVELOPMENT_PLAN.md` J1 验证项、`DEVELOPMENT_STATUS.md` 代码/验证/已确认事实/进度矩阵同步。
- focused validation：`--filter "FullyQualifiedName~Keysound|FullyQualifiedName~TestSceneBmsPlayerAudioSemantics"` **50/50** 通过（含新增 `TestSharedKeysoundStorePrefersIdleChannelOverBusyOne`、`TestPoorPressStillTriggersKeysound`、`TestSharedKeysoundStoreCutsSameSampleRetrigger`）；完整 `osu.Game.Rulesets.Bms.Tests` **863/863**（Debug）通过；`osu.Game.Rulesets.Bms` Release 构建 0 警告 0 错误。
- ⚠️ 未结：用户报告一段约 1~2 秒人声 keysound 在 256 通道、低密度下仍被截断（"stomp your feet" 念到 f 处断）。静态分析已排除键音通道池（idle-first 经测试证明不偷在播通道；每样本 `DrawablePool` `maximumSize=null` 不回收在播实例；BMS 侧正常播放无显式 stop，仅 pause/seek 触发 `StopAllPlayback`）。根因待用户提供 autoplay 复现 / 该 WAV 的谱面排布（是否被重复触发）后再定位。

## 2026-05-18

### 代码 / 测试：补上 BMS keysound 的 autoplay 预热缺口，前移首次 sample pool 初始化

- 进一步排查 dense full autoplay 的“整局只卡一次”后，当前更具体的结论是：core `Playfield` 虽然会预建 `hitObject.Samples` / `AuxiliarySamples` 的 sample pool，但 BMS gameplay keysound 走的是 `BmsKeysoundStore` 专用路径，并不吃这条通用预热链。
- 为把首次命中的 keysound sample pool 初始化从 gameplay 时刻前移出去，`DrawableBmsRuleset` 现在会在 full autoplay 的 `LoadComplete()` 时收集 beatmap 中所有 BMS keysound，并交给 `BmsPlayfield.PrewarmKeysounds()` 预建底层 sample pool；`Playfield` 也新增了显式的 `PrepareSamplePool()` 入口给 ruleset-local 预热复用。
- 这条补丁没有继续碰 replay correctness，只是把可能的一次性懒初始化成本挪到进场加载阶段，目标是压掉 dense autoplay 中偶发但致命的首次卡顿。
- focused validation：`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter "FullyQualifiedName~TestSceneBmsAutoplayReplayPlayback|FullyQualifiedName~TestAutoPlayObjectsStillApplyMaxResult"` **4/4** 通过；邻接 keysound 回归 `dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter "FullyQualifiedName~TestSceneBmsSharedKeysoundTiming|FullyQualifiedName~TestSceneBmsKeysoundPlaybackLifecycle|FullyQualifiedName~TestSceneBmsKeysoundChannelConfigBinding"` **9/9** 通过。

### 代码 / 测试：BMS full autoplay 分流到对象级 `AutoPlay` 与 direct-time replay 采样

- dense autoplay 的下一刀没有继续碰 core `FramedReplayInputHandler`，而是只对 BMS full autoplay 分流：`DrawableBmsRuleset` 现在会给 full autoplay 下的 `BmsHitObject` 设置对象级 `AutoPlay`，并改用 `BmsAutoplayReplayInputHandler` 作为专用 replay input handler。
- 这条 handler 不再承担“逐 replay frame 边界推进判定”的职责，而是把 replay 输入降级为“按当前时间直接采样状态”，继续服务 `ReplayPlayer` / HUD / key counter；普通 replay 仍保留既有 `BmsFramedReplayInputHandler` 和逐边界推进合同。
- 为了证明这条分流没有把 correctness 打坏，新增了 player-level `TestSceneBmsAutoplayReplayPlayback`。该 scene 现在用真实 `LN + scratch` stub chart 验证三件事：full autoplay replay 会完成回放、所有非忽略判定仍为 `Perfect`、并且 key counter 仍能收到 replay input。
- focused validation：`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter FullyQualifiedName~TestSceneBmsAutoplayReplayPlayback` **3/3** 通过；相邻回归 `dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter "FullyQualifiedName~TestSceneBmsAutoplayReplayPlayback|FullyQualifiedName~BmsReplayFrameTest|FullyQualifiedName~TestSceneBmsReplayStability|FullyQualifiedName~TestSceneBmsReplayRecording|FullyQualifiedName~TestAutoPlayObjectsStillApplyMaxResult"` **11/11** 通过。

## 2026-05-17

### 代码 / 测试：dense autoplay 的 replay-state 分配收口，core skip-fast-forward 回退

- `BmsReplayFrame` 现已缓存 lane-action mask 与 lane-only action list；`BmsFramedReplayInputHandler` 则直接复用这些缓存，去掉每帧 `Any/Where/ToList` 来判断重要区间与构造 pressed actions。
- 同轮曾尝试在 core `FramedReplayInputHandler` 中让 non-frame-accurate playback 单次跨过多个 replay frame，以减少高密度 autoplay 的 catch-up 成本；后续人工压测显示这会让 autoplay 丢掉中间输入状态并出现大量 miss，因此该 fast-forward 已撤回。
- 当前结论是：BMS replay handler 的缓存化去分配是安全保留项；而 core `SetFrameFromTime()` 在 frame-stable playback 下仍必须保持“每次调用最多推进一个 replay frame 边界”的合同。
- focused validation：撤回 core skip-fast-forward 后，`dotnet test osu.Game.Tests --no-restore -v minimal --filter FullyQualifiedName~FramedReplayInputHandlerTest` **9/9** 通过；`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter "FullyQualifiedName~BmsReplayFrameTest|FullyQualifiedName~TestSceneBmsReplayStability|FullyQualifiedName~TestSceneBmsReplayRecording"` **7/7** 通过。

### 代码 / 测试：长键 body tick 的每帧解析改为 early-break

- `BmsHoldNote.CreateNestedHitObjects()` 本来就按时间顺序生成 `BodyTicks`；基于这条既有合同，`DrawableBmsHoldNote.resolveBodyTicksUpToCurrentTime()` 现在在遇到首个 future tick 时会直接停止扫描，而不是每帧继续把整条长键剩余 body tick 列表从头扫到尾。
- 这是一条专门针对 dense long-note / HCN 压力场景的 hot-path 减负，不改 tail/body 结算语义，也不碰一次性的 `resolveAllBodyTicks()` 完结路径。
- focused validation：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsDrawableRulesetTest|FullyQualifiedName~BmsGaugeProcessorTest"` **111/111** 通过。

### 测试：补上 player-level pause / seek 音频语义 proof

- 新增 `TestSceneBmsPlayerAudioSemantics`，把当前用户最关心的两条 BMS player 语义独立锁住：pause / resume 期间 `GameplayClockContainer` 会持位并从原位置继续，而 seek 回 `BmsBgmEvent` 之前后，shared store 的旧请求会被清掉，重新跨过事件时间后会再次发起播放请求。
- 这条 focused scene 刻意不把 headless 虚拟 source track 的 `Track.IsRunning` 直接当作“真实主音轨已经暂停”的唯一判据，而是把 proof 收口在 Player 当前真正拥有的 gameplay clock 语义和 `BmsBgmEvent` 重播合同上，避免在 backing-track 观察面不足时过度承诺。
- focused validation：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~TestSceneBmsPlayerAudioSemantics"` **3/3** 通过。

### 代码 / 测试：继续收口 `BmsLane` 与 shared store 的热路径分配与重复扫描

- `BmsLane` 已移除玩家命中后的重复 ordered-hit `HandleHit()` 调用；player-hit 路径现只在 `DrawableBmsHitObject.OnUserPressedSuccessfully` 上触发一次 locking 扫描，不再在 `NewResult` 上重复做同一轮候选遍历。
- empty-poor 检查已改成无 `HashSet` 的布尔流式判定。该路径原本只做布尔 OR，不依赖唯一计数，因此去掉按键期去重分配不会改变结果，但能减少每次空击检测的分配压力。
- `BmsKeysoundStore` 的单样本入口现已切到 channel-local 双缓冲：在继续遵守 `SkinnableSound.Samples` array contract 的前提下，shared store 不再为每次单样本播放临时 new 单元素数组。对应新增 `BmsDrawableRulesetTest.TestSharedKeysoundStoreSingleSamplePathRotatesBuffers`，锁住连续单样本播放仍会更新到新 sample 的合同。
- focused validation：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter "FullyQualifiedName~TestSharedKeysoundStoreSingleSamplePathRotatesBuffers|FullyQualifiedName~TestSceneBmsSharedKeysoundTiming|FullyQualifiedName~TestSceneBmsKeysoundPlaybackLifecycle|FullyQualifiedName~BmsOrderedHitPolicyTest|FullyQualifiedName~TestBeatorajaLaneTriggersLateEmptyPoorAfterJudgedNote|FullyQualifiedName~TestLr2LaneDoesNotTriggerLateEmptyPoorAfterJudgedNote"` **11/11** 通过。

### 代码 / 测试：补上 shared keysound store 的 pause / seek 生命周期回收

- `BmsKeysoundStore` 现会监听 `GameplayClockContainer.IsPaused` 与 `OnSeek`，并在 gameplay 暂停或 seek 时统一执行 `StopAllPlayback()`，避免通用 `PausableSkinnableSound` 只立即停掉 looping sample 的默认语义，让长 one-shot BGM / keysound 样本继续穿透暂停或拖拽边界。
- 新增 headless focused suite `TestSceneBmsKeysoundPlaybackLifecycle`，分别锁住 pause 与 seek 两条 shared-store 生命周期回收链，不再只靠人工复现验证 Autoplay 拖拽与暂停恢复场景。
- focused validation：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter "FullyQualifiedName~TestSceneBmsKeysoundPlaybackLifecycle|FullyQualifiedName~TestSceneBmsSharedKeysoundTiming|FullyQualifiedName~TestSceneBmsKeysoundChannelConfigBinding"` **9/9** 通过。

## 2026-05-16

### 代码 / 测试：补齐 shared keysound timing 的 owner-level focused proof，并收口 pooled sample fallback 边界

- 新增 `TestSceneBmsSharedKeysoundTiming`，分别锁住 `DrawableBmsHitObject` 命中与 `BmsLane` lane replay 在同一 step 内就会向 shared `BmsKeysoundStore` 发起请求，不再只靠大回归文件间接覆盖。
- 这条 focused scene 同时暴露出 lane replay 的 pooled sample retrieval 边界仍可能把错误冒泡到调用方；`Playfield.GetPooledSample()` 现已在 pool 未 ready 或取样失效时回退 `null`，由既有 `SkinnableSound` consumer contract 自动降级成 unpooled sample，而不是直接让 gameplay 链路抛错。
- focused validation：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~TestSceneBmsSharedKeysoundTiming"` **3/3** 通过；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release` **774/774** 通过。

### 测试：补上 `BmsOrderedHitPolicy` 的 dedicated focused suite

- 新增 `BmsOrderedHitPolicyTest`，把 scratch stream ordered-hit 的两个核心合同从 `BmsDrawableRulesetTest` 中独立出来：前一个对象结算后后一个对象可正常命中；若仍处于 miss window 内直接击打后一个对象，则前一个未判对象会被强制记为 miss。
- 这次补强不改生产代码，只把 `J5` 的 owner-level focused coverage 从“完全依赖大回归文件”推进到“ordered-hit 已有单独 suite，shared timing 作为剩余主缺口待补”。
- focused validation：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~BmsOrderedHitPolicyTest"` **2/2** 通过。

### 代码 / 文档：补齐 `J4` 剩余的 config-binding coverage 与 settings 口径同步

- 新增 headless focused suite `TestSceneBmsKeysoundChannelConfigBinding`，把 `RulesetConfigs` 中的 `KeysoundConcurrentChannels` 改值真实驱到 `DrawableBmsRuleset -> BmsPlayfield.KeysoundStore`，同时覆盖初始加载与 live update 两条链路。
- `BmsSettingsSubsection` 的 `键音通道数` hover 提示现已同步到当前 runtime 合同：调高会立即补充可用通道，调低则等待超额 channel 自然停播后再逐步回收，不再暗示 runtime 改值会直接切断当前音频。
- 这次收口后，`J4` 的生产代码残留缺口已从“store resize + binding + UX”三段，压缩到只剩 dense-chart / layered-BGM / rapid empty-strike 的后置人工验收。
- focused validation：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~TestSceneBmsKeysoundChannelConfigBinding"` **3/3** 通过。

### 代码：`J4` 首刀把 live channel resize 改成 non-destructive contract

- `BmsKeysoundStore` 不再在 `KeysoundConcurrentChannels` 变更时整池 `Clear()`；现在 grow 会立即扩容，shrink 则只在超额 channel 不再处于 active/queued 状态后再裁剪，避免 runtime 改值立刻切断当前播放。
- 为了让 headless tests 能精确驱动同一裁剪逻辑，shared store 现暴露最小 internal 测试面：实际 channel 数、channel pool 枚举，以及 `ApplyPendingChannelResize()`。
- `BmsDrawableRulesetTest` 新增 `TestSharedKeysoundStoreShrinkDoesNotCutActiveChannelsImmediately` 与 `TestSharedKeysoundStoreShrinkRemovesStoppedDeferredChannels`，锁住 shrink 保活和停播后回收语义。
- focused validation：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~BmsDrawableRulesetTest"` **60/60** 通过；完整 `osu.Game.Rulesets.Bms.Tests` **766/766** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### 代码：`J1` / `J2` / `J3` 首刀落地并收口 gameplay hot path

- `BmsKeysoundStore` 已移除 gameplay keysound 的无条件下一帧 `Schedule()`，并新增数组快路径与单样本播放入口；命中 / lane replay keysound 现默认走 same-frame 播放。
- `BmsLane.shouldTriggerEmptyPoor()` 已改为单次遍历候选，不再在每次按键上先 `ToArray()`；`BmsOrderedHitPolicy.getParticipatingHitObjects()` 也已改为 alive-first 流式枚举，不再为判空物化整组对象。
- `DrawableBmsHitObject.PlaySamples()` 已收口到单样本 keysound 路径，去掉为单个 sample 做 `Cast().ToArray()` 的重复分配。
- focused validation：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~BmsDrawableRulesetTest"` **58/58** 持续通过；补回缺失 chart filter stats 合同后，更宽 `osu.Game.Rulesets.Bms.Tests` 全量回归已恢复，当前最新快照为 **766/766**；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### 文档：新建 P1-J 子线并冻结首轮 hot-path 优化范围

- 已建立 `P1-J` 四件套，正式把 BMS gameplay runtime 的 keysound timing、lane/order hot path、sample allocation 与 live channel resize 安全合同独立归线。
- 当前已明确判定：该专题不并入 `P1-C` 或 `P1-E`；`P1-C` 继续拥有判定/反馈语义，`P1-E` 继续拥有真实谱面验校，而 `P1-J` 单独拥有 shared gameplay/audio hot path 的优化 authority。
- 最新只读审查已收口四类首轮风险：shared `BmsKeysoundStore` 的无条件 `Schedule()` 播放延后、`BmsLane` / `BmsOrderedHitPolicy` 的容器枚举热路径、重复 sample 数组分配，以及 `KeysoundConcurrentChannels` live 改值 rebuild-all 可能切断当前音频。
- 本轮仅完成文档治理与归线规划，无生产代码改动、无新增测试执行。
