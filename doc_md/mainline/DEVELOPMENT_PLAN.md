# OMS 当前开发规划

> 最后更新：2026-09-09（实际进度校正；campaign 顺序不变）
> 本页只保留未完成工作的全局顺序、依赖和验收门。当前事实见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，子线实现细节进入对应 `P1-*`，历史进入 [CHANGELOG.md](CHANGELOG.md)。

## 当前目标

完成可公开交付的 Windows-only、离线优先 OMS：mania 与第一类 BMS 主流程可用，用户数据可保全，默认/用户皮肤不会破坏可玩性，真实控制器与真实谱面通过人工验收。

Phase 1.x 只有在以下 gate 同时成立时才算完成：

1. BMS/mania 主流程和本地数据升级不阻断用户。
2. 无外部皮肤或用户包损坏时由只读 `oms-simple.osk` 提供可玩 fallback；用户皮肤支持 `Provide/Inherit/Suppress`，缺件逐组件回落且可选视觉可明确关闭；主题化程序渲染退出最终产品链。
3. BMS 输入、LN/CN/HCN、键音/BGA 在真实设备与真实谱面上通过验收。
4. portable `data/`、自定义数据根和覆盖更新不丢用户内容。
5. Release 构建及约定的 focused/full tests 达到当前基线，已知失败被明确归因。

## 已关闭前置

- R0 皮肤异常恢复与 R1 schema 56 数据安全门已经关闭；恢复证据见 [恢复审计](../other/SKIN_SYSTEM_RECOVERY_20260710.md)，数据证据见 [`SV1-0` 报告](../other/SKIN_SYSTEM_SV1_0_INVENTORY_20260713.md)。迁移归档和无 authority orphan blob 继续保全，不做全局清理。
- R2 已关闭进入 G1 所需的前置合同和首个 Note/LN 纵切自动闭环：已导入 `.osk` 的 BMS 普通短键与长条 head/body/tail 四组件通过自动、合同、安全与回退 gate。`V-001`～`V-004` 视觉签收仍为 0/4，`SV1-1`、Skin V1 与 release 均不得据此声明完成；完整 layout/shared codec、所需 slot 三态与 scene/event/script runtime 归 R4，不是进入 R3/`SV1-2` 的前置。

## 强制执行顺序

Skin V1剩余工作不再用`SV1-*`阶段编号暗示协作轮数。P1-A采用最多七个持久campaign的硬预算：作者文件工作区/G1 UX与archive安全、当前consumer reload/detach、P1-K+唯一layout、shared codec/catalog/resolver、scene/event及剩余slot production、sandbox并关闭最终整包reload门、canonical双包/Authoring Kit/自动release。每个campaign必须在同一对话持续到真实caller/consumer、失败回退、宽测试、文档与终审闭合；审计、产品路线决定或foundation不能单独消耗一次handoff。第七个campaign退出时只留人工视觉/实机签收，完整燃尽表见[P1-A PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md)。

### R3：`SV1-2` G1 存储与 revision 冻结输入

C1～C5 已闭合；目录安全、三源 revision 生命周期、P1-K authority、唯一 layout/shared codec/material/scene/event 作为 C6/C7 的冻结输入，完整合同只维护于 [P1-A 技术约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)。external 永久只读、Settings 唯一 manual Reload、live gameplay/preview 在 source prepare 前拒绝、no-watcher、失败保留旧 publication 与最后 lease detach 后退役的边界继续生效。G1 最终整包门由 C6 关闭；异常期归档仍只能定点取证。

### R4：完成 Skin V1 sandbox 与 canonical 发行闭环

1. **C6 sandbox/script**：在同一 campaign 内完成权限、确定性和预算 spike，接入真实作者层与 production consumer，再关闭 ini/manifest/scene/script/全部素材的最终整包 reload 门。新增 consumer 消费同一 publication/lease，不改变 P1-L BGA 内容 authority；不移植 LR2/beatoraja runtime。
2. **C7 双包与 fallback**：`oms-simple.osk` 同包覆盖 mania/BMS，经过 parity、完整性、原子恢复与实机 gate 后接管最终 fallback；`oms-complex.osk` 同包仅用公开 API 证明表达上限。按约定退出程序化 `OmsSkin` 产品渲染。
3. **C7 作者与发行工具**：交付两包可编辑源、模板、schema/event/layout 参考、validator/diagnostics、打包说明与自动 release；保持 `.osk`、根 `skin.ini`、mania 素材命名和拖入导入心智。

新增合同、类型或抽象须在同一切片或紧随切片有真实 host/renderer/authoring consumer，不以 DTO/fixture 代替用户能力。完整 C6/C7 退出门以 [P1-A PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md)为准，不拆成新的 campaign。

视觉验收统一登记到[集中清单](../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)；待签收不阻塞可自动证明的后续切片，只有视觉结论决定设计或自动证据无法裁决异常时才暂停请求反馈。`V-001`～`V-004` 必须在 Skin V1/release 完成声明前签收。beatmap-local 新作者格式继续排除，legacy direct visual compatibility 和注入式 fixture 不代表 public sidecar 能力。

