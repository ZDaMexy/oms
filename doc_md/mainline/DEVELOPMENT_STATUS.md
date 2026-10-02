# OMS 当前开发状态

> 最后核对：2026-10-02（下载审查缺口未修复；实网成功门保留，皮肤外观专项仍为 2026-09-13）
> 本页只保留全局状态与风险。执行顺序见[当前计划](DEVELOPMENT_PLAN.md)，专项事实从[子线路由](../subline/README.md)进入，历史见[CHANGELOG](CHANGELOG.md)。

## 一句话状态

OMS处于Phase 1.x后段。玩家可在游戏内从 Ginger Rush / 616 下载原包、自动入库并打开指定 BMS 难度；原生 BMS 与转谱游玩已完成热路径优化。皮肤作者可控制 BGA 窗口并使用随包完整教程和例子。静线是 BMS/mania 唯一内置、默认与保底，两种玩法可分别选择皮肤；固定目录刷新和原组件编辑器可保存独立副本。星轨已退役，静线外观打磨仍暂停。整体画面、真实设备与长期体验尚未签收，Skin V1 与 release 未完成。产品能力和剩余门见 [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)，性能与听感见 [P1-J](../subline/P1-J/DEVELOPMENT_STATUS.md)。

## 产品与仓库基线

静线已完成当前控制区、分段血槽、轨宽及演奏信息迭代：白黑/皿轨独立比例，BGA布局由皮肤声明，作者标级与表名/表内等级独立显示。正常开发启动、build/publish只同步simple源。当前暂停继续打磨，既有成果、最新验证和未完成门统一见[P1-A状态](../subline/P1-A/DEVELOPMENT_STATUS.md)；历史双内置和各轮截图不作为新的签收。

