---
name: reference-bms-stopmotion-bypass
description: STOP/极端 BPM 演出谱 D(t) 滚动、base BPM 标定与 IScrollingInfo 注入地雷
metadata:
  node_type: memory
  type: reference
---

# Stop-motion scroll 诊断

实现与模式合同见 [P1-L CONSTRAINTS](../../doc_md/subline/P1-L/TECHNICAL_CONSTRAINTS.md)，当前剩余校准/人工门只读 [STATUS](../../doc_md/subline/P1-L/DEVELOPMENT_STATUS.md) / [PLAN](../../doc_md/subline/P1-L/DEVELOPMENT_PLAN.md)。历史实谱观察按 DEAD SOUL / stop-motion 查 [CHANGELOG](../../doc_md/subline/P1-L/CHANGELOG.md)，不作为本轮验证。

## 时序与滚动不要修到一起

- BmsScrollProfile 是分段线性 D(t)，在 converter 的既有 time-walk 旁累计原始未 clamp 的 BPM/STOP/measure/scroll 距离；STOP 段 dD=0，极端 BPM 陡斜率。它不进 HitObjects，判定仍使用 StartTime。
- 距离先以 scroll-weighted beats 累计，完整 walk 后再乘 baseBeatLength；不要在未知全局 base BPM 时逐段改变基准。
- BmsStopMotionScrollAlgorithm 以 D(t) 替换 constant scroll 的 chart time；正常基准段 D≈t。
- BmsScrollingInfo 包住 base IScrollingInfo，在 BmsPlayfield.CreateChildDependencies 重新 CacheAs<IScrollingInfo>；Direction/TimeRange透传，未启用时 Algorithm 逐实例跟随 base。它绕过 shared timing clamp，不修改 shared TimingControlPoint 或 ScrollingHitObjectContainer。
- 无 base IScrollingInfo 的 parent-less fixture 保持原行为；要验证真实注入需 Player-based scene，单测构造算法不能证明 DI 生效。

## DEAD SOUL 标定坑

历史实谱诊断中 GetMostCommonBeatLength 得到 6（BPM 10000），而不是基准 BPM 132 对应的约454.5ms：STOP/极端 BPM 的时长权重与 clamp 会支配该统计。将 baseBeatLength 改成 6 来“对齐”会重现常速段挤压。

BmsScrollProfile 的基准应来自 raw BPM 的 non-STOP 时长。Normal Hi-Speed 的 modeScale=1，不借 GetMostCommonBeatLength 缩放，基准132段因此可忠实；Floating/Classic 的绝对标定与反向滚动不能从 Normal 结果推断完成，进度回 P1-L。

## 门控与相关复现

- 用户决定默认 Auto；Off 是显式回退。检测使用 MaxSlope/FrozenFraction，正常/中度 soflan 不应轻易触发；阈值只在现行合同/代码维护，调整必须复核正常谱回归，不能在 memory 固化第二份常数表。
- OFF 测试要看 BmsScrollingInfo 跟随 base 的实例，不只比较数值；ON/AUTO 还需真实 DI 与实谱视觉。
- mine 最末 lane 被丢弃的旧故障是 GetKeyCount 与含 scratch 的 GetLaneCount 混用；统一边界见 [[reference_bms_lane_keysound_timeline_bounds]]。7K 的最后普通键 index=7，合法 lane count=8。
- 逐帧视觉、其它 Hi-Speed 标定、负向/反向滚动与极端谱性能各有自己的门；初次“看起来正确”不替代这些证据。
