# P1-J 技术约束：BMS gameplay 性能与音频时序

> 最后更新：2026-07-16
> 当前事实见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，事故取证与旧测试数字按日期查 [CHANGELOG.md](CHANGELOG.md)。

## 归线与 authority

1. `BmsPlayfield.KeysoundStore` / shared `BmsKeysoundStore` 是 BGM、note、LN、lane replay 的唯一 playback pool authority；不得长出长期 per-note、per-lane 或 per-drawable sample player。
2. P1-K 拥有 converter object、lane timeline 与 keymode truth；P1-C 拥有判定/poor 语义；P1-E/P1-G 拥有真实谱与人工验收。P1-J 不复制这些 authority。
3. gameplay keysound 必须 same-frame；跨线程 marshal 必须显式、可验证且不能引入固定一帧延迟。

## 原生 BMS 发声合同

1. 玩家 key-down 必须发声：clean hit 由 note `PlaySamples` 发声；被判为 pressed-poor/miss 且消费按键时，由 press path 补播该 note keysound；没有 key-down 的自然漏过 miss 静音。clean hit 不得 double。
2. LN tail 一律不自动发声；tail keysound 只可保留在对象/timeline 中用于 armed empty-strike 语义。LN head 与普通 note 遵守同一 shared store/cut 合同。
3. `BmsBeatmap.LaneKeysoundTimelines` 必须覆盖 lane count 全范围。P1-K 构建 timeline，P1-J 证明 5K/7K 最右键、14K 右侧末键与 S2 的 runtime 可达；lane runtime 不得另加补偿 timeline。
4. autoplay 发声必须与 100% 完美游玩逐次等价：自动音符自己发声时，同 lane 的 armed keysound 必须被抑制；玩家空击语义不受影响。守卫包括 `TestAutoPlayNoteSuppressesRedundantLaneKeysound` 与 lane replay 对照。
5. full autoplay 的 BMS owner-side 分流不得破坏 core `FramedReplayInputHandler` 的 one-boundary-per-call 合同，也不得让 replay HUD/key counter 失去输入活动。

## shared store 与资源合同

1. 通道选择 idle-first；全部通道忙时按需增长到 `MAX_CONCURRENT_CHANNELS=256`，只有达到硬上限仍饱和才允许近似最旧轮转。不得在有空闲通道时偷断声音。
1a. `getNextChannel()` 必须保持 O(1)，不得在每次 `Play()` 时扫描全池；空闲集合可以每帧以 O(N)、零分配方式统一重建，并在 resize 时播种。任何替代结构都须用 dense profile 证明不回归后才能改变该复杂度合同。
2. 原生初始/常驻基线为 `DEFAULT_CONCURRENT_CHANNELS=32`；用户“键音通道数”设置已删除，不得仅为手调上限恢复 UI。转谱 store 的 floor 保持 `Math.Max(32, 128)`。
3. 任何内部 live resize 都必须 non-destructive：调高增量建通道；调低只回收 idle，busy 延后；禁止 rebuild-all 或直接截断当前发声。裁剪的通道必须 dispose 并标记 retired。
4. per-WAV cut 必须按 `KeysoundId/#WAVxx` 槽号分组，不能按文件名。相同槽重触发应在同一 busy channel 干净重启；不同槽即使引用同一文件也不得合并。无 cut-group 的多样本入口不参与 cut。
5. same-sample fast path 可以直接 stop/replay，避免每次重建 sample drawable；切到不同样本或多样本时必须正确失效缓存，不能改变 cut 语义。
6. keysound prewarm 对玩家与 autoplay、原生 BMS 与转谱-mania 对等执行，只复用现有 `Playfield` sample pool 与 shared store；不得建立第二套 retained sample authority。加载期变长是允许的显式取舍，update thread 中冷解码不是。
7. pause/seek/retry 必须统一停止 one-shot store 播放，防止样本逃逸；当前底层不保证长 one-shot 保位 resume，文档和 UI 不得宣称已支持。

