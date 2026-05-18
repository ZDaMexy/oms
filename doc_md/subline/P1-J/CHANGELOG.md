# P1-J 变更日志：BMS gameplay runtime 性能与音频时序治理

> 本文件记录 `P1-J` 相关的验证通过变更，按时间倒序排列。
> 当前进度见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，执行规划见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

---

## 2026-05-18

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
