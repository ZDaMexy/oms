# P1-J 开发进度：BMS gameplay runtime 性能与音频时序治理

> 最后更新：2026-05-18
> 主线全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。本文件只记录 `P1-J` 的真实进展。

## 当前阶段

- **阶段定位**：`P1-J` 已从 planning-only 进入首轮代码落地；`J1` 已完成，`J2` / `J3` 第一刀已完成，`J4` 已完成，当前重点已从“50k 密段明显慢放”转到“dense full autoplay 是否仍残留 once-per-run 单次致命卡顿”的剩余收口，同时保留 `J5` 的后置人工验收与 `J3` 是否冻结的决策。core replay fast-forward 已确认不再继续；当前 active branch 改为只对 BMS full autoplay 做专用分流。
- **代码状态**：shared `BmsKeysoundStore` 继续作为 BGM / note / LN / lane replay 的统一播放池，但 gameplay keysound 已不再无条件延后到后续 scheduler tick；`BmsLane.shouldTriggerEmptyPoor()` 与 `BmsOrderedHitPolicy.getParticipatingHitObjects()` 已去掉首批热路径对象物化，`DrawableBmsHitObject.PlaySamples()` 已收口到单样本 keysound 路径，`KeysoundConcurrentChannels` live 改值也已从 rebuild-all 改为 grow-immediately / shrink-deferred 的 non-destructive resize；同轮还补上 pause / seek 生命周期回收，shared store 现会在 gameplay 暂停与 seek 时主动停掉活跃频道，避免长 one-shot BGM / keysound 样本穿透暂停边界。其后续热路径 follow-up 也已落地：`BmsLane` 不再在玩家命中后通过 `NewResult` 重复触发 ordered-hit 扫描，empty-poor 检查已去掉 per-press `HashSet` 分配，shared store 单样本入口也已改成 channel-local 双缓冲，不再为每次单样本播放临时 new 单元素数组；`DrawableBmsHoldNote.resolveBodyTicksUpToCurrentTime()` 也已改成按时间有序的 early-break，不再在每帧把整条长键后续 body tick 全量白扫。dense autoplay 的 replay 链最近保留了两条 BMS-side 优化：一是 `BmsFramedReplayInputHandler` 继续复用 `BmsReplayFrame` 缓存的 lane-action 掩码与列表，去掉每帧 `Any/Where/ToList`；二是 `DrawableBmsRuleset` 现对 full autoplay 额外分流到“对象级 `AutoPlay` + `BmsAutoplayReplayInputHandler` 直接按当前时间采样输入状态”的专用路径，不再让 dense full autoplay 继续依赖逐 replay frame 边界驱动判定本身。基于最新人工回报里“50k 已可播完但偶发单次致命卡顿”，当前又补上一条更窄的 owner-side 预热：因为 core `Playfield` 只会预建 `hitObject.Samples` / `AuxiliarySamples` 的 sample pool，而 BMS gameplay keysound 走的是 `BmsKeysoundStore` 专用 keysound sample，所以 full autoplay 现在会在 `LoadComplete()` 时预热 beatmap 内唯一 keysound 对应的 sample pool，把首次命中的懒初始化成本前移到进场加载。
- **验证状态**：`BmsDrawableRulesetTest` 已在每个切片后持续通过；`J4` 现已同时具备 shared-store shrink 回归与 `config -> drawable ruleset -> playfield` direct binding coverage，其中 `TestSceneBmsKeysoundChannelConfigBinding` **3/3** 已通过；`J5` 侧已同时补上 dedicated `BmsOrderedHitPolicyTest` **2/2**、`TestSceneBmsSharedKeysoundTiming` **3/3**、`TestSceneBmsKeysoundPlaybackLifecycle` **3/3** 与新的 player-level `TestSceneBmsPlayerAudioSemantics` **3/3**，把 scratch stream ordered-hit、shared-store same-frame timing、pause / seek 生命周期回收，以及 seek 回放后的 BGM event 重播语义都从大回归文件里独立出来；本轮还补上 `TestSharedKeysoundStoreSingleSamplePathRotatesBuffers` 并回跑 ordered-hit / empty-poor / shared-store 相关 focused suite **11/11**。针对 dense autoplay / replay 的最新 focused validation 当前基线为：在撤回 core skip-fast-forward 后，`FramedReplayInputHandlerTest` **9/9** 通过，BMS replay focused suite `BmsReplayFrameTest|TestSceneBmsReplayStability|TestSceneBmsReplayRecording` **7/7** 通过；新增 `TestSceneBmsAutoplayReplayPlayback` 也已证明 full autoplay 专用路径仍会完成回放、保持非忽略判定全 `Perfect`，并继续驱动 key counter，focused suite **3/3** 通过；相邻 combined replay/autoplay suite **11/11** 通过。最新补上的 autoplay keysound 预热改动也已通过 autoplay-focused **4/4** 与 keysound-neighbour **9/9**。完整 `osu.Game.Rulesets.Bms.Tests` 的最近全量快照仍为 **774/774**。
- **文档状态**：`P1-J` 四件套与主线文档现已同步到“首轮代码已开始落地”的口径，不再停留在仅规划状态。

