---
name: reference_skin_atomic_reload_detach
description: ordinary .osk、managed与external current revision原子publication/detach的C2入口、2026-08-09 NO-GO遗留缺口与关闭条件
metadata:
  node_type: memory
  type: reference
---

# Skin current revision atomic reload/detach 地雷

## 当前结论

- 2026-08-09按真实settings/selection/renderer/owner链审计，current managed atomic reload/detach为**NO-GO**。C1作者文件工作区前置已经满足，燃尽现为`1/7 closed，C2 active`；这表示可以在C2冻结真实路线并实现纵切，不表示旧NO-GO所列consumer/lifecycle缺口已经消失。当前状态与顺序只看[P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)、[PLAN](../../doc_md/subline/P1-A/DEVELOPMENT_PLAN.md)和[CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)，dated证据见[2026-08-09交接](../../doc_md/other/SKIN_SYSTEM_PROGRESS_HANDOFF_20260809.md)。
- C2承接当前production consumer的revision publication/lease/detach协议；`C3`～`C6`新增layout/scene/script consumer必须同切加入该协议，直到`C6`才关闭覆盖`ini/manifest/scene/script/素材`的最终整包reload与G1自动门。C2必须先冻结唯一真实触发、允许场景和participant集合，再以同一campaign的红测与实现闭合，禁止脱离caller预建barrier foundation。
- C1已经提供external registration/capture、managed Workspace与ordinary `.osk` ingress安全输入；当前仓库仍没有reload current revision的真实caller/UI/watcher或manager API。C2不得把same-value selection、现有`SourceChanged`或ordinary `.osk`即时dispose链冒充该caller。
- settings/config/hotkey只请求selection，same-value在准备前短路；startup scanner是单次reconcile而非watcher。filesystem-backed skin继续被editor、update import与external edit拒绝。普通Realm `ExternalEditOverlay`的new-instance后立即dispose旧实例不是managed caller，也没有barrier，不能复用。

## publication 与 owner 地雷

- `SkinManager`的`CurrentSkinInfo`/`CurrentSkin` pair只在manager内coherent，随后`SourceChanged`是事件扇出；没有package revision publication对象、consumer snapshot/registry、ack、detach receipt或old-instance retire queue。pair commit、`SkinReloadableDrawable`和`BmsAsyncNoteDrawable`都不是整包atomic reload。
- `BmsPlayfield`在loader一次读取skin geometry并缓存layout/profile，不监听`SourceChanged`。BMS Note/LN gameplay与pre-start preview是独立per-host异步consumer；core/mania drawable还混合同步、scheduler和next-update更新；菜单背景会在fade/expire期间继续持有旧`Skin`。当前publication可产生旧geometry+新shell/note等mixed revision。
- exact capsule经factory转入`Skin` owning store；`Skin.Dispose()`释放texture/sample/fallback store/capsule，`BmsLegacySkin.Dispose()`还取消package note preparation。成功selection没有全consumer detach后的退役协议，既有产品测试手工dispose superseded managed skin。即时dispose可能破坏旧consumer，不dispose则owner生命周期未闭合。
- 现有测试只证明capsule/store ownership、guarded selection和per-host A→B，不证明same-ID revision gate、全host barrier、failure保留exact旧pair/owner、detach后dispose once或reload latest-wins/reentrant/cancel/shutdown join。

## 重新开门门槛

- C2先冻结唯一真实触发方式、允许场景（尤其live gameplay是否允许或延后）和全部consumer participation/publication/detach/retirement协议；红测必须从该可达caller跨manager直到真实renderer/owner，不能只发明barrier DTO。
- participant inventory须从完整production object graph取得，并区分coherent重建consumer、lease-only lifecycle holder与已证明不持旧owner者；menu/shell/background/transition只作旧owner生命周期覆盖，不扩大为作者皮肤面。冻结prepare/commit窗口的动态attach/detach：prepare中新增consumer不漏barrier，commit前detach不悬挂retire，commit后late attach只取得已提交revision和对应lease。
- 纵切须统一ordinary Realm `.osk`、managed与external来源：fresh authoritative Realm/path/owner/freeze/capture/factory复核、完整immutable revision与new skin instance后台准备、generation/current-selection/revision gate、所有consumer coherent publication、失败保留exact旧revision、全consumer detach后幂等dispose旧owner，以及latest-wins/reentrant/首个不可逆边界前取消/shutdown exact claim-reap-join与脱敏诊断。
- current external unregister只能在coherent fallback/new revision已经发布且所有旧consumer detach后做pure-Realm compare-remove；失败时source record与旧pair都应保持，不得先注销再尝试切换。ordinary `.osk`现有new-instance后立即dispose旧实例的旁路必须迁入统一协议或被禁用。
- current managed Delete也必须把protected fallback publication与旧revision detach接入C2协议；只有二者成功后才可沿用C1既有journal/physical detach，失败不得先删目录。测试至少覆盖same-record-ID/content-revision三源、attach-during-prepare、detach-before-commit、late attach、跨revision fade与`ExternalEditOverlay` reachable bypass。
- 禁止manager-only reload API、强制同ID selection、逐组件`SourceChanged`拼接、即时dispose旧owner或没有真实caller/consumer的barrier/DTO foundation。managed delete journal/detach是独立operation合同，不提供reload的全renderer生命周期事务。

## 关联入口

- exact capsule/owner：[[reference_skin_package_revision_capsule]]。
- managed selection与`551a`协调：[[reference_skin_managed_folder_selection]]、[[reference_skin_managed_folder_scanner]]。
- authoring/product边界：[[project_oms_bms_skin_authoring]]。
- release-ready差距与external前置工作包：[[project_oms_skin_product_progress]]。
