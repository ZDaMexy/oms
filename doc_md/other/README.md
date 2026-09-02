# 其他重要参考文档索引

这里收口不会直接替代主线计划，但持续影响方向判断、实现边界与发行方式的重要参考材料。

读取原则：只在任务命中对应主题时打开；参考结论一旦成为当前决策，回写相应 mainline/subline 后只在此保留背景和证据，避免出现第二份“当前状态”。

## 文档清单

- [SKINNING.md](SKINNING.md)：皮肤制作手册、当前 fallback 粒度与未冻结边界。
- [GAMEPLAY_SKIN_PUBLIC_CATALOG_V1.md](GAMEPLAY_SKIN_PUBLIC_CATALOG_V1.md)：C4冻结的28项Gameplay Skin V1公共目录、Common/BMS v1作者语法、target/三态/precedence、runtime边界与beatmap-local终态。
- [RELEASE.md](RELEASE.md)：发行方式、打包约束与公开 release gate。
- [IIDX_REFERENCE_AUDIT.md](IIDX_REFERENCE_AUDIT.md)：外部 IIDX / LR2 / beatoraja 方向校准与训练反馈基线。
- [BMS_FORMAT_REFERENCE.md](BMS_FORMAT_REFERENCE.md)：BMS / bmson 格式权威参考（channel 编码陷阱、时序、长条、复合规则、控制流与解析审查对照清单），主要服务 [P1-K 状态](../subline/P1-K/DEVELOPMENT_STATUS.md) 所属解析链路审查。
- [BMS_GIMMICK_CHART_RENDERING.md](BMS_GIMMICK_CHART_RENDERING.md)：BMS 演出/Gimmick 谱（如 DEAD SOUL [Revive]）视觉复刻的背景、机理与方案分析；当前状态、执行和约束以 [P1-L 状态](../subline/P1-L/DEVELOPMENT_STATUS.md) 为准。**红线：不得改坏正常游玩链路。**
- [UPSTREAM.md](UPSTREAM.md)：上游锁定点、本地 diff 基线与 cherry-pick 风险面。
- [SKIN_SYSTEM_RECOVERY_20260710.md](SKIN_SYSTEM_RECOVERY_20260710.md)：2026-06-30 分界后的皮肤系统取证、恢复锚点、撤回范围与重新准入门槛。
- [SKIN_SYSTEM_SV1_0_INVENTORY_20260713.md](SKIN_SYSTEM_SV1_0_INVENTORY_20260713.md)：schema 56 只读取证、定点迁移与 `SV1-0` 闭门证据；不授权重复操作生产数据。
- [SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md](SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)：Skin V1 的架构证据与设计解释；执行顺序和硬约束仍以 P1-A 四件套为准。
- [SKIN_SYSTEM_PROGRESS_AUDIT_20260731.md](SKIN_SYSTEM_PROGRESS_AUDIT_20260731.md)：基于`c53f1e0`的历史阶段快照；其中启动选择竞态已在2026-08-01闭合，不得当作当前状态。
- [SKIN_SYSTEM_C1_COMPLETION_HANDOFF_20260813.md](SKIN_SYSTEM_C1_COMPLETION_HANDOFF_20260813.md)：C1闭门边界与产品价值复核；其中进度只作为交付时快照，当前状态与后续工作门以P1-A四件套为准。
- [SKIN_SYSTEM_C3_LAYOUT_COMPLETION_HANDOFF_20260830.md](SKIN_SYSTEM_C3_LAYOUT_COMPLETION_HANDOFF_20260830.md)：C3的P1-K keymode/lane前置、唯一immutable gameplay layout、全production consumer与C2 revision pair扩展的完成边界和最终验证；当前燃尽仍以P1-A四件套为准。
- [SKIN_SYSTEM_C4_CODEC_MATERIAL_COMPLETION_HANDOFF_20260831.md](SKIN_SYSTEM_C4_CODEC_MATERIAL_COMPLETION_HANDOFF_20260831.md)：C4的public catalog/shared codec/三态resolver、BMS+mania production resolved material、beatmap-local排除、revision triple与最终验证；当前燃尽仍以P1-A四件套为准。
- [SKIN_BMS_NOTE_ANIMATION_MANUAL_GATE.md](SKIN_BMS_NOTE_ANIMATION_MANUAL_GATE.md)：已导入 `.osk` 的 BMS 普通短键编号帧动画、选择切换与 selected 坏包回落的确定性手工素材及验收边界。
- [SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md](SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)：Skin V1 自动 gate 后集中等待用户签收的视觉清单、状态定义与反馈记录。

## 联动要求

1. 这里只承载参考材料，不直接替代 `mainline` 或 `subline` 的计划、状态与约束。
2. 任何参考结论一旦变成正式优先级、正式约束或正式状态，必须同步回写对应的 `mainline` 与 `subline` 文档。
