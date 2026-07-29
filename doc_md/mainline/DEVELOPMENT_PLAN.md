# OMS 当前开发规划

> 最后更新：2026-07-29
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

### R3：`SV1-2` G1 可视文件夹存储重设计

authority/path preflight、受管目录 Windows handle-relative/no-follow capture、pure immutable capsule、production exact-capsule factory/guarded selection、schema 57 exact-owner启动发现/reconcile、专用mutation authority/recovery foundation、directory-only rename及fixed-source staged import现已闭合成窄生产链。当前推进managed delete；external registration/capture与atomic reload/detach继续各自独立过门。

1. **路径模型**：managed 与 external authority 分离；外部绝对路径使用 `NativeStorage`。
2. **安全删改**：公共scanner/selection/mutation线性化、durable recovery journal、启动幂等恢复、歧义冻结、current delete protected fallback pair门、directory-only rename及fixed-source staged import已闭合；staged import以durable content/tree fingerprint固定Prepared，current recovery严格逐阶段write + exact reload且拒绝fixed-ID journal，rename/import仍无UI。当前managed delete必须独立满足held-root write authority、containment、冲突、reparse/hardlink、final identity、Realm publication与crash-point恢复；可安全判定时幂等forward/rollback到明确完成态或旧态，不能安全判定时保留journal并继续冻结。external永久只读。
3. **扫描与选择**：启动扫描已按exact owner、Observed/Valid分离和完整scan单事务reconcile落地；继续保持只维护自身authority，不得清理普通`.osk`、未知来源记录或无authority blob。它不是watcher/热重载。
4. **整包原子重载**：覆盖 `skin.ini`、素材变化和原子替换；以 package revision 为发布单位，并消除同一 `BmsLegacySkin` 实例成功 preparation cache 不感知 revision 的陈旧风险；生产 `SkinManager`/选择链测试必须存在。
5. **实机 gate**：managed/external、重启、切换、缺件 fallback、导入/删除/重命名均经人工确认。

G1 必须按独立切片推进，不得从异常期存档整批恢复。

### R4：补齐 Skin V1 共同合同、layout、兼容层与外部运行时

1. **共同合同**：补齐 `SV1-1` 剩余生产 slot 的 `Provide/Inherit/Suppress`、ruleset-neutral shared codec 与 mania compatibility；最小可玩组件不可 suppress，缺件逐组件回落，beatmap-local authority 不被无意穿透；保持 shared runtime 与 mania/BMS adapter 分界，禁止 BMS 继承 mania 具体 Drawable/transformer。
2. **layout descriptor**：完成 `SV1-3` 的唯一 resolved layout，统一求解 5K/7K 四 style、9K BMS/PMS、14K DP 的 playfield group/lane/BGA viewport/HUD safe slot，并负责 finite/range/screen-space validation；BGA 播放 authority 留在引擎。
3. **scene/event ABI**：按 `SV1-4`～`SV1-6` 冻结只读 lifecycle/layout/input/object/judgement/score/timing/BGA event family、版本、排序、capability/manifest/activation 与 fail-closed runtime gate；外部 package 可声明 scene、动画和状态机，不为每种视觉新增固定 BMS C# 实现。
4. **sandbox script**：先通过权限、确定性和预算 spike，再作为可选作者层接入；不兼容或移植 LR2/beatoraja runtime。
5. **双极限证明**：`oms-simple.osk` 同包覆盖 mania/BMS、承担最终 fallback；`oms-complex.osk` 同包覆盖 mania/BMS、只用公开 API 证明表达上限。
6. **社区作者面**：完成 `SV1-7`，交付两包可编辑源、模板、schema/event/layout 参考、validator/diagnostics 与打包说明，同时保持 `.osk`、根 `skin.ini`、mania 素材命名和拖入导入心智。

R4 事项仍是 Skin V1/release 的完成条件，但不是启动 R3/`SV1-2` 的前置。视觉验收继续使用[集中清单](../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)，不作为逐组件串行开工门；只有视觉结论实际决定后续设计或自动证据无法裁决异常时才暂停请求反馈。`V-001`～`V-004` 必须在 Skin V1/release 完成声明前统一签收；真实 BMS beatmap-local 尚无作者格式/生产 producer，不得用注入式 fixture 冒充实机能力。详细完成定义只从 [P1-A PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md) 进入，架构证据见 [Skin V1 架构审计](../other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)。

### R5：Phase 1 玩法与硬件收尾

1. `P1-K/P1-J`：先修 lane timeline 上界、sparse keymode authority 与末端 lane keysound，再进入相关皮肤、音频和真实谱验收。
2. `P1-B`：闭合 analog scratch 跨设备 edge/hold 合同与真实 HID 控制器。
3. `P1-D`：补齐 deadzone、sensitivity、scratch 模式说明与 live diagnostics。
4. `P1-E`：验收真实 LN/CN/HCN、长 BGM、键音密集谱和 5K/7K/9K/14K 游玩组合。
5. `P1-G`：把皮肤、输入、长条、Song Select、BGA 的人工结果汇总为 release checklist。

### R6：公开发行门

1. 复核公开皮肤选择面、双包、三态 fallback、canonical `oms-simple` 完整性/原子恢复，以及程序化主题渲染已退出产品链。
2. 复核 `portable.ini → data/`、`storage.ini` 自定义根和覆盖更新。
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
| P1-I | 选歌筛选 | 补 focused/visual 与大库体验 gate |
| P1-J / P1-K | 音频性能、解析与转换 | 先闭合末端 lane 与 keymode authority，再供 R4/R5 消费 |
| P1-L | Gimmick/BGA | 保留内容播放 authority，与 P1-A 解耦 skin viewport |
| P1-M | 音乐播放器 | Phase 1 release gate 前不抢占 R3–R6 |

具体状态和入口统一从 [子线路由](../subline/README.md) 进入。

## Phase 1.x 验收矩阵

| 面 | 未闭合 gate |
| --- | --- |
| BMS 解析/转换 | 特殊谱尾项、lane/keymode authority 与真实谱组合证明 |
| gameplay/判定 | 真实设备和真实 LN/CN/HCN 谱验收 |
| 音频/BGA | 转谱 LN、极端 dense、逐谱视觉与暂停/恢复体验 |
| 皮肤 | 新动画实机、其它 slot 三态、G1、安全 layout、shared ini、scene/event/script、双包、Authoring Kit、移除程序化产品视觉 |
| Song Select | 拖拽 headless、shared visual、人工大库体验 |
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
| shared skin、mania compatibility 或 fallback authority | core skin focused + mania relevant + BMS relevant + Release | 受影响 keymode/style/选择/fallback；完整三态、双包与 canonical 恢复留到对应 gate |
| 输入 | `oms.Input`/bridge focused + BMS relevant | 真实控制器 edge/hold/轴 |
| 存储/Realm | importer/scanner focused + Release | 备份数据根上的升级/重扫/恢复 |
| 音频/BGA | 对应 player/store/cache focused + BMS full | pause/seek、长样本、逐谱视听 |
| 发行 | Release build/publish | 冷启动、portable/custom root、覆盖更新 |

## 规划维护规则

- 本页不记录逐刀实现、当前测试数字、暂停 commit 或已完成事项的展开历史。
- 优先级变化只更新“强制执行顺序”和相关子线摘要，不复制子线全文。
- 新功能必须先明确归线、依赖、最小验收和回退路径，才能进入活动顺序。
