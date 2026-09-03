---
name: project_oms_skin_product_progress
description: Skin V1产品价值核算、C1作者工作区、C2 revision、C3唯一layout、C4 public material与C5 scene/event完成态
metadata:
  node_type: memory
  type: project
---

# OMS Skin V1 产品进度召回

权威当前态与 C5/C6 工作门只看[P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)与[PLAN](../../doc_md/subline/P1-A/DEVELOPMENT_PLAN.md)，稳定inventory/owner/layout/material/scene/event合同见[CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)；C1边界见[2026-08-13完成交接](../../doc_md/other/SKIN_SYSTEM_C1_COMPLETION_HANDOFF_20260813.md)，C3见[2026-08-30完成交接](../../doc_md/other/SKIN_SYSTEM_C3_LAYOUT_COMPLETION_HANDOFF_20260830.md)，C4见[C4完成交接](../../doc_md/other/SKIN_SYSTEM_C4_CODEC_MATERIAL_COMPLETION_HANDOFF_20260831.md)，C5见[C5完成交接](../../doc_md/other/SKIN_SYSTEM_C5_SCENE_EVENT_COMPLETION_HANDOFF_20260903.md)，此前价值核算史见[P1-A CHANGELOG](../../doc_md/subline/P1-A/CHANGELOG.md)。本页保存如何计算产品价值和选择后续工作，不用旧百分比或focused测试冒充campaign完成。

## 进度核算规则

- 产品进度按`真实caller → authoritative manager/backend → production host/renderer → 用户结果 → 失败回退/人工验收`计算，不按提交数、类/DTO数、journal复杂度或测试数量计算。
- 直接保护真实caller与玩家数据的capture、owner、coordinator、journal/recovery属于产品安全价值，但应与新增可见功能分栏；不能把“工程/安全地基成熟度”直接写成release-ready完成度。
- production程序集内的internal API如果没有非测试caller、UI/stager或renderer，只是潜在后端。可以保留已完成风险资产，但不得继续横向扩张来制造进度。
- shared topology/config/event/capability/provenance fixture只有在同切或紧随切片存在production host/renderer/authoring consumer时才继续；否则STOP。
- “多推进”指一次闭合更完整的玩家纵切，不是放宽数据安全、原子性、owner安全归属/释放边界或把一个foundation拆成更多提交；只有声称释放/替换旧owner时才必须同时闭合consumer detach/retirement。

## 2026-08-13 C1 完成态

- Folder Skin Workspace、external只读注册/显式选择/configured restart/pure-Realm noncurrent unregister、exact-set managed mutation、single-v3 ManagedCopy、managed Open/Rename/Delete、动态脱敏journal支持面与ordinary `.osk` bounded ingress已经接成真实caller→manager→renderer/用户结果链；external与managed-copy两条旅程均包含production BMS Note/LN及legacy mania note/hold。
- C1交付时仅关闭七项中的首项并转入C2；这是历史快照。C1只关闭作者文件工作区/G1 UX与ordinary `.osk` ingress安全门，不等于C2或G1最终整包门完成。
- external源永久只读；service-owner token只证明Realm记录归属，不是source capability。selection、Open、ManagedCopy与managed mutation collision都依赖fresh held physical proof；noncurrent Unregister是唯一不触source I/O的pure-Realm compare-remove。
- **C1闭门时**current external unregister、watcher/same-ID reload和全部consumer publication/detach/owner retirement仍归C2；完整layout/shared codec、scene/event、sandbox、canonical双包、Authoring Kit与release也未交付。视觉`V-001`～`V-004`仍0/4。

## 2026-08-24 C2 完成态

