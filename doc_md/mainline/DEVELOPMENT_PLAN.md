# OMS 当前开发规划

> 最后更新：2026-07-16
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

## 强制执行顺序

### R2：闭合首个产品纵切并完成 Skin V1 共同合同

1. 先由用户单独实机确认 managed `.osk` 的 BMS 普通短键编号帧动画；该结论不得复用静态恢复验收。
2. 实机门闭合后，由产品选择 `SV1-1` 的下一组件，并在 P1-A 重新冻结最小切片、受影响 authority、回退路径和自动/实机验收面。
3. 完成 ruleset-neutral ini codec、layout context、lane identity/topology、显式配置 presence 与 mania compatibility fixtures；唯一 resolved layout 负责 finite/range/screen-space validation。
4. 将 `Provide/Inherit/Suppress` 接到所需生产 slot；最小可玩组件不可 suppress，缺件继续逐组件回落，beatmap-local authority 不被无意穿透。
5. 冻结只读 lifecycle/layout/input/object/judgement/score/timing/BGA event family、版本、排序和禁止写入 authority；真实 capability、manifest、activation 与 runtime gate 必须 fail-closed。
6. 保持 shared runtime 与 mania/BMS adapter 分界；禁止 BMS 继承 mania 具体 Drawable/transformer。

详细完成定义与当前切片只从 [P1-A PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md) 进入，架构证据见 [Skin V1 架构审计](../other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)。

### R3：G1 可视文件夹存储重设计

1. **路径模型**：managed 与 external authority 分离；外部绝对路径使用 `NativeStorage`。
2. **安全删改**：resolved-root containment、冲突拒绝、reparse-point/symlink 风险处理；外部目录只读。
3. **扫描与选择**：扫描只能维护自身 authority 的 Realm 记录，不得清理普通 `.osk`、未知来源记录或无 authority blob。
4. **热重载**：覆盖 `skin.ini`、素材变化和原子替换；生产 `SkinManager`/选择链测试必须存在。
5. **实机 gate**：managed/external、重启、切换、缺件 fallback、删除/重命名均经人工确认。

G1 必须按独立切片推进，不得从异常期存档整批恢复。

### R4：Skin V1 layout、兼容层与外部运行时

1. **layout descriptor**：统一求解 5K/7K 四 style、9K BMS/PMS、14K DP 的 playfield group/lane/BGA viewport/HUD safe slot；BGA 播放 authority 留在引擎。
2. **mania-compatible ini**：共同字段使用同一 codec/resolver；BMS 只扩展 scratch/side/DP/gauge/BGA/gimmick。
3. **scene/event ABI**：外部 package 可声明 scene、动画、状态机并消费只读 gameplay 事件，不为每种视觉新增固定 BMS C# 实现。
4. **sandbox script**：先通过权限、确定性和预算 spike，再作为可选作者层接入；不兼容或移植 LR2/beatoraja runtime。
5. **双极限证明**：`oms-simple.osk` 同包覆盖 mania/BMS、承担最终 fallback；`oms-complex.osk` 同包覆盖 mania/BMS、只用公开 API 证明表达上限。
6. **社区作者面**：交付两包可编辑源、模板、schema/event/layout 参考、validator/diagnostics 与打包说明，同时保持 `.osk`、根 `skin.ini`、mania 素材命名和拖入导入心智。

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
| P1-M | 音乐播放器 | Phase 1 release gate 前不抢占 R2–R6 |

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
