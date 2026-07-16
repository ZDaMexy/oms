# OMS 当前开发规划

> 最后更新：2026-07-16
> 本页只保留全局执行顺序、依赖和验收门。子线实现细节进入对应 `P1-*`，历史进入 [CHANGELOG.md](CHANGELOG.md)。当前事实以 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md) 为准。

## 当前目标

在不重新引入 Osu/Taiko/Catch、不提前开放联网能力的前提下，完成可公开交付的 Windows-only、离线优先 OMS：mania 与第一类 BMS 主流程可用，用户数据可保全，默认/用户皮肤不会破坏可玩性，真实控制器与真实谱面通过人工验收。

Phase 1.x 的完成不以“代码数量”判断，而以以下 gate 同时成立判断：

1. BMS/mania 主流程和本地数据升级不阻断用户。
2. 无外部皮肤或用户包损坏时由只读 `oms-simple.osk` 提供可玩 fallback；用户皮肤支持 `Provide/Inherit/Suppress`，缺件逐组件回落且可选视觉可明确关闭。最终产品不保留主题化程序渲染 fallback。
3. BMS 输入、LN/CN/HCN、键音/BGA 在真实设备与真实谱面上通过验收。
4. portable `data/`、自定义数据根和覆盖更新不丢用户内容。
5. Release 构建及约定的 focused/full tests 达到当前基线，已知失败被明确归因。

## 强制执行顺序

### R0：皮肤异常恢复

- Git、dirty tree、不可达对象与运行时数据保全：**已完成**。
- 可信代码面、文档和 memory 恢复并推送：**已完成**。
- 自动回归：**已完成**；结果见主线 STATUS。
- 无外部皮肤、`.osk` 用户皮肤、partial fallback、5K/7K/9K/14K 与双皿/资源隔离实机视觉：**2026-07-14 已通过**。

证据账：[SKIN_SYSTEM_RECOVERY_20260710.md](../other/SKIN_SYSTEM_RECOVERY_20260710.md)。

### R1：恢复后的数据与皮肤安全门

1. schema 56 只读清点已完成：folder authority 正常，异常记录已在备份和副本演练后定点处置；见 [`SV1-0` 报告](../other/SKIN_SYSTEM_SV1_0_INVENTORY_20260713.md)。
2. 用户已选择保全后移除无价值的异常 mutable copy；生产迁移完成并经 read-only reopen 验证。迁移归档保留，物理 orphan blob 暂不做全局清理。
3. 保持当前 `.osk` F1 路线和程序化 `OmsSkin` 迁移 fallback 稳定，不在 `oms-simple` parity/完整性/恢复 gate 前提前删除，也不把它写成最终产品能力。

### R2：Skin V1 共同合同与首个产品纵切

状态：R0/R1 已解除；`SV1-1` 在前二十个合同地基之后已实现首个玩家可见组件：用户选中已导入的 managed `.osk` 时，BMS 普通短键可使用编号帧动画。自动 gate 已通过，因此当前新增可见功能计为 1；新动画实机仍待确认。`SV1-1` 未完成，`SV1-2` 只有 early carrier，`SV1-3`～`SV1-7` 未实现，不能把当前纵切描述为 Skin V1 可用。实现暂停于 `d1ea483`；下一新对话先做文档与 memory 健康治理，治理只归位事实/历史、不改产品合同或代码。治理完成并重新冻结执行门后，再闭合新动画实机 gate，并由产品决定下一组件。

1. 冻结 ruleset-neutral ini codec、layout context、lane group/role/side/stable ID 和 mania compatibility fixtures；lane identity/order snapshot、neutral validator、topology-only publication/process-local native-context revision、decoder bucket presence、legacy mania scalar/indexed-array/四项 global colour/per-column colour/exact 13 项 bucket-global resource/`NoteBodyStyle` accepted provenance、native `[Bms]` exact 22 项 colour / 12 项 geometry accepted provenance、两侧六类 lane-resource decoder-time accepted provenance、mapping/resolution 已落。geometry snapshot 只是 parser-accepted source provenance，full finite/range/screen-space validation 与唯一 resolved layout 仍归 neutral descriptor/solver。BMS 普通短键是首个真实文件窄纵切，已闭合其所需的精确 package authority、资源/帧验证、预算、owner 与后台准备边界；这不代表其它资源、完整 neutral config/shared codec、layout、production revision/event/wire 或整包重载已经完成。
2. 冻结 `Provide / Inherit / Suppress` 三态及最小可玩组件；平行 result/resolver、precedence fixture 与 26 项内部 semantic slot 分类已完成，BMS 普通短键 critical slot 已把 `Provide/Inherit` 接到生产 gameplay。作者 `Suppress`、其它 slot、manifest mapping 和最终 `oms-simple` 文件 fallback 仍待后续。
3. 冻结只读 lifecycle/layout/input/object/judgement/score/timing/BGA event family、版本和禁止写入 authority；process-local envelope/order 与 capability decision/hard-deny foundation 已落，concrete payload、producer/dispatch、sampling、真实 manifest/version/activation/runtime gate 仍待。
4. 明确 shared runtime 与 mania/BMS adapter 分界；禁止 BMS 直接继承 mania 具体 Drawable/transformer。

