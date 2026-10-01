# 参考与证据索引

当前事实与执行门从[子线路由](../subline/README.md)读取。这里按用途区分使用说明、恢复边界和历史证据；日期报告中的状态只代表当时，不覆盖当前 STATUS/CONSTRAINTS。

## 使用说明与主题参考

- [完整皮肤制作手册](../../skin-authoring/docs/SKINNING.md)：随制作套件提供的入门、元素查表、进阶能力/上限与可运行例子；[旧入口](SKINNING.md)仅保留兼容路由。
- [GAMEPLAY_SKIN_PUBLIC_CATALOG_V1.md](GAMEPLAY_SKIN_PUBLIC_CATALOG_V1.md)：公共 slot、语法、三态和适用性。
- [可选脚本指南](../../skin-authoring/docs/SCRIPTING.md)：数值语言、权限、预算和可运行例子；[旧入口](SKIN_SCRIPT_V1_AUTHORING.md)只作路由。
- [RELEASE.md](RELEASE.md)：打包、便携模式、数据根与覆盖更新。
- [BMS_FORMAT_REFERENCE.md](BMS_FORMAT_REFERENCE.md)：格式、channel、时序、长条与控制流，供 P1-K 定点查阅。
- [BMS_GIMMICK_CHART_RENDERING.md](BMS_GIMMICK_CHART_RENDERING.md)：演出谱视觉机理与方案，当前门归 P1-L。
- [IIDX_REFERENCE_AUDIT.md](IIDX_REFERENCE_AUDIT.md)：外部判定/训练反馈参考。
- [UPSTREAM.md](UPSTREAM.md)：上游锁定点、选择性 cherry-pick 与风险边界。

## 恢复与视觉验收

- [静线 1P 外部参考凭据](references/simple-1p-20260912/README.md)：用户提供的五张原始对照图及来源标识，供布局打磨随时对照。
- [静线布局调整与验证](SKIN_SIMPLE_LAYOUT_20260912.md)：底部信息、独立键区和 BGA 作者参数，以及当时包的实际检查记录。
- [静线机台结构打磨](SKIN_SIMPLE_CABINET_20260912.md)：完整底板、原创材质、分立仪表与实际渲染证据；保留整体视觉未签收边界。
- [静线判定与血槽细节](SKIN_SIMPLE_STAGE_HUD_20260913.md)：图10之后的薄音符、同源舞台模板、固定血槽外框与真实读数。
- [静线演奏信息与键区比例](SKIN_SIMPLE_INFORMATION_20260913.md)：实时判定、MIN/BPM/MAX、曲名/难度、Score/HiSpeed，通用信息区域及实际画面验证。
- [静线黑白轨与皿轨比例](SKIN_SIMPLE_LANE_PROPORTIONS_20260913.md)：参考图测量、独立黑键轨宽、皿旁间隔及音符透明边修正。
- [历史双内置构建与后续退役](SKIN_BUILTIN_BUILD_20260912.md)：开发启动、build/publish 的源文件同步及当时发行证据；当前仅静线参与构建。
- [历史双内置选择验证](SKIN_BUILTIN_SELECTION_20260912.md)：当时无需导入的选择、重启保留、游玩与安装恢复；当前仅静线内置。
- [皮肤恢复审计](SKIN_SYSTEM_RECOVERY_20260710.md)：皮肤任务必读；恢复锚点、撤回范围与重新准入。
- [schema 56 清点](SKIN_SYSTEM_SV1_0_INVENTORY_20260713.md)：副本取证与 SV1-0 历史证据，不授权重复操作生产数据。
- [Skin V1 架构依据](SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)：设计解释；现行合同归 P1-A。
- [普通短键动画手工素材](SKIN_BMS_NOTE_ANIMATION_MANUAL_GATE.md)：确定性测试素材及验收步骤。
- [集中视觉清单](SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)：待签收项、状态定义和用户反馈记录。

## 带日期的审查与交付证据

这些文件保存当时的基线、验证范围和限制；不因后续代码或网络状态变化改写原始结论。

- [2026-10-01 两源 BMS 浏览下载](BMS_DOWNLOAD_20261001.md)：实施规划、Ginger Rush/616 接口与真实包、后台任务、精确选歌、桌面及自动验证边界。
- [2026-10-01 下载入口与三级筛选](BMS_DOWNLOAD_FILTERS_20261001.md)：原浏览位置、来源→表→等级联动、完整表资料、精确匹配与真实两源桌面下载选歌。
- [2026-09-30 原生与转谱 gameplay 性能](GAMEPLAY_PERFORMANCE_20260930.md)：原生判定/长条、音频分配、转谱池化与 BGA 共享的前后证据和验证边界。
- [2026-09-30 BGA 皮肤与作者手册](BGA_SKIN_AUTHORING_20260930.md)：游戏持有的播放会话、作者窗口、完整教程与可运行例子的验证和边界。

- [2026-09-29 谱库、声音与选歌体验闭环](EXPERIENCE_CLOSURE_20260929.md)：缺失恢复与历史保全、当前页难度表、暂停续播、手动长条和单轨筛选的验证与剩余人工门。

- [2026-09-22 BMS TOTAL](BMS_TOTAL_RULES_AUDIT_20260922.md)：作者声明、各家族缺省、辅助前后物量与历史成绩版本的来源和验证。
- [2026-09-09 全项目进度审查](PROJECT_PROGRESS_AUDIT_20260909.md)：P1-A～M 生产链对照及本次实测矩阵。
- [2026-09-09 C6 验证](SKIN_SYSTEM_C6_VALIDATION_20260909.md)：可选脚本、作者工具、授权隔离与最终整包/G1 证据。
- [2026-09-09 C7 验证](SKIN_SYSTEM_C7_VALIDATION_20260909.md)：完整双包、制作体验、安装恢复与最终自动检查；人工事实独立保留。
- [2026-09-09 C7 暂停检查点](SKIN_SYSTEM_C7_RESUME_20260909.md)：当日成果、未验修改及配方与旧成品差异；旧续接步骤已由后续 C7 交付及 P1-A 当前停止边界取代。
- [2026-09-03 C5](SKIN_SYSTEM_C5_SCENE_EVENT_COMPLETION_HANDOFF_20260903.md)：scene/event、slot hosts、预算与 publication 验证。
- [2026-08-31 C4](SKIN_SYSTEM_C4_CODEC_MATERIAL_COMPLETION_HANDOFF_20260831.md)：codec/catalog/resolver/material 完成证据。
- [2026-08-30 C3](SKIN_SYSTEM_C3_LAYOUT_COMPLETION_HANDOFF_20260830.md)：keymode/lane 前置、唯一 layout 与消费矩阵。
- [2026-08-13 C1](SKIN_SYSTEM_C1_COMPLETION_HANDOFF_20260813.md)：作者工作区与 archive 安全退出门。
- [2026-07-31 皮肤阶段审查](SKIN_SYSTEM_PROGRESS_AUDIT_20260731.md)：历史快照；其中选择竞态的后续修复见 P1-A CHANGELOG。

参考结论成为正式决定时回写 owning PLAN/CONSTRAINTS；新证据改变当前状态时更新 owning STATUS。维护规则见[文档入口](../README.md)。
