# P1-A 当前状态：Skin V1、产品面与 release gate

> 最后核对：2026-09-23（跨线状态与证据同步；专项产品/人工验证日期不变）
> 全局见[主线状态](../../mainline/DEVELOPMENT_STATUS.md)，后续门见[PLAN](DEVELOPMENT_PLAN.md)，稳定合同见[TECHNICAL_CONSTRAINTS](TECHNICAL_CONSTRAINTS.md)，历史见[CHANGELOG](CHANGELOG.md)。

## 一句话状态

静线是唯一内置、首次默认与正式保底；complex 已退役，历史作者文件与验证证据保留，普通用户导入包不删除。用户认可最近调整并决定暂时停止外观打磨；已按新增需求收简皮肤设置：移除外部注册工作区，固定目录配打开/刷新，并恢复原组件布局编辑功能，未进入新的外观阶段。BMS 与 osu!mania 现在各自记住皮肤，旧全局配置首次启动会复制到两个模式；切换模式时立即应用对应选择。此反馈不等于原 V-001～V-005、真实设备或长期体验签收，Skin V1/release 仍未完成。原 C1～C7 工程证据不重计，程序化 OmsSkin 已退出实际回落链，物理删除仍待原实机门。

## 当前产品能力与剩余门

| 产品面 | 当前可用结果 | 必须保持的边界与未完成项 |
| --- | --- | --- |
| 导入、选择与管理 | 普通 .osk、固定 chartskin 目录；BMS 与 osu!mania 可分别选择并跨重启保留，设置不再显示外部注册工作区 | 旧 external 记录保留且始终只读；固定目录新增作品可手动刷新发现；首次启动从旧全局皮肤复制模式偏好，失效/受保护项回到静线 |
| 更新与失败保护 | 设置“刷新皮肤”重新发现固定目录并重载当前整包，失败保留此前外观 | 游玩/预览期间不可 reload；无 watcher 或游玩中替换 |
| 唯一内置与恢复 | 正常开发启动、build/publish 从作者源自动同步 simple；安装原件验证后恢复工作副本，旧内置 complex 选择迁回 simple | canonical 原件故障须提示修复安装并阻止游玩/预览，不恢复程序化主题；不按同名清除用户包 |
| 静线游玩外观 | 文件素材提供完整底板、轨道、转盘/键帽、固定分段血槽和 BGA 外框；白黑/皿轨独立比例、分隔线补偿、14K 每侧键序一致 | 已依据用户参考反复改进；当前暂停继续打磨，完整歌曲、其它尺寸及设备体验未整体签收 |
| 实时演奏信息 | 实时判定与断连统计、EX SCORE/HiSpeed、MIN/当前/MAX BPM；曲名、作者标级与独立的表名/表内等级 | 读取规则真实状态；表归类不替代作者难度，皮肤不改变判定/计分、时间或难度表导入 |
| 皮肤布局与公共能力 | BMS 公开键区、血槽、BGA 大小/位置及上下信息区；可选黑键轨宽，无声明旧包保持普通键等宽；mania/BMS 共用作者框架 | 9K/PMS 不应用 IIDX 黑白轨比；BGA 内容/时钟归 P1-L，仍逐 viewport 一个 player；不扩展选歌/结算皮肤面 |
| 作者制作与可选演出 | [完整套件](../../../skin-authoring/README.md)支持修改、检查、重做、打包、导入/更新；公开 scene/script 授权与隔离可用 | 原编辑器可拖拽既有组件、调整属性、导入图片并保存独立副本；完整 scene/script 没有可视化编辑器，历史 complex 仅作公共 API 参考。当前 simple/Aurora 可重复制作结果不等于旧三包演练重新执行 |
| 安装与发行 | 此前单内置候选已完成便携、自定义根、旧选择迁移、工作副本恢复和覆盖启动检查 | 9月13日外观修改及9月14日设置/编辑修改各有其自动证据，均不自动更新旧 ZIP 安装验收；独立账户非便携、设备/长期及公开发行组合门仍保留 |

C2～C6共享同一 package/layout/material/scene publication、lease/detach 与脚本隔离合同；无游玩宿主的菜单也检查整包。授权撤销不扩大 reload 准入，具体合同只在[技术约束](TECHNICAL_CONSTRAINTS.md)维护。

