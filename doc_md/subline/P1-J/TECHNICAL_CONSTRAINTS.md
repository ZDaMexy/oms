# P1-J 技术约束：BMS gameplay 性能与音频时序

> 最后更新：2026-09-30（有序索引、长条推进、转谱池化与样本维护合同）
> 当前事实见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，事故取证与旧测试数字按日期查 [CHANGELOG.md](CHANGELOG.md)。

## 归线与 authority

1. `BmsPlayfield.KeysoundStore` / shared `BmsKeysoundStore` 是 BGM、note、LN、lane replay 的唯一 playback pool authority；不得长出长期 per-note、per-lane 或 per-drawable sample player。
2. P1-K 拥有 converter object、lane timeline 与 keymode truth；P1-C 拥有判定/poor 语义；P1-E/P1-G 拥有真实谱与人工验收。P1-J 不复制这些 authority。
3. gameplay keysound 必须 same-frame；跨线程 marshal 必须显式、可验证且不能引入固定一帧延迟。

## 原生 BMS 发声合同

以下按键发声规则适用于关闭自动键音的默认模式；开启后的发声责任见下节，判定责任不变。

1. 玩家 key-down 必须发声：clean hit 由 note `PlaySamples` 发声；被判为 pressed-poor/miss 且消费按键时，由 press path 补播该 note keysound；没有 key-down 的自然漏过 miss 静音。clean hit 不得 double。
2. LN tail 一律不自动发声；tail keysound 只可保留在对象/timeline 中用于 armed empty-strike 语义。LN head 与普通 note 遵守同一 shared store/cut 合同。
3. `BmsBeatmap.LaneKeysoundTimelines` 必须覆盖 lane count 全范围。P1-K 构建 timeline，P1-J 守住 5K/7K 最右键、9K 全 lane、14K 右侧末键与 S2 的 runtime 可达及 mod 后 source WAV/target `LaneId` 一致性；现有 converter 与 production proof 一并保留，lane runtime 不得另加补偿 timeline。
4. autoplay 发声必须与 100% 完美游玩逐次等价：自动音符自己发声时，同 lane 的 armed keysound 必须被抑制；玩家空击语义不受影响。守卫包括 `TestAutoPlayNoteSuppressesRedundantLaneKeysound` 与 lane replay 对照。
5. full autoplay 的 BMS owner-side 分流不得破坏 core `FramedReplayInputHandler` 的 one-boundary-per-call 合同，也不得让 replay HUD/key counter 失去输入活动。

## 自动键音合同（2026-09-29）

1. BMS `AutoKeysound` 与 mania `AutoKeysoundForBms` 是独立、默认关闭的声音设置，每局初始化时读取；不创建 Mod、不改成绩分组。后者只作用于 BMS 转谱。回放采用当前声音偏好，不记录或伪造输入，不改写成绩。
2. 开启时由 shared store 按 gameplay 主时钟推进有序音频游标，普通键/皿/LN head/BGM 各播放原有事件一次，不能依赖 drawable 生存期、命中或自然 miss。关闭 note/pressed-poor/lane empty 与 mania note/column feedback 的额外发声，但保留输入传播、判定、血量、计分、灯及失败流程。自动演奏/辅助 Mod 的原判定语义不受影响。
3. LN parent 只投影一个 head；tail、armed invisible、mine 不进入自动清单。BGM/转谱皿旧 drawable 在自动模式只维持 Ignore 生命周期，由游标统一发声。相同时刻保留转换器顺序及每次 WAV slot 事件，禁止按文件名合并；继续使用同一通道池、cut、预热和音量/变速链。
4. BMS→mania 转换器在玩法 Mod 之前保留只读音频值快照，NR/HO/IN 重建/删除对象不能丢音乐；clone 共享只读数据、每局独立游标。自动音乐声像使用转换源音频列和当前声像强度，玩法重排不重写该音频列；默认手动路径仍跟随当前玩法列。自动清单包含 Mod 已删除的 BGM/皿，预热也须覆盖这些样本。不在开关变化时修改共享谱面或 Samples/NodeSamples。
5. 暂停冻结既有通道位置且不播放未来事件，继续时从原位置恢复；seek 停止旧声部并定位到目标时刻，跳过过去事件、保留目标及未来事件，不重建过去已开始的长样本。后退重新经过事件时可再次发声，重试重新定位。普通向前跨帧处理全部到期事件；frame-stable 重复追帧不能重播相同主时钟事件。不消费 visual offset。
6. 必须证明两个模式的无输入发声且仍 miss、提前/晚按/空按不重复、LN head/tail、纯 LN、同输入成绩对照、pause/seek 和原生 mania 不受影响。DTO/游标测试不能代替真实 Player/store 请求证明；真实听感仍需人工验收。

