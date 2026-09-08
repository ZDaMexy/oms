# 参考与证据索引

当前事实与执行门从[子线路由](../subline/README.md)读取。这里按用途区分使用说明、恢复边界和历史证据；日期报告中的状态只代表当时，不覆盖当前 STATUS/CONSTRAINTS。

## 使用说明与主题参考

- [SKINNING.md](SKINNING.md)：皮肤作者手册，区分公开 ABI、legacy 兼容与未交付能力。
- [GAMEPLAY_SKIN_PUBLIC_CATALOG_V1.md](GAMEPLAY_SKIN_PUBLIC_CATALOG_V1.md)：公共 slot、语法、三态和适用性。
- [可选脚本作者说明](SKIN_SCRIPT_V1_AUTHORING.md)：C6 数值语言、权限、工具链与可导入候选。
- [RELEASE.md](RELEASE.md)：打包、便携模式、数据根与覆盖更新。
- [BMS_FORMAT_REFERENCE.md](BMS_FORMAT_REFERENCE.md)：格式、channel、时序、长条与控制流，供 P1-K 定点查阅。
- [BMS_GIMMICK_CHART_RENDERING.md](BMS_GIMMICK_CHART_RENDERING.md)：演出谱视觉机理与方案，当前门归 P1-L。
- [IIDX_REFERENCE_AUDIT.md](IIDX_REFERENCE_AUDIT.md)：外部判定/训练反馈参考。
- [UPSTREAM.md](UPSTREAM.md)：上游锁定点、选择性 cherry-pick 与风险边界。

## 恢复与视觉验收

- [皮肤恢复审计](SKIN_SYSTEM_RECOVERY_20260710.md)：皮肤任务必读；恢复锚点、撤回范围与重新准入。
- [schema 56 清点](SKIN_SYSTEM_SV1_0_INVENTORY_20260713.md)：副本取证与 SV1-0 历史证据，不授权重复操作生产数据。
- [Skin V1 架构依据](SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)：设计解释；现行合同归 P1-A。
- [普通短键动画手工素材](SKIN_BMS_NOTE_ANIMATION_MANUAL_GATE.md)：确定性测试素材及验收步骤。
- [集中视觉清单](SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)：待签收项、状态定义和用户反馈记录。

## 带日期的审查与交付证据

这些文件保存当时的基线、验证范围和限制；不因后续代码或网络状态变化改写原始结论。

- [2026-09-09 全项目进度审查](PROJECT_PROGRESS_AUDIT_20260909.md)：P1-A～M 生产链对照及本次实测矩阵。
- [2026-09-09 C6 验证](SKIN_SYSTEM_C6_VALIDATION_20260909.md)：可选脚本、作者工具、授权隔离与最终整包/G1 证据。
- [2026-09-03 C5](SKIN_SYSTEM_C5_SCENE_EVENT_COMPLETION_HANDOFF_20260903.md)：scene/event、slot hosts、预算与 publication 验证。
- [2026-08-31 C4](SKIN_SYSTEM_C4_CODEC_MATERIAL_COMPLETION_HANDOFF_20260831.md)：codec/catalog/resolver/material 完成证据。
- [2026-08-30 C3](SKIN_SYSTEM_C3_LAYOUT_COMPLETION_HANDOFF_20260830.md)：keymode/lane 前置、唯一 layout 与消费矩阵。
- [2026-08-13 C1](SKIN_SYSTEM_C1_COMPLETION_HANDOFF_20260813.md)：作者工作区与 archive 安全退出门。
- [2026-07-31 皮肤阶段审查](SKIN_SYSTEM_PROGRESS_AUDIT_20260731.md)：历史快照；其中选择竞态的后续修复见 P1-A CHANGELOG。

参考结论成为正式决定时回写 owning PLAN/CONSTRAINTS；新证据改变当前状态时更新 owning STATUS。维护规则见[文档入口](../README.md)。
