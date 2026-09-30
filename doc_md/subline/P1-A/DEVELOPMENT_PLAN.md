# P1-A 当前计划：Skin V1、产品面与 release gate

> 最后核对：2026-10-01（文档治理；已交付能力不再列为实施待办，保留人工门与测试欠账）
> 全局顺序见[主线计划](../../mainline/DEVELOPMENT_PLAN.md)，当前事实见[STATUS](DEVELOPMENT_STATUS.md)，稳定合同见[TECHNICAL_CONSTRAINTS](TECHNICAL_CONSTRAINTS.md)，历史见[CHANGELOG](CHANGELOG.md)。

## 子线目标

交付mania/BMS共用公开作者路径的Windows-only、离线优先Skin V1；只读canonical `oms-simple.osk`同包支持两玩法，使用与第三方相同的文件、配置和公开演出能力。当前实现与已完成工程结果只在STATUS维护，不重复开发。

## 当前执行门与冻结输入

用户认可静线最近调整并决定暂停外观/素材打磨。另行授权的 BGA 作者窗口、游戏持有播放会话与完整制作手册已于2026-09-30完成软件和桌面验证；与此前固定目录刷新、BMS/mania 独立选择及原组件编辑功能一起作为既有输入，事实与证据见 [STATUS](DEVELOPMENT_STATUS.md)。编辑器仍限原组件布局，不把完整 scene/script 可视化制作列为已授权待办；complex 只保留历史参考。静线唯一内置、首次默认与正式保底，用户包保全、canonical 完整性及恢复继续作为回归边界。

## 静线当前迭代验收与后续

以下保留为用户恢复开发或正式验收后的待办，不自行启动新的产品改动：

