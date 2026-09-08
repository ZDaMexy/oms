---
name: project_oms_bms_skin_authoring
description: BMS 皮肤用户决定、作者能力边界与常见错误入口的召回
metadata:
  node_type: memory
  type: project
---

# BMS 皮肤创作召回

当前可选包、public slot、视觉签收只读 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)；产品目标/硬约束读 [PLAN](../../doc_md/subline/P1-A/DEVELOPMENT_PLAN.md) / [CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)，作者说明读 [SKINNING](../../doc_md/other/SKINNING.md)。皮肤异常任务先进入 [[reference_skin_recovery_20260710]]。

## 必须召回的用户决定

- 范围是 gameplay；不移植 LR2/beatoraja runtime，只对齐元素族与表达力。保持 .osk 分发、根 skin.ini、mania 素材/动画命名、目录编辑和拖入导入心智；BMS/scene/script 为版本化扩展，不要求 DLL。
- mania 普通 .osk 是固定行为宿主 + legacy 素材/参数，不能当作通用作者脚本上限。共享 neutral codec/scene/event/reload/sandbox，ruleset topology adapter 各自保留。
- 引擎掌握 gameplay truth、layout、滚动/LN 裁剪、池、BGA 内容时钟与安全；作者控制 scene/动画/只读响应。三态按 catalog 的 requirement/applicability 决定，不另列会漂移的 suppress 清单。
- canonical oms-simple 和公开 API 展示包 oms-complex 同时覆盖 mania/BMS；Authoring Kit 是可编辑源、模板、schema/事件/layout/预算说明、validator/diagnostics 与打包文档，不是第二套 SDK/runtime。
- 程序化 OmsSkin 在 canonical parity、完整性、原子恢复与实机 gate 前保留，接管后退出产品渲染；canonical 损坏走明确安装修复，不暗落另一套程序化主题。
- 视觉采用集中签收；自动可证切片可继续，但不得把“实现/自动通过，视觉待验收”写成产品/release 完成，或复用 2026-07-14 静态恢复签收。

## 常见误入口

- F2/F3/G2、Lua、reference-default 等是恢复期历史名称，不据此恢复代码或判断当前门。异常归档只定点取证，不整包恢复。
- core LegacySkin 不编译依赖 BMS；精确反射类型匹配必须排除 LegacyBeatmapSkin 等其它子类。
- 注入式 BeatmapNoteSkin fixture 不证明真实 public sidecar。WorkingBeatmap.Skin 的只读 legacy direct visual compatibility 可继续存在，但 public sections 不进入作者 resolver；重开新作者格式需独立完整产品 gate。
- lazer editor 的 ISerialisableDrawable/CLR Type JSON 不能复用为外部 scene manifest；scene 只接受版本化 allowlisted node ID。
- callback 返回后量 stopwatch 无法阻止 while true；sandbox 必须可抢占并有 instruction/heap/node/resource quota。
- schema 来自生产组件与合同，SKINNING 是派生说明，不反向把旧说明当实现需求。

## 诊断导航

| 现象 | 优先进入 |
| --- | --- |
| ordinary .osk stream、14K 第二皿素材 | 恢复期保留修正是 base parser 前 rewind stream、S2/P2 素材；先查当前回归，见 [[reference_skin_recovery_20260710]] |
| lane 相对宽修改无效、hit position/裁剪漂移 | [[reference_bms_default_skin_geometry]]、[[reference_gameplay_skin_layout_snapshot]]；同比缩放全部 relative width 会被归一化抵消，HitTargetVerticalOffset 保持时序合同 |
| 缺 Keys 被当成声明、同名贴图/width 跨包拼接 | [[reference_gameplay_skin_config_presence]]、[[reference_gameplay_skin_lane_resource_compatibility]] |
| LN body 异步到达后颜色/状态不对 | 真实 DrawableBmsHoldNote 是 Idle/Holding/Broken authority；新 visual 立即投影当前态，不自建 gameplay state；常数只查 P1-A 的 LN 视觉合同 |
| 原位文件修改未生效、旧资源释放过早 | [[reference_skin_atomic_reload_detach]]；active immutable instance 不观察磁盘，manual Reload 是统一入口 |
| Workspace copy/rename/delete 或源丢失 | [[reference_skin_external_workspace_managed_copy]]、[[reference_skin_managed_folder_mutation_foundation]]；external 永久只读 |
| 多 BGA viewport 占用多 decoder | [[reference_bms_bga_chain]] 与 P1-L；统一 viewport 不等于单内容源 |
| 窄 foundation 被写成整轮交付 | [[project_oms_skin_product_progress]] |

不在此页重复 campaign 燃尽、slot 数量、candidate 表或测试数字。