## 最近一次验证

2026-09-22 TOTAL 完整回归暴露29项旧皮肤测试预期失败，均已在修改前45d8613复现：28项寻找已移除的工作区，1项仍要求编辑器禁用。2026-09-23复核留存TRX及现行设置源码，失败身份与基线一致；具名清单见[TOTAL报告](../../other/BMS_TOTAL_RULES_AUDIT_20260922.md#完整回归失败的基线复现)。这是尚待整理的自动测试欠账，不恢复旧入口，也不代表下述功能专项或人工门被重新签收。

2026-09-14 完成固定目录与原编辑功能恢复的自动验证：

- core Debug focused **23/23**：default/Realm/folder 完整副本、真实 mania 预览的拖拽/属性/图片显示与重读、真实菜单打开/无编辑清理、保存后退出预览并只更新编辑模式、目录放入/修改/移出/放回、启动扫描退出等待、旧全局迁移及模式切换。
- BMS Debug focused **17/17**：外部编辑仍禁用、旧工作区后端保留、普通包整包重载、原 current 修改禁令、嵌套 importer 写入、设置关闭与 current pair 一致性。
- core Debug build 通过，0 警告/0 错误；最终 Desktop solution Release build 通过，0 错误。BMS 测试工程两项未修改代码告警为 `TestSceneFilesystemBackedStoryboardFallback.cs:151` 的 CS8600、`BmsRulesetStatisticsTest.cs:555` 的 CA2007，未屏蔽。
- 定点 whitespace 格式整理、文档检查与 `git diff --check` 通过。未重跑三工程 full，未代签新安装、完整歌曲、视觉、设备或长期体验。

命令、首败与修复见 [本次日志](CHANGELOG.md#2026-09-14固定皮肤目录与原编辑器恢复)。此前静线外观修改的测试、实绘和成品记录仍见 [轨道记录](../../other/SKIN_SIMPLE_LANE_PROPORTIONS_20260913.md)，不把本次编辑路径验证计为视觉重新签收。

## 此前单内置与布局验证

以下只作历史检索，不是最新代码的重新验收：[单内置退役与启动](CHANGELOG.md#放弃星轨并恢复唯一内置静线)、[构建/冷启动](../../other/SKIN_BUILTIN_BUILD_20260912.md)、[首轮布局](../../other/SKIN_SIMPLE_LAYOUT_20260912.md)、[底板与转盘](../../other/SKIN_SIMPLE_CABINET_20260912.md)、[判定/血槽与键面](../../other/SKIN_SIMPLE_STAGE_HUD_20260913.md)、[演奏信息](../../other/SKIN_SIMPLE_INFORMATION_20260913.md)、[C7制作与安装](../../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md)。旧作品与报告保留原样，当前入口不再逐轮复制测试数字。

## 当前风险与未完成项

- V-001～V-004未逐项签收、V-005未签收；用户对局部改进的认可不补填未观察矩阵，见[集中清单](../../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)。
- OmsSkin只保留历史证据与人工对照，源码物理移除仍待原实机门；安装故障不得重新启用它。
- G1 held-root/journal不是filesystem transaction，foreign addition/replacement可导致冻结；未知旧记录及无完整恢复证据的intent不猜测迁移。旧保存根事故只有事后保全，不追溯宣称无损。
- BmsBeatmapDecoderOptions.KeymodeOverride只是host/importer seam，普通loader无用户纠正UI；证据不足的sparse .bms/.bml仍拒绝，该缺口归P1-K。
- BGA内容/seek/decoder归P1-L，输入与设备归P1-B/D，真实LN/音频和发行验收见对应子线；本次收尾不继续开发这些阶段。
- 按模式选择只改变皮肤配置与启动/切换时的选择，不改变判定、输入、BGA 内容或布局 authority；视觉验收仍按原 V-001～V-005 门执行。

## 文档治理验证

2026-09-22 核对 `701893f` 的设置、扫描、草稿复制/保存/关闭应用代码及已有测试结果，校正合同与作者说明、记忆中的旧禁用和工作区入口表述。产品验证仍为 2026-09-14，未重跑 full/Release 或新安装与实机验收；文档与差异检查见[本次治理记录](CHANGELOG.md#2026-09-22工作区与文档记忆核对)。
