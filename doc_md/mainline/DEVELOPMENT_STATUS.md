# OMS 当前开发状态

> 最后核对：2026-10-13（专项进度与文档记忆同步；各产品及生产证据保持原日期）
> 本页只保留全局状态与风险。执行顺序见[当前计划](DEVELOPMENT_PLAN.md)，专项事实从[子线路由](../subline/README.md)进入，历史见[CHANGELOG](CHANGELOG.md)。

## 一句话状态

OMS处于Phase 1.x后段。玩家可在游戏内从 Ginger Rush / 616 下载原包、自动入库并打开指定 BMS 难度；原生 BMS 与转谱游玩已完成热路径优化。皮肤作者可控制 BGA 窗口并使用随包完整教程和例子。静线是 BMS/mania 唯一内置、默认与保底，两种玩法可分别选择皮肤；固定目录刷新和原组件编辑器可保存独立副本。星轨已退役，静线外观打磨仍暂停。整体画面、真实设备与长期体验尚未签收，Skin V1 与 release 未完成。产品能力和剩余门见 [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)，性能与听感见 [P1-J](../subline/P1-J/DEVELOPMENT_STATUS.md)。

## 产品与仓库基线

玩家直接登录固定OMSIR，保存后新局上传；新闻 / 帮助 / 排行 / 玩家沿原生页面。谱面列表先选模式：BMS 仅 Ginger Rush / 616，osu!mania 仅 Sayobot，恢复上次 BMS 来源；紧凑筛选、原生卡片和各来源阅读状态保持。正常OMS窗口的lazer体验仍待签收，隔离内容宽度检查不代签；源码 / 软件及剩余门取[P3-IR状态](../subline/P3-IR/DEVELOPMENT_STATUS.md)。社交后续，旧ppy关闭；Phase1.x原门保持。

