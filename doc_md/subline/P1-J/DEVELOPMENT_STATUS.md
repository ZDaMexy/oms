# P1-J 当前状态：BMS gameplay 性能与音频时序

> 最后更新：2026-09-29（暂停保位与手动转谱 LN 自动证据闭合，保留真实听感门）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。

## 当前阶段

普通密度 BMS 与转谱-mania 的主要键音故障、帧抖动和开局 gen2 冻结已有修复基线。末端 lane timeline 与 shared-store production proof 已随 P1-A C3/P1-K 闭合。默认手动转谱 LN head 已接入 shared store，长 one-shot 暂停保位续播已有实际位置及 Player 自动证据，完整回归未新增失败；50k 极端 dense 谱 profile 仍按当前真机复现触发，真实谱音频清单仍待人工验收。Release 与统一验收结果见 [本轮验证记录](../../other/EXPERIENCE_CLOSURE_20260929.md)。

用户指定的原生 BMS / BMS→mania 自动键音已实现：两个独立默认关闭设置，每局固定，声音按谱面时刻播放，真实操作与成绩仍由原判定链处理。当前执行与退出门见 [PLAN](DEVELOPMENT_PLAN.md#0-用户指定自动键音2026-09-29)，稳定边界见 [自动键音合同](TECHNICAL_CONSTRAINTS.md#自动键音合同2026-09-29)。

## 当前有效合同

- `BmsPlayfield.KeysoundStore` 是 BGM/note/LN/lane replay 的 shared pool owner。
- gameplay keysound same-frame 播放；lane/ordered-hit 热路径已移除首批无谓物化和重复扫描。
- channel 选择为 idle-first，饱和时增长到上限 256；原生默认基线 32，转谱 store floor 128。
- 同一 `KeysoundId` 走 per-WAV cut；不同槽即使同文件也不合并。
- LN tail 不发声；默认手动键音时自然 miss 不发声，key-down 的 hit/pressed-poor 按既有合同发声；开启自动键音后仅声音改由谱面时刻触发。
- pause 将既有 one-shot 通道频率乘数置零、保留播放位置，resume 恢复原速率；seek/retry 仍停止并清除旧声部，不重建跨越目标时刻的过去长 BGM。
- full autoplay 走对象级 autoplay + direct-time replay 分流；普通 replay 保留 framed 边界推进。
- 玩家模式已预热 keysound；同样本重触发走 channel fast path，避免 sample-drawable 重建 churn。
- `LaneKeysoundTimelines` 已使用完整 lane count；末端 lane、post-mod 对象/armed timeline/skin `LaneId` 与 shared-store 请求已有 production 回归，不再等待 converter 修复。

## BMS→mania 音频当前态

- BGM/scratch/tap note 走复用的 `BmsKeysoundStore`。
- BGM/scratch sample-only 对象的 `Samples` 为空，避免 mania 按键反馈再次触发；实际键音走 `KeysoundSample`。
- tap note 已池化并具备 per-WAV cut；暂停停 BGM、长 BGM 被 32 通道偷断、bgm1 按 key1 重播均已修复并有历史实机证明。
- gameplay 主 Track 对 BMS 静音但保留时钟 authority；选歌试听只接受 `#PREVIEW`，存量由 backfill 回写。
- 转谱 LN 仍使用普通 mania `HoldNote` 与嵌套 head 池化；自动模式按原 head WAV slot 经 store 发声，手动模式的 pooled head 现在从父对象读取同一 sample/slot 并进入 store。`NodeSamples` 数据保留，普通 mania 原行为不变；Player 真按键已证明头音一次、原 slot 与尾静音。

## 进度

| 切片 | 状态 |
| --- | --- |
| J1 keysound timing hardening | 完成 |
| J2 lane/ordered-hit hot path | 首轮完成，后续由 profiler 驱动 |
| J3 sample allocation | 主路径完成，array-based 底层合同仍在 |
| J4 live channel safety | 完成；用户设置项已移除，内部 resize 合同保留 |
| J5 focused/dense validation | 自动化具备，人工清单待闭合 |
| J6 转谱音频 | tap/BGM/scratch、手动与自动 LN、长样本暂停保位均有自动证据；保留真实听感门 |

## 最近一次验证

- 本轮暂停保位/手动 LN：实际 WAV/native channel position 三项通过，所在 focused 59 通过；BMS full 2394 通过、29 既有失败、16 跳过，mania relevant 421 通过、4 既有失败。失败身份、完整消息与调用栈均与同日较早自动键音基线一致；Release 成功（0 错误、2 项既有警告），格式 verify 通过，软件门完成。命令与工件集中见 [本轮验证记录](../../other/EXPERIENCE_CLOSURE_20260929.md)，完整回归不宣称全绿。
- 2026-09-29：自动键音 Player 证明无输入仍播放但正常 Miss/零分，固定输入开关对照判定、时差、准确率、成绩、连击与血量一致；两个设置互不影响，普通 mania 保持原音效。
- 同日较早自动键音交付结果保留在 [自动键音验证记录](CHANGELOG.md#bms-与-bmsmania-自动键音)；本轮不以旧工件代替新验证，真实设备听感仍未签收。

## 当前风险与下一步

1. lane timeline：converter 与 production shared-store 自动证据已闭合，继续保留每轨空击/不可见 keysound 的真实谱 smoke；闭门依据见 [P1-K CHANGELOG](../P1-K/CHANGELOG.md#2026-08-30)。
2. 手动转谱 LN：按真实谱复核长条头、同槽截音与静音尾，软件 Player 路由证明不能代签听感。
3. 50k dense：只有真机重现时才用 `BmsGameplayStallDiagnostics` 区分 gen2、晋升风暴或 render/present；不把普通密度旧问题重新打开。
4. 人工清单：dense fully-keysounded、layered/long BGM、rapid empty-strike、pause/seek，结果回交 P1-G。
5. 长样本暂停保位已利用现有 BASS 通道能力实现；仍需真实设备试听。任意 seek 不补播过去已开始的长样本，不能将 pause/resume 能力扩大成 seek 音乐重建。

## 文档治理验证

2026-09-29：本轮已同步暂停保位、seek 清旧声与手动转谱 LN 合同；此前 `4258d4f` 上仅文档审查的过程仍留在 [CHANGELOG](CHANGELOG.md#2026-09-29)，不作为当前缺口描述。统一文档检查与交付验证见 [本轮记录](../../other/EXPERIENCE_CLOSURE_20260929.md)，未新增真实设备听感签收。
