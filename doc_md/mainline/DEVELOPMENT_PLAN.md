# OMS 当前开发规划

> 最后更新：2026-09-12（皮肤开发暂停；保留原阶段和工程证据，双包修改待用户恢复）
> 本页维护全局顺序、跨线依赖和改动验收；当前事实见[STATUS](DEVELOPMENT_STATUS.md)，专项动作从[子线路由](../subline/README.md)进入，历史见[CHANGELOG](CHANGELOG.md)。

## 当前目标

交付Windows-only、离线优先OMS。Phase 1.x完成必须同时满足：

1. BMS/mania主流程与本地数据升级不阻断用户。
2. Skin V1双包、公开三态作者能力及canonical fallback通过P1-A全部门，程序化主题视觉退出产品链；用户包缺件/损坏仍可玩。
3. 输入、LN/CN/HCN、键音/BGA经真实设备与谱面验收。
4. portable、自定义根及覆盖更新保全用户内容。
5. Release及约定focused/full测试达到有效基线，已知失败逐项归因。

## 强制执行顺序

恢复与数据安全`SV1-0`、Skin C1～C7既有工程结果作为后续输入保留，不重复开发；星轨最新总体体验不通过，历史关闭记录不表示作品修改已完成。双包实现按用户要求暂缓，恢复开发前须由用户重新启动迭代；停止边界见 P1-A 状态。P1-A的**七个持久campaign预算、共同执行规则和剩余人工退出门**只在[P1-A PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md#七个持久campaign预算与剩余退出门)维护；不重计、不拆新阶段。

### R3：`SV1-2` G1 存储与 revision 冻结输入

保持C1～C6的既有行为和安全门，完整合同见[P1-A技术约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)。不另建layout、event、material或publication authority，不整包恢复异常期代码。

### R4：完成 Skin V1 sandbox 与 canonical 发行闭环

1. **C6已闭合**：可选脚本、真实作者入口/consumer与隔离能力、最终整包reload/G1自动门见[P1-A结果](../subline/P1-A/DEVELOPMENT_STATUS.md)。
2. **C7 暂时收尾**：既有工程和安装证据保留，星轨总体观感不通过；simple/complex 修改与后续验收待用户恢复迭代，当前默认保持静线，不重计阶段。具体停止边界、待改内容及剩余签收见 [P1-A 计划](../subline/P1-A/DEVELOPMENT_PLAN.md)。
3. 后续恢复后按[集中清单](../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)签收V-001～V-005及修改后的双包；不得借自动可证绕过用户的停止指令。未签收不得称Skin V1/release完成。

具体source、权限、预算、回退与journal迁移条件均以[P1-A C6/C7退出门](../subline/P1-A/DEVELOPMENT_PLAN.md)为准。P1-L继续拥有BGA内容/timeline/seek；不扩大beatmap-local作者面或移植LR2/beatoraja runtime。

### R5：Phase 1 玩法与硬件收尾

| 子线 | 下一动作与依赖 |
| --- | --- |
| P1-J/P1-K | 保持C3 lane/keymode/shared-store authority，补转谱LN、剩余实谱、极端dense与人工音频证明 |
| P1-B/P1-D | analog scratch跨设备edge/hold、真实HID、deadzone/sensitivity、模式说明与live diagnostics；只向皮肤提供只读状态 |
| P1-C/P1-E | 保持判定parity；验收LN/CN/HCN、长BGM、密集键音和各keymode组合，不恢复已删除常驻反馈卡 |
| P1-I | 将三行双端原型落实为既定单轨上限段，再补拖拽headless、shared/visual与大库门 |
| P1-L/P1-G | 单BGA content/decoder迁移、逐谱演出和反向滚动；汇总皮肤/输入/长条/选歌/BGA人工release清单 |
| P1-H | 收口删除/失效、跨root同hash与重扫恢复；谱面scanner经验不授予皮肤mutation authority |

### R6：公开发行门

P1-F结合P1-G统一复核：

- P1-A全部Skin V1门、最终双包与第三方包、公开选择面、canonical完整性/原子恢复及无程序化主题fallback。
- `portable.ini → data/`、bootstrap storage中的`storage.ini`与自定义根；保持已验证的完整包、覆盖工具与保存位置合同，补独立账户非便携及设备/长时发行体验。
- Release build/publish、BMS full、mania/core relevant及各子线要求的测试；失败逐项稳定归因。
- 发布说明区分code-provider/ini/scene/script能力，不宣称未通过门的G1、脚本、格式兼容或在线能力。

## 冻结项

- P1-M播放器在R3～R6/release门前不抢占工作；除产品明确改序外保持后置。
- 已提前实现的Phase 2能力不代表Phase 1完成；1P/2P flip、完整FHS、dan、BSS/MSS等冻结，除非成为Phase 1阻塞修复。
- Phase 3的OMS私有服务、默认endpoint、登录、成绩提交、排行榜、谱面下载、聊天、多人和自动更新冻结；用户主动添加公共BMS难度表URL只是既有窄例外。
- 不盲目同步上游，只按[UPSTREAM](../other/UPSTREAM.md)选择性cherry-pick。

## 改动验收矩阵

| 改动面 | 最低自动验证 | 额外人工验证 |
| --- | --- | --- |
| BMS parser/gameplay | BMS focused + BMS full | 命中特殊谱时逐谱验收 |
| 仅 BMS ruleset 内皮肤组件且不改 shared/mania/fallback authority | BMS skin focused + BMS relevant/full + Release | 对应 keymode、选择/回落与新增视觉实机 |
| shared skin、mania compatibility、scene/event 或 fallback authority | core skin focused + mania/BMS relevant + 所属子线要求的 full + Release | 受影响 keymode/style/选择/fallback；双包与 canonical 恢复留到 C7 |
| 输入 | `oms.Input`/bridge focused + BMS relevant | 真实控制器 edge/hold/轴 |
| 存储/Realm | importer/scanner focused + Release | 备份数据根上的升级/重扫/恢复 |
| 音频/BGA | 对应 player/store/cache focused + BMS full | pause/seek、长样本、逐谱视听 |
| 发行 | Release build/publish | 冷启动、portable/custom root、覆盖更新 |
| 仅文档/协作规则 | 文档检查 + diff 检查；审阅状态、合同与引用一致性 | 不复用或刷新产品/实机验证日期 |
| 开发检查脚本 | 对应 runtime 的真实正反 fixture + 文档/diff 检查 | 涉及产品打包/启动时追加对应发行门 |

`--no-build` 的当前产物前提、并发协调及重复验证条件见 [AGENTS](../../AGENTS.md#并行与验证协调)。新失败按具体测试身份和错误归因；通过所需门后，不因习惯扩大测试。
