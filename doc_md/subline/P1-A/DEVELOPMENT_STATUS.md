# P1-A 当前状态：Skin V1、产品面与 release gate

> 最后更新：2026-09-14（设置页收简与按模式选择皮肤切片）
> 全局见[主线状态](../../mainline/DEVELOPMENT_STATUS.md)，后续门见[PLAN](DEVELOPMENT_PLAN.md)，稳定合同见[TECHNICAL_CONSTRAINTS](TECHNICAL_CONSTRAINTS.md)，历史见[CHANGELOG](CHANGELOG.md)。

## 一句话状态

静线是唯一内置、首次默认与正式保底；complex 已退役，历史作者文件与验证证据保留，普通用户导入包不删除。用户认可最近调整并决定暂时停止外观打磨；本轮按新增需求完成设置页收简和按模式选择皮肤，未进入下一阶段。BMS 与 osu!mania 现在各自记住皮肤，旧全局配置首次启动会复制到两个模式；切换模式时立即应用对应选择。此反馈不等于原 V-001～V-005、真实设备或长期体验签收，Skin V1/release 仍未完成。原 C1～C7 工程证据不重计，程序化 OmsSkin 已退出实际回落链，物理删除仍待原实机门。

## 当前产品能力与剩余门

| 产品面 | 当前可用结果 | 必须保持的边界与未完成项 |
| --- | --- | --- |
| 导入、选择与管理 | 普通 .osk、受管目录、登记作者外部目录；BMS 与 osu!mania 可分别选择并跨重启保留 | external 始终只读；新增受管目录需启动扫描发现；首次启动从旧全局皮肤复制模式偏好，失效/受保护项回到静线 |
| 更新与失败保护 | 设置可手动重新载入整包，失败保留此前外观 | 游玩/预览期间不可 reload；无 watcher 或游玩中替换 |
| 唯一内置与恢复 | 正常开发启动、build/publish 从作者源自动同步 simple；安装原件验证后恢复工作副本，旧内置 complex 选择迁回 simple | canonical 原件故障须提示修复安装并阻止游玩/预览，不恢复程序化主题；不按同名清除用户包 |
| 静线游玩外观 | 文件素材提供完整底板、轨道、转盘/键帽、固定分段血槽和 BGA 外框；白黑/皿轨独立比例、分隔线补偿、14K 每侧键序一致 | 已依据用户参考反复改进；当前暂停继续打磨，完整歌曲、其它尺寸及设备体验未整体签收 |
| 实时演奏信息 | 实时判定与断连统计、EX SCORE/HiSpeed、MIN/当前/MAX BPM；曲名、作者标级与独立的表名/表内等级 | 读取规则真实状态；表归类不替代作者难度，皮肤不改变判定/计分、时间或难度表导入 |
| 皮肤布局与公共能力 | BMS 公开键区、血槽、BGA 大小/位置及上下信息区；可选黑键轨宽，无声明旧包保持普通键等宽；mania/BMS 共用作者框架 | 9K/PMS 不应用 IIDX 黑白轨比；BGA 内容/时钟归 P1-L，仍逐 viewport 一个 player；不扩展选歌/结算皮肤面 |
| 作者制作与可选演出 | [完整套件](../../../skin-authoring/README.md)支持修改、检查、重做、打包、导入/更新；公开 scene/script 授权与隔离可用 | 无可视化编辑器；历史 complex 仅作公共 API 参考。当前 simple/Aurora 可重复制作结果不等于旧三包演练重新执行 |
| 安装与发行 | 此前单内置候选已完成便携、自定义根、旧选择迁移、工作副本恢复和覆盖启动检查 | 9月13日皮肤后续更新只验证当前 Release 与成品，不自动更新旧 ZIP 验收；独立账户非便携、设备/长期及公开发行组合门仍保留 |

C2～C6共享同一 package/layout/material/scene publication、lease/detach 与脚本隔离合同；无游玩宿主的菜单也检查整包。授权撤销不扩大 reload 准入，具体合同只在[技术约束](TECHNICAL_CONSTRAINTS.md)维护。

## 最近一次验证

2026-09-14完成设置面收简与按模式选择皮肤的产品切片：

- 顶层启动、旧全局皮肤迁移、模式切换和两项偏好独立性 **7/7** 通过；新增 `TestRulesetSpecificSkinPreferences` 覆盖 BMS → mania 切换后的实际皮肤应用。
- Folder Skin Workspace 空状态回归 **7/7** 通过；作者脚本、作者按钮与恢复提示在无对应内容时按需隐藏，已有记录/故障仍保留原入口。
- 真实设置 caller 的既有删除、授权与当前 revision 选择回归 **5/5** 通过。
- `dotnet build osu.Game/osu.Game.csproj --no-restore -p:Configuration=Debug -m -verbosity:minimal` 通过（0 警告、0 错误）。本切只影响设置/配置选择链，未刷新视觉、设备或 Release 人工签收。

最近产品修改为 `234ce1f`（2026-09-13，轨宽与仪表底色），本次治理未重跑或刷新其验证日期：

- focused 185通过；BMS Release full 2265通过、5项旧素材预期失败、16跳过。保持生产代码不变，修正分隔线和14K第二侧角色预期后相关7项重建复验通过；未重新跑整个full，不合并计数冒称单次全绿。16跳过为15项缺核验备份根、1项headless截图入口。
- 真实 desktop 图17捕获、作者检查19项、最终Release（0警告/0错误）和两个成品各119项逐条内容核对通过；合成7K截图含测试边栏、无BGA媒体，不能代签用户完整歌曲。
- 此修改仅BMS布局与普通作者文件，core/mania full未重跑；较早信息区修改的对应结果另存[信息区记录](../../other/SKIN_SIMPLE_INFORMATION_20260913.md)。原已知core五项/mania四项失败按精确身份保留，不用数量替代归因。

命令、首败、修复、摘要和图17见[轨道验证记录](../../other/SKIN_SIMPLE_LANE_PROPORTIONS_20260913.md)。

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

2026-09-14在上述基线之上核对按模式配置、规则集切换回落、设置页空状态与测试 caller；本次代码与文档同步不改变视觉签收门。文档检查及跨线范围统一见[主线治理记录](../../mainline/CHANGELOG.md#项目事实与文档记忆全量同步)；设置切片的自动结果见本页“最近一次验证”，不代表新安装或实机视觉签收。