架构证据见 [SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md](../other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)。

### R3：G1 可视文件夹存储重设计

按独立切片推进，不得从异常期存档整批恢复：

1. **路径模型**：managed 与 external authority 分离；外部绝对路径使用 `NativeStorage`。
2. **安全删改**：resolved-root containment、冲突拒绝、reparse-point/symlink 风险处理；外部目录只读。
3. **扫描与选择**：扫描只能维护自身 authority 的 Realm 记录，不得清理普通 `.osk` 或未知来源记录。
4. **热重载**：覆盖 `skin.ini`、素材变化和原子替换；生产 `SkinManager`/选择链测试必须存在。
5. **实机 gate**：managed/external、重启、切换、缺件 fallback、删除/重命名均经人工确认。

### R4：Skin V1 layout、兼容层与外部运行时

1. **layout descriptor**：5K/7K 四 style、9K BMS/PMS、14K DP 的 playfield group/lane/BGA viewport/HUD safe slot 统一求解；BGA 播放 authority 留在引擎。
2. **mania-compatible ini**：共同字段使用同一 codec/resolver；BMS 只扩展 scratch/side/DP/gauge/BGA/gimmick。
3. **scene/event ABI**：外部 package 可声明 scene、动画、状态机并消费只读 gameplay 事件，不为每种视觉新增固定 BMS C# 实现。
4. **sandbox script**：先通过权限/确定性/预算 spike，再作为可选作者层接入；不兼容或移植 LR2/beatoraja runtime。
5. **双极限证明**：`oms-simple.osk` 同包覆盖 mania/BMS、只保留可玩核心并承担最终 fallback；`oms-complex.osk` 同包覆盖 mania/BMS、只用公开 API 证明接近 IIDX 复杂度的表达力。
6. **社区作者面**：保持 `.osk`、根目录 `skin.ini`、mania 素材/动画命名、解包编辑和拖入导入心智；交付两包可编辑源、模板、schema/event/layout 参考、validator/diagnostics 与打包说明。

### R5：Phase 1 玩法与硬件收尾

1. `P1-B`：analog scratch 跨设备 edge/hold 合同与真实 HID 控制器。
2. `P1-D`：deadzone、sensitivity、scratch 模式说明与 live diagnostics。
3. `P1-E`：真实 LN/CN/HCN、长 BGM、键音密集谱和 5K/7K/9K/14K 游玩验校。
4. `P1-G`：把皮肤、输入、长条、Song Select、BGA 的人工结果汇总为 release checklist。

### R6：公开发行门

1. 复核公开皮肤选择面、`oms-simple/oms-complex`、三态 fallback、canonical `oms-simple` 完整性/原子恢复，以及程序化主题渲染已退出产品链。
2. 复核 `portable.ini → data/`、`storage.ini` 自定义根和覆盖更新。
3. Release 构建、BMS 全量、mania/core relevant focused tests 通过或已知失败有稳定归因。
4. 发布说明区分 code-provider/ini/scene/script 四层能力；不得宣称未通过 gate 的 G1、script、格式兼容或在线能力。

## 子线编排

