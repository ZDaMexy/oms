# P1-J 当前状态：BMS gameplay 性能与音频时序

> 最后更新：2026-07-16（文档健康治理；功能状态未改变）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。

## 当前阶段

普通密度 BMS 与转谱-mania 的主要键音故障、帧抖动和开局 gen2 冻结已收口。当前只保留四个活动缺口：P1-K 修正后的末端 lane keysound runtime proof、转谱 LN 键音池化、50k 极端 dense 谱 profile、真实谱音频人工清单。

## 当前有效合同

- `BmsPlayfield.KeysoundStore` 是 BGM/note/LN/lane replay 的 shared pool owner。
- gameplay keysound same-frame 播放；lane/ordered-hit 热路径已移除首批无谓物化和重复扫描。
- channel 选择为 idle-first，饱和时增长到上限 256；原生默认基线 32，转谱 store floor 128。
- 同一 `KeysoundId` 走 per-WAV cut；不同槽即使同文件也不合并。
- LN tail 不发声；自然 miss 不发声；key-down 的 hit/pressed-poor 按既有合同发声。
- pause/seek 会统一停止 one-shot store 播放，避免样本穿透；这不等价于长 BGM 真正保位恢复。
- full autoplay 走对象级 autoplay + direct-time replay 分流；普通 replay 保留 framed 边界推进。
- 玩家模式已预热 keysound；同样本重触发走 channel fast path，避免 sample-drawable 重建 churn。

## BMS→mania 音频当前态

- BGM/scratch/tap note 走复用的 `BmsKeysoundStore`。
- BGM/scratch sample-only 对象的 `Samples` 为空，避免 mania 按键反馈再次触发；实际键音走 `KeysoundSample`。
- tap note 已池化并具备 per-WAV cut；暂停停 BGM、长 BGM 被 32 通道偷断、bgm1 按 key1 重播均已修复并有历史实机证明。
- gameplay 主 Track 对 BMS 静音但保留时钟 authority；选歌试听只接受 `#PREVIEW`，存量由 backfill 回写。

## 进度

| 切片 | 状态 |
| --- | --- |
| J1 keysound timing hardening | 完成 |
| J2 lane/ordered-hit hot path | 首轮完成，后续由 profiler 驱动 |
| J3 sample allocation | 主路径完成，array-based 底层合同仍在 |
| J4 live channel safety | 完成；用户设置项已移除，内部 resize 合同保留 |
| J5 focused/dense validation | 自动化具备，人工清单待闭合 |
| J6 转谱音频 | tap/BGM/scratch 主链完成；LN 与长 BGM resume 仍开放 |

## 当前验证

- 全局最新产品验证统一见 [mainline STATUS 的“最近一次验证”](../../mainline/DEVELOPMENT_STATUS.md#最近一次验证)；2026-07-16 仅治理文档，未运行产品测试或 Release。
- store/audio/runtime 的本线历史 focused/full 数字与逐日取证统一查 [CHANGELOG.md](CHANGELOG.md)，不冒充当前全局 gate。

## 当前风险与下一步

1. lane timeline 边界：P1-K 当前用 key count 过滤，可能丢 5K/7K 最右键及 14K 右侧末键/Scratch2；converter 修复后，本线补每轨空击/不可见 keysound 实机 smoke。
2. 转谱 LN：先用现有 player-level harness 取证，再尝试池化嵌套 head；禁止重走曾导致空 Head 容器崩溃的非池化方案。
3. 50k dense：只有真机重现时才用 `BmsGameplayStallDiagnostics` 区分 gen2、晋升风暴或 render/present；不把普通密度旧问题重新打开。
4. 人工清单：dense fully-keysounded、layered/long BGM、rapid empty-strike、pause/seek，结果回交 P1-G。
5. 长 one-shot 真 pause/resume 仍缺底层能力；当前“边界即停”只能防逃逸，不能宣称保位续播。