客户端共用徽章、启动字标、窗口标题和应用图标已统一为 OMS；正常窗口与 Windows 图标表现待验，范围见 [P1-A 品牌计划](../subline/P1-A/DEVELOPMENT_PLAN.md#客户端品牌正常窗口验收)。静线布局、作者能力与按模式选择 / 刷新 / 编辑路径取 [P1-A 状态](../subline/P1-A/DEVELOPMENT_STATUS.md)；只同步 simple 源，外观打磨暂停。

- Windows-only，保留osu!mania与第一类BMS，Osu/Taiko/Catch已删除；离线优先，旧 osu! API endpoint 为空。固定 OMSIR 的直接登录与按需公开查榜沿 P3-IR，不扩大旧全套在线产品面。公共BMS难度表URL、用户指定Ginger Rush / 616 BMS与Sayobot原生mania镜像下载为窄例外，合同见 P1-A [BMS](../subline/P1-A/TECHNICAL_CONSTRAINTS.md#第三方-bms-浏览下载) / [mania](../subline/P1-A/TECHNICAL_CONSTRAINTS.md#sayobot-mania-浏览下载)。
- BMS直读`chartbms/`，mania直读`chartmania/`；支持portable `data/`与自定义数据根。主要工程为`osu.Desktop.slnf`、`osu.Game.Rulesets.Bms`及`oms.Input`。
- 当前协作分支为`master`。皮肤恢复/数据门`SV1-0`已关闭；迁移归档和四个无authority orphan blob继续保全，不能由scanner认领或清理。恢复事实见[恢复审计](../other/SKIN_SYSTEM_RECOVERY_20260710.md)及[数据/实机报告](../other/SKIN_SYSTEM_SV1_0_INVENTORY_20260713.md)。

## 工作区与开发环境

客户端当前位于 F:/zdamexy-workspace/oms，入口见 [AGENTS](../../AGENTS.md)；总工作区提供客户端、网站、后端与两桥路由。2026-10-09 搬迁、原 tracked 字节保全、工作副本连接与工具链迁移及开发环境验证已完成，证据只取 [迁移日志](CHANGELOG.md#2026-10-09)。仍待用户从新 VS Code 目录非调试启动确认谱库 / 待交及必要时重新登录 IR；旧 F:/oms 空容器和系统盘遗留分别待收尾。

2026-10-10 已在[VS Code工作区设置](../../.vscode/settings.json)为Pylance排除开发缓存 / 虚拟环境、验证产物与编译输出，主动打开的Python脚本仍可分析。配置与目录范围检查取[本轮日志](CHANGELOG.md#2026-10-10)，提示消失及编辑器实际表现待重新加载窗口确认；本轮不刷新客户端产品或发行验证。

2026-10-13 根目录历史日志已归档，工程 / 模板输出、两套旧截图探针和空临时目录清理已完成；Desktop 运行目录、曲库 / 存档、作者原件和验收证据在清理前后保全，范围只取 [清理日志](CHANGELOG.md#2026-10-13工作区残留专项清理)。后续开发按当前工程实际产物正常 restore / build，不能复用已清除输出的 `--no-build` / `--no-restore`。

同日后续品牌验证新建的 `.dev-cache/temp/brand-render-host` 仍保留：自动审批拒绝删除，源码 / 截图 / 日志已另存长期证据目录，具体见 [品牌记录](../subline/P1-A/CHANGELOG.md#2026-10-13客户端品牌标识专项)。此前扫描与补算探针的清理收尾取 [P1-H 历史](../subline/P1-H/CHANGELOG.md)，不能用较早清理结果宣称后续临时目录也已清除。

## 当前执行门与全局风险

下载首次完成后的可玩状态已修复；后续维护、具名旧检查及实网成功门见[P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)与[计划](../subline/P1-A/DEVELOPMENT_PLAN.md)，不扩大全局在线能力。

| 顺序 | 当前事实与下一道门 | 归属 |
| --- | --- | --- |
| 1 | BGA 作者窗口、共享播放会话与完整手册已具软件和桌面证据；继续保留真实素材、窗口/DPI、设备与长时人工门，静线外观打磨仍暂停 | [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)、[P1-L](../subline/P1-L/DEVELOPMENT_STATUS.md) |
| 2 | canonical普通简洁包接管已实现；旧OmsSkin只保留历史/人工对照，物理删除仍待实机门 | [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md) |
| 3 | 输入软件基线可用；analog scratch跨设备、校准与真实HID尚未闭合 | [P1-B](../subline/P1-B/DEVELOPMENT_STATUS.md)、[P1-D](../subline/P1-D/DEVELOPMENT_STATUS.md) |
| 4 | 真实LN/CN/HCN、音频/特殊谱、BGA、选歌大库与发行组合仍需验收；原生/转谱热路径已优化，BGA多窗已共享内容源，证据与剩余门见记录 | [性能验证](../other/GAMEPLAY_PERFORMANCE_20260930.md) |
| 5 | V-001～V-005及当前完整候选人工签收，承接公共下载与离线启动组合；2026-07-14恢复和旧 ZIP 验收不能代替新增能力的最终包验证 | [集中清单](../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)、[P1-F](../subline/P1-F/DEVELOPMENT_STATUS.md)、[P1-G](../subline/P1-G/DEVELOPMENT_STATUS.md) |

谱库缺失恢复与历史保全、当前页难度表刷新及扫描热路径优化已实现，便携日志导出已修复宿主调用链，转谱补算的播放/图片资源已改为按需创建；真实大库响应、现场资源不足原因与 Explorer 选中文件仍取 [P1-H](../subline/P1-H/DEVELOPMENT_STATUS.md) 的剩余门。长伴奏暂停保位、手动转谱长条发声和单轨上限筛选的交互 / 听感门见 [P1-I](../subline/P1-I/DEVELOPMENT_STATUS.md)、[P1-J](../subline/P1-J/DEVELOPMENT_STATUS.md)。发行覆盖须保持原便携模式，非便携真实设备与公开发行组合验收归P1-F/P1-G。

旧 C7 安装 / 跨版本及保存根保全证据见 [P1-F](../subline/P1-F/DEVELOPMENT_STATUS.md)；首次误入保存根事故没有事前快照，不能追溯宣称无损，也不代签当前发行。

皮肤安全与失败回退取 [P1-A 合同](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)：无游玩中 reload / watcher，external 只读，授权撤销不扩大准入，C6 完成不代签 C7 / 人工门；异常归档只能定点取证。局部自动结果不证明完整选择链、视觉、硬件或特殊 Gimmick。

## 最近一次验证

2026-10-13 客户端品牌的共用控件、启动、加载和两玩法资源选择已有软件、Desktop 双配置构建及隔离绘制证据；范围、初次失败和原件只取 [P1-A 品牌记录](../subline/P1-A/CHANGELOG.md#2026-10-13客户端品牌标识专项)。正常窗口 / 用户主题与曲库、DPI、图标缓存及后续候选仍待验，未重签皮肤、设备或发行门。

2026-10-11 转谱补算资源与 Debug 读取优化已有受控测量、回归及 Desktop 双配置证据；旧皮肤失败已逐项对照。范围、取舍与未验项只取 [P1-H 最新验证](../subline/P1-H/DEVELOPMENT_STATUS.md#最近一次验证)。扫描 / 便携宿主修复的 `d6f0d60` 来源见 [扫描审查](../other/LIBRARY_SCAN_REVIEW_20261010.md)。未重扫用户库，样本或探针不代签真实大库、Explorer 或发行。

下载完成状态的 2026-10-03 软件、两源 BMS 小包的 10 月 1 日桌面成功及 Sayobot 素材 / 原连接失败分别保留原日期，从 [P1-A 状态与证据索引](../subline/P1-A/DEVELOPMENT_STATUS.md)读取。Sayobot 真实包成功、修复后实站观察与大包 / 听感仍待验，旧连接结果不表示节点持续故障；已采用官网外链获取沿 [P3-IR 合同](../subline/P3-IR/TECHNICAL_CONSTRAINTS.md#身份结算与保存)。

2026-09-30 的 BGA 作者窗口 / 共享会话、完整手册及原生 / 转谱性能软件证据分别见 [作者能力验证](../other/BGA_SKIN_AUTHORING_20260930.md)与 [性能记录](../other/GAMEPLAY_PERFORMANCE_20260930.md)。真实素材、设备 / 听感、静线外观和发行门保留。

2026-10-12 P3-IR 模式→所属来源、BMS 来源恢复与等待关闭已有软件和隔离内容宽度证据，取 [模式分层记录](../subline/P3-IR/CHANGELOG.md#2026-10-12谱面列表模式与来源分层)；正常 OMS 窗口体验未签收。10 月 11 日等待 / 压缩和手动补交修复仍取 [具名网络来源](../../../oms-server/oms_client_bridge_md/doc_md/other/oms-ir-network-snapshot-20261011.md)；真实账号 / 两端同范围、P/C 和发行门保持。

已有完整回归记录并非全绿：逐项身份、原因与业务堆栈对照集中于上述报告，遗留测试维护归各 owning 子线；专项通过不代表整个仓库全绿。较早的谱库/选歌、偏移/TOTAL 和设置/编辑证据从相应 STATUS 或 [CHANGELOG](CHANGELOG.md)检索，不在本页累计历轮结果。

最近一次静线外观修改仍为 2026-09-13 的 `234ce1f`，实绘见[轨道验证记录](../other/SKIN_SIMPLE_LANE_PROPORTIONS_20260913.md)。发行最新已有证据为 2026-10-03 候选的同包隔离启动/恢复/覆盖；生成物随后按用户要求删除，历史结果保留。范围与跨版本、设备/长期等剩余门只取 [P1-F](../subline/P1-F/DEVELOPMENT_STATUS.md#最近一次验证)，不据此新签收产品体验。

## 文档治理验证

2026-10-13 专项审查覆盖 P1-A～M / P3-IR 当前入口、诊断记忆与两桥采用边界，补品牌和后续候选验收导航、纠正已完成项的待办语气，并固定具名证据链接。客户端文档、总工作区与差异检查结果取 [本轮收据](CHANGELOG.md#2026-10-13专项进度与文档记忆健康同步)。来源与 11 待复核保持；没有新产品、实网、生产或人工验证。

开发存储继续遵守 [AGENTS](../../AGENTS.md#开发磁盘约束)。旧系统盘产物回收和 Codex 全局迁移未完成，不能因文档检查通过而宣称已释放或零增长；待办保留在 [PLAN](DEVELOPMENT_PLAN.md#改动验收矩阵)。