## 已确认事实

- `BmsPlayfield.KeysoundStore` 是当前 shared keysound pool owner；`DrawableBmsHitObject` 和 `BmsLane` 都通过它播放样本。
- `BmsKeysoundStore.Play()` 已移除 gameplay keysound 的无条件 `Schedule()`；当前保留 `IEnumerable`、数组与单样本三个入口，其中数组路径可直接复用上游已 materialize 的样本数组。
- `BmsLane.shouldTriggerEmptyPoor()` 现已改为单次遍历候选，不再先 `ToArray()`；`BmsOrderedHitPolicy.getParticipatingHitObjects()` 也已改为 alive-first 的流式枚举，不再为判空而先物化整组对象。
- `DrawableBmsHitObject.PlaySamples()` 已收口为单样本 keysound 路径，不再对单个 keysound 做 `Cast().ToArray()`；`BmsLane.playCurrentLaneKeysound()` 也已接到 shared store 的单样本入口。
- `BmsLane` 现已移除玩家命中后的重复 ordered-hit `HandleHit()` 调用；当前 player-hit 路径只在 `DrawableBmsHitObject.OnUserPressedSuccessfully` 上触发一次 ordered-hit 扫描。
- `BmsLane.shouldTriggerEmptyPoor()` 现已去掉 per-press `HashSet` 去重分配；这段逻辑只做布尔 OR，不依赖唯一计数，重复候选不会改变最终判定。
- `BmsKeysoundStore` 的单样本入口现已改成 channel-local 双缓冲；在继续遵守 `SkinnableSound.Samples` array contract 的前提下，shared store 不再为每次单样本播放临时构造新的单元素数组。
- `BmsHoldNote.CreateNestedHitObjects()` 会按时间顺序生成 `BodyTicks`；因此 `DrawableBmsHoldNote.resolveBodyTicksUpToCurrentTime()` 现在可以在遇到首个 future tick 时直接 `break`，避免 HCN/CN/LN 长键在每帧重复扫描整条后半段 body tick 列表。
- dense autoplay 当前确认仍走 `ReplayPlayer -> FrameStabilityContainer -> FramedReplayInputHandler` 链，而不是玩家手动输入链；因此高压 autoplay 掉帧时，replay catch-up 与 replay-state 分配本身也是可优化的 owner surface。
- non-frame-accurate replay 过去虽然不是“每帧全表扫描”，但在 frame-stable playback 下，`FramedReplayInputHandler.SetFrameFromTime()` 仍承担着“每次调用最多推进一个 replay frame 边界”的合同，供外层 catch-up 循环逐步喂入中间输入状态。dense autoplay 的人工压测已经证明，若在这里单次跨过多个 frame，会直接转化成 autoplay miss，而不是纯性能收益。
- `BmsReplayFrame` 现已缓存 lane-action mask 与 lane-only action list；`BmsFramedReplayInputHandler` 不再每帧做 `Any/Where/ToList` 来判断重要区间和构造 pressed actions。
- `DrawableBmsRuleset` 现已把 full autoplay 与普通 replay 区分开：full autoplay 会给 `BmsHitObject` 设置对象级 `AutoPlay`，并改走 `BmsAutoplayReplayInputHandler` 的 direct-time 输入采样；普通 replay 仍保留既有 `BmsFramedReplayInputHandler` 与逐 replay frame 边界推进合同。
- core `Playfield` 虽然会在 `OnHitObjectAdded()` 时预建 `hitObject.Samples` / `AuxiliarySamples` 的 sample pool，但 BMS gameplay keysound 走的是 `BmsKeysoundStore` 专用 `BmsKeysoundSampleInfo` 路径，并不自动吃这条通用预热；因此 dense full autoplay 的单次卡顿补丁当前落在 `DrawableBmsRuleset.LoadComplete() -> BmsPlayfield.PrewarmKeysounds()` 这条 BMS owner 链，而不是继续改 core replay。
- `KeysoundConcurrentChannels` 当前默认值为 `32`；live 改值已改成 non-destructive resize：grow 立即扩容，shrink 延后到超额 channel 停播后再裁剪，不再通过整池 `Clear()` 立刻切断当前播放。
- `PausableSkinnableSound` 默认只会在 sample-disable 时立即停掉 looping 样本；为避免长 one-shot `BmsBgmEvent` / keysound 样本穿透 pause / seek，`BmsKeysoundStore` 现额外监听 `GameplayClockContainer.IsPaused` 与 `OnSeek`，并在边界处统一 `StopAllPlayback()`。
- `TestSceneBmsPlayerAudioSemantics` 现已锁住两条更接近玩家观察面的合同：`GameplayClockContainer` 在 pause / resume 间会持位并从原位置继续，且 `BmsBgmEvent` 在 seek 回事件之前后会清掉旧请求，并在再次跨过事件时间时重新向 shared store 发起播放请求。
- `DrawableBmsRuleset` 到 `BmsPlayfield.KeysoundStore` 的 `KeysoundConcurrentChannels` live binding 现已有 dedicated headless coverage；`BmsSettingsSubsection` 的提示文案也已同步到“调高立即补通道、调低延后回收、不直接切断当前发声”的当前合同。
- `BmsDrawableRulesetTest` 现已锁住 late-empty-poor 行为；`P1-J` 任何优化都必须以“不回归这些语义”为前提。
- 当前同轮审查**没有**新增证据证明 `BmsSoloPlayer` 的 pre-start / start-sequence 主音乐 handoff 仍有新的 gameplay bug，因此 `P1-J` 当前不把它列为首轮重点。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 子线归线与四件套建档 | 已完成 | `P1-J` 已正式建立 |
| `J1` keysound timing hardening | 已完成 | gameplay keysound 已切到 same-frame 播放，且 note hit / lane replay 的 owner-level timing proof 已补齐 |
| `J2` lane / ordered-hit hot path 收口 | 进行中 | `BmsLane` / `BmsOrderedHitPolicy` 的首批 `ToArray()` / 判空物化已收口，玩家命中链的重复 ordered-hit 扫描也已移除 |
| `J3` sample allocation tightening | 进行中 | `DrawableBmsHitObject` 单样本路径、shared store overload 与 channel-local 单样本双缓冲已落地；仍有 array-based sample contract 未完全消除 |
| `J4` live channel reconfigure safety | 已完成 | non-destructive resize、drawable binding focused test 与 settings tooltip 已同步 |
| `J5` focused validation / dense-chart checklist | 进行中 | ordered-hit **2/2**、shared timing **3/3**、player audio semantics **3/3**、targeted suite **11/11**、replay-focused **7/7**、autoplay combined **11/11**、autoplay-facing **4/4**、keysound-neighbour **9/9** 与完整 `osu.Game.Rulesets.Bms.Tests` **774/774** 已通过；dense-chart manual checklist 仍后置 |

