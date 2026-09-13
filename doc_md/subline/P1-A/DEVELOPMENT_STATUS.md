# P1-A 当前状态：Skin V1、产品面与 release gate

> 最后更新：2026-09-13（静线薄音符、大写判定与固定血槽读数；整体视觉仍待签收）
> 全局见[主线状态](../../mainline/DEVELOPMENT_STATUS.md)，后续门见[PLAN](DEVELOPMENT_PLAN.md)，实现合同见[TECHNICAL_CONSTRAINTS](TECHNICAL_CONSTRAINTS.md)，历史见[CHANGELOG](CHANGELOG.md)。

## 一句话状态

用户已明确放弃 complex，只继续打磨 simple。静线是唯一内置、首次默认与正式保底；星轨不再是内置选项、启动依赖或构建对象，历史作者文件仅保留参考。旧内置星轨选择迁回静线，普通用户导入的皮肤不清除；不再要求星轨视觉签收。静线仍在依据实机反馈打磨，尚未取得整体视觉签收。此前双内置、布局与安装证据保留历史身份，当前退役验证见下文。原七阶段不重计，程序化 `OmsSkin` 仍只供历史与人工对照，不重新进入回退链。当前迁移和单内置自动检查已完成，Skin V1/release 整体视觉与设备门仍未完成。

## 当前产品能力与剩余门

| 产品面 | 当前可用结果 | 尚未完成或必须保持的边界 |
| --- | --- | --- |
| 导入、选择与管理 | 可导入普通皮肤包，管理游戏内皮肤目录，或登记作者自己的目录；选择可在重启后保留 | 作者外部目录始终只读；已有用户数据恢复要求继续生效 |
| 修改后更新与失败保护 | 三种来源均可从设置手动重新载入整个皮肤；更新失败保留原来可用的外观 | 游玩和预览期间不能重新载入；不提供自动监听更新 |
| 游玩外观与动画 | BMS、mania 已接通适用的音符、长条、按键、判定、布局及动画，可响应实际游玩情况 | 两种玩法的专属组件不同；作者文件已具备，但星轨动画、美术安排与精细度已被用户否定，后续需修改并重新观察；未扩展选歌、结算等页面的皮肤制作范围 |
| 可选组合效果 | 第三方公开 scene/script 的授权、拒绝和撤销能力保留 | 星轨历史作者文件仅作 API 参考，不再是内置作品或视觉签收项；原 Momentum/C6 输入保留历史身份 |
| 作者独立制作 | [完整套件](../../../skin-authoring/README.md)提供静线源文件与历史 complex 参考、模板、完整公共参考、错误定位和打包/更新；已在独立发行工具目录、无 Git/SDK 的环境实际重做 Aurora Study，并验证三款作品可重复得到相同成品 | 制作命令准备独立可消耗副本，保留正式作品；游戏导入由同字节成品的实际游戏路径证明，鼠标操作未代签。无可视化编辑器，作者外部目录始终只读 |
| 唯一内置皮肤与安装恢复 | 安装后直接使用静线，保留普通作者文件与导出入口；工作副本损坏时保全旧件并从只读原件完整恢复。旧内置星轨选择迁回静线，普通用户导入包保留 | 原件缺失/损坏必须提示修复安装并阻止游玩/预览；未知旧记录、中断操作保全。旧程序化代码的物理移除仍受实机门约束，不能以保留代码恢复临时外观 |
| 实际安装与便携使用 | 此前 C7 完整多文件 ZIP 已完成首次便携、自定义保存、工作副本恢复与覆盖后启动四轮；另从上一修复版真实跨版本更新，游戏首次启动自动更新默认工作副本。集中验收包已在无 Git/SDK 的 PS5 环境组装 | 自动启动和目录证据不能代签鼠标体验、画面和设备；只交付最终包，中间候选保留为纠错证据 |
| 实际观感与设备体验 | 静线布局打磨已恢复；星轨已放弃，不再要求视觉签收 | V-001～V-004 签收 0/4，V-005 未签收；未提供具体键数/样式、设备或逐项观察证据，不代填矩阵，不把静线当已签收，见[集中清单](../../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md) |

