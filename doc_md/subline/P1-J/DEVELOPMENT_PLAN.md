# P1-J 开发计划：BMS gameplay runtime 性能与音频时序治理

> 最后更新：2026-05-18
> 主线总规划见 [../../mainline/DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md)。本文件只拆解 `P1-J` 的执行顺序；判定/反馈语义见 [../P1-C/DEVELOPMENT_PLAN.md](../P1-C/DEVELOPMENT_PLAN.md)，真实谱面验校见 [../P1-E/DEVELOPMENT_PLAN.md](../P1-E/DEVELOPMENT_PLAN.md)。

## 子线定位

| 维度 | 归属 | 说明 |
| --- | --- | --- |
| 主归属 | `P1-J` | 收口 BMS gameplay runtime 的 keysound 时序、dense-chart 热路径与 shared audio pool 安全合同 |
| 协作子线 | `P1-C` | `P1-J` 的任何优化都不得破坏 empty-poor、LN tail keysound、judge-family-specific lane 行为与反馈语义 |
| 协作子线 | `P1-E` | `P1-J` 先把 runtime hot path 收口，再把真实谱面验校与 dense-chart checklist 回交给 `P1-E` / `P1-G` |
| 协作子线 | `P1-G` | dense fully-keysounded chart、BGM layering、rapid empty-strike 与 live channel change 的最终人工确认仍后置到 `P1-G` |

## 归线结论

- 该专题**不并入 `P1-C`**。`P1-C` 当前负责判定、训练反馈、结果页与绿色数字/调速反馈闭环；它可以约束 `P1-J` 不得回归语义，但不应该再拥有 `BmsKeysoundStore` / `BmsLane` / `BmsOrderedHitPolicy` 这类 shared runtime hot path 的性能 authority。
- 该专题**不并入 `P1-E`**。`P1-E` 的职责是用真实谱面验证 gameplay 结果，而不是拥有一套独立的 shared pool / lane locking / sample allocation 合同。若把 hot-path 优化挂到 `P1-E`，后续会把“修运行时”和“做验校”混成一条线。
- 该专题**不并入 `P1-B`**。当前发现的问题不是输入注入链本身，而是输入进入 BMS runtime 后，在 keysound playback、empty-poor 检查与 lane locking 上的后半段热路径。
- 因此 `P1-J` 作为独立子线成立，和 `P1-I` 一样拥有自己的执行顺序、冻结点与回归边界。

## 当前确认基线

- `BmsPlayfield` 当前缓存一个 shared `BmsKeysoundStore`，并由 `DrawableBmsHitObject`、`BmsLane` lane replay / empty-hit playback 与 BGM/note/LN keysound 共用。
- `DrawableBmsRuleset` 当前通过 `KeysoundConcurrentChannels` 把 settings/runtime 写回到同一个 shared pool；默认值为 `32`，配置范围是 `1..256`。
- shared keysound timing hardening 已完成：`BmsKeysoundStore.Play()` 不再对 gameplay keysound 做无条件下一帧 `Schedule()`；pause / seek 生命周期回收也已补齐，并有 player-level `TestSceneBmsPlayerAudioSemantics` 锁住 pause/resume 持位与 `BmsBgmEvent` seek 后重播语义。
- lane/order hot path 首轮收口已完成：`BmsLane.shouldTriggerEmptyPoor()` 与 `BmsOrderedHitPolicy.getParticipatingHitObjects()` 的首批对象物化已移除，玩家命中后的重复 ordered-hit 扫描也已去掉；`DrawableBmsHoldNote.resolveBodyTicksUpToCurrentTime()` 现会在遇到首个 future tick 时 early-break。
- sample allocation tightening 仍在进行中，但当前已落地的边界包括：`DrawableBmsHitObject` 单样本 keysound 路径、`BmsKeysoundStore` channel-local 单样本双缓冲、以及 full autoplay 的 unique keysound sample pool prewarm，用于把首次命中的 sample pool 初始化前移到进场加载。
- live channel reconfigure safety 已完成：`KeysoundConcurrentChannels` 现为 grow-immediately / shrink-deferred 的 non-destructive resize，并有 `config -> drawable ruleset -> playfield shared store` 的 direct binding coverage。
- dense autoplay 当前已明确两条冻结事实：
  1. core `FramedReplayInputHandler` 的 generic stepping contract 不能再被放宽；`SetFrameFromTime()` 仍必须保持 one-boundary-per-call progression。
  2. 若要继续优化 dense full autoplay，只能在 BMS owner side 分流；当前 full autoplay 已改走对象级 `AutoPlay` + `BmsAutoplayReplayInputHandler` 的 direct-time 输入采样，而普通 replay 保持既有边界推进语义。
