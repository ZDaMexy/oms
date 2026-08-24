---
name: reference_skin_atomic_reload_detach
description: 三源current revision原子publication/lease/detach/retire的C2完成态、唯一manual Reload、live fail-closed与current mutation边界
metadata:
  node_type: memory
  type: reference
---

# Skin current revision atomic reload/detach 地雷

## 当前结论（2026-08-24，C2已签发）

- C2已由真实Settings caller接通ordinary Realm `.osk`、managed与external三源same-record-ID/content-revision纵切，并通过focused/full、Release、文档门与独立终审；权威燃尽是`2/7 closed，C3 active`。当前状态只看[P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)，完整participant/holder/bypass inventory与已签发C3 prompt见[C2完成交接](../../doc_md/other/SKIN_SYSTEM_C2_COMPLETION_HANDOFF_20260824.md)。
- 唯一产品入口是Settings → Skin → `Reload current skin`。Folder Skin Workspace无行级Reload，same-value selection仍no-op，startup scanner仍只做一次reconcile，不实现watcher；legacy Skin Editor、external-edit与update-import的UI/backend均稳定fail-closed。
- live gameplay/gameplay preview由`RulesetSkinProvidingContainer`和`PlayerLoader`登记`LiveGameplayHost`，manager在任何source capture/parse/provisional prepare前确定拒绝并给出退出后重试反馈。其它attached且无staged receipt的visual consumer也fail-closed；禁止先改变active pair再延后。
- C2只关闭当前production consumer。C3～C6新增layout/codec/scene/script consumer必须同切加入协议；`ini/manifest/scene/script/素材`最终整包reload与G1自动门仍到C6关闭。

## revision、participant 与 owner

- `SkinCurrentRevision`绑定generation、record ID、content revision、source kind和exact owning `Skin`；manager、participant、work与operation lease分别表示current authority、visible attach、隐藏异步work与rollback存活，不能用record ID或`SourceChanged`猜owner。
- participant inventory分三类：必须coherent处理的core/mania/BMS provider/renderer、generic skinnable、ordinary provider、fountain/sample/storyboard与pending screen/player graph；跨fade/sample/materializer/callback的lease-only holder；以及只聚合descendant的sound wrapper、guarded UI projection、独立beatmap/ruleset authority和已禁用authoring路径。完整类型表只在C2 handoff维护，memory不复制第二份authority。
- prepare开始前capture participant/current/source snapshot；source与全部staged material准备后、commit前以及publication lock内再次复核participant generation、target generation、current selection/owner/revision和exact source revision。Realm/blob/held filesystem I/O、capture、parser、texture/sample/materializer等所有可失败工作必须止于background prepare。
- `BmsAsyncNoteDrawable`/`SkinnableContainer`的non-alive host需要GameHost scheduler：outer `Loading`时先等Ready再admit inner，之后`SourceChanged` rebuild也走host scheduler。base source event先同步调用旧work invalidation，再以generation标记调度fresh rebuild；独立第二订阅者会与host scheduler竞速并误杀新generation。Dispose或publication shutdown先进入participant terminal、推进generation并取消pending/Ready admission；否则可能非法mutation、吞掉exact-B rebuild或让晚到callback复活已关闭participant。
- update-thread commit只交换已准备且可逆的内存引用；全部participant ready前B不可见，commit fault须逆序rollback并保持exact A。prepare中attach或commit前detach使snapshot失效并有界fresh retry；commit后late attach只取得已提交revision与lease。commit前取消保A，commit开始后取消不得回滚成split。
- old manager lease释放后，还必须同时满足`ConsumersDetached`与`WorkDetached`；最后participant/work/operation lease detach后才能在update thread exactly-once retire owner。异步graph的framework callback与ownership sentinel须使用同一scheduler保持FIFO；Editor mode graph固定为`ScreenContainer.Scheduler`。shutdown先claim participant集合并令每个participant进入terminal，再调用真实owner hook cancel/reap callback、join真实worker/materializer/work fence，最后同步detach/revision回收；manager不能只发cancel或代替consumer释放work lease。
- BMS/Skinnable invalidation须在各自work admission gate内推进generation并exact claim pending owner/CTS；prepare install和finish publish都比较captured generation，shutdown/dispose同样在gate内推进generation后claim。因此CTS completion不能与invalidation形成double-dispose/已dispose正常窄窗；跨代worker只能回收，不能装入field或发布。
- latest-wins允许新request在旧uncooperative worker退出前发布，但旧worker永不commit且operation admission保持到真实退出。成功publication清理诊断必须compare自己的generation：同代startup contention成功可清`None`；若`SourceChanged` observer重入并推进generation产生新的invalid/reentrant拒绝，outer completion不得覆盖其脱敏reason。

## 三源 exact authority

- ordinary Realm `.osk`：fresh detached metadata与完整file declaration，逐blob读取并核对SHA-256，再构造规范capsule/content revision；declaration/blob漂移保A。发布后Realm record的file-declaration path、external或DeletePending projection漂移不得改变active selection/owner/revision，late renderer继续消费active immutable owner；fresh reload/mutation重读到path改变造成的declaration mismatch时拒绝。不要误称registry file drift。
- managed：exact scanner-owner record、resolver request、held no-follow package session与metadata content revision保持到commit validation。
- external：exact service-owner record、full registry declaration/physical proof、held package session与content revision保持到commit；OMS始终不写source。
- `NoChange`只比较exact prepared content revision，不替换owner。direct current file mutation、retained stale handle、update-import或external-edit不能绕过统一admission。

## current mutation

- current external Unregister先发布protected fallback并等待old `ConsumersDetached`，再fresh compare fallback/current generation/full registry/exact service-owner record并pure-Realm remove。prepare/publication/detach/fresh compare/Realm任一步失败借old-revision operation lease恢复exact A并保留record；source missing/drift不授予source I/O，任何结果source零变化。
- current managed Delete先held capture并证明exact source/content revision等于current，再发布fallback并等待detach；此边界成功前不得创建journal或触碰physical tree，失败保留或恢复A。之后才进入C1 single-v3 journal/physical mutation；首个physical步骤后的uncertain failure只保证durable recovery与protected fallback，不承诺恢复A。C7前fallback仍为程序化`OmsSkin`。
- current ordinary `.osk` Delete同样先fallback+detach，再做Realm soft-delete；Realm失败恢复exact旧pair/revision、record与blob。
- 禁止manager-only API、强制same-ID selection、per-host reloadable、逐component `SourceChanged`拼接、即时dispose旧owner或无caller/consumer的barrier foundation。

## 关联入口

- exact capsule/owner：[[reference_skin_package_revision_capsule]]。
- managed/external authority：[[reference_skin_managed_folder_selection]]、[[reference_skin_managed_folder_scanner]]、[[reference_skin_external_workspace_managed_copy]]。
- mutation recovery：[[reference_skin_managed_folder_mutation_foundation]]、[[reference_skin_osk_archive_import_safety]]。
- authoring/product边界：[[project_oms_bms_skin_authoring]]、[[project_oms_skin_product_progress]]。
