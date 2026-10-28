# OMS 当前开发状态

> 最后核对：2026-10-28（Omni当前源回归通过；v44剥页后完整CLI/新原生待闭合）
> 本页只保留全局状态与风险。执行顺序见[当前计划](DEVELOPMENT_PLAN.md)，专项事实从[子线路由](../subline/README.md)进入，历史见[CHANGELOG](CHANGELOG.md)。

## 一句话状态

OMS处于Phase 1.x后段，原生 BMS 与转谱游玩已完成热路径优化。静线是 BMS/mania 唯一内置、默认与保底，皮肤按模式和键数选择；固定目录刷新和原组件编辑器可保存独立副本。星轨已退役，静线外观打磨仍暂停。整体画面、真实设备与长期体验尚未签收，Skin V1 与 release 未完成。下载、BGA 作者能力和剩余门见 [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)，性能与听感见 [P1-J](../subline/P1-J/DEVELOPMENT_STATUS.md)。

Autoplay连续跳转、详细窗统计及独立Omni血条/33默认FC已修，具名证据取[P1-A修复记录](../subline/P1-A/CHANGELOG.md#2026-10-22autoplay跳转与omni反馈修复)；正常窗口剩余门由其STATUS维护，先前重进或开场测试不代签。

## 产品与仓库基线

依赖安全修复已入树；当前构建/回归、具体版本、正规许可和旧索引告警从[P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)进入。原生最终配方从零重编与发行输出仍分别待验。

游玩判定/结果及性能取[10月19日深审](../other/GAMEPLAY_PERFORMANCE_JUDGEMENT_20261019.md)；设备入口、输入时间、轴用途、长皿与校准接续取[10月21日输入审查](../other/INPUT_SIGNAL_REVIEW_20261021.md)。前台帧时间与实体设备仍待验收。

Lighting Model Skin Omni Until 33默认33，27～33及八支BPL为Game Frame；AUTO/HUD软件回归及私有诊断ZIP默认DP取景已有分项证据，v44已剥页，完整CLI/余下原生未闭合，无新交付。两页只核拟真可行性，达不到取消两页作品计划，保留OMS原界面与通用PageAPI。身份取[P1-A状态](../subline/P1-A/DEVELOPMENT_STATUS.md)，动作沿[作品计划](../subline/P1-A/DEVELOPMENT_PLAN.md#iidx-历代皮肤作品)。原HDD保全，作品私下分享、不内置或公开分发，原版校准、设备及发行门保持。

玩家直接登录固定OMSIR，保存后新局上传；新闻 / 帮助 / 排行 / 玩家沿原生页面。谱面列表先选模式：BMS 仅 Ginger Rush / 616，osu!mania 仅 Sayobot，恢复上次 BMS 来源；紧凑筛选、原生卡片和各来源阅读状态保持。正常OMS窗口的lazer体验仍待签收，隔离内容宽度检查不代签；源码 / 软件及剩余门取[P3-IR状态](../subline/P3-IR/DEVELOPMENT_STATUS.md)。社交后续，旧ppy关闭；Phase1.x原门保持。

- Windows-only，保留osu!mania与第一类BMS，Osu/Taiko/Catch已删除；离线优先，旧 osu! API endpoint 为空。固定 OMSIR 的直接登录与按需公开查榜沿 P3-IR，不扩大旧全套在线产品面。公共BMS难度表URL、用户指定Ginger Rush / 616 BMS与Sayobot原生mania镜像下载为窄例外，合同见 P1-A [BMS](../subline/P1-A/TECHNICAL_CONSTRAINTS.md#第三方-bms-浏览下载) / [mania](../subline/P1-A/TECHNICAL_CONSTRAINTS.md#sayobot-mania-浏览下载)。
- BMS直读`chartbms/`，mania直读`chartmania/`；支持portable `data/`与自定义数据根。主要工程为`osu.Desktop.slnf`、`osu.Game.Rulesets.Bms`及`oms.Input`。
- 当前协作分支为`master`。皮肤恢复/数据门`SV1-0`已关闭；迁移归档和四个无authority orphan blob继续保全，不能由scanner认领或清理。恢复事实见[恢复审计](../other/SKIN_SYSTEM_RECOVERY_20260710.md)及[数据/实机报告](../other/SKIN_SYSTEM_SV1_0_INVENTORY_20260713.md)。

## 工作区与开发环境

客户端入口见[AGENTS](../../AGENTS.md)；搬迁、工具链及环境证据取[迁移日志](CHANGELOG.md#2026-10-09)。用户已从当前工作区VS Code非调试进入Autoplay；完整谱库、旧待交及IR账号未因此签收。10月25日只读核对旧F:/oms容器已不存在；系统盘遗留另行收尾。

10月25日[工作区汇总清理](CHANGELOG.md#2026-10-25工作区垃圾清查与手动清理准备)已由用户执行并复核，324项目标全部移除，受保护文件与诊断源码备份一致。原拒绝记录保留，已完成清单不重复执行；作者源、验收/恢复材料和共享依赖保全，测试/作者工具下次先重编。

10月27日将E盘根目录散落的11个OMS目录归入`E:\OMS-Development\sources/`与`evidence/`，原文件及皮肤包保持，当前辅助脚本与验收链接已修正；历史路径换算和本轮核对见[归并记录](CHANGELOG.md#2026-10-27oms外置开发目录归并)。这是存储整理，不刷新产品、原版或设备验收。

## 当前执行门与全局风险

下载首次完成后的可玩状态已修复；后续维护、具名旧检查及实网成功门见[P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)与[计划](../subline/P1-A/DEVELOPMENT_PLAN.md)，不扩大全局在线能力。

| 顺序 | 当前事实与下一道门 | 归属 |
| --- | --- | --- |
| 1 | BGA 作者窗口、共享播放会话与完整手册已具软件和桌面证据；继续保留真实素材、窗口/DPI、设备与长时人工门，静线外观打磨仍暂停 | [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)、[P1-L](../subline/P1-L/DEVELOPMENT_STATUS.md) |
| 2 | canonical普通简洁包接管已实现；旧OmsSkin只保留历史/人工对照，物理删除仍待实机门 | [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md) |
| 3 | 输入时间、轴模式与校准软件已接通；真实窗口、跨设备手感与实体HID待签收 | [P1-B](../subline/P1-B/DEVELOPMENT_STATUS.md)、[P1-D](../subline/P1-D/DEVELOPMENT_STATUS.md) |
| 4 | 真实LN/CN/HCN、音频/特殊谱、BGA、选歌大库与发行组合仍需验收；原生/转谱热路径已优化，BGA多窗已共享内容源，证据与剩余门见记录 | [性能验证](../other/GAMEPLAY_PERFORMANCE_20260930.md) |
| 5 | V-001～V-005及当前完整候选人工签收，承接公共下载与离线启动组合；2026-07-14恢复和旧 ZIP 验收不能代替新增能力的最终包验证 | [集中清单](../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)、[P1-F](../subline/P1-F/DEVELOPMENT_STATUS.md)、[P1-G](../subline/P1-G/DEVELOPMENT_STATUS.md) |

谱库缺失恢复与历史保全、当前页难度表刷新及扫描热路径优化已实现，便携日志导出已修复宿主调用链，转谱补算的播放/图片资源已改为按需创建；真实大库响应、现场资源不足原因与 Explorer 选中文件仍取 [P1-H](../subline/P1-H/DEVELOPMENT_STATUS.md) 的剩余门。长伴奏暂停保位、手动转谱长条发声和单轨上限筛选的交互 / 听感门见 [P1-I](../subline/P1-I/DEVELOPMENT_STATUS.md)、[P1-J](../subline/P1-J/DEVELOPMENT_STATUS.md)。发行覆盖须保持原便携模式，非便携真实设备与公开发行组合验收归P1-F/P1-G。

旧 C7 安装 / 跨版本及保存根保全证据见 [P1-F](../subline/P1-F/DEVELOPMENT_STATUS.md)；首次误入保存根事故没有事前快照，不能追溯宣称无损，也不代签当前发行。

皮肤安全与失败回退取 [P1-A 合同](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)：无游玩中 reload / watcher，external 只读，授权撤销不扩大准入，C6 完成不代签 C7 / 人工门；异常归档只能定点取证。局部自动结果不证明完整选择链、视觉、硬件或特殊 Gimmick。

## 最近一次验证

2026-10-28：r22构建/Core、非交付诊断ZIP默认DP取景，以及仍属r20源的r21 BMS full取[P1-A最近验证](../subline/P1-A/DEVELOPMENT_STATUS.md#最近一次验证)和[日志](../subline/P1-A/CHANGELOG.md#2026-10-28omniauto状态与最终候选续验)。完整诊断CLI越过原失败点但未完，不称已修；新旧Replay仍跑。旧页面不签拟真/交付，原版130/190不提升为公式；正常窗口、设备与最终原生配方门保持。

2026-10-11转谱补算、Debug读取及旧失败对照保留原日期，范围与未验项取[P1-H](../subline/P1-H/DEVELOPMENT_STATUS.md#最近一次验证)。

下载完成状态的 2026-10-03 软件、两源 BMS 小包的 10 月 1 日桌面成功及 Sayobot 素材 / 原连接失败分别保留原日期，从 [P1-A 状态与证据索引](../subline/P1-A/DEVELOPMENT_STATUS.md)读取。Sayobot 真实包成功、修复后实站观察与大包 / 听感仍待验，旧连接结果不表示节点持续故障；已采用官网外链获取沿 [P3-IR 合同](../subline/P3-IR/TECHNICAL_CONSTRAINTS.md#身份结算与保存)。

2026-09-30 的 BGA 作者窗口 / 共享会话、完整手册及原生 / 转谱性能软件证据分别见 [作者能力验证](../other/BGA_SKIN_AUTHORING_20260930.md)与 [性能记录](../other/GAMEPLAY_PERFORMANCE_20260930.md)。真实素材、设备 / 听感、静线外观和发行门保留。

2026-10-12 P3-IR 模式→所属来源、BMS 来源恢复与等待关闭已有软件和隔离内容宽度证据，取 [模式分层记录](../subline/P3-IR/CHANGELOG.md#2026-10-12谱面列表模式与来源分层)；正常 OMS 窗口体验未签收。10 月 11 日等待 / 压缩和手动补交修复仍取 [具名网络来源](../../../oms-server/oms_client_bridge_md/doc_md/other/oms-ir-network-snapshot-20261011.md)；真实账号 / 两端同范围、P/C 和发行门保持。

历史回归按原报告保留失败身份与堆栈，后续修复取owning STATUS；专项通过不代表全仓库全绿。较早谱库、偏移/TOTAL及设置/编辑证据从相应STATUS或[CHANGELOG](CHANGELOG.md)检索。

最近一次静线外观修改仍为 2026-09-13 的 `234ce1f`，实绘见[轨道验证记录](../other/SKIN_SIMPLE_LANE_PROPORTIONS_20260913.md)。发行最新已有证据为 2026-10-03 候选的同包隔离启动/恢复/覆盖；生成物随后按用户要求删除，历史结果保留。范围与跨版本、设备/长期等剩余门只取 [P1-F](../subline/P1-F/DEVELOPMENT_STATUS.md#最近一次验证)，不据此新签收产品体验。

## 文档治理验证

2026-10-27完成外置目录归并和存储约束回写，目录/包完整性、辅助脚本语法、文档及总工作区链接检查通过，范围见[归并记录](CHANGELOG.md#2026-10-27oms外置开发目录归并)；产品、生产与人工证据日期保持。10月25日跨线核对取[前次记录](CHANGELOG.md#2026-10-25全工作区复核与遗漏状态回写)。

开发存储继续遵守 [AGENTS](../../AGENTS.md#开发磁盘约束)。旧系统盘产物回收和 Codex 全局迁移未完成，不能因文档检查通过而宣称已释放或零增长；待办保留在 [PLAN](DEVELOPMENT_PLAN.md#改动验收矩阵)。
