# P1-J 当前状态：BMS gameplay 性能与音频时序

> 最后更新：2026-09-09（代码与跨线证据同步；产品验证未刷新）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。

## 当前阶段

普通密度 BMS 与转谱-mania 的主要键音故障、帧抖动和开局 gen2 冻结已有修复基线。末端 lane timeline 与 shared-store production proof 已随 P1-A C3/P1-K 闭合；仍开放转谱 LN head 的 shared-store 接入、长 one-shot 保位续播、50k 极端 dense 谱 profile 和真实谱音频人工清单。

## 当前有效合同

- `BmsPlayfield.KeysoundStore` 是 BGM/note/LN/lane replay 的 shared pool owner。
- gameplay keysound same-frame 播放；lane/ordered-hit 热路径已移除首批无谓物化和重复扫描。
- channel 选择为 idle-first，饱和时增长到上限 256；原生默认基线 32，转谱 store floor 128。
- 同一 `KeysoundId` 走 per-WAV cut；不同槽即使同文件也不合并。
- LN tail 不发声；自然 miss 不发声；key-down 的 hit/pressed-poor 按既有合同发声。
- pause/seek 会统一停止 one-shot store 播放，避免样本穿透；这不等价于长 BGM 真正保位恢复。
- full autoplay 走对象级 autoplay + direct-time replay 分流；普通 replay 保留 framed 边界推进。
- 玩家模式已预热 keysound；同样本重触发走 channel fast path，避免 sample-drawable 重建 churn。
- `LaneKeysoundTimelines` 已使用完整 lane count；末端 lane、post-mod 对象/armed timeline/skin `LaneId` 与 shared-store 请求已有 production 回归，不再等待 converter 修复。

## BMS→mania 音频当前态

- BGM/scratch/tap note 走复用的 `BmsKeysoundStore`。
- BGM/scratch sample-only 对象的 `Samples` 为空，避免 mania 按键反馈再次触发；实际键音走 `KeysoundSample`。
- tap note 已池化并具备 per-WAV cut；暂停停 BGM、长 BGM 被 32 通道偷断、bgm1 按 key1 重播均已修复并有历史实机证明。
- gameplay 主 Track 对 BMS 静音但保留时钟 authority；选歌试听只接受 `#PREVIEW`，存量由 backfill 回写。
- 转谱 LN 仍产出普通 mania `HoldNote`，头音经 `NodeSamples[0]` 播放；没有 `IHasManiaKeysound`/cut-group 的 pooled nested head，不能把 tap-note store proof 扩大成 LN 已接入。

## 进度

| 切片 | 状态 |
| --- | --- |
| J1 keysound timing hardening | 完成 |
| J2 lane/ordered-hit hot path | 首轮完成，后续由 profiler 驱动 |
| J3 sample allocation | 主路径完成，array-based 底层合同仍在 |
| J4 live channel safety | 完成；用户设置项已移除，内部 resize 合同保留 |
| J5 focused/dense validation | 自动化具备，人工清单待闭合 |
| J6 转谱音频 | tap/BGM/scratch 主链完成；LN 与长 BGM resume 仍开放 |

## 最近一次验证

- 全局最新产品验证统一见 [mainline STATUS 的“最近一次验证”](../../mainline/DEVELOPMENT_STATUS.md#最近一次验证)；2026-07-16 仅治理文档，未运行产品测试或 Release。
- store/audio/runtime 的本线历史 focused/full 数字与逐日取证统一查 [CHANGELOG.md](CHANGELOG.md)，不冒充当前全局 gate。

## 当前风险与下一步

1. lane timeline：converter 与 production shared-store 自动证据已闭合，继续保留每轨空击/不可见 keysound 的真实谱 smoke；闭门依据见 [P1-K CHANGELOG](../P1-K/CHANGELOG.md#2026-08-30)。
2. 转谱 LN：先用现有 player-level harness 取证，再尝试池化嵌套 head；禁止重走曾导致空 Head 容器崩溃的非池化方案。
3. 50k dense：只有真机重现时才用 `BmsGameplayStallDiagnostics` 区分 gen2、晋升风暴或 render/present；不把普通密度旧问题重新打开。
4. 人工清单：dense fully-keysounded、layered/long BGM、rapid empty-strike、pause/seek，结果回交 P1-G。
5. 长 one-shot 真 pause/resume 仍缺底层能力；当前“边界即停”只能防逃逸，不能宣称保位续播。

## 文档治理验证

2026-09-09：对照 [converter](../../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs)、[真实 shared-store 测试源码](../../../osu.Game.Rulesets.Bms.Tests/TestSceneBmsSharedKeysoundTiming.cs)及 [BMS→mania converter](../../../osu.Game.Rulesets.Bms/Beatmaps/BmsToManiaBeatmapConverter.cs)，移除已完成的 lane 修复待办，保留 LN 与人工/性能边界。本节仅记录源码审查，既有数字仍按原日期引用；全局实测见主线最新验证。
