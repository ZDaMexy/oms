# P1-J 技术约束：BMS gameplay runtime 性能与音频时序治理

> 最后更新：2026-06-08（新增第 11 条：转谱 sample-only autoplay 对象 `Samples` 必空，键音只放 `KeysoundSample`——防 mania 按键音效反馈触发 BGM/scratch 键音；第 10 条已收口结构化，逐日调查史移交 CHANGELOG）
> 本文件记录 `P1-J` 的硬约束。若实现与本文冲突，先修正文档或代码其中一边，再继续开发。

## 归线约束

1. 本子线属于 Phase 1.x 下的 `P1-J`；主 authority 是 BMS gameplay runtime 的 keysound timing、dense-chart 热路径与 shared audio pool 安全合同，不得回写成 `P1-C` 或 `P1-E` 的主线任务。
2. `P1-C` 只承接判定 / 反馈语义不回归这条从属约束；`P1-E` 只承接真实谱面验校与 checklist 消费。二者都不得再各自长出第二套 runtime hot-path contract。

## runtime / audio authority 约束

1. shared `BmsKeysoundStore` 继续是 BGM / note / LN / lane replay 的唯一 playback pool authority；不得为了“更快”重新长出 per-note、per-lane 或 per-drawable 的独立 sample player。
2. gameplay keysound 播放不得再默认依赖“无条件下一帧调度”作为长期语义；若需要跨线程 marshal，必须是显式、可验证且不引入固定帧级延迟的合同。
3. note hit、BGM event、LN head keysound、lane replay / empty-hit playback 的语义合同：**玩家按键（key-down）必出声**——命中走 note `PlaySamples`（Hit 状态），而被判为 POOR/miss 且消费了按键的玩家按键（含 LN head）改为在 key-down 时由 `PlayKeysoundFromPress()` 直接补播该 note 的 keysound（对齐 IIDX/LR2/beatoraja 的"按键必出声"，修复此前 pressed-POOR 静音）；**未按键的自然漏过 miss 仍静音**（无 key-down 即无声）；BGM / autoplay 继续在 auto 命中时走 shared pool。clean hit 不得因此 double（只有非命中 press 才显式补播）。
3a. **LN tail 一律不发声**（`DrawableBmsHoldNoteTail.PlaySamples()` 重写为空，含 release / autoplay）——对齐 LR2/beatoraja「长条只头发声」。LNTYPE1 长条尾对象常重复头 WAV，若播放会与头叠成 double（实测 GOODBOUNCE scratch 长条 → "stomp your fee feet"），叠加 per-WAV cut 还会掐断头。尾对象的 keysound 仍保留在 object 模型（`TailKeysoundSample` / `GetSamples()`）以 **arm 空击 keysound 时间线**（`BmsBeatmap.LaneKeysoundTimelines`），只是不再自动 auto-play。
4. `KeysoundConcurrentChannels` 仍由 `BmsRulesetConfigManager` / `BmsSettingsSubsection` 提供持久配置 authority；但 runtime 改值不能继续以 rebuild-all 作为默认隐式行为，除非文档与 UI 明确声明为 deferred apply。
5. 任何 live channel resize 策略都不得 silently 截断当前音频后又对外宣称“安全即时生效”。
6. core generic replay contract 不属于 `P1-J` 可继续放宽的 surface；`FramedReplayInputHandler.SetFrameFromTime()` 在 frame-stable playback 下仍必须保持 one-boundary-per-call progression。若 dense full autoplay 需要继续优化，只能在 BMS owner side 分流，而不是再修改 core replay stepping semantics。
7. full autoplay 的 keysound prewarm 只允许复用既有 `Playfield` sample pool 与 shared `BmsKeysoundStore` authority，把首次初始化成本前移到安全边界；不得为此引入第二套 retained sample authority、per-note/per-lane 预解码 player，或绕过既有 pooled/unpooled fallback contract。
8. `BmsKeysoundStore` 的通道分配必须 **idle-first**：仍有空闲通道时不得回收正在播放的通道（避免在远低于复音上限时就提前截断长样本）；只有在全部通道繁忙（真正复音饱和）时才允许按轮转偷取近似最旧者。该选择不得回退成"每次触发全表扫描"——空闲集每帧重建（`reclaimIdleChannels`，O(N) 读、无分配），`getNextChannel()` 保持 O(1)，以守住 dense-chart 热路径。shrink 裁剪通道时必须真正 dispose 并标记 retired，不得留下脱挂未释放的 sound drawable。
9. `BmsKeysoundStore` 实现 **per-WAV cut（每键音单声部）**，且**必须按 BMS WAV 槽号（#WAVxx / `KeysoundId`）归组，不得按文件名**：同一槽在仍发声时被再次触发，必须复用其所在通道令其干净重启（掐断前一实例），而不是占用第二个通道叠加副本——对齐 BM98/LR2/beatoraja。**关键红线**：不同槽即使指向同一音频文件也**不得**合并 cut 组——谱师常把同一文件挂到多个 #WAV 槽专门用来自重叠（hi-hat/拍手等），按文件名归组会错误掐断它们。映射 `activeSampleChannels` 以 `int` 槽号为键、`Play(sample, balance, int cutGroup)` 传入，复用前提为"该通道仍 busy 且 `CurrentCutGroup == cutGroup`"，陈旧项自然回退到 `getNextChannel()`；无槽号入口（`Play(sample, balance)` / 多样本数组）不参与 cut（`CurrentCutGroup = null`）。槽号在播放链由 note/head/BGM 的 `KeysoundId`、空击 armed 由 `BmsLaneKeysoundEntry.KeysoundId` 提供。注意：同一槽被谱面重复排布时**后一次必定掐断前一次**（与参考实现一致），属预期。
10. **`BMS -> mania` 转谱 BGM/scratch/note 的 mania-runtime 呈现保真与性能归本子线 J6**（与 P1-K K11 的"转谱对象语义"分线）。绑定约束：
    - **必须用复用的 `BmsKeysoundStore`**（非对象池）：E（暂停停 BGM）必须有中心化 pause-aware store 才能解（一次性样本不随暂停停），对象池只能帮 D。转谱 BGM/scratch/note 经该 store 播放，pause/seek 统一停。
    - **转谱 BGM sample-only 对象必须在 mania 游玩 autoplay 出声**：不得因 mania 无原生 `BmsKeysoundStore` 就让 BGM 静音（纯键音 BMS 的 mania 转谱音频须与 BMS 原生一致）。
    - **mania 转谱 LN 尾必须静音**（同第 3a 条）：转谱器（K11）不得把尾 keysound 写进 `HoldNote.NodeSamples[1]`，否则 `TailNote` release 会播它、对 LNTYPE1 复用头 WAV 的谱复现 double。
    - **note/LN head 的 per-WAV cut 应经 store 获得**（否则同槽在 store(BGM/scratch) 与 mania 采样两路径间、或同槽连触间无法互掐 → BMS 原生没有的重复）。**再尝试约束**：(a) 不得用非池化自定义嵌套 hold drawable（`DrawableHoldNote.Update()→Head` 空容器必崩；嵌套 head 须池化类型）；(b) 不得靠静态推理在生产里试 store 路由——须先有 player-level 集成测试 + 运行时日志（已落地：`BmsKeysoundStore.EnablePlaybackLogForTesting` + `KeysoundPlaybackRecord` + `TestSceneBmsToManiaKeyNoteStoreRouting`）；(c) 不得为挽回池化新长出 per-note/per-lane 独立 sample player（沿用第 1 条）。
    - **当前态**：tap-note→store **已转正生产默认**（`BmsConvertedKeyNoteHitObject`/`DrawableBmsConvertedKeyNote`，`ShouldHost` 含 KEY 音符），per-WAV cut 跨 BGM↔KEY+KEY↔KEY(tap) 生效。🔴 **非池化 perf**：mania 精确类型池化 → `Note` 子类必非池化、每个转谱 tap 音符一个非池化 drawable，dense(D) 可能加压，pooling-preserving 替代（mania core 让池化 `DrawableNote` 走 shared store、不碰第 1 条）= perf 回归时正解。**LN 部分仍后置**（须池化嵌套头）。
    - **转谱 store 通道 floor 128**：`BmsToManiaKeysoundStoreFactory.Create(IRulesetConfigCache?)` 设 `ConcurrentChannels = Math.Max(config 的 KeysoundConcurrentChannels, 128)`——修长 BGM（如 `bgm1`，整首单次触发、占通道最久）被 32 通道饱和轮转偷断（实测峰值 27–36>32）。转谱 store 专播 BGM 自动层 + scratch（必须完整的背景音乐、非玩家 polyphony 预算），floor 远超峰值 → 长 BGM 永不被偷。治标近似；治本（长样本不进可偷池 / BGM 与 keysound 分池）碰第 8 条 dense hot-path 红线、后置。BMS 原生 store 未动；prewarm 必须复用 core `Playfield.PrepareSamplePool`、不得引入第二套 retained sample authority（沿用第 7 条）。
    - **D（dense 极端谱高密段）仍极度缓慢**：共享 store 已排除「音频对象数」为主因，真瓶颈（疑 drawable 数量 / 转换链 / 渲染）待 profile，后置。
    - 逐日落地与两次回退、误判作废过程见 CHANGELOG 2026-06-01~06-08。

