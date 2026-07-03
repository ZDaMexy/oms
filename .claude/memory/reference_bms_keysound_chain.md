---
name: reference-bms-keysound-chain
description: "BMS keysound chain contracts + BMS→mania converted-audio landmines: idle-first allocator, key-down always sounds, per-WAV cut keyed by WAV slot (not filename), LN tail silent, the #LNTYPE-default decoder root cause; plus J6 store-routing current state (BGM/scratch non-pooled drawables; tap KEY note now POOLED, routed via mania IManiaKeysoundStore/IHasManiaKeysound interfaces; channel floor 128) and the dead-ends not to retread"
metadata:
  node_type: memory
  type: reference
  originSessionId: 20fbcdec-71c0-4b92-b255-775b78ab861a
---

> 本文件 = BMS 键音链路的**稳定合同 + 易踩坑**（recall 用）。按日期展开的调查史在 `doc_md/subline/P1-J/CHANGELOG.md`（J 系）与 `doc_md/subline/P1-K/CHANGELOG.md`（K11）；权威约束在 `doc_md/subline/P1-J/TECHNICAL_CONSTRAINTS.md` #3 / #3a / #8 / #9 / #10 / #11。
> ✅ 已解决的「按 key1 触发 bgm1 / 胡乱按键长音重叠 / 暂停不停」bug 见 [[reference-bms-bgm1-pause-keytrigger-bug]]——真因 = mania 按键音效反馈 `GameplaySampleTriggerSource`，**与本文件的 store / per-WAV cut 无关**（早先 orphan-on-reuse / LN-head 等误判已全部回退/否定）。

## 链路总览

键音来源（note / LN head+tail / BGM）在转换期由 `BmsBeatmapConverter.createKeysoundSample` 落到对象上；空击 armed 键音走 `BmsBeatmap.LaneKeysoundTimelines`（含 invisible channel 31-49 arming，二分取"最近≤now"，符合 IIDX）。播放统一汇聚到单一 `BmsPlayfield.KeysoundStore`（`BmsKeysoundStore`，基线 32 通道、饱和自动增长封顶 256，BGM 与玩家键音共用；**2026-06-22 起基线硬编码、无 UI 设置**——「键音通道数（基线）」选项已删）。

## 四条反直觉合同（改前都"像 bug"，别退回去）

