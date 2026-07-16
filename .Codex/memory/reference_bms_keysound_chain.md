---
name: reference-bms-keysound-chain
description: BMS 键音稳定合同、转谱音频现状和性能/隐藏发声地雷
metadata:
  node_type: memory
  type: reference
---

# BMS 键音链路召回

权威约束：[P1-J CONSTRAINTS](../../doc_md/subline/P1-J/TECHNICAL_CONSTRAINTS.md)；当前态：[P1-J STATUS](../../doc_md/subline/P1-J/DEVELOPMENT_STATUS.md)；解析侧见 P1-K。

## 四条稳定合同

1. **idle-first + 饱和增长**：`BmsKeysoundStore` 基线 32，繁忙时增长到 256，达到上限才轮转；不要恢复用户通道数滑条或每次 Play 全池扫描。
2. **key-down 必出声**：clean hit 与被消费的 pressed-poor/miss 发对应键音；自然 miss 静音。
3. **per-WAV cut 按 `KeysoundId` 槽号**：同槽重触发 cut，不按文件名合并；同文件不同槽允许自重叠。
4. **LN tail 静音**：头发声，尾不发；尾 sample 只可用于 armed timeline。

autoplay 必须等价于 100% 完美游玩。自动音符存在时 lane armed 键音要抑制，发声交给音符，避免双触发。

## BMS→mania 当前态

- BGM/scratch/tap note 走 shared `BmsKeysoundStore`；tap note 经 mania 接口走基类池化 drawable。
- BGM/scratch sample-only 的 `Samples` 必须为空，键音只放 `KeysoundSample`，否则会被 mania `GameplaySampleTriggerSource` 按键反馈误播。详见 [[reference_bms_bgm1_pause_keytrigger_bug]]。
- 转谱 store floor 128；玩家模式预热全部 keysound；同样本重触发走 channel fast path。
- 已闭合：pause 边界停播、长 BGM 被 32 通道偷断、tap per-WAV cut、bgm1 按键误触发、普通密度帧抖动与冷解码 gen2 冻结。
- 仍开放：转谱 LN 池化嵌套头、长 one-shot BGM 真 pause/resume、50k 极端 dense profile。

## 地雷与诊断

- 转谱 HoldNote 不能用非池化自定义嵌套 head；mania `DrawableHoldNote.Update()` 假设池化 head/tail 已建立。
- mania pool 有 base-type fallback，但前提是 `CreateDrawableRepresentation` 返回 null；返回专用 drawable 就绕过池。
- “人声截断/少键”先检查 parser，尤其缺省 `#LNTYPE` 应按 1；不要先改通道池。
- 隐藏发声不经过 store/hit-object 时，在最底层 `PoolableSkinnableSample.Play()` 用文件名过滤 + stack trace 定位。
- 虚拟轨测试看不见真实发声/静音，音频改动必须真机。
- GC 性能看 gen0:gen1、pause duration 和对象存活；少量中寿命分配也可造成晋升风暴。普通密度问题已收口，50k 先用 `BmsGameplayStallDiagnostics` 取证。

历史误判、旧测试数字和逐日回退只查 P1-J/P1-K CHANGELOG。