## BMS→mania 音频合同

1. P1-K 决定“转出什么对象”，P1-J 决定这些对象在 mania runtime 如何通过 shared store 发声。
2. 转谱 BGM/scratch/tap note 使用 hosted `IManiaKeysoundStore`/`BmsKeysoundStore`；tap note 保持 mania `DrawableNote` 池化，不恢复专用非池化 drawable。
3. sample-only BGM/scratch 对象的 `Samples` 必须为空，真实键音只放 `KeysoundSample`；否则 mania column feedback 会把 BGM/scratch 当下一可玩对象按键触发。converter test 必须同时守住空 `Samples` 与存在 `KeysoundSample`。
4. 转谱 BGM sample-only 对象必须 autoplay 出声；pause/seek 由中心 store 统一停止。
5. 转谱 LN tail 必须静音。LN head 未来接 store 时须先有 player-level playback log/harness，并使用可池化嵌套 head；禁止非池化自定义 hold drawable和 per-note sample player。
6. `BmsToManiaKeysoundStoreFactory.Create(IRulesetConfigCache?)` 签名因 mania 反射绑定保持兼容；内部通道 floor 128 不得无 profile/保真替代方案而下调。
7. BMS gameplay 的 `working.Track` 必须静音但继续作为 gameplay clock source：
   - 通过 `Ruleset.PlayBeatmapTrackDuringGameplay` 区分，BMS 为 false，其它 ruleset 默认 true；解析失败 fail-open。
   - 使用 volume adjustment mute，不替换 `MasterGameplayClockContainer` 的 track source。
   - mute 必须加在 `musicController.ResetTrackAdjustments()` 之后，退出 gameplay 时移除并恢复共享试听轨。
   - `TestSceneBmsGameplayTrackMuting` 与 `TestSceneBmsPlayerAudioSemantics` 同时守住静音和时钟。
8. BMS 选歌试听只来自 `#PREVIEW`，并从 `PreviewTime=0` 播放；无 `#PREVIEW` 时 AudioFile 为空。不得恢复“未引用且大于 1MB 音频即试听”的启发式。
9. 存量试听策略由 `BmsPreviewAudioBackfill` 一次性后台收敛：完成标记门控、单次 Realm 快照、解码循环零 Realm、批量写回和进度通知必须保留；不得每次进入选歌重复扫描。

## 热路径与诊断

1. lane/order runtime 不得以每次按键/命中全量枚举容器或重复 `ToArray()` 作为长期默认；优化必须落在 owning abstraction 并保留 detached harness 与真实 runtime 的同一语义。
2. sample materialization 边界尽量唯一；不得为了少量分配删除多样本、BGM、LN 或 lane replay 能力。
3. `BmsGameplayStallDiagnostics` 是长期只读诊断 seam。50k/dense 问题必须先用 stall、GC、allocation、play count 和 frame 证据归因，再改对象池、channel、调度或渲染。
4. 无当前真机复现时不得重开普通密度旧问题，或扩成全仓 LINQ、audio backend、render/present 清扫。
5. 不新增默认 audio latency/offset surface，不推进新 gameplay mod、Phase 2 speed 体系或大范围 HUD/UX。

## 测试与发布

1. 修改 store/channel/cut/prewarm 至少覆盖 shared store owner、lane/order、playfield binding、pause/seek 与 BMS relevant/full；修改转谱路径加 converter、mania hold/autoplay relevant 与 player-level playback proof。
2. late-empty-poor、empty-poor score/gauge、LN tail、replay-loaded HUD/key counter 等回归不得以“性能优化”为由删除。
3. Release build 仍是代码变更门；dense fully-keysounded、layered/long BGM、rapid empty-strike、pause/seek 的人工结果交 P1-G，但自动缺口不能全部甩给人工。
