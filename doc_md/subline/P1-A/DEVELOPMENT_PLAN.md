# P1-A 当前计划：Skin V1、产品面与 release gate

> 最后更新：2026-09-09（原C6闭合，明确C7成品与创作流程重点；预算不重计）
> 全局顺序见[主线计划](../../mainline/DEVELOPMENT_PLAN.md)，当前事实/验证见[STATUS](DEVELOPMENT_STATUS.md)，稳定合同见[TECHNICAL_CONSTRAINTS](TECHNICAL_CONSTRAINTS.md)，历史见[CHANGELOG](CHANGELOG.md)。

## 子线目标

交付mania/BMS共用公开作者路径的Windows-only、离线优先Skin V1：最小可玩的只读canonical `oms-simple.osk`与证明公开API表达上限的`oms-complex.osk`均同包支持两ruleset；保留.osk、根skin.ini、mania命名/帧序列、解包编辑/拖入导入心智，作者无需编译DLL。引擎/作者ownership与非V1范围只见[技术约束](TECHNICAL_CONSTRAINTS.md#核心-ownership)。

## 当前执行门与冻结输入

当前 **`6/7 closed，C7 active`**。C1工作区/archive、C2三源revision、C3唯一layout、C4 codec/catalog/material、C5 scene/event继续保持既有合同；C6公开脚本及最终整包/G1自动门已闭合。不重开campaign或重计预算，完整合同见[TECH](TECHNICAL_CONSTRAINTS.md)，完成证据见[CHANGELOG](CHANGELOG.md)。SV1-0恢复/数据处置继续生效，归档和无authority orphan blob保全，异常期代码不得整包恢复。

C6实现与验收见[C6报告](../../other/SKIN_SYSTEM_C6_VALIDATION_20260909.md)；下一campaign为C7双包/完整作者工具/自动release，本次未提前接管canonical或删除OmsSkin。`V-001`～`V-004`仍0/4，C6新增`V-005`未签收，全部可见结果统一到[集中清单](../../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)。待签收不阻塞自动可证工作；只有视觉结论决定设计或自动证据无法裁决异常时等待反馈。人工未签收只能称“自动gate通过，视觉待验收”，不能称SV1-1、Skin V1或release完成，也不能复用2026-07-14静态恢复验收。

## 七个持久Campaign预算与剩余退出门

`SV1-0`～`SV1-7`表示能力与依赖层，不暗示协作轮数。自2026-08-09的campaign启动prompt起，已知Skin V1/P1-A范围及各campaign必须取得终态的产品路线、P1-K layout前置，在最多七个持久campaign内收口；第七个campaign退出时只允许保留集中视觉、真实设备、长时间体验等人工签收。该承诺是campaign prompt预算，不是日历或单提交工期：同一对话可有多次交互、上下文压缩、有意义提交与测试，未过退出门不得生成后续campaign prompt。不通过拆分子campaign或重新计数扩张该预算。

### `C6` 可选脚本与隔离及最终整包reload门

**已闭合。** 以下为本campaign已满足的退出合同；公开工具链、真实候选/授权UI、完整三源与双host、安全隔离、宽测和独立复审见[C6证据](../../other/SKIN_SYSTEM_C6_VALIDATION_20260909.md)。没有向C7转移VM、权限、cache、profiler或最终整包门欠账。

映射：`SV1-6`并最终复核`SV1-2`。

**必须闭合的非人工产品结果：**

同一campaign完成VM选型spike、所需产品确认与production实现，spike/决策不能作为终态。若选择in-tree bounded bytecode VM，必须同切交付无需DLL的package作者入口、版本化source/bytecode格式、compiler/verifier、malformed/untrusted bytecode验证、version reject/compat策略、source-mapped诊断与deterministic fixtures。无论选型均须闭合只读snapshot/event、授权scene node、四方capability协商、per-skin identity授权持久化/撤销/重协商、compiler/runtime版本失效与cache规则、永久hard-deny、instruction/heap/node/resource预算、deterministic clock/seed、seek/retry/reload、异步compile、熔断、profiler与授权UI；script host同切加入C2 revision lease/detach协议。

**硬退出门：**

真实BMS/mania host运行complex候选脚本；无限循环、超限、异常、取消/shutdown不阻塞update thread且只熔断脚本/scene；ini/manifest/scene/script/素材全部参与同一publication/detach/owner矩阵，至此关闭最终整包reload与G1自动门。不得只交选型文档、catalog、mock consumer，也不得把语言ABI/工具链、授权持久化、profiler或异常回落推给`C7`。

**专项验收补充：** 脚本只用于声明式能力无法合理表达的组合逻辑，不得成为普通note/key/judgement显示的必要条件；只读snapshot/event、操作获准节点。选型还须证明license、Windows打包、性能/GC、调试诊断、低端硬件、权限逃逸防护及pause状态重建；instruction/heap quota须可抢占，编译/I/O在后台。最终整包矩阵覆盖真实选择、重启、切换、rename/import/delete、缺件、原子替换与备份数据根；权限撤销同样加入现有revision协议，不另造event/layout/material/publication或改变P1-L内容authority。

### `C7` canonical双包、Authoring Kit与自动release收口

映射：`SV1-7`并汇总`SV1-1`～`SV1-7`。

**面向产品的实施重点：** 尽早形成可完整游玩的简洁皮肤与复杂展示皮肤，并让作者走通“修改 → 检查 → 打包 → 导入 → 验证”；围绕这两款成品完成默认外观替代和安装恢复。C6 Momentum 只证明组合效果可用，不算最终复杂皮肤。`oms-simple`是最终保底外观，`oms-complex`仍为展示包/默认候选，不擅自锁定首次默认选择。

**必须闭合的非人工产品结果：**

交付可编辑、可复现构建的`oms-simple.osk`/`oms-complex.osk`、模板、完整schema/event/layout/capability/budget文档、validator/diagnostics与打包导入说明；发行物只读携带、完整性验证/原子恢复。canonical fallback接管必须覆盖`SkinManager`初始/current/config失败pair、ruleset providing containers、selection/reload失败回落、current managed delete/current external unregister、protected Realm record。升级时仍存在且具备完整现行证据的supported pre-C1 v2及C1以后journal，可由旧`OmsSkin`证据继续恢复或显式版本迁移；缺tombstone/fingerprint/manifest/disposition的pre-product legacy-v1/old-v2 Delete继续strict Invalid并进入安装修复，绝不猜测迁移。之后才让程序化`OmsSkin`退出产品authority；canonical缺失/损坏必须阻止进入gameplay并进入明确安装修复，不能重新生成程序化视觉。第三方包、portable/custom-root/update、性能及全套自动门收敛。

**硬退出门：**

工程状态达到`SV1-1`～`SV1-7`“自动/合同/安全/release gate通过，人工待签收”；canonical切换前后的全部受支持journal/recovery、delete/unregister receipt与失败回落均可证明收口，invalid旧intent也有不扩大authority的安装修复路径；无程序化主题fallback、私有canonical特权、TODO validator/Authoring Kit或未归因自动失败，同时生成一键人工验收包，用户只需执行视觉/实机清单。

**专项验收补充：** 双包均走普通导入/导出链；simple只含最小可玩件并显式Suppress可选视觉，complex以公开slot/event/script证明上限，不用私有C# provider、隐藏资源或内置特权。最终矩阵包含第三方包、缺失/损坏用户包仍可玩、canonical安装故障修复、启动/切换/reload、全部keymode、BGA、脚本性能、portable/custom-root/覆盖更新及人工视觉与真实设备/谱面。

### 共同执行规则

1. 只读审计、GO/NO-GO、路线冻结、红测、foundation、DTO、单个consumer、单个提交或文档同步都不能独占一个campaign，也不能推进编号；它们只能是当前campaign的前段或组成部分。
2. 每个campaign最低终态为`产品红测 → runtime/backend → 真实UI caller → 全部声明涉及的production consumer → 失败回退/owner边界 → focused/full/Release → docs/memory → 独立终审 → 有意义提交`。
3. 当前campaign未闭合就留在同一对话继续；若必须由用户改变产品语义，则在同一对话等待，不生成新的handoff prompt来消耗预算。若提前闭合，在用户授权范围内直接进入下一个campaign也允许；七个prompt是上限而非配额，停止边界遵循[协作规则](../../../AGENTS.md#工作流)。
4. 人工签收发现的新缺陷形成新证据后仍须修复，但不能预先虚构其不存在；七个campaign承诺覆盖2026-08-09已知P1-A范围、各campaign内须取得终态的产品路线及明确的P1-K layout前置，不把P1-B/D/E/G其它产品子线偷塞进Skin预算。

## 跨线依赖

| 子线 | 向 P1-A 提供 | P1-A 不得越权 |
| --- | --- | --- |
| P1-B/P1-D | 只读输入状态、真实硬件结果 | 不修改输入 edge/hold/calibration authority |
| P1-C/P1-E | 判定、LN/CN/HCN 与反馈语义 | 不由皮肤解释规则结果 |
| P1-H | 路径/authority/重扫经验 | 不直接复制谱面 scanner 的删除 authority |
| P1-J/P1-K | lane keysound proof、keymode/topology truth | 不由 renderer 二次猜 lane/keymode |
| P1-L | BGA timeline/content truth | 不让皮肤创建第二套 player/clock |
| P1-G | 用户实机与 release checklist 汇总 | 不用自动测试替代视觉/硬件结论 |

## 验证与兼容

基础改动面验收按[主线矩阵](../../mainline/DEVELOPMENT_PLAN.md#改动验收矩阵)，皮肤专项宽测、真实caller/consumer矩阵及精确失败比较按[测试与发布约束](TECHNICAL_CONSTRAINTS.md#测试与发布约束)。G1另覆盖importer/scanner/containment/selection与备份根重启删改；layout另覆盖topology/BGA、keymode/style/宽高比/DPI/逐轨；scene/script与双包按上方完整C6/C7门验收，不能只用局部focused替代。

.osk/[Mania]/[Bms]、nullable ISkin、选择链和程序化迁移fallback在对应替代门前保持；新切片失败只回退该切片，不恢复异常期整包。旧F/G术语只用于历史检索，当前执行只按本页C6/C7门，SV1编号仅表示能力依赖。
