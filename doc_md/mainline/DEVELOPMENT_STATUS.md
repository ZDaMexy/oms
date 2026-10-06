# OMS 当前开发状态

> 最后核对：2026-10-05（P3-IR 原登录 / 个人页适配软件门与外部试运行；全项目其他产品和人工日期保留）
> 本页只保留全局状态与风险。执行顺序见[当前计划](DEVELOPMENT_PLAN.md)，专项事实从[子线路由](../subline/README.md)进入，历史见[CHANGELOG](CHANGELOG.md)。

## 一句话状态

OMS处于Phase 1.x后段。玩家可在游戏内从 Ginger Rush / 616 下载原包、自动入库并打开指定 BMS 难度；原生 BMS 与转谱游玩已完成热路径优化。皮肤作者可控制 BGA 窗口并使用随包完整教程和例子。静线是 BMS/mania 唯一内置、默认与保底，两种玩法可分别选择皮肤；固定目录刷新和原组件编辑器可保存独立副本。星轨已退役，静线外观打磨仍暂停。整体画面、真实设备与长期体验尚未签收，Skin V1 与 release 未完成。产品能力和剩余门见 [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)，性能与听感见 [P1-J](../subline/P1-J/DEVELOPMENT_STATUS.md)。

## 产品与仓库基线

2026-10-03 按用户授权实现客户端主动连接、保存后交分、待交恢复与端内查榜，独立服务 / 网页已部署试运行，50 人代表性容量和一致恢复通过；用户通过 VS Code 非调试启动核对同一局、断网重启与账号归属，不需要 agent 生成发行包，见 [P3-IR](../subline/P3-IR/DEVELOPMENT_STATUS.md)。默认 endpoint 和旧在线总开关保持原值，Phase 1.x 人工 / 发行门不变。