- 唯一入口冻结为Settings → Skin → `Reload current skin`；Folder Skin Workspace无Reload，same-value selection不冒充reload，不实现watcher。live gameplay/preview由真实provider/player host在source prepare前拒绝并给出退出后重试反馈。
- ordinary Realm `.osk`、managed与external三源已按same record ID准备fresh immutable revision；全部participant ready后才在update thread一次发布，失败保持exact A。dynamic attach/detach、late attach、participant/work/operation lease、跨fade/sample/materializer holder和最后detach exactly-once retire进入同一protocol。
- current external Unregister、current managed Delete与ordinary current `.osk` Delete都先protected fallback publication+old detach；external随后只pure-Realm remove且source零变化，managed随后才进入C1 journal/physical边界，ordinary随后Realm soft-delete。legacy editor、external-edit与update-import UI/backend稳定禁用。
- 真实产品路径、core/mania/BMS宽回归、Release、targeted formatter、文档门与participant/holder/bypass/concurrency/owner/tests独立终审均已闭合；C2签发当时燃尽推进为`2/7 closed，C3 active`。最终ini/manifest/scene/script/素材整包门仍到C6。

## 2026-08-30 C3 完成态

- P1-K Skin前置已闭合：parser/converter是keymode、lane count与keysound timeline唯一truth；sparse authority、host/importer override seam、fail-closed diagnostic、`GetLaneCount()`末端lane和native/converted shared-store实际发声均已进入production proof。该结论不等于整条P1-K完成，也不代表普通导入已有终端用户纠正UI。
- core只发布一个ruleset-neutral immutable context/snapshot；neutral snapshot与ruleset typed adapter是同一个`GameplaySkinLayoutPublication`，由exact `GameplaySkinLayoutRevisionOwner`持有。BMS只有一个solver，mania adapter使用真实single/dual stage vector并保持stage-local special-key语义。
- BMS/mania/core完整gameplay renderer、BGA最终viewport及HUD/gauge/combo只读同一snapshot；stable LaneId/GroupId与显式四类index继续复用既有topology。逐字段geometry失败产生稳定诊断并回到一个完整fallback snapshot，不会拼出部分新/部分旧geometry。
- package/current revision与layout revision作为不可分pair进入C2 background prepare、fresh barrier、update-thread commit、复核、late attach、lease/detach/最后retire。失败保exact A；live gameplay/preview仍在source prepare前拒绝，无watcher。注入carrier、第二provider、compatibility升级或consumer二次交换均fail-closed。
- C3闭门时燃尽为`3/7 closed，C4 active`；这是历史快照。C4后续已闭合shared material，C3的唯一geometry与P1-K authority仍是不可降级输入。

## 2026-09-02 C4 完成态

- 28项`GameplaySkinSlotCatalog`成为codec/validator/resolver/BMS+mania consumer/文档唯一public ID authority；Common v1与唯一BMS v1 extension共用exact-bytes shared tokenizer/codec，legacy adapter只消费同一immutable token stream。
- 显式`Provide/Inherit/Suppress` resolver以C3 stable LaneId/GroupId和全部显式index解析；required/recommended suppress拒绝，invalid/empty不能冒充absent或回头借同package较宽声明。BMS 5K/7K/9K/14K candidate与版本化9K raw/canonical mapping进入production。
- ordinary Realm `.osk`、managed、external三源从真实`SkinManager` current revision经ruleset prepare到actual BMS Note/LN与mania Note/Hold/KeyVisual、scene/event consumer；所有consumer只读同一package+layout+material+scene publication，失败保A，late attach/lease/detach/retire沿C2/C3协议。
- public diagnostic的完整persistence-safe payload在material prepare时预构造，成功commit后的observer只持immutable字符串与轻量receipt并异步输出稳定、去重、脱敏摘要；listener故障不改变commit或延长material生命周期。新beatmap-local作者格式明确排除，公共source/candidate不可达；既有legacy direct visual compatibility保留且不消费public section。
- dead foundation结论：BMS candidate/resource/capability接入production；被resolved material取代的lane-colour/bucket snapshot删除；event cursor明确归C5、capability negotiator归C6且不计C4。`Create(BmsLaneLayout,...)`、raw requirement resolver overload与`PublishForTesting`只保留isolation/compat seam，不是production能力。
- C4闭门时燃尽为`4/7 closed，C5 active`，这是历史快照。C5已于2026-09-03闭合 scene/event、全部适用 slot production、预算/池化与真实 BMS/mania host；当前燃尽为`5/7 closed，C6 active`。sandbox/script、最终整包reload、canonical双包/Authoring Kit与最终release仍未完成；BGA内容/timeline/seek仍归P1-L，程序化`OmsSkin`继续保留。

## C3/C4最终产品价值核算

