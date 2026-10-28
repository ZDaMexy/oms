# OMS 当前开发规划

> 最后核对：2026-10-28（Omni当前两页方案取消；gameplay与原产品/发行门保持）
> 本页维护全局顺序、跨线依赖和改动验收；当前事实见[STATUS](DEVELOPMENT_STATUS.md)，专项动作从[子线路由](../subline/README.md)进入，历史见[CHANGELOG](CHANGELOG.md)。

## 当前目标

依赖安全迁移及10月24日完整BMS补验已作为后续输入，适用范围取[P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md#最近一次验证)，没有新改动或失败时不重跑该门；按[P1-L](../subline/P1-L/DEVELOPMENT_PLAN.md#当前执行顺序)补最终配方从零重编与原生身份检查。图片使用正规程序集许可，不能靠屏蔽审计或沿用旧发行包替代当前依赖验证；新候选的载荷/许可旁带归[P1-F](../subline/P1-F/DEVELOPMENT_PLAN.md#当前剩余验收)。

按[P1-A修复后复核](../subline/P1-A/DEVELOPMENT_PLAN.md#iidx-历代皮肤作品)在正常窗口检查Autoplay跳转、统计及新包血条/默认FC；沿下方P1-B/P1-E/P1-J/P1-L与P1-G验证真实设备、长条/键音/BGA和长期体验。[既有游玩审查](../other/GAMEPLAY_REVIEW_20261019.md)作为输入，不扩成无现场依据的重构。正常窗口、隔离宿主与发行证据分开。

2026-10-18用户授权IIDX历代第三方皮肤，缺少的表现能力随真实组件补齐。当前Omni SongSelect/Results扩展方案按拟真硬门取消，保留OMS原界面，v44剔除样机，不留该方案实施待办；未来仅新可靠拟真路线才重评。gameplay持续推进，通用PageAPI保留，范围和顺序只取[P1-A计划](../subline/P1-A/DEVELOPMENT_PLAN.md#iidx-历代皮肤作品)，不恢复静线打磨或改变原release门。

先按[P3-IR 原生页面接续计划](../subline/P3-IR/CLIENT_NATIVE_SERVICES_PLAN.md)在用户正常OMS窗口对标lazer，验已交付模式→所属来源、BMS来源恢复、加载中关闭与阅读体验。按已授权的独立IR与完整玩家网站范围，沿[P3-IR真人接续](../subline/P3-IR/DEVELOPMENT_PLAN.md#原版官网部署后的接续)验收原账号、保存后的新局 / 断网补交、网站公开最佳可见记录及同谱同来源 / 条件，先完成OMS＋全量历史＋ED7K的P，再完成指定宿主与玩法的C；继续维护备份、增长和同库回退，不重复未变已过门。用户通过VS Code非调试启动当前工作区，发行构建自行执行，不生成额外Windows包或安装副本；旧 osu! API endpoint 与下方Phase 1.x门不变。

按 [P1-A 品牌正常窗口验收](../subline/P1-A/DEVELOPMENT_PLAN.md#客户端品牌正常窗口验收)观察共用徽章、启动字标、窗口标题 / 图标和用户主题 / DPI；软件与隔离截图作为输入，结果由 P1-G 汇总，后续实际候选由 P1-F 承接。

下载已交付能力及修复作为后续输入，结果只在[P1-A状态](../subline/P1-A/DEVELOPMENT_STATUS.md)维护；改动归档、浏览或完成状态时的回归与具名旧检查维护见[P1-A计划](../subline/P1-A/DEVELOPMENT_PLAN.md#下载维护与回归)，不恢复旧入口或放宽输入失败语义。

Ginger Rush与616/Alvorna后续真实窗口、首次完成、大包/网络/完整歌曲体验见[P1-A计划](../subline/P1-A/DEVELOPMENT_PLAN.md#第三方-bms-剩余体验验收)。不自动推进独立在线试听、整表下载或续传，既有皮肤/设备/发行门保留。

Sayobot真实纯mania及混合小包成功入库与稳定打开仍待补验，动作见[P1-A计划](../subline/P1-A/DEVELOPMENT_PLAN.md#sayobot-mania-实网成功验收)。网络结论沿最近留存实测日期读取，不把素材或HEAD结果替代实际下载；下载不启用官网/旧在线链，独立 IR 只沿 P3-IR。

以已有软件与桌面证据为输入，后续补齐 BGA/作者作品的设备、真实素材与长时验收，归 [P1-A](../subline/P1-A/DEVELOPMENT_PLAN.md) / [P1-L](../subline/P1-L/DEVELOPMENT_PLAN.md)；静线外观打磨仍暂停。谱库、声音与单轨筛选的隔离根、大库、听感与交互验收归 [P1-H](../subline/P1-H/DEVELOPMENT_PLAN.md)、[P1-J](../subline/P1-J/DEVELOPMENT_PLAN.md)、[P1-I](../subline/P1-I/DEVELOPMENT_PLAN.md)。已交付实现与验证只在 STATUS/CHANGELOG 维护，不重复开发或据此关闭人工门。

交付Windows-only、离线优先OMS。Phase 1.x完成必须同时满足：

1. BMS/mania主流程与本地数据升级不阻断用户。
2. Skin V1静线及第三方包、公开三态作者能力及canonical fallback通过P1-A全部门，程序化主题视觉退出产品链；用户包缺件/损坏仍可玩。
3. 输入、LN/CN/HCN、键音/BGA经真实设备与谱面验收。
4. portable、自定义根及覆盖更新保全用户内容。
5. Release及约定focused/full测试达到有效基线，已知失败逐项归因。

## 强制执行顺序

恢复与数据安全`SV1-0`、Skin C1～C7既有工程结果作为后续输入保留，不重复开发；星轨最新总体体验不通过，历史关闭记录不表示作品修改已完成。用户认可静线最近调整并暂停外观打磨；设置页收简、按模式皮肤选择、固定目录刷新与原组件编辑功能恢复已完成，作为后续输入，不自行进入视觉下一阶段。星轨已放弃，仅保留历史参考，详见 P1-A 状态。P1-A的**七个持久campaign预算、共同执行规则和剩余人工退出门**只在[P1-A PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md#七个持久campaign预算与剩余退出门)维护；不重计、不拆新阶段。

### R3：`SV1-2` G1 存储与 revision 冻结输入

保持C1～C6的既有行为和安全门，完整合同见[P1-A技术约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)。不另建layout、event、material或publication authority，不整包恢复异常期代码。

### R4：完成 Skin V1 sandbox 与 canonical 发行闭环

当前静线质量目标、Global背景、独立转盘宽度与固定血槽合同见[机台结构记录](../other/SKIN_SIMPLE_CABINET_20260912.md)；LITONE仅作对照，后续以合格 beatmania style 的读谱、控制区与信息层级为准，不恢复complex。

1. **C6已闭合**：可选脚本、真实作者入口/consumer与隔离能力、最终整包reload/G1自动门见[P1-A结果](../subline/P1-A/DEVELOPMENT_STATUS.md)。
2. **C7 作品迭代**：既有工程和安装证据保留，静线控制区、轨宽与演奏信息已有实际截图和自动验证，当前暂停继续打磨；星轨已放弃。仅静线随安装内置、默认与保底，不重计阶段。具体当前迭代范围、待改内容及剩余签收见 [P1-A 计划](../subline/P1-A/DEVELOPMENT_PLAN.md)。
3. 修改验证后按[集中清单](../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)签收V-001～V-005及修改后的静线；自动结果不得代签实际画面与设备体验。未签收不得称Skin V1/release完成。

具体source、权限、预算、回退与journal迁移条件均以[P1-A C6/C7退出门](../subline/P1-A/DEVELOPMENT_PLAN.md)为准。P1-L继续拥有BGA内容/timeline/seek；不扩大beatmap-local作者面或移植LR2/beatoraja runtime。

### R5：Phase 1 玩法与硬件收尾

2026-09-29 用户指定的原生 BMS 与 BMS→mania 独立自动键音设置已完成软件交付；剩余听感/设备门统一见 [P1-J](../subline/P1-J/DEVELOPMENT_PLAN.md)，由 P1-G 汇总，不推进新 gameplay Mod 或其它冻结功能。

2026-09-29 用户指定的「自动调整偏移」互斥 style 已完成软件交付：保留 lazer 的上一局校准，增加 BMS beatoraja 显示时机调整；剩余真实设备、实谱收敛和回放体验验收由 [P1-C](../subline/P1-C/DEVELOPMENT_PLAN.md#2026-09-29-用户指定自动调整偏移-style-互斥)维护，不恢复常驻反馈卡或推进其它冻结功能。

TOTAL 的作者声明、家族缺省与新旧成绩版本合同由 [P1-C](../subline/P1-C/DEVELOPMENT_PLAN.md) / [P1-K](../subline/P1-K/DEVELOPMENT_PLAN.md) 共同守门；后续变化必须同时验证演奏、回放和结算，不改变既有 Gauge Mod 选择与人工验收边界。

| 子线 | 下一动作与依赖 |
| --- | --- |
| P1-J/P1-K | 保持C3 lane/keymode/shared-store authority，验收手动转谱长条、暂停续播与自动键音听感；极端dense治理须有现场证据 |
| P1-B/P1-D | 以[输入专项](../other/INPUT_SIGNAL_REVIEW_20261021.md)为软件输入，验真实窗口/实体设备的轴校准、连皿/长皿、热插拔及同型号设备；只向皮肤提供只读状态 |
| P1-C/P1-E | 保持判定parity；验收LN/CN/HCN、长BGM、密集键音和各keymode组合，不恢复已删除常驻反馈卡 |
| P1-I | 单轨上限段已实现；完成真实拖拽手感、窄窗口及大库体验验收 |
| P1-L/P1-G | 验收游戏持有的BGA会话与作者窗口在真实素材、窗口/DPI和长时下的表现；反向滚动缺口保留，汇总皮肤/输入/长条/选歌/BGA人工release清单 |
| P1-H | 缺失恢复、历史保全、当前页难度表刷新及扫描优化已有软件证据；补隔离数据根 / 真实大库与导出通知定位，按每根日志分析现场读取错误；谱面scanner经验不授予皮肤mutation authority |

### R6：公开发行门

P1-F结合P1-G统一复核：

- 当前客户端品牌、启动字标、窗口标题与应用图标；正常窗口及后续实际候选按 [P1-A 品牌计划](../subline/P1-A/DEVELOPMENT_PLAN.md#客户端品牌正常窗口验收)验收，旧候选不覆盖新增美术。
- P1-A全部Skin V1门、最终静线与第三方包、公开选择面、canonical完整性/原子恢复及无程序化主题fallback。
- `portable.ini → data/`、bootstrap storage中的`storage.ini`与自定义根；保持已验证的完整包、覆盖工具与保存位置合同，补独立账户非便携及设备/长时发行体验。
- Release build/publish、BMS full、mania/core relevant及各子线要求的测试；失败逐项稳定归因。
- 发布说明区分code-provider/ini/scene/script能力，不宣称未通过门的G1、脚本、格式兼容或在线能力。
- 剩余发行组合按 [P1-F](../subline/P1-F/DEVELOPMENT_PLAN.md)承接公共下载、后台任务、失败/取消、入库选歌与离线启动；实网成功和大包/听感门从 [P1-A](../subline/P1-A/DEVELOPMENT_PLAN.md)进入，由 [P1-G](../subline/P1-G/DEVELOPMENT_PLAN.md)汇总，不以同包自动启动或开发软件结果代签。独立 IR 真人门归 P3-IR，不新增到 Phase 1.x 必过矩阵。

## 冻结项

- P1-M播放器在R3～R6/release门前不抢占工作；除产品明确改序外保持后置。
- 已提前实现的Phase 2能力不代表Phase 1完成；1P/2P flip、dan、BSS/MSS等继续冻结。已授权的Omni Start/Floating/甩盘专项取[P1-A合同](../subline/P1-A/TECHNICAL_CONSTRAINTS.md#既有产品面硬约束未被-skin-v1-取代)，不据局部交互与读数签收完整原机FHS等价。
- 2026-10-03 用户授权的 [P3-IR](../subline/P3-IR/DEVELOPMENT_PLAN.md) 独立按需服务已进入主动连接 / 公网试运行；2026-10-05 后续批准来源的官网谱面获取沿外部玩家网站专项推进。旧 osu! API endpoint、聊天、多人与自动更新仍冻结；既有客户端公共下载沿 P1-A，不扩大旧全套在线面。
- 不盲目同步上游，只按[UPSTREAM](../other/UPSTREAM.md)选择性cherry-pick。

## 改动验收矩阵

当前工作区已获非调试进入Autoplay的现场证据，完整谱库 / 旧待交 / IR账号仍待核实。后续只对新产生且确认闲置的可再生成内容清理，按[当前工作区](DEVELOPMENT_STATUS.md#工作区与开发环境)归档核对，不重复执行已闭合清单。每次新shell遵守[开发磁盘约束](../../AGENTS.md#开发磁盘约束)，先重编相应工程、测试和作者工具，不直接使用`--no-build`。旧系统盘产物和Codex全局迁移不在本次工作区清理范围；后者仍须等待其它活动任务结束并核对聊天、数据库、工作副本与重启恢复。

| 改动面 | 最低自动验证 | 额外人工验证 |
| --- | --- | --- |
| BMS parser/gameplay | BMS focused + BMS full | 命中特殊谱时逐谱验收 |
| 仅 BMS ruleset 内皮肤组件且不改 shared/mania/fallback authority | BMS skin focused + BMS relevant/full + Release | 对应 keymode、选择/回落与新增视觉实机 |
| shared skin、mania compatibility、scene/event 或 fallback authority | core skin focused + mania/BMS relevant + 所属子线要求的 full + Release | 受影响 keymode/style/选择/fallback；静线与 canonical 恢复按 C7 合同复验 |
| 输入 | `oms.Input`/bridge focused + BMS relevant | 真实控制器 edge/hold/轴 |
| 存储/Realm | importer/scanner focused + Release | 备份数据根上的升级/重扫/恢复 |
| 音频/BGA | 对应 player/store/cache focused + BMS full | pause/seek、长样本、逐谱视听 |
| 发行 | Release build/publish | 冷启动、portable/custom root、覆盖更新 |
| 仅文档/协作规则 | 文档检查 + diff 检查；审阅状态、合同与引用一致性 | 不复用或刷新产品/实机验证日期 |
| 开发检查脚本 | 对应 runtime 的真实正反 fixture + 文档/diff 检查 | 涉及产品打包/启动时追加对应发行门 |

`--no-build` 的当前产物前提、并发协调及重复验证条件见 [AGENTS](../../AGENTS.md#并行与验证协调)。新失败按具体测试身份和错误归因；通过所需门后，不因习惯扩大测试。