此前作者包、真实制作、安装恢复、完整使用验证和独立复核证据集中于 [C7 报告](../../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md)。[集中体验包](../../../skin-c7-acceptance/README.md)提供静线作者包、作者练习、第三方输入，并保留原 V-001/V-005 历史输入和未签收记录。

C2～C6共用exact package+layout+material+scene publication，script及编译工作已加入同一owner/participant/lease/detach/retire。无gameplay host的菜单也先验证ini/manifest/scene/script/素材；授权撤销独立使旧host失权，不扩大Reload准入。完整合同只在[技术约束](TECHNICAL_CONSTRAINTS.md)维护。

## 当前质量打磨（进行中）

图10后的细节迭代：BMS音符收薄但落键边缘不动，判定大写并收拢连击；同源Stage素材模板将血槽外框、暗格、裁切亮格与百分比独立排版，GaugeHeight预留高度。图11真实desktop合成7K确认24%血量时完整槽体、薄音符及判定位置；无BGA且带测试边栏，不代签完整歌曲或整体质量。core focused90/90，BMS布局/选定包143/143，附加真实表面/裁切1/1；BMS full2242 Passed/0 Failed/16 Skipped，core Skin1359 Passed/5 Failed、mania863 Passed/4 Failed，剩余失败名称、类别及完整消息与冻结基线一致。作者完整制作/错误保护和Release通过，见[细节记录](../../other/SKIN_SIMPLE_STAGE_HUD_20260913.md)。下面图09结果属于此前迭代。

此前图07～09推动底板、键面、BGA内框、按键/命中光和分立仪表接入普通作者包，mania保留透明背景；`ScratchKeyWidth:2`放大独立转盘，连续键座与长白/短黑键改善控制台。纯纹理gauge完整暗槽/亮图裁切继续供兼容路径使用，当前静线模板见上段。原创金属/转盘原图及提示词保留。LITONE仅作完成度和分区参考，整体质量待签收。