1. **通道分配是 idle-first，且饱和时自动增长（2026-06-21 起，非偷取；用户实机实测确认优化明显 ✅）**。`getNextChannel()` 先取每帧由 `reclaimIdleChannels()` 重建（(re)size 时播种）的 `freeChannels` 空闲通道；**全通道繁忙（真复音饱和）时新增一个通道（封顶 `MAX_CONCURRENT_CHANNELS`=256）而非偷断仍在播的样本**，仅在已达 256 仍饱和才轮转偷取。演进史：纯 round-robin（initial）→ idle-first（修「低于上限就偷断」）→ 自动增长（修「达上限就截音」）。**真因**：旧默认 32 远低于真实 BMS 复音（叠层 BGM + 长衰减样本，转谱实测峰值 27–36>32），转谱-mania 早 floor 128（#10）而**原生只有 32**——同谱原生更糟、且把取舍甩给用户旋钮；自动增长一并消除（保真单调=只补不截、自然有界于该谱真实峰值复音）。`ConcurrentChannels` 现是**起始/常驻基线**（非硬上限）：调高即扩容、调低 non-destructive（闲置即收/发声延后收），增长到基线以上的通道**不随常规播放裁剪**（避免增长/收缩抖动）。保持 `getNextChannel` O(1)（freeChannels 空集即判定饱和→增长，**不扫描全池**）、每帧重建 O(N) 无分配；**不得为"更准"改成每次 Play 全表扫描**（dense 热路径红线，#8）。shrink 用 `Retired` 标记 + `Remove(channel,true)` 真 dispose。**2026-06-22 删除「键音通道数（基线）」UI 设置**（自动增长后无需手调）：`BmsRulesetSetting.KeysoundConcurrentChannels` enum 成员/默认/滑条/`DrawableBmsRuleset` 绑定全移除，基线回落硬编码 `DEFAULT=32`（原生 `new BmsKeysoundStore()`）/转谱楼底 128。**无隐患删除关键**：ruleset config 按 enum **成员名**(`RealmRulesetSetting.Key=lookup.ToString()`)持久化、删中间成员**不移位**其它设置；消费方全移除→旧库残留值惰性无害不再被读、用户旧自定义值不再生效。`BmsToManiaKeysoundStoreFactory.Create(IRulesetConfigCache?)` 签名**保留**（`DrawableManiaRuleset` 以 `Func<IRulesetConfigCache,Drawable>` 反射绑定，参数现未用）。`ConcurrentChannels` setter（non-destructive resize）仍在、现仅构造时由 factory 用。**红线**：勿为「让用户调通道数」重新引入该设置（P1-J CONSTRAINTS #2）。
2. **玩家 key-down 必出声**。clean hit 走 `DrawableBmsHitObject.PlaySamples()`（`ArmedState.Hit`）；被消费的 pressed-POOR/miss（普通 note 在 `OnPressed`、LN head 在 `TryApplyHeadPress`）经 `PlayKeysoundFromPress()` 补播——osu 基类只在 Hit 调 `PlaySamples`，不补播就"按了键判 POOR 却静音"，偏离 IIDX/LR2/beatoraja。未按键的自然 miss 仍静音；LN tail 见第 4 条。
3. **per-WAV cut 按 WAV 槽号（`KeysoundId`）归组，绝不按文件名**。`activeSampleChannels`（键=槽号）+ 通道 `CurrentCutGroup`；同槽仍发声时被再触发 → 复用其通道干净重启（掐前一实例）。**红线**：不同槽即使同文件也不合并——谱师常把同一文件挂到多个 #WAV 槽专门做自重叠（hi-hat/拍手），按文件名归组会错误掐断。槽号经 `Play(sample, balance, int cutGroup)` 传入（note/head/BGM 的 `KeysoundId`、空击 armed 的 `BmsLaneKeysoundEntry.KeysoundId`）；无槽入口不 cut。同槽重复排布后一次必掐前一次（预期）。
4. **LN tail 一律不发声**（`DrawableBmsHoldNoteTail.PlaySamples()` 空实现，含 release/autoplay）。LNTYPE1 尾对象常重复头 WAV，播放会与头叠 double（实测 GOODBOUNCE scratch 长条 "stomp your fee feet"），per-WAV cut 下还掐头；对齐 LR2/beatoraja「长条只头发声」。尾 `TailKeysoundSample` 仍保留供空击 armed 时间线，只是不 auto-play。

## autoplay 必须 = 100% 完美游玩（判据 + 已修双触发，2026-06-21，**用户实机实测确认优化明显·暂无异常 ✅**）

**判据（用户提出、已采纳为审查框架）**：autoplay 的发声必须与「每音符完美命中」逐次等价；若 autoplay 有而完美游玩没有的差异 → 是 autoplay 专属 bug；若两者都有 → 真实游玩也坏（同一条 store 路径），不得只当 autoplay 问题。**本轮两改（lane 双触发抑制 + 键音池自动增长）用户实机对照 beatoraja 实测「优化及其明显、暂时无异常」✅——这是该链路唯一可靠的保真验收（虚拟轨测试对真发声/真不截是盲区）。**

**已修的 autoplay 专属差异 = lane 双触发**（CONSTRAINTS #3b）。完美游玩：音符命中**消费按键** → 事件不下传 lane → lane 不发声 → 每音符只经自身 `PlaySamples` 一次声。autoplay：[DrawableBmsRuleset.cs](osu.Game.Rulesets.Bms/UI/DrawableBmsRuleset.cs) `CreateDrawableRepresentation` 把音符设 `AutoPlay=true`（→ `HandleUserInput=false`、退出输入处理）→ replay 合成按键直达 `BmsLane.OnPressed` → `playCurrentLaneKeysound()` 的 armed 键音**叠**音符自身 auto-apply 键音（`CheckForResult(false)`→`ShouldAutoApplyMaxResult`→`PlaySamples`）= 每音符两次 store 播放。**per-WAV cut 多数时塌成一次**（同槽复用同通道 Stop+Play），但**露馅**于：armed 槽≠音符槽（隐藏通道 3x arming / fallback）→ 发两个不同音；`Play()` 后 `IsPlaying` 异步未及时置位 → 两路各占一通道真重叠（轻微 flam）；lane 用 balance 0、音符用定位声像。**修复** = `BmsLane.playCurrentLaneKeysound` 在 `laneHasAutoPlayNote()`（`HitObjectContainer.AliveObjects` 有 `!AcceptsPlayerInput` 的 `DrawableBmsHitObject`）时 early-return，发声交给音符自身。玩家 lane 不匹配（音符接受输入、命中消费按键）→ 真·空击键音不受影响。回归 `TestAutoPlayNoteSuppressesRedundantLaneKeysound`（对照 `TestLaneReplayTriggersSharedKeysoundImmediately`：玩家音符 lane 仍发声）。注：这也覆盖 auto-scratch/auto-note 的 auto-lane。BGM（`BmsBgmEvent`，非 `BmsHitObject`）在 playfield 容器、不在 lane，不受此影响。

