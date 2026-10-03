# OMS 当前开发规划

> 最后核对：2026-10-03（全项目剩余动作与发行承接已复核；产品验证与人工日期保留）
> 本页维护全局顺序、跨线依赖和改动验收；当前事实见[STATUS](DEVELOPMENT_STATUS.md)，专项动作从[子线路由](../subline/README.md)进入，历史见[CHANGELOG](CHANGELOG.md)。

## 当前目标

2026-10-03 用户授权提前推进独立 OMS IR 并直接部署：候选端内保存后交分 / 待交恢复和 50 人服务已具软件、容量 / 恢复及生产证据，后续由用户在候选包验收真实游玩、网页同一局、断网重启与原账号恢复。范围和退出条件统一见 [P3-IR](../subline/P3-IR/DEVELOPMENT_PLAN.md)；这项改序不关闭下方 Phase 1.x 门，也不填入默认 endpoint。

下载已交付能力及修复作为后续输入，结果只在[P1-A状态](../subline/P1-A/DEVELOPMENT_STATUS.md)维护；改动归档、浏览或完成状态时的回归与具名旧检查维护见[P1-A计划](../subline/P1-A/DEVELOPMENT_PLAN.md#下载维护与回归)，不恢复旧入口或放宽输入失败语义。

Ginger Rush与616/Alvorna后续真实窗口、首次完成、大包/网络/完整歌曲体验见[P1-A计划](../subline/P1-A/DEVELOPMENT_PLAN.md#第三方-bms-剩余体验验收)。不自动推进独立在线试听、整表下载或续传，既有皮肤/设备/发行门保留。

Sayobot真实纯mania及混合小包成功入库与稳定打开仍待补验，动作见[P1-A计划](../subline/P1-A/DEVELOPMENT_PLAN.md#sayobot-mania-实网成功验收)。网络结论沿最近留存实测日期读取，不把素材或HEAD结果替代实际下载，也不启用官网/私有服务。

以已有软件与桌面证据为输入，后续补齐 BGA/作者作品的设备、真实素材与长时验收，归 [P1-A](../subline/P1-A/DEVELOPMENT_PLAN.md) / [P1-L](../subline/P1-L/DEVELOPMENT_PLAN.md)；静线外观打磨仍暂停。谱库、声音与单轨筛选的隔离根、大库、听感与交互验收归 [P1-H](../subline/P1-H/DEVELOPMENT_PLAN.md)、[P1-J](../subline/P1-J/DEVELOPMENT_PLAN.md)、[P1-I](../subline/P1-I/DEVELOPMENT_PLAN.md)。已交付实现与验证只在 STATUS/CHANGELOG 维护，不重复开发或据此关闭人工门。

交付Windows-only、离线优先OMS。Phase 1.x完成必须同时满足：

1. BMS/mania主流程与本地数据升级不阻断用户。
2. Skin V1静线及第三方包、公开三态作者能力及canonical fallback通过P1-A全部门，程序化主题视觉退出产品链；用户包缺件/损坏仍可玩。
3. 输入、LN/CN/HCN、键音/BGA经真实设备与谱面验收。
4. portable、自定义根及覆盖更新保全用户内容。
5. Release及约定focused/full测试达到有效基线，已知失败逐项归因。

## 强制执行顺序

恢复与数据安全`SV1-0`、Skin C1～C7既有工程结果作为后续输入保留，不重复开发；星轨最新总体体验不通过，历史关闭记录不表示作品修改已完成。用户认可静线最近调整并暂停外观打磨；按新增需求完成设置页收简、按模式皮肤选择、固定目录刷新与原组件编辑功能恢复，随后仍不自行进入视觉下一阶段。星轨已放弃，仅保留历史参考，详见 P1-A 状态。P1-A的**七个持久campaign预算、共同执行规则和剩余人工退出门**只在[P1-A PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md#七个持久campaign预算与剩余退出门)维护；不重计、不拆新阶段。

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
| P1-B/P1-D | analog scratch跨设备edge/hold、真实HID、deadzone/sensitivity、模式说明与live diagnostics；只向皮肤提供只读状态 |
| P1-C/P1-E | 保持判定parity；验收LN/CN/HCN、长BGM、密集键音和各keymode组合，不恢复已删除常驻反馈卡 |
| P1-I | 单轨上限段已实现；完成真实拖拽手感、窄窗口及大库体验验收 |
| P1-L/P1-G | 验收游戏持有的BGA会话与作者窗口在真实素材、窗口/DPI和长时下的表现；反向滚动缺口保留，汇总皮肤/输入/长条/选歌/BGA人工release清单 |
| P1-H | 缺失恢复、跨目录同内容保全与当前页难度表刷新已实现；补隔离数据根和真实大库验收，谱面scanner经验不授予皮肤mutation authority |

### R6：公开发行门

P1-F结合P1-G统一复核：

- P1-A全部Skin V1门、最终静线与第三方包、公开选择面、canonical完整性/原子恢复及无程序化主题fallback。
- `portable.ini → data/`、bootstrap storage中的`storage.ini`与自定义根；保持已验证的完整包、覆盖工具与保存位置合同，补独立账户非便携及设备/长时发行体验。
- Release build/publish、BMS full、mania/core relevant及各子线要求的测试；失败逐项稳定归因。
- 发布说明区分code-provider/ini/scene/script能力，不宣称未通过门的G1、脚本、格式兼容或在线能力。
- 当前完整候选承接公共下载、后台任务、失败/取消、入库选歌与离线启动组合；实网成功和大包/听感门从 [P1-A](../subline/P1-A/DEVELOPMENT_PLAN.md)进入，由 [P1-G](../subline/P1-G/DEVELOPMENT_PLAN.md)汇总，不以旧 ZIP 或开发软件结果代签。

## 冻结项

- P1-M播放器在R3～R6/release门前不抢占工作；除产品明确改序外保持后置。
- 已提前实现的Phase 2能力不代表Phase 1完成；1P/2P flip、完整FHS、dan、BSS/MSS等冻结，除非成为Phase 1阻塞修复。
- 2026-10-03 用户授权的 [P3-IR](../subline/P3-IR/DEVELOPMENT_PLAN.md) 独立按需服务已进入主动连接 / 公网试运行；默认 endpoint、OMS/mania 官网谱面下载、聊天、多人与自动更新仍冻结。既有公共下载窄例外沿 P1-A 合同，不扩大旧全套在线面。
- 不盲目同步上游，只按[UPSTREAM](../other/UPSTREAM.md)选择性cherry-pick。

## 改动验收矩阵

开发存储后续收尾：每次新 shell 遵守 [开发磁盘约束](../../AGENTS.md#开发磁盘约束)。首次使用新缓存时 restore 对应产品工程；旧系统盘工作副本的可再生成产物仍待合规回收，不能绕过工具拦截。Codex 全局数据迁移须待其它活动任务结束后再核对聊天、数据库、工作副本路径与重启恢复；当前不声称 C 盘零增长，不自动删除聊天或验收资料。

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