- 现阶段**没有**新增证据表明 `BmsSoloPlayer` 的 pre-start / song-select music handoff 仍有新的 gameplay 主音乐接管 bug；因此 `P1-J` 继续把重点放在 gameplay 内部 hot path，而不是 start-sequence。
- 现有 automated coverage 已锁住相关语义：`BmsDrawableRulesetTest` 覆盖 late-empty-poor，`FramedReplayInputHandlerTest` 锁住 core replay stepping contract，`TestSceneBmsAutoplayReplayPlayback` 锁住 full autoplay correctness 与 replay-loaded HUD/key-counter surface；后续优化不能把这些回归当成“性能改动可接受副作用”。

## 分期计划

### J0：归线、术语与观测基线

状态：已完成

目标：先把“什么属于 runtime hot-path contract、什么只是后置体验验收”写死，避免后续在没有 measurement / regression guard 的情况下做泛化调优。

建议交付：

1. 建立 `P1-J` 四件套，并同步主线索引、主线总规划与主线状态页。
2. 冻结本子线的首轮范围：只处理 shared keysound timing、lane/order hot path、sample allocation 与 live channel resize，不把问题扩张为“整个仓库的性能专项”。
3. 建立 focused validation checklist：dense fully-keysounded chart、layered BGM、rapid empty-strike、late-empty-poor、live channel change、Release build。
4. 对仍缺量化验证的点先建立 owner-level test 落点，而不是先开大而泛的 benchmark 框架。

### J1：keysound timing hardening

状态：已完成；same-frame 播放、pause/seek 生命周期回收与 player-level 语义 proof 均已落地

目标：shared `BmsKeysoundStore` 继续作为唯一播放 authority，但 gameplay 的 note / BGM / LN keysound 不再被默认压到后续 scheduler tick 才真正播放。

建议交付：

1. 先审出 `BmsKeysoundStore.Play()` 的调用者是否已经处在 gameplay update 线程；若调用链都在同一线程，优先移除无条件 `Schedule()`。
2. 若确有非 gameplay-thread 调用者，不能继续沿用“全部调度到下一帧”的粗暴路径；应改成显式 queue/flush 或等价的 same-frame marshal，而不是让 gameplay 命中音频永久带帧级延后。
3. 保持 balance / shared pool / 多样本播放 authority 不变；不得借机让每个 `DrawableBmsHitObject` 拥有自己的 sample player。
4. 明确验证 note hit、BGM event、LN head 与 lane replay 的播放语义不回归；其中"玩家按键必出声"为现行合同——pressed-POOR/miss（含 LN head）在 key-down 时补播该 note keysound，未按键的自然 miss 仍静音；**LN tail 一律不发声**（只头发声），BGM / autoplay 继续按既有合同播放（详见 TECHNICAL_CONSTRAINTS 第 3 / 3a 条）。

可能文件切片：

1. `osu.Game.Rulesets.Bms/Audio/BmsKeysoundStore.cs`
2. `osu.Game.Rulesets.Bms/UI/DrawableBmsHitObject.cs`
3. `osu.Game.Rulesets.Bms/UI/BmsLane.cs`
4. `osu.Game.Rulesets.Bms.Tests/*keysound*` 或新的 owner-level store tests

### J2：lane / ordered-hit hot path scan 收口

**状态：进行中；首批 `ToArray()` / 判空物化已收口**

目标：把 `BmsLane` 与 `BmsOrderedHitPolicy` 从“每次按键或命中时都枚举容器对象”收口成更窄、更稳定的候选 authority，同时保留当前 empty-poor / late-empty-poor 语义。

建议交付：

1. `BmsLane.shouldTriggerEmptyPoor()` 不再在每次 `OnPressed()` 时 materialize `DrawableBmsHitObject[]`；应改为按当前前景候选或等价的窄窗口 authority 做判定。
2. `BmsOrderedHitPolicy.getParticipatingHitObjects()` 不再把 `AliveObjects` / `Objects` 枚举作为 gameplay 热路径的默认实现；若 detached test harness 需要 fallback，应把 fallback 与 runtime hot path 清晰拆开。
3. `HandleHit()` 与 `IsHittable()` 的优化不能改变现有 late-empty-poor 行为，尤其不能破坏 `BEATORAJA` / `LR2` 差异语义。
4. 若需要 cache / queue / next-candidate pointer，优先放在 `BmsLane` / `BmsOrderedHitPolicy` 这两个 owning abstraction，而不是外部 UI 或测试 harness 里堆条件分支。

可能文件切片：

1. `osu.Game.Rulesets.Bms/UI/BmsLane.cs`
2. `osu.Game.Rulesets.Bms/UI/BmsOrderedHitPolicy.cs`
3. `osu.Game.Rulesets.Bms.Tests/BmsDrawableRulesetTest.cs`
4. 必要时新增 dedicated lane hot-path regression scene

### J3：sample allocation tightening

状态：进行中；已与 `J1` / `J2` 同刀收口重复数组分配，并继续补到 full autoplay keysound prewarm

