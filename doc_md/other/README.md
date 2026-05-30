# 其他重要参考文档索引

这里收口不会直接替代主线计划，但持续影响方向判断、实现边界与发行方式的重要参考材料。

## 文档清单

- [SKINNING.md](SKINNING.md)：皮肤制作手册、当前 fallback 粒度与未冻结边界。
- [RELEASE.md](RELEASE.md)：发行方式、打包约束与公开 release gate。
- [IIDX_REFERENCE_AUDIT.md](IIDX_REFERENCE_AUDIT.md)：外部 IIDX / LR2 / beatoraja 方向校准与训练反馈基线。
- [BMS_FORMAT_REFERENCE.md](BMS_FORMAT_REFERENCE.md)：BMS / bmson 格式权威参考（channel 编码陷阱、时序、长条、复合规则、控制流与解析审查对照清单），主要服务 [P1-K](../subline/P1-K/) 解析链路审查。
- [BMS_GIMMICK_CHART_RENDERING.md](BMS_GIMMICK_CHART_RENDERING.md)：BMS 演出/Gimmick 谱（如 DEAD SOUL [Revive]）视觉复刻的可行性与架构分析（机理/方案权威来源）；已升级为子线 [P1-L](../subline/P1-L/)，Phase 1（地雷视觉）已落地。**红线：不得改坏正常游玩链路。**
- [UPSTREAM.md](UPSTREAM.md)：上游锁定点、本地 diff 基线与 cherry-pick 风险面。

## 联动要求

1. 这里只承载参考材料，不直接替代 `mainline` 或 `subline` 的计划、状态与约束。
2. 任何参考结论一旦变成正式优先级、正式约束或正式状态，必须同步回写对应的 `mainline` 与 `subline` 文档。
