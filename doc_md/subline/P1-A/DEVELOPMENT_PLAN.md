# P1-A 当前计划：Skin V1、产品面与 release gate

> 最后核对：2026-09-29（补原验收步骤的当前可达性边界；保留暂停与测试欠账）
> 全局顺序见[主线计划](../../mainline/DEVELOPMENT_PLAN.md)，当前事实见[STATUS](DEVELOPMENT_STATUS.md)，稳定合同见[TECHNICAL_CONSTRAINTS](TECHNICAL_CONSTRAINTS.md)，历史见[CHANGELOG](CHANGELOG.md)。

## 子线目标

交付mania/BMS共用公开作者路径的Windows-only、离线优先Skin V1；只读canonical `oms-simple.osk`同包支持两玩法，使用与第三方相同的文件、配置和公开演出能力。当前实现与已完成工程结果只在STATUS维护，不重复开发。

## 当前执行门与冻结输入

用户认可静线最近调整并决定暂停外观/素材打磨。固定目录刷新、BMS/mania 独立选择及原组件编辑功能已完成，事实与验证见 [STATUS](DEVELOPMENT_STATUS.md)。后续开发仍需用户明确恢复。后续仍保持编辑范围为原组件布局，不把完整 scene/script 可视化制作列为已授权待办；complex 只保留历史参考。静线唯一内置、首次默认与正式保底，用户包保全、canonical 完整性及恢复继续作为回归边界。
## 静线当前迭代验收与后续

以下保留为用户恢复开发或正式验收后的待办，不自行启动新的产品改动：