图09当时验证：修正四处旧单图检查；安装准入及后续检查曾失败，独立复验和整组加载/切换均未再出现，不按执行顺序臆断原因。core/mania剩余失败匹配基线，Release与作者检查通过。完整计数、命令、首败及截图边界见[机台记录](../../other/SKIN_SIMPLE_CABINET_20260912.md#按键区与血槽继续打磨)。以下均为此前验证，保留历史身份。

## 最近一次验证

已完成：core focused 20/20，BMS 退役记录/用户包保留及真实 simple 双玩法输入 4/4；core Skin full 1343 Passed / 5 Failed、mania full 863 Passed / 4 Failed，失败名称、类别和完整消息经 Compare-FailureBaseline 逐项匹配冻结基线，无新增失败。单内置构建增删/摘要/缺失源 fixture 与独立作者套件正常制作、重复打包、错误拒绝及中断保护通过。BMS 未重跑 full：未改解析、判定或布局执行逻辑，使用上述对应真实路径 relevant 检查。实际独立单内置副本四轮启动、旧 complex 配置迁回 simple、custom root、损坏副本恢复及同包覆盖后正常关闭通过；未扩称已有个人库迁移或视觉签收。证据：artifacts/simple-only-*.log、artifacts/simple-only-baseline.json、artifacts/simple-only-startup-20260912/results.json。 下文为此前运行，保持历史身份。

二次素材打磨已完成本轮自动复验：BMS Release full 2219 Passed / 0 Failed / 15 Skipped（未提供核验备份根），素材与双玩法 focused 263/263；作者完整制作与重复打包、Release 构建通过。按新增图 06 调整 BMS 轨道底色与分隔线、暗底座、红判定线及原创转盘图；未修改运行时生产 C#，mania/complex 作者作品不变。分段血条 scene 试验因未知舞台目标被拒绝，已完全撤回，当前保留连续血条。素材与[作者预览板](../../../skin-authoring/dist/oms-simple-preview.png)不能代替游戏截图；过程与本轮结果见[二次素材打磨](../../other/SKIN_SIMPLE_LAYOUT_20260912.md#二次素材打磨与实机反馈)。以下数字属于此前验证。

此前两款长期内置选择完成当时代码的自动复验：首次直接选择、重启保留、实例复用与安装包恢复 focused 77/77；无需导入的真实 BMS/mania 计分通过；BMS full 的两项旧单内置预期修正后相关 6/6 通过，15 项备份根检查仍跳过。mania 863/867、core Skin 1345/1350，剩余 4/5 项失败与原精确消息一致；Release 成功。完整首败、修复、命令及边界见[双内置验证](../../other/SKIN_BUILTIN_SELECTION_20260912.md)，不代签实际画面。

上一轮静线布局的 BMS 完整复验 2213 Passed / 0 Failed / 15 Skipped（未提供已核验备份根）；最终包聚焦 67 项通过。mania 863/867、core Skin 1339/1344，四项与五项旧失败的名称、分类及完整消息逐项一致。Release 与作者重复生成/打包通过；完整命令、首败修复、摘要失配中止及边界见[本次验证记录](../../other/SKIN_SIMPLE_LAYOUT_20260912.md)。静线 BMS 落键区高度改为 `0.70`、7K 宽度 `0.25`，独立按键区 `KeyAreaHeight=0.12`，血条紧随其后；信息改用公开 `hud.text` 槽位，BMS 在底部、mania 在顶部，移除重复的额外连击。BGA 增加三个公开布局参数，内容播放不变；作者源、生成包及实际加载已同轮验证，自动结果不能代签实际画面。

BMS 公共信息区现独立占安全区底部 `8%`，因此未声明独立按键区的旧默认 `0.92` 落键区也会受可用空间约束缩至约 `0.878`；这是公共布局修复，并非只有静线变化。旧包未声明 `KeyAreaHeight` 时保留原按键区位置。星轨已退役，仅保留其此前公共布局验证的历史身份。

此前交付后修复、完整作者制作、安装恢复、跨版本更新及原失败身份只作为历史基线，详见 [C7 修复证据](../../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md#交付后默认皮肤提示与构建警告修复)。旧发行物和此前自动结果不表示本轮新包、实际画面或安装已验证；原保存根事故没有事前快照，不追溯宣称无损。原 `V-001`～`V-005`、设备及长期体验仍待实际签收。

## 当前风险与未完成项

- `BmsBeatmapDecoderOptions.KeymodeOverride`只是host/importer seam；普通loader传null，无用户纠正UI。证据不足的sparse .bms/.bml仍安全拒绝；可用性缺口归P1-K，不重开C3。
- scanner只在启动后对账一次；新增managed direct child须重启发现，已登记current内容只能手动Reload。没有watcher或live gameplay replacement。
- C1 held-root/journal不是filesystem transaction；foreign addition/replacement可导致冻结。current mutation的具体失败阶段与external零写入合同继续生效。
- 程序化 OmsSkin 已退出实际产品回退链，只保留历史和人工对照；源码物理删除仍须等原实机门，原七阶段非人工收口不代签该门，NotApplicable 也不能写成普遍 unsupported。
- 星轨已退出当前内置与签收范围；历史作者目录及旧发行证据不清除，普通导入包也不因同名而删除。静线仍须按真实画面继续打磨。
- BGA内容/timeline/seek/gimmick仍归P1-L；在线服务、sample pool、判定、binding不属C5。既有失败及已解决的原 sample 对照继续按[精确失败合同](TECHNICAL_CONSTRAINTS.md#测试与发布约束)逐项保留和比较，此前基线剩余 core 五项/mania 四项，不能按数量掩盖回归。

## 文档治理验证

2026-09-12 全项目对齐审计按生产接线与测试源码修正派生手册/记忆中程序化保底、尾部迁移对照及工具待交付的旧描述。只读核对三款源码与成品逐文件相同、成品摘要和原验收输入摘要一致；未改运行输入或重新签收体验，未重跑游戏、Release、制作或安装检查。详见[本线审计记录](CHANGELOG.md#项目实际内容文档与记忆对齐审计)。