- C3不是DTO/solver-only投入：`Player/RulesetSkinProvidingContainer → BmsRuleset/ManiaRuleset preparer → exact layout owner → BMS/mania/core renderer`是可达production链，Note/LN、barline、target/judgement、pre-start、BGA viewport、gauge/combo/HUD均消费同一snapshot。末端lane shared-store发声也在真实native/converted host发生，直接消除漏声、错lane与geometry撕裂。
- 这些交付主要提升“谱面不误判、末端键不静音、换包不混revision、所有playfield件不各算一套”，不是新增大量视觉花样；因此属于必要产品正确性与后续作者表达力的脊柱，但不能冒充完整Skin V1表现力。
- 最终审计删除了零caller的`BmsGameplayLayoutProvider.PrepareExact()` convenience wrapper，production只保留真实`TryPrepareExact()`入口。另保留并如实标注`BmsBeatmapDecoderOptions.KeymodeOverride`：它是authoritative host/importer seam，普通loader当前传`null`且没有用户UI；模糊sparse谱安全拒绝。该用户纠正流程属于P1-K后续产品缺口，不重开C3安全/layout gate。
- C4价值来自作者public声明经唯一codec/catalog/resolver和exact current revision真正改变BMS/mania drawable，并在坏声明/取消/reload失败时保持旧画面；不是28个descriptor、DTO或fixture数量。
- 当前保留的event cursor已进入C5 production，通用capability negotiator/authorization归C6；compat/isolation seam也不计产品能力。C5已让全部适用optional slot经真实scene/event host可达，不能再按public目录总数掩盖ruleset的NotApplicable决定。

## C1最终产品价值核算

- 真实链以settings/import caller为起点：Folder Skin Workspace的注册、选择、Open、ManagedCopy、Rename、Delete、noncurrent Unregister与support面进入manager；external/managed selection再进入BMS Note/LN与legacy mania note/hold consumer。ordinary `.osk`从真实拖入导入进入selection/renderer，bounded ingress与receipt直接保护该路径。
- C1的大部分代码量属于Windows held authority、exact-set、journal/recovery、并发receipt与产品测试。它不增加scene/script表现力，但直接防止external源被写、错目标删改、partial copy、半提交Realm与共享blob误删，属于用户数据安全价值。
- 仓库仍有一个C1前已有、没有独立非测试caller的internal fixed-staging import surface；StagedImport operation/handler仍无production caller，但共同的fixed-slot authority/native move+inspection及journal/coordinator/recovery框架已被ManagedCopy复用，因此不计作额外用户功能，也不能把全部共同底层当死代码。C1新增完成清单中的主要交付均有production caller；selection/import/ManagedCopy另有production consumer证据，Open/Rename/Delete/Unregister/support形成直接用户结果。不得因底层复杂就误判为无意义，也不得把这项结论用于继续扩张无caller foundation。
- 进度只报`5/7 closed`硬退出门，不换算线性工期。恢复/导入安全、作者目录工作区、current revision生命周期、唯一layout、shared material、scene/event与全部适用 slot production已过门；sandbox、最终整包reload、canonical双包/Authoring Kit、视觉与release仍未完成。最终用户可见Skin V1仍在收口期，工程安全基础不得被误报为release完成。
- C1～C5实现密度已经是维护风险；代码量不算产品进度。C6只能在同一publication上接入sandbox/script并关闭最终整包门，C7再交付canonical双包/Authoring Kit；不得重做C1/C2、建立第二layout/material/scene/event publication或先造无caller foundation。

## C1已关闭合同（后续不可降级）

P1-A/`SV1-2`的`C1`作者文件工作区已关闭；下列链是C2及后续必须保留的输入合同：

`settings目录选择/独立registrations行级管理 → resolved physical identity/no-follow capture → immutable capsule → versioned service-owner Realm注册 → dropdown选择/配置重启 → BMS Note/LN与legacy mania note/hold最小artifact → 行级打开源目录/只解除注册`

