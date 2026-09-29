---
name: project_oms_bms_skin_authoring
description: BMS 皮肤用户决定、作者能力边界与常见错误入口的召回
metadata:
  node_type: memory
  type: project
---

# BMS 皮肤创作召回

当前可选包、public slot、视觉签收只读 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)；产品目标/硬约束读 [PLAN](../../doc_md/subline/P1-A/DEVELOPMENT_PLAN.md) / [CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)，作者正文唯一维护在随套件发行的[SKINNING](../../skin-authoring/docs/SKINNING.md)，旧doc_md同名页只作路由。皮肤异常任务先进入 [[reference_skin_recovery_20260710]]。

## 必须召回的用户决定

- 范围是 gameplay；不移植 LR2/beatoraja runtime，只对齐元素族与表达力。保持 .osk 分发、根 skin.ini、mania 素材/动画命名、目录编辑和拖入导入心智；BMS/scene/script 为版本化扩展，不要求 DLL。
- mania 普通 .osk 是固定行为宿主 + legacy 素材/参数，不能当作通用作者脚本上限。共享 neutral codec/scene/event/reload/sandbox，ruleset topology adapter 各自保留。
- 引擎掌握 gameplay truth、layout、滚动/LN 裁剪、池、BGA 内容时钟与安全；作者控制 scene/动画/只读响应。三态按 catalog 的 requirement/applicability 决定，不另列会漂移的 suppress 清单。
- canonical oms-simple 是唯一内置、默认与保底，覆盖 mania/BMS；用户已放弃 oms-complex，其文件只作历史作者样本，不再作为启动、构建或视觉签收对象。Authoring Kit 是可编辑源、模板、schema/事件/layout/预算说明、validator/diagnostics 与打包文档，不是第二套 SDK/runtime。
- 手册按普通作者从零制作、可见元素/实际文件/尺寸/帧、布局、绑定/状态机/脚本、上限、失败和刷新打包组织。README只导航；START_HERE、SKINNING、SCRIPTING、REFERENCE及素材/例子必须在发行套件内闭合，不能把不随包复制的doc_md当必要入口。First Scene与BGA Layout是完整小包，Reference Study需复制静线再覆盖三文件；明确片段与整包、工具检查与真实挂载、历史原样截图与素材示意。
- BGA作者控制安全范围内的窗口数量/位置/比例及fit/fill/stretch/none；游戏根会话掌握唯一播放内容与时钟，所有窗口含单窗都是只读view。无声明保留兼容默认，none不删除独立信息区，BMS转mania仍无BGA。详细合同不在memory重复，查P1-A/P1-L及[[reference_bms_bga_chain]]。
- canonical 普通包已接管产品渲染保底；程序化 OmsSkin 源码仍保留历史证据与人工对照，物理删除须等原实机 gate，不能把代码保留误读为尚未接管。canonical 损坏走明确安装修复，不暗落另一套程序化主题。
- 视觉采用集中签收；已获授权的自动可证切片可继续；用户明确暂停时须停止开发，具体边界见 P1-A STATUS。不得把“实现/自动通过，视觉待验收”写成产品/release 完成，或复用 2026-07-14 静态恢复签收。

## 常见误入口

- F2/F3/G2、Lua、reference-default 等是恢复期历史名称，不据此恢复代码或判断当前门。异常归档只定点取证，不整包恢复。
- core LegacySkin 不编译依赖 BMS；精确反射类型匹配必须排除 LegacyBeatmapSkin 等其它子类。
- 注入式 BeatmapNoteSkin fixture 不证明真实 public sidecar。WorkingBeatmap.Skin 的只读 legacy direct visual compatibility 可继续存在，但 public sections 不进入作者 resolver；重开新作者格式需独立完整产品 gate。
- lazer editor 的 ISerialisableDrawable/CLR Type JSON 不能复用为外部 scene manifest；scene 只接受版本化 allowlisted node ID。
- callback 返回后量 stopwatch 无法阻止 while true；sandbox 必须可抢占并有 instruction/heap/node/resource quota。
- schema 来自生产组件与合同，SKINNING 是派生说明，不反向把旧说明当实现需求。
- Windows上的JSON缩进序列化会写CRLF，即使最后只追加LF；配方中的多行文字还会受C#源码检出换行影响。保留源文件有`eol=lf`时，所有作者输出须主动规范LF，否则新检出后源文件打包不再等于随包成品。核对真实新建、生成、重复打包及源与成品字节，而不是仅检查当前工作目录内的两次打包相同。
- 场景文字本身作为`hud.text`所有者时采用HUD区域，容器下的显式global子文字可沿安全屏幕定位；模板同理。仅通过离线schema检查不能证明最终位置，需真实挂载测量。零缩放会被绘图矩阵夹到极小值，进度条用固定容器下的子图宽度归零避免细线。
- 作者`check`能拒绝BGA语法/范围/预算，不能离线确定屏幕/样式碰撞。整组无窗查runtime.log的`bms.layout.bga-viewports-unavailable`，没有专属设置错误面板；零窗仍校验BGA场景素材和所有者。工具通过、旧截图与新增测试代码都不是本轮视觉签收；会话/窗口/例子的独立证据见[作者能力记录](../../doc_md/other/BGA_SKIN_AUTHORING_20260930.md)。

## 诊断导航

手册示例也要真实选包并在两玩法挂载：Common部件（含BGA）不能写入Bms扩展section；目标的ruleset才限定玩法。测试负例须有同基线成功例，防止全被无效声明掩盖。暂停冻结整个游玩scene，运行状态机不能即时刷新暂停菜单；脚本有限数值越界会钳制，不等于VM故障。

| 现象 | 优先进入 |
| --- | --- |
| ordinary .osk stream、14K 第二皿素材 | 恢复期保留修正是 base parser 前 rewind stream、S2/P2 素材；先查当前回归，见 [[reference_skin_recovery_20260710]] |
| lane 相对宽修改无效、hit position/裁剪漂移 | [[reference_bms_default_skin_geometry]]、[[reference_gameplay_skin_layout_snapshot]]；同比缩放全部 relative width 会被归一化抵消，HitTargetVerticalOffset 保持时序合同 |
| 缺 Keys 被当成声明、同名贴图/width 跨包拼接 | [[reference_gameplay_skin_config_presence]]、[[reference_gameplay_skin_lane_resource_compatibility]] |
| LN body 异步到达后颜色/状态不对 | 真实 DrawableBmsHoldNote 是 Idle/Holding/Broken authority；新 visual 立即投影当前态，不自建 gameplay state；常数只查 P1-A 的 LN 视觉合同 |
| 原位文件修改未生效、旧资源释放过早 | [[reference_skin_atomic_reload_detach]]；active immutable instance 不观察磁盘，manual Reload 是统一入口 |
| 旧 external/managed 记录的 copy/rename/delete 或源丢失 | [[reference_skin_external_workspace_managed_copy]]、[[reference_skin_managed_folder_mutation_foundation]]；external 永久只读 |
| 换皮肤显示/窗口导致BGA重播、POOR丢失或decoder重建 | [[reference_bms_bga_chain]]与P1-L；正式游玩会话由ruleset持有，显示只退役views，退出根才释放player |
| 窄 foundation 被写成整轮交付 | [[project_oms_skin_product_progress]] |

不在此页重复 campaign 燃尽、slot 数量、candidate 表或测试数字。
