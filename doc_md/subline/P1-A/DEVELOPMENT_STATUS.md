# P1-A 当前状态：Skin V1、产品面与 release gate

> 最后更新：2026-09-12（原 C7 内修复默认皮肤提示与构建警告，完整复验和实际交付完成；最终产物独立复核通过，人工未签收）
> 全局见[主线状态](../../mainline/DEVELOPMENT_STATUS.md)，后续门见[PLAN](DEVELOPMENT_PLAN.md)，实现合同见[TECHNICAL_CONSTRAINTS](TECHNICAL_CONSTRAINTS.md)，历史见[CHANGELOG](CHANGELOG.md)。

## 一句话状态

玩家可以使用同时支持 BMS、mania 的静线与星轨完整皮肤，作者可用随包独立工具完成修改、检查、打包、导入和更新。静线是当前首次默认与正式保底；星轨保留展示包和默认候选身份，首次选择不擅自改为星轨。本次默认皮肤提示与构建警告修复已完成完整自动复验、真实安装恢复和跨版本覆盖验证，可运行集中验收包已生成，最终独立产物复核通过。最新事实见 [C7 当前修复](../../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md#交付后默认皮肤提示与构建警告修复)，制作演练见 [WORKSHOP](../../../skin-authoring/docs/WORKSHOP.md)，发行边界见 [P1-F](../P1-F/DEVELOPMENT_STATUS.md)。

原七阶段工程记录仍为 **`7/7 closed，C7 非人工结果完成`**，交付后的实际问题继续在原 C7 内修复，不重计或拆出新阶段；本次修复的独立复核已通过，最终文档与提交记录见 C7 报告。只读简洁包承担保底，旧程序化 `OmsSkin` 只保留历史证据与实机对照，源码物理删除仍待原实机门，不能重新进入产品回退链。`V-001`～`V-004` 签收仍 **0/4**，`V-005` 与 C7 观感、设备及长时间体验未签收；`SV1-1` 整体签收、Skin V1 与 release 未完成，不换算线性完成比例。

## 当前产品能力与剩余门

| 产品面 | 当前可用结果 | 尚未完成或必须保持的边界 |
| --- | --- | --- |
| 导入、选择与管理 | 可导入普通皮肤包，管理游戏内皮肤目录，或登记作者自己的目录；选择可在重启后保留 | 作者外部目录始终只读；已有用户数据恢复要求继续生效 |
| 修改后更新与失败保护 | 三种来源均可从设置手动重新载入整个皮肤；更新失败保留原来可用的外观 | 游玩和预览期间不能重新载入；不提供自动监听更新 |
| 游玩外观与动画 | BMS、mania 已接通适用的音符、长条、按键、判定、布局及动画，可响应实际游玩情况 | 两种玩法的专属组件不同；两款完整作者文件已具备，最终观感等待人工观察；未扩展选歌、结算等页面的皮肤制作范围 |
| 可选组合效果 | “星轨”包含独立舞台、动画音符和随近期击打、能量变化的组合演出；玩家可以授权、拒绝、查询和撤销 | 基本游玩不依赖额外授权；星轨仍是展示包与默认候选；当前首次默认保持静线。原 Momentum 作为 C6/V-005 原输入保留 |
| 作者独立制作 | [完整套件](../../../skin-authoring/README.md)提供双包源文件、模板、完整公共参考、错误定位和打包/更新；已在独立发行工具目录、无 Git/SDK 的环境实际重做 Aurora Study，并验证三款作品可重复得到相同成品 | 制作命令准备独立可消耗副本，保留正式作品；游戏导入由同字节成品的实际游戏路径证明，鼠标操作未代签。无可视化编辑器，作者外部目录始终只读 |
| 两款正式皮肤与安装恢复 | 静线用于清楚克制的基本游玩，星轨展示完整布局和组合演出；两包均通过普通作者文件与导入/导出入口。简洁工作副本损坏时保全旧件并从只读原件完整恢复 | 原件缺失/损坏必须提示修复安装并阻止游玩/预览；未知旧记录、中断操作保全。旧程序化代码的物理移除仍受实机门约束，不能以保留代码恢复临时外观 |
| 实际安装与便携使用 | 当前完整多文件 ZIP 已完成首次便携、自定义保存、工作副本恢复与覆盖后启动四轮；另从上一修复版真实跨版本更新，游戏首次启动自动更新默认工作副本。集中验收包已在无 Git/SDK 的 PS5 环境组装 | 自动启动和目录证据不能代签鼠标体验、画面和设备；只交付最终包，中间候选保留为纠错证据 |
| 实际观感与设备体验 | 集中清单已包含已有外观和新组合效果 | V-001～V-004 签收 0/4，V-005 未签收；清晰度、整体美术、低端设备与长时间体验不能用自动检查代签，见[集中清单](../../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md) |

本轮成品、真实制作、安装恢复、完整使用验证和独立复核证据集中于 [C7 报告](../../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md)。[集中体验包](../../../skin-c7-acceptance/README.md)保留两款成品、作者练习、第三方输入、原 V-001/V-005 输入和未签收记录。

C2～C6共用exact package+layout+material+scene publication，script及编译工作已加入同一owner/participant/lease/detach/retire。无gameplay host的菜单也先验证ini/manifest/scene/script/素材；授权撤销独立使旧host失权，不扩大Reload准入。完整合同只在[技术约束](TECHNICAL_CONSTRAINTS.md)维护。

## 最近一次验证

**2026-09-12，原 C7 交付后修复：** 两款作品和 Aurora 的 BMS 5K/7K 声明已明确适用样式，普通导入附加的整行说明也可正常读取；真正的错误仍保留完整日志并给出中文更新指引。MessagePack/SignalR 已按同支修补版更新，未扩大联网或皮肤权限；VS Code 实际使用的 Release 同命令构建为零警告，之前 Debug 只是补充检查。根因、源码复核和仍存在的依赖风险见 [C7 当前修复](../../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md#交付后默认皮肤提示与构建警告修复)。

当前冻结产物完整复验为 BMS **2220/2220**、mania **863/867**、core `~Skin` **1339/1344**、FileStore **11/11**；原 mania 四项/core 五项失败的名称、类别和完整消息均精确一致，原 sample 对照继续通过。共享定点 **104/104**、真实两玩法产品与入口 **21/21** 通过。首轮 BMS 五项选择失败、同产物定点六项通过与随后完整复验均保留，首败原因未确定，不追认为旧基线或已查明原因；详细检查及 triage 只在上述报告维护，不能用定点通过替代整套。

当前交付为 `release-repo/oms_20260911_startup-fix-final.zip` 和 `release-repo/oms-skin-c7-acceptance-20260911-startup-fix-final/`，实际制作身份见 [WORKSHOP](../../../skin-authoring/docs/WORKSHOP.md)。四轮安装恢复正常启动并退出，两处测试根的两玩法均可用；另从 preview-fix 隔离安装完成真实跨版本覆盖、默认工作副本自动更新及第三处只读测试副本的两玩法核验。原三处保存根前后字节和属性一致，未打开原库。最终独立产物复核通过，精确交付摘要及原始证据见 [C7 报告](../../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md)。

旧 [BMS 真实预览入口缺陷](../../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md#交付后-bms-预览入口修复)、[C6 验证](../../other/SKIN_SYSTEM_C6_VALIDATION_20260909.md)、C7 旧失败/已解决 sample 对照及中止记录保留在对应报告和[本线历史](CHANGELOG.md)，不因本轮通过删除。旧自解压候选误入已有保存根只有事后保全、没有该根事前快照，不能追溯宣称无损或回滚，见 [P1-F](../P1-F/DEVELOPMENT_STATUS.md)；本次原根未变不抹除该事故。

## 当前风险与未完成项

- `BmsBeatmapDecoderOptions.KeymodeOverride`只是host/importer seam；普通loader传null，无用户纠正UI。证据不足的sparse .bms/.bml仍安全拒绝；可用性缺口归P1-K，不重开C3。
- scanner只在启动后对账一次；新增managed direct child须重启发现，已登记current内容只能手动Reload。没有watcher或live gameplay replacement。
- C1 held-root/journal不是filesystem transaction；foreign addition/replacement可导致冻结。current mutation的具体失败阶段与external零写入合同继续生效。
- 程序化 OmsSkin 已退出实际产品回退链，只保留历史和人工对照；源码物理删除仍须等原实机门，原七阶段非人工收口不代签该门，NotApplicable 也不能写成普遍 unsupported。
- 原七阶段非人工结果已完成，人工签收结果尚未知；发现新的真实问题后按证据修复，不预先虚构结论。原保存根事故只有事后数据与指针保全，没有事前该根快照；公开不披露账户路径，不宣称回滚或未知事前后一致性。
- BGA内容/timeline/seek/gimmick仍归P1-L；在线服务、sample pool、判定、binding不属C5。既有失败及已解决的原 sample 对照继续按[精确失败合同](TECHNICAL_CONSTRAINTS.md#测试与发布约束)逐项保留和比较，当前最终剩余 core 五项/mania 四项，不能按数量掩盖回归。
