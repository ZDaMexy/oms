---
name: project_oms_skin_product_progress
description: Skin V1产品价值核算、release-ready差距、整条玩家纵切准入与external后续工作包
metadata:
  node_type: memory
  type: project
---

# OMS Skin V1 产品进度召回

权威当前态只看[P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)与[PLAN](../../doc_md/subline/P1-A/DEVELOPMENT_PLAN.md)；2026-08-09完整证据见[产品交接](../../doc_md/other/SKIN_SYSTEM_PROGRESS_HANDOFF_20260809.md)。本页保存如何计算产品价值和如何选择后续工作，不用旧百分比覆盖新代码事实。

## 进度核算规则

- 产品进度按`真实caller → authoritative manager/backend → production host/renderer → 用户结果 → 失败回退/人工验收`计算，不按提交数、类/DTO数、journal复杂度或测试数量计算。
- 直接保护真实caller与玩家数据的capture、owner、coordinator、journal/recovery属于产品安全价值，但应与新增可见功能分栏；不能把“工程/安全地基成熟度”直接写成release-ready完成度。
- production程序集内的internal API如果没有非测试caller、UI/stager或renderer，只是潜在后端。可以保留已完成风险资产，但不得继续横向扩张来制造进度。
- shared topology/config/event/capability/provenance fixture只有在同切或紧随切片存在production host/renderer/authoring consumer时才继续；否则STOP。
- “多推进”指一次闭合更完整的玩家纵切，不是放宽数据安全、原子性、owner安全归属/释放边界或把一个foundation拆成更多提交；只有声称释放/替换旧owner时才必须同时闭合consumer detach/retirement。

## 2026-08-09价值锚点

- 玩家真实可达：普通`.osk`选择后的BMS Note/LN head/body/tail；managed目录手工放入后的重启发现/选择；settings确认式物理删除。
- 直接保护上述链：immutable capsule固定active revision；`551a`闭合configured selection↔startup scanner竞态；shared coordinator/journal/recovery被managed delete真实消费。
- 尚无玩家入口：directory-only rename与fixed-source staged import专属后端。
- 未交付：external、atomic reload/detach、完整layout/shared codec、scene/event、sandbox、canonical双包、Authoring Kit与release。
- 当日审慎估计release-ready玩家能力约27%～32%（约三成），工程/安全地基约45%～55%；它是dated排期视图而非gate，没有线性剩余工期含义。视觉`V-001`～`V-004`仍0/4，不能用自动门替代签收。

## 后续整条纵切

P1-A/`SV1-2`下一工程GO/NO-GO候选是external只读作者工作区：

`settings目录选择/独立registrations行级管理 → resolved physical identity/no-follow capture → immutable capsule → versioned service-owner Realm注册 → dropdown选择/配置重启 → BMS Note/LN与legacy mania note/hold最小artifact → 行级打开源目录/只解除注册`

- external永久只读，不复制、写入、rename或删除外部源；settings必须新增持有已提交record ID的独立external registrations行级管理面，提供Open Folder/Unregister，不得复用只绑定current的Delete按钮/dialog；unregister与managed physical delete必须使用不同文案和API。
- 注册不自动选择，active实例不读live store，same-value与原位变化不冒充reload。
- versioned service-owner token只授权本服务管理Realm记录，不是source capability。selection与冲突判断仍须fresh held physical proof；要收窄managed mutation全局阻断，每个真实admission必须把相关external root/ancestry proof保持到final collision linearization point，否则本纵切NO-GO并保留全局阻断。
- 首个纵切只允许noncurrent unregister：current pair两半都不指向目标时才可执行，任一半目标或pair split时UI禁用且manager稳定拒绝，用户须先显式选择并提交其他skin。unregister不dispose任何prior `Skin`/capsule，也不宣称detach/retirement。
- settings caller、capture、Realm、selection/restart、BMS Note/LN与legacy mania note/hold具体artifact、noncurrent unregister与current稳定拒绝、取消/shutdown、脱敏诊断及真实Windows测试必须同切闭合。该mania artifact不等于完整mania compatibility；若只能交付request/DTO/registry/capture foundation则NO-GO。
- hard boundaries与产品语义回到P1-A CONSTRAINTS；external专项实现后再新增独立memory，不在本页预写native细节。

## 后续依赖顺序

1. external G1完整纵切。
2. 冻结manual reload触发、live gameplay允许/拒绝/延后语义及全consumer participation/detach协议，再对atomic reload重新GO/NO-GO；当前仍查[[reference_skin_atomic_reload_detach]]。
3. P1-K keymode/lane timeline前置。
4. `SV1-3`唯一layout → `SV1-4`shared codec → `SV1-5`scene/event → `SV1-6`sandbox。
5. `SV1-7` canonical `oms-simple/oms-complex`、Authoring Kit、集中视觉/性能/release。

thin staged-import stager/caller、逐件optional slot私有C#、提前canonical包及无consumer shared foundation继续NO-GO；作者面整体决议见[[project_oms_bms_skin_authoring]]。