- Windows-only，保留osu!mania与第一类BMS，Osu/Taiko/Catch已删除；离线优先，Phase 3前OMS私有服务与默认endpoint为空。公共BMS难度表URL、用户指定Ginger Rush / 616 BMS与Sayobot原生mania镜像下载为窄例外，合同见 P1-A [BMS](../subline/P1-A/TECHNICAL_CONSTRAINTS.md#第三方-bms-浏览下载) / [mania](../subline/P1-A/TECHNICAL_CONSTRAINTS.md#sayobot-mania-浏览下载)。
- BMS直读`chartbms/`，mania直读`chartmania/`；支持portable `data/`与自定义数据根。主要工程为`osu.Desktop.slnf`、`osu.Game.Rulesets.Bms`及`oms.Input`。
- 当前协作分支为`master`。皮肤恢复/数据门`SV1-0`已关闭；迁移归档和四个无authority orphan blob继续保全，不能由scanner认领或清理。恢复事实见[恢复审计](../other/SKIN_SYSTEM_RECOVERY_20260710.md)及[数据/实机报告](../other/SKIN_SYSTEM_SV1_0_INVENTORY_20260713.md)。
- 2026-10-01 Sayobot接入开始时工作区干净，HEAD为`1cedd56`，已含两源BMS下载及原浏览视觉；`git fetch origin`成功，当时领先`origin/master`6、落后0。这是开工在线核对，不能当作收尾在线查询；未经用户确认不推送。

## 当前执行门与全局风险

下载增改全量审查确认BMS坏包收尾与两类内存边界须优先修复，其余完整性/重试/提示缺口同属[P1-A修复门](../subline/P1-A/DEVELOPMENT_PLAN.md#下载增改审查的修复门2026-10-02)。审查未改生产代码，不签收坏包或发行边界；证据见[审查记录](../other/BEATMAP_DOWNLOAD_REVIEW_20261002.md)。

| 顺序 | 当前事实与下一道门 | 归属 |
| --- | --- | --- |
| 1 | BGA 作者窗口、共享播放会话与完整手册已具软件和桌面证据；继续保留真实素材、窗口/DPI、设备与长时人工门，静线外观打磨仍暂停 | [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)、[P1-L](../subline/P1-L/DEVELOPMENT_STATUS.md) |
| 2 | canonical普通简洁包接管已实现；旧OmsSkin只保留历史/人工对照，物理删除仍待实机门 | [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md) |
| 3 | 输入软件基线可用；analog scratch跨设备、校准与真实HID尚未闭合 | [P1-B](../subline/P1-B/DEVELOPMENT_STATUS.md)、[P1-D](../subline/P1-D/DEVELOPMENT_STATUS.md) |
| 4 | 真实LN/CN/HCN、音频/特殊谱、BGA、选歌大库与发行组合仍需验收；原生/转谱热路径已优化，BGA多窗已共享内容源，证据与剩余门见记录 | [性能验证](../other/GAMEPLAY_PERFORMANCE_20260930.md) |
| 5 | V-001～V-005及候选发行包人工签收；2026-07-14恢复验收不能代替新增视觉与最终包验证 | [集中清单](../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)、[P1-G](../subline/P1-G/DEVELOPMENT_STATUS.md) |

谱库缺失恢复与历史保全、难度表当前页刷新、长伴奏暂停保位、手动转谱长条发声和单轨上限筛选已实现；软件验证与未完成的真实大库、交互和听感门分别见 [P1-H](../subline/P1-H/DEVELOPMENT_STATUS.md)、[P1-I](../subline/P1-I/DEVELOPMENT_STATUS.md)、[P1-J](../subline/P1-J/DEVELOPMENT_STATUS.md)。发行覆盖须保持原便携模式，非便携真实设备与公开发行组合验收归P1-F/P1-G。

此前 C7 完整包已完成实际安装恢复及从上一修复版跨版本覆盖，原保存根前后字节和属性一致；旧候选误入已有保存根的事故仍只有事后保全、没有该根事前快照，不能追溯宣称无损。发行和数据边界见 [P1-F 状态](../subline/P1-F/DEVELOPMENT_STATUS.md)。

皮肤安全与失败回退详见[P1-A技术约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)：当前并无live gameplay reload或watcher，external永久只读；授权撤销不扩大Reload准入，C6完成不等于C7或人工门关闭。异常期归档只能定点取证。局部自动测试不能代替完整真实选择链，自动证据也不能替代视觉、硬件或特殊Gimmick证明。

## 最近一次验证

2026-10-01 Sayobot原生mania已接入共用浏览入口、后台任务和原难度入库/打开，软件/素材窗口路径通过；实站查询筛选成功，但实际包TLS连接断开，实网成功门保留。详细规划、取证、修复及未签收范围见[P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)与[Sayobot记录](../other/MANIA_SAYOBOT_DOWNLOAD_20261001.md)，官网/私有服务和原人工门继续冻结。

2026-10-01交互修正：顶部下载入口回到音乐之前，来源→难度表→表内等级联动已可用；两个真实站点均完成Satellite 11级筛选、下载入库和稳定选歌。详细专项及原工具栏失败对照见 [三级筛选记录](../other/BMS_DOWNLOAD_FILTERS_20261001.md)，其它全局与人工门不变。

2026-10-01：玩家可在游戏内浏览 Ginger Rush 与 616，下载整包自动入库并打开指定原生 BMS 难度；后台通知、取消/重试及不打断游玩已实现。两个真实小包均通过桌面搜索→下载→入库→稳定选歌，软件回归和剩余听感/大包/网络体验门见 [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md) 与 [下载闭环记录](../other/BMS_DOWNLOAD_20261001.md)。不解除官方/私有在线冻结，也不新增皮肤/设备/发行签收。

2026-09-30 后续：BGA 窗口声明、共享播放会话与作者手册已实现，独立信息区保留、作者场景合法性和可运行例子一并验证；有效软件结果、桌面图像与完整回归对照统一见 [作者能力验证](../other/BGA_SKIN_AUTHORING_20260930.md)。不重新签收静线外观、设备听感或发行组合。

2026-09-30：按用户授权对原生 BMS 与 BMS→mania 完成性能复审和对应优化，覆盖按键/长条、声音维护、转谱常驻对象与 BGA 重复解码；前后指标、软件 gate 和既有失败对照统一见 [性能验证记录](../other/GAMEPLAY_PERFORMANCE_20260930.md)。真实设备听感、逐谱演出和发行门未重新签收。

完整回归没有新增失败，但并非全绿：逐项身份、原因与业务堆栈对照集中于上述报告，遗留测试维护归各 owning 子线。较早的谱库/选歌、偏移/TOTAL 和设置/编辑证据从相应 STATUS 或 [CHANGELOG](CHANGELOG.md)检索，不在本页累计历轮结果。

最近一次静线外观修改仍为 2026-09-13 的 `234ce1f`；该轮实绘与自动证据见[轨道验证记录](../other/SKIN_SIMPLE_LANE_PROPORTIONS_20260913.md)。此前发行 ZIP 和安装证据未随后续产品修改重新验收。

## 文档治理验证

2026-10-01：对照已提交产品代码和 9 月 30 日留存验证，统一主线、子线路由、多语使用说明、作者/发行入口与诊断记忆；清理已完成事项的待实施措辞和重复验证历史。仅文档与记忆治理，不重跑产品测试、不新增实机或发行签收；检查结果见[同步记录](CHANGELOG.md#2026-10-01)。

开发存储继续遵守 [AGENTS](../../AGENTS.md#开发磁盘约束)。旧系统盘产物回收和 Codex 全局迁移未完成，不能因文档检查通过而宣称已释放或零增长；待办保留在 [PLAN](DEVELOPMENT_PLAN.md#改动验收矩阵)。
