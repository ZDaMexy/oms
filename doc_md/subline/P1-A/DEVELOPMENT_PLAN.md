# P1-A 当前计划：Skin V1、产品面与 release gate

> 最后更新：2026-09-13（静线演奏信息区继续打磨；保留迁移与视觉验收门）
> 全局顺序见[主线计划](../../mainline/DEVELOPMENT_PLAN.md)，当前事实/验证见[STATUS](DEVELOPMENT_STATUS.md)，稳定合同见[TECHNICAL_CONSTRAINTS](TECHNICAL_CONSTRAINTS.md)，历史见[CHANGELOG](CHANGELOG.md)。

## 子线目标

保存星轨后冷启动崩溃已按真实日志修复；后续启动验证保留顶层宿主、已保存非默认选择用例，不再只用默认空库或嵌套 Game 重建。验证见[构建与冷启动记录](../../other/SKIN_BUILTIN_BUILD_20260912.md)。

静线的源文件更新继续使用已接入的正常开发启动和构建发行，后续素材修改直接走此路径，不再把手工刷新 dist 作为运行前置条件。验收见[构建更新记录](../../other/SKIN_BUILTIN_BUILD_20260912.md)；视觉打磨与原人工门继续保留。

交付 mania/BMS 共用公开作者路径的 Windows-only、离线优先 Skin V1：只读 canonical `oms-simple.osk` 同包支持两玩法；保留普通 `.osk`、根 skin.ini、传统命名/帧序列、解包编辑与导入方式，作者无需编译 DLL。历史 complex 仅作参考，ownership 合同见 [技术约束](TECHNICAL_CONSTRAINTS.md#核心-ownership)。

## 当前执行门与冻结输入

用户已明确放弃 complex，只继续打磨 simple。静线是唯一内置、首次默认与正式保底；星轨不再是内置选项、启动依赖或构建对象，历史作者文件仅保留参考。旧内置星轨选择迁回静线，普通用户导入的皮肤不清除；不再要求星轨视觉签收。保留退役选择迁移、用户包保留和单内置构建/启动回归，继续静线实机打磨。原 C1～C7 与双内置验证作为历史保留，不重计阶段；旧 `OmsSkin` 不恢复为产品回落。数据恢复和三源权限合同继续生效。

## 静线当前迭代验收与后续

- 继续对照实机黑白/皿轨比例、皿音符边界与14K第二侧键面，保持旧包等宽兼容和横向布局不影响滚动时间，见[轨道记录](../../other/SKIN_SIMPLE_LANE_PROPORTIONS_20260913.md)。

- 继续对照完整歌曲观察曲名和多表归类截断、作者标级与表内等级区分、判定读数、BPM变速与HiSpeed、小窗口和双侧布局；不将公共数据接通视为整体视觉签收，见[信息区记录](../../other/SKIN_SIMPLE_INFORMATION_20260913.md)。

- 图12后的键面/支座、切角血槽与仪表外壳先完成实际画面对照及作者包回归；继续观察整体控制台衔接与底部空间关系。仅修改已有文件配方和字高，不新增公共运行时能力，不将“换上金属材质”等同整体质量完成。

- 图10之后补薄音符、大写判定、相邻连击和固定外框的实时血槽读数，使用同源Stage素材模板。继续对照实际歌曲观察小窗口文字、判定与音符重叠、控制台与信息区衔接；不凭单张截图代签整体质量。Global `stage.background` 保持 BMS 完整底板与 mania 透明背景，原创底板不使用参考图片输入。当前迭代证据见[细节记录](../../other/SKIN_SIMPLE_STAGE_HUD_20260913.md)。

- 根据图 06～10 复验普通轨与转盘轨分隔线的可见度和粗细关系、黑白轨底色、底座遮蔽、转盘正圆与红判定线；先用当前作者素材和成品验证，再以真实游戏画面观察，不将作者板作为签收。血槽独立外框与暗亮格由文件引用，原纯纹理路径保留固定暗槽与亮层裁切。
- 对照[原始参考与首轮实机反馈图](../../other/references/simple-1p-20260912/README.md)核验：信息不遮挡音符、黑白键与转盘可辨、血条和判定清楚，右侧 BGA 形成主画面；只保留 simple，并以高质量、合格的 beatmania style 成品为目标；LITONE 用于功能分区和完成度参考，不照抄外框、标识或素材。简洁不再作为降低素材质量的理由。
- 保持已验证的作者源和成品同步；后续修改继续检查公共配置与唯一布局求解、真实 BMS/mania 加载及 Release，覆盖 P1/P2、其它键数、窄屏和旧包无独立按键区的行为。BGA 画面大小归皮肤布局，当前迭代不改变 P1-L 内容播放职责。
- 公共底部信息区影响旧包的可用高度，后续微调须覆盖静线和受影响第三方包；实际观感仍须用户观察，不复用旧安装包或原人工签收。

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

映射：`SV1-7`并汇总`SV1-1`～`SV1-7`。

**面向产品的实施重点：** 只继续交付和打磨 `oms-simple`，形成高质量、可完整游玩的 beatmania style 外观，并让作者走通“修改 → 检查 → 打包 → 导入 → 验证”。静线是唯一内置、首次默认与最终保底；complex 与 C6 Momentum 仅保留历史验证身份，不再作为当前成品或默认候选。

**执行输入与交付门：** 双包、作者源和独立制作路径、完整自动复验、安装恢复与跨版本更新均作为已有输入，不重复开发；保留旧包作为对照，不改写其摘要或历史结果。星轨已获总体否定反馈，自动及独立工程复核不等于成品质量通过；真实结果、精确失败比较与交付身份只在 [STATUS](DEVELOPMENT_STATUS.md#此前单内置与布局验证)、[C7 报告](../../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md)、[WORKSHOP](../../../skin-authoring/docs/WORKSHOP.md)及 [P1-F](../P1-F/DEVELOPMENT_STATUS.md)维护，不用中间候选替代最终交付。

**静线迭代与后续验收：** 当前先按用户五张参考图完成静线布局打磨；星轨已放弃，不再列入后续作品修改或签收。围绕完整可玩的静线尽早提供可评审结果，沿既有“修改 → 检查 → 打包 → 导入 → 验证”路径迭代，必要系统修改须对应真实使用问题，不重做已完成能力或扩成可视化编辑器。修改后按影响范围复验，并使用[集中验收包](../../../skin-c7-acceptance/README.md)观察两玩法的键数/样式、单双舞台、缩放、宽高比、必要信息、BGA 区域、授权拒绝/撤销和组合演出；真实设备、音频与长期体验仍要另取实际证据。V-001～V-004 仍 0/4、V-005 未签收；这次总体否定不冒充上述每格已运行，也不表示静线已通过。旧 OmsSkin 物理删除仍待原实机门。Skin V1 和 release 未完成。后续迭代的推送仍遵循 AGENTS 的授权要求，不沿用此前收尾许可。

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

基础改动面验收按[主线矩阵](../../mainline/DEVELOPMENT_PLAN.md#改动验收矩阵)，皮肤专项宽测、真实caller/consumer矩阵及精确失败比较按[测试与发布约束](TECHNICAL_CONSTRAINTS.md#测试与发布约束)。G1另覆盖importer/scanner/containment/selection与备份根重启删改；layout另覆盖topology/BGA、keymode/style/宽高比/DPI/逐轨；scene/script与双包按上方完整C6/C7门验收，不能只用局部focused替代。

.osk/[Mania]/[Bms]、nullable ISkin 与普通选择链继续保持；当前必要缺件由普通简洁包补齐，作者明确关闭的可选内容保持关闭。旧程序化实现仅作隔离历史合同与人工对照，不能因检查适配或发行故障重新接入默认外观。旧F/G术语只用于历史检索，当前执行只按本页C6/C7门，SV1编号仅表示能力依赖。