### R5：Phase 1 玩法与硬件收尾

1. `P1-K/P1-J`：保持C3已冻结的lane timeline/keymode/shared-store authority，再补剩余真实谱、转谱LN、极端dense与人工音频验收。
2. `P1-B`：闭合 analog scratch 跨设备 edge/hold 合同与真实 HID 控制器。
3. `P1-D`：补齐 deadzone、sensitivity、scratch 模式说明与 live diagnostics。
4. `P1-E`：验收真实 LN/CN/HCN、长 BGM、键音密集谱和 5K/7K/9K/14K 游玩组合。
5. `P1-I`：先将三行双端筛选原型落实为既定单轨上限段产品面，再关闭 shared/headless、视觉与大库 gate。
6. `P1-L/P1-G`：BGA 单内容源迁移仍未完成；复核逐谱演出，并把皮肤、输入、长条、Song Select、BGA 的人工结果汇总为 release checklist。

### R6：公开发行门

1. 复核公开皮肤选择面、双包、三态 fallback、canonical `oms-simple` 完整性/原子恢复，以及程序化主题渲染已退出产品链。
2. 复核 `portable.ini → data/`、启动存储中的 `storage.ini` 与自定义根；更新须保持原便携模式，修正随包说明，验证非便携覆盖不因新包 marker 切根。
3. Release 构建、BMS 全量、mania/core relevant focused tests 通过，或已知失败有稳定归因。
4. 发布说明区分 code-provider/ini/scene/script 四层能力；不得宣称未通过 gate 的 G1、script、格式兼容或在线能力。

## 子线编排

| 子线 | 当前作用 | 与执行顺序的关系 |
| --- | --- | --- |
| P1-A | 产品面、Skin V1 与 release gate | R2–R4 主归属 |
| P1-B / P1-D | 输入语义、硬件与校准 | R5 联合验收；只向皮肤发布只读输入事件 |
| P1-C | 判定语义与反馈 | 保持 parity gate；不恢复已删除的常驻反馈卡 |
| P1-E / P1-G | 真实谱面与人工验收 | R5 组合证明与 release checklist |
| P1-F | 离线发行 | R6 portable/custom-root/覆盖更新复核 |
| P1-H | 存储拓扑 | 为 G1 提供经验，但皮肤 authority 必须独立建模 |
| P1-I | 选歌筛选 | 单轨产品面待实现，再补 focused/visual 与大库体验 gate |
| P1-J / P1-K | 音频性能、解析与转换 | C3所需末端lane/keymode/shared-store authority已闭合；继续供R4/R5消费并补剩余真实谱验收 |
| P1-L | Gimmick/BGA | viewport 已统一；逐 viewport player 迁移为单内容源仍待完成 |
| P1-M | 音乐播放器 | Phase 1 release gate 前不抢占 R3–R6 |

具体状态和入口统一从 [子线路由](../subline/README.md) 进入。

## Phase 1.x 验收矩阵

| 面 | 未闭合 gate |
| --- | --- |
| BMS 解析/转换 | 特殊谱尾项与剩余真实谱组合证明；C3 lane/keymode authority保持冻结 |
| gameplay/判定 | 真实设备和真实 LN/CN/HCN 谱验收 |
| 音频/BGA | 转谱 LN、极端 dense、逐谱视觉与暂停/恢复体验 |
| 皮肤 | C5 scene/animation/event与全部适用optional slot自动/production gate已闭合；仍待C6 sandbox/最终整包reload、C7双包/Authoring Kit、移除程序化产品视觉及人工实机签收；C3唯一layout与C4 codec/catalog/resolver/material保持冻结 |
| Song Select | 单轨筛选实现、拖拽 headless、shared visual、人工大库体验 |
| 存储/发行 | 删除/失效/去重策略与最终覆盖更新复核 |
| 输入 | analog scratch、一致校准、真实硬件 |

## 冻结项

- Phase 2 中已提前落地的能力不代表 Phase 1 完成；`1P/2P flip`、完整 FHS、dan、BSS/MSS 等继续冻结，除非成为 Phase 1 阻塞修复。
- Phase 3 的 OMS 私有服务、默认 endpoint、登录、成绩提交、排行榜、谱面下载、聊天、多人和自动更新全部冻结。用户主动添加公共 BMS 难度表 URL 是既有窄例外，不得扩张为 OMS 在线产品能力。
- 不盲目同步上游；只按 [UPSTREAM.md](../other/UPSTREAM.md) 选择性 cherry-pick。

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

## 规划维护规则

- 本页不记录逐刀实现、当前测试数字、暂停 commit 或已完成事项的展开历史。
- 优先级变化只更新“强制执行顺序”和相关子线摘要，不复制子线全文。
- 新功能必须先明确归线、依赖、最小验收和回退路径，才能进入活动顺序。