目标：在不改变 sample authority 的前提下，削减 dense-chart 里的重复数组分配与 LINQ 中间对象，降低 GC 压力。

建议交付：

1. 收口 `DrawableBmsHitObject.PlaySamples()` 与 `BmsKeysoundStore.Play()` 之间的双重 `ToArray()`；只保留一处必要的 materialize 边界。
2. `BmsLane.playCurrentLaneKeysound()` 不再每次按键都 new 一个单元素数组；应提供 dedicated single-sample path 或可复用缓冲。
3. 任何 allocation tightening 都不能删除多样本播放能力；BGM / LN / 复合 sample 仍必须维持现有合同。
4. 不把这一刀扩大成全仓库 LINQ 清扫；只处理已确认在 BMS gameplay hot path 上的分配点。

可能文件切片：

1. `osu.Game.Rulesets.Bms/Audio/BmsKeysoundStore.cs`
2. `osu.Game.Rulesets.Bms/UI/DrawableBmsHitObject.cs`
3. `osu.Game.Rulesets.Bms/UI/BmsLane.cs`

### J4：live channel reconfigure safety

状态：已完成；shared store 已从 rebuild-all 切到 non-destructive resize，direct binding coverage 与表述同步也已补齐

目标：把 `KeysoundConcurrentChannels` 的 runtime 改值补成稳定合同，避免 gameplay 中无提示硬切当前播放中的样本。

完成交付：

1. 已明确区分“增加 channel 数”和“减少 channel 数”的语义，并以 grow-immediately / shrink-deferred 的 non-destructive resize 落地，而不是继续 rebuild 整个 pool。
2. deferred apply 的文案、行为与测试已同步到同一合同；runtime 改值不再默认切断当前播放链。
3. `ruleset config -> drawable ruleset -> playfield shared store` 的 focused binding test 已通过 `TestSceneBmsKeysoundChannelConfigBinding` 补齐。
4. settings tooltip 与主线/子线文档已继续保持“低值更易截断、高值成本更高”的调参表述，同时明确 grow/shrink 的实际生效语义。

可能文件切片：

1. `osu.Game.Rulesets.Bms/UI/DrawableBmsRuleset.cs`
2. `osu.Game.Rulesets.Bms/Audio/BmsKeysoundStore.cs`
3. `osu.Game.Rulesets.Bms/BmsSettingsSubsection.cs`
4. 对应 settings/config focused tests

### J5：focused validation 与后置验收

状态：进行中；focused regression 与 BMS 全量自动化回归已通过，autoplay 专用 replay proof 与 keysound-neighbour regression 也已闭合，dense-chart manual checklist 与 once-per-run hitch 现场确认仍后置

目标：让 `P1-J` 有独立的 automated proof，再把 dense-chart / BGM layering 体验回交给 `P1-G` 做最终人工确认。

建议交付：

1. automated validation 现已覆盖：keysound timing hardening、lane/order late-empty-poor regression、config->store binding、shared timing owner-level proof、player-level pause/seek proof、full autoplay correctness / replay input counter，以及 allocation tightening / keysound prewarm 未回归 sample 语义。
2. Release build 继续作为子线门槛；本专题不能以“只是性能优化”为理由跳过 build gate。
3. manual checklist 继续后置到 `P1-G`：dense fully-keysounded chart、layered BGM、LN tail keysound、rapid empty-strike、live channel resize、pre-start -> gameplay 正常过渡。
4. 当前 1-2 已成立；待 3 完成后，`P1-J` 才能进入只接回归修复的冻结态。

## 明确不做

1. 不借本专题替换 ManagedBass、重做全局 audio backend 或引入跨 ruleset latency framework。
2. 不把“修 gameplay keysound timing”偷换成默认新增用户音频 offset 控件；BMS 当前主 timing-correction 路径仍应保持视觉链路优先。
3. 不在 `P1-J` 里顺手推进 Phase 2 功能，如 `FHS`、BSS / MSS、全键模式扩张或新的 gameplay mod。
4. 不把本专题扩写成泛化性能愿望单、渲染愿望单或 borderless/fullscreen 体验总路线；当前 authority 只覆盖 BMS gameplay audio/runtime hot path。

## 当前优先顺序

1. 用同一套 dense autoplay chart 再做现场压测，重点确认 full autoplay keysound prewarm 之后是否仍会出现 once-per-run 单次致命卡顿。
2. `P1-G` 后置人工验收：dense fully-keysounded chart、layered BGM、rapid empty-strike 与 live channel change。
3. 评估 single-sample array contract 是否继续下探，还是把当前实现作为 `J3` 第一阶段冻结点；若继续触碰 pooled-audio boundary，先回跑 `TestSceneBmsSharedKeysoundTiming` 与完整 `osu.Game.Rulesets.Bms.Tests`。
