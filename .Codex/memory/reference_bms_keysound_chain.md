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
- 转谱 LN 载体继承普通 HoldNote，自动头音经 store；手动 pooled head 从 `ParentHitObject.HitObject` 读取 `IHasManiaKeysound`，不需要新 head 类型或非池化 drawable。只对 HeadNote 读取父头音，不能让 tail 走同路。不能拿自动清单测试代替手动 Player 真按键证明。
- BGM/scratch 的 `Samples` 为空是防按键旁路的边界；成因和定位方法见 [[reference_bms_bgm1_pause_keytrigger_bug]]。暂停保位依赖既有声部保留，不能重新调用 Stop/Play；seek/retry 则必须真正清除旧声部。

## 地雷与诊断

- 固定 framework `2026.303.0` 的 `SampleChannelBass` 支持零频率保位暂停：`BassRelativeFrequencyHandler` 把频率变零转为 mixer pause，恢复非零继续同通道；paused 且仍有播放请求的 channel 维持 Playing，不能作为 idle 回收。不要因为 `SampleChannel` 没有公开 Seek/Position 就推断原位暂停无底层能力。实际位置测试通过测试端读取 pinned backend 句柄取证，生产不反射底层、不新增播放器。验证结果以 P1-J CHANGELOG 为准，测试代码存在不代表已通过。
- NR/HO/IN 会重建 mania 对象，HO/IN 还会删除 BGM/皿 sample-only；自动键音不能只在 Mods 之后扫描 `IHasManiaKeysound`。转换器保存只读音频值快照，每局游标独立；预热必须覆盖快照里的已删除对象声音。当前开关关闭时这些 Mod 的旧音频行为未改。
- 自动键音不能挂在 playable drawable 的 Update 上：早按后对象可能在应发声时刻前已回收。音频游标必须独立；mania Column 的按键反馈也须单独关，不能仅 gate `DrawableNote.PlaySamples()`。
- Player 时间测试中普通推进应驱动实际 `FramedBeatmapClock.Source`，并扣除 `TotalAppliedOffset`；`GameplayClockContainer.Seek` 是显式跳转，会按合同跳过过去的自动声音，不能拿它模拟连续游玩。确定性输入/时差对照的 ManualClock 设 Rate=0，避免插值自行推进；速率行为另作生命周期测试。
- 音频位置 visual test 的清理用 `[TearDownSteps]` 加末尾步骤；NUnit `[TearDown]` 早于框架 `AfterTest` 的 `RunTestBlocking`，若其中 Schedule(Clear)，会在实际步骤前拆掉场景，使空通道集合的 All 就绪检查误过、时钟启动调度永远不执行。出现 running 超时先核查场景生命期，不能删时钟/实际位置断言规避。
- 转谱 HoldNote 不能用非池化自定义嵌套 head；mania `DrawableHoldNote.Update()` 假设池化 head/tail 已建立。
- mania pool 有 base-type fallback，但前提是 `CreateDrawableRepresentation` 返回 null；返回专用 drawable 就绕过池。
- “人声截断/少键”先检查 parser，尤其缺省 `#LNTYPE` 应按 1；不要先改通道池。
- “末端 lane/改键后静音”先检查 parser keymode、timeline 与 post-mod lane，见 [[reference_bms_lane_keysound_timeline_bounds]]、[[reference_bms_lane_rearrangement]]；自动转谱声像按原音频列，不能用重排后的玩法列反推音频漏路由。
- 虚拟轨或 store 请求计数只能证明调度/路由；实际 WAV/native position 可证明后端保位，但实际设备听感仍须人工验收，不能互相代签。
- GC 性能看 gen0:gen1、pause duration 和对象存活；少量中寿命分配也可造成晋升风暴。普通密度问题已收口，50k 先用 `BmsGameplayStallDiagnostics` 取证。

历史误判、旧测试数字和逐日回退只查 P1-J/P1-K CHANGELOG。2026-08-30 C3 前置的最终 focused/full/Release 数字见 [P1-K CHANGELOG](../../doc_md/subline/P1-K/CHANGELOG.md#2026-08-30)。