2026-10-04 [多播放器、来源混榜与 LR2IR 历史榜](../subline/P3-IR/DEVELOPMENT_PLAN.md#多播放器与-lr2-历史榜实施)已完成正式审查并实施客户端来源查询与实际规则投射；软件、服务运行和真人门分开记录，不刷新全项目产品验证或关闭原门。

2026-10-05外部多来源/全量历史已完成运行恢复门并部署，当前“已部署待验收”；[多来源客户端](../subline/P3-IR/DEVELOPMENT_STATUS.md#多来源扩展状态)来源选择/规则投射的软件门已有证据，同条件限已证明OMS组。接续VS Code非调试OMS、ED7K先导及完整宿主真人，P/C和Phase1.x未关闭。

2026-10-05 按用户采用将 IR 账号与本人记录接入原 lazer 用户按钮、登录窗口与个人页，奖杯仅查榜；原账号界面 / 隔离软件门与普通 Desktop 编译通过。实际 osu-web 官网已部署待验收，准确外部来源、维护与未完门见 [P3-IR](../subline/P3-IR/DEVELOPMENT_STATUS.md#玩家当前能做什么)。不重新启用旧在线生命周期；用户非调试真机、两端真实对照与 P/C 未签收。

2026-10-06 完整玩家网站候选d1 / 22b仍未部署；R6实际资源失败保留，完整F保全退役后已启动相同上限的新全量R7。当前外部来源、运行 / 空间 / 发布与真人未完门只取 [P3-IR当前来源](../subline/P3-IR/DEVELOPMENT_STATUS.md#当前客户端与外部候选来源)，客户端运行源码234未改，原全项目人工 / 发行门保持。

静线已完成当前控制区、分段血槽、轨宽及演奏信息迭代：白黑/皿轨独立比例，BGA布局由皮肤声明，作者标级与表名/表内等级独立显示。正常开发启动、build/publish只同步simple源。当前暂停继续打磨，既有成果、最新验证和未完成门统一见[P1-A状态](../subline/P1-A/DEVELOPMENT_STATUS.md)；历史双内置和各轮截图不作为新的签收。

- Windows-only，保留osu!mania与第一类BMS，Osu/Taiko/Catch已删除；离线优先，默认 endpoint 为空。用户授权的独立 IR 主动连接 / 试运行沿 P3-IR，不扩大旧全套在线产品面。公共BMS难度表URL、用户指定Ginger Rush / 616 BMS与Sayobot原生mania镜像下载为窄例外，合同见 P1-A [BMS](../subline/P1-A/TECHNICAL_CONSTRAINTS.md#第三方-bms-浏览下载) / [mania](../subline/P1-A/TECHNICAL_CONSTRAINTS.md#sayobot-mania-浏览下载)。
- BMS直读`chartbms/`，mania直读`chartmania/`；支持portable `data/`与自定义数据根。主要工程为`osu.Desktop.slnf`、`osu.Game.Rulesets.Bms`及`oms.Input`。
- 当前协作分支为`master`。皮肤恢复/数据门`SV1-0`已关闭；迁移归档和四个无authority orphan blob继续保全，不能由scanner认领或清理。恢复事实见[恢复审计](../other/SKIN_SYSTEM_RECOVERY_20260710.md)及[数据/实机报告](../other/SKIN_SYSTEM_SV1_0_INVENTORY_20260713.md)。

## 当前执行门与全局风险

下载首次完成后的可玩状态已修复；后续维护、具名旧检查及实网成功门见[P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)与[计划](../subline/P1-A/DEVELOPMENT_PLAN.md)，不扩大全局在线能力。

| 顺序 | 当前事实与下一道门 | 归属 |
| --- | --- | --- |
| 1 | BGA 作者窗口、共享播放会话与完整手册已具软件和桌面证据；继续保留真实素材、窗口/DPI、设备与长时人工门，静线外观打磨仍暂停 | [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)、[P1-L](../subline/P1-L/DEVELOPMENT_STATUS.md) |
| 2 | canonical普通简洁包接管已实现；旧OmsSkin只保留历史/人工对照，物理删除仍待实机门 | [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md) |
| 3 | 输入软件基线可用；analog scratch跨设备、校准与真实HID尚未闭合 | [P1-B](../subline/P1-B/DEVELOPMENT_STATUS.md)、[P1-D](../subline/P1-D/DEVELOPMENT_STATUS.md) |
| 4 | 真实LN/CN/HCN、音频/特殊谱、BGA、选歌大库与发行组合仍需验收；原生/转谱热路径已优化，BGA多窗已共享内容源，证据与剩余门见记录 | [性能验证](../other/GAMEPLAY_PERFORMANCE_20260930.md) |
| 5 | V-001～V-005及当前完整候选人工签收，承接公共下载与离线启动组合；2026-07-14恢复和旧 ZIP 验收不能代替新增能力的最终包验证 | [集中清单](../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)、[P1-F](../subline/P1-F/DEVELOPMENT_STATUS.md)、[P1-G](../subline/P1-G/DEVELOPMENT_STATUS.md) |

谱库缺失恢复与历史保全、难度表当前页刷新、长伴奏暂停保位、手动转谱长条发声和单轨上限筛选已实现；软件验证与未完成的真实大库、交互和听感门分别见 [P1-H](../subline/P1-H/DEVELOPMENT_STATUS.md)、[P1-I](../subline/P1-I/DEVELOPMENT_STATUS.md)、[P1-J](../subline/P1-J/DEVELOPMENT_STATUS.md)。发行覆盖须保持原便携模式，非便携真实设备与公开发行组合验收归P1-F/P1-G。

此前 C7 完整包已完成实际安装恢复及从上一修复版跨版本覆盖，原保存根前后字节和属性一致；旧候选误入已有保存根的事故仍只有事后保全、没有该根事前快照，不能追溯宣称无损。发行和数据边界见 [P1-F 状态](../subline/P1-F/DEVELOPMENT_STATUS.md)。

皮肤安全与失败回退详见[P1-A技术约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)：当前并无live gameplay reload或watcher，external永久只读；授权撤销不扩大Reload准入，C6完成不等于C7或人工门关闭。异常期归档只能定点取证。局部自动测试不能代替完整真实选择链，自动证据也不能替代视觉、硬件或特殊Gimmick证明。

## 最近一次验证

2026-10-03下载完成状态修复已有软件与Release证据：首次成功后可直接打开所选难度，删除/目录不可用目标不能误显示可玩。当前能力、验证范围、原坏包/目录修复及未完成门统一见[P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)；修复后实站观察、真实大包/听感及原人工门保留。

2026-10-01的两源BMS小包桌面成功、Sayobot素材路径与实站查询/包连接失败证据见[P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)及其历史记录。Sayobot真实包成功入库仍未签收，旧连接结果不代表节点持续故障；授权独立 IR 试运行见 P3-IR，官网谱包、聊天、多人等仍冻结。

2026-09-30 后续：BGA 窗口声明、共享播放会话与作者手册已实现，独立信息区保留、作者场景合法性和可运行例子一并验证；有效软件结果、桌面图像与完整回归对照统一见 [作者能力验证](../other/BGA_SKIN_AUTHORING_20260930.md)。不重新签收静线外观、设备听感或发行组合。

2026-10-06 完整 osu-web 玩家站当前外部d1 / 22b候选已执行全量、1,800秒和两空恢复分项，但验收进程资源门失败，仍待保全复核、同上限复验及联动发布；生产b520与真人待验收状态不提升。批准源网页原包获取 / 个人公开最佳 / 两玩法分榜及客户端原账号、mania六判定保持；准确来源和剩余门见[P3-IR](../subline/P3-IR/DEVELOPMENT_STATUS.md#当前客户端与外部候选来源)。

2026-09-30：按用户授权对原生 BMS 与 BMS→mania 完成性能复审和对应优化，覆盖按键/长条、声音维护、转谱常驻对象与 BGA 重复解码；前后指标、软件 gate 和既有失败对照统一见 [性能验证记录](../other/GAMEPLAY_PERFORMANCE_20260930.md)。真实设备听感、逐谱演出和发行门未重新签收。

已有完整回归记录并非全绿：逐项身份、原因与业务堆栈对照集中于上述报告，遗留测试维护归各 owning 子线；专项通过不代表整个仓库全绿。较早的谱库/选歌、偏移/TOTAL 和设置/编辑证据从相应 STATUS 或 [CHANGELOG](CHANGELOG.md)检索，不在本页累计历轮结果。

最近一次静线外观修改仍为 2026-09-13 的 `234ce1f`，实绘见[轨道验证记录](../other/SKIN_SIMPLE_LANE_PROPORTIONS_20260913.md)。发行最新已有证据为 2026-10-03 候选的同包隔离启动/恢复/覆盖；生成物随后按用户要求删除，历史结果保留。范围与跨版本、设备/长期等剩余门只取 [P1-F](../subline/P1-F/DEVELOPMENT_STATUS.md#最近一次验证)，不据此新签收产品体验。

## 文档治理验证

2026-10-05仅同步P3-IR外部实际发布、公开匿名读榜和维护/未完成门，专项治理结果见下方P3-IR入口；客户端源码和全项目产品/人工日期不变，没有新Windows包。

2026-10-04：全项目入口、相关合同、派生说明与诊断记忆复核，修正泛化在线冻结、漏记已有候选证据及已完成单轨筛选的旧待办；后置播放器 BGA 依赖回链现行合同。范围、取证和健康检查见[日志](CHANGELOG.md#项目进度与文档记忆一致性复核)。仅文档/验收说明与记忆，没有产品重验、实网探针、打包或人工代签。

[P3-IR 专项外部投影复核](../subline/P3-IR/DEVELOPMENT_STATUS.md#文档治理验证)分别保留2026-10-04旧结果和2026-10-05发布后文档水位；现行v1、已采纳多来源合同和独立真人门保持。其余未改变事实的子线不机械刷新日期，旧治理记录从CHANGELOG检索。

开发存储继续遵守 [AGENTS](../../AGENTS.md#开发磁盘约束)。旧系统盘产物回收和 Codex 全局迁移未完成，不能因文档检查通过而宣称已释放或零增长；待办保留在 [PLAN](DEVELOPMENT_PLAN.md#改动验收矩阵)。
