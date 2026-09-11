# P1-A 当前状态：Skin V1、产品面与 release gate

> 最后更新：2026-09-11（原七阶段非人工结果完成；双包、制作套件、真实发行四轮与集中验收包已交付，人工未签收）
> 全局见[主线状态](../../mainline/DEVELOPMENT_STATUS.md)，后续门见[PLAN](DEVELOPMENT_PLAN.md)，实现合同见[TECHNICAL_CONSTRAINTS](TECHNICAL_CONSTRAINTS.md)，历史见[CHANGELOG](CHANGELOG.md)。

## 一句话状态

玩家可以安装同时支持 BMS、mania 的静线与星轨完整皮肤，游玩并选择是否启用星轨的组合演出；作者可以用随包独立工具完成修改、检查、打包和更新，同一成品已通过游戏普通导入、选择、导出与再次导入。最终发行物已完成首次便携、自定义保存、工作副本恢复和覆盖后启动四轮，均正常退出；可直接运行的集中验收包也已由最终发行物组装完成。原 C7 的非人工产品结果已完成，剩余是画面、设备和长时间体验的人工签收。最新产品证据见 [C7 报告](../../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md)，独立制作事实见 [WORKSHOP](../../../skin-authoring/docs/WORKSHOP.md)，发行纠错与待验边界见 [P1-F](../P1-F/DEVELOPMENT_STATUS.md)；[暂停检查点](../../other/SKIN_SYSTEM_C7_RESUME_20260909.md)仅保留历史。

原七阶段工程记录为 **`7/7 closed，C7 非人工结果完成`**。双包、完整制作路径、正式保底、安装恢复、完整自动复验与真实发行检查均已落实，最终独立产物复核和文档检查通过，不拆出新阶段。只读携带的简洁包承担正式保底，旧程序化 `OmsSkin` 只保留历史证据与实机对照，物理删除仍待原实机门，不能重新进入产品回退链。`V-001`～`V-004` 签收仍 **0/4**，`V-005` 与 C7 观感、设备及长时间体验未签收；`SV1-1` 整体签收、Skin V1 与 release 未完成。原七阶段不重新计数，不换算线性完成比例。

## 当前产品能力与剩余门

| 产品面 | 当前可用结果 | 尚未完成或必须保持的边界 |
| --- | --- | --- |
| 导入、选择与管理 | 可导入普通皮肤包，管理游戏内皮肤目录，或登记作者自己的目录；选择可在重启后保留 | 作者外部目录始终只读；已有用户数据恢复要求继续生效 |
| 修改后更新与失败保护 | 三种来源均可从设置手动重新载入整个皮肤；更新失败保留原来可用的外观 | 游玩和预览期间不能重新载入；不提供自动监听更新 |
| 游玩外观与动画 | BMS、mania 已接通适用的音符、长条、按键、判定、布局及动画，可响应实际游玩情况 | 两种玩法的专属组件不同；两款完整作者文件已具备，最终观感等待人工观察；未扩展选歌、结算等页面的皮肤制作范围 |
| 可选组合效果 | “星轨”包含独立舞台、动画音符和随近期击打、能量变化的组合演出；玩家可以授权、拒绝、查询和撤销 | 基本游玩不依赖额外授权；星轨仍是展示包与默认候选，不决定首次默认选择。原 Momentum 作为 C6/V-005 原输入保留 |
| 作者独立制作 | [完整套件](../../../skin-authoring/README.md)提供双包源文件、模板、完整公共参考、错误定位和打包/更新；已在独立发行工具目录、无 Git/SDK 的环境实际重做 Aurora Study，并验证三款作品可重复得到相同成品 | 制作命令准备独立可消耗副本，保留正式作品；游戏导入由同字节成品的实际游戏路径证明，鼠标操作未代签。无可视化编辑器，作者外部目录始终只读 |
| 两款正式皮肤与安装恢复 | 静线用于清楚克制的基本游玩，星轨展示完整布局和组合演出；两包均通过普通作者文件与导入/导出入口。简洁工作副本损坏时保全旧件并从只读原件完整恢复 | 原件缺失/损坏必须提示修复安装并阻止游玩/预览；未知旧记录、中断操作保全。旧程序化代码的物理移除仍受实机门约束，不能以保留代码恢复临时外观 |
| 实际安装与便携使用 | 最终完整多文件 ZIP 已实际完成首次便携、自定义保存、简洁工作副本恢复与覆盖后启动四轮，核对保存位置和程序旁缓存，均稳定运行并正常退出；最终集中验收包已在无 Git/SDK 的 PS5 环境组装完成 | 自动启动和目录证据不能代签鼠标体验、画面和设备；只交付最终包，中间候选保留为纠错证据 |
| 实际观感与设备体验 | 集中清单已包含已有外观和新组合效果 | V-001～V-004 签收 0/4，V-005 未签收；清晰度、整体美术、低端设备与长时间体验不能用自动检查代签，见[集中清单](../../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md) |

本轮成品、真实制作、安装恢复、完整使用验证和独立复核证据集中于 [C7 报告](../../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md)。[集中体验包](../../../skin-c7-acceptance/README.md)保留两款成品、作者练习、第三方输入、原 V-001/V-005 输入和未签收记录。

