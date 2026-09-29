---
name: reference-bms-keysound-chain
description: BMS 手动与自动键音责任、转谱 LN、Mod 重建与时钟诊断
metadata:
  node_type: memory
  type: reference
---

# BMS 键音链路召回

权威约束：[P1-J CONSTRAINTS](../../doc_md/subline/P1-J/TECHNICAL_CONSTRAINTS.md)；当前态：[P1-J STATUS](../../doc_md/subline/P1-J/DEVELOPMENT_STATUS.md)；解析侧见 P1-K。

## 发声责任诊断

- 先确认本局自动键音设置：关闭时 native key-down/armed timeline 与对象发声；开启时 shared store 按主时钟播放，命中与漏按不推进音频游标。默认手动 autoplay 抑制 lane armed 重音并交给音符发声，自动键音模式则统一交给游标，不能混用两种责任。
- 同文件不同 WAV slot 允许重叠；排查截断看 `KeysoundId`，不能按文件名合并 cut group。通道数/预热数值只查 [资源合同](../../doc_md/subline/P1-J/TECHNICAL_CONSTRAINTS.md#shared-store-与资源合同)，不要恢复用户滑条或每次 Play 全池扫描。
- 转谱 LN 载体继承普通 HoldNote，自动头音经 store；关闭自动键音时仍是 `NodeSamples[0]`，嵌套 head 没有 store 路由。不能把自动清单证明写成手动 LN 已接入，也不能把此缺口写成 HoldNote 未池化。
- BGM/scratch 的 `Samples` 为空是防按键旁路的边界；成因和定位方法见 [[reference_bms_bgm1_pause_keytrigger_bug]]。暂停停止样本不等于长 one-shot 保位续播。

## 地雷与诊断

- NR/HO/IN 会重建 mania 对象，HO/IN 还会删除 BGM/皿 sample-only；自动键音不能只在 Mods 之后扫描 `IHasManiaKeysound`。转换器保存只读音频值快照，每局游标独立；预热必须覆盖快照里的已删除对象声音。当前开关关闭时这些 Mod 的旧音频行为未改。
- 自动键音不能挂在 playable drawable 的 Update 上：早按后对象可能在应发声时刻前已回收。音频游标必须独立；mania Column 的按键反馈也须单独关，不能仅 gate `DrawableNote.PlaySamples()`。
- Player 时间测试中普通推进应驱动实际 `FramedBeatmapClock.Source`，并扣除 `TotalAppliedOffset`；`GameplayClockContainer.Seek` 是显式跳转，会按合同跳过过去的自动声音，不能拿它模拟连续游玩。确定性输入/时差对照的 ManualClock 设 Rate=0，避免插值自行推进；速率行为另作生命周期测试。
- 转谱 HoldNote 不能用非池化自定义嵌套 head；mania `DrawableHoldNote.Update()` 假设池化 head/tail 已建立。
- mania pool 有 base-type fallback，但前提是 `CreateDrawableRepresentation` 返回 null；返回专用 drawable 就绕过池。
- “人声截断/少键”先检查 parser，尤其缺省 `#LNTYPE` 应按 1；不要先改通道池。
- “末端 lane/改键后静音”先检查 parser keymode、timeline 与 post-mod lane，见 [[reference_bms_lane_keysound_timeline_bounds]]、[[reference_bms_lane_rearrangement]]；自动转谱声像按原音频列，不能用重排后的玩法列反推音频漏路由。
- 虚拟轨测试看不见真实发声/静音，音频改动必须真机。
- GC 性能看 gen0:gen1、pause duration 和对象存活；少量中寿命分配也可造成晋升风暴。普通密度问题已收口，50k 先用 `BmsGameplayStallDiagnostics` 取证。

历史误判、旧测试数字和逐日回退只查 P1-J/P1-K CHANGELOG。2026-08-30 C3 前置的最终 focused/full/Release 数字见 [P1-K CHANGELOG](../../doc_md/subline/P1-K/CHANGELOG.md#2026-08-30)。