**连带结论（用判据反推、本轮未动）**：① 键音池饱和截断（已改自动增长，见合同#1）= **非 autoplay 专属**，玩家完美游玩同样截；② 键音从游戏更新线程**同步触发、无样本级前瞻调度**（`PlaySamples` 嵌在 `UpdateState(Hit)` 内）= 架构性，帧抖动/GC 时挤堆，玩家游玩同样有，后置（需解耦的样本级调度器，碰大改）。

## "人声截断"的真根因在解码器，不在键音池（务必先怀疑解析）

`GOODBOUNCE [A]`（`_goodbounce(SPA).bms`）等差分用 5X/6X 长条通道却**省略 `#LNTYPE`**；`BmsBeatmapInfo.LongNoteType`（`int?`）缺省 null，`handleLongNoteChannelEvent` 只认 `case 1/2` → 缺省整条丢长条（该谱丢 31 条），vocal 收尾段在 scratch 长条上丢 = "念到 f 断" + 少键。修复 `LongNoteType ?? 1`（规范默认 1；权威 `doc_md/other/BMS_FORMAT_REFERENCE` §5 / P1-K CHANGELOG 2026-05-31）。**教训**：BMS "截断"先查解析/少键（尤其省略 #LNTYPE、scratch 长条），别一头扎进音频通道池。

## BMS→mania 转谱音频（K11 + J6）当前真实状态