- external源永久只读；register/select/restart/unregister不隐式复制、写入、rename或删除源。Folder Skin Workspace按committed record ID管理：external行提供Open Folder/Import Managed Copy/Unregister，scanner-owned managed行提供Open Folder/Rename Folder/Delete，普通Realm`.osk`不进列表。managed row Delete与现有current button只共享同一fresh record-ID `CanDelete`、确认语义、manager-owned `DeleteSkinAsync`和journal/recovery，不要求先把noncurrent目标选成current，也不形成第二authority；operation在线性化点fresh判current/noncurrent/split，只有current需要fallback。Import Managed Copy只接收external record ID与用户明确target child，operation ID/staging path由manager生成；fresh capture成对产出exact capsule与含empty directory的immutable logical manifest，文件bytes只来自capsule，destination handles按manifest重建。首写前已有single canonical v3 combined intent并覆盖copy→ProvisionalReady→既有move/publish，仍绝不修改external源。
- 注册不自动选择，active实例不读live store，same-value与原位变化不冒充reload；安全screen须显式点击`Reload current skin`，切走再选或configured restart也仍经fresh capsule取得新revision。configured external必须延续typed startup completion、generic epoch fail-closed、update-thread non-blocking与shutdown join，不能因external不归scanner维护而绕过`551a`。
- versioned service-owner token只授权本服务管理Realm记录，不是source capability。合法非重叠external不再触发旧global block；异步selection在无coordinator lease的capture阶段持有managed authority、完整registry physical proof与target package session，最终只在fresh selection lease内复验generation、generic mutation epoch、full declarations/physical set和target proof后线性化。Realm内以fresh包metadata更新`Name`/`Creator`/`Hash`，较新的不同请求可推进generation并取消陈旧准备；每个mutation admission同样把fresh external proof保持到final collision point，任何集合/identity漂移继续fail-closed。
- **C1完成时的首个纵切**只允许pure-Realm noncurrent unregister：事务内按record ID compare-remove exact service-owner记录，不解析/触碰source；source缺失/不可读/漂移仍可解除陈旧注册。C2现已在此基础上闭合current fallback/detach/fresh compare-remove，不能把历史current拒绝当成当前产品事实。
- **C1完成时**settings caller、capture、Realm、selection/restart、BMS Note/LN与legacy mania note/hold具体artifact、noncurrent unregister与current稳定拒绝、取消/shutdown、脱敏诊断及真实Windows测试已同切闭合。该mania artifact仍不等于完整mania compatibility；后续不得把纵切退化成request/DTO/registry/capture foundation。
- hard boundaries与产品语义回到P1-A CONSTRAINTS；external/ManagedCopy稳定地雷见[[reference_skin_external_workspace_managed_copy]]。

## 七个持久新对话 Campaign

2026-08-09用户纠正了协作粒度：`SV1-*`只能作为能力与依赖taxonomy，不能继续让窄审计/foundation消耗整轮。权威燃尽表在P1-A PLAN；稳定召回如下：

| Campaign | 最低完整结果 |
| --- | --- |
| `C1` | external只读作者工作区 + typed startup/exact-set linearization + durable full managed-copy stager + managed rename/import/delete真实UX + 可证明的journal支持面 + 普通`.osk` early archive importer安全门，关闭G1作者文件工作区 |
| `C2` | 冻结真实可达reload路线；当前全部production consumer与普通`.osk`统一revision publication/detach/owner retirement，并完成current external unregister |
| `C3` | **已闭合**：P1-K keymode/lane前置 + 唯一layout snapshot/publication及全部BMS/mania/core/BGA viewport/HUD consumer + C2 package/layout pair扩展 |
| `C4` | **已闭合**：shared codec、28项public catalog/三态resolver、现有BMS/mania consumer迁移、结构化诊断与beatmap-local排除终态 |
| `C5` | **已闭合（2026-09-03）**：versioned manifest/scene/animation/state、read-only event Snapshot/Reset、BMS/mania全部适用public slot host、预算/池化与同一revision publication/lease/detach协议进入真实production |
| `C6` | 可抢占sandbox VM、公开语言/编译验证工具链、授权持久化、预算、determinism、熔断与profiler；script host加入revision协议并关闭ini/manifest/scene/script最终reload/G1自动门 |
| `C7` | canonical双包、Authoring Kit、validator、证据完整的supported pre-C1 v2与C1后journal迁移、invalid旧Delete安装修复、canonical损坏修复、`OmsSkin`产品authority退出及全部自动release收敛 |

