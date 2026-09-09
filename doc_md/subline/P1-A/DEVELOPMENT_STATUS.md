# P1-A 当前状态：Skin V1、产品面与 release gate

> 最后更新：2026-09-09（按用户要求暂停；C7 保留当前成果，最终验证未完成）
> 全局见[主线状态](../../mainline/DEVELOPMENT_STATUS.md)，后续门见[PLAN](DEVELOPMENT_PLAN.md)，实现合同见[TECHNICAL_CONSTRAINTS](TECHNICAL_CONSTRAINTS.md)，历史见[CHANGELOG](CHANGELOG.md)。

## 一句话状态

**续接先读 [C7 暂停检查点](../../other/SKIN_SYSTEM_C7_RESUME_20260909.md)。** 新信息顶栏、按下反馈的配方比成品新，尚未重新生成；最新 Game 公共信息实现已编译，新测试与最终宽测/发行未完成。当前提交只保全成果，不表示无未验问题。

Skin V1 为 **`6/7 closed，C7 active`**：C7 已形成同时支持 BMS、mania 的“静线”与“星轨”两款完整作者包、可重复制作套件和安装恢复；正在完成最终使用验证与发行检查。只读携带的简洁包承担正式保底，旧程序化 `OmsSkin` 只保留历史证据与实机对照，不能重新进入产品回退链。`V-001`～`V-004` 签收仍 **0/4**，`V-005` 未签收；`SV1-1`、Skin V1 与 release 未完成。原七阶段不重新计数，不换算线性完成比例。

## 当前产品能力与剩余门

| 产品面 | 当前可用结果 | 尚未完成或必须保持的边界 |
| --- | --- | --- |
| 导入、选择与管理 | 可导入普通皮肤包，管理游戏内皮肤目录，或登记作者自己的目录；选择可在重启后保留 | 作者外部目录始终只读；已有用户数据恢复要求继续生效 |
| 修改后更新与失败保护 | 三种来源均可从设置手动重新载入整个皮肤；更新失败保留原来可用的外观 | 游玩和预览期间不能重新载入；不提供自动监听更新 |
| 游玩外观与动画 | BMS、mania 已接通适用的音符、长条、按键、判定、布局及动画，可响应实际游玩情况 | 两种玩法的专属组件不同；这不等于两款正式成品已完成，也未扩展选歌、结算等页面的皮肤制作范围 |
| 可选组合效果 | “星轨”包含独立舞台、动画音符和随近期击打、能量变化的组合演出；玩家可以授权、拒绝、查询和撤销 | 基本游玩不依赖额外授权；星轨仍是展示包与默认候选，不决定首次默认选择。原 Momentum 作为 C6/V-005 原输入保留 |
| 作者独立制作 | [完整套件](../../../skin-authoring/README.md)提供双包源文件、模板、公共目录、检查和错误定位、打包与普通导入/更新；Aurora Study 是实际从模板制作的练习成品 | 无可视化编辑器；直接改素材后检查和打包，明确选择重新生成时才覆盖配方输出。作者外部目录始终只读 |
| 两款正式皮肤与安装恢复 | 静线用于清楚克制的基本游玩，星轨展示完整布局和组合演出；两包均通过普通作者文件与导入/导出入口。简洁工作副本损坏时保全旧件并从只读原件完整恢复 | 原件缺失/损坏必须提示修复安装并阻止游玩/预览；未知旧记录、中断操作保全。旧程序化代码的物理移除仍受实机门约束，不能以保留代码恢复临时外观 |
| 实际观感与设备体验 | 集中清单已包含已有外观和新组合效果 | V-001～V-004 签收 0/4，V-005 未签收；清晰度、整体美术、低端设备与长时间体验不能用自动检查代签，见[集中清单](../../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md) |

本轮成品、真实制作、安装恢复、完整使用验证和独立复核证据集中于 [C7 报告](../../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md)。[集中体验包](../../../skin-c7-acceptance/README.md)保留两款成品、作者练习、第三方输入、原 V-001/V-005 输入和未签收记录。

C2～C6共用exact package+layout+material+scene publication，script及编译工作已加入同一owner/participant/lease/detach/retire。无gameplay host的菜单也先验证ini/manifest/scene/script/素材；授权撤销独立使旧host失权，不扩大Reload准入。完整合同只在[技术约束](TECHNICAL_CONSTRAINTS.md)维护。

## 最近一次验证

**2026-09-09 C6最终实际验证**（Release当前源码，共享构建/测试串行）：

| 测试面 | 结果 |
| --- | --- |
| core GameplaySkin focused | 486/486 |
| BMS / mania relevant | 414/414及复审新增atomic-save 2/2；mania 69/69 |
| 三源备份根 G1 | source/实际CLI bytecode × 三源 6/6；用户原根与baseline保持一致 |
| BMS full | 1776/1776，无跳过或hang artifact |
| mania full | 860/864，四项既有HoldNote frame-count失败 |
| core ~Skin / FileStoreTests | 1275/1281，六项既有archive/default-skin失败；存储11/11 |
| Windows Release | 0 error / 18条NU1902输出，九条既有MessagePack告警在restore/build重复；BMS重新编译仍仅有既有CS8600/CA2007 |

十项既有失败的名称、错误分类及精确消息与[项目审查](../../other/PROJECT_PROGRESS_AUDIT_20260909.md)实际TRX逐项全等。命令、红转绿、source/consumer矩阵、性能环境、独立终审和检查结论集中于[C6报告](../../other/SKIN_SYSTEM_C6_VALIDATION_20260909.md)。未跑全core、publish、低端实机或GPU/字体视觉门；C5既有完成证据仍见[2026-09-03交接](../../other/SKIN_SYSTEM_C5_SCENE_EVENT_COMPLETION_HANDOFF_20260903.md)，不重签C1～C5。

## 当前风险与未完成项

- `BmsBeatmapDecoderOptions.KeymodeOverride`只是host/importer seam；普通loader传null，无用户纠正UI。证据不足的sparse .bms/.bml仍安全拒绝；可用性缺口归P1-K，不重开C3。
- scanner只在启动后对账一次；新增managed direct child须重启发现，已登记current内容只能手动Reload。没有watcher或live gameplay replacement。
- C1 held-root/journal不是filesystem transaction；foreign addition/replacement可导致冻结。current mutation的具体失败阶段与external零写入合同继续生效。
- 程序化OmsSkin在canonical parity、完整性、原子恢复与实机gate前不得删除；C6完成不能提前核算C7或人工签收，NotApplicable也不能写成普遍unsupported。
- BGA内容/timeline/seek/gimmick仍归P1-L；在线服务、sample pool、判定、binding不属C5。既有core六项/mania四项失败须按[精确失败合同](TECHNICAL_CONSTRAINTS.md#测试与发布约束)比较，不能按数量掩盖回归。
