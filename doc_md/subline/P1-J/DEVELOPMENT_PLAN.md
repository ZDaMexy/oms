# P1-J 当前计划：BMS gameplay 性能与音频时序

> 最后更新：2026-07-16
> 当前事实见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，稳定音频合同见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)，已完成修复与取证按日期查 [CHANGELOG.md](CHANGELOG.md)。

## 子线职责

P1-J 只拥有 BMS gameplay runtime 的 keysound timing、shared audio pool、lane/order 热路径与基于 profiler 的 dense-chart 治理。

- P1-K 定义 converter 对象、lane timeline 与 keymode truth。
- P1-C 定义判定/poor 语义；P1-E/P1-G 承接真实谱和人工验收。
- 不把本线扩成全仓音频后端、渲染、选歌或通用性能专项。

## 当前基线

- 原生 BMS 与转谱-mania 的普通密度主要键音、帧抖动和开局冻结故障已收口。
- BGM/scratch/tap note 已走 shared `BmsKeysoundStore`；转谱 LN 仍是开放缺口。
- lane/order 热路径、通道自动增长、per-WAV cut、prewarm、pause/seek stop 与 diagnostics seam 已有稳定合同。
- BMS gameplay beatmap track 保持静音但仍是时钟源；选歌试听只接受 `#PREVIEW`。

完成阶段和误判/回退过程不在 PLAN 重述，统一查 [CHANGELOG](CHANGELOG.md)。

## 当前执行顺序

### 1. 末端 lane keysound runtime proof

依赖：P1-K 先把 `buildLaneKeysoundTimelines()` 上界从 key count 修为 lane count，并以 converter focused 证明 timeline 完整。

1. 共用 P1-A `SV1-3` topology fixture，覆盖 5K K5、7K K7、14K K14/S2。
2. 证明末端 lane 进入同一个 shared store，并在玩家、autoplay、空击/不可见 keysound 路径按现有语义发声。
3. 不只断言 converter DTO 数量；必须有 runtime/playback record 或等价 owner-level proof。
4. 不借本切改变 pool、cut、判定、lane action 或 skin/layout authority。

验收：converter focused 与 runtime proof 同时通过，且每轨 smoke 可交 P1-A/P1-G 复用。

### 2. 转谱 LN keysound 进入 shared store

依赖：现有 tap-note store 路由、player-level playback log/harness 与 mania hold pooling 行为保持可验证。

1. 先用现有 harness 记录当前 LN head 的播放次数、cut group、pause/seek 与 fallback，不直接在生产猜路由。
2. LN head 必须经 store 获得 per-WAV cut；tail 继续静音。
3. 嵌套 head 必须沿 mania 可池化类型接入；禁止恢复会让 `DrawableHoldNote.Head` 为空的非池化自定义 hold drawable。
4. 不能为绕开 pooling 新增长期 per-note/per-lane sample player。
5. 新路径失败时保持当前一次性 LN head 行为，不影响已稳定的 BGM/scratch/tap 路径。

验收：真实转谱 LN 不重复、不静音，pause/seek 不逃逸，tap/BGM/scratch 与原生 mania hold pooling 不回归。

### 3. 50k 极端 dense 谱只按证据治理

触发条件：用户在当前版本真机复现，并提供 `BmsGameplayStallDiagnostics` 日志、谱面范围和可重复操作。

1. 先区分阻塞 gen2、gen0→gen1 晋升风暴、shared store 扫描、alive sample-only drawable、render/present 或其它瓶颈。
2. 给出复现前后相同场景的 frame/GC/allocation/playback 证据，再决定最小 owning abstraction。
3. 任何调度器化、长样本分池、channel floor 调整或对象模型变化必须单独立项，并保护普通密度基线。
4. 无复现或证据不足时保持 backlog，不做泛化 LINQ/对象池/渲染清扫。

验收：改动与单一已证实瓶颈对应，自动 proof 和相同真机场景均改善，普通密度无回归。

### 4. 人工音频清单交 P1-G

1. dense fully-keysounded。
2. layered/long BGM。
3. rapid empty-strike 与 lane armed keysound。
4. pause/seek/retry；明确当前 one-shot 只保证边界停止，不保证长样本保位续播。
5. 原生 BMS 与转谱-mania 的代表谱对照。

P1-J 提供谱面、步骤、期望和自动证据；P1-G 统一记录设备与人工结果。发现缺陷后回 P1-J 修复，不在验收表中长期堆积。

## 验证矩阵

| 改动面 | 最低自动验证 | 额外证明 |
| --- | --- | --- |
| lane timeline/runtime proof | converter focused + lane/store owner proof + BMS relevant/full | 末端 lane 实机 smoke |
| 转谱 LN/store | converter + player-level playback log + mania hold relevant + BMS full | 真实 LN、pause/seek |
| store/channel/cut/prewarm | shared store owner + gameplay timing + BMS full + Release | layered/long BGM |
| hot path/perf | owning regression + BMS full + Release | 同谱同段 profile 前后对照 |

## 明确不做

- 不替换 ManagedBass，不新增默认 audio latency/offset 产品面。
- 不修改 core generic replay stepping 来迁就 BMS autoplay。
- 不重开已修复的普通密度故障，也不把已修复的试听 track 泄漏列为当前缺口。
- 不提前推进 FHS、BSS/MSS、新 gameplay mod 或全键模式扩张。