- 整理已移除工作区/旧编辑器禁用预期的测试欠账：29项在[9月30日完整回归](../../other/BGA_SKIN_AUTHORING_20260930.md)中仍逐项复现，修改前证据见[TOTAL报告](../../other/BMS_TOTAL_RULES_AUDIT_20260922.md#完整回归失败的基线复现)。按现行固定目录与原编辑器用户路径更新测试；仍有效的后端安全合同保留验证，不为旧测试恢复已删除功能。归因不等于维护完成。
- 用完整歌曲核对黑白/皿轨比例与note边界、14K第二侧键序、小窗口判定/连击、键区/血槽/仪表衔接；维持旧包等宽兼容和横向布局不改变滚动时间。当前证据见[轨道记录](../../other/SKIN_SIMPLE_LANE_PROPORTIONS_20260913.md)。
- 核对曲名和多表归类截断、作者标级独立性、实时判定读数、BPM变速与HiSpeed、不同宽高比/单双舞台。信息数据接通不等于整体视觉签收，见[信息区记录](../../other/SKIN_SIMPLE_INFORMATION_20260913.md)。
- 继续使用已保存[原始参考与实机反馈](../../other/references/simple-1p-20260912/README.md)作对照；以高质量beatmania style为目标，LITONE仅作功能分区和完成度参考，不复制外框/标识/素材。
- 后续实际修改按影响范围复验公共配置、唯一布局、BMS/mania选择与回落及Release；保留无声明旧包、P1/P2、其它键数、窄屏及canonical恢复。皮肤控制BGA布局，内容/时间线职责仍归P1-L。
- 作者窗口在代表设备、DPI和真实谱中的可读性、遮挡与三种适配仍需验收，步骤与内容保真由[P1-L计划](../P1-L/DEVELOPMENT_PLAN.md)统一维护；工具检查、真实例子挂载和合成图像证据不能代签这些门。
- 按集中清单补未观察的玩法、样式、设备与长期体验，不复用旧安装包或局部认可补签。正式执行 [V-005](../../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md#v-005c6-可选脚本与双规则集-momentum-候选) 前须按其中的当前入口说明区分可操作项与仅存后端的旧格子；不可达项保持未执行，不为清单恢复已删除入口或用自动证据代签。

设置/模式选择与原编辑器的稳定行为仅在 [TECHNICAL_CONSTRAINTS](TECHNICAL_CONSTRAINTS.md#g1选择ui与startup协调) 维护。

## 七个持久Campaign预算与剩余退出门

`SV1-0`～`SV1-7`表示能力与依赖层，不暗示协作轮数。自2026-08-09的campaign启动prompt起，已知Skin V1/P1-A范围及各campaign必须取得终态的产品路线、P1-K layout前置，在最多七个持久campaign内收口；第七个campaign退出时只允许保留集中视觉、真实设备、长时间体验等人工签收。该承诺是campaign prompt预算，不是日历或单提交工期：同一对话可有多次交互、上下文压缩、有意义提交与测试，未过退出门不得生成后续campaign prompt。不通过拆分子campaign或重新计数扩张该预算。

### `C6` 可选脚本与隔离及最终整包reload门

**已闭合，作为冻结输入。** 映射`SV1-6`并最终复核`SV1-2`；公开工具链、真实授权UI、完整三源与双host、安全隔离、宽测和独立复审见[C6证据](../../other/SKIN_SYSTEM_C6_VALIDATION_20260909.md)。没有向C7转移VM、权限、cache、profiler或最终整包门欠账，不重新安排选型或实现。

后续修改继续守[scene/event与脚本合同](TECHNICAL_CONSTRAINTS.md#scene事件与脚本约束)、[C2 publication](TECHNICAL_CONSTRAINTS.md#c2-current-revision与publication)及[测试与发布约束](TECHNICAL_CONSTRAINTS.md#测试与发布约束)。脚本仍为可选能力，不能成为普通音符与必要判定信息显示的前提；历史complex候选证据不恢复其成品身份。

### `C7` canonical双包、Authoring Kit与自动release收口

标题沿用原campaign任务名，双包已不是现行交付要求；以下保留原退出合同及当前剩余验收边界，已完成工程不重复开发。映射：`SV1-7`并汇总`SV1-1`～`SV1-7`。

**面向产品的实施重点：** 用户恢复迭代时只交付和打磨 `oms-simple`，形成高质量、可完整游玩的 beatmania style 外观，并让作者走通“修改 → 检查 → 打包 → 导入 → 验证”。静线是唯一内置、首次默认与最终保底；complex 与 C6 Momentum 仅保留历史验证身份，不再作为当前成品或默认候选。

**已完成工程输入：** canonical普通包接管、作者源/制作工具、安装恢复与跨版本更新保留[C7证据](../../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md)；9月30日已补齐随套件完整手册及三个实际检查/打包/挂载的练习，见[作者能力记录](../../other/BGA_SKIN_AUTHORING_20260930.md)。不重复开发，不把较早安装包的验收套用到后续运行时与作者源改动；最终发行复验归[P1-F](../P1-F/DEVELOPMENT_STATUS.md)。旧双包及[WORKSHOP](../../../skin-authoring/docs/WORKSHOP.md)只保留各自历史身份。

**静线迭代与后续验收：** 当前按用户决定暂停继续打磨，以下为恢复后仍需满足的验收条件；星轨已放弃，不再列入后续作品修改或签收。围绕完整可玩的静线尽早提供可评审结果，沿既有“修改 → 检查 → 打包 → 导入 → 验证”路径迭代，必要系统修改须对应真实使用问题，不重做已完成能力或扩成可视化编辑器。修改后按影响范围复验，并使用[集中验收包](../../../skin-c7-acceptance/README.md)观察两玩法的键数/样式、单双舞台、缩放、宽高比、必要信息、BGA 区域、授权拒绝/撤销和组合演出；真实设备、音频与长期体验仍要另取实际证据。V-001～V-004 仍 0/4、V-005 未签收；局部改进认可和暂停打磨不代表上述每格已运行或整体签收。旧 OmsSkin 物理删除仍待原实机门。Skin V1 和 release 未完成。后续迭代的推送仍遵循 AGENTS 的授权要求，不沿用此前收尾许可。

**后续退出门：** 已完成的canonical、journal/recovery、普通作者接口及安装故障保护继续按[技术约束](TECHNICAL_CONSTRAINTS.md)回归，不以移除旧OmsSkin源码为由削弱恢复证据。维护上方旧测试预期并对新失败逐项归因；按实际修改重新验证第三方/缺件包、canonical修复、启动/切换/reload、keymode/BGA/脚本及发行组合。软件、合同与安全门不能代替集中视觉、真实设备和长时体验；这些人工门及最终发行复验未关闭前，不宣称Skin V1/release完成。

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
