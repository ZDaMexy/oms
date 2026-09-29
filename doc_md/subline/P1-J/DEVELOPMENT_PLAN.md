# P1-J 当前计划：BMS gameplay 性能与音频时序

> 最后更新：2026-09-29（暂停保位与手动 LN 自动证据闭合，活动计划保留人工门）
> 当前事实见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，稳定音频合同见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)，已完成修复与取证按日期查 [CHANGELOG.md](CHANGELOG.md)。

## 子线职责

P1-J 只拥有 BMS gameplay runtime 的 keysound timing、shared audio pool、lane/order 热路径与基于 profiler 的 dense-chart 治理。

- P1-K 定义 converter 对象、lane timeline 与 keymode truth。
- P1-C 定义判定/poor 语义；P1-E/P1-G 承接真实谱和人工验收。
- 不把本线扩成全仓音频后端、渲染、选歌或通用性能专项。

## 当前基线

- 原生 BMS 与转谱-mania 的普通密度主要键音、帧抖动和开局冻结故障已收口。
- BGM/scratch/tap note 与默认手动转谱 LN head 均已走 shared `BmsKeysoundStore`，Player 路由和回归证据已闭合。
- lane/order 热路径、通道自动增长、per-WAV cut、prewarm 与 diagnostics seam 已有稳定合同；pause 保位冻结，seek/retry 清除旧声部。
- BMS gameplay beatmap track 保持静音但仍是时钟源；选歌试听只接受 `#PREVIEW`。
- 完整 lane timeline、末端 lane 与 mod 后 shared-store production proof 已随 C3/P1-K 闭合；后续只保留回归和真实谱 smoke，证据见 [P1-K CHANGELOG](../P1-K/CHANGELOG.md#2026-08-30)。

完成阶段和误判/回退过程不在 PLAN 重述，统一查 [CHANGELOG](CHANGELOG.md)。

## 当前执行顺序

暂停保位与手动 LN 的实现规划已归档 [CHANGELOG](CHANGELOG.md#暂停保位与手动转谱长条声音闭环)，自动证据、完整失败对照与 Release 结果集中见 [验证记录](../../other/EXPERIENCE_CLOSURE_20260929.md)。本线剩余体验门如下。

### 0. 用户指定：自动键音（2026-09-29）

软件已交付；设置和播放边界以 [自动键音合同](TECHNICAL_CONSTRAINTS.md#自动键音合同2026-09-29)为准，此项仅保留真实听感验收。

软件步骤与验证归档见 [CHANGELOG](CHANGELOG.md#bms-与-bmsmania-自动键音)。剩余人工门：在原生 BMS 与 BMS→mania 分别试听 fully-keysounded、长条/皿、同槽连续音、密集和弦、暂停长 BGM，并对照开关听感；记录设备与谱面，软件证明不得代签。

### 1. 手动转谱长条与长伴奏实机验收

用真实谱确认手动长条头不重复、不静音，松开尾部不额外发声；同槽长条/短键/BGM 重触发维持原截音效果。长伴奏中途暂停应静音，继续后从原位置接着唱；反复暂停及变速也须试听。seek/retry 应清除旧音乐，不承诺任意 seek 补回过去已开始的长样本。

记录版本、谱面、设备、模式、自动键音开关与操作步骤，结果回交 P1-G；不以软件路由或原位暂停测试代签真实听感。

### 2. 50k 极端 dense 谱只按证据治理

触发条件：用户在当前版本真机复现，并提供 `BmsGameplayStallDiagnostics` 日志、谱面范围和可重复操作。

1. 先区分阻塞 gen2、gen0→gen1 晋升风暴、shared store 扫描、alive sample-only drawable、render/present 或其它瓶颈。
2. 给出复现前后相同场景的 frame/GC/allocation/playback 证据，再决定最小 owning abstraction。
3. 任何调度器化、长样本分池、channel floor 调整或对象模型变化必须单独立项，并保护普通密度基线。
4. 无复现或证据不足时保持 backlog，不做泛化 LINQ/对象池/渲染清扫。

验收：改动与单一已证实瓶颈对应，自动 proof 和相同真机场景均改善，普通密度无回归。

### 3. 人工音频清单交 P1-G

1. dense fully-keysounded。
2. layered/long BGM。
3. rapid empty-strike 与 lane armed keysound，覆盖 5K/7K 末键、9K 全 lane、14K K14/S2 及 mod 后目标 lane；复用已完成的 production proof，补真实谱听感。
4. pause/resume 的长样本保位与 seek/retry 清旧声分别试听，不混用两种承诺。
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