11. **`BMS -> mania` 转谱的 sample-only autoplay 对象（`BmsConvertedBgmSampleHitObject` / `BmsConvertedScratchSampleHitObject`）必须保持 `Samples` 为空**，其键音只放在 `KeysoundSample`（经 shared `BmsKeysoundStore` 自动发声）。**红线起因（2026-06-08 用户实测确诊并修复）**：mania `Column.OnPressed` 每次按键都调 `GameplaySampleTriggerSource.Play()`（按键音效反馈），它播放**本列下一个对象的 `Samples`**，用自己一池非循环、不受 store 暂停管的 `PausableSkinnableSound`。这些 sample-only 对象被钉在可玩列（BGM→column 0、scratch→其锚定列），若其 `Samples` 携带键音，则**按该列的键就会经反馈播出 BGM/scratch 键音**——`bgm1` 被钉在 column 0，按 key1 即反复触发 bgm1、重叠、且绕开 store 与暂停（"按 key1 触发 bgm1 / 胡乱按键长音重叠 / 暂停不停"）。因此转谱器**不得**把这些对象的键音写进 `Samples`（写进去即复现该 bug）；它们经 store 用 `KeysoundSample` 自动播放，`Samples` 对其实际发声是多余的。回归守卫见 `BmsToManiaBeatmapConverterTest`（断言 BGM/scratch 的 `Samples` 为空、键音在 `KeysoundSample`）。**连带后置**：`prewarmConvertedKeysounds` 遍历 `Samples` 预热，置空后 BGM/scratch 不再被预热（仅 `ModAutoplay` 路径受影响、store 仍按需加载，影响小）；若日后需恢复 BGM/scratch 预热，须经 `KeysoundSample` 路径而非把键音放回 `Samples`。**注意区分**：可玩 KEY note（`BmsConvertedKeyNoteHitObject`）/ LN head 仍保留 `Samples`（按键反馈播下一个真实 KEY 音是 BMS-like 的、可接受），只有 autoplay sample-only 对象必须空 `Samples`。

