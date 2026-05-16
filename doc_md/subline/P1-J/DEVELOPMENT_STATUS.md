# P1-J 开发进度：BMS gameplay runtime 性能与音频时序治理

> 最后更新：2026-05-16
> 主线全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。本文件只记录 `P1-J` 的真实进展。

## 当前阶段

- **阶段定位**：`P1-J` 已从 planning-only 进入首轮代码落地；`J1` 已完成，`J2` / `J3` 第一刀已完成，`J4` 已完成，当前重点已收窄到 `J5` 的后置人工验收与 `J3` 是否冻结的决策。
- **代码状态**：shared `BmsKeysoundStore` 继续作为 BGM / note / LN / lane replay 的统一播放池，但 gameplay keysound 已不再无条件延后到后续 scheduler tick；`BmsLane.shouldTriggerEmptyPoor()` 与 `BmsOrderedHitPolicy.getParticipatingHitObjects()` 已去掉首批热路径对象物化，`DrawableBmsHitObject.PlaySamples()` 已收口到单样本 keysound 路径，`KeysoundConcurrentChannels` live 改值也已从 rebuild-all 改为 grow-immediately / shrink-deferred 的 non-destructive resize。
- **验证状态**：`BmsDrawableRulesetTest` 已在每个切片后持续通过；`J4` 现已同时具备 shared-store shrink 回归与 `config -> drawable ruleset -> playfield` direct binding coverage，其中 `TestSceneBmsKeysoundChannelConfigBinding` **3/3** 已通过；`J5` 侧已同时补上 dedicated `BmsOrderedHitPolicyTest` **2/2** 与 `TestSceneBmsSharedKeysoundTiming` **3/3**，把 scratch stream ordered-hit 与 shared-store same-frame timing 都从大回归文件里独立出来；完整 `osu.Game.Rulesets.Bms.Tests` 全量回归的最近快照现为 **774/774**。
- **文档状态**：`P1-J` 四件套与主线文档现已同步到“首轮代码已开始落地”的口径，不再停留在仅规划状态。

## 已确认事实

- `BmsPlayfield.KeysoundStore` 是当前 shared keysound pool owner；`DrawableBmsHitObject` 和 `BmsLane` 都通过它播放样本。
- `BmsKeysoundStore.Play()` 已移除 gameplay keysound 的无条件 `Schedule()`；当前保留 `IEnumerable`、数组与单样本三个入口，其中数组路径可直接复用上游已 materialize 的样本数组。
- `BmsLane.shouldTriggerEmptyPoor()` 现已改为单次遍历候选，不再先 `ToArray()`；`BmsOrderedHitPolicy.getParticipatingHitObjects()` 也已改为 alive-first 的流式枚举，不再为判空而先物化整组对象。
- `DrawableBmsHitObject.PlaySamples()` 已收口为单样本 keysound 路径，不再对单个 keysound 做 `Cast().ToArray()`；`BmsLane.playCurrentLaneKeysound()` 也已接到 shared store 的单样本入口。
- `KeysoundConcurrentChannels` 当前默认值为 `32`；live 改值已改成 non-destructive resize：grow 立即扩容，shrink 延后到超额 channel 停播后再裁剪，不再通过整池 `Clear()` 立刻切断当前播放。
- `DrawableBmsRuleset` 到 `BmsPlayfield.KeysoundStore` 的 `KeysoundConcurrentChannels` live binding 现已有 dedicated headless coverage；`BmsSettingsSubsection` 的提示文案也已同步到“调高立即补通道、调低延后回收、不直接切断当前发声”的当前合同。
- `BmsDrawableRulesetTest` 现已锁住 late-empty-poor 行为；`P1-J` 任何优化都必须以“不回归这些语义”为前提。
- 当前同轮审查**没有**新增证据证明 `BmsSoloPlayer` 的 pre-start / start-sequence 主音乐 handoff 仍有新的 gameplay bug，因此 `P1-J` 当前不把它列为首轮重点。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 子线归线与四件套建档 | 已完成 | `P1-J` 已正式建立 |
| `J1` keysound timing hardening | 已完成 | gameplay keysound 已切到 same-frame 播放，且 note hit / lane replay 的 owner-level timing proof 已补齐 |
| `J2` lane / ordered-hit hot path 收口 | 进行中 | `BmsLane` / `BmsOrderedHitPolicy` 的首批 `ToArray()` / 判空物化已收口 |
| `J3` sample allocation tightening | 进行中 | `DrawableBmsHitObject` 单样本路径与 shared store overload 已落地；仍有 array-based sample contract 未完全消除 |
| `J4` live channel reconfigure safety | 已完成 | non-destructive resize、drawable binding focused test 与 settings tooltip 已同步 |
| `J5` focused validation / dense-chart checklist | 进行中 | `BmsOrderedHitPolicyTest` **2/2**、`TestSceneBmsSharedKeysoundTiming` **3/3** 与完整 `osu.Game.Rulesets.Bms.Tests` **774/774** 已通过；dense-chart manual checklist 仍后置 |

