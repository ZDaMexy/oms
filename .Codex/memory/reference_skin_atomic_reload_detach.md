---
name: reference_skin_atomic_reload_detach
description: current managed skin整包原子reload/detach的2026-08-09 NO-GO、真实consumer缺口与重新开门条件
metadata:
  node_type: memory
  type: reference
---

# Managed skin atomic reload/detach 地雷

## 当前结论

- 2026-08-09按真实settings/selection/renderer/owner链审计，current managed atomic reload/detach为**NO-GO**；不得新增reload foundation。当前状态与顺序只看[P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)、[PLAN](../../doc_md/subline/P1-A/DEVELOPMENT_PLAN.md)和[CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)，dated证据见[2026-08-09交接](../../doc_md/other/SKIN_SYSTEM_PROGRESS_HANDOFF_20260809.md)。
- PLAN仍把external registration/capture排在atomic reload/detach之前。managed reload只有在已有production caller/host/renderer能让完整纵切独立闭合时才可例外提前；当前仓库没有reload current managed revision的caller、UI、watcher或manager API。
- settings/config/hotkey只请求selection，same-value在准备前短路；startup scanner是单次reconcile而非watcher。filesystem-backed skin继续被editor、update import与external edit拒绝。普通Realm `ExternalEditOverlay`的new-instance后立即dispose旧实例不是managed caller，也没有barrier，不能复用。

## publication 与 owner 地雷

- `SkinManager`的`CurrentSkinInfo`/`CurrentSkin` pair只在manager内coherent，随后`SourceChanged`是事件扇出；没有package revision publication对象、consumer snapshot/registry、ack、detach receipt或old-instance retire queue。pair commit、`SkinReloadableDrawable`和`BmsAsyncNoteDrawable`都不是整包atomic reload。
- `BmsPlayfield`在loader一次读取skin geometry并缓存layout/profile，不监听`SourceChanged`。BMS Note/LN gameplay与pre-start preview是独立per-host异步consumer；core/mania drawable还混合同步、scheduler和next-update更新；菜单背景会在fade/expire期间继续持有旧`Skin`。当前publication可产生旧geometry+新shell/note等mixed revision。
- exact capsule经factory转入`Skin` owning store；`Skin.Dispose()`释放texture/sample/fallback store/capsule，`BmsLegacySkin.Dispose()`还取消package note preparation。成功selection没有全consumer detach后的退役协议，既有产品测试手工dispose superseded managed skin。即时dispose可能破坏旧consumer，不dispose则owner生命周期未闭合。
- 现有测试只证明capsule/store ownership、guarded selection和per-host A→B，不证明same-ID revision gate、全host barrier、failure保留exact旧pair/owner、detach后dispose once或reload latest-wins/reentrant/cancel/shutdown join。

## 重新开门门槛

- 产品先冻结唯一真实触发方式、允许场景（尤其live gameplay是否允许）和全部consumer participation/publication/detach/retirement协议；在此之前，“先写红测”会先发明缺失caller/barrier，不是合格产品红测。
- 获准纵切仍须fresh authoritative Realm/path/owner/freeze/capture/factory复核、完整immutable capsule与new skin instance准备、generation/current-selection/revision gate、所有consumer coherent publication、失败保留exact旧revision、全consumer detach后幂等dispose旧owner，以及latest-wins/reentrant/首个不可逆边界前取消/shutdown exact claim-reap-join与脱敏诊断。
- 禁止manager-only reload API、强制同ID selection、逐组件`SourceChanged`拼接、即时dispose旧owner或没有真实caller/consumer的barrier/DTO foundation。managed delete journal/detach是独立operation合同，不提供reload的全renderer生命周期事务。

## 关联入口

- exact capsule/owner：[[reference_skin_package_revision_capsule]]。
- managed selection与`551a`协调：[[reference_skin_managed_folder_selection]]、[[reference_skin_managed_folder_scanner]]。
- authoring/product边界：[[project_oms_bms_skin_authoring]]。
