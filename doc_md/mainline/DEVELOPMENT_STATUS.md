# OMS 当前开发状态

> 最后核对：2026-10-11（扫描交付、固定 OMSIR 与清理收尾同步；各项软件、人工和发行证据保留原日期）
> 本页只保留全局状态与风险。执行顺序见[当前计划](DEVELOPMENT_PLAN.md)，专项事实从[子线路由](../subline/README.md)进入，历史见[CHANGELOG](CHANGELOG.md)。

## 一句话状态

OMS处于Phase 1.x后段。玩家可在游戏内从 Ginger Rush / 616 下载原包、自动入库并打开指定 BMS 难度；原生 BMS 与转谱游玩已完成热路径优化。皮肤作者可控制 BGA 窗口并使用随包完整教程和例子。静线是 BMS/mania 唯一内置、默认与保底，两种玩法可分别选择皮肤；固定目录刷新和原组件编辑器可保存独立副本。星轨已退役，静线外观打磨仍暂停。整体画面、真实设备与长期体验尚未签收，Skin V1 与 release 未完成。产品能力和剩余门见 [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)，性能与听感见 [P1-J](../subline/P1-J/DEVELOPMENT_STATUS.md)。

## 产品与仓库基线

玩家从原账号窗口直接登录固定 OMSIR，登录即允许保存后新局上传；从选歌 / 本人记录的具体谱面进入公开榜，未登录也可主动查榜，顶部通用奖杯已移除。2026-10-08 原版 osu-web 官网已部署待验收，最新客户端软件、网站实际运行与真实账号 / 下载、VS Code 非调试 OMS 及先导 P / 完整 C 的未签收项只取[P3-IR 状态](../subline/P3-IR/DEVELOPMENT_STATUS.md)。旧 osu! API 地址空与在线总开关 false 不变，日常验收不生成 Windows 发行物，Phase 1.x 原门保留。

静线当前布局、作者能力与按模式选择 / 刷新 / 编辑路径统一见 [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)；开发构建只同步 simple 源，外观打磨暂停，历史双内置和旧截图不代签当前体验。

