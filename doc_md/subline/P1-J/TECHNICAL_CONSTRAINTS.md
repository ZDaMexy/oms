# P1-J 技术约束：BMS gameplay runtime 性能与音频时序治理

> 最后更新：2026-05-18
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