- **K11（转谱发什么对象）**：转谱器 `case BmsBgmEvent` 把 BGM（autoplay ch01）转成 sample-only `BmsConvertedBgmSampleHitObject`（column 0、Alpha=0、`isScorableHitObject` 排除、不进 TotalObjectCount/star）autoplay 发声，补回纯键音 BMS 在 mania（mania ruleset + `ShowConvertedBeatmaps`）丢失的鼓/贝斯/铺底/人声；同刀把非 scratch 长条 `NodeSamples[1]` 与 scratch 长条 tail sample 置空，对齐第 4 条。样本源 ruleset 无关（`FilesystemBackedBeatmapResourceProvider` 指向 `chartbms/`，BGM 用同型 `BmsKeysoundSampleInfo` piggyback）。
- **J6（mania-runtime 怎么播）**：转谱 BGM/scratch/tap-note 改走**复用的 `BmsKeysoundStore`**——`DrawableManiaRuleset` 反射 `BmsToManiaKeysoundStoreFactory` 创建、`CreateChildDependencies` 按 runtime 类型 `Cache`、`load` 里 `AddInternal` 解析 `GameplayClockContainer`；暂停/seek 由 store 统一 `StopAllPlayback`，缺席安全回退 `PlaySamples`。已落地的真实状态：
  - **E（暂停停 BGM）已修 ✅**（必须中心化 pause-aware store；一次性样本不随暂停停）。
  - **tap-note→store 已转正为生产默认，且已池化（2026-06-10）**：`case BmsHitObject` 发 `BmsConvertedKeyNoteHitObject`、`ShouldHost` 含 KEY 音符 → per-WAV cut 跨 BGM↔KEY + KEY↔KEY(tap) 统一生效，转谱键音重复的 **tap-note 部分已修**。✅ **此前 🔴「非池化 perf」已解**：不再有专用非池化 `DrawableBmsConvertedKeyNote`（已删）。做法 = mania 定义自有接口 `IManiaKeysoundStore`（`BmsKeysoundStore` 显式实现，桥接 cut/no-cut 重载）+ `IHasManiaKeysound`（`BmsConvertedKeyNoteHitObject`/BGM/scratch 显式实现）；转谱 drawable 工厂**不再认领 KEY note** → `CreateDrawableRepresentation` 返回 null → 框架基类型池回退命中 mania `Note` 池 → 发**池化 `DrawableNote`**；其 `PlaySamples` 重写：有 store+`IHasManiaKeysound` 键音则 `IManiaKeysoundStore.Play(sample,balance,cutGroup)`，否则 `base.PlaySamples()`。store 在 `CreateChildDependencies` 额外 `CacheAs<IManiaKeysoundStore>`。**音频语义零改动**（命中同时机/同 store/同 cut；回归守护 `TestSceneBmsToManiaKeyNoteStoreRouting` 2/2）。未碰核心 BMS 概念、未新长 per-note sample player。
  - **长 BGM 被通道饱和偷断已修 ✅**：长 BGM（如 `bgm1`，整首单次触发、占通道最久）曾在 32 通道饱和（实测峰值 27–36>32）时被 `getNextChannel` 轮转偷取掐断；转谱侧修复 = store `ConcurrentChannels = Math.Max(DEFAULT=32, 128)`（floor 远超峰值 → 永不被偷；2026-06-22 前为 `Math.Max(config,128)`，删「键音通道数」设置后不再读 config）。**2026-06-21：原生侧改为「饱和自动增长（封顶 256）」**——比固定 floor 更通用的治本方向（见合同#1），原生不再受 32 限、不再与转谱 128 不对称；转谱侧仍保留 128 floor（可后续也切到自动增长统一）。原「治本（长样本不进可偷池/分池）碰 #8 红线」的顾虑，被「增长而非偷取」绕开（增长不偷任何通道、O(1) 不扫描）。
  - **bgm1 按键触发 bug 已修 ✅**：BGM/scratch sample-only 对象的 `Samples` 置空（键音改只放 `KeysoundSample` 经 store 自动发声）。详见 [[reference-bms-bgm1-pause-keytrigger-bug]] + CONSTRAINTS #11。
  - **mania 转谱 keysound prewarm**（`DrawableManiaRuleset.LoadComplete → prewarmConvertedKeysounds`；**2026-06-11 起玩家模式也跑、不再 gate ModAutoplay，BMS 原生同步放开**——玩家模式无预热时全场 ~362 WAV 游玩中冷解码，实测触发 3 次 ~220ms **阻塞 gen2 全量 GC** 冻结（集中开局前 30s）；全量预载对齐 LR2/beatoraja，代价=加载变长，P1-J #7 已重写）：遍历 `Samples`/`HoldNote.NodeSamples` 调 `Playfield.PrepareSamplePool`，且额外按 `IHasManiaKeysound.KeysoundSample` 预热（BGM/scratch `Samples` 空、键音仅此可达；store 与 playfield sample pool 共享 decoded 缓存）。store 通道基线由 `BmsToManiaKeysoundStoreFactory.Create(IRulesetConfigCache?)` 设 `Math.Max(DEFAULT=32, 128)`（**2026-06-22 起不再读 config**——删「键音通道数」设置后；`configCache` 参数仅留作 mania 反射签名）。
  - **通道同样本快路径（2026-06-11，游玩期帧抖动真因修复、用户实测 ✅）**：`BmsKeysoundChannel.PlaySingleSample` 记住 `currentSingleSample`，值相等（同槽 memo 同实例 → 引用相等）则**跳过 `Samples` 赋值**直接 `Stop+Play` 重启（cut 语义不变）；多样本 `PlaySampleArray` 显式置 null 防误跳过。**真因机制**：每次换 `Samples` 引用 → `SkinnableSound.updateSamples()` 全量重建 sample-drawable（RemoveAll+Clear+GetPooledSample+Add，实测 ~30KB/次、中寿命）→ **gen1 晋升风暴**（gen0:gen1 锁死 1:1、每 ~40KB 一次回收、~100 次/秒、15–30ms 帧尖峰、规律「一顿一顿」、与按键/密度挂钩、休息段即恢复）；原生 mania 音符 `PausableSkinnableSound` 持久加载、重播零重建——同密度原生平稳/转谱抖的不对称即此。修复后同段 maxFrame 15–30ms→5–10ms。

## 别重蹈的坑（来自两次回归 + 多轮误判）