- Windows-only，保留osu!mania与第一类BMS，Osu/Taiko/Catch已删除；离线优先，旧 osu! API endpoint 为空。固定 OMSIR 的直接登录与按需公开查榜沿 P3-IR，不扩大旧全套在线产品面。公共BMS难度表URL、用户指定Ginger Rush / 616 BMS与Sayobot原生mania镜像下载为窄例外，合同见 P1-A [BMS](../subline/P1-A/TECHNICAL_CONSTRAINTS.md#第三方-bms-浏览下载) / [mania](../subline/P1-A/TECHNICAL_CONSTRAINTS.md#sayobot-mania-浏览下载)。
- BMS直读`chartbms/`，mania直读`chartmania/`；支持portable `data/`与自定义数据根。主要工程为`osu.Desktop.slnf`、`osu.Game.Rulesets.Bms`及`oms.Input`。
- 当前协作分支为`master`。皮肤恢复/数据门`SV1-0`已关闭；迁移归档和四个无authority orphan blob继续保全，不能由scanner认领或清理。恢复事实见[恢复审计](../other/SKIN_SYSTEM_RECOVERY_20260710.md)及[数据/实机报告](../other/SKIN_SYSTEM_SV1_0_INVENTORY_20260713.md)。

## 工作区与开发环境

客户端当前位于 F:/zdamexy-workspace/oms，入口见 [AGENTS](../../AGENTS.md)；总工作区提供客户端、网站、后端与两桥路由。2026-10-09 已完成文件搬迁、原 tracked 字节保全核对、两个关联工作副本连接修复，以及 Python / 工具链活动入口迁移。旧 F:/oms 仅有六个空目录容器，旧 Codex oms 项目保留历史；用户不再在旧项目协作。

2026-10-10 已在[VS Code工作区设置](../../.vscode/settings.json)为Pylance排除开发缓存 / 虚拟环境、验证产物与编译输出，主动打开的Python脚本仍可分析。配置与目录范围检查取[本轮日志](CHANGELOG.md#2026-10-10)，提示消失及编辑器实际表现待重新加载窗口确认；本轮不刷新客户端产品或发行验证。

2026-10-10 经用户授权逐项核对后，已清理工程、测试、模板与作者工具中可再生成的构建输出；Desktop整个运行目录、曲库 / 存档、原作者包、历史验收 / 发行资料和依赖缓存保留。客户端运行目录的文件内容及元数据前后一致，下一次开发启动正常restore / build，不复用已清除工程的`--no-build`或`--no-restore`。实际回收与验证取[清理日志](CHANGELOG.md#确认后清理构建输出)，产品与实机日期保留。

2026-10-11 用户已手动清理扫描审查的两个临时探针目录，本轮只读复核均不存在；源码、前后测量和 TRX 仍在长期证据目录。此前自动删除拦截保留为历史，收尾取 [P1-H 日志](../subline/P1-H/CHANGELOG.md#临时探针清理收尾)；旧 F:/oms 空目录及系统盘遗留是不同待办。

新路径下普通 Desktop Debug restore / build、脚本语法、三套 Java SDK 探针编译、统一文档及客户端文档校验通过；本轮方法、原失败、证据位置和人工接续见 [迁移日志](CHANGELOG.md#2026-10-09)。尚待用户在新 VS Code 目录非调试启动确认谱库 / 待交及必要时重新登录 IR；旧空目录清理被自动审批拦截，本轮保留，后续待监视释放后人工清理。此结果只验证迁移后的开发环境，不代签原软件来源、玩家体验、生产或发行门。

## 当前执行门与全局风险

下载首次完成后的可玩状态已修复；后续维护、具名旧检查及实网成功门见[P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)与[计划](../subline/P1-A/DEVELOPMENT_PLAN.md)，不扩大全局在线能力。

| 顺序 | 当前事实与下一道门 | 归属 |
| --- | --- | --- |
| 1 | BGA 作者窗口、共享播放会话与完整手册已具软件和桌面证据；继续保留真实素材、窗口/DPI、设备与长时人工门，静线外观打磨仍暂停 | [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)、[P1-L](../subline/P1-L/DEVELOPMENT_STATUS.md) |
| 2 | canonical普通简洁包接管已实现；旧OmsSkin只保留历史/人工对照，物理删除仍待实机门 | [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md) |
| 3 | 输入软件基线可用；analog scratch跨设备、校准与真实HID尚未闭合 | [P1-B](../subline/P1-B/DEVELOPMENT_STATUS.md)、[P1-D](../subline/P1-D/DEVELOPMENT_STATUS.md) |
| 4 | 真实LN/CN/HCN、音频/特殊谱、BGA、选歌大库与发行组合仍需验收；原生/转谱热路径已优化，BGA多窗已共享内容源，证据与剩余门见记录 | [性能验证](../other/GAMEPLAY_PERFORMANCE_20260930.md) |
| 5 | V-001～V-005及当前完整候选人工签收，承接公共下载与离线启动组合；2026-07-14恢复和旧 ZIP 验收不能代替新增能力的最终包验证 | [集中清单](../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)、[P1-F](../subline/P1-F/DEVELOPMENT_STATUS.md)、[P1-G](../subline/P1-G/DEVELOPMENT_STATUS.md) |

谱库缺失恢复与历史保全、当前页难度表刷新及扫描热路径优化已实现，便携日志导出已修复宿主调用链；真实大库响应、现场资源不足原因与 Explorer 选中文件仍取 [P1-H](../subline/P1-H/DEVELOPMENT_STATUS.md) 的剩余门。长伴奏暂停保位、手动转谱长条发声和单轨上限筛选的交互 / 听感门见 [P1-I](../subline/P1-I/DEVELOPMENT_STATUS.md)、[P1-J](../subline/P1-J/DEVELOPMENT_STATUS.md)。发行覆盖须保持原便携模式，非便携真实设备与公开发行组合验收归P1-F/P1-G。

旧 C7 安装 / 跨版本及保存根保全证据见 [P1-F](../subline/P1-F/DEVELOPMENT_STATUS.md)；首次误入保存根事故没有事前快照，不能追溯宣称无损，也不代签当前发行。

皮肤安全与失败回退取 [P1-A 合同](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)：无游玩中 reload / watcher，external 只读，授权撤销不扩大准入，C6 完成不代签 C7 / 人工门；异常归档只能定点取证。局部自动结果不证明完整选择链、视觉、硬件或特殊 Gimmick。

## 最近一次验证

2026-10-10～11 扫描优化、素材匹配与混合玩法增量、历史身份保全和便携日志定位已有隔离软件及普通 Desktop 双配置证据，来源提交为 `d6f0d60`；范围、受控收益与未验项只取 [P1-H 最新验证](../subline/P1-H/DEVELOPMENT_STATUS.md#最近一次验证)。本轮文档同步未重扫用户库，不将探针调用链或样本耗时提升为真实大库、Explorer 或发行签收。

2026-10-03下载完成状态修复已有软件与Release证据：首次成功后可直接打开所选难度，删除/目录不可用目标不能误显示可玩。当前能力、验证范围、原坏包/目录修复及未完成门统一见[P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)；修复后实站观察、真实大包/听感及原人工门保留。

2026-10-01的两源BMS小包桌面成功、Sayobot素材路径与实站查询/包连接失败证据见[P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)及其历史记录。Sayobot真实包成功入库仍未签收，旧连接结果不代表节点持续故障。官网批准两BMS源 / Sayobot元数据查询与原包外链获取沿[P3-IR合同](../subline/P3-IR/TECHNICAL_CONSTRAINTS.md#身份结算与保存)；未采用的官网谱包、聊天、多人和旧在线面仍冻结。

2026-09-30 的 BGA 作者窗口 / 共享会话、完整手册及原生 / 转谱性能软件证据分别见 [作者能力验证](../other/BGA_SKIN_AUTHORING_20260930.md)与 [性能记录](../other/GAMEPLAY_PERFORMANCE_20260930.md)。真实素材、设备 / 听感、静线外观和发行门保留。

2026-10-10 P3-IR已恢复已实现网站的客户端入口：谱面详情 / 榜、个人网页和玩家排行按真实身份与范围连接；有效软件、编译及HTTP范围取[专项最新验证](../subline/P3-IR/DEVELOPMENT_STATUS.md#最近一次验证)。真实点击、网页同范围和原P/C / 发行门仍待用户验收，网站运行与旧证据保持各自来源日期。

已有完整回归记录并非全绿：逐项身份、原因与业务堆栈对照集中于上述报告，遗留测试维护归各 owning 子线；专项通过不代表整个仓库全绿。较早的谱库/选歌、偏移/TOTAL 和设置/编辑证据从相应 STATUS 或 [CHANGELOG](CHANGELOG.md)检索，不在本页累计历轮结果。

最近一次静线外观修改仍为 2026-09-13 的 `234ce1f`，实绘见[轨道验证记录](../other/SKIN_SIMPLE_LANE_PROPORTIONS_20260913.md)。发行最新已有证据为 2026-10-03 候选的同包隔离启动/恢复/覆盖；生成物随后按用户要求删除，历史结果保留。范围与跨版本、设备/长期等剩余门只取 [P1-F](../subline/P1-F/DEVELOPMENT_STATUS.md#最近一次验证)，不据此新签收产品体验。

## 文档治理验证

2026-10-11 对照当前客户端、P1-A～M / P3-IR 与全部诊断记忆，同步扫描交付、已维护的 mania 注册检查、固定 OMSIR 操作和探针清理；跨仓只纠正采用措辞及旧版本被写成当前的落点。账号 / 榜软件仍取 `21daf78`，扫描 / portable 修复取 `d6f0d60`；本轮治理不刷新二者验证或生产日期，事实登记原字节和 11 项待复核保留。确切基线、保全范围、命令与结果见 [本轮日志](CHANGELOG.md#专项进度与文档记忆健康同步)，较早治理取 [历史](CHANGELOG.md#七项目进度与文档记忆复核)。

开发存储继续遵守 [AGENTS](../../AGENTS.md#开发磁盘约束)。旧系统盘产物回收和 Codex 全局迁移未完成，不能因文档检查通过而宣称已释放或零增长；待办保留在 [PLAN](DEVELOPMENT_PLAN.md#改动验收矩阵)。
