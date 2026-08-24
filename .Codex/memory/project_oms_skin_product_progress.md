---
name: project_oms_skin_product_progress
description: Skin V1产品价值核算、C1作者工作区与C2 reload/detach完成态、C3唯一layout及后续campaign准入
metadata:
  node_type: memory
  type: project
---

# OMS Skin V1 产品进度召回

权威当前态只看[P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)与[PLAN](../../doc_md/subline/P1-A/DEVELOPMENT_PLAN.md)；C1完成边界见[2026-08-13完成交接](../../doc_md/other/SKIN_SYSTEM_C1_COMPLETION_HANDOFF_20260813.md)，C2完成事实、inventory与已签发C3入口见[2026-08-24完成交接](../../doc_md/other/SKIN_SYSTEM_C2_COMPLETION_HANDOFF_20260824.md)，此前价值核算见[2026-08-09产品交接](../../doc_md/other/SKIN_SYSTEM_PROGRESS_HANDOFF_20260809.md)。本页保存如何计算产品价值和选择后续工作，不用旧百分比或focused测试冒充campaign完成。

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
- 真实产品路径、core/mania/BMS宽回归、Release、targeted formatter、文档门与participant/holder/bypass/concurrency/owner/tests独立终审均已闭合；燃尽推进为`2/7 closed，C3 active`。最终ini/manifest/scene/script/素材整包门仍到C6。

## C1最终产品价值核算

- 真实链以settings/import caller为起点：Folder Skin Workspace的注册、选择、Open、ManagedCopy、Rename、Delete、noncurrent Unregister与support面进入manager；external/managed selection再进入BMS Note/LN与legacy mania note/hold consumer。ordinary `.osk`从真实拖入导入进入selection/renderer，bounded ingress与receipt直接保护该路径。
- C1的大部分代码量属于Windows held authority、exact-set、journal/recovery、并发receipt与产品测试。它不增加scene/script表现力，但直接防止external源被写、错目标删改、partial copy、半提交Realm与共享blob误删，属于用户数据安全价值。
- 仓库仍有一个C1前已有、没有独立非测试caller的internal fixed-staging import surface；StagedImport operation/handler仍无production caller，但共同的fixed-slot authority/native move+inspection及journal/coordinator/recovery框架已被ManagedCopy复用，因此不计作额外用户功能，也不能把全部共同底层当死代码。C1新增完成清单中的主要交付均有production caller；selection/import/ManagedCopy另有production consumer证据，Open/Rename/Delete/Unregister/support形成直接用户结果。不得因底层复杂就误判为无意义，也不得把这项结论用于继续扩张无caller foundation。
- 进度只报`2/7 closed`硬退出门，不换算线性工期。恢复/导入安全、作者目录工作区与current revision生命周期已过门；唯一layout、shared codec/全slot三态、scene/event、sandbox、canonical双包/Authoring Kit、视觉与release均未完成。最终用户可见Skin V1仍在早期，工程安全基础明显先行。
- C1/C2实现密度已经是维护风险；代码量不算产品进度。C3应让P1-K真实decode/keymode/keysound authority进入唯一layout与完整renderer纵切，并复用C2 revision协议；不得借重构之名重做C1/C2或先造无caller DTO/solver framework。

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
| `C3` | P1-K keymode/lane前置 + 唯一layout snapshot及全部BMS/mania/BGA/HUD consumer |
| `C4` | shared codec、完整public catalog/三态resolver、现有consumer迁移、mania compatibility与结构化诊断；beatmap-local范围同campaign取得终态决定 |
| `C5` | manifest/scene/animation/state/event完整production runtime与BMS/mania hosts，让剩余optional slot进入production并关闭`SV1-1`自动门；所有scene host加入revision lease/detach协议 |
| `C6` | 可抢占sandbox VM、公开语言/编译验证工具链、授权持久化、预算、determinism、熔断与profiler；script host加入revision协议并关闭ini/manifest/scene/script最终reload/G1自动门 |
| `C7` | canonical双包、Authoring Kit、validator、证据完整的supported pre-C1 v2与C1后journal迁移、invalid旧Delete安装修复、canonical损坏修复、`OmsSkin`产品authority退出及全部自动release收敛 |

- 当前为`2/7 closed，C3 active`。一个campaign可跨多轮、compaction与多个提交；只有完整退出门闭合才推进编号。
- audit、NO-GO、路线冻结、红测、foundation/DTO、单个caller/consumer、单个提交或文档不能推进编号；需用户产品决策也在原对话等待。
- `C7`退出时，2026-08-09已知P1-A范围（含各campaign内须取得终态并实现的产品路线）的非人工代码/测试/工具/release任务必须为零，只允许集中视觉、真实设备、长时间体验等人工签收；人工反馈产生的新缺陷按新证据修复，不能预先伪称不存在。
- 若campaign提前闭合，可在同一对话直接进入下一campaign，因此七个新对话prompt是上限，不是必须耗尽的配额。

## 后续依赖顺序

1. `C2`已冻结Settings唯一manual Reload与live gameplay/preview prepare前拒绝；participant/holder/bypass inventory、三源纵切、current mutation、宽测试、Release与独立终审均已闭合。稳定地雷见[[reference_skin_atomic_reload_detach]]。
2. 当前协议把holder分成coherent consumer、lease-only holder和已证明无旧owner者；三源same-ID、background prepare、reversible update-thread barrier、dynamic attach/detach/late attach及最后lease detach retire已有production证明。成功诊断清理仍须由请求generation守卫，不能覆盖observer推进generation后的新拒绝reason。
3. `ExternalEditOverlay`、update-import和immediate-dispose旁路已连UI/backend禁用或纳入协议；current external/managed/ordinary mutation均先fallback+detach。C3不得重开或放宽C1/C2。
4. 当前`C3`先闭合P1-K keymode/lane timeline前置，再交付唯一layout及全部BMS/mania/core/BGA/HUD consumer；新增consumer同步加入revision协议。
5. `C4` shared codec → `C5` scene/event → `C6` sandbox；每个新consumer同切加入revision协议，`C6`才关闭最终整包reload/G1自动门。
6. `C7` canonical `oms-simple/oms-complex`、Authoring Kit与自动release；退出后只保留集中视觉、真实设备和长时间体验签收。

thin/arbitrary-path/foundation-only staged-import stager、逐件optional slot私有C#、提前canonical包及无consumer shared foundation继续NO-GO；现有full ManagedCopy必须保持“已注册external fresh physical proof + paired capsule/manifest + single v3 intent + UI/recovery/tests”整体，不能退化为普通递归copy。作者面整体决议见[[project_oms_bms_skin_authoring]]。