- **非池化自定义嵌套 hold drawable 必崩**：`DrawableHoldNote.Update()` 无条件访问 `Head`（`headContainer.Child`），非池化自定义嵌套 head 破坏 mania 嵌套加载时序 → `Cannot call InternalChild ... (currently 0)`。LN 走 store 须用**池化嵌套头**。
- **mania 池按 `GetType()` 精确匹配，但有 base-type 回退**（2026-06-10 查清、即 tap 池化修复的机制）：`Playfield.prepareDrawableHitObjectPool` 在精确类型未注册时遍历已注册池找 `IsInstanceOfType` 命中者 → 一个 `Note` 子类**会**被回退到 `Note` 池、发池化 `DrawableNote`。**关键**：该回退只在对象走池化路径（即 `DrawableRuleset.CreateDrawableRepresentation` 返回 **null**）时触发；只要 `CreateDrawableRepresentation` 返回非空 drawable，对象就非池化、回退永不跑。⇒ 让转谱 `Note` 子类**池化**的正解 = 别为它造专用 drawable（工厂不认领、返回 null），键音改经接口在池化 `DrawableNote.PlaySamples` 里路由 store（见 J6）。`HoldNote` 子类（LN）暂不能照搬：嵌套 head/tail 须池化类型，否则崩（见下条）。star 不变（`ManiaDifficultyCalculator` 用 `is HoldNote`/否则 note，含子类）。
- **dense+soflan 谱音频问题靠耳朵跨模式比对极不可靠**：本链多轮误判都是没先取证就改（"无丢声"结论被推翻、autoplay 丢声误判为 KEY-note 路径实为长 BGM 被偷断）。改前先用运行时日志取客观数据。再尝试 note/LN→store 前须先有 player-level 集成测试 + 运行时日志（常驻 seam：`BmsKeysoundStore.EnablePlaybackLogForTesting()` + `KeysoundPlaybackRecord` + `TestSceneBmsToManiaKeyNoteStoreRouting`）。
- **隐藏发声路径**（既不经 store 也不经 hit-object `PlaySamples`）：直接在最底层 `PoolableSkinnableSample.Play()` 加 `Environment.StackTrace` 探针定位最快（bgm1 bug 即一栈定位到 `GameplaySampleTriggerSource`）。
- **虚拟轨（TestWorkingBeatmap）测试环境对"音轨是否真发声/静音"是盲区**：音频静音类改动不能只靠单测、必须真实 app 实测（Track 静音尝试因此假阳性、已回退）。
- **perf 诊断：「占总分配 %」单指标会误导，晋升/存活才是 GC 卡顿判别器**：store Play 路径只占总分配 10%（185MB/1.7GB）却是帧抖动主因——字节少但全是**中寿命对象**（存活数秒→逢 gen0 必晋升）。判别信号 = **gen0:gen1 比值**（锁死 1:1 = 晋升风暴）+ `GC.GetTotalPauseDuration()` 增量 + 回收间隔分配量（每 ~40KB 一次 = 预算坍缩）；探针 seam = `BmsGameplayStallDiagnostics`（store 子节点、真实游玩才激活、stall/gen2/心跳才写日志）+ store 的 `PlayPathAllocatedBytes`/`TotalKeysoundPlays`/`ColdKeysoundFirstPlayCount`（临时，收口后移除）。~220ms 偶发冻结的签名 = `STALL+GEN2` 同帧（阻塞 gen2，多由游玩中冷解码大缓冲顶爆 gen2 预算；后台并发 gen2 仅几 ms 不可感知）。

## 仍开放的相邻遗留（均与已解决的 bgm1 bug 无关）

- **转谱键音重复的 LN 部分**：仍后置（须池化嵌套头）。tap-note 部分已修且已池化（见上）。
- **长 BGM resume 截断**：暂停后恢复续播——需把长 BGM 改走时钟驱动 Track 才能保位，一次性 store 样本做不到。native + 转谱通用。
- **per-WAV cut orphan-on-reuse**：traced 但未验证、尝试已回退，后置。
- **dense D 卡顿（已大幅收口，仅剩 50k 极端档）**：普通密度转谱谱（☆11/12 级）的帧抖动+冻结已于 2026-06-11 确诊修复（同样本快路径 + 玩家模式 prewarm，用户实测「合格」）；仅 **50k 极端 autoplay 谱**是否仍慢未 profile——如重提，先用 `BmsGameplayStallDiagnostics` 日志归因再改。
- **选歌预览 Track 泄漏进游玩**：有 master AudioFile 的谱面 `working.Track` 是真实音频，BMS/转谱两条游玩路径都没让它静音（BMS 原生解耦时钟不停源 Track；转谱-mania 时钟驱动 Track 叠键音）；`StopUsingBeatmapClock` 修复尝试在真实耦合时钟下无效、已回退，须先真实 app 取证再重做。