- 当前为`5/7 closed，C6 active`。一个campaign可跨多轮、compaction与多个提交；只有完整退出门闭合才推进编号。
- audit、NO-GO、路线冻结、红测、foundation/DTO、单个caller/consumer、单个提交或文档不能推进编号；需用户产品决策也在原对话等待。
- `C7`退出时，2026-08-09已知P1-A范围（含各campaign内须取得终态并实现的产品路线）的非人工代码/测试/工具/release任务必须为零，只允许集中视觉、真实设备、长时间体验等人工签收；人工反馈产生的新缺陷按新证据修复，不能预先伪称不存在。
- 若campaign提前闭合，可在同一对话直接进入下一campaign，因此七个新对话prompt是上限，不是必须耗尽的配额。

## 后续依赖顺序

1. `C2`已冻结Settings唯一manual Reload与live gameplay/preview prepare前拒绝；participant/holder/bypass inventory、三源纵切、current mutation、宽测试、Release与独立终审均已闭合。稳定地雷见[[reference_skin_atomic_reload_detach]]。
2. 当前协议把holder分成coherent consumer、lease-only holder和已证明无旧owner者；三源same-ID、background prepare、reversible update-thread barrier、dynamic attach/detach/late attach及最后lease detach retire已有production证明。成功诊断清理仍须由请求generation守卫，不能覆盖observer推进generation后的新拒绝reason。
3. `ExternalEditOverlay`、update-import和immediate-dispose旁路已连UI/backend禁用或纳入协议；current external/managed/ordinary mutation均先fallback+detach。C3不得重开或放宽C1/C2。
4. `C3`已闭合P1-K keymode/lane timeline前置与唯一layout；`C4`已闭合shared codec/catalog/resolver与BMS/mania material consumer；`C5`再闭合prepared scene/event与全部适用slot host。package+layout+material+scene、fail-closed入口与revision协议是C6以后不可降级的输入。稳定地雷见[[reference_gameplay_skin_layout_snapshot]]与[[reference_gameplay_skin_codec_material]]。
5. `C5` scene/event已闭合：每个新consumer同切加入revision协议，BMS profile 28项均有route（9K按适用性26格），mania 23项Supported + `object.mine`、`playfield.turntable`、`playfield.laser`、`bga.viewport`、`bga.frame`五个NotApplicable；当前进入`C6` sandbox，且C6才关闭最终整包reload/G1自动门。
6. `C7` canonical `oms-simple/oms-complex`、Authoring Kit与自动release；退出后只保留集中视觉、真实设备和长时间体验签收。

thin/arbitrary-path/foundation-only staged-import stager、逐件optional slot私有C#、提前canonical包及无consumer shared foundation继续NO-GO；现有full ManagedCopy必须保持“已注册external fresh physical proof + paired capsule/manifest + single v3 intent + UI/recovery/tests”整体，不能退化为普通递归copy。作者面整体决议见[[project_oms_bms_skin_authoring]]。

## C5 召回（2026-09-03）

C5 的产品结果不是新增 DTO 或单一 drawable，而是 exact package+layout+material+scene publication 的真实纵切：scene manifest/graph、animation/state/binding/template、只读 event stream 与 Snapshot/Reset 都由 background prepare 产出，BMS/mania/core renderer 只读取 immutable prepared output。BMS/mania producer 不把 scene 提升为判定、输入、分数、clock、BGA 内容或 resource authority；scene 故障只隔离 slot/scene。

runtime profile `oms-gameplay-skin-runtime-support.v1` 对每个 catalog ID 做显式决策。BMS 28 项均有 route（9K 适用矩阵只含 26 格）；Mania 23 项 Supported，`object.mine`、`playfield.turntable`、`playfield.laser`、`bga.viewport`、`bga.frame` 是明确 NotApplicable。C5 的自动证据与当前失败基线见 P1-A STATUS，不在 memory 复制旧 C4 数字。`GameplayResumed` 可以由 engine envelope 发布，但 scene ABI 不接受 `gameplay.resume`，因为 Snapshot 已重建 Running 状态。
