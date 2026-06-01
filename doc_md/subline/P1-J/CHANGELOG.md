# P1-J 变更日志：BMS gameplay runtime 性能与音频时序治理

> 本文件记录 `P1-J` 相关的验证通过变更，按时间倒序排列。
> 当前进度见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，执行规划见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

---

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