## shared store 与资源合同

1. 通道选择 idle-first；全部通道忙时按需增长到 `MAX_CONCURRENT_CHANNELS=256`，只有达到硬上限仍饱和才允许近似最旧轮转。不得在有空闲通道时偷断声音。
1a. `getNextChannel()` 必须保持 O(1)，不得在每次 `Play()` 时扫描全池；空闲集合可以每帧以 O(N)、零分配方式统一重建，并在 resize 时播种。任何替代结构都须用 dense profile 证明不回归后才能改变该复杂度合同。
2. 原生初始/常驻基线为 `DEFAULT_CONCURRENT_CHANNELS=32`；用户“键音通道数”设置已删除，不得仅为手调上限恢复 UI。转谱 store 的 floor 保持 `Math.Max(32, 128)`。
3. 任何内部 live resize 都必须 non-destructive：调高增量建通道；调低只回收 idle，busy 延后；禁止 rebuild-all 或直接截断当前发声。裁剪的通道必须 dispose 并标记 retired。
4. per-WAV cut 必须按 `KeysoundId/#WAVxx` 槽号分组，不能按文件名。相同槽重触发应在同一 busy channel 干净重启；不同槽即使引用同一文件也不得合并。无 cut-group 的多样本入口不参与 cut。
5. same-sample fast path 可以直接 stop/replay，避免每次重建 sample drawable；切到不同样本或多样本时必须正确失效缓存，不能改变 cut 语义。
6. keysound prewarm 对玩家与 autoplay、原生 BMS 与转谱-mania 对等执行，只复用现有 `Playfield` sample pool 与 shared store；不得建立第二套 retained sample authority。加载期变长是允许的显式取舍，update thread 中冷解码不是。
7. pause 必须通过现有样本通道的零频率调整冻结位置，resume 恢复原频率调整链；不得用 Stop/Play 重建通道来冒充续播。暂停声部仍为 busy，不得被 idle 回收或 resize 裁剪。seek/retry 必须清除旧声部，避免恢复后旧音乐逃逸；任意 seek 不承诺重建已开始长样本。底层实际位置证明与真实设备听感分别记录，前者不能代签后者。
8. 稳定播放/空闲维护不分配临时完成集合；确有多路结束或旧 revision sample 待释放时才物化所需集合。必须先从活动记录移除全部已结束通道，再释放历史 drawable，最后释放 revision lease；不得用零分配目标破坏旧声尾音、热更或 shutdown 的所有权顺序。

## BMS→mania 音频合同

1. P1-K 决定“转出什么对象”，P1-J 决定这些对象在 mania runtime 如何通过 shared store 发声。
2. 转谱 BGM/scratch/tap note 使用 hosted `IManiaKeysoundStore`/`BmsKeysoundStore`；tap note 保持 mania `DrawableNote` 池化，不恢复专用非池化 drawable。
3. sample-only BGM/scratch 对象的 `Samples` 必须为空，真实键音只放 `KeysoundSample`；否则 mania column feedback 会把 BGM/scratch 当下一可玩对象按键触发。converter test 必须同时守住空 `Samples` 与存在 `KeysoundSample`。
4. 转谱 BGM sample-only 对象必须 autoplay 出声；pause 由中心 store 保位冻结，resume 原位继续；seek/retry 清除旧声部，遵守上节资源合同。
5. 转谱 LN tail 必须静音。`BmsConvertedHoldNoteHitObject` 保留头音/slot，继承普通 HoldNote 池化与嵌套行为；自动键音由音频清单统一经 store 播放。关闭自动键音时 pooled `DrawableNote` 仅在对象为 `HeadNote` 时读取父对象的 `IHasManiaKeysound` 并经 store 发声；不更换普通 HeadNote 类型、不让 tail 使用父对象头音。普通 mania 保持原样本路径。禁止非池化自定义 hold drawable 和 per-note sample player。
6. `BmsToManiaKeysoundStoreFactory.Create(IRulesetConfigCache?)` 签名因 mania 反射绑定保持兼容；内部通道 floor 128 不得无 profile/保真替代方案而下调。
7. BMS gameplay 的 `working.Track` 必须静音但继续作为 gameplay clock source：
   - 通过 `Ruleset.PlayBeatmapTrackDuringGameplay` 区分，BMS 为 false，其它 ruleset 默认 true；解析失败 fail-open。
   - 使用 volume adjustment mute，不替换 `MasterGameplayClockContainer` 的 track source。
   - mute 必须加在 `musicController.ResetTrackAdjustments()` 之后，退出 gameplay 时移除并恢复共享试听轨。
   - `TestSceneBmsGameplayTrackMuting` 与 `TestSceneBmsPlayerAudioSemantics` 同时守住静音和时钟。