- 整理已移除工作区/旧编辑器禁用预期的测试欠账：29项具名失败及修改前复现见[TOTAL报告](../../other/BMS_TOTAL_RULES_AUDIT_20260922.md#完整回归失败的基线复现)。按现行固定目录与原编辑器用户路径更新测试；仍有效的后端安全合同保留验证，不为旧测试恢复已删除功能。归因不等于这项维护已完成。

- 用完整歌曲核对黑白/皿轨比例与note边界、14K第二侧键序、小窗口判定/连击、键区/血槽/仪表衔接；维持旧包等宽兼容和横向布局不改变滚动时间。当前证据见[轨道记录](../../other/SKIN_SIMPLE_LANE_PROPORTIONS_20260913.md)。
- 核对曲名和多表归类截断、作者标级独立性、实时判定读数、BPM变速与HiSpeed、不同宽高比/单双舞台。信息数据接通不等于整体视觉签收，见[信息区记录](../../other/SKIN_SIMPLE_INFORMATION_20260913.md)。
- 继续使用已保存[原始参考与实机反馈](../../other/references/simple-1p-20260912/README.md)作对照；以高质量beatmania style为目标，LITONE仅作功能分区和完成度参考，不复制外框/标识/素材。
- 后续实际修改按影响范围复验公共配置、唯一布局、BMS/mania选择与回落及Release；保留无声明旧包、P1/P2、其它键数、窄屏及canonical恢复。皮肤控制BGA布局，内容/时间线职责仍归P1-L。
- 按集中清单补未观察的玩法、样式、设备与长期体验，不复用旧安装包或局部认可补签。正式执行 [V-005](../../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md#v-005c6-可选脚本与双规则集-momentum-候选) 前须按其中的当前入口说明区分可操作项与仅存后端的旧格子；不可达项保持未执行，不为清单恢复已删除入口或用自动证据代签。

设置/模式选择与原编辑器的稳定行为仅在 [TECHNICAL_CONSTRAINTS](TECHNICAL_CONSTRAINTS.md#g1选择ui与startup协调) 维护。

## 七个持久Campaign预算与剩余退出门

`SV1-0`～`SV1-7`表示能力与依赖层，不暗示协作轮数。自2026-08-09的campaign启动prompt起，已知Skin V1/P1-A范围及各campaign必须取得终态的产品路线、P1-K layout前置，在最多七个持久campaign内收口；第七个campaign退出时只允许保留集中视觉、真实设备、长时间体验等人工签收。该承诺是campaign prompt预算，不是日历或单提交工期：同一对话可有多次交互、上下文压缩、有意义提交与测试，未过退出门不得生成后续campaign prompt。不通过拆分子campaign或重新计数扩张该预算。

### `C6` 可选脚本与隔离及最终整包reload门

**已闭合。** 以下为本campaign已满足的退出合同；公开工具链、真实候选/授权UI、完整三源与双host、安全隔离、宽测和独立复审见[C6证据](../../other/SKIN_SYSTEM_C6_VALIDATION_20260909.md)。没有向C7转移VM、权限、cache、profiler或最终整包门欠账。

映射：`SV1-6`并最终复核`SV1-2`。

**必须闭合的非人工产品结果：**

同一campaign完成VM选型spike、所需产品确认与production实现，spike/决策不能作为终态。若选择in-tree bounded bytecode VM，必须同切交付无需DLL的package作者入口、版本化source/bytecode格式、compiler/verifier、malformed/untrusted bytecode验证、version reject/compat策略、source-mapped诊断与deterministic fixtures。无论选型均须闭合只读snapshot/event、授权scene node、四方capability协商、per-skin identity授权持久化/撤销/重协商、compiler/runtime版本失效与cache规则、永久hard-deny、instruction/heap/node/resource预算、deterministic clock/seed、seek/retry/reload、异步compile、熔断、profiler与授权UI；script host同切加入C2 revision lease/detach协议。

**硬退出门：**

此前 C6 使用真实 BMS/mania host 运行 complex 候选脚本的证据保留；无限循环、超限、异常、取消/shutdown不阻塞update thread且只熔断脚本/scene；ini/manifest/scene/script/素材全部参与同一publication/detach/owner矩阵，至此关闭最终整包reload与G1自动门。不得只交选型文档、catalog、mock consumer，也不得把语言ABI/工具链、授权持久化、profiler或异常回落推给`C7`。

**专项验收补充：** 脚本只用于声明式能力无法合理表达的组合逻辑，不得成为普通note/key/judgement显示的必要条件；只读snapshot/event、操作获准节点。选型还须证明license、Windows打包、性能/GC、调试诊断、低端硬件、权限逃逸防护及pause状态重建；instruction/heap quota须可抢占，编译/I/O在后台。最终整包矩阵覆盖真实选择、重启、切换、rename/import/delete、缺件、原子替换与备份数据根；权限撤销同样加入现有revision协议，不另造event/layout/material/publication或改变P1-L内容authority。

### `C7` canonical双包、Authoring Kit与自动release收口

标题沿用原campaign任务名，双包已不是现行交付要求；以下保留原退出合同及当前剩余验收边界，已完成工程不重复开发。映射：`SV1-7`并汇总`SV1-1`～`SV1-7`。

**面向产品的实施重点：** 用户恢复迭代时只交付和打磨 `oms-simple`，形成高质量、可完整游玩的 beatmania style 外观，并让作者走通“修改 → 检查 → 打包 → 导入 → 验证”。静线是唯一内置、首次默认与最终保底；complex 与 C6 Momentum 仅保留历史验证身份，不再作为当前成品或默认候选。

**执行输入与交付门：** 双包、作者源和独立制作路径、完整自动复验、安装恢复与跨版本更新均作为已有输入，不重复开发；保留旧包作为对照，不改写其摘要或历史结果。星轨已获总体否定反馈，自动及独立工程复核不等于成品质量通过；真实结果、精确失败比较与交付身份只在 [STATUS](DEVELOPMENT_STATUS.md#最近一次验证)、[C7 报告](../../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md)、[WORKSHOP](../../../skin-authoring/docs/WORKSHOP.md)及 [P1-F](../P1-F/DEVELOPMENT_STATUS.md)维护，不用中间候选替代最终交付。

**静线迭代与后续验收：** 当前按用户决定暂停继续打磨，以下为恢复后仍需满足的验收条件；星轨已放弃，不再列入后续作品修改或签收。围绕完整可玩的静线尽早提供可评审结果，沿既有“修改 → 检查 → 打包 → 导入 → 验证”路径迭代，必要系统修改须对应真实使用问题，不重做已完成能力或扩成可视化编辑器。修改后按影响范围复验，并使用[集中验收包](../../../skin-c7-acceptance/README.md)观察两玩法的键数/样式、单双舞台、缩放、宽高比、必要信息、BGA 区域、授权拒绝/撤销和组合演出；真实设备、音频与长期体验仍要另取实际证据。V-001～V-004 仍 0/4、V-005 未签收；局部改进认可和暂停打磨不代表上述每格已运行或整体签收。旧 OmsSkin 物理删除仍待原实机门。Skin V1 和 release 未完成。后续迭代的推送仍遵循 AGENTS 的授权要求，不沿用此前收尾许可。

**必须闭合的非人工产品结果：**

交付可编辑、可复现构建的唯一内置 `oms-simple.osk`、模板、完整schema/event/layout/capability/budget文档、validator/diagnostics与打包导入说明；发行物只读携带、完整性验证/原子恢复。canonical fallback接管必须覆盖`SkinManager`初始/current/config失败pair、ruleset providing containers、selection/reload失败回落、current managed delete/current external unregister、protected Realm record。升级时仍存在且具备完整现行证据的supported pre-C1 v2及C1以后journal，可由旧`OmsSkin`证据继续恢复或显式版本迁移；缺tombstone/fingerprint/manifest/disposition的pre-product legacy-v1/old-v2 Delete继续strict Invalid并进入安装修复，绝不猜测迁移。之后才让程序化`OmsSkin`退出产品authority；canonical缺失/损坏必须阻止进入gameplay并进入明确安装修复，不能重新生成程序化视觉。第三方包、portable/custom-root/update、性能及全套自动门收敛。

**硬退出门：**

工程状态达到`SV1-1`～`SV1-7`“自动/合同/安全/release gate通过，人工待签收”；canonical切换前后的全部受支持journal/recovery、delete/unregister receipt与失败回落均可证明收口，invalid旧intent也有不扩大authority的安装修复路径；无程序化主题fallback、私有canonical特权、TODO validator/Authoring Kit或未归因自动失败，同时生成一键人工验收包，用户只需执行视觉/实机清单。

**专项验收补充：** simple 走普通导入/导出链，以公开 slot/event/script 完成所需视觉，不用私有 C# provider、隐藏资源或内置特权；可选视觉按实际设计声明，不以“最小可玩”限制素材质量。complex 的双包验证只作为历史证据。最终矩阵包含第三方包、缺失/损坏用户包仍可玩、canonical安装故障修复、启动/切换/reload、全部keymode、BGA、脚本性能、portable/custom-root/覆盖更新及人工视觉与真实设备/谱面。

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

基础改动面验收按[主线矩阵](../../mainline/DEVELOPMENT_PLAN.md#改动验收矩阵)，皮肤专项宽测、真实caller/consumer矩阵及精确失败比较按[测试与发布约束](TECHNICAL_CONSTRAINTS.md#测试与发布约束)。G1另覆盖importer/scanner/containment/selection与备份根重启删改；layout另覆盖topology/BGA、keymode/style/宽高比/DPI/逐轨；scene/script、静线与第三方输入按上方C6/C7门验收，双包结果只保留历史身份，不能只用局部focused替代。

.osk/[Mania]/[Bms]、nullable ISkin 与普通选择链继续保持；当前必要缺件由普通简洁包补齐，作者明确关闭的可选内容保持关闭。旧程序化实现仅作隔离历史合同与人工对照，不能因检查适配或发行故障重新接入默认外观。旧F/G术语只用于历史检索，当前执行只按本页C6/C7门，SV1编号仅表示能力依赖。