| 子线 | 当前作用 | 与当前顺序的关系 |
| --- | --- | --- |
| P1-A | 产品面、Skin V1 与 release gate | 当前主归属；按 R0–R4 建立恢复、数据、shared contract、G1 与外部运行时 |
| P1-B | 输入语义与硬件 | R5 主项，与 P1-D/P1-E 联合验收；向皮肤只发布只读输入事件 |
| P1-C | 判定语义与反馈 | 保持 parity gate；不恢复已删除的常驻反馈卡 |
| P1-D | 控制器校准 | P1-B 的真实设备配套面 |
| P1-E | 真实谱面 gameplay | 验证解析/音频/输入在真实谱上的组合结果 |
| P1-F | 离线发行 | portable 基线已落，R6 复核 |
| P1-G | 人工验收 | 收口所有无法由 headless tests 证明的结果 |
| P1-H | 存储拓扑 | 为谱库与 G1 提供路径/authority 经验，但皮肤须独立建模 |
| P1-I | 选歌筛选 | 主功能已落，只补 focused/visual gate |
| P1-J | 性能与音频 | 普通密度问题已收口；为末端 lane 提供运行时发声 proof，其余只按 profiler/真机证据继续 |
| P1-K | 解析与转换 | 先修 lane timeline 上界与 keymode source，再为 P1-A/P1-E/P1-L 提供稳定 topology authority |
| P1-L | Gimmick/BGA | 保留内容播放 authority；与 P1-A 解耦 skin viewport，继续隔离旁路与逐谱验证 |
| P1-M | 音乐播放器 | 规划保留，Phase 1 release gate 前不抢占当前恢复与硬件工作 |

具体状态和入口统一从 [子线路由](../subline/README.md) 进入。

## Phase 1.x 验收矩阵

| 面 | 已有基线 | 未闭合 gate |
| --- | --- | --- |
| BMS 解析/转换 | raw/typed 模型、主要控制事件、BMS→mania 转换 | 特殊谱尾项与真实谱组合证明 |
| gameplay/判定 | 主要 keymode、判定家族、gauge、EX score、LN/CN/HCN 链 | 真实设备和真实谱验收 |
| 音频/BGA | shared keysound、转谱音频主链、BGA 图/视频链 | 转谱 LN、极端 dense、逐谱视觉/暂停恢复体验 |
| 皮肤 | `.osk` F1 静态素材/ini + component lookup + 程序化迁移 fallback + schema 56；SV1-0 自动/数据/实机 gate 已过；managed `.osk` BMS 普通短键编号帧动画已通过自动 gate | 先治理文档/memory；之后单独确认新动画实机；其它 slot 三态、G1、LN/mania compatibility、安全 layout descriptor、shared ini、scene/event/script、`oms-simple/oms-complex`、Authoring Kit、移除程序化产品视觉 |
| Song Select | BMS 分组、筛选、搜索和主要展示 | 拖拽 headless、shared visual、人工大库体验 |
| 存储/发行 | `chartbms/chartmania`、多根扫描、portable/custom root | 删除/失效/去重策略、最终覆盖更新复核 |
| 输入 | keyboard/Raw/XInput/Mouse/DirectInput 基线 | analog scratch、一致校准、真实硬件 |

## Phase 2 / Phase 3

- Phase 2 中已有部分能力提前落地，但不因此宣布 Phase 1 完成。`1P/2P flip`、完整 FHS、dan、BSS/MSS 等保持冻结，除非它们成为 Phase 1 阻塞修复。
- Phase 3 联网功能全部冻结：默认 endpoint 为空，登录、成绩提交、排行榜、下载、聊天、多人、更新等不得作为当前可用能力描述。
- 不盲目同步上游；只按 [UPSTREAM.md](../other/UPSTREAM.md) 选择性 cherry-pick。

## 改动验证矩阵

| 改动面 | 最低自动验证 | 额外人工验证 |
| --- | --- | --- |
| BMS parser/gameplay | BMS focused + BMS full | 命中特殊谱时逐谱验收 |
| 仅 BMS ruleset 内的皮肤组件且不改 shared/mania/fallback authority | BMS skin focused + BMS relevant/full + Release | 对应 keymode、选择/回落与新增视觉实机 |
| shared skin、mania compatibility 或 fallback authority | core skin focused + mania relevant + BMS relevant + Release | 本次已实现且受影响的 keymode/style/选择/fallback；完整三态、双包、canonical 恢复与脚本矩阵留到对应能力实现及 release gate |
| 输入 | `oms.Input`/bridge focused + BMS relevant | 真实控制器 edge/hold/轴 |
| 存储/Realm | importer/scanner focused + Release | 备份数据根上的升级/重扫/恢复 |
| 音频/BGA | 对应 player/store/cache focused + BMS full | pause/seek、长样本、逐谱视听 |
| 发行 | Release build/publish | 冷启动、portable/custom root、覆盖更新 |

## 规划维护规则

- 本页不记录逐刀实现和旧测试数字；它们进入对应 `CHANGELOG`。
- 已完成事项只在仍影响依赖时保留一行，避免计划变成历史百科。
- 优先级变化只更新“强制执行顺序”和相关子线摘要，不复制子线全文。
- 新功能必须先明确归线、依赖、最小验收和回退路径，才能进入活动顺序。