8. BMS 选歌试听只来自 `#PREVIEW`，并从 `PreviewTime=0` 播放；无 `#PREVIEW` 时 AudioFile 为空。不得恢复“未引用且大于 1MB 音频即试听”的启发式。
9. 存量试听策略由 `BmsPreviewAudioBackfill` 一次性后台收敛：完成标记门控、单次 Realm 快照、解码循环零 Realm、批量写回和进度通知必须保留；不得每次进入选歌重复扫描。
10. BGM/scratch sample-only drawable 使用既有 Column 对象池，加载时注册类型，初始池大小为零并按真实播放窗口增长；不再每对象反射/整谱创建，也不建立新调度器。池化带来的 usage callbacks 不得将纯音频事件发布为可玩 Note 的 skin scene 状态。
11. 仅当 hosted store 实际拥有发声责任时，转谱 tap/head 才跳过普通 sample drawable 准备。Samples/NodeSamples 数据、加载期 shared prewarm、无 store 回退及普通 mania 样本行为保留。

## 热路径与诊断

1. lane/order runtime 不得以每次按键/命中全量枚举容器或重复 `ToArray()` 作为长期默认；优化必须落在 owning abstraction 并保留 detached harness 与真实 runtime 的同一语义。
2. sample materialization 边界尽量唯一；不得为了少量分配删除多样本、BGM、LN 或 lane replay 能力。
3. `BmsGameplayStallDiagnostics` 是长期只读诊断 seam。50k/dense 问题必须先用 stall、GC、allocation、play count 和 frame 证据归因，再改对象池、channel、调度或渲染。
4. 无当前真机复现时不得重开普通密度旧问题，或扩成全仓 LINQ、audio backend、render/present 清扫。
5. 不新增默认 audio latency/offset surface，不推进新 gameplay mod、Phase 2 speed 体系或大范围 HUD/UX。
6. 有序容器快照在成员或 StartTime 改变时失效，完整 entry 索引须追踪未激活对象的时间编辑，并在移除/释放时解绑。空击保持 alive-first 与原 fallback、相同时间顺序和 sample-only 静音语义；加载完成预建索引，回退和成员变化重置游标，不能把整谱首次排序推给玩家首击。
7. LN/CN/HCN 只跳过已经处理的 tick，保留每个嵌套判定对象及血量结果；结果撤销和 pooled Apply 必须重置推进位置。LongNoteMode 每局固定解析，不能在逐帧热路径重复遍历 Mods。

## 测试与发布

当前暂停保位与手动 LN 的实际位置、Player 路由、完整回归及 Release 证据集中见 [2026-09-29 验证记录](../../other/EXPERIENCE_CLOSURE_20260929.md)；历史自动键音结果不能代替后续改动验证。

1. 修改 store/channel/cut/prewarm 至少覆盖 shared store owner、lane/order、playfield binding、pause/seek 与 BMS relevant/full；修改转谱路径加 converter、mania hold/autoplay relevant 与 player-level playback proof。
2. late-empty-poor、empty-poor score/gauge、LN tail、replay-loaded HUD/key counter 等回归不得以“性能优化”为由删除。
3. Release build 仍是代码变更门；dense fully-keysounded、layered/long BGM、rapid empty-strike、pause/seek 的人工结果交 P1-G，但自动缺口不能全部甩给人工。