## 当前风险

- **剩余分配风险**：shared store 已移除单样本按次数组分配，但 `SkinnableSound.Samples` 仍是 array-based contract；若后续继续下探，只能在不破坏多样本 / pooled sample 合同的前提下推进。
- **后置验收风险**：自动化回归已恢复全绿，但 dense fully-keysounded chart、layered BGM、rapid empty-strike 与 live channel change 的人工 checklist 仍未执行。
- **sample 生命周期风险**：底层 `PausableSkinnableSound` / pooled sample contract 仍没有 one-shot sample 的真 pause/resume 能力；当前已先把 pause / seek 逃逸收口为“边界即停播”，但 layered BGM 的恢复体验仍应放在后置人工 checklist 中确认。
- **主音乐验证边界**：当前新增的 player-level proof 锁住的是 `GameplayClockContainer` pause / resume 持位语义与 `BmsBgmEvent` seek 重播语义；若后续要对“带独立主音轨的 BMS 谱面在 pause 后是否按真实音频位置恢复”做更强结论，仍应补一条带实际 backing-track 观察面的专项验证或人工 checklist。
- **shared pool 边界风险**：lane replay focused scene 暴露过 pooled sample retrieval 的脆弱边界；当前 `Playfield.GetPooledSample()` 已改为在 pool 不可用时回退 `null`，由 `SkinnableSound` 继续降级到 unpooled sample。后续若再改 pooled-audio authority，必须重跑 shared timing focused suite。
- **渲染预算风险**：附带日志仍显示 `ReplayPlayer` 场景下 `Texture upload queue large` 与 atlas 扩张记录，但全局统计末尾的 `Bass CPU%` 很低；这说明超高压 autoplay 更像 update / render / present 综合预算问题，而不只是音频 mixer 压力。当前 50k chart 已可完成 autoplay，因此主要剩余现场风险已从“整段慢放”收窄成“是否仍存在 once-per-run 单次致命卡顿”。
- **首次样本初始化风险**：current patch 认为剩余单次卡顿更像 BMS gameplay keysound 首次 sample pool 初始化，因此对 full autoplay 补上了 unique keysound prewarm；这条假设已经通过代码与 focused regression 自洽，但是否真正击中用户现场卡顿，还要靠下一轮相同 dense chart 人工压测确认。

## 下一检查点

1. 用同一套 10k / 50k dense autoplay chart 重新做人工压测，但重点已改为确认 keysound prewarm 后是否还会出现 once-per-run 单次致命卡顿，而不是继续判断 50k 能否完整播完。
2. 若单次卡顿仍可稳定复现，收集更贴近卡顿时刻的 runtime / performance 日志与现场时间点，再决定是继续下钻首次样本初始化边界，还是转向 render/update/present 预算侧诊断。
3. 执行 dense fully-keysounded chart、layered BGM、rapid empty-strike 与 live channel change 的人工 checklist，并把结果回交 `P1-G`。

## 当前验证基线

- 当前 focused suite 基线已在“验证状态”和“进度矩阵”中汇总：ordered-hit **2/2**、shared timing **3/3**、player audio semantics **3/3**、targeted suite **11/11**、replay-focused **7/7**、autoplay combined **11/11**、autoplay-facing **4/4**、keysound-neighbour **9/9**，完整 `osu.Game.Rulesets.Bms.Tests` 最近快照 **774/774**。
- 桌面端 Release 构建当前可通过；按日期展开的热路径切片、回归命令与构建记录见 [CHANGELOG.md](CHANGELOG.md)。
