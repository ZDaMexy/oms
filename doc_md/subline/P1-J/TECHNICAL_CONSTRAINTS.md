# P1-J 技术约束：BMS gameplay runtime 性能与音频时序治理

> 最后更新：2026-05-18
> 本文件记录 `P1-J` 的硬约束。若实现与本文冲突，先修正文档或代码其中一边，再继续开发。

## 归线约束

1. 本子线属于 Phase 1.x 下的 `P1-J`；主 authority 是 BMS gameplay runtime 的 keysound timing、dense-chart 热路径与 shared audio pool 安全合同，不得回写成 `P1-C` 或 `P1-E` 的主线任务。
2. `P1-C` 只承接判定 / 反馈语义不回归这条从属约束；`P1-E` 只承接真实谱面验校与 checklist 消费。二者都不得再各自长出第二套 runtime hot-path contract。

## runtime / audio authority 约束

1. shared `BmsKeysoundStore` 继续是 BGM / note / LN / lane replay 的唯一 playback pool authority；不得为了“更快”重新长出 per-note、per-lane 或 per-drawable 的独立 sample player。
2. gameplay keysound 播放不得再默认依赖“无条件下一帧调度”作为长期语义；若需要跨线程 marshal，必须是显式、可验证且不引入固定帧级延迟的合同。
3. note hit、BGM event、LN head/tail keysound、lane replay / empty-hit playback 的既有语义必须保持：命中时播放、miss 时跳过 note keysound、BGM 继续走 shared pool、tail miss 不补播尾音。
4. `KeysoundConcurrentChannels` 仍由 `BmsRulesetConfigManager` / `BmsSettingsSubsection` 提供持久配置 authority；但 runtime 改值不能继续以 rebuild-all 作为默认隐式行为，除非文档与 UI 明确声明为 deferred apply。
5. 任何 live channel resize 策略都不得 silently 截断当前音频后又对外宣称“安全即时生效”。
6. core generic replay contract 不属于 `P1-J` 可继续放宽的 surface；`FramedReplayInputHandler.SetFrameFromTime()` 在 frame-stable playback 下仍必须保持 one-boundary-per-call progression。若 dense full autoplay 需要继续优化，只能在 BMS owner side 分流，而不是再修改 core replay stepping semantics。
7. full autoplay 的 keysound prewarm 只允许复用既有 `Playfield` sample pool 与 shared `BmsKeysoundStore` authority，把首次初始化成本前移到安全边界；不得为此引入第二套 retained sample authority、per-note/per-lane 预解码 player，或绕过既有 pooled/unpooled fallback contract。

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
