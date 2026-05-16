# P1-J 变更日志：BMS gameplay runtime 性能与音频时序治理

> 本文件记录 `P1-J` 相关的验证通过变更，按时间倒序排列。
> 当前进度见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，执行规划见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

---

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