## 性能与热路径约束

1. `BmsLane.shouldTriggerEmptyPoor()` 与 `BmsOrderedHitPolicy.getParticipatingHitObjects()` 不得长期维持“每次按键/命中都全枚举容器对象”作为 runtime 热路径默认实现。
2. 优化 lane/order hot path 时，不得破坏 `BEATORAJA` / `LR2` 的 late-empty-poor 差异语义，也不得让 detached test harness 和真实 runtime 走两套互相漂移的判定 authority。
3. `DrawableBmsHitObject`、`BmsLane` 与 `BmsKeysoundStore` 之间的 sample materialize 边界必须尽量唯一；dense-chart 热路径不得长期保留双重 `ToArray()` 与单元素数组的重复分配。
4. `P1-J` 只处理已确认的 BMS gameplay hot path；不得把本专题扩大成全仓库渲染、窗口模式、选歌性能或任意 unrelated allocation 清扫。
5. full autoplay 路径的性能补丁不得破坏 `ReplayPlayer` 当前仍需消费的 replay-loaded surface；HUD / key counter / replay statistics frame 输入若仍由 replay path 提供，就必须在优化后继续保持可见和可验证。

## 产品面与配置约束

1. 不得借 `P1-J` 顺手新增默认对用户开放的 audio latency / offset product surface；BMS 当前主 timing-correction 路径仍以视觉 presentation 调整为主。
2. settings tooltip 继续保持“低值更易截断、高值成本更高”的表述，不得暗示“高值一定更好”；若 runtime apply 语义改变，tooltip 与主线文档必须同步更新。
3. 本专题不负责引入新的 gameplay mod、Phase 2 speed 体系、全键模式扩张或大范围 HUD/UX 重构。

## 测试与发布约束

1. 至少补齐三层 focused coverage：shared store owner-level 行为、lane/order regression、config->playfield-store binding；不要等代码全做完再临时补测试。
2. 既有 late-empty-poor、empty-poor gauge/score、LN tail keysound 与 related regression 不得因为“性能优化”而被跳过或删除。
3. Release build 继续是子线门槛；本专题不能以“只改性能、不改功能”为理由绕过 build gate。
4. dense fully-keysounded chart、layered BGM、rapid empty-strike 与 live channel resize 的最终人工确认继续后置到 `P1-G`，但不得把 automation 缺口全部甩给人工验收兜底。
5. 触碰 BMS full autoplay 专用路径时，至少要保住一条 player-level proof：回放必须能完成、非忽略判定仍为 `Perfect`，并且 replay-loaded HUD / key-counter surfaces 继续能观察到 replay activity。