## 当前风险

- **剩余分配风险**：shared store 虽已拿掉上游双重 `ToArray()`，但 `SkinnableSound.Samples` 仍是 array-based contract；`Play(ISampleInfo)` 目前仍要在 store 内部构造单元素数组。
- **后置验收风险**：自动化回归已恢复全绿，但 dense fully-keysounded chart、layered BGM、rapid empty-strike 与 live channel change 的人工 checklist 仍未执行。
- **shared pool 边界风险**：lane replay focused scene 暴露过 pooled sample retrieval 的脆弱边界；当前 `Playfield.GetPooledSample()` 已改为在 pool 不可用时回退 `null`，由 `SkinnableSound` 继续降级到 unpooled sample。后续若再改 pooled-audio authority，必须重跑 shared timing focused suite。

## 下一检查点

1. 执行 dense fully-keysounded chart、layered BGM、rapid empty-strike 与 live channel change 的人工 checklist，并把结果回交 `P1-G`。
2. 评估 single-sample array contract 是否值得继续下探到 `PausableSkinnableSound` / `SkinnableSound`，还是把现状作为 `J3` 的第一阶段冻结点。
3. 若后续继续触碰 pooled-audio boundary，先回跑 `TestSceneBmsSharedKeysoundTiming` 与完整 `osu.Game.Rulesets.Bms.Tests`，再决定是否扩大修补范围。

## 验证记录

- 2026-05-16：`BmsKeysoundStore`、`BmsLane`、`BmsOrderedHitPolicy` 与 `DrawableBmsHitObject` 已完成首轮 hot-path 收口；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~BmsDrawableRulesetTest"` **58/58** 持续通过，`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。
- 2026-05-16：`BmsKeysoundStore` 的 live channel resize 已改成 non-destructive resize，并新增 `TestSharedKeysoundStoreShrinkDoesNotCutActiveChannelsImmediately` 与 `TestSharedKeysoundStoreShrinkRemovesStoppedDeferredChannels` 两条回归；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~BmsDrawableRulesetTest"` **60/60** 通过。
- 2026-05-16：补上 `TestSceneBmsKeysoundChannelConfigBinding` 后，`KeysoundConcurrentChannels` 已具备 `config -> drawable ruleset -> playfield shared store` 的 direct binding focused coverage；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~TestSceneBmsKeysoundChannelConfigBinding"` **3/3** 通过。
- 2026-05-16：新增 `BmsOrderedHitPolicyTest` 后，scratch stream 的 ordered-hit 两个核心合同已拥有 dedicated focused suite：前一个对象结算后允许命中后一个对象、在 miss window 内直接击打后一个对象会强制把前一个未判对象记为 miss；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~BmsOrderedHitPolicyTest"` **2/2** 通过。
- 2026-05-16：补回 `BmsFilterCriteria` 的“缺失 chart filter stats 不静默过滤、test resolver 可回填”合同后，`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~BmsFilterCriteriaTest"` **4/4** 通过；更宽 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release` 现已恢复并扩展到 **766/766**。
- 2026-05-16：新增 `TestSceneBmsSharedKeysoundTiming` 后，`DrawableBmsHitObject` 命中与 `BmsLane` lane replay 的 same-frame shared-store 请求都已拥有 dedicated owner-level focused regression；同轮还把 `Playfield.GetPooledSample()` 收口为“pool 不可用时回退 `null`，由 `SkinnableSound` 自动降级到 unpooled sample”的安全边界。`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~TestSceneBmsSharedKeysoundTiming"` **3/3** 通过；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release` **774/774** 通过。
- 2026-05-16：完成只读代码审查，范围覆盖 `BmsKeysoundStore`、`DrawableBmsHitObject`、`DrawableBmsHoldNote`、`BmsLane`、`BmsOrderedHitPolicy`、`BmsPlayfield`、`DrawableBmsRuleset` 与 `BmsSoloPlayer` 的相关链路，并据此建立 `P1-J` 文档与执行顺序。