C2～C6共用exact package+layout+material+scene publication，script及编译工作已加入同一owner/participant/lease/detach/retire。无gameplay host的菜单也先验证ini/manifest/scene/script/素材；授权撤销独立使旧host失权，不扩大Reload准入。完整合同只在[技术约束](TECHNICAL_CONSTRAINTS.md)维护。

## 最近一次验证

**2026-09-11 当前 C7：** 两款成品和作者练习的普通导入、三种来源、备份根保护与导出再导入已有实际记录。必要信息、不同屏幕下的顶部布局、真实按下反馈和声音已复验；星轨在真实击打、长条早松、暂停、拒绝/撤销额外效果、重试与时间跳转后仍保持基本游玩。作品、操作、环境、失败身份与修复证据集中于 [C7 报告](../../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md)，独立发布工具的实际制作及逐字节重现见 [WORKSHOP](../../../skin-authoring/docs/WORKSHOP.md)。

最终当前产物的完整自动复验已完成：BMS **2215/2215**、mania **863/867**、core `~Skin` **1314/1319**、FileStore **11/11**。mania 剩余原有四项、core 剩余原有五项的名称与完整消息均与独立既有基线精确一致；原 core sample 对照已按当前普通样本路径验证通过，其解决事实独立记录，未删除旧失败历史。`closure-failure-comparison.json` 为通过，规定格式检查及 Release 编译也已通过；原始记录在 `artifacts/skin-c7-evidence/tests/c7-closure-*.trx`。这些是最终当前产物结果，不以此前候选或局部检查代替。

最终制作工具已独立发布，在 PS5、PATH 无 Git/SDK 的独立套件中完成两组实际演练：从模板修改资料及 README、定位并修复图片错误、打包和准备导入/更新副本，以及三款作品检查、重复打包与重新生成后的逐字节一致性。工具 SHA-256 为 `c478dac99f66fc2cfacd74b3f03d1617047885f24d0b43616047f71cff513aaf`，工作流执行时间为 `2026-09-11T08:37:54.7568409Z`；对应新证据已同步 [WORKSHOP](../../../skin-authoring/docs/WORKSHOP.md)及其两份 JSON，旧记录保全。该工具先独立发布和演练，再逐字节进入最终发行物；本记录不混淆这两个执行来源。

最终发行 ZIP 为 `release-repo/oms_20260911_4.zip`，SHA-256 `bcf6aa8da7700f822db6613734dfc4af20d4bf57c5ff6d29f25cf5c4b2ce9ac8`，Windows Shell 解压后的只读原件与摘要检查通过。`2026-09-11T09:08:41.8937534Z`～`09:10:58.7608168Z` 完成首次便携、自定义保存、工作副本恢复、完整覆盖后启动四轮，全部 `Passed`、`NormalExit=true`、`ExitCode=0`、无强制终止，源发行目录保持原样。记录为 `artifacts/skin-c7-evidence/release-startup-final4/results.json`。对首次便携根和最后覆盖后的自定义根分别读取真实数据库，BMS、mania 均 `Available=true`；中间两轮没有另存数据库快照，不扩张此项证据范围。

最终集中验收目录为 `release-repo/oms-skin-c7-acceptance-20260911-final/`，由最终发行物随包 `Build-Acceptance.ps1` 在 PS5、PATH 无 Git/SDK 环境实际生成，退出码 0、来源不变，记录为 `artifacts/skin-c7-evidence/acceptance-final4-assembly.json`。它携带双包、完整作者路径、第三方与原有验收输入，并保留全部未签收项。真实启动中发现的恢复重入和取消资源清理已修复并复验。此前错误自解压包曾误入已有自定义数据根并执行数据库迁移，只有事后保全；后续隔离运行前后的既有目录对照不能追溯证明该次事故无损，详见 [P1-F](../P1-F/DEVELOPMENT_STATUS.md)。

2026-09-09 的 C6 完整验证、精确旧失败比较及独立终审保留于 [C6 报告](../../other/SKIN_SYSTEM_C6_VALIDATION_20260909.md)和本线历史；它们不替代 C7 当前成品与最终发行门，也不重签 C1～C6。

## 当前风险与未完成项

- `BmsBeatmapDecoderOptions.KeymodeOverride`只是host/importer seam；普通loader传null，无用户纠正UI。证据不足的sparse .bms/.bml仍安全拒绝；可用性缺口归P1-K，不重开C3。
- scanner只在启动后对账一次；新增managed direct child须重启发现，已登记current内容只能手动Reload。没有watcher或live gameplay replacement。
- C1 held-root/journal不是filesystem transaction；foreign addition/replacement可导致冻结。current mutation的具体失败阶段与external零写入合同继续生效。
- 程序化 OmsSkin 已退出实际产品回退链，只保留历史和人工对照；源码物理删除仍须等原实机门，原七阶段非人工收口不代签该门，NotApplicable 也不能写成普遍 unsupported。
- 原七阶段非人工结果已完成，人工签收结果尚未知；发现新的真实问题后按证据修复，不预先虚构结论。原保存根事故只有事后数据与指针保全，没有事前该根快照；公开不披露账户路径，不宣称回滚或未知事前后一致性。
- BGA内容/timeline/seek/gimmick仍归P1-L；在线服务、sample pool、判定、binding不属C5。既有失败及已解决的原 sample 对照继续按[精确失败合同](TECHNICAL_CONSTRAINTS.md#测试与发布约束)逐项保留和比较，当前最终剩余 core 五项/mania 四项，不能按数量掩盖回归